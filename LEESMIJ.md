# Techbase Blocks 1.2.3

Gebruik drie tabs in AutoCAD en Civil 3D voor Windows: Blocks, Lichtmasten en Enexis kabelchecker.

## Installeren

1. Pak de zip uit. Laat de map `Techbase.Blocks.bundle` naast `Installeren.cmd` staan.
2. Sluit AutoCAD en Civil 3D. Dubbelklik op `Installeren.cmd`. Beheerdersrechten zijn niet nodig.
3. Start AutoCAD of Civil 3D en voer **TECHBASE** uit.

De plugin staat daarna onder `%APPDATA%\Autodesk\ApplicationPlugins`. Bestaande bestanden van deze plugin worden bij opnieuw installeren eerst gekopieerd naar een backup. De installatie wijzigt de oorspronkelijke toolpalette, DWG en Lisp niet.

## Blocks plaatsen

- Zoek of kies een groep. Dubbelklik op een block of klik **Plaatsen**.
- Selecteer een block en kies onder de lijst **Eerst draaien, dan invoegpunt**, **Draaien na het invoegpunt** of **Niet automatisch draaien**. De keuze wordt direct per paletteknop opgeslagen en blijft na herstart behouden. Bij eerst draaien zie je het voorbeeld in het midden van de huidige weergave; na de hoekkeuze volgt het gedraaide voorbeeld de cursor naar het invoegpunt.
- Zonder eigen keuze volgt een block de bestaande draaivraag uit de toolpalette. Met **Niet automatisch draaien** kun je die vraag uitschakelen. De handmatige optie **Draaien** blijft beschikbaar.
- Aan de cursor zie je het block inclusief de zichtbare attribuutteksten. Klik op het invoegpunt. Object snaps blijven beschikbaar.
- Blocks met een draaivraag blijven na het invoegpunt zichtbaar terwijl je de hoek kiest; de tekst draait mee. Je kunt klikken, een hoek invoeren of Enter gebruiken voor de standaardhoek.
- Typ bij het invoegpunt **Draaien** om ook andere blocks vooraf te oriÃ«nteren. Daarna kies je het invoegpunt met het gedraaide cursorvoorbeeld. Esc annuleert de volledige plaatsing.
- Met **Laag van object overnemen** aan krijgt de nieuwe blockreferentie de laag van een lijn, polyline, boog, cirkel, spline, ellips of 3D-curve nabij het invoegpunt. Zoekafstand standaard: **0,05 tekeneenheden** (5 cm in een meter-tekening).
- Voor tekst, arceringen, blocks en Civil-objecten gebruikt de plugin de werkelijke objectselectie onder de cursor. Voor een expliciete keuze typ je tijdens plaatsen **Object** en klik je het bedoelde object. Bij een curve wordt het invoegpunt op de curve gezet. Deze expliciete optie werkt ook als de automatische schakelaar uit staat.
- Bij een kruising van verschillende lagen vraagt de plugin welk object bedoeld wordt; Enter gebruikt de palettelaag. Zonder object gebruikt de plugin de vaste laag uit de palette, of de huidige laag als de palette geen laag voorschrijft.
- Met de schakelaar uit gebruikt plaatsen de palettelaag. De huidige tekenlaag blijft in beide standen gelijk.
- Bij een vergrendelde laag breekt plaatsen af met een melding. De plugin ontgrendelt geen lagen.
- **Herhaald plaatsen** blijft het gekozen block plaatsen tot Esc. Met Undo kun je het plaatscommando, inclusief een eventuele reeks, terugnemen.
- Schaal, standaard draaivragen (tenzij je ze per block aanpast), dynamische blockeigenschappen, attribuutwaarden en de twee explode-instellingen uit de XTP worden meegenomen. De interne vaste lagen van blockonderdelen blijven behouden; laag 0 erft de invoeglaag.

De schakelaar regelt plaatsingen vanuit deze plugin. De oorspronkelijke palette en gewone INSERT-commando's worden niet onderschept. De oude Lisp is hiervoor niet nodig. Als die Lisp nog actief is, kan hij zijn eigen gewone INSERT-commando's blijven behandelen.

## Bronnen en instellingen

De installatie bevat een kopie van de XTP, bijbehorende afbeeldingen en de benodigde bron-DWG. Hierdoor werkt de meegeleverde palette zonder de gedeelde Techbase-map. De kopie is een momentopname; bronhashes staan in `Contents/Assets/bronversies.json`. **Vernieuwen** leest de huidige geselecteerde XTP opnieuw. Kies **Bron...** om een andere XTP te gebruiken; daarbij worden de eigen DWG-paden uit die XTP gebruikt, met ondersteuning voor `%UserProfileFolder%` en relatieve paden.

Een blockdefinitie die al in de tekening bestaat wordt hergebruikt. Bestaande blocks worden niet opnieuw gedefinieerd of gewijzigd. Nieuwe blockdefinities worden afzonderlijk uit de bron-DWG geÃƒÂ¯mporteerd, inclusief dynamische eigenschappen en afhankelijkheden. Annuleren laat geen ingevoegd block achter. Invoegeenheden worden tussen block en tekening omgerekend als beide een eenheid hebben; de paletfactor (bijvoorbeeld Flatkast 0,5) wordt daarop toegepast.

