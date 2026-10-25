using System.Collections.Generic;
using UnityEngine;

// Bouwen op de planeet: de camera hangt boven een punt op de bol en schuift over het oppervlak (WASD),
// draait (rechtermuis) en zoomt (scroll). Klik een vak om het gekozen blok te plaatsen; een weg is ook een blok.
// Tab = rondlopen op de planeet.
public class PlanetBuilder : MonoBehaviour
{
    public Camera Cam;
    Vector3 focus = Vector3.up, fwd = Vector3.forward;
    float pitch = 55f, dist = 260f;
    int tool = 1, hover = -1; bool hasHover;
    readonly List<(Dictionary<int, List<string>> cells, Dictionary<int, float> terr)> undo = new List<(Dictionary<int, List<string>>, Dictionary<int, float>)>();
    LineRenderer outline; PlanetWalker walker; bool walking; string msg = ""; float msgT;
    int lastPainted = -1; bool painting;
    int lineStart = -1; List<int> linePlan; readonly List<LineRenderer> planLines = new List<LineRenderer>();   // weg slepen: een rechte lijn van vakken (30°-richtingen)                 // slepen: elk nieuw vak onder de muis krijgt het blok
    Beauty.Result beauty; float beautyT = -99f, fps; int fpsN; float fpsT;
    Vector3 centre; float centreT = -99f; bool hasCentre;   // middelpunt van de stad, voor de pijl
    bool showHelp; Vector2 rightAt; int rightCell = -1;

    void Start()
    {
        int s0 = Planet.Starts[Random.Range(0, Planet.Starts.Length)]; focus = Planet.Cells[s0].dir; fwd = Planet.Cells[s0].north;   // elke speler begint op een startplek: zo ver mogelijk van de vijfhoeken
        if (PlanetCity.Cells.Count == 0) { PlanetCity.Sample(s0); PlanetCity.Rebuild(); PlanetCity.Save(); dist = 160f; }   // lege stad: begin met een voorbeeldstadje
        outline = new GameObject("Hover").AddComponent<LineRenderer>(); outline.loop = true; outline.positionCount = 6; outline.widthMultiplier = 0.35f; outline.material = Raatstad.MatOutline; outline.useWorldSpace = true;
        walker = new GameObject("Wandelaar").AddComponent<PlanetWalker>(); walker.Cam = Cam; walker.gameObject.SetActive(false);
        Say("Planeet: " + Planet.Cells.Length + " vakken, omtrek " + Mathf.RoundToInt(2f * Mathf.PI * Planet.R) + " m · toets 1-9 of de balk kiest een blok · klik een vak");
    }

    void Say(string s) { msg = s; msgT = Time.time + 4f; }
    void Push() { undo.Add((PlanetCity.Clone(PlanetCity.Cells), new Dictionary<int, float>(PlanetCity.Terr))); if (undo.Count > 100) undo.RemoveAt(0); }
    void Undo()
    {
        var u = undo[undo.Count - 1]; undo.RemoveAt(undo.Count - 1);
        PlanetCity.Cells = u.cells; PlanetCity.Terr.Clear(); foreach (var kv in u.terr) PlanetCity.Terr[kv.Key] = kv.Value;
        PlanetCity.TouchAll(); Changed(); PlanetCity.RebuildGrid(); Say("Ongedaan gemaakt");
    }
    void Changed() { PlanetCity.Rebuild(); PlanetCity.Save(); }

