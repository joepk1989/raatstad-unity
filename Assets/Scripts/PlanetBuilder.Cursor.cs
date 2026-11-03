using System.Collections.Generic;
using UnityEngine;

// Aanwijzen (V), zoals 15g-aanwijzen.js van de webversie: niet bouwen maar aanwijzen. Het vak onder de muis licht op met
// zijn naam; een klik opent opties (meer hiervan, bovenste weg, vak leegmaken, hierheen, lopen vanaf hier). Rechts klikken
// opent "Oproepen": een meteoor, ander weer of een dorpje op die plek. Buiten Aanwijzen maakt rechts klikken een vak leeg.
public partial class PlanetBuilder
{
    bool cursorMode; int cursorCell = -1, panelCell = -1, callCell = -1; Vector2 panelAt, cursorRightAt; bool cursorRight;
    string cursorBar;   // het blok van de bouwbalk bij het aanzetten: een ander blok kiezen zet Aanwijzen uit

    void SetCursorMode(bool on)
    {
        cursorMode = on; panelCell = callCell = -1; cursorBar = on ? CurBlock + slot : null;
        if (on) { eraser = false; catOpen = wheelOpen = false; Say("Aanwijzen (V): klik voor opties, rechts klikken roept iets op (een meteoor, ander weer)"); }
        else Say("Bouwen");
    }

    // in Update, na de ruimte: true = Aanwijzen is aan, de bouwbediening slaat over
    bool CursorUpdate()
    {
        if (Inp.Down(K.V)) SetCursorMode(!cursorMode);
        if (!cursorMode) return false;
        if (CurBlock + slot != cursorBar) { SetCursorMode(false); return false; }   // een blok gekozen (1–6, de balk, het wiel): weer bouwen
        if (Inp.Down(K.Esc)) { if (panelCell >= 0 || callCell >= 0) panelCell = callCell = -1; else SetCursorMode(false); return true; }
        ShowBrush(-1); ShowGhost(-1); ShowVerts(-1); ShowPlan(null); prevOk = false;
        cursorCell = -1;
        if (!OverBar && Physics.Raycast(Cam.ScreenPointToRay(Inp.Pos), out RaycastHit hit, 5000f, Layers.Wijzen, QueryTriggerInteraction.Ignore))
            cursorCell = Planet.Nearest(hit.point - hit.normal * 0.3f);
        int show = panelCell >= 0 ? panelCell : callCell >= 0 ? callCell : cursorCell;   // met een venster open blijft dat vak oplichten
        if (show >= 0) ShowOutline(show); else outline.enabled = false;
        hover = cursorCell; hasHover = cursorCell >= 0;
        if (Inp.LeftDown && camDrag == 0 && !Ui.MouseOverUI) { callCell = -1; panelCell = cursorCell; panelAt = Ui.Mouse; }
        // rechts klikken zonder te slepen: Oproepen (slepen blijft schuiven)
        if (Inp.RightDown && !Ui.MouseOverUI) { cursorRight = true; cursorRightAt = Inp.Pos; }
        if (Inp.RightUp && cursorRight) { cursorRight = false; if (Vector2.Distance(Inp.Pos, cursorRightAt) < 6f && cursorCell >= 0) { panelCell = -1; callCell = cursorCell; panelAt = Ui.Mouse; } }
        rightCell = -1;   // nooit een vak leegmaken met rechts in Aanwijzen
        return true;
    }

