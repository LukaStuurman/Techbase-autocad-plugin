using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Techbase.Blocks;

internal static class BlockTemplate
{
    internal static string PathFor(Catalog catalog)
    {
        var tool = catalog.Tools.FirstOrDefault(t => System.IO.Path.GetFileName(t.SourceFile).Equals("Techbase template LS en OV.dwg", StringComparison.OrdinalIgnoreCase))
            ?? catalog.Tools.First();
        return System.IO.Path.GetFullPath(catalog.ResolveSource(tool));
    }
}

public sealed partial class Plugin
{
    [CommandMethod("TBTEMPLATE", CommandFlags.Session)]
    public void OpenBlockTemplate()
    {
        try
        {
            var path = BlockTemplate.PathFor(Blocks ?? new Catalog(DefaultPalette));
            var documents = AcApp.DocumentManager;
            var opened = documents.Cast<Document>().FirstOrDefault(d => Path.GetFullPath(d.Name).Equals(path, StringComparison.OrdinalIgnoreCase));
            documents.MdiActiveDocument = opened ?? documents.Open(path, false);
        }
        catch (System.Exception ex) { Error(ex); }
    }
}