    void Update()
    {
        if (Inp.Down(K.Tab)) ToggleWalk();
        if (Inp.Down(K.M)) { Babble.Muted = !Babble.Muted; Ambience.Muted = Babble.Muted; Say(Babble.Muted ? "Geluid uit (M)" : "Geluid aan (M)"); }
        if (walking) return;
        CameraControl();

        K[] dk = { K.D1, K.D2, K.D3, K.D4, K.D5, K.D6, K.D7, K.D8, K.D9 };
        for (int i = 0; i < dk.Length; i++) if (Inp.Down(dk[i])) { tool = i; Say("Gereedschap: " + Blocks.Names[i]); }
        if (Inp.Down(K.G)) { PlanetCity.GridOn = !PlanetCity.GridOn; PlanetCity.ShowGrid(PlanetCity.GridOn && !walking); Say(PlanetCity.GridOn ? "Raster aan (G)" : "Raster uit (G)"); }
        if (Blocks.Tools[tool] == "weg") { if (Inp.Down(K.B)) CycleStyle(); if (Inp.Down(K.O)) ToggleOneWay(); }
        if (Inp.Down(K.L)) ToggleLights();
        if (Inp.Down(K.H)) { CityCentre(); if (hasCentre) { focus = centre; Say("Naar het midden van de stad (H)"); } }
        if (Inp.Down(K.P)) { bool north = Vector3.Dot(focus, Planet.Cells[Planet.North].dir) < 0.999f; focus = Planet.Cells[north ? Planet.North : Planet.South].dir; Say(north ? "Naar de noordpool (P, nog een keer: zuidpool)" : "Naar de zuidpool (P)"); }
        if (Inp.Down(K.Z) && Inp.Held(K.Ctrl) && undo.Count > 0 && !sculpting) Undo();
        if (TerrTool)
        {
            if (Inp.Down(K.T)) { terrMode = (terrMode + 1) % TerrModes.Length; Say("Terrein: " + TerrModes[terrMode] + " (T) · " + TerrHints[terrMode]); }
            if (Inp.Down(K.LBr) && brush > 0) { brush--; Say("Penseel: " + BrushSize(brush) + " vak" + (brush > 0 ? "ken" : "") + " ([ en ])"); }
            if (Inp.Down(K.RBr) && brush < 3) { brush++; Say("Penseel: " + BrushSize(brush) + " vakken ([ en ])"); }
        }

        // aanwijzen: het vak onder de muis
        hasHover = false;
        if (!OverBar && Physics.Raycast(Cam.ScreenPointToRay(Inp.Pos), out RaycastHit hit, 5000f, Layers.Grond, QueryTriggerInteraction.Ignore))
        {
            hover = Planet.Nearest(hit.point); hasHover = true;
            ShowOutline(hover);
            if (Inp.Down(K.X)) { Push(); if (PlanetCity.RemoveTop(hover)) Changed(); }
            if (Inp.RightDown) { rightAt = Inp.Pos; rightCell = hover; }
            if (Inp.RightUp && rightCell >= 0 && Vector2.Distance(Inp.Pos, rightAt) < 6f) { Push(); if (PlanetCity.Clear(hover)) { Changed(); Say("Vak leeggemaakt"); } else if (PlanetCity.IsHalf(hover)) Say("Hier loopt een weg overheen: klik met rechts op een wegvak van die weg"); rightCell = -1; }
            bool roadTool = Blocks.Tools[tool] == "weg" || Blocks.Tools[tool] == "pad";
            if (TerrTool) { ShowBrush(hover); if (Inp.LeftDown) { Push(); SculptStart(hover); } }
            else if (Inp.LeftDown) { Push(); painting = !roadTool; lastPainted = -1; lineStart = roadTool ? hover : -1; linePlan = null; }
            if (lineStart >= 0 && Inp.LeftHeld) { linePlan = hover == lineStart ? null : PlanetCity.LinePlan(lineStart, hover); ShowPlan(linePlan); }
            if (painting && Inp.LeftHeld && hover != lastPainted)
            {
                lastPainted = hover; string t = Blocks.Tools[tool];
                string err = PlanetCity.Place(hover, t); if (err != null) Say(err); else Changed();
            }
        }
        if (!hasHover || !TerrTool) ShowBrush(-1);
        if (sculpting) { if (Inp.LeftHeld) SculptUpdate(Time.deltaTime); else SculptEnd(); }
        if (Inp.LeftUp)
        {
            if (sculpting) SculptEnd();
            if (lineStart >= 0)
            {
                string t = Blocks.Tools[tool]; int ok = 0; string err = null;
                if (linePlan != null) ok = PlanetCity.PlaceLine(linePlan, t); else { err = PlanetCity.Place(lineStart, t); if (err == null) ok = 1; }
                if (ok > 0) Changed(); else if (err != null) Say(err);
                if (linePlan != null && ok > 0) Say(ok + " vakken " + Blocks.Names[tool].ToLower());
                lineStart = -1; linePlan = null; ShowPlan(null);
            }
            painting = false;
        }
        outline.enabled = hasHover;
    }

