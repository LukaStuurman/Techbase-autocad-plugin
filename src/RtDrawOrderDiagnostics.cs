using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Techbase.Blocks;

internal static class RtDrawOrderDiagnostics
{
    internal static void Run(Catalog catalog, string output)
    {
        var folder = Path.Combine(output, "RT"); Directory.CreateDirectory(folder);
        var tools = catalog.Tools.Where(t => RtDrawOrder.IsRt(t.BlockName)).ToArray();
        var checks = new List<string>(); var inventory = new List<object>();
        void Check(bool ok, string name) { if (!ok) throw new InvalidOperationException("RT: " + name); checks.Add(name); }
        string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        var hashes = tools.Select(t => catalog.ResolveSource(t)).Distinct().ToDictionary(p => p, Hash);
        using var db = new Database(true, true);
        using var working = new WorkingDatabaseScope(db);
        var ids = new List<ObjectId>();
        foreach (var tool in tools)
        {
            using var source = new Database(false, true);
            source.ReadDwgFile(catalog.ResolveSource(tool), FileOpenMode.OpenForReadAndAllShare, true, null); source.CloseInput(true);
            using (var tx = source.TransactionManager.StartTransaction())
            {
                var root = (BlockTableRecord)tx.GetObject(((BlockTable)tx.GetObject(source.BlockTableId, OpenMode.ForRead))[tool.BlockName], OpenMode.ForRead);
                var entities = root.Cast<ObjectId>().Select(id => (Entity)tx.GetObject(id, OpenMode.ForRead)).ToArray();
                inventory.Add(new { tool.BlockName, root.IsDynamicBlock, Types = entities.GroupBy(e => e.GetType().Name).ToDictionary(g => g.Key, g => g.Count()), OriginalOrder = ((DrawOrderTable)tx.GetObject(root.DrawOrderTableId, OpenMode.ForRead)).GetFullDrawOrder(0).Cast<ObjectId>().Select(id => tx.GetObject(id, OpenMode.ForRead).GetType().Name).ToArray() });
                Check(!entities.OfType<BlockReference>().Any(), tool.BlockName + ": alle inhoud rechtstreeks in definitie");
            }
            var id = SourceBlockImporter.Import(db, catalog.ResolveSource(tool), tool.BlockName); ids.Add(id);
            using (var tx = db.TransactionManager.StartTransaction())
            {
                Verify(tx, id, Check, tool.BlockName + ": import");
                var space = (BlockTableRecord)tx.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                var reference = new BlockReference(new Point3d(20 * (ids.Count % 4), 25 * (ids.Count / 4), 0), id);
                space.AppendEntity(reference); tx.AddNewlyCreatedDBObject(reference, true); tx.Commit();
            }
            Check(SourceBlockImporter.Import(db, catalog.ResolveSource(tool), tool.BlockName) == id, tool.BlockName + ": gecorrigeerde versie hergebruikt");
        }
        // Simulate a previously placed plugin definition with incorrect order.
        using (var tx = db.TransactionManager.StartTransaction())
        {
            foreach (var id in ids)
            {
                var block = (BlockTableRecord)tx.GetObject(id, OpenMode.ForRead);
                var order = (DrawOrderTable)tx.GetObject(block.DrawOrderTableId, OpenMode.ForWrite);
                var wipes = block.Cast<ObjectId>().Where(e => tx.GetObject(e, OpenMode.ForRead) is Wipeout).ToArray();
                if (wipes.Length > 0) order.MoveToTop(new ObjectIdCollection(wipes));
            }
            var masked = ids.Count(id => ((BlockTableRecord)tx.GetObject(id, OpenMode.ForRead)).Cast<ObjectId>().Any(e => tx.GetObject(e, OpenMode.ForRead) is Wipeout));
            Check(RtDrawOrder.Apply(db, tx) == masked, "bestaande plugin-RT-definities met masker allemaal hersteld");
            foreach (var id in ids) Verify(tx, id, Check, "herstel " + id.Handle);
            Check(RtDrawOrder.Apply(db, tx) == 0, "nogmaals herstellen wijzigt niets");
            tx.Commit();
        }
        var handles = ids.Select(id => id.Handle).ToArray();
        var dwg = Path.Combine(folder, "RT blocks.dwg"); db.SaveAs(dwg, DwgVersion.Current);
        db.DxfOut(Path.Combine(folder, "RT blocks.dxf"), 16, DwgVersion.Current);
        using (var saved = new Database(false, true))
        {
            saved.ReadDwgFile(dwg, FileOpenMode.OpenForReadAndAllShare, true, null); saved.CloseInput(true);
            using var tx = saved.TransactionManager.StartTransaction();
            foreach (var handle in handles) Verify(tx, saved.GetObjectId(false, handle, 0), Check, "opnieuw geopend " + handle);
        }
        foreach (var (path, hash) in hashes) Check(Hash(path) == hash, "originele bron blijft intact");
        File.WriteAllText(Path.Combine(folder, "draw-order.json"), JsonSerializer.Serialize(new { Status = "PASS", ToolCount = tools.Length, Checks = checks, Inventory = inventory }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void Verify(Transaction tx, ObjectId id, Action<bool, string> check, string label)
    {
        var block = (BlockTableRecord)tx.GetObject(id, OpenMode.ForRead);
        var order = ((DrawOrderTable)tx.GetObject(block.DrawOrderTableId, OpenMode.ForRead)).GetFullDrawOrder(0).Cast<ObjectId>().ToArray();
        var wipes = order.Count(e => tx.GetObject(e, OpenMode.ForRead) is Wipeout);
        check(order.Take(wipes).All(e => tx.GetObject(e, OpenMode.ForRead) is Wipeout) && order.Skip(wipes).All(e => tx.GetObject(e, OpenMode.ForRead) is not Wipeout), label + ": alle wipeouts onder alle overige inhoud");
        check(order.Any(e => tx.GetObject(e, OpenMode.ForRead) is Circle) && order.Any(e => tx.GetObject(e, OpenMode.ForRead) is DBText or MText or AttributeDefinition), label + ": bolletjes en tekst aanwezig boven maskers");
    }
}
