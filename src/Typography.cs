using System.Diagnostics;
using System.IO;
using System.Drawing;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.GraphicsInterface;
using Font = System.Drawing.Font;

namespace Techbase.Blocks;

internal static class Typography
{
    internal const string Family = "Arial Nova Light";
    internal const string FileName = "ArialNova-Light.ttf";
    internal const string Revision = "ANL1";
    private const string StyleName = "TECHBASE_ARIAL_NOVA_LIGHT";
    internal static string FontPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts", FileName);

    internal static void EnsureInstalled()
    {
        if (File.Exists(FontPath)) return;
        var script = Path.Combine(Plugin.ContentsRoot, "Lettertype.ps1");
        if (!File.Exists(script)) return;
        Process.Start(new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\"")
        { UseShellExecute = false, CreateNoWindow = true });
    }

    internal static ObjectId Style(Database db, Transaction tx)
    {
        using var working = new WorkingDatabaseScope(db);
        var table = (TextStyleTable)tx.GetObject(db.TextStyleTableId, OpenMode.ForRead);
        // A user-created style with this name must never change other texts.
        for (var i = 0; ; i++)
        {
            var name = StyleName + (i == 0 ? "" : "_" + i);
            if (table.Has(name))
            {
                var existing = (TextStyleTableRecord)tx.GetObject(table[name], OpenMode.ForRead);
                if (Path.GetFileName(existing.FileName).Equals(FileName, StringComparison.OrdinalIgnoreCase) &&
                    existing.Font.TypeFace == Family && !existing.Font.Bold && !existing.Font.Italic &&
                    existing.TextSize == 0 && existing.XScale == 1 && existing.ObliquingAngle == 0)
                    return existing.ObjectId;
                continue;
            }
            table.UpgradeOpen();
            var record = new TextStyleTableRecord { Name = name, FileName = FileName,
                Font = new FontDescriptor(Family, false, false, 0, 34), TextSize = 0, XScale = 1 };
            var id = table.Add(record); tx.AddNewlyCreatedDBObject(record, true); return id;
        }
    }

    internal static string WithoutFontOverrides(string contents)
    {
        // Skip escaped backslashes, so literal text such as \\filename survives.
        return Regex.Replace(contents, @"\\\\|\\[fF][^;]*;", match => match.Value == @"\\" ? match.Value : "");
    }

    internal static void Apply(Entity entity, ObjectId style)
    {
        if (entity is MText mtext)
        {
            mtext.TextStyleId = style; mtext.Contents = WithoutFontOverrides(mtext.Contents);
        }
        else if (entity is DBText text)
        {
            text.TextStyleId = style;
            if (text is AttributeDefinition definition && definition.IsMTextAttributeDefinition)
            {
                using var multiline = definition.MTextAttributeDefinition;
                Apply(multiline, style); definition.MTextAttributeDefinition = multiline;
            }
            else if (text is AttributeReference attribute && attribute.IsMTextAttribute)
            {
                using var multiline = attribute.MTextAttribute;
                Apply(multiline, style); attribute.MTextAttribute = multiline; attribute.UpdateMTextAttribute();
            }
        }
    }

    internal static void NormalizeSource(Database db, Transaction tx, string styleName)
    {
        using var working = new WorkingDatabaseScope(db);
        var styles = (TextStyleTable)tx.GetObject(db.TextStyleTableId, OpenMode.ForRead);
        if (styles.Has(styleName))
        {
            var old = (TextStyleTableRecord)tx.GetObject(styles[styleName], OpenMode.ForWrite);
            old.Name = "TB_FONT_IMPORT_" + Guid.NewGuid().ToString("N");
        }
        styles.UpgradeOpen();
        var record = new TextStyleTableRecord { Name = styleName, FileName = FileName,
            Font = new FontDescriptor(Family, false, false, 0, 34), TextSize = 0, XScale = 1 };
        var style = styles.Add(record); tx.AddNewlyCreatedDBObject(record, true);
        var table = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
        foreach (ObjectId blockId in table)
        {
            var block = (BlockTableRecord)tx.GetObject(blockId, OpenMode.ForRead);
            if (block.IsFromExternalReference || block.IsDependent) continue;
            foreach (ObjectId id in block)
            {
                if (tx.GetObject(id, OpenMode.ForRead) is not Entity entity || entity is not (DBText or MText)) continue;
                entity.UpgradeOpen(); Apply(entity, style);
                if (entity is DBText text) text.AdjustAlignment(db);
            }
        }
    }
}

internal static class UiTypography
{
    private static readonly Dictionary<(float, GraphicsUnit), Font> Fonts = new();
    private static Font Light(Font original)
    {
        var key = (original.Size, original.Unit);
        if (!Fonts.TryGetValue(key, out var font)) Fonts[key] = font = new Font(Typography.Family, original.Size, FontStyle.Regular, original.Unit);
        return font;
    }
    internal static void Apply(Control control)
    {
        void SetFont()
        {
            var font = Light(control.Font);
            if (!control.Font.Equals(font)) control.Font = font;
        }
        SetFont();
        control.FontChanged += (_, _) => SetFont();
        if (control is DataGridView grid)
        {
            foreach (var style in new[] { grid.DefaultCellStyle, grid.AlternatingRowsDefaultCellStyle,
                grid.RowsDefaultCellStyle, grid.ColumnHeadersDefaultCellStyle, grid.RowHeadersDefaultCellStyle })
                style.Font = Light(style.Font ?? control.Font);
        }
        foreach (Control child in control.Controls) Apply(child);
        control.ControlAdded += (_, args) => { if (args.Control != null) Apply(args.Control); };
    }
}
