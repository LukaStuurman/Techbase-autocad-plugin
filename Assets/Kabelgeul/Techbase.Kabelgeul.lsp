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

(setq *tbkg:version* "KabelGeul Omni 2026-08-06ad - vaste rijafstand en compacte twee-zijdige bak")

;; Alle kabelvarianten gebruiken dezelfde vaste mantelbuismaat. De straal is
;; verkleind van 0.74 naar 0.64, zodat de mantel duidelijk compacter rond
;; Enkel, Dubbel, Drie en Combi ligt.
(setq *tbkg:mantelbuis-radius* 0.64)
;; Eén vaste hart-op-hartafstand voor alle kabelrijen en voor beide zijden.
;; Diameter 1.28 + 0.05 vrije ruimte = 1.33. Hierdoor raken mantelbuizen
;; elkaar net niet en blijft de afstand overal gelijk, ongeacht kabelvorm.
(setq *tbkg:vaste-hartafstand* (+ (* 2.0 *tbkg:mantelbuis-radius*) 0.05))
(setq *tbkg:twosided-center-sep* *tbkg:vaste-hartafstand*)
(setq *tbkg:kabelrij-spacing* *tbkg:vaste-hartafstand*)

;; Compacte vaste bakhoogte. Twee-zijdige mantelbuizen passen hierin met
;; een kleine buitenmarge, terwijl hun onderlinge vrije ruimte 0.05 blijft.
(setq *tbkg:bakhoogte* 3.00)

