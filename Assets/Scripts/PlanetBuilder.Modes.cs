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
        if (!modesOpen) return;
        float w = 290f, ch = 54f, h = 34f + Modes.Length * (ch + 6f) + 4f; var r = new Rect(G, br.yMax + 6f, w, h); Ui.Panel(r, true, 14f);
        Ui.Label(new Rect(r.x + 14f, r.y + 8f, w - 28f, 18f), "SPELMODUS", Ui.T.Tiny, null, null, 10);
        Ui.Label(new Rect(r.x + 14f, r.y + 8f, w - 28f, 18f), "concept", Ui.T.Dim, null, TextAnchor.MiddleRight, 10);
        for (int k = 0; k < Modes.Length; k++)
        {
            var m = Modes[k]; var c = new Rect(r.x + 8f, r.y + 32f + k * (ch + 6f), w - 16f, ch); bool on = k == mode;
            bool click = Ui.Button(c, null, on, null, false, 10f, true);
            Ui.Tex(new Rect(c.x + 10f, c.y + 13f, 30f, 28f), Ui.Hex(on ? Ui.Signal : Ui.SlotFill, on ? Ui.Signal : Ui.Line, 1.5f));
            Ui.Tex(new Rect(c.x + 17f, c.y + 19f, 16f, 16f), Ui.Icon(m.icon), on ? Ui.SignalInk : m.live ? Ui.Ink : Ui.InkDim);
            Color ink = on ? Ui.SignalInk : m.live ? Ui.Ink : Ui.InkDim;
            Ui.Label(new Rect(c.x + 50f, c.y + 8f, c.width - 60f, 20f), m.name, Ui.T.Bold, ink);
            Ui.Label(new Rect(c.x + 50f, c.y + 28f, c.width - 60f, 18f), m.sub, Ui.T.Dim, on ? Ui.SignalInk : null, null, 11);
            if (!m.live) Ui.Label(new Rect(c.x, c.y + 8f, c.width - 10f, 16f), "BINNENKORT", Ui.T.Tiny, new Color(Ui.Signal.r, Ui.Signal.g, Ui.Signal.b, 0.8f), TextAnchor.MiddleRight, 8);
            if (click) { if (m.live) { mode = k; modesOpen = false; Say(m.name); } else Say(m.name + " komt later: dit is nog een concept"); }
        }
    }
}
