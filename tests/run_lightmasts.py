"""Run xref, symbol, region and actual area-input tests in AutoCAD 2026."""
import json
import argparse
import os
from pathlib import Path
import subprocess
from package_paths import ROOT, dll

parser = argparse.ArgumentParser()
parser.add_argument('--autoload', action='store_true')
args = parser.parse_args()

output = ROOT / 'validation'
output.mkdir(exist_ok=True)
script = output / 'lightmasts.scr'
lines = ([] if args.autoload else ['_NETLOAD', f'"{dll().as_posix()}"']) + [
    'TBMASTVALIDATE',
    'TBMASTAREATEST', '_Tekenen', '0,0,0', '10,0,0', '10,10,0', '0,10,0', '',
    'TBMASTAREATEST', '_Tekenen', '0,0,0', '12,0,0', '_Terug', '10,0,0', '10,10,0', '0,10,0', '_Sluiten',
    '(entmake (list (cons 0 "LWPOLYLINE") (cons 100 "AcDbEntity") (cons 100 "AcDbPolyline") (cons 90 4) (cons 70 1) (cons 10 (list 0.0 0.0)) (cons 10 (list 10.0 0.0)) (cons 10 (list 10.0 10.0)) (cons 10 (list 0.0 10.0))))',
    '_ZOOM', '_E', '_REGEN',
    'TBMASTAREATEST', '_Polyline', '5,0,0',
    '_UCS', '_Z', '90',
    'TBMASTAREATEST', '_Tekenen', '0,0,0', '0,-10,0', '10,-10,0', '10,0,0', '',
    '_UCS', '_W',
    '_QUIT', '_Y'
]
script.write_text('\n'.join(lines) + '\n', encoding='ascii')
env = dict(os.environ, TECHBASE_TEST_OUTPUT=str(output), TECHBASE_SETTINGS_FILE=str(output / 'lightmast-settings.json'))
with (output / 'lightmasts.raw.log').open('wb') as log:
    result = subprocess.run([r'C:\Program Files\Autodesk\AutoCAD 2026\accoreconsole.exe', '/s', str(script), '/l', 'en-US'], cwd=output, env=env, stdout=log, stderr=subprocess.STDOUT, timeout=120)
raw = (output / 'lightmasts.raw.log').read_bytes()
clean = raw.decode('utf-16-le', errors='replace').replace('\x00', '').replace('\r\r', '\r')
(output / 'lightmasts.log').write_text(clean, encoding='utf-8')
report = output / 'Lichtmasten/validatie-lichtmasten.json'
if not report.exists() or report.stat().st_mtime < script.stat().st_mtime or clean.count('AREA_INPUT_PASS') != 4:
    print(clean[-9000:])
    failure = output / 'lichtmast-failure.log'
    if failure.exists():
        print(failure.read_text(encoding='utf-8'))
    raise SystemExit('Lightmast service or area-input checks failed.')
data = json.loads(report.read_text(encoding='utf-8'))
assert data['Status'] == 'PASS'
print(json.dumps({'Status': data['Status'], 'ServiceChecks': len(data['Checks']), 'AreaInputChecks': 4, 'Host': data['Host']}))
(output / 'Lichtmasten/gebiedscommando.json').write_text(json.dumps({'Status': 'PASS', 'Checks': ['tekenen en Enter sluiten', 'Terug en Sluiten tijdens tekenen', 'bestaande polyline kiezen', 'tekenen bij gedraaid UCS']} , indent=2), encoding='utf-8')
