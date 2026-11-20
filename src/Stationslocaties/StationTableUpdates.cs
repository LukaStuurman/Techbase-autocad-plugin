using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Stationslocaties;

/// <summary>
/// Houdt A3-detailkaarten en C1-C4 bij na het verplaatsen of draaien van
/// trafoblokken. Een command-event meldt dat er iets kan zijn veranderd;
/// een lichte, periodieke vergelijking van de echte blocktransformaties
/// vangt ook gripbewerkingen en wijzigingen via eigenschappen op.
/// Tekeningen worden uitsluitend bijgewerkt vanuit Application.Idle.
/// </summary>
public sealed class StationTableUpdates : IExtensionApplication
{
    private readonly Dictionary<Database, HashSet<ObjectId>> pending = new();
    private readonly Dictionary<Database, Document> trackedDocuments = new();
    private readonly Dictionary<Database, Dictionary<ObjectId, StationPose>> knownPoses = new();
    private readonly Dictionary<Database, DateTime> lastPoll = new();
    private readonly Dictionary<Database, DateTime> lastDiscovery = new();
    private readonly HashSet<Database> rescanAll = new();
    private readonly HashSet<Database> discoverStations = new();
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan DiscoveryInterval = TimeSpan.FromSeconds(15);
    private bool updating;

    private readonly record struct StationPose(
        Point3d Position, double Rotation, Scale3d Scale, Vector3d Normal)
    {
        internal bool ApproximatelyEquals(StationPose other) =>
            Position.DistanceTo(other.Position) < 0.0000001 &&
            Math.Abs(Rotation - other.Rotation) < 0.0000001 &&
            Math.Abs(Scale.X - other.Scale.X) < 0.0000001 &&
            Math.Abs(Scale.Y - other.Scale.Y) < 0.0000001 &&
            Math.Abs(Scale.Z - other.Scale.Z) < 0.0000001 &&
            (Normal - other.Normal).Length < 0.0000001;
    }

