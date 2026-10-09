using Enexis.KabelChecker.AutoCAD;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Techbase.Blocks;

/// <summary>Hosts the upstream direction builder itself, retaining its calculation and export workflow.</summary>
internal sealed class KabelCheckerPanel : UserControl
{
    private readonly Panel viewport = new() { Dock = DockStyle.Fill, AutoScroll = true };
    internal KabelCheckerForm Builder { get; }
    private readonly Label infoStatus = new() { AutoSize = true, MaximumSize = new Size(700, 0), Padding = new Padding(6) };
    internal KabelCheckerPanel(Action? expand = null, Action? returnToBlocks = null,
        IReadOnlyDictionary<string, double>? presets = null, string? message = null)
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 9);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var heading = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(6) };
        heading.Controls.Add(new Label { Text = "ENEXIS KABELCHECKER", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Padding = new Padding(0, 6, 6, 0) });
        var enlarge = new Button { Text = "Groter openen", AutoSize = true };
        enlarge.Click += (_, _) => expand?.Invoke();
        heading.Controls.Add(enlarge);
        layout.Controls.Add(heading, 0, 0);
        layout.Controls.Add(viewport, 0, 1);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(6), WrapContents = true };
        var placeInfo = new Button { Name = "PlaceDirectionInfoButton", Text = "Richting-info plaatsen", AutoSize = true };
        placeInfo.Click += (_, _) => QueueDirectionInfo();
        footer.Controls.Add(placeInfo); footer.Controls.Add(infoStatus);
        layout.Controls.Add(footer, 0, 2);
        Controls.Add(layout);
        Builder = new KabelCheckerForm(presets, message)
        {
            TopLevel = false, FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.Manual, Location = Point.Empty,
            MinimumSize = new Size(940, 790), Size = new Size(1000, 820)
        };
        Builder.FormClosing += (_, args) =>
        {
            if (args.CloseReason != CloseReason.UserClosing) return;
            args.Cancel = true;
            returnToBlocks?.Invoke();
        };
        viewport.Controls.Add(Builder);
        DeleteAllDirectionsButtonInstaller.AttachToForms(new[] { Builder });
        viewport.Resize += (_, _) => FitBuilder();
        Builder.Show();
        FitBuilder();
    }
    private void QueueDirectionInfo()
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc == null) { infoStatus.Text = "Open eerst een tekening."; return; }
        if (Convert.ToInt32(AcApp.GetSystemVariable("CMDACTIVE")) != 0) { infoStatus.Text = "Rond het actieve commando af of druk Esc."; return; }
        if (Plugin.PendingDirectionInfo != null) { infoStatus.Text = "De vorige plaatsopdracht staat nog klaar."; return; }
        try
        {
            var direction = Builder.CaptureSelectedDirectionInfo();
            if (direction == null) return;
            var request = new DirectionInfoRequest(doc, direction, KaderVersionSelection.Current, KaderVersionSelection.CurrentMode);
            DirectionInfoService.ValidateLayer(doc.Database, direction.Number);
            Plugin.PendingDirectionInfo = request;
            doc.SendStringToExecute("_TBRICHTINGINFO ", true, false, false);
            infoStatus.Text = $"Klik de plaats voor de informatie van richting {direction.Number}.";
        }
        catch (Exception ex) { infoStatus.Text = ex.Message; }
    }
    internal void ReportInfo(string text) => infoStatus.Text = text;
    private void FitBuilder()
    {
        if (Builder == null || Builder.IsDisposed) return;
        Builder.Size = new Size(Math.Max(Builder.MinimumSize.Width, viewport.ClientSize.Width - SystemInformation.VerticalScrollBarWidth),
            Math.Max(Builder.MinimumSize.Height, viewport.ClientSize.Height - SystemInformation.HorizontalScrollBarHeight));
        viewport.AutoScrollMinSize = Builder.Size;
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) Builder?.Dispose();
        base.Dispose(disposing);
    }
}
