using System.IO;
using System.Text.Json;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Techbase.Blocks;

internal sealed record KabelgeulRequest(Document Document);
internal static class KabelgeulCommands
{
    internal static KabelgeulRequest? Pending { get; set; }
    internal static KabelgeulRequest? Active { get; set; }
    private static Action? restore;
    internal static void Finish()
    {
        try { KabelgeulEditing.Abort(); }
        finally { var action = restore; restore = null; try { action?.Invoke(); } finally { Active = null; } }
    }
    internal static string Initialize()
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("Open eerst een tekening.");
        var request = Pending; Pending = null;
        if (request != null && request.Document != doc) throw new InvalidOperationException("De actieve tekening is veranderd.");
        Finish(); Active = request;
        var frame = AcApp.GetSystemVariable("WIPEOUTFRAME");
        void Restore(object sender, CommandEventArgs args)
        {
            if (!new[] { "TBKABELGEUL", "TBGEULCOPY", "TBGEULEDIT", "TBGEULCODE" }.Contains(args.GlobalCommandName, StringComparer.OrdinalIgnoreCase)) return;
            Finish();
        }
        restore = () => { doc.CommandEnded -= Restore; doc.CommandCancelled -= Restore; doc.CommandFailed -= Restore;
            AcApp.SetSystemVariable("WIPEOUTFRAME", frame); };
        doc.CommandEnded += Restore; doc.CommandCancelled += Restore; doc.CommandFailed += Restore;
        string style;
        using (var tx = doc.Database.TransactionManager.StartTransaction())
        { style = ((TextStyleTableRecord)tx.GetObject(Typography.Style(doc.Database, tx), OpenMode.ForRead)).Name; tx.Commit(); }
        return style;
    }
}

public sealed partial class Plugin
{
    [CommandMethod("TBGEULINIT", CommandFlags.Modal)] public void InitializeKabelgeul() { }
    [LispFunction("TBKG_NATIVE_STYLE")] public static string InitializeKabelgeulStyle(ResultBuffer args) => KabelgeulCommands.Initialize();
    [LispFunction("TBKG_NATIVE_FINISH")] public static void FinishKabelgeul(ResultBuffer args) => KabelgeulCommands.Finish();

