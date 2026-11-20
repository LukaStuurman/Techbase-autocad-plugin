using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Stationslocaties;

/// <summary>
/// Na verplaatsen, gripbewerken of draaien van een trafoblok worden het
/// detailkaartvenster en de hoekpunten (C1-C4) bijgewerkt. CAD-mutaties
/// gebeuren uitsluitend in de actieve documentcontext tijdens Idle.
/// </summary>
public sealed class StationTableUpdates : IExtensionApplication
{
    private readonly Dictionary<Database, HashSet<ObjectId>> pending = new();
    private readonly Dictionary<Database, Document> trackedDocuments = new();
    private readonly HashSet<Database> rescanAll = new();

    private static readonly HashSet<string> TransformCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "MOVE", "3DMOVE", "STRETCH", "ROTATE", "3DROTATE", "ROTATE3D",
        "SCALE", "MIRROR", "ALIGN", "UNDO", "U", "REDO"
    };

    public void Initialize()
    {
        var documents = AcApp.DocumentManager;
        documents.DocumentCreated += DocumentCreated;
        documents.DocumentToBeDestroyed += DocumentToBeDestroyed;
        foreach (Document document in documents) Track(document);
        AcApp.Idle += UpdateOnIdle;
    }

    public void Terminate()
    {
        AcApp.Idle -= UpdateOnIdle;
        var documents = AcApp.DocumentManager;
        documents.DocumentCreated -= DocumentCreated;
        documents.DocumentToBeDestroyed -= DocumentToBeDestroyed;
        foreach (var document in trackedDocuments.Values.ToArray()) Untrack(document);
    }

    private void DocumentCreated(object sender, DocumentCollectionEventArgs args) => Track(args.Document);

    private void Track(Document document)
    {
        var database = document.Database;
        if (!pending.TryAdd(database, new HashSet<ObjectId>())) return;
        trackedDocuments.Add(database, document);
        database.ObjectModified += StationModified;
        document.CommandEnded += StationCommandFinished;
        document.CommandCancelled += StationCommandFinished;
        document.CommandFailed += StationCommandFinished;
    }

    private void Untrack(Document document)
    {
        document.CommandEnded -= StationCommandFinished;
        document.CommandCancelled -= StationCommandFinished;
        document.CommandFailed -= StationCommandFinished;
        var database = document.Database;
        if (pending.Remove(database)) database.ObjectModified -= StationModified;
        trackedDocuments.Remove(database);
        rescanAll.Remove(database);
    }

    private void DocumentToBeDestroyed(object sender, DocumentCollectionEventArgs args)
    {
        StationReferences.Forget(args.Document);
        Untrack(args.Document);
    }

    private void StationModified(object sender, ObjectEventArgs args)
    {
        // Gebruik de database van het werkelijke gewijzigde object: event-sender
        // verschilt bij sommige CAD-bewerkingen en versies.
        if (args.DBObject is not BlockReference { IsErased: false } station) return;
        var database = station.Database;
        if (database != null && pending.TryGetValue(database, out var changed) &&
            StationLocationSheets.IsStationLayer(station.Layer))
            changed.Add(station.ObjectId);
    }

    private void StationCommandFinished(object sender, CommandEventArgs args)
    {
        var command = args.GlobalCommandName?.Trim().TrimStart('_', '.', '-') ?? "";
        if (!TransformCommands.Contains(command) &&
            !command.StartsWith("GRIP", StringComparison.OrdinalIgnoreCase)) return;

        // Zelfs als ObjectModified tijdens slepen geen BlockReference meldt,
        // wordt na het commando de actuele modelpositie opnieuw gecontroleerd.
        var database = (sender as Document)?.Database ?? AcApp.DocumentManager.MdiActiveDocument?.Database;
        if (database != null && pending.ContainsKey(database)) rescanAll.Add(database);
    }

    private void UpdateOnIdle(object? sender, EventArgs args)
    {
        var document = AcApp.DocumentManager.MdiActiveDocument;
        if (document == null || !string.IsNullOrEmpty(document.CommandInProgress) ||
            !pending.TryGetValue(document.Database, out var changes)) return;

        var database = document.Database;
        var checkAll = rescanAll.Remove(database);
        if (changes.Count == 0 && !checkAll) return;

        var stationIds = changes.ToArray();
        changes.Clear();
        try
        {
            bool updated;
            using (document.LockDocument())
                updated = StationLocationSheets.UpdateMovedStationTables(database, stationIds, checkAll);
            // Het opslaan van ViewTarget en C1-C4 is niet genoeg: de actieve
            // AutoCAD-weergave moet eveneens opnieuw worden gegenereerd.
            if (updated) document.Editor.Regen();
        }
        catch (Autodesk.AutoCAD.Runtime.Exception ex) when (ex.ErrorStatus == ErrorStatus.LockViolation)
        {
            changes.UnionWith(stationIds);
            if (checkAll) rescanAll.Add(database);
        }
        catch (System.Exception ex)
        {
            document.Editor.WriteMessage("\nStationslocaties (kaartuitsnede/C1-C4): " + ex.Message);
        }
    }
}
