using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;

namespace Techbase.Blocks;

internal static class DirectionTextAppearance
{
    internal static string Revision(Settings settings, double height)
    {
        var values = System.Text.Json.JsonSerializer.Serialize(new[] { settings.DirectionWidth(height, false).ToString("R", CultureInfo.InvariantCulture),
            settings.DirectionWidth(height, true).ToString("R", CultureInfo.InvariantCulture), settings.DirectionTextMask.ToString(),
            settings.DirectionTextMaskBorder.ToString("R", CultureInfo.InvariantCulture), settings.DirectionTextMaskDrawingColor.ToString(), settings.DirectionTextMaskRgb.ToString(CultureInfo.InvariantCulture),
            settings.DirectionLsText, settings.DirectionExistingLsText, settings.DirectionOvText, settings.DirectionExistingOvText });
        return "_AP1_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(values)))[..16];
    }
    internal static void Apply(MText text, bool existing, double height, Settings settings)
    {
        if (text.ColumnType != ColumnType.NoColumns) text.ColumnType = ColumnType.NoColumns;
        text.Width = settings.DirectionWidth(height, existing);
        text.BackgroundScaleFactor = settings.DirectionTextMaskBorder;
        var rgb = settings.DirectionTextMaskRgb;
        text.BackgroundFillColor = Autodesk.AutoCAD.Colors.Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        text.UseBackgroundColor = settings.DirectionTextMaskDrawingColor;
        // Setting the fill color can enable the mask in AutoCAD; apply the toggle last.
        text.BackgroundFill = settings.DirectionTextMask;
    }
    internal static void ApplySource(Database db, Transaction tx, ObjectId root, double height, Settings settings)
    {
        var seen = new HashSet<ObjectId>();
        void Visit(ObjectId id)
        {
            if (!seen.Add(id)) return;
            var record = (BlockTableRecord)tx.GetObject(id, OpenMode.ForRead);
            if (record.IsDependent || record.IsFromExternalReference) return;
            foreach (ObjectId child in record)
            {
                var item = tx.GetObject(child, OpenMode.ForRead);
                if (item is MText text) { text.UpgradeOpen(); Apply(text, ExistingCableColors.Matches(text.Layer), height, settings); }
                else if (item is BlockReference block) Visit(block.BlockTableRecord);
            }
        }
        Visit(root);
    }
}
