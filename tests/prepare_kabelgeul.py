"""Extract the reviewed KabelGeul module, isolate symbols and add native UI bridges."""
import hashlib
import json
from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
source = root / 'Reference/Techbase lisp pakket.lsp'
text = source.read_text(encoding='utf-8')
text = text[:text.index(';;; Ingevoegd uit: LICHTMAST.LSP')]
text = text.replace('kg:', 'tbkg:').replace('KabelGeulOmniCodeSwitch', 'TechbaseKabelGeulCodeSwitch')
text = re.sub(r'(?i)\bC:([A-Z0-9_-]+)', lambda m: 'C:TBKG_' + m[1].upper(), text)
text = text.replace("'(7 . \"Standard\")", '(cons 7 *tbkg:style*)')
text = text.replace('(defun *error* (msg)', '(defun *error* (msg)\n  (TBKG_NATIVE_FINISH)\n  (command-s "_.UNDO" "_End")')

def replace_function(name, replacement):
    global text
    match = re.search(r'\(defun ' + re.escape(name) + r'\s', text, re.I)
    assert match, name
    start = match.start()
    depth = 0; quoted = False; escaped = False; comment = False
    for i in range(start, len(text)):
        c = text[i]
        if comment:
            if c == '\n': comment = False
            continue
        if escaped: escaped = False; continue
        if c == '\\' and quoted: escaped = True; continue
        if c == '"': quoted = not quoted; continue
        if not quoted:
            if c == ';': comment = True; continue
            if c == '(': depth += 1
            elif c == ')':
                depth -= 1
                if depth == 0:
                    text = text[:start] + replacement + text[i+1:]
                    return
    raise AssertionError(name)

replace_function('tbkg:make-layer', '(defun tbkg:make-layer (name color ltype) (TBKG_NATIVE_LAYER name color ltype))')
replace_function('tbkg:ensure-layers', '(defun tbkg:ensure-layers (/ d) (foreach d (tbkg:layer-defs) (tbkg:make-layer (car d) (cadr d) (caddr d))))')
replace_function('tbkg:clone-block-definition', '(defun tbkg:clone-block-definition (oldname newname) (TBKG_NATIVE_CLONE oldname newname))')
replace_function('tbkg:copy-insert-to', '(defun tbkg:copy-insert-to (insert-ent base target) (TBKG_NATIVE_COPY insert-ent base target))')
replace_function('tbkg:show-dialog', '''(defun tbkg:show-dialog (/ result)
  (setq result (read (TBKG_CONFIG_STRING (vl-prin1-to-string (list *tbkg:last-count* *tbkg:last-types* *tbkg:last-labels*
    *tbkg:last-doubles* *tbkg:last-mantels* *tbkg:last-splits* *tbkg:last-combis*
    *tbkg:last-existing-labels* *tbkg:last-triples* *tbkg:last-mantel-spec*
    *tbkg:last-twosideds* *tbkg:last-side2-doubles* *tbkg:last-side2-triples* *tbkg:last-side2-combis*)) *tbkg:action*)))
  (if result
    (progn
      (foreach layer '("E_LV_CROSS_SECTION" "E_LV_CABLE_POINT" "Mantelbuis") (TBKG_NATIVE_CHECK_LAYER layer))
      (foreach typ (cadr result)
        (TBKG_NATIVE_CHECK_LAYER (tbkg:type->layer typ))
        (TBKG_NATIVE_CHECK_LAYER (tbkg:type->text-layer typ))
        (if (= typ "Half rooien") (TBKG_NATIVE_CHECK_LAYER "01 - Bestaande kabel")))))
  result)'''.replace('(/ result)', '(/ result layer typ)'))
