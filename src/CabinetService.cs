using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Colors;

namespace Techbase.Blocks;

internal static class CabinetService
{
    private const string AppName = "TECHBASE_CABINETS";
    internal static int Place(Database db, LightmastRequest request, AreaBoundary? boundary)
    {
        using var tx = db.TransactionManager.StartTransaction();
        var root = (BlockReference)tx.GetObject(request.XrefId, OpenMode.ForRead);
        var definition = (BlockTableRecord)tx.GetObject(root.BlockTableRecord, OpenMode.ForRead);
        var loaded = definition.GetXrefDatabase(true) ?? throw new InvalidOperationException("De xref-bron is niet geladen.");
        using var source = new Database(false, true);
        source.ReadDwgFile(loaded.Filename, FileOpenMode.OpenForReadAndAllShare, true, null);
        source.CloseInput(true);
        source.ResolveXrefs(true, false);
        var selected = request.Layers.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var names = request.BlockNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existing = new HashSet<string>(StringComparer.Ordinal);
        var space = (BlockTableRecord)tx.GetObject(db.CurrentSpaceId, OpenMode.ForRead);
        foreach (ObjectId id in space)
        {
            if (tx.GetObject(id, OpenMode.ForRead) is not Entity entity) continue;
            using var data = entity.GetXDataForApplication(AppName);
            if (data?.AsArray() is { Length: > 1 } values && values[1].Value is string key) existing.Add(key);
        }
        var apps = (RegAppTable)tx.GetObject(db.RegAppTableId, OpenMode.ForRead);
        if (!apps.Has(AppName))
        {
            apps.UpgradeOpen();
            var app = new RegAppTableRecord { Name = AppName }; apps.Add(app); tx.AddNewlyCreatedDBObject(app, true);
        }
        var count = 0;
        var added = new ObjectIdCollection();
        void Visit(Database origin, ObjectId recordId, Matrix3d transform, string prefix, string inherited, string path, int depth)
        {
            if (depth > 64) throw new InvalidOperationException("Te diepe blockstructuur in de xref.");
            using var read = origin.TransactionManager.StartOpenCloseTransaction();
            var record = (BlockTableRecord)read.GetObject(recordId, OpenMode.ForRead);
            foreach (ObjectId id in record)
            {
                if (id.IsErased || read.GetObject(id, OpenMode.ForRead) is not Entity entity) continue;
                var localLayer = entity.Layer;
                var layer = localLayer == "0" ? inherited : prefix + localLayer;
                var childPath = path + "/" + id.Handle;
                var block = entity as BlockReference;
                var child = block == null ? null : (BlockTableRecord)read.GetObject(block.BlockTableRecord, OpenMode.ForRead);
                if (child?.IsFromExternalReference == true)
                {
                    var nestedLoaded = child.GetXrefDatabase(true);
                    if (nestedLoaded != null)
                    {
                        using var nested = new Database(false, true);
                        nested.ReadDwgFile(nestedLoaded.Filename, FileOpenMode.OpenForReadAndAllShare, true, null); nested.CloseInput(true);
                        nested.ResolveXrefs(true, false);
                        Visit(nested, SymbolUtilityServices.GetBlockModelSpaceId(nested), block!.BlockTransform.PreMultiplyBy(transform), prefix + child.Name + "|", layer, childPath, depth + 1);
                    }
                    continue;
                }
                var effective = block?.IsDynamicBlock == true ? (BlockTableRecord)read.GetObject(block.DynamicBlockTableRecord, OpenMode.ForRead) : child;
                var matches = selected.Contains(layer) && !LightmastService.MatchesLayer(layer, LightmastService.LightLayer) &&
                    (names.Count == 0 || (effective != null && names.Contains(prefix + effective.Name)));
                if (matches)
                {
                    Point3d position;
                    if (block != null) position = block.Position.TransformBy(transform);
                    else
                    {
                        try { var ext = entity.GeometricExtents; position = (ext.MinPoint + (ext.MaxPoint - ext.MinPoint) * 0.5).TransformBy(transform); }
                        catch (Autodesk.AutoCAD.Runtime.Exception) { if (boundary != null) continue; position = Point3d.Origin; }
                    }
                    if (boundary != null && !boundary.Contains(position)) continue;
                    var signature = childPath + "/" + string.Join(",", transform.ToArray().Select(v => v.ToString("R", CultureInfo.InvariantCulture)));
                    var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature)));
                    if (!existing.Add(key)) continue;
                    using var mapping = new IdMapping();
                    origin.WblockCloneObjects(new ObjectIdCollection(new[] { id }), db.CurrentSpaceId, mapping, DuplicateRecordCloning.MangleName, false);
                    var copy = (Entity)tx.GetObject(mapping[id].Value, OpenMode.ForWrite);
                    var importedLayer = (LayerTableRecord)tx.GetObject(copy.LayerId, OpenMode.ForRead);
                    try
                    {
                        if (copy is BlockReference copiedBlock)
                        {
                            copiedBlock.BlockTransform = copiedBlock.BlockTransform.PreMultiplyBy(transform);
                            foreach (ObjectId attributeId in copiedBlock.AttributeCollection)
                                ((AttributeReference)tx.GetObject(attributeId, OpenMode.ForWrite)).TransformBy(transform);
                        }
                        else if (copy is Line line)
                        {
                            line.StartPoint = line.StartPoint.TransformBy(transform);
                            line.EndPoint = line.EndPoint.TransformBy(transform);
                        }
                        else copy.TransformBy(transform);
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception ex) when (ex.ErrorStatus == Autodesk.AutoCAD.Runtime.ErrorStatus.CannotScaleNonUniformly)
                    {
                        // Curves such as circles cannot always represent unequal xref scales directly.
                        // Keep their original geometry inside a block with the full xref transform.
                        var blocks = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForWrite);
                        var wrapper = new BlockTableRecord { Name = "*U" };
                        var wrapperId = blocks.Add(wrapper); tx.AddNewlyCreatedDBObject(wrapper, true);
                        using var innerMapping = new IdMapping();
                        db.DeepCloneObjects(new ObjectIdCollection(new[] { copy.ObjectId }), wrapperId, innerMapping, false);
                        var inner = (Entity)tx.GetObject(innerMapping[copy.ObjectId].Value, OpenMode.ForWrite);
                        inner.Layer = "0"; inner.ColorIndex = 0;
                        copy.Erase();
                        var outer = new BlockReference(Point3d.Origin, wrapperId);
                        outer.SetDatabaseDefaults(db); outer.BlockTransform = transform; outer.Color = entity.Color;
                        space.UpgradeOpen(); space.AppendEntity(outer); tx.AddNewlyCreatedDBObject(outer, true);
                        copy = outer;
                    }
                    var target = layer[(layer.LastIndexOf('|') + 1)..];
                    var layerTable = (LayerTable)tx.GetObject(db.LayerTableId, OpenMode.ForRead);
                    var targetExisted = layerTable.Has(target);
                    var targetId = BlockInserter.DefaultLayer(db, tx, target);
                    LayerResolver.CheckLayer(tx, targetId);
                    if (!targetExisted)
                    {
                        var targetLayer = (LayerTableRecord)tx.GetObject(targetId, OpenMode.ForWrite);
                        targetLayer.Color = importedLayer.Color;
                        targetLayer.LinetypeObjectId = importedLayer.LinetypeObjectId;
                        targetLayer.LineWeight = importedLayer.LineWeight;
                    }
                    copy.LayerId = targetId;
                    if (LightmastService.MatchesLayer(layer, LightmastService.FlatLayer))
                    {
                        var flat = (LayerTableRecord)tx.GetObject(targetId, OpenMode.ForWrite);
                        flat.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByAci, 3);
                        copy.ColorIndex = 256;
                    }
                    using var data = new ResultBuffer(new TypedValue((int)DxfCode.ExtendedDataRegAppName, AppName), new TypedValue((int)DxfCode.ExtendedDataAsciiString, key));
                    copy.XData = data;
                    added.Add(copy.ObjectId);
                    count++;
                }
                else if (block != null)
                    Visit(origin, block.BlockTableRecord, block.BlockTransform.PreMultiplyBy(transform), prefix, layer, childPath, depth + 1);
            }
        }
        Visit(source, SymbolUtilityServices.GetBlockModelSpaceId(source), root.BlockTransform, definition.Name + "|", root.Layer, root.Handle.ToString(), 0);
        if (added.Count > 0) ((DrawOrderTable)tx.GetObject(space.DrawOrderTableId, OpenMode.ForWrite)).MoveToTop(added);
        tx.Commit();
        return count;
    }
}
