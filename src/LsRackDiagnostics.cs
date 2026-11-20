using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Techbase.Blocks;

internal static class LsRackDiagnostics
{
    internal static void Run()
    {
        var output = Path.Combine(LightmastDiagnostics.Output, "LsRekken"); Directory.CreateDirectory(output);
        var catalog = new Catalog(Plugin.DefaultPalette);
        var tools = catalog.Tools.Where(t => LsRackFuses.IsRack(t.BlockName) && !LsRackFuses.IsOv(t.BlockName)).ToArray();
        var checks = new List<string>();
        void Check(bool ok, string text) { if (!ok) throw new InvalidOperationException("LS-rek: " + text); checks.Add(text); }
        Check(tools.Length == 12 && tools.Count(t => t.Group == "LS REKKEN" && t.BlockName.StartsWith("RT ")) == 5, "alle vijf RT-blocks uit LS REKKEN en zeven schematische LS-rekken gevonden");
        var hashes = tools.Select(t => catalog.ResolveSource(t)).Distinct().ToDictionary(p => p, p => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))));
        using var db = new Database(true, true); using var working = new WorkingDatabaseScope(db);
        var layouts = new List<object>();
        for (var i = 0; i < tools.Length; i++)
        {
            var tool = tools[i]; var definition = BlockInserter.Import(db, catalog, tool);
            var rt = tool.BlockName.StartsWith("RT "); var firstTag = rt ? "RT_01:" : "RI1";
            ObjectId rackId, untouchedId;
            using (var tx = db.TransactionManager.StartTransaction())
            {
                using var rack = BlockInserter.CreateReference(db, tx, definition, tool, new Point3d(i * 40, 0, 0), db.LayerZero, Matrix3d.Identity);
                BlockInserter.Append(db, tx, rack, false, new Dictionary<string, string> { [firstTag] = "Bestaande richtingtekst" }); rackId = rack.ObjectId;
                using var untouched = BlockInserter.CreateReference(db, tx, definition, tool, new Point3d(i * 40, 20, 0), db.LayerZero, Matrix3d.Identity);
                BlockInserter.Append(db, tx, untouched, false); untouchedId = untouched.ObjectId;
                var source = (BlockTableRecord)tx.GetObject(definition, OpenMode.ForRead);
                var slots = LsRackFuses.Slots(source, tx);
                Check(slots.Length == LsRackFuses.DirectionCount(tool.BlockName), tool.BlockName + ": alle fysieke richtingen herkend");
                Check(slots.Zip(slots.Skip(1), (a, b) => rt ? a.Center.Y < b.Center.Y : a.Center.X < b.Center.X).All(v => v), tool.BlockName + ": richtingvolgorde op de juiste fysieke rondjes");
                Check(LsRackFuses.Active(source, tx).Length == 0, tool.BlockName + ": standaard lege rondjes");
                if (i == 0)
                {
                    using var dialog = LsRackFuses.CreateChoiceDialog(slots, new[] { 1, 3, 12 }, true);
                    dialog.ShowInTaskbar = false; dialog.Opacity = 0; dialog.Show(); Application.DoEvents();
                    var list = dialog.Controls.OfType<TableLayoutPanel>().Single().Controls.OfType<CheckedListBox>().Single();
                    Check(list.Items.Count == 12 && list.CheckedIndices.Cast<int>().SequenceEqual(new[] { 0, 2, 11 }), "keuzevenster koppelt vinkjes aan de juiste richtingnummers");
                    using var image = new System.Drawing.Bitmap(dialog.Width, dialog.Height);
                    dialog.DrawToBitmap(image, new System.Drawing.Rectangle(System.Drawing.Point.Empty, image.Size)); image.Save(Path.Combine(output, "Richtingen-in-gebruik.png"));
                }
                tx.Commit();
            }
            using (var tx = db.TransactionManager.StartTransaction())
            {
                var rack = (BlockReference)tx.GetObject(rackId, OpenMode.ForRead);
                rack.UpgradeOpen(); rack.Rotation = .4; rack.ScaleFactors = new Scale3d(1.5);
                LsRackFuses.Apply(db, tx, rack, catalog, Enumerable.Range(1, LsRackFuses.DirectionCount(tool.BlockName)));
                var target = (BlockTableRecord)tx.GetObject(rack.BlockTableRecord, OpenMode.ForRead);
                Check(SourceBlockImporter.OriginalName(target, tx) == tool.BlockName, tool.BlockName + ": pluginidentiteit behouden voor tekst- en onderhoudsacties");
                var slots = LsRackFuses.Slots(target, tx);
                var fuses = target.Cast<ObjectId>().Select(id => tx.GetObject(id, OpenMode.ForRead)).OfType<BlockReference>().ToArray();
                Check(fuses.Length == slots.Length, tool.BlockName + ": exact een zekering per ingeschakelde richting");
                foreach (var slot in slots)
                {
                    var fuse = fuses.OrderBy(f =>
                    {
                        var box = f.GeometricExtents;
                        return (box.MinPoint + (box.MaxPoint - box.MinPoint) * .5).DistanceTo(slot.Center);
                    }).First();
                    var ext = fuse.GeometricExtents; var center = ext.MinPoint + (ext.MaxPoint - ext.MinPoint) * .5;
                    var endpoints = LsRackFuses.FuseEndpoints(tx, fuse);
                    Check(center.DistanceTo(slot.Center) < 1e-7 && endpoints.Length == 4 && endpoints.All(p => Math.Abs(p.DistanceTo(slot.Center) - slot.Radius) < 1e-7),
                        tool.BlockName + ": richting " + slot.Number + " gecentreerd met vier uiteinden exact op de cirkel");
                    Check(rt ? fuse.Color.ColorIndex == 256 && fuse.Layer == DirectionLines.LayerName(slot.Number) : fuse.Color.ColorIndex == 6 && fuse.LayerId == db.LayerZero,
                        tool.BlockName + ": richting " + slot.Number + (rt ? " volgt eigen laagkleur" : " blijft roze op laag 0"));
                    var transformedCenter = center.TransformBy(rack.BlockTransform);
                    Check(transformedCenter.DistanceTo(slot.Center.TransformBy(rack.BlockTransform)) < 1e-7, tool.BlockName + ": zekering volgt gedraaid en geschaald rek");
                }
                Check(((BlockReference)tx.GetObject(untouchedId, OpenMode.ForRead)).BlockTableRecord == definition && LsRackFuses.Active((BlockTableRecord)tx.GetObject(definition, OpenMode.ForRead), tx).Length == 0,
                    tool.BlockName + ": andere invoeging en brondefinitie blijven leeg");
                Check(rack.AttributeCollection.Cast<ObjectId>().Select(id => (AttributeReference)tx.GetObject(id, OpenMode.ForRead)).Any(a => a.Tag == firstTag && a.TextString == "Bestaande richtingtekst"),
                    tool.BlockName + ": bestaande attribuuttekst behouden");
                if (rt)
                {
                    var bottom = ((DrawOrderTable)tx.GetObject(target.DrawOrderTableId, OpenMode.ForRead)).GetFullDrawOrder(0).Cast<ObjectId>().First();
                    Check(tx.GetObject(bottom, OpenMode.ForRead) is Wipeout, tool.BlockName + ": masker blijft achter tekst en zekeringen");
                }
                layouts.Add(new { Name = tool.BlockName, Slots = slots.Select(s => new { s.Number, X = s.Center.X, Y = s.Center.Y, s.Radius }).ToArray() });
                tx.Commit();
            }
            using (var tx = db.TransactionManager.StartTransaction())
            {
                var rack = (BlockReference)tx.GetObject(rackId, OpenMode.ForRead);
                LsRackFuses.Apply(db, tx, rack, catalog, new[] { 2 });
                Check(LsRackFuses.Active((BlockTableRecord)tx.GetObject(rack.BlockTableRecord, OpenMode.ForRead), tx).SequenceEqual(new[] { 2 }), tool.BlockName + ": opnieuw kiezen verwijdert uitgeschakelde zekeringen");
                var before = rack.BlockTableRecord; LsRackFuses.Apply(db, tx, rack, catalog, new[] { 2 });
                Check(rack.BlockTableRecord == before, tool.BlockName + ": zelfde keuze maakt geen dubbele symbolen");
                tx.Commit();
            }
            var layerId = rt ? DirectionLines.EnsureStyle(db, 1).Layer : db.LayerZero;
            using (var tx = db.TransactionManager.StartTransaction()) { ((LayerTableRecord)tx.GetObject(layerId, OpenMode.ForWrite)).IsLocked = true; tx.Commit(); }
            var rejected = false;
            try
            {
                using var tx = db.TransactionManager.StartTransaction();
                LsRackFuses.Apply(db, tx, (BlockReference)tx.GetObject(rackId, OpenMode.ForRead), catalog, new[] { 1 }); tx.Commit();
            }
            catch (InvalidOperationException) { rejected = true; }
            using (var tx = db.TransactionManager.StartTransaction())
            {
                var layer = (LayerTableRecord)tx.GetObject(layerId, OpenMode.ForWrite);
                Check(rejected && layer.IsLocked, tool.BlockName + ": vergrendelde richtinglaag beschermd"); layer.IsLocked = false;
                var rack = (BlockReference)tx.GetObject(rackId, OpenMode.ForRead);
                Check(LsRackFuses.Active((BlockTableRecord)tx.GetObject(rack.BlockTableRecord, OpenMode.ForRead), tx).SequenceEqual(new[] { 2 }), tool.BlockName + ": mislukte wijziging behoudt vorige toestand");
                tx.Commit();
            }
        }
        db.SaveAs(Path.Combine(output, "LS-rekken.dwg"), DwgVersion.Current);
        foreach (var (path, hash) in hashes) Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) == hash, "bron-DWG ongewijzigd");
        File.WriteAllText(Path.Combine(output, "rekken.json"), JsonSerializer.Serialize(new { Status = "PASS", Checks = checks, Layouts = layouts }, new JsonSerializerOptions { WriteIndented = true }));
        AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nLS RACK VALIDATION PASS: " + checks.Count);
    }
}

public sealed partial class Plugin
{
    [CommandMethod("TBLSREKVALIDATE", CommandFlags.Modal)]
    public void ValidateLsRacks()
    {
        try { LsRackDiagnostics.Run(); }
        catch (System.Exception ex)
        {
            Directory.CreateDirectory(LightmastDiagnostics.Output);
            File.WriteAllText(Path.Combine(LightmastDiagnostics.Output, "rekken-failure.log"), ex.ToString());
            AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nLS RACK VALIDATION FAILED: " + ex.Message);
        }
    }
}
