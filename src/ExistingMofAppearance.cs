using System.Text.RegularExpressions;
using Autodesk.AutoCAD.DatabaseServices;
using Color = Autodesk.AutoCAD.Colors.Color;

namespace Techbase.Blocks;

internal static class ExistingMofAppearance
{
    internal static string Revision(string original) => original.Equals("BESTAANDE MOF", StringComparison.OrdinalIgnoreCase) ? "_BLACK_CONT7_1" : "";
    internal static void Apply(Database source, Transaction tx)
    {
        var table = (BlockTable)tx.GetObject(source.BlockTableId, OpenMode.ForRead);
        if (!table.Has("BESTAANDE MOF")) return;
        var block = (BlockTableRecord)tx.GetObject(table["BESTAANDE MOF"], OpenMode.ForRead);
        var continuous = ((LinetypeTable)tx.GetObject(source.LinetypeTableId, OpenMode.ForRead))["Continuous"];
        foreach (ObjectId id in block)
        {
            if (tx.GetObject(id, OpenMode.ForRead) is not Entity entity || entity is not (Circle or DBText or MText)) continue;
            entity.UpgradeOpen(); entity.Color = Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 7);
            if (entity is Circle) { entity.LinetypeId = continuous; entity.LinetypeScale = 1; }
            if (entity is MText text) text.Contents = WithoutColorOverrides(text.Contents);
            else if (entity is AttributeDefinition definition && definition.IsMTextAttributeDefinition)
            {
                using var multiline = definition.MTextAttributeDefinition;
                multiline.Color = Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 7); multiline.Contents = WithoutColorOverrides(multiline.Contents);
                definition.MTextAttributeDefinition = multiline;
            }
        }
    }
    private static string WithoutColorOverrides(string contents) => Regex.Replace(contents,
        @"\\\\|\\[cC][^;]*;", match => match.Value == @"\\" ? match.Value : "");
}
