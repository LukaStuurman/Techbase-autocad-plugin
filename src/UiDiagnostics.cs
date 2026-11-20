using System.Drawing;
using System.IO;
using System.Text.Json;
using Autodesk.AutoCAD.Runtime;

namespace Techbase.Blocks;

internal static class UiDiagnostics
{
    internal static void Run()
    {
        var failure = Path.Combine(LightmastDiagnostics.Output, "ui-failure.log"); if (File.Exists(failure)) File.Delete(failure);
        var folder = Path.Combine(LightmastDiagnostics.Output, "UI-" + typeof(Plugin).Assembly.GetName().Version!.ToString(3)); Directory.CreateDirectory(folder);
        using (var logo = typeof(Plugin).Assembly.GetManifestResourceStream("Techbase.Logo"))
        using (var output = File.Create(Path.Combine(folder, "Techbase-logo.png"))) logo!.CopyTo(output);
        var checks = new List<string>();
        void Check(bool ok, string text) { if (!ok) throw new InvalidOperationException("UI: " + text); checks.Add(text); }
        IEnumerable<Control> All(Control c) { yield return c; foreach (Control child in c.Controls) foreach (var item in All(child)) yield return item; }
        using var shell = new WorkspacePanel(new BlocksPanel(), new LightmastPanel(), new KabelCheckerPanel(), new DirectionPanel(), new SettingsPanel(), new KabelgeulPanel(), new StationLocationsPanel(), new ZroPanel());
        using var host = new Form { ShowInTaskbar = false, FormBorderStyle = FormBorderStyle.None, Opacity = 0, ClientSize = new Size(380, 940) };
        host.Controls.Add(shell); host.Show(); Application.DoEvents();
        shell.CreateControl();
        var names = new[] { "Blocks", "Lichtmasten", "Kabelchecker", "Richtingen", "Instellingen", "Kabelgeul", "Stationslocaties", "ZRO-bladen" };
        foreach (var width in new[] { 380, 1040 })
        {
            host.ClientSize = new Size(width, 940); shell.PerformLayout(); Application.DoEvents();
            for (var index = 0; index < 8; index++)
            {
                shell.Navigate(index); shell.PerformLayout();
                foreach (var control in All(shell).Where(c => c.Visible)) { _ = control.Handle; control.PerformLayout(); }
                Application.DoEvents();
                Check(shell.SelectedIndex == index, names[index] + " navigatie op " + width);
                Check(All(shell).Where(c => c.Visible).OfType<Button>().All(b => b.Height >= (index == 2 ? 24 : 28)), names[index] + " bruikbare knoppen op " + width);
                using var bitmap = new Bitmap(width, shell.Height);
                shell.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                bitmap.Save(Path.Combine(folder, names[index] + "-" + width + ".png"));
            }
        }
        shell.Navigate(6);
        var stationPanel = All(shell).OfType<StationLocationsPanel>().Single();
        var stationHeader = All(stationPanel).OfType<StationLocationHeader>().Single();
        var stationButton = stationHeader.NewLocation;
        Check(stationButton.Text == "Nieuwe locatie" && stationButton.PrimaryAction,
            "Stationslocaties toont een primaire nieuwe-locatieactie");
        Check(stationHeader.RefreshLink.Text == "Lijst vernieuwen"
            && stationHeader.HelpLink.Text == "Uitleg en tips",
            "stationsacties hebben begrijpelijke, zichtbare teksten");
        Check(stationButton.Width >= 140 && stationButton.Height >= 38 &&
            stationButton.Parent!.ClientRectangle.Contains(stationButton.Bounds),
            "Nieuwe locatie staat volledig binnen zijn koprij");
        Check(stationButton.Region != null &&
            !stationButton.Region.IsVisible(1, stationButton.Height - 2) &&
            stationButton.Region.IsVisible(stationButton.Width / 2, stationButton.Height - 3),
            "Nieuwe locatie heeft ronde onderste hoeken");
        Check(All(stationPanel).OfType<ThemedButton>().Any(b => b.Text == "Project")
            && All(stationPanel).OfType<ThemedButton>().Any(b => b.Text == "Uitvoer"),
            "Stationslocaties scheidt project- en uitvoerfuncties");
        shell.Navigate(0);
        var search = All(shell).OfType<TextBox>().Single(c => c.PlaceholderText == "Zoek een block...");
        var blocksPage = All(shell).OfType<BlocksPanel>().Single();
        var library = All(blocksPage).OfType<ListView>().Single();
        var originalCount = library.Items.Count;
        search.Text = "NIEUWE MOF";
        Check(library.Items.Count > 0 && library.Items.Count < originalCount && library.Items.Cast<ListViewItem>().All(i => i.Text.Contains("NIEUWE MOF", StringComparison.OrdinalIgnoreCase)), "zoeken filtert de echte blockbibliotheek");
        library.Items[0].Selected = true;
        shell.Navigate(4); shell.Navigate(0);
        Check(search.Text == "NIEUWE MOF" && library.SelectedItems.Count == 1, "navigeren behoudt zoekterm en blockselectie");
        search.Text = "";
        var options = All(shell).OfType<Button>().Single(b => b.AccessibleName == "Plaatsopties");
        options.PerformClick(); Check(All(shell).OfType<CheckBox>().Single(c => c.Name == "UseChosenBlockLayer").Visible, "plaatsopties uitklappen");
        // Alle drie schakelaars in Blocks delen één layoutkolom. Controleer dit
        // bij zowel brede als smalle werkplaatsen om inspringen te voorkomen.
        foreach (var width in new[] { 380, 1040 })
        {
            host.ClientSize = new Size(width, 940);
            shell.PerformLayout(); Application.DoEvents();
            var toggles = All(blocksPage).OfType<ThemedToggle>()
                .Where(c => c.Text is "Laag van object overnemen"
                    or "Gekozen laag gebruiken" or "Herhaald plaatsen (Esc stopt)")
                .OrderBy(c => c.Top).ToArray();
            Check(toggles.Length == 3 &&
                toggles.All(c => c.Parent == toggles[0].Parent && c.Left == toggles[0].Left) &&
                toggles[0].Top < toggles[1].Top && toggles[1].Top < toggles[2].Top,
                "drie Blocks-schakelaars staan recht onder elkaar op " + width);
        }
        options.PerformClick(); Check(!All(shell).OfType<CheckBox>().Single(c => c.Name == "UseChosenBlockLayer").Visible, "plaatsopties inklappen");
        shell.Navigate(1);
        var advanced = All(shell).OfType<CheckBox>().Single(c => c.Text == "Geavanceerde modus");
        advanced.Checked = true;
        Check(All(shell).OfType<CheckedListBox>().All(c => c.Visible), "geavanceerde modus toont lagen en blockfilter");
        host.ClientSize = new Size(380, 940); Application.DoEvents();
        var picker = All(shell).OfType<ComboBox>().Single(x => x.AccessibleName == "Kies Techbase-onderdeel");
        Check(picker.Visible && picker.Items.Count == 8, "compacte navigatie toont alle acht onderdelen bij versmallen");
        using (var image = new Bitmap(shell.Width, shell.Height))
        { shell.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(Path.Combine(folder, "Lichtmasten-advanced-380.png")); }
        advanced.Checked = false; shell.Navigate(0); host.ClientSize = new Size(1040, 940); Application.DoEvents();
        using (var bitmap = new Bitmap(shell.Width, shell.Height))
        { shell.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(folder, "Blocks-1040.png")); }
        Check(All(shell).All(c => c.Font.FontFamily.Name == Typography.Family), "Arial Nova Light behouden in alle onderdelen");
        shell.Navigate(3);
        var directionPage = All(shell).OfType<DirectionPanel>().Single();
        Check(All(directionPage).OfType<ThemedButton>().Count(b => b.Text.Length == 0 && b.Glyph >= 12 && !string.IsNullOrWhiteSpace(b.AccessibleName)) == 8,
            "acht richtingacties als icoon met toegankelijke naam");
        shell.Navigate(2);
        var checker = All(shell).OfType<KabelCheckerPanel>().Single();
        var calculate = All(checker).OfType<Button>().Single(b => b.Name == "CalculateDirectionButton");
        foreach (var size in new[] { new Size(900, 680), new Size(960, 780), new Size(820, 700), new Size(420, 640), new Size(1040, 940) })
        {
            host.ClientSize = size; Application.DoEvents(); shell.PerformLayout(); Application.DoEvents();
            var bounds = shell.RectangleToClient(calculate.Parent!.RectangleToScreen(calculate.Bounds));
            Check(calculate.Visible && bounds.Top >= 0 && bounds.Bottom <= shell.ClientSize.Height && bounds.Left >= 0 && bounds.Right <= shell.ClientSize.Width,
                "berekenknop vast zichtbaar op " + size.Width + "x" + size.Height);
            if (size.Width >= 820) Check(!checker.HasVerticalScroll, "geen verticale paginaschuifbalk kabelchecker op " + size.Width + "x" + size.Height + ": " + checker.Builder.CompactMetrics() + "; " + checker.ViewportMetrics());
            using var image = new Bitmap(shell.Width, shell.Height);
            shell.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(Path.Combine(folder, "Kabelchecker-" + size.Width + "x" + size.Height + ".png"));
        }
        host.ClientSize = new Size(960, 780); Application.DoEvents();
        var aligned = 0;
        foreach (var label in All(checker.Builder).OfType<Label>().Where(l => l.Visible && l.Text.TrimEnd().EndsWith(':') && l.Parent is FlowLayoutPanel))
        {
            var siblings = label.Parent!.Controls.Cast<Control>().ToArray();
            var input = siblings.Skip(Array.IndexOf(siblings, label) + 1).FirstOrDefault(c => c.Visible && c is TextBox or ComboBox or NumericUpDown);
            if (input == null) continue;
            Check(Math.Abs(label.Top + label.Height / 2f - input.Top - input.Height / 2f) <= 2,
                "label verticaal gecentreerd bij invoerveld: " + label.Text + ": " + label.Bounds + " / " + input.Bounds + " in " + label.Parent.ClientSize);
            aligned++;
        }
        Check(aligned >= 7, "uitlijning van minimaal zeven invoerlabels gecontroleerd");
        Check(checker.Builder.Font.Size <= 8.25f, "kleinere kabelcheckerlettertypes");
        var grids = All(checker.Builder).OfType<DataGridView>().ToArray();
        Check(grids.All(g => g.ColumnHeadersDefaultCellStyle.BackColor.GetBrightness() > .8f), "uitsluitend lichte tabelkoppen");
        Check(All(shell).Where(c => c is Panel or UserControl or Form).All(c => c.BackColor.GetBrightness() > .8f), "alle panelen van de plugin hebben een lichte achtergrond");
        var cableGrid = grids.Single(g => g.Columns.Cast<DataGridViewColumn>().Any(c => c.HeaderText == "Kabeltype"));
        var loadGrid = grids.Single(g => g.Columns.Contains("Amps"));
        host.ClientSize = new Size(900, 680); Application.DoEvents();
        var compactLoadHeight = loadGrid.Height; var cableHeight = cableGrid.Height;
        Check(cableGrid.Height > loadGrid.Height, "kabeltabel groter dan ontwerpstroomtabel in compact venster");
        host.ClientSize = new Size(960, 780); Application.DoEvents();
        Check(loadGrid.Height > compactLoadHeight && cableGrid.Height == cableHeight, "extra vensterhoogte gaat naar ontwerpstroomtabel");
        Check(grids.All(g => g.DefaultCellStyle.Font.Size <= 8.25f), "ook tabeltekst gebruikt het compacte lettertype");
        var deleteAll = All(checker).OfType<Button>().Single(b => b.AccessibleName == "Alle richtingen verwijderen");
        using (var square = new Bitmap(deleteAll.Width, deleteAll.Height))
        {
            deleteAll.DrawToBitmap(square, new Rectangle(Point.Empty, square.Size));
            Check(square.GetPixel(2, 2).ToArgb() == UiTheme.Danger.ToArgb(), "alles verwijderen is rood tot in de rechte hoeken");
        }
        using (var icons = new Bitmap(900, 45))
        {
            using var graphics = Graphics.FromImage(icons); graphics.Clear(Color.White); graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            for (var i = 0; i < 25; i++) ThemedButton.PaintGlyph(graphics, i, new RectangleF(5 + i * 36, 10, 20, 22), Color.Black);
            icons.Save(Path.Combine(folder, "Alle-iconen.png"));
        }
        foreach (var scale in new[] { 1f, 1.25f, 1.5f, 2f })
        {
            using var strip = new Bitmap((int)(200 * scale), (int)(50 * scale));
            var boxes = Enumerable.Range(0, 4).Select(i => new RectangleF((10 + i * 45) * scale, 10 * scale, 20 * scale, 22 * scale)).ToArray();
            using (var graphics = Graphics.FromImage(strip))
            {
                graphics.Clear(Color.White); graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                for (var i = 0; i < 4; i++) ThemedButton.PaintGlyph(graphics, 14 + i, boxes[i], Color.Black);
            }
            bool outside = false;
            for (var y = 0; y < strip.Height; y++)
                for (var x = 0; x < strip.Width; x++)
                    if (strip.GetPixel(x, y).R < 150 && !boxes.Any(b => RectangleF.Inflate(b, 1, 1).Contains(x, y))) outside = true;
            Check(!outside, "LS/OV pictogrammen blijven binnen hun tekenvak op " + scale * 100 + "%");
            foreach (var box in boxes)
            {
                var top = Rectangle.Round(new RectangleF(box.X, box.Y, box.Width, 12 * scale));
                Check(Enumerable.Range(top.Left, top.Width).Any(x => Enumerable.Range(top.Top, top.Height).Any(y => strip.GetPixel(x, y).R < 150)), "LS/OV letters volledig in eigen pictogram op " + scale * 100 + "%");
            }
            strip.Save(Path.Combine(folder, "Richting-iconen-" + (int)(scale * 100) + ".png"));
        }
        var late = new ThemedButton { Text = "Later toegevoegd", AutoSize = true };
        shell.Controls.Add(late); Check(late.FlatStyle == FlatStyle.Flat && late.MinimumSize.Height >= 32, "dynamische controls volgen huisstijl");
        shell.Controls.Remove(late); late.Dispose();
        var loadPanel = All(checker.Builder).OfType<Enexis.KabelChecker.AutoCAD.CurrentLoadPanel>().Single();
        var verdict = All(loadPanel).OfType<Label>().Single(l => l.Name == "CurrentLoadFitResult");
        var calculation = new Enexis.KabelChecker.Core.KabelCheckerEngine().Calculate(
            new[] { new Enexis.KabelChecker.Core.CableSegment("4*150mm2 Al", 100) }, Enexis.KabelChecker.Core.LoadProfile.Evenredig);
        var option = Enexis.KabelChecker.AutoCAD.ExcelLoadCatalog.For(Enexis.KabelChecker.AutoCAD.KaderVersionSelection.Current).First(x => x.CableDesignCurrentAmps > 0);
        foreach (var count in new[] { 1, 10000 })
        {
            loadPanel.LoadCurrentLoads(new[] { new Enexis.KabelChecker.AutoCAD.CurrentLoadInput(option.CableDesignCurrentAmps, count) },
                new[] { new Enexis.KabelChecker.AutoCAD.ExcelMappedLoad(option.Key, option.CableDesignCurrentAmps, count) });
            loadPanel.SetCalculation(calculation); Application.DoEvents();
            var fits = count == 1;
            Check(verdict.Text == (fits ? "PAST" : "PAST NIET") && verdict.ForeColor == (fits ? Color.SeaGreen : Color.Firebrick) && verdict.Font.Size >= 16 && verdict.Font.Bold,
                "grote resultaatmelding met juiste kleur: " + verdict.Text);
            var textRows = verdict.Parent!.Controls.OfType<Label>().Where(l => l != verdict).OrderBy(l => l.Top).ToArray();
            Check(textRows.Length == 2 && textRows[0].Left == textRows[1].Left && textRows[0].Bottom <= textRows[1].Top && verdict.Left >= textRows[0].Right,
                "resultaatteksten onder elkaar met melding rechts");
            using var resultImage = new Bitmap(shell.Width, shell.Height);
            shell.DrawToBitmap(resultImage, new Rectangle(Point.Empty, resultImage.Size)); resultImage.Save(Path.Combine(folder, fits ? "Kabelchecker-PAST.png" : "Kabelchecker-PAST-NIET.png"));
        }
        loadPanel.SetCalculation(null);
        Check(verdict.Text == "", "geen verouderde resultaatmelding zonder berekening");
        File.WriteAllText(Path.Combine(folder, "ui.json"), JsonSerializer.Serialize(new { Status = "PASS", Checks = checks }, new JsonSerializerOptions { WriteIndented = true }));
        Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nUI VALIDATION PASS: " + checks.Count);
    }
}

public sealed partial class Plugin
{
    [CommandMethod("TBUIVALIDATE", CommandFlags.Modal)]
    public void ValidateUi()
    {
        try { UiDiagnostics.Run(); }
        catch (System.Exception ex)
        {
            File.WriteAllText(Path.Combine(LightmastDiagnostics.Output, "ui-failure.log"), ex.ToString());
            Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nUI VALIDATION FAILED: " + ex.Message);
        }
    }
}
