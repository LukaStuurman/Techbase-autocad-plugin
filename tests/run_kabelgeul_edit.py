"""Rebuild real direct/floating geuls and inspect CAD, copies, transforms and aborts."""
import json
import os
from pathlib import Path
import subprocess
import sys
from package_paths import ROOT, BUNDLE, dll

sys.stdout.reconfigure(encoding='utf-8')
out = ROOT / 'validation/Kabelgeul'
edit = {'Rows': [
    {'Type': 'K10', 'Text': 'Ri 10 150Al gewijzigd', 'Shape': 'Drie', 'Mantel': 'X'},
    {'Type': 'MS Bestaand', 'Text': 'MS 95Cu gewijzigd', 'Shape': 'Combi', 'TwoSides': True, 'Text2': 'Zijde 2 gewijzigd', 'Shape2': 'Drie', 'Mantel': 'B', 'NewTray': True},
    {'Type': 'Rooien', 'Text': '50Cu gewijzigd', 'Shape': 'Dubbel', 'Mantel': 'B'}], 'MantelSpec': 'HDPE 200 gewijzigd'}
edit_path = out / 'edit-input.json'
edit_path.write_text(json.dumps(edit, indent=2), encoding='utf-8')
checks = []
for case in ['edit', 'cancel']:
    lines = ['_NETLOAD', f'"{dll().as_posix()}"', f'(load "{(BUNDLE / "Contents/Assets/Kabelgeul/Techbase.Kabelgeul.lsp").as_posix()}")',
        '(setvar "OSMODE" 0)', '(setvar "WIPEOUTFRAME" 2)', '(setvar "ATTREQ" 0)',
        '(entmake (list (cons 0 "LINE") (cons 10 (list 0.0 0.0 0.0)) (cons 11 (list 80.0 0.0 0.0))))',
        '_ZOOM', '_E', '_REGEN', '_TBKABELGEUL', '10,0,0', '10,8,0', '_Direct',
        '(setq original (entlast) olddef (cdr (assoc 2 (entget original))))',
        '(setq untouched (TBKG_NATIVE_COPY original (list 0.0 0.0 0.0) (list 20.0 0.0 0.0)))',
        '(entmod (subst (cons 50 (/ pi 2.0)) (assoc 50 (entget original)) (entget original)))',
        '(entmod (subst (cons 41 1.5) (assoc 41 (entget original)) (entget original)))',
        '(entmod (subst (cons 42 1.5) (assoc 42 (entget original)) (entget original)))',
        '(entmod (subst (cons 43 1.5) (assoc 43 (entget original)) (entget original)))',
        '(entupd original)', '_ZOOM', '_E', '_REGEN',
        '_UCS', '_Origin', '100,200,0', '_UCS', '_Z', '90',
        '_TBGEULEDIT', '-192,84,0', '_TBGEULEDIT', '-192,84,0',
        '(princ (strcat "\nOLD_HANDLE=" (cdr (assoc 5 (entget original)))))',
        '(princ (strcat "\nCOPY_UNCHANGED=" (if (= olddef (cdr (assoc 2 (entget untouched)))) "PASS" "FAIL")))',
        '(princ (strcat "\nDEFINITION_CHANGED=" (if (= olddef (cdr (assoc 2 (entget original)))) "NO" "YES")))',
        '(princ (strcat "\nUCS_ORIGIN=" (vl-prin1-to-string (getvar "UCSORG")) " UCS_X=" (vl-prin1-to-string (getvar "UCSXDIR"))))',
        '_UCS', '_W',
        # Floating geul remembers its own code position, irrespective of the global toggle.
        '(setq *tbkg:future-code-switch* T)', '_TBKABELGEUL', '50,0,0', '50,8,0', '_Zwevend', '50,20,0',
        '(setq floating (entlast) codebefore (tbkg:insert-attribute-value floating "ZWEVEND_CODE"))',
        '(setq *tbkg:future-code-switch* nil)', '_ZOOM', '_E', '_REGEN', '_TBGEULEDIT', '50,19.5,0',
        '(princ (strcat "\nCODE_KEPT=" (if (= codebefore (tbkg:insert-attribute-value floating "ZWEVEND_CODE")) "PASS" "FAIL")))',
        '(princ (strcat "\nSWAP_KEPT=" (cadr (tbkg:block-option-metadata (tblobjname "BLOCK" (cdr (assoc 2 (entget floating)))) "TB_CODESWAP" 0))))']
    # Explicitly provoke a failed commit after creating a temporary rebuild.
    # Finish must clean up only that rebuild, preserving the original and its copy.
    if case == 'edit':
        lines += ['(setq defbeforefailure (cdr (assoc 2 (entget original))))',
            '(TBKG_NATIVE_STYLE)',
            '(TBKG_NATIVE_BEGIN_EDIT original)',
            '(setq abortdata (tbkg:copy-data-from-insert original) *tbkg:copy-stage* nil)',
            '(setq temporary (apply (function tbkg:draw-trench-at-end) (append (list (list 0.0 -8.0 0.0) (list 0.0 0.0 0.0)) (cdr abortdata))))',
            '(princ (strcat "\nTEMP_CREATED=" (if temporary "PASS" "FAIL")))',
            '(vl-catch-all-apply (function TBKG_NATIVE_COMMIT_EDIT) (list original))',
            '(TBKG_NATIVE_FINISH)',
            '(princ (strcat "\nFAILURE_KEPT=" (if (= defbeforefailure (cdr (assoc 2 (entget original)))) "PASS" "FAIL")))']
    lines += ['(princ (strcat "\nINSERT_COUNT=" (itoa (sslength (ssget "_X" (list (cons 0 "INSERT")))))))',
        '(princ (strcat "\nFRAME=" (itoa (getvar "WIPEOUTFRAME")) " CURRENT=" (getvar "CLAYER") " ATTREQ=" (itoa (getvar "ATTREQ")) " CMDECHO=" (itoa (getvar "CMDECHO"))))',
        '_DXFOUT', f'"{(out / f"Kabelgeul - {case}.dxf").as_posix()}"', '16', '_QUIT', '_Y']
    # TB_CODESWAP is a single constant tag, not an indexed row tag.
    lines = [l.replace('(cadr (tbkg:block-option-metadata (tblobjname "BLOCK" (cdr (assoc 2 (entget floating)))) "TB_CODESWAP" 0))',
        '(cdr (assoc 1 (entget (tbkg:block-attdef-ent (tblobjname "BLOCK" (cdr (assoc 2 (entget floating)))) "TB_CODESWAP"))))') for l in lines]
    script = out / f'{case}.scr'
    script.write_text('\n'.join(l.replace('"\n','"\\n') for l in lines)+'\n', encoding='ascii')
    raw = out / f'{case}.raw.log'
    with raw.open('wb') as log:
        result = subprocess.run([r'C:\Program Files\Autodesk\AutoCAD 2026\accoreconsole.exe','/s',str(script),'/l','en-US'],
            cwd=out, env=dict(os.environ,TECHBASE_KABELGEUL_TEST_CONFIG=str(out/'input.json'), TECHBASE_KABELGEUL_TEST_EDIT=str(edit_path) if case=='edit' else 'CANCEL', TECHBASE_SETTINGS_FILE=str(out/'isolated-settings.json')),
            stdout=log,stderr=subprocess.STDOUT,timeout=120)
    log = raw.read_bytes().decode('utf-16-le',errors='replace').replace('\x00','')
    (out/f'{case}.log').write_text('\n'.join(l.rstrip() for l in log.splitlines()).rstrip()+'\n',encoding='utf-8')
    expected=['COPY_UNCHANGED=PASS','CODE_KEPT=PASS','SWAP_KEPT=1','INSERT_COUNT=3','FRAME=2 CURRENT=0 ATTREQ=0 CMDECHO=1',
        'UCS_ORIGIN=(100.0 200.0 0.0)', 'DEFINITION_CHANGED='+('YES' if case=='edit' else 'NO')]
    if case=='edit': expected+=['Kabelgeul volledig bijgewerkt.','FAILURE_KEPT=PASS','TEMP_CREATED=PASS']
    else: expected+=['Kabelgeul bewerken geannuleerd.']
    missing=[x for x in expected if x not in log]
    if result.returncode!=0 or missing: print(log[-13000:]); raise AssertionError((case,result.returncode,missing))
    checks += [case+': '+e for e in expected]

    rows=(out/f'Kabelgeul - {case}.dxf').read_text(encoding='utf-8',errors='replace').splitlines()
    records=[]; record=[]; section=''
    for i in range(0,len(rows)-1,2):
        pair=int(rows[i].strip()),rows[i+1].strip()
        if pair[0]==2 and record==[(0,'SECTION')]: section=pair[1]
        if pair[0]==0:
            if record: records.append((section,record))
            record=[]
            if pair[1]=='ENDSEC': section=''
        record.append(pair)
    entities=[r for s,r in records if s=='ENTITIES']
    inserts=[dict(r) for r in entities if r[0]==(0,'INSERT')]
    assert len(inserts)==3,inserts
    old=next(r for r in inserts if abs(float(r[10])-10)<1e-7)
    copy=next(r for r in inserts if abs(float(r[10])-30)<1e-7)
    assert old[50]=='90.0' and all(abs(float(old[c])-1.5)<1e-8 for c in (41,42,43)),old
    assert abs(float(old[20])-8)<1e-8
    assert (copy[2]!=old[2]) if case=='edit' else (copy[2]==old[2])
    checks += [case+': saved drawing preserves insert transform and independent shared copy']
    attrs=[dict(r) for r in entities if r[0]==(0,'ATTRIB')]
    changed=[r for r in attrs if 'gewijzigd' in r.get(1,'')]
    assert bool(changed)==(case=='edit'),changed
    if case=='edit':
        assert any('Ri 10 150Al gewijzigd'==r.get(1) for r in attrs)
        assert any('Ri 1 150Al'==r.get(1) for r in attrs),'untouched copy text'
        assert not any('TEMPORARY' in r.get(1,'') for r in attrs)
        assert all(r.get(7,'').startswith('TECHBASE_ARIAL_NOVA_LIGHT') for r in attrs)
        checks += ['edited texts, retained copy text, Light font and aborted temporary objects read back from DXF']
        blocks={}; current=None
        for section, record in records:
            if section!='BLOCKS': continue
            item=dict(record)
            if item.get(0)=='BLOCK': current=item[2]; blocks[current]=[]
            elif item.get(0)=='ENDBLK': current=None
            elif current: blocks[current].append(record)
        changed_block=[dict(r) for r in blocks[old[2]]]
        symbols=[r for r in changed_block if r.get(0)=='CIRCLE' and r.get(8)=='Aansluiting LS K10']
        assert len(symbols)==3,symbols
        assert any(r.get(0)=='CIRCLE' and r.get(8)=='MS Bestaand' for r in changed_block)
        metadata={r[2]:r[1] for r in changed_block if r.get(0)=='ATTDEF'}
        k10=next(k.split('_')[-1] for k,v in metadata.items() if k.startswith('TBTYPE_') and v=='K10')
        ms=next(k.split('_')[-1] for k,v in metadata.items() if k.startswith('TBTYPE_') and v=='MS Bestaand')
        assert metadata['DRIE_'+k10]=='1',metadata
        assert metadata['TWEEZIJDIG_'+ms]=='1' and metadata['COMBI_'+ms]=='1' and metadata['DRIE2_'+ms]=='1',metadata
        assert metadata['VOLG_'+ms]=='1' and metadata['MANTEL_'+ms]=='B',metadata
        checks += ['saved new K10 icon has three circles; MS Bestaand icons and two-sided shapes, mantel and new tray match edited data']
        leader=next(r for r in blocks[old[2]] if dict(r).get(0)=='LWPOLYLINE' and dict(r).get(8)=='E_LV_CROSS_SECTION' and dict(r).get(90)=='6')
        xs=[float(v) for k,v in leader if k==10]; ys=[float(v) for k,v in leader if k==20]
        assert abs(xs[0])<1e-7 and abs(ys[0]+8)<1e-7 and abs(xs[1])<1e-7 and abs(ys[1])<1e-7
        assert old[5] in log.split('OLD_HANDLE=',1)[-1]
        assert len([r for r in blocks[copy[2]] if dict(r).get(0)=='CIRCLE' and dict(r).get(8)=='Aansluiting LS K01'])>0
        checks += ['saved leader retains endpoints and original insert handle; shared copy retains old K01 icons']
print(json.dumps({'Status':'PASS','Checks':len(checks)}))
(out/'editing.json').write_text(json.dumps({'Status':'PASS','Checks':checks},indent=2),encoding='utf-8')
