# Controle versie 1.2.2 — 9 oktober 2026

De plugin is gebouwd en lokaal geïnstalleerd. De automatische controles draaien in de echte AutoCAD 2026 Core Console (ACADVER 25.1), zonder de bron-DWG of gebruikersinstellingen te wijzigen.

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
