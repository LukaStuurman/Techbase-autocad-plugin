using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.Colors;
using Color = Autodesk.AutoCAD.Colors.Color;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Techbase.Blocks;

internal static class BlockInserter
{
    internal static ObjectId Import(Database target, Catalog catalog, BlockTool tool)
    {
        using (var tx = target.TransactionManager.StartTransaction())
        {
            var table = (BlockTable)tx.GetObject(target.BlockTableId, OpenMode.ForRead);
            if (table.Has(tool.BlockName)) return table[tool.BlockName];
        }
        using var source = new Database(false, true);
        source.ReadDwgFile(catalog.ResolveSource(tool), FileOpenMode.OpenForReadAndAllShare, true, null);
        source.CloseInput(true);
        using var tr = source.TransactionManager.StartTransaction();
        var blocks = (BlockTable)tr.GetObject(source.BlockTableId, OpenMode.ForRead);
        if (!blocks.Has(tool.BlockName)) throw new InvalidOperationException("Block '" + tool.BlockName + "' ontbreekt in " + catalog.ResolveSource(tool));
        var sourceId = blocks[tool.BlockName];
        var map = new IdMapping();
        source.WblockCloneObjects(new ObjectIdCollection(new[] { sourceId }), target.BlockTableId, map, DuplicateRecordCloning.Ignore, false);
        tr.Commit();
        return map[sourceId].Value;
    }

    internal static ObjectId DefaultLayer(Database db, Transaction tx, string name)
    {
        if (name.Length == 0) return db.Clayer;
        var table = (LayerTable)tx.GetObject(db.LayerTableId, OpenMode.ForRead);
        if (table.Has(name)) return table[name];
        table.UpgradeOpen();
        using var layer = new LayerTableRecord { Name = name };
        var id = table.Add(layer);
        tx.AddNewlyCreatedDBObject(layer, true);
        return id;
    }

    internal static BlockReference CreateReference(Database db, Transaction tx, ObjectId definition, BlockTool tool, Point3d point, ObjectId layer, Matrix3d ucs)
    {
        var record = (BlockTableRecord)tx.GetObject(definition, OpenMode.ForRead);
        var factor = record.Units == UnitsValue.Undefined || db.Insunits == UnitsValue.Undefined ? 1 : UnitsConverter.GetConversionFactor(record.Units, db.Insunits);
        var block = new BlockReference(Point3d.Origin, definition);
        block.SetDatabaseDefaults(db);
        block.LayerId = layer;
        block.Color = Color.FromColorIndex(ColorMethod.ByLayer, 256);
        block.LinetypeId = db.ByLayerLinetype;
        block.ScaleFactors = new Scale3d(tool.Scale * factor);
        block.Rotation = tool.Rotation;
        block.TransformBy(ucs);
        block.Position = point;
        return block;
    }

    internal static List<ObjectId> Append(Database db, Transaction tx, BlockReference block, bool explode,
        IReadOnlyDictionary<string, string>? values = null)
    {
        var space = (BlockTableRecord)tx.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
        var inserted = new List<ObjectId> { space.AppendEntity(block) };
        tx.AddNewlyCreatedDBObject(block, true);
        var record = (BlockTableRecord)tx.GetObject(block.BlockTableRecord, OpenMode.ForRead);
        foreach (ObjectId id in record)
        {
            if (tx.GetObject(id, OpenMode.ForRead) is not AttributeDefinition definition || definition.Constant) continue;
            using var attribute = new AttributeReference();
            attribute.SetAttributeFromBlock(definition, block.BlockTransform);
            attribute.TextString = values != null && values.TryGetValue(definition.Tag, out var value) ? value : definition.TextString;
            if (definition.Layer == "0") attribute.LayerId = block.LayerId;
            block.AttributeCollection.AppendAttribute(attribute);
            tx.AddNewlyCreatedDBObject(attribute, true);
            attribute.AdjustAlignment(db);
        }
        if (explode)
        {
            using var collection = new DBObjectCollection();
            block.Explode(collection);
            inserted.Clear();
            foreach (DBObject item in collection)
            {
                if (item is Entity entity)
                {
                    if (entity.Layer == "0") entity.LayerId = block.LayerId;
                    inserted.Add(space.AppendEntity(entity));
                    tx.AddNewlyCreatedDBObject(entity, true);
                }
                else item.Dispose();
            }
            block.Erase();
        }
        return inserted;
    }

