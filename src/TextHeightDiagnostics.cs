using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Techbase.Blocks;

internal static class TextHeightDiagnostics
{
    internal static void Run(Catalog catalog, string output)
    {
        var checks = new List<string>();
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException("Teksthoogte: " + message); checks.Add(message); }
        var settings = new Settings();
        Check(settings.MofTextHeight == 0.45 && settings.DirectionTextHeight == 0.45, "nieuwe instellingen standaard beide 0,45");
        var targets = catalog.Tools.Where(t => settings.BlockTextHeight(t.BlockName).HasValue).ToArray();
        Check(targets.Count(t => t.BlockName.Contains("MOF", StringComparison.OrdinalIgnoreCase)) == 3 && targets.Count(t => t.BlockName.StartsWith("RICHTINGEN TEKST")) == 2,
            "drie MOF-blocks en LS/OV-teksten vallen onder de instelling");
        Check(settings.BlockTextHeight("12 RICHTINGEN LS REK") == null, "afzekeringsblocks houden hun oorspronkelijke teksthoogtes");
        using var db = new Database(true, true); using var working = new WorkingDatabaseScope(db);
        var row = 0;
        foreach (var tool in targets)
        {
            var versions = new List<ObjectId>();
            foreach (var height in new[] { 0.75, 0.45 })
            {
                settings.MofTextHeight = height; settings.DirectionTextHeight = height;
                var id = BlockInserter.Import(db, catalog, tool, settings); versions.Add(id);
                Check(BlockInserter.Import(db, catalog, tool, settings) == id, tool.Name + ": gelijke hoogte hergebruikt eigen definitie " + height);
                using var tx = db.TransactionManager.StartTransaction();
                var record = (BlockTableRecord)tx.GetObject(id, OpenMode.ForRead);
                var texts = record.Cast<ObjectId>().Select(x => tx.GetObject(x, OpenMode.ForRead)).Where(x => x is DBText or MText).ToArray();
                Check(texts.Length > 0 && texts.All(x => Math.Abs(x is MText m ? m.TextHeight - height : ((DBText)x).Height - height) < 1e-9),
                    tool.Name + ": alle tekst-/attribuutdefinities hebben hoogte " + height);
                using var block = BlockInserter.CreateReference(db, tx, id, tool, new Point3d(row++ * 20, 0, 0), db.Clayer, Matrix3d.Identity);
                using var jig = new PlacementJig(db, tx, block, tool.Rotation);
                jig.SetAngle(Math.PI / 2);
                Check(jig.Attributes.All(a => Math.Abs(a.Height - height) < 1e-9), tool.Name + ": gedraaid attribuutvoorbeeld hoogte " + height);
                var isText = tool.BlockName.StartsWith("RICHTINGEN TEKST");
                var inserted = BlockInserter.Append(db, tx, block, false, burst: isText);
                if (isText)
                    Check(inserted.Count == 13 && inserted.All(x => tx.GetObject(x, OpenMode.ForRead) is MText m && Math.Abs(m.TextHeight - height) < 1e-9 && Math.Abs(m.Width - settings.DirectionWidth(height, ExistingCableColors.Matches(m.Layer))) < 1e-9), tool.Name + ": 13 losse burstteksten op gekozen hoogte " + height);
                else
                    Check(block.AttributeCollection.Cast<ObjectId>().All(x => Math.Abs(((AttributeReference)tx.GetObject(x, OpenMode.ForRead)).Height - height) < 1e-9), tool.Name + ": geplaatste attributen hoogte " + height);
                tx.Commit();
            }
            using (var tx = db.TransactionManager.StartTransaction())
            {
                Check(versions[0] != versions[1], tool.Name + ": hoogtevariant krijgt eigen definitie");
                var record = (BlockTableRecord)tx.GetObject(versions[0], OpenMode.ForRead);
                Check(record.Cast<ObjectId>().Select(x => tx.GetObject(x, OpenMode.ForRead)).All(x => x is not DBText t || Math.Abs(t.Height - 0.75) < 1e-9),
                    tool.Name + ": oude 0,75-definitie blijft intact na kiezen 0,45");
            }
        }
        db.SaveAs(Path.Combine(output, "Teksthoogtes - controle.dwg"), DwgVersion.Current);
        db.DxfOut(Path.Combine(output, "Teksthoogtes - controle.dxf"), 16, DwgVersion.Current);
        var oldFile = Environment.GetEnvironmentVariable("TECHBASE_SETTINGS_FILE");
        var oldMof = Plugin.Preferences.MofTextHeight; var oldDirection = Plugin.Preferences.DirectionTextHeight;
        try
        {
            var file = Path.Combine(output, "text-height-settings.json"); Environment.SetEnvironmentVariable("TECHBASE_SETTINGS_FILE", file);
            File.WriteAllText(file, "{\"InheritLayer\":false}");
            Check(Settings.Read().MofTextHeight == 0.45 && Settings.Read().DirectionTextHeight == 0.45 && !Settings.Read().InheritLayer, "oude instellingen krijgen 0,45 en behouden bestaande keuzes");
            File.WriteAllText(file, "{\"MofTextHeight\":-1,\"DirectionTextHeight\":99}");
            Check(Settings.Read().MofTextHeight == 0.45 && Settings.Read().DirectionTextHeight == 0.45, "ongeldige opgeslagen hoogtes worden 0,45");
            Plugin.Preferences.MofTextHeight = 0.45; Plugin.Preferences.DirectionTextHeight = 0.45;
            using var panel = new SettingsPanel { Size = new Size(380, 520) }; panel.CreateControl();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var mof = (ComboBox)typeof(SettingsPanel).GetField("mof", flags)!.GetValue(panel)!;
            var directions = (ComboBox)typeof(SettingsPanel).GetField("directions", flags)!.GetValue(panel)!;
            Check(mof.Items.Count == 2 && directions.Items.Count == 2 && mof.SelectedIndex == 0 && directions.SelectedIndex == 0, "instellingentab toont twee keuzes met 0,45 standaard");
            mof.SelectedIndex = 1;
            Check(Settings.Read().MofTextHeight == 0.75 && Settings.Read().DirectionTextHeight == 0.45, "MOF-keuze wordt afzonderlijk opgeslagen");
            directions.SelectedIndex = 1;
            Check(Settings.Read().MofTextHeight == 0.75 && Settings.Read().DirectionTextHeight == 0.75, "richtingstekstkeuze wordt opgeslagen");
            using var reloaded = new SettingsPanel();
            Check(((ComboBox)typeof(SettingsPanel).GetField("mof", flags)!.GetValue(reloaded)!).SelectedIndex == 1, "nieuwe instellingentab gebruikt de gekozen hoogte");
            using var image = new Bitmap(panel.Width, panel.Height); panel.DrawToBitmap(image, new Rectangle(Point.Empty, panel.Size)); image.Save(Path.Combine(output, "Instellingen-tab.png"));
        }
        finally { Plugin.Preferences.MofTextHeight = oldMof; Plugin.Preferences.DirectionTextHeight = oldDirection; Environment.SetEnvironmentVariable("TECHBASE_SETTINGS_FILE", oldFile); }
        File.WriteAllText(Path.Combine(output, "teksthoogtes.json"), JsonSerializer.Serialize(new { Status = "PASS", Checks = checks }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
