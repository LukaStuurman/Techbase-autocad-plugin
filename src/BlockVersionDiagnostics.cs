using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Techbase.Blocks;

internal static class BlockVersionDiagnostics
{
    internal static void Run(Catalog catalog, string output)
    {
        var checks = new List<string>();
        void Check(bool ok, string name) { if (!ok) throw new InvalidOperationException("Blockversie: " + name); checks.Add(name); }
        var tool = catalog.Tools.First(t => t.BlockName == "NIEUWE MOF");
        var sourcePath = catalog.ResolveSource(tool);
        string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        var before = Hash(sourcePath);
        using var db = new Database(true, true);
        using var working = new WorkingDatabaseScope(db);
        ObjectId oldId;
        var expectedAttributes = 0;
        var dynamic = false;
        using (var source = new Database(false, true))
        {
            source.ReadDwgFile(sourcePath, FileOpenMode.OpenForReadAndAllShare, true, null); source.CloseInput(true);
            using var tx = source.TransactionManager.StartTransaction();
            var table = (BlockTable)tx.GetObject(source.BlockTableId, OpenMode.ForRead);
            // Prefer a real dynamic source with attributes, so a legacy name
            // collision also exercises AutoCAD's anonymous dynamic records.
            tool = catalog.Tools.Where(t => catalog.ResolveSource(t) == sourcePath && table.Has(t.BlockName))
                .FirstOrDefault(t =>
                {
                    var candidate = (BlockTableRecord)tx.GetObject(table[t.BlockName], OpenMode.ForRead);
                    return candidate.IsDynamicBlock && candidate.Cast<ObjectId>().Any(id =>
                        tx.GetObject(id, OpenMode.ForRead) is AttributeDefinition a && !a.Constant);
                }) ?? tool;
            var record = (BlockTableRecord)tx.GetObject(table[tool.BlockName], OpenMode.ForRead);
            dynamic = record.IsDynamicBlock;
            expectedAttributes = record.Cast<ObjectId>().Count(id => tx.GetObject(id, OpenMode.ForRead) is AttributeDefinition a && !a.Constant);
            var map = new IdMapping();
            source.WblockCloneObjects(new ObjectIdCollection(new[] { record.ObjectId }), db.BlockTableId, map, DuplicateRecordCloning.Ignore, false);
            oldId = map[record.ObjectId].Value; tx.Commit();
        }
        ObjectId oldInsert;
        int oldGeometry;
        using (var tx = db.TransactionManager.StartTransaction())
        {
            var old = (BlockTableRecord)tx.GetObject(oldId, OpenMode.ForWrite);
            foreach (var id in old.Cast<ObjectId>().ToArray())
                if (tx.GetObject(id, OpenMode.ForWrite) is AttributeDefinition attribute) attribute.Erase();
            oldGeometry = old.Cast<ObjectId>().Count(id => !id.IsErased);
            using var reference = BlockInserter.CreateReference(db, tx, oldId, tool, Point3d.Origin, db.Clayer, Matrix3d.Identity);
            oldInsert = BlockInserter.Append(db, tx, reference, false).Single();
            Check(reference.AttributeCollection.Count == 0, "oude plaatsing zonder tekst als echte uitgangssituatie");
            tx.Commit();
        }
        var currentId = BlockInserter.Import(db, catalog, tool);
        Check(currentId != oldId, "plugin gebruikt eigen brondefinitie bij gelijke blocknaam");
        ObjectId currentInsert;
        using (var tx = db.TransactionManager.StartTransaction())
        {
            var current = (BlockTableRecord)tx.GetObject(currentId, OpenMode.ForRead);
            using var reference = BlockInserter.CreateReference(db, tx, currentId, tool, new Point3d(10, 0, 0), db.Clayer, Matrix3d.Identity);
            currentInsert = BlockInserter.Append(db, tx, reference, false).Single();
            Check(expectedAttributes > 0 && reference.AttributeCollection.Count == expectedAttributes, "nieuwe plaatsing bevat alle actuele attribuutteksten");
            Check(current.IsDynamicBlock == dynamic && reference.IsDynamicBlock == dynamic, "dynamische bronfunctie behouden naast oude versie");
            var old = (BlockTableRecord)tx.GetObject(oldId, OpenMode.ForRead);
            Check(old.Name == tool.BlockName && old.Cast<ObjectId>().Count(id => !id.IsErased) == oldGeometry,
                "oude blockdefinitie en geometrie ongewijzigd");
            var original = (BlockReference)tx.GetObject(oldInsert, OpenMode.ForRead);
            Check(original.BlockTableRecord == oldId && original.AttributeCollection.Count == 0 && original.Position.IsEqualTo(Point3d.Origin),
                "bestaande plaatsing niet veranderd of van tekst voorzien");
            tx.Commit();
        }
        Check(BlockInserter.Import(db, catalog, tool) == currentId, "dezelfde bronversie hergebruikt eigen definitie");
        var drawing = Path.Combine(output, "Blockversies - controle.dwg");
        db.SaveAs(drawing, DwgVersion.AC1032); db.DxfOut(Path.Combine(output, "Blockversies - controle.dxf"), 16, DwgVersion.AC1032);
        var currentHandle = currentId.Handle;
        using (var reopened = new Database(false, true))
        {
            reopened.ReadDwgFile(drawing, FileOpenMode.OpenForReadAndAllShare, true, null); reopened.CloseInput(true);
            var persistedId = reopened.GetObjectId(false, currentHandle, 0);
            Check(BlockInserter.Import(reopened, catalog, tool) == persistedId, "bronherkomst blijft na opslaan en heropenen bruikbaar");
            using var tx = reopened.TransactionManager.StartTransaction();
            Check(((BlockReference)tx.GetObject(reopened.GetObjectId(false, currentInsert.Handle, 0), OpenMode.ForRead)).AttributeCollection.Count == expectedAttributes,
                "actuele tekst teruggelezen uit opgeslagen CAD");
        }
        Check(Hash(sourcePath) == before, "meegeleverde bron-DWG niet gewijzigd");

        // Independent nested fixture: both ordinary names and generated-looking
        // names are already occupied by obsolete definitions without metadata.
        var fixture = Path.Combine(output, "blockversie-bron.dwg");
        WriteSource(fixture, "Actueel 1");
        string canonicalRoot, canonicalChild;
        using (var clean = new Database(true, true))
        {
            var rootId = SourceBlockImporter.Import(clean, fixture, "Parent");
            using var tx = clean.TransactionManager.StartTransaction();
            var root = (BlockTableRecord)tx.GetObject(rootId, OpenMode.ForRead); canonicalRoot = root.Name;
            var child = root.Cast<ObjectId>().Select(id => tx.GetObject(id, OpenMode.ForRead)).OfType<BlockReference>().Single();
            canonicalChild = ((BlockTableRecord)tx.GetObject(child.BlockTableRecord, OpenMode.ForRead)).Name;
        }
        using var nested = new Database(true, true);
        using var nestedWorking = new WorkingDatabaseScope(nested);
        using (var tx = nested.TransactionManager.StartTransaction())
        {
            foreach (var name in new[] { "Parent", "Child", canonicalRoot, canonicalChild }) AddTextDefinition(nested, tx, name, "Oud");
            tx.Commit();
        }
        var first = SourceBlockImporter.Import(nested, fixture, "Parent");
        using (var tx = nested.TransactionManager.StartTransaction())
        {
            var table = (BlockTable)tx.GetObject(nested.BlockTableId, OpenMode.ForRead);
            var root = (BlockTableRecord)tx.GetObject(first, OpenMode.ForRead);
            Check(root.Name != canonicalRoot && first != table["Parent"], "bezette pluginnaam zonder herkomst wordt niet vertrouwd");
            var child = root.Cast<ObjectId>().Select(id => tx.GetObject(id, OpenMode.ForRead)).OfType<BlockReference>().Single();
            Check(child.BlockTableRecord != table["Child"] && child.BlockTableRecord != table[canonicalChild], "geneste brondefinitie krijgt ook eigen versie");
            Check(ReadNestedText(nested, tx, first) == "Actueel 1", "nieuwe geneste tekst ondanks twee naamconflicten");
            foreach (var name in new[] { "Parent", "Child", canonicalRoot, canonicalChild })
                Check(ReadText(tx, table[name]) == "Oud", "bestaande definitie beschermd: " + name);
        }
        Check(SourceBlockImporter.Import(nested, fixture, "Parent") == first, "eigen alternatief bij naamconflict wordt hergebruikt");
        WriteSource(fixture, "Actueel 2");
        var second = SourceBlockImporter.Import(nested, fixture, "Parent");
        using (var tx = nested.TransactionManager.StartTransaction())
        {
            Check(second != first && ReadNestedText(nested, tx, second) == "Actueel 2", "gewijzigde bron krijgt automatisch nieuwe definitie");
            Check(ReadNestedText(nested, tx, first) == "Actueel 1", "eerdere bronversie blijft ongewijzigd");
        }
        File.WriteAllText(Path.Combine(output, "blockversies.json"), JsonSerializer.Serialize(new { Status = "PASS", SourceBlock = tool.BlockName, Dynamic = dynamic, AttributeCount = expectedAttributes, Checks = checks },
            new JsonSerializerOptions { WriteIndented = true }));
    }

