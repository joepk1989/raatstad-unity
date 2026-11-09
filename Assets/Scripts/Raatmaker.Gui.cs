using UnityEngine;

// Raatmaker: het scherm (in de stijl van Ui.cs) en de toetsen.
public partial class Raatmaker
{
    // gereedschap, korte naam, toets, uitleg; null = scheiding tussen groepen
    static readonly string[][] Tools = {
        new[] { "select", "Kies", "V", "Kiezen: klik iets aan om het te bekijken, te veranderen of weg te halen; sleep een voorwerp om het te verplaatsen" },
        null,
        new[] { "floor", "Vloer", "F", "Vloer: schilder driehoeken (slepen kan); rechts klikken wist. N wisselt het penseel" },
        new[] { "fill", "Vul", "G", "Vullen: een ruimte tot aan de muren krijgt deze vloer; rechts klikken maakt hem leeg" },
        null,
        new[] { "wall", "Muur", "W", "Muur: sleep van rasterpunt naar rasterpunt (ook haaks op het raster). Haag en hek zijn laag" },
        new[] { "room", "Kamer", "M", "Kamer: klik op een vak, er komen muren op alle zes zijden" },
        new[] { "door", "Deur", "D", "Deur: klik op een muur" },
        new[] { "window", "Raam", "R", "Raam: klik op een muur" },
        new[] { "gate", "Poort", "P", "Poort: klik op een muur" },
        new[] { "open", "Open", "O", "Open doorgang: klik op een muur" },
        new[] { "close", "Dicht", "C", "Dicht: klik op een muur om de opening weg te halen" },
        null,
        new[] { "stair", "Trap", "T", "Trap: wijs een driehoekje aan en klik (3 zijden, 16 treden), of sleep voor een andere lengte of richting" },
        new[] { "ustair", "Bordes", "U", "Bordestrap: sleep de eerste steek; op het bordes keer je en loopt de tweede steek ernaast terug omhoog" },
        new[] { "spiral", "Wentel", "J", "Wenteltrap: klik op een rasterpunt (het midden); sleep naar de kant waar je onderaan instapt" },
        new[] { "ramp", "Helling", "L", "Helling: sleep van onderaan omhoog" },
        new[] { "hexramp", "Schans", "", "Schans: klik in een vak, vlak bij de zijde waar de helling onderaan begint; het hele vak loopt 3 m op" },
        null,
        new[] { "roof", "Dak", "K", "Dak: klik op een vak; het dak volgt de muren van dat vak. Nog eens klikken haalt het weg" },
        new[] { "object", "Ding", "B", "Voorwerp: klik om neer te zetten (Q/E draait), klik erop om 30° te draaien; banken en kasten gaan tegen de muur (Ctrl: los)" },
        null,
        new[] { "erase", "Gum", "X", "Gum: klik of sleep; wat rood oplicht gaat weg (voorwerp, muur, trap, dak, dan vloer)" },
    };
    static GUIStyle field, wrap;
    void Styles()
    {
        if (field != null) return;
        field = new GUIStyle { font = Resources.Load<Font>("Fonts/AtkinsonHyperlegible-Bold"), fontSize = 14, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip, padding = new RectOffset(10, 10, 0, 0) };
        field.normal.textColor = field.focused.textColor = field.hover.textColor = Ui.Ink;
        wrap = new GUIStyle { font = Resources.Load<Font>("Fonts/AtkinsonHyperlegible-Regular"), fontSize = 11, wordWrap = true, richText = false }; wrap.normal.textColor = Ui.InkDim;
    }
    string ToolName(string t) { foreach (var x in Tools) if (x != null && x[0] == t) return x[1]; return t; }
    string ToolHint(string t) { foreach (var x in Tools) if (x != null && x[0] == t) return x[3]; return ""; }

