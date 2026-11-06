using System.Collections.Generic;
using UnityEngine;

// Terrein v2. Stap 1: één hoogtefunctie H(punt) voor de hele planeet. Alles wat een hoogte van het maaiveld nodig heeft (lopen,
// rijden, bomen, bruggen, de scan in de tests) vraagt het hier; de mesh (BuildGround) hoort hetzelfde te geven, en in stap 2 wordt
// de mesh rechtstreeks uit H gebouwd, met gedeelde punten op de vakranden, zodat twee buren nooit meer verschillen.
// De lagen, in vaste volgorde:
//  1. het basisterrein: per vak een vlak over de waaierdriehoek (midden op G plus CenterOfs, hoeken op CornerBase: de laagste van de
//     drie vakken, of de weghoogte tussen twee verbonden wegvakken, of de kade of dam); een bebouwd vak is vlak op G (plateau);
//  2. geulen en dijken van onderdoorgangen (Carve, Fill): de hoogste dijk erop, de laagste geul erin;
//  3. de hoogtefunctie langs wegen (GroundField): wegen op een niveau en gewone wegen op een helling, met talud of steile kant,
//     onder een weg nooit boven het wegdek, onder een dalbrug lager, en de geulen daar nog eens.
// Een vlak plateau (bebouwd, niet door een weg of geul gesneden) blijft overal G: zijn sokkel doet de rest.
public static partial class PlanetCity
{
    // Het vak waar punt dir in ligt, met de waaierdriehoek k (midden, hoek k, hoek k+1) en de gewichten (b0 het midden, b1 hoek k,
    // b2 hoek k+1). Het dichtstbijzijnde midden is niet altijd het vak waar het punt in ligt (vakken zijn niet overal even groot),
    // daarom ook de buren proberen.
    static bool CellAt(Vector3 dir, out int i, out int k, out float b0, out float b1, out float b2)
    {
        int i0 = Planet.Nearest(dir * Planet.R);
        if (TriAt(i0, dir, out k, out b0, out b1, out b2)) { i = i0; return true; }
        foreach (int j in Planet.Cells[i0].nb) if (TriAt(j, dir, out k, out b0, out b1, out b2)) { i = j; return true; }
        i = i0; k = 0; b0 = 1f; b1 = b2 = 0f; return false;
    }
    static bool TriAt(int i, Vector3 dir, out int k, out float b0, out float b1, out float b2)
    {
        var c = Planet.Cells[i]; int m = c.corners.Length; Vector3 v = (dir - c.dir) * Planet.R; Vector2 q = new Vector2(Vector3.Dot(v, c.east), Vector3.Dot(v, c.north));
        for (k = 0; k < m; k++)
        {
            Vector2 L1 = c.local[k], L2 = c.local[(k + 1) % m]; float det = L1.x * L2.y - L1.y * L2.x; if (Mathf.Abs(det) < 1e-6f) continue;
            b1 = (q.x * L2.y - q.y * L2.x) / det; b2 = (L1.x * q.y - L1.y * q.x) / det; b0 = 1f - b1 - b2;
            if (b1 >= -0.02f && b2 >= -0.02f && b0 >= -0.02f) return true;
        }
        k = 0; b0 = 1f; b1 = b2 = 0f; return false;
    }
    // de hoogte van het maaiveld op punt dir (eenheidsvector), in meters boven de bol
    public static float H(Vector3 dir)
    {
        if (!CellAt(dir, out int i, out int k, out float b0, out float b1, out float b2)) return G(i);
        var su = segsUse; var fu = flatsUse; var cu = capsUse;   // H kiest zelf zijn stukken weg; wie bezig was (BuildGround) krijgt de zijne terug
        try { return H(dir, i, k, b0, b1, b2); } finally { segsUse = su; flatsUse = fu; capsUse = cu; }
    }
    // idem, voor een punt waarvan bekend is in welk vak en welke waaierdriehoek het ligt (de mesh). Dezelfde keuzes als het tekenen
    // van een vak in BuildGround, in dezelfde volgorde: tot de mesh uit H gebouwd wordt (stap 2) is dit de enige manier om gelijk te blijven.
    static float H(Vector3 dir, int i, int k, float b0, float b1, float b2)
    {
        if (fieldCells.Contains(i)) UseNear(i); else UseAll();   // eerst: dezelfde stukken weg als het tekenen van dit vak (UseNear: de buurt); ook FlatGround en StripCut kijken ernaar
        if (FlatGround(i)) return G(i);
        var c = Planet.Cells[i]; int m = c.corners.Length, k1 = (k + 1) % m; float g = G(i);
        if (Ground2)   // Terrein v2: dezelfde functie als het tekenen van het vak (Ground2Cell)
        {
            bool plat = Plateau(i), fld = fieldCells.Contains(i) && NearSeg(c.dir, c.radius); var nr = cuts.Count > 0 && !plat ? CutsNear(c) : null;
            float c2 = plat ? g : Center2(i), e1 = plat ? g : CornerBase(i, k), e2 = plat ? g : CornerBase(i, k1);
            return H2(dir, c2 * b0 + e1 * b1 + e2 * b2, fld, nr != null && nr.Count > 0);
        }
        // (BuildGround kijkt hier naar FlatGround, niet naar Plateau: een half wegvak of een strook langs een weg volgt de hoogtefunctie)
        // langs een weg op een niveau of een weg op een helling: het gewone terrein als vlak, de hoogtefunctie erover (LineGround)
        if (fieldCells.Contains(i) && NearSeg(c.dir, c.radius))
        {
            float hc0 = 0f; if (levelled.Contains(i)) { for (int q = 0; q < m; q++) hc0 += FieldBase(i, q); hc0 /= m; } else hc0 = g;
            hc0 += CenterOfs1(i);
            return GroundField(dir, hc0 * b0 + FieldBase(i, k) * b1 + FieldBase(i, k1) * b2);   // (met aan het eind de geulen)
        }
        // bij een geul of dijk in de buurt: het maaiveld zonder geul als vlak, de hoogste dijk erop, de laagste geul erin (ExactGround)
        var near = carved.Count > 0 || fillN > 0 ? CutsNear(c) : null;
        if (near != null && near.Count > 0)
        {
            float hcb = g; if (IsWalk(i) && !levelled.Contains(i)) { hcb = 0f; for (int q = 0; q < m; q++) hcb += CornerBase(i, q); hcb /= m; }
            float h = hcb * b0 + CornerBase(i, k) * b1 + CornerBase(i, k1) * b2;
            if (fillN > 0) h = Mathf.Max(h, Fill(dir)); if (carved.Count > 0) h = Mathf.Min(h, Carve(dir));
            return h;
        }
        // het gewone vak: het midden op G plus de eigen verschuiving (een weg- of padvak in een kom tot zijn laagste hoek), de hoeken zoals de buren ze hebben
        float hk1 = OwnCorner(i, k), hk2 = OwnCorner(i, k1), hc = g + CenterOfs1(i);
        if (IsWalk(i) && !carved.Contains(i) && !levelled.Contains(i)) for (int q = 0; q < m; q++) hc = Mathf.Min(hc, OwnCorner(i, q));
        if (cuts.Count > 0) hc = Mathf.Min(hc, Carve(c.dir));
        return hc * b0 + hk1 * b1 + hk2 * b2;
    }
    // voor de test: de tussenstappen van H op punt dir
    public static string HInfo(Vector3 dir)
    {
        bool ok = CellAt(dir, out int i, out int k, out float b0, out float b1, out float b2); var c = Planet.Cells[i]; int m = c.corners.Length, k1 = (k + 1) % m;
        var su = segsUse; var fu = flatsUse; var cu = capsUse; if (fieldCells.Contains(i)) UseNear(i); else UseAll();
        var sb = new System.Text.StringBuilder(); sb.Append("vak ").Append(i).Append(ok ? "" : " (niet gevonden)").Append(" k ").Append(k).Append(" b ").Append(b0.ToString("0.00")).Append('/').Append(b1.ToString("0.00")).Append('/').Append(b2.ToString("0.00"));
        sb.Append(" G ").Append(G(i).ToString("0.00")).Append(" vlak ").Append(FlatGround(i)).Append(" plateau ").Append(Plateau(i)).Append(" weg ").Append(IsWalk(i)).Append(" niveau ").Append(levelled.Contains(i)).Append(" geul ").Append(carved.Contains(i));
        sb.Append(" veld ").Append(fieldCells.Contains(i)).Append('/').Append(NearSeg(c.dir, c.radius)).Append(" geulen ").Append(carved.Count > 0 || fillN > 0 ? CutsNear(c)?.Count ?? 0 : 0);
        sb.Append(" veldbasis ").Append(FieldBase(i, k).ToString("0.00")).Append('/').Append(FieldBase(i, k1).ToString("0.00")).Append(" hoeken ").Append(OwnCorner(i, k).ToString("0.00")).Append('/').Append(OwnCorner(i, k1).ToString("0.00"));
        sb.Append(" H ").Append(H(dir).ToString("0.00"));
        segsUse = su; flatsUse = fu; capsUse = cu; return sb.ToString();
    }

