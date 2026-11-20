using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Techbase.Blocks;

/// <summary>
/// Afgeronde actie met een echte WinForms-label als tekstlaag.
/// Anders dan een Button.Region (die bij DrawToBitmap in een complexe
/// AutoCAD-palette rechthoekig kan blijven) wordt de vorm zelf geschilderd.
/// De tekst is geen custom GDI-tekst en blijft dus leesbaar.
/// </summary>
internal sealed class StationToolbarButton : UserControl
{
    private readonly Label caption = new()
    {
        Dock = DockStyle.Fill,
        AutoSize = false,
        AutoEllipsis = true,
        BackColor = Color.Transparent,
        TextAlign = ContentAlignment.MiddleCenter,
        UseMnemonic = false,
        Cursor = Cursors.Hand,
        Margin = Padding.Empty
    };
    private bool hovered;
    private bool pressed;

    [DefaultValue(false)]
    internal bool PrimaryAction { get; set; }

    public override string Text
    {
        get => base.Text;
        set { base.Text = value; caption.Text = value; }
    }

    internal StationToolbarButton()
    {
        AutoSize = false;
        BackColor = UiTheme.Canvas;
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleRole = AccessibleRole.PushButton;
        SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw, true);
        Controls.Add(caption);

        // Het label vangt de klik op; voer precies dezelfde actie uit als
        // een klik op de rand van de knop.
        caption.Click += (_, _) => OnClick(EventArgs.Empty);
        caption.MouseEnter += (_, _) => { hovered = true; Invalidate(); };
        caption.MouseLeave += (_, _) => { hovered = false; pressed = false; Invalidate(); };
        caption.MouseDown += (_, _) => { pressed = true; Invalidate(); };
        caption.MouseUp += (_, _) => { pressed = false; Invalidate(); };
        ApplyAppearance();
    }

    internal void ApplyAppearance()
    {
        BackColor = Parent?.BackColor ?? UiTheme.Canvas;
        ForeColor = SystemInformation.HighContrast ? SystemColors.ControlText :
            PrimaryAction ? Color.White : UiTheme.Ink;
        caption.ForeColor = ForeColor;
        caption.BackColor = Color.Transparent;
        caption.Font = Font;
        caption.Text = Text;
        caption.Enabled = Enabled;
        Invalidate();
    }

    protected override void OnForeColorChanged(EventArgs e)
    {
        base.OnForeColorChanged(e);
        caption.ForeColor = ForeColor;
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        caption.Font = Font;
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        caption.Enabled = Enabled;
        ApplyAppearance();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        hovered = true;
        base.OnMouseEnter(e);
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        hovered = pressed = false;
        base.OnMouseLeave(e);
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        pressed = true;
        base.OnMouseDown(e);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        pressed = false;
        base.OnMouseUp(e);
        Invalidate();
    }

    protected override bool IsInputKey(Keys keyData)
    {
        if (keyData == Keys.Space || keyData == Keys.Enter) return true;
        return base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            OnClick(EventArgs.Empty);
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var canvas = Parent?.BackColor ?? UiTheme.Canvas;
        e.Graphics.Clear(canvas);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var frame = new RectangleF(0.75f, 0.75f,
            Math.Max(1f, Width - 1.5f), Math.Max(1f, Height - 1.5f));
        var radius = Math.Min(10f * DeviceDpi / 96f, Math.Min(frame.Width, frame.Height) / 2f);
        var diameter = radius * 2f;
        using var outline = new GraphicsPath();
        outline.AddArc(frame.Left, frame.Top, diameter, diameter, 180, 90);
        outline.AddArc(frame.Right - diameter, frame.Top, diameter, diameter, 270, 90);
        outline.AddArc(frame.Right - diameter, frame.Bottom - diameter, diameter, diameter, 0, 90);
        outline.AddArc(frame.Left, frame.Bottom - diameter, diameter, diameter, 90, 90);
        outline.CloseFigure();

        var fill = SystemInformation.HighContrast ? SystemColors.Control :
            !Enabled ? UiTheme.Line :
            pressed ? Color.FromArgb(132, 49, 8) :
            hovered ? Color.FromArgb(155, 58, 10) :
            PrimaryAction ? Color.FromArgb(180, 66, 10) : Color.White;
        using (var brush = new SolidBrush(fill)) e.Graphics.FillPath(brush, outline);
        using (var border = new Pen(PrimaryAction && Enabled ? fill : UiTheme.Line))
            e.Graphics.DrawPath(border, outline);

        if (Focused && ShowFocusCues)
            ControlPaint.DrawFocusRectangle(e.Graphics,
                Rectangle.Inflate(ClientRectangle, -5, -5), ForeColor, fill);
    }
}
