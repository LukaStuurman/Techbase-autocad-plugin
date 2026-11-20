using System.Globalization;
using System.Text.Json;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Techbase.Blocks.Zro;

/// <summary>Invoer die voor ieder ZRO-blad opnieuw aangepast kan worden.</summary>
internal sealed class ZroFields
{
    public string ProjectCode { get; set; } = "";
    public string StationName { get; set; } = "";
    public string StationNumber { get; set; } = "";
    public string DrawingNumber { get; set; } = "";
    public string Drafter { get; set; } = "";
    public string Department { get; set; } = "";
    public string Email { get; set; } = "";
    public string Address { get; set; } = "";
    public string Place { get; set; } = "";
    public string Municipality { get; set; } = "";
    public string CadastralMunicipality { get; set; } = "";
    public string Section { get; set; } = "";
    public string ParcelNumber { get; set; } = "";
    public string OwnerName { get; set; } = "";
    public string OwnerAddress { get; set; } = "";
    public string OwnerPostcodeCity { get; set; } = "";
    public string UserName { get; set; } = "";
    public string UserAddress { get; set; } = "";
    public string UserPostcodeCity { get; set; } = "";
    public string Date { get; set; } = DateTime.Now.ToString("dd-MM-yyyy", CultureInfo.GetCultureInfo("nl-NL"));
    public string Notes { get; set; } = "";
    public string TemplatePath { get; set; } = "";

    public string Description => ProjectCode.Trim() + " - " + StationName.Trim();
    public string MunicipalityLabel => string.IsNullOrWhiteSpace(Place) ? Municipality :
        string.IsNullOrWhiteSpace(Municipality) ? Place :
        Place.Trim() + " gem. " + Municipality.Trim();

    internal Dictionary<string, string> Attributes(string blockName, ZroFootprint footprint, int index = 1, int count = 1)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["KAD.GEM"] = CadastralMunicipality.Trim(),
            ["SECTIE.L"] = Section.Trim(),
            ["SECTIE.NR"] = ParcelNumber.Trim(),
            ["EIGENDOMR1"] = OwnerName.Trim(),
            ["EIGENDOMR2"] = OwnerAddress.Trim(),
            ["EIGENDOMR3"] = OwnerPostcodeCity.Trim(),
            ["GEBRUIKERR1"] = UserName.Trim(),
            ["GEBRUIKERR2"] = UserAddress.Trim(),
            ["GEBRUIKERR3"] = UserPostcodeCity.Trim(),
            ["OPP.IN.M"] = Math.Round(footprint.Area, 0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture),
            ["type.stat"] = blockName,
            ["FUNCTIEPLAATSNUMMER"] = Department.Trim(),
            ["schaal.sit"] = "schaal 1:100",
            ["schaal.over"] = "schaal 1:1000",
            ["TEKENINGNUMMER:"] = DrawingNumber.Trim(),
            ["EMAILADRES:"] = Email.Trim(),
            ["OMSCHRIJVING{108}"] = Description,
            ["ADRES{108}"] = Address.Trim(),
            ["GEMEENTE{108}"] = MunicipalityLabel,
            ["MSR.NR.{55}"] = StationNumber.Trim(),
            ["STATIONSNUMMER"] = StationNumber.Trim(),
            ["FORMAAT"] = "A4",
            ["GET.{33}"] = Drafter.Trim(),
            ["GEZIEN{33}"] = "",
            ["DATUM"] = Date.Trim(),
            ["OPMERKING{80}"] = Notes.Trim(),
            ["AANTALBLADEN"] = count.ToString(CultureInfo.InvariantCulture),
            ["BLADNR"] = index.ToString(CultureInfo.InvariantCulture),
            ["WIJZ1"] = "",
            ["DATUM1"] = "",
            ["PAR{7}"] = ""
        };
        for (var i = 0; i < Math.Min(4, footprint.Corners.Length); i++)
        {
            var corner = footprint.Corners[i];
            values["COÖRDINAAT_C" + (i + 1) + "_X"] = corner.X.ToString("F3", CultureInfo.InvariantCulture);
            values["COÖRDINAAT_C" + (i + 1) + "_Y"] = corner.Y.ToString("F3", CultureInfo.InvariantCulture);
        }
        return values;
    }

    // Persoonlijke en kadastrale bladvelden worden niet met andere projecten
    // gedeeld. Alleen de werkstandaarden worden lokaal onthouden.
    private static readonly string DefaultsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Techbase", "ZRO", "instellingen.json");

    internal static ZroFields LoadDefaults()
    {
        try
        {
            if (File.Exists(DefaultsFile))
                return JsonSerializer.Deserialize<ZroFields>(File.ReadAllText(DefaultsFile)) ?? new();
        }
        catch (Exception) { /* start met lege waarden */ }
        return new();
    }

    internal void SaveDefaults()
    {
        var defaults = new ZroFields
        {
            ProjectCode = ProjectCode, Drafter = Drafter, Department = Department,
            Email = Email, TemplatePath = TemplatePath
        };
        Directory.CreateDirectory(Path.GetDirectoryName(DefaultsFile)!);
        File.WriteAllText(DefaultsFile, JsonSerializer.Serialize(defaults,
            new JsonSerializerOptions { WriteIndented = true }));
    }
}

