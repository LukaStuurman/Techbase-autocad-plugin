# Techbase 1.20.3 — beter leesbare Stationslocaties en automatische kaartvolging

## Interface
- **Nieuwe locatie** heeft nu een compact formaat, grotere vetgedrukte witte tekst en een donkeroranje achtergrond met beter contrast.
- **Vernieuwen** en **Uitleg** zijn echte leesbare knoppen in plaats van lege witte vlakken.
- Een lege locatielijst toont de volgende stap; acties waarvoor een geselecteerde locatie nodig is worden tijdelijk verborgen.
- Kopteksten en primaire actieknoppen behouden voortaan hun bedoelde vetgedrukte lettertype.

## AutoCAD-kaarten en C1–C4
- De bestaande objectwijzigingsbewaking is uitgebreid met een controle na MOVE, ROTATE, STRETCH, SCALE, gripbewerkingen en UNDO/REDO.
- Het 1:250-kaartvenster van een detailblad volgt na een gewijzigde invoegpositie het verplaatste trafoblok.
- C1–C4 wordt opnieuw bepaald op basis van de actuele `BlockTransform`, ook na draaien of schalen. De getoonde tabel gebruikt de nieuwe RD-coördinaten op twee decimalen.
- AutoCAD ververst de tekening na een daadwerkelijk gewijzigde kaartuitsnede of hoekpuntentabel.
- Zonder gewijzigde gegevens wordt geen onnodige schermgeneratie uitgevoerd.

## Compatibiliteit
De bestaande LS/OV-rekken, Kabelgeul, PDOK/KLIC-referenties, projecten, bladen en andere AutoCAD-commando's zijn ongewijzigd. Ondersteunt AutoCAD/Civil 3D 2025–2026 (R25/.NET 8) en 2027 (R26/.NET 10).

## Installeren
Download `Techbase.Blocks.1.20.3.zip`, pak de ZIP uit, sluit AutoCAD/Civil 3D en start `Installeren.cmd`. Start AutoCAD daarna opnieuw en voer `TECHBASE` uit.

## Teststatus
R25 en R26 bouwen op Windows in GitHub Actions. De release-workflow controleert de inhoud van het installatiepakket. De interactie met AutoCAD bij echte gripbewerkingen, het hercentreren van een detailblad en de uiteindelijke C1–C4-waarden moeten daarnaast op een echte project-DWG worden gecontroleerd.
