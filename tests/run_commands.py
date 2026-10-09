"""Exercise actual AutoCAD command input, with settings isolated from the user."""
import json
import os
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET
from package_paths import ROOT, ASSETS, dll as plugin_dll

root = ROOT
output = root / 'validation'
output.mkdir(exist_ok=True)
dll = plugin_dll()
xtp = ASSETS / 'Techbase toolpallete.xtp'
tool = next(t for t in ET.parse(xtp).findall('.//Palette/Tools/Tool') if t.findtext('Data/Block/BlockName') == 'BESTAANDE MOF')
code = tool.find('ItemID').attrib['idValue'].strip('{}').replace('-', '').lower()
settings = output / 'command-settings.json'
settings.write_text(json.dumps({'InheritLayer': True, 'Tolerance': 0.05, 'Repeat': False}), encoding='utf-8')
script = output / 'commands.scr'
lines = [
    '_NETLOAD', f'"{dll.as_posix()}"',
    '(setvar "ATTREQ" 0)',
    '(setvar "INSUNITS" 0)',
    '(entmake (list (cons 0 "LAYER") (cons 2 "TEST_MAIN") (cons 70 0) (cons 62 1)))',
    '(entmake (list (cons 0 "LINE") (cons 8 "TEST_MAIN") (cons 10 (list 0.0 0.0 0.0)) (cons 11 (list 20.0 0.0 0.0))))',
    '_TBPLAATS', code, '5,0,0',
    '(princ (strcat "\\nCHECK_ON=" (cdr (assoc 8 (entget (entlast))))))',
    '_TBLAAG', '_TBPLAATS', code, '10,0,0',
    '(princ (strcat "\\nCHECK_OFF=" (cdr (assoc 8 (entget (entlast))))))',
    '_TBLAAG', '_ZOOM', '_E', '_REGEN',
    '_TBPLAATS', code, '_Object', '15,0,0',
    '(princ (strcat "\\nCHECK_OBJECT=" (cdr (assoc 8 (entget (entlast))))))',
    '(princ (strcat "\\nCHECK_CURRENT=" (getvar "CLAYER")))',
    '(entmake (list (cons 0 "LINE") (cons 8 "TEST_MAIN") (cons 10 (list 100.0 0.0 0.0)) (cons 11 (list 100.0 20.0 0.0))))',
    '_UCS', '_Z', '90',
    '_TBPLAATS', code, '5,-100,0',
    '(princ (strcat "\\nCHECK_UCS_LAYER=" (cdr (assoc 8 (entget (entlast))))))',
    '(princ (strcat "\\nCHECK_UCS_POSITION=" (rtos (car (cdr (assoc 10 (entget (entlast))))) 2 3) "," (rtos (cadr (cdr (assoc 10 (entget (entlast))))) 2 3)))',
    '_UCS', '_W',
    '_UNDO', '2',
    '(princ (strcat "\\nCHECK_AFTER_UNDO=" (itoa (sslength (ssget "_X" (list (cons 0 "INSERT")))))))',
    '_QUIT', '_Y',
]
script.write_text('\n'.join(lines) + '\n', encoding='ascii')
env = dict(os.environ, TECHBASE_SETTINGS_FILE=str(settings))
with (output / 'commands.raw.log').open('wb') as log:
    result = subprocess.run([r'C:\Program Files\Autodesk\AutoCAD 2026\accoreconsole.exe', '/s', str(script), '/l', 'en-US'], cwd=output, env=env, stdout=log, stderr=subprocess.STDOUT, timeout=120)
clean = (output / 'commands.raw.log').read_bytes().decode('utf-16-le', errors='replace').replace('\x00', '').replace('\r\r', '\r')
(output / 'commands.log').write_text(clean, encoding='utf-8')
expected = ['CHECK_ON=TEST_MAIN', 'CHECK_OFF=0', 'CHECK_OBJECT=TEST_MAIN', 'CHECK_CURRENT=0', 'CHECK_UCS_LAYER=TEST_MAIN', 'CHECK_UCS_POSITION=100,5', 'CHECK_AFTER_UNDO=3']
missing = [marker for marker in expected if marker not in clean]
if missing:
    print(clean[-6500:])
    raise SystemExit('Missing command checks: ' + str(missing))
print(json.dumps({'status': 'PASS', 'checks': expected, 'host': 'AutoCAD 2026 Core Console'}))
(output / 'commands.json').write_text(json.dumps({'Status': 'PASS', 'Checks': expected}, indent=2), encoding='utf-8')
