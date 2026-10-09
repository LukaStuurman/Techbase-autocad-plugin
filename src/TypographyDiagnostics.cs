using System.IO;
using System.Text.Json;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Techbase.Blocks;

internal static class TypographyDiagnostics
{
    internal static void Run(Catalog catalog, string output)
    {
        var checks = new List<string>();
        void Check(bool ok, string name) { if (!ok) throw new InvalidOperationException("Lettertype: " + name); checks.Add(name); }
        Check(File.Exists(Typography.FontPath), "Arial Nova Light geinstalleerd in Windows gebruikersfonts");
        using var fonts = new System.Drawing.Text.InstalledFontCollection();
        Check(fonts.Families.Any(f => f.Name == Typography.Family), "Windows herkent de echte Light-lettertypefamilie");
        Check(Typography.WithoutFontOverrides(@"{\fArial|b1|i1;Ri 1}\P{\C1;\H2x;150Al} \\filename") ==
            @"{Ri 1}\P{\C1;\H2x;150Al} \\filename", "inline lettertype/gewicht verwijderd; inhoud, kleur, hoogte, alineas en letterlijke backslash behouden");
        using var db = new Database(true, true);
        using var working = new WorkingDatabaseScope(db);
        ObjectId protectedStyle;
        using (var tx = db.TransactionManager.StartTransaction())
        {
            var styles = (TextStyleTable)tx.GetObject(db.TextStyleTableId, OpenMode.ForWrite);
            var record = new TextStyleTableRecord { Name = "TECHBASE_ARIAL_NOVA_LIGHT", FileName = "arial.ttf" };
            protectedStyle = styles.Add(record); tx.AddNewlyCreatedDBObject(record, true); tx.Commit();
        }
        foreach (var tool in catalog.Tools) BlockInserter.Import(db, catalog, tool);
        using (var tx = db.TransactionManager.StartTransaction())
        {
            var old = (TextStyleTableRecord)tx.GetObject(protectedStyle, OpenMode.ForRead);
            Check(old.FileName == "arial.ttf", "bestaande gelijknamige gebruikersstijl blijft intact");
            var table = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
            var count = 0;
            foreach (ObjectId id in table)
            {
                var record = (BlockTableRecord)tx.GetObject(id, OpenMode.ForRead);
                if (record.IsLayout || record.IsDependent) continue;
                foreach (ObjectId entityId in record)
                {
                    var entity = tx.GetObject(entityId, OpenMode.ForRead);
                    var style = entity is DBText t ? t.TextStyleId : entity is MText m ? m.TextStyleId : ObjectId.Null;
                    if (style.IsNull) continue;
                    var face = (TextStyleTableRecord)tx.GetObject(style, OpenMode.ForRead);
                    Check(Path.GetFileName(face.FileName).Equals(Typography.FileName, StringComparison.OrdinalIgnoreCase) &&
                        face.Font.TypeFace == Typography.Family && !face.Font.Bold && !face.Font.Italic, "blocktekst " + (++count) + " gebruikt Arial Nova Light");
                    if (entity is MText text) Check(text.Contents == Typography.WithoutFontOverrides(text.Contents), "geen inline fontoverride in blocktekst " + count);
                }
            }
            Check(count > 100, "tekst en attributen in alle bronblocks, inclusief geneste/dynamische definities, gecontroleerd");
        }
        ObjectId original;
        using (var tx = db.TransactionManager.StartTransaction())
        {
            var space = (BlockTableRecord)tx.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
            var text = new MText { Contents = @"{\fArial|b1|i1;Test}\P{\C2;150Al}", TextHeight = 1,
                TextStyleId = protectedStyle, Location = new Point3d(30, 30, 0) };
            original = space.AppendEntity(text); tx.AddNewlyCreatedDBObject(text, true); tx.Commit();
        }
        var copy = DirectionText.Apply(db, new[] { original }, true, Point3d.Origin, new Point3d(10, 0, 0), Matrix3d.Identity)[0];
        using (var tx = db.TransactionManager.StartTransaction())
        {
            var source = (MText)tx.GetObject(original, OpenMode.ForRead); var text = (MText)tx.GetObject(copy, OpenMode.ForRead);
            Check(source.TextStyleId == protectedStyle && source.Contents.Contains(@"\fArial"), "kopieren beschermt de bronstijl en fontoverride");
            Check(text.Contents == @"{Test}\P{\C2;150Al}" && text.TextStyleId != protectedStyle, "gekopieerde richtingstekst gebruikt Light zonder inhoud/kleur te verliezen");
        }
        DirectionText.Apply(db, new[] { original }, false, Point3d.Origin, new Point3d(10, 0, 0), Matrix3d.Identity);
        using (var tx = db.TransactionManager.StartTransaction())
            Check(((MText)tx.GetObject(original, OpenMode.ForRead)).TextStyleId != protectedStyle, "verplaatste richtingstekst krijgt eveneens Light");
        foreach (var panel in new Control[] { new BlocksPanel(), new LightmastPanel(), new DirectionPanel(), new KabelCheckerPanel(), new SettingsPanel() })
        {
            using (panel)
            {
                IEnumerable<Control> Descendants(Control root)
                { yield return root; foreach (Control child in root.Controls) foreach (var descendant in Descendants(child)) yield return descendant; }
                var controls = Descendants(panel).ToArray();
                Check(controls.All(c => c.Font.FontFamily.Name == Typography.Family && c.Font.Style == System.Drawing.FontStyle.Regular),
                    panel.GetType().Name + ": alle " + controls.Length + " controles gebruiken de echte Light-familie");
                Check(controls.OfType<DataGridView>().All(g => g.DefaultCellStyle.Font.Name == Typography.Family &&
                    g.ColumnHeadersDefaultCellStyle.Font.Style == System.Drawing.FontStyle.Regular), "tabellen en kolomkoppen gebruiken Light");
                using var button = new Button { Text = "Later toegevoegd", Font = new System.Drawing.Font("Arial", 11, System.Drawing.FontStyle.Bold) };
                panel.Controls.Add(button);
                Check(button.Font.Name == Typography.Family && button.Font.Size == 11 && button.Font.Style == System.Drawing.FontStyle.Regular,
                    "later toegevoegde controls behouden grootte en krijgen Light");
            }
        }
        db.SaveAs(Path.Combine(output, "Lettertype - controle.dwg"), DwgVersion.Current);
        db.DxfOut(Path.Combine(output, "Lettertype - controle.dxf"), 16, DwgVersion.Current);
        File.WriteAllText(Path.Combine(output, "lettertype.json"), JsonSerializer.Serialize(new { Status = "PASS", Checks = checks }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
