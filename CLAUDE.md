# Raatstad (Unity)

3D-citybuilder met stapelbare hexagon-blokken waar je zelf doorheen kunt lopen.
De wereld is een bol (planeet). Alles is gebaseerd op hexagons en driehoeken.

## Techniek
- Unity 6.3 LTS, Universal Render Pipeline (URP)
- Taal: C#
- Doelplatform: pc. De WebGL-build is het eerste speelbare prototype.
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
- Bolwereld: noord- en zuidpool zijn speciale plekken waar het grid anders samenkomt dan zes buren. Houd daar rekening mee in de aansluitlogica.

## Werkwijze
- Bouw in kleine, speelbare stappen. Elke stap moet in Play Mode werken voordat de volgende begint.
- Pas scripts gericht aan, herschrijf geen hele bestanden zonder reden.
- Na een scriptwijziging: wachten tot Unity klaar is met compileren, dan de Console lezen en fouten oplossen.
- Commit na elke werkende stap met een duidelijke commitboodschap.
- Geen code in `Library/`, `Temp/`, `Logs/` of `UserSettings/`.

## Volgorde prototype
1. Hex-grid op de bol, inclusief de polen
2. Wegen leggen met automatisch aansluiten
3. Rondlopen (speler-controller)
4. Modules laden uit JSON
5. Laadscherm (`HexLoadingScreen.cs`)
