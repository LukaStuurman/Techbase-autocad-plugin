using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Techbase.Blocks;

internal static class LightmastDiagnostics
{
    internal static string Output => Environment.GetEnvironmentVariable("TECHBASE_TEST_OUTPUT") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Techbase", "Blocks", "Validation");
    internal static void Run()
    {
        var folder = Path.Combine(Output, "Lichtmasten"); Directory.CreateDirectory(folder);
        var checks = new List<string>();
        void Check(bool condition, string label) { if (!condition) throw new InvalidOperationException("Lichtmasttest mislukt: " + label); checks.Add(label); }
        bool TopObjects(Database database, string application, int count)
        {
            using var tx = database.TransactionManager.StartTransaction();
            var space = (BlockTableRecord)tx.GetObject(database.CurrentSpaceId, OpenMode.ForRead);
            var order = (DrawOrderTable)tx.GetObject(space.DrawOrderTableId, OpenMode.ForRead);
            return order.GetFullDrawOrder(0).Cast<ObjectId>().Where(id => !id.IsErased).TakeLast(count).All(id =>
            { using var data = ((Entity)tx.GetObject(id, OpenMode.ForRead)).GetXDataForApplication(application); return data != null; });
        }
        var automatic = new[] { "X|E_STREET_LIGHT_CONNECTION", "X|NESTED|E_LV_MULTISTORY_CONNECTION", "X|E_LV_DISTRIBUTION_BOX", "X|E_STREET_LIGHT_CONNECTION_EXTRA", "OTHER" };
        Check(LightmastService.AutomaticLayers(automatic, true, false).SequenceEqual(automatic.Take(1)), "eenvoudig: alleen exacte lichtmastlaag, inclusief xref-prefix");
        Check(LightmastService.AutomaticLayers(automatic, false, true).SequenceEqual(automatic.Skip(1).Take(2)), "eenvoudig: flatkasten en verdeelkasten, inclusief geneste xref-prefix");
        Check(LightmastService.AutomaticLayers(automatic, true, true).Length == 3 && LightmastService.AutomaticLayers(automatic, false, false).Length == 0, "toevoegingen combineren of uitschakelen");
        var box = new AreaBoundary(new[] { new Point2d(0, 0), new Point2d(10, 0), new Point2d(10, 10), new Point2d(0, 10) });
        Check(box.Contains(new Point3d(5, 5, 100)), "gebied in bovenaanzicht inclusief hoogte");
        Check(box.Contains(new Point3d(0, 5, 0)) && box.Contains(Point3d.Origin), "grens en hoekpunt inbegrepen");
        Check(!box.Contains(new Point3d(11, 5, 0)), "buiten rechte polygoon");
        var concave = new AreaBoundary(new[] { new Point2d(0, 0), new Point2d(10, 0), new Point2d(10, 4), new Point2d(4, 4), new Point2d(4, 10), new Point2d(0, 10) });
        Check(concave.Contains(new Point3d(2, 8, 0)) && !concave.Contains(new Point3d(8, 8, 0)), "concave polygoon");
        using (var circle = new Polyline(2))
        {
            circle.AddVertexAt(0, new Point2d(-10, 0), 1, 0, 0); circle.AddVertexAt(1, new Point2d(10, 0), 1, 0, 0); circle.Closed = true;
            var round = AreaBoundary.FromPolyline(circle);
            Check(round.Contains(Point3d.Origin) && round.Contains(new Point3d(0, 9.999, 0)), "bulge-bogen worden niet als koorden behandeld");
            Check(round.Contains(new Point3d(0, 10, 0)) && !round.Contains(new Point3d(0, 10.001, 0)), "exacte booggrens");
            var reverse = new AreaBoundary(new[] { new Point2d(-10, 0), new Point2d(10, 0) }, new[] { -1d, -1d });
            Check(reverse.Contains(new Point3d(0, 9, 0)) && !reverse.Contains(new Point3d(11, 0, 0)), "negatieve bulges");
            circle.Closed = false; var rejected = false;
            try { AreaBoundary.FromPolyline(circle); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "open polyline geweigerd");
        }
        var invalid = false;
        try { new AreaBoundary(new[] { new Point2d(0, 0), new Point2d(10, 10), new Point2d(0, 10), new Point2d(12, 0) }); } catch (InvalidOperationException) { invalid = true; }
        Check(invalid, "zelfkruisende getekende polygoon geweigerd");
        var sourceFile = Path.Combine(folder, "test-xref.dwg");
        using (var source = new Database(true, true))
        {
            using var working = new WorkingDatabaseScope(source);
            source.Insbase = new Point3d(5, 7, 0);
            using var tx = source.TransactionManager.StartTransaction();
            var lightLayer = BlockInserter.DefaultLayer(source, tx, "OV_MAST");
            var otherLayer = BlockInserter.DefaultLayer(source, tx, "OTHER");
            BlockInserter.DefaultLayer(source, tx, "EMPTY_LAYER");
            var flatLayer = BlockInserter.DefaultLayer(source, tx, LightmastService.FlatLayer);
            var cabinetLayer = BlockInserter.DefaultLayer(source, tx, LightmastService.DistributionLayer);
            ((LayerTableRecord)tx.GetObject(flatLayer, OpenMode.ForWrite)).Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 1);
            ((LayerTableRecord)tx.GetObject(cabinetLayer, OpenMode.ForWrite)).Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 2);
            var blocks = (BlockTable)tx.GetObject(source.BlockTableId, OpenMode.ForWrite);
            ObjectId Symbol(string name)
            {
                using var record = new BlockTableRecord { Name = name }; var id = blocks.Add(record); tx.AddNewlyCreatedDBObject(record, true);
                using var circle = new Circle(Point3d.Origin, Vector3d.ZAxis, 0.25); circle.SetDatabaseDefaults(source);
                record.AppendEntity(circle); tx.AddNewlyCreatedDBObject(circle, true); return id;
            }
            var mast = Symbol("MAST"); var other = Symbol("OTHER_SYMBOL");
            using var group = new BlockTableRecord { Name = "GROUP" }; var groupId = blocks.Add(group); tx.AddNewlyCreatedDBObject(group, true);
            using var child = new BlockReference(new Point3d(1, 2, 5), mast); child.SetDatabaseDefaults(source);
            group.AppendEntity(child); tx.AddNewlyCreatedDBObject(child, true);
            var model = (BlockTableRecord)tx.GetObject(source.CurrentSpaceId, OpenMode.ForWrite);
            void Add(ObjectId definition, Point3d location, ObjectId layer, bool nested = false)
            {
                using var reference = new BlockReference(location, definition); reference.SetDatabaseDefaults(source); reference.LayerId = layer;
                if (nested) { reference.ScaleFactors = new Scale3d(1.5, 2, 1); reference.Rotation = Math.PI / 4; }
                model.AppendEntity(reference); tx.AddNewlyCreatedDBObject(reference, true);
            }
            Add(mast, new Point3d(10, 20, 5), lightLayer);
            Add(mast, new Point3d(-10, 20, 0), lightLayer);
            Add(other, new Point3d(10, 20, 0), otherLayer);
            Add(groupId, new Point3d(20, 30, 2), lightLayer, true);
            Add(other, new Point3d(11, 22, 0), flatLayer);
            using var cabinetLine = new Line(new Point3d(8, 18, 0), new Point3d(12, 18, 0));
            cabinetLine.SetDatabaseDefaults(source); cabinetLine.LayerId = cabinetLayer;
            model.AppendEntity(cabinetLine); tx.AddNewlyCreatedDBObject(cabinetLine, true);
            using var cabinetCircle = new Circle(new Point3d(15, 18, 0), Vector3d.ZAxis, 1);
            cabinetCircle.SetDatabaseDefaults(source); cabinetCircle.LayerId = cabinetLayer;
            model.AppendEntity(cabinetCircle); tx.AddNewlyCreatedDBObject(cabinetCircle, true);
            tx.Commit(); source.SaveAs(sourceFile, DwgVersion.AC1032);
        }
        var originalHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourceFile)));
        using var db = new Database(true, true);
        db.DisableUndoRecording(false);
        using var active = new WorkingDatabaseScope(db);
        var xrefDefinition = db.AttachXref(sourceFile, "MAST_XREF");
        ObjectId xrefId, secondId;
        using (var tx = db.TransactionManager.StartTransaction())
        {
            var model = (BlockTableRecord)tx.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
            using var reference = new BlockReference(new Point3d(100, 200, 30), xrefDefinition);
            reference.SetDatabaseDefaults(db); reference.ScaleFactors = new Scale3d(2, 3, 4); reference.Rotation = Math.PI / 2;
            xrefId = model.AppendEntity(reference); tx.AddNewlyCreatedDBObject(reference, true);
            using var second = new BlockReference(new Point3d(-100, 0, 0), xrefDefinition); second.SetDatabaseDefaults(db);
            secondId = model.AppendEntity(second); tx.AddNewlyCreatedDBObject(second, true); tx.Commit();
        }
        Check(LightmastService.Xrefs(db).Count == 2, "meerdere invoegingen van dezelfde xref apart kiezen");
        var snapshot = LightmastService.Scan(db, xrefId);
        Check(LightmastService.XrefLayers(db, xrefId).Any(l => l.EndsWith("|EMPTY_LAYER")), "geavanceerd bevat ook lagen zonder blocks");
        Check(snapshot.Warnings.Count == 0, "geladen xref volledig gelezen");
        var light = snapshot.Candidates.First(c => c.Layer.EndsWith("|OV_MAST")).Layer;
        var name = snapshot.Candidates.First(c => c.BlockName.EndsWith("|MAST")).BlockName;
        var selected = LightmastService.Select(snapshot, new[] { light }, new[] { name }, null);
        Check(selected.Length == 3, "lagen en optionele blocktypen samen filteren; alle gebied");
        Check(LightmastService.Select(snapshot, new[] { light }, Array.Empty<string>(), null).Length == 3, "samengesteld block en interne onderdelen niet dubbel markeren");
        // Independent arithmetic: base-point, unequal scales, 90-degree rotation, elevation.
        Point3d Expected(Point3d p) => new(100 - 3 * (p.Y - 7), 200 + 2 * (p.X - 5), 30 + 4 * p.Z);
        var expected = Expected(new Point3d(10, 20, 5));
        Check(selected.Any(c => c.Position.DistanceTo(expected) < 1e-7), "xref basispunt + niet-uniforme schaal + rotatie + Z");
        var nestedLocal = new Point3d(20 + (1.5 - 4) / Math.Sqrt(2), 30 + (1.5 + 4) / Math.Sqrt(2), 7);
        Check(selected.Any(c => c.Position.DistanceTo(Expected(nestedLocal)) < 1e-7), "genest block en laag-0-overerving");
        var secondSnapshot = LightmastService.Scan(db, secondId);
        Check(!snapshot.Candidates.Select(c => c.Key).Intersect(secondSnapshot.Candidates.Select(c => c.Key)).Any(), "bronidentiteit onderscheidt xref-invoegingen");
        var region = new AreaBoundary(new[] { new Point2d(expected.X - 1, expected.Y - 1), new Point2d(expected.X + 1, expected.Y - 1), new Point2d(expected.X + 1, expected.Y + 1), new Point2d(expected.X - 1, expected.Y + 1) });
        Check(LightmastService.Select(snapshot, new[] { light }, new[] { name }, region).Length == 1, "gebied filtert op wereldpositie");
        var currentLayer = db.Clayer;
        var placed = LightmastService.Place(db, selected);
        Check(placed.Placed == 3 && db.Clayer == currentLayer, "Lisp-symbool plaatsen zonder huidige laag te veranderen");
        Check(TopObjects(db, "TECHBASE_LIGHTMAST", 3), "lichtmasten expliciet bovenaan de tekenvolgorde");
        var repeated = LightmastService.Place(db, selected);
        Check(repeated.Placed == 0 && repeated.AlreadyPresent == 3, "herhaling op dezelfde bronpunten zonder dubbelen");
        var savedHandles = new List<string>();
        using (var tx = db.TransactionManager.StartTransaction())
        {
            var blocks = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
            var symbol = (BlockTableRecord)tx.GetObject(blocks[LightmastService.SymbolName], OpenMode.ForRead);
            var lines = symbol.Cast<ObjectId>().Select(id => (Polyline)tx.GetObject(id, OpenMode.ForRead)).ToArray();
            Check(lines.Length == 3 && lines.All(p => p.Layer == "0" && Math.Abs(p.ConstantWidth - 0.2) < 1e-12), "symbool drie polylijnen, laag 0, breedte 0,2");
            Check(lines.Count(p => p.Closed) == 1 && Math.Abs(lines[0].GetPoint2dAt(0).GetDistanceTo(lines[0].GetPoint2dAt(1)) - 1.1977) < 1e-12, "Lisp-diameter 1,1977 en twee halve cirkels");
            var model = (BlockTableRecord)tx.GetObject(db.CurrentSpaceId, OpenMode.ForRead);
            foreach (var reference in model.Cast<ObjectId>().Select(id => tx.GetObject(id, OpenMode.ForRead)).OfType<BlockReference>().Where(r => r.BlockTableRecord == symbol.ObjectId))
            {
                Check(reference.Layer == "Bestaand" && reference.ColorIndex == 256 && reference.ScaleFactors == new Scale3d(1), "invoegstijl Bestaand ByLayer 1:1");
                savedHandles.Add(reference.Handle.ToString());
            }
        }
        var drawing = Path.Combine(folder, "Lichtmasten - controle.dwg");
        db.SaveAs(drawing, DwgVersion.AC1032); db.DxfOut(Path.Combine(folder, "Lichtmasten - controle.dxf"), 16, DwgVersion.AC1032);
        using (var reopened = new Database(false, true))
        {
            reopened.ReadDwgFile(drawing, FileOpenMode.OpenForReadAndAllShare, true, null); reopened.CloseInput(true);
            using var tx = reopened.TransactionManager.StartTransaction();
            foreach (var handle in savedHandles)
            {
                var reference = (BlockReference)tx.GetObject(reopened.GetObjectId(false, new Handle(Convert.ToInt64(handle, 16)), 0), OpenMode.ForRead);
                Check(reference.Layer == "Bestaand" && selected.Any(c => c.Position.DistanceTo(reference.Position) < 1e-7), "opgeslagen symboolpositie " + handle);
            }
        }
        Check(originalHash == Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourceFile))), "xref-bronbestand ongewijzigd");
        var cabinetLayers = LightmastService.AutomaticLayers(LightmastService.XrefLayers(db, xrefId), false, true);
        var request = new LightmastRequest(null!, xrefId, cabinetLayers, Array.Empty<string>(), AreaMode.All, false, true);
        var copiedCount = CabinetService.Place(db, request, null);
        Check(copiedCount == 3, "kastlagen nemen blocks, lijnen en cirkels over met niet-uniforme xref-schaal");
        Check(TopObjects(db, "TECHBASE_CABINETS", 3), "alle kastobjecten bovenaan, inclusief losse en geschaalde geometrie");
        Check(CabinetService.Place(db, request, null) == 0, "kasten herhalen zonder dubbelen");
        using (var tx = db.TransactionManager.StartTransaction())
        {
            var table = (LayerTable)tx.GetObject(db.LayerTableId, OpenMode.ForRead);
            Check(((LayerTableRecord)tx.GetObject(table[LightmastService.FlatLayer], OpenMode.ForRead)).Color.ColorIndex == 3, "alleen overgenomen flatkastlaag groen");
            Check(((LayerTableRecord)tx.GetObject(table[LightmastService.DistributionLayer], OpenMode.ForRead)).Color.ColorIndex == 2, "verdeelkastlaag behoudt bronkleur");
            var model = (BlockTableRecord)tx.GetObject(db.CurrentSpaceId, OpenMode.ForRead);
            var copied = model.Cast<ObjectId>().Select(id => tx.GetObject(id, OpenMode.ForRead)).OfType<Entity>().ToArray();
            Check(copied.OfType<Line>().Any(l => l.Layer == LightmastService.DistributionLayer && l.StartPoint.DistanceTo(Expected(new Point3d(8, 18, 0))) < 1e-7), "losse kastgeometrie volgt xref-transformatie");
            Check(copied.OfType<BlockReference>().Any(b => b.Layer == LightmastService.FlatLayer && b.Position.DistanceTo(Expected(new Point3d(11, 22, 0))) < 1e-7 && b.Name != LightmastService.SymbolName), "flatkast behoudt eigen symbool en wereldpositie");
        }
        Check(originalHash == Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourceFile))), "kasten overnemen wijzigt bronbestand niet");
        var cabinetRegion = new AreaBoundary(new[] { new Point2d(-95,14), new Point2d(-93,14), new Point2d(-93,16), new Point2d(-95,16) });
        Check(CabinetService.Place(db, request with { XrefId = secondId }, cabinetRegion) == 1, "kasten gebiedselectie en tweede xref-invoeging");
        db.SaveAs(Path.Combine(folder, "Beheerkaart - kasten.dwg"), DwgVersion.AC1032);
        using (var reopened = new Database(false, true))
        {
            reopened.ReadDwgFile(Path.Combine(folder, "Beheerkaart - kasten.dwg"), FileOpenMode.OpenForReadAndAllShare, true, null); reopened.CloseInput(true);
            Check(TopObjects(reopened, "TECHBASE_CABINETS", 4), "tekenvolgorde van toevoegingen blijft bewaard in DWG");
        }
        File.WriteAllText(Path.Combine(folder, "validatie-lichtmasten.json"), JsonSerializer.Serialize(new { Status = "PASS", Host = Convert.ToString(AcApp.GetSystemVariable("ACADVER")), Checks = checks, Count = selected.Length, XrefSha256 = originalHash }, new JsonSerializerOptions { WriteIndented = true }));
        AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nLIGHTMAST VALIDATION PASS: " + checks.Count + " checks.");
    }
}
