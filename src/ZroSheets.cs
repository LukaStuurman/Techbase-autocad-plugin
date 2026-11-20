using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Techbase.Blocks;

/// <summary>
/// ZRO-bladen naar de Bakel-voorbeeldtekening (TRAFO-10 en TRAFO-4).
/// De twee model-viewports volgen de RD-trafo met vaste schalen 1:100 en 1:1000.
/// Een blad gebruikt dezelfde kader- en Enexis-blokdefinities als het bronmodel.
/// </summary>
internal static class ZroSheets
{
    private const string AppName = "TB_ZRO_SHEET";
    private const string TemplateSheet = "TRAFO-10";
    private const string DataKey = "TB_ZRO_FIELDS";
    private static readonly CultureInfo Dutch = CultureInfo.GetCultureInfo("nl-NL");
    private static string? templatePath;
    internal static ZroFields Defaults { get; private set; } = new();

    internal static IReadOnlyList<string> List(Database db)
    {
        var result = new List<string>();
        using var tx = db.TransactionManager.StartTransaction();
        var layouts = (DBDictionary)tx.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
        foreach (DBDictionaryEntry item in layouts)
        {
            var layout = (Layout)tx.GetObject(item.Value, OpenMode.ForRead);
            using var metadata = layout.GetXDataForApplication(AppName);
            if (metadata?.AsArray() is { Length: >= 2 }) result.Add(layout.LayoutName);
        }
        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    internal static bool HasSourceLayout(Database db)
    {
        using var tx = db.TransactionManager.StartTransaction();
        var layouts = (DBDictionary)tx.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
        return layouts.Contains(TemplateSheet);
    }

    internal static string? SelectTemplateIfNeeded(Database db)
    {
        if (HasSourceLayout(db)) return null;
        if (templatePath != null && File.Exists(templatePath)) return templatePath;
        using var chooser = new OpenFileDialog
        {
            Title = "Kies één keer het ZRO-voorbeeldblad (DWG of DXF, bijvoorbeeld Bakel)",
            Filter = "ZRO-tekening (*.dwg;*.dxf)|*.dwg;*.dxf",
            CheckFileExists = true, Multiselect = false
        };
        if (chooser.ShowDialog() != DialogResult.OK) return null;
        templatePath = chooser.FileName;
        return templatePath;
    }

    internal static void Create(Document doc, ObjectId stationId, ZroFields fields,
        ZroFootprint footprint, IReadOnlyList<ZroParcelLabel> autoLabels)
    {
        var db = doc.Database;
        if (db.Insunits is not (UnitsValue.Meters or UnitsValue.Undefined))
            throw new InvalidOperationException("ZRO vereist een model in meters (RD New). Controleer INSUNITS.");
        if (footprint.Center.X is < 0 or > 300000 || footprint.Center.Y is < 250000 or > 650000)
            throw new InvalidOperationException("ZRO vereist RD New X/Y-coördinaten (EPSG:28992).");
        var name = SafeLayoutName(fields.SuggestedLayout);
        if (LayoutManager.Current.LayoutExists(name))
            throw new InvalidOperationException("Er bestaat al een ZRO-blad '" + name + "'.");

        var source = SelectTemplateIfNeeded(db);
        if (source == null && !HasSourceLayout(db))
            throw new InvalidOperationException("Kies een ZRO-sjabloonbestand met het blad TRAFO-10.");
        if (source == null)
        {
            // Zelfde DWG als het aangeleverde voorbeeld: 1:1 kopie inclusief
            // kader, Noordpijl, titelblokken, hatch en twee viewports.
            LayoutManager.Current.CopyLayout(TemplateSheet, name);
        }
        else
        {
            ImportLayoutFromFile(db, source, name);
        }

        try
        {
            using var tx = db.TransactionManager.StartTransaction();
            var layout = GetLayout(db, tx, name, OpenMode.ForWrite);
            UpdateAttributes(db, tx, layout, fields, footprint);
            CenterViewports(tx, layout, footprint.Center);
            WriteMetadata(db, tx, layout, stationId, fields);
            AddParcelLabels(db, tx, layout, footprint.Center, autoLabels);
            tx.Commit();
            Defaults = fields.Copy();
            doc.Editor.Regen();
        }
        catch
        {
            // Laat geen half aangemaakt ZRO-blad staan.
            if (LayoutManager.Current.LayoutExists(name))
                LayoutManager.Current.DeleteLayout(name);
            throw;
        }
    }

    internal static bool Refresh(Document doc, string layoutName)
    {
        var db = doc.Database;
        using var tx = db.TransactionManager.StartTransaction();
        var layout = GetLayout(db, tx, layoutName, OpenMode.ForWrite);
        var stationId = AttachedStation(db, layout);
        if (stationId.IsNull || !stationId.IsValid || stationId.IsErased)
            throw new InvalidOperationException("De gekoppelde trafo bestaat niet meer: " + layoutName);
        var footprint = ZroGeometry.Inspect(db, stationId);
        CenterViewports(tx, layout, footprint.Center);
        UpdateCorners(tx, layout, footprint.Corners);
        tx.Commit();
        doc.Editor.Regen();
        return true;
    }

    internal static void RefreshAll(Document doc)
    {
        foreach (var name in List(doc.Database)) Refresh(doc, name);
    }

    private static string SafeLayoutName(string requested)
    {
        var s = new string(requested.Where(c =>
            !char.IsControl(c) && "\\/:;<>?*|=\"".IndexOf(c) < 0).ToArray()).Trim();
        if (s.Length > 80) s = s[..80];
        if (s.Length == 0) throw new InvalidOperationException("Geef een geldige trafonaam op.");
        return s;
    }

    private static Layout GetLayout(Database db, Transaction tx, string name, OpenMode mode = OpenMode.ForRead)
    {
        var layouts = (DBDictionary)tx.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
        if (!layouts.Contains(name)) throw new InvalidOperationException("Layout ontbreekt: " + name);
        return (Layout)tx.GetObject(layouts.GetAt(name), mode);
    }

    private static void ImportLayoutFromFile(Database target, string filename, string outputName)
    {
        using var source = new Database(false, true);
        if (string.Equals(Path.GetExtension(filename), ".dxf", StringComparison.OrdinalIgnoreCase))
        {
            source.DxfIn(filename, "");
        }
        else
        {
            source.ReadDwgFile(filename, FileShare.Read, true, "");
            source.CloseInput(true);
        }

        using var srcTransaction = source.TransactionManager.StartTransaction();
        var layout = GetLayout(source, srcTransaction, TemplateSheet);
        var paper = (BlockTableRecord)srcTransaction.GetObject(layout.BlockTableRecordId, OpenMode.ForRead);
        var ids = new ObjectIdCollection();
        foreach (ObjectId id in paper)
        {
            if (srcTransaction.GetObject(id, OpenMode.ForRead) is Viewport vp && vp.Width > 250)
                continue; // Papier-viewport (nummer 1) hoort niet meegekopieerd.
            ids.Add(id);
        }
        LayoutManager.Current.CreateLayout(outputName);
        using var dstTransaction = target.TransactionManager.StartTransaction();
        var destination = GetLayout(target, dstTransaction, outputName);
        var mapping = new IdMapping();
        source.WblockCloneObjects(ids, destination.BlockTableRecordId,
            mapping, DuplicateRecordCloning.Ignore, false);
        dstTransaction.Commit();
    }

    private static IEnumerable<Entity> PaperEntities(Transaction tx, Layout layout)
    {
        var record = (BlockTableRecord)tx.GetObject(layout.BlockTableRecordId, OpenMode.ForRead);
        foreach (ObjectId id in record)
            if (id.IsValid && !id.IsErased &&
                tx.GetObject(id, OpenMode.ForRead) is Entity entity) yield return entity;
    }

    private static string Name(Transaction tx, BlockReference reference)
    {
        var id = reference.IsDynamicBlock ? reference.DynamicBlockTableRecord : reference.BlockTableRecord;
        return ((BlockTableRecord)tx.GetObject(id, OpenMode.ForRead)).Name;
    }

    private static void UpdateAttributes(Database db, Transaction tx, Layout layout, ZroFields info, ZroFootprint footprint)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["KAD.GEM"] = info.CadastralMunicipality,
            ["SECTIE.L"] = info.Section,
            ["SECTIE.NR"] = info.ParcelNumber,
            ["EIGENDOMR1"] = info.OwnerName,
            ["EIGENDOMR2"] = info.OwnerAddress,
            ["EIGENDOMR3"] = info.OwnerPostcodePlace,
            ["GEBRUIKERR1"] = info.Users1,
            ["GEBRUIKERR2"] = info.Users2,
            ["GEBRUIKERR3"] = info.Users3,
            ["OPP.IN.M"] = double.TryParse(info.AreaSquareMeters, NumberStyles.Float, Dutch, out var specified)
                ? Math.Round(specified).ToString("0", Dutch)
                : Math.Round(footprint.Area).ToString("0", Dutch),
            ["type.stat"] = footprint.BlockType,
            ["TEKENINGNUMMER:"] = info.DrawingNumber,
            ["EMAILADRES:"] = info.Email,
            ["AFDELING"] = info.Department,
            ["schaal.sit"] = "schaal 1:100",
            ["schaal.over"] = "schaal 1:1000",
            ["OMSCHRIJVING{108}"] = info.Description,
            ["ADRES{108}"] = info.Address,
            ["GEMEENTE{108}"] = info.MunicipalText,
            ["MSR.NR.{55}"] = info.StationNumber,
            ["STATIONSNUMMER"] = info.StationNumber,
            ["GET.{33}"] = info.Draftsman,
            ["DATUM"] = info.Date,
            ["OPMERKING{80}"] = info.Note,
            ["FORMAAT"] = "A4",
            ["AANTALBLADEN"] = "1",
            ["BLADNR"] = "1"
        };
        for (var i = 0; i < footprint.Corners.Length; i++)
        {
            attributes[$"COÖRDINAAT_C{i+1}_X"] = ZroGeometry.Number(footprint.Corners[i].X);
            attributes[$"COÖRDINAAT_C{i+1}_Y"] = ZroGeometry.Number(footprint.Corners[i].Y);
        }
        foreach (var block in PaperEntities(tx, layout).OfType<BlockReference>())
        {
            if (Name(tx, block) is not ("Kader-A4s-MSR" or "Enexis-OH-MSR")) continue;
            foreach (ObjectId id in block.AttributeCollection)
            {
                var attribute = (AttributeReference)tx.GetObject(id, OpenMode.ForWrite);
                if (attributes.TryGetValue(attribute.Tag, out var value))
                    attribute.TextString = value.Length > 255 ? value[..255] : value;
                else if (attribute.Tag.StartsWith("EIGENDOM", StringComparison.OrdinalIgnoreCase))
                    attribute.TextString = "";
            }
        }
        UpdateDepartmentOverlay(db, tx, layout, info.Department);
    }

    private static void UpdateCorners(Transaction tx, Layout layout, Point3d[] corners)
    {
        var attributes = PaperEntities(tx, layout).OfType<BlockReference>()
            .Where(b => Name(tx, b) == "Kader-A4s-MSR")
            .SelectMany(b => b.AttributeCollection.Cast<ObjectId>())
            .Select(id => (AttributeReference)tx.GetObject(id, OpenMode.ForWrite))
            .ToDictionary(x => x.Tag, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < 4; i++)
        {
            var tagX = $"COÖRDINAAT_C{i+1}_X";
            var tagY = $"COÖRDINAAT_C{i+1}_Y";
            if (attributes.TryGetValue(tagX, out var x)) x.TextString = ZroGeometry.Number(corners[i].X);
            if (attributes.TryGetValue(tagY, out var y)) y.TextString = ZroGeometry.Number(corners[i].Y);
        }
    }

    private static void UpdateDepartmentOverlay(Database db, Transaction tx, Layout layout, string department)
    {
        // In de originele Bakel-DXF is "Afdeling : Grondzaken" een statische
        // TEXT in het kadermodel, geen attribuut. Het is dus géén wijzigbaar
        // veld. Maskeer die regel uitsluitend op dit ene blad met MText.
        const string layerName = "TB_ZRO_AFDELING";
        var record = (BlockTableRecord)tx.GetObject(layout.BlockTableRecordId, OpenMode.ForWrite);
        var previous = record.Cast<ObjectId>().Select(id =>
            tx.GetObject(id, OpenMode.ForRead)).OfType<MText>()
            .FirstOrDefault(x => x.Layer == layerName);
        if (previous != null)
        {
            previous.UpgradeOpen();
            previous.Contents = "Afdeling : " + department;
            return;
        }
        var label = new MText
        {
            Contents = "Afdeling : " + department,
            Location = new Point3d(151.1, 73.71, 0),
            Width = 120, TextHeight = 1.8,
            Attachment = AttachmentPoint.MiddleLeft,
            BackgroundFill = true, UseBackgroundColor = true,
            Layer = layerName
        };
        // Zorg dat de overlaylaag bestaat, zodat iedere gebruiker één waarde
        // per blad kan gebruiken zonder het gedeelde blok te wijzigen.
        EnsureLayer(db, tx, layerName);
        record.AppendEntity(label);
        tx.AddNewlyCreatedDBObject(label, true);
    }

    private static ObjectId EnsureLayer(Database db, Transaction tx, string name, short color = 7)
    {
        var table = (LayerTable)tx.GetObject(db.LayerTableId, OpenMode.ForRead);
        if (table.Has(name)) return table[name];
        table.UpgradeOpen();
        var layer = new LayerTableRecord { Name = name };
        layer.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
            Autodesk.AutoCAD.Colors.ColorMethod.ByAci, color);
        var id = table.Add(layer);
        tx.AddNewlyCreatedDBObject(layer, true);
        return id;
    }

    private static void CenterViewports(Transaction tx, Layout layout, Point3d station)
    {
        var viewports = PaperEntities(tx, layout).OfType<Viewport>()
            .Where(vp => vp.Height > 80 && vp.Width > 100 && vp.Width < 220)
            .OrderByDescending(vp => vp.Height).ToArray();
        if (viewports.Length != 2)
            throw new InvalidOperationException("Het ZRO-blad moet twee kaartviewports bevatten (boven 1:100, onder 1:1000).");
        var upper = viewports[0];
        var lower = viewports[1];
        foreach (var (vp, scale) in new[] { (upper, 100), (lower, 1000) })
        {
            vp.UpgradeOpen();
            vp.Locked = false;
            vp.ViewDirection = Vector3d.ZAxis;
            vp.ViewTarget = station;
            vp.ViewCenter = Point2d.Origin;
            vp.TwistAngle = 0;
            vp.ViewHeight = vp.Height * scale / 1000.0;
            vp.GridOn = false; vp.On = true;
            vp.Locked = true;
        }
    }

    private static void WriteMetadata(Database db, Transaction tx, Layout layout, ObjectId stationId, ZroFields info)
    {
        var regApps = (RegAppTable)tx.GetObject(db.RegAppTableId, OpenMode.ForRead);
        if (!regApps.Has(AppName))
        {
            regApps.UpgradeOpen();
            var app = new RegAppTableRecord { Name = AppName };
            regApps.Add(app);
            tx.AddNewlyCreatedDBObject(app, true);
        }
        using var record = new ResultBuffer(new TypedValue(1001, AppName),
            new TypedValue(1000, stationId.Handle.ToString()),
            new TypedValue(1000, info.TrafoName),
            new TypedValue(1000, info.ProjectCode));
        layout.XData = record;
    }

    private static ObjectId AttachedStation(Database db, Layout layout)
    {
        using var values = layout.GetXDataForApplication(AppName);
        if (values?.AsArray() is not { Length: >= 2 } a) return ObjectId.Null;
        var handleString = a[1].Value?.ToString();
        if (handleString == null || !long.TryParse(handleString, NumberStyles.HexNumber,
            CultureInfo.InvariantCulture, out var handle)) return ObjectId.Null;
        try { return db.GetObjectId(false, new Handle(handle), 0); }
        catch { return ObjectId.Null; }
    }

    private static void AddParcelLabels(Database db, Transaction tx, Layout layout,
        Point3d station, IReadOnlyList<ZroParcelLabel> automatic)
    {
        var labels = automatic.ToList();
        // Het Bakel-voorbeeld bevat al blauwe SKG-PERCEELNUMMER-blokken met
        // sectie, nummer en teksthoogte 0,9 m. Teken die niet dubbel.
        var model = (BlockTableRecord)tx.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);
        var existingBlueLabels = false;
        foreach (ObjectId id in model)
        {
            if (tx.GetObject(id, OpenMode.ForRead) is not BlockReference block ||
                !Name(tx, block).Contains("PERCEELNUMMER", StringComparison.OrdinalIgnoreCase) ||
                Math.Abs(block.Position.X - station.X) > 9.5 ||
                Math.Abs(block.Position.Y - station.Y) > 7.025) continue;
            var data = block.AttributeCollection.Cast<ObjectId>()
                .Select(a => (AttributeReference)tx.GetObject(a, OpenMode.ForRead))
                .ToDictionary(a => a.Tag, a => a.TextString, StringComparer.OrdinalIgnoreCase);
            if (data.TryGetValue("SECTIE", out var section) &&
                data.TryGetValue("PERCEELNUMMER", out var number))
            {
                existingBlueLabels = true;
            }
        }
        if (existingBlueLabels || labels.Count == 0) return;
        var name = "TB_ZRO_PERCEEL_" + layout.LayoutName.Replace(' ', '_');
        var layer = EnsureLayer(db, tx, name, 5);
        foreach (var item in labels)
        {
            var label = new DBText
            {
                TextString = item.Text,
                Height = 0.9, LayerId = layer,
                Position = new Point3d(item.Position.X, item.Position.Y, station.Z),
                HorizontalMode = TextHorizontalMode.TextCenter,
                AlignmentPoint = new Point3d(item.Position.X, item.Position.Y, station.Z)
            };
            model.AppendEntity(label);
            tx.AddNewlyCreatedDBObject(label, true);
        }
        var viewports = PaperEntities(tx, layout).OfType<Viewport>()
            .Where(vp => vp.Width > 100 && vp.Width < 220).ToArray();
        var lower = viewports.OrderBy(vp => vp.Height).FirstOrDefault();
        if (lower != null)
        {
            lower.UpgradeOpen();
            lower.FreezeLayersInViewport(new[] { layer }.GetEnumerator());
        }
        // Verberg de blauwe labels van andere ZRO-bladen in deze viewport.
        var layers = (LayerTable)tx.GetObject(db.LayerTableId, OpenMode.ForRead);
        var foreign = layers.Cast<ObjectId>().Select(id => (LayerTableRecord)tx.GetObject(id, OpenMode.ForRead))
            .Where(x => x.Name.StartsWith("TB_ZRO_PERCEEL_", StringComparison.OrdinalIgnoreCase) &&
                x.ObjectId != layer).Select(x => x.ObjectId).ToArray();
        foreach (var vp in viewports)
        {
            if (foreign.Length == 0) continue;
            vp.UpgradeOpen();
            vp.FreezeLayersInViewport(foreign.GetEnumerator());
        }
    }
}