    // ---------- stap 2: de bodem uit H, met gedeelde randpunten per vakrand ----------
    public static bool Ground2;   // schakelaar Terrein v2 (ontwikkelaarsmodus): de bodem van elk vak uit de hoogtefunctie, de randen gedeeld met de buren

    // De hoogte van het maaiveld (v2) op punt dir met de gewone terreinhoogte basis daar: de hoogtefunctie langs wegen (alleen in de
    // vakken binnen bereik, field; daarbuiten doet ze toch niets), de hoogste dijk erop, de laagste geul erin (alleen bij vakken waar
    // een geul of dijk in de buurt komt, cutsNear). Dezelfde regels voor elk punt, in welk vak het ook ligt: zo krijgen twee buren op
    // hun gedeelde rand dezelfde hoogte.
    static float H2(Vector3 dir, float basis, bool field, bool cutsNear) => H2(dir, basis, field, cutsNear, out _);
    static float H2(Vector3 dir, float basis, bool field, bool cutsNear, out bool slope)
    {
        slope = false; float h = field ? GroundField(dir, basis, out slope) : basis;
        if (cutsNear) { if (fillN > 0) h = Mathf.Max(h, Fill(dir)); if (carved.Count > 0) h = Mathf.Min(h, Carve(dir)); }
        return h;
    }
    // het midden van het gewone terrein van een vak zonder plateau: G plus de eigen verschuiving; een vak op een niveau het gemiddelde
    // van zijn hoeken; een weg- of padvak op het maaiveld in een kom tot zijn laagste hoek (het maaiveld steekt dan nooit door de weg)
    static float Center2(int i)
    {
        var c = Planet.Cells[i]; int m = c.corners.Length;
        if (levelled.Contains(i)) { float s = 0f; for (int k = 0; k < m; k++) s += CornerBase(i, k); return s / m + CenterOfs1(i); }
        float hc = G(i) + CenterOfs1(i);
        if (IsWalk(i) && !carved.Contains(i)) for (int k = 0; k < m; k++) hc = Mathf.Min(hc, CornerBase(i, k));
        return hc;
    }
    // Een randpunt: t langs de rand (0 bij hoek k, 1 bij hoek k+1) en de hoogte van het maaiveld daar
    struct EPt { public float t, h; }
    static readonly Dictionary<long, (long key, List<EPt> pts)> edgeCache = new Dictionary<long, (long, List<EPt>)>();
    static long EdgeKey(int i, int j, int k)
    {
        long h = 1469598103934665603L; void F(float f) { h = unchecked((h ^ (uint)System.BitConverter.SingleToInt32Bits(f)) * 1099511628211L); }
        var c = Planet.Cells[i]; int m = c.corners.Length;
        F(fieldVer); F(cutsHash); F(FieldK); F((RoadShape ? 1f : 0f) + (RoadSlopes ? 2f : 0f)); F(Mathf.Min(G(i), G(j))); F(Mathf.Max(G(i), G(j))); float c1 = CornerBase(i, k), c2 = CornerBase(i, (k + 1) % m); F(Mathf.Min(c1, c2)); F(Mathf.Max(c1, c2)); F(spans.Count); F(cuts.Count);   // (symmetrisch: van beide kanten dezelfde sleutel)
        return h;
    }
    // De randpunten van rand k van vak i, gedeeld met de buur aan die kant: de hoogte van het maaiveld langs de rand als polylijn,
    // met een punt op elke knik (de snijlijnen van de hoogtefunctie en van de geulen) en waar hij daartussen nog kromt. Het vak met
    // het laagste nummer rekent ze uit, in zijn eigen vlakke coördinaten; de buur krijgt dezelfde lijst (omgekeerd). Zo tekenen
    // beide vakken precies dezelfde rand en is er geen kier en geen naad meer nodig.
    static List<EPt> EdgeProfile(int i, int k)
    {
        Topo(); var c = Planet.Cells[i]; int m = c.corners.Length, k1 = (k + 1) % m, j = edN[i][k];
        bool own = i <= j; int a = own ? i : j;
        long id = ((long)Mathf.Min(i, j) << 32) | (uint)Mathf.Max(i, j); long key = EdgeKey(i, j, k);
        if (!edgeCache.TryGetValue(id, out var e) || e.key != key)
        {
            int ka = k;
            if (!own) { ka = -1; var ca = Planet.Cells[a]; for (int q = 0; q < ca.corners.Length; q++) if (edN[a][q] == i) { ka = q; break; } if (ka < 0) { own = true; a = i; ka = k; } }
            var su = segsUse; var fu = flatsUse; var cu = capsUse;
            try { e = (key, EdgeProfile0(a, ka)); } finally { segsUse = su; flatsUse = fu; capsUse = cu; }
            edgeCache[id] = e;
        }
        if (own) return e.pts;
        // de buur loopt de rand andersom (zelfde hoekpunten, de volgorde omgekeerd); voor de zekerheid aan de hoekpunten gemeten
        var ca2 = Planet.Cells[a]; int ka2 = 0; for (int q = 0; q < ca2.corners.Length; q++) if (edN[a][q] == i) { ka2 = q; break; }
        bool rev = Vector3.Dot(ca2.corners[ka2], c.corners[k]) < Vector3.Dot(ca2.corners[ka2], c.corners[k1]);
        if (!rev) return e.pts;
        var r = new List<EPt>(e.pts.Count); for (int q = e.pts.Count - 1; q >= 0; q--) r.Add(new EPt { t = 1f - e.pts[q].t, h = e.pts[q].h }); return r;
    }
    static List<EPt> EdgeProfile0(int a, int ka)
    {
        var c = Planet.Cells[a]; int m = c.corners.Length, k1 = (ka + 1) % m; Vector3 A = c.corners[ka], B = c.corners[k1]; Vector2 L1 = c.local[ka], L2 = c.local[k1];
        bool field = fieldCells.Contains(a); if (field) UseNear(a); else UseAll();
        float bA = CornerBase(a, ka), bB = CornerBase(a, k1), hc0 = Plateau(a) ? G(a) : Center2(a);
        var near = cuts.Count > 0 ? CutsNear(c) : null; bool cn = near != null && near.Count > 0;
        var ts = new List<float> { 0f, 1f };
        void Cross(Lin f) { float f1 = f.At(L1), f2 = f.At(L2); if ((f1 < 0f) == (f2 < 0f) || Mathf.Abs(f1 - f2) < 1e-6f) return; float t = f1 / (f1 - f2); if (t > 1e-3f && t < 1f - 1e-3f) ts.Add(t); }
        if (field && NearSeg(c.dir, c.radius)) foreach (var f in FieldLines(c, ka, hc0, bA, bB, new List<Seg>(), new List<Cap>())) Cross(f);
        if (cn) foreach (var n in near) foreach (var f in new[] { n.sd - Lin.C(-0.01f), n.sd - Lin.C(n.fl), n.sd - Lin.C(n.fl + n.rl), n.sd - Lin.C(n.end), n.t - Lin.C(n.q.hw), n.t - Lin.C(-n.q.hw) }) Cross(f);
        ts.Sort(); var pts = new List<EPt>(); float len = (L2 - L1).magnitude;
        float HAt(float t) => H2(Vector3.Lerp(A, B, t).normalized, Mathf.Lerp(bA, bB, t), field, cn);
        foreach (float t in ts) { if (pts.Count > 0 && t - pts[pts.Count - 1].t < 2e-4f) continue; pts.Add(new EPt { t = t, h = HAt(t) }); }
        // verfijnen waar de polylijn meer dan 3 cm van de functie afwijkt (een kop is rond, twee taluds kruisen elkaar), tot stukjes van 30 cm
        for (int v = 0; v + 1 < pts.Count; )
        {
            float t0 = pts[v].t, t1 = pts[v + 1].t;
            if ((t1 - t0) * len > 0.3f) { float tm = (t0 + t1) * 0.5f, hm = HAt(tm); if (Mathf.Abs(hm - (pts[v].h + pts[v + 1].h) * 0.5f) > 0.03f) { pts.Insert(v + 1, new EPt { t = tm, h = hm }); continue; } }
            v++;
        }
        return pts;
    }
    static float ProfileAt(List<EPt> pts, float t)
    {
        if (t <= pts[0].t) return pts[0].h; for (int v = 1; v < pts.Count; v++) if (t <= pts[v].t) { float d = pts[v].t - pts[v - 1].t; return d < 1e-6f ? pts[v].h : Mathf.Lerp(pts[v - 1].h, pts[v].h, (t - pts[v - 1].t) / d); }
        return pts[pts.Count - 1].h;
    }
    // Vak i tekenen (v2): per waaierdriehoek de veelhoeken langs de snijlijnen van de hoogtefunctie en de geulen (zoals LineGround en
    // ExactGround), de gedeelde randpunten erin (op de rand komt de hoogte uit de polylijn, niet uit de functie: zo is de rand van twee
    // kanten gelijk), de rest uit H2, en waar het binnenin nog kromt verfijnd via het midden (geen nieuwe punten op de rand).
    // plat: een bebouwd vak (plateau op G; alleen de hoogtefunctie erover, de sokkel doet de rest); skip: driehoekjes onder een brugdek
    static void Ground2Cell(int i, bool plat, System.Action<Vector3, Vector3, Vector3, float, float, float, bool> emit, System.Func<int, bool> skip)
    {
        var c = Planet.Cells[i]; int m = c.corners.Length; float g = G(i);
        if (FlatGround(i)) { Vector3 mid = c.dir * (Planet.R + g); for (int k = 0; k < m; k++) { if (skip(k)) continue; emit(mid, c.corners[k] * (Planet.R + g), c.corners[(k + 1) % m] * (Planet.R + g), g, g, g, false); } return; }
        bool field = fieldCells.Contains(i) && NearSeg(c.dir, c.radius);
        var near = cuts.Count > 0 && !plat ? CutsNear(c) : null; bool cn = near != null && near.Count > 0;
        float hc0 = plat ? g : Center2(i);
        for (int k = 0; k < m; k++)
        {
            if (skip(k)) continue; int k1 = (k + 1) % m;
            float h1 = plat ? g : CornerBase(i, k), h2 = plat ? g : CornerBase(i, k1);
            Vector2 L1 = c.local[k], L2 = c.local[k1]; float det = L1.x * L2.y - L1.y * L2.x; if (Mathf.Abs(det) < 1e-6f) continue;
            Vector3 D0 = c.dir, D1 = c.corners[k], D2 = c.corners[k1];
            var polys = new List<List<Vector2>> { new List<Vector2> { Vector2.zero, L1, L2 } };
            void Split(Lin f) { var next = new List<List<Vector2>>(); foreach (var pl in polys) if (!SplitBy(pl, f, next)) next.Add(pl); polys = next; }
            if (field) foreach (var f in FieldLines(c, k, hc0, h1, h2, new List<Seg>(), new List<Cap>())) Split(f);
            if (cn) foreach (var n in near) foreach (var f in new[] { n.sd - Lin.C(-0.01f), n.sd - Lin.C(n.fl), n.sd - Lin.C(n.fl + n.rl), n.sd - Lin.C(n.end), n.t - Lin.C(n.q.hw), n.t - Lin.C(-n.q.hw) }) Split(f);
            var prof = plat ? null : EdgeProfile(i, k);
            Vector2 E = L2 - L1; float el2 = E.sqrMagnitude, el = Mathf.Sqrt(el2);
            float TOf(Vector2 q) => Vector2.Dot(q - L1, E) / el2;
            bool OnEdge(Vector2 q) => Mathf.Abs((E.x * (q.y - L1.y) - E.y * (q.x - L1.x)) / el) < 1e-3f;
            Vector3 Dir(Vector2 q) { float b1 = (q.x * L2.y - q.y * L2.x) / det, b2 = (L1.x * q.y - L1.y * q.x) / det; return (D0 * (1f - b1 - b2) + D1 * b1 + D2 * b2).normalized; }
            float Base(Vector2 q) { float b1 = (q.x * L2.y - q.y * L2.x) / det, b2 = (L1.x * q.y - L1.y * q.x) / det; return hc0 * (1f - b1 - b2) + h1 * b1 + h2 * b2; }
            foreach (var pl0 in polys)
            {
                // de hoekpunten: op de rand (t >= 0) of binnenin (t < 0); de gedeelde randpunten tussen twee randhoekpunten erbij, en een
                // randhoekpunt vlak bij een gedeeld punt (binnen 1,5 cm) daarop gezet
                var pl = new List<(Vector2 q, float t)>(); int n0 = pl0.Count;
                for (int v = 0; v < n0; v++)
                {
                    Vector2 q = pl0[v]; bool on = prof != null && OnEdge(q); float t = on ? Mathf.Clamp01(TOf(q)) : -1f;
                    if (on) foreach (var ep in prof) if (Mathf.Abs(ep.t - t) * el < 0.015f) { t = ep.t; break; }
                    pl.Add((q, t));
                    if (!on) continue; Vector2 qn = pl0[(v + 1) % n0]; if (!OnEdge(qn)) continue;
                    float tn = Mathf.Clamp01(TOf(qn)); foreach (var ep in prof) if (Mathf.Abs(ep.t - tn) * el < 0.015f) { tn = ep.t; break; }
                    if (tn > t) { foreach (var ep in prof) if (ep.t > t + 1e-6f && ep.t < tn - 1e-6f) pl.Add((L1 + E * ep.t, ep.t)); }
                    else for (int w = prof.Count - 1; w >= 0; w--) { var ep = prof[w]; if (ep.t < t - 1e-6f && ep.t > tn + 1e-6f) pl.Add((L1 + E * ep.t, ep.t)); }
                }
                int n = pl.Count; var P = new Vector3[n]; var H = new float[n];
                for (int v = 0; v < n; v++)
                {
                    if (pl[v].t >= 0f) { Vector3 d = Vector3.Lerp(D1, D2, pl[v].t).normalized; H[v] = ProfileAt(prof, pl[v].t); P[v] = d * (Planet.R + H[v]); }
                    else { Vector3 d = Dir(pl[v].q); H[v] = H2(d, Base(pl[v].q), field, cn); P[v] = d * (Planet.R + H[v]); }
                }
                int r = 0; for (int v = 0; v < n; v++) if (pl[v].t < 0f) { r = v; break; }   // waaieren vanuit een punt dat niet op de rand ligt (anders platte driehoekjes langs de rand)
                for (int v = 1; v + 1 < n; v++) { int a = (r + v) % n, b = (r + v + 1) % n; Refine2(pl[r].q, pl[a].q, pl[b].q, P[r], P[a], P[b], H[r], H[a], H[b], 0); }
            }
            void Refine2(Vector2 a, Vector2 b, Vector2 cc, Vector3 Pa, Vector3 Pb, Vector3 Pc, float ha, float hb, float hc2, int depth)
            {
                Vector2 m2 = (a + b + cc) / 3f; Vector3 dm = Dir(m2); float hm = H2(dm, Base(m2), field, cn, out bool steep);
                if (depth < 3 && Mathf.Abs(hm - (ha + hb + hc2) / 3f) > 0.06f && (b - a).sqrMagnitude + (cc - a).sqrMagnitude > 0.5f)
                {
                    Vector3 Pm = dm * (Planet.R + hm);
                    Refine2(a, b, m2, Pa, Pb, Pm, ha, hb, hm, depth + 1); Refine2(b, cc, m2, Pb, Pc, Pm, hb, hc2, hm, depth + 1); Refine2(cc, a, m2, Pc, Pa, Pm, hc2, ha, hm, depth + 1);
                    return;
                }
                emit(Pa, Pb, Pc, ha, hb, hc2, steep);
            }
        }
    }

