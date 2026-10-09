using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Color = Autodesk.AutoCAD.Colors.Color;

namespace Techbase.Blocks;

internal enum AreaMode { Polyline, All, Draw }
internal sealed record XrefChoice(ObjectId Id, string Name, string Description)
{
    public override string ToString() => Description;
}
internal sealed record MastCandidate(string Key, string Layer, string BlockName, Point3d Position, string[]? AncestorKeys = null);
internal sealed record MastSnapshot(IReadOnlyList<MastCandidate> Candidates, IReadOnlyList<string> Warnings);
internal sealed record MastResult(int Selected, int Placed, int AlreadyPresent);
internal sealed record LightmastRequest(Autodesk.AutoCAD.ApplicationServices.Document Document, ObjectId XrefId,
    string[] Layers, string[] BlockNames, AreaMode Mode);

internal static class LightmastService
{
    internal const string SymbolName = "Lichtmast_Def";
    internal const string TargetLayer = "Bestaand";
    private const string RegAppName = "TECHBASE_LIGHTMAST";
    internal static IReadOnlyList<XrefChoice> Xrefs(Database db)
    {
        using var tx = db.TransactionManager.StartTransaction();
        var space = (BlockTableRecord)tx.GetObject(db.CurrentSpaceId, OpenMode.ForRead);
        var result = new List<XrefChoice>();
        foreach (ObjectId id in space)
        {
            if (id.IsErased || tx.GetObject(id, OpenMode.ForRead) is not BlockReference reference) continue;
            var definition = (BlockTableRecord)tx.GetObject(reference.BlockTableRecord, OpenMode.ForRead);
            if (!definition.IsFromExternalReference || definition.XrefStatus != XrefStatus.Resolved) continue;
            result.Add(new XrefChoice(id, definition.Name, definition.Name + " · " + id.Handle + " · " + reference.Position.X.ToString("0.##") + ", " + reference.Position.Y.ToString("0.##")));
        }
        return result.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Id.Handle.Value).ToArray();
    }
    internal static MastSnapshot Scan(Database db, ObjectId xrefId)
    {
        if (xrefId.IsNull || xrefId.IsErased || xrefId.Database != db) throw new InvalidOperationException("De gekozen xref bestaat niet meer. Vernieuw de lijst.");
        using var tx = db.TransactionManager.StartTransaction();
        var root = tx.GetObject(xrefId, OpenMode.ForRead) as BlockReference ?? throw new InvalidOperationException("Kies een xref-invoeging.");
        if (root.OwnerId != db.CurrentSpaceId) throw new InvalidOperationException("De xref hoort niet bij de huidige tekenruimte. Vernieuw de lijst.");
        var definition = (BlockTableRecord)tx.GetObject(root.BlockTableRecord, OpenMode.ForRead);
        if (!definition.IsFromExternalReference || definition.XrefStatus != XrefStatus.Resolved) throw new InvalidOperationException("De xref is niet geladen of de bron ontbreekt. Laad de xref eerst.");
        var result = new List<MastCandidate>();
        var warnings = new HashSet<string>();
        var ancestors = new HashSet<ObjectId>();
        var rootLayer = ((LayerTableRecord)tx.GetObject(root.LayerId, OpenMode.ForRead)).Name;
        void Visit(ObjectId recordId, Matrix3d transform, string inheritedLayer, string path, int depth, string[] ancestorKeys)
        {
            if (depth > 64 || !ancestors.Add(recordId)) throw new InvalidOperationException("Te diepe of cyclische blockstructuur in de xref.");
            var record = (BlockTableRecord)tx.GetObject(recordId, OpenMode.ForRead);
            foreach (ObjectId id in record)
            {
                if (id.IsErased || tx.GetObject(id, OpenMode.ForRead) is not BlockReference symbol) continue;
                var child = (BlockTableRecord)tx.GetObject(symbol.BlockTableRecord, OpenMode.ForRead);
                var actualLayer = ((LayerTableRecord)tx.GetObject(symbol.LayerId, OpenMode.ForRead)).Name;
                if (actualLayer == "0" || actualLayer.EndsWith("|0", StringComparison.Ordinal)) actualLayer = inheritedLayer;
                var childPath = path + "/" + id.Handle;
                if (child.IsFromExternalReference)
                {
                    if (child.XrefStatus == XrefStatus.Resolved) Visit(child.ObjectId, symbol.BlockTransform.PreMultiplyBy(transform), actualLayer, childPath, depth + 1, ancestorKeys);
                    else warnings.Add("Geneste xref niet geladen: " + child.Name);
                    continue;
                }
                var effectiveDefinition = symbol.IsDynamicBlock ? (BlockTableRecord)tx.GetObject(symbol.DynamicBlockTableRecord, OpenMode.ForRead) : child;
                var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(childPath)));
                result.Add(new MastCandidate(key, actualLayer, effectiveDefinition.Name, symbol.Position.TransformBy(transform), ancestorKeys));
                // Also expose symbols nested inside ordinary group blocks, preserving the full transform and layer-0 inheritance.
                Visit(child.ObjectId, symbol.BlockTransform.PreMultiplyBy(transform), actualLayer, childPath, depth + 1, ancestorKeys.Append(key).ToArray());
            }
            ancestors.Remove(recordId);
        }
        Visit(root.BlockTableRecord, root.BlockTransform, rootLayer, xrefId.Handle.ToString(), 0, Array.Empty<string>());
        return new MastSnapshot(result, warnings.OrderBy(w => w).ToArray());
    }
    internal static MastCandidate[] Select(MastSnapshot snapshot, IEnumerable<string> layers, IEnumerable<string> blockNames, AreaBoundary? boundary)
    {
        var layerSet = new HashSet<string>(layers, StringComparer.OrdinalIgnoreCase);
        var nameSet = new HashSet<string>(blockNames, StringComparer.OrdinalIgnoreCase);
        if (layerSet.Count == 0) throw new InvalidOperationException("Vink minimaal één lichtmastlaag aan.");
        var filtered = snapshot.Candidates.Where(c => layerSet.Contains(c.Layer) && (nameSet.Count == 0 || nameSet.Contains(c.BlockName)) && (boundary == null || boundary.Contains(c.Position))).ToArray();
        var keys = filtered.Select(c => c.Key).ToHashSet(StringComparer.Ordinal);
        // A composite mast block can contain several smaller inserts. Never mark both the selected mast and its parts.
        return filtered.Where(c => c.AncestorKeys == null || !c.AncestorKeys.Any(keys.Contains)).ToArray();
    }
    internal static MastResult Place(Database db, IReadOnlyList<MastCandidate> selected)
    {
        if (selected.Count == 0) return new MastResult(0, 0, 0);
        using var working = new WorkingDatabaseScope(db);
        using var tx = db.TransactionManager.StartTransaction();
        var layer = BlockInserter.DefaultLayer(db, tx, TargetLayer);
        LayerResolver.CheckLayer(tx, layer);
        var space = (BlockTableRecord)tx.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
        var existing = new Dictionary<string, List<Point3d>>(StringComparer.Ordinal);
        foreach (ObjectId id in space)
        {
            if (id.IsErased || tx.GetObject(id, OpenMode.ForRead) is not BlockReference reference) continue;
            using var data = reference.GetXDataForApplication(RegAppName);
            if (data == null) continue;
            var values = data.AsArray();
            if (values.Length > 1 && values[1].Value is string key && values.Length > 2 && values[2].Value is Point3d original && reference.Position.DistanceTo(original) < 1e-7)
            {
                if (!existing.TryGetValue(key, out var positions)) { positions = new List<Point3d>(); existing.Add(key, positions); }
                positions.Add(original);
            }
        }
        var pending = selected.Where(c => !existing.TryGetValue(c.Key, out var positions) || !positions.Any(p => p.DistanceTo(c.Position) < 1e-7)).GroupBy(c => c.Key).Select(g => g.First()).ToArray();
        if (pending.Length == 0) return new MastResult(selected.Count, 0, selected.Count);
        var symbol = EnsureSymbol(db, tx);
        var applications = (RegAppTable)tx.GetObject(db.RegAppTableId, OpenMode.ForRead);
        if (!applications.Has(RegAppName))
        {
            applications.UpgradeOpen();
            using var app = new RegAppTableRecord { Name = RegAppName }; applications.Add(app); tx.AddNewlyCreatedDBObject(app, true);
        }
        foreach (var candidate in pending)
        {
            using var block = new BlockReference(candidate.Position, symbol);
            block.SetDatabaseDefaults(db); block.LayerId = layer;
            block.Color = Color.FromColorIndex(ColorMethod.ByLayer, 256);
            block.LinetypeId = db.ByLayerLinetype;
            space.AppendEntity(block); tx.AddNewlyCreatedDBObject(block, true);
            using var data = new ResultBuffer(new TypedValue((int)DxfCode.ExtendedDataRegAppName, RegAppName),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, candidate.Key),
                new TypedValue((int)DxfCode.ExtendedDataXCoordinate, candidate.Position));
            block.XData = data;
        }
        tx.Commit();
        return new MastResult(selected.Count, pending.Length, selected.Count - pending.Length);
    }
    private static ObjectId EnsureSymbol(Database db, Transaction tx)
    {
        var blocks = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
        if (blocks.Has(SymbolName)) return blocks[SymbolName];
        var linetypes = (LinetypeTable)tx.GetObject(db.LinetypeTableId, OpenMode.ForRead);
        if (!linetypes.Has("DASHED"))
        {
            try { db.LoadLineTypeFile("DASHED", db.Measurement == MeasurementValue.Metric ? "acadiso.lin" : "acad.lin"); }
            catch (Autodesk.AutoCAD.Runtime.Exception) { /* Same ByLayer fallback as the supplied Lisp. */ }
        }
        var linetype = linetypes.Has("DASHED") ? linetypes["DASHED"] : db.ByLayerLinetype;
        blocks.UpgradeOpen();
        using var definition = new BlockTableRecord { Name = SymbolName, Origin = Point3d.Origin, Units = UnitsValue.Undefined };
        var id = blocks.Add(definition); tx.AddNewlyCreatedDBObject(definition, true);
        const double radius = 1.1977 / 2;
        void Add(Point2d a, Point2d b, bool circle)
        {
            using var polyline = new Polyline(2);
            polyline.SetDatabaseDefaults(db); polyline.Layer = "0";
            polyline.Color = Color.FromColorIndex(ColorMethod.ByLayer, 256);
            polyline.LinetypeId = linetype;
            polyline.AddVertexAt(0, a, circle ? 1 : 0, 0, 0);
            polyline.AddVertexAt(1, b, circle ? 1 : 0, 0, 0);
            polyline.Closed = circle; polyline.ConstantWidth = 0.2;
            definition.AppendEntity(polyline); tx.AddNewlyCreatedDBObject(polyline, true);
        }
        Add(new Point2d(-radius, 0), new Point2d(radius, 0), true);
        Add(new Point2d(-radius, 0), new Point2d(radius, 0), false);
        Add(new Point2d(0, -radius), new Point2d(0, radius), false);
        return id;
    }
}