internal sealed record ZroFootprint(Point3d[] Corners, double Area, string BlockType, bool Estimated)
{
    internal static ZroFootprint FromStation(Transaction tr, BlockReference station)
    {
        var id = station.IsDynamicBlock ? station.DynamicBlockTableRecord : station.BlockTableRecord;
        var block = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);
        var blockType = block.Name;
        Point3d[]? chosen = null;
        var max = 0d;
        foreach (ObjectId entityId in block)
        {
            if (tr.GetObject(entityId, OpenMode.ForRead) is not Polyline { Closed: true, NumberOfVertices: 4 } border)
                continue;
            var corners = Enumerable.Range(0, 4).Select(i =>
                new Point3d(border.GetPoint2dAt(i).X, border.GetPoint2dAt(i).Y, border.Elevation)
                    .TransformBy(station.BlockTransform)).ToArray();
            var a = corners[1] - corners[0];
            var b = corners[2] - corners[1];
            var c = corners[3] - corners[2];
            if (a.Length < 0.01 || b.Length < 0.01 ||
                Math.Abs(a.DotProduct(b)) > a.Length * b.Length * 0.04 ||
                Math.Abs(b.DotProduct(c)) > b.Length * c.Length * 0.04) continue;
            var area = a.CrossProduct(b).Length;
            if (area <= max) continue;
            chosen = corners;
            max = area;
        }

        if (chosen == null)
        {
            // Een block zonder rechthoekige voetafdruk krijgt een expliciet
            // geschatte omvang. De gebruiker kan dit in het blad controleren.
            var e = station.GeometricExtents;
            max = Math.Abs((e.MaxPoint.X - e.MinPoint.X) * (e.MaxPoint.Y - e.MinPoint.Y));
            chosen =
            [
                new Point3d(e.MinPoint.X, e.MaxPoint.Y, 0),
                new Point3d(e.MaxPoint.X, e.MaxPoint.Y, 0),
                new Point3d(e.MaxPoint.X, e.MinPoint.Y, 0),
                new Point3d(e.MinPoint.X, e.MinPoint.Y, 0)
            ];
            return new ZroFootprint(chosen, max, blockType, true);
        }
        var center = new Point3d(chosen.Average(x => x.X), chosen.Average(x => x.Y), 0);
        var ordered = chosen.OrderByDescending(p => Math.Atan2(p.Y - center.Y, p.X - center.X)).ToArray();
        var topLeft = Enumerable.Range(0, 4)
            .OrderByDescending(i => ordered[i].Y).ThenBy(i => ordered[i].X).First();
        var result = Enumerable.Range(0, 4).Select(i => ordered[(topLeft + i) % 4]).ToArray();
        return new ZroFootprint(result, max, blockType, false);
    }
}
