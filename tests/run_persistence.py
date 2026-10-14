"""Reload saved custom/reset settings in fresh AutoCAD processes and inspect actual output."""
import argparse,json,os,subprocess
from pathlib import Path
from package_paths import ROOT,BUNDLE,dll
parser=argparse.ArgumentParser();parser.add_argument('--autoload',action='store_true');args=parser.parse_args()
out=ROOT/'validation/Instellingen';out.mkdir(exist_ok=True);checks=[]
installed=Path(os.environ['APPDATA'])/'Autodesk/ApplicationPlugins/Techbase.Blocks.bundle';bundle=installed if args.autoload else BUNDLE
for case in ['custom','reset']:
    settings=out/('persistent.before.json' if case=='custom' else 'persistent.json');data=json.loads(settings.read_text())
    height=data['DirectionTextHeight'];width=data['DirectionTextWidth075' if height==.75 else 'DirectionTextWidth045'];existing=data['ExistingDirectionTextWidth075' if height==.75 else 'ExistingDirectionTextWidth045']
    lines=([] if args.autoload else ['_NETLOAD',f'"{dll().as_posix()}"'])+[f'(load "{(bundle/"Contents/Assets/Kabelgeul/Techbase.Kabelgeul.lsp").as_posix()}")',
        '(setvar "OSMODE" 0)','(setvar "ATTREQ" 0)',
        '(entmake (list (cons 0 "LINE") (cons 8 "Aansluiting LS K10") (cons 10 (list 0.0 0.0 0.0)) (cons 11 (list 80.0 0.0 0.0))))',
        '(entmake (list (cons 0 "LINE") (cons 8 "01 - Bestaande kabel") (cons 10 (list 0.0 2.0 0.0)) (cons 11 (list 80.0 2.0 0.0))))',
        '_TBAUTOTEKSTLS','_TweePunten','5,0,0','5,3,0','5,5,0',
        '_ZOOM','_E','_REGEN','_TBKABELGEUL','30,0,0','30,3,0','_Direct','_TBBESTAANDEKLEUR',
        '_DXFOUT',f'"{(out/f"Herstart-{case}.dxf").as_posix()}"','16','_QUIT','_Y']
    script=out/f'herstart-{case}.scr';script.write_text('\n'.join(lines)+'\n',encoding='ascii');raw=out/f'herstart-{case}.raw.log'
    with raw.open('wb') as log:subprocess.run([r'C:\Program Files\Autodesk\AutoCAD 2026\accoreconsole.exe','/s',str(script),'/l','en-US'],cwd=out,env=dict(os.environ,TECHBASE_SETTINGS_FILE=str(settings),TECHBASE_KABELGEUL_TEST_CONFIG='DEFAULTS'),stdout=log,stderr=subprocess.STDOUT,check=True,timeout=120)
    log=raw.read_bytes().decode('utf-16-le',errors='replace').replace('\x00','');(out/f'herstart-{case}.log').write_text('\n'.join(l.rstrip() for l in log.splitlines()).rstrip()+'\n',encoding='utf-8')
    assert 'KabelGeul geplaatst als uniek block.' in log and '2 LS-richtingsteksten geplaatst.' in log,log[-6000:]
    lines=(out/f'Herstart-{case}.dxf').read_text(errors='replace').splitlines();records=[];rec=[];section=''
    for i in range(0,len(lines)-1,2):
        pair=int(lines[i].strip()),lines[i+1].strip()
        if pair[0]==2 and rec==[(0,'SECTION')]:section=pair[1]
        if pair[0]==0:
            if rec:records.append((section,rec))
            rec=[]
            if pair[1]=='ENDSEC':section=''
        rec.append(pair)
    texts=[{k:next(v for c,v in r if c==k) for k,_ in r} for section,r in records if section=='ENTITIES' and r[0]==(0,'MTEXT')];assert len(texts)==2
    assert all(abs(float(t[40])-height)<1e-8 and abs(float(t[41])-(existing if t[8]=='01 - Bestaande kabel' else width))<1e-8 for t in texts)
    assert all(bool(int(t.get(90,0))&1)==data['DirectionTextMask'] for t in texts)
    attrs=[dict(r) for section,r in records if section=='ENTITIES' and r[0]==(0,'ATTRIB')]
    assert any(a.get(1)==('Eigen tekst' if case=='custom' else 'Ri 10') for a in attrs),attrs
    layer=next(dict(r) for section,r in records if r[0]==(0,'LAYER') and dict(r)[2]=='01 - Bestaande kabel')
    assert layer.get(420)=='0' if data['ExistingCableBlack'] else layer[62]=='125'
    checks.extend([case+': saved widths/height after a fresh process',case+': mask preference preserved',case+': saved Kabelgeul default text used',case+': layer color preference preserved'])
(out/'herstart.json').write_text(json.dumps({'Status':'PASS','Checks':checks,'Autoload':args.autoload},indent=2),encoding='utf-8');print(json.dumps({'Status':'PASS','Checks':len(checks),'Autoload':args.autoload}))
