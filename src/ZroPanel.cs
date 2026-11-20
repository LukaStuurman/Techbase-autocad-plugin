using System.Drawing;
using Font = System.Drawing.Font;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Techbase.Blocks;

internal enum ZroAction { Create, Open, Edit, Refresh, RefreshAll, References, Blocks }
internal sealed record ZroRequest(Document Document, ZroAction Action, string? Sheet);

/// <summary>Dezelfde opmaak en navigatie als de tab Stationslocaties.</summary>
internal sealed class ZroPanel : UserControl
{
    private readonly StationLocationHeader header = new();
    private readonly ListView sheets = new()
    {
        Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true,
        MultiSelect = false, HideSelection = false, BorderStyle = BorderStyle.None,
        HeaderStyle = ColumnHeaderStyle.None, AccessibleName = "ZRO-bladenlijst"
    };
    private readonly Label emptyState = new()
    {
        Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
        AutoSize = false, Padding = new Padding(16),
        Text = "Nog geen ZRO-bladen in deze tekening.\nKlik op ‘Nieuw ZRO-blad’ om een Pacto 25 of ander trafoblok te plaatsen."
    };
    private readonly Label selectedInfo = new()
    {
        Dock = DockStyle.Fill, AutoEllipsis = true, ForeColor = UiTheme.Muted,
        Padding = new Padding(0, 7, 0, 2)
    };
    private readonly Label status = new()
    {
        Dock = DockStyle.Fill, AutoEllipsis = true, ForeColor = UiTheme.Muted,
        Padding = new Padding(0, 6, 0, 0)
    };
    private readonly Dictionary<ZroAction, ThemedButton> actions = new();
    private readonly ThemedButton[] sectionTabs = new ThemedButton[3];
    private readonly Panel[] pages = new Panel[3];
    private readonly FlowLayoutPanel[] flows = new FlowLayoutPanel[3];
    private readonly Label[] descriptions = new Label[3];
    private List<string> available = new();
    private bool filtering;

