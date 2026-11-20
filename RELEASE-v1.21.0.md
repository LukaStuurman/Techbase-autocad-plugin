# TechBase AutoCAD 1.21.0 — ZRO-bladen op basis van de Bakel-DXF

## Nieuwe achtste werkruimte: ZRO-bladen
- Gebruik **Nieuw ZRO-blad** om in modelruimte een trafoblok te selecteren en een blad per trafo te maken.
- Gebruik de layout **TRAFO-10** van het voorbeeldmodel als standaard. **TRAFO-3** is afwijkend en wordt niet als sjabloon gekozen.
- In een bestaande project-DWG die TRAFO-10 bevat wordt dat blad rechtstreeks gekopieerd. In andere tekeningen vraagt de plugin één keer om een ZRO-DWG/DXF-bron met TRAFO-10 en onthoudt het gekozen bestand.
- Kopieert de opmaak met de blokken `Kader-A4s-MSR`, `Enexis-OH-MSR` en `Enexis-Noordpijl`.
- De bovenste viewport blijft op **1:100**, de onderste viewport op **1:1000**.

## Automatisch invullen en controleren
- Type station = naam van het gekozen trafoblok.
- Oppervlakte in m² = oppervlakte van de grootste gesloten vierhoek met rechte hoeken in het blok; waarde blijft aanpasbaar voor bijzondere blokken.
- C1–C4 = vier hoekpunten na de actuele positie, rotatie en schaal van de trafo.
- Projectomschrijving = `projectcode - trafonaam`; adres = straat, huisnummer en eventuele letter/toevoeging.
- Gewone gemeente en kadastrale gemeente zijn afzonderlijke velden. Sectie en nummer komen van het perceel.
- Eigenaarregels 1–3, afdeling, e-mailadres, tekeningnummer, stationsnummer en tekenaar worden gecontroleerd of handmatig ingevuld.
- De ingevoerde gegevens worden per ZRO-layout in de DWG bewaard en kunnen achteraf met **Bladgegevens wijzigen** worden aangepast.

## Perceelnummers en kaartreferenties
- Blauwe sectie/perceelnummerteksten in de bovenste kaart: aanwezige `SKG-PERCEELNUMMER-SO`-blokken worden hergebruikt; ontbrekende labels worden bij beschikbare internetverbinding via PDOK gevraagd.
- De plugin voorkomt dubbele blauwe labels bij bestaande voorbeeldblokken en kan nieuwe labeltekst op een eigen laag zetten die in de onderste viewport bevroren is.
- Topografie, percelen en KLIC kunnen via het bestaande Stationslocaties-kaartreferentiescherm als afzonderlijke DWG-XREF's worden gekoppeld.

## Installeren en valideren
Download `Techbase.Blocks.1.21.0.zip`, pak uit, sluit AutoCAD/Civil 3D en voer `Installeren.cmd` uit. Herstart AutoCAD. Open `TECHBASE` en kies `ZRO-bladen`, of voer `TBZROBLADEN` in.

De code compileert op R25 (.NET 8, AutoCAD/Civil 3D 2025–2026) en R26 (.NET 10, AutoCAD/Civil 3D 2027). De werkelijke ZRO-generatie, layout-import, PDOK-resultaten en plotweergave moeten nog op een AutoCAD-project-DWG worden getest. Bronmodel: `Bakel ZRO AANKOOP DEELGEBIED 1.dxf`. Details staan in `docs/ZRO-Bakel-voorbeeld.md`.
