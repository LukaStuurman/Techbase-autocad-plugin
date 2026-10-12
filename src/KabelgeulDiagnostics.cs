using System.Drawing;
using System.IO;
using System.Text.Json;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Techbase.Blocks;

internal static class KabelgeulDiagnostics
{
    internal static void Run(string parent)
    {
        var output = Path.Combine(parent, "Kabelgeul"); Directory.CreateDirectory(output);
        var checks = new List<string>();
        void Check(bool ok, string description) { if (!ok) throw new InvalidOperationException(description); checks.Add(description); }
        var data = new KabelgeulData(KabelgeulData.Types.Select((type, i) => new KabelgeulRow(type,
            "Test \"" + type + "\" \\ pad\nregel 2", KabelgeulData.Shapes[i % 4], type != "Half rooien", "Zijde 2",
            KabelgeulData.Shapes[(i + 1) % 4], i % 2 == 0 ? "X" : "A", i % 3 == 0)).ToArray(), "PVC 160");
        var parsed = KabelgeulData.Parse(data.Lisp());
        foreach (var pair in data.Rows.Zip(parsed.Rows))
        {
            var expected = pair.First.Type == "Half rooien" ? pair.First with { Shape = "Dubbel", TwoSides = false } : pair.First;
            Check(expected == pair.Second, "Lisp-uitwisseling bewaart type, tekst, zijden, vormen, mantel en bak: " + expected.Type);
        }
        Check(parsed.MantelSpec == data.MantelSpec, "mantelspecificatie blijft behouden");
        foreach (var broken in new[] { "(", "(\"onvolledig", "nil", "()", data.Lisp() + " onjuist" })
        {
            var rejected = false; try { KabelgeulData.Parse(broken); } catch (FormatException) { rejected = true; }
            Check(rejected, "onvolledige Lisp-invoer geeft een gecontroleerde fout");
        }
        using var panel = new KabelgeulPanel(); panel.Editor.Set(parsed);
        var grid = panel.Editor.Grid;
        Check(grid.Rows.Count == 18 && grid.Columns.Count == 8, "formulier ondersteunt alle achttien kabeltypen en acht velden");
        var half = grid.Rows.Cast<DataGridViewRow>().Single(r => (string)r.Cells[0].Value == "Half rooien");
        Check(half.Cells[2].ReadOnly && half.Cells[3].ReadOnly && !half.Cells[4].ReadOnly && half.Cells[5].ReadOnly, "Half rooien toont dubbele kabel en onafhankelijke bestaande tekst");
        Check(panel.Editor.CaptureData().Lisp() == parsed.Lisp(), "formulier bewaart alle gegevens bij lezen/schrijven");
        IEnumerable<Control> Descendants(Control control)
        { yield return control; foreach (Control child in control.Controls) foreach (var descendant in Descendants(child)) yield return descendant; }
        var controls = Descendants(panel).ToArray();
        var reverse = controls.OfType<Button>().Single(b => b.Text == "Volgorde omdraaien"); reverse.PerformClick();
        Check(panel.Editor.CaptureData().Rows[0] == parsed.Rows[^1], "volgorde omdraaien bewaart alle rijgegevens"); reverse.PerformClick();
        controls.OfType<Button>().Single(b => b.Text == "Rij toevoegen").PerformClick();
        Check(grid.Rows.Count == 19, "nieuwe kabelrij toevoegen"); grid.CurrentCell = grid.Rows[^1].Cells[0];
        controls.OfType<Button>().Single(b => b.Text == "Rij verwijderen").PerformClick();
        Check(grid.Rows.Count == 18 && panel.Editor.CaptureData().Lisp() == parsed.Lisp(), "gekozen rij verwijderen bewaart overige rijen");
        Check(controls.All(c => c.Font.Name == Typography.Family), "hele tab gebruikt Arial Nova Light");
        panel.Editor.Set(KabelgeulData.Default);
        foreach (var width in new[] { 380, 1040 })
        {
            panel.Size = new Size(width, 650); panel.CreateControl(); panel.PerformLayout();
            foreach (var control in Descendants(panel)) { _ = control.Handle; control.PerformLayout(); }
            var textbox = Descendants(panel).OfType<TextBox>().Single();
            Check(textbox.Width > 100 && textbox.Height > 10 && textbox.Bottom <= textbox.Parent!.Height, "mantelspecificatie past binnen het formulier bij breedte " + width);
            using var bitmap = new Bitmap(width, 650); panel.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bitmap.Save(Path.Combine(output, $"Kabelgeul-tab-{width}.png"));
            Check(panel.Editor.Grid.Height > 100, "formulier houdt bruikbare kabeltabel bij breedte " + width);
        }
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        var frame = AcApp.GetSystemVariable("WIPEOUTFRAME");
        KabelgeulCommands.Pending = new(doc, parsed, false);
        var style = KabelgeulCommands.Initialize();
        using var args = new ResultBuffer(new TypedValue((int)LispDataType.Text, KabelgeulData.Default.Lisp()), new TypedValue((int)LispDataType.Text, "KABELGEUL"));
        Check(Plugin.ConfigureKabelgeul(args) == parsed.Lisp(), "handmatige tabinvoer heeft voorrang op gedetecteerde kabels zonder dialoog");
        KabelgeulCommands.Finish();
        Check(AcApp.GetSystemVariable("WIPEOUTFRAME").Equals(frame) && KabelgeulCommands.Active == null, "commandocontext en wipeoutinstelling hersteld");
        Check(style.StartsWith("TECHBASE_ARIAL_NOVA_LIGHT"), "geultekst gebruikt de eigen Light-stijl");
        File.WriteAllText(Path.Combine(output, "formulier.json"), JsonSerializer.Serialize(new { Status = "PASS", Checks = checks }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
