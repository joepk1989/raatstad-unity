# Raatstad afmaken: het eindplan

Dit plan is gemaakt uit de kaart van acht gebieden, drie plannen en twee rondes kritiek. Alles komt uit het lezen van de code. Er is niets gedraaid in Unity. Waar iets alleen is afgeleid en niet gezien, staat dat erbij.

De gekozen lijn: **eerst een speelbare eerste versie voor anderen**, klein gehouden. Wat traag of ongetest is, gaat achter een ontwikkelaarsschakelaar in plaats van dat het nu gerepareerd wordt. Uit de andere plannen komen mee: de veiligheid van opslaan, de tests laten draaien zoals het spel draait, en een klein speldoel met een ronde.

Vier sessies lopen al: Terrein v2, modules en de Raatmaker, zelf rijden, instellingen. Hun werk heet hieronder "loopt".

---

## 1. Waar staat de game nu

| Onderdeel | Staat | Grootste gat |
|---|---|---|
| Planeet (bol, polen, vakken) | Af | Geen |
| Terrein en boetseren | Werkt, met gebreken | Tijdens het boetseren springt een weg pas bij loslaten. Geen hoogtelijnen |
| Terrein v2 (wegprofiel en bodem) | Loopt | Een nieuw gemaakte stad krijgt geen wegprofiel tot de eerste klik. Oude en nieuwe opbouw lopen op 42 plekken door elkaar |
| Snelheid op een volle planeet | Loopt | 46 s herbouw en 0,6 tot 1,6 s per klik volgens de docs. Het echte spelpad is niet gemeten |
| Wegen, paden, fietspaden | Af | Sterkste deel van het spel |
| Bruggen, tunnels, niveaus | Werkt, met gebreken | Ongelijkvloers op het maaiveld (U) is onder Terrein v2 naar de code stuk. Niveaus worden aan de uiteinden uitgesmeerd. Loopbrug kan niet |
| Rotonde | Werkt, met gebreken | De ring kan loskomen van het midden bij een latere wegwijziging |
| Modules: formaat, bibliotheek (159 stuks), tekenen | Af, loopt | Geen |
| Modules plaatsen | Werkt, met gebreken | Niet op halve wegvakken, vijfhoeken, paden of water. Boven 159 modules is de snelheid nooit gemeten |
| Raatmaker | Half, loopt | Kan een al geplaatste module stuk maken (van 1 naar 7 vakken). Bewaart op een plek die niet in een losse versie komt |
| Twee bouwsystemen | Naast elkaar | Startstad en toets 7 zijn modules. Bouwbalk, dorpen, voorbeeldstad, Planeet vol en AI-bouwer zetten blokken |
| Schoonheidsscore | Werkt voor blokken | Ziet modules niet, dus klopt niet in de startstad. De rij Afwisseling is met modules makkelijk vol te krijgen |
| Drukte, praatjes, namen, hoogtes | Werkt voor blokken | Een module telt als niets en als 0 m hoog. Mensen zeggen "Meer woningen graag!" in een stad vol huizen |
| Mensen en verkeer | Breed, werkt | Mensen lopen niet over pleinen van modules. Auto's remmen niet voor de wandelaar. Het verkeer geeft je eigen auto nooit voorrang |
| Zelf lopen (L) | Half | Je loopt naar de code door hekken en leuningen heen en kunt van een trap vallen. Niet in het spel gezien |
| Zelf rijden (J) | Loopt | Zie de autosessie |
| AI-bouwer | Werkt met blokken | Kent geen modules en legt wegen nog op de oude manier |
| Dorpen, voorbeeldstad, Planeet vol | Werken met blokken | Geen modules, geen wegprofiel |
| Opslaan en steden | Werkt, met gebreken | Een stad die niet gelezen kan worden, wordt na 3 minuten of bij afsluiten overschreven met een lege planeet. Geen versienummer in het bestand |
| Speldoel | Ontbreekt | Alleen het cijfer. Geen reden om door te bouwen of opnieuw te beginnen |
| Spelmodi | Alleen vrije modus | De regels van duel, multiplayer en oorlog zijn niet besloten |
| Interface en uitleg | Breed, werkt | Uitleg voor een nieuwe speler is vier regels. Esc opent geen menu. Geen Afsluiten in het menu |
| Ontwikkelaarsgereedschap | Niet afgeschermd | Terreintest, Planeet vol, Ontwikkelaar-menu en testscripts zijn voor iedereen bereikbaar |
| Beeld | Veel staat er | De zon verlicht maar de helft van de bol. Geen schaduw in de bouwstand (50 m bereik, camera op 230 m). Kartelranden |
| Geluid | Veel staat er | Geen volumeregelaar. In de bouwstand is de stad stil. Vogels alleen bij het oude blok park |
| Losse versie (build) | Ontbreekt | Nog nooit gemaakt. Bedrijfsnaam is nog DefaultCompany, en die bepaalt de map met steden |
| Werk veiligstellen | Open | 27 commits staan alleen lokaal. Drie testbestanden staan niet in git |
| Samenwerken van sessies | Kwetsbaar | Eén Unity, één vlagbestand, één testslot. Elke opgeslagen `.cs` herstart Play en breekt de test van een ander af |

---

## 2. Wat is de eerste versie

