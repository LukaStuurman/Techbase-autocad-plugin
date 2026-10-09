"""Direction service, interactive prompts and independently saved CAD checks."""
import argparse
import json
import os
from pathlib import Path
import subprocess
from package_paths import ROOT, dll

parser = argparse.ArgumentParser()
parser.add_argument('--autoload', action='store_true')
args = parser.parse_args()
out = ROOT / 'validation' / 'Richtingen'
out.mkdir(exist_ok=True)
script = out / 'richtingen.scr'
def capture(name, code):
    return f'(princ (strcat "\\n{name}=" (vl-princ-to-string (cdr (assoc {code} (entget (entlast)))))))'
lines = ([] if args.autoload else ['_NETLOAD', f'"{dll().as_posix()}"']) + [
    'TBRICHTINGVALIDATE',
    '(setvar "OSMODE" 0)', '(setvar "PLINEWID" 3.0)', '(setvar "CELTSCALE" 7.0)', '(setvar "CECOLOR" "2")',
    '_TBTEKENRICHTING', '10', '0,0,0', '10,0,0', '10,10,0', '', capture('DRAW_LAYER', 8), capture('DRAW_WIDTH', 43),
    '_UCS', '_Origin', '100,0,0', '_UCS', '_Z', '90',
    '_TBTEKENRICHTING', '2', '0,0,0', '5,0,0', '',
    '_UCS', '_W',
    '_TBTEKENRICHTING', '3', '20,0,0', '25,0,0', '25,5,0', '_Sluiten', capture('DRAW_CLOSED', 70),
    '_TBTEKENRICHTING', '4', '30,0,0', '35,0,0', '_Terug', '30,5,0', '',
    '_TBTEKENRICHTING', '5', '40,0,0', '45,0,0', '\x03(princ)',
    '(setq sample (entmakex (list (cons 0 "LWPOLYLINE") (cons 100 "AcDbEntity") (cons 8 "0") (cons 100 "AcDbPolyline") (cons 90 3) (cons 70 1) (cons 43 2.0) (cons 10 (list 50.0 0.0)) (cons 42 0.5) (cons 10 (list 55.0 0.0)) (cons 10 (list 55.0 5.0)))))',
    '(sssetfirst nil (ssadd sample))',
    '_TBMAAKRICHTING', '10', capture('CONVERT_LAYER', 8), capture('CONVERT_WIDTH', 43),
    '(princ (strcat "\\nSAME_HANDLE=" (if (equal sample (entlast)) "YES" "NO")))',
    '(sssetfirst nil nil)',
    '(entmake (list (cons 0 "LINE") (cons 8 "0") (cons 10 (list 60.0 0.0 0.0)) (cons 11 (list 65.0 0.0 0.0))))',
    '_TBMAAKRICHTING', '12', '_L', '', capture('LINE_CONVERT_TYPE', 0), capture('LINE_CONVERT_LAYER', 8),
    '_U', capture('UNDO_LINE_TYPE', 0),
    '(princ (strcat "\\nGLOBAL_STATE=" (getvar "CLAYER") ";" (rtos (getvar "PLINEWID") 2 3) ";" (rtos (getvar "CELTSCALE") 2 3) ";" (getvar "CECOLOR")))',
    '_DXFOUT', f'"{(out / "Richtingen - commando.dxf").as_posix()}"', '16', '_QUIT', '_Y',
]
script.write_text('\n'.join(lines) + '\n', encoding='ascii')
raw = out / 'richtingen.raw.log'
env = dict(os.environ, TECHBASE_TEST_OUTPUT=str(out), TECHBASE_SETTINGS_FILE=str(out / 'test-settings.json'))
with raw.open('wb') as log:
    subprocess.run([r'C:\Program Files\Autodesk\AutoCAD 2026\accoreconsole.exe', '/s', str(script), '/l', 'en-US'],
                   cwd=out, env=env, stdout=log, stderr=subprocess.STDOUT, timeout=180, check=True)
log = raw.read_bytes().decode('utf-16-le', errors='replace').replace('\x00', '')
(out / 'richtingen.log').write_text(log, encoding='utf-8')
markers = ['DRAW_LAYER=Aansluiting LS K10', 'DRAW_WIDTH=0.1', 'CONVERT_LAYER=Aansluiting LS K10',
           'CONVERT_WIDTH=0.1', 'SAME_HANDLE=YES', 'LINE_CONVERT_TYPE=LWPOLYLINE',
           'LINE_CONVERT_LAYER=Aansluiting LS K12', 'UNDO_LINE_TYPE=LINE', 'GLOBAL_STATE=0;3;7;2']
missing = [m for m in markers if m not in log]
report = out / 'richtingen.json'
if missing or not report.is_file() or report.stat().st_mtime < script.stat().st_mtime:
    print(log[-8500:])
    raise AssertionError(('command/service checks failed', missing))
service = json.loads(report.read_text())
assert service['Status'] == 'PASS'
rows = (out / 'Richtingen - commando.dxf').read_text(encoding='utf-8', errors='replace').splitlines()
records = []; section = None; current = []
for i in range(0, len(rows) - 1, 2):
    pair = (int(rows[i].strip()), rows[i + 1].strip())
    if pair[0] == 2 and current == [(0, 'SECTION')]: section = pair[1]
    if pair[0] == 0:
        if current and section == 'ENTITIES': records.append(current)
        current = []
        if pair[1] == 'ENDSEC': section = None
    current.append(pair)
polylines = [r for r in records if r[0] == (0, 'LWPOLYLINE')]
assert len(polylines) == 5, 'cancelled drawing added a polyline or LINE Undo failed'
for record in polylines:
    values = dict(record)
    assert float(values[43]) == 0.1 and values[6] == 'DASHED' and float(values[48]) == 0.0035
    assert values.get(62, '256') == '256'
def vertices(record):
    result = []; x = None
    for code, value in record:
        if code == 10: x = float(value)
        if code == 20: result.append((x, float(value)))
    return result
assert vertices(polylines[0]) == [(0, 0), (10, 0), (10, 10)]
assert vertices(polylines[1]) == [(100, 0), (100, 5)]
assert int(dict(polylines[2])[70]) & 1
assert vertices(polylines[3]) == [(30, 0), (30, 5)]
assert vertices(polylines[4]) == [(50, 0), (55, 0), (55, 5)]
assert (42, '0.5') in polylines[4], 'conversion lost the existing arc'
result = {'Status': 'PASS', 'ServiceChecks': len(service['Checks']), 'CommandChecks': len(markers),
          'SavedPolylines': 5, 'Host': 'AutoCAD 2026 Core Console', 'Autoload': args.autoload}
(out / 'command-checks.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
print(json.dumps(result))
