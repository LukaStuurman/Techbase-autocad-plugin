using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Techbase.Blocks;

internal sealed class BlocksPanel : UserControl
{
    private readonly ThemedToggle inherit = new() { Text = "Laag van object overnemen", AutoSize = true };
    private readonly ThemedToggle chosenLayer = new() { Name = "UseChosenBlockLayer", Text = "Gekozen laag gebruiken", AutoSize = true, Margin = new Padding(0, 6, 8, 3) };
    private readonly ComboBox blockLayer = new() { Name = "ChosenBlockLayer", DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
    private readonly ThemedToggle repeat = new() { Text = "Herhaald plaatsen (Esc stopt)", AutoSize = true };
    private readonly TextBox search = new() { Dock = DockStyle.Fill, PlaceholderText = "Zoek een block..." };
    private readonly ComboBox groups = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly NumericUpDown tolerance = new() { DecimalPlaces = 3, Minimum = 0.001m, Maximum = 1000m, Increment = 0.01m, Width = 90 };
    private readonly ListView list = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false };
    private readonly Label status = new() { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(6) };
    private readonly ThemedButton place = new() { Text = "Plaatsen", Dock = DockStyle.Fill, Height = 34 };
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
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, Padding = new Padding(12) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 7; i++) layout.RowStyles.Add(i == 5 ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.AutoSize));
        var heading = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
        heading.Controls.Add(UiTheme.Title("Blockbibliotheek"));
        var openTemplate = new ThemedButton { Name = "OpenBlockTemplate", Text = "Template openen", AutoSize = true };
        openTemplate.Click += (_, _) => AcApp.DocumentManager.MdiActiveDocument?.SendStringToExecute("_TBTEMPLATE ", true, false, false);
        heading.Controls.Add(openTemplate); layout.Controls.Add(heading, 0, 0);
        // Eén vast kolomraster voor alle drie schakelaars. De oude geneste
        // FlowLayoutPanels lieten vooral "Gekozen laag gebruiken" inspringen
        // of verspringen wanneer de werkplaats smaller werd.
        var placementOptions = new TableLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top, ColumnCount = 1, RowCount = 5,
            Padding = new Padding(3, 5, 3, 3), Margin = Padding.Empty
        };
        placementOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 5; row++)
            placementOptions.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        inherit.Dock = DockStyle.Top;
        chosenLayer.Dock = DockStyle.Top;
        repeat.Dock = DockStyle.Top;
        // De drie schakelaars hebben dezelfde container, X-positie en marge.
        foreach (var toggle in new[] { inherit, chosenLayer, repeat })
            toggle.Margin = new Padding(0, 3, 0, 3);
        placementOptions.Controls.Add(inherit, 0, 0);
        placementOptions.Controls.Add(chosenLayer, 0, 1);

        for (var i = 1; i <= 12; i++) blockLayer.Items.Add("Richting " + i);
        blockLayer.Items.Add("Bestaande kabel");
        // De combobox hoort bij de tweede schakelaar en staat eronder,
        // ingesprongen op de tekstpositie, zonder de schakelaar te verplaatsen.
        blockLayer.Dock = DockStyle.Top;
        blockLayer.Margin = new Padding(45, 0, 2, 8);
        placementOptions.Controls.Add(blockLayer, 0, 2);
        placementOptions.Controls.Add(repeat, 0, 3);

        var settings = new FlowLayoutPanel
        {
            AutoSize = true, Dock = DockStyle.Fill,
            WrapContents = true, Margin = new Padding(0, 7, 0, 0)
        };
        settings.Controls.Add(new Label { Text = "Zoekafstand:", AutoSize = true, Padding = new Padding(0, 5, 0, 0) });
        settings.Controls.Add(tolerance);
        settings.Controls.Add(new Label { Text = "tekeneenheden", AutoSize = true, Padding = new Padding(0, 5, 0, 0) });
        placementOptions.Controls.Add(settings, 0, 4);
        layout.Controls.Add(UiTheme.Fold("Plaatsopties", placementOptions), 0, 3);
        var sources = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        var source = new ThemedButton { Text = "Bron...", AutoSize = true };
        source.Click += (_, _) => SelectSource();
        var reload = new ThemedButton { Text = "Vernieuwen", AutoSize = true };
        reload.Click += (_, _) => { try { Plugin.Reload(); } catch (Exception ex) { ShowError(ex); } };
        sources.Controls.Add(source); sources.Controls.Add(reload);
        var rtBack = new ThemedButton { Text = "RT-maskers naar achter", AutoSize = true };
        rtBack.Click += (_, _) => AcApp.DocumentManager.MdiActiveDocument?.SendStringToExecute("_TBRTACHTER ", true, false, false);
        sources.Controls.Add(rtBack);
        var textLayer = new ThemedButton { Text = "Blocktekst laag herstellen", AutoSize = true };
        textLayer.Click += (_, _) => AcApp.DocumentManager.MdiActiveDocument?.SendStringToExecute("_TBBLOCKTEKSTLAAG ", true, false, false);
        sources.Controls.Add(textLayer);
        var rackDirections = new ThemedButton { Text = "LS-rek richtingen", AutoSize = true };
        rackDirections.Click += (_, _) => AcApp.DocumentManager.MdiActiveDocument?.SendStringToExecute("_TBLSREK ", true, false, false);
        sources.Controls.Add(rackDirections);
        var ovDirections = new ThemedButton { Text = "OV-rek richtingen", AutoSize = true };
        ovDirections.Click += (_, _) => AcApp.DocumentManager.MdiActiveDocument?.SendStringToExecute("_TBOVREK ", true, false, false);
        sources.Controls.Add(ovDirections);
        var stationLocations = new ThemedButton { Text = "Stationlocaties", AutoSize = true };
        stationLocations.Click += (_, _) => AcApp.DocumentManager.MdiActiveDocument?.SendStringToExecute("_TBSTATIONLOCATIES ", true, false, false);
        sources.Controls.Add(stationLocations);
        layout.Controls.Add(UiTheme.Fold("Bibliotheek & onderhoud", sources), 0, 4);
        search.Margin = new Padding(0, 4, 0, 5);
        groups.Margin = new Padding(0, 0, 0, 6);
        layout.Controls.Add(search, 0, 1); layout.Controls.Add(groups, 0, 2);
        list.Columns.Add("Block", 280); list.HeaderStyle = ColumnHeaderStyle.None; list.OwnerDraw = true;
        list.Resize += (_, _) => list.Columns[0].Width = Math.Max(100, list.ClientSize.Width - 2);
        list.DrawColumnHeader += (_, args) => args.DrawDefault = true;
        list.DrawSubItem += (_, args) =>
        {
            var item = args.Item;
            if (item == null || args.ColumnIndex != 0) return;
            var selected = item.Selected;
            using var background = new SolidBrush(selected ? UiTheme.SoftOrange : Color.White);
            args.Graphics.FillRectangle(background, args.Bounds);
            if (selected) { using var accent = new SolidBrush(UiTheme.Orange); args.Graphics.FillRectangle(accent, args.Bounds.Left, args.Bounds.Top, 3, args.Bounds.Height); }
            if (images != null && images.Images.ContainsKey(item.ImageKey))
                args.Graphics.DrawImage(images.Images[item.ImageKey]!, new Rectangle(args.Bounds.Left + 8, args.Bounds.Top + 4, 32, 32));
            var text = new Rectangle(args.Bounds.Left + 50, args.Bounds.Top + 4, Math.Max(1, args.Bounds.Width - 58), 18);
            TextRenderer.DrawText(args.Graphics, item.Text, list.Font, text, UiTheme.Ink, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            if (item.SubItems.Count > 1)
            {
                text.Y += 17;
                TextRenderer.DrawText(args.Graphics, item.SubItems[1].Text, list.Font, text, UiTheme.Muted, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
            if (selected && list.Focused) ControlPaint.DrawFocusRectangle(args.Graphics, Rectangle.Inflate(args.Bounds, -4, -2));
        };
        layout.Controls.Add(list, 0, 5);
        var footer = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 1 };
        rotation.Items.AddRange(new object[] { "Eerst draaien, dan invoegpunt", "Draaien na het invoegpunt", "Niet automatisch draaien" });
        footer.Controls.Add(selectedBlock); footer.Controls.Add(rotation);
        footer.Controls.Add(place); footer.Controls.Add(status);
        layout.Controls.Add(footer, 0, 6); Controls.Add(layout);
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