**Wat erin zit**

- Een losse Windows-versie met het versienummer in beeld.
- Startscherm met Startstad, Lege planeet en Natuurplaneet. Steden openen en bewaren zonder dat er iets stil verloren gaat.
- Vrije modus: wegen, paden, fietspaden, rotonde, niveaus met Page Up en Page Down, bruggen en tunnels zoals Terrein v2 ze nu maakt, groen, water, boetseren.
- Gebouwen zet je met modules. De blokken woning, kantoor, winkel, dak, blok, toren en molen staan niet meer in de bouwbalk. Park en plein blijven onder Groen, omdat er op een half wegvak geen module past.
- Score, tip, drukte, praatjes, Aanwijzen en ruimtekaart tellen modules mee.
- Lopen door je stad zonder door hekken te vallen, rijden, cameramodus, weer, dag en nacht, muziek, meteoor.
- Een klein doel: beste score per stad, het woord (Rommelig tot Prachtig) als mijlpaal, drie opdrachten. Als jij daartoe besluit ook een ronde op tijd met een uitslag.
- Een korte uitleg in stappen, een bijgewerkt venster Bediening, Esc opent het menu, Afsluiten in het menu.
- F8-melding met een knop "Map openen" en het versienummer erin.
- Instellingen voor volume, scherm en muis.

**Wat er bewust niet in zit**

Verborgen achter de ontwikkelaarsschakelaar:
- Planeet vol en Terreintest op het startscherm.
- De AI-bouwer, "Voorbeeldstad hier" en "Dorpje hier" (zetten blokken tussen modules).
- Het Ontwikkelaar-menu, de console, de Talud-schuif, Terreinlijnen en de planeetgrootte.
- De knop Ongelijkvloers (U) bij wegen, tot hij in het spel bewezen werkt.
- De wegdekmodules in de lade (ze doen nu niets).

Niet gebouwd voor deze versie:
- Duel, multiplayer, oorlogsmodus.
- Snelheid op een volle planeet, modules samenvoegen.
- AI-bouwer en generatoren op modules, oude blokken omzetten.
- Mensen met doelen en deuren, parkeren, bus, tram, trein, spoor.
- Loopbrug, gelaagde stad, hoogtelijnen, tunnels achteraf.
- Bewegend water, texturen op wegen, wolken en maan vanaf de grond.
- Controller, vertalen, toetsen toewijzen, opnieuw doen (Ctrl+Y).
- Opruimen van de oude terreinopbouw en van de dode code van het platte raster.

**Stopregel.** Loopt het lopen door modules uit, dan gaat de versie uit met "lopen op straat werkt, binnen deels". Is de ronde na tien keer spelen niet leuk, dan gaat de versie uit met alleen beste score, mijlpalen en opdrachten.

---

## 3. De volgorde

Vooraf, voor alle fasen: schrijven kan tegelijk, testen niet. Er is één Unity. Reken op een rij voor het testen. Het bestand `PlanetBuilder.cs` komt in bijna elke fase terug: daar werkt steeds één sessie tegelijk in (hieronder "de interfacesessie").

### Fase 0: kijken, besluiten, veiligstellen

**Doel.** Niets kan meer verloren gaan. De afgeleide gebreken zijn in het spel gezien of weggestreept. De besluiten die later werk blokkeren zijn genomen. Bijna geen code.

**Stappen**

1. Joep: de 27 lokale commits pushen.
2. Joep: een uur spelen in de huidige editor en bij alles wat opvalt F8 drukken. Kijk in elk geval naar:
   - de startstad nadat je hem hebt gesloten en opnieuw geopend (die heeft dan al een wegprofiel): de verlaagde fietsring en de zes bruggen;
   - een weg onder een weg met U op vlak terrein;
   - lopen: een huis in, een trap op, tegen een hek;
   - lantaarns in de nacht, het weer Mist op de gewone camera, het venster Bediening;
   - een rotonde op een helling, en boetseren vlak naast een weg.
3. Eén sessie zonder code: de meldingen lezen en per punt opschrijven "bevestigd" of "valt mee", in een nieuw bestand `docs/kijkronde.md`. Dit kan hele stappen hieronder schrappen of vergroten.
4. Joep beslist, een kwartier per punt:
   - **Het doel.** Beste score, mijlpalen en opdrachten: ja of nee. Een ronde erbij: ja of nee, en op tijd of op zetten. Advies: op tijd, want een gesleepte weg van twintig vakken is één zet en Ctrl+Z maakt tellen onduidelijk. Zet het besluit in `docs/spelmodi.md`.
   - **De bedrijfsnaam.** Die bepaalt de map met steden, meldingen en eigen modules. Na het uitdelen kan hij niet meer veranderen.
   - **De zon.** Overal dezelfde tijd (advies) of een draaiende planeet.
   - **Halve wegvakken en vijfhoeken.** Advies voor nu: daar past alleen groen.
   - **Totale chaos** bereikbaar voor testers of niet.
   - **De drie testbestanden.** `LevelTest.cs` in git (de terreinsessie gebruikt hem dagelijks). `DriveTest.cs` en `TunnelTest.cs` in git of weg.
   - **Hoe de versie bij testers komt** en waar zij hun meldingsmap heen sturen.
