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

## Waar staat wat in PlanetCity
`PlanetCity` is één statische partial-klasse, verdeeld over bestanden per onderwerp. Werk liefst in het bestand van je onderwerp, dan botsen sessies minder.
- `PlanetCity.cs`: de kern: `Cells`, `ResetAll`, `Place`, `PlaceLine`, `Clear`/`ClearFrom`, `Touch`, `ClearAll`, `Slot`, `StartTownVersion`, `CanPlace`, de klasse `Wieken`.
- `PlanetCity.Terrain.cs`: `Terr`, `G`, `SetTerr`, hoekpunten (`CornerH`, `CornerBase0`, `CornerOfs`, `NudgeEdge`), `LineGround`, `fieldCells`, de hoogtefunctie (`Seg`, `Cap`, `FieldK`, `GroundField`, `UseNear`).
- `PlanetCity.Cuts.cs`: geulen en dijken (`Cut`, `Carve`, `Fill`, `preCarve`, `preLevel`), de precieze bodem erlangs (`ExactGround`, `CutsNear`) en `CollectCuts`.
- `PlanetCity.Levels.cs`: niveaus (`WalkLevel`, `LevelTarget`, `SetLevel`, `DipUnder`, `CrossingPreview`), `CarvePaths`, `UpdateCuts`, `FloorH`, `SurfaceH`.
- `PlanetCity.Graph.cs`: de wegengraaf (`Links`, `Partners`, `Under`, `Low`, `Style`, `OneWay`, `Bike`, `TunnelLinks`, `Drag*`), halve vakken (`wegdeel`), `LinePlan`.
- `PlanetCity.Save.cs`: `Save`, `Load`, `Snapshot`, `Restore` (ook voor undo), `PSaveData`.
- `PlanetCity.Sample.cs`: het voorbeeldstadje `Sample`.
- `PlanetCity.Ground.cs`: `Init`, raster, evenaar, `LatLon`, `BuildGround` (met `groundCache`), `TerrColor`, de poolpaal.
- `PlanetCity.Shapes.cs`: `Frame` en de bouwstenen voor meshes (muren, daken, kegels, molenwieken, stroken).
- `PlanetCity.RoadDraw.cs`: wegen tekenen (`Dims`, stoeprand, stoep, middenberm, palen, `RoadNode`).
- `PlanetCity.Bridges.cs`: koker (`TubeSeg`), `TerrainAt`, `BridgeBlock`, `Portal`, schuine kruisingen (`Crossing`), `UnderMesh`.
- `PlanetCity.Draw.cs`: `Lake`, `ColorOf`, `Rebuild`/`RebuildSome`/`RebuildSteps`, `GhostMesh`, `BuildCell`, `BuildChunk`.
- Verder: `PlanetCity.Scorch.cs`, `PlanetTunnels.cs`, `PlanetRoads.cs`, `PlanetPaths.cs`, `PlanetNature.cs`, `PlanetRoundabout.cs` en andere `Planet*.cs` met `partial class PlanetCity`.

## Stand van zaken (25 oktober 2026)
Al aanwezig in `Assets/Scripts/`: de planeet (`Planet`, `PlanetBuilder`, `PlanetCity`, `PlanetLife`, `PlanetWalker`), het platte raster (`Hex`, `City`, `Builder`), wegen (`RoadMesher`, `RoadRules`), verkeer (`Cars`, `Traffic`), mensen (`People`, `Chatter`, `Babble`), weer en dag/nacht, geluid (`Ambience`) en interface (`Ui`, `Beauty`).

