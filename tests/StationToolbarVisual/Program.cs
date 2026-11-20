using System.Drawing;
using System.Windows.Forms;
using Techbase.Blocks;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        using var host = new Form
        {
            Width = 420, Height = 210,
            BackColor = UiTheme.Canvas,
            ShowInTaskbar = false
        };
        var buttons = new[]
        {
            new StationToolbarButton { Text = "Nieuwe locatie", PrimaryAction = true,
                Size = new Size(170, 45), Location = new Point(12, 12),
                Font = new Font("Segoe UI", 10, FontStyle.Bold) },
            new StationToolbarButton { Text = "Vernieuwen",
                Size = new Size(115, 41), Location = new Point(12, 75),
                Font = new Font("Segoe UI", 9) },
            new StationToolbarButton { Text = "Uitleg",
                Size = new Size(72, 41), Location = new Point(137, 75),
                Font = new Font("Segoe UI", 9) }
        };
        host.Controls.AddRange(buttons);
        host.CreateControl();
        foreach (var button in buttons)
        {
            button.CreateControl();
            using var bitmap = new Bitmap(button.Width, button.Height);
            button.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            // De vier hoeken moeten achtergrondkleur hebben, met een gevulde onderrand.
            var lowerLeft = bitmap.GetPixel(1, bitmap.Height - 2);
            var lowerMiddle = bitmap.GetPixel(bitmap.Width / 2, bitmap.Height - 4);
            var cornerIsBackground = lowerLeft.GetBrightness() > .7f;
            var bottomIsFilled = button.PrimaryAction && lowerMiddle.GetBrightness() < .5f;
            if (!cornerIsBackground || (button.PrimaryAction && !bottomIsFilled))
                throw new InvalidOperationException("Onderste hoeken niet rond: " + button.Text);

            var textPixels = 0;
            for (var y = bitmap.Height / 4; y < bitmap.Height * 3 / 4; y++)
                for (var x = button.PrimaryAction ? 33 : 10; x < bitmap.Width - 12; x++)
                {
                    var color = bitmap.GetPixel(x, y);
                    if (button.PrimaryAction
                        ? color.R > 215 && color.G > 215 && color.B > 215
                        : color.R < 130 && color.G < 130 && color.B < 130)
                        textPixels++;
                }
            if (textPixels < 8)
                throw new InvalidOperationException("Knop heeft geen leesbare tekst: " + button.Text);

            Console.WriteLine("PASS: " + button.Text + " (" + button.Width + "x" + button.Height +
                "), tekstrasterpixels: " + textPixels);
        }
        return 0;
    }
}