    void OnGUI()
    {
        if (!open) return;
        Styles(); Ui.Begin(); float W = Ui.W, H = Ui.H;
        Keys();
        TopBar(W); ToolBar(H); OptionPanel(W, H); LevelBar(H);
        // onderaan: wat het gereedschap doet, en meldingen
        string hint = ToolHint(tool); var hr = new Rect(400f, H - 12f - 40f, W - 400f - 260f, 40f);
        if (hr.width > 200f) { Ui.Panel(hr, false, 12f); GUI.Label(new Rect(hr.x + 12f, hr.y + 5f, hr.width - 24f, hr.height - 8f), hint, wrap); }
        if (Time.unscaledTime < msgT) Ui.Toast(msg, Mathf.Clamp01((msgT - Time.unscaledTime) / 0.5f), 12f + 40f + 10f);
        if (confirmClose) ConfirmGui(W, H);
        Ui.End();
    }

    void TopBar(float W)
    {
        var r = new Rect(12f, 12f, W - 24f, 48f); Ui.Panel(r, true, 14f);
        Ui.Label(new Rect(r.x + 16f, r.y, 110f, r.height), "RAATMAKER", Ui.T.Tiny, Ui.Signal, TextAnchor.MiddleLeft, 12);
        var nf = new Rect(r.x + 120f, r.y + 9f, 250f, 30f); Ui.Box(nf, new Color(1f, 1f, 1f, 0.07f), Ui.Line, 8f); Ui.Hit(nf);
        GUI.SetNextControlName("modnaam"); string nn = GUI.TextField(nf, m.name ?? "", 48, field); if (nn != m.name) { m.name = nn; dirty = true; }
        Ui.Tip(nf, "Naam", "Klik om de naam te veranderen");
        var cb = new Rect(nf.xMax + 8f, r.y + 9f, 140f, 30f);
        if (Ui.Button(cb, m.cat ?? "Overig")) { int i = System.Array.IndexOf(MV2.Cats, m.cat); m.cat = MV2.Cats[(i + 1) % MV2.Cats.Length]; dirty = true; }
        Ui.Tip(cb, "Soort", "Klik voor de volgende soort (Wonen, Werken, Winkels …)");
        int k = Ui.Seg(new Rect(cb.xMax + 8f, r.y + 7f, 170f, 34f), new[] { "1 vak", "7 vakken" }, m.k);
        if (k >= 0 && k != m.k) { Snap(); m.k = k; Changed(); Fit(); Say(k == 1 ? "7 vakken: het vak en de zes eromheen" : "1 vak"); }
        // rechts
        float x = r.xMax - 10f;
        Rect B(float w) { x -= w; var b = new Rect(x, r.y + 9f, w, 30f); x -= 6f; return b; }
        var bc = B(84f); if (Ui.Button(bc, "Sluiten")) { if (dirty) confirmClose = true; else Close(); } Ui.Tip(bc, "Sluiten", dirty ? "Er is iets niet bewaard: je krijgt eerst de vraag" : "Terug naar de stad", "Esc");
        var bs = B(96f); if (Ui.Button(bs, "Bewaren", true)) Save(false); Ui.Tip(bs, "Bewaren", string.IsNullOrEmpty(origId) ? "Als nieuwe module in de bibliotheek (Eigen)" : "Alle exemplaren in de stad passen zich aan", "Ctrl+S");
        var bk = B(96f); if (Ui.Button(bk, "Als kopie")) Save(true); Ui.Tip(bk, "Als kopie bewaren", "Een nieuwe module; het origineel blijft zoals het was");
        x -= 10f;
        var b3 = B(44f); if (Ui.Button(b3, "3D", v3d)) { v3d = true; meshDirty = true; } Ui.Tip(b3, "3D", "Draaien: middelste muisknop of Alt slepen; schuiven: rechts of Shift slepen; wiel zoomt", "3");
        var b2 = B(44f); if (Ui.Button(b2, "2D", !v3d)) { v3d = false; meshDirty = true; } Ui.Tip(b2, "Bovenaanzicht", "Recht van boven, tot en met deze verdieping", "2");
        var bf = B(74f); if (Ui.Button(bf, "Passend")) Fit(); Ui.Tip(bf, "Passend", "De hele module in beeld");
        x -= 10f;
        var br = B(78f); if (Ui.Button(br, "Opnieuw", false, null, redoS.Count == 0)) Redo(); Ui.Tip(br, "Opnieuw", null, "Ctrl+Y");
        var bu = B(44f); if (Ui.Button(bu, null, false, "ongedaan", undoS.Count == 0)) Undo(); Ui.Tip(bu, "Ongedaan maken", null, "Ctrl+Z");
    }

