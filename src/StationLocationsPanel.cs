using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using StationAction = Stationslocaties.StationLocationAction;
using StationSheet = Stationslocaties.StationSheet;

namespace Techbase.Blocks;

/// <summary>
/// Volledig geïntegreerd stationslocatiebeheer binnen de Techbase-palette.
/// De tab zelf is de werkruimte; alleen invoerformulieren openen als aparte dialoog.
/// </summary>
internal sealed record StationActionRequest(Document Document, StationAction Action, string? SheetName);

internal sealed class StationLocationsPanel : UserControl
{
    private static readonly Color Surface = Color.FromArgb(245, 247, 249);
    private static readonly Color Orange = Color.FromArgb(190, 82, 17);
    private static readonly Color OrangeHover = Color.FromArgb(161, 65, 10);
    private static readonly Color Ink = Color.FromArgb(41, 47, 56);
    private static readonly Color Muted = Color.FromArgb(100, 110, 121);
    private static readonly Color Border = Color.FromArgb(215, 222, 230);

    private readonly Label summary = new()
    {
        Dock = DockStyle.Fill, AutoEllipsis = true, ForeColor = Muted,
        Padding = new Padding(0, 3, 0, 4)
    };
    private readonly Label status = new()
    {
        Dock = DockStyle.Fill, AutoEllipsis = true, ForeColor = Muted,
        Padding = new Padding(0, 6, 0, 0)
    };
    private readonly TextBox search = new()
    {
        Dock = DockStyle.Fill,
        PlaceholderText = "Zoek op trafo, locatie of blad..."
    };
    private readonly ListView sheets = new()
    {
        View = View.Details, Dock = DockStyle.Fill, FullRowSelect = true,
        MultiSelect = false, HideSelection = false,
        BorderStyle = BorderStyle.FixedSingle
    };
    private readonly Button refresh = new() { Text = "Vernieuwen", AutoSize = true };
    private readonly Dictionary<StationAction, Button> actionButtons = new();
    private readonly ToolTip tips = new();
    private readonly List<StationSheet> cached = new();
    private HashSet<string> photos = new(StringComparer.OrdinalIgnoreCase);
    private bool overview;
    private bool loading;

