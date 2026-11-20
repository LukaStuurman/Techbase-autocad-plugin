namespace Techbase.Blocks;

internal static class OvRackChoice
{
    internal static LsRackFuses.Selection? Choose(LsRackFuses.Slot[] slots, IEnumerable<int> active, IEnumerable<int> combi, bool placing)
    {
        var automated = Environment.GetEnvironmentVariable("TECHBASE_LS_RACK_TEST_SELECTION");
        if (automated != null && Environment.GetEnvironmentVariable("TECHBASE_TEST_OUTPUT") != null)
        {
            if (automated == "CANCEL") return null;
            if (automated == "EMPTY") return new(Array.Empty<int>(), Array.Empty<int>());
            var choices = automated.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
            return new(choices.Select(s => int.Parse(s.TrimEnd('C', 'c'))).ToArray(), choices.Where(s => s.EndsWith('C') || s.EndsWith('c')).Select(s => int.Parse(s[..^1])).ToArray());
        }
        return Show(slots, active, combi, placing);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static LsRackFuses.Selection? Show(LsRackFuses.Slot[] slots, IEnumerable<int> active, IEnumerable<int> combi, bool placing)
    {
        using var form = Create(slots, active, combi, placing);
        if (Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(form) != DialogResult.OK) return null;
        var grid = form.Controls.OfType<TableLayoutPanel>().Single().Controls.OfType<DataGridView>().Single(); grid.EndEdit();
        var rows = grid.Rows.Cast<DataGridViewRow>().ToArray();
        return new(rows.Where(r => (string)r.Cells[1].Value != "Leeg").Select(r => (int)r.Cells[0].Value).ToArray(),
            rows.Where(r => (string)r.Cells[1].Value == "Combi").Select(r => (int)r.Cells[0].Value).ToArray());
    }

    internal static Form Create(LsRackFuses.Slot[] slots, IEnumerable<int> active, IEnumerable<int> combi, bool placing)
    {
        var form = new Form { Text = "OV — zekeringen per richting", ClientSize = new System.Drawing.Size(440, 450), MinimumSize = new System.Drawing.Size(380, 350), StartPosition = FormStartPosition.CenterParent };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var contacts = string.Join(", ", slots.Where(s => s.Normal).Select(s => s.Contact).Distinct().OrderBy(n => n));
        layout.Controls.Add(new Label { Text = "Leeg: geen zekeringen\nNormaal: rondjes " + contacts + " · Combi: alle vier rondjes", AutoSize = true, Margin = new Padding(0, 0, 0, 10) }, 0, 0);
        var grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, EditMode = DataGridViewEditMode.EditOnEnter };
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Richting", ReadOnly = true });
        var mode = new DataGridViewComboBoxColumn { HeaderText = "Zekeringen", FlatStyle = FlatStyle.Flat }; mode.Items.AddRange("Leeg", "Normaal", "Combi"); grid.Columns.Add(mode);
        var selected = active.ToHashSet(); var combined = combi.ToHashSet();
        foreach (var number in slots.Select(s => s.Number).Distinct().OrderBy(n => n)) grid.Rows.Add(number, combined.Contains(number) ? "Combi" : selected.Contains(number) ? "Normaal" : "Leeg");
        grid.CurrentCellDirtyStateChanged += (_, _) => { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        layout.Controls.Add(grid, 0, 1);
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var accept = new ThemedButton { Text = "Toepassen", AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new ThemedButton { Text = placing ? "Leeg laten" : "Annuleren", AutoSize = true, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(accept); buttons.Controls.Add(cancel); layout.Controls.Add(buttons, 0, 2);
        form.Controls.Add(layout); form.AcceptButton = accept; form.CancelButton = cancel;
        UiTypography.Apply(form); UiTheme.Primary(accept); return form;
    }
}
