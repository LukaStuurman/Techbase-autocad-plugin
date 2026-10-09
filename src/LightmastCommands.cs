using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Polyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace Techbase.Blocks;

internal static class LightmastCommands
{
    internal static string Execute(LightmastRequest request)
    {
        var doc = request.Document;
        var db = doc.Database;
        using var working = new WorkingDatabaseScope(db);
        // Re-scan at execution, not from a stale panel snapshot.
        var snapshot = LightmastService.Scan(db, request.XrefId);
        var boundary = request.Mode switch
        {
            AreaMode.Polyline => SelectBoundary(doc.Editor, db),
            AreaMode.Draw => DrawBoundary(doc.Editor),
            _ => null
        };
        if (request.Mode != AreaMode.All && boundary == null) return "Geannuleerd; geen lichtmasten geplaatst.";
        var selected = LightmastService.Select(snapshot, request.Layers, request.BlockNames, boundary);
        var result = LightmastService.Place(db, selected);
        var message = result.Placed + " lichtmasten geplaatst op laag Bestaand" +
            (result.AlreadyPresent > 0 ? " · " + result.AlreadyPresent + " al aanwezig" : "") + ".";
        if (selected.Length == 0) message = "Geen lichtmastblocks gevonden binnen de gekozen lagen, blocktypen en het gebied.";
        if (snapshot.Warnings.Count > 0) message += "\n" + string.Join("\n", snapshot.Warnings);
        doc.Editor.WriteMessage("\n" + message);
        return message;
    }
    internal static AreaBoundary? SelectBoundary(Editor editor, Database db)
    {
        var options = new PromptEntityOptions("\nSelecteer de gesloten polyline van het gebied: ");
        options.SetRejectMessage("\nKies een gesloten 2D-polyline.");
        options.AddAllowedClass(typeof(Polyline), true);
        var result = editor.GetEntity(options);
        if (result.Status != PromptStatus.OK) return null;
        using var tx = db.TransactionManager.StartTransaction();
        return AreaBoundary.FromPolyline((Polyline)tx.GetObject(result.ObjectId, OpenMode.ForRead));
    }
    internal static AreaBoundary? DrawBoundary(Editor editor)
    {
        var points = new List<Point3d>();
        var ucs = editor.CurrentUserCoordinateSystem;
        using var preview = new Polyline();
        preview.ColorIndex = 3;
        var manager = TransientManager.CurrentTransientManager;
        var viewports = new IntegerCollection();
        var visible = false;
        try
        {
            while (true)
            {
                var options = new PromptPointOptions(points.Count == 0 ? "\nEerste hoekpunt van het gebied: " : "\nVolgend hoekpunt (Enter = sluiten): ")
                { AllowNone = points.Count >= 3, UseBasePoint = points.Count > 0 };
                if (points.Count > 0) { options.BasePoint = points.Last().TransformBy(ucs.Inverse()); options.Keywords.Add("Terug"); }
                if (points.Count >= 3) options.Keywords.Add("Sluiten");
                var result = editor.GetPoint(options);
                if (result.Status == PromptStatus.Cancel) return null;
                if (result.Status == PromptStatus.None || (result.Status == PromptStatus.Keyword && result.StringResult == "Sluiten"))
                    return new AreaBoundary(points.Select(p => new Point2d(p.X, p.Y)).ToArray());
                if (result.Status == PromptStatus.Keyword && result.StringResult == "Terug")
                {
                    if (points.Count > 0) { points.RemoveAt(points.Count - 1); preview.RemoveVertexAt(preview.NumberOfVertices - 1); }
                }
                else if (result.Status == PromptStatus.OK)
                {
                    // GetPoint returns UCS coordinates; region tests and the transient use world coordinates.
                    var point = result.Value.TransformBy(ucs);
                    if (points.Count >= 3 && new Point2d(point.X, point.Y).GetDistanceTo(new Point2d(points[0].X, points[0].Y)) < 1e-7)
                        return new AreaBoundary(points.Select(p => new Point2d(p.X, p.Y)).ToArray());
                    if (points.Count > 0 && new Point2d(point.X, point.Y).GetDistanceTo(new Point2d(points.Last().X, points.Last().Y)) < 1e-7)
                    { editor.WriteMessage("\nDit hoekpunt is gelijk aan het vorige; kies een ander punt."); continue; }
                    points.Add(point);
                    preview.AddVertexAt(preview.NumberOfVertices, new Point2d(point.X, point.Y), 0, 0, 0);
                    preview.Elevation = point.Z;
                }
                else return null;
                if (preview.NumberOfVertices >= 2)
                {
                    if (!visible) { manager.AddTransient(preview, TransientDrawingMode.DirectShortTerm, 128, viewports); visible = true; }
                    else manager.UpdateTransient(preview, viewports);
                }
                else if (visible) { manager.EraseTransient(preview, viewports); visible = false; }
            }
        }
        finally { if (visible) manager.EraseTransient(preview, viewports); }
    }
}
