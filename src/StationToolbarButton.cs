using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Techbase.Blocks;

/// <summary>
/// Kleine, zelfstandig getekende actieknop voor Stationslocaties.
/// De tekst is onafhankelijk van FlatStyle/WinForms theme painting en
/// alle vier de hoeken worden binnen de eigen client-rectangle getekend.
/// </summary>
internal sealed class StationToolbarButton : Button
{
    [System.ComponentModel.DefaultValue(false)]
    internal bool PrimaryAction { get; set; }
    private bool hovering;
    private bool pressing;

    internal StationToolbarButton()
    {
        UseMnemonic = false;
        UseVisualStyleBackColor = false;
        FlatStyle = FlatStyle.Flat;
        Cursor = Cursors.Hand;
        AutoSize = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        hovering = true;
        base.OnMouseEnter(e);
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        hovering = false;
        pressing = false;
        base.OnMouseLeave(e);
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        pressing = true;
        base.OnMouseDown(e);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        pressing = false;
        base.OnMouseUp(e);
        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (SystemInformation.HighContrast) { base.OnPaint(e); return; }

        e.Graphics.Clear(Parent?.BackColor ?? UiTheme.Canvas);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var frame = new RectangleF(1f, 1f, Math.Max(1, Width - 2.5f), Math.Max(1, Height - 2.5f));
        var diameter = Math.Min(Math.Min(frame.Width, frame.Height), 17f * DeviceDpi / 96f);
        using var round = new GraphicsPath();
        round.AddArc(frame.Left, frame.Top, diameter, diameter, 180, 90);
        round.AddArc(frame.Right - diameter, frame.Top, diameter, diameter, 270, 90);
        round.AddArc(frame.Right - diameter, frame.Bottom - diameter, diameter, diameter, 0, 90);
        round.AddArc(frame.Left, frame.Bottom - diameter, diameter, diameter, 90, 90);
        round.CloseFigure();

        var fill = !Enabled ? UiTheme.Canvas
            : PrimaryAction
                ? pressing ? Color.FromArgb(128, 49, 8) :
                  hovering ? Color.FromArgb(153, 58, 9) : Color.FromArgb(180, 66, 10)
                : pressing ? Color.FromArgb(255, 219, 187) :
                  hovering ? UiTheme.SoftOrange : Color.White;
        var border = PrimaryAction && Enabled ? fill : UiTheme.Line;
        using (var background = new SolidBrush(fill)) e.Graphics.FillPath(background, round);
        using (var borderPen = new Pen(border)) e.Graphics.DrawPath(borderPen, round);

        var ink = !Enabled ? SystemColors.GrayText : PrimaryAction ? Color.White : UiTheme.Ink;
        var labelBounds = Rectangle.FromLTRB(PrimaryAction ? 28 : 8, 3, Math.Max(29, Width - 8), Math.Max(4, Height - 3));
        if (PrimaryAction && Enabled)
        {
            using var pen = new Pen(Color.White, 1.6f);
            var iconX = 17f * DeviceDpi / 96f;
            var iconY = Height / 2f;
            e.Graphics.DrawLine(pen, iconX - 5, iconY, iconX + 5, iconY);
            e.Graphics.DrawLine(pen, iconX, iconY - 5, iconX, iconY + 5);
        }
        TextRenderer.DrawText(e.Graphics, Text, Font, labelBounds, ink,
            TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis |
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        if (Focused && ShowFocusCues)
            ControlPaint.DrawFocusRectangle(e.Graphics,
                Rectangle.Inflate(ClientRectangle, -5, -5), ink, fill);
    }
}
