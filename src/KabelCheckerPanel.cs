using Enexis.KabelChecker.AutoCAD;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Techbase.Blocks;

/// <summary>Hosts the upstream direction builder itself, retaining its calculation and export workflow.</summary>
internal sealed class KabelCheckerPanel : UserControl
{
    private readonly Panel viewport = new() { Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty };
    internal KabelCheckerForm Builder { get; }
    internal bool HasVerticalScroll => viewport.VerticalScroll.Visible;
    internal string ViewportMetrics() => "page=" + ClientSize + "; rows=" + string.Join(",", hostLayout.GetRowHeights()) + "; heading=" + headerBar.Height + "; actions=" + actionBar.Height + "; viewport=" + viewport.Bounds + "; client=" + viewport.ClientSize + "; scroll=" + viewport.HorizontalScroll.Visible + "/" + viewport.VerticalScroll.Visible + "; margins=" + viewport.Margin + "/" + Builder.Margin;
    private bool fitting;
    private bool fitQueued;
    private bool compactReady;
    private readonly TableLayoutPanel hostLayout;
    private readonly FlowLayoutPanel headerBar;
    private readonly FlowLayoutPanel actionBar;
    private readonly Label infoStatus = new() { AutoSize = true, MaximumSize = new Size(700, 0), Padding = new Padding(6) };
    internal KabelCheckerPanel(Action? expand = null, Action? returnToBlocks = null,
        IReadOnlyDictionary<string, double>? presets = null, string? message = null)
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 9);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        hostLayout = layout;
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var heading = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(3), Margin = Padding.Empty };
        headerBar = heading;
        var title = UiTheme.Title("Enexis kabelchecker"); title.Font = new Font(Typography.Family, 12); title.Margin = new Padding(0, 2, 12, 2);
        heading.Controls.Add(title);
        var enlarge = new ThemedButton { Text = "Groter openen", AutoSize = true };
        enlarge.Click += (_, _) => expand?.Invoke();
        heading.Controls.Add(enlarge);
        layout.Controls.Add(heading, 0, 0);
        layout.Controls.Add(viewport, 0, 1);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(3), Margin = Padding.Empty, WrapContents = true };
        actionBar = footer;
        var placeInfo = new ThemedButton { Name = "PlaceDirectionInfoButton", Text = "Richting-info plaatsen", AutoSize = true };
        placeInfo.Click += (_, _) => QueueDirectionInfo();
        footer.Controls.Add(placeInfo); footer.Controls.Add(infoStatus);
        layout.Controls.Add(footer, 0, 2);
        Controls.Add(layout);
        Builder = new KabelCheckerForm(presets, message)
        {
            TopLevel = false, FormBorderStyle = FormBorderStyle.None,
            AutoScaleMode = AutoScaleMode.None,
            StartPosition = FormStartPosition.Manual, Location = Point.Empty,
            Margin = Padding.Empty,
            MinimumSize = new Size(780, 380), Size = new Size(920, 480)
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
        UiTypography.Apply(this);
        Builder.ApplyCompactStyle();
        foreach (var button in new Button[] { enlarge, placeInfo })
        {
            button.Font = Builder.Font; button.MinimumSize = new Size(0, 24); button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            button.Margin = new Padding(1); button.Padding = new Padding(4, 1, 4, 1);
        }
        infoStatus.Padding = new Padding(2); infoStatus.Font = Builder.Font;
        using var actions = Builder.TakeActionBar();
        var buttons = actions.Controls.OfType<Button>().ToArray();
        foreach (var button in buttons) footer.Controls.Add(button);
        var calculate = buttons.Single(b => b.Text == "Bereken richting");
        calculate.Name = "CalculateDirectionButton";
        footer.Controls.SetChildIndex(calculate, 0);
        footer.Controls.SetChildIndex(buttons.Single(b => b.Text == "Huidige richting leegmaken"), 1);
        footer.Controls.SetChildIndex(buttons.Single(b => b.Text == "Sluiten"), 2);
        compactReady = true;
        Builder.CompactLayoutChanged += FitBuilder;
        hostLayout.Layout += (_, _) => QueueFit();
        SizeChanged += (_, _) => QueueFit(); VisibleChanged += (_, _) => { if (Visible) QueueFit(); };
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
    private void QueueFit()
    {
        if (fitting || fitQueued || !IsHandleCreated || IsDisposed || Disposing) return;
        fitQueued = true;
        BeginInvoke(new Action(() =>
        {
            fitQueued = false;
            if (!IsDisposed && !Disposing) FitBuilder();
        }));
    }
    private void FitBuilder()
    {
        if (Builder == null || Builder.IsDisposed || fitting) return;
        fitting = true;
        try
        {
            var scroll = viewport.AutoScrollPosition;
            viewport.AutoScroll = false;
            viewport.AutoScrollMinSize = Size.Empty;
            var width = Math.Max(Builder.MinimumSize.Width, viewport.Width - 1);
            var horizontal = width > viewport.Width ? SystemInformation.HorizontalScrollBarHeight : 0;
            Builder.Width = width;
            var preferred = compactReady ? Math.Max(Builder.MinimumSize.Height, Builder.MeasureCompactHeight()) : Builder.Height;
            Builder.Height = preferred;
            if (compactReady)
            {
                var available = Math.Max(0, ClientSize.Height - headerBar.Height - actionBar.Height - 6);
                Builder.Height = Math.Max(Builder.MinimumSize.Height, Builder.MeasureCompactHeight(available - horizontal - 1));
                hostLayout.RowStyles[1].SizeType = SizeType.Absolute;
                hostLayout.RowStyles[1].Height = Math.Min(available, Builder.Height + horizontal + 1);
                hostLayout.PerformLayout();
            }
            viewport.AutoScroll = true;
            viewport.AutoScrollMinSize = Builder.Size;
            viewport.AutoScrollPosition = new Point(-scroll.X, -scroll.Y);
        }
        finally { fitting = false; }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) Builder?.Dispose();
        base.Dispose(disposing);
    }
}
