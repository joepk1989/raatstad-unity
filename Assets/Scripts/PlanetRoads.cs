using System.Collections.Generic;
using UnityEngine;

// Het wegennet als graaf, zoals in de webversie (07-driehoeken-vectorwegen.js). De opslag blijft per vak (een wegvak met
// zijn verbindingen), maar getekend wordt er niet meer per vak:
//  - knopen: wegvakken met geen, één of drie en meer wegburen, en vakken waar de weg een knik van 75° of meer maakt;
//  - lijnen: van knoop tot knoop door de vakmiddens (een ring zonder knopen is een gesloten lus). De hoeken in een lijn worden
//    rond gemaakt zoals smoothPts: een kwadratische boog tot 8 m voor en na de hoek, nooit verder dan 45% van een stuk;
//  - langs een lijn doorlopende linten: asfalt, schuine stoeprand, stoep en een zijkant tot onder het maaiveld, met markering,
//    lantaarns, eenrichtingspijlen, brugleuningen en de middenberm van een laan;
//  - op een knoop de kruisingsvorm (RoadNode): stoephoeken tussen de armen, de ronde buitenkant van een knik of een ronde kop.
//    De linten houden op 7,5 m van het knooppunt op (NodeTrim); daar begint de knoop, zodat alles naadloos aansluit.
// Elk stuk (lijn of knoop) is een eigen object met een vingerafdruk van alles waar het van afhangt: na een wijziging worden
// alleen de stukken opnieuw gemaakt waarvan die veranderd is.
public static partial class PlanetCity
{
    static Transform roadRoot;
    class RoadPiece { public long sig; public GameObject go; }
    static readonly Dictionary<string, RoadPiece> roadPieces = new Dictionary<string, RoadPiece>();
    const float BendNode = 75f;
    static readonly List<Vector3> crossAt = new List<Vector3>();   // de brugpunten van schuine wegen over een pad (geen diepe stoepzijkant daar)   // vanaf deze draaihoek is een wegvak een knik (knoop) in plaats van een ronde bocht in de lijn

    static void RoadsInit(Transform parent) { roadRoot = new GameObject("Wegen").transform; roadRoot.SetParent(parent, false); roadPieces.Clear(); }

    static List<int> RoadNb(int i) { var l = new List<int>(4); foreach (int j in Partners(i)) if (IsRoad(j)) l.Add(j); return l; }
    // draaihoek in vak i tussen a → i → b (0 = rechtdoor), in graden
    static float TurnAt(int i, int a, int b)
    {
        Vector3 c = Planet.Cells[i].dir, va = Planet.Cells[a].dir - c, vb = Planet.Cells[b].dir - c;
        va -= c * Vector3.Dot(va, c); vb -= c * Vector3.Dot(vb, c);
        return 180f - Vector3.Angle(va, vb);
    }
    static float CellDist(int a, int b) => Vector3.Distance(Planet.Cells[a].dir, Planet.Cells[b].dir) * Planet.R;
    // tot hier (vanaf het knooppunt) tekent de knoop; daarna begint het lint naar buur j
    // Een schuine weg die een rechte weg kruist op de rand tussen twee vakken (vak i en zijn buur h): het kruispunt ligt op die
    // rand, zodat beide wegen recht blijven. Vak i is dan verbonden met h en met de twee vakken aan weerszijden van de rand i|h,
    // die zelf niet met h verbonden zijn. Bijgewerkt in RebuildRoads.
    static readonly Dictionary<int, int> edgeMate = new Dictionary<int, int>();
    static int EdgeMateOf(int i)
    {
        if (Rot.Contains(i)) return -1;
        foreach (int h in Partners(i))
        {
            if (!IsRoad(h) || !IsNeighbour(i, h) || UnderLink(i, h)) continue;
            int cnt = 0; foreach (int p in Partners(i)) if (p != h && IsRoad(p) && IsNeighbour(p, h) && !Linked(p, h) && !UnderLink(i, p)) cnt++;
            if (cnt == 2) return h;
        }
        return -1;
    }
    // het hart van een wegknoop op de bol (eenheidsvector): het vakmidden, of het midden van de rand bij zo'n schuine kruising
    public static Vector3 NodeDir(int i) => edgeMate.TryGetValue(i, out int h) ? (Planet.Cells[i].dir + Planet.Cells[h].dir).normalized : Planet.Cells[i].dir;
    static float NodeTrim(int n, int j, int deg) => IsRot(n) ? RotTrim(n, j) : Mathf.Max(Mathf.Min(deg == 1 ? 7.0f : 7.5f, CellDist(n, j) * 0.48f), AcuteTrim(n, j));
    // Twee armen die onder een spitse hoek (minder dan 50°, op het raster 30°: een rechte en een schuine weg) uit dezelfde knoop komen:
    // hun stoepen liggen dan tot ver buiten het kruisingsvak over elkaar en over het asfalt van de ander. De knoop loopt dan door tot
    // waar de twee stoepen loskomen; daartussen tekent hij één spits eiland met een ronde neus (de stoephoek van RoadNode).
    static float AcuteTrim(int n, int j)
    {
        var nb = RoadNb(n); if (nb.Count < 2) return 0f;
        Vector3 c = NodeDir(n), dj = NodeDir(j) - c; dj -= c * Vector3.Dot(dj, c); float best = 0f;
        Dims(ArmStyle(n, j), out _, out float sj);
        foreach (int k in nb)
        {
            if (k == j) continue; Vector3 dk = NodeDir(k) - c; dk -= c * Vector3.Dot(dk, c);
            float phi = Vector3.Angle(dj, dk); if (phi >= 50f) continue;
            Dims(ArmStyle(n, k), out _, out float sk);
            best = Mathf.Max(best, (sj + sk + 0.3f) / Mathf.Sin(phi * Mathf.Deg2Rad));
        }
        return Mathf.Min(best, 22f);
    }

