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
            host.CreateControl();
            header.CreateControl();
            header.ApplyAppearance();
            header.PerformLayout();
            Application.DoEvents();

            var button = header.NewLocation;
            Check(button.Text == "Nieuwe locatie", "Primaire actie heeft juiste tekst");
            Check(button.PrimaryAction && button.Width >= 165 && button.Height >= 40,
                "Primaire actie heeft herkenbaar formaat op " + width);
            Check(button.Parent != null &&
                button.Parent.ClientRectangle.Contains(button.Bounds),
                "Nieuwe locatie wordt niet afgeknipt op " + width);
            Check(button.Region != null &&
                !button.Region.IsVisible(1, button.Height - 1) &&
                !button.Region.IsVisible(button.Width - 1, button.Height - 1) &&
                button.Region.IsVisible(button.Width / 2, button.Height - 2),
                "Alle onderste hoeken zijn afgerond op " + width);
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
            snapshot.Save(Path.Combine("tests", "StationToolbarVisual", "out", "header-" + width + ".png"));
        }
        return 0;
    }
}
