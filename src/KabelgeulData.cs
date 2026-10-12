namespace Techbase.Blocks;

internal sealed record KabelgeulRow(string Type = "K01", string Text = "", string Shape = "Enkel", bool TwoSides = false,
    string Text2 = "", string Shape2 = "Enkel", string Mantel = "", bool NewTray = false);
internal sealed record KabelgeulData(KabelgeulRow[] Rows, string MantelSpec = "")
{
    internal static readonly string[] Types = Enumerable.Range(1, 12).Select(i => $"K{i:00}").Concat(new[] { "MS Nieuw", "MS Bestaand", "MS Rooien", "Bestaand", "Rooien", "Half rooien" }).ToArray();
    internal static readonly string[] Shapes = { "Enkel", "Dubbel", "Drie", "Combi" };
    internal static KabelgeulData Default => new(new[] { new KabelgeulRow(), new KabelgeulRow("K02"), new KabelgeulRow("Bestaand") });
    internal object?[] Values() => new object?[] { Rows.Length, Rows.Select(r => r.Type).ToArray(), Rows.Select(r => r.Text).ToArray(),
        Rows.Select(r => Flag(r.Shape == "Dubbel" || r.Type == "Half rooien")).ToArray(), Rows.Select(r => r.Mantel.Length == 0 ? null : r.Mantel).ToArray(),
        Rows.Select(r => Flag(r.NewTray)).ToArray(), Rows.Select(r => Flag(r.Shape == "Combi")).ToArray(), Rows.Select(r => r.Text2).ToArray(),
        Rows.Select(r => Flag(r.Shape == "Drie")).ToArray(), MantelSpec, Rows.Select(r => Flag(r.TwoSides && r.Type != "Half rooien")).ToArray(),
        Rows.Select(r => Flag(r.Shape2 == "Dubbel")).ToArray(), Rows.Select(r => Flag(r.Shape2 == "Drie")).ToArray(), Rows.Select(r => Flag(r.Shape2 == "Combi")).ToArray() };
    private static object? Flag(bool value) => value ? true : null;
    internal string Lisp() => Encode(Values());
    private static string Encode(object? value) => value switch
    {
        null => "nil", bool => "T", int n => n.ToString(), string s => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t") + "\"",
        System.Collections.IEnumerable list => "(" + string.Join(" ", list.Cast<object?>().Select(Encode)) + ")", _ => throw new InvalidOperationException("Onbekend kabelgeulgegeven.")
    };
    internal static KabelgeulData Parse(string text)
    {
        var index = 0;
        object? Read()
        {
            while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
            if (index >= text.Length) throw new FormatException("Onvolledige kabelgeulgegevens.");
            if (text[index] == '(')
            { index++; var list = new List<object?>(); while (true) { while (index < text.Length && char.IsWhiteSpace(text[index])) index++; if (index >= text.Length) throw new FormatException("Onvolledige kabelgeulgegevens."); if (text[index] == ')') { index++; return list.ToArray(); } list.Add(Read()); } }
            if (text[index] == '"')
            { index++; var result = new System.Text.StringBuilder(); while (index < text.Length && text[index] != '"') { var c = text[index++]; if (c == '\\') { if (index >= text.Length) throw new FormatException("Onvolledige kabelgeultekst."); c = text[index++] switch { 'n' => '\n', 'r' => '\r', 't' => '\t', var escaped => escaped }; } result.Append(c); } if (index >= text.Length) throw new FormatException("Onvolledige kabelgeultekst."); index++; return result.ToString(); }
            var start = index; while (index < text.Length && !char.IsWhiteSpace(text[index]) && text[index] != ')') index++;
            var token = text[start..index]; return token.Equals("nil", StringComparison.OrdinalIgnoreCase) ? null : token.Equals("T", StringComparison.OrdinalIgnoreCase) ? true : int.Parse(token);
        }
        if (Read() is not object?[] values || values.Length == 0) throw new FormatException("Ongeldige kabelgeulgegevens.");
        if (!string.IsNullOrWhiteSpace(text[index..])) throw new FormatException("Ongeldige kabelgeulgegevens.");
        return FromValues(values);
    }
    private static KabelgeulData FromValues(object?[] root)
    {
        if (root.Length == 1 && root[0] is object?[] nested) root = nested;
        object? At(int col, int row) => col < root.Length && root[col] is object?[] list && row < list.Length ? list[row] : null;
        string Text(int col, int row) => Convert.ToString(At(col, row)) ?? "";
        bool FlagAt(int col, int row) => At(col, row) != null;
        string Shape(int d, int t, int c, int row) => FlagAt(t, row) ? "Drie" : FlagAt(c, row) ? "Combi" : FlagAt(d, row) ? "Dubbel" : "Enkel";
        var count = Math.Clamp(Convert.ToInt32(root[0]), 1, 100);
        return new KabelgeulData(Enumerable.Range(0, count).Select(i => new KabelgeulRow(Text(1, i), Text(2, i), Shape(3, 8, 6, i), FlagAt(10, i),
            Text(7, i), Shape(11, 12, 13, i), Text(4, i), FlagAt(5, i))).ToArray(), root.Length > 9 ? Convert.ToString(root[9]) ?? "" : "");
    }
}
