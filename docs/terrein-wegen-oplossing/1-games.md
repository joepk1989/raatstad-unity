# How city-builder and transport games shape terrain around roads (per-game technical comparison)

Scope note: this file covers the per-game mechanisms (deform vs. follow, stamping vs. nodes vs. per-tile, bridge/tunnel/retaining-wall thresholds, structure–terrain junction, mixed-height intersections, artifacts). Each key question below is one game or engine. "Documented" = stated in the cited source; "Inferred" = my reasoning from the sources. Many primary sources (Paradox forum, Frontier CMS, TechRaptor, PC Gamer, gamedev.net blog) refused automated fetching (403 / bot check); where that happened I relied on search-result snippets and secondary mirrors and flagged it.

## Cities: Skylines 2 (Colossal Order, 2023)

### Takeaway
CS2 roads are a node/segment network with per-node elevation set in steps (1.25–10 m); the terrain is a heightmap that is flattened/clipped under the net (`FlattenTerrain` / `ClipTerrain` geometry flags) and otherwise "follows" the road, with retaining walls and bridges appearing automatically only at "significant" elevation differences; a road below roughly −12.5 m becomes a tunnel, shallower negatives are open cuts. Exact strip width and embankment slope were not found in any public source.

### Cited Findings
- Official feature page: "cut-and-fill construction for roads digging through the terrain"; negative elevation values create cut roads and "deeper cuts turn the road into a tunnel"; elevated roads need clearance below and are built "as easy as building a bridge or an overpass"; road guides show "the slope steepness from the segment's start to its endpoint, as a number" — [Colossal Order / Paradox road tools feature page](https://colossalorder.fi/?p=1547); also [Paradox Interactive road-tools page](https://paradoxinteractive.com/games/cities-skylines-ii/features/road-tools)
- First Feature Highlight (19 June 2023) covered grid tool, replace tool, intersections across existing roads, "cut-and-fill construction for roads that dig through the terrain and the ability to elevate roads above other roads easily" — [TechRaptor summary of Dev Diary #1](https://techraptor.net/gaming/news/cities-skylines-2-dev-diary); original diary at [Paradox forum "Development Diary #1: Road Tools"](https://forum.paradoxplaza.com/forum/developer-diary/development-diary-1-road-tools.1590300/) (fetch blocked by bot check; not read directly)
- Game Developer interview: roads snap into place "while automatically accounting for elevations in the terrain and other variables"; helper lines account for "steepness, angles, and crossings"; CEO Hallikainen: early builds had placing a road cause "textures completely exploding" or a void leading to "nothingness"; "You can place a roundabout on a slope and it shouldn't break." — [Game Developer: How Colossal Order turned roads into the backbone of CS2](https://www.gamedeveloper.com/design/how-colossal-order-turned-roads-into-the-backbone-of-cities-skylines-ii)
- Modding data (decompiled CS2 API docs): `Game.Net.GeometryFlags` contains `FlattenTerrain = 0x20000` ("Flatten terrain under the net when building/placing it") and `ClipTerrain = 0x40000` — [ps1ke CS2 Modding Guide, GeometryFlags](https://ps1ke.github.io/Cities-Skylines-2-Modding-Guide/Game/Net/GeometryFlags) (page body is JS-rendered; values come from the search snippet of that page). Related prefab components exist: `NetTerrainData`, `NetTerrainPiece`, `BuildingTerraformData`, `AdditionalBuildingTerraformElement`, `TerrainSystem` — [NetTerrainData](https://ps1ke.github.io/Cities-Skylines-2-Modding-Guide/Game/Prefabs/NetTerrainData), [NetTerrainPiece](https://ps1ke.github.io/Cities-Skylines-2-Modding-Guide/Game/Prefabs/NetTerrainPiece), [BuildingTerraformData](https://ps1ke.github.io/Cities-Skylines-2-Modding-Guide/Game/Prefabs/BuildingTerraformData), [TerrainSystem](https://ps1ke.github.io/Cities-Skylines-2-Modding-Guide/Game/Simulation/TerrainSystem) (field lists could not be read: pages render client-side)
- Roads vs quays (official patch note, June 2025, "Bridges & Ports"/quays update, mirrored): roads and pedestrian paths follow the terrain, "creating retaining walls and bridges only when there is a significant elevation change", whereas quays "detect the slightest change in elevation and create a retaining wall on the lower side"; quays "automatically push the terrain down on the retaining wall side"; for terraces "you'll need at least a 10-meter height difference"; with shoreline snapping quays keep "a consistent height above the water surface", raisable "based on the selected elevation steps" — [Steam news mirror (eprison.de)](https://www.eprison.de/spiele/cities-skylines-ii/steam-news/1802261838707488/7442/97560.html); summarized by [PC Gamer](https://www.pcgamer.com/games/city-builder/cities-skylines-2s-latest-patch-has-introduced-quays-and-piers-after-seeing-players-using-makeshift-substitutes-we-know-how-challenging-they-can-be-to-build/)
- Elevation step control "goes from 1.25m to 10m"; a road is "fully underground at −12.5 m or below", above that it is an open, unroofed cut — [buzzaria tunnel guide](https://buzzaria.pages.dev/posts/how-to-build-tunnels-in-cities-skylines-2) (secondary guide, unverified against game data); "small height changes, below tunnel or bridge height, can create retaining walls on slopes and underpass-style interchanges" — [Steam discussion](https://steamcommunity.com/app/949230/discussions/0/3877096256094329351)
- Coasts: "There aren't designated quays like in the first game. Instead, if you build a road on a coast, the road will have railing on the side and a supporting wall" (launch-era, before the 2025 quay patch) — [Steam discussion](https://steamcommunity.com/app/949230/discussions/0/3878221560447256388)
- Player artifact reports: "Sometimes a segment is just barely too long and the game decides the road should dip about 30 feet lower than the rest of the wall"; "Without Move-it and assets like retaining walls and terraforming networks like in CS1, hill maps are hard work to enjoy" — [r/CitiesSkylines mirror](https://lr.in.psf.lt/r/CitiesSkylines/comments/17rdat0/gotta_love_how_much_terrain_issues_have_improved); medium-density zones "create steep terrain steps", CS1 "handled automatic flattening during construction better" — [Steam discussion](https://steamcommunity.com/app/949230/discussions/0/3877095833476954030); "in water" errors after terraforming, roads "suddenly trying to climb at extreme angles on flat ground", fix by changing the road's angle — [Steam discussion](https://steamcommunity.com/app/949230/discussions/0/3878221560447256388)
- Launch maps "quite bumpy"; advice to smooth terrain before the first road; smoothing near a river flooded a district — [PC Gamer](https://www.pcgamer.com/cities-skylines-2s-weird-terrain-quirks-mean-youll-need-to-start-landscaping-immediately) (article body blocked; from snippet)
- Patch 1.0.13: "Fixed wrong zoning grid offset when opposite side of pedestrian street, alley or gravel road has retaining wall" — [TechRaptor](https://techraptor.net/gaming/news/cities-skylines-2-patch-1013-available-now-to-fix-your-garbage-problems)
- Manual retaining-wall workaround at launch: level terrain with Level Terrain tool, then keep road incline near zero — [GameSkinny](https://www.gameskinny.com/tips/cities-skylines-2-how-to-build-retaining-walls/)

### Inferences
- The existence of both `FlattenTerrain` and `ClipTerrain` as separate flags suggests two operations: (1) write road height into the heightmap under the net footprint, and (2) mark/cut terrain so it is not rendered where the net is below ground (cuts/tunnels). The embankment outside the footprint is almost certainly a smoothed falloff in the heightmap (the game is heightmap-based), but no source states the width or slope. (Inferred)
- Retaining walls are a per-segment composition chosen when the terrain-to-road height gap on a side crosses a threshold; the "segment barely too long → dips 30 ft" complaint is consistent with the threshold being evaluated per segment with the road snapping to a discrete elevation step. (Inferred)
- The 2025 quay note confirms a design distinction: roads = permissive (follow terrain, structure only at big gaps); quays = strict (wall at any gap, terrain pushed down). (Documented behaviour, inferred as design intent)

### Gaps
- No public numeric values for the flattened strip width, embankment slope, or the "significant elevation change" threshold that triggers a retaining wall or bridge. Paradox forum Dev Diary #1 could not be fetched (bot check); CS2 modding doc pages are JS-rendered and their field tables were unreadable via fetch.

## Cities: Skylines 1 (Colossal Order, 2015)

### Takeaway
CS1 nets are node/segment splines with discrete elevation steps (3/6/12 m); the game flattens the heightmap under the road footprint (NetInfo `m_flattenTerrain`), and roads above a height limit become bridges with pillars, below ground tunnels; mods (Move It, Fine Road Tool/Anarchy, Terrain Flattener, "terraforming networks") were used to conform terrain and build retaining walls.

### Cited Findings
- Height changed with Page Up/Down; default step 12 m, selectable 3, 6 or 12 m; "Roads above a certain height become bridges"; elevated roads "typically need support pillars"; all roads except gravel can be underground, with limits on incline/decline and tunnel depth; gravel roads can be steeper than other types; elevated gravel road becomes a wooden/covered bridge — [Cities: Skylines wiki, Roads](https://skylines.paradoxwikis.com/Roads)
- `m_flattenTerrain` is a NetInfo flag stored in the .crp asset; a mod exposes an "m_flattenTerrain toggle" — [Paradox forum thread "forcing flat ground when placing park"](https://admin-forum.paradoxplaza.com/forum/threads/forcing-flat-ground-when-placing-park.846561); [Steam Workshop changelog](https://steamcommunity.com/sharedfiles/filedetails/changelog/912329352?p=2)
- Road Editor: a different width on the road's base mesh "can cause clipping or z-fighting around the road"; a forum workaround for flickering edges is setting "Build Height" from 0 to 0.001–0.01 — [CS wiki Road Editor (mirror)](https://wiki.biligame.com/csl/Road_Editor); [Paradox forum: ground level road clipping terrain on sides](https://admin-forum.paradoxplaza.com/forum/threads/road-editor-ground-level-road-clipping-terrain-on-sides-how-to-remove-it.1071408)
- Player practice: "place a road there to flatten the ground or if I don't want a road there a terraforming network"; terrain tools affect ground not the road, "delete the road later and the terrain then shows the terrain changes hidden by the road"; Fine Road Tools / Fine Road Anarchy to force elevated roads over slopes; Move It to adjust nodes afterwards; Terrain Flattener mod for ground near quays — [Steam discussions](https://steamcommunity.com/app/255710/discussions/0/2950376208535613885), [Terrain Flattener comments](https://steamcommunity.com/sharedfiles/filedetails/comments/1468227932)
- "roads are always laid with a spirit level in this game"; workaround: build a ramp with the slope terrain tool before the road — [Steam discussion](https://steamcommunity.com/app/255710/discussions/0/2950376208535613885)

### Inferences
- CS1's `m_flattenTerrain` writes the road's surface height into the heightmap under the segment footprint (plus some blend); the fact that deleting a road "reveals" terrain changes shows the heightmap is permanently modified, not masked. (Inferred from the cited player observation)
- The z-fighting/clipping notes show the classic artifact of a mesh road laid on a heightmap: edges must either be slightly raised or the heightmap must be pulled exactly to the road edge. (Inferred)

### Gaps
- Could not find authoritative documentation for `m_clipTerrain`, `m_lowerTerrain`, `m_createPavement` semantics (no source mentioned `m_lowerTerrain` at all). Numeric max gradient and bridge-height threshold not given on the wiki.

## Transport Fever 1 / 2 (Urban Games)

### Takeaway
TF nodes carry height; the player sets start/end heights (M/N or ,/. keys) and the game proposes bridge or tunnel automatically once the track deviates from terrain by a threshold (community: minimum bridge height ≈2 m; bridge offered at roughly "four clicks"); elsewhere the terrain is pulled to the track as an embankment/cutting whose side slopes are data-driven (`embankmentSlopeLow = 0.75`, `embankmentSlopeHigh = 2.5` defaults in track Lua).

### Cited Findings
- Track files `res/config/track/standard.lua`, `high_speed.lua` have `t.embankmentSlopeLow = 0.75` and `t.embankmentSlopeHigh = 2.5` (defaults); setting them to 0 and 5 makes the terrain next to the track drop off steeply, "both where it is raised and where it is lowered" — [transportfever.net Lexikon "Gleis Aufschüttungen"](https://www.transportfever.net/lexicon/entry/273-gleis-aufsch%C3%BCttungen/) (German fan wiki, Jan 2020; units not defined on the page)
- Street mods live in `res/config/street/*.lua`; max slope for street/track segments is read from `res/config/game_script/track_street_builder_info.lua` (line 84, "c is the max slope of all segments") — [Steam Workshop comments](https://steamcommunity.com/sharedfiles/filedetails/comments/2298331429)
- "The parameters for streets and tracks are static" (cannot change embankment slope while building; requires `mod.lua` edit) — [Realistic Railway Slopes mod comments](https://steamcommunity.com/sharedfiles/filedetails/comments/2161175689)
- Height controls: "." and "," raise/lower start height; M and N in TF1; game shows ground height at cursor; forcing a tunnel = set height low enough to trigger one — [Steam discussion](https://steamcommunity.com/app/1066780/discussions/0/1866119378820014259); "if you create the tunnel in one go and use the n and m keys to adjust height the tunnels should be relatively flat. Building in steps tends to cause problems." — [Steam discussion](https://steamcommunity.com/app/1066780/discussions/0/5739253211802059891)
- Threshold complaint: "The minimum height for a bridge over a road or rail is much lower than the height at which the game offers the option of a bridge... I have to play around with terrain heights to trick the game into creating a realistic bridge"; a bridge is not offered at three clicks but is at four — [Steam discussions](https://steamcommunity.com/app/1066780/discussions/0/1866119378820014259), [TF1 discussion](https://steamcommunity.com/app/446800/discussions/0/1696046976474230349)
- Zero Height Bridge mod (Enzojz): commenter says the game's minimum bridge height is 2 m; an experimental `settings.lua` option controls bridge height; the author says tunnels "always get walls" so a ground-level tunnel would look wrong; a separate mod gives "2 sliders, min bridge height and min tunnel depth" — [Steam Workshop comments](https://steamcommunity.com/sharedfiles/filedetails/comments/2030394734)
- Terrain-alignment behaviour: the first segment of a "flat" build aligns to terrain before flattening; a transition arc is needed from flat to slope; workaround is to smooth terrain first — [Steam Workshop comments](https://steamcommunity.com/sharedfiles/filedetails/comments/2298331429)
- TF1: "preconstructing the embankments on either side of the bridge stops it doing its arcing behaviour"; shortening a span lowers its height — [Steam TF1 discussion](https://steamcommunity.com/app/446800/discussions/0/1843493219427354182)
- Underwater tunnels "can deform the land around the entrance and leave blocks of water that cannot be removed" — [Steam discussion](https://steamcommunity.com/app/1066780/discussions/0/2639605226293165170)
- Mod changelogs: "terrain alignment was reimplemented to get rid of zig-zags" (Flying Junction Road Extension); option "to align terrain to the top of the wall" — [Steam changelog](https://steamcommunity.com/sharedfiles/filedetails/changelog/1114711628)
- Official wiki exists (collaborative devs/community) but its streets/tracks page could not be retrieved — [transportfever2.com/wiki](https://transportfever2.com/wiki/)

### Inferences
- `embankmentSlopeLow/High` being two numbers suggests a two-stage side profile (a gentler slope near the track, steeper further out, or low vs. high embankments using different gradients); the lexicon does not define them. Likely rise/run ratios (0.75 ≈ 37°, 2.5 ≈ 68°) but this is my guess. (Inferred, unverified)
- From my own knowledge of TF modding (not confirmed by a fetched source this session): construction/station `.con` files contain `terrainAlignmentLists = { { type = "EQUAL" | "LESS" | "GREATER", faces = {...}, slopeLow = ..., slopeHigh = ... } }` where EQUAL sets terrain exactly to the polygon, LESS only lowers, GREATER only raises, and slopeLow/slopeHigh give the embankment side gradients. Treat as unverified; see Gaps.
- "Zig-zag" artifacts in terrain alignment mods indicate the terrain mesh is re-triangulated along the alignment polygons, and badly spaced polygons produce jagged edges. (Inferred)

### Gaps
- No fetched source documents `terrainAlignmentType`/`terrainAlignmentLists` semantics or the exact bridge/tunnel trigger thresholds; official Urban Games modding docs not reached. Units of `embankmentSlope*` undefined.

## OpenTTD / Transport Tycoon

### Takeaway
Pure per-tile approach: terrain is a grid of corner heights with 19 slope types; roads/rails never deform terrain, instead "foundations" (retaining-wall sprites) level or incline a tile so track can sit on it; max slope is one height level per tile, bridges/tunnels are explicit player-built tools with endpoints within one level.

### Cited Findings
- Slope enum: `SLOPE_FLAT`, four single-corner, six two-corner, four three-corner, four `SLOPE_STEEP_*` (two-level) slopes, plus `SLOPE_HALFTILE_*`; `NUM_SLOPES = 19`. Foundation enum: `None`, `Leveled` ("tile is leveled up to a flat slope"), `InclinedX`, `InclinedY`, `SteepLower` (lowest corner raised so rail can be built), `SteepBoth` (lowest corner raised and upper halftile leveled), `HalfTileW/S/E/N`, `RailW/S/E/N` ("anti-zig-zag foundation for a single track... not a leveled foundation") — [OpenTTD source docs slope_type.h](https://docs.openttd.org/source/d7/d6d/slope__type_8h_source.html)
- Foundation sprite blocks chosen by which walls are visible: block 0 = walls NW+NE, 1 = NE only, 2 = NW only, 3 = none; heights at the north corner are always multiples of `TILE_HEIGHT`; fractional in-tile heights rounded down — [OpenTTD source docs landscape.h](https://docs.openttd.org/source/d1/d01/landscape_8h.html)
- Build-on-slopes rules: most infrastructure on slopes "with a height difference of one level", described as "an implementation of retaining walls seen in real life"; roads can run perpendicular to a hillside; rail in any direction except pieces ending on the lower foundation edge; roads/track/stations can cross depressions up to one level deep; bridge ends on same level or one apart; steep tiles (since r5864/0.5.0) allow track up/down or along top or bottom edge but not both; setting `[construction] build_on_slopes` — [OpenTTD wiki: Build on slopes](https://wiki.openttd.org/en/Archive/Manual/Settings/Build%20on%20slopes)
- Autoslope (`[construction] autoslope`) allows terraforming under existing buildings/tracks; original patch "Yet another autoslope patch" in trunk since r11107 — [OpenTTD wiki construction settings (archive)](https://wiki.openttd.org/en/Archive/Manual/Settings/Configuring%20patches/Construction), [tt-forums autoslope](https://www.tt-forums.net/viewtopic.php?p=620645); halftile foundations since r11321 let two tracks sit on steep slopes — [tt-forums halftile foundations](https://www.tt-forums.net/viewtopic.php?p=630775)
- Road tool works with half-tile precision (click right half of a tile to start with a half tile); bridges (B) and tunnels (T) are explicit tools — [OpenTTD wiki Roadway construction](https://wiki.openttd.org/en/Manual/Roadway%20construction)
- A road on a foundation-raised tile won't connect to an adjacent road; slopes matter more than tile height for connections — [tt-forums](https://www.tt-forums.net/viewtopic.php?p=699645)
- "More height levels" extended the old limit; rendering glitches reported at ~level 31+ during development — [tt-forums](https://www.tt-forums.net/viewtopic.php?p=751957)

### Inferences
- OpenTTD is the clearest "one height per corner, discrete slopes, never deform for infrastructure" model: the structure (foundation) absorbs the mismatch, and the visual junction is a hard, vertical wall sprite — exactly the retaining-wall approach, applied everywhere automatically. Zig-zag artifacts on diagonal track were serious enough to add dedicated `Rail*` foundation types. (Inferred from enum comments)
- For a low-poly flat-shaded grid this is attractive because every tile is one of a finite set of shapes, so the structure–terrain seam is always a known edge. (My inference)

### Gaps
- `TILE_HEIGHT` numeric value (8 pixels in source) not confirmed via fetch (tile_type.h page returned only navigation). Max map height limit not confirmed.

## Workers & Resources: Soviet Republic (3Division)

### Takeaway
Free-form spline roads on a heightmap; road placement auto-levels the terrain along the road (players exploit the road-planning preview to flatten "huge strips of land"), with real excavator/bulldozer cost when construction offices do the earthwork; manual raise/lower terrain tools exist.

### Cited Findings
- "You can use the road leveling tool to level huge strips of land": build a short mud road at the target height, then drag a road plan out far without building it; repeat to create large flat areas; train tracks work the same; stock a construction office with bulldozer and excavator to make it free — [Steam discussion](https://steamcommunity.com/app/784150/discussions/0/1849197902649005301)
- Changelog 0.7.9.13: fix for an editor problem "that sometimes blocked auto-leveling while building roads" — [Hooded Horse wiki, Game versions](https://hoodedhorse.com/wiki/Workers_Resources_Soviet_Republic/Game_versions)
- Construction menu includes terrain icons to raise or lower ground — [Hooded Horse wiki, General](https://hoodedhorse.com/wiki/Workers_Resources_Soviet_Republic/General)
- Players request terrain alteration be automated by construction offices; "No free excavator at construction office" errors — [Steam discussion](https://steamcommunity.com/app/784150/discussions/0/2567564329112119475), [Steam discussion](https://steamcommunity.com/app/784150/discussions/0/4738295219690117874)

### Inferences
- The "drag a plan without building it levels the terrain" exploit implies the leveling is applied to the heightmap during the preview/plan step (terrain is modified by the road plan geometry, not by construction completion). (Inferred from player description)

### Gaps
- No official statement of flattened width, side slope, or bridge/tunnel thresholds found.

## Anno 1800 / Anno 117 (Ubisoft Mainz)

### Takeaway
Anno 1800 islands are hand-designed with buildable terrain capped at 12% slope and non-editable terrain; roads do not deform terrain. Anno 117 doubled the buildable slope to 24% and roads "adjust to terrain elevation" with bridges over rivers.

### Cited Findings
- "Anno 1800 had a maximum terrain angle of 12%, which presented us a significant limit for island design"; Anno 117 "doubled the maximum buildable terrain slope to 24%", requiring cross-department work because slope affects nearly every part of the building system — [Anno Union devblog: Island creation in Anno 117 (devtrackers mirror)](https://devtrackers.gg/anno-1800/p/31c1b89e-devblog-island-creation-in-anno-117-pax-romana)
- Anno 117 roads "adjust to terrain elevation", bridges over rivers, pier-style versions on the coast — [Anno Union devblog "All roads lead to Anno" (devtrackers mirror)](https://devtrackers.gg/anno-1800/p/23ff27b3-devblog-all-roads-lead-to-anno)
- Terrain cannot be freely edited in Anno 1800 (players told leveling cliffs isn't possible) — [Steam discussion (German)](https://steamcommunity.com/app/916440/discussions/3/3119298624515533479)

### Inferences
- Anno sidesteps the problem by constraining the authored terrain to a slope roads/buildings can tolerate, i.e. the "road follows terrain, no deformation" model made viable by a strict slope cap. (Inferred)

### Gaps
- No detail on Anno 117's mesh method for roads on 24% slopes.

## Timberborn (Mechanistry)

### Takeaway
Fully block-based terrain: one block per tile per vertical unit, stackable with overhangs; paths and buildings never deform terrain; height is changed only by placing/removing blocks (dynamite, tunnels, excavators, builders adding dirt blocks).

### Cited Findings
- Maps are built from Terrain Blocks; elevation capped at 16 tiles at map creation, 22 total in-game (11 reserved for vertical building); terrain removed by dynamite, tunnels or Dirt Excavators, added by builders from Dirt; new blocks behave like original terrain — [Timberborn wiki: Terraforming](https://timberborn.wiki.gg/wiki/Terraforming)
- Terrain Blocks can be stacked; overhangs up to three blocks sideways; Update 7 made terrain placeable on all solid surfaces — [Timberborn wiki: Terrain Block](https://timberborn.wiki.gg/wiki/Terrain_Block), [SteamDB patch notes](https://steamdb.info/patchnotes/18346774)
- Natural slopes act as stairs but aren't everywhere; otherwise Wooden Stairs are needed — [Gfinity](https://www.gfinityesports.com/article/how-to-access-high-terrain)

### Inferences
- Timberborn = voxel model: no seams because every surface is axis-aligned; vertical movement is handled by stair objects, not by terrain slopes. (Inferred)

### Gaps
- None material.

## Factorio (Wube)

### Takeaway
Flat 2D world; cliffs are "just a visual trick and no elevation is actually present"; Space Age elevated rails are a second layer reached by 16×4-tile ramps and held by ~4×4 supports placed automatically, with a concrete base hiding the ground junction.

### Cited Findings
- Cliffs: "this is just a visual trick and no elevation is actually present"; removable with cliff explosives — [Factorio wiki: Cliff](https://wiki.factorio.com/Cliff)
- FFF-378: ramp is 16 tiles long, 4 wide ("the longest entity in the game"), 4 rotations, solid concrete base with metal upper structure; supports ~4×4 collision box, 8 rotations, "a hive of optical hacks as they need to fill their collision boxes"; "supports will be automatically added between some units of rails"; elevated rail can't be built over tall entities (rocket silo, roboport, big pole); ramps/supports can stand on water/landfill with a special under-terrain patch — [FFF #378 Trains on another level](https://factorio.com/blog/post/fff-378); [Factorio wiki: Rail ramp](https://wiki.factorio.com/Rail_ramp), [Rail support](https://wiki.factorio.com/Rail_support)
- Fulgora: "The first 4 tiles of the ramp needs to be placed on solid ground" — [Factorio wiki: Rail support](https://wiki.factorio.com/Rail_support)
- Forum feedback: red ramps/supports criticized as too high/steep looking — [Factorio forums FFF-378 thread](https://forums.factorio.com/viewtopic.php?p=592734)

### Inferences
- Factorio's answer to mixed heights is two discrete layers plus a fixed-length ramp object; the terrain is never touched, and the ramp's "concrete base" is the visual device that closes the junction with the ground. (Inferred)

### Gaps
- Exact automatic support spacing not stated in FFF.

## Banished (Shining Rock)

### Takeaway
Little documentation found; players recall a flatten tool with a preview and hills that "are always in the way", suggesting roads follow terrain and flattening is a separate manual tool.

### Cited Findings
- Players recall Banished's flatten tool "showed a preview of the outcome... and it only had to be used once" — [Crate Entertainment forum (about another game, recalling Banished)](https://forums.crateentertainment.com/t/flattening-terrain/130897); "hills are really bad. You can not do anything with them and they are always in the way" — [Steam Banished discussion](https://steamcommunity.com/app/242920/discussions/0/617319460843121030); flatten tool used to remove lakes, streams and hills — [Steam guide](https://steamcommunity.com/app/242920/discussions/0/133257636776137742)

### Inferences
- Roads in Banished do not deform terrain; the manual flatten tool is the only deformation. (Inferred; weak sourcing)

### Gaps
- No primary documentation on Banished's road/terrain mechanics located.

## SimCity (2013, Maxis GlassBox)

### Takeaway
Curved spline roads on a heightmap; sources found only cover curved roads and zoning following curves, nothing on terrain deformation or retaining walls.

### Cited Findings
- Curved roads were a headline GlassBox feature; the engine "automatically curves the zone along the road" — [GamesRadar: first details on GlassBox](https://gamesradar.com/simcity-first-details-glassbox-engine); roads carry water, power, sewage — [Wikipedia](https://en.wikipedia.org/wiki/SimCity_(2013_video_game))
- Terrain bug thread (odd slopes affecting road/zone placement) — [EA Answers](https://answers.ea.com/t5/SimCity-2013/Terrain-Bug/td-p/6261803/page/2)

### Inferences
- None beyond: roads likely flatten locally (standard for the genre), unconfirmed.

### Gaps
- No source on SimCity 2013's terrain-under-road mechanism or retaining walls.

## Railway Empire (Gaming Minds)

### Takeaway
Auto-router: the game computes the simplest connection and "creates the necessary embankments, bridges or tunnels automatically", biased to minimal gradient; waypoints steer the route; H shows slope guide lines.

### Cited Findings
- The game creates the simplest connection and adds needed embankments, bridges or tunnels; "shortest route with a minimal gradient"; bridges/tunnels very expensive; waypoints avoid structures at the cost of steeper gradient; bridges/tunnels carry max two tracks — [Gamepressure Railway Empire guide](https://www.gamepressure.com/railway-empire-guide/building-tracks-and-creating-lines/zeaa84)
- Holding "H" during track laying shows green slope lines; lay full track then add points to eliminate red lines — [Steam discussions](https://steamcommunity.com/app/503940/discussions/0/1843567770209193767), [Steam discussion](https://steamcommunity.com/app/503940/discussions/0/3153060475188361228)

### Inferences
- Railway Empire is a "structure chooser": the track node elevation is solved, then every span is classified embankment / bridge / tunnel by its offset from terrain. (Inferred)

### Gaps
- No thresholds or embankment profile published.

## Planet Coaster 1 / 2 (Frontier)

### Takeaway
PC1 paths follow terrain and players must reshape ground manually (paths and terrain "hard to combine", unfinished auto-tunnel in config); PC2 adds a per-path "Terrain Style" switch between a path suspended from terrain and terrain shaped to follow the path, plus auto-tunnel when a path runs through terrain.

### Cited Findings
- PC1: lay path, reshape land, delete path, smooth, re-lay; an unfinished auto-tunnel for paths reachable via config file, "glitchy" — [Frontier forums](https://forums.frontier.co.uk/goto/post?id=7518465); requests for "snap-to-angle" / "mold terrain to path" — [Frontier forums](https://forums.frontier.co.uk/threads/first-impressions-and-recommendations-to-the-dev-team.629794); terrain Smooth and Chisel brushes — [Planet Coaster fandom: Terrain](https://planetcoaster.fandom.com/wiki/Terrain)
- PC2 Line Builder "Terrain Style" switches between paths suspended from terrain and paths where the terrain follows the path; Rounding Tool smooths path corners — [Frontier CMS guide](https://cms.zaonce.net/en-GB/node/6209) (fetch 403; from search snippet); when a path runs through terrain it deforms to fit, used for tunnels — [WeArePlayStation pathing guide](https://www.weareplaystation.fr/communautes/planet-coaster-2/astuces/wiki/planet-coaster-2-pathing-guide)

### Inferences
- PC2 exposes the "deform vs. follow" choice to the player per path, which is the same dichotomy CS2 resolves implicitly. (Inferred)

### Gaps
- No numbers on PC2's deformation width/slope.

## Satisfactory (Coffee Stain)

### Takeaway
No terraforming at all: terrain is a static hand-crafted mesh; players build "roads" from foundations on pillars.

### Cited Findings
- "Its terrain is hand-crafted which unfortunately means static terrain"; "the 'terraforming' available in the game is purely mesh based" (burying buildings) — [Steam discussion](https://steamcommunity.com/app/526870/discussions/0/5241649843389545572); roads made of foundations, pillars from AWESOME shop so roads/tracks go "over uneven terrain" — [Steam discussion](https://steamcommunity.com/app/526870/discussions/0/2793871682807648995); RoadSplineTool mod 16/32/64 m roads — [Steam discussion](https://steamcommunity.com/app/526870/discussions/0/3468361783669826945?l=russian)

### Inferences
- Satisfactory is the extreme "structure only" model: the structure never meets terrain cleanly because it floats on pillars or intersects it. (Inferred)

### Gaps
- None material.

## Unity EasyRoads3D (road tool / engine example)

### Takeaway
Heightmap stamping along a spline with a two-zone cross-section: an Indent zone leveled to road height, then a Surrounding zone that blends to the original terrain; a "Preserve Terrain Contours" 0–1 slider trades exact flat shoulders for restored relief; deformation can be disabled per road (bridges); mesh terrains are not deformed.

### Cited Findings
- "The Indent value represents the distance from the edge of the roads outwards to the terrain point that should be leveled at the same height of the road at that point"; per-marker override but not below the scene minimum; default derived from heightmap scale — [EasyRoads3D V3 manual, General Settings](https://nlp.fi.muni.cz/aiproject/ui/hutta_peter2019/pa026-project/Assets/EasyRoads3D/html/general_settings.html)
- "Left/Right Surrounding represents the distance used to level the area between the road and the current terrain height", "especially useful in hilly areas"; heights transition gradually across the surround — same source
- Terrain Deformation can be disabled per road (e.g. bridge segments); mesh terrains are not deformed; Raise Road Network option moves the road above the terrain to avoid z-fighting; "Reapply Critical Road Heights" restores terrain heights that must match the road; Preserve Terrain Contours: "A value of 0 means that the terrain will exactly match the shape of the white surfaces surrounding the roads", 1 rebuilds natural terrain shape pulled toward the critical road heights — [EasyRoads3D V3 manual, Terrain control](https://nlp.fi.muni.cz/aiproject/ui/hutta_peter2019/pa026-project/Assets/EasyRoads3D/html/terrain_control.html), [Creating roads](https://nlp.fi.muni.cz/aiproject/ui/hutta_peter2019/pa026-project/Assets/EasyRoads3D/html/creating_roads.html)
- Troubleshooting: increase indent where results look poor; check terrain Pixel Error; back up terrains because restore can fail — [Release notes](https://nlp.fi.muni.cz/aiproject/ui/hutta_peter2019/pa026-project/Assets/EasyRoads3D/html/release_notes.html)

### Inferences
- The indent/surrounding scheme = Raatstad's "margin at road height + talud" (`GroundField`); EasyRoads blends with a smooth interpolation rather than a fixed slope ratio, which is fine on smooth-shaded heightmaps but would produce many slightly different triangle normals on a flat-shaded mesh. (My inference)

### Gaps
- The manual copy is a 2019 mirror; current defaults may differ.

## Unreal Engine Landscape Splines

### Takeaway
Heightmap stamping with Half-Width (flat), cosine-blended Side Falloff on each side, End Falloff at spline ends, and per-segment Raise Terrain / Lower Terrain switches (embankments vs. ditches).

### Cited Findings
- Heightmap is raised or lowered to fit the spline "with a smooth cosine-blended falloff on either side"; Half-Width (solid lines), Side Falloff (dotted lines, left/right factors), End Falloff on a one-segment control point; "Raise Terrain" for roads on embankments, "Lower Terrain" for rivers/ditches; needs a Spline edit layer — [Unreal Engine docs: Landscape Splines](https://dev.epicgames.com/documentation/en-us/unreal-engine/landscape-splines-in-unreal-engine); [UE 4.26 docs](https://docs.unrealengine.com/4.26/BuildingWorlds/Landscape/Editing/Splines)
- `EditorApplySpline(StartWidth, EndWidth, StartSideFalloff, EndSideFalloff, StartRoll, EndRoll, NumSubdivisions, bRaiseHeights, bLowerHeights, PaintLayer)`; if both raise/lower false "no height modification"; NumSubdivisions too high "will be slow and may cause artifacts" — [UE 4.27 Blueprint API](https://docs.unrealengine.com/4.27/en-US/BlueprintAPI/Landscape/Editor/EditorApplySpline/), [UE 5.4 C++ API](https://dev.epicgames.com/documentation/unreal-engine/API/Runtime/Landscape/ALandscapeProxy/EditorApplySpline?application_version=5.4)

### Inferences
- The raise-only / lower-only switches are the same idea as Transport Fever's (unverified) LESS/GREATER alignment and Raatstad's "a road only digs in when its deck is below the terrain". (Inferred)

### Gaps
- Docs do not describe behaviour where two splines overlap at different heights.

## BeamNG Road Architect and other engine examples (gamedev reference points)

### Takeaway
BeamNG's Road Architect stamps a plateau with "Margin" (soft shoulder width) and "Domain of Influence" (total reach), and where roads cross at different elevations "the terrain follows the lowest road at each point", forming cavities for bridges; 1 m heightmap resolution causes bumpy roads on banked bends.

### Cited Findings
- Parameters in meters: Domain of Influence (how far modification extends; small = sharp narrow plateau, large = gentle wide slopes) and Margin (width of the soft zone around road edges, smoothed into surroundings); modes single road (neighbours may float or get buried), all roads, groups; "Where roads cross at different elevations, the terrain follows the lowest road at each point, which can form cavities for bridges"; "Overlapping roads terraform to the lower road, producing natural embankments"; terrain resolution typically 1 m/pixel "can cause bumpy roads, especially with significant banking or bends" (known bug) — [BeamNG docs: Road Architect terraforming](https://documentation.beamng.com/world_editor/tools/road_architect/terraforming/)
- GameDev.net blog: nested quad strips, falloff blend map rasterized from the narrower strip, road elevation blended with noise terrain; "sharp, high causeways are mostly eliminated" — [gamedev.net "...and more roads"](https://nexe.gamedev.net/blogs/entry/600044-and-more-roads) (site unreachable; from search snippet)
- symbios-tensor (Rust road generator): traces roads over heightmap then carves terrain; road elevations use smoothed node heights so roads "cut through hills and bridge dips"; embankment is a separate step; "tile boundaries currently produce visible seams" — [docs.rs symbios-tensor README](https://docs.rs/crate/symbios-tensor/0.2.0/source/README.md)
- Age of Empires IV editor "Terrain Deform Along Splines" exists — [AoE support](https://support.ageofempires.com/hc/en-us/articles/4418066345748-Terrain-Deform-Along-Splines) (403; not read)

### Inferences
- The BeamNG "lowest road wins" rule is a concrete, documented policy for mixed-height crossings in a heightfield: lower road carves, upper road becomes a bridge over the cavity. (Documented rule; inference that it's generalizable)

### Gaps
- No defaults published for Margin / Domain of Influence.

## Which approach suits a hex grid on a sphere, one height per cell, flat-shaded low-poly triangles?

### Takeaway
The documented approaches fall into three families: (1) heightfield stamping with a smooth falloff (CS1/CS2, Transport Fever, EasyRoads3D, Unreal, BeamNG) — produces organic embankments but relies on smooth shading and fine heightmap resolution to hide the profile; (2) per-tile discrete slopes with foundations/retaining walls (OpenTTD) — the structure absorbs every mismatch, no deformation; (3) no terrain interaction at all with layered structures (Timberborn, Factorio, Satisfactory). For one height per hex cell with visible flat triangles, the OpenTTD-style "finite set of cell shapes plus walls" and the BeamNG/Unreal rules (fixed cross-section, lowest road wins at crossings, raise-only/lower-only) are the most transferable; the width/slope numbers from the smooth-heightmap games are largely unpublished.

### Cited Findings
- Heightmap resolution directly causes road bumpiness (1 m/pixel, BeamNG) — [BeamNG docs](https://documentation.beamng.com/world_editor/tools/road_architect/terraforming/); too many spline subdivisions "may cause artifacts" (Unreal) — [UE API](https://dev.epicgames.com/documentation/unreal-engine/API/Runtime/Landscape/ALandscapeProxy/EditorApplySpline?application_version=5.4); tile boundaries produce seams in a carve-after-trace pipeline — [symbios-tensor](https://docs.rs/crate/symbios-tensor/0.2.0/source/README.md)
- OpenTTD needed dedicated "anti-zig-zag" rail foundations and halftile foundations to make diagonal and steep cases look right within a discrete-slope system — [OpenTTD slope_type.h](https://docs.openttd.org/source/d7/d6d/slope__type_8h_source.html), [tt-forums halftile foundations](https://www.tt-forums.net/viewtopic.php?p=630775)
- Transport Fever's embankment side slopes are plain data per track type (0.75 / 2.5) and players edit them to get steeper, cleaner banks — [transportfever.net](https://www.transportfever.net/lexicon/entry/273-gleis-aufsch%C3%BCttungen/)
- CS2 distinguishes a tolerant network (roads: walls only at "significant" gaps) from a strict one (quays: wall at any gap, terrain pushed down on the wall side) — [Steam news mirror](https://www.eprison.de/spiele/cities-skylines-ii/steam-news/1802261838707488/7442/97560.html)
- CS2 segment-level threshold evaluation produces the "segment barely too long → road dips 30 ft below the wall" complaint — [r/CitiesSkylines mirror](https://lr.in.psf.lt/r/CitiesSkylines/comments/17rdat0/gotta_love_how_much_terrain_issues_have_improved)
- Anno constrains authored slope (12% → 24%) instead of deforming — [Anno Union devblog](https://devtrackers.gg/anno-1800/p/31c1b89e-devblog-island-creation-in-anno-117-pax-romana)

### Inferences
- With flat shading, every triangle normal is visible, so a smooth cosine/lerp falloff (Unreal/EasyRoads) across a few 27 m-ish hex cells will read as a visible staircase of facets; a fixed-ratio talud with cuts exactly on the profile knee points (what Raatstad's `LineGround` already does) is closer to how Transport Fever's data-driven `embankmentSlope` works and keeps facets planar. (My inference, consistent with the cited TF data and the project's CLAUDE.md)
- Where the talud cannot reach the terrain within the available cells (steep terrain), OpenTTD's answer — a vertical foundation/retaining wall that is itself part of the road piece, with sprite variants per visible wall — is the documented low-poly-friendly solution; CS2's "significant elevation change" rule is the same idea with an undisclosed threshold. (Inferred)
- For crossings at different heights, BeamNG's documented "terrain follows the lowest road; higher road bridges the cavity" is a simple, deterministic rule that maps onto a heightfield with one height per cell; Factorio's and CS2's practice of a fixed ramp length/elevation step avoids ambiguous mid-heights. (Inferred from documented rules)
- No game found publishes the flattened-strip width or embankment slope for its road stamping; TF's 0.75/2.5 and CS2's 1.25–10 m elevation steps and −12.5 m tunnel threshold (secondary source) are the only numbers recovered. (Documented gap)

### Gaps
- No published algorithm text (width, slope, smoothing kernel) for CS1/CS2 terrain-under-road; Paradox forum dev diary unreadable; CS2 modding doc field tables unreadable (client-side rendering).
- Transport Fever construction alignment fields (`terrainAlignmentLists`, `type = EQUAL/LESS/GREATER`, `slopeLow/slopeHigh`) not confirmed by any fetched source this session.
- No sources found describing SimCity 2013's or Banished's terrain-under-road mechanism.
