"""Actual directional placement, unchanged editing and repeated copy keep logical row order."""
import argparse,json,os,subprocess
from pathlib import Path
from package_paths import ROOT,BUNDLE,dll
parser=argparse.ArgumentParser();parser.add_argument('--autoload',action='store_true');args=parser.parse_args()
out=ROOT/'validation/Kabelgeul';out.mkdir(exist_ok=True);checks=[]
config=out/'order-input.json';config.write_text(json.dumps({'Rows':[{'Type':'K01','Text':'A','TwoSides':True,'Text2':'A2','Mantel':'X'},{'Type':'K10','Text':'B','Shape':'Combi','NewTray':True},{'Type':'K12','Text':'C','Shape':'Drie'},{'Type':'Rooien','Text':'D','NewTray':True}],'MantelSpec':'Eigen 160'}),encoding='utf-8')
installed=Path(os.environ['APPDATA'])/'Autodesk/ApplicationPlugins/Techbase.Blocks.bundle';bundle=installed if args.autoload else BUNDLE
for case,end,half,p1,p2 in [('down','0,-8,0','0,-4,0',(-100,0),(100,0)),('up','0,8,0','0,4,0',(-100,0),(100,0)),('right','8,0,0','4,0,0',(0,-100),(0,100)),('left','-8,0,0','-4,0,0',(0,-100),(0,100))]:
    start,endpoint=[float(v) for v in end.split(',')[:2]],[float(v) for v in end.split(',')[:2]]
    target=f'30,30,0'; targetend=f'{30+endpoint[0]},{30+endpoint[1]},0';targethalf=f'{30+endpoint[0]/2},{30+endpoint[1]/2},0'
    nexttarget='60,60,0';nextend=f'{60+endpoint[0]},{60+endpoint[1]},0'
    lines=([] if args.autoload else ['_NETLOAD',f'"{dll().as_posix()}"'])+[f'(load "{(bundle/"Contents/Assets/Kabelgeul/Techbase.Kabelgeul.lsp").as_posix()}")',
        '(setvar "OSMODE" 0)','(setvar "ATTREQ" 0)',f'(entmake (list (cons 0 "LINE") (cons 10 (list {p1[0]}.0 {p1[1]}.0 0.0)) (cons 11 (list {p2[0]}.0 {p2[1]}.0 0.0))))',
        '_ZOOM','_E','_REGEN','_TBKABELGEUL','0,0,0',end,'_Direct',
        '(setq original (entlast) expected (tbkg:copy-data-from-insert original))',
        '(princ (strcat "\\nORDER=" (vl-prin1-to-string (cadr expected))))',
        '_ZOOM','_E','_REGEN','_TBGEULEDIT',half,'_TBGEULEDIT',half,
        '(princ (strcat "\\nEDIT_ORDER=" (if (equal expected (tbkg:copy-data-from-insert original)) "PASS" "FAIL")))',
        # Temporary fixture lines serve as the new cable selected during each copy.
        '(entmake (list (cons 0 "LINE") (cons 10 (list 20.0 30.0 0.0)) (cons 11 (list 40.0 30.0 0.0))))' if endpoint[0]==0 else '(entmake (list (cons 0 "LINE") (cons 10 (list 30.0 20.0 0.0)) (cons 11 (list 30.0 40.0 0.0))))',
        '_ZOOM','_E','_REGEN','_TBGEULCOPY',half,target,targetend,'_Direct',
        '(setq copied (entlast))','(princ (strcat "\\nCOPY_ORDER=" (if (equal expected (tbkg:copy-data-from-insert copied)) "PASS" "FAIL")))',
        '(entmake (list (cons 0 "LINE") (cons 10 (list 50.0 60.0 0.0)) (cons 11 (list 70.0 60.0 0.0))))' if endpoint[0]==0 else '(entmake (list (cons 0 "LINE") (cons 10 (list 60.0 50.0 0.0)) (cons 11 (list 60.0 70.0 0.0))))',
        '_ZOOM','_E','_REGEN','_TBGEULCOPY',targethalf,nexttarget,nextend,'_Direct',
        '(princ (strcat "\\nREPEAT_ORDER=" (if (equal expected (tbkg:copy-data-from-insert (entlast))) "PASS" "FAIL")))',
        '_DXFOUT',f'"{(out/f"Volgorde-{case}.dxf").as_posix()}"','16','_QUIT','_Y']
    script=out/f'order-{case}.scr';script.write_text('\n'.join(lines)+'\n',encoding='ascii');raw=out/f'order-{case}.raw.log'
    with raw.open('wb') as log:subprocess.run([r'C:\Program Files\Autodesk\AutoCAD 2026\accoreconsole.exe','/s',str(script),'/l','en-US'],cwd=out,env=dict(os.environ,TECHBASE_KABELGEUL_TEST_CONFIG=str(config),TECHBASE_SETTINGS_FILE=str(out/'isolated-settings.json')),stdout=log,stderr=subprocess.STDOUT,check=True,timeout=120)
    log=raw.read_bytes().decode('utf-16-le',errors='replace').replace('\x00','');(out/f'order-{case}.log').write_text('\n'.join(l.rstrip() for l in log.splitlines()).rstrip()+'\n',encoding='utf-8')
    for marker in ['ORDER=("K01" "K10" "K12" "Rooien")','EDIT_ORDER=PASS','COPY_ORDER=PASS','REPEAT_ORDER=PASS']:
        assert marker in log,(case,marker,log[-10000:]);checks.append(case+': '+marker)
    rawrows=(out/f'Volgorde-{case}.dxf').read_text(errors='replace').splitlines();entities=[];rec=[];section=''
    for i in range(0,len(rawrows)-1,2):
        pair=int(rawrows[i].strip()),rawrows[i+1].strip()
        if pair[0]==2 and rec==[(0,'SECTION')]:section=pair[1]
        if pair[0]==0:
            if rec and section=='ENTITIES':entities.append(dict(rec))
            rec=[]
            if pair[1]=='ENDSEC':section=''
        rec.append(pair)
    geuls=[r for r in entities if r.get(0)=='INSERT'];assert len(geuls)==3
    positions={}
    for g in geuls:
        positions[g[5]]={}
        for r in entities:
            if r.get(0)=='ATTRIB' and r.get(330)==g[5] and r.get(2,'').startswith(('KABEL_','ZIJDE2_')):
                code=11 if int(r.get(72,0)) else 10
                positions[g[5]][r[1]]=(float(r[code])-float(g[10]),float(r[code+10])-float(g[20]))
    reference=positions[geuls[0][5]];assert set(reference)=={'A','A2','B','C','D'}
    assert all(all(abs(p[0]-reference[t][0])<1e-7 and abs(p[1]-reference[t][1])<1e-7 for t,p in positions[g[5]].items()) for g in geuls)
    checks.append(case+': saved copied cable texts keep the same position/order relative to the leader')
(out/'order.json').write_text(json.dumps({'Status':'PASS','Checks':checks,'Autoload':args.autoload},indent=2),encoding='utf-8');print(json.dumps({'Status':'PASS','Checks':len(checks),'Autoload':args.autoload}))
