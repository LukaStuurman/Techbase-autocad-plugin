"""Per-button saved settings exercised in two separate AutoCAD processes."""
import json
import math
import os
import re
import subprocess
import xml.etree.ElementTree as ET
from package_paths import ROOT, ASSETS, dll

output = ROOT / 'validation'
tools = ET.parse(ASSETS / 'Techbase toolpallete.xtp').findall('.//Palette/Tools/Tool')
def code(name):
    t = next(t for t in tools if t.findtext('Data/Block/BlockName') == name)
    return t.find('ItemID').attrib['idValue'].strip('{}').replace('-', '').lower()
settings = output / 'rotation-modes-settings.json'
settings.write_text(json.dumps({'InheritLayer': True, 'Repeat': False, 'BlockRotationModes': {
    code('BESTAANDE MOF'): 'BeforePoint', code('NIEUWE MOF'): 'None', code('Flatkast'): 'AfterPoint',
}}), encoding='utf-8')
before = settings.read_bytes()

def capture(label):
    return f'(princ (strcat "\\nMODE_{label}=" (rtos (cdr (assoc 50 (entget (entlast)))) 2 8) ";" (cdr (assoc 8 (entget (entlast))))))'

checks = []
for run in [1, 2]:
    script = output / f'rotation-modes-{run}.scr'
    commands = [
        '_NETLOAD', f'"{dll().as_posix()}"', '(setvar "ATTREQ" 0)', '(setvar "OSMODE" 0)', '(setvar "INSUNITS" 0)',
        '(entmake (list (cons 0 "LAYER") (cons 2 "TEST_ROTATION_MAIN") (cons 70 0) (cons 62 1)))',
        '(entmake (list (cons 0 "LINE") (cons 8 "TEST_ROTATION_MAIN") (cons 10 (list 0.0 0.0 0.0)) (cons 11 (list 100.0 0.0 0.0))))',
        '_TBPLAATS', code('BESTAANDE MOF'), '90', '5,0,0', capture('BEFORE'),
        '_TBPLAATS', code('NIEUWE MOF'), '10,0,0', capture('OFF'),
        '_TBPLAATS', code('Flatkast'), '15,0,0', '45', capture('AFTER'),
        '_UCS', '_Z', '90',
        '_TBPLAATS', code('BESTAANDE MOF'), '30', '0,-20,0', capture('UCS'),
        '_UCS', '_W',
        '_TBPLAATS', code('BESTAANDE MOF'), '\x03(princ)',
        '_TBPLAATS', code('BESTAANDE MOF'), '20', '\x03(princ)',
        '(princ (strcat "\\nMODE_COUNT=" (itoa (sslength (ssget "_X" (list (cons 0 "INSERT")))))))',
        '_QUIT', '_Y',
    ]
    script.write_text('\n'.join(commands) + '\n', encoding='ascii')
    raw = output / f'rotation-modes-{run}.raw.log'
    with raw.open('wb') as f:
        subprocess.run([r'C:\Program Files\Autodesk\AutoCAD 2026\accoreconsole.exe', '/s', str(script), '/l', 'en-US'],
                       cwd=output, env=dict(os.environ, TECHBASE_SETTINGS_FILE=str(settings)),
                       stdout=f, stderr=subprocess.STDOUT, timeout=120, check=True)
    log = raw.read_bytes().decode('utf-16-le', errors='replace').replace('\x00', '')
    (output / f'rotation-modes-{run}.log').write_text(log, encoding='utf-8')
    try:
        for name, degrees in [('BEFORE', 90), ('OFF', 0), ('AFTER', 45), ('UCS', 120)]:
            angle, layer = re.search(r'\nMODE_' + name + r'=([-0-9.]+);([^"\r\n]+)', log).groups()
            assert abs(float(angle) - math.radians(degrees)) < 1e-7
            assert layer == 'TEST_ROTATION_MAIN'
            checks.append(f'process {run}: {name} rotation and object layer')
        assert 'MODE_COUNT=4' in log
        # The real prompts show the order, including suppression of the XTP question.
        starts = log.split('Tool-ID: ')
        first = starts[1]
        assert first.index('Draaihoek') < first.index('Invoegpunt')
        assert 'Draaihoek' not in starts[2]
        assert starts[3].index('Invoegpunt') < starts[3].index('Draaihoek')
        checks.extend([f'process {run}: saved prompt order', f'process {run}: both cancellations leave no insert'])
    except (AssertionError, AttributeError):
        print(log[-6500:]); raise
assert settings.read_bytes() == before
report = {'Status': 'PASS', 'Checks': checks, 'Host': 'AutoCAD 2026 Core Console'}
(output / 'rotation-modes.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps({'Status': 'PASS', 'Checks': len(checks), 'Processes': 2}))
