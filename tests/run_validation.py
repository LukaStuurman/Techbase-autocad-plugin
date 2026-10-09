"""Run the real managed plugin in the installed AutoCAD engine; preserve a clean log."""
import argparse
import json
import os
from pathlib import Path
import subprocess
from package_paths import ROOT, dll as plugin_dll

parser = argparse.ArgumentParser()
parser.add_argument('--engine', default=r'C:\Program Files\Autodesk\AutoCAD 2026\accoreconsole.exe')
parser.add_argument('--autoload', action='store_true', help='Use the installed bundle without NETLOAD.')
args = parser.parse_args()
root = ROOT
output = root / 'validation'
output.mkdir(exist_ok=True)
dll = plugin_dll()
script = output / 'validate.scr'
load = '' if args.autoload else f'_NETLOAD\n"{dll.as_posix()}"\n'
script.write_text(load + 'TBVALIDATE\n_QUIT\n_Y\n', encoding='ascii')
env = dict(os.environ, TECHBASE_TEST_OUTPUT=str(output), TECHBASE_SETTINGS_FILE=str(output / 'test-settings.json'))
with (output / 'engine.raw.log').open('wb') as log:
    result = subprocess.run([args.engine, '/s', str(script), '/l', 'en-US'], cwd=output, env=env, stdout=log, stderr=subprocess.STDOUT, timeout=240)
raw = (output / 'engine.raw.log').read_bytes()
clean = raw.decode('utf-16-le', errors='replace').replace('\x00', '')
(output / 'engine.log').write_text(clean, encoding='utf-8')
report = output / 'validatie.json'
if report.exists() and report.stat().st_mtime >= script.stat().st_mtime:
    data = json.loads(report.read_text(encoding='utf-8'))
    print(json.dumps({'status': data['Status'], 'tools': data['ToolCount'], 'checks': len(data['Checks']), 'host': data['Host'], 'report': str(report)}, ensure_ascii=False))
else:
    print(clean[-4000:])
    raise SystemExit('No current passing validation report was produced.')
