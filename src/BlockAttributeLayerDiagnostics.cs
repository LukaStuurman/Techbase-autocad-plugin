using System.IO;
using System.Text.Json;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Techbase.Blocks;

internal static class BlockAttributeLayerDiagnostics
{
    internal static void Run(Catalog catalog, string output)
    {
        var folder = Path.Combine(output, "Attribuutlagen"); Directory.CreateDirectory(folder);
        var checks = new List<string>(); var references = new List<ObjectId>();
        void Check(bool ok, string label) { if (!ok) throw new InvalidOperationException("Attribuutlaag: " + label); checks.Add(label); }
        using var db = new Database(true, true);
        using var working = new WorkingDatabaseScope(db);
        foreach (var name in new[] { "NIEUWE MOF", "BESTAANDE MOF", "MOF bestaand-nieuw", "RT 1-12 LS" })
        {
            var tool = catalog.Tools.First(t => t.BlockName == name);
            var definition = BlockInserter.Import(db, catalog, tool);
            using var tx = db.TransactionManager.StartTransaction();
            var existing = BlockLayers.Ensure(db, tx, 0); var direction = BlockLayers.Ensure(db, tx, 6);
            var root = (BlockTableRecord)tx.GetObject(definition, OpenMode.ForRead);
            var definitions = root.Cast<ObjectId>().Select(id => tx.GetObject(id, OpenMode.ForRead)).OfType<AttributeDefinition>()
                .Where(a => !a.Constant).ToDictionary(a => a.Tag, StringComparer.OrdinalIgnoreCase);
            using var block = BlockInserter.CreateReference(db, tx, definition, tool, new Point3d(references.Count * 25, 0, 0), existing, Matrix3d.Identity);
            BlockInserter.Append(db, tx, block, false); references.Add(block.ObjectId);
            var attributes = block.AttributeCollection.Cast<ObjectId>().Select(id => (AttributeReference)tx.GetObject(id, OpenMode.ForRead)).ToArray();
            Check(attributes.Length > 0 && attributes.All(a => a.LayerId == definitions[a.Tag].LayerId && a.Color.ColorMethod == definitions[a.Tag].Color.ColorMethod && a.Color.ColorValue == definitions[a.Tag].Color.ColorValue), name + ": bronlaag en bronkleur behouden");
            block.LayerId = direction;
            Check(attributes.All(a => a.LayerId == definitions[a.Tag].LayerId), name + ": laagwisseling zet attribuut niet vast op oude kabel");
            if (name == "NIEUWE MOF")
                Check(attributes.All(a => a.Layer == "0" && a.ColorIndex == 256), "NIEUWE MOF: tekst erft oranje van richting 6 zonder ATTSYNC");
            if (name == "BESTAANDE MOF")
                Check(attributes.All(a => a.ColorIndex == 7), "BESTAANDE MOF: vaste tekstkleur 7 blijft behouden");
            using var jig = new PlacementJig(db, tx, block, tool.Rotation);
            Check(jig.Attributes.Where(a => definitions[a.Tag].Layer == "0").All(a => a.LayerId == direction), name + ": los cursorvoorbeeld volgt blocklaag");
            tx.Commit();
        }
        // Repair an old pinned attribute without replacing its text or placement.
        string contents; Point3d position; double height;
        using (var tx = db.TransactionManager.StartTransaction())
        {
            var block = (BlockReference)tx.GetObject(references[0], OpenMode.ForRead);
            var attribute = (AttributeReference)tx.GetObject(block.AttributeCollection.Cast<ObjectId>().Single(), OpenMode.ForWrite);
            attribute.LayerId = BlockLayers.Ensure(db, tx, 0);
            contents = attribute.TextString = "Eigen EM / behouden";
            position = attribute.Position += new Vector3d(1.2, 2.3, 0);
            height = attribute.Height = .61;
            tx.Commit();
        }
        var repaired = BlockAttributeLayers.Repair(db, new[] { references[0] });
        Check(repaired == (1, 0), "oude pluginattribuutlaag hersteld");
        using (var tx = db.TransactionManager.StartTransaction())
        {
            var block = (BlockReference)tx.GetObject(references[0], OpenMode.ForRead);
            var attribute = (AttributeReference)tx.GetObject(block.AttributeCollection.Cast<ObjectId>().Single(), OpenMode.ForRead);
            Check(attribute.Layer == "0" && attribute.TextString == contents && attribute.Position == position && attribute.Height == height, "herstel bewaart eigen tekst, positie en hoogte");
        }
        Check(BlockAttributeLayers.Repair(db, references) == (0, 0), "herhaald herstel heeft geen bijwerkingen");
        var drawing = Path.Combine(folder, "Mof laagwisseling.dwg");
        var handles = references.Select(id => id.Handle).ToArray();
        db.SaveAs(drawing, DwgVersion.Current); db.DxfOut(Path.Combine(folder, "Mof laagwisseling.dxf"), 16, DwgVersion.Current);
        using (var saved = new Database(false, true))
        {
            saved.ReadDwgFile(drawing, FileOpenMode.OpenForReadAndAllShare, true, null); saved.CloseInput(true);
            using var tx = saved.TransactionManager.StartTransaction();
            foreach (var handle in handles)
            {
                var block = (BlockReference)tx.GetObject(saved.GetObjectId(false, handle, 0), OpenMode.ForRead);
                var definition = (BlockTableRecord)tx.GetObject(block.IsDynamicBlock ? block.DynamicBlockTableRecord : block.BlockTableRecord, OpenMode.ForRead);
                var layers = definition.Cast<ObjectId>().Select(id => tx.GetObject(id, OpenMode.ForRead)).OfType<AttributeDefinition>().ToDictionary(a => a.Tag, a => a.Layer, StringComparer.OrdinalIgnoreCase);
                Check(block.Layer == BlockLayers.Name(6) && block.AttributeCollection.Cast<ObjectId>().Select(id => (AttributeReference)tx.GetObject(id, OpenMode.ForRead)).All(a => a.Layer == layers[a.Tag]), "opnieuw geopende bronlaag/laagovername " + handle);
            }
        }
        File.WriteAllText(Path.Combine(folder, "service.json"), JsonSerializer.Serialize(new { Status = "PASS", Checks = checks }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
