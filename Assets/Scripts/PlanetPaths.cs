using System.Collections.Generic;
using UnityEngine;

// Paden en fiets- en voetpaden als graaf, net als de wegen (PlanetRoads.cs): lijnen van knoop tot knoop door de vakmiddens met
// ronde hoeken, en op elke knoop één omtrek met afgeronde hoeken tussen de armen. Zo sluiten ze altijd aan zoals een weg;
// alleen het profiel verschilt:
//  - pad: zand, 2,2 m breed;
//  - fiets- en voetpad: zand 6,4 m breed met in het midden een rood fietsdeel van 3,2 m (2 cm hoger).
// Knopen: padvakken met geen, één of drie en meer buren, een knik van 75° of meer, of een overgang van fietspad naar pad (het
// fietspadvak versmalt). Een weg is een eindpunt: het pad houdt op bij de stoep, of bij de rand van het vak als het onder een
// brug door gaat (UnderMesh tekent het stuk onder de brug).
public static partial class PlanetCity
{
    const float PadY = 0.105f, FietsY = 0.125f;
    static float PadHW(byte st) => st == 1 ? 3.2f : 1.1f;      // halve breedte van het zand
    static float FietsHW(byte st) => st == 1 ? 1.6f : 0f;      // halve breedte van het rode fietsdeel
    static byte PStyle(int i) => Bike.Contains(i) ? (byte)1 : (byte)0;
    static List<int> PathNb(int i) { var l = new List<int>(4); foreach (int j in Partners(i)) if (IsWalk(j)) l.Add(j); return l; }
    static bool PathNodeShape(int i, List<int> nb)
    {
        if (nb.Count != 2 || TurnAt(i, nb[0], nb[1]) >= BendNode) return true;
        if (PStyle(i) == 0) return false;
        foreach (int j in nb) if (IsPath(j) && PStyle(j) == 0 && PathNb(j).Count == 2) return true;   // fietspad gaat over in een pad
        return false;
    }
    static bool IsPathNode(int i) => IsPath(i) && PathNodeShape(i, PathNb(i));
    // stijl van de arm van padknoop n naar buur j (zoals ArmStyle bij wegen)
    static byte PArmStyle(int n, int j)
    {
        if (!IsPath(j)) return PStyle(n);
        if (!IsPathNode(j)) return PStyle(j);
        bool dn = PathNb(n).Count == 2, dj = PathNb(j).Count == 2;
        return dn && !dj ? PStyle(n) : dj && !dn ? PStyle(j) : (byte)Mathf.Min(PStyle(n), PStyle(j));
    }
    // vanaf het hart van n tot hier tekent n (knoop of wegeind); daarna begint de lijn naar j
    static float PTrim(int n, int j)
    {
        if (IsRoad(n))   // tot onder de rand van de stoep (bij een schuine aansluiting verder: gedeeld door de sinus van de hoek met de weg)
        {
            if (UnderLink(n, j)) return CellDist(n, j) * 0.5f; Dims(StyleOf(n), out _, out float s);
            Vector3 rd = UpperRoadDir(n), c = Planet.Cells[n].dir; float sin = rd == Vector3.zero ? 1f : Vector3.Cross(rd.normalized, Tan(Planet.Cells[j].dir - c, c)).magnitude;
            return Mathf.Min((s - 0.3f) / Mathf.Max(0.35f, sin), CellDist(n, j) * 0.6f);
        }
        return PathNb(n).Count == 1 ? Mathf.Min(1f, CellDist(n, j) * 0.3f) : Mathf.Min(7.5f, CellDist(n, j) * 0.48f);
    }
    static float PathH(int i, int from) => IsRoad(i) ? LevelFor(i, from) : G(i);
    // Hoogte langs een pad: een padknoop is vlak tot de rand van de knoop, daarna helt het pad tot het volgende vakmidden (of tot de
    // rand van de volgende knoop). Dezelfde regel voor de tekening, het terrein (GroundField) en wie erover loopt (SurfaceH).
    static float PathPlateau(int i, int j) => IsPath(i) && IsPathNode(i) ? PTrim(i, j) : 0f;
    static float AxisH(int i, int j, float s, float hi, float hj)
    {
        float D = CellDist(i, j), Ti = PathPlateau(i, j), Tj = PathPlateau(j, i);
        return Mathf.Lerp(hi, hj, Mathf.Clamp01((s - Ti) / Mathf.Max(0.5f, D - Ti - Tj)));
    }
    // de hoogte van het pad in vak i op wereldpunt w: langs de arm waar het punt het meest in ligt
    static float PathSurfaceH(int i, Vector3 w)
    {
        Vector3 c = Planet.Cells[i].dir, v = w - c * Planet.R; v -= c * Vector3.Dot(v, c); if (v.sqrMagnitude < 1e-4f) return G(i);
        int best = -1; float bd = -2f; Vector3 bax = Vector3.zero;
        foreach (int j in PathNb(i)) { Vector3 ax = Tan(Planet.Cells[j].dir - c, c); float d = Vector3.Dot(ax, v.normalized); if (d > bd) { bd = d; best = j; bax = ax; } }
        return best < 0 ? G(i) : AxisH(i, best, Mathf.Max(0f, Vector3.Dot(v, bax)), G(i), LevelFor(best, i));
    }
    // een stuk van de hoogtefunctie langs een pad: met een vlak stuk op elke padknoop aan een eind
    static void AddWalkSeg(List<Seg> list, Seg q)
    {
        float Ta = PathPlateau(q.ia, q.ib), Tb = PathPlateau(q.ib, q.ia), D = Vector3.Distance(q.a, q.b);
        if ((Ta <= 0f && Tb <= 0f) || Ta + Tb > D - 0.5f) { list.Add(q); return; }
        Vector3 A = Vector3.Lerp(q.a, q.b, Ta / D), B = Vector3.Lerp(q.a, q.b, 1f - Tb / D);
        if (Ta > 0f) list.Add(new Seg { a = q.a, b = A, ha = q.ha, hb = q.ha, hw = q.hw, capA = q.capA, ia = q.ia, ib = q.ib });
        list.Add(new Seg { a = A, b = B, ha = q.ha, hb = q.hb, hw = q.hw, capA = Ta <= 0f && q.capA, capB = Tb <= 0f && q.capB, ia = q.ia, ib = q.ib });
        if (Tb > 0f) list.Add(new Seg { a = B, b = q.b, ha = q.hb, hb = q.hb, hw = q.hw, capB = q.capB, ia = q.ia, ib = q.ib });
    }

