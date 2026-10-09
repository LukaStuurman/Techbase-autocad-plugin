using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;

namespace Techbase.Blocks;

internal static class DirectionText
{
    internal const double Offset = 0.4;
    internal const double Gap = 0.2;
    internal static BlockTool Template(Catalog catalog, bool ov) => catalog.Tools.Single(t => t.Name.Equals(
        ov ? "RICHTINGEN TEKST OV" : "RICHTINGEN TEKST LS", StringComparison.OrdinalIgnoreCase));
    internal static string PlaceTemplate(Autodesk.AutoCAD.ApplicationServices.Document doc, bool ov)
    {
        var catalog = new Catalog(Plugin.DefaultPalette); var tool = Template(catalog, ov);
        return BlockInserter.Place(doc, catalog, tool, Plugin.Preferences, burst: true, useBlockLayerSelection: false)
            ? tool.Name + " als losse teksten geplaatst." : "Tekst plaatsen geannuleerd.";
    }
    private static MText Read(Database db, Transaction tx, ObjectId id)
    {
        if (id.IsNull || id.IsErased || id.Database != db || tx.GetObject(id, OpenMode.ForRead) is not MText text || text.OwnerId != db.CurrentSpaceId)
            throw new InvalidOperationException("Kies losse MText in de huidige tekenruimte.");
        var layer = (LayerTableRecord)tx.GetObject(text.LayerId, OpenMode.ForRead);
        if (layer.IsLocked || layer.IsDependent) throw new InvalidOperationException("Deze tekstlaag is vergrendeld of extern.");
        return text;
    }
    internal static void Layout(IReadOnlyList<MText> texts, IReadOnlyList<double> heights, Point3d start, Point3d end, Matrix3d ucs)
    {
        var a = start.TransformBy(ucs.Inverse()); var b = end.TransformBy(ucs.Inverse());
        var axis = new Vector2d(b.X - a.X, b.Y - a.Y);
        if (axis.Length < 1e-9) throw new InvalidOperationException("Kies twee verschillende punten voor de tekstrichting.");
        var angle = Math.Atan2(axis.Y, axis.X); if (angle < 0) angle += Math.PI * 2;
        var flip = angle > Math.PI / 2 && angle <= Math.PI * 1.5;
        var drawAngle = angle + (flip ? Math.PI : 0);
        var basis = flip ? b : a;
        var distance = Offset;
        for (var i = 0; i < texts.Count; i++)
        {
            var text = texts[i];
            text.Normal = ucs.CoordinateSystem3d.Zaxis;
            text.Attachment = flip ? AttachmentPoint.TopLeft : AttachmentPoint.BottomLeft;
            text.Direction = new Vector3d(Math.Cos(drawAngle), Math.Sin(drawAngle), 0).TransformBy(ucs);
            text.Location = new Point3d(basis.X - Math.Sin(angle) * distance, basis.Y + Math.Cos(angle) * distance, basis.Z).TransformBy(ucs);
            distance += heights[i] + Gap;
        }
    }
    internal static ObjectId[] Apply(Database db, IReadOnlyList<ObjectId> ordered, bool copy, Point3d start, Point3d end, Matrix3d ucs)
    {
        if (ordered.Count == 0 || ordered.Distinct().Count() != ordered.Count) throw new InvalidOperationException("Selecteer teksten eenmaal in de gewenste volgorde.");
        using var tx = db.TransactionManager.StartTransaction();
        var originals = ordered.Select(id => Read(db, tx, id)).ToArray();
        var style = Typography.Style(db, tx);
        var targets = new List<MText>();
        var space = (BlockTableRecord)tx.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
        foreach (var original in originals)
        {
            if (copy)
            {
                var clone = (MText)original.Clone(); space.AppendEntity(clone); tx.AddNewlyCreatedDBObject(clone, true); targets.Add(clone);
            }
            else { original.UpgradeOpen(); targets.Add(original); }
        }
        foreach (var text in targets) Typography.Apply(text, style);
        var heights = targets.Select(t => t.ActualHeight).ToArray();
        Layout(targets, heights, start, end, ucs);
        var ids = targets.Select(t => t.ObjectId).ToArray(); tx.Commit(); return ids;
    }
    internal static void RemoveCopy(Database db, IEnumerable<ObjectId> ids)
    {
        using var tx = db.TransactionManager.StartTransaction();
        foreach (var id in ids) if (!id.IsNull && !id.IsErased) ((Entity)tx.GetObject(id, OpenMode.ForWrite)).Erase();
        tx.Commit();
    }
    internal static string Run(Autodesk.AutoCAD.ApplicationServices.Document doc, bool copy)
    {
        var db = doc.Database; var editor = doc.Editor;
        using var working = new WorkingDatabaseScope(db);
        var ordered = new List<ObjectId>(); var history = new Stack<ObjectId[]>(); var count = 0;
        void Highlight(ObjectId id, bool on)
        {
            if (id.IsErased) return;
            using var tx = db.TransactionManager.StartTransaction();
            var text = (Entity)tx.GetObject(id, OpenMode.ForRead); if (on) text.Highlight(); else text.Unhighlight();
        }
        try
        {
            editor.WriteMessage("\nKlik MTexten in de gewenste stapelvolgorde. Nogmaals klikken verwijdert een tekst; Enter = klaar.");
            while (true)
            {
                var options = new PromptEntityOptions($"\n{ordered.Count} teksten geselecteerd - volgende MText (Enter = klaar): ") { AllowNone = true };
                options.SetRejectMessage("\nKies losse MText."); options.AddAllowedClass(typeof(MText), true);
                var result = editor.GetEntity(options);
                if (result.Status == PromptStatus.Cancel) return "Teksten selecteren geannuleerd.";
                if (result.Status == PromptStatus.None) break;
                if (result.Status != PromptStatus.OK) continue;
                using var tx = db.TransactionManager.StartTransaction();
                try
                {
                    var text = Read(db, tx, result.ObjectId);
                    if (ordered.Remove(result.ObjectId)) { Highlight(result.ObjectId, false); editor.WriteMessage("\nTekst uit selectie verwijderd."); }
                    else { ordered.Add(result.ObjectId); Highlight(result.ObjectId, true); editor.WriteMessage("\nToegevoegd: " + text.Text.Replace("\n", " ")); }
                }
                catch (InvalidOperationException ex) { editor.WriteMessage("\n" + ex.Message); }
            }
            if (ordered.Count == 0) return "Geen teksten geselecteerd.";
            var ucs = editor.CurrentUserCoordinateSystem;
            while (true)
            {
                var options = new PromptPointOptions(copy ? "\nStartpunt (Enter = klaar): " : "\nStartpunt verplaatsen (Enter = annuleren): ") { AllowNone = true };
                if (copy) options.Keywords.Add("Terug");
                var first = editor.GetPoint(options);
                if (first.Status == PromptStatus.Keyword && first.StringResult == "Terug")
                {
                    if (history.Count > 0) { RemoveCopy(db, history.Pop()); count--; editor.Regen(); editor.WriteMessage("\nLaatste kopie verwijderd."); }
                    else editor.WriteMessage("\nGeen kopie om terug te nemen.");
                    continue;
                }
                if (first.Status != PromptStatus.OK) break;
                var start = first.Value.TransformBy(ucs);
                using var preview = new DirectionTextJig(db, ordered, start, ucs);
                var second = editor.Drag(preview);
                if (second.Status != PromptStatus.OK) break;
                if (!preview.HasDirection) { editor.WriteMessage("\nKies twee verschillende punten."); continue; }
                var ids = Apply(db, ordered, copy, start, preview.End, ucs);
                count++; editor.Regen();
                if (!copy) break;
                history.Push(ids);
            }
            return copy ? count + " tekststapels gekopieerd." : count > 0 ? "Teksten verplaatst." : "Verplaatsen geannuleerd.";
        }
        finally { foreach (var id in ordered) Highlight(id, false); }
    }
}

