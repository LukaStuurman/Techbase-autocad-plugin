using System.Drawing;

namespace Techbase.Blocks;

internal sealed class SettingsPanel : UserControl
{
    private readonly ComboBox mof = new() { Name = "MofTextHeight", DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly ComboBox directions = new() { Name = "DirectionTextHeight", DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
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
        layout.Controls.Add(new Label { Text = "De keuze wordt automatisch bewaard.\nGeldt voor nieuwe MOF-blocks en richtingstekstblocks, ook via de LS/OV-burstknoppen.\n\nHet plaatsvoorbeeld gebruikt dezelfde teksthoogte. Bestaande geplaatste blocks en losse teksten blijven ongewijzigd.",
            AutoSize = true, MaximumSize = new Size(290, 0), Padding = new Padding(0, 14, 0, 0) });
        Controls.Add(layout);
        foreach (var choice in new[] { mof, directions }) choice.Items.AddRange(new object[] { "0,75 (standaard)", "0,45" });
        mof.SelectedIndexChanged += (_, _) => Save(true);
        directions.SelectedIndexChanged += (_, _) => Save(false);
        RefreshSettings(); UiTypography.Apply(this);
    }
    private void Save(bool isMof)
    {
        if (refreshing) return;
        var choice = isMof ? mof : directions;
        if (choice.SelectedIndex < 0) return;
        var value = choice.SelectedIndex == 0 ? 0.75 : 0.45;
        if (isMof) Plugin.Preferences.MofTextHeight = value;
        else Plugin.Preferences.DirectionTextHeight = value;
        Plugin.UpdateSettings();
    }
    internal void RefreshSettings()
    {
        refreshing = true;
        try { mof.SelectedIndex = Plugin.Preferences.MofTextHeight == 0.45 ? 1 : 0; directions.SelectedIndex = Plugin.Preferences.DirectionTextHeight == 0.45 ? 1 : 0; }
        finally { refreshing = false; }
    }
}
