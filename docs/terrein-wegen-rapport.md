# Rapport: terrein rond wegen in Raatstad — probleem, geschiedenis, open vragen

Datum: 7 november 2026. Doel: een onderzoeksvraag voor deep research, los van de huidige code. Lees dit als een
beschrijving van het probleem, niet als een voorstel.

## 1. De wereld

- Een bol met straal 600 m, verdeeld in 21.162 vakken (Goldberg-veelvlak: een icosaëder in 46×46 driehoeken; zeshoeken
  plus 12 vijfhoeken op de hoekpunten van de icosaëder). Hart-tot-hart tussen buurvakken ≈ 15 m; een vak heeft een
  middelpunt en 5 of 6 hoekpunten (afstand midden–hoek ≈ 7,8 m). Vakken zijn niet allemaal even groot of regelmatig
  (vervorming tot ~17% naast een vijfhoek).
- Ware schaal: 1 eenheid = 1 m. Terrein: één hoogte per vak (−12 tot 90 m boven de bol). Low-poly stijl: de bodem is een
  waaier van driehoekjes per vak (midden naar hoekpunten), elk driehoekje zijn eigen vlak en normaal; geen gladde
  interpolatie, geen textuur-blending die vormfouten verbergt.
- Een hoekpunt is van drie vakken tegelijk. De hoogte ervan is een regel (zie 3). Alles wat er langs een vakrand
  misgaat is meteen zichtbaar als kier (je kijkt door de planeet heen) of als tand.
- Wegen liggen op de vakken: een weg is een reeks vakken (hart naar hart, buur naar buur onder 60°, of "schuin": naar het
  vak op afstand 2 onder 90°, dwars over een rand tussen twee halve vakken). Wegbreedte: asfalt 7 m, met stoep ~12 m, een
  pad 6,4 m. Verkeer rijdt van hart tot hart over een rechte lijn (helt mee), met een afronding in bochten.
- Kunstwerken: onderdoorgang (een weg kruist een andere op een ander niveau: koker, portalen, betonblok), tunnel door
  een berg (koker, portaal, zeshoekige mantel die uit de helling steekt), dalbrug (dek, borstwering, landhoofden,
  pijlers om de 24 m). Niveaus: een weg kan met Page Up/Down op een hoogte boven of onder het maaiveld worden gelegd
  (halve stappen van 2,25 m; een vol niveau 4,5 m).

## 2. Wat het moet doen (eisen)

1. Een weg sluit altijd aan op het terrein: onder de weg ligt het wegdek, ernaast loopt de grond met een talud (1 op 1,5)
   naar het terrein. Nooit grond door of boven het wegdek, nooit een kier.
2. Wegen zijn nooit onrealistisch steil (richtwaarde hooguit 10%; tunnels 15%). Het landschap mag kliffen van 90 m
   hebben; een weg daarover vraagt dus om een profiel met insnijdingen, dijken, bruggen en tunnels.
3. Waar een ophoging of insnijding te hoog wordt (meer dan ~14 m) hoort er een brug, een tunnel of een keermuur te komen,
   geen dijk van 50 m.
4. Twee buurvakken tekenen hun gedeelde rand precies gelijk (geen T-kruisingen met hoogteverschil, geen naden als
   lapmiddel).
5. Het moet interactief blijven: een klik (één weg slepen) op een volle planeet in ~0,2 s; een hele planeet opbouwen in
   enkele tientallen seconden. Het tekenen gaat per deel van 128 vakken.
6. Het terrein zelf blijft boetseerbaar door de speler (ophogen, afgraven, egaliseren); een weg weghalen zet alles terug.
7. Kunstwerken (dek, koker, portaal, landhoofd, keermuur, sokkel van een gebouw, kademuur) moeten naadloos aansluiten op
   het terrein dat uit dezelfde gegevens komt.

## 3. Hoe het nu werkt (in grote lijnen)

- Hoekpunthoogte (`CornerBase0`): het laagste van de drie vakken; tussen twee verbonden wegvakken de weghoogte
  (`RoadCorner`); bij een gracht de kade, bij een meer een dam. Hoekpunten worden dus per regel gekozen, niet uit één
  functie.
