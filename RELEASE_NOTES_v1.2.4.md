# Techbase AutoCAD Plugin v1.2.4

Bij plaatsen in een oude tekening gebruikt de plugin nu de blockversie uit de gekozen bron, ook als er al een oud block met dezelfde naam zonder tekst aanwezig is.

- Nieuwe plaatsingen krijgen de actuele geometrie en attribuutteksten.
- Bestaande blocks en hun plaatsingen blijven intact.
- Ook gewone geneste blocks gebruiken eigen brondefinities.
- Dezelfde bronversie wordt hergebruikt; een gewijzigde bron krijgt automatisch een nieuwe definitie.
- In de palette blijven de vertrouwde namen staan. De interne CAD-definitienaam begint met `TB_` en bevat een bronversie.

Pak **Techbase.Blocks.1.2.4.zip** uit, voer **Installeren.cmd** uit en herstart AutoCAD of Civil 3D. Open de plugin met **TECHBASE**.

Beide builds slagen. De automatische controles testen oude blocks zonder tekst, naamconflicten in geneste blocks, een gewijzigde bron en behoud van bestaande plaatsingen. Ook de bestaande block-, attribuut-, draai- en laagcontroles slagen. De volledige desktopbediening en overige productversies moeten nog praktisch worden getest; zie `VALIDATIE.md`.