Erbij gekomen op 25 oktober 2026:
- Bouwblokken winkel, plein, toren en molen (draaiende `Wieken`), ook in de schoonheidsscore en de praatjes. Bloktypes staan in `Blocks` (`City.cs`), tekenen per blok in `PlanetCity.BuildChunk`.
- Polen gemarkeerd: `Planet.North`/`South`/`IsPole`, gestreepte paal met vlag op een ijswit vak; toets N springt ernaartoe (P is pauze, zoals de webversie).
- Wegprofiel van de webversie: asfalt 10 cm, schuine stoeprand 30 cm, stoep 25 cm; geen stoep over het asfalt van een andere arm, ronde stoephoeken op kruisingen.
- Terrein (low poly, als `07b-terrein.js`): hoogte per vak −12 tot 90 m in `PlanetCity.Terr`, bebouwde vakken op een sokkel, wegen hellen mee (`Frame.H`, `PlanetCity.SurfaceH`). Boetseren met het gereedschap Terrein (toets 9): ophogen, afgraven, egaliseren, gladstrijken, helling; penseel 1/7/19/37 vakken.

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

Erbij gekomen op 1 november 2026 (natuur, `PlanetNature.cs`):
- Bomen los op het terrein, zonder grasplaat of sokkel (park, meer en gracht zijn geen plateau: `OpenGround`): loofboom, den, berk en struik op ware grootte, soort naar hoogte, helling en een bosruis; boomgrens 48 m. Op bomen kun je bouwen: een vak met alleen bomen maakt plaats voor wat je neerzet (`TreesYield`), een daktuin niet.
- Zeeniveau (`SeaLevel` = −1 m): waar het maaiveld lager ligt staat water (`SeaChunk`), met zand en riet langs de waterlijn; wegen tellen niet mee. Meer: het vak ligt vanzelf op `LakeBed` (via `G`), een weg erover is een dam (`DamSkip`). Gracht: bodem en bakstenen kademuren (`Canal`, `Quay`), de buren houden hun hoogte.
- Bergen tot 90 m (`TerrTop`): rots op steile flanken, sneeuw boven 54 m. Hele planeet vol maakt berggebieden zonder stadjes (`MountainMask`). De camera volgt het maaiveld vloeiend (`camH`) en blijft boven een bergflank.