    static void PathGraph(out HashSet<int> nodes, out List<List<int>> chains, out List<bool> closed)
    {
        var nbs = new Dictionary<int, List<int>>(); nodes = new HashSet<int>(); chains = new List<List<int>>(); closed = new List<bool>();
        foreach (var kv in Cells) if (kv.Value.Count == 1 && kv.Value[0] == "pad") nbs[kv.Key] = PathNb(kv.Key);
        var starts = new List<int>();
        foreach (var kv in nbs)
        {
            if (PathNodeShape(kv.Key, kv.Value)) { nodes.Add(kv.Key); starts.Add(kv.Key); }
            foreach (int j in kv.Value) if (IsRoad(j) && !starts.Contains(j)) starts.Add(j);   // wegen zijn eindpunten
        }
        var done = new HashSet<long>();
        foreach (int s in starts)
        {
            var first = IsRoad(s) ? new List<int>() : nbs[s];
            if (IsRoad(s)) foreach (int j in Partners(s)) if (IsPath(j)) first.Add(j);
            foreach (int p in first)
            {
                if (done.Contains(DKey(s, p))) continue;
                var ch = new List<int> { s }; int prev = s, cur = p;
                while (true)
                {
                    ch.Add(cur); done.Add(DKey(prev, cur)); done.Add(DKey(cur, prev));
                    if (IsRoad(cur) || nodes.Contains(cur) || !nbs.ContainsKey(cur) || ch.Count > 4000) break;
                    var l = nbs[cur]; int nx = l[0] == prev ? l[1] : l[0]; prev = cur; cur = nx;
                }
                if (IsRoad(s) && IsRoad(ch[ch.Count - 1]) && ch.Count == 2) continue;
                chains.Add(ch); closed.Add(false);
            }
        }
        foreach (var kv in nbs)   // ringen zonder knoop
        {
            int s = kv.Key; if (nodes.Contains(s) || kv.Value.Count != 2 || done.Contains(DKey(s, kv.Value[0]))) continue;
            var ch = new List<int> { s }; int prev = s, cur = kv.Value[0]; done.Add(DKey(s, cur)); done.Add(DKey(cur, s));
            while (cur != s && ch.Count < 4000)
            {
                ch.Add(cur); var l = nbs[cur]; int nx = l[0] == prev ? l[1] : l[0]; done.Add(DKey(cur, nx)); done.Add(DKey(nx, cur)); prev = cur; cur = nx;
            }
            chains.Add(ch); closed.Add(true);
        }
    }

