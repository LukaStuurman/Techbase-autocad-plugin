"""Check persisted RT order independently, and repair original RT definitions."""
import argparse, hashlib, json, os, subprocess
from package_paths import ROOT, ASSETS, dll
from dxf_records import block_orders, value

parser = argparse.ArgumentParser()
parser.add_argument('--autoload', action='store_true')
args = parser.parse_args()
out = ROOT / 'validation/RT'
out.mkdir(exist_ok=True)
checks = []
def verify(path, predicate):
    blocks = block_orders(path, predicate)
    assert len(blocks) == 11, (path, list(blocks))
    masks = 0
    for name, entities in blocks.items():
        wipes = sum(r[0] == (0, 'WIPEOUT') for r in entities)
        masks += wipes
        assert all(r[0] == (0, 'WIPEOUT') for r in entities[:wipes]), name
        assert all(r[0] != (0, 'WIPEOUT') for r in entities[wipes:]), name
        checks.append(path.name + ': ' + name + ': maskers volledig achter')
    assert masks > 0
    return masks

import_masks = verify(out / 'RT blocks.dxf', lambda n: n.startswith('TB_RT'))
source = ASSETS / 'Library/Techbase template LS en OV.dwg'
before = hashlib.sha256(source.read_bytes()).hexdigest()
script = out / 'repair.scr'
for name in ['RT voor herstel.dxf', 'RT hersteld.dxf']:
    (out/name).unlink(missing_ok=True)
lines = ([] if args.autoload else ['_NETLOAD', f'"{dll().as_posix()}"']) + [
    '_DXFOUT', f'"{(out/"RT voor herstel.dxf").as_posix()}"', '16',
    '_TBRTACHTER', '_TBRTACHTER',
    '_DXFOUT', f'"{(out/"RT hersteld.dxf").as_posix()}"', '16', '_QUIT', '_Y']
script.write_text('\n'.join(lines) + '\n', encoding='ascii')
raw = out / 'repair.raw.log'
with raw.open('wb') as log:
    subprocess.run([r'C:\Program Files\Autodesk\AutoCAD 2026\accoreconsole.exe', '/i', str(source), '/s', str(script), '/l', 'en-US'],
                   cwd=out, env=dict(os.environ, TECHBASE_SETTINGS_FILE=str(out/'settings.json')),
                   stdout=log, stderr=subprocess.STDOUT, check=True, timeout=120)
log = raw.read_bytes().decode('utf-16-le', errors='replace').replace('\x00', '')
(out/'repair.log').write_text('\n'.join(l.rstrip() for l in log.splitlines()).rstrip()+'\n', encoding='utf-8')
assert 'RT-maskers: 0 blockdefinities bijgewerkt' in log, log[-4000:]
repair_masks = verify(out/'RT hersteld.dxf', lambda n: n.startswith('RT'))
all_before = block_orders(out/'RT voor herstel.dxf', lambda n: True)
all_after = block_orders(out/'RT hersteld.dxf', lambda n: True)
assert all_before.keys() == all_after.keys()
for name, entities in all_before.items():
    # Compare entities independently of the intentional RT ordering change.
    before_by_handle = {value(r,5):r for r in entities}
    after_by_handle = {value(r,5):r for r in all_after[name]}
    assert before_by_handle == after_by_handle, name
    if not name.startswith('RT'):
        assert entities == all_after[name], name
checks += ['alle blockinhoud identiek voor/na herstel', 'tekenvolgorde niet-RT blocks intact']
assert repair_masks == import_masks
assert hashlib.sha256(source.read_bytes()).hexdigest() == before
checks += ['herhaald herstel wijzigt niets', 'oorspronkelijke bron-DWG ongewijzigd']
report = {'Status':'PASS', 'Checks':checks, 'MaskCount':import_masks, 'Autoload':args.autoload}
(out/'commands.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps({'Status':'PASS', 'Checks':len(checks), 'Masks':import_masks, 'Autoload':args.autoload}))
