# Techbase AutoCAD Plugin

Plugin voor AutoCAD en Civil 3D op Windows met zes tabs:

- **Blocks**: de 58 blockknoppen uit de Techbase toolpalette, een cursorvoorbeeld met meedraaiende attribuutteksten, een bewaarde draai-instelling per block, zoeken, groepen en een schakelaar voor laagovername van het object waarop je plaatst.
- **Lichtmasten**: kies een xref, lichtmastlagen en eventueel blocktypen. Werk met een bestaande gesloten polyline, alles in de gekozen xref of een zelf getekende polygoon.
- **Enexis kabelchecker**: de richtingbouwer uit [AutoCAD-Enexis-kabels-checker](https://github.com/LukaStuurman/AutoCAD-Enexis-kabels-checker), inclusief vier kaders, gescheiden verbruik/opwek, stationsopslag en officiële Excel-export. Onderin staat **Richting-info plaatsen** voor de stroom- en lengteteksten van de geselecteerde richting.
- **Kabelgeul**: instellingen per kabeltype voor standaardteksten, vormen en mantelbuizen. Plaatsen opent de gevonden kabels; voeg rijen voor of na de selectie in. Bewerk een bestaande geul volledig, inclusief iconen en bakken, op dezelfde plek.
- **Richtingen**: kies 1 t/m 12, teken een richtingpolyline of zet bestaande 2D-polylines/LINE-lijnen om. Opmaak: global width 0,1, DASHED en lijntypeschaal 0,0035. Plaats LS/OV-richtingsteksten als losse tekst en kopieer of verplaats een gekozen tekststapel leesbaar langs de kabel.

Blocks heeft ook een aan/uit-schakelaar voor een vaste laagkeuze: Richting 1 t/m 12 of Bestaande kabel. Deze keuze heeft voorrang op automatische laagovername en wordt bewaard.

Er is ook een tab **Instellingen** met afzonderlijke teksthoogtes voor MOF-blocks en LS/OV-richtingsteksten: standaard **0,45**, alternatief **0,75**. De keuze blijft bewaard.

## Installeren

Download **Techbase.Blocks.1.8.0.zip** uit de [release v1.8.0](https://github.com/LukaStuurman/Techbase-autocad-plugin/releases/tag/v1.8.0), pak hem uit en dubbelklik **Installeren.cmd**. De installer controleert en installeert ook **Arial Nova Light** via het officiele Microsoft Store-pakket als het lettertype ontbreekt. Geen aparte fontinstallatie nodig. Start AutoCAD of Civil 3D opnieuw en voer **TECHBASE** uit.

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

De build maakt `dist/1.8.0/Techbase.Blocks.bundle` en `Techbase Blocks 1.8.0.zip`. Een andere bronpalette kan met `-PaletteRoot <map>` worden opgegeven.

De meegeleverde Kabelgeul-bron staat in `Assets/Kabelgeul`; herkomst en hashes staan in `bronversie.json`. De bewerkte module wordt reproduceerbaar gemaakt met `python tests/prepare_kabelgeul.py` uit de vastgelegde `Reference/Techbase lisp pakket.lsp`. De build controleert de hashes en heeft geen Python nodig.

De bronrepo bevat ook de afgeronde oudere installatiepakketten. Compiler-caches, tijdelijke bestanden en interne Git-metadata van de meegeleverde Enexis-bron worden niet opgenomen.
