using System.Drawing;
using System.IO;
using System.Text.Json;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Techbase.Blocks;

internal static class DirectionDefaultsDiagnostics
{
    internal static void Run(Catalog catalog,string parent)
    {
        var output=Path.Combine(parent,"Richtingstandaarden");Directory.CreateDirectory(output);var checks=new List<string>();
        void Check(bool ok,string name){if(!ok)throw new InvalidOperationException("Richtingstandaarden: "+name);checks.Add(name);}
        var p=Plugin.Preferences;var saved=(p.DirectionLsText,p.DirectionExistingLsText,p.DirectionOvText,p.DirectionExistingOvText);var oldFile=Environment.GetEnvironmentVariable("TECHBASE_SETTINGS_FILE");
        try
        {
            Environment.SetEnvironmentVariable("TECHBASE_SETTINGS_FILE",Path.Combine(output,"settings.json"));
            p.DirectionLsText="150AL / ????-00";p.DirectionExistingLsText="95AL / (was ????-00)";p.DirectionOvText="6CU / ????-00";p.DirectionExistingOvText="6CU / (was ????-00)";
            using var panel=new DirectionPanel {Size=new Size(380,1250)};panel.CreateControl();
            IEnumerable<Control> All(Control control){foreach(Control child in control.Controls){yield return child;foreach(var item in All(child))yield return item;}}
            var boxes=All(panel).OfType<TextBox>().ToArray();
            Check(boxes.Length==4 && boxes.Single(t=>t.Name=="DefaultExistingLsText").Text=="95AL / (was ????-00)","vier velden zichtbaar; LS bestaand standaard 95AL");
            using var db=new Database(true,true);using var working=new WorkingDatabaseScope(db);
            var oldDefs=new[]{false,true}.Select(ov=>BlockInserter.Import(db,catalog,DirectionText.Template(catalog,ov))).ToArray();
            boxes.Single(t=>t.Name=="DefaultLsText").Text="185AL / eigen LS";
            boxes.Single(t=>t.Name=="DefaultExistingLsText").Text="95CU / eigen bestaand LS";
            boxes.Single(t=>t.Name=="DefaultOvText").Text="10CU / eigen OV";
            boxes.Single(t=>t.Name=="DefaultExistingOvText").Text="16CU / eigen bestaand OV";
            var reloaded=Settings.Read();
            Check(reloaded.DirectionLsText=="185AL / eigen LS" && reloaded.DirectionExistingLsText=="95CU / eigen bestaand LS" && reloaded.DirectionOvText=="10CU / eigen OV" && reloaded.DirectionExistingOvText=="16CU / eigen bestaand OV","alle vier getypte teksten onmiddellijk opgeslagen");
            using(var reopen=new DirectionPanel())Check(All(reopen).OfType<TextBox>().Select(t=>t.Text).SequenceEqual(boxes.Select(t=>t.Text)),"nieuwe tab toont bewaarde teksten");
            var row=0;
            foreach(var ov in new[]{false,true})
            {
                var id=BlockInserter.Import(db,catalog,DirectionText.Template(catalog,ov));
                Check(id!=oldDefs[ov?1:0],"tekstwijziging gebruikt nieuwe bronvariant");
                using var tx=db.TransactionManager.StartTransaction();
                var old=(BlockTableRecord)tx.GetObject(oldDefs[ov?1:0],OpenMode.ForRead);
                Check(old.Cast<ObjectId>().Select(i=>tx.GetObject(i,OpenMode.ForRead)).OfType<MText>().All(t=>t.Text==new Settings().DirectionLabel(ov,ExistingCableColors.Matches(t.Layer))),"oude definitie en eerdere inhoud behouden");
                var tool=DirectionText.Template(catalog,ov);using var block=BlockInserter.CreateReference(db,tx,id,tool,new Point3d(row++*30,0,0),db.Clayer,Matrix3d.Identity);
                var ids=BlockInserter.Append(db,tx,block,false,burst:true);
                Check(ids.Count==13 && ids.Select(i=>(MText)tx.GetObject(i,OpenMode.ForRead)).All(t=>t.Text==p.DirectionLabel(ov,ExistingCableColors.Matches(t.Layer))),"13 burstteksten gebruiken gewone/bestaande standaard");
                var hits=new[]{new DirectionCrossing(1,ObjectId.Null,BlockInserter.DefaultLayer(db,tx,"Aansluiting LS K01"),Point3d.Origin,Vector3d.XAxis,0),new DirectionCrossing(0,ObjectId.Null,BlockInserter.DefaultLayer(db,tx,"01 - Bestaande kabel"),new Point3d(0,2,0),Vector3d.XAxis,2)};
                var texts=AutomaticDirectionText.Create(db,hits,ov,p.DirectionTextHeight,tx);
                try{Check(texts[0].Text==p.DirectionLabel(ov,false) && texts[1].Text==p.DirectionLabel(ov,true),"automatische LS/OV-stapel gebruikt dezelfde bewaarde teksten");}
                finally{foreach(var t in texts)t.Dispose();}tx.Commit();
            }
            boxes.Single(t=>t.Name=="DefaultLsText").Text="";Check(Settings.Read().DirectionLsText=="","bewust lege standaardtekst blijft bewaard");
            using(var text=new MText {Contents=AutomaticDirectionText.Contents(@"Pad \code {eigen}")})Check(text.Text==@"Pad \code {eigen}","backslash en accolades blijven letterlijke standaardtekst");
            db.SaveAs(Path.Combine(output,"Standaardteksten.dwg"),DwgVersion.AC1032);db.DxfOut(Path.Combine(output,"Standaardteksten.dxf"),16,DwgVersion.AC1032);
            panel.PerformLayout();using var image=new Bitmap(panel.Width,panel.Height);panel.DrawToBitmap(image,new Rectangle(Point.Empty,panel.Size));image.Save(Path.Combine(output,"Richtingen.png"));
            File.WriteAllText(Path.Combine(output,"defaults.json"),JsonSerializer.Serialize(new{Status="PASS",Checks=checks},new JsonSerializerOptions{WriteIndented=true}));
        }
        finally{(p.DirectionLsText,p.DirectionExistingLsText,p.DirectionOvText,p.DirectionExistingOvText)=saved;Environment.SetEnvironmentVariable("TECHBASE_SETTINGS_FILE",oldFile);}
    }
}
