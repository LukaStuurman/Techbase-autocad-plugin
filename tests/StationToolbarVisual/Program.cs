using System.Drawing;
using System.Windows.Forms;
using Techbase.Blocks;

internal static class Program
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        Console.WriteLine("PASS: " + message);
    }

    private static int InkPixels(Control control, bool lightInk)
    {
        using var bmp = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bmp, new Rectangle(Point.Empty, bmp.Size));
        var count = 0;
        for (var y = 4; y < bmp.Height - 4; y++)
            for (var x = 4; x < bmp.Width - 4; x++)
            {
                var pixel = bmp.GetPixel(x, y);
                if (lightInk
                    ? (pixel.R > 205 && pixel.G > 205 && pixel.B > 205)
                    : (pixel.R < 135 && pixel.G < 135 && pixel.B < 135))
                    count++;
            }
        return count;
    }

    private static int MatchingPixels(Bitmap image, Rectangle bounds, Func<Color, bool> predicate)
    {
        var left = Math.Clamp(bounds.Left, 0, image.Width);
        var top = Math.Clamp(bounds.Top, 0, image.Height);
        var right = Math.Clamp(bounds.Right, left, image.Width);
        var bottom = Math.Clamp(bounds.Bottom, top, image.Height);
        var count = 0;
        for (var y = top; y < bottom; y++)
            for (var x = left; x < right; x++)
                if (predicate(image.GetPixel(x, y))) count++;
        return count;
    }

    private static Rectangle BoundsInHeader(StationLocationHeader header, Control control)
    {
        var origin = header.PointToClient(control.PointToScreen(Point.Empty));
        return new Rectangle(origin, control.Size);
    }

    [STAThread]
    private static int Main()
    {
        Directory.CreateDirectory("tests/StationToolbarVisual/out");
        foreach (var width in new[] { 380, 780, 1190 })
        {
            using var host = new Form
            {
                ClientSize = new Size(width, 300),
                BackColor = UiTheme.Canvas,
                ShowInTaskbar = false
            };
            using var header = new StationLocationHeader
            {
                Dock = DockStyle.None,
                Location = new Point(12, 12)
            };
            host.Controls.Add(header);
            header.UseCompactLayout(width < 620);
            header.Size = new Size(width - 24, header.PreferredHeaderHeight);
            // Niet alleen losse controls tekenen: het *hele* zichtbare venster
            // moet gerenderd worden. DrawToBitmap op een verborgen form
            // leverde voorheen volledig lege, maar 'geslaagde' screenshots op.
            host.Show();
            header.ApplyAppearance();
            header.PerformLayout();
            Application.DoEvents();
            Check(host.Visible && header.Visible,
                "Testvenster en complete Stationslocaties-kop zijn zichtbaar op " + width);

            var button = header.NewLocation;
            Check(button.Text == "Nieuwe locatie", "Primaire actie heeft juiste tekst");
            Check(button.PrimaryAction && button.Width >= 165 && button.Height >= 40,
                "Primaire actie heeft herkenbaar formaat op " + width);
            Check(button.Parent != null &&
                button.Parent.ClientRectangle.Contains(button.Bounds),
                "Nieuwe locatie wordt niet afgeknipt op " + width);
            // Een afgeronde Region kan in de samengesteld gerenderde kop alsnog
            // als een rechthoek verschijnen. Test daarom de werkelijke pixels.
            using (var buttonBitmap = new Bitmap(button.Width, button.Height))
            {
                button.DrawToBitmap(buttonBitmap, new Rectangle(Point.Empty, buttonBitmap.Size));
                var corner = buttonBitmap.GetPixel(1, button.Height - 2);
                var middle = buttonBitmap.GetPixel(button.Width / 2, button.Height - 3);
                Check(corner.GetBrightness() > .72f && middle.GetBrightness() < .50f,
                    "Werkelijk gerenderde onderste knophoek is afgerond op " + width);
            }
            Check(InkPixels(button, true) > 25,
                "Witte tekst is daadwerkelijk getekend op Nieuwe locatie (" + width + ")");

            foreach (var (link, expected) in new[]
            {
                (header.RefreshLink, "Lijst vernieuwen"),
                (header.HelpLink, "Uitleg en tips")
            })
            {
                Check(link.Text == expected && link.Width > 60 && link.Height >= 12 &&
                    link.Parent != null &&
                    link.Parent.ClientRectangle.Contains(link.Bounds),
                    expected + " is volledig zichtbaar bij " + width);
                Check(InkPixels(link, false) > 20,
                    expected + " is getekend als leesbare tekst bij " + width);
            }

            using var snapshot = new Bitmap(header.Width, header.Height);
            header.DrawToBitmap(snapshot, new Rectangle(Point.Empty, snapshot.Size));

            // Pixelcontrole op de daadwerkelijk samengestelde kop, niet op
            // afzonderlijke knoppen (die kunnen los goed zijn, maar toch verborgen).
            var wholeButton = BoundsInHeader(header, button);
            var orangePixels = MatchingPixels(snapshot, wholeButton,
                color => color.R >= 135 && color.R <= 205 && color.G < 112 && color.B < 75);
            var primaryText = MatchingPixels(snapshot, wholeButton,
                color => color.R > 250 && color.G > 250 && color.B > 250);
            Check(orangePixels > 300 && primaryText > 20,
                "Samengestelde kop toont oranje actie en witte knoptekst op " + width);
            var lowerLeft = snapshot.GetPixel(wholeButton.Left + 1, wholeButton.Bottom - 2);
            var lowerRight = snapshot.GetPixel(wholeButton.Right - 2, wholeButton.Bottom - 2);
            var lowerMiddle = snapshot.GetPixel(wholeButton.Left + wholeButton.Width / 2, wholeButton.Bottom - 3);
            Check(lowerLeft.GetBrightness() > .72f && lowerRight.GetBrightness() > .72f &&
                lowerMiddle.GetBrightness() < .50f,
                "De complete zichtbare kop toont twee afgeronde onderste knophoeken op " + width);

            foreach (var link in new[] { header.RefreshLink, header.HelpLink })
            {
                var pixels = MatchingPixels(snapshot, BoundsInHeader(header, link),
                    color => color.R < 135 && color.G < 135 && color.B < 135);
                Check(pixels > 20, "Samengestelde kop toont linktekst " + link.Text + " op " + width);
            }

            var distinct = new HashSet<int>();
            for (var y = 0; y < snapshot.Height; y += 2)
                for (var x = 0; x < snapshot.Width; x += 2)
                    distinct.Add(snapshot.GetPixel(x, y).ToArgb());
            Check(distinct.Count > 12,
                "Volledige screenshot bevat daadwerkelijk gerenderde UI op " + width);

            snapshot.Save(Path.Combine("tests", "StationToolbarVisual", "out", "header-" + width + ".png"));
            host.Hide();
        }
        return 0;
    }
}