5. Joep: één ronde met de hand spelen. Natuurplaneet, wekker op 15 minuten, score opschrijven, en noteren wat leuk was. Dat is de goedkoopste toets van het doel.

**Wat tegelijk kan.** Alles. Stap 1, 2, 4 en 5 zijn van Joep. Stap 3 raakt geen code. De lopende sessies werken gewoon door.

**Klaar als.** Alles is gepusht, `docs/kijkronde.md` bestaat, en de besluiten staan in `docs/spelmodi.md`.

### Fase 1: rustig samenwerken en niets meer kwijtraken

**Doel.** Sessies breken elkaars tests niet meer af. Een tester kan zijn stad niet kwijtraken en komt niet in een toestand die geen test dekt. Er is een eerste proefversie buiten de editor.

**Stappen**

1. `Assets/Editor/AutoHerstart.cs`: Play niet herstarten terwijl er een test loopt, en `brugtest.txt` eruit (geen script leest het nog). Dit is de eerste codewijziging van het hele plan.
2. Elke test een eigen vlagbestand en een eigen testslot. Nu lezen vier scripts hetzelfde `niveautest.txt` en gebruiken ze allemaal slot 9. `LevelTest.cs` en `TunnelTest.cs` door de terreinsessie, `DriveTest.cs` door de autosessie, het naspelen van meldingen in `Meldingen.cs` door één sessie.
3. Steden veilig, in `StartScreen.cs` (`OpenRoutine`) en `CityFiles.cs`. Lukt het lezen niet: lege planeet in het geheugen, geen huidige stad meer (`Current` leeg, niet `MarkOpened`), terug naar het startscherm met een melding. `CityFiles.WriteText` laat een reservekopie (`.bak`) achter, ook als een oude startstad stil door een nieuwe wordt vervangen.
4. De eerste wijziging in een nieuwe startstad telt altijd. Nu gaat dat mis via het startscherm (`startFresh` in `PlanetCity.UserEdit`). Gelijk trekken op beide plekken waar een startstad ontstaat (`StartScreen.OpenRoutine` en `PlanetBuilder.Start`), of `startFresh` schrappen.
5. Terrein v2-sessie, in `PlanetCity.Save.cs`: een versienummer `ver` in het stadsbestand. `Restore` controleert eerst (planeetgrootte, versie) en wist de bestaande stad pas daarna. Nu wist hij eerst en kan hij halverwege stoppen.
6. Terrein v2-sessie: `PlanetCity.Reprofile` aanroepen na het maken van een stad (`StartScreen.cs` regel 121-131, `PlanetBuilder.cs` regel 132 en 554). In `PlanetCity.Grade.cs` de oude weghoogtes ook wissen als het laatste wegvak weg is (de terugkeer bij nul wegvakken staat nu boven `RoadH.Clear()`). Een vlagtest erbij: nieuwe stad maken, één klik, vergelijken met opnieuw openen.
7. Terrein v2-sessie: `LevelTest.cs` laten draaien zoals het spel draait, dus met wegprofiel. De rode gevallen op een lijst. Niveaus met Page Up en Page Down horen daarbij, want die vervangen U in de eerste versie. In `PlanetBuilder.cs` regel 147 de dubbele herberekening bij Ctrl+Z weghalen.
8. Instellingensessie: een eigenschap `Dev.On` in de **bestaande** klasse `Dev` in `DevTools.cs`. Geen nieuw bestand: een tweede klasse `Dev` compileert niet. Aan in de editor, uit in een gewone losse versie.
9. Verbergen achter `Dev.On`, per bestand een kleine wijziging:
   - `StartScreen.cs`: Planeet vol en Terreintest;
   - `PlanetBuilder.Settings.cs` en `PlanetBuilder.Dev.cs`: Ontwikkelaar, planeetgrootte, Terreinlijnen, console;
   - `PlanetBuilder.HexMenu.cs` en `PlanetBuilder.Cursor.cs`: AI-bouwer, Voorbeeldstad, Dorpje hier, de woorden "voor Claude";
   - `PlanetBuilder.cs`: Talud-schuif en de knop Ongelijkvloers bij wegen (regel 1175 en 1184), en U uitzetten als hij verborgen is;
   - `PlanetBuilder.Modules.cs`: wegdekmodules uit de lade.
10. De startfuncties van de testscripts (`WegVoorbeeld.cs`, `LevelTest.cs`, `DriveTest.cs`, `TunnelTest.cs`, `Meldingen.cs`) doen niets als `Dev.On` uit staat.
11. Modulessessie, in `PlanetCity.Modules.cs`: een module die niet gevonden wordt, tekent als grijze doos en wordt bij het openen één keer gemeld. Nu is hij onzichtbaar en blijft het vak bezet.
12. Op één afgesproken stil moment: de bedrijfsnaam zetten in de projectinstellingen en de map met steden één keer overzetten. Alle sessies krijgen het nieuwe pad.
13. Eerste proefversie maken, **zonder** het vinkje "Development Build" (anders staat alles wat verborgen moest zijn weer aan). Opstarten met een lege map. Alleen opschrijven wat stuk is: roze of dichte materialen, haar van de figuren, lucht, pauze als het venster niet vooraan staat. Meet ook de opstarttijd en de tijd van "Startstad maken". Laat één buitenstaander tien minuten spelen en zet het op een gewone laptop: noteer de beeldsnelheid.

