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
    public double DirectionTextWidth045 { get; set; } = 4.80;
    public double DirectionTextWidth075 { get; set; } = 7.70;
    public double ExistingDirectionTextWidth045 { get; set; } = 7.80;
    public double ExistingDirectionTextWidth075 { get; set; } = 10.70;
    public bool DirectionTextMask { get; set; } = true;
    public double DirectionTextMaskBorder { get; set; } = 1.30;
    public bool DirectionTextMaskDrawingColor { get; set; } = true;
    public int DirectionTextMaskRgb { get; set; } = 0xFFFFFF;
    public double DirectionWidth(double height, bool existing) => height == .75
        ? existing ? ExistingDirectionTextWidth075 : DirectionTextWidth075
        : existing ? ExistingDirectionTextWidth045 : DirectionTextWidth045;
    public bool ExistingCableBlack { get; set; }
    public Dictionary<string, KabelgeulRow> KabelgeulDefaults { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public int KabelgeulTextFormat { get; set; }
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
            double Width(double value, double fallback) => double.IsFinite(value) && value >= .01 && value <= 10000 ? value : fallback;
            settings.DirectionTextWidth045 = Width(settings.DirectionTextWidth045, 4.80);
            settings.DirectionTextWidth075 = Width(settings.DirectionTextWidth075, 7.70);
            settings.ExistingDirectionTextWidth045 = Width(settings.ExistingDirectionTextWidth045, 7.80);
            settings.ExistingDirectionTextWidth075 = Width(settings.ExistingDirectionTextWidth075, 10.70);
            if (!double.IsFinite(settings.DirectionTextMaskBorder) || settings.DirectionTextMaskBorder < 1 || settings.DirectionTextMaskBorder > 5) settings.DirectionTextMaskBorder = 1.30;
            if (settings.DirectionTextMaskRgb is < 0 or > 0xFFFFFF) settings.DirectionTextMaskRgb = 0xFFFFFF;
            settings.BlockRotationModes = new Dictionary<string, BlockRotationMode>(
                settings.BlockRotationModes ?? new(), StringComparer.OrdinalIgnoreCase);
            settings.KabelgeulDefaults = new Dictionary<string, KabelgeulRow>(settings.KabelgeulDefaults ?? new(), StringComparer.OrdinalIgnoreCase);
            settings.KabelgeulMantelSpec ??= "";
            KabelgeulData.MigrateText(settings);
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
