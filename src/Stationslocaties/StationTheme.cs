using System.Drawing;
using System.Windows.Forms;

namespace Stationslocaties;

// Gedeelde vormgeving van het stationslocatiebeheer, afgestemd op de Techbase-palette.
internal static class StationTheme
{
    internal static readonly Color Canvas = Techbase.Blocks.UiTheme.Canvas;
    internal static readonly Color Ink = Techbase.Blocks.UiTheme.Ink;
    internal static readonly Color Muted = Techbase.Blocks.UiTheme.Muted;
    internal static readonly Color Border = Techbase.Blocks.UiTheme.Line;
    internal static readonly Color Accent = Techbase.Blocks.UiTheme.Orange;
    internal static readonly Color Tint = Color.White;

    internal static void StyleButtons(Control parent, Button primary, Button? destructive = null)
    {
        foreach (Control child in parent.Controls)
        {
            if (child is not Button button)
            {
                StyleButtons(child, primary, destructive);
                continue;
            }
            var prominent = button == primary;
            button.UseVisualStyleBackColor = false;
            button.FlatStyle = FlatStyle.Flat;
            button.BackColor = prominent ? Accent : Color.White;
            button.ForeColor = prominent ? Color.White : button == destructive ? Color.Firebrick : Ink;
            button.FlatAppearance.BorderColor = prominent ? Accent : Border;
            button.FlatAppearance.MouseOverBackColor = prominent ? Color.FromArgb(161, 65, 10) : Color.FromArgb(255, 244, 234);
            button.FlatAppearance.MouseDownBackColor = prominent ? Color.FromArgb(140, 53, 6) : Color.FromArgb(255, 229, 208);
            button.Padding = new Padding(8, 0, 8, 0);
        }
        if (parent is Form) Techbase.Blocks.UiTypography.Apply(parent);
    }
}