    internal ZroPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = UiTheme.Canvas;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font(Typography.Family, 9);
        header.Configure("ZRO-bladen",
            "Grondaankoop per trafo · situatie 1:100 · overzicht 1:1000",
            "Nieuw ZRO-blad", "Zoek trafo of ZRO-blad…");

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6,
            Padding = new Padding(12, 9, 12, 10), Margin = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var row in new[]
        {
            new RowStyle(SizeType.Absolute, 180),
            new RowStyle(SizeType.Percent, 56),
            new RowStyle(SizeType.Absolute, 32),
            new RowStyle(SizeType.Absolute, 44),
            new RowStyle(SizeType.Percent, 44),
            new RowStyle(SizeType.Absolute, 28)
        }) layout.RowStyles.Add(row);
        void UpdateHeader()
        {
            header.UseCompactLayout(ClientSize.Width < 620);
            layout.RowStyles[0].Height = header.PreferredHeaderHeight;
        }
        SizeChanged += (_, _) => UpdateHeader();
        layout.Controls.Add(header, 0, 0);

        var listCard = new Panel
        {
            Dock = DockStyle.Fill, Padding = new Padding(1), Margin = new Padding(0, 0, 0, 3)
        };
        sheets.Columns.Add("Blad", 220);
        sheets.Resize += (_, _) => sheets.Columns[0].Width = Math.Max(100, sheets.ClientSize.Width - 4);
        sheets.SelectedIndexChanged += (_, _) => UpdateActions();
        sheets.DoubleClick += (_, _) => Execute(ZroAction.Open);
        listCard.Controls.Add(sheets);
        listCard.Controls.Add(emptyState);
        layout.Controls.Add(listCard, 0, 1);
        layout.Controls.Add(selectedInfo, 0, 2);

        var tabs = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty
        };
        for (var i = 0; i < 3; i++)
            tabs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
        var names = new[] { "ZRO-bladen", "Project & kaarten", "Uitvoer" };
        var icons = new[] { 6, 4, 10 };
        for (var i = 0; i < names.Length; i++)
        {
            var index = i;
            var button = new ThemedButton
            {
                Text = names[i], Glyph = icons[i], Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, i == 2 ? 0 : 5, 5),
                AccessibleName = names[i] + " acties"
            };
            sectionTabs[i] = button;
            tabs.Controls.Add(button, i, 0);
            button.Click += (_, _) => SelectSection(index);
        }
        layout.Controls.Add(tabs, 0, 3);

        var actionsHost = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        pages[0] = CreatePage(0, "Acties voor het geselecteerde ZRO-blad. Dubbelklik op een blad om het te openen.");
        pages[1] = CreatePage(1, "Dezelfde trafoblokbibliotheek en PDOK-kaarten als Stationslocaties; zonder KLIC.");
        pages[2] = CreatePage(2, "Werk de 1:100- en 1:1000-kaarten opnieuw bij na het verplaatsen van een trafo.");
        foreach (var page in pages) actionsHost.Controls.Add(page);

        AddAction(0, ZroAction.Open, "Open blad", 10);
        AddAction(0, ZroAction.Edit, "Bladgegevens wijzigen", 12);
        AddAction(1, ZroAction.Blocks, "Trafoblokken beheren", 0);
        AddAction(1, ZroAction.References, "Topografie en percelen", 3);
        AddAction(2, ZroAction.Refresh, "Geselecteerd blad bijwerken", 7);
        AddAction(2, ZroAction.RefreshAll, "Alle ZRO-bladen bijwerken", 18);
        layout.Controls.Add(actionsHost, 0, 4);
        layout.Controls.Add(status, 0, 5);
        Controls.Add(layout);

        header.NewLocation.Click += (_, _) => Execute(ZroAction.Create);
        header.Search.TextChanged += (_, _) => Filter();
        header.RefreshLink.LinkClicked += (_, _) => Reload();
        header.HelpLink.LinkClicked += (_, _) =>
            MessageBox.Show("Maak een ZRO-blad met een Pacto 25 AANKOOP of ander trafoblok. " +
                "De bovenste kaart heeft schaal 1:100; de onderste 1:1000. " +
                "Kadastrale gegevens komen van PDOK; gegevens van de eigenaar kun je invullen. " +
                "Voor de achtergrond gebruik je alleen Topografie en Percelen.",
                "TechBase ZRO-bladen", MessageBoxButtons.OK, MessageBoxIcon.Information);
        VisibleChanged += (_, _) => { if (Visible) Reload(); };

        UiTypography.Apply(this);
        UiTheme.Apply(this);
        header.ApplyAppearance();
        emptyState.BackColor = Color.White;
        emptyState.ForeColor = UiTheme.Muted;
        listCard.BackColor = UiTheme.Line;
        foreach (var page in pages) page.BackColor = Color.White;
        foreach (var flow in flows) flow.BackColor = Color.White;
        foreach (var description in descriptions) description.ForeColor = UiTheme.Muted;
        UpdateHeader();
        SelectSection(0);
        Reload();
    }

    private Panel CreatePage(int index, string explanation)
    {
        var page = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(8) };
        var info = new Label
        {
            Text = explanation, Dock = DockStyle.Top, Height = 44,
            AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft
        };
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight,
            WrapContents = true, AutoScroll = true, Margin = Padding.Empty
        };
        flow.SizeChanged += (_, _) => FitButtons(flow);
        page.Controls.Add(flow);
        page.Controls.Add(info);
        flows[index] = flow;
        descriptions[index] = info;
        return page;
    }

    private void AddAction(int section, ZroAction action, string title, int icon)
    {
        var button = new ThemedButton
        {
            Name = "Zro" + action, Text = title, Glyph = icon,
            Height = 42, MinimumSize = new Size(120, 40),
            TextAlign = ContentAlignment.MiddleLeft,
            AccessibleName = title, AutoSize = false,
            Margin = new Padding(0, 0, 7, 7)
        };
        button.Click += (_, _) => Execute(action);
        actions.Add(action, button);
        flows[section].Controls.Add(button);
    }

    private static void FitButtons(FlowLayoutPanel flow)
    {
        if (flow.IsDisposed || flow.ClientSize.Width < 90) return;
        var available = Math.Max(120, flow.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 14);
        var width = available >= 370 ? Math.Max(140, (available - 8) / 2) : available;
        foreach (var b in flow.Controls.OfType<ThemedButton>())
            if (b.Width != width) b.Width = width;
    }

    private void SelectSection(int index)
    {
        for (var i = 0; i < pages.Length; i++)
        {
            pages[i].Visible = i == index;
            if (!SystemInformation.HighContrast)
            {
                sectionTabs[i].BackColor = i == index ? UiTheme.SoftOrange : Color.White;
                sectionTabs[i].FlatAppearance.BorderColor = i == index ? UiTheme.Orange : UiTheme.Line;
            }
        }
        pages[index].BringToFront();
        FitButtons(flows[index]);
    }

    private string? Selected => sheets.SelectedItems.Count == 1 ? sheets.SelectedItems[0].Text : null;

    internal void Reload()
    {
        var document = AcApp.DocumentManager.MdiActiveDocument;
        try
        {
            available = document == null ? new() : ZroSheets.List(document.Database).ToList();
            header.Summary.Text = available.Count + " ZRO-bladen · boven 1:100 · onder 1:1000";
            status.Text = document == null ? "Open een DWG om ZRO-bladen te maken." :
                available.Count == 0 ? "Kies ‘Nieuw ZRO-blad’ om met Pacto 25 te beginnen." :
                "Selecteer een blad voor de beschikbare acties.";
            Filter();
        }
        catch (System.Exception ex)
        {
            status.Text = "ZRO-lijst kan niet worden geladen: " + ex.Message;
        }
    }

    private void Filter()
    {
        if (filtering) return;
        filtering = true;
        try
        {
            var old = Selected;
            sheets.BeginUpdate();
            try
            {
                sheets.Items.Clear();
                foreach (var name in available.Where(x =>
                    x.Contains(header.Search.Text.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    var item = sheets.Items.Add(name);
                    if (old == name) item.Selected = true;
                }
            }
            finally { sheets.EndUpdate(); }
            emptyState.Visible = sheets.Items.Count == 0;
            emptyState.Text = available.Count == 0
                ? "Nog geen ZRO-bladen in deze tekening.\nKies ‘Nieuw ZRO-blad’ om een trafoblok te plaatsen."
                : "Geen ZRO-bladen gevonden.\nWis je zoekterm.";
            if (emptyState.Visible) emptyState.BringToFront();
        }
        finally { filtering = false; }
        UpdateActions();
    }

    private void UpdateActions()
    {
        var hasDocument = AcApp.DocumentManager.MdiActiveDocument != null;
        var selected = Selected;
        header.NewLocation.Enabled = hasDocument;
        header.RefreshLink.Enabled = hasDocument;
        selectedInfo.Text = selected == null ? "Selecteer een blad om het te openen of te wijzigen." :
            selected + " · ZRO op schaal 1:100 / 1:1000";
        flows[0].Visible = hasDocument && selected != null;
        descriptions[0].Text = selected == null
            ? "Voor een nieuw ZRO-blad gebruik je de knop ‘Nieuw ZRO-blad’ bovenaan."
            : "Acties voor " + selected + ": open of wijzig de bladgegevens.";
        foreach (var button in actions.Values) button.Enabled = hasDocument;
        actions[ZroAction.Open].Enabled = actions[ZroAction.Edit].Enabled = hasDocument && selected != null;
        actions[ZroAction.Refresh].Enabled = hasDocument && selected != null;
        actions[ZroAction.RefreshAll].Enabled = hasDocument && available.Count > 0;
    }

    private void Execute(ZroAction action)
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc == null) { status.Text = "Open eerst een tekening."; return; }
        if (Convert.ToInt32(AcApp.GetSystemVariable("CMDACTIVE")) != 0)
        {
            status.Text = "Rond eerst het actieve AutoCAD-commando af (of druk Esc).";
            return;
        }
        if (Plugin.PendingZroAction != null) return;
        Plugin.PendingZroAction = new ZroRequest(doc, action, Selected);
        doc.SendStringToExecute("_TBZROACTIE ", true, false, false);
    }
}

