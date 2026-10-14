"""Actual LS/OV bursts and automatic stacks with editable profiles and mask variants."""
import argparse,json,os,subprocess
from package_paths import ROOT,dll
parser=argparse.ArgumentParser();parser.add_argument('--autoload',action='store_true');args=parser.parse_args()
out=ROOT/'validation/Tekstopmaak';out.mkdir(exist_ok=True);checks=[]
for case,height,normal,existing,mask,border,drawing,rgb in [('045',.45,4.8,7.8,True,1.3,True,0xFFFFFF),('075',.75,7.7,10.7,True,1.3,True,0xFFFFFF),('custom',.75,8.3,11.9,True,1.65,False,0x14283C),('off',.75,8.3,11.9,False,1.65,False,0x14283C)]:
    settings=out/f'{case}-settings.json';settings.write_text(json.dumps({'DirectionTextHeight':height,'DirectionTextWidth045':normal,'DirectionTextWidth075':normal,'ExistingDirectionTextWidth045':existing,'ExistingDirectionTextWidth075':existing,'DirectionTextMask':mask,'DirectionTextMaskBorder':border,'DirectionTextMaskDrawingColor':drawing,'DirectionTextMaskRgb':rgb}),encoding='utf-8')
    lines=([] if args.autoload else ['_NETLOAD',f'"{dll().as_posix()}"'])+['(setvar "OSMODE" 0)','(setvar "INSUNITS" 0)','_TBTEKSTLS','0,0,0','_TBTEKSTOV','30,0,0',
        '(entmake (list (cons 0 "LINE") (cons 8 "Aansluiting LS K10") (cons 10 (list 0.0 20.0 0.0)) (cons 11 (list 20.0 20.0 0.0))))',
        '(entmake (list (cons 0 "LINE") (cons 8 "01 - Bestaande kabel") (cons 10 (list 0.0 22.0 0.0)) (cons 11 (list 20.0 22.0 0.0))))',
        '_TBAUTOTEKSTLS','_TweePunten','5,19,0','5,23,0','5,25,0','_TBAUTOTEKSTOV','_TweePunten','5,19,0','5,23,0','10,25,0',
        '_DXFOUT',f'"{(out/f"Opmaak-{case}.dxf").as_posix()}"','16','_QUIT','_Y']
    script=out/f'{case}.scr';script.write_text('\n'.join(lines)+'\n',encoding='ascii');raw=out/f'{case}.raw.log'
    with raw.open('wb') as log:
        subprocess.run([r'C:\Program Files\Autodesk\AutoCAD 2026\accoreconsole.exe','/s',str(script),'/l','en-US'],cwd=out,env=dict(os.environ,TECHBASE_SETTINGS_FILE=str(settings)),stdout=log,stderr=subprocess.STDOUT,check=True,timeout=120)
    log=raw.read_bytes().decode('utf-16-le',errors='replace').replace('\x00','');(out/f'{case}.log').write_text('\n'.join(l.rstrip() for l in log.splitlines()).rstrip()+'\n',encoding='utf-8')
    assert '2 LS-richtingsteksten geplaatst.' in log and '2 OV-richtingsteksten geplaatst.' in log,log[-5000:]
    rows=(out/f'Opmaak-{case}.dxf').read_text(errors='replace').splitlines();records=[];rec=[];section=''
    for i in range(0,len(rows)-1,2):
        pair=int(rows[i].strip()),rows[i+1].strip()
        if pair[0]==2 and rec==[(0,'SECTION')]:section=pair[1]
        if pair[0]==0:
            if rec and section=='ENTITIES':records.append(rec)
            rec=[]
            if pair[1]=='ENDSEC':section=''
        rec.append(pair)
    texts=[{k:next(v for c,v in r if c==k) for k,_ in r} for r in records if r[0]==(0,'MTEXT')];assert len(texts)==30
    for t in texts:
        assert abs(float(t[40])-height)<1e-8 and abs(float(t[41])-(existing if t[8]=='01 - Bestaande kabel' else normal))<1e-8,t
        flag=int(t.get(90,0));assert bool(flag&1)==mask,t
        if mask:
            assert abs(float(t[45])-border)<1e-8 and bool(flag&2)==drawing,t
            if not drawing:assert int(t[421])==rgb,t
    checks.extend([case+': actual burst and automatic LS/OV placement',case+': saved normal/existing widths and height',case+': saved mask toggle, border and drawing/RGB color'])
(out/'commands.json').write_text(json.dumps({'Status':'PASS','Checks':checks,'Autoload':args.autoload},indent=2),encoding='utf-8')
print(json.dumps({'Status':'PASS','Checks':len(checks),'Autoload':args.autoload}))
