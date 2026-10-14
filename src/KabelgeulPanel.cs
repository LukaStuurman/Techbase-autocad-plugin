using System.Drawing;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Techbase.Blocks;

internal sealed class KabelgeulEditor : UserControl
{
    internal DataGridView Grid { get; } = new() { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoGenerateColumns = false, RowHeadersWidth = 32, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
    internal TextBox MantelSpecBox { get; } = new() { Width = 270 };
    private readonly bool rules;
    private readonly bool useDefaultsOnTypeChange;
    private bool updating;
    internal KabelgeulEditor(KabelgeulData data, bool rules = false, bool useDefaultsOnTypeChange = true)
    {
        this.rules = rules;
        this.useDefaultsOnTypeChange = useDefaultsOnTypeChange;
        Dock = DockStyle.Fill;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        layout.ColumnStyles.Add(new(SizeType.Percent, 100));
        layout.RowStyles.Add(new(SizeType.AutoSize)); layout.RowStyles.Add(new(SizeType.Percent, 100)); layout.RowStyles.Add(new(SizeType.AutoSize));
        var tools = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        foreach (var item in rules ? Array.Empty<(string, int)>() : new[] { ("Rij vóór", 0), ("Rij na", 1), ("Rij verwijderen", 2), ("Volgorde omdraaien", 3) })
        {
            var button = new Button { Text = item.Item1, AutoSize = true }; var action = item.Item2;
            button.Click += (_, _) =>
            {
                if (action is 0 or 1) InsertRow(action == 1);
                else if (action == 2 && Grid.Rows.Count > 1 && Grid.CurrentRow != null) Grid.Rows.Remove(Grid.CurrentRow);
                else if (action == 3) { var current = CaptureData(); Set(current with { Rows = current.Rows.Reverse().ToArray() }); }
            };
            tools.Controls.Add(button);
        }
        layout.Controls.Add(tools, 0, 0); layout.Controls.Add(Grid, 0, 1);
        var footer = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, MinimumSize = new Size(0, 54) };
        footer.Controls.Add(new Label { Text = rules ? "Standaard mantelspecificatie:" : "Mantelbuis specificaties:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }); footer.Controls.Add(MantelSpecBox); layout.Controls.Add(footer, 0, 2);
        DataGridViewComboBoxColumn Choice(string name, string[] options, int width)
        { var col = new DataGridViewComboBoxColumn { HeaderText = name, Width = width }; col.Items.AddRange(options); return col; }
        Grid.Columns.Add(rules ? new DataGridViewTextBoxColumn { HeaderText = "Kabeltype", Width = 125 } : Choice("Kabeltype", KabelgeulData.Types, 125));
        Grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Tekst zijde 1", Width = 180 });
        Grid.Columns.Add(Choice("Vorm zijde 1", KabelgeulData.Shapes, 105));
        Grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "2-zijdig", Width = 70 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Tekst zijde 2", Width = 180 });
        Grid.Columns.Add(Choice("Vorm zijde 2", KabelgeulData.Shapes, 105));
        Grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Mantel", Width = 75 });
        Grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Nieuwe bak", Width = 85 });
        if (rules) { Grid.Columns[0].ReadOnly = true; Grid.Columns[1].HeaderText = "Standaardtekst zijde 1"; Grid.Columns[4].HeaderText = "Standaardtekst zijde 2"; }
        Grid.DataError += (_, e) => { e.ThrowException = false; };
        Grid.CurrentCellDirtyStateChanged += (_, _) => { if (Grid.IsCurrentCellDirty && Grid.CurrentCell is DataGridViewCheckBoxCell or DataGridViewComboBoxCell) Grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        Grid.CellValueChanged += (_, args) =>
        {
            if (updating || args.RowIndex < 0 || args.ColumnIndex is not (0 or 3)) return;
            updating = true;
            try
            {
                var row = Grid.Rows[args.RowIndex];
                if (!rules && useDefaultsOnTypeChange && args.ColumnIndex == 0 && row.Tag is string oldType)
                {
                    var next = KabelgeulData.Rule(Plugin.Preferences, Convert.ToString(row.Cells[0].Value) ?? "K01");
                    var old = KabelgeulData.Rule(Plugin.Preferences, oldType);
                    if (Convert.ToString(row.Cells[1].Value) == old.Text) row.Cells[1].Value = next.Text;
                    if (Convert.ToString(row.Cells[4].Value) == old.Text2) row.Cells[4].Value = next.Text2;
                }
                row.Tag = Convert.ToString(row.Cells[0].Value); UpdateRow(row);
            }
            finally { updating = false; }
        };
        Controls.Add(layout); Set(data); UiTypography.Apply(this);
    }
    private void Add(KabelgeulRow row) { var index = Grid.Rows.Add(row.Type, row.Text, row.Shape, row.TwoSides, row.Text2, row.Shape2, row.Mantel, row.NewTray); Grid.Rows[index].Tag = row.Type; UpdateRow(Grid.Rows[index]); }
    internal void InsertRow(bool after)
    {
        if (rules || Grid.Rows.Count >= 100) return;
        var data = CaptureData(); var index = Math.Clamp((Grid.CurrentRow?.Index ?? 0) + (after ? 1 : 0), 0, data.Rows.Length);
        var type = Grid.CurrentRow == null ? "K01" : data.Rows[Grid.CurrentRow.Index].Type;
        var rows = data.Rows.ToList(); rows.Insert(index, KabelgeulData.Rule(Plugin.Preferences, type));
        Set(data with { Rows = rows.ToArray() }); Grid.CurrentCell = Grid.Rows[index].Cells[0]; Grid.ClearSelection(); Grid.Rows[index].Selected = true;
    }
    private static void UpdateRow(DataGridViewRow row)
    {
        var half = Convert.ToString(row.Cells[0].Value) == "Half rooien";
        if (half) { row.Cells[2].Value = "Dubbel"; if (row.Cells[3].Value is true) row.Cells[3].Value = false; }
        row.Cells[2].ReadOnly = half; row.Cells[3].ReadOnly = half;
        row.Cells[4].ReadOnly = !half && row.Cells[3].Value is not true;
        row.Cells[5].ReadOnly = half || row.Cells[3].Value is not true;
    }
    internal void Set(KabelgeulData data) { updating = true; try { Grid.Rows.Clear(); foreach (var row in data.Rows) Add(row); MantelSpecBox.Text = data.MantelSpec; } finally { updating = false; } }
    internal KabelgeulData CaptureData(bool commitEdit = true)
    {
        if (commitEdit) Grid.EndEdit();
        string Cell(DataGridViewRow r, int col) => !commitEdit && Grid.IsCurrentCellInEditMode && Grid.CurrentCell?.RowIndex == r.Index && Grid.CurrentCell.ColumnIndex == col && Grid.EditingControl is TextBox edit
            ? edit.Text : Convert.ToString(r.Cells[col].Value) ?? "";
        bool Flag(DataGridViewRow r, int col) => r.Cells[col].Value is true;
        var rows = Grid.Rows.Cast<DataGridViewRow>().Select(r => new KabelgeulRow(Cell(r, 0), Cell(r, 1), Cell(r, 2), Flag(r, 3), Cell(r, 4), Cell(r, 5), Cell(r, 6), Flag(r, 7))).ToArray();
        if (rows.Length == 0 || rows.Any(r => !KabelgeulData.Types.Contains(r.Type) || !KabelgeulData.Shapes.Contains(r.Shape) || !KabelgeulData.Shapes.Contains(r.Shape2)))
            throw new InvalidOperationException("Kies een geldig kabeltype en een vorm per rij.");
        return new(rows, MantelSpecBox.Text);
    }
}

internal sealed class KabelgeulPanel : UserControl
{
    internal KabelgeulEditor Editor { get; } = new(KabelgeulData.Rules(Plugin.Preferences), true);
    private bool saving;
    private readonly Label status = new() { AutoSize = true, MaximumSize = new Size(650, 0) };
    internal KabelgeulPanel(Action? enlarge = null)
    {
        Dock = DockStyle.Fill; Font = new Font(Typography.Family, 9);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = new Padding(8) };
        layout.ColumnStyles.Add(new(SizeType.Percent, 100));
        layout.RowStyles.Add(new(SizeType.AutoSize)); layout.RowStyles.Add(new(SizeType.Percent, 100)); layout.RowStyles.Add(new(SizeType.AutoSize)); layout.RowStyles.Add(new(SizeType.AutoSize));
        var heading = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        heading.Controls.Add(new Label { Text = "KABELGEUL", AutoSize = true, Padding = new Padding(0, 6, 12, 0) });
        var larger = new Button { Text = "Groter openen", AutoSize = true }; larger.Click += (_, _) => enlarge?.Invoke(); heading.Controls.Add(larger);
        heading.Controls.Add(new Label { Text = "Standaardinstellingen per kabeltype", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        layout.Controls.Add(heading, 0, 0); layout.Controls.Add(Editor, 0, 1);
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        foreach (var item in new[] { ("Kabelgeul plaatsen", "TBKABELGEUL"), ("Gegevens kopiëren", "TBGEULCOPY"), ("Kabelgeul bewerken", "TBGEULEDIT"), ("Codepositie wisselen", "TBGEULCODE") })
        { var button = new Button { Text = item.Item1, AutoSize = true }; var command = item.Item2; button.Click += (_, _) => Queue(command); buttons.Controls.Add(button); }
        layout.Controls.Add(buttons, 0, 2);
        var footer = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
        var notes = new Label { Text = "Deze tabel bepaalt de standaardteksten en opmaak bij nieuwe geulen. Wijzigingen worden automatisch bewaard.\nDe volledige tekst is zichtbaar en bewerkbaar, inclusief Ri-nummer. Wat je invult wordt precies zo geplaatst.\nPlaatsen opent het kabelvenster met de gevonden kabels. Daar kun je rijen vóór of na een geselecteerde rij invoegen.\nBewerken en kopiëren behouden bestaande teksten; standaarden worden niet opnieuw toegepast. Mantel: X of een groepscode.", AutoSize = true, MaximumSize = new Size(700, 0) };
        footer.Controls.Add(notes);
        SizeChanged += (_, _) => { notes.MaximumSize = new Size(Math.Max(180, ClientSize.Width - 30), 0); status.MaximumSize = notes.MaximumSize; };
        footer.Controls.Add(status); layout.Controls.Add(footer, 0, 3); Controls.Add(layout); UiTypography.Apply(this);
        Editor.Grid.CellValueChanged += (_, _) => SaveRules(); Editor.Grid.CellEndEdit += (_, _) => SaveRules(); Editor.MantelSpecBox.TextChanged += (_, _) => SaveRules();
        Editor.Grid.EditingControlShowing += (_, args) =>
        {
            if (args.Control is not TextBox edit) return;
            edit.TextChanged -= SaveTypedText; edit.TextChanged += SaveTypedText;
        };
    }
    private void SaveTypedText(object? sender, EventArgs args) => SaveRules(false);
    private bool SaveRules(bool commitEdit = true)
    {
        if (saving) return true;
        saving = true;
        try { var data = Editor.CaptureData(commitEdit); Plugin.Preferences.KabelgeulDefaults = data.Rows.ToDictionary(r => r.Type, StringComparer.OrdinalIgnoreCase); Plugin.Preferences.KabelgeulTextFormat = 1; Plugin.Preferences.KabelgeulMantelSpec = data.MantelSpec; Plugin.Preferences.Save(); Report("Standaardinstellingen opgeslagen."); return true; }
        catch (Exception ex) { Report(ex.Message); return false; }
        finally { saving = false; }
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
            if (!SaveRules()) return;
            KabelgeulCommands.Pending = new(doc);
            doc.SendStringToExecute("(if (not *tbkg:ready*) (load \"" + path + "\"))\n" + command + " ", true, false, false);
        }
        catch (Exception ex) { Report(ex.Message); }
    }
    internal void Report(string message) => status.Text = message;
    internal void RefreshSettings()
    {
        saving = true;
        try { Editor.Set(KabelgeulData.Rules(Plugin.Preferences)); Report("Standaardinstellingen hersteld."); }
        finally { saving = false; }
    }
}