**Wat tegelijk kan.** Stap 1 eerst en alleen. Daarna tegelijk: stap 3 en 4 (stedensessie), stap 5 tot 7 (terreinsessie), stap 8 en de instellingenbestanden van stap 9 (instellingensessie), stap 11 (modulessessie). De regels in `PlanetBuilder.cs`, `PlanetBuilder.HexMenu.cs` en `PlanetBuilder.Cursor.cs` door de interfacesessie, na elkaar. Stap 12 en 13 pas als iedereen heeft gecommit en niemand Unity gebruikt.

**Klaar als.** Een expres beschadigd stadsbestand geeft een melding en blijft op schijf heel. Een nieuwe startstad ziet er voor en na de eerste klik en na opnieuw openen hetzelfde uit. Met `Dev.On` uit is niets van het ontwikkelaarsgereedschap te vinden. Er ligt een lijst van wat in de proefversie stuk was.

### Fase 2: modules tellen mee

**Doel.** De startstad heeft een score, drukte, praatjes, namen en hoogtes die kloppen met wat er staat. De score is het enige doel van de speler, dus dit gaat voor de bouwbalk en de uitleg.

**Stappen**

1. Modulessessie, eerst en alleen: één leesfunctie `PlanetCity.Inhoud(vak)` in `PlanetCity.Modules.cs`. Hij zegt wat er staat (wonen, werken, winkel, horeca, openbaar, plein, groen, water, blikvanger, niets), hoeveel lagen, of het een gebouw is, en of het vak het midden of een buurvak van een module van 7 vakken is. Voor modules uit `ModuleV2.cat` en `MV2.MetaOf`, voor oude blokken uit de naam. Onthouden per moduletekst, niet per vak. Niet `CellInfo` noemen: die naam bestaat al. Daarnaast `PlanetCity.TopHeight(vak)` voor de zichtbare hoogte. `StackHeight` zelf blijft zoals hij is, want het terrein leunt erop.
2. `Beauty.cs`: de score leest `Inhoud`. Een module van 7 vakken telt één keer. Parkmodules zijn groen, pleinmodules een voetgangersplek, kerk en windmolen een blikvanger. Weging uit de overdracht van de webversie (Winkels 0,6, Openbaar 0,4). De rij Afwisseling herzien: nu maken negen verschillende modules en vijf hoogtes die rij vol, hoe de stad er ook uitziet. Voorstel: tellen per soort gebouw, niet per module. Joep kiest.
3. Interfacesessie, in `PlanetBuilder.cs` (regel 1240-1242): het effect vooraf werkt nu door één vak te vervangen. Voor een module van 7 vakken moeten zeven vakken gezet en teruggezet worden. In `PlanetBuilder.Modules.cs` (`ModUpdate`) het effect vooraf ook bij een module tonen.
4. Meten en opschrijven: wat scoort een verse startstad na stap 2? Dat getal is nodig voordat er mijlpalen of een ronde op gebouwd worden.
5. `PlanetLife.Crowd.cs` (`Score`), `Chatter.cs` (`Measure`) en `PlanetLife.Chaos.cs` (`Wall`) lezen `Inhoud` en `TopHeight`. Pleinen en parken blijven doorlaatbaar voor auto's. Dit haalt ook het plotseling leeglopen boven 400 wegvakken weg.
6. Hoogte en naam in beeld: `TopHeight` in `PlanetBlueprint.cs`, `PlanetBuilder.Cursor.cs` (ook de nette naam in `CellName`), `PlanetBuilder.Space.cs` en de regels 478, 574, 757, 823 en 898 in `PlanetBuilder.cs`. Niet regel 844: de loopstart blijft op de grond.
7. `Ambience.cs` en `Sounds.cs`: groen en water uit `Inhoud` en uit het natuurterrein, zodat vogels ook bij een modulepark en in de natuur klinken.
8. Een vlagtest "modules" in `WegVoorbeeld.cs`: modules plaatsen (1 en 7 vakken, gestapeld, op palen, gespiegeld), bewaren en laden, aantal en score vergelijken. Daarna meten met 500, 1000 en 3000 modules. Alleen handelen als het merkbaar is.

**Wat tegelijk kan.** Stap 1 eerst. Daarna vier sessies zonder gedeelde bestanden: `Beauty.cs`; de drie levensbestanden; de interfacebestanden; de twee geluidsbestanden. De test door de modulessessie. Testen om de beurt, `Beauty.cs` eerst.

**Klaar als.** In een verse startstad zijn alle negen rijen van de score gevuld. Een extra huis verhoogt de score en geeft het muntgeluid. Aanwijzen toont de naam van de module. De vakmarkering ligt op het dak. Niemand vraagt om woningen naast een rijhuis. De vlagtest meldt 0 fouten.

### Fase 3: één bouwbalk

**Doel.** De speler heeft één manier om gebouwen te zetten. Pas nu ligt de bediening vast.

**Stappen**

