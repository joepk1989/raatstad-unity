using System.Collections.Generic;
using UnityEngine;

// Tunnels door een berg of heuvel. Ligt het terrein op een recht stuk van een gesleepte weg of pad ergens minstens 9 m boven de
// rechte lijn tussen de voet aan beide kanten (met Ongelijkvloers, U: 6 m, net genoeg voor de koker), dan gaat dat stuk erdoorheen
// in plaats van eroverheen. In de graaf is dat een tunnelverbinding (TunnelLinks, zoals een weg onder een brug door) van het laatste
// wegvak vóór de berg naar het eerste erna; de vakken ertussen zijn weer gewoon terrein met hun eigen hoogte (er kan op gebouwd
// worden). Getekend naar het model tunnel entrance.dae (hexgrid city concept): de zeshoekige koker en het portaal van de
// onderdoorgang, met eromheen een zeshoekige mantel (2,17 m boven en onder de koker, 3,75 m opzij, de zijden onder 30°) die uit de
// helling steekt. Het terrein binnen de mantel wordt weggesneden (BoreTri), de rest blijft staan. De weg in de tunnel helt recht van
// voet tot voet; het verkeer rijdt er vanzelf door (SurfaceH helt van hart tot hart naar de partner).
public static partial class PlanetCity
{
    const float BoreGrade = 0.15f;   // hooguit 15% helling in een tunnel
    static float BoreNeed => DragUnder ? 6f : 2f * LevelH;   // zoveel terrein moet ergens boven de weg liggen

    // is a–b een tunnel door een berg (een tunnelverbinding over meer dan één vak)?
    public static bool IsBore(int a, int b) => TunnelLinks.Contains(Key(a, b)) && MidCell(a, b) < 0;
    // begint in vak i een tunnel door een berg (een portaal)?
    static bool BorePortal(int i) { if (TunnelLinks.Count == 0) return false; foreach (int j in Partners(i)) if (IsBore(i, j)) return true; return false; }

