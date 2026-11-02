using System.Collections.Generic;
using UnityEngine;

// Bouwen op de planeet: de camera hangt boven een punt op de bol en schuift over het oppervlak (WASD),
// draait (rechtermuis) en zoomt (scroll). Klik een vak om het gekozen blok te plaatsen; een weg is ook een blok.
// De bediening volgt de webversie (v100, 15d-bediening.js): linksboven de stad en de schoonheid, rechtsboven tijd en weer,
// geluid en Menu; onderaan de snelbalk 1–9 met "Alle blokken" (Tab), linksonder gum en ongedaan maken, rechtsonder Bouwen/Lopen (L)
// met daarboven de knop ? (alle toetsen). Alle toetsen en de muis staan in Help() op een rij: houd dat venster gelijk met de code.
public class PlanetBuilder : MonoBehaviour
{
    public Camera Cam;
    Vector3 focus = Vector3.up, fwd = Vector3.forward;
    float pitch = 55f, dist = 260f, camH = float.NaN;   // camH: de hoogte waar de camera omheen draait, vloeiend over het terrein
    int hover = -1; bool hasHover;
    readonly List<string> undo = new List<string>();   // de hele stad per stap (PlanetCity.Snapshot): ook verbindingen, bruggen en terrein van voor een niveau
    LineRenderer outline; PlanetWalker walker; bool walking; string msg = ""; float msgT;
    int lastPainted = -1; bool painting;
    int lineStart = -1; List<int> linePlan; readonly List<LineRenderer> planLines = new List<LineRenderer>();   // weg slepen: een rechte lijn van vakken (30°-richtingen)
    Beauty.Result beauty; float beautyT = -99f, fps; int fpsN; float fpsT;
    Vector3 centre; float centreT = -99f; bool hasCentre;   // middelpunt van de stad, voor de pijl
    Vector2 rightAt; int rightCell = -1;
    int camDrag, camBtn; Vector2 camFrom; bool camMoved; float wheelAcc;   // slepen met de muis: 1 schuiven, 2 draaien; knop 0 links, 1 rechts, 2 midden

    // ---------- catalogus en snelbalk ----------
    class Item { public string id, name, tab, block; public Color col; }
    static Item It(string id, string name, string tab, string block = null, Color? col = null) => new Item { id = id, name = name, tab = tab, block = block ?? id, col = col ?? Ui.ToolColor(block ?? id) };
    static readonly string[] Tabs = { "Wegen", "Paden", "Gebouwen", "Groen", "Water", "Terrein" };   // de zes categorieën (toetsen 1–6, de zes kanten van het keuzewiel)
    static readonly Item[] Catalog = {
        It("weg", "Gewone weg", "Wegen"), It("weg:breed", "Brede weg", "Wegen", "weg", new Color(0.38f, 0.4f, 0.43f)), It("weg:laan", "Laan", "Wegen", "weg", new Color(0.33f, 0.5f, 0.32f)), It("rotonde", "Rotonde", "Wegen"),
        It("pad", "Pad", "Paden"),
        It("woning", "Woning", "Gebouwen"), It("kantoor", "Kantoor", "Gebouwen"), It("winkel", "Winkel", "Gebouwen"), It("dak", "Dak", "Gebouwen"), It("blok", "Blok", "Gebouwen"), It("toren", "Toren", "Gebouwen"), It("molen", "Molen", "Gebouwen"),
        It("park", "Park", "Groen"), It("plein", "Plein", "Groen"), It("water", "Gracht", "Water"), It("meer", "Meer", "Water"),
        // kleuren van het penseel zoals TERR_COL in de webversie
        It("t:0", "Ophogen", "Terrein", "terrein", new Color(0.95f, 0.70f, 0.20f)), It("t:1", "Afgraven", "Terrein", "terrein", new Color(0.35f, 0.66f, 0.90f)), It("t:2", "Egaliseren", "Terrein", "terrein", new Color(0.92f, 0.93f, 0.92f)),
        It("t:3", "Gladstrijken", "Terrein", "terrein", new Color(0.56f, 0.82f, 0.42f)), It("t:4", "Helling", "Terrein", "terrein", new Color(0.94f, 0.54f, 0.36f)), It("t:5", "Rand", "Terrein", "terrein", new Color(0.80f, 0.62f, 0.95f)), It("kuil", "Uitgraving", "Terrein"),
    };
    static string IconOf(Item it) => it.id.StartsWith("t:") ? TerrIcons[it.id[2] - '0'] : null;   // terrein: het pictogram op het blokje
    // wat het blok doet, voor de uitleg bij aanwijzen (snelbalk en catalogus)
    static string Desc(string id)
    {
        switch (id)
        {
            case "weg": return "Slepen voor een rechte weg met stoepen";
            case "weg:breed": return "Bredere weg voor drukke routes";
            case "weg:laan": return "Weg met bomen langs de kant";
            case "rotonde": return "Klik op een vak: rotonde over 7 vakken; wegen op de ring sluiten aan";
            case "pad": return "Voetpad, of met B een fiets- en voetpad";
            case "woning": return "Om in te wonen, stapelbaar";
            case "kantoor": return "Werkplekken, stapelbaar";
            case "winkel": return "Winkel met luifel en etalage";
            case "dak": return "Een dak bovenop je stapel";
            case "blok": return "Bouwblok, stapelbaar";
            case "toren": return "Hoog; daarop kan niets meer";
            case "molen": return "Molen met draaiende wieken";
            case "park": return "Gras en bomen";
            case "plein": return "Bestraat plein";
            case "water": return "Gracht met kademuren, water op het zeeniveau; een weg erover wordt een brug";
            case "meer": return "Meer: een kom tot onder het zeeniveau, met zand en riet aan de oever; een weg erover wordt een dam";
            case "kuil": return "Uitgraving; een weg erover wordt een brug";
            case "t:0": return "Grond omhoog (Shift: omlaag)";
            case "t:1": return "Grond omlaag (Shift: omhoog)";
            case "t:2": return "Op de hoogte van het eerste vak";
            case "t:3": return "Hobbels glad strijken";
            case "t:4": return "Sleep van hier naar het andere eind";
            case "t:5": return "Een rand omhoog of omlaag";
        }
        return null;
    }
    static Item Find(string id) { foreach (var it in Catalog) if (it.id == id) return it; return Catalog[0]; }
    // per categorie het laatst gekozen blok; slot is de categorie die nu gekozen is
    static readonly string[] DefaultBar = { "weg", "pad", "woning", "park", "water", "t:0" };
    readonly string[] bar = (string[])DefaultBar.Clone();
    int slot; bool eraser; float nameT = -99f;
    static int TabOf(string id) => System.Array.IndexOf(Tabs, Find(id).tab);
    static int CountIn(int c) { int n = 0; foreach (var it in Catalog) if (it.tab == Tabs[c]) n++; return n; }
    Item Cur => Find(bar[slot]);
    string CurBlock => eraser ? "gum" : Cur.block;
    bool TerrTool => !eraser && Cur.block == "terrein";
    bool LevelTool => !eraser && (CurBlock == "pad" || CurBlock == "weg");   // weg of pad: Page Up/Down kiest het niveau
    bool PathDrag => lineStart >= 0 && Inp.LeftHeld && LevelTool;   // een weg of pad slepen: ook + en - en het wiel kiezen het niveau (Q/E blijven draaien)

    // open panelen: catalogus, tijd/weer, schoonheid, menu, bediening, welkom
    bool catOpen, todOpen, scoreOpen, menuOpen, helpOpen, welcome;
    bool Modal => menuOpen || helpOpen || welcome || scoreOpen;
    bool OverBar => Ui.MouseOverUI || Modal;

    void LoadBar()
    {
        var s = PlayerPrefs.GetString("raatstad-categorie", ""); var parts = s.Split(',');
        if (parts.Length == bar.Length) for (int k = 0; k < bar.Length; k++) { foreach (var it in Catalog) if (it.id == parts[k] && TabOf(it.id) == k) { bar[k] = parts[k]; break; } }
    }
    void SaveBar() { PlayerPrefs.SetString("raatstad-categorie", string.Join(",", bar)); }
    void ApplyItem()
    {
        var it = Cur;
        if (it.id == "weg") PlanetCity.DragStyle = 0; else if (it.id == "weg:breed") PlanetCity.DragStyle = 1; else if (it.id == "weg:laan") PlanetCity.DragStyle = 2;
        if (it.block == "terrein") terrMode = it.id[2] - '0';
    }
    void SelectSlot(int k) { slot = k; eraser = false; ApplyItem(); nameT = Time.time + 1.6f; }
    // een blok kiezen (lade of wiel): de categorie onthoudt het, er wordt niets overschreven
    void Pick(string id)
    {
        int c = TabOf(id); if (c < 0) return;
        bar[c] = id; SaveBar(); SelectSlot(c); catOpen = false;
    }
    // pipet: het bovenste blok van een vak (een weg met zijn wegtype, een pad als voet- of fietspad) wordt het gekozen blok
    Vector2 midAt; int midCell = -1;
    void Pipet(int i)
    {
        string top = null;
        if (PlanetCity.Cells.TryGetValue(i, out var l)) for (int k = l.Count - 1; k >= 0; k--) if (l[k] != "wegdeel") { top = l[k]; break; }
        if (top == null && PlanetCity.IsHalf(i)) top = "weg";   // een half vak zonder bebouwing: de schuine weg erover
        if (top == "weg") top = StyleIds[Mathf.Clamp(PlanetCity.StyleOf(i), 0, StyleIds.Length - 1)];
        if (top == "pad" && PlanetCity.IsBike(i) != PlanetCity.DragBike) PlanetCity.DragBike = !PlanetCity.DragBike;
        int c = top != null ? TabOf(top) : -1;
        if (c < 0 || Find(top).id != top) { Say("Pipet: hier staat niets om over te nemen"); return; }
        Pick(top); Say("Pipet: " + Cur.name + (top == "pad" && PlanetCity.DragBike ? " (fiets- en voetpad)" : ""));
    }
    // dezelfde cijfertoets nog eens: het volgende blok in die categorie
    void NextInTab(int c)
    {
        var ids = new List<string>(); foreach (var it in Catalog) if (it.tab == Tabs[c]) ids.Add(it.id);
        Pick(ids[(ids.IndexOf(bar[c]) + 1) % ids.Count]); Say(Tabs[c] + ": " + Cur.name);
    }
    // B en T wisselen de soort binnen het vak: het vak krijgt de nieuwe soort
    void SetVariant(string id) { bar[slot] = id; SaveBar(); ApplyItem(); nameT = Time.time + 1.6f; }

    void Start()
    {
        int s0 = Planet.North; focus = Planet.Cells[s0].dir; fwd = Planet.Cells[s0].north; dist = 230f; pitch = 48f;   // je begint altijd op de noordpool (een startplek)
        if (PlanetCity.Cells.Count == 0) { PlanetCity.StartTown(s0); PlanetCity.Rebuild(); PlanetCity.Save(); }   // lege stad: de standaardstad rond de noordpool
        outline = new GameObject("Hover").AddComponent<LineRenderer>(); outline.loop = true; outline.positionCount = 6; outline.widthMultiplier = 0.35f; outline.material = Raatstad.MatOutline; outline.useWorldSpace = true;
        walker = new GameObject("Wandelaar").AddComponent<PlanetWalker>(); walker.Cam = Cam; walker.gameObject.SetActive(false);
        LoadBar(); ApplyItem();
        welcome = PlayerPrefs.GetInt("raatstad-welkom-uit", 0) == 0;
    }

