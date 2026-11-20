using System.Drawing;
using Font = System.Drawing.Font;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Techbase.Blocks;

internal enum ZroAction { Create, Open, Edit, Refresh, RefreshAll, References }
internal sealed record ZroRequest(Document Document, ZroAction Action, string? Sheet);

internal sealed class ZroPanel : UserControl
{
    private readonly ListView sheets = new()
    {
        Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true,
        MultiSelect = false, HideSelection = false, BorderStyle = BorderStyle.None
    };
    private readonly Label summary = new()
    {
        Dock = DockStyle.Fill, AutoEllipsis = true,
        Text = "ZRO-bladen op basis van het Bakel-voorbeeld"
    };
    private readonly Label status = new()
    {
        Dock = DockStyle.Fill, AutoEllipsis = true,
        Text = "Kies een trafoblok om een ZRO-blad te maken."
    };
    private readonly TextBox search = new()
    {
        Dock = DockStyle.Fill, PlaceholderText = "Zoek op trafo of ZRO-blad…"
    };
    private readonly ThemedButton create = new()
    {
        Text = "Nieuw ZRO-blad", Dock = DockStyle.Fill,
        AccessibleName = "Selecteer een trafoblok om een ZRO-blad te maken"
    };
    private readonly ThemedButton open = new()
    {
        Text = "Open blad", Dock = DockStyle.Fill
    };
    private readonly ThemedButton edit = new()
    {
        Text = "Bladgegevens wijzigen", Dock = DockStyle.Fill
    };
    private readonly ThemedButton refresh = new()
    {
        Text = "Blad bijwerken", Dock = DockStyle.Fill
    };
    private readonly ThemedButton references = new()
    {
        Text = "Kaartreferenties (PDOK/KLIC)", Dock = DockStyle.Fill
    };
    private readonly ThemedButton all = new()
    {
        Text = "Alle ZRO-bladen bijwerken", Dock = DockStyle.Fill
    };
    private readonly Label notes = new()
    {
        Text = "Bovenste kaart 1:100   ·   Onderste kaart 1:1000\n" +
               "Kadaster/straatadres uit PDOK, eigendom handmatig controleren.",
        AutoSize = true, Dock = DockStyle.Fill, ForeColor = UiTheme.Muted
    };
    private List<string> available = new();

    internal ZroPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = UiTheme.Canvas;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font(Typography.Family, 9);
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7,
            Padding = new Padding(12, 9, 12, 11), Margin = Padding.Empty
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 51));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 212));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        grid.Controls.Add(new Label
        {
            Text = "ZRO-bladen", Font = new Font(Typography.Family, 17),
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        notes.Padding = new Padding(2, 0, 0, 2);
        grid.Controls.Add(notes, 0, 1);

        var tools = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        tools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tools.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 172));
        tools.Controls.Add(search, 0, 0);
        create.Margin = new Padding(5, 1, 0, 1);
        tools.Controls.Add(create, 1, 0);
        grid.Controls.Add(tools, 0, 2);

        sheets.Columns.Add("ZRO-blad", 270);
        sheets.Resize += (_, _) => sheets.Columns[0].Width = Math.Max(90, sheets.ClientSize.Width - 2);
        sheets.SelectedIndexChanged += (_, _) => UpdateActions();
        sheets.DoubleClick += (_, _) => Execute(ZroAction.Open);
        grid.Controls.Add(sheets, 0, 3);
        summary.Padding = new Padding(2, 5, 0, 0);
        grid.Controls.Add(summary, 0, 4);

        var buttons = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4,
            Padding = new Padding(0, 6, 0, 0), Margin = Padding.Empty
        };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (int i = 0; i < 4; i++) buttons.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        buttons.Controls.Add(open, 0, 0);
        buttons.Controls.Add(edit, 1, 0);
        buttons.Controls.Add(refresh, 0, 1);
        buttons.Controls.Add(all, 1, 1);
        buttons.Controls.Add(references, 0, 2);
        buttons.SetColumnSpan(references, 2);
        var instruction = new Label
        {
            Text = "Een nieuwe DWG? Kies bij de eerste ZRO-aanmaak de voorbeeld-DXF met blad TRAFO-10.",
            Dock = DockStyle.Fill, AutoEllipsis = true, ForeColor = UiTheme.Muted
        };
        buttons.Controls.Add(instruction, 0, 3);
        buttons.SetColumnSpan(instruction, 2);
        foreach (var button in new[] { open, edit, refresh, all, references })
            button.Margin = new Padding(0, 3, 6, 3);
        grid.Controls.Add(buttons, 0, 5);
        grid.Controls.Add(status, 0, 6);
        Controls.Add(grid);

        create.Click += (_, _) => Execute(ZroAction.Create);
        open.Click += (_, _) => Execute(ZroAction.Open);
        edit.Click += (_, _) => Execute(ZroAction.Edit);
        refresh.Click += (_, _) => Execute(ZroAction.Refresh);
        all.Click += (_, _) => Execute(ZroAction.RefreshAll);
        references.Click += (_, _) => Execute(ZroAction.References);
        search.TextChanged += (_, _) => Filter();
        VisibleChanged += (_, _) => { if (Visible) Reload(); };
        UiTypography.Apply(this);
        UiTheme.Apply(this);
        UiTheme.Primary(create);
        notes.ForeColor = summary.ForeColor = status.ForeColor = UiTheme.Muted;
        Reload();
    }

    private string? Selected => sheets.SelectedItems.Count == 1 ?
        sheets.SelectedItems[0].Text : null;

    internal void Reload()
    {
        var document = AcApp.DocumentManager.MdiActiveDocument;
        try
        {
            available = document == null ? new() : ZroSheets.List(document.Database).ToList();
            summary.Text = available.Count + " ZRO-bladen · boven 1:100 / onder 1:1000";
            status.Text = document == null ? "Open eerst een DWG." :
                available.Count == 0 ? "Selecteer een trafo voor het eerste ZRO-blad." :
                "Dubbelklik op een blad om het te openen.";
            Filter();
        }
        catch (Exception ex) { status.Text = ex.Message; }
    }

    private void Filter()
    {
        var old = Selected;
        sheets.BeginUpdate();
        try
        {
            sheets.Items.Clear();
            foreach (var name in available.Where(x =>
                x.Contains(search.Text.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                var item = sheets.Items.Add(name);
                if (old == name) item.Selected = true;
            }
        }
        finally { sheets.EndUpdate(); }
        UpdateActions();
    }

    private void UpdateActions()
    {
        var hasDocument = AcApp.DocumentManager.MdiActiveDocument != null;
        create.Enabled = hasDocument; references.Enabled = hasDocument;
        open.Enabled = edit.Enabled = refresh.Enabled = hasDocument && Selected != null;
        all.Enabled = hasDocument && available.Count > 0;
    }

    private void Execute(ZroAction action)
    {
        var document = AcApp.DocumentManager.MdiActiveDocument;
        if (document == null) { status.Text = "Open eerst een tekening."; return; }
        if (Convert.ToInt32(AcApp.GetSystemVariable("CMDACTIVE")) != 0)
        {
            status.Text = "Rond eerst het actieve AutoCAD-commando af.";
            return;
        }
        if (Plugin.PendingZroAction != null) return;
        Plugin.PendingZroAction = new ZroRequest(document, action, Selected);
        document.SendStringToExecute("_TBZROACTIE ", true, false, false);
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
                new Stationslocaties.StationLocationController().ShowStationLocations(Stationslocaties.StationLocationAction.References);
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