- Een weg op een niveau, en sinds kort elke weg (`RoadField`), komt in een **hoogtefunctie** (`GroundField`): per
  wegstuk (hart tot hart) een strook op weghoogte met een talud naar buiten (tot 14 m hoogteverschil, daarbuiten een
  sprong), per uiteinde/bocht/splitsing een zeshoekige "kop", voor gewone wegen in de buurt een vlak stuk. Het resultaat is
  een min/max van vlakken: eerst de hoogste ophoging, dan de laagste uitgraving, dan de geulen van onderdoorgangen, dan
  de bodem onder een brugdek. Elk vak dat binnen bereik ligt wordt uit die functie getekend: het waaierdriehoekje wordt
  gesneden langs alle knikken van de functie (`LineGround`: de wegranden, de voet van het talud, begin en eind van elk
  stuk, de zijden van een kop) en elk stuk apart op hoogte gezet.
- Daarnaast verzet een weg het terrein zelf: een weg op een niveau zet de vakhoogte (`LevelTerr`, met de oude hoogte in
  `preLevel`), een onderdoorgang graaft een geul in de buurvakken (`Carve`/`preCarve`), een dalbrug drukt de grond
  onder het dek omlaag, een tunnel snijdt het terrein binnen de mantel weg (`BoreTri`), een kruising met een weg op
  een ander niveau laat de nieuwe weg onder de oude door dippen (`DipUnder`). Dat zijn vijf verschillende manieren
  waarop "de weg" het terrein beïnvloedt, elk met eigen gegevens en eigen tekencode.
- Tunnels en bruggen worden achteraf gezocht (`BoreLine`): op een recht stuk de langste reeks vakken waar het terrein
  ≥ 2,25 m boven de rechte lijn van hart tot hart ligt (en ergens ≥ 9 m) wordt een tunnel; waar het eronder ligt een
  dalbrug. Die rechte lijn is een aanname; het profiel van de weg volgt verder gewoon de vakhoogtes.

## 4. Wat er is geprobeerd (chronologisch) en waarom het niet volstond

1. **Naden** (stroken van 10 m naar beneden langs vakranden) om kieren af te dekken. Werkt als pleister; verbergt fouten,
   lost niets op; bij een wand zie je ze.
2. **Eén hoogtefunctie H(punt)** voor alles wat een maaiveldhoogte nodig heeft (lopen, rijden, bruggen), met een scan
   die meet hoe ver de mesh van H afwijkt. Vond echte fouten (de overkant van de planeet leek vlakbij; een stuk dat
   schuin naar de verste hoek van een veelhoek liep). Maar H bevatte dezelfde per-vak keuzes als de tekencode, dus
   twee buren konden op hun rand nog steeds verschillen.
3. **Gedeelde randpunten per vakrand** (Terrein v2): elke rand één keer berekend als polylijn (snijlijnen van de
   hoogtefunctie plus knikzoeker), door beide buren gebruikt; sokkelwanden tot op diezelfde punten. Dit werkt:
   0 hoeken en 0 randen ongelijk; op een gewone stad 0 kieren. Dit onderdeel is houdbaar.
4. **Exacte vlakken binnenin een vak**: elke veelhoek wordt gesneden langs de snijlijnen van alle vlakken die er
   gelden (basis, wegdek, talud per stuk, kop, geul, dijk, brugdek) tot elk stuk één vlak is, en buurveelhoeken worden
   aan elkaar geregen. Werkt, maar: elk nieuw soort kunstwerk moet als "vlak" in deze lijst; en de functie heeft
   sprongen (wanden) die als stroken van 2 cm getekend worden, wat van opzij tanden geeft.
5. **Hellingbegrenzer** per lijn, daarna planeetbreed, daarna met begrensd grondverzet (±12 m), daarna met vaste
   portalen, daarna met vaste kruispunten. Elke variant verplaatste het probleem: óf te steile stukken bij kruispunten
   die dicht bij elkaar liggen, óf dijken van 50 m, óf een dalbrug waarvan het portaalvak verzet was zodat het dek door
   de heuvel liep.