    void ToolBar(float H)
    {
        int nT = 0, nS = 0; foreach (var t in Tools) if (t == null) nS++; else nT++;
        float bh = Mathf.Min(32f, (H - 72f - 70f - nS * 8f - 16f) / nT), y = 72f;
        var r = new Rect(12f, y, 76f, nT * (bh + 2f) + nS * 8f + 12f); Ui.Panel(r, true, 14f); y += 6f;
        foreach (var t in Tools)
        {
            if (t == null) { Ui.Box(new Rect(r.x + 12f, y + 3f, r.width - 24f, 1f), Ui.Line, Color.clear, 0f); y += 8f; continue; }
            var b = new Rect(r.x + 6f, y, r.width - 12f, bh); y += bh + 2f;
            if (Ui.Button(b, t[1], tool == t[0], null, false, 8f)) SetTool(t[0]);
            if (t[2] != "") Ui.Label(new Rect(b.xMax - 14f, b.y + 1f, 12f, 12f), t[2], Ui.T.Tiny, tool == t[0] ? Ui.SignalInk : Ui.InkDim, TextAnchor.UpperRight, 8);
            Ui.Tip(b, t[3].Split(':')[0], t[3].Contains(":") ? t[3].Substring(t[3].IndexOf(':') + 1).Trim() : null, t[2] != "" ? t[2] : null);
        }
    }
    void SetTool(string t) { tool = t; dragKind = null; if (t != "select") selOn = false; prevKey = null; }

