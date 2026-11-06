using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Het startscherm: bij Play eerst dit, met de planeet die vanuit de ruimte langzaam rond draait. Links de knoppen (Verder,
// Nieuwe stad, Stad openen, Spelmodus, Instellingen, Afsluiten), rechts ervan het venster van de gekozen knop.
// Openen en nieuw gaan zoals Hele planeet vol: blauwdruk en het laadpaneel onderaan met de echte voortgang (FillSteps, RebuildSteps).
// In het spel: Opslaan, Opslaan als en Naar het startscherm (menu), en elke 3 minuten vanzelf opslaan (CityFiles).
public partial class PlanetBuilder
{
    public static bool SkipHome;   // voor automatische tests: meteen in het spel, zonder startscherm
    bool home = !SkipHome;
    enum Page { Main, New, Open, Modes, Settings }
    Page page;
    int dialog;   // in het spel: 1 opslaan als, 2 eerst opslaan voor het startscherm?
    bool HomeModal => home || dialog != 0;
    string nameEdit = ""; int newKind = 1; int listOfs; string delAsk;   // delAsk: de stad waarvan gevraagd wordt of hij weg mag
    // laden
    bool busy;
    // opslaan
    const float AutoSaveEvery = 180f;
    float autoT, thumbAt = -1f; int savedHash; bool savedKnown;

    const float HomeDist = 2050f;

    // in Start: het startscherm op, behalve als er een automatische test draait (LevelTest, AiTest, TunnelTest: die bouwen zelf)
    void HomeBoot()
    {
        foreach (var m in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)) if (m.GetType().Name.EndsWith("Test")) home = false;
        if (!home) return;
        welcome = false; dist = HomeDist; CityFiles.Scan();
    }

    // ---------- Update ----------
    // true: Update stopt hier (startscherm, of een venster van het opslaan open)
    bool HomeUpdate()
    {
        if (home)
        {
            if (!busy)
            {
                if (Inp.Down(K.Esc)) page = Page.Main;
                focus = (Quaternion.AngleAxis(-3.5f * Time.deltaTime, Planet.Axis) * focus).normalized; distGoal = HomeDist; CameraControl();
                Cam.transform.rotation = Geo.Look(-Cam.transform.position, Cam.transform.up);   // recht naar het middelpunt van de bol: de planeet precies in het midden
                PlanetCity.ShowGrid(false);   // op het startscherm geen raster (bij het openen van een stad komt het terug)
            }
            return true;
        }
        if (dialog != 0) { if (Inp.Down(K.Esc)) { dialog = 0; homeToNew = false; } return true; }
        if (loading != null) return false;
        if (thumbAt > 0f && Time.time > thumbAt && CityFiles.Current != null) { thumbAt = -1f; var t = Shot(); CityFiles.SaveThumb(CityFiles.Current, t); Destroy(t); }
        if (Time.time - autoT > AutoSaveEvery && !flying) { autoT = Time.time; if (SaveCity(true)) Say("Stad vanzelf opgeslagen"); }
        return false;
    }

    // ---------- opslaan ----------
    // de stad bewaren als CityFiles.Current; onlyIfChanged: alleen als er iets veranderd is sinds de vorige keer
    bool SaveCity(bool onlyIfChanged)
    {
        if (CityFiles.Current == null || busy) return false;
        string json = PlanetCity.Snapshot(); int h = json.GetHashCode();
        if (onlyIfChanged && savedKnown && h == savedHash) return false;
        Texture2D t = walking ? null : Shot();   // lopend: het oude voorbeeld houden (dat is van bovenaf)
        bool ok = CityFiles.Save(CityFiles.Current, t, json); if (t != null) Destroy(t);
        if (ok) { savedHash = h; savedKnown = true; }
        return ok;
    }
    void SaveAs(string name)
    {
        var c = CityFiles.Create(name); CityFiles.Current = c; savedKnown = false;
        if (SaveCity(false)) Say("Opgeslagen als " + c.name); else Say("Opslaan lukte niet");
    }
    void OnApplicationQuit() { if (!home && CityFiles.Current != null) SaveCity(true); }

    // een kleine schermafbeelding van wat de camera ziet, zonder interface
    Texture2D Shot()
    {
        const int w = 320, h = 180;
        var rt = RenderTexture.GetTemporary(w, h, 24); var old = Cam.targetTexture; var oldActive = RenderTexture.active;
        Cam.targetTexture = rt; Cam.Render(); Cam.targetTexture = old;
        RenderTexture.active = rt; var t = new Texture2D(w, h, TextureFormat.RGB24, false); t.ReadPixels(new Rect(0, 0, w, h), 0, 0); t.Apply();
        RenderTexture.active = oldActive; RenderTexture.ReleaseTemporary(rt); return t;
    }

    // ---------- naar het startscherm en terug ----------
    bool homeToNew;   // Nieuwe wereld (raatmenu): na het opslaan meteen Nieuwe stad op het startscherm
    void GoHome()
    {
        dialog = 0; menuOpen = helpOpen = scoreOpen = welcome = todOpen = catOpen = wheelOpen = modesOpen = false; eraser = false;
        if (aiOn) AiStop();
        if (walking) ToggleWalk();
        outline.enabled = false; ShowBrush(-1); ShowGhost(-1); ShowVerts(-1); ShowPlan(null); hasHover = false;
        home = true; page = homeToNew ? Page.New : Page.Main; CityFiles.Scan(); homeT = Time.unscaledTime;
        if (homeToNew) nameEdit = CityFiles.FreeName("Nieuwe stad"); homeToNew = false;   // via Nieuwe wereld: meteen het venster Nieuwe stad
    }

    void OpenCity(CityInfo c) { if (!busy) StartCoroutine(OpenRoutine(c, -1)); }
    void NewCity(string name, int kind) { if (!busy) StartCoroutine(OpenRoutine(CityFiles.Create(name), kind)); }

    // kind -1: openen van schijf; 0 leeg, 1 startstad, 2 hele planeet vol, 3 natuurplaneet, 4 hoogtetest (PlanetHeightTest.cs).
    // Zoals Hele planeet vol (FillRoutine): geen laadscherm, de camera draait om de bol, die als blauwdruk volloopt
    // (PlanetBlueprint); bij het tekenen vervaagt de blauwdruk. Onderaan het laadpaneel (LoadingGui) met de echte voortgang.
    IEnumerator OpenRoutine(CityInfo c, int kind)
    {
        busy = true; loading = kind < 0 ? c.name + " openen" : c.name + " maken"; loadFrac = loadShown = 0f; PlanetBlueprint.I.Show();
        for (float t = 0f; t < 0.6f; t += Time.deltaTime) yield return null;   // eerst de blauwdruk op
        string empty = JsonUtility.ToJson(new PSaveData { m = Planet.M, r = Planet.R });
        bool ok = true; int start = Planet.North;
        if (kind < 0)
        {
            string json = CityFiles.Read(c);
            ok = json != null && PlanetCity.Restore(json);
            if (ok && PlanetCity.StartVer > 0 && PlanetCity.StartVer < PlanetCity.StartTownVersion)   // een ongewijzigde oude startstad: de nieuwe (zoals PlanetCity.Load)
            {
                int s = PlanetCity.StartCell >= 0 && PlanetCity.StartCell < Planet.Cells.Length ? PlanetCity.StartCell : Planet.North;
                PlanetCity.Restore(empty); PlanetCity.StartTown(s);
            }
            if (!ok) PlanetCity.Restore(empty);
            loadFrac = 0.4f;
        }
        else if (kind == 2) yield return PlanetCity.FillSteps((f, what) => { loadFrac = f * 0.4f; loading = what; });
        else if (kind == 3) yield return PlanetCity.WildSteps((f, what) => { loadFrac = f * 0.4f; loading = what; }, Random.Range(1, 100000));   // natuurplaneet
        else if (kind == 4) yield return PlanetCity.HeightTestSteps((f, what) => { loadFrac = f * 0.4f; loading = what; });   // hoogtetest
        else
        {
            PlanetCity.Restore(empty);
            if (kind == 1) PlanetCity.StartTown(start);
            loadFrac = 0.4f;
        }
        for (float t = 0f; t < 0.5f; t += Time.deltaTime) yield return null;   // de blauwdruk van de stad even laten zien
        loading = "Tekenen"; yield return PlanetCity.RebuildSteps(f => { loadFrac = 0.4f + f * 0.6f; PlanetBlueprint.I.Fade(1f - f); });
        PlanetCity.RebuildGrid(); loadFrac = 1f;
        // de camera naar de stad: van ver weg zakt hij erheen
        undo.Clear(); beautyT = -99f; beauty = null; centreT = -99f; CityCentre();
        Vector3 to = kind == 1 || kind == 0 || kind >= 3 || !hasCentre ? Planet.Cells[start].dir : centre;
        focus = to; fwd = Planet.Cells[Planet.Nearest(to)].north; pitch = 48f; distGoal = LandDist;
        if (kind == 4) { pitch = 38f; distGoal = 420f; }   // hoogtetest: van de pool naar het noorden, berg, hoogvlakte en dal in beeld
        CityFiles.MarkOpened(c); savedKnown = false; autoT = Time.time; busy = false;
        if (ok) { if (kind >= 0) { SaveCity(false); thumbAt = Time.time + 5f; } else { savedHash = PlanetCity.Snapshot().GetHashCode(); savedKnown = true; } }   // een nieuwe stad meteen bewaren
        CityFiles.Scan();
        welcome = kind >= 0 && PlayerPrefs.GetInt("raatstad-welkom-uit", 0) == 0;   // de uitleg alleen bij een nieuwe stad
        PlanetBlueprint.I.Hide(); loading = null; home = false; PlanetCity.ShowGrid(PlanetCity.GridOn);   // het raster weer zoals ingesteld
        if (kind >= 0) Sounds.Instead("gen");
        Say(ok ? (kind < 0 ? c.name + " geopend" : c.name + " gemaakt") : "Deze stad kon niet gelezen worden: lege bouwgrond");
    }

    // ---------- interface ----------
    static GUIStyle fieldStyle;
    string NameField(Rect r, string text)
    {
        if (fieldStyle == null || fieldStyle.font == null)
        {
            fieldStyle = new GUIStyle { font = Resources.Load<Font>("Fonts/AtkinsonHyperlegible-Bold"), fontSize = 14, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip, padding = new RectOffset(12, 12, 0, 0) };
            fieldStyle.normal.textColor = fieldStyle.focused.textColor = fieldStyle.hover.textColor = Ui.Ink;
        }
        Ui.Box(r, new Color(0f, 0f, 0f, 0.35f), Ui.Signal, 10f);
        GUI.SetNextControlName("stadsnaam"); string s = GUI.TextField(r, text, 40, fieldStyle);
        if (GUI.GetNameOfFocusedControl() != "stadsnaam") GUI.FocusControl("stadsnaam");
        return s;
    }
    static bool EnterPressed => Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);

    // Het startscherm in de stijl van het raatmenu: links de knoppen als zeshoekige tegels op een onzichtbare boog (Tiles), geen
    // verloop maar tekst met schaduw; rechts ervan het venster van de gekozen knop, ook met zeshoekjes. De planeet draait in het midden.
    float homeT = -99f;
    void HomeGui(float W, float H)
    {
        if (homeT < 0f) homeT = Time.unscaledTime;
        var last = CityFiles.Last; int n = CityFiles.All.Count;
        var items = new List<MItem> {
            new MItem { name = "Verder", sub = last != null ? last.name + " · " + CityFiles.When(last.saved) : "Nog geen stad bewaard", icon = "huis", act = last != null ? () => OpenCity(last) : (System.Action)null },
            new MItem { name = "Nieuwe stad", sub = "Startstad, leeg, natuur of de hele planeet vol", icon = "stad", act = () => { page = page == Page.New ? Page.Main : Page.New; nameEdit = CityFiles.FreeName("Nieuwe stad"); } },
            new MItem { name = "Stad openen", sub = n == 0 ? "Nog geen steden" : n == 1 ? "1 stad bewaard" : n + " steden bewaard", icon = "boek", act = n > 0 ? () => { page = page == Page.Open ? Page.Main : Page.Open; listOfs = 0; } : (System.Action)null },
            new MItem { name = "Spelmodus", sub = Modes[mode].name, icon = "kompas", act = () => page = page == Page.Modes ? Page.Main : Page.Modes },
            new MItem { name = "Instellingen", sub = "Geluid, muziek, interface en verkeer", icon = "zon", act = () => page = page == Page.Settings ? Page.Main : Page.Settings },
            new MItem { name = "Afsluiten", icon = "sluit", act = () => Quit() } };
        int open = page == Page.New ? 1 : page == Page.Open ? 2 : page == Page.Modes ? 3 : page == Page.Settings ? 4 : -1;
        float cy = H * 0.5f + 30f; hexRight = false;
        // dicht bij de planeet: de straal van de bol in beeld uit de camera, de knoppen met hun tekst er net links van
        float d = Cam.transform.position.magnitude, rp = H * 0.5f * Mathf.Tan(Mathf.Asin(Mathf.Clamp01(Planet.R / Mathf.Max(d, Planet.R + 1f)))) / Mathf.Tan(Cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float left = Mathf.Clamp(W * 0.5f - rp - 470f, 76f, W * 0.5f);
        // de naam bovenaan, met een zeshoek ervoor
        float ty = cy - 330f;
        float lx = left - 20f;   // de naam boven de knoppen
        Ui.Tex(new Rect(lx, ty + 4f, 44f, 50f), Ui.Hex(Ui.Signal, Ui.Signal, 0f, true));
        Shadowed(new Rect(lx + 60f, ty - 4f, 360f, 50f), "RAATSTAD", Ui.T.Big, Ui.Ink, 46);
        Shadowed(new Rect(lx + 62f, ty + 40f, 360f, 18f), "Een stad van zeshoeken op een planeet", Ui.T.Dim, new Color(Ui.Ink.r, Ui.Ink.g, Ui.Ink.b, 0.8f), 13);
        int hit = Tiles(items, left, cy, homeT, open, null, 64f, 20, out _, out _, out _, out _);
        if (hit >= 0) items[hit].act?.Invoke();
        var pr = new Rect(Mathf.Min(left + 430f, W - 440f - 24f), cy - 250f, 440f, 0f);   // het venster vlak naast de knoppen (over de planeet heen)
        if (page == Page.New) NewPanel(pr);
        else if (page == Page.Open) OpenPanel(pr, H);
        else if (page == Page.Modes) ModesPanel(pr);
        else if (page == Page.Settings) SettingsPanel(pr);
        Shadowed(new Rect(56f, H - 34f, 500f, 16f), "Ware grootte: een vak is 15,6 m breed, de planeet 3,8 km rond.", Ui.T.Dim, new Color(Ui.Ink.r, Ui.Ink.g, Ui.Ink.b, 0.7f), 11);
    }

    void PanelHead(Rect r, string title, ref float y)
    {
        Ui.Tex(new Rect(r.x + 18f, y + 6f, 16f, 14.4f), Ui.Hex(Ui.Signal, Ui.Signal, 0f));
        Ui.Label(new Rect(r.x + 42f, y, r.width - 94f, 28f), title, Ui.T.Title);
        if (Ui.RoundButton(new Rect(r.xMax - 18f - 30f, y - 1f, 30f, 30f), "sluit")) page = Page.Main;
        y += 42f;
    }
    // een regel met een zeshoekje ervoor (zoals de keuzes in het raatmenu): naam, waarde of uitleg; true als erop geklikt is
    bool HexRow(Rect r, string name, string sub, string value, bool on, bool dim = false)
    {
        bool hv = r.Contains(Ui.Mouse) && !dim; const float TS = 30f;
        var hx = new Rect(r.x + 4f, r.center.y - TS * 0.45f, TS, TS * 0.9f);
        Ui.Tex(hx, Ui.Hex(hv ? Ui.Signal : on ? new Color(Ui.Signal.r, Ui.Signal.g, Ui.Signal.b, 0.35f) : new Color(0.07f, 0.09f, 0.12f, 0.92f), hv || on ? Ui.Signal : new Color(1f, 1f, 1f, dim ? 0.15f : 0.3f), 1.2f));
        float tx = hx.xMax + 12f; Color ink = dim ? Ui.InkDim : hv ? Ui.Signal : Ui.Ink;
        string val = value == null ? "" : "   <color=" + (on || value == "aan" ? "#F2B233" : "#A8B2AB") + ">" + value + "</color>";
        Ui.Label(new Rect(tx, sub != null ? r.y + 4f : r.y, r.width - tx + r.x, sub != null ? 20f : r.height), name + val, Ui.T.Bold, ink, null, 14);
        if (sub != null) Ui.Label(new Rect(tx, r.y + 24f, r.width - tx + r.x, 16f), sub, Ui.T.Dim, null, null, 11);
        if (dim) return false;
        Ui.Hit(r); bool c = GUI.Button(r, GUIContent.none, GUIStyle.none); if (c) Sounds.Fx("click"); return c;
    }

    // nieuwe stad: een naam, dan vier zeshoeken op een rij om mee te beginnen, met eronder wat de gekozen doet
    static readonly string[] NewNames = { "Startstad", "Lege planeet", "Natuur", "Planeet vol", "Hoogtetest" };
    static readonly string[] NewSubs = { "Een plein met winkels en een ring rond de noordpool", "Alleen terrein, zelf vanaf nul", "Zeeën, bergen, rivieren, ijs en woestijn, zonder stad", "Dorpen en steden over de hele bol (duurt even)", "20 landschappen, een per vlak: bergen, dalen, ravijnen en steilwanden" };
    static readonly string[] NewIcons = { "stad", "leeg", "bewolkt", "kompas", "heuvel" }; static readonly int[] NewKinds = { 1, 0, 3, 2, 4 };
    void NewPanel(Rect r)
    {
        r.height = 344f; Ui.Panel(r, true, 16f); float x = r.x + 18f, iw = r.width - 36f, y = r.y + 14f;
        PanelHead(r, "NIEUWE STAD", ref y);
        Ui.Label(new Rect(x + 2f, y, iw, 16f), "NAAM", Ui.T.Tiny); y += 18f;
        bool enter = EnterPressed;
        nameEdit = NameField(new Rect(x, y, iw, 38f), nameEdit); y += 52f;
        Ui.Label(new Rect(x + 2f, y, iw, 16f), "BEGIN MET", Ui.T.Tiny); y += 22f;
        float cw = iw / NewKinds.Length; const float TS = 56f; int sel = System.Array.IndexOf(NewKinds, newKind);
        for (int k = 0; k < NewKinds.Length; k++)
        {
            var c = new Rect(x + k * cw, y, cw, TS + 26f); bool on = k == sel, hv = c.Contains(Ui.Mouse);
            var hx = new Rect(c.center.x - TS * 0.5f, y + (hv && !on ? -3f : 0f), TS, TS * 0.9f);
            Ui.Tex(hx, Ui.Hex(on || hv ? Ui.Signal : new Color(0.07f, 0.09f, 0.12f, 0.92f), on || hv ? Ui.Signal : new Color(1f, 1f, 1f, 0.3f), 1.5f));
            Ui.Tex(new Rect(hx.center.x - 13f, hx.center.y - 13f, 26f, 26f), Ui.Icon(NewIcons[k]), on || hv ? Ui.SignalInk : Ui.Ink);
            Ui.Label(new Rect(c.x, hx.yMax + 4f, c.width, 18f), NewNames[k], Ui.T.Bold, on ? Ui.Signal : Ui.Ink, TextAnchor.MiddleCenter, 12);
            Ui.Hit(c); if (GUI.Button(c, GUIContent.none, GUIStyle.none)) { newKind = NewKinds[k]; Sounds.Fx("click"); }
        }
        y += TS + 32f;
        Ui.Label(new Rect(x, y, iw, 18f), sel >= 0 ? NewSubs[sel] : "", Ui.T.Dim, null, TextAnchor.MiddleCenter, 12); y += 30f;
        if (Ui.Button(new Rect(x, y, iw, 40f), "Maken", true) || enter) NewCity(nameEdit, newKind);
    }

    void OpenPanel(Rect r, float H)
    {
        var all = CityFiles.All; const float rowH = 74f; int rows = Mathf.Clamp(Mathf.FloorToInt((H - r.y - 110f) / (rowH + 6f)), 2, 6);
        r.height = 14f + 42f + Mathf.Max(1, Mathf.Min(rows, all.Count)) * (rowH + 6f) + 30f; Ui.Panel(r, true, 16f);
        float x = r.x + 18f, iw = r.width - 36f, y = r.y + 14f;
        PanelHead(r, "STAD OPENEN", ref y);
        if (all.Count == 0) { Ui.Label(new Rect(x, y, iw, 40f), "Nog geen steden bewaard. Begin met Nieuwe stad.", Ui.T.Dim); return; }
        if (r.Contains(Ui.Mouse) && Inp.Scroll != 0f) listOfs = Mathf.Clamp(listOfs + (Inp.Scroll < 0f ? 1 : -1), 0, Mathf.Max(0, all.Count - rows));
        listOfs = Mathf.Clamp(listOfs, 0, Mathf.Max(0, all.Count - rows));
        var cur = CityFiles.Last;
        for (int k = listOfs; k < Mathf.Min(all.Count, listOfs + rows); k++)
        {
            var c = all[k]; var rr = new Rect(x, y, iw, rowH); bool hv = rr.Contains(Ui.Mouse);
            Ui.Box(rr, new Color(1f, 1f, 1f, hv ? 0.08f : 0.03f), hv ? Ui.Signal : Ui.Line, 10f);
            var tr = new Rect(rr.x + 6f, rr.y + 6f, (rowH - 12f) * 16f / 9f, rowH - 12f); var th = CityFiles.Thumb(c);
            if (th != null) Ui.Tex(tr, th); else { Ui.Box(tr, new Color(0.04f, 0.13f, 0.3f, 0.9f), Color.clear, 6f); Ui.Tex(new Rect(tr.center.x - 11f, tr.center.y - 12f, 22f, 25f), Ui.Hex(new Color(0.45f, 0.7f, 1f, 0.5f), Color.clear, 0f, true)); }
            float tx = tr.xMax + 12f, tw = rr.xMax - tx - 10f;
            if (c == cur) Ui.Tex(new Rect(rr.xMax - 22f, rr.y + 10f, 12f, 10.8f), Ui.Hex(Ui.Signal, Ui.Signal, 0f));   // de laatste stad
            Ui.Label(new Rect(tx, rr.y + 10f, tw - 16f, 20f), c.name, Ui.T.Bold, hv ? Ui.Signal : Ui.Ink, null, 14);
            Ui.Label(new Rect(tx, rr.y + 31f, tw, 16f), CityFiles.When(c.saved) + (!string.IsNullOrEmpty(c.from) ? " · uit " + c.from : ""), Ui.T.Dim, null, null, 11);
            Ui.Label(new Rect(tx, rr.y + 48f, tw, 16f), CityFiles.Stats(c), Ui.T.Dim, Ui.Signal, null, 11);
            if (delAsk == c.id)   // eerst vragen: over de rij een balk met Verwijderen en Nee
            {
                var ar = new Rect(rr.x + 1f, rr.y + 1f, rr.width - 2f, rr.height - 2f); Ui.Box(ar, Ui.PanelSolid, Color.clear, 9f);
                Ui.Label(new Rect(ar.x + 14f, ar.y + 10f, ar.width - 28f, 20f), c.name + " verwijderen?", Ui.T.Bold, null, null, 14);
                Ui.Label(new Rect(ar.x + 14f, ar.y + 32f, ar.width - 220f, 30f), "Gaat naar de prullenbak in de map steden", Ui.T.Dim, null, null, 11);
                if (Ui.Button(new Rect(ar.xMax - 196f, ar.y + 30f, 110f, 32f), "Verwijderen", true, "gum")) { CityFiles.Delete(c); delAsk = null; Sounds.Fx("remove"); break; }
                if (Ui.Button(new Rect(ar.xMax - 80f, ar.y + 30f, 68f, 32f), "Nee")) delAsk = null;
                Ui.Hit(rr); y += rowH + 6f; continue;
            }
            var db = new Rect(rr.xMax - 38f, rr.yMax - 36f, 30f, 30f);
            if (hv && Ui.RoundButton(db, "gum")) delAsk = c.id;   // verwijderen (alleen zichtbaar bij aanwijzen)
            if (hv) Ui.Tip(db, "Verwijderen", "Naar de prullenbak");
            Ui.Hit(rr); if (GUI.Button(rr, GUIContent.none, GUIStyle.none)) { Sounds.Fx("click"); delAsk = null; OpenCity(c); }
            y += rowH + 6f;
        }
        if (all.Count > rows) Ui.Label(new Rect(x, y, iw, 18f), (listOfs + 1) + "–" + Mathf.Min(all.Count, listOfs + rows) + " van " + all.Count + " · scroll voor meer", Ui.T.Dim, null, TextAnchor.MiddleCenter, 11);
    }

    void ModesPanel(Rect r)
    {
        r.height = 14f + 42f + Modes.Length * 50f + 34f; Ui.Panel(r, true, 16f);
        float x = r.x + 18f, iw = r.width - 36f, y = r.y + 14f;
        PanelHead(r, "SPELMODUS", ref y);
        for (int k = 0; k < Modes.Length; k++)
        {
            var m = Modes[k];
            if (HexRow(new Rect(x, y, iw, 44f), m.name, m.live ? m.sub : m.sub + " · komt later", null, k == mode, !m.live) && m.live) mode = k;
            y += 50f;
        }
        Ui.Label(new Rect(x, y, iw, 24f), "Alleen de vrije modus werkt nu.", Ui.T.Dim, null, null, 11);
    }

    void SettingsPanel(Rect r)
    {
        r.height = 14f + 42f + 8 * 40f + 8f; Ui.Panel(r, true, 16f);
        float x = r.x + 18f, iw = r.width - 36f, y = r.y + 14f;
        PanelHead(r, "INSTELLINGEN", ref y);
        bool welcomeOff = PlayerPrefs.GetInt("raatstad-welkom-uit", 0) == 1;
        float[] sizes = { 0.9f, 1f, 1.15f, 1.3f }; int si = 0; for (int k = 1; k < 4; k++) if (Mathf.Abs(sizes[k] - Ui.Size) < Mathf.Abs(sizes[si] - Ui.Size)) si = k;
        System.Func<string, string, bool, bool> row = (nm, val, on) => { bool c = HexRow(new Rect(x, y, iw, 34f), nm, null, val, on); y += 40f; return c; };
        if (row("Geluid", Babble.Muted ? "uit" : "aan", !Babble.Muted)) { Babble.Muted = !Babble.Muted; Ambience.Muted = Babble.Muted; }
        if (row("Bouwmuziek", BuildMusic.StyleNames[BuildMusic.Style], false)) BuildMusic.SetStyle((BuildMusic.Style + 1) % BuildMusic.StyleNames.Length);
        if (row("Mensen", PlanetLife.PeopleOn ? "aan" : "uit", PlanetLife.PeopleOn)) PlanetLife.PeopleOn = !PlanetLife.PeopleOn;
        if (row("Auto's", PlanetLife.CarsOn ? "aan" : "uit", PlanetLife.CarsOn)) PlanetLife.CarsOn = !PlanetLife.CarsOn;
        if (row("Verkeerslichten", PlanetCity.Lights ? "aan" : "uit", PlanetCity.Lights)) PlanetCity.Lights = !PlanetCity.Lights;   // getekend bij het openen van een stad
        if (row("Totale chaos", PlanetLife.Chaos ? "aan" : "uit", PlanetLife.Chaos)) PlanetLife.Chaos = !PlanetLife.Chaos;
        if (row("Uitleg bij een nieuwe stad", welcomeOff ? "uit" : "aan", !welcomeOff)) PlayerPrefs.SetInt("raatstad-welkom-uit", welcomeOff ? 0 : 1);
        if (row("Interfacegrootte", Mathf.RoundToInt(sizes[si] * 100f) + "%", false)) Ui.Size = sizes[(si + 1) % 4];
    }

    // in het spel: de vensters van het opslaan (bovenop de rest)
    void HomeDialogs(float W, float H)
    {
        if (dialog == 0) return;
        Ui.DimBack(0.35f);
        float w = 420f; var r = new Rect((W - w) * 0.5f, H * 0.5f - 120f, w, dialog == 1 ? 196f : 214f); Ui.Panel(r, true, 16f);
        float x = r.x + 18f, iw = w - 36f, y = r.y + 14f;
        if (dialog == 1)
        {
            Ui.Label(new Rect(x, y, iw - 40f, 28f), "OPSLAAN ALS", Ui.T.Title);
            if (Ui.RoundButton(new Rect(r.xMax - 18f - 30f, y - 1f, 30f, 30f), "sluit")) dialog = 0; y += 42f;
            Ui.Label(new Rect(x + 2f, y, iw, 16f), "NAAM VAN DE NIEUWE STAD", Ui.T.Tiny); y += 18f;
            bool enter = EnterPressed;
            nameEdit = NameField(new Rect(x, y, iw, 38f), nameEdit); y += 52f;
            float half = (iw - 8f) * 0.5f;
            if (Ui.Button(new Rect(x, y, half, 40f), "Annuleren")) dialog = 0;
            if (Ui.Button(new Rect(x + half + 8f, y, half, 40f), "Opslaan", true) || enter) { dialog = 0; SaveAs(nameEdit); }
        }
        else
        {
            Ui.Label(new Rect(x, y, iw, 28f), homeToNew ? "NIEUWE WERELD" : "NAAR HET STARTSCHERM", Ui.T.Title); y += 38f;
            Ui.Label(new Rect(x, y, iw, 36f), "Wil je " + (CityFiles.Current != null ? CityFiles.Current.name : "je stad") + " eerst opslaan?", Ui.T.Text); y += 44f;
            if (Ui.CardButton(new Rect(x, y, iw, 48f), homeToNew ? "Opslaan en een nieuwe wereld" : "Opslaan en naar het startscherm", null, true, "huis")) { SaveCity(false); GoHome(); }
            y += 56f; float half = (iw - 8f) * 0.5f;
            if (Ui.Button(new Rect(x, y, half, 40f), "Niet opslaan")) GoHome();
            if (Ui.Button(new Rect(x + half + 8f, y, half, 40f), "Annuleren")) { dialog = 0; homeToNew = false; }
        }
    }

    // het menu in het spel: de steden (in plaats van de drie oude slots)
    void HomeMenuCards(float x, float y, float iw, float half)
    {
        if (Ui.CardButton(new Rect(x, y, half, 48f), "Opslaan", "Nu bewaren (ook elke 3 min)", false, "boek")) { menuOpen = false; Say(SaveCity(false) ? (CityFiles.Current?.name ?? "Stad") + " opgeslagen" : "Opslaan lukte niet"); }
        if (Ui.CardButton(new Rect(x + half + 8f, y, half, 48f), "Opslaan als", "Als nieuwe stad bewaren", false, "boek")) { menuOpen = false; dialog = 1; nameEdit = CityFiles.FreeName(CityFiles.Current != null ? CityFiles.Current.name : "Nieuwe stad"); }
        if (Ui.CardButton(new Rect(x, y + 56f, half, 48f), "Naar het startscherm", "Andere stad openen of nieuw", false, "huis")) { menuOpen = false; dialog = 2; }
    }
    string CityName => CityFiles.Current != null ? CityFiles.Current.name : "Stad";

    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
