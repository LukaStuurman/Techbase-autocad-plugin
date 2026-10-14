using Autodesk.AutoCAD.DatabaseServices;

namespace Techbase.Blocks;

internal static class BlockLayers
{
    internal const string ExistingCable = "01 - Bestaande kabel";
    internal static string Name(int selection) => selection == 0 ? ExistingCable : DirectionLines.LayerName(selection);
    internal static ObjectId Ensure(Database db, Transaction tx, int selection)
    {
        var name = Name(selection);
        var layers = (LayerTable)tx.GetObject(db.LayerTableId, OpenMode.ForRead);
        if (!layers.Has(name))
        {
            var catalog = new Catalog(Plugin.DefaultPalette);
            using var source = new Database(false, true);
            source.ReadDwgFile(catalog.ResolveSource(catalog.Tools[0]), FileOpenMode.OpenForReadAndAllShare, true, null);
            source.CloseInput(true);
            using var sourceTx = source.TransactionManager.StartTransaction();
            var sourceLayers = (LayerTable)sourceTx.GetObject(source.LayerTableId, OpenMode.ForRead);
            if (!sourceLayers.Has(name)) throw new InvalidOperationException("Laag ontbreekt in de Techbase-bron: " + name);
            source.WblockCloneObjects(new ObjectIdCollection(new[] { sourceLayers[name] }), db.LayerTableId,
                new IdMapping(), DuplicateRecordCloning.Ignore, false);
            sourceTx.Commit();
            if (selection == 0) ((LayerTableRecord)tx.GetObject(layers[name], OpenMode.ForWrite)).Color = ExistingCableColors.Choice(Plugin.Preferences.ExistingCableBlack);
        }
        var layer = (LayerTableRecord)tx.GetObject(layers[name], OpenMode.ForRead);
        if (layer.IsLocked || layer.IsDependent || layer.IsOff || layer.IsFrozen)
            throw new InvalidOperationException("Laag '" + name + "' is vergrendeld, extern of niet zichtbaar. Maak de laag eerst zelf bruikbaar.");
        return layer.ObjectId;
    }
}
