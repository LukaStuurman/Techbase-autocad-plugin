using System.IO;
using System.Text.Json;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Techbase.Blocks;

internal static class ExistingMofUpdateDiagnostics
{
    internal static void Run(Catalog catalog, string output)
    {
        var previous = Environment.GetEnvironmentVariable("TECHBASE_PREVIOUS_BLOCK_SOURCE");
        if (string.IsNullOrEmpty(previous)) return;
        var tool = catalog.Tools.First(t => t.BlockName == "BESTAANDE MOF");
        var current = catalog.ResolveSource(tool);
        var checks = new List<string>();
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException("Mof-update: " + message); checks.Add(message); }
        using var db = new Database(true, true);
        using var working = new WorkingDatabaseScope(db);
        using (var source = new Database(false, true))
        {
            source.ReadDwgFile(previous, FileOpenMode.OpenForReadAndAllShare, true, null); source.CloseInput(true);
            using var tx = source.TransactionManager.StartTransaction();
            var table = (BlockTable)tx.GetObject(source.BlockTableId, OpenMode.ForRead);
            source.WblockCloneObjects(new ObjectIdCollection(new[] { table[tool.BlockName] }), db.BlockTableId,
                new IdMapping(), DuplicateRecordCloning.Ignore, false);
        }
        ObjectId canonical;
        string canonicalSignature;
        using (var tx = db.TransactionManager.StartTransaction())
        {
            canonical = ((BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead))[tool.BlockName];
            canonicalSignature = Signature(tx, canonical);
        }
        var oldId = SourceBlockImporter.Import(db, previous, tool.BlockName);
        string oldSignature;
        using (var tx = db.TransactionManager.StartTransaction()) oldSignature = Signature(tx, oldId);
        string expected;
        using (var source = new Database(false, true))
        {
            source.ReadDwgFile(current, FileOpenMode.OpenForReadAndAllShare, true, null); source.CloseInput(true);
            using var sourceWorking = new WorkingDatabaseScope(source);
            using var tx = source.TransactionManager.StartTransaction();
            Typography.NormalizeSource(source, tx, "TB_COMPARE_MOF");
            var table = (BlockTable)tx.GetObject(source.BlockTableId, OpenMode.ForRead);
            if (Plugin.Preferences.BlockTextHeight(tool.BlockName) is double height)
                BlockTextHeight.Apply(source, tx, table[tool.BlockName], height);
            expected = Signature(tx, table[tool.BlockName]);
        }
        Check(oldSignature != expected, "BESTAANDE MOF in nieuwe bron verschilt daadwerkelijk van 1.5.0");
        var updated = BlockInserter.Import(db, catalog, tool);
        using (var tx = db.TransactionManager.StartTransaction())
        {
            Check(updated != oldId, "oude pluginversie in een tekening krijgt een aparte nieuwe blockdefinitie");
            Check(updated != canonical && Signature(tx, canonical) == canonicalSignature, "oude gelijknamige BESTAANDE MOF blijft beschermd en wordt niet geplaatst");
            Check(Signature(tx, updated) == expected, "nieuwe definitie heeft dezelfde geometrie en teksten als de bijgewerkte bron");
            Check(Signature(tx, oldId) == oldSignature, "oude definitie blijft intact");
            using var block = BlockInserter.CreateReference(db, tx, updated, tool, new Point3d(20, 0, 0), db.Clayer, Matrix3d.Identity);
            using var preview = new PlacementJig(db, tx, block, tool.Rotation);
            var record = (BlockTableRecord)tx.GetObject(updated, OpenMode.ForRead);
            var expectedAttributes = record.Cast<ObjectId>().Select(id => tx.GetObject(id, OpenMode.ForRead)).OfType<AttributeDefinition>().Count(a => !a.Constant);
            Check(preview.Attributes.Count == expectedAttributes, "plaatsvoorbeeld toont attributen van de nieuwe mof");
            BlockInserter.Append(db, tx, block, false);
            Check(block.BlockTableRecord == updated && block.AttributeCollection.Count == expectedAttributes, "plaatsing gebruikt de nieuwe mof met bijbehorende attributen");
            tx.Commit();
        }
        db.SaveAs(Path.Combine(output, "Bestaande mof - update.dwg"), DwgVersion.Current);
        db.DxfOut(Path.Combine(output, "Bestaande mof - update.dxf"), 16, DwgVersion.Current);
        using (var reopened = new Database(false, true))
        {
            reopened.ReadDwgFile(Path.Combine(output, "Bestaande mof - update.dwg"), FileOpenMode.OpenForReadAndAllShare, true, null); reopened.CloseInput(true);
            using var reopenedWorking = new WorkingDatabaseScope(reopened);
            using var tx = reopened.TransactionManager.StartTransaction();
            Check(Signature(tx, reopened.GetObjectId(false, updated.Handle, 0)) == expected, "opgeslagen nieuwe mof teruggelezen");
            Check(Signature(tx, reopened.GetObjectId(false, oldId.Handle, 0)) == oldSignature, "opgeslagen oude definitie blijft beschermd");
        }
        File.WriteAllText(Path.Combine(output, "bestaande-mof-update.json"), JsonSerializer.Serialize(new { Status = "PASS", Checks = checks,
            Old = JsonSerializer.Deserialize<object>(oldSignature), Updated = JsonSerializer.Deserialize<object>(expected) }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string Signature(Transaction tx, ObjectId root)
    {
        var entries = new List<string>();
        double[] Point(Point3d p) => new[] { Math.Round(p.X, 8), Math.Round(p.Y, 8), Math.Round(p.Z, 8) };
        void Visit(ObjectId blockId, HashSet<ObjectId> path)
        {
            if (!path.Add(blockId)) throw new InvalidOperationException("Recursieve blockdefinitie.");
            var block = (BlockTableRecord)tx.GetObject(blockId, OpenMode.ForRead);
            foreach (ObjectId id in block)
            {
                if (tx.GetObject(id, OpenMode.ForRead) is not Entity entity) continue;
                object geometry;
                if (entity is MText m) geometry = new { Text = m.Text, Position = Point(m.Location), Height = m.TextHeight, m.Width, Direction = m.Direction.ToArray() };
                else if (entity is DBText t) geometry = new { Text = t.TextString, Position = Point(t.Position), Height = t.Height, t.Rotation };
                else if (entity is BlockReference reference)
                { geometry = reference.BlockTransform.ToArray(); Visit(reference.BlockTableRecord, new HashSet<ObjectId>(path)); }
                else
                {
                    try { var bounds = entity.GeometricExtents; geometry = new { Min = Point(bounds.MinPoint), Max = Point(bounds.MaxPoint) }; }
                    catch (Autodesk.AutoCAD.Runtime.Exception) { geometry = "no extents"; }
                }
                entries.Add(JsonSerializer.Serialize(new { Type = entity.GetType().Name, entity.Layer, Geometry = geometry }));
            }
        }
        Visit(root, new HashSet<ObjectId>()); entries.Sort(StringComparer.Ordinal); return JsonSerializer.Serialize(entries);
    }
}