    bool OverBar => Inp.Pos.y < 96f || (Inp.Pos.y > Screen.height - 70f) || showHelp || ((Blocks.Tools[tool] == "weg" || TerrTool) && Inp.Pos.x < (TerrTool ? 760f : 612f) && Inp.Pos.y > 96f && Inp.Pos.y < 152f);

    // ---------- terrein boetseren (zoals 14b-boetseren.js in de webversie) ----------
    // Vijf standen, allemaal met de linkermuisknop ingedrukt houden en slepen; Shift doet het omgekeerde:
    //  Ophogen / Afgraven  het terrein onder het penseel gaat omhoog of omlaag zolang je drukt
    //  Egaliseren          alles wat je raakt gaat naar de gemiddelde hoogte onder het penseel waar je begon (Shift: de hoogte van dat vak)
    //  Gladstrijken        scherpe overgangen worden zacht (elk vak schuift naar het gemiddelde van zijn buren)
    //  Helling             sleep van A naar B: een gelijkmatige helling van de hoogte bij A naar die bij B
    // Het penseel is 1, 7, 19 of 37 vakken ([ en ]); het midden werkt het hardst. Vakken waar iets op staat blijven liggen.
    static readonly string[] TerrModes = { "Ophogen", "Afgraven", "Egaliseren", "Gladstrijken", "Helling" };
    static readonly string[] TerrHints = { "Shift: omlaag", "Shift: omhoog", "Shift: naar de hoogte van het startvak", "slepen om glad te strijken", "sleep van hier naar het andere eind" };
    int terrMode, brush = 1; bool sculpting; int sA = -1, sLast = -1; float sHa, sHv, sAcc, sCost; bool sInv;
    readonly Dictionary<int, float> sPrior = new Dictionary<int, float>(); readonly HashSet<int> sTouched = new HashSet<int>();
    bool TerrTool => Blocks.Tools[tool] == "terrein";
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
        PlanetCity.RebuildGrid(); Changed(); beautyT = -99f;
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

    static readonly string[] StyleNames = { "Gewone weg", "Brede weg", "Laan" };
    void CycleStyle() { PlanetCity.DragStyle = (byte)((PlanetCity.DragStyle + 1) % 3); Say("Wegtype: " + StyleNames[PlanetCity.DragStyle] + " (B) · sleep of klik om aan te leggen; een weg over water of een uitgraving wordt een brug"); }
    void ToggleOneWay() { PlanetCity.DragOneWay = !PlanetCity.DragOneWay; Say(PlanetCity.DragOneWay ? "Eenrichtingsverkeer (O): de weg die je sleept is eenrichting, in de sleeprichting" : "Weer in twee richtingen (O)"); }
    void ToggleLights() { PlanetCity.Lights = !PlanetCity.Lights; PlanetCity.TouchAll(); PlanetCity.Rebuild(); Say(PlanetCity.Lights ? "Verkeerslichten aan (L)" : "Verkeerslichten uit (L)"); }

    void SwitchSlot(int n)
    {
        if (n == PlanetCity.Slot) return;
        PlanetCity.Save(); PlanetCity.Slot = n; undo.Clear();
        if (!PlanetCity.Load()) { PlanetCity.Cells = new Dictionary<int, List<string>>(); PlanetCity.Terr.Clear(); PlanetCity.ClearLinks(); PlanetCity.TouchAll(); PlanetCity.RebuildGrid(); }
        PlanetCity.Rebuild(); beautyT = -99f; Say("Stad " + n + (PlanetCity.Cells.Count == 0 ? " (leeg)" : ""));
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
        for (int k = 0; k < planLines.Count; k++)
        {
            if (k >= n) { planLines[k].enabled = false; continue; }
            var pc = polys[k]; planLines[k].enabled = true; planLines[k].widthMultiplier = 0.3f; brushShown = -2;
            if (planLines[k].positionCount != pc.Length) planLines[k].positionCount = pc.Length;
            for (int q = 0; q < pc.Length; q++) planLines[k].SetPosition(q, pc[q] * (Planet.R + hs[k]));
        }
    }