    // De tunnels op een lijn: per recht stuk de langste waarbij elk vak ertussen minstens een half niveau boven de rechte weg ligt
    // (ground) en ergens BoreNeed, dan links en rechts daarvan opnieuw. plain: kan een portaal zijn; step: de lijn gaat recht naar de
    // buur; inner: mag de berg in; road: de hoogte van de weg in een portaalvak. Geeft de paren (portaal, portaal) als index in de lijn.
    static List<(int i, int j)> BoreSearch(List<int> line, System.Func<int, bool> plain, System.Func<int, bool> step, System.Func<int, bool> inner, System.Func<int, float> road, System.Func<int, float> ground)
    {
        var done = new List<(int i, int j)>();
        void Search(int lo, int hi)
        {
            int bi = -1, bj = -1; float best = 0f;
            for (int i = lo; i < hi; i++)
            {
                if (!plain(line[i])) continue;
                for (int j = i + 1; j <= hi; j++)
                {
                    if (!step(j - 1) || (j - 1 > i && !inner(j - 1))) break;
                    if (j - i < 3 || !plain(line[j])) continue;   // minstens twee vakken berg (één vak is een gewone brug)
                    float ga = road(line[i]), gb = road(line[j]), dist = (Planet.Cells[line[i]].dir - Planet.Cells[line[j]].dir).magnitude * Planet.R;
                    if (Mathf.Abs(gb - ga) > BoreGrade * dist) continue;
                    float mn = float.MaxValue, mx = float.MinValue;
                    for (int k = i + 1; k < j; k++) { float cv = ground(line[k]) - Mathf.Lerp(ga, gb, (k - i) / (float)(j - i)); mn = Mathf.Min(mn, cv); mx = Mathf.Max(mx, cv); }
                    if (mn < StepH || mx < BoreNeed) continue;
                    if (j - i > bj - bi || (j - i == bj - bi && mn > best)) { bi = i; bj = j; best = mn; }
                }
            }
            if (bi < 0) return; done.Add((bi, bj)); Search(lo, bi); Search(bj, hi);
        }
        if (line != null && line.Count >= 4) Search(0, line.Count - 1);
        return done;
    }
    // Na het leggen van een lijn op het maaiveld: de stukken die door een berg moeten, worden een tunnel. Alleen vakken die alleen bij
    // deze lijn horen (geen kruising, brug of weg op een niveau) gaan de berg in.
    public static int BoreLine(List<int> line)
    {
        if (line == null || line.Count < 4 || DragLevel != 0) return 0;
        bool Plain(int c) => IsWalk(c) && !Underpass(c) && !IsRot(c) && !preCarve.ContainsKey(c);
        var done = BoreSearch(line, Plain, k => IsNeighbour(line[k], line[k + 1]) && Linked(line[k], line[k + 1]),
            k => { int c = line[k], p = line[k - 1], q = line[k + 1]; var ps = Partners(c); return Plain(c) && ps.Count == 2 && ps.Contains(p) && ps.Contains(q) && MidCell(p, q) == c; },
            G, c => preLevel.TryGetValue(c, out var h) ? h : G(c));   // het terrein van voor de weg (SetLevel zette hem op de hoogte van zijn buren)
        float len = 0f;
        foreach (var (i, j) in done)
        {
            int a = line[i], b = line[j], f = Flow(a, line[i + 1]);
            for (int k = i + 1; k < j; k++) Clear(line[k]);   // het stuk door de berg is geen weg op het maaiveld meer
            LinkTunnel(a, b); if (f > 0) OneWay.Add(DKey(a, b)); else if (f < 0) OneWay.Add(DKey(b, a));
            Touch(a); Touch(b); len += (Planet.Cells[a].dir - Planet.Cells[b].dir).magnitude * Planet.R - 2f * FlatL;
        }
        if (done.Count > 0) LastNote = (done.Count == 1 ? "Een tunnel" : done.Count + " tunnels") + " door de berg (" + Mathf.RoundToInt(len) + " m)";
        return done.Count;
    }
    // Voor het voorbeeld tijdens het slepen: waar de geplande lijn een tunnel krijgt, met de hoogte van de weg bij de portalen (zoals
    // BoreLine na PlaceLine en SetLevel: een nieuw vak komt op de hoogte van zijn buren, de berg in gaan alleen nieuwe vakken)
    public static List<(int a, int b, float ha, float hb)> BorePlan(List<int> line, string type)
    {
        var o = new List<(int, int, float, float)>(); if (line == null || line.Count < 4 || DragLevel != 0) return o;
        bool Plain(int c) => !Underpass(c) && !IsRot(c) && !preCarve.ContainsKey(c) && (IsWalk(c) || CanRoad(c, type));
        float Road(int c)   // een nieuw vak: het gemiddelde van zijn buren die geen weg of pad zijn en niet op de lijn liggen (PadGround na het leggen)
        {
            if (IsWalk(c)) return G(c); float s = 0f; int n = 0;
            foreach (int j in Planet.Cells[c].nb) if (!IsWalk(j) && !Underpass(j) && !line.Contains(j)) { s += G(j); n++; }
            return n > 0 ? s / n : G(c);
        }
        foreach (var (i, j) in BoreSearch(line, Plain, k => IsNeighbour(line[k], line[k + 1]), k => !IsWalk(line[k]) && Plain(line[k]) && MidCell(line[k - 1], line[k + 1]) == line[k], Road, G))
        {
            int a = line[i], b = line[j]; float ha = Road(a), hb = Road(b);
            if (a > b) { (a, b) = (b, a); (ha, hb) = (hb, ha); }   // zoals de tunnel straks getekend wordt: vanaf het kleinste vak
            o.Add((a, b, ha, hb));
        }
        return o;
    }

