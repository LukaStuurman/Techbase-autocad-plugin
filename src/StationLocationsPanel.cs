using System;
using System.Drawing;
using System.Windows.Forms;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Techbase.Blocks;

/// <summary>Stationslocatiebeheer als onderdeel van de bestaande Techbase-palette.</summary>
internal sealed class StationLocationsPanel : UserControl
{
    private readonly Label summary = new() { AutoSize = true, MaximumSize = new Size(340, 0) };
    private readonly Label status = new() { AutoSize = true, MaximumSize = new Size(340, 0) };
    private readonly ListView sheets = new()
    {
        View = View.Details, Dock = DockStyle.Fill, FullRowSelect = true,
        MultiSelect = false, HideSelection = false, BorderStyle = BorderStyle.FixedSingle
    };
    private readonly Button manage = new() { Text = "Stationlocaties beheren", Height = 36, Dock = DockStyle.Fill };
    private readonly Button refresh = new() { Text = "Vernieuwen", AutoSize = true };

    internal StationLocationsPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(245, 247, 249);
        Font = new Font("Segoe UI", 9);
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, Padding = new Padding(8)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var heading = new Label
        {
            Text = "TECHBASE  /  STATIONSLOCATIES", AutoSize = true,
            Font = new Font(Font, FontStyle.Bold), Padding = new Padding(0, 4, 0, 8)
        };
        layout.Controls.Add(heading, 0, 0);
        layout.Controls.Add(new Label
        {
            Text = "Beheer trafolocaties, VL/AL, projectgegevens, locatiefoto's en A3-bladen (1:250).",
            AutoSize = true, MaximumSize = new Size(340, 0),
            Padding = new Padding(0, 0, 0, 8)
        }, 0, 1);
        layout.Controls.Add(manage, 0, 2);
        var tools = new FlowLayoutPanel
        {
            AutoSize = true, Dock = DockStyle.Fill, WrapContents = true,
            Margin = new Padding(0, 8, 0, 8)
        };
        tools.Controls.Add(refresh);
        tools.Controls.Add(new Label
        {
            Text = "Overzicht van de actieve tekening", AutoSize = true,
            Padding = new Padding(6, 6, 0, 0)
        });
        layout.Controls.Add(tools, 0, 3);
        sheets.Columns.Add("Blad", 128);
        sheets.Columns.Add("Type", 74);
        sheets.Columns.Add("Foto", 74);
        sheets.Resize += (_, _) =>
        {
            if (sheets.Columns.Count == 3)
                sheets.Columns[0].Width = Math.Max(95, sheets.ClientSize.Width - 154);
        };
        layout.Controls.Add(sheets, 0, 4);
        layout.Controls.Add(summary, 0, 5);
        layout.Controls.Add(status, 0, 6);
        manage.Click += (_, _) => OpenManager();
        refresh.Click += (_, _) => RefreshSheets();
        VisibleChanged += (_, _) => { if (Visible) RefreshSheets(); };
        Controls.Add(layout);
        UiTypography.Apply(this);
        RefreshSheets();
    }

    private void OpenManager()
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc == null) { status.Text = "Open eerst een tekening."; return; }
        if (Convert.ToInt32(AcApp.GetSystemVariable("CMDACTIVE")) != 0)
        {
            status.Text = "Rond eerst het actieve AutoCAD-commando af.";
            return;
        }
        status.Text = "Stationlocatiebeheer openen…";
        doc.SendStringToExecute("_TBSTATIONBEHEER ", true, false, false);
    }

    internal void RefreshSheets()
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        sheets.BeginUpdate();
        try
        {
            sheets.Items.Clear();
            manage.Enabled = doc != null;
            if (doc == null)
            {
                summary.Text = "Geen actieve tekening.";
                status.Text = "Open een DWG om locaties te beheren.";
                return;
            }
            var locations = Stationslocaties.StationLocationSheets.List(doc.Database);
            var withPhoto = Stationslocaties.StationLocationSheets.SheetsWithPhoto(doc.Database, locations);
            foreach (var sheet in locations)
            {
                var kind = sheet.Alternative == 0 ? "VL" : "AL" + (sheet.Alternative > 1 ? sheet.Alternative.ToString() : "");
                var photo = withPhoto.Contains(sheet.Name) ? "Ja" : "Nee";
                var item = new ListViewItem(new[] { sheet.Name, kind, photo });
                if (sheets.Items.Count % 2 == 1) item.BackColor = Color.White;
                sheets.Items.Add(item);
            }
            var total = Stationslocaties.StationLocationSheets.HasOverview(doc.Database);
            summary.Text = $"{locations.Count} locatiebladen · {withPhoto.Count} met foto · Totaal: {(total ? "aanwezig" : "nog niet gemaakt")}";
            status.Text = locations.Count == 0
                ? "Klik op 'Stationlocaties beheren' om de eerste locatie te plaatsen."
                : "Gebruik Beheren voor locaties, kaartreferenties, foto's, PDF's en instellingen.";
        }
        catch (Exception ex)
        {
            summary.Text = "Overzicht kon niet worden geladen.";
            status.Text = ex.Message;
        }
        finally { sheets.EndUpdate(); }
    }
}