    internal static bool Place(Document doc, Catalog catalog, BlockTool tool, Settings settings)
    {
        var db = doc.Database;
        var ed = doc.Editor;
        using var working = new WorkingDatabaseScope(db);
        var inheritLayer = settings.InheritLayer;
        var tolerance = settings.Tolerance;
        // Insertion entities are transactional; imported definitions may remain as with ordinary INSERT.
        using var tx = db.TransactionManager.StartTransaction();
        var definition = Import(db, catalog, tool);
        var defaultLayer = DefaultLayer(db, tx, tool.Layer);
        using var block = CreateReference(db, tx, definition, tool, Point3d.Origin, defaultLayer, ed.CurrentUserCoordinateSystem);
        using var jig = new PlacementJig(block);
        var picked = Array.Empty<ObjectId>();
        Point3d? pickedPoint = null;
        void Monitor(object sender, PointMonitorEventArgs args)
        {
            pickedPoint = args.Context.ComputedPoint;
            picked = args.Context.GetPickedEntities().Select(p => p.GetObjectIds()).Where(ids => ids.Length > 0).Select(ids => ids[0]).Distinct().ToArray();
        }
        PromptResult result;
        if (inheritLayer)
        {
            ed.TurnForcedPickOn();
            ed.PointMonitor += Monitor;
        }
        try { result = ed.Drag(jig); }
        finally
        {
            if (inheritLayer) { ed.PointMonitor -= Monitor; ed.TurnForcedPickOff(); }
        }
        ObjectId explicitLayer = ObjectId.Null;
        if (result.Status == PromptStatus.Keyword && result.StringResult == "Object")
        {
            var selection = ed.GetEntity("\nSelecteer het object op de gewenste plaats: ");
            if (selection.Status != PromptStatus.OK) return false;
            var target = (Entity)tx.GetObject(selection.ObjectId, OpenMode.ForRead);
            explicitLayer = LayerResolver.CheckLayer(tx, target.LayerId).LayerId;
            block.Position = selection.PickedPoint;
            if (target is Curve curve) block.Position = curve.GetClosestPointTo(selection.PickedPoint, false);
        }
        else if (result.Status != PromptStatus.OK) return false;
        if (inheritLayer || !explicitLayer.IsNull)
        {
            var validPicks = pickedPoint.HasValue && pickedPoint.Value.DistanceTo(block.Position) <= tolerance ? picked : Array.Empty<ObjectId>();
            var match = explicitLayer.IsNull ? LayerResolver.Resolve(db, tx, block.Position, tolerance, validPicks) : LayerResolver.CheckLayer(tx, explicitLayer);
            if (match.Ambiguous)
            {
                ed.WriteMessage("\n" + match.Message);
                var options = new PromptEntityOptions("\nWijs het bedoelde object aan (Enter = palettelaag): ") { AllowNone = true };
                var selected = ed.GetEntity(options);
                if (selected.Status == PromptStatus.Cancel) return false;
                if (selected.Status == PromptStatus.OK)
                    match = LayerResolver.CheckLayer(tx, ((Entity)tx.GetObject(selected.ObjectId, OpenMode.ForRead)).LayerId);
            }
            if (!match.LayerId.IsNull) block.LayerId = match.LayerId;
            ed.WriteMessage("\n" + match.Message);
        }
        LayerResolver.CheckLayer(tx, block.LayerId);
        if (tool.PromptRotation)
        {
            var angle = ed.GetAngle(new PromptAngleOptions("\nDraaihoek <0>: ")
            { BasePoint = block.Position, UseBasePoint = true, AllowNone = true, DefaultValue = tool.Rotation });
            if (angle.Status == PromptStatus.Cancel) return false;
            if (angle.Status == PromptStatus.OK) block.Rotation += angle.Value - tool.Rotation;
        }
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var record = (BlockTableRecord)tx.GetObject(definition, OpenMode.ForRead);
        if (Convert.ToInt16(AcApp.GetSystemVariable("ATTREQ")) != 0)
        {
            foreach (ObjectId id in record)
            {
                if (tx.GetObject(id, OpenMode.ForRead) is not AttributeDefinition att || att.Constant || att.Invisible || att.Preset) continue;
                var response = ed.GetString(new PromptStringOptions("\n" + (att.Prompt.Length > 0 ? att.Prompt : att.Tag) + " <" + att.TextString + ">: ") { AllowSpaces = true });
                if (response.Status == PromptStatus.Cancel) return false;
                attributes[att.Tag] = response.StringResult.Length == 0 ? att.TextString : response.StringResult;
            }
        }
        Append(db, tx, block, tool.Explode, attributes);
        tx.Commit();
        ed.WriteMessage("\nGeplaatst: " + tool.Name + " op laag " + block.Layer + ".");
        return true;
    }
}

internal sealed class PlacementJig : EntityJig, IDisposable
{
    private readonly BlockReference block;
    private Point3d point;
    internal PlacementJig(BlockReference block) : base(block) { this.block = block; point = block.Position; }
    protected override SamplerStatus Sampler(JigPrompts prompts)
    {
        var options = new JigPromptPointOptions("\nInvoegpunt: ")
        { UserInputControls = UserInputControls.Accept3dCoordinates | UserInputControls.NullResponseAccepted };
        options.Keywords.Add("Object");
        var result = prompts.AcquirePoint(options);
        if (result.Status == PromptStatus.Keyword) return SamplerStatus.OK;
        if (result.Status == PromptStatus.Cancel || result.Status == PromptStatus.None) return SamplerStatus.Cancel;
        if (result.Value.IsEqualTo(point)) return SamplerStatus.NoChange;
        point = result.Value;
        return SamplerStatus.OK;
    }
    protected override bool Update() { block.Position = point; return true; }
    public void Dispose() { }
}