    void ShowOutline(int i)
    {
        var c = Planet.Cells[i]; float h = PlanetCity.StackHeight(i) + 0.3f; bool plat = PlanetCity.Plateau(i);
        if (Planet.IsPole(i) && Time.time > msgT) Say(i == Planet.North ? "De noordpool: hier gaat de as van de planeet doorheen" : "De zuidpool: hier gaat de as van de planeet doorheen");
        else if (c.corners.Length == 5 && Time.time > msgT) Say("Dit is een van de 12 vijfhoeken (oranje): op een bol passen niet alleen zeshoeken");
        else if (Time.time > msgT && System.Array.IndexOf(Planet.Starts, i) >= 0) Say("Startplek (een van de 20): zo ver mogelijk van de vijfhoeken, hier is het raster het netst");
        if (outline.positionCount != c.corners.Length) outline.positionCount = c.corners.Length;
        for (int k = 0; k < c.corners.Length; k++)   // op het plateau, of op het maaiveld en de weg die er helt
            outline.SetPosition(k, c.corners[k] * (Planet.R + (plat ? PlanetCity.G(i) + h : (PlanetCity.IsWalk(i) ? Mathf.Max(PlanetCity.OwnCorner(i, k), PlanetCity.SurfaceH(i, c.corners[k] * Planet.R)) : PlanetCity.OwnCorner(i, k)) + 0.3f)));
    }

    void CameraControl()
    {
        float dt = Time.deltaTime, sp = (Inp.Held(K.Shift) ? 3f : 1f) * dist * 0.8f;
        Vector3 up = focus; fwd = (fwd - up * Vector3.Dot(fwd, up)).normalized; Vector3 right = Vector3.Cross(up, fwd);
        Vector3 move = Vector3.zero;
        if (Inp.Held(K.W)) move += fwd; if (Inp.Held(K.S)) move -= fwd; if (Inp.Held(K.D)) move += right; if (Inp.Held(K.A)) move -= right;
        if (move != Vector3.zero) { focus = (focus + move.normalized * (sp * dt / Planet.R)).normalized; up = focus; fwd = (fwd - up * Vector3.Dot(fwd, up)).normalized; }
        dist = Mathf.Clamp(dist * (1f - Inp.Scroll * 0.1f), 12f, 2600f);
        if (Inp.RightHeld) { Vector2 d = Inp.Delta; fwd = Quaternion.AngleAxis(d.x * 0.2f, up) * fwd; pitch = Mathf.Clamp(pitch - d.y * 0.2f, 15f, 89f); }
        int fc = Planet.Nearest(focus); float h = PlanetCity.G(fc) + PlanetCity.StackHeight(fc);
        Vector3 target = focus * (Planet.R + h);
        Vector3 pos = target + (-fwd * Mathf.Cos(pitch * Mathf.Deg2Rad) + up * Mathf.Sin(pitch * Mathf.Deg2Rad)) * dist;
        Cam.transform.position = pos; Cam.transform.rotation = Geo.Look(target - pos, up);
    }

    void ToggleWalk()
    {
        walking = !walking; PlanetCity.ShowGrid(PlanetCity.GridOn && !walking);
        if (walking)
        {
            outline.enabled = false;
            int i = Planet.Nearest(focus); float h = PlanetCity.G(i) + PlanetCity.StackHeight(i);
            if (PlanetCity.IsWalk(i)) h = PlanetCity.SurfaceH(i, focus * Planet.R) + 0.26f;
            walker.Enter(focus * (Planet.R + h + 0.3f), fwd);
        }
        else { walker.Exit(); focus = walker.transform.position.normalized; fwd = walker.Forward; }
    }