1. Interfacesessie, in `PlanetBuilder.cs` (`Catalog`, `DefaultBar`): woning, kantoor, winkel, dak, blok, toren en molen uit de balk. De categorie Gebouwen opent de modulelade. Park en plein blijven onder Groen: de startstad zet ze zelf nog neer waar geen module past, en naast een schuine weg past geen module. De pipet op een oud blok geeft een nette melding. De tekencode voor blokken blijft, zodat oude steden blijven werken.
2. In het spel nalopen wat je nog kunt zetten op een half wegvak en naast een vijfhoek. De weigertekst van `CanPlaceModule` (`PlanetCity.Modules.cs`) zegt wat daar wel past.
3. `PlanetBuilder.cs` (`OnGUI`): in de modulestand geen resten van het vorige gereedschap (laagkiezer, terreinlabel, hangend scorelabel).
4. Modulessessie, in `PlanetBuilder.Modules.cs`: gum en rechts klikken werken binnen de modulestand, de soortenlijst schuift in plaats van af te breken, een zoekveld bovenaan.
5. Modulessessie: `FaceRot` uit `PlanetStartTown.cs` gebruiken als startdraaiing, zodat een deur vanzelf naar de straat wijst.
6. Modulessessie, in `Raatmaker.cs` (`Save`): een module waarvan al exemplaren staan, kan bij een ander aantal vakken of minder lagen alleen als kopie bewaard worden.
7. Een hulpscript in `Tools`: vergelijkt de eigen modulemap met `Assets/Resources/Modules` en waarschuwt als bewerkte modules alleen in de eigen map staan. De bibliotheek niet opnieuw omzetten.

**Wat tegelijk kan.** `PlanetBuilder.cs` door de interfacesessie. De rest door de modulessessie. Ze schrijven tegelijk en testen na elkaar. Stap 7 raakt Unity niet.

**Klaar als.** Met toets 3 of 7 kom je in dezelfde lade. Er is geen gebouwblok meer te kiezen. Een module staat na één klik met de deur naar de weg. Een oude stad met blokken opent en tekent als voorheen.

### Fase 4: lopen en leven in de modulestad

**Doel.** "Loop door je eigen stad" klopt in de startstad. Het verkeer doet geen dingen die een tester als fout meldt.

**Stappen**

1. Eerst een looptest (vlag "lopen", eigen vlagbestand): door de deur van een rijhuis, een trap op, langs een hek, over een dakrand. Hij schrijft per stap hoogte en botsing weg.
2. Modulessessie, in `ModuleMesh.cs`: dichte botsvlakken voor hekken en leuningen. Dit kan niet alleen in de loper worden opgelost: de loper stapt 0,5 m op en traptreden zijn 0,19 m, dus een lagere muurtoets houdt je ook op de trap tegen.
3. `PlanetWalker.cs`: de grondtoets kiest het hoogste vlak binnen opstaphoogte, zodat je niet door een tree zakt. De onderste muurtoets blijft boven 0,5 m. Na elke aanpassing de looptest, en ook een brug, een tunnel en een talud.
4. Interfacesessie, in `PlanetBuilder.cs` (`ToggleWalk`, regel 844): wijs je een module aan, dan begin je op het dichtstbijzijnde weg- of padvak.
5. `PlanetLife.Physics.cs`: nette auto's remmen voor de wandelaar en voor iemand op hun rijlijn. `MustYield` slaat je eigen auto niet meer over. Daarna de verkeersmeting (`PlanetLife.Diag`) opnieuw draaien op de startstad.
6. Autosessie rondt af wat een tester merkt: uitstappen, niet door de grond vallen, een lege weg bij hard rijden.
7. `Sounds.cs` en `Ambience.cs`: het luisterpunt tussen camera en brandpunt, zodat je in de bouwstand de stad hoort.
8. Alleen als de rest staat: mensen over pleinen en parken van modules. Dat wordt een **eigen functie in `PlanetLife`**. `PlanetCity.IsWalk` niet uitbreiden: die wordt op ongeveer honderd plekken gelezen als "weg of pad", en een plein zou dan zijn sokkel verliezen en een wegprofiel krijgen. Dit punt mag vervallen.

**Wat tegelijk kan.** Vier sessies: looptest met `PlanetWalker.cs`; `ModuleMesh.cs`; verkeer; geluid. De autosessie loopt al. Testen strikt om de beurt.

**Klaar als.** De looptest meldt nul keer "door een hek" en nul keer "gezakt". Je loopt een huis in en een trap op. Een auto stopt voor de wandelaar. In de bouwstand hoor je de stad.

### Fase 5: een doel

**Doel.** Een reden om door te bouwen, en een reden om opnieuw te beginnen. Alleen wat Joep in fase 0 besloten heeft.

**Stappen**

1. Nieuw bestand `PlanetBuilder.Goals.cs` en `CityFiles.cs`: beste score per stad bewaren en tonen op het startscherm en naast de score. Het woord (Rommelig tot Prachtig) als mijlpaal met het muntgeluid.
2. Drie opdrachten tegelijk, afgeleid van de zwakste rijen van de score. Is er één klaar, dan komt er een nieuwe.
3. Als Joep voor een ronde koos: "Uitdaging" als keuze bij Nieuwe stad (`StartScreen.cs`), op de plek van de verborgen Terreintest. Een ronde op tijd, op een willekeurige plek van een natuurplaneet. Niet op de vaste startstad: die heeft bij elk startgetal dezelfde opzet en begint al met een hoge score. De natuurplaneet heeft al een willekeurig startgetal en er zijn twintig startplekken. Aan het eind een uitslag: score, woord, sterkste en zwakste rij, je beste, en de knoppen "Nog een keer", "Doorbouwen" en "Foto". De rondestand gaat mee in het stadsbestand.
4. Joep speelt de ronde tien keer. Let op of de score te bespelen is. Niet leuk: de ronde eruit, stap 1 en 2 blijven.

