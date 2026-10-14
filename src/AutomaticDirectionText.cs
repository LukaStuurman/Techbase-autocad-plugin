using System.Text.RegularExpressions;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;

namespace Techbase.Blocks;

internal sealed record DirectionCrossing(int Number, ObjectId Curve, ObjectId Layer, Point3d Point, Vector3d Tangent, double Distance);
internal static class AutomaticDirectionText
{
    internal static string DefaultText(bool ov, bool existing = false) => (ov ? "6CU / " : "150AL / ") + (existing ? "(was ????-00)" : "????-00");
    internal static int Number(string layer)
    {
        var match = Regex.Match(layer, @"^Aansluiting LS K(0[1-9]|1[0-2])$", RegexOptions.IgnoreCase);
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }
    internal static DirectionCrossing[] Detect(Database db, Point3d first, Point3d last, ObjectId exclude = default)
    {
        if (first.DistanceTo(last) < 1e-8) throw new InvalidOperationException("Kies twee verschillende punten voor de detectielijn.");
        using var line = new Line(first, last); using var tx = db.TransactionManager.StartTransaction();
        var results = new List<DirectionCrossing>();
        var space = (BlockTableRecord)tx.GetObject(db.CurrentSpaceId, OpenMode.ForRead);
        foreach (ObjectId id in space)
        {
            if (id == exclude || id.IsErased || tx.GetObject(id, OpenMode.ForRead) is not Curve curve || !curve.Visible) continue;
            var layer = (LayerTableRecord)tx.GetObject(curve.LayerId, OpenMode.ForRead); var number = Number(layer.Name);
            if ((number == 0 && !ExistingCableColors.Matches(layer.Name)) || layer.IsDependent || layer.IsOff || layer.IsFrozen) continue;
            var points = new Point3dCollection();
            try
            {
                curve.IntersectWith(line, Intersect.OnBothOperands, points, IntPtr.Zero, IntPtr.Zero);
                // Exact endpoint contact is sometimes omitted by IntersectWith.
                var nearest = curve.GetClosestPointTo(first, false);
                if (nearest.DistanceTo(first) < 1e-7) points.Add(first);
                foreach (Point3d point in points)
                    results.Add(new(number, id, layer.ObjectId, point, curve.GetFirstDerivative(curve.GetClosestPointTo(point, false)), first.DistanceTo(point)));
            }
            catch (Autodesk.AutoCAD.Runtime.Exception) { /* Unsupported curves cannot supply a direction crossing. */ }
        }
        // One label per numbered direction; existing cables each retain their own label.
        return results.OrderBy(r => r.Distance).ThenBy(r => r.Number)
            .GroupBy(r => (r.Number, r.Number == 0 ? r.Curve : ObjectId.Null)).Select(g => g.First()).ToArray();
    }
    internal static Vector3d Axis(IReadOnlyList<DirectionCrossing> hits, Point3d first, Point3d last, Matrix3d ucs)
    {
        var normal = ucs.CoordinateSystem3d.Zaxis;
        var tangent = hits[0].Tangent; tangent -= normal * tangent.DotProduct(normal);
        if (tangent.Length < 1e-8) throw new InvalidOperationException("De kabelrichting past niet in het huidige tekenvlak.");
        tangent = tangent.GetNormal();
        if (normal.CrossProduct(tangent).DotProduct(last - first) < 0) tangent = -tangent;
        return tangent;
    }
    internal static MText[] Create(Database db, IReadOnlyList<DirectionCrossing> hits, bool ov, double height, Transaction tx)
    {
        var style = Typography.Style(db, tx); var texts = new List<MText>();
        try
        {
            foreach (var hit in hits)
            {
                var layer = (LayerTableRecord)tx.GetObject(hit.Layer, OpenMode.ForRead);
                if (layer.IsLocked || layer.IsDependent || layer.IsOff || layer.IsFrozen) throw new InvalidOperationException("Richtingslaag '" + layer.Name + "' is vergrendeld, extern of verborgen.");
                var text = new MText { Contents = DefaultText(ov, hit.Number == 0), TextHeight = height, LayerId = hit.Layer,
                    TextStyleId = style, ColorIndex = 256, LineWeight = LineWeight.ByLayer };
                texts.Add(text); DirectionTextAppearance.Apply(text, hit.Number == 0, height, Plugin.Preferences);
            }
            return texts.ToArray();
        }
        catch { foreach (var text in texts) text.Dispose(); throw; }
    }
    internal static void Layout(IReadOnlyList<MText> texts, Point3d anchor, Vector3d axis, Matrix3d ucs)
    {
        var delta = axis.TransformBy(ucs.Inverse()); var angle = Math.Atan2(delta.Y, delta.X); if (angle < 0) angle += Math.PI * 2;
        var flip = angle > Math.PI / 2 && angle <= Math.PI * 1.5;
        DirectionText.Layout(texts, texts.Select(t => t.ActualHeight).ToArray(), flip ? anchor - axis : anchor, flip ? anchor : anchor + axis, ucs);
    }
    internal static ObjectId[] Append(Database db, IReadOnlyList<MText> texts)
    {
        using var tx = db.TransactionManager.StartTransaction();
        var space = (BlockTableRecord)tx.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
        foreach (var text in texts)
        {
            var layer = (LayerTableRecord)tx.GetObject(text.LayerId, OpenMode.ForRead);
            if (layer.IsLocked || layer.IsDependent) throw new InvalidOperationException("Richtingslaag is niet bruikbaar.");
            space.AppendEntity(text); tx.AddNewlyCreatedDBObject(text, true);
        }
        var ids = texts.Select(t => t.ObjectId).ToArray(); tx.Commit(); return ids;
    }
    internal static string Run(Autodesk.AutoCAD.ApplicationServices.Document doc, bool ov)
    {
        var db = doc.Database; var ed = doc.Editor; var ucs = ed.CurrentUserCoordinateSystem;
        using var working = new WorkingDatabaseScope(db);
        var options = new PromptEntityOptions("\nSelecteer eerste richtingkabel [Lijn/TweePunten]: ");
        options.SetRejectMessage("\nKies een kabel/polyline."); options.AddAllowedClass(typeof(Curve), false);
        options.Keywords.Add("Lijn"); options.Keywords.Add("TweePunten");
        var selected = ed.GetEntity(options);
        Point3d first, last; ObjectId excluded = ObjectId.Null;
        if (selected.Status == PromptStatus.Keyword && selected.StringResult == "TweePunten")
        {
            var point = ed.GetPoint("\nStartpunt detectielijn: "); if (point.Status != PromptStatus.OK) return "Automatische teksten geannuleerd.";
            first = point.Value.TransformBy(ucs);
            var end = ed.GetPoint(new PromptPointOptions("\nEindpunt detectielijn: ") { BasePoint = point.Value, UseBasePoint = true });
            if (end.Status != PromptStatus.OK) return "Automatische teksten geannuleerd."; last = end.Value.TransformBy(ucs);
        }
        else
        {
            var existingLine = selected.Status == PromptStatus.Keyword && selected.StringResult == "Lijn";
            if (existingLine)
            {
                selected = ed.GetEntity("\nSelecteer een bestaande rechte detectielijn: ");
                if (selected.Status != PromptStatus.OK) return "Automatische teksten geannuleerd.";
            }
            else if (selected.Status != PromptStatus.OK) return "Automatische teksten geannuleerd.";
            using var tx = db.TransactionManager.StartTransaction();
            if (tx.GetObject(selected.ObjectId, OpenMode.ForRead) is not Curve curve || curve.OwnerId != db.CurrentSpaceId)
                throw new InvalidOperationException("Kies een curve in de hoofdtekening.");
            if (existingLine)
            {
                if (curve is not Line) throw new InvalidOperationException("Kies een rechte LINE als detectielijn, of gebruik TweePunten.");
                first = curve.StartPoint; last = curve.EndPoint; excluded = curve.ObjectId;
            }
            else
            {
                first = curve.GetClosestPointTo(selected.PickedPoint, false);
                var tangent = curve.GetFirstDerivative(first); var normal = ucs.CoordinateSystem3d.Zaxis;
                tangent -= normal * tangent.DotProduct(normal);
                if (tangent.Length < 1e-8) throw new InvalidOperationException("Kies een kabel in het huidige tekenvlak.");
                var perp = normal.CrossProduct(tangent).GetNormal();
                var point = ed.GetPoint(new PromptPointOptions("\nAfstandspunt: trek een lijn dwars over de richtingen: ") { BasePoint = first.TransformBy(ucs.Inverse()), UseBasePoint = true });
                if (point.Status != PromptStatus.OK) return "Automatische teksten geannuleerd.";
                last = first + perp * (point.Value.TransformBy(ucs) - first).DotProduct(perp);
            }
        }
        var hits = Detect(db, first, last, excluded);
        if (hits.Length == 0) return "Geen richtingen 1 t/m 12 of bestaande kabels langs de detectielijn gevonden.";
        var axis = Axis(hits, first, last, ucs);
        ed.WriteMessage("\nHerkend in kabelvolgorde: " + string.Join(", ", hits.Select(h => h.Number == 0 ? "Bestaand" : "Ri " + h.Number)));
        MText[] texts;
        using (var tx = db.TransactionManager.StartTransaction()) { texts = Create(db, hits, ov, Plugin.Preferences.DirectionTextHeight, tx); tx.Commit(); }
        using var jig = new AutomaticDirectionTextJig(texts, hits[^1].Point, axis, ucs);
        var placement = ed.Drag(jig);
        if (placement.Status is not (PromptStatus.OK or PromptStatus.None)) return "Automatische teksten geannuleerd; geen teksten geplaatst.";
        Append(db, texts); ed.Regen(); return $"{texts.Length} {(ov ? "OV" : "LS")}-richtingsteksten geplaatst.";
    }
}

