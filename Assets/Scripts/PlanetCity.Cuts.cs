using System.Collections.Generic;
using UnityEngine;

// Geulen en dijken van onderdoorgangen en wegen op een niveau (Cut, Carve) en de precieze bodem erlangs (ExactGround, CollectCuts).
public static partial class PlanetCity
{
    // ---------- de geul van een onderdoorgang ----------
    // Elk portaal snijdt een geul in het terrein, naar buiten toe: een bodem van 9 m breed op de hoogte van het pad, die na een
    // half vak over twee vakken (RampL) oploopt tot het maaiveld, met zijkanten onder 45°. Alleen omlaag, nooit omhoog.
    struct Cut { public Vector3 p, o, t; public float floor, ground, hw, skew, flat, ramp, end; public bool fill; }   // fill: een dijk onder een hoger gelegde weg (floor is dan de kruin)   // skew: het portaalvlak loopt scheef (evenwijdig aan de weg erover); flat, ramp, end: 0 is FlatL, RampL, tot voorbij de helling
    static readonly List<Cut> cuts = new List<Cut>();
    static readonly HashSet<int> carved = new HashSet<int>();
    public static bool RoadField = true;   // gewone wegen op een helling in de hoogtefunctie (uit: alleen wegen op een niveau, zoals vroeger)
    public static readonly Dictionary<int, float> preCarve = new Dictionary<int, float>();   // de hoogte van een vak voordat een geul het omlaag zette
    public static readonly Dictionary<int, float> preLevel = new Dictionary<int, float>();   // de hoogte van het terrein voordat een weg of pad op een niveau het verzette (komt terug bij weghalen)
    // een weg of pad verzet het terrein van zijn vak: de oude hoogte onthouden (de eerste keer) en de nieuwe zetten
    static void LevelTerr(int i, float h)
    {
        if (!preLevel.ContainsKey(i)) preLevel[i] = preCarve.TryGetValue(i, out var pre) ? pre : G(i); preCarve.Remove(i); SetTerr(i, h);
        foreach (int j in Planet.Cells[i].nb) Touch(j);   // het talud reikt tot twee vakken ver (een flauwe helling, twee niveaus diep)
    }
    // een weg of pad is weg: het terrein van het vak terug zoals het was (de geul ernaast gaat via RestoreCarve terug)
    static void RestoreLevel(int i) { if (preLevel.TryGetValue(i, out var h)) { preLevel.Remove(i); preCarve.Remove(i); SetTerr(i, h); Touch(i); foreach (int j in Planet.Cells[i].nb) Touch(j); } }
    // geulen die verdwenen of kleiner werden: de vakken gaan terug naar hun oude hoogte (of naar wat de geul nog vraagt)
    static void RestoreCarve()
    {
        foreach (var kv in new List<KeyValuePair<int, float>>(preCarve))
        {
            int c = kv.Key; float target = kv.Value; if (IsWalk(c) && !Underpass(c) && cuts.Count > 0) target = Mathf.Min(target, Carve(Planet.Cells[c].dir));
            if (G(c) < target - 0.01f) SetTerr(c, target);
            if (target >= kv.Value - 0.01f) preCarve.Remove(c);
        }
    }                  // de padvakken in een geul (hun hoogte komt uit de geul)
    static readonly Dictionary<int, Cut> deckSide = new Dictionary<int, Cut>(); // padvakken onder het dek van een schuine brug: de helft naar de weg is dek
    static float SideK => FieldK;   // de zijkant van een geul: even steil als het talud langs een weg op een niveau (1 op 1,5; was 45°, dat gaf een strook kale aarde bij het portaal)
    const float RampL = 31.2f, FlatL = 7.8f, PathHW = 4.5f, RoadHW = 6f, CutHW = 9f;   // halve breedte van de koker (pad, weg) en van de geulbodem (een vak)
    static float Carve(Vector3 dir) => Carve(dir, out _);
    static float Carve(Vector3 dir, out float sdMin)
    {
        float h = float.MaxValue; sdMin = float.MaxValue; Vector3 w = dir.normalized * Planet.R;
        foreach (var q in cuts)
        {
            if (q.fill || !CutVal(q, w, out float v, out float sd)) continue;
            h = Mathf.Min(h, v); sdMin = Mathf.Min(sdMin, Mathf.Max(0f, sd));
        }
        return h;
    }
    // de dijken onder hoger gelegde wegen en paden: het hoogste (MinValue: geen dijk)
    static float Fill(Vector3 dir)
    {
        float h = float.MinValue; Vector3 w = dir.normalized * Planet.R;
        foreach (var q in cuts) if (q.fill && CutVal(q, w, out float v, out _)) h = Mathf.Max(h, v);
        return h;
    }
    static int fillN;   // aantal dijkstukken
    // de hoogte van één geul of dijk op wereldpunt w (false: daar niet)
    static bool CutVal(Cut q, Vector3 w, out float v, out float sd)
    {
        float fl = q.flat > 0f ? q.flat : FlatL, rl = q.ramp > 0f ? q.ramp : RampL, end = q.end > 0f ? q.end : FlatL + RampL + 20f; v = 0f;
        Vector3 d = w - q.p; sd = Vector3.Dot(d, q.o) - q.skew * Vector3.Dot(d, q.t); if (sd < -0.01f || sd > end) return false;
        float t = Mathf.Abs(Vector3.Dot(d, q.t)); if (t > 40f) return false;
        v = q.floor + (q.ground - q.floor) * Mathf.Clamp01((sd - fl) / rl) + (q.fill ? -1f : 1f) * Mathf.Max(0f, t - q.hw) * SideK; return true;
    }
    static Vector3 Tan(Vector3 v, Vector3 up) { v -= up * Vector3.Dot(v, up); return v.normalized; }

