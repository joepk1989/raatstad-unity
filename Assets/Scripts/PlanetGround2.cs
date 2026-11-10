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
            Vector3 V0 = c.dir * (Planet.R + c2), V1 = c.corners[k] * (Planet.R + e1), V2 = c.corners[k1] * (Planet.R + e2), Nrm = Vector3.Cross(V1 - V0, V2 - V0); float dn = Vector3.Dot(dir, Nrm);
            float basis = Mathf.Abs(dn) < 1e-9f ? c2 : Vector3.Dot(V0, Nrm) / dn - Planet.R;   // het vlak door de drie hoekpunten, zoals de mesh
            return H2(dir, basis, fld, nr != null && nr.Count > 0);
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
    public static bool Ground2Merge = true;   // stukken met hetzelfde vlak samenvoegen (ontwerp 8.5 punt 3); uit: om te vergelijken
    public static long Ground2Tris;           // teller: driehoeken die Ground2Cell tekende (voor de test)
    // tijdmeting (voor de test): vakken met en zonder hoogtefunctie, randprofielen (opnieuw berekend of uit het geheugen)
    public static long G2Cells, G2Field, G2EdgeNew, G2EdgeHit; public static readonly System.Diagnostics.Stopwatch G2SwField = new System.Diagnostics.Stopwatch(), G2SwPlain = new System.Diagnostics.Stopwatch(), G2SwEdge = new System.Diagnostics.Stopwatch();
    public static string G2Stats() => G2Cells + " vakken (" + G2Field + " met hoogtefunctie): " + G2SwField.ElapsedMilliseconds + " ms met, " + G2SwPlain.ElapsedMilliseconds + " ms zonder; randprofielen " + G2EdgeNew + " berekend (" + G2SwEdge.ElapsedMilliseconds + " ms), " + G2EdgeHit + " uit het geheugen";
    public static void G2Reset() { G2Cells = G2Field = G2EdgeNew = G2EdgeHit = 0; G2SwField.Reset(); G2SwPlain.Reset(); G2SwEdge.Reset(); }
    public static bool Ground2 = true;   // Terrein v2 (standaard aan; uit in de ontwikkelaarsmodus, voor die speelsessie): de bodem van elk vak uit de hoogtefunctie, de randen gedeeld met de buren, wegen met een profiel (RoadH)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Ground2Default() { Ground2 = true; }   // bij elke start van Play weer aan (statische velden overleven een herstart zonder domein-herladen)

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
        if (RoadH.Count > 0 && IsWalk(i)) return Terrain(i) + CenterOfs1(i);   // nieuwe opbouw: het terrein onder de weg; de weg zelf komt uit de hoogtefunctie
        float hc = G(i) + CenterOfs1(i);
        if (IsWalk(i) && !carved.Contains(i)) for (int k = 0; k < m; k++) hc = Mathf.Min(hc, CornerBase(i, k));
        return hc;
    }
    // Een randpunt: t langs de rand (0 bij hoek k, 1 bij hoek k+1) en de hoogte van het maaiveld daar
    // side: bij een wand staan er twee punten op dezelfde t, side +1 het punt aan de kant van de strook (de binnenkant), -1 aan de kant van het terrein
    struct EPt { public float t, h; public int side; }
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
            bool run = G2SwEdge.IsRunning; G2SwEdge.Start(); G2EdgeNew++;
            try { e = (key, EdgeProfile0(a, ka)); } finally { segsUse = su; flatsUse = fu; capsUse = cu; if (!run) G2SwEdge.Stop(); }
            edgeCache[id] = e;
        }
        else G2EdgeHit++;
        if (own) return e.pts;
        // de buur loopt de rand andersom (zelfde hoekpunten, de volgorde omgekeerd); voor de zekerheid aan de hoekpunten gemeten
        var ca2 = Planet.Cells[a]; int ka2 = 0; for (int q = 0; q < ca2.corners.Length; q++) if (edN[a][q] == i) { ka2 = q; break; }
        bool rev = Vector3.Dot(ca2.corners[ka2], c.corners[k]) < Vector3.Dot(ca2.corners[ka2], c.corners[k1]);
        if (!rev) return e.pts;
        var r = new List<EPt>(e.pts.Count); for (int q = e.pts.Count - 1; q >= 0; q--) r.Add(new EPt { t = 1f - e.pts[q].t, h = e.pts[q].h, side = e.pts[q].side }); return r;
    }
    static List<EPt> EdgeProfile0(int a, int ka)
    {
        var c = Planet.Cells[a]; int m = c.corners.Length, k1 = (ka + 1) % m; Vector3 A = c.corners[ka], B = c.corners[k1]; Vector2 L1 = c.local[ka], L2 = c.local[k1];
        bool field = fieldCells.Contains(a); if (field) UseNear(a); else UseAll();
        float bA = CornerBase(a, ka), bB = CornerBase(a, k1), hc0 = Plateau(a) ? G(a) : Center2(a);
        var near = cuts.Count > 0 ? CutsNear(c) : null; bool cn = near != null && near.Count > 0;
        var ts = new List<float> { 0f, 1f };
        void Cross(Lin f) { float f1 = f.At(L1), f2 = f.At(L2); if ((f1 < 0f) == (f2 < 0f) || Mathf.Abs(f1 - f2) < 1e-6f) return; float t = f1 / (f1 - f2); if (t > 1e-3f && t < 1f - 1e-3f) ts.Add(t); }
        var ls = new List<Seg>(); var lc = new List<Cap>(); bool fld = field && NearSeg(c.dir, c.radius);
        if (fld) foreach (var f in FieldLines(c, ka, hc0, bA, bB, ls, lc)) Cross(f);
        if (cn) foreach (var n in near) foreach (var f in new[] { n.sd - Lin.C(-0.01f), n.sd - Lin.C(n.fl), n.sd - Lin.C(n.fl + n.rl), n.sd - Lin.C(n.end), n.t - Lin.C(n.q.hw), n.t - Lin.C(-n.q.hw) }) Cross(f);
        var spI = RoadH.Count == 0 && spanNear.Count > 0 && spanNear.TryGetValue(a, out var spl) ? spl : null; foreach (var f in SpanLines(c, spI)) Cross(f);   // (nieuwe opbouw: het terrein onder een dek blijft liggen, SpanBottom staat uit: geen deklijnen in de grond)
        ts.Sort();
        {   // de snijlijnen van de vlakken die in elk stuk gelden (gemeten in het midden van het stuk), daar de snijpunten met de rand bij
            float det0 = L1.x * L2.y - L1.y * L2.x; Lin BL = new Lin { a = hc0, x = ((bA - hc0) * L2.y - (bB - hc0) * L1.y) / det0, y = ((bB - hc0) * L1.x - (bA - hc0) * L2.x) / det0 };
            var extra = new List<float>();
            for (int v = 0; v + 1 < ts.Count; v++)
            {
                if (ts[v + 1] - ts[v] < 1e-3f) continue; Vector2 pm = Vector2.Lerp(L1, L2, (ts[v] + ts[v + 1]) * 0.5f);
                var lins = PlaneLins(c, BL, ls, lc, cn ? near : null, spI, pm);
                for (int x = 0; x < lins.Count; x++) for (int y = x + 1; y < lins.Count; y++)
                {
                    Lin f = lins[x] - lins[y]; float f1 = f.At(L1), f2 = f.At(L2); if ((f1 < 0f) == (f2 < 0f) || Mathf.Abs(f1 - f2) < 1e-6f) continue;
                    float t = f1 / (f1 - f2); if (t > ts[v] + 1e-3f && t < ts[v + 1] - 1e-3f) extra.Add(t);
                }
            }
            ts.AddRange(extra); ts.Sort();
        }
        var pts = new List<EPt>(); float len = (L2 - L1).magnitude;
        Vector3 PA = A * (Planet.R + bA), PB = B * (Planet.R + bB);   // de randpunten liggen op de koorde tussen de twee hoekpunten (zoals in de mesh)
        float HAt(float t) { Vector3 P = Vector3.Lerp(PA, PB, t); return H2(P.normalized, P.magnitude - Planet.R, field, cn); }
        float HAt2(float t, float bias) { float ob = wallBias; wallBias = bias; try { return HAt(t); } finally { wallBias = ob; } }
        // De wanden: de wandlijnen zijn vlak en de hoogtefunctie leeft op de bol, dus de sprong ligt millimeters naast de snijlijn; de echte
        // sprong zoeken (bisectie binnen 4 cm) en daar twee punten op dezelfde t zetten, eerst de kant van de lagere t (ontwerp 8.5 punt 4)
        var wallT = new List<(float t, float h0, float h1, int side0)>();
        if (fld) foreach (var w in WallLins(c, ls, lc))
        {
            float f1 = w.At(L1), f2 = w.At(L2); if ((f1 < 0f) == (f2 < 0f) || Mathf.Abs(f1 - f2) < 1e-6f) continue; float tw = f1 / (f1 - f2);
            float sinA = Mathf.Abs(w.x * (L2.y - L1.y) - w.y * (L2.x - L1.x)) / (Mathf.Sqrt(w.x * w.x + w.y * w.y) * len), dq = Mathf.Min(0.5f, 0.04f / Mathf.Max(sinA, 0.08f)) / len;   // (een lijn die de rand scheef kruist: de sprong ligt langs de rand verder van de snijlijn, tot 50 cm)
            float lo = Mathf.Max(0f, tw - dq), hi = Mathf.Min(1f, tw + dq); if (hi - lo < 1e-6f) continue;
            float d0 = HAt(lo), d1 = HAt(hi); if (Mathf.Abs(d1 - d0) < 0.2f) continue; float mid = (d0 + d1) * 0.5f;
            for (int it = 0; it < 20; it++) { float m2 = (lo + hi) * 0.5f; if ((HAt(m2) > mid) == (d1 > d0)) hi = m2; else lo = m2; }
            float ts2 = (lo + hi) * 0.5f; if (ts2 < 1e-3f || ts2 > 1f - 1e-3f) continue;
            float hA = HAt2(ts2, 1e-3f), hB = HAt2(ts2, -1e-3f); if (Mathf.Abs(hA - hB) < 1e-3f) continue;   // (de twee kanten; welke eerst komt zegt d0)
            bool inFirst = Mathf.Abs(d0 - hA) < Mathf.Abs(d0 - hB);
            ts.RemoveAll(t => t > 1e-3f && t < 1f - 1e-3f && Mathf.Abs(t - ts2) < dq * 1.1f); ts.Add(ts2); wallT.Add((ts2, inFirst ? hA : hB, inFirst ? hB : hA, inFirst ? 1 : -1));
        }
        ts.Sort();
        foreach (float t in ts)
        {
            if (pts.Count > 0 && t - pts[pts.Count - 1].t < 2e-4f) continue;
            int wi = wallT.FindIndex(x => x.t == t);
            if (wi < 0) { pts.Add(new EPt { t = t, h = HAt(t) }); continue; }
            var wp = wallT[wi]; pts.Add(new EPt { t = t, h = wp.h0, side = wp.side0 }); pts.Add(new EPt { t = t, h = wp.h1, side = -wp.side0 });
        }
        // de knikken die de snijlijnen niet geven (twee taluds die elkaar kruisen, een talud tegen een geul): zoeken (Kinks)
        var all = new List<EPt>();
        for (int v = 0; v + 1 < pts.Count; v++)
        {
            all.Add(pts[v]); var ks = new List<(float t, bool step)>(); Kinks(HAt, pts[v].h, pts[v + 1].h, pts[v].t, pts[v + 1].t, len, ks, 0); ks.Sort((x, y) => x.t.CompareTo(y.t));
            float dq = 0.01f / len;
            foreach (var kk in ks) { if (kk.step) { all.Add(new EPt { t = kk.t - dq, h = HAt(kk.t - dq) }); all.Add(new EPt { t = kk.t + dq, h = HAt(kk.t + dq) }); } else all.Add(new EPt { t = kk.t, h = HAt(kk.t) }); }
        }
        all.Add(pts[pts.Count - 1]);
        // Punten waar de rand niet knikt weg (een snijlijn die de rand kruist zonder dat de hoogte er verandert): elk randpunt wordt in
        // beide buren een hoekpunt met een driehoek naar het midden, dus een waaier van dunne driehoeken in vlak gras. Een punt mag weg
        // als het (en alle punten die sinds het laatste behouden punt al weg zijn) binnen 3 mm van de rechte lijn naar het volgende ligt;
        // een wandpaar blijft.
        if (all.Count > 2)
        {
            var kept = new List<EPt> { all[0] }; int last = 0;
            for (int v = 1; v + 1 < all.Count; v++)
            {
                bool drop = all[v].side == 0 && all[v + 1].t - all[last].t > 1e-6f;
                for (int w = last + 1; w <= v && drop; w++) { float hl = Mathf.Lerp(all[last].h, all[v + 1].h, (all[w].t - all[last].t) / (all[v + 1].t - all[last].t)); if (Mathf.Abs(hl - all[w].h) > 0.003f) drop = false; }
                if (!drop) { kept.Add(all[v]); last = v; }
            }
            kept.Add(all[all.Count - 1]); all = kept;
        }
        return all;
    }
    // De knikken van f op het stuk van t0 tot t1 (f(t0) = f0, f(t1) = f1; len: de lengte van het hele stuk in meters). De hoogtefunctie
    // is stuksgewijs vlak, dus langs een lijn stuksgewijs lineair: waar ze van de rechte lijn tussen de einden afwijkt zit een knik, en de
    // afwijking is daar het grootst (ternair zoeken). Dan aan beide kanten verder, tot er geen afwijking meer is (2 cm) of stukjes van 10 cm.
    // Een sprong (step: een wand, meer dan 15 cm verschil binnen 4 cm) wordt apart gemerkt: daar hoort een punt aan elke kant van de wand.
    static void Kinks(System.Func<float, float> f, float f0, float f1, float t0, float t1, float len, List<(float t, bool step)> outT, int depth)
    {
        if (depth > 5 || (t1 - t0) * len < 0.1f) return;
        float D(float t) => f(t) - Mathf.Lerp(f0, f1, (t - t0) / (t1 - t0));
        bool off = false; for (int q = 1; q < 4 && !off; q++) if (Mathf.Abs(D(t0 + (t1 - t0) * q * 0.25f)) > 0.02f) off = true;
        if (!off) return;
        float lo = t0, hi = t1;
        for (int it = 0; it < 26; it++) { float a = lo + (hi - lo) / 3f, b = hi - (hi - lo) / 3f; if (Mathf.Abs(D(a)) < Mathf.Abs(D(b))) lo = a; else hi = b; }
        float tk = (lo + hi) * 0.5f; if (tk - t0 < 1e-4f || t1 - tk < 1e-4f) return;
        float dq = 0.02f / len, fa = f(Mathf.Max(t0, tk - dq)), fb = f(Mathf.Min(t1, tk + dq)); bool step = Mathf.Abs(fb - fa) > 0.15f;
        outT.Add((tk, step && RoadH.Count == 0));   // zonder profiel (het spel nu): een punt aan elke kant van een sprong, anders wordt een klif op het bereik een scheve flap (melding 9). Met profiel komen de wanden uit de paren (EdgeProfile0) en is een sprong hier een fout die de meting moet laten zien
        Kinks(f, f0, fa, t0, Mathf.Max(t0, tk - dq), len, outT, depth + 1); Kinks(f, fb, f1, Mathf.Min(t1, tk + dq), t1, len, outT, depth + 1);
    }
    // De hoogtefunctie is een min/max van vlakken (hoogte als lineaire functie van de vlakke coördinaten): het basisterrein, per stuk
    // weg het wegdek en het talud aan de kant waar p ligt (omhoog en omlaag), per kop idem met de zijde die bij p geldt, de vlakke
    // stukken, de geulen en dijken (CutLin), het dek van een dalbrug. Waar twee van die vlakken elkaar snijden knikt de functie: die
    // lijnen zijn de snijlijnen die de losse lijnen (FieldLines) niet geven. Alleen de vlakken die bij p gelden (de zijkanten zijn al
    // gesneden langs T = ±hw, de graten van een kop en de randen van een geul, dus binnen één stuk verandert dat niet).
    static List<Lin> PlaneLins(Planet.Cell c, Lin B, List<Seg> ls, List<Cap> lc, List<NearCut> near, List<int> spanIdx, Vector2 p)
    {
        var o3 = c.dir * Planet.R; var res = new List<Lin> { B };
        foreach (var q in ls)
        {
            Vector3 ab = q.b - q.a; float len = ab.magnitude; if (len < 1e-3f) continue;
            Vector3 along = Tan(ab, c.dir), across = Vector3.Cross(c.dir, along).normalized;
            Lin T = new Lin { a = Vector3.Dot(o3 - q.a, across), x = Vector3.Dot(c.east, across), y = Vector3.Dot(c.north, across) };
            Lin S = new Lin { a = Vector3.Dot(o3 - q.a, along), x = Vector3.Dot(c.east, along), y = Vector3.Dot(c.north, along) };
            float sp = S.At(p); Lin hr = sp <= 0f ? Lin.C(q.ha) : sp >= len ? Lin.C(q.hb) : Lin.C(q.ha) + S * ((q.hb - q.ha) / len);
            float tp = T.At(p);
            if (q.wall)   // sokkel of bak: binnen de wand de strook op weghoogte, erbuiten niets (de wand zelf is geen vlak)
            {
                float wo = WallOut; if (q.tap0) wo = Mathf.Min(wo, sp); if (q.tap1) wo = Mathf.Min(wo, len - sp);
                if (Mathf.Abs(tp) > q.hw + wo || sp < -EndSlack || sp > len + EndSlack) continue; res.Add(hr - Lin.C(0.05f)); continue;
            }
            Lin side = tp > q.hw ? (T - Lin.C(q.hw)) * FK : tp < -q.hw ? (Lin.C(-q.hw) - T) * FK : Lin.C(0f);
            if (RoadH.Count > 0) { float ov = Mathf.Max(0f, Mathf.Max(-sp, sp - len)); bool hard = sp < 0f ? q.hardA : q.hardB; if (ov > 0f && !hard && ov > Mathf.Abs(tp) - q.hw) side = (sp < 0f ? Lin.C(0f) - S : S - Lin.C(len)) * FK; }   // voorbij het eind: de kegel in de lengterichting (niet aan een hard eind)
            res.Add(hr - side - Lin.C(0.05f)); res.Add(hr + side - Lin.C(0.05f));
        }
        foreach (var q in lc)
        {
            Lin best = Lin.C(float.MinValue); float bd = float.MinValue;
            foreach (var n in q.n) { Lin N = new Lin { a = Vector3.Dot(o3 - q.c, n), x = Vector3.Dot(c.east, n), y = Vector3.Dot(c.north, n) }; float d = N.At(p); if (d > bd) { bd = d; best = N; } }
            Lin HC = new Lin { a = q.h + Vector3.Dot(o3 - q.c, q.g), x = Vector3.Dot(c.east, q.g), y = Vector3.Dot(c.north, q.g) };   // het vlak van de kop (schuin bij een kruispunt)
            if (q.wall) { if (bd > q.hw + q.wo) continue; res.Add(HC - Lin.C(0.05f)); continue; }   // sokkel- of bakkop: binnen de wand vlak, erbuiten niets
            Lin side = bd > q.hw ? (best - Lin.C(q.hw)) * FK : Lin.C(0f);
            res.Add(HC - side - Lin.C(0.05f)); res.Add(HC + side - Lin.C(0.05f));
        }
        if (RoadShape) foreach (var q in flatsUse)
        {
            Vector3 ab = q.b - q.a; float len = ab.magnitude; if (len < 1e-3f) continue;
            Vector3 along = Tan(ab, c.dir), across = Vector3.Cross(c.dir, along).normalized;
            Lin T = new Lin { a = Vector3.Dot(o3 - q.a, across), x = Vector3.Dot(c.east, across), y = Vector3.Dot(c.north, across) };
            Lin S = new Lin { a = Vector3.Dot(o3 - q.a, along), x = Vector3.Dot(c.east, along), y = Vector3.Dot(c.north, along) };
            if (Mathf.Abs(T.a) > q.hw + c.radius + 21f || S.a < -c.radius - 21f || S.a > len + c.radius + 21f) continue;
            float sp = S.At(p); Lin hr = (sp <= 0f ? Lin.C(q.ha) : sp >= len ? Lin.C(q.hb) : Lin.C(q.ha) + S * ((q.hb - q.ha) / len)) - Lin.C(0.03f);
            float tp = T.At(p); Lin side = tp > q.hw ? (T - Lin.C(q.hw)) * FK : tp < -q.hw ? (Lin.C(-q.hw) - T) * FK : Lin.C(0f);
            res.Add(hr - side); res.Add(hr + side);
        }
        if (near != null) foreach (var n in near) { var f = CutLin(n, p); if (f.HasValue) res.Add(f.Value); }
        if (spanIdx != null) foreach (int si in spanIdx)
        {
            var q = spans[si]; Lin S = new Lin { a = Vector3.Dot(o3 - q.dA * Planet.R, q.eA), x = Vector3.Dot(c.east, q.eA), y = Vector3.Dot(c.north, q.eA) };
            Lin X = new Lin { a = Vector3.Dot(o3, q.n), x = Vector3.Dot(c.east, q.n), y = Vector3.Dot(c.north, q.n) };
            float sp = S.At(p); if (sp <= q.s0 || sp >= q.s1 || Mathf.Abs(X.At(p)) > q.hw + 0.6f) continue;
            res.Add(Lin.C(q.ha - 1.5f) + S * ((q.hb - q.ha) / q.L));
        }
        return res;
    }
    // de grenslijnen van de dekken van dalbruggen boven vak c (daaronder ligt de grond lager, SpanBottom)
    static List<Lin> SpanLines(Planet.Cell c, List<int> spanIdx)
    {
        var res = new List<Lin>(); if (spanIdx == null) return res; var o3 = c.dir * Planet.R;
        foreach (int si in spanIdx)
        {
            var q = spans[si]; Lin S = new Lin { a = Vector3.Dot(o3 - q.dA * Planet.R, q.eA), x = Vector3.Dot(c.east, q.eA), y = Vector3.Dot(c.north, q.eA) };
            Lin X = new Lin { a = Vector3.Dot(o3, q.n), x = Vector3.Dot(c.east, q.n), y = Vector3.Dot(c.north, q.n) };
            res.Add(S - Lin.C(q.s0)); res.Add(S - Lin.C(q.s1)); res.Add(X - Lin.C(q.hw + 0.6f)); res.Add(X + Lin.C(q.hw + 0.6f));
        }
        return res;
    }
    // De hoogte van het randprofiel op t. Op een wand (twee punten op dezelfde t) kiest bias de kant: > 0 de strook, < 0 het terrein;
    // zonder bias de kant waar het punt vandaan kwam (t0, voor het op het profiel gezet werd)
    static float ProfileAt(List<EPt> pts, float t, float bias = 0f, float t0 = -1f)
    {
        if (t <= pts[0].t)
        {
            if (pts.Count > 1 && pts[1].t - pts[0].t < 1e-7f && pts[0].side != 0) { if (bias != 0f) return (bias > 0f) == (pts[0].side > 0) ? pts[0].h : pts[1].h; return t0 > pts[0].t ? pts[1].h : pts[0].h; }
            return pts[0].h;
        }
        for (int v = 1; v < pts.Count; v++)
        {
            if (t > pts[v].t) continue;
            if (v + 1 < pts.Count && pts[v + 1].t - pts[v].t < 1e-7f && Mathf.Abs(t - pts[v].t) < 1e-7f && pts[v].side != 0)   // een wandpaar
            {
                if (bias != 0f) return (bias > 0f) == (pts[v].side > 0) ? pts[v].h : pts[v + 1].h;
                return t0 >= 0f && t0 > pts[v].t ? pts[v + 1].h : pts[v].h;
            }
            float d = pts[v].t - pts[v - 1].t; return d < 1e-6f ? pts[v].h : Mathf.Lerp(pts[v - 1].h, pts[v].h, (t - pts[v - 1].t) / d);
        }
        return pts[pts.Count - 1].h;
    }
    // Vak i tekenen (v2): per waaierdriehoek de veelhoeken langs de snijlijnen van de hoogtefunctie en de geulen (zoals LineGround en
    // ExactGround), de gedeelde randpunten erin (op de rand komt de hoogte uit de polylijn, niet uit de functie: zo is de rand van twee
    // kanten gelijk), de rest uit H2, en waar het binnenin nog kromt verfijnd via het midden (geen nieuwe punten op de rand).
    // plat: een bebouwd vak (plateau op G; alleen de hoogtefunctie erover, de sokkel doet de rest); skip: driehoekjes onder een brugdek
    // wall: een wandquad (boven a-b, onder c-d, outw naar de lage kant), naar het materiaal sokkel
    static void Ground2Cell(int i, bool plat, System.Action<Vector3, Vector3, Vector3, float, float, float, bool> emit, System.Func<int, bool> skip, System.Action<Vector3, Vector3, Vector3, Vector3, Vector3> wall = null)
    {
        bool fld0 = fieldCells.Contains(i); var swc = fld0 ? G2SwField : G2SwPlain; G2Cells++; if (fld0) G2Field++; swc.Start();
        try { Ground2Cell0(i, plat, emit, skip, wall); } finally { swc.Stop(); }
    }
    static void Ground2Cell0(int i, bool plat, System.Action<Vector3, Vector3, Vector3, float, float, float, bool> emit, System.Func<int, bool> skip, System.Action<Vector3, Vector3, Vector3, Vector3, Vector3> wall)
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
            Vector3 V0 = D0 * (Planet.R + hc0), V1 = D1 * (Planet.R + h1), V2 = D2 * (Planet.R + h2), Nrm = Vector3.Cross(V1 - V0, V2 - V0); float ND = Vector3.Dot(V0, Nrm);   // het vlak door de drie hoekpunten
            var polys = new List<List<Vector2>> { new List<Vector2> { Vector2.zero, L1, L2 } };
            void Split(Lin f) { var next = new List<List<Vector2>>(); foreach (var pl in polys) if (!SplitBy(pl, f, next)) next.Add(pl); polys = next; }
            var ls = new List<Seg>(); var lc = new List<Cap>(); var spI = RoadH.Count == 0 && spanNear.Count > 0 && spanNear.TryGetValue(i, out var spl) ? spl : null;   // (nieuwe opbouw: geen deklijnen, zie EdgeProfile0)
            if (field) foreach (var f in FieldLines(c, k, hc0, h1, h2, ls, lc)) Split(f);
            foreach (var f in SpanLines(c, spI)) Split(f);
            var wg = new List<int>(); var wnq = new List<bool>(); var wl = field ? WallLins(c, ls, lc, wg, wnq) : new List<Lin>();   // de wanden hier (sokkels en bakken), binnenkant negatief; wg: per lijn de strook of kop
            Lin B2 = new Lin { a = hc0, x = ((h1 - hc0) * L2.y - (h2 - hc0) * L1.y) / det, y = ((h2 - hc0) * L1.x - (h1 - hc0) * L2.x) / det };
            if (cn) foreach (var n in near) foreach (var f in new[] { n.sd - Lin.C(-0.01f), n.sd - Lin.C(n.fl), n.sd - Lin.C(n.fl + n.rl), n.sd - Lin.C(n.end), n.t - Lin.C(n.q.hw), n.t - Lin.C(-n.q.hw) }) Split(f);
            var prof = plat ? null : EdgeProfile(i, k);
            Vector2 E = L2 - L1; float el2 = E.sqrMagnitude, el = Mathf.Sqrt(el2);
            float TOf(Vector2 q) => Vector2.Dot(q - L1, E) / el2;
            bool OnEdge(Vector2 q) => Mathf.Abs((E.x * (q.y - L1.y) - E.y * (q.x - L1.x)) / el) < 1e-3f;
            Vector3 Dir(Vector2 q) { float b1 = (q.x * L2.y - q.y * L2.x) / det, b2 = (L1.x * q.y - L1.y * q.x) / det; return (D0 * (1f - b1 - b2) + D1 * b1 + D2 * b2).normalized; }
            float Base(Vector2 q) { Vector3 d = Dir(q); float dn = Vector3.Dot(d, Nrm); return Mathf.Abs(dn) < 1e-9f ? hc0 : ND / dn - Planet.R; }   // waar de straal het vlak raakt
            // Elke veelhoek langs de snijlijnen van de vlakken die er gelden (PlaneLins, gemeten in het midden), tot er niets meer te
            // snijden is: dan is elk stuk één vlak (zoals ExactGround voor de geulen). Daarna tekenen.
            {
                var done = new List<List<Vector2>>(); var work = new Stack<List<Vector2>>(polys); int guard = 0;
                while (work.Count > 0)
                {
                    var pl = work.Pop(); Vector2 ctr = Vector2.zero; foreach (var v in pl) ctr += v; ctr /= pl.Count;
                    bool split = false;
                    if (++guard < 4000 && (field || cn || spI != null))
                    {
                        var fs = PlaneLins(c, B2, ls, lc, cn ? near : null, spI, ctr);
                        for (int x = 0; x < fs.Count && !split; x++) for (int y = x + 1; y < fs.Count && !split; y++)
                        {
                            var tmp = new List<List<Vector2>>(); if (SplitBy(pl, fs[x] - fs[y], tmp)) { foreach (var part in tmp) work.Push(part); split = true; }
                        }
                    }
                    if (!split) done.Add(pl);
                }
                polys = done;
            }
            // Samenvoegen (ontwerp 8.5 punt 3): de snijlijnen zijn oneindige lijnen van alle wegstukken binnen bereik, dus ook waar de
            // hoogtefunctie niet knikt is de grond in repen gesneden (een spinnenweb van dunne driehoeken). Twee veelhoeken met een hele
            // zijde gemeen die in hetzelfde vlak liggen (binnenpunten van de een op het vlak van de ander, binnen 4 mm) en samen bol
            // blijven worden weer één veelhoek. Een wand of knik scheidt ze vanzelf: dan klopt het vlak niet.
            if (Ground2Merge && polys.Count > 1 && (field || cn))
            {
                var P2 = polys; var Hn = new List<List<Vector3>>(P2.Count);   // per veelhoek: binnenpunten (x, y, hoogte)
                foreach (var pl in P2)
                {
                    Vector2 ctr = Vector2.zero; foreach (var v in pl) ctr += v; ctr /= pl.Count; var l = new List<Vector3>(pl.Count + 1);
                    foreach (var v in pl) { Vector2 q = Vector2.Lerp(v, ctr, 0.15f); l.Add(new Vector3(q.x, q.y, HQ(q))); }
                    l.Add(new Vector3(ctr.x, ctr.y, HQ(ctr))); Hn.Add(l);
                }
                bool SamePlane(List<Vector3> A, List<Vector3> B)   // het vlak door de drie wijdste punten van A; A en B liggen erop
                {
                    Vector3 a0 = A[0], a1 = A[0], a2 = A[0]; float best = -1f;
                    foreach (var v in A) { float d = (v.x - a0.x) * (v.x - a0.x) + (v.y - a0.y) * (v.y - a0.y); if (d > best) { best = d; a1 = v; } }
                    best = -1f; foreach (var v in A) { float ar = Mathf.Abs((a1.x - a0.x) * (v.y - a0.y) - (a1.y - a0.y) * (v.x - a0.x)); if (ar > best) { best = ar; a2 = v; } }
                    if (best < 0.02f) return false;   // te smal om een vlak te bepalen
                    Vector3 nrm = Vector3.Cross(a1 - a0, a2 - a0); if (Mathf.Abs(nrm.z) < 1e-9f) return false;
                    foreach (var v in A) if (Mathf.Abs(a0.z - (nrm.x * (v.x - a0.x) + nrm.y * (v.y - a0.y)) / nrm.z - v.z) > 0.004f) return false;
                    foreach (var v in B) if (Mathf.Abs(a0.z - (nrm.x * (v.x - a0.x) + nrm.y * (v.y - a0.y)) / nrm.z - v.z) > 0.004f) return false;
                    return true;
                }
                bool Same(Vector2 a, Vector2 b) => (a - b).sqrMagnitude < 1e-6f;
                List<Vector2> Join(List<Vector2> A, List<Vector2> B)   // A en B langs hun gemeenschappelijke zijde, of null (geen hele zijde gemeen, of niet bol)
                {
                    int na = A.Count, nb = B.Count, ei = -1, ej = -1;
                    for (int x = 0; x < na && ei < 0; x++) for (int y = 0; y < nb; y++) if (Same(A[x], B[(y + 1) % nb]) && Same(A[(x + 1) % na], B[y])) { ei = x; ej = y; break; }
                    if (ei < 0) return null;
                    var r = new List<Vector2>(na + nb - 2);
                    for (int k = 0; k < na; k++) r.Add(A[(ei + 1 + k) % na]);
                    for (int k = 0; k < nb - 2; k++) r.Add(B[(ej + 2 + k) % nb]);
                    // punten op een rechte lijn weg, dan moet de rest bol zijn (één draairichting)
                    for (int k = r.Count - 1; k >= 0 && r.Count > 3; k--)
                    {
                        Vector2 u = r[k] - r[(k + r.Count - 1) % r.Count], w = r[(k + 1) % r.Count] - r[k];
                        if (Mathf.Abs(u.x * w.y - u.y * w.x) < 1e-4f * Mathf.Max(1e-3f, u.magnitude * w.magnitude) && Vector2.Dot(u, w) > 0f) r.RemoveAt(k);
                    }
                    if (r.Count < 3) return null; int sg = 0;
                    for (int k = 0; k < r.Count; k++)
                    {
                        Vector2 u = r[k] - r[(k + r.Count - 1) % r.Count], w = r[(k + 1) % r.Count] - r[k]; float cr = u.x * w.y - u.y * w.x;
                        if (Mathf.Abs(cr) < 1e-5f) continue; int sn = cr > 0f ? 1 : -1; if (sg == 0) sg = sn; else if (sn != sg) return null;
                    }
                    return r;
                }
                for (int pass = 0; pass < 10; pass++)
                {
                    bool any = false; var edges = new Dictionary<(int, int, int, int), int>();
                    for (int a = 0; a < P2.Count; a++)
                    {
                        if (P2[a] == null) continue; bool merged = false;
                        for (int e = 0; !merged && e < P2[a].Count; e++)
                        {
                            Vector2 v0 = P2[a][e], v1 = P2[a][(e + 1) % P2[a].Count];
                            int x0 = Mathf.RoundToInt(v0.x * 1000f), y0 = Mathf.RoundToInt(v0.y * 1000f), x1 = Mathf.RoundToInt(v1.x * 1000f), y1 = Mathf.RoundToInt(v1.y * 1000f);
                            var key = x0 < x1 || (x0 == x1 && y0 <= y1) ? (x0, y0, x1, y1) : (x1, y1, x0, y0);
                            if (!edges.TryGetValue(key, out int b) || b == a || P2[b] == null) { edges[key] = a; continue; }
                            if (!SamePlane(Hn[a], Hn[b]) && !SamePlane(Hn[b], Hn[a])) continue;
                            var j = Join(P2[b], P2[a]); if (j == null) continue;
                            P2[b] = j; Hn[b].AddRange(Hn[a]); P2[a] = null; merged = true; any = true;
                        }
                    }
                    if (!any) break;
                }
                var keep = new List<List<Vector2>>(); foreach (var pl in P2) if (pl != null) keep.Add(pl); polys = keep;
            }
            // Aan elkaar rijgen: een buurveelhoek is langs een andere lijn gesneden, dus zijn hoekpunt ligt midden op een zijde van deze
            // (een T-kruising); op een knik is dat een kier. Elk hoekpunt dat op een zijde van een andere veelhoek ligt komt daar ook in.
            {
                var allV = new List<Vector2>(); foreach (var pl in polys) foreach (var v in pl) allV.Add(v);
                for (int pi = 0; pi < polys.Count; pi++)
                {
                    var pl = polys[pi]; var outp = new List<Vector2>();
                    for (int v = 0; v < pl.Count; v++)
                    {
                        Vector2 qa = pl[v], qb = pl[(v + 1) % pl.Count]; outp.Add(qa); Vector2 e = qb - qa; float el2b = e.sqrMagnitude; if (el2b < 1e-8f) continue;
                        var ins = new List<(float u, Vector2 q)>();
                        foreach (var w in allV)
                        {
                            float u = Vector2.Dot(w - qa, e) / el2b; if (u <= 1e-4f || u >= 1f - 1e-4f) continue;
                            float off = Mathf.Abs((e.x * (w.y - qa.y) - e.y * (w.x - qa.x))) / Mathf.Sqrt(el2b); if (off > 1e-3f) continue;
                            bool dup = false; foreach (var x in ins) if (Mathf.Abs(x.u - u) < 1e-5f) { dup = true; break; } if (!dup) ins.Add((u, w));
                        }
                        ins.Sort((x, y) => x.u.CompareTo(y.u)); foreach (var x in ins) outp.Add(x.q);
                    }
                    polys[pi] = outp;
                }
            }
            // De wandlijnen zijn vlak en de hoogtefunctie leeft op de bol: een punt op een wandlijn ligt millimeters naast de echte sprong.
            // Elk punt binnen 4 cm van een wandlijn (niet op de vakrand: die komen uit het profiel) schuift langs de normaal naar de sprong
            // (bisectie); dezelfde functie van de positie, dus alle kopieën van het punt schuiven gelijk
            float HQ(Vector2 q) => H2(Dir(q), Base(q), field, cn);
            Vector2 SnapWall(Vector2 q)
            {
                if (OnEdge(q)) return q;
                foreach (var w in wl)
                {
                    if (Mathf.Abs(w.At(q)) > 0.04f) continue; Vector2 nh = new Vector2(w.x, w.y).normalized;
                    float d0 = HQ(q - nh * 0.04f), d1 = HQ(q + nh * 0.04f); if (Mathf.Abs(d1 - d0) < 0.2f) continue;
                    float mid = (d0 + d1) * 0.5f, lo = -0.04f, hi = 0.04f;
                    for (int it = 0; it < 20; it++) { float m2 = (lo + hi) * 0.5f; if ((HQ(q + nh * m2) > mid) == (d1 > d0)) hi = m2; else lo = m2; }
                    return q + nh * ((lo + hi) * 0.5f);
                }
                return q;
            }
            if (wl.Count > 0) foreach (var pl in polys) for (int v = 0; v < pl.Count; v++) pl[v] = SnapWall(pl[v]);
            foreach (var pl0 in polys) Piece(pl0);
            // Aan welke kant van een wand ligt een hoekpunt van deze veelhoek: op een wandlijn van groep g (een strook of kop) hoort het bij
            // de binnenkant (+1 mm) als het midden van de veelhoek binnen alle lijnen van g ligt, anders bij het terrein (-1 mm); een punt dat
            // op geen wandlijn ligt krijgt geen kant. Per hoekpunt, want een veelhoek kan binnen de kop en toch buiten de strook van een arm liggen.
            bool InsideG(int g, Vector2 ctr) { for (int y = 0; y < wl.Count; y++) if (wg[y] == g && wl[y].At(ctr) >= 0f) return false; return true; }
            void Piece(List<Vector2> pl0)
            {
                int n0 = pl0.Count; if (n0 < 3) return; Vector2 ctr = Vector2.zero; foreach (var v in pl0) ctr += v; ctr /= n0;
                // de hoekpunten: op de rand (t >= 0) of binnenin (t < 0); de gedeelde randpunten tussen twee randhoekpunten erbij, en een
                // randhoekpunt vlak bij een gedeeld punt (binnen 1,5 cm) daarop gezet (t0: waar het vandaan kwam, voor de kant van een wand)
                var pl = new List<(Vector2 q, float t, float t0)>();
                for (int v = 0; v < n0; v++)
                {
                    Vector2 q = pl0[v]; bool on = prof != null && OnEdge(q); float t = on ? Mathf.Clamp01(TOf(q)) : -1f, tq = t;
                    if (on) foreach (var ep in prof) if (Mathf.Abs(ep.t - t) * el < (ep.side != 0 ? 0.045f : 0.015f)) { t = ep.t; break; }   // (een wandpaar ligt tot 4 cm naast zijn lijn)
                    pl.Add((q, t, tq));
                    if (!on) continue; Vector2 qn = pl0[(v + 1) % n0]; if (!OnEdge(qn)) continue;
                    float tn = Mathf.Clamp01(TOf(qn)); foreach (var ep in prof) if (Mathf.Abs(ep.t - tn) * el < (ep.side != 0 ? 0.045f : 0.015f)) { tn = ep.t; break; }
                    if (tn > t) { foreach (var ep in prof) if (ep.t > t + 1e-6f && ep.t < tn - 1e-6f) pl.Add((L1 + E * ep.t, ep.t, ep.t)); }
                    else for (int w = prof.Count - 1; w >= 0; w--) { var ep = prof[w]; if (ep.t < t - 1e-6f && ep.t > tn + 1e-6f) pl.Add((L1 + E * ep.t, ep.t, ep.t)); }
                }
                for (int v = pl.Count - 1; v >= 0 && pl.Count > 3; v--) if ((pl[v].q - pl[(v + pl.Count - 1) % pl.Count].q).sqrMagnitude < 1e-6f) pl.RemoveAt(v);   // dubbele punten (na het snijden)
                int n = pl.Count; if (n < 3) return; var P = new Vector3[n]; var H = new float[n];
                float HOf(int v, float bs) { if (pl[v].t >= 0f) return ProfileAt(prof, pl[v].t, bs, pl[v].t0); float ob = wallBias; wallBias = bs; try { return H2(Dir(pl[v].q), Base(pl[v].q), field, cn); } finally { wallBias = ob; } }
                Vector3 DOf(int v) => pl[v].t >= 0f ? Vector3.Lerp(D1 * (Planet.R + h1), D2 * (Planet.R + h2), pl[v].t).normalized : Dir(pl[v].q);
                var bv = new float[n]; var lv = new int[n];
                for (int v = 0; v < n; v++)   // (bij een hoek waar twee wanden elkaar raken ligt een punt op twee lijnen: binnen als de veelhoek binnen een van die groepen ligt)
                {
                    lv[v] = -1; bv[v] = 0f; if (wl.Count == 0) continue;
                    for (int x = 0; x < wl.Count; x++) { if (Mathf.Abs(wl[x].At(pl[v].q)) >= 0.045f) continue; if (lv[v] < 0) { lv[v] = x; bv[v] = -1e-3f; } if (InsideG(wg[x], ctr)) { lv[v] = x; bv[v] = 1e-3f; break; } }
                }
                for (int v = 0; v < n; v++) { H[v] = HOf(v, bv[v]); P[v] = DOf(v) * (Planet.R + H[v]); }
                // de wand (ontwerp 8.5): langs elke zijde van de veelhoek op een wandlijn, aan de kant van de strook, een quad van de eigen hoogte
                // naar de hoogte van de andere kant (bit-gelijk aan wat de buurveelhoek daar tekent), naar de lage kant gekeerd
                if (wall != null)
                    for (int v = 0; v < n; v++)
                    {
                        int v1 = (v + 1) % n; if (bv[v] <= 0f || bv[v1] <= 0f) continue; Lin w = default; bool found = false;   // een zijde aan de binnenkant van een wand
                        for (int x = 0; x < wl.Count; x++) if (!wnq[x] && Mathf.Abs(wl[x].At(pl[v].q)) < 0.045f && Mathf.Abs(wl[x].At(pl[v1].q)) < 0.045f) { w = wl[x]; found = true; break; }
                        if (!found) continue;
                        float la = HOf(v, -1e-3f), lb = HOf(v1, -1e-3f); if (Mathf.Abs(la - H[v]) < 1e-3f && Mathf.Abs(lb - H[v1]) < 1e-3f) continue;
                        Vector3 outw = (c.east * w.x + c.north * w.y).normalized; if (la + lb > H[v] + H[v1]) outw = -outw;   // naar de lage kant: het terrein (sokkel) of de strook (bak)
                        wall(P[v], P[v1], DOf(v1) * (Planet.R + lb), DOf(v) * (Planet.R + la), outw);
                    }
                int r = 0; for (int v = 0; v < n; v++) if (pl[v].t < 0f) { r = v; break; }   // waaieren vanuit een punt dat niet op de rand ligt (anders platte driehoekjes langs de rand)
                for (int v = 1; v + 1 < n; v++) { int a = (r + v) % n, b = (r + v + 1) % n; Refine2(pl[r].q, pl[a].q, pl[b].q, P[r], P[a], P[b], H[r], H[a], H[b], 0f, 0); }
            }
            void Refine2(Vector2 a, Vector2 b, Vector2 cc, Vector3 Pa, Vector3 Pb, Vector3 Pc, float ha, float hb, float hc2, float bias, int depth)
            {
                Vector2 m2 = (a + b + cc) / 3f; Vector3 dm = Dir(m2); float ob = wallBias; wallBias = bias; float hm; bool steep; try { hm = H2(dm, Base(m2), field, cn, out steep); } finally { wallBias = ob; }
                if (depth < 3 && Mathf.Abs(hm - (ha + hb + hc2) / 3f) > 0.06f && (b - a).sqrMagnitude + (cc - a).sqrMagnitude > 0.5f)
                {
                    Vector3 Pm = dm * (Planet.R + hm);
                    Refine2(a, b, m2, Pa, Pb, Pm, ha, hb, hm, bias, depth + 1); Refine2(b, cc, m2, Pb, Pc, Pm, hb, hc2, hm, bias, depth + 1); Refine2(cc, a, m2, Pc, Pa, Pm, hc2, ha, hm, bias, depth + 1);
                    return;
                }
                Ground2Tris++; emit(Pa, Pb, Pc, ha, hb, hc2, steep);
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
                var pk = EdgeProfile(i, k); var pp = EdgeProfile(i, (k + m - 1) % m);   // (t loopt langs de koorde tussen de hoekpunten)
                float ha = pk[0].h, hb = pp[pp.Count - 1].h;   // (op een wand: een paar op de hoek, dan hoort een van de twee te kloppen)
                bool okc = Mathf.Abs(ha - hb) <= 0.002f;
                if (!okc && pk.Count > 1 && pk[1].t - pk[0].t < 1e-7f) okc = Mathf.Abs(pk[1].h - hb) <= 0.002f;
                if (!okc && pp.Count > 1 && pp[pp.Count - 1].t - pp[pp.Count - 2].t < 1e-7f) okc = Mathf.Abs(ha - pp[pp.Count - 2].h) <= 0.002f || (pk.Count > 1 && pk[1].t - pk[0].t < 1e-7f && Mathf.Abs(pk[1].h - pp[pp.Count - 2].h) <= 0.002f);
                if (!okc) { nc++; if (nc <= 4) sb.Append("  hoek ").Append(i).Append('/').Append(k).Append(": rand ").Append(k).Append(' ').Append(ha.ToString("0.00")).Append(" rand ").Append((k + m - 1) % m).Append(' ').Append(hb.ToString("0.00")).Append(" (eigenaar ").Append(Mathf.Min(i, edN[i][k])).Append('/').Append(Mathf.Min(i, edN[i][(k + m - 1) % m])).Append(')').AppendLine(); }
                int j = edN[i][k]; if (Plateau(j)) continue; int kj = -1; for (int q = 0; q < Planet.Cells[j].corners.Length; q++) if (edN[j][q] == i) kj = q; if (kj < 0) continue;
                var pj = EdgeProfile(j, kj); bool same = pj.Count == pk.Count; if (same) for (int q = 0; q < pk.Count; q++) if (Mathf.Abs(pj[pj.Count - 1 - q].h - pk[q].h) > 0.002f || Mathf.Abs(1f - pj[pj.Count - 1 - q].t - pk[q].t) > 1e-4f) same = false;
                if (!same) { nn++; if (nn <= 4) sb.Append("  rand ").Append(i).Append('/').Append(k).Append(" en ").Append(j).Append('/').Append(kj).Append(": ").Append(pk.Count).Append(" en ").Append(pj.Count).Append(" punten").AppendLine(); }
            }
        }
        return "randpunten: " + n + " vakken, " + nc + " hoeken ongelijk, " + nn + " randen ongelijk met de buur" + (sb.Length > 0 ? "\n" + sb : "");
    }

    // Voor de test: alle driehoekjes van vak i zoals Ground2Cell ze tekent, met per driehoekje de hoogte van H2 in het midden en het
    // verschil met het vlak van het driehoekje (de ergste eerst), plus de randprofielen
    public static string Ground2Dump(int i)
    {
        var c = Planet.Cells[i]; int m = c.corners.Length; var sb = new System.Text.StringBuilder(); var tris = new List<(Vector3 a, Vector3 b, Vector3 c, float d)>();
        var su = segsUse; var fu = flatsUse; var cu = capsUse; if (fieldCells.Contains(i)) UseNear(i); else UseAll();
        try
        {
            sb.Append("vak ").Append(i).Append(' ').Append(Desc(i)).Append(" G ").Append(G(i).ToString("0.00")).Append(" plateau ").Append(Plateau(i)).Append(" vlak ").Append(FlatGround(i)).Append(" veld ").Append(fieldCells.Contains(i)).Append('/').Append(NearSeg(c.dir, c.radius)).AppendLine();
            for (int k = 0; k < m; k++) { var pr = EdgeProfile(i, k); sb.Append("  rand ").Append(k).Append(" naar ").Append(edN[i][k]).Append(" (CornerBase ").Append(CornerBase(i, k).ToString("0.00")).Append("): "); foreach (var e in pr) sb.Append(e.t.ToString("0.000")).Append('=').Append(e.h.ToString("0.00")).Append(' '); sb.AppendLine(); }
            {   // de stukken en koppen hier met hun indeling, en per rand de snijpunten van de wandlijnen (om te zien of een wand als paar herkend wordt)
                float hc0 = Plateau(i) ? G(i) : Center2(i);
                for (int k = 0; k < m; k++)
                {
                    var ls = new List<Seg>(); var lc = new List<Cap>(); var fl = FieldLines(c, k, hc0, CornerBase(i, k), CornerBase(i, (k + 1) % m), ls, lc); var wl = WallLins(c, ls, lc);
                    sb.Append("  rand ").Append(k).Append(" stukken:"); foreach (var q in ls) sb.Append(' ').Append(q.ia).Append('-').Append(q.ib).Append(q.wall ? "W" : "m").Append(q.tap0 ? "0" : "").Append(q.tap1 ? "1" : "");
                    sb.Append(" koppen:"); foreach (var q in lc) sb.Append(' ').Append(q.ic).Append(q.wall ? "W" : "m").Append("/wo").Append(q.wo.ToString("0.00"));
                    Vector2 L1 = c.local[k], L2 = c.local[(k + 1) % m]; sb.Append(" wandlijnen ").Append(wl.Count).Append(" snijden op t:");
                    Vector3 PA = c.corners[k] * (Planet.R + CornerBase(i, k)), PB = c.corners[(k + 1) % m] * (Planet.R + CornerBase(i, (k + 1) % m)); float elen = (L2 - L1).magnitude;
                    float HAtE(float t) { Vector3 P = Vector3.Lerp(PA, PB, t); return H2(P.normalized, P.magnitude - Planet.R, fieldCells.Contains(i) && NearSeg(c.dir, c.radius), false); }
                    foreach (var f in wl)
                    {
                        float f1 = f.At(L1), f2 = f.At(L2); if ((f1 < 0f) == (f2 < 0f)) continue; float tw = f1 / (f1 - f2); sb.Append(' ').Append(tw.ToString("0.000"));
                        float dq = 0.3f / elen, lo = Mathf.Max(0f, tw - dq), hi = Mathf.Min(1f, tw + dq), d0 = HAtE(lo), d1 = HAtE(hi); if (Mathf.Abs(d1 - d0) < 0.2f) { sb.Append("(geen sprong binnen 30 cm)"); continue; }
                        float mid = (d0 + d1) * 0.5f; for (int it = 0; it < 20; it++) { float m2 = (lo + hi) * 0.5f; if ((HAtE(m2) > mid) == (d1 > d0)) hi = m2; else lo = m2; }
                        sb.Append("(sprong op ").Append(((lo + hi) * 0.5f).ToString("0.000")).Append(' ').Append((((lo + hi) * 0.5f - tw) * elen * 100f).ToString("+0.0;-0.0")).Append(" cm)");
                    }
                    sb.AppendLine();
                }
            }
            Ground2Cell(i, Plateau(i), (a, b, cc, ha, hb, hc, steep) =>
            {
                Vector3 mid = (a + b + cc) / 3f; float hm = H(mid.normalized), hp = (ha + hb + hc) / 3f; tris.Add((a, b, cc, Mathf.Abs(hm - hp)));
            }, k => false);
        }
        finally { segsUse = su; flatsUse = fu; capsUse = cu; }
        tris.Sort((x, y) => y.d.CompareTo(x.d));
        sb.Append("  ").Append(tris.Count).Append(" driehoekjes; de ergste:").AppendLine();
        string V(Vector3 w) { Vector3 v = w - c.dir * Planet.R; return "(" + Vector3.Dot(v, c.east).ToString("0.00") + "," + Vector3.Dot(v, c.north).ToString("0.00") + " h " + (w.magnitude - Planet.R).ToString("0.00") + ")"; }
        for (int q = 0; q < Mathf.Min(12, tris.Count); q++) sb.Append("    ").Append(tris[q].d.ToString("0.00")).Append(" m: ").Append(V(tris[q].a)).Append(' ').Append(V(tris[q].b)).Append(' ').Append(V(tris[q].c)).AppendLine();
        return sb.ToString();
    }
}