    void Say(string s) { msg = s; msgT = Time.time + 4f; if (flying) { flyNote = s; flyBadgeT = Time.time; } }   // in de cameramodus op het label
    void Push() { undo.Add(PlanetCity.Snapshot()); if (undo.Count > 100) undo.RemoveAt(0); }
    void Undo()
    {
        if (undo.Count == 0) return;
        var u = undo[undo.Count - 1]; undo.RemoveAt(undo.Count - 1);
        PlanetCity.Restore(u); PlanetCity.TouchAll(); Changed(); Say("Ongedaan gemaakt");
    }
    // na een wijziging: het deel onder de muis meteen opnieuw tekenen, de rest (buurdelen, raster, opslaan) in de volgende beelden
    bool rebuilding;
    void Changed() { PlanetCity.UserEdit(); beautyVer++; PlanetCity.Save(); PlanetCity.RebuildSoon(hasHover ? hover : -1); rebuilding = true; RebuildTick(0f); beautyT = -99f; }   // 0: in dit beeld alleen het deel van de klik
    void RebuildTick(float budget) { if (PlanetCity.RebuildSome(budget)) { PlanetCity.RebuildGrid(); rebuilding = false; } }
    void ToggleSound() { Babble.Muted = !Babble.Muted; Ambience.Muted = Babble.Muted; Say(Babble.Muted ? "Geluid uit" : "Geluid aan"); }
    void ToggleHelp()
    {
        helpOpen = !helpOpen; if (!helpOpen) return;
        menuOpen = scoreOpen = welcome = false;
        if (walking) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }   // tijdens het lopen: de muis los om het venster te kunnen sluiten
    }
    void ToggleGrid() { PlanetCity.GridOn = !PlanetCity.GridOn; PlanetCity.ShowGrid(PlanetCity.GridOn && !walking); Say(PlanetCity.GridOn ? "Raster aan (G)" : "Raster uit (G)"); }
    void GoCentre() { centreT = -99f; CityCentre(); if (hasCentre) { focus = centre; Say("Naar het midden van de stad (H)"); } }
    // naar de andere kant van de bol: het punt recht tegenover waar je nu kijkt (K, nog een keer: terug)
    void GoOpposite()
    {
        focus = -focus.normalized; fwd -= focus * Vector3.Dot(fwd, focus); if (fwd.sqrMagnitude < 1e-6f) fwd = Planet.Cells[Planet.Nearest(focus)].north; fwd.Normalize();
        PlanetCity.LatLon(focus, out float lat, out float lon);
        Say("Naar de andere kant van de bol: " + Mathf.Abs(lat).ToString("0") + "° " + (lat >= 0f ? "N" : "Z") + " " + Mathf.Abs(lon).ToString("0") + "° " + (lon >= 0f ? "O" : "W") + " (K: terug)");
    }
    void GoPole() { bool north = Vector3.Dot(focus, Planet.Cells[Planet.North].dir) < 0.999f; focus = Planet.Cells[north ? Planet.North : Planet.South].dir; Say(north ? "Naar de noordpool (N, nog een keer: zuidpool)" : "Naar de zuidpool (N)"); }
    void Sample()
    {
        Push(); string why = PlanetCity.Sample(Planet.Nearest(focus));
        if (why != null) { undo.RemoveAt(undo.Count - 1); Say(why); return; }   // geen ruimte: niets veranderd
        Changed(); Say("Voorbeeldstad geplaatst (Ctrl+Z haalt hem weg)");
    }
    // de hele bol vol stadjes en platteland (vervangt de stad die open is; Ctrl+Z zet hem terug)
    // in stappen achter het laadscherm (de gele zeshoeken), met de voortgang erop; de bediening staat zolang stil
    string loading; float loadFrac; HexLoadingScreen loader;
    void FillPlanet() { if (loading == null) StartCoroutine(FillRoutine()); }
    System.Collections.IEnumerator FillRoutine()
    {
        loading = "Planeet vullen"; loadFrac = 0f;
        if (loader == null) loader = new GameObject("Laadscherm").AddComponent<HexLoadingScreen>(); else loader.Show();
        yield return null; yield return null;   // eerst het laadscherm in beeld
        Push(); var sw = System.Diagnostics.Stopwatch.StartNew();
        yield return PlanetCity.FillSteps((f, what) => { loadFrac = f * 0.4f; loading = what; });
        loading = "Tekenen"; yield return PlanetCity.RebuildSteps(f => loadFrac = 0.4f + f * 0.6f);
        PlanetCity.RebuildGrid(); PlanetCity.Save(); beautyT = -99f;
        loader.Hide(); loading = null;
        Say("Planeet vol: " + PlanetCity.FillTowns.Count + " stadjes met platteland ertussen (" + (sw.ElapsedMilliseconds / 1000f).ToString("0") + " s; Ctrl+Z haalt alles weg)");
    }
    void LoadingGui(float W, float H)
    {
        var r = new Rect((W - 420f) * 0.5f, H * 0.5f + 90f, 420f, 74f); Ui.Panel(r, true, 16f);
        Ui.Label(new Rect(r.x + 18f, r.y + 10f, r.width - 36f, 26f), loading + "…", Ui.T.Bold);
        Ui.Label(new Rect(r.x + 18f, r.y + 10f, r.width - 36f, 26f), Mathf.RoundToInt(loadFrac * 100f) + "%", Ui.T.Dim, null, TextAnchor.MiddleRight);
        var bar = new Rect(r.x + 18f, r.y + 46f, r.width - 36f, 10f); Ui.Tex(bar, Texture2D.grayTexture); Ui.Tex(new Rect(bar.x, bar.y, bar.width * loadFrac, bar.height), Texture2D.whiteTexture);
    }
    // opnieuw beginnen: de planeet leeg en de startstad op de dichtstbijzijnde startplek (vlag), en de camera erheen
    void StartTown()
    {
        Push(); PlanetCity.ClearAll(); int st = PlanetCity.FreeStart(focus); PlanetCity.StartTown(st); Changed();   // opnieuw beginnen: alleen de startstad op de planeet
        focus = Planet.Cells[st].dir; fwd = Planet.Cells[st].north; dist = 230f; pitch = 48f; Say("Opnieuw begonnen met de startstad rond de vlag (Ctrl+Z zet de vorige planeet terug)");
    }
    void Empty()
    {
        Push(); PlanetCity.ClearAll(); Changed();
        focus = Planet.Cells[Planet.North].dir; fwd = Planet.Cells[Planet.North].north; dist = 230f; pitch = 48f;   // terug naar het startpunt: de noordpool
        Say("Lege bouwgrond, bij de noordpool (Ctrl+Z haalt je stad terug)");
    }
    // het bovenste paneel dicht (Escape)
    bool CloseTop()
    {
        if (welcome) { welcome = false; return true; } if (helpOpen) { helpOpen = false; return true; } if (menuOpen) { menuOpen = false; return true; }
        if (scoreOpen) { scoreOpen = false; return true; } if (catOpen) { catOpen = false; return true; } if (todOpen) { todOpen = false; return true; }
        if (eraser) { eraser = false; return true; }
        return false;
    }

    void Update()
    {
        if (loading != null) return;   // de planeet wordt gevuld: geen bediening
        if (rebuilding) RebuildTick(25f);
        // overal, ook tijdens het lopen en in de cameramodus: P zet de tijd stil, O ander weer (Shift+O het vorige)
        if (Inp.Down(K.P)) { DayNight.ToggleFreeze(); Say(DayNight.Speed > 0f ? "De tijd loopt weer (P)" : "Pauze: de tijd staat stil (P)"); }
        if (Inp.Down(K.O) && Weather.I != null) Say("Weer: " + Weather.I.Cycle(Inp.Held(K.Shift) ? -1 : 1) + " (O, Shift+O terug)");
        if (flying) { FlyUpdate(); return; }   // cameramodus: alleen vliegen, kijken en een foto
        if (Inp.Down(K.L)) ToggleWalk();
        if (Inp.Down(K.Slash)) ToggleHelp();
        if (walking) { if (Inp.Down(K.Esc)) helpOpen = false; return; }
        if (Inp.Down(K.Esc)) CloseTop();
        if (Inp.Down(K.M)) { bool open = !menuOpen; helpOpen = scoreOpen = welcome = todOpen = catOpen = false; menuOpen = open; }
        if (Inp.Down(K.Space) && !Modal) { FlyStart(); return; }
        CameraControl();
        if (Modal) { outline.enabled = false; ShowBrush(-1); ShowGhost(-1); ShowVerts(-1); return; }   // een venster staat open: niets in de stad

        // Tab vasthouden: het keuzewiel bij de muis; kant op bewegen en loslaten kiest. Kort tikken: de lade van de categorie open of dicht
        if (Inp.Down(K.Tab)) { wheelOpen = true; wheelAt = Ui.Mouse; wheelT = Time.unscaledTime; }
        if (wheelOpen)
        {
            if (!Inp.Held(K.Tab) || Inp.LeftDown)
            {
                wheelOpen = false; int c = WheelSector();
                if (c >= 0) { SelectSlot(c); catOpen = false; Say(Tabs[c] + ": " + Cur.name); }
                else if (Time.unscaledTime - wheelT < 0.35f) catOpen = !catOpen;
            }
            outline.enabled = false; ShowBrush(-1); ShowGhost(-1); ShowVerts(-1); return;   // zolang het wiel open is: niet bouwen
        }
        K[] dk = { K.D1, K.D2, K.D3, K.D4, K.D5, K.D6 };
        for (int i = 0; i < dk.Length; i++) if (Inp.Down(dk[i])) { if (i == slot && !eraser) NextInTab(i); else { SelectSlot(i); Say(Tabs[i] + ": " + Cur.name); } }
        if (Inp.Down(K.Del) || Inp.Down(K.Back)) { eraser = !eraser; Say(eraser ? "Gum: klik of sleep over vakken om ze leeg te maken (Delete)" : "Gum uit"); }
        if (Inp.Down(K.G)) ToggleGrid();
        if (LevelTool)
        {
            int dl = (Inp.Down(K.PgUp) ? 1 : 0) - (Inp.Down(K.PgDn) ? 1 : 0);
            if (PathDrag) { dl += (Inp.Down(K.Plus) ? 1 : 0) - (Inp.Down(K.Minus) ? 1 : 0); wheelAcc += Inp.Scroll; if (Mathf.Abs(wheelAcc) >= 0.9f) { dl += wheelAcc > 0f ? 1 : -1; wheelAcc = 0f; } }
            if (dl != 0) StepLevel(dl);
        }
        if (CurBlock == "pad" && Inp.Down(K.B)) ToggleBike();
        if (CurBlock == "weg") { if (Inp.Down(K.B)) CycleStyle(); if (Inp.Down(K.I)) ToggleOneWay(); if (Inp.Down(K.U)) ToggleUnder(); }
        if (CurBlock == "pad" && Inp.Down(K.U)) ToggleUnder();   // ook een pad kan onder kruisende wegen door
        if (Inp.Down(K.H)) GoCentre();
        if (Inp.Down(K.N)) GoPole();
        if (Inp.Down(K.K)) GoOpposite();
        if (Inp.Down(K.Z) && Inp.Held(K.Ctrl) && !sculpting) Undo();
        if (TerrTool)
        {
            if (Inp.Down(K.T)) { terrMode = (terrMode + 1) % TerrModes.Length; SetVariant("t:" + terrMode); Say("Terrein: " + TerrModes[terrMode] + " (T) · " + TerrHints[terrMode]); }
            if (Inp.Down(K.LBr) && brush > 0) { brush--; Say("Penseel: " + BrushSize(brush) + " vak" + (brush > 0 ? "ken" : "") + " ([ en ])"); }
            if (Inp.Down(K.RBr) && brush < 3) { brush++; Say("Penseel: " + BrushSize(brush) + " vakken ([ en ])"); }
        }
        // Shift+wiel: het volgende of vorige vak van de snelbalk (zoals de webversie)
        if (Inp.Held(K.Shift) && !Ui.MouseOverUI && !PathDrag) { wheelAcc += Inp.Scroll; if (Mathf.Abs(wheelAcc) >= 0.9f) { SelectSlot((slot + (wheelAcc < 0f ? 1 : Tabs.Length - 1)) % Tabs.Length); wheelAcc = 0f; } } else if (!PathDrag) wheelAcc = 0f;
        // een klik in de stad sluit de catalogus en het tijdpaneel (en bouwt dan niet); Shift- of Alt-slepen beweegt de camera
        bool leftDown = Inp.LeftDown && camDrag == 0;
        if (leftDown && !Ui.MouseOverUI && (catOpen || todOpen)) { catOpen = todOpen = false; leftDown = false; }

        // pipet: middelste knop klikken zonder te slepen neemt het blok van dat vak over (slepen blijft draaien)
        if (midCell >= 0 && !Inp.MiddleHeld) { if (Vector2.Distance(Inp.Pos, midAt) < 6f) Pipet(midCell); midCell = -1; }

        // aanwijzen: het vak onder de muis
        hasHover = false;
        if (!OverBar && Physics.Raycast(Cam.ScreenPointToRay(Inp.Pos), out RaycastHit hit, 5000f, Layers.Wijzen, QueryTriggerInteraction.Ignore))
        {
            hover = Planet.Nearest(hit.point - hit.normal * 0.3f); hasHover = true; hoverPoint = hit.point;   // net in het vlak dat je raakt: de zijkant van een gebouw hoort bij dat gebouw, niet bij de buur
            ShowOutline(hover);
            if (ShowPreview) BeautyPreview(); else prevOk = false;
            if (Inp.Down(K.X)) { Push(); if (PlanetCity.RemoveTop(hover)) Changed(); }
            if (Inp.RightDown) { rightAt = Inp.Pos; rightCell = hover; }
            if (Inp.MiddleDown) { midAt = Inp.Pos; midCell = hover; }
            if (Inp.RightUp && rightCell >= 0 && Vector2.Distance(Inp.Pos, rightAt) < 6f) { Push(); bool half = PlanetCity.IsHalf(hover); if (PlanetCity.Clear(hover)) { Changed(); Say(half && !PlanetCity.IsHalf(hover) ? "Schuine weg weggehaald" : "Vak leeggemaakt"); } rightCell = -1; }
            bool roadTool = !eraser && (CurBlock == "weg" || CurBlock == "pad");   // met de gum wis je altijd, wat er ook gekozen is
            if (TerrTool && !eraser && terrMode == 5) { ShowBrush(-1); EdgeTool(); }   // een rand omhoog of omlaag
            else if (TerrTool && !eraser) { ShowBrush(hover); if (leftDown) { Push(); SculptStart(hover); } }
            else if (leftDown) { Push(); painting = !roadTool; lastPainted = -1; lineStart = roadTool ? hover : -1; linePlan = null; }
            if (lineStart >= 0 && Inp.LeftHeld) { linePlan = hover == lineStart ? null : PlanetCity.LinePlan(lineStart, hover); ShowPlan(linePlan); }
            if (painting && Inp.LeftHeld && hover != lastPainted)
            {
                int prev = lastPainted; lastPainted = hover;
                if (eraser) { if (PlanetCity.ClearFrom(hover, prev)) Changed(); }   // de gum vanaf de weg onder een brug haalt die onderdoorgang weg, niet de brug
                else { string err = PlanetCity.Place(hover, CurBlock); if (err != null) Say(err); else Changed(); }
            }
        }
        if (leftDown && lineStart >= 0 && LevelTool && PlanetCity.DragLevel != 0) StepLevel(0);   // herinnering: dit komt hoger of lager
        if (!hasHover || !TerrTool) ShowBrush(-1);
        ShowVerts(hasHover && TerrTool && !eraser ? hover : -1);
        if (sculpting) { if (Inp.LeftHeld) SculptUpdate(Time.deltaTime); else SculptEnd(); }
        if (Inp.LeftUp)
        {
            if (sculpting) SculptEnd();
            if (lineStart >= 0)
            {
                string t = CurBlock; int ok = 0; string err = null;
                bool du = PlanetCity.DragUnder; int lv = PlanetCity.DragLevel; bool level = t == "pad" || t == "weg";
                if (t == "weg" && lv != 0) PlanetCity.DragUnder = lv <= -2;   // een niveau lager: onder kruisende wegen door; hoger: eroverheen (SetLevel); een half niveau: kruispunt
                if (linePlan != null) ok = PlanetCity.PlaceLine(linePlan, t);
                else if (level && lv != 0 && (err = PlanetCity.LevelClickProblem(lineStart, lv, t)) != null) ok = 0;   // een los vak op een niveau: niet op of naast een weg op een andere hoogte
                else { err = PlanetCity.Place(lineStart, t); if (err == null) ok = 1; }
                PlanetCity.DragUnder = du;
                if (ok > 0 && level) PlanetCity.SetLevel(linePlan ?? new List<int> { lineStart }, lv, t);
                if (ok > 0 && level && lv == 0 && linePlan != null) PlanetCity.DipUnder(linePlan);   // ongelijkvloers op het maaiveld: alleen bij de kruising omlaag
                if (ok > 0) Changed(); else if (err != null) Say(err);
                if (linePlan != null && ok > 0) Say(PlanetCity.LastNote ?? ok + " vakken " + Cur.name.ToLower());   // een melding over de lijn gaat voor
                lineStart = -1; linePlan = null; ShowPlan(null);
            }
            painting = false;
        }
        outline.enabled = hasHover; ShowGhost(hasHover ? hover : -1);
    }

    // ---------- terrein boetseren (zoals 14b-boetseren.js in de webversie) ----------
    // Vijf standen, allemaal met de linkermuisknop ingedrukt houden en slepen; Shift doet het omgekeerde:
    //  Ophogen / Afgraven  het terrein onder het penseel gaat omhoog of omlaag zolang je drukt
    //  Egaliseren          alles wat je raakt gaat naar de gemiddelde hoogte onder het penseel waar je begon (Shift: de hoogte van dat vak)
    //  Gladstrijken        scherpe overgangen worden zacht (elk vak schuift naar het gemiddelde van zijn buren)
    //  Helling             sleep van A naar B: een gelijkmatige helling van de hoogte bij A naar die bij B
    // Het penseel is 1, 7, 19 of 37 vakken ([ en ]); het midden werkt het hardst. Vakken waar iets op staat blijven liggen.
    static readonly string[] TerrModes = { "Ophogen", "Afgraven", "Egaliseren", "Gladstrijken", "Helling", "Rand" };
    static readonly string[] TerrIcons = { "ophogen", "afgraven", "egaliseren", "gladstrijken", "helling", "rand" };
    static readonly string[] TerrHints = { "Shift: omlaag", "Shift: omhoog", "Shift: naar de hoogte van het startvak", "slepen om glad te strijken", "sleep van hier naar het andere eind", "rand of doorkruislijn (door het midden): klik 0,5 m omhoog · Shift+klik omlaag · Page Up/Down 0,25 m" };
    // stand Rand: de rand van het vak onder de muis die het dichtst bij de muis ligt (twee hoekpunten), omhoog of omlaag
    int edgeK = -1; bool edgeDiag; Vector3 hoverPoint;
    // wat ligt het dichtst bij de muis: een rand (hoekpunt k naar k+1) of, in een zeshoek, een doorkruislijn (hoekpunt k door het midden
    // naar k+3)? In de vlakke coordinaten van het vak, de afstand tot elk lijnstuk.
    void NearestLine(int c, Vector3 p, out int k, out bool diag)
    {
        var cell = Planet.Cells[c]; var L = cell.local; int m = L.Length; Vector3 v = p.normalized * Planet.R - cell.dir * Planet.R;
        Vector2 q = new Vector2(Vector3.Dot(v, cell.east), Vector3.Dot(v, cell.north)); float bd = float.MaxValue; k = 0; diag = false;
        float Seg(Vector2 a, Vector2 b) { Vector2 ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude); return (q - (a + ab * t)).sqrMagnitude; }
        for (int j = 0; j < m; j++) { float dd = Seg(L[j], L[(j + 1) % m]); if (dd < bd) { bd = dd; k = j; diag = false; } }
        if (m == 6) for (int j = 0; j < 3; j++) { float dd = Seg(L[j], L[j + 3]); if (dd < bd) { bd = dd; k = j; diag = true; } }
    }
    void EdgeTool()
    {
        NearestLine(hover, hoverPoint, out edgeK, out edgeDiag); var cs = Planet.Cells[hover].corners; int m = cs.Length, k2 = edgeDiag ? (edgeK + 3) % m : (edgeK + 1) % m;
        Vector3 Pk(int k) => cs[k] * (Planet.R + PlanetCity.OwnCorner(hover, k) + 0.4f);
        outline.enabled = true; outline.loop = false; outline.widthMultiplier = 0.6f;
        if (edgeDiag) { outline.positionCount = 3; outline.SetPosition(0, Pk(edgeK)); outline.SetPosition(1, Planet.Cells[hover].dir * (Planet.R + PlanetCity.G(hover) + PlanetCity.CenterOfs1(hover) + 0.4f)); outline.SetPosition(2, Pk(k2)); }
        else { outline.positionCount = 2; outline.SetPosition(0, Pk(edgeK)); outline.SetPosition(1, Pk(k2)); }
        float d = 0f;
        if (Inp.LeftDown && camDrag == 0 && !Ui.MouseOverUI) d = Inp.Held(K.Shift) ? -0.5f : 0.5f;
        if (Inp.Down(K.PgUp)) d = 0.25f; if (Inp.Down(K.PgDn)) d = -0.25f;
        if (d != 0f)
        {
            Push(); if (edgeDiag) PlanetCity.NudgeDiagonal(hover, edgeK, d); else PlanetCity.NudgeEdge(hover, edgeK, d); Changed();
            Say((edgeDiag ? "Doorkruislijn " : "Rand ") + (d > 0f ? "omhoog" : "omlaag") + ": nu " + PlanetCity.CornerOfs1(hover, edgeK).ToString("+0.00;-0.00;0") + " m en " + PlanetCity.CornerOfs1(hover, k2).ToString("+0.00;-0.00;0") + " m t.o.v. het maaiveld" + (edgeDiag ? ", het midden " + PlanetCity.CenterOfs1(hover).ToString("+0.00;-0.00;0") + " m" : ""));
        }
    }
    int terrMode, brush = 1; bool sculpting; int sA = -1, sLast = -1; float sHa, sHv, sAcc, sCost; bool sInv;
    readonly Dictionary<int, float> sPrior = new Dictionary<int, float>(); readonly HashSet<int> sTouched = new HashSet<int>();
    static int BrushSize(int b) => 1 + 3 * b * (b + 1);
    static float BrushW(int d, int b) => d == 0 ? 1f : 1f - 0.55f * d / (b + 0.5f);
    // de vakken binnen b stappen van c, met hun afstand
    static List<(int c, int d)> Brush(int c0, int b)
    {
        var res = new List<(int, int)> { (c0, 0) }; var seen = new HashSet<int> { c0 }; int from = 0;
        for (int d = 1; d <= b; d++) { int to = res.Count; for (int k = from; k < to; k++) foreach (int j in Planet.Cells[res[k].Item1].nb) if (seen.Add(j)) res.Add((j, d)); from = to; }
        return res;
    }
    float NbAvg(int c) { float s = 0f; var nb = Planet.Cells[c].nb; foreach (int j in nb) s += PlanetCity.G(j); return s / nb.Length; }

    void SculptStart(int c)
    {
        sculpting = true; sA = c; sLast = c; sInv = Inp.Held(K.Shift); sHa = PlanetCity.G(c); sHv = sHa; sAcc = 0f; sPrior.Clear(); sTouched.Clear();
        if (terrMode == 2 && !sInv) { float s = 0f; var bc = Brush(c, brush); foreach (var x in bc) s += PlanetCity.G(x.c); sHv = Mathf.Round(s / bc.Count * 2f) / 2f; }   // egaliseren: het gemiddelde, op een halve meter
        if (terrMode <= 1) SculptStep(0.12f);   // een klik is ook al een stapje
        if (terrMode == 4) SlopeTo(c);
    }
    void SculptUpdate(float dt)
    {
        if (hasHover) sLast = hover;
        if (terrMode == 4) { if (hasHover) SlopeTo(hover); return; }
        sAcc += dt; if (sAcc < Mathf.Max(0.05f, sCost * 3f)) return;   // 20 keer per seconde is genoeg; kost een stap veel, dan minder vaak
        float t0 = Time.realtimeSinceStartup; SculptStep(Mathf.Min(0.15f, sAcc)); sAcc = 0f; sCost = sCost * 0.6f + (Time.realtimeSinceStartup - t0) * 0.4f;
    }
    void SculptStep(float dt)
    {
        bool raise = (terrMode == 0) != sInv, any = false;
        foreach (var x in Brush(sLast, brush))
        {
            if (PlanetCity.Plateau(x.c)) continue;
            float w = BrushW(x.d, brush), h = PlanetCity.G(x.c), nh = h;
            if (terrMode <= 1) nh = h + (raise ? 1f : -1f) * 6f * w * dt;
            else if (terrMode == 2) { float dv = sHv - h; nh = h + Mathf.Sign(dv) * Mathf.Min(Mathf.Abs(dv), 12f * w * dt); }
            else if (terrMode == 3) nh = h + (NbAvg(x.c) - h) * Mathf.Min(1f, 4f * w * dt);
            if (PlanetCity.SetTerr(x.c, nh)) { sTouched.Add(x.c); any = true; }
        }
        if (any) PlanetCity.Rebuild();
    }
    // helling: alles terugzetten en opnieuw een rechte helling van A naar het vak onder de muis
    void SlopeTo(int b)
    {
        foreach (var kv in sPrior) PlanetCity.SetTerr(kv.Key, kv.Value);
        sPrior.Clear();
        Vector3 A = Planet.Cells[sA].dir, B = Planet.Cells[b].dir, AB = B - A; float hb = PlanetCity.G(b), L2 = AB.sqrMagnitude;
        if (b != sA && L2 > 1e-10f)
        {
            var line = PlanetCity.LinePlan(sA, b) ?? new List<int> { sA, b }; var set = new Dictionary<int, float>();
            foreach (int lc in line) foreach (var x in Brush(lc, brush))
            {
                if (set.ContainsKey(x.c) || PlanetCity.Plateau(x.c)) continue;
                float t = Mathf.Clamp01(Vector3.Dot(Planet.Cells[x.c].dir - A, AB) / L2); set[x.c] = sHa + (hb - sHa) * t;
            }
            foreach (var kv in set) { sPrior[kv.Key] = PlanetCity.G(kv.Key); PlanetCity.SetTerr(kv.Key, kv.Value); sTouched.Add(kv.Key); }
        }
        PlanetCity.Rebuild();
    }
    void SculptEnd()
    {
        sculpting = false; sPrior.Clear();
        if (sTouched.Count == 0) { if (undo.Count > 0) undo.RemoveAt(undo.Count - 1); return; }   // niets veranderd: ook niets ongedaan te maken
        PlanetCity.RebuildGrid(); Changed();
        string name = terrMode == 0 ? (sInv ? "Afgegraven" : "Opgehoogd") : terrMode == 1 ? (sInv ? "Opgehoogd" : "Afgegraven") : terrMode == 2 ? "Geëgaliseerd" : terrMode == 3 ? "Gladgestreken" : "Helling gemaakt";
        Say(name + ": " + sTouched.Count + (sTouched.Count == 1 ? " vak" : " vakken"));
    }
    // penseel: de randen van de vakken onder het penseel, op hun eigen hoogte
    int brushShown = -2, brushSizeShown = -1;
    void ShowBrush(int c)
    {
        if (c == brushShown && brush == brushSizeShown && !sculpting) return; brushShown = c; brushSizeShown = brush;
        var cells = c < 0 ? new List<(int c, int d)>() : Brush(c, brush); int n = cells.Count;
        while (planLines.Count < n) { var l = new GameObject("Plan").AddComponent<LineRenderer>(); l.loop = true; l.widthMultiplier = 0.3f; l.material = Raatstad.MatOutline; l.useWorldSpace = true; planLines.Add(l); }
        for (int k = 0; k < planLines.Count; k++)
        {
            if (k >= n) { planLines[k].enabled = false; continue; }
            int ci = cells[k].c; var pc = Planet.Cells[ci].corners; planLines[k].enabled = true; planLines[k].widthMultiplier = cells[k].d == 0 ? 0.35f : 0.18f;
            if (planLines[k].positionCount != pc.Length) planLines[k].positionCount = pc.Length;
            for (int q = 0; q < pc.Length; q++) planLines[k].SetPosition(q, pc[q] * (Planet.R + PlanetCity.OwnCorner(ci, q) + (PlanetCity.Plateau(ci) ? PlanetCity.StackHeight(ci) : 0f) + 0.3f));
        }
    }

    // Terrein: de hoekpunten van het terrein rond de muis als ruitjes (wit; zelf verzet met Rand: oranje) en de middens van de
    // vakken (blauw), zodat je ziet waar het terrein knikt. Elke 0,2 s opnieuw (het terrein wordt na een wijziging in stappen bijgewerkt).
    const float VertR = 45f;   // zo ver rond het vak onder de muis
    GameObject verts; MeshFilter[] vertMf; int vertsAt = -2; float vertsT, vertsSize;
    void ShowVerts(int c)
    {
        if (verts == null)
        {
            verts = new GameObject("Hoekpunten"); vertMf = new MeshFilter[3];
            var cols = new[] { ("wit", new Color(0.97f, 0.97f, 0.92f)), ("oranje", new Color(1f, 0.55f, 0.12f)), ("blauw", new Color(0.30f, 0.62f, 1f)) };
            for (int q = 0; q < 3; q++)
            {
                var g = new GameObject("Hoekpunten " + cols[q].Item1); g.transform.SetParent(verts.transform, false);
                vertMf[q] = g.AddComponent<MeshFilter>(); var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = Mats.GetUnlit("p_hoekpunt_" + cols[q].Item1, cols[q].Item2);
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            }
        }
        verts.SetActive(c >= 0); if (c < 0) { vertsAt = -2; return; }
        float size = Mathf.Clamp(Vector3.Distance(Cam.transform.position, hoverPoint) * 0.0045f, 0.12f, 1.6f);   // ongeveer even groot op het scherm
        if (c == vertsAt && Time.time - vertsT < 0.2f && Mathf.Abs(size - vertsSize) < vertsSize * 0.25f) return;
        vertsAt = c; vertsT = Time.time; vertsSize = size;
        var mb = new[] { new MeshBuilder(), new MeshBuilder(), new MeshBuilder() };
        void Dot(MeshBuilder b, Vector3 dir, float h, float s)
        {
            Vector3 up = dir, e = Vector3.Cross(up, Mathf.Abs(up.y) < 0.9f ? Vector3.up : Vector3.right).normalized, n = Vector3.Cross(up, e);
            Vector3 p = dir * (Planet.R + h + s * 0.6f), top = p + up * s * 1.2f, bot = p - up * s * 0.6f;
            Vector3[] r = { p + e * s, p + n * s, p - e * s, p - n * s };
            for (int k = 0; k < 4; k++) { Vector3 a = r[k], b2 = r[(k + 1) % 4], m = (a + b2) * 0.5f - p; b.Tri(top, a, b2, m + up * 0.3f); b.Tri(bot, b2, a, m - up * 0.3f); }
        }
        var seen = new HashSet<long>(); var q2 = new Queue<int>(); var done = new HashSet<int> { c }; q2.Enqueue(c); Vector3 c0 = Planet.Cells[c].dir;
        while (q2.Count > 0)
        {
            int i = q2.Dequeue(); var cell = Planet.Cells[i];
            if (Vector3.Angle(cell.dir, c0) * Mathf.Deg2Rad * Planet.R > VertR) continue;
            Dot(mb[2], cell.dir, PlanetCity.TerrainAt(cell.dir), size * 0.7f);
            for (int k = 0; k < cell.corners.Length; k++)
            {
                if (!seen.Add(PlanetCity.CornerKey(i, k))) continue;   // een hoekpunt delen drie vakken: één ruitje
                bool moved = Mathf.Abs(PlanetCity.CornerOfs1(i, k)) > 0.005f;
                Dot(mb[moved ? 1 : 0], cell.corners[k], PlanetCity.Plateau(i) ? PlanetCity.CornerH(i, k) : PlanetCity.OwnCorner(i, k), size);   // een bebouwd vak staat plat op zijn sokkel: dan het maaiveld
            }
            foreach (int j in cell.nb) if (done.Add(j)) q2.Enqueue(j);
        }
        for (int q = 0; q < 3; q++) { var old = vertMf[q].sharedMesh; vertMf[q].sharedMesh = mb[q].Build(); if (old != null) Destroy(old); }
    }

    static readonly string[] StyleNames = { "Gewone weg", "Brede weg", "Laan" };
    static readonly string[] StyleIds = { "weg", "weg:breed", "weg:laan" };
    void ToggleBike() { PlanetCity.DragBike = !PlanetCity.DragBike; Say(PlanetCity.DragBike ? "Fiets- en voetpad (B): breder, met een rood fietsdeel; fietsers rijden erover" : "Voetpad (B)"); }
    void CycleStyle() { PlanetCity.DragStyle = (byte)((PlanetCity.DragStyle + 1) % 3); SetVariant(StyleIds[PlanetCity.DragStyle]); Say("Wegtype: " + StyleNames[PlanetCity.DragStyle] + " (B) · sleep of klik om aan te leggen; een weg over water of een uitgraving wordt een brug"); }
    void ToggleUnder() { PlanetCity.DragUnder = !PlanetCity.DragUnder; string w = CurBlock == "pad" ? "het pad" : "de weg"; Say(PlanetCity.DragUnder ? "Ongelijkvloers (U): " + w + " dat je sleept gaat onder kruisende wegen en paden door" : "Gelijkvloers (U): kruisende wegen en paden worden een kruispunt of oversteek"); }
    void ToggleOneWay() { PlanetCity.DragOneWay = !PlanetCity.DragOneWay; Say(PlanetCity.DragOneWay ? "Eenrichtingsverkeer (I): de weg die je sleept is eenrichting, in de sleeprichting" : "Weer in twee richtingen (I)"); }
    // het niveau van de weg of het pad dat je legt (blijft staan), in halve niveaus: 0 op het maaiveld, -2 tot +2 niveaus, stappen van 2,25 m
    static string LevelName(int lv) { int a = Mathf.Abs(lv); string n = (a / 2 > 0 ? (a / 2).ToString() : "") + (a % 2 == 1 ? "½" : ""); return lv == 0 ? "0" : (lv > 0 ? "+" : "−") + n; }
    static string Meters(int lv) => (Mathf.Abs(lv) * PlanetCity.StepH).ToString("0.##").Replace('.', ',') + " m";
    void StepLevel(int d)
    {
        PlanetCity.DragLevel = Mathf.Clamp(PlanetCity.DragLevel + d, -4, 4); int lv = PlanetCity.DragLevel; string wat = CurBlock == "pad" ? "Pad" : "Weg";
        string extra = lv == -2 ? "; past precies onder een weg door" : lv == 2 && CurBlock == "weg" ? "; een weg op het maaiveld past er precies onder" : Mathf.Abs(lv) == 1 ? "; een half niveau: kruisen wordt een kruispunt" : "";
        Say(lv == 0 ? wat + " op het maaiveld (Page Up/Down kiest het niveau)"
            : wat + " op niveau " + LevelName(lv) + ": " + Meters(lv) + (lv < 0 ? " lager, in een geul" : " hoger, op een dijk") + extra + " (Page Up/Down)");
        if (linePlan != null) ShowPlan(linePlan);
        ghostKey = null;
    }
    void ToggleLights() { PlanetCity.Lights = !PlanetCity.Lights; PlanetCity.TouchAll(); PlanetCity.Rebuild(); Say(PlanetCity.Lights ? "Verkeerslichten aan" : "Verkeerslichten uit"); }

    void SwitchSlot(int n)
    {
        if (n == PlanetCity.Slot) return;
        PlanetCity.Save(); PlanetCity.Slot = n; undo.Clear();
        bool fresh = !PlanetCity.Load();
        if (fresh) { PlanetCity.ClearAll(); PlanetCity.StartTown(Planet.North); PlanetCity.Save(); }   // een nieuwe stad: de standaardstad rond de noordpool
        PlanetCity.Rebuild(); beautyT = -99f; focus = Planet.Cells[Planet.North].dir; fwd = Planet.Cells[Planet.North].north; dist = 230f; pitch = 48f;
        Say("Stad " + n + (fresh ? " (nieuw, rond de noordpool)" : ""));
    }

    void CityCentre()
    {
        if (Time.time - centreT < 2f) return; centreT = Time.time;
        Vector3 sum = Vector3.zero; int n = 0; foreach (var kv in PlanetCity.Cells) { sum += Planet.Cells[kv.Key].dir; n++; }
        hasCentre = n > 0 && sum.sqrMagnitude > 1e-6f; if (hasCentre) centre = sum.normalized;
    }

    void ShowPlan(List<int> plan0)
    {
        // te tekenen vlakken: de vakken op de lijn heel, de halve vakken ernaast alleen de helft die de weg echt bezet
        var polys = new List<Vector3[]>(); var hs = new List<float>();
        if (plan0 != null)
        {
            for (int k = 0; k < plan0.Count; k++)
            {
                polys.Add(Planet.Cells[plan0[k]].corners); hs.Add(PlanetCity.G(plan0[k]) + PlanetCity.StackHeight(plan0[k]) + 0.35f);
                if (k > 0 && System.Array.IndexOf(Planet.Cells[plan0[k - 1]].nb, plan0[k]) < 0)
                {
                    Vector3 nn = Vector3.Cross(Planet.Cells[plan0[k - 1]].dir, Planet.Cells[plan0[k]].dir).normalized;
                    foreach (int hc in PlanetCity.Between(plan0[k - 1], plan0[k])) { polys.Add(PlanetCity.HalfDirs(hc, nn).ToArray()); hs.Add(PlanetCity.G(hc) + PlanetCity.StackHeight(hc) + 0.35f); }
                }
            }
        }
        int n = polys.Count;
        while (planLines.Count < n) { var l = new GameObject("Plan").AddComponent<LineRenderer>(); l.loop = true; l.widthMultiplier = 0.3f; l.material = Raatstad.MatOutline; l.useWorldSpace = true; planLines.Add(l); }
        float lvOff = LevelTool ? PlanetCity.DragLevel * PlanetCity.StepH : 0f;   // een weg of pad op een ander niveau: de lijn ook
        for (int k = 0; k < planLines.Count; k++)
        {
            if (k >= n) { planLines[k].enabled = false; continue; }
            var pc = polys[k]; planLines[k].enabled = true; planLines[k].widthMultiplier = 0.3f; brushShown = -2;
            if (planLines[k].positionCount != pc.Length) planLines[k].positionCount = pc.Length;
            for (int q = 0; q < pc.Length; q++) planLines[k].SetPosition(q, pc[q] * (Planet.R + hs[k] + lvOff));
        }
    }

    // ---------- bouwvoorbeeld: wat je gaat doen, doorzichtig op de plek waar het komt (zoals de webversie) ----------
    //  - een blok: het blok in zijn echte vorm bovenop de stapel (het spookblok);
    //  - een weg of pad: het wegstuk dat een klik zou maken, en tijdens het slepen de hele weg langs de geplande lijn;
    //  - terrein: plaatjes op de hoogte na één flinke streek onder het penseel (terrPreview), in de kleur van de stand;
    //  - de gum: wat er verdwijnt, in het rood (ook een wegstuk).
    // Amber als het kan, rood als het niet kan. Alleen opnieuw gemaakt als er iets verandert.
    MeshFilter ghostF; MeshRenderer ghostR; string ghostKey; static Material ghostOk, ghostBad; readonly Dictionary<int, Material> terrMats = new Dictionary<int, Material>();
    void ShowGhost(int c)
    {
        string type = CurBlock; bool erase = eraser, line = !erase && linePlan != null && lineStart >= 0;
        ShowLevelGhost(LevelTool && !sculpting ? (line ? linePlan : c >= 0 ? new List<int> { c } : null) : null);   // ook op niveau 0: een brug over een verdiepte weg
        if (c < 0 && !line || sculpting || painting) { if (ghostR != null) ghostR.enabled = false; ghostKey = null; return; }
        string key;
        if (line) key = "L|" + type + "|" + PlanetCity.DragStyle + "|" + string.Join(",", linePlan);
        else if (TerrTool && !erase) key = "T|" + c + "|" + terrMode + "|" + brush + "|" + Inp.Held(K.Shift) + "|" + PlanetCity.G(c);
        else { PlanetCity.Cells.TryGetValue(c, out var st); key = c + "|" + type + "|" + erase + "|" + PlanetCity.DragStyle + "|" + (st != null ? string.Join(",", st) : "") + "|" + PlanetCity.G(c); }
        int lvG = LevelTool && !erase && (type == "weg" || type == "pad") ? PlanetCity.DragLevel : 0; key += "|" + lvG;   // op een ander niveau: het voorbeeld schuift mee
        if (key == ghostKey && ghostR != null) { ghostR.enabled = ghostF.sharedMesh != null; return; }
        ghostKey = key;
        if (ghostF == null)
        {
            var go = new GameObject("Bouwvoorbeeld"); ghostF = go.AddComponent<MeshFilter>(); ghostR = go.AddComponent<MeshRenderer>();
            ghostR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; ghostR.receiveShadows = false;
        }
        if (ghostOk == null) { ghostOk = Raatstad.MakeMat(new Color(0.949f, 0.698f, 0.2f, 0.42f), true); ghostBad = Raatstad.MakeMat(new Color(0.878f, 0.341f, 0.243f, 0.45f), true); }
        Mesh mesh; Vector3 origin; bool ok; float scale = 1.01f; Material mat = null;
        if (line)
        {
            mesh = PlanetCity.GhostLine(linePlan, type, out origin); ok = true; foreach (int x in linePlan) if (!PlanetCity.CanRoad(x, type)) ok = false; scale = 1f;
        }
        else if (TerrTool && !erase) { mesh = TerrainGhost(c, out origin); ok = true; scale = 1f; mat = TerrMat(terrMode); }
        else if (PlanetCity.IsRoad(c) && erase) { mesh = PlanetCity.GhostRoad(c, type, true, out origin); ok = false; scale = 1.02f; }
        else if (!erase && (type == "weg" || type == "pad")) { mesh = PlanetCity.GhostRoad(c, type, false, out origin); ok = PlanetCity.CanRoad(c, type); scale = 1f; }
        else { mesh = PlanetCity.GhostMesh(c, type, erase, out origin); ok = !erase && PlanetCity.CanPlace(c, type) == null; scale = erase ? 1.04f : 1.01f; }
        var old = ghostF.sharedMesh; ghostF.sharedMesh = mesh; if (old != null) Destroy(old);
        ghostR.sharedMaterial = mat != null ? mat : ok ? ghostOk : ghostBad;
        var t = ghostF.transform; t.position = origin + origin.normalized * (lvG * PlanetCity.StepH); t.localScale = Vector3.one * scale;   // net iets groter, zodat het niet door het echte blok flikkert
        ghostR.enabled = mesh != null;
    }
    // Voorbeeld tijdens het slepen van een weg of pad: de uitgraving (blauw) of het talud (oranje) als een doorzichtige strook langs de
    // lijn, met de bodem zo breed als de weg en zijkanten onder 45°, zoals hij komt; en waar de lijn een weg kruist die een niveau
    // hoger of lager ligt (ook op niveau 0, over een verdiepte weg), alvast de brug: een dek met twee portalen (amber).
    MeshFilter lvlF, brgF; MeshRenderer lvlR, brgR; string lvlKey;
    void ShowLevelGhost(List<int> cells)
    {
        int lv = PlanetCity.DragLevel; string type = CurBlock;
        if (cells == null || cells.Count == 0 || !LevelTool) { if (lvlR != null) lvlR.enabled = false; if (brgR != null) brgR.enabled = false; lvlKey = null; return; }
        string key = lv + "|" + type + "|" + PlanetCity.DragStyle + "|" + string.Join(",", cells);
        if (key == lvlKey && lvlR != null) { lvlR.enabled = lvlF.sharedMesh != null; brgR.enabled = brgF.sharedMesh != null; return; } lvlKey = key;
        if (lvlF == null)
        {
            var go = new GameObject("Niveauvoorbeeld"); lvlF = go.AddComponent<MeshFilter>(); lvlR = go.AddComponent<MeshRenderer>(); lvlR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lvlR.receiveShadows = false;
            var gb = new GameObject("Brugvoorbeeld"); brgF = gb.AddComponent<MeshFilter>(); brgR = gb.AddComponent<MeshRenderer>(); brgR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; brgR.receiveShadows = false;
        }
        if (ghostOk == null) { ghostOk = Raatstad.MakeMat(new Color(0.949f, 0.698f, 0.2f, 0.42f), true); ghostBad = Raatstad.MakeMat(new Color(0.878f, 0.341f, 0.243f, 0.45f), true); }
        Vector3 origin = Planet.Cells[cells[0]].dir * (Planet.R + PlanetCity.G(cells[0])); var m = new MeshBuilder(); var mb = new MeshBuilder();
        float hw = PlanetCity.WalkHalfWidth(type), R = Planet.R;
        Vector3 P(Vector3 d, Vector3 across, float sdw, float h) => (d * R + across * sdw).normalized * (R + h) - origin;   // punt op de bol: vak d, dwars sdw, hoogte h
        for (int k = 0; k < cells.Count; k++)
        {
            int a = cells[k]; Vector3 ad = Planet.Cells[a].dir;
            float ta = PlanetCity.LevelTarget(a, lv), ga = PlanetCity.G(a);
            if (lv != 0 && k + 1 < cells.Count)   // de strook van hart tot hart, de zijkanten tot op het maaiveld van het vak
            {
                int b = cells[k + 1]; Vector3 bd = Planet.Cells[b].dir, along = (bd - ad - ad * Vector3.Dot(bd - ad, ad)).normalized, ax = Vector3.Cross(ad, along).normalized, bx = Vector3.Cross(bd, along).normalized;
                float tb = PlanetCity.LevelTarget(b, lv), gb = PlanetCity.G(b), wa = hw + Mathf.Abs(ta - ga) / PlanetCity.FieldK, wb = hw + Mathf.Abs(tb - gb) / PlanetCity.FieldK, y = 0.12f;   // zijkanten onder de hoek van het schuifje Talud
                m.Quad(P(ad, ax, -hw, ta + y), P(ad, ax, hw, ta + y), P(bd, bx, hw, tb + y), P(bd, bx, -hw, tb + y), ad);   // bodem of kruin
                m.Quad(P(ad, ax, -hw, ta + y), P(ad, ax, hw, ta + y), P(bd, bx, hw, tb + y), P(bd, bx, -hw, tb + y), -ad);
                for (int sg = -1; sg <= 1; sg += 2)   // de zijkanten onder 45°
                {
                    Vector3 q0 = P(ad, ax, sg * hw, ta + y), q1 = P(ad, ax, sg * wa, ga + y), q2 = P(bd, bx, sg * wb, gb + y), q3 = P(bd, bx, sg * hw, tb + y);
                    m.Quad(q0, q1, q2, q3, ad); m.Quad(q0, q1, q2, q3, -ad);
                }
            }
            else if (lv != 0 && cells.Count == 1)   // één vak: een plaatje op de hoogte
            {
                var cell = Planet.Cells[a]; Vector3 mid = ad * (R + ta + 0.12f) - origin;
                for (int q = 0; q < cell.corners.Length; q++) { Vector3 c1 = Vector3.Lerp(ad, cell.corners[q], 0.92f).normalized * (R + ta + 0.12f) - origin, c2 = Vector3.Lerp(ad, cell.corners[(q + 1) % cell.corners.Length], 0.92f).normalized * (R + ta + 0.12f) - origin; m.Tri(mid, c1, c2, ad); m.Tri(mid, c1, c2, -ad); }
            }
            // een brug: over de lijn heen (de nieuwe gaat eronderdoor) of langs de lijn (de nieuwe gaat erover)
            int cr = PlanetCity.CrossingPreview(a, cells, lv, type, out float deck);
            if (cr == 0) continue;
            Vector3 dir;   // de richting van het dek
            if (cr > 0) { int p0 = k > 0 ? cells[k - 1] : a, p1 = k + 1 < cells.Count ? cells[k + 1] : a; dir = Planet.Cells[p1].dir - Planet.Cells[p0].dir; }
            else { var ps = PlanetCity.Partners(a); dir = ps.Count >= 2 ? Planet.Cells[ps[1]].dir - Planet.Cells[ps[0]].dir : Planet.Cells[a].north; }
            dir -= ad * Vector3.Dot(dir, ad); if (dir.sqrMagnitude < 1e-6f) continue; dir.Normalize(); Vector3 acr = Vector3.Cross(ad, dir).normalized;
            float dw = cr > 0 ? hw - 1.5f : PlanetCity.WalkHalfWidth(PlanetCity.IsPath(a) ? "pad" : "weg") - 1.5f, L = 7.8f, top = deck + 0.3f, bot = deck - 1.08f;
            Vector3 Q(float along, float sdw, float h) => (ad * R + dir * along + acr * sdw).normalized * (R + h) - origin;
            Vector3[] t4 = { Q(-L, -dw, top), Q(-L, dw, top), Q(L, dw, top), Q(L, -dw, top) }, b4 = { Q(-L, -dw, bot), Q(-L, dw, bot), Q(L, dw, bot), Q(L, -dw, bot) };
            mb.Quad(t4[0], t4[1], t4[2], t4[3], ad); mb.Quad(b4[0], b4[1], b4[2], b4[3], -ad);
            for (int q = 0; q < 4; q++) { int r2 = (q + 1) % 4; mb.Quad(t4[q], t4[r2], b4[r2], b4[q], (t4[q] + t4[r2]) * 0.5f - (ad * R - origin)); }
            for (int sg = -1; sg <= 1; sg += 2)   // de portalen: een lijst van 2 m dik rond de opening, tot 2,5 m onder het dek
            {
                float a0 = sg * L, a1 = sg * (L + 2f), pw = dw + 3f, low = bot - PlanetCity.LevelH + 1.08f;
                foreach (var (w0, w1) in new[] { (-pw, -dw), (dw, pw) })
                {
                    Vector3 f0 = Q(a0, w0, top), f1 = Q(a0, w1, top), f2 = Q(a0, w1, low), f3 = Q(a0, w0, low), e0 = Q(a1, w0, top), e1 = Q(a1, w1, top), e2 = Q(a1, w1, low), e3 = Q(a1, w0, low);
                    mb.Quad(e0, e1, e2, e3, dir * sg); mb.Quad(f0, f1, e1, e0, ad); mb.Quad(f0, e0, e3, f3, -acr); mb.Quad(f1, e1, e2, f2, acr);
                }
                Vector3 l0 = Q(a0, -pw, top), l1 = Q(a0, pw, top), l2 = Q(a1, pw, top), l3 = Q(a1, -pw, top); mb.Quad(l0, l1, l2, l3, ad);
            }
        }
        var old = lvlF.sharedMesh; lvlF.sharedMesh = m.v.Count > 0 ? m.Build() : null; if (old != null) Destroy(old);
        var oldb = brgF.sharedMesh; brgF.sharedMesh = mb.v.Count > 0 ? mb.Build() : null; if (oldb != null) Destroy(oldb);
        lvlR.sharedMaterial = TerrMat(lv < 0 ? 1 : 0); lvlF.transform.position = origin; lvlR.enabled = lvlF.sharedMesh != null;
        brgR.sharedMaterial = ghostOk; brgF.transform.position = origin; brgR.enabled = brgF.sharedMesh != null;
    }
    // terrein: per vak onder het penseel een plaatje op de hoogte na één flinke streek (bebouwde vakken blijven liggen)
    Mesh TerrainGhost(int c, out Vector3 origin)
    {
        origin = Planet.Cells[c].dir * (Planet.R + PlanetCity.G(c)); var m = new MeshBuilder(); bool inv = Inp.Held(K.Shift), raise = (terrMode == 0) != inv;
        float hv = PlanetCity.G(c); if (terrMode == 2 && !inv) { float s = 0f; var bc = Brush(c, brush); foreach (var x in bc) s += PlanetCity.G(x.c); hv = Mathf.Round(s / bc.Count * 2f) / 2f; }
        foreach (var x in Brush(c, brush))
        {
            if (PlanetCity.Plateau(x.c)) continue;
            float w = BrushW(x.d, brush), h = PlanetCity.G(x.c), nh = h;
            if (terrMode <= 1) nh = Mathf.Clamp(h + (raise ? 1f : -1f) * 1.5f * w, PlanetCity.TerrLow, PlanetCity.TerrTop);
            else if (terrMode == 2) nh = hv;
            else if (terrMode == 3) nh = h + (NbAvg(x.c) - h) * Mathf.Min(1f, 0.8f * w);
            else continue;   // helling: geen plaatjes (zoals de webversie); de lijn laat het zien zodra je sleept
            var cell = Planet.Cells[x.c]; Vector3 mid = cell.dir * (Planet.R + nh + 0.12f) - origin;
            for (int k = 0; k < cell.corners.Length; k++)
            {
                Vector3 a = Vector3.Lerp(cell.dir, cell.corners[k], 0.92f).normalized * (Planet.R + nh + 0.12f) - origin, b = Vector3.Lerp(cell.dir, cell.corners[(k + 1) % cell.corners.Length], 0.92f).normalized * (Planet.R + nh + 0.12f) - origin;
                m.Tri(mid, a, b, cell.dir); m.Tri(mid, b, a, -cell.dir);   // van beide kanten zichtbaar (ook als het onder de grond ligt)
            }
        }
        return m.v.Count == 0 ? null : m.Build();
    }
    Material TerrMat(int mode)
    {
        if (terrMats.TryGetValue(mode, out var mt) && mt != null) return mt;
        var col = Find("t:" + mode).col; col.a = 0.35f; mt = Raatstad.MakeMat(col, true); terrMats[mode] = mt; return mt;
    }

    void ShowOutline(int i)
    {
        var c = Planet.Cells[i]; float h = PlanetCity.StackHeight(i) + 0.3f; bool plat = PlanetCity.Plateau(i);
        outline.loop = true; outline.widthMultiplier = 0.35f;   // (de stand Rand tekent er één rand mee)
        if (Planet.IsPole(i) && Time.time > msgT) Say(i == Planet.North ? "De noordpool: hier gaat de as van de planeet doorheen" : "De zuidpool: hier gaat de as van de planeet doorheen");
        else if (c.corners.Length == 5 && Time.time > msgT) Say("Dit is een van de 12 vijfhoeken (oranje): op een bol passen niet alleen zeshoeken");
        else if (Time.time > msgT && System.Array.IndexOf(Planet.Starts, i) >= 0) Say("Startplek (een van de 20): zo ver mogelijk van de vijfhoeken, hier is het raster het netst");
        if (outline.positionCount != c.corners.Length) outline.positionCount = c.corners.Length;
        for (int k = 0; k < c.corners.Length; k++)   // op het plateau, of op het maaiveld en de weg die er helt
            outline.SetPosition(k, c.corners[k] * (Planet.R + (plat ? PlanetCity.G(i) + h : (PlanetCity.IsWalk(i) ? Mathf.Max(PlanetCity.OwnCorner(i, k), PlanetCity.SurfaceH(i, c.corners[k] * Planet.R)) : PlanetCity.OwnCorner(i, k)) + 0.3f)));
    }

    // ---------- camera bij het bouwen (zoals 13-camera, 14-invoer en 15b-sneltoetsen in de webversie) ----------
    // Toetsen: W/A/S/D of de pijltjes schuiven (Shift sneller), Q/E draaien, + en - zoomen. Muis: het wiel zoomt (Shift+wiel kiest
    // een blok), rechts slepen of Shift+slepen schuift, de middelste knop of Alt+slepen draait en kantelt. Schuiven pakt de grond
    // vast: die volgt de muis. Een klik met rechts zonder te slepen maakt een vak leeg. Links slepen bouwt (de webversie draait dan).
    void Pan(Vector3 metres) { focus = (focus + metres / Planet.R).normalized; fwd = (fwd - focus * Vector3.Dot(fwd, focus)).normalized; }
    void CameraControl()
    {
        float dt = Time.deltaTime, sp = (Inp.Held(K.Shift) ? 3f : 1f) * dist;   // ongeveer een schermbreedte per seconde, zoals de webversie
        Vector3 up = focus; fwd = (fwd - up * Vector3.Dot(fwd, up)).normalized; Vector3 right = Vector3.Cross(up, fwd);
        Vector3 move = Vector3.zero; float turn = 0f, zoomIn = 0f;
        if (!Modal)
        {
            if (Inp.Held(K.W) || Inp.Held(K.Up)) move += fwd; if (Inp.Held(K.S) || Inp.Held(K.Down)) move -= fwd;
            if (Inp.Held(K.D) || Inp.Held(K.Right)) move += right; if (Inp.Held(K.A) || Inp.Held(K.Left)) move -= right;
            turn = (Inp.Held(K.Q) ? 1f : 0f) - (Inp.Held(K.E) ? 1f : 0f);
            if (!PathDrag) zoomIn = (Inp.Held(K.Plus) ? 1f : 0f) - (Inp.Held(K.Minus) ? 1f : 0f);   // een pad slepen: + en - kiezen het niveau
        }
        if (move != Vector3.zero) Pan(move.normalized * sp * dt);
        if (turn != 0f) fwd = Quaternion.AngleAxis(turn * 92f * dt, focus) * fwd;   // 1,6 rad/s
        if (!Ui.MouseOverUI && !Modal && !Inp.Held(K.Shift) && !PathDrag) dist *= 1f - Inp.Scroll * 0.1f;   // scrollen in de catalogus zoomt niet
        if (zoomIn != 0f) dist *= Mathf.Pow(2f, -zoomIn * 1.5f * dt);
        dist = Mathf.Clamp(dist, 12f, 2600f);
        // slepen met de muis: schuiven of draaien
        if (camDrag == 0 && !Modal && !Ui.MouseOverUI)
        {
            bool alt = Inp.Held(K.Alt), shiftPan = Inp.Held(K.Shift) && !TerrTool;   // terrein: Shift boetseert andersom
            if (Inp.MiddleDown) { camDrag = 2; camBtn = 2; }
            else if (Inp.LeftDown && alt) { camDrag = 2; camBtn = 0; }
            else if (Inp.RightDown) { camDrag = 1; camBtn = 1; }
            else if (Inp.LeftDown && shiftPan) { camDrag = 1; camBtn = 0; }
            if (camDrag != 0) { camFrom = Inp.Pos; camMoved = camDrag == 2; }
        }
        if (camDrag != 0)
        {
            bool held = camBtn == 0 ? Inp.LeftHeld : camBtn == 1 ? Inp.RightHeld : Inp.MiddleHeld;
            if (!held || Modal) camDrag = 0;
            else
            {
                if (!camMoved && Vector2.Distance(Inp.Pos, camFrom) > 6f) { camMoved = true; rightCell = -1; }   // gesleept: geen klik met rechts
                Vector2 d = Inp.Delta;
                if (camMoved && camDrag == 1) { right = Vector3.Cross(focus, fwd); Pan(-(right * d.x + fwd * d.y) * dist * 0.0019f); }
                else if (camDrag == 2) { fwd = Quaternion.AngleAxis(d.x * 0.4f, focus) * fwd; pitch = Mathf.Clamp(pitch - d.y * 0.34f, 15f, 89f); }
            }
        }
        up = focus; fwd = (fwd - up * Vector3.Dot(fwd, up)).normalized;
        int fc = Planet.Nearest(focus); float h = PlanetCity.Plateau(fc) ? PlanetCity.G(fc) + PlanetCity.StackHeight(fc) : PlanetCity.TerrainAt(focus);   // het maaiveld onder het midden (geen sprong per vak)
        camH = float.IsNaN(camH) || Mathf.Abs(h - camH) > 150f ? h : Mathf.Lerp(camH, h, 1f - Mathf.Exp(-6f * dt));   // vloeiend mee omhoog en omlaag over bergen
        Vector3 target = focus * (Planet.R + camH);
        Vector3 pos = target + (-fwd * Mathf.Cos(pitch * Mathf.Deg2Rad) + up * Mathf.Sin(pitch * Mathf.Deg2Rad)) * dist;
        { float gp = PlanetCity.TerrainAt(pos.normalized) + 2f; if (pos.magnitude < Planet.R + gp) pos = pos.normalized * (Planet.R + gp); }   // niet door een bergflank
        Cam.transform.position = pos; Cam.transform.rotation = Geo.Look(target - pos, up);
    }

    void ToggleWalk()
    {
        walking = !walking; PlanetCity.ShowGrid(PlanetCity.GridOn && !walking); catOpen = todOpen = menuOpen = scoreOpen = helpOpen = welcome = wheelOpen = false;
        if (walking)
        {
            walkT = Time.time; outline.enabled = false; ShowBrush(-1); ShowVerts(-1); ShowPlan(null); ShowGhost(-1);
            int i = Planet.Nearest(focus); float h = PlanetCity.G(i) + PlanetCity.StackHeight(i);
            if (PlanetCity.IsWalk(i)) h = PlanetCity.SurfaceH(i, focus * Planet.R) + 0.26f;
            walker.Enter(focus * (Planet.R + h + 0.3f), fwd);
        }
        else { walker.Exit(); focus = walker.transform.position.normalized; fwd = walker.Forward; }
    }

    // ---------- cameramodus (zoals flyCam in de webversie, 13-camera-modus-geluid.js) ----------
    // Spatie in de bouwmodus aan en uit (Esc ook). Geen interface, geen bouwvoorbeeld en geen raster; bouwen is geblokkeerd.
    // De muis kijkt (aanwijzer vast), W/A/S/D vliegt met vaart (de snelheid volgt het doel met dt·3,5, zoals een drone), E/Q hoger
    // en lager. Snelheid max(12, hoogte·0,9) m/s, Shift ×3, Ctrl ×¼, ingezoomd rustiger. Het wiel zoomt (beeldhoek 12–95°, start 65°),
    // F bewaart een foto zonder knoppen. Altijd minstens 2 m boven terrein of blok. Bij stoppen komt het draaipunt op wat je aankijkt.
    // Op de bol is omhoog de richting vanaf het middelpunt: de kijkrichting is een koers langs het oppervlak plus een hoek omhoog.
    bool flying; Vector3 flyPos, flyVel, flyHead; float flyPitch, flyFovT = 65f, oldFov, oldNear, flyBadgeT, flashT = -99f; bool photoFrame; string flyNote;
    void FlyStart()
    {
        flying = true; catOpen = todOpen = false; eraser = false;
        flyPos = Cam.transform.position; Vector3 up = flyPos.normalized, look = Cam.transform.forward;
        flyHead = look - up * Vector3.Dot(look, up); if (flyHead.sqrMagnitude < 1e-6f) flyHead = fwd; flyHead.Normalize();
        flyPitch = Mathf.Asin(Mathf.Clamp(Vector3.Dot(look, up), -1f, 1f)) * Mathf.Rad2Deg; flyVel = Vector3.zero;
        oldFov = Cam.fieldOfView; oldNear = Cam.nearClipPlane; Cam.fieldOfView = 65f; flyFovT = 65f; Cam.nearClipPlane = 0.3f;
        outline.enabled = false; ShowBrush(-1); ShowVerts(-1); ShowPlan(null); ShowGhost(-1); PlanetCity.ShowGrid(false); painting = false; lineStart = -1;
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; flyBadgeT = Time.time; flyNote = null;
    }
    void FlyStop()
    {
        flying = false; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        Cam.fieldOfView = oldFov; Cam.nearClipPlane = oldNear; PlanetCity.ShowGrid(PlanetCity.GridOn);
        // het draaipunt op wat je aankijkt (of een stuk voor je uit), de camera blijft waar hij is
        Vector3 up = flyPos.normalized, look = Cam.transform.forward, tg;
        if (Physics.Raycast(flyPos, look, out RaycastHit h, 1500f, Layers.Grond, QueryTriggerInteraction.Ignore) && h.distance > 8f) tg = h.point;
        else tg = flyPos + look * 120f;
        focus = tg.normalized; Vector3 tu = focus, dv = flyPos - tg; float len = dv.magnitude;
        dist = Mathf.Clamp(len, 14f, 2600f);
        Vector3 flat = -(dv - tu * Vector3.Dot(dv, tu)); if (flat.sqrMagnitude > 1e-6f) fwd = flat.normalized;
        pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(Vector3.Dot(dv, tu) / Mathf.Max(1e-3f, len), -1f, 1f)) * Mathf.Rad2Deg, 15f, 89f);
    }
    void FlyUpdate()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.1f);
        if (Inp.Down(K.Space) || Inp.Down(K.Esc)) { FlyStop(); return; }
        if (Inp.Down(K.F)) { photoFrame = true; StartCoroutine(Photo()); }
        if (Cursor.lockState != CursorLockMode.Locked && Inp.LeftDown) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }   // na alt-tab weer vastzetten met een klik
        Vector3 up = flyPos.normalized;
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 d = Inp.Delta; const float sens = 0.0022f * Mathf.Rad2Deg;
            flyHead = Quaternion.AngleAxis(d.x * sens, up) * flyHead; flyPitch = Mathf.Clamp(flyPitch + d.y * sens, -86f, 86f);
        }
        flyHead -= up * Vector3.Dot(flyHead, up); flyHead.Normalize();
        float sc = Inp.Scroll; if (Mathf.Abs(sc) > 0.01f) flyFovT = Mathf.Clamp(flyFovT * Mathf.Pow(1.12f, -Mathf.Sign(sc) * Mathf.Min(3f, Mathf.Abs(sc))), 12f, 95f);
        Cam.fieldOfView += (flyFovT - Cam.fieldOfView) * Mathf.Min(1f, dt * 6f);
        Vector3 look = flyHead * Mathf.Cos(flyPitch * Mathf.Deg2Rad) + up * Mathf.Sin(flyPitch * Mathf.Deg2Rad), right = Vector3.Cross(up, flyHead);
        float f = (Inp.Held(K.W) ? 1f : 0f) - (Inp.Held(K.S) ? 1f : 0f), s = (Inp.Held(K.D) ? 1f : 0f) - (Inp.Held(K.A) ? 1f : 0f), u = (Inp.Held(K.E) ? 1f : 0f) - (Inp.Held(K.Q) ? 1f : 0f);
        int cell = Planet.Nearest(flyPos); float ground = PlanetCity.G(cell) + (PlanetCity.Plateau(cell) ? PlanetCity.StackHeight(cell) : PlanetCity.IsWalk(cell) ? 0.25f : 0f);
        float alt = flyPos.magnitude - Planet.R;
        float speed = Mathf.Max(12f, (alt - PlanetCity.G(cell)) * 0.9f) * (Inp.Held(K.Shift) ? 3f : 1f) * (Inp.Held(K.Ctrl) ? 0.25f : 1f) * (Cam.fieldOfView / 65f);   // ingezoomd: rustiger
        Vector3 want = (look * f + right * s + up * u) * speed;
        flyVel += (want - flyVel) * Mathf.Min(1f, dt * 3.5f);   // vaart: optrekken en uitglijden
        flyPos += flyVel * dt;
        float r = Mathf.Clamp(flyPos.magnitude, Planet.R + ground + 2f, Planet.R + 1800f); flyPos = flyPos.normalized * r;
        up = flyPos.normalized; flyHead -= up * Vector3.Dot(flyHead, up); flyHead.Normalize();
        look = flyHead * Mathf.Cos(flyPitch * Mathf.Deg2Rad) + up * Mathf.Sin(flyPitch * Mathf.Deg2Rad);
        Cam.transform.position = flyPos; Cam.transform.rotation = Geo.Look(look, up);
        focus = up;   // de lucht, de mist en het geluid volgen mee
    }
    // foto: het beeld zelf zonder knoppen, als png in Afbeeldingen\Raatstad
    System.Collections.IEnumerator Photo()
    {
        yield return new WaitForEndOfFrame();   // dit beeld is zonder interface getekend (photoFrame)
        try
        {
            string dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyPictures), "Raatstad"); System.IO.Directory.CreateDirectory(dir);
            var tex = ScreenCapture.CaptureScreenshotAsTexture(); string path = System.IO.Path.Combine(dir, "raatstad-" + System.DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png");
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG()); Destroy(tex);
            flyNote = "Foto bewaard: " + path; flashT = Time.time; flyBadgeT = Time.time;
        }
        catch (System.Exception e) { flyNote = "Foto maken lukte niet (" + e.Message + ")"; flyBadgeT = Time.time; }
        photoFrame = false;
    }
    // in de cameramodus alleen een klein label dat na een paar seconden vervaagt, en een flits bij een foto
    void FlyGui(float W, float H)
    {
        if (Time.time - flashT < 0.45f) Ui.Tex(new Rect(0, 0, W, H), Texture2D.whiteTexture, new Color(1f, 1f, 1f, 0.7f * (1f - (Time.time - flashT) / 0.45f)));
        string t = flyNote ?? "<b>Cameramodus</b> · muis kijkt · W A S D · E/Q hoger/lager · Shift snel · wiel zoomt · F foto · Spatie stopt";
        float a = Time.time - flyBadgeT < 3.5f ? 1f : 0.35f, w = Ui.Width(t, Ui.T.Text) + 28f; var r = new Rect((W - w) * 0.5f, H - 16f - 32f, w, 32f);
        var old = GUI.color; GUI.color = new Color(1f, 1f, 1f, a); Ui.Panel(r, false, 16f); Ui.Label(r, t, Ui.T.Text, null, TextAnchor.MiddleCenter); GUI.color = old;
    }

    // ================= interface =================
    const float G = 16f;   // marge langs de randen
    void OnGUI()
    {
        Ui.Begin(); float W = Ui.W, H = Ui.H;
        if (loading != null) { LoadingGui(W, H); Ui.End(); return; }
        if (Time.time - beautyT > 3f) { beauty = Beauty.Compute(); beautyT = Time.time; }
        fpsN++; if (Time.time - fpsT > 0.5f) { fps = fpsN / (Time.time - fpsT); fpsN = 0; fpsT = Time.time; }
        if (flying) { if (!photoFrame) FlyGui(W, H); Ui.End(); return; }   // cameramodus: geen interface

        CentreArrow(W, H);
        if (!walking) { TopLeft(); Incidents(); } TopRight(W);   // lopen: zo min mogelijk in beeld, de stad zelf is het beeld
        if (!walking) { Hotbar(W, H); Tools(H); if (!catOpen) Options(W, H); }
        else WalkHint(W, H);
        ModeSwitch(W, H);
        if (todOpen) TodPanel(W);
        if (catOpen && !walking) Catalogue(W, H);
        if (wheelOpen && !walking && !Modal) Wheel();
        if (TerrTool && hasHover && !walking && !Modal) TerrLabel();
        else if (prevOk && hasHover && ShowPreview && !walking && !Modal && !Ui.MouseOverUI) PreviewLabel();
        if (LevelTool && !walking) LayerPick(W, H);
        if (Time.time < msgT) Ui.Toast(msg, Mathf.Clamp01((msgT - Time.time) / 0.5f), catOpen ? CatBottom + CatHeight(W) + 12f : walking ? G + 52f : DockTop);
        if (scoreOpen) ScorePanel(W, H);
        if (menuOpen) Menu(W, H);
        if (helpOpen) Help(W, H);
        if (welcome) Welcome(W, H);
        Ui.End();
    }

    // ---- lopen: de toetsen onderaan, na een paar seconden weg
    float walkT;
    void WalkHint(float W, float H)
    {
        float a = Mathf.Clamp01(1f - (Time.time - walkT - 6f)); if (a <= 0f) return;
        string wt = "WASD lopen · muis kijken · Shift rennen · Spatie springen · Esc muis los · L terug naar bouwen · ? toetsen"; float ww = Ui.Width(wt, Ui.T.Dim) + 40f; var wr = new Rect((W - ww) * 0.5f, H - G - 40, ww, 40);
        var old = GUI.color; GUI.color = new Color(1f, 1f, 1f, a); Ui.Panel(wr, false, 20f); Ui.Label(wr, wt, Ui.T.Dim, null, TextAnchor.MiddleCenter); GUI.color = old;
    }

    // ---- linksboven: logo, naam, stad en de schoonheid (klikbaar)
    void TopLeft()
    {
        PlanetCity.LatLon(walking ? walker.transform.position : focus, out float lat, out float lon);   // waar je bent: breedte en lengte (evenaar rood-wit op de grond)
        string sub = "Stad " + PlanetCity.Slot + (beauty != null ? " · " + beauty.blocks + " blokken" : "") + " · " + Mathf.Abs(lat).ToString("0") + "° " + (lat >= 0f ? "N" : "Z") + " " + Mathf.Abs(lon).ToString("0") + "° " + (lon >= 0f ? "O" : "W");
        float tw = Mathf.Max(Ui.Width("RAATSTAD", Ui.T.Title), Ui.Width(sub, Ui.T.Dim, 11)), w = 40f + tw + 16f + 62f;
        var r = new Rect(G, 12f, w, 48f); Ui.Panel(r, false, 12f);
        Ui.Tex(new Rect(r.x + 11f, r.y + 11.5f, 22f, 25f), Ui.Hex(Ui.Signal, Ui.Signal, 0f, true));
        Ui.Label(new Rect(r.x + 41f, r.y + 4f, tw, 24f), "RAATSTAD", Ui.T.Title);
        Ui.Label(new Rect(r.x + 41f, r.y + 27f, tw + 4f, 15f), sub, Ui.T.Dim, null, null, 11);
        var sr = new Rect(r.xMax - 64f, r.y + 6f, 56f, 36f); bool hv = sr.Contains(Ui.Mouse);
        Ui.Tex(sr, Ui.Hex(new Color(Ui.Signal.r, Ui.Signal.g, Ui.Signal.b, hv ? 0.22f : 0.12f), Ui.Signal, 1.5f));
        Ui.Label(new Rect(sr.x, sr.y + 1f, sr.width, 22f), beauty != null && beauty.score > 0 ? beauty.score.ToString() : "–", Ui.T.Title, Ui.Signal, TextAnchor.MiddleCenter, 20);
        Ui.Label(new Rect(sr.x, sr.y + 21f, sr.width, 12f), "SCHOONHEID", Ui.T.Tiny, Ui.Signal, TextAnchor.MiddleCenter, 7);
        Ui.Hit(sr); if (GUI.Button(sr, GUIContent.none, GUIStyle.none)) { scoreOpen = !scoreOpen; beautyT = -99f; }
    }

    // ---- onder de stadsnaam: de ongelukjes (zoals de webversie); kleurt even rood bij een nieuw ongeluk, een klik gaat erheen
    void Incidents()
    {
        var L = PlanetLife.I; if (L == null || (!PlanetLife.Chaos && L.Crashes + L.Knocked == 0)) return;
        string t = "Botsingen <color=#F2B233>" + L.Crashes + "</color>  ·  Aangereden <color=#F2B233>" + L.Knocked + "</color>";
        var r = new Rect(G, 12f + 48f + 6f, Ui.Width(t, Ui.T.Bold, 11) + 22f, 24f); bool fresh = Time.time - L.LastIncidentT < 1.5f;
        if (Ui.Button(r, null, false, null, false, 12f) && L.LastIncidentT > 0f) { focus = L.LastIncident.normalized; Say("Naar het laatste ongelukje"); }
        Ui.Label(r, t, Ui.T.Bold, fresh ? new Color(1f, 0.55f, 0.5f) : Ui.Ink, TextAnchor.MiddleCenter, 11);
    }

    // ---- rechtsboven: tijd en weer (paneel), geluid, menu
    void TopRight(float W)
    {
        float x = W - G, y = 12f, h = 36f;
        float mw = Ui.Width("Menu", Ui.T.Bold) + 28f; if (Ui.Button(new Rect(x - mw, y, mw, h), "Menu", menuOpen)) { menuOpen = !menuOpen; todOpen = false; }
        Ui.Tip(new Rect(x - mw, y, mw, h), "Menu", "Steden, gereedschap en instellingen", "M"); x -= mw + 8f;
        if (Ui.Button(new Rect(x - h, y, h, h), null, false, Babble.Muted ? "stil" : "geluid")) ToggleSound();
        Ui.Tip(new Rect(x - h, y, h, h), Babble.Muted ? "Geluid aan" : "Geluid uit"); x -= h + 8f;
        string clock = DayNight.Clock; float cw = Ui.Width(clock, Ui.T.Bold) + 18f + 7f + 26f;
        if (Ui.Button(new Rect(x - cw, y, cw, h), clock, todOpen, WeatherIcon(true))) { todOpen = !todOpen; menuOpen = false; }
        if (!todOpen) Ui.Tip(new Rect(x - cw, y, cw, h), "Tijd en weer", "P stilzetten · O ander weer");
    }
    string WeatherIcon(bool withNight)
    {
        var k = Weather.I != null ? Weather.I.Current : Weather.Kind.Zon;
        if (k == Weather.Kind.Zon) return withNight && DayNight.Night > 0.6f ? "maan" : "zon";
        return k == Weather.Kind.Bewolkt ? "bewolkt" : k == Weather.Kind.Regen ? "regen" : k == Weather.Kind.Onweer ? "onweer" : k == Weather.Kind.Mist ? "mist" : "sneeuw";
    }

    // ---- het tijdpaneel: tijd van de dag, snelheid, weer en wat je ziet (zoals de webversie)
    void TodPanel(float W)
    {
        var r = new Rect(W - G - 312f, 56f, 312f, 252f); Ui.Panel(r, true, 14f); float x = r.x + 16f, iw = r.width - 32f, y = r.y + 12f;
        float hr = DayNight.Hour; string part = hr < 6f || hr >= 22f ? "NACHT" : hr < 12f ? "OCHTEND" : hr < 18f ? "MIDDAG" : "AVOND";
        Ui.Label(new Rect(x, y, 140f, 34f), DayNight.Clock, Ui.T.Big); Ui.Label(new Rect(x, y, iw, 34f), part, Ui.T.Tiny, null, TextAnchor.MiddleRight); y += 42f;
        float nv = Ui.Slider(new Rect(x, y, iw, 16f), hr / 24f); if (Mathf.Abs(nv * 24f - hr) > 0.01f) DayNight.Hour = Mathf.Clamp(nv * 24f, 0f, 23.99f); y += 26f;
        string[] parts = { "Ochtend", "Middag", "Avond", "Nacht" }; float[] at = { 8f, 13f, 19f, 23f }; float bw = (iw - 18f) / 4f;
        for (int k = 0; k < 4; k++) if (Ui.Button(new Rect(x + k * (bw + 6f), y, bw, 34f), parts[k], false, null, false, 10f, true)) DayNight.Hour = at[k];
        y += 46f;
        float[] speeds = { 0f, 1f, 4f, 12f }; int sel = System.Array.IndexOf(speeds, DayNight.Speed);
        Ui.Label(new Rect(x, y, 90f, 32f), "SNELHEID", Ui.T.Tiny); int hit = Ui.Seg(new Rect(x + iw - 176f, y, 176f, 32f), new[] { "0", "1×", "4×", "12×" }, sel); if (hit >= 0) DayNight.Speed = speeds[hit]; y += 40f;
        var wx = new[] { "zon", "bewolkt", "regen", "onweer", "mist", "sneeuw", "wissel" }; int wsel = Weather.I == null ? -1 : Weather.I.Wissel ? 6 : (int)Weather.I.Current;
        Ui.Label(new Rect(x, y, 90f, 32f), "WEER", Ui.T.Tiny); hit = Ui.Seg(new Rect(x + iw - 212f, y, 212f, 32f), wx, wsel, true);
        if (hit >= 0 && Weather.I != null) { if (hit == 6) { Weather.I.Wissel = true; Say("Weer: wisselend"); } else { Weather.I.Set((Weather.Kind)hit, true); Say("Weer: " + Weather.Names[hit]); } }
        y += 40f;
        Ui.Label(new Rect(x, y, 90f, 30f), "BEELD", Ui.T.Tiny); float cx = r.xMax - 16f;
        float w3 = Ui.Width("Auto's", Ui.T.Bold) + 22f; if (Ui.Button(new Rect(cx - w3, y, w3, 30f), "Auto's", PlanetLife.CarsOn)) PlanetLife.CarsOn = !PlanetLife.CarsOn; cx -= w3 + 6f;
        float w2 = Ui.Width("Mensen", Ui.T.Bold) + 22f; if (Ui.Button(new Rect(cx - w2, y, w2, 30f), "Mensen", PlanetLife.PeopleOn)) PlanetLife.PeopleOn = !PlanetLife.PeopleOn; cx -= w2 + 6f;
        float w1 = Ui.Width("Raster", Ui.T.Bold) + 22f; if (Ui.Button(new Rect(cx - w1, y, w1, 30f), "Raster", PlanetCity.GridOn)) ToggleGrid();
    }

    // ---- onderaan: de zes categorieën (1–6), elk met het laatst gekozen blok; een klik opent de lade met de blokken ervan
    const float SlotW = 54f, SlotH = 48.6f, CatW = 72f, BarH = SlotH + 24f;
    void Hotbar(float W, float H)
    {
        string sug = SuggestId();
        float bw = Tabs.Length * CatW + (Tabs.Length - 1) * 3f + 12f; var r = new Rect((W - bw) * 0.5f, H - 12f - BarH, bw, BarH); Ui.Panel(r, false, 14f);
        for (int k = 0; k < Tabs.Length; k++)
        {
            var it = Find(bar[k]); float cx = r.x + 6f + k * (CatW + 3f); var sr = new Rect(cx + (CatW - SlotW) * 0.5f, r.y + 4f, SlotW, SlotH);
            bool on = k == slot && !eraser;
            if (Ui.HexSlot(sr, Ui.Swatch(it.id, it.col), IconOf(it), (k + 1).ToString(), on)) { if (on) catOpen = !catOpen; else { SelectSlot(k); catOpen = true; } }
            Ui.Label(new Rect(cx, sr.yMax + 1f, CatW, 15f), Tabs[k], Ui.T.Bold, on ? Ui.Signal : Ui.InkDim, TextAnchor.MiddleCenter, 10);
            if (sug != null && TabOf(sug) == k) Ui.Tex(new Rect(sr.xMax - 9f, sr.y + 1f, 9f, 8f), Ui.Hex(Ui.Signal, Ui.Signal, 0f));   // hier zit het voorstel
            if (!catOpen) Ui.Tip(new Rect(cx, r.y, CatW, r.height), Tabs[k] + ": " + it.name, CountIn(k) > 1 ? "Klik: alle " + CountIn(k) + " · nog eens " + (k + 1) + ": volgende" : Desc(it.id), (k + 1).ToString());
        }
        if (Time.time < nameT && !catOpen && !HasOptions)   // met keuzes staat de naam vooraan in die balk
        {
            string nm = eraser ? "Gum" : Cur.name; float nw = Ui.Width(nm, Ui.T.Bold) + 24f; var nr = new Rect((W - nw) * 0.5f, r.y - 8f - 26f, nw, 26f);
            float a = Mathf.Clamp01((nameT - Time.time) / 0.3f); var old = GUI.color; GUI.color = new Color(1f, 1f, 1f, a); Ui.Panel(nr, true, 13f); Ui.Label(nr, nm, Ui.T.Bold, null, TextAnchor.MiddleCenter); GUI.color = old;
        }
    }

    // ---- het voorstel: het blok dat het zwakste onderdeel van de schoonheid verbetert (zelfde keuze als de tip in het schoonheidspaneel)
    Beauty.Row SuggestRow()
    {
        if (beauty == null) return null; Beauty.Row weak = null;
        foreach (var r in beauty.rows) { if (r.v < 0f) continue; if (weak == null || r.v * r.w < weak.v * weak.w) weak = r; }
        return weak != null && weak.v < 0.9f ? weak : null;
    }
    string SuggestId()
    {
        var r = SuggestRow(); if (r == null) return null;
        switch (r.name) { case "Groen": return "park"; case "Levendig": return "plein"; case "Voorzieningen": return "winkel"; case "Blikvangers": return "toren"; case "Water": return "meer"; case "Bereikbaar": return "pad"; case "Wegennet": return "weg"; }
        return null;   // afwisseling en samenhang: geen vast blok
    }

    // ---- de lade: de blokken van de gekozen categorie, net boven de balk; een keuze wordt het blok van die categorie
    const float CatBottom = 12f + BarH + 8f, CardW = 112f;
    float CatWidth(float W) { int n = CountIn(slot); return Mathf.Clamp(20f + n * CardW + (n - 1) * 8f, 320f, Mathf.Min(920f, W - 24f)); }
    int CatPer(float cw) => Mathf.Max(1, Mathf.FloorToInt((cw - 20f + 8f) / (CardW + 8f)));
    float CatHeight(float W) => 40f + Mathf.Ceil(CountIn(slot) / (float)CatPer(CatWidth(W))) * 88f + 2f;
    void Catalogue(float W, float H)
    {
        float cw = CatWidth(W), ch = CatHeight(W); var r = new Rect((W - cw) * 0.5f, H - CatBottom - ch, cw, ch); Ui.Panel(r, true, 16f);
        Ui.Label(new Rect(r.x + 16f, r.y + 8f, 200f, 26f), Tabs[slot].ToUpper(), Ui.T.Tiny, Ui.Signal, null, 11);
        Ui.Label(new Rect(r.x + 16f, r.y + 8f, cw - 32f, 26f), "Tab vasthouden: keuzewiel", Ui.T.Dim, null, TextAnchor.MiddleRight, 11);
        int per = CatPer(cw), k = 0; float cardW = (cw - 20f - (per - 1) * 8f) / per; string sug = SuggestId(); var sugRow = SuggestRow();
        foreach (var it in Catalog)
        {
            if (it.tab != Tabs[slot]) continue;
            var c = new Rect(r.x + 10f + (k % per) * (cardW + 8f), r.y + 38f + (k / per) * 88f, cardW, 80f); k++;
            bool on = it.id == bar[slot] && !eraser, hv = c.Contains(Ui.Mouse);
            bool click = Ui.Button(c, null, false, null, false, 10f, true);
            var sr = new Rect(c.center.x - 21f, c.y + 8f, 42f, 38f); Ui.Tex(sr, Ui.Swatch(it.id, it.col));
            Ui.Tex(sr, Ui.Hex(Color.clear, on ? Ui.Signal : new Color(1f, 1f, 1f, hv ? 0.5f : 0.25f), on ? 2.5f : 1.5f));
            string ic = IconOf(it); if (ic != null) Ui.Tex(new Rect(sr.center.x - 11f, sr.center.y - 11f, 22f, 22f), Ui.Icon(ic), Color.white);
            Ui.Label(new Rect(c.x, c.y + 50f, c.width, 22f), it.name, Ui.T.Bold, on ? Ui.Signal : Ui.Ink, TextAnchor.MiddleCenter);
            bool tip = it.id == sug;
            if (tip) { Ui.Box(c, Color.clear, Ui.Signal, 10f); Ui.Label(new Rect(c.x + 8f, c.y + 5f, 30f, 14f), "TIP", Ui.T.Tiny, Ui.Signal, null, 9); }
            Ui.Tip(c, it.name, tip ? sugRow.name + " is het zwakst: " + sugRow.tip : Desc(it.id));
            if (click) Pick(it.id);
        }
    }

    // ---- het keuzewiel (Tab vasthouden): zes zeshoeken rond de muis, één per categorie; de kant waar de muis heen gaat, kiest
    bool wheelOpen; Vector2 wheelAt; float wheelT;
    const float WheelR = 92f;
    static Vector2 WheelDir(int k) { float a = (-90f + k * 60f) * Mathf.Deg2Rad; return new Vector2(Mathf.Cos(a), Mathf.Sin(a)); }   // Wegen boven, dan met de klok mee
    int WheelSector()
    {
        Vector2 d = Ui.Mouse - wheelAt; if (d.magnitude < 28f) return -1;
        int best = 0; float bd = -2f; for (int k = 0; k < Tabs.Length; k++) { float v = Vector2.Dot(d.normalized, WheelDir(k)); if (v > bd) { bd = v; best = k; } }
        return best;
    }
    void Wheel()
    {
        Ui.Hit(new Rect(0, 0, Ui.W, Ui.H)); int sel = WheelSector();
        Ui.Tex(new Rect(wheelAt.x - 24f, wheelAt.y - 21f, 48f, 42f), Ui.Hex(new Color(Ui.PanelSolid.r, Ui.PanelSolid.g, Ui.PanelSolid.b, 0.9f), Ui.Line, 1.5f));
        Ui.Label(new Rect(wheelAt.x - 40f, wheelAt.y - 10f, 80f, 20f), sel >= 0 ? Tabs[sel] : "kies", Ui.T.Bold, sel >= 0 ? Ui.Signal : Ui.InkDim, TextAnchor.MiddleCenter, 10);
        for (int k = 0; k < Tabs.Length; k++)
        {
            var it = Find(bar[k]); Vector2 p = wheelAt + WheelDir(k) * WheelR; bool on = k == sel;
            float s = on ? 1.15f : 1f; var hr = new Rect(p.x - 36f * s, p.y - 32f * s, 72f * s, 64f * s);
            Ui.Tex(hr, Ui.Hex(new Color(Ui.PanelSolid.r, Ui.PanelSolid.g, Ui.PanelSolid.b, 0.94f), on ? Ui.Signal : Ui.Line, on ? 2.5f : 1.5f));
            var sw = new Rect(p.x - 17f, p.y - 28f * s, 34f, 31f); Ui.Tex(sw, Ui.Swatch(it.id, it.col));
            string ic = IconOf(it); if (ic != null) Ui.Tex(new Rect(sw.center.x - 9f, sw.center.y - 9f, 18f, 18f), Ui.Icon(ic), Color.white);
            Ui.Label(new Rect(p.x - 40f, sw.yMax + 1f, 80f, 16f), Tabs[k], Ui.T.Bold, on ? Ui.Signal : Ui.Ink, TextAnchor.MiddleCenter, 10);
            Ui.Label(new Rect(p.x - 40f, sw.yMax + 13f, 80f, 14f), it.name, Ui.T.Dim, null, TextAnchor.MiddleCenter, 9);
        }
    }

    // ---- boven de snelbalk: de keuzes van het gekozen gereedschap (wegtype, eenrichting, lichten; terreinstand en penseel)
    // Eén balk: vooraan de naam van het gekozen blok (amber), dan de keuzes; meldingen komen er net boven (DockTop).
    bool HasOptions => !catOpen && (eraser || TerrTool || CurBlock == "weg" || CurBlock == "pad");
    float DockTop => 12f + BarH + 8f + (HasOptions ? 42f + 8f : 0f);
    static float BtnW(string t, bool icon = false) => Ui.Width(t, Ui.T.Bold) + 26f + (icon ? 25f : 0f);
    const float TaludW = 196f;
    void Options(float W, float H)
    {
        float y = H - 12f - BarH - 8f - 42f;
        if (eraser)
        {
            string gt = "Gum: klik of sleep over vakken om ze leeg te maken"; float gw = Ui.Width(gt, Ui.T.Text) + 32f; var gr = new Rect((W - gw) * 0.5f, y + 4f, gw, 34f); Ui.Panel(gr, false, 17f); Ui.Label(gr, gt, Ui.T.Text, null, TextAnchor.MiddleCenter);
            return;
        }
        if (!HasOptions) return;
        string nm = CurBlock == "weg" ? "Weg" : CurBlock == "pad" ? "Pad" : "Terrein"; float nw = Ui.Width(nm, Ui.T.Bold) + 14f, cw;
        string ow = PlanetCity.DragOneWay ? "Eenrichting" : "Twee richtingen";
        string[] padNames = { "Voetpad", "Fiets- en voetpad" };
        bool useBrush = terrMode <= 3;   // het penseel doet alleen iets bij ophogen, afgraven, egaliseren en gladstrijken
        float segW = 0f;
        if (CurBlock == "weg") { segW = 300f; cw = segW + 6f + BtnW(ow) + 6f + BtnW("Ongelijkvloers") + 6f + BtnW("Lichten", true) + 10f + TaludW; }
        else if (CurBlock == "pad") { segW = 260f; cw = segW + 6f + BtnW("Ongelijkvloers") + 10f + TaludW; }
        else { foreach (var t in TerrModes) segW += Ui.Width(t, Ui.T.Bold) + 44f; cw = segW + (useBrush ? 6f + BtnW("Penseel 37") : 0f); }
        float w = 12f + nw + cw + 6f; var r = new Rect((W - w) * 0.5f, y, w, 42f); Ui.Panel(r, false, 12f);
        Ui.Label(new Rect(r.x + 14f, r.y, nw, r.height), nm, Ui.T.Bold, Ui.Signal); float x = r.x + 12f + nw, by = r.y + 5f;
        if (CurBlock == "weg")
        {
            int s = Ui.Seg(new Rect(x, by, segW, 32f), StyleNames, PlanetCity.DragStyle); if (s >= 0 && s != PlanetCity.DragStyle) { PlanetCity.DragStyle = (byte)s; SetVariant(StyleIds[s]); }
            Ui.Tip(new Rect(x, by, segW, 32f), "Wegtype", "Voor de weg die je nu sleept", "B"); x += segW + 6f;
            if (Ui.Button(new Rect(x, by, BtnW(ow), 32f), ow, PlanetCity.DragOneWay)) ToggleOneWay();
            Ui.Tip(new Rect(x, by, BtnW(ow), 32f), "Eenrichting", "Rijrichting is de sleeprichting", "I"); x += BtnW(ow) + 6f;
            if (Ui.Button(new Rect(x, by, BtnW("Ongelijkvloers"), 32f), "Ongelijkvloers", PlanetCity.DragUnder)) ToggleUnder();
            Ui.Tip(new Rect(x, by, BtnW("Ongelijkvloers"), 32f), "Ongelijkvloers", "Onder kruisende wegen en paden door; uit: een kruispunt of oversteek", "U"); x += BtnW("Ongelijkvloers") + 6f;
            if (Ui.Button(new Rect(x, by, BtnW("Lichten", true), 32f), "Lichten", PlanetCity.Lights, "licht")) ToggleLights();
            Ui.Tip(new Rect(x, by, BtnW("Lichten", true), 32f), "Verkeerslichten", "Op alle kruispunten aan of uit"); x += BtnW("Lichten", true) + 10f;
            Talud(new Rect(x, by, TaludW, 32f));
        }
        else if (CurBlock == "pad")
        {
            int s = Ui.Seg(new Rect(x, by, segW, 32f), padNames, PlanetCity.DragBike ? 1 : 0); if (s >= 0 && (s == 1) != PlanetCity.DragBike) ToggleBike(); x += segW + 6f;
            if (Ui.Button(new Rect(x, by, BtnW("Ongelijkvloers"), 32f), "Ongelijkvloers", PlanetCity.DragUnder)) ToggleUnder();
            Ui.Tip(new Rect(x, by, BtnW("Ongelijkvloers"), 32f), "Ongelijkvloers", "Onder kruisende wegen door; uit: een oversteek", "U"); x += BtnW("Ongelijkvloers") + 10f;
            Talud(new Rect(x, by, TaludW, 32f));
        }
        else
        {
            int s = Ui.SegIcon(new Rect(x, by, segW, 32f), TerrModes, TerrIcons, terrMode); if (s >= 0 && s != terrMode) { terrMode = s; SetVariant("t:" + s); Say("Terrein: " + TerrModes[s] + " · " + TerrHints[s]); } x += segW + 6f;
            if (useBrush && Ui.Button(new Rect(x, by, BtnW("Penseel 37"), 32f), "Penseel " + BrushSize(brush))) brush = (brush + 1) % 4;
            if (useBrush) Ui.Tip(new Rect(x, by, BtnW("Penseel 37"), 32f), "Penseel", "1, 7, 19 of 37 vakken tegelijk", "[ ]");
        }
    }
    // rechts: de laagkiezer (zoals de webversie): op welk niveau de weg of het pad komt, van +2 bovenaan tot −2 onderaan
    void LayerPick(float W, float H)
    {
        const float bw = 50f, bh = 26f; float h = 24f + 9f * (bh + 2f) + 22f;
        var r = new Rect(W - G - bw - 8f, Mathf.Max(70f, (H - h) * 0.5f - 20f), bw + 8f, h); Ui.Panel(r, false, 12f);
        Ui.Label(new Rect(r.x, r.y + 4f, r.width, 18f), "NIVEAU", Ui.T.Tiny, null, TextAnchor.MiddleCenter, 9);
        float y = r.y + 24f; int cur = PlanetCity.DragLevel;
        for (int lv = 4; lv >= -4; lv--)
        {
            var b = new Rect(r.x + 4f, y, bw, bh); y += bh + 2f;
            if (Ui.Button(b, lv == 0 ? "0" : LevelName(lv), lv == cur, null, false, 8f) && lv != cur) StepLevel(lv - cur);
            if (b.Contains(Ui.Mouse))   // uitleg links ernaast
            {
                string t = lv == 0 ? "Op het maaiveld" : Meters(lv) + (lv > 0 ? " hoger, op een dijk" : " lager, in een geul");
                float tw = Ui.Width(t, Ui.T.Bold) + 24f; var tr = new Rect(r.x - 8f - tw, b.center.y - 14f, tw, 28f); Ui.Panel(tr, true, 14f); Ui.Label(tr, t, Ui.T.Bold, null, TextAnchor.MiddleCenter);
            }
        }
        Ui.Label(new Rect(r.x, y, r.width, 18f), "PgUp/Dn", Ui.T.Tiny, null, TextAnchor.MiddleCenter, 9);
    }
    // in de keuzebalk: de hoek van de kant langs een weg of pad op een niveau (20°–70°), om te kijken hoe het terrein afloopt
    void Talud(Rect r)
    {
        if (slopeDeg < 0f) slopeDeg = Mathf.Round(Mathf.Atan(PlanetCity.FieldK) * Mathf.Rad2Deg); string t = "Talud " + Mathf.RoundToInt(slopeDeg) + "°";
        Ui.Label(new Rect(r.x, r.y, 76f, r.height), t, Ui.T.Bold, Ui.InkDim, TextAnchor.MiddleLeft);
        slopeDeg = 20f + Mathf.Round(Ui.Slider(new Rect(r.x + 78f, r.y + 8f, r.width - 86f, r.height - 16f), (slopeDeg - 20f) / 50f) * 50f);
        float k = Mathf.Tan(slopeDeg * Mathf.Deg2Rad);   // het terrein volgt hooguit 8 keer per seconde, het schuifje meteen
        if (Mathf.Abs(k - PlanetCity.FieldK) > 1e-4f && Time.unscaledTime > slopeT) { slopeT = Time.unscaledTime + 0.12f; PlanetCity.SetFieldK(k); }
    }
    float slopeT, slopeDeg = -1f;
    // ---- schoonheid vooraf: wat het gekozen blok op het vak onder de muis met de schoonheid doet, nog voor je klikt.
    // Het blok wordt even in de stapel gezet, de score opnieuw berekend en alles teruggezet. Wegen niet: een los wegvak heeft nog
    // geen verbindingen en zou als "los" tellen.
    bool ShowPreview => !eraser && !TerrTool && CurBlock != "weg" && !painting && lineStart < 0 && !wheelOpen && !catOpen;
    int beautyVer, beforeVer = -1; Beauty.Result before; string prevKey, prevWhy, prevErr; float prevT, prevDelta; bool prevOk; int prevFloor, prevMax;
    void BeautyPreview()
    {
        string type = CurBlock, key = hover + "|" + type + "|" + beautyVer;
        if (key == prevKey || Time.unscaledTime < prevT) return;
        prevKey = key; prevT = Time.unscaledTime + 0.1f; prevOk = false; prevErr = PlanetCity.CanPlace(hover, type); prevFloor = 0;
        if (prevErr != null) { prevOk = true; return; }   // kan niet: de reden al vóór de klik (het voorbeeld is dan rood)
        var cells = PlanetCity.Cells; bool had = cells.TryGetValue(hover, out var l);
        var nl = had ? new List<string>(l) : new List<string>();
        if (type == "pad") { if (nl.Count == 1 && nl[0] == "pad") return; if (nl.Count > 0) return; nl.Add("pad"); }
        else { if (type == "water" || type == "meer" || (nl.Count > 0 && (nl[nl.Count - 1] == "water" || nl[nl.Count - 1] == "meer"))) nl.Clear(); nl.Add(type); }
        bool half = nl.Count > 0 && nl[0] == "wegdeel"; prevMax = half ? 7 : 8; prevFloor = type == "water" || type == "meer" || type == "pad" ? 0 : nl.Count - (half ? 1 : 0);   // stapelen: de hoeveelste laag (hooguit 8 blokken)
        if (beforeVer != beautyVer || before == null) { before = Beauty.Compute(); beforeVer = beautyVer; }
        Beauty.Result after;
        cells[hover] = nl; try { after = Beauty.Compute(); } finally { if (had) cells[hover] = l; else cells.Remove(hover); }
        prevDelta = after.raw - before.raw;
        // de twee onderdelen die het meest veranderen
        string a = null, b = null; float da = 0f, db = 0f;
        for (int k = 0; k < after.rows.Count && k < before.rows.Count; k++)
        {
            var r0 = before.rows[k]; var r1 = after.rows[k]; if (r0.v < 0f || r1.v < 0f) continue;
            float d = (r1.v - r0.v) * r1.w; if (Mathf.Abs(d) < 0.0015f) continue;
            if (Mathf.Abs(d) > Mathf.Abs(da)) { b = a; db = da; a = r1.name; da = d; } else if (Mathf.Abs(d) > Mathf.Abs(db)) { b = r1.name; db = d; }
        }
        string Part(string n, float d) => n == null ? null : "<color=" + (d > 0f ? "#7CD992" : "#F07A6A") + ">" + n + (d > 0f ? " +" : " −") + "</color>";
        prevWhy = Part(a, da); if (b != null) prevWhy += "  " + Part(b, db);
        prevOk = true;
    }
    void PreviewLabel()
    {
        if (prevErr != null)
        {
            var mm = Ui.Mouse; float ew = Ui.Width(prevErr, Ui.T.Bold) + 24f; var er = new Rect(Mathf.Min(mm.x + 18f, Ui.W - ew - 8f), mm.y + 14f, ew, 28f);
            Ui.Panel(er, true, 8f); Ui.Label(er, prevErr, Ui.T.Bold, new Color(0.94f, 0.48f, 0.42f), TextAnchor.MiddleCenter); return;
        }
        bool zero = Mathf.Abs(prevDelta) < 0.05f;
        string d = zero ? "±0" : (prevDelta > 0f ? "+" : "−") + Mathf.Abs(prevDelta).ToString("0.0").Replace('.', ',');
        string t = (prevFloor >= 2 ? "Laag " + prevFloor + " van " + prevMax + "   " : "") + "Schoonheid <color=" + (zero ? "#A8B2AB" : prevDelta > 0f ? "#7CD992" : "#F07A6A") + ">" + d + "</color>" + (prevWhy != null ? "   " + prevWhy : "");
        var m = Ui.Mouse; float w = Ui.Width(t, Ui.T.Bold) + 24f; var r = new Rect(Mathf.Min(m.x + 18f, Ui.W - w - 8f), m.y + 14f, w, 28f);
        Ui.Panel(r, true, 8f); Ui.Label(r, t, Ui.T.Bold, null, TextAnchor.MiddleCenter);
    }
    void TerrLabel()
    {
        string t = (Mathf.Round(PlanetCity.G(hover) * 2f) / 2f).ToString("0.0") + " m" + (PlanetCity.Plateau(hover) ? " · bebouwd, blijft liggen" : sculpting ? "" : " · " + TerrHints[terrMode]);
        var m = Ui.Mouse; float w = Ui.Width(t, Ui.T.Text) + 24f; var r = new Rect(m.x + 18f, m.y + 14f, w, 28f);
        Ui.Panel(r, true, 8f); Ui.Label(r, t, Ui.T.Text, null, TextAnchor.MiddleCenter);
    }

    // ---- linksonder: gum en ongedaan maken (zoals de webversie; het raster staat in het tijdpaneel en onder G); daarboven de beeldsnelheid
    void Tools(float H)
    {
        float y = H - 12f - 44f, x = G;
        if (Ui.RoundButton(new Rect(x, y, 44f, 44f), "gum", eraser)) { eraser = !eraser; nameT = Time.time + 1.6f; }
        Ui.Tip(new Rect(x, y, 44f, 44f), "Gum", "Klik of sleep om vakken leeg te maken", "Delete"); x += 50f;
        if (Ui.RoundButton(new Rect(x, y, 44f, 44f), "ongedaan", false, undo.Count == 0)) Undo();
        Ui.Tip(new Rect(x, y, 44f, 44f), "Ongedaan maken", undo.Count == 0 ? "Nog niets om terug te draaien" : null, "Ctrl+Z");
        string f = Mathf.RoundToInt(fps) + " fps";   // mensen en auto's staan in het schoonheidspaneel
        var fr = new Rect(G, y - 8f - 20f, Ui.Width(f, Ui.T.Bold, 10) + 16f, 20f); Ui.Panel(fr, false, 10f); Ui.Label(fr, f, Ui.T.Bold, Ui.InkDim, TextAnchor.MiddleCenter, 10);
    }

    // ---- rechtsonder: bouwen of lopen (L), daarboven ? voor alle toetsen
    void ModeSwitch(float W, float H)
    {
        float b1 = 14f + 17f + 6f + Ui.Width("Bouwen", Ui.T.Bold, 14) + 14f, b2 = 14f + 17f + 6f + Ui.Width("Lopen", Ui.T.Bold, 14) + 14f;
        var r = new Rect(W - G - (b1 + b2 + 10f), H - 12f - 44f, b1 + b2 + 10f, 44f); Ui.Panel(r, false, 22f);
        var r1 = new Rect(r.x + 4f, r.y + 4f, b1, 36f); var r2 = new Rect(r1.xMax + 2f, r.y + 4f, b2, 36f);
        if (Ui.Button(r1, "Bouwen", !walking, "huis", false, 18f) && walking) ToggleWalk();
        if (Ui.Button(r2, "Lopen", walking, "lopen", false, 18f) && !walking) ToggleWalk();
        Ui.Tip(walking ? r1 : r2, walking ? "Terug naar bouwen" : "Lopen", walking ? null : "Loop door je eigen straten", "L");
        var hr = new Rect(W - G - 38f, r.y - 12f - 38f, 38f, 38f);
        if (Ui.Button(hr, "?", helpOpen)) ToggleHelp();   // alle toetsen
        Ui.Tip(hr, "Bediening", "Alle toetsen op een rij", "?");
    }

    // ---- schoonheid: in het midden, met een zeshoekige score, een balk per onderdeel en een tip (zoals de webversie)
    void ScorePanel(float W, float H)
    {
        Ui.DimBack(0.25f);
        if (beauty == null) beauty = Beauty.Compute();
        float w = 420f, h = 160f + beauty.rows.Count * 60f + 70f; var r = new Rect((W - w) * 0.5f, Mathf.Max(20f, (H - h) * 0.5f), w, h); Ui.Panel(r, true, 16f);
        float x = r.x + 18f, iw = w - 36f, y = r.y + 14f;
        Ui.Tex(new Rect(x, y + 5f, 18f, 18f), Ui.Icon("raster"), Ui.Ink); Ui.Label(new Rect(x + 28f, y, 200f, 28f), "SCHOONHEID", Ui.T.Title);
        if (Ui.RoundButton(new Rect(r.xMax - 18f - 30f, y - 1f, 30f, 30f), "sluit")) scoreOpen = false; y += 42f;
        Ui.Tex(new Rect(x, y, 66f, 74f), Ui.Hex(Ui.SlotOn, Ui.Signal, 2f, true));
        Ui.Label(new Rect(x, y, 66f, 74f), beauty.score > 0 ? beauty.score.ToString() : "–", Ui.T.Big, Ui.Signal, TextAnchor.MiddleCenter);
        Ui.Label(new Rect(x + 82f, y + 14f, iw - 82f, 22f), beauty.label, Ui.T.Bold, null, null, 15);
        int people = PlanetLife.I != null ? PlanetLife.I.People.Count : 0, cars = PlanetLife.I != null ? PlanetLife.I.CarCount : 0;
        Ui.Label(new Rect(x + 82f, y + 38f, iw - 82f, 18f), beauty.blocks + " blokken · " + people + " mensen en " + cars + " auto's op straat", Ui.T.Dim);
        y += 88f; Ui.Label(new Rect(x, y, iw, 16f), "ONDERDELEN", Ui.T.Tiny); y += 20f;
        for (int k = 0; k < beauty.rows.Count; k++)
        {
            var row = beauty.rows[k]; var c = new Rect(x, y, iw, 54f); Ui.Button(c, null, false, null, false, 10f, true); bool none = row.v < 0f; Color ink = none ? Ui.InkDim : Ui.Ink;
            string ic = RowIcon(row.name); Ui.Tex(new Rect(c.x + 10f, c.y + 9f, 16f, 16f), Ui.Icon(ic), ink);
            Ui.Label(new Rect(c.x + 34f, c.y + 6f, 200f, 20f), row.name, Ui.T.Bold, ink); Ui.Label(new Rect(c.x, c.y + 6f, c.width - 10f, 20f), none ? "–" : Mathf.RoundToInt(row.v * 100f).ToString(), Ui.T.Bold, ink, TextAnchor.MiddleRight);
            var bar0 = new Rect(c.x + 10f, c.y + 29f, c.width - 20f, 4f); Ui.Tex(bar0, Texture2D.whiteTexture, new Color(1f, 1f, 1f, 0.1f)); if (!none) Ui.Tex(new Rect(bar0.x, bar0.y, bar0.width * Mathf.Clamp01(row.v), 4f), Texture2D.whiteTexture, Ui.Signal);
            Ui.Label(new Rect(c.x + 10f, c.y + 35f, c.width - 20f, 16f), row.detail, Ui.T.Dim, null, null, 11);
            y += 60f;
        }
        var tr = new Rect(x, y + 4f, iw, 52f); Ui.Tex(tr, Texture2D.whiteTexture, new Color(Ui.Signal.r, Ui.Signal.g, Ui.Signal.b, 0.12f));
        Ui.Tex(new Rect(tr.x + 10f, tr.y + 17f, 18f, 18f), Ui.Icon("licht"), Ui.Signal);
        var hs = new Rect(tr.x + 36f, tr.y + 4f, tr.width - 46f, tr.height - 8f); Ui.Label(hs, beauty.tip, Ui.T.Help, null, TextAnchor.MiddleLeft);
        if (Ui.DimClick()) scoreOpen = false;
    }
    static string RowIcon(string n) => n == "Groen" ? "bewolkt" : n == "Bereikbaar" ? "huis" : n == "Wegennet" ? "kompas" : n == "Afwisseling" ? "stad" : n == "Levendig" ? "lopen" : n == "Water" ? "regen" : n == "Voorzieningen" ? "stad" : n == "Blikvangers" ? "licht" : "raster";

    // ---- het menu: in het midden, met kaarten per groep (zoals de webversie)
    void Menu(float W, float H)
    {
        Ui.DimBack(0.25f);
        float w = 440f, h = 756f; var r = new Rect((W - w) * 0.5f, Mathf.Max(16f, (H - h) * 0.5f), w, h); Ui.Panel(r, true, 16f);
        float x = r.x + 18f, iw = w - 36f, y = r.y + 14f, half = (iw - 8f) * 0.5f;
        Ui.Tex(new Rect(x, y + 3f, 20f, 23f), Ui.Hex(Ui.Signal, Ui.Signal, 0f, true)); Ui.Label(new Rect(x + 30f, y, 200f, 28f), "MENU", Ui.T.Title);
        if (Ui.RoundButton(new Rect(r.xMax - 18f - 30f, y - 1f, 30f, 30f), "sluit")) menuOpen = false; y += 40f;
        int blocks = beauty != null ? beauty.blocks : PlanetCity.Cells.Count;
        Ui.CardButton(new Rect(x, y, iw, 50f), "Stad " + PlanetCity.Slot, blocks + " blokken · stad nu open", true, "stad"); y += 56f;
        Ui.Label(new Rect(x, y, iw, 18f), "Je stad wordt vanzelf bewaard. Een andere stad openen verandert er niets aan.", Ui.T.Dim, null, null, 11); y += 28f;
        System.Action<string> section = s => { Ui.Label(new Rect(x, y, iw, 16f), s, Ui.T.Tiny); y += 20f; };
        section("STEDEN");
        for (int k = 1; k <= 3; k++)
        {
            var c = new Rect(x + ((k - 1) % 2) * (half + 8f), y + ((k - 1) / 2) * 56f, half, 48f);
            if (Ui.CardButton(c, "Stad " + k, k == PlanetCity.Slot ? "nu open" : PlanetCity.SlotExists(k) ? "bewaard" : "nog leeg", false, "stad") && k != PlanetCity.Slot) { SwitchSlot(k); menuOpen = false; }
        }
        if (Ui.CardButton(new Rect(x + half + 8f, y + 56f, half, 48f), "Lege bouwgrond", "Zelf vanaf nul (Ctrl+Z terug)", false, "leeg")) { Empty(); menuOpen = false; }
        y += 2 * 56f + 8f;
        section("GEREEDSCHAP");
        if (Ui.CardButton(new Rect(x, y, half, 48f), "Voorbeeldstad hier", "Ringweg, huizen en een meer", false, "huis")) { Sample(); menuOpen = false; }
        if (Ui.CardButton(new Rect(x + half + 8f, y, half, 48f), "Naar het midden", "Camera terug naar je stad (H)", false, "kompas")) { GoCentre(); menuOpen = false; }
        if (Ui.CardButton(new Rect(x, y + 56f, half, 48f), "Naar de polen", "Noord- en zuidpool (N)", false, "kompas")) { GoPole(); menuOpen = false; }
        if (Ui.CardButton(new Rect(x + half + 8f, y + 56f, half, 48f), "Startstad", "Opnieuw beginnen rond een vlag", false, "stad")) { StartTown(); menuOpen = false; }
        if (Ui.CardButton(new Rect(x, y + 112f, iw, 48f), "Hele planeet vol", "Stadjes overal, platteland ertussen (Ctrl+Z terug)", false, "stad")) { FillPlanet(); menuOpen = false; }
        y += 3 * 56f + 8f;
        section("LEVEN EN INSTELLINGEN");
        if (Ui.CardButton(new Rect(x, y, half, 48f), "Mensen", PlanetLife.PeopleOn ? "aan" : "uit", PlanetLife.PeopleOn, "mensen")) PlanetLife.PeopleOn = !PlanetLife.PeopleOn;
        if (Ui.CardButton(new Rect(x + half + 8f, y, half, 48f), "Auto's", PlanetLife.CarsOn ? "aan" : "uit", PlanetLife.CarsOn, "auto")) PlanetLife.CarsOn = !PlanetLife.CarsOn;
        if (Ui.CardButton(new Rect(x, y + 56f, half, 48f), "Verkeerslichten", PlanetCity.Lights ? "aan" : "uit", PlanetCity.Lights, "licht")) ToggleLights();
        if (Ui.CardButton(new Rect(x + half + 8f, y + 56f, half, 48f), "Geluid", Babble.Muted ? "uit" : "aan", !Babble.Muted, Babble.Muted ? "stil" : "geluid")) ToggleSound();
        if (Ui.CardButton(new Rect(x, y + 112f, half, 48f), "Bediening", "Alle toetsen op een rij (?)", false, "boek")) { helpOpen = true; menuOpen = false; }
        if (Ui.CardButton(new Rect(x + half + 8f, y + 112f, half, 48f), "Welkom", "Het beginscherm", false, "zon")) { welcome = true; menuOpen = false; }
        if (Ui.CardButton(new Rect(x, y + 168f, half, 48f), "Totale chaos", PlanetLife.Chaos ? "racers, ontwijkers en jagers" : "uit: netjes verkeer", PlanetLife.Chaos, "auto")) { PlanetLife.Chaos = !PlanetLife.Chaos; Say(PlanetLife.Chaos ? "Totale chaos: racers, ontwijkers en jagers op de weg" : "Netjes verkeer"); }
        var sz = new Rect(x + half + 8f, y + 168f, half, 48f); Ui.Label(new Rect(sz.x + 2f, sz.y, sz.width, 16f), "INTERFACEGROOTTE", Ui.T.Tiny);
        float[] sizes = { 0.9f, 1f, 1.15f, 1.3f }; int si = 0; for (int k = 1; k < 4; k++) if (Mathf.Abs(sizes[k] - Ui.Size) < Mathf.Abs(sizes[si] - Ui.Size)) si = k;
        int hs = Ui.Seg(new Rect(sz.x, sz.y + 18f, sz.width, 30f), new[] { "90%", "100%", "115%", "130%" }, si); if (hs >= 0 && hs != si) Ui.Size = sizes[hs];
        y += 4 * 56f + 4f;
        Ui.Label(new Rect(x, y, iw, 16f), "Ware grootte: een vak is 15,6 m breed, een verdieping 3 m, de planeet 3,8 km rond.", Ui.T.Dim, null, null, 11);
        if (Ui.DimClick()) menuOpen = false;
    }

    // ---- bediening: alle toetsen en de muis op een rij, in twee kolommen (zoals het venster ? van de webversie, 15b-sneltoetsen.js)
    // "#Kop" is een kopje; anders "toetsen|uitleg". In de toetsen is elk woord met een hoofdletter of een teken een toets,
    // een woord met een kleine letter (muis, wiel, slepen) gewone tekst, en "/" betekent "of".
    static readonly string[] HelpLeft = {
        "#Camera",
        "W A S D / pijltjes|schuiven (Shift: sneller)",
        "Q E|draaien",
        "+ - / wiel|zoomen",
        "rechts slepen / Shift+slepen|schuiven",
        "midden slepen / Alt+slepen|draaien en kantelen",
        "H|naar het midden van je stad",
        "N|naar de noordpool, nog eens: zuidpool",
        "K|naar de andere kant van de bol",
        "#Bouwen",
        "1–6 / Shift+wiel|categorie; nog eens: volgende blok",
        "Tab vasthouden|keuzewiel bij de muis",
        "Tab tikken|lade met alle blokken",
        "middelste klik|pipet: neem het blok van een vak over",
        "klik / slepen|bouwen",
        "rechts klik|vak leegmaken",
        "X|bovenste blok weghalen",
        "Delete|gum aan en uit",
        "Ctrl+Z|ongedaan maken",
        "#Wegen en paden",
        "slepen|een rechte lijn in 12 richtingen",
        "B|weg: gewone weg, brede weg, laan; pad: voetpad of fiets- en voetpad",
        "I|eenrichting",
        "U|ongelijkvloers (weg of pad): onder kruisende wegen en paden door; uit: kruispunt of oversteek",
        "PgUp PgDn|niveau: stappen van 2,25 m (ook + - wiel bij slepen)",
        "#Terrein",
        "slepen|boetseren (ingedrukt houden)",
        "Shift+slepen|andersom",
        "T|ophogen, afgraven, egaliseren, glad, helling",
        "[ ]|penseel: 1, 7, 19 of 37 vakken",
    };
    static readonly string[] HelpRight = {
        "#Cameramodus",
        "Spatie|aan en uit (of Esc)",
        "muis|rondkijken",
        "W A S D|vliegen",
        "E Q|omhoog en omlaag",
        "Shift / Ctrl|sneller of langzamer",
        "wiel|inzoomen",
        "F|foto, in Afbeeldingen\\Raatstad",
        "#Lopen",
        "L|lopen, en terug naar bouwen",
        "W A S D|lopen (Shift: rennen)",
        "Spatie|springen",
        "muis|rondkijken (klik om te vangen)",
        "Esc|muis loslaten",
        "#Altijd",
        "P|tijd stilzetten en verder",
        "O|ander weer (Shift+O: vorige)",
        "G|raster aan en uit",
        "M|menu",
        "?|dit overzicht",
        "Esc|venster sluiten",
    };
    static float HelpHeight(string[] rows) { float h = 0f; for (int k = 0; k < rows.Length; k++) h += rows[k][0] == '#' ? (k > 0 ? 30f : 22f) : 21f; return h; }
    void Help(float W, float H)
    {
        Ui.DimBack(0.25f);
        float w = 900f, colW = (w - 36f - 28f) * 0.5f, h = 54f + Mathf.Max(HelpHeight(HelpLeft), HelpHeight(HelpRight)) + 14f;
        var r = new Rect((W - w) * 0.5f, Mathf.Max(16f, (H - h) * 0.5f), w, h); Ui.Panel(r, true, 16f);
        float x = r.x + 18f, y = r.y + 14f;
        Ui.Tex(new Rect(x, y + 5f, 18f, 18f), Ui.Icon("boek"), Ui.Ink); Ui.Label(new Rect(x + 28f, y, 200f, 28f), "BEDIENING", Ui.T.Title);
        if (Ui.RoundButton(new Rect(r.xMax - 18f - 30f, y - 1f, 30f, 30f), "sluit")) helpOpen = false; y += 40f;
        HelpColumn(HelpLeft, x, y, colW); HelpColumn(HelpRight, x + colW + 28f, y, colW);
        if (Ui.DimClick()) helpOpen = false;
    }
    static void HelpColumn(string[] rows, float x, float y, float w)
    {
        const float keysW = 178f;
        for (int k = 0; k < rows.Length; k++)
        {
            string row = rows[k];
            if (row[0] == '#') { if (k > 0) y += 8f; Ui.Label(new Rect(x, y, w, 16f), row.Substring(1).ToUpper(), Ui.T.Tiny); y += 22f; continue; }
            int bar = row.IndexOf('|'); float kx = x;
            foreach (string tok in row.Substring(0, bar).Split(' '))
            {
                if (tok == "/") { kx = HelpText("of", kx, y); continue; }
                string[] parts = tok.Length > 1 ? tok.Split('+') : new[] { tok };
                for (int q = 0; q < parts.Length; q++)
                {
                    if (q > 0) kx = HelpText("+", kx, y);
                    string pt = parts[q];
                    if (pt.Length > 1 && char.IsLower(pt[0])) kx = HelpText(pt, kx, y);
                    else { float kw = Mathf.Max(20f, Ui.Width(pt, Ui.T.Kbd) + 12f); Ui.Kbd(new Rect(kx, y + 1f, kw, 18f), pt); kx += kw + 4f; }
                }
            }
            Ui.Label(new Rect(x + keysW, y, w - keysW, 20f), row.Substring(bar + 1), Ui.T.Text, null, TextAnchor.MiddleLeft);
            y += 21f;
        }
    }
    static float HelpText(string t, float x, float y) { float tw = Ui.Width(t, Ui.T.Dim); Ui.Label(new Rect(x, y, tw + 2f, 20f), t, Ui.T.Dim, null, TextAnchor.MiddleLeft); return x + tw + 5f; }

    // ---- welkom: bij het starten (uit te zetten)
    void Welcome(float W, float H)
    {
        Ui.DimBack(0.3f);
        float w = 440f, h = 420f; var r = new Rect((W - w) * 0.5f, (H - h) * 0.5f, w, h); Ui.Panel(r, true, 16f);
        float x = r.x + 20f, iw = w - 40f, y = r.y + 18f;
        Ui.Tex(new Rect(x, y + 2f, 30f, 34f), Ui.Hex(Ui.Signal, Ui.Signal, 0f, true)); Ui.Label(new Rect(x + 42f, y - 2f, 300f, 30f), "RAATSTAD", Ui.T.Title, null, null, 26);
        Ui.Label(new Rect(x + 42f, y + 24f, iw - 42f, 16f), "Bouw een stad van zeshoeken op een planeet en wandel erdoorheen", Ui.T.Dim, null, null, 11); y += 56f;
        Ui.Label(new Rect(x, y, iw, 112f),
            "•  <b>Bouwen</b>: kies onderin een categorie en tik op een vak. Huizen, winkels, parken en wegen sluiten vanzelf op elkaar aan.\n" +
            "•  <b>Lopen</b>: knop rechtsonder (L): loop door je eigen straten.\n" +
            "•  <b>Leven</b>: auto's rijden, mensen wandelen en praten over je stad.", Ui.T.Help); y += 118f;
        int blocks = beauty != null ? beauty.blocks : PlanetCity.Cells.Count;
        if (Ui.CardButton(new Rect(x, y, iw, 54f), "Verder bouwen", "Stad " + PlanetCity.Slot + " · " + blocks + " blokken", true, "huis")) welcome = false; y += 62f;
        float half = (iw - 8f) * 0.5f;
        if (Ui.CardButton(new Rect(x, y, half, 48f), "Startstad", null, false, "stad")) { StartTown(); welcome = false; }
        if (Ui.CardButton(new Rect(x + half + 8f, y, half, 48f), "Lege bouwgrond", null, false, "leeg")) { Empty(); welcome = false; }
        y += 60f;
        bool off = PlayerPrefs.GetInt("raatstad-welkom-uit", 0) == 1; var cb = new Rect(x, y, 16f, 16f);
        bool flip = Ui.Button(cb, null, off, off ? "sluit" : null, false, 4f); Ui.Label(new Rect(x + 24f, y - 2f, iw - 24f, 20f), "Niet meer tonen bij het starten", Ui.T.Dim);
        flip |= GUI.Button(new Rect(x + 20f, y - 2f, 220f, 20f), GUIContent.none, GUIStyle.none);
        if (flip) PlayerPrefs.SetInt("raatstad-welkom-uit", off ? 0 : 1);
        if (Ui.DimClick()) welcome = false;
    }

    // ---- pijl naar het midden van de stad als je er ver vandaan bent
    void CentreArrow(float W, float H)
    {
        CityCentre(); if (!hasCentre) return;
        Vector3 here = (walking ? walker.transform.position : focus * Planet.R).normalized;
        float d = Mathf.Acos(Mathf.Clamp(Vector3.Dot(here, centre), -1f, 1f)) * Planet.R; if (d <= 120f) return;
        Vector3 sp = Cam.WorldToScreenPoint(centre * (Planet.R + 20f)) / Ui.S; Vector2 c2 = new Vector2(W / 2f, H / 2f);
        Vector2 dir = (new Vector2(sp.x, sp.y) - c2).normalized; if (sp.z < 0f) dir = -dir;
        bool onScreen = sp.z > 0f && sp.x > 0 && sp.x < W && sp.y > 0 && sp.y < H;
        Vector2 pos = onScreen ? new Vector2(sp.x, H - sp.y) : c2 + new Vector2(dir.x, -dir.y) * Mathf.Min(W, H) * 0.38f;
        float ang = Mathf.Atan2(-dir.y, dir.x) * Mathf.Rad2Deg;
        var m = GUI.matrix; GUIUtility.RotateAroundPivot(ang, pos * Ui.S);
        Ui.Tex(new Rect(pos.x - 14f, pos.y - 14f, 28f, 28f), Ui.Icon("pijl"), Ui.Signal); GUI.matrix = m;
        string t = "Centrum · " + Mathf.RoundToInt(d) + " m"; float w = Ui.Width(t, Ui.T.Bold) + 22f; var cr = new Rect(pos.x - w * 0.5f, pos.y + 16f, w, 26f);
        Ui.Panel(cr, false, 13f); Ui.Label(cr, t, Ui.T.Bold, null, TextAnchor.MiddleCenter);
    }
}
