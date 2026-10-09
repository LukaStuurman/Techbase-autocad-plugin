using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Techbase.Blocks;

internal sealed record LayerMatch(ObjectId LayerId, bool Ambiguous, string Message);

internal static class LayerResolver
{
    internal static LayerMatch Resolve(Database db, Transaction tr, Point3d point, double tolerance,
        IEnumerable<ObjectId>? picked = null)
    {
        var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);
        var candidates = new List<(ObjectId Layer, double Distance)>();
        foreach (ObjectId id in space)
        {
            var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
            if (entity == null || entity.IsErased || !entity.Visible) continue;
            var layer = (LayerTableRecord)tr.GetObject(entity.LayerId, OpenMode.ForRead);
            if (layer.IsOff || layer.IsFrozen) continue;
            if (entity is Curve curve)
            {
                try
                {
                    var box = curve.GeometricExtents;
                    if (point.X < box.MinPoint.X - tolerance || point.X > box.MaxPoint.X + tolerance ||
                        point.Y < box.MinPoint.Y - tolerance || point.Y > box.MaxPoint.Y + tolerance ||
                        point.Z < box.MinPoint.Z - tolerance || point.Z > box.MaxPoint.Z + tolerance) continue;
                    var distance = curve.GetClosestPointTo(point, false).DistanceTo(point);
                    if (distance <= tolerance) candidates.Add((curve.LayerId, distance));
                }
                catch (Autodesk.AutoCAD.Runtime.Exception) { /* Unsupported proxy geometry is not guessed. */ }
            }
            else if (entity is DBPoint dbPoint && dbPoint.Position.DistanceTo(point) <= tolerance)
                candidates.Add((entity.LayerId, dbPoint.Position.DistanceTo(point)));
        }
        if (candidates.Count > 0)
        {
            var nearest = candidates.Min(c => c.Distance);
            var layers = candidates.Where(c => Math.Abs(c.Distance - nearest) <= 1e-8).Select(c => c.Layer).Distinct().ToArray();
            if (layers.Length > 1) return new LayerMatch(ObjectId.Null, true, "Meerdere lagen op dit punt. Kies Object om de laag expliciet aan te wijzen.");
            return CheckLayer(tr, layers[0]);
        }
        // For text, hatches, blocks and Civil objects use actual cursor picks, never bounding boxes.
        var pickedLayers = new HashSet<ObjectId>();
        foreach (var id in picked ?? Array.Empty<ObjectId>())
        {
            if (id.IsNull || id.IsErased || id.Database != db) continue;
            if (tr.GetObject(id, OpenMode.ForRead) is not Entity entity || entity.OwnerId != db.CurrentSpaceId || !entity.Visible) continue;
            var layer = (LayerTableRecord)tr.GetObject(entity.LayerId, OpenMode.ForRead);
            if (!layer.IsOff && !layer.IsFrozen) pickedLayers.Add(entity.LayerId);
        }
        if (pickedLayers.Count > 1) return new LayerMatch(ObjectId.Null, true, "Meerdere objectlagen onder de cursor. Kies Object.");
        return pickedLayers.Count == 1 ? CheckLayer(tr, pickedLayers.First()) : new LayerMatch(ObjectId.Null, false, "Geen object bij het invoegpunt; palettelaag gebruikt.");
    }
    internal static LayerMatch CheckLayer(Transaction tr, ObjectId id)
    {
        var layer = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
        if (layer.IsDependent) return new LayerMatch(ObjectId.Null, false, "Een externe referentielaag kan niet als invoeglaag dienen. Selecteer de referentie zelf.");
        if (layer.IsLocked) throw new InvalidOperationException("Laag '" + layer.Name + "' is vergrendeld. Kies een andere laag of ontgrendel deze zelf.");
        return new LayerMatch(id, false, "Laag overgenomen: " + layer.Name);
    }
}
