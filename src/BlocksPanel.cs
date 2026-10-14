using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Techbase.Blocks;

internal sealed class BlocksPanel : UserControl
{
    private readonly CheckBox inherit = new() { Text = "Laag van object overnemen", AutoSize = true };
    private readonly CheckBox chosenLayer = new() { Name = "UseChosenBlockLayer", Text = "Gekozen laag gebruiken", AutoSize = true, Margin = new Padding(0, 6, 8, 3) };
    private readonly ComboBox blockLayer = new() { Name = "ChosenBlockLayer", DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
    private readonly CheckBox repeat = new() { Text = "Herhaald plaatsen (Esc stopt)", AutoSize = true };
    private readonly TextBox search = new() { Dock = DockStyle.Fill, PlaceholderText = "Zoek een block..." };
    private readonly ComboBox groups = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly NumericUpDown tolerance = new() { DecimalPlaces = 3, Minimum = 0.001m, Maximum = 1000m, Increment = 0.01m, Width = 90 };
    private readonly ListView list = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false };
    private readonly Label status = new() { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(6) };
    private readonly Button place = new() { Text = "Plaatsen", Dock = DockStyle.Fill, Height = 34 };
    private readonly Label selectedBlock = new() { Text = "Selecteer een block voor de draai-instelling", AutoSize = true, Dock = DockStyle.Fill };
    private readonly ComboBox rotation = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, Enabled = false };
    private static readonly BlockRotationMode[] rotationModes = { BlockRotationMode.BeforePoint, BlockRotationMode.AfterPoint, BlockRotationMode.None };
    private bool refreshing;
    private ImageList? images;
    internal BlocksPanel()
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 9);
        BackColor = Color.FromArgb(245, 247, 249);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 9, Padding = new Padding(8) };
        for (var i = 0; i < 9; i++) layout.RowStyles.Add(i == 7 ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label { Text = "TECHBASE  /  BLOCKS", Font = new Font(Font, FontStyle.Bold), AutoSize = true, Padding = new Padding(0, 4, 0, 8) }, 0, 0);
        var layerOptions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        layerOptions.Controls.Add(inherit);
        var layerChoice = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        for (var i = 1; i <= 12; i++) blockLayer.Items.Add("Richting " + i);
        blockLayer.Items.Add("Bestaande kabel");
        layerChoice.Controls.Add(chosenLayer); layerChoice.Controls.Add(blockLayer);
        layerOptions.Controls.Add(layerChoice);
        layout.Controls.Add(layerOptions, 0, 1);
        layout.Controls.Add(repeat, 0, 2);
        var settings = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        settings.Controls.Add(new Label { Text = "Zoekafstand:", AutoSize = true, Padding = new Padding(0, 5, 0, 0) });
        settings.Controls.Add(tolerance);
        settings.Controls.Add(new Label { Text = "tekeneenheden", AutoSize = true, Padding = new Padding(0, 5, 0, 0) });
        layout.Controls.Add(settings, 0, 3);
        var sources = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        var source = new Button { Text = "Bron...", AutoSize = true };
        source.Click += (_, _) => SelectSource();
        var reload = new Button { Text = "Vernieuwen", AutoSize = true };
        reload.Click += (_, _) => { try { Plugin.Reload(); } catch (Exception ex) { ShowError(ex); } };
        sources.Controls.Add(source); sources.Controls.Add(reload);
        var rtBack = new Button { Text = "RT-maskers naar achter", AutoSize = true };
        rtBack.Click += (_, _) => AcApp.DocumentManager.MdiActiveDocument?.SendStringToExecute("_TBRTACHTER ", true, false, false);
        sources.Controls.Add(rtBack);
        layout.Controls.Add(sources, 0, 4);
        search.Margin = new Padding(0, 4, 0, 5);
        groups.Margin = new Padding(0, 0, 0, 6);
        layout.Controls.Add(search, 0, 5); layout.Controls.Add(groups, 0, 6);
        list.Columns.Add("Block", 280); list.Columns.Add("Groep", 150);
        list.Resize += (_, _) => list.Columns[0].Width = Math.Max(170, list.ClientSize.Width - 150);
        layout.Controls.Add(list, 0, 7);
        var footer = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 1 };
        rotation.Items.AddRange(new object[] { "Eerst draaien, dan invoegpunt", "Draaien na het invoegpunt", "Niet automatisch draaien" });
        footer.Controls.Add(selectedBlock); footer.Controls.Add(rotation);
        footer.Controls.Add(place); footer.Controls.Add(status);
        layout.Controls.Add(footer, 0, 8); Controls.Add(layout);
        inherit.CheckedChanged += (_, _) => { if (!refreshing) { Plugin.Preferences.InheritLayer = inherit.Checked; Plugin.UpdateSettings(); } };
        chosenLayer.CheckedChanged += (_, _) => { if (!refreshing) { Plugin.Preferences.UseChosenBlockLayer = chosenLayer.Checked; Plugin.UpdateSettings(); RefreshSettings(); } };
        blockLayer.SelectedIndexChanged += (_, _) =>
        {
            if (refreshing || blockLayer.SelectedIndex < 0) return;
            Plugin.Preferences.ChosenBlockLayer = blockLayer.SelectedIndex == 12 ? 0 : blockLayer.SelectedIndex + 1;
            Plugin.UpdateSettings();
            RefreshLayerStatus();
        };
        repeat.CheckedChanged += (_, _) => { if (!refreshing) { Plugin.Preferences.Repeat = repeat.Checked; Plugin.UpdateSettings(); } };
        tolerance.ValueChanged += (_, _) => { if (!refreshing) { Plugin.Preferences.Tolerance = (double)tolerance.Value; Plugin.UpdateSettings(); } };
        search.TextChanged += (_, _) => Filter(); groups.SelectedIndexChanged += (_, _) => Filter();
        list.SelectedIndexChanged += (_, _) => RefreshRotation();
        rotation.SelectedIndexChanged += (_, _) =>
        {
            if (refreshing || list.SelectedItems.Count != 1 || rotation.SelectedIndex < 0) return;
            Plugin.Preferences.SetRotationMode((BlockTool)list.SelectedItems[0].Tag!, rotationModes[rotation.SelectedIndex]);
            Plugin.UpdateSettings();
        };
        place.Click += (_, _) => InsertSelected(); list.DoubleClick += (_, _) => InsertSelected();
        list.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { InsertSelected(); e.Handled = true; } };
        RefreshSettings(); RefreshTools(); UiTypography.Apply(this);
    }
    internal void RefreshSettings()
    {
        refreshing = true;
        inherit.Checked = Plugin.Preferences.InheritLayer;
        chosenLayer.Checked = Plugin.Preferences.UseChosenBlockLayer;
        inherit.Enabled = !chosenLayer.Checked;
        blockLayer.Enabled = chosenLayer.Checked;
        blockLayer.SelectedIndex = Plugin.Preferences.ChosenBlockLayer == 0 ? 12 : Plugin.Preferences.ChosenBlockLayer - 1;
        repeat.Checked = Plugin.Preferences.Repeat;
        tolerance.Value = Math.Clamp((decimal)Plugin.Preferences.Tolerance, tolerance.Minimum, tolerance.Maximum);
        refreshing = false;
        RefreshRotation();
        RefreshLayerStatus();
    }
    private void RefreshRotation()
    {
        var wasRefreshing = refreshing;
        refreshing = true;
        try
        {
            var tool = list.SelectedItems.Count == 1 ? (BlockTool)list.SelectedItems[0].Tag! : null;
            selectedBlock.Text = tool == null ? "Selecteer een block voor de draai-instelling" : "Draaien bij plaatsen: " + tool.Name;
            rotation.Enabled = tool != null;
            rotation.SelectedIndex = tool == null ? -1 : Array.IndexOf(rotationModes, Plugin.Preferences.GetRotationMode(tool));
        }
        finally { refreshing = wasRefreshing; }
    }
    internal void RefreshTools()
    {
        var selectedGroup = groups.SelectedItem as string;
        groups.Items.Clear(); groups.Items.Add("Alle groepen");
        foreach (var group in Plugin.Blocks?.Tools.Select(t => t.Group).Distinct() ?? Array.Empty<string>()) groups.Items.Add(group);
        groups.SelectedItem = groups.Items.Contains(selectedGroup ?? "") ? selectedGroup : "Alle groepen";
        var previous = images;
        images = new ImageList { ImageSize = new Size(40, 40), ColorDepth = ColorDepth.Depth32Bit };
        foreach (var tool in Plugin.Blocks?.Tools ?? Array.Empty<BlockTool>())
        {
            if (tool.IconPath == null) continue;
            try { using var original = Image.FromFile(tool.IconPath); images.Images.Add(tool.Id, new Bitmap(original)); }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is OutOfMemoryException) { }
        }
        list.SmallImageList = images; previous?.Dispose(); Filter();
    }
    private void Filter()
    {
        list.BeginUpdate(); list.Items.Clear();
        var term = search.Text.Trim(); var selectedGroup = groups.SelectedItem as string;
        foreach (var tool in Plugin.Blocks?.Tools ?? Array.Empty<BlockTool>())
        {
            if (selectedGroup != null && selectedGroup != "Alle groepen" && tool.Group != selectedGroup) continue;
            if (term.Length > 0 && !(tool.Name + " " + tool.BlockName + " " + tool.Group).Contains(term, StringComparison.OrdinalIgnoreCase)) continue;
            var item = new ListViewItem(tool.Name) { Tag = tool, ImageKey = tool.Id, ToolTipText = tool.BlockName };
            item.SubItems.Add(tool.Group); list.Items.Add(item);
        }
        list.EndUpdate();
        RefreshRotation();
        RefreshLayerStatus();
    }
    private void RefreshLayerStatus() => status.Text = list.Items.Count + " blocks · dubbelklik om te plaatsen\n" +
        (Plugin.Preferences.UseChosenBlockLayer ? "Gekozen laag: " + BlockLayers.Name(Plugin.Preferences.ChosenBlockLayer) + "\nObject = plaats aanwijzen; gekozen laag blijft gelden" :
            "Tijdens plaatsen: Object = laag en plaats aanwijzen");
    private void InsertSelected()
    {
        if (list.SelectedItems.Count == 0) { status.Text = "Selecteer eerst een block."; return; }
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc == null) { status.Text = "Open eerst een tekening."; return; }
        if (Convert.ToInt32(AcApp.GetSystemVariable("CMDACTIVE")) != 0) { status.Text = "Rond eerst het actieve commando af (of druk Esc)."; return; }
        var tool = (BlockTool)list.SelectedItems[0].Tag!;
        doc.SendStringToExecute("_TBPLAATS " + tool.Id + " ", true, false, false);
    }
    private void SelectSource()
    {
        using var dialog = new OpenFileDialog { Filter = "AutoCAD Toolpalette (*.xtp)|*.xtp", Title = "Kies de Techbase toolpalette", CheckFileExists = true };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        try { Plugin.Reload(dialog.FileName); } catch (Exception ex) { ShowError(ex); }
    }
    private void ShowError(Exception ex) => status.Text = ex.Message;
    protected override void Dispose(bool disposing) { if (disposing) images?.Dispose(); base.Dispose(disposing); }
}
