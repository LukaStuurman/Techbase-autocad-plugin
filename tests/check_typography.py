"""Check persisted CAD font data independently of the managed implementation."""
import json
from pathlib import Path
from package_paths import ROOT

report = json.loads((ROOT / 'validation/lettertype.json').read_text())
assert report['Status'] == 'PASS'
lines = (ROOT / 'validation/Lettertype - controle.dxf').read_text(encoding='utf-8', errors='replace').splitlines()
records, record = [], []
for i in range(0, len(lines) - 1, 2):
    pair = (int(lines[i].strip()), lines[i + 1].strip())
    if pair[0] == 0:
        if record: records.append(record)
        record = []
    record.append(pair)
styles = {dict(r).get(2): r for r in records if r[0] == (0, 'STYLE')}
assert dict(styles['TECHBASE_ARIAL_NOVA_LIGHT'])[3].lower() == 'arial.ttf', 'Existing user style changed'
texts = [r for r in records if r[0][1] in ('TEXT', 'MTEXT', 'ATTDEF', 'ATTRIB')]
assert len(texts) > 100
for text in texts:
    style = styles[dict(text)[7]]
    assert dict(style)[3].lower().endswith('arialnova-light.ttf'), dict(style)
    assert (1000, 'Arial Nova Light') in style, style
    if text[0][1] == 'MTEXT':
        contents = ''.join(value for code, value in text if code in (1, 3))
        assert '\\fArial' not in contents
installer = json.loads((ROOT / 'validation/font-installer.json').read_text(encoding='utf-8-sig'))
assert installer['Status'] == 'PASS'
print(json.dumps({'Status': 'PASS', 'ManagedChecks': len(report['Checks']),
                  'SavedTexts': len(texts), 'InstallerChecks': installer['Checks']}))
