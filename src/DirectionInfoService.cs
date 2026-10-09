using System.Globalization;
using Enexis.KabelChecker.AutoCAD;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Color = Autodesk.AutoCAD.Colors.Color;

namespace Techbase.Blocks;

internal sealed record DirectionInfoRequest(Autodesk.AutoCAD.ApplicationServices.Document Document,
    DirectionState Direction, KaderVersion Version, DesignCurrentMode Mode);
internal sealed record DirectionInfoLabels(string Current, string Length);

internal static class DirectionInfoService
{
    internal const double TextHeight = 1;
    internal const double Spacing = 1.1150990076130256;
    private static readonly CultureInfo Dutch = CultureInfo.GetCultureInfo("nl-NL");
    internal static DirectionInfoLabels Labels(DirectionState direction, KaderVersion version, DesignCurrentMode mode)
    {
        if (direction.Number is < 1 or > 12 || direction.Segments.Count == 0) throw new InvalidOperationException("Kies een geldige richting met kabelsegmenten.");
        if (!DesignCurrentCalculator.MatchesInputs(direction.CurrentLoads, direction.ExcelLoads)) throw new InvalidOperationException("De stroominvoer is niet volledig aan kadercategorieën gekoppeld. Bereken en sla de richting eerst op.");
        var amps = DesignCurrentCalculator.Calculate(version, direction.ExcelLoads).CableCurrent(mode);
        var length = direction.Segments.Sum(s => s.LengthMeters);
        if (!double.IsFinite(amps) || amps < 0 || !double.IsFinite(length) || length <= 0) throw new InvalidOperationException("De richting heeft geen geldige stroom of rekenlengte.");
        return new DirectionInfoLabels(amps.ToString("0.##", Dutch) + "Amp.", length.ToString("0.00", Dutch) + "Met.");
    }
    internal static ObjectId ValidateLayer(Database db, int number)
    {
        using var tx = db.TransactionManager.StartTransaction();
        var name = $"Aansluiting LS K{number:00}";
        var layers = (LayerTable)tx.GetObject(db.LayerTableId, OpenMode.ForRead);
        if (!layers.Has(name)) throw new InvalidOperationException($"Laag '{name}' ontbreekt. De richting-info gebruikt de bestaande richtingslaag.");
        return LayerResolver.CheckLayer(tx, layers[name]).LayerId;
    }
    internal static string Place(DirectionInfoRequest request)
    {
        var db = request.Document.Database;
        var editor = request.Document.Editor;
        var labels = Labels(request.Direction, request.Version, request.Mode);
        ValidateLayer(db, request.Direction.Number);
        var ucs = editor.CurrentUserCoordinateSystem;
        var point = editor.GetPoint($"\nPlaats de bovenste richting-info tekst K{request.Direction.Number:00}: ");
        if (point.Status != PromptStatus.OK) return "Plaatsen geannuleerd; geen teksten toegevoegd.";
        Append(db, labels, request.Direction.Number, point.Value, ucs);
        editor.Regen();
        return $"Richting {request.Direction.Number}: {labels.Current} en {labels.Length} geplaatst op Aansluiting LS K{request.Direction.Number:00}.";
    }
    internal static IReadOnlyList<ObjectId> Append(Database db, DirectionInfoLabels labels, int number, Point3d ucsPoint, Matrix3d ucs)
    {
        using var working = new WorkingDatabaseScope(db);
        var layer = ValidateLayer(db, number);
        using var tx = db.TransactionManager.StartTransaction();
        var style = Typography.Style(db, tx);
        var space = (BlockTableRecord)tx.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
        var ids = new List<ObjectId>();
        var texts = new[] { labels.Current, labels.Length };
        for (var i = 0; i < texts.Length; i++)
        {
            var center = new Point3d(ucsPoint.X, ucsPoint.Y - i * Spacing, ucsPoint.Z);
            using var text = new DBText();
            text.SetDatabaseDefaults(db); text.Height = TextHeight; text.TextString = texts[i]; text.TextStyleId = style;
            text.HorizontalMode = TextHorizontalMode.TextCenter; text.VerticalMode = TextVerticalMode.TextVerticalMid;
            text.Position = center; text.AlignmentPoint = center;
            text.LayerId = layer; text.Color = Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByLayer, 256);
            text.TransformBy(ucs);
            ids.Add(space.AppendEntity(text)); tx.AddNewlyCreatedDBObject(text, true); text.AdjustAlignment(db);
        }
        tx.Commit();
        return ids;
    }
}