(setq *tbkg:last-count* 3)
(setq *tbkg:last-types* '("K01" "K02" "Bestaand"))
(setq *tbkg:last-labels* '("" "" ""))
(setq *tbkg:last-existing-labels* '("" "" ""))
(setq *tbkg:last-doubles* '(nil nil nil))
(setq *tbkg:last-triples* '(nil nil nil))
(setq *tbkg:last-twosideds* '(nil nil nil))
(setq *tbkg:last-side2-doubles* '(nil nil nil))
(setq *tbkg:last-side2-triples* '(nil nil nil))
(setq *tbkg:last-side2-combis* '(nil nil nil))
(setq *tbkg:last-mantels* '(nil nil nil))
(setq *tbkg:last-splits* '(nil nil nil))
(setq *tbkg:last-combis* '(nil nil nil))
(setq *tbkg:last-mantel-spec* "")
(setq *tbkg:type-options* '("K01" "K02" "K03" "K04" "K05" "K06" "K07" "K08" "K09" "K10" "K11" "K12" "MS Nieuw" "MS Bestaand" "MS Rooien" "Bestaand" "Rooien" "Half rooien"))
(setq *tbkg:shape-options* '("Enkel" "Dubbel" "Drie" "Combi"))
(setq *tbkg:dialog-row-count* 3)
(setq *tbkg:dialog-resize-count* nil)
(setq *tbkg:block-counter* 0)
(setq *tbkg:new-ents* nil)
(setq *tbkg:floating-code* nil)
;; CODESWITCH bepaalt de opmaak van nieuw te plaatsen zwevende KabelGeulen.
;; De keuze wordt in AutoCAD bewaard, zodat zij ook na opnieuw starten actief blijft.
(if (not (boundp '*tbkg:future-code-switch*))
  (setq *tbkg:future-code-switch*
    (if (equal (getenv "TechbaseKabelGeulCodeSwitch") "1") T nil)))
(setq *tbkg:copy-stage* nil)
(setq *tbkg:mask-app* "KABELGEUL_MASK")
(setq *tbkg:lsp-dir*
  (if (and (boundp '*load-truename*) *load-truename*)
    (vl-filename-directory *load-truename*)
    nil))

(defun tbkg:track-ent (ent)
  (if ent
    (setq *tbkg:new-ents* (cons ent *tbkg:new-ents*)))
  ent)

(defun tbkg:unique-block-name (/ base name)
  (setq base (strcat "KabelGeul_" (vl-string-translate "." "_" (rtos (getvar "CDATE") 2 8))))
  (setq *tbkg:block-counter* (1+ *tbkg:block-counter*))
  (setq name (strcat base "_" (itoa *tbkg:block-counter*)))
  (while (tblsearch "BLOCK" name)
    (setq *tbkg:block-counter* (1+ *tbkg:block-counter*))
    (setq name (strcat base "_" (itoa *tbkg:block-counter*))))
  name)

(defun tbkg:layer-defs (/)
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
     ;; De kleur van MS Rooien wordt in tbkg:ensure-layers gelijkgezet aan
     ;; de normale Rooien-laag (ACI 160).
     ("MS Rooien" 160 "DASHDOT")
     ("Mantelbuis" 6 "Continuous")
    ("Opmerking" 1 "Continuous")))

(defun tbkg:ensure-ltype (doc ltype / ltypes)
  (if (and (/= (strcase ltype) "CONTINUOUS")
           (not (tblsearch "LTYPE" ltype)))
    (progn
      (setq ltypes (vla-get-Linetypes doc))
      (vl-catch-all-apply 'vla-Load (list ltypes ltype "acad.lin"))))
  (if (tblsearch "LTYPE" ltype) ltype "Continuous"))

(defun tbkg:safe-put (obj prop val)
  (vl-catch-all-apply
    'vlax-put-property
    (list obj prop val)))

(defun tbkg:make-layer (name color ltype) (TBKG_NATIVE_LAYER name color ltype))

(defun tbkg:set-layer-aci-color (name color / ent data)
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

(defun tbkg:set-layer-true-color (name rgb / ent data)
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

(defun tbkg:ensure-layers (/ d) (foreach d (tbkg:layer-defs) (tbkg:make-layer (car d) (cadr d) (caddr d))))

(defun tbkg:hide-wipeout-frames (/ result)
  ;; WIPEOUTFRAME=0 verbergt frames op scherm en in plots. FRAME wordt bewust
  ;; niet aangepast, zodat frames van afbeeldingen, underlays en xrefs behouden blijven.
  (setq result (vl-catch-all-apply 'setvar (list "WIPEOUTFRAME" 0)))
  (not (vl-catch-all-error-p result)))

(defun tbkg:type->layer (typ)
  (cond
    ((= typ "MS Nieuw") "MS Nieuw")
    ((= typ "MS Bestaand") "MS Bestaand")
    ((= typ "MS Rooien") "MS Rooien")
    ((= typ "Bestaand") "01 - Bestaande kabel")
    ((= typ "Rooien") "01-Te Rooien Kabel")
    ((= typ "Half rooien") "01-Te Rooien Kabel")
    (T (strcat "Aansluiting LS " typ))))

(defun tbkg:type->text-layer (typ)
  ;; MS Nieuw heeft een zwarte cirkel, maar roze kabeltekst zoals K05.
  (if (= typ "MS Nieuw")
    "Aansluiting LS K05"
    (tbkg:type->layer typ)))

(defun tbkg:type->label (typ)
  (cond
    ((= typ "MS Nieuw") "MS Nieuw")
    ((= typ "MS Bestaand") "MS Bestaand")
    ((= typ "MS Rooien") "(Rooien) kabel")
    ((= typ "Bestaand") "Bestaande kabel")
    ((= typ "Rooien") "(Rooien) kabel")
    ((= typ "Half rooien") "(Rooien) kabel")
    (T (strcat "Ri " (itoa (atoi (substr typ 2))) " "))))

(defun tbkg:type->color (typ / layer def result)
  (setq layer (strcase (tbkg:type->layer typ)))
  (foreach def (tbkg:layer-defs)
    (if (= (strcase (car def)) layer)
      (setq result (cadr def))))
  result)

(defun tbkg:color->type (color / opt result)
  (if color
    (foreach opt *tbkg:type-options*
      (if (and (not (member opt '("MS Nieuw" "MS Bestaand")))
               (not (= opt "Half rooien"))
               (= color (tbkg:type->color opt)))
        (setq result opt))))
  result)

(defun tbkg:starts-with (txt prefix / n)
  (if (null txt) (setq txt ""))
  (setq n (strlen prefix))
  (and (>= (strlen txt) n)
       (= (substr txt 1 n) prefix)))

(defun tbkg:prefixed-label (typ txt) (if txt txt ""))

(defun tbkg:midpoint (p1 p2)
  (mapcar '(lambda (a b) (/ (+ a b) 2.0)) p1 p2))

(defun tbkg:p+ (p a d)
  (polar p a d))

(defun tbkg:local->world (base ang x y)
  (tbkg:p+ (tbkg:p+ base ang x) (+ ang (/ pi 2.0)) y))

(defun tbkg:dot2 (p1 p2)
  (+ (* (car p1) (car p2)) (* (cadr p1) (cadr p2))))

(defun tbkg:vector2 (p1 p2)
  (list (- (car p2) (car p1)) (- (cadr p2) (cadr p1))))

(defun tbkg:endpoint-from-perp (p1 rawp2 perp-ang / unit v d)
  (setq unit (list (cos perp-ang) (sin perp-ang)))
  (setq v (tbkg:vector2 p1 rawp2))
  (setq d (tbkg:dot2 v unit))
  (if (< d 0.0)
    (progn
      (setq perp-ang (+ perp-ang pi))
      (setq d (- d))))
  (setq d (distance p1 rawp2))
  (polar p1 perp-ang d))

(defun tbkg:pt2 (p)
  (list (car p) (cadr p)))

(defun tbkg:point-p (p)
  (and (listp p)
       (numberp (car p))
       (numberp (cadr p))))

(defun tbkg:valid-points (pts / out)
  (setq out '())
  (if (listp pts)
    (foreach pt pts
      (if (tbkg:point-p pt)
        (setq out (cons pt out)))))
  (reverse out))

(defun tbkg:friendly-error-message (msg / upper pos value)
  (setq upper (strcase (if msg msg "")))
  (if (wcmatch upper "*BAD ARGUMENT TYPE: CONSP*")
    (progn
      (setq pos (vl-string-search "CONSP " upper))
      (setq value (if pos (substr msg (+ pos 7)) "onbekend"))
      (strcat
        "ongeldig punt of coordinaat in het bronblok. Gevonden waarde: " value
        ". Selecteer bij een zwevende geul het blok, de zwevende code of de blauwe referentielijn."))
    msg))

(defun tbkg:pt3 (p)
  (list (car p) (cadr p) (if (caddr p) (caddr p) 0.0)))

(defun tbkg:world-up (pt dist)
  (list
    (car pt)
    (+ (cadr pt) dist)
    (if (caddr pt) (caddr pt) 0.0)))

(defun tbkg:readable-text-props (ang / two flipped)
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

(defun tbkg:index-string (i)
  (if (< i 10)
    (strcat "0" (itoa i))
    (itoa i)))

(defun tbkg:index-tag (prefix i)
  (strcat prefix "_" (tbkg:index-string i)))

(defun tbkg:trim (txt)
  (vl-string-trim " \t\r\n" (if txt txt "")))

(defun tbkg:normalize-type (txt / upper n result)
  (setq upper (strcase (tbkg:trim txt)))
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
       (if (= upper (strcase (tbkg:type->label (strcat "K" (tbkg:index-string n)))))
         (setq result (strcat "K" (tbkg:index-string n))))
       (setq n (1- n)))
     result)))

(defun tbkg:strip-known-prefix (txt) (if txt txt ""))

(defun tbkg:editor-item-point (item)
  (cond
    ((nth 5 item) (nth 5 item))
    ((nth 4 item) (nth 4 item))
    (T (nth 3 item))))

(defun tbkg:editor-item< (a b / ap bp ay by ax bx)
  (setq ap (tbkg:editor-item-point a))
  (setq bp (tbkg:editor-item-point b))
  (setq ay (cadr ap))
  (setq by (cadr bp))
  (setq ax (car ap))
  (setq bx (car bp))
  (if (equal ay by 1e-8)
    (< ax bx)
    (> ay by)))

(defun tbkg:curve-angle-at-pick (ent pick / cp param deriv data ang)
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

(defun tbkg:curve-point-at-pick (ent pick / cp)
  (setq cp (vl-catch-all-apply 'vlax-curve-getClosestPointTo (list ent pick)))
  (if (or (vl-catch-all-error-p cp) (null cp))
    pick
    cp))

(defun tbkg:make-lwpoly (pts layer closed / data)
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
          '(lambda (p) (list (cons 10 (tbkg:pt2 p))))
          pts))))
  (tbkg:track-ent (entmakex data)))

(defun tbkg:make-wipeout (pts layer / data poly result wipe wipe-data)
  (tbkg:hide-wipeout-frames)
  (setq pts (tbkg:valid-points pts))
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
              '(lambda (p) (list (cons 10 (tbkg:pt2 p))))
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
              (tbkg:track-ent wipe))
            (progn
              (if (entget poly) (entdel poly))
              nil)))))))

(defun tbkg:set-mask-info (ent tag txt / data)
  (if (and ent tag)
    (progn
      (regapp *tbkg:mask-app*)
      ;; entget zonder applijst laat bestaande XData weg; zo wordt onze informatie schoon vervangen.
      (setq data (entget ent))
      (entmod
        (append data
          (list
            (list -3
              (list *tbkg:mask-app*
                (cons 1000 tag)
                (cons 1000 (if txt txt "")))))))
      ent)))

(defun tbkg:mask-info (ent / xdata app values)
  (if ent
    (progn
      (setq xdata (assoc -3 (entget ent (list *tbkg:mask-app*))))
      (setq app (if xdata (cadr xdata) nil))
      (setq values (if app (cdr app) nil))
      (if values
        (list
          (if (car values) (cdr (car values)) nil)
          (if (cadr values) (cdr (cadr values)) ""))
        nil))))

(defun tbkg:text-mask-width (txt h / box width)
  (setq box
    (vl-catch-all-apply
      'textbox
      (list
        (list '(0 . "TEXT")
              (cons 1 (if txt txt ""))
              (cons 40 h)
              (cons 7 *tbkg:style*)))))
  (setq width
    (if (or (vl-catch-all-error-p box)
            (null box)
            (null (cadr box)))
      (* h 0.65 (max 1 (strlen (if txt txt ""))))
      (- (car (cadr box)) (car (car box)))))
  (max h width))

(defun tbkg:text-mask-points (pt ang just txt h tag / width pad start-overlap half-thickness cable-mask mantel-mask x1 x2 y1 y2)
  (if (tbkg:point-p pt)
    (progn
      (setq width (tbkg:text-mask-width txt h))
      (setq pad 0.10)
      (setq cable-mask
        (or (tbkg:starts-with (strcase (if tag tag "")) "KABEL_")
            (tbkg:starts-with (strcase (if tag tag "")) "BESTAAND_")
            (tbkg:starts-with (strcase (if tag tag "")) "ZIJDE2_")))
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
          (+ (/ *tbkg:kabelrij-spacing* 2.0) 0.01)
          (+ (/ h 2.0) pad)))
      (setq y1 (- half-thickness)
            y2 half-thickness)
      (list
        (tbkg:local->world pt ang x1 y1)
        (tbkg:local->world pt ang x2 y1)
        (tbkg:local->world pt ang x2 y2)
        (tbkg:local->world pt ang x1 y2)))))

(defun tbkg:draw-text-wipeout (pt ang just txt h tag / wipe)
  (setq wipe
    (tbkg:make-wipeout
      (tbkg:text-mask-points pt ang just txt h tag)
      "E_LV_CROSS_SECTION"))
  (if wipe (tbkg:set-mask-info wipe tag txt))
  wipe)

(defun tbkg:set-wipeout-points (ent pts / minx miny maxx maxy size clips first-clip data out pair code)
  (setq pts (tbkg:valid-points pts))
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

(defun tbkg:make-circle (pt rad layer)
  (tbkg:track-ent
    (entmakex
    (list '(0 . "CIRCLE")
          '(100 . "AcDbEntity")
          (cons 8 layer)
          '(6 . "Continuous")
          '(100 . "AcDbCircle")
          (cons 10 (tbkg:pt3 pt))
            (cons 40 rad)))))

(defun tbkg:make-line (p1 p2 layer)
  (tbkg:track-ent
    (entmakex
    (list '(0 . "LINE")
          '(100 . "AcDbEntity")
          (cons 8 layer)
          '(6 . "Continuous")
          '(100 . "AcDbLine")
          (cons 10 (tbkg:pt3 p1))
            (cons 11 (tbkg:pt3 p2))))))

(defun tbkg:make-attribute (pt h rot tag prompt val layer hjust flags / data)
  (if (null hjust) (setq hjust 0))
  (if (null flags) (setq flags 0))
  (tbkg:track-ent
    (entmakex
    (list '(0 . "ATTDEF")
          '(100 . "AcDbEntity")
          (cons 8 layer)
          '(100 . "AcDbText")
          (cons 10 (tbkg:pt3 pt))
          (cons 40 h)
          (cons 1 val)      ; De waarde
          (cons 50 rot)
          (cons 7 *tbkg:style*)
          (cons 72 hjust)   ; 0 = Middle Left, 2 = Middle Right
          (cons 11 (tbkg:pt3 pt))
          '(100 . "AcDbAttributeDefinition")
          '(280 . 0)
          (cons 3 prompt)   ; DE PROMPT (Verplicht voor goede werking!)
          (cons 2 tag)      ; De Tag-naam (bijv. KABEL_1)
          (cons 70 flags)
          '(73 . 0)
          '(74 . 2)         ; Middle Left: midden verticaal
            '(280 . 0)))))

(defun tbkg:make-text (pt h rot val layer hjust track / data ent)
  (if (null hjust) (setq hjust 0))
  (setq data
    (list '(0 . "TEXT")
          '(100 . "AcDbEntity")
          (cons 8 layer)
          '(100 . "AcDbText")
          (cons 10 (tbkg:pt3 pt))
          (cons 40 h)
          (cons 1 val)
          (cons 50 rot)
          (cons 7 *tbkg:style*)
          (cons 72 hjust)
          (cons 11 (tbkg:pt3 pt))
          '(73 . 2)))
  (setq ent (entmakex data))
  (if track
    (tbkg:track-ent ent)
    ent))

(defun tbkg:floating-code-p (txt)
  (and txt
       (= (strlen txt) 4)
       (wcmatch (substr txt 1 3) "###")
       (wcmatch (strcase (substr txt 4 1)) "[A-Z]")))

(defun tbkg:floating-code-index (txt / num letter)
  (setq num (atoi (substr txt 1 3)))
  (setq letter (- (ascii (strcase (substr txt 4 1))) 65))
  (+ (1- num) (* letter 999)))

(defun tbkg:index->floating-code (idx / num letter)
  (setq num (1+ (rem idx 999)))
  (setq letter (chr (+ 65 (/ idx 999))))
  (strcat
    (cond
      ((< num 10) (strcat "00" (itoa num)))
      ((< num 100) (strcat "0" (itoa num)))
      (T (itoa num)))
    letter))

(defun tbkg:next-floating-code (/ ss idx ent data txt maxidx code)
  (setq maxidx -1)
  (setq ss (ssget "_X" '((0 . "TEXT,MTEXT,ATTRIB"))))
  (if ss
    (progn
      (setq idx 0)
      (while (< idx (sslength ss))
        (setq ent (ssname ss idx))
        (setq data (entget ent))
        (setq txt (cdr (assoc 1 data)))
        (if (tbkg:floating-code-p txt)
          (setq maxidx (max maxidx (tbkg:floating-code-index txt))))
        (setq idx (1+ idx)))))
  (setq code (tbkg:index->floating-code (1+ maxidx)))
  code)

(defun tbkg:draw-floating-reference (p1 p2 code / ang text-props text-pt)
  (setq ang (angle p1 p2))
  (setq text-props (tbkg:readable-text-props (- ang (/ pi 2.0))))
  (setq text-pt (tbkg:local->world p2 ang 0.45 0.0))
  (entmakex
    (list '(0 . "LWPOLYLINE")
          '(100 . "AcDbEntity")
          '(8 . "KabelGeul zwevend referentie")
          '(62 . 5)
          '(100 . "AcDbPolyline")
          '(90 . 2)
          '(70 . 0)
          '(43 . 0.1)
          (cons 10 (tbkg:pt2 p1))
          (cons 10 (tbkg:pt2 p2))))
  (tbkg:make-text text-pt 0.55 (car text-props) code "KabelGeul zwevend code" 1 nil))

(defun tbkg:find-new-insert (after / next data found)
  (setq next (if after (entnext after) (entnext)))
  (while (and next (null found))
    (setq data (entget next))
    (if (= (cdr (assoc 0 data)) "INSERT")
      (setq found next))
    (setq next (entnext next)))
  found)

(defun tbkg:bring-to-front (ent / ss result)
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

(defun tbkg:open-enhanced-attribute-editor (insert-ent / oldcmddia ss result)
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

(defun tbkg:insert-by-floating-code (code / ss i insert-ent attr data found)
  (if (tbkg:floating-code-p code)
    (setq ss (ssget "_X" '((0 . "INSERT") (66 . 1)))))
  (if ss
    (progn
      (setq i 0)
      (while (and (< i (sslength ss)) (null found))
        (setq insert-ent (ssname ss i))
        (setq attr (tbkg:attr-ent insert-ent "ZWEVEND_CODE"))
        (if attr
          (progn
            (setq data (entget attr))
            (if (= (strcase (if (cdr (assoc 1 data)) (cdr (assoc 1 data)) "")) (strcase code))
              (setq found insert-ent))))
        (setq i (1+ i)))))
  found)

(defun tbkg:nearest-floating-code (pt / ss i ent data txt text-pt dist best-dist best-code)
  (setq ss (ssget "_X" '((0 . "TEXT,MTEXT") (8 . "KabelGeul zwevend code"))))
  (if ss
    (progn
      (setq i 0)
      (while (< i (sslength ss))
        (setq ent (ssname ss i))
        (setq data (entget ent))
        (setq txt (cdr (assoc 1 data)))
        (setq text-pt (cdr (assoc 10 data)))
        (if (and text-pt (tbkg:floating-code-p txt))
          (progn
            (setq dist (distance (tbkg:pt2 pt) (tbkg:pt2 text-pt)))
            (if (and (< dist 2.0) (or (null best-dist) (< dist best-dist)))
              (setq best-dist dist
                    best-code txt))))
        (setq i (1+ i)))))
  best-code)

(defun tbkg:floating-code-for-entity (ent / data typ layer pair pts pt txt)
  (if ent
    (progn
      (setq data (entget ent))
      (setq typ (cdr (assoc 0 data)))
      (setq layer (cdr (assoc 8 data)))
      (cond
        ((member typ '("TEXT" "MTEXT"))
         (setq txt (cdr (assoc 1 data)))
         (if (tbkg:floating-code-p txt) txt nil))
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
            (setq pts (tbkg:old-polyline-points ent))
            (setq pt (if pts (car (last pts)) nil))))
         (if pt (tbkg:nearest-floating-code pt) nil))
        (T nil)))))

(defun tbkg:selected-insert-original (ent / data owner code)
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
         (setq code (tbkg:floating-code-for-entity ent))
         (if code (tbkg:insert-by-floating-code code) nil))))))

(defun tbkg:dxf-set (data code val)
  (if (assoc code data)
    (subst (cons code val) (assoc code data) data)
    (append data (list (cons code val)))))

(defun tbkg:update-ent-codes (ent pairs / data pair)
  (if (and ent (entget ent))
    (progn
      (setq data (entget ent))
      (foreach pair pairs
        (setq data (tbkg:dxf-set data (car pair) (cdr pair))))
      (entmod data)
      (entupd ent))))

(defun tbkg:attribute-point (data)
  (cond
    ((assoc 11 data) (cdr (assoc 11 data)))
    ((assoc 10 data) (cdr (assoc 10 data)))
    (T '(0.0 0.0 0.0))))

(defun tbkg:attr-ent (insert-ent tag / next data found)
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

(defun tbkg:attr-ent-by-index (insert-ent prefix i)
  (or
    (tbkg:attr-ent insert-ent (tbkg:index-tag prefix i))
    (tbkg:attr-ent insert-ent (strcat prefix "_" (itoa i)))))

(defun tbkg:kabel-tag-p (tag)
  (tbkg:starts-with (strcase (if tag tag "")) "KABEL_"))

(defun tbkg:bestaand-tag-p (tag)
  (tbkg:starts-with (strcase (if tag tag "")) "BESTAAND_"))

(defun tbkg:zijde2-tag-p (tag)
  (tbkg:starts-with (strcase (if tag tag "")) "ZIJDE2_"))

(defun tbkg:tag-index (tag)
  (atoi (substr (strcase tag) 7)))

(defun tbkg:bestaand-tag-index (tag)
  (atoi (substr (strcase tag) 10)))

(defun tbkg:zijde2-tag-index (tag)
  (atoi (substr (strcase tag) 8)))

(defun tbkg:direction-tag-p (tag)
  (tbkg:starts-with (strcase (if tag tag "")) "RICHTING_"))

(defun tbkg:remove-direction-attributes (insert-ent / data blkname blkrec next todel)
  (if (and insert-ent (= (cdr (assoc 0 (entget insert-ent))) "INSERT"))
    (progn
      (setq next (entnext insert-ent))
      (while next
        (setq data (entget next))
        (cond
          ((= (cdr (assoc 0 data)) "ATTRIB")
           (if (tbkg:direction-tag-p (cdr (assoc 2 data)))
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
               (if (tbkg:direction-tag-p (cdr (assoc 2 data)))
                 (setq todel (cons next todel))))
              ((= (cdr (assoc 0 data)) "ENDBLK")
               (setq next nil)))
            (if next
              (setq next (entnext next))))
          (foreach next todel
            (entdel next))))
      (entupd insert-ent))))

(defun tbkg:block-attdef-ent (blkrec tag / next data found)
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

(defun tbkg:text-mask-tag-p (tag / upper)
  (setq upper (strcase (if tag tag "")))
  (or (tbkg:starts-with upper "KABEL_")
      (tbkg:starts-with upper "BESTAAND_")
      (tbkg:starts-with upper "ZIJDE2_")
      (= upper "ZWEVEND_CODE")
      (= upper "MANTEL_SPEC")))

(defun tbkg:block-text-mask-ent (blkrec tag / next data info found)
  (setq tag (strcase (if tag tag "")))
  (if blkrec
    (progn
      (setq next (entnext blkrec))
      (while (and next (null found))
        (setq data (entget next))
        (cond
          ((= (cdr (assoc 0 data)) "WIPEOUT")
           (setq info (tbkg:mask-info next))
           (if (and info (= (strcase (if (car info) (car info) "")) tag))
             (setq found next)))
          ((= (cdr (assoc 0 data)) "ENDBLK")
           (setq next nil)))
        (if (and next (null found))
          (setq next (entnext next))))))
  found)

(defun tbkg:update-text-wipeouts-for-insert (insert-ent / insert-data blkname blkrec next data tag txt mask info attdef attdef-data pt ang just h pts changed)
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
           (if (tbkg:text-mask-tag-p tag)
             (progn
               (setq txt (if (assoc 1 data) (cdr (assoc 1 data)) ""))
               (setq mask (tbkg:block-text-mask-ent blkrec tag))
               (setq info (if mask (tbkg:mask-info mask) nil))
               (if (and mask info (/= (cadr info) txt))
                 (progn
                   (setq attdef (tbkg:block-attdef-ent blkrec tag))
                   (if attdef
                     (progn
                       (setq attdef-data (entget attdef))
                       (setq pt (tbkg:attribute-point attdef-data))
                       (setq ang (if (assoc 50 attdef-data) (cdr (assoc 50 attdef-data)) 0.0))
                       (setq just (if (assoc 72 attdef-data) (cdr (assoc 72 attdef-data)) 0))
                       (setq h (if (assoc 40 attdef-data) (cdr (assoc 40 attdef-data)) 0.55))
                       (setq pts (tbkg:text-mask-points pt ang just txt h tag))
                       (if (tbkg:set-wipeout-points mask pts)
                         (progn
                           (tbkg:set-mask-info mask tag txt)
                           (setq changed (1+ changed)))))))))))
          ((= (cdr (assoc 0 data)) "SEQEND")
           (setq next nil)))
        (if next (setq next (entnext next))))
      (if (> changed 0)
        (entupd insert-ent))))
  changed)


(defun tbkg:placement-value (data code / value)
  ;; Lees een tekstplaatsingswaarde met veilige AutoCAD-standaardwaarden.
  (setq value (assoc code data))
  (cond
    (value (cdr value))
    ((= code 11) (if (assoc 10 data) (cdr (assoc 10 data)) '(0.0 0.0 0.0)))
    ((= code 50) 0.0)
    ((= code 72) 0)
    ((= code 74) 2)
    (T nil)))

(defun tbkg:swap-text-placement (ent-a ent-b / data-a data-b new-a new-b code value-a value-b)
  ;; Wissel uitsluitend de plaatsing, rotatie en uitlijning; tekst, tag en laag blijven gelijk.
  (if (and ent-a ent-b (entget ent-a) (entget ent-b))
    (progn
      (setq data-a (entget ent-a)
            data-b (entget ent-b)
            new-a data-a
            new-b data-b)
      (foreach code '(10 11 50 72 74)
        (setq value-a (tbkg:placement-value data-a code)
              value-b (tbkg:placement-value data-b code))
        (if value-b (setq new-a (tbkg:dxf-set new-a code value-b)))
        (if value-a (setq new-b (tbkg:dxf-set new-b code value-a))))
      (if (and (entmod new-a) (entmod new-b))
        (progn
          (entupd ent-a)
          (entupd ent-b)
          T)
        nil))
    nil))

(defun tbkg:refresh-text-mask-for-tag (insert-ent tag / insert-data blkname blkrec attr data txt mask attdef attdef-data pt ang just h pts)
  ;; Herbereken één wipeout na een plaatsingswissel, ook als de tekstwaarde niet veranderde.
  (if (and insert-ent (= (cdr (assoc 0 (entget insert-ent))) "INSERT"))
    (progn
      (setq insert-data (entget insert-ent)
            blkname (cdr (assoc 2 insert-data))
            blkrec (if blkname (tblobjname "BLOCK" blkname) nil)
            attr (tbkg:attr-ent insert-ent tag)
            mask (tbkg:block-text-mask-ent blkrec tag)
            attdef (tbkg:block-attdef-ent blkrec tag))
      (if (and attr mask attdef)
        (progn
          (setq data (entget attr)
                txt (if (assoc 1 data) (cdr (assoc 1 data)) "")
                attdef-data (entget attdef)
                pt (tbkg:attribute-point attdef-data)
                ang (if (assoc 50 attdef-data) (cdr (assoc 50 attdef-data)) 0.0)
                just (if (assoc 72 attdef-data) (cdr (assoc 72 attdef-data)) 0)
                h (if (assoc 40 attdef-data) (cdr (assoc 40 attdef-data)) 0.55)
                pts (tbkg:text-mask-points pt ang just txt h tag))
          (if (tbkg:set-wipeout-points mask pts)
            (progn
              (tbkg:set-mask-info mask tag txt)
              T)
            nil))
        nil))
    nil))

(defun tbkg:codeswitch-insert (insert-ent / insert-data blkname blkrec code-attr mantel-attr code-def mantel-def attrs-ok defs-ok)
  ;; Wissel de zwevende code en mantelbuistekst. Omdat elk geplaatst KabelGeul-
  ;; block uniek is, mogen ook de ATTDEF-posities en block-wipeouts worden aangepast.
  (if (and insert-ent (= (cdr (assoc 0 (entget insert-ent))) "INSERT"))
    (progn
      (setq insert-data (entget insert-ent)
            blkname (cdr (assoc 2 insert-data))
            blkrec (if blkname (tblobjname "BLOCK" blkname) nil)
            code-attr (tbkg:attr-ent insert-ent "ZWEVEND_CODE")
            mantel-attr (tbkg:attr-ent insert-ent "MANTEL_SPEC")
            code-def (tbkg:block-attdef-ent blkrec "ZWEVEND_CODE")
            mantel-def (tbkg:block-attdef-ent blkrec "MANTEL_SPEC"))
      (if (and code-attr mantel-attr code-def mantel-def)
        (progn
          (setq attrs-ok (tbkg:swap-text-placement code-attr mantel-attr)
                defs-ok (tbkg:swap-text-placement code-def mantel-def))
          (if (and attrs-ok defs-ok)
            (progn
              (tbkg:refresh-text-mask-for-tag insert-ent "ZWEVEND_CODE")
              (tbkg:refresh-text-mask-for-tag insert-ent "MANTEL_SPEC")
              (entupd insert-ent)
              (vl-catch-all-apply
                'vla-Regen
                (list (vla-get-ActiveDocument (vlax-get-acad-object)) 1))
              T)
            nil))
        nil))
    nil))

(defun tbkg:update-all-text-wipeouts (/ ss i insert-ent data blkname changed)
  (tbkg:hide-wipeout-frames)
  (setq changed 0)
  (setq ss (ssget "_X" '((0 . "INSERT") (66 . 1))))
  (if ss
    (progn
      (setq i 0)
      (while (< i (sslength ss))
        (setq insert-ent (ssname ss i))
        (setq data (entget insert-ent))
        (setq blkname (cdr (assoc 2 data)))
        (if (tbkg:starts-with (strcase (if blkname blkname "")) "KABELGEUL_")
          (setq changed (+ changed (tbkg:update-text-wipeouts-for-insert insert-ent))))
        (setq i (1+ i)))))
  (if (> changed 0)
    (vl-catch-all-apply
      'vla-Regen
      (list (vla-get-ActiveDocument (vlax-get-acad-object)) 1)))
  changed)

(defun tbkg:mask-command-ended (reactor params / cmd)
  (setq cmd (strcase (if (and params (car params)) (car params) "")))
  (if (member cmd '("EATTEDIT" "ATTEDIT" "-ATTEDIT" "DDEDIT"))
    (vl-catch-all-apply 'tbkg:update-all-text-wipeouts '()))
  (princ))

(defun tbkg:install-mask-reactor (/ result)
  (if (and (boundp '*tbkg:mask-command-reactor*) *tbkg:mask-command-reactor*)
    (vl-catch-all-apply 'vlr-remove (list *tbkg:mask-command-reactor*)))
  (setq result
    (vl-catch-all-apply
      'vlr-command-reactor
      (list nil (list (cons :vlr-commandEnded 'tbkg:mask-command-ended)))))
  (if (vl-catch-all-error-p result)
    (setq *tbkg:mask-command-reactor* nil)
    (setq *tbkg:mask-command-reactor* result)))

(defun tbkg:block-cross-angle (blkrec / next data pts)
  (setq next (entnext blkrec))
  (while (and next (null pts))
    (setq data (entget next))
    (cond
      ((and (= (cdr (assoc 0 data)) "LWPOLYLINE") (= (cdr (assoc 8 data)) "E_LV_CROSS_SECTION"))
       (setq pts (tbkg:lwpolyline-points data))
       (if (/= (length pts) 6) (setq pts nil)))
      ((= (cdr (assoc 0 data)) "ENDBLK") (setq next nil)))
    (if (and next (null pts)) (setq next (entnext next))))
  (if pts (angle (car pts) (cadr pts)) 0.0))

(defun tbkg:world->block (insert-ent pt / data base rot sx sy dx dy)
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

(defun tbkg:block->world (insert-ent pt / data base rot sx sy x y)
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

(defun tbkg:nearest-block-circle (blkrec pt / next data cen dist best bestdist)
  (setq next (entnext blkrec))
  (while next
    (setq data (entget next))
    (cond
      ((= (cdr (assoc 0 data)) "CIRCLE")
       (setq cen (cdr (assoc 10 data)))
       (setq dist (distance (tbkg:pt2 pt) (tbkg:pt2 cen)))
       (if (or (null bestdist) (< dist bestdist))
         (setq best next bestdist dist)))
      ((= (cdr (assoc 0 data)) "ENDBLK")
       (setq next nil)))
    (if next
      (setq next (entnext next))))
  best)

(defun tbkg:delete-rooien-lines-near (blkrec center / next data p1 p2 mid todel)
  (setq next (entnext blkrec))
  (while next
    (setq data (entget next))
    (cond
      ((= (cdr (assoc 0 data)) "LINE")
       (setq p1 (cdr (assoc 10 data)))
       (setq p2 (cdr (assoc 11 data)))
       (setq mid (tbkg:midpoint p1 p2))
       (if (< (distance (tbkg:pt2 mid) (tbkg:pt2 center)) 0.05)
         (setq todel (cons next todel))))
      ((= (cdr (assoc 0 data)) "ENDBLK")
       (setq next nil)))
    (if next
      (setq next (entnext next))))
  (foreach next todel
    (entdel next)))

(defun tbkg:block-make-line (blkrec p1 p2 layer linetype)
  (entmakex
    (list '(0 . "LINE")
          (cons 330 blkrec)
          '(100 . "AcDbEntity")
          (cons 8 layer)
          (cons 6 linetype)
          '(100 . "AcDbLine")
          (cons 10 (tbkg:pt3 p1))
          (cons 11 (tbkg:pt3 p2)))))

(defun tbkg:sync-icon-type (blkrec circle-ent typ ang combi-meta / layer data center r ext combi)
  (setq layer (tbkg:type->layer typ))
  (setq data (entget circle-ent))
  (setq center (cdr (assoc 10 data)))
  (setq r (if (assoc 40 data) (cdr (assoc 40 data)) 0.25))
  (setq combi (if combi-meta (tbkg:double-p (cadr combi-meta)) (> r 0.27)))
  (setq r (if (or combi (tbkg:ms-type-p typ)) 0.30 0.25))
  (tbkg:update-ent-codes circle-ent (list (cons 8 layer) (cons 6 "Continuous") (cons 40 r)))
  (tbkg:delete-rooien-lines-near blkrec center)
  (cond
     ((and combi (or (= typ "Rooien") (= typ "MS Rooien")))
     (setq ext (* 2.0 r))
     (tbkg:block-make-line blkrec (tbkg:p+ center (+ ang (* 0.25 pi)) ext) (tbkg:p+ center (+ ang (* 1.25 pi)) ext) layer "DASHDOT")
     (tbkg:block-make-line blkrec (tbkg:p+ center (+ ang (* 0.75 pi)) ext) (tbkg:p+ center (+ ang (* 1.75 pi)) ext) layer "DASHDOT")
     ;; De doorlopende Combi-streep blijft aanwezig en draait bij rooien
     ;; 15 graden linksom, zodat hij niet samenvalt met het rooikruis.
     (tbkg:block-make-line
       blkrec
       (tbkg:p+ center (+ ang (* 0.25 pi) (/ pi 12.0)) r)
       (tbkg:p+ center (+ ang (* 1.25 pi) (/ pi 12.0)) r)
       layer
       "Continuous"))
    (combi
     (tbkg:block-make-line
       blkrec
       (tbkg:p+ center (+ ang (* 0.25 pi)) r)
       (tbkg:p+ center (+ ang (* 1.25 pi)) r)
       layer
       "Continuous"))
     ((or (= typ "Rooien") (= typ "MS Rooien"))
     (progn
      (setq ext (* 2.0 r))
      (tbkg:block-make-line blkrec (tbkg:p+ center (+ ang (* 0.25 pi)) ext) (tbkg:p+ center (+ ang (* 1.25 pi)) ext) layer "DASHDOT")
      (tbkg:block-make-line blkrec (tbkg:p+ center (+ ang (* 0.75 pi)) ext) (tbkg:p+ center (+ ang (* 1.75 pi)) ext) layer "DASHDOT")))))

(defun tbkg:sync-insert-from-attrs (insert-ent / data blkname blkrec ang i dir-ent text-ent dir-data text-data typ layer raw label attdef circle local-pt combi-meta)
  (if (and insert-ent (= (cdr (assoc 0 (entget insert-ent))) "INSERT"))
    (progn
      (setq data (entget insert-ent))
      (setq blkname (cdr (assoc 2 data)))
      (setq blkrec (tblobjname "BLOCK" blkname))
      (if blkrec
        (progn
           (setq ang (tbkg:block-cross-angle blkrec))
           (setq i 1)
           (setq text-ent (tbkg:attr-ent-by-index insert-ent "KABEL" i))
           (while text-ent
             (setq dir-ent (tbkg:attr-ent-by-index insert-ent "RICHTING" i))
            (if (and dir-ent text-ent)
              (progn
                (setq dir-data (entget dir-ent))
                (setq text-data (entget text-ent))
                (setq typ (tbkg:normalize-type (cdr (assoc 1 dir-data))))
                 (if typ
                   (progn
                     (setq combi-meta (tbkg:block-option-metadata blkrec "COMBI" i))
                     (setq layer (tbkg:type->text-layer typ))
                    (setq raw (cdr (assoc 1 text-data)))
                    (setq label (tbkg:prefixed-label typ (tbkg:strip-known-prefix raw)))
                    (tbkg:update-ent-codes dir-ent (list (cons 1 typ) (cons 8 "Opmerking")))
                    (tbkg:update-ent-codes text-ent (list (cons 1 label) (cons 8 layer)))
                    (setq attdef (tbkg:block-attdef-ent blkrec (tbkg:index-tag "RICHTING" i)))
                    (if attdef
                      (tbkg:update-ent-codes attdef (list (cons 1 typ) (cons 8 "Opmerking"))))
                    (setq attdef (tbkg:block-attdef-ent blkrec (tbkg:index-tag "KABEL" i)))
                    (if attdef
                      (tbkg:update-ent-codes attdef (list (cons 1 label) (cons 8 layer))))
                     (setq local-pt (tbkg:world->block insert-ent (tbkg:attribute-point text-data)))
                     (setq circle (tbkg:nearest-block-circle blkrec local-pt))
                     (if circle
                       (tbkg:sync-icon-type blkrec circle typ ang combi-meta))))))
             (setq i (1+ i))
             (setq text-ent (tbkg:attr-ent-by-index insert-ent "KABEL" i)))
           (entupd insert-ent))))))

(defun tbkg:draw-mantelbuis (pt)
  (tbkg:make-circle pt *tbkg:mantelbuis-radius* "Mantelbuis"))

(defun tbkg:draw-mantelbuis-radius (pt rad)
  (tbkg:make-circle pt rad "Mantelbuis"))

(defun tbkg:arc-points (center rad a1 a2 steps / i pts a)
  (setq i 0 pts '())
  (while (<= i steps)
    (setq a (+ a1 (* (/ (- a2 a1) (* 1.0 steps)) i)))
    (setq pts (append pts (list (polar center a rad))))
    (setq i (1+ i)))
  pts)

(defun tbkg:draw-mantelbuis-capsule (pts ang r / base unit minp maxp p pos c1 c2 outline)
  (if (null r) (setq r *tbkg:mantelbuis-radius*))
  (if (= (length pts) 1)
    (tbkg:draw-mantelbuis-radius (car pts) r)
    (progn
      (setq base (car pts))
      (setq unit (list (cos ang) (sin ang)))
      (setq minp nil maxp nil)
      (foreach p pts
        (setq pos (tbkg:dot2 (tbkg:vector2 base p) unit))
        (if (or (null minp) (< pos minp)) (setq minp pos))
        (if (or (null maxp) (> pos maxp)) (setq maxp pos)))
      (if (< (abs (- maxp minp)) 1e-8)
        (tbkg:draw-mantelbuis-radius base r)
        (progn
          (setq c1 (polar base ang minp))
          (setq c2 (polar base ang maxp))
          (setq outline
            (append
              (tbkg:arc-points c2 r (+ ang (/ pi 2.0)) (- ang (/ pi 2.0)) 8)
              (tbkg:arc-points c1 r (- ang (/ pi 2.0)) (- ang (* 1.5 pi)) 8)))
          (tbkg:make-lwpoly outline "Mantelbuis" T))))))

(defun tbkg:item-mantel-radius (item)
  ;; Enkel, Dubbel en Drie gebruiken exact dezelfde mantelbuismaat.
  *tbkg:mantelbuis-radius*)

(defun tbkg:draw-mantelbuis-items (items ang / item key pt pt2 rad entry groups singles group twosided)
  (setq groups '() singles '())
  (foreach item items
    (setq key (tbkg:mantel-group-key (nth 3 item)))
    (setq pt (nth 4 item))
    (setq pt2 (nth 11 item))
    (setq twosided (tbkg:double-p (nth 10 item)))
    (setq rad (tbkg:item-mantel-radius item))
    (cond
      ;; Twee-zijdig krijgt altijd twee afzonderlijke mantelbuiscirkels.
      ;; Zo blijft tussen beide symbolen een duidelijke vrije ruimte zichtbaar.
      ((and twosided (tbkg:mantel-p (nth 3 item)))
       (setq singles (cons (list pt rad) singles))
       (if (tbkg:point-p pt2)
         (setq singles (cons (list pt2 rad) singles))))
      (key
       (setq entry (assoc key groups))
       (if entry
         (setq groups
           (subst
             (cons key (cons (max rad (cadr entry)) (cons pt (cddr entry))))
             entry groups))
         (setq groups (cons (list key rad pt) groups))))
      ((tbkg:mantel-p (nth 3 item))
       (setq singles (cons (list pt rad) singles)))))
  (foreach entry singles
    (tbkg:draw-mantelbuis-radius (car entry) (cadr entry)))
  (foreach group groups
    (tbkg:draw-mantelbuis-capsule (reverse (cddr group)) ang (cadr group))))

(defun tbkg:draw-combi-logo (pt ang layer / r)
  (setq r 0.30)
  (tbkg:make-circle pt r layer)
  (tbkg:draw-combi-line pt ang layer r))

(defun tbkg:draw-combi-line (pt ang layer r)
  (tbkg:make-line
    (tbkg:p+ pt (+ ang (* 0.25 pi)) r)
    (tbkg:p+ pt (+ ang (* 1.25 pi)) r)
    layer))

(defun tbkg:draw-combi-rooien-line (pt ang layer r)
  ;; Bij te rooien 15 graden linksom ten opzichte van de normale Combi-streep.
  (tbkg:make-line
    (tbkg:p+ pt (+ ang (* 0.25 pi) (/ pi 12.0)) r)
    (tbkg:p+ pt (+ ang (* 1.25 pi) (/ pi 12.0)) r)
    layer))

(defun tbkg:half-rooien-p (typ)
  (= typ "Half rooien"))

(defun tbkg:ms-type-p (typ)
  (member typ '("MS Nieuw" "MS Bestaand" "MS Rooien")))

(defun tbkg:draw-single-cable-icon (pt ang typ layer combi)
  (cond
    ((or (= typ "Rooien") (= typ "MS Rooien"))
     (tbkg:draw-rooien-logo-radius
       pt ang layer
       (if (or (tbkg:double-p combi) (tbkg:ms-type-p typ)) 0.30 0.25))
     (if (tbkg:double-p combi)
       (tbkg:draw-combi-rooien-line pt ang layer 0.30)))
    ((tbkg:double-p combi)
     (tbkg:draw-combi-logo pt ang layer))
    (T
     (tbkg:make-circle pt (if (tbkg:ms-type-p typ) 0.30 0.25) layer))))

(defun tbkg:draw-rooien-span-line (p1 p2 layer)
  (tbkg:track-ent
    (entmakex
      (list '(0 . "LINE")
            '(100 . "AcDbEntity")
            (cons 8 layer)
            '(6 . "DASHDOT")
            '(100 . "AcDbLine")
            (cons 10 (tbkg:pt3 p1))
            (cons 11 (tbkg:pt3 p2))))))

(defun tbkg:draw-rooien-logo-radius (pt ang layer r / ext)
  (setq ext (* 2.0 r))
  (tbkg:make-circle pt r layer)
  (tbkg:draw-rooien-span-line
    (tbkg:p+ pt (+ ang (* 0.25 pi)) ext)
    (tbkg:p+ pt (+ ang (* 1.25 pi)) ext)
    layer)
  (tbkg:draw-rooien-span-line
    (tbkg:p+ pt (+ ang (* 0.75 pi)) ext)
    (tbkg:p+ pt (+ ang (* 1.75 pi)) ext)
    layer))

(defun tbkg:draw-double-rooien-logo (p1 p2 ang layer combi typ / r ext out1 out2 a1 a2 b1 b2)
  (setq r (if (or (tbkg:double-p combi) (tbkg:ms-type-p typ)) 0.30 0.25))
  (setq ext (* 2.0 r))
  (setq out1 (- ang (/ pi 2.0)))
  (setq out2 (+ ang (/ pi 2.0)))
  (setq a1 (tbkg:p+ p1 (+ out1 (* 0.25 pi)) ext))
  (setq a2 (tbkg:p+ p1 (- out1 (* 0.25 pi)) ext))
  (setq b1 (tbkg:p+ p2 (+ out2 (* 0.25 pi)) ext))
  (setq b2 (tbkg:p+ p2 (- out2 (* 0.25 pi)) ext))
  (tbkg:make-circle p1 r layer)
  (tbkg:make-circle p2 r layer)
  (tbkg:draw-rooien-span-line a1 b1 layer)
  (tbkg:draw-rooien-span-line a2 b2 layer)
  (if (tbkg:double-p combi)
    (progn
      (tbkg:draw-combi-rooien-line p1 ang layer r)
      (tbkg:draw-combi-rooien-line p2 ang layer r))))

(defun tbkg:draw-cable-icon (pt geul-ang typ double triple mantel combi / layer double-offset p1 p2 is-double side bottom-y top-y p3)
  (setq typ (tbkg:normalize-type typ))
  (if (null typ) (setq typ "K01"))
  (setq layer (tbkg:type->layer typ))
  (setq double-offset 0.22)
  (setq is-double (or (tbkg:double-p double) (tbkg:half-rooien-p typ)))
  (cond
    ((and (tbkg:double-p triple) (not (tbkg:half-rooien-p typ)))
     ;; Twee licht overlappende cirkels onderin en de derde erboven.
     (setq side 0.21 bottom-y -0.12 top-y 0.24)
     (setq p1 (tbkg:local->world pt geul-ang (- side) bottom-y))
     (setq p2 (tbkg:local->world pt geul-ang side bottom-y))
     (setq p3 (tbkg:local->world pt geul-ang 0.0 top-y))
     (tbkg:draw-single-cable-icon p1 geul-ang typ layer combi)
     (tbkg:draw-single-cable-icon p2 geul-ang typ layer combi)
     (tbkg:draw-single-cable-icon p3 geul-ang typ layer combi))
    (is-double
     (setq p1 (polar pt (- geul-ang (/ pi 2.0)) double-offset))
     (setq p2 (polar pt (+ geul-ang (/ pi 2.0)) double-offset))
     (cond
       ((tbkg:half-rooien-p typ)
        (tbkg:make-circle p1 0.25 (tbkg:type->layer "Bestaand"))
        (tbkg:draw-rooien-logo p2 geul-ang (tbkg:type->layer "Rooien")))
        ((or (= typ "Rooien") (= typ "MS Rooien"))
         (tbkg:draw-double-rooien-logo p1 p2 geul-ang layer combi typ))
       (T
        (tbkg:draw-single-cable-icon p1 geul-ang typ layer combi)
        (tbkg:draw-single-cable-icon p2 geul-ang typ layer combi))))
    (T
     (tbkg:draw-single-cable-icon pt geul-ang typ layer combi))))

(defun tbkg:draw-cable-attributes (text-pt text-ang text-just typ txt attr-index / layer label tag)
  (setq typ (tbkg:normalize-type typ))
  (if (null typ) (setq typ "K01"))
  (if (null txt) (setq txt ""))
  (setq layer (tbkg:type->text-layer typ))
  (setq label (tbkg:prefixed-label typ txt))
  (setq tag (tbkg:index-tag "KABEL" attr-index))
  (tbkg:make-attribute text-pt 0.55 text-ang tag (strcat "Kabel/leiding tekst " (tbkg:index-string attr-index) ":") label layer text-just 0))

(defun tbkg:draw-bestaand-attribute (text-pt text-ang text-just txt attr-index / layer label tag)
  (if (null txt) (setq txt ""))
  (setq layer (tbkg:type->layer "Bestaand"))
  (setq label (tbkg:prefixed-label "Bestaand" txt))
  (setq tag (tbkg:index-tag "BESTAAND" attr-index))
  (tbkg:make-attribute text-pt 0.55 text-ang tag (strcat "Bestaande kabel tekst " (tbkg:index-string attr-index) ":") label layer text-just 0))

(defun tbkg:draw-side2-attribute (text-pt text-ang text-just typ txt attr-index / layer label tag)
  (setq typ (tbkg:normalize-type typ))
  (if (null typ) (setq typ "K01"))
  (if (null txt) (setq txt ""))
  (setq layer (tbkg:type->text-layer typ))
  (setq label (tbkg:prefixed-label typ txt))
  (setq tag (tbkg:index-tag "ZIJDE2" attr-index))
  (tbkg:make-attribute text-pt 0.55 text-ang tag
    (strcat "Kabel/leiding tekst zijde 2 " (tbkg:index-string attr-index) ":")
    label layer text-just 0))

(defun tbkg:half-rooien-text-points (cable-pt geul-ang text-pt text-ang / rooien-circle rooien-away rooien-just)
  (setq rooien-circle (polar cable-pt (+ geul-ang (/ pi 2.0)) 0.22))
  (setq rooien-away (+ (angle cable-pt text-pt) pi))
  (setq rooien-just
    (if (>= (cos (- rooien-away text-ang)) 0.0)
      0
      2))
  (list (tbkg:p+ rooien-circle rooien-away 0.65) text-pt rooien-just))

(defun tbkg:two-sided-text-points (primary-pt secondary-pt text-pt text-ang / away just primary-text-pt touch-distance)
  ;; De buitenste tekst is de tekst die in versie X aan de juiste zijde is gezet.
  ;; Plaats hem nu zo dicht bij de halve bak dat de rand van zijn wipeout exact
  ;; aansluit op de wipeout van de bak. De vaste bakhoogte en de vaste
  ;; kabelhartafstand worden samen gebruikt; het kabeltekstmasker loopt
  ;; 0.20 terug achter het invoegpunt.
  (setq away (+ (angle primary-pt text-pt) pi))
  (setq just (if (>= (cos (- away text-ang)) 0.0) 0 2))
  (setq touch-distance
    (+ (/ (- *tbkg:bakhoogte* *tbkg:twosided-center-sep*) 2.0) 0.20))
  (setq primary-text-pt (tbkg:p+ primary-pt away touch-distance))

  ;; De andere tekst blijft op het normale tekstpunt onder de bak staan.
  (list primary-text-pt text-pt just))

(defun tbkg:draw-split-attribute (pt ang just split attr-index / tag val)
  (setq tag (tbkg:index-tag "VOLG" attr-index))
  (setq val (if split "1" "0"))
  ;; Onzichtbaar + constant: metadata blijft in het unieke block zonder extra invoerveld.
  (tbkg:make-attribute pt 0.10 ang tag "Volgende kabel begint een nieuwe geulbak:" val "E_LV_CROSS_SECTION" just 3))

(defun tbkg:draw-copy-option-attributes (pt ang just double triple mantel combi twosided side2double side2triple side2combi attr-index / mantel-val)
  ;; Bewaar de kabelvorm van beide zijden expliciet. Daardoor blijven de
  ;; onafhankelijke keuzes ook behouden bij KGOMNICOPY.
  (setq mantel-val (tbkg:mantel-value mantel))
  (tbkg:make-attribute pt 0.10 ang (tbkg:index-tag "DUBBEL" attr-index)
    "Dubbele kabel zijde 1:" (if (tbkg:double-p double) "1" "0") "E_LV_CROSS_SECTION" just 3)
  (tbkg:make-attribute pt 0.10 ang (tbkg:index-tag "DRIE" attr-index)
    "Drie kabelcirkels zijde 1:" (if (tbkg:double-p triple) "1" "0") "E_LV_CROSS_SECTION" just 3)
  (tbkg:make-attribute pt 0.10 ang (tbkg:index-tag "COMBI" attr-index)
    "Combi zijde 1:" (if (tbkg:double-p combi) "1" "0") "E_LV_CROSS_SECTION" just 3)
  (tbkg:make-attribute pt 0.10 ang (tbkg:index-tag "DUBBEL2" attr-index)
    "Dubbele kabel zijde 2:" (if (tbkg:double-p side2double) "1" "0") "E_LV_CROSS_SECTION" just 3)
  (tbkg:make-attribute pt 0.10 ang (tbkg:index-tag "DRIE2" attr-index)
    "Drie kabelcirkels zijde 2:" (if (tbkg:double-p side2triple) "1" "0") "E_LV_CROSS_SECTION" just 3)
  (tbkg:make-attribute pt 0.10 ang (tbkg:index-tag "COMBI2" attr-index)
    "Combi zijde 2:" (if (tbkg:double-p side2combi) "1" "0") "E_LV_CROSS_SECTION" just 3)
  (tbkg:make-attribute pt 0.10 ang (tbkg:index-tag "MANTEL" attr-index)
    "Mantelbuis:" (if mantel-val mantel-val "") "E_LV_CROSS_SECTION" just 3)
  (tbkg:make-attribute pt 0.10 ang (tbkg:index-tag "TWEEZIJDIG" attr-index)
    "Twee-zijdige kabel:" (if (tbkg:double-p twosided) "1" "0") "E_LV_CROSS_SECTION" just 3))

(defun tbkg:draw-cable (pt text-pt geul-ang text-ang text-just typ txt double triple mantel attr-index /)
  (tbkg:draw-cable-icon pt geul-ang typ double triple mantel nil)
  (tbkg:draw-cable-attributes text-pt text-ang text-just typ txt attr-index))

(defun tbkg:clamp-int (val minv maxv)
  (if (< val minv) (setq val minv))
  (if (> val maxv) (setq val maxv))
  val)

(defun tbkg:layer-direction-type (upper / n key result)
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

(defun tbkg:layer->type (layer / upper num dir)
  (setq upper (strcase (if layer layer "")))
  (cond
    ((wcmatch upper "*MS NIEUW*") "MS Nieuw")
    ((wcmatch upper "*MS BESTAAND*") "MS Bestaand")
    ((wcmatch upper "*MS ROOIEN*") "MS Rooien")
    ((wcmatch upper "*ROOIEN*") "Rooien")
    ((wcmatch upper "*BESTAAND*") "Bestaand")
    ((setq dir (tbkg:layer-direction-type upper)) dir)
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

(defun tbkg:cross2 (a b)
  (- (* (car a) (cadr b)) (* (cadr a) (car b))))

(defun tbkg:sub2 (a b)
  (list (- (car a) (car b)) (- (cadr a) (cadr b))))

(defun tbkg:point2 (p)
  (list (car p) (cadr p)))

(defun tbkg:between (v lo hi eps)
  (and (>= v (- lo eps)) (<= v (+ hi eps))))

(defun tbkg:segment-intersection-t (p1 p2 q1 q2 / r s den qp t u eps)
  (setq eps 1e-8)
  (setq p1 (tbkg:point2 p1))
  (setq p2 (tbkg:point2 p2))
  (setq q1 (tbkg:point2 q1))
  (setq q2 (tbkg:point2 q2))
  (setq r (tbkg:sub2 p2 p1))
  (setq s (tbkg:sub2 q2 q1))
  (setq den (tbkg:cross2 r s))
  (if (> (abs den) eps)
    (progn
      (setq qp (tbkg:sub2 q1 p1))
      (setq t (/ (tbkg:cross2 qp s) den))
      (setq u (/ (tbkg:cross2 qp r) den))
      (if (and (tbkg:between t 0.0 1.0 eps)
               (tbkg:between u 0.0 1.0 eps))
        t
        nil))
    nil))

(defun tbkg:lwpolyline-points (data / pts)
  (setq pts '())
  (foreach pair data
    (if (and (listp pair)
             (= (car pair) 10)
             (tbkg:point-p (cdr pair)))
      (setq pts (append pts (list (cdr pair))))))
  pts)

(defun tbkg:old-polyline-points (ent / pts next data)
  (setq pts '())
  (setq next (entnext ent))
  (while next
    (setq data (entget next))
    (cond
      ((= (cdr (assoc 0 data)) "VERTEX")
       (if (tbkg:point-p (cdr (assoc 10 data)))
         (setq pts (append pts (list (cdr (assoc 10 data)))))))
      ((= (cdr (assoc 0 data)) "SEQEND")
       (setq next nil)))
    (if next
      (setq next (entnext next))))
  pts)

(defun tbkg:polyline-points (ent data)
  (cond
    ((= (cdr (assoc 0 data)) "LWPOLYLINE")
     (tbkg:lwpolyline-points data))
    ((= (cdr (assoc 0 data)) "POLYLINE")
     (tbkg:old-polyline-points ent))
    (T nil)))

(defun tbkg:line-points (data / p1 p2)
  (setq p1 (cdr (assoc 10 data)))
  (setq p2 (cdr (assoc 11 data)))
  (if (and p1 p2)
    (list p1 p2)
    nil))

(defun tbkg:sample-curve-points (ent / sp ep steps i param pt pts)
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

(defun tbkg:entity-points (ent data)
  (cond
    ((member (cdr (assoc 0 data)) '("LWPOLYLINE" "POLYLINE"))
     (tbkg:polyline-points ent data))
    ((= (cdr (assoc 0 data)) "LINE")
     (tbkg:line-points data))
    ((member (cdr (assoc 0 data)) '("ARC" "CIRCLE" "ELLIPSE" "SPLINE"))
     (tbkg:sample-curve-points ent))
    (T nil)))

(defun tbkg:polyline-closed-p (data)
  (= 1 (logand 1 (if (assoc 70 data) (cdr (assoc 70 data)) 0))))

(defun tbkg:polyline-cross-t (p1 p2 pts closed / best a b t rest)
  (setq best nil)
  (setq rest pts)
  (while (and rest (cdr rest))
    (setq a (car rest))
    (setq b (cadr rest))
    (setq t (tbkg:segment-intersection-t p1 p2 a b))
    (if (and t (or (null best) (< t best)))
      (setq best t))
    (setq rest (cdr rest)))
  (if (and closed (> (length pts) 2))
    (progn
      (setq t (tbkg:segment-intersection-t p1 p2 (car (last pts)) (car pts)))
      (if (and t (or (null best) (< t best)))
        (setq best t))))
  best)

(defun tbkg:same-entity-p (a b)
  (and a b (= (vl-princ-to-string a) (vl-princ-to-string b))))

(defun tbkg:crossed-cable-types-raw (p1 p2 exclude / ss idx ent data layer typ pts t items)
  (setq items '())
  (setq ss (ssget "_X" '((0 . "LWPOLYLINE,POLYLINE,LINE,ARC,CIRCLE,ELLIPSE,SPLINE"))))
  (if ss
    (progn
      (setq idx 0)
      (while (< idx (sslength ss))
        (setq ent (ssname ss idx))
        (if (not (tbkg:same-entity-p ent exclude))
          (progn
            (setq data (entget ent))
            (setq layer (cdr (assoc 8 data)))
            (setq typ (tbkg:layer->type layer))
            (if typ
              (progn
                (setq pts (tbkg:entity-points ent data))
                (if (> (length pts) 1)
                  (progn
                    (setq t (tbkg:polyline-cross-t p1 p2 pts (tbkg:polyline-closed-p data)))
                    (if t
                      (setq items (cons (list t typ) items)))))))))
        (setq idx (1+ idx)))))
  (mapcar 'cadr
    (vl-sort items
      '(lambda (a b) (< (car a) (car b))))))

(defun tbkg:crossed-cable-types (p1 p2 exclude / result)
  (setq result
    (vl-catch-all-apply
      'tbkg:crossed-cable-types-raw
      (list p1 p2 exclude)))
  (if (vl-catch-all-error-p result)
    nil
    result))

(defun tbkg:fence-cable-types-raw (p1 p2 exclude / ss idx ent data layer typ items)
  (setq items '())
  (setq ss (ssget "_F" (list p1 p2) '((0 . "LWPOLYLINE,POLYLINE,LINE,ARC,CIRCLE,ELLIPSE,SPLINE"))))
  (if ss
    (progn
      (setq idx 0)
      (while (< idx (sslength ss))
        (setq ent (ssname ss idx))
        (if (not (tbkg:same-entity-p ent exclude))
          (progn
            (setq data (entget ent))
            (setq layer (cdr (assoc 8 data)))
            (setq typ (tbkg:layer->type layer))
            (if typ
              (setq items (append items (list typ))))))
        (setq idx (1+ idx)))))
  items)

(defun tbkg:fence-cable-types (p1 p2 exclude / result)
  (setq result
    (vl-catch-all-apply
      'tbkg:fence-cable-types-raw
      (list p1 p2 exclude)))
  (if (vl-catch-all-error-p result)
    nil
    result))

(defun tbkg:count-crossed-polylines-raw (p1 p2 / ss idx ent data layer cnt)
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

(defun tbkg:count-crossed-polylines (p1 p2 / result)
  (setq result
    (vl-catch-all-apply
      'tbkg:count-crossed-polylines-raw
      (list p1 p2)))
  (if (vl-catch-all-error-p result)
    1
    result))

(defun tbkg:type-index (typ / i found)
  (setq i 0 found 0)
  (foreach opt *tbkg:type-options*
    (if (= opt typ) (setq found i))
    (setq i (1+ i)))
  found)

(defun tbkg:popup-fill (key value)
  (start_list key)
  (mapcar 'add_list *tbkg:type-options*)
  (end_list)
  (set_tile key (itoa (tbkg:type-index value))))

(defun tbkg:shape-index (double triple combi)
  ;; De vier vormen zijn in de nieuwe GUI bewust onderling exclusief.
  (cond
    ((tbkg:double-p combi) 3)
    ((tbkg:double-p triple) 2)
    ((tbkg:double-p double) 1)
    (T 0)))

(defun tbkg:shape-flags (index)
  (cond
    ((= index 1) (list T nil nil))
    ((= index 2) (list nil T nil))
    ((= index 3) (list nil nil T))
    (T (list nil nil nil))))

(defun tbkg:shape-popup-fill (key double triple combi)
  (start_list key)
  (mapcar 'add_list *tbkg:shape-options*)
  (end_list)
  (set_tile key (itoa (tbkg:shape-index double triple combi))))

(defun tbkg:dialog-row-controls (i active / typ twosided half state)
  (setq state (if active 0 1))
  (setq typ (nth (atoi (get_tile (strcat "type" (itoa i)))) *tbkg:type-options*))
  (if (null typ) (setq typ "K01"))
  (setq half (tbkg:half-rooien-p typ))
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

(defun tbkg:dialog-type-changed (i / count)
  (setq count (max 1 (atoi (get_tile "count"))))
  (tbkg:dialog-row-controls i (<= i count)))

(defun tbkg:dialog-double-changed (i)
  ;; Compatibele hulpfunctie voor oudere geladen DCL-definities.
  (tbkg:dialog-type-changed i))

(defun tbkg:dialog-triple-changed (i)
  ;; Compatibele hulpfunctie voor oudere geladen DCL-definities.
  (tbkg:dialog-type-changed i))

(defun tbkg:dialog-twosided-changed (i)
  (tbkg:dialog-type-changed i))

(defun tbkg:update-row-state (count / i row-count)
  (setq row-count (max 1 *tbkg:dialog-row-count*))
  (setq i 1)
  (while (<= i row-count)
    (tbkg:dialog-row-controls i (<= i count))
    (setq i (1+ i))))

(defun tbkg:dialog-row-data (i / type-idx typ label bestaand shape1 shape2 double triple combi side2double side2triple side2combi mantel split twosided)
  (setq type-idx (atoi (get_tile (strcat "type" (itoa i)))))
  (setq typ (nth type-idx *tbkg:type-options*))
  (if (null typ) (setq typ "K01"))
  (setq label (get_tile (strcat "label" (itoa i))))
  (setq bestaand (get_tile (strcat "bestaand" (itoa i))))
  (setq twosided (tbkg:double-p (get_tile (strcat "twosided" (itoa i)))))
  (setq shape1 (tbkg:shape-flags (atoi (get_tile (strcat "vorm1" (itoa i))))))
  (setq double (car shape1)
        triple (cadr shape1)
        combi (caddr shape1))
  (setq shape2 (tbkg:shape-flags (atoi (get_tile (strcat "vorm2" (itoa i))))))
  (setq side2double (car shape2)
        side2triple (cadr shape2)
        side2combi (caddr shape2))
  (if (tbkg:half-rooien-p typ)
    (setq double T triple nil combi nil twosided nil
          side2double nil side2triple nil side2combi nil))
  (if (not twosided)
    (setq side2double nil side2triple nil side2combi nil))
  (setq mantel (tbkg:mantel-value (get_tile (strcat "mantel" (itoa i)))))
  (setq split (tbkg:double-p (get_tile (strcat "split" (itoa i)))))
  (list typ label bestaand double triple combi mantel split twosided
        side2double side2triple side2combi))

(defun tbkg:dialog-rows (count / i rows)
  (setq rows '() i 1)
  (while (<= i count)
    (setq rows (append rows (list (tbkg:dialog-row-data i))))
    (setq i (1+ i)))
  rows)

(defun tbkg:blank-dialog-row ()
  (list "K01" "" "" nil nil nil nil nil nil nil nil nil))

(defun tbkg:dialog-result-from-rows (rows mantel-spec / row types labels bestaande-labels doubles triples combis mantels splits twosideds side2doubles side2triples side2combis)
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

(defun tbkg:list-remove-at (lst index / out i item)
  (setq out '() i 1)
  (foreach item lst
    (if (/= i index) (setq out (append out (list item))))
    (setq i (1+ i)))
  out)

(defun tbkg:list-insert-at (lst index item / out i inserted old)
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

(defun tbkg:dialog-collect (/ count row-count collect-count rows)
  (setq count (max 1 (atoi (get_tile "count"))))
  (setq row-count (max 1 *tbkg:dialog-row-count*))
  (setq collect-count (min count row-count))
  (setq rows (tbkg:dialog-rows collect-count))
  (while (< (length rows) count)
    (setq rows (append rows (list (tbkg:blank-dialog-row)))))
  (setq *tbkg:dialog-resize-count* (if (/= count row-count) count nil))
  (setq *tbkg:dialog-result*
    (tbkg:dialog-result-from-rows rows (get_tile "mantelspec"))))

(defun tbkg:dialog-remove-row (remove-index / count rows)
  (setq count (min (max 1 (atoi (get_tile "count"))) (max 1 *tbkg:dialog-row-count*)))
  (if (> count 1)
    (progn
      (setq rows (tbkg:list-remove-at (tbkg:dialog-rows count) remove-index))
      (setq *tbkg:dialog-resize-count* (length rows))
      (setq *tbkg:dialog-result*
        (tbkg:dialog-result-from-rows rows (get_tile "mantelspec")))
      (done_dialog 2))))

(defun tbkg:dialog-reverse-order (/ count rows)
  (setq count (min (max 1 (atoi (get_tile "count"))) (max 1 *tbkg:dialog-row-count*)))
  (setq rows (reverse (tbkg:dialog-rows count)))
  (setq *tbkg:dialog-resize-count* count)
  (setq *tbkg:dialog-result*
    (tbkg:dialog-result-from-rows rows (get_tile "mantelspec")))
  (done_dialog 2))

(defun tbkg:dialog-insert-row (insert-index / count rows)
  (setq count (min (max 1 (atoi (get_tile "count"))) (max 1 *tbkg:dialog-row-count*)))
  (setq rows (tbkg:list-insert-at (tbkg:dialog-rows count) insert-index (tbkg:blank-dialog-row)))
  (setq *tbkg:dialog-resize-count* (length rows))
  (setq *tbkg:dialog-result*
    (tbkg:dialog-result-from-rows rows (get_tile "mantelspec")))
  (done_dialog 2))

(defun tbkg:blank-labels (count / vals)
  (setq vals '())
  (while (> count 0) (setq vals (append vals (list "")) count (1- count)))
  vals)

(defun tbkg:blank-existing-labels (count / vals)
  (tbkg:blank-labels count))

(defun tbkg:blank-doubles (count / vals)
  (setq vals '())
  (while (> count 0) (setq vals (append vals (list nil)) count (1- count)))
  vals)

(defun tbkg:blank-triples (count / vals)
  (tbkg:blank-doubles count))

(defun tbkg:blank-twosideds (count / vals)
  (tbkg:blank-doubles count))

(defun tbkg:blank-mantels (count / vals)
  (tbkg:blank-doubles count))

(defun tbkg:blank-splits (count / vals)
  (tbkg:blank-doubles count))

(defun tbkg:blank-combis (count / vals)
  (tbkg:blank-doubles count))

(defun tbkg:double-p (val)
  (or (= val T) (= val 1) (= val "1")))

(defun tbkg:mantel-p (val)
  (and val
       (/= (tbkg:trim (vl-princ-to-string val)) "")
       (/= (tbkg:trim (vl-princ-to-string val)) "0")))

(defun tbkg:mantel-value (val / out)
  (setq out (tbkg:trim (if val (vl-princ-to-string val) "")))
  (if (or (= out "") (= out "0")) nil out))

(defun tbkg:mantel-individual-p (val / key)
  (setq key (strcase (tbkg:trim (if val (vl-princ-to-string val) ""))))
  (member key '("X" "J" "JA" "Y" "YES" "T" "TRUE")))

(defun tbkg:mantel-group-key (val / key)
  (setq key (tbkg:mantel-value val))
  (if (and key (not (tbkg:mantel-individual-p key))) (strcase key) nil))

(defun tbkg:dcl-header-line ()
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

(defun tbkg:dcl-row-line (i)
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

(defun tbkg:dcl-lines (/ lines i row-count)
  (setq row-count (max 1 *tbkg:dialog-row-count*))
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
  (setq lines (append lines (list (tbkg:dcl-header-line))))
  (setq i 1)
  (while (<= i row-count)
    (setq lines (append lines (list (tbkg:dcl-row-line i))))
    (setq i (1+ i)))
  (append lines '("  }" "  ok_cancel;" "}")))

(defun tbkg:write-temp-dcl (/ path file)
  (setq path (strcat (getvar "TEMPPREFIX") "KabelGeul_Omni.dcl"))
  (setq file (open path "w"))
  (if file
    (progn
      (foreach line (tbkg:dcl-lines) (write-line line file))
      (close file)
      path)
    nil))

(defun tbkg:dcl-path (/ same-dir support-path temp-path)
  (setq same-dir (if *tbkg:lsp-dir* (strcat *tbkg:lsp-dir* "\\KabelGeul_Omni.dcl") nil))
  (setq support-path (findfile "KabelGeul_Omni.dcl"))
  (setq temp-path (tbkg:write-temp-dcl))
  (cond
    ((and temp-path (findfile temp-path)) temp-path)
    ((and same-dir (findfile same-dir)) same-dir)
    (support-path support-path)
    (T nil)))

(defun tbkg:show-dialog (/ result layer typ)
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
  result)

(defun tbkg:draw-rooien-logo (pt ang layer / r ext)
  (tbkg:draw-rooien-logo-radius pt ang layer 0.25))

(defun tbkg:draw-trench-at-end (p1 p2 types labels doubles mantels splits combis existing-labels triples mantel-spec twosideds side2doubles side2triples side2combis / n spacing boxlen height top bottom stub ang text-props text-ang text-just mantel-text-props mantel-text-ang mantel-text-just mantel-text-pt pts x i gi pt text-pt double triple combi mantel twosided items item attr-index ss next blkname insert-before insert-ent oldattreq insert-result groups gtypes glabels gdoubles gtriples gmantels gcombis gexisting-labels gsplits gtwosideds group curr-types curr-labels curr-existing-labels curr-doubles curr-triples curr-mantels curr-combis curr-splits curr-twosideds offset group-start group-end group-boxlen group-n group-has-twosided attr-pts sorted-items floating-text-pt floating-text-ang floating-text-just swap-pt swap-ang swap-just primary-label existing-label secondary-label mask-index tray-polys lower-stub-a lower-stub-b lower-stub-mid tray-center mantel-inward-ang mantel-normal-a mantel-normal-b mantel-outward-ang mantel-base-pt entry-stub-start switch-u switch-diff switch-parallel switch-normal switch-inward mantel-axis mantel-q-pt mantel-low-pt mantel-q-vec mantel-low-vec mantel-parallel mantel-normal-offset has-twosided side-sep item-side-sep primary-pt secondary-pt side2double side2triple side2combi curr-side2doubles curr-side2triples curr-side2combis gside2doubles gside2triples gside2combis)
  (if (null doubles) (setq doubles (tbkg:blank-doubles (length types))))
  (if (null triples) (setq triples (tbkg:blank-triples (length types))))
  (if (null twosideds) (setq twosideds (tbkg:blank-twosideds (length types))))
  ;; Oudere data gebruikte op beide zijden dezelfde vorm. Die blijft als fallback behouden.
  (if (null side2doubles) (setq side2doubles doubles))
  (if (null side2triples) (setq side2triples triples))
  (if (null side2combis) (setq side2combis combis))
  (if (null mantels) (setq mantels (tbkg:blank-mantels (length types))))
  (if (null splits) (setq splits (tbkg:blank-splits (length types))))
  (if (null combis) (setq combis (tbkg:blank-combis (length types))))
  (if (null existing-labels) (setq existing-labels (tbkg:blank-existing-labels (length types))))
  (if (null mantel-spec) (setq mantel-spec ""))
  (setq *tbkg:new-ents* nil)

  (setq n (length types))
  (setq spacing *tbkg:kabelrij-spacing*)
  (setq boxlen (+ (* (max 1 (- n 1)) spacing) 2.10))
  (setq has-twosided nil)
  (foreach twosided twosideds
    (if (tbkg:double-p twosided) (setq has-twosided T)))
  ;; De twee-zijdige bak blijft compact. Met een hartafstand van 1.33 en
  ;; mantelradius 0.64 passen beide mantelbuizen binnen dezelfde bakhoogte van 3.00,
  ;; met een kleine buitenmarge en zonder dat de mantelbuizen elkaar raken.
  (setq height *tbkg:bakhoogte*)
  (setq side-sep *tbkg:twosided-center-sep*)
  (setq top 0.0)
  (setq bottom (- height))
  (setq stub 1.05)
  (setq ang (angle p1 p2))
  (setq text-props (tbkg:readable-text-props (- ang (/ pi 2.0))))
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
      (if (tbkg:double-p twosided) (setq group-has-twosided T)))
    ;; Twee-zijdig vergroot de baklengte niet meer kunstmatig. De vaste
    ;; hartafstand en de standaard eindmarges bepalen voor iedere rij dezelfde maat.
    (setq group-boxlen
      (+ (* (max 1 (- group-n 1)) spacing)
         2.10))
    (setq group-start (tbkg:local->world p2 ang offset 0.0))
    (setq group-end (tbkg:local->world p2 ang (+ offset group-boxlen stub) 0.0))
    (setq pts
      (if (= offset 0.0)
        (list p1 group-start
          (tbkg:local->world group-start ang 0.0 bottom)
          (tbkg:local->world group-start ang group-boxlen bottom)
          (tbkg:local->world group-start ang group-boxlen top)
          group-end)
        (list group-start
          (tbkg:local->world group-start ang 0.0 bottom)
          (tbkg:local->world group-start ang group-boxlen bottom)
          (tbkg:local->world group-start ang group-boxlen top)
          group-end)))
    ;; Laat de bak-wipeout aan beide open uiteinden even ver doorlopen.
    ;; Rechts liep hij al tot het bovenste uitsteeksel; links wordt hij nu
    ;; ook één STUB teruggetrokken tot het onderste uitsteeksel.
    (tbkg:make-wipeout
      (list
        (tbkg:local->world group-start ang (- stub) top)
        (tbkg:local->world group-start ang (+ group-boxlen stub) top)
        (tbkg:local->world group-start ang (+ group-boxlen stub) bottom)
        (tbkg:local->world group-start ang (- stub) bottom))
      "E_LV_CROSS_SECTION")
    (setq tray-polys (append tray-polys (list pts)))
    (setq gi 0)
    (foreach typ gtypes
      (setq x (+ (/ group-boxlen 2.0) (- (* gi spacing) (/ (* (- group-n 1) spacing) 2.0))))
      (setq pt (tbkg:local->world group-start ang x (/ bottom 2.0)))
      (setq text-pt (tbkg:local->world group-start ang x (- bottom 0.20)))
      (setq double (nth gi gdoubles))
      (setq triple (nth gi gtriples))
      (setq twosided (tbkg:double-p (nth gi gtwosideds)))
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
            (tbkg:local->world group-start ang x (+ (/ bottom 2.0) (/ item-side-sep 2.0))))
          (setq secondary-pt
            (tbkg:local->world group-start ang x (- (/ bottom 2.0) (/ item-side-sep 2.0)))))
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
  (setq lower-stub-a (tbkg:midpoint entry-stub-start p2))
  (setq lower-stub-b
    (tbkg:midpoint
      (tbkg:local->world group-start ang group-boxlen top)
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
    (tbkg:local->world p2 ang (/ (max 0.0 (- offset stub)) 2.0) (/ bottom 2.0)))
  (setq mantel-inward-ang (angle lower-stub-mid tray-center))

  ;; Kies van de twee normalen op de tekstrichting degene die van de bak af
  ;; wijst. De onderste positie wordt nu in twee onafhankelijke componenten
  ;; opgebouwd. Zo blijft de goede rechtspositie van versie Q behouden zonder
  ;; dat de tekst door die horizontale verschuiving opnieuw omhoog gaat.
  (setq mantel-normal-a (+ mantel-text-ang (/ pi 2.0)))
  (setq mantel-normal-b (- mantel-text-ang (/ pi 2.0)))
  (setq mantel-outward-ang
    (if (<
          (tbkg:dot2
            (list (cos mantel-normal-a) (sin mantel-normal-a))
            (tbkg:vector2 lower-stub-mid tray-center))
          (tbkg:dot2
            (list (cos mantel-normal-b) (sin mantel-normal-b))
            (tbkg:vector2 lower-stub-mid tray-center)))
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
  (setq mantel-q-vec (tbkg:vector2 lower-stub-mid mantel-q-pt))
  (setq mantel-low-vec (tbkg:vector2 lower-stub-mid mantel-low-pt))
  (setq mantel-parallel (tbkg:dot2 mantel-q-vec mantel-axis))
  (setq mantel-normal-offset
    (tbkg:dot2 mantel-low-vec
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
          (tbkg:dot2
            (list (cos mantel-text-ang) (sin mantel-text-ang))
            (tbkg:vector2 lower-stub-mid tray-center))
          0.0)
      0
      2))

  (setq sorted-items (vl-sort items 'tbkg:editor-item<))
  (setq floating-text-ang text-ang
        floating-text-just 1)
  (if *tbkg:floating-code*
    (progn
      (setq floating-text-pt
        (tbkg:p+
          (tbkg:local->world p2 ang (+ (- offset 0.90) (if (< (cadr p2) (cadr p1)) 0.35 0.45)) -0.45)
          text-ang
          (if (< (cadr p2) (cadr p1)) -0.80 0.80)))
      ;; Bij ingeschakelde CODESWITCH worden uitsluitend nieuwe zwevende
      ;; kabelgeulen omgewisseld. Bestaande blocks worden niet aangepast.
      (if *tbkg:future-code-switch*
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
          (setq switch-diff (tbkg:vector2 p2 mantel-text-pt))
          (setq switch-parallel (tbkg:dot2 switch-diff switch-u))
          (setq switch-normal
            (list
              (- (car switch-diff) (* switch-parallel (car switch-u)))
              (- (cadr switch-diff) (* switch-parallel (cadr switch-u)))))
          (setq switch-inward
            (if (>= (tbkg:dot2 switch-u (tbkg:vector2 p2 tray-center)) 0.0)
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
        ((tbkg:half-rooien-p (car item))
         (tbkg:half-rooien-text-points (nth 4 item) ang (nth 5 item) text-ang))
        ((tbkg:double-p (nth 10 item))
         (tbkg:two-sided-text-points (nth 4 item) (nth 11 item) (nth 5 item) text-ang))
        (T
         (list (nth 5 item) nil text-just))))
    (setq primary-label (tbkg:prefixed-label (car item) (cadr item)))
    (tbkg:draw-text-wipeout (car attr-pts) text-ang (nth 2 attr-pts) primary-label 0.55 (tbkg:index-tag "KABEL" mask-index))
    (cond
      ((tbkg:half-rooien-p (car item))
       (setq existing-label (tbkg:prefixed-label "Bestaand" (nth 8 item)))
       (tbkg:draw-text-wipeout (cadr attr-pts) text-ang text-just existing-label
         0.55 (tbkg:index-tag "BESTAAND" mask-index)))
      ((tbkg:double-p (nth 10 item))
       (setq secondary-label (tbkg:prefixed-label (car item) (nth 8 item)))
       (tbkg:draw-text-wipeout (cadr attr-pts) text-ang text-just secondary-label
         0.55 (tbkg:index-tag "ZIJDE2" mask-index))))
    (setq mask-index (1+ mask-index)))
  (if floating-text-pt
    (tbkg:draw-text-wipeout floating-text-pt floating-text-ang floating-text-just *tbkg:floating-code* 0.55 "ZWEVEND_CODE"))

  ;; De wipeout van MANTEL_SPEC wordt altijd vóór de blauwe bak- en leaderlijnen
  ;; aangemaakt. Daardoor blijft de wipeout onder de blauwe geometrie, ook wanneer
  ;; CODESWITCH de mantelbuistekst naar de bovenste positie verplaatst. De tekst
  ;; zelf wordt daarna als attribuut gemaakt en blijft dus boven de lijnen zichtbaar.
  (tbkg:draw-text-wipeout mantel-text-pt mantel-text-ang mantel-text-just
    mantel-spec 0.55 "MANTEL_SPEC")

  (foreach pts tray-polys
    (tbkg:make-lwpoly pts "E_LV_CROSS_SECTION" nil))

  (if *tbkg:floating-code*
    (tbkg:make-attribute floating-text-pt 0.55 floating-text-ang "ZWEVEND_CODE" "Zwevende KabelGeul code:" *tbkg:floating-code* "KabelGeul zwevend code" floating-text-just 0))

  ;; Altijd als attribuut aanmaken, zodat de tekst later met KGEDIT aangepast kan worden.
  (tbkg:make-attribute mantel-text-pt 0.55 mantel-text-ang "MANTEL_SPEC"
    "Mantelbuis specificaties:" mantel-spec "Mantelbuis" mantel-text-just 0)

  (vl-catch-all-apply 'tbkg:draw-mantelbuis-items (list items ang))
  (foreach item (reverse items)
    (tbkg:draw-cable-icon (nth 4 item) ang (car item) (nth 2 item) (nth 9 item) nil (nth 6 item))
    (if (and (tbkg:double-p (nth 10 item)) (tbkg:point-p (nth 11 item)))
      ;; Zijde 2 gebruikt zijn eigen gekozen vorm.
      (tbkg:draw-cable-icon (nth 11 item) ang (car item) (nth 12 item) (nth 13 item) nil (nth 14 item))))

  (tbkg:make-attribute p2 0.10 text-ang "TB_CODESWAP" "Codepositie:"
    (if *tbkg:future-code-switch* "1" "0") "E_LV_CROSS_SECTION" text-just 3)
  (setq attr-index 1)
  (foreach item sorted-items
    (setq attr-pts
      (cond
        ((tbkg:half-rooien-p (car item))
         (tbkg:half-rooien-text-points (nth 4 item) ang (nth 5 item) text-ang))
        ((tbkg:double-p (nth 10 item))
         (tbkg:two-sided-text-points (nth 4 item) (nth 11 item) (nth 5 item) text-ang))
        (T
         (list (nth 5 item) nil text-just))))
    (tbkg:draw-cable-attributes (car attr-pts) text-ang (nth 2 attr-pts) (car item) (cadr item) attr-index)
    (cond
      ((tbkg:half-rooien-p (car item))
       (tbkg:draw-bestaand-attribute (cadr attr-pts) text-ang text-just
         (nth 8 item) attr-index))
      ((tbkg:double-p (nth 10 item))
       (tbkg:draw-side2-attribute (cadr attr-pts) text-ang text-just
         (car item) (nth 8 item) attr-index)))
    (tbkg:draw-split-attribute (nth 5 item) text-ang text-just (nth 7 item) attr-index)
    (tbkg:make-attribute (nth 5 item) 0.10 text-ang (tbkg:index-tag "TBTYPE" attr-index)
      "Kabeltype:" (car item) "E_LV_CROSS_SECTION" text-just 3)
    (tbkg:draw-copy-option-attributes (nth 5 item) text-ang text-just
      (nth 2 item) (nth 9 item) (nth 3 item) (nth 6 item) (nth 10 item)
      (nth 12 item) (nth 13 item) (nth 14 item) attr-index)
    (setq attr-index (1+ attr-index)))

  (setq ss (ssadd))
  (foreach next (reverse *tbkg:new-ents*)
    (if (and next (entget next)) (ssadd next ss)))
  (setq *tbkg:new-ents* nil)

  (if (> (sslength ss) 0)
    (progn
      (setq blkname (tbkg:unique-block-name))
      (vl-cmdf "_.BLOCK" blkname "_non" p2 ss "")
      (setq insert-before (entlast))
      (setq oldattreq (getvar "ATTREQ"))
      (setvar "ATTREQ" 0)
      (setq insert-result (vl-catch-all-apply 'vl-cmdf (list "_.INSERT" blkname "_non" p2 1.0 1.0 0.0)))
      (setvar "ATTREQ" oldattreq)
      (setq insert-ent (tbkg:find-new-insert insert-before))
      (if insert-ent (tbkg:bring-to-front insert-ent))
      (if insert-ent insert-ent insert-result))))

(defun tbkg:draw-trench (base ang types labels / p1 p2)
  (setq p1 (tbkg:local->world base ang -7.35 1.5))
  (setq p2 (tbkg:local->world base ang 0.0 1.5))
  (tbkg:draw-trench-at-end p1 p2 types labels nil nil nil nil nil nil "" nil nil nil nil))

(defun tbkg:draw-trench-with-place-mode (p1 p2 data / mode code ang floating-leader-len place fp1 fp2 oldcode placed)
  (initget "Direct Zwevend")
  (setq mode (getkword "\nPlaatsing kabelgeul [Direct/Zwevend] <Direct>: "))
  (if (null mode) (setq mode "Direct"))
  (cond
    ((= mode "Zwevend")
     (setq code (tbkg:next-floating-code))
     (setq ang (angle p1 p2))
     (setq floating-leader-len 1.05)
     (setq place (getpoint "\nKlik plaats voor zwevende kabelgeul: "))
     (if place
       (progn
         (tbkg:draw-floating-reference p1 p2 code)
         (setq fp2 place)
         (setq fp1 (polar fp2 (+ ang pi) floating-leader-len))
         (setq oldcode *tbkg:floating-code*)
         (setq *tbkg:floating-code* code)
         (tbkg:draw-trench-at-end fp1 fp2 (cadr data) (caddr data) (cadddr data) (nth 4 data) (nth 5 data) (nth 6 data) (nth 7 data) (nth 8 data) (nth 9 data) (nth 10 data) (nth 11 data) (nth 12 data) (nth 13 data))
         (setq *tbkg:floating-code* oldcode)
         (setq placed T))
       (princ "\nZwevende KabelGeul geannuleerd.")))
    (T
     (tbkg:draw-trench-at-end p1 p2 (cadr data) (caddr data) (cadddr data) (nth 4 data) (nth 5 data) (nth 6 data) (nth 7 data) (nth 8 data) (nth 9 data) (nth 10 data) (nth 11 data) (nth 12 data) (nth 13 data))
     (setq placed T)))
  placed)

(defun C:TBKG_KABELGEUL (/ olderr oldcmd sel ent insert-ent p1 rawp2 p2 cable-ang geul-ang data crossed fence-types start-type placed handled)
  (tbkg:hide-wipeout-frames)
  (setq olderr *error*)
  (defun *error* (msg)
  (TBKG_NATIVE_FINISH)
  (command-s "_.UNDO" "_End")
    (if oldcmd (setvar "CMDECHO" oldcmd))
    (setq *error* olderr)
    (if (and msg (not (wcmatch (strcase msg) "*CANCEL*,*QUIT*")))
      (princ
        (strcat
          "\nFout"
          (if *tbkg:copy-stage* (strcat " bij " *tbkg:copy-stage*) "")
          ": " (tbkg:friendly-error-message msg))))
    (setq *tbkg:copy-stage* nil)
    (princ))
  (setq oldcmd (getvar "CMDECHO"))
  (setvar "CMDECHO" 0)
  (tbkg:ensure-layers)
  (setq sel (entsel "\nSelecteer eerste polyline / begin lange lijn of bestaande KabelGeul: "))
  (if sel
    (progn
      (setq ent (car sel))
      (setq insert-ent (tbkg:selected-insert ent))
      (if insert-ent
        (progn
          (setq handled T)
          (setq placed (tbkg:create-copy-from-insert insert-ent)))
        (progn
          (setq p1 (cadr sel))
          (setq cable-ang (tbkg:curve-angle-at-pick ent p1))
          (setq p1 (tbkg:curve-point-at-pick ent p1))))))
  (if (not handled)
    (progn
      (if (and p1 cable-ang)
        (setq rawp2 (getpoint p1 "\nAfstandspunt richting halve vierkant: ")))
      (if (and p1 rawp2 cable-ang)
        (progn
          (setq geul-ang (+ cable-ang (/ pi 2.0)))
          (setq p2 (tbkg:endpoint-from-perp p1 rawp2 geul-ang))
          (setq start-type (tbkg:layer->type (cdr (assoc 8 (entget ent)))))
          (setq crossed (tbkg:crossed-cable-types p1 p2 ent))
          (setq fence-types (tbkg:fence-cable-types p1 p2 ent))
          (if (> (length fence-types) (length crossed))
            (setq crossed fence-types))
          (if start-type
            (setq crossed (cons start-type crossed)))
          (if crossed
            (progn
              (setq *tbkg:last-count* (max 1 (length crossed)))
              (setq *tbkg:last-types* crossed)
              (setq *tbkg:last-labels* (tbkg:blank-labels *tbkg:last-count*))
              (setq *tbkg:last-existing-labels* (tbkg:blank-existing-labels *tbkg:last-count*))
              (setq *tbkg:last-doubles* (tbkg:blank-doubles *tbkg:last-count*))
              (setq *tbkg:last-triples* (tbkg:blank-triples *tbkg:last-count*))
              (setq *tbkg:last-twosideds* (tbkg:blank-twosideds *tbkg:last-count*))
              (setq *tbkg:last-side2-doubles* (tbkg:blank-doubles *tbkg:last-count*))
              (setq *tbkg:last-side2-triples* (tbkg:blank-triples *tbkg:last-count*))
              (setq *tbkg:last-side2-combis* (tbkg:blank-combis *tbkg:last-count*))
              (setq *tbkg:last-mantels* (tbkg:blank-mantels *tbkg:last-count*))
              (setq *tbkg:last-splits* (tbkg:blank-splits *tbkg:last-count*))
              (setq *tbkg:last-combis* (tbkg:blank-combis *tbkg:last-count*)))
            (progn
              (setq *tbkg:last-count* (max 1 (tbkg:count-crossed-polylines p1 p2)))
              (setq *tbkg:last-existing-labels* (tbkg:blank-existing-labels *tbkg:last-count*))
              (setq *tbkg:last-doubles* (tbkg:blank-doubles *tbkg:last-count*))
              (setq *tbkg:last-triples* (tbkg:blank-triples *tbkg:last-count*))
              (setq *tbkg:last-twosideds* (tbkg:blank-twosideds *tbkg:last-count*))
              (setq *tbkg:last-side2-doubles* (tbkg:blank-doubles *tbkg:last-count*))
              (setq *tbkg:last-side2-triples* (tbkg:blank-triples *tbkg:last-count*))
              (setq *tbkg:last-side2-combis* (tbkg:blank-combis *tbkg:last-count*))
              (setq *tbkg:last-mantels* (tbkg:blank-mantels *tbkg:last-count*))
              (setq *tbkg:last-splits* (tbkg:blank-splits *tbkg:last-count*))
              (setq *tbkg:last-combis* (tbkg:blank-combis *tbkg:last-count*))))
          (setq *tbkg:last-mantel-spec* "")
          (setq data (tbkg:show-dialog))
          (if data
            (setq placed (tbkg:draw-trench-with-place-mode p1 p2 data))
            (princ "\nKabelGeul geannuleerd.")))
        (princ "\nKabelGeul geannuleerd."))))
  (setvar "CMDECHO" oldcmd)
  (setq *error* olderr)
  (if placed
    (princ "\nKabelGeul geplaatst als uniek block."))
  (princ))

(defun C:TBKG_KG () (C:TBKG_KABELGEUL))

(defun C:TBKG_KGEDIT (/ *error* sel original data context replacement oldfloating oldswitch world-set)
  (setq oldfloating *tbkg:floating-code* oldswitch *tbkg:future-code-switch*)
  (defun *error* (msg)
    (setq *tbkg:floating-code* oldfloating *tbkg:future-code-switch* oldswitch *tbkg:copy-stage* nil)
    (TBKG_NATIVE_FINISH)
    (if world-set (command-s "_.UCS" "_Previous"))
    (command-s "_.UNDO" "_End")
    (if (and msg (not (wcmatch (strcase msg) "*CANCEL*,*QUIT*"))) (princ (strcat "\nKabelgeul bewerken: " msg)))
    (princ))
  (setq sel (entsel "\nSelecteer KabelGeul of zwevende referentie om volledig te bewerken: "))
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
                (progn (TBKG_NATIVE_COMMIT_EDIT replacement) (princ "\nKabelgeul volledig bijgewerkt."))
                (princ "\nGeul kon niet opnieuw worden opgebouwd; oorspronkelijke geul behouden."))
              (command "_.UCS" "_Previous")
              (setq world-set nil))
            (princ "\nKabelgeul bewerken geannuleerd.")))
        (princ "\nDeze geul bevat geen bruikbare kabelgegevens.")))
    (princ "\nGeen bewerkbare KabelGeul geselecteerd."))
  (setq *tbkg:floating-code* oldfloating *tbkg:future-code-switch* oldswitch *tbkg:copy-stage* nil)
  (princ))


(defun C:TBKG_CODESWITCH (/)
  ;; Geen selectie: wissel de standaardpositie voor alle KabelGeulen die
  ;; vanaf nu met KABELGEUL/OMNI in de modus Zwevend worden gemaakt.
  (setq *tbkg:future-code-switch* (not *tbkg:future-code-switch*))
  (setenv "TechbaseKabelGeulCodeSwitch" (if *tbkg:future-code-switch* "1" "0"))
  (princ
    (if *tbkg:future-code-switch*
      "\nCODESWITCH staat AAN: bij toekomstige zwevende KabelGeulen komen code en mantelbuistekst omgewisseld te staan."
      "\nCODESWITCH staat UIT: toekomstige zwevende KabelGeulen gebruiken de standaardposities."))
  (princ))

(defun C:TBKG_KGCODESWITCH () (C:TBKG_CODESWITCH))

(defun tbkg:copy-insert-to (insert-ent base target) (TBKG_NATIVE_COPY insert-ent base target))

(defun tbkg:clone-block-definition (oldname newname) (TBKG_NATIVE_CLONE oldname newname))

(defun tbkg:set-insert-block-name (insert-ent blkname / data)
  (setq data (entget insert-ent))
  (if (assoc 2 data)
    (progn
      (entmod (subst (cons 2 blkname) (assoc 2 data) data))
      (entupd insert-ent)
      blkname)
    nil))

(defun tbkg:make-copy-unique-block (insert-ent / data oldname newname)
  (setq data (entget insert-ent))
  (setq oldname (cdr (assoc 2 data)))
  (setq newname (tbkg:unique-block-name))
  (if (and oldname (tbkg:clone-block-definition oldname newname))
    (tbkg:set-insert-block-name insert-ent newname)
    nil))

(defun tbkg:block-circles (blkrec / next data circles)
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

(defun tbkg:icon-circle->type (circle / color layer typ)
  (setq color (nth 3 circle))
  (setq layer (car circle))
  ;; De MS-types mogen dezelfde kleur hebben als bestaande kabels, maar
  ;; hebben eigen lagen. Gebruik daarom eerst de laag en pas daarna kleur.
  (setq typ (tbkg:layer->type layer))
  (if typ
    typ
    (tbkg:color->type color)))

(defun tbkg:block-mantelbuis-polys (blkrec / next data polys)
  (setq polys '())
  (if blkrec
    (progn
      (setq next (entnext blkrec))
      (while next
        (setq data (entget next))
        (cond
          ((and (= (cdr (assoc 0 data)) "LWPOLYLINE")
                (= (strcase (if (cdr (assoc 8 data)) (cdr (assoc 8 data)) "")) "MANTELBUIS"))
           (setq polys (cons (tbkg:lwpolyline-points data) polys)))
          ((= (cdr (assoc 0 data)) "ENDBLK")
           (setq next nil)))
        (if next
          (setq next (entnext next))))))
  (reverse polys))

(defun tbkg:point-in-poly-p (pt poly / x y inside p1 p2 x1 y1 x2 y2 rest)
  (setq poly (tbkg:valid-points poly))
  (setq inside nil)
  (if (and (tbkg:point-p pt) (> (length poly) 2))
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

(defun tbkg:mantel-poly-key-for-point (pt polys / idx found)
  (setq idx 1)
  (while (and (tbkg:point-p pt) (listp polys) polys (null found))
    (if (tbkg:point-in-poly-p pt (car polys))
      (setq found (strcat "M" (itoa idx))))
    (setq idx (1+ idx))
    (setq polys (cdr polys)))
  found)

(defun tbkg:copy-icon-data-for-point (pt ang circles polys / icon-pt circle layer center dist typ best-type best-dist count mantel combi triple has-rooien has-bestaand)
  (if (tbkg:point-p pt)
    (progn
      (setq icon-pt (polar pt (+ ang (/ pi 2.0)) 1.70))
      (setq count 0 mantel (tbkg:mantel-poly-key-for-point icon-pt polys) best-type nil best-dist nil combi nil triple nil has-rooien nil has-bestaand nil)
      (foreach circle circles
        (setq layer (if (listp circle) (car circle) nil))
        (setq center (if (listp circle) (cadr circle) nil))
        (if (and (listp circle) (tbkg:point-p center))
          (progn
            (setq dist (distance (tbkg:pt2 center) (tbkg:pt2 icon-pt)))
            (cond
              ((= (strcase (if layer layer "")) "MANTELBUIS")
               (if (and (< dist 0.80) (null mantel)) (setq mantel "X")))
              ((setq typ (tbkg:icon-circle->type circle))
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

(defun tbkg:safe-copy-icon-data-for-point (pt ang circles polys / result)
  (setq result (vl-catch-all-apply 'tbkg:copy-icon-data-for-point (list pt ang circles polys)))
  (if (vl-catch-all-error-p result)
    (list nil nil nil nil nil)
    result))

(defun tbkg:copy-attr-items (insert-ent / next data tag idx txt layer typ pt items second-items second-item)
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
             ((tbkg:kabel-tag-p tag)
              (setq idx (tbkg:tag-index tag))
              (setq txt (if (assoc 1 data) (cdr (assoc 1 data)) ""))
              (setq layer (cdr (assoc 8 data)))
              (setq typ (tbkg:layer->type layer))
              (if (null typ) (setq typ (tbkg:normalize-type txt)))
              (if (null typ) (setq typ "K01"))
              (setq pt (tbkg:attribute-point data))
              (if (tbkg:point-p pt)
                (setq pt (tbkg:world->block insert-ent pt))
                (setq pt nil))
              (setq items (cons (list idx typ txt pt) items)))
             ((tbkg:bestaand-tag-p tag)
              (setq idx (tbkg:bestaand-tag-index tag))
              (setq txt (if (assoc 1 data) (cdr (assoc 1 data)) ""))
              (setq pt (tbkg:attribute-point data))
              (if (tbkg:point-p pt)
                (setq pt (tbkg:world->block insert-ent pt))
                (setq pt nil))
              (setq second-items (cons (list idx txt pt "BESTAAND") second-items)))
             ((tbkg:zijde2-tag-p tag)
              (setq idx (tbkg:zijde2-tag-index tag))
              (setq txt (if (assoc 1 data) (cdr (assoc 1 data)) ""))
              (setq pt (tbkg:attribute-point data))
              (if (tbkg:point-p pt)
                (setq pt (tbkg:world->block insert-ent pt))
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

(defun tbkg:split-tag-p (tag)
  (tbkg:starts-with (strcase (if tag tag "")) "VOLG_"))

(defun tbkg:block-has-split-metadata-p (blkrec / next data found)
  (if blkrec
    (progn
      (setq next (entnext blkrec))
      (while (and next (null found))
        (setq data (entget next))
        (cond
          ((and (= (cdr (assoc 0 data)) "ATTDEF")
                (tbkg:split-tag-p (cdr (assoc 2 data))))
           (setq found T))
          ((= (cdr (assoc 0 data)) "ENDBLK")
           (setq next nil)))
        (if (and next (null found))
          (setq next (entnext next))))))
  found)

(defun tbkg:block-split-value (blkrec i / ent data)
  (setq ent (tbkg:block-attdef-ent blkrec (tbkg:index-tag "VOLG" i)))
  (if (null ent)
    (setq ent (tbkg:block-attdef-ent blkrec (strcat "VOLG_" (itoa i)))))
  (if ent
    (progn
      (setq data (entget ent))
      (tbkg:double-p (cdr (assoc 1 data))))
    nil))

(defun tbkg:block-option-metadata (blkrec prefix i / ent data)
  (setq ent (tbkg:block-attdef-ent blkrec (tbkg:index-tag prefix i)))
  (if (null ent)
    (setq ent (tbkg:block-attdef-ent blkrec (strcat prefix "_" (itoa i)))))
  (if ent
    (progn
      (setq data (entget ent))
      ;; De buitenste lijst onderscheidt 'metadata ontbreekt' van een opgeslagen lege/0-waarde.
      (list T (if (assoc 1 data) (cdr (assoc 1 data)) "")))
    nil))

(defun tbkg:block-group-starts (blkrec ang / next data pts start unit starts)
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
           (setq pts (tbkg:lwpolyline-points data))
           (if pts
             (progn
               ;; De eerste bak bevat ook de leader en heeft daarom één punt extra.
               (setq start (if (> (length pts) 5) (cadr pts) (car pts)))
               (if (tbkg:point-p start)
                 (setq starts (cons (tbkg:dot2 start unit) starts))))))
          ((= (cdr (assoc 0 data)) "ENDBLK")
           (setq next nil)))
        (if next
          (setq next (entnext next))))))
  (vl-sort starts '<))

(defun tbkg:point-group-id (pt starts ang / unit pos idx group start)
  (if (tbkg:point-p pt)
    (progn
      (setq unit (list (cos ang) (sin ang)))
      (setq pos (tbkg:dot2 pt unit))
      (setq idx 0 group 0)
      (foreach start starts
        (if (>= pos (- start 1e-6))
          (setq group idx))
        (setq idx (1+ idx)))
      group)
    0))

(defun tbkg:groupids->splits (groupids / first prev id splits)
  (setq first T prev nil splits '())
  (foreach id groupids
    (setq splits
      (append splits (list (if first nil (/= id prev)))))
    (setq first nil prev id))
  splits)

(defun tbkg:reverse-splits-for-copy (splits)
  (if splits
    (cons nil (reverse (cdr splits)))
    nil))

(defun tbkg:insert-attribute-value (insert-ent tag / ent data)
  (setq ent (tbkg:attr-ent insert-ent tag))
  (if ent
    (progn
      (setq data (entget ent))
      (if (assoc 1 data) (cdr (assoc 1 data)) ""))
    ""))

(defun tbkg:copy-data-from-insert (insert-ent / data blkname blkrec ang circles polys items item icon typ types labels existing-labels doubles triples twosideds side2doubles side2triples side2combis mantels splits combis mantel-spec has-split-metadata has-half-attribute has-twosided-attribute twosided group-starts groupids out-splits icon-pt double-meta triple-meta mantel-meta combi-meta twosided-meta side2double-meta side2triple-meta side2combi-meta primary-double primary-triple primary-combi side2double side2triple side2combi)
  (setq *tbkg:copy-stage* "bronblok: blokverwijzing lezen")
  (setq data (entget insert-ent))
  (setq blkname (cdr (assoc 2 data)))
  (setq blkrec (if blkname (tblobjname "BLOCK" blkname) nil))
  (setq *tbkg:copy-stage* "bronblok: geulrichting bepalen")
  (setq ang (if blkrec (tbkg:block-cross-angle blkrec) 0.0))
  (setq *tbkg:copy-stage* "bronblok: kabelsymbolen lezen")
  (setq circles (if blkrec (tbkg:block-circles blkrec) nil))
  (setq *tbkg:copy-stage* "bronblok: mantelbuizen lezen")
  (setq polys (if blkrec (tbkg:block-mantelbuis-polys blkrec) nil))
  (setq *tbkg:copy-stage* "bronblok: tekstattributen lezen")
  (setq items (tbkg:copy-attr-items insert-ent))
  (setq items (vl-sort items '(lambda (a b / ap bp ax bx)
    (setq ap (nth 3 a) bp (nth 3 b))
    (if (and (tbkg:point-p ap) (tbkg:point-p bp))
      (progn
        (setq ax (+ (* (car ap) (cos ang)) (* (cadr ap) (sin ang))))
        (setq bx (+ (* (car bp) (cos ang)) (* (cadr bp) (sin ang))))
        (if (equal ax bx 1e-8) (< (car a) (car b)) (< ax bx)))
      (< (car a) (car b))))))
  (setq mantel-spec (tbkg:insert-attribute-value insert-ent "MANTEL_SPEC"))
  (setq *tbkg:copy-stage* "bronblok: volgmetadata lezen")
  (setq has-split-metadata (tbkg:block-has-split-metadata-p blkrec))
  (setq *tbkg:copy-stage* "bronblok: geulgroepen bepalen")
  (setq group-starts (if has-split-metadata nil (tbkg:block-group-starts blkrec ang)))
  (setq types '() labels '() existing-labels '() doubles '() triples '()
        twosideds '() side2doubles '() side2triples '() side2combis '() mantels '() splits '() combis '() groupids '())
  (foreach item items
    (setq *tbkg:copy-stage* (strcat "bronblok: kabel " (itoa (car item)) " verwerken"))
    (setq typ (cadr item))
    (setq has-half-attribute (= (nth 6 item) "BESTAAND"))
    (setq has-twosided-attribute (= (nth 6 item) "ZIJDE2"))
    (setq icon-pt (cond ((tbkg:point-p (nth 5 item)) (nth 5 item)) ((tbkg:point-p (nth 3 item)) (nth 3 item)) (T nil)))
    (setq icon (tbkg:safe-copy-icon-data-for-point icon-pt ang circles polys))
    (setq double-meta (tbkg:block-option-metadata blkrec "DUBBEL" (car item)))
    (setq triple-meta (tbkg:block-option-metadata blkrec "DRIE" (car item)))
    (setq mantel-meta (tbkg:block-option-metadata blkrec "MANTEL" (car item)))
    (setq combi-meta (tbkg:block-option-metadata blkrec "COMBI" (car item)))
    (setq twosided-meta (tbkg:block-option-metadata blkrec "TWEEZIJDIG" (car item)))
    (setq side2double-meta (tbkg:block-option-metadata blkrec "DUBBEL2" (car item)))
    (setq side2triple-meta (tbkg:block-option-metadata blkrec "DRIE2" (car item)))
    (setq side2combi-meta (tbkg:block-option-metadata blkrec "COMBI2" (car item)))
    (setq twosided
      (if twosided-meta
        (tbkg:double-p (cadr twosided-meta))
        has-twosided-attribute))
    (if has-half-attribute (setq twosided nil))
    (if (car icon) (setq typ (car icon)))
    (if (tbkg:block-option-metadata blkrec "TBTYPE" (car item))
      (setq typ (cadr (tbkg:block-option-metadata blkrec "TBTYPE" (car item)))))
    (if has-half-attribute (setq typ "Half rooien"))
    (setq types (cons typ types))
    (setq labels (cons (tbkg:strip-known-prefix (caddr item)) labels))
    (setq existing-labels (cons (tbkg:strip-known-prefix (nth 4 item)) existing-labels))
    (setq primary-triple
      (if has-half-attribute nil
        (if triple-meta (tbkg:double-p (cadr triple-meta)) (nth 4 icon))))
    (setq primary-combi
      (if combi-meta (tbkg:double-p (cadr combi-meta)) (nth 3 icon)))
    (setq primary-double
      (if has-half-attribute T
        (if (or primary-triple primary-combi) nil
          (if double-meta (tbkg:double-p (cadr double-meta)) (cadr icon)))))
    (setq side2triple
      (if twosided
        (if side2triple-meta (tbkg:double-p (cadr side2triple-meta)) primary-triple)
        nil))
    (setq side2combi
      (if twosided
        (if side2combi-meta (tbkg:double-p (cadr side2combi-meta)) primary-combi)
        nil))
    (setq side2double
      (if twosided
        (if (or side2triple side2combi) nil
          (if side2double-meta (tbkg:double-p (cadr side2double-meta)) primary-double))
        nil))
    (setq triples (cons primary-triple triples))
    (setq doubles (cons primary-double doubles))
    (setq twosideds (cons twosided twosideds))
    (setq side2doubles (cons side2double side2doubles))
    (setq side2triples (cons side2triple side2triples))
    (setq side2combis (cons side2combi side2combis))
    (setq mantels (cons (if mantel-meta (tbkg:mantel-value (cadr mantel-meta)) (caddr icon)) mantels))
    (if has-split-metadata
      (setq splits (cons (tbkg:block-split-value blkrec (car item)) splits))
      (setq groupids (cons (tbkg:point-group-id (nth 3 item) group-starts ang) groupids)))
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
  (setq out-splits (if has-split-metadata splits (tbkg:groupids->splits groupids)))
  (if (> (length types) 0)
    (progn
      (setq *tbkg:copy-stage* "bronblok uitgelezen")
      (list (length types) types labels doubles mantels out-splits combis
            existing-labels triples mantel-spec twosideds side2doubles side2triples side2combis))
    nil))

(defun tbkg:seed-dialog-data (data)
  (setq *tbkg:last-count* (car data))
  (setq *tbkg:last-types* (cadr data))
  (setq *tbkg:last-labels* (caddr data))
  (setq *tbkg:last-doubles* (cadddr data))
  (setq *tbkg:last-mantels* (nth 4 data))
  (setq *tbkg:last-splits* (nth 5 data))
  (setq *tbkg:last-combis* (nth 6 data))
  (setq *tbkg:last-existing-labels* (nth 7 data))
  (setq *tbkg:last-triples* (if (nth 8 data) (nth 8 data) (tbkg:blank-triples (car data))))
  (setq *tbkg:last-mantel-spec* (if (nth 9 data) (nth 9 data) ""))
  (setq *tbkg:last-twosideds* (if (nth 10 data) (nth 10 data) (tbkg:blank-twosideds (car data))))
  (setq *tbkg:last-side2-doubles*
    (if (nth 11 data) (nth 11 data) *tbkg:last-doubles*))
  (setq *tbkg:last-side2-triples*
    (if (nth 12 data) (nth 12 data) *tbkg:last-triples*))
  (setq *tbkg:last-side2-combis*
    (if (nth 13 data) (nth 13 data) *tbkg:last-combis*))
  data)

(defun tbkg:leader-poly-ent (blkrec / next data found)
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

(defun tbkg:set-first-lwpoly-point (ent pt / data out done pair)
  (setq data (entget ent))
  (setq out '() done nil)
  (foreach pair data
    (if (and (not done) (= (car pair) 10))
      (progn
        (setq out (cons (cons 10 (tbkg:pt2 pt)) out))
        (setq done T))
      (setq out (cons pair out))))
  (if done
    (progn
      (entmod (reverse out))
      (entupd ent)
      T)
    nil))

(defun tbkg:set-copy-leader-length (insert-ent / data blkname blkrec poly pts p1 p2 p2-world picked picked-block newlen newp1 dir)
  (setq data (entget insert-ent))
  (setq blkname (cdr (assoc 2 data)))
  (setq blkrec (if blkname (tblobjname "BLOCK" blkname) nil))
  (setq poly (if blkrec (tbkg:leader-poly-ent blkrec) nil))
  (if (and poly (setq pts (tbkg:lwpolyline-points (entget poly))) (cadr pts))
    (progn
      (setq p1 (car pts))
      (setq p2 (cadr pts))
      (setq dir (angle p2 p1))
      (setq p2-world (tbkg:block->world insert-ent p2))
      (setq picked (getpoint p2-world "\nKlik nieuwe lengte voor de leader <Enter behoudt lengte>: "))
      (if picked
        (progn
          (setq picked-block (tbkg:world->block insert-ent picked))
          (setq newlen (distance (tbkg:pt2 p2) (tbkg:pt2 picked-block)))
          (setq newp1 (polar p2 dir newlen))
          (if (tbkg:set-first-lwpoly-point poly newp1)
            (progn
              (entupd insert-ent)
              T)
            nil))
        T))
    nil))

(defun tbkg:insert-leader-angle (insert-ent / data blkname blkrec poly pts)
  (setq data (entget insert-ent))
  (setq blkname (cdr (assoc 2 data)))
  (setq blkrec (if blkname (tblobjname "BLOCK" blkname) nil))
  (setq poly (if blkrec (tbkg:leader-poly-ent blkrec) nil))
  (if (and poly
           (setq pts (tbkg:lwpolyline-points (entget poly)))
           (cadr pts))
    (angle (tbkg:block->world insert-ent (car pts)) (tbkg:block->world insert-ent (cadr pts)))
    nil))

(defun tbkg:create-copy-from-insert (insert-ent / source-data dialog-data sel ent p1 rawp2 p2 cable-ang geul-ang placed)
  (setq *tbkg:copy-stage* "bronblok uitlezen")
  (setq source-data (tbkg:copy-data-from-insert insert-ent))
  (if source-data
    (progn
      (setq *tbkg:copy-stage* "nieuwe kabel selecteren")
      (setq sel (entsel "\nSelecteer kabel/lijn voor beginpunt van de geul leader: "))
      (if sel
        (progn
          (setq ent (car sel))
          (setq p1 (cadr sel))
          (setq *tbkg:copy-stage* "kabelrichting bepalen")
          (setq cable-ang (tbkg:curve-angle-at-pick ent p1))
          (setq p1 (tbkg:curve-point-at-pick ent p1))
          (if (and p1 cable-ang)
            (progn
              (setq *tbkg:copy-stage* "leaderpunt kiezen")
              (setq rawp2 (getpoint p1 "\nKlik lengte/eindpunt en richtingzijde van de geul leader: "))
              (if rawp2
                (progn
                  (setq *tbkg:copy-stage* "leadergeometrie berekenen")
                  (setq geul-ang (+ cable-ang (/ pi 2.0)))
                  (setq p2 (tbkg:endpoint-from-perp p1 rawp2 geul-ang))
                  (setq *tbkg:copy-stage* "kopieervenster vullen")
                  (tbkg:seed-dialog-data source-data)
                  (setq dialog-data (tbkg:show-dialog))
                  (if dialog-data
                    (progn
                      (setq *tbkg:copy-stage* "nieuwe kabelgeul tekenen")
                      (setq placed (tbkg:draw-trench-with-place-mode p1 p2 dialog-data)))
                    (princ "\nKabelGeul copy geannuleerd.")))
                (princ "\nKabelGeul copy geannuleerd.")))
            (princ "\nKabelGeul copy geannuleerd.")))
        (princ "\nKabelGeul copy geannuleerd.")))
    (princ "\nKan geen KabelGeul-gegevens uit dit block lezen."))
  (setq *tbkg:copy-stage* nil)
  placed)

(defun C:TBKG_KABELGEULCOPY (/ olderr oldcmd sel insert-ent placed)
  (tbkg:hide-wipeout-frames)
  (setq olderr *error*)
  (defun *error* (msg)
  (TBKG_NATIVE_FINISH)
  (command-s "_.UNDO" "_End")
    (if oldcmd (setvar "CMDECHO" oldcmd))
    (setq *error* olderr)
    (if (and msg (not (wcmatch (strcase msg) "*CANCEL*,*QUIT*")))
      (princ
        (strcat
          "\nFout"
          (if *tbkg:copy-stage* (strcat " bij " *tbkg:copy-stage*) "")
          ": " (tbkg:friendly-error-message msg))))
    (setq *tbkg:copy-stage* nil)
    (princ))
  (setq oldcmd (getvar "CMDECHO"))
  (setvar "CMDECHO" 0)
  (tbkg:ensure-layers)
  (setq sel (entsel "\nSelecteer KabelGeul block om gegevens over te nemen: "))
  (if sel
    (progn
      (setq insert-ent (tbkg:selected-insert (car sel)))
      (if insert-ent
        (progn
          (setq placed (tbkg:create-copy-from-insert insert-ent))
          (if placed
            (princ "\nKabelGeul opnieuw aangemaakt met overgenomen gegevens.")))
        (princ "\nSelectie is geen block met attributen.")))
    (princ "\nGeen block geselecteerd."))
  (setvar "CMDECHO" oldcmd)
  (setq *error* olderr)
  (princ))

(defun C:TBKG_KGCOPY () (C:TBKG_KABELGEULCOPY))

(defun C:TBKG_KGTEST (/ base)
  (tbkg:hide-wipeout-frames)
  (tbkg:ensure-layers)
  (setq base (getpoint "\nPlaats test KabelGeul: "))
  (if base
    (progn
      (tbkg:draw-trench base 0.0 '("K02" "Rooien" "K04" "Bestaand")
                      '("Ri 2 4x150Al" "(Rooien) 95Al" "Ri 4 4x150Al" "Bestaande kabel"))
      (princ "\nTest KabelGeul geplaatst als block.")))
  (princ))

;; Unieke Omni-commando's voorkomen verwarring met oudere KabelGeul-versies.
(defun C:TBKG_KABELGEULOMNI () (C:TBKG_KABELGEUL))
(defun C:TBKG_KGOMNI () (C:TBKG_KABELGEUL))
(defun C:TBKG_KGOMNI06B () (C:TBKG_KABELGEUL))
(defun C:TBKG_KGOMNI06C () (C:TBKG_KABELGEUL))
(defun C:TBKG_KGOMNI06D () (C:TBKG_KABELGEUL))
(defun C:TBKG_KGOMNI06E () (C:TBKG_KABELGEUL))
(defun C:TBKG_KGOMNI06F () (C:TBKG_KABELGEUL))
(defun C:TBKG_KGOMNI06G () (C:TBKG_KABELGEUL))
(defun C:TBKG_KGOMNI06H () (C:TBKG_KABELGEUL))
(defun C:TBKG_KGOMNI06I () (C:TBKG_KABELGEUL))
(defun C:TBKG_KGOMNI06J () (C:TBKG_KABELGEUL))
(defun C:TBKG_KGOMNI06L () (C:TBKG_KABELGEUL))
(defun C:TBKG_KGOMNI06M () (C:TBKG_KABELGEUL))
(defun C:TBKG_KGOMNI06N () (C:TBKG_KABELGEUL))
(defun C:TBKG_KGOMNI06O () (C:TBKG_KABELGEUL))
(defun C:TBKG_KGOMNI06P () (C:TBKG_KABELGEUL))
(defun C:TBKG_KGOMNI06Q () (C:TBKG_KABELGEUL))
(defun C:TBKG_KGOMNI06R () (C:TBKG_KABELGEUL))
(defun C:TBKG_KGOMNI06U () (C:TBKG_KABELGEUL))
(defun C:TBKG_KGOMNI06V () (C:TBKG_KABELGEUL))
(defun C:TBKG_KGOMNI06W () (C:TBKG_KABELGEUL))
(defun C:TBKG_KGOMNI06X () (C:TBKG_KABELGEUL))
(defun C:TBKG_KGOMNI06Y () (C:TBKG_KABELGEUL))
(defun C:TBKG_KGOMNI06Z () (C:TBKG_KABELGEUL))
(defun C:TBKG_KGOMNI06AA () (C:TBKG_KABELGEUL))
(defun C:TBKG_KGOMNI06AC () (C:TBKG_KABELGEUL))
(defun C:TBKG_KGOMNI06AD () (C:TBKG_KABELGEUL))
(defun C:TBKG_KGOMNINIEUW () (C:TBKG_KABELGEUL))
(defun C:TBKG_OMNI () (C:TBKG_KABELGEUL))
(defun C:TBKG_KGOMNIEDIT () (C:TBKG_KGEDIT))
(defun C:TBKG_KGOMNICOPY () (C:TBKG_KABELGEULCOPY))

;; Oude MS-commando's blijven als compatibele aliassen beschikbaar.
(defun C:TBKG_KABELGEULMS () (C:TBKG_KABELGEUL))
(defun C:TBKG_KGMS () (C:TBKG_KABELGEUL))

(defun C:TBKG_KGVER (/ locatie)
  (setq locatie
    (if *tbkg:lsp-dir*
      *tbkg:lsp-dir*
      "onbekende map"))
  (alert
    (strcat
      "Actieve versie: " *tbkg:version*
       "\nGeladen uit: " locatie
       "\n\nGebruik KGOMNI06AD of KGOMNINIEUW voor de unieke laadtest."
       "\nCODESWITCH voor toekomstige plaatsingen: "
       (if *tbkg:future-code-switch* "AAN (omgewisseld)" "UIT (standaard)")))
  (princ))



(princ "\nTechbase Kabelgeul geladen.")
(princ)



;;; ============================================================

(defun tbkg:selected-insert (ent / candidate)
  (setq candidate (tbkg:selected-insert-original ent))
  (if candidate (TBKG_NATIVE_GUARD candidate) nil))
(defun tbkg:plugin-run (action / *error*)
  (defun *error* (msg)
    (TBKG_NATIVE_FINISH)
    (command-s "_.UNDO" "_End")
    (if (and msg (not (wcmatch (strcase msg) "*CANCEL*,*QUIT*"))) (princ (strcat "\nKabelgeul: " msg)))
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

(setq *tbkg:ready* T)
(princ)

(defun C:TBKABELGEUL () (vl-cmdf "TBGEULINIT") (tbkg:plugin-run "KABELGEUL") (princ))

(defun C:TBGEULCOPY () (vl-cmdf "TBGEULINIT") (tbkg:plugin-run "KABELGEULCOPY") (princ))

(defun C:TBGEULEDIT () (vl-cmdf "TBGEULINIT") (tbkg:plugin-run "KGEDIT") (princ))

(defun C:TBGEULCODE () (vl-cmdf "TBGEULINIT") (tbkg:plugin-run "CODESWITCH") (princ))
