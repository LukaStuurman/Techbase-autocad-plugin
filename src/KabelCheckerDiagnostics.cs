using System.IO.Compression;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using Enexis.KabelChecker.AutoCAD;
using Enexis.KabelChecker.Core;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Techbase.Blocks;

internal static class KabelCheckerDiagnostics
{
    internal static void Run()
    {
        var folder = Path.Combine(LightmastDiagnostics.Output, "Kabelchecker");
        Directory.CreateDirectory(folder);
        var checks = new List<string>();
        void Check(bool condition, string description) { if (!condition) throw new InvalidOperationException("Kabelcheckertest mislukt: " + description); checks.Add(description); }
        var engine = new KabelCheckerEngine();
        var calculation = engine.Calculate(new[] { new CableSegment("4*150mm2 Al", 204), new CableSegment("4*120mm2 Al", 127) }, LoadProfile.LaatsteHelft);
        Check(calculation.FuseAmps == 160 && calculation.MaxDesignCurrentAmps == 144, "geïntegreerde rekenengine: upstream referentie 160 gG / 144 A");
        Check(Math.Abs(calculation.TotalImpedanceOhm - 0.08207385655615314) < 1e-12, "geïntegreerde impedantie gelijk aan upstream referentie");
        foreach (var version in Enum.GetValues<KaderVersion>())
        {
            var options = ExcelLoadCatalog.For(version);
            Check(options.Count > 0, "belastingcatalogus " + version);
            var definition = KaderVersions.Get(version);
            var mapped = new[] { new ExcelMappedLoad(options[0].Key, options[0].CableDesignCurrentAmps, 2) };
            var directions = new[] { new DirectionState(1, LoadProfile.Evenredig, new[] { new CableSegment("4*150mm2 Al", 100) },
                new[] { new CurrentLoadInput(options[0].CableDesignCurrentAmps, 2) }, mapped) };
            KaderVersionSelection.SetCurrent(version);
            KaderVersionSelection.SetMode(DesignCurrentMode.Automatisch);
            var request = (string)typeof(ExcelDirectionExporter).GetMethod("BuildWorkerRequest", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { directions, version })!;
            var bytes = (byte[])typeof(ExcelDirectionExporter).GetMethod("ReadEmbeddedTemplate", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { definition.ResourceFileName })!;
            Check(bytes.Length > 100000, "ingebouwd officieel template " + version);
            var file = Path.Combine(folder, "Geintegreerde export " + version + ".xlsx");
            IsolatedExcelWorker.Export(file, bytes, request);
            using var archive = ZipFile.OpenRead(file);
            var workbookEntry = archive.GetEntry("xl/workbook.xml") ?? throw new InvalidOperationException("Ongeldige XLSX.");
            using var input = workbookEntry.Open();
            var workbook = XDocument.Load(input);
            XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var sheetNames = workbook.Descendants(spreadsheet + "sheet").Select(s => (string?)s.Attribute("name")).ToArray();
            var legacy = version is KaderVersion.K2024_1_0 or KaderVersion.K2025_2_0;
            Check(sheetNames.Contains(legacy ? "Ontwerpstroom_trafo" : "Transformator"), "gezamenlijk trafoblad in export " + version);
            Check(sheetNames.Contains(legacy ? "Ontwerpstroom_kabel R1" : "(1)"), "ingevuld richtingblad in export " + version);
            var sheet = workbook.Descendants(spreadsheet + "sheet").Single(s => (string?)s.Attribute("name") == (legacy ? "Ontwerpstroom_kabel R1" : "(1)"));
            XNamespace officeRelationship = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            XNamespace packageRelationship = "http://schemas.openxmlformats.org/package/2006/relationships";
            using var relationStream = archive.GetEntry("xl/_rels/workbook.xml.rels")!.Open();
            var relations = XDocument.Load(relationStream);
            var relation = relations.Descendants(packageRelationship + "Relationship").Single(r => (string?)r.Attribute("Id") == (string?)sheet.Attribute(officeRelationship + "id"));
            var target = (string)relation.Attribute("Target")!;
            var sheetPath = target.StartsWith("/") ? target.TrimStart('/') : "xl/" + target;
            using var sheetStream = archive.GetEntry(sheetPath)!.Open();
            var sheetXml = XDocument.Load(sheetStream);
            var countAddress = (legacy ? "A" : "B") + options[0].Row;
            var count = sheetXml.Descendants(spreadsheet + "c").Single(c => (string?)c.Attribute("r") == countAddress).Element(spreadsheet + "v")?.Value;
            Check(count == "2", "definitieve categorieaantallen in opgeslagen XLSX " + version);
        }
        Check(!System.Runtime.Loader.AssemblyLoadContext.Default.Assemblies.Any(a => a.GetName().Name == "ClosedXML"), "Excel-dependencies blijven buiten de AutoCAD standaardcontext");
        KaderVersionSelection.SetCurrent(KaderVersion.K2026_3_2);
        using (var panel = new KabelCheckerPanel(presets: new Dictionary<string, double> { ["4*150mm2 Al"] = 100 }))
        {
            panel.Size = new Size(1060, 900);
            panel.CreateControl();
            panel.PerformLayout();
            panel.Builder.CreateControl();
            panel.Builder.PerformLayout();
            Application.DoEvents();
            Control[] All(Control control) => new[] { control }.Concat(control.Controls.Cast<Control>().SelectMany(All)).ToArray();
            var controls = All(panel);
            foreach (var text in new[] { "Station opslaan", "Station laden", "Richting opslaan", "Bereken richting", "Hele polyline", "Deel tot knippunt", "Uit kader", "Excel exporteren", "Alle richtingen verwijderen", "Richting-info plaatsen" })
                Check(controls.OfType<Button>().Any(button => button.Text == text), "bediening in tab: " + text);
            Check(panel.Builder.TopLevel == false && panel.Builder.Parent != null, "echte richtingbouwer ingebed in tab");
            var grid = (DataGridView)typeof(KabelCheckerForm).GetField("_grid", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(panel.Builder)!;
            Check(grid.Rows.Count == 1, "preset kabelsegment in ingebedde richtingbouwer");
            var currentPanel = (CurrentLoadPanel)typeof(KabelCheckerForm).GetField("_currentLoadPanel", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(panel.Builder)!;
            var option = ExcelLoadCatalog.For(KaderVersion.K2026_3_2)[0];
            currentPanel.LoadCurrentLoads(new[] { new CurrentLoadInput(option.CableDesignCurrentAmps, 2) },
                new[] { new ExcelMappedLoad(option.Key, option.CableDesignCurrentAmps, 2) });
            typeof(KabelCheckerForm).GetMethod("Calculate", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(panel.Builder, null);
            var fuse = (Label)typeof(KabelCheckerForm).GetField("_fuseResult", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(panel.Builder)!;
            Check(fuse.Text.Contains("250 A gG"), "berekenknop gebruikt rekenengine vanuit de ingebedde vorm");
            var save = (bool)typeof(KabelCheckerForm).GetMethod("SaveDirectionCore", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(panel.Builder, new object[] { false })!;
            var captured = panel.Builder.CaptureSelectedDirectionInfo();
            Check(save && captured?.Number == 1 && Math.Abs(captured.Segments.Sum(s => s.LengthMeters) - 100) < 1e-9, "info-knop neemt de geselecteerde richting met actuele invoer over");
            using var image = new Bitmap(panel.Width, panel.Height);
            panel.DrawToBitmap(image, new Rectangle(Point.Empty, panel.Size));
            image.Save(Path.Combine(folder, "Kabelchecker-tab.png"), System.Drawing.Imaging.ImageFormat.Png);
            panel.Builder.Close();
            Check(!panel.Builder.IsDisposed && grid.Rows.Count == 1, "Sluiten bewaart onopgeslagen invoer in de tab");
        }
        DirectionInfoDiagnostics.Run(checks, folder);
        File.WriteAllText(Path.Combine(folder, "validatie-kabelchecker.json"), JsonSerializer.Serialize(new { Status = "PASS", Host = Convert.ToString(AcApp.GetSystemVariable("ACADVER")), SourceCommit = "4009601e15fa4b1b6926380395132b0c1630ce21", Checks = checks }, new JsonSerializerOptions { WriteIndented = true }));
        AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nKABELCHECKER VALIDATION PASS: " + checks.Count + " checks.");
    }
}
