# TechBase AutoCAD 1.20.4 — automatische kaartvolging en duidelijke knoppen

## Stationslocaties: kaart en coördinaten volgen de trafo
- Een aanvullende controle vergelijkt na het voltooien van een tekenbewerking de **werkelijke invoegpositie, rotatie en schaal** van geplaatste stationblokken. Hierdoor worden ook wijzigingen via grips of eigenschappen herkend wanneer AutoCAD geen bruikbare commandogegevens doorgeeft.
- De controle leest na de eerste inventarisatie uitsluitend bekende trafoblokken en ontdekt nieuwe stationinvoegingen; een volledige controle wordt slechts af en toe uitgevoerd.
- Het A3-detailkaartvenster (1:250) controleert zowel de `ViewTarget` als de `ViewCenter`-offset, kaartschaal en vergrendeling. Zo worden niet alleen coördinaten, maar ook werkelijk verschoven of incorrect gecentreerde kaartuitsneden hersteld.
- De C1–C4-coördinaten worden opnieuw berekend met de actuele bloktransformatie en worden bij wijzigingen in het blad bijgewerkt. Wanneer gegevens zijn veranderd, wordt de tekening opnieuw gegenereerd.
- Bij een tijdelijk vergrendelde tekening wordt later opnieuw geprobeerd, zonder een constante herhalingslus.

## Stationslocaties: interface
- **Nieuwe locatie** heeft nu vier daadwerkelijk afgeronde hoeken en vrije ruimte onder de knop. De oranje knop staat niet meer tegen de onderrand van de kopregel.
- De knopteksten **Nieuwe locatie**, **Vernieuwen** en **Uitleg** worden expliciet getekend en blijven leesbaar onder verschillende WinForms-/AutoCAD-thema's.
- De bestaande contextuele indeling, zoekfunctie en Blocks-schakelaaruitlijning blijven intact.

## Validatie
De broncode wordt gecompileerd voor **AutoCAD/Civil 3D 2025–2026** (.NET 8/R25) en **2027** (.NET 10/R26). Een extra Windows Forms-test controleert in gerenderde pixels of de drie knoplabels zichtbaar zijn en de onderste hoeken afgerond zijn. De GitHub-release controleert beide DLL's in het installatiepakket.

**Beperking:** de automatische synchronisatie van kaartvenster en C1–C4 moet nog met een echte DWG in AutoCAD worden gecontroleerd; de compiler en een los UI-rendertest vervangen geen AutoCAD-praktijktest.

## Installeren
Download `Techbase.Blocks.1.20.4.zip`, pak het bestand uit, sluit AutoCAD en voer `Installeren.cmd` uit. Herstart AutoCAD/Civil 3D en open `TECHBASE`. Verwijder een eventueel nog geïnstalleerde losse `Stationlocaties.bundle` om dubbele commandoregistratie te vermijden.