Erbij gekomen op 1 november 2026 (wegen en verkeer):
- Wegen, paden en fietspaden zijn een graaf; breedtes sluiten aan op de knopen. Kruisingen zijn standaard gelijkvloers; Ongelijkvloers (U) maakt een brug of onderdoorgang, ook schuin. Een schuine weg over een rechte weg krijgt zijn kruispunt op de rand tussen twee vakken (`NodeDir`). Wegen en paden kunnen door een bos; de bomen wijken.
- Rotonde (blok in Wegen, `PlanetRoundabout.cs`): over een vak en de zes eromheen, maten van de webversie, verkeer tegen de klok in met voorrang. Zebrapaden op kruisingsarmen en oversteken, verkeerslichten op de stoep, lantaarns om en om.
- Rijden met fysica (`PlanetLife.Physics`): twee rijstroken, voorrang (brede weg, dan rechts), stopstreep of haaientanden, auto's met losse wielen. Een auto per drie wegvakken (hooguit 40). Middenberm van een laan eindigt met een ronde kop bij een kruising.
- Nieuwe startstad (`StartTownVersion`): een laan als rondweg met keerkoppen binnenin. Menu Raatstad > Teststad (verkeersmeting, auto's bekijken) en Raatstad > Wegvoorbeeld (wegbreedtes, fietspaden, rotondes).
- De hoogtefunctie langs wegen op een niveau (`GroundField`, `LineGround`) staat als JavaScript in de webversie: `src/js/07a-terreinveld.js` in `joepk1989/raatstad-web` (nog niet aangesloten; uitleg in `docs/terreinveld.md` daar). Daar is het veld doorlopend gemaakt: een weg graaft pas in als zijn wegdek onder het terrein ligt en een kop ligt ook 5 cm onder het wegdek. In Unity zit op die plek nog een sprongetje van 5 cm.

Erbij gekomen op 1 november 2026 (dorpen, `PlanetVillages.cs`):
- Hele planeet vol maakt vijf soorten: gehucht (boerderijen, molen, boomgaard), kerkdorp (lintdorp met kerk), brinkdorp (driehoekige brink), marktstad (`Sample`) en 4 grote steden (ruit van straten onder 60°, hoge kantoren in het centrum). Aan de voet van de bergen alleen gehuchten.
- Wegen tussen de dorpen sluiten liefst aan op doodlopende einden (`Connect` met `from`); een overgebleven eind krijgt een weg naar het dorp in die richting.

Erbij gekomen op 2 november 2026 (terrein):
- Geen sprongetje van 5 cm meer waar een verdiepte weg het terrein raakt (zoals de webversie): een weg graaft pas in als zijn wegdek (5 cm onder de weg) onder het terrein ligt, een kop ligt ook 5 cm onder het wegdek; `LineGround` snijdt op de voet van het talud met het terrein als vlak over de driehoek.
- Een gewone weg op een helling ligt niet meer in een kom: een hoekpunt tussen twee verbonden weg- of padvakken ligt op weghoogte (`RoadCorner` in `CornerBase0`) in plaats van op het laagste van de drie vakken. Een pad (geen weg) laat de helling ernaast liggen: zo'n hoekpunt ligt op het vlak door de drie vakken, nooit hoger dan het pad. Langs een schuin pad (90°) zijn de lege halve vakken geen plaat meer (`PathHalf`) en liggen de twee hoekpunten op de lijn van het pad op padhoogte (`SkewPathCorner`).
- Gereedschap Terrein toont de hoekpunten rond de muis (`PlanetBuilder.ShowVerts`): wit een hoekpunt, oranje zelf verzet met Rand, blauw het midden van een vak.
- Geen bruine driehoek meer naast de stoep bij een schuine onderdoorgang: op de bol liggen twee stukken van een rechte weg nooit precies op één lijn, een punt op de wegrand naast een hart viel net buiten beide; een punt hoort nu tot 25 cm voorbij het eind nog bij een stuk (`EndSlack` in `GroundField`).

Erbij gekomen op 2 november 2026 (tunnels, `PlanetTunnels.cs`):
- Een gesleepte weg of pad op het maaiveld gaat vanzelf door een berg (`BoreLine`, na `DipUnder`): op een recht stuk de langste tunnel waarbij elk vak ertussen minstens 2,25 m boven de rechte weg ligt en ergens 9 m (met Ongelijkvloers U: 6 m), minstens twee vakken berg, hooguit 15% helling. In de graaf een `TunnelLinks`-verbinding van portaalvak tot portaalvak (`IsBore`; `MidCell` geeft alleen nog buren op afstand 2); de vakken ertussen worden weer terrein met hun hoogte van voor de weg (`RestoreLevel`).
- Getekend naar het model `tunnel entrance.dae` (hexgrid city concept, iets anders dan de brug): de koker (`TubeSeg`, in stukken van 6 m) en het portaal (`Portal`) van de onderdoorgang, met eromheen een zeshoekige mantel (`Mantle`: 2,17 m boven en onder de koker, 3,75 m opzij, zijden onder 30°) in plaats van het betonblok, in een vlak op de bol dat met de weg meehelt (`Frame.Lift`/`Incline`). De mantel steekt uit de helling; alleen het terrein binnen de mantel wordt weggesneden (`BoreTri`, zeven vlakken, vakken in `boreNear`). Een portaalvak telt niet als weg op een niveau (`BorePortal` in `WalkLevel`). De portalen staan vooraan, op de rand van het portaalvak bij de voet van de berg; daar steekt het blok met gras erop uit de helling (geprobeerd en weer weggehaald: portalen diep in de berg met een inrit tussen keermuren, dat verwoestte te veel terrein). Geen lantaarns of bermbomen in een tunnel (`InBore` in `PlanetRoads`); wel tunnellichten: om de 6 m twee lichtbalken tegen het plafond, boven elke rijstrook (materiaal `lamp`, geen echte lampen). De hoogtefunctie slaat tunnels over. Verkeer rijdt erdoor via `SurfaceH` (helt van hart tot hart); lopen (L) gaat erdoor via `PlanetCity.InBore` (de vloer van de koker in plaats van het maaiveld in `PlanetWalker`).
- Tijdens het slepen toont het voorbeeld de tunnel al (`PlanetCity.BorePlan`, dezelfde regels via `BoreSearch`; `PlanetBuilder.TunnelGhost`): het betonblok met portalen in amber; het weg-voorbeeld (`GhostLine`) stopt bij de portaalvakken.

Erbij gekomen op 2 november 2026 (lucht, `PlanetSky.cs`, shaders in `Assets/Resources/Lucht`):
- Skybox `Raatstad/Lucht`: verloop van de kim naar boven rond de plaatselijke 'boven', de kim zakt mee met de hoogte van de camera; zon met gloed, oranje bij zonsopgang en -ondergang, sterren 's nachts. De kimkleur is `cam.backgroundColor` van `DayNight` (die ook de mist kleurt), bewolking maakt hem grijs.
- Van ver weg (camera hoger dan zo'n 300–1100 m): zwarte ruimte met sterren en zon, een dunne blauwe atmosfeerrand (`Raatstad/Atmosfeer`, bol tot 260 m, schaalhoogte 40 m) en een wolkenlaag op 200 m (`Raatstad/Wolken`, ruis per richting, meer wolken bij slechter weer) die in 25 minuten om `Planet.Axis` draait. Bij inzoomen vervagen wolken en rand en worden ze niet meer getekend; de mist van `Weather` schuift hoger op weg en is in de ruimte uit.
- Muziek, berekend bij de start op de achtergrond (`RenderedMusic`, geen opnames): op de planeet bouwmuziek (`BuildMusic`, keuze in het menu onder Bouwmuziek, onthouden in PlayerPrefs: Uit, Vrolijk (marimba, 120 bpm), Chill (elektrische piano met swing, 84 bpm), 8-bit (blokgolven, 140 bpm), Folk (gitaar en fluitje, 100 bpm); elk 16 maten met een melodie A A' B A; een andere stijl wordt op de achtergrond uitgerekend en vloeit in; 's nachts zachter), in de ruimte een rustig stuk (`SpaceMusic`: zachte akkoorden met belletjes op een zestonige ladder, galm; 64 s). Ze vloeien in elkaar over met `PlanetSky.Space`; de wind van `Ambience` valt weg in de ruimte. Geluid uit zet ook de muziek uit.

Erbij gekomen op 3 november 2026 (geluid, `Sounds.cs`, naar `13-camera-modus-geluid.js` van de webversie):
- Effecten, berekend bij de start: `Sounds.Fx(naam)` in je oor of `Fx(naam, plek, sterkte, ref)` zachter op afstand (inverse afstand vanaf ref m; met ref < 50 m stil in de ruimte), per naam een minimale tussentijd. Namen: place, remove, earth (terrein), deny, click, undo, whoosh (lopen/cameramodus), gen (startstad, planeet vol), coin (schoonheid omhoog), cricket, crunch, squeal, splash, thunder.
- Bouwen en slopen: `PlanetBuilder.Push` roept `Sounds.Before`, `Changed` roept `Sounds.After` aan; die tellen de inhoud van de stad (meer = place, minder = remove, alleen terrein = earth); undo en gen gaan voor (`Sounds.Instead`). Elke knop in `Ui` klikt.
- Omgeving die de plek volgt (vakken rond de camera): regen, windvlagen bij storm en regen, donder na de bliksem, golfjes bij water en meren, krekels 's nachts op gras en in parken (stil bij regen). Alles valt weg met de hoogte van de camera en in de ruimte; Geluid uit (`Ambience.Muted`) zet alles uit.
- Voetstappen (`Footsteps` in `Ambience.cs`): zachter, hak en bal van de voet, asfalt een plof met een tikje, gras en zand korreltjes, zes varianten.
- Nog niet aangeroepen: crunch (botsen, sterkte = impact/12) en squeal (slippen) in `PlanetLife`; aanroep `Sounds.Fx("crunch", plek, sterkte, 12f)`.

Erbij gekomen op 3 november 2026 (leven rond de kijker, `PlanetLife.Crowd.cs`):
- Op een grote planeet (vanaf 400 weg- en padvakken) lopen mensen, auto's en fietsers alleen binnen 140–320 m van waar de camera naar kijkt (straal groeit met de hoogte; lopend met L 170 m rond de wandelaar). Wie te ver weg en buiten beeld is, komt elders in de buurt terug, ook buiten beeld; alleen bij het vullen (bijna niemand in de buurt) verschijnen ze ook in beeld.
- Dichtheid per vak met het kwadraat van de bebouwing eromheen (`Busy`: winkel en plein 5, kantoor 2, toren 1,5, woning 1 per laag; buren en vakken op afstand 2 half): druk in steden, rustig in dorpen, een enkeling op het platteland. Hooguit 150 mensen, 70 auto's, 30 fietsers. Boven zo'n 380–700 m hoogte (en in de ruimte) niemand.
- Een kleine stad (de startstad, teststad, wegvoorbeeld) werkt zoals vroeger: over de hele stad, hooguit 40 mensen, 40 auto's, 20 fietsers.
- Gemeten op een volle planeet, grote stad: 115 mensen en 43 auto's, 157 fps (zonder leven 341 fps).

Erbij gekomen op 3 november 2026 (meteoor, `PlanetMeteor.cs`, experimenteel):
- Menu Gereedschap of spelmodus (ook vanuit de ruimte): een meteoor op het midden van het beeld, klein, middel of groot (`meteorSize` in `PlanetBuilder.Meteor.cs`; kom van 1, 7 of 19 vakken, 6/11/17 m diep, met opgeworpen rand en puin). Rots met vuurstaart en rookspoor, feller in de atmosfeer; bij de inslag flits, zeshoekige schokgolf, een puingordijn en stofzuil omhoog, brokstukken en een schuddende camera. Auto's vliegen weg en mensen gaan neer (`PlanetLife.Blast`).
- De krater (`PlanetMeteor.Crater`) holt het terrein uit via `SetTerr`; gebouwen, wegen en bomen in kom en rand verdwijnen. Verschroeid (`PlanetCity.Scorch`, in opslag en undo): donkere kom, lichtbruine rand en een zwarte roetdeken die naar buiten rafelt (`ScorchTag` in `NatureTag`). De kom blijft droog, ook onder zeeniveau (`DryPit` in `Wet`); met water vlak naast breekt de wand na een paar seconden door, een inslag in het water is meteen nat. Ctrl+Z zet alles terug.
- Geluid in stereo, zelf berekend (`PlanetMeteor.Sound.cs`): gebrul en supersone knal tijdens de vlucht; klap, dreun, brokstukken en gerommel met echo's; plons en stromend water. Op afstand later (343 m/s), zachter en dof (laagdoorlaatfilter per laag).

Nog te doen:
1. Modules als JSON met edge-codes (nu nog in code)
2. Onderdelen uit `docs/overdracht-webversie.md` bouwen: stadsgenerator, wenteltrap; geluid: crunch/squeal aanroepen in `PlanetLife`, jump in `PlanetWalker`
3. Laadscherm (`Assets/Scripts/UI/HexLoadingScreen.cs`) in de startscène hangen
4. Terrein: hoogtelijnen tijdens het boetseren; tunnels door hoog terrein (bestaande koker, portalen en `TunnelLinks` hergebruiken)
5. Ragdolls voor mensen die bij de chaos worden geraakt
6. Tunnels: een tunnel komt alleen bij het leggen (niet als je later een berg boetseert over een weg)
