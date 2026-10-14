"""Actual saved layer color command, both preferences and protected layers."""
import argparse, hashlib, json, os, subprocess
from package_paths import ROOT, dll
parser=argparse.ArgumentParser(); parser.add_argument('--autoload',action='store_true'); args=parser.parse_args()
out=ROOT/'validation/AutomatischeTeksten'; out.mkdir(exist_ok=True)
checks=[]
source=out/'Automatische teksten - controle.dwg'; source_hash=hashlib.sha256(source.read_bytes()).hexdigest()
xref_name=source.stem+'|01 - Bestaande kabel'
for black in [False,True]:
    label='black' if black else 'green'; settings=out/f'{label}-settings.json'
    settings.write_text(json.dumps({'ExistingCableBlack':black}),encoding='utf-8')
    lines=([] if args.autoload else ['_NETLOAD',f'"{dll().as_posix()}"'])+[
        '(entmake (list (cons 0 "LAYER") (cons 100 "AcDbSymbolTableRecord") (cons 100 "AcDbLayerTableRecord") (cons 2 "01 - Bestaande kabel") (cons 70 0) (cons 62 3) (cons 6 "Continuous")))',
        '(entmake (list (cons 0 "LAYER") (cons 100 "AcDbSymbolTableRecord") (cons 100 "AcDbLayerTableRecord") (cons 2 "Bestaande kabel OV") (cons 70 4) (cons 62 3) (cons 6 "Continuous")))',
        f'(command "_-XREF" "_Attach" "{source.as_posix()}" "0,0,0" "1" "1" "0")',
        '(entmake (list (cons 0 "LAYER") (cons 100 "AcDbSymbolTableRecord") (cons 100 "AcDbLayerTableRecord") (cons 2 "Te rooien kabel") (cons 70 0) (cons 62 3) (cons 6 "Continuous")))',
        '(entmake (list (cons 0 "LAYER") (cons 100 "AcDbSymbolTableRecord") (cons 100 "AcDbLayerTableRecord") (cons 2 "Bestaand") (cons 70 0) (cons 62 3) (cons 6 "Continuous")))',
        '_TBBESTAANDEKLEUR',
        '(princ (strcat "\\nRGB=" (if (assoc 420 (entget (tblobjname "LAYER" "01 - Bestaande kabel"))) (itoa (cdr (assoc 420 (entget (tblobjname "LAYER" "01 - Bestaande kabel")))) ) "ACI")))',
        '(princ (strcat "\\nBESTAAND=" (itoa (cdr (assoc 62 (tblsearch "LAYER" "Bestaand"))))))',
        '(princ (strcat "\\nBESTAAND_RGB=" (if (assoc 420 (entget (tblobjname "LAYER" "Bestaand"))) (itoa (cdr (assoc 420 (entget (tblobjname "LAYER" "Bestaand")))) ) "ACI")))',
        f'(princ (strcat "\\nCOLOR=" (itoa (cdr (assoc 62 (tblsearch "LAYER" "01 - Bestaande kabel"))))))',
        f'(princ (strcat "\\nLOCKED=" (itoa (cdr (assoc 62 (tblsearch "LAYER" "Bestaande kabel OV"))))))',
        f'(princ (strcat "\\nXREF=" (itoa (cdr (assoc 62 (tblsearch "LAYER" "{xref_name}"))))))',
        f'(princ (strcat "\\nOTHER=" (itoa (cdr (assoc 62 (tblsearch "LAYER" "Te rooien kabel"))))))',
        '_DXFOUT',f'"{(out/f"Kleur-{label}.dxf").as_posix()}"','16','_QUIT','_Y']
    script=out/f'kleur-{label}.scr'; script.write_text('\n'.join(lines)+'\n',encoding='ascii')
    raw=out/f'kleur-{label}.raw.log'
    with raw.open('wb') as log:
        subprocess.run([r'C:\Program Files\Autodesk\AutoCAD 2026\accoreconsole.exe','/s',str(script),'/l','en-US'],cwd=out,
            env=dict(os.environ,TECHBASE_SETTINGS_FILE=str(settings)),stdout=log,stderr=subprocess.STDOUT,check=True,timeout=120)
    text=raw.read_bytes().decode('utf-16-le',errors='replace').replace('\x00','')
    (out/f'kleur-{label}.log').write_text('\n'.join(l.rstrip() for l in text.splitlines()).rstrip()+'\n',encoding='utf-8')
    for marker in (["RGB=0","BESTAAND_RGB=0"] if black else ["RGB=ACI","COLOR=125","BESTAAND_RGB=ACI","BESTAAND=125"])+['LOCKED=3','XREF=125','OTHER=3','1 vergrendelde lagen overgeslagen.']:
        assert marker in text,(marker,text[-7000:]); checks.append(f'{label}: {marker}')
    rows=(out/f'Kleur-{label}.dxf').read_text(errors='replace').splitlines(); rec=[]; records=[]
    for i in range(0,len(rows)-1,2):
        pair=int(rows[i].strip()),rows[i+1].strip()
        if pair[0]==0:
            if rec: records.append(rec)
            rec=[]
        rec.append(pair)
    layers={dict(r)[2]:dict(r) for r in records if r[0]==(0,'LAYER')}
    for name in ['01 - Bestaande kabel','Bestaand']:
        if black:assert layers[name].get(420)=='0',layers[name]
        else:assert layers[name][62]=='125' and 420 not in layers[name],layers[name]
    assert all(layers[n][62]=='3' for n in ['Bestaande kabel OV','Te rooien kabel'])
    assert layers[xref_name][62]=='125'
    assert hashlib.sha256(source.read_bytes()).hexdigest()==source_hash
    checks.append(label+': independently read saved DXF layer colors')
(out/'color-commands.json').write_text(json.dumps({'Status':'PASS','Checks':checks,'Autoload':args.autoload},indent=2),encoding='utf-8')
print(json.dumps({'Status':'PASS','Checks':len(checks),'Autoload':args.autoload}))