    void OnGUI()
    {
        Ui.Init(); float W = Screen.width, H = Screen.height;
        if (Time.time - beautyT > 3f) { beauty = Beauty.Compute(); beautyT = Time.time; }
        fpsN++; if (Time.time - fpsT > 0.5f) { fps = fpsN / (Time.time - fpsT); fpsN = 0; fpsT = Time.time; }

        // ---- bovenbalk links: naam, stad, blokken, score
        var r = Ui.Card(new Rect(12, 12, 330, 58));
        Ui.Title(new Rect(r.x, r.y, 160, 24), "RAATSTAD");
        Ui.Dim(new Rect(r.x, r.y + 24, 300, 18), (beauty != null ? beauty.blocks + " blokken · hoogste punt " + beauty.top.ToString("0.#") + " m" : "") + " · stad " + PlanetCity.Slot);
        if (beauty != null)
        {
            bool over = new Rect(342, 12, 160, 58).Contains(Inp.Pos.x > 0 ? new Vector2(Inp.Pos.x, H - Inp.Pos.y) : Vector2.zero);
            var sr = Ui.Card(new Rect(348, 12, 160, 58));
            Ui.Big(new Rect(sr.x, sr.y - 2, 50, 30), beauty.score > 0 ? beauty.score.ToString() : "–");
            Ui.Dim(new Rect(sr.x + 52, sr.y + 2, 110, 18), "<b>" + beauty.label + "</b>");
            Ui.Dim(new Rect(sr.x, sr.y + 24, 110, 16), "schoonheid");
            if (over && beauty.score > 0)
            {
                string txt = ""; foreach (var row in beauty.rows) txt += "<b>" + row.name + "</b>  " + (row.v < 0f ? "–" : Mathf.RoundToInt(row.v * 100f) + "%") + "   <color=#c8d0d8>" + row.detail + "</color>\n";
                txt += "\n<color=#f3b43b>" + beauty.tip + "</color>";
                var tr = Ui.Card(new Rect(348, 76, 440, 60 + beauty.rows.Count * 22), 0.95f); Ui.Text(new Rect(tr.x, tr.y, tr.width, tr.height), txt);
            }
        }
        // ---- bovenbalk rechts: klok, weer, geluid, hulp
        float x = W - 12;
        if (Ui.Button(new Rect(x - 40, 12, 40, 40), "?", showHelp)) showHelp = !showHelp; x -= 48;
        if (Ui.Button(new Rect(x - 76, 12, 76, 40), Babble.Muted ? "Stil" : "Geluid")) { Babble.Muted = !Babble.Muted; Ambience.Muted = Babble.Muted; } x -= 84;
        if (Weather.I != null) { if (Ui.Button(new Rect(x - 120, 12, 120, 40), Weather.Names[(int)Weather.I.Current] + (Weather.I.Wissel ? "" : " ·"))) { Weather.I.Set((Weather.Kind)(((int)Weather.I.Current + 1) % 6), true); } x -= 128; }
        var dn = GetComponent<DayNight>();
        if (Ui.Button(new Rect(x - 96, 12, 96, 40), DayNight.Clock + (dn != null && dn.Frozen ? " ·" : ""))) { Inp.Fake(K.N); }
        // fps linksonder boven de balk
        Ui.Dim(new Rect(16, H - 92, 200, 16), Mathf.RoundToInt(fps) + " fps · " + (PlanetLife.I != null ? PlanetLife.I.People.Count + " mensen · " + PlanetLife.I.CarCount + " auto's" : ""));

        // ---- melding
        if (Time.time < msgT) Ui.Toast(msg, Mathf.Clamp01((msgT - Time.time) / 0.5f));

        // ---- onderbalk: gereedschap, modus, slots
        if (!walking)
        {
            int n = Blocks.Tools.Length - 1; float bw = Mathf.Min(74f, (W - 420f) / n), x0 = 12f;   // zonder de gum: rechtermuisknop maakt een vak leeg
            for (int i = 0; i < n; i++)
            {
                string key = i < 9 ? (i + 1).ToString() : null;
                if (Ui.Tool(new Rect(x0 + i * bw, H - 68, bw - 6, 58), Blocks.Names[i], key, Ui.ToolColor(Blocks.Tools[i]), i == tool)) tool = i;
            }
            if (Blocks.Tools[tool] == "weg")
            {
                var pr = Ui.Card(new Rect(12, H - 146, 600, 46), 0.9f); float bx = pr.x;
                if (Ui.Button(new Rect(bx, pr.y, 210, 30), "Type: " + StyleNames[PlanetCity.DragStyle] + "  <size=10>B</size>", PlanetCity.DragStyle != 0)) CycleStyle(); bx += 216;
                if (Ui.Button(new Rect(bx, pr.y, 180, 30), (PlanetCity.DragOneWay ? "Eenrichting" : "Twee richtingen") + "  <size=10>O</size>", PlanetCity.DragOneWay)) ToggleOneWay(); bx += 186;
                if (Ui.Button(new Rect(bx, pr.y, 150, 30), "Lichten: " + (PlanetCity.Lights ? "aan" : "uit") + "  <size=10>L</size>", PlanetCity.Lights)) ToggleLights();
            }
            if (TerrTool)   // terrein: de vijf standen en de grootte van het penseel
            {
                var pr = Ui.Card(new Rect(12, H - 146, 748, 46), 0.9f); float bx = pr.x;
                for (int m = 0; m < TerrModes.Length; m++) { if (Ui.Button(new Rect(bx, pr.y, 112, 30), TerrModes[m], m == terrMode)) { terrMode = m; Say("Terrein: " + TerrModes[m] + " · " + TerrHints[m]); } bx += 116; }
                if (Ui.Button(new Rect(bx, pr.y, 136, 30), "Penseel " + BrushSize(brush) + "  <size=10>[ ]</size>")) brush = (brush + 1) % 4;
                if (hasHover && !OverBar)   // hoogte onder de muis
                {
                    string t = (Mathf.Round(PlanetCity.G(hover) * 2f) / 2f).ToString("0.0") + " m" + (PlanetCity.Plateau(hover) ? " · bebouwd, blijft liggen" : sculpting ? "" : " · " + TerrHints[terrMode]);
                    var lr = Ui.Card(new Rect(Inp.Pos.x + 18, H - Inp.Pos.y + 14, 300, 30), 0.85f); Ui.Dim(new Rect(lr.x, lr.y - 2, lr.width, 18), t);
                }
            }
            float sx = W - 12;
            if (Ui.Button(new Rect(sx - 110, H - 56, 110, 44), "Lopen  <size=10>Tab</size>", false, Ui.Amber)) ToggleWalk(); sx -= 118;
            for (int k = 3; k >= 1; k--) { if (Ui.Button(new Rect(sx - 64, H - 56, 64, 44), "Stad " + k, k == PlanetCity.Slot)) SwitchSlot(k); sx -= 70; }
        }
        else
        {
            var wr = Ui.Card(new Rect(W / 2 - 230, H - 52, 460, 40), 0.8f);
            Ui.Dim(new Rect(wr.x, wr.y, wr.width, 24), "WASD lopen · muis kijken · Shift rennen · Spatie springen · Esc muis los · Tab terug naar bouwen");
        }

        // ---- hulp
        if (showHelp)
        {
            var hr = Ui.Card(new Rect(W / 2 - 260, 90, 520, 480), 0.95f);
            Ui.Title(new Rect(hr.x, hr.y, 300, 26), "Bediening");
            if (Ui.Button(new Rect(hr.x + hr.width - 150, hr.y, 150, 30), "Voorbeeldstad hier")) { Push(); PlanetCity.Sample(Planet.Nearest(focus)); Changed(); showHelp = false; Say("Voorbeeldstad geplaatst (Ctrl+Z haalt hem weg)"); }
            Ui.Help(new Rect(hr.x, hr.y + 32, hr.width, 430),
                "<b>Bouwen</b>  klik of sleep over vakken (1-9 kiest een blok) · rechtermuisknop maakt een vak helemaal leeg · X haalt het bovenste blok weg · Ctrl+Z maakt ongedaan\n" +
                "<b>Wegen en paden slepen</b>  klik een vak en sleep: een rechte lijn in een van de 12 richtingen (30°). Alleen de vakken in de lijn worden verbonden; twee wegen naast elkaar blijven los. Een klik verbindt met alle buurwegen.\n" +
                "<b>Wegtypes</b>  bij de weg kies je met B gewone weg, brede weg of laan (met bomen), met O eenrichtingsverkeer (in de sleeprichting; pijlen op het asfalt) en met L verkeerslichten. Sleep een weg over water of een uitgraving: dan wordt het een brug. Een weg in een tussenrichting laat de helft van de vakken ernaast vrij voor gebouwen.\n" +
                "<b>Wegen en paden</b>  zijn ook blokken: zet ze op aangrenzende vakken en ze sluiten vanzelf aan\n" +
                "<b>Winkel, plein, toren, molen</b>  etalages lichten 's avonds op (zet er gerust woningen bovenop), een plein heeft een fontein en bankjes, en de molenwieken draaien harder als het stormt\n" +
                "<b>Terrein</b>  toets 9: ophogen, afgraven, egaliseren, gladstrijken of helling (T wisselt), houd de muis ingedrukt en sleep; Shift doet het omgekeerde, [ en ] maken het penseel kleiner of groter. Bebouwde vakken blijven liggen en staan op een sokkel; wegen hellen mee\n" +
                "<b>Camera</b>  WASD schuiven (Shift sneller) · rechtermuis draaien · scroll zoomen · H naar het midden van je stad · P naar de noord- of zuidpool\n" +
                "<b>Wandelen</b>  Tab · WASD · Shift rennen · Spatie springen · Esc muis los\n" +
                "<b>Weer en tijd</b>  R volgend weer (vast) · N zon stilzetten · M geluid aan/uit · G raster aan/uit\n" +
                "<b>Planeet</b>  21.162 vakken, omtrek 3,8 km. Oranje: de 12 vijfhoeken. Lichtblauw: de 20 startplekken. Rood-wit gestreepte paal met vlag: de noord- en zuidpool.");
        }

        // ---- pijl naar het midden van de stad als je er ver vandaan bent
        CityCentre();
        if (hasCentre)
        {
            Vector3 here = (walking ? walker.transform.position : focus * Planet.R).normalized;
            float dist = Mathf.Acos(Mathf.Clamp(Vector3.Dot(here, centre), -1f, 1f)) * Planet.R;
            if (dist > 120f)
            {
                Vector3 sp = Cam.WorldToScreenPoint(centre * (Planet.R + 20f)); Vector2 c2 = new Vector2(W / 2f, H / 2f);
                Vector2 dir = (new Vector2(sp.x, sp.y) - c2).normalized; if (sp.z < 0f) dir = -dir;
                bool onScreen = sp.z > 0f && sp.x > 0 && sp.x < W && sp.y > 0 && sp.y < H;
                Vector2 pos = onScreen ? new Vector2(sp.x, H - sp.y) : c2 + new Vector2(dir.x, -dir.y) * Mathf.Min(W, H) * 0.38f;
                float ang = Mathf.Atan2(-dir.y, dir.x) * Mathf.Rad2Deg;
                var m = GUI.matrix; GUIUtility.RotateAroundPivot(ang, pos);
                var ast = new GUIStyle { fontSize = 26, alignment = TextAnchor.MiddleCenter }; ast.normal.textColor = Ui.Amber;
                GUI.Label(new Rect(pos.x - 20, pos.y - 14, 40, 28), "➤", ast); GUI.matrix = m;
                var cr = Ui.Card(new Rect(pos.x - 70, pos.y + 16, 140, 30), 0.85f); Ui.Dim(new Rect(cr.x, cr.y - 2, cr.width, 18), "Centrum · " + Mathf.RoundToInt(dist) + " m");
            }
        }
    }
}
