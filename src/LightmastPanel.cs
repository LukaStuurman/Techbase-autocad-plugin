using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Techbase.Blocks;

internal sealed class LightmastPanel : UserControl
{
    private readonly ComboBox xrefs = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, DropDownWidth = 650 };
    private readonly CheckedListBox layers = new() { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false };
    private readonly CheckedListBox types = new() { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false };
    private readonly RadioButton polyline = new() { Text = "Bestaande gesloten polyline", AutoSize = true, Checked = true };
    private readonly RadioButton all = new() { Text = "Alles in de gekozen xref", AutoSize = true };
    private readonly RadioButton draw = new() { Text = "Zelf polygoon tekenen", AutoSize = true };
    private readonly Label status = new() { Dock = DockStyle.Fill, AutoSize = true, MaximumSize = new Size(340, 0), Padding = new Padding(0, 6, 0, 0) };
    private readonly Button run = new() { Text = "Lichtmasten plaatsen", Dock = DockStyle.Fill, Height = 34 };
    private Document? document;
    private MastSnapshot? snapshot;
    private bool refreshing;
    internal LightmastPanel()
    {
        Dock = DockStyle.Fill; Font = new Font("Segoe UI", 9); BackColor = Color.FromArgb(245, 247, 249);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 10, Padding = new Padding(8) };
        for (var i = 0; i < 10; i++) layout.RowStyles.Add(i == 4 ? new RowStyle(SizeType.Percent, 65) : i == 6 ? new RowStyle(SizeType.Percent, 35) : new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label { Text = "LICHTMASTEN UIT XREF", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Padding = new Padding(0, 4, 0, 8) }, 0, 0);
        var refresh = new Button { Text = "Xrefs vernieuwen", AutoSize = true };
        refresh.Click += (_, _) => RefreshDrawing(); layout.Controls.Add(refresh, 0, 1);
        layout.Controls.Add(xrefs, 0, 2);
        var layerHeading = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        layerHeading.Controls.Add(new Label { Text = "Lichtmastlagen", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        var selectAll = new Button { Text = "Alle", AutoSize = true };
        var clear = new Button { Text = "Geen", AutoSize = true };
        selectAll.Click += (_, _) => { for (var i = 0; i < layers.Items.Count; i++) layers.SetItemChecked(i, true); };
        clear.Click += (_, _) => { for (var i = 0; i < layers.Items.Count; i++) layers.SetItemChecked(i, false); };
        layerHeading.Controls.Add(selectAll); layerHeading.Controls.Add(clear);
        layout.Controls.Add(layerHeading, 0, 3); layout.Controls.Add(layers, 0, 4);
        layout.Controls.Add(new Label { Text = "Blockfilter (optioneel; leeg = alle typen)", AutoSize = true, Padding = new Padding(0, 6, 0, 3) }, 0, 5);
        layout.Controls.Add(types, 0, 6);
        var region = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        region.Controls.Add(polyline); region.Controls.Add(all); region.Controls.Add(draw);
        layout.Controls.Add(region, 0, 7); layout.Controls.Add(run, 0, 8); layout.Controls.Add(status, 0, 9);
        Controls.Add(layout);
        xrefs.SelectedIndexChanged += (_, _) => { if (!refreshing) RefreshCandidates(); };
        layers.ItemCheck += (_, _) => { if (IsHandleCreated) BeginInvoke(new Action(UpdateTypes)); };
        run.Click += (_, _) => QueuePlacement();
        AcApp.DocumentManager.DocumentActivated += Activated;
        AcApp.DocumentManager.DocumentToBeDestroyed += Closing;
        RefreshDrawing();
    }
    private void Activated(object sender, DocumentCollectionEventArgs args) => RefreshDrawing();
    private void Closing(object sender, DocumentCollectionEventArgs args)
    {
        if (args.Document != document) return;
        document = null; snapshot = null; xrefs.Items.Clear(); layers.Items.Clear(); types.Items.Clear();
        status.Text = "Open een tekening en vernieuw de xrefs.";
    }
    internal void RefreshDrawing()
    {
        if (IsDisposed) return;
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        refreshing = true;
        var previous = document == doc && xrefs.SelectedItem is XrefChoice choice ? choice.Id : Autodesk.AutoCAD.DatabaseServices.ObjectId.Null;
        document = doc; snapshot = null; xrefs.Items.Clear(); layers.Items.Clear(); types.Items.Clear();
        try
        {
            if (doc == null) { status.Text = "Open eerst een tekening."; return; }
            using var locked = doc.LockDocument();
            foreach (var item in LightmastService.Xrefs(doc.Database)) xrefs.Items.Add(item);
            if (xrefs.Items.Count == 0) { status.Text = "Geen geladen xref-invoegingen in de huidige tekenruimte. Laad een xref en vernieuw."; return; }
            xrefs.SelectedItem = xrefs.Items.Cast<XrefChoice>().FirstOrDefault(c => c.Id == previous) ?? xrefs.Items[0];
        }
        catch (Exception ex) { status.Text = ex.Message; }
        finally { refreshing = false; }
        RefreshCandidates();
    }
    private void RefreshCandidates()
    {
        snapshot = null; layers.Items.Clear(); types.Items.Clear();
        try
        {
            if (document == null || xrefs.SelectedItem is not XrefChoice selected) return;
            using var locked = document.LockDocument();
            snapshot = LightmastService.Scan(document.Database, selected.Id);
            foreach (var group in snapshot.Candidates.GroupBy(c => c.Layer).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
                layers.Items.Add(new FilterItem(group.Key, group.Key + " (" + group.Count() + ")"));
            UpdateTypes();
            status.Text = "Vink de lichtmastlagen aan. Plaatst het Lisp-symbool op laag Bestaand." +
                (snapshot.Warnings.Count == 0 ? "" : "\n" + string.Join("\n", snapshot.Warnings));
        }
        catch (Exception ex) { status.Text = ex.Message; }
    }
    private void UpdateTypes()
    {
        if (IsDisposed) return;
        var checkedTypes = types.CheckedItems.Cast<FilterItem>().Select(i => i.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selectedLayers = layers.CheckedItems.Cast<FilterItem>().Select(i => i.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        types.Items.Clear();
        if (snapshot == null) return;
        foreach (var group in snapshot.Candidates.Where(c => selectedLayers.Contains(c.Layer)).GroupBy(c => c.BlockName).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            types.Items.Add(new FilterItem(group.Key, group.Key + " (" + group.Count() + ")"), checkedTypes.Contains(group.Key));
    }
    private void QueuePlacement()
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc == null || doc != document) { status.Text = "De actieve tekening is veranderd. Vernieuw de xrefs."; return; }
        if (Convert.ToInt32(AcApp.GetSystemVariable("CMDACTIVE")) != 0) { status.Text = "Rond het actieve commando af of druk Esc."; return; }
        if (Plugin.PendingLightmast != null) { status.Text = "De vorige lichtmastopdracht staat nog klaar."; return; }
        if (xrefs.SelectedItem is not XrefChoice selected) { status.Text = "Kies eerst een geladen xref."; return; }
        var selectedLayers = layers.CheckedItems.Cast<FilterItem>().Select(i => i.Key).ToArray();
        if (selectedLayers.Length == 0) { status.Text = "Vink minimaal één lichtmastlaag aan."; return; }
        var selectedTypes = types.CheckedItems.Cast<FilterItem>().Select(i => i.Key).ToArray();
        var mode = all.Checked ? AreaMode.All : draw.Checked ? AreaMode.Draw : AreaMode.Polyline;
        Plugin.PendingLightmast = new LightmastRequest(doc, selected.Id, selectedLayers, selectedTypes, mode);
        doc.SendStringToExecute("_TBLICHTMAST ", true, false, false);
        status.Text = mode == AreaMode.All ? "Lichtmasten worden geplaatst..." : mode == AreaMode.Draw ? "Klik de hoekpunten; Enter sluit. Esc annuleert." : "Selecteer de gesloten gebiedspolyline in de tekening.";
    }
    internal void Report(string text) => status.Text = text;
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            AcApp.DocumentManager.DocumentActivated -= Activated;
            AcApp.DocumentManager.DocumentToBeDestroyed -= Closing;
        }
        base.Dispose(disposing);
    }
    private sealed record FilterItem(string Key, string Label) { public override string ToString() => Label; }
}
