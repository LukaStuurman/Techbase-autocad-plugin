# Native Stationslocaties in de Techbase-palette

## UI
Stationslocaties is een eigen werkruimte in de bestaande Techbase-palette, niet een knop die een tweede hoofdvenster opent.
De werkruimte bevat een zoekveld, een VL/AL-lijst met fotostatus, en directe acties onder Locaties, Project & kaarten en Uitvoer.
Beschikbare acties: plaatsen, blad openen of bewerken, kaartopmerking, verwijderen, projectgegevens, alle bladen, kaartreferenties, trafoblokken, opdrachtgevers, paraaflogo, Totaal en PDF.
Bij gegevensinvoer blijven alleen de benodigde kleine invul- en bevestigingsvensters beschikbaar.

## AutoCAD-documentcontext
Palette-knoppen kunnen een tekening niet veilig vanuit hun eigen modeless UI-event bewerken. De tab zet daarom een StationActionRequest klaar en start de AutoCAD-opdracht TBSTATIONACTIE.
Deze opdracht controleert het juiste actieve document en roept StationLocationController aan met uitsluitend de gekozen actie en bladnaam.
STATIONLOCATIES, TBSTATIONBEHEER en TBSTATIONLOCATIES openen nu allemaal direct dezelfde tab.

## Beschikbaarheid van 1.19.2-bron
De aangeleverde ZIP Techbase Blocks 1.19.2.zip bevat gecompileerde R25/R26-DLLs, PDB, hulpbestanden en de handleiding, maar geen C#-project of .cs-bronbestanden.
De GitHub-hoofdbranch bevat momenteel nog 1.13.0-code. Dit native-UI-werk is daarom een aparte integratiebranch en is nadrukkelijk geen volledige update naar 1.19.2.
Nieuwere functies in 1.19.2 zoals TBLSREK, TBOVREK, TBTEMPLATE, TBGEULAFSTAND, TBGEULVERPLAATS en de nieuwe vaste navigatie zijn niet in de oudere GitHub-source opgenomen.
Een 1.19.2-rebase mag pas worden vrijgegeven wanneer de bijbehorende 1.19.2-broncode beschikbaar en gecompileerd is, zodat bestaande functies niet verdwijnen.

## Controlepunten
- GitHub Actions bouwt AutoCAD R25 en R26 op Windows.
- Test met echte DWG: zoek/selecteer locatie, VL/AL plaatsen, blad wijzigen, overzicht bijwerken, foto/plot, opdrachtgever/paraaflogo en KLIC/Topo/Percelen.
- Test tab bij breed en smal docken en juiste documentcontext na wisseling van tekening.