    // Een knoop: geen twee wegburen, een knik, of een overgang naar een smallere weg (het bredere vak wordt de knoop en tekent
    // de overgang, zodat elke lijn één breedte heeft)
    static bool NodeShape(int i, List<int> nb)
    {
        if (Rot.Contains(i) || nb.Count != 2 || TurnAt(i, nb[0], nb[1]) >= BendNode) return true;   // een rotonde is altijd een knoop
        byte s = StyleOf(i); return (StyleOf(nb[0]) < s && RoadNb(nb[0]).Count == 2 && !Rot.Contains(nb[0])) || (StyleOf(nb[1]) < s && RoadNb(nb[1]).Count == 2 && !Rot.Contains(nb[1]));   // de stijl van een kruising of eind telt niet: die volgt zijn armen
    }
    static bool IsRoadNode(int i) => NodeShape(i, RoadNb(i));
    // de stijl van de arm van knoop n naar wegbuur j: die van de lijn erachter (het eerste vak ervan). Twee knopen naast elkaar:
    // die van het vak met twee wegburen (een kruising of eind volgt zijn armen), anders de smalste van de twee
    static byte ArmStyle(int n, int j)
    {
        if (styleOverride >= 0) return (byte)styleOverride;
        if (!IsRoad(j)) return StyleOf(n);
        if (!IsRoadNode(j)) return StyleOf(j);
        bool dn = RoadNb(n).Count == 2 && !Rot.Contains(n), dj = RoadNb(j).Count == 2 && !Rot.Contains(j);
        return dn && !dj ? StyleOf(n) : dj && !dn ? StyleOf(j) : (byte)Mathf.Min(StyleOf(n), StyleOf(j));
    }
    public static int RoadPrio(int j, int arm) => ArmStyle(j, arm) > 0 ? 1 : 0;   // voorrang op kruising j: een brede weg of laan gaat voor
    static byte ChainStyle(List<int> ch, bool closed) => closed || ch.Count > 2 ? StyleOf(ch[closed ? 0 : 1]) : ArmStyle(ch[0], ch[ch.Count - 1]);

    // ---------- de graaf ----------
    static void RoadGraph(out Dictionary<int, List<int>> nbs, out HashSet<int> nodes, out List<List<int>> chains, out List<bool> closed)
    {
        nbs = new Dictionary<int, List<int>>(); nodes = new HashSet<int>(); chains = new List<List<int>>(); closed = new List<bool>();
        foreach (var kv in Cells) if (kv.Value.Count > 0 && kv.Value[0] == "weg") nbs[kv.Key] = RoadNb(kv.Key);
        foreach (var kv in nbs) if (NodeShape(kv.Key, kv.Value)) nodes.Add(kv.Key);
        var done = new HashSet<long>();   // stappen van vak naar vak die al in een lijn zitten (beide richtingen)
        foreach (int n in nodes) foreach (int p in nbs[n])
        {
            if (done.Contains(DKey(n, p))) continue;
            var ch = new List<int> { n }; int prev = n, cur = p;
            while (true)
            {
                ch.Add(cur); done.Add(DKey(prev, cur)); done.Add(DKey(cur, prev));
                if (nodes.Contains(cur) || ch.Count > 4000) break;
                var l = nbs[cur]; int nx = l[0] == prev ? l[1] : l[0]; prev = cur; cur = nx;
            }
            chains.Add(ch); closed.Add(false);
        }
        foreach (var kv in nbs)   // ringen zonder knoop
        {
            int s = kv.Key; if (nodes.Contains(s) || done.Contains(DKey(s, kv.Value[0]))) continue;
            var ch = new List<int> { s }; int prev = s, cur = kv.Value[0]; done.Add(DKey(s, cur)); done.Add(DKey(cur, s));
            while (cur != s && ch.Count < 4000)
            {
                ch.Add(cur); var l = nbs[cur]; int nx = l[0] == prev ? l[1] : l[0]; done.Add(DKey(cur, nx)); done.Add(DKey(nx, cur)); prev = cur; cur = nx;
            }
            chains.Add(ch); closed.Add(true);
        }
    }