internal sealed class AutomaticDirectionTextJig : DrawJig, IDisposable
{
    private readonly MText[] texts;
    private readonly Vector3d axis;
    private readonly Matrix3d ucs;
    private Point3d anchor;
    private readonly Point3d defaultAnchor;
    internal AutomaticDirectionTextJig(MText[] texts, Point3d anchor, Vector3d axis, Matrix3d ucs)
    { this.texts = texts; this.anchor = anchor; defaultAnchor = anchor; this.axis = axis; this.ucs = ucs; AutomaticDirectionText.Layout(texts, anchor, axis, ucs); }
    protected override SamplerStatus Sampler(JigPrompts prompts)
    {
        var result = prompts.AcquirePoint(new JigPromptPointOptions("\nPlaats tekststapel (Enter = boven de gevonden kabels): ")
        { UserInputControls = UserInputControls.Accept3dCoordinates | UserInputControls.NullResponseAccepted | UserInputControls.UseBasePointElevation });
        if (result.Status == PromptStatus.None) { anchor = defaultAnchor; AutomaticDirectionText.Layout(texts, anchor, axis, ucs); return SamplerStatus.OK; }
        if (result.Status != PromptStatus.OK) return SamplerStatus.Cancel;
        if (result.Value.IsEqualTo(anchor)) return SamplerStatus.NoChange;
        anchor = result.Value; AutomaticDirectionText.Layout(texts, anchor, axis, ucs); return SamplerStatus.OK;
    }
    protected override bool WorldDraw(WorldDraw draw) { foreach (var text in texts) draw.Geometry.Draw(text); return true; }
    public void Dispose() { foreach (var text in texts) text.Dispose(); }
}

