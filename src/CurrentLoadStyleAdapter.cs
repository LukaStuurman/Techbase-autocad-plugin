using System.Drawing;
using Enexis.KabelChecker.Core;

namespace Enexis.KabelChecker.AutoCAD;

internal sealed partial class CurrentLoadPanel
{
    internal void CompactResults(TableLayoutPanel overview, TableLayoutPanel status, ToolTip tips)
    {
        // Reuse the space previously split between the grid footer and the
        // status strip, preserving the visible input rows.
        overview.Controls.Remove(_total);
        overview.RowStyles[1].SizeType = SizeType.Absolute; overview.RowStyles[1].Height = 0;
        status.SuspendLayout(); status.Controls.Clear();
        status.AutoSize = false; status.AutoScroll = false;
        status.RowCount = 2; status.ColumnCount = 2;
        status.Padding = Padding.Empty; status.Margin = Padding.Empty;
        status.ColumnStyles.Clear();
        status.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        status.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        status.RowStyles.Clear();
        status.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        status.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        foreach (var label in new[] { _total, _details })
        {
            label.AutoSize = false; label.AutoEllipsis = true;
            label.MaximumSize = Size.Empty; label.Padding = Padding.Empty;
            label.Margin = new Padding(1, 0, 4, 0); label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
        }
        status.Controls.Add(_total, 0, 0); status.Controls.Add(_details, 0, 1);
        var resultFont = new Font(Techbase.Blocks.Typography.Family, 16, FontStyle.Bold);
        var result = new Label
        {
            Name = "CurrentLoadFitResult", Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter, Font = resultFont,
            Margin = new Padding(6, 0, 0, 0), AccessibleName = "Toets ontwerpstroom"
        };
        status.Controls.Add(result, 1, 0); status.SetRowSpan(result, 2);
        void UpdateResult()
        {
            var mapped = GetMappedLoads();
            bool? fits = mapped != null && HasInputs && _calculation != null
                ? _calculation.MaxDesignCurrentAmps is int max && DesignCurrentCalculator.Calculate(KaderVersionSelection.Current, mapped).CableCurrent(KaderVersionSelection.CurrentMode) <= max + 1e-9
                : null;
            result.Text = fits is true ? "PAST" : fits is false ? "PAST NIET" : "";
            result.ForeColor = fits is false ? Color.Firebrick : Color.SeaGreen;
            result.AccessibleDescription = _assessment.Text;
            tips.SetToolTip(result, _assessment.Text);
            tips.SetToolTip(_total, _total.Text + "\n" + _stationTotal.Text);
            tips.SetToolTip(_details, _details.Text);
            // Upstream resizing can restore a maximum width for details.
            _details.MaximumSize = Size.Empty;
        }
        _assessment.TextChanged += (_, _) => UpdateResult();
        _details.TextChanged += (_, _) => UpdateResult();
        _total.TextChanged += (_, _) => UpdateResult();
        _stationTotal.TextChanged += (_, _) => UpdateResult();
        status.SizeChanged += (_, _) => _details.MaximumSize = Size.Empty;
        Resize += (_, _) => _details.MaximumSize = Size.Empty;
        Disposed += (_, _) => { _assessment.Dispose(); _stationTotal.Dispose(); resultFont.Dispose(); };
        UpdateResult(); status.ResumeLayout(true);
    }
}
