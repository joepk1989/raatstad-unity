using System.Collections.Generic;
using UnityEngine;

// In de ruimte (ver uitgezoomd) een eigen interface: geen bouwbalk, wel de planeet als kaart. Markers op de dorpen en steden,
// je eigen stad en de polen; een klik vliegt erheen en landt. Onderaan de navigatie (H, N, K, landen). Bouwen kan pas weer
// dichterbij: van zo ver zijn de vakken te klein om te raken.
public partial class PlanetBuilder
{
    bool inSpace; float spaceT = -99f;
    Vector3 flyGoal; bool flyLand;   // vliegen naar een plek op de bol (zero: niet)
    const float SpaceIn = 950f, SpaceOut = 800f, LandDist = 230f;

    // in Update, na de vensters: true = in de ruimte, de rest van de bouwbediening slaat over
    bool SpaceUpdate()
    {
        bool was = inSpace; inSpace = dist > (inSpace ? SpaceOut : SpaceIn);
        if (inSpace != was) { spaceT = Time.unscaledTime; if (inSpace) { catOpen = wheelOpen = false; eraser = false; Say("In de ruimte: klik een dorp of stad om erheen te vliegen"); } }
        if (!inSpace) return false;
        outline.enabled = false; ShowBrush(-1); ShowGhost(-1); ShowVerts(-1); ShowPlan(null); hasHover = false;
        if (Inp.Down(K.H)) { centreT = -99f; CityCentre(); if (hasCentre) FlyTo(centre, true, "Naar je stad"); }
        if (Inp.Down(K.N)) { bool north = Vector3.Dot(focus, Planet.Cells[Planet.North].dir) < 0.999f; FlyTo(Planet.Cells[north ? Planet.North : Planet.South].dir, false, north ? "Naar de noordpool (N, nog eens: zuidpool)" : "Naar de zuidpool (N)"); }
        if (Inp.Down(K.K)) FlyTo(-focus.normalized, false, "Naar de andere kant van de bol (K)");
        if (Inp.Down(K.G)) ToggleGrid();
        return true;
    }

    void FlyTo(Vector3 dir, bool land, string say) { flyGoal = dir.normalized; flyLand = land; if (say != null) Say(say); }

    // in CameraControl: zacht over de bol naar het doel; zelf schuiven of slepen breekt het af
    void FlyStep(float dt)
    {
        if (flyGoal == Vector3.zero) return;
        bool input = camDrag != 0 || Inp.Held(K.W) || Inp.Held(K.A) || Inp.Held(K.S) || Inp.Held(K.D) || Inp.Held(K.Up) || Inp.Held(K.Down) || Inp.Held(K.Left) || Inp.Held(K.Right);
        if (input) { flyGoal = Vector3.zero; return; }
        camVel = Vector3.zero;
        float ang = Vector3.Angle(focus, flyGoal);
        if (ang < 0.3f) { focus = flyGoal; if (flyLand) distGoal = LandDist; flyGoal = Vector3.zero; return; }
        float step = Mathf.Max(ang * (1f - Mathf.Exp(-2.2f * dt)), Mathf.Min(ang, 4f * dt));   // naijlen, maar nooit eindeloos
        focus = Vector3.RotateTowards(focus, flyGoal, step * Mathf.Deg2Rad, 0f).normalized;
        if (flyLand && ang < 25f) distGoal = Mathf.Min(distGoal, Mathf.Lerp(LandDist, 1600f, ang / 25f));   // het laatste stuk zakt de camera naar de grond
    }

