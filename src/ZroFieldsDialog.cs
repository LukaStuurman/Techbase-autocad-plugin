using System.Drawing;
using System.Windows.Forms;
using Autodesk.AutoCAD.Geometry;

namespace Techbase.Blocks;

internal sealed class ZroFieldsDialog : Form
{
    private readonly Dictionary<string, TextBox> editors = new(StringComparer.OrdinalIgnoreCase);
    private readonly Point3d stationPoint;
    private readonly ZroFields draft;
    private readonly Label feedback = new()
    {
        AutoSize = true, ForeColor = UiTheme.Muted,
        MaximumSize = new Size(660, 0)
    };
    internal ZroFields? Result { get; private set; }
    internal IReadOnlyList<ZroParcelLabel> Labels { get; private set; } = Array.Empty<ZroParcelLabel>();

    internal ZroFieldsDialog(ZroFields values, Point3d center, string type, double area)
    {
        stationPoint = center;
        draft = values.Copy();
        Text = "TechBase | ZRO-blad — gegevens controleren";
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(835, 770);
        MinimumSize = new Size(620, 570);
        BackColor = UiTheme.Canvas;
        ForeColor = UiTheme.Ink;
        Font = new Font(Typography.Family, 9.5f);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1,
            Padding = new Padding(14), Margin = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true,
            ColumnCount = 1, Padding = Padding.Empty
        };
        header.Controls.Add(new Label
        {
            Text = "ZRO-blad aanmaken", AutoSize = true,
            Font = new Font(Typography.Family, 17)
        });
        header.Controls.Add(new Label
        {
            Text = $"Gekozen trafoblok: {type}    ·    Oppervlakte grootste rechthoek: {area:F2} m²",
            AutoSize = true, ForeColor = UiTheme.Muted
        });
        var autofill = new Button
        {
            Text = "Adres en kadastrale gegevens ophalen (PDOK)",
            Height = 38, Width = 355, FlatStyle = FlatStyle.Flat,
            AutoSize = false, Margin = new Padding(0, 10, 0, 8),
            BackColor = Color.White
        };
        header.Controls.Add(autofill);
        header.Controls.Add(feedback);
        root.Controls.Add(header, 0, 0);

        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var columns = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2,
            RowCount = 1, Padding = new Padding(0, 8, 0, 8),
        };
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var project = Column();
        var location = Column();
        var owners = Column();
        Section(project, "Project & tekening");
        Field(project, "Projectcode / projectnummer", "ProjectCode", draft.ProjectCode);
        Field(project, "Trafo-naam", "TrafoName", draft.TrafoName);
        Field(project, "Stationsnummer (MSR.NR.)", "StationNumber", draft.StationNumber);
        Field(project, "Tekeningnummer", "DrawingNumber", draft.DrawingNumber);
        Field(project, "Tekenaar (voorletter.achternaam)", "Draftsman", draft.Draftsman);
        Field(project, "Afdeling", "Department", draft.Department);
        Field(project, "E-mailadres", "Email", draft.Email);
        Field(project, "Datum", "Date", draft.Date);

        Section(location, "Adres & kadastrale aanduiding");
        Field(location, "Adres (straat, nummer, toevoeging)", "Address", draft.Address);
        Field(location, "Plaatsnaam", "Place", draft.Place);
        Field(location, "Gemeentenaam (normale gemeente)", "Municipality", draft.Municipality);
        Field(location, "Kadastrale gemeente (Kad.Gem)", "CadastralMunicipality", draft.CadastralMunicipality);
        Field(location, "Sectieletter", "Section", draft.Section);
        Field(location, "Perceelnummer", "ParcelNumber", draft.ParcelNumber);

        Section(owners, "Eigendom & overige gegevens");
        Field(owners, "Eigenaar naam (EIGENDOMR1)", "OwnerName", draft.OwnerName);
        Field(owners, "Eigenaar straat + nummer (EIGENDOMR2)", "OwnerAddress", draft.OwnerAddress);
        Field(owners, "Eigenaar postcode + plaats (EIGENDOMR3)", "OwnerPostcodePlace", draft.OwnerPostcodePlace);
        Field(owners, "Gebruiker regel 1", "Users1", draft.Users1);
        Field(owners, "Gebruiker regel 2", "Users2", draft.Users2);
        Field(owners, "Gebruiker regel 3", "Users3", draft.Users3);
        Field(owners, "Opmerking", "Note", draft.Note);

        var left = new Panel { Dock = DockStyle.Fill, AutoScroll = false };
        var right = new Panel { Dock = DockStyle.Fill, AutoScroll = false };
        left.Controls.Add(project);
        right.Controls.Add(location);
        right.Controls.Add(owners);
        location.Dock = DockStyle.Top;
        owners.Dock = DockStyle.Top;
        owners.BringToFront();
        location.BringToFront();
        project.Dock = DockStyle.Top;
        columns.Controls.Add(left, 0, 0);
        columns.Controls.Add(right, 1, 0);
        columns.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        scroll.Controls.Add(columns);
        root.Controls.Add(scroll, 0, 1);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
            Margin = Padding.Empty, Padding = new Padding(0, 7, 0, 0)
        };
        var save = new Button
        {
            Text = "ZRO-blad maken", DialogResult = DialogResult.None,
            Width = 155, Height = 36, BackColor = UiTheme.Orange,
            FlatStyle = FlatStyle.Flat
        };
        var cancel = new Button { Text = "Annuleren", DialogResult = DialogResult.Cancel, Width = 110, Height = 36 };
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        root.Controls.Add(buttons, 0, 2);
        Controls.Add(root);
        CancelButton = cancel;

        autofill.Click += async (_, _) =>
        {
            autofill.Enabled = false;
            feedback.Text = "PDOK: gegevens ophalen…";
            try
            {
                var result = await ZroPdok.FillAsync(stationPoint, Read());
                ApplyValues(result.Fields);
                Labels = result.Labels;
                feedback.Text = result.Warning == null
                    ? $"PDOK: adres en perceel ingevuld; controleer de waarden voor het opslaan."
                    : "PDOK gedeeltelijk beschikbaar: " + result.Warning +
                      " — ontbrekende velden handmatig invullen.";
            }
            catch (Exception ex) { feedback.Text = "PDOK niet bereikbaar: " + ex.Message; }
            finally { autofill.Enabled = true; }
        };
        save.Click += (_, _) =>
        {
            var details = Read();
            if (string.IsNullOrWhiteSpace(details.ProjectCode) ||
                string.IsNullOrWhiteSpace(details.TrafoName) ||
                string.IsNullOrWhiteSpace(details.DrawingNumber) ||
                string.IsNullOrWhiteSpace(details.Draftsman))
            {
                MessageBox.Show("Vul minimaal projectcode, trafonaam, tekeningnummer en tekenaar in.",
                    "ZRO-blad", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Result = details;
            DialogResult = DialogResult.OK;
            Close();
        };
        UiTypography.Apply(this);
    }

    private static TableLayoutPanel Column() => new()
    {
        Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1,
        Padding = new Padding(6), Margin = new Padding(0, 0, 8, 0)
    };

    private static void Section(TableLayoutPanel panel, string title) =>
        panel.Controls.Add(new Label
        {
            Text = title, AutoSize = true, UseMnemonic = false,
            ForeColor = UiTheme.Ink, Font = new Font(Typography.Family, 11, FontStyle.Bold),
            Margin = new Padding(0, 8, 0, 9)
        });

    private void Field(TableLayoutPanel panel, string title, string key, string value)
    {
        var label = new Label
        {
            Text = title, AutoSize = true, Margin = new Padding(0, 4, 0, 2)
        };
        var input = new TextBox
        {
            Text = value, Dock = DockStyle.Top, Height = 28,
            AccessibleName = title, Margin = new Padding(0, 0, 0, 8)
        };
        panel.Controls.Add(label);
        panel.Controls.Add(input);
        editors.Add(key, input);
    }

    private string Get(string key) => editors[key].Text.Trim();

    private ZroFields Read() => new()
    {
        ProjectCode = Get("ProjectCode"),
        TrafoName = Get("TrafoName"),
        StationNumber = Get("StationNumber"),
        DrawingNumber = Get("DrawingNumber"),
        Draftsman = Get("Draftsman"),
        Department = Get("Department"),
        Email = Get("Email"),
        Date = Get("Date"),
        Address = Get("Address"),
        Place = Get("Place"),
        Municipality = Get("Municipality"),
        CadastralMunicipality = Get("CadastralMunicipality"),
        Section = Get("Section"),
        ParcelNumber = Get("ParcelNumber"),
        OwnerName = Get("OwnerName"),
        OwnerAddress = Get("OwnerAddress"),
        OwnerPostcodePlace = Get("OwnerPostcodePlace"),
        Users1 = Get("Users1"),
        Users2 = Get("Users2"),
        Users3 = Get("Users3"),
        Note = Get("Note")
    };

    private void ApplyValues(ZroFields values)
    {
        editors["Address"].Text = values.Address;
        editors["Place"].Text = values.Place;
        editors["Municipality"].Text = values.Municipality;
        editors["CadastralMunicipality"].Text = values.CadastralMunicipality;
        editors["Section"].Text = values.Section;
        editors["ParcelNumber"].Text = values.ParcelNumber;
    }
}
