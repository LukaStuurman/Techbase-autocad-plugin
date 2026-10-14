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
        using var editor = new KabelgeulEditor(parsed);
        var grid = editor.Grid;
        Check(grid.Rows.Count == 18 && grid.Columns.Count == 8, "formulier ondersteunt alle achttien kabeltypen en acht velden");
        var half = grid.Rows.Cast<DataGridViewRow>().Single(r => Convert.ToString(r.Cells[0].Value) == "Half rooien");
        Check(half.Cells[2].ReadOnly && half.Cells[3].ReadOnly && !half.Cells[4].ReadOnly && half.Cells[5].ReadOnly, "Half rooien toont dubbele kabel en onafhankelijke bestaande tekst");
        Check(editor.CaptureData().Lisp() == parsed.Lisp(), "formulier bewaart alle gegevens bij lezen/schrijven");
        IEnumerable<Control> Descendants(Control control)
        { yield return control; foreach (Control child in control.Controls) foreach (var descendant in Descendants(child)) yield return descendant; }
        var controls = Descendants(editor).ToArray();
        var reverse = controls.OfType<Button>().Single(b => b.Text == "Volgorde omdraaien"); reverse.PerformClick();
        Check(editor.CaptureData().Rows[0] == parsed.Rows[^1], "volgorde omdraaien bewaart alle rijgegevens"); reverse.PerformClick();
        grid.CurrentCell = grid.Rows[8].Cells[0];
        var originalRow = parsed.Rows[8];
        controls.OfType<Button>().Single(b => b.Text == "Rij vóór").PerformClick();
        Check(grid.Rows.Count == 19 && editor.CaptureData().Rows[9] == originalRow && grid.CurrentRow!.Index == 8, "rij vóór geselecteerde rij invoegen en selecteren");
        controls.OfType<Button>().Single(b => b.Text == "Rij na").PerformClick();
        Check(grid.Rows.Count == 20 && editor.CaptureData().Rows[10] == originalRow && grid.CurrentRow!.Index == 9, "rij tussen bestaande rijen na selectie invoegen");
        controls.OfType<Button>().Single(b => b.Text == "Rij verwijderen").PerformClick(); grid.CurrentCell = grid.Rows[8].Cells[0];
        controls.OfType<Button>().Single(b => b.Text == "Rij verwijderen").PerformClick();
        Check(grid.Rows.Count == 18 && editor.CaptureData().Lisp() == parsed.Lisp(), "invoegen/verwijderen bewaart alle oorspronkelijke rijgegevens");
        grid.CurrentCell = grid.Rows[0].Cells[0]; editor.InsertRow(false);
        Check(grid.CurrentRow!.Index == 0 && editor.CaptureData().Rows[1] == parsed.Rows[0], "invoegen voor eerste rij");
        editor.Set(parsed); grid.CurrentCell = grid.Rows[^1].Cells[0]; editor.InsertRow(true);
        Check(grid.CurrentRow!.Index == 18 && editor.CaptureData().Rows[17] == parsed.Rows[^1], "invoegen na laatste rij");
        editor.Set(new KabelgeulData(Enumerable.Repeat(new KabelgeulRow(),100).ToArray())); editor.InsertRow(true);
        Check(grid.Rows.Count == 100, "maximum van honderd kabelrijen bewaakt");
        Check(controls.All(c => c.Font.Name == Typography.Family), "hele invoervenster gebruikt Arial Nova Light");
        using var panel = new KabelgeulPanel();
        Check(panel.Editor.Grid.Rows.Count == 18 && panel.Editor.Grid.Columns[0].ReadOnly, "tab is een vaste instelling per kabeltype");
        Check(!Descendants(panel).OfType<Button>().Any(b => b.Text.StartsWith("Rij ")), "tab bevat geen misleidende lijst met plaatsingsrijen");
        Check(KabelgeulData.Types.Take(12).Select((type, i) => KabelgeulData.Rule(new Settings(), type).Text == "Ri " + (i + 1)).All(ok => ok), "standaardtekst toont Ri en het nummer voor alle twaalf richtingen");
        var migrated = new Settings(); migrated.KabelgeulDefaults["K10"] = new KabelgeulRow("K10", "150Al", TwoSides:true, Text2:"Ri 10 16Cu");
        KabelgeulData.MigrateText(migrated);
        Check(migrated.KabelgeulDefaults["K10"].Text == "Ri 10 150Al" && migrated.KabelgeulDefaults["K10"].Text2 == "Ri 10 16Cu", "oude bewaarde regels krijgen eenmaal het eerder impliciete voorvoegsel");
        migrated.KabelgeulDefaults["K10"] = migrated.KabelgeulDefaults["K10"] with { Text = "Eigen tekst", Text2 = "" };
        KabelgeulData.MigrateText(migrated);
        Check(KabelgeulData.Rule(migrated,"K10").Text == "Eigen tekst" && KabelgeulData.Rule(migrated,"K10").Text2 == "", "aangepaste volledige tekst en bewust lege tekst worden niet opnieuw aangevuld");
        using (var existingEditor = new KabelgeulEditor(new KabelgeulData(new[] { new KabelgeulRow("K01", "Ri 1", TwoSides:true, Text2:"" ) }), useDefaultsOnTypeChange:false))
        {
            existingEditor.Grid.Rows[0].Cells[0].Value = "K10";
            Check(existingEditor.CaptureData().Rows[0].Text == "Ri 1" && existingEditor.CaptureData().Rows[0].Text2 == "", "kabeltype wijzigen bij bewerken/kopieren behoudt beide bestaande teksten zonder defaults");
        }
        using (var newEditor = new KabelgeulEditor(new KabelgeulData(new[] { KabelgeulData.Rule(new Settings(),"K01") })))
        {
            newEditor.Grid.Rows[0].Cells[0].Value = "K10";
            Check(newEditor.CaptureData().Rows[0].Text == "Ri 10", "bij nieuw plaatsen wisselt onaangepaste standaardtekst mee met kabeltype");
            newEditor.Grid.Rows[0].Cells[1].Value = "Eigen kabel"; newEditor.Grid.Rows[0].Cells[0].Value = "K12";
            Check(newEditor.CaptureData().Rows[0].Text == "Eigen kabel", "eigen tekst blijft ook bij nieuw plaatsen behouden na typewissel");
        }
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
        var previous = Environment.GetEnvironmentVariable("TECHBASE_SETTINGS_FILE");
        var previousDefaults = Plugin.Preferences.KabelgeulDefaults;
        var previousMantel = Plugin.Preferences.KabelgeulMantelSpec;
        var settingsPath = Path.Combine(output, "defaults-test.json");
        try
        {
            Environment.SetEnvironmentVariable("TECHBASE_SETTINGS_FILE", settingsPath);
            var settings = new Settings { KabelgeulMantelSpec = "PVC 160" };
            settings.KabelgeulDefaults["K10"] = new KabelgeulRow("K10", "150Al", "Combi", true, "OV 16Cu", "Dubbel", "A", true);
            settings.Save(); var reloaded = Settings.Read();
            Check(KabelgeulData.Rule(reloaded,"K10") == KabelgeulData.Rule(settings,"K10") && reloaded.KabelgeulMantelSpec == "PVC 160", "standaardteksten en alle opmaakregels blijven na herstart bewaard");
            var detected = new KabelgeulData(new[] { new KabelgeulRow("K10"), new KabelgeulRow("K01") }).WithDefaults(reloaded);
            Check(detected.Rows[0] == settings.KabelgeulDefaults["K10"] && detected.Rows[1].Type == "K01", "standaarden volgen gedetecteerd kabeltype en behouden kabelvolgorde");
            reloaded.KabelgeulDefaults["Half rooien"] = new KabelgeulRow("Half rooien", Shape:"Combi", TwoSides:true);
            Check(KabelgeulData.Rule(reloaded,"Half rooien").Shape == "Dubbel" && !KabelgeulData.Rule(reloaded,"Half rooien").TwoSides, "Half rooien blijft geldig bij afwijkende opgeslagen regels");
            using var settingsPanel = new KabelgeulPanel(); settingsPanel.Size = new Size(1040,650); settingsPanel.CreateControl();
            var settingsGrid = settingsPanel.Editor.Grid; _ = settingsGrid.Handle;
            settingsGrid.CurrentCell = settingsGrid.Rows[9].Cells[1];
            Check(settingsGrid.BeginEdit(true), "standaardtekst is in de echte tabel bewerkbaar");
            var input = (DataGridViewTextBoxEditingControl)settingsGrid.EditingControl;
            input.Text = "150"; settingsGrid.NotifyCurrentCellDirty(true);
            Check(settingsGrid.IsCurrentCellInEditMode, "tekstbewerking blijft actief tijdens typen");
            input.Text += "Al standaard"; settingsGrid.EndEdit();
            settingsGrid.Rows[9].Cells[2].Value = "Combi";
            settingsPanel.Editor.MantelSpecBox.Text = "HDPE 160 standaard";
            var savedUi = Settings.Read();
            Check(savedUi.KabelgeulDefaults["K10"].Text == "150Al standaard" && savedUi.KabelgeulDefaults["K10"].Shape == "Combi" && savedUi.KabelgeulMantelSpec == "HDPE 160 standaard", "tekst, vorm en mantelspecificatie worden door de tab automatisch opgeslagen");
            using var reopened = new KabelgeulPanel();
            Check(Convert.ToString(reopened.Editor.Grid.Rows[9].Cells[1].Value) == "150Al standaard", "opnieuw geopende tab toont de bewaarde regels");
        }
        finally { Environment.SetEnvironmentVariable("TECHBASE_SETTINGS_FILE", previous); Plugin.Preferences.KabelgeulDefaults = previousDefaults; Plugin.Preferences.KabelgeulMantelSpec = previousMantel; }
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        var frame = AcApp.GetSystemVariable("WIPEOUTFRAME"); KabelgeulCommands.Pending = new(doc);
        var style = KabelgeulCommands.Initialize(); KabelgeulCommands.Finish();
        Check(AcApp.GetSystemVariable("WIPEOUTFRAME").Equals(frame) && KabelgeulCommands.Active == null, "commandocontext en wipeoutinstelling hersteld");
        Check(style.StartsWith("TECHBASE_ARIAL_NOVA_LIGHT"), "geultekst gebruikt de eigen Light-stijl");
        File.WriteAllText(Path.Combine(output, "formulier.json"), JsonSerializer.Serialize(new { Status = "PASS", Checks = checks }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