    // ---- de interface in de ruimte
    struct Mark { public Vector3 dir; public string name, sub; public bool big, home; }
    readonly List<Mark> marks = new List<Mark>();
    void SpaceGui(float W, float H)
    {
        float a = Mathf.Clamp01((Time.unscaledTime - spaceT) / 0.4f); var old = GUI.color; GUI.color = new Color(1f, 1f, 1f, a);
        // markers: je stad, de polen, de dorpen en steden van Hele planeet vol
        marks.Clear();
        CityCentre(); if (hasCentre) marks.Add(new Mark { dir = centre, name = "Je stad", sub = "Stad " + PlanetCity.Slot, big = true, home = true });
        marks.Add(new Mark { dir = Planet.Cells[Planet.North].dir, name = "Noordpool", sub = "startplek", big = true });
        marks.Add(new Mark { dir = Planet.Cells[Planet.South].dir, name = "Zuidpool", sub = "startplek", big = true });
        var towns = PlanetCity.FillTowns; var kinds = PlanetCity.FillKinds;
        if (towns.Count > 0)
            for (int t = 0; t < towns.Count; t++)
            {
                var kind = t < kinds.Count ? kinds[t] : PlanetCity.Village.Kerkdorp;
                marks.Add(new Mark { dir = Planet.Cells[towns[t]].dir, name = TownName(towns[t]), sub = PlanetCity.VillageName(kind), big = kind == PlanetCity.Village.Stad || kind == PlanetCity.Village.Marktstad });
            }
        else foreach (var m in Clusters()) marks.Add(m);   // na opnieuw starten is FillTowns leeg: de plaatsen uit de bebouwing
        Vector3 camDir = Cam.transform.position.normalized; float near = Mathf.InverseLerp(1800f, SpaceOut, dist);   // dichterbij: ook de namen van de kleine dorpen
        Mark? hot = null; Rect hotR = default;
        foreach (var m in marks)
        {
            Vector3 p = m.dir * (Planet.R + 2f); if (Vector3.Dot(m.dir, camDir) < 0.25f) continue;   // achter de bol of te ver op de rand
            Vector3 sp = Cam.WorldToScreenPoint(p); if (sp.z <= 0f) continue;
            var c = new Vector2(sp.x / Ui.S, (Screen.height - sp.y) / Ui.S); float s = m.big ? 14f : 9f;
            var r = new Rect(c.x - s * 0.5f, c.y - s * 0.45f, s, s * 0.9f); var hitR = new Rect(c.x - 12f, c.y - 12f, 24f, 24f);
            bool hv = hitR.Contains(Ui.Mouse); Color col = m.home ? Ui.Signal : m.big ? Ui.Ink : new Color(Ui.Ink.r, Ui.Ink.g, Ui.Ink.b, 0.75f);
            Ui.Tex(r, Ui.Hex(hv ? Ui.Signal : new Color(0.08f, 0.1f, 0.13f, 0.85f), col, m.big ? 2f : 1.5f));
            if (m.big || near > 0.5f || hv) Ui.Label(new Rect(c.x - 80f, c.y + s * 0.5f, 160f, 16f), m.name, Ui.T.Bold, hv ? Ui.Signal : col, TextAnchor.MiddleCenter, m.big ? 11 : 10);
            Ui.Hit(hitR); if (hv) { hot = m; hotR = hitR; }
            if (GUI.Button(hitR, GUIContent.none, GUIStyle.none)) FlyTo(m.dir, true, "Naar " + (m.home ? "je stad" : m.name));
        }
        if (hot != null) { var m = hot.Value; PlanetCity.LatLon(m.dir, out float la, out float lo); Ui.Tip(hotR, m.name, m.sub + " · " + LatLonText(la, lo) + " · klik: erheen vliegen"); }
        // het midden: waar je naar kijkt
        PlanetCity.LatLon(focus, out float lat, out float lon);
        Ui.Tex(new Rect(W * 0.5f - 7f, H * 0.5f - 6f, 14f, 12.6f), Ui.Hex(Color.clear, new Color(1f, 1f, 1f, 0.6f), 1.5f));
        // onderaan: de navigatie
        string[] lab = { "Naar je stad", "Polen", "Andere kant", "Landen" }; string[] key = { "H", "N", "K", "" };
        float bw = 0f; foreach (var t in lab) bw += BtnW(t) + 6f; float lw = Mathf.Max(80f, Ui.Width("88° N  188° O", Ui.T.Dim, 11));   // vaste breedte: het paneel verspringt niet als de graden veranderen
        var pr = new Rect((W - (14f + lw + 14f + bw + 8f)) * 0.5f, H - 12f - 56f, 14f + lw + 14f + bw + 8f, 56f); Ui.Panel(pr, false, 14f);
        Ui.Label(new Rect(pr.x + 14f, pr.y + 7f, lw, 20f), "PLANEET", Ui.T.Tiny, Ui.Signal, null, 10);
        Ui.Label(new Rect(pr.x + 14f, pr.y + 27f, lw, 20f), LatLonText(lat, lon), Ui.T.Dim, null, null, 11);
        float x = pr.x + 14f + lw + 14f;
        for (int k = 0; k < lab.Length; k++)
        {
            var br = new Rect(x, pr.y + 10f, BtnW(lab[k]), 36f); x += br.width + 6f;
            if (Ui.Button(br, lab[k], false, null, false, 10f))
            {
                if (k == 0) { centreT = -99f; CityCentre(); if (hasCentre) FlyTo(centre, true, "Naar je stad"); }
                else if (k == 1) { bool north = Vector3.Dot(focus, Planet.Cells[Planet.North].dir) < 0.999f; FlyTo(Planet.Cells[north ? Planet.North : Planet.South].dir, false, north ? "Naar de noordpool" : "Naar de zuidpool"); }
                else if (k == 2) FlyTo(-focus.normalized, false, "Naar de andere kant van de bol");
                else { distGoal = LandDist; Say("Landen"); }
            }
            Ui.Tip(br, lab[k], k == 3 ? "Inzoomen tot je weer kunt bouwen" : k == 1 ? "Noordpool, nog eens: zuidpool" : null, key[k] != "" ? key[k] : null);
        }
        GUI.color = old;
    }
    // plaatsen uit de bebouwing: gebouwen binnen zo'n 6 vakken van elkaar horen bij één plaats (onthouden tot er iets verandert)
    readonly List<Mark> clusters = new List<Mark>(); int clusterVer = -1;
    List<Mark> Clusters()
    {
        if (clusterVer == beautyVer && clusters.Count > 0) return clusters;
        clusterVer = beautyVer; clusters.Clear();
        float cosR = Mathf.Cos(6f * 15.6f / Planet.R);
        var seeds = new List<int>(); var sum = new List<Vector3>(); var cnt = new List<int>();
        foreach (var kv in PlanetCity.Cells)
        {
            var l = kv.Value; if (l.Count == 0) continue; string b = l[l.Count - 1];
            if (!(b == "woning" || b == "kantoor" || b == "winkel" || b == "toren" || b == "molen" || b == "plein" || b == "dak" || b == "blok")) continue;
            Vector3 d = Planet.Cells[kv.Key].dir; int best = -1;
            for (int s = 0; s < seeds.Count; s++) if (Vector3.Dot(sum[s].normalized, d) > cosR) { best = s; break; }
            if (best < 0) { seeds.Add(kv.Key); sum.Add(d); cnt.Add(1); } else { sum[best] += d; cnt[best]++; }
        }
        for (int s = 0; s < seeds.Count; s++)
        {
            if (cnt[s] < 4) continue;   // een losse boerderij is geen plaats
            clusters.Add(new Mark { dir = sum[s].normalized, name = TownName(seeds[s]), sub = cnt[s] >= 60 ? "stad" : cnt[s] >= 20 ? "dorp" : "gehucht", big = cnt[s] >= 40 });
        }
        return clusters;
    }
    static string LatLonText(float lat, float lon) => Mathf.Abs(lat).ToString("0") + "° " + (lat >= 0f ? "N" : "Z") + "  " + Mathf.Abs(lon).ToString("0") + "° " + (lon >= 0f ? "O" : "W");

    // een vaste naam per dorp (uit het vaknummer), in de geest van de bijenkorf
    static readonly string[] NameA = { "Raat", "Honing", "Korf", "Was", "Zes", "Bijen", "Hoek", "Linde", "Klaver", "Heide", "Molen", "Brink", "Dauw", "Hazel", "Eik", "Riet" };
    static readonly string[] NameB = { "dorp", "hoven", "wijk", "dam", "veen", "horst", "berg", "veld", "loo", "hem", "rade", "sloot", "rode", "brug", "meer", "kamp" };
    static string TownName(int cell)
    {
        uint h = (uint)cell * 2654435761u; string n = NameA[(h >> 8) % NameA.Length] + NameB[(h >> 16) % NameB.Length];
        return (h >> 28) % 5 == 0 ? ((h >> 4) % 2 == 0 ? "Groot " : "Nieuw ") + n : n;
    }
}
