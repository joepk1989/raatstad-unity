using UnityEngine;

// Links de spelmodi, op een boog (de modi uit het plan). Elke modus heeft een submenu op een tweede boog ernaast.
// De vrije modus werkt (verder bouwen, startstad, lege planeet, hele planeet vol, Totale chaos) en de oorlogsmodus begint met
// een tankgevecht (PlanetBuilder.Battle.cs); de rest staat erbij als wat er later komt, met een melding.
public partial class PlanetBuilder
{
    struct Mode { public string name, sub, icon; public bool live; }
    static readonly Mode[] Modes = {
        new Mode { name = "Vrije modus", sub = "De vreedzame standaardmodus: bouw wat je wilt", icon = "huis", live = true },
        new Mode { name = "Multiplayer", sub = "Wie bouwt de mooiste stad op één planeet", icon = "mensen" },
        new Mode { name = "Duel tegen Claude", sub = "Jij tegen de AI, na de vrije modus", icon = "kompas" },
        new Mode { name = "Oorlogsmodus", sub = "Begint met een tankgevecht tegen de AI", icon = "onweer", live = true },
    };
    struct Sub { public string name, sub; public System.Action act; public bool on; }   // act null: komt later
    Sub[] SubsOf(int k)
    {
        switch (k)
        {
            case 0: return new[] {
                new Sub { name = "Verder bouwen", sub = CityName + " · " + (beauty != null ? beauty.blocks : PlanetCity.Cells.Count) + " blokken", act = () => { if (inSpace) distGoal = LandDist; Say("Verder bouwen"); } },
                new Sub { name = "Startstad", sub = "Opnieuw beginnen rond een vlag", act = () => StartTown() },
                new Sub { name = "Lege planeet", sub = "Alleen terrein, niets gebouwd", act = () => Empty() },
                new Sub { name = "Hele planeet vol", sub = "Dorpen en steden over de hele bol", act = () => FillPlanet() },
                new Sub { name = "Natuurplaneet", sub = "Alleen natuur: zeeën, bergen, rivieren, ijs en woestijn", act = () => WildPlanet() },
                new Sub { name = "Meteoor", sub = MeteorSub, act = () => Meteor() },
                new Sub { name = "Meteorenregen", sub = "Een halve minuut lang, van alle kanten", act = () => MeteorShower() },
                new Sub { name = "Totale chaos", sub = PlanetLife.Chaos ? "Aan: racers, ontwijkers en jagers" : "Uit: netjes verkeer", on = PlanetLife.Chaos,
                          act = () => { PlanetLife.Chaos = !PlanetLife.Chaos; SaveSettings(); Say(PlanetLife.Chaos ? "Totale chaos: racers, ontwijkers en jagers" : "Netjes verkeer"); } } };
            case 1: return new[] {
                new Sub { name = "Mooiste stad", sub = "Elkaar uitdagen, om beurten scoren" },
                new Sub { name = "Bouwbereik", sub = "Hoe meer je bouwt, hoe groter je bereik" },
                new Sub { name = "Lagen en hofjes", sub = "Hoogte en binnenpleinen om te verkennen" },
                new Sub { name = "Doorloop", sub = "Een stad waar je goed doorheen loopt" } };
            case 2: return new[] {
                new Sub { name = "Jij tegen Claude", sub = "Komt na de vrije modus" } };
            default: return new[] {
                new Sub { name = "Tankgevecht", sub = "Jij in een tank tegen een tank van de AI · T toeschouwer", act = () => { mode = 3; if (inSpace) { distGoal = LandDist; battleAfterLanding = true; } else StartBattle(); } },
                new Sub { name = "Meer", sub = "Meer tanks, teams, een stad verdedigen: later" } };
        }
    }
    bool modesOpen; int mode, openMode = -1;

