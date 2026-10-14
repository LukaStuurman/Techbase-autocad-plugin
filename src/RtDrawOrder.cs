using Autodesk.AutoCAD.DatabaseServices;

namespace Techbase.Blocks;

internal static class RtDrawOrder
{
    internal static bool IsRt(string name) => name.StartsWith("RT", StringComparison.OrdinalIgnoreCase);
    internal static string Revision(string name) => IsRt(name) ? "_RT_BACK1" : "";

    internal static int Apply(Database db, Transaction tx)
    {
        var count = 0;
        var visited = new HashSet<ObjectId>();
        foreach (ObjectId id in (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead))
        {
            var block = (BlockTableRecord)tx.GetObject(id, OpenMode.ForRead);
            if (block.IsLayout || block.IsFromExternalReference || block.IsDependent ||
                !IsRt(SourceBlockImporter.OriginalName(block, tx) ?? block.Name)) continue;
            count += ApplyBlock(block, tx, visited);
        }
        return count;
    }

    internal static int ApplyDefinition(ObjectId id, Transaction tx)
        => ApplyBlock((BlockTableRecord)tx.GetObject(id, OpenMode.ForRead), tx, new HashSet<ObjectId>());

    private static int ApplyBlock(BlockTableRecord block, Transaction tx, HashSet<ObjectId> visited)
    {
        if (!visited.Add(block.ObjectId) || block.IsFromExternalReference || block.IsDependent) return 0;
        var wipeouts = block.Cast<ObjectId>().Where(id => !id.IsErased && tx.GetObject(id, OpenMode.ForRead) is Wipeout).ToArray();
        var count = 0;
        if (wipeouts.Length > 0)
        {
            var order = (DrawOrderTable)tx.GetObject(block.DrawOrderTableId, OpenMode.ForRead);
            var bottom = order.GetFullDrawOrder(0).Cast<ObjectId>().Take(wipeouts.Length).ToHashSet();
            if (!bottom.SetEquals(wipeouts))
            {
                order.UpgradeOpen();
                order.MoveToBottom(new ObjectIdCollection(wipeouts));
                count++;
            }
        }
        // Anonymous evaluated records have their own order table.
        if (block.IsDynamicBlock)
            foreach (ObjectId id in block.GetAnonymousBlockIds())
                count += ApplyBlock((BlockTableRecord)tx.GetObject(id, OpenMode.ForRead), tx, visited);
        return count;
    }
}
