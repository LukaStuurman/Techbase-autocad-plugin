"""Exercise both placement stages through real AutoCAD command input."""
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
    tool = next(t for t in tools if t.findtext('Data/Block/BlockName') == name)
    return tool.find('ItemID').attrib['idValue'].strip('{}').replace('-', '').lower()

def capture(label):
    return f'(princ (strcat "\\nROTATION_{label}=" (rtos (cdr (assoc 50 (entget (entlast)))) 2 8)))'

settings = output / 'rotation-settings.json'
settings.write_text(json.dumps({'InheritLayer': False, 'Repeat': False}), encoding='utf-8')
script = output / 'rotation.scr'
lines = [
    '_NETLOAD', f'"{dll().as_posix()}"',
    '(setvar "ATTREQ" 0)', '(setvar "INSUNITS" 0)', '(setvar "OSMODE" 0)',
    '_TBPLAATS', code('NIEUWE MOF'), '0,0,0', '0', capture('BASE'),
    '_TBPLAATS', code('NIEUWE MOF'), '20,0,0', '90', capture('PROMPT'),
    '_TBPLAATS', code('NIEUWE MOF'), '40,0,0', '', capture('DEFAULT'),
    '_TBPLAATS', code('BESTAANDE MOF'), '_Draaien', '-90', '60,0,0', capture('OPTION'),
    '_TBPLAATS', code('NIEUWE MOF'), '_Draaien', '45', '80,0,0', capture('CHOSEN'),
    '_UCS', '_Z', '90',
    '_TBPLAATS', code('NIEUWE MOF'), '0,-100,0', '30', capture('UCS'),
    '_UCS', '_W',
    '_TBPLAATS', code('NIEUWE MOF'), '120,0,0', '\x03(princ)',
    '_TBPLAATS', code('BESTAANDE MOF'), '_Draaien', '\x03(princ)',
    '(princ (strcat "\\nINSERT_COUNT=" (itoa (sslength (ssget "_X" (list (cons 0 "INSERT")))))))',
    '_DXFOUT', f'"{(output / "Draaien - commando.dxf").as_posix()}"', '16',
    '_QUIT', '_Y',
]
script.write_text('\n'.join(lines) + '\n', encoding='ascii')
env = dict(os.environ, TECHBASE_SETTINGS_FILE=str(settings))
with (output / 'rotation.raw.log').open('wb') as log:
    subprocess.run([r'C:\Program Files\Autodesk\AutoCAD 2026\accoreconsole.exe', '/s', str(script), '/l', 'en-US'],
                   cwd=output, env=env, stdout=log, stderr=subprocess.STDOUT, timeout=120, check=True)
log = (output / 'rotation.raw.log').read_bytes().decode('utf-16-le', errors='replace').replace('\x00', '').replace('\r\r', '\r')
(output / 'rotation.log').write_text(log, encoding='utf-8')
try:
    for label, degrees in [('BASE', 0), ('PROMPT', 90), ('DEFAULT', 0), ('OPTION', 270), ('CHOSEN', 45), ('UCS', 120)]:
        marker = f'ROTATION_{label}='
        value = float(re.search(r'\n' + marker + r'([-0-9.]+)', log).group(1))
        assert abs(value - math.radians(degrees)) < 1e-7, (label, value)
    assert 'INSERT_COUNT=6' in log
except (AssertionError, IndexError, ValueError):
    print(log[-9000:])
    raise

# Read the actual saved attribute geometry, independent of the jig code.
rows = (output / 'Draaien - commando.dxf').read_text(encoding='utf-8', errors='replace').splitlines()
pairs = [(int(rows[i].strip()), rows[i + 1].strip()) for i in range(0, len(rows) - 1, 2)]
entities = []
section = None
current = []
for pair in pairs:
    if pair[0] == 2 and current == [(0, 'SECTION')]: section = pair[1]
    if pair[0] == 0:
        if current and section == 'ENTITIES': entities.append(current)
        current = []
        if pair[1] == 'ENDSEC': section = None
    current.append(pair)
blocks = []
for record in entities:
    data = {}
    for key, value in record:
        if key == 101: break  # Embedded MText has its own position and direction codes.
        data.setdefault(key, value)
    if data.get(0) == 'INSERT': blocks.append({'insert': data, 'attributes': []})
    elif data.get(0) == 'ATTRIB': blocks[-1]['attributes'].append(data)
assert len(blocks) == 6
baseline = blocks[0]
checks = 9
for index in [1, 2, 4, 5]:
    block = blocks[index]
    theta = math.radians(float(block['insert'].get(50, 0)))
    assert len(block['attributes']) == len(baseline['attributes']) > 0
    for source, actual in zip(baseline['attributes'], block['attributes']):
        assert actual[1] == source[1] and actual[2] == source[2]
        for x, y in [(10, 20), (11, 21)]:
            if x not in source: continue
            sx, sy = float(source[x]), float(source[y])
            expected = (sx * math.cos(theta) - sy * math.sin(theta) + float(block['insert'][10]),
                        sx * math.sin(theta) + sy * math.cos(theta) + float(block['insert'][20]))
            assert math.dist(expected, (float(actual[x]), float(actual[y]))) < 1e-6, (index, source[2], expected)
        difference = math.radians(float(actual.get(50, 0)) - float(source.get(50, 0))) - theta
        assert abs(math.sin(difference)) < 1e-7 and math.cos(difference) > 0
        checks += 1
report = {'Status': 'PASS', 'Checks': checks, 'Host': 'AutoCAD 2026 Core Console',
          'Cases': ['prompt rotation', 'Enter default', 'Draaien option', 'one chosen angle', 'rotated UCS', 'cancel after point', 'cancel before point', 'saved attribute geometry']}
(output / 'rotation.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report))
