using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Techbase.Blocks;

internal static class DirectionLineDiagnostics
{
    internal static void Run()
    {
        var output = Environment.GetEnvironmentVariable("TECHBASE_TEST_OUTPUT") ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Techbase", "Blocks", "Validation", "Richtingen");
        Directory.CreateDirectory(output);
        var checks = new List<string>();
        void Check(bool ok, string name) { if (!ok) throw new InvalidOperationException("Richtingen: " + name); checks.Add(name); }
        using var db = new Database(true, true);
        using var working = new WorkingDatabaseScope(db);
        var originalLayer = db.Clayer;
        var saved = new List<Handle>();
        var catalog = new Catalog(Plugin.DefaultPalette);
        using var source = new Database(false, true);
        source.ReadDwgFile(catalog.ResolveSource(catalog.Tools[0]), FileOpenMode.OpenForReadAndAllShare, true, null); source.CloseInput(true);
        using var sourceTx = source.TransactionManager.StartTransaction();
        var sourceLayers = (LayerTable)sourceTx.GetObject(source.LayerTableId, OpenMode.ForRead);
        void Styled(Entity entity, int number)
        {
            Check(entity.Layer == DirectionLines.LayerName(number) && entity.Linetype == "DASHED" &&
                Math.Abs(entity.LinetypeScale - 0.0035) < 1e-12 && entity.ColorIndex == 256 && entity.LineWeight == LineWeight.ByLayer,
                "laag en entity-opmaak K" + number.ToString("00"));
            if (entity is Polyline p) Check(Math.Abs(p.ConstantWidth - 0.1) < 1e-12 &&
                Enumerable.Range(0, p.NumberOfVertices).All(i => Math.Abs(p.GetStartWidthAt(i) - 0.1) < 1e-12 && Math.Abs(p.GetEndWidthAt(i) - 0.1) < 1e-12),
                "constante breedte op alle segmenten K" + number.ToString("00"));
        }
        for (var number = 1; number <= 12; number++)
        {
            using var p = new Polyline(); p.SetDatabaseDefaults(db);
            p.AddVertexAt(0, new Point2d(number * 10, 0), 0, 0, 0); p.AddVertexAt(1, new Point2d(number * 10, 5), 0, 0, 0);
            var id = DirectionLines.Append(db, p, number); saved.Add(id.Handle);
            Styled(p, number);
            using var tx = db.TransactionManager.StartTransaction();
            var layer = (LayerTableRecord)tx.GetObject(p.LayerId, OpenMode.ForRead);
            var expected = (LayerTableRecord)sourceTx.GetObject(sourceLayers[DirectionLines.LayerName(number)], OpenMode.ForRead);
            Check(layer.Color.ColorValue.ToArgb() == expected.Color.ColorValue.ToArgb(), "nieuwe richtingslaag heeft Techbase-bronkleur K" + number.ToString("00"));
        }
        ObjectId lightId, heavyId, flatLine, spatialLine, lockedId, nestedId, circleId;
        var spatialStart = new Point3d(2, 3, 4); var spatialEnd = new Point3d(8, 9, 10);
        using (var tx = db.TransactionManager.StartTransaction())
        {
            var space = (BlockTableRecord)tx.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
            ObjectId Add(Entity e) { e.SetDatabaseDefaults(db); var id = space.AppendEntity(e); tx.AddNewlyCreatedDBObject(e, true); return id; }
            using var light = new Polyline();
            light.AddVertexAt(0, new Point2d(0, 20), 0.5, 2, 3); light.AddVertexAt(1, new Point2d(10, 20), 0, 4, 5);
            light.AddVertexAt(2, new Point2d(10, 30), 0, 0, 0); light.Closed = true; light.Elevation = 7;
            lightId = Add(light);
            using var heavy = new Polyline2d(); heavyId = Add(heavy);
            foreach (var point in new[] { new Point3d(0, 40, 0), new Point3d(10, 40, 0), new Point3d(10, 50, 0) })
            {
                using var v = new Vertex2d { Position = point, StartWidth = 2, EndWidth = 3, Bulge = 0.25 };
                heavy.AppendVertex(v); tx.AddNewlyCreatedDBObject(v, true);
            }
            flatLine = Add(new Line(new Point3d(0, 60, 0), new Point3d(10, 60, 0)));
            spatialLine = Add(new Line(spatialStart, spatialEnd));
            using var protectedLine = new Polyline();
            protectedLine.AddVertexAt(0, new Point2d(0, 70), 0, 0, 0); protectedLine.AddVertexAt(1, new Point2d(10, 70), 0, 0, 0);
            lockedId = Add(protectedLine);
            var lockedLayer = BlockInserter.DefaultLayer(db, tx, "TEST_LOCKED");
            ((Entity)tx.GetObject(lockedId, OpenMode.ForWrite)).LayerId = lockedLayer;
            ((LayerTableRecord)tx.GetObject(lockedLayer, OpenMode.ForWrite)).IsLocked = true;
            circleId = Add(new Circle(new Point3d(0, 80, 0), Vector3d.ZAxis, 2));
            var table = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForWrite);
            using var block = new BlockTableRecord { Name = "PROTECTED_BLOCK" };
            table.Add(block); tx.AddNewlyCreatedDBObject(block, true);
            using var nested = new Polyline(); nested.AddVertexAt(0, Point2d.Origin, 0, 0, 0); nested.AddVertexAt(1, new Point2d(1, 0), 0, 0, 0);
            nestedId = block.AppendEntity(nested); tx.AddNewlyCreatedDBObject(nested, true);
            tx.Commit();
        }
        var result = DirectionLines.Convert(db, new[] { lightId, heavyId, flatLine, spatialLine, lockedId, circleId, nestedId, lightId }, 10);
        Check(result.Changed == 4 && result.LinesConverted == 2 && result.Skipped == 3, "gemengde selectie: polylines/lijnen, deduplicatie en beschermde objecten");
        using (var tx = db.TransactionManager.StartTransaction())
        {
            var light = (Polyline)tx.GetObject(lightId, OpenMode.ForRead); Styled(light, 10);
            Check(light.Closed && light.NumberOfVertices == 3 && light.Elevation == 7 && light.GetBulgeAt(0) == 0.5 &&
                light.GetPoint2dAt(0).IsEqualTo(new Point2d(0, 20)) && light.GetPoint2dAt(1).IsEqualTo(new Point2d(10, 20)) &&
                light.GetPoint2dAt(2).IsEqualTo(new Point2d(10, 30)), "omzetten behoudt handle, punten, boog, sluiten en hoogte");
            var heavy = (Polyline2d)tx.GetObject(heavyId, OpenMode.ForRead); Styled(heavy, 10);
            Check(Math.Abs(heavy.ConstantWidth - 0.1) < 1e-12 && heavy.Cast<ObjectId>().Select(id => (Vertex2d)tx.GetObject(id, OpenMode.ForRead))
                .All(v => v.Bulge == 0.25 && Math.Abs(v.StartWidth - 0.1) < 1e-12 && Math.Abs(v.EndWidth - 0.1) < 1e-12), "oude 2D-polyline behoudt vertices en bogen met uniforme breedte");
            var converted = result.ResultIds.Skip(2).Select(id => (Polyline)tx.GetObject(id, OpenMode.ForRead)).ToArray();
            Check(flatLine.IsErased && spatialLine.IsErased && converted[0].GetPoint3dAt(0).IsEqualTo(new Point3d(0, 60, 0)) &&
                converted[0].GetPoint3dAt(1).IsEqualTo(new Point3d(10, 60, 0)), "losse LINE wordt polyline met dezelfde eindpunten");
            Check(converted[1].GetPoint3dAt(0).DistanceTo(spatialStart) < 1e-8 && converted[1].GetPoint3dAt(1).DistanceTo(spatialEnd) < 1e-8,
                "ruimtelijke LINE behoudt beide wereldpunten");
            foreach (var p in converted) Styled(p, 10);
            Check(((Entity)tx.GetObject(lockedId, OpenMode.ForRead)).Layer == "TEST_LOCKED" &&
                ((Entity)tx.GetObject(nestedId, OpenMode.ForRead)).Layer == "0" && ((Entity)tx.GetObject(circleId, OpenMode.ForRead)).Layer == "0",
                "vergrendelde objecten, blockinhoud en andere geometrie ongemoeid");
            var layer = (LayerTableRecord)tx.GetObject(DirectionLines.EnsureStyle(db, 11).Layer, OpenMode.ForWrite); layer.IsLocked = true; tx.Commit();
        }
        var rejected = false;
        try { DirectionLines.Convert(db, new[] { lightId }, 11); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "vergrendelde doellaag breekt omzetten af");
        using (var tx = db.TransactionManager.StartTransaction())
            Check(((Entity)tx.GetObject(lightId, OpenMode.ForRead)).Layer == DirectionLines.LayerName(10), "afgebroken omzetting behoudt bronlaag");
        Check(db.Clayer == originalLayer, "huidige tekenlaag blijft behouden");
        using (var tx = db.TransactionManager.StartTransaction())
        {
            var layer = (LayerTableRecord)tx.GetObject(DirectionLines.EnsureStyle(db, 7).Layer, OpenMode.ForWrite);
            layer.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 120); tx.Commit();
        }
        var existingStyle = DirectionLines.EnsureStyle(db, 7);
        using (var tx = db.TransactionManager.StartTransaction())
            Check(((LayerTableRecord)tx.GetObject(existingStyle.Layer, OpenMode.ForRead)).Color.ColorIndex == 120,
                "bestaande aangepaste richtingskleur blijft behouden");
        var file = Path.Combine(output, "Richtingen - controle.dwg");
        db.SaveAs(file, DwgVersion.AC1032); db.DxfOut(Path.Combine(output, "Richtingen - controle.dxf"), 16, DwgVersion.AC1032);
        using (var reopened = new Database(false, true))
        {
            reopened.ReadDwgFile(file, FileOpenMode.OpenForReadAndAllShare, true, null); reopened.CloseInput(true);
            using var tx = reopened.TransactionManager.StartTransaction();
            for (var n = 1; n <= 12; n++) Styled((Polyline)tx.GetObject(reopened.GetObjectId(false, saved[n - 1], 0), OpenMode.ForRead), n);
        }
        var priorPath = Environment.GetEnvironmentVariable("TECHBASE_SETTINGS_FILE"); var priorNumber = Plugin.Preferences.SelectedDirectionNumber;
        try
        {
            Environment.SetEnvironmentVariable("TECHBASE_SETTINGS_FILE", Path.Combine(output, "richting-ui-settings.json"));
            Plugin.Preferences.SelectedDirectionNumber = 1;
            using var panel = new DirectionPanel { Size = new Size(380, 650) }; panel.CreateControl(); panel.PerformLayout();
            IEnumerable<Control> Descend(Control c) => c.Controls.Cast<Control>().SelectMany(child => new[] { child }.Concat(Descend(child)));
            var buttons = Descend(panel).OfType<RadioButton>().ToArray();
            Check(buttons.Length == 12 && buttons.Count(b => b.Checked) == 1, "richtingenpaneel biedt precies twaalf keuzes");
            buttons.Single(b => b.Text == "Richting 10").Checked = true;
            Check(Plugin.Preferences.SelectedDirectionNumber == 10 && Settings.Read().SelectedDirectionNumber == 10 && buttons.Count(b => b.Checked) == 1,
                "gekozen richting wordt bewaard en maar eenmaal geselecteerd");
            using var image = new Bitmap(panel.Width, panel.Height); panel.DrawToBitmap(image, new Rectangle(Point.Empty, panel.Size));
            image.Save(Path.Combine(output, "Richtingen-tab.png"));
        }
        finally { Plugin.Preferences.SelectedDirectionNumber = priorNumber; Plugin.UpdateSettings(); Environment.SetEnvironmentVariable("TECHBASE_SETTINGS_FILE", priorPath); }
        File.WriteAllText(Path.Combine(output, "richtingen.json"), JsonSerializer.Serialize(new { Status = "PASS", Checks = checks },
            new JsonSerializerOptions { WriteIndented = true }));
    }
}
