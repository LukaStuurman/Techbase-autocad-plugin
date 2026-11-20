# Techbase AutoCAD Plugin

> **Nieuw in 1.21.1 — AANKOOP-trafoblokken:** bij alle stationsblokken uit de Blocks-tab verschijnen herkenbare varianten met **AANKOOP** achter hun bestaande naam. Ze behouden de oorspronkelijke blockgeometrie en krijgen dezelfde groene ANSI31-arcering (kleur 102, schaal 0,1) en vrije ruimte als de standaard Stationslocaties-trafo. De groene lijnen staan zowel **op de trafo** als in het omliggende aankoopgebied. Dezelfde keuze is beschikbaar bij Stationslocaties en ZRO-bladen, met **Pacto 25 AANKOOP** standaard geselecteerd. Eigen trafoblokken kunnen eveneens worden geregistreerd en gebruikt. Met **Template openen** krijg je een bewerkbare templatekopie met de nieuwe blokdefinities; de oorspronkelijke template blijft behouden. De ZRO-tab heeft nu dezelfde indeling als Stationslocaties en biedt Topografie/Perceel-XREF's zonder KLIC. Zie [AANKOOP-blokken](docs/AANKOOP-trafoblokken.md).

> **Nieuw in 1.21.0 — ZRO-bladen:** naast Stationslocaties is een achtste werkruimte toegevoegd waarmee je per gekozen trafoblok automatisch een ZRO-blad maakt in dezelfde opmaak als de aangeleverde Bakel-DXF (normale voorbeelden TRAFO-10 en TRAFO-4). Schalen boven **1:100** en onder **1:1000**; bloktype, footprint/oppervlakte en C1–C4 worden automatisch bepaald. Adres en kadastrale kenmerken worden via PDOK voorgesteld en blijven bewerkbaar; eigenaar, tekeningnummer, tekenaar, afdeling en e-mail zijn eigen invoervelden. Blauwe sectie-/perceelnummerlabels worden overgenomen of uit PDOK samengesteld. Je kunt bladen later opnieuw openen, bijwerken en de gegevens aanpassen. Zie [ZRO-handleiding](docs/ZRO-Bakel-voorbeeld.md).

> **Nieuw in TechBase 1.21.0:** de kop van **Stationslocaties** is volledig opnieuw opgebouwd. De twee onleesbare witte vlakken zijn vervangen door de zichtbare tekstacties **Lijst vernieuwen** en **Uitleg en tips** onder het zoekveld. De knop **Nieuwe locatie** heeft een vaste hoogte, een eigen ruimte rondom en echt afgeronde hoeken; zijn tekst wordt weer door de normale Windows-knopweergave getekend. De nieuwe kop is als geheel automatisch getest op 380, 780 en 1190 px breed.

> **Sinds 1.20.1: volledige Stationslocaties-integratie.** De codebasis is overgenomen uit de aangeleverde 1.20.0-bron en gecombineerd met het uitgebreide Stationslocaties-beheer. In de nieuwe werkplaats heeft Stationslocaties een eigen zevende navigatieknop die het volledige beheer rechtstreeks in dezelfde interface toont. Voor oudere stationsbladen uit de ingebouwde 1.20-functie blijft `TBSTATIONLEGACY` beschikbaar.


Plugin voor AutoCAD en Civil 3D op Windows met acht werkruimtes:

- **Blocks**: de 58 blockknoppen uit de Techbase toolpalette, een cursorvoorbeeld met meedraaiende attribuutteksten, een bewaarde draai-instelling per block, zoeken, groepen en een schakelaar voor laagovername van het object waarop je plaatst.
- **Lichtmasten**: kies een xref, lichtmastlagen en eventueel blocktypen. Werk met een bestaande gesloten polyline, alles in de gekozen xref of een zelf getekende polygoon.
- **Enexis kabelchecker**: de richtingbouwer uit [AutoCAD-Enexis-kabels-checker](https://github.com/LukaStuurman/AutoCAD-Enexis-kabels-checker), inclusief vier kaders, gescheiden verbruik/opwek, stationsopslag en officiële Excel-export. Onderin staat **Richting-info plaatsen** voor de stroom- en lengteteksten van de geselecteerde richting.
- **Kabelgeul**: instellingen per kabeltype met volledige bewerkbare standaardteksten, inclusief Ri-nummer, vormen en mantelbuizen. Kopieren en bewerken behouden de bestaande teksten. Plaatsen opent de gevonden kabels; voeg rijen voor of na de selectie in. Bewerk een bestaande geul volledig, inclusief iconen en bakken, op dezelfde plek.
- **Richtingen**: kies 1 t/m 12, teken een richtingpolyline of zet bestaande 2D-polylines/LINE-lijnen om. Opmaak: global width 0,1, DASHED en lijntypeschaal 0,0035. Herken kabels met een detectielijn en plaats hun LS/OV-teksten onder elkaar. Plaats ook een volledige bursttemplate en kopieer of verplaats een gekozen tekststapel leesbaar langs de kabel.

- **Stationslocaties**: trafolocaties, voorkeurs- en alternatieve locaties, 1:250 A3-detailbladen, totaalkaart, PDOK/KLIC-kaartreferenties, foto's en PDF-export. De tab **Stationslocaties** bevat alle beheerfuncties direct in de Techbase-palette: locaties plaatsen, bladen openen en wijzigen, kaartopmerkingen, kaartreferenties, projectgegevens, totaalkaart en PDF-export. Geen aparte knop of hoofdvenster meer nodig. **STATIONLOCATIES**, **TBSTATIONBEHEER** en **TBSTATIONLOCATIES** openen dezelfde tab.

Blocks heeft ook een aan/uit-schakelaar voor een vaste laagkeuze: Richting 1 t/m 12 of Bestaande kabel. Deze keuze heeft voorrang op automatische laagovername en wordt bewaard.

Er is ook een tab **Instellingen** met afzonderlijke teksthoogtes voor MOF-blocks en LS/OV-richtingsteksten: standaard **0,45**, alternatief **0,75**. Breedtes zijn per hoogte instelbaar: 0,45 heeft 4,80 (bestaand 6,80), 0,75 heeft 7,70 (bestaand 10,70). LS/OV-teksten krijgen standaard een achtergrondmasker met randfactor 1,3 en tekeningachtergrondkleur; aan/uit, randfactor en eigen kleur zijn instelbaar. De keuzes en Kabelgeul-standaardteksten blijven bewaard, ook na herstart. Instellingen heeft een resetknop met een kopie van de vorige instellingen. Bestaande kabellagen en de laag Bestaand kunnen donkergroen of echt zwart (RGB 0,0,0) worden. Kies de kleur en pas deze op de huidige tekening toe; de keuze geldt ook voor nieuwe geimporteerde kabellagen.

In Richtingen zijn de standaardteksten voor gewone/bestaande LS en OV afzonderlijk bewerkbaar en worden ze automatisch bewaard. De resetknop herstelt deze teksten. LS-teksten zijn **150AL / ????-00**, OV-teksten **6CU / ????-00**. Voor bestaande kabels: **95AL / (was ????-00)** en **6CU / (was ????-00)**.

Bij **Instellingen > Tekst bewerken tijdens block plaatsen** kun je de tekstvragen aanzetten. Deze optie staat standaard uit: de plugin plaatst de bestaande attribuuttekst zonder vragen. De keuze blijft bewaard; reset zet hem weer uit. De AutoCAD-instellingen ATTREQ/ATTDIA worden niet gewijzigd.

Alle RT-blocks worden met hun wipeouts achter de overige inhoud geimporteerd. Voor RT-blocks die al in de huidige tekening staan, klik je in **Blocks** op **RT-maskers naar achter** of voer je **TBRTACHTER** uit. Dit herstelt de tekenvolgorde van oorspronkelijke RT-definities en eerdere pluginvarianten; tekst, bolletjes en geometrie behouden hun waarden. Xrefs blijven intact.

Mofteksten volgen nu ook na een latere laagwisseling de kleur van het block. Je kunt **NIEUWE MOF** op een bestaande kabel plaatsen en daarna op richting 6 zetten: de tekst volgt meteen mee, zonder ATTSYNC. Voor eerder geplaatste pluginblocks: klik **Blocks > Blocktekst laag herstellen** (commando **TBBLOCKTEKSTLAAG**) en selecteer de blocks. De herstelactie behoudt eigen tekstwaarden, positie, hoogte en vaste kleuren; expliciete richtingslagen in RT-blocks blijven behouden. Alleen geselecteerde pluginblocks in de huidige tekenruimte worden hersteld; externe referenties, andere blocks en vergrendelde lagen worden overgeslagen.

De stationslocatiekaart is onderdeel van dezelfde Techbase-DLL en heeft geen losse installatie meer nodig. Als de oude zelfstandige `Stationlocaties.bundle` nog geïnstalleerd is, verwijder die dan om dubbele registratie van het oude commando `STATIONLOCATIES` te voorkomen. De Techbase-tab gebruikt altijd het unieke `TBSTATIONBEHEER`.

## Installeren

Download **Techbase.Blocks.1.21.1.zip** uit de [release v1.21.1](https://github.com/LukaStuurman/Techbase-autocad-plugin/releases/tag/v1.21.1), pak hem uit en dubbelklik **Installeren.cmd**. De installer controleert en installeert ook **Arial Nova Light** via het officiele Microsoft Store-pakket als het lettertype ontbreekt. Geen aparte fontinstallatie nodig. Start AutoCAD of Civil 3D opnieuw en voer **TECHBASE** uit.

Nieuwe plaatsingen gebruiken eigen bronblocks, ook als de tekening al een oudere blockdefinitie met dezelfde naam bevat. Bestaande plaatsingen blijven intact.

Met **TBKABELCHECK** open je de checker direct groter. Beheerdersrechten zijn niet nodig: de installatie staat onder `%APPDATA%\Autodesk\ApplicationPlugins`. Bestaande stations en plugininstellingen blijven behouden. De losse Enexis-plugin wordt niet vervangen.

[Volledige handleiding](LEESMIJ.md) · [Testresultaten en praktische beperkingen](VALIDATIE.md)

## Ondersteuning en controle

Het pakket bevat twee builds: **R25 / .NET 8** voor AutoCAD/Civil 3D 2025–2026 en **R26 / .NET 10** voor 2027. De automatische CAD-tests draaien in de AutoCAD 2026-engine. Andere productversies en de volledige desktopbediening moeten nog praktisch worden getest; een geslaagde build is geen volledige producttest.

De gecontroleerde artifacts staan in `validation`: CAD-tekeningen, ingevulde Excel-exports, testverslagen en een render van de checker. De bron van de Enexis-checker is gepind op commit `4009601e15fa4b1b6926380395132b0c1630ce21`; de herkomst en hashes staan in `enexis-source.json`.

## Zelf bouwen

Gebruik Windows en een .NET 10 SDK:

```powershell
.\Build.ps1
```

De benodigde XTP, afbeeldingen en bron-DWG staan in `Assets/Palette`. De kader-Excels en Enexis-bronnen staan onder `vendor/AutoCAD-Enexis-kabels-checker`. De clone heeft daardoor geen lokale Techbase-map of submodule nodig. NuGet haalt de Autodesk API-referenties en Excel-dependencies op. AutoCAD zelf blijft nodig voor de CAD-engine-tests.

De build maakt `dist/1.21.1/Techbase.Blocks.bundle` en `Techbase Blocks 1.21.1.zip`. Een andere bronpalette kan met `-PaletteRoot <map>` worden opgegeven.

De meegeleverde Kabelgeul-bron staat in `Assets/Kabelgeul`; herkomst en hashes staan in `bronversie.json`. De bewerkte module wordt reproduceerbaar gemaakt met `python tests/prepare_kabelgeul.py` uit de vastgelegde `Reference/Techbase lisp pakket.lsp`. De build controleert de hashes en heeft geen Python nodig.

De bronrepo bevat ook de afgeronde oudere installatiepakketten. Compiler-caches, tijdelijke bestanden en interne Git-metadata van de meegeleverde Enexis-bron worden niet opgenomen.
