using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

namespace Techbase.Blocks.Zro;

/// <summary>
/// ZRO-bladen uit het zelfstandige ZRO-A4-sjabloon (niet uit Stationslocaties).
/// Het sjabloon bevat Kader-A4s-MSR en Enexis-OH-MSR en twee viewports.
/// De bron DXF wordt alleen gelezen; nieuwe bladen komen in de huidige DWG.
/// </summary>
internal static class ZroSheets
{
    private const string RegApp = "TECHBASE_ZRO";
    private const string LabelLayer = "TECHBASE_ZRO_KADASTRALE_NUMMERS";

    internal static string NewName(string stationName)
    {
        var clean = new string(stationName.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or ' ').ToArray()).Trim();
        if (clean.Length == 0) throw new InvalidOperationException("Vul een trafonaam in.");
        return ("ZRO - " + clean)[..Math.Min(70, ("ZRO - " + clean).Length)];
    }

    internal static IReadOnlyList<string> List(Database db)
    {
        using var tr = db.TransactionManager.StartTransaction();
        var dictionary = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
        var names = new List<string>();
        foreach (DBDictionaryEntry item in dictionary)
        {
            if (item.Key.StartsWith("ZRO - ", StringComparison.OrdinalIgnoreCase))
                names.Add(item.Key);
        }
        return names.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    internal static bool IsZroLayout(Layout layout) =>
        layout.GetXDataForApplication(RegApp) is { } metadata &&
        metadata.AsArray().Length >= 2;

    internal static void CreateOrUpdate(Document document, ObjectId stationId, ZroFields values,
        ZroPdokResult pdok)
    {
        if (stationId.Database != document.Database || stationId.IsErased)
            throw new InvalidOperationException("Kies eerst een geldig trafoblok uit de actieve tekening.");
        if (string.IsNullOrWhiteSpace(values.DrawingNumber))
            throw new InvalidOperationException("Vul eerst een ZRO-tekeningnummer in.");
        if (string.IsNullOrWhiteSpace(values.ProjectCode))
            throw new InvalidOperationException("Vul eerst een projectnummer/code in.");
        var name = NewName(values.StationName);
        var layoutManager = LayoutManager.Current;
        var exists = layoutManager.LayoutExists(name);

        ZroFootprint footprint;
        Point3d stationPoint;
        using (var tr = document.Database.TransactionManager.StartTransaction())
        {
            var station = (BlockReference)tr.GetObject(stationId, OpenMode.ForRead);
            footprint = ZroFootprint.FromStation(tr, station);
            stationPoint = station.Position;
        }

        // Eénmalig een bron-DXF selecteren als die nog niet in de projectinstellingen
        // staat. Een origineel ZRO-model werkt direct zonder het trafo-3-blad te kopiëren.
        var template = values.TemplatePath;
        if (template.Length == 0 && document.Name.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase))
            template = document.Name;
        if (!exists && !File.Exists(template))
            throw new FileNotFoundException(
                "Kies eerst het ZRO-sjabloon (de DXF met TRAFO-10 of TRAFO-4) onder 'Sjabloon kiezen'.", template);

        var attrs = values.Attributes(footprint.BlockType, footprint);
        if (exists)
        {
            using var tr = document.Database.TransactionManager.StartTransaction();
            var layout = GetLayout(document.Database, tr, name);
            if (!IsZroLayout(layout))
                throw new InvalidOperationException("Er bestaat al een blad met deze naam dat niet door de ZRO-plugin is gemaakt.");
            UpdateLayout(document.Database, tr, layout, stationPoint, attrs, values.Department, pdok.SurroundingParcels);
            SetMetadata(document.Database, tr, layout, stationId, values);
            tr.Commit();
        }
        else
        {
            using var templateDb = new Database(false, true);
            // DxfIn is alleen veilig op een nieuw gemaakte, lege database.
            templateDb.DxfIn(template, "");
            using var sourceTr = templateDb.TransactionManager.StartTransaction();
            var sourceLayouts = (DBDictionary)sourceTr.GetObject(templateDb.LayoutDictionaryId, OpenMode.ForRead);
            var sourceName = new[] { "ZRO_TEMPLATE", "TRAFO-10", "TRAFO-4" }.FirstOrDefault(sourceLayouts.Contains);
            if (sourceName == null)
                throw new InvalidOperationException("Geen standaard ZRO-layout gevonden (TRAFO-10 of TRAFO-4). TRAFO-3 is bewust geen standaard.");
            var sourceLayout = (Layout)sourceTr.GetObject(sourceLayouts.GetAt(sourceName), OpenMode.ForRead);
            var sourceSpace = (BlockTableRecord)sourceTr.GetObject(sourceLayout.BlockTableRecordId, OpenMode.ForRead);
            var sourceEntities = new ObjectIdCollection();
            foreach (ObjectId id in sourceSpace)
            {
                if (sourceTr.GetObject(id, OpenMode.ForRead) is Viewport vp &&
                    vp.Width > 250) continue; // Papier-/standaardviewport, geen kaart.
                sourceEntities.Add(id);
            }

            // CreateLayout heeft zijn eigen PaperSpace. De template-objecten worden
            // inclusief twee viewports en titleblock-referenties gekloond.
            var layoutId = layoutManager.CreateLayout(name);
            try
            {
                using var tr = document.Database.TransactionManager.StartTransaction();
                var layout = (Layout)tr.GetObject(layoutId, OpenMode.ForWrite);
                var targetSpace = (BlockTableRecord)tr.GetObject(layout.BlockTableRecordId, OpenMode.ForWrite);
                templateDb.WblockCloneObjects(sourceEntities, targetSpace.ObjectId,
                    new IdMapping(), DuplicateRecordCloning.Ignore, false);
                layout.CopyFrom(sourceLayout);
                UpdateLayout(document.Database, tr, layout, stationPoint, attrs, values.Department, pdok.SurroundingParcels);
                SetMetadata(document.Database, tr, layout, stationId, values);
                tr.Commit();
            }
            catch
            {
                // Laat geen gedeeltelijk gemaakt detailblad achter.
                try { if (layoutManager.LayoutExists(name)) layoutManager.DeleteLayout(name); }
                catch (Exception) { }
                throw;
            }
        }
        values.SaveDefaults();
        document.Editor.Regen();
    }

    private static Layout GetLayout(Database db, Transaction tr, string name)
    {
        var layouts = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
        return (Layout)tr.GetObject(layouts.GetAt(name), OpenMode.ForWrite);
    }

    private static void SetMetadata(Database db, Transaction tr, Layout layout, ObjectId station, ZroFields fields)
    {
        var apps = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
        if (!apps.Has(RegApp))
        {
            apps.UpgradeOpen();
            var app = new RegAppTableRecord { Name = RegApp };
            apps.Add(app);
            tr.AddNewlyCreatedDBObject(app, true);
        }
        var data = new ResultBuffer(
            new TypedValue(1001, RegApp),
            new TypedValue(1000, station.Handle.ToString()),
            new TypedValue(1000, fields.StationName),
            new TypedValue(1000, fields.ProjectCode),
            new TypedValue(1000, fields.DrawingNumber));
        layout.XData = data;
        data.Dispose();
    }

    private static void UpdateLayout(Database db, Transaction tr, Layout layout, Point3d position,
        IReadOnlyDictionary<string, string> attrs, string department, IReadOnlyList<ZroParcel> parcels)
    {
        var space = (BlockTableRecord)tr.GetObject(layout.BlockTableRecordId, OpenMode.ForWrite);
        Viewport? detail = null, overview = null;
        var frameFound = false;
        var titleFound = false;
        foreach (ObjectId id in space)
        {
            var entity = tr.GetObject(id, OpenMode.ForRead);
            if (entity is Viewport viewport)
            {
                if (Math.Abs(viewport.Width - 190) < 2 && Math.Abs(viewport.Height - 140.5) < 2)
                    detail = viewport;
                else if (Math.Abs(viewport.Width - 135) < 2 && Math.Abs(viewport.Height - 94.5) < 2)
                    overview = viewport;
            }
            if (entity is BlockReference block)
            {
                var ownTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (ObjectId attId in block.AttributeCollection)
                {
                    if (tr.GetObject(attId, OpenMode.ForRead) is AttributeReference attr)
                        ownTags.Add(attr.Tag);
                }
                frameFound |= ownTags.Contains("KAD.GEM");
                titleFound |= ownTags.Contains("OMSCHRIJVING{108}");
                foreach (ObjectId attId in block.AttributeCollection)
                {
                    if (tr.GetObject(attId, OpenMode.ForWrite) is AttributeReference attr &&
                        attrs.TryGetValue(attr.Tag, out var value))
                        attr.TextString = value;
                }
            }
        }
        if (!frameFound || !titleFound || detail == null || overview == null)
            throw new InvalidOperationException("ZRO-sjabloon mist een kaderblok, onderhoek of één van de twee kaartviewports.");

        Center(detail, position, 100);
        Center(overview, position, 1000);
        SetDepartment(db, tr, space, department);
        DrawParcelLabels(db, tr, space, detail, position, parcels);
    }

    private static void Center(Viewport viewport, Point3d point, int denominator)
    {
        if (!viewport.IsWriteEnabled) viewport.UpgradeOpen();
        viewport.Locked = false;
        viewport.ViewDirection = Vector3d.ZAxis;
        viewport.TwistAngle = 0;
        viewport.ViewTarget = point;
        viewport.ViewCenter = Point2d.Origin;
        viewport.ViewHeight = viewport.Height * denominator / 1000d;
        viewport.GridOn = false;
        viewport.On = true;
        viewport.Locked = true;
    }

    private static ObjectId Layer(Database db, Transaction tr, string name, short colorIndex)
    {
        var layers = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
        if (layers.Has(name)) return layers[name];
        layers.UpgradeOpen();
        var layer = new LayerTableRecord { Name = name, Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
            Autodesk.AutoCAD.Colors.ColorMethod.ByAci, colorIndex) };
        var id = layers.Add(layer);
        tr.AddNewlyCreatedDBObject(layer, true);
        return id;
    }

    private static void SetDepartment(Database db, Transaction tr, BlockTableRecord space, string department)
    {
        const string layerName = "TECHBASE_ZRO_AFDELING";
        var layer = Layer(db, tr, layerName, 7);
        foreach (ObjectId id in space)
        {
            if (tr.GetObject(id, OpenMode.ForRead) is Entity { LayerId: var entityLayer } e &&
                entityLayer == layer)
            {
                e.UpgradeOpen();
                e.Erase();
            }
        }
        // Afdeling is in het bronkader vaste TEXT, geen ATTDEF.
        // Witte achtergrond maskeert de standaard 'Grondzaken', de nieuwe waarde
        // wordt in paper-space op precies dezelfde plaats geschreven.
        var overlay = new Solid(
            new Point3d(151.0, 72.6, 0), new Point3d(200, 72.6, 0),
            new Point3d(151.0, 76.1, 0), new Point3d(200, 76.1, 0))
        {
            LayerId = layer,
            Color = Autodesk.AutoCAD.Colors.Color.FromRgb(255, 255, 255)
        };
        space.AppendEntity(overlay);
        tr.AddNewlyCreatedDBObject(overlay, true);
        var text = new DBText
        {
            TextString = "Afdeling :  " + (department.Trim().Length == 0 ? "—" : department.Trim()),
            Height = 2,
            Position = new Point3d(151.1, 73.7131405446, 0),
            LayerId = layer
        };
        space.AppendEntity(text);
        tr.AddNewlyCreatedDBObject(text, true);
    }

    private static void DrawParcelLabels(Database db, Transaction tr, BlockTableRecord space,
        Viewport detail, Point3d station, IReadOnlyList<ZroParcel> parcels)
    {
        var layer = Layer(db, tr, LabelLayer, 5); // opvallend blauw, alleen op de detailkaart
        foreach (ObjectId id in space)
            if (tr.GetObject(id, OpenMode.ForRead) is Entity { LayerId: var entityLayer } e &&
                entityLayer == layer)
            {
                e.UpgradeOpen();
                e.Erase();
            }
        var already = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parcel in parcels)
        {
            var label = parcel.Label;
            if (label.Length == 0 || !already.Add(label)) continue;
            var x = detail.CenterPoint.X + (parcel.LabelPoint.X - station.X) * 10d;
            var y = detail.CenterPoint.Y + (parcel.LabelPoint.Y - station.Y) * 10d;
            // Alleen percelen die in de bovenste viewport zichtbaar zijn.
            if (Math.Abs(x - detail.CenterPoint.X) > detail.Width / 2 - 5 ||
                Math.Abs(y - detail.CenterPoint.Y) > detail.Height / 2 - 4)
                continue;
            var text = new MText
            {
                Contents = label, TextHeight = 3,
                Attachment = AttachmentPoint.MiddleCenter,
                Location = new Point3d(x, y, 0),
                LayerId = layer
            };
            space.AppendEntity(text);
            tr.AddNewlyCreatedDBObject(text, true);
        }
    }
}