    void ModesGui(float W, float H)
    {
        if (inSpace) return;   // in de ruimte staat het menu al open (SpaceGui)
        var L = PlanetLife.I; bool inc = L != null && (PlanetLife.Chaos || L.Crashes + L.Knocked > 0);
        float y = 12f + 48f + 6f + (inc ? 24f + 6f : 0f);   // onder de stadsnaam (en de ongelukjesteller)
        string t = Modes[mode].name; float mw = Ui.Width("MODUS", Ui.T.Tiny, 9), tw = Ui.Width(t, Ui.T.Bold, 12);
        var br = new Rect(G, y, 12f + mw + 8f + tw + 12f, 28f);
        if (Ui.Button(br, null, modesOpen, null, false, 14f)) { modesOpen = !modesOpen; openMode = -1; }
        Ui.Label(new Rect(br.x + 12f, br.y, mw + 2f, br.height), "MODUS", Ui.T.Tiny, modesOpen ? Ui.SignalInk : Ui.Signal, null, 9);
        Ui.Label(new Rect(br.x + 12f + mw + 8f, br.y, tw + 4f, br.height), t, Ui.T.Bold, modesOpen ? Ui.SignalInk : Ui.Ink, null, 12);
        if (!modesOpen) Ui.Tip(br, "Spelmodus", "Kies een modus en wat je wilt doen");
    }
    // De keuzes op een boog, als laatste getekend (boven de rest van de interface). Zoals Endless Space 2 maar andersom
    // gekromd: het middelpunt ligt rechts, bij de planeet, dus het midden van de rij staat het verst naar links.
    // Een klik op een modus opent rechts ervan het submenu, op een boog evenwijdig aan de eerste.
    void ModesArc(float W, float H, bool overlay = true)
    {
        if (overlay) Ui.DimBack(0.12f);
        Ui.Tex(new Rect(0f, 0f, openMode >= 0 ? 900f : 600f, H), Shade(), new Color(1f, 1f, 1f, overlay ? 1f : 0.7f));   // links een donker verloop, zodat de tekst leesbaar blijft
        const float R = 430f, step = 100f, IS = 60f; float c0x = G + 80f + R, c0y = H * 0.5f + 16f, da = step / R;
        Vector2 At(float ang, float rad) => new Vector2(c0x - Mathf.Cos(ang) * rad, c0y + Mathf.Sin(ang) * rad);
        float span = (Modes.Length - 1) * 0.5f * da, reach = span + 0.75f;
        // de bogen: één gladde textuur (randen zacht, uiteinden vervagen), opnieuw gemaakt als de interfacegrootte verandert
        float x0 = c0x - (R + RingOut) - 4f, x1 = c0x - R * Mathf.Cos(reach) + 4f, yh = (R + RingOut) * Mathf.Sin(reach) + 4f;
        var ar = new Rect(x0, c0y - yh, x1 - x0, 2f * yh); Ui.Tex(ar, ArcTex(R, reach, span, 0f, ar, c0x, c0y, true));
        var top = At(-span, R); Ui.Label(new Rect(top.x + 44f, top.y - 70f, 300f, 20f), "SPELMODUS", Ui.T.Tiny, Ui.Signal, null, 12);
        float maxLw = 0f; foreach (var m in Modes) maxLw = Mathf.Max(maxLw, Mathf.Max(Ui.Width(m.name.ToUpper(), Ui.T.Bold, 19), Ui.Width(m.sub, Ui.T.Dim, 13)));
        for (int k = 0; k < Modes.Length; k++)
        {
            var m = Modes[k]; var p = At(-span + k * da, R); bool on = k == mode, open = k == openMode;
            var ir = new Rect(p.x - IS * 0.5f, p.y - IS * 0.5f, IS, IS); float lw = Mathf.Max(Ui.Width(m.name.ToUpper(), Ui.T.Bold, 19), Ui.Width(m.sub, Ui.T.Dim, 13));
            var hit = new Rect(ir.x, ir.y, IS + 18f + lw + 8f, IS); bool hv = hit.Contains(Ui.Mouse);
            bool dim = openMode >= 0 && !open && !hv;   // met een submenu open vallen de andere modi terug
            Color ring = open || on ? Ui.Signal : hv ? new Color(Ui.Signal.r, Ui.Signal.g, Ui.Signal.b, 0.8f) : new Color(1f, 1f, 1f, m.live ? 0.55f : 0.25f);
            if (dim) ring.a *= 0.5f;
            Ui.Box(ir, new Color(0.06f, 0.08f, 0.1f, 0.9f), ring, IS * 0.5f);
            Ui.Box(new Rect(ir.x - 5f, ir.y - 5f, IS + 10f, IS + 10f), Color.clear, new Color(ring.r, ring.g, ring.b, ring.a * (open ? 0.7f : 0.35f)), IS * 0.5f + 5f);   // een dunne ring eromheen
            Color ink = open || hv ? Ui.Signal : m.live ? Ui.Ink : Ui.InkDim; if (dim) ink.a *= 0.55f;
            Ui.Tex(new Rect(p.x - 14f, p.y - 14f, 28f, 28f), Ui.Icon(m.icon), ink);
            Ui.Label(new Rect(ir.xMax + 18f, p.y - 24f, lw + 4f, 26f), m.name.ToUpper(), Ui.T.Bold, ink, null, 19);
            Ui.Label(new Rect(ir.xMax + 18f, p.y + 3f, lw + 4f, 20f), m.sub, Ui.T.Dim, dim ? new Color(Ui.InkDim.r, Ui.InkDim.g, Ui.InkDim.b, 0.5f) : (Color?)null, null, 13);
            if (on) Ui.Tex(new Rect(ir.x - 22f, p.y - 4f, 8f, 8f), Ui.Hex(Ui.Signal, Ui.Signal, 0f));   // de modus waarin je speelt
            if (GUI.Button(hit, GUIContent.none, GUIStyle.none)) openMode = open ? -1 : k;
        }
        if (openMode >= 0) SubArc(SubsOf(openMode), -span + openMode * da, R, c0x + IS * 0.5f + 18f + maxLw + 70f, c0y, overlay);
        if (overlay && Ui.DimClick()) { if (openMode >= 0) openMode = -1; else modesOpen = false; }   // ernaast klikken: eerst het submenu dicht, dan het menu
    }
    // het submenu: kleinere knoppen op een boog evenwijdig aan de eerste (zelfde kromming, naar rechts geschoven), rond de hoogte van de modus
    void SubArc(Sub[] subs, float mid, float R, float cx, float cy, bool overlay)
    {
        const float step = 62f, IS = 34f; float da = step / R, span = (subs.Length - 1) * 0.5f * da;
        mid = Mathf.Clamp(mid, -0.62f + span, 0.62f - span);   // binnen het scherm houden
        Vector2 At(float ang) => new Vector2(cx - Mathf.Cos(ang) * R, cy + Mathf.Sin(ang) * R);
        float reach = span + 0.35f, x0 = cx - R - 4f, x1 = cx - R * Mathf.Cos(Mathf.Abs(mid) + reach) + 4f, y0 = cy + R * Mathf.Sin(mid - reach) - 4f, y1 = cy + R * Mathf.Sin(mid + reach) + 4f;
        var ar = new Rect(x0, y0, x1 - x0, y1 - y0); Ui.Tex(ar, ArcTex(R, reach, span, mid, ar, cx, cy, false));
        for (int j = 0; j < subs.Length; j++)
        {
            var sb = subs[j]; var p = At(mid - span + j * da); bool live = sb.act != null;
            var ir = new Rect(p.x - IS * 0.5f, p.y - IS * 0.5f, IS, IS); float lw = Mathf.Max(Ui.Width(sb.name, Ui.T.Bold, 15), Ui.Width(sb.sub, Ui.T.Dim, 12));
            var hit = new Rect(ir.x, ir.y - 4f, IS + 14f + lw + 8f, IS + 8f); bool hv = hit.Contains(Ui.Mouse);
            Color ring = hv || sb.on ? Ui.Signal : new Color(1f, 1f, 1f, live ? 0.6f : 0.25f);
            Ui.Box(ir, new Color(0.06f, 0.08f, 0.1f, 0.9f), ring, IS * 0.5f);
            Ui.Tex(new Rect(p.x - 6f, p.y - 5.4f, 12f, 10.8f), Ui.Hex(hv || sb.on ? Ui.Signal : live ? Ui.Ink : Ui.InkDim, Color.clear, 0f));
            Color ink = hv ? Ui.Signal : live ? Ui.Ink : Ui.InkDim;
            Ui.Label(new Rect(ir.xMax + 14f, p.y - 18f, lw + 4f, 20f), sb.name, Ui.T.Bold, ink, null, 15);
            Ui.Label(new Rect(ir.xMax + 14f, p.y + 1f, lw + 4f, 17f), sb.sub, Ui.T.Dim, null, null, 12);
            if (!live) Ui.Label(new Rect(ir.xMax + 14f + Ui.Width(sb.name, Ui.T.Bold, 15) + 10f, p.y - 17f, 100f, 18f), "BINNENKORT", Ui.T.Tiny, new Color(Ui.Signal.r, Ui.Signal.g, Ui.Signal.b, 0.8f), null, 8);
            if (GUI.Button(hit, GUIContent.none, GUIStyle.none))
            {
                if (!live) { Say(sb.name + " komt later: dit is nog een concept"); continue; }
                sb.act(); if (openMode == 0) mode = 0; openMode = -1; if (overlay) modesOpen = false;
            }
        }
    }