    // ---------- bijwerken ----------
    static long Mix(long h, long x) => (h ^ x) * 1099511628211L;
    static long Cm(float v) => Mathf.RoundToInt(v * 100f);
    static void RebuildRoads()
    {
        if (roadRoot == null) return;
        RoadGraph(out var nbs, out var nodes, out var chains, out var closed);
        edgeMate.Clear(); foreach (int n in nodes) if (nbs[n].Count >= 4) { int h = EdgeMateOf(n); if (h >= 0) edgeMate[n] = h; }
        var want = new HashSet<string>();
        var xs = EdgeCrossings(); crossAt.Clear(); var crossEnds = new HashSet<int>();
        foreach (var x in xs)
        {
            crossAt.Add(CrossPoint(x)); crossEnds.Add(x.u); crossEnds.Add(x.v);
            string key = "x:" + x.u + "," + x.v; want.Add(key);
            long sig = 1469598103934665603L; foreach (int c in new[] { x.u, x.v, x.b1, x.b2 }) { sig = Mix(sig, c); sig = Mix(sig, Cm(G(c))); }
            var xx = x; Piece(key, sig, mb => CrossMesh(mb, xx));
        }
        for (int c = 0; c < chains.Count; c++)
        {
            var ch = chains[c]; string key = (closed[c] ? "r:" : "c:") + string.Join(",", ch); want.Add(key);
            long sig = 1469598103934665603L;
            foreach (int i in ch) { sig = Mix(sig, i); sig = Mix(sig, Cm(G(i))); sig = Mix(sig, StyleOf(i)); sig = Mix(sig, Cm(MinCorner(i))); sig = Mix(sig, Under.ContainsKey(i) ? 1 : 0); sig = Mix(sig, nbs[i].Count); sig = Mix(sig, crossEnds.Contains(i) ? 1 : 0); sig = Mix(sig, Rot.Contains(i) ? 1 : 0); }   // ook als een kruispunt later een rotonde wordt: dan de armen opnieuw (ze houden op bij de buitenrand)
            for (int k = 0; k + 1 < ch.Count; k++) sig = Mix(sig, Flow(ch[k], ch[k + 1]));
            sig = Mix(sig, edgeMate.TryGetValue(ch[0], out int m0) ? m0 : -1); sig = Mix(sig, edgeMate.TryGetValue(ch[ch.Count - 1], out int m1) ? m1 : -1);
            sig = Mix(sig, Lights ? 1 : 0); if (ch.Count >= 2) { sig = Mix(sig, RoadPrio(ch[0], ch[1])); sig = Mix(sig, RoadPrio(ch[ch.Count - 1], ch[ch.Count - 2])); foreach (int j in nbs[ch[0]]) sig = Mix(sig, RoadPrio(ch[0], j)); foreach (int j in nbs[ch[ch.Count - 1]]) sig = Mix(sig, RoadPrio(ch[ch.Count - 1], j)); }
            Piece(key, sig, mb => ChainMesh(mb, ch, closed[c], nbs, nodes));
        }
        foreach (int n in nodes)
        {
            string key = "n:" + n; want.Add(key);
            long sig = Mix(Mix(Mix(Mix(Mix(1469598103934665603L, n), Cm(G(n))), StyleOf(n)), Cm(MinCorner(n))), (Lights ? 2 : 0) + (Under.ContainsKey(n) ? 1 : 0) + (Rot.Contains(n) ? 4 : 0));
            sig = Mix(sig, edgeMate.TryGetValue(n, out int em) ? em : -1); foreach (int j in Partners(n)) sig = Mix(sig, edgeMate.TryGetValue(j, out int ej) ? ej : -1);
            foreach (int j in Partners(n)) { sig = Mix(sig, j); sig = Mix(sig, Cm(G(j))); sig = Mix(sig, IsRoad(j) ? 1 : IsPath(j) ? 2 : 0); sig = Mix(sig, Flow(n, j)); sig = Mix(sig, IsRoad(j) ? ArmStyle(n, j) : 0); }
            Piece(key, sig, mb => NodeMesh(mb, n, nbs[n]));
        }
        RebuildPaths(want);   // paden en fietspaden (PlanetPaths.cs)
        var gone = new List<string>(); foreach (var kv in roadPieces) if (!want.Contains(kv.Key)) gone.Add(kv.Key);
        foreach (var k in gone) { if (roadPieces[k].go != null) { PropsForget(roadPieces[k].go); Object.Destroy(roadPieces[k].go); } roadPieces.Remove(k); }
    }
    static void Piece(string key, long sig, System.Action<Dictionary<string, MeshBuilder>> build)
    {
        if (roadPieces.TryGetValue(key, out var old)) { if (old.sig == sig && old.go != null) return; if (old.go != null) { PropsForget(old.go); Object.Destroy(old.go); } }
        var mb = new Dictionary<string, MeshBuilder>(); PropsStart(mb); try { build(mb); } catch { PropsCommit(null); throw; }
        var go = new GameObject(key); go.transform.SetParent(roadRoot, false);
        foreach (var kv in mb)
        {
            if (kv.Value.v.Count == 0) continue;
            var child = new GameObject(kv.Key); child.transform.SetParent(go.transform, false);
            child.layer = kv.Key == "gras" || kv.Key == "stam" || kv.Key == "blad" || kv.Key == "steen" || kv.Key == "water" || kv.Key == "zand" || kv.Key == "riet" ? Layers.Gebouw : Layers.Weg;
            var mesh = kv.Value.Build(); child.AddComponent<MeshFilter>().sharedMesh = mesh;
            child.AddComponent<MeshRenderer>().sharedMaterial = kv.Key == "lamp" ? Mats.GetUnlit("p_lamp", ColorOf(kv.Key)) : Mats.Get("p_" + kv.Key, ColorOf(kv.Key));
            if (kv.Key != "lijn" && kv.Key != "lamp" && kv.Key != "blad" && kv.Key != "water" && kv.Key != "riet") child.AddComponent<MeshCollider>().sharedMesh = mesh;
        }
        PropsCommit(go);   // lantaarns en bomen langs de weg sloopbaar (PlanetCity.Wreck.cs)
        roadPieces[key] = new RoadPiece { sig = sig, go = go };
    }