    // ---- rechts: de keuzes bij het gereedschap ----
    void OptionPanel(float W, float H)
    {
        float pw = 236f; var r = new Rect(W - 12f - pw, 72f, pw, H - 72f - 64f); Ui.Panel(r, true, 14f);
        float x = r.x + 12f, y = r.y + 10f, iw = pw - 24f;
        Ui.Label(new Rect(x, y, iw, 22f), (tool == "object" ? "Voorwerp" : ToolName(tool)).ToUpper(), Ui.T.Tiny, Ui.Signal, null, 11); y += 26f;
        switch (tool)
        {
            case "floor": case "fill":
                y = Swatches(x, y, iw, MV2.Floors, MV2.FloorNames, fmat, s => fmat = s);
                if (tool == "floor") { y += 6f; Ui.Label(new Rect(x, y, iw, 18f), "Penseel (N)", Ui.T.Dim, null, null, 11); y += 20f; y = Choice(x, y, iw, Brushes, BrushNames, brush, s => brush = s, 2); }
                break;
            case "wall": case "room": y = Swatches(x, y, iw, MV2.Walls, MV2.WallNames, wmat, s => wmat = s); break;
            case "stair": case "ustair":
                y = Choice(x, y, iw, new[] { "1.95", "3.9" }, new[] { "1 rij · 1,95 m", "2 rijen · 3,9 m" }, sw >= 3f ? "3.9" : "1.95", s => sw = s == "3.9" ? 3.9f : 1.95f, 1);
                y += 6f;
                if (tool == "stair") y = Choice(x, y, iw, new[] { "1", "0", "-1" }, new[] { "Links van de lijn", "Op de lijn", "Rechts van de lijn" }, ssd.ToString(), s => ssd = int.Parse(s), 1);
                else y = Choice(x, y, iw, new[] { "1", "-1" }, new[] { "Keert naar links", "Keert naar rechts" }, usd.ToString(), s => usd = int.Parse(s), 1);
                break;
            case "spiral": y = Choice(x, y, iw, new[] { "1", "-1" }, new[] { "Linksom omhoog", "Rechtsom omhoog" }, wsd.ToString(), s => wsd = int.Parse(s), 1); break;
            case "ramp": y = Choice(x, y, iw, new[] { "1.2", "2", "3" }, new[] { "Smal 1,2 m", "Breed 2 m", "Extra breed 3 m" }, rampW.ToString(System.Globalization.CultureInfo.InvariantCulture), s => rampW = float.Parse(s, System.Globalization.CultureInfo.InvariantCulture), 1); break;
            case "roof": y = Choice(x, y, iw, RoofKinds, RoofNames, rkind, s => rkind = s, 1); break;
            case "object":
                {
                    var rr = new Rect(x, y, iw, 30f); float w3 = (iw - 8f) / 3f;
                    if (Ui.Button(new Rect(x, y, w3, 28f), "−30°")) orot = Mathf.Repeat(orot - Mathf.PI / 6f, Mathf.PI * 2f);
                    Ui.Label(new Rect(x + w3 + 4f, y, w3, 28f), Mathf.RoundToInt(orot * Mathf.Rad2Deg) + "°", Ui.T.Bold, null, TextAnchor.MiddleCenter);
                    if (Ui.Button(new Rect(x + 2f * (w3 + 4f), y, w3, 28f), "+30°")) orot = Mathf.Repeat(orot + Mathf.PI / 6f, Mathf.PI * 2f);
                    y += 34f; y = Choice(x, y, iw, MV2.Objects, MV2.ObjectNames, obj, s => obj = s, 2, r.yMax - 8f); break;
                }
            case "erase": y = Choice(x, y, iw, Brushes, BrushNames, brush, s => brush = s, 2); Ui.Label(new Rect(x, y + 4f, iw, 18f), "(penseel: alleen bij de vloer)", Ui.T.Dim, null, null, 10); break;
            case "select": SelectPanel(x, y, iw); break;
            case "door": case "window": case "gate": case "open": case "close": GUI.Label(new Rect(x, y, iw, 60f), "Klik op een muur van deze verdieping. Het voorbeeld toont de muur zoals hij wordt.", wrap); break;
            case "hexramp": GUI.Label(new Rect(x, y, iw, 60f), "Een vak dat in zijn geheel 3 m oploopt, van de zijde waar je klikt naar de overkant.", wrap); break;
        }
    }
    void SelectPanel(float x, float y, float iw)
    {
        if (!selOn) { GUI.Label(new Rect(x, y, iw, 60f), "Klik in de module op een voorwerp, muur, trap, dak of vloer.", wrap); return; }
        var pk = sel; string nm = pk.Name ?? ""; Ui.Label(new Rect(x, y, iw, 20f), char.ToUpper(nm.Length > 0 ? nm[0] : ' ') + (nm.Length > 1 ? nm.Substring(1) : ""), Ui.T.Bold); y += 26f;
        if (Ui.Button(new Rect(x, y, iw, 28f), "Weghalen (Delete)")) { Snap(); RemovePick(pk); selOn = false; Changed(); return; }
        y += 36f;
        if (pk.kind == "o" && pk.idx < Lvl.o.Count)
        {
            float w2 = (iw - 6f) / 2f; var O = Lvl.o[pk.idx];
            if (Ui.Button(new Rect(x, y, w2, 28f), "−30° (Q)")) { Snap(); O.rot = Mathf.Repeat(O.rot - Mathf.PI / 6f, Mathf.PI * 2f); Changed(); }
            if (Ui.Button(new Rect(x + w2 + 6f, y, w2, 28f), "+30° (E)")) { Snap(); O.rot = Mathf.Repeat(O.rot + Mathf.PI / 6f, Mathf.PI * 2f); Changed(); }
            y += 36f; GUI.Label(new Rect(x, y, iw, 40f), "Sleep het voorwerp om het te verplaatsen.", wrap);
        }
        else if (pk.kind == "w" && Lvl.w.TryGetValue(pk.key, out var W))
        {
            y = Choice(x, y, iw, new[] { "", "door", "window", "gate", "open" }, new[] { "Dicht", "Deur", "Raam", "Poort", "Open" }, W.o ?? "", s => { Snap(); W.o = s; Changed(); sel.Name = MV2.NameOf(MV2.Walls, MV2.WallNames, W.m) + (s == "" ? "" : " met " + OpeningName(s).ToLower()); }, 3);
            y += 6f; Swatches(x, y, iw, MV2.Walls, MV2.WallNames, W.m, s => { Snap(); W.m = s; Changed(); });
        }
        else if (pk.kind == "f" && Lvl.f.TryGetValue(pk.key, out var fm))
        {
            var key = pk.key; y = Swatches(x, y, iw, MV2.Floors, MV2.FloorNames, fm, s => { Snap(); Lvl.f[key] = s; Changed(); });
            GUI.Label(new Rect(x, y + 4f, iw, 40f), "Tip: met Vullen (G) krijgt de hele ruimte een andere vloer.", wrap);
        }
        else if (pk.kind == "r" && Lvl.r.TryGetValue(pk.cell, out var rk)) { var c = pk.cell; Choice(x, y, iw, RoofKinds, RoofNames, rk, s => { Snap(); Lvl.r[c] = s; Changed(); }, 1); }
    }
    // kleurvakjes (materialen), 4 per rij; de naam bij aanwijzen
    float Swatches(float x, float y, float iw, string[] keys, string[] names, string cur, System.Action<string> pick)
    {
        const int per = 4; float w = (iw - (per - 1) * 6f) / per, h = 34f;
        for (int i = 0; i < keys.Length; i++)
        {
            var b = new Rect(x + (i % per) * (w + 6f), y + (i / per) * (h + 18f), w, h); bool on = keys[i] == cur;
            if (Ui.Button(b, null, on, null, false, 8f)) pick(keys[i]);
            var c = MV2.Col(keys[i]); c.a = 1f; Ui.Box(new Rect(b.x + 4f, b.y + 4f, b.width - 8f, b.height - 8f), c, new Color(0, 0, 0, 0.3f), 5f);
            string nm = names[i]; Ui.Label(new Rect(b.x - 4f, b.yMax + 1f, b.width + 8f, 14f), nm.Length > 11 ? nm.Substring(0, 10) + "…" : nm, Ui.T.Dim, on ? Ui.Signal : null, TextAnchor.MiddleCenter, 9);
            Ui.Tip(b, nm);
        }
        return y + Mathf.Ceil(keys.Length / (float)per) * (h + 18f);
    }
    // keuzeknoppen, per rijen van 'per'
    float Choice(float x, float y, float iw, string[] keys, string[] names, string cur, System.Action<string> pick, int per, float maxY = 1e9f)
    {
        float w = (iw - (per - 1) * 4f) / per, h = 24f;
        for (int i = 0; i < keys.Length; i++)
        {
            var b = new Rect(x + (i % per) * (w + 4f), y + (i / per) * (h + 3f), w, h); if (b.yMax > maxY) break;
            if (Ui.Button(b, names[i], keys[i] == cur, null, false, 7f)) pick(keys[i]);
        }
        return y + Mathf.Ceil(keys.Length / (float)per) * (h + 3f);
    }

