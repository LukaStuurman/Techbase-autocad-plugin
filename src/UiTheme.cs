using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;

namespace Techbase.Blocks;

internal static class UiTheme
{
    internal static readonly Color Ink = Color.FromArgb(35, 39, 44);
    internal static readonly Color Muted = Color.FromArgb(99, 108, 117);
    internal static readonly Color Canvas = Color.FromArgb(243, 245, 247);
    internal static readonly Color Line = Color.FromArgb(218, 224, 229);
    internal static readonly Color Orange = Color.FromArgb(252, 140, 37);
    internal static readonly Color SoftOrange = Color.FromArgb(255, 239, 222);
    internal static readonly Color Input = Color.FromArgb(238, 241, 244);
    internal static readonly Color Danger = Color.FromArgb(222, 49, 63);
    private static readonly ConditionalWeakTable<Control, object> Styled = new();
    internal static void Primary(Button button)
    {
        button.BackColor = Orange; button.ForeColor = Ink;
        button.FlatAppearance.BorderColor = Orange;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 157, 64);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(242, 127, 24);
        button.AccessibleDescription = "Primaire actie: " + (button.Text.Length > 0 ? button.Text : button.AccessibleName);
    }
    internal static void IconOnly(ThemedButton button, int glyph, ToolTip tips, int size = 30)
    {
        var label = button.Text.Length > 0 ? button.Text : button.AccessibleName ?? "Actie";
        button.AccessibleName = label; button.AccessibleDescription = label;
        tips.SetToolTip(button, label); button.Text = ""; button.Glyph = glyph;
        button.AutoSize = false; button.Dock = DockStyle.None;
        button.MinimumSize = new Size(size, Math.Min(size, 28)); button.MaximumSize = new Size(size, size);
        button.Size = new Size(size, size); button.Invalidate();
    }
    internal static Label Title(string text) => new()
    {
        Text = text, AutoSize = true, UseMnemonic = false, Font = new Font(Typography.Family, 17),
        Margin = new Padding(0, 4, 0, 14), ForeColor = Ink
    };
    internal static Label Hint(string text) => new() { Text = text, AutoSize = true, ForeColor = Muted, MaximumSize = new Size(320, 0), Margin = new Padding(0, 4, 0, 10), UseMnemonic = false };
    internal static Label Field(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(0, 8, 0, 2), UseMnemonic = false };
    internal static UiSection Section(string title, params Control[] controls)
    {
        var section = new UiSection();
        section.Controls.Add(new Label { Text = title, UseMnemonic = false, AutoSize = true, Font = new Font(Typography.Family, 12), Margin = new Padding(0, 0, 0, 8) });
        foreach (var control in controls) { control.Dock = DockStyle.Top; section.Controls.Add(control); }
        return section;
    }
    internal static void Apply(Control root)
    {
        if (Styled.TryGetValue(root, out _)) return;
        Styled.Add(root, new object());
        if (SystemInformation.HighContrast) return;
        root.ForeColor = Ink;
        switch (root)
        {
            case Button button:
                button.FlatStyle = FlatStyle.Flat; button.UseVisualStyleBackColor = false;
                button.BackColor = Color.White; button.FlatAppearance.BorderColor = Line;
                button.FlatAppearance.MouseOverBackColor = SoftOrange;
                button.FlatAppearance.MouseDownBackColor = Color.FromArgb(255, 218, 181);
                button.Cursor = Cursors.Hand;
                button.MinimumSize = new Size(0, 32);
                button.Padding = new Padding(8, 2, 8, 2);
                button.Margin = new Padding(3, 4, 3, 4);
                if (button.Text is "Plaatsen" or "Toevoegingen uitvoeren" or "Nieuwe richting tekenen" or "Kabelgeul plaatsen" or "Bereken richting") Primary(button);
                break;
            case TextBox text:
                text.BackColor = Input; text.BorderStyle = BorderStyle.None;
                text.Margin = new Padding(0, 4, 0, 8);
                break;
            case ComboBox combo:
                combo.BackColor = Input; combo.FlatStyle = FlatStyle.Flat;
                combo.Margin = new Padding(0, 4, 0, 8);
                break;
            case NumericUpDown number:
                number.BackColor = Input; number.BorderStyle = BorderStyle.None;
                break;
            case CheckedListBox checks:
                checks.BackColor = Color.White; checks.BorderStyle = BorderStyle.None;
                checks.IntegralHeight = false;
                break;
            case ListView list:
                list.BackColor = Color.White; list.BorderStyle = BorderStyle.None;
                list.GridLines = false; list.ShowItemToolTips = true;
                break;
            case DataGridView grid:
                grid.BackgroundColor = Color.White; grid.BorderStyle = BorderStyle.None;
                grid.GridColor = Line; grid.EnableHeadersVisualStyles = false;
                grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
                grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
                grid.ColumnHeadersDefaultCellStyle.BackColor = Input;
                grid.ColumnHeadersDefaultCellStyle.ForeColor = Ink;
                grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Input;
                grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Ink;
                grid.DefaultCellStyle.BackColor = Color.White;
                grid.DefaultCellStyle.ForeColor = Ink;
                grid.DefaultCellStyle.SelectionBackColor = SoftOrange;
                grid.DefaultCellStyle.SelectionForeColor = Ink;
                grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 250);
                grid.RowHeadersDefaultCellStyle.BackColor = Canvas;
                grid.RowHeadersDefaultCellStyle.ForeColor = Ink;
                grid.RowHeadersDefaultCellStyle.SelectionBackColor = SoftOrange;
                grid.RowHeadersDefaultCellStyle.SelectionForeColor = Ink;
                grid.RowTemplate.Height = Math.Max(30, grid.RowTemplate.Height);
                grid.ColumnHeadersHeight = Math.Max(34, grid.ColumnHeadersHeight);
                break;
            case CheckBox check:
                check.Margin = new Padding(0, 6, 8, 6); check.Cursor = Cursors.Hand;
                break;
            case RadioButton radio:
                radio.Margin = new Padding(3, 6, 3, 6); radio.Cursor = Cursors.Hand;
                if (radio.Appearance == Appearance.Button)
                {
                    void UpdateChoice()
                    {
                        radio.UseVisualStyleBackColor = false;
                        radio.BackColor = radio.Checked ? SoftOrange : Color.White;
                        radio.FlatAppearance.BorderColor = radio.Checked ? Orange : Line;
                    }
                    UpdateChoice(); radio.CheckedChanged += (_, _) => UpdateChoice();
                }
                break;
            case Label label:
                if (label.Text.Length > 100) label.ForeColor = Muted;
                break;
            case Panel or UserControl or Form or GroupBox:
                var card = root is UiSection;
                for (var parent = root.Parent; !card && parent != null; parent = parent.Parent) card = parent is UiSection;
                root.BackColor = card ? Color.White : Canvas;
                void Frame(object? sender, PaintEventArgs args)
                { using var border = new Pen(Line); args.Graphics.DrawRectangle(border, 0, 0, Math.Max(0, root.Width - 1), Math.Max(0, root.Height - 1)); }
                if (root is Panel panel && panel.BorderStyle != BorderStyle.None) { panel.BorderStyle = BorderStyle.None; panel.Paint += Frame; }
                if (root is UserControl user && user.BorderStyle != BorderStyle.None) { user.BorderStyle = BorderStyle.None; user.Paint += Frame; }
                break;
        }
        foreach (Control child in root.Controls) Apply(child);
        root.ControlAdded += (_, args) => { if (args.Control != null) Apply(args.Control); };
    }
    internal static Control Fold(string title, Control content, bool expanded = false)
    {
        var panel = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 1, Margin = new Padding(0, 4, 0, 6) };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var toggle = new ThemedButton { Text = (expanded ? "−  " : "+  ") + title, AutoSize = true, Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleLeft, AccessibleName = title };
        content.Visible = expanded; content.Dock = DockStyle.Top;
        toggle.Click += (_, _) =>
        {
            expanded = !expanded; content.Visible = expanded;
            toggle.Text = (expanded ? "−  " : "+  ") + title;
        };
        panel.Controls.Add(toggle, 0, 0); panel.Controls.Add(content, 0, 1);
        return panel;
    }
}

