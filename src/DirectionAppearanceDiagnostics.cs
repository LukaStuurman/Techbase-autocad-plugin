using System.Drawing;
using System.IO;
using System.Text.Json;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Techbase.Blocks;

internal static class DirectionAppearanceDiagnostics
{
    internal static void Run(Catalog catalog, string parent)
    {
        var output = Path.Combine(parent, "Tekstopmaak"); Directory.CreateDirectory(output);
        var checks = new List<string>();
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException("Tekstopmaak: " + message); checks.Add(message); }
        using var db = new Database(true, true); using var working = new WorkingDatabaseScope(db);
        var defaults = new Settings();
        Check(defaults.DirectionWidth(.45,false)==4.8 && defaults.DirectionWidth(.45,true)==6.8 && defaults.DirectionWidth(.75,false)==7.7 && defaults.DirectionWidth(.75,true)==10.7, "standaardbreedtes 4,80/6,80 en 7,70/10,70");
        Check(defaults.DirectionTextMask && defaults.DirectionTextMaskDrawingColor && defaults.DirectionTextMaskBorder==1.3, "masker standaard aan, rand 1,3 en tekeningachtergrond");
        var ids = new List<ObjectId>(); var row = 0;
        foreach (var height in new[] { .45, .75 }) foreach (var ov in new[] { false, true })
        {
            var tool = DirectionText.Template(catalog, ov); var settings = new Settings { DirectionTextHeight = height };
            var first = BlockInserter.Import(db, catalog, tool, settings);
            Check(BlockInserter.Import(db, catalog, tool, settings)==first, "gelijke opmaak hergebruikt eigen definitie");
            foreach (var mode in new[] { "default", "custom", "off" })
            {
                if (mode=="custom") { settings.DirectionTextWidth045=settings.DirectionTextWidth075=8.30; settings.ExistingDirectionTextWidth045=settings.ExistingDirectionTextWidth075=11.90; settings.DirectionTextMaskBorder=1.65; settings.DirectionTextMaskDrawingColor=false; settings.DirectionTextMaskRgb=0x14283C; }
                if (mode=="off") settings.DirectionTextMask=false;
                var def = BlockInserter.Import(db, catalog, tool, settings);
                using var tx = db.TransactionManager.StartTransaction();
                var record = (BlockTableRecord)tx.GetObject(def, OpenMode.ForRead);
                var texts = record.Cast<ObjectId>().Select(id => tx.GetObject(id, OpenMode.ForRead)).OfType<MText>().ToArray();
                Check(texts.Length==13 && texts.All(t => Math.Abs(t.Width-settings.DirectionWidth(height, ExistingCableColors.Matches(t.Layer)))<1e-8), $"{height} {(ov ? "OV" : "LS")} {mode}: definitie heeft gewone/bestaande breedte");
                Check(texts.All(t => t.BackgroundFill==settings.DirectionTextMask && (!t.BackgroundFill || Math.Abs(t.BackgroundScaleFactor-settings.DirectionTextMaskBorder)<1e-8 && t.UseBackgroundColor==settings.DirectionTextMaskDrawingColor)), mode+": masker/rand/achtergrond in brondefinitie");
                if (mode=="custom") Check(texts.All(t => t.BackgroundFillColor.ColorValue.ToArgb()==Color.FromArgb(20,40,60).ToArgb()), "eigen RGB-maskerkleur 20,40,60");
                using var block = BlockInserter.CreateReference(db, tx, def, tool, new Point3d(row++*20,0,0),db.Clayer,Matrix3d.Identity);
                var inserted = BlockInserter.Append(db,tx,block,false,burst:true); ids.AddRange(inserted);
                Check(inserted.All(id => tx.GetObject(id,OpenMode.ForRead) is MText t && Math.Abs(t.Width-settings.DirectionWidth(height,ExistingCableColors.Matches(t.Layer)))<1e-8 && t.BackgroundFill==settings.DirectionTextMask), mode+": burst bewaart breedte en masker");
                if(mode!="default")
                {
                    var original=(BlockTableRecord)tx.GetObject(first,OpenMode.ForRead);
                    Check(def!=first && original.Cast<ObjectId>().Select(id=>tx.GetObject(id,OpenMode.ForRead)).OfType<MText>().All(t=>t.BackgroundFill && t.UseBackgroundColor && Math.Abs(t.Width-defaults.DirectionWidth(height,ExistingCableColors.Matches(t.Layer)))<1e-8), "nieuwe opmaakvariant beschermt eerdere definitie");
                }
                tx.Commit();
            }
        }
        var manual=ids[0];
        using(var tx=db.TransactionManager.StartTransaction())
        {
            var text=(MText)tx.GetObject(manual,OpenMode.ForWrite); text.Width=12.34; text.BackgroundFill=true; text.BackgroundScaleFactor=2.1; text.UseBackgroundColor=false;
            text.BackgroundFillColor=Autodesk.AutoCAD.Colors.Color.FromRgb(120,30,40);tx.Commit();
        }
        var copy=DirectionText.Apply(db,new[]{manual},true,new Point3d(0,50,0),new Point3d(10,50,0),Matrix3d.Identity);
        DirectionText.Apply(db,new[]{manual},false,new Point3d(0,60,0),new Point3d(10,60,0),Matrix3d.Identity);
        using(var tx=db.TransactionManager.StartTransaction())
            Check(new[]{manual,copy[0]}.All(id => tx.GetObject(id,OpenMode.ForRead) is MText t && Math.Abs(t.Width-12.34)<1e-8 && t.BackgroundFill && Math.Abs(t.BackgroundScaleFactor-2.1)<1e-8 && !t.UseBackgroundColor && t.BackgroundFillColor.ColorValue.ToArgb()==Color.FromArgb(120,30,40).ToArgb()), "handmatige breedte/masker/RGB behouden bij kopie en verplaatsen: " + string.Join("; ",new[]{manual,copy[0]}.Select(id => {var t=(MText)tx.GetObject(id,OpenMode.ForRead);return $"w={t.Width} fill={t.BackgroundFill} border={t.BackgroundScaleFactor} drawing={t.UseBackgroundColor} rgb={t.BackgroundFillColor.ColorValue}";})));
        var oldFile=Environment.GetEnvironmentVariable("TECHBASE_SETTINGS_FILE"); var saved=JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(Plugin.Preferences))!;
        void Restore()
        {
            var p=Plugin.Preferences;p.DirectionTextHeight=saved.DirectionTextHeight;p.DirectionTextWidth045=saved.DirectionTextWidth045;p.DirectionTextWidth075=saved.DirectionTextWidth075;p.ExistingDirectionTextWidth045=saved.ExistingDirectionTextWidth045;p.ExistingDirectionTextWidth075=saved.ExistingDirectionTextWidth075;
            p.DirectionTextMask=saved.DirectionTextMask;p.DirectionTextMaskBorder=saved.DirectionTextMaskBorder;p.DirectionTextMaskDrawingColor=saved.DirectionTextMaskDrawingColor;p.DirectionTextMaskRgb=saved.DirectionTextMaskRgb;
        }
        try
        {
            var file=Path.Combine(output,"settings.json");Environment.SetEnvironmentVariable("TECHBASE_SETTINGS_FILE",file);File.WriteAllText(file,"{}");
            var old=Settings.Read();Check(old.DirectionWidth(.75,false)==7.7 && old.DirectionWidth(.75,true)==10.7 && old.DirectionTextMask, "oude instellingen krijgen nieuwe defaults");
            File.WriteAllText(file,"{\"DirectionTextWidth075\":-2,\"ExistingDirectionTextWidth045\":0,\"DirectionTextMaskBorder\":9,\"DirectionTextMaskRgb\":-1}");
            var fixedSettings=Settings.Read();Check(fixedSettings.DirectionTextWidth075==7.7 && fixedSettings.ExistingDirectionTextWidth045==6.8 && fixedSettings.DirectionTextMaskBorder==1.3 && fixedSettings.DirectionTextMaskRgb==0xFFFFFF, "ongeldige breedte/rand/kleur hersteld");
            var p=Plugin.Preferences;p.DirectionTextHeight=.45;p.DirectionTextWidth045=4.8;p.DirectionTextWidth075=7.7;p.ExistingDirectionTextWidth045=6.8;p.ExistingDirectionTextWidth075=10.7;p.DirectionTextMask=true;p.DirectionTextMaskBorder=1.3;p.DirectionTextMaskDrawingColor=true;
            using var panel=new SettingsPanel {Size=new Size(380,1050)}; panel.CreateControl();
            Control Find(string name) { IEnumerable<Control> All(Control c) {foreach(Control child in c.Controls){yield return child;foreach(var item in All(child))yield return item;}} return All(panel).Single(c=>c.Name==name); }
            var height=(ComboBox)Find("DirectionTextHeight");var width=(NumericUpDown)Find("DirectionTextWidth");var existing=(NumericUpDown)Find("ExistingDirectionTextWidth");
            width.Value=5.25m;existing.Value=8.90m;height.SelectedIndex=1;
            Check(width.Value==7.70m && existing.Value==10.70m, "hoogtewissel toont breedtes van 0,75");
            width.Value=8.15m;existing.Value=11.95m;height.SelectedIndex=0;
            Check(width.Value==5.25m && existing.Value==8.90m, "terug naar 0,45 bewaart eigen breedtes");
            ((NumericUpDown)Find("DirectionTextMaskBorder")).Value=1.8m;((CheckBox)Find("DirectionTextMaskDrawingColor")).Checked=false;
            Check(Find("DirectionTextMaskColor").Enabled,"eigen kleurkeuze beschikbaar");
            ((CheckBox)Find("DirectionTextMask")).Checked=false;
            var read=Settings.Read();Check(!read.DirectionTextMask && !read.DirectionTextMaskDrawingColor && read.DirectionTextMaskBorder==1.8 && read.DirectionTextWidth045==5.25 && read.ExistingDirectionTextWidth075==11.95, "UI bewaart beide breedteprofielen en maskeropties");
            ((CheckBox)Find("DirectionTextMask")).Checked=true;
            panel.PerformLayout();using var image=new Bitmap(panel.Width,panel.Height);panel.DrawToBitmap(image,new Rectangle(Point.Empty,panel.Size));image.Save(Path.Combine(output,"Instellingen.png"));
        }
        finally {Restore();Environment.SetEnvironmentVariable("TECHBASE_SETTINGS_FILE",oldFile);}
        db.SaveAs(Path.Combine(output,"LS OV opmaak.dwg"),DwgVersion.AC1032);db.DxfOut(Path.Combine(output,"LS OV opmaak.dxf"),16,DwgVersion.AC1032);
        File.WriteAllText(Path.Combine(output,"opmaak.json"),JsonSerializer.Serialize(new{Status="PASS",Checks=checks},new JsonSerializerOptions{WriteIndented=true}));
    }
}
