using UnityEngine;

// Concept: links een keuze voor de spelmodus. Alleen de vrije modus bestaat; de rest laat zien waar het heen kan
// (nog zonder werking, een klik geeft een melding).
public partial class PlanetBuilder
{
    struct Mode { public string name, sub, icon; public bool live; }
    static readonly Mode[] Modes = {
        new Mode { name = "Vrije modus", sub = "Bouw wat je wilt, zonder grenzen", icon = "huis", live = true },
        new Mode { name = "Opdrachten", sub = "Doelen halen: een groene wijk, een plein met winkels", icon = "boek" },
        new Mode { name = "Budget", sub = "Bouwen kost geld; bewoners betalen belasting", icon = "stad" },
        new Mode { name = "Ontdekken", sub = "Reis over de bol en vind alle dorpen", icon = "zoek" },
        new Mode { name = "Uitdaging", sub = "Begin op een berg, een eiland of de pool", icon = "leeg" },
    };
    bool modesOpen; int mode;

    void ModesGui(float W, float H)
    {
        var L = PlanetLife.I; bool inc = L != null && (PlanetLife.Chaos || L.Crashes + L.Knocked > 0);
        float y = 12f + 48f + 6f + (inc ? 24f + 6f : 0f);   // onder de stadsnaam (en de ongelukjesteller)
        string t = Modes[mode].name; float mw = Ui.Width("MODUS", Ui.T.Tiny, 9), tw = Ui.Width(t, Ui.T.Bold, 12);
        var br = new Rect(G, y, 12f + mw + 8f + tw + 12f, 28f);
        if (Ui.Button(br, null, modesOpen, null, false, 14f)) modesOpen = !modesOpen;
        Ui.Label(new Rect(br.x + 12f, br.y, mw + 2f, br.height), "MODUS", Ui.T.Tiny, modesOpen ? Ui.SignalInk : Ui.Signal, null, 9);
        Ui.Label(new Rect(br.x + 12f + mw + 8f, br.y, tw + 4f, br.height), t, Ui.T.Bold, modesOpen ? Ui.SignalInk : Ui.Ink, null, 12);
        if (!modesOpen) Ui.Tip(br, "Spelmodus", "Concept: alleen de vrije modus werkt nu");
    }
    // De keuzes op een boog, als laatste getekend (boven de rest van de interface). Zoals Endless Space 2 maar andersom
    // gekromd: het middelpunt ligt rechts, bij de planeet, dus het midden van de rij staat het verst naar links.
    void ModesArc(float W, float H)
    {
        Ui.DimBack(0.12f);
        for (int k = 0; k < 40; k++)   // links een donker verloop, zodat de tekst op de kaart leesbaar blijft
        {
            float f = k / 40f; Ui.Tex(new Rect(f * 560f, 0f, 14.5f, H), Texture2D.whiteTexture, new Color(0.02f, 0.03f, 0.05f, 0.78f * (1f - f) * (1f - f)));
        }
        const float R = 430f, step = 100f, IS = 60f; float c0x = G + 80f + R, c0y = H * 0.5f + 16f, da = step / R;
        Vector2 At(float ang, float rad) => new Vector2(c0x - Mathf.Cos(ang) * rad, c0y + Mathf.Sin(ang) * rad);
        float span = (Modes.Length - 1) * 0.5f * da;
        for (float ang = -span - 0.6f; ang <= span + 0.6f; ang += 0.006f)   // twee dunne bogen: door de knoppen en een buitenste
        {
            float fade = 1f - Mathf.Clamp01((Mathf.Abs(ang) - span) / 0.6f);
            var p = At(ang, R); Ui.Box(new Rect(p.x - 1.3f, p.y - 1.3f, 2.6f, 2.6f), new Color(1f, 1f, 1f, 0.5f * fade), Color.clear, 1.3f);
            var q = At(ang, R + 46f); Ui.Box(new Rect(q.x - 1f, q.y - 1f, 2f, 2f), new Color(1f, 1f, 1f, 0.26f * fade), Color.clear, 1f);
            var q2 = At(ang, R + 110f); Ui.Box(new Rect(q2.x - 0.8f, q2.y - 0.8f, 1.6f, 1.6f), new Color(1f, 1f, 1f, 0.14f * fade), Color.clear, 0.8f);
        }
        var top = At(-span, R); Ui.Label(new Rect(top.x + 44f, top.y - 70f, 300f, 20f), "SPELMODUS  <color=#A8B2AB>· concept</color>", Ui.T.Tiny, Ui.Signal, null, 12);
        for (int k = 0; k < Modes.Length; k++)
        {
            var m = Modes[k]; var p = At(-span + k * da, R); bool on = k == mode;
            var ir = new Rect(p.x - IS * 0.5f, p.y - IS * 0.5f, IS, IS); float lw = Mathf.Max(Ui.Width(m.name.ToUpper(), Ui.T.Bold, 19), Ui.Width(m.sub, Ui.T.Dim, 13));
            var hit = new Rect(ir.x, ir.y, IS + 18f + lw + 8f, IS); bool hv = hit.Contains(Ui.Mouse);
            Color ring = on ? Ui.Signal : hv ? new Color(Ui.Signal.r, Ui.Signal.g, Ui.Signal.b, 0.8f) : new Color(1f, 1f, 1f, m.live ? 0.55f : 0.25f);
            Ui.Box(ir, new Color(0.06f, 0.08f, 0.1f, 0.9f), ring, IS * 0.5f);
            Ui.Box(new Rect(ir.x - 5f, ir.y - 5f, IS + 10f, IS + 10f), Color.clear, new Color(ring.r, ring.g, ring.b, ring.a * 0.35f), IS * 0.5f + 5f);   // een dunne ring eromheen
            Color ink = on || hv ? Ui.Signal : m.live ? Ui.Ink : Ui.InkDim;
            Ui.Tex(new Rect(p.x - 14f, p.y - 14f, 28f, 28f), Ui.Icon(m.icon), ink);
            Ui.Label(new Rect(ir.xMax + 18f, p.y - 24f, lw + 4f, 26f), m.name.ToUpper(), Ui.T.Bold, ink, null, 19);
            Ui.Label(new Rect(ir.xMax + 18f, p.y + 3f, lw + 4f, 20f), m.sub, Ui.T.Dim, m.live || hv ? null : new Color(Ui.InkDim.r, Ui.InkDim.g, Ui.InkDim.b, 0.75f), null, 13);
            if (!m.live) Ui.Label(new Rect(ir.xMax + 18f + Ui.Width(m.name.ToUpper(), Ui.T.Bold, 19) + 12f, p.y - 23f, 100f, 24f), "BINNENKORT", Ui.T.Tiny, new Color(Ui.Signal.r, Ui.Signal.g, Ui.Signal.b, 0.8f), null, 9);
            if (GUI.Button(hit, GUIContent.none, GUIStyle.none)) { if (m.live) { mode = k; modesOpen = false; Say(m.name); } else Say(m.name + " komt later: dit is nog een concept"); }
        }
        if (Ui.DimClick()) modesOpen = false;   // ernaast klikken sluit
    }
}
