# Raatstad (Unity)

3D-citybuilder met stapelbare hexagon-blokken waar je zelf doorheen kunt lopen.
De wereld is een bol (planeet). Alles is gebaseerd op hexagons en driehoeken.

## Techniek
- Unity 6000.6.7f1 (Unity 6.x), Universal Render Pipeline (URP)
- Taal: C#
- Doelplatform: pc. De browserversie (raatstad-web, eerst hexstad) was het eerste speelbare prototype.
- De Unity-editor is gekoppeld via MCP. Gebruik de Console om je eigen werk te controleren na elke scriptwijziging.

## Referentie
- De browserversie (repo `joepk1989/raatstad-web`, eerst `hexstad`, JavaScript) is de referentie voor hoe dingen moeten werken.
- Neem regels en data over (module-JSON, edge-codes, wegregels), vertaal de JS-code niet regel voor regel.
- `docs/overdracht-webversie.md` beschrijft wat er in de webversie is bijgekomen (maten, regels, formules): stadsgenerator, wenteltrap, voertuigen, geluid. Lees dat voordat je een van die onderdelen in Unity bouwt.

## Ontwerpregels
- Ware schaal: 1 Unity-eenheid = 1 meter.
- Alles volgt hexagon- en driehoeksgeometrie. Geen vrije vormen of willekeurige hoeken.
- Wegen lopen alleen onder 60° en 90° (hex-hoeken).
- Modules sluiten naadloos op elkaar aan via edge-codes en zijn stapelbaar.
- Modules worden als JSON-data beschreven, niet hard in code.
- Voetgangersmodules (paden, oplopende paden, kruisingen, hofjes) creëren hoogte en zijn beloopbaar.
- Standaardspelmodus heet "vrije modus". Alle spelmodi (vrije modus, duel tegen AI, multiplayer, oorlogsmodus), de volgorde en wat nog open is: `docs/spelmodi.md`. Lees dat voordat je aan een modus, scores tussen spelers of een modusmenu werkt.
- Bolwereld (`Assets/Scripts/Planet.cs`): Goldberg-veelvlak, een icosaëder verdeeld in M×M driehoeken (M = 46, straal 600 m) met 10·M²+2 vakken. Dat zijn zeshoeken plus de 12 vijfhoeken die op een bol onvermijdelijk zijn. De vijfhoeken zitten op de hoekpunten van de icosaëder. Aansluitlogica moet dus 5 of 6 buren aankunnen (`Cell.nb`).
- Polen: de as (`Planet.Axis`) gaat door de middens van twee tegenover elkaar liggende icosaëdervlakken. Noord- en zuidpool zijn dus twee startplekken (`Planet.North`/`South`), en noord en zuid zijn elkaars spiegelbeeld. De evenaar is de rand tussen de vakken van beide halfronden (`PlanetCity.NorthSide`); breedte en lengte via `PlanetCity.LatLon`.

## Werkwijze
- Bouw in kleine, speelbare stappen. Elke stap moet in Play Mode werken voordat de volgende begint.
- Pas scripts gericht aan, herschrijf geen hele bestanden zonder reden.
- Na een scriptwijziging: wachten tot Unity klaar is met compileren, dan de Console lezen en fouten oplossen.
- Commit na elke werkende stap met een duidelijke commitboodschap.
- Geen code in `Library/`, `Temp/`, `Logs/` of `UserSettings/`.
- Meldingen van de gebruiker (F8 in het spel, `Meldingen.cs`) staan in `persistentDataPath/meldingen/<nummer>-<datum>/`: lees `melding.json` (tekst, camera, vak onder de muis, gereedschap, weer, Console) en bekijk `beeld.png` en `beeld-met-interface.png`; `stad.json` is de stad op dat moment.
- Terugspelen: zet `melding 3` in `persistentDataPath/niveautest.txt`; het spel laadt die stad in testslot 9, zet camera, weer en tijd terug en schrijft `terug.png` en `terug.txt` in de map van de melding (verkeer en mensen lopen anders). `melding maak <tekst>` maakt zelf een testmelding.

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
- `PlanetCity.Modules.cs`: modules op de bol. Vakinhoud `m|id|draaiing|spiegeling|laag` (midden) en `m^|midden|laag` (buren van 7 vakken); `ModArea`, `ModPose`, `CanPlaceModule`, `PlaceModule`, `RemoveModuleAt`, `SyncModules` (één object per exemplaar).

## Modules (formaat v2 van de webversie)
- `ModuleV2.cs`: gegevens en JSON (gelijk aan de webversie), `MV2` (raster, `Codes`, `MetaOf`, `Mirror`, `StairHole`, `RoofFit`), `ModuleLib` (bibliotheek in `Assets/Resources/Modules`, eigen modules in `persistentDataPath/modules`). `ModuleJson.cs`: kleine JSON-lezer.
- `ModuleMesh.cs`: module → mesh (submesh per materiaal), onthouden per id, versie, spiegeling en dekking. `ModuleStudio.cs`: studio ver van de planeet (voorbeeldplaatjes, Raatmaker), zon op 13:00 zolang een studiocamera tekent.
- `PlanetBuilder.Modules.cs`: categorie Modules (toets 7, lade, R draait, F spiegelt). `Raatmaker*.cs`: de module-editor.
- Bibliotheek opnieuw omzetten: `node Tools/bibliotheek_omzetten.js D:/raatstad-web`.

## Stand van zaken
Wat er al is en hoe het werkt (per onderwerp, met functie- en bestandsnamen): `docs/stand-van-zaken.md`. Lees het deel van je onderwerp voordat je eraan begint, en zet daar wat erbij komt (niet in dit bestand).
Onderwerpen daarin: planeet en polen, terrein en boetseren, wegen op een niveau, bruggen en tunnels, Terrein v2, hele planeet vol en dorpen, startstad, fietspaden, mensen en verkeer, natuur en zee, lucht en muziek, geluid, leven rond de kijker, meteoor, AI-bouwer, startscherm en steden, bodem met textures, zelf rijden.

Nog te doen:
1. Alles wordt modules: de in code getekende blokken (woning, winkel, toren, molen, plein) vervangen door modules; schoonheid, AI-bouwer, drukte (`Busy`), opslaan en Hele planeet vol lezen dan modulegegevens
2. Onderdelen uit `docs/overdracht-webversie.md` bouwen: stadsgenerator, wenteltrap; geluid: jump in `PlanetWalker`
3. Steden: hernoemen op het startscherm, en de prullenbak legen of terugzetten in het spel
4. Terrein: hoogtelijnen tijdens het boetseren; tunnels door hoog terrein (bestaande koker, portalen en `TunnelLinks` hergebruiken)
5. Tunnels: een tunnel komt alleen bij het leggen (niet als je later een berg boetseert over een weg)
