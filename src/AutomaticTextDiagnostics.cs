using System.Drawing;
using System.IO;
using System.Text.Json;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Techbase.Blocks;

internal static class AutomaticTextDiagnostics
{
    internal static void Run()
    {
        var output = Environment.GetEnvironmentVariable("TECHBASE_TEST_OUTPUT") ?? Path.Combine(Path.GetTempPath(), "TechbaseAutoText");
        Directory.CreateDirectory(output);
        var checks = new List<string>();
        void Check(bool ok, string name) { if (!ok) throw new InvalidOperationException(name); checks.Add(name); }
        using var db = new Database(true, true); using var working = new WorkingDatabaseScope(db);
        ObjectId hidden, locked, existing, excluded;
        using (var tx = db.TransactionManager.StartTransaction())
        {
            var space = (BlockTableRecord)tx.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
            ObjectId Add(Curve curve, string name)
            {
                curve.LayerId = BlockInserter.DefaultLayer(db, tx, name);
                space.AppendEntity(curve); tx.AddNewlyCreatedDBObject(curve, true); return curve.ObjectId;
            }
            for (var n = 1; n <= 12; n++)
            {
                var polyline = new Polyline(); polyline.AddVertexAt(0, new Point2d(0, n), 0, 0, 0); polyline.AddVertexAt(1, new Point2d(20, n), 0, 0, 0);
                Add(polyline, DirectionLines.LayerName(n));
            }
            Add(new Line(new Point3d(0, 1.5, 0), new Point3d(20, 1.5, 0)), DirectionLines.LayerName(1));
            excluded = Add(new Line(new Point3d(0, 13, 0), new Point3d(20, 13, 0)), "01 - Bestaande kabel");
            existing = ((Curve)tx.GetObject(excluded, OpenMode.ForRead)).LayerId;
            Add(new Line(new Point3d(0, 14, 0), new Point3d(20, 14, 0)), "01 - Bestaande kabel");
            Add(new Line(new Point3d(0, 15, 0), new Point3d(20, 15, 0)), "OVERIG");
            Add(new Line(new Point3d(0, 16, 0), new Point3d(20, 16, 0)), "xref|01 - Bestaande kabel");
            Add(new Circle(new Point3d(50, 0, 0), Vector3d.ZAxis, 1), DirectionLines.LayerName(2));
            hidden = BlockInserter.DefaultLayer(db, tx, DirectionLines.LayerName(3));
            locked = BlockInserter.DefaultLayer(db, tx, DirectionLines.LayerName(10));
            tx.Commit();
        }
        var first = new Point3d(5, 0, 0); var last = new Point3d(5, 17, 0);
        var hits = AutomaticDirectionText.Detect(db, first, last);
        Check(hits.Length == 14 && hits.Take(12).Select(h => h.Number).SequenceEqual(Enumerable.Range(1, 12)), "12 richtingen, kabelvolgorde en dubbele richting eenmaal");
        Check(hits.Skip(12).All(h => h.Number == 0) && hits[12].Curve != hits[13].Curve, "twee bestaande kabels op dezelfde laag krijgen eigen teksten");
        Check(AutomaticDirectionText.Detect(db, first, last, excluded).Length == 13, "detectielijn uitgesloten");
        Check(AutomaticDirectionText.Detect(db, last, first).Select(h => h.Number).SequenceEqual(hits.Reverse().Select(h => h.Number)), "omgekeerde lijn geeft omgekeerde kabelvolgorde");
        Check(AutomaticDirectionText.Detect(db, new Point3d(30, 0, 0), new Point3d(30, 17, 0)).Length == 0, "geen oneindige verlenging buiten kabeluiteinden");
        var curved = AutomaticDirectionText.Detect(db, new Point3d(50, -2, 0), new Point3d(50, 2, 0));
        Check(curved.Length == 1 && curved[0].Number == 2 && Math.Abs(curved[0].Tangent.Y) < 1e-8, "gebogen kabel: werkelijke kruising/tangent, herhaalde kruising eenmaal");
        var failed = false;
        try { AutomaticDirectionText.Detect(db, first, first); } catch (InvalidOperationException) { failed = true; }
        Check(failed, "nul-lengte detectielijn afgewezen");
        using (var tx = db.TransactionManager.StartTransaction()) { ((LayerTableRecord)tx.GetObject(hidden, OpenMode.ForWrite)).IsOff = true; tx.Commit(); }
        Check(AutomaticDirectionText.Detect(db, first, last).Length == 13, "uitgeschakelde kabel niet herkend");
        using (var tx = db.TransactionManager.StartTransaction())
        {
            ((LayerTableRecord)tx.GetObject(hidden, OpenMode.ForWrite)).IsOff = false;
            ((LayerTableRecord)tx.GetObject(locked, OpenMode.ForWrite)).IsLocked = true; tx.Commit();
        }
        using (var tx = db.TransactionManager.StartTransaction())
        {
            failed = false;
            try { AutomaticDirectionText.Create(db, hits, false, .45, tx); } catch (InvalidOperationException) { failed = true; }
            Check(failed, "vergrendelde richting breekt plaatsing af zonder gedeeltelijke teksten");
        }
        using (var tx = db.TransactionManager.StartTransaction()) { ((LayerTableRecord)tx.GetObject(locked, OpenMode.ForWrite)).IsLocked = false; tx.Commit(); }
        var textIds = new List<ObjectId>();
        foreach (var ov in new[] { false, true }) foreach (var height in new[] { .45, .75 })
        {
            MText[] texts;
            using (var tx = db.TransactionManager.StartTransaction()) { texts = AutomaticDirectionText.Create(db, hits, ov, height, tx); tx.Commit(); }
            try
            {
                var axis = AutomaticDirectionText.Axis(hits, first, last, Matrix3d.Identity);
                AutomaticDirectionText.Layout(texts, last, axis, Matrix3d.Identity);
                Check(texts.Take(12).All(t => t.Contents == AutomaticDirectionText.DefaultText(ov)) &&
                    texts.Skip(12).All(t => t.Contents == AutomaticDirectionText.DefaultText(ov, true)), $"exacte {(ov ? "OV" : "LS")}-tekst en bestaand '(was ????-00)' op {height}");
                Check(texts.All(t => Math.Abs(t.Width - 4.80) < 1e-8 && t.TextHeight == height && t.ColorIndex == 256), "hoogte, defined width en ByLayer");
                Check(texts.Zip(texts.Skip(1)).All(pair => pair.Second.Location.Y - pair.First.Location.Y >= pair.First.ActualHeight + .2 - 1e-8), "teksten stapelen met werkelijke hoogte zonder overlap");
                textIds.AddRange(AutomaticDirectionText.Append(db, texts));
            }
            finally { foreach (var text in texts) text.Dispose(); }
        }
        var ucs = Matrix3d.Displacement(new Vector3d(100, 200, 5)) * Matrix3d.Rotation(Math.PI / 2, Vector3d.ZAxis, Point3d.Origin);
        var rotated = hits.Select(h => h with { Point = h.Point.TransformBy(ucs), Tangent = h.Tangent.TransformBy(ucs) }).ToArray();
        Check(AutomaticDirectionText.Axis(rotated, first.TransformBy(ucs), last.TransformBy(ucs), ucs).IsEqualTo(Vector3d.YAxis), "kabel-as in gedraaid en verschoven UCS");
        using (var tx = db.TransactionManager.StartTransaction())
        {
            var texts = AutomaticDirectionText.Create(db, hits.Take(1).ToArray(), false, .45, tx);
            try
            {
                AutomaticDirectionText.Layout(texts, Point3d.Origin.TransformBy(ucs), Vector3d.YAxis, ucs);
                Check(texts[0].Location.DistanceTo(new Point3d(0, .4, 0).TransformBy(ucs)) < 1e-8, "tekstanker in gedraaid/verschoven UCS");
                AutomaticDirectionText.Layout(texts, Point3d.Origin, -Vector3d.XAxis, Matrix3d.Identity);
                Check(texts[0].Direction.IsEqualTo(Vector3d.XAxis) && texts[0].Attachment == AttachmentPoint.TopLeft && texts[0].Location.DistanceTo(new Point3d(0, -.4, 0)) < 1e-8, "omgekeerde kabel-as blijft leesbaar met vast anker");
            }
            finally { foreach (var text in texts) text.Dispose(); }
        }
        ObjectId lockedExisting, unrelated, xref;
        using (var tx = db.TransactionManager.StartTransaction())
        {
            lockedExisting = BlockInserter.DefaultLayer(db, tx, "Bestaande kabel OV"); unrelated = BlockInserter.DefaultLayer(db, tx, "Te rooien kabel"); xref = BlockInserter.DefaultLayer(db, tx, "xref|01 - Bestaande kabel");
            foreach (var id in new[] { lockedExisting, unrelated, xref }) ((LayerTableRecord)tx.GetObject(id, OpenMode.ForWrite)).Color = ExistingCableColors.Choice(false);
            ((LayerTableRecord)tx.GetObject(lockedExisting, OpenMode.ForWrite)).IsLocked = true;
            var layer = (LayerTableRecord)tx.GetObject(existing, OpenMode.ForWrite); layer.IsFrozen = true; layer.LineWeight = LineWeight.LineWeight050; tx.Commit();
        }
        foreach (var black in new[] { true, false })
        {
            var result = ExistingCableColors.Apply(db, black);
            using var tx = db.TransactionManager.StartTransaction(); var layer = (LayerTableRecord)tx.GetObject(existing, OpenMode.ForRead);
            Check(result.Changed == 1 && result.Locked == 1 && layer.Color.ColorIndex == (black ? 7 : 125), "kleur wisselt tussen 7/125; vergrendeld gemeld");
            Check(layer.IsFrozen && layer.LineWeight == LineWeight.LineWeight050, "kleurwissel behoudt laagstatus en lijndikte");
            Check(new[] { lockedExisting, unrelated, xref }.All(id => ((LayerTableRecord)tx.GetObject(id, OpenMode.ForRead)).Color.ColorIndex == 125), "xref, rooien en vergrendelde laag behouden kleur");
        }
        var settings = new Settings { ExistingCableBlack = true, DirectionTextHeight = .75, KabelgeulMantelSpec = "behouden" };
        var restored = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(settings))!;
        Check(restored.ExistingCableBlack && restored.DirectionTextHeight == .75 && restored.KabelgeulMantelSpec == "behouden" && !new Settings().ExistingCableBlack, "kleuroptie opgeslagen; andere instellingen behouden; standaard groen");
        var previousBlack = Plugin.Preferences.ExistingCableBlack;
        try
        {
            var catalog = new Catalog(Plugin.DefaultPalette);
            foreach (var black in new[] { true, false })
            {
                Plugin.Preferences.ExistingCableBlack = black;
                using var target = new Database(true, true); using var scope = new WorkingDatabaseScope(target);
                BlockInserter.Import(target, catalog, DirectionText.Template(catalog, false));
                using (var tx = target.TransactionManager.StartTransaction())
                {
                    var layers = (LayerTable)tx.GetObject(target.LayerTableId, OpenMode.ForRead);
                    var layer = (LayerTableRecord)tx.GetObject(layers["01 - Bestaande kabel"], OpenMode.ForWrite);
                    Check(layer.Color.ColorIndex == (black ? 7 : 125), "nieuwe bronlaag gebruikt bewaarde kleurkeuze");
                    layer.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 3); tx.Commit();
                }
                BlockInserter.Import(target, catalog, DirectionText.Template(catalog, true));
                using (var tx = target.TransactionManager.StartTransaction())
                {
                    var layers = (LayerTable)tx.GetObject(target.LayerTableId, OpenMode.ForRead);
                    Check(((LayerTableRecord)tx.GetObject(layers["01 - Bestaande kabel"], OpenMode.ForRead)).Color.ColorIndex == 3, "volgende blockimport behoudt bestaande laagkleur");
                }
            }
        }
        finally { Plugin.Preferences.ExistingCableBlack = previousBlack; }
        db.SaveAs(Path.Combine(output, "Automatische teksten - controle.dwg"), DwgVersion.AC1032);
        db.DxfOut(Path.Combine(output, "Automatische teksten - controle.dxf"), 16, DwgVersion.AC1032);
        using (var readback = new Database(false, true))
        {
            readback.ReadDwgFile(Path.Combine(output, "Automatische teksten - controle.dwg"), FileOpenMode.OpenForReadAndAllShare, true, null); readback.CloseInput(true);
            using var tx = readback.TransactionManager.StartTransaction();
            Check(textIds.All(id => tx.GetObject(readback.GetObjectId(false, id.Handle, 0), OpenMode.ForRead) is MText), "56 losse teksten teruggelezen uit opgeslagen DWG");
        }
        foreach (var width in new[] { 380, 1040 })
        {
            foreach (var pair in new (UserControl Panel, string Name)[] { (new DirectionPanel(), "Richtingen"), (new SettingsPanel(), "Instellingen") })
            {
                using var panel = pair.Panel; panel.Size = new Size(width, 1100); panel.CreateControl(); panel.PerformLayout();
                using var image = new Bitmap(panel.Width, panel.Height); panel.DrawToBitmap(image, new Rectangle(Point.Empty, panel.Size)); image.Save(Path.Combine(output, $"{pair.Name}-{width}.png"));
            }
        }
        File.WriteAllText(Path.Combine(output, "service.json"), JsonSerializer.Serialize(new { Status = "PASS", Checks = checks }, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public sealed partial class Plugin
{
    [Autodesk.AutoCAD.Runtime.CommandMethod("TBAUTOTEKSTVALIDATE", Autodesk.AutoCAD.Runtime.CommandFlags.Modal)]
    public void ValidateAutomaticText() { AutomaticTextDiagnostics.Run(); }
}
