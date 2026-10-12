"""Exercise the bundled Omni module in AutoCAD, then inspect the saved drawing."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
from package_paths import ROOT, BUNDLE, dll

parser = argparse.ArgumentParser()
parser.add_argument('--autoload', action='store_true')
args = parser.parse_args()
sys.stdout.reconfigure(encoding='utf-8')
out = ROOT / 'validation/Kabelgeul'
out.mkdir(exist_ok=True)
config = {'Rows': [
    {'Type': 'K01', 'Text': 'Ri 1 150Al', 'Shape': 'Combi', 'TwoSides': True,
     'Text2': 'Ri 1 150Al+', 'Shape2': 'Dubbel', 'Mantel': 'X'},
    {'Type': 'Bestaand', 'Text': 'Bestaand 95Cu', 'Shape': 'Enkel', 'Mantel': 'A'},
    {'Type': 'Half rooien', 'Text': 'Rooien 50Cu', 'Text2': 'Behouden 50Cu', 'Shape': 'Dubbel', 'NewTray': True},
    {'Type': 'MS Nieuw', 'Text': 'MS 240Al', 'Shape': 'Drie', 'Mantel': 'A'}], 'MantelSpec': 'PVC 160'}
config_path = out / 'input.json'
config_path.write_text(json.dumps(config, indent=2), encoding='utf-8')
installed = Path(os.environ['APPDATA']) / 'Autodesk/ApplicationPlugins/Techbase.Blocks.bundle'
source_bundle = installed if args.autoload else BUNDLE
lines = [] if args.autoload else ['_NETLOAD', f'"{dll().as_posix()}"']
# Core Console does not scan new Lisp bundle components. Use the same load
# expression as the tab; TBGEULINIT then demand-loads the installed .NET DLL.
lines += ['(defun c:KGOMNI () (princ "ORIGINAL_UNCHANGED"))', f'(if (not *tbkg:ready*) (load "{(source_bundle / "Contents/Assets/Kabelgeul/Techbase.Kabelgeul.lsp").as_posix()}"))']
lines += ['(setvar "OSMODE" 0)', '(setvar "WIPEOUTFRAME" 2)', '(setvar "ATTREQ" 0)',
    '(setq oldcodesetting (getenv "TechbaseKabelGeulCodeSwitch"))',
    '_-LAYER', '_New', '"Aansluiting LS K01"', '_Color', '3', '"Aansluiting LS K01"', '',
    '(entmake (list (cons 0 "LINE") (cons 8 "Aansluiting LS K01") (cons 10 (list 0.0 0.0 0.0)) (cons 11 (list 80.0 0.0 0.0))))',
    '_ZOOM', '_E', '_REGEN', '_TBKABELGEUL', '10,0,0', '10,8,0', '_Direct',
    '(setq firstgeul (entlast))',
    '(princ (strcat "\nFRAME_DIRECT=" (itoa (getvar "WIPEOUTFRAME"))))',
    '(princ (strcat "\nSOURCE_DATA=" (vl-prin1-to-string (tbkg:copy-data-from-insert firstgeul))))',
    '(princ (strcat "\nGUARD=" (if (TBKG_NATIVE_GUARD firstgeul) "PASS" "FAIL")))',
    '_ZOOM', '_E', '_REGEN', '_TBGEULCOPY', '10,4,0', '30,0,0', '30,8,0', '_Direct',
    '(princ (strcat "\nFRAME_COPY=" (itoa (getvar "WIPEOUTFRAME"))))',
    '_TBKABELGEUL', '50,0,0', '50,8,0', '_Zwevend', '50,20,0',
    '(princ (strcat "\nFRAME_FLOAT=" (itoa (getvar "WIPEOUTFRAME"))))',
    '_TBGEULCODE', '_TBGEULCODE',
    '(princ (strcat "\nFRAME_CODE=" (itoa (getvar "WIPEOUTFRAME"))))',
    '_TBKABELGEUL', '\x03(princ)',
    '(princ (strcat "\nFRAME_CANCEL=" (itoa (getvar "WIPEOUTFRAME"))))',
    '_TBGEULEDIT', '\x03(princ)', '(princ (strcat "\nFRAME_EDIT_CANCEL=" (itoa (getvar "WIPEOUTFRAME"))))',
    '_-LAYER', '_Lock', '"E_LV_CROSS_SECTION"', '', '_TBKABELGEUL', '65,0,0', '65,8,0',
    '(princ (strcat "\nFRAME_LOCK=" (itoa (getvar "WIPEOUTFRAME"))))',
    '(princ (strcat "\nINSERTS_LOCK=" (itoa (sslength (ssget "_X" (list (cons 0 "INSERT")))))))',
    '_-LAYER', '_Unlock', '"E_LV_CROSS_SECTION"', '',
    '(princ (strcat "\nLAYER_COLOR=" (itoa (cdr (assoc 62 (tblsearch "LAYER" "Aansluiting LS K01"))))))',
    '(princ (strcat "\nCURRENT=" (getvar "CLAYER") " ATTREQ=" (itoa (getvar "ATTREQ")) " CMDECHO=" (itoa (getvar "CMDECHO"))))',
    '(c:KGOMNI)', '(if oldcodesetting (setenv "TechbaseKabelGeulCodeSwitch" oldcodesetting) (setenv "TechbaseKabelGeulCodeSwitch" "0"))',
    '_DXFOUT', f'"{(out / "Kabelgeul - commando.dxf").as_posix()}"', '16', '_QUIT', '_Y']
script = out / 'commands.scr'
script.write_text('\n'.join(l.replace('"\n', '"\\n') for l in lines) + '\n', encoding='ascii')
raw = out / 'commands.raw.log'
with raw.open('wb') as log:
    process = subprocess.run([r'C:\Program Files\Autodesk\AutoCAD 2026\accoreconsole.exe', '/s', str(script), '/l', 'en-US'],
        cwd=out, env=dict(os.environ, TECHBASE_KABELGEUL_TEST_CONFIG=str(config_path)), stdout=log, stderr=subprocess.STDOUT, timeout=120)
log = raw.read_bytes().decode('utf-16-le', errors='replace').replace('\x00', '')
(out / 'commands.log').write_text('\n'.join(l.rstrip() for l in log.splitlines()).rstrip() + '\n', encoding='utf-8')
checks = [f'FRAME_{name}=2' for name in ['DIRECT', 'COPY', 'FLOAT', 'CODE', 'CANCEL', 'EDIT_CANCEL', 'LOCK']]
checks += ['GUARD=PASS', 'LAYER_COLOR=3', 'CURRENT=0 ATTREQ=0 CMDECHO=1', 'ORIGINAL_UNCHANGED',
           'KabelGeul opnieuw aangemaakt met overgenomen gegevens.', 'KabelGeul geplaatst als uniek block.']
checks += ['SOURCE_DATA=(4 ("K01" "Bestaand" "Half rooien" "MS Nieuw")']
checks += ["Laag 'E_LV_CROSS_SECTION' is vergrendeld.", 'INSERTS_LOCK=3']
missing = [c for c in checks if c not in log]
if process.returncode != 0 or missing:
    print(log[-12000:]); raise AssertionError((process.returncode, missing))

rows = (out / 'Kabelgeul - commando.dxf').read_text(encoding='utf-8', errors='replace').splitlines()
records, record, section = [], [], ''
for i in range(0, len(rows)-1, 2):
    pair = int(rows[i].strip()), rows[i+1].strip()
    if pair[0] == 2 and record == [(0, 'SECTION')]: section = pair[1]
    if pair[0] == 0:
        if record: records.append((section, record))
        record = []
        if pair[1] == 'ENDSEC': section = ''
    record.append(pair)
entities = [r for s, r in records if s == 'ENTITIES']
inserts = [dict(r) for r in entities if r[0] == (0, 'INSERT')]
geuls = [r for r in inserts if r[2].startswith('KabelGeul_')]
assert len(geuls) == 3 and len({r[2] for r in geuls}) == 3, geuls
checks += ['three unique inserted geuls: Direct, copied data, Zwevend']
attributes = [dict(r) for r in entities if r[0] == (0, 'ATTRIB')]
for text in ['Ri 1 150Al', 'Ri 1 150Al+', 'Bestaand 95Cu', 'Rooien 50Cu', 'Behouden 50Cu', 'MS 240Al', 'PVC 160']:
    assert any(text in r.get(1, '') for r in attributes), text
    checks += [f'saved attributes contain {text}']
assert all(r.get(7, '').startswith('TECHBASE_ARIAL_NOVA_LIGHT') for r in attributes), attributes
checks += ['all saved attributes use the private Arial Nova Light style']
block_records = [r for s, r in records if s == 'BLOCKS']
circles = [dict(r) for r in block_records if r[0] == (0, 'CIRCLE')]
assert any(abs(float(r[40]) - .64) < 1e-8 for r in circles), 'mantel radius'
checks += ['saved mantel radius is 0.64']
assert any(r.get(2) == 'ZWEVEND_CODE' for r in attributes), 'floating code'
checks += ['saved floating geul contains reference code']
report = {'Status': 'PASS', 'Autoload': args.autoload, 'Checks': checks}
(out / 'commands.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps({'Status': 'PASS', 'Checks': len(checks), 'Autoload': args.autoload}))