internal sealed class UiSection : TableLayoutPanel
{
    internal UiSection()
    {
        ColumnCount = 1; AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Dock = DockStyle.Top; Padding = new Padding(12); Margin = new Padding(0, 0, 0, 12);
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        SizeChanged += (_, _) =>
        {
            foreach (Control child in Controls)
                if (child is Label or CheckBox) child.MaximumSize = new Size(Math.Max(120, ClientSize.Width - Padding.Horizontal - child.Margin.Horizontal), 0);
        };
        Paint += (_, args) => { using var pen = new Pen(UiTheme.Line); args.Graphics.DrawRectangle(pen, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1)); };
    }
}

internal sealed class ThemedButton : Button
{
    [System.ComponentModel.DefaultValue(-1)]
    internal int Glyph { get; set; } = -1;
    private bool hovered;
    private bool pressed;
    internal ThemedButton()
    {
        UseMnemonic = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
    }
    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        if (SystemInformation.HighContrast) { base.OnPaint(e); return; }
        e.Graphics.Clear(Parent?.BackColor ?? UiTheme.Canvas);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new RectangleF(.5f, .5f, Math.Max(1, Width - 1.5f), Math.Max(1, Height - 1.5f));
        var primary = BackColor == UiTheme.Orange;
        var fill = !Enabled ? UiTheme.Canvas : pressed ? FlatAppearance.MouseDownBackColor : hovered ? FlatAppearance.MouseOverBackColor : BackColor;
        // Subtiele afgeronde hoeken; één consistente vorm voor navigatie én acties.
        var radius = Math.Min(9f * DeviceDpi / 96f, Math.Min(bounds.Width, bounds.Height) / 2f);
        var diameter = radius * 2;
        using var outline = new GraphicsPath();
        outline.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        outline.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        outline.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        outline.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        outline.CloseFigure();
        using var brush = new SolidBrush(fill);
        e.Graphics.FillPath(brush, outline);
        using var pen = new Pen(primary ? UiTheme.Orange : FlatAppearance.BorderColor);
        e.Graphics.DrawPath(pen, outline);
        var textBounds = Rectangle.Inflate(ClientRectangle, -8, -3);
        if (Glyph >= 0)
        {
            var logical = Glyph is >= 14 and <= 17 ? new SizeF(20, 22) : new SizeF(16, 16);
            var scale = Math.Min(DeviceDpi / 96f, Math.Min(Math.Max(1, Width - 10) / logical.Width, Math.Max(1, Height - 8) / logical.Height));
            var iconWidth = logical.Width * scale; var iconHeight = logical.Height * scale;
            PaintGlyph(e.Graphics, Glyph, new RectangleF(Text.Length == 0 ? (Width - iconWidth) / 2 : 11, (Height - iconHeight) / 2, iconWidth, iconHeight), Enabled ? ForeColor : SystemColors.GrayText);
            textBounds.X += 23; textBounds.Width -= 23;
        }
        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | (UseMnemonic ? 0 : TextFormatFlags.NoPrefix);
        flags |= Glyph >= 0 || TextAlign is ContentAlignment.MiddleLeft or ContentAlignment.TopLeft or ContentAlignment.BottomLeft ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter;
        TextRenderer.DrawText(e.Graphics, Text, Font, textBounds, Enabled ? ForeColor : SystemColors.GrayText, flags);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -4, -4), ForeColor, fill);
    }
    internal static void PaintGlyph(Graphics graphics, int glyph, RectangleF bounds, Color color)
    {
        var state = graphics.Save(); graphics.TranslateTransform(bounds.X, bounds.Y);
        var labelIcon = glyph is >= 14 and <= 17;
        graphics.ScaleTransform(bounds.Width / (labelIcon ? 20 : 16), bounds.Height / (labelIcon ? 22 : 16));
        using var pen = new Pen(color, 1.3f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
        switch (glyph)
        {
            case 0:
                graphics.DrawPolygon(pen, new[] { new PointF(1, 4), new PointF(7, 1), new PointF(13, 4), new PointF(13, 11), new PointF(7, 14), new PointF(1, 11) });
                graphics.DrawLines(pen, new[] { new PointF(1, 4), new PointF(7, 7), new PointF(13, 4) }); graphics.DrawLine(pen, 7, 7, 7, 14); break;
            case 1:
                graphics.DrawLine(pen, 6, 14, 6, 3); graphics.DrawArc(pen, 6, 1, 7, 5, 180, 180); graphics.DrawLine(pen, 3, 14, 10, 14); graphics.DrawLine(pen, 11, 7, 13, 7); break;
            case 2:
                graphics.DrawLines(pen, new[] { new PointF(8, 0), new PointF(2, 8), new PointF(7, 8), new PointF(5, 15), new PointF(13, 5), new PointF(8, 5) }); break;
            case 3:
                graphics.DrawLines(pen, new[] { new PointF(1, 13), new PointF(6, 8), new PointF(10, 8), new PointF(14, 2) }); graphics.DrawLines(pen, new[] { new PointF(9, 2), new PointF(14, 2), new PointF(14, 7) }); break;
            case 4:
                graphics.DrawLine(pen, 1, 3, 14, 3); graphics.DrawLine(pen, 1, 8, 14, 8); graphics.DrawLine(pen, 1, 13, 14, 13);
                graphics.DrawEllipse(pen, 4, 1, 4, 4); graphics.DrawEllipse(pen, 9, 6, 4, 4); graphics.DrawEllipse(pen, 3, 11, 4, 4); break;
            case 5:
                graphics.DrawLines(pen, new[] { new PointF(1, 2), new PointF(1, 13), new PointF(14, 13), new PointF(14, 2) }); graphics.DrawEllipse(pen, 4, 7, 3, 3); graphics.DrawEllipse(pen, 9, 7, 3, 3); break;
            case 6:
                graphics.DrawRectangle(pen, 1, 1, 13, 13); graphics.DrawRectangle(pen, 4, 1, 7, 5); graphics.DrawRectangle(pen, 4, 9, 7, 5); break;
            case 7:
                graphics.DrawLines(pen, new[] { new PointF(1, 12), new PointF(1, 3), new PointF(6, 3), new PointF(8, 5), new PointF(14, 5) });
                graphics.DrawPolygon(pen, new[] { new PointF(1, 13), new PointF(4, 7), new PointF(15, 7), new PointF(12, 13) }); break;
            case 8:
                graphics.DrawLine(pen, 2, 3, 13, 3); graphics.DrawLines(pen, new[] { new PointF(3, 3), new PointF(4, 14), new PointF(11, 14), new PointF(12, 3) });
                graphics.DrawLine(pen, 5, 1, 10, 1); graphics.DrawLine(pen, 6, 6, 6, 11); graphics.DrawLine(pen, 9, 6, 9, 11);
                break;
            case 11:
                graphics.DrawLine(pen, 0, 2, 8, 2); graphics.DrawLine(pen, 2, 0, 6, 0);
                graphics.DrawLines(pen, new[] { new PointF(1, 2), new PointF(2, 12), new PointF(6, 12) });
                graphics.DrawLine(pen, 6, 5, 15, 5); graphics.DrawLine(pen, 8, 3, 12, 3);
                graphics.DrawLines(pen, new[] { new PointF(7, 5), new PointF(8, 15), new PointF(13, 15), new PointF(14, 5) }); break;
            case 9:
                graphics.DrawLine(pen, 7, 2, 7, 13); graphics.DrawLine(pen, 2, 7, 13, 7); break;
            case 10:
                graphics.DrawRectangle(pen, 1, 1, 10, 13); graphics.DrawLine(pen, 1, 5, 11, 5); graphics.DrawLine(pen, 5, 5, 5, 14);
                graphics.DrawLine(pen, 8, 9, 15, 9); graphics.DrawLines(pen, new[] { new PointF(12, 6), new PointF(15, 9), new PointF(12, 12) }); break;
            case 12:
                graphics.DrawPolygon(pen, new[] { new PointF(1, 14), new PointF(2, 9), new PointF(11, 0), new PointF(15, 4), new PointF(6, 13) }); graphics.DrawLine(pen, 9, 2, 13, 6); break;
            case 13:
                graphics.DrawRectangle(pen, 1, 1, 5, 5); graphics.DrawLine(pen, 7, 8, 14, 8); graphics.DrawLines(pen, new[] { new PointF(11, 5), new PointF(14, 8), new PointF(11, 11) }); graphics.DrawRectangle(pen, 1, 9, 5, 5); break;
            case 14:
            case 15:
            case 16:
            case 17:
                // GDI TextRenderer ignores Graphics transforms; use GDI+ for the complete icon.
                using (var font = new Font(Typography.Family, 10, FontStyle.Regular, GraphicsUnit.Pixel))
                using (var brush = new SolidBrush(color))
                using (var format = new StringFormat(StringFormat.GenericTypographic) { Alignment = StringAlignment.Center })
                    graphics.DrawString(glyph is 14 or 16 ? "LS" : "OV", font, brush, new RectangleF(0, 0, 20, 12), format);
                graphics.DrawLine(pen, 3, 17, 17, 17);
                if (glyph is 14 or 15) { graphics.DrawLine(pen, 14, 14, 17, 17); graphics.DrawLine(pen, 14, 20, 17, 17); }
                else { graphics.DrawLine(pen, 10, 14, 10, 20); }
                break;
            case 18:
                graphics.DrawRectangle(pen, 1, 1, 9, 10); graphics.DrawRectangle(pen, 5, 5, 9, 10); break;
            case 19:
                graphics.DrawLine(pen, 7, 0, 7, 15); graphics.DrawLine(pen, 0, 7, 15, 7);
                graphics.DrawLines(pen, new[] { new PointF(4, 3), new PointF(7, 0), new PointF(10, 3) }); graphics.DrawLines(pen, new[] { new PointF(12, 4), new PointF(15, 7), new PointF(12, 10) }); break;
            case 20:
                graphics.DrawLines(pen, new[] { new PointF(2, 13), new PointF(6, 5), new PointF(13, 2) });
                graphics.DrawRectangle(pen, 0, 11, 3, 3); graphics.DrawRectangle(pen, 4, 3, 3, 3); graphics.DrawRectangle(pen, 11, 0, 3, 3); break;
            case 21:
                graphics.DrawLines(pen, new[] { new PointF(1, 13), new PointF(6, 5), new PointF(14, 2) });
                graphics.DrawLine(pen, 8, 2, 12, 10); graphics.DrawLine(pen, 12, 2, 8, 10); graphics.DrawRectangle(pen, 0, 11, 3, 3); break;
            case 22:
                graphics.DrawEllipse(pen, 1, 1, 13, 13); graphics.DrawLine(pen, 7, 4, 7, 11); graphics.DrawLine(pen, 4, 7, 11, 7); break;
            case 23:
                graphics.DrawRectangle(pen, 1, 3, 13, 10);
                for (var x = 4; x <= 10; x += 3) { graphics.DrawLine(pen, x, 5, x, 8); }
                graphics.DrawLine(pen, 4, 10, 11, 10); break;
            case 24:
                graphics.DrawRectangle(pen, 1, 1, 10, 11); graphics.DrawLine(pen, 1, 5, 11, 5); graphics.DrawLine(pen, 5, 1, 5, 12);
                graphics.DrawLine(pen, 11, 9, 11, 15); graphics.DrawLine(pen, 8, 12, 14, 12); break;
        }
        graphics.Restore(state);
    }
}