Instellingen staan in `%APPDATA%\Techbase\Blocks\settings.json` en blijven bewaard na herstart.

## Tab Lichtmasten

De functionaliteit uit `LICHTMAST.LSP` is rechtstreeks in de plugin opgenomen. Je hoeft de Lisp niet te laden.

1. Open **TECHBASE** en kies de tab **Lichtmasten**, of voer **TBLICHTMAST** uit.
2. Kies de geladen xref. Als dezelfde xref meerdere keren is ingevoegd, staan de invoegingen apart in de lijst met hun handle en locatie. De lijst gebruikt de huidige model-/papieromgeving.
3. Vink de **lichtmastlagen** aan. De lijst bevat lagen waarop blockinvoegingen voorkomen, met aantallen. Onder **Blockfilter** kun je optioneel de gewenste blocktypen aanvinken, zoals de oude Lisp deed. Geen vinkjes bij blocktypen betekent alle typen op de aangevinkte lagen. Bij groepsblocks op dezelfde laag kun je het filter gebruiken om alleen de echte masttypen te kiezen.
4. Kies het gebied en klik **Lichtmasten plaatsen**:

| Gebied | Werking |
| --- | --- |
| Bestaande gesloten polyline | Klik een horizontale, gesloten 2D-polyline. Boogsegmenten worden als bogen getoetst. |
| Alles in de gekozen xref | Plaats alle gefilterde masten uit deze xref-invoeging, zonder gebiedsgrens. |
| Zelf polygoon tekenen | Klik minstens drie hoekpunten. Enter of **Sluiten** sluit de grens; **Terug** neemt het laatste hoekpunt terug. Klikken op het eerste punt sluit ook. Esc annuleert. |

De gebiedsgrens wordt in bovenaanzicht gebruikt; punten op de grens tellen mee. Een zelf getekende grens is een tijdelijk selectiemiddel en blijft niet als polyline in de tekening achter. Bij annuleren zijn er nog geen masten geplaatst.

De plugin plaatst hetzelfde **Lichtmast_Def**-symbool als de Lisp: drie polylijnen, cirkeldiameter **1,1977**, breedte **0,2**, interne laag **0**, kleur ByLayer en DASHED als dat lijntype beschikbaar is. Nieuwe referenties staan op **Bestaand**, met schaal 1 en draaing 0. De huidige tekenlaag en object-snapinstellingen blijven gelijk. Een al aanwezige definitie wordt hergebruikt. De schakelaar uit de tab Blocks heeft geen invloed op deze lichtmastuitwerking.

Verplaatsing, rotatie, xref-basispunt, ongelijke schaalfactoren en Z worden verwerkt. Ook geneste blocks en laag-0-overerving worden meegenomen. Een geselecteerd samengesteld block wordt eenmaal gemarkeerd, zonder extra masten voor zijn interne blocks. Met het blockfilter kun je juist geneste masttypen in een groepsblock kiezen. De xref en bron-DWG blijven ongewijzigd. Onbeladen geneste xrefs worden gemeld. De selectie volgt de blockinvoegingen uit de xref; een eventuele XCLIP-grens is geen extra gebiedsfilter.

Een opnieuw uitgevoerde selectie slaat al door deze plugin geplaatste masten op hetzelfde bronpunt over. Dit geldt niet voor oude handmatige/Lisp-symbolen zonder bronregistratie. Bij verplaatsing van een xref of een eerder geplaatst symbool worden oude symbolen niet automatisch verplaatst of verwijderd. Met Undo kun je de gehele plaatsing terugnemen.

## Tab Enexis kabelchecker

