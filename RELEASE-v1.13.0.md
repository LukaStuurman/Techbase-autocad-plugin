# Techbase AutoCAD 1.13.0 — Stationslocaties

De zelfstandige Stationslocatie-kaart-plugin is geïntegreerd in de bestaande Techbase AutoCAD-plugin. Je hoeft nog maar één plugin te installeren.

## Nieuw
- Nieuwe tab **Stationslocaties** in het vertrouwde Techbase-venster met een overzicht van bestaande VL- en AL-bladen en hun fotostatus.
- Alle stationslocatiefuncties: trafolocaties plaatsen en bewerken, A3-detailbladen 1:250, totaalblad, projectgegevens en locatiefoto's.
- PDOK-adressen, KLIC-, topografie- en percelenkaartreferenties, trafoblokbibliotheek, opdrachtgevers, paraaflogo's en PDF-export.
- Gelijke opmaak voor het stationslocatiebeheer: lichte Techbase-kleuren, knoppen en Arial Nova Light.

## Commando's
- `TECHBASE`: opent de plugin met zeven tabs.
- `TBSTATIONLOCATIES`: opent de nieuwe tab.
- `TBSTATIONBEHEER`: opent het uitgebreide stationslocatiebeheer.
- `STATIONLOCATIES`: compatibiliteitsalias.

## Installatie
Download `Techbase.Blocks.1.13.0.zip`, pak de bestanden uit, sluit AutoCAD/Civil 3D en voer `Installeren.cmd` uit. De installer behoudt de bestaande Techbase-instellingen en maakt een back-up van de huidige installatie.

**Let op:** verwijder een eerder geïnstalleerde losse `Stationlocaties.bundle` uit de Autodesk ApplicationPlugins-map om conflicten met het oude `STATIONLOCATIES`-commando te voorkomen.

Dit pakket bevat builds voor AutoCAD/Civil 3D 2025–2026 (R25/.NET 8) en 2027 (R26/.NET 10).

## Controle
De releaseprocedure bouwt beide DLL's en controleert of ze in het ZIP-pakket aanwezig zijn. Het praktische gebruik van AutoCAD, PDOK en KLIC moet ook met een echte tekening worden gecontroleerd.
