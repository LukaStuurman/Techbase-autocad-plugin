using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Techbase.Blocks;

internal sealed record ZroFootprint(
    string BlockType, double Area, Point3d[] Corners, Point3d Center, ObjectId StationId);

internal static class ZroGeometry
{
    internal static ZroFootprint Inspect(Database database, ObjectId stationId)
    {
        using var tx = database.TransactionManager.StartTransaction();
        var station = (BlockReference)tx.GetObject(stationId, OpenMode.ForRead);
        var definition = (BlockTableRecord)tx.GetObject(
            station.IsDynamicBlock ? station.DynamicBlockTableRecord : station.BlockTableRecord,
            OpenMode.ForRead);
        var type = definition.Name;
        var rectangles = new List<(double Area, Point3d[] Corners)>();
        foreach (ObjectId id in definition)
        {
            if (tx.GetObject(id, OpenMode.ForRead) is not Polyline poly ||
                !poly.Closed || poly.NumberOfVertices != 4) continue;
            var points = Enumerable.Range(0, 4)
                .Select(i => new Point3d(poly.GetPoint2dAt(i).X, poly.GetPoint2dAt(i).Y, 0))
                .ToArray();
            var edges = Enumerable.Range(0, 4)
                .Select(i => points[(i + 1) % 4] - points[i]).ToArray();
            if (edges.Any(e => e.Length < 0.001)) continue;
            var rectangular = Enumerable.Range(0, 4).All(i =>
                Math.Abs(edges[i].DotProduct(edges[(i + 1) % 4])) /
                (edges[i].Length * edges[(i + 1) % 4].Length) < 0.04);
            if (!rectangular) continue;
            var area = Math.Abs(points.Take(4).Select((p, i) =>
                p.X * points[(i + 1) % 4].Y - points[(i + 1) % 4].X * p.Y).Sum()) / 2.0;
            var transformed = points.Select(p => p.TransformBy(station.BlockTransform)).ToArray();
            var transformedArea = Math.Abs(transformed.Take(4).Select((p, i) =>
                p.X * transformed[(i + 1) % 4].Y - transformed[(i + 1) % 4].X * p.Y).Sum()) / 2.0;
            if (area > 0.01) rectangles.Add((transformedArea, transformed));
        }
        Point3d[] result;
        double footprint;
        if (rectangles.Count > 0)
        {
            var best = rectangles.OrderByDescending(item => item.Area).First();
            result = best.Corners;
            footprint = best.Area;
        }
        else
        {
            // Ontbreekt een gesloten rechthoek in het blok? Een benaderde
            // omhullende is beschikbaar, maar vraag dan om extra controle.
            var ext = station.GeometricExtents;
            var low = ext.MinPoint; var high = ext.MaxPoint;
            result =
            [
                new Point3d(low.X, high.Y, 0), new Point3d(high.X, high.Y, 0),
                new Point3d(high.X, low.Y, 0), new Point3d(low.X, low.Y, 0)
            ];
            footprint = Math.Abs((high.X - low.X) * (high.Y - low.Y));
        }
        // Numerieke volgorde: C1 linksboven, C2 rechtsboven, C3 rechtsonder,
        // C4 linksonder. De punten worden al in RD New teruggegeven.
        var top = result.OrderByDescending(p => p.Y).Take(2).OrderBy(p => p.X).ToArray();
        var bottom = result.OrderBy(p => p.Y).Take(2).OrderByDescending(p => p.X).ToArray();
        var ordered = new[] { top[0], top[1], bottom[0], bottom[1] };
        return new(type, footprint, ordered, station.Position, stationId);
    }

    internal static string Number(double number) =>
        number.ToString("F3", CultureInfo.GetCultureInfo("nl-NL"));
}