6. **"Eerst het profiel, dan de kunstwerken"** (`ProfileAll`): alle wegen samen op hooguit 10% (iteratieve ontspanning
   per verbinding), dan tunnels/bruggen gezocht aan dat profiel in plaats van aan de rechte lijn; de weghoogte als
   eigen gegeven (`RoadH`) naast het terrein, dat niet meer verzet wordt. Dit is de goede richting: 0 te steile
   verbindingen, terrein blijft liggen. Maar het terrein eromheen komt nog uit de oude hoogtefunctie met uitzonderingen
   ("als RoadH, dan…"): keermuur op de wegrand waar het talud niet reikt, geen bodem onder het dek, dek van hart tot
   hart, ondergronds kruispunt zonder portalen. Elke uitzondering verschoof de meting (gaten 31 → 107 → 77; terrein
   boven het wegdek 75 → 1872 punten, bijna alleen in landhoofdvakken).
7. Langer talud (60 m) in plaats van een wand: 7× zo traag en dijken van 50 m. Teruggedraaid.

Kernoorzaak, zoals ik hem nu zie: **er zijn meerdere bronnen van waarheid** (vakhoogte, verzet vakhoogte, oude hoogte,
geul, bodem onder een dek, mantelsnede, en nu ook het profiel) en meerdere tekenaars (vak-waaier, hoogtefunctie met
snijlijnen, sokkel, kade, portaal, mantel, dek). Elke combinatie van twee ervan op hetzelfde vak is een geval apart, en
een stress-planeet met kliffen van 90 m en dertig kruisende wegen produceert al die combinaties tegelijk. Het meten
(gaten, grond boven de weg, afwijking van H) werd het doel, terwijl het beeld het doel is.

## 5. Meetlat die er nu is (voor wie het wil nameten)

Testvlag `hoogtetest v2 profiel` (20 landschappen met 30 wegen van startpunt naar startpunt): steilste helling 10,0%,
gaten 77, terrein boven het wegdek 1,9% van de punten op de weg (bijna allemaal landhoofdvakken van dalbruggen),
randpunten 0 ongelijk. Op een gewone stad (Bug planet): 0 gaten, 0 boven de weg. Een opbouw kost 25–60 s.

## 6. Onderzoeksvragen

1. Hoe lossen citybuilders dit op? Cities: Skylines (1 en 2), Transport Fever 2, Workers & Resources, Anno, Banished,
   Timberborn, OpenTTD en Factorio hebben elk een andere aanpak van "weg vormt terrein": heightfield-stempels langs een
   spline met een dwarsprofiel (CS2), netwerk-knopen met eigen hoogte en automatische brug/tunnel bij een drempel
   (Transport Fever), of helemaal geen vervorming (OpenTTD: de weg volgt rasterhellingen). Welke past bij een
   hex-raster op een bol met één hoogte per vak en low-poly driehoekjes?
2. Is "één hoogtefunctie die een min/max van vlakken is, exact gesneden" de juiste keuze, of is een **gelaagd
   heightfield op fijnere resolutie** (bijvoorbeeld 6 of 12 punten per vak, gestempeld door de wegen met een dwarsprofiel)
   eenvoudiger en robuuster, ook al is het niet exact? Hoe verhoudt dat zich tot low-poly stijl (eigen normaal per
   driehoek) en tot kieren tussen vakken?
3. Wegprofiel: welke algoritmes bepalen een verticaal alignement met maximale helling en minimaal grondverzet over een
   graaf met kruispunten (niet alleen over één lijn)? Zoek naar "vertical alignment optimization", "road profile
   grade constraint", "cut and fill balancing", "earthwork optimization", "piecewise linear fit with slope constraint"
   (ook "isotonic/Lipschitz regression"). Hoe gaan ze om met twee vaste punten die te dicht bij elkaar liggen
   (onvermijdelijk steil, of een omweg)?
4. Kunstwerken uit het profiel: welke drempels en regels gebruiken anderen voor "hier wordt het een brug" en "hier een
   tunnel" (hoogteverschil met het terrein, minimale lengte, afstand tussen portalen, kruispunt in de berg)? En hoe
   tekenen ze een landhoofd, portaal en keermuur zodat de overgang naar het terrein altijd dicht is: door het terrein
   af te snijden op een vlak (CSG/booleans), door het kunstwerk met een "schort" in het terrein te laten steken, of door
   het terrein zelf om het kunstwerk heen te genereren?
