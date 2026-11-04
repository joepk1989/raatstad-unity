using System.Collections.Generic;
using UnityEngine;

// De AI-bouwer in het spel (de planner zelf staat in PlanetAI.cs, los van de interface):
//  - menu > Gereedschap > AI-bouwer: daarna klik je het vak aan waar hij begint; dezelfde kaart pauzeert en gaat verder, ernaast Stoppen
//    en het tempo (langzaam, normaal, snel);
//  - elke zet gaat dezelfde weg als een klik van de speler: Push, PlaceLine/Place (met SetLevel, DipUnder en BoreLine voor een weg),
//    Changed; dus Ctrl+Z, geluid en opslaan werken gewoon;
//  - een zet wordt eerst even getoond (blauwe omtrek op de vakken, een draaiende pijl boven het vak) en dan gedaan;
//  - bovenin een klein paneel: wat hij doet, het aantal zetten, de schoonheid en het bereik, met pauze, stop, camera volgen en het tempo.
public partial class PlanetBuilder
{
    bool aiPick, aiOn, aiPaused, aiFollow = true; int aiTempo = 1, aiFails, aiPinCell = -1, aiBeauty0 = -1, aiLogged; float aiStartT; string aiBest;
    float aiNext, aiShowT; PlanetAI.Move aiMove; string aiWhat = ""; bool aiUnsaved;
    // tempo: seconden per zet (een deel tonen, een deel wachten) en hoeveel ms hij per beeld mag denken; Turbo toont de zet maar heel kort
    static readonly string[] AiTempoNames = { "Langzaam", "Normaal", "Snel", "Turbo" }; static readonly float[] AiPace = { 2.6f, 1.1f, 0.4f, 0.08f }, AiThinkMs = { 3f, 3f, 4f, 8f };
    readonly List<LineRenderer> aiLines = new List<LineRenderer>(); Transform aiPin; static Material aiMat;
    static readonly Color AiCol = new Color(0.35f, 0.76f, 1f);

    string AiCardSub => aiPick ? "Klik een vak aan waar hij begint" : !aiOn ? "Een AI bouwt een stad op een vak dat jij aanwijst" : aiPaused ? "Gepauzeerd: klik om verder te gaan" : "Bezig: klik om te pauzeren";
    // bouwstijl: -1 de AI kiest zelf, anders een van PlanetAI.StyleNames; kiezen bij het aanwijzen van het startvak (de balk bovenin)
    int aiStyle = -1;
    string AiStyleName => aiStyle < 0 ? "Zelf kiezen" : PlanetAI.StyleNames[aiStyle];
    void AiNextStyle() => aiStyle = aiStyle + 1 >= PlanetAI.StyleNames.Length ? -1 : aiStyle + 1;
    string aiPickWhy;
    // undo: de stand van vóór de AI (aiBaseSnap, op plek aiUndoBase in de undo-lijst) blijft altijd bewaard, en de stappen van de
    // speler van daarvoor ook: raakt de lijst vol, dan valt de oudste AI-stap weg in plaats van een stap van de speler.
    // aiUndoN: hoe lang de lijst was na de laatste zet; korter = de speler deed Ctrl+Z (dan pauzeert de AI en kijkt wat er nog van hem is).
    int aiUndoBase = -1, aiUndoN; string aiBaseSnap;
    int AiBaseIndex() { if (aiBaseSnap == null) return -1; if (aiUndoBase >= 0 && aiUndoBase < undo.Count && ReferenceEquals(undo[aiUndoBase], aiBaseSnap)) return aiUndoBase; for (int i = 0; i < undo.Count; i++) if (ReferenceEquals(undo[i], aiBaseSnap)) return aiUndoBase = i; return -1; }   // waarom hij op het vak onder de muis niet kan beginnen (null: dat kan wel)

