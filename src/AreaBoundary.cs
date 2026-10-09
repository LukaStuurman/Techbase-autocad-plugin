using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Techbase.Blocks;

/// <summary>Plan-view region with analytic bulge arcs; boundary points are included.</summary>
internal sealed class AreaBoundary
{
    private readonly Edge[] edges;
    private readonly double tolerance;
    internal AreaBoundary(IReadOnlyList<Point2d> points, IReadOnlyList<double>? bulges = null)
    {
        if (points.Count < 2 || (points.Count < 3 && (bulges == null || bulges.All(b => Math.Abs(b) < 1e-12))))
            throw new InvalidOperationException("Een gebied heeft minimaal drie verschillende hoekpunten nodig.");
        var span = Math.Max(points.Max(p => p.X) - points.Min(p => p.X), points.Max(p => p.Y) - points.Min(p => p.Y));
        tolerance = Math.Max(1e-8, span * 1e-10);
        edges = Enumerable.Range(0, points.Count).Select(i => new Edge(points[i], points[(i + 1) % points.Count], bulges?[i] ?? 0)).ToArray();
        if (edges.Any(e => e.A.GetDistanceTo(e.B) <= tolerance)) throw new InvalidOperationException("Het gebied bevat samenvallende opeenvolgende hoekpunten.");
        var origin = points[0];
        var signedArea = edges.Sum(e => (e.A.X - origin.X) * (e.B.Y - origin.Y) - (e.B.X - origin.X) * (e.A.Y - origin.Y)
            + (e.IsArc ? e.Radius * e.Radius * (e.Sweep - Math.Sin(e.Sweep)) : 0)) / 2;
        if (Math.Abs(signedArea) <= tolerance * tolerance) throw new InvalidOperationException("Het gebied heeft geen oppervlakte.");
        if (edges.All(e => !e.IsArc))
        {
            for (int i = 0; i < edges.Length; i++)
                for (int j = i + 1; j < edges.Length; j++)
                {
                    if (j == i + 1 || (i == 0 && j == edges.Length - 1)) continue;
                    if (Crosses(edges[i], edges[j])) throw new InvalidOperationException("De polygoon kruist zichzelf. Teken een eenvoudige gebiedsgrens.");
                }
        }
    }
    internal static AreaBoundary FromPolyline(Polyline polyline)
    {
        if (!polyline.Closed) throw new InvalidOperationException("Kies een gesloten polyline. Sluit de grens eerst met PEDIT of kies 'Zelf polygoon tekenen'.");
        if (Math.Abs(Math.Abs(polyline.Normal.Z) - 1) > 1e-8) throw new InvalidOperationException("De gebiedsgrens moet horizontaal liggen (een gebied in bovenaanzicht).");
        var points = Enumerable.Range(0, polyline.NumberOfVertices).Select(i => polyline.GetPoint3dAt(i)).Select(p => new Point2d(p.X, p.Y)).ToArray();
        var bulges = Enumerable.Range(0, polyline.NumberOfVertices).Select(i => polyline.GetBulgeAt(i) * Math.Sign(polyline.Normal.Z)).ToArray();
        return new AreaBoundary(points, bulges);
    }
    internal bool Contains(Point3d point)
    {
        var p = new Point2d(point.X, point.Y);
        if (edges.Any(e => e.Distance(p) <= tolerance)) return true;
        var inside = false;
        foreach (var edge in edges)
        {
            if (!edge.IsArc)
            {
                if ((edge.A.Y > p.Y) != (edge.B.Y > p.Y))
                {
                    var x = edge.A.X + (p.Y - edge.A.Y) * (edge.B.X - edge.A.X) / (edge.B.Y - edge.A.Y);
                    if (x > p.X) inside = !inside;
                }
                continue;
            }
            // Split at vertical extrema: every piece is monotone in y and uses half-open endpoints.
            var cuts = new List<double> { 0, 1 };
            foreach (var angle in new[] { Math.PI / 2, 3 * Math.PI / 2 })
            {
                var parameter = edge.Parameter(angle);
                if (parameter > 1e-12 && parameter < 1 - 1e-12) cuts.Add(parameter);
            }
            cuts.Sort();
            for (var i = 0; i < cuts.Count - 1; i++)
            {
                var angleA = edge.Start + edge.Sweep * cuts[i];
                var angleB = edge.Start + edge.Sweep * cuts[i + 1];
                var yA = edge.Center.Y + edge.Radius * Math.Sin(angleA);
                var yB = edge.Center.Y + edge.Radius * Math.Sin(angleB);
                if ((yA > p.Y) == (yB > p.Y)) continue;
                var dy = p.Y - edge.Center.Y;
                var dx = Math.Sqrt(Math.Max(0, edge.Radius * edge.Radius - dy * dy));
                var side = Math.Cos((angleA + angleB) / 2) >= 0 ? 1 : -1;
                if (edge.Center.X + side * dx > p.X) inside = !inside;
            }
        }
        return inside;
    }
    private bool Crosses(Edge a, Edge b)
    {
        double Orient(Point2d x, Point2d y, Point2d z) => (y.X - x.X) * (z.Y - x.Y) - (y.Y - x.Y) * (z.X - x.X);
        var a1 = Orient(a.A, a.B, b.A); var a2 = Orient(a.A, a.B, b.B);
        var b1 = Orient(b.A, b.B, a.A); var b2 = Orient(b.A, b.B, a.B);
        if (a1 * a2 < 0 && b1 * b2 < 0) return true;
        return a.Distance(b.A) <= tolerance || a.Distance(b.B) <= tolerance || b.Distance(a.A) <= tolerance || b.Distance(a.B) <= tolerance;
    }
    private sealed class Edge
    {
        internal Point2d A { get; }
        internal Point2d B { get; }
        internal bool IsArc { get; }
        internal Point2d Center { get; }
        internal double Radius { get; }
        internal double Start { get; }
        internal double Sweep { get; }
        internal Edge(Point2d a, Point2d b, double bulge)
        {
            A = a; B = b; IsArc = Math.Abs(bulge) > 1e-12;
            if (!IsArc) return;
            var dx = b.X - a.X; var dy = b.Y - a.Y;
            var factor = (1 - bulge * bulge) / (4 * bulge);
            Center = new Point2d((a.X + b.X) / 2 - dy * factor, (a.Y + b.Y) / 2 + dx * factor);
            Radius = a.GetDistanceTo(b) * (1 + bulge * bulge) / (4 * Math.Abs(bulge));
            Start = Math.Atan2(a.Y - Center.Y, a.X - Center.X);
            Sweep = 4 * Math.Atan(bulge);
        }
        internal double Parameter(double angle)
        {
            var delta = Sweep >= 0 ? angle - Start : Start - angle;
            delta %= 2 * Math.PI; if (delta < 0) delta += 2 * Math.PI;
            return delta / Math.Abs(Sweep);
        }
        internal double Distance(Point2d p)
        {
            if (IsArc)
            {
                var angle = Math.Atan2(p.Y - Center.Y, p.X - Center.X);
                if (Parameter(angle) <= 1 + 1e-12) return Math.Abs(p.GetDistanceTo(Center) - Radius);
                return Math.Min(p.GetDistanceTo(A), p.GetDistanceTo(B));
            }
            var dx = B.X - A.X; var dy = B.Y - A.Y;
            var lengthSquared = dx * dx + dy * dy;
            var t = lengthSquared == 0 ? 0 : Math.Clamp(((p.X - A.X) * dx + (p.Y - A.Y) * dy) / lengthSquared, 0, 1);
            return p.GetDistanceTo(new Point2d(A.X + t * dx, A.Y + t * dy));
        }
    }
}