**Wat tegelijk kan.** Stap 1 en 2 in het nieuwe bestand botsen met niemand. Stap 3 raakt `StartScreen.cs` en `PlanetCity.Save.cs`: afstemmen met de stedensessie en de terreinsessie. Fase 6 kan al naast deze fase lopen.

**Klaar als.** Je ziet je beste score en krijgt een mijlpaal. Met een ronde: je speelt hem uit, ziet de uitslag, en begint met één knop op een andere plek.

### Fase 6: een losse versie die klopt en er verzorgd uitziet

**Doel.** De lijst uit de proefversie is weggewerkt. De zichtbaarste beeldfouten in de bouwstand zijn weg. Geen nieuwe beeldsystemen.

**Stappen**

1. Materialen veiligstellen: een handvol voorbeeldmaterialen in `Assets/Resources` die `Raatstad.MakeMat` als sjabloon gebruikt, en een controle in `Rocketbox.cs` (regel 75) en `PlanetSky.cs` (regel 18-20). Nu worden alle materialen pas in het spel gemaakt en kunnen ze in een losse versie ontbreken.
2. Versienummer op het startscherm (`StartScreen.cs`) en in `melding.json` (`Meldingen.cs`).
3. `DayNight.cs`: lantaarns altijd lichtgevend (regel 33), de sprong van het nachtlicht om middernacht weg, en de zon gerekend vanaf de plek waar je kijkt, volgens het besluit uit fase 0.
4. Eerst de laptopmeting uit fase 1 erbij pakken. Dan pas: schaduwbereik dat meegaat met de camera-afstand, gladdere randen, en mist die meeschaalt (`Weather.cs`, `PlanetSky.cs`). Voor en na meten. Valt het tegen: schaduw uit als keuze en standaard minder mensen en verkeer.
5. Instellingensessie, in `PlanetBuilder.Settings.cs`: totaalvolume en muziekvolume, volledig scherm of venster, beeldlimiet, schaduw aan of uit, muisgevoeligheid.
6. Interfacesessie, in `PlanetBuilder.cs` en `PlanetBuilder.HexMenu.cs`: Esc opent het menu, M werkt ook tijdens lopen, Afsluiten in het menu.
7. Joep: bron en licentie invullen van `klap.mp3`, `grunts.mp3`, `schreeuw.mp3` en `vogels.mp3`, of ze laten vervangen. Het automodel `cybertruck.txt` nakijken.
8. Tweede proefversie maken en de lijst opnieuw aflopen.

**Wat tegelijk kan.** Vijf stromen met eigen bestanden: materialen; dag, nacht en weer; instellingen; versienummer; menu. `PlanetSky.cs` zit in twee stromen: de tweede wacht op de eerste. Het maken van de versie alleen als niemand Unity gebruikt.

**Klaar als.** De versie start op een lege map, toont het versienummer, heeft geen roze of dichte materialen, figuren met haar, schaduw in de bouwstand, verlichte lantaarns, en loopt door als het venster niet vooraan staat.

### Fase 7: spelen zonder uitleg van Joep, en uitdelen

**Doel.** Iemand die het spel nooit zag, bouwt binnen een paar minuten een straat met huizen, loopt erdoorheen en weet hoe hij een melding maakt.

**Stappen**

1. Interfacesessie, in `PlanetBuilder.cs`: het welkomstvenster wordt een reeks stappen die verdwijnen als je ze gedaan hebt: draaien en zoomen, een weg leggen, een huis zetten, de score en de tip, lopen. Daarna één aanwijzing over niveaus (Page Up en Page Down voor een brug), want de startstad laat bruggen zien en U is verborgen.
2. Venster Bediening: modules, de terreinstand Rand, de ruimtekaart en Backspace erbij. Lange regels afbreken of een derde kolom (`Ui.cs`).
3. `Meldingen.cs`: titel "Melding maken", na het bewaren het pad tonen met de knop "Map openen", en zeggen dat de stad en de laatste foutregels meegaan.
4. Een bladzijde voor spelers (wat het is, bediening, F8, waar steden staan) en een stappenlijst "zo maak je een losse versie" in `docs`.
5. Een map met echte steden van deze versie bewaren. Bij elke latere wijziging aan opslag of wegprofiel worden ze geopend en vergeleken. Het wegprofiel wordt niet bewaard maar bij het laden uitgerekend, dus een stad kan veranderen zonder dat het bestand verandert.
6. Proef: iemand zonder uitleg, op een andere pc. Joep kijkt mee en helpt niet. Wat eruit komt gaat als meldingen naar de sessie die het bestand bezit.
7. `docs/stand-van-zaken.md` bijwerken: wat verborgen is en waarom, de leesfunctie, de verouderde regels 21 en 144 over terrein.
8. Uitdelen.

**Wat tegelijk kan.** `PlanetBuilder.cs` door de interfacesessie. `Meldingen.cs`, de teksten en de map met teststeden zijn losse stromen.

