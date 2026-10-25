# Raatstad (Unity)

3D-citybuilder met stapelbare hexagon-blokken waar je zelf doorheen kunt lopen.
De wereld is een bol (planeet). Alles is gebaseerd op hexagons en driehoeken.

## Techniek
- Unity 6000.6.7f1 (Unity 6.x), Universal Render Pipeline (URP)
- Taal: C#
- Doelplatform: pc. De browserversie (hexstad) was het eerste speelbare prototype.
- De Unity-editor is gekoppeld via MCP. Gebruik de Console om je eigen werk te controleren na elke scriptwijziging.

## Referentie
- De browserversie (repo `joepk1989/hexstad`, JavaScript) is de referentie voor hoe dingen moeten werken.
- Neem regels en data over (module-JSON, edge-codes, wegregels), vertaal de JS-code niet regel voor regel.
- `docs/overdracht-webversie.md` beschrijft wat er in de webversie is bijgekomen (maten, regels, formules): stadsgenerator, wenteltrap, voertuigen, geluid. Lees dat voordat je een van die onderdelen in Unity bouwt.

## Ontwerpregels
- Ware schaal: 1 Unity-eenheid = 1 meter.
- Alles volgt hexagon- en driehoeksgeometrie. Geen vrije vormen of willekeurige hoeken.
- Wegen lopen alleen onder 60° en 90° (hex-hoeken).
- Modules sluiten naadloos op elkaar aan via edge-codes en zijn stapelbaar.
- Modules worden als JSON-data beschreven, niet hard in code.
- Voetgangersmodules (paden, oplopende paden, kruisingen, hofjes) creëren hoogte en zijn beloopbaar.
- Standaardspelmodus heet "vrije modus".
- Bolwereld (`Assets/Scripts/Planet.cs`): Goldberg-veelvlak, een icosaëder verdeeld in M×M driehoeken (M = 46, straal 600 m) met 10·M²+2 vakken. Dat zijn zeshoeken plus de 12 vijfhoeken die op een bol onvermijdelijk zijn. De vijfhoeken zitten op de hoekpunten van de icosaëder. Aansluitlogica moet dus 5 of 6 buren aankunnen (`Cell.nb`).
- Polen: de y-as gaat door het midden van een icosaëderrand. Omdat M even is, ligt daar een gewoon zeshoekig vak. Noordpool = `Planet.Nearest(Vector3.up)`, zuidpool = `Planet.Nearest(Vector3.down)`. Geen speciale gridlogica nodig, alleen een markering op die twee vakken.

## Werkwijze
- Bouw in kleine, speelbare stappen. Elke stap moet in Play Mode werken voordat de volgende begint.
- Pas scripts gericht aan, herschrijf geen hele bestanden zonder reden.
- Na een scriptwijziging: wachten tot Unity klaar is met compileren, dan de Console lezen en fouten oplossen.
- Commit na elke werkende stap met een duidelijke commitboodschap.
- Geen code in `Library/`, `Temp/`, `Logs/` of `UserSettings/`.

## Stand van zaken (25 oktober 2026)
Al aanwezig in `Assets/Scripts/`: de planeet (`Planet`, `PlanetBuilder`, `PlanetCity`, `PlanetLife`, `PlanetWalker`), het platte raster (`Hex`, `City`, `Builder`), wegen (`RoadMesher`, `RoadRules`), verkeer (`Cars`, `Traffic`), mensen (`People`, `Chatter`, `Babble`), weer en dag/nacht, geluid (`Ambience`) en interface (`Ui`, `Beauty`).

Nog te doen:
1. Polen markeren (paal met vlag op de twee polvakken)
2. Modules als JSON met edge-codes (nu nog in code)
3. Onderdelen uit `docs/overdracht-webversie.md` bouwen: stadsgenerator, wenteltrap, low-poly voertuigen, roekeloos rijgedrag, geluiden
4. Laadscherm (`Assets/Scripts/UI/HexLoadingScreen.cs`) in de startscène hangen