    [LispFunction("TBKG_CONFIG_STRING")]
    public static string ConfigureKabelgeul(ResultBuffer args)
    {
        return ConfigureKabelgeulCore(args);
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static string ConfigureKabelgeulCore(ResultBuffer args)
    {
        var test = Environment.GetEnvironmentVariable("TECHBASE_KABELGEUL_TEST_CONFIG");
        var detected = KabelgeulData.Parse((string)args.AsArray()[0].Value);
        var action = args.AsArray().ElementAtOrDefault(1).Value as string ?? "KABELGEUL";
        if (action == "KABELGEUL") detected = detected.WithDefaults(Preferences);
        if (test == "DEFAULTS") return detected.Lisp();
        var editTest = Environment.GetEnvironmentVariable("TECHBASE_KABELGEUL_TEST_EDIT");
        if (action == "KABELGEULEDIT" && editTest != null) return editTest == "CANCEL" ? "nil" : (JsonSerializer.Deserialize<KabelgeulData>(File.ReadAllText(editTest)) ?? detected).Lisp();
        if (test != null) return action is "KABELGEULCOPY" or "KABELGEULEDIT" ? detected.Lisp() : (JsonSerializer.Deserialize<KabelgeulData>(File.ReadAllText(test)) ?? detected).Lisp();
        return ShowKabelgeulDialog(detected, action);
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static string ShowKabelgeulDialog(KabelgeulData detected, string action)
    {
        var editing = action == "KABELGEULEDIT";
        using var form = new Form { Text = editing ? "Kabelgeul bewerken - kabels en zijden" : "Kabelgeul - kabels en zijden", Size = new System.Drawing.Size(1040, 590), MinimumSize = new System.Drawing.Size(700, 400), StartPosition = FormStartPosition.CenterParent };
        using var editor = new KabelgeulEditor(detected, useDefaultsOnTypeChange: action == "KABELGEUL");
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft };
        var accept = new Button { Text = editing ? "Wijzigingen opslaan" : "Plaatsen", AutoSize = true };
        var cancel = new Button { Text = "Annuleren", AutoSize = true, DialogResult = DialogResult.Cancel };
        KabelgeulData? result = null;
        accept.Click += (_, _) => { try { result = editor.CaptureData(); form.DialogResult = DialogResult.OK; } catch (System.Exception ex) { MessageBox.Show(form, ex.Message, "Kabelgeul"); } };
        footer.Controls.Add(accept); footer.Controls.Add(cancel);
        form.Controls.Add(editor); form.Controls.Add(footer); form.AcceptButton = accept; form.CancelButton = cancel; UiTypography.Apply(form);
        if (Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(form) != DialogResult.OK || result == null) return "nil";
        return result.Lisp();
    }

    [LispFunction("TBKG_NATIVE_LAYER")]
    public static string KabelgeulLayer(ResultBuffer args)
    {
        var values = args.AsArray(); var name = (string)values[0].Value; var color = Convert.ToInt16(values[1].Value); var linetype = (string)values[2].Value;
        var db = AcApp.DocumentManager.MdiActiveDocument.Database;
        using var tx = db.TransactionManager.StartTransaction(); var table = (LayerTable)tx.GetObject(db.LayerTableId, OpenMode.ForRead);
        if (table.Has(name)) return name;
        var types = (LinetypeTable)tx.GetObject(db.LinetypeTableId, OpenMode.ForRead);
        if (!types.Has(linetype)) { try { db.LoadLineTypeFile(linetype, db.Measurement == MeasurementValue.Metric ? "acadiso.lin" : "acad.lin"); } catch (Autodesk.AutoCAD.Runtime.Exception) { linetype = "Continuous"; } }
        table.UpgradeOpen(); var layer = new LayerTableRecord { Name = name, Color = name is "MS Nieuw" or "MS Bestaand" ? Autodesk.AutoCAD.Colors.Color.FromRgb(0, 0, 0) : Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, color), LinetypeObjectId = types[linetype] };
        if (ExistingCableColors.Matches(name)) layer.Color = ExistingCableColors.Choice(Preferences.ExistingCableBlack);
        table.Add(layer); tx.AddNewlyCreatedDBObject(layer, true); tx.Commit(); return name;
    }
    [LispFunction("TBKG_NATIVE_CHECK_LAYER")]
    public static void CheckKabelgeulLayer(ResultBuffer args)
    {
        var name = (string)args.AsArray()[0].Value;
        var db = AcApp.DocumentManager.MdiActiveDocument.Database;
        using var tx = db.TransactionManager.StartTransaction(); var layers = (LayerTable)tx.GetObject(db.LayerTableId, OpenMode.ForRead);
        if (!layers.Has(name)) throw new InvalidOperationException("Geullaag ontbreekt: " + name);
        var layer = (LayerTableRecord)tx.GetObject(layers[name], OpenMode.ForRead);
        LayerResolver.CheckLayer(tx, layer.ObjectId);
        if (layer.IsDependent || layer.IsOff || layer.IsFrozen) throw new InvalidOperationException("Geullaag '" + name + "' is extern, verborgen of bevroren. Pas de laag zelf aan.");
    }
    [LispFunction("TBKG_NATIVE_GUARD")]
    public static TypedValue GuardKabelgeul(ResultBuffer args)
    {
        var id = (ObjectId)args.AsArray()[0].Value; var db = AcApp.DocumentManager.MdiActiveDocument.Database;
        using var tx = db.TransactionManager.StartTransaction();
        if (id.Database != db || tx.GetObject(id, OpenMode.ForRead) is not BlockReference block || block.OwnerId != db.CurrentSpaceId) return new((int)LispDataType.Nil);
        var record = (BlockTableRecord)tx.GetObject(block.BlockTableRecord, OpenMode.ForRead);
        if (record.IsDependent || record.IsFromExternalReference || !record.Name.StartsWith("KabelGeul_", StringComparison.OrdinalIgnoreCase)) return new((int)LispDataType.Nil);
        LayerResolver.CheckLayer(tx, block.LayerId); return new((int)LispDataType.ObjectId, id);
    }
    [LispFunction("TBKG_NATIVE_COPY")]
    public static ObjectId CopyKabelgeulInsert(ResultBuffer args)
    {
        var values = args.AsArray(); var id = (ObjectId)values[0].Value; var from = (Point3d)values[1].Value; var to = (Point3d)values[2].Value;
        var db = AcApp.DocumentManager.MdiActiveDocument.Database; using var tx = db.TransactionManager.StartTransaction();
        var source = (BlockReference)tx.GetObject(id, OpenMode.ForRead); LayerResolver.CheckLayer(tx, source.LayerId);
        var map = new IdMapping(); db.DeepCloneObjects(new ObjectIdCollection(new[] { id }), db.CurrentSpaceId, map, false);
        var copied = map[id].Value; var clone = (BlockReference)tx.GetObject(copied, OpenMode.ForWrite);
        var transform = Matrix3d.Displacement(to - from); clone.TransformBy(transform);
        foreach (ObjectId attrId in clone.AttributeCollection) ((AttributeReference)tx.GetObject(attrId, OpenMode.ForWrite)).TransformBy(transform);
        tx.Commit(); return copied;
    }
    [LispFunction("TBKG_NATIVE_CLONE")]
    public static string? CloneKabelgeulDefinition(ResultBuffer args)
    {
        var values = args.AsArray(); var oldName = (string)values[0].Value; var newName = (string)values[1].Value;
        var db = AcApp.DocumentManager.MdiActiveDocument.Database; using var tx = db.TransactionManager.StartTransaction(); var table = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForWrite);
        if (!table.Has(oldName) || table.Has(newName)) return null;
        var old = (BlockTableRecord)tx.GetObject(table[oldName], OpenMode.ForRead); if (old.IsDependent || old.IsFromExternalReference) return null;
        var record = new BlockTableRecord { Name = newName, Origin = old.Origin, Units = old.Units }; var id = table.Add(record); tx.AddNewlyCreatedDBObject(record, true);
        db.DeepCloneObjects(new ObjectIdCollection(old.Cast<ObjectId>().ToArray()), id, new IdMapping(), false); tx.Commit(); return newName;
    }
}