    // Voor de test: kloppen de randpunten? Per vak zonder plateau: de hoogte van hoek k uit rand k (t = 0) en uit rand k-1 (t = 1) moet
    // gelijk zijn, en de lijst van de buur moet dezelfde zijn (omgekeerd). Geeft het aantal afwijkingen en de eerste paar.
    public static string Ground2Check(IEnumerable<int> cells)
    {
        int nc = 0, nn = 0, n = 0; var sb = new System.Text.StringBuilder();
        foreach (int i in cells)
        {
            if (Plateau(i) || !Planet.Cells.Length.Equals(Planet.Cells.Length)) continue; var c = Planet.Cells[i]; int m = c.corners.Length; n++;
            for (int k = 0; k < m; k++)
            {
                var pk = EdgeProfile(i, k); var pp = EdgeProfile(i, (k + m - 1) % m);
                float ha = pk[0].h, hb = pp[pp.Count - 1].h;
                if (Mathf.Abs(ha - hb) > 0.002f) { nc++; if (nc <= 4) sb.Append("  hoek ").Append(i).Append('/').Append(k).Append(": rand ").Append(k).Append(' ').Append(ha.ToString("0.00")).Append(" rand ").Append((k + m - 1) % m).Append(' ').Append(hb.ToString("0.00")).Append(" (eigenaar ").Append(Mathf.Min(i, edN[i][k])).Append('/').Append(Mathf.Min(i, edN[i][(k + m - 1) % m])).Append(')').AppendLine(); }
                int j = edN[i][k]; if (Plateau(j)) continue; int kj = -1; for (int q = 0; q < Planet.Cells[j].corners.Length; q++) if (edN[j][q] == i) kj = q; if (kj < 0) continue;
                var pj = EdgeProfile(j, kj); bool same = pj.Count == pk.Count; if (same) for (int q = 0; q < pk.Count; q++) if (Mathf.Abs(pj[pj.Count - 1 - q].h - pk[q].h) > 0.002f || Mathf.Abs(1f - pj[pj.Count - 1 - q].t - pk[q].t) > 1e-4f) same = false;
                if (!same) { nn++; if (nn <= 4) sb.Append("  rand ").Append(i).Append('/').Append(k).Append(" en ").Append(j).Append('/').Append(kj).Append(": ").Append(pk.Count).Append(" en ").Append(pj.Count).Append(" punten").AppendLine(); }
            }
        }
        return "randpunten: " + n + " vakken, " + nc + " hoeken ongelijk, " + nn + " randen ongelijk met de buur" + (sb.Length > 0 ? "\n" + sb : "");
    }
}
