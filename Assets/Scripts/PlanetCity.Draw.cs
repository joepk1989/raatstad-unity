using System.Collections.Generic;
using UnityEngine;

// Tekenen per deel: meertje, kleuren, Rebuild/RebuildSome/RebuildSteps, bouwvoorbeeld (GhostMesh), BuildCell en BuildChunk.
public static partial class PlanetCity
{
    // Een meertje: zand aan de kant, water iets lager, riet langs de oever. Grenst een buurvak ook aan een meer,
    // dan loopt het water door tot de gedeelde rand, zodat meerdere vakken één meer vormen.
    static void Lake(Dictionary<string, MeshBuilder> mb, Frame fr, int i, float h, int hash)
    {
        System.Func<string, MeshBuilder> M = k => { if (!mb.TryGetValue(k, out var x)) { x = new MeshBuilder(); mb[k] = x; } return x; };
        var c = Planet.Cells[i]; int n = fr.poly.Length;
        // per rand: open naar een buurmeer? De buur met dezelfde richting als de rand (middelpunt van de rand ligt richting die buur)
        var open = new bool[n];
        for (int e = 0; e < n; e++)
        {
            Vector2 mid = (fr.poly[e] + fr.poly[(e + 1) % n]) * 0.5f; int best = -1; float bd = -1f;
            foreach (int j in c.nb) { Vector3 d3 = Planet.Cells[j].dir * Planet.R - fr.o; Vector2 q = new Vector2(Vector3.Dot(d3, fr.e), Vector3.Dot(d3, fr.n)).normalized; float d = Vector2.Dot(q, mid.normalized); if (d > bd) { bd = d; best = j; } }
            open[e] = best >= 0 && Cells.TryGetValue(best, out var l) && l.Count > 0 && l[0] == "meer";
        }
        // waterrand: hoekpunt op de rand als beide aangrenzende randen open zijn, anders naar binnen (golvend)
        var wp = new Vector2[n]; var rnd = new System.Random(hash);
        for (int k = 0; k < n; k++)
        {
            float kk = (open[(k + n - 1) % n] || open[k]) ? 1.0f : 0.68f + (float)rnd.NextDouble() * 0.1f;   // naast een open rand precies op het hoekpunt (anders blijft er een driehoekje open tussen de buurmeren)
            wp[k] = fr.poly[k] * kk;
        }
        float wy = h - 0.35f;
        for (int k = 0; k < n; k++)
        {
            Vector2 a = wp[k], b = wp[(k + 1) % n], am = open[k] ? (fr.poly[k] + fr.poly[(k + 1) % n]) * 0.5f : (a + b) * 0.5f * 0.95f;
            M("water").Tri(fr.P(0, wy, 0), fr.P(a.x, wy, a.y), fr.P(am.x, wy, am.y), fr.u); M("water").Tri(fr.P(0, wy, 0), fr.P(am.x, wy, am.y), fr.P(b.x, wy, b.y), fr.u);
            // oever: een helling van het zand (h+0.03) naar onder water (wy-0.3), en riet op gesloten randen
            Vector2 oa = fr.poly[k] * 0.98f, ob = fr.poly[(k + 1) % n] * 0.98f;
            if (!open[k]) {
                // buitenrand (zand, iets de bodem in) en de oever: van de rand schuin omlaag tot onder water
                Vector2 pa = fr.poly[k], pb = fr.poly[(k + 1) % n], pm = (pa + pb) * 0.5f;
                M("zand").Quad(fr.P(pa.x, -0.25f, pa.y), fr.P(pb.x, -0.25f, pb.y), fr.P(pb.x, h + 0.03f, pb.y), fr.P(pa.x, h + 0.03f, pa.y), fr.D(pm.x, 0, pm.y));
                M("zand").Quad(fr.P(pa.x, h + 0.03f, pa.y), fr.P(pb.x, h + 0.03f, pb.y), fr.P(ob.x, h + 0.02f, ob.y), fr.P(oa.x, h + 0.02f, oa.y), fr.u);
                M("zand").Quad(fr.P(oa.x, h + 0.02f, oa.y), fr.P(ob.x, h + 0.02f, ob.y), fr.P(b.x, wy - 0.3f, b.y), fr.P(a.x, wy - 0.3f, a.y), fr.D(-am.x, 0.5f * am.magnitude, -am.y));
                int reeds = 2 + rnd.Next(0, 3);
                for (int r = 0; r < reeds; r++) { float t = 0.15f + (float)rnd.NextDouble() * 0.7f; Vector2 rp = Vector2.Lerp(a, b, t) * (0.97f + (float)rnd.NextDouble() * 0.06f); Cone(M("riet"), fr, rp.x, rp.y, 0.12f, wy - 0.1f, h + 0.9f + (float)rnd.NextDouble() * 0.5f, 0f); } }
        }
    }

