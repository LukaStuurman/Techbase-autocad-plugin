using System;
using System.Drawing;
using System.Windows.Forms;

namespace Techbase.Blocks;

/// <summary>
/// Gedeelde, testbare kop van de stationslocatie-werkruimte.
/// Alle labels worden door WinForms zelf gerenderd; er zijn geen
/// custom-painted tekstvlakken die in de AutoCAD-palette leeg kunnen lijken.
/// </summary>
internal sealed class StationLocationHeader : UserControl
{
    private readonly TableLayoutPanel content;
    private readonly TableLayoutPanel heading;
    private readonly Panel primaryHost;
    private readonly Label title;
    private readonly Label subtitle;

    internal StationToolbarButton NewLocation { get; } = new()
    {
        Name = "StationNewLocation",
        Text = "Nieuwe locatie",
        PrimaryAction = true,
        AccessibleName = "Nieuwe stationslocatie plaatsen",
        Size = new Size(174, 42),
        TabStop = true,
    };

    internal LinkLabel RefreshLink { get; } = new()
    {
        Name = "StationRefresh",
        Text = "Lijst vernieuwen",
        AccessibleName = "Lijst met stationslocaties vernieuwen",
        AutoSize = true,
        TabStop = true,
        LinkBehavior = LinkBehavior.HoverUnderline,
        Margin = new Padding(13, 5, 5, 3)
    };

    internal LinkLabel HelpLink { get; } = new()
    {
        Name = "StationHelp",
        Text = "Uitleg en tips",
        AccessibleName = "Uitleg over Stationslocaties openen",
        AutoSize = true,
        TabStop = true,
        LinkBehavior = LinkBehavior.HoverUnderline,
        Margin = new Padding(12, 5, 0, 3)
    };

    internal TextBox Search { get; } = new()
    {
        Name = "StationSearch",
        Dock = DockStyle.Fill,
        PlaceholderText = "Zoek trafonummer of blad…",
        AccessibleName = "Zoek stationslocatie"
    };

    internal Label Summary { get; } = new()
    {
        Name = "StationSummary", Dock = DockStyle.Fill,
        AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft
    };

    private bool compact;

    internal int PreferredHeaderHeight => compact ? 216 : 180;

    internal StationLocationHeader()
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        Dock = DockStyle.Fill;
        BackColor = UiTheme.Canvas;
        Font = new Font("Segoe UI", 9);

        content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
            Margin = Padding.Empty, Padding = Padding.Empty
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        heading = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2,
            Margin = Padding.Empty, Padding = Padding.Empty
        };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        var captions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
            Margin = Padding.Empty, Padding = Padding.Empty
        };
        captions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        captions.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        captions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        title = new Label
        {
            Name = "StationslocatiesTitle",
            Text = "Stationslocaties",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 17),
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false
        };
        subtitle = new Label
        {
            Text = "Trafokaarten, voorkeurslocaties en alternatieven",
            Dock = DockStyle.Fill, AutoEllipsis = true,
            UseMnemonic = false, TextAlign = ContentAlignment.MiddleLeft
        };
        captions.Controls.Add(title, 0, 0);
        captions.Controls.Add(subtitle, 0, 1);
        heading.Controls.Add(captions, 0, 0);

        primaryHost = new Panel
        {
            Dock = DockStyle.Fill, Margin = Padding.Empty,
            Padding = Padding.Empty, BackColor = UiTheme.Canvas
        };
        primaryHost.Controls.Add(NewLocation);
        primaryHost.SizeChanged += (_, _) => PositionPrimaryButton();
        heading.Controls.Add(primaryHost, 1, 0);
        content.Controls.Add(heading, 0, 0);

        Summary.Padding = new Padding(1, 1, 0, 0);
        content.Controls.Add(Summary, 0, 1);

        var tools = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1,
            Margin = Padding.Empty, Padding = Padding.Empty
        };
        tools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tools.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        tools.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var searchBox = new Panel
        {
            Name = "StationSearchContainer",
            Dock = DockStyle.Fill, Padding = new Padding(11, 7, 10, 7),
            Margin = new Padding(0, 0, 0, 2),
            BackColor = UiTheme.Input
        };
        searchBox.Controls.Add(Search);
        tools.Controls.Add(searchBox, 0, 0);
        var quickLinks = new FlowLayoutPanel
        {
            Name = "StationQuickLinks", Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoScroll = false, WrapContents = false,
            Padding = new Padding(2, 0, 0, 0),
            Margin = Padding.Empty, BackColor = UiTheme.Canvas
        };
        quickLinks.Controls.Add(HelpLink);
        quickLinks.Controls.Add(RefreshLink);
        tools.Controls.Add(quickLinks, 0, 1);
        content.Controls.Add(tools, 0, 2);

        Controls.Add(content);
        PositionPrimaryButton();
        ApplyAppearance();
    }

    private void PositionPrimaryButton()
    {
        var x = Math.Max(0, primaryHost.ClientSize.Width - NewLocation.Width - 5);
        var y = Math.Max(0, (primaryHost.ClientSize.Height - NewLocation.Height) / 2);
        NewLocation.Location = new Point(x, y);
    }

    internal void UseCompactLayout(bool enabled)
    {
        if (compact == enabled) return;
        compact = enabled;
        heading.SuspendLayout();
        heading.RowStyles[0].Height = compact ? 60 : 74;
        heading.RowStyles[1].Height = compact ? 50 : 0;
        heading.ColumnStyles[1].Width = compact ? 0 : 190;
        heading.SetColumnSpan(heading.GetControlFromPosition(0, 0)!, compact ? 2 : 1);
        heading.SetCellPosition(primaryHost, new TableLayoutPanelCellPosition(compact ? 0 : 1, compact ? 1 : 0));
        heading.SetColumnSpan(primaryHost, compact ? 2 : 1);
        content.RowStyles[0].Height = compact ? 110 : 74;
        heading.ResumeLayout(true);
        PositionPrimaryButton();
    }

    internal void ApplyAppearance()
    {
        if (SystemInformation.HighContrast) return;
        BackColor = UiTheme.Canvas;
        content.BackColor = UiTheme.Canvas;
        heading.BackColor = UiTheme.Canvas;
        primaryHost.BackColor = UiTheme.Canvas;
        title.ForeColor = UiTheme.Ink;
        subtitle.ForeColor = UiTheme.Muted;
        Summary.ForeColor = UiTheme.Muted;
        Search.BackColor = UiTheme.Input;
        Search.ForeColor = UiTheme.Ink;
        foreach (var link in new[] { RefreshLink, HelpLink })
        {
            link.BackColor = UiTheme.Canvas;
            link.ForeColor = UiTheme.Ink;
            link.LinkColor = UiTheme.Ink;
            link.ActiveLinkColor = Color.FromArgb(150, 60, 13);
            link.VisitedLinkColor = UiTheme.Ink;
            link.DisabledLinkColor = UiTheme.Muted;
            link.Font = new Font(Font.FontFamily, 9.5f, FontStyle.Regular);
        }
        NewLocation.Font = new Font(Font.FontFamily, 10f, FontStyle.Bold);
        NewLocation.ApplyAppearance();
    }
}
