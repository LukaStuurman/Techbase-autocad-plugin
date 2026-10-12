"""Independently check saved DXF insertions and deployable bundle contents."""
import hashlib
import json
from pathlib import Path
import zipfile
import xml.etree.ElementTree as ET
from package_paths import ROOT, BUNDLE, ASSETS, VERSION, dll as plugin_dll

root = ROOT
bundle = BUNDLE
assets = ASSETS
geul_version = json.loads((assets / 'Kabelgeul/bronversie.json').read_text(encoding='utf-8'))
assert hashlib.sha256((root / geul_version['Source']).read_bytes()).hexdigest() == geul_version['SourceSHA256']
assert hashlib.sha256((assets / 'Kabelgeul/Techbase.Kabelgeul.lsp').read_bytes()).hexdigest() == geul_version['GeneratedSHA256']
assert (assets / 'Kabelgeul/Techbase.Kabelgeul.lsp').read_bytes() == (root / 'Assets/Kabelgeul/Techbase.Kabelgeul.lsp').read_bytes()
xtp = ET.parse(assets / 'Techbase toolpallete.xtp')
source_tools = [t for t in xtp.findall('.//Palette/Tools/Tool') if t.find('Data/Block') is not None]
report = json.loads((root / 'validation/validatie.json').read_text(encoding='utf-8'))
assert report['Status'] == 'PASS'
assert report['ToolCount'] == len(source_tools)
hashes = json.loads((assets / 'bronversies.json').read_text(encoding='utf-8-sig'))
for entry in hashes:
    path = assets / entry['File'] if entry['File'].endswith('.xtp') else assets / 'Library' / entry['File']
    assert hashlib.sha256(path.read_bytes()).hexdigest().upper() == entry['SHA256']

manifest = ET.parse(bundle / 'PackageContents.xml')
for series in [(25, 0), (25, 1), (26, 0)]:
    compatible = []
    for component in manifest.findall('Components'):
        if component.find('ComponentEntry').attrib.get('AppType') == 'Lisp':
            entry = component.find('ComponentEntry')
            assert (bundle / entry.attrib['ModuleName']).is_file()
            assert {'TBKABELGEUL', 'TBGEULCOPY', 'TBGEULEDIT', 'TBGEULCODE'} == {c.attrib['Global'] for c in entry.findall('Commands/Command')}
            continue
        commands = {c.attrib['Global'] for c in component.findall('ComponentEntry/Commands/Command')}
        assert 'TECHBASE' in commands and 'TBLOCKS' not in commands
        assert {'TBRICHTINGEN', 'TBTEKENRICHTING', 'TBMAAKRICHTING', 'TBRICHTINGVALIDATE'} <= commands
        assert {'TBTEKSTLS', 'TBTEKSTOV', 'TBTEKSTKOPIE', 'TBTEKSTVERPLAATS', 'TBTEKSTVALIDATE'} <= commands
        requirement = component.find('RuntimeRequirements')
        minimum = tuple(map(int, requirement.attrib['SeriesMin'][1:].split('.')))
        maximum = tuple(map(int, requirement.attrib['SeriesMax'][1:].split('.')))
        if minimum <= series <= maximum:
            compatible.append(component.find('ComponentEntry'))
    assert len(compatible) == 1
    assert (bundle / compatible[0].attrib['ModuleName']).is_file()
assert not list(bundle.rglob('AcMgd.dll')), 'Host API must not be distributed.'

lines = (root / 'validation/Alle blocks - controle.dxf').read_text(encoding='utf-8', errors='replace').splitlines()
pairs = [(int(lines[i].strip()), lines[i + 1].strip()) for i in range(0, len(lines) - 1, 2)]
records = []
section = ''
record = []
for code, value in pairs:
    if code == 2 and record == [(0, 'SECTION')]:
        section = value
    if code == 0:
        if record and section == 'ENTITIES':
            records.append(record)
        record = []
    record.append((code, value))
    if code == 0 and value == 'ENDSEC':
        section = ''
insertions = [dict(r) for r in records if r[0] == (0, 'INSERT')]
expected = sum(t.findtext('Data/Block/Explode') != '1' for t in source_tools)
assert len(insertions) == expected, (len(insertions), expected)
assert all(r.get(8) == 'TEST_INSERT' for r in insertions)
archive = root / f'Techbase Blocks {VERSION}.zip'
with zipfile.ZipFile(archive) as zip_file:
    assert zip_file.testzip() is None
    for name in ['Installeren.cmd', 'Installeren.ps1', 'Lettertype.ps1', 'LEESMIJ.md', 'VALIDATIE.md']:
        assert name in zip_file.namelist()
        assert zip_file.read(name) == (bundle.parent / name).read_bytes()
    assert zip_file.read('Techbase.Blocks.bundle/Contents/Lettertype.ps1') == (root / 'Lettertype.ps1').read_bytes()
    assert not any(name.lower().endswith(('.ttf', '.otf')) for name in zip_file.namelist()), 'Microsoft fonts must be installed from their official distribution.'
    for series in ['R25.0', 'R26.0']:
        path = plugin_dll(series)
        name = 'Techbase.Blocks.bundle/' + path.relative_to(bundle).as_posix()
        assert zip_file.read(name) == path.read_bytes()
        assert not (path.parent / 'ClosedXML.dll').exists()
        for required in ['Enexis.KabelChecker.ExcelWorker.dll', 'ClosedXML.dll', 'DocumentFormat.OpenXml.dll']:
            worker = path.parent / 'excel' / required
            assert worker.is_file(), required
            assert zip_file.read('Techbase.Blocks.bundle/' + worker.relative_to(bundle).as_posix()) == worker.read_bytes()
    assert json.loads(zip_file.read('Techbase.Blocks.bundle/Contents/Assets/enexis-source.json').decode('utf-8-sig'))['Commit'] == '4009601e15fa4b1b6926380395132b0c1630ce21'
print(json.dumps({'Status': 'PASS', 'XtpTools': len(source_tools), 'SavedDXFInsertions': len(insertions), 'ExpectedExplodedTools': len(source_tools) - expected, 'Archive': str(archive)}, indent=2))