Deze tab bevat de bestaande richtingbouwer uit [jouw GitHub-repo](https://github.com/LukaStuurman/AutoCAD-Enexis-kabels-checker), vastgezet op [commit 4009601](https://github.com/LukaStuurman/AutoCAD-Enexis-kabels-checker/commit/4009601e15fa4b1b6926380395132b0c1630ce21). De rekenengine, kadercatalogi, richting-/stationsmodellen, Excel-worker en vier officiÃƒÂ«le kaderbestanden zijn uit die bron overgenomen. Alleen de vensterkoppeling is aangepast voor de tab.

Open **TECHBASE Ã¢â€ â€™ Enexis kabelchecker**, of gebruik **TBKABELCHECK** om de tab direct groter en zwevend te openen. In een smalle palette blijven alle onderdelen via schuifbalken bereikbaar. **Groter openen** biedt de volledige werkruimte.

- Bouw richtingen op met kabeltype en een hele polyline, of een deel tot een virtueel knippunt. De bestaande eenhedenomrekening en herkenning van richtingnummers worden gebruikt.
- Kies kader **1.0, 2.0, 3.0 of 3.2** en stroombasis **Automatisch, Verbruik of Opwek**.
- Voeg stroominvoer toe met **Cirkel**, **Handmatig** of **Uit kader**. De kabel- en trafototalen blijven gescheiden, zoals in de repo.
- Bereken en bewaar richtingen, laad/bewaar stations en exporteer de ingevulde kader-Excel met het gezamenlijke trafoblad.
- De extra knoppen **Excel exporteren** en **Alle richtingen verwijderen** zijn ook in de tab aanwezig. Bestaande bevestigingen voor verwijderen blijven gelden.
- **Sluiten** keert terug naar de tab Blocks en bewaart de nog niet opgeslagen invoer in de sessie. Gebruik **Station opslaan** voor bewaren na afsluiten van de CAD-toepassing.

Onderin de tab staat **Richting-info plaatsen**. Kies een opgeslagen richting bij **Opgeslagen** (of sla de huidige richting eerst op), klik op de knop en klik daarna in de tekening op het middelpunt van de bovenste tekst. De knop verwerkt actuele wijzigingen in de richting via de bestaande validatie en slaat die in de sessie op; de stations-JSON wordt daarmee niet automatisch overschreven.

Er worden twee gecentreerde Arial-teksten geplaatst: de berekende totale kabelstroom als `...Amp.` en de rekenlengte als `...Met.`. Ze volgen de actieve stroombasis en gebruiken de bestaande laag `Aansluiting LS Kxx`, kleur ByLayer en teksthoogte 1 tekeneenheid. Opwek en verbruik worden eerst apart opgeteld, inclusief de kader-fasefactor. De teksten volgen het huidige UCS. Een ontbrekende of vergrendelde richtingslaag wordt gemeld. Esc annuleert de plaatsing; Undo neemt beide teksten samen terug.

Opgeslagen stations blijven in de bestaande map `%APPDATA%\EnexisKabelChecker\stations.json`. Daardoor kun je de bestaande stations van de losse kabelchecker laden. De huidige richtingenset is, net als in de repo, ÃƒÂ©ÃƒÂ©n sessie; wisselen van tekening maakt geen nieuw station aan. De oorspronkelijke losse plugin en haar ENEXISKABELCHECK-commando worden niet verwijderd of vervangen. In deze plugin gebruik je TBKABELCHECK.

De Excel-dependencies staan per CAD-generatie in een aparte map `excel` en worden via de bestaande geÃƒÂ¯soleerde laadcontext gebruikt. De volledige installatiemap is nodig voor export. Bronversie en bestandschecksums staan in `Contents/Assets/enexis-source.json`; een volgende wijziging in GitHub wordt pas na een nieuwe gecontroleerde build opgenomen.

## Commando's

| Commando | Functie |
| --- | --- |
| TECHBASE | Open het blockpaneel |
| TBLAAG | Wissel automatische laagovername aan/uit |
| TBHERLAAD | Lees de geselecteerde XTP opnieuw |
| TBLICHTMAST | Open de tab Lichtmasten; het paneel start ook dit uitvoercommando |
| TBPLAATS | Intern plaatscommando, aangeroepen door het paneel |
| TBVALIDATE | Test in een aparte database; schrijft test-DWG, DXF en JSON onder `%LOCALAPPDATA%\Techbase\Blocks\Validation` |
| TBMASTVALIDATE | Controleer lichtmastselectie, xref-transformaties en symbolen in aparte testtekeningen |
| TBKABELCHECK | Open de derde tab, Enexis kabelchecker, groter en zwevend |
| TBKABELVALIDATE | Controleer de geÃƒÂ¯ntegreerde rekenengine, bediening en vier echte Excel-exports |
| TBRICHTINGINFO | Plaatscommando achter de knop Richting-info plaatsen |

## Versies en controle

Het pakket bevat twee afzonderlijk gebouwde DLL's: **R25 / .NET 8** voor AutoCAD en Civil 3D 2025/2026, en **R26 / .NET 10** voor 2027. De Autodesk autoloader kiest op productversie. Er worden alleen gewone AutoCAD-API's gebruikt, zonder Civil-specifieke DLL's. AutoCAD LT en Mac zijn geen doelplatformen.

Autodesk bevestigt de API/runtime-grens in [Managed .NET Compatibility](https://help.autodesk.com/cloudhelp/2027/ENU/AutoCAD-Customization/files/GUID-A6C680F2-DE2E-418A-A182-E4884073338A.htm). Het pakket beperkt 2027 expliciet tot R26.0; latere productreleases vragen opnieuw een compatibiliteitscontrole.

De concrete testresultaten en resterende praktijktests staan in `VALIDATIE.md` bij de broncode. Een succesvolle build vervangt geen test in iedere productversie.

## Opnieuw bouwen

Gebruik een .NET 10 SDK en voer `Build.ps1 -Dotnet <pad-naar-dotnet.exe>` uit. De build haalt Autodesk AutoCAD.NET API 25.0.1 en 26.0.0 via NuGet op en bouwt de Excel-worker voor beide runtimes. De Autodesk API-DLL's worden niet meegeleverd: die levert de hostapplicatie. De uitvoer staat in `dist/1.2.3` en in `Techbase Blocks 1.2.3.zip`. Versiegebonden DLL-mappen maken een update mogelijk zonder een reeds geladen oude DLL te overschrijven; herstart de CAD-toepassing om de nieuwe versie te laden.
