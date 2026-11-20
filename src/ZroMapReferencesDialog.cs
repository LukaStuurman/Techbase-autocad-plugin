using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Stationslocaties;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Techbase.Blocks;

/// <summary>
/// ZRO-kaartbronnen: alleen topografie en kadastrale percelen als PDOK-DWG-XREF.
/// De bestaande asynchrone Stationslocaties-kaartimport verzorgt het downloaden.
/// </summary>
internal sealed class ZroMapReferencesDialog : Form
{
    private readonly ComboBox choices = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 285,
        AccessibleName = "Kaartlagen"
    };
    private readonly CheckBox attachExisting = new()
    {
        AutoSize = true,
        Text = "Bestaande DWG als XREF koppelen"
    };
    private readonly Label hint = new()
    {
        AutoSize = true,
        Text = "Selecteer een gebied in modelruimte; de lagen worden als XREF gekoppeld."
    };
    internal bool AttachExisting => attachExisting.Checked;
    internal int SelectedKind => choices.SelectedIndex;

    internal ZroMapReferencesDialog()
    {
        Text = "TechBase | ZRO-kaartreferenties";
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ClientSize = new Size(505, 235);
        Padding = new Padding(16);
        BackColor = UiTheme.Canvas;
        ForeColor = UiTheme.Ink;

        var stack = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = false, Padding = Padding.Empty
        };
        stack.Controls.Add(new Label
        {
            Text = "Kies welke PDOK-achtergrond je wilt gebruiken:",
            AutoSize = true, Margin = new Padding(0, 0, 0, 10)
        });
        choices.Items.AddRange(new object[]
        {
            "Topografie en percelen",
            "Alleen topografie",
            "Alleen kadastrale percelen"
        });
        choices.SelectedIndex = 0;
        stack.Controls.Add(choices);
        attachExisting.Margin = new Padding(0, 13, 0, 9);
        stack.Controls.Add(attachExisting);
        hint.MaximumSize = new Size(462, 0);
        stack.Controls.Add(hint);
        attachExisting.CheckedChanged += (_, _) =>
            hint.Text = AttachExisting
                ? "Wijs een bestaande topo- of percelen-DWG aan."
                : "Selecteer een gebied in modelruimte; de kaart wordt als XREF gekoppeld.";

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 45,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 4, 0, 0)
        };
        var okay = new Button { Text = "Doorgaan", Width = 128, Height = 33, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Annuleren", Width = 110, Height = 33, DialogResult = DialogResult.Cancel };
        actions.Controls.Add(okay);
        actions.Controls.Add(cancel);
        Controls.Add(stack);
        Controls.Add(actions);
        CancelButton = cancel;
        AcceptButton = okay;
        UiTypography.Apply(this);
    }

    internal static void Run(Document doc)
    {
        using var dialog = new ZroMapReferencesDialog();
        if (Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(dialog) != DialogResult.OK) return;
        var selected = dialog.SelectedKind;
        if (dialog.AttachExisting)
        {
            using var open = new OpenFileDialog
            {
                Title = "Koppel bestaande topografie- of percelen-DWG",
                Filter = "AutoCAD-tekening (*.dwg)|*.dwg",
                CheckFileExists = true
            };
            if (open.ShowDialog() != DialogResult.OK) return;
            var type = selected == 1 ? "TOPO" : selected == 2 ? "PERCELEN" : null;
            StationReferences.Attach(doc, open.FileName, type);
            doc.Editor.Regen();
            return;
        }

        LayoutManager.Current.CurrentLayout = "Model";
        var first = doc.Editor.GetPoint("\nEerste hoek van het topo/percelengebied: ");
        if (first.Status != PromptStatus.OK) return;
        var second = doc.Editor.GetCorner("\nTegenoverliggende hoek van het gebied: ", first.Value);
        if (second.Status != PromptStatus.OK) return;
        var ucs = doc.Editor.CurrentUserCoordinateSystem;
        var area = ParcelArea.FromCorners(first.Value.TransformBy(ucs), second.Value.TransformBy(ucs));

        if (selected == 0)
        {
            using var folder = new FolderBrowserDialog
            {
                Description = "Kies de uitvoermap voor de topografie- en percelen-XREF's",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true
            };
            if (Path.IsPathFullyQualified(doc.Database.Filename))
                folder.InitialDirectory = Path.GetDirectoryName(doc.Database.Filename) ?? "";
            if (folder.ShowDialog() != DialogResult.OK) return;
            StationReferences.QueueTopoAndPercelen(doc, folder.SelectedPath, area);
        }
        else
        {
            var kind = selected == 1 ? MapKind.Topo : MapKind.Percelen;
            using var save = new SaveFileDialog
            {
                Filter = "AutoCAD-tekening (*.dwg)|*.dwg",
                DefaultExt = "dwg",
                FileName = kind + " " + Path.GetFileNameWithoutExtension(doc.Name) + ".dwg"
            };
            if (Path.IsPathFullyQualified(doc.Database.Filename))
                save.InitialDirectory = Path.GetDirectoryName(doc.Database.Filename);
            if (save.ShowDialog() != DialogResult.OK) return;
            StationReferences.QueueCreate(doc, save.FileName, area, kind);
        }
    }
}
