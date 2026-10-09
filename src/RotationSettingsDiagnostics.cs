using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Reflection;
using System.Text.Json;
using System.Windows.Forms;

namespace Techbase.Blocks;

internal static class RotationSettingsDiagnostics
{
    internal static void Run(Catalog catalog, string output)
    {
        var previous = Environment.GetEnvironmentVariable("TECHBASE_SETTINGS_FILE");
        var path = Path.Combine(output, "block-rotation-settings.json");
        var checks = new System.Collections.Generic.List<string>();
        void Check(bool ok, string message)
        { if (!ok) throw new InvalidOperationException(message); checks.Add(message); }
        try
        {
            Environment.SetEnvironmentVariable("TECHBASE_SETTINGS_FILE", path);
            // Actual old-settings migration, save and reload. The user's file is not used.
            File.WriteAllText(path, "{\"InheritLayer\":false,\"Tolerance\":0.123,\"Repeat\":true}");
            var a = catalog.Tools.First(t => t.PromptRotation);
            var b = catalog.Tools.First(t => !t.PromptRotation);
            var settings = Settings.Read();
            Check(settings.GetRotationMode(a) == BlockRotationMode.AfterPoint && settings.GetRotationMode(b) == BlockRotationMode.None,
                "oude instellingen behouden palettegedrag");
            settings.SetRotationMode(a, BlockRotationMode.BeforePoint);
            settings.SetRotationMode(b, BlockRotationMode.AfterPoint);
            settings.Save();
            var reloaded = Settings.Read();
            Check(reloaded.GetRotationMode(a) == BlockRotationMode.BeforePoint && reloaded.GetRotationMode(b) == BlockRotationMode.AfterPoint,
                "afzonderlijke keuzes blijven na herladen bewaard");
            Check(!reloaded.InheritLayer && reloaded.Repeat && Math.Abs(reloaded.Tolerance - 0.123) < 1e-10,
                "overige instellingen behouden");
            reloaded.SetRotationMode(a, BlockRotationMode.None); reloaded.Save();
            reloaded = Settings.Read();
            Check(reloaded.GetRotationMode(a) == BlockRotationMode.None && reloaded.GetRotationMode(b) == BlockRotationMode.AfterPoint,
                "uitzetten geldt alleen voor het gekozen block");
            var otherButton = a with { Id = "another-palette-button" };
            Check(reloaded.GetRotationMode(otherButton) == BlockRotationMode.AfterPoint,
                "palettevarianten met dezelfde blocknaam blijven onafhankelijk");
            reloaded.SetRotationMode(a, BlockRotationMode.AfterPoint); reloaded.Save();
            reloaded = Settings.Read();
            Check(reloaded.GetRotationMode(a) == BlockRotationMode.AfterPoint && !reloaded.BlockRotationModes.ContainsKey(a.Id),
                "terugzetten naar paletstandaard verwijdert alleen eigen uitzondering");
            File.WriteAllText(path, "{\"BlockRotationModes\":null}");
            Check(Settings.Read().GetRotationMode(a) == BlockRotationMode.AfterPoint,
                "lege draai-instellingen zijn bruikbaar");
            var originalModes = Plugin.Preferences.BlockRotationModes;
            Plugin.Preferences.BlockRotationModes = new(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var panel = new BlocksPanel { Size = new Size(380, 700) };
                panel.CreateControl(); panel.PerformLayout();
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var list = (ListView)typeof(BlocksPanel).GetField("list", flags)!.GetValue(panel)!;
                var rotation = (ComboBox)typeof(BlocksPanel).GetField("rotation", flags)!.GetValue(panel)!;
                _ = list.Handle; _ = rotation.Handle;
                Check(!rotation.Enabled, "draai-instelling vraagt eerst een geselecteerd block");
                var item = list.Items.Cast<ListViewItem>().First(i => ((BlockTool)i.Tag!).Id == a.Id);
                item.Selected = true;
                Check(rotation.Enabled && rotation.SelectedIndex == 1, "keuzelijst volgt geselecteerd block");
                rotation.SelectedIndex = 0;
                Check(Settings.Read().GetRotationMode(a) == BlockRotationMode.BeforePoint &&
                    Settings.Read().GetRotationMode(b) == BlockRotationMode.None, "keuzelijst slaat alleen gekozen block op");
                using (var image = new Bitmap(panel.Width, panel.Height))
                {
                    panel.DrawToBitmap(image, new Rectangle(Point.Empty, panel.Size));
                    image.Save(Path.Combine(output, "Blocks-draai-instelling.png"));
                }
                rotation.SelectedIndex = 2;
                Check(Settings.Read().GetRotationMode(a) == BlockRotationMode.None, "keuzelijst schakelt bestaande draaivraag uit");
            }
            finally { Plugin.Preferences.BlockRotationModes = originalModes; Plugin.UpdateSettings(); }
            settings.Save();
            File.WriteAllText(Path.Combine(output, "block-rotation-settings-checks.json"), JsonSerializer.Serialize(
                new { Status = "PASS", Checks = checks }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { Environment.SetEnvironmentVariable("TECHBASE_SETTINGS_FILE", previous); }
    }
}