    private static readonly HashSet<string> TransformCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "MOVE", "3DMOVE", "STRETCH", "ROTATE", "3DROTATE", "ROTATE3D",
        "SCALE", "MIRROR", "ALIGN", "UNDO", "U", "REDO",
        "CHPROP", "CHANGE", "PROPERTIES", "COPY", "PASTECLIP"
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
        discoverStations.Add(database);
        rescanAll.Add(database); // Herstel ook een blad dat eerder al achterliep.
        database.ObjectModified += StationModified;
        database.ObjectAppended += StationAppended;
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
        database.ObjectModified -= StationModified;
        database.ObjectAppended -= StationAppended;
        pending.Remove(database);
        trackedDocuments.Remove(database);
        knownPoses.Remove(database);
        lastPoll.Remove(database);
        lastDiscovery.Remove(database);
        rescanAll.Remove(database);
        discoverStations.Remove(database);
    }

    private void DocumentToBeDestroyed(object sender, DocumentCollectionEventArgs args)
    {
        StationReferences.Forget(args.Document);
        Untrack(args.Document);
    }

    private void StationModified(object sender, ObjectEventArgs args)
    {
        if (updating || args.DBObject is not BlockReference { IsErased: false } station) return;
        var database = station.Database;
        if (database != null && pending.TryGetValue(database, out var changed) &&
            StationLocationSheets.IsStationLayer(station.Layer))
            changed.Add(station.ObjectId);
    }

    private void StationAppended(object sender, ObjectEventArgs args)
    {
        if (updating || args.DBObject is not BlockReference station) return;
        var database = station.Database;
        if (database != null && pending.ContainsKey(database))
            discoverStations.Add(database);
    }

    private void StationCommandFinished(object sender, CommandEventArgs args)
    {
        var command = args.GlobalCommandName?.Trim().TrimStart('_', '.', '-') ?? "";
        if (!TransformCommands.Contains(command) &&
            !command.StartsWith("GRIP", StringComparison.OrdinalIgnoreCase)) return;

        // Na afloop van het commando (niet tijdens de drag-preview) synchroniseren.
        var database = (sender as Document)?.Database ?? AcApp.DocumentManager.MdiActiveDocument?.Database;
        if (database != null && pending.ContainsKey(database)) rescanAll.Add(database);
    }

    private static bool IsLive(ObjectId id) =>
        !id.IsNull && id.IsValid && !id.IsErased && !id.IsEffectivelyErased;

    private static Dictionary<ObjectId, StationPose> ReadPoses(Database db, IEnumerable<ObjectId>? known)
    {
        using var tr = db.TransactionManager.StartTransaction();
        IEnumerable<ObjectId> ids = known ??
            ((BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead))
            .Cast<ObjectId>();
        var result = new Dictionary<ObjectId, StationPose>();
        foreach (var id in ids)
        {
            if (!IsLive(id)) continue;
            if (tr.GetObject(id, OpenMode.ForRead) is not BlockReference station ||
                !StationLocationSheets.IsStationLayer(station.Layer)) continue;
            result.Add(id, new StationPose(station.Position, station.Rotation,
                station.ScaleFactors, station.Normal));
        }
        return result;
    }

    private void UpdateOnIdle(object? sender, EventArgs args)
    {
        var document = AcApp.DocumentManager.MdiActiveDocument;
        if (updating || document == null || !string.IsNullOrEmpty(document.CommandInProgress) ||
            !pending.TryGetValue(document.Database, out var changes)) return;

        var database = document.Database;
        var now = DateTime.UtcNow;
        var due = !lastPoll.TryGetValue(database, out var polled) || now - polled >= PollInterval;
        var force = rescanAll.Contains(database);
        var discovery = discoverStations.Contains(database) || !knownPoses.ContainsKey(database) ||
            !lastDiscovery.TryGetValue(database, out var discovered) || now - discovered >= DiscoveryInterval;
        if (!due && !force && changes.Count == 0 && !discovery) return;

        // Haal de wachtrij pas weg wanneer we gaan verwerken. Nieuwe
        // ObjectModified-events tijdens het wegschrijven blijven zo bewaard.
        var queuedIds = changes.ToArray();
        changes.Clear();
        var requestedFull = rescanAll.Remove(database);
        var requestedDiscovery = discoverStations.Remove(database);
        updating = true;
        try
        {
            bool updated = false;
            using (document.LockDocument())
            {
                knownPoses.TryGetValue(database, out var previously);
                var scanEntireModel = discovery || requestedDiscovery || previously == null;
                var current = ReadPoses(database, scanEntireModel ? null : previously!.Keys);
                var changedIds = new HashSet<ObjectId>(queuedIds);
                foreach (var pair in current)
                    if (previously == null || !previously.TryGetValue(pair.Key, out var old) ||
                        !pair.Value.ApproximatelyEquals(old))
                        changedIds.Add(pair.Key);

                if (requestedFull || previously == null || changedIds.Count != 0)
                    updated = StationLocationSheets.UpdateMovedStationTables(
                        database, changedIds.ToArray(), requestedFull || previously == null);

                knownPoses[database] = current;
                lastPoll[database] = now;
                if (scanEntireModel) lastDiscovery[database] = now;
            }
            if (updated) document.Editor.Regen();
        }
        catch (Autodesk.AutoCAD.Runtime.Exception ex) when (ex.ErrorStatus == ErrorStatus.LockViolation)
        {
            changes.UnionWith(queuedIds);
            if (requestedFull) rescanAll.Add(database);
            if (requestedDiscovery) discoverStations.Add(database);
        }
        catch (System.Exception ex)
        {
            // Geen veranderingen kwijtraken bij tijdelijke AutoCAD-status
            // (bijv. als een viewport nog in gebruik is).
            changes.UnionWith(queuedIds);
            if (requestedFull) rescanAll.Add(database);
            if (requestedDiscovery) discoverStations.Add(database);
            document.Editor.WriteMessage("\nStationslocaties: kaart en C1-C4 konden niet worden bijgewerkt: " + ex.Message);
        }
        finally { updating = false; }
    }
}
