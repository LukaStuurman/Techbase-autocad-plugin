using System;
using System.Drawing;
using Font = System.Drawing.Font;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Stationslocaties;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Techbase.Blocks;

/// <summary>
/// Zelfde trafobibliotheek voor Stationslocaties en ZRO:
/// de Stationlocaties-gebruikersbibliotheek + de stationsblokken uit Blocks.
/// AANKOOP-varianten krijgen dezelfde groene ANSI31-vlakken.
/// </summary>
internal sealed record StationBlockSelection(
    string DisplayName, BlockTool? PaletteTool = null,
    StationBlockChoice? Registered = null, bool Acquisition = false, bool UseExisting = false)
{
    public override string ToString() => DisplayName;

    internal ObjectId ResolveDefinition(Database db)
    {
        if (UseExisting) throw new InvalidOperationException("Selecteer een bestaand blok in de tekening.");
        if (PaletteTool != null)
            return BlockInserter.Import(db, Plugin.Blocks ?? new Catalog(Plugin.DefaultPalette), PaletteTool);
        if (Registered != null)
        {
            ObjectId original;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                original = StationBlockLibrary.Import(db, tr, Registered);
                tr.Commit();
            }
            return Acquisition ?
                StationPurchaseVariants.Ensure(db, original, Registered.Name + StationPurchaseVariants.Suffix) :
                original;
        }
        using (var tr = db.TransactionManager.StartTransaction())
        {
            var native = NativeStationGeometry.GetSymbol(db, tr, "TB_STATION_TRAFO");
            tr.Commit();
            return native;
        }
    }

    internal Scale3d ScaleFor(Database db, ObjectId definition)
    {
        if (Registered != null) return Registered.Scale;
        if (PaletteTool == null) return new Scale3d(1);
        using var tx = db.TransactionManager.StartTransaction();
        var record = (BlockTableRecord)tx.GetObject(definition, OpenMode.ForRead);
        var units = record.Units == UnitsValue.Undefined || db.Insunits == UnitsValue.Undefined
            ? 1 : UnitsConverter.GetConversionFactor(record.Units, db.Insunits);
        return new Scale3d(PaletteTool.Scale * units);
    }
}

internal sealed class StationBlockSelectionDialog : Form
{
    private readonly ComboBox choices = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill
    };
    private readonly bool includeExisting;
    private readonly Label hint = new() { AutoSize = true, ForeColor = UiTheme.Muted };
    internal StationBlockSelection? Selected => choices.SelectedItem as StationBlockSelection;
    internal bool AddCustom { get; private set; }

    internal StationBlockSelectionDialog(bool allowExisting)
    {
        includeExisting = allowExisting;
        Text = "TechBase | Trafoblok kiezen";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(585, 235);
        MinimumSize = new Size(475, 225);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = UiTheme.Canvas;
        ForeColor = UiTheme.Ink;
        Padding = new Padding(16);
        Font = new Font(Typography.Family, 10);

        var options = new System.Collections.Generic.List<StationBlockSelection>();
        if (allowExisting)
            options.Add(new("Bestaand trafoblok in tekening selecteren", UseExisting: true));
        var tools = (Plugin.Blocks ?? new Catalog(Plugin.DefaultPalette)).Tools
            .Where(StationPurchaseVariants.IsStationTool)
            .OrderBy(t => t.Name).ToArray();
        options.AddRange(tools.Select(t => new StationBlockSelection(t.Name, PaletteTool: t)));
        var catalog = StationBlockLibrary.Read();
        foreach (var block in catalog.Blocks)
        {
            options.Add(new(block.Name, Registered: block));
            options.Add(new(block.Name + StationPurchaseVariants.Suffix,
                Registered: block, Acquisition: true));
        }
        // De oorspronkelijke standaard Stationslocaties-trafo blijft beschikbaar.
        options.Add(new("Standaard Stationslocaties-trafo (groene arcering)"));
        choices.Items.AddRange(options.Cast<object>().ToArray());
        var preferred = options.FindIndex(x =>
            x.PaletteTool != null && x.PaletteTool.Name.Contains("PACTO 25",
                StringComparison.OrdinalIgnoreCase) &&
            StationPurchaseVariants.IsPurchaseName(x.PaletteTool.BlockName));
        choices.SelectedIndex = preferred >= 0 ? preferred : 0;
        hint.Text = "Standaard: Pacto 25 AANKOOP. Voeg zelf nieuwe trafoblokken toe aan de gedeelde bibliotheek.";
        hint.MaximumSize = new Size(530, 0);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 37));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label
        {
            Text = "Selecteer het stationtype", Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        layout.Controls.Add(choices, 0, 1);
        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.Controls.Add(hint, 0, 0);
        bottom.SetColumnSpan(hint, 2);
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true,
            FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft
        };
        var ok = new Button { Text = "Verder", Width = 92, Height = 34, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Annuleren", Width = 100, Height = 34, DialogResult = DialogResult.Cancel };
        var add = new Button { Text = "Extra trafoblok toevoegen", Width = 196, Height = 34 };
        add.Click += (_, _) => { AddCustom = true; DialogResult = DialogResult.Retry; };
        actions.Controls.Add(ok); actions.Controls.Add(cancel); actions.Controls.Add(add);
        bottom.Controls.Add(actions, 0, 1);
        bottom.SetColumnSpan(actions, 2);
        layout.Controls.Add(bottom, 0, 2);
        Controls.Add(layout);
        AcceptButton = ok;
        CancelButton = cancel;
        UiTypography.Apply(this);
    }

    internal static StationBlockSelection? Choose(Document doc, bool allowExisting = false)
    {
        while (true)
        {
            using var chooser = new StationBlockSelectionDialog(allowExisting);
            var response = Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(chooser);
            if (response == DialogResult.OK) return chooser.Selected;
            if (!chooser.AddCustom) return null;
            var corners = MessageBox.Show(
                "Wil je de vier hoekpunten C1–C4 voor dit eigen blok aanwijzen?",
                "Trafoblokken", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (corners == DialogResult.Cancel) continue;
            LayoutManager.Current.CurrentLayout = "Model";
            StationBlockLibrary.Register(doc, corners == DialogResult.Yes);
        }
    }
}