    // in het menu, onder Gereedschap
    void AiMenuCards(float x, float y, float half)
    {
        if (Ui.CardButton(new Rect(x, y, half, 48f), aiOn ? (aiPaused ? "AI-bouwer verder" : "AI-bouwer pauzeren") : "AI-bouwer", AiCardSub, aiOn && !aiPaused, "stad")) { AiButton(); menuOpen = false; }
        if (aiOn) { if (Ui.CardButton(new Rect(x + half + 8f, y, half, 48f), "AI-bouwer stoppen", PlanetAI.Moves + " zetten gedaan", false, "sluit")) { AiStop(); menuOpen = false; } }
        else
        {
            // vier tempo's passen niet naast elkaar op een halve kaart: een klik kiest het volgende
            if (Ui.CardButton(new Rect(x + half + 8f, y, half, 48f), "Tempo: " + AiTempoNames[aiTempo], "Klik voor het volgende tempo", false, "wissel")) aiTempo = (aiTempo + 1) % AiTempoNames.Length;
        }
    }
    void AiButton()
    {
        if (!aiOn)
        {
            aiPick = true; catOpen = wheelOpen = false; if (cursorMode) SetCursorMode(false);
            Say("AI-bouwer: klik op het vak waar hij mag beginnen (Esc: toch niet)");
        }
        else { aiPaused = !aiPaused; Say(aiPaused ? "AI-bouwer gepauzeerd" : "AI-bouwer gaat verder"); }
    }
    void AiStop() => AiStop(true);
    void AiStop(bool score)
    {
        if (score && aiOn && PlanetAI.Moves > 0) AiScore("gestopt");
        if (aiUnsaved) { PlanetCity.Save(); aiUnsaved = false; }   // wat Turbo nog niet bewaard had
        aiOn = aiPaused = aiPick = false; aiMove = null;
        Say("AI-bouwer gestopt na " + PlanetAI.Moves + " zetten (Ctrl+Z haalt ze één voor één weg)");
        aiBaseSnap = null; aiUndoBase = -1;
        PlanetAI.Reset(); AiLines(null); aiPinCell = -1; if (aiPin != null) aiPin.gameObject.SetActive(false); AiFlagShow(-1);
    }

    // in Update, na Aanwijzen: true zolang het startvak gekozen wordt (de klik is dan voor de AI)
    bool AiUpdate()
    {
        if (!aiPick) return false;
        if (Inp.Down(K.Esc)) { aiPick = false; AiFlagShow(-1); Say("AI-bouwer: niet gestart"); return true; }
        ShowBrush(-1); ShowGhost(-1); ShowVerts(-1); ShowPlan(null); prevOk = false;
        hasHover = false;
        if (!OverBar && Physics.Raycast(Cam.ScreenPointToRay(Inp.Pos), out RaycastHit hit, 5000f, Layers.Wijzen, QueryTriggerInteraction.Ignore)) { hover = Planet.Nearest(hit.point - hit.normal * 0.3f); hasHover = true; hoverPoint = hit.point; }
        if (hasHover) ShowOutline(hover); else outline.enabled = false;
        // het voorbeeld: de vlag op het vak onder de muis, amber als hij daar kan beginnen, rood als het niet kan
        aiPickWhy = hasHover ? PlanetAI.CanStart(hover) : null;
        AiFlagShow(hasHover ? hover : -1, hasHover ? (aiPickWhy == null ? 1 : 2) : 0);
        if (hasHover && Inp.LeftDown && camDrag == 0 && !Ui.MouseOverUI)
        {
            string why = PlanetAI.Begin(hover, hover * 31 + 7, aiStyle);
            if (why != null) { Say(why); Sounds.Fx("deny"); }
            else
            {
                aiPick = false; aiOn = true; aiPaused = false; aiFails = 0; aiNext = Time.time + 0.4f; aiMove = null; aiWhat = "kijkt rond"; aiPinCell = hover; aiBeauty0 = beauty != null ? beauty.score : 0;
                aiStartT = Time.time; aiLogged = 0; aiBest = AiBestScore(); aiBaseSnap = null; aiUndoBase = -1; aiUndoN = undo.Count;
                AiFlagShow(hover); Sounds.Fx("place"); Say("De AI-bouwer begint hier, bij zijn vlag: een " + PlanetAI.StyleNames[(int)PlanetAI.Kind].ToLower() + " (menu: pauzeren of stoppen)");
            }
        }
        return true;
    }

