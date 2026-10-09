;;; KabelGeul_Alles.lsp
;;; Gecombineerd laadbestand voor de AutoCAD-LISP-bestanden in deze map.
;;; Bevat: KabelGeul_Omni, LICHTMAST en RICHTINGEN.
;;; Laad alleen dit bestand met APPLOAD.


;;; ============================================================
;;; Ingevoegd uit: KabelGeul_Omni.lsp
;;; ============================================================

;;; KabelGeul_Omni_06-08-2026ad.lsp
;;; Hoofdcommando: KABELGEULOMNI (ook KGOMNI of OMNI)
;;; Auteur: Luka Stuurman
;;; Plaatst een kabelgeulbak vanaf een lange lijn naar een halve vierkante bak en groepeert dit als een uniek Block.

(vl-load-com)

(setq *kg:version* "KabelGeul Omni 2026-08-06ad - vaste rijafstand en compacte twee-zijdige bak")

;; Alle kabelvarianten gebruiken dezelfde vaste mantelbuismaat. De straal is
;; verkleind van 0.74 naar 0.64, zodat de mantel duidelijk compacter rond
;; Enkel, Dubbel, Drie en Combi ligt.
(setq *kg:mantelbuis-radius* 0.64)
;; Eén vaste hart-op-hartafstand voor alle kabelrijen en voor beide zijden.
;; Diameter 1.28 + 0.05 vrije ruimte = 1.33. Hierdoor raken mantelbuizen
;; elkaar net niet en blijft de afstand overal gelijk, ongeacht kabelvorm.
(setq *kg:vaste-hartafstand* (+ (* 2.0 *kg:mantelbuis-radius*) 0.05))
(setq *kg:twosided-center-sep* *kg:vaste-hartafstand*)
(setq *kg:kabelrij-spacing* *kg:vaste-hartafstand*)

;; Compacte vaste bakhoogte. Twee-zijdige mantelbuizen passen hierin met
;; een kleine buitenmarge, terwijl hun onderlinge vrije ruimte 0.05 blijft.
(setq *kg:bakhoogte* 3.00)