    internal StationLocationsPanel()
    {
        Dock = DockStyle.Fill;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Surface;
        ForeColor = Ink;
        Font = new Font("Segoe UI", 9);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8,
            Padding = new Padding(12, 10, 12, 10)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // titel
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // beschrijving
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // zoeken
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 44)); // locaties
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // samenvatting
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // actietitel
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 56)); // acties
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // status

        root.Controls.Add(new Label
        {
            Text = "TECHBASE  /  STATIONSLOCATIES",
            Font = new Font(Font, FontStyle.Bold),
            AutoSize = true,
            Padding = new Padding(0, 4, 0, 8),
            ForeColor = Ink
        }, 0, 0);
        root.Controls.Add(new Label
        {
            Text = "Voorkeurs- en alternatieve locaties · A3-detailbladen · Totaal",
            AutoSize = true, ForeColor = Muted,
            MaximumSize = new Size(430, 0),
            Padding = new Padding(0, 0, 0, 10)
        }, 0, 1);

        var bar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2,
            AutoSize = true, Margin = new Padding(0, 0, 0, 9)
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.Controls.Add(search, 0, 0);
        bar.Controls.Add(refresh, 1, 0);
        root.Controls.Add(bar, 0, 2);

        sheets.Columns.Add("Blad", 120);
        sheets.Columns.Add("VL / AL", 65);
        sheets.Columns.Add("Foto", 55);
        sheets.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        sheets.BackColor = Color.White;
        sheets.Resize += (_, _) => SizeColumns();
        sheets.SelectedIndexChanged += (_, _) => UpdateActions();
        sheets.DoubleClick += (_, _) =>
        {
            if (Selected != null) Execute(StationAction.Open);
        };
        root.Controls.Add(sheets, 0, 3);
        root.Controls.Add(summary, 0, 4);
        root.Controls.Add(new Label
        {
            Text = "ACTIES & PROJECT",
            AutoSize = true, Font = new Font(Font, FontStyle.Bold),
            ForeColor = Ink, Padding = new Padding(0, 11, 0, 7)
        }, 0, 5);

        var operations = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true,
            Padding = new Padding(0, 2, 2, 6)
        };
        var groups = new List<FlowLayoutPanel>();
        void AddGroup(string heading, string explanation, params (StationAction Action, string Text)[] options)
        {
            var caption = new Label
            {
                AutoSize = true, Text = heading.ToUpperInvariant(),
                ForeColor = Orange, Font = new Font(Font, FontStyle.Bold),
                Margin = new Padding(0, groups.Count == 0 ? 0 : 12, 0, 4)
            };
            operations.Controls.Add(caption);
            var hint = new Label
            {
                Text = explanation, AutoSize = true,
                MaximumSize = new Size(430, 0), ForeColor = Muted,
                Margin = new Padding(0, 0, 0, 5)
            };
            operations.Controls.Add(hint);
            var buttons = new FlowLayoutPanel
            {
                AutoSize = true, MinimumSize = new Size(0, 36),
                WrapContents = true, Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            foreach (var option in options)
            {
                var primary = option.Action == StationAction.Add;
                var destructive = option.Action == StationAction.Delete;
                var button = new Button
                {
                    Name = "Station" + option.Action,
                    Text = option.Text,
                    MinimumSize = new Size(148, 34),
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    FlatStyle = FlatStyle.Flat,
                    UseVisualStyleBackColor = false,
                    BackColor = primary ? Orange : Color.White,
                    ForeColor = primary ? Color.White : destructive ? Color.Firebrick : Ink,
                    Padding = new Padding(9, 2, 9, 2),
                    Margin = new Padding(0, 0, 6, 6),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                button.FlatAppearance.BorderColor = primary ? Orange : Border;
                button.FlatAppearance.MouseOverBackColor = primary ? OrangeHover : Surface;
                button.Click += (_, _) => Execute(option.Action);
                tips.SetToolTip(button, option.Text);
                buttons.Controls.Add(button);
                actionButtons.Add(option.Action, button);
            }
            groups.Add(buttons);
            operations.Controls.Add(buttons);
        }
        AddGroup("Locaties", "Plaats en bewerk locaties direct vanuit deze tab.",
            (StationAction.Add, "Locatie plaatsen"),
            (StationAction.Open, "Open blad"),
            (StationAction.Edit, "Blad bewerken"),
            (StationAction.Note, "Kaartopmerking"),
            (StationAction.Delete, "Verwijderen"));
        AddGroup("Project & kaarten", "Gegevens, referenties, blokken en locatieoverzichten.",
            (StationAction.Project, "Projectgegevens"),
            (StationAction.EditAll, "Alle bladen bewerken"),
            (StationAction.References, "Kaartreferenties"),
            (StationAction.Blocks, "Trafoblokken"),
            (StationAction.Client, "Opdrachtgevers"),
            (StationAction.Signature, "Paraaflogo"));
        AddGroup("Uitvoer", "Maak en plot de A3-bladen en de totaalkaart.",
            (StationAction.Overview, "Totaal maken"),
            (StationAction.Plot, "Bladen plotten"));
        operations.Resize += (_, _) =>
        {
            var targetWidth = Math.Max(205, operations.ClientSize.Width - 28);
            foreach (var buttons in groups) buttons.Width = targetWidth;
        };
        root.Controls.Add(operations, 0, 6);
        root.Controls.Add(status, 0, 7);
        Controls.Add(root);

        search.TextChanged += (_, _) => Filter();
        refresh.Click += (_, _) => RefreshSheets();
        VisibleChanged += (_, _) => { if (Visible) RefreshSheets(); };
        UiTypography.Apply(this);
        RefreshSheets();
    }

    private StationSheet? Selected => sheets.SelectedItems.Count == 1
        ? sheets.SelectedItems[0].Tag as StationSheet
        : null;

    private void SizeColumns()
    {
        if (sheets.Columns.Count != 3) return;
        sheets.Columns[0].Width = Math.Max(95, sheets.ClientSize.Width - 140);
        sheets.Columns[1].Width = 75;
        sheets.Columns[2].Width = 60;
    }

    private void Filter()
    {
        if (loading) return;
        var selectedName = Selected?.Name;
        var find = search.Text.Trim();
        sheets.BeginUpdate();
        loading = true;
        try
        {
            sheets.Items.Clear();
            foreach (var item in cached.Where(x => find.Length == 0
                || x.Name.Contains(find, StringComparison.OrdinalIgnoreCase)
                || x.Trafo.Contains(find, StringComparison.OrdinalIgnoreCase)))
            {
                var kind = item.Alternative == 0 ? "VL"
                    : item.Alternative == 1 ? "AL" : "AL " + item.Alternative;
                var photo = photos.Contains(item.Name) ? "Ja" : "Nee";
                var row = new ListViewItem(new[] { item.Name, kind, photo }) { Tag = item };
                if (sheets.Items.Count % 2 == 1) row.BackColor = Surface;
                sheets.Items.Add(row);
                if (string.Equals(selectedName, item.Name, StringComparison.OrdinalIgnoreCase))
                    row.Selected = true;
            }
        }
        finally
        {
            loading = false;
            sheets.EndUpdate();
        }
        UpdateActions();
    }

    internal void RefreshSheets()
    {
        if (loading) return;
        var document = AcApp.DocumentManager.MdiActiveDocument;
        loading = true;
        try
        {
            cached.Clear();
            photos.Clear();
            overview = false;
            if (document == null)
            {
                summary.Text = "Geen actieve tekening";
                status.Text = "Open een DWG om stationslocaties te beheren.";
                return;
            }
            cached.AddRange(Stationslocaties.StationLocationSheets.List(document.Database));
            photos = Stationslocaties.StationLocationSheets.SheetsWithPhoto(document.Database, cached);
            overview = Stationslocaties.StationLocationSheets.HasOverview(document.Database);
            summary.Text = cached.Count + " locatiebladen  ·  " + photos.Count + " met foto"
                + "  ·  Totaal: " + (overview ? "aanwezig" : "nog niet gemaakt");
            status.Text = cached.Count == 0
                ? "Plaats eerst een voorkeurslocatie (VL)."
                : "Selecteer een blad en kies een actie. Dubbelklik om te openen.";
            if (actionButtons.TryGetValue(StationAction.Overview, out var total))
                total.Text = overview ? "Totaal vernieuwen" : "Totaal maken";
        }
        catch (Exception ex)
        {
            summary.Text = "Overzicht niet beschikbaar.";
            status.Text = ex.Message;
        }
        finally { loading = false; }
        Filter();
    }

    private void UpdateActions()
    {
        var hasDocument = AcApp.DocumentManager.MdiActiveDocument != null;
        var hasSelection = Selected != null;
        foreach (var button in actionButtons.Values) button.Enabled = hasDocument;
        foreach (var action in new[] { StationAction.Open, StationAction.Edit, StationAction.Note, StationAction.Delete })
            actionButtons[action].Enabled = hasDocument && hasSelection;
        foreach (var action in new[] { StationAction.EditAll, StationAction.Project })
            actionButtons[action].Enabled = hasDocument && cached.Count > 0;
        actionButtons[StationAction.Overview].Enabled = hasDocument && (cached.Count > 0 || overview);
        actionButtons[StationAction.Plot].Enabled = hasDocument && (cached.Count > 0 || overview);
        refresh.Enabled = hasDocument;
    }

    private void Execute(StationAction action)
    {
        var document = AcApp.DocumentManager.MdiActiveDocument;
        if (document == null) { status.Text = "Open eerst een tekening."; return; }
        if (Convert.ToInt32(AcApp.GetSystemVariable("CMDACTIVE")) != 0)
        {
            status.Text = "Rond eerst het actieve AutoCAD-commando af (of druk Esc).";
            return;
        }
        if (Plugin.PendingStationAction != null)
        {
            status.Text = "Er staat al een stationslocatieactie klaar.";
            return;
        }
        Plugin.PendingStationAction = new StationActionRequest(document, action, Selected?.Name);
        status.Text = "Actie gestart: " + actionButtons[action].Text;
        document.SendStringToExecute("_TBSTATIONACTIE ", true, false, false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) tips.Dispose();
        base.Dispose(disposing);
    }
}