    // elk beeld (ook met een venster open of tijdens het lopen): denken in stukjes, de zet tonen, dan doen
    void AiTick()
    {
        if (!aiOn || loading != null) return;
        if (undo.Count < aiUndoN)   // Ctrl+Z van de speler (ook als hij al gepauzeerd was): pauzeren en opnieuw kijken wat er nog van de AI is
        {
            aiUndoN = undo.Count; aiMove = null; AiLines(null); PlanetAI.Resync(); aiFails = 0;
            if (AiBaseIndex() < 0) aiBaseSnap = null;   // tot voor de start van de AI teruggegaan
            if (!aiPaused) Say("De AI-bouwer wacht even: je hebt iets ongedaan gemaakt (Verder: hij gaat door met wat er nog staat)");
            aiPaused = true; aiWhat = "wacht: je hebt iets ongedaan gemaakt (Verder om door te gaan)";
        }
        else if (undo.Count != aiUndoN && aiMove == null) aiUndoN = undo.Count;   // de speler bouwde zelf iets
        AiFollow();
        if (aiPaused) { if (aiUnsaved && !rebuilding) { PlanetCity.Save(); aiUnsaved = false; } return; }   // gepauzeerd of klaar: bewaren wat Turbo nog niet bewaard had
        if (aiMove == null)
        {
            if (Time.time < aiNext) return;
            if (!PlanetAI.Think(AiThinkMs[aiTempo], out var m)) { aiWhat = "denkt na: " + PlanetAI.Status; return; }
            if (m == null)
            {
                aiFails++; aiNext = Time.time + 0.6f;
                if (aiFails >= 3) { aiPaused = true; aiWhat = "klaar: geen plek meer binnen het bereik"; Say("De AI-bouwer is klaar: geen plek meer binnen zijn bereik (" + PlanetAI.Moves + " zetten)"); AiScore("klaar"); }
                return;
            }
            aiFails = 0; aiMove = m; aiShowT = Time.time; aiWhat = m.what; aiPinCell = m.line != null ? m.line[m.line.Count / 2] : m.cell; AiLines(m.line ?? new List<int> { m.cell });
            return;
        }
        if (Time.time - aiShowT < AiPace[aiTempo] * 0.45f) return;   // eerst even laten zien wat er komt
        if (rebuilding) return;   // het vorige moet eerst getekend zijn
        AiExecute(aiMove); aiMove = null; AiLines(null); aiNext = Time.time + AiPace[aiTempo] * 0.55f;
    }
    // één zet, precies zoals een klik of sleep van de speler (zie Update): met de gewone sleepinstellingen
    void AiExecute(PlanetAI.Move m)
    {
        int at = m.line != null ? m.line[m.line.Count / 2] : m.cell;
        byte ks = PlanetCity.DragStyle; bool ko = PlanetCity.DragOneWay, kb = PlanetCity.DragBike, ku = PlanetCity.DragUnder; int kl = PlanetCity.DragLevel;
        PlanetCity.DragStyle = m.style; PlanetCity.DragOneWay = false; PlanetCity.DragBike = m.bike; PlanetCity.DragUnder = false; PlanetCity.DragLevel = 0;
        int bi = AiBaseIndex(); if (bi >= 0 && undo.Count >= 100 && undo.Count - bi >= 2) undo.RemoveAt(bi + 1);   // vol: de oudste AI-stap weg, niet die van de speler
        Push(); int ok = 0; string err = null; long was = AiFingerprint();
        bool first = aiBaseSnap == null; if (first) { aiUndoBase = undo.Count - 1; aiBaseSnap = undo[aiUndoBase]; }   // de eerste zet: dit is de stand van vóór de AI
        try
        {
            if (m.type == "water") { foreach (int c in m.line) if (PlanetCity.Place(c, "water") == null) ok++; }   // de gracht: vak voor vak, zoals slepen met de gracht
            else if (m.line != null && m.type == "weg" && m.line.Exists(PlanetCity.IsRot))   // tot het midden van een rotonde gesleept: de soort weg van de rotonde zelf blijft wat hij was
            {
                var keep = new Dictionary<int, byte>(); foreach (int c in m.line) if (PlanetCity.IsRot(c)) keep[c] = PlanetCity.StyleOf(c);
                ok = PlanetCity.PlaceLine(m.line, m.type); if (ok > 0) { PlanetCity.SetLevel(m.line, 0, m.type); PlanetCity.DipUnder(m.line); PlanetCity.BoreLine(m.line); }
                foreach (var kv in keep) { if (kv.Value == 0) PlanetCity.Style.Remove(kv.Key); else PlanetCity.Style[kv.Key] = kv.Value; PlanetCity.Touch(kv.Key); }
            }
            else if (m.line != null) { ok = PlanetCity.PlaceLine(m.line, m.type); if (ok > 0) { PlanetCity.SetLevel(m.line, 0, m.type); PlanetCity.DipUnder(m.line); PlanetCity.BoreLine(m.line); } }
            else if (m.type == "gum") { if (PlanetCity.Clear(m.cell)) ok = 1; else err = "Dat kon niet weg"; }   // bomen kappen of een eigen stukje opruimen, zoals rechts klikken
            else { err = PlanetCity.Place(m.cell, m.type); if (err == null) ok = 1; }
        }
        finally { PlanetCity.DragStyle = ks; PlanetCity.DragOneWay = ko; PlanetCity.DragBike = kb; PlanetCity.DragUnder = ku; PlanetCity.DragLevel = kl; }
        if (ok > 0 && AiFingerprint() == was) { ok = 0; err = "Dat lag er al"; }   // niets veranderd (bijvoorbeeld een lijn die er al lag): geen zet, en niet nog eens
        if (ok > 0)
        {
            bool hh = hasHover; int hv = hover; Vector3 hp = hoverPoint;
            hasHover = true; hover = at; hoverPoint = Planet.Cells[at].dir * (Planet.R + PlanetCity.G(at));   // het geluid en het tekenen op de plek van de zet
            if (aiTempo < 3) Changed();
            else   // Turbo: zoals Changed, maar de stad niet bij elke zet bewaren (dat kost op een grote planeet veel tijd), wel om de 20 zetten en bij Stop
            {
                Sounds.After(hoverPoint); PlanetCity.UserEdit(); beautyVer++; PlanetCity.RebuildSoon(at); rebuilding = true; RebuildTick(0f); beautyT = -99f;
                if ((PlanetAI.Moves + 1) % 20 == 0) PlanetCity.Save(); else aiUnsaved = true;
            }
            hasHover = hh; hover = hv; hoverPoint = hp;
            PlanetAI.Commit(m); aiUndoN = undo.Count;
            if (PlanetAI.Moves % 50 == 0) AiScore("tussenstand");
        }
        else { undo.RemoveAt(undo.Count - 1); if (first) { aiBaseSnap = null; aiUndoBase = -1; } aiUndoN = undo.Count; PlanetAI.Reject(m); if (err != null) aiWhat = "kon niet: " + err.ToLower(); }   // niets veranderd: ook niets ongedaan te maken
    }
    // ---------- scoreboek: elke run een paar regels in ai-scores.txt (persistentDataPath), om versies van de planner te vergelijken ----------
    static string AiScorePath => System.IO.Path.Combine(Application.persistentDataPath, "ai-scores.txt");
    void AiScore(string why)
    {
        try
        {
            var mt = PlanetAI.Measure(); int sec = Mathf.RoundToInt(Time.time - aiStartT); aiLogged = PlanetAI.Moves;
            // om de 100 zetten, bij Stop en als hij klaar is: ook de plattegrond en de stad in ai-runs (PlanetAI.Eval.cs)
            string file = why != "tussenstand" || PlanetAI.Moves % 100 == 0 ? PlanetAI.Archive(why) : null;
            string line = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ";versie " + PlanetAI.Version + ";" + PlanetAI.Moves + " zetten;schoonheid " + mt.beauty + ";" + PlanetAI.Owned.Count + " vakken;" + sec + " s;" + AiTempoNames[aiTempo].ToLower() + ";" + why + ";stad " + PlanetCity.Slot + ";stijl " + PlanetAI.StyleNames[(int)PlanetAI.Kind].ToLower() + ";" + mt.Line + (file != null ? ";plattegrond ai-runs/" + file + ".png" : "");
            System.IO.File.AppendAllText(AiScorePath, line + "\n");
        }
        catch (System.Exception e) { Debug.LogWarning("Scoreboek van de AI niet bijgewerkt: " + e.Message); }
    }
    // de beste score tot nu toe uit het scoreboek, voor het paneel
    static string AiBestScore()
    {
        try
        {
            if (!System.IO.File.Exists(AiScorePath)) return null; int best = -1; string bestLine = null;
            foreach (var l in System.IO.File.ReadAllLines(AiScorePath))
            {
                var p = l.Split(';'); if (p.Length < 4 || !p[3].StartsWith("schoonheid ") || !int.TryParse(p[3].Substring(11), out int s)) continue;
                if (s > best) { best = s; bestLine = "beste tot nu " + s + " (" + p[1] + ", " + p[2] + ")"; }
            }
            return bestLine;
        }
        catch { return null; }
    }
    // een grove vingerafdruk van de stad: verandert bij elk blok, elke verbinding en elk fietspad
    static long AiFingerprint()
    {
        long h = PlanetCity.Links.Count * 1000003L + PlanetCity.Bike.Count * 7919L + PlanetCity.Cells.Count;
        foreach (var kv in PlanetCity.Cells) h += (kv.Key + 1L) * kv.Value.Count;
        return h;
    }
    // de camera schuift zachtjes mee naar waar hij bouwt, tenzij je zelf de camera beweegt
    void AiFollow()
    {
        if (!aiFollow || aiPinCell < 0 || walking || flying || inSpace || camDrag != 0) return;
        foreach (var k in new[] { K.W, K.A, K.S, K.D, K.Up, K.Down, K.Left, K.Right, K.Q, K.E }) if (Inp.Held(k)) return;
        Vector3 tgt = Planet.Cells[aiPinCell].dir; float m = Vector3.Angle(focus, tgt) * Mathf.Deg2Rad * Planet.R;
        if (m < 25f) return;
        focus = Vector3.Slerp(focus, tgt, 1f - Mathf.Exp(-1.8f * Time.deltaTime)).normalized; fwd = (fwd - focus * Vector3.Dot(fwd, focus)).normalized;
    }

