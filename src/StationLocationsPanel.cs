using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using StationAction = Stationslocaties.StationLocationAction;

namespace Techbase.Blocks;

// De palette blijft modeless. AutoCAD-tekenacties worden in documentcontext uitgevoerd.
internal sealed record StationActionRequest(Document Document, StationAction Action, string? SheetName);

/// <summary>
/// Compacte werkruimte: één primaire actie, doorzoekbare bladlijst en contextuele bediening.
/// Projectbeheer en uitvoer zijn apart bereikbaar zonder een tweede hoofdvenster.
/// </summary>
internal sealed class StationLocationsPanel : UserControl
{
    private readonly ToolTip tips = new();
    private readonly ContextMenuStrip sheetMenu = new();
    private readonly List<Stationslocaties.StationSheet> cached = new();
    private HashSet<string> photos = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<StationAction, ThemedButton> actionButtons = new();

    private readonly Label summary = new() { Dock = DockStyle.Fill, AutoEllipsis = true };
    private readonly Label selectionInfo = new() { Dock = DockStyle.Fill, AutoEllipsis = true };
    private readonly Label status = new() { Dock = DockStyle.Fill, AutoEllipsis = true };
    private readonly Label subtitle = new()
    {
        Dock = DockStyle.Fill, Text = "Trafokaarten, voorkeurslocaties en alternatieven",
        AutoEllipsis = true
    };
    private readonly TextBox search = new()
    {
        Dock = DockStyle.Fill, PlaceholderText = "Zoek trafonummer of blad…",
        AccessibleName = "Zoek stationslocatie"
    };
    private readonly ListView sheets = new()
    {
        Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true,
        MultiSelect = false, HideSelection = false, HeaderStyle = ColumnHeaderStyle.Nonclickable,
        BorderStyle = BorderStyle.None, AccessibleName = "Locatiebladen"
    };
    private readonly ThemedButton newLocation = new()
    {
        Name = "StationNewLocation",
        Text = "+  Nieuwe locatie", Glyph = -1,
        Size = new Size(170, 39), AutoSize = false,
        Anchor = AnchorStyles.Right,
        AccessibleName = "Nieuwe stationslocatie plaatsen",
        TextAlign = ContentAlignment.MiddleCenter
    };
    // Native WinForms-buttons gebruiken standaard tekstweergave:
    // Bijwerken en Uitleg blijven leesbaar bij verschillende DPI-instellingen.
    private readonly Button refresh = new()
    {
        Name = "StationRefresh",
        Text = "Vernieuwen", Dock = DockStyle.Fill,
        AccessibleName = "Locatielijst bijwerken", TextAlign = ContentAlignment.MiddleCenter
    };
    private readonly Button help = new()
    {
        Name = "StationHelp",
        Text = "Uitleg", Dock = DockStyle.Fill,
        AccessibleName = "Informatie over stationslocaties", TextAlign = ContentAlignment.MiddleCenter
    };
    private readonly Label emptyLocations = new()
    {
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter,
        AutoSize = false,
        Padding = new Padding(20)
    };

    private readonly ThemedButton[] sectionTabs = new ThemedButton[3];
    private readonly Panel[] actionPages = new Panel[3];
    private readonly FlowLayoutPanel[] actionFlows = new FlowLayoutPanel[3];
    private readonly Label[] explanations = new Label[3];

    private int currentSection = -1;
    private bool hasOverview;
    private bool loading;

