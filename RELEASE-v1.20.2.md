# Techbase AutoCAD 1.20.2 — vernieuwde werkplaatsinterface

Deze versie moderniseert de navigatie en de bediening van Stationslocaties zonder de bestaande tekenfunctionaliteit te vervangen.

## Nieuwe bediening
- Compacte onderdeelkeuze in smalle gedockte vensters, overzichtelijke icoontabs in bredere vensters. Minder schermruimte gaat verloren aan navigatie.
- Logische volgorde van de onderdelen: Blocks, Richtingen, Kabelgeul, Lichtmasten, Kabelchecker, Stationslocaties en Instellingen.
- Stationslocaties heeft één primaire knop **Nieuwe locatie**, een doorzoekbare lijst met VL/AL- en fotostatus en contextacties bij de gekozen locatie.
- De acties zijn verdeeld in **Locaties**, **Project** en **Uitvoer**. Niet alle knoppen staan meer tegelijk in beeld; lange groepen zijn scrollbaar.
- Rechtermuisknop op een locatie biedt Openen, Bewerken, Kaartopmerking en Verwijderen.
- Gelijke subtiel afgeronde knoppen voor de gedeelde Techbase-bediening, met toetsenbordfocus en ondersteuning voor hoge contrastinstellingen.

## Ongewijzigd
Alle AutoCAD-commando's en bestaande bewerkingsfuncties blijven behouden, inclusief LS/OV-rekken, Kabelgeul, KLIC/PDOK, bladen en PDF. Stationslocaties blijft deel van dezelfde plugin (geen losse installatie).

## Installeren
Download **Techbase.Blocks.1.20.2.zip**, pak deze uit, sluit AutoCAD/Civil 3D en voer **Installeren.cmd** uit. Verwijder een oude afzonderlijke `Stationlocaties.bundle` om eventuele dubbele commandoregistratie te voorkomen.

## Compatibiliteit en controles
De CI controleert de broncode voor R25 (.NET 8, AutoCAD/Civil 3D 2025–2026) en R26 (.NET 10, AutoCAD/Civil 3D 2027). De releaseprocedure controleert of beide DLL's aanwezig zijn in de installatie-ZIP. Visuele en interactieve bediening moet daarnaast op een echte AutoCAD-installatie worden gecontroleerd.
