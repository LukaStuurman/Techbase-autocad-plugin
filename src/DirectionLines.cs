using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Color = Autodesk.AutoCAD.Colors.Color;
using Polyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace Techbase.Blocks;

internal sealed record DirectionLineStyle(ObjectId Layer, ObjectId Linetype);
internal sealed record DirectionConversion(int Changed, int LinesConverted, int Skipped, ObjectId[] ResultIds);

internal static class DirectionLines
{
    internal const double Width = 0.1;
    internal const double Scale = 0.0035;
    internal static string LayerName(int number)
    {
        if (number is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(number), "Kies een richting van 1 t/m 12.");
        return $"Aansluiting LS K{number:00}";
    }
    internal static DirectionLineStyle EnsureStyle(Database db, int number)
    {
        var name = LayerName(number);
        using var tx = db.TransactionManager.StartTransaction();
        var layers = (LayerTable)tx.GetObject(db.LayerTableId, OpenMode.ForRead);
        var types = (LinetypeTable)tx.GetObject(db.LinetypeTableId, OpenMode.ForRead);
        if (!layers.Has(name) || !types.Has("DASHED"))
        {
            var catalog = new Catalog(Plugin.DefaultPalette);
            using var source = new Database(false, true);
            source.ReadDwgFile(catalog.ResolveSource(catalog.Tools[0]), FileOpenMode.OpenForReadAndAllShare, true, null);
            source.CloseInput(true);
            using var sourceTx = source.TransactionManager.StartTransaction();
            var sourceLayers = (LayerTable)sourceTx.GetObject(source.LayerTableId, OpenMode.ForRead);
            var sourceTypes = (LinetypeTable)sourceTx.GetObject(source.LinetypeTableId, OpenMode.ForRead);
            if (!layers.Has(name))
            {
                if (!sourceLayers.Has(name)) throw new InvalidOperationException("Richtingslaag ontbreekt in de Techbase-bron: " + name);
                source.WblockCloneObjects(new ObjectIdCollection(new[] { sourceLayers[name] }), db.LayerTableId,
                    new IdMapping(), DuplicateRecordCloning.Ignore, false);
            }
            if (!types.Has("DASHED") && sourceTypes.Has("DASHED"))
                source.WblockCloneObjects(new ObjectIdCollection(new[] { sourceTypes["DASHED"] }), db.LinetypeTableId,
                    new IdMapping(), DuplicateRecordCloning.Ignore, false);
            sourceTx.Commit();
        }
        if (!types.Has("DASHED"))
            db.LoadLineTypeFile("DASHED", db.Measurement == MeasurementValue.Metric ? "acadiso.lin" : "acad.lin");
        if (!types.Has("DASHED")) throw new InvalidOperationException("Lijntype DASHED kon niet worden geladen.");
        var layer = (LayerTableRecord)tx.GetObject(layers[name], OpenMode.ForRead);
        if (layer.IsLocked || layer.IsDependent || layer.IsOff || layer.IsFrozen)
            throw new InvalidOperationException("Laag '" + name + "' is vergrendeld, extern of niet zichtbaar. Maak de laag eerst zelf bruikbaar.");
        var result = new DirectionLineStyle(layer.ObjectId, types["DASHED"]);
        tx.Commit(); return result;
    }
    internal static void Apply(Entity entity, DirectionLineStyle style)
    {
        entity.LayerId = style.Layer; entity.LinetypeId = style.Linetype;
        entity.LinetypeScale = Scale;
        entity.Color = Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByLayer, 256);
        entity.LineWeight = LineWeight.ByLayer;
        if (entity is Polyline light) { light.ConstantWidth = Width; light.Plinegen = true; }
        else if (entity is Polyline2d heavy) { heavy.ConstantWidth = Width; heavy.LinetypeGenerationOn = true; }
        else throw new InvalidOperationException("Kies een 2D-polyline.");
    }
    internal static ObjectId Append(Database db, Polyline polyline, int number)
    {
        var style = EnsureStyle(db, number);
        using var tx = db.TransactionManager.StartTransaction();
        Apply(polyline, style);
        var space = (BlockTableRecord)tx.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
        var id = space.AppendEntity(polyline); tx.AddNewlyCreatedDBObject(polyline, true);
        tx.Commit(); return id;
    }
    internal static DirectionConversion Convert(Database db, IEnumerable<ObjectId> selection, int number)
    {
        LayerName(number);
        using var tx = db.TransactionManager.StartTransaction();
        var valid = new List<Entity>(); var skipped = 0;
        foreach (var id in selection.Distinct())
        {
            if (id.IsNull || id.IsErased || id.Database != db) { skipped++; continue; }
            if (tx.GetObject(id, OpenMode.ForRead) is not Entity entity || entity.OwnerId != db.CurrentSpaceId ||
                entity is not (Polyline or Polyline2d or Line)) { skipped++; continue; }
            var layer = (LayerTableRecord)tx.GetObject(entity.LayerId, OpenMode.ForRead);
            if (layer.IsLocked || layer.IsDependent) { skipped++; continue; }
            if (entity is Line line && line.Length < 1e-9) { skipped++; continue; }
            valid.Add(entity);
        }
        if (valid.Count == 0) return new DirectionConversion(0, 0, skipped, Array.Empty<ObjectId>());
        var style = EnsureStyle(db, number);
        var ids = new List<ObjectId>(); var converted = 0;
        var space = (BlockTableRecord)tx.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
        foreach (var entity in valid)
        {
            entity.UpgradeOpen();
            if (entity is Line line)
            {
                var delta = line.EndPoint - line.StartPoint;
                var normal = Math.Abs(delta.Z) < 1e-9 ? Vector3d.ZAxis : delta.GetPerpendicularVector().GetNormal();
                using var polyline = new Polyline(); polyline.SetDatabaseDefaults(db);
                polyline.Normal = normal;
                var frame = Matrix3d.WorldToPlane(normal);
                var start = line.StartPoint.TransformBy(frame); var end = line.EndPoint.TransformBy(frame);
                polyline.Elevation = start.Z;
                polyline.AddVertexAt(0, new Point2d(start.X, start.Y), 0, 0, 0);
                polyline.AddVertexAt(1, new Point2d(end.X, end.Y), 0, 0, 0);
                Apply(polyline, style);
                ids.Add(space.AppendEntity(polyline)); tx.AddNewlyCreatedDBObject(polyline, true);
                using var data = line.XData; if (data != null) polyline.XData = data;
                line.Erase(); converted++;
            }
            else { Apply(entity, style); ids.Add(entity.ObjectId); }
        }
        tx.Commit(); return new DirectionConversion(valid.Count, converted, skipped, ids.ToArray());
    }
    internal static string Draw(Autodesk.AutoCAD.ApplicationServices.Document doc, int number)
    {
        LayerName(number);
        var editor = doc.Editor; var db = doc.Database;
        using var working = new WorkingDatabaseScope(db);
        var style = EnsureStyle(db, number);
        var first = editor.GetPoint($"\nBeginpunt richting {number}: ");
        if (first.Status != PromptStatus.OK) return "Tekenen geannuleerd; geen richting toegevoegd.";
        var ucs = editor.CurrentUserCoordinateSystem;
        using var polyline = new Polyline(); polyline.SetDatabaseDefaults(db);
        polyline.Normal = ucs.CoordinateSystem3d.Zaxis;
        var start = first.Value.TransformBy(ucs).TransformBy(Matrix3d.WorldToPlane(polyline.Normal));
        polyline.Elevation = start.Z;
        polyline.AddVertexAt(0, new Point2d(start.X, start.Y), 0, 0, 0);
        Apply(polyline, style);
        using var jig = new DirectionLineJig(polyline);
        while (true)
        {
            var result = editor.Drag(jig);
            if (result.Status == PromptStatus.Cancel) return "Tekenen geannuleerd; geen richting toegevoegd.";
            if (result.Status == PromptStatus.Keyword)
            {
                jig.RemovePreview();
                if (result.StringResult == "Terug") { if (polyline.NumberOfVertices > 1) polyline.RemoveVertexAt(polyline.NumberOfVertices - 1); }
                else if (result.StringResult == "Sluiten" && polyline.NumberOfVertices >= 3) { polyline.Closed = true; break; }
                continue;
            }
            if (result.Status == PromptStatus.None) { jig.RemovePreview(); break; }
            if (result.Status != PromptStatus.OK) return "Tekenen geannuleerd; geen richting toegevoegd.";
            jig.AcceptPoint();
        }
        if (polyline.NumberOfVertices < 2) return "Geen richting toegevoegd; kies minstens twee punten.";
        Append(db, polyline, number); editor.Regen();
        return $"Richting {number} getekend op {LayerName(number)}.";
    }
}

