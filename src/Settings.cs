using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Techbase.Blocks;

[JsonConverter(typeof(JsonStringEnumConverter<BlockRotationMode>))]
public enum BlockRotationMode { None, BeforePoint, AfterPoint }

public sealed class Settings
{
    public bool InheritLayer { get; set; } = true;
    public bool UseChosenBlockLayer { get; set; }
    public int ChosenBlockLayer { get; set; } = 1;
    public double Tolerance { get; set; } = 0.05;
    public string? PalettePath { get; set; }
    public bool Repeat { get; set; }
    public int SelectedDirectionNumber { get; set; } = 1;
    public double MofTextHeight { get; set; } = 0.45;
    public double DirectionTextHeight { get; set; } = 0.45;
    public bool ExistingCableBlack { get; set; }
    public Dictionary<string, KabelgeulRow> KabelgeulDefaults { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string KabelgeulMantelSpec { get; set; } = "";
    public double? BlockTextHeight(string name) => name.Contains("MOF", StringComparison.OrdinalIgnoreCase) ? MofTextHeight :
        name.StartsWith("RICHTINGEN TEKST", StringComparison.OrdinalIgnoreCase) ? DirectionTextHeight : null;
    public Dictionary<string, BlockRotationMode> BlockRotationModes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public BlockRotationMode GetRotationMode(BlockTool tool) =>
        BlockRotationModes.TryGetValue(tool.Id, out var mode) && Enum.IsDefined(mode)
            ? mode : tool.PromptRotation ? BlockRotationMode.AfterPoint : BlockRotationMode.None;
    public void SetRotationMode(BlockTool tool, BlockRotationMode mode)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        var original = tool.PromptRotation ? BlockRotationMode.AfterPoint : BlockRotationMode.None;
        if (mode == original) BlockRotationModes.Remove(tool.Id);
        else BlockRotationModes[tool.Id] = mode;
    }
    public static string FilePath => Environment.GetEnvironmentVariable("TECHBASE_SETTINGS_FILE") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Techbase", "Blocks", "settings.json");
    public static Settings Read()
    {
        try
        {
            var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
            if (!double.IsFinite(settings.Tolerance) || settings.Tolerance <= 0) settings.Tolerance = 0.05;
            if (settings.SelectedDirectionNumber is < 1 or > 12) settings.SelectedDirectionNumber = 1;
            if (settings.ChosenBlockLayer is < 0 or > 12) settings.ChosenBlockLayer = 1;
            if (settings.MofTextHeight is not (0.75 or 0.45)) settings.MofTextHeight = 0.45;
            if (settings.DirectionTextHeight is not (0.75 or 0.45)) settings.DirectionTextHeight = 0.45;
            settings.BlockRotationModes = new Dictionary<string, BlockRotationMode>(
                settings.BlockRotationModes ?? new(), StringComparer.OrdinalIgnoreCase);
            settings.KabelgeulDefaults = new Dictionary<string, KabelgeulRow>(settings.KabelgeulDefaults ?? new(), StringComparer.OrdinalIgnoreCase);
            settings.KabelgeulMantelSpec ??= "";
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