    // TIJDELIJK (test): de grond van vak i zoals ExactGround hem tekent: per stuk het laagste en hoogste punt, en de geulen erbij
    public static string DebugGround(int i)
    {
        var c = Planet.Cells[i]; var near = CutsNear(c); if (near == null) return "geen geul in de buurt";
        var sb = new System.Text.StringBuilder(); sb.Append(near.Count).Append(" geulen (");
        foreach (var n in near) sb.Append(n.q.fill ? "dijk " : "geul ").Append("vloer ").Append(n.q.floor.ToString("0.0")).Append(" sd0 ").Append(n.sd.a.ToString("0.0")).Append(" t0 ").Append(n.t.a.ToString("0.0")).Append(" end ").Append(n.end.ToString("0")).Append("; ");
        sb.Append(") stukken: "); int m = c.corners.Length; float lo = float.MaxValue, hi = float.MinValue; int np = 0;
        for (int k = 0; k < m; k++) ExactGround(c, k, near, G(i), CornerBase(i, k), CornerBase(i, (k + 1) % m), (P0, P1, P2, h0, h1, h2) => { np++; lo = Mathf.Min(lo, Mathf.Min(h0, Mathf.Min(h1, h2))); hi = Mathf.Max(hi, Mathf.Max(h0, Mathf.Max(h1, h2))); });
        sb.Append(np).Append(" driehoeken, hoogte ").Append(lo.ToString("0.0")).Append(" tot ").Append(hi.ToString("0.0"));
        return sb.ToString();
    }
    // ---------- de bodem langs een geul of dijk, precies ----------
    // Elk driehoekje van een vak (in de vlakke coördinaten van het vak) wordt gesneden langs de lijnen waar een geul of dijk van
    // vorm verandert (begin en einde, eind van het vlakke stuk en van de helling, de randen van de bodem) en langs de lijnen waar
    // twee vlakken elkaar snijden. Elk stuk ligt dan in één vlak: het maaiveld, de hoogste dijk erop, de laagste geul erin. Zo
    // krijgt een verdiepte of verhoogde weg strakke randen dwars door de vakken, in plaats van een rafelige langs de hoekpunten.
    // Buren rekenen met dezelfde geulen, dus de randen sluiten op elkaar aan.
    struct Lin
    {
        public float a, x, y;
        public float At(Vector2 p) => a + x * p.x + y * p.y;
        public static Lin operator -(Lin p, Lin q) => new Lin { a = p.a - q.a, x = p.x - q.x, y = p.y - q.y };
        public static Lin operator +(Lin p, Lin q) => new Lin { a = p.a + q.a, x = p.x + q.x, y = p.y + q.y };
        public static Lin operator *(Lin p, float k) => new Lin { a = p.a * k, x = p.x * k, y = p.y * k };
        public static Lin C(float v) => new Lin { a = v };
    }
    struct NearCut { public Cut q; public Lin sd, t; public float fl, rl, end; }
    // de geulen en dijken die bij vak c in de buurt komen, met hun afstand langs (sd) en dwars (t) als lineaire functie van de vlakke coördinaten
    static List<NearCut> CutsNear(Planet.Cell c)
    {
        List<NearCut> res = null; Vector3 o3 = c.dir * Planet.R;
        System.Func<Vector2, Vector3> W = q2 => (o3 + c.east * q2.x + c.north * q2.y).normalized * Planet.R;
        foreach (var q in cuts)
        {
            float fl = q.flat > 0f ? q.flat : FlatL, rl = q.ramp > 0f ? q.ramp : RampL, end = q.end > 0f ? q.end : FlatL + RampL + 20f;
            System.Func<Vector2, float> SD = q2 => { Vector3 d = W(q2) - q.p; return Vector3.Dot(d, q.o) - q.skew * Vector3.Dot(d, q.t); };
            System.Func<Vector2, float> TT = q2 => Vector3.Dot(W(q2) - q.p, q.t);
            float s0 = SD(Vector2.zero), t0 = TT(Vector2.zero);
            if (s0 < -c.radius - 2f || s0 > end + c.radius + 2f || Mathf.Abs(t0) > 40f + c.radius + 2f) continue;
            const float st = 5f;
            var sd = new Lin { a = s0, x = (SD(new Vector2(st, 0f)) - s0) / st, y = (SD(new Vector2(0f, st)) - s0) / st };
            var tl = new Lin { a = t0, x = (TT(new Vector2(st, 0f)) - t0) / st, y = (TT(new Vector2(0f, st)) - t0) / st };
            (res ??= new List<NearCut>()).Add(new NearCut { q = q, sd = sd, t = tl, fl = fl, rl = rl, end = end });
        }
        return res;
    }
    // de hoogte van één geul of dijk op p (false: daar niet), zoals CutVal
    static bool CutAt(NearCut n, Vector2 p, out float v)
    {
        v = 0f; float s = n.sd.At(p); if (s < -0.01f || s > n.end) return false;
        float t = Mathf.Abs(n.t.At(p)); if (t > 40f) return false;
        v = n.q.floor + (n.q.ground - n.q.floor) * Mathf.Clamp01((s - n.fl) / n.rl) + (n.q.fill ? -1f : 1f) * Mathf.Max(0f, t - n.q.hw) * SideK; return true;
    }
    // dezelfde geul of dijk als lineaire functie, in het stuk waar p ligt (null: daar niet)
    static Lin? CutLin(NearCut n, Vector2 p)
    {
        float s = n.sd.At(p); if (s < -0.01f || s > n.end) return null;
        float t = n.t.At(p); if (Mathf.Abs(t) > 40f) return null;
        var h = Lin.C(n.q.floor);
        if (s >= n.fl + n.rl) h = h + Lin.C(n.q.ground - n.q.floor);
        else if (s > n.fl) h = h + (n.sd - Lin.C(n.fl)) * ((n.q.ground - n.q.floor) / n.rl);
        float sk = n.q.fill ? -SideK : SideK;
        if (t > n.q.hw) h = h + (n.t - Lin.C(n.q.hw)) * sk; else if (t < -n.q.hw) h = h + (Lin.C(-n.q.hw) - n.t) * sk;
        return h;
    }
    // een veelhoek in tweeën langs f = 0 (alleen als f er echt van teken wisselt)
    static bool SplitBy(List<Vector2> poly, Lin f, List<List<Vector2>> outp)
    {
        float lo = float.MaxValue, hi = float.MinValue; foreach (var v in poly) { float x = f.At(v); lo = Mathf.Min(lo, x); hi = Mathf.Max(hi, x); }
        if (lo > -1e-3f || hi < 1e-3f) return false;
        var a = ClipHalf(poly, new Vector2(f.x, f.y), -f.a); var b = ClipHalf(poly, new Vector2(-f.x, -f.y), f.a);
        if (a.Count >= 3) outp.Add(a); if (b.Count >= 3) outp.Add(b); return true;
    }
    // driehoekje k van vak c (midden, hoek k, hoek k+1) met het maaiveld hc0/h1/h2 zonder geul of dijk, gesneden en op hoogte
    static void ExactGround(Planet.Cell c, int k, List<NearCut> near, float hc0, float h1, float h2, System.Action<Vector3, Vector3, Vector3, float, float, float> emit)
    {
        int m = c.corners.Length; Vector2 L1 = c.local[k], L2 = c.local[(k + 1) % m]; Vector3 D0 = c.dir, D1 = c.corners[k], D2 = c.corners[(k + 1) % m];
        float det = L1.x * L2.y - L1.y * L2.x; if (Mathf.Abs(det) < 1e-6f) return;
        float gx = ((h1 - hc0) * L2.y - (h2 - hc0) * L1.y) / det, gy = ((h2 - hc0) * L1.x - (h1 - hc0) * L2.x) / det; var bas = new Lin { a = hc0, x = gx, y = gy };   // het maaiveld als vlak
        // 1. langs de vormlijnen van de geulen en dijken
        var polys = new List<List<Vector2>> { new List<Vector2> { Vector2.zero, L1, L2 } };
        foreach (var n in near)
            foreach (var f in new[] { n.sd - Lin.C(-0.01f), n.sd - Lin.C(n.fl), n.sd - Lin.C(n.fl + n.rl), n.sd - Lin.C(n.end), n.t - Lin.C(n.q.hw), n.t - Lin.C(-n.q.hw) })
            {
                var next = new List<List<Vector2>>(); foreach (var pl in polys) if (!SplitBy(pl, f, next)) next.Add(pl); polys = next;
            }
        // 2. langs de snijlijnen van de vlakken die er gelden
        var done = new List<List<Vector2>>(); var work = new Stack<List<Vector2>>(polys); int guard = 0;
        while (work.Count > 0)
        {
            var pl = work.Pop(); Vector2 ctr = Vector2.zero; foreach (var v in pl) ctr += v; ctr /= pl.Count;
            var fs = new List<Lin> { bas }; foreach (var n in near) { var f = CutLin(n, ctr); if (f.HasValue) fs.Add(f.Value); }
            bool split = false;
            if (++guard < 4000)
                for (int a = 0; a < fs.Count && !split; a++) for (int b = a + 1; b < fs.Count && !split; b++)
                {
                    var tmp = new List<List<Vector2>>(); if (SplitBy(pl, fs[a] - fs[b], tmp)) { foreach (var x in tmp) work.Push(x); split = true; }
                }
            if (!split) done.Add(pl);
        }
        // 3. op hoogte: het maaiveld, de hoogste dijk erop, de laagste geul erin; op de bol via de hoeken van het driehoekje
        System.Func<Vector2, Vector3> Dir = q2 =>
        {
            float b1 = (q2.x * L2.y - q2.y * L2.x) / det, b2 = (L1.x * q2.y - L1.y * q2.x) / det, b0 = 1f - b1 - b2;
            return (D0 * b0 + D1 * b1 + D2 * b2).normalized;
        };
        foreach (var pl in done)
        {
            var P = new Vector3[pl.Count]; var Hh = new float[pl.Count];
            for (int v = 0; v < pl.Count; v++)
            {
                float h = bas.At(pl[v]);
                foreach (var n in near) if (n.q.fill && CutAt(n, pl[v], out float fv)) h = Mathf.Max(h, fv);
                foreach (var n in near) if (!n.q.fill && CutAt(n, pl[v], out float cv)) h = Mathf.Min(h, cv);
                Hh[v] = h; P[v] = Dir(pl[v]) * (Planet.R + h);
            }
            for (int v = 1; v + 1 < pl.Count; v++) emit(P[0], P[v], P[v + 1], Hh[0], Hh[v], Hh[v + 1]);
        }
    }