# Retain the original lookup (including floating reference codes), then guard the result.
text = text.replace('(defun tbkg:selected-insert ', '(defun tbkg:selected-insert-original ', 1)
text += '''
(defun tbkg:selected-insert (ent / candidate)
  (setq candidate (tbkg:selected-insert-original ent))
  (if candidate (TBKG_NATIVE_GUARD candidate) nil))
(defun tbkg:plugin-run (action / *error*)
  (defun *error* (msg)
    (TBKG_NATIVE_FINISH)
    (command-s "_.UNDO" "_End")
    (if (and msg (not (wcmatch (strcase msg) "*CANCEL*,*QUIT*"))) (princ (strcat "\\nKabelgeul: " msg)))
    (princ))
  (command "_.UNDO" "_Begin")
  (setq *tbkg:style* (TBKG_NATIVE_STYLE) *tbkg:action* action)
  (cond
    ((= action "KABELGEUL") (C:TBKG_KABELGEUL))
    ((= action "KABELGEULCOPY") (C:TBKG_KABELGEULCOPY))
    ((= action "KGEDIT") (C:TBKG_KGEDIT))
    ((= action "CODESWITCH") (C:TBKG_CODESWITCH)))
  (TBKG_NATIVE_FINISH)
  (command "_.UNDO" "_End")
  (princ))
'''
# Keep the exact cable type independently of the circle/text layers. MS Nieuw
# text intentionally uses K05; legacy icon detection can therefore confuse it.
needle = '(tbkg:draw-split-attribute (nth 5 item) text-ang text-just (nth 7 item) attr-index)'
assert needle in text
text = text.replace(needle, needle + '''
    (tbkg:make-attribute (nth 5 item) 0.10 text-ang (tbkg:index-tag "TBTYPE" attr-index)
      "Kabeltype:" (car item) "E_LV_CROSS_SECTION" text-just 3)''')
needle = '(if has-half-attribute (setq typ "Half rooien"))'
assert needle in text
text = text.replace(needle, '''(if (tbkg:block-option-metadata blkrec "TBTYPE" (car item))
      (setq typ (cadr (tbkg:block-option-metadata blkrec "TBTYPE" (car item)))))
    ''' + needle)
# Loading defines functionality only. Do not change drawing settings or install a
# second attribute reactor beside the user's independently loaded original Lisp.
text = re.sub(r'^\(tbkg:hide-wipeout-frames\)\s*$', '', text, flags=re.M)
text = re.sub(r'^\(tbkg:install-mask-reactor\)\s*$', '', text, flags=re.M)
text = re.sub(r'^\(princ \(strcat "\\nKabelGeul_Omni geladen:.*$', lambda m: '(princ "\\nTechbase Kabelgeul geladen.")', text, flags=re.M)
text += '\n(setq *tbkg:ready* T)\n(princ)\n'
for command, action in [('TBKABELGEUL','KABELGEUL'), ('TBGEULCOPY','KABELGEULCOPY'), ('TBGEULEDIT','KGEDIT'), ('TBGEULCODE','CODESWITCH')]:
    text += f'\n(defun C:{command} () (vl-cmdf "TBGEULINIT") (tbkg:plugin-run "{action}") (princ))\n'
target = root / 'Assets/Kabelgeul/Techbase.Kabelgeul.lsp'
target.parent.mkdir(parents=True, exist_ok=True)
target.write_text('\n'.join(line.rstrip() for line in text.splitlines()).rstrip() + '\n', encoding='utf-8')
(target.parent / 'bronversie.json').write_text(json.dumps({
    'Source': 'Reference/Techbase lisp pakket.lsp', 'SourceSHA256': hashlib.sha256(source.read_bytes()).hexdigest(),
    'GeneratedSHA256': hashlib.sha256(target.read_bytes()).hexdigest(),
    'Module': 'KabelGeul Omni 2026-08-06ad',
    'Adapters': ['private command/function namespace', 'managed layer/copy helpers', 'Arial Nova Light', 'managed configuration UI', 'host block guards', 'no load-time drawing changes', 'exact cable type metadata', 'restore command state', 'Undo Begin/End']
}, indent=2), encoding='utf-8')
