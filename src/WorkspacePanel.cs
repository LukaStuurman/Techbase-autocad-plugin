using System.Drawing;
using System.Reflection;

namespace Techbase.Blocks;

internal sealed class WorkspacePanel : UserControl
{
    private readonly Control[] pages;
    private readonly Button[] navigation = new Button[7];
    private readonly Panel content = new() { Dock = DockStyle.Fill, Padding = new Padding(4, 0, 4, 0) };
    private readonly TableLayoutPanel navigationLayout;
    private readonly TableLayoutPanel compactNavigation;
    private readonly ComboBox pagePicker = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Dock = DockStyle.Fill, AccessibleName = "Kies Techbase-onderdeel"
    };
    private readonly Panel navigationHost = new() { Dock = DockStyle.Fill, Margin = Padding.Empty };
    private readonly int[] order = { 0, 1, 6, 2, 3, 5, 4 };
    private bool updatingPicker;
    private int displayedColumns = -1;
    internal int SelectedIndex { get; private set; } = -1;
    internal WorkspacePanel(params Control[] pages)
    {
        if (pages.Length != 7) throw new ArgumentException("Zeven werkbladen vereist.", nameof(pages));
        this.pages = pages;
        Dock = DockStyle.Fill; BackColor = UiTheme.Canvas;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        var brand = new BrandHeader { Dock = DockStyle.Fill, Margin = Padding.Empty };
        navigationLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 7, RowCount = 1,
            Padding = new Padding(9, 4, 9, 4), Margin = Padding.Empty
        };
        for (var i = 0; i < 7; i++)
            navigationLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 7));
        navigationLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var labels = new[] { "Blocks", "Beheerkaart", "Kabelchecker", "Richtingen", "Instellingen", "Kabelgeul", "Stationslocaties" };
        foreach (var index in order)
        {
            var button = new ThemedButton
            {
                Text = labels[index], Glyph = index, Dock = DockStyle.Fill,
                Margin = new Padding(2, 2, 2, 2),
                AccessibleName = "Open " + labels[index], TabIndex = index
            };
            button.Click += (_, _) => Navigate(index);
            navigation[index] = button;
            navigationLayout.Controls.Add(button);
        }
        compactNavigation = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
            Padding = new Padding(13, 6, 13, 6), Margin = Padding.Empty
        };
        compactNavigation.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));
        compactNavigation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        compactNavigation.Controls.Add(new Label
        {
            Text = "Onderdeel", Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = UiTheme.Muted, UseMnemonic = false
        }, 0, 0);
        pagePicker.Items.AddRange(labels);
        pagePicker.SelectedIndexChanged += (_, _) =>
        {
            if (!updatingPicker && pagePicker.SelectedIndex >= 0)
                Navigate(pagePicker.SelectedIndex);
        };
        compactNavigation.Controls.Add(pagePicker, 1, 0);
        navigationHost.Controls.Add(navigationLayout);
        navigationHost.Controls.Add(compactNavigation);
        var footer = new Label { Text = "TECHBASE   /   Van visie tot revisie", AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(14, 0, 0, 0), Margin = Padding.Empty, Font = new Font(Typography.Family, 8) };
        layout.Controls.Add(brand, 0, 0); layout.Controls.Add(navigationHost, 0, 1); layout.Controls.Add(content, 0, 2); layout.Controls.Add(footer, 0, 3);
        // Alleen bij voldoende breedte worden losse tabknoppen getoond.
        // In een gedockte, smalle palette neemt de keuzelijst slechts één rij in.
        SizeChanged += (_, _) => UpdateNavigationLayout(layout);
        Controls.Add(layout);
        foreach (var page in pages) { page.Dock = DockStyle.Fill; page.Visible = false; content.Controls.Add(page); }
        UiTypography.Apply(this); UiTheme.Apply(this);
        brand.BackColor = Color.White;
        footer.ForeColor = UiTheme.Muted;
        compactNavigation.BackColor = Color.White;
        UpdateNavigationLayout(layout);
        Navigate(0);
    }

    private void UpdateNavigationLayout(TableLayoutPanel layout)
    {
        var scale = DeviceDpi / 96f;
        var compact = ClientSize.Width < 680 * scale;
        if (compact)
        {
            navigationLayout.Visible = false;
            compactNavigation.Visible = true;
            compactNavigation.BringToFront();
            layout.RowStyles[1].Height = 48 * scale;
            return;
        }

        var columns = ClientSize.Width >= 1110 * scale ? 7 : 4;
        var rows = (navigation.Length + columns - 1) / columns;
        compactNavigation.Visible = false;
        navigationLayout.Visible = true;
        if (columns != displayedColumns)
        {
            navigationLayout.SuspendLayout();
            navigationLayout.ColumnCount = columns;
            navigationLayout.RowCount = rows;
            navigationLayout.ColumnStyles.Clear();
            navigationLayout.RowStyles.Clear();
            for (var i = 0; i < columns; i++)
                navigationLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
            for (var i = 0; i < rows; i++)
                navigationLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows));
            for (var i = 0; i < order.Length; i++)
                navigationLayout.SetCellPosition(navigation[order[i]],
                    new TableLayoutPanelCellPosition(i % columns, i / columns));
            navigationLayout.ResumeLayout(true);
            displayedColumns = columns;
        }
        navigationLayout.BringToFront();
        layout.RowStyles[1].Height = (rows * 44 + 8) * scale;
    }

    internal void Navigate(int index)
    {
        if (index < 0 || index >= pages.Length) throw new ArgumentOutOfRangeException(nameof(index));
        content.SuspendLayout();
        for (var i = 0; i < pages.Length; i++)
        {
            pages[i].Visible = i == index;
            navigation[i].BackColor = i == index ? UiTheme.Orange : Color.White;
            navigation[i].FlatAppearance.BorderColor = i == index ? UiTheme.Orange : UiTheme.Line;
            navigation[i].AccessibleDescription = i == index ? "Huidig onderdeel" : "Open " + navigation[i].Text;
            navigation[i].Invalidate();
        }
        pages[index].BringToFront(); SelectedIndex = index;
        updatingPicker = true;
        try { if (pagePicker.SelectedIndex != index) pagePicker.SelectedIndex = index; }
        finally { updatingPicker = false; }
        content.ResumeLayout(true);
    }
    private sealed class BrandHeader : Control
    {
        private readonly Image? logo;
        internal BrandHeader()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            using var stream = typeof(Plugin).Assembly.GetManifestResourceStream("Techbase.Logo");
            if (stream != null) { using var source = Image.FromStream(stream); logo = new Bitmap(source); }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Color.White);
            var scale = DeviceDpi / 96f;
            if (logo != null) e.Graphics.DrawImage(logo, new Rectangle(14, 12, (int)(157 * scale), (int)(157 * scale * logo.Height / logo.Width)));
            else TextRenderer.DrawText(e.Graphics, "techbase", Font, new Point(14, 14), UiTheme.Ink);
            if (Width > 320 * scale)
                TextRenderer.DrawText(e.Graphics, "CAD WERKPLAATS", Font, new Rectangle(Width - (int)(140 * scale), 17, (int)(126 * scale), 30), UiTheme.Muted, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            using var pen = new Pen(UiTheme.Orange, 3 * scale);
            e.Graphics.DrawLine(pen, 0, Height - 2, Width, Height - 2);
        }
        protected override void Dispose(bool disposing) { if (disposing) logo?.Dispose(); base.Dispose(disposing); }
    }
}