internal sealed class DirectionTextJig : DrawJig, IDisposable
{
    private readonly List<MText> texts = new();
    private readonly double[] heights;
    private readonly Point3d start;
    private readonly Matrix3d ucs;
    internal Point3d End { get; private set; }
    internal bool HasDirection { get; private set; }
    internal DirectionTextJig(Database db, IEnumerable<ObjectId> ids, Point3d start, Matrix3d ucs)
    {
        this.start = start; this.ucs = ucs; End = start;
        try
        {
            using var tx = db.TransactionManager.StartTransaction();
            var style = Typography.Style(db, tx);
            foreach (var id in ids) texts.Add((MText)tx.GetObject(id, OpenMode.ForRead).Clone());
            foreach (var text in texts) Typography.Apply(text, style);
            heights = texts.Select(t => t.ActualHeight).ToArray();
            tx.Commit();
        }
        catch { Dispose(); throw; }
    }
    protected override SamplerStatus Sampler(JigPrompts prompts)
    {
        var result = prompts.AcquirePoint(new JigPromptPointOptions("\nRichting (eindpunt): ")
        { BasePoint = start, UseBasePoint = true, Cursor = CursorType.RubberBand,
            UserInputControls = UserInputControls.Accept3dCoordinates | UserInputControls.GovernedByOrthoMode | UserInputControls.UseBasePointElevation });
        if (result.Status != PromptStatus.OK) return SamplerStatus.Cancel;
        if (result.Value.IsEqualTo(End)) return SamplerStatus.NoChange;
        End = result.Value;
        var delta = (End - start).TransformBy(ucs.Inverse()); HasDirection = new Vector2d(delta.X, delta.Y).Length > 1e-9;
        if (HasDirection) DirectionText.Layout(texts, heights, start, End, ucs);
        return SamplerStatus.OK;
    }
    protected override bool WorldDraw(WorldDraw draw) { if (HasDirection) foreach (var text in texts) draw.Geometry.Draw(text); return true; }
    public void Dispose() { foreach (var text in texts) text.Dispose(); texts.Clear(); }
}