    // halve breedte van de koker: een weg 6 m (een brede weg of laan wat meer), een pad 4,5 m
    static float BoreHW(int a) => IsRoad(a) ? Mathf.Max(RoadHW, FlatHW(a)) : PathHW;
    // Het vlak van een tunnel: in het hart van a, e naar b, op de bol (gnomonisch: een rechte lijn in het vlak is een grote cirkel),
    // y = 0 een niveau boven de weg (het dek, zoals bij een brug) en hellend met de weg mee. p0, p1: de portalen, een half vak van
    // het hart van a en van b.
    static Frame BoreFrame(int a, int b, out Vector2 p0, out Vector2 p1, out float k)
    {
        Vector3 dA = Planet.Cells[a].dir, dB = Planet.Cells[b].dir;
        var f = new Frame(a, true).Flat(); f.e = Tan(dB - dA, dA); f.n = Vector3.Cross(f.u, f.e).normalized;
        float X(Vector3 d) => Vector3.Dot(d * (Planet.R / Vector3.Dot(d, dA)) - dA * Planet.R, f.e);
        float L = X(dB); k = (G(b) - G(a)) / L;
        p0 = new Vector2(FlatL, 0f); p1 = new Vector2(X((dB * Planet.R + Tan(dA - dB, dB) * FlatL).normalized), 0f);
        return f.Lift(LevelH).Incline(k);
    }
    // de koker, de portalen en de mantel van elke tunnel die in a begint (de kleinste van de twee tekent)
    static void BoreMesh(Dictionary<string, MeshBuilder> mb, int a)
    {
        System.Func<string, MeshBuilder> M = key => { if (!mb.TryGetValue(key, out var y)) { y = new MeshBuilder(); mb[key] = y; } return y; };
        foreach (int b in Partners(a))
        {
            if (b < a || !IsBore(a, b)) continue;
            var f = BoreFrame(a, b, out var p0, out var p1, out _); tubeW = BoreHW(a); float fl = -LevelH, top = -DeckT;
            int ns = Mathf.Max(1, Mathf.CeilToInt((p1 - p0).magnitude / 6f));   // in stukken, zodat de koker de bol volgt
            for (int s = 0; s < ns; s++) TubeSeg(M, f, Vector2.Lerp(p0, p1, s / (float)ns), Vector2.Lerp(p0, p1, (s + 1) / (float)ns), fl, fl, top, false, false);
            Portal(M, f, p0, new Vector2(-1f, 0f), fl, top); Portal(M, f, p1, new Vector2(1f, 0f), fl, top);
            Mantle(M, f, p0, p1, fl, top);
            for (float s = p0.x + 3f; s < p1.x - 2f; s += 6f)   // tunnellichten: om de 6 m twee lichtbalken tegen het plafond, boven elke rijstrook
                foreach (float x in new[] { -tubeW * 0.45f, tubeW * 0.45f })
                    M("lamp").Quad(f.P(s - 0.9f, top - 0.03f, x - 0.18f), f.P(s + 0.9f, top - 0.03f, x - 0.18f), f.P(s + 0.9f, top - 0.03f, x + 0.18f), f.P(s - 0.9f, top - 0.03f, x + 0.18f), -f.u);
            tubeW = PathHW;
        }
    }

    // De zeshoekige mantel om de koker (tunnel entrance.dae): de grote zeshoek van het portaal (onder en boven 3,75 m buiten de koker,
    // 2,17 m onder de vloer en boven het plafond, op halve hoogte onder 30° breder) als buis van portaal tot portaal, in stukken van
    // 6 m zodat hij de bol volgt. De binnenkant is de koker (TubeSeg), de voorkant het portaal (Portal).
    static void Mantle(System.Func<string, MeshBuilder> M, Frame f, Vector2 p0, Vector2 p1, float fl, float top)
    {
        float yB = fl - 2.17f, yO = top + 2.17f, co = (yO - yB) * 0.5f * 0.577f, ym = (yB + yO) * 0.5f, wb = tubeW + 3.75f;
        var o = new[] { new Vector2(-wb, yB), new Vector2(wb, yB), new Vector2(wb + co, ym), new Vector2(wb, yO), new Vector2(-wb, yO), new Vector2(-wb - co, ym) };
        int n = Mathf.Max(1, Mathf.CeilToInt((p1.x - p0.x) / 6f));
        for (int q = 0; q < n; q++)
        {
            float sa = Mathf.Lerp(p0.x, p1.x, q / (float)n), sb = Mathf.Lerp(p0.x, p1.x, (q + 1) / (float)n); Vector3 axis = f.P((sa + sb) * 0.5f, ym, 0f);
            for (int v = 0; v < 6; v++)
            {
                int w = (v + 1) % 6; Vector3 a0 = f.P(sa, o[v].y, o[v].x), a1 = f.P(sa, o[w].y, o[w].x), b1 = f.P(sb, o[w].y, o[w].x), b0 = f.P(sb, o[v].y, o[v].x);
                M("steen").Quad(a0, a1, b1, b0, (a0 + a1 + b1 + b0) * 0.25f - axis);   // naar buiten gekeerd
            }
        }
    }

