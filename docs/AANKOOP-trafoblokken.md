# AANKOOP-varianten van trafoblokken

## Wat wordt toegevoegd?
Bij ieder stationsblok uit de TechBase-Blocks-toolpalette is een tweede, afzonderlijk blok te kiezen met dezelfde naam gevolgd door **AANKOOP**. Voorbeeld: `PACTO 25 exc. afm.` en `PACTO 25 exc. afm. AANKOOP`. De basisdefinitie wordt nooit overschreven of opnieuw gedefinieerd.

De varianten worden automatisch zichtbaar in **Blocks**; bij hun icoon staan groene arceerlijnen. In **Stationslocaties** en **ZRO-bladen** is dezelfde keuzelijst beschikbaar. De standaardselectie is **Pacto 25 AANKOOP**. Via **Extra trafoblok toevoegen** kun je eigen statische blokken uit modelruimte opslaan in de gedeelde gebruikersbibliotheek, waarna ook daarvan een **AANKOOP**-uitvoering beschikbaar wordt.

## Identiek aan de originele groene stationslocatie
De waarden zijn gecontroleerd tegen `src/Stationslocaties/Resources/StationGeometry.json.gz`.

| Eigenschap | Waarde |
| --- | --- |
| Hatch | ANSI31 |
| AutoCAD-kleurindex | 102 (groen) |
| Patroonhoek | −90° |
| Patroonschaal | 0,1 bij meterblokken |
| Vrije ruimte links | 2,14 m |
| Vrije ruimte rechts | 0,40 m |
| Vrije ruimte boven | 0,40 m |
| Vrije ruimte onder | 1,91 m |

De plugin bepaalt de grootste rechthoekige trafo-omtrek uit het gekozen blok. Rond deze omtrek komt het **aankoopgebied** met de groene ANSI31-arcering. De oorspronkelijke trafo-entities blijven identiek; er wordt nog een tweede groene hatch over het trafo-oppervlak geplaatst, zodat ook varianten met opvullingen of wipeouts de groene lijnen zichtbaar houden. De hatches gebruiken dezelfde bloktransformatie, rotatie en schaal als de trafo.

Wanneer het bronblok geen herkenbare rechthoekige omtrek bevat, probeert de plugin een begrenzing uit de aanwezige geometrie af te leiden. Als ook die ontbreekt, verschijnt een duidelijke foutmelding in plaats van onjuist berekende aankoopruimte.

## De template
**Blocks → Template openen** opent niet langer direct de beschermde pakket-DWG, maar een bewerkbare kopie:

`%LOCALAPPDATA%\\Techbase\\Templates\\Techbase template LS en OV AANKOOP.dwg`

De aankoopvarianten worden daarin opgeslagen als echte DWG-blokdefinities met hun achtervoegsel **AANKOOP**. Ook eigen geregistreerde trafoblokken worden opgenomen wanneer die beschikbaar zijn. De originele `Techbase template LS en OV.dwg` in het installatiepakket blijft intact. Je kunt de varianten ook met AutoCAD **INSERT** kiezen nadat de kopie is geopend.

## Samenwerking Stationslocaties en ZRO
Bij **Stationslocaties → Nieuwe locatie** kies je een originele, AANKOOP- of zelf geregistreerde trafo en plaats je hem met de bestaande AutoCAD-voorbeeldplaatsing. Het detailblad en de C1–C4-tabel blijven gekoppeld aan deze blokreferentie.

Bij **ZRO-bladen → Nieuw ZRO-blad** kies je óf een nieuwe trafo uit dezelfde lijst, óf een reeds geplaatste trafo. Wanneer je de aanvraag annuleert, wordt een eventueel nieuw geplaatst blok weer verwijderd. De ZRO-viewports behouden de schalen 1:100 en 1:1000, met de automatische gebieds- en perceelgegevens. Het ZRO-kaartbeheer maakt uitsluitend **PDOK Topografie en Percelen** als losse XREF's; KLIC blijft bij Stationslocaties beschikbaar maar verschijnt niet in ZRO.

## Automatische en handmatige controle
De GitHub CI bouwt AutoCAD R25/R26 en vergelijkt de brongeometrie voor de groene aankoopvariant met de ingepakte stationsgeometrie. Een visuele controle in AutoCAD met `PACTO 25 exc. afm. AANKOOP`, een eigen trafoblok en een opgeslagen AANKOOP-template blijft noodzakelijk voordat de geometrie formeel wordt toegepast voor aankoop of kadastrale uitgifte.
