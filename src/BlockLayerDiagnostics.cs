using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Techbase.Blocks;

internal static class BlockLayerDiagnostics
{
    internal static void Run(Catalog catalog, string output)
    {
        var checks = new List<string>();
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException("Blocklaag: " + message); checks.Add(message); }
        using var db = new Database(true, true);
        using var working = new WorkingDatabaseScope(db);
        var tool = catalog.Tools.First(t => t.BlockName == "NIEUWE MOF");
        var definition = BlockInserter.Import(db, catalog, tool);
        var colors = new Dictionary<string, System.Drawing.Color>();
        using (var source = new Database(false, true))
        {
            source.ReadDwgFile(catalog.ResolveSource(catalog.Tools[0]), FileOpenMode.OpenForReadAndAllShare, true, null); source.CloseInput(true);
            using var tx = source.TransactionManager.StartTransaction();
            var table = (LayerTable)tx.GetObject(source.LayerTableId, OpenMode.ForRead);
            for (var number = 0; number <= 12; number++)
                colors.Add(BlockLayers.Name(number), ((LayerTableRecord)tx.GetObject(table[BlockLayers.Name(number)], OpenMode.ForRead)).Color.ColorValue);
        }
        for (var number = 0; number <= 12; number++)
        {
            using var tx = db.TransactionManager.StartTransaction();
            var layer = BlockLayers.Ensure(db, tx, number);
            var record = (LayerTableRecord)tx.GetObject(layer, OpenMode.ForRead);
            Check(record.Name == BlockLayers.Name(number) && record.Color.ColorValue == colors[record.Name], "juiste laag en Techbase-bronkleur: " + record.Name);
            using var block = BlockInserter.CreateReference(db, tx, definition, tool, new Point3d(number * 10, 0, 0), layer, Matrix3d.Identity);
            BlockInserter.Append(db, tx, block, false);
            Check(block.LayerId == layer && block.AttributeCollection.Cast<ObjectId>().Select(id => (AttributeReference)tx.GetObject(id, OpenMode.ForRead)).All(a => a.Layer == "0"),
                "block op gekozen laag; laag-0-attributen erven blijvend van block " + number);
            tx.Commit();
        }
        using (var tx = db.TransactionManager.StartTransaction())
        {
            var layer = (LayerTableRecord)tx.GetObject(BlockLayers.Ensure(db, tx, 10), OpenMode.ForWrite);
            layer.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 3);
            Check(BlockLayers.Ensure(db, tx, 10) == layer.ObjectId && layer.Color.ColorIndex == 3, "bestaande aangepaste laagkleur blijft intact");
            foreach (var state in new[] { "locked", "off", "frozen" })
            {
                layer.IsLocked = state == "locked"; layer.IsOff = state == "off"; layer.IsFrozen = state == "frozen";
                var rejected = false; try { BlockLayers.Ensure(db, tx, 10); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "onbruikbare gekozen laag wordt gemeld: " + state);
            }
            layer.IsLocked = false; layer.IsOff = false; layer.IsFrozen = false; tx.Commit();
        }
        db.SaveAs(Path.Combine(output, "Blocklaag - controle.dwg"), DwgVersion.Current);
        db.DxfOut(Path.Combine(output, "Blocklaag - controle.dxf"), 16, DwgVersion.Current);
        var previousFile = Environment.GetEnvironmentVariable("TECHBASE_SETTINGS_FILE");
        var preferences = Plugin.Preferences;
        var oldUse = preferences.UseChosenBlockLayer; var oldChoice = preferences.ChosenBlockLayer; var oldInherit = preferences.InheritLayer;
        try
        {
            var settingsFile = Path.Combine(output, "block-layer-settings.json");
            Environment.SetEnvironmentVariable("TECHBASE_SETTINGS_FILE", settingsFile);
            File.WriteAllText(settingsFile, "{\"InheritLayer\":true}");
            Check(!Settings.Read().UseChosenBlockLayer && Settings.Read().ChosenBlockLayer == 1, "oude instellingen behouden automatische laagovername");
            File.WriteAllText(settingsFile, "{\"ChosenBlockLayer\":99}");
            Check(Settings.Read().ChosenBlockLayer == 1, "ongeldige opgeslagen laagkeuze hersteld");
            preferences.InheritLayer = true; preferences.UseChosenBlockLayer = false; preferences.ChosenBlockLayer = 1;
            using var panel = new BlocksPanel { Size = new Size(380, 760) };
            panel.CreateControl(); panel.PerformLayout();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var enabled = (CheckBox)typeof(BlocksPanel).GetField("chosenLayer", flags)!.GetValue(panel)!;
            var choice = (ComboBox)typeof(BlocksPanel).GetField("blockLayer", flags)!.GetValue(panel)!;
            var inherit = (CheckBox)typeof(BlocksPanel).GetField("inherit", flags)!.GetValue(panel)!;
            void CreateHandles(Control parent)
            { _ = parent.Handle; foreach (Control child in parent.Controls) CreateHandles(child); }
            CreateHandles(panel);
            Check(choice.Items.Count == 13 && !choice.Enabled && inherit.Enabled && inherit.Checked, "13 laagkeuzes; keuze uit behoudt laagovername");
            enabled.Checked = true; choice.SelectedIndex = 9;
            Check(!inherit.Enabled && inherit.Checked && choice.Enabled && Settings.Read().UseChosenBlockLayer && Settings.Read().ChosenBlockLayer == 10,
                "aanzetten en richting 10 kiezen bewaart de instelling en onthoudt automatische laagovername");
            choice.SelectedIndex = 12;
            Check(Settings.Read().ChosenBlockLayer == 0, "Bestaande kabel slaat de echte kabelkeuze op");
            using (var image = new Bitmap(panel.Width, panel.Height))
            { panel.DrawToBitmap(image, new Rectangle(Point.Empty, panel.Size)); image.Save(Path.Combine(output, "Blocks-laagkeuze.png")); }
            enabled.Checked = false;
            Check(!choice.Enabled && inherit.Enabled && inherit.Checked && !Settings.Read().UseChosenBlockLayer && Settings.Read().ChosenBlockLayer == 0,
                "uitzetten herstelt vorige laagovername en onthoudt de gekozen laag");
            enabled.Checked = true;
            Check(choice.SelectedIndex == 12 && preferences.InheritLayer, "opnieuw aan gebruikt de bewaarde keuze");
            panel.Width = 330; panel.PerformLayout();
            using (var image = new Bitmap(panel.Width, panel.Height))
            { panel.DrawToBitmap(image, new Rectangle(Point.Empty, panel.Size)); image.Save(Path.Combine(output, "Blocks-laagkeuze-smal.png")); }
        }
        finally
        {
            preferences.UseChosenBlockLayer = oldUse; preferences.ChosenBlockLayer = oldChoice; preferences.InheritLayer = oldInherit;
            Environment.SetEnvironmentVariable("TECHBASE_SETTINGS_FILE", previousFile);
        }
        File.WriteAllText(Path.Combine(output, "block-layer-checks.json"), JsonSerializer.Serialize(new { Status = "PASS", Checks = checks }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