    // ---- linksonder: verdiepingen ----
    void LevelBar(float H)
    {
        var r = new Rect(12f, H - 12f - 40f, 378f, 40f); Ui.Panel(r, true, 12f);
        var bd = new Rect(r.x + 6f, r.y + 5f, 30f, 30f); if (Ui.Button(bd, "▼", false, null, lv == 0)) SetLevel(lv - 1); Ui.Tip(bd, "Verdieping lager", null, "PgDn");
        Ui.Label(new Rect(bd.xMax + 4f, r.y, 128f, r.height), (lv == 0 ? "Begane grond" : lv + "e verdieping") + " · " + m.lv.Count, Ui.T.Bold, null, TextAnchor.MiddleCenter);
        var bu = new Rect(bd.xMax + 136f, r.y + 5f, 30f, 30f); if (Ui.Button(bu, "▲", false, null, lv >= MV2.MAX_LV - 1)) SetLevel(lv + 1); Ui.Tip(bu, "Verdieping hoger", "Een nieuwe verdieping komt er vanzelf bij", "PgUp");
        var bk = new Rect(bu.xMax + 8f, r.y + 5f, 76f, 30f); if (Ui.Button(bk, "Kopie ↑", false, null, lv >= MV2.MAX_LV - 1)) CopyUp(); Ui.Tip(bk, "Kopie naar boven", "Vloer en muren van deze verdieping naar de verdieping erboven");
        var bt = new Rect(bk.xMax + 6f, r.y + 5f, 82f, 30f); if (Ui.Button(bt, "Tot hier", upto, null, !v3d)) { upto = !upto; meshDirty = true; } Ui.Tip(bt, "Tot hier", "In 3D alleen tot en met deze verdieping");
    }

