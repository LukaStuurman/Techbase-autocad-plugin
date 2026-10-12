## Kabelgeul - versie 1.7.0, 12 oktober 2026

De zesde tab bevat KabelGeul Omni 2026-08-06ad uit `Reference/Techbase lisp pakket.lsp`, bronhash **B3923054FBD878B09C16F1738B5D61EB676FDB42A65E5D39EF2BF519CF1F4AEC**. Alleen de Kabelgeul-module is opgenomen in de uitvoerbare, aangepaste Lisp; de lichtmast- en richtingstekstmodules uit het pakket worden niet geladen. De extractie/adapters staan in `tests/prepare_kabelgeul.py`. `Assets/Kabelgeul/bronversie.json` legt bron- en uitvoerhash vast.

- Automatische detectie of handmatige tabelinvoer; 18 typen, 4 vormen, onafhankelijke zijden, Half rooien, mantelgroepen, losse mantels, specificatie en splitsen in meerdere bakken. Knoppen voor plaatsen, gegevens kopieren, teksten bewerken en codepositie wisselen.
- 38 formulier-/uitwisselingscontroles slagen, inclusief teksten met quotes, backslashes en regeleinden, incomplete invoer, alle typen/vormen/opties, rijbewerkingen, Half rooien, de handmatige route en fontstijl. Tab gerenderd op 380 en 1040 pixels; alle controls gebruiken Arial Nova Light. De native TextBox wordt door Core Console DrawToBitmap niet afgebeeld; zijn afmetingen en positie zijn gecontroleerd.
- **27 commando- en DXF-controles slagen** in AutoCAD 2026 Core Console: Direct, Zwevend, gegevens kopieren, behoud van alle gekopieerde typen (inclusief MS Nieuw), twee zijden, Half rooien, mantel en aparte bakken, referentiecode, Esc bij plaatsen/bewerken, vergrendelde gebruikte geullaag, ongewijzigde bestaande kleur en oorspronkelijke KGOMNI, herstel van WIPEOUTFRAME/CLAYER/ATTREQ/CMDECHO. Opgeslagen DXF bevat drie unieke geulblocks, de opgegeven teksten, eigen Light-stijl en mantelradius 0,64.
- De geulmodule gebruikt een eigen namespace en wijzigt bij laden geen tekeninstellingen. Bestaande lagen worden behouden. Het formulier vervangt DCL; native attribuutbewerking en de bestaande maskersynchronisatie blijven gebruikt. Er wordt geen tweede algemene attribuutreactor geinstalleerd.
- Beide API-builds zijn gemaakt. De lokaal geinstalleerde .NET-build wordt via de geulbootstrap geladen; de tab laadt de eigen Lisp-module indien nodig. Core Console scant nieuwe Lisp-bundlecomponenten niet; daarom gebruikt deze test dezelfde laadexpressie als de tab. Het manifest gebruikt de door Autodesk gedocumenteerde [Lisp-component en commandolading](https://help.autodesk.com/cloudhelp/2027/ENU/AutoCAD-Customization/files/GUID-3C25E517-8660-4BB7-9447-2310462EF06F.htm).

Praktijktests blijven nodig voor de volledige desktopinteractie met het modale invoervenster, EATTEDIT en bijgewerkte tekstmaskers, Undo/Redo en AutoCAD/Civil 3D 2025/2027. UNDO Begin/End kon hier ook bij een losse standaard LINE-test in Core Console niet als volledige groep worden teruggenomen; daarom wordt geen geslaagde geul-Undo-test geclaimd. De normale Begin/End-opbouw is in de module opgenomen.

Bewijs: `validation/Kabelgeul/formulier.json`, `commands.json`, `commands.log`, `Kabelgeul - commando.dxf` en de twee tabrenders. De bestaande 410 blockcontroles, bronversie-, MOF-, hoogte-, plaatsvoorbeeld-, laagkeuze- en lettertypecontroles slagen opnieuw.

## Standaard 0,45 en LS/OV-breedte 4,80 - versie 1.6.1

- Standaard teksthoogte voor MOF- en LS/OV-teksten is 0,45; tweede keuze 0,75. Ontbrekende/ongeldige opgeslagen waarden krijgen 0,45. Eerder bewust opgeslagen hoogtes blijven behouden; lokaal beide hoogtes op verzoek op 0,45 gezet.
- LS/OV-richtingsteksten krijgen Defined width 4,80 in het block en na burst, bij beide hoogtekeuzes. Een aparte breedteversie voorkomt hergebruik van oudere definities.
- 59 hoogte-/instellingencontroles, 410 algemene blockcontroles en 36 richtingstekstservice-/8 commandocontroles geslaagd in AutoCAD 2026 Core Console. Opgeslagen DXF onafhankelijk gelezen: alle 52 LS/OV-burstteksten hebben primaire groep 41 (Defined width) 4,80; beide hoogtes gecontroleerd.
- Beide API-builds gemaakt; geen afzonderlijke interactieve 2025/2027- of Civil 3D-test.

## Instellingen en teksthoogtes - versie 1.6.0

- Vijfde tab Instellingen: afzonderlijke hoogtes voor MOF-blocks en LS/OV-richtingsteksten, standaard 0,75; alternatief 0,45. Oude instellingen krijgen beide standaardwaarden. Keuze blijft bewaard; ongeldige opgeslagen waarden worden hersteld.
- 59 gerichte controles: beide hoogtes in de drie MOF-blocks en twee richtingstekstblocks; attributen en gedraaid plaatsvoorbeeld; 13 losse teksten per burst; aparte definities per hoogte; eerdere definitie intact; daadwerkelijke keuzelijst en opslaan/herladen.
- Opgeslagen DXF onafhankelijk gelezen: 26 losse MTexten op 0,75 en 26 op 0,45; MOF-attributen op beide hoogtes. Primaire groep 40 voor TextHeight gelezen; MTEXT-kolomhoogtes zijn aparte waarden. Afzekerings-/rekblocks worden niet door de hoogte-instelling aangepast.
- 410 algemene blockcontroles, bronversie-/plaatsvoorbeeld-/laagkeuzecontroles, 752 lettertypecontroles in vijf tabs, 36 richtingstekstservice- en 8 commandocontroles en 13 draai-/UCS-/Esc-controles geslaagd.
- Beide API-builds gemaakt voor AutoCAD/Civil 3D 2025/2026 en 2027. CAD-tests uitgevoerd met AutoCAD 2026 Core Console; geen afzonderlijke interactieve 2025/2027- of Civil 3D-test.

## BESTAANDE MOF zwart en Continuous - versie 1.5.2

- Alleen de omtrek en teksten/attribuutdefinities van BESTAANDE MOF krijgen vaste AutoCAD-kleur 7 (zwart/wit). De omtrek krijgt expliciet Continuous en schaal 1. Richtingslaagkleuren en DASHED hebben hierop geen invloed; de arcering behoudt de bronopmaak.
- Eigen uitvoeringsversie voor dit block voorkomt hergebruik van een eerdere plugindefinitie. Bron-DWG en reeds geplaatste oude blocks worden niet gewijzigd.
- Plaatsing, Object, Undo, Escape en UCS op richting 1, 10, 12 en Bestaande kabel getest. Opgeslagen cirkel/attributen gecontroleerd op vaste kleur en Continuous. Beide API-builds gemaakt; CAD-tests in AutoCAD 2026 Core Console.

## Bijgewerkte BESTAANDE MOF - versie 1.5.1

- Actuele gebruikersbron `Techbase template LS en OV.dwg` overgenomen in de pluginbibliotheek. SHA-256: `40955691265e0ef638434c265b549f7cae047b8a4c372e9c4a17dd78d54b7c4c` (1.114.306 bytes). De oorspronkelijke gebruikersbron is niet gewijzigd.
- Vergelijking met versie 1.5.0: cirkel en arcering zijn kleiner; de cirkel staat nu op laag 0. Attribuut Bestaand behouden. Vergelijking afgeleid uit beide CAD-bronnen, zonder nieuwe geometrie te bedenken.
- 9 gerichte controles geslaagd. Nieuwe brongeometrie, tekst en attributen vergeleken met de geimporteerde en daadwerkelijk opgeslagen nieuwe blockdefinitie. Zowel een oude gelijknamige definitie als een eerder geimporteerde pluginversie blijven beschermd; nieuwe plaatsingen gebruiken de nieuwe bronversie.
- Opgeslagen DXF onafhankelijk gecontroleerd: nieuwe geplaatste definitie hoort bij bronhash `40955691265E`, cirkelradius 0,305 op laag 0; oude gelijknamige definitie blijft radius 0,5 op Knooppunt LS AM.
- 410 algemene blockcontroles, plaatsvoorbeeld-/lettertype-/laagkeuzecontroles en 7 werkelijke plaats-/Undo-/UCS-controles geslaagd in AutoCAD 2026 Core Console. Builds voor R25 en R26. Geen afzonderlijke interactieve 2025/2027- of Civil 3D-test.

## Blocklaag kiezen - versie 1.5.0

- Nieuwe aan/uit-schakelaar en keuzelijst in Blocks: Richting 1 t/m 12 en `01 - Bestaande kabel`.
- Gekozen laag heeft voorrang; automatische laagovername blijft onthouden en wordt hersteld zodra de keuze uitgaat. Keuze en schakelaar blijven na herladen bewaard. LS/OV-burst in Richtingen blijft zijn eigen lagen gebruiken.
- 37 service-/UI-controles: alle 13 lagen met hun echte bronkleuren, geplaatste blocks en laag-0-attributen; bestaande aangepaste kleur beschermd; vergrendelde/uitgeschakelde/bevroren laag gemeld; oude en ongeldige instellingen verwerkt; toggle en keuzelijst getest.
- 29 werkelijke CAD-commandocontroles voor richting 1, 10, 12 en Bestaande kabel: plaatsen op een andere kabel houdt de gekozen laag, Object behoudt de laag, draaien werkt, Undo en Escape plaatsen geen extra block, UCS-positie klopt en CLAYER blijft intact. De opgeslagen DXF's zijn onafhankelijk teruggelezen. Ook geslaagd via automatisch laden van de lokaal geinstalleerde 1.5.0-bundle.
- De 410 algemene blockcontroles, bronversie-/plaatsvoorbeeld-/draai-instellingcontroles en 749 lettertypecontroles geslaagd in AutoCAD 2026 Core Console. Beide API-builds voor 2025/2026 en 2027; geen afzonderlijke interactieve 2025/2027- of Civil 3D-test.

## Arial Nova Light - versie 1.4.0

- Beide builds voor AutoCAD/Civil 3D 2025-2026 (R25) en 2027 (R26) samengesteld. De daadwerkelijke CAD-controle is uitgevoerd met de AutoCAD 2026 Core Console; geen afzonderlijke interactieve 2025/2027- of Civil 3D-test.
- 749 lettertypecontroles: Windows herkent Arial Nova Light; alle geimporteerde tekst-/attribuutdefinities, geneste en dynamische blocks gebruiken de echte Light-variant. Alle controls in de vier tabs en tabelkoppen gecontroleerd, inclusief later toegevoegde controls. Bestaande gelijknamige gebruikersstijl beschermd.
- Richtingstekst kopieren beschermt het origineel; kopie en verplaatste tekst krijgen Light. Inline font/gewicht verwijderd met behoud van tekst, kleur, hoogte en alineas. De opgeslagen DWG/DXF is teruggelezen en onafhankelijk gecontroleerd.
- Lettertype-installer getest onder Windows PowerShell 5.1: ontbrekend font activeert het officiele Microsoft Store-installatiepad; bestaande installatie wordt hergebruikt; ontbrekende App Installer geeft een duidelijke melding. Store-download in de ontbrekend-fonttest gesimuleerd; registratie-/activatiepad met het echte font uitgevoerd. Op deze computer was het officiele Store-pakket al aanwezig; de installer heeft de Light-variant voor de gebruiker geregistreerd.
- Regressies: 410 blockcontroles, 20 bronversiecontroles, 1549 plaatsvoorbeeldcontroles, 11 draai-instellingcontroles; 7 plaats-/Undo-controles; 13 draai-/UCS-/Esc-controles; 49 kabelcheckercontroles; 79 richtingservice- en 9 commandocontroles; 36 tekstservice- en 8 commandocontroles; 28 lichtmast- en 4 gebiedscontroles geslaagd.
- UI-afbeeldingen van Richtingen en Kabelchecker met Arial Nova Light bekeken. De bronbestanden zijn niet aangepast.

# Controle versie 1.3.1 — 9 oktober 2026

De plugin is gebouwd en lokaal geïnstalleerd. De automatische controles draaien in de echte AutoCAD 2026 Core Console (ACADVER 25.1), zonder de bron-DWG of gebruikersinstellingen te wijzigen.

## Richtingsteksten en burst 1.3.1

Vier nieuwe knoppen plaatsen de LS/OV-teksttemplates als losse MTexten en kopiëren/verplaatsen MTexten zoals de aangeleverde `RICHTINGEN`-Lisp. Selectie gebeurt in klikvolgorde met opnieuw klikken om te verwijderen. De lay-out volgt offset 0,4, tussenruimte 0,2 plus werkelijke teksthoogte, omkeren bij een hoek groter dan 90 en tot en met 270 graden en de bijbehorende top-/bottom-left-ankering. Een extra cursorvoorbeeld toont de tekststapel bij het eindpunt.

- **36 servicecontroles slagen**: LS en OV leveren ieder 13 losse MTexten met broninhoud en richtingslagen; burst bewaart een gewijzigde meerregelige attribuutwaarde; acht richtingen controleren leesbaarheid, klikvolgorde, exacte offsets, werkelijke hoogte en inline opmaak; kopie beschermt de bron; verplaatsen bewaart handles; een verplaatst/gedraaid UCS; Terug; gelijke eindpunten; een vergrendelde tekstlaag; opgeslagen CAD opnieuw lezen.
- **8 commandocontroles plus onafhankelijke DXF-controles slagen** via de echte knoppencommando's: beide burst-plaatsingen, selectietoggle, twee blijvende kopiestapels, een tussenliggende kopiestapel met Terug verwijderen, verplaatsen, Undo en Esc zonder bronwijziging. De opgeslagen hoofdtekening bevat 32 MTexten en geen INSERT/ATTRIB/ATTDEF uit de templates.
- De paneelindeling is offscreen gerenderd en visueel bekeken. Beide CAD-generaties bouwen zonder fouten. De bestaande block-/attribuut-/bronversie-/instellingcontroles en richtinglijncontroles slagen opnieuw. De live desktopbediening en andere productversies moeten nog praktisch worden getest.

Bronmomentopname: `Reference/Techbase lisp pakket.lsp`, SHA-256 **B3923054FBD878B09C16F1738B5D61EB676FDB42A65E5D39EF2BF519CF1F4AEC**. De gelezen functie is vanaf `c:RICHTINGEN` aan het einde van het pakket. De overige Lisp-functies worden niet geladen of uitgevoerd. Het oorspronkelijke `RICHTINGEN`-commando blijft beschikbaar.

Bewijs: `validation/Teksten/richtingsteksten.json`, `command-checks.json`, `teksten.log`, `Richtingsteksten - controle.dwg`/`.dxf`, `Richtingsteksten - commando.dxf` en `Richtingen-met-tekst.png`.

## Tab Richtingen 1.3.0

De vierde tab biedt twaalf richtingskeuzes, een nieuw tekencommando en een selectie-omzetting. De entity-opmaak is breedte 0,1, DASHED, schaal 0,0035, kleur/lijndikte ByLayer. Nieuwe lagen krijgen de kleuren uit de Techbase-template; bestaande kleuren blijven intact. Lichte en oude 2D-polylines worden ter plaatse opgemaakt. LINE-objecten worden vervangen door polylines met dezelfde eindpunten; niet-planare 3D-polylines worden niet afgevlakt.

- **79 service- en UI-controles slagen**: alle twaalf lagen, bronkleuren, DASHED, entity-schaal en uniforme breedte op alle segmenten; punten, bogen, sluiten, hoogte en handles van bestaande polylines; oude 2D-vertices; rechte en ruimtelijke LINE-conversie; bescherming van vergrendelde objecten, blockinhoud en ongeschikte objecttypen; vergrendelde doellaag; behoud van huidige laag en bestaande kleuren; teruglezen uit opgeslagen DWG; twaalf radiokeuzes en opgeslagen selectie.
- **9 commandocontroles slagen**, naast onafhankelijke DXF-controles: tekenen met Enter, sluiten, Terug, een verplaatst/90° gedraaid UCS, Esc zonder nieuwe polyline, een vooraf geselecteerde polyline naar richting 10, selectie van een LINE naar richting 12, Undo van die conversie en behoud van CLAYER, PLINEWID, CELTSCALE en CECOLOR. De vijf definitieve polylines zijn uit DXF teruggelezen, inclusief exacte punten, een bestaande boog en entity-opmaak.
- Beide CAD-generaties bouwen zonder fouten. Het nieuwe paneel is offscreen gerenderd en visueel bekeken; volledige desktopbediening en andere productversies moeten nog praktisch worden getest.

Bewijs: `validation/Richtingen/richtingen.json`, `command-checks.json`, `richtingen.log`, `Richtingen - controle.dwg`/`.dxf`, `Richtingen - commando.dxf` en `Richtingen-tab.png`. De controles gebruiken zelfstandige testtekeningen en geïsoleerde instellingen.

## Oude blocknamen en bronversies 1.2.4

De import gebruikt eigen `TB_`-blockdefinities met een SHA-256-herkomst van de volledige bron-DWG. Gewone naamgenoten in de tekening worden niet hergebruikt of opnieuw gedefinieerd. Ook de gewone geneste blockdefinities krijgen een eigen bronnaam. Anonieme/dynamische records blijven onder beheer van AutoCAD. Lagen en tekststijlen volgen de bestaande importregels.

Bij herhaald plaatsen en na het heropenen van de tekening wordt dezelfde gemarkeerde brondefinitie hergebruikt. Een gewijzigde bron krijgt een nieuwe definitie. Als een gegenereerde naam al bezet is zonder de juiste herkomst, wordt een eigen alternatief gekozen. De bron-DWG wordt alleen in geheugen hernoemd en blijft op schijf intact.

**20 geslaagde controles** simuleren een echte oude paletteblock zonder attributen, controleren actuele attribuutteksten en dynamische bronfunctie bij een nieuwe plaatsing, beschermen de oude definitie en plaatsing en lezen de tekst terug uit opgeslagen CAD. Een afzonderlijke geneste fixture bevat oude definities onder zowel gewone als gegenereerde namen; een tweede bronversie wijzigt alleen de volgende import. Bronbestanden, eerdere versies en bestaande geometrie blijven intact.

Bewijs: `validation/blockversies.json`, `Blockversies - controle.dwg`/`.dxf` en de afzonderlijke testbron. De bestaande block-, attribuut-, draai-, instelling-, laagovername- en Undo-controles slagen opnieuw. Volledige desktopbediening en de overige productversies moeten nog praktisch worden getest.

## Draai-instelling per block 1.2.3

Onder de blocklijst staat voor de geselecteerde paletteknop een keuzelijst: **Eerst draaien, dan invoegpunt**, **Draaien na het invoegpunt** of **Niet automatisch draaien**. Instellingen worden per vaste XTP-tool-ID opgeslagen. Ook varianten van dezelfde blockdefinitie blijven daardoor onafhankelijk. Zonder uitzondering blijft het oorspronkelijke palettegedrag gelden.

Bij eerst draaien verschijnt het voorbeeld bij het midden van de huidige CAD-weergave. Na de hoekkeuze volgt het gedraaide block met zijn teksten de cursor naar het invoegpunt. Automatische laagovername gebeurt bij het definitieve invoegpunt.

- **11 geslaagde controles**: oude instellingen lezen, instellingen schrijven/herladen, andere voorkeuren behouden, onafhankelijke varianten, een bestaande draaivraag uitschakelen, terugzetten naar de paletstandaard en een lege instellingenmap. Ook de echte WinForms-keuzelijst is bediend: selectie, actuele keuze, automatisch bewaren en uitschakelen voor het gekozen block.
- **12 geslaagde controles in twee afzonderlijke AutoCAD-processen**: eerst draaien, draaien na het invoegpunt, niet draaien, een gedraaid UCS, objectlaagovername, de werkelijke promptvolgorde en annuleren bij de hoek of het invoegpunt. Dezelfde opgeslagen instellingen worden in beide processen gebruikt.
- De bestaande 13 draaicontroles, 7 plaats-/laag-/Undo-controles, 1549 attribuutgeometriecontroles en 410 algemene blockcontroles slagen opnieuw. Beide CAD-generaties bouwen zonder fouten.

Bewijs: `block-rotation-settings-checks.json`, `rotation-modes.json`, twee commandologs en `Blocks-draai-instelling.png` in `validation`. De afbeelding is een offscreen-render van het blockpaneel; dit vervangt geen volledige desktoptest.

## Plaatsen en hoofdcommando 1.2.2

Het hoofdcommando is nu **TECHBASE**. Manifest, installer en gebruikershandleiding zijn aangepast. De installer vervangt de oude TBLOCKS-startregistratie alleen binnen de gecontroleerde registratie van deze plugin; oude waarden worden eerst opgeslagen.

Het cursorvoorbeeld bevat het block en de zichtbare attribuutteksten, inclusief meerdere regels. De teksten worden met het block verplaatst en gedraaid vóór er een invoegobject aan de tekening wordt toegevoegd. De draaivraag gebruikt nu hetzelfde voorbeeld. De extra optie **Draaien** bij het invoegpunt werkt ook voor blocks zonder draaivraag uit de XTP.

- **1549 geslaagde geometriecontroles** voor alle **33 paletteknoppen met niet-constante attributen**. De bewegende tekst is vergeleken met onafhankelijk verplaatste/gedraaide attributen van een echt ingevoegd block, op vijf hoeken en afwisselend in wereld- en gedraaid UCS. Ook meerregelige tekst, laagovererving, het ontbreken van tijdelijke inserts en de definitieve tekstgeometrie zijn gecontroleerd.
- **13 geslaagde controles** via de echte plaats-/draaiprompts: klikpunt en numerieke hoek, Enter voor de standaardhoek, vooraf Draaien bij beide soorten blocks, geen tweede draaivraag na een gekozen hoek, gedraaid UCS, annuleren vóór/na het invoegpunt en onafhankelijk teruglezen van de opgeslagen attribuutgeometrie uit DXF.
- De bestaande 410 block-/CAD-controles en 7 controles op laagovername en Undo slagen opnieuw. Het pakket bevat het TECHBASE-commando voor beide CAD-generaties.

Bewijs: `validation/plaatsvoorbeeld.json`, `Plaatsvoorbeeld - controle.dwg`/`.dxf`, `rotation.json`, `rotation.log` en `Draaien - commando.dxf`. De Core Console test de geometrie en prompts; de live cursorweergave in de desktop-GUI moet nog praktisch worden bekeken.

## Derde tab Enexis kabelchecker

Versie 1.2.1 voegt onderin **Richting-info plaatsen** toe. De geselecteerde richting wordt via de oorspronkelijke validatie met de actuele invoer vastgelegd, waarna twee info-teksten op de bestaande richtingslaag worden geplaatst. De annotatiestroom gebruikt dezelfde kabelberekening en actieve stroombasis als de checker.

Er slagen nu **49 integratiecontroles**, inclusief de nieuwe knop, het overnemen van de geselecteerde richting, gescheiden verbruik/opwek-totalen, beide expliciete stroombasissen, tekstinhoud en -volgorde, ByLayer, centrering, hoogte, UCS-positie/rotatie, teruglezen uit opgeslagen CAD en bescherming van ontbrekende/vergrendelde lagen. De echte TBRICHTINGINFO-plaatsopdracht is bovendien uitgevoerd in een gedraaid UCS en levert twee teksten op de juiste laag. Bewijs staat in `Richting-info - controle.dwg`/`.dxf` en de actuele kabelcheckerlogs. De oorspronkelijke ENEXISRICHTINGINFO-entrypoint wordt niet geregistreerd door deze bundle, zodat de losse plugin haar eigen commando behoudt.

Bron: [AutoCAD-Enexis-kabels-checker](https://github.com/LukaStuurman/AutoCAD-Enexis-kabels-checker), vastgezet op [commit 4009601](https://github.com/LukaStuurman/AutoCAD-Enexis-kabels-checker/commit/4009601e15fa4b1b6926380395132b0c1630ce21). Remote HEAD en de nieuwe broncheckout kwamen overeen. De oorspronkelijke bronnen in de checkout zijn ongewijzigd; de integratie compileert dezelfde rekenengine, formulierlogica en Excel-export. De selectiehelpers en extra knop-installatie worden voor hosting gegenereerd. Alleen de entrypoints/vensterhosting zijn aangepast. Herkomst en bestandschecksums staan in `enexis-source.json`.

- Beide CAD-generaties zijn succesvol gebouwd. De upstream formulierbestanden geven twee bestaande nullable-waarschuwingen bij `SystemFonts.MessageBoxFont`; er zijn geen buildfouten. De aparte Excel-workers bouwen zonder waarschuwingen.
- **5 upstream testgroepen slagen**: Excel/rekenreferenties op .NET 8, volledige kader- en stroomcontroles op .NET 8 en .NET 10, en controle van de geïsoleerde ClosedXML-export op beide runtimes. De .NET 8-controles draaien op de geïnstalleerde .NET 8-runtime; de .NET 10-controles op de lokale .NET 10-runtime.
- **36 integratiecontroles slagen in AutoCAD 2026**: de opgenomen rekenengine, de impedantie en afzekering van een upstream referentie, alle vier kadercatalogi en templates, vier echte `.xlsx`-exports met richting- en trafobladen, teruglezen van de ingevulde categorieaantallen, geïsoleerde Excel-dependencies, aanwezigheid van de oorspronkelijke bediening en de extra verwijderknop, berekenen via de ingebedde vorm en behoud van onopgeslagen invoer bij Sluiten.
- De volledige tab is als WinForms-layout gerenderd en visueel bekeken: `validation/Kabelchecker/Kabelchecker-tab.png`. Dit controleert de ingebedde bediening; het is geen volledige praktijktest van de AutoCAD palette in de desktop-GUI.
- **4 aanvullende kabelinvoerchecks slagen** via de echte AutoCAD-prompts: lengte van een hele polyline met mm-naar-m-omrekening, virtuele kniplengte, richtingnummer uit de laagnaam en behoud van het oorspronkelijke fysieke kabeluiteinde.
- Ook de geïnstalleerde bundle is via het nieuwe TBKABELVALIDATE-commando automatisch geladen, zonder NETLOAD; die run doorloopt de 36 integratie- en 4 kabelinvoerchecks.
- Bestaande Blocks- en Lichtmasten-controles zijn na de integratie opnieuw uitgevoerd en slagen: 410 block-/CAD-controles, 7 plaatscommando-checks, 28 lichtmastservice-checks en 4 gebiedsinvoerchecks.

Bewijs: `validation/Kabelchecker/validatie-kabelchecker.json`, `invoercommando.json`, vier ingevulde `Geintegreerde export ...xlsx`-bestanden, de render, `validation/kabelchecker.log` en `validation/Kabelchecker/upstream/checks.json` plus logs. Tests schrijven niet naar de bestaande stationsopslag; daarvoor gebruiken ze de opgeslagen invoer alleen bij het maken van de vorm.

Volledige bediening in de AutoCAD/Civil 3D desktop-GUI en de overige CAD-productversies moeten nog praktisch worden getest. Voor de checker omvat dat het kiezen van kabels en stroomteksten vanuit de palette, een station opslaan/laden, Excel-export via de dialoog, tekenen van richtinginformatie en wisselen van tab/tekening.

## Nieuwe tab Lichtmasten

De functionaliteit van `LICHTMAST.LSP` is opgenomen in een tweede tab. De nieuwe tab biedt xref-invoegingen, lichtmastlagen, een optioneel blockfilter en gebiedsopties bestaande gesloten polyline, alles en zelf polygoon tekenen.

- **28 geslaagde servicecontroles** met een zelfstandig gemaakte test-xref: meerdere invoegingen apart kiezen; lagen/blocktypen filteren; niet-uniforme xref-schaal, verplaatsing, 90° rotatie, xref-basispunt en hoogte; een genest block met eigen rotatie en schaal; laag-0-overerving; gebiedsselectie in wereldcoördinaten; samengesteld block en zijn interne onderdelen eenmaal tellen; herhaling zonder dubbelen.
- Het getekende symbool volgt de Lisp: drie polylijnen, cirkeldiameter 1,1977, breedte 0,2, laag 0 intern en referenties op Bestaand met kleur ByLayer en schaal 1.
- Controles op rechte, concave en ronde gebieden, positieve/negatieve bulges, exacte booggrenzen, grenspunten, open polylines en zelfkruisende rechte polygonen.
- **4 geslaagde invoercontroles via de echte gebiedsprompts**: klikken en Enter sluiten, Terug/Sluiten tijdens tekenen, bestaande polyline aanwijzen en zelf tekenen in een 90° gedraaid UCS.
- De opgeslagen DWG is opnieuw geopend en de symboollagen en wereldposities zijn teruggelezen. De xref-DWG heeft vóór en na uitvoering dezelfde SHA-256.
- De geïnstalleerde versie 1.1.0 laadt op het nieuwe TBMASTVALIDATE-commando automatisch, zonder NETLOAD, en doorloopt dezelfde 28 service- en 4 gebiedsinvoercontroles. De installatie werkt bestaande startregistraties van uitsluitend deze plugin bij, met een backup van de oude waarden.

Bewijs staat in `validation/Lichtmasten/validatie-lichtmasten.json`, `gebiedscommando.json`, de test-xref, `Lichtmasten - controle.dwg`/`.dxf` en `validation/lightmasts.log`. De gelezen Lisp is apart bewaard in `Reference/LICHTMAST.LSP`, SHA-256 `AF52E8C80F4C0CA2B0D0C5484BBF6AA63A21EAA01C521D0D3D13A0BEE07B5BF5`.

De volledige modeless tab is nog niet visueel getest in de AutoCAD/Civil 3D-gebruikersinterface. De praktijkcontrole hieronder blijft ook voor deze tab van toepassing: xref kiezen, lagen en blocktypen aanvinken, iedere gebiedsoptie gebruiken, Esc en Undo controleren, en wisselen tussen tekeningen/model/papier. AutoCAD/Civil 3D 2025 en 2027 zijn hier niet als testhost beschikbaar.

## Uitgevoerde controles

- Twee afzonderlijke Release-builds zonder fouten: Autodesk API **25.0.1 / .NET 8** en **26.0.0 / .NET 10**. De actuele upstream formulierwaarschuwingen staan hierboven.
- **58 paletknoppen**, **53 unieke blockdefinities**, waarvan **13 knoppen een dynamisch block gebruiken**. Dubbele namen zoals Zekering blijven aparte paletknoppen, volgens de XTP.
- **410 geslaagde controles**: importeren en plaatsen van alle tools, paletfactoren waaronder Flatkast 0,5, attributen, beide explode-tools, dynamische varianten waar de bron keuzewaarden aanbiedt, laagtoewijzing en opnieuw openen van de opgeslagen DWG.
- Laagdetectie bij een lijn, polyline, cirkel, binnen/buiten tolerantie, 3D-afstand, meerdere gelijke lagen, verschillende lagen op een kruising, cursorselectie van tekst en bescherming van een vergrendelde laag.
- Annuleren van een transactie laat geen blockreferentie achter. De huidige tekenlaag blijft behouden.
- **7 aanvullende checks via het daadwerkelijke TBPLAATS/TBLAAG-commando**: automatische laag aan, automatische laag uit, expliciete Object-keuze, huidige laag behouden, laag bij een 90° gedraaid UCS, juiste wereldcoördinaten bij dat UCS en Undo van de plaatsing.
- Instellingen tijdens tests zijn geïsoleerd in de validatiemap. De oude Lisp-reactors worden niet verwijderd of gewijzigd.
- De geïnstalleerde bundle is zonder NETLOAD via het geregistreerde commando automatisch geladen in AutoCAD 2026 Core Console; ook die run doorloopt alle 410 controles.

Bewijs: `validation/validatie.json`, `validation/commands.json`, de bijbehorende logbestanden en `validation/Alle blocks - controle.dwg` plus `.dxf`. De bronmomentopname is herkenbaar aan SHA-256 in `Contents/Assets/bronversies.json`.

Bij het openen van de bron-DWG meldt de geïnstalleerde Civil Object Enabler ontbrekende componenten/catalogi in deze Core Console-omgeving. De gebruikte blockdefinities zijn desondanks afzonderlijk geïmporteerd en alle bovengenoemde controles slagen. De plugin wijzigt die computerinstellingen niet.

## Praktijktests die nog nodig zijn

| Product | Build | Engine/commando getest | Volledige gebruikersinterface getest |
| --- | --- | --- | --- |
| AutoCAD 2025 | Geslaagd, API 25.0.1 | Niet beschikbaar op deze computer | Nog uitvoeren |
| Civil 3D 2025 | Zelfde R25-build | Niet uitgevoerd | Nog uitvoeren |
| AutoCAD 2026 | Geslaagd, R25 | Geslaagd in Core Console | Nog uitvoeren |
| Civil 3D 2026 | Zelfde R25-build | Geen aparte Civil-hosttest; Object Enabler is geen volledige Civil-test | Nog uitvoeren |
| AutoCAD 2027 | Geslaagd, API 26.0.0 | Niet beschikbaar op deze computer | Nog uitvoeren |
| Civil 3D 2027 | Zelfde R26-build | Niet uitgevoerd | Nog uitvoeren |

De compatibiliteitsindeling volgt de officiële Autodesk SDK-matrix. Dit is geen claim dat alle zes productcombinaties al praktisch zijn getest.

Korte praktijkcontrole per versie: open een kopie van een tekening, voer TBLOCKS uit, controleer zoeken en afbeeldingen, plaats BESTAANDE MOF op een polyline met de schakelaar aan/uit, plaats met Object op tekst of een Civil-object, bewerk een dynamisch block en een attribuut, annuleer en gebruik Undo. Herstart de toepassing en controleer dat de schakelaar bewaard is gebleven. Controleer herhaling en model-/papieromgeving. Bij een kruisende of samenvallende laag moet een keuzevraag verschijnen.
