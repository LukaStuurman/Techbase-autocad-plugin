using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Colors;
using Color = Autodesk.AutoCAD.Colors.Color;

namespace Techbase.Blocks;

internal static class ExistingCableColors
{
    internal static bool Matches(string name) => !name.Contains('|') && name.Contains("bestaand", StringComparison.OrdinalIgnoreCase) && name.Contains("kabel", StringComparison.OrdinalIgnoreCase);
    internal static Color Choice(bool black) => Color.FromColorIndex(ColorMethod.ByAci, (short)(black ? 7 : 125));
    internal static void ApplyNew(Database db, Transaction tx, HashSet<ObjectId> previous)
    {
        var table = (LayerTable)tx.GetObject(db.LayerTableId, OpenMode.ForRead);
        foreach (ObjectId id in table)
        {
            if (previous.Contains(id)) continue;
            var layer = (LayerTableRecord)tx.GetObject(id, OpenMode.ForRead);
            if (layer.IsDependent || !Matches(layer.Name)) continue;
            layer.UpgradeOpen(); layer.Color = Choice(Plugin.Preferences.ExistingCableBlack);
        }
    }
    internal static (int Changed, int Locked) Apply(Database db, bool black)
    {
        using var tx = db.TransactionManager.StartTransaction();
        var table = (LayerTable)tx.GetObject(db.LayerTableId, OpenMode.ForRead);
        var changed = 0; var locked = 0;
        foreach (ObjectId id in table)
        {
            var layer = (LayerTableRecord)tx.GetObject(id, OpenMode.ForRead);
            if (layer.IsDependent || !Matches(layer.Name)) continue;
            if (layer.IsLocked) { locked++; continue; }
            layer.UpgradeOpen(); layer.Color = Choice(black); changed++;
        }
        tx.Commit(); return (changed, locked);
    }
}
