"""Apply saved defaults to actually detected cables and inspect their saved CAD."""
import json
import os
from pathlib import Path
import subprocess
from package_paths import ROOT, BUNDLE, dll

out=ROOT/'validation/Kabelgeul'
settings=out/'placement-defaults.json'
settings.write_text(json.dumps({'KabelgeulDefaults':{
    'K10':{'Type':'K10','Text':'150Al preset','Shape':'Drie','Mantel':'X'},
    'K01':{'Type':'K01','Text':'185Al preset','Shape':'Combi','TwoSides':True,'Text2':'16Cu preset','Shape2':'Dubbel','NewTray':True}},
    'KabelgeulMantelSpec':'PE 250 preset'},indent=2),encoding='utf-8')
lines=['_NETLOAD',f'"{dll().as_posix()}"',f'(load "{(BUNDLE/"Contents/Assets/Kabelgeul/Techbase.Kabelgeul.lsp").as_posix()}")',
    '(setvar "OSMODE" 0)', '(setvar "WIPEOUTFRAME" 2)', '(setvar "ATTREQ" 0)',
    '(entmake (list (cons 0 "LINE") (cons 8 "Aansluiting LS K10") (cons 10 (list 0.0 0.0 0.0)) (cons 11 (list 80.0 0.0 0.0))))',
    '(entmake (list (cons 0 "LINE") (cons 8 "Aansluiting LS K01") (cons 10 (list 0.0 2.0 0.0)) (cons 11 (list 80.0 2.0 0.0))))',
    '_ZOOM','_E','_REGEN','_TBKABELGEUL','50,0,0','50,8,0','_Direct',
    '(princ (strcat "\nDETECTED=" (vl-prin1-to-string *tbkg:last-types*)))',
    '_DXFOUT',f'"{(out/"Kabelgeul - defaults.dxf").as_posix()}"','16','_QUIT','_Y']
script=out/'defaults.scr'; script.write_text('\n'.join(l.replace('"\n','"\\n') for l in lines)+'\n',encoding='ascii')
raw=out/'defaults.raw.log'
with raw.open('wb') as log:
    result=subprocess.run([r'C:\Program Files\Autodesk\AutoCAD 2026\accoreconsole.exe','/s',str(script),'/l','en-US'],cwd=out,
        env=dict(os.environ,TECHBASE_SETTINGS_FILE=str(settings),TECHBASE_KABELGEUL_TEST_CONFIG='DEFAULTS'),stdout=log,stderr=subprocess.STDOUT,timeout=120)
log=raw.read_bytes().decode('utf-16-le',errors='replace').replace('\x00','')
(out/'defaults.log').write_text('\n'.join(l.rstrip() for l in log.splitlines()).rstrip()+'\n',encoding='utf-8')
assert result.returncode==0 and 'KabelGeul geplaatst als uniek block.' in log,log[-6000:]
rows=(out/'Kabelgeul - defaults.dxf').read_text(encoding='utf-8',errors='replace').splitlines()
records=[]; record=[]
for i in range(0,len(rows)-1,2):
    pair=int(rows[i].strip()),rows[i+1].strip()
    if pair[0]==0:
        if record: records.append(dict(record))
        record=[]
    record.append(pair)
checks=[]
attributes=[r for r in records if r.get(0)=='ATTRIB']
for text in ['Ri 10 150Al preset','Ri 1 185Al preset','Ri 1 16Cu preset','PE 250 preset']:
    assert any(r.get(1)==text for r in attributes),text
    checks.append('Saved default text: '+text)
metadata={r[2]:r[1] for r in records if r.get(0)=='ATTDEF'}
for type,shape in [('K10','DRIE'),('K01','COMBI')]:
    index=next(k.split('_')[-1] for k,v in metadata.items() if k.startswith('TBTYPE_') and v==type)
    assert metadata[shape+'_'+index]=='1',metadata
    checks.append('Saved default shape: '+type+' '+shape)
k01=next(k.split('_')[-1] for k,v in metadata.items() if k.startswith('TBTYPE_') and v=='K01')
assert metadata['TWEEZIJDIG_'+k01]=='1' and metadata['DUBBEL2_'+k01]=='1' and metadata['VOLG_'+k01]=='1'
checks.append('Saved default two sides, side 2 shape and new tray')
assert len([r for r in records if r.get(0)=='CIRCLE' and r.get(8)=='Aansluiting LS K10'])==3
checks.append('Saved K10 icon uses three circles')
(out/'defaults.json').write_text(json.dumps({'Status':'PASS','Checks':checks},indent=2),encoding='utf-8')
print(json.dumps({'Status':'PASS','Checks':len(checks)}))