    // het blok bovenop een vak, als id uit de catalogus (null: leeg)
    string TopId(int i)
    {
        string top = null;
        if (PlanetCity.Cells.TryGetValue(i, out var l)) for (int k = l.Count - 1; k >= 0; k--) if (l[k] != "wegdeel") { top = l[k]; break; }
        if (top == null && PlanetCity.IsHalf(i)) top = "weg";
        if (top == "weg") top = StyleIds[Mathf.Clamp(PlanetCity.StyleOf(i), 0, StyleIds.Length - 1)];
        return top;
    }
    string CellName(int i) { string id = TopId(i); if (id == null) return "Leeg vak"; var it = Find(id); return it.id == id ? it.name : char.ToUpper(id[0]) + id.Substring(1); }
    string CellInfo(int i)
    {
        int n = 0; if (PlanetCity.Cells.TryGetValue(i, out var l)) foreach (var b in l) if (b != "wegdeel") n++;
        float g = PlanetCity.G(i), h = PlanetCity.StackHeight(i);
        string s = n > 0 ? n + (n == 1 ? " laag" : " lagen") + (h > 0.5f ? " · " + h.ToString("0") + " m hoog" : "") + " · " : "";
        return s + g.ToString("0") + " m boven zee";
    }

    void CursorGui(float W, float H)
    {
        if (walking || inSpace) return;
        // de knop linksonder, naast gum en ongedaan maken
        var br = new Rect(G + 100f, H - 12f - 44f, 44f, 44f);
        if (Ui.RoundButton(br, "pijl", cursorMode)) SetCursorMode(!cursorMode);
        Ui.Tip(br, "Aanwijzen", "Klik voor opties, rechts klikken roept iets op", "V");
        if (!cursorMode) return;
        if (panelCell < 0 && callCell < 0 && cursorCell >= 0 && !Ui.MouseOverUI)   // bij de muis: wat je aanwijst
        {
            string t = "<b>" + CellName(cursorCell) + "</b>  <color=#A8B2AB>· klik voor opties · rechts: oproepen</color>";
            var m = Ui.Mouse; float w = Ui.Width(t, Ui.T.Text) + 24f; var r = new Rect(Mathf.Min(m.x + 18f, W - w - 8f), m.y + 14f, w, 28f);
            Ui.Box(r, Ui.PanelSolid, Ui.Line, 8f); Ui.Label(r, t, Ui.T.Text, null, TextAnchor.MiddleCenter);
        }
        if (panelCell >= 0) OptionsPanel(W, H);
        else if (callCell >= 0) CallPanel(W, H);
    }