5. Mesh: wat is de gangbare manier om een stuksgewijs vlakke functie met sprongen (wanden) als dichte mesh te tekenen
   zonder tanden: de wand als eigen quad-strip tussen de twee niveaus, of de 2 cm-strook die nu wordt gebruikt?
   Zoek naar "constrained Delaunay triangulation of terrain with breaklines", "terrain with cliffs / discontinuities",
   "watertight mesh from piecewise planar function".
6. Snelheid: hoe houd je dit interactief (0,2 s per klik) als het terrein alleen in de buurt van een gewijzigde weg
   opnieuw hoeft (welke vakken, welke cache, welke sleutel)?

## 7. Randvoorwaarden voor een oplossing

- Alles hexagon/driehoek, hoeken van 60° en 90°; geen vrije vormen. Low poly: harde driehoekjes, geen gladde normalen.
- Eén hoogte per vak blijft de bron voor het terrein (boetseren, opslag, meteoorkraters, meren, zee op −1 m).
- Wegen als graaf van vakken; verkeer en lopen lezen een hoogte per vak en een helling van hart tot hart.
- De speler moet een weg kunnen slepen, ongelijkvloers maken (U), op een niveau leggen, en weghalen met herstel.
- C#, Unity 6, URP; meshes per deel van 128 vakken; de bodem als één mesh met een shader die gras/rots/sneeuw per pixel
  bepaalt.

## 8. Wat er nu in de code staat dat bruikbaar blijft

- `PlanetGround2.cs`: `H(punt)`, gedeelde randprofielen (`EdgeProfile`), exacte vlakkensplitsing (`PlaneLins`),
  `Ground2Check` (rand- en hoekcontrole), `Ground2Dump` (alle driehoekjes van een vak).
- `PlanetCity.Grade.cs`: `ProfileAll` (profiel voor alle wegen), `GradeStats` (hellingstatistiek), `RoadH`.
- `WegVoorbeeld.cs`: de scan (A: grond boven het wegdek, B: gaten, C: mesh tegenover H), foto's per slechtste plek,
  vlaggen `v2`, `profiel`, `geennaden`, `melding <nr>` (een bug van de speler naspelen).

## 9. Foto's (map `docs/terrein-wegen-rapport/`)

Alle foto's komen van de testplaneet (`hoogtetest`: 20 landschappen met kliffen tot 90 m, 30 wegen van startpunt naar
startpunt), tenzij anders vermeld. De foto's staan in de volgorde van het verhaal in deel 4.

![Spitsen naast de weg](terrein-wegen-rapport/01-spitsen-naast-weg.jpg)
**1. Spitsen naast een weg bij een kruispunt aan een tunnelportaal.** De hoogtefunctie knikt op plekken die de
snijlijnen niet kennen (twee taluds die elkaar kruisen, een kop tegen een stuk); de verfijning via het midden van een
driehoekje maakte daar spitsen van, met gaten ertussen.

![Dezelfde plek na de knikzoeker](terrein-wegen-rapport/02-zelfde-plek-na-knikzoeker.jpg)
**2. Dezelfde plek na de knikzoeker en de exacte vlakkensplitsing.** De spitsen zijn weg; de insnijding links van de weg
is strak. Dit deel van de aanpak (gedeelde randpunten, exacte vlakken) houdt stand.

![Wand met tanden](terrein-wegen-rapport/03-wand-met-tanden.jpg)
**3. De grens van het talud.** Het talud reikt hooguit 14 m hoogteverschil; daarboven zet de hoogtefunctie een
loodrechte wand, en de vlakken die daar in 2 cm van weghoogte naar terreinhoogte vallen zien er van opzij uit als
tanden. Hier ligt de weg op een dijk die eigenlijk een brug had moeten zijn.

