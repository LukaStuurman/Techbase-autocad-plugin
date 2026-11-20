using System.Text.RegularExpressions;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using AcColor = Autodesk.AutoCAD.Colors.Color;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using FlowDirection = System.Windows.Forms.FlowDirection;

namespace Techbase.Blocks;

internal static class LsRackFuses
{
    private const string RackKey = "TECHBASE_LS_RACK";
    private const string FuseKey = "TECHBASE_LS_FUSE";
    private const string FitKey = "TECHBASE_FUSE_FIT";
    internal sealed record Slot(int Number, Point3d Center, double Radius, int Contact = 0, bool Normal = true);
    internal sealed record Selection(int[] Directions, int[] Combi);
    internal static bool IsRack(string name) => Regex.IsMatch(name, @"^(?:(8|10|12) RICHTINGEN LS REK(?: REVERSE| KLEIN)?|RT 1-(4|6|8|10|12) (LS|OV)|(8|12) RICHTING(?:EN)? REK OV(?: REVERSE)?|OV[ -]kast 1-(4|6|8))$", RegexOptions.IgnoreCase);
    internal static bool IsOv(string name) => name.EndsWith(" OV", StringComparison.OrdinalIgnoreCase) || name.Contains("REK OV", StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(name, @"^OV[ -]kast", RegexOptions.IgnoreCase);
    internal static int DirectionCount(string name) => int.Parse(Regex.Match(name, @"^(?:RT |OV[ -]kast )1-(\d+)(?: (?:LS|OV))?$", RegexOptions.IgnoreCase) is { Success: true } rt
        ? rt.Groups[1].Value : name.Split(' ')[0]);

    private static string? Metadata(DBObject item, Transaction tx, string key)
    {
        if (item.ExtensionDictionary.IsNull) return null;
        var dictionary = (DBDictionary)tx.GetObject(item.ExtensionDictionary, OpenMode.ForRead);
        if (!dictionary.Contains(key)) return null;
        using var buffer = ((Xrecord)tx.GetObject(dictionary.GetAt(key), OpenMode.ForRead)).Data;
        return buffer?.AsArray().FirstOrDefault().Value as string;
    }
    private static void SetMetadata(DBObject item, Transaction tx, string key, string text)
    {
        if (item.ExtensionDictionary.IsNull) item.CreateExtensionDictionary();
        var dictionary = (DBDictionary)tx.GetObject(item.ExtensionDictionary, OpenMode.ForWrite);
        var record = new Xrecord(); dictionary.SetAt(key, record); tx.AddNewlyCreatedDBObject(record, true);
        using var buffer = new ResultBuffer(new TypedValue((int)DxfCode.Text, text)); record.Data = buffer;
    }
    private static string RackName(BlockTableRecord definition, Transaction tx) =>
        Metadata(definition, tx, RackKey) ?? SourceBlockImporter.OriginalName(definition, tx) ?? definition.Name;

    internal static Slot[] Slots(BlockTableRecord definition, Transaction tx)
    {
        var name = RackName(definition, tx);
        if (!IsRack(name) || definition.IsDependent || definition.IsFromExternalReference)
            throw new InvalidOperationException("Selecteer een lokaal LS- of OV-richtingenblock, rek of OV-kast.");
        var items = definition.Cast<ObjectId>().Where(id => !id.IsErased).Select(id => tx.GetObject(id, OpenMode.ForRead)).ToArray();
        var expected = DirectionCount(name);
        if (IsOv(name)) return OvSlots(name, items, expected);
        if (name.StartsWith("RT ", StringComparison.OrdinalIgnoreCase))
        {
            // The LS REKKEN catalogue group contains RT text blocks. Their
            // circles already carry the definitive direction layer and number.
            var rtSlots = items.OfType<Circle>().Select(c => (Circle: c, Match: Regex.Match(c.Layer, @"^Aansluiting LS K(\d{2})$", RegexOptions.IgnoreCase)))
                .Where(c => c.Match.Success).Select(c => new Slot(int.Parse(c.Match.Groups[1].Value), c.Circle.Center, c.Circle.Radius)).OrderBy(s => s.Number).ToArray();
            if (rtSlots.Length != expected || !rtSlots.Select(s => s.Number).SequenceEqual(Enumerable.Range(1, expected)))
                throw new InvalidOperationException("De gekleurde richtingrondjes in dit RT LS-block zijn onvolledig.");
            return rtSlots;
        }
        var attributes = items.OfType<AttributeDefinition>().Select(a => (Attribute: a, Match: Regex.Match(a.Tag, @"^RI(\d+)$", RegexOptions.IgnoreCase)))
            .Where(a => a.Match.Success).Select(a => (a.Attribute, Number: int.Parse(a.Match.Groups[1].Value))).OrderBy(a => a.Number).ToArray();
        if (attributes.Length != expected || !attributes.Select(a => a.Number).SequenceEqual(Enumerable.Range(1, expected)))
            throw new InvalidOperationException("De richtingnummers in dit LS-rek zijn onvolledig.");
        var circles = items.OfType<Circle>().ToList();
        var result = new List<Slot>();
        foreach (var (attribute, number) in attributes)
        {
            // The RI tags identify the actual physical slots, including reverse
            // layouts. Do not rely on entity order or count the other symbols.
            var circle = circles.OrderBy(c => c.Center.DistanceTo(attribute.Position)).FirstOrDefault();
            if (circle == null || circle.Center.DistanceTo(attribute.Position) > circle.Radius * 3)
                throw new InvalidOperationException("Geen passend rondje gevonden voor richting " + number + ".");
            result.Add(new(number, circle.Center, circle.Radius)); circles.Remove(circle);
        }
        return result.ToArray();
    }
    private static Slot[] OvSlots(string name, DBObject[] items, int expected)
    {
        var circles = items.OfType<Circle>().ToArray(); var result = new List<Slot>();
        var rt = name.StartsWith("RT ", StringComparison.OrdinalIgnoreCase);
        var cabinet = name.StartsWith("OV", StringComparison.OrdinalIgnoreCase);
        var attributes = items.OfType<AttributeDefinition>().Where(a => Regex.IsMatch(a.Tag, @"^RI\d+$", RegexOptions.IgnoreCase)).ToArray();
        for (var number = 1; number <= expected; number++)
        {
            IEnumerable<Circle> candidates;
            if (rt) candidates = circles.Where(c => c.Layer.Equals(DirectionLines.LayerName(number), StringComparison.OrdinalIgnoreCase));
            else
            {
                var attribute = attributes.SingleOrDefault(a => a.Tag.Equals("RI" + number, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException("Richting " + number + " ontbreekt in het OV-block.");
                var closest = circles.OrderBy(c => c.Center.DistanceTo(attribute.Position)).FirstOrDefault()
                    ?? throw new InvalidOperationException("Geen OV-rondjes gevonden.");
                candidates = circles.Where(c => Math.Abs(cabinet ? c.Center.Y - closest.Center.Y : c.Center.X - closest.Center.X) < closest.Radius * .5);
            }
            var group = new List<Circle>();
            // Some source schematics contain a second circle at the same
            // contact. Treat those as one physical position.
            foreach (var circle in candidates.OrderBy(c => rt || cabinet ? c.Center.X : c.Center.Y))
                if (!group.Any(c => c.Center.DistanceTo(circle.Center) < Math.Min(c.Radius, circle.Radius) * .05)) group.Add(circle);
            if (group.Count != 4) throw new InvalidOperationException("Richting " + number + " bevat geen vier herkenbare OV-rondjes.");
            result.AddRange(group.Select((circle, index) => new Slot(number, circle.Center, circle.Radius, index + 1, index != (rt || cabinet ? 2 : 1))));
        }
        return result.ToArray();
    }
    internal static int[] Active(BlockTableRecord definition, Transaction tx) => definition.Cast<ObjectId>().Where(id => !id.IsErased)
        .Select(id => tx.GetObject(id, OpenMode.ForRead)).OfType<BlockReference>()
        .Select(b => Metadata(b, tx, FuseKey)).Where(s => int.TryParse(s, out _)).Select(s => int.Parse(s!)).Distinct().OrderBy(n => n).ToArray();
    internal static int[] Combi(BlockTableRecord definition, Transaction tx) => definition.Cast<ObjectId>().Where(id => !id.IsErased)
        .Select(id => tx.GetObject(id, OpenMode.ForRead)).OfType<BlockReference>()
        .Select(b => Metadata(b, tx, FuseKey)).Where(s => int.TryParse(s, out _)).GroupBy(s => s).Where(g => g.Count() == 4).Select(g => int.Parse(g.Key!)).OrderBy(n => n).ToArray();

    internal static void Apply(Database db, Transaction tx, BlockReference rack, Catalog catalog, IEnumerable<int> chosen, IEnumerable<int>? combiDirections = null)
    {
        if (rack.OwnerId != db.CurrentSpaceId) throw new InvalidOperationException("Selecteer een rek in de actieve tekeningruimte.");
        LayerResolver.CheckLayer(tx, rack.LayerId);
        var source = (BlockTableRecord)tx.GetObject(rack.BlockTableRecord, OpenMode.ForRead);
        var directionColors = RackName(source, tx).StartsWith("RT ", StringComparison.OrdinalIgnoreCase);
        var pink = !directionColors && !Regex.IsMatch(RackName(source, tx), @"^OV[ -]kast", RegexOptions.IgnoreCase);
        var slots = Slots(source, tx); var selected = chosen.Distinct().OrderBy(n => n).ToArray();
        var combi = (combiDirections ?? Array.Empty<int>()).Distinct().OrderBy(n => n).ToArray();
        if (combi.Any(n => !selected.Contains(n)) || combi.Length > 0 && !slots.Any(s => s.Contact > 0)) throw new InvalidOperationException("Combi kan alleen voor een ingeschakelde OV-richting.");
        if (selected.Any(n => !slots.Any(s => s.Number == n))) throw new InvalidOperationException("Kies alleen richtingen die in dit rek aanwezig zijn.");
        var currentFuses = source.Cast<ObjectId>().Where(id => !id.IsErased).Select(id => tx.GetObject(id, OpenMode.ForRead)).OfType<BlockReference>()
            .Where(b => Metadata(b, tx, FuseKey) != null).ToArray();
        var colorsMatch = currentFuses.All(b => directionColors
            ? b.Color.ColorIndex == 256 && int.TryParse(Metadata(b, tx, FuseKey), out var number) && b.Layer == DirectionLines.LayerName(number)
            : b.Color.ColorIndex == (pink ? 6 : 256) && b.LayerId == db.LayerZero);
        var expectedSlots = slots.Where(s => selected.Contains(s.Number) && (s.Normal || combi.Contains(s.Number))).ToArray();
        // Reapplying also repairs the former normal schematic pattern (1/2/4),
        // even when the selected directions and colours have not changed.
        var positionsMatch = currentFuses.Length == expectedSlots.Length && expectedSlots.All(s => currentFuses.Any(f =>
        {
            var box = f.GeometricExtents;
            return (box.MinPoint + (box.MaxPoint - box.MinPoint) * .5).DistanceTo(s.Center) < 1e-7;
        }));
        var sizeMatches = currentFuses.All(f => Metadata(f, tx, FitKey) == "1");
        if (selected.SequenceEqual(Active(source, tx)) && combi.SequenceEqual(Combi(source, tx)) && colorsMatch && positionsMatch && sizeMatches) return;
        ObjectId fuseDefinition = ObjectId.Null;
        var layers = new Dictionary<int, ObjectId>();
        if (selected.Length > 0)
        {
            foreach (var number in selected) layers[number] = directionColors ? DirectionLines.EnsureStyle(db, number).Layer : db.LayerZero;
            var tool = catalog.Tools.First(t => t.BlockName.Equals("Zekering", StringComparison.OrdinalIgnoreCase));
            fuseDefinition = BlockInserter.Import(db, catalog, tool);
        }
        // Isolate only this rack. Other insertions can keep sharing the source
        // definition, and copied racks can later be changed independently.
        var table = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForWrite);
        using var target = new BlockTableRecord { Name = "TB_LSREK_" + Guid.NewGuid().ToString("N"), Origin = source.Origin, Units = source.Units };
        var targetId = table.Add(target); tx.AddNewlyCreatedDBObject(target, true);
        SetMetadata(target, tx, RackKey, RackName(source, tx));
        SourceBlockImporter.CopyIdentity(source, target, tx);
        var map = new IdMapping();
        db.DeepCloneObjects(new ObjectIdCollection(source.Cast<ObjectId>().Where(id => !id.IsErased).ToArray()), targetId, map, false);
        var order = ((DrawOrderTable)tx.GetObject(source.DrawOrderTableId, OpenMode.ForRead)).GetFullDrawOrder(0);
        ((DrawOrderTable)tx.GetObject(target.DrawOrderTableId, OpenMode.ForWrite)).MoveToTop(new ObjectIdCollection(order.Cast<ObjectId>()
            .Where(id => map.Contains(id) && map[id].IsCloned).Select(id => map[id].Value).ToArray()));
        foreach (ObjectId id in target)
            if (tx.GetObject(id, OpenMode.ForRead) is BlockReference oldFuse && Metadata(oldFuse, tx, FuseKey) != null)
            { LayerResolver.CheckLayer(tx, oldFuse.LayerId); oldFuse.UpgradeOpen(); oldFuse.Erase(); }
        if (selected.Length > 0)
        {
            var fitted = new Dictionary<double, ObjectId>();
            foreach (var slot in expectedSlots)
            {
                if (!fitted.TryGetValue(slot.Radius, out var fittedDefinition))
                    fitted[slot.Radius] = fittedDefinition = FitSymbol(db, tx, fuseDefinition, slot.Radius);
                using var symbol = new BlockReference(slot.Center, fittedDefinition)
                {
                    LayerId = layers[slot.Number],
                    Color = AcColor.FromColorIndex(pink ? Autodesk.AutoCAD.Colors.ColorMethod.ByAci : Autodesk.AutoCAD.Colors.ColorMethod.ByLayer, (short)(pink ? 6 : 256)),
                    LinetypeId = db.ContinuousLinetype, LineWeight = LineWeight.ByLayer
                };
                target.AppendEntity(symbol); tx.AddNewlyCreatedDBObject(symbol, true);
                SetMetadata(symbol, tx, FuseKey, slot.Number.ToString(System.Globalization.CultureInfo.InvariantCulture));
                SetMetadata(symbol, tx, FitKey, "1");
            }
        }
        if (RtDrawOrder.IsRt(RackName(source, tx))) RtDrawOrder.ApplyDefinition(targetId, tx);
        rack.UpgradeOpen(); rack.BlockTableRecord = targetId;
    }

    private static ObjectId FitSymbol(Database db, Transaction tx, ObjectId definition, double radius)
    {
        if (!double.IsFinite(radius) || radius <= 0) throw new InvalidOperationException("Het rondje heeft geen geldige straal.");
        var source = (BlockTableRecord)tx.GetObject(definition, OpenMode.ForRead);
        var lines = source.Cast<ObjectId>().Select(id => tx.GetObject(id, OpenMode.ForRead)).OfType<Polyline>().ToArray();
        if (lines.Length != 2 || lines.Any(p => p.NumberOfVertices != 2 || p.GetPoint3dAt(0).DistanceTo(p.GetPoint3dAt(1)) < 1e-9))
            throw new InvalidOperationException("Het zekeringsymbool bevat geen twee herkenbare kruisstrepen.");
        var table = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForWrite);
        using var target = new BlockTableRecord { Name = "TB_ZEKERING_FIT_" + Guid.NewGuid().ToString("N"), Units = source.Units };
        var id = table.Add(target); tx.AddNewlyCreatedDBObject(target, true);
        foreach (var line in lines)
        {
            // Retain the original stroke directions and appearance, but centre
            // both diameters and put every endpoint exactly on the circle.
            var axis = (line.GetPoint2dAt(1) - line.GetPoint2dAt(0)).GetNormal() * radius;
            using var stroke = (Polyline)line.Clone();
            stroke.SetPointAt(0, new Point2d(-axis.X, -axis.Y)); stroke.SetPointAt(1, new Point2d(axis.X, axis.Y));
            stroke.ConstantWidth = line.ConstantWidth * (2 * radius / line.GetPoint2dAt(0).GetDistanceTo(line.GetPoint2dAt(1)));
            target.AppendEntity(stroke); tx.AddNewlyCreatedDBObject(stroke, true);
        }
        return id;
    }

    internal static Point3d[] FuseEndpoints(Transaction tx, BlockReference symbol) =>
        ((BlockTableRecord)tx.GetObject(symbol.BlockTableRecord, OpenMode.ForRead)).Cast<ObjectId>()
            .Select(id => tx.GetObject(id, OpenMode.ForRead)).OfType<Polyline>()
            .SelectMany(p => Enumerable.Range(0, p.NumberOfVertices).Select(i => p.GetPoint3dAt(i).TransformBy(symbol.BlockTransform))).ToArray();

    internal static Selection? ChooseState(Slot[] slots, IEnumerable<int> active, IEnumerable<int> combi, bool placing)
    {
        if (slots.Any(s => s.Contact > 0)) return OvRackChoice.Choose(slots, active, combi, placing);
        var chosen = Choose(slots, active, placing);
        return chosen == null ? null : new(chosen, Array.Empty<int>());
    }

    internal static int[]? Choose(Slot[] slots, IEnumerable<int> active, bool placing)
    {
        var automated = Environment.GetEnvironmentVariable("TECHBASE_LS_RACK_TEST_SELECTION");
        if (automated != null && Environment.GetEnvironmentVariable("TECHBASE_TEST_OUTPUT") != null)
            return automated == "CANCEL" ? null : automated == "EMPTY" ? Array.Empty<int>() : automated.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
        return ShowChoiceDialog(slots, active, placing);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static int[]? ShowChoiceDialog(Slot[] slots, IEnumerable<int> active, bool placing)
    {
        using var form = CreateChoiceDialog(slots, active, placing);
        var list = form.Controls.OfType<TableLayoutPanel>().Single().Controls.OfType<CheckedListBox>().Single();
        return Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(form) == DialogResult.OK
            ? list.CheckedIndices.Cast<int>().Select(i => slots[i].Number).ToArray() : null;
    }

    internal static Form CreateChoiceDialog(Slot[] slots, IEnumerable<int> active, bool placing)
    {
        var form = new Form
        {
            Text = "LS-rek — richtingen in gebruik", StartPosition = FormStartPosition.CenterParent,
            ClientSize = new System.Drawing.Size(420, 430), MinimumSize = new System.Drawing.Size(380, 360)
        };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label { Text = "Vink de richtingen aan die in gebruik zijn.\nNiet aangevinkt = leeg rondje.", AutoSize = true, Margin = new Padding(0, 0, 0, 10) }, 0, 0);
        var list = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false };
        var selected = active.ToHashSet();
        foreach (var slot in slots) list.Items.Add("Richting " + slot.Number + " — in gebruik", selected.Contains(slot.Number));
        layout.Controls.Add(list, 0, 1);
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var accept = new ThemedButton { Text = "Toepassen", AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new ThemedButton { Text = placing ? "Leeg laten" : "Annuleren", AutoSize = true, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(accept); buttons.Controls.Add(cancel); layout.Controls.Add(buttons, 0, 2);
        form.Controls.Add(layout); form.AcceptButton = accept; form.CancelButton = cancel; UiTypography.Apply(form); UiTheme.Primary(accept);
        return form;
    }
}

public sealed partial class Plugin
{
    [CommandMethod("TBOVREK", CommandFlags.Modal)]
    public void ConfigureOvRack() => ConfigureLsRack();

    [CommandMethod("TBLSREK", CommandFlags.Modal)]
    public void ConfigureLsRack()
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument; if (doc == null) return;
        try
        {
            var options = new PromptEntityOptions("\nSelecteer het LS- of OV-block waarvan je de richtingen wilt instellen: ");
            options.SetRejectMessage("\nSelecteer een richtingenblock of rek."); options.AddAllowedClass(typeof(BlockReference), true);
            var result = doc.Editor.GetEntity(options); if (result.Status != PromptStatus.OK) return;
            using var tx = doc.Database.TransactionManager.StartTransaction();
            var rack = (BlockReference)tx.GetObject(result.ObjectId, OpenMode.ForRead);
            var definition = (BlockTableRecord)tx.GetObject(rack.BlockTableRecord, OpenMode.ForRead);
            var chosen = LsRackFuses.ChooseState(LsRackFuses.Slots(definition, tx), LsRackFuses.Active(definition, tx), LsRackFuses.Combi(definition, tx), false);
            if (chosen == null) return;
            LsRackFuses.Apply(doc.Database, tx, rack, Blocks ?? new Catalog(DefaultPalette), chosen.Directions, chosen.Combi);
            var ov = LsRackFuses.Slots((BlockTableRecord)tx.GetObject(rack.BlockTableRecord, OpenMode.ForRead), tx).Any(s => s.Contact > 0);
            tx.Commit(); doc.Editor.Regen(); doc.Editor.WriteMessage("\n" + (ov ? "OV-rek" : "LS-rek") + " bijgewerkt: " + chosen.Directions.Length + " richting(en) in gebruik.");
        }
        catch (System.Exception ex) { doc.Editor.WriteMessage("\nLS-rek: " + ex.Message); }
    }
}
