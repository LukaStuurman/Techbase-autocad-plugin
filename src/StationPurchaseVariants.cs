using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Techbase.Blocks;

/// <summary>
/// Maakt AANKOOP-uitvoeringen van de trafo's uit de Blocks-toolpalette.
/// De oorspronkelijke blokdefinities blijven volledig onaangetast.
/// De vrije ruimte en de groene arcering volgen de bestaande stationslocatie:
/// ANSI31, ACI 102, schaal 0,1, hoek -90°, en asymmetrische marges in meters.
/// </summary>
internal static class StationPurchaseVariants
{
    internal const string Suffix = " AANKOOP";
    private const string MetadataKey = "TB_STATION_AANKOOP_2026";
    private const short Green = 102;
    private const double Left = 2.14, Right = 0.40, Top = 0.40, Bottom = 1.91;

    internal static bool IsPurchaseName(string name) =>
        name.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase);

    internal static bool IsStationTool(BlockTool tool) =>
        string.Equals(tool.Layer.Trim(), "01 - Station", StringComparison.OrdinalIgnoreCase) ||
        tool.Group.Contains("trafo", StringComparison.OrdinalIgnoreCase) ||
        tool.Group.Contains("station", StringComparison.OrdinalIgnoreCase);

    internal static BlockTool[] AddToCatalog(IEnumerable<BlockTool> original)
    {
        var result = original.ToList();
        foreach (var tool in original.Where(t => IsStationTool(t) && !IsPurchaseName(t.BlockName)))
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(tool.Id + "|STATION-AANKOOP"));
            var id = new Guid(hash.AsSpan(0, 16)).ToString("N");
            if (result.Any(t => t.Id == id ||
                t.BlockName.Equals(tool.BlockName + Suffix, StringComparison.OrdinalIgnoreCase)))
                continue;
            result.Add(tool with
            {
                Id = id,
                Name = tool.Name + Suffix,
                BlockName = tool.BlockName + Suffix,
                Explode = false
            });
        }
        return result.ToArray();
    }

    /// <summary>Haal het oorspronkelijke blok op en voeg de groene aankoopvorm toe.</summary>
    internal static ObjectId Import(Database db, Catalog catalog, BlockTool tool)
    {
        if (!IsPurchaseName(tool.BlockName))
            throw new ArgumentException("Selecteer een AANKOOP-variant.", nameof(tool));
        var original = tool.BlockName[..^Suffix.Length];
        var baseId = SourceBlockImporter.Import(db, catalog.ResolveSource(tool), original,
            Plugin.Preferences.BlockTextHeight(original), Plugin.Preferences);
        return Ensure(db, baseId, tool.BlockName);
    }

    internal static ObjectId Ensure(Database db, ObjectId baseId, string name)
    {
        if (!IsPurchaseName(name)) throw new InvalidOperationException("Naam moet eindigen op AANKOOP.");
        using var tx = db.TransactionManager.StartTransaction();
        var table = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
        var source = (BlockTableRecord)tx.GetObject(baseId, OpenMode.ForRead);
        if (source.IsLayout || source.IsAnonymous || source.IsFromExternalReference)
            throw new InvalidOperationException("Een normaal, statisch trafoblok is vereist.");

        if (table.Has(name))
        {
            var existing = (BlockTableRecord)tx.GetObject(table[name], OpenMode.ForRead);
            if (!HasMarker(tx, existing))
                throw new InvalidOperationException("Er is al een zelfgemaakt blok met de naam '" +
                    name + "'. Hernoem dit eerst om overschrijven te voorkomen.");
            return existing.ObjectId;
        }

        var footprint = FindFootprint(tx, source);
        var units = source.Units == UnitsValue.Undefined ? 1 :
            UnitsConverter.GetConversionFactor(UnitsValue.Meters, source.Units);
        if (!double.IsFinite(units) || units <= 0) units = 1;
        var space = new BlockTableRecord { Name = name, Origin = source.Origin, Units = source.Units };
        table.UpgradeOpen();
        var targetId = table.Add(space);
        tx.AddNewlyCreatedDBObject(space, true);

        var bounds = new[]
        {
            new Point2d(footprint.MinX - Left * units, footprint.MinY - Bottom * units),
            new Point2d(footprint.MaxX + Right * units, footprint.MinY - Bottom * units),
            new Point2d(footprint.MaxX + Right * units, footprint.MaxY + Top * units),
            new Point2d(footprint.MinX - Left * units, footprint.MaxY + Top * units)
        };

        // Het groene vlak ligt onder het originele block: de details,
        // contouren en teksten van de trafo blijven daardoor zichtbaar.
        var hatch = new Hatch { ColorIndex = Green, Associative = false };
        space.AppendEntity(hatch);
        tx.AddNewlyCreatedDBObject(hatch, true);
        hatch.SetHatchPattern(HatchPatternType.PreDefined, "ANSI31");
        hatch.PatternAngle = -Math.PI / 2;
        hatch.PatternScale = 0.1 * units;
        var closed = new Point2dCollection([.. bounds, bounds[0]]);
        hatch.AppendLoop(HatchLoopTypes.External, closed, new DoubleCollection(new double[closed.Count]));
        hatch.SetHatchPattern(HatchPatternType.PreDefined, "ANSI31");
        hatch.EvaluateHatch(true);

        var edge = new Polyline(4) { Closed = true, ColorIndex = Green };
        foreach (var point in bounds) edge.AddVertexAt(edge.NumberOfVertices, point, 0, 0, 0);
        space.AppendEntity(edge);
        tx.AddNewlyCreatedDBObject(edge, true);

        // Geen overname van dimensions in de acquisition-geometry zelf:
        // de oorspronkelijke blokentiteiten worden ongewijzigd gekopieerd.
        foreach (ObjectId id in source)
        {
            if (id.IsNull || !id.IsValid || id.IsErased) continue;
            if (tx.GetObject(id, OpenMode.ForRead) is not Entity original) continue;
            var entity = (Entity)original.Clone();
            space.AppendEntity(entity);
            tx.AddNewlyCreatedDBObject(entity, true);
        }

        space.CreateExtensionDictionary();
        var dict = (DBDictionary)tx.GetObject(space.ExtensionDictionary, OpenMode.ForWrite);
        var marker = new Xrecord
        {
            Data = new ResultBuffer(new TypedValue((int)DxfCode.Text, source.Name))
        };
        dict.SetAt(MetadataKey, marker);
        tx.AddNewlyCreatedDBObject(marker, true);
        tx.Commit();
        return targetId;
    }

    private static bool HasMarker(Transaction tx, BlockTableRecord block)
    {
        if (block.ExtensionDictionary.IsNull) return false;
        var dict = (DBDictionary)tx.GetObject(block.ExtensionDictionary, OpenMode.ForRead);
        return dict.Contains(MetadataKey);
    }

    private readonly record struct Bounds(double MinX, double MinY, double MaxX, double MaxY)
    {
        internal double Area => (MaxX - MinX) * (MaxY - MinY);
    }

    private static Bounds FindFootprint(Transaction tx, BlockTableRecord definition)
    {
        var candidates = new List<Bounds>();
        foreach (ObjectId id in definition)
        {
            if (tx.GetObject(id, OpenMode.ForRead) is not Polyline poly ||
                !poly.Closed || poly.NumberOfVertices != 4) continue;
            var p = Enumerable.Range(0, 4).Select(poly.GetPoint2dAt).ToArray();
            var edges = Enumerable.Range(0, 4).Select(i => p[(i + 1) % 4] - p[i]).ToArray();
            if (edges.Any(e => e.Length < 0.0001)) continue;
            if (Enumerable.Range(0, 4).Any(i =>
                Math.Abs(edges[i].DotProduct(edges[(i + 1) % 4])) >
                edges[i].Length * edges[(i + 1) % 4].Length * 0.04)) continue;
            candidates.Add(new Bounds(p.Min(x => x.X), p.Min(x => x.Y),
                p.Max(x => x.X), p.Max(x => x.Y)));
        }
        var pick = candidates.OrderByDescending(x => x.Area).FirstOrDefault();
        if (pick.Area > 0.01) return pick;

        // Sommige paletteblokken hebben losse lijnen in plaats van een
        // gesloten outline. Begrens dan uitsluitend de aanwezige
        // polyline/solid geometrie, niet hun maatteksten en maatlijnen.
        var extents = new List<Extents3d>();
        foreach (ObjectId id in definition)
        {
            if (tx.GetObject(id, OpenMode.ForRead) is not Entity entity ||
                entity is not (Polyline or Solid or Region)) continue;
            try { extents.Add(entity.GeometricExtents); }
            catch (Autodesk.AutoCAD.Runtime.Exception) { }
        }
        if (extents.Count > 0)
        {
            pick = new Bounds(extents.Min(x => x.MinPoint.X), extents.Min(x => x.MinPoint.Y),
                extents.Max(x => x.MaxPoint.X), extents.Max(x => x.MaxPoint.Y));
            if (pick.Area > 0.01) return pick;
        }
        throw new InvalidOperationException("Voor " + definition.Name +
            " kon geen bruikbare rechthoek worden gevonden. Voeg een gesloten trafo-omtrek toe.");
    }
}