    // een klein venster bij de klik: titel, een regel uitleg en knoppen in twee kolommen (zoals .kies-menu in de webversie)
    struct Opt { public string label, icon; public System.Action act; public bool danger; }
    void SmallPanel(float W, float H, string title, string sub, List<Opt> opts, System.Action close)
    {
        const float w = 264f; int rows = (opts.Count + 1) / 2; float h = 12f + 22f + 18f + 8f + rows * 44f + 6f;
        var r = new Rect(Mathf.Clamp(panelAt.x + 14f, 8f, W - w - 8f), Mathf.Clamp(panelAt.y - 20f, 8f, H - h - 8f), w, h);
        Ui.Panel(r, true, 12f); Ui.Tex(new Rect(r.x + 10f, r.y, w - 20f, 3f), Texture2D.whiteTexture, Ui.Signal);   // de amber rand bovenaan
        Ui.Label(new Rect(r.x + 12f, r.y + 10f, w - 54f, 22f), title, Ui.T.Bold, null, null, 14);
        if (Ui.RoundButton(new Rect(r.xMax - 36f, r.y + 8f, 26f, 26f), "sluit")) { close(); return; }
        Ui.Label(new Rect(r.x + 12f, r.y + 32f, w - 24f, 18f), sub, Ui.T.Dim, null, null, 12);
        float bw = (w - 24f - 6f) * 0.5f;
        for (int k = 0; k < opts.Count; k++)
        {
            var o = opts[k]; var b = new Rect(r.x + 12f + (k % 2) * (bw + 6f), r.y + 58f + (k / 2) * 44f, bw, 38f);
            if (Ui.Button(b, null, false, null, false, 9f, true)) { o.act(); return; }
            Color ink = o.danger ? new Color(0.94f, 0.48f, 0.42f) : Ui.Ink;
            if (o.icon != null) Ui.Tex(new Rect(b.x + 9f, b.y + 11f, 16f, 16f), Ui.Icon(o.icon), ink);
            Ui.Label(new Rect(b.x + (o.icon != null ? 31f : 10f), b.y, b.width - 36f, b.height), o.label, Ui.T.Bold, ink, null, 12);
        }
    }
    void OptionsPanel(float W, float H)
    {
        int c = panelCell; string id = TopId(c); var o = new List<Opt>();
        if (id != null) o.Add(new Opt { label = "Meer hiervan", icon = "zoek", act = () => { panelCell = -1; SetCursorMode(false); Pipet(c); } });
        if (id != null) o.Add(new Opt { label = "Bovenste weg", icon = "gum", danger = true, act = () => { Push(); if (PlanetCity.RemoveTop(c)) Changed(); if (TopId(c) == null) panelCell = -1; } });
        if (id != null) o.Add(new Opt { label = "Vak leegmaken", icon = "leeg", danger = true, act = () => { Push(); if (PlanetCity.Clear(c)) { Changed(); Say("Vak leeggemaakt"); } if (TopId(c) == null) panelCell = -1; } });
        o.Add(new Opt { label = "Hierheen", icon = "kompas", act = () => { focus = Planet.Cells[c].dir; panelCell = -1; } });
        o.Add(new Opt { label = "Lopen vanaf hier", icon = "lopen", act = () => { focus = Planet.Cells[c].dir; panelCell = -1; SetCursorMode(false); ToggleWalk(); } });
        SmallPanel(W, H, CellName(c), CellInfo(c), o, () => panelCell = -1);
    }
    void CallPanel(float W, float H)
    {
        int c = callCell; var o = new List<Opt>();
        for (int s = 0; s < 3; s++) { int size = s; o.Add(new Opt { label = "Meteoor " + PlanetMeteor.SizeNames[s].ToLower(), icon = "onweer", danger = true, act = () => { callCell = -1; meteorSize = size; MeteorAt(c); } }); }
        o.Add(new Opt { label = "Dorpje hier", icon = "stad", act = () => { callCell = -1; VillageAt(c); } });
        o.Add(new Opt { label = "Onweer", icon = "onweer", act = () => { callCell = -1; SetWeather(Weather.Kind.Onweer); } });
        o.Add(new Opt { label = "Regen", icon = "regen", act = () => { callCell = -1; SetWeather(Weather.Kind.Regen); } });
        o.Add(new Opt { label = "Zon", icon = "zon", act = () => { callCell = -1; SetWeather(Weather.Kind.Zon); } });
        SmallPanel(W, H, "Oproepen", "Op " + CellName(c).ToLower() + " · " + CellInfo(c), o, () => callCell = -1);
    }
    void SetWeather(Weather.Kind k) { if (Weather.I == null) return; Weather.I.Set(k, true); Say("Weer: " + Weather.Names[(int)k]); }
    // een meteoor op dit vak (net als Meteor in PlanetBuilder.Meteor.cs, dat op het midden van het beeld mikt)
    void MeteorAt(int c)
    {
        PlanetMeteor.Launch(c, meteorSize, Cam, (cell, size) =>
        {
            Push();
            PlanetMeteor.Crater(cell, size, () => { PlanetCity.RebuildGrid(); Changed(); Sounds.Instead("meteoor"); Say("De wand is doorgebroken: het water stroomt de krater in"); });
            PlanetCity.RebuildGrid(); Changed(); Sounds.Instead("meteoor");   // geen bouwgeluid: de inslag klinkt al (PlanetMeteor.Sound.cs)
            Say("Inslag! Een krater van " + MeteorCells[size] + " vak" + (size > 0 ? "ken" : "") + " (Ctrl+Z zet alles terug)");
        });
        Say("Meteoor " + PlanetMeteor.SizeNames[meteorSize].ToLower() + " op weg…");
    }
    void VillageAt(int c)
    {
        Push(); string why = PlanetCity.Sample(c);
        if (why != null) { undo.RemoveAt(undo.Count - 1); Say(why); return; }
        Changed(); Say("Dorpje geplaatst (Ctrl+Z haalt het weg)");
    }
}
