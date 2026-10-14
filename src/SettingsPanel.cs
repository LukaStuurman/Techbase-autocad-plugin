using System.Drawing;

namespace Techbase.Blocks;

internal sealed class SettingsPanel : UserControl
{
    private readonly ComboBox mof = new() { Name = "MofTextHeight", DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly ComboBox directions = new() { Name = "DirectionTextHeight", DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly ComboBox existingColor = new() { Name = "ExistingCableColor", DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly NumericUpDown width = Number("DirectionTextWidth", .01m, 10000m);
    private readonly NumericUpDown existingWidth = Number("ExistingDirectionTextWidth", .01m, 10000m);
    private readonly CheckBox mask = new() { Name = "DirectionTextMask", Text = "Achtergrondmasker gebruiken", AutoSize = true };
    private readonly NumericUpDown border = Number("DirectionTextMaskBorder", 1m, 5m);
    private readonly CheckBox drawingColor = new() { Name = "DirectionTextMaskDrawingColor", Text = "Tekeningachtergrondkleur gebruiken", AutoSize = true };
    private readonly Button maskColor = new() { Name = "DirectionTextMaskColor", Text = "Eigen maskerkleur kiezen", AutoSize = true };
    private readonly Label maskRgb = new() { AutoSize = true };
    private readonly Label colorStatus = new() { AutoSize = true, MaximumSize = new Size(290, 0) };
    private bool refreshing;
    private static NumericUpDown Number(string name, decimal min, decimal max) => new() { Name = name, Minimum = min, Maximum = max, DecimalPlaces = 2, Increment = .10m, Width = 220 };
    internal SettingsPanel()
    {
        Dock = DockStyle.Fill; AutoScroll = true; Font = new Font(Typography.Family, 9); BackColor = Color.FromArgb(245, 247, 249);
        var layout = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(12) };
        layout.Controls.Add(new Label { Text = "INSTELLINGEN", AutoSize = true, Padding = new Padding(0, 0, 0, 12) });
        layout.Controls.Add(new Label { Text = "Teksthoogte MOF-blocks", AutoSize = true });
        layout.Controls.Add(mof);
        layout.Controls.Add(new Label { Text = "Teksthoogte richtingsteksten (LS/OV)", AutoSize = true, Padding = new Padding(0, 14, 0, 0) });
        layout.Controls.Add(directions);
        layout.Controls.Add(new Label { Text = "Breedte LS/OV bij gekozen teksthoogte", AutoSize = true, Padding = new Padding(0, 8, 0, 0) }); layout.Controls.Add(width);
        layout.Controls.Add(new Label { Text = "Breedte bestaande kabeltekst (was ...)", AutoSize = true, Padding = new Padding(0, 8, 0, 0) }); layout.Controls.Add(existingWidth);
        layout.Controls.Add(new Label { Text = "Breedtes blijven per hoogte bewaard.\n0,45: standaard 4,80 / bestaand 7,80.\n0,75: standaard 7,70 / bestaand 10,70.", AutoSize = true, MaximumSize = new Size(290, 0) });
        layout.Controls.Add(mask);
        layout.Controls.Add(new Label { Text = "Randfactor masker (border offset)", AutoSize = true }); layout.Controls.Add(border);
        layout.Controls.Add(drawingColor); layout.Controls.Add(maskColor); layout.Controls.Add(maskRgb);
        layout.Controls.Add(new Label { Text = "Kleur bestaande kabellagen en Bestaand", AutoSize = true, Padding = new Padding(0, 14, 0, 0) });
        existingColor.Items.AddRange(new object[] { "Donkergroen (standaard)", "Zwart" }); layout.Controls.Add(existingColor);
        var apply = new Button { Text = "Op huidige tekening toepassen", AutoSize = true }; apply.Click += (_, _) => QueueColor(); layout.Controls.Add(apply);
        layout.Controls.Add(new Label { Text = "Zwart is RGB 0,0,0. Keuze blijft bewaard voor bestaande kabellagen en de laag Bestaand. Bij een andere tekening: klik Toepassen. Vergrendelde lagen worden gemeld.", AutoSize = true, MaximumSize = new Size(290, 0) });
        layout.Controls.Add(colorStatus);
        layout.Controls.Add(new Label { Text = "Keuzes worden automatisch bewaard. Breedte en masker gelden voor nieuwe LS/OV-blocks, burstteksten en automatische tekststapels. Losse teksten kun je ook handmatig aanpassen in AutoCAD. Kopiëren/verplaatsen behoudt je eigen breedte en masker.",
            AutoSize = true, MaximumSize = new Size(290, 0), Padding = new Padding(0, 14, 0, 0) });
        Controls.Add(layout);
        foreach (var choice in new[] { mof, directions }) choice.Items.AddRange(new object[] { "0,45 (standaard)", "0,75" });
        mof.SelectedIndexChanged += (_, _) => Save(true);
        directions.SelectedIndexChanged += (_, _) => Save(false);
        width.ValueChanged += (_, _) => SaveAppearance(); existingWidth.ValueChanged += (_, _) => SaveAppearance();
        mask.CheckedChanged += (_, _) => SaveAppearance(); border.ValueChanged += (_, _) => SaveAppearance(); drawingColor.CheckedChanged += (_, _) => SaveAppearance();
        maskColor.Click += (_, _) =>
        {
            using var dialog = new ColorDialog { FullOpen = true, Color = Color.FromArgb(unchecked((int)0xFF000000) | Plugin.Preferences.DirectionTextMaskRgb) };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            Plugin.Preferences.DirectionTextMaskRgb = dialog.Color.ToArgb() & 0xFFFFFF;
            Plugin.UpdateSettings(); RefreshSettings();
        };
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
        RefreshSettings();
    }
    private void SaveAppearance()
    {
        if (refreshing) return;
        var settings = Plugin.Preferences;
        if (settings.DirectionTextHeight == .75) { settings.DirectionTextWidth075 = (double)width.Value; settings.ExistingDirectionTextWidth075 = (double)existingWidth.Value; }
        else { settings.DirectionTextWidth045 = (double)width.Value; settings.ExistingDirectionTextWidth045 = (double)existingWidth.Value; }
        settings.DirectionTextMask = mask.Checked; settings.DirectionTextMaskBorder = (double)border.Value; settings.DirectionTextMaskDrawingColor = drawingColor.Checked;
        Plugin.UpdateSettings(); RefreshSettings();
    }
    internal void RefreshSettings()
    {
        refreshing = true;
        try
        {
            var settings = Plugin.Preferences;
            mof.SelectedIndex = settings.MofTextHeight == .75 ? 1 : 0; directions.SelectedIndex = settings.DirectionTextHeight == .75 ? 1 : 0; existingColor.SelectedIndex = settings.ExistingCableBlack ? 1 : 0;
            width.Value = (decimal)settings.DirectionWidth(settings.DirectionTextHeight, false); existingWidth.Value = (decimal)settings.DirectionWidth(settings.DirectionTextHeight, true);
            mask.Checked = settings.DirectionTextMask; border.Value = (decimal)settings.DirectionTextMaskBorder; drawingColor.Checked = settings.DirectionTextMaskDrawingColor;
            border.Enabled = drawingColor.Enabled = mask.Checked; maskColor.Enabled = mask.Checked && !drawingColor.Checked;
            var rgb = Color.FromArgb(unchecked((int)0xFF000000) | settings.DirectionTextMaskRgb);
            maskRgb.Text = $"Eigen maskerkleur: RGB {rgb.R}, {rgb.G}, {rgb.B}";
        }
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