    // de padstukken in het wegennet (aangeroepen door RebuildRoads, met dezelfde vingerafdrukken)
    static void RebuildPaths(HashSet<string> want)
    {
        PathGraph(out var nodes, out var chains, out var closed);
        for (int c = 0; c < chains.Count; c++)
        {
            var ch = chains[c]; string key = (closed[c] ? "pr:" : "pc:") + string.Join(",", ch); want.Add(key);
            long sig = 1469598103934665603L;
            for (int k = 0; k < ch.Count; k++)
            {
                int i = ch[k]; sig = Mix(sig, i); sig = Mix(sig, Cm(PathH(i, ch[k > 0 ? k - 1 : Mathf.Min(1, ch.Count - 1)]))); sig = Mix(sig, PStyle(i)); sig = Mix(sig, IsRoad(i) ? 7 + StyleOf(i) : 0);
                sig = Mix(sig, Cm(MinCorner(i))); sig = Mix(sig, PathNb(i).Count); sig = Mix(sig, Underpass(i) ? 1 : 0);
            }
            if (ch.Count == 2) sig = Mix(sig, IsPathNode(ch[0]) ? 1 : 0) ^ (IsPathNode(ch[1]) ? 2 : 0);
            var cc = ch; bool cl = closed[c]; Piece(key, sig, mb => PathChainMesh(mb, cc, cl, nodes));
        }
        foreach (int n in nodes)
        {
            string key = "pn:" + n; want.Add(key);
            long sig = Mix(Mix(Mix(1469598103934665603L, n), Cm(G(n))), PStyle(n)); sig = Mix(sig, Cm(MinCorner(n)));
            var arms = PathNb(n);
            foreach (int j in arms) { sig = Mix(sig, j); sig = Mix(sig, Cm(LevelFor(j, n))); sig = Mix(sig, PArmStyle(n, j)); sig = Mix(sig, IsRoad(j) ? 1 : 2); }
            Piece(key, sig, mb => PathNodeMesh(mb, n, arms));
        }
    }

    // ---------- een padlijn ----------
    static void PathChainMesh(Dictionary<string, MeshBuilder> mb, List<int> ch, bool closed, HashSet<int> nodes)
    {
        System.Func<string, MeshBuilder> M = k => { if (!mb.TryGetValue(k, out var x)) { x = new MeshBuilder(); mb[k] = x; } return x; };
        byte st = closed || (ch.Count > 2 && IsPath(ch[1])) ? PStyle(ch[closed ? 0 : 1]) : IsRoad(ch[0]) ? PStyle(ch[1]) : IsRoad(ch[ch.Count - 1]) ? PStyle(ch[0]) : PArmStyle(ch[0], ch[1]);
        float w = PadHW(st), rw = FietsHW(st);
        var hs = new float[ch.Count]; for (int k = 0; k < ch.Count; k++) hs[k] = PathH(ch[k], ch[k > 0 ? k - 1 : Mathf.Min(1, ch.Count - 1)]);
        var line = SmoothLine(ch, closed, hs);
        bool e0 = !closed && (nodes.Contains(ch[0]) || IsRoad(ch[0])), e1 = !closed && (nodes.Contains(ch[ch.Count - 1]) || IsRoad(ch[ch.Count - 1]));
        float t0 = e0 ? PTrim(ch[0], ch[1]) : 0f, t1 = e1 ? PTrim(ch[ch.Count - 1], ch[ch.Count - 2]) : 0f;
        var pts = TrimLine(line, t0, t1); int N = pts.Count; if (N < 2) return;
        for (int k = 0; k < N; k++) if (IsPath(pts[k].cell)) { var q = pts[k]; q.h = PathSurfaceH(q.cell, q.p); pts[k] = q; }   // vlak op een knoop, dan de helling
        var up = new Vector3[N]; var side = new Vector3[N]; var low = new float[N];
        for (int k = 0; k < N; k++)
        {
            up[k] = pts[k].p.normalized; Vector3 t = pts[Mathf.Min(N - 1, k + 1)].p - pts[Mathf.Max(0, k - 1)].p; t -= up[k] * Vector3.Dot(t, up[k]);
            side[k] = Vector3.Cross(up[k], t.normalized).normalized;
            low[k] = IsRoad(pts[k].cell) ? SKB : SkirtDepth(pts[k].cell);
        }
        System.Func<int, float, float, Vector3> W = (k, off, y) => (pts[k].p + side[k] * off).normalized * (Planet.R + pts[k].h + y);
        for (int k = 0; k + 1 < N; k++)
        {
            int m = k + 1; Vector3 u = up[k];
            M("pad").Quad(W(k, -w, PadY), W(k, w, PadY), W(m, w, PadY), W(m, -w, PadY), u);
            if (rw > 0f) M("fietspad").Quad(W(k, -rw, FietsY), W(k, rw, FietsY), W(m, rw, FietsY), W(m, -rw, FietsY), u);
            for (int sg = -1; sg <= 1; sg += 2) M("pad").Quad(W(k, sg * w, low[k]), W(m, sg * w, low[m]), W(m, sg * w, PadY), W(k, sg * w, PadY), side[k] * sg);
        }
    }

