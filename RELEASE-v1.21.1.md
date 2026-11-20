# TechBase 1.21.1 — AANKOOP-trafoblokken, gedeeld met Stationslocaties en ZRO

## Nieuwe blokken in Blocks en template
- Ieder stationsblok uit de Blocks-toolpalette heeft een tweede, gelijknamige variant met **AANKOOP** erachter, waaronder **PACTO 25 exc. afm. AANKOOP**.
- Groene arcering op de trafo **én** in het omliggende aankoopgebied, met dezelfde stijl als de standaard Stationslocaties-trafo: ANSI31, kleurindex 102, patroonhoek −90°, patroonschaal 0,1 en links/rechts/boven/onder 2,14 / 0,40 / 0,40 / 1,91 meter vrije ruimte.
- Oorspronkelijke blokken blijven behouden; de AANKOOP-varianten hebben eigen echte DWG-blokdefinities. Groene arcering is zichtbaar op de pictogrammen.
- **Template openen** maakt/opent een bewerkbare templatekopie onder `%LOCALAPPDATA%/Techbase/Templates/`, met alle AANKOOP-blokdefinities. De originele pakket-DWG blijft ongemoeid.

## Stationslocaties en ZRO
- Stationslocaties en ZRO gebruiken nu dezelfde trafoblokbibliotheek. Pacto 25 AANKOOP is standaard geselecteerd, andere palette- of gebruikersblokken kunnen worden gekozen of geregistreerd.
- ZRO kan een nieuwe, groen gearceerde trafo plaatsen of een bestaande stationreferentie selecteren.
- De ZRO-werkplaats volgt de Stationslocaties-vormgeving: doorzoekbare bladlijst zonder storende kolomkop, duidelijk lege-lijstbericht, en groepen ZRO-bladen, Project & kaarten en Uitvoer.
- ZRO-kaartreferenties zijn **alleen PDOK Topografie en Percelen** als losse DWG-XREF's; KLIC wordt niet gebruikt. Stationslocaties behoudt de bestaande KLIC-functies.

## Installeren en validatie
Download `Techbase.Blocks.1.21.1.zip`, pak uit, sluit AutoCAD/Civil 3D en voer `Installeren.cmd` uit. Herstart AutoCAD en voer `TECHBASE` uit.

De GitHub-releaseworkflow compileert R25 (.NET 8, AutoCAD/Civil 3D 2025–2026) en R26 (.NET 10, AutoCAD/Civil 3D 2027) en controleert beide DLL's in de ZIP. Een aparte sourcegeometrietest vergelijkt de groene hatch met het originele stationssymbool. De werkelijke blockinvoeging, DWG-templateopslag, hatchweergave en A3/ZRO-plot moeten daarnaast nog in AutoCAD met een echt project worden gecontroleerd.
