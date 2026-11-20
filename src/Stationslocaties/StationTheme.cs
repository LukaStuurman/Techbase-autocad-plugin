using System.Drawing;
using System.Windows.Forms;

namespace Stationslocaties;

// Gedeelde vormgeving van het stationslocatiebeheer, afgestemd op de Techbase-palette.
internal static class StationTheme
{
    internal static readonly Color Canvas = Color.FromArgb(245, 247, 249);
    internal static readonly Color Ink = Color.FromArgb(42, 49, 58);
    internal static readonly Color Muted = Color.FromArgb(98, 108, 120);
    internal static readonly Color Border = Color.FromArgb(211, 219, 228);
    internal static readonly Color Accent = Color.FromArgb(39, 98, 154);
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
            button.FlatAppearance.MouseOverBackColor = prominent ? Color.FromArgb(30, 82, 130) : Color.FromArgb(235, 241, 248);
            button.FlatAppearance.MouseDownBackColor = prominent ? Color.FromArgb(25, 69, 111) : Color.FromArgb(221, 232, 244);
            button.Padding = new Padding(8, 0, 8, 0);
        }
        if (parent is Form) Techbase.Blocks.UiTypography.Apply(parent);
    }
}
