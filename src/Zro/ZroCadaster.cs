using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using Autodesk.AutoCAD.Geometry;

namespace Techbase.Blocks.Zro;

internal sealed record ZroParcel(string Municipality, string Section, string Number, Point2d LabelPoint, bool ContainsStation)
{
    internal string Label => (Section + " " + Number).Trim();
}

internal sealed record ZroPdokResult(
    string Address, string Place, string Municipality, ZroParcel? StationParcel,
    IReadOnlyList<ZroParcel> SurroundingParcels, string Warning);

internal static class ZroCadaster
{
    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private const string ParcelEndpoint =
        "https://api.pdok.nl/kadaster/brk-kadastrale-kaart/ogc/v1/collections/perceel/items";
    private const string ReverseEndpoint = "https://api.pdok.nl/bzk/locatieserver/search/v3_1/reverse";

    internal static ZroPdokResult Lookup(Point3d position)
    {
        if (position.X is < -10000 or > 300000 || position.Y is < 280000 or > 650000)
            return new("", "", "", null, [], "Voor automatisch invullen zijn RD-coördinaten in meters nodig.");
        var warnings = new List<string>();
        string address = "", place = "", municipality = "";
        try
        {
            var url = FormattableString.Invariant(
                $"{ReverseEndpoint}?X={position.X:0.###}&Y={position.Y:0.###}&type=adres&rows=1&distance=1000");
            using var doc = JsonDocument.Parse(Client.GetStringAsync(url).GetAwaiter().GetResult());
            var docs = doc.RootElement.GetProperty("response").GetProperty("docs");
            if (docs.GetArrayLength() > 0)
            {
                var p = docs[0];
                var street = String(p, "straatnaam");
                var number = String(p, "huisnummer");
                var suffix = String(p, "huisletter") + String(p, "huisnummertoevoeging");
                address = (street + " " + number + suffix).Trim();
                place = String(p, "woonplaatsnaam");
                municipality = String(p, "gemeentenaam");
                if (string.IsNullOrWhiteSpace(address))
                {
                    var display = String(p, "weergavenaam");
                    address = display.Split(',')[0].Trim();
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or
            JsonException or InvalidOperationException or KeyNotFoundException)
        { warnings.Add("PDOK vond geen automatisch adres; adres en gemeente zelf invullen."); }

        IReadOnlyList<ZroParcel> parcels = [];
        try { parcels = GetParcels(position, 24); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or
            JsonException or InvalidOperationException or KeyNotFoundException)
        { warnings.Add("PDOK-percelen niet beschikbaar; vul kadastrale gegevens handmatig in."); }

        var selected = parcels.FirstOrDefault(p => p.ContainsStation);
        if (selected == null) warnings.Add("Controleer de kadastrale gemeente, sectie en perceelnummer bij een perceelgrens.");
        return new(address, place, municipality, selected, parcels, string.Join(" ", warnings));
    }

    internal static IReadOnlyList<ZroParcel> GetParcels(Point3d station, double radius)
    {
        if (radius is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(radius));
        // Open PDOK OGC API Features: bbox en geometrie in RD (EPSG:28992).
        // Geen enkel perceel wordt als 'eigenaar' geïnterpreteerd.
        var bbox = string.Join(",", new[]
        {
            station.X - radius, station.Y - radius, station.X + radius, station.Y + radius
        }.Select(value => value.ToString("0.###", Invariant)));
        const string crs = "http://www.opengis.net/def/crs/EPSG/0/28992";
        var url = ParcelEndpoint + "?f=json&limit=250&bbox=" + bbox +
            "&bbox-crs=" + Uri.EscapeDataString(crs) + "&crs=" + Uri.EscapeDataString(crs);
        using var json = JsonDocument.Parse(Client.GetStringAsync(url).GetAwaiter().GetResult());
        if (!json.RootElement.TryGetProperty("features", out var features) ||
            features.ValueKind != JsonValueKind.Array) return [];

        var results = new List<ZroParcel>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var feature in features.EnumerateArray())
        {
            if (!feature.TryGetProperty("properties", out var props) ||
                !feature.TryGetProperty("geometry", out var geometry)) continue;
            var section = String(props, "sectie");
            var number = String(props, "perceelnummer");
            var cadastral = String(props, "kadastrale_gemeente_waarde");
            if (section.Length == 0 || number.Length == 0) continue;
            var id = cadastral + "|" + section + "|" + number;
            if (!seen.Add(id)) continue;
            var rings = ReadOuterRings(geometry).ToArray();
            if (rings.Length == 0) continue;
            var center = new Point2d(station.X, station.Y);
            var containing = rings.FirstOrDefault(ring => Contains(ring, center));
            var ringForLabel = containing ?? rings.OrderByDescending(PolygonArea).First();
            var anchor = Center(ringForLabel);
            if (anchor.X is < 0 or > 300000 || anchor.Y is < 280000 or > 650000)
                throw new InvalidOperationException("PDOK heeft geen RD-geometrie geleverd; perceellabels worden niet gegokt.");
            results.Add(new(cadastral, section, number, anchor, containing != null));
        }
        return results;
    }

    private static string String(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var prop) || prop.ValueKind == JsonValueKind.Null) return "";
        return prop.ValueKind == JsonValueKind.String ? prop.GetString() ?? "" : prop.ToString();
    }

    private static IEnumerable<Point2d[]> ReadOuterRings(JsonElement geometry)
    {
        if (!geometry.TryGetProperty("type", out var t) ||
            !geometry.TryGetProperty("coordinates", out var c)) yield break;
        var type = t.GetString();
        IEnumerable<JsonElement> polygons = type switch
        {
            "Polygon" => new[] { c },
            "MultiPolygon" => c.EnumerateArray(),
            _ => []
        };
        foreach (var polygon in polygons)
        {
            if (polygon.ValueKind != JsonValueKind.Array || polygon.GetArrayLength() == 0) continue;
            var outer = polygon[0];
            var points = new List<Point2d>();
            foreach (var point in outer.EnumerateArray())
            {
                if (point.GetArrayLength() < 2) continue;
                points.Add(new Point2d(point[0].GetDouble(), point[1].GetDouble()));
            }
            if (points.Count > 2) yield return points.ToArray();
        }
    }

    private static bool Contains(IReadOnlyList<Point2d> polygon, Point2d point)
    {
        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var a = polygon[i]; var b = polygon[j];
            if ((a.Y > point.Y) == (b.Y > point.Y)) continue;
            var x = (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X;
            if (point.X < x) inside = !inside;
        }
        return inside;
    }

    private static double PolygonArea(IReadOnlyList<Point2d> ring)
    {
        double area = 0;
        for (var i = 0; i < ring.Count; i++)
        {
            var a = ring[i]; var b = ring[(i + 1) % ring.Count];
            area += a.X * b.Y - b.X * a.Y;
        }
        return Math.Abs(area) / 2;
    }

    private static Point2d Center(IReadOnlyList<Point2d> ring)
    {
        // Gebruik eerst het centrum; ligt dat buiten het perceel, plaats het
        // label dichter bij het eerste hoekpunt. Geen labels buiten het perceel.
        var mid = new Point2d(ring.Average(p => p.X), ring.Average(p => p.Y));
        if (Contains(ring, mid)) return mid;
        var first = ring[0];
        return new Point2d((mid.X + first.X) / 2, (mid.Y + first.Y) / 2);
    }
}
