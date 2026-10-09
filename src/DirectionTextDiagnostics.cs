using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Techbase.Blocks;

internal static class DirectionTextDiagnostics
{
    internal static void Run()
    {
        var output = Environment.GetEnvironmentVariable("TECHBASE_TEST_OUTPUT") ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Techbase", "Blocks", "Validation", "Teksten");
        Directory.CreateDirectory(output);
        var checks = new List<string>();
        void Check(bool ok, string name) { if (!ok) throw new InvalidOperationException("Richtingstekst: " + name); checks.Add(name); }
        using var db = new Database(true, true);
        using var working = new WorkingDatabaseScope(db);
        var catalog = new Catalog(Plugin.DefaultPalette);
        var templateIds = new List<ObjectId>();
        foreach (var ov in new[] { false, true })
        {
            var tool = DirectionText.Template(catalog, ov);
            using var tx = db.TransactionManager.StartTransaction();
            var definition = BlockInserter.Import(db, catalog, tool);
            var record = (BlockTableRecord)tx.GetObject(definition, OpenMode.ForRead);
            var expected = record.Cast<ObjectId>().Select(id => tx.GetObject(id, OpenMode.ForRead)).OfType<MText>().ToArray();
            using var block = BlockInserter.CreateReference(db, tx, definition, tool, new Point3d(ov ? 30 : 0, 0, 0), db.Clayer, Matrix3d.Identity);
            var ids = BlockInserter.Append(db, tx, block, false, burst: true); templateIds.AddRange(ids);
            Check(block.IsErased && ids.Count == 13 && ids.All(id => tx.GetObject(id, OpenMode.ForRead) is MText),
                (ov ? "OV" : "LS") + " bronblock levert 13 losse MTexten zonder block/attribuutdefinities");
            var actual = ids.Select(id => (MText)tx.GetObject(id, OpenMode.ForRead)).ToArray();
            Check(actual.Select(t => t.Contents).SequenceEqual(expected.Select(t => t.Contents)) &&
                actual.Select(t => t.Layer).SequenceEqual(expected.Select(t => t.Layer)), (ov ? "OV" : "LS") + " broninhoud en richtingslagen behouden");
            tx.Commit();
        }
        // BURST must preserve entered values, rather than exposing attribute tags.
        using (var tx = db.TransactionManager.StartTransaction())
        {
            var tool = catalog.Tools.First(t => t.BlockName == "NIEUWE MOF");
            var def = BlockInserter.Import(db, catalog, tool);
            using var block = BlockInserter.CreateReference(db, tx, def, tool, new Point3d(60, 0, 0), db.Clayer, Matrix3d.Identity);
            var ids = BlockInserter.Append(db, tx, block, false, new Dictionary<string, string> { ["MOF_TEKST"] = "Eigen waarde 150Al" }, burst: true);
            Check(ids.Select(id => tx.GetObject(id, OpenMode.ForRead)).OfType<MText>().Any(t => t.Text.Contains("Eigen waarde 150Al")) &&
                ids.All(id => tx.GetObject(id, OpenMode.ForRead) is not AttributeDefinition), "BURST behoudt eigen meerregelige attribuutwaarde");
            tx.Commit();
        }
        ObjectId a, b;
        using (var tx = db.TransactionManager.StartTransaction())
        {
            var space = (BlockTableRecord)tx.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
            ObjectId Add(string contents, string layerName, Point3d point)
            {
                using var text = new MText(); text.SetDatabaseDefaults(db); text.TextHeight = 1; text.Width = 10;
                text.Contents = contents; text.Location = point; text.LayerId = BlockInserter.DefaultLayer(db, tx, layerName);
                var id = space.AppendEntity(text); tx.AddNewlyCreatedDBObject(text, true); return id;
            }
            a = Add("{\\C1;Ri 1 150Al}\\PExtra regel", "TEST_TEXT_A", new Point3d(0, 20, 0));
            b = Add("Ri 2 150Al", "TEST_TEXT_B", new Point3d(0, 25, 0)); tx.Commit();
        }
        double[] heights;
        string[] contents;
        using (var tx = db.TransactionManager.StartTransaction())
        {
            var texts = new[] { b, a }.Select(id => (MText)tx.GetObject(id, OpenMode.ForRead)).ToArray();
            var style = Typography.Style(db, tx);
            heights = texts.Select(t => { using var clone = (MText)t.Clone(); Typography.Apply(clone, style); return clone.ActualHeight; }).ToArray();
            contents = texts.Select(t => t.Contents).ToArray(); tx.Commit();
        }
        var stackIds = new List<ObjectId>();
        foreach (var degrees in new[] { 0, 45, 90, 135, 180, 225, 270, 315 })
        {
            var angle = degrees * Math.PI / 180;
            var start = new Point3d(degrees, 50, 0); var end = start + new Vector3d(10 * Math.Cos(angle), 10 * Math.Sin(angle), 0);
            var ids = DirectionText.Apply(db, new[] { b, a }, true, start, end, Matrix3d.Identity); stackIds.AddRange(ids);
            using var tx = db.TransactionManager.StartTransaction();
            var texts = ids.Select(id => (MText)tx.GetObject(id, OpenMode.ForRead)).ToArray();
            var flip = degrees > 90 && degrees <= 270; var origin = flip ? end : start;
            var perp = new Vector3d(-Math.Sin(angle), Math.Cos(angle), 0);
            Check(texts[0].Location.DistanceTo(origin + perp * 0.4) < 1e-8 &&
                texts[1].Location.DistanceTo(origin + perp * (0.4 + heights[0] + 0.2)) < 1e-8,
                "Lisp-offset en werkelijke hoogte/ruimte in klikvolgorde " + degrees);
            var wanted = angle + (flip ? Math.PI : 0);
            Check(texts.All(t => t.Direction.IsEqualTo(new Vector3d(Math.Cos(wanted), Math.Sin(wanted), 0)) &&
                t.Attachment == (flip ? AttachmentPoint.TopLeft : AttachmentPoint.BottomLeft)), "leesbare draairichting en Lisp-ankering " + degrees);
            Check(texts.Select(t => t.Contents).SequenceEqual(contents) && texts[0].Layer == "TEST_TEXT_B" && texts[1].Layer == "TEST_TEXT_A",
                "kopie behoudt inhoud, inline opmaak en oorspronkelijke lagen " + degrees);
        }
        using (var tx = db.TransactionManager.StartTransaction())
            Check(((MText)tx.GetObject(a, OpenMode.ForRead)).Location.IsEqualTo(new Point3d(0, 20, 0)) &&
                ((MText)tx.GetObject(b, OpenMode.ForRead)).Location.IsEqualTo(new Point3d(0, 25, 0)), "kopiëren wijzigt oorspronkelijke teksten niet");
        var ucs = Matrix3d.Displacement(new Vector3d(100, 200, 5)) * Matrix3d.Rotation(Math.PI / 2, Vector3d.ZAxis, Point3d.Origin);
        var moved = DirectionText.Apply(db, new[] { a, b }, false, Point3d.Origin.TransformBy(ucs), new Point3d(10, 0, 0).TransformBy(ucs), ucs);
        Check(moved.SequenceEqual(new[] { a, b }), "verplaatsen behoudt oorspronkelijke objecthandles");
        using (var tx = db.TransactionManager.StartTransaction())
        {
            var text = (MText)tx.GetObject(a, OpenMode.ForRead);
            Check(text.Location.DistanceTo(new Point3d(0, 0.4, 0).TransformBy(ucs)) < 1e-8 && text.Direction.IsEqualTo(Vector3d.XAxis.TransformBy(ucs)),
                "verplaatst/gedraaid UCS levert correcte wereldpositie en tekstoriëntatie");
        }
        var back = DirectionText.Apply(db, new[] { a }, true, new Point3d(0, 80, 0), new Point3d(10, 80, 0), Matrix3d.Identity);
        DirectionText.RemoveCopy(db, back);
        Check(back.All(id => id.IsErased) && !a.IsErased && !b.IsErased, "Terug verwijdert alleen de laatste kopie");
        var failed = false;
        try { DirectionText.Apply(db, new[] { a }, false, Point3d.Origin, Point3d.Origin, Matrix3d.Identity); }
        catch (InvalidOperationException) { failed = true; }
        using (var tx = db.TransactionManager.StartTransaction())
            Check(failed && ((MText)tx.GetObject(a, OpenMode.ForRead)).Location.DistanceTo(new Point3d(0, 0.4, 0).TransformBy(ucs)) < 1e-8,
                "gelijke eindpunten veroorzaken geen wijziging");
        using (var tx = db.TransactionManager.StartTransaction())
        {
            var text = (MText)tx.GetObject(a, OpenMode.ForRead);
            ((LayerTableRecord)tx.GetObject(text.LayerId, OpenMode.ForWrite)).IsLocked = true; tx.Commit();
        }
        failed = false;
        try { DirectionText.Apply(db, new[] { a }, false, Point3d.Origin, new Point3d(10, 0, 0), Matrix3d.Identity); }
        catch (InvalidOperationException) { failed = true; }
        Check(failed, "vergrendelde tekstlaag wordt niet gewijzigd");
        var file = Path.Combine(output, "Richtingsteksten - controle.dwg");
        db.SaveAs(file, DwgVersion.AC1032); db.DxfOut(Path.Combine(output, "Richtingsteksten - controle.dxf"), 16, DwgVersion.AC1032);
        using (var reopened = new Database(false, true))
        {
            reopened.ReadDwgFile(file, FileOpenMode.OpenForReadAndAllShare, true, null); reopened.CloseInput(true);
            using var tx = reopened.TransactionManager.StartTransaction();
            Check(templateIds.Concat(stackIds).All(id => tx.GetObject(reopened.GetObjectId(false, id.Handle, 0), OpenMode.ForRead) is MText),
                "losse bron- en kopieteksten teruggelezen uit opgeslagen CAD");
        }
        using (var panel = new DirectionPanel { Size = new Size(380, 760) })
        {
            panel.CreateControl(); panel.PerformLayout(); using var image = new Bitmap(panel.Width, panel.Height);
            panel.DrawToBitmap(image, new Rectangle(Point.Empty, panel.Size)); image.Save(Path.Combine(output, "Richtingen-met-tekst.png"));
        }
        File.WriteAllText(Path.Combine(output, "richtingsteksten.json"), JsonSerializer.Serialize(new { Status = "PASS", Checks = checks },
            new JsonSerializerOptions { WriteIndented = true }));
    }
}