**Klaar als.** Een buitenstaander legt zonder hulp een weg, zet vijf modules, loopt erdoor en stuurt een F8-map terug die een sessie kan naspelen.

### Daarna, in deze volgorde

1. Wegen onder het wegprofiel afmaken: U op het maaiveld, niveaus die vast blijven staan, een ondergrens bij water.
2. Stadsgenerator met modules, als vervanger van voorbeeldstad en dorpen. Dan pas modules samenvoegen en Planeet vol.
3. AI-bouwer op modules, daarna het duel. Eerst de regels beslissen.
4. Terrein: weghoogte bewaren, oude opbouw opruimen, snelheid per klik.
5. Blokken echt weg, met omzetten van oude steden.
6. Mensen met doelen en deuren, parkeren, loopbrug, gelaagde stad, spoor.

---

## 4. Niet doen

- **Snelheid op een volle planeet.** Planeet vol is verborgen. De terreinsessie mag doorwerken, maar het houdt de eerste versie niet tegen.
- **De oude terreinopbouw opruimen of weghoogtes bewaren.** Dat raakt de startstad en elke bewaarde stad, en zou steden van testers twee keer omzetten.
- **U op het maaiveld repareren.** Verbergen is één regel, repareren is middelgroot werk.
- **AI-bouwer en generatoren ombouwen naar modules, en het duel.** De AI-bouwer rekent per verdieping en per bloksoort. Dat is een herontwerp.
- **De tekencode van de blokken weghalen.** Oude steden, generatoren, AI-bouwer en tests gebruiken hem nog.
- **`StackHeight` van betekenis veranderen** of **`PlanetCity.IsWalk` uitbreiden.** Het terrein leunt op allebei.
- **De bibliotheek opnieuw omzetten.** Dat overschrijft de met de hand verbeterde kade en tankstationluifel, en de webversie heeft nieuw formaat dat Unity stil verkeerd leest.
- **De bedrijfsnaam wijzigen na het uitdelen**, of terwijl sessies testen.
- **`PlanetBuilder.cs` splitsen, dode code opruimen, pakketten opruimen.** Geen speler merkt het en elke opslag herstart Play voor alle sessies.
- **Een lange uitleg schrijven voordat bouwbalk en doel vastliggen.** Hij veroudert meteen, zoals het venster Bediening nu.
- **Regels van duel, multiplayer en oorlog zelf invullen.** `docs/spelmodi.md` zegt: navragen.
- **Bouwen op gebreken die alleen uit de code zijn afgeleid**, zonder ze eerst in het spel te zien.
- **Mooi-maakwerk dat geen fout herstelt:** bewegend water, texturen op wegen, wolken en maan, natte wegen, lichtvlekken onder lantaarns.
- **Controller, vertalen, toetsen toewijzen, opnieuw doen, hernoemen van steden, stemmen.**

---

## 5. Risico's

1. **Eén Unity voor alle sessies, en één groot bestand.** Elke opgeslagen `.cs` herstart Play. Vier tests delen één vlagbestand en één testslot. `PlanetBuilder.cs` (1486 regels) komt in bijna elke fase terug. *Wat je eraan doet:* de eerste codewijziging is `AutoHerstart.cs` en een eigen vlagbestand per test. Per fase één eigenaar voor `PlanetBuilder.cs`. Testen om de beurt en vooraf melden.

2. **Er is nog nooit een losse versie gemaakt.** Materialen, haar van de figuren en de opstarttijd zijn buiten de editor onbewezen. *Wat je eraan doet:* de proefversie al in fase 1, zonder "Development Build". Reken op een tot drie rondes.

3. **Veel is afgeleid, niet gezien.** Lopen door hekken, U, de startstad met wegprofiel, doffe lantaarns. Lopen kan groot uitvallen omdat het botsvlakken in de modules vraagt. *Wat je eraan doet:* de kijkronde in fase 0, de looptest vóór elke aanpassing, en de terugval "binnen deels" als het uitloopt.

4. **De score verschuift, is te bespelen, en het doel is niet besloten.** Zodra de score modules leest, veranderen alle scores. Met 159 modules loopt Afwisseling vanzelf vol. *Wat je eraan doet:* Joep beslist het doel in fase 0 en speelt een ronde met de hand. Afwisseling wordt herzien in fase 2. De score van de startstad wordt gemeten voordat er mijlpalen komen. Tien rondes spelen voor het uitdelen.

5. **Steden van testers kunnen veranderen of zoekraken.** Het wegprofiel wordt bij elk laden opnieuw uitgerekend, dus elke wijziging van de terreinsessie kan een bewaarde stad een ander wegbeeld geven. De bedrijfsnaam bepaalt de map. *Wat je eraan doet:* versienummer in het bestand en veilig openen in fase 1, de bedrijfsnaam vóór de proefversie, en de map met teststeden uit fase 7 die bij elke wijziging wordt vergeleken.

---

## 6. De eerstvolgende drie sessies

Deze drie raken elkaars bestanden niet en ook niet die van de lopende sessies. Sessie 1 en 2 slaan elk één keer code op. Laat ze dat doen op een moment dat geen andere sessie test, en laat ze het eerst vragen.

