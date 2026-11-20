# TechBase AutoCAD 1.20.5 — zichtbare acties en verbeterde Stationslocaties-kop

## Dit is veranderd
- De twee lege witte vlakken bij het zoeken zijn helemaal vervangen door leesbare WinForms-tekstlinks: **Lijst vernieuwen** en **Uitleg en tips**. Ze hebben een duidelijke naam en behouden hun klikfunctie.
- De oranje actie **Nieuwe locatie** gebruikt nu een gewone Windows-knoptekst met een afgeronde knopregio. De knop heeft een vaste hoogte en vrije ruimte rondom, zodat de onderrand en beide onderste hoeken niet worden afgeknipt.
- Alle drie de acties zitten in een zelfstandig `StationLocationHeader`-component. Op smalle schermen kan de primaire knop onder de titel komen te staan en blijven de tekstacties onder het zoekveld zichtbaar.
- Het zoekveld blijft over de beschikbare breedte, in plaats van ruimte te moeten delen met twee witte knoppen.
- Onderaan de werkplaats staat nu de daadwerkelijk geladen pluginversie, zodat je direct kunt zien of AutoCAD na herstart **1.20.5** gebruikt.

## Behouden uit 1.20.4
De synchronisatie van stationsposities, rotaties, A3-kaartuitsneden en C1–C4 blijft intact, evenals alle kabel-, LS/OV-, PDOK/KLIC- en overige tekenfuncties.

## Controles
- GitHub Actions compileert de plugin voor AutoCAD/Civil 3D 2025–2026 (R25/.NET 8) en 2027 (R26/.NET 10).
- De echte productiekop wordt in een afzonderlijke Windows Forms-test gerenderd bij 380, 780 en 1190 pixels. De test controleert de tekstrasterpixels van beide links en de hoofdactie, de knoppositie en de ronde onderhoeken.
- Schermafbeeldingen van deze test worden als GitHub Actions-artifact opgeslagen.

**Praktijktest:** de werking van de interface binnen AutoCAD zelf moet nog worden gecontroleerd. Deze test controleert wel de volledige nieuwe kop in WinForms, niet slechts losse knopobjecten.

## Installatie
Download `Techbase.Blocks.1.20.5.zip`, pak uit, sluit AutoCAD/Civil 3D en voer `Installeren.cmd` uit. Herstart AutoCAD zodat de nieuwe DLL wordt geladen.
