using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Autodesk.AutoCAD.Geometry;

namespace Techbase.Blocks;

internal sealed class ZroFields
{
    internal string ProjectCode { get; set; } = "";
    internal string TrafoName { get; set; } = "";
    internal string StationNumber { get; set; } = "";
    internal string DrawingNumber { get; set; } = "";
    internal string AreaSquareMeters { get; set; } = "";
    internal string CadastralMunicipality { get; set; } = "";
    internal string Section { get; set; } = "";
    internal string ParcelNumber { get; set; } = "";
    internal string Address { get; set; } = "";
    internal string Place { get; set; } = "";
    internal string Municipality { get; set; } = "";
    internal string OwnerName { get; set; } = "";
    internal string OwnerAddress { get; set; } = "";
    internal string OwnerPostcodePlace { get; set; } = "";
    internal string Department { get; set; } = "Grondzaken";
    internal string Email { get; set; } = "";
    internal string Draftsman { get; set; } = "";
    internal string Date { get; set; } = DateTime.Today.ToString("dd-MM-yyyy", CultureInfo.GetCultureInfo("nl-NL"));
    internal string Note { get; set; } = "";
    internal string Users1 { get; set; } = "";
    internal string Users2 { get; set; } = "";
    internal string Users3 { get; set; } = "";

    internal ZroFields Copy() => (ZroFields)MemberwiseClone();
    internal string MunicipalText => string.IsNullOrWhiteSpace(Municipality) ?
        Place : Place + " gem. " + Municipality;
    internal string Description => ProjectCode.Trim() + " - " + TrafoName.Trim();
    internal string SuggestedLayout => "ZRO-" + TrafoName.Trim();
}

internal sealed record ZroParcelLabel(string Text, Point2d Position);
internal sealed record ZroPdokResult(ZroFields Fields, IReadOnlyList<ZroParcelLabel> Labels, string? Warning);

internal static class ZroPdok
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private const string Reverse = "https://api.pdok.nl/bzk/locatieserver/search/v3_1/reverse";
    private const string Parcels = "https://api.pdok.nl/kadaster/brk-kadastrale-kaart/ogc/v1/collections/perceel/items";

    internal static async Task<ZroPdokResult> FillAsync(Point3d center, ZroFields existing)
    {
        var values = existing.Copy();
        var labels = new List<ZroParcelLabel>();
        var warnings = new List<string>();
        if (center.X is < 0 or > 300000 || center.Y is < 250000 or > 650000)
            return new(values, labels, "RD New (EPSG:28992) is vereist voor PDOK-opzoeking.");

        async Task<JsonElement?> ReverseAt(string kind)
        {
            var url = Reverse + "?X=" + center.X.ToString("F2", CultureInfo.InvariantCulture) +
                "&Y=" + center.Y.ToString("F2", CultureInfo.InvariantCulture) +
                "&type=" + Uri.EscapeDataString(kind) + "&rows=1";
            using var response = await Http.GetAsync(url).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using var data = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            if (data.RootElement.TryGetProperty("response", out var root) &&
                root.TryGetProperty("docs", out var docs) && docs.ValueKind == JsonValueKind.Array &&
                docs.GetArrayLength() > 0)
                return docs[0].Clone();
            return null;
        }

        try
        {
            if (await ReverseAt("adres") is { } a)
            {
                var street = Prop(a, "straatnaam");
                var number = Prop(a, "huisnummer");
                var letter = Prop(a, "huisletter");
                var addition = Prop(a, "huisnummertoevoeging");
                var addr = (street + " " + number + letter + addition).Trim();
                if (addr.Length != 0) values.Address = addr;
                values.Place = Prop(a, "woonplaatsnaam", "woonplaats");
                values.Municipality = Prop(a, "gemeentenaam", "gemeente");
            }
        }
        catch (Exception ex) { warnings.Add("BAG-adres: " + ex.Message); }

        try
        {
            if (await ReverseAt("perceel") is { } c)
            {
                values.CadastralMunicipality = Prop(c, "kadastralegemeentenaam",
                    "kadastrale_gemeente_waarde", "kadastralegemeente");
                values.Section = Prop(c, "sectie", "sectieletter");
                values.ParcelNumber = Prop(c, "perceelnummer", "perceelnummer_waarde");
            }
        }
        catch (Exception ex) { warnings.Add("BRK-perceel: " + ex.Message); }

        try
        {
            // Bovenste viewport beslaat 190 x 140,5 mm op 1:100 = 19 x 14,05 m.
            // Vraag daarom alleen de percelen binnen die werkelijke kaartruimte.
            var bbox = string.Join(",", new[]
            {
                center.X - 9.5, center.Y - 7.025, center.X + 9.5, center.Y + 7.025
            }.Select(x => x.ToString("F2", CultureInfo.InvariantCulture)));
            var rd = Uri.EscapeDataString("http://www.opengis.net/def/crs/EPSG/0/28992");
            var url = Parcels + "?bbox=" + bbox + "&bbox-crs=" + rd + "&crs=" + rd + "&limit=100";
            using var response = await Http.GetAsync(url).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            if (document.RootElement.TryGetProperty("features", out var features) &&
                features.ValueKind == JsonValueKind.Array)
            {
                foreach (var feature in features.EnumerateArray())
                {
                    if (!feature.TryGetProperty("properties", out var props)) continue;
                    var section = Prop(props, "sectie", "sectieletter");
                    var number = Prop(props, "perceelnummer", "perceelnummer_waarde");
                    if (section.Length == 0 || number.Length == 0) continue;
                    if (!feature.TryGetProperty("geometry", out var geometry) ||
                        !TryRepresentativePoint(geometry, out var point)) continue;
                    if (Math.Abs(point.X - center.X) > 9.5 || Math.Abs(point.Y - center.Y) > 7.025)
                        continue;
                    var label = section + " " + number;
                    if (!labels.Any(x => x.Text == label)) labels.Add(new(label, point));
                }
            }
        }
        catch (Exception ex) { warnings.Add("BRK-perceellabels: " + ex.Message); }

        return new(values, labels, warnings.Count == 0 ? null : string.Join(" | ", warnings));
    }

    internal static string Prop(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (element.ValueKind != JsonValueKind.Object) break;
            foreach (var property in element.EnumerateObject())
                if (string.Equals(property.Name, key, StringComparison.OrdinalIgnoreCase))
                    return property.Value.ValueKind switch
                    {
                        JsonValueKind.String => property.Value.GetString() ?? "",
                        JsonValueKind.Number => property.Value.ToString(),
                        _ => ""
                    };
        }
        return "";
    }

    private static bool TryRepresentativePoint(JsonElement geometry, out Point2d point)
    {
        point = default;
        if (!geometry.TryGetProperty("coordinates", out var coordinates)) return false;
        var xs = new List<double>(); var ys = new List<double>();
        void Read(JsonElement node)
        {
            if (node.ValueKind != JsonValueKind.Array || node.GetArrayLength() == 0) return;
            if (node[0].ValueKind == JsonValueKind.Number && node.GetArrayLength() >= 2 &&
                node[1].ValueKind == JsonValueKind.Number)
            {
                var x = node[0].GetDouble(); var y = node[1].GetDouble();
                if (x is > 0 and < 300000 && y is > 250000 and < 650000)
                {
                    xs.Add(x); ys.Add(y);
                }
                return;
            }
            foreach (var child in node.EnumerateArray()) Read(child);
        }
        Read(coordinates);
        if (xs.Count == 0) return false;
        point = new Point2d((xs.Min() + xs.Max()) / 2, (ys.Min() + ys.Max()) / 2);
        return true;
    }
}