internal static class ZroController
{
    internal static void Execute(Document doc, ZroRequest request)
    {
        switch (request.Action)
        {
            case ZroAction.Create:
                Create(doc);
                break;
            case ZroAction.Open:
                if (request.Sheet != null)
                {
                    LayoutManager.Current.CurrentLayout = request.Sheet;
                    doc.Editor.Regen();
                }
                break;
            case ZroAction.Edit:
                if (request.Sheet != null) Edit(doc, request.Sheet);
                break;
            case ZroAction.Refresh:
                if (request.Sheet != null) ZroSheets.Refresh(doc, request.Sheet);
                break;
            case ZroAction.RefreshAll:
                ZroSheets.RefreshAll(doc);
                break;
            case ZroAction.References:
                ZroMapReferencesDialog.Run(doc); // alleen topo/percelen, zonder KLIC
                break;
            case ZroAction.Blocks:
                new Stationslocaties.StationLocationController().ShowStationLocations(
                    Stationslocaties.StationLocationAction.Blocks);
                break;
        }
    }

    private static void Edit(Document doc, string sheet)
    {
        var id = ZroSheets.StationFor(doc.Database, sheet);
        if (id.IsNull) throw new InvalidOperationException("Deze ZRO-layout heeft geen gekoppeld station.");
        var footprint = ZroGeometry.Inspect(doc.Database, id);
        var current = ZroSheets.ReadFields(doc.Database, sheet);
        using var dialog = new ZroFieldsDialog(current, footprint.Center, footprint.BlockType, footprint.Area);
        if (Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(dialog) != DialogResult.OK ||
            dialog.Result is not { } fields) return;
        ZroSheets.Edit(doc, sheet, fields);
    }