internal sealed class DirectionLineJig : DrawJig, IDisposable
{
    private readonly Polyline polyline;
    private bool preview;
    internal DirectionLineJig(Polyline polyline) { this.polyline = polyline; }
    internal void RemovePreview() { if (preview) polyline.RemoveVertexAt(polyline.NumberOfVertices - 1); preview = false; }
    internal void AcceptPoint()
    {
        if (preview && polyline.GetPoint2dAt(polyline.NumberOfVertices - 1).GetDistanceTo(polyline.GetPoint2dAt(polyline.NumberOfVertices - 2)) < 1e-9)
            RemovePreview();
        preview = false;
    }
    protected override SamplerStatus Sampler(JigPrompts prompts)
    {
        var committed = polyline.NumberOfVertices - (preview ? 1 : 0);
        var options = new JigPromptPointOptions("\nVolgend punt (Enter = klaar): ")
        {
            BasePoint = polyline.GetPoint3dAt(committed - 1), UseBasePoint = true,
            UserInputControls = UserInputControls.Accept3dCoordinates | UserInputControls.NullResponseAccepted |
                UserInputControls.GovernedByOrthoMode | UserInputControls.UseBasePointElevation
        };
        if (committed > 1) options.Keywords.Add("Terug");
        if (committed >= 3) options.Keywords.Add("Sluiten");
        var result = prompts.AcquirePoint(options);
        if (result.Status == PromptStatus.Cancel) return SamplerStatus.Cancel;
        if (result.Status != PromptStatus.OK) return SamplerStatus.OK;
        var world = result.Value.TransformBy(Matrix3d.WorldToPlane(polyline.Normal));
        var point = new Point2d(world.X, world.Y);
        if (preview && point.IsEqualTo(polyline.GetPoint2dAt(polyline.NumberOfVertices - 1))) return SamplerStatus.NoChange;
        if (preview) polyline.SetPointAt(polyline.NumberOfVertices - 1, point);
        else { polyline.AddVertexAt(polyline.NumberOfVertices, point, 0, DirectionLines.Width, DirectionLines.Width); preview = true; }
        return SamplerStatus.OK;
    }
    protected override bool WorldDraw(WorldDraw draw) { draw.Geometry.Draw(polyline); return true; }
    public void Dispose() { }
}
