using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using System.Drawing;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: ExtensionApplication(typeof(Techbase.Blocks.Plugin))]
[assembly: CommandClass(typeof(Techbase.Blocks.Plugin))]

namespace Techbase.Blocks;

public sealed class Plugin : IExtensionApplication
{
    internal static Settings Preferences { get; private set; } = Settings.Read();
    internal static Catalog? Blocks { get; private set; }
    private static PaletteSet? palette;
    private static BlocksPanel? panel;
    private static LightmastPanel? lightmastPanel;
    private static KabelCheckerPanel? kabelCheckerPanel;
    private static DirectionPanel? directionPanel;
    internal static LightmastRequest? PendingLightmast { get; set; }
    internal static DirectionInfoRequest? PendingDirectionInfo { get; set; }
    internal static string ContentsRoot => Directory.GetParent(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!)!.FullName;
    internal static string DefaultPalette => Path.Combine(ContentsRoot, "Assets", "Techbase toolpallete.xtp");
    public void Initialize()
    {
        try { Reload(); }
        catch (System.Exception ex) { AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nTechbase Blocks: " + ex.Message); }
    }
    public void Terminate() { directionPanel?.Dispose(); kabelCheckerPanel?.Dispose(); lightmastPanel?.Dispose(); panel?.Dispose(); palette?.Dispose(); }
    internal static void Reload(string? path = null)
    {
        var candidate = new Catalog(path ?? Preferences.PalettePath ?? DefaultPalette);
        Blocks = candidate;
        if (path != null) { Preferences.PalettePath = path; Preferences.Save(); }
        panel?.RefreshTools();
    }
    internal static void UpdateSettings()
    {
        try { Preferences.Save(); }
        catch (System.Exception ex) { AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nInstelling kon niet worden opgeslagen: " + ex.Message); }
        panel?.RefreshSettings();
    }
    [CommandMethod("TECHBASE", CommandFlags.Modal)]
    public void Show()
    {
        try
        {
            if (Blocks == null) Reload();
            if (palette == null)
            {
                palette = new PaletteSet("Techbase", new Guid("F0674EA1-D625-43F5-A0BA-016A165B0D66"))
                {
                    Style = PaletteSetStyles.ShowAutoHideButton | PaletteSetStyles.ShowCloseButton | PaletteSetStyles.ShowPropertiesMenu,
                    MinimumSize = new Size(330, 560), Size = new Size(380, 650),
                    KeepFocus = false, DockEnabled = DockSides.Left | DockSides.Right
                };
                panel = new BlocksPanel();
                palette.Add("Blocks", panel);
                lightmastPanel = new LightmastPanel();
                palette.Add("Lichtmasten", lightmastPanel);
                kabelCheckerPanel = new KabelCheckerPanel(ExpandChecker, () => palette.Activate(0));
                palette.Add("Enexis kabelchecker", kabelCheckerPanel);
                directionPanel = new DirectionPanel();
                palette.Add("Richtingen", directionPanel);
            }
            palette.Visible = true;
        }
        catch (System.Exception ex) { Error(ex); }
    }
    [CommandMethod("TBLAAG", CommandFlags.Modal)]
    public void Toggle()
    {
        Preferences.InheritLayer = !Preferences.InheritLayer;
        UpdateSettings();
        AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nTechbase: laag overnemen " + (Preferences.InheritLayer ? "AAN" : "UIT") + ".");
    }
    [CommandMethod("TBRICHTINGEN", CommandFlags.Modal)]
    public void ShowDirections() { Show(); if (palette != null) palette.Activate(3); }
    private static int? PromptDirection()
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc == null) return null;
        var result = doc.Editor.GetInteger(new Autodesk.AutoCAD.EditorInput.PromptIntegerOptions("\nRichting (1 t/m 12): ")
        { LowerLimit = 1, UpperLimit = 12, DefaultValue = Preferences.SelectedDirectionNumber, AllowNone = true });
        return result.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK ? result.Value :
            result.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.None ? Preferences.SelectedDirectionNumber : null;
    }
    private static void ReportDirection(string message)
    { directionPanel?.Report(message); AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\n" + message); }
    [CommandMethod("TBTEKENRICHTING", CommandFlags.Modal)]
    public void DrawDirection()
    {
        try
        {
            var number = PromptDirection(); if (!number.HasValue) return;
            var doc = AcApp.DocumentManager.MdiActiveDocument; if (doc == null) return;
            ReportDirection(DirectionLines.Draw(doc, number.Value));
        }
        catch (System.Exception ex) { ReportDirection(ex.Message); }
    }
    [CommandMethod("TBMAAKRICHTING", CommandFlags.Modal | CommandFlags.UsePickSet)]
    public void ConvertDirection()
    {
        try
        {
            var number = PromptDirection(); if (!number.HasValue) return;
            var doc = AcApp.DocumentManager.MdiActiveDocument; if (doc == null) return;
            var selection = doc.Editor.SelectImplied();
            if (selection.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                selection = doc.Editor.GetSelection(new Autodesk.AutoCAD.EditorInput.PromptSelectionOptions
                { MessageForAdding = "\nSelecteer polylines of losse lijnen om om te zetten: " },
                new Autodesk.AutoCAD.EditorInput.SelectionFilter(new[]
                { new Autodesk.AutoCAD.DatabaseServices.TypedValue((int)Autodesk.AutoCAD.DatabaseServices.DxfCode.Start, "LWPOLYLINE,POLYLINE,LINE") }));
            if (selection.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) { ReportDirection("Omzetten geannuleerd."); return; }
            using var working = new WorkingDatabaseScope(doc.Database);
            var result = DirectionLines.Convert(doc.Database, selection.Value.GetObjectIds(), number.Value);
            doc.Editor.Regen();
            ReportDirection($"{result.Changed} objecten omgezet naar richting {number.Value}. " +
                $"{result.LinesConverted} losse lijnen werden polylines; {result.Skipped} objecten overgeslagen." +
                (result.Skipped > 0 ? " Alleen bewerkbare 2D-polylines en LINE-lijnen in de huidige tekenruimte worden omgezet." : ""));
        }
        catch (System.Exception ex) { ReportDirection(ex.Message); }
    }
    [CommandMethod("TBRICHTINGVALIDATE", CommandFlags.Modal)]
    public void ValidateDirectionLines()
    {
        try { DirectionLineDiagnostics.Run(); }
        catch (System.Exception ex)
        {
            Error(ex); var folder = Environment.GetEnvironmentVariable("TECHBASE_TEST_OUTPUT");
            if (folder != null) { Directory.CreateDirectory(folder); File.WriteAllText(Path.Combine(folder, "richting-failure.log"), ex.ToString()); }
        }
    }
    [CommandMethod("TBKABELCHECK", CommandFlags.Modal)]
    public void KabelChecker()
    {
        Show();
        if (palette == null) return;
        palette.Activate(2);
        ExpandChecker();
    }
    private static void ExpandChecker()
    {
        if (palette == null) return;
        var screen = System.Windows.Forms.Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 900);
        palette.Dock = DockSides.None;
        palette.Size = new Size(Math.Min(1080, screen.Width - 60), Math.Min(940, screen.Height - 60));
        palette.Visible = true;
    }
    [CommandMethod("TBRICHTINGINFO", CommandFlags.Modal)]
    public void DirectionInfo()
    {
        var request = PendingDirectionInfo;
        PendingDirectionInfo = null;
        if (request == null) { KabelChecker(); kabelCheckerPanel?.ReportInfo("Selecteer een richting en klik onderin op Richting-info plaatsen."); return; }
        if (request.Document != AcApp.DocumentManager.MdiActiveDocument)
        { kabelCheckerPanel?.ReportInfo("De actieve tekening is veranderd; selecteer de richting opnieuw."); return; }
        try
        {
            var message = DirectionInfoService.Place(request);
            kabelCheckerPanel?.ReportInfo(message); request.Document.Editor.WriteMessage("\n" + message);
        }
        catch (System.Exception ex) { Error(ex); kabelCheckerPanel?.ReportInfo(ex.Message); }
    }
    [CommandMethod("TBLICHTMAST", CommandFlags.Modal)]
    public void Lightmasts()
    {
        var request = PendingLightmast;
        PendingLightmast = null;
        if (request == null) { Show(); if (palette != null) palette.Activate(1); return; }
        if (request.Document != AcApp.DocumentManager.MdiActiveDocument)
        { AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nDe actieve tekening is veranderd. Kies de xref opnieuw."); return; }
        try { var message = LightmastCommands.Execute(request); lightmastPanel?.Report(message); }
        catch (System.Exception ex) { Error(ex); lightmastPanel?.Report(ex.Message); }
    }
    [CommandMethod("TBPLAATS", CommandFlags.Modal)]
    public void Insert()
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc == null) return;
        try
        {
            if (Blocks == null) Reload();
            var code = doc.Editor.GetString("\nTool-ID: ");
            if (code.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) return;
            var tool = Blocks!.Tools.FirstOrDefault(t => t.Id.Equals(code.StringResult, StringComparison.OrdinalIgnoreCase));
            if (tool == null) { doc.Editor.WriteMessage("\nOnbekende tool-ID. Gebruik TECHBASE."); return; }
            do { if (!BlockInserter.Place(doc, Blocks, tool, Preferences)) break; } while (Preferences.Repeat);
        }
        catch (System.Exception ex) { Error(ex); }
    }
    [CommandMethod("TBHERLAAD", CommandFlags.Modal)]
    public void Refresh()
    {
        try { Reload(); AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nTechbase-palette opnieuw ingelezen."); }
        catch (System.Exception ex) { Error(ex); }
    }
    [CommandMethod("TBVALIDATE", CommandFlags.Modal)]
    public void Validate()
    {
        try { Diagnostics.Run(Blocks ?? new Catalog(DefaultPalette)); }
        catch (System.Exception ex)
        {
            Error(ex);
            var folder = Environment.GetEnvironmentVariable("TECHBASE_TEST_OUTPUT");
            if (folder != null) { Directory.CreateDirectory(folder); File.WriteAllText(Path.Combine(folder, "failure.log"), ex.ToString()); }
        }
    }
    [CommandMethod("TBMASTVALIDATE", CommandFlags.Modal)]
    public void ValidateLightmasts()
    {
        try { LightmastDiagnostics.Run(); }
        catch (System.Exception ex)
        {
            Error(ex); Directory.CreateDirectory(LightmastDiagnostics.Output);
            File.WriteAllText(Path.Combine(LightmastDiagnostics.Output, "lichtmast-failure.log"), ex.ToString());
        }
    }
    [CommandMethod("TBKABELVALIDATE", CommandFlags.Modal)]
    public void ValidateKabelChecker()
    {
        try { KabelCheckerDiagnostics.Run(); }
        catch (System.Exception ex)
        {
            Error(ex); Directory.CreateDirectory(LightmastDiagnostics.Output);
            File.WriteAllText(Path.Combine(LightmastDiagnostics.Output, "kabelchecker-failure.log"), ex.ToString());
        }
    }
    [CommandMethod("TBKABELINPUTTEST", CommandFlags.Modal)]
    public void TestKabelInput()
    {
        if (Environment.GetEnvironmentVariable("TECHBASE_TEST_OUTPUT") == null) return;
        try
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var options = new Autodesk.AutoCAD.EditorInput.PromptKeywordOptions("\nKabelselectietest: ");
            options.Keywords.Add("Volledig"); options.Keywords.Add("Knip");
            var mode = doc.Editor.GetKeywords(options);
            var result = mode.StringResult == "Knip"
                ? Enexis.KabelChecker.AutoCAD.DirectionPolylineSelection.PickPolylinePartToVirtualCut("4*150mm2 Al")
                : Enexis.KabelChecker.AutoCAD.DirectionPolylineSelection.PickSinglePolyline("4*150mm2 Al");
            var expected = mode.StringResult == "Knip" ? 75 : 100;
            if (result.Cancelled || Math.Abs(result.LengthMeters - expected) > 1e-8 || result.SuggestedDirectionNumber != 3)
                throw new InvalidOperationException("Kabelselectietest mislukt: " + result.Message);
            doc.Editor.WriteMessage(mode.StringResult == "Knip" ? "\nKABEL_CUT_INPUT_PASS" : "\nKABEL_FULL_INPUT_PASS");
        }
        catch (System.Exception ex) { Error(ex); }
    }
    [CommandMethod("TBINFOSETUPTEST", CommandFlags.Modal)]
    public void TestInfoSetup()
    {
        if (Environment.GetEnvironmentVariable("TECHBASE_TEST_OUTPUT") != null) DirectionInfoDiagnostics.QueueFixture();
    }
    [CommandMethod("TBMASTAREATEST", CommandFlags.Modal)]
    public void TestDrawArea()
    {
        if (Environment.GetEnvironmentVariable("TECHBASE_TEST_OUTPUT") == null) return;
        try
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var prompt = new Autodesk.AutoCAD.EditorInput.PromptKeywordOptions("\nGebiedstest: ");
            prompt.Keywords.Add("Tekenen"); prompt.Keywords.Add("Polyline");
            var mode = doc.Editor.GetKeywords(prompt);
            var area = mode.StringResult == "Polyline" ? LightmastCommands.SelectBoundary(doc.Editor, doc.Database) : LightmastCommands.DrawBoundary(doc.Editor);
            if (area == null) { doc.Editor.WriteMessage("\nAREA_CANCEL_PASS"); return; }
            if (!area.Contains(new Autodesk.AutoCAD.Geometry.Point3d(5, 5, 0)) || area.Contains(new Autodesk.AutoCAD.Geometry.Point3d(15, 5, 0)))
                throw new InvalidOperationException("Gebiedstest mislukt.");
            doc.Editor.WriteMessage("\nAREA_INPUT_PASS");
        }
        catch (System.Exception ex) { Error(ex); }
    }
    private static void Error(System.Exception ex) => AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nTechbase: " + ex.Message);
}
