using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Techbase.Blocks;

internal static class PlacementDiagnostics
{
    internal static void Run(Catalog catalog, string output)
    {
        var checks = new List<string>();
        void Check(bool condition, string name)
        { if (!condition) throw new InvalidOperationException("Plaatsvoorbeeld: " + name); checks.Add(name); }
        using var db = new Database(true, true);
        using var working = new WorkingDatabaseScope(db);
        var tested = 0;
        foreach (var tool in catalog.Tools)
        {
            using var tx = db.TransactionManager.StartTransaction();
            var id = BlockInserter.Import(db, catalog, tool);
            var record = (BlockTableRecord)tx.GetObject(id, OpenMode.ForRead);
            if (!record.Cast<ObjectId>().Any(oid => tx.GetObject(oid, OpenMode.ForRead) is AttributeDefinition a && !a.Constant)) continue;
            tested++;
            // Compare with a real insert, then independently transform its actual
            // attribute geometry. This catches missing, unrotated or misaligned text.
            var ucs = tested % 2 == 0 ? Matrix3d.Rotation(Math.PI / 2, Vector3d.ZAxis, Point3d.Origin) : Matrix3d.Identity;
            var layer = BlockInserter.DefaultLayer(db, tx, "PREVIEW_SOURCE");
            var destinationLayer = BlockInserter.DefaultLayer(db, tx, "PREVIEW_DESTINATION");
            using var original = BlockInserter.CreateReference(db, tx, id, tool, Point3d.Origin, layer, ucs);
            BlockInserter.Append(db, tx, original, false);
            var references = original.AttributeCollection.Cast<ObjectId>()
                .Select(oid => (AttributeReference)tx.GetObject(oid, OpenMode.ForRead)).ToArray();
            using var preview = BlockInserter.CreateReference(db, tx, id, tool, Point3d.Origin, layer, ucs);
            var space = (BlockTableRecord)tx.GetObject(db.CurrentSpaceId, OpenMode.ForRead);
            var before = space.Cast<ObjectId>().Count();
            using (var jig = new PlacementJig(db, tx, preview, tool.Rotation))
            {
                Check(jig.Attributes.Count == references.Length, tool.Name + ": tekst aanwezig vóór het invoegpunt");
                foreach (var angle in new[] { 0.0, Math.PI / 2, Math.PI, Math.PI * 1.5, tool.Rotation })
                {
                    jig.MoveTo(new Point3d(tested * 50, 50 + angle * 10, 2));
                    jig.SetAngle(angle);
                    var delta = preview.BlockTransform * original.BlockTransform.Inverse();
                    for (var index = 0; index < references.Length; index++)
                    {
                        using var expected = (AttributeReference)references[index].Clone();
                        expected.TransformBy(delta);
                        Compare(expected, jig.Attributes[index], tool.Name + ": bewegende tekst " + references[index].Tag, Check);
                    }
                }
                preview.LayerId = destinationLayer; jig.RefreshAttributes();
                Check(jig.Attributes.Where(a => references.First(r => r.Tag == a.Tag).Layer == "0")
                    .All(a => a.LayerId == destinationLayer), tool.Name + ": laag-0-tekst erft nieuwe laag");
                Check(preview.ObjectId.IsNull && space.Cast<ObjectId>().Count() == before,
                    tool.Name + ": cursorvoorbeeld voegt geen tijdelijk object toe");
                BlockInserter.Append(db, tx, preview, false);
                var saved = preview.AttributeCollection.Cast<ObjectId>()
                    .Select(oid => (AttributeReference)tx.GetObject(oid, OpenMode.ForRead)).ToArray();
                Check(saved.Length == jig.Attributes.Count, tool.Name + ": geen dubbele attributen");
                for (var index = 0; index < saved.Length; index++)
                    Compare(jig.Attributes[index], saved[index], tool.Name + ": definitieve tekst gelijk aan voorbeeld", Check);
            }
            tx.Commit();
        }
        Check(tested > 0, "echte paletteblocks met attributen onderzocht");
        db.SaveAs(Path.Combine(output, "Plaatsvoorbeeld - controle.dwg"), DwgVersion.AC1032);
        db.DxfOut(Path.Combine(output, "Plaatsvoorbeeld - controle.dxf"), 16, DwgVersion.AC1032);
        File.WriteAllText(Path.Combine(output, "plaatsvoorbeeld.json"), JsonSerializer.Serialize(
            new { Status = "PASS", AttributeTools = tested, Checks = checks }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void Compare(AttributeReference expected, AttributeReference actual, string name, Action<bool, string> check)
    {
        var angle = expected.Rotation - actual.Rotation;
        check(expected.Position.DistanceTo(actual.Position) < 1e-7 &&
            (expected.IsDefaultAlignment || expected.AlignmentPoint.DistanceTo(actual.AlignmentPoint) < 1e-7) &&
            Math.Abs(Math.Sin(angle)) < 1e-8 && Math.Cos(angle) > 0 &&
            expected.Normal.IsEqualTo(actual.Normal) && Math.Abs(expected.Height - actual.Height) < 1e-8 &&
            expected.TextString == actual.TextString && expected.Invisible == actual.Invisible &&
            expected.IsMTextAttribute == actual.IsMTextAttribute, name);
        if (actual.IsMTextAttribute)
        {
            using var wanted = expected.MTextAttribute;
            using var text = actual.MTextAttribute;
            check(wanted.Location.DistanceTo(text.Location) < 1e-7 && wanted.Contents == text.Contents &&
                wanted.Direction.IsEqualTo(text.Direction), name + " (meerdere regels)");
        }
    }
}
