"""Real insertion with prompts off/on, retained defaults and independent ATTREQ."""
import argparse, json, os, subprocess, xml.etree.ElementTree as ET
from package_paths import ROOT, ASSETS, dll
from dxf_records import records, value

parser = argparse.ArgumentParser()
parser.add_argument('--autoload', action='store_true')
args = parser.parse_args()
out = ROOT/'validation/Blocktekst'
out.mkdir(exist_ok=True)
tool = next(t for t in ET.parse(ASSETS/'Techbase toolpallete.xtp').findall('.//Palette/Tools/Tool') if t.findtext('Data/Block/BlockName') == 'NIEUWE MOF')
code = tool.find('ItemID').attrib['idValue'].strip('{}').replace('-', '').lower()
checks = []
for case, enabled, attreq in [('default', None, 1), ('on', True, 0), ('off', False, 1)]:
    settings = out/f'{case}-settings.json'
    data = {'InheritLayer':False, 'BlockRotationModes':{code:'None'}}
    if enabled is not None:
        data['PromptBlockText'] = enabled
    settings.write_text(json.dumps(data), encoding='utf-8')
    before = settings.read_bytes()
    (out/f'{case}.dxf').unlink(missing_ok=True)
    lines = ([] if args.autoload else ['_NETLOAD',f'"{dll().as_posix()}"'])+[
        '(setvar "OSMODE" 0)', '(setvar "INSUNITS" 0)', f'(setvar "ATTREQ" {attreq})',
        '_TBPLAATS', code, '0,0,0']
    if enabled:
        lines += ['Eigen moftekst']
    lines += [f'(princ (strcat "\\nATTREQ_UNCHANGED=" (itoa (getvar "ATTREQ"))))',
              '_DXFOUT', f'"{(out/f"{case}.dxf").as_posix()}"', '16', '_QUIT','_Y']
    script = out/f'{case}.scr'
    script.write_text('\n'.join(lines)+'\n', encoding='ascii')
    raw = out/f'{case}.raw.log'
    with raw.open('wb') as log:
        subprocess.run([r'C:\Program Files\Autodesk\AutoCAD 2026\accoreconsole.exe','/s',str(script),'/l','en-US'],
                       cwd=out, env=dict(os.environ,TECHBASE_SETTINGS_FILE=str(settings)),
                       stdout=log,stderr=subprocess.STDOUT,check=True,timeout=120)
    log = raw.read_bytes().decode('utf-16-le',errors='replace').replace('\x00','')
    (out/f'{case}.log').write_text('\n'.join(l.rstrip() for l in log.splitlines()).rstrip()+'\n',encoding='utf-8')
    assert 'Geplaatst: NIEUWE MOF' in log,log[-4000:]
    assert ('Mof_tekst <' in log) == bool(enabled),log[-4000:]
    attrs = [r for r in records(out/f'{case}.dxf') if r[0]==(0,'ATTRIB')]
    assert len(attrs)==1 and value(attrs[0],1)==('Eigen moftekst' if enabled else 'EM 150Al (was ????-??)'),attrs
    assert f'ATTREQ_UNCHANGED={attreq}' in log
    assert settings.read_bytes()==before
    checks += [case+': juiste tekstvraag',case+': opgeslagen attribuuttekst',case+': AutoCAD ATTREQ behouden',case+': bewaarde voorkeur behouden']
(out/'commands.json').write_text(json.dumps({'Status':'PASS','Checks':checks,'Autoload':args.autoload},indent=2),encoding='utf-8')
print(json.dumps({'Status':'PASS','Checks':len(checks),'Autoload':args.autoload}))
