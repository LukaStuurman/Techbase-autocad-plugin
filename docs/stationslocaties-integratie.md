# Integratie stationslocatiekaart in Techbase AutoCAD

## Opbouw
De stationslocatiefunctionaliteit is overgenomen uit [Stationslocatie-kaart-plugin-autocad-](https://github.com/LukaStuurman/Stationslocatie-kaart-plugin-autocad-) (bronversie 1.4.31) naar `src/Stationslocaties`. Deze bron bevat de bladindeling, model- en paper-space-objecten, PDOK-adressen, KLIC-import, PDOK-topografie/percelen, locatiefoto's, trafoblokbibliotheek en plotfunctionaliteit.

De bronbestanden worden meegecompileerd in de bestaande `Techbase.Blocks.dll` voor de twee al ondersteunde AutoCAD API-series (`R25`/.NET 8 en `R26`/.NET 10). Er wordt geen aparte Stationslocaties-DLL geladen. De vijf resources (logo's, stationsgeometrie, KLIC-tekenregels en stijl-DXF) zijn als ingebouwde resources opgenomen met hun oorspronkelijke logische resource-naam.

## Gebruikersinterface en commando's
- In `TECHBASE` staat een nieuwe tab **Stationslocaties**, in dezelfde lichte Techbase-opmaak en met Arial Nova Light.
- De tab toont een actueel overzicht van de VL/AL-bladen en hun fotostatus, met knoppen **Stationlocaties beheren** en **Vernieuwen**.
- Het beheer gebruikt de bestaande dialoog en functionaliteit, met aangepaste kleuren, knoppen, typografie en titel.
- `TBSTATIONLOCATIES` opent de tab, `TBSTATIONBEHEER` opent het beheer. `STATIONLOCATIES` blijft compatibel als alias.
- `TBSTATIONXREFSTART` voert de asynchrone import van kaartreferenties uit. Deze unieke opdracht voorkomt conflicten met de interne opdracht van de zelfstandige plugin.

## Installeren en overgang
Een build van `Build.ps1` bevat alles in de gebruikelijke `Techbase.Blocks.bundle`. Installeer geen tweede stationslocatiepakket. Verwijder de zelfstandige `Stationlocaties.bundle` als die eerder is geïnstalleerd; beide pakketten bevatten anders het alias-commando `STATIONLOCATIES`. De eigen `TBSTATIONBEHEER`-route werkt onafhankelijk van dat alias.

## Praktische controlepunten
1. Bouw `src/Techbase.Blocks.csproj` zowel voor `ApiSeries=R25` als voor `ApiSeries=R26` op Windows met de .NET 10 SDK.
2. Controleer in een nieuwe én een bestaande DWG dat `TECHBASE` opent, dat de tab Stationslocaties verschijnt, en dat de bestaande tabs behouden zijn.
3. Maak een VL en een AL, wijzig de trafonaam, verplaats een locatie, controleer de cirkel, bladgegevens en kaartopmerkingen; controleer de schaal 1:250 en de Totaal-layout.
4. Controleer dat nieuwe en bestaande bladen met locatiefoto, opdrachtgever, paraaflogo en projectinformatie correct werken.
5. Controleer `TBSTATIONXREFSTART` via de UI met een uitgepakte KLIC-levering, en het gezamenlijk ophalen van Topo en Percelen.
6. Plot een A3-detailblad en Totaal als PDF en controleer de lijngewichten en fotopaden.
7. Controleer de zelfstandige plugin niet gelijktijdig met dezelfde `STATIONLOCATIES`-alias.

De AutoCAD-desktoptests zijn alleen mogelijk op een geschikte Windows-installatie. Een code-integratie of geslaagde compile bewijst nog niet dat bovenstaande interacties werken.