    internal StationLocationsPanel()
    {
        Dock = DockStyle.Fill;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = UiTheme.Canvas;
        Font = new Font(Typography.Family, 9);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8,
            Padding = new Padding(12, 9, 12, 10), Margin = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var style in new[]
        {
            new RowStyle(SizeType.Absolute, 62), // titel + primaire actie
            new RowStyle(SizeType.Absolute, 28), // samenvatting
            new RowStyle(SizeType.Absolute, 46), // zoeken
            new RowStyle(SizeType.Percent, 56),  // locatiebladen
            new RowStyle(SizeType.Absolute, 32), // geselecteerde locatie
            new RowStyle(SizeType.Absolute, 44), // navigatie in acties
            new RowStyle(SizeType.Percent, 44),  // contextafhankelijke acties
            new RowStyle(SizeType.Absolute, 28)  // status
        }) layout.RowStyles.Add(style);

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 178));
        var textHeader = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty
        };
        textHeader.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        textHeader.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        textHeader.Controls.Add(new Label
        {
            Text = "Stationslocaties", Dock = DockStyle.Fill,
            Font = new Font(Typography.Family, 17),
            AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        textHeader.Controls.Add(subtitle, 0, 1);
        header.Controls.Add(textHeader, 0, 0);
        newLocation.Margin = new Padding(5, 0, 0, 0);
        header.Controls.Add(newLocation, 1, 0);
        layout.Controls.Add(header, 0, 0);

        summary.Padding = new Padding(0, 3, 0, 3);
        layout.Controls.Add(summary, 0, 1);

        var toolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1,
            Margin = new Padding(0, 0, 0, 5)
        };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 119));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 75));
        var searchBox = new Panel
        {
            Dock = DockStyle.Fill, Padding = new Padding(10, 6, 8, 6),
            Margin = new Padding(0, 0, 5, 0)
        };
        searchBox.Controls.Add(search);
        toolbar.Controls.Add(searchBox, 0, 0);
        refresh.Margin = new Padding(0, 0, 5, 0);
        help.Margin = Padding.Empty;
        toolbar.Controls.Add(refresh, 1, 0);
        toolbar.Controls.Add(help, 2, 0);
        layout.Controls.Add(toolbar, 0, 2);

        var listCard = new Panel
        {
            Dock = DockStyle.Fill, Padding = new Padding(1),
            Margin = new Padding(0, 0, 0, 3)
        };
        sheets.Columns.Add("Blad", 190);
        sheets.Columns.Add("VL/AL", 66);
        sheets.Columns.Add("Foto", 52);
        sheets.Resize += (_, _) => SizeColumns();
        sheets.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Right) return;
            var row = sheets.GetItemAt(e.X, e.Y);
            if (row != null) { sheets.SelectedItems.Clear(); row.Selected = true; }
        };
        sheetMenu.Items.Add("Open blad", null, (_, _) => Execute(StationAction.Open));
        sheetMenu.Items.Add("Blad bewerken", null, (_, _) => Execute(StationAction.Edit));
        sheetMenu.Items.Add("Kaartopmerking plaatsen", null, (_, _) => Execute(StationAction.Note));
        sheetMenu.Items.Add(new ToolStripSeparator());
        sheetMenu.Items.Add("Locatie verwijderen", null, (_, _) => Execute(StationAction.Delete));
        sheetMenu.Opening += (_, e) => { if (Selected == null) e.Cancel = true; };
        sheets.ContextMenuStrip = sheetMenu;
        sheets.SelectedIndexChanged += (_, _) => { if (!loading) UpdateActions(); };
        sheets.DoubleClick += (_, _) =>
        {
            if (Selected != null) Execute(StationAction.Open);
        };
        listCard.Controls.Add(sheets);
        listCard.Controls.Add(emptyLocations);
        emptyLocations.BringToFront();
        layout.Controls.Add(listCard, 0, 3);

        selectionInfo.Padding = new Padding(0, 7, 0, 2);
        layout.Controls.Add(selectionInfo, 0, 4);

        var tabs = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty
        };
        for (var i = 0; i < 3; i++)
            tabs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
        var tabLabels = new[] { "Locaties", "Project", "Uitvoer" };
        var tabGlyphs = new[] { 6, 4, 10 };
        for (var i = 0; i < tabLabels.Length; i++)
        {
            var index = i;
            var button = new ThemedButton
            {
                Text = tabLabels[i], Glyph = tabGlyphs[i],
                Dock = DockStyle.Fill, Margin = new Padding(0, 0, i == 2 ? 0 : 5, 5),
                TextAlign = ContentAlignment.MiddleLeft,
                AccessibleName = tabLabels[i] + " acties"
            };
            button.Click += (_, _) => SelectSection(index);
            sectionTabs[i] = button;
            tabs.Controls.Add(button, i, 0);
        }
        layout.Controls.Add(tabs, 0, 5);

        var actionsHost = new Panel
        {
            Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 3)
        };
        actionPages[0] = MakeActionPage("Werk met de geselecteerde locatie. Voor nieuwe locaties gebruik je de oranje knop bovenaan.", 0);
        actionPages[1] = MakeActionPage("Projectgegevens, kaartbronnen en instellingen voor alle locaties.", 1);
        actionPages[2] = MakeActionPage("Maak het totaaloverzicht en exporteer de gewenste A3-bladen.", 2);
        foreach (var page in actionPages) actionsHost.Controls.Add(page);

        AddAction(0, StationAction.Open, "Open blad", 10);
        AddAction(0, StationAction.Edit, "Blad bewerken", 12);
        AddAction(0, StationAction.Note, "Kaartopmerking", 22);
        AddAction(0, StationAction.Delete, "Locatie verwijderen", 8, danger: true);

        AddAction(1, StationAction.Project, "Projectgegevens", 4);
        AddAction(1, StationAction.EditAll, "Alle bladen bewerken", 10);
        AddAction(1, StationAction.References, "Kaartreferenties", 3);
        AddAction(1, StationAction.Blocks, "Trafoblokken", 0);
        AddAction(1, StationAction.Client, "Opdrachtgevers", 2);
        AddAction(1, StationAction.Signature, "Paraaflogo", 12);

        AddAction(2, StationAction.Overview, "Totaal maken", 7);
        AddAction(2, StationAction.Plot, "Bladen plotten", 18);
        layout.Controls.Add(actionsHost, 0, 6);

        status.Padding = new Padding(0, 6, 0, 0);
        layout.Controls.Add(status, 0, 7);
        Controls.Add(layout);

        newLocation.Click += (_, _) => Execute(StationAction.Add);
        refresh.Click += (_, _) => RefreshSheets();
        help.Click += (_, _) =>
        {
            using var dialog = new Stationslocaties.StationHelpDialog();
            dialog.ShowDialog(this);
        };
        search.TextChanged += (_, _) => Filter();
        VisibleChanged += (_, _) => { if (Visible) RefreshSheets(); };

        UiTypography.Apply(this);
        UiTheme.Apply(this);
        if (!SystemInformation.HighContrast)
        {
            // Donkeroranje met witte tekst: beter contrast dan het oude oranje vlak.
            newLocation.BackColor = Color.FromArgb(184, 69, 10);
            newLocation.ForeColor = Color.White;
            newLocation.FlatAppearance.BorderColor = newLocation.BackColor;
            newLocation.FlatAppearance.MouseOverBackColor = Color.FromArgb(158, 57, 8);
            newLocation.FlatAppearance.MouseDownBackColor = Color.FromArgb(133, 46, 6);
            actionButtons[StationAction.Delete].ForeColor = UiTheme.Danger;
        }
        newLocation.Font = new Font(Typography.Family, 10f, FontStyle.Bold);
        foreach (var button in new[] { refresh, help })
        {
            button.FlatStyle = FlatStyle.Flat;
            button.BackColor = Color.White;
            button.ForeColor = UiTheme.Ink;
            button.FlatAppearance.BorderColor = UiTheme.Line;
            button.FlatAppearance.MouseOverBackColor = UiTheme.SoftOrange;
            button.Padding = new Padding(5, 0, 5, 0);
            button.Font = new Font(Typography.Family, 9.5f);
        }
        emptyLocations.BackColor = Color.White;
        emptyLocations.ForeColor = UiTheme.Muted;
        foreach (var page in actionPages) page.BackColor = Color.White;
        foreach (var flow in actionFlows) flow.BackColor = Color.White;
        searchBox.BackColor = UiTheme.Input;
        search.BackColor = UiTheme.Input;
        listCard.BackColor = UiTheme.Line;
        summary.ForeColor = UiTheme.Muted;
        subtitle.ForeColor = UiTheme.Muted;
        selectionInfo.ForeColor = UiTheme.Muted;
        status.ForeColor = UiTheme.Muted;
        foreach (var explanation in explanations) explanation.ForeColor = UiTheme.Muted;
        newLocation.Margin = new Padding(5, 0, 0, 0);
        refresh.Margin = new Padding(0, 0, 5, 0);
        help.Margin = Padding.Empty;
        foreach (var flow in actionFlows) FitButtons(flow);
        SelectSection(0);
        RefreshSheets();
    }

    private Panel MakeActionPage(string explanation, int index)
    {
        var page = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8), BackColor = Color.White };
        var label = new Label
        {
            Text = explanation, Dock = DockStyle.Top, Height = 47,
            AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft
        };
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true, AutoScroll = true, Padding = Padding.Empty,
            Margin = Padding.Empty, BackColor = Color.White
        };
        flow.SizeChanged += (_, _) => FitButtons(flow);
        page.Controls.Add(flow);
        page.Controls.Add(label);
        explanations[index] = label;
        actionFlows[index] = flow;
        return page;
    }

    private void AddAction(int section, StationAction action, string label, int glyph, bool danger = false)
    {
        var button = new ThemedButton
        {
            Name = "Station" + action, Text = label, Glyph = glyph,
            Height = 42, MinimumSize = new Size(115, 40),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = false, AccessibleName = label
        };
        if (danger) button.ForeColor = UiTheme.Danger;
        button.Margin = new Padding(0, 0, 7, 7);
        button.Click += (_, _) => Execute(action);
        actionButtons.Add(action, button);
        actionFlows[section].Controls.Add(button);
        tips.SetToolTip(button, label);
    }

    private static void FitButtons(FlowLayoutPanel flow)
    {
        if (flow.IsDisposed || flow.ClientSize.Width < 90) return;
        var width = Math.Max(115, flow.ClientSize.Width - flow.Padding.Horizontal -
            SystemInformation.VerticalScrollBarWidth - 14);
        var columns = width >= 370 ? 2 : 1;
        var target = columns == 2 ? Math.Max(140, (width - 8) / 2) : width;
        foreach (var button in flow.Controls.OfType<ThemedButton>())
            if (button.Width != target) button.Width = target;
    }

    private void SelectSection(int index)
    {
        if (index < 0 || index >= actionPages.Length) return;
        currentSection = index;
        for (var i = 0; i < actionPages.Length; i++)
        {
            actionPages[i].Visible = i == index;
            var tab = sectionTabs[i];
            if (!SystemInformation.HighContrast)
            {
                tab.BackColor = i == index ? UiTheme.SoftOrange : Color.White;
                tab.FlatAppearance.BorderColor = i == index ? UiTheme.Orange : UiTheme.Line;
            }
            tab.AccessibleDescription = i == index ? "Geselecteerde groep" : "Toon " + tab.Text;
            tab.Invalidate();
        }
        actionPages[index].BringToFront();
        FitButtons(actionFlows[index]);
    }

    private Stationslocaties.StationSheet? Selected => sheets.SelectedItems.Count == 1
        ? sheets.SelectedItems[0].Tag as Stationslocaties.StationSheet
        : null;

    private void SizeColumns()
    {
        if (sheets.Columns.Count != 3) return;
        sheets.Columns[0].Width = Math.Max(96, sheets.ClientSize.Width - 144);
        sheets.Columns[1].Width = 70;
        sheets.Columns[2].Width = 54;
    }

    private void Filter()
    {
        if (loading) return;
        var previous = Selected?.Name;
        var find = search.Text.Trim();
        loading = true;
        sheets.BeginUpdate();
        try
        {
            sheets.Items.Clear();
            foreach (var item in cached.Where(x => find.Length == 0 ||
                x.Name.Contains(find, StringComparison.OrdinalIgnoreCase) ||
                x.Trafo.Contains(find, StringComparison.OrdinalIgnoreCase)))
            {
                var type = item.Alternative == 0 ? "VL" :
                    item.Alternative == 1 ? "AL" : "AL " + item.Alternative;
                var row = new ListViewItem(new[] {
                    item.Name, type, photos.Contains(item.Name) ? "Ja" : "Nee"
                }) { Tag = item };
                if (sheets.Items.Count % 2 == 1)
                    row.BackColor = UiTheme.Canvas;
                sheets.Items.Add(row);
                if (string.Equals(previous, item.Name, StringComparison.OrdinalIgnoreCase))
                    row.Selected = true;
            }
        }
        finally
        {
            sheets.EndUpdate();
            loading = false;
        }
        // Een lege lijst krijgt uitleg in plaats van een volledig wit vlak.
        emptyLocations.Text = cached.Count == 0
            ? "Nog geen stationslocaties in deze tekening.\nKies ‘Nieuwe locatie’ om een voorkeurslocatie te plaatsen."
            : "Geen locaties gevonden.\nWis de zoekterm om alle bladen te zien.";
        emptyLocations.Visible = sheets.Items.Count == 0;
        if (emptyLocations.Visible) emptyLocations.BringToFront();
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
            hasOverview = false;
            if (document != null)
            {
                cached.AddRange(Stationslocaties.StationLocationSheets.List(document.Database));
                photos = Stationslocaties.StationLocationSheets.SheetsWithPhoto(document.Database, cached);
                hasOverview = Stationslocaties.StationLocationSheets.HasOverview(document.Database);
                var vl = cached.Count(x => x.Alternative == 0);
                var al = cached.Count - vl;
                summary.Text = vl + " VL  ·  " + al + " AL  ·  " + photos.Count +
                    " met foto  ·  Totaal " + (hasOverview ? "aanwezig" : "ontbreekt");
                status.Text = cached.Count == 0
                    ? "Begin met Nieuwe locatie om een VL te plaatsen."
                    : "Dubbelklik op een blad om het te openen.";
            }
            else
            {
                summary.Text = "Geen actieve tekening";
                status.Text = "Open een DWG om locaties te beheren.";
            }
            actionButtons[StationAction.Overview].Text =
                hasOverview ? "Totaal vernieuwen" : "Totaal maken";
        }
        catch (Exception ex)
        {
            summary.Text = "Overzicht kon niet worden geladen";
            status.Text = ex.Message;
        }
        finally { loading = false; }
        Filter();
    }

    private void UpdateActions()
    {
        if (loading) return;
        var hasDocument = AcApp.DocumentManager.MdiActiveDocument != null;
        var chosen = Selected;
        if (chosen == null)
        {
            selectionInfo.Text = cached.Count == 0
                ? "Nog geen locaties in deze tekening."
                : "Selecteer een locatie om de bladacties te gebruiken.";
        }
        else
        {
            var type = chosen.Alternative == 0 ? "voorkeurslocatie" :
                "alternatieve locatie" + (chosen.Alternative > 1 ? " " + chosen.Alternative : "");
            selectionInfo.Text = chosen.Trafo + "  ·  " + type + "  ·  foto " +
                (photos.Contains(chosen.Name) ? "aanwezig" : "ontbreekt");
        }
        // Zolang niets geselecteerd is, verberg in plaats van vier onleesbare
        // grijze acties te tonen. De knop Nieuwe locatie blijft altijd zichtbaar.
        actionFlows[0].Visible = chosen != null && hasDocument;
        explanations[0].Text = chosen == null
            ? (cached.Count == 0 ? "Begin met de knop ‘Nieuwe locatie’ bovenaan." :
               "Selecteer eerst een locatie in de lijst om dit blad te bewerken.")
            : "Acties voor " + chosen.Name + ": open het blad, wijzig de gegevens of plaats een kaartopmerking.";
        newLocation.Enabled = hasDocument;
        refresh.Enabled = hasDocument;
        foreach (var button in actionButtons.Values) button.Enabled = hasDocument;
        foreach (var action in new[] {
            StationAction.Open, StationAction.Edit, StationAction.Note, StationAction.Delete
        }) actionButtons[action].Enabled = hasDocument && chosen != null;
        foreach (var action in new[] { StationAction.Project, StationAction.EditAll })
            actionButtons[action].Enabled = hasDocument && cached.Count > 0;
        foreach (var action in new[] { StationAction.Overview, StationAction.Plot })
            actionButtons[action].Enabled = hasDocument && (cached.Count > 0 || hasOverview);
    }

    private void Execute(StationAction action)
    {
        var document = AcApp.DocumentManager.MdiActiveDocument;
        if (document == null) { status.Text = "Open eerst een tekening."; return; }
        if ((action is StationAction.Open or StationAction.Edit or StationAction.Note or StationAction.Delete)
            && Selected == null)
        {
            status.Text = "Selecteer eerst een locatie.";
            return;
        }
        if (Convert.ToInt32(AcApp.GetSystemVariable("CMDACTIVE")) != 0)
        {
            status.Text = "Rond eerst het actieve AutoCAD-commando af (of druk Esc).";
            return;
        }
        if (Plugin.PendingStationAction != null)
        {
            status.Text = "Er is al een stationsactie gestart.";
            return;
        }
        Plugin.PendingStationAction = new StationActionRequest(document, action, Selected?.Name);
        status.Text = "Gestart: " + (action == StationAction.Add ? newLocation.Text : actionButtons[action].Text);
        document.SendStringToExecute("_TBSTATIONACTIE ", true, false, false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { tips.Dispose(); sheetMenu.Dispose(); }
        base.Dispose(disposing);
    }
}
