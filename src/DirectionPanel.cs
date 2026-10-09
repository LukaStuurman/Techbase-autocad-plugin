using System;
using System.Drawing;
using System.Windows.Forms;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Techbase.Blocks;

internal sealed class DirectionPanel : UserControl
{
    private readonly Label layer = new() { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 8) };
    private readonly Label status = new() { AutoSize = true, Dock = DockStyle.Fill, MaximumSize = new Size(340, 0), Padding = new Padding(0, 8, 0, 0) };
    private int selected = Plugin.Preferences.SelectedDirectionNumber;
    internal DirectionPanel()
    {
        Dock = DockStyle.Fill; Font = new Font("Segoe UI", 9); BackColor = Color.FromArgb(245, 247, 249);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, AutoScroll = true, Padding = new Padding(8) };
        layout.Controls.Add(new Label { Text = "RICHTINGEN", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Padding = new Padding(0, 4, 0, 8) });
        layout.Controls.Add(new Label { Text = "Kies een richting", AutoSize = true });
        var grid = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, RowCount = 4 };
        for (var c = 0; c < 3; c++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
        for (var n = 1; n <= 12; n++)
        {
            var number = n;
            var button = new RadioButton { Text = "Richting " + n, AutoSize = true, Checked = n == selected, Margin = new Padding(3, 6, 3, 6) };
            button.CheckedChanged += (_, _) =>
            {
                if (!button.Checked) return;
                selected = number; Plugin.Preferences.SelectedDirectionNumber = number; Plugin.UpdateSettings(); RefreshLayer();
            };
            grid.Controls.Add(button, (n - 1) % 3, (n - 1) / 3);
        }
        layout.Controls.Add(grid); layout.Controls.Add(layer);
        layout.Controls.Add(new Label { Text = "Global width: 0,1\nLijntype: DASHED\nLijntypeschaal: 0,0035\nKleur en lijndikte: volgens richtingslaag", AutoSize = true, Padding = new Padding(0, 4, 0, 12) });
        var draw = new Button { Text = "Nieuwe richting tekenen", Height = 36, Dock = DockStyle.Top };
        var convert = new Button { Text = "Selectie naar deze richting omzetten", Height = 36, Dock = DockStyle.Top };
        draw.Click += (_, _) => Queue("TBTEKENRICHTING"); convert.Click += (_, _) => Queue("TBMAAKRICHTING");
        layout.Controls.Add(draw); layout.Controls.Add(convert);
        layout.Controls.Add(new Label { Text = "Tekenen: klik punten, Enter = klaar.\nTerug verwijdert het laatste punt; Sluiten sluit de lijn.\nOmzetten: kies polylines of losse LINE-lijnen.\nPolylines behouden hun punten en bochten.", AutoSize = true, MaximumSize = new Size(340, 0), Padding = new Padding(0, 10, 0, 0) });
        layout.Controls.Add(new Label { Text = "TEKSTEN BOVEN KABELS", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Padding = new Padding(0, 12, 0, 6) });
        var textButtons = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 2 };
        textButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); textButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        foreach (var buttonInfo in new[] { ("LS-tekst plaatsen (burst)", "TBTEKSTLS"), ("OV-tekst plaatsen (burst)", "TBTEKSTOV"),
                     ("Tekst kopiëren", "TBTEKSTKOPIE"), ("Tekst verplaatsen", "TBTEKSTVERPLAATS") })
        {
            var button = new Button { Text = buttonInfo.Item1, Dock = DockStyle.Fill, Height = 36 };
            var command = buttonInfo.Item2; button.Click += (_, _) => Queue(command, false); textButtons.Controls.Add(button);
        }
        layout.Controls.Add(textButtons);
        layout.Controls.Add(new Label { Text = "LS/OV plaatst de losse richtingsteksten.\nKies daarna MTexten in klikvolgorde en twee punten langs de kabel. Kopiëren: herhalen; Terug wist de laatste kopie.", AutoSize = true,
            MaximumSize = new Size(340, 0), Padding = new Padding(0, 6, 0, 0) });
        layout.Controls.Add(status); Controls.Add(layout); RefreshLayer();
    }
    private void RefreshLayer() => layer.Text = "Laag: " + DirectionLines.LayerName(selected);
    private void Queue(string command, bool withDirection = true)
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc == null) { Report("Open eerst een tekening."); return; }
        if (Convert.ToInt32(AcApp.GetSystemVariable("CMDACTIVE")) != 0) { Report("Rond eerst het actieve commando af, of druk Esc."); return; }
        doc.SendStringToExecute("_" + command + " " + (withDirection ? selected + " " : ""), true, false, false);
    }
    internal void Report(string message) => status.Text = message;
}
