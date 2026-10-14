"""Place on an existing cable, change parent layer, copy and repair old attributes."""
import argparse, json, os, subprocess, xml.etree.ElementTree as ET
from package_paths import ROOT, ASSETS, dll
from dxf_records import records, value

parser = argparse.ArgumentParser()
parser.add_argument('--autoload', action='store_true')
args = parser.parse_args()
out = ROOT/'validation/Attribuutlagen'
out.mkdir(exist_ok=True)
tools = ET.parse(ASSETS/'Techbase toolpallete.xtp').findall('.//Palette/Tools/Tool')
tool = next(t for t in tools if t.findtext('Data/Block/BlockName') == 'NIEUWE MOF')
code = tool.find('ItemID').attrib['idValue'].strip('{}').replace('-','').lower()
settings = out/'command-settings.json'
settings.write_text(json.dumps({'InheritLayer':True,'BlockRotationModes':{code:'None'}}),encoding='utf-8')
result_dxf = out/'Laagwisseling commando.dxf'
result_dxf.unlink(missing_ok=True)
lines = ([] if args.autoload else ['_NETLOAD',f'"{dll().as_posix()}"']) + [
    '(setvar "OSMODE" 0)', '(setvar "INSUNITS" 0)', '(setvar "PICKFIRST" 1)',
    '(command "_-LAYER" "_New" "01 - Bestaande kabel" "_Color" "125" "01 - Bestaande kabel" "_New" "Aansluiting LS K06" "_Color" "30" "Aansluiting LS K06" "")',
    '(entmake (list (cons 0 "LINE") (cons 8 "01 - Bestaande kabel") (cons 10 (list 0.0 0.0 0.0)) (cons 11 (list 20.0 0.0 0.0))))',
    '_TBPLAATS', code, '5,0,0',
    '(setq original (entlast))',
    '(princ (strcat "\\nINITIAL_LAYER=" (cdr (assoc 8 (entget original)))))',
    '(command "_CHPROP" original "" "_LA" "Aansluiting LS K06" "")',
    '(command "_COPY" original "" "_Multiple" "0,0,0" "10,0,0" "")',
    '_TBPLAATS', code, '8,0,0',
    '(setq old (entlast) attribute (entnext old) data (entget attribute))',
    '(entmod (subst (cons 8 "01 - Bestaande kabel") (assoc 8 data) (subst (cons 1 "Eigen EM tekst") (assoc 1 data) data)))',
    '(command "_CHPROP" old "" "_LA" "Aansluiting LS K06" "")',
    '(sssetfirst nil (ssadd old))', '_TBBLOCKTEKSTLAAG',
    '(sssetfirst nil (ssadd old))', '_TBBLOCKTEKSTLAAG',
    '_DXFOUT', f'"{result_dxf.as_posix()}"', '16', '_QUIT', '_Y']
script = out/'commands.scr'
script.write_text('\n'.join(lines)+'\n',encoding='ascii')
raw = out/'commands.raw.log'
with raw.open('wb') as log:
    subprocess.run([r'C:\Program Files\Autodesk\AutoCAD 2026\accoreconsole.exe','/s',str(script),'/l','en-US'],
                   cwd=out,env=dict(os.environ,TECHBASE_SETTINGS_FILE=str(settings)),stdout=log,stderr=subprocess.STDOUT,check=True,timeout=120)
log = raw.read_bytes().decode('utf-16-le',errors='replace').replace('\x00','')
(out/'commands.log').write_text('\n'.join(l.rstrip() for l in log.splitlines()).rstrip()+'\n',encoding='utf-8')
assert 'INITIAL_LAYER=01 - Bestaande kabel' in log,log[-5000:]
assert 'Blocktekstlaag: 1 attributen hersteld' in log and 'Blocktekstlaag: 0 attributen hersteld' in log,log[-5000:]
data = records(result_dxf)
insertions = [r for r in data if r[0] == (0,'INSERT') and value(r,2,'').startswith('TB_NIEUWE MOF')]
attrs = [r for r in data if r[0] == (0,'ATTRIB')]
assert len(insertions) == len(attrs) == 3
assert all(value(r,8) == 'Aansluiting LS K06' for r in insertions)
assert all(value(a,8) == '0' and int(value(a,62,'256')) == 256 for a in attrs)
layer = next(r for r in data if r[0]==(0,'LAYER') and value(r,2)=='Aansluiting LS K06')
assert int(value(layer,62)) == 30
assert sorted(value(a,1) for a in attrs)==['EM 150Al (was ????-??)','EM 150Al (was ????-??)','Eigen EM tekst']
checks = ['geplaatst op bestaande kabel met laagovername','CHPROP naar richting 6 zonder ATTSYNC',
          'kopie behoudt laagovername','drie opgeslagen blocks op richting 6','attribuutlaag 0 en ByLayer: effectieve kleur 30',
          'oude vastgezette tekstlaag hersteld','eigen tekstwaarde behouden','herhaald herstel wijzigt niets']
(out/'commands.json').write_text(json.dumps({'Status':'PASS','Checks':checks,'Autoload':args.autoload},indent=2),encoding='utf-8')
print(json.dumps({'Status':'PASS','Checks':len(checks),'Autoload':args.autoload}))