### Sessie 1: tests en automatisch herstarten ontkoppelen

> Werk alleen in `Assets/Editor/AutoHerstart.cs`. Lees eerst hoe de testscripts starten (`WegVoorbeeld.cs`, `LevelTest.cs`, `TunnelTest.cs`, `DriveTest.cs`, het naspelen in `Meldingen.cs`): ze lezen een vlagbestand in `persistentDataPath`. Wijzig die testscripts niet.
> Doel: (1) Play wordt niet gestopt en herstart zolang er een test loopt, ook niet als een andere sessie een `.cs` opslaat; de herstart gebeurt daarna. (2) `brugtest.txt` verdwijnt uit `Tick`: geen script leest het nog.
> Schrijf daarna in `docs/stand-van-zaken.md` een korte werkafspraak: een sessie meldt het als ze Unity gebruikt, tests draaien om de beurt.
> Schrijf ook op, als voorstel voor de terreinsessie en de autosessie, hoe elke test een eigen vlagbestand en een eigen testslot krijgt (nu delen ze `niveautest.txt` en slot 9). Voer dat voorstel niet zelf uit.
> Controleer met Unity's compiler via het `.rsp`-bestand voordat je opslaat. Sla één keer op, op een moment dat geen sessie test: vraag het eerst. Lees daarna de Console. Commit, niet pushen.

### Sessie 2: een stad kan niet meer stil verloren gaan

> Werk alleen in `Assets/Scripts/StartScreen.cs` en `Assets/Scripts/CityFiles.cs`. Raak `PlanetCity.Save.cs` niet aan: dat doet de terreinsessie.
> Probleem: lukt het lezen van een stad niet (`CityFiles.Read` geeft null of `PlanetCity.Restore` geeft false), dan gaat `OpenRoutine` door met een lege planeet, roept toch `CityFiles.MarkOpened` aan en laat `savedKnown` op false. Na 180 seconden of bij afsluiten schrijft `SaveCity(true)` de lege planeet over het bestand. `Restore` kan bovendien halverwege stoppen nadat hij de stad al heeft gewist.
> Doel: (1) Bij mislukken: een schone lege planeet in het geheugen, geen huidige stad (`Current` leeg), niet `MarkOpened`, terug naar het startscherm met een duidelijke melding. Bij afsluiten wordt er dan niets overschreven. (2) `CityFiles.WriteText` geeft `File.Replace` een reservekopie (`.bak`) mee, ook als een oude startstad stil door een nieuwe wordt vervangen. (3) Het kopje boven Nieuwe stad zegt "vier zeshoeken": het zijn er vijf.
> Test in Play: maak een kopie van een stadsbestand, beschadig de kopie, open hem, wacht ruim drie minuten, sluit af. Het bestand moet ongewijzigd zijn. Open daarna een gewone stad: die werkt als voorheen.
> Vraag voordat je opslaat of een andere sessie aan het testen is. Lees na het compileren de Console. Commit, niet pushen. Zet een regel in `docs/stand-van-zaken.md`.

### Sessie 3: de kijkronde (geen code)

> Schrijf geen enkel `.cs`-bestand en start geen test. Maak één nieuw bestand: `docs/kijkronde.md`.
> Deel 1, nu: maak een kijklijst voor Joep van dingen die alleen uit de code zijn afgeleid en die hij in een uur spelen kan bevestigen of wegstrepen. Per punt: wat hij doet, wat hij zou moeten zien, en wat er volgens de code misgaat. De punten: de startstad na sluiten en opnieuw openen (verlaagde fietsring, zes bruggen); een weg onder een weg met U op vlak terrein; lopen een huis in, een trap op, tegen een hek en langs een dakrand; lantaarns in de nacht; het weer Mist op de gewone camera; het venster Bediening (lopen lange regels uit?); een rotonde op een helling; boetseren vlak naast een weg; Aanwijzen op een module; de score in de startstad.
> Deel 2, zodra Joep gespeeld heeft: lees zijn F8-meldingen in `persistentDataPath/meldingen/` (`melding.json`, `beeld.png`, `beeld-met-interface.png`). Zet per punt in `docs/kijkronde.md`: bevestigd, valt mee, of anders dan verwacht, met het nummer van de melding. Sluit af met een lijst: welke stappen uit het plan kunnen vervallen en welke worden groter.
> Verander niets in `persistentDataPath`. Commit alleen het nieuwe document.

### Wat de lopende sessies erbij krijgen

Dit zijn geen nieuwe sessies maar korte berichten aan sessies die al lopen:

- **Terrein v2:** `Reprofile` na het maken van een stad; de oude weghoogtes wissen als het laatste wegvak weg is; `ver` in het stadsbestand en `Restore` eerst controleren, dan wissen; `LevelTest.cs` laten draaien met wegprofiel.
- **Modules:** de leesfunctie `PlanetCity.Inhoud` en `PlanetCity.TopHeight` (fase 2, stap 1); een grijze doos voor een module die niet gevonden wordt.
- **Instellingen:** de eigenschap `Dev.On` in de bestaande klasse `Dev` in `DevTools.cs`, en het verbergen van het Ontwikkelaar-menu.
- **Zelf rijden:** een eigen vlagbestand en testslot voor `DriveTest.cs`, volgens het voorstel van sessie 1.
