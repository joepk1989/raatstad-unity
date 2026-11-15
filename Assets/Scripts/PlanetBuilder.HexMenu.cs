using System.Collections.Generic;
using UnityEngine;

// Het raatmenu: het menu in het spel (knop Menu, M) en in de ruimte (altijd in beeld). Zeshoekige tegels op een onzichtbare
// boog (het midden het verst naar links, de uiteinden buigen naar rechts), zonder lijnen of ringen. Bij openen schuiven de
// tegels een voor een binnen; aanwijzen kleurt een tegel amber en schuift hem iets naar voren. Een klik op een groep laat
// rechts ernaast de keuzes uitrollen, als kleine zeshoekjes op een tweede onzichtbare boog; aan/uit en keuzes tonen hun waarde.
// Nieuwe planeten maken en spelmodi kiezen gebeurt op het startscherm; alleen het tankgevecht (oorlogsmodus) staat ook hier.
public partial class PlanetBuilder
{
    class MItem { public string name, sub, icon, value; public System.Action act; public System.Func<List<MItem>> kids; public bool on, danger, head, note; public string[] opts; public int sel = -1; public System.Action<int> pick; }   // head: een kopje, note: alleen uitleg (niet aan te klikken); opts: keuzes naast elkaar, direct te kiezen (pick)
    int hexOpen = -1; float hexT = -99f, hexSubT = -99f; bool hexWasOpen; bool spaceMenuOff;   // in de ruimte het menu weggeklikt (hamburger of M)
    // rechts (in het spel, onder de knop Menu) is het menu gespiegeld: alles wordt eerst links uitgerekend en dan omgeklapt
    bool hexRight; Rect Mx(Rect r) => hexRight ? new Rect(Ui.W - r.xMax, r.y, r.width, r.height) : r;

    List<MItem> MainItems(bool space)
    {
        int blocks = beauty != null ? beauty.blocks : PlanetCity.Cells.Count;
        var l = new List<MItem>();
        if (space) l.Add(new MItem { name = "Landen", sub = "Terug naar de grond om te bouwen", icon = "huis", act = () => { distGoal = LandDist; Say("Landen"); } });
        else l.Add(new MItem { name = "Verder spelen", sub = CityName + " · " + blocks + " blokken", icon = "huis", act = () => menuOpen = false });
        l.Add(new MItem { name = "Nieuwe wereld", sub = "Startstad, leeg, natuur of de hele planeet vol", icon = "kompas", act = () => { menuOpen = false; homeToNew = true; dialog = 2; } });   // eerst opslaan? dan het startscherm met Nieuwe stad open
        l.Add(new MItem { name = "Opslaan", sub = "Elke 3 minuten vanzelf, en bij afsluiten", icon = "boek", kids = () => new List<MItem> {
            new MItem { name = "Nu opslaan", sub = CityName, act = () => { menuOpen = false; Say(SaveCity(false) ? CityName + " opgeslagen" : "Opslaan lukte niet"); } },
            new MItem { name = "Opslaan als", sub = "Als nieuwe stad bewaren", act = () => { menuOpen = false; dialog = 1; nameEdit = CityFiles.FreeName(CityFiles.Current != null ? CityFiles.Current.name : "Nieuwe stad"); } },
            new MItem { name = "Naar het startscherm", sub = "Andere stad openen of een nieuwe", act = () => { menuOpen = false; dialog = 2; } } } });
        l.Add(new MItem { name = "Gereedschap", sub = "Voorbeeldstad, meteoor, AI-bouwer", icon = "stad", kids = () => {
            var g = new List<MItem> {
                new MItem { name = "Melding maken", sub = "Iets mis? Beeld, plek en stad bewaren voor Claude (F8)", act = () => StartReport() },
                new MItem { name = "Voorbeeldstad hier", sub = "Ringweg, huizen en een meer (midden van het beeld)", act = () => { menuOpen = false; Sample(); } },
                new MItem { name = "Meteoor", sub = "Inslag op het midden van het beeld", danger = true, act = () => { menuOpen = false; Meteor(); } },
                new MItem { name = "Maat van de meteoor", value = PlanetMeteor.SizeNames[meteorSize] + " · " + MeteorCells[meteorSize], act = () => meteorSize = (meteorSize + 1) % 3 },
                new MItem { name = "Meteorenregen", sub = "Een halve minuut lang, van alle kanten (Ctrl+Z haalt alles weg)", danger = true, act = () => { menuOpen = false; MeteorShower(); } },
                new MItem { name = aiOn ? (aiPaused ? "AI-bouwer verder" : "AI-bouwer pauzeren") : "AI-bouwer", sub = AiCardSub, on = aiOn && !aiPaused, act = () => { menuOpen = false; AiButton(); } } };
            if (aiOn) g.Add(new MItem { name = "AI-bouwer stoppen", sub = PlanetAI.Moves + " zetten gedaan", danger = true, act = () => { menuOpen = false; AiStop(); } });
            else g.Add(new MItem { name = "Tempo AI-bouwer", value = AiTempoNames[aiTempo], act = () => aiTempo = (aiTempo + 1) % AiTempoNames.Length });
            return g; } });
        l.Add(new MItem { name = "Oorlogsmodus", sub = "Tankgevecht tegen de AI", icon = "onweer", kids = () => new List<MItem> {
            new MItem { name = "Tankgevecht", sub = "Jij in een tank tegen een tank van de AI · T toeschouwer", danger = true, act = () => { menuOpen = false; if (space) { distGoal = LandDist; battleAfterLanding = true; } else StartBattle(); } } } });   // PlanetBuilder.Battle.cs
        l.Add(new MItem { name = "Instellingen", sub = "Beeld, geluid, leven, planeet en meer", icon = "zon", kids = SettingsMenuItems });   // in secties (PlanetBuilder.Settings.cs)
        l.Add(new MItem { name = "Bediening", sub = "Alle toetsen op een rij (?)", icon = "menu", act = () => { menuOpen = false; helpOpen = true; } });
        return l;
    }

    // overlay: in het spel (dimt het beeld een beetje, ernaast klikken sluit); anders in de ruimte, altijd open
    void HexMenu(float W, float H, bool overlay)
    {
        if (!hexWasOpen) { hexT = Time.unscaledTime; hexOpen = -1; } hexWasOpen = true;
        if (overlay) Ui.DimBack(0.42f);
        var items = MainItems(!overlay);
        hexRight = false;   // links, onder de hamburger linksboven (gespiegeld rechts kan nog, maar links bleek fijner)
        int hit = Tiles(items, G + 70f, H * 0.5f + 10f, hexT, hexOpen, overlay ? "MENU" : "RAATSTAD", 60f, 18, out float subX, out float mid, out float R, out float cy);
        if (hit >= 0) { hexSec = -1; var m = items[hit]; if (m.kids != null) { hexOpen = hexOpen == hit ? -1 : hit; hexSubT = Time.unscaledTime; } else { hexOpen = -1; m.act?.Invoke(); } }
        if (hexOpen >= 0 && hexOpen < items.Count && items[hexOpen].kids != null)
            SubHex(items[hexOpen].kids(), hexOpenMid, R, subX, cy, Time.unscaledTime);
        if (overlay && Ui.DimClick()) { if (hexOpen >= 0) hexOpen = -1; else menuOpen = false; }   // ernaast klikken: eerst de keuzes dicht, dan het menu
    }
    void HexMenuClosed() { hexWasOpen = false; }
    float hexOpenMid;   // de hoek van de open groep (voor de keuzes ernaast)

    // Een rij zeshoekige tegels op een onzichtbare boog (ook voor het startscherm). left: waar het midden van de boog staat,
    // t0: wanneer de rij opende (binnenschuiven), open: de groep die oplicht. Geeft de aangeklikte tegel terug (of -1), en waar
    // de keuzes van een groep beginnen (subX, op dezelfde boog).
    int Tiles(List<MItem> items, float left, float cy0, float t0, int open, string title, float TS, int size, out float subX, out float midOut, out float ROut, out float cyOut)
    {
        float R = 440f, step = TS + 28f; float cx = left + R, cy = cy0, da = step / R, span = (items.Count - 1) * 0.5f * da;
        Vector2 At(float ang) => new Vector2(cx - Mathf.Cos(ang) * R, cy + Mathf.Sin(ang) * R);
        float now = Time.unscaledTime, maxW = 0f; int clicked = -1;
        foreach (var m in items) maxW = Mathf.Max(maxW, Mathf.Max(Ui.Width(m.name.ToUpper(), Ui.T.Bold, size), Ui.Width(m.sub ?? "", Ui.T.Dim, 12)));
        if (title != null) { var top = At(-span); Shadowed(Mx(new Rect(top.x - TS * 0.5f, top.y - TS * 0.5f - 34f, 300f, 20f)), title, Ui.T.Tiny, Ui.Signal, 12, hexRight); }
        for (int k = 0; k < items.Count; k++)
        {
            var m = items[k]; float e = Ease((now - t0 - k * 0.045f) / 0.28f); if (e <= 0f) continue;
            var p = At(-span + k * da); bool on = k == open, dis = m.act == null && m.kids == null;
            var hit = Mx(new Rect(p.x - TS * 0.5f, p.y - TS * 0.5f, TS + 16f + maxW, TS)); bool hv = hit.Contains(Ui.Mouse) && e >= 1f && !dis;
            float push = (hv || on ? 10f : 0f) + (1f - e) * -40f; p.x += push;   // binnenschuiven, en naar voren bij aanwijzen
            var old = GUI.color; GUI.color = new Color(1f, 1f, 1f, e * (dis ? 0.5f : 1f));
            bool lit = hv || on || m.on;
            Color fill = lit ? Ui.Signal : new Color(0.07f, 0.09f, 0.12f, 0.92f), edge = lit ? Ui.Signal : new Color(1f, 1f, 1f, 0.35f);
            var tr = new Rect(p.x - TS * 0.5f, p.y - TS * 0.45f, TS, TS * 0.9f); float ic = TS * 0.43f;
            Ui.Tex(Mx(tr), Ui.Hex(fill, edge, 1.5f));
            Ui.Tex(Mx(new Rect(p.x - ic * 0.5f, p.y - ic * 0.5f, ic, ic)), Ui.Icon(m.icon), lit ? Ui.SignalInk : Ui.Ink);
            string arrow = m.kids != null ? (hexRight ? "<color=#F2B233>‹</color>  " : "") : "", arrowR = m.kids != null && !hexRight ? "  <color=#F2B233>›</color>" : "";   // het pijltje wijst naar de kant waar de keuzes uitrollen
            Shadowed(Mx(new Rect(tr.xMax + 16f, p.y - size - 5f, maxW + 40f, size + 8f)), arrow + m.name.ToUpper() + arrowR, Ui.T.Bold, hv || on ? Ui.Signal : Ui.Ink, size, hexRight);
            if (m.sub != null) Shadowed(Mx(new Rect(tr.xMax + 16f, p.y + 3f, maxW + 40f, 18f)), m.sub, Ui.T.Dim, new Color(Ui.Ink.r, Ui.Ink.g, Ui.Ink.b, 0.78f), 12, hexRight);
            GUI.color = old;
            if (dis) continue;
            Ui.Hit(hit);
            if (GUI.Button(hit, GUIContent.none, GUIStyle.none)) clicked = k;
        }
        subX = cx - R + TS * 0.5f + 16f + maxW + 90f + R; midOut = open >= 0 ? -span + open * da : 0f; ROut = R; cyOut = cy;
        if (clicked >= 0 && items[clicked].kids != null) hexOpenMid = -span + clicked * da; else if (open >= 0) hexOpenMid = -span + open * da;
        return clicked;
    }

    // de keuzes van een groep: kleine zeshoekjes op een boog evenwijdig aan de eerste, rond de hoogte van die groep
    void SubHex(List<MItem> kids, float mid, float R, float cx, float cy, float now)
    {
        // gelijke afstand in de hoogte (ook bij een lange lijst geen keuzes over elkaar), dichter op elkaar als het niet past
        const float TS = 34f; float limY = Ui.H * 0.5f - 95f;   // onderaan vrij van de bouwbalk
        float step = Mathf.Min(54f, 2f * limY / Mathf.Max(1, kids.Count - 1)), half = (kids.Count - 1) * 0.5f * step;
        float y0 = Mathf.Clamp(Mathf.Sin(mid) * R, -limY + half, Mathf.Max(-limY + half, limY - half));   // binnen het scherm houden
        float AngOf(int j) => Mathf.Asin(Mathf.Clamp((y0 - half + j * step) / R, -0.98f, 0.98f));
        Vector2 At(float ang) => new Vector2(cx - Mathf.Cos(ang) * R, cy + Mathf.Sin(ang) * R);
        float maxW = 0f; foreach (var m in kids) maxW = Mathf.Max(maxW, Mathf.Max(Ui.Width(m.name, Ui.T.Bold, 15) + (m.value != null ? Ui.Width(m.value, Ui.T.Bold, 13) + 30f : 0f), Ui.Width(m.sub ?? "", Ui.T.Dim, 12)));
        foreach (var m in kids) if (m.opts != null) { float ow = 0f; foreach (var o in m.opts) ow += Ui.Width(o, Ui.T.Bold, 12) + 16f; maxW = Mathf.Max(maxW, ow); }
        for (int j = 0; j < kids.Count; j++)
        {
            var m = kids[j]; float e = Ease((now - hexSubT - j * 0.04f) / 0.22f); if (e <= 0f) continue;
            var p = At(AngOf(j)); p.x += (1f - e) * -24f;
            if (m.head || m.note)   // een kopje of uitleg: alleen tekst, op de plek van een keuze
            {
                var o = GUI.color; GUI.color = new Color(1f, 1f, 1f, e); float nx = p.x + TS * 0.5f + 14f;
                if (m.head) Shadowed(Mx(new Rect(nx, p.y - 9f, maxW + 20f, 18f)), m.name.ToUpper(), Ui.T.Tiny, Ui.Signal, 11, hexRight);
                else Shadowed(Mx(new Rect(nx, p.y - 16f, maxW + 20f, 32f)), m.name, Ui.T.Dim, new Color(Ui.Ink.r, Ui.Ink.g, Ui.Ink.b, 0.85f), 12, hexRight);
                GUI.color = o; continue;
            }
            var hit = Mx(new Rect(p.x - TS * 0.5f, p.y - TS * 0.5f, TS + 14f + maxW + 10f, TS)); bool hv = hit.Contains(Ui.Mouse) && e >= 1f;
            var old = GUI.color; GUI.color = new Color(1f, 1f, 1f, e);
            Color ac = m.danger ? new Color(0.94f, 0.48f, 0.42f) : Ui.Signal;
            Ui.Tex(Mx(new Rect(p.x - TS * 0.5f, p.y - TS * 0.45f, TS, TS * 0.9f)), Ui.Hex(hv ? ac : m.on ? new Color(ac.r, ac.g, ac.b, 0.35f) : new Color(0.07f, 0.09f, 0.12f, 0.92f), hv || m.on ? ac : new Color(1f, 1f, 1f, 0.3f), 1.2f));
            float tx = p.x + TS * 0.5f + 14f;
            if (m.opts != null)   // de naam, eronder de keuzes naast elkaar: een klik kiest meteen
            {
                Shadowed(Mx(new Rect(tx, p.y - 17f, maxW + 20f, 20f)), m.name, Ui.T.Bold, hv ? ac : Ui.Ink, 15, hexRight);
                float ox = tx - 6f;
                for (int q = 0; q < m.opts.Length; q++)
                {
                    float ow = Ui.Width(m.opts[q], Ui.T.Bold, 12) + 12f; var cr = Mx(new Rect(ox, p.y + 2f, ow, 19f)); bool sel = q == m.sel, ch = cr.Contains(Ui.Mouse) && e >= 1f;
                    if (sel || ch) Ui.Box(cr, sel ? Ui.Signal : new Color(1f, 1f, 1f, 0.14f), Color.clear, 6f);
                    Ui.Label(cr, m.opts[q], Ui.T.Bold, sel ? Ui.SignalInk : ch ? Ui.Signal : Ui.Ink, TextAnchor.MiddleCenter, 12);
                    Ui.Hit(cr); if (GUI.Button(cr, GUIContent.none, GUIStyle.none) && !sel) { Sounds.Fx("click"); m.pick?.Invoke(q); }
                    ox += ow + 4f;
                }
                GUI.color = old; Ui.Hit(hit); continue;
            }
            bool two = m.sub != null;
            string val = m.value == null ? "" : "   <color=" + (m.on || m.value == "aan" ? "#F2B233" : "#A8B2AB") + ">" + m.value + "</color>";   // de waarde in dezelfde regel, ook gespiegeld goed leesbaar
            Shadowed(Mx(new Rect(tx, two ? p.y - 17f : p.y - 10f, maxW + 20f, 20f)), m.name + val, Ui.T.Bold, hv ? ac : Ui.Ink, 15, hexRight);
            if (two) Shadowed(Mx(new Rect(tx, p.y + 2f, maxW + 20f, 17f)), m.sub, Ui.T.Dim, new Color(Ui.Ink.r, Ui.Ink.g, Ui.Ink.b, 0.78f), 12, hexRight);
            GUI.color = old;
            Ui.Hit(hit);
            if (GUI.Button(hit, GUIContent.none, GUIStyle.none)) m.act?.Invoke();
        }
    }
    static float Ease(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t) * (1f - t); }
    // tekst met een zachte schaduw: leesbaar op de stad en in de ruimte, zonder vlak erachter
    static void Shadowed(Rect r, string t, Ui.T style, Color col, int size, bool right = false)
    {
        TextAnchor? al = right ? TextAnchor.MiddleRight : (TextAnchor?)null;
        string plain = System.Text.RegularExpressions.Regex.Replace(t, "<.*?>", "");
        var a = GUI.color.a; var sh = new Color(0f, 0f, 0f, 0.55f * a);   // schaduw in een kringetje eromheen (drie kanten), dan de tekst
        Ui.Label(new Rect(r.x + 1f, r.y + 1.5f, r.width, r.height), plain, style, sh, al, size); Ui.Label(new Rect(r.x - 1f, r.y + 1f, r.width, r.height), plain, style, sh, al, size); Ui.Label(new Rect(r.x, r.y + 2.5f, r.width, r.height), plain, style, sh, al, size);
        Ui.Label(r, t, style, col, al, size);
    }
}
