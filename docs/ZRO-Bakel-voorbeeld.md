# ZRO-bladen — Bakel-model

## Bron en werkwijze
Deze functionaliteit is ontworpen op basis van de door de gebruiker aangeleverde DXF `Bakel ZRO AANKOOP DEELGEBIED 1.dxf`. **TRAFO-10** en **TRAFO-4** zijn de standaardvoorbeelden; TRAFO-3 is afwijkend en wordt niet als standaardsjabloon gebruikt.

De ZRO-werkruimte is een apart achtste onderdeel van TechBase. Met **Nieuw ZRO-blad** kies je een trafoblok uit dezelfde bibliotheek als bij Stationslocaties (standaard **Pacto 25 AANKOOP**) en plaats je dat interactief in modelruimte, óf selecteer je een reeds geplaatst blok. De plugin bepaalt het bloktype, zoekt de grootste gesloten rechthoek binnen het blok, berekent de oppervlakte en de RD-hoekpunten en laat de projectgegevens controleren. Daarna wordt een blad met naam `ZRO-<trafonaam>` aangemaakt.

Wanneer de huidige DWG al een layout `TRAFO-10` bevat, kopieert de plugin dat blad. In een nieuwe DWG kies je bij het eerste gebruik het Bakel-voorbeeldbestand (DWG of DXF) als bron; de locatie wordt voor volgende AutoCAD-sessies bewaard in `%APPDATA%/Techbase/zro-template-path.txt`. De bron wordt niet in het nieuwe model ingeladen als kaartachtergrond: alleen de **papier-entities en kaderblokken** worden gekopieerd. Topografie/percelen kunnen via het aparte PDOK-**kaartreferenties**-scherm (zonder KLIC) als aparte DWG-XREF worden opgebouwd.

## Bladopbouw, gevalideerd in de DXF
De standaardbladen zijn A4 staand met de blokken `Kader-A4s-MSR`, `Enexis-OH-MSR` en `Enexis-Noordpijl`. Zij bevatten twee kaartvensters:

| Kaart | Papierformaat viewport | Schaal |
| --- | --- | --- |
| Boven, situatie | ca. 190 × 140,5 mm | **1:100** |
| Onder, overzicht | ca. 135 × 94,5 mm | **1:1000** |

De template bevat verder de legenda voor benodigde grond en kadastrale grens. De plugin vervangt alle relevante attribuutwaarden, niet de bestaande stijl van de kaders.

## Attributen
| Blok / attribuut | Waarde |
| --- | --- |
| Kader `KAD.GEM` | Kadastrale gemeente van perceel via PDOK, daarna te bewerken |
| `SECTIE.L`, `SECTIE.NR` | Sectieletter en perceelnummer van perceel bij station |
| `EIGENDOMR1` | Naam eigenaar, **handmatig** |
| `EIGENDOMR2` | Straat + huisnummer/toevoeging eigenaar, handmatig |
| `EIGENDOMR3` | Postcode + plaats eigenaar, handmatig |
| `OPP.IN.M` | Oppervlakte van grootste gesloten rechthoek in gekozen trafoblok, in m²; controleerbaar |
| `type.stat` | Naam van gekozen trafoblok |
| `COÖRDINAAT_C1_X/Y` t/m `C4_X/Y` | Vier hoeken uit actuele bloktransformatie, in RD New |
| `schaal.sit`, `schaal.over` | 1:100 en 1:1000 |
| `TEKENINGNUMMER:` | Handmatig invoeren |
| `EMAILADRES:` | Handmatig invoeren |
| Afdeling | Handmatig; in originele bron een **statische tekst**, via een eigen papierruimte-overlay aangepast |
| `OMSCHRIJVING{108}` | `<projectcode> - <trafonaam>` |
| `ADRES{108}` | BAG-straatnaam + huisnummer en toevoeging |
| `GEMEENTE{108}` | `<plaatsnaam> gem. <normale gemeente>` |
| `MSR.NR.{55}` / `STATIONSNUMMER` | Stationsnummer |
| `GET.{33}` | Voorletter + achternaam van tekenaar |
| `DATUM` | Invulbaar, met huidige dag als standaard |

Alle invoerwaarden worden in de DWG per ZRO-layout bewaard, zodat ze via **Bladgegevens wijzigen** te bewerken zijn. Bij **Blad bijwerken** wordt de gekoppelde trafopositie opnieuw gelezen en worden de kaartviewports op de twee vaste schalen ingesteld.

## Perceelnummers en achtergrond
In de bron-DXF staan blauwe `SKG-PERCEELNUMMER-SO`-blokken op de kadastrale laag met teksthoogte 0,9 m. Deze worden niet dubbel getekend. Wanneer ze ontbreken, vraagt de plugin de nabijgelegen percelen uit de officiële PDOK BRK OGC Features API en schrijft blauw tekstlabel `<sectie> <nummer>` in modelruimte op een eigen laag. De laag wordt in de onderste 1:1000-viewport bevroren, zodat de grote labels voor de bovenste 1:100-kaart bedoeld zijn.

De PDOK-adresopzoeking en perceelopzoeking gebruikt RD New, `https://api.pdok.nl/bzk/locatieserver/search/v3_1/reverse`. Gegevens blijven **voorstellen**: vooral bij de perceelgrens kunnen automatisch gevonden gegevens handmatig worden gecorrigeerd. De openbare kadastrale kaart bevat geen betrouwbaar vrij toegankelijk eigenaar/contactoverzicht. Eigendom en e-mail blijven daarom bewust handmatig.

## Controles
- R25/.NET 8 en R26/.NET 10 moeten in GitHub Actions compileren.
- In echte AutoCAD: TRAFO-10 kopiëren naar een nieuw ZRO-blad, exacte viewports 1:100 / 1:1000, kader- en titelattributen invullen, C1–C4 en footprint nameten, blauwe perceelteksten en XREF-laagzichtbaarheid controleren.
- Test PDOK-foutscenario en het achteraf bewerken van gegevens.
- Controleer bij nieuw project het inladen van de oorspronkelijke DXF als externe sjabloon.

**Belangrijk:** de bron-DXF geeft een voorbeeldopmaak. De nieuwe ZRO-functie is geen juridische eigendomscontrole of vervanging van officiële kadastrale eigendomsinformatie.

## Aanvulling 1.21.1 — blokbibliotheek en ZRO-werkplaats

ZRO en Stationslocaties gebruiken dezelfde trafoblokkeuze, waaronder alle stationsblokvarianten uit de Blocks-toolpalette met de suffix **AANKOOP**. Het origineel zonder arcering blijft behouden. Nieuwe groene uitvoeringen gebruiken ANSI31, ACI 102, patroonhoek −90° en schaal 0,1. Het aankoopgebied volgt de marges van de standaard Stationslocaties-trafo. Via **Extra trafoblok toevoegen** kan een eigen blok in de gedeelde bibliotheek worden opgeslagen.

De ZRO-tab toont alleen echte aangemaakte ZRO-layouts (geen kolomkop in een lege lijst), heeft dezelfde drie actiegroepen als Stationslocaties en biedt onder **Project & kaarten** uitsluitend **Topo en Percelen**. In het ZRO-scherm wordt geen KLIC-levering opgevraagd. Topografie en kadastrale percelen worden als losse DWG-XREF's gekoppeld.