    // ---------- het terrein in de mantel weg ----------
    // Het terrein is een hoogteveld: het kan niet tegelijk boven en onder de koker liggen. Wat binnen de zeshoekige mantel valt (vooral
    // bij de portalen, waar de berg nog laag is) wordt weggesneden, iets binnen de buitenkant zodat het terrein er net in steekt; de
    // mantel is daar de buitenkant. De rest blijft zoals het is.
    struct BoreC { public int a; public Vector3 dA, e, n; public float s0, s1, h0, k, tw, yO, ym, wm, cs; }   // langs e van s0 tot s1; h0 + k·s: het dek; tw: halve breedte van de koker; de zeshoek: bovenkant yO, halve hoogte ym (halve breedte wm), zijden cs breder per meter lager
    static readonly List<BoreC> bores = new List<BoreC>();
    static readonly Dictionary<int, List<int>> boreNear = new Dictionary<int, List<int>>();   // vak → de tunnels eronder
    static string boreSig = "";
    static void CollectBores()
    {
        var old = new List<int>(boreNear.Keys); bores.Clear(); boreNear.Clear(); var sb = new System.Text.StringBuilder();
        foreach (long key in TunnelLinks)
        {
            int a = (int)(key >> 20), b = (int)(key & 0xFFFFF); if (MidCell(a, b) >= 0 || !IsWalk(a) || !IsWalk(b)) continue;
            BoreFrame(a, b, out var p0, out var p1, out float k); Vector3 dA = Planet.Cells[a].dir, e = Tan(Planet.Cells[b].dir - dA, dA);
            // de mantel, zoals Mantle (ten opzichte van het dek), 10 cm kleiner
            float fl = -LevelH, top = -DeckT, yB = fl - 2.17f, yO = top + 2.17f, co = (yO - yB) * 0.5f * 0.577f, ym = (yB + yO) * 0.5f;
            int idx = bores.Count;
            bores.Add(new BoreC { a = a, dA = dA, e = e, n = Vector3.Cross(dA, e).normalized, s0 = p0.x, s1 = p1.x, h0 = G(a) + LevelH, k = k, tw = BoreHW(a), yO = yO - 0.1f, ym = ym, wm = BoreHW(a) + 3.75f + co - 0.1f, cs = co / (yO - ym) });
            sb.Append(a).Append('-').Append(b).Append(':').Append(G(a).ToString("F2")).Append(',').Append(G(b).ToString("F2")).Append(',').Append(BoreHW(a)).Append('|');
            foreach (int c in BoreRow(a, b))
                foreach (int j in Planet.Cells[c].nb)
                    foreach (int x in new[] { c, j })
                    {
                        if (!boreNear.TryGetValue(x, out var l)) boreNear[x] = l = new List<int>(1);
                        if (!l.Contains(idx)) l.Add(idx);
                    }
        }
        string sig = sb.ToString(); if (sig == boreSig) return; boreSig = sig;
        foreach (int c in old) Touch(c); foreach (int c in boreNear.Keys) Touch(c);   // een tunnel kwam, ging of veranderde: het terrein erboven opnieuw
    }
    // de vakken op de rechte lijn van a naar b (a en b erbij)
    static List<int> BoreRow(int a, int b)
    {
        var o = new List<int> { a }; int cur = a;
        for (int s = 0; s < 400 && cur != b; s++)
        {
            Vector3 cd = Planet.Cells[cur].dir, to = Tan(Planet.Cells[b].dir - cd, cd); int best = -1; float bd = -2f;
            foreach (int j in Planet.Cells[cur].nb) { if (j == b) { best = b; break; } float d = Vector3.Dot(Tan(Planet.Cells[j].dir - cd, cd), to); if (d > bd) { bd = d; best = j; } }
            cur = best; o.Add(cur);
        }
        return o;
    }
    // een driehoekje van het maaiveld, zonder het deel dat in de mantel van een tunnel valt
    static void BoreTri(MeshBuilder m, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 up, List<int> near)
    {
        if (near == null) { m.Tri(p0, p1, p2, up); return; }
        BorePiece(m, new List<Vector3> { p0, p1, p2 }, up, near, 0);
    }
    static void BorePiece(MeshBuilder m, List<Vector3> poly, Vector3 up, List<int> near, int idx)
    {
        if (idx == near.Count) { for (int k = 1; k + 1 < poly.Count; k++) m.Tri(poly[0], poly[k], poly[k + 1], up); return; }
        var q = bores[near[idx]];
        // de mantel is de doorsnede van zeven halve ruimtes (g ≤ 0); wat buiten één ervan valt blijft staan, de rest gaat naar de volgende
        for (int f = 0; f < 7; f++)
        {
            var outP = ClipBore(poly, q, f, true); if (outP.Count >= 3) BorePiece(m, outP, up, near, idx + 1);
            poly = ClipBore(poly, q, f, false); if (poly.Count < 3) return;
        }
    }
    // In een tunnel (voor wie loopt, PlanetWalker): staat wereldpunt p in een koker onder het plafond, dan de hoogte van de vloer daar
    public static bool InBore(Vector3 p, out float floor)
    {
        floor = 0f; if (boreNear.Count == 0 || !boreNear.TryGetValue(Planet.Nearest(p), out var bl)) return false;
        float r = p.magnitude; Vector3 d = p / r; bool hit = false;
        foreach (int bi in bl)
        {
            var q = bores[bi]; Vector3 v = d * (Planet.R / Vector3.Dot(d, q.dA)) - q.dA * Planet.R; float s = Vector3.Dot(v, q.e), x = Vector3.Dot(v, q.n);
            float road = q.h0 - LevelH + q.k * s;   // de weg: een niveau onder het dek
            if (s < q.s0 - 1.5f || s > q.s1 + 1.5f || Mathf.Abs(x) > q.tw + 1f || r - Planet.R > road + LevelH - DeckT) continue;   // net voor het portaal telt mee (de voorrand)
            if (!hit || road > floor) floor = road; hit = true;
        }
        return hit;
    }
    static float BoreG(BoreC q, Vector3 p, int f)
    {
        float r = p.magnitude; Vector3 d = p / r, v = d * (Planet.R / Vector3.Dot(d, q.dA)) - q.dA * Planet.R; float s = Vector3.Dot(v, q.e), x = Vector3.Dot(v, q.n);
        float h = r - Planet.R - (q.h0 + q.k * s);   // ten opzichte van het dek
        switch (f)
        {
            case 0: return q.s0 - s;                              // voor het portaal
            case 1: return s - q.s1;                              // na het andere portaal
            case 2: return h - q.yO;                              // boven de mantel
            case 3: return x + (h - q.ym) * q.cs - q.wm;          // de schuine zijden boven halve hoogte
            case 4: return -x + (h - q.ym) * q.cs - q.wm;
            case 5: return x - (h - q.ym) * q.cs - q.wm;          // en eronder
            default: return -x - (h - q.ym) * q.cs - q.wm;
        }
    }
    // het deel van een veelhoek met g > 0 (outside) of g ≤ 0
    static List<Vector3> ClipBore(List<Vector3> poly, BoreC q, int f, bool outside)
    {
        var o = new List<Vector3>(poly.Count + 2); int n = poly.Count; var g = new float[n];
        for (int k = 0; k < n; k++) { g[k] = BoreG(q, poly[k], f); if (outside) g[k] = -g[k]; }
        for (int k = 0; k < n; k++)
        {
            int w = (k + 1) % n; if (g[k] <= 0f) o.Add(poly[k]);
            if ((g[k] <= 0f) != (g[w] <= 0f)) o.Add(Vector3.Lerp(poly[k], poly[w], g[k] / (g[k] - g[w])));
        }
        return o;
    }
}