    private static ObjectId PlaceNewTrafo(Document doc, StationBlockSelection choice)
    {
        var db = doc.Database;
        var definition = choice.ResolveDefinition(db);
        var scale = choice.ScaleFor(db, definition);
        LayoutManager.Current.CurrentLayout = "Model";
        using var preview = new BlockReference(Autodesk.AutoCAD.Geometry.Point3d.Origin, definition);
        preview.SetDatabaseDefaults(db);
        preview.ScaleFactors = scale;
        if (!new Stationslocaties.StationPlacementJig(preview).Pick(doc.Editor))
            return ObjectId.Null;

        using var tr = db.TransactionManager.StartTransaction();
        var model = (BlockTableRecord)tr.GetObject(
            SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);
        var layerName = choice.PaletteTool?.Layer;
        var layer = BlockInserter.DefaultLayer(db, tr,
            string.IsNullOrWhiteSpace(layerName) ? "01 - Station" : layerName);
        var station = new BlockReference(preview.Position, definition)
        {
            Rotation = preview.Rotation, ScaleFactors = preview.ScaleFactors,
            LayerId = layer
        };
        var id = model.AppendEntity(station);
        tr.AddNewlyCreatedDBObject(station, true);
        var record = (BlockTableRecord)tr.GetObject(definition, OpenMode.ForRead);
        foreach (ObjectId objectId in record)
        {
            if (tr.GetObject(objectId, OpenMode.ForRead) is not AttributeDefinition
                { Constant: false } attribute) continue;
            var value = new AttributeReference();
            value.SetAttributeFromBlock(attribute, station.BlockTransform);
            value.TextString = attribute.TextString;
            station.AttributeCollection.AppendAttribute(value);
            tr.AddNewlyCreatedDBObject(value, true);
        }
        tr.Commit();
        return id;
    }

    private static void EraseNewTrafo(Database db, ObjectId id)
    {
        if (id.IsNull || !id.IsValid || id.IsErased) return;
        using var tr = db.TransactionManager.StartTransaction();
        var station = (BlockReference)tr.GetObject(id, OpenMode.ForWrite);
        station.Erase();
        tr.Commit();
    }

    private static void Create(Document doc)
    {
        // Zelfde blokkiezer als Stationslocaties. Standaard is Pacto 25
        // AANKOOP; extra blokken uit de toolpalette/bibliotheek blijven kiezen.
        var source = StationBlockSelectionDialog.Choose(doc, allowExisting: true);
        if (source == null) return;
        var inserted = ObjectId.Null;
        var success = false;
        try
        {
            ObjectId stationId;
            if (source.UseExisting)
            {
                LayoutManager.Current.CurrentLayout = "Model";
                var options = new PromptEntityOptions(
                    "\nSelecteer een bestaand trafoblok voor dit ZRO-blad: ");
                options.SetRejectMessage("\nKies een INSERT/trafoblok.");
                options.AddAllowedClass(typeof(BlockReference), true);
                var picked = doc.Editor.GetEntity(options);
                if (picked.Status != PromptStatus.OK) return;
                stationId = picked.ObjectId;
            }
            else
            {
                stationId = inserted = PlaceNewTrafo(doc, source);
                if (stationId.IsNull) return;
            }

            var footprint = ZroGeometry.Inspect(doc.Database, stationId);
            var defaults = ZroSheets.Defaults.Copy();
            defaults.TrafoName = ""; // voorkom naam van de vorige trafo
            using var dialog = new ZroFieldsDialog(defaults, footprint.Center,
                source.UseExisting ? footprint.BlockType : source.DisplayName, footprint.Area);
            if (Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(dialog) != DialogResult.OK ||
                dialog.Result is not { } details) return;
            ZroSheets.Create(doc, stationId, details, footprint, dialog.Labels);
            success = true;
        }
        finally
        {
            // Plaatst de gebruiker een nieuwe AANKOOP-trafo, maar annuleert
            // die daarna de ZRO-gegevens? Laat dan geen losse insert achter.
            if (!success && !inserted.IsNull)
                EraseNewTrafo(doc.Database, inserted);
        }
    }
}
