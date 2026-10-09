using System;
using Autodesk.AutoCAD.DatabaseServices;

namespace Techbase.Blocks;

internal sealed class WorkingDatabaseScope : IDisposable
{
    private readonly Database previous;
    internal WorkingDatabaseScope(Database database)
    {
        previous = HostApplicationServices.WorkingDatabase;
        HostApplicationServices.WorkingDatabase = database;
    }
    public void Dispose() => HostApplicationServices.WorkingDatabase = previous;
}
