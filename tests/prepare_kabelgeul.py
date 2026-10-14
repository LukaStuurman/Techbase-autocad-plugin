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
# Native forms now expose the complete final label. Drawing and rereading must
# preserve it verbatim, including custom/empty labels and legacy Ri prefixes.
replace_function('tbkg:prefixed-label', '(defun tbkg:prefixed-label (typ txt) (if txt txt ""))')
replace_function('tbkg:strip-known-prefix', '(defun tbkg:strip-known-prefix (txt) (if txt txt ""))')
# Attribute tags follow readable top-to-bottom order, which can be opposite to
# the input rows. Recover rows along the stored incoming leader, in block space.
replace_function('tbkg:block-cross-angle', '''(defun tbkg:block-cross-angle (blkrec / next data pts)
  (setq next (entnext blkrec))
  (while (and next (null pts))
    (setq data (entget next))
    (cond
      ((and (= (cdr (assoc 0 data)) "LWPOLYLINE") (= (cdr (assoc 8 data)) "E_LV_CROSS_SECTION"))
       (setq pts (tbkg:lwpolyline-points data))
       (if (/= (length pts) 6) (setq pts nil)))
      ((= (cdr (assoc 0 data)) "ENDBLK") (setq next nil)))
    (if (and next (null pts)) (setq next (entnext next))))
  (if pts (angle (car pts) (cadr pts)) 0.0))''')
needle='(setq items (tbkg:copy-attr-items insert-ent))'
assert needle in text
text=text.replace(needle, needle+'''
  (setq items (vl-sort items '(lambda (a b / ap bp ax bx)
    (setq ap (nth 3 a) bp (nth 3 b))
    (if (and (tbkg:point-p ap) (tbkg:point-p bp))
      (progn
        (setq ax (+ (* (car ap) (cos ang)) (* (cadr ap) (sin ang))))
        (setq bx (+ (* (car bp) (cos ang)) (* (cadr bp) (sin ang))))
        (if (equal ax bx 1e-8) (< (car a) (car b)) (< ax bx)))
      (< (car a) (car b))))))''')
text=text.replace('(setq out-splits (if has-split-metadata (reverse splits) (tbkg:groupids->splits (reverse groupids))))', '(setq out-splits (if has-split-metadata splits (tbkg:groupids->splits groupids)))')
needle='''(list (length types) (reverse types) (reverse labels) (reverse doubles)
            (reverse mantels) out-splits (reverse combis)
            (reverse existing-labels) (reverse triples) mantel-spec
            (reverse twosideds) (reverse side2doubles)
            (reverse side2triples) (reverse side2combis))'''
assert needle in text
text=text.replace(needle, '''(list (length types) types labels doubles mantels out-splits combis
            existing-labels triples mantel-spec twosideds side2doubles side2triples side2combis)''')
replace_function('tbkg:show-dialog', '''(defun tbkg:show-dialog (/ result)
  (setq result (read (TBKG_CONFIG_STRING (vl-prin1-to-string (list *tbkg:last-count* *tbkg:last-types* *tbkg:last-labels*
    *tbkg:last-doubles* *tbkg:last-mantels* *tbkg:last-splits* *tbkg:last-combis*
    *tbkg:last-existing-labels* *tbkg:last-triples* *tbkg:last-mantel-spec*
    *tbkg:last-twosideds* *tbkg:last-side2-doubles* *tbkg:last-side2-triples* *tbkg:last-side2-combis*)) (if *tbkg:copy-stage* "KABELGEULCOPY" *tbkg:action*))))
  (if result
    (progn
      (foreach layer '("E_LV_CROSS_SECTION" "E_LV_CABLE_POINT" "Mantelbuis") (TBKG_NATIVE_CHECK_LAYER layer))
      (foreach typ (cadr result)
        (TBKG_NATIVE_CHECK_LAYER (tbkg:type->layer typ))
        (TBKG_NATIVE_CHECK_LAYER (tbkg:type->text-layer typ))
        (if (= typ "Half rooien") (TBKG_NATIVE_CHECK_LAYER "01 - Bestaande kabel")))))
  result)'''.replace('(/ result)', '(/ result layer typ)'))
replace_function('C:TBKG_KGEDIT', '''(defun C:TBKG_KGEDIT (/ *error* sel original data context replacement oldfloating oldswitch world-set)
  (setq oldfloating *tbkg:floating-code* oldswitch *tbkg:future-code-switch*)
  (defun *error* (msg)
    (setq *tbkg:floating-code* oldfloating *tbkg:future-code-switch* oldswitch *tbkg:copy-stage* nil)
    (TBKG_NATIVE_FINISH)
    (if world-set (command-s "_.UCS" "_Previous"))
    (command-s "_.UNDO" "_End")
    (if (and msg (not (wcmatch (strcase msg) "*CANCEL*,*QUIT*"))) (princ (strcat "\\nKabelgeul bewerken: " msg)))
    (princ))
  (setq sel (entsel "\\nSelecteer KabelGeul of zwevende referentie om volledig te bewerken: "))
  (if (and sel (setq original (tbkg:selected-insert (car sel))))
    (progn
      (setq data (tbkg:copy-data-from-insert original) *tbkg:copy-stage* nil)
      (if data
        (progn
          (tbkg:seed-dialog-data data)
          (setq *tbkg:action* "KABELGEULEDIT" data (tbkg:show-dialog))
          (if data
            (progn
              (command "_.UCS" "_World")
              (setq world-set T)
              (setq context (read (TBKG_NATIVE_BEGIN_EDIT original)))
              (setq *tbkg:floating-code* (if (= (nth 2 context) "") nil (nth 2 context)) *tbkg:future-code-switch* (nth 3 context))
              (setq replacement (tbkg:draw-trench-at-end (car context) (cadr context)
                (cadr data) (caddr data) (cadddr data) (nth 4 data) (nth 5 data) (nth 6 data) (nth 7 data)
                (nth 8 data) (nth 9 data) (nth 10 data) (nth 11 data) (nth 12 data) (nth 13 data)))
              (if replacement
                (progn (TBKG_NATIVE_COMMIT_EDIT replacement) (princ "\\nKabelgeul volledig bijgewerkt."))
                (princ "\\nGeul kon niet opnieuw worden opgebouwd; oorspronkelijke geul behouden."))
              (command "_.UCS" "_Previous")
              (setq world-set nil))
            (princ "\\nKabelgeul bewerken geannuleerd.")))
        (princ "\\nDeze geul bevat geen bruikbare kabelgegevens.")))
    (princ "\\nGeen bewerkbare KabelGeul geselecteerd."))
  (setq *tbkg:floating-code* oldfloating *tbkg:future-code-switch* oldswitch *tbkg:copy-stage* nil)
  (princ))''')
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
# Save the floating layout with the block, independent of future-placement settings.
needle = '(setq attr-index 1)\n  (foreach item sorted-items'
assert needle in text
text = text.replace(needle, '''(tbkg:make-attribute p2 0.10 text-ang "TB_CODESWAP" "Codepositie:"
    (if *tbkg:future-code-switch* "1" "0") "E_LV_CROSS_SECTION" text-just 3)
  ''' + needle)
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
    'Adapters': ['private command/function namespace', 'managed layer/copy helpers', 'Arial Nova Light', 'managed configuration UI', 'host block guards', 'no load-time drawing changes', 'exact cable type metadata', 'restore command state', 'Undo Begin/End', 'full geul editing with unique rebuild and rollback', 'stored floating code layout', 'complete editable labels preserved verbatim; no prefix reapplication', 'recover input row order along the stored six-vertex incoming leader']
}, indent=2), encoding='utf-8')
