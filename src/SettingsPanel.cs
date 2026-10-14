using System.Drawing;

namespace Techbase.Blocks;

internal sealed class SettingsPanel : UserControl
{
    private readonly ComboBox mof = new() { Name = "MofTextHeight", DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly ComboBox directions = new() { Name = "DirectionTextHeight", DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly ComboBox existingColor = new() { Name = "ExistingCableColor", DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly Label colorStatus = new() { AutoSize = true, MaximumSize = new Size(290, 0) };
    private bool refreshing;
    internal SettingsPanel()
    {
        Dock = DockStyle.Fill; AutoScroll = true; Font = new Font(Typography.Family, 9); BackColor = Color.FromArgb(245, 247, 249);
        var layout = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(12) };
        layout.Controls.Add(new Label { Text = "INSTELLINGEN", AutoSize = true, Padding = new Padding(0, 0, 0, 12) });
        layout.Controls.Add(new Label { Text = "Teksthoogte MOF-blocks", AutoSize = true });
        layout.Controls.Add(mof);
        layout.Controls.Add(new Label { Text = "Teksthoogte richtingsteksten (LS/OV)", AutoSize = true, Padding = new Padding(0, 14, 0, 0) });
        layout.Controls.Add(directions);
        layout.Controls.Add(new Label { Text = "Kleur bestaande kabellagen en Bestaand", AutoSize = true, Padding = new Padding(0, 14, 0, 0) });
        existingColor.Items.AddRange(new object[] { "Donkergroen (standaard)", "Zwart" }); layout.Controls.Add(existingColor);
        var apply = new Button { Text = "Op huidige tekening toepassen", AutoSize = true }; apply.Click += (_, _) => QueueColor(); layout.Controls.Add(apply);
        layout.Controls.Add(new Label { Text = "Zwart is RGB 0,0,0. Keuze blijft bewaard voor bestaande kabellagen en de laag Bestaand. Bij een andere tekening: klik Toepassen. Vergrendelde lagen worden gemeld.", AutoSize = true, MaximumSize = new Size(290, 0) });
        layout.Controls.Add(colorStatus);
        layout.Controls.Add(new Label { Text = "Breedte LS/OV-teksten: 4,80.\n\nDe keuze wordt automatisch bewaard.\nGeldt voor nieuwe MOF-blocks en richtingstekstblocks, ook via de LS/OV-burstknoppen.\n\nHet plaatsvoorbeeld gebruikt dezelfde teksthoogte. Bestaande geplaatste blocks en losse teksten blijven ongewijzigd.",
            AutoSize = true, MaximumSize = new Size(290, 0), Padding = new Padding(0, 14, 0, 0) });
        Controls.Add(layout);
        foreach (var choice in new[] { mof, directions }) choice.Items.AddRange(new object[] { "0,45 (standaard)", "0,75" });
        mof.SelectedIndexChanged += (_, _) => Save(true);
        directions.SelectedIndexChanged += (_, _) => Save(false);
        existingColor.SelectedIndexChanged += (_, _) => { if (refreshing || existingColor.SelectedIndex < 0) return; Plugin.Preferences.ExistingCableBlack = existingColor.SelectedIndex == 1; Plugin.UpdateSettings(); QueueColor(); };
        RefreshSettings(); UiTypography.Apply(this);
    }
    private void Save(bool isMof)
    {
        if (refreshing) return;
        var choice = isMof ? mof : directions;
        if (choice.SelectedIndex < 0) return;
        var value = choice.SelectedIndex == 0 ? 0.45 : 0.75;
        if (isMof) Plugin.Preferences.MofTextHeight = value;
        else Plugin.Preferences.DirectionTextHeight = value;
        Plugin.UpdateSettings();
    }
    internal void RefreshSettings()
    {
        refreshing = true;
        try { mof.SelectedIndex = Plugin.Preferences.MofTextHeight == 0.75 ? 1 : 0; directions.SelectedIndex = Plugin.Preferences.DirectionTextHeight == 0.75 ? 1 : 0; existingColor.SelectedIndex = Plugin.Preferences.ExistingCableBlack ? 1 : 0; }
        finally { refreshing = false; }
    }
    private void QueueColor()
    {
        var doc = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;
        if (doc == null) { colorStatus.Text = "Keuze opgeslagen. Open een tekening om de kleur toe te passen."; return; }
        if (Convert.ToInt32(Autodesk.AutoCAD.ApplicationServices.Core.Application.GetSystemVariable("CMDACTIVE")) != 0) { colorStatus.Text = "Keuze opgeslagen. Rond het commando af en klik Toepassen."; return; }
        doc.SendStringToExecute("TBBESTAANDEKLEUR ", true, false, false);
    }
    internal void ReportColor(string message) => colorStatus.Text = message;
}
