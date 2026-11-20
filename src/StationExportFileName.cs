using System.IO;
using System.Text.RegularExpressions;

namespace Techbase.Blocks;

internal static class StationExportFileName
{
    internal static string Create(string? station)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var name = new string((station ?? "").Trim().Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c).ToArray()).TrimEnd(' ', '.');
        if (name.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)) name = name[..^5].TrimEnd(' ', '.');
        if (string.IsNullOrWhiteSpace(name)) name = "Station";
        if (Regex.IsMatch(name, @"^(CON|PRN|AUX|NUL|COM[1-9¹²³]|LPT[1-9¹²³])(?:\.|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) name = "Station_" + name;
        if (name.Length > 120)
        {
            name = name[..120];
            if (char.IsHighSurrogate(name[^1])) name = name[..^1];
            name = name.TrimEnd(' ', '.');
        }
        return name + ".xlsx";
    }
}
