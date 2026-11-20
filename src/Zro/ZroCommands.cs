using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Techbase.Blocks.Zro;

namespace Techbase.Blocks;

public sealed partial class Plugin
{
    internal static ZroCreateRequest? PendingZroCreate { get; set; }
    internal static ZroOpenRequest? PendingZroOpen { get; set; }

    [CommandMethod("TBZRO", CommandFlags.Modal)]
    public void ZroWorkspace()
    {
        Show();
        workspace?.Navigate(7);
        zroPanel?.RefreshSheets();
    }

    [CommandMethod("TBZROKIES", CommandFlags.Modal)]
    public void ZroChooseStation()
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc == null) return;
        try { zroPanel?.ChooseStation(doc); }
        catch (System.Exception ex) { Error(ex); }
    }

    [CommandMethod("TBZROMAAK", CommandFlags.Modal)]
    public void ZroCreateSheet()
    {
        var request = PendingZroCreate;
        PendingZroCreate = null;
        if (request == null) return;
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc == null || doc != request.Document)
        {
            zroPanel?.ReportResult("ZRO: de actieve tekening is gewijzigd. Kies de trafo opnieuw.");
            return;
        }
        try
        {
            ZroSheets.CreateOrUpdate(doc, request.Station, request.Fields, request.Pdok);
            zroPanel?.ReportResult("ZRO-blad gemaakt of bijgewerkt: " + ZroSheets.NewName(request.Fields.StationName));
        }
        catch (System.Exception ex)
        {
            doc.Editor.WriteMessage("\nZRO-blad: " + ex.Message);
            zroPanel?.ReportResult(ex.Message);
        }
    }

    [CommandMethod("TBZROOPEN", CommandFlags.Modal)]
    public void ZroOpenSheet()
    {
        var request = PendingZroOpen;
        PendingZroOpen = null;
        if (request == null) return;
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc == null || doc != request.Document) return;
        try
        {
            if (!LayoutManager.Current.LayoutExists(request.LayoutName))
                throw new System.InvalidOperationException("Dit ZRO-blad bestaat niet meer.");
            LayoutManager.Current.CurrentLayout = request.LayoutName;
            doc.Editor.Regen();
        }
        catch (System.Exception ex) { Error(ex); }
    }
}