    static Color ColorOf(string k)
    {
        switch (k)
        {
            case "muur": case "muur0": return new Color(0.93f, 0.86f, 0.72f); case "muur1": return new Color(0.72f, 0.42f, 0.32f); case "muur2": return new Color(0.88f, 0.88f, 0.84f); case "muur3": return new Color(0.55f, 0.5f, 0.45f);
            case "dak": case "dak0": return new Color(0.65f, 0.30f, 0.22f); case "dak1": return new Color(0.35f, 0.33f, 0.36f);
            case "paal": return new Color(0.25f, 0.27f, 0.3f); case "rand": return new Color(0.7f, 0.68f, 0.62f); case "lamp": return new Color(0.85f, 0.85f, 0.8f);
            case "kantoor": return new Color(0.58f, 0.68f, 0.78f); case "glas": return new Color(0.20f, 0.30f, 0.40f);
            case "plat": return new Color(0.50f, 0.50f, 0.53f); case "gras": return new Color(0.52f, 0.74f, 0.33f);
            case "bodem0": return new Color(0.64f, 0.78f, 0.45f);   // hetzelfde materiaal als het gewone terrein (p_bodem0), voor de bovenkant van een brugblok
            case "water": return new Color(0.20f, 0.50f, 0.78f); case "zand": return new Color(0.86f, 0.79f, 0.55f);
            case "pad": return new Color(0.82f, 0.76f, 0.63f); case "fietspad": return new Color(0.62f, 0.27f, 0.22f); case "blad": return new Color(0.20f, 0.50f, 0.22f);
            case "steen": return new Color(0.62f, 0.60f, 0.58f); case "tunnel": return new Color(0.30f, 0.30f, 0.31f); case "stoep": return new Color(0.88f, 0.84f, 0.72f);
            case "asfalt": return new Color(0.27f, 0.28f, 0.30f); case "lijn": return Color.white;
            case "modder": return new Color(0.45f, 0.38f, 0.26f); case "riet": return new Color(0.45f, 0.6f, 0.25f);
            case "etalage": return new Color(0.55f, 0.68f, 0.75f); case "doek": return new Color(0.95f, 0.93f, 0.88f);
            case "luifel0": return new Color(0.78f, 0.2f, 0.18f); case "luifel1": return new Color(0.2f, 0.5f, 0.32f); case "luifel2": return new Color(0.18f, 0.35f, 0.62f);
            case "bord": return new Color(0.95f, 0.70f, 0.23f); case "klinker": return new Color(0.70f, 0.55f, 0.45f); case "hout": return new Color(0.52f, 0.36f, 0.22f);
            case "baksteen": return new Color(0.62f, 0.33f, 0.25f); case "leien": return new Color(0.24f, 0.27f, 0.32f); case "klok": return new Color(0.95f, 0.95f, 0.9f); case "goud": return new Color(0.9f, 0.72f, 0.25f);
            case "molen": return new Color(0.30f, 0.30f, 0.28f); case "kap": return new Color(0.42f, 0.36f, 0.26f); case "wiek": return new Color(0.35f, 0.22f, 0.15f); case "zeil": return new Color(0.92f, 0.9f, 0.84f);
            default: return NatureColor(k, out var nc) ? nc : new Color(0.40f, 0.28f, 0.18f);
        }
    }

