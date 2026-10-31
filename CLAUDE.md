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
- Polen: de as (`Planet.Axis`) gaat door de middens van twee tegenover elkaar liggende icosaëdervlakken. Noord- en zuidpool zijn dus twee startplekken (`Planet.North`/`South`), en noord en zuid zijn elkaars spiegelbeeld. De evenaar is de rand tussen de vakken van beide halfronden (`PlanetCity.NorthSide`); breedte en lengte via `PlanetCity.LatLon`.

## Werkwijze
- Bouw in kleine, speelbare stappen. Elke stap moet in Play Mode werken voordat de volgende begint.
- Pas scripts gericht aan, herschrijf geen hele bestanden zonder reden.
- Na een scriptwijziging: wachten tot Unity klaar is met compileren, dan de Console lezen en fouten oplossen.
- Commit na elke werkende stap met een duidelijke commitboodschap.
- Geen code in `Library/`, `Temp/`, `Logs/` of `UserSettings/`.

## Stand van zaken (25 oktober 2026)
Al aanwezig in `Assets/Scripts/`: de planeet (`Planet`, `PlanetBuilder`, `PlanetCity`, `PlanetLife`, `PlanetWalker`), het platte raster (`Hex`, `City`, `Builder`), wegen (`RoadMesher`, `RoadRules`), verkeer (`Cars`, `Traffic`), mensen (`People`, `Chatter`, `Babble`), weer en dag/nacht, geluid (`Ambience`) en interface (`Ui`, `Beauty`).

Erbij gekomen op 25 oktober 2026:
- Bouwblokken winkel, plein, toren en molen (draaiende `Wieken`), ook in de schoonheidsscore en de praatjes. Bloktypes staan in `Blocks` (`City.cs`), tekenen per blok in `PlanetCity.BuildChunk`.
- Polen gemarkeerd: `Planet.North`/`South`/`IsPole`, gestreepte paal met vlag op een ijswit vak; toets N springt ernaartoe (P is pauze, zoals de webversie).
- Wegprofiel van de webversie: asfalt 10 cm, schuine stoeprand 30 cm, stoep 25 cm; geen stoep over het asfalt van een andere arm, ronde stoephoeken op kruisingen.
- Terrein (low poly, als `07b-terrein.js`): hoogte per vak 0–30 m in `PlanetCity.Terr`, bebouwde vakken op een sokkel, wegen hellen mee (`Frame.H`, `PlanetCity.SurfaceH`). Boetseren met het gereedschap Terrein (toets 9): ophogen, afgraven, egaliseren, gladstrijken, helling; penseel 1/7/19/37 vakken.

Erbij gekomen op 28 oktober 2026:
- Bediening gelijk aan de webversie: Q/E draaien, WASD/pijltjes schuiven, +/− zoomen, rechts of Shift slepen schuift, middelste knop of Alt slepen draait; P pauze, O weer, N polen, I eenrichting, M menu, ? bediening. Het venster Bediening (`PlanetBuilder.Help`) hoort gelijk te blijven met de code.
- Wegen en paden op een niveau (`PlanetCity.DragLevel`, Page Up/Down, halve stappen van 2,25 m): het terrein langs zo'n weg komt uit één hoogtefunctie (`PlanetCity.GroundField`, zoals Cities: Skylines 2): onder de weg plus een marge op weghoogte, daarbuiten onder een talud van 1 op 1,5 (34°, schuifje Talud: `PlanetCity.FieldK`) terug naar het terrein; uiteinden, bochten en splitsingen krijgen een zeshoekige kop; de vakken erlangs worden gesneden op de knikken van die functie (`LineGround`), zodat de kanten strakke vlakken zijn en gedeelde randen kloppen. Een kruising met een weg op een ander niveau wordt vanzelf ongelijkvloers (`SetLevel`); een bestaande weg wordt nooit verzet. Weghalen zet het terrein terug (`preLevel`). Een brug over een weg of pad op een niveau volgt het model `overpass_path.dae` (Google Drive, hexgrid city concept): zeshoekige koker, portalen met borstwering en één betonblok van portaal tot portaal (`PlanetCity.BridgeBlock`), los van het terrein: het terrein is een hoogteveld en loopt als bak onder de brug door; de bovenkant van het blok snapt op het maaiveld (`TerrainAt`). Automatische test: `Assets/Scripts/LevelTest.cs` (niet in git) met `niveautest.txt` in persistentDataPath.

