using System;
using System.IO;
using System.Text.Json;

namespace Techbase.Blocks;

public sealed class Settings
{
    public bool InheritLayer { get; set; } = true;
    public double Tolerance { get; set; } = 0.05;
    public string? PalettePath { get; set; }
    public bool Repeat { get; set; }
    public static string FilePath => Environment.GetEnvironmentVariable("TECHBASE_SETTINGS_FILE") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Techbase", "Blocks", "settings.json");
    public static Settings Read()
    {
        try
        {
            var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
            if (!double.IsFinite(settings.Tolerance) || settings.Tolerance <= 0) settings.Tolerance = 0.05;
            return settings;
        }
        catch (IOException) { return new Settings(); }
        catch (UnauthorizedAccessException) { return new Settings(); }
        catch (JsonException) { return new Settings(); }
    }
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, FilePath, true);
    }
}
