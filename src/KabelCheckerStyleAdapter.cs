using System.Drawing;

namespace Enexis.KabelChecker.AutoCAD;

internal sealed partial class KabelCheckerForm
{
    private readonly ToolTip compactTips = new() { ShowAlways = true };
    private readonly Font compactFont = new(Techbase.Blocks.Typography.Family, 8.25f);
    private readonly Font compactResultFont = new(Techbase.Blocks.Typography.Family, 10);
    internal string SuggestedExportFileName => Techbase.Blocks.StationExportFileName.Create(_stationName.Text);
    internal event Action? CompactLayoutChanged;
    internal string CompactMetrics()
    {
        var root = Controls.OfType<TableLayoutPanel>().Single();
        var inputs = _currentLoadPanel.Controls.OfType<TableLayoutPanel>().Single();
        return "rows=" + string.Join(",", root.GetRowHeights()) + "; input=" + string.Join(",", inputs.GetRowHeights()) + "; bar=" + inputs.GetControlFromPosition(0, 0)!.Bounds;
    }
    internal int MeasureCompactHeight(int availableHeight = 0)
    {
        var root = Controls.OfType<TableLayoutPanel>().Single(); root.PerformLayout();
        var currentRoot = _currentLoadPanel.Controls.OfType<TableLayoutPanel>().Single(); currentRoot.PerformLayout();
        var inputs = currentRoot.Controls.OfType<TableLayoutPanel>().First(c => c.Controls.OfType<DataGridView>().Any());
        var count = inputs.Controls.OfType<DataGridView>().Single().Rows.Count;
        var toolbar = (FlowLayoutPanel)currentRoot.GetControlFromPosition(0, 0)!;
        var width = Math.Max(100, ClientSize.Width - root.Padding.Horizontal - _currentLoadPanel.Margin.Horizontal - currentRoot.Padding.Horizontal);
        var toolbarHeight = toolbar.GetPreferredSize(new Size(width, 0)).Height;
        root.RowStyles[6].Height = Math.Max(170, toolbarHeight + 136) + 20 * Math.Clamp(count - 2, 0, 1);
        root.PerformLayout();
        var minimum = root.GetRowHeights().Take(9).Sum() + root.Padding.Vertical;
        var extra = Math.Max(0, availableHeight - minimum);
        root.RowStyles[6].Height += extra;
        root.PerformLayout();
        return minimum + extra;
    }
    internal void ApplyCompactStyle()
    {
        IEnumerable<Control> All(Control c) { yield return c; foreach (Control child in c.Controls) foreach (var item in All(child)) yield return item; }
        SuspendLayout();
        foreach (var control in All(this)) control.Font = compactFont;
        _fuseResult.Font = compactResultFont;
        foreach (var button in All(this).OfType<Button>())
        {
            button.MinimumSize = new Size(0, 24); button.Padding = new Padding(4, 1, 4, 1);
            button.Margin = new Padding(1, 1, 1, 1); button.AutoSize = true;
            button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        }
        foreach (var panel in All(this).OfType<FlowLayoutPanel>().Where(p => p.Parent == Controls.OfType<TableLayoutPanel>().Single()))
        {
            panel.Padding = new Padding(4, 0, 4, 0); panel.Margin = new Padding(1);
            panel.BackColor = Color.White;
        }
        _stationName.Width = 175; _savedStations.Width = 175; _savedDirections.Width = 135;
        _stationName.PlaceholderText = "Naam van het station";
        _stationName.AccessibleName = "Stationsnaam";
        _profile.Width = 220; _cablePicker.Width = 175;
        var icons = new Dictionary<string, int>
        {
            ["Station opslaan"] = 6, ["Station laden"] = 7, ["Station verwijderen"] = 8,
            ["Nieuwe richting"] = 9, ["Richting opslaan"] = 6, ["Verwijderen"] = 8,
            ["Excel exporteren"] = 10, ["Alle richtingen verwijderen"] = 11,
            ["Hele polyline"] = 20, ["Deel tot knippunt"] = 21, ["Verwijder kabelrij"] = 8,
            ["Cirkel"] = 22, ["Handmatig"] = 23, ["Uit kader"] = 24, ["Verwijder rij"] = 8
        };
        foreach (var button in All(this).OfType<Techbase.Blocks.ThemedButton>())
            if (icons.TryGetValue(button.Text, out var glyph)) Techbase.Blocks.UiTheme.IconOnly(button, glyph, compactTips, 26);
        var export = All(this).OfType<Button>().Single(b => b.AccessibleName == "Excel exporteren");
        export.BackColor = Color.FromArgb(33, 135, 76); export.ForeColor = Color.White;
        export.FlatAppearance.BorderColor = export.BackColor;
        export.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 153, 88);
        export.FlatAppearance.MouseDownBackColor = Color.FromArgb(26, 112, 62);
        var deleteAll = All(this).OfType<Button>().Single(b => b.AccessibleName == "Alle richtingen verwijderen");
        deleteAll.BackColor = Techbase.Blocks.UiTheme.Danger; deleteAll.ForeColor = Color.White;
        deleteAll.FlatAppearance.BorderColor = deleteAll.BackColor;
        deleteAll.FlatAppearance.MouseOverBackColor = Color.FromArgb(236, 70, 82);
        deleteAll.FlatAppearance.MouseDownBackColor = Color.FromArgb(205, 39, 52);
        _stationName.Width = 130; _savedStations.Width = 130; _savedDirections.Width = 100;
        _profile.Width = 160; _cablePicker.Width = 130;
        var root = Controls.OfType<TableLayoutPanel>().Single();
        root.Padding = new Padding(2); root.ColumnStyles.Clear(); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles[3].SizeType = SizeType.Absolute;
        void FitCableRows() { root.RowStyles[3].Height = 24 + 20 * Math.Clamp(_grid.Rows.Count, 5, 8) + 18; CompactLayoutChanged?.Invoke(); }
        _grid.RowsAdded += (_, _) => FitCableRows(); _grid.RowsRemoved += (_, _) => FitCableRows(); FitCableRows();
        _message.AutoSize = false; _message.AutoEllipsis = true; _message.Height = 16;
        _message.MaximumSize = Size.Empty; _message.Dock = DockStyle.Fill; _message.Padding = Padding.Empty;
        _message.Margin = Padding.Empty; _message.TextAlign = ContentAlignment.MiddleLeft;
        root.RowStyles[4].SizeType = SizeType.Absolute; root.RowStyles[4].Height = 18;
        void UpdateMessageTip() { compactTips.SetToolTip(_message, _message.Text); _message.AccessibleDescription = _message.Text; }
        _message.TextChanged += (_, _) => UpdateMessageTip(); UpdateMessageTip();
        if (root.GetControlFromPosition(0, 8) is Control hint) hint.Visible = false;
        root.RowStyles[8].SizeType = SizeType.Absolute; root.RowStyles[8].Height = 0;
        foreach (var grid in All(this).OfType<DataGridView>())
        {
            foreach (var style in new[] { grid.DefaultCellStyle, grid.RowsDefaultCellStyle, grid.AlternatingRowsDefaultCellStyle, grid.ColumnHeadersDefaultCellStyle, grid.RowHeadersDefaultCellStyle }) style.Font = compactFont;
            grid.RowTemplate.Height = 20;
            foreach (DataGridViewRow row in grid.Rows) row.Height = 20;
            grid.ColumnHeadersHeight = 24;
        }
        CompactCurrentInputs();
        root.RowStyles[6].SizeType = SizeType.Absolute; root.RowStyles[6].Height = 190;
        root.RowCount = 10; root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        foreach (var input in All(this).Where(c => c is ComboBox or TextBox or NumericUpDown))
        { input.Margin = new Padding(1, 2, 4, 2); input.Anchor = AnchorStyles.Top | AnchorStyles.Left; }
        var topBars = Enumerable.Range(0, 3).Select(i => (FlowLayoutPanel)root.GetControlFromPosition(0, i)!).ToArray();
        foreach (var bar in topBars)
        {
            foreach (Control item in bar.Controls)
            {
                // FlowLayoutPanel centres controls within each flow row when
                // neither Top nor Bottom is anchored, including wrapped rows.
                item.Anchor = AnchorStyles.Left;
                item.Margin = new Padding(1, 1, 4, 1);
                if (item is not Label caption) continue;
                caption.AutoSize = false; caption.Padding = Padding.Empty;
                caption.Height = 26; caption.TextAlign = ContentAlignment.MiddleLeft;
                void FitCaption() => caption.Width = TextRenderer.MeasureText(caption.Text, caption.Font).Width;
                FitCaption(); caption.TextChanged += (_, _) => FitCaption(); caption.FontChanged += (_, _) => FitCaption();
            }
        }
        foreach (var label in All(this).OfType<Label>().Where(l => l.Parent is FlowLayoutPanel flow && flow.FlowDirection == FlowDirection.LeftToRight && l.Text.TrimEnd().EndsWith(':') && !topBars.Any(p => p == l.Parent)))
        {
            var siblings = label.Parent!.Controls.Cast<Control>().ToArray();
            var input = siblings.Skip(Array.IndexOf(siblings, label) + 1).FirstOrDefault(c => c is TextBox or ComboBox or NumericUpDown);
            if (input == null) continue;
            void AlignLabel()
            {
                label.AutoSize = false; label.Padding = Padding.Empty;
                label.Size = new Size(TextRenderer.MeasureText(label.Text, label.Font).Width, input.Height);
                label.Margin = new Padding(1, input.Margin.Top, 4, input.Margin.Bottom);
                label.Anchor = AnchorStyles.Top | AnchorStyles.Left; label.TextAlign = ContentAlignment.MiddleLeft;
            }
            AlignLabel(); input.SizeChanged += (_, _) => AlignLabel(); label.FontChanged += (_, _) => AlignLabel();
        }
        // A result label is not an input: remove upstream padding and share its
        // text baseline explicitly, while preserving the larger result font.
        var fuseCaption = _fuseResult.Parent!.Controls.OfType<Label>().Single(l => l != _fuseResult);
        void AlignFuse()
        {
            fuseCaption.Padding = _fuseResult.Padding = Padding.Empty;
            var height = Math.Max(fuseCaption.PreferredHeight, _fuseResult.PreferredHeight);
            foreach (var item in new[] { fuseCaption, _fuseResult })
            {
                item.AutoSize = false; item.Padding = Padding.Empty;
                item.Size = new Size(TextRenderer.MeasureText(item.Text, item.Font).Width, height);
                item.Margin = new Padding(1, 1, 4, 1); item.TextAlign = ContentAlignment.BottomLeft;
            }
        }
        AlignFuse(); _fuseResult.TextChanged += (_, _) => AlignFuse();
        var loadGrid = All(_currentLoadPanel).OfType<DataGridView>().Single();
        void FitLoadRows() { root.RowStyles[6].Height = 190 + 20 * Math.Clamp(loadGrid.Rows.Count - 2, 0, 1); CompactLayoutChanged?.Invoke(); }
        loadGrid.RowsAdded += (_, _) => FitLoadRows(); loadGrid.RowsRemoved += (_, _) => FitLoadRows(); FitLoadRows();
        Disposed += (_, _) => { compactTips.Dispose(); compactFont.Dispose(); compactResultFont.Dispose(); };
        ResumeLayout(true);
    }
    internal Control TakeActionBar()
    {
        var root = Controls.OfType<TableLayoutPanel>().Single();
        var bar = root.GetControlFromPosition(0, 7) ?? throw new InvalidOperationException("Actiebalk ontbreekt.");
        root.Controls.Remove(bar); root.RowStyles[7].SizeType = SizeType.Absolute; root.RowStyles[7].Height = 0;
        return bar;
    }
    private void CompactCurrentInputs()
    {
        var root = _currentLoadPanel.Controls.OfType<TableLayoutPanel>().Single();
        var actions = (FlowLayoutPanel)root.GetControlFromPosition(0, 0)!;
        var overview = root.GetControlFromPosition(1, 0)!;
        var status = (TableLayoutPanel)root.GetControlFromPosition(2, 0)!;
        root.SuspendLayout();
        var groups = actions.Controls.OfType<FlowLayoutPanel>().ToArray();
        var instructions = actions.Controls.OfType<Label>().FirstOrDefault(l => l.Text.Length > 100);
        if (instructions != null) { instructions.Visible = false; compactTips.SetToolTip(actions, instructions.Text); }
        var label = actions.Controls.OfType<Label>().FirstOrDefault(l => l.Text == "Ontwerpstroom");
        if (label != null)
        {
            groups[2].Controls.Add(label); groups[2].Controls.SetChildIndex(label, 0);
            var button = groups[2].Controls.OfType<Button>().First();
            label.AutoSize = false; label.Padding = Padding.Empty;
            label.Size = new Size(TextRenderer.MeasureText(label.Text, label.Font).Width, button.Height);
            label.Margin = new Padding(1, button.Margin.Top, 4, button.Margin.Bottom);
            label.TextAlign = ContentAlignment.MiddleLeft;
        }
        groups[0].FlowDirection = FlowDirection.LeftToRight;
        foreach (var group in groups) { group.AutoSizeMode = AutoSizeMode.GrowAndShrink; group.Margin = new Padding(0, 0, 5, 0); group.Padding = Padding.Empty; }
        foreach (var combo in actions.Controls.Cast<Control>().SelectMany(c => c.Controls.Cast<Control>()).OfType<ComboBox>())
        { combo.MaximumSize = new Size(combo.Width > 150 ? 170 : 110, 0); combo.Margin = new Padding(0, 2, 6, 2); }
        actions.Controls.SetChildIndex(groups[3], 2);
        actions.FlowDirection = FlowDirection.LeftToRight; actions.WrapContents = true;
        actions.AutoScroll = false; actions.AutoSize = true; actions.Margin = Padding.Empty;
        root.Controls.Remove(overview); root.Controls.Remove(status);
        root.RowCount = 3; root.RowStyles.Clear(); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        root.SetColumnSpan(actions, 3);
        root.Controls.Add(overview, 0, 1); root.SetColumnSpan(overview, 3);
        root.Controls.Add(status, 0, 2); root.SetColumnSpan(status, 3);
        _currentLoadPanel.CompactResults((TableLayoutPanel)overview, status, compactTips);
        root.Padding = new Padding(2);
        root.ResumeLayout(true);
    }
}
