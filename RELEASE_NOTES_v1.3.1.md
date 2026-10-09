# Techbase AutoCAD Plugin v1.3.1

De tab **Richtingen** krijgt vier extra knoppen:

- **LS-tekst plaatsen (burst)**
- **OV-tekst plaatsen (burst)**
- **Tekst kopiëren**
- **Tekst verplaatsen**

LS/OV plaatst de bronblocks als losse, bewerkbare richtingsteksten. Kies daarna MTexten in klikvolgorde en twee punten langs de kabel. De tekst wordt leesbaar gestapeld met de offset en tussenruimte uit de aangeleverde RICHTINGEN-Lisp. Een cursorvoorbeeld toont de tekststapel. Kopiëren kan herhaald worden; **Terug** verwijdert de laatste kopiestapel. Verplaatsen bewerkt de oorspronkelijke teksten eenmaal.

Pak **Techbase.Blocks.1.3.1.zip** uit, voer **Installeren.cmd** uit en herstart AutoCAD of Civil 3D. Open **TECHBASE → Richtingen**.

Beide builds slagen. De AutoCAD 2026-engine controleert beide templates, tekstinhoud, lagen, leesbaarheid in acht richtingen, offsets, herhaling, Terug, Undo en Esc. De oorspronkelijke Lisp hoeft niet geladen te worden. Volledige desktopbediening en overige productversies moeten nog praktisch worden getest; zie `VALIDATIE.md`.
