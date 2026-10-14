"""Real automatic detection/placement, modes, ordering, UCS, cancel and saved text."""
import argparse, json, os, subprocess, sys
from pathlib import Path
from package_paths import ROOT, dll
sys.stdout.reconfigure(encoding='utf-8')
parser=argparse.ArgumentParser(); parser.add_argument('--autoload',action='store_true'); args=parser.parse_args()
out=ROOT/'validation/AutomatischeTeksten';out.mkdir(exist_ok=True)
def line(layer,y):
    return f'(entmake (list (cons 0 "LINE") (cons 8 "{layer}") (cons 10 (list 0.0 {y}.0 0.0)) (cons 11 (list 20.0 {y}.0 0.0))))'
lines=([] if args.autoload else ['_NETLOAD',f'"{dll().as_posix()}"'])+['TBAUTOTEKSTVALIDATE','(setvar "OSMODE" 0)',line('Aansluiting LS K01',0),line('Aansluiting LS K10',2),line('Aansluiting LS K12',4),
    line('Aansluiting LS K10',3),line('01 - Bestaande kabel',5),line('OVERIG',6),
    '(entmake (list (cons 0 "LINE") (cons 8 "0") (cons 10 (list 7.0 0.0 0.0)) (cons 11 (list 7.0 6.0 0.0))))',
    '_ZOOM','_E','_REGEN','_TBAUTOTEKSTLS','5,0,0','5,6,0','',
    '_TBAUTOTEKSTOV','_Lijn','7,1,0','12,8,0',
    '_TBAUTOTEKSTLS','_TweePunten','9,4,0','9,0,0','15,10,0',
    '_U','(princ (strcat "\nUNDO_COUNT=" (itoa (sslength (ssget "_X" (list (cons 0 "MTEXT")))))))',
    '_TBAUTOTEKSTLS','_TweePunten','9,4,0','9,0,0','15,10,0',
    '_UCS','_Origin','100,200,0','_UCS','_Z','90',
    '_TBAUTOTEKSTLS','_TweePunten','-200,91,0','-194,91,0','-185,80,0','_UCS','_W',
    '_TBAUTOTEKSTLS','_TweePunten','2,10,0','2,12,0',
    '_TBAUTOTEKSTLS','5,0,0','5,6,0','\x03(princ)',
    '(princ (strcat "\nCOUNT=" (itoa (sslength (ssget "_X" (list (cons 0 "MTEXT")))))))',
    '(princ (strcat "\nCURRENT=" (getvar "CLAYER")))',
    '_DXFOUT',f'"{(out/"Automatische teksten - commando.dxf").as_posix()}"','16','_QUIT','_Y']
script=out/'auto.scr';script.write_text('\n'.join(l.replace('"\n','"\\n') for l in lines)+'\n',encoding='ascii')
with (out/'auto.raw.log').open('wb') as log:
    result=subprocess.run([r'C:\Program Files\Autodesk\AutoCAD 2026\accoreconsole.exe','/s',str(script),'/l','en-US'],cwd=out,
        env=dict(os.environ,TECHBASE_TEST_OUTPUT=str(out),TECHBASE_SETTINGS_FILE=str(out/'test-settings.json')),stdout=log,stderr=subprocess.STDOUT,timeout=120)
log=(out/'auto.raw.log').read_bytes().decode('utf-16-le',errors='replace').replace('\x00','')
(out/'auto.log').write_text('\n'.join(l.rstrip() for l in log.splitlines()).rstrip()+'\n',encoding='utf-8')
checks=['Herkend in kabelvolgorde: Ri 1, Ri 10, Ri 12, Bestaand','Herkend in kabelvolgorde: Ri 12, Ri 10, Ri 1','3 LS-richtingsteksten geplaatst.',
    '4 LS-richtingsteksten geplaatst.', '4 OV-richtingsteksten geplaatst.','Geen richtingen 1 t/m 12 of bestaande kabels langs de detectielijn gevonden.','Automatische teksten geannuleerd; geen teksten geplaatst.','COUNT=15','CURRENT=0','UNDO_COUNT=8']
missing=[m for m in checks if m not in log]
if result.returncode!=0 or missing: print(log[-11000:]);raise AssertionError((result.returncode,missing))
service=json.loads((out/'service.json').read_text()); assert service['Status']=='PASS'
assert (out/'service.json').stat().st_mtime >= script.stat().st_mtime, 'stale service report'
rows=(out/'Automatische teksten - commando.dxf').read_text(encoding='utf-8',errors='replace').splitlines();records=[];rec=[];section=''
for i in range(0,len(rows)-1,2):
    pair=int(rows[i].strip()),rows[i+1].strip()
    if pair[0]==2 and rec==[(0,'SECTION')]:section=pair[1]
    if pair[0]==0:
        if rec and section=='ENTITIES':records.append(rec)
        rec=[]
        if pair[1]=='ENDSEC':section=''
    rec.append(pair)
texts=[{k:next(v for c,v in r if c==k) for k,_ in r} for r in records if r[0]==(0,'MTEXT')]
assert len(texts)==15,texts
assert [t[8] for t in texts[:3]]==['Aansluiting LS K01','Aansluiting LS K10','Aansluiting LS K12']
assert [t[8] for t in texts[8:11]]==['Aansluiting LS K12','Aansluiting LS K10','Aansluiting LS K01']
assert all(t[1]=='150AL / ????-00' for t in texts[:3]+texts[8:11]+texts[11:14]) and all(t[1]=='6CU / ????-00' for t in texts[4:7])
assert texts[3][1]=='150AL / (was ????-00)' and texts[7][1]=='6CU / (was ????-00)'
assert texts[14][1]=='150AL / (was ????-00)'
assert abs(float(texts[11][10])-20)<1e-8 and abs(float(texts[11][20])-15.4)<1e-8, texts[11]
assert all(abs(float(t[40])-.45)<1e-8 and abs(float(t[41])-(7.8 if t[8]=='01 - Bestaande kabel' else 4.8))<1e-8 and t[7].startswith('TECHBASE_ARIAL_NOVA_LIGHT') for t in texts)
assert all(int(t[90]) & 3 == 3 and abs(float(t[45])-1.3)<1e-8 for t in texts)
assert all(float(t[20])>5 for t in texts[:4]),texts[:4]
assert not any(r[0]==(0,'INSERT') for r in records),'no Kabelgeul blocks'
checks+=['saved crossing order and reversed crossing order','exact LS/OV defaults, height .45, widths 4.80/7.80, background mask 1.3/drawing color and Light font','default stack is above all detected cables','only loose MText; no Kabelgeul blocks']
checks+=['actual shifted/rotated UCS point input and jig world placement']
(out/'commands.json').write_text(json.dumps({'Status':'PASS','Checks':checks,'ServiceChecks':len(service['Checks']),'Autoload':args.autoload},indent=2),encoding='utf-8')
print(json.dumps({'Status':'PASS','Checks':len(checks),'ServiceChecks':len(service['Checks']),'Autoload':args.autoload}))
