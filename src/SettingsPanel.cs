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
        var reset = new Button { Name = "ResetPluginSettings", Text = "Reset plugininstellingen", AutoSize = true };
        reset.Click += (_, _) => { try { colorStatus.Text = Plugin.ResetSettings(); RefreshSettings(); } catch (Exception ex) { colorStatus.Text = "Reset mislukt: " + ex.Message; } };
        layout.Controls.Add(reset);
        layout.Controls.Add(new Label { Text = "Herstelt alle plugininstellingen en Kabelgeul-standaardteksten. Je vorige instellingen worden eerst als kopie bewaard.", AutoSize = true, MaximumSize = new Size(290, 0) });
        layout.Controls.Add(new Label { Text = "Teksthoogte MOF-blocks", AutoSize = true });
        layout.Controls.Add(mof);
        layout.Controls.Add(new Label { Text = "Teksthoogte richtingsteksten (LS/OV)", AutoSize = true, Padding = new Padding(0, 14, 0, 0) });
        layout.Controls.Add(directions);
        layout.Controls.Add(new Label { Text = "Breedte LS/OV bij gekozen teksthoogte", AutoSize = true, Padding = new Padding(0, 8, 0, 0) }); layout.Controls.Add(width);
        layout.Controls.Add(new Label { Text = "Breedte bestaande kabeltekst (was ...)", AutoSize = true, Padding = new Padding(0, 8, 0, 0) }); layout.Controls.Add(existingWidth);
        layout.Controls.Add(new Label { Text = "Breedtes blijven per hoogte bewaard.\n0,45: standaard 4,80 / bestaand 6,80.\n0,75: standaard 7,70 / bestaand 10,70.", AutoSize = true, MaximumSize = new Size(290, 0) });
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
        width.ValueChanged += (_, _) => SaveAppearance(width); existingWidth.ValueChanged += (_, _) => SaveAppearance(existingWidth);
        foreach (var numeric in new[] { width, existingWidth, border }) numeric.TextChanged += (_, _) => SaveTypedNumber(numeric);
        mask.CheckedChanged += (_, _) => SaveAppearance(); border.ValueChanged += (_, _) => SaveAppearance(border); drawingColor.CheckedChanged += (_, _) => SaveAppearance();
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
    private void SaveAppearance(NumericUpDown? changed = null)
    {
        if (refreshing) return;
        var settings = Plugin.Preferences;
        double Value(NumericUpDown field, double fallback) => field == changed ? (double)field.Value : decimal.TryParse(field.Text, out var value) && value >= field.Minimum && value <= field.Maximum ? (double)value : fallback;
        var normal = Value(width, settings.DirectionWidth(settings.DirectionTextHeight, false)); var existing = Value(existingWidth, settings.DirectionWidth(settings.DirectionTextHeight, true));
        if (settings.DirectionTextHeight == .75) { settings.DirectionTextWidth075 = normal; settings.ExistingDirectionTextWidth075 = existing; }
        else { settings.DirectionTextWidth045 = normal; settings.ExistingDirectionTextWidth045 = existing; }
        settings.DirectionTextMask = mask.Checked; settings.DirectionTextMaskBorder = Value(border, settings.DirectionTextMaskBorder); settings.DirectionTextMaskDrawingColor = drawingColor.Checked;
        Plugin.UpdateSettings(); RefreshSettings();
    }
    private void SaveTypedNumber(NumericUpDown numeric)
    {
        if (refreshing || !decimal.TryParse(numeric.Text, out var value) || value < numeric.Minimum || value > numeric.Maximum) return;
        var settings = Plugin.Preferences;
        if (numeric == border) settings.DirectionTextMaskBorder = (double)value;
        else if (numeric == width) { if (settings.DirectionTextHeight == .75) settings.DirectionTextWidth075 = (double)value; else settings.DirectionTextWidth045 = (double)value; }
        else { if (settings.DirectionTextHeight == .75) settings.ExistingDirectionTextWidth075 = (double)value; else settings.ExistingDirectionTextWidth045 = (double)value; }
        try { settings.Save(); }
        catch (Exception ex) { colorStatus.Text = "Instelling kon niet worden opgeslagen: " + ex.Message; }
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