public sealed partial class Plugin
{
    [Autodesk.AutoCAD.Runtime.CommandMethod("TBAUTOTEKSTLS", Autodesk.AutoCAD.Runtime.CommandFlags.Modal)] public void AutoLSText() => AutoText(false);
    [Autodesk.AutoCAD.Runtime.CommandMethod("TBAUTOTEKSTOV", Autodesk.AutoCAD.Runtime.CommandFlags.Modal)] public void AutoOVText() => AutoText(true);
    private static void AutoText(bool ov)
    { try { var doc = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument; if (doc != null) ReportDirection(AutomaticDirectionText.Run(doc, ov)); } catch (Exception ex) { ReportDirection(ex.Message); } }
    [Autodesk.AutoCAD.Runtime.CommandMethod("TBBESTAANDEKLEUR", Autodesk.AutoCAD.Runtime.CommandFlags.Modal)]
    public void ApplyExistingColor()
    {
        var doc = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument; if (doc == null) return;
        try
        {
            var result = ExistingCableColors.Apply(doc.Database, Preferences.ExistingCableBlack); doc.Editor.Regen();
            var message = $"{result.Changed} bestaande kabellagen/Bestaand {(Preferences.ExistingCableBlack ? "zwart (RGB 0,0,0)" : "donkergroen")}. {result.Locked} vergrendelde lagen overgeslagen.";
            settingsPanel?.ReportColor(message); doc.Editor.WriteMessage("\n" + message);
        }
        catch (Exception ex) { settingsPanel?.ReportColor(ex.Message); doc.Editor.WriteMessage("\n" + ex.Message); }
    }
}