    // ---------- een padknoop ----------
    static void PathNodeMesh(Dictionary<string, MeshBuilder> mb, int n, List<int> arms)
    {
        System.Func<string, MeshBuilder> M = k => { if (!mb.TryGetValue(k, out var x)) { x = new MeshBuilder(); mb[k] = x; } return x; };
        var fr = new Frame(n, true).Flat(); float sk = SkirtDepth(n); int na = arms.Count;
        if (na == 0)   // los padvak: een rondje
        {
            byte s0 = PStyle(n); FanFill(M("pad"), fr, Circle(Vector2.zero, PadHW(s0), 0f, Mathf.PI * 2f, 24), PadY, sk, null);
            if (FietsHW(s0) > 0f) FanFill(M("fietspad"), fr, Circle(Vector2.zero, FietsHW(s0), 0f, Mathf.PI * 2f, 24), FietsY, 0f, null);
            return;
        }
        var ends = new List<Vector2>(); var dirs = new List<Vector2>(); var w = new float[na]; var rw = new float[na];
        for (int a = 0; a < na; a++)
        {
            int j = arms[a]; Vector3 d3 = (Planet.Cells[j].dir - Planet.Cells[n].dir) * Planet.R; Vector2 q = new Vector2(Vector3.Dot(d3, fr.e), Vector3.Dot(d3, fr.n));
            ends.Add(q.normalized * PTrim(n, j)); dirs.Add(-q.normalized); byte st = PArmStyle(n, j); w[a] = PadHW(st); rw[a] = FietsHW(st);
        }
        var order = new List<int>(); for (int a = 0; a < na; a++) order.Add(a);
        System.Func<int, float> angOf = a => Mathf.Atan2(-dirs[a].y, -dirs[a].x);
        order.Sort((x, y) => angOf(x).CompareTo(angOf(y)));
        // de knoop is vlak (de helling begint pas in de lijn, zie AxisH)
        var mouths = new HashSet<int>();
        FanFill(M("pad"), fr, PathOutline(order, ends, dirs, w, angOf, mouths), PadY, sk, mouths);
        var red = PathOutline(order, ends, dirs, rw, angOf, null); if (red.Count > 2) FanFill(M("fietspad"), fr, red, FietsY, 0f, null);
    }
    // De omtrek van een padknoop (tegen de klok in): per arm de mond (de twee randen op de lijngrens), dan naar de volgende arm:
    // een afgeronde hoek waar de randen elkaar snijden, recht door bij armen in elkaars verlengde (ook bij een andere breedte:
    // dan loopt de rand schuin over de knoop), of een boog om het hart aan de buitenkant van een knik en bij een eind (kop).
    // Armen met breedte 0 doen niet mee (het rode deel van een pad zonder fietsdeel). mouths: de index van elke mond.
    static List<Vector2> PathOutline(List<int> order, List<Vector2> ends, List<Vector2> dirs, float[] wid, System.Func<int, float> angOf, HashSet<int> mouths)
    {
        var sel = new List<int>(); foreach (int a in order) if (wid[a] > 0f) sel.Add(a);
        var rim = new List<Vector2>(); int m = sel.Count; if (m == 0) return rim;
        System.Func<int, float, float, Vector2> P = (a, lat, s) =>
        {
            Vector2 e = ends[a], d = dirs[a], r = new Vector2(d.y, -d.x), en = e.normalized, p0 = e + r * lat; float den = Vector2.Dot(d, en);
            return p0 + d * ((Mathf.Abs(den) < 1e-4f ? 0f : Vector2.Dot(e - p0, en) / den) + s);
        };
        System.Func<int, float> sc = a => Vector2.Dot(-P(a, 0f, 0f), dirs[a]);   // van de mond tot naast het hart
        for (int c = 0; c < m; c++)
        {
            int a = sel[c], b = sel[(c + 1) % m];
            mouths?.Add(rim.Count); rim.Add(P(a, -wid[a], 0f)); rim.Add(P(a, wid[a], 0f));
            float phi = m == 1 ? Mathf.PI * 2f : Mathf.Repeat(angOf(b) - angOf(a), Mathf.PI * 2f);
            if (m == 1 || phi > Mathf.PI * 190f / 180f)
            {
                Vector2 p = P(a, wid[a], sc(a)), q = P(b, -wid[b], sc(b));
                float a0 = Mathf.Atan2(p.y, p.x), a1 = Mathf.Atan2(q.y, q.x); if (a1 <= a0 + 0.01f) a1 += Mathf.PI * 2f;
                int ns = Mathf.Max(4, Mathf.CeilToInt((a1 - a0) / 0.2f));
                for (int k = 0; k <= ns; k++) { float f = k / (float)ns, an = Mathf.Lerp(a0, a1, f); rim.Add(new Vector2(Mathf.Cos(an), Mathf.Sin(an)) * Mathf.Lerp(wid[a], wid[b], f)); }
            }
            else if (phi < Mathf.PI * 170f / 180f)
            {
                Vector2 pa = P(a, wid[a], 0f), pb = P(b, -wid[b], 0f); float cr = Cross(dirs[a], dirs[b]);
                float t = Cross(pb - pa, dirs[b]) / cr, u = Cross(pb - pa, dirs[a]) / cr;
                if (t > 0.15f && u > 0.15f)
                {
                    float rf = Mathf.Min(1.5f, Mathf.Min(t, u) - 0.1f); Vector2 X = pa + dirs[a] * t, P0 = P(a, wid[a], t - rf), Q0 = P(b, -wid[b], u - rf);
                    for (int k = 0; k <= 6; k++) { float f = k / 6f, g = 1f - f; rim.Add(P0 * (g * g) + X * (2f * g * f) + Q0 * (f * f)); }
                }
            }
        }
        return rim;
    }
    // een vlak van het hart naar de omtrek, in stukjes (op een helling kleiner: de hoogte is daar gebogen), met een zijkant tot
    // onder het maaiveld langs de omtrek behalve in de monden
    static void FanFill(MeshBuilder m, Frame fr, IList<Vector2> rim, float y, float skirt, HashSet<int> mouths, System.Func<Vector2, float> hf = null)
    {
        float step = hf != null ? 0.8f : 2f; int n = rim.Count;
        System.Func<Vector2, float, Vector3> Q = (v, yy) => fr.P(v.x, yy + (hf != null ? hf(v) : 0f), v.y);
        for (int k = 0; k < n; k++)
        {
            Vector2 p = rim[k], q = rim[(k + 1) % n]; if ((q - p).sqrMagnitude < 1e-6f) continue;
            int ne = Mathf.Max(1, Mathf.CeilToInt((q - p).magnitude / step)), nr = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(p.magnitude, q.magnitude) / step));
            for (int e = 0; e < ne; e++)
            {
                Vector2 a = Vector2.Lerp(p, q, e / (float)ne), b = Vector2.Lerp(p, q, (e + 1) / (float)ne);
                m.Tri(Q(Vector2.zero, y), Q(a / nr, y), Q(b / nr, y), fr.u);
                for (int r = 1; r < nr; r++) { float f0 = r / (float)nr, f1 = (r + 1) / (float)nr; m.Quad(Q(a * f0, y), Q(a * f1, y), Q(b * f1, y), Q(b * f0, y), fr.u); }
                if (mouths != null && !mouths.Contains(k)) { Vector2 o = (a + b) * 0.5f; m.Quad(Q(a, skirt), Q(b, skirt), Q(b, y), Q(a, y), fr.D(o.x, 0f, o.y)); }
            }
        }
    }
}