![Melding 7](terrein-wegen-rapport/04-melding7-dalbrug-door-heuvel.jpg)
**4. Bug van de speler (melding 7): "wat gebeurt er hier met de brug?"** De hellingbegrenzer had het portaalvak van een
dalbrug verzet; het dek (hart tot hart, een rechte lijn) liep daardoor anders dan het terrein eronder: een trog door de
heuvel en een reusachtig landhoofdblok. Voorbeeld van twee bronnen van waarheid die elkaar tegenspreken.

![Melding 8](terrein-wegen-rapport/05-melding8-brug-eindigt-in-tunnel.jpg)
**5. Bug van de speler (melding 8): "waarom misvormt het terrein zich hier?"** Een dalbrug eindigt precies op een
tunnelportaal. Op dat ene vak gelden drie terreinregels tegelijk: de bodem onder het dek omlaag, de grond bij het
portaal omhoog met talud, en de mantel van de tunnel die het terrein wegsnijdt. Wat overblijft zijn losse groene
driehoekjes en een gat in de planeet.

![Profiel: brug en tunnel](terrein-wegen-rapport/06-profiel-brug-en-tunnel.jpg)
**6. Eerst het profiel, dan de kunstwerken.** Alle wegen op hooguit 10%; waar het terrein ver onder het profiel ligt
een dalbrug (links over de vlakte), waar het er ver boven ligt een tunnel (rechts de berg in). Dit is de goede richting.

![Drie portalen op een kruispunt](terrein-wegen-rapport/07-drie-portalen-op-kruispunt.jpg)
**7. De keerzijde van het profiel:** een kruispunt dat door het profiel diep in de berg komt te liggen krijgt drie
tunnels die elk met een portaal op hetzelfde vak eindigen (hier in de sneeuw). Een kruispunt bij het terrein vastzetten
gaf elders 45 te steile stukken; het werd uiteindelijk een ondergronds kruispunt (foto 11).

![Insnijding met portaal](terrein-wegen-rapport/08-insnijding-met-portaal.jpg)
**8. Wat wél goed gaat met het profiel:** een weg in een insnijding, netjes op helling, met het portaal van een tunnel
aan het eind. Het terrein blijft liggen; de weg ligt er als profiel overheen.

![Keermuur op een dijk](terrein-wegen-rapport/09-keermuur-op-dijk.jpg)
**9. Keermuur op de wegrand.** Waar het talud niet reikt staat nu een stenen wand op de wegrand in plaats van de wand
met tanden van foto 3. Dit is een uitzondering in de oude hoogtefunctie ("als er een profiel is, dan…"), geen eigen
kunstwerk.

![Keermuur naast een brugdek met rafels](terrein-wegen-rapport/10-keermuur-naast-brugdek-rafels.jpg)
**10. Dezelfde keermuur naast een brugdek, van onderen.** Onderaan rafels: het dek (eigen tekencode) en de wand (uit de
hoogtefunctie) ontmoeten elkaar niet; de strook van 2 cm loopt als slierten door.

![Ondergronds kruispunt](terrein-wegen-rapport/11-ondergronds-kruispunt.jpg)
**11. Ondergronds kruispunt, van binnen gezien.** Het kruispunt van foto 7 krijgt geen portalen meer; de kokers lopen
door tot het hart van het vak. Ruw: de kokerwanden kruisen elkaar zonder splitsing en de gewone lantaarns staan er nog.

![Dalbrug met landhoofd op het hart](terrein-wegen-rapport/12-dalbrug-landhoofd-op-hart.jpg)
**12. Dalbrug met het dek van hart tot hart en het landhoofd op het hart van het landhoofdvak.** Het dek en de pijlers
zijn goed; het landhoofdblok (rechts, grijs) loopt tot in de grond en wordt enorm als het hart hoog boven het terrein
ligt. In het vak zelf ligt een deel van de stoep onder het terrein (de meting "terrein boven het wegdek" zit bijna
helemaal in zulke vakken).

![Dalbrug naar een tunnel](terrein-wegen-rapport/13-dalbrug-naar-tunnel.jpg)
**13. Dalbrug die in een tunnelportaal uitkomt**, dezelfde situatie als melding 8 maar met het profiel. De overgang
dek–portaal is hier toevallig goed; de regel die dat garandeert bestaat niet.
