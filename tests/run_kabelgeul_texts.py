"""Complete labels, explicit empty text and unchanged repeated copy/edit under conflicting defaults."""
import argparse, json, os, subprocess
from package_paths import ROOT, BUNDLE, dll
parser=argparse.ArgumentParser(); parser.add_argument('--autoload',action='store_true');args=parser.parse_args()
out=ROOT/'validation/Kabelgeul';out.mkdir(exist_ok=True)
rows=[{'Type':'K01','Text':'Ri 1'},
      {'Type':'K10','Text':'Eigen tekst','TwoSides':True,'Text2':'Ri 88'},
      {'Type':'K12','Text':'','TwoSides':True,'Text2':'Zijde 2 zonder Ri'},
      {'Type':'Rooien','Text':'Geen automatisch prefix'},
      {'Type':'Half rooien','Text':'Zelf ingevuld','Text2':'Zelf behouden'},
      {'Type':'Bestaand','Text':''}, {'Type':'MS Nieuw','Text':'MS eigen tekst'}]
config=out/'complete-text-input.json';config.write_text(json.dumps({'Rows':rows}),encoding='utf-8')
settings=out/'complete-text-settings.json';settings.write_text(json.dumps({'KabelgeulTextFormat':1,'KabelgeulDefaults':{r['Type']:{'Type':r['Type'],'Text':'NIEUWE STANDAARD','Text2':'NIEUWE ZIJDE 2'} for r in rows}}),encoding='utf-8')
installed=__import__('pathlib').Path(os.environ['APPDATA'])/'Autodesk/ApplicationPlugins/Techbase.Blocks.bundle'
bundle=installed if args.autoload else BUNDLE
lines=([] if args.autoload else ['_NETLOAD',f'"{dll().as_posix()}"'])+[f'(load "{(bundle/"Contents/Assets/Kabelgeul/Techbase.Kabelgeul.lsp").as_posix()}")',
    '(setvar "OSMODE" 0)','(setvar "ATTREQ" 0)',
    '(entmake (list (cons 0 "LINE") (cons 10 (list 0.0 0.0 0.0)) (cons 11 (list 80.0 0.0 0.0))))',
    '_ZOOM','_E','_REGEN','_TBKABELGEUL','10,0,0','10,8,0','_Direct',
    '_ZOOM','_E','_REGEN','_TBGEULCOPY','10,4,0','30,0,0','30,8,0','_Direct',
    '_ZOOM','_E','_REGEN','_TBGEULCOPY','30,4,0','50,0,0','50,8,0','_Direct',
    '_ZOOM','_E','_REGEN','_TBGEULEDIT','10,4,0','_TBGEULEDIT','10,4,0',
    '_DXFOUT',f'"{(out/"Kabelgeul - volledige tekst.dxf").as_posix()}"','16','_QUIT','_Y']
script=out/'complete-text.scr';script.write_text('\n'.join(lines)+'\n',encoding='ascii')
raw=out/'complete-text.raw.log'
with raw.open('wb') as log:
    subprocess.run([r'C:\Program Files\Autodesk\AutoCAD 2026\accoreconsole.exe','/s',str(script),'/l','en-US'],cwd=out,
        env=dict(os.environ,TECHBASE_SETTINGS_FILE=str(settings),TECHBASE_KABELGEUL_TEST_CONFIG=str(config)),stdout=log,stderr=subprocess.STDOUT,check=True,timeout=120)
text=raw.read_bytes().decode('utf-16-le',errors='replace').replace('\x00','')
(out/'complete-text.log').write_text('\n'.join(l.rstrip() for l in text.splitlines()).rstrip()+'\n',encoding='utf-8')
assert text.count('KabelGeul opnieuw aangemaakt met overgenomen gegevens.')==2,text[-9000:]
assert text.count('Kabelgeul volledig bijgewerkt.')==2,text[-9000:]
lines=(out/'Kabelgeul - volledige tekst.dxf').read_text(errors='replace').splitlines()
records=[];rec=[];section=''
for i in range(0,len(lines)-1,2):
    pair=int(lines[i].strip()),lines[i+1].strip()
    if pair[0]==2 and rec==[(0,'SECTION')]:section=pair[1]
    if pair[0]==0:
        if rec:
            item=dict(rec);item['_section']=section;records.append(item)
        rec=[]
        if pair[1]=='ENDSEC':section=''
    rec.append(pair)
geuls=[r for r in records if r.get(0)=='INSERT' and r['_section']=='ENTITIES']; assert len(geuls)==3
metadata={};current=None
for r in records:
    if r['_section']!='BLOCKS':continue
    if r.get(0)=='BLOCK':current=r[2];metadata[current]={}
    elif r.get(0)=='ENDBLK':current=None
    elif current and r.get(0)=='ATTDEF' and r.get(2,'').startswith('TBTYPE_'):metadata[current][r[2][7:]]=r[1]
labels={g[5]:{} for g in geuls}
for r in records:
    if r.get(0)=='ATTRIB' and r.get(2,'').startswith(('KABEL_','ZIJDE2_','BESTAAND_')):labels[r[330]][r[2]]=r[1]
expected={r['Type']:{'Text':r['Text'],**({'Text2':r['Text2']} if r.get('TwoSides') or r['Type']=='Half rooien' else {})} for r in rows}
for g in geuls:
    actual={}
    for tag,value in labels[g[5]].items():
        kind,index=tag.rsplit('_',1);type=metadata[g[2]][index]
        actual.setdefault(type,{})['Text' if kind=='KABEL' else 'Text2']=value
    assert actual==expected,(actual,expected)
checks=['custom labels receive no Ri or Rooien prefix','explicit empty labels stay empty','both sides preserved exactly',
        'unchanged twice edited source with conflicting current defaults','first and repeated copy preserve all final source texts',
        'independently read all three saved geuls']
(out/'complete-text.json').write_text(json.dumps({'Status':'PASS','Checks':checks,'Autoload':args.autoload},indent=2),encoding='utf-8')
print(json.dumps({'Status':'PASS','Checks':len(checks),'Autoload':args.autoload}))
