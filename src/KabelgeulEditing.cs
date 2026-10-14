using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Techbase.Blocks;

internal static class KabelgeulEditing
{
    private sealed record Scope(Document Document, ObjectId Original, HashSet<ObjectId> Entities, HashSet<ObjectId> Definitions, Matrix3d Ucs);
    private static Scope? scope;
    internal static string Begin(ObjectId id)
    {
        if (scope != null) throw new InvalidOperationException("Er wordt al een geul bewerkt.");
        var doc = AcApp.DocumentManager.MdiActiveDocument; var db = doc.Database;
        using var tx = db.TransactionManager.StartTransaction();
        var block = (BlockReference)tx.GetObject(id, OpenMode.ForRead);
        if (block.OwnerId != db.CurrentSpaceId) throw new InvalidOperationException("Kies een geul in de actieve tekeningruimte.");
        LayerResolver.CheckLayer(tx, block.LayerId);
        var definition = (BlockTableRecord)tx.GetObject(block.BlockTableRecord, OpenMode.ForRead);
        if (definition.IsDependent || definition.IsFromExternalReference || !definition.Name.StartsWith("KabelGeul_", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Kies een eigen KabelGeul; externe referenties worden niet bewerkt.");
        var leader = definition.Cast<ObjectId>().Select(i => tx.GetObject(i, OpenMode.ForRead)).OfType<Polyline>()
            // The incoming leader belongs to the first tray (six vertices).
            // Additional trays have five vertices and can precede it in the block table.
            .FirstOrDefault(p => p.Layer == "E_LV_CROSS_SECTION" && p.NumberOfVertices == 6);
        if (leader == null) throw new InvalidOperationException("Deze geul bevat geen bruikbare leader.");
        var code = ""; var swap = false;
        foreach (ObjectId attrId in block.AttributeCollection)
        {
            if (attrId.IsErased) continue;
            var attr = (AttributeReference)tx.GetObject(attrId, OpenMode.ForRead);
            if (attr.Tag == "ZWEVEND_CODE") { code = attr.TextString; swap = attr.HorizontalMode != TextHorizontalMode.TextCenter; }
        }
        foreach (ObjectId childId in definition)
            if (tx.GetObject(childId, OpenMode.ForRead) is AttributeDefinition attr && attr.Tag == "TB_CODESWAP") swap = attr.TextString == "1";
        string Point(Point3d p) => "(" + string.Join(" ", new[] { p.X, p.Y, p.Z }.Select(v => v.ToString("G17", CultureInfo.InvariantCulture))) + ")";
        var context = "(" + Point(leader.GetPoint3dAt(0)) + " " + Point(leader.GetPoint3dAt(1)) + " " + KabelgeulData.Quote(code) + " " + (swap ? "T" : "nil") + ")";
        scope = new(doc, id, ((BlockTableRecord)tx.GetObject(db.CurrentSpaceId, OpenMode.ForRead)).Cast<ObjectId>().ToHashSet(),
            ((BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead)).Cast<ObjectId>().ToHashSet(), doc.Editor.CurrentUserCoordinateSystem);
        return context;
    }
    internal static ObjectId Commit(ObjectId replacement)
    {
        var current = scope ?? throw new InvalidOperationException("Geen geulbewerking actief.");
        var db = current.Document.Database;
        using var tx = db.TransactionManager.StartTransaction();
        var old = (BlockReference)tx.GetObject(current.Original, OpenMode.ForWrite);
        var next = (BlockReference)tx.GetObject(replacement, OpenMode.ForWrite);
        if (next.OwnerId != old.OwnerId || next.ObjectId == old.ObjectId || current.Entities.Contains(replacement)) throw new InvalidOperationException("Ongeldig vervangingsblock.");
        LayerResolver.CheckLayer(tx, old.LayerId);
        var matrix = old.BlockTransform * next.BlockTransform.Inverse();
        var attributes = new List<AttributeReference>();
        try
        {
            foreach (ObjectId attrId in next.AttributeCollection)
            { if (attrId.IsErased) continue; var clone = (AttributeReference)tx.GetObject(attrId, OpenMode.ForRead).Clone(); attributes.Add(clone); clone.TransformBy(matrix); }
            foreach (ObjectId attrId in old.AttributeCollection) if (!attrId.IsErased) tx.GetObject(attrId, OpenMode.ForWrite).Erase();
            old.BlockTableRecord = next.BlockTableRecord;
            foreach (var attr in attributes) { old.AttributeCollection.AppendAttribute(attr); tx.AddNewlyCreatedDBObject(attr, true); }
            next.Erase();
            var space = (BlockTableRecord)tx.GetObject(old.OwnerId, OpenMode.ForRead);
            ((DrawOrderTable)tx.GetObject(space.DrawOrderTableId, OpenMode.ForWrite)).MoveToTop(new ObjectIdCollection(new[] { old.ObjectId }));
            tx.Commit();
        }
        finally { foreach (var attr in attributes) if (!attr.IsDisposed) attr.Dispose(); }
        if (!current.Document.Editor.CurrentUserCoordinateSystem.Equals(current.Ucs)) current.Document.Editor.CurrentUserCoordinateSystem = current.Ucs;
        scope = null;
        return current.Original;
    }
    internal static void Abort()
    {
        var current = scope; scope = null; if (current == null) return;
        try
        {
            var db = current.Document.Database;
            using var tx = db.TransactionManager.StartTransaction();
            var space = (BlockTableRecord)tx.GetObject(((BlockReference)tx.GetObject(current.Original, OpenMode.ForRead)).OwnerId, OpenMode.ForRead);
            foreach (ObjectId id in space) if (!id.IsErased && !current.Entities.Contains(id)) tx.GetObject(id, OpenMode.ForWrite).Erase();
            var table = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
            foreach (ObjectId id in table)
            {
                if (current.Definitions.Contains(id)) continue;
                var definition = (BlockTableRecord)tx.GetObject(id, OpenMode.ForRead);
                if (!definition.IsLayout && !definition.IsDependent && definition.Name.StartsWith("KabelGeul_", StringComparison.OrdinalIgnoreCase) && definition.GetBlockReferenceIds(false, false).Count == 0)
                { definition.UpgradeOpen(); definition.Erase(); }
            }
            tx.Commit();
        }
        finally { if (!current.Document.Editor.CurrentUserCoordinateSystem.Equals(current.Ucs)) current.Document.Editor.CurrentUserCoordinateSystem = current.Ucs; }
    }
}

public sealed partial class Plugin
{
    [LispFunction("TBKG_NATIVE_BEGIN_EDIT")] public static string BeginKabelgeulEdit(ResultBuffer args) => KabelgeulEditing.Begin((ObjectId)args.AsArray()[0].Value);
    [LispFunction("TBKG_NATIVE_COMMIT_EDIT")] public static ObjectId CommitKabelgeulEdit(ResultBuffer args) => KabelgeulEditing.Commit((ObjectId)args.AsArray()[0].Value);
}
