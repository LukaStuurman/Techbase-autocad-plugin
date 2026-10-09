using System.Globalization;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.DatabaseServices;

namespace Techbase.Blocks;

internal static class BlockTextHeight
{
    internal static string Revision(double? height) => height.HasValue ? "_TEXT1_H" + height.Value.ToString("0.00", CultureInfo.InvariantCulture).Replace(".", "") : "";
    internal static void Apply(Database db, Transaction tx, ObjectId root, double height)
    {
        using var working = new WorkingDatabaseScope(db);
        var visited = new HashSet<ObjectId>();
        string Contents(string value) => Regex.Replace(value, @"\\\\|\\H[^;]*;", match => match.Value == @"\\" ? match.Value : "");
        void Visit(ObjectId blockId)
        {
            if (!visited.Add(blockId)) return;
            var record = (BlockTableRecord)tx.GetObject(blockId, OpenMode.ForRead);
            if (record.IsFromExternalReference || record.IsDependent) return;
            foreach (ObjectId id in record)
            {
                var entity = tx.GetObject(id, OpenMode.ForRead);
                if (entity is BlockReference reference) { Visit(reference.BlockTableRecord); continue; }
                if (entity is MText text)
                { text.UpgradeOpen(); text.TextHeight = height; text.Contents = Contents(text.Contents); }
                else if (entity is DBText single)
                {
                    single.UpgradeOpen(); single.Height = height;
                    if (single is AttributeDefinition definition && definition.IsMTextAttributeDefinition)
                    {
                        using var multiline = definition.MTextAttributeDefinition;
                        multiline.TextHeight = height; multiline.Contents = Contents(multiline.Contents);
                        definition.MTextAttributeDefinition = multiline;
                    }
                    single.AdjustAlignment(db);
                }
            }
            if (record.IsDynamicBlock) foreach (ObjectId anonymous in record.GetAnonymousBlockIds()) Visit(anonymous);
        }
        Visit(root);
    }
}
