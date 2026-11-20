using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Techbase.Blocks;

internal static class BlockTemplate
{
    internal static string PathFor(Catalog catalog)
    {
        var tool = catalog.Tools.FirstOrDefault(t => Path.GetFileName(t.SourceFile).Equals(
            "Techbase template LS en OV.dwg", StringComparison.OrdinalIgnoreCase))
            ?? catalog.Tools.First();
        return Path.GetFullPath(catalog.ResolveSource(tool));
    }

    private static string PurchaseCopyPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Techbase", "Templates", "Techbase template LS en OV AANKOOP.dwg");

    /// <summary>
    /// De template bevat de AANKOOP-varianten ook daadwerkelijk als DWG-blocks.
    /// Bewerk nooit de gebundelde bron: kopieer die naar een gebruikersmap en
    /// verrijk haar daar zodat de nieuwe varianten opgeslagen kunnen worden.
    /// </summary>
    internal static (string Path, string[] Warnings, int Added) PurchaseTemplate(Catalog catalog)
    {
        var path = PurchaseCopyPath;
        if (!File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.Copy(PathFor(catalog), path);
        }

        var warnings = new List<string>();
        var added = 0;
        using var db = new Database(false, true);
        db.ReadDwgFile(path, FileOpenMode.OpenForReadAndAllShare, false, null);
        db.CloseInput(true);
        foreach (var tool in catalog.Tools.Where(t =>
            StationPurchaseVariants.IsStationTool(t) &&
            !StationPurchaseVariants.IsPurchaseName(t.BlockName)))
        {
            try
            {
                ObjectId source;
                using (var tx = db.TransactionManager.StartTransaction())
                {
                    var table = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
                    if (!table.Has(tool.BlockName))
                    {
                        warnings.Add(tool.BlockName + ": blok ontbreekt in de originele template.");
                        continue;
                    }
                    source = table[tool.BlockName];
                    tx.Commit();
                }

                var output = tool.BlockName + StationPurchaseVariants.Suffix;
                bool hadVariant;
                using (var tx = db.TransactionManager.StartTransaction())
                {
                    var table = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
                    hadVariant = table.Has(output);
                    tx.Commit();
                }
                StationPurchaseVariants.Ensure(db, source, output);
                if (!hadVariant) added++;
            }
            catch (System.Exception ex)
            {
                warnings.Add(tool.Name + ": " + ex.Message);
            }
        }
        // Ook trafoblokken die gebruikers in Stationslocaties/ZRO hebben
        // geregistreerd krijgen een gelijknamige AANKOOP-variant in deze
        // kopie van de template. De oorspronkelijke blokdefinitie blijft bestaan.
        foreach (var choice in Stationslocaties.StationBlockLibrary.Read().Blocks)
        {
            try
            {
                ObjectId original;
                using (var tx = db.TransactionManager.StartTransaction())
                {
                    original = Stationslocaties.StationBlockLibrary.Import(db, tx, choice);
                    tx.Commit();
                }
                var name = choice.Name + StationPurchaseVariants.Suffix;
                bool exists;
                using (var tx = db.TransactionManager.StartTransaction())
                {
                    var blocks = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
                    exists = blocks.Has(name);
                    tx.Commit();
                }
                StationPurchaseVariants.Ensure(db, original, name);
                if (!exists) added++;
            }
            catch (System.Exception ex)
            {
                warnings.Add(choice.Name + ": " + ex.Message);
            }
        }
        if (added != 0)
            db.SaveAs(path, DwgVersion.Current);
        return (path, warnings.ToArray(), added);
    }
}

public sealed partial class Plugin
{
    [CommandMethod("TBTEMPLATE", CommandFlags.Session)]
    public void OpenBlockTemplate()
    {
        try
        {
            var catalog = Blocks ?? new Catalog(DefaultPalette);
            var documents = AcApp.DocumentManager;
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Techbase", "Templates", "Techbase template LS en OV AANKOOP.dwg");
            var opened = documents.Cast<Document>().FirstOrDefault(d =>
                Path.GetFullPath(d.Name).Equals(path, StringComparison.OrdinalIgnoreCase));

            // Als de kopie al in AutoCAD openstaat, niet achter de gebruiker
            // om de inhoud van diezelfde DWG overschrijven.
            var result = opened == null
                ? BlockTemplate.PurchaseTemplate(catalog)
                : (path, Array.Empty<string>(), 0);
            documents.MdiActiveDocument = opened ?? documents.Open(result.Item1, false);
            var editor = documents.MdiActiveDocument?.Editor;
            editor?.WriteMessage($"\nAANKOOP-template: {result.Item3} nieuwe trafoblokken toegevoegd. " +
                "De normale TechBase-template is behouden.");
            if (result.Item2.Length > 0)
                editor?.WriteMessage("\nNiet toegevoegd: " + string.Join("; ", result.Item2));
        }
        catch (System.Exception ex) { Error(ex); }
    }
}