    // ---------- markering: blauwe omtrek op de vakken van de zet, en een draaiende pijl boven het vak ----------
    void AiLines(List<int> cells)
    {
        int n = cells != null ? cells.Count : 0;
        if (aiMat == null) aiMat = Raatstad.MakeMat(AiCol, true);
        while (aiLines.Count < n) { var l = new GameObject("AI-plan").AddComponent<LineRenderer>(); l.loop = true; l.widthMultiplier = 0.4f; l.material = aiMat; l.useWorldSpace = true; aiLines.Add(l); }
        for (int k = 0; k < aiLines.Count; k++)
        {
            if (k >= n) { aiLines[k].enabled = false; continue; }
            int c = cells[k]; var pc = Planet.Cells[c].corners; aiLines[k].enabled = true;
            if (aiLines[k].positionCount != pc.Length) aiLines[k].positionCount = pc.Length;
            float h = PlanetCity.G(c) + PlanetCity.StackHeight(c) + 0.4f;
            for (int q = 0; q < pc.Length; q++) aiLines[k].SetPosition(q, pc[q] * (Planet.R + h));
        }
    }
    // het vlaggetje van de AI op zijn startvak: een paal met een blauwe wimpel, zolang hij bezig is
    // look: 0 de echte vlag (blauw), 1 voorbeeld: hier kan hij beginnen (amber, doorzichtig), 2 voorbeeld: hier niet (rood, doorzichtig)
    Transform aiFlag; Transform aiFlagCloth; int aiFlagLook = -1; static Material aiFlagOk, aiFlagBad, aiFlagPoleGhost;
    void AiFlagShow(int cell, int look = 0)
    {
        if (cell < 0) { if (aiFlag != null) aiFlag.gameObject.SetActive(false); return; }
        if (aiFlag == null)
        {
            if (aiMat == null) aiMat = Raatstad.MakeMat(AiCol, true);
            var go = new GameObject("AI-vlag"); aiFlag = go.transform;
            var pole = new MeshBuilder(); float r = 0.14f, hgt = 8f;   // de paal: een dun zeshoekig prisma
            var lo = new Vector3[6]; var hi = new Vector3[6];
            for (int k = 0; k < 6; k++) { float a = k * Mathf.PI / 3f; lo[k] = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r); hi[k] = lo[k] + Vector3.up * hgt; }
            for (int k = 0; k < 6; k++) { int n = (k + 1) % 6; pole.Quad(lo[k], lo[n], hi[n], hi[k], (lo[k] + lo[n]) * 0.5f); }
            for (int k = 0; k < 6; k++) pole.Tri(new Vector3(0f, hgt, 0f), hi[(k + 1) % 6], hi[k], Vector3.up);
            var pm = go.AddComponent<MeshFilter>(); pm.sharedMesh = pole.Build(); var pr = go.AddComponent<MeshRenderer>(); pr.sharedMaterial = Mats.GetUnlit("ai_paal", new Color(0.95f, 0.95f, 0.93f)); pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var cloth = new GameObject("Wimpel"); cloth.transform.SetParent(aiFlag, false); aiFlagCloth = cloth.transform;   // de wimpel: een driehoek, aan twee kanten zichtbaar
            var cm = new MeshBuilder(); Vector3 t0 = new Vector3(0f, hgt - 0.1f, 0f), t1 = new Vector3(0f, hgt - 1.9f, 0f), tip = new Vector3(2.6f, hgt - 1f, 0f);
            cm.Tri(t0, tip, t1, Vector3.forward); cm.Tri(t0, t1, tip, Vector3.back);
            cloth.AddComponent<MeshFilter>().sharedMesh = cm.Build(); var cr = cloth.AddComponent<MeshRenderer>(); cr.sharedMaterial = aiMat; cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        aiFlag.gameObject.SetActive(true);
        if (look != aiFlagLook)   // de kleuren van het bouwvoorbeeld (zoals ShowGhost): amber kan, rood kan niet
        {
            aiFlagLook = look;
            if (aiFlagOk == null) { aiFlagOk = Raatstad.MakeMat(new Color(0.949f, 0.698f, 0.2f, 0.55f), true); aiFlagBad = Raatstad.MakeMat(new Color(0.878f, 0.341f, 0.243f, 0.55f), true); aiFlagPoleGhost = Raatstad.MakeMat(new Color(0.95f, 0.95f, 0.93f, 0.45f), true); }
            aiFlag.GetComponent<MeshRenderer>().sharedMaterial = look == 0 ? Mats.GetUnlit("ai_paal", new Color(0.95f, 0.95f, 0.93f)) : aiFlagPoleGhost;
            aiFlagCloth.GetComponent<MeshRenderer>().sharedMaterial = look == 0 ? aiMat : look == 1 ? aiFlagOk : aiFlagBad;
        }
        var c = Planet.Cells[cell]; aiFlag.position = c.dir * (Planet.R + PlanetCity.G(cell) + PlanetCity.StackHeight(cell)); aiFlag.rotation = Quaternion.FromToRotation(Vector3.up, c.dir);
    }
    void AiPin()
    {
        if (aiFlagCloth != null && aiFlag.gameObject.activeSelf) aiFlagCloth.localRotation = Quaternion.AngleAxis(Time.time * 40f + Mathf.Sin(Time.time * 3f) * 25f, Vector3.up);   // de wimpel wappert
        if (!aiOn || aiPinCell < 0) { if (aiPin != null) aiPin.gameObject.SetActive(false); return; }
        if (aiPin == null)
        {
            if (aiMat == null) aiMat = Raatstad.MakeMat(AiCol, true);
            var mb = new MeshBuilder(); Vector3 apex = Vector3.zero; float y = 2.4f, r = 1.1f;
            var ring = new Vector3[6]; for (int k = 0; k < 6; k++) { float a = k * Mathf.PI / 3f; ring[k] = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r); }
            for (int k = 0; k < 6; k++) { mb.Tri(apex, ring[k], ring[(k + 1) % 6], (ring[k] + ring[(k + 1) % 6]) * 0.5f - new Vector3(0f, y * 0.5f, 0f)); mb.Tri(new Vector3(0f, y, 0f), ring[(k + 1) % 6], ring[k], Vector3.up); }
            var go = new GameObject("AI-pijl"); go.AddComponent<MeshFilter>().sharedMesh = mb.Build(); var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = aiMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            aiPin = go.transform;
        }
        aiPin.gameObject.SetActive(true);
        var cell = Planet.Cells[aiPinCell]; float h = PlanetCity.G(aiPinCell) + PlanetCity.StackHeight(aiPinCell) + 4f + Mathf.Sin(Time.time * 2.5f) * 0.6f;
        float s = Mathf.Clamp(dist / 110f, 1f, 7f);
        aiPin.position = cell.dir * (Planet.R + h); aiPin.rotation = Quaternion.FromToRotation(Vector3.up, cell.dir) * Quaternion.AngleAxis(Time.time * 60f, Vector3.up); aiPin.localScale = Vector3.one * s;
    }

    // alles wat de AI bouwde in één keer terug: de stand van vóór de AI (en alle stappen erna uit de undo-lijst)
    void AiRevertAll()
    {
        int bi = AiBaseIndex(); if (bi < 0) { Say("Er is niets van de AI-bouwer om terug te draaien"); return; }
        if (PlanetAI.Moves > 0) AiScore("teruggedraaid");
        string u = undo[bi]; undo.RemoveRange(bi, undo.Count - bi);
        PlanetCity.Restore(u); PlanetCity.TouchAll(); Changed(); Sounds.Instead("undo");   // zoals Undo
        int n = PlanetAI.Moves; aiUnsaved = false; AiStop(false); Say("Alles van de AI-bouwer teruggedraaid (" + n + " zetten)");
    }
    // ---------- het paneel bovenin ----------
    void AiGui(float W, float H)
    {
        AiPin();
        if (aiPick)   // het startvak kiezen: uitleg, de bouwstijl en bij de muis of hij daar kan beginnen
        {
            var styles = new string[PlanetAI.StyleNames.Length + 1]; styles[0] = "Zelf kiezen"; for (int k = 0; k < PlanetAI.StyleNames.Length; k++) styles[k + 1] = PlanetAI.StyleNames[k];
            float w = 640f; var r = new Rect((W - w) * 0.5f, 12f, w, 92f); Ui.Panel(r, false, 14f);
            Ui.Tex(new Rect(r.x + 14f, r.y + 12f, 16f, 16f), Ui.Icon("stad"), AiCol);
            Ui.Label(new Rect(r.x + 38f, r.y + 8f, w - 52f, 24f), "AI-BOUWER   Klik op het vak waar hij mag beginnen   ·   Esc: toch niet", Ui.T.Bold, AiCol);
            Ui.Label(new Rect(r.x + 14f, r.y + 50f, 60f, 30f), "STIJL", Ui.T.Tiny);
            int s = Ui.Seg(new Rect(r.x + 64f, r.y + 48f, w - 78f, 32f), styles, aiStyle + 1); if (s >= 0) aiStyle = s - 1;
            if (aiStyle >= 0) Ui.Tip(new Rect(r.x + 64f, r.y + 48f, w - 78f, 32f), PlanetAI.StyleNames[aiStyle], PlanetAI.StyleInfo[aiStyle]);
            if (hasHover && !Ui.MouseOverUI)
            {
                string tip = aiPickWhy ?? "Hier begint hij" + (aiStyle >= 0 ? ": een " + PlanetAI.StyleNames[aiStyle].ToLower() : "");
                var mm = Ui.Mouse; float tw = Ui.Width(tip, Ui.T.Bold) + 24f; var tr = new Rect(Mathf.Min(mm.x + 18f, Ui.W - tw - 8f), mm.y + 14f, tw, 28f);
                Ui.Panel(tr, true, 8f); Ui.Label(tr, tip, Ui.T.Bold, aiPickWhy != null ? new Color(0.94f, 0.48f, 0.42f) : Ui.Signal, TextAnchor.MiddleCenter);
            }
            return;
        }
        if (!aiOn) return;
        float pw = 700f, ph = 126f; var p = new Rect((W - pw) * 0.5f, 12f, pw, ph); Ui.Panel(p, false, 14f);
        float x = p.x + 14f, y = p.y + 10f, iw = pw - 28f;
        Ui.Tex(new Rect(x, y + 2f, 16f, 16f), Ui.Icon("stad"), AiCol); Ui.Label(new Rect(x + 24f, y, 200f, 20f), "AI-BOUWER", Ui.T.Bold, AiCol, null, 12);
        string state = aiPaused ? (aiFails >= 3 ? "klaar" : "gepauzeerd") : aiMove != null ? "bouwt" : "denkt";
        Ui.Label(new Rect(x, y, iw, 20f), PlanetAI.StyleNames[(int)PlanetAI.Kind].ToLower() + " · " + state + " · " + AiTempoNames[aiTempo].ToLower(), Ui.T.Dim, null, TextAnchor.MiddleRight, 11); y += 22f;
        Ui.Label(new Rect(x, y, iw, 20f), string.IsNullOrEmpty(aiWhat) ? "" : char.ToUpper(aiWhat[0]) + aiWhat.Substring(1), Ui.T.Text); y += 22f;
        int sc = beauty != null ? beauty.score : 0, d = sc - aiBeauty0;
        Ui.Label(new Rect(x, y, iw, 18f), PlanetAI.Moves + " zetten · schoonheid " + sc + (d != 0 ? " (" + (d > 0 ? "+" : "−") + Mathf.Abs(d) + ")" : "") + " · bereik " + PlanetAI.RangeCells + " vakken · " + PlanetAI.Owned.Count + " bebouwd" + (aiBest != null ? " · " + aiBest : ""), Ui.T.Dim, null, null, 11); y += 26f;
        float bw = Ui.Width("Verder", Ui.T.Bold) + 24f;
        if (Ui.Button(new Rect(x, y, bw, 30f), aiPaused ? "Verder" : "Pauze", false)) { aiPaused = !aiPaused; if (!aiPaused) aiFails = 0; }
        float sw = Ui.Width("Stop", Ui.T.Bold) + 24f; if (Ui.Button(new Rect(x + bw + 6f, y, sw, 30f), "Stop", false)) AiStop();
        float fw = Ui.Width("Camera volgt", Ui.T.Bold) + 24f; if (Ui.Button(new Rect(x + bw + sw + 12f, y, fw, 30f), "Camera volgt", aiFollow)) aiFollow = !aiFollow;
        float aw = Ui.Width("Alles terug", Ui.T.Bold) + 24f; var ar = new Rect(x + bw + sw + fw + 18f, y, aw, 30f);
        if (Ui.Button(ar, "Alles terug", false, null, aiBaseSnap == null)) AiRevertAll();
        Ui.Tip(ar, "Alles terug", "Alles wat de AI bouwde in één keer ongedaan maken");
        float segW = iw - (bw + sw + fw + aw + 30f); int t2 = Ui.Seg(new Rect(p.xMax - 14f - segW, y, segW, 30f), AiTempoNames, aiTempo); if (t2 >= 0) aiTempo = t2;
    }
}
