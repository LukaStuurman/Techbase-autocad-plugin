using System.IO;
using System.Text.Json;

namespace Techbase.Blocks;

public sealed partial class Plugin
{
    internal static void CheckSettingsPersistence(string parent)
    {
        var output = Path.Combine(parent,"Instellingen"); Directory.CreateDirectory(output);
        var checks = new List<string>();
        void Check(bool ok,string message) { if(!ok) throw new InvalidOperationException("Instellingen: "+message); checks.Add(message); }
        var saved = Preferences; var oldBlocks = Blocks; var oldFile = Environment.GetEnvironmentVariable("TECHBASE_SETTINGS_FILE");
        try
        {
            var file=Path.Combine(output,"persistent.json");Environment.SetEnvironmentVariable("TECHBASE_SETTINGS_FILE",file);
            Preferences = new Settings { InheritLayer=false, UseChosenBlockLayer=true, ChosenBlockLayer=10, Repeat=true,PromptBlockText=true,Tolerance=.123,SelectedDirectionNumber=12,
                MofTextHeight=.75,DirectionTextHeight=.75,DirectionTextWidth045=5.21,ExistingDirectionTextWidth045=8.12,DirectionTextWidth075=8.31,ExistingDirectionTextWidth075=11.41,
                DirectionTextMask=false,DirectionTextMaskBorder=1.8,DirectionTextMaskDrawingColor=false,DirectionTextMaskRgb=0x123456,ExistingCableBlack=true,KabelgeulTextFormat=1,KabelgeulMantelSpec="Eigen mantel",
                DirectionLsText="Eigen LS", DirectionExistingLsText="Eigen bestaande LS", DirectionOvText="Eigen OV", DirectionExistingOvText="Eigen bestaande OV" };
            Preferences.KabelgeulDefaults["K10"]=new KabelgeulRow("K10","Eigen tekst",TwoSides:true,Text2:"",Shape:"Drie");
            Preferences.BlockRotationModes["eigen-knop"]=BlockRotationMode.BeforePoint; Preferences.Save();
            var json=File.ReadAllText(file);
            Check(JsonSerializer.Serialize(Settings.Read())==JsonSerializer.Serialize(Preferences),"alle instellingen, standaardteksten, lege tekst en draai-instellingen herladen zonder verlies");
            using(var panel=new SettingsPanel())
            {
                IEnumerable<Control> All(Control control) {foreach(Control c in control.Controls){yield return c;foreach(var child in All(c)) yield return child;}}
                var width=(NumericUpDown)All(panel).Single(c=>c.Name=="DirectionTextWidth");width.Text=9.25m.ToString();
                var prompt=(CheckBox)All(panel).Single(c=>c.Name=="PromptBlockText");
                Check(prompt.Checked, "tekstvragen-keuze zichtbaar na heropenen");
                prompt.Checked=false;
                Check(!Settings.Read().PromptBlockText, "tekstvragen uit direct opgeslagen");
                prompt.Checked=true;
                Check(Settings.Read().PromptBlockText, "tekstvragen aan direct opgeslagen");
                Check(Settings.Read().DirectionTextWidth075==9.25,"getypte breedte onmiddellijk opgeslagen zonder het veld te verlaten");
                ((CheckBox)All(panel).Single(c=>c.Name=="DirectionTextMask")).Checked=true;
                Check(Settings.Read().DirectionTextWidth075==9.25,"andere instelling wijzigen overschrijft de nog getypte breedte niet");
                var reset=(Button)All(panel).Single(c=>c.Name=="ResetPluginSettings");reset.PerformClick();
                var restored=Settings.Read();
                Check(!restored.PromptBlockText && !prompt.Checked, "reset herstelt tekstvragen standaard uit");
                Check(restored.ExistingDirectionTextWidth045==6.8 && restored.DirectionTextWidth075==7.7 && restored.ExistingDirectionTextWidth075==10.7,"resetknop herstelt nieuwe standaardbreedtes");
                Check(restored.DirectionLsText=="150AL / ????-00" && restored.DirectionExistingLsText=="95AL / (was ????-00)" && restored.DirectionOvText=="6CU / ????-00" && restored.DirectionExistingOvText=="6CU / (was ????-00)","reset herstelt vier richtingsteksten met 95AL bestaand LS");
                Check(restored.MofTextHeight==.45 && restored.DirectionTextHeight==.45 && restored.DirectionTextMask && restored.DirectionTextMaskDrawingColor && restored.DirectionTextMaskBorder==1.3,"reset herstelt hoogtes en masker");
                Check(restored.InheritLayer && !restored.UseChosenBlockLayer && !restored.Repeat && restored.ChosenBlockLayer==1 && restored.SelectedDirectionNumber==1 && restored.Tolerance==.05 && !restored.ExistingCableBlack,"reset herstelt laagkeuze, richting, kleur en plaatsopties");
                Check(restored.KabelgeulDefaults.Count==0 && restored.KabelgeulMantelSpec=="" && restored.BlockRotationModes.Count==0 && restored.PalettePath==null,"reset herstelt Kabelgeul- en blockstandaarden");
                var backup=Directory.GetFiles(output,"persistent.json.before-reset-*.json").OrderBy(File.GetLastWriteTimeUtc).Last();
                var previous=JsonSerializer.Deserialize<Settings>(File.ReadAllText(backup))!;
                Check(previous.DirectionTextWidth075==9.25 && previous.KabelgeulDefaults["K10"].Text=="Eigen tekst" && previous.ExistingCableBlack,"reset bewaart vorige aangepaste instellingen als kopie");
                Check(((NumericUpDown)All(panel).Single(c=>c.Name=="ExistingDirectionTextWidth")).Value==6.8m,"reset toont direct de standaardbreedte in het scherm");
            }
            using(var geul=new KabelgeulPanel())
            {
                Check(Convert.ToString(geul.Editor.Grid.Rows[9].Cells[1].Value)=="Ri 10","opnieuw geopende Kabelgeul-tab toont resetstandaard");
                Preferences.KabelgeulDefaults["K10"]=new KabelgeulRow("K10","nieuw bewaard");geul.RefreshSettings();
                Check(Convert.ToString(geul.Editor.Grid.Rows[9].Cells[1].Value)=="nieuw bewaard","bestaande Kabelgeul-tab vernieuwt zonder oude regels terug te schrijven");
            }
            var snapshot=Preferences;var fail=Path.Combine(output,"cannot-write");Directory.CreateDirectory(fail);Environment.SetEnvironmentVariable("TECHBASE_SETTINGS_FILE",fail);
            var rejected=false;try{ResetSettings();}catch(IOException){rejected=true;}catch(UnauthorizedAccessException){rejected=true;}
            Check(rejected && ReferenceEquals(snapshot,Preferences),"mislukte reset behoudt actieve instellingen");
            File.WriteAllText(Path.Combine(output,"persistent.before.json"),json);
        }
        finally {Preferences=saved;Blocks=oldBlocks;Environment.SetEnvironmentVariable("TECHBASE_SETTINGS_FILE",oldFile);}
        File.WriteAllText(Path.Combine(output,"persistent-checks.json"),JsonSerializer.Serialize(new{Status="PASS",Checks=checks},new JsonSerializerOptions{WriteIndented=true}));
    }
}
