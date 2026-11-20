using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Techbase.Blocks;

/// <summary>
/// Normale WinForms-knop met een afgeronde zichtbare regio.
/// Geen eigen tekstschildering: de native knoprenderer verzorgt het label,
/// waardoor AutoCAD/WinForms-thema's de tekst niet meer weg kunnen schilderen.
/// </summary>
internal sealed class StationToolbarButton : Button
{
    [DefaultValue(false)]
    internal bool PrimaryAction { get; set; }

    internal StationToolbarButton()
    {
        AutoSize = false;
        UseMnemonic = false;
        UseVisualStyleBackColor = false;
        FlatStyle = FlatStyle.Flat;
        Cursor = Cursors.Hand;
        TextAlign = ContentAlignment.MiddleCenter;
        Margin = Padding.Empty;
        ApplyAppearance();
    }

    internal void ApplyAppearance()
    {
        if (SystemInformation.HighContrast)
        {
            FlatStyle = FlatStyle.Standard;
            ForeColor = SystemColors.ControlText;
            BackColor = SystemColors.Control;
            Region = null;
            return;
        }

        UseVisualStyleBackColor = false;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = PrimaryAction ? 0 : 1;
        BackColor = PrimaryAction ? Color.FromArgb(180, 66, 10) : Color.White;
        ForeColor = PrimaryAction ? Color.White : UiTheme.Ink;
        FlatAppearance.BorderColor = PrimaryAction ? BackColor : UiTheme.Line;
        FlatAppearance.MouseOverBackColor = PrimaryAction
            ? Color.FromArgb(155, 58, 10) : UiTheme.SoftOrange;
        FlatAppearance.MouseDownBackColor = PrimaryAction
            ? Color.FromArgb(132, 49, 8) : Color.FromArgb(255, 218, 181);
        ApplyRoundCorners();
        Invalidate();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (!SystemInformation.HighContrast) ApplyRoundCorners();
    }

    private void ApplyRoundCorners()
    {
        if (Width < 8 || Height < 8) return;
        var radius = Math.Min(10f * DeviceDpi / 96f, Math.Min(Width, Height) / 2f);
        var diameter = radius * 2f;
        var frame = new RectangleF(0, 0, Width, Height);
        using var rounded = new GraphicsPath();
        rounded.AddArc(frame.Left, frame.Top, diameter, diameter, 180, 90);
        rounded.AddArc(frame.Right - diameter, frame.Top, diameter, diameter, 270, 90);
        rounded.AddArc(frame.Right - diameter, frame.Bottom - diameter, diameter, diameter, 0, 90);
        rounded.AddArc(frame.Left, frame.Bottom - diameter, diameter, diameter, 90, 90);
        rounded.CloseFigure();
        var old = Region;
        Region = new Region(rounded);
        old?.Dispose();
    }
}
