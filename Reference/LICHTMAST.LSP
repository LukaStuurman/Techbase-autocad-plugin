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