    // het verloop: links donker, naar rechts doorzichtig (kwadratisch), één textuur zonder streepjes
    static Texture2D shade;
    static Texture2D Shade()
    {
        if (shade != null) return shade;
        shade = new Texture2D(256, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int x = 0; x < 256; x++) { float f = x / 255f; shade.SetPixel(x, 0, new Color(0.02f, 0.03f, 0.05f, 0.8f * (1f - f) * (1f - f))); }
        shade.Apply(); return shade;
    }
    // drie bogen om hetzelfde middelpunt: door de knoppen, en twee vagere erbuiten (zoals de banen om de planeet in Endless Space 2)
    const float RingOut = 100f;
    static readonly System.Collections.Generic.Dictionary<string, Texture2D> arcs = new System.Collections.Generic.Dictionary<string, Texture2D>();
    static Texture2D ArcTex(float R, float reach, float span, float mid, Rect r, float cx, float cy, bool three)
    {
        string key = R + "|" + reach + "|" + span + "|" + mid + "|" + Ui.S + "|" + r.width + "|" + r.height + "|" + (cx - r.x) + "|" + (cy - r.y) + "|" + three;
        if (arcs.TryGetValue(key, out var t) && t != null) return t;
        if (arcs.Count > 24) { foreach (var o in arcs.Values) if (o != null) Object.Destroy(o); arcs.Clear(); }   // niet eindeloos bewaren
        int w = Mathf.Max(1, Mathf.CeilToInt(r.width * Ui.S)), h = Mathf.Max(1, Mathf.CeilToInt(r.height * Ui.S)); float s = Ui.S;
        t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        float[] rad = three ? new[] { R, R + 40f, R + RingOut } : new[] { R }, alp = three ? new[] { 0.55f, 0.24f, 0.12f } : new[] { 0.45f }, wid = three ? new[] { 1.3f, 1f, 0.9f } : new[] { 1.1f };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float vx = r.x + (x + 0.5f) / s - cx, vy = r.y + (h - 1 - y + 0.5f) / s - cy;   // virtuele punten (y omlaag in de interface)
                float d = Mathf.Sqrt(vx * vx + vy * vy), ang = Mathf.Atan2(vy, -vx) - mid;
                float fade = 1f - Mathf.Clamp01((Mathf.Abs(ang) - span) / (reach - span)); fade *= fade * (3f - 2f * fade);   // zacht naar de uiteinden
                float a = 0f;
                for (int k = 0; k < rad.Length; k++) { float q = Mathf.Abs(d - rad[k]) * s / (wid[k] * s * 0.5f + 0.5f); a = Mathf.Max(a, alp[k] * Mathf.Clamp01(1.5f - q)); }   // een lijn van een paar pixels, met zachte randen
                px[y * w + x] = new Color32(255, 255, 255, (byte)(255f * a * fade));
            }
        t.SetPixels32(px); t.Apply(); arcs[key] = t; return t;
    }
}
