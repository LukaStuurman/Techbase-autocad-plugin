using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Techbase.Blocks;

internal static class KabelgeulPlacement
{
    internal sealed record Geometry(Point3d Anchor, Point3d End);

    private static BlockReference DirectBlock(Transaction tx, Database db, ObjectId id)
    {
        if (id.Database != db || tx.GetObject(id, OpenMode.ForRead) is not BlockReference block || block.OwnerId != db.CurrentSpaceId)
            throw new InvalidOperationException("Kies een directe kabelgeul in de actieve tekeningruimte.");
        LayerResolver.CheckLayer(tx, block.LayerId);
        var definition = (BlockTableRecord)tx.GetObject(block.BlockTableRecord, OpenMode.ForRead);
        if (definition.IsDependent || definition.IsFromExternalReference || !definition.Name.StartsWith("KabelGeul_", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Kies een eigen directe kabelgeul.");
        foreach (ObjectId attrId in block.AttributeCollection)
            if (tx.GetObject(attrId, OpenMode.ForRead) is AttributeReference attr && attr.Tag == "ZWEVEND_CODE" && !string.IsNullOrWhiteSpace(attr.TextString))
                throw new InvalidOperationException("Deze actie is voor directe kabelgeulen. Kies een geul zonder zwevende referentie.");
        return block;
    }

    private static Polyline Leader(Transaction tx, ObjectId definition) =>
        ((BlockTableRecord)tx.GetObject(definition, OpenMode.ForRead)).Cast<ObjectId>()
            .Select(id => tx.GetObject(id, OpenMode.ForRead)).OfType<Polyline>()
            .FirstOrDefault(p => p.Layer == "E_LV_CROSS_SECTION" && p.NumberOfVertices == 6)
            ?? throw new InvalidOperationException("Deze kabelgeul bevat geen bruikbare verwijslijn.");

    internal static Geometry Read(Database db, ObjectId id)
    {
        using var tx = db.TransactionManager.StartTransaction();
        var block = DirectBlock(tx, db, id); var leader = Leader(tx, block.BlockTableRecord);
        return new(leader.GetPoint3dAt(0).TransformBy(block.BlockTransform), leader.GetPoint3dAt(1).TransformBy(block.BlockTransform));
    }

    internal static void SetDistance(Database db, ObjectId id, double distance)
    {
        if (!double.IsFinite(distance) || distance <= 1e-6) throw new InvalidOperationException("Geef een afstand groter dan nul op.");
        using var tx = db.TransactionManager.StartTransaction();
        var block = DirectBlock(tx, db, id); var source = (BlockTableRecord)tx.GetObject(block.BlockTableRecord, OpenMode.ForRead);
        var leader = Leader(tx, source.ObjectId); var anchor = leader.GetPoint3dAt(0); var end = leader.GetPoint3dAt(1);
        var worldAnchor = anchor.TransformBy(block.BlockTransform); var worldEnd = end.TransformBy(block.BlockTransform);
        var axis = worldEnd - worldAnchor;
        if (axis.Length <= 1e-6) throw new InvalidOperationException("De verwijslijn heeft geen geldige richting.");
        var delta = worldAnchor + axis.GetNormal() * distance - worldEnd;
        var localDelta = delta.TransformBy(block.BlockTransform.Inverse());
        // Give only this insertion its own definition; shared copies retain their
        // geometry. Move the complete tray and labels, keeping the cable anchor.
        var table = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForWrite);
        using var target = new BlockTableRecord { Name = "KabelGeul_" + Guid.NewGuid().ToString("N"), Origin = source.Origin, Units = source.Units };
        var targetId = table.Add(target); tx.AddNewlyCreatedDBObject(target, true);
        var mapping = new IdMapping();
        db.DeepCloneObjects(new ObjectIdCollection(source.Cast<ObjectId>().ToArray()), targetId, mapping, false);
        var order = ((DrawOrderTable)tx.GetObject(source.DrawOrderTableId, OpenMode.ForRead)).GetFullDrawOrder(0);
        var clonedOrder = new ObjectIdCollection(order.Cast<ObjectId>().Where(i => mapping.Contains(i) && mapping[i].IsCloned).Select(i => mapping[i].Value).ToArray());
        ((DrawOrderTable)tx.GetObject(target.DrawOrderTableId, OpenMode.ForWrite)).MoveToTop(clonedOrder);
        foreach (ObjectId childId in target)
        {
            if (tx.GetObject(childId, OpenMode.ForWrite) is not Entity entity) continue;
            LayerResolver.CheckLayer(tx, entity.LayerId);
            entity.TransformBy(Matrix3d.Displacement(localDelta));
        }
        var newLeader = Leader(tx, targetId); newLeader.UpgradeOpen();
        newLeader.SetPointAt(0, new Point2d(anchor.X, anchor.Y));
        block.UpgradeOpen(); block.BlockTableRecord = targetId;
        foreach (ObjectId attrId in block.AttributeCollection)
        {
            var attr = (AttributeReference)tx.GetObject(attrId, OpenMode.ForWrite);
            LayerResolver.CheckLayer(tx, attr.LayerId); attr.TransformBy(Matrix3d.Displacement(delta));
        }
        tx.Commit();
    }

    internal static void Move(Database db, ObjectId id, Vector3d delta)
    {
        using var tx = db.TransactionManager.StartTransaction();
        var block = DirectBlock(tx, db, id); block.UpgradeOpen();
        var matrix = Matrix3d.Displacement(delta); block.TransformBy(matrix);
        foreach (ObjectId attrId in block.AttributeCollection)
        {
            var attr = (AttributeReference)tx.GetObject(attrId, OpenMode.ForWrite);
            LayerResolver.CheckLayer(tx, attr.LayerId); attr.TransformBy(matrix);
        }
        tx.Commit();
    }
}

public sealed partial class Plugin
{
    [CommandMethod("TBGEULAFSTAND", CommandFlags.Modal)]
    public void ChangeKabelgeulDistance() => EditKabelgeulPlacement(false);

