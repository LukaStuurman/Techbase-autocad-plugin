using Autodesk.AutoCAD.DatabaseServices;

namespace Techbase.Blocks;

internal static class BlockAttributeLayers
{
    internal static (int Changed, int Skipped) Repair(Database db, IEnumerable<ObjectId> selection)
    {
        using var tx = db.TransactionManager.StartTransaction();
        var changed = 0; var skipped = 0;
        foreach (var id in selection.Distinct())
        {
            if (id.Database != db || id.IsErased || tx.GetObject(id, OpenMode.ForRead) is not BlockReference reference ||
                reference.OwnerId != db.CurrentSpaceId ||
                ((LayerTableRecord)tx.GetObject(reference.LayerId, OpenMode.ForRead)).IsLocked)
            { skipped++; continue; }
            var definition = (BlockTableRecord)tx.GetObject(reference.IsDynamicBlock ? reference.DynamicBlockTableRecord : reference.BlockTableRecord, OpenMode.ForRead);
            if (definition.IsFromExternalReference || definition.IsDependent || SourceBlockImporter.OriginalName(definition, tx) == null)
            { skipped++; continue; }
            var zeroTags = definition.Cast<ObjectId>().Select(e => tx.GetObject(e, OpenMode.ForRead)).OfType<AttributeDefinition>()
                .Where(a => !a.Constant && a.Layer == "0").Select(a => a.Tag).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (ObjectId attributeId in reference.AttributeCollection)
            {
                var attribute = (AttributeReference)tx.GetObject(attributeId, OpenMode.ForRead);
                if (!zeroTags.Contains(attribute.Tag) || attribute.Layer == "0") continue;
                if (((LayerTableRecord)tx.GetObject(attribute.LayerId, OpenMode.ForRead)).IsLocked) { skipped++; continue; }
                attribute.UpgradeOpen(); attribute.LayerId = db.LayerZero;
                if (attribute.IsMTextAttribute) attribute.UpdateMTextAttribute();
                changed++;
            }
        }
        tx.Commit();
        return (changed, skipped);
    }
}
