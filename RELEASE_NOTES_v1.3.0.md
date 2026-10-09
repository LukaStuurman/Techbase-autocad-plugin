# Techbase AutoCAD Plugin v1.3.0

Nieuwe tab **Richtingen**:

- Kies richting **1 t/m 12** op laag `Aansluiting LS K01` t/m `K12`.
- **Nieuwe richting tekenen** met cursorvoorbeeld, Enter, Terug en Sluiten.
- **Selectie naar deze richting omzetten** voor bestaande 2D-polylines en losse LINE-lijnen. Bestaande punten en bogen blijven behouden.
- Beide acties gebruiken **global width 0,1**, **DASHED** en **linetype scale 0,0035**, met kleur/lijndikte ByLayer.
- De laatste richtingkeuze wordt bewaard. Ontbrekende lagen krijgen de Techbase-bronkleur; bestaande kleuren blijven intact.

Pak **Techbase.Blocks.1.3.0.zip** uit, voer **Installeren.cmd** uit en herstart AutoCAD of Civil 3D. Open **TECHBASE → Richtingen** of **TBRICHTINGEN**.

Beide builds slagen. In de AutoCAD 2026-engine slagen 79 service-/UI-controles en 9 commandocontroles, plus onafhankelijke controles van opgeslagen polylines. Volledige desktopbediening en overige productversies moeten nog praktisch worden getest. Zie `VALIDATIE.md` voor de controles en ondersteunde objecttypen.
