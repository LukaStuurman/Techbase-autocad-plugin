using Enexis.KabelChecker.AutoCAD;
using Enexis.KabelChecker.Core;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Techbase.Blocks;

internal static class DirectionInfoDiagnostics
{
    internal static DirectionState Fixture()
    {
        var options = ExcelLoadCatalog.For(KaderVersion.K2026_3_2);
        var mapped = new[] { new ExcelMappedLoad(options[0].Key, options[0].CableDesignCurrentAmps, 4), new ExcelMappedLoad(options[1].Key, options[1].CableDesignCurrentAmps, 3) };
        return new DirectionState(3, LoadProfile.Evenredig, new[] { new CableSegment("4*150mm2 Al", 100), new CableSegment("4*120mm2 Al", 5.37) },
            mapped.Select(m => new CurrentLoadInput(m.Amps, m.Count)).ToArray(), mapped);
    }
    internal static void Run(List<string> checks, string folder)
    {
        void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException("Richting-info test mislukt: " + name); checks.Add(name); }
        var state = Fixture();
        var labels = DirectionInfoService.Labels(state, KaderVersion.K2026_3_2, DesignCurrentMode.Automatisch);
        Check(labels.Current == "57,8Amp." && labels.Length == "105,37Met.", "richting-info telt verbruik/opwek apart en gebruikt het hoogste totaal");
        Check(DirectionInfoService.Labels(state, KaderVersion.K2026_3_2, DesignCurrentMode.Verbruik).Current == "53,7Amp.", "richting-info volgt stroombasis Verbruik");
        Check(DirectionInfoService.Labels(state, KaderVersion.K2026_3_2, DesignCurrentMode.Opwek).Current == "57,8Amp.", "richting-info volgt stroombasis Opwek");
        using var db = new Database(true, true); db.DisableUndoRecording(false);
        using var working = new WorkingDatabaseScope(db);
        var missing = false;
        try { DirectionInfoService.ValidateLayer(db, 3); } catch (InvalidOperationException) { missing = true; }
        Check(missing, "richting-info maakt geen ontbrekende richtingslaag aan");
        ObjectId layer;
        using (var tx = db.TransactionManager.StartTransaction())
        {
            layer = BlockInserter.DefaultLayer(db, tx, "Aansluiting LS K03");
            var record = (LayerTableRecord)tx.GetObject(layer, OpenMode.ForWrite); record.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 3); tx.Commit();
        }
        var ucs = Matrix3d.Rotation(Math.PI / 2, Vector3d.ZAxis, Point3d.Origin);
        var ids = DirectionInfoService.Append(db, labels, 3, new Point3d(5, -100, 0), ucs);
        string[] handles;
        using (var tx = db.TransactionManager.StartTransaction())
        {
            var texts = ids.Select(id => (DBText)tx.GetObject(id, OpenMode.ForRead)).ToArray();
            Check(texts.Length == 2 && texts.All(t => t.LayerId == layer && t.ColorIndex == 256), "twee info-teksten op gekozen richtingslaag met ByLayer");
            Check(texts[0].AlignmentPoint.DistanceTo(new Point3d(100, 5, 0)) < 1e-7, "bovenste tekst op gekozen wereldpunt bij gedraaid UCS");
            Check(texts[1].AlignmentPoint.DistanceTo(new Point3d(100 + DirectionInfoService.Spacing, 5, 0)) < 1e-7, "afstand en tekstvolgorde volgen UCS");
            Check(texts.All(t => Math.Abs(t.Rotation - Math.PI / 2) < 1e-8 && t.HorizontalMode == TextHorizontalMode.TextCenter && t.VerticalMode == TextVerticalMode.TextVerticalMid), "teksten gecentreerd en gedraaid in UCS");
            Check(texts.All(t => Math.Abs(t.Height - 1) < 1e-9), "teksthoogte volgens repo");
            handles = texts.Select(t => t.Handle.ToString()).ToArray();
        }
        var file = System.IO.Path.Combine(folder, "Richting-info - controle.dwg");
        db.SaveAs(file, DwgVersion.AC1032); db.DxfOut(System.IO.Path.Combine(folder, "Richting-info - controle.dxf"), 16, DwgVersion.AC1032);
        using (var reopened = new Database(false, true))
        {
            reopened.ReadDwgFile(file, FileOpenMode.OpenForReadAndAllShare, true, null); reopened.CloseInput(true);
            using var tx = reopened.TransactionManager.StartTransaction();
            var texts = handles.Select(h => (DBText)tx.GetObject(reopened.GetObjectId(false, new Handle(Convert.ToInt64(h, 16)), 0), OpenMode.ForRead)).ToArray();
            Check(texts[0].TextString == labels.Current && texts[1].TextString == labels.Length && texts.All(t => t.Layer == "Aansluiting LS K03"), "opgeslagen CAD teruggelezen: inhoud en richtingslaag");
        }
        using (var tx = db.TransactionManager.StartTransaction()) { ((LayerTableRecord)tx.GetObject(layer, OpenMode.ForWrite)).IsLocked = true; tx.Commit(); }
        var locked = false;
        try { DirectionInfoService.ValidateLayer(db, 3); } catch (InvalidOperationException) { locked = true; }
        Check(locked, "vergrendelde richtingslaag beschermd");
    }
    internal static void QueueFixture()
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc != null) Plugin.PendingDirectionInfo = new DirectionInfoRequest(doc, Fixture(), KaderVersion.K2026_3_2, DesignCurrentMode.Automatisch);
    }
}