    [CommandMethod("TBGEULVERPLAATS", CommandFlags.Modal)]
    public void MoveKabelgeul() => EditKabelgeulPlacement(true);

    private static void EditKabelgeulPlacement(bool move)
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument; if (doc == null) return;
        var ed = doc.Editor;
        try
        {
            var select = new PromptEntityOptions("\nSelecteer een directe kabelgeul: ");
            select.SetRejectMessage("\nKies het block van een kabelgeul."); select.AddAllowedClass(typeof(BlockReference), true);
            var result = ed.GetEntity(select); if (result.Status != PromptStatus.OK) return;
            var geometry = KabelgeulPlacement.Read(doc.Database, result.ObjectId);
            if (move)
            {
                var start = ed.GetPoint("\nKies het basispunt voor verplaatsen: "); if (start.Status != PromptStatus.OK) return;
                var destination = ed.GetPoint(new PromptPointOptions("\nKies de nieuwe locatie: ") { BasePoint = start.Value, UseBasePoint = true });
                if (destination.Status != PromptStatus.OK) return;
                KabelgeulPlacement.Move(doc.Database, result.ObjectId, (destination.Value - start.Value).TransformBy(ed.CurrentUserCoordinateSystem));
            }
            else
            {
                var input = ed.GetDistance(new PromptDistanceOptions("\nNieuwe afstand tot de kabels: ")
                {
                    AllowNegative = false, AllowZero = false, UseBasePoint = true,
                    BasePoint = geometry.Anchor.TransformBy(ed.CurrentUserCoordinateSystem.Inverse()),
                    UseDefaultValue = true, DefaultValue = geometry.Anchor.DistanceTo(geometry.End)
                });
                if (input.Status != PromptStatus.OK) return;
                KabelgeulPlacement.SetDistance(doc.Database, result.ObjectId, input.Value);
            }
            ed.Regen(); ed.WriteMessage(move ? "\nKabelgeul verplaatst." : "\nAfstand tot kabels aangepast; aansluitpunt behouden.");
        }
        catch (System.Exception ex) { ed.WriteMessage("\nKabelgeul: " + ex.Message); }
    }
}