    void ConfirmGui(float W, float H)
    {
        Ui.DimBack(0.45f); var r = new Rect((W - 420f) * 0.5f, (H - 150f) * 0.5f, 420f, 150f); Ui.Panel(r, true, 16f);
        Ui.Label(new Rect(r.x + 20f, r.y + 16f, r.width - 40f, 26f), "Niet bewaard", Ui.T.Bold, null, null, 16);
        GUI.Label(new Rect(r.x + 20f, r.y + 46f, r.width - 40f, 40f), "Je wijzigingen aan \"" + m.name + "\" zijn nog niet bewaard.", wrap);
        float bw = (r.width - 40f - 16f) / 3f, y = r.yMax - 46f;
        if (Ui.Button(new Rect(r.x + 20f, y, bw, 30f), "Bewaren", true)) { Save(false); Close(); }
        if (Ui.Button(new Rect(r.x + 20f + bw + 8f, y, bw, 30f), "Niet bewaren")) { dirty = false; Close(); }
        if (Ui.Button(new Rect(r.x + 20f + 2f * (bw + 8f), y, bw, 30f), "Annuleren")) confirmClose = false;
    }

    // ---- toetsen (via de IMGUI-gebeurtenissen; niet terwijl je de naam typt) ----
    void Keys()
    {
        var e = Event.current; if (e.type != EventType.KeyDown || e.keyCode == KeyCode.None) return;
        if (GUIUtility.keyboardControl != 0) { if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.Escape || e.keyCode == KeyCode.KeypadEnter) { GUIUtility.keyboardControl = 0; e.Use(); } return; }
        bool ctl = e.control || e.command; var k = e.keyCode;
        if (confirmClose) { if (k == KeyCode.Escape) { confirmClose = false; e.Use(); } return; }
        if (ctl && k == KeyCode.Z) { if (e.shift) Redo(); else Undo(); e.Use(); return; }
        if (ctl && k == KeyCode.Y) { Redo(); e.Use(); return; }
        if (ctl && k == KeyCode.S) { Save(false); e.Use(); return; }
        if (ctl) return;
        if (k == KeyCode.Escape) { if (dragKind != null) dragKind = null; else if (selOn) selOn = false; else if (dirty) confirmClose = true; else Close(); e.Use(); return; }
        if (k == KeyCode.Alpha2) { v3d = false; meshDirty = true; e.Use(); return; }
        if (k == KeyCode.Alpha3) { v3d = true; meshDirty = true; e.Use(); return; }
        if (k == KeyCode.PageUp || k == KeyCode.KeypadPlus || k == KeyCode.Equals) { SetLevel(lv + 1); e.Use(); return; }
        if (k == KeyCode.PageDown || k == KeyCode.KeypadMinus || k == KeyCode.Minus) { SetLevel(lv - 1); e.Use(); return; }
        if (k == KeyCode.N) { int i = System.Array.IndexOf(Brushes, brush); brush = Brushes[(i + 1) % Brushes.Length]; Say("Penseel: " + BrushNames[System.Array.IndexOf(Brushes, brush)]); e.Use(); return; }
        if ((k == KeyCode.Delete || k == KeyCode.Backspace) && tool == "select" && selOn) { Snap(); RemovePick(sel); selOn = false; Changed(); e.Use(); return; }
        if (k == KeyCode.Q || k == KeyCode.E)
        {
            float d = (k == KeyCode.Q ? -1f : 1f) * Mathf.PI / 6f;
            if (tool == "select" && selOn && sel.kind == "o" && sel.idx < Lvl.o.Count) { Snap(); Lvl.o[sel.idx].rot = Mathf.Repeat(Lvl.o[sel.idx].rot + d, Mathf.PI * 2f); Changed(); }
            else if (tool == "object") { orot = Mathf.Repeat(orot + d, Mathf.PI * 2f); Say("Draaiing " + Mathf.RoundToInt(orot * Mathf.Rad2Deg) + "°"); }
            e.Use(); return;
        }
        string ch = k.ToString(); if (ch.Length != 1) return;
        foreach (var t in Tools) if (t != null && t[2] == ch) { SetTool(t[0]); Say(t[1]); e.Use(); return; }
    }
}