    private static ObjectId AddTextDefinition(Database db, Transaction tx, string name, string text)
    {
        var table = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForWrite);
        using var record = new BlockTableRecord { Name = name };
        var id = table.Add(record); tx.AddNewlyCreatedDBObject(record, true);
        using var label = new DBText { TextString = text, Height = 1, Position = Point3d.Origin };
        record.AppendEntity(label); tx.AddNewlyCreatedDBObject(label, true); return id;
    }
    private static void WriteSource(string path, string text)
    {
        using var db = new Database(true, true);
        using (var tx = db.TransactionManager.StartTransaction())
        {
            var child = AddTextDefinition(db, tx, "Child", text);
            var table = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForWrite);
            using var parent = new BlockTableRecord { Name = "Parent" };
            table.Add(parent); tx.AddNewlyCreatedDBObject(parent, true);
            using var reference = new BlockReference(Point3d.Origin, child);
            parent.AppendEntity(reference); tx.AddNewlyCreatedDBObject(reference, true); tx.Commit();
        }
        db.SaveAs(path, DwgVersion.AC1032);
    }
    private static string ReadText(Transaction tx, ObjectId id) => ((BlockTableRecord)tx.GetObject(id, OpenMode.ForRead))
        .Cast<ObjectId>().Select(oid => tx.GetObject(oid, OpenMode.ForRead)).OfType<DBText>().Single().TextString;
    private static string ReadNestedText(Database db, Transaction tx, ObjectId root) => ReadText(tx,
        ((BlockTableRecord)tx.GetObject(root, OpenMode.ForRead)).Cast<ObjectId>().Select(id => tx.GetObject(id, OpenMode.ForRead))
            .OfType<BlockReference>().Single().BlockTableRecord);
}
