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
    float aiNext, aiShowT; PlanetAI.Move aiMove; string aiWhat = "";
    static readonly string[] AiTempoNames = { "Langzaam", "Normaal", "Snel" }; static readonly float[] AiPace = { 2.6f, 1.1f, 0.4f };
    readonly List<LineRenderer> aiLines = new List<LineRenderer>(); Transform aiPin; static Material aiMat;
    static readonly Color AiCol = new Color(0.35f, 0.76f, 1f);

    string AiCardSub => aiPick ? "Klik een vak aan waar hij begint" : !aiOn ? "Een AI bouwt een stad op een vak dat jij aanwijst" : aiPaused ? "Gepauzeerd: klik om verder te gaan" : "Bezig: klik om te pauzeren";

    // in het menu, onder Gereedschap
    void AiMenuCards(float x, float y, float half)
    {
        if (Ui.CardButton(new Rect(x, y, half, 48f), aiOn ? (aiPaused ? "AI-bouwer verder" : "AI-bouwer pauzeren") : "AI-bouwer", AiCardSub, aiOn && !aiPaused, "stad")) { AiButton(); menuOpen = false; }
        if (aiOn) { if (Ui.CardButton(new Rect(x + half + 8f, y, half, 48f), "AI-bouwer stoppen", PlanetAI.Moves + " zetten gedaan", false, "sluit")) { AiStop(); menuOpen = false; } }
        else
        {
            var sz = new Rect(x + half + 8f, y, half, 48f); Ui.Label(new Rect(sz.x + 2f, sz.y, sz.width, 16f), "TEMPO AI-BOUWER", Ui.T.Tiny);
            int t = Ui.Seg(new Rect(sz.x, sz.y + 18f, sz.width, 30f), AiTempoNames, aiTempo); if (t >= 0) aiTempo = t;
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
    void AiStop()
    {
        if (aiOn && PlanetAI.Moves > 0) AiScore("gestopt");
        aiOn = aiPaused = aiPick = false; aiMove = null;
        Say("AI-bouwer gestopt na " + PlanetAI.Moves + " zetten (Ctrl+Z haalt ze één voor één weg)");
        PlanetAI.Reset(); AiLines(null); aiPinCell = -1; if (aiPin != null) aiPin.gameObject.SetActive(false); AiFlagShow(-1);
    }

    // in Update, na Aanwijzen: true zolang het startvak gekozen wordt (de klik is dan voor de AI)
    bool AiUpdate()
    {
        if (!aiPick) return false;
        if (Inp.Down(K.Esc)) { aiPick = false; Say("AI-bouwer: niet gestart"); return true; }
        ShowBrush(-1); ShowGhost(-1); ShowVerts(-1); ShowPlan(null); prevOk = false;
        hasHover = false;
        if (!OverBar && Physics.Raycast(Cam.ScreenPointToRay(Inp.Pos), out RaycastHit hit, 5000f, Layers.Wijzen, QueryTriggerInteraction.Ignore)) { hover = Planet.Nearest(hit.point - hit.normal * 0.3f); hasHover = true; hoverPoint = hit.point; }
        if (hasHover) ShowOutline(hover); else outline.enabled = false;
        if (hasHover && Inp.LeftDown && camDrag == 0 && !Ui.MouseOverUI)
        {
            string why = PlanetAI.Begin(hover, hover * 31 + 7);
            if (why != null) { Say(why); Sounds.Fx("deny"); }
            else
            {
                aiPick = false; aiOn = true; aiPaused = false; aiFails = 0; aiNext = Time.time + 0.4f; aiMove = null; aiWhat = "kijkt rond"; aiPinCell = hover; aiBeauty0 = beauty != null ? beauty.score : 0;
                aiStartT = Time.time; aiLogged = 0; aiBest = AiBestScore();
                AiFlagShow(hover); Sounds.Fx("place"); Say("De AI-bouwer begint hier, bij zijn vlag (menu: pauzeren of stoppen)");
            }
        }
        return true;
    }

    // elk beeld (ook met een venster open of tijdens het lopen): denken in stukjes, de zet tonen, dan doen
    void AiTick()
    {
        if (!aiOn || loading != null) return;
        AiFollow();
        if (aiPaused) return;
        if (aiMove == null)
        {
            if (Time.time < aiNext) return;
            if (!PlanetAI.Think(3f, out var m)) { aiWhat = "denkt na: " + PlanetAI.Status; return; }
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
        Push(); int ok = 0; string err = null; long was = AiFingerprint();
        try
        {
            if (m.type == "water") { foreach (int c in m.line) if (PlanetCity.Place(c, "water") == null) ok++; }   // de gracht: vak voor vak, zoals slepen met de gracht
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
            Changed();
            hasHover = hh; hover = hv; hoverPoint = hp;
            PlanetAI.Commit(m);
            if (PlanetAI.Moves % 50 == 0) AiScore("tussenstand");
        }
        else { undo.RemoveAt(undo.Count - 1); PlanetAI.Reject(m); if (err != null) aiWhat = "kon niet: " + err.ToLower(); }   // niets veranderd: ook niets ongedaan te maken
    }
    // ---------- scoreboek: elke run een paar regels in ai-scores.txt (persistentDataPath), om versies van de planner te vergelijken ----------
    static string AiScorePath => System.IO.Path.Combine(Application.persistentDataPath, "ai-scores.txt");
    void AiScore(string why)
    {
        try
        {
            var b = Beauty.Compute(); int sec = Mathf.RoundToInt(Time.time - aiStartT); aiLogged = PlanetAI.Moves;
            string line = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ";versie " + PlanetAI.Version + ";" + PlanetAI.Moves + " zetten;schoonheid " + b.score + ";" + PlanetAI.Owned.Count + " vakken;" + sec + " s;" + AiTempoNames[aiTempo].ToLower() + ";" + why + ";stad " + PlanetCity.Slot;
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
    Transform aiFlag; Transform aiFlagCloth;
    void AiFlagShow(int cell)
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

    // ---------- het paneel bovenin ----------
    void AiGui(float W, float H)
    {
        AiPin();
        if (aiPick)
        {
            string t = "AI-BOUWER   Klik op het vak waar hij mag beginnen   ·   Esc: toch niet"; float w = Ui.Width(t, Ui.T.Bold) + 40f; var r = new Rect((W - w) * 0.5f, 12f, w, 36f);
            Ui.Panel(r, false, 18f); Ui.Label(r, t, Ui.T.Bold, AiCol, TextAnchor.MiddleCenter); return;
        }
        if (!aiOn) return;
        float pw = 520f, ph = 126f; var p = new Rect((W - pw) * 0.5f, 12f, pw, ph); Ui.Panel(p, false, 14f);
        float x = p.x + 14f, y = p.y + 10f, iw = pw - 28f;
        Ui.Tex(new Rect(x, y + 2f, 16f, 16f), Ui.Icon("stad"), AiCol); Ui.Label(new Rect(x + 24f, y, 200f, 20f), "AI-BOUWER", Ui.T.Bold, AiCol, null, 12);
        string state = aiPaused ? (aiFails >= 3 ? "klaar" : "gepauzeerd") : aiMove != null ? "bouwt" : "denkt";
        Ui.Label(new Rect(x, y, iw, 20f), state + " · " + AiTempoNames[aiTempo].ToLower(), Ui.T.Dim, null, TextAnchor.MiddleRight, 11); y += 22f;
        Ui.Label(new Rect(x, y, iw, 20f), string.IsNullOrEmpty(aiWhat) ? "" : char.ToUpper(aiWhat[0]) + aiWhat.Substring(1), Ui.T.Text); y += 22f;
        int sc = beauty != null ? beauty.score : 0, d = sc - aiBeauty0;
        Ui.Label(new Rect(x, y, iw, 18f), PlanetAI.Moves + " zetten · schoonheid " + sc + (d != 0 ? " (" + (d > 0 ? "+" : "−") + Mathf.Abs(d) + ")" : "") + " · bereik " + PlanetAI.RangeCells + " vakken · " + PlanetAI.Owned.Count + " bebouwd" + (aiBest != null ? " · " + aiBest : ""), Ui.T.Dim, null, null, 11); y += 26f;
        float bw = Ui.Width("Verder", Ui.T.Bold) + 24f;
        if (Ui.Button(new Rect(x, y, bw, 30f), aiPaused ? "Verder" : "Pauze", false)) { aiPaused = !aiPaused; if (!aiPaused) aiFails = 0; }
        float sw = Ui.Width("Stop", Ui.T.Bold) + 24f; if (Ui.Button(new Rect(x + bw + 6f, y, sw, 30f), "Stop", false)) AiStop();
        float fw = Ui.Width("Camera volgt", Ui.T.Bold) + 24f; if (Ui.Button(new Rect(x + bw + sw + 12f, y, fw, 30f), "Camera volgt", aiFollow)) aiFollow = !aiFollow;
        float segW = iw - (bw + sw + fw + 24f); int t2 = Ui.Seg(new Rect(p.xMax - 14f - segW, y, segW, 30f), AiTempoNames, aiTempo); if (t2 >= 0) aiTempo = t2;
    }
}