    // de portalen van alle onderdoorgangen (rechte door een wegvak en schuine over een rand)
    static void CollectCuts(List<Cut> o, Dictionary<int, Cut> side)
    {
        foreach (var x in EdgeCrossings())
        {
            float hw = x.road ? RoadHW : PathHW;
            if (x.swap ? WalkLevel(x.u) != 0 && WalkLevel(x.v) != 0 : WalkLevel(x.b1) != 0 && WalkLevel(x.b2) != 0) continue;   // de onderste ligt op een niveau: eigen bak, het betonblok tekent de brug (BridgeBlock)
            if (x.swap)   // de schuine weg u–v gaat onder de weg b1–b2 door: portalen langs de rand, 7,8 m voor en na het midden
            {
                float deck = Mathf.Max(G(x.b1), G(x.b2)), floor = Mathf.Min(deck - UnderClear, Mathf.Min(G(x.u), G(x.v)));
                Vector3 up = (Planet.Cells[x.u].dir + Planet.Cells[x.v].dir).normalized, along = Tan(Planet.Cells[x.v].dir - Planet.Cells[x.u].dir, up), across = Vector3.Cross(up, along).normalized;
                for (int sg = -1; sg <= 1; sg += 2) o.Add(new Cut { p = (up * Planet.R + along * (sg * FlatL)).normalized * Planet.R, o = along * sg, t = across, floor = floor, ground = deck, hw = CutHW });
                continue;
            }
            {
                float deck = Mathf.Max(G(x.u), G(x.v)), floor = Mathf.Min(deck - UnderClear, Mathf.Min(G(x.b1), G(x.b2)));
                Vector3 up = (Planet.Cells[x.u].dir + Planet.Cells[x.v].dir).normalized, road = Tan(Planet.Cells[x.v].dir - Planet.Cells[x.u].dir, up);
                foreach (var (b, other) in new[] { (x.b1, x.b2), (x.b2, x.b1) })
                {
                    Vector3 bd = Planet.Cells[b].dir, out3 = Tan(bd - Planet.Cells[other].dir, bd);
                    var c = new Cut { p = bd * Planet.R, o = out3, t = road, floor = floor, ground = deck, hw = CutHW }; o.Add(c); side[b] = c;
                }
            }
        }
        foreach (var kv in Under)
        {
            if (!Underpass(kv.Key) || !Low.TryGetValue(kv.Key, out var lo)) continue; int i = kv.Key; var cell = Planet.Cells[i]; Topo();
            bool road = kv.Value == "weg";
            if (UnderIsLevelled(i, road)) continue;   // wat eronderdoor gaat ligt zelf op een niveau: eigen bak, het betonblok tekent de brug (BridgeBlock)
            foreach (var od in Openings(i))
            {
                Vector3 rw = SkewFor(i, od), mdir = (cell.dir * Planet.R + od * FlatL).normalized, out3 = Tan(od, mdir), tt = Vector3.Cross(mdir, out3).normalized;
                float rt = Vector3.Dot(rw, tt), sk = rw == Vector3.zero || Mathf.Abs(rt) < 0.2f ? 0f : Mathf.Clamp(Vector3.Dot(rw, out3) / rt, -1f, 1f);
                var cutc = new Cut { p = mdir * Planet.R, o = out3, t = tt, floor = lo, ground = G(i), hw = CutHW, skew = sk }; o.Add(cutc);
                int nbj = OpeningNb(i, od); if (sk != 0f && nbj >= 0) side[nbj] = cutc;
            }
        }
        // Een weg of pad op een ander niveau (Page Up/Down) is een vlak vak op zijn eigen hoogte met keermuren langs de randen naar
        // de buren (zoals een bebouwd vak zijn sokkel heeft): verdiept een bak, verhoogd een plateau. Hier alleen bijhouden welke.
        // Een weg of pad op een ander niveau (Page Up/Down) schrijft zijn hoogte in het terrein, zoals in Cities: Skylines 2: onder de
        // weg (plus een marge) ligt de grond op weghoogte, daarbuiten loopt hij onder 45° terug naar het gewone terrein. Dat is één
        // hoogtefunctie (GroundField) van de afstand tot de weglijn, van hart tot hart; de vakken erlangs worden fijner verdeeld en
        // bemonsterd (BuildGround), zodat gedeelde randen in beide vakken dezelfde hoogte krijgen.
        levelled.Clear(); fillN = 0; segs.Clear(); caps.Clear();
        foreach (var kv in Cells) if (WalkLevel(kv.Key) != 0) levelled.Add(kv.Key);
        int WalkN(int i) { int n = 0; foreach (int j in Partners(i)) if (IsWalk(j)) n++; return n; }
        // een kop (zeshoek rond het hart) bij een doodlopend eind, een bocht of een splitsing; alleen een recht stuk loopt gewoon door
        bool Capped(int i)
        {
            var ws = new List<int>(); foreach (int j in Partners(i)) if (IsWalk(j)) ws.Add(j); if (ws.Count != 2) return true;
            Vector3 d = Planet.Cells[i].dir; return Vector3.Dot(Tan(Planet.Cells[ws[0]].dir - d, d), Tan(Planet.Cells[ws[1]].dir - d, d)) > -0.95f;
        }
        foreach (int a in levelled)
        {
            Vector3 ad = Planet.Cells[a].dir; float hw = WalkHW(a); int na = WalkN(a);
            foreach (int b in Partners(a))
            {
                if (!IsWalk(b) || IsBore(a, b)) continue; if (levelled.Contains(b) && b < a) continue;   // elk stuk één keer; een tunnel door een berg graaft niets af
                Vector3 bd = Planet.Cells[b].dir, along = Tan(bd - ad, ad); Vector3 pa = ad * Planet.R, pb = bd * Planet.R;
                // elk stuk loopt van hart tot hart: op een recht stuk sluit het precies aan op het volgende (een stuk dat doorliep gaf
                // trapjes waar de helling verandert); een doodlopend eind, bocht of splitsing krijgt een zeshoekige kop (AddCap)
                bool capA = Capped(a), capB = levelled.Contains(b) && Capped(b);
                AddWalkSeg(segs, new Seg { a = pa, b = pb, ha = G(a), hb = LevelFor(b, a), hw = hw, capA = capA, capB = capB, ia = a, ib = b });   // met een vlak stuk op een padknoop
            }
            if (Capped(a)) AddCap(a, G(a), na <= 1 && IsRoad(a) ? hw + HeadExtra(a) : hw);   // doodlopend, bocht, splitsing of los vak; doodlopend: ruim genoeg voor het keerrondje
        }
        // een weg of pad dat de geul van een brug uitgroef (preCarve) en helt naar een buur op het maaiveld: ook dat stuk krijgt de
        // bak met talud (de geul zelf loopt korter op dan de weg, en dan lag de grond op de helling boven de weg)
        foreach (var kv in preCarve)
        {
            int a = kv.Key; if (!IsWalk(a) || Underpass(a) || levelled.Contains(a)) continue; Vector3 ad = Planet.Cells[a].dir; float hw = WalkHW(a);
            foreach (int b in Partners(a))
            {
                if (!IsWalk(b) || Underpass(b) || levelled.Contains(b) || (preCarve.ContainsKey(b) && b < a) || IsBore(a, b)) continue;
                AddWalkSeg(segs, new Seg { a = ad * Planet.R, b = Planet.Cells[b].dir * Planet.R, ha = G(a), hb = LevelFor(b, a), hw = hw, ia = a, ib = b });
            }
        }
        // Gewone wegen (niet op een niveau) waar het terrein ernaast niet vlak is: ook in de hoogtefunctie, zoals in Cities: Skylines 2.
        // Een weg is breder dan zijn vakken (de stoep in een bocht, een kruispunt, een spits eiland); zonder dit stak daar het terrein of
        // de sokkel van een huis ernaast door de weg heen. Op vlakke grond verandert er niets (en kost het niets).
        bool Uneven(int a)
        {
            float ga = G(a);
            foreach (int x in Planet.Cells[a].nb) { if (Mathf.Abs(G(x) - ga) > 0.2f) return true; foreach (int y in Planet.Cells[x].nb) if (Mathf.Abs(G(y) - ga) > 0.2f) return true; }
            return false;
        }
        bool PlainRoad(int a) => IsRoad(a) && !levelled.Contains(a) && !Underpass(a) && !carved.Contains(a) && !preCarve.ContainsKey(a) && !IsRot(a);
        var uneven = new HashSet<int>(); if (RoadField) foreach (var kv in Cells) if (PlainRoad(kv.Key) && (RoadH.Count > 0 || Uneven(kv.Key))) uneven.Add(kv.Key);   // nieuwe opbouw (RoadH): elke weg, het terrein eronder is niet op weghoogte
        // De weg wordt in een bocht afgerond (SmoothLine): bij het hart van een bochtvak ligt hij dan op het gemiddelde van de bocht,
        // lager dan het vak zelf als hij daar daalt; de grond rekent met die hoogte (anders lag hij tot een meter boven de stoep)
        float RH(int x)
        {
            var ps = new List<int>(); foreach (int j in Partners(x)) if (IsRoad(j) && !IsBore(x, j)) ps.Add(j);
            if (ps.Count >= 3) { float lo = 0f; foreach (int j in ps) lo = Mathf.Min(lo, G(j) - G(x)); return G(x) + lo * 0.6f; }   // een kruising helt naar zijn armen (Frame.H): de kop zo laag als de laagste arm op 8 m
            if (ps.Count != 2 || !Capped(x)) return G(x);
            return Mathf.Min(G(x), (G(ps[0]) + 2f * G(x) + G(ps[1])) * 0.25f);
        }
        foreach (int a in uneven)
        {
            Vector3 ad = Planet.Cells[a].dir; float hw = WalkHW(a); int na = 0;
            foreach (int b in Partners(a))
            {
                if (!IsRoad(b)) continue; na++;
                if (!PlainRoad(b) || IsBore(a, b) || TunnelLinks.Contains(Key(a, b)) || (uneven.Contains(b) && b < a)) continue;
                AddWalkSeg(segs, new Seg { a = ad * Planet.R, b = Planet.Cells[b].dir * Planet.R, ha = RH(a), hb = RH(b), hw = hw, capA = Capped(a), capB = uneven.Contains(b) && Capped(b), ia = a, ib = b });
            }
            // een kruising tekent zijn stoep verder dan de gewone breedte (afgeronde hoeken, een spitse kruising loopt door): de kop ruimer
            if (Capped(a)) AddCap(a, RH(a), na <= 1 ? hw + HeadExtra(a) : na >= 3 ? hw + 4f : hw);
        }
        // Een brug over een dal: van het hart van elk landhoofdvak tot het landhoofd ligt de grond op weghoogte, met het talud ernaast
        // (anders ligt het terrein van de helling over de weg naar de brug); onder het dek blijft het lager (SpanBottom in GroundField).
        foreach (long k in TunnelLinks)   // (ook een tunnel: de weg helt van het portaalvak de berg in)
        {
            int a0 = (int)(k >> 20), b0 = (int)(k & 0xFFFFF); if (!IsBore(a0, b0) || !IsWalk(a0) || !IsWalk(b0)) continue;
            bool spanNew = RoadH.Count > 0 && SpanLinks.Contains(k);   // nieuwe opbouw: het dek van een dalbrug loopt van hart tot hart; de grond bij het landhoofd hoort bij de weg tot waar het dek 6 m boven het terrein komt (daar begint het echte dek: ontwerp 8.3)
            foreach (var (e, oe) in new[] { (a0, b0), (b0, a0) })
            {
                Vector3 de = Planet.Cells[e].dir, dO = Planet.Cells[oe].dir; float L = Vector3.Angle(de, dO) * Mathf.Deg2Rad * Planet.R, t = FlatL * 0.55f / L;
                if (spanNew)
                {
                    t = 2f / L; for (float sm = 2f; sm < L * 0.5f; sm += 1f) { float dd = Mathf.Lerp(G(e), G(oe), sm / L) - BaseAt(Vector3.Slerp(de, dO, sm / L).normalized); if (dd > 6f) break; t = sm / L; }
                }
                Vector3 pe = Vector3.Slerp(de, dO, t).normalized * Planet.R;
                AddWalkSeg(segs, new Seg { a = de * Planet.R, b = pe, ha = G(e), hb = Mathf.Lerp(G(e), G(oe), t), hw = WalkHW(e), ia = e, ib = e });
            }
        }
        // gewone wegen en paden (niet op een niveau) in de buurt: onder zo'n weg ligt de grond op de hoogte van die weg, wat het talud
        // ernaast ook doet (zoals Cities: Skylines 2), anders loopt een talud bij een kruispunt over de weg heen
        SplitWalls();   // nieuwe opbouw: maaiveld, sokkel of bak per deel van een stuk (ontwerp 8.3)
        FillFieldCells();
        flats.Clear();
        if (HasField)
        {
            var seen = new HashSet<long>();
            foreach (var kv in Cells)
            {
                int a = kv.Key; if (!fieldCells.Contains(a) || !IsWalk(a) || levelled.Contains(a) || Underpass(a) || !NearSeg(Planet.Cells[a].dir, Planet.Cells[a].radius)) continue;   // (een vak binnen bereik zit altijd in fieldCells)
                foreach (int b in Partners(a))
                {
                    if (!IsWalk(b) || levelled.Contains(b) || IsBore(a, b) || !seen.Add(Key(a, b))) continue;
                    if (uneven.Contains(a) && IsRoad(b) && PlainRoad(b)) continue;   // die zit al in de hoogtefunctie
                    float fw = IsRoad(a) ? FlatHW(a) : Bike.Contains(a) ? 3.4f : 1.6f;   // een fiets- en voetpad is breder
                    AddWalkSeg(flats, new Seg { a = Planet.Cells[a].dir * Planet.R, b = Planet.Cells[b].dir * Planet.R, ha = G(a), hb = LevelFor(b, a), hw = fw, ia = a, ib = b });
                }
            }
        }
        fieldVer++;
    }
}