(setq *kg:last-count* 3)
(setq *kg:last-types* '("K01" "K02" "Bestaand"))
(setq *kg:last-labels* '("" "" ""))
(setq *kg:last-existing-labels* '("" "" ""))
(setq *kg:last-doubles* '(nil nil nil))
(setq *kg:last-triples* '(nil nil nil))
(setq *kg:last-twosideds* '(nil nil nil))
(setq *kg:last-side2-doubles* '(nil nil nil))
(setq *kg:last-side2-triples* '(nil nil nil))
(setq *kg:last-side2-combis* '(nil nil nil))
(setq *kg:last-mantels* '(nil nil nil))
(setq *kg:last-splits* '(nil nil nil))
(setq *kg:last-combis* '(nil nil nil))
(setq *kg:last-mantel-spec* "")
(setq *kg:type-options* '("K01" "K02" "K03" "K04" "K05" "K06" "K07" "K08" "K09" "K10" "K11" "K12" "MS Nieuw" "MS Bestaand" "MS Rooien" "Bestaand" "Rooien" "Half rooien"))
(setq *kg:shape-options* '("Enkel" "Dubbel" "Drie" "Combi"))
(setq *kg:dialog-row-count* 3)
(setq *kg:dialog-resize-count* nil)
(setq *kg:block-counter* 0)
(setq *kg:new-ents* nil)
(setq *kg:floating-code* nil)
;; CODESWITCH bepaalt de opmaak van nieuw te plaatsen zwevende KabelGeulen.
;; De keuze wordt in AutoCAD bewaard, zodat zij ook na opnieuw starten actief blijft.
(if (not (boundp '*kg:future-code-switch*))
  (setq *kg:future-code-switch*
    (if (equal (getenv "KabelGeulOmniCodeSwitch") "1") T nil)))
(setq *kg:copy-stage* nil)
(setq *kg:mask-app* "KABELGEUL_MASK")
(setq *kg:lsp-dir*
  (if (and (boundp '*load-truename*) *load-truename*)
    (vl-filename-directory *load-truename*)
    nil))

(defun kg:track-ent (ent)
  (if ent
    (setq *kg:new-ents* (cons ent *kg:new-ents*)))
  ent)

(defun kg:unique-block-name (/ base name)
  (setq base (strcat "KabelGeul_" (vl-string-translate "." "_" (rtos (getvar "CDATE") 2 8))))
  (setq *kg:block-counter* (1+ *kg:block-counter*))
  (setq name (strcat base "_" (itoa *kg:block-counter*)))
  (while (tblsearch "BLOCK" name)
    (setq *kg:block-counter* (1+ *kg:block-counter*))
    (setq name (strcat base "_" (itoa *kg:block-counter*))))
  name)

(defun kg:layer-defs (/)
  '(("E_LV_CROSS_SECTION" 5 "Continuous")
    ("E_LV_CABLE_POINT" 125 "Continuous")
    ("KabelGeul zwevend referentie" 5 "Continuous")
    ("KabelGeul zwevend code" 6 "Continuous")
    ("01 - Bestaande kabel" 7 "DASHED")
    ("01-Te Rooien Kabel" 160 "DASHDOT")
    ("Aansluiting LS K01" 4 "Continuous")
    ("Aansluiting LS K02" 52 "DASHED")
    ("Aansluiting LS K03" 5 "Continuous")
    ("Aansluiting LS K04" 3 "DASHED")
    ("Aansluiting LS K05" 6 "Continuous")
    ("Aansluiting LS K06" 30 "Continuous")
    ("Aansluiting LS K07" 252 "Continuous")
    ("Aansluiting LS K08" 244 "Continuous")
    ("Aansluiting LS K09" 114 "Continuous")
     ("Aansluiting LS K10" 1 "Continuous")
     ("Aansluiting LS K11" 190 "Continuous")
     ("Aansluiting LS K12" 201 "Continuous")
     ("MS Nieuw" 7 "Continuous")
     ("MS Bestaand" 7 "DASHED")
     ;; De kleur van MS Rooien wordt in kg:ensure-layers gelijkgezet aan
     ;; de normale Rooien-laag (ACI 160).
     ("MS Rooien" 160 "DASHDOT")
     ("Mantelbuis" 6 "Continuous")
    ("Opmerking" 1 "Continuous")))

(defun kg:ensure-ltype (doc ltype / ltypes)
  (if (and (/= (strcase ltype) "CONTINUOUS")
           (not (tblsearch "LTYPE" ltype)))
    (progn
      (setq ltypes (vla-get-Linetypes doc))
      (vl-catch-all-apply 'vla-Load (list ltypes ltype "acad.lin"))))
  (if (tblsearch "LTYPE" ltype) ltype "Continuous"))

(defun kg:safe-put (obj prop val)
  (vl-catch-all-apply
    'vlax-put-property
    (list obj prop val)))

(defun kg:make-layer (name color ltype / doc layers layer result existed)
  (setq doc (vla-get-ActiveDocument (vlax-get-acad-object)))
  (setq layers (vla-get-Layers doc))
  (setq ltype (kg:ensure-ltype doc ltype))
  (setq existed (if (tblsearch "LAYER" name) T nil))
  (setq layer
    (if existed
      (vla-item layers name)
      (progn
        (setq result (vl-catch-all-apply 'vla-add (list layers name)))
        (if (vl-catch-all-error-p result)
          nil
          result))))
  (if layer
    (progn
      ;; Behoud de door de gebruiker ingestelde kleur van bestaande lagen.
      ;; Alleen een nieuw aangemaakte laag krijgt de standaardkleur uit kg:layer-defs.
      (if (not existed)
        (kg:safe-put layer 'Color color))
      (kg:safe-put layer 'Linetype ltype)
      (kg:safe-put layer 'LayerOn :vlax-true)
      (kg:safe-put layer 'Freeze :vlax-false)
      (kg:safe-put layer 'Lock :vlax-false)))
  name)

(defun kg:set-layer-aci-color (name color / ent data)
  ;; Zet een bestaande laag terug naar een ACI-kleur. Een eventueel aanwezige
  ;; truecolor-code wordt verwijderd, zodat de ACI-kleur ook echt zichtbaar is.
  (setq ent (tblobjname "LAYER" name))
  (if ent
    (progn
      (setq data (entget ent))
      (if (assoc 62 data)
        (setq data (subst (cons 62 color) (assoc 62 data) data))
        (setq data (append data (list (cons 62 color)))))
      (if (assoc 420 data)
        (setq data (vl-remove (assoc 420 data) data)))
      (entmod data)
      (entupd ent)
      T)))

(defun kg:set-layer-true-color (name rgb / ent data)
  ;; AutoCAD-ACI 7 wordt vaak als wit weergegeven. Gebruik voor de MS-lagen
  ;; daarom expliciet truecolor; RGB 0 is zwart.
  (setq ent (tblobjname "LAYER" name))
  (if ent
    (progn
      (setq data (entget ent))
      (if (assoc 420 data)
        (setq data (subst (cons 420 rgb) (assoc 420 data) data))
        (setq data (append data (list (cons 420 rgb)))))
      (entmod data)
      (entupd ent)
      T)))

(defun kg:ensure-layers (/ d)
  (foreach d (kg:layer-defs)
    (kg:make-layer (car d) (cadr d) (caddr d)))
  ;; MS Nieuw en MS Bestaand moeten in AutoCAD echt zwart zijn, ook als de
  ;; lagen al eerder met ACI 7 (wit) waren aangemaakt.
  (kg:set-layer-true-color "MS Nieuw" 0)
  (kg:set-layer-true-color "MS Bestaand" 0)
  ;; Houd MS Rooien exact gelijk aan de kleur van normaal Rooien.
  (kg:set-layer-aci-color "MS Rooien" 160))

(defun kg:hide-wipeout-frames (/ result)
  ;; WIPEOUTFRAME=0 verbergt frames op scherm en in plots. FRAME wordt bewust
  ;; niet aangepast, zodat frames van afbeeldingen, underlays en xrefs behouden blijven.
  (setq result (vl-catch-all-apply 'setvar (list "WIPEOUTFRAME" 0)))
  (not (vl-catch-all-error-p result)))

(defun kg:type->layer (typ)
  (cond
    ((= typ "MS Nieuw") "MS Nieuw")
    ((= typ "MS Bestaand") "MS Bestaand")
    ((= typ "MS Rooien") "MS Rooien")
    ((= typ "Bestaand") "01 - Bestaande kabel")
    ((= typ "Rooien") "01-Te Rooien Kabel")
    ((= typ "Half rooien") "01-Te Rooien Kabel")
    (T (strcat "Aansluiting LS " typ))))

(defun kg:type->text-layer (typ)
  ;; MS Nieuw heeft een zwarte cirkel, maar roze kabeltekst zoals K05.
  (if (= typ "MS Nieuw")
    "Aansluiting LS K05"
    (kg:type->layer typ)))

(defun kg:type->label (typ)
  (cond
    ((= typ "MS Nieuw") "MS Nieuw")
    ((= typ "MS Bestaand") "MS Bestaand")
    ((= typ "MS Rooien") "(Rooien) kabel")
    ((= typ "Bestaand") "Bestaande kabel")
    ((= typ "Rooien") "(Rooien) kabel")
    ((= typ "Half rooien") "(Rooien) kabel")
    (T (strcat "Ri " (itoa (atoi (substr typ 2))) " "))))

(defun kg:type->color (typ / layer def result)
  (setq layer (strcase (kg:type->layer typ)))
  (foreach def (kg:layer-defs)
    (if (= (strcase (car def)) layer)
      (setq result (cadr def))))
  result)

(defun kg:color->type (color / opt result)
  (if color
    (foreach opt *kg:type-options*
      (if (and (not (member opt '("MS Nieuw" "MS Bestaand")))
               (not (= opt "Half rooien"))
               (= color (kg:type->color opt)))
        (setq result opt))))
  result)

(defun kg:starts-with (txt prefix / n)
  (if (null txt) (setq txt ""))
  (setq n (strlen prefix))
  (and (>= (strlen txt) n)
       (= (substr txt 1 n) prefix)))

(defun kg:prefixed-label (typ txt / prefix base)
  (if (null txt) (setq txt ""))
  (setq base (if (= txt "") (kg:type->label typ) txt))
  (cond
    ((or (= typ "Rooien") (= typ "Half rooien") (= typ "MS Rooien"))
     (setq prefix "(Rooien) ")
     (if (kg:starts-with base "(Te rooien) ")
       (setq base (kg:trim (substr base (1+ (strlen "(Te rooien) "))))))
     (if (kg:starts-with base prefix)
       base
       (strcat prefix base)))
    ((wcmatch typ "K##")
     (setq prefix (strcat "Ri " (itoa (atoi (substr typ 2))) " "))
     (if (kg:starts-with base prefix)
       base
       (strcat prefix base)))
    (T base)))

(defun kg:midpoint (p1 p2)
  (mapcar '(lambda (a b) (/ (+ a b) 2.0)) p1 p2))

(defun kg:p+ (p a d)
  (polar p a d))

(defun kg:local->world (base ang x y)
  (kg:p+ (kg:p+ base ang x) (+ ang (/ pi 2.0)) y))

(defun kg:dot2 (p1 p2)
  (+ (* (car p1) (car p2)) (* (cadr p1) (cadr p2))))

(defun kg:vector2 (p1 p2)
  (list (- (car p2) (car p1)) (- (cadr p2) (cadr p1))))

(defun kg:endpoint-from-perp (p1 rawp2 perp-ang / unit v d)
  (setq unit (list (cos perp-ang) (sin perp-ang)))
  (setq v (kg:vector2 p1 rawp2))
  (setq d (kg:dot2 v unit))
  (if (< d 0.0)
    (progn
      (setq perp-ang (+ perp-ang pi))
      (setq d (- d))))
  (setq d (distance p1 rawp2))
  (polar p1 perp-ang d))

(defun kg:pt2 (p)
  (list (car p) (cadr p)))

(defun kg:point-p (p)
  (and (listp p)
       (numberp (car p))
       (numberp (cadr p))))

(defun kg:valid-points (pts / out)
  (setq out '())
  (if (listp pts)
    (foreach pt pts
      (if (kg:point-p pt)
        (setq out (cons pt out)))))
  (reverse out))

(defun kg:friendly-error-message (msg / upper pos value)
  (setq upper (strcase (if msg msg "")))
  (if (wcmatch upper "*BAD ARGUMENT TYPE: CONSP*")
    (progn
      (setq pos (vl-string-search "CONSP " upper))
      (setq value (if pos (substr msg (+ pos 7)) "onbekend"))
      (strcat
        "ongeldig punt of coordinaat in het bronblok. Gevonden waarde: " value
        ". Selecteer bij een zwevende geul het blok, de zwevende code of de blauwe referentielijn."))
    msg))

(defun kg:pt3 (p)
  (list (car p) (cadr p) (if (caddr p) (caddr p) 0.0)))

(defun kg:world-up (pt dist)
  (list
    (car pt)
    (+ (cadr pt) dist)
    (if (caddr pt) (caddr pt) 0.0)))

(defun kg:readable-text-props (ang / two flipped)
  (setq flipped nil)
  (setq two (* 2.0 pi))
  (while (< ang 0.0)
    (setq ang (+ ang two)))
  (while (>= ang two)
    (setq ang (- ang two)))
  (if (and (> ang (/ pi 2.0)) (< ang (* 1.5 pi)))
    (progn
      (setq ang (+ ang pi))
      (setq flipped T)))
  (while (>= ang two)
    (setq ang (- ang two)))
  (list ang (if flipped 2 0)))

(defun kg:index-string (i)
  (if (< i 10)
    (strcat "0" (itoa i))
    (itoa i)))

(defun kg:index-tag (prefix i)
  (strcat prefix "_" (kg:index-string i)))

(defun kg:trim (txt)
  (vl-string-trim " \t\r\n" (if txt txt "")))

(defun kg:normalize-type (txt / upper n result)
  (setq upper (strcase (kg:trim txt)))
  (cond
    ((= upper "MS NIEUW") "MS Nieuw")
    ((= upper "MS BESTAAND") "MS Bestaand")
    ((= upper "MS ROOIEN") "MS Rooien")
    ((= upper "BESTAAND") "Bestaand")
    ((or (= upper "ROOIEN")
         (= upper "TE ROOIEN")
         (= upper "(TE ROOIEN)")) "Rooien")
    ((or (= upper "HALF ROOIEN")
         (= upper "HALFROOIEN")) "Half rooien")
    ((wcmatch upper "K##") upper)
    ((wcmatch upper "K#") (strcat "K0" (substr upper 2 1)))
    ((wcmatch upper "RI ##") (strcat "K" (substr upper 4 2)))
    ((wcmatch upper "RI #") (strcat "K0" (substr upper 4 1)))
    ((wcmatch upper "RI##") (strcat "K" (substr upper 3 2)))
    ((wcmatch upper "RI#") (strcat "K0" (substr upper 3 1)))
    ((wcmatch upper "##") (strcat "K" upper))
    ((wcmatch upper "#") (strcat "K0" upper))
    (T
     (setq n 12 result nil)
     (while (and (>= n 1) (null result))
       (if (= upper (strcase (kg:type->label (strcat "K" (kg:index-string n)))))
         (setq result (strcat "K" (kg:index-string n))))
       (setq n (1- n)))
     result)))

(defun kg:strip-known-prefix (txt / out n p)
  (setq out (kg:trim txt))
  (setq n 12)
  (while (>= n 1)
    (setq p (strcat "Ri " (itoa n) " "))
    (if (kg:starts-with out p)
      (setq out (kg:trim (substr out (1+ (strlen p))))))
    (setq n (1- n)))
  (if (kg:starts-with out "(Te rooien) ")
    (setq out (kg:trim (substr out (1+ (strlen "(Te rooien) "))))))
  (if (kg:starts-with out "(Rooien) ")
    (setq out (kg:trim (substr out (1+ (strlen "(Rooien) "))))))
  out)

(defun kg:editor-item-point (item)
  (cond
    ((nth 5 item) (nth 5 item))
    ((nth 4 item) (nth 4 item))
    (T (nth 3 item))))

(defun kg:editor-item< (a b / ap bp ay by ax bx)
  (setq ap (kg:editor-item-point a))
  (setq bp (kg:editor-item-point b))
  (setq ay (cadr ap))
  (setq by (cadr bp))
  (setq ax (car ap))
  (setq bx (car bp))
  (if (equal ay by 1e-8)
    (< ax bx)
    (> ay by)))

(defun kg:curve-angle-at-pick (ent pick / cp param deriv data ang)
  (setq cp (vl-catch-all-apply 'vlax-curve-getClosestPointTo (list ent pick)))
  (if (not (vl-catch-all-error-p cp))
    (progn
      (setq param (vl-catch-all-apply 'vlax-curve-getParamAtPoint (list ent cp)))
      (if (not (vl-catch-all-error-p param))
        (progn
          (setq deriv (vl-catch-all-apply 'vlax-curve-getFirstDeriv (list ent param)))
          (if (and (not (vl-catch-all-error-p deriv)) deriv)
            (setq ang (angle '(0.0 0.0 0.0) deriv)))))))
  (if (null ang)
    (progn
      (setq data (entget ent))
      (if (and (assoc 10 data) (assoc 11 data))
        (setq ang (angle (cdr (assoc 10 data)) (cdr (assoc 11 data)))))))
  ang)

(defun kg:curve-point-at-pick (ent pick / cp)
  (setq cp (vl-catch-all-apply 'vlax-curve-getClosestPointTo (list ent pick)))
  (if (or (vl-catch-all-error-p cp) (null cp))
    pick
    cp))

(defun kg:make-lwpoly (pts layer closed / data)
  (setq data
    (append
      (list '(0 . "LWPOLYLINE")
            '(100 . "AcDbEntity")
            (cons 8 layer)
            '(100 . "AcDbPolyline")
            (cons 90 (length pts))
            (cons 70 (if closed 1 0)))
      (apply 'append
        (mapcar
          '(lambda (p) (list (cons 10 (kg:pt2 p))))
          pts))))
  (kg:track-ent (entmakex data)))

(defun kg:make-wipeout (pts layer / data poly result wipe wipe-data)
  (kg:hide-wipeout-frames)
  (setq pts (kg:valid-points pts))
  (if (> (length pts) 2)
    (progn
      (setq data
        (append
          (list '(0 . "LWPOLYLINE")
                '(100 . "AcDbEntity")
                (cons 8 layer)
                '(100 . "AcDbPolyline")
                (cons 90 (length pts))
                '(70 . 1))
          (apply 'append
            (mapcar
              '(lambda (p) (list (cons 10 (kg:pt2 p))))
              pts))))
      (setq poly (entmakex data))
      (if poly
        (progn
          (setq result
            (vl-catch-all-apply
              'vl-cmdf
              (list "_.WIPEOUT" "_Polyline" poly "_Yes")))
          (if (and (not (vl-catch-all-error-p result))
                   (setq wipe (entlast))
                   (= (cdr (assoc 0 (entget wipe))) "WIPEOUT"))
            (progn
              (setq wipe-data (entget wipe))
              (if (assoc 8 wipe-data)
                (entmod (subst (cons 8 layer) (assoc 8 wipe-data) wipe-data)))
              (kg:track-ent wipe))
            (progn
              (if (entget poly) (entdel poly))
              nil)))))))

(defun kg:set-mask-info (ent tag txt / data)
  (if (and ent tag)
    (progn
      (regapp *kg:mask-app*)
      ;; entget zonder applijst laat bestaande XData weg; zo wordt onze informatie schoon vervangen.
      (setq data (entget ent))
      (entmod
        (append data
          (list
            (list -3
              (list *kg:mask-app*
                (cons 1000 tag)
                (cons 1000 (if txt txt "")))))))
      ent)))

(defun kg:mask-info (ent / xdata app values)
  (if ent
    (progn
      (setq xdata (assoc -3 (entget ent (list *kg:mask-app*))))
      (setq app (if xdata (cadr xdata) nil))
      (setq values (if app (cdr app) nil))
      (if values
        (list
          (if (car values) (cdr (car values)) nil)
          (if (cadr values) (cdr (cadr values)) ""))
        nil))))

(defun kg:text-mask-width (txt h / box width)
  (setq box
    (vl-catch-all-apply
      'textbox
      (list
        (list '(0 . "TEXT")
              (cons 1 (if txt txt ""))
              (cons 40 h)
              '(7 . "Standard")))))
  (setq width
    (if (or (vl-catch-all-error-p box)
            (null box)
            (null (cadr box)))
      (* h 0.65 (max 1 (strlen (if txt txt ""))))
      (- (car (cadr box)) (car (car box)))))
  (max h width))

(defun kg:text-mask-points (pt ang just txt h tag / width pad start-overlap half-thickness cable-mask mantel-mask x1 x2 y1 y2)
  (if (kg:point-p pt)
    (progn
      (setq width (kg:text-mask-width txt h))
      (setq pad 0.10)
      (setq cable-mask
        (or (kg:starts-with (strcase (if tag tag "")) "KABEL_")
            (kg:starts-with (strcase (if tag tag "")) "BESTAAND_")
            (kg:starts-with (strcase (if tag tag "")) "ZIJDE2_")))
      (setq mantel-mask
        (= (strcase (if tag tag "")) "MANTEL_SPEC"))
      ;; Kabeltekst overlapt de bakrand beperkt. Voor MANTEL_SPEC loopt het
      ;; masker verder terug, zodat het kleine leaderstuk volledig onder de
      ;; tekst wordt weggepoetst.
      (setq start-overlap
        (cond
          (cable-mask 0.20)
          (mantel-mask 0.35)
          (T pad)))
      (cond
        ((= just 2)
         (setq x1 (- (+ width pad)) x2 start-overlap))
        ((= just 1)
         (setq x1 (- (+ (/ width 2.0) pad)) x2 (+ (/ width 2.0) pad)))
        (T
         (setq x1 (- start-overlap) x2 (+ width pad))))
      ;; Kabeltekstmaskers volgen exact het vaste kabelrijraster. Een kleine
      ;; overlap van 0.01 voorkomt haarlijntjes door afronding of weergave.
      ;; Daardoor sluiten wipeouts van opeenvolgende rijen volledig op elkaar aan.
      (setq half-thickness
        (if cable-mask
          (+ (/ *kg:kabelrij-spacing* 2.0) 0.01)
          (+ (/ h 2.0) pad)))
      (setq y1 (- half-thickness)
            y2 half-thickness)
      (list
        (kg:local->world pt ang x1 y1)
        (kg:local->world pt ang x2 y1)
        (kg:local->world pt ang x2 y2)
        (kg:local->world pt ang x1 y2)))))

(defun kg:draw-text-wipeout (pt ang just txt h tag / wipe)
  (setq wipe
    (kg:make-wipeout
      (kg:text-mask-points pt ang just txt h tag)
      "E_LV_CROSS_SECTION"))
  (if wipe (kg:set-mask-info wipe tag txt))
  wipe)

(defun kg:set-wipeout-points (ent pts / minx miny maxx maxy size clips first-clip data out pair code)
  (setq pts (kg:valid-points pts))
  (if (and ent (= (cdr (assoc 0 (entget ent))) "WIPEOUT") (> (length pts) 2))
    (progn
      (setq minx (car (car pts))
            maxx minx
            miny (cadr (car pts))
            maxy miny)
      (foreach pt (cdr pts)
        (setq minx (min minx (car pt))
              maxx (max maxx (car pt))
              miny (min miny (cadr pt))
              maxy (max maxy (cadr pt))))
      (setq size (max (- maxx minx) (- maxy miny) 1e-6))
      (setq clips
        (mapcar
          '(lambda (pt)
             (list
               (- (/ (- (car pt) minx) size) 0.5)
               (- 0.5 (/ (- (cadr pt) miny) size))
               0.0))
          pts))
      (setq first-clip (car clips))
      (setq clips (append clips (list first-clip)))
      (setq data (entget ent) out '())
      (foreach pair data
        (setq code (car pair))
        (cond
          ((= code 10) (setq out (cons (cons 10 (list minx miny 0.0)) out)))
          ((= code 11) (setq out (cons (cons 11 (list size 0.0 0.0)) out)))
          ((= code 12) (setq out (cons (cons 12 (list 0.0 size 0.0)) out)))
          ((= code 91)
           (setq out (cons (cons 91 (length clips)) out))
           (foreach pt clips
             (setq out (cons (cons 14 pt) out))))
          ((= code 14))
          (T (setq out (cons pair out)))))
      (if (entmod (reverse out))
        (progn (entupd ent) ent)
        nil))))

(defun kg:make-circle (pt rad layer)
  (kg:track-ent
    (entmakex
    (list '(0 . "CIRCLE")
          '(100 . "AcDbEntity")
          (cons 8 layer)
          '(6 . "Continuous")
          '(100 . "AcDbCircle")
          (cons 10 (kg:pt3 pt))
            (cons 40 rad)))))

(defun kg:make-line (p1 p2 layer)
  (kg:track-ent
    (entmakex
    (list '(0 . "LINE")
          '(100 . "AcDbEntity")
          (cons 8 layer)
          '(6 . "Continuous")
          '(100 . "AcDbLine")
          (cons 10 (kg:pt3 p1))
            (cons 11 (kg:pt3 p2))))))

(defun kg:make-attribute (pt h rot tag prompt val layer hjust flags / data)
  (if (null hjust) (setq hjust 0))
  (if (null flags) (setq flags 0))
  (kg:track-ent
    (entmakex
    (list '(0 . "ATTDEF")
          '(100 . "AcDbEntity")
          (cons 8 layer)
          '(100 . "AcDbText")
          (cons 10 (kg:pt3 pt))
          (cons 40 h)
          (cons 1 val)      ; De waarde
          (cons 50 rot)
          '(7 . "Standard")
          (cons 72 hjust)   ; 0 = Middle Left, 2 = Middle Right
          (cons 11 (kg:pt3 pt))
          '(100 . "AcDbAttributeDefinition")
          '(280 . 0)
          (cons 3 prompt)   ; DE PROMPT (Verplicht voor goede werking!)
          (cons 2 tag)      ; De Tag-naam (bijv. KABEL_1)
          (cons 70 flags)
          '(73 . 0)
          '(74 . 2)         ; Middle Left: midden verticaal
            '(280 . 0)))))

(defun kg:make-text (pt h rot val layer hjust track / data ent)
  (if (null hjust) (setq hjust 0))
  (setq data
    (list '(0 . "TEXT")
          '(100 . "AcDbEntity")
          (cons 8 layer)
          '(100 . "AcDbText")
          (cons 10 (kg:pt3 pt))
          (cons 40 h)
          (cons 1 val)
          (cons 50 rot)
          '(7 . "Standard")
          (cons 72 hjust)
          (cons 11 (kg:pt3 pt))
          '(73 . 2)))
  (setq ent (entmakex data))
  (if track
    (kg:track-ent ent)
    ent))

(defun kg:floating-code-p (txt)
  (and txt
       (= (strlen txt) 4)
       (wcmatch (substr txt 1 3) "###")
       (wcmatch (strcase (substr txt 4 1)) "[A-Z]")))

(defun kg:floating-code-index (txt / num letter)
  (setq num (atoi (substr txt 1 3)))
  (setq letter (- (ascii (strcase (substr txt 4 1))) 65))
  (+ (1- num) (* letter 999)))

(defun kg:index->floating-code (idx / num letter)
  (setq num (1+ (rem idx 999)))
  (setq letter (chr (+ 65 (/ idx 999))))
  (strcat
    (cond
      ((< num 10) (strcat "00" (itoa num)))
      ((< num 100) (strcat "0" (itoa num)))
      (T (itoa num)))
    letter))

(defun kg:next-floating-code (/ ss idx ent data txt maxidx code)
  (setq maxidx -1)
  (setq ss (ssget "_X" '((0 . "TEXT,MTEXT,ATTRIB"))))
  (if ss
    (progn
      (setq idx 0)
      (while (< idx (sslength ss))
        (setq ent (ssname ss idx))
        (setq data (entget ent))
        (setq txt (cdr (assoc 1 data)))
        (if (kg:floating-code-p txt)
          (setq maxidx (max maxidx (kg:floating-code-index txt))))
        (setq idx (1+ idx)))))
  (setq code (kg:index->floating-code (1+ maxidx)))
  code)

(defun kg:draw-floating-reference (p1 p2 code / ang text-props text-pt)
  (setq ang (angle p1 p2))
  (setq text-props (kg:readable-text-props (- ang (/ pi 2.0))))
  (setq text-pt (kg:local->world p2 ang 0.45 0.0))
  (entmakex
    (list '(0 . "LWPOLYLINE")
          '(100 . "AcDbEntity")
          '(8 . "KabelGeul zwevend referentie")
          '(62 . 5)
          '(100 . "AcDbPolyline")
          '(90 . 2)
          '(70 . 0)
          '(43 . 0.1)
          (cons 10 (kg:pt2 p1))
          (cons 10 (kg:pt2 p2))))
  (kg:make-text text-pt 0.55 (car text-props) code "KabelGeul zwevend code" 1 nil))

(defun kg:find-new-insert (after / next data found)
  (setq next (if after (entnext after) (entnext)))
  (while (and next (null found))
    (setq data (entget next))
    (if (= (cdr (assoc 0 data)) "INSERT")
      (setq found next))
    (setq next (entnext next)))
  found)

(defun kg:bring-to-front (ent / ss result)
  (if (and ent (entget ent))
    (progn
      (setq ss (ssadd))
      (ssadd ent ss)
      (setq result
        (vl-catch-all-apply
          'vl-cmdf
          (list "_.DRAWORDER" ss "" "_Front")))
      (if (vl-catch-all-error-p result)
        nil
        ent))))

(defun kg:open-enhanced-attribute-editor (insert-ent / oldcmddia ss result)
  (if (and insert-ent
           (= (cdr (assoc 0 (entget insert-ent))) "INSERT"))
    (progn
      (setq oldcmddia (getvar "CMDDIA"))
      (setq ss (ssadd))
      (ssadd insert-ent ss)
      (sssetfirst nil ss)
      (setvar "CMDDIA" 1)
      (setq result (vl-catch-all-apply 'vl-cmdf (list "_.EATTEDIT" insert-ent)))
      (setvar "CMDDIA" oldcmddia)
      (sssetfirst nil nil)
      (not (vl-catch-all-error-p result)))))

(defun kg:insert-by-floating-code (code / ss i insert-ent attr data found)
  (if (kg:floating-code-p code)
    (setq ss (ssget "_X" '((0 . "INSERT") (66 . 1)))))
  (if ss
    (progn
      (setq i 0)
      (while (and (< i (sslength ss)) (null found))
        (setq insert-ent (ssname ss i))
        (setq attr (kg:attr-ent insert-ent "ZWEVEND_CODE"))
        (if attr
          (progn
            (setq data (entget attr))
            (if (= (strcase (if (cdr (assoc 1 data)) (cdr (assoc 1 data)) "")) (strcase code))
              (setq found insert-ent))))
        (setq i (1+ i)))))
  found)

(defun kg:nearest-floating-code (pt / ss i ent data txt text-pt dist best-dist best-code)
  (setq ss (ssget "_X" '((0 . "TEXT,MTEXT") (8 . "KabelGeul zwevend code"))))
  (if ss
    (progn
      (setq i 0)
      (while (< i (sslength ss))
        (setq ent (ssname ss i))
        (setq data (entget ent))
        (setq txt (cdr (assoc 1 data)))
        (setq text-pt (cdr (assoc 10 data)))
        (if (and text-pt (kg:floating-code-p txt))
          (progn
            (setq dist (distance (kg:pt2 pt) (kg:pt2 text-pt)))
            (if (and (< dist 2.0) (or (null best-dist) (< dist best-dist)))
              (setq best-dist dist
                    best-code txt))))
        (setq i (1+ i)))))
  best-code)

(defun kg:floating-code-for-entity (ent / data typ layer pair pts pt txt)
  (if ent
    (progn
      (setq data (entget ent))
      (setq typ (cdr (assoc 0 data)))
      (setq layer (cdr (assoc 8 data)))
      (cond
        ((member typ '("TEXT" "MTEXT"))
         (setq txt (cdr (assoc 1 data)))
         (if (kg:floating-code-p txt) txt nil))
        ((and (member typ '("LINE" "LWPOLYLINE" "POLYLINE"))
              (= (strcase (if layer layer "")) "KABELGEUL ZWEVEND REFERENTIE"))
         (cond
           ((= typ "LINE")
            (setq pt (cdr (assoc 11 data))))
           ((= typ "LWPOLYLINE")
            (foreach pair data
              (if (= (car pair) 10)
                (setq pt (cdr pair)))))
           ((= typ "POLYLINE")
            (setq pts (kg:old-polyline-points ent))
            (setq pt (if pts (car (last pts)) nil))))
         (if pt (kg:nearest-floating-code pt) nil))
        (T nil)))))

(defun kg:selected-insert (ent / data owner code)
  (if ent
    (progn
      (setq data (entget ent))
      (cond
        ((= (cdr (assoc 0 data)) "INSERT") ent)
        ((= (cdr (assoc 0 data)) "ATTRIB")
         (setq owner (cdr (assoc 330 data)))
         (if (and owner (= (cdr (assoc 0 (entget owner))) "INSERT"))
           owner
           nil))
        (T
         (setq code (kg:floating-code-for-entity ent))
         (if code (kg:insert-by-floating-code code) nil))))))

(defun kg:dxf-set (data code val)
  (if (assoc code data)
    (subst (cons code val) (assoc code data) data)
    (append data (list (cons code val)))))

(defun kg:update-ent-codes (ent pairs / data pair)
  (if (and ent (entget ent))
    (progn
      (setq data (entget ent))
      (foreach pair pairs
        (setq data (kg:dxf-set data (car pair) (cdr pair))))
      (entmod data)
      (entupd ent))))

(defun kg:attribute-point (data)
  (cond
    ((assoc 11 data) (cdr (assoc 11 data)))
    ((assoc 10 data) (cdr (assoc 10 data)))
    (T '(0.0 0.0 0.0))))

(defun kg:attr-ent (insert-ent tag / next data found)
  (setq tag (strcase tag))
  (setq next (entnext insert-ent))
  (while (and next (null found))
    (setq data (entget next))
    (cond
      ((= (cdr (assoc 0 data)) "ATTRIB")
       (if (= (strcase (cdr (assoc 2 data))) tag)
         (setq found next)))
      ((= (cdr (assoc 0 data)) "SEQEND")
       (setq next nil)))
    (if (and next (null found))
      (setq next (entnext next))))
  found)

(defun kg:attr-ent-by-index (insert-ent prefix i)
  (or
    (kg:attr-ent insert-ent (kg:index-tag prefix i))
    (kg:attr-ent insert-ent (strcat prefix "_" (itoa i)))))

(defun kg:kabel-tag-p (tag)
  (kg:starts-with (strcase (if tag tag "")) "KABEL_"))

(defun kg:bestaand-tag-p (tag)
  (kg:starts-with (strcase (if tag tag "")) "BESTAAND_"))

(defun kg:zijde2-tag-p (tag)
  (kg:starts-with (strcase (if tag tag "")) "ZIJDE2_"))

(defun kg:tag-index (tag)
  (atoi (substr (strcase tag) 7)))

(defun kg:bestaand-tag-index (tag)
  (atoi (substr (strcase tag) 10)))

(defun kg:zijde2-tag-index (tag)
  (atoi (substr (strcase tag) 8)))

(defun kg:direction-tag-p (tag)
  (kg:starts-with (strcase (if tag tag "")) "RICHTING_"))

(defun kg:remove-direction-attributes (insert-ent / data blkname blkrec next todel)
  (if (and insert-ent (= (cdr (assoc 0 (entget insert-ent))) "INSERT"))
    (progn
      (setq next (entnext insert-ent))
      (while next
        (setq data (entget next))
        (cond
          ((= (cdr (assoc 0 data)) "ATTRIB")
           (if (kg:direction-tag-p (cdr (assoc 2 data)))
             (setq todel (cons next todel))))
          ((= (cdr (assoc 0 data)) "SEQEND")
           (setq next nil)))
        (if next
          (setq next (entnext next))))
      (foreach next todel
        (entdel next))
      (setq data (entget insert-ent))
      (setq blkname (cdr (assoc 2 data)))
      (setq blkrec (tblobjname "BLOCK" blkname))
      (if blkrec
        (progn
          (setq todel nil)
          (setq next (entnext blkrec))
          (while next
            (setq data (entget next))
            (cond
              ((= (cdr (assoc 0 data)) "ATTDEF")
               (if (kg:direction-tag-p (cdr (assoc 2 data)))
                 (setq todel (cons next todel))))
              ((= (cdr (assoc 0 data)) "ENDBLK")
               (setq next nil)))
            (if next
              (setq next (entnext next))))
          (foreach next todel
            (entdel next))))
      (entupd insert-ent))))

(defun kg:block-attdef-ent (blkrec tag / next data found)
  (setq tag (strcase tag))
  (setq next (entnext blkrec))
  (while (and next (null found))
    (setq data (entget next))
    (cond
      ((= (cdr (assoc 0 data)) "ATTDEF")
       (if (= (strcase (cdr (assoc 2 data))) tag)
         (setq found next)))
      ((= (cdr (assoc 0 data)) "ENDBLK")
       (setq next nil)))
    (if (and next (null found))
      (setq next (entnext next))))
  found)

(defun kg:text-mask-tag-p (tag / upper)
  (setq upper (strcase (if tag tag "")))
  (or (kg:starts-with upper "KABEL_")
      (kg:starts-with upper "BESTAAND_")
      (kg:starts-with upper "ZIJDE2_")
      (= upper "ZWEVEND_CODE")
      (= upper "MANTEL_SPEC")))

(defun kg:block-text-mask-ent (blkrec tag / next data info found)
  (setq tag (strcase (if tag tag "")))
  (if blkrec
    (progn
      (setq next (entnext blkrec))
      (while (and next (null found))
        (setq data (entget next))
        (cond
          ((= (cdr (assoc 0 data)) "WIPEOUT")
           (setq info (kg:mask-info next))
           (if (and info (= (strcase (if (car info) (car info) "")) tag))
             (setq found next)))
          ((= (cdr (assoc 0 data)) "ENDBLK")
           (setq next nil)))
        (if (and next (null found))
          (setq next (entnext next))))))
  found)

(defun kg:update-text-wipeouts-for-insert (insert-ent / insert-data blkname blkrec next data tag txt mask info attdef attdef-data pt ang just h pts changed)
  (setq changed 0)
  (if (and insert-ent (= (cdr (assoc 0 (entget insert-ent))) "INSERT"))
    (progn
      (setq insert-data (entget insert-ent))
      (setq blkname (cdr (assoc 2 insert-data)))
      (setq blkrec (if blkname (tblobjname "BLOCK" blkname) nil))
      (setq next (entnext insert-ent))
      (while next
        (setq data (entget next))
        (cond
          ((= (cdr (assoc 0 data)) "ATTRIB")
           (setq tag (cdr (assoc 2 data)))
           (if (kg:text-mask-tag-p tag)
             (progn
               (setq txt (if (assoc 1 data) (cdr (assoc 1 data)) ""))
               (setq mask (kg:block-text-mask-ent blkrec tag))
               (setq info (if mask (kg:mask-info mask) nil))
               (if (and mask info (/= (cadr info) txt))
                 (progn
                   (setq attdef (kg:block-attdef-ent blkrec tag))
                   (if attdef
                     (progn
                       (setq attdef-data (entget attdef))
                       (setq pt (kg:attribute-point attdef-data))
                       (setq ang (if (assoc 50 attdef-data) (cdr (assoc 50 attdef-data)) 0.0))
                       (setq just (if (assoc 72 attdef-data) (cdr (assoc 72 attdef-data)) 0))
                       (setq h (if (assoc 40 attdef-data) (cdr (assoc 40 attdef-data)) 0.55))
                       (setq pts (kg:text-mask-points pt ang just txt h tag))
                       (if (kg:set-wipeout-points mask pts)
                         (progn
                           (kg:set-mask-info mask tag txt)
                           (setq changed (1+ changed)))))))))))
          ((= (cdr (assoc 0 data)) "SEQEND")
           (setq next nil)))
        (if next (setq next (entnext next))))
      (if (> changed 0)
        (entupd insert-ent))))
  changed)


(defun kg:placement-value (data code / value)
  ;; Lees een tekstplaatsingswaarde met veilige AutoCAD-standaardwaarden.
  (setq value (assoc code data))
  (cond
    (value (cdr value))
    ((= code 11) (if (assoc 10 data) (cdr (assoc 10 data)) '(0.0 0.0 0.0)))
    ((= code 50) 0.0)
    ((= code 72) 0)
    ((= code 74) 2)
    (T nil)))

(defun kg:swap-text-placement (ent-a ent-b / data-a data-b new-a new-b code value-a value-b)
  ;; Wissel uitsluitend de plaatsing, rotatie en uitlijning; tekst, tag en laag blijven gelijk.
  (if (and ent-a ent-b (entget ent-a) (entget ent-b))
    (progn
      (setq data-a (entget ent-a)
            data-b (entget ent-b)
            new-a data-a
            new-b data-b)
      (foreach code '(10 11 50 72 74)
        (setq value-a (kg:placement-value data-a code)
              value-b (kg:placement-value data-b code))
        (if value-b (setq new-a (kg:dxf-set new-a code value-b)))
        (if value-a (setq new-b (kg:dxf-set new-b code value-a))))
      (if (and (entmod new-a) (entmod new-b))
        (progn
          (entupd ent-a)
          (entupd ent-b)
          T)
        nil))
    nil))

(defun kg:refresh-text-mask-for-tag (insert-ent tag / insert-data blkname blkrec attr data txt mask attdef attdef-data pt ang just h pts)
  ;; Herbereken één wipeout na een plaatsingswissel, ook als de tekstwaarde niet veranderde.
  (if (and insert-ent (= (cdr (assoc 0 (entget insert-ent))) "INSERT"))
    (progn
      (setq insert-data (entget insert-ent)
            blkname (cdr (assoc 2 insert-data))
            blkrec (if blkname (tblobjname "BLOCK" blkname) nil)
            attr (kg:attr-ent insert-ent tag)
            mask (kg:block-text-mask-ent blkrec tag)
            attdef (kg:block-attdef-ent blkrec tag))
      (if (and attr mask attdef)
        (progn
          (setq data (entget attr)
                txt (if (assoc 1 data) (cdr (assoc 1 data)) "")
                attdef-data (entget attdef)
                pt (kg:attribute-point attdef-data)
                ang (if (assoc 50 attdef-data) (cdr (assoc 50 attdef-data)) 0.0)
                just (if (assoc 72 attdef-data) (cdr (assoc 72 attdef-data)) 0)
                h (if (assoc 40 attdef-data) (cdr (assoc 40 attdef-data)) 0.55)
                pts (kg:text-mask-points pt ang just txt h tag))
          (if (kg:set-wipeout-points mask pts)
            (progn
              (kg:set-mask-info mask tag txt)
              T)
            nil))
        nil))
    nil))

(defun kg:codeswitch-insert (insert-ent / insert-data blkname blkrec code-attr mantel-attr code-def mantel-def attrs-ok defs-ok)
  ;; Wissel de zwevende code en mantelbuistekst. Omdat elk geplaatst KabelGeul-
  ;; block uniek is, mogen ook de ATTDEF-posities en block-wipeouts worden aangepast.
  (if (and insert-ent (= (cdr (assoc 0 (entget insert-ent))) "INSERT"))
    (progn
      (setq insert-data (entget insert-ent)
            blkname (cdr (assoc 2 insert-data))
            blkrec (if blkname (tblobjname "BLOCK" blkname) nil)
            code-attr (kg:attr-ent insert-ent "ZWEVEND_CODE")
            mantel-attr (kg:attr-ent insert-ent "MANTEL_SPEC")
            code-def (kg:block-attdef-ent blkrec "ZWEVEND_CODE")
            mantel-def (kg:block-attdef-ent blkrec "MANTEL_SPEC"))
      (if (and code-attr mantel-attr code-def mantel-def)
        (progn
          (setq attrs-ok (kg:swap-text-placement code-attr mantel-attr)
                defs-ok (kg:swap-text-placement code-def mantel-def))
          (if (and attrs-ok defs-ok)
            (progn
              (kg:refresh-text-mask-for-tag insert-ent "ZWEVEND_CODE")
              (kg:refresh-text-mask-for-tag insert-ent "MANTEL_SPEC")
              (entupd insert-ent)
              (vl-catch-all-apply
                'vla-Regen
                (list (vla-get-ActiveDocument (vlax-get-acad-object)) 1))
              T)
            nil))
        nil))
    nil))

(defun kg:update-all-text-wipeouts (/ ss i insert-ent data blkname changed)
  (kg:hide-wipeout-frames)
  (setq changed 0)
  (setq ss (ssget "_X" '((0 . "INSERT") (66 . 1))))
  (if ss
    (progn
      (setq i 0)
      (while (< i (sslength ss))
        (setq insert-ent (ssname ss i))
        (setq data (entget insert-ent))
        (setq blkname (cdr (assoc 2 data)))
        (if (kg:starts-with (strcase (if blkname blkname "")) "KABELGEUL_")
          (setq changed (+ changed (kg:update-text-wipeouts-for-insert insert-ent))))
        (setq i (1+ i)))))
  (if (> changed 0)
    (vl-catch-all-apply
      'vla-Regen
      (list (vla-get-ActiveDocument (vlax-get-acad-object)) 1)))
  changed)

(defun kg:mask-command-ended (reactor params / cmd)
  (setq cmd (strcase (if (and params (car params)) (car params) "")))
  (if (member cmd '("EATTEDIT" "ATTEDIT" "-ATTEDIT" "DDEDIT"))
    (vl-catch-all-apply 'kg:update-all-text-wipeouts '()))
  (princ))

(defun kg:install-mask-reactor (/ result)
  (if (and (boundp '*kg:mask-command-reactor*) *kg:mask-command-reactor*)
    (vl-catch-all-apply 'vlr-remove (list *kg:mask-command-reactor*)))
  (setq result
    (vl-catch-all-apply
      'vlr-command-reactor
      (list nil (list (cons :vlr-commandEnded 'kg:mask-command-ended)))))
  (if (vl-catch-all-error-p result)
    (setq *kg:mask-command-reactor* nil)
    (setq *kg:mask-command-reactor* result)))

(defun kg:block-cross-angle (blkrec / next data pts p1 p2)
  (setq next (entnext blkrec))
  (while (and next (null pts))
    (setq data (entget next))
    (cond
      ((and (= (cdr (assoc 0 data)) "LWPOLYLINE")
            (= (cdr (assoc 8 data)) "E_LV_CROSS_SECTION"))
       (setq pts (kg:lwpolyline-points data)))
      ((= (cdr (assoc 0 data)) "ENDBLK")
       (setq next nil)))
    (if (and next (null pts))
      (setq next (entnext next))))
  (setq p1 (if pts (car pts) nil))
  (setq p2 (if pts (cadr pts) nil))
  (if (and (kg:point-p p1) (kg:point-p p2))
    (angle p1 p2)
    0.0))

(defun kg:world->block (insert-ent pt / data base rot sx sy dx dy)
  (setq data (entget insert-ent))
  (setq base (cdr (assoc 10 data)))
  (setq rot (if (assoc 50 data) (cdr (assoc 50 data)) 0.0))
  (setq sx (if (and (assoc 41 data) (/= (cdr (assoc 41 data)) 0.0)) (cdr (assoc 41 data)) 1.0))
  (setq sy (if (and (assoc 42 data) (/= (cdr (assoc 42 data)) 0.0)) (cdr (assoc 42 data)) 1.0))
  (setq dx (- (car pt) (car base)))
  (setq dy (- (cadr pt) (cadr base)))
  (list
    (/ (+ (* dx (cos rot)) (* dy (sin rot))) sx)
    (/ (+ (* (- dx) (sin rot)) (* dy (cos rot))) sy)
    0.0))

(defun kg:block->world (insert-ent pt / data base rot sx sy x y)
  (setq data (entget insert-ent))
  (setq base (cdr (assoc 10 data)))
  (setq rot (if (assoc 50 data) (cdr (assoc 50 data)) 0.0))
  (setq sx (if (assoc 41 data) (cdr (assoc 41 data)) 1.0))
  (setq sy (if (assoc 42 data) (cdr (assoc 42 data)) 1.0))
  (setq x (* (car pt) sx))
  (setq y (* (cadr pt) sy))
  (list
    (+ (car base) (- (* x (cos rot)) (* y (sin rot))))
    (+ (cadr base) (+ (* x (sin rot)) (* y (cos rot))))
    (+ (if (caddr base) (caddr base) 0.0) (if (caddr pt) (caddr pt) 0.0))))

(defun kg:nearest-block-circle (blkrec pt / next data cen dist best bestdist)
  (setq next (entnext blkrec))
  (while next
    (setq data (entget next))
    (cond
      ((= (cdr (assoc 0 data)) "CIRCLE")
       (setq cen (cdr (assoc 10 data)))
       (setq dist (distance (kg:pt2 pt) (kg:pt2 cen)))
       (if (or (null bestdist) (< dist bestdist))
         (setq best next bestdist dist)))
      ((= (cdr (assoc 0 data)) "ENDBLK")
       (setq next nil)))
    (if next
      (setq next (entnext next))))
  best)

(defun kg:delete-rooien-lines-near (blkrec center / next data p1 p2 mid todel)
  (setq next (entnext blkrec))
  (while next
    (setq data (entget next))
    (cond
      ((= (cdr (assoc 0 data)) "LINE")
       (setq p1 (cdr (assoc 10 data)))
       (setq p2 (cdr (assoc 11 data)))
       (setq mid (kg:midpoint p1 p2))
       (if (< (distance (kg:pt2 mid) (kg:pt2 center)) 0.05)
         (setq todel (cons next todel))))
      ((= (cdr (assoc 0 data)) "ENDBLK")
       (setq next nil)))
    (if next
      (setq next (entnext next))))
  (foreach next todel
    (entdel next)))

(defun kg:block-make-line (blkrec p1 p2 layer linetype)
  (entmakex
    (list '(0 . "LINE")
          (cons 330 blkrec)
          '(100 . "AcDbEntity")
          (cons 8 layer)
          (cons 6 linetype)
          '(100 . "AcDbLine")
          (cons 10 (kg:pt3 p1))
          (cons 11 (kg:pt3 p2)))))

(defun kg:sync-icon-type (blkrec circle-ent typ ang combi-meta / layer data center r ext combi)
  (setq layer (kg:type->layer typ))
  (setq data (entget circle-ent))
  (setq center (cdr (assoc 10 data)))
  (setq r (if (assoc 40 data) (cdr (assoc 40 data)) 0.25))
  (setq combi (if combi-meta (kg:double-p (cadr combi-meta)) (> r 0.27)))
  (setq r (if (or combi (kg:ms-type-p typ)) 0.30 0.25))
  (kg:update-ent-codes circle-ent (list (cons 8 layer) (cons 6 "Continuous") (cons 40 r)))
  (kg:delete-rooien-lines-near blkrec center)
  (cond
     ((and combi (or (= typ "Rooien") (= typ "MS Rooien")))
     (setq ext (* 2.0 r))
     (kg:block-make-line blkrec (kg:p+ center (+ ang (* 0.25 pi)) ext) (kg:p+ center (+ ang (* 1.25 pi)) ext) layer "DASHDOT")
     (kg:block-make-line blkrec (kg:p+ center (+ ang (* 0.75 pi)) ext) (kg:p+ center (+ ang (* 1.75 pi)) ext) layer "DASHDOT")
     ;; De doorlopende Combi-streep blijft aanwezig en draait bij rooien
     ;; 15 graden linksom, zodat hij niet samenvalt met het rooikruis.
     (kg:block-make-line
       blkrec
       (kg:p+ center (+ ang (* 0.25 pi) (/ pi 12.0)) r)
       (kg:p+ center (+ ang (* 1.25 pi) (/ pi 12.0)) r)
       layer
       "Continuous"))
    (combi
     (kg:block-make-line
       blkrec
       (kg:p+ center (+ ang (* 0.25 pi)) r)
       (kg:p+ center (+ ang (* 1.25 pi)) r)
       layer
       "Continuous"))
     ((or (= typ "Rooien") (= typ "MS Rooien"))
     (progn
      (setq ext (* 2.0 r))
      (kg:block-make-line blkrec (kg:p+ center (+ ang (* 0.25 pi)) ext) (kg:p+ center (+ ang (* 1.25 pi)) ext) layer "DASHDOT")
      (kg:block-make-line blkrec (kg:p+ center (+ ang (* 0.75 pi)) ext) (kg:p+ center (+ ang (* 1.75 pi)) ext) layer "DASHDOT")))))

(defun kg:sync-insert-from-attrs (insert-ent / data blkname blkrec ang i dir-ent text-ent dir-data text-data typ layer raw label attdef circle local-pt combi-meta)
  (if (and insert-ent (= (cdr (assoc 0 (entget insert-ent))) "INSERT"))
    (progn
      (setq data (entget insert-ent))
      (setq blkname (cdr (assoc 2 data)))
      (setq blkrec (tblobjname "BLOCK" blkname))
      (if blkrec
        (progn
           (setq ang (kg:block-cross-angle blkrec))
           (setq i 1)
           (setq text-ent (kg:attr-ent-by-index insert-ent "KABEL" i))
           (while text-ent
             (setq dir-ent (kg:attr-ent-by-index insert-ent "RICHTING" i))
            (if (and dir-ent text-ent)
              (progn
                (setq dir-data (entget dir-ent))
                (setq text-data (entget text-ent))
                (setq typ (kg:normalize-type (cdr (assoc 1 dir-data))))
                 (if typ
                   (progn
                     (setq combi-meta (kg:block-option-metadata blkrec "COMBI" i))
                     (setq layer (kg:type->text-layer typ))
                    (setq raw (cdr (assoc 1 text-data)))
                    (setq label (kg:prefixed-label typ (kg:strip-known-prefix raw)))
                    (kg:update-ent-codes dir-ent (list (cons 1 typ) (cons 8 "Opmerking")))
                    (kg:update-ent-codes text-ent (list (cons 1 label) (cons 8 layer)))
                    (setq attdef (kg:block-attdef-ent blkrec (kg:index-tag "RICHTING" i)))
                    (if attdef
                      (kg:update-ent-codes attdef (list (cons 1 typ) (cons 8 "Opmerking"))))
                    (setq attdef (kg:block-attdef-ent blkrec (kg:index-tag "KABEL" i)))
                    (if attdef
                      (kg:update-ent-codes attdef (list (cons 1 label) (cons 8 layer))))
                     (setq local-pt (kg:world->block insert-ent (kg:attribute-point text-data)))
                     (setq circle (kg:nearest-block-circle blkrec local-pt))
                     (if circle
                       (kg:sync-icon-type blkrec circle typ ang combi-meta))))))
             (setq i (1+ i))
             (setq text-ent (kg:attr-ent-by-index insert-ent "KABEL" i)))
           (entupd insert-ent))))))

(defun kg:draw-mantelbuis (pt)
  (kg:make-circle pt *kg:mantelbuis-radius* "Mantelbuis"))

(defun kg:draw-mantelbuis-radius (pt rad)
  (kg:make-circle pt rad "Mantelbuis"))

(defun kg:arc-points (center rad a1 a2 steps / i pts a)
  (setq i 0 pts '())
  (while (<= i steps)
    (setq a (+ a1 (* (/ (- a2 a1) (* 1.0 steps)) i)))
    (setq pts (append pts (list (polar center a rad))))
    (setq i (1+ i)))
  pts)

(defun kg:draw-mantelbuis-capsule (pts ang r / base unit minp maxp p pos c1 c2 outline)
  (if (null r) (setq r *kg:mantelbuis-radius*))
  (if (= (length pts) 1)
    (kg:draw-mantelbuis-radius (car pts) r)
    (progn
      (setq base (car pts))
      (setq unit (list (cos ang) (sin ang)))
      (setq minp nil maxp nil)
      (foreach p pts
        (setq pos (kg:dot2 (kg:vector2 base p) unit))
        (if (or (null minp) (< pos minp)) (setq minp pos))
        (if (or (null maxp) (> pos maxp)) (setq maxp pos)))
      (if (< (abs (- maxp minp)) 1e-8)
        (kg:draw-mantelbuis-radius base r)
        (progn
          (setq c1 (polar base ang minp))
          (setq c2 (polar base ang maxp))
          (setq outline
            (append
              (kg:arc-points c2 r (+ ang (/ pi 2.0)) (- ang (/ pi 2.0)) 8)
              (kg:arc-points c1 r (- ang (/ pi 2.0)) (- ang (* 1.5 pi)) 8)))
          (kg:make-lwpoly outline "Mantelbuis" T))))))

(defun kg:item-mantel-radius (item)
  ;; Enkel, Dubbel en Drie gebruiken exact dezelfde mantelbuismaat.
  *kg:mantelbuis-radius*)

(defun kg:draw-mantelbuis-items (items ang / item key pt pt2 rad entry groups singles group twosided)
  (setq groups '() singles '())
  (foreach item items
    (setq key (kg:mantel-group-key (nth 3 item)))
    (setq pt (nth 4 item))
    (setq pt2 (nth 11 item))
    (setq twosided (kg:double-p (nth 10 item)))
    (setq rad (kg:item-mantel-radius item))
    (cond
      ;; Twee-zijdig krijgt altijd twee afzonderlijke mantelbuiscirkels.
      ;; Zo blijft tussen beide symbolen een duidelijke vrije ruimte zichtbaar.
      ((and twosided (kg:mantel-p (nth 3 item)))
       (setq singles (cons (list pt rad) singles))
       (if (kg:point-p pt2)
         (setq singles (cons (list pt2 rad) singles))))
      (key
       (setq entry (assoc key groups))
       (if entry
         (setq groups
           (subst
             (cons key (cons (max rad (cadr entry)) (cons pt (cddr entry))))
             entry groups))
         (setq groups (cons (list key rad pt) groups))))
      ((kg:mantel-p (nth 3 item))
       (setq singles (cons (list pt rad) singles)))))
  (foreach entry singles
    (kg:draw-mantelbuis-radius (car entry) (cadr entry)))
  (foreach group groups
    (kg:draw-mantelbuis-capsule (reverse (cddr group)) ang (cadr group))))

(defun kg:draw-combi-logo (pt ang layer / r)
  (setq r 0.30)
  (kg:make-circle pt r layer)
  (kg:draw-combi-line pt ang layer r))

(defun kg:draw-combi-line (pt ang layer r)
  (kg:make-line
    (kg:p+ pt (+ ang (* 0.25 pi)) r)
    (kg:p+ pt (+ ang (* 1.25 pi)) r)
    layer))

(defun kg:draw-combi-rooien-line (pt ang layer r)
  ;; Bij te rooien 15 graden linksom ten opzichte van de normale Combi-streep.
  (kg:make-line
    (kg:p+ pt (+ ang (* 0.25 pi) (/ pi 12.0)) r)
    (kg:p+ pt (+ ang (* 1.25 pi) (/ pi 12.0)) r)
    layer))

(defun kg:half-rooien-p (typ)
  (= typ "Half rooien"))

(defun kg:ms-type-p (typ)
  (member typ '("MS Nieuw" "MS Bestaand" "MS Rooien")))

(defun kg:draw-single-cable-icon (pt ang typ layer combi)
  (cond
    ((or (= typ "Rooien") (= typ "MS Rooien"))
     (kg:draw-rooien-logo-radius
       pt ang layer
       (if (or (kg:double-p combi) (kg:ms-type-p typ)) 0.30 0.25))
     (if (kg:double-p combi)
       (kg:draw-combi-rooien-line pt ang layer 0.30)))
    ((kg:double-p combi)
     (kg:draw-combi-logo pt ang layer))
    (T
     (kg:make-circle pt (if (kg:ms-type-p typ) 0.30 0.25) layer))))

(defun kg:draw-rooien-span-line (p1 p2 layer)
  (kg:track-ent
    (entmakex
      (list '(0 . "LINE")
            '(100 . "AcDbEntity")
            (cons 8 layer)
            '(6 . "DASHDOT")
            '(100 . "AcDbLine")
            (cons 10 (kg:pt3 p1))
            (cons 11 (kg:pt3 p2))))))

(defun kg:draw-rooien-logo-radius (pt ang layer r / ext)
  (setq ext (* 2.0 r))
  (kg:make-circle pt r layer)
  (kg:draw-rooien-span-line
    (kg:p+ pt (+ ang (* 0.25 pi)) ext)
    (kg:p+ pt (+ ang (* 1.25 pi)) ext)
    layer)
  (kg:draw-rooien-span-line
    (kg:p+ pt (+ ang (* 0.75 pi)) ext)
    (kg:p+ pt (+ ang (* 1.75 pi)) ext)
    layer))

(defun kg:draw-double-rooien-logo (p1 p2 ang layer combi typ / r ext out1 out2 a1 a2 b1 b2)
  (setq r (if (or (kg:double-p combi) (kg:ms-type-p typ)) 0.30 0.25))
  (setq ext (* 2.0 r))
  (setq out1 (- ang (/ pi 2.0)))
  (setq out2 (+ ang (/ pi 2.0)))
  (setq a1 (kg:p+ p1 (+ out1 (* 0.25 pi)) ext))
  (setq a2 (kg:p+ p1 (- out1 (* 0.25 pi)) ext))
  (setq b1 (kg:p+ p2 (+ out2 (* 0.25 pi)) ext))
  (setq b2 (kg:p+ p2 (- out2 (* 0.25 pi)) ext))
  (kg:make-circle p1 r layer)
  (kg:make-circle p2 r layer)
  (kg:draw-rooien-span-line a1 b1 layer)
  (kg:draw-rooien-span-line a2 b2 layer)
  (if (kg:double-p combi)
    (progn
      (kg:draw-combi-rooien-line p1 ang layer r)
      (kg:draw-combi-rooien-line p2 ang layer r))))

(defun kg:draw-cable-icon (pt geul-ang typ double triple mantel combi / layer double-offset p1 p2 is-double side bottom-y top-y p3)
  (setq typ (kg:normalize-type typ))
  (if (null typ) (setq typ "K01"))
  (setq layer (kg:type->layer typ))
  (setq double-offset 0.22)
  (setq is-double (or (kg:double-p double) (kg:half-rooien-p typ)))
  (cond
    ((and (kg:double-p triple) (not (kg:half-rooien-p typ)))
     ;; Twee licht overlappende cirkels onderin en de derde erboven.
     (setq side 0.21 bottom-y -0.12 top-y 0.24)
     (setq p1 (kg:local->world pt geul-ang (- side) bottom-y))
     (setq p2 (kg:local->world pt geul-ang side bottom-y))
     (setq p3 (kg:local->world pt geul-ang 0.0 top-y))
     (kg:draw-single-cable-icon p1 geul-ang typ layer combi)
     (kg:draw-single-cable-icon p2 geul-ang typ layer combi)
     (kg:draw-single-cable-icon p3 geul-ang typ layer combi))
    (is-double
     (setq p1 (polar pt (- geul-ang (/ pi 2.0)) double-offset))
     (setq p2 (polar pt (+ geul-ang (/ pi 2.0)) double-offset))
     (cond
       ((kg:half-rooien-p typ)
        (kg:make-circle p1 0.25 (kg:type->layer "Bestaand"))
        (kg:draw-rooien-logo p2 geul-ang (kg:type->layer "Rooien")))
        ((or (= typ "Rooien") (= typ "MS Rooien"))
         (kg:draw-double-rooien-logo p1 p2 geul-ang layer combi typ))
       (T
        (kg:draw-single-cable-icon p1 geul-ang typ layer combi)
        (kg:draw-single-cable-icon p2 geul-ang typ layer combi))))
    (T
     (kg:draw-single-cable-icon pt geul-ang typ layer combi))))

(defun kg:draw-cable-attributes (text-pt text-ang text-just typ txt attr-index / layer label tag)
  (setq typ (kg:normalize-type typ))
  (if (null typ) (setq typ "K01"))
  (if (null txt) (setq txt ""))
  (setq layer (kg:type->text-layer typ))
  (setq label (kg:prefixed-label typ txt))
  (setq tag (kg:index-tag "KABEL" attr-index))
  (kg:make-attribute text-pt 0.55 text-ang tag (strcat "Kabel/leiding tekst " (kg:index-string attr-index) ":") label layer text-just 0))

(defun kg:draw-bestaand-attribute (text-pt text-ang text-just txt attr-index / layer label tag)
  (if (null txt) (setq txt ""))
  (setq layer (kg:type->layer "Bestaand"))
  (setq label (kg:prefixed-label "Bestaand" txt))
  (setq tag (kg:index-tag "BESTAAND" attr-index))
  (kg:make-attribute text-pt 0.55 text-ang tag (strcat "Bestaande kabel tekst " (kg:index-string attr-index) ":") label layer text-just 0))

(defun kg:draw-side2-attribute (text-pt text-ang text-just typ txt attr-index / layer label tag)
  (setq typ (kg:normalize-type typ))
  (if (null typ) (setq typ "K01"))
  (if (null txt) (setq txt ""))
  (setq layer (kg:type->text-layer typ))
  (setq label (kg:prefixed-label typ txt))
  (setq tag (kg:index-tag "ZIJDE2" attr-index))
  (kg:make-attribute text-pt 0.55 text-ang tag
    (strcat "Kabel/leiding tekst zijde 2 " (kg:index-string attr-index) ":")
    label layer text-just 0))

(defun kg:half-rooien-text-points (cable-pt geul-ang text-pt text-ang / rooien-circle rooien-away rooien-just)
  (setq rooien-circle (polar cable-pt (+ geul-ang (/ pi 2.0)) 0.22))
  (setq rooien-away (+ (angle cable-pt text-pt) pi))
  (setq rooien-just
    (if (>= (cos (- rooien-away text-ang)) 0.0)
      0
      2))
  (list (kg:p+ rooien-circle rooien-away 0.65) text-pt rooien-just))

(defun kg:two-sided-text-points (primary-pt secondary-pt text-pt text-ang / away just primary-text-pt touch-distance)
  ;; De buitenste tekst is de tekst die in versie X aan de juiste zijde is gezet.
  ;; Plaats hem nu zo dicht bij de halve bak dat de rand van zijn wipeout exact
  ;; aansluit op de wipeout van de bak. De vaste bakhoogte en de vaste
  ;; kabelhartafstand worden samen gebruikt; het kabeltekstmasker loopt
  ;; 0.20 terug achter het invoegpunt.
  (setq away (+ (angle primary-pt text-pt) pi))
  (setq just (if (>= (cos (- away text-ang)) 0.0) 0 2))
  (setq touch-distance
    (+ (/ (- *kg:bakhoogte* *kg:twosided-center-sep*) 2.0) 0.20))
  (setq primary-text-pt (kg:p+ primary-pt away touch-distance))

  ;; De andere tekst blijft op het normale tekstpunt onder de bak staan.
  (list primary-text-pt text-pt just))

(defun kg:draw-split-attribute (pt ang just split attr-index / tag val)
  (setq tag (kg:index-tag "VOLG" attr-index))
  (setq val (if split "1" "0"))
  ;; Onzichtbaar + constant: metadata blijft in het unieke block zonder extra invoerveld.
  (kg:make-attribute pt 0.10 ang tag "Volgende kabel begint een nieuwe geulbak:" val "E_LV_CROSS_SECTION" just 3))

(defun kg:draw-copy-option-attributes (pt ang just double triple mantel combi twosided side2double side2triple side2combi attr-index / mantel-val)
  ;; Bewaar de kabelvorm van beide zijden expliciet. Daardoor blijven de
  ;; onafhankelijke keuzes ook behouden bij KGOMNICOPY.
  (setq mantel-val (kg:mantel-value mantel))
  (kg:make-attribute pt 0.10 ang (kg:index-tag "DUBBEL" attr-index)
    "Dubbele kabel zijde 1:" (if (kg:double-p double) "1" "0") "E_LV_CROSS_SECTION" just 3)
  (kg:make-attribute pt 0.10 ang (kg:index-tag "DRIE" attr-index)
    "Drie kabelcirkels zijde 1:" (if (kg:double-p triple) "1" "0") "E_LV_CROSS_SECTION" just 3)
  (kg:make-attribute pt 0.10 ang (kg:index-tag "COMBI" attr-index)
    "Combi zijde 1:" (if (kg:double-p combi) "1" "0") "E_LV_CROSS_SECTION" just 3)
  (kg:make-attribute pt 0.10 ang (kg:index-tag "DUBBEL2" attr-index)
    "Dubbele kabel zijde 2:" (if (kg:double-p side2double) "1" "0") "E_LV_CROSS_SECTION" just 3)
  (kg:make-attribute pt 0.10 ang (kg:index-tag "DRIE2" attr-index)
    "Drie kabelcirkels zijde 2:" (if (kg:double-p side2triple) "1" "0") "E_LV_CROSS_SECTION" just 3)
  (kg:make-attribute pt 0.10 ang (kg:index-tag "COMBI2" attr-index)
    "Combi zijde 2:" (if (kg:double-p side2combi) "1" "0") "E_LV_CROSS_SECTION" just 3)
  (kg:make-attribute pt 0.10 ang (kg:index-tag "MANTEL" attr-index)
    "Mantelbuis:" (if mantel-val mantel-val "") "E_LV_CROSS_SECTION" just 3)
  (kg:make-attribute pt 0.10 ang (kg:index-tag "TWEEZIJDIG" attr-index)
    "Twee-zijdige kabel:" (if (kg:double-p twosided) "1" "0") "E_LV_CROSS_SECTION" just 3))

(defun kg:draw-cable (pt text-pt geul-ang text-ang text-just typ txt double triple mantel attr-index /)
  (kg:draw-cable-icon pt geul-ang typ double triple mantel nil)
  (kg:draw-cable-attributes text-pt text-ang text-just typ txt attr-index))

(defun kg:clamp-int (val minv maxv)
  (if (< val minv) (setq val minv))
  (if (> val maxv) (setq val maxv))
  val)

(defun kg:layer-direction-type (upper / n key result)
  (setq n 12)
  (setq result nil)
  (while (and (>= n 1) (null result))
    (setq key (if (< n 10) (strcat "0" (itoa n)) (itoa n)))
    (if (or (wcmatch upper (strcat "*K" key "*"))
            (wcmatch upper (strcat "*RI" (itoa n) "*"))
            (wcmatch upper (strcat "*RI " (itoa n) "*")))
      (setq result (strcat "K" key)))
    (setq n (1- n)))
  result)

(defun kg:layer->type (layer / upper num dir)
  (setq upper (strcase (if layer layer "")))
  (cond
    ((wcmatch upper "*MS NIEUW*") "MS Nieuw")
    ((wcmatch upper "*MS BESTAAND*") "MS Bestaand")
    ((wcmatch upper "*MS ROOIEN*") "MS Rooien")
    ((wcmatch upper "*ROOIEN*") "Rooien")
    ((wcmatch upper "*BESTAAND*") "Bestaand")
    ((setq dir (kg:layer-direction-type upper)) dir)
    ((wcmatch upper "*AANSLUITING LS K##*")
     (setq num (substr upper (- (strlen upper) 1) 2))
     (strcat "K" num))
    ((wcmatch upper "*AANSL LS RI##*")
     (setq num (substr upper (- (strlen upper) 1) 2))
     (strcat "K" num))
    ((wcmatch upper "*AANSL LS RI#*")
     (setq num (substr upper (strlen upper) 1))
     (strcat "K0" num))
    ((wcmatch upper "* RI##*")
     (setq num (substr upper (- (strlen upper) 1) 2))
     (strcat "K" num))
    ((wcmatch upper "* RI#*")
     (setq num (substr upper (strlen upper) 1))
     (strcat "K0" num))
    (T nil)))

(defun kg:cross2 (a b)
  (- (* (car a) (cadr b)) (* (cadr a) (car b))))

(defun kg:sub2 (a b)
  (list (- (car a) (car b)) (- (cadr a) (cadr b))))

(defun kg:point2 (p)
  (list (car p) (cadr p)))

(defun kg:between (v lo hi eps)
  (and (>= v (- lo eps)) (<= v (+ hi eps))))

(defun kg:segment-intersection-t (p1 p2 q1 q2 / r s den qp t u eps)
  (setq eps 1e-8)
  (setq p1 (kg:point2 p1))
  (setq p2 (kg:point2 p2))
  (setq q1 (kg:point2 q1))
  (setq q2 (kg:point2 q2))
  (setq r (kg:sub2 p2 p1))
  (setq s (kg:sub2 q2 q1))
  (setq den (kg:cross2 r s))
  (if (> (abs den) eps)
    (progn
      (setq qp (kg:sub2 q1 p1))
      (setq t (/ (kg:cross2 qp s) den))
      (setq u (/ (kg:cross2 qp r) den))
      (if (and (kg:between t 0.0 1.0 eps)
               (kg:between u 0.0 1.0 eps))
        t
        nil))
    nil))

(defun kg:lwpolyline-points (data / pts)
  (setq pts '())
  (foreach pair data
    (if (and (listp pair)
             (= (car pair) 10)
             (kg:point-p (cdr pair)))
      (setq pts (append pts (list (cdr pair))))))
  pts)

(defun kg:old-polyline-points (ent / pts next data)
  (setq pts '())
  (setq next (entnext ent))
  (while next
    (setq data (entget next))
    (cond
      ((= (cdr (assoc 0 data)) "VERTEX")
       (if (kg:point-p (cdr (assoc 10 data)))
         (setq pts (append pts (list (cdr (assoc 10 data)))))))
      ((= (cdr (assoc 0 data)) "SEQEND")
       (setq next nil)))
    (if next
      (setq next (entnext next))))
  pts)

(defun kg:polyline-points (ent data)
  (cond
    ((= (cdr (assoc 0 data)) "LWPOLYLINE")
     (kg:lwpolyline-points data))
    ((= (cdr (assoc 0 data)) "POLYLINE")
     (kg:old-polyline-points ent))
    (T nil)))

(defun kg:line-points (data / p1 p2)
  (setq p1 (cdr (assoc 10 data)))
  (setq p2 (cdr (assoc 11 data)))
  (if (and p1 p2)
    (list p1 p2)
    nil))

(defun kg:sample-curve-points (ent / sp ep steps i param pt pts)
  (setq pts '())
  (setq sp (vl-catch-all-apply 'vlax-curve-getStartParam (list ent)))
  (setq ep (vl-catch-all-apply 'vlax-curve-getEndParam (list ent)))
  (if (and (not (vl-catch-all-error-p sp))
           (not (vl-catch-all-error-p ep))
           sp ep)
    (progn
      (setq steps 80)
      (setq i 0)
      (while (<= i steps)
        (setq param (+ sp (* (/ (- ep sp) steps) i)))
        (setq pt (vl-catch-all-apply 'vlax-curve-getPointAtParam (list ent param)))
        (if (and (not (vl-catch-all-error-p pt)) pt)
          (setq pts (append pts (list pt))))
        (setq i (1+ i)))))
  pts)

(defun kg:entity-points (ent data)
  (cond
    ((member (cdr (assoc 0 data)) '("LWPOLYLINE" "POLYLINE"))
     (kg:polyline-points ent data))
    ((= (cdr (assoc 0 data)) "LINE")
     (kg:line-points data))
    ((member (cdr (assoc 0 data)) '("ARC" "CIRCLE" "ELLIPSE" "SPLINE"))
     (kg:sample-curve-points ent))
    (T nil)))

(defun kg:polyline-closed-p (data)
  (= 1 (logand 1 (if (assoc 70 data) (cdr (assoc 70 data)) 0))))

(defun kg:polyline-cross-t (p1 p2 pts closed / best a b t rest)
  (setq best nil)
  (setq rest pts)
  (while (and rest (cdr rest))
    (setq a (car rest))
    (setq b (cadr rest))
    (setq t (kg:segment-intersection-t p1 p2 a b))
    (if (and t (or (null best) (< t best)))
      (setq best t))
    (setq rest (cdr rest)))
  (if (and closed (> (length pts) 2))
    (progn
      (setq t (kg:segment-intersection-t p1 p2 (car (last pts)) (car pts)))
      (if (and t (or (null best) (< t best)))
        (setq best t))))
  best)

(defun kg:same-entity-p (a b)
  (and a b (= (vl-princ-to-string a) (vl-princ-to-string b))))

(defun kg:crossed-cable-types-raw (p1 p2 exclude / ss idx ent data layer typ pts t items)
  (setq items '())
  (setq ss (ssget "_X" '((0 . "LWPOLYLINE,POLYLINE,LINE,ARC,CIRCLE,ELLIPSE,SPLINE"))))
  (if ss
    (progn
      (setq idx 0)
      (while (< idx (sslength ss))
        (setq ent (ssname ss idx))
        (if (not (kg:same-entity-p ent exclude))
          (progn
            (setq data (entget ent))
            (setq layer (cdr (assoc 8 data)))
            (setq typ (kg:layer->type layer))
            (if typ
              (progn
                (setq pts (kg:entity-points ent data))
                (if (> (length pts) 1)
                  (progn
                    (setq t (kg:polyline-cross-t p1 p2 pts (kg:polyline-closed-p data)))
                    (if t
                      (setq items (cons (list t typ) items)))))))))
        (setq idx (1+ idx)))))
  (mapcar 'cadr
    (vl-sort items
      '(lambda (a b) (< (car a) (car b))))))

(defun kg:crossed-cable-types (p1 p2 exclude / result)
  (setq result
    (vl-catch-all-apply
      'kg:crossed-cable-types-raw
      (list p1 p2 exclude)))
  (if (vl-catch-all-error-p result)
    nil
    result))

(defun kg:fence-cable-types-raw (p1 p2 exclude / ss idx ent data layer typ items)
  (setq items '())
  (setq ss (ssget "_F" (list p1 p2) '((0 . "LWPOLYLINE,POLYLINE,LINE,ARC,CIRCLE,ELLIPSE,SPLINE"))))
  (if ss
    (progn
      (setq idx 0)
      (while (< idx (sslength ss))
        (setq ent (ssname ss idx))
        (if (not (kg:same-entity-p ent exclude))
          (progn
            (setq data (entget ent))
            (setq layer (cdr (assoc 8 data)))
            (setq typ (kg:layer->type layer))
            (if typ
              (setq items (append items (list typ))))))
        (setq idx (1+ idx)))))
  items)

(defun kg:fence-cable-types (p1 p2 exclude / result)
  (setq result
    (vl-catch-all-apply
      'kg:fence-cable-types-raw
      (list p1 p2 exclude)))
  (if (vl-catch-all-error-p result)
    nil
    result))

(defun kg:count-crossed-polylines-raw (p1 p2 / ss idx ent data layer cnt)
  (setq cnt 0)
  (setq ss (ssget "_F" (list p1 p2) '((0 . "LWPOLYLINE,POLYLINE,LINE"))))
  (if ss
    (progn
      (setq idx 0)
      (while (< idx (sslength ss))
        (setq ent (ssname ss idx))
        (setq data (entget ent))
        (setq layer (cdr (assoc 8 data)))
        (if (not (member layer '("E_LV_CROSS_SECTION")))
          (setq cnt (1+ cnt)))
        (setq idx (1+ idx)))))
  cnt)

(defun kg:count-crossed-polylines (p1 p2 / result)
  (setq result
    (vl-catch-all-apply
      'kg:count-crossed-polylines-raw
      (list p1 p2)))
  (if (vl-catch-all-error-p result)
    1
    result))

(defun kg:type-index (typ / i found)
  (setq i 0 found 0)
  (foreach opt *kg:type-options*
    (if (= opt typ) (setq found i))
    (setq i (1+ i)))
  found)

(defun kg:popup-fill (key value)
  (start_list key)
  (mapcar 'add_list *kg:type-options*)
  (end_list)
  (set_tile key (itoa (kg:type-index value))))

(defun kg:shape-index (double triple combi)
  ;; De vier vormen zijn in de nieuwe GUI bewust onderling exclusief.
  (cond
    ((kg:double-p combi) 3)
    ((kg:double-p triple) 2)
    ((kg:double-p double) 1)
    (T 0)))

(defun kg:shape-flags (index)
  (cond
    ((= index 1) (list T nil nil))
    ((= index 2) (list nil T nil))
    ((= index 3) (list nil nil T))
    (T (list nil nil nil))))

(defun kg:shape-popup-fill (key double triple combi)
  (start_list key)
  (mapcar 'add_list *kg:shape-options*)
  (end_list)
  (set_tile key (itoa (kg:shape-index double triple combi))))

(defun kg:dialog-row-controls (i active / typ twosided half state)
  (setq state (if active 0 1))
  (setq typ (nth (atoi (get_tile (strcat "type" (itoa i)))) *kg:type-options*))
  (if (null typ) (setq typ "K01"))
  (setq half (kg:half-rooien-p typ))
  (setq twosided (= (get_tile (strcat "twosided" (itoa i))) "1"))
  ;; Half te rooien houdt zijn bestaande speciale symbool en kan niet nogmaals
  ;; als twee-zijdige kabel worden gebruikt.
  (if half
    (progn
      (set_tile (strcat "vorm1" (itoa i)) "1")
      (set_tile (strcat "twosided" (itoa i)) "0")
      (setq twosided nil)))
  (mode_tile (strcat "type" (itoa i)) state)
  (mode_tile (strcat "label" (itoa i)) state)
  (mode_tile (strcat "bestaand" (itoa i))
    (if (and active (or half twosided)) 0 1))
  (mode_tile (strcat "vorm1" (itoa i))
    (if (and active (not half)) 0 1))
  (mode_tile (strcat "twosided" (itoa i))
    (if (and active (not half)) 0 1))
  (mode_tile (strcat "vorm2" (itoa i))
    (if (and active twosided (not half)) 0 1))
  (mode_tile (strcat "mantel" (itoa i)) state)
  (mode_tile (strcat "split" (itoa i)) state)
  (mode_tile (strcat "insertbefore" (itoa i)) state)
  (mode_tile (strcat "insertafter" (itoa i)) state)
  (mode_tile (strcat "remove" (itoa i)) state))

(defun kg:dialog-type-changed (i / count)
  (setq count (max 1 (atoi (get_tile "count"))))
  (kg:dialog-row-controls i (<= i count)))

(defun kg:dialog-double-changed (i)
  ;; Compatibele hulpfunctie voor oudere geladen DCL-definities.
  (kg:dialog-type-changed i))

(defun kg:dialog-triple-changed (i)
  ;; Compatibele hulpfunctie voor oudere geladen DCL-definities.
  (kg:dialog-type-changed i))

(defun kg:dialog-twosided-changed (i)
  (kg:dialog-type-changed i))

(defun kg:update-row-state (count / i row-count)
  (setq row-count (max 1 *kg:dialog-row-count*))
  (setq i 1)
  (while (<= i row-count)
    (kg:dialog-row-controls i (<= i count))
    (setq i (1+ i))))

(defun kg:dialog-row-data (i / type-idx typ label bestaand shape1 shape2 double triple combi side2double side2triple side2combi mantel split twosided)
  (setq type-idx (atoi (get_tile (strcat "type" (itoa i)))))
  (setq typ (nth type-idx *kg:type-options*))
  (if (null typ) (setq typ "K01"))
  (setq label (get_tile (strcat "label" (itoa i))))
  (setq bestaand (get_tile (strcat "bestaand" (itoa i))))
  (setq twosided (kg:double-p (get_tile (strcat "twosided" (itoa i)))))
  (setq shape1 (kg:shape-flags (atoi (get_tile (strcat "vorm1" (itoa i))))))
  (setq double (car shape1)
        triple (cadr shape1)
        combi (caddr shape1))
  (setq shape2 (kg:shape-flags (atoi (get_tile (strcat "vorm2" (itoa i))))))
  (setq side2double (car shape2)
        side2triple (cadr shape2)
        side2combi (caddr shape2))
  (if (kg:half-rooien-p typ)
    (setq double T triple nil combi nil twosided nil
          side2double nil side2triple nil side2combi nil))
  (if (not twosided)
    (setq side2double nil side2triple nil side2combi nil))
  (setq mantel (kg:mantel-value (get_tile (strcat "mantel" (itoa i)))))
  (setq split (kg:double-p (get_tile (strcat "split" (itoa i)))))
  (list typ label bestaand double triple combi mantel split twosided
        side2double side2triple side2combi))

(defun kg:dialog-rows (count / i rows)
  (setq rows '() i 1)
  (while (<= i count)
    (setq rows (append rows (list (kg:dialog-row-data i))))
    (setq i (1+ i)))
  rows)

(defun kg:blank-dialog-row ()
  (list "K01" "" "" nil nil nil nil nil nil nil nil nil))

(defun kg:dialog-result-from-rows (rows mantel-spec / row types labels bestaande-labels doubles triples combis mantels splits twosideds side2doubles side2triples side2combis)
  (setq types '() labels '() bestaande-labels '() doubles '() triples '()
        combis '() mantels '() splits '() twosideds '()
        side2doubles '() side2triples '() side2combis '())
  (foreach row rows
    (setq types (append types (list (nth 0 row))))
    (setq labels (append labels (list (nth 1 row))))
    (setq bestaande-labels (append bestaande-labels (list (nth 2 row))))
    (setq doubles (append doubles (list (nth 3 row))))
    (setq triples (append triples (list (nth 4 row))))
    (setq combis (append combis (list (nth 5 row))))
    (setq mantels (append mantels (list (nth 6 row))))
    (setq splits (append splits (list (nth 7 row))))
    (setq twosideds (append twosideds (list (nth 8 row))))
    (setq side2doubles (append side2doubles (list (nth 9 row))))
    (setq side2triples (append side2triples (list (nth 10 row))))
    (setq side2combis (append side2combis (list (nth 11 row)))))
  (list (length rows) types labels doubles mantels splits combis
        bestaande-labels triples mantel-spec twosideds
        side2doubles side2triples side2combis))

(defun kg:list-remove-at (lst index / out i item)
  (setq out '() i 1)
  (foreach item lst
    (if (/= i index) (setq out (append out (list item))))
    (setq i (1+ i)))
  out)

(defun kg:list-insert-at (lst index item / out i inserted old)
  (setq out '() i 1 inserted nil)
  (foreach old lst
    (if (and (not inserted) (= i index))
      (progn
        (setq out (append out (list item)))
        (setq inserted T)))
    (setq out (append out (list old)))
    (setq i (1+ i)))
  (if (not inserted) (setq out (append out (list item))))
  out)

(defun kg:dialog-collect (/ count row-count collect-count rows)
  (setq count (max 1 (atoi (get_tile "count"))))
  (setq row-count (max 1 *kg:dialog-row-count*))
  (setq collect-count (min count row-count))
  (setq rows (kg:dialog-rows collect-count))
  (while (< (length rows) count)
    (setq rows (append rows (list (kg:blank-dialog-row)))))
  (setq *kg:dialog-resize-count* (if (/= count row-count) count nil))
  (setq *kg:dialog-result*
    (kg:dialog-result-from-rows rows (get_tile "mantelspec"))))

(defun kg:dialog-remove-row (remove-index / count rows)
  (setq count (min (max 1 (atoi (get_tile "count"))) (max 1 *kg:dialog-row-count*)))
  (if (> count 1)
    (progn
      (setq rows (kg:list-remove-at (kg:dialog-rows count) remove-index))
      (setq *kg:dialog-resize-count* (length rows))
      (setq *kg:dialog-result*
        (kg:dialog-result-from-rows rows (get_tile "mantelspec")))
      (done_dialog 2))))

(defun kg:dialog-reverse-order (/ count rows)
  (setq count (min (max 1 (atoi (get_tile "count"))) (max 1 *kg:dialog-row-count*)))
  (setq rows (reverse (kg:dialog-rows count)))
  (setq *kg:dialog-resize-count* count)
  (setq *kg:dialog-result*
    (kg:dialog-result-from-rows rows (get_tile "mantelspec")))
  (done_dialog 2))

(defun kg:dialog-insert-row (insert-index / count rows)
  (setq count (min (max 1 (atoi (get_tile "count"))) (max 1 *kg:dialog-row-count*)))
  (setq rows (kg:list-insert-at (kg:dialog-rows count) insert-index (kg:blank-dialog-row)))
  (setq *kg:dialog-resize-count* (length rows))
  (setq *kg:dialog-result*
    (kg:dialog-result-from-rows rows (get_tile "mantelspec")))
  (done_dialog 2))

(defun kg:blank-labels (count / vals)
  (setq vals '())
  (while (> count 0) (setq vals (append vals (list "")) count (1- count)))
  vals)

(defun kg:blank-existing-labels (count / vals)
  (kg:blank-labels count))

(defun kg:blank-doubles (count / vals)
  (setq vals '())
  (while (> count 0) (setq vals (append vals (list nil)) count (1- count)))
  vals)

(defun kg:blank-triples (count / vals)
  (kg:blank-doubles count))

(defun kg:blank-twosideds (count / vals)
  (kg:blank-doubles count))

(defun kg:blank-mantels (count / vals)
  (kg:blank-doubles count))

(defun kg:blank-splits (count / vals)
  (kg:blank-doubles count))

(defun kg:blank-combis (count / vals)
  (kg:blank-doubles count))

(defun kg:double-p (val)
  (or (= val T) (= val 1) (= val "1")))

(defun kg:mantel-p (val)
  (and val
       (/= (kg:trim (vl-princ-to-string val)) "")
       (/= (kg:trim (vl-princ-to-string val)) "0")))

(defun kg:mantel-value (val / out)
  (setq out (kg:trim (if val (vl-princ-to-string val) "")))
  (if (or (= out "") (= out "0")) nil out))

(defun kg:mantel-individual-p (val / key)
  (setq key (strcase (kg:trim (if val (vl-princ-to-string val) ""))))
  (member key '("X" "J" "JA" "Y" "YES" "T" "TRUE")))

(defun kg:mantel-group-key (val / key)
  (setq key (kg:mantel-value val))
  (if (and key (not (kg:mantel-individual-p key))) (strcase key) nil))

(defun kg:dcl-header-line ()
  (strcat
    "    : row { "
    ": text { label = \"Nr\"; width = 3; alignment = centered; } "
    ": text { label = \"Kabeltype\"; width = 16; } "
    ": text { label = \"Tekst zijde 1\"; width = 20; } "
    ": text { label = \"Vorm zijde 1\"; width = 11; } "
    ": text { label = \"2-zijdig\"; width = 8; alignment = centered; } "
    ": text { label = \"Tekst zijde 2\"; width = 20; } "
    ": text { label = \"Vorm zijde 2\"; width = 11; } "
    ": text { label = \"Mantel\"; width = 7; } "
    ": text { label = \"Nieuwe bak\"; width = 9; alignment = centered; } "
    ": text { label = \"+ voor\"; width = 6; alignment = centered; } "
    ": text { label = \"+ na\"; width = 6; alignment = centered; } "
    ": text { label = \"Wis\"; width = 6; alignment = centered; } }"))

(defun kg:dcl-row-line (i)
  (strcat
    "    : row { : text { label = \"" (itoa i) "\"; width = 3; alignment = centered; } "
    ": popup_list { key = \"type" (itoa i) "\"; label = \"\"; width = 16; } "
    ": edit_box { key = \"label" (itoa i) "\"; label = \"\"; edit_width = 20; width = 20; } "
    ": popup_list { key = \"vorm1" (itoa i) "\"; label = \"\"; width = 11; } "
    ": toggle { key = \"twosided" (itoa i) "\"; label = \"\"; width = 8; } "
    ": edit_box { key = \"bestaand" (itoa i) "\"; label = \"\"; edit_width = 20; width = 20; } "
    ": popup_list { key = \"vorm2" (itoa i) "\"; label = \"\"; width = 11; } "
    ": edit_box { key = \"mantel" (itoa i) "\"; label = \"\"; edit_width = 5; width = 7; } "
    ": toggle { key = \"split" (itoa i) "\"; label = \"\"; width = 9; } "
    ": button { key = \"insertbefore" (itoa i) "\"; label = \"+\"; width = 6; } "
    ": button { key = \"insertafter" (itoa i) "\"; label = \"+\"; width = 6; } "
    ": button { key = \"remove" (itoa i) "\"; label = \"X\"; width = 6; } }"))

(defun kg:dcl-lines (/ lines i row-count)
  (setq row-count (max 1 *kg:dialog-row-count*))
  (setq lines
    '("kabelgeul_omni : dialog {"
      "  label = \"KabelGeul Omni - kabelvorm per zijde\";"
      "  : boxed_column {"
      "    label = \"Algemeen\";"
      "    : row {"
      "      : edit_box { key = \"count\"; label = \"Aantal kabelregels\"; edit_width = 4; width = 18; }"
      "      : edit_box { key = \"mantelspec\"; label = \"Mantelbuis specificaties\"; edit_width = 42; width = 52; }"
      "      : button { key = \"reverse\"; label = \"Volgorde omdraaien\"; width = 22; }"
      "    }"
      "    : text { label = \"Kies per zijde afzonderlijk: Enkel, Dubbel, Drie of Combi. Zijde 2 wordt actief bij 2-zijdig.\"; }"
      "  }"
      "  : boxed_column {"
      "    label = \"Kabels en zijden\";"))
  (setq lines (append lines (list (kg:dcl-header-line))))
  (setq i 1)
  (while (<= i row-count)
    (setq lines (append lines (list (kg:dcl-row-line i))))
    (setq i (1+ i)))
  (append lines '("  }" "  ok_cancel;" "}")))

(defun kg:write-temp-dcl (/ path file)
  (setq path (strcat (getvar "TEMPPREFIX") "KabelGeul_Omni.dcl"))
  (setq file (open path "w"))
  (if file
    (progn
      (foreach line (kg:dcl-lines) (write-line line file))
      (close file)
      path)
    nil))

(defun kg:dcl-path (/ same-dir support-path temp-path)
  (setq same-dir (if *kg:lsp-dir* (strcat *kg:lsp-dir* "\\KabelGeul_Omni.dcl") nil))
  (setq support-path (findfile "KabelGeul_Omni.dcl"))
  (setq temp-path (kg:write-temp-dcl))
  (cond
    ((and temp-path (findfile temp-path)) temp-path)
    ((and same-dir (findfile same-dir)) same-dir)
    (support-path support-path)
    (T nil)))

(defun kg:show-dialog (/ dcl-file dcl-id ok i typ label bestaand twosided mantel split result done side2double side2triple side2combi)
  (setq *kg:dialog-row-count* (max 1 *kg:last-count*))
  (setq result nil done nil)
  (while (not done)
    (setq dcl-file (kg:dcl-path))
    (if (null dcl-file)
      (progn
        (alert "KabelGeul dialoog kan niet worden gemaakt. Controleer of AutoCAD mag schrijven naar de tijdelijke map.")
        (setq done T result nil))
      (progn
        (setq dcl-id (load_dialog dcl-file))
        (if (not (new_dialog "kabelgeul_omni" dcl-id))
          (progn
            (unload_dialog dcl-id)
            (alert "Kan dialoog kabelgeul_omni niet openen.")
            (setq done T result nil))
          (progn
            (setq *kg:dialog-result* nil *kg:dialog-resize-count* nil)
            (set_tile "count" (itoa *kg:dialog-row-count*))
            (set_tile "mantelspec" (if *kg:last-mantel-spec* *kg:last-mantel-spec* ""))
            (setq i 1)
            (while (<= i *kg:dialog-row-count*)
              (setq typ (if (nth (1- i) *kg:last-types*) (nth (1- i) *kg:last-types*) "K01"))
              (setq label (if (nth (1- i) *kg:last-labels*) (nth (1- i) *kg:last-labels*) ""))
              (setq bestaand (if (nth (1- i) *kg:last-existing-labels*) (nth (1- i) *kg:last-existing-labels*) ""))
              (setq twosided (if (nth (1- i) *kg:last-twosideds*) "1" "0"))
              (setq side2double (nth (1- i) *kg:last-side2-doubles*))
              (setq side2triple (nth (1- i) *kg:last-side2-triples*))
              (setq side2combi (nth (1- i) *kg:last-side2-combis*))
              (setq mantel (if (nth (1- i) *kg:last-mantels*) (vl-princ-to-string (nth (1- i) *kg:last-mantels*)) ""))
              (setq split (if (nth (1- i) *kg:last-splits*) "1" "0"))
              (kg:popup-fill (strcat "type" (itoa i)) typ)
              (kg:shape-popup-fill (strcat "vorm1" (itoa i))
                (or (nth (1- i) *kg:last-doubles*) (kg:half-rooien-p typ))
                (nth (1- i) *kg:last-triples*)
                (nth (1- i) *kg:last-combis*))
              (kg:shape-popup-fill (strcat "vorm2" (itoa i))
                side2double side2triple side2combi)
              (set_tile (strcat "label" (itoa i)) label)
              (set_tile (strcat "bestaand" (itoa i)) bestaand)
              (set_tile (strcat "twosided" (itoa i)) twosided)
              (set_tile (strcat "mantel" (itoa i)) mantel)
              (set_tile (strcat "split" (itoa i)) split)
              (kg:dialog-type-changed i)
              (action_tile (strcat "type" (itoa i)) (strcat "(kg:dialog-type-changed " (itoa i) ")"))
              (action_tile (strcat "twosided" (itoa i)) (strcat "(kg:dialog-twosided-changed " (itoa i) ")"))
              (action_tile (strcat "insertbefore" (itoa i)) (strcat "(kg:dialog-insert-row " (itoa i) ")"))
              (action_tile (strcat "insertafter" (itoa i)) (strcat "(kg:dialog-insert-row " (itoa (1+ i)) ")"))
              (action_tile (strcat "remove" (itoa i)) (strcat "(kg:dialog-remove-row " (itoa i) ")"))
              (setq i (1+ i)))
            (action_tile "count" "(kg:update-row-state (max 1 (atoi (get_tile \"count\"))))")
            (action_tile "reverse" "(kg:dialog-reverse-order)")
            (action_tile "accept" "(kg:dialog-collect)(if *kg:dialog-resize-count* (done_dialog 2) (done_dialog 1))")
            (action_tile "cancel" "(done_dialog 0)")
            (setq ok (start_dialog))
            (unload_dialog dcl-id)
            (cond
              ((or (= ok 1) (= ok 2))
               (setq *kg:last-count* (car *kg:dialog-result*))
               (setq *kg:last-types* (cadr *kg:dialog-result*))
               (setq *kg:last-labels* (caddr *kg:dialog-result*))
               (setq *kg:last-doubles* (cadddr *kg:dialog-result*))
               (setq *kg:last-mantels* (nth 4 *kg:dialog-result*))
               (setq *kg:last-splits* (nth 5 *kg:dialog-result*))
               (setq *kg:last-combis* (nth 6 *kg:dialog-result*))
               (setq *kg:last-existing-labels* (nth 7 *kg:dialog-result*))
               (setq *kg:last-triples* (nth 8 *kg:dialog-result*))
               (setq *kg:last-mantel-spec* (nth 9 *kg:dialog-result*))
               (setq *kg:last-twosideds* (nth 10 *kg:dialog-result*))
               (setq *kg:last-side2-doubles* (nth 11 *kg:dialog-result*))
               (setq *kg:last-side2-triples* (nth 12 *kg:dialog-result*))
               (setq *kg:last-side2-combis* (nth 13 *kg:dialog-result*))
               (if (= ok 1)
                 (progn (setq result *kg:dialog-result*) (setq done T))
                 (setq *kg:dialog-row-count* *kg:dialog-resize-count*)))
              (T (setq result nil done T))))))))
  result)

(defun kg:draw-rooien-logo (pt ang layer / r ext)
  (kg:draw-rooien-logo-radius pt ang layer 0.25))

(defun kg:draw-trench-at-end (p1 p2 types labels doubles mantels splits combis existing-labels triples mantel-spec twosideds side2doubles side2triples side2combis / n spacing boxlen height top bottom stub ang text-props text-ang text-just mantel-text-props mantel-text-ang mantel-text-just mantel-text-pt pts x i gi pt text-pt double triple combi mantel twosided items item attr-index ss next blkname insert-before insert-ent oldattreq insert-result groups gtypes glabels gdoubles gtriples gmantels gcombis gexisting-labels gsplits gtwosideds group curr-types curr-labels curr-existing-labels curr-doubles curr-triples curr-mantels curr-combis curr-splits curr-twosideds offset group-start group-end group-boxlen group-n group-has-twosided attr-pts sorted-items floating-text-pt floating-text-ang floating-text-just swap-pt swap-ang swap-just primary-label existing-label secondary-label mask-index tray-polys lower-stub-a lower-stub-b lower-stub-mid tray-center mantel-inward-ang mantel-normal-a mantel-normal-b mantel-outward-ang mantel-base-pt entry-stub-start switch-u switch-diff switch-parallel switch-normal switch-inward mantel-axis mantel-q-pt mantel-low-pt mantel-q-vec mantel-low-vec mantel-parallel mantel-normal-offset has-twosided side-sep item-side-sep primary-pt secondary-pt side2double side2triple side2combi curr-side2doubles curr-side2triples curr-side2combis gside2doubles gside2triples gside2combis)
  (if (null doubles) (setq doubles (kg:blank-doubles (length types))))
  (if (null triples) (setq triples (kg:blank-triples (length types))))
  (if (null twosideds) (setq twosideds (kg:blank-twosideds (length types))))
  ;; Oudere data gebruikte op beide zijden dezelfde vorm. Die blijft als fallback behouden.
  (if (null side2doubles) (setq side2doubles doubles))
  (if (null side2triples) (setq side2triples triples))
  (if (null side2combis) (setq side2combis combis))
  (if (null mantels) (setq mantels (kg:blank-mantels (length types))))
  (if (null splits) (setq splits (kg:blank-splits (length types))))
  (if (null combis) (setq combis (kg:blank-combis (length types))))
  (if (null existing-labels) (setq existing-labels (kg:blank-existing-labels (length types))))
  (if (null mantel-spec) (setq mantel-spec ""))
  (setq *kg:new-ents* nil)

  (setq n (length types))
  (setq spacing *kg:kabelrij-spacing*)
  (setq boxlen (+ (* (max 1 (- n 1)) spacing) 2.10))
  (setq has-twosided nil)
  (foreach twosided twosideds
    (if (kg:double-p twosided) (setq has-twosided T)))
  ;; De twee-zijdige bak blijft compact. Met een hartafstand van 1.33 en
  ;; mantelradius 0.64 passen beide mantelbuizen binnen dezelfde bakhoogte van 3.00,
  ;; met een kleine buitenmarge en zonder dat de mantelbuizen elkaar raken.
  (setq height *kg:bakhoogte*)
  (setq side-sep *kg:twosided-center-sep*)
  (setq top 0.0)
  (setq bottom (- height))
  (setq stub 1.05)
  (setq ang (angle p1 p2))
  (setq text-props (kg:readable-text-props (- ang (/ pi 2.0))))
  (setq text-ang (car text-props))
  (setq text-just (cadr text-props))
  ;; Mantelbuistekst gebruikt dezelfde leesrichting als de overige kabelteksten.
  ;; De definitieve positie wordt na het opbouwen van de bak bepaald op het
  ;; midden van het onderste uitstekende leaderdeel tussen p1 en p2.
  (setq mantel-text-ang text-ang)
  (setq mantel-text-just text-just)
  (setq mantel-text-pt nil)

  (setq groups '() curr-types '() curr-labels '() curr-existing-labels '()
        curr-doubles '() curr-triples '() curr-mantels '() curr-combis '()
        curr-splits '() curr-twosideds '() curr-side2doubles '() curr-side2triples '() curr-side2combis '() i 0)
  (while (< i n)
    (if (and (nth i splits) (> (length curr-types) 0))
      (progn
        (setq groups
          (append groups
            (list
              (list curr-types curr-labels curr-doubles curr-mantels curr-combis
                    curr-existing-labels curr-splits curr-triples curr-twosideds
                    curr-side2doubles curr-side2triples curr-side2combis))))
        (setq curr-types '() curr-labels '() curr-existing-labels '()
              curr-doubles '() curr-triples '() curr-mantels '() curr-combis '()
              curr-splits '() curr-twosideds '() curr-side2doubles '() curr-side2triples '() curr-side2combis '())))
    (setq curr-types (append curr-types (list (nth i types))))
    (setq curr-labels (append curr-labels (list (nth i labels))))
    (setq curr-existing-labels (append curr-existing-labels (list (nth i existing-labels))))
    (setq curr-doubles (append curr-doubles (list (nth i doubles))))
    (setq curr-triples (append curr-triples (list (nth i triples))))
    (setq curr-mantels (append curr-mantels (list (nth i mantels))))
    (setq curr-combis (append curr-combis (list (nth i combis))))
    (setq curr-splits (append curr-splits (list (nth i splits))))
    (setq curr-twosideds (append curr-twosideds (list (nth i twosideds))))
    (setq curr-side2doubles (append curr-side2doubles (list (nth i side2doubles))))
    (setq curr-side2triples (append curr-side2triples (list (nth i side2triples))))
    (setq curr-side2combis (append curr-side2combis (list (nth i side2combis))))
    (if (= (1+ i) n)
      (progn
        (setq groups
          (append groups
            (list
              (list curr-types curr-labels curr-doubles curr-mantels curr-combis
                    curr-existing-labels curr-splits curr-triples curr-twosideds
                    curr-side2doubles curr-side2triples curr-side2combis))))
        (setq curr-types '() curr-labels '() curr-existing-labels '()
              curr-doubles '() curr-triples '() curr-mantels '() curr-combis '()
              curr-splits '() curr-twosideds '() curr-side2doubles '() curr-side2triples '() curr-side2combis '())))
    (setq i (1+ i)))

  (setq items '() tray-polys '() offset 0.0)
  (foreach group groups
    (setq gtypes (car group))
    (setq glabels (cadr group))
    (setq gdoubles (caddr group))
    (setq gmantels (cadddr group))
    (setq gcombis (nth 4 group))
    (setq gexisting-labels (nth 5 group))
    (setq gsplits (nth 6 group))
    (setq gtriples (nth 7 group))
    (setq gtwosideds (nth 8 group))
    (setq gside2doubles (nth 9 group))
    (setq gside2triples (nth 10 group))
    (setq gside2combis (nth 11 group))
    (setq group-n (length gtypes))
    (setq group-has-twosided nil)
    (foreach twosided gtwosideds
      (if (kg:double-p twosided) (setq group-has-twosided T)))
    ;; Twee-zijdig vergroot de baklengte niet meer kunstmatig. De vaste
    ;; hartafstand en de standaard eindmarges bepalen voor iedere rij dezelfde maat.
    (setq group-boxlen
      (+ (* (max 1 (- group-n 1)) spacing)
         2.10))
    (setq group-start (kg:local->world p2 ang offset 0.0))
    (setq group-end (kg:local->world p2 ang (+ offset group-boxlen stub) 0.0))
    (setq pts
      (if (= offset 0.0)
        (list p1 group-start
          (kg:local->world group-start ang 0.0 bottom)
          (kg:local->world group-start ang group-boxlen bottom)
          (kg:local->world group-start ang group-boxlen top)
          group-end)
        (list group-start
          (kg:local->world group-start ang 0.0 bottom)
          (kg:local->world group-start ang group-boxlen bottom)
          (kg:local->world group-start ang group-boxlen top)
          group-end)))
    ;; Laat de bak-wipeout aan beide open uiteinden even ver doorlopen.
    ;; Rechts liep hij al tot het bovenste uitsteeksel; links wordt hij nu
    ;; ook één STUB teruggetrokken tot het onderste uitsteeksel.
    (kg:make-wipeout
      (list
        (kg:local->world group-start ang (- stub) top)
        (kg:local->world group-start ang (+ group-boxlen stub) top)
        (kg:local->world group-start ang (+ group-boxlen stub) bottom)
        (kg:local->world group-start ang (- stub) bottom))
      "E_LV_CROSS_SECTION")
    (setq tray-polys (append tray-polys (list pts)))
    (setq gi 0)
    (foreach typ gtypes
      (setq x (+ (/ group-boxlen 2.0) (- (* gi spacing) (/ (* (- group-n 1) spacing) 2.0))))
      (setq pt (kg:local->world group-start ang x (/ bottom 2.0)))
      (setq text-pt (kg:local->world group-start ang x (- bottom 0.20)))
      (setq double (nth gi gdoubles))
      (setq triple (nth gi gtriples))
      (setq twosided (kg:double-p (nth gi gtwosideds)))
      (setq mantel (nth gi gmantels))
      (setq combi (nth gi gcombis))
      (setq side2double (nth gi gside2doubles))
      (setq side2triple (nth gi gside2triples))
      (setq side2combi (nth gi gside2combis))
      ;; Enkel, Dubbel, Drie en Combi zijn per zijde onderling exclusief.
      (if triple (setq double nil combi nil))
      (if combi (setq double nil triple nil))
      (if side2triple (setq side2double nil side2combi nil))
      (if side2combi (setq side2double nil side2triple nil))
      (if (not twosided)
        (setq side2double nil side2triple nil side2combi nil))
      (if twosided
        (progn
          ;; Bij Twee-zijdig kan iedere zijde Enkel, Dubbel, Drie of Combi tonen.
          ;; Alle mantelbuizen hebben dezelfde vaste straal; de vaste
          ;; hart-op-hartafstand houdt de twee mantelcirkels los van elkaar.
          (setq item-side-sep side-sep)
          (setq primary-pt
            (kg:local->world group-start ang x (+ (/ bottom 2.0) (/ item-side-sep 2.0))))
          (setq secondary-pt
            (kg:local->world group-start ang x (- (/ bottom 2.0) (/ item-side-sep 2.0)))))
        (setq primary-pt pt secondary-pt nil))
      (setq items
        (cons
          (list typ (nth gi glabels) double mantel primary-pt text-pt combi
                (nth gi gsplits) (nth gi gexisting-labels) triple
                twosided secondary-pt side2double side2triple side2combi)
          items))
      (setq gi (1+ gi)))
    (setq offset (+ offset group-boxlen stub)))

  ;; Bepaal beide korte uitstekende leaderstukjes relatief aan de halve bak:
  ;; 1) aan de inkomende zijde: altijd precies STUB lang terug vanaf p2;
  ;; 2) aan de tegenoverliggende zijde: het laatste uitgaande stukje.
  ;; Bij Direct kan p1 ver van de bak liggen. Daarom mag het midden van p1-p2
  ;; hier niet worden gebruikt. Deze berekening geeft Direct en Zwevend exact
  ;; dezelfde tekstpositie ten opzichte van de halve bak.
  (setq entry-stub-start (polar p2 (+ ang pi) stub))
  (setq lower-stub-a (kg:midpoint entry-stub-start p2))
  (setq lower-stub-b
    (kg:midpoint
      (kg:local->world group-start ang group-boxlen top)
      group-end))
  (setq lower-stub-mid
    (if (< (cadr p2) (cadr p1))
      ;; Bij boven-naar-beneden staat de code aan het verre uiteinde.
      ;; Gebruik voor de mantelbuistekst het tegenoverliggende leaderuiteinde.
      lower-stub-a
      ;; De oorspronkelijke keuze blijft volledig behouden voor
      ;; beneden-naar-boven en voor horizontale/overige richtingen.
      (if (< (cadr lower-stub-a) (cadr lower-stub-b))
        lower-stub-a
        lower-stub-b)))

  ;; Het midden van de totale bak wordt gebruikt om de binnenzijde te bepalen.
  (setq tray-center
    (kg:local->world p2 ang (/ (max 0.0 (- offset stub)) 2.0) (/ bottom 2.0)))
  (setq mantel-inward-ang (angle lower-stub-mid tray-center))

  ;; Kies van de twee normalen op de tekstrichting degene die van de bak af
  ;; wijst. De onderste positie wordt nu in twee onafhankelijke componenten
  ;; opgebouwd. Zo blijft de goede rechtspositie van versie Q behouden zonder
  ;; dat de tekst door die horizontale verschuiving opnieuw omhoog gaat.
  (setq mantel-normal-a (+ mantel-text-ang (/ pi 2.0)))
  (setq mantel-normal-b (- mantel-text-ang (/ pi 2.0)))
  (setq mantel-outward-ang
    (if (<
          (kg:dot2
            (list (cos mantel-normal-a) (sin mantel-normal-a))
            (kg:vector2 lower-stub-mid tray-center))
          (kg:dot2
            (list (cos mantel-normal-b) (sin mantel-normal-b))
            (kg:vector2 lower-stub-mid tray-center)))
      mantel-normal-a
      mantel-normal-b))

  ;; Q-punt levert de gewenste afstand naar rechts; het eerdere lage punt
  ;; levert de gewenste afstand omlaag. De projecties worden gecombineerd in
  ;; de lokale tekst-as en de loodrechte buitenrichting.
  (setq mantel-axis (list (cos mantel-text-ang) (sin mantel-text-ang)))
  (setq mantel-q-pt
    (polar (polar lower-stub-mid mantel-inward-ang 0.75) mantel-outward-ang 0.41))
  (setq mantel-low-pt
    (polar (polar lower-stub-mid mantel-inward-ang 0.45) mantel-outward-ang 0.41))
  (setq mantel-q-vec (kg:vector2 lower-stub-mid mantel-q-pt))
  (setq mantel-low-vec (kg:vector2 lower-stub-mid mantel-low-pt))
  (setq mantel-parallel (kg:dot2 mantel-q-vec mantel-axis))
  (setq mantel-normal-offset
    (kg:dot2 mantel-low-vec
      (list (cos mantel-outward-ang) (sin mantel-outward-ang))))
  (setq mantel-text-pt
    (list
      (+ (car lower-stub-mid)
         (* mantel-parallel (car mantel-axis))
         (* mantel-normal-offset (cos mantel-outward-ang)))
      (+ (cadr lower-stub-mid)
         (* mantel-parallel (cadr mantel-axis))
         (* mantel-normal-offset (sin mantel-outward-ang)))
      0.0))

  (setq mantel-text-just
    (if (>=
          (kg:dot2
            (list (cos mantel-text-ang) (sin mantel-text-ang))
            (kg:vector2 lower-stub-mid tray-center))
          0.0)
      0
      2))

  (setq sorted-items (vl-sort items 'kg:editor-item<))
  (setq floating-text-ang text-ang
        floating-text-just 1)
  (if *kg:floating-code*
    (progn
      (setq floating-text-pt
        (kg:p+
          (kg:local->world p2 ang (+ (- offset 0.90) (if (< (cadr p2) (cadr p1)) 0.35 0.45)) -0.45)
          text-ang
          (if (< (cadr p2) (cadr p1)) -0.80 0.80)))
      ;; Bij ingeschakelde CODESWITCH worden uitsluitend nieuwe zwevende
      ;; kabelgeulen omgewisseld. Bestaande blocks worden niet aangepast.
      (if *kg:future-code-switch*
        (progn
          (setq swap-pt floating-text-pt
                swap-ang floating-text-ang
                swap-just floating-text-just)
          (setq floating-text-pt mantel-text-pt
                floating-text-ang mantel-text-ang
                floating-text-just mantel-text-just)
          (setq mantel-text-pt swap-pt
                mantel-text-ang swap-ang
                mantel-text-just swap-just)
          ;; De mantelbuistekst is doorgaans veel langer dan de zwevende code.
          ;; Na CODESWITCH blijft hij exact op dezelfde hoogte als de normale
          ;; zwevende code. Alleen de positie langs de tekstlijn verandert:
          ;; de tekst begint net voorbij het korte blauwe streepje en groeit
          ;; naar de binnenzijde van de halve bak. Daardoor gaat de lijn niet
          ;; meer door de tekst en wordt de tekst niet verder omhoog geschoven.
          (setq switch-u
            (list (cos mantel-text-ang) (sin mantel-text-ang)))
          (setq switch-diff (kg:vector2 p2 mantel-text-pt))
          (setq switch-parallel (kg:dot2 switch-diff switch-u))
          (setq switch-normal
            (list
              (- (car switch-diff) (* switch-parallel (car switch-u)))
              (- (cadr switch-diff) (* switch-parallel (cadr switch-u)))))
          (setq switch-inward
            (if (>= (kg:dot2 switch-u (kg:vector2 p2 tray-center)) 0.0)
              1.0
              -1.0))
          (setq mantel-text-pt
            (list
              (+ (car p2)
                 (car switch-normal)
                 (* switch-inward 0.22 (car switch-u)))
              (+ (cadr p2)
                 (cadr switch-normal)
                 (* switch-inward 0.22 (cadr switch-u)))
              0.0))
          (setq mantel-text-just
            (if (> switch-inward 0.0) 0 2))))))

  (setq mask-index 1)
  (foreach item sorted-items
    (setq attr-pts
      (cond
        ((kg:half-rooien-p (car item))
         (kg:half-rooien-text-points (nth 4 item) ang (nth 5 item) text-ang))
        ((kg:double-p (nth 10 item))
         (kg:two-sided-text-points (nth 4 item) (nth 11 item) (nth 5 item) text-ang))
        (T
         (list (nth 5 item) nil text-just))))
    (setq primary-label (kg:prefixed-label (car item) (cadr item)))
    (kg:draw-text-wipeout (car attr-pts) text-ang (nth 2 attr-pts) primary-label 0.55 (kg:index-tag "KABEL" mask-index))
    (cond
      ((kg:half-rooien-p (car item))
       (setq existing-label (kg:prefixed-label "Bestaand" (nth 8 item)))
       (kg:draw-text-wipeout (cadr attr-pts) text-ang text-just existing-label
         0.55 (kg:index-tag "BESTAAND" mask-index)))
      ((kg:double-p (nth 10 item))
       (setq secondary-label (kg:prefixed-label (car item) (nth 8 item)))
       (kg:draw-text-wipeout (cadr attr-pts) text-ang text-just secondary-label
         0.55 (kg:index-tag "ZIJDE2" mask-index))))
    (setq mask-index (1+ mask-index)))
  (if floating-text-pt
    (kg:draw-text-wipeout floating-text-pt floating-text-ang floating-text-just *kg:floating-code* 0.55 "ZWEVEND_CODE"))

  ;; De wipeout van MANTEL_SPEC wordt altijd vóór de blauwe bak- en leaderlijnen
  ;; aangemaakt. Daardoor blijft de wipeout onder de blauwe geometrie, ook wanneer
  ;; CODESWITCH de mantelbuistekst naar de bovenste positie verplaatst. De tekst
  ;; zelf wordt daarna als attribuut gemaakt en blijft dus boven de lijnen zichtbaar.
  (kg:draw-text-wipeout mantel-text-pt mantel-text-ang mantel-text-just
    mantel-spec 0.55 "MANTEL_SPEC")

  (foreach pts tray-polys
    (kg:make-lwpoly pts "E_LV_CROSS_SECTION" nil))

  (if *kg:floating-code*
    (kg:make-attribute floating-text-pt 0.55 floating-text-ang "ZWEVEND_CODE" "Zwevende KabelGeul code:" *kg:floating-code* "KabelGeul zwevend code" floating-text-just 0))

  ;; Altijd als attribuut aanmaken, zodat de tekst later met KGEDIT aangepast kan worden.
  (kg:make-attribute mantel-text-pt 0.55 mantel-text-ang "MANTEL_SPEC"
    "Mantelbuis specificaties:" mantel-spec "Mantelbuis" mantel-text-just 0)

  (vl-catch-all-apply 'kg:draw-mantelbuis-items (list items ang))
  (foreach item (reverse items)
    (kg:draw-cable-icon (nth 4 item) ang (car item) (nth 2 item) (nth 9 item) nil (nth 6 item))
    (if (and (kg:double-p (nth 10 item)) (kg:point-p (nth 11 item)))
      ;; Zijde 2 gebruikt zijn eigen gekozen vorm.
      (kg:draw-cable-icon (nth 11 item) ang (car item) (nth 12 item) (nth 13 item) nil (nth 14 item))))

  (setq attr-index 1)
  (foreach item sorted-items
    (setq attr-pts
      (cond
        ((kg:half-rooien-p (car item))
         (kg:half-rooien-text-points (nth 4 item) ang (nth 5 item) text-ang))
        ((kg:double-p (nth 10 item))
         (kg:two-sided-text-points (nth 4 item) (nth 11 item) (nth 5 item) text-ang))
        (T
         (list (nth 5 item) nil text-just))))
    (kg:draw-cable-attributes (car attr-pts) text-ang (nth 2 attr-pts) (car item) (cadr item) attr-index)
    (cond
      ((kg:half-rooien-p (car item))
       (kg:draw-bestaand-attribute (cadr attr-pts) text-ang text-just
         (nth 8 item) attr-index))
      ((kg:double-p (nth 10 item))
       (kg:draw-side2-attribute (cadr attr-pts) text-ang text-just
         (car item) (nth 8 item) attr-index)))
    (kg:draw-split-attribute (nth 5 item) text-ang text-just (nth 7 item) attr-index)
    (kg:draw-copy-option-attributes (nth 5 item) text-ang text-just
      (nth 2 item) (nth 9 item) (nth 3 item) (nth 6 item) (nth 10 item)
      (nth 12 item) (nth 13 item) (nth 14 item) attr-index)
    (setq attr-index (1+ attr-index)))

  (setq ss (ssadd))
  (foreach next (reverse *kg:new-ents*)
    (if (and next (entget next)) (ssadd next ss)))
  (setq *kg:new-ents* nil)

  (if (> (sslength ss) 0)
    (progn
      (setq blkname (kg:unique-block-name))
      (vl-cmdf "_.BLOCK" blkname "_non" p2 ss "")
      (setq insert-before (entlast))
      (setq oldattreq (getvar "ATTREQ"))
      (setvar "ATTREQ" 0)
      (setq insert-result (vl-catch-all-apply 'vl-cmdf (list "_.INSERT" blkname "_non" p2 1.0 1.0 0.0)))
      (setvar "ATTREQ" oldattreq)
      (setq insert-ent (kg:find-new-insert insert-before))
      (if insert-ent (kg:bring-to-front insert-ent))
      (if insert-ent insert-ent insert-result))))

(defun kg:draw-trench (base ang types labels / p1 p2)
  (setq p1 (kg:local->world base ang -7.35 1.5))
  (setq p2 (kg:local->world base ang 0.0 1.5))
  (kg:draw-trench-at-end p1 p2 types labels nil nil nil nil nil nil "" nil nil nil nil))

(defun kg:draw-trench-with-place-mode (p1 p2 data / mode code ang floating-leader-len place fp1 fp2 oldcode placed)
  (initget "Direct Zwevend")
  (setq mode (getkword "\nPlaatsing kabelgeul [Direct/Zwevend] <Direct>: "))
  (if (null mode) (setq mode "Direct"))
  (cond
    ((= mode "Zwevend")
     (setq code (kg:next-floating-code))
     (setq ang (angle p1 p2))
     (setq floating-leader-len 1.05)
     (setq place (getpoint "\nKlik plaats voor zwevende kabelgeul: "))
     (if place
       (progn
         (kg:draw-floating-reference p1 p2 code)
         (setq fp2 place)
         (setq fp1 (polar fp2 (+ ang pi) floating-leader-len))
         (setq oldcode *kg:floating-code*)
         (setq *kg:floating-code* code)
         (kg:draw-trench-at-end fp1 fp2 (cadr data) (caddr data) (cadddr data) (nth 4 data) (nth 5 data) (nth 6 data) (nth 7 data) (nth 8 data) (nth 9 data) (nth 10 data) (nth 11 data) (nth 12 data) (nth 13 data))
         (setq *kg:floating-code* oldcode)
         (setq placed T))
       (princ "\nZwevende KabelGeul geannuleerd.")))
    (T
     (kg:draw-trench-at-end p1 p2 (cadr data) (caddr data) (cadddr data) (nth 4 data) (nth 5 data) (nth 6 data) (nth 7 data) (nth 8 data) (nth 9 data) (nth 10 data) (nth 11 data) (nth 12 data) (nth 13 data))
     (setq placed T)))
  placed)

(defun C:KABELGEUL (/ olderr oldcmd sel ent insert-ent p1 rawp2 p2 cable-ang geul-ang data crossed fence-types start-type placed handled)
  (kg:hide-wipeout-frames)
  (setq olderr *error*)
  (defun *error* (msg)
    (if oldcmd (setvar "CMDECHO" oldcmd))
    (setq *error* olderr)
    (if (and msg (not (wcmatch (strcase msg) "*CANCEL*,*QUIT*")))
      (princ
        (strcat
          "\nFout"
          (if *kg:copy-stage* (strcat " bij " *kg:copy-stage*) "")
          ": " (kg:friendly-error-message msg))))
    (setq *kg:copy-stage* nil)
    (princ))
  (setq oldcmd (getvar "CMDECHO"))
  (setvar "CMDECHO" 0)
  (kg:ensure-layers)
  (setq sel (entsel "\nSelecteer eerste polyline / begin lange lijn of bestaande KabelGeul: "))
  (if sel
    (progn
      (setq ent (car sel))
      (setq insert-ent (kg:selected-insert ent))
      (if insert-ent
        (progn
          (setq handled T)
          (setq placed (kg:create-copy-from-insert insert-ent)))
        (progn
          (setq p1 (cadr sel))
          (setq cable-ang (kg:curve-angle-at-pick ent p1))
          (setq p1 (kg:curve-point-at-pick ent p1))))))
  (if (not handled)
    (progn
      (if (and p1 cable-ang)
        (setq rawp2 (getpoint p1 "\nAfstandspunt richting halve vierkant: ")))
      (if (and p1 rawp2 cable-ang)
        (progn
          (setq geul-ang (+ cable-ang (/ pi 2.0)))
          (setq p2 (kg:endpoint-from-perp p1 rawp2 geul-ang))
          (setq start-type (kg:layer->type (cdr (assoc 8 (entget ent)))))
          (setq crossed (kg:crossed-cable-types p1 p2 ent))
          (setq fence-types (kg:fence-cable-types p1 p2 ent))
          (if (> (length fence-types) (length crossed))
            (setq crossed fence-types))
          (if start-type
            (setq crossed (cons start-type crossed)))
          (if crossed
            (progn
              (setq *kg:last-count* (max 1 (length crossed)))
              (setq *kg:last-types* crossed)
              (setq *kg:last-labels* (kg:blank-labels *kg:last-count*))
              (setq *kg:last-existing-labels* (kg:blank-existing-labels *kg:last-count*))
              (setq *kg:last-doubles* (kg:blank-doubles *kg:last-count*))
              (setq *kg:last-triples* (kg:blank-triples *kg:last-count*))
              (setq *kg:last-twosideds* (kg:blank-twosideds *kg:last-count*))
              (setq *kg:last-side2-doubles* (kg:blank-doubles *kg:last-count*))
              (setq *kg:last-side2-triples* (kg:blank-triples *kg:last-count*))
              (setq *kg:last-side2-combis* (kg:blank-combis *kg:last-count*))
              (setq *kg:last-mantels* (kg:blank-mantels *kg:last-count*))
              (setq *kg:last-splits* (kg:blank-splits *kg:last-count*))
              (setq *kg:last-combis* (kg:blank-combis *kg:last-count*)))
            (progn
              (setq *kg:last-count* (max 1 (kg:count-crossed-polylines p1 p2)))
              (setq *kg:last-existing-labels* (kg:blank-existing-labels *kg:last-count*))
              (setq *kg:last-doubles* (kg:blank-doubles *kg:last-count*))
              (setq *kg:last-triples* (kg:blank-triples *kg:last-count*))
              (setq *kg:last-twosideds* (kg:blank-twosideds *kg:last-count*))
              (setq *kg:last-side2-doubles* (kg:blank-doubles *kg:last-count*))
              (setq *kg:last-side2-triples* (kg:blank-triples *kg:last-count*))
              (setq *kg:last-side2-combis* (kg:blank-combis *kg:last-count*))
              (setq *kg:last-mantels* (kg:blank-mantels *kg:last-count*))
              (setq *kg:last-splits* (kg:blank-splits *kg:last-count*))
              (setq *kg:last-combis* (kg:blank-combis *kg:last-count*))))
          (setq *kg:last-mantel-spec* "")
          (setq data (kg:show-dialog))
          (if data
            (setq placed (kg:draw-trench-with-place-mode p1 p2 data))
            (princ "\nKabelGeul geannuleerd.")))
        (princ "\nKabelGeul geannuleerd."))))
  (setvar "CMDECHO" oldcmd)
  (setq *error* olderr)
  (if placed
    (princ "\nKabelGeul geplaatst als uniek block."))
  (princ))

(defun C:KG () (C:KABELGEUL))

(defun C:KGEDIT (/ sel insert-ent)
  (kg:hide-wipeout-frames)
  (setq sel (entsel "\nSelecteer KabelGeul block of kabel/leiding tekst: "))
  (if sel
    (progn
      (setq insert-ent (kg:selected-insert (car sel)))
      (if insert-ent
        (progn
          (kg:remove-direction-attributes insert-ent)
          (if (kg:open-enhanced-attribute-editor insert-ent)
            (kg:update-text-wipeouts-for-insert insert-ent)))
        (princ "\nSelectie is geen block met attributen.")))
    (princ "\nGeen block geselecteerd."))
  (princ))


(defun C:CODESWITCH (/)
  ;; Geen selectie: wissel de standaardpositie voor alle KabelGeulen die
  ;; vanaf nu met KABELGEUL/OMNI in de modus Zwevend worden gemaakt.
  (setq *kg:future-code-switch* (not *kg:future-code-switch*))
  (setenv "KabelGeulOmniCodeSwitch" (if *kg:future-code-switch* "1" "0"))
  (princ
    (if *kg:future-code-switch*
      "\nCODESWITCH staat AAN: bij toekomstige zwevende KabelGeulen komen code en mantelbuistekst omgewisseld te staan."
      "\nCODESWITCH staat UIT: toekomstige zwevende KabelGeulen gebruiken de standaardposities."))
  (princ))

(defun C:KGCODESWITCH () (C:CODESWITCH))

(defun kg:copy-insert-to (insert-ent base target / obj copy-ent ss)
  (setq obj (vlax-ename->vla-object insert-ent))
  (setq copy-ent (vla-Copy obj))
  (vla-Move copy-ent (vlax-3d-point (kg:pt3 base)) (vlax-3d-point (kg:pt3 target)))
  (setq copy-ent (vlax-vla-object->ename copy-ent))
  (if copy-ent
    (progn
      (kg:bring-to-front copy-ent)
      (setq ss (ssadd))
      (ssadd copy-ent ss)
      (sssetfirst nil ss)))
  copy-ent)

(defun kg:clone-block-definition (oldname newname / doc blocks oldblk newblk count objs idx obj result)
  (setq doc (vla-get-ActiveDocument (vlax-get-acad-object)))
  (setq blocks (vla-get-Blocks doc))
  (setq oldblk (vl-catch-all-apply 'vla-item (list blocks oldname)))
  (if (vl-catch-all-error-p oldblk)
    nil
    (progn
      (setq newblk (vl-catch-all-apply 'vla-add (list blocks (vlax-3d-point '(0.0 0.0 0.0)) newname)))
      (if (vl-catch-all-error-p newblk)
        nil
        (progn
          (setq count (vla-get-Count oldblk))
          (if (> count 0)
            (progn
              (setq objs (vlax-make-safearray vlax-vbObject (cons 0 (1- count))))
              (setq idx 0)
              (vlax-for obj oldblk
                (vlax-safearray-put-element objs idx obj)
                (setq idx (1+ idx)))
              (setq result
                (vl-catch-all-apply
                  'vla-CopyObjects
                  (list doc (vlax-make-variant objs) newblk)))))
          (if (or (= count 0) (not (vl-catch-all-error-p result)))
            newname
            nil))))))

(defun kg:set-insert-block-name (insert-ent blkname / data)
  (setq data (entget insert-ent))
  (if (assoc 2 data)
    (progn
      (entmod (subst (cons 2 blkname) (assoc 2 data) data))
      (entupd insert-ent)
      blkname)
    nil))

(defun kg:make-copy-unique-block (insert-ent / data oldname newname)
  (setq data (entget insert-ent))
  (setq oldname (cdr (assoc 2 data)))
  (setq newname (kg:unique-block-name))
  (if (and oldname (kg:clone-block-definition oldname newname))
    (kg:set-insert-block-name insert-ent newname)
    nil))

(defun kg:block-circles (blkrec / next data circles)
  (setq circles '())
  (if blkrec
    (progn
      (setq next (entnext blkrec))
      (while next
        (setq data (entget next))
        (cond
          ((= (cdr (assoc 0 data)) "CIRCLE")
           (setq circles
             (cons
               (list
                 (cdr (assoc 8 data))
                 (cdr (assoc 10 data))
                 (if (assoc 40 data) (cdr (assoc 40 data)) 0.0)
                 (if (assoc 62 data) (abs (cdr (assoc 62 data))) nil))
               circles)))
          ((= (cdr (assoc 0 data)) "ENDBLK")
           (setq next nil)))
        (if next
          (setq next (entnext next))))))
  circles)

(defun kg:icon-circle->type (circle / color layer typ)
  (setq color (nth 3 circle))
  (setq layer (car circle))
  ;; De MS-types mogen dezelfde kleur hebben als bestaande kabels, maar
  ;; hebben eigen lagen. Gebruik daarom eerst de laag en pas daarna kleur.
  (setq typ (kg:layer->type layer))
  (if typ
    typ
    (kg:color->type color)))

(defun kg:block-mantelbuis-polys (blkrec / next data polys)
  (setq polys '())
  (if blkrec
    (progn
      (setq next (entnext blkrec))
      (while next
        (setq data (entget next))
        (cond
          ((and (= (cdr (assoc 0 data)) "LWPOLYLINE")
                (= (strcase (if (cdr (assoc 8 data)) (cdr (assoc 8 data)) "")) "MANTELBUIS"))
           (setq polys (cons (kg:lwpolyline-points data) polys)))
          ((= (cdr (assoc 0 data)) "ENDBLK")
           (setq next nil)))
        (if next
          (setq next (entnext next))))))
  (reverse polys))

(defun kg:point-in-poly-p (pt poly / x y inside p1 p2 x1 y1 x2 y2 rest)
  (setq poly (kg:valid-points poly))
  (setq inside nil)
  (if (and (kg:point-p pt) (> (length poly) 2))
    (progn
      (setq x (car pt)
            y (cadr pt))
      (setq p1 (car (last poly)))
      (setq rest poly)
      (while rest
        (setq p2 (car rest))
        (setq x1 (car p1) y1 (cadr p1) x2 (car p2) y2 (cadr p2))
        (if (and (not (eq (> y1 y) (> y2 y)))
                 (< x (+ x1 (/ (* (- x2 x1) (- y y1)) (- y2 y1)))))
          (setq inside (not inside)))
        (setq p1 p2)
        (setq rest (cdr rest)))))
  inside)

(defun kg:mantel-poly-key-for-point (pt polys / idx found)
  (setq idx 1)
  (while (and (kg:point-p pt) (listp polys) polys (null found))
    (if (kg:point-in-poly-p pt (car polys))
      (setq found (strcat "M" (itoa idx))))
    (setq idx (1+ idx))
    (setq polys (cdr polys)))
  found)

(defun kg:copy-icon-data-for-point (pt ang circles polys / icon-pt circle layer center dist typ best-type best-dist count mantel combi triple has-rooien has-bestaand)
  (if (kg:point-p pt)
    (progn
      (setq icon-pt (polar pt (+ ang (/ pi 2.0)) 1.70))
      (setq count 0 mantel (kg:mantel-poly-key-for-point icon-pt polys) best-type nil best-dist nil combi nil triple nil has-rooien nil has-bestaand nil)
      (foreach circle circles
        (setq layer (if (listp circle) (car circle) nil))
        (setq center (if (listp circle) (cadr circle) nil))
        (if (and (listp circle) (kg:point-p center))
          (progn
            (setq dist (distance (kg:pt2 center) (kg:pt2 icon-pt)))
            (cond
              ((= (strcase (if layer layer "")) "MANTELBUIS")
               (if (and (< dist 0.80) (null mantel)) (setq mantel "X")))
              ((setq typ (kg:icon-circle->type circle))
               (if (< dist 0.65)
                 (progn
                   (setq count (1+ count))
                   (if (= typ "Rooien") (setq has-rooien T))
                   (if (= typ "Bestaand") (setq has-bestaand T))
                   (if (or (null best-dist) (< dist best-dist))
                     (setq best-type typ best-dist dist
                       combi (and (numberp (nth 2 circle)) (> (nth 2 circle) 0.27)))))))))))
      (if (and has-rooien has-bestaand) (setq best-type "Half rooien"))
      (setq triple (>= count 3))
      (list best-type (= count 2) mantel combi triple))
    (list nil nil nil nil nil)))

(defun kg:safe-copy-icon-data-for-point (pt ang circles polys / result)
  (setq result (vl-catch-all-apply 'kg:copy-icon-data-for-point (list pt ang circles polys)))
  (if (vl-catch-all-error-p result)
    (list nil nil nil nil nil)
    result))

(defun kg:copy-attr-items (insert-ent / next data tag idx txt layer typ pt items second-items second-item)
  (setq items '() second-items '())
  (if insert-ent
    (progn
      (setq next (entnext insert-ent))
      (while next
        (setq data (entget next))
        (cond
          ((= (cdr (assoc 0 data)) "ATTRIB")
           (setq tag (cdr (assoc 2 data)))
           (cond
             ((kg:kabel-tag-p tag)
              (setq idx (kg:tag-index tag))
              (setq txt (if (assoc 1 data) (cdr (assoc 1 data)) ""))
              (setq layer (cdr (assoc 8 data)))
              (setq typ (kg:layer->type layer))
              (if (null typ) (setq typ (kg:normalize-type txt)))
              (if (null typ) (setq typ "K01"))
              (setq pt (kg:attribute-point data))
              (if (kg:point-p pt)
                (setq pt (kg:world->block insert-ent pt))
                (setq pt nil))
              (setq items (cons (list idx typ txt pt) items)))
             ((kg:bestaand-tag-p tag)
              (setq idx (kg:bestaand-tag-index tag))
              (setq txt (if (assoc 1 data) (cdr (assoc 1 data)) ""))
              (setq pt (kg:attribute-point data))
              (if (kg:point-p pt)
                (setq pt (kg:world->block insert-ent pt))
                (setq pt nil))
              (setq second-items (cons (list idx txt pt "BESTAAND") second-items)))
             ((kg:zijde2-tag-p tag)
              (setq idx (kg:zijde2-tag-index tag))
              (setq txt (if (assoc 1 data) (cdr (assoc 1 data)) ""))
              (setq pt (kg:attribute-point data))
              (if (kg:point-p pt)
                (setq pt (kg:world->block insert-ent pt))
                (setq pt nil))
              (setq second-items (cons (list idx txt pt "ZIJDE2") second-items)))))
          ((= (cdr (assoc 0 data)) "SEQEND")
           (setq next nil)))
        (if next (setq next (entnext next))))))
  (setq items (vl-sort items '(lambda (a b) (< (car a) (car b)))))
  (mapcar
    '(lambda (item)
       (setq second-item (assoc (car item) second-items))
       (append item
         (list
           (if second-item (cadr second-item) "")
           (if second-item (caddr second-item) nil)
           (if second-item (nth 3 second-item) nil))))
    items))

(defun kg:split-tag-p (tag)
  (kg:starts-with (strcase (if tag tag "")) "VOLG_"))

(defun kg:block-has-split-metadata-p (blkrec / next data found)
  (if blkrec
    (progn
      (setq next (entnext blkrec))
      (while (and next (null found))
        (setq data (entget next))
        (cond
          ((and (= (cdr (assoc 0 data)) "ATTDEF")
                (kg:split-tag-p (cdr (assoc 2 data))))
           (setq found T))
          ((= (cdr (assoc 0 data)) "ENDBLK")
           (setq next nil)))
        (if (and next (null found))
          (setq next (entnext next))))))
  found)

(defun kg:block-split-value (blkrec i / ent data)
  (setq ent (kg:block-attdef-ent blkrec (kg:index-tag "VOLG" i)))
  (if (null ent)
    (setq ent (kg:block-attdef-ent blkrec (strcat "VOLG_" (itoa i)))))
  (if ent
    (progn
      (setq data (entget ent))
      (kg:double-p (cdr (assoc 1 data))))
    nil))

(defun kg:block-option-metadata (blkrec prefix i / ent data)
  (setq ent (kg:block-attdef-ent blkrec (kg:index-tag prefix i)))
  (if (null ent)
    (setq ent (kg:block-attdef-ent blkrec (strcat prefix "_" (itoa i)))))
  (if ent
    (progn
      (setq data (entget ent))
      ;; De buitenste lijst onderscheidt 'metadata ontbreekt' van een opgeslagen lege/0-waarde.
      (list T (if (assoc 1 data) (cdr (assoc 1 data)) "")))
    nil))

(defun kg:block-group-starts (blkrec ang / next data pts start unit starts)
  (setq unit (list (cos ang) (sin ang)))
  (setq starts '())
  (if blkrec
    (progn
      (setq next (entnext blkrec))
      (while next
        (setq data (entget next))
        (cond
          ((and (= (cdr (assoc 0 data)) "LWPOLYLINE")
                (= (strcase (if (cdr (assoc 8 data)) (cdr (assoc 8 data)) "")) "E_LV_CROSS_SECTION"))
           (setq pts (kg:lwpolyline-points data))
           (if pts
             (progn
               ;; De eerste bak bevat ook de leader en heeft daarom één punt extra.
               (setq start (if (> (length pts) 5) (cadr pts) (car pts)))
               (if (kg:point-p start)
                 (setq starts (cons (kg:dot2 start unit) starts))))))
          ((= (cdr (assoc 0 data)) "ENDBLK")
           (setq next nil)))
        (if next
          (setq next (entnext next))))))
  (vl-sort starts '<))

(defun kg:point-group-id (pt starts ang / unit pos idx group start)
  (if (kg:point-p pt)
    (progn
      (setq unit (list (cos ang) (sin ang)))
      (setq pos (kg:dot2 pt unit))
      (setq idx 0 group 0)
      (foreach start starts
        (if (>= pos (- start 1e-6))
          (setq group idx))
        (setq idx (1+ idx)))
      group)
    0))

(defun kg:groupids->splits (groupids / first prev id splits)
  (setq first T prev nil splits '())
  (foreach id groupids
    (setq splits
      (append splits (list (if first nil (/= id prev)))))
    (setq first nil prev id))
  splits)

(defun kg:reverse-splits-for-copy (splits)
  (if splits
    (cons nil (reverse (cdr splits)))
    nil))

(defun kg:insert-attribute-value (insert-ent tag / ent data)
  (setq ent (kg:attr-ent insert-ent tag))
  (if ent
    (progn
      (setq data (entget ent))
      (if (assoc 1 data) (cdr (assoc 1 data)) ""))
    ""))

(defun kg:copy-data-from-insert (insert-ent / data blkname blkrec ang circles polys items item icon typ types labels existing-labels doubles triples twosideds side2doubles side2triples side2combis mantels splits combis mantel-spec has-split-metadata has-half-attribute has-twosided-attribute twosided group-starts groupids out-splits icon-pt double-meta triple-meta mantel-meta combi-meta twosided-meta side2double-meta side2triple-meta side2combi-meta primary-double primary-triple primary-combi side2double side2triple side2combi)
  (setq *kg:copy-stage* "bronblok: blokverwijzing lezen")
  (setq data (entget insert-ent))
  (setq blkname (cdr (assoc 2 data)))
  (setq blkrec (if blkname (tblobjname "BLOCK" blkname) nil))
  (setq *kg:copy-stage* "bronblok: geulrichting bepalen")
  (setq ang (if blkrec (kg:block-cross-angle blkrec) 0.0))
  (setq *kg:copy-stage* "bronblok: kabelsymbolen lezen")
  (setq circles (if blkrec (kg:block-circles blkrec) nil))
  (setq *kg:copy-stage* "bronblok: mantelbuizen lezen")
  (setq polys (if blkrec (kg:block-mantelbuis-polys blkrec) nil))
  (setq *kg:copy-stage* "bronblok: tekstattributen lezen")
  (setq items (kg:copy-attr-items insert-ent))
  (setq mantel-spec (kg:insert-attribute-value insert-ent "MANTEL_SPEC"))
  (setq *kg:copy-stage* "bronblok: volgmetadata lezen")
  (setq has-split-metadata (kg:block-has-split-metadata-p blkrec))
  (setq *kg:copy-stage* "bronblok: geulgroepen bepalen")
  (setq group-starts (if has-split-metadata nil (kg:block-group-starts blkrec ang)))
  (setq types '() labels '() existing-labels '() doubles '() triples '()
        twosideds '() side2doubles '() side2triples '() side2combis '() mantels '() splits '() combis '() groupids '())
  (foreach item items
    (setq *kg:copy-stage* (strcat "bronblok: kabel " (itoa (car item)) " verwerken"))
    (setq typ (cadr item))
    (setq has-half-attribute (= (nth 6 item) "BESTAAND"))
    (setq has-twosided-attribute (= (nth 6 item) "ZIJDE2"))
    (setq icon-pt (cond ((kg:point-p (nth 5 item)) (nth 5 item)) ((kg:point-p (nth 3 item)) (nth 3 item)) (T nil)))
    (setq icon (kg:safe-copy-icon-data-for-point icon-pt ang circles polys))
    (setq double-meta (kg:block-option-metadata blkrec "DUBBEL" (car item)))
    (setq triple-meta (kg:block-option-metadata blkrec "DRIE" (car item)))
    (setq mantel-meta (kg:block-option-metadata blkrec "MANTEL" (car item)))
    (setq combi-meta (kg:block-option-metadata blkrec "COMBI" (car item)))
    (setq twosided-meta (kg:block-option-metadata blkrec "TWEEZIJDIG" (car item)))
    (setq side2double-meta (kg:block-option-metadata blkrec "DUBBEL2" (car item)))
    (setq side2triple-meta (kg:block-option-metadata blkrec "DRIE2" (car item)))
    (setq side2combi-meta (kg:block-option-metadata blkrec "COMBI2" (car item)))
    (setq twosided
      (if twosided-meta
        (kg:double-p (cadr twosided-meta))
        has-twosided-attribute))
    (if has-half-attribute (setq twosided nil))
    (if (car icon) (setq typ (car icon)))
    (if has-half-attribute (setq typ "Half rooien"))
    (setq types (cons typ types))
    (setq labels (cons (kg:strip-known-prefix (caddr item)) labels))
    (setq existing-labels (cons (kg:strip-known-prefix (nth 4 item)) existing-labels))
    (setq primary-triple
      (if has-half-attribute nil
        (if triple-meta (kg:double-p (cadr triple-meta)) (nth 4 icon))))
    (setq primary-combi
      (if combi-meta (kg:double-p (cadr combi-meta)) (nth 3 icon)))
    (setq primary-double
      (if has-half-attribute T
        (if (or primary-triple primary-combi) nil
          (if double-meta (kg:double-p (cadr double-meta)) (cadr icon)))))
    (setq side2triple
      (if twosided
        (if side2triple-meta (kg:double-p (cadr side2triple-meta)) primary-triple)
        nil))
    (setq side2combi
      (if twosided
        (if side2combi-meta (kg:double-p (cadr side2combi-meta)) primary-combi)
        nil))
    (setq side2double
      (if twosided
        (if (or side2triple side2combi) nil
          (if side2double-meta (kg:double-p (cadr side2double-meta)) primary-double))
        nil))
    (setq triples (cons primary-triple triples))
    (setq doubles (cons primary-double doubles))
    (setq twosideds (cons twosided twosideds))
    (setq side2doubles (cons side2double side2doubles))
    (setq side2triples (cons side2triple side2triples))
    (setq side2combis (cons side2combi side2combis))
    (setq mantels (cons (if mantel-meta (kg:mantel-value (cadr mantel-meta)) (caddr icon)) mantels))
    (if has-split-metadata
      (setq splits (cons (kg:block-split-value blkrec (car item)) splits))
      (setq groupids (cons (kg:point-group-id (nth 3 item) group-starts ang) groupids)))
    (setq combis (cons primary-combi combis)))
  (setq types (reverse types))
  (setq labels (reverse labels))
  (setq existing-labels (reverse existing-labels))
  (setq doubles (reverse doubles))
  (setq triples (reverse triples))
  (setq twosideds (reverse twosideds))
  (setq side2doubles (reverse side2doubles))
  (setq side2triples (reverse side2triples))
  (setq side2combis (reverse side2combis))
  (setq mantels (reverse mantels))
  (setq splits (reverse splits))
  (setq combis (reverse combis))
  (setq groupids (reverse groupids))
  (setq out-splits (if has-split-metadata (reverse splits) (kg:groupids->splits (reverse groupids))))
  (if (> (length types) 0)
    (progn
      (setq *kg:copy-stage* "bronblok uitgelezen")
      (list (length types) (reverse types) (reverse labels) (reverse doubles)
            (reverse mantels) out-splits (reverse combis)
            (reverse existing-labels) (reverse triples) mantel-spec
            (reverse twosideds) (reverse side2doubles)
            (reverse side2triples) (reverse side2combis)))
    nil))

(defun kg:seed-dialog-data (data)
  (setq *kg:last-count* (car data))
  (setq *kg:last-types* (cadr data))
  (setq *kg:last-labels* (caddr data))
  (setq *kg:last-doubles* (cadddr data))
  (setq *kg:last-mantels* (nth 4 data))
  (setq *kg:last-splits* (nth 5 data))
  (setq *kg:last-combis* (nth 6 data))
  (setq *kg:last-existing-labels* (nth 7 data))
  (setq *kg:last-triples* (if (nth 8 data) (nth 8 data) (kg:blank-triples (car data))))
  (setq *kg:last-mantel-spec* (if (nth 9 data) (nth 9 data) ""))
  (setq *kg:last-twosideds* (if (nth 10 data) (nth 10 data) (kg:blank-twosideds (car data))))
  (setq *kg:last-side2-doubles*
    (if (nth 11 data) (nth 11 data) *kg:last-doubles*))
  (setq *kg:last-side2-triples*
    (if (nth 12 data) (nth 12 data) *kg:last-triples*))
  (setq *kg:last-side2-combis*
    (if (nth 13 data) (nth 13 data) *kg:last-combis*))
  data)

(defun kg:leader-poly-ent (blkrec / next data found)
  (setq next (entnext blkrec))
  (while (and next (null found))
    (setq data (entget next))
    (cond
      ((and (= (cdr (assoc 0 data)) "LWPOLYLINE")
            (= (cdr (assoc 8 data)) "E_LV_CROSS_SECTION"))
       (setq found next))
      ((= (cdr (assoc 0 data)) "ENDBLK")
       (setq next nil)))
    (if (and next (null found))
      (setq next (entnext next))))
  found)

(defun kg:set-first-lwpoly-point (ent pt / data out done pair)
  (setq data (entget ent))
  (setq out '() done nil)
  (foreach pair data
    (if (and (not done) (= (car pair) 10))
      (progn
        (setq out (cons (cons 10 (kg:pt2 pt)) out))
        (setq done T))
      (setq out (cons pair out))))
  (if done
    (progn
      (entmod (reverse out))
      (entupd ent)
      T)
    nil))

(defun kg:set-copy-leader-length (insert-ent / data blkname blkrec poly pts p1 p2 p2-world picked picked-block newlen newp1 dir)
  (setq data (entget insert-ent))
  (setq blkname (cdr (assoc 2 data)))
  (setq blkrec (if blkname (tblobjname "BLOCK" blkname) nil))
  (setq poly (if blkrec (kg:leader-poly-ent blkrec) nil))
  (if (and poly (setq pts (kg:lwpolyline-points (entget poly))) (cadr pts))
    (progn
      (setq p1 (car pts))
      (setq p2 (cadr pts))
      (setq dir (angle p2 p1))
      (setq p2-world (kg:block->world insert-ent p2))
      (setq picked (getpoint p2-world "\nKlik nieuwe lengte voor de leader <Enter behoudt lengte>: "))
      (if picked
        (progn
          (setq picked-block (kg:world->block insert-ent picked))
          (setq newlen (distance (kg:pt2 p2) (kg:pt2 picked-block)))
          (setq newp1 (polar p2 dir newlen))
          (if (kg:set-first-lwpoly-point poly newp1)
            (progn
              (entupd insert-ent)
              T)
            nil))
        T))
    nil))

(defun kg:insert-leader-angle (insert-ent / data blkname blkrec poly pts)
  (setq data (entget insert-ent))
  (setq blkname (cdr (assoc 2 data)))
  (setq blkrec (if blkname (tblobjname "BLOCK" blkname) nil))
  (setq poly (if blkrec (kg:leader-poly-ent blkrec) nil))
  (if (and poly
           (setq pts (kg:lwpolyline-points (entget poly)))
           (cadr pts))
    (angle (kg:block->world insert-ent (car pts)) (kg:block->world insert-ent (cadr pts)))
    nil))

(defun kg:create-copy-from-insert (insert-ent / source-data dialog-data sel ent p1 rawp2 p2 cable-ang geul-ang placed)
  (setq *kg:copy-stage* "bronblok uitlezen")
  (setq source-data (kg:copy-data-from-insert insert-ent))
  (if source-data
    (progn
      (setq *kg:copy-stage* "nieuwe kabel selecteren")
      (setq sel (entsel "\nSelecteer kabel/lijn voor beginpunt van de geul leader: "))
      (if sel
        (progn
          (setq ent (car sel))
          (setq p1 (cadr sel))
          (setq *kg:copy-stage* "kabelrichting bepalen")
          (setq cable-ang (kg:curve-angle-at-pick ent p1))
          (setq p1 (kg:curve-point-at-pick ent p1))
          (if (and p1 cable-ang)
            (progn
              (setq *kg:copy-stage* "leaderpunt kiezen")
              (setq rawp2 (getpoint p1 "\nKlik lengte/eindpunt en richtingzijde van de geul leader: "))
              (if rawp2
                (progn
                  (setq *kg:copy-stage* "leadergeometrie berekenen")
                  (setq geul-ang (+ cable-ang (/ pi 2.0)))
                  (setq p2 (kg:endpoint-from-perp p1 rawp2 geul-ang))
                  (setq *kg:copy-stage* "kopieervenster vullen")
                  (kg:seed-dialog-data source-data)
                  (setq dialog-data (kg:show-dialog))
                  (if dialog-data
                    (progn
                      (setq *kg:copy-stage* "nieuwe kabelgeul tekenen")
                      (setq placed (kg:draw-trench-with-place-mode p1 p2 dialog-data)))
                    (princ "\nKabelGeul copy geannuleerd.")))
                (princ "\nKabelGeul copy geannuleerd.")))
            (princ "\nKabelGeul copy geannuleerd.")))
        (princ "\nKabelGeul copy geannuleerd.")))
    (princ "\nKan geen KabelGeul-gegevens uit dit block lezen."))
  (setq *kg:copy-stage* nil)
  placed)

(defun C:KABELGEULCOPY (/ olderr oldcmd sel insert-ent placed)
  (kg:hide-wipeout-frames)
  (setq olderr *error*)
  (defun *error* (msg)
    (if oldcmd (setvar "CMDECHO" oldcmd))
    (setq *error* olderr)
    (if (and msg (not (wcmatch (strcase msg) "*CANCEL*,*QUIT*")))
      (princ
        (strcat
          "\nFout"
          (if *kg:copy-stage* (strcat " bij " *kg:copy-stage*) "")
          ": " (kg:friendly-error-message msg))))
    (setq *kg:copy-stage* nil)
    (princ))
  (setq oldcmd (getvar "CMDECHO"))
  (setvar "CMDECHO" 0)
  (kg:ensure-layers)
  (setq sel (entsel "\nSelecteer KabelGeul block om gegevens over te nemen: "))
  (if sel
    (progn
      (setq insert-ent (kg:selected-insert (car sel)))
      (if insert-ent
        (progn
          (setq placed (kg:create-copy-from-insert insert-ent))
          (if placed
            (princ "\nKabelGeul opnieuw aangemaakt met overgenomen gegevens.")))
        (princ "\nSelectie is geen block met attributen.")))
    (princ "\nGeen block geselecteerd."))
  (setvar "CMDECHO" oldcmd)
  (setq *error* olderr)
  (princ))

(defun C:KGCOPY () (C:KABELGEULCOPY))

(defun C:KGTEST (/ base)
  (kg:hide-wipeout-frames)
  (kg:ensure-layers)
  (setq base (getpoint "\nPlaats test KabelGeul: "))
  (if base
    (progn
      (kg:draw-trench base 0.0 '("K02" "Rooien" "K04" "Bestaand")
                      '("Ri 2 4x150Al" "(Rooien) 95Al" "Ri 4 4x150Al" "Bestaande kabel"))
      (princ "\nTest KabelGeul geplaatst als block.")))
  (princ))

;; Unieke Omni-commando's voorkomen verwarring met oudere KabelGeul-versies.
(defun C:KABELGEULOMNI () (C:KABELGEUL))
(defun C:KGOMNI () (C:KABELGEUL))
(defun C:KGOMNI06B () (C:KABELGEUL))
(defun C:KGOMNI06C () (C:KABELGEUL))
(defun C:KGOMNI06D () (C:KABELGEUL))
(defun C:KGOMNI06E () (C:KABELGEUL))
(defun C:KGOMNI06F () (C:KABELGEUL))
(defun C:KGOMNI06G () (C:KABELGEUL))
(defun C:KGOMNI06H () (C:KABELGEUL))
(defun C:KGOMNI06I () (C:KABELGEUL))
(defun C:KGOMNI06J () (C:KABELGEUL))
(defun C:KGOMNI06L () (C:KABELGEUL))
(defun C:KGOMNI06M () (C:KABELGEUL))
(defun C:KGOMNI06N () (C:KABELGEUL))
(defun C:KGOMNI06O () (C:KABELGEUL))
(defun C:KGOMNI06P () (C:KABELGEUL))
(defun C:KGOMNI06Q () (C:KABELGEUL))
(defun C:KGOMNI06R () (C:KABELGEUL))
(defun C:KGOMNI06U () (C:KABELGEUL))
(defun C:KGOMNI06V () (C:KABELGEUL))
(defun C:KGOMNI06W () (C:KABELGEUL))
(defun C:KGOMNI06X () (C:KABELGEUL))
(defun C:KGOMNI06Y () (C:KABELGEUL))
(defun C:KGOMNI06Z () (C:KABELGEUL))
(defun C:KGOMNI06AA () (C:KABELGEUL))
(defun C:KGOMNI06AC () (C:KABELGEUL))
(defun C:KGOMNI06AD () (C:KABELGEUL))
(defun C:KGOMNINIEUW () (C:KABELGEUL))
(defun C:OMNI () (C:KABELGEUL))
(defun C:KGOMNIEDIT () (C:KGEDIT))
(defun C:KGOMNICOPY () (C:KABELGEULCOPY))

;; Oude MS-commando's blijven als compatibele aliassen beschikbaar.
(defun C:KABELGEULMS () (C:KABELGEUL))
(defun C:KGMS () (C:KABELGEUL))

(defun C:KGVER (/ locatie)
  (setq locatie
    (if *kg:lsp-dir*
      *kg:lsp-dir*
      "onbekende map"))
  (alert
    (strcat
      "Actieve versie: " *kg:version*
       "\nGeladen uit: " locatie
       "\n\nGebruik KGOMNI06AD of KGOMNINIEUW voor de unieke laadtest."
       "\nCODESWITCH voor toekomstige plaatsingen: "
       (if *kg:future-code-switch* "AAN (omgewisseld)" "UIT (standaard)")))
  (princ))

(kg:hide-wipeout-frames)
(kg:install-mask-reactor)
(princ (strcat "\nKabelGeul_Omni geladen: " *kg:version* ". Gebruik KGOMNI06AD, KGOMNINIEUW, KGOMNI, OMNI, KABELGEULOMNI, CODESWITCH, KGOMNIEDIT of KGOMNICOPY."))
(princ)



;;; ============================================================
;;; Ingevoegd uit: LICHTMAST.LSP
;;; ============================================================

(defun c:LICHTMAST (/ *error* write-dcl get-xref-list get-xref-blocknames inside-p ensure-lichtmast-block
                                 dcl-file dcl-id xrefs-list selected-xref-idx xrefName
                                 names-list selected-indices selected-names
                                 polyEnt polyPts targetEnt targetBasePt
                                 xrefEntData insPtX insPtY rot scaleX 
                                 ptLocalX ptLocalY worldX worldY worldPt
                                 count old_osmode result ssXref ent entData)

  (defun *error* (msg)
    (if dcl-id (unload_dialog dcl-id))
    (if (and dcl-file (findfile dcl-file)) (vl-file-delete dcl-file))
    (if old_osmode (setvar "OSMODE" old_osmode))
    (if targetEnt (entdel targetEnt)) ; Ruim tijdelijk block op bij een fout
    (if (and msg (not (wcmatch (strcase msg) "*BREAK*,*CANCEL*,*EXIT*")))
      (princ (strcat "\nFout: " msg))
    )
    (princ)
  )

  ;; --- Functie om het Lichtmast block te maken indien niet aanwezig ---
  (defun ensure-lichtmast-block ( / blkName diameter radius width linFile)
    (setq blkName "Lichtmast_Def" diameter 1.1977 radius (/ diameter 2.0) width 0.2)
    
    ;; Controleer en laad linetype DASHED indien nodig (veilig laden)
    (if (not (tblsearch "LTYPE" "DASHED"))
      (progn
        (setq linFile (if (= (getvar "MEASUREMENT") 1) "acadiso.lin" "acad.lin"))
        (vl-catch-all-apply 'vl-cmdf (list "._-LINETYPE" "_Load" "DASHED" linFile ""))
      )
    )

    ;; Controleer of het block al bestaat, anders aanmaken
    (if (not (tblsearch "BLOCK" blkName))
      (progn
        (entmake (list '(0 . "BLOCK") (cons 2 blkName) '(70 . 2) '(10 0.0 0.0 0.0)))
        ;; Cirkel (Gesloten Polylijn) expliciet op laag "0" én Kleur 256 (ByLayer)
        (entmake (list '(0 . "LWPOLYLINE") '(8 . "0") '(62 . 256) '(100 . "AcDbEntity") (if (tblsearch "LTYPE" "DASHED") '(6 . "DASHED") '(6 . "BYLAYER")) '(100 . "AcDbPolyline") 
                       '(90 . 2) '(70 . 1) (cons 43 width) (list 10 (- radius) 0.0) '(42 . 1.0) (list 10 radius 0.0) '(42 . 1.0)))
        ;; Horizontale Lijn expliciet op laag "0" én Kleur 256 (ByLayer)
        (entmake (list '(0 . "LWPOLYLINE") '(8 . "0") '(62 . 256) '(100 . "AcDbEntity") (if (tblsearch "LTYPE" "DASHED") '(6 . "DASHED") '(6 . "BYLAYER")) '(100 . "AcDbPolyline") 
                       '(90 . 2) (cons 43 width) (list 10 (- radius) 0.0) (list 10 radius 0.0)))
        ;; Verticale Lijn expliciet op laag "0" én Kleur 256 (ByLayer)
        (entmake (list '(0 . "LWPOLYLINE") '(8 . "0") '(62 . 256) '(100 . "AcDbEntity") (if (tblsearch "LTYPE" "DASHED") '(6 . "DASHED") '(6 . "BYLAYER")) '(100 . "AcDbPolyline") 
                       '(90 . 2) (cons 43 width) (list 10 0.0 (- radius)) (list 10 0.0 radius)))
        (entmake '((0 . "ENDBLK")))
      )
    )
  )

  ;; --- DCL Interface Generator ---
  (defun write-dcl (filename title label key)
    (setq file (open filename "w"))
    (write-line (strcat "list_dialog : dialog { label = \"" title "\";") file)
    (write-line (strcat "  : text { label = \"" label "\"; }") file)
    (write-line (strcat "  : list_box { key = \"" key "\"; width = 60; height = 15; multiple_select = " 
                        (if (= key "blk_list") "true" "false") "; }") file)
    (write-line "  ok_cancel; }" file)
    (close file)
  )

  ;; Haal alle Xrefs op die in de tekening staan (als Insert)
  (defun get-xref-list (/ lst item)
    (setq item (tblnext "BLOCK" t))
    (while item
      (if (= (logand (cdr (assoc 70 item)) 4) 4)
        (setq lst (cons (cdr (assoc 2 item)) lst))
      )
      (setq item (tblnext "BLOCK"))
    )
    (vl-sort lst '<)
  )

  (defun get-xref-blocknames (blkname / ent entData name lst)
    (setq ent (tblobjname "BLOCK" blkname))
    (while (setq ent (entnext ent))
      (setq entData (entget ent))
      (if (= (cdr (assoc 0 entData)) "INSERT")
        (progn
          (setq name (cdr (assoc 2 entData)))
          (if (and (not (wcmatch name "`**")) (not (member name lst)))
            (setq lst (cons name lst))
          )
        )
      )
    )
    (vl-sort lst '<)
  )

  (defun inside-p (pt pts / p1 p2 c)
    (setq c nil p1 (last pts))
    (foreach p2 pts
      (if (and (or (and (> (cadr p1) (cadr pt)) (<= (cadr p2) (cadr pt)))
                   (and (> (cadr p2) (cadr pt)) (<= (cadr p1) (cadr pt))))
               (< (car pt) (+ (car p1) (* (- (cadr pt) (cadr p1)) (/ (- (car p2) (car p1)) (- (cadr p2) (cadr p1)))))))
        (setq c (not c))
      )
      (setq p1 p2)
    )
    c
  )

  (defun str-to-list (str / pos lst)
    (while (setq pos (vl-string-search " " str))
      (setq lst (cons (atoi (substr str 1 pos)) lst))
      (setq str (substr str (+ pos 2)))
    )
    (reverse (cons (atoi str) lst))
  )

  ;; --- Normaal Program ---
  (princ "\n--- Xref Full GUI Selector (Lichtmast Mode) ---")

  ;; STAP 1: Selecteer de Xref uit een lijst
  (setq xrefs-list (get-xref-list))
  (if (not xrefs-list) (progn (alert "Geen Xrefs gevonden in deze tekening.") (exit)))

  (setq dcl-file (vl-filename-mktemp "xref_sel.dcl"))
  (write-dcl dcl-file "Xref Kiezen" "Selecteer de Xref onderlegger:" "xref_list")
  (setq dcl-id (load_dialog dcl-file))
  (if (not (new_dialog "list_dialog" dcl-id)) (exit))
  (start_list "xref_list") (mapcar 'add_list xrefs-list) (end_list)
  (action_tile "xref_list" "(setq selected-xref-idx (atoi $value))")
  (setq result (start_dialog))
  (unload_dialog dcl-id)
  (if (or (= result 0) (not selected-xref-idx)) (exit))
  (setq xrefName (nth selected-xref-idx xrefs-list))

  ;; STAP 2: Selecteer Blokken uit die Xref
  (setq names-list (get-xref-blocknames xrefName))
  (if (not names-list) (progn (alert "Geen blokken gevonden in deze Xref.") (exit)))
  
  (write-dcl dcl-file "Blokken Kiezen" (strcat "Kies blokken uit " xrefName ":") "blk_list")
  (setq dcl-id (load_dialog dcl-file))
  (if (not (new_dialog "list_dialog" dcl-id)) (exit))
  (start_list "blk_list") (mapcar 'add_list names-list) (end_list)
  (action_tile "blk_list" "(setq selected-indices $value)")
  (setq result (start_dialog))
  (unload_dialog dcl-id)
  (vl-file-delete dcl-file)
  (if (or (= result 0) (not selected-indices)) (exit))
  (setq selected-names (mapcar '(lambda (idx) (nth idx names-list)) (str-to-list selected-indices)))

  ;; STAP 3: Grens Selecteren
  (setq polyEnt (car (entsel "\nStap 3: Selecteer de Polyline grens: ")))
  (if (or (not polyEnt) (/= (cdr (assoc 0 (entget polyEnt))) "LWPOLYLINE")) (exit))
  (defun get-p-pts (e / p) (foreach x (entget e) (if (= 10 (car x)) (setq p (cons (cdr x) p)))) (reverse p))
  (setq polyPts (get-p-pts polyEnt))

  ;; Zorg ervoor dat het 'Lichtmast_Def' block klaargemaakt is
  (ensure-lichtmast-block)

  ;; --- Zorg ervoor dat laag 'Bestaand' bestaat ---
  (if (not (tblsearch "LAYER" "Bestaand"))
    (entmake (list '(0 . "LAYER")
                   '(100 . "AcDbSymbolTableRecord")
                   '(100 . "AcDbLayerTableRecord")
                   '(2 . "Bestaand")
                   '(70 . 0)
                   '(62 . 7) ; Kleur 7 (Standaard Zwart/Wit)
             )
    )
  )

  ;; Maak een tijdelijk bron-block aan om te kunnen kopiëren
  (entmake (list '(0 . "INSERT")
                 '(2 . "Lichtmast_Def")  ; Nieuwe blocknaam
                 '(8 . "Bestaand")       ; Direct op de juiste laag geplaatst
                 '(62 . 256)             ; Kleur geforceerd ByLayer voor het block zelf
                 '(10 0.0 0.0 0.0)
                 '(41 . 1.0)
                 '(42 . 1.0)
                 '(43 . 1.0)
                 '(50 . 0.0)
           )
  )
  (setq targetEnt (entlast))
  (setq targetBasePt '(0.0 0.0 0.0))

  ;; STAP 4: Uitvoeren (Scannen en Kopiëren)
  (setq old_osmode (getvar "OSMODE"))
  (setvar "OSMODE" 0)
  (setq count 0)

  ;; Zoek de fysieke Xref insert in de tekening voor de coördinaten
  (setq ssXref (ssget "_X" (list '(0 . "INSERT") (cons 2 xrefName))))
  (if ssXref
    (progn
      (setq xrefEntData (entget (ssname ssXref 0)))
      (setq insPtX (cadr (assoc 10 xrefEntData)))
      (setq insPtY (caddr (assoc 10 xrefEntData)))
      (setq rot (cdr (assoc 50 xrefEntData)))
      (setq scaleX (cdr (assoc 41 xrefEntData)))

      (setq ent (tblobjname "BLOCK" xrefName))
      (while (setq ent (entnext ent))
        (setq entData (entget ent))
        (if (and (= (cdr (assoc 0 entData)) "INSERT") (member (cdr (assoc 2 entData)) selected-names))
          (progn
            (setq ptLocalX (cadr (assoc 10 entData)))
            (setq ptLocalY (caddr (assoc 10 entData)))
            (setq worldX (+ (- (* ptLocalX scaleX (cos rot)) (* ptLocalY scaleX (sin rot))) insPtX))
            (setq worldY (+ (+ (* ptLocalX scaleX (sin rot)) (* ptLocalY scaleX (cos rot))) insPtY))
            (setq worldPt (list worldX worldY 0.0))

            (if (inside-p worldPt polyPts)
              (progn
                ;; Gebruik het originele COPY commando met het zojuist geplaatste bron-block
                (command "_.COPY" targetEnt "" targetBasePt worldPt)
                (setq count (1+ count))
              )
            )
          )
        )
      )
    )
  )

  ;; Verwijder het tijdelijke bron-block weer op zodat je tekening netjes blijft
  (if targetEnt (entdel targetEnt))

  (setvar "OSMODE" old_osmode)
  (princ (strcat "\nKlaar! " (itoa count) " Lichtmast(en) geplaatst op de laag 'Bestaand' (kleur zwart)."))
  (princ)
)


;;; ============================================================
;;; Ingevoegd uit: RICHTINGEN.LSP
;;; ============================================================

(defun c:RICHTINGEN (/ ss mtext-list p1 p2 ang dist en ed pt draw-ang attach dist-ang current-space mode loop-dist selection-active UnformatMText GetTotalString base-pt history new-ents last-ents loop-active target-en vla-copy-obj)
  (vl-load-com)

  (defun UnformatMText (str / regex)
    (setq regex (vlax-create-object "VBScript.RegExp"))
    (vlax-put-property regex "IgnoreCase" :vlax-true)
    (vlax-put-property regex "Global" :vlax-true)
    (vlax-put-property regex "Pattern" "\\\\[a-zA-Z].*?;|\\\\P|[{} ]")
    (setq str (vlax-invoke-method regex "Replace" str " "))
    (vlax-put-property regex "Pattern" " {2,}")
    (setq str (vlax-invoke-method regex "Replace" str " "))
    (vlax-release-object regex)
    (vl-string-trim " " str)
  )

  (defun GetTotalString (lst / out)
    (setq out "")
    (foreach e lst (setq out (strcat out " | " (UnformatMText (cdr (assoc 1 (entget e)))))))
    (if (> (strlen out) 3) (substr out 4) "Geen")
  )
  
  (initget "Kopiëren Verplaatsen")
  (setq mode (getkword "\nWil je de MTexten [Kopiëren/Verplaatsen] <Kopiëren>: "))
  (if (null mode) (setq mode "Kopiëren"))

  (setq mtext-list '())
  (prompt "\nSelecteer MText objecten. (ENTER = klaar)")
  
  (setq selection-active t)
  (while selection-active
    (setvar "ERRNO" 0)
    (setq ss (entsel (strcat "\nItems (" (itoa (length mtext-list)) "): [" (GetTotalString mtext-list) "] - Selecteer: ")))
    (cond
      ((and (not ss) (= (getvar "ERRNO") 52)) (setq selection-active nil))
      ((not ss) (princ "\nMisgeklikt!"))
      (t
       (setq en (car ss))
       (setq ed (entget en))
       (if (= (cdr (assoc 0 ed)) "MTEXT")
         (if (member en mtext-list)
           (progn (setq mtext-list (vl-remove en mtext-list)) (redraw en 4) (princ (strcat "\nVerwijderd. Totaal nu: " (GetTotalString mtext-list))))
           (progn (redraw en 3) (setq mtext-list (append mtext-list (list en))) (princ (strcat "\nToegevoegd: " (UnformatMText (cdr (assoc 1 ed)))))))
         (princ "\nFout: Geen MText.")))
    )
  )

  (if mtext-list
    (progn
      (setq history '()) ;; Lijst om acties op te slaan voor "Stap Terug"
      (setq loop-active t)
      
      (while loop-active
        (if (= mode "Kopiëren")
          (initget "Terug")
        )
        (setq p1 (getpoint (if (= mode "Kopiëren") "\nStartpunt of [Terug] (Enter/Esc om te stoppen): " "\nStartpunt verplaatsen: ")))
        
        (cond
          ;; --- GEVAL 1: JE KIEST "TERUG" (UNDO) ---
          ((= p1 "Terug")
           (if (> (length history) 0)
             (progn
               (setq last-ents (car history))
               (foreach e last-ents (if (entget e) (entdel e))) ;; Verwijder vorige teksten
               (setq history (cdr history)) ;; Haal uit geschiedenis
               (princ "\nLaatste stap ongedaan gemaakt.")
             )
             (princ "\nNiets om ongedaan te maken.")
           )
          )
          
          ;; --- GEVAL 2: JE KLIKT EEN STARTPUNT ---
          ((listp p1)
           ;; Gebruik de NATIVE AutoCAD getpoint functie voor p2. 
           (if (setq p2 (getpoint p1 "\nRichting (Eindpunt): "))
             (progn
               (setq ang (angle p1 p2))
               (if (and (> ang (/ pi 2.0)) (<= ang (* pi 1.5)))
                 (setq draw-ang (+ ang pi) dist-ang (+ ang (/ pi 2.0)) attach 1 base-pt p2 loop-dist 0.4 current-space 0.2)
                 (setq draw-ang ang dist-ang (+ ang (/ pi 2.0)) attach 7 base-pt p1 loop-dist 0.4 current-space 0.2)
               )
               
               (setq dist loop-dist new-ents '())
               
               (foreach en mtext-list
                 (redraw en 4)
                 
                 ;; --- NIEUWE VEILIGE KOPIEER METHODE ---
                 (if (= mode "Verplaatsen")
                   (setq target-en en) ; Gebruik origineel voor Verplaatsen
                   (progn
                     ; Maak een schone ActiveX kopie om database/klembord errors te voorkomen
                     (setq vla-copy-obj (vla-copy (vlax-ename->vla-object en)))
                     (setq target-en (vlax-vla-object->ename vla-copy-obj))
                     (setq new-ents (cons target-en new-ents))
                   )
                 )

                 ;; Haal de DXF data op van het te bewerken object (origineel óf de schone kopie)
                 (setq ed (entget target-en))
                 (setq pt (polar base-pt dist-ang dist))
                 
                 ;; Pas locatie en rotatie aan
                 (setq ed (subst (cons 10 pt) (assoc 10 ed) ed))
                 (setq ed (subst (cons 50 draw-ang) (assoc 50 ed) ed))
                 (if (assoc 71 ed)
                   (setq ed (subst (cons 71 attach) (assoc 71 ed) ed))
                   (setq ed (append ed (list (cons 71 attach)))))
                 
                 ;; Update het object in AutoCAD
                 (entmod ed)
                 
                 ;; Bereken afstand voor de volgende MText in de loop
                 (setq dist (+ dist current-space (cdr (assoc 43 (entget en)))))
               )
               
               ;; Beheer Kopiëren/Verplaatsen loops
               (if (= mode "Kopiëren")
                 (setq history (cons new-ents history)) ;; Sla op voor de Undo (Terug) functie
                 (setq loop-active nil) ;; Stop na 1 keer verplaatsen
               )
             )
           )
          )
          
          ;; --- GEVAL 3: ENTER OF ESC IN STARTPUNT (STOP) ---
          (t (setq loop-active nil)) 
        )
      )
    )
  )
  
  (foreach en mtext-list (if (entget en) (redraw en 4)))
  (princ "\nKlaar.")
  (princ)
)

;;; Einde gecombineerd laadbestand
(princ "\nKabelGeul_Alles geladen: KABELGEUL, LICHTMAST en RICHTINGEN zijn beschikbaar.")
(princ)
