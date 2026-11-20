using System.Drawing;
using System.IO;
using Font = System.Drawing.Font;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Techbase.Blocks.Zro;

namespace Techbase.Blocks;

/// <summary>Een aparte ZRO-werkruimte, onafhankelijk van Stationslocaties.</summary>
internal sealed class ZroPanel : UserControl
{
    private readonly Dictionary<string, TextBox> inputs = new(StringComparer.Ordinal);
    private readonly Label stationStatus = new() { AutoSize = true, ForeColor = UiTheme.Muted };
    private readonly Label lookupStatus = new() { AutoSize = true, ForeColor = UiTheme.Muted };
    private readonly Label progress = new() { AutoSize = true, ForeColor = UiTheme.Muted };
    private readonly ListBox sheets = new() { Dock = DockStyle.Fill, Height = 120 };
    private readonly Button pick = new() { Text = "1  Selecteer trafoblok", Height = 38, AutoSize = true };
    private readonly Button lookup = new() { Text = "PDOK-gegevens ophalen", Height = 35, AutoSize = true };
    private readonly Button create = new() { Text = "2  ZRO-blad maken / bijwerken", Height = 42, AutoSize = true };
    private readonly Button template = new() { Text = "ZRO-sjabloon instellen", Height = 35, AutoSize = true };
    private readonly Button maps = new() { Text = "Topo + percelen als XREF", Height = 35, AutoSize = true };
    private readonly Button open = new() { Text = "Open geselecteerd ZRO-blad", Height = 35, AutoSize = true };
    private readonly Label templateStatus = new() { AutoSize = true, ForeColor = UiTheme.Muted };
    private ObjectId stationId = ObjectId.Null;
    private Document? stationDocument;
    private Point3d stationPoint = Point3d.Origin;
    private ZroFootprint? footprint;
    private ZroPdokResult lastPdok = new("", "", "", null, [], "");
    private bool lookupInProgress;

