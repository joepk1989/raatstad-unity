# Terrein rond wegen in Raatstad: onderzoek en aanbevolen oplossing

Datum: 7 november 2026. Antwoord op `docs/terrein-wegen-rapport.md` (deel 6: de onderzoeksvragen, deel 5: de meetlat,
deel 7: de randvoorwaarden). Dit document wijzigt geen code; het is een ontwerp met onderbouwing. De zes uitgebreide
bronnotities (één per onderzoeksvraag, met een URL bij elke bewering en per punt "gedocumenteerd" of "eigen afleiding")
staan in `docs/terrein-wegen-oplossing/1-games.md` tot en met `6-snelheid.md`. In de tekst hieronder verwijst [G], [H],
[P], [K], [M] en [S] naar die notities (games, hoogtefunctie, profiel, kunstwerken, mesh, snelheid); de belangrijkste
losse bronnen staan ook in deel 9.

## 1. Samenvatting (één bladzijde)

**De diagnose uit het rapport klopt en is ook wat de literatuur zegt:** het probleem is niet de wiskunde van het talud of
de snijlijnen (dat deel werkt: 0 ongelijke randen, strakke insnijdingen), maar dat er meerdere bronnen van waarheid zijn
die elkaar op hetzelfde vak tegenspreken, en dat verticale wanden (sprongen) in een hoogtefunctie worden gepropt die daar
niet voor bedoeld is. Elk productiegereedschap dat is onderzocht (Civil 3D, Transport Fever 2, Cities: Skylines 2,
Unreal, Catlike Coding's hexkaart) houdt zich aan dezelfde twee regels: **het terrein is één doorlopende functie zonder
sprongen, en alles wat verticaal is (keermuur, landhoofd, portaal, sokkel) is een eigen mesh die op die functie staat.**
[M][K]

**Aanbeveling in vijf regels:**

1. **Drie opgeslagen gegevens, verder niets.** (a) Het terrein: één hoogte per vak (`Terr`) plus de hoekpuntverschuivingen
   van de speler; wegen schrijven er *nooit* in. (b) De wegengraaf: wegvakken, verbindingen (ook de lange verbindingen
   van tunnels en bruggen), soort, fietspad, eenrichting. (c) De weghoogte per wegvak (`RoadH`), plus de ankers die de
   speler zelf zette (Page Up/Down). Alles wat nu ook nog opgeslagen of bijgehouden wordt (`preLevel`, `preCarve`, de
   geulen `cuts`, `Low`, `Under` als apart mechanisme, `levelled`, `carved`) is afgeleid en verdwijnt. Weg weghalen =
   wegvak uit de graaf; het terrein was nooit veranderd, dus "terugzetten" bestaat niet meer. [P][S]
2. **Eerst het profiel, dan de kunstwerken, dan het terrein** (de richting van `ProfileAll` was goed). Het profiel wordt
   een exacte, kleine berekening: twee Dijkstra-lopen geven of een helling van hooguit 10% haalbaar is en de strakste
   onder- en bovengrens per knoop (McShane–Whitney), daarna een projectie (Dykstra) die de weg zo dicht mogelijk bij het
   terrein legt. Alleen de nieuwe wegvakken zijn vrij; bestaande wegen liggen vast. Onhaalbaar (twee vaste punten te
   dicht bij elkaar) wordt vooraf gemeld met het conflictpaar en de benodigde helling, en volgt een vaste beslisboom
   (plaatselijk tot 15%, anders ongelijkvloers, anders weigeren). [P]
3. **Eén maat beslist per punt langs een weg wat het wordt:** `d = weghoogte − terrein` (het terrein van voor de weg).
   |d| ≤ 6 m: talud in het terrein. 6–9 m: de weg op een sokkel (ophoging) of in een bak met keermuren (insnijding).
   d > 9 m: brugdek op pijlers, met het landhoofd precies op het punt waar d = 9 m. d < −9 m: koker (tunnel), met het
   portaal precies op het punt waar d = −9 m. Een onderdoorgang is een koker van één vak, een ondergronds kruispunt een
   kamer, een hoog kruispunt of een bocht in een viaduct een sokkelvak. Alle kunstwerken komen zo uit dezelfde twee
   getallen en uit dezelfde snijpunten als het terrein. De drempels zijn ontleend aan de civiele praktijk (aardebaan tot
   9–10 m, insnijding tot 6–8 m) en aan CS2 (tunnel vanaf −12,5 m). [K][P]
4. **Het terrein is één doorlopende functie (min/max van vlakken) zonder sprongen; elke wand is een quad-strook.** De
   hoogtefunctie bevat per weg alleen het *maaiveld-deel* van elk stuk (waar |d| ≤ 6 m): een strook op weghoogte en een
   talud van 1 op 1,5 dat altijd het terrein bereikt, met zeshoekige koppen die ook in de lengterichting aflopen. Wanden
   (keermuur, sokkel, landhoofd, portaal, kade) zijn eigen meshes waarvan de voet op dezelfde functie staat en waarvan de
   voetlijn een snijlijn in het terrein is. De vakken worden gesneden langs bekende constructielijnen (wegrand, voet van
   het talud, einde van een stuk, zijden van een kop, voetlijnen van wanden) en elk stuk krijgt de hoogte van *zijn eigen
   vlak*, niet van de globale functie. De gedeelde randprofielen van Terrein v2 (`EdgeProfile`) blijven de enige bron voor
   alle punten op een vakrand, met snapping op een rooster van 1 mm. Geen naden, geen stroken van 2 cm, geen
   verfijning via het midden. [M][H]
5. **Snelheid door sleutels, niet door slimmigheid.** Per vak één sleutel die alle invoer omvat (de hoogte van het vak en
   zijn buren, de wegstukken binnen bereik met hun hoogte en soort, de wanden, een versienummer van de code); een klik
   herbouwt alleen de vakken waarvan de sleutel veranderde plus één ring, in één keer aan het eind van het beeld. Het
   profiel wordt alleen voor de nieuwe lijn opgelost. Unity's eigen cijfers zeggen dat een deel van 128 vakken op de
   snelle weg (MeshData, Burst) 1–2 ms kost en op de gewone weg 12–25 ms plus 10 MB afval; de 0,15–0,25 s van nu zit
   waarschijnlijk in GC en in het bakken van de MeshCollider, niet in het rekenwerk. [S]

**Wat uit de huidige code blijft:** `Terr`/`SetTerr`, Terrein v2 (`EdgeProfile`, `PlaneLins`, `Ground2Check`,
`Ground2Dump`), de gedachte van `ProfileAll` en `RoadH`, `BoreSearch` als zoekfunctie, alle tekencode van koker, portaal,
mantel, dek, pijlers en landhoofd (`TubeSeg`, `Portal`, `Mantle`, `SpanMesh`), `RoadDraw`, de scan in `WegVoorbeeld`.
**Wat weg kan:** `LevelTerr`/`preLevel`/`RestoreLevel`, de geulen (`Cut`, `Carve`, `Fill`, `CutVal`, `ExactGround`,
`CollectCuts` voor zover het geulen betreft), `preCarve`/`carved`/`levelled`, `DipUnder` en de terreinschrijvende delen van
`SetLevel`, `LimitGrade`/`LimitGradeAll`, de naden (`Skirt`, `SeamCell`), de 2 cm-wandstroken en `WallOut`-lijnen in
`GroundField`, `Refine`/`Refine2`, `RoadCorner`/`SkewPathCorner`/`PathHalf`, `SpanBottom`, `deckSide`.

**Testvolgorde** (deel 8): één weg op een helling, een insnijding, een dijk, een keermuurstuk, een dalbrug, een tunnel,
een gelijkvloers kruispunt op een helling, een onderdoorgang, brug-naar-portaal, dan meldingen 7 en 8 naspelen en de
stressplaneet `hoogtetest`. Meetlat per stap: 0 gaten, 0 terrein boven het wegdek, 0 ongelijke randpunten, steilste
helling ≤ 10%, en een foto.

**Grootste risico's** (deel 9): de drempels flapperen als het terrein naast een weg geboetseerd wordt (hysterese nodig);
het omzetten van opgeslagen steden (geulen en `Under` worden kokerstukken); de vakken in de graaf van bruggen en tunnels
(lange verbindingen) veranderen bij hertekenen; floating point op de bol (twee stukken van een "rechte" weg liggen nooit
op één lijn; daarom snijden langs constructielijnen, niet langs vlak-met-vlak); en de stap naar Burst is een grote
verbouwing die alleen nodig is als de eenvoudige maatregelen (hergebruik van buffers, collider in een job) niet volstaan.

## 2. Vraag 1: hoe doen andere spellen het, en wat past bij Raatstad

Volledige notitie: `docs/terrein-wegen-oplossing/1-games.md` [G].

**Drie families.** (1) *Heightfield-stempel met zachte afloop*: Cities: Skylines 1 en 2 (vlakken onder de weg via
`m_flattenTerrain`/`FlattenTerrain`, keermuren en bruggen "alleen bij een aanzienlijk hoogteverschil", tunnel vanaf
ongeveer −12,5 m), Transport Fever 2 (knopen met eigen hoogte, taluds als data `embankmentSlopeLow = 0,75` en
`embankmentSlopeHigh = 2,5`, brug of tunnel automatisch bij een drempel), Workers & Resources (de weg egaliseert het
heightfield al tijdens het plannen), Unreal Landscape Splines (halve breedte plus een cosinus-afloop, met aparte
schakelaars "alleen ophogen"/"alleen afgraven"), EasyRoads3D (Indent plus Surrounding), BeamNG Road Architect (Margin
plus Domain of Influence, en de regel "waar wegen op verschillende hoogten kruisen volgt het terrein de laagste weg").
(2) *Discrete hellingen met funderingen*: OpenTTD (19 hellingtypen uit hoekhoogten, de weg vervormt het terrein nooit,
een "foundation" met keermuur-sprites vangt het verschil op; speciale anti-zigzag-funderingen voor diagonaal spoor).
(3) *Geen vervorming*: Timberborn (blokken), Factorio (vlak; verhoogd spoor met vaste hellingbanen van 16 vakken en
automatisch geplaatste steunen met een betonnen voet die de aansluiting verbergt), Anno (het landschap zelf is begrensd op
12%, nu 24%), Satisfactory.

**Wat niemand publiceert:** de breedte van de vlakke strook en de helling van het talud in CS1/CS2, en de precieze
drempels. De enige harde getallen zijn TF2's 0,75/2,5, CS2's stappen van 1,25–10 m en −12,5 m voor een tunnel, en
OpenTTD's één niveau per vak. [G]

**Wat past bij een hex-raster op een bol met één hoogte per vak en harde driehoekjes (eigen afleiding uit [G] en [H]):**
- De zachte cosinus-afloop van Unreal/EasyRoads werkt alleen met gladde normalen en fijne heightmaps; in flat shading
  wordt elke tussenstap een zichtbaar facet. Een talud met één vaste helling (zoals TF2's data-talud) en snijlijnen
  precies op de knikken (wat `LineGround` al doet) houdt de vlakken strak. Dat is dus de goede keuze geweest.
- OpenTTD's antwoord op "het talud past niet": de weg draagt zijn eigen fundering (keermuur) als onderdeel van het
  wegstuk; het terrein blijft liggen. Dat is precies de sokkel die een bebouwd vak in Raatstad al heeft. CS2 doet
  hetzelfde met een drempel ("significant elevation change").
- BeamNG's "de laagste weg wint" bij kruisende wegen op verschillende hoogten is een eenvoudige, deterministische regel
  die één op één past op de huidige regel "op een weg zelf nooit hoger dan die weg" (`onLevelH`).
- Vaste hoogtestappen (CS2, Factorio) voorkomen dubbelzinnige tussenhoogten; Raatstad heeft die al (2,25 m).

Conclusie: niet overstappen op een gestempeld heightfield (vraag 2), wél de twee ontbrekende regels overnemen: het
kunstwerk draagt zijn eigen wand, en het terrein wordt nooit door een weg herschreven.

## 3. Vraag 2: exacte hoogtefunctie met snijlijnen, of een fijner gestempeld heightfield

Volledige notitie: `docs/terrein-wegen-oplossing/2-hoogtefunctie-vs-heightfield.md` [H].

**Bevindingen.** Er is geen publieke bron van een spel dat een exacte envelop van vlakken berekent en langs de knikken
trianguleert als terrein; alle productiegereedschappen stempelen een heightfield óf bouwen een TIN met breaklines
(Esri, Civil 3D). De exacte envelop van vlakken is in de meetkunde goed begrepen (lineaire complexiteit, CGAL
`Envelope_3`), maar CGAL rekent met exacte rationale getallen omdat vlak-met-vlak-snijlijnen in floating point wegschieten
bij bijna evenwijdige vlakken; robuuste mesh-booleans zijn pas sinds 2022 interactief. Een gestempeld heightfield is
robuust (alleen functie-evaluatie per punt), maar: de voet van het talud rafelt met de sampleafstand (bij 6–12 punten per
vak van 15 m zo'n 2–4 m rafel, in flat shading goed zichtbaar), het terrein kan door het wegdek steken waar de
interpolatie tussen samples boven het dek ligt, en een verticale wand wordt een steile helling ter breedte van één sample.
[H]

**Afweging voor Raatstad.**

| | Exact (nu) | Gestempeld |
|---|---|---|
| Kieren tussen buren | alleen als buren anders snijden; opgelost door gedeelde randprofielen (werkt: 0 ongelijk) | triviaal bij gedeelde samplepunten |
| Low-poly uiterlijk | kaarsrechte knikken, één vlak per talud | rafel op taludvoet en wegrand |
| Terrein door het wegdek | onmogelijk per constructie | mogelijk, marge onder het dek nodig |
| Wanden | mogelijk als aparte polylijn met twee hoogten | niet (functie), aparte wandmesh nodig |
| Numeriek | kwetsbaar bij vlak-met-vlak | robuust |
| Code | hoog, casuïstiek | laag |
| Precedent | geen | alle engines |

**Keuze: exact blijven, maar de kwetsbaarheid eruit halen.** Drie maatregelen uit [H] en [M]:
1. Snijlijnen zijn *constructielijnen*, geen vlak-met-vlak-snijdingen: de wegrand is een offset van de hartlijn, de voet
   van het talud is een offset plus (hoogteverschil ÷ helling), het einde van een stuk, de zijden van een kop en de
   voetlijn van een wand zijn gegeven. Alleen "talud raakt terrein" is een echte snijding, en die is binnen één
   waaierdriehoek (het terrein is daar één vlak) een rechte lijn die je vindt door tekenwissel op de drie zijden: één
   lineaire interpolatie per zijde. Dat is wat `FieldLines` al grotendeels doet (`toeP`/`toeM`); de vlak-paren in
   `PlaneLins` zijn alleen nog nodig waar twee taluds of een talud en een kop elkaar kruisen, en die paren moeten in een
   vaste volgorde (stabiele id) gesneden worden.
2. Elk stuk krijgt de hoogte van zijn eigen vlak (het vlak dat in het zwaartepunt van het stuk actief is), niet van de
   globale min/max-functie per hoekpunt. Dan is elk stuk per constructie vlak, en zijn er geen "knikken die de
   snijlijnen niet kennen" meer, dus ook geen `Refine` via het midden (dat was de oorzaak van de spitsen op foto 1).
3. Punten op een vakrand komen uitsluitend uit het gedeelde randprofiel (de eigenaar is het vak met het laagste nummer,
   dezelfde parametrisering van laag naar hoog nummer, dubbele precisie in één wereldstelsel, daarna gesnapt op 1 mm).
   Dat is de regel van Catlike Coding's hexkaart (randpunten uit dezelfde brugpunten) en van alle "crack-free
   chunk"-literatuur: geen T-kruisingen, bit-gelijke punten, geen naden. [M]

Een vaste onderverdeling per vak (bijvoorbeeld 24 of 54 driehoekjes met snapping van hoekpunten naar de knikken) is een
denkbare middenweg, maar verschuift elke knik tot een halve sjabloonmaat en lost de wanden niet op. Niet aanbevolen. [H]

## 4. Vraag 3: het wegprofiel over een graaf met kruispunten

Volledige notitie: `docs/terrein-wegen-oplossing/3-wegprofiel.md` [P].

**Wat de civiele techniek doet** (Hare, Lucet en anderen, UBC Okanagan): het verticale alignement als gemengd-geheeltallig
lineair programma met een kwadratische spline, hellingsgrenzen en kosten per m³ ontgraven, ophogen en vervoeren; de
netwerkvariant uit 2025 (UVA/CUVA) rekent 1,1–61 s op 327–1508 stations. Te traag en te zwaar voor een klik; en geen
van die modellen behandelt een graaf met splitsingen midden in een weg. Dynamisch programmeren (Goh 1988) en genetische
algoritmen (Fwa 2002) idem. [P]

**Wat ons probleem werkelijk is.** Zonder boogstralen en grondtransport is "elke verbinding hooguit 10%" een stelsel
verschilbeperkingen |h_i − h_j| ≤ g·d_ij met vaste knopen. Dat is klassiek (CLRS 24.4) en exact opgelost door Kyng,
Rao, Sachdeva en Spielman (COLT 2015, "Lipschitz learning on graphs"):
- **Haalbaar** dan en slechts dan als voor elk paar vaste knopen s, t geldt |h_s − h_t| ≤ g · dist(s, t), met dist de
  kortste-padafstand over de graaf (hun Lemma 3.5).
- De **strakste grenzen** per knoop zijn U(x) = min over vaste t van [h_t + g·dist(x, t)] en L(x) = max over vaste t van
  [h_t − g·dist(x, t)]: twee Dijkstra-lopen met meerdere bronnen, O(E log V) (hun Sectie 4; dit zijn de
  McShane–Whitney-uitbreidingen). L en U zijn zelf geldige profielen; een willekeurige functie ertussen is dat niet. [P]
- De huidige lus "halveer het teveel op elke verbinding" is de methode van afwisselende projecties (POCS): ze komt uit op
  *een* haalbaar punt, niet op het punt dat het dichtst bij het terrein ligt, en bij onhaalbaarheid blijft ze
  oscilleren. Met Dykstra's correctiescalar per beperking wordt het de echte kleinste-kwadratenprojectie (lineaire
  convergentie voor veelvlakken; kan in lange ketens even stallen, dus warm starten en een sweep-limiet). [P]

**Aanbevolen berekening** (zo'n 40 regels C#, geen bibliotheek):

```
invoer: knopen van de nieuwe lijn (vrij) + hun aansluitknopen (vast: bestaande wegen, ankers van de speler),
        terrein z_i per knoop, helling g = 0,10, afstanden d_ij (hart tot hart, over de bol)
1. U = Dijkstra(bronnen: vaste t met label h_t, kantkosten g·d);  L = −Dijkstra(bronnen: −h_t)
   als ergens L > U: onhaalbaar → conflictpaar (s, t), g_nodig = |h_s − h_t| / dist(s, t) → beslisboom
2. h_i = clamp(z_i, L_i, U_i) voor vrije i
3. Dykstra-sweeps over alle verbindingen (per verbinding twee halfvlakken, elk met een onthouden correctie)
   tot het grootste teveel < 1 mm of 200 sweeps; rest: op de grens snappen (haalbaar, niet exact optimaal)
4. gewichten: een kruispunt of bebouwd vak zwaarder (schaal h en z met √w)
```

Voor 30 vrije knopen is dit microseconden; voor de hele planeet (Hele planeet vol: ~5000 knopen) blijft het ver onder
een seconde. De midrange-iteratie van de ∞-Laplaciaan (AMLE) is het juiste gereedschap als er géén terreindoel is
(een brugdek zo vlak mogelijk tussen twee vaste einden); voor gewone wegen is de kleinste-kwadratenprojectie passender
omdat ze de weg op het maaiveld houdt. [P]

**Twee vaste punten te dicht bij elkaar.** De praktijk (Softree, patent US 6.996.507) controleert de hellingen tussen
vaste punten vooraf via het kortste pad en "verzacht" dan een randvoorwaarde met een gemelde overschrijding;
ontwerprichtlijnen laten plaatselijk tot 15% toe in zeer beperkte omstandigheden (TxDOT/AASHTO). Niemand schrijft voor
welk vast punt je opgeeft. Voorstel, in deze volgorde: (1) g_nodig ≤ 15%: accepteer de steilere helling alleen op het pad
s–t en meld het ("steil stuk, 13%"); (2) anders: de nieuwe aansluiting wordt ongelijkvloers (de nieuwe weg gaat onder
of over de bestaande door, zoals `SetLevel` nu al bij ≥ een heel niveau doet), of (3) weigeren met reden en rode vlag
(zoals `CanStart` van de AI-bouwer). Bestaande wegen worden nooit verzet (regel in CLAUDE.md). [P]

**Hoe dit het foto-7-probleem oplost** (kruispunt diep in de berg met drie portalen): het kruispunt is gewoon een knoop
met een terreindoel; de kleinste-kwadratenprojectie houdt het zo dicht mogelijk bij het terrein. Komt het tóch diep te
liggen (omdat de buren dat afdwingen), dan is het volgens vraag 4 een *kamervak* (ondergronds kruispunt, foto 11),
getekend als één zeshoekige kamer met kokermonden, niet als drie portalen op elkaar.

## 5. Vraag 4: wanneer brug, tunnel of keermuur, en hoe de aansluiting dicht blijft

Volledige notitie: `docs/terrein-wegen-oplossing/4-kunstwerken.md` [K].

**Drempels in spellen en praktijk.** CS2: drie trappen (talud, "cut road" met keermuren bij de eerste negatieve waarden,
gesloten tunnel vanaf −12,5 m; stapelen vanaf 8,75 m). CS1: stappen 3/6/12 m, "boven een zekere hoogte een brug". TF2:
brug zodra het terrein vlak vóór het beginpunt zo'n 2–3 stappen onder de weg ligt; "een tunnel krijgt altijd muren,
anders klopt het beeld niet" (modder Enzojz). Civiel: aardebaan tot 9–10 m (daarboven viaduct: JICA, ČVUT), insnijding
tot 6–8 m naar terreintype, keermuren altijd uit ruimtegebrek (WSDOT 730), portaal daar waar de insnijding genoeg
hoogte geeft voor de koker (Milltimber Brae), landhoofden "full height" (gesloten wand) of "spill-through" (het talud
loopt onder het dek door en alleen de bovenste meter wordt gekeerd; FHWA BIRM 10.1). [K]

**Vertaald naar vakken van 15 m en een talud van 1 op 1,5:** een talud van h meter heeft zijn voet 1,5·h verder. Bij
6 m is dat 9 m: net in het buurvak. Bij 9 m is het 13,5 m: tot aan het hart van de buur. Dat geeft de tabel van de
samenvatting (talud ≤ 6, wand 6–9, kunstwerk > 9). De cijfers zijn een keuze binnen de gevonden bandbreedte, geen
natuurwet; ze horen als constanten in de code, met hysterese van een halve stap (zie deel 9).

**Drie manieren om de naad dicht te maken** [K]:
- (a) *Terrein wegsnijden* (Unity Terrain Holes, Unreal Visibility Mask, MSTS "transparant maken"): per texel of
  driehoek binair, gerafelde rand, altijd afgedekt met geometrie ("hide the aliased edges with rock meshes"). Raatstad
  doet dit al goed met `BoreTri`: de snede loopt langs eigen driehoeken en de mantel dekt hem af. Alleen nodig waar een
  koker echt door het terrein moet.
- (b) *Schort/mantel die in de grond steekt* (TF2-muren rond elke tunnelmond, MSTS-transfers, CS1-pijlers,
  CS2-keermuren): het goedkoopste en robuuste, en in low-poly stijl ook het mooiste: harde betonranden tegen vlakke
  grasdriehoeken lezen als opzettelijk, terwijl shader-blending (DICE Battlefront, >200 instructies) bij fotorealisme
  hoort.
- (c) *Het terrein om het kunstwerk heen genereren* (TF2-taluds, CS2 cut-and-fill, Trainz Smooth Spline, Civil 3D
  corridor met "daylight"-lijn): precies `GroundField`. Werkt voor alles wat doorlopend is, faalt per definitie bij een
  verticale wand, want een hoogtefunctie kan geen verticaal vlak maken.

**Dus: (c) voor taluds en insnijdingen, (b) voor alles met een verticaal vlak, (a) alleen in de koker.** Alle spellen
combineren het zo. De keermuur op de wegrand (foto 9) is nu "een uitzondering in de hoogtefunctie"; het hoort een mesh
te zijn (b) waarvan de voet op de functie staat.

**Brug die in een portaal eindigt** (foto's 5 en 13): geen enkel onderzocht spel laat een los dek op een los portaal
aansluiten; TF2 en CS2 hebben er altijd een knoop op het maaiveld tussen. Regel voor Raatstad: landhoofd en portaal zijn
hetzelfde soort ding (een verticale wand op de lijn waar d = ±9 m), en als het punt d = +9 (landhoofd) en het punt
d = −9 (portaal) in hetzelfde stuk minder dan een halve koker uit elkaar liggen, worden ze één gecombineerde mesh (de
zeshoekige mantel met aan de dalkant een dekoplegging). [K]

**Ondergronds kruispunt** (foto 11): CS2 tekent alles dieper dan 12,5 m zonder portalen; TF2-spelers lopen vast op
tunnelwanden die bij een wissel in de weg zitten. Eén kamer-mesh op het knoopvak met kokermonden, geen kokerwanden die
elkaar snijden. [K]

## 6. Vraag 5: een dichte mesh voor een functie met wanden

Volledige notitie: `docs/terrein-wegen-oplossing/5-mesh.md` [M].

**Breaklines.** Een stuksgewijs vlak oppervlak met knikken heet in GIS/CAD een TIN met breaklines; de constrained
Delaunay-triangulatie (Chew 1989, Shewchuk's Triangle) dwingt elke knik als driehoekszijde af. Voor één convex hexvak
met een handvol vlakken is dat overdreven: de knikken zijn analytisch bekend, dus het vak langs die lijnen snijden
(Sutherland–Hodgman per halfvlak) en de convexe stukken waaieren volstaat; dat is wat `LineGround`/`Ground2Cell` doen.
Robuust snijden: na elke snede opeenvolgende dubbele punten weggooien en stukken met < 3 punten of een zijde korter dan
2 roostereenheden verwijderen (de regel van Clipper2), punten op de rand nooit zelf berekenen maar uit het randprofiel
halen. [M]

**Het corridor-model van Civil 3D** is het civiele equivalent van "weg met talud in het terrein": stations langs de as,
per station een dwarsprofiel (wegrand, "hinge", "daylight"-punt waar het talud het bestaande terrein raakt), gelijke
punten van opeenvolgende stations verbonden tot "feature lines", en de corridor-surface is een TIN met die lijnen als
breaklines en de daylight-lijnen als buitenrand. Twee lessen: de wegrand, de knik van het talud én de taludvoet zijn
alle drie breaklines; en waar het talud verticaal zou worden gebruikt CAD een "wall breakline" (een tweede lijn op
1 mm afstand), een noodgreep die bestaat omdat een TIN geen verticaal vlak kan bevatten, en die in de praktijk "broken
surfaces" geeft. [M]

**Wanden.** Geen enkele bron ondersteunt een strook van 2 cm als wand. Flat shading toont zo'n sliver als een
flikkerende lijn van 1–2 pixels, van bovenaf als tanden (foto 3), en elke epsilon voor sliver-verwijdering gooit hem weg.
De juiste constructie (Catlike Coding's klif-quad, veralgemeend): de wand is een polylijn in het vlak, met twee hoogten
per punt (onder en boven) uit dezelfde gedeelde functie; het lage terrein stopt precies op de polylijn (hoekpunten op
z_laag), het hoge terrein stopt er ook (hoekpunten op z_hoog), en per lijnstuk komt één quad met eigen hoekpuntrecords
(zodat de normaal niet met de grond vermengt), gewonden naar de lage kant, in een eigen submesh (beton/baksteen). Waar
het hoogteverschil naar 0 loopt eindigt de wand en gaat de polylijn over in een gewone knik. Het lage terrein mag niet
onder de wand doorlopen (z-fighting aan de voet). [M]

**Gedeelde randen.** Twee oorzaken van kieren: T-kruisingen (een punt aan één kant) en floating point (hetzelfde punt
langs twee codepaden verschilt in de laatste bits; zelfs op de GPU z-fight twee "gelijke" posities die anders berekend
zijn). Oplossing: canonieke eigenaar per rand, één functie die de hele gesorteerde puntenlijst op die rand levert
(hoeken, elke knik, elke wandvoet en -top), beide buren gebruiken die lijst (omgekeerd waar nodig) en berekenen nooit
zelf een kruising op een rand; berekend in dubbele precisie in één wereldstelsel, gesnapt op een rooster (1 mm), pas
daarna float. Dat is wat `EdgeProfile` doet; de snapping ontbreekt nog. Naden (skirts) zijn voor LOD-verschillen, niet
voor dit probleem, en falen van bovenaf. [M]

## 7. Vraag 6: interactief houden

Volledige notitie: `docs/terrein-wegen-oplossing/6-snelheid.md` [S].

- **Vuile verzameling, één keer herbouwen.** Catlike Coding (hexkaart in delen van 5×5): een wijziging markeert het eigen
  deel én de delen van buren met gedeelde geometrie; `Refresh` zet alleen een vlag, `LateUpdate` trianguleert één keer.
  Veloren leerde dat buren meermaals in de rij belanden zonder een set. Unity Terrain: `SetHeightsDelayLOD` en één
  `SyncHeightmap` bij loslaten. Raatstad: het beïnvloede gebied is analytisch bekend (voetafdruk + bereik van het talud
  + koppen + kunstwerken + één ring omdat een vak de hoeken van zijn buren gebruikt). [S]
- **Sleutel per vak** (Houdini PDG: een cache is alleen geldig als alle invoer en de versie van het algoritme gelijk
  zijn; ongeldig maken langs de afhankelijkheden, nooit door naar de uitvoer te kijken): hoogte van het vak en zijn
  buren (ring 1, en ring 2 waar de hoeken daarvan afhangen), de wegstukken binnen bereik met hoogte, soort, breedte en
  kunstwerk, de wanden, de schakelaars, en een `GroundVersion`. `FieldKey`/`groundCache` zijn dit al bijna; de
  versie en de wanden ontbreken. [S]
- **Wegstukken per vak** in emmers (segment ≈ een vak lang; voor een straal van 40 m zo'n 2–3 ringen); een BVH is bij
  5000 stukken onnodig. `UseNear` doet dit al, zij het door alle stukken te scannen. [S]
- **Lokaal profiel.** De McShane-uitbreiding is puntsgewijs een minimum over ankers plus afstand; een wijziging kan
  alleen knopen raken waar een pad door het gewijzigde gebied de huidige waarde verslaat. Met alle bestaande wegknopen
  vast is het per definitie lokaal. [S]
- **Unity-cijfers.** Mesh-API-voorbeeld van Unity: 4,6 miljoen hoekpunten samenvoegen kost 760–920 ms en 640 MB afval
  op de gewone weg, 60–70 ms en 0,3 MB met MeshData + Jobs + Burst. Geschaald naar een deel van 128 vakken
  (~50.000 driehoeken): 1–2 ms snel, 12–25 ms plus ~10 MB afval gewoon. `Physics.BakeMesh` is thread-safe en bedoeld voor
  een `IJobParallelFor` vóór het toekennen van `sharedMesh` (155 ms piek gemeld voor één collider op de hoofdthread).
  Volgorde van goedkope winst: buffers hergebruiken, `MeshUpdateFlags.DontValidateIndices | DontRecalculateBounds |
  DontNotifyMeshUsers` met eigen bounds, de `Mesh` hergebruiken in plaats van `new Mesh()`, collider bakken in een job
  en één beeld later toekennen; pas daarna Burst. Eerst meten met de Profiler (`GC.Alloc`, `BuildChunk`,
  `Physics.BakePhysXCollisionMeshData`). [S]

## 8. Het ontwerp

### 8.1 Gegevensmodel: drie opgeslagen dingen

| Gegeven | Waar | Wie schrijft | Wie leest |
|---|---|---|---|
| Terrein `Terr[i]` (+ `CornerOfs`, `CenterOfs`) | per vak | speler (boetseren), generator, meteoor, meer/gracht | alles |
| Graaf: wegvakken, `Links` (kort en lang), soort, fiets, eenrichting, rotonde | per vak/verbinding | leggen, slopen, kunstwerk-zoeker (lange verbindingen) | profiel, terreinveld, verkeer |
| Weghoogte `RoadH[i]` en ankers `Anchor[i]` | per wegvak | profiel-oplosser (RoadH), speler met Page Up/Down (Anchor) | terreinveld, kunstwerken, verkeer, lopen |

Alles andere is afgeleid en wordt niet opgeslagen: per verbinding de soort kunstwerk en de snijpunten (landhoofd,
portaal), per vak de soort (maaiveld, sokkel, kamer), het terreinveld (stukken, koppen, wanden), de randprofielen, de
meshes. Een opgeslagen stad van nu bevat `preLevel`, `preCarve`, `Under`/`Low`; bij het laden worden die één keer
omgezet (deel 10, stap 0).

Twee regels die alle eerdere "vijf manieren waarop de weg het terrein beïnvloedt" vervangen:
1. **Een weg schrijft nooit in `Terr`.** Een weg op een niveau is een anker in `Anchor`, geen terreinhoogte. Het terrein
   onder een weg mag de speler gewoon boetseren; de weg houdt zijn `RoadH` en de classificatie (deel 8.3) past zich aan.
2. **Elk wegvak heeft precies één hoogte, `RoadH`,** ook een portaalvak, een landhoofdvak en een brugvak. Verkeer en
   lopen lezen die (via `G`) en de helling van hart tot hart, zoals nu.

### 8.2 Rekenstappen per klik, in deze volgorde

1. **Graaf** bijwerken (`PlaceLine`, `Place`, `Clear`), zoals nu.
2. **Profiel** (deel 4): vrij zijn de vakken van de nieuwe lijn die nog geen `RoadH` hadden; vast is al het andere plus
   de ankers. Dijkstra-omhullenden → haalbaar of beslisboom → clamp → Dykstra. Resultaat: `RoadH` voor de nieuwe vakken.
   Bij slopen: niets (de overgebleven `RoadH` blijven geldig). Bij boetseren onder een weg: niets aan `RoadH`.
3. **Classificatie** (deel 8.3): per verbinding de functie d(s) en de snijpunten; per vak de soort. Rechte reeksen van
   dek- of kokervakken worden een lange verbinding (de binnenvakken worden weer bouwgrond), zoals `BoreSearch` nu doet
   maar dan vanuit d(s) in plaats van vanuit aparte drempels per soort, en al vanaf één binnenvak.
4. **Terreinveld** samenstellen: per verbinding het maaiveld-deel als strook met talud, koppen op einden, bochten en
   splitsingen, wanden als polylijnen met twee hoogten. Dit is de huidige `CollectCuts` zonder geulen.
5. **Vuile vakken**: alle vakken binnen bereik van gewijzigde stukken, koppen en wanden, plus één ring; per vak de nieuwe
   sleutel vergelijken met de oude; alleen vakken met een andere sleutel opnieuw.
6. **Randprofielen** voor de vuile randen (eigenaar = laagste vaknummer), dan de vakken (deel 8.4), dan de kunstwerken
   (deel 8.5), dan per deel de mesh en de collider (deel 8.6). Alles in één `LateUpdate`, met `RebuildSome` voor het deel
   onder de muis eerst als het meer dan één beeld kost.

### 8.3 Classificatie: één maat, één tabel

Langs elke verbinding (hart a naar hart b, lengte L) is het wegdek z_r(s) lineair en het terrein z_t(s) het basisterrein
(het vlak over de waaierdriehoek waar s ligt, zonder enige wegbijdrage). d(s) = z_r − z_t. Constanten (voorstel, als
`const` bij elkaar, met een halve stap hysterese bij herclassificatie):

| d | wat het wordt | wie het tekent |
|---|---|---|
| −6 ≤ d ≤ 6 m | maaiveld: strook op weghoogte, talud 1:1,5 naar het terrein | terreinveld (deel 8.4) |
| 6 < d ≤ 9 m | sokkelstuk: de weg op een zeshoekige betonvoet, het terrein eronder blijft | wandmesh op de wegrand (`WallOut`), voet op H |
| −9 ≤ d < −6 m | bak: strook op weghoogte, keermuren op de rand, het terrein erbuiten blijft | terreinveld (strook) + wandmesh |
| d > 9 m | dek op pijlers; landhoofd op het punt s waar d = 9 | `SpanMesh` (dek, borstwering, pijlers tot H, landhoofd als wand) |
| d < −9 m | koker; portaal op het punt s waar d = −9 | `TubeSeg`, `Portal`, `Mantle`; terrein in de mantel weg (`BoreTri`) |

Per vak volgt de soort uit d in het hart: maaiveldvak, sokkelvak (d > 9, bij een bocht, splitsing of doodlopend eind:
een zeshoekige betonkolom tot het terrein met de weg erop; dit vervangt het "reusachtige landhoofdblok" van foto 4 en
12), kamervak (d < −9: zeshoekige kamer met kokermonden; foto 11). Een recht binnenvak van een reeks dek- of kokervakken
wordt vrijgemaakt (lange verbinding). Een onderdoorgang (weg onder weg in één vak) is dan vanzelf een kokerstuk van één
vak onder de strook van de bovenste weg: het terrein onder de bovenste weg is daar "het terrein" voor de onderste, dus d
springt onder die strook naar < −9 (een niveau van 4,5 m plus de dikte van het dek is niet genoeg: zie deel 9, dit is de
reden dat de kokerdrempel voor een onderdoorgang de bestaande `UnderClear` moet gebruiken en niet 9 m). Een weg óver een
weg op +4,5 m is hetzelfde geval van de andere kant: de onderste weg ligt op het maaiveld, de bovenste op een dijk, en
de onderste gaat als koker door die dijk.

Snijpunten zijn geen vakgrenzen: het landhoofd en het portaal staan op het precieze punt s langs de verbinding waar d de
drempel kruist (lineaire interpolatie, want z_r is lineair en z_t per waaierdriehoek lineair). Daardoor ligt het talud
vóór het landhoofd altijd aan (spill-through) en begint de koker altijd waar de berg hoog genoeg is (headroom-regel).

### 8.4 Het terreinveld: doorlopend, zonder sprongen

H(p) = basisterrein, met daarop in vaste volgorde (zoals nu in `GroundField`): eerst de hoogste ophoging (max), dan de
laagste uitgraving (min), dan "op een weg nooit boven die weg" (min met het wegdek binnen elke strook; de laagste weg
wint). Nieuw ten opzichte van nu:
- Elk stuk draagt alleen bij op zijn **maaiveld-deel** (waar |d(s)| ≤ 6 m, of ≤ 9 m voor de strook van een bak). Voorbij
  dat deel is er geen bijdrage; de overgang is doorlopend omdat de bijdrage daar precies het terrein raakt (bij |d| = 6
  reikt het talud nog tot het terrein; bij de bak staat er een wand, zie hieronder).
- De bijdrage van een stuk is een **afstandskegel** tot zijn maaiveld-deel: weghoogte min (helling × afstand tot het
  lijnstuk), met de zeshoekige afstand voor koppen (zoals `CapDist`) zodat ook de uiteinden in de lengterichting aflopen
  (het End Falloff van Unreal). Daarmee is H overal doorlopend: max en min van doorlopende functies.
- **Wanden zijn geen deel van H.** Een wand is een polylijn W met per punt z_laag = H aan de lage kant en z_hoog = de
  weghoogte (sokkel) of het terrein (bak, kade, sokkel van een gebouw). H zelf is aan beide kanten van W gedefinieerd
  zonder sprong: aan de wegkant van een bak is H de strook (binnen `WallOut`), aan de buitenkant het terrein; de twee
  stukken grenzen aan W maar hebben er een andere hoogte, en de quad-strook van de wand sluit het gat. Wie H opvraagt
  voor lopen of rijden krijgt op de wegkant de strook (zoals nu).
- De keermuur-uitzonderingen in `GroundField` (`RoadH.Count > 0 && |hr − base| > FieldReach·FK`), `WallOut` als
  snijlijn in H, `FieldReach` als harde grens, `SpanBottom`, `Carve`/`Fill` vervallen. `FieldK` (schuifje Talud) blijft.

### 8.5 De mesh per vak

Per waaierdriehoek (midden, hoek k, hoek k+1), in de vlakke coördinaten van het vak zoals nu:
1. **Snijden langs constructielijnen** (zoals `FieldLines`): wegranden, voet van het talud (als rechte lijn in (S, T),
   zoals `toeP`/`toeM`), begin en eind van het maaiveld-deel van elk stuk, zijden en graten van koppen, de wandpolylijnen,
   de grenslijnen van dekken en mantels. Dit zijn gegeven lijnen; geen vlak-met-vlak.
2. **Snijden langs de resterende knikken**: alleen waar in één stuk twee bijdragen (twee taluds, talud en kop, talud en
   terreinvlak) elkaar kruisen; de paren in vaste volgorde (stabiele id per stuk en kop), zoals `PlaneLins` nu, maar met
   de hoogte van elk stuk daarna uit **zijn eigen vlak** (het vlak dat in het zwaartepunt actief is). Elk stuk is zo
   exact vlak; `Refine`/`Refine2` vervalt.
3. **Opschonen** na elke snede: dubbele punten weg (roostergelijkheid), stukken met < 3 punten of een zijde < 2 mm weg;
   stukken met hetzelfde vlak en een gedeelde zijde mogen samengevoegd (minder driehoeken).
4. **Randpunten** uitsluitend uit `EdgeProfile` (eigenaar laagste nummer, parametrisering van laag naar hoog nummer,
   dubbele precisie in het wereldstelsel, snap op 1 mm, dan float). Het randprofiel bevat ook de wandvoeten en -toppen
   als paren (zoals `Kinks` nu met `step`), zodat beide buren en de wandquad hetzelfde punt hebben. Elk punt op een rand
   uit het profiel; een eigen snijlijn die op de rand eindigt levert dus ook een punt in de buur (collineair, zonder
   gevolg voor de vorm).
5. **Wanden** als quad-stroken per lijnstuk van W, eigen hoekpuntrecords, normaal naar de lage kant, eigen submesh
   ("sokkel"/"kade"/"steen"), onderpunten bit-gelijk aan het lage terrein, bovenpunten bit-gelijk aan het hoge; waar
   z_hoog − z_laag < 1 mm eindigt de wand.
6. **Waaieren** vanuit een inwendig punt (zoals `Piece` nu), flat shading met eigen normaal per driehoek. Geen naden.

Controle blijft `Ground2Check` (randen en hoeken bit-gelijk; nu met 0 toegestaan verschil in plaats van 2 mm) en
`Ground2Dump`.

### 8.6 Kunstwerken uit dezelfde gegevens

| Kunstwerk | Invoer | Voet/aansluiting |
|---|---|---|
| Talud, insnijding | stuk, maaiveld-deel | is H zelf |
| Sokkel van een wegvak of gebouw | wegrand + `WallOut`, of vakrand; z_hoog = `RoadH`/`G` | voet = H op de wandpolylijn (randprofiel) |
| Keermuur van een bak | strookrand; z_hoog = terrein, z_laag = strook | idem |
| Landhoofd | punt s waar d = 9; breedte = dek + borstwering; z_hoog = dek; onderkant 0,5 m onder H | wand op de dwarslijn, in het terrein gesneden als snijlijn |
| Dek, pijlers | lange verbinding of dekstuk; pijlers om de 24 m tot H − 0,6 (zoals nu) | pijlervoet uit H (`TerrainAt` = H) |
| Portaal, koker, mantel | punt s waar d = −9; koker tot het volgende portaal of de kamer | mantel dekt de `BoreTri`-snede (zoals nu) |
| Kamer | kamervak | zeshoekige wanden tot H, kokermonden per arm |
| Brug-naar-portaal | landhoofdpunt en portaalpunt < halve koker uit elkaar | één mantel met dekoplegging |

Alle "voet"-hoogten komen uit dezelfde H en dezelfde randprofielen als het terrein, dus de naad is per constructie dicht.
Pijlers en landhoofden gebruiken `TerrainAt` = H, zoals nu al in `SpanMesh`.

### 8.7 Snelheid

- Sleutel per vak = hash van: `Terr` van het vak en ring 1 (en ring 2 waar `CornerBase` van afhangt), `CornerOfs`,
  de wegstukken binnen bereik (id, hoogten, breedte, soort, maaiveld-deel), koppen, wanden, dekken en mantels binnen
  bereik, `FieldK`, de schakelaars, en `GroundVersion`. Dit is `FieldKey` uitgebreid; `groundCache` onthoudt de
  driehoekjes, `edgeCache` de randprofielen (sleutel idem, symmetrisch).
- Wegstukken in emmers per vak (ring 0–3), bijgewerkt bij elke wijziging; `UseNear` leest daaruit.
- Profiel alleen voor de nieuwe lijn; bij Hele planeet vol één keer voor alles (`ProfileAll` blijft als die vorm).
- Herbouw: vuile delen in een `HashSet`, één keer in `LateUpdate`; het deel onder de muis eerst (`RebuildSome`).
- Mesh: buffers hergebruiken, `MeshUpdateFlags`, eigen bounds, `Mesh` hergebruiken; collider via `Physics.BakeMesh`
  in een job, één beeld later toekennen, eerst de delen dicht bij de camera. Burst pas als de meting daarom vraagt.

## 9. Risico's en wat ik niet zeker weet

1. **Flapperen aan een drempel.** Boetseren naast een weg kan d rond 6 of 9 m heen en weer duwen, zodat een dijk een
   brug wordt en terug, met een andere graaf (lange verbinding) tot gevolg. Maatregel: hysterese van een halve stap
   (2,25 m) en herclassificatie alleen bij het leggen en bij boetseren onder de weg zelf, niet bij elke buur. Of de
   drempels precies goed liggen weet ik niet; ze zijn een keuze binnen de civiele bandbreedte.
2. **Onderdoorgang.** Een weg op het maaiveld onder een weg op +4,5 m door: d onder de bovenste strook is maar zo'n
   −5,6 m (4,5 m niveau plus 1,08 m dek), ruim boven de kokerdrempel van −9 m. De kokerregel voor "onder een andere weg
   door" moet daarom de bestaande maat `UnderClear`/`LevelH` gebruiken (de koker past precies, zoals het model
   overpass_path), niet de tunneldrempel. Dat is één extra regel in de tabel, geen apart mechanisme; maar het is het
   punt waar het ontwerp het meest op het huidige `overpass_path`-model leunt en het minst op bronnen.
3. **Omzetten van opgeslagen steden** (`preLevel`, `preCarve`, `Under`/`Low`, `levelled`): één keer bij laden
   (deel 10, stap 0). Oude steden kunnen er iets anders uitzien (een geul wordt een kokerstuk met portaal op het
   d-snijpunt). Ctrl+Z (`Snapshot`/`Restore`) moet de nieuwe drie gegevens bevatten.
4. **Lange verbindingen vanuit d(s)** veranderen de graaf; de AI-bouwer (`PlanetAI`), `Connect` in `PlanetFill` en de
   verkeersgraaf rekenen daar al mee (`IsBore`, `TunnelLinks`), maar "vanaf één binnenvak" is nieuw en kan meer korte
   bruggen geven dan nu. Of dat mooi is, is te zien in test 5.
5. **Floating point op de bol.** Twee stukken van een "rechte" weg liggen nooit exact op één lijn (`EndSlack`). Daarom
   constructielijnen in plaats van vlak-met-vlak, en koppen met afstandskegels die de uiteinden afdekken. Of C#/Burst
   bit-gelijke uitkomsten geeft langs twee codepaden is niet uit een bron bevestigd; `Ground2Check` meet het, en de
   snapping op 1 mm vangt het laatste bit af.
6. **Dykstra kan stallen** in een lange keten tussen twee strakke vaste einden (bewezen in de literatuur, zonder snelheid
   voor dit geval); het vangnet (warm start, 200 sweeps, snappen op de grens) is pragmatisch, geen garantie van
   optimaliteit. Voor het spel maakt dat niets uit.
7. **Burst/MeshData** is een grote verbouwing (`PlanetCity.Shapes`, `RoadDraw` zijn niet Burst-geschikt). De schatting
   dat GC en de collider de tijd opslokken is uit Unity's cijfers afgeleid, niet in dit project gemeten.
8. **Stijl.** Harde betonkanten tegen gras passen bij low poly; of de zeshoekige sokkelkolom onder een hoog kruispunt en
   de kamer mooi zijn, is pas te zien in de test. Dat is een ontwerpkeuze van de eigenaar.
9. **Niet gevonden:** CS2's stroken en taluds (dev diary onbereikbaar), TF2's drempelsleutel, een regel welk vast punt
   je opgeeft bij onhaalbaarheid, een maat voor "hoe dun is te dun" in flat shading, en een precedent voor een exacte
   vlakkenenvelop als spelterrein. Alle conclusies daarover zijn eigen afleiding, zo gemarkeerd in de notities.

## 10. Overgang vanaf de huidige code

Elke stap moet in Play Mode werken en de meetlat halen voordat de volgende begint (CLAUDE.md). De test uit deel 8 van het
rapport (`hoogtetest v2 profiel`, scan A/B/C, foto's) blijft de meetlat; het testscript hoeft niet in git.

0. **Laden en opslaan**: `Snapshot`/`Restore`/`Save`/`Load` kennen `RoadH` en `Anchor`; bij het laden van een oude stad
   worden `preLevel`/`preCarve` weggegooid (het terrein van voor de weg is het terrein), `levelled` wordt `Anchor`,
   `Under`/`Low` wordt een lange verbinding met `RoadH` op de onderste weg. Nog niets tekent anders.
1. **Profiel**: `ProfileLine` voor een nieuwe lijn met Dijkstra + Dykstra (vervangt `LimitGrade`, `LimitRun`,
   `LimitGradeAll`, de ontspanningslus in `ProfileAll`); `RoadH` voor élk wegvak, `G` leest `RoadH`; `LevelTerr` en
   `RestoreLevel` verdwijnen, `SetLevel` schrijft `Anchor` in plaats van `Terr`, `DipUnder` wordt een anker op
   −`LevelH`. Meetlat: steilste helling ≤ 10%, `GradeStats`, en het terrein (`Terr`) is na leggen en slopen bit-gelijk
   aan ervoor.
2. **Classificatie en lange verbindingen uit d(s)**: `BoreSearch` krijgt één vergelijking (d tegen ±9, met
   `UnderClear` voor onder-een-weg-door) in plaats van `BoreNeed`/`BoreCover`/`SpanClear`/`SpanNeed`; snijpunten s van
   landhoofd en portaal worden onthouden per lange verbinding. Nog getekend met de oude tekenaar; meetlat: de tunnels en
   bruggen van de hoogtetest komen op dezelfde plekken of beter (foto 6).
3. **Terreinveld doorlopend**: stukken met maaiveld-deel en afstandskegels; de geulen (`cuts`, `Carve`, `Fill`,
   `ExactGround`, `CollectCuts`-geuldeel, `CarvePaths`, `UpdateCuts0`) weg; `SpanBottom`, de keermuur-uitzondering en
   `WallOut` uit `GroundField`; `CornerBase0` zonder `RoadCorner`/`SkewPathCorner`/`PathHalf` (de strook doet het).
   Meetlat: scan A = 0 (niets boven het wegdek), geen tanden (foto 3 opnieuw), de 5 cm-sprong blijft weg.
4. **Mesh per vak met eigen vlakken en wanden**: `Ground2Cell` met vlak-per-stuk, opschonen, snapping, wanden als
   quad-stroken uit de wandpolylijnen; `Skirt`/`SeamCell`/`Seams`, `Refine`/`Refine2`, de 2 cm-lijnen weg; `Ground2` is
   geen schakelaar meer maar de enige weg. Meetlat: scan B = 0 gaten, `Ground2Check` 0/0 met tolerantie 0, foto's 1–3
   en 9–10.
5. **Kunstwerken op de snijpunten**: landhoofd en portaal op s, sokkelvak, kamervak, bak met keermuren, brug-naar-portaal
   als één mantel; `BridgeBlock` gaat op in de mantel (een onderdoorgang is een koker met mantel). Meetlat: foto's 4,
   5, 7, 11, 12, 13 opnieuw; meldingen 7 en 8 naspelen (`melding 7`, `melding 8` in `niveautest.txt`).
6. **Snelheid**: sleutel met versie en wanden, emmers, `LateUpdate`-herbouw, bufferhergebruik, collider in een job;
   meten met de Profiler. Meetlat: klik op een volle planeet ≤ 0,2 s, opbouw van de hoogtetest ≤ 30 s.
7. **Opruimen**: `RoadShape`, `RoadSlopes`, `RoadField`, `Ground2` als schakelaars, `DebugGround`, `fillN`, `deckSide`,
   dode code in `PlanetCity.Cuts.cs` en `PlanetCity.Levels.cs`; CLAUDE.md bijwerken ("Waar staat wat").

## 11. Testvolgorde

Elke test: een kleine planeet of een hoek van de teststad, één handeling, de scan (A terrein boven het wegdek, B gaten,
C mesh tegenover H), `Ground2Check`, `GradeStats`, een foto van de slechtste plek, en Ctrl+Z terug met bit-gelijk
terrein. Verwachting per test staat erbij.

1. **Eén weg op een helling** (0,35 m/m, zoals TerreinTest "helling"): strook op weghoogte, talud aan beide kanten,
   geen kom (de hoekpunten liggen door de strook op weghoogte), helling ≤ 10% dus een insnijding bovenaan en een dijk
   onderaan. A = 0, B = 0.
2. **Insnijding** (weg over een heuvel van 5 m): talud 1:1,5 beide kanten, voet strak, geen 5 cm-sprong.
3. **Dijk** (weg over een laagte van 5 m): talud, zeshoekige kop aan het eind, de kop loopt ook in de lengte af.
4. **Keermuurstuk** (heuvel van 8 m, laagte van 8 m): bak met wanden, sokkelstuk; wandvoet bit-gelijk aan het terrein
   (geen rafels van onderen: foto 10), wand in eigen submesh.
5. **Dalbrug** (laagte van 20 m, 5 vakken): landhoofd op het punt d = 9 met talud ervoor (spill-through), dek, pijlers
   tot H, geen dek door de heuvel (foto 4), geen landhoofdblok tot in de grond (foto 12).
6. **Tunnel** (berg van 30 m): portaal op het punt d = −9, mantel dekt de snede, geen grond in de koker, lantaarns uit.
7. **Gelijkvloers kruispunt op een helling**: vier armen, één `RoadH` voor het kruispuntvak, alle armen ≤ 10%, de
   strook van de kop ruimer (stoepafronding), geen talud over een arm heen (de laagste weg wint).
8. **Onderdoorgang** (U, en een weg op +4,5 m over een weg op het maaiveld): koker van één vak met twee portalen op
   `UnderClear`, mantel in de dijk van de bovenste weg, bak met talud of keermuur ervoor.
9. **Brug naar portaal** (melding 8): één gecombineerde mantel; de scan vindt geen gat en geen losse driehoekjes.
10. **Kruispunt in de berg** (foto 7): kamervak zonder portalen, drie kokers die op de kamer uitkomen.
11. **Meldingen 7 en 8** naspelen, dan **`hoogtetest`** (20 landschappen, 30 wegen): doel steilste helling 10,0%,
    gaten 0, terrein boven het wegdek 0,0%, randpunten 0 ongelijk, opbouw ≤ 30 s, klik ≤ 0,2 s.
12. **Boetseren onder en naast een weg** (ophogen, afgraven, meteoor): de classificatie springt hooguit één keer
    (hysterese), geen flapperen; weg weghalen laat het terrein staan zoals de speler het boetseerde.

## 12. Bronnen

De zes notities in `docs/terrein-wegen-oplossing/` geven per bewering een URL; hieronder de bronnen die het ontwerp
dragen.

Spellen en engines [G], [K]
- Colossal Order / Paradox, CS2 Road tools: https://colossalorder.fi/?p=1547 ,
  https://www.paradoxinteractive.com/games/cities-skylines-ii/features/road-tools
- CS2 quays-patch (wegen "retaining walls and bridges only when there is a significant elevation change"):
  https://www.eprison.de/spiele/cities-skylines-ii/steam-news/1802261838707488/7442/97560.html
- CS2 modding, `GeometryFlags` FlattenTerrain/ClipTerrain: https://ps1ke.github.io/Cities-Skylines-2-Modding-Guide/Game/Net/GeometryFlags
- CS2 tunnels (−12,5 m, stappen 1,25–10 m; spelersgids): https://buzzaria.pages.dev/posts/how-to-build-tunnels-in-cities-skylines-2
- Cities: Skylines wiki, Roads (stappen 3/6/12 m, bruggen boven een hoogte): https://skylines.paradoxwikis.com/Roads
- Transport Fever, taluds als data (`embankmentSlopeLow/High`): https://www.transportfever.net/lexicon/entry/273-gleis-aufsch%C3%BCttungen/
- Transport Fever 2, brugdrempel en "a tunnel always gets walls": https://steamcommunity.com/app/1066780/discussions/0/1866119378820014259 ,
  https://steamcommunity.com/sharedfiles/filedetails/comments/2030394734
- OpenTTD slopes en foundations: https://docs.openttd.org/source/d7/d6d/slope__type_8h_source.html ,
  https://wiki.openttd.org/en/Archive/Manual/Settings/Build%20on%20slopes
- Unreal Landscape Splines (cosinus-afloop, raise/lower): https://dev.epicgames.com/documentation/en-us/unreal-engine/landscape-splines-in-unreal-engine
- EasyRoads3D Indent/Surrounding: https://nlp.fi.muni.cz/aiproject/ui/hutta_peter2019/pa026-project/Assets/EasyRoads3D/html/general_settings.html
- BeamNG Road Architect ("terrain follows the lowest road"): https://documentation.beamng.com/world_editor/tools/road_architect/terraforming/
- Factorio FFF-378 (hellingbanen, steunen): https://factorio.com/blog/post/fff-378
- Unity Stamp Terrain (Min/Set/Max): https://docs.unity3d.com/6000.4/Documentation/Manual/terrain-StampTerrain.html
- Unity Paint Holes / `SetHoles`: https://docs.unity.com/en-us/engine/current/manual/creating-environments/script-terrain/terrain-height/terrain-basic-sculpting/terrain-paint-holes
- Unreal Landscape Visibility: https://dev.epicgames.com/documentation/en-us/unreal-engine/landscape-visibility-tool-in-unreal-engine

Meetkunde en mesh [H], [M]
- Catlike Coding, Hex Map 3 (terrassen, kliffen als één quad), 4 (grenspunt niet verstoren), 5 (delen, Refresh in
  LateUpdate), 7 (wegen): https://catlikecoding.com/unity/tutorials/hex-map/part-3/ , .../part-4/ , .../part-5/ , .../part-7/
- CGAL Envelope_3: https://doc.cgal.org/latest/Envelope_3/index.html
- Shewchuk, robuuste predicaten: https://people.eecs.berkeley.edu/~jrs/papers/robust-predicates.abstract
- Cherchi e.a., interactieve robuuste mesh-booleans: https://arxiv.org/abs/2205.14151
- Zhou e.a., Mesh Arrangements for Solid Geometry: http://www.cs.columbia.edu/cg/mesh-arrangements/
- Clipper2 Robustness: https://www.angusj.com/clipper2/Docs/Robustness.htm
- Esri TIN, breaklines (hard = klif, muur, stoeprand): https://pro.arcgis.com/en/pro-app/3.5/tool-reference/3d-analyst/create-tin.htm
- Civil 3D breaklines en wall breaklines: https://help.autodesk.com/cloudhelp/2024/ENG/Civil3D-UserGuide/files/GUID-FCCAF28A-506B-4773-9706-DD1F1F8CC933.htm ,
  https://help.autodesk.com/cloudhelp/2015/ENU/Civil3D-UserGuide/files/GUID-67133CC6-D9DD-45B3-90F9-DA9FD0F2A3A7.htm
- Civil 3D corridor, daylight, overhang correction: https://help.autodesk.com/cloudhelp/2025/ENU/Civil3D-Subassembly-Ref/files/GUID-BB29AEA5-F447-43C9-BA6D-01BD7E290A08.htm ,
  https://help.autodesk.com/cloudhelp/2015/ENG/Civil3D-UserGuide/files/GUID-49E7C910-1FB7-41C8-9FCD-BDF8A43A78FE.htm
- Wikipedia, Constrained Delaunay triangulation: https://en.wikipedia.org/wiki/Constrained_Delaunay_triangulation
- Khronos, kieren door floating point en T-kruisingen: https://community.khronos.org/t/single-pixel-cracking-issue/55383
- Ulrich, chunked LOD (skirts): https://classes.cs.uchicago.edu/archive/2014/winter/23700-1/project_4_and_5/project-04.pdf
- godot_voxel, slivers in flat shading: https://github.imc.re/Zylann/godot_voxel/issues/714

Wegprofiel [P]
- Kyng, Rao, Sachdeva, Spielman, Algorithms for Lipschitz Learning on Graphs (COLT 2015): https://ar5iv.labs.arxiv.org/html/1505.00290
- CLRS 24.4, verschilbeperkingen: https://walkccc.me/CLRS/Chap24/24.4/
- Wikipedia, Lipschitz continuity (McShane-uitbreiding): https://en.wikipedia.org/wiki/Lipschitz_continuity
- Wikipedia, Dykstra's projection algorithm: https://en.wikipedia.org/wiki/Dykstra%27s_projection_algorithm
- Bačák, Hertrich, Neumayer, Steidl, ∞-Laplaciaan op grafen: https://ar5iv.labs.arxiv.org/html/1903.04873
- Unified Vertical Alignment (netwerken, rekentijden): https://arxiv.org/html/2508.15953v1
- Hare, Lucet, Rahman (EJOR 2015): https://ideas.repec.org/a/eee/ejores/v241y2015i3p631-641.html
- Softree, onhaalbare problemen en "softening": https://support.softree.com/knowledge-base/optimization/softree-optimal-concepts/infeasible-problems
- TxDOT Roadway Design Manual, hellingen (tot 15% in beperkte omstandigheden): https://www.txdot.gov/manuals/des/rdw/chapter-4--basic-design-criteria/4-8-vertical-alignment/4-8-1-grades.html

Kunstwerken en drempels [K]
- ČVUT 12SDK, oefening 2 (aardebaan tot 10 m, insnijding 6/7/8 m): https://k612.fd.cvut.cz/predmety/12SDK/English/Exercise/Texts/12sdk-c-02_ENG.pdf
- JICA (viaduct boven 9 m): https://openjicareport.jica.go.jp/pdf/12345716_02.pdf
- WSDOT Design Manual 730, keermuren: https://wsdot.wa.gov/publications/manuals/fulltext/M22-01/730.pdf
- FHWA BIRM 10.1, landhoofden (full height, stub, spill-through): https://www.cedengineering.com/userfiles/Abutments%20and%20Wingwalls%20(Part%201).pdf
- Transport Scotland, Milltimber Brae (portaal waar de insnijding genoeg hoogte geeft): https://www.transport.gov.scot/media/8433/milltimber-brae-tunnel-report.pdf

Snelheid [S]
- Unity MeshApiExamples (metingen): https://github.com/Unity-Technologies/MeshApiExamples/blob/master/readme.md
- Unity `Mesh.AllocateWritableMeshData`: https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/mesh/allocatewritablemeshdata
- Unity `Physics.BakeMesh`: https://docs.unity.com/en-us/engine/current/script-reference/unityengine/physics/bakemesh
- Unity `MeshUpdateFlags`: https://docs.unity.com/en-us/engine/current/script-reference/unityengine/rendering/meshupdateflags
- Unity `SetHeightsDelayLOD`: https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/terraindata/setheightsdelaylod
- Houdini PDG cache-regels: https://www.sidefx.com/docs/houdini/tops/pdg/workItemCacheState.html
- Veloren, buren meermaals in de rij: https://gitlab.com/veloren/veloren/-/merge_requests/119
- Ramalingam & Reps, incrementele kortste paden: https://research.cs.wisc.edu/wpis/abstracts/jalg96.abs.html