internal sealed class ThemedToggle : CheckBox
{
    internal ThemedToggle()
    {
        AutoSize = true; Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        CheckedChanged += (_, _) => Invalidate();
    }
    public override Size GetPreferredSize(Size proposedSize)
    {
        var scale = DeviceDpi / 96f;
        var left = (int)(46 * scale);
        var maxWidth = MaximumSize.Width > 0 ? MaximumSize.Width : proposedSize.Width > left ? proposedSize.Width : 1000;
        var text = TextRenderer.MeasureText(Text, Font, new Size(Math.Max(50, maxWidth - left), 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        return new Size(Math.Min(maxWidth, text.Width + left), Math.Max((int)(28 * scale), text.Height + 6));
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        if (SystemInformation.HighContrast) { base.OnPaint(e); return; }
        e.Graphics.Clear(Parent?.BackColor ?? UiTheme.Canvas);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var scale = DeviceDpi / 96f;
        var w = 34 * scale; var h = 19 * scale; var y = (Height - h) / 2;
        using var track = new SolidBrush(!Enabled ? UiTheme.Line : Checked ? UiTheme.Orange : Color.FromArgb(182, 191, 199));
        e.Graphics.FillRectangle(track, 1, y, w, h);
        using var thumb = new SolidBrush(Color.White);
        var thumbSize = h - 4 * scale;
        e.Graphics.FillRectangle(thumb, Checked ? 1 + w - h + 2 * scale : 1 + 2 * scale, y + 2 * scale, thumbSize, thumbSize);
        var textBounds = new Rectangle((int)(45 * scale), 0, Math.Max(0, Width - (int)(45 * scale)), Height);
        TextRenderer.DrawText(e.Graphics, Text, Font, textBounds, Enabled ? UiTheme.Ink : SystemColors.GrayText, TextFormatFlags.WordBreak | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, textBounds);
    }
}
