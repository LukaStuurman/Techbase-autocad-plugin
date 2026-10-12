using System.Drawing;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Techbase.Blocks;

internal sealed class KabelgeulEditor : UserControl
{
    internal DataGridView Grid { get; } = new() { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoGenerateColumns = false, RowHeadersWidth = 32, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
    private readonly TextBox mantel = new() { Width = 270 };
    internal KabelgeulEditor(KabelgeulData data)
    {
        Dock = DockStyle.Fill;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        layout.ColumnStyles.Add(new(SizeType.Percent, 100));
        layout.RowStyles.Add(new(SizeType.AutoSize)); layout.RowStyles.Add(new(SizeType.Percent, 100)); layout.RowStyles.Add(new(SizeType.AutoSize));
        var tools = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        foreach (var item in new[] { ("Rij toevoegen", 0), ("Rij verwijderen", 1), ("Volgorde omdraaien", 2) })
        {
            var button = new Button { Text = item.Item1, AutoSize = true }; var action = item.Item2;
            button.Click += (_, _) =>
            {
                if (action == 0) { if (Grid.Rows.Count < 100) Add(new KabelgeulRow()); }
                else if (action == 1 && Grid.Rows.Count > 1 && Grid.CurrentRow != null) Grid.Rows.Remove(Grid.CurrentRow);
                else if (action == 2) Set(CaptureData() with { Rows = CaptureData().Rows.Reverse().ToArray() });
            };
            tools.Controls.Add(button);
        }
        layout.Controls.Add(tools, 0, 0); layout.Controls.Add(Grid, 0, 1);
        var footer = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, MinimumSize = new Size(0, 54) };
        footer.Controls.Add(new Label { Text = "Mantelbuis specificaties:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }); footer.Controls.Add(mantel); layout.Controls.Add(footer, 0, 2);
        DataGridViewComboBoxColumn Choice(string name, string[] options, int width)
        { var col = new DataGridViewComboBoxColumn { HeaderText = name, Width = width }; col.Items.AddRange(options); return col; }
        Grid.Columns.Add(Choice("Kabeltype", KabelgeulData.Types, 125));
        Grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Tekst zijde 1", Width = 180 });
        Grid.Columns.Add(Choice("Vorm zijde 1", KabelgeulData.Shapes, 105));
        Grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "2-zijdig", Width = 70 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Tekst zijde 2", Width = 180 });
        Grid.Columns.Add(Choice("Vorm zijde 2", KabelgeulData.Shapes, 105));
        Grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Mantel", Width = 75 });
        Grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Nieuwe bak", Width = 85 });
        Grid.DataError += (_, e) => { e.ThrowException = false; };
        Grid.CurrentCellDirtyStateChanged += (_, _) => { if (Grid.IsCurrentCellDirty) Grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        Grid.CellValueChanged += (_, args) => { if (args.RowIndex >= 0 && args.ColumnIndex is 0 or 3) UpdateRow(Grid.Rows[args.RowIndex]); };
        Controls.Add(layout); Set(data); UiTypography.Apply(this);
    }
    private void Add(KabelgeulRow row) { var index = Grid.Rows.Add(row.Type, row.Text, row.Shape, row.TwoSides, row.Text2, row.Shape2, row.Mantel, row.NewTray); UpdateRow(Grid.Rows[index]); }
    private static void UpdateRow(DataGridViewRow row)
    {
        var half = Convert.ToString(row.Cells[0].Value) == "Half rooien";
        if (half) { row.Cells[2].Value = "Dubbel"; if (row.Cells[3].Value is true) row.Cells[3].Value = false; }
        row.Cells[2].ReadOnly = half; row.Cells[3].ReadOnly = half;
        row.Cells[4].ReadOnly = !half && row.Cells[3].Value is not true;
        row.Cells[5].ReadOnly = half || row.Cells[3].Value is not true;
    }
    internal void Set(KabelgeulData data) { Grid.Rows.Clear(); foreach (var row in data.Rows) Add(row); mantel.Text = data.MantelSpec; }
    internal KabelgeulData CaptureData()
    {
        Grid.EndEdit();
        string Cell(DataGridViewRow r, int col) => Convert.ToString(r.Cells[col].Value) ?? "";
        bool Flag(DataGridViewRow r, int col) => r.Cells[col].Value is true;
        var rows = Grid.Rows.Cast<DataGridViewRow>().Select(r => new KabelgeulRow(Cell(r, 0), Cell(r, 1), Cell(r, 2), Flag(r, 3), Cell(r, 4), Cell(r, 5), Cell(r, 6), Flag(r, 7))).ToArray();
        if (rows.Length == 0 || rows.Any(r => !KabelgeulData.Types.Contains(r.Type) || !KabelgeulData.Shapes.Contains(r.Shape) || !KabelgeulData.Shapes.Contains(r.Shape2)))
            throw new InvalidOperationException("Kies een geldig kabeltype en een vorm per rij.");
        return new(rows, mantel.Text);
    }
}

internal sealed class KabelgeulPanel : UserControl
{
    internal KabelgeulEditor Editor { get; } = new(KabelgeulData.Default);
    private readonly CheckBox automatic = new() { Text = "Kabels uit de tekening overnemen", Checked = true, AutoSize = true };
    private readonly Label status = new() { AutoSize = true, MaximumSize = new Size(650, 0) };
    internal KabelgeulPanel(Action? enlarge = null)
    {
        Dock = DockStyle.Fill; Font = new Font(Typography.Family, 9);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = new Padding(8) };
        layout.ColumnStyles.Add(new(SizeType.Percent, 100));
        layout.RowStyles.Add(new(SizeType.AutoSize)); layout.RowStyles.Add(new(SizeType.Percent, 100)); layout.RowStyles.Add(new(SizeType.AutoSize)); layout.RowStyles.Add(new(SizeType.AutoSize));
        var heading = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        heading.Controls.Add(new Label { Text = "KABELGEUL", AutoSize = true, Padding = new Padding(0, 6, 12, 0) });
        var larger = new Button { Text = "Groter openen", AutoSize = true }; larger.Click += (_, _) => enlarge?.Invoke(); heading.Controls.Add(larger); heading.Controls.Add(automatic);
        layout.Controls.Add(heading, 0, 0); layout.Controls.Add(Editor, 0, 1);
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        foreach (var item in new[] { ("Kabelgeul plaatsen", "TBKABELGEUL"), ("Gegevens kopiëren", "TBGEULCOPY"), ("Teksten bewerken", "TBGEULEDIT"), ("Codepositie wisselen", "TBGEULCODE") })
        { var button = new Button { Text = item.Item1, AutoSize = true }; var command = item.Item2; button.Click += (_, _) => Queue(command); buttons.Controls.Add(button); }
        layout.Controls.Add(buttons, 0, 2);
        var footer = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
        var notes = new Label { Text = "Kies een kabel en een afstandspunt. Plaats Direct of Zwevend.\nVorm: Enkel, Dubbel, Drie of Combi per zijde. Mantel: X of een groepscode.\nNieuwe bak begint vanaf die rij een aparte bak. Scroll horizontaal voor alle kolommen.", AutoSize = true, MaximumSize = new Size(700, 0) };
        footer.Controls.Add(notes);
        SizeChanged += (_, _) => { notes.MaximumSize = new Size(Math.Max(180, ClientSize.Width - 30), 0); status.MaximumSize = notes.MaximumSize; };
        footer.Controls.Add(status); layout.Controls.Add(footer, 0, 3); Controls.Add(layout); UiTypography.Apply(this);
    }
    private void Queue(string command)
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument; if (doc == null) { Report("Open eerst een tekening."); return; }
        if (Convert.ToInt32(AcApp.GetSystemVariable("CMDACTIVE")) != 0) { Report("Rond eerst het actieve commando af of druk Esc."); return; }
        try
        {
            var source = System.IO.Path.Combine(Plugin.ContentsRoot, "Assets", "Kabelgeul", "Techbase.Kabelgeul.lsp");
            if (!System.IO.File.Exists(source)) throw new InvalidOperationException("Kabelgeul-bron ontbreekt. Installeer de plugin opnieuw.");
            var path = source.Replace("\\", "/").Replace("\"", "\\\"");
            KabelgeulCommands.Pending = new(doc, Editor.CaptureData(), command == "TBGEULCOPY" || automatic.Checked);
            doc.SendStringToExecute("(if (not *tbkg:ready*) (load \"" + path + "\"))\n" + command + " ", true, false, false);
        }
        catch (Exception ex) { Report(ex.Message); }
    }
    internal void Report(string message) => status.Text = message;
}
