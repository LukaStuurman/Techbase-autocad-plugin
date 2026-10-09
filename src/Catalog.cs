using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace Techbase.Blocks;

public sealed record BlockTool(string Id, string Name, string Group, string BlockName,
    string SourceFile, string Layer, double Scale, double Rotation, bool PromptRotation,
    bool Explode, string? IconPath);

public sealed class Catalog
{
    public string PalettePath { get; }
    public IReadOnlyList<BlockTool> Tools { get; }
    public Catalog(string path)
    {
        PalettePath = Path.GetFullPath(path);
        var options = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var reader = XmlReader.Create(path, options);
        var xml = XDocument.Load(reader);
        var root = Path.GetDirectoryName(PalettePath)!;
        var dependent = (string?)xml.Root?.Element("DependentFilesPath") ?? "";
        var result = new List<BlockTool>();
        foreach (var palette in xml.Descendants("Palette"))
        {
            var group = "Algemeen";
            // Direct children only: nested property records can also be called Tool.
            foreach (var item in palette.Element("Tools")?.Elements("Tool") ?? Enumerable.Empty<XElement>())
            {
                var name = (string?)item.Element("Properties")?.Element("ItemName") ?? "";
                if ((string?)item.Element("ToolType") == "3")
                {
                    if (!name.StartsWith("Voeg de", StringComparison.OrdinalIgnoreCase)) group = name;
                    continue;
                }
                var block = item.Element("Data")?.Element("Block");
                if (block == null) continue; // Command macros are data, never executed.
                var blockName = (string?)block.Element("BlockName") ?? "";
                var source = (string?)block.Element("SourceFile") ?? "";
                if (blockName.Length == 0 || source.Length == 0) throw new InvalidDataException("Block zonder naam of bron: " + name);
                var rawId = (string?)item.Element("ItemID")?.Attribute("idValue");
                if (!Guid.TryParse(rawId, out var id)) throw new InvalidDataException("Ongeldige tool-ID: " + name);
                var prop = item.Element("Data")?.Element("GeneralProperties")?.Element("Layer")?.Element("BasicPropInfo")?.Element("PropValue");
                var layer = (string?)prop?.Attribute("unspecified") == "FALSE" ? (string?)prop.Attribute("value") ?? "" : "";
                var image = item.Element("Properties")?.Element("Images")?.Elements("Image")
                    .OrderByDescending(i => (string?)i.Attribute("IsDark") == "false")
                    .ThenByDescending(i => (int?)i.Attribute("cx") ?? 0).FirstOrDefault();
                string? icon = null;
                if (image != null)
                {
                    var relative = (string?)image.Attribute("src") ?? "";
                    var candidate = Path.GetFullPath(Path.Combine(root, dependent, relative));
                    var allowed = Path.GetFullPath(Path.Combine(root, dependent)) + Path.DirectorySeparatorChar;
                    if (candidate.StartsWith(allowed, StringComparison.OrdinalIgnoreCase) && File.Exists(candidate)) icon = candidate;
                }
                result.Add(new BlockTool(id.ToString("N"), name, group, blockName, source, layer,
                    Number(block, "Scale", 1), Number(block, "Rotation", 0) * Math.PI / 180,
                    (string?)block.Element("PromptRotation") == "1", (string?)block.Element("Explode") == "1", icon));
            }
        }
        if (result.Count == 0) throw new InvalidDataException("Geen blocktools gevonden in de XTP.");
        if (result.Select(t => t.Id).Distinct().Count() != result.Count) throw new InvalidDataException("Dubbele tool-ID in de XTP.");
        Tools = result;
    }
    private static double Number(XElement parent, string name, double fallback)
    {
        var value = (string?)parent.Element(name);
        if (value == null) return fallback;
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) || !double.IsFinite(result))
            throw new InvalidDataException("Ongeldige " + name + ": " + value);
        if (name == "Scale" && result <= 0) throw new InvalidDataException("Schaal moet positief zijn.");
        return result;
    }
    public string ResolveSource(BlockTool tool)
    {
        var expanded = tool.SourceFile.Replace("%UserProfileFolder%", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), StringComparison.OrdinalIgnoreCase);
        expanded = Environment.ExpandEnvironmentVariables(expanded);
        var root = Path.GetDirectoryName(PalettePath)!;
        // A packaged snapshot makes the default palette portable. Custom palettes use their own paths.
        var local = Path.Combine(root, "Library", Path.GetFileName(expanded));
        if (File.Exists(local)) return local;
        if (!Path.IsPathRooted(expanded)) expanded = Path.Combine(root, expanded);
        if (!File.Exists(expanded)) throw new FileNotFoundException("Blockbron ontbreekt. Kies een bereikbare palette via Bron...", expanded);
        return Path.GetFullPath(expanded);
    }
}
