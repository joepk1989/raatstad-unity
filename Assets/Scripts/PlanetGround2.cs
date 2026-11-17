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
    // De stukken en koppen die in de driehoek die Ground2Cell nu tekent kunnen meedoen (FieldLines, nieuwe opbouw): de hoogte daarbinnen
    // rekent alleen met die, de andere halen de driehoek niet (hun talud of dak ligt er overal buiten). Scheelt het meeste rekenwerk.
    static List<Seg> triSegs; static List<Cap> triCaps;
    public static long Ground2Tris;           // teller: driehoeken die Ground2Cell tekende (voor de test)
    // tijdmeting (voor de test): vakken met en zonder hoogtefunctie, randprofielen (opnieuw berekend of uit het geheugen)
    public static long G2Cells, G2Field, G2EdgeNew, G2EdgeHit; public static readonly System.Diagnostics.Stopwatch G2SwField = new System.Diagnostics.Stopwatch(), G2SwPlain = new System.Diagnostics.Stopwatch(), G2SwEdge = new System.Diagnostics.Stopwatch();
    public static Dictionary<int, long> G2CellMs;   // (voor de test: de tijd per vak in ticks van de stopwatch, als niet null)
    public static int SegsNear => segsUse.Count; public static int CapsNear => capsUse.Count; public static void UseNearPub(int i) => UseNear(i);
    public static string G2Stats() => G2Cells + " vakken (" + G2Field + " met hoogtefunctie): " + G2SwField.ElapsedMilliseconds + " ms met, " + G2SwPlain.ElapsedMilliseconds + " ms zonder; randprofielen " + G2EdgeNew + " berekend (" + G2SwEdge.ElapsedMilliseconds + " ms), " + G2EdgeHit + " uit het geheugen; vakken " + G2CacheHit + " uit het geheugen, " + G2CacheMiss + " berekend";
    public static readonly System.Diagnostics.Stopwatch G2SwLines = new System.Diagnostics.Stopwatch(), G2SwPlanes = new System.Diagnostics.Stopwatch(), G2SwMerge = new System.Diagnostics.Stopwatch(), G2SwPiece = new System.Diagnostics.Stopwatch(), G2SwNear = new System.Diagnostics.Stopwatch();
    public static long G2Polys, G2Lines;
    public static string G2Steps() => "lijnen " + G2SwLines.ElapsedMilliseconds + " ms (" + G2Lines + " lijnen), vlakken snijden " + G2SwPlanes.ElapsedMilliseconds + " ms (" + G2Polys + " stukken), samenvoegen " + G2SwMerge.ElapsedMilliseconds + " ms, tekenen " + G2SwPiece.ElapsedMilliseconds + " ms, UseNear " + G2SwNear.ElapsedMilliseconds + " ms";
    public static void G2Reset() { G2Cells = G2Field = G2EdgeNew = G2EdgeHit = G2Polys = G2Lines = G2CacheHit = G2CacheMiss = 0; G2SwField.Reset(); G2SwPlain.Reset(); G2SwEdge.Reset(); G2SwLines.Reset(); G2SwPlanes.Reset(); G2SwMerge.Reset(); G2SwPiece.Reset(); G2SwNear.Reset(); }
    // Terrein v2 is de enige bodem (de oude opbouw en de schakelaar zijn weg, 16 november 2026); deze eigenschap bestaat nog voor tests die hem zetten
    public static bool Ground2 { get => true; set { } }

    // De hoogte van het maaiveld (v2) op punt dir met de gewone terreinhoogte basis daar: de hoogtefunctie langs wegen (alleen in de
    // vakken binnen bereik, field; daarbuiten doet ze toch niets), de hoogste dijk erop, de laagste geul erin (alleen bij vakken waar
    // een geul of dijk in de buurt komt, cutsNear). Dezelfde regels voor elk punt, in welk vak het ook ligt: zo krijgen twee buren op
    // hun gedeelde rand dezelfde hoogte.
    static float H2(Vector3 dir, float basis, bool field, bool cutsNear) => H2(dir, basis, field, cutsNear, out _);
    static float H2(Vector3 dir, float basis, bool field, bool cutsNear, out bool slope)
    {
        slope = false; float h = field ? (triSegs != null ? GroundField(dir, basis, out slope, triSegs, triCaps) : GroundField(dir, basis, out slope)) : basis;
        if (cutsNear) { if (fillN > 0) h = Mathf.Max(h, Fill(dir)); if (carved.Count > 0) h = Mathf.Min(h, Carve(dir)); }
        return h;
    }
    // het midden van het gewone terrein van een vak zonder plateau: G plus de eigen verschuiving; een vak op een niveau het gemiddelde
    // van zijn hoeken; een weg- of padvak op het maaiveld in een kom tot zijn laagste hoek (het maaiveld steekt dan nooit door de weg)
    static float Center2(int i)
    {
        var c = Planet.Cells[i]; int m = c.corners.Length;
        if (levelled.Contains(i)) { float s = 0f; for (int k = 0; k < m; k++) s += CornerBase(i, k); return s / m + CenterOfs1(i); }
        if (IsWalk(i)) return Terrain(i) + CenterOfs1(i);   // nieuwe opbouw: het terrein onder de weg; de weg zelf komt uit de hoogtefunctie
        // Op een helling liggen de hoeken van een vak op het laagste van de drie vakken eromheen, dus lager dan het midden: elk vak was
        // een puntje. Het midden zakt daarom naar het gemiddelde van zijn hoeken (het vlak door de hoeken, de helling vloeit door);
        // alleen wat een vak boven zijn hoogste buur uitsteekt blijft erbovenop staan (een top, een los opgehoogd vak).
        float g = G(i), sc = 0f, mx = float.MinValue;
        for (int k = 0; k < m; k++) sc += CornerBase(i, k);
        foreach (int j in c.nb) mx = Mathf.Max(mx, Terrain(j));
        return Mathf.Min(g, sc / m + Mathf.Max(0f, g - mx)) + CenterOfs1(i);
    }
    // Een randpunt: t langs de rand (0 bij hoek k, 1 bij hoek k+1) en de hoogte van het maaiveld daar
    // side: bij een wand staan er twee punten op dezelfde t, side +1 het punt aan de kant van de strook (de binnenkant), -1 aan de kant van het terrein
    struct EPt { public float t, h; public int side; }
    static readonly Dictionary<long, (long key, List<EPt> pts)> edgeCache = new Dictionary<long, (long, List<EPt>)>();
    static long EdgeKey(int i, int j, int k)
    {
        long h = 1469598103934665603L; void F(float f) { h = unchecked((h ^ (uint)System.BitConverter.SingleToInt32Bits(f)) * 1099511628211L); }
        var c = Planet.Cells[i]; int m = c.corners.Length;
        void L(long x) { h = unchecked((h ^ x) * 1099511628211L); }
        // (de inhoud waar het profiel van afhangt, niet fieldVer: zo blijft een rand staan als er elders een weg of het terrein verandert)
        for (int s = 0; s < 2; s++) { int x = s == 0 ? Mathf.Min(i, j) : Mathf.Max(i, j); if (x < 0) continue; bool px = Plateau(x); L(NearHash(x)); F(px ? 1f : 0f); F(px ? G(x) : Center2(x)); if (!fieldCells.Contains(x) && RoadShape) L(AllFlatsHash()); }   // (een vak buiten het bereik rekent met alle vlakke stukken, PlaneLins)
        F(cutsHash); F(FieldK); F((RoadShape ? 1f : 0f) + (RoadSlopes ? 2f : 0f)); F(Mathf.Min(G(i), G(j))); F(Mathf.Max(G(i), G(j))); float c1 = CornerBase(i, k), c2 = CornerBase(i, (k + 1) % m); F(Mathf.Min(c1, c2)); F(Mathf.Max(c1, c2)); F(spans.Count); F(cuts.Count);   // (symmetrisch: van beide kanten dezelfde sleutel)
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
        var ts0 = triSegs; var tc0 = triCaps; triSegs = null; triCaps = null;   // (een randprofiel rekent met alle stukken in de buurt van zijn eigenaar)
        try { return EdgeProfile1(a, ka); } finally { triSegs = ts0; triCaps = tc0; }
    }
    static List<EPt> EdgeProfile1(int a, int ka)
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
        ts.Sort();
        {   // de snijlijnen van de vlakken die in elk stuk gelden (gemeten in het midden van het stuk), daar de snijpunten met de rand bij
            float det0 = L1.x * L2.y - L1.y * L2.x; Lin BL = new Lin { a = hc0, x = ((bA - hc0) * L2.y - (bB - hc0) * L1.y) / det0, y = ((bB - hc0) * L1.x - (bA - hc0) * L2.x) / det0 };
            var extra = new List<float>();
            for (int v = 0; v + 1 < ts.Count; v++)
            {
                if (ts[v + 1] - ts[v] < 1e-3f) continue; Vector2 pm = Vector2.Lerp(L1, L2, (ts[v] + ts[v + 1]) * 0.5f);
                var lins = PlaneLins(c, BL, ls, lc, cn ? near : null, pm);
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
            all.Add(pts[v]); var ks = new List<float>(); Kinks(HAt, pts[v].h, pts[v + 1].h, pts[v].t, pts[v + 1].t, len, ks, 0); ks.Sort();
            foreach (float kt in ks) all.Add(new EPt { t = kt, h = HAt(kt) });
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
    static void Kinks(System.Func<float, float> f, float f0, float f1, float t0, float t1, float len, List<float> outT, int depth)
    {
        if (depth > 5 || (t1 - t0) * len < 0.1f) return;
        float D(float t) => f(t) - Mathf.Lerp(f0, f1, (t - t0) / (t1 - t0));
        bool off = false; for (int q = 1; q < 4 && !off; q++) if (Mathf.Abs(D(t0 + (t1 - t0) * q * 0.25f)) > 0.02f) off = true;
        if (!off) return;
        float lo = t0, hi = t1;
        for (int it = 0; it < 26; it++) { float a = lo + (hi - lo) / 3f, b = hi - (hi - lo) / 3f; if (Mathf.Abs(D(a)) < Mathf.Abs(D(b))) lo = a; else hi = b; }
        float tk = (lo + hi) * 0.5f; if (tk - t0 < 1e-4f || t1 - tk < 1e-4f) return;
        float dq = 0.02f / len, fa = f(Mathf.Max(t0, tk - dq)), fb = f(Mathf.Min(t1, tk + dq));
        outT.Add(tk);   // (een sprong hier is een fout die de meting moet laten zien: de wanden komen uit de paren, EdgeProfile0)
        Kinks(f, f0, fa, t0, Mathf.Max(t0, tk - dq), len, outT, depth + 1); Kinks(f, fb, f1, Mathf.Min(t1, tk + dq), t1, len, outT, depth + 1);
    }
    // De hoogtefunctie is een min/max van vlakken (hoogte als lineaire functie van de vlakke coördinaten): het basisterrein, per stuk
    // weg het wegdek en het talud aan de kant waar p ligt (omhoog en omlaag), per kop idem met de zijde die bij p geldt, de vlakke
    // stukken, de geulen en dijken (CutLin), het dek van een dalbrug. Waar twee van die vlakken elkaar snijden knikt de functie: die
    // lijnen zijn de snijlijnen die de losse lijnen (FieldLines) niet geven. Alleen de vlakken die bij p gelden (de zijkanten zijn al
    // gesneden langs T = ±hw, de graten van een kop en de randen van een geul, dus binnen één stuk verandert dat niet).
    static List<Lin> PlaneLins(Planet.Cell c, Lin B, List<Seg> ls, List<Cap> lc, List<NearCut> near, Vector2 p)
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
            { float ov = Mathf.Max(0f, Mathf.Max(-sp, sp - len)); bool hard = sp < 0f ? q.hardA : q.hardB; if (ov > 0f && !hard && ov > Mathf.Abs(tp) - q.hw) side = (sp < 0f ? Lin.C(0f) - S : S - Lin.C(len)) * FK; }   // voorbij het eind: de kegel in de lengterichting (niet aan een hard eind)
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
            if ((o3 - q.a).sqrMagnitude > (len + q.hw + c.radius + 25f) * (len + q.hw + c.radius + 25f)) continue;   // (aan de overkant van de bol lijkt een stuk plat gerekend vlakbij)
            if (Mathf.Abs(T.a) > q.hw + c.radius + 21f || S.a < -c.radius - 21f || S.a > len + c.radius + 21f) continue;
            float sp = S.At(p); Lin hr = (sp <= 0f ? Lin.C(q.ha) : sp >= len ? Lin.C(q.hb) : Lin.C(q.ha) + S * ((q.hb - q.ha) / len)) - Lin.C(0.03f);
            float tp = T.At(p); Lin side = tp > q.hw ? (T - Lin.C(q.hw)) * FK : tp < -q.hw ? (Lin.C(-q.hw) - T) * FK : Lin.C(0f);
            res.Add(hr - side); res.Add(hr + side);
        }
        if (near != null) foreach (var n in near) { var f = CutLin(n, p); if (f.HasValue) res.Add(f.Value); }
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
        bool fld0 = fieldCells.Contains(i); var swc = fld0 ? G2SwField : G2SwPlain; G2Cells++; if (fld0) G2Field++; swc.Start(); long t0 = G2CellMs != null ? swc.ElapsedTicks : 0;
        try
        {
            if (Ground2Cache && !FlatGround(i) && CellKey(i, plat, skip, wall != null, out long key))
            {
                if (cellCache.TryGetValue(i, out var e) && e.key == key)   // hetzelfde als de vorige keer: de onthouden driehoeken en wanden
                {
                    G2CacheHit++; Ground2Tris += e.t.Length;
                    if (G2CacheWrong != null)   // (voor de test: toch uitrekenen en vergelijken)
                    {
                        var chk = new List<G2Tri>(); long tr0 = Ground2Tris; Ground2Cell0(i, plat, (a, b, cc, ha, hb, hc, st) => chk.Add(new G2Tri { a = a, b = b, c = cc }), skip, wall == null ? null : (a, b, cc, d, o) => { }); Ground2Tris = tr0;
                        bool same = chk.Count == e.t.Length; if (same) for (int q = 0; q < chk.Count; q++) if ((chk[q].a - e.t[q].a).sqrMagnitude + (chk[q].b - e.t[q].b).sqrMagnitude + (chk[q].c - e.t[q].c).sqrMagnitude > 1e-6f) { same = false; break; }
                        if (!same) G2CacheWrong.Add(i + " (" + e.t.Length + " onthouden, " + chk.Count + " nu)");
                    }
                    foreach (var t in e.t) emit(t.a, t.b, t.c, t.ha, t.hb, t.hc, t.steep);
                    if (wall != null) foreach (var w in e.w) wall(w.a, w.b, w.c, w.d, w.o);
                    return;
                }
                G2CacheMiss++; var lt = new List<G2Tri>(); var lw = new List<G2Wall>();
                Ground2Cell0(i, plat, (a, b, cc, ha, hb, hc, st) => { lt.Add(new G2Tri { a = a, b = b, c = cc, ha = ha, hb = hb, hc = hc, steep = st }); emit(a, b, cc, ha, hb, hc, st); }, skip,
                    wall == null ? null : (a, b, cc, d, o) => { lw.Add(new G2Wall { a = a, b = b, c = cc, d = d, o = o }); wall(a, b, cc, d, o); });
                cellCache[i] = (key, lt.ToArray(), lw.ToArray()); return;
            }
            Ground2Cell0(i, plat, emit, skip, wall);
        }
        finally { swc.Stop(); triSegs = null; triCaps = null; if (G2CellMs != null) G2CellMs[i] = (G2CellMs.TryGetValue(i, out long o2) ? o2 : 0) + swc.ElapsedTicks - t0; }
    }
    // ---------- onthouden per vak ----------
    // Een vak langs een weg of geul kost het meeste rekenwerk (snijden, samenvoegen). Zijn driehoeken en wanden blijven bewaard onder een
    // sleutel van alles waar ze van afhangen: de hoogtes van het vak en zijn hoeken, de stukken weg in de buurt, de geulen, en de
    // randprofielen zelf. Verandert er elders iets (een krater, een weg verderop), dan komt dit vak uit het geheugen.
    public static bool Ground2Cache = true; public static long G2CacheHit, G2CacheMiss; public static List<string> G2CacheWrong;   // (voor de test, als niet null: de vakken die uit het geheugen anders kwamen dan opnieuw berekend)
    public static void Ground2CacheClear() { cellCache.Clear(); edgeCache.Clear(); nearHash.Clear(); }
    // Voor de test: de onthouden randprofielen opnieuw uitrekenen en vergelijken (een rand die bleef staan terwijl hij veranderd is)
    public static string Ground2EdgeCheck()
    {
        Topo(); var sb = new System.Text.StringBuilder(); int bad = 0, n = 0; var su = segsUse; var fu = flatsUse; var cu = capsUse;
        foreach (var kv in new List<KeyValuePair<long, (long key, List<EPt> pts)>>(edgeCache))
        {
            int a = (int)(kv.Key >> 32), b = (int)(kv.Key & 0xFFFFFFFF), ka = -1; var ca = Planet.Cells[a]; for (int q = 0; q < ca.corners.Length; q++) if (edN[a][q] == b) { ka = q; break; }
            if (ka < 0 || kv.Value.key != EdgeKey(a, b, ka)) continue; n++;
            List<EPt> fr; try { fr = EdgeProfile0(a, ka); } finally { segsUse = su; flatsUse = fu; capsUse = cu; }
            bool same = fr.Count == kv.Value.pts.Count; if (same) for (int q = 0; q < fr.Count; q++) if (Mathf.Abs(fr[q].h - kv.Value.pts[q].h) > 1e-4f || Mathf.Abs(fr[q].t - kv.Value.pts[q].t) > 1e-5f) { same = false; break; }
            if (!same) { bad++; if (bad <= 12) sb.Append("  rand ").Append(a).Append('-').Append(b).Append(": ").Append(kv.Value.pts.Count).Append(" punten onthouden, ").Append(fr.Count).Append(" nu; ").Append(Desc(a)).Append(" | ").Append(Desc(b)); sb.Append(" veld ").Append(fieldCells.Contains(a)).Append(" geulen ").Append(cuts.Count > 0 ? (CutsNear(ca)?.Count ?? 0) : 0).Append(" G ").Append(G(a).ToString("0.00")).Append(" oud:"); foreach (var x in kv.Value.pts) sb.Append(' ').Append(x.t.ToString("0.000")).Append('=').Append(x.h.ToString("0.00")); sb.Append(" nu:"); foreach (var x in fr) sb.Append(' ').Append(x.t.ToString("0.000")).Append('=').Append(x.h.ToString("0.00")); sb.AppendLine(); }
        }
        return "randen onthouden en nog geldig: " + n + ", anders dan opnieuw berekend: " + bad + (bad > 0 ? " " + sb : "");
    }
    struct G2Tri { public Vector3 a, b, c; public float ha, hb, hc; public bool steep; }
    struct G2Wall { public Vector3 a, b, c, d, o; }
    static readonly Dictionary<int, (long key, G2Tri[] t, G2Wall[] w)> cellCache = new Dictionary<int, (long, G2Tri[], G2Wall[])>();
    static readonly Dictionary<int, (int ver, long h)> nearHash = new Dictionary<int, (int, long)>();
    static long Mix(long h, float f) => unchecked((h ^ (uint)System.BitConverter.SingleToInt32Bits(f)) * 1099511628211L);
    static long Mix(long h, Vector3 v) => Mix(Mix(Mix(h, v.x), v.y), v.z);
    static long MixL(long h, long x) => unchecked((h ^ x) * 1099511628211L);
    static long MixSegs(long h, List<Seg> l)
    {
        h = MixL(h, l.Count);
        foreach (var q in l) { h = Mix(h, q.a); h = Mix(h, q.b); h = Mix(h, q.ha); h = Mix(h, q.hb); h = Mix(h, q.hw); h = Mix(h, q.reach); h = MixL(h, ((long)q.ia << 32) ^ (uint)q.ib); h = MixL(h, (q.capA ? 1 : 0) | (q.capB ? 2 : 0) | (q.wall ? 4 : 0) | (q.tap0 ? 8 : 0) | (q.tap1 ? 16 : 0) | (q.hardA ? 32 : 0) | (q.hardB ? 64 : 0) | (q.bak ? 128 : 0)); }
        return h;
    }
    // de stukken en koppen in de buurt van vak i (UseNear) als één getal; per verzameling stukken (fieldVer) een keer
    static long NearHash(int i)
    {
        if (!fieldCells.Contains(i)) return 0;
        if (nearHash.TryGetValue(i, out var e) && e.ver == fieldVer) return e.h;
        var su = segsUse; var fu = flatsUse; var cu = capsUse; long h = 1469598103934665603L;
        try
        {
            UseNear(i); h = MixSegs(h, segsUse); h = MixSegs(h, flatsUse); h = MixL(h, capsUse.Count);
            foreach (var q in capsUse) { h = Mix(h, q.c); h = Mix(h, q.g); h = Mix(h, q.h); h = Mix(h, q.hw); h = Mix(h, q.wo); h = Mix(h, q.reach); h = MixL(h, q.ic * 2 + (q.wall ? 1 : 0)); if (q.n != null) foreach (var n in q.n) h = Mix(h, n); }
        }
        finally { segsUse = su; flatsUse = fu; capsUse = cu; }
        if (h == 0) h = 1; nearHash[i] = (fieldVer, h); return h;
    }
    static (int ver, long h) allFlats = (-1, 0);
    static long AllFlatsHash() { if (allFlats.ver != fieldVer) allFlats = (fieldVer, MixSegs(1469598103934665603L, flats)); return allFlats.h; }
    // De sleutel van vak i; false: een gewoon vak (geen weg, geul of dek in de buurt), dat is zo getekend en wordt niet onthouden
    static bool CellKey(int i, bool plat, System.Func<int, bool> skip, bool walls, out long key)
    {
        var c = Planet.Cells[i]; int m = c.corners.Length; key = 0;
        bool inField = fieldCells.Contains(i), field = inField && NearSeg(c.dir, c.radius);
        var near = cuts.Count > 0 && !plat ? CutsNear(c) : null; bool cn = near != null && near.Count > 0;
        if (!field && !cn) return false;
        long h = 1469598103934665603L; float g = G(i);
        h = MixL(h, i); h = MixL(h, (plat ? 1 : 0) | (walls ? 2 : 0) | (field ? 4 : 0) | (cn ? 8 : 0) | (RoadShape ? 32 : 0) | (RoadSlopes ? 64 : 0) | (carved.Count > 0 ? 1024 : 0));
        h = Mix(h, FieldK); h = Mix(h, g); h = Mix(h, plat ? g : Center2(i));
        int sk = 0; for (int k = 0; k < m; k++) { if (skip(k)) sk |= 1 << k; h = Mix(h, plat ? g : CornerBase(i, k)); } h = MixL(h, sk);
        if (inField) h = MixL(h, NearHash(i)); else if (RoadShape) h = MixL(h, AllFlatsHash());
        if (cuts.Count > 0) { h = MixL(h, cutsHash); h = MixL(h, fillN); }
        if (!plat) for (int k = 0; k < m; k++)
        {
            if ((sk & (1 << k)) != 0) continue; var pr = EdgeProfile(i, k); h = MixL(h, pr.Count);
            foreach (var ep in pr) { h = Mix(h, ep.t); h = Mix(h, ep.h); h = MixL(h, ep.side); }
        }
        key = h; return true;
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
            var ls = new List<Seg>(); var lc = new List<Cap>();
            List<Lin> fls = null; G2SwLines.Start(); if (field) { fls = FieldLines(c, k, hc0, h1, h2, ls, lc); G2Lines += fls.Count; } G2SwLines.Stop();   // (de lijnen snijden pas waar een stuk niet vlak is, zie hieronder)
            bool fieldT = field && !(ls.Count == 0 && lc.Count == 0 && flatsUse.Count == 0);   // (geen stuk kan deze driehoek halen: gewoon het terreinvlak, zonder snijden en samenvoegen)
            var wg = new List<int>(); var wnq = new List<bool>(); var wl = fieldT ? WallLins(c, ls, lc, wg, wnq) : new List<Lin>(); if (fls != null) foreach (var f in wl) Split(f);   // (een wand is een sprong, daar moet een zijde van een stuk precies op liggen: altijd vooraf snijden)   // de wanden hier (sokkels en bakken), binnenkant negatief; wg: per lijn de strook of kop
            Lin B2 = new Lin { a = hc0, x = ((h1 - hc0) * L2.y - (h2 - hc0) * L1.y) / det, y = ((h2 - hc0) * L1.x - (h1 - hc0) * L2.x) / det };
            if (cn) foreach (var n in near) foreach (var f in new[] { n.sd - Lin.C(-0.01f), n.sd - Lin.C(n.fl), n.sd - Lin.C(n.fl + n.rl), n.sd - Lin.C(n.end), n.t - Lin.C(n.q.hw), n.t - Lin.C(-n.q.hw) }) Split(f);
            var prof = plat ? null : EdgeProfile(i, k);
            triSegs = null; triCaps = null; if (fieldT) { triSegs = ls; triCaps = lc; }   // vanaf hier: de hoogte met alleen de stukken van deze driehoek
            Vector2 E = L2 - L1; float el2 = E.sqrMagnitude, el = Mathf.Sqrt(el2);
            float TOf(Vector2 q) => Vector2.Dot(q - L1, E) / el2;
            bool OnEdge(Vector2 q) => Mathf.Abs((E.x * (q.y - L1.y) - E.y * (q.x - L1.x)) / el) < 1e-3f;
            Vector3 Dir(Vector2 q) { float b1 = (q.x * L2.y - q.y * L2.x) / det, b2 = (L1.x * q.y - L1.y * q.x) / det; return (D0 * (1f - b1 - b2) + D1 * b1 + D2 * b2).normalized; }
            float Base(Vector2 q) { Vector3 d = Dir(q); float dn = Vector3.Dot(d, Nrm); return Mathf.Abs(dn) < 1e-9f ? hc0 : ND / dn - Planet.R; }   // waar de straal het vlak raakt
            // Elke veelhoek langs de snijlijnen van de vlakken die er gelden (PlaneLins, gemeten in het midden), tot er niets meer te
            // snijden is: dan is elk stuk één vlak (zoals ExactGround voor de geulen). Daarna tekenen.
            {
                G2SwPlanes.Start(); var done = new List<List<Vector2>>(); var work = new Stack<List<Vector2>>(polys); int guard = 0;
                while (work.Count > 0)
                {
                    var pl = work.Pop(); Vector2 ctr = Vector2.zero; foreach (var v in pl) ctr += v; ctr /= pl.Count;
                    bool split = false;
                    if (++guard < 4000 && (fieldT || cn))
                    {
                        var fs = PlaneLins(c, B2, ls, lc, cn ? near : null, ctr);
                        // Snel: ligt het hele stuk al in het vlak dat in zijn midden geldt (gemeten vlak bij elk hoekpunt en
                        // elk midden van een zijde, binnen 4 mm: dezelfde maat als het samenvoegen), dan valt er niets te snijden. Zo niet:
                        // eerst de knik tussen dat vlak en het vlak dat op het afwijkende punt geldt. Zonder dit sneed elk paar vlakken van
                        // alle stukken weg in de buurt het vak in repen, die het samenvoegen daarna weer aan elkaar moest zetten.
                        {
                            // de meetpunten: vlak bij elk hoekpunt en elk midden van een zijde, en binnenin een raster van 1,2 m (een smal pad dwars door een groot stuk)
                            float hcq = HQ(ctr); int gi = -1; for (int x = 0; x < fs.Count; x++) if (Mathf.Abs(fs[x].At(ctr) - hcq) < 0.004f) { gi = x; break; }
                            bool flat = gi >= 0; Vector2 bad = ctr;
                            if (flat)
                            {
                                Vector2 lo = pl[0], hi = pl[0];
                                for (int v = 0; v < pl.Count && flat; v++)
                                {
                                    lo = Vector2.Min(lo, pl[v]); hi = Vector2.Max(hi, pl[v]);
                                    Vector2 q1 = Vector2.Lerp(pl[v], ctr, 0.04f), q2 = Vector2.Lerp((pl[v] + pl[(v + 1) % pl.Count]) * 0.5f, ctr, 0.04f);
                                    if (Mathf.Abs(fs[gi].At(q1) - HQ(q1)) > 0.004f) { flat = false; bad = q1; } else if (Mathf.Abs(fs[gi].At(q2) - HQ(q2)) > 0.004f) { flat = false; bad = q2; }
                                }
                                const float St = 1.2f;
                                if (flat && (hi.x - lo.x > St || hi.y - lo.y > St))
                                    for (float gx = lo.x + St * 0.5f; gx < hi.x && flat; gx += St) for (float gy = lo.y + St * 0.5f; gy < hi.y && flat; gy += St)
                                    {
                                        Vector2 q = new Vector2(gx, gy); bool inside = true; int sgn = 0;
                                        for (int v = 0; v < pl.Count && inside; v++) { Vector2 e = pl[(v + 1) % pl.Count] - pl[v], w = q - pl[v]; float cr = e.x * w.y - e.y * w.x; if (Mathf.Abs(cr) < 1e-6f) continue; int sn = cr > 0f ? 1 : -1; if (sgn == 0) sgn = sn; else if (sn != sgn) inside = false; }
                                        if (inside && Mathf.Abs(fs[gi].At(q) - HQ(q)) > 0.004f) { flat = false; bad = q; }
                                    }
                                if (flat) { done.Add(pl); continue; }
                            }
                            // niet vlak: eerst een lijn van een wegstuk die het midden van het afwijkende punt scheidt (de wegrand, een wand, de voet
                            // van een talud), dan de knik tussen het vlak in het midden en het vlak op het afwijkende punt, dan een andere lijn
                            if (fls != null) for (int x = 0; x < fls.Count && !split; x++)
                            {
                                if ((fls[x].At(ctr) < 0f) == (fls[x].At(bad) < 0f)) continue;
                                var tmp = new List<List<Vector2>>(); if (SplitBy(pl, fls[x], tmp)) { foreach (var part in tmp) work.Push(part); split = true; }
                            }
                            if (gi >= 0 && !split)
                            {
                                float hb = HQ(bad);
                                for (int x = 0; x < fs.Count && !split; x++)
                                {
                                    if (x == gi || Mathf.Abs(fs[x].At(bad) - hb) > 0.004f) continue;
                                    var tmp = new List<List<Vector2>>(); if (SplitBy(pl, fs[gi] - fs[x], tmp)) { foreach (var part in tmp) work.Push(part); split = true; }
                                }
                            }
                            if (fls != null) for (int x = 0; x < fls.Count && !split; x++)
                            {
                                var tmp = new List<List<Vector2>>(); if (SplitBy(pl, fls[x], tmp)) { foreach (var part in tmp) work.Push(part); split = true; }
                            }
                        }
                        for (int x = 0; x < fs.Count && !split; x++) for (int y = x + 1; y < fs.Count && !split; y++)
                        {
                            var tmp = new List<List<Vector2>>(); if (SplitBy(pl, fs[x] - fs[y], tmp)) { foreach (var part in tmp) work.Push(part); split = true; }
                        }
                    }
                    if (!split) done.Add(pl);
                }
                polys = done;
            }
            G2SwPlanes.Stop(); G2Polys += polys.Count; G2SwMerge.Start();
            // Samenvoegen (ontwerp 8.5 punt 3): de snijlijnen zijn oneindige lijnen van alle wegstukken binnen bereik, dus ook waar de
            // hoogtefunctie niet knikt is de grond in repen gesneden (een spinnenweb van dunne driehoeken). Twee veelhoeken met een hele
            // zijde gemeen die in hetzelfde vlak liggen (binnenpunten van de een op het vlak van de ander, binnen 4 mm) en samen bol
            // blijven worden weer één veelhoek. Een wand of knik scheidt ze vanzelf: dan klopt het vlak niet.
            if (polys.Count > 1 && (fieldT || cn))
            {
                var P2 = polys; var Hn = new List<List<Vector3>>(P2.Count);   // per veelhoek: binnenpunten (x, y, hoogte)
                foreach (var pl in P2)
                {
                    Vector2 ctr = Vector2.zero; foreach (var v in pl) ctr += v; ctr /= pl.Count; var l = new List<Vector3>(pl.Count + 1);   // een punt bij elk hoekpunt en het midden (met minder punten werden stukken samengevoegd die net niet in één vlak lagen)
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
                    // een heen-en-terug (meer dan één zijde gemeen: a, b, a) weg
                    for (bool again = true; again && r.Count > 3;)
                    {
                        again = false;
                        for (int k = 0; k < r.Count; k++) if (Same(r[(k + r.Count - 1) % r.Count], r[(k + 1) % r.Count])) { int k1 = (k + 1) % r.Count; if (k1 > k) { r.RemoveAt(k1); r.RemoveAt(k); } else { r.RemoveAt(k); r.RemoveAt(k1); } again = true; break; }
                    }
                    // punten op een rechte lijn weg; dan moet de rest bol zijn (één draairichting), of zonder wanden een enkelvoudige veelhoek (geen punt twee keer)
                    for (int k = r.Count - 1; k >= 0 && r.Count > 3; k--)
                    {
                        Vector2 u = r[k] - r[(k + r.Count - 1) % r.Count], w = r[(k + 1) % r.Count] - r[k];
                        if (Mathf.Abs(u.x * w.y - u.y * w.x) < 1e-4f * Mathf.Max(1e-3f, u.magnitude * w.magnitude) && Vector2.Dot(u, w) > 0f) r.RemoveAt(k);
                    }
                    if (r.Count < 3) return null; int sg = 0;
                    for (int k = 0; k < r.Count; k++)
                    {
                        Vector2 u = r[k] - r[(k + r.Count - 1) % r.Count], w = r[(k + 1) % r.Count] - r[k]; float cr = u.x * w.y - u.y * w.x;
                        if (Mathf.Abs(cr) < 1e-5f) continue; int sn = cr > 0f ? 1 : -1; if (sg == 0) sg = sn; else if (sn != sg) { if (wl.Count > 0) return null; sg = 2; break; }
                    }
                    if (sg == 2) { if (r.Count > 40) return null; for (int x = 0; x < r.Count; x++) for (int y = x + 1; y < r.Count; y++) if (Same(r[x], r[y])) return null; }   // niet bol: alleen als hij zichzelf nergens raakt
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
                            P2[b] = j; P2[a] = null; merged = true; any = true; if (Hn[b].Count < 24) Hn[b].AddRange(Hn[a]);   // (de punten van beide, tot 24: zo blijft een samengevoegd stuk vlak zonder dat de toets steeds langer duurt)
                        }
                    }
                    if (!any) break;
                }
                var keep = new List<List<Vector2>>(); foreach (var pl in P2) if (pl != null) keep.Add(pl); polys = keep;
            }
            G2SwMerge.Stop();
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
            float HQ(Vector2 q) => H2(Dir(q), Base(q), fieldT, cn);
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
            G2SwPiece.Start(); foreach (var pl0 in polys) Piece(pl0); G2SwPiece.Stop();
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
                float HOf(int v, float bs) { if (pl[v].t >= 0f) return ProfileAt(prof, pl[v].t, bs, pl[v].t0); float ob = wallBias; wallBias = bs; try { return H2(Dir(pl[v].q), Base(pl[v].q), fieldT, cn); } finally { wallBias = ob; } }
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
                // In driehoeken: geen waaier vanuit één hoekpunt (met de ingevoegde punten op de zijden gaf dat bundels naalddunne driehoeken),
                // maar steeds het oor afknippen waarvan de omgeschreven cirkel geen ander hoekpunt bevat (Delaunay: de stompst mogelijke
                // driehoeken voor deze punten); is er geen, dan het oor met de grootste kleinste hoek. Drie punten op een lijn zijn geen oor.
                var ring = new List<int>(n); for (int v = 0; v < n; v++) ring.Add(v);
                float area = 0f; for (int v = 0; v < n; v++) { Vector2 a = pl[v].q, b = pl[(v + 1) % n].q; area += a.x * b.y - a.y * b.x; }
                float orient = area >= 0f ? 1f : -1f;
                float MinAng(Vector2 a, Vector2 b, Vector2 cc) { Vector2 ab = (b - a).normalized, bc = (cc - b).normalized, ca = (a - cc).normalized; return Mathf.Min(-Vector2.Dot(ab, ca), Mathf.Min(-Vector2.Dot(bc, ab), -Vector2.Dot(ca, bc))); }   // (de grootste cosinus hoort bij de kleinste hoek: kleiner is beter)
                while (ring.Count > 3)
                {
                    int rn = ring.Count, bestE = -1; float bestA = float.MaxValue; bool bestD = false;
                    for (int e = 0; e < rn; e++)
                    {
                        Vector2 a = pl[ring[(e + rn - 1) % rn]].q, b = pl[ring[e]].q, cc = pl[ring[(e + 1) % rn]].q;
                        float ar = (b.x - a.x) * (cc.y - a.y) - (b.y - a.y) * (cc.x - a.x), ln = Mathf.Max((b - a).sqrMagnitude, Mathf.Max((cc - b).sqrMagnitude, (a - cc).sqrMagnitude));
                        if (Mathf.Abs(ar) < 1e-4f * ln || ar * orient < 0f) continue;   // op een lijn, of een holle hoek
                        bool empty = true;   // geen ander punt van de ring in de driehoek of op de nieuwe zijde (een samengevoegd stuk kan hol zijn)
                        for (int x = 0; x < rn && empty; x++)
                        {
                            if (x == e || x == (e + rn - 1) % rn || x == (e + 1) % rn) continue; Vector2 q = pl[ring[x]].q;
                            float s1 = ((b.x - a.x) * (q.y - a.y) - (b.y - a.y) * (q.x - a.x)) * orient, s2 = ((cc.x - b.x) * (q.y - b.y) - (cc.y - b.y) * (q.x - b.x)) * orient, s3 = ((a.x - cc.x) * (q.y - cc.y) - (a.y - cc.y) * (q.x - cc.x)) * orient;
                            if (s1 > 1e-5f && s2 > 1e-5f && s3 > -1e-5f) empty = false;
                        }
                        if (!empty) continue;
                        // de omgeschreven cirkel van (a, b, cc): ligt er een ander punt van de ring binnen?
                        float d = 2f * ar, a2 = a.sqrMagnitude, b2 = b.sqrMagnitude, c2 = cc.sqrMagnitude;
                        Vector2 o = new Vector2((a2 * (b.y - cc.y) + b2 * (cc.y - a.y) + c2 * (a.y - b.y)) / d, (a2 * (cc.x - b.x) + b2 * (a.x - cc.x) + c2 * (b.x - a.x)) / d); float rr = (a - o).sqrMagnitude * (1f - 1e-4f);
                        bool del = true; for (int x = 0; x < rn && del; x++) { if (x == e || x == (e + rn - 1) % rn || x == (e + 1) % rn) continue; if ((pl[ring[x]].q - o).sqrMagnitude < rr) del = false; }
                        float ma = MinAng(a, b, cc);
                        if ((del && !bestD) || (del == bestD && ma < bestA)) { bestE = e; bestA = ma; bestD = del; }
                    }
                    if (bestE < 0) break;   // alles op een lijn: niets te tekenen
                    int ia = ring[(bestE + rn - 1) % rn], ib = ring[bestE], ic = ring[(bestE + 1) % rn];
                    Refine2(pl[ia].q, pl[ib].q, pl[ic].q, P[ia], P[ib], P[ic], H[ia], H[ib], H[ic], 0f, 0); ring.RemoveAt(bestE);
                }
                if (ring.Count == 3) Refine2(pl[ring[0]].q, pl[ring[1]].q, pl[ring[2]].q, P[ring[0]], P[ring[1]], P[ring[2]], H[ring[0]], H[ring[1]], H[ring[2]], 0f, 0);
            }
            void Refine2(Vector2 a, Vector2 b, Vector2 cc, Vector3 Pa, Vector3 Pb, Vector3 Pc, float ha, float hb, float hc2, float bias, int depth)
            {
                Vector2 m2 = (a + b + cc) / 3f; Vector3 dm = Dir(m2); float ob = wallBias; wallBias = bias; float hm; bool steep; try { hm = H2(dm, Base(m2), fieldT, cn, out steep); } finally { wallBias = ob; }
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
