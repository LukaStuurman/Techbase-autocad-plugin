# Techbase AutoCAD v1.20.1 — Stationslocaties ingebouwd

- Techbase 1.20.0 C#-bronnen overgenomen uit de aangeleverde `src.zip`; bestaande nieuwe kabel-, rek-, controle- en weergavefuncties behouden.
- Eén Techbase-werkplaats met zeven navigatieonderdelen, inclusief **Stationslocaties** als volledige ingebedde beheeromgeving. Geen aparte hoofdbeheerknop.
- Gedetailleerde stationlocatiefunctionaliteit geïntegreerd (VL/AL, 1:250 A3-detailbladen, totaalblad, foto, PDF, KLIC/Topo/Percelen, opdrachtgever, paraaf en opmerkingen).
- Eigen commandoreeks `TBSTATIONLOCATIES`, `STATIONLOCATIES`, `TBSTATIONBEHEER` en `TBSTATIONACTIE`, plus `TBSTATIONXREFSTART`.
- Compatibiliteit voor de oude, eenvoudigere stationlocatiebladen via `TBSTATIONLEGACY`.
- Techbase-gebruikersinterface aangepast aan de v1.20-oranje stijl.

## Installeren

Sluit AutoCAD/Civil 3D, pak de release-ZIP uit en voer `Installeren.cmd` uit. Een oudere standalone `Stationlocaties.bundle` dient te worden verwijderd om mogelijke commandoconflicten te voorkomen.

De AutoCAD-functionaliteit moet op Windows in de CAD-omgeving nog praktisch worden gecontroleerd.