Erbij gekomen op 31 oktober 2026:
- Hele planeet vol (menu, Gereedschap; `PlanetFill.cs`): voorbeeldstadjes (`Sample` met `rich`: ook winkels, pleinen, torens) over de hele bol, verbonden door wegen (kortste opspannende boom, route over lege vakken met een bochtstraf), daartussen platteland (heuvels, bossen, meertjes, molens; vlak langs de wegen, op een talud alleen bomen). Achter het laadscherm (`HexLoadingScreen`) in stappen (`FillSteps`, `RebuildSteps`).
- Een weg onder een brug ligt altijd op volle diepte (vaste brugmaat); de helling van een verdiepte weg begint pas na de brug.
- Snelheid: delen van 128 vakken (`ChunkN`), het raster wordt per vak bijgewerkt, tekenen kijkt alleen naar wegstukken in de buurt (`UseNear`, onthouden per `fieldVer`), de grond langs een weg op een niveau wordt onthouden (`groundCache`, sleutel `FieldKey`). Een klik op een volle planeet kost zo'n 0,15–0,25 s.

- Startstad (`PlanetStartTown.cs`, menu: opnieuw beginnen, de planeet eerst leeg): plein met winkels, op 4 vakken een verlaagde zeshoekige ring voor voetgangers en fietsers (4,5 m diep), zes paden haaks naar buiten die schuin over het raster onder de ringweg (7 vakken) door gaan, met zes bruggen. Een klik tekent eerst het deel onder de muis, de rest volgt in de volgende beelden (`RebuildSome`). De zijkanten van een geul zijn even steil als het talud (`SideK` = `FieldK`).

- Fiets- en voetpaden (`PlanetCity.Bike`, padgereedschap B): breder pad met een rood fietsdeel; voetgangers lopen rechts op de zandstrook. Fietsers (`PlanetLife.Bikes.cs`) rijden alleen over fietspaden; de startstad heeft fietspaden in de ring en de zes paden.

- Mensen: realistische figuren uit Microsoft Rocketbox (MIT, `Assets/Resources/Mensen`, licentie erbij) met Rocketbox-animaties (lopen, stilstaan, zwaaien, zitten), als Humanoid geïmporteerd (`Assets/Editor/MensenImport.cs`), afgespeeld in `Rocketbox.cs`. Voetgangers sturen vloeiend naar hun route en wijken voor elkaar uit (`PlacePerson`). Fiets- en voetpad 6,4 m breed (3,2 m fietsdeel).

- Fietsers houden het stuur vast en trappen mee: na de animatie buigen arm en been (twee botten, cosinusregel) naar handvat en pedaal (`PlanetLife.LateUpdate`); omafietsstuur, trappers draaien met de wielen mee.
- Voertuigen en rijgedrag van de webversie: zeven low-poly auto's uit zeshoekige doorsneden; netjes verkeer komt los achter een stilstaande auto; Totale chaos (menu) geeft elke auto een persona (racer, ontwijker, jager), met slippen, botsen, schade, brand en mensen omverrijden.
- Interface: zes vaste bouwcategorieën (1–6) met een lade, Tab vasthouden voor een zeshoekig keuzewiel, middelste klik als pipet, vooraf het effect op de schoonheid bij de muis en een TIP voor het zwakste onderdeel; blokpatronen, laagkiezer, uitleg bij aanwijzen (`Ui.Tip`), interfacegrootte in het menu, ongelukjesteller.

Nog te doen:
1. Modules als JSON met edge-codes (nu nog in code)
2. Onderdelen uit `docs/overdracht-webversie.md` bouwen: stadsgenerator, wenteltrap, geluiden
3. Laadscherm (`Assets/Scripts/UI/HexLoadingScreen.cs`) in de startscène hangen
4. Terrein: hoogtelijnen tijdens het boetseren; een weg tussen twee vakken met een groot hoogteverschil ligt nog in een kom; bruine driehoek naast de stoep bij een schuine onderdoorgang
5. Ragdolls voor mensen die bij de chaos worden geraakt
