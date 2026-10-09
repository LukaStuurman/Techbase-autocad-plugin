using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;

namespace Techbase.Blocks;

internal static class SourceBlockImporter
{
    private const string MetadataKey = "TECHBASE_BLOCK_SOURCE";

    internal static ObjectId Import(Database target, string path, string blockName)
    {
        var fingerprint = Fingerprint(path);
        using var targetTx = target.TransactionManager.StartTransaction();
        var targetTable = (BlockTable)targetTx.GetObject(target.BlockTableId, OpenMode.ForRead);
        var (name, existing) = FindName(targetTable, targetTx, blockName, fingerprint);
        if (!existing.IsNull) return existing;

        // Rename only in the temporary source database. Never redefine a record
        // in the user's drawing; Ignore is safe after isolating block names.
        using var source = new Database(false, true);
        source.ReadDwgFile(path, FileOpenMode.OpenForReadAndAllShare, true, null);
        source.CloseInput(true);
        if (Fingerprint(path) != fingerprint) throw new IOException("De blockbron is tijdens het lezen veranderd. Probeer opnieuw.");
        using var sourceTx = source.TransactionManager.StartTransaction();
        var sourceTable = (BlockTable)sourceTx.GetObject(source.BlockTableId, OpenMode.ForRead);
        if (!sourceTable.Has(blockName)) throw new InvalidOperationException("Block '" + blockName + "' ontbreekt in " + path);
        var sourceId = sourceTable[blockName];
        var records = sourceTable.Cast<ObjectId>().Select(id => (BlockTableRecord)sourceTx.GetObject(id, OpenMode.ForRead))
            .Where(r => !r.IsLayout && !r.IsAnonymous && !r.IsDependent).ToArray();
        var names = new List<(BlockTableRecord Record, string Original, string Name)>();
        foreach (var record in records)
        {
            var original = record.Name;
            var chosen = record.ObjectId == sourceId ? name : FindName(targetTable, targetTx, original, fingerprint).Name;
            names.Add((record, original, chosen));
        }
        // Two passes avoid colliding with a source record that already happens
        // to have a generated name. AutoCAD manages anonymous dynamic records.
        foreach (var (record, _, _) in names)
        {
            record.UpgradeOpen(); record.Name = "TB_IMPORT_" + Guid.NewGuid().ToString("N");
        }
        foreach (var (record, original, chosen) in names)
        {
            record.Name = chosen;
            SetMetadata(record, sourceTx, original, fingerprint);
        }
        var map = new IdMapping();
        source.WblockCloneObjects(new ObjectIdCollection(new[] { sourceId }), target.BlockTableId, map, DuplicateRecordCloning.Ignore, false);
        var imported = map[sourceId].Value;
        sourceTx.Commit(); targetTx.Commit();
        return imported;
    }

    private static string Fingerprint(string path)
    {
        using var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return Convert.ToHexString(SHA256.HashData(file));
    }

    private static (string Name, ObjectId Existing) FindName(BlockTable table, Transaction tx, string original, string fingerprint)
    {
        var nameHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(original.ToUpperInvariant())))[..8];
        var stem = "TB_" + original[..Math.Min(original.Length, 210)] + "__" + fingerprint[..12] + "_" + nameHash;
        for (var index = 0; ; index++)
        {
            var name = stem + (index == 0 ? "" : "_" + index);
            if (!table.Has(name)) return (name, ObjectId.Null);
            var record = (BlockTableRecord)tx.GetObject(table[name], OpenMode.ForRead);
            if (Matches(record, tx, original, fingerprint)) return (name, record.ObjectId);
        }
    }

    private static bool Matches(BlockTableRecord record, Transaction tx, string original, string fingerprint)
    {
        if (record.ExtensionDictionary.IsNull) return false;
        var dictionary = (DBDictionary)tx.GetObject(record.ExtensionDictionary, OpenMode.ForRead);
        if (!dictionary.Contains(MetadataKey) || tx.GetObject(dictionary.GetAt(MetadataKey), OpenMode.ForRead) is not Xrecord metadata) return false;
        using var buffer = metadata.Data;
        var values = buffer?.AsArray().Select(v => v.Value as string).ToArray();
        return values is { Length: 3 } && values[0] == "1" &&
            string.Equals(values[1], original, StringComparison.OrdinalIgnoreCase) && values[2] == fingerprint;
    }

    private static void SetMetadata(BlockTableRecord record, Transaction tx, string original, string fingerprint)
    {
        if (record.ExtensionDictionary.IsNull) record.CreateExtensionDictionary();
        var dictionary = (DBDictionary)tx.GetObject(record.ExtensionDictionary, OpenMode.ForWrite);
        Xrecord metadata;
        if (dictionary.Contains(MetadataKey)) metadata = (Xrecord)tx.GetObject(dictionary.GetAt(MetadataKey), OpenMode.ForWrite);
        else
        {
            metadata = new Xrecord(); dictionary.SetAt(MetadataKey, metadata); tx.AddNewlyCreatedDBObject(metadata, true);
        }
        using var buffer = new ResultBuffer(new TypedValue((int)DxfCode.Text, "1"),
            new TypedValue((int)DxfCode.Text, original), new TypedValue((int)DxfCode.Text, fingerprint));
        metadata.Data = buffer;
    }
}