    // ---------- een knoop ----------
    static void NodeMesh(Dictionary<string, MeshBuilder> mb, int n, List<int> arms)
    {
        var fr = new Frame(n, true).At(NodeDir(n)); var ends = new List<Vector2>();
        foreach (int j in arms)
        {
            Vector3 d3 = (NodeDir(j) - NodeDir(n)) * Planet.R; Vector2 q = new Vector2(Vector3.Dot(d3, fr.e), Vector3.Dot(d3, fr.n));   // naar het hart van de buur (bij een kruispunt op de rand: daar, net als de lijn ertussen)
            ends.Add(q.normalized * NodeTrim(n, j, arms.Count));
        }
        if (IsRot(n)) { RoundaboutMesh(mb, n, arms); return; }
        RoadNode(mb, fr, n, ends, arms);
    }

    // ---------- een lijn ----------
    struct RP { public Vector3 p; public float h; public int cell; }
    static RP LerpRP(RP a, RP b, float f) => new RP { p = Vector3.Lerp(a.p, b.p, f), h = Mathf.Lerp(a.h, b.h, f), cell = f < 0.5f ? a.cell : b.cell };
    // de lijn door de vakmiddens met ronde hoeken (smoothPts), ongeveer elke 1,5 m een punt, op de bol
    static List<RP> SmoothLine(List<int> ch, bool closed, float[] hs = null)   // hs: de hoogte per vak (anders G)
    {
        var v = new List<RP>(); for (int k = 0; k < ch.Count; k++) { int i = ch[k]; v.Add(new RP { p = NodeDir(i) * Planet.R, h = hs != null ? hs[k] : G(i), cell = i }); }
        if (closed) { var m = LerpRP(v[0], v[1], 0.5f); m.cell = ch[0]; var w = new List<RP> { m }; for (int k = 1; k < v.Count; k++) w.Add(v[k]); w.Add(v[0]); w.Add(m); v = w; }
        var o = new List<RP> { v[0] }; RP cur = v[0];
        System.Action<RP, RP> lineTo = (a, b) => { int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a.p, b.p) / 1.5f)); for (int k = 1; k <= n; k++) o.Add(LerpRP(a, b, k / (float)n)); };
        for (int k = 1; k < v.Count - 1; k++)
        {
            RP A = cur, P = v[k], B = v[k + 1]; float la = Vector3.Distance(P.p, A.p), lb = Vector3.Distance(B.p, P.p), d = Mathf.Min(8f, la * 0.45f, lb * 0.45f);
            if (d < 0.3f) { lineTo(cur, P); cur = P; continue; }
            RP P1 = LerpRP(P, A, d / la), P2 = LerpRP(P, B, d / lb); P1.cell = P.cell; P2.cell = P.cell;
            lineTo(cur, P1);
            int nn = Mathf.Max(4, Mathf.CeilToInt(d / 1.0f));
            for (int s = 1; s <= nn; s++) { float t = s / (float)nn, u = 1f - t; o.Add(new RP { p = P1.p * (u * u) + P.p * (2f * u * t) + P2.p * (t * t), h = t < 0.5f ? Mathf.Lerp(P1.h, P.h, t * 2f) : Mathf.Lerp(P.h, P2.h, t * 2f - 1f), cell = P.cell }); }   // de bocht rond, de hoogte recht van hart tot hart (zoals het terrein): anders zakt de weg bovenaan een helling onder de grond
            cur = P2;
        }
        lineTo(cur, v[v.Count - 1]);
        for (int k = 0; k < o.Count; k++) { var q = o[k]; q.p = q.p.normalized * Planet.R; o[k] = q; }
        return o;
    }
    static List<RP> TrimLine(List<RP> pts, float t0, float t1)
    {
        var S = new float[pts.Count]; for (int k = 1; k < pts.Count; k++) S[k] = S[k - 1] + Vector3.Distance(pts[k].p, pts[k - 1].p);
        float len = S[pts.Count - 1]; if (t0 + t1 > len - 0.5f) { float f = Mathf.Max(0f, len - 0.5f) / Mathf.Max(1e-4f, t0 + t1); t0 *= f; t1 *= f; }
        System.Func<float, RP> at = s => { int k = 1; while (k < pts.Count - 1 && S[k] < s) k++; return LerpRP(pts[k - 1], pts[k], Mathf.Clamp01((s - S[k - 1]) / Mathf.Max(1e-5f, S[k] - S[k - 1]))); };
        var o = new List<RP> { at(t0) }; for (int k = 0; k < pts.Count; k++) if (S[k] > t0 + 0.05f && S[k] < len - t1 - 0.05f) o.Add(pts[k]); o.Add(at(len - t1));
        return o;
    }

    static void ChainMesh(Dictionary<string, MeshBuilder> mb, List<int> ch, bool closed, Dictionary<int, List<int>> nbs, HashSet<int> nodes)
    {
        System.Func<string, MeshBuilder> M = k => { if (!mb.TryGetValue(k, out var x)) { x = new MeshBuilder(); mb[k] = x; } return x; };
        byte style = ChainStyle(ch, closed);
        Dims(style, out hwA, out hwS);
        var line = SmoothLine(ch, closed);
        float t0 = !closed && nodes.Contains(ch[0]) ? NodeTrim(ch[0], ch[1], nbs[ch[0]].Count) : 0f, t1 = !closed && nodes.Contains(ch[ch.Count - 1]) ? NodeTrim(ch[ch.Count - 1], ch[ch.Count - 2], nbs[ch[ch.Count - 1]].Count) : 0f;
        var pts = TrimLine(line, t0, t1); int N = pts.Count; if (N < 2) return;
        // per punt: omhoog, opzij en de afstand langs de lijn
        var up = new Vector3[N]; var side = new Vector3[N]; var fw = new Vector3[N]; var S = new float[N]; var low = new float[N];
        for (int k = 0; k < N; k++)
        {
            up[k] = pts[k].p.normalized; Vector3 t = pts[Mathf.Min(N - 1, k + 1)].p - pts[Mathf.Max(0, k - 1)].p; t -= up[k] * Vector3.Dot(t, up[k]);
            fw[k] = t.normalized; side[k] = Vector3.Cross(up[k], fw[k]).normalized;
            if (k > 0) S[k] = S[k - 1] + Vector3.Distance(pts[k].p, pts[k - 1].p);
            bool overTube = false; foreach (var cx in crossAt) if ((pts[k].p - cx).sqrMagnitude < 8f * 8f) overTube = true;   // boven de koker van een brug
            bool nearPortal = false; foreach (var q in cuts) if ((pts[k].p - q.p).sqrMagnitude < 14f * 14f) nearPortal = true;   // vlak bij een portaal: de zijkant tot in de geul (anders een kier)
            low[k] = Underpass(pts[k].cell) || overTube || OverSpan(pts[k].p) ? SKB : Mathf.Min(SKB, (nearPortal ? MinCorner(pts[k].cell) : MinCornerNoCut(pts[k].cell)) - pts[k].h - 0.35f);   // onderkant van de zijkant: tot onder het maaiveld (niet in de opening van een onderdoorgang)
        }
        System.Func<int, float, float, Vector3> W = (k, off, y) => (pts[k].p + side[k] * off).normalized * (Planet.R + pts[k].h + y);
        for (int k = 0; k + 1 < N; k++)
        {
            int m = k + 1; Vector3 u = up[k];
            M("asfalt").Quad(W(k, -hwA, AY), W(k, hwA, AY), W(m, hwA, AY), W(m, -hwA, AY), u);
            for (int sg = -1; sg <= 1; sg += 2)
            {
                Vector3 out3 = side[k] * sg;
                M("rand").Quad(W(k, sg * hwA, AY), W(m, sg * hwA, AY), W(m, sg * (hwA + KW), SY), W(k, sg * (hwA + KW), SY), -out3 * KW + u * (SY - AY));
                M("stoep").Quad(W(k, sg * (hwA + KW), SY), W(m, sg * (hwA + KW), SY), W(m, sg * hwS, SY), W(k, sg * hwS, SY), u);
                if (Underpass(pts[k].cell) == Underpass(pts[m].cell)) M("stoep").Quad(W(k, sg * hwS, low[k]), W(m, sg * hwS, low[m]), W(m, sg * hwS, SY), W(k, sg * hwS, SY), out3);
                else   // naar een onderdoorgang: de diepe zijkant houdt op bij de rand van het vak (halverwege), anders hangt hij in de opening
                {
                    float hm = (pts[k].h + pts[m].h) * 0.5f; Vector3 pm = (pts[k].p + pts[m].p) * 0.5f, sm = (side[k] + side[m]).normalized * sg * hwS;
                    System.Func<float, Vector3> Wm = y => (pm + sm).normalized * (Planet.R + hm + y);
                    float lk = Underpass(pts[k].cell) ? SKB : low[k], lm = Underpass(pts[m].cell) ? SKB : low[m];
                    M("stoep").Quad(W(k, sg * hwS, lk), Wm(lk), Wm(SY), W(k, sg * hwS, SY), out3);
                    M("stoep").Quad(Wm(lm), W(m, sg * hwS, lm), W(m, sg * hwS, SY), Wm(SY), out3);
                }
                if (Under.ContainsKey(pts[k].cell) && Under.ContainsKey(pts[m].cell))   // brug: leuning langs beide kanten
                {
                    Vector3 a = W(k, sg * hwS, SY), b = W(m, sg * hwS, SY), ta = W(k, sg * hwS, 1.05f), tb = W(m, sg * hwS, 1.05f);
                    M("rand").Quad(a, b, tb, ta, out3); M("rand").Quad(a, b, tb, ta, -out3);
                }
            }
            if (style == 2)   // laan: middenberm met gras en een rand
            {
                M("rand").Quad(W(k, -1.3f, 0.2f), W(k, 1.3f, 0.2f), W(m, 1.3f, 0.2f), W(m, -1.3f, 0.2f), u);
                M("gras").Quad(W(k, -1.1f, 0.23f), W(k, 1.1f, 0.23f), W(m, 1.1f, 0.23f), W(m, -1.1f, 0.23f), u);
                foreach (int sg in new[] { -1, 1 }) M("rand").Quad(W(k, sg * 1.3f, AY), W(m, sg * 1.3f, AY), W(m, sg * 1.3f, 0.2f), W(k, sg * 1.3f, 0.2f), side[k] * sg);   // zijkant van de berm
            }
        }
        float L = S[N - 1];
        // markering: middenstreepjes (bij een brede weg ook de rijstrookstrepen); niet bij een laan
        System.Func<float, int> seg = s => { int k = 0; while (k < N - 2 && S[k + 1] < s) k++; return k; };
        System.Func<float, float, float, Vector3> At = (s, off, y) => { int k = seg(s); float f = Mathf.Clamp01((s - S[k]) / Mathf.Max(1e-5f, S[k + 1] - S[k])); Vector3 p = Vector3.Lerp(pts[k].p, pts[k + 1].p, f), sd = Vector3.Lerp(side[k], side[k + 1], f).normalized; float h = Mathf.Lerp(pts[k].h, pts[k + 1].h, f); return (p + sd * off).normalized * (Planet.R + h + y); };
        // bij een kruising (aan het begin of eind van dit stuk): een zebrapad over de arm op 0,5–2,5 m van de knoop, en daarvoor, waar het
        // binnenkomende verkeer stopt (de rechterhelft), een stopstreep (lichten aan) of haaientanden (lichten uit, op de arm die voorrang
        // moet geven: een smallere weg). Hier buiten de knoop is de weg altijd recht, ook bij scherpe hoeken; de middenstreepjes beginnen erna.
        bool jx0 = !closed && nodes.Contains(ch[0]) && nbs[ch[0]].Count >= 3 && !IsRot(ch[0]), jx1 = !closed && nodes.Contains(ch[ch.Count - 1]) && nbs[ch[ch.Count - 1]].Count >= 3 && !IsRot(ch[ch.Count - 1]);
        System.Action<bool> Mark = atStart =>
        {
            int n = atStart ? ch[0] : ch[ch.Count - 1], arm = atStart ? ch[1] : ch[ch.Count - 2]; float dir = atStart ? 1f : -1f, e0 = atStart ? 0f : L;   // s = e0 + dir·afstand tot de knoop
            System.Func<float, float, float> Sx = (dist, off) => off * (atStart ? -1f : 1f);   // rechts van wie naar de knoop rijdt
            System.Func<float, float, Vector3> Q = (dist, off) => At(e0 + dir * dist, Sx(dist, off), LY);
            int zm = Mathf.FloorToInt((hwA - 0.5f) / 0.95f);
            for (int z = -zm; z <= zm; z++) { float o = z * 0.95f; M("lijn").Quad(Q(0.5f, o - 0.28f), Q(0.5f, o + 0.28f), Q(2.5f, o + 0.28f), Q(2.5f, o - 0.28f), up[seg(e0 + dir * 1.5f)]); }
            if (!CanDrive(arm, n)) return;
            int maxPrio = 0; foreach (int j in nbs[n]) maxPrio = Mathf.Max(maxPrio, RoadPrio(n, j));
            if (Lights) M("lijn").Quad(Q(3.1f, 0.15f), Q(3.1f, hwA - 0.15f), Q(3.6f, hwA - 0.15f), Q(3.6f, 0.15f), up[seg(e0 + dir * 3.3f)]);
            else if (RoadPrio(n, arm) < maxPrio) for (float t = 0.3f; t + 0.5f < hwA - 0.1f; t += 0.7f) M("lijn").Tri(Q(3.0f, t), Q(3.0f, t + 0.5f), Q(3.7f, t + 0.25f), up[seg(e0 + dir * 3.3f)]);
        };
        if (jx0 && L > 4f) Mark(true); if (jx1 && L > 4f) Mark(false);
        float sBeg = jx0 ? 4.2f : 0.6f, sEnd = jx1 ? L - 4.2f : L - 0.3f;
        if (style != 2)
        {
            var lanes = style == 1 ? new[] { 0f, -hwA * 0.5f, hwA * 0.5f } : new[] { 0f };
            foreach (float lo in lanes)
            {
                float w = lo == 0f ? 0.07f : 0.05f;
                for (float s = sBeg; s + 1.4f < sEnd; s += 2.6f) M("lijn").Quad(At(s, lo - w, LY), At(s, lo + w, LY), At(s + 1.4f, lo + w, LY), At(s + 1.4f, lo - w, LY), up[seg(s)]);
            }
        }
        else for (float s = jx0 ? 6f : 3.5f; s < L - (jx1 ? 6f : 2f); s += 7f) { int k = seg(s); Vector3 b = At(s, 0f, 0.2f); if (InBore(b, out _)) continue;   /* geen bomen in een tunnel */ if (!PropBegin(PropBoom, b, up[k], 0.25f, 4.6f, 1.5f, 1.7f)) continue; Prism3D(M("stam"), b, up[k], 0.2f, 0.15f, 2.1f); Prism3D(M("blad"), b + up[k] * 1.7f, up[k], 1.5f, 0f, 2.9f); PropEnd(); }
        // lantaarns aan beide kanten om de 24 m, om en om (elke 12 m een)
        for (int sg = 1; sg >= -1; sg -= 2)
            for (float s = sg > 0 ? Mathf.Min(6f, L * 0.5f) : 18f; s < L - 2f; s += 24f) { int k = seg(s); Vector3 b = At(s, sg * (hwS - 0.6f), SY); if (InBore(b, out _)) continue;   /* geen lantaarns in een tunnel of de inrit ervoor */ if (!PropBegin(PropPaal, b, up[k], 0.14f, 5.1f)) continue; Prism3D(M("paal"), b, up[k], 0.08f, 0.06f, 4.95f); Prism3D(M("lamp"), b + up[k] * 4.65f, up[k], 0.28f, 0.2f, 0.45f); PropEnd(); }
        // eenrichtingsverkeer: een pijl halverwege elk stuk tussen twee vakmiddens, in de rijrichting
        for (int c = 0; c + 1 < ch.Count; c++)
        {
            int fl = Flow(ch[c], ch[c + 1]); if (fl == 0) continue;
            Vector3 mid = ((Planet.Cells[ch[c]].dir + Planet.Cells[ch[c + 1]].dir) * 0.5f).normalized * Planet.R; int bk = 0; float bd = 1e9f;
            for (int k = 0; k < N; k++) { float dd = (pts[k].p - mid).sqrMagnitude; if (dd < bd) { bd = dd; bk = k; } }
            Vector3 dirF = Planet.Cells[ch[c + 1]].dir - Planet.Cells[ch[c]].dir; float sgn = Vector3.Dot(dirF, fw[bk]) >= 0f ? 1f : -1f; if (fl < 0) sgn = -sgn;
            float s0 = Mathf.Clamp(S[bk], 1.2f, L - 1.2f), y = LY + 0.001f;
            Vector3 b0 = At(s0 - sgn * 1.1f, 0f, y), b1 = At(s0 + sgn * 0.22f, 0f, y), tip = At(s0 + sgn * 1.1f, 0f, y), sd = side[bk] * 0.14f, sw = side[bk] * 0.55f;
            M("lijn").Quad(b0 - sd, b0 + sd, b1 + sd, b1 - sd, up[bk]); M("lijn").Tri(b1 - sw, b1 + sw, tip, up[bk]);
        }
    }
    // een zeshoekig paaltje of kegeltje recht omhoog vanaf b
    static void Prism3D(MeshBuilder m, Vector3 b, Vector3 up, float r0, float r1, float h)
    {
        Vector3 a = Vector3.Cross(up, Mathf.Abs(up.y) < 0.9f ? Vector3.up : Vector3.right).normalized, c = Vector3.Cross(up, a);
        for (int i = 0; i < 6; i++)
        {
            float a0 = i * Mathf.PI / 3f, a1 = (i + 1) * Mathf.PI / 3f; Vector3 d0 = a * Mathf.Cos(a0) + c * Mathf.Sin(a0), d1 = a * Mathf.Cos(a1) + c * Mathf.Sin(a1), om = (d0 + d1) * 0.5f;
            if (r1 <= 0.001f) m.Tri(b + d0 * r0, b + d1 * r0, b + up * h, om);
            else { m.Quad(b + d0 * r0, b + d1 * r0, b + up * h + d1 * r1, b + up * h + d0 * r1, om); m.Tri(b + up * h, b + up * h + d0 * r1, b + up * h + d1 * r1, up); }
        }
    }

    // ---------- bouwvoorbeeld voor wegen en paden ----------
    // Kan hier een weg (of pad) komen? Dezelfde regels als Place: leeg, een bos, al een weg, een half vak, of een brug over water of een uitgraving.
    public static bool CanRoad(int i, string type)
    {
        if (!Cells.TryGetValue(i, out var l) || l.Count == 0) return true;
        if (l.Count == 1 && (l[0] == "park" || IsWild(l[0]))) return true;   // door een bos of ander landschap: de natuur wijkt (Place)
        if (type == "pad") return l.Count == 1 && (l[0] == "pad" || l[0] == "weg" || l[0] == "wegdeel");   // over een weg: onderdoorgang
        return (l.Count == 1 && (l[0] == "weg" || l[0] == "wegdeel" || l[0] == "meer" || l[0] == "water" || l[0] == "kuil" || l[0] == "pad"));   // over een pad: onderdoorgang
    }
    // Wat een klik op vak i zou maken (een wegknoop met armen naar alle wegen ernaast; bij een pad een rondje met strookjes),
    // of bij de gum het wegstuk dat verdwijnt.
    public static Mesh GhostRoad(int i, string type, bool erase, out Vector3 origin)
    {
        origin = Planet.Cells[i].dir * (Planet.R + G(i)); var mb = new Dictionary<string, MeshBuilder>();
        if (type == "pad" && !erase) { PadGhost(mb, new List<int> { i }); return MergeMesh(mb, origin); }
        var arms = new List<int>(); if (erase) arms = RoadNb(i); else foreach (int j in ClickLinks(i)) if (IsRoad(j)) arms.Add(j);
        styleOverride = erase ? -1 : DragStyle;
        try { NodeMesh(mb, i, arms); } finally { styleOverride = -1; }
        return MergeMesh(mb, origin);
    }
    // De weg (of het pad) langs een geplande sleeplijn, als doorlopend lint met ronde hoeken
    public static Mesh GhostLine(List<int> line, string type, out Vector3 origin)
    {
        origin = Planet.Cells[line[0]].dir * (Planet.R + G(line[0])); var mb = new Dictionary<string, MeshBuilder>();
        // waar de lijn een tunnel krijgt (BorePlan) niet over de berg: alleen de stukken ervoor en erna (de koker tekent ShowLevelGhost)
        var parts = new List<List<int>>(); int from = 0;
        var cuts2 = new List<(int, int)>(); foreach (var t in BorePlan(line, type)) { int x = line.IndexOf(t.a), y = line.IndexOf(t.b); cuts2.Add((Mathf.Min(x, y), Mathf.Max(x, y))); }
        cuts2.Sort();
        foreach (var (i, j) in cuts2) { parts.Add(line.GetRange(from, i - from + 1)); from = j; }
        parts.Add(line.GetRange(from, line.Count - from));
        foreach (var part in parts)
        {
            if (type == "pad") { PadGhost(mb, part); continue; }
            if (part.Count < 2 && parts.Count > 1) continue;
            styleOverride = DragStyle;
            try { ChainMesh(mb, part, false, new Dictionary<int, List<int>>(), new HashSet<int>()); } finally { styleOverride = -1; }
        }
        return MergeMesh(mb, origin);
    }
    // een pad als smal lint (2,2 m) door de vakmiddens, met een rondje op elk eind
    static void PadGhost(Dictionary<string, MeshBuilder> mb, List<int> line)
    {
        if (!mb.TryGetValue("pad", out var m)) mb["pad"] = m = new MeshBuilder();   // ook een tweede stuk (na een tunnel) in hetzelfde
        var pts = line.Count > 1 ? SmoothLine(line, false) : new List<RP> { new RP { p = Planet.Cells[line[0]].dir * Planet.R, h = G(line[0]), cell = line[0] } };
        System.Func<Vector3, float, Vector3> up = (p, h) => p.normalized * (Planet.R + h + 0.11f);
        for (int k = 0; k + 1 < pts.Count; k++)
        {
            Vector3 a = pts[k].p, b = pts[k + 1].p, u = a.normalized, t = b - a, sd = Vector3.Cross(u, t).normalized * 1.1f;
            m.Quad(up(a - sd, pts[k].h), up(a + sd, pts[k].h), up(b + sd, pts[k + 1].h), up(b - sd, pts[k + 1].h), u);
        }
        foreach (var e in new[] { pts[0], pts[pts.Count - 1] })
        {
            Vector3 u = e.p.normalized, ax = Vector3.Cross(u, Mathf.Abs(u.y) < 0.9f ? Vector3.up : Vector3.right).normalized, az = Vector3.Cross(u, ax);
            for (int k = 0; k < 16; k++) { float a0 = k * Mathf.PI / 8f, a1 = (k + 1) * Mathf.PI / 8f; m.Tri(up(e.p, e.h), up(e.p + (ax * Mathf.Cos(a0) + az * Mathf.Sin(a0)) * 1.4f, e.h), up(e.p + (ax * Mathf.Cos(a1) + az * Mathf.Sin(a1)) * 1.4f, e.h), u); }
        }
    }
}