    // Rebuild in stappen (voor een laadscherm): een paar delen per beeld, prog(deel 0..1)
    public static System.Collections.IEnumerator RebuildSteps(System.Action<float> prog)
    {
        UpdateCuts(); var todo = new List<int>(dirty); dirty.Clear(); gridV = null;   // alles nieuw: het raster ook helemaal
        for (int k = 0; k < todo.Count; k++) { BuildChunk(todo[k]); if (k % 6 == 5) { prog?.Invoke((k + 1f) / todo.Count); yield return null; } }
        foreach (int ch in new List<int>(dirty)) BuildChunk(ch); dirty.Clear();   // wat er intussen nog bij kwam
        RebuildRoads(); SyncModules();
    }
    // Rebuild in stukjes voor een klik: eerst het deel waar het gebeurde (Focus) en de wegen, de rest met een tijdbudget per beeld;
    // zo staat wat je bouwt meteen in beeld. true als alles bij is. Bij een nieuwe wijziging opnieuw beginnen (RebuildSoon).
    public static int Focus = -1; static bool midRebuild;
    public static void RebuildSoon(int focus) { Focus = focus; midRebuild = false; }
    public static bool RebuildSome(float budgetMs)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew(); bool first = !midRebuild;
        if (first) { SyncModules(); UpdateCutsIfNeeded(); if (dirty.Count == 0) return true; midRebuild = true; }
        var order = new List<int>(dirty); int fc = Focus >= 0 ? Focus / ChunkN : 0; order.Sort((a, b) => Mathf.Abs(a - fc).CompareTo(Mathf.Abs(b - fc)));   // het deel van de klik eerst
        foreach (int ch in order) { BuildChunk(ch); lastBuilt.Add(ch); dirty.Remove(ch); if (sw.ElapsedMilliseconds >= budgetMs) break; }
        if (first) RebuildRoads();
        if (dirty.Count > 0) return false;
        midRebuild = false; return true;
    }
    public static void Rebuild()
    {
        midRebuild = false;
        SyncModules(); UpdateCutsIfNeeded();
        if (dirty.Count == 0) return;
        foreach (int ch in dirty) { BuildChunk(ch); lastBuilt.Add(ch); }
        dirty.Clear();
        RebuildRoads();   // het wegennet: alleen de lijnen en knopen die veranderd zijn
    }

    // ---------- bouwvoorbeeld (het spookblok van de webversie) ----------

    // De vorm van wat Place(i, type) zou toevoegen, of bij de gum de hele stapel, als één mesh rond het hart van het vak (origin).
    // null als er niets te tonen is (een wegvak: dat tekent het wegennet).
    public static Mesh GhostMesh(int i, string type, bool erase, out Vector3 origin)
    {
        origin = Planet.Cells[i].dir * (Planet.R + G(i));
        if (type == "rotonde" && !erase) { var rmb = new Dictionary<string, MeshBuilder>(); var ra = new List<int>(); foreach (int j in Planet.Cells[i].nb) if (IsRoad(j)) ra.Add(j); RoundaboutMesh(rmb, i, ra); return MergeMesh(rmb, origin); }
        Cells.TryGetValue(i, out var cur); var list = cur != null ? new List<string>(cur) : new List<string>(); int from;
        if (erase) { if (list.Count == 0 || list[0] == "weg") return null; from = 0; }
        else
        {
            if (list.Count > 0 && list[0] == "weg") return null;
            if (type == "water" || type == "meer" || (list.Count > 0 && (list[list.Count - 1] == "water" || list[list.Count - 1] == "meer"))) { bool half = list.Count > 0 && list[0] == "wegdeel"; list.Clear(); if (half) list.Add("wegdeel"); }
            if (TreesYield(list, type)) list.Clear();
            from = list.Count; list.Add(type);
        }
        var mb = new Dictionary<string, MeshBuilder>(); BuildCell(mb, i, list, new List<(Vector3, Quaternion, int)>(), from);
        return MergeMesh(mb, origin);
    }
    // alle stukken van een tekening samen tot één mesh rond origin (voor het bouwvoorbeeld, dat één doorzichtige kleur heeft)
    static Mesh MergeMesh(Dictionary<string, MeshBuilder> mb, Vector3 origin)
    {
        var all = new MeshBuilder();
        foreach (var b in mb.Values) { int o = all.v.Count; foreach (var p in b.v) all.v.Add(p - origin); all.n.AddRange(b.n); foreach (int t in b.t) all.t.Add(o + t); }
        return all.v.Count == 0 ? null : all.Build();
    }

    // Eén vak tekenen: de stapel blokken (vanaf blok 'from'; het bouwvoorbeeld tekent alleen het nieuwe blok bovenop)
    static void BuildCell(Dictionary<string, MeshBuilder> mb, int i, List<string> list, List<(Vector3 pos, Quaternion rot, int hash)> mills, int from)
    {
        if (IsModItem(list[0])) return;   // een module tekent zichzelf (PlanetCity.Modules.cs)
        System.Func<string, MeshBuilder> M = k => { if (!mb.TryGetValue(k, out var x)) { x = new MeshBuilder(); mb[k] = x; } return x; };
        var fr = new Frame(i);
            if (list[0] == "weg") { if (Under.ContainsKey(i)) { UnderMesh(mb, fr, i, i * 73856093); BridgePillar(M, fr, i); } return; }   // de weg zelf tekent het wegennet (PlanetRoads.cs); hier alleen wat onder een brug ligt
            if (list[0] == "wegdeel")   // half vak naast een schuine weg: de helft naar de weg toe is gewoon de grond (geen plaat erop); wat erop gebouwd is staat op de vrije helft
            {
                var rest = list.GetRange(1, list.Count - 1);
                if (InCut(i)) { if (rest.Count == 0) return; }   // in de geul van een onderdoorgang: niets
                else if (!HalfNormal(i, out var hn)) { if (rest.Count == 0) return; }   // zonder bekende weg: gewoon een vak, de grond ligt er al
                else
                {
                    var hd = HalfDirs(i, hn); var lp = new List<Vector3>();
                    foreach (var d in hd) { Vector3 w = d * Planet.R - fr.o; lp.Add(new Vector3(Vector3.Dot(w, fr.e), 0f, Vector3.Dot(w, fr.n))); }
                    if (rest.Count == 0) return;
                    var fd = HalfDirs(i, hn, true);
                    if (fd.Count >= 3)
                    {
                        Vector3 cen = Vector3.zero; foreach (var d in fd) cen += d; cen = (cen / fd.Count).normalized; Vector3 o2 = cen * (Planet.R + G(i));
                        var pl = new Vector2[fd.Count]; float rs = 0f;
                        for (int k2 = 0; k2 < fd.Count; k2++) { Vector3 w = fd[k2] * Planet.R - o2; pl[k2] = new Vector2(Vector3.Dot(w, fr.e), Vector3.Dot(w, fr.n)) * 0.86f; rs += pl[k2].magnitude; }
                        fr = new Frame(fr, o2, pl, rs / fd.Count);
                    }
                }
                list = rest; from = Mathf.Max(0, from - 1);
            }
            if (list.Count == 1 && list[0] == "meer") return;   // een meer is diep terrein onder het zeeniveau (PlanetNature.cs)
            if (list.Count == 1 && list[0] == "pad") return;   // paden tekent het wegennet (PlanetPaths.cs)
            float h = 0f; int hash = i * 73856093;
            for (int k = 0; k < list.Count; k++)
            {
                string t = list[k]; bool top = k == list.Count - 1; float h1 = h + Blocks.Height(t), h0 = k == 0 ? -0.2f : h;   // onderste blok iets de bodem in (de bol loopt af naar de hoeken)
                if (k < from) { h = h1; continue; }   // bouwvoorbeeld: alleen het nieuwe blok bovenop
                switch (t)
                {
                    case "woning":
                        { string mu = "muur" + ((hash >> 3) & 3), dk = "dak" + ((hash >> 7) & 1);
                        Wall(M(mu), fr, 0.78f, h0, h1); Windows(M("glas"), fr, 0.78f, h + 1.0f, h + 2.0f, 0.18f, 0.40f); Windows(M("glas"), fr, 0.78f, h + 1.0f, h + 2.0f, 0.60f, 0.82f);
                        if (top) { if (!HouseRoof(M(dk), M("plat"), M("glas"), fr, 0.84f, h1, hash)) { Roof(M(dk), fr, 0.84f, h1); Top(M(dk), fr, 0.84f, h1 - 0.02f); } } else Top(M(mu), fr, 0.78f, h1); }   // het dak uit SketchUp (PlanetCity.Daken.cs)
                        break;
                    case "kantoor":
                        Wall(M("kantoor"), fr, 0.9f, h0, h1); Windows(M("glas"), fr, 0.9f, h + 0.5f, h + 1.2f, 0.08f, 0.92f); Windows(M("glas"), fr, 0.9f, h + 1.7f, h + 2.4f, 0.08f, 0.92f);
                        Top(M("plat"), fr, 0.9f, h1); if (top) { Wall(M("plat"), fr, 0.9f, h1, h1 + 0.25f); Top(M("plat"), fr, 0.9f, h1 + 0.25f); }
                        break;
                    case "dak":
                        Cone(M("dak"), fr, 0, 0, fr.rad * 0.95f, h, h1, fr.rad * 0.5f); Top(M("dak"), fr, 0.5f, h1);
                        break;
                    case "ijs": case "woestijn": case "heide": case "droogheide": case "palmen": case "rotsen":
                        Wild(M, fr, i, hash, t);   // landschappen van de natuurplaneet (PlanetWild.cs)
                        break;
                    case "park":
                        if (k > 0) Top(M("gras"), fr, 0.98f, h + 0.05f);   // daktuin; op de grond staan de bomen los op het terrein
                        Trees(M, fr, i, hash, h + 0.05f, k > 0);   // PlanetNature.cs
                        break;
                    case "water":
                        Canal(M, i); break;   // gracht: bodem en kademuren, het water komt van het zeeniveau (PlanetNature.cs)
                    case "meer":
                        break;
                    case "kuil":   // uitgraving: de bodem 3 m lager, zeshoekige grashellingen van de rand naar beneden
                        for (int e2 = 0; e2 < fr.poly.Length; e2++)
                        {
                            Vector2 a2 = C(fr, e2, 1.0f), b2 = C(fr, e2 + 1, 1.0f), a3 = C(fr, e2, 0.55f), b3 = C(fr, e2 + 1, 0.55f), m2 = (a2 + b2) * 0.5f;
                            M("gras").Quad(fr.P(a2.x, h, a2.y), fr.P(b2.x, h, b2.y), fr.P(b3.x, h1, b3.y), fr.P(a3.x, h1, a3.y), fr.D(-m2.x, 2f, -m2.y));
                        }
                        Top(M("gras"), fr, 0.55f, h1);
                        break;
                    case "pad":
                        Wall(M("pad"), fr, 0.72f, h0, h1); Top(M("pad"), fr, 0.72f, h1);
                        break;
                    case "heuvel":
                        Cone(M("gras"), fr, 0, 0, fr.rad * 1.0f, h0, h1, fr.rad * 0.55f); Cap6(M("gras"), fr, fr.rad * 0.55f, h1);
                        break;
                    case "blok":
                        Wall(M("steen"), fr, 0.96f, h0, h1); Top(M("steen"), fr, 0.96f, h1);
                        break;
                    case "winkel":   // winkel: grote etalages (lichten 's avonds op), een gestreepte luifel en een naambord; erboven kan gewoon gewoond worden
                        { string mu = "muur" + ((hash >> 3) & 3);
                        Wall(M(mu), fr, 0.86f, h0, h1); Windows(M("etalage"), fr, 0.86f, h + 0.3f, h + 2.0f, 0.14f, 0.86f); Windows(M("bord"), fr, 0.86f, h + 2.45f, h + 2.85f, 0.3f, 0.7f);
                        Awning(M, fr, 0.87f, h + 2.35f, "luifel" + ((hash >> 5) & 0xffff) % 3);
                        Top(M(top ? "plat" : mu), fr, 0.86f, h1); if (top) { Wall(M("plat"), fr, 0.86f, h1, h1 + 0.3f); Top(M("plat"), fr, 0.86f, h1 + 0.3f); } }
                        break;
                    case "plein":   // plein: klinkers, een fontein in het midden, bankjes eromheen en twee lantaarns
                        { Top(M("klinker"), fr, 1.0f, h1); Wall(M("klinker"), fr, 1.0f, h0, h1); Top(M("steen"), fr, 0.62f, h1 + 0.005f); Top(M("klinker"), fr, 0.5f, h1 + 0.01f);
                        if (k > 0 || FountainHere(i)) { Skirt(M("steen"), fr, 2.4f, h1, h1 + 0.55f); Disc(M("steen"), fr, 2.4f, h1 + 0.55f); Disc(M("water"), fr, 2.1f, h1 + 0.57f);
                        Cone(M("steen"), fr, 0f, 0f, 0.3f, h1 + 0.55f, h1 + 1.6f, 0.22f); Cone(M("steen"), fr, 0f, 0f, 0.25f, h1 + 1.45f, h1 + 1.75f, 1.0f); Disc(M("water"), fr, 0.9f, h1 + 1.76f); }
                        float a0 = (hash & 63) / 63f * Mathf.PI * 2f;
                        for (int j = 0; j < 3; j++)
                        {
                            float a = a0 + j * Mathf.PI * 2f / 3f, rr = fr.rad * 0.6f, bx = Mathf.Cos(a) * rr, bz = Mathf.Sin(a) * rr, tang = a + Mathf.PI / 2f;
                            Box(M("hout"), fr, bx, bz, tang, 0.9f, 0.24f, h1 + 0.42f, h1 + 0.5f);
                            Box(M("hout"), fr, bx + Mathf.Cos(a) * 0.24f, bz + Mathf.Sin(a) * 0.24f, tang, 0.9f, 0.04f, h1 + 0.5f, h1 + 0.95f);
                            for (int s2 = -1; s2 <= 1; s2 += 2) Box(M("paal"), fr, bx + Mathf.Cos(tang) * 0.75f * s2, bz + Mathf.Sin(tang) * 0.75f * s2, tang, 0.05f, 0.2f, h1, h1 + 0.42f);
                            if (j < 2) { float la = a + Mathf.PI / 3f, lx = Mathf.Cos(la) * fr.rad * 0.78f, lz = Mathf.Sin(la) * fr.rad * 0.78f; Cone(M("paal"), fr, lx, lz, 0.08f, h1, h1 + 4.2f, 0.06f); Cone(M("lamp"), fr, lx, lz, 0.28f, h1 + 3.9f, h1 + 4.45f, 0f); }
                        } }
                        break;
                    case "toren":   // kerktoren: stenen voet, bakstenen schacht met klok en galmgaten, leien spits met een gouden knop
                        Wall(M("steen"), fr, 0.55f, h0, h + 2.2f); Top(M("steen"), fr, 0.55f, h + 2.2f);
                        Wall(M("baksteen"), fr, 0.4f, h + 2.2f, h1 - 0.3f); Windows(M("klok"), fr, 0.4f, h1 - 4.6f, h1 - 3.5f, 0.3f, 0.7f); Windows(M("glas"), fr, 0.4f, h1 - 2.5f, h1 - 1.0f, 0.36f, 0.64f);
                        Wall(M("steen"), fr, 0.45f, h1 - 0.3f, h1); Top(M("steen"), fr, 0.45f, h1);
                        if (top) { Spire(M("leien"), fr, 0.42f, h1, 8f); Cone(M("goud"), fr, 0f, 0f, 0.14f, h1 + 7.6f, h1 + 8.6f, 0f); }
                        break;
                    case "molen":   // molen: taps toelopende romp, een kap en vier draaiende wieken, met het gezicht naar een willekeurige kant
                        { float r0 = fr.rad * 0.5f, r1 = fr.rad * 0.3f, a = ((hash >> 4) & 7) / 8f * Mathf.PI * 2f; Vector2 fd = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        Cone(M("steen"), fr, 0f, 0f, r0 * 1.06f, h0, h + 0.7f, r0 * 1.04f);
                        Cone(M("molen"), fr, 0f, 0f, r0, h + 0.7f, h1 - 1.6f, r1); Cone(M("kap"), fr, 0f, 0f, r1 * 1.18f, h1 - 1.6f, h1 + 0.8f, 0.2f);
                        Box(M("hout"), fr, fd.x * (r0 - 0.1f), fd.y * (r0 - 0.1f), a, 0.35f, 0.6f, h + 0.7f, h + 2.6f);   // deur
                        Box(M("wiek"), fr, fd.x * (r1 + r0 + 0.5f) * 0.5f, fd.y * (r1 + r0 + 0.5f) * 0.5f, a, (r0 + 0.5f - r1) * 0.5f + 0.5f, 0.15f, h1 - 1.15f, h1 - 0.85f);   // as
                        float hub = r0 + 0.5f; mills.Add((fr.P(fd.x * hub, h1 - 1.0f, fd.y * hub), Geo.Look(fr.D(fd.x, 0f, fd.y), fr.u), hash)); }
                        break;
                }
                h = h1;
            }
    }

    static void BuildChunk(int ch)
    {
        BuildGround(ch);
        if (chunks.TryGetValue(ch, out var old) && old != null) UnityEngine.Object.Destroy(old.gameObject);
        chunks.Remove(ch);
        var mb = new Dictionary<string, MeshBuilder>();
        System.Func<string, MeshBuilder> M = k => { if (!mb.TryGetValue(k, out var x)) { x = new MeshBuilder(); mb[k] = x; } return x; };
        int i0 = ch * ChunkN, i1 = Mathf.Min(Planet.Cells.Length, i0 + ChunkN);
        var mills = new List<(Vector3 pos, Quaternion rot, int hash)>();   // molenwieken: losse objecten die draaien
        try
        {
            for (int i = i0; i < i1; i++) if (Cells.TryGetValue(i, out var list) && list.Count > 0)
            {
                if ((bores.Count > 0 || spans.Count > 0) && IsWalk(i)) { UseAll(); BoreMesh(mb, i); }   // een tunnel door een berg (koker, portalen en betonblok) of een brug over een dal (PlanetTunnels.cs)
                if (fieldCells.Contains(i)) UseNear(i); else UseAll();   // alleen de wegen in de buurt (een brug meet het terrein op zo'n honderd punten)
                BuildCell(mb, i, list, mills, 0);
            }
        }
        finally { UseAll(); }
        if (mb.Count == 0) return;
        var go = new GameObject("Deel " + ch); go.transform.SetParent(root, false); chunks[ch] = go.transform;
        foreach (var kv in mb)
        {
            if (kv.Value.v.Count == 0) continue;
            var child = new GameObject(kv.Key); child.transform.SetParent(go.transform, false);
            child.layer = (kv.Key == "asfalt" || kv.Key == "stoep" || kv.Key == "rand" || kv.Key == "lijn" || kv.Key == "paal" || kv.Key == "lamp" || kv.Key == "pad") ? Layers.Weg : Layers.Gebouw;
            bool wet = kv.Key == "water" || kv.Key == "zand"; if (wet) child.layer = Layers.Water;   // een meer heeft geen bodem: het water en de oever zijn om aan te wijzen (weghalen), niet om op te lopen
            var mesh = kv.Value.Build(); child.AddComponent<MeshFilter>().sharedMesh = mesh; child.AddComponent<MeshRenderer>().sharedMaterial = kv.Key == "lamp" || kv.Key == "etalage" ? Mats.GetUnlit("p_" + kv.Key, ColorOf(kv.Key)) : MatOf(kv.Key, ColorOf(kv.Key));
            if (kv.Key == "bodem0" && SoilMat() != null) { PlainSoil(mesh); child.GetComponent<MeshRenderer>().sharedMaterial = SoilMat(); }   // de grasbovenkant van een brugblok: zoals het terrein ernaast (PlanetSoil.cs)
            if (wet || kv.Key != "lijn" && kv.Key != "lamp" && kv.Key != "riet" && kv.Key != "doek" && !kv.Key.StartsWith("luifel")) child.AddComponent<MeshCollider>().sharedMesh = mesh;
        }
        foreach (var ml in mills) MakeWieken(go.transform, ml.pos, ml.rot, ml.hash);
    }
}
