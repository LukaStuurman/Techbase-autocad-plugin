"""Actual burst, ordered selection, repeat, move, Back, Escape and Undo."""
import argparse
import json
import os
import subprocess
from package_paths import ROOT, dll

parser = argparse.ArgumentParser(); parser.add_argument('--autoload', action='store_true'); args = parser.parse_args()
out = ROOT / 'validation' / 'Teksten'; out.mkdir(exist_ok=True)
script = out / 'teksten.scr'
settings = out / 'test-settings.json'
settings.write_text(json.dumps({'InheritLayer': False, 'Repeat': False}), encoding='utf-8')
def fixture(variable, label, y):
    return f'(setq {variable} (entmakex (list (cons 0 "MTEXT") (cons 100 "AcDbEntity") (cons 8 "0") (cons 100 "AcDbMText") (cons 10 (list 100.0 {y}.0 0.0)) (cons 40 1.0) (cons 41 12.0) (cons 71 1) (cons 1 "{label}"))))'
def capture(name, code, entity='(entlast)'):
    return f'(princ (strcat "\\n{name}=" (vl-princ-to-string (cdr (assoc {code} (entget {entity}))))))'
lines = ([] if args.autoload else ['_NETLOAD', f'"{dll().as_posix()}"']) + [
    'TBTEKSTVALIDATE', '(setvar "OSMODE" 0)', '(setvar "ATTREQ" 0)', '(setvar "INSUNITS" 0)',
    '_TBTEKSTLS', '0,0,0', '_TBTEKSTOV', '40,0,0',
    '(princ (strcat "\\nBURST_TEXT_COUNT=" (itoa (sslength (ssget "_X" (list (cons 0 "MTEXT")))))))',
    fixture('text_a', 'Alpha', 20), fixture('text_b', 'Beta', 24), '_ZOOM', '_E', '_REGEN',
    '_TBTEKSTKOPIE', '100.2,19.5,0', '100.2,19.5,0', '100.2,23.5,0', '100.2,19.5,0', '',
    '0,40,0', '10,40,0', '0,50,0', '-10,50,0', '_Terug', '0,60,0', '0,70,0', '',
    capture('LAST_COPY_CONTENT', 1), capture('LAST_COPY_ROTATION', 50),
    '(princ (strcat "\\nCOPY_TEXT_COUNT=" (itoa (sslength (ssget "_X" (list (cons 0 "MTEXT")))))))',
    '_TBTEKSTVERPLAATS', '100.2,19.5,0', '', '20,40,0', '10,40,0',
    capture('MOVE_LOCATION', 10, 'text_a'), capture('MOVE_ANCHOR', 71, 'text_a'),
    '_U', capture('UNDO_LOCATION', 10, 'text_a'),
    '_TBTEKSTVERPLAATS', '100.2,23.5,0', '', '0,80,0', '\x03(princ)',
    capture('CANCEL_LOCATION', 10, 'text_b'),
    '_DXFOUT', f'"{(out / "Richtingsteksten - commando.dxf").as_posix()}"', '16', '_QUIT', '_Y',
]
script.write_text('\n'.join(lines) + '\n', encoding='ascii')
raw = out / 'teksten.raw.log'
with raw.open('wb') as log:
    subprocess.run([r'C:\Program Files\Autodesk\AutoCAD 2026\accoreconsole.exe', '/s', str(script), '/l', 'en-US'],
                   cwd=out, env=dict(os.environ, TECHBASE_TEST_OUTPUT=str(out), TECHBASE_SETTINGS_FILE=str(settings)),
                   stdout=log, stderr=subprocess.STDOUT, timeout=180, check=True)
log = raw.read_bytes().decode('utf-16-le', errors='replace').replace('\x00', '')
(out / 'teksten.log').write_text(log, encoding='utf-8')
markers = ['BURST_TEXT_COUNT=26', 'COPY_TEXT_COUNT=32', 'LAST_COPY_CONTENT=Alpha', 'MOVE_ANCHOR=1',
           'Laatste kopie verwijderd.', 'Tekst uit selectie verwijderd.', 'UNDO_LOCATION=(100.0 20.0 0.0)',
           'CANCEL_LOCATION=(100.0 24.0 0.0)']
missing = [m for m in markers if m not in log]
proof = out / 'richtingsteksten.json'
if missing or not proof.is_file() or proof.stat().st_mtime < script.stat().st_mtime:
    print(log[-8500:]); raise AssertionError(missing or 'No new service report')
data = json.loads(proof.read_text()); assert data['Status'] == 'PASS'
rows = (out / 'Richtingsteksten - commando.dxf').read_text(encoding='utf-8', errors='replace').splitlines()
records = []; section = None; record = []
for i in range(0, len(rows) - 1, 2):
    pair = int(rows[i].strip()), rows[i + 1].strip()
    if pair[0] == 2 and record == [(0, 'SECTION')]: section = pair[1]
    if pair[0] == 0:
        if record and section == 'ENTITIES': records.append(record)
        record = []
        if pair[1] == 'ENDSEC': section = None
    record.append(pair)
texts = [dict(r) for r in records if r[0] == (0, 'MTEXT')]
assert len(texts) == 32 and not any(r[0][1] in ['INSERT', 'ATTRIB', 'ATTDEF'] for r in records)
assert texts[0][1]=='{95AL / (was ????-00)}' and texts[13][1]=='{6CU / (was ????-00)}'
assert all(t[1]=='{150AL / ????-00}' for t in texts[1:13]) and all(t[1]=='{6CU / ????-00}' for t in texts[14:26])
alpha = [t for t in texts if t.get(1) == 'Alpha']; beta = [t for t in texts if t.get(1) == 'Beta']
assert len(alpha) == len(beta) == 3
assert any(float(t[10]) == 100 and float(t[20]) == 20 for t in alpha)
assert any(float(t[10]) == 100 and float(t[20]) == 24 for t in beta)
assert any(abs(float(t[20]) - 40.4) < 1e-8 for t in beta)
assert not any(abs(float(t[20]) - 49.6) < 1e-8 for t in texts), 'Back retained the reverse copy'
assert any(abs(float(t[10]) + 0.4) < 1e-8 and abs(float(t[20]) - 60) < 1e-8 for t in beta)
result = {'Status': 'PASS', 'ServiceChecks': len(data['Checks']), 'CommandChecks': len(markers),
          'SavedMText': 32, 'Autoload': args.autoload}
(out / 'command-checks.json').write_text(json.dumps(result, indent=2), encoding='utf-8'); print(json.dumps(result))