    internal ZroPanel()
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        Dock = DockStyle.Fill;
        BackColor = UiTheme.Canvas;
        Font = new Font(Typography.Family, 9);
        var area = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(12, 8, 12, 14) };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1, Margin = Padding.Empty, Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        area.Controls.Add(layout);
        Controls.Add(area);

        void Add(Control control)
        {
            control.Dock = DockStyle.Top;
            control.Margin = new Padding(0, 3, 0, 5);
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(control, 0, layout.RowCount++);
        }
        void Heading(string title, string subtitle)
        {
            Add(new Label { Text = title, AutoSize = true,
                Font = new Font(Typography.Family, 15), Padding = new Padding(0, 8, 0, 3) });
            Add(new Label { Text = subtitle, AutoSize = true, ForeColor = UiTheme.Muted,
                MaximumSize = new Size(620, 0) });
        }
        void Section(string title)
        {
            Add(new Label { Text = title.ToUpperInvariant(), AutoSize = true,
                Font = new Font(Typography.Family, 10, FontStyle.Bold),
                ForeColor = UiTheme.Ink, Padding = new Padding(0, 14, 0, 6) });
        }
        void Field(string title, string property, string hint = "")
        {
            var label = new Label { Text = title, AutoSize = true, Margin = new Padding(0, 6, 0, 2) };
            Add(label);
            var input = new TextBox
            {
                Name = "Zro" + property, AccessibleName = title,
                Dock = DockStyle.Top, Height = 30,
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = hint
            };
            inputs[property] = input;
            Add(input);
        }

        Heading("ZRO-bladen", "Automatisch A4-blad per trafo volgens het ZRO-model. Boven 1:100, onder 1:1000.");
        Add(pick);
        Add(stationStatus);
        Add(lookup);
        Add(lookupStatus);

        Section("Project en stationsnummer");
        Field("Projectnummer / code", nameof(ZroFields.ProjectCode), "bijvoorbeeld G.14031");
        Field("Trafonaam", nameof(ZroFields.StationName), "bijvoorbeeld Trafo 10");
        Field("MSR.NR. (stationsnummer)", nameof(ZroFields.StationNumber));
        Field("Tekeningnummer (zelf invullen)", nameof(ZroFields.DrawingNumber));
        Field("Getekend door — voorletters en achternaam", nameof(ZroFields.Drafter));
        Field("Afdeling", nameof(ZroFields.Department), "zelf invullen");
        Field("E-mail", nameof(ZroFields.Email), "zelf invullen");

        Section("Adres en kadastrale gegevens");
        Add(new Label { AutoSize = true,
            Text = "Adres, plaats, gemeente en perceel worden waar mogelijk vanuit PDOK ingevuld. Controleer de juiste grond bij een grens.",
            MaximumSize = new Size(640, 0), ForeColor = UiTheme.Muted });
        Field("Adres (straatnaam, huisnummer en letter)", nameof(ZroFields.Address));
        Field("Plaatsnaam", nameof(ZroFields.Place));
        Field("Gemeente (reguliere gemeente)", nameof(ZroFields.Municipality));
        Field("Kad.Gem (kadastrale gemeente)", nameof(ZroFields.CadastralMunicipality));
        Field("Sectie (letter)", nameof(ZroFields.Section));
        Field("Perceelnummer", nameof(ZroFields.ParcelNumber));

        Section("Eigendom en gebruik");
        Add(new Label { Text = "Eigenaargegevens zijn geen openbare PDOK-kaartgegevens en worden zelf ingevuld.",
            AutoSize = true, MaximumSize = new Size(640, 0), ForeColor = UiTheme.Muted });
        Field("EIGENDOMR1 — eigenaar", nameof(ZroFields.OwnerName));
        Field("EIGENDOMR2 — straat en huisnummer", nameof(ZroFields.OwnerAddress));
        Field("EIGENDOMR3 — postcode en plaats", nameof(ZroFields.OwnerPostcodeCity));
        Field("GEBRUIKERR1 (optioneel)", nameof(ZroFields.UserName));
        Field("GEBRUIKERR2 (optioneel)", nameof(ZroFields.UserAddress));
        Field("GEBRUIKERR3 (optioneel)", nameof(ZroFields.UserPostcodeCity));

        Section("Overige gegevens en sjabloon");
        Field("Datum", nameof(ZroFields.Date));
        Field("Opmerking", nameof(ZroFields.Notes));
        Add(template);
        Add(templateStatus);
        Add(new Label { Text = "Eenmalig de ZRO-model-DXF kiezen (TRAFO-10 of TRAFO-4). TRAFO-3 wordt niet als sjabloon gebruikt.",
            AutoSize = true, ForeColor = UiTheme.Muted, MaximumSize = new Size(640, 0) });

        Section("Blad aanmaken en kaartlagen");
        Add(create);
        Add(maps);
        Add(progress);
        Section("Bestaande ZRO-bladen");
        Add(sheets);
        Add(open);

        var defaults = ZroFields.LoadDefaults();
        Populate(defaults);
        templateStatus.Text = File.Exists(defaults.TemplatePath)
            ? "Sjabloon: " + Path.GetFileName(defaults.TemplatePath)
            : "Kies het meegeleverde ZRO-model als sjabloon.";

        pick.Click += (_, _) =>
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) { progress.Text = "Open eerst een DWG."; return; }
            doc.SendStringToExecute("_TBZROKIES ", true, false, false);
        };
        lookup.Click += async (_, _) => await LookupPdok();
        create.Click += (_, _) =>
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null || doc != stationDocument || stationId.IsNull)
            {
                progress.Text = "Selecteer eerst een trafoblok in deze tekening.";
                return;
            }
            try
            {
                var values = ReadValues();
                if (!File.Exists(values.TemplatePath) && !doc.Name.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase))
                {
                    progress.Text = "Stel eerst de ZRO-model-DXF in.";
                    return;
                }
                Plugin.PendingZroCreate = new ZroCreateRequest(doc, stationId, values, lastPdok);
                doc.SendStringToExecute("_TBZROMAAK ", true, false, false);
                progress.Text = "ZRO-blad wordt aangemaakt of bijgewerkt...";
            }
            catch (Exception ex) { progress.Text = ex.Message; }
        };
        template.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "AutoCAD DXF (*.dxf)|*.dxf",
                Title = "Kies een ZRO-voorbeeld-DXF met TRAFO-10 of TRAFO-4"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            var values = ReadValues();
            values.TemplatePath = dlg.FileName;
            values.SaveDefaults();
            templateStatus.Text = "Sjabloon: " + Path.GetFileName(dlg.FileName);
            progress.Text = "ZRO-sjabloon ingesteld voor volgende bladen.";
        };
        maps.Click += (_, _) => QueueMaps();
        open.Click += (_, _) =>
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null || sheets.SelectedItem is not string chosen) return;
            Plugin.PendingZroOpen = new ZroOpenRequest(doc, chosen);
            doc.SendStringToExecute("_TBZROOPEN ", true, false, false);
        };
        VisibleChanged += (_, _) => { if (Visible) RefreshSheets(); };

        UiTypography.Apply(this);
        if (!SystemInformation.HighContrast)
        {
            UiTheme.Primary(create);
            UiTheme.Primary(pick);
        }
        RefreshSheets();
    }

    private void Populate(ZroFields values)
    {
        foreach (var pair in inputs)
        {
            var value = typeof(ZroFields).GetProperty(pair.Key)?.GetValue(values) as string;
            pair.Value.Text = value ?? "";
        }
    }

    private ZroFields ReadValues()
    {
        var defaults = ZroFields.LoadDefaults();
        var result = new ZroFields { TemplatePath = defaults.TemplatePath };
        foreach (var pair in inputs)
            typeof(ZroFields).GetProperty(pair.Key)?.SetValue(result, pair.Value.Text.Trim());
        return result;
    }

    internal void ChooseStation(Document document)
    {
        var opts = new PromptEntityOptions("\nKies de trafo als AutoCAD-block: ");
        opts.SetRejectMessage("\nSelecteer een ingevoegd block (INSERT).");
        opts.AddAllowedClass(typeof(BlockReference), false);
        var response = document.Editor.GetEntity(opts);
        if (response.Status != PromptStatus.OK) return;
        stationId = response.ObjectId;
        stationDocument = document;
        using (var tr = document.Database.TransactionManager.StartTransaction())
        {
            var station = (BlockReference)tr.GetObject(stationId, OpenMode.ForRead);
            stationPoint = station.Position;
            footprint = ZroFootprint.FromStation(tr, station);
        }
        stationStatus.Text = "Gekozen: " + footprint.BlockType + "  |  " +
            Math.Round(footprint.Area).ToString("0") + " m²" +
            (footprint.Estimated ? " (geschat — controleer voetafdruk)" : " (grootste rechthoek)");
        progress.Text = "Trafoblok geselecteerd. Vul de projectgegevens in.";
        _ = LookupPdok();
    }

    private async Task LookupPdok()
    {
        if (lookupInProgress || stationId.IsNull || stationDocument == null)
        {
            lookupStatus.Text = "Selecteer eerst een trafo.";
            return;
        }
        lookupInProgress = true;
        lookup.Enabled = false;
        lookupStatus.Text = "Adres en kadastrale gegevens ophalen bij PDOK…";
        try
        {
            var target = stationPoint;
            var pdok = await Task.Run(() => ZroCadaster.Lookup(target));
            if (IsDisposed || !IsHandleCreated) return;
            lastPdok = pdok;
            void SetIfEmpty(string property, string value)
            {
                if (inputs.TryGetValue(property, out var control) &&
                    string.IsNullOrWhiteSpace(control.Text) && value.Length != 0)
                    control.Text = value;
            }
            SetIfEmpty(nameof(ZroFields.Address), pdok.Address);
            SetIfEmpty(nameof(ZroFields.Place), pdok.Place);
            SetIfEmpty(nameof(ZroFields.Municipality), pdok.Municipality);
            if (pdok.StationParcel is { } parcel)
            {
                SetIfEmpty(nameof(ZroFields.CadastralMunicipality), parcel.Municipality);
                SetIfEmpty(nameof(ZroFields.Section), parcel.Section);
                SetIfEmpty(nameof(ZroFields.ParcelNumber), parcel.Number);
            }
            lookupStatus.Text = (pdok.StationParcel == null ? "Geen bevestigd perceel. " :
                "Perceel: " + pdok.StationParcel.Label + ". ") +
                pdok.Warning + " Controleer de ingevulde gegevens.";
        }
        catch (Exception ex) { lookupStatus.Text = "PDOK niet bereikbaar: " + ex.Message; }
        finally { lookup.Enabled = true; lookupInProgress = false; }
    }

    private void QueueMaps()
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc == null || stationId.IsNull || doc != stationDocument)
        {
            progress.Text = "Selecteer eerst een trafoblok.";
            return;
        }
        using var directory = new FolderBrowserDialog { Description = "Kies uitvoermap voor Topo en Percelen XREF" };
        if (directory.ShowDialog() != DialogResult.OK) return;
        try
        {
            var area = Stationslocaties.ParcelArea.FromCorners(
                stationPoint + new Vector3d(-100, -100, 0),
                stationPoint + new Vector3d(100, 100, 0));
            Stationslocaties.StationReferences.QueueTopoPercelen(doc, directory.SelectedPath, area);
            progress.Text = "Topo en Percelen worden als losse DWG-XREF's voorbereid.";
        }
        catch (Exception ex) { progress.Text = ex.Message; }
    }

    internal void RefreshSheets()
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        var selected = sheets.SelectedItem as string;
        sheets.Items.Clear();
        if (doc != null)
            foreach (var name in ZroSheets.List(doc.Database)) sheets.Items.Add(name);
        if (selected != null && sheets.Items.Contains(selected)) sheets.SelectedItem = selected;
    }

    internal void ReportResult(string text)
    {
        progress.Text = text;
        RefreshSheets();
    }
}

internal sealed record ZroCreateRequest(Document Document, ObjectId Station, ZroFields Fields, ZroPdokResult Pdok);
internal sealed record ZroOpenRequest(Document Document, string LayoutName);
