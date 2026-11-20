using System.IO;
using System.Text.Json;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Techbase.Blocks;

internal static class OvRackDiagnostics
{
    internal static void Run()
    {
        var output = Path.Combine(LightmastDiagnostics.Output, "OvRekken"); Directory.CreateDirectory(output);
        var catalog = new Catalog(Plugin.DefaultPalette);
        var tools = catalog.Tools.Where(t => LsRackFuses.IsRack(t.BlockName) && LsRackFuses.IsOv(t.BlockName)).ToArray();
        var checks = new List<string>();
        void Check(bool ok, string text) { if (!ok) throw new InvalidOperationException("OV: " + text); checks.Add(text); }
        Check(tools.Length == 12, "vijf RT OV-blocks, drie OV-kasten en vier schematische rekken");
        using var db = new Database(true, true); using var working = new WorkingDatabaseScope(db);
        for (var index = 0; index < tools.Length; index++)
        {
            var tool = tools[index]; var definition = BlockInserter.Import(db, catalog, tool);
            using var tx = db.TransactionManager.StartTransaction();
            var source = (BlockTableRecord)tx.GetObject(definition, OpenMode.ForRead); var slots = LsRackFuses.Slots(source, tx);
            var count = LsRackFuses.DirectionCount(tool.BlockName); var numbers = Enumerable.Range(1, count).ToArray();
            Check(slots.Length == count * 4 && slots.GroupBy(s => s.Number).All(g => g.Select(s => s.Contact).SequenceEqual(new[] { 1, 2, 3, 4 })), tool.BlockName + ": vier unieke fysieke rondjes per richting");
            using var rack = BlockInserter.CreateReference(db, tx, definition, tool, new Point3d(index * 40, 0, 0), db.LayerZero, Matrix3d.Identity);
            BlockInserter.Append(db, tx, rack, false);
            using var copy = BlockInserter.CreateReference(db, tx, definition, tool, new Point3d(index * 40, 30, 0), db.LayerZero, Matrix3d.Identity);
            BlockInserter.Append(db, tx, copy, false);
            var text = rack.AttributeCollection.Cast<ObjectId>().Select(id => (AttributeReference)tx.GetObject(id, OpenMode.ForRead)).Select(a => (a.Tag, a.TextString)).ToArray();
            Check(LsRackFuses.Active(source, tx).Length == 0, tool.BlockName + ": standaard leeg");
            foreach (var combi in new[] { Array.Empty<int>(), numbers })
            {
                LsRackFuses.Apply(db, tx, rack, catalog, numbers, combi);
                var target = (BlockTableRecord)tx.GetObject(rack.BlockTableRecord, OpenMode.ForRead);
                var symbols = target.Cast<ObjectId>().Where(id => !id.IsErased).Select(id => tx.GetObject(id, OpenMode.ForRead)).OfType<BlockReference>().ToArray();
                var mode = combi.Length == 0 ? "normaal" : "combi";
                Check(symbols.Length == count * (combi.Length == 0 ? 3 : 4), tool.BlockName + ": juiste aantal zekeringen voor " + mode);
                foreach (var slot in slots)
                {
                    var match = symbols.FirstOrDefault(s =>
                    {
                        var ext = s.GeometricExtents;
                        return (ext.MinPoint + (ext.MaxPoint - ext.MinPoint) * .5).DistanceTo(slot.Center) < 1e-7;
                    });
                    var normalContacts = tool.BlockName.StartsWith("RT ") || tool.BlockName.StartsWith("OV", StringComparison.OrdinalIgnoreCase) ? new[] { 1, 2, 4 } : new[] { 1, 3, 4 };
                    Check((match != null) == (normalContacts.Contains(slot.Contact) || combi.Length > 0), tool.BlockName + ": " + mode + " richting " + slot.Number + " rondje " + slot.Contact);
                    if (match == null) continue;
                    var rt = tool.BlockName.StartsWith("RT "); var cabinet = tool.BlockName.StartsWith("OV", StringComparison.OrdinalIgnoreCase);
                    Check(rt ? match.Layer == DirectionLines.LayerName(slot.Number) && match.Color.ColorIndex == 256 : match.LayerId == db.LayerZero && match.Color.ColorIndex == (cabinet ? 256 : 6),
                        tool.BlockName + ": kleur/laag bij " + mode + " richting " + slot.Number);
                    var endpoints = LsRackFuses.FuseEndpoints(tx, match);
                    Check(endpoints.Length == 4 && endpoints.All(p => Math.Abs(slot.Center.DistanceTo(p) - slot.Radius) < 1e-7), tool.BlockName + ": vier symbooluiteinden exact op de cirkel");
                }
                Check(copy.BlockTableRecord == definition && LsRackFuses.Active(source, tx).Length == 0, tool.BlockName + ": gedeelde kopie en bron blijven leeg");
                Check(text.SequenceEqual(rack.AttributeCollection.Cast<ObjectId>().Select(id => (AttributeReference)tx.GetObject(id, OpenMode.ForRead)).Select(a => (a.Tag, a.TextString))), tool.BlockName + ": bestaande teksten behouden");
                if (tool.BlockName.StartsWith("RT "))
                    Check(tx.GetObject(((DrawOrderTable)tx.GetObject(target.DrawOrderTableId, OpenMode.ForRead)).GetFullDrawOrder(0)[0], OpenMode.ForRead) is Wipeout, tool.BlockName + ": masker blijft achteraan");
            }
            LsRackFuses.Apply(db, tx, rack, catalog, new[] { 1, 2 }, new[] { 2 });
            var mixed = (BlockTableRecord)tx.GetObject(rack.BlockTableRecord, OpenMode.ForRead);
            Check(LsRackFuses.Active(mixed, tx).SequenceEqual(new[] { 1, 2 }) && LsRackFuses.Combi(mixed, tx).SequenceEqual(new[] { 2 }), tool.BlockName + ": normale en combirichting samen bewaard");
            LsRackFuses.Apply(db, tx, rack, catalog, Array.Empty<int>());
            Check(LsRackFuses.Active((BlockTableRecord)tx.GetObject(rack.BlockTableRecord, OpenMode.ForRead), tx).Length == 0, tool.BlockName + ": terug naar leeg verwijdert alle zekeringen");
            if (index == 0)
            {
                using var form = OvRackChoice.Create(slots, new[] { 1, 2 }, new[] { 2 }, false);
                form.ShowInTaskbar = false; form.Opacity = 0; form.Show(); Application.DoEvents();
                var grid = form.Controls.OfType<TableLayoutPanel>().Single().Controls.OfType<DataGridView>().Single();
                Check(grid.Rows.Count == count && (string)grid.Rows[0].Cells[1].Value == "Normaal" && (string)grid.Rows[1].Cells[1].Value == "Combi" && (string)grid.Rows[2].Cells[1].Value == "Leeg", "keuzevenster toont Leeg/Normaal/Combi per richting");
                using var image = new System.Drawing.Bitmap(form.Width, form.Height);
                form.DrawToBitmap(image, new System.Drawing.Rectangle(System.Drawing.Point.Empty, image.Size)); image.Save(Path.Combine(output, "OV-keuze.png"));
            }
            tx.Commit();
        }
        File.WriteAllText(Path.Combine(output, "ov.json"), JsonSerializer.Serialize(new { Status = "PASS", Checks = checks }, new JsonSerializerOptions { WriteIndented = true }));
        AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nOV VALIDATION PASS: " + checks.Count);
    }
}

public sealed partial class Plugin
{
    [CommandMethod("TBOVREKVALIDATE", CommandFlags.Modal)]
    public void ValidateOvRacks()
    {
        try { OvRackDiagnostics.Run(); }
        catch (System.Exception ex)
        {
            Directory.CreateDirectory(LightmastDiagnostics.Output);
            File.WriteAllText(Path.Combine(LightmastDiagnostics.Output, "ov-failure.log"), ex.ToString());
            AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nOV VALIDATION FAILED: " + ex.Message);
        }
    }
}
