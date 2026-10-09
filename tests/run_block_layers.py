"""Exercise forced block layers through actual command input, with independent settings."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET
from package_paths import ROOT, ASSETS, dll

parser = argparse.ArgumentParser()
parser.add_argument('--autoload', action='store_true')
args = parser.parse_args()
tools = ET.parse(ASSETS / 'Techbase toolpallete.xtp').findall('.//Palette/Tools/Tool')
def code(name):
    tool = next(t for t in tools if t.findtext('Data/Block/BlockName') == name)
    return tool.find('ItemID').attrib['idValue'].strip('{}').replace('-', '').lower()
def layer(number): return f'Aansluiting LS K{number:02}' if number else '01 - Bestaande kabel'
def capture(label): return f'(princ (strcat "\\n{label}=" (cdr (assoc 8 (entget (entlast))))))'
output = ROOT / 'validation/Blocklagen'
output.mkdir(exist_ok=True)
checks = []
for number in [1, 10, 12, 0]:
    name = layer(number)
    settings = output / f'settings-{number}.json'
    settings.write_text(json.dumps({'InheritLayer': True, 'UseChosenBlockLayer': True,
                                   'ChosenBlockLayer': number, 'Repeat': False}), encoding='utf-8')
    script = output / f'layer-{number}.scr'
    lines = [] if args.autoload else ['_NETLOAD', f'"{dll().as_posix()}"']
    lines += ['(setvar "ATTREQ" 0)', '(setvar "INSUNITS" 0)', '(setvar "OSMODE" 0)',
              '(entmake (list (cons 0 "LAYER") (cons 2 "TEST_MAIN") (cons 70 0) (cons 62 1)))',
              '(entmake (list (cons 0 "LINE") (cons 8 "TEST_MAIN") (cons 10 (list 0.0 0.0 0.0)) (cons 11 (list 20.0 0.0 0.0))))']
    if number == 10:
        lines += ['_-LAYER', '_New', f'"{name}"', '_Color', '3', f'"{name}"', '']
    lines += ['_TBPLAATS', code('BESTAANDE MOF'), '5,0,0', capture('FIXED'),
              '_ZOOM', '_E', '_REGEN', '_TBPLAATS', code('BESTAANDE MOF'), '_Object', '15,0,0', capture('OBJECT'),
              '_TBPLAATS', code('NIEUWE MOF'), '30,20,0', '90', capture('ROTATED'),
              '(princ (strcat "\\nCURRENT=" (getvar "CLAYER")))',
              f'(princ (strcat "\\nCOLOR=" (itoa (cdr (assoc 62 (tblsearch "LAYER" "{name}"))))))',
              '_U', '_TBPLAATS', code('NIEUWE MOF'), '50,20,0', '\x03(princ)',
              '(princ (strcat "\\nINSERTS=" (itoa (sslength (ssget "_X" (list (cons 0 "INSERT")))))))',
              '_UCS', '_Z', '90', '_TBPLAATS', code('BESTAANDE MOF'), '20,-70,0', capture('UCS'), '_UCS', '_W',
              '_DXFOUT', f'"{(output / f"layer-{number}.dxf").as_posix()}"', '16', '_QUIT', '_Y']
    script.write_text('\n'.join(lines) + '\n', encoding='ascii')
    raw = output / f'layer-{number}.raw.log'
    with raw.open('wb') as log:
        result = subprocess.run([r'C:\Program Files\Autodesk\AutoCAD 2026\accoreconsole.exe', '/s', str(script), '/l', 'en-US'],
                                cwd=output, env=dict(os.environ, TECHBASE_SETTINGS_FILE=str(settings)),
                                stdout=log, stderr=subprocess.STDOUT, timeout=180)
    log = raw.read_bytes().decode('utf-16-le', errors='replace').replace('\x00', '')
    (output / f'layer-{number}.log').write_text('\n'.join(line.rstrip() for line in log.splitlines()).rstrip() + '\n', encoding='utf-8')
    markers = [f'FIXED={name}', f'OBJECT={name}', f'ROTATED={name}', f'UCS={name}', 'CURRENT=0', 'INSERTS=2']
    if number == 10: markers += ['COLOR=3']
    missing = [m for m in markers if m not in log]
    if missing: print(log[-7500:]); raise AssertionError(missing)
    checks += markers
    # Read the saved inserts, rather than trusting only command output.
    rows = (output / f'layer-{number}.dxf').read_text(encoding='utf-8', errors='replace').splitlines()
    records, record, section = [], [], ''
    for i in range(0, len(rows) - 1, 2):
        pair = int(rows[i].strip()), rows[i + 1].strip()
        if pair[0] == 2 and record == [(0, 'SECTION')]: section = pair[1]
        if pair[0] == 0:
            if record and section == 'ENTITIES': records.append(record)
            record = []
            if pair[1] == 'ENDSEC': section = ''
        record.append(pair)
    inserts = [dict(r) for r in records if r[0] == (0, 'INSERT')]
    assert len(inserts) == 3 and all(r[8] == name for r in inserts), inserts
    assert any(abs(float(r[10])-70) < 1e-8 and abs(float(r[20])-20) < 1e-8 for r in inserts)
    checks += [f'{number}: saved DXF has three inserts on the chosen layer with correct UCS position']
    print(f'Block layer {number}: PASS', flush=True)
print(json.dumps({'Status': 'PASS', 'Checks': len(checks), 'Cases': [1, 10, 12, 0], 'Autoload': args.autoload}))
(output / 'commands.json').write_text(json.dumps({'Status': 'PASS', 'Checks': checks, 'Autoload': args.autoload}, indent=2), encoding='utf-8')
