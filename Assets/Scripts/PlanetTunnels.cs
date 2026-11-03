using System.Collections.Generic;
using UnityEngine;

// Tunnels door een berg of heuvel. Ligt het terrein op een recht stuk van een gesleepte weg of pad ergens minstens 9 m boven de
// rechte lijn tussen de voet aan beide kanten (met Ongelijkvloers, U: 6 m, net genoeg voor de koker), dan gaat dat stuk erdoorheen
// in plaats van eroverheen. In de graaf is dat een tunnelverbinding (TunnelLinks, zoals een weg onder een brug door) van het laatste
// wegvak vóór de berg naar het eerste erna; de vakken ertussen zijn weer gewoon terrein met hun eigen hoogte (er kan op gebouwd
// worden). Getekend met de onderdelen van de onderdoorgang (overpass_path): de zeshoekige koker, een portaal aan elke kant en het
// betonblok, waarvan de bovenkant het maaiveld volgt; waar de berg nog laag is steekt het blok erboven uit. Het terrein binnen het
// blok wordt weggesneden (BoreTri), anders steekt het door de koker. De weg in de tunnel helt recht van voet tot voet; het verkeer
// rijdt er vanzelf door (SurfaceH helt van hart tot hart naar de partner).
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
    public static List<(int a, int b, float ha, float hb, Vector4 ends)> BorePlan(List<int> line, string type)
    {
        var o = new List<(int, int, float, float, Vector4)>(); if (line == null || line.Count < 4 || DragLevel != 0) return o;
        bool Plain(int c) => !Underpass(c) && !IsRot(c) && !preCarve.ContainsKey(c) && (IsWalk(c) || CanRoad(c, type));
        float Road(int c)   // een nieuw vak: het gemiddelde van zijn buren die geen weg of pad zijn en niet op de lijn liggen (PadGround na het leggen)
        {
            if (IsWalk(c)) return G(c); float s = 0f; int n = 0;
            foreach (int j in Planet.Cells[c].nb) if (!IsWalk(j) && !Underpass(j) && !line.Contains(j)) { s += G(j); n++; }
            return n > 0 ? s / n : G(c);
        }
        foreach (var (i, j) in BoreSearch(line, Plain, k => IsNeighbour(line[k], line[k + 1]), k => !IsWalk(line[k]) && Plain(line[k]) && MidCell(line[k - 1], line[k + 1]) == line[k], Road, G))
        {
            int a = line[i], b = line[j]; float ha = Road(a), hb = Road(b); bool road = type == "weg";
            if (a > b) { (a, b) = (b, a); (ha, hb) = (hb, ha); }   // zoals de tunnel straks getekend wordt: vanaf het kleinste vak
            o.Add((a, b, ha, hb, ScanEnds(a, b, ha, hb, road ? Mathf.Max(RoadHW, FlatHW(a)) : PathHW)));
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
        var f = BoreFrame0(a, b, G(a), G(b), out p0, out p1, out k);
        if (boreEnds.TryGetValue(Key(a, b), out var se)) { p0.x = se.y; p1.x = se.z; }   // de portalen dieper in de berg (BoreEnds)
        return f;
    }
    // idem met de weghoogte bij de portaalvakken gegeven (het voorbeeld: de weg ligt er nog niet), de portalen op de rand van het vak
    static Frame BoreFrame0(int a, int b, float ga, float gb, out Vector2 p0, out Vector2 p1, out float k)
    {
        Vector3 dA = Planet.Cells[a].dir, dB = Planet.Cells[b].dir;
        var f = new Frame(a, true).Flat(); f.e = Tan(dB - dA, dA); f.n = Vector3.Cross(f.u, f.e).normalized;
        float X(Vector3 d) => Vector3.Dot(d * (Planet.R / Vector3.Dot(d, dA)) - dA * Planet.R, f.e);
        float L = X(dB); k = (gb - ga) / L;
        p0 = new Vector2(FlatL, 0f); p1 = new Vector2(X((dB * Planet.R + Tan(dA - dB, dB) * FlatL).normalized), 0f);
        return f.Lift(LevelH + (ga - G(a))).Incline(k);   // het dek een niveau boven de weg (ook als die nog niet op G(a) ligt)
    }
    // De portalen zo diep in de berg dat het betonblok erin verdwijnt: vanaf een half vak van het portaalvak naar binnen tot het
    // maaiveld boven de koker en aan beide kanten van het blok boven het dek ligt (hooguit tot vlak voor het midden). Daarvoor komt de
    // weg in een inrit tussen twee betonnen keermuren (Trough), vanaf waar de berg boven de weg uitkomt; het terrein daartussen
    // snijdt BoreTri weg, daarbuiten blijft het zoals het was. Aangeroepen vanuit CollectCuts.
    static readonly Dictionary<long, Vector4> boreEnds = new Dictionary<long, Vector4>();   // per tunnel: begin inrit a, portaal a, portaal b, eind inrit b (langs e)
    const float WallT = 0.6f;   // dikte van een keermuur
    static float TroughW(int a) => BoreHW(a) + 1.5f;   // halve breedte van de inrit tussen de keermuren
    static void BoreEnds()
    {
        boreEnds.Clear();
        foreach (long key in TunnelLinks)
        {
            int a = (int)(key >> 20), b = (int)(key & 0xFFFFF); if (MidCell(a, b) >= 0 || !IsWalk(a) || !IsWalk(b)) continue;
            boreEnds[key] = ScanEnds(a, b, G(a), G(b), BoreHW(a));
        }
    }
    // begin inrit a, portaal a, portaal b, eind inrit b (langs e vanaf het hart van a), voor een weg met hoogte ga en gb in de portaalvakken
    static Vector4 ScanEnds(int a, int b, float ga, float gb, float bhw)
    {
        var f = BoreFrame0(a, b, ga, gb, out var p0, out var p1, out float k); Vector3 dA = Planet.Cells[a].dir;
        float wb = bhw + 3.75f, L = p1.x + FlatL, ww = bhw + 1.5f + WallT;
        float T(float s, float x) => TerrainAt((dA * Planet.R + f.e * s + f.n * x).normalized);
        bool Covered(float s) { float deck = ga + k * s + LevelH; return T(s, 0f) >= deck + 1.1f && T(s, -wb) >= deck && T(s, wb) >= deck; }
        bool Above(float s) { float road = ga + k * s; return T(s, 0f) > road + 0.3f || T(s, -ww) > road + 0.3f || T(s, ww) > road + 0.3f; }   // de berg komt hier boven de weg uit
        float s0 = p0.x, s1 = p1.x;
        while (s0 < L * 0.5f - 4f && !Covered(s0)) s0 += 1f;
        while (s1 > s0 + 8f && !Covered(s1)) s1 -= 1f;
        float sa = s0, sb = s1;   // de inritten: terug tot waar de berg niet meer boven de weg uitkomt (hooguit tot het hart van het portaalvak)
        while (sa > 0f && Above(sa - 1f)) sa -= 1f;
        while (sb < L && Above(sb + 1f)) sb += 1f;
        return new Vector4(Mathf.Max(0f, sa - 1f), s0, s1, Mathf.Min(L, sb + 1f));
    }
    // de koker, de portalen en het betonblok van elke tunnel die in a begint (de kleinste van de twee tekent)
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
            BridgeBlock(M, f, p0, p1, fl, top, 0f);
            if (boreEnds.TryGetValue(Key(a, b), out var se)) { Trough(M, f, a, se.x, p0.x); Trough(M, f, a, p1.x, se.w); if (se.x < p0.x - 0.5f) Headwall(M, f, a, p0.x - 1.44f, -1f); if (se.w > p1.x + 0.5f) Headwall(M, f, a, p1.x + 1.44f, 1f); }   // de inritten, met een frontmuur boven elk portaal
            tubeW = PathHW;
        }
    }

    // Een inrit van s0 tot s1 (langs de tunnel): een betonnen vloer naast de weg en aan beide kanten een keermuur, van onder het
    // maaiveld tot net boven de berg erlangs (minstens 0,6 m boven de weg), om de 1,5 m bemonsterd. In het vlak van de tunnel: y = 0
    // is het dek, de weg ligt op -LevelH.
    static void Trough(System.Func<string, MeshBuilder> M, Frame f, int a, float s0, float s1)
    {
        if (s1 - s0 < 0.5f) return;
        float wi = TroughW(a), wo = wi + WallT, road = -LevelH, bot = road - 3f; int n = Mathf.Max(1, Mathf.CeilToInt((s1 - s0) / 1.5f));
        var tops = new float[n + 1];
        for (int sd = 0; sd < 2; sd++)
        {
            float sg = sd == 0 ? -1f : 1f;
            for (int q = 0; q <= n; q++)
            {
                float s = Mathf.Lerp(s0, s1, q / (float)n), deckH = f.P(s, 0f, 0f).magnitude - Planet.R;
                tops[q] = Mathf.Max(TerrainAt(f.P(s, 0f, sg * wo).normalized) + 0.15f - deckH, road + 0.6f);
            }
            for (int q = 0; q < n; q++)
            {
                float sa = Mathf.Lerp(s0, s1, q / (float)n), sb = Mathf.Lerp(s0, s1, (q + 1) / (float)n), ta = tops[q], tb = tops[q + 1];
                Vector3 inA0 = f.P(sa, road - 0.05f, sg * wi), inB0 = f.P(sb, road - 0.05f, sg * wi), inA1 = f.P(sa, ta, sg * wi), inB1 = f.P(sb, tb, sg * wi);
                Vector3 ouA1 = f.P(sa, ta, sg * wo), ouB1 = f.P(sb, tb, sg * wo), ouA0 = f.P(sa, bot, sg * wo), ouB0 = f.P(sb, bot, sg * wo);
                M("steen").Quad(inA0, inB0, inB1, inA1, f.D(0f, 0f, -sg));   // de binnenkant
                M("steen").Quad(inA1, inB1, ouB1, ouA1, f.u);                // de bovenkant
                M("steen").Quad(ouA0, ouB0, ouB1, ouA1, f.D(0f, 0f, sg));    // de buitenkant (meest onder het maaiveld)
                if (sd == 0) M("steen").Quad(f.P(sa, road - 0.03f, -wi), f.P(sb, road - 0.03f, -wi), f.P(sb, road - 0.03f, wi), f.P(sa, road - 0.03f, wi), f.u);   // de vloer
            }
            foreach (var (s, q, nrm) in new[] { (s0, 0, -1f), (s1, n, 1f) })   // de kopse kanten
                M("steen").Quad(f.P(s, road - 0.05f, sg * wi), f.P(s, tops[q], sg * wi), f.P(s, tops[q], sg * wo), f.P(s, bot, sg * wo), f.D(nrm, 0f, 0f));
        }
        foreach (var (s, nrm) in new[] { (s0, -1f), (s1, 1f) })   // de voorkant onder de vloer: waar het terrein lager ligt dan de weg zie je anders onder de inrit door
            M("steen").Quad(f.P(s, bot, -wo), f.P(s, bot, wo), f.P(s, road - 0.03f, wo), f.P(s, road - 0.03f, -wo), f.D(nrm, 0f, 0f));
    }

    // De frontmuur boven een portaal in een inrit: op de voorrand van het portaal (s), van net onder de bovenkant van het portaal tot
    // net boven de berg, zo breed als de inrit met muren. Anders kijk je onder de rand van het terrein achter het portaal door.
    static void Headwall(System.Func<string, MeshBuilder> M, Frame f, int a, float s, float sg)
    {
        float wo = TroughW(a) + WallT, y0 = -DeckT + 2.17f - 0.3f; int n = Mathf.Max(2, Mathf.CeilToInt(2f * wo / 1.5f));
        float deckH = f.P(s, 0f, 0f).magnitude - Planet.R;
        float Top(float x) => Mathf.Max(TerrainAt(f.P(s, 0f, x).normalized) + 0.15f - deckH, y0 + 0.3f);
        for (int q = 0; q < n; q++)
        {
            float xa = Mathf.Lerp(-wo, wo, q / (float)n), xb = Mathf.Lerp(-wo, wo, (q + 1) / (float)n), ta = Top(xa), tb = Top(xb);
            Vector3 a0 = f.P(s, y0, xa), b0 = f.P(s, y0, xb), a1 = f.P(s, ta, xa), b1 = f.P(s, tb, xb), a2 = f.P(s - sg * WallT, ta, xa), b2 = f.P(s - sg * WallT, tb, xb);
            M("steen").Quad(a0, b0, b1, a1, f.D(sg, 0f, 0f));   // de voorkant
            M("steen").Quad(a1, b1, b2, a2, f.u);                // de bovenrand
        }
    }

    // ---------- het terrein in het betonblok weg ----------
    // Het terrein is een hoogteveld: het kan niet tegelijk boven en onder de koker liggen. Waar de berg lager is dan de bovenkant van
    // het blok (vooral bij de portalen) zou het door de koker steken; daar wordt het weggesneden en is de bovenkant van het blok het
    // maaiveld. Hoger blijft het terrein zoals het is (het blok ligt er net onder).
    struct BoreC { public int a; public Vector3 dA, e, n; public float s0, s1, hw, h0, k, tw; public bool open; }   // langs e van s0 tot s1, opzij tot hw, onder h0 + k·s; tw: halve breedte van de koker; open: een inrit (alles weg, ook hoger)
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
            // de bovenkant van het blok, zoals BridgeBlock: de grote zeshoek rond de koker, op dekhoogte
            float fl = -LevelH, top = -DeckT, yB = fl - 2.17f, yO = top + 2.17f, co = (yO - yB) * 0.5f * 0.577f, ym = (yB + yO) * 0.5f;
            float wt = BoreHW(a) + 3.75f + co - co * (0f - ym) / (yO - ym);
            int idx = bores.Count;
            bores.Add(new BoreC { a = a, dA = dA, e = e, n = Vector3.Cross(dA, e).normalized, s0 = p0.x - 1.5f, s1 = p1.x + 1.5f, hw = wt - 0.3f, h0 = G(a) + LevelH - 0.04f, k = k, tw = BoreHW(a) });
            if (boreEnds.TryGetValue(key, out var se))   // de inritten tussen de keermuren: daar al het terrein weg
            {
                foreach (var (r0, r1) in new[] { (se.x, p0.x - 1.5f), (p1.x + 1.5f, se.w) })   // tot de voorrand van het portaal: daarboven blijft de berg staan
                    if (r1 - r0 > 0.5f) bores.Add(new BoreC { a = a, dA = dA, e = e, n = Vector3.Cross(dA, e).normalized, s0 = r0, s1 = r1, hw = TroughW(a) + WallT * 0.5f, h0 = G(a) + LevelH - 0.04f, k = k, tw = TroughW(a), open = true });
                sb.Append(se.x.ToString("F1")).Append(',').Append(se.w.ToString("F1")).Append(',');
            }
            int last = bores.Count - 1;
            sb.Append(a).Append('-').Append(b).Append(':').Append(G(a).ToString("F2")).Append(',').Append(G(b).ToString("F2")).Append(',').Append(BoreHW(a)).Append('|');
            foreach (int c in BoreRow(a, b))
                foreach (int j in Planet.Cells[c].nb)
                    foreach (int x in new[] { c, j })
                    {
                        if (!boreNear.TryGetValue(x, out var l)) boreNear[x] = l = new List<int>(1);
                        for (int bi = idx; bi <= last; bi++) if (!l.Contains(bi)) l.Add(bi);
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
    // een driehoekje van het maaiveld, zonder het deel dat in het betonblok van een tunnel valt
    static void BoreTri(MeshBuilder m, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 up, List<int> near)
    {
        if (near == null) { m.Tri(p0, p1, p2, up); return; }
        BorePiece(m, new List<Vector3> { p0, p1, p2 }, up, near, 0);
    }
    static void BorePiece(MeshBuilder m, List<Vector3> poly, Vector3 up, List<int> near, int idx)
    {
        if (idx == near.Count) { for (int k = 1; k + 1 < poly.Count; k++) m.Tri(poly[0], poly[k], poly[k + 1], up); return; }
        var q = bores[near[idx]];
        // het blok is de doorsnede van vijf halve ruimtes (g ≤ 0); wat buiten één ervan valt blijft staan, de rest gaat naar de volgende
        for (int f = 0; f < 5; f++)
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
            float road = q.h0 + 0.04f - LevelH + q.k * s;   // de weg: een niveau onder het dek
            if (s < q.s0 - 1.5f || s > q.s1 + 1.5f || Mathf.Abs(x) > q.tw + 1f || (!q.open && r - Planet.R > road + LevelH - DeckT)) continue;   // net voor het portaal telt mee (de voorrand); een inrit heeft geen plafond
            if (!hit || road > floor) floor = road; hit = true;
        }
        return hit;
    }
    static float BoreG(BoreC q, Vector3 p, int f)
    {
        float r = p.magnitude; Vector3 d = p / r, v = d * (Planet.R / Vector3.Dot(d, q.dA)) - q.dA * Planet.R; float s = Vector3.Dot(v, q.e);
        switch (f)
        {
            case 0: return q.s0 - s;
            case 1: return s - q.s1;
            case 2: return Vector3.Dot(v, q.n) - q.hw;
            case 3: return -Vector3.Dot(v, q.n) - q.hw;
            default: return q.open ? -1f : r - Planet.R - (q.h0 + q.k * s);
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
