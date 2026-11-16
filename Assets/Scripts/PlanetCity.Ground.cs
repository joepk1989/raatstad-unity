using System.Collections.Generic;
using UnityEngine;

// De bodem: Init, raster, evenaar, BuildGround per deel (met groundCache) en de poolpaal.
public static partial class PlanetCity
{
    // ---------- bodem ----------
    static readonly Dictionary<int, List<GameObject>> groundChunks = new Dictionary<int, List<GameObject>>();
    public static void Init(Transform parent)
    {
        root = new GameObject("Planeetstad").transform; root.SetParent(parent, false);
        groundRoot = new GameObject("Bodem").transform; groundRoot.SetParent(parent, false);
        chunks.Clear(); groundChunks.Clear();
        for (int c0 = 0; c0 < Planet.Cells.Length; c0 += ChunkN) BuildGround(c0 / ChunkN);
        BuildGrid(parent); BuildEquator(parent); RoadsInit(parent);
    }

    // Rasterlijnen langs alle vakranden (één lijnenmesh, 6 cm boven de bodem). G = aan/uit; in wandelmodus verborgen.
    static GameObject grid; public static bool GridOn = true;
    static void BuildGrid(Transform parent)
    {
        grid = new GameObject("Raster"); grid.transform.SetParent(parent, false); grid.AddComponent<MeshFilter>().sharedMesh = GridMesh();
        var mr = grid.AddComponent<MeshRenderer>(); mr.sharedMaterial = Raatstad.MatGrid; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
    }
    // elk vak tekent zijn eigen rand op zijn eigen hoeken (een plateau op zijn hoogte, het maaiveld op de hoogte van de hoekpunten)
    static Mesh GridMesh()
    {
        var v = new List<Vector3>(); var idx = new List<int>(); gridAt = new int[Planet.Cells.Length];
        try
        {
        for (int i = 0; i < Planet.Cells.Length; i++)
        {
            if (fieldCells.Contains(i)) UseNear(i); else UseAll();   // alleen de wegen in de buurt (zoals BuildGround)
            var c = Planet.Cells[i]; int m = c.corners.Length; gridAt[i] = v.Count;
            for (int k = 0; k < m; k++) { idx.Add(v.Count); v.Add(c.corners[k] * (Planet.R + OwnCorner(i, k) + 0.06f)); idx.Add(v.Count); v.Add(c.corners[(k + 1) % m] * (Planet.R + OwnCorner(i, (k + 1) % m) + 0.06f)); }
        }
        }
        finally { UseAll(); }
        gridV = v.ToArray();
        var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; mesh.SetVertices(v); mesh.SetIndices(idx, MeshTopology.Lines, 0); mesh.RecalculateBounds(); return mesh;
    }
    // De evenaar: de rand tussen de vakken van het noordelijk en het zuidelijk halfrond (het vlak midden tussen de twee polen),
    // langs de vakranden, dus volgens het raster. Rood-wit per rand, altijd zichtbaar (ook als het raster uit staat).
    static GameObject equator;
    public static Vector3 Axis => Planet.Axis;
    public static bool NorthSide(int i) => Vector3.Dot(Planet.Cells[i].dir, Axis) > -1e-5f;   // precies op het vlak telt als noord
    // breedte (+ noord, - zuid) en lengte (+ oost, - west) in graden; lengte 0 ligt in de richting van de x-as
    public static void LatLon(Vector3 d, out float lat, out float lon)
    {
        Vector3 ax = Axis, x0 = Vector3.right - ax * Vector3.Dot(Vector3.right, ax); if (x0.sqrMagnitude < 1e-6f) x0 = Vector3.forward - ax * Vector3.Dot(Vector3.forward, ax);
        x0.Normalize(); Vector3 y0 = Vector3.Cross(ax, x0); d = d.normalized;
        lat = Mathf.Asin(Mathf.Clamp(Vector3.Dot(d, ax), -1f, 1f)) * Mathf.Rad2Deg; lon = Mathf.Atan2(Vector3.Dot(d, y0), Vector3.Dot(d, x0)) * Mathf.Rad2Deg;
    }
    static void BuildEquator(Transform parent)
    {
        equator = new GameObject("Evenaar"); equator.transform.SetParent(parent, false); RebuildEquator();
    }
    static void RebuildEquator()
    {
        if (equator == null) return; Topo();
        foreach (Transform t in equator.transform) UnityEngine.Object.Destroy(t.gameObject);
        var red = new MeshBuilder(); var white = new MeshBuilder(); int n = 0;
        for (int i = 0; i < Planet.Cells.Length; i++)
        {
            if (!NorthSide(i)) continue; var c = Planet.Cells[i]; int m = c.corners.Length;
            for (int k = 0; k < m; k++)
            {
                if (NorthSide(edN[i][k])) continue;
                Vector3 a = c.corners[k] * (Planet.R + OwnCorner(i, k) + 0.09f), b = c.corners[(k + 1) % m] * (Planet.R + OwnCorner(i, (k + 1) % m) + 0.09f), up = (a + b).normalized;
                Vector3 s = Vector3.Cross(up, b - a).normalized * 0.45f;
                (n++ % 2 == 0 ? red : white).Quad(a - s, b - s, b + s, a + s, up);
            }
        }
        foreach (var (mb, key, col) in new[] { (red, "rood", new Color(0.85f, 0.16f, 0.14f)), (white, "wit", new Color(0.97f, 0.97f, 0.95f)) })
        {
            if (mb.v.Count == 0) continue;
            var g = new GameObject("Evenaar " + key); g.transform.SetParent(equator.transform, false);
            g.AddComponent<MeshFilter>().sharedMesh = mb.Build(); var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = Mats.GetUnlit("p_evenaar_" + key, col); mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
    static bool gridDirty;
    public static void RebuildGrid()   // het raster volgt het terrein en de bebouwing; uit staat het niet bij te werken, dat gebeurt bij aanzetten
    {
        RebuildEquator();
        if (grid == null) return; if (!grid.activeSelf) { gridDirty = true; return; }
        gridDirty = false; var mf = grid.GetComponent<MeshFilter>();
        if (gridV != null && mf.sharedMesh != null && lastBuilt.Count * ChunkN < Planet.Cells.Length / 2)   // alleen de vakken van de delen die net opnieuw getekend zijn
        {
            try { foreach (int ch in lastBuilt) for (int i = ch * ChunkN; i < Mathf.Min(Planet.Cells.Length, (ch + 1) * ChunkN); i++) { if (fieldCells.Contains(i)) UseNear(i); else UseAll(); GridCell(i, gridV); } }
            finally { UseAll(); }
            mf.sharedMesh.SetVertices(gridV); lastBuilt.Clear(); return;   // de omvang blijft die van de bol
        }
        lastBuilt.Clear(); var old = mf.sharedMesh; mf.sharedMesh = GridMesh(); if (old != null) UnityEngine.Object.Destroy(old);
    }
    static readonly HashSet<int> lastBuilt = new HashSet<int>();   // de delen die sinds het laatste raster opnieuw getekend zijn
    static Vector3[] gridV; static int[] gridAt;   // de rasterpunten, en per vak waar die van dat vak beginnen
    static void GridCell(int i, Vector3[] v)
    {
        var c = Planet.Cells[i]; int m = c.corners.Length, at = gridAt[i];
        for (int k = 0; k < m; k++) { v[at + 2 * k] = c.corners[k] * (Planet.R + OwnCorner(i, k) + 0.06f); v[at + 2 * k + 1] = c.corners[(k + 1) % m] * (Planet.R + OwnCorner(i, (k + 1) % m) + 0.06f); }
    }
    public static void ShowGrid(bool on) { if (grid == null) return; grid.SetActive(on); if (on && gridDirty) RebuildGrid(); }

    // De bodem van één deel. Vakken met een meer of een uitgraving krijgen geen bodem (die zit er anders doorheen).
    struct GTri { public string tag; public Vector3 a, b, c; }
    static readonly Dictionary<int, (long key, List<GTri> tris)> groundCache = new Dictionary<int, (long, List<GTri>)>();
    static long cutsHash;
    // alles waar de grond van vak i langs een weg op een niveau van afhangt, als één getal
    static long FieldKey(int i, float hcb0, float[] hb0, bool special)
    {
        long h = 1469598103934665603L;
        void F(float f) { h = unchecked((h ^ (uint)System.BitConverter.SingleToInt32Bits(f)) * 1099511628211L); }
        void V(Vector3 v) { F(v.x); F(v.y); F(v.z); }
        F(i); F(hcb0); foreach (float x in hb0) F(x); F(FieldK); F(special ? 1f : 0f); F((RoadShape ? 1f : 0f) + (RoadSlopes ? 2f : 0f));   // de schakelaars Terrein door wegen en Talud F(carved.Count > 0 && cuts.Count > 0 ? 1f : 0f); h ^= cutsHash;
        foreach (var q in segsUse) { V(q.a); V(q.b); F(q.ha); F(q.hb); F(q.hw); F(q.capA ? 1f : 0f); F(q.capB ? 1f : 0f); }
        foreach (var q in flatsUse) { V(q.a); V(q.b); F(q.ha); F(q.hb); F(q.hw); }
        foreach (var q in capsUse) { V(q.c); F(q.h); F(q.hw); }
        return h;
    }
    static void BuildGround(int ch) { try { BuildGround0(ch); } finally { UseAll(); } }   // na het tekenen weer alle wegstukken (UseNear)
    const float SkirtD = 10f;   // hoe diep een naad langs de rand van een vak naar beneden hangt (Skirt in BuildGround0)
    public static bool Seams = true;   // de naden aan (uit: om te zien waar het terrein niet aansluit)
    // vakken waar de grond door wegen, geulen of tunnels gesneden wordt, en hun buren: daar kan een gedeelde rand van twee kanten anders uitvallen
    static bool SeamCell(int i)
    {
        if (!Seams) return false;
        if (fieldCells.Contains(i) || boreNear.ContainsKey(i) || spanNear.ContainsKey(i) || (carved.Count > 0 && carved.Contains(i)) || IsWalk(i)) return true;
        foreach (int j in Planet.Cells[i].nb) if (fieldCells.Contains(j) || (carved.Count > 0 && carved.Contains(j)) || IsWalk(j) || Plateau(j)) return true;   // ook naast een weg of een bebouwd vak (een half wegvak ligt soms lager dan de hoek ernaast)
        return false;
    }
    // heeft vak j geen eigen bodem (een gracht, uitgraving of de geul onder een brug)? Daar geen naad naartoe.
    static bool NoGround(int j) => (Cells.TryGetValue(j, out var s) && s.Count > 0 && (s[0] == "water" || s[0] == "kuil"))
        || (Under.TryGetValue(j, out var u) && (u == "water" || u == "kuil" || u == "pad" || u == "weg") && !FieldBridge(j));
    static void BuildGround0(int ch)
    {
        { long h = 17; foreach (var q in cuts) h = unchecked(h * 31 + (q.p.GetHashCode() ^ q.floor.GetHashCode() * 7 ^ q.ground.GetHashCode() * 13 ^ q.hw.GetHashCode() * 17 ^ q.skew.GetHashCode() * 19 ^ q.flat.GetHashCode() * 23 ^ q.ramp.GetHashCode() * 29 ^ q.end.GetHashCode() * 37 ^ (q.fill ? 41 : 0) ^ q.o.GetHashCode() * 43 ^ q.t.GetHashCode() * 47)); cutsHash = h; }
        if (groundChunks.TryGetValue(ch, out var olds)) { foreach (var g in olds) if (g != null) UnityEngine.Object.Destroy(g); }
        var made = new List<GameObject>(); groundChunks[ch] = made;
        int n = Planet.Cells.Length, c0 = ch * ChunkN;
        {
            // De bodem per vak; de kleur loopt van gewoon gras (regelmatige zeshoek) naar fel oranje (het meest vervormd, naast een vijfhoek)
            const int Steps = 8; var ms = new MeshBuilder[Steps]; for (int k = 0; k < Steps; k++) ms[k] = new MeshBuilder();
            var pm = new MeshBuilder();   // de vijfhoeken zelf
            var sm = new MeshBuilder();   // de startplekken
            var polar = new Dictionary<string, MeshBuilder>();   // de polen, per kleur
            System.Func<string, MeshBuilder> PM = k => { if (!polar.TryGetValue(k, out var x)) { x = new MeshBuilder(); polar[k] = x; } return x; };
            var terrB = new Dictionary<string, MeshBuilder>();   // verhoogd terrein per hoogteband, aarde op steile flanken, sokkels
            System.Func<string, MeshBuilder> TB = k => { if (!terrB.TryGetValue(k, out var x)) { x = new MeshBuilder(); terrB[k] = x; } return x; };
            var soil = SoilMat() != null ? new MeshBuilder() : null; var soilCell = new List<int>();   // de bodem met textures, als één mesh (PlanetSoil.cs)
            Topo();
            for (int i = c0; i < Mathf.Min(n, c0 + ChunkN); i++)
            {
                if (fieldCells.Contains(i)) UseNear(i); else UseAll();
                var c = Planet.Cells[i]; int m = c.corners.Length; bool pent = m == 5;
                var bn = boreNear.Count > 0 && boreNear.TryGetValue(i, out var bl) ? bl : null;   // een tunnel door een berg hier: het terrein in het betonblok weg (BoreTri)
                bool cut = (Cells.TryGetValue(i, out var stack) && stack.Count > 0 && (stack[0] == "water" || stack[0] == "kuil"))
                        || (Under.TryGetValue(i, out var und) && (und == "water" || und == "kuil" || und == "pad" || und == "weg")) && !FieldBridge(i);   // gracht, uitgraving of brug daarover: geen bodem (een meer is gewoon diep terrein, PlanetNature.cs)
                float g = G(i), RG = Planet.R + g; bool plat = FlatGround(i);
                // hoogte van het midden en de hoeken; onder een weg of pad een kom tot de laagste hoek, zodat het maaiveld nooit door de hellende weg steekt
                var hk = new float[m]; for (int k = 0; k < m; k++) hk[k] = OwnCorner(i, k);
                float hc = g + CenterOfs1(i); if (IsWalk(i) && !carved.Contains(i) && !levelled.Contains(i)) foreach (float x in hk) hc = Mathf.Min(hc, x);
                if (cuts.Count > 0) hc = Mathf.Min(hc, Carve(c.dir));
                bool hasDeck = deckSide.TryGetValue(i, out var dk);
                float kk = Mathf.Pow(Mathf.Clamp01((c.distort - 0.04f) / 0.125f), 1.4f);
                bool start = System.Array.IndexOf(Planet.Starts, i) >= 0, pole = Planet.IsPole(i), special = pole || pent || start;
                var mb0 = pole ? PM("ijs") : pent && Dev.Pentagons ? pm : start && Dev.Starts ? sm : Dev.Distort ? ms[Mathf.Clamp(Mathf.RoundToInt(kk * (Steps - 1)), 0, Steps - 1)] : ms[0];   // vervorming en vijfhoeken alleen als ontwikkelaarshulp (Dev)
                if (!cut)
                {
                    Vector3 mid = c.dir * (Planet.R + hc);
                    void Emit(Vector3 P0, Vector3 P1, Vector3 P2, float h0, float h1, float h2) => Emit2(P0, P1, P2, h0, h1, h2, false);
                    List<GTri> rec = null;   // de driehoekjes van een vak langs een weg op een niveau, om te onthouden (groundCache)
                    void Put(string tag, Vector3 P0, Vector3 P1, Vector3 P2)   // naar de bodemmesh (met het vak bij elk hoekpunt) of naar het materiaal van de soort
                    {
                        PutTri(tag, P0, P1, P2, c.dir);
                    }
                    void PutTri(string tag, Vector3 P0, Vector3 P1, Vector3 P2, Vector3 outw)
                    {
                        if (soil != null && SoilTag(tag) && (tag != null || mb0 == ms[0])) { int v0 = soil.v.Count; BoreTri(soil, P0, P1, P2, outw, bn); for (int q = v0; q < soil.v.Count; q++) soilCell.Add(i); }
                        else BoreTri(tag == null ? mb0 : TB(tag), P0, P1, P2, outw, bn);
                    }
                    void Emit2(Vector3 P0, Vector3 P1, Vector3 P2, float h0, float h1, float h2, bool steep)   // steep: op de schuine kant van een weg (gras, of aarde als het steiler is dan 40°)
                    {
                        string tag = null; float up = Mathf.Abs(Vector3.Dot(Vector3.Cross(P1 - P0, P2 - P0).normalized, c.dir));
                        float hdif = Mathf.Max(h0, Mathf.Max(h1, h2)) - Mathf.Min(h0, Mathf.Min(h1, h2));
                        Vector3 nrm = Vector3.Cross(P1 - P0, P2 - P0); float area2 = nrm.magnitude, lmax = Mathf.Max((P1 - P0).magnitude, Mathf.Max((P2 - P1).magnitude, (P0 - P2).magnitude));
                        if (RoadH.Count > 0 && up < 0.12f && hdif > 1.5f && area2 > 0.2f * lmax * lmax) tag = "sokkel";   // keermuur (nieuwe opbouw): een loodrechte kant van meer dan anderhalve meter is steen (niet een splinter: die heeft geen echte normaal)
                        else if (steep) { if (up < SteepUp) tag = "aarde"; }   // een talud is gras, alleen waar het steiler is dan 40° kale aarde
                        else if (!special && (Mathf.Max(h0, Mathf.Max(h1, h2)) > 0.03f || Mathf.Min(h0, Mathf.Min(h1, h2)) < -0.03f))   // verhoogd: kleur naar de hoogte, aarde waar het steiler is dan zo'n 45°
                            tag = up < 0.72f ? "aarde" : "t" + Mathf.Clamp(Mathf.FloorToInt((h0 + h1 + h2) / 9f), 0, 29);
                        if (!steep) tag = NatureTag(i, tag, h0, h1, h2, up);   // rots, sneeuw, strand en zeebodem (PlanetNature.cs)
                        Put(tag, P0, P1, P2); rec?.Add(new GTri { tag = tag, a = P0, b = P1, c = P2 });
                    }
                    // het vak uit de hoogtefunctie, de randpunten gedeeld met de buren (PlanetGround2.cs)
                    Ground2Cell(i, Plateau(i), Emit2, k => { if (!hasDeck) return false; Vector3 dw = (c.corners[k] + c.corners[(k + 1) % m]).normalized * Planet.R - dk.p; return Vector3.Dot(dw, dk.o) - dk.skew * Vector3.Dot(dw, dk.t) < -0.5f; },
                        (wa, wb, wc, wd, outw) => TB("sokkel").Quad(wa, wb, wc, wd, outw));   // de wanden van sokkels en bakken (ontwerp 8.5): eigen quads, steen
                }
                if (cut && Underpass(i))   // brugvak: het stuk buiten een portaal is gewoon de geul
                {
                    var loc = new List<Vector2>(c.local);
                    foreach (var od in Openings(i))
                    {
                        skewRoad = Loc(SkewFor(i, od), c.east, c.north);
                        Vector2 d2 = new Vector2(Vector3.Dot(od, c.east), Vector3.Dot(od, c.north)).normalized, np = PortalNormal(d2);
                        var outside = ClipHalf(loc, -np, -Vector2.Dot(d2 * FlatL, np)); if (outside.Count < 3) continue;
                        var pts = new List<Vector3>(); foreach (var q in outside) { int ck = System.Array.IndexOf(c.local, q); Vector3 w = ck >= 0 ? c.corners[ck] : (c.dir * Planet.R + c.east * q.x + c.north * q.y).normalized; pts.Add(w * (Planet.R + (ck >= 0 ? CornerH(i, ck) : Mathf.Min(g, Carve(w))))); }   // hoekpunten zoals de buren ze hebben
                        for (int k = 1; k + 1 < pts.Count; k++) mb0.Tri(pts[0], pts[k], pts[k + 1], c.dir);
                    }
                    skewRoad = Vector2.zero;
                }
                Walls:
                // sokkel: een plateau boven het maaiveld krijgt stenen wanden tot op het maaiveld ernaast (tussen twee plateaus: een keermuur)
                bool strip = !plat && !cut && StripCut(i);   // een bebouwd vak waar een weg in steekt: ook een sokkel, met als bovenkant de grond van het vak (op de weg zelf lager)
                if ((plat || cut || strip) && !Quay(i))   // een gracht heeft kademuren in plaats van een sokkel (PlanetNature.cs)
                    for (int k = 0; k < m; k++)
                    {
                        int j = edN[i][k]; if (FlatGround(j) && G(j) >= g - 0.01f) continue;   // alleen naast een echt vlak buurvak geen wand (een half wegvak in een talud is dat niet)
                        if (Underpass(i))   // brugvak: de muur langs de rand alleen binnen de portalen (daarbuiten ligt de geul)
                        {
                            Vector2 la = c.local[k], lb = c.local[(k + 1) % m]; float t0 = 0f, t1 = 1f;
                            foreach (var od in Openings(i))
                            {
                                skewRoad = Loc(SkewFor(i, od), c.east, c.north);
                                Vector2 d2 = new Vector2(Vector3.Dot(od, c.east), Vector3.Dot(od, c.north)).normalized, np = PortalNormal(d2); float lim = Vector2.Dot(d2 * FlatL, np);
                                float fa = Vector2.Dot(la, np) - lim, fb = Vector2.Dot(lb, np) - lim;
                                if (fa > 0f && fb > 0f) { t1 = -1f; break; }
                                if (fa > 0f) t0 = Mathf.Max(t0, fa / (fa - fb)); else if (fb > 0f) t1 = Mathf.Min(t1, fa / (fa - fb));
                            }
                            skewRoad = Vector2.zero;
                            if (t1 - t0 < 0.01f) continue;
                            Vector3 ca = c.corners[k], cb = c.corners[(k + 1) % m]; float hA = CornerH(i, k), hB = CornerH(i, (k + 1) % m);
                            Vector3 pa = Vector3.Lerp(ca, cb, t0).normalized, pb = Vector3.Lerp(ca, cb, t1).normalized; float ba = Mathf.Lerp(hA, hB, t0), bb = Mathf.Lerp(hA, hB, t1);
                            if (g - ba < 0.01f && g - bb < 0.01f) continue;
                            TB("sokkel").Quad(pa * RG, pb * RG, pb * (Planet.R + bb), pa * (Planet.R + ba), (ca + cb) * 0.5f - c.dir);
                            continue;
                        }
                        {   // de wand tot op de randpunten die de buur ook tekent (precies aansluitend, geen kier)
                            var prof = EdgeProfile(i, k); Vector3 ca = c.corners[k], cb = c.corners[(k + 1) % m];
                            Vector3 qa = ca * (Planet.R + CornerBase(i, k)), qb = cb * (Planet.R + CornerBase(i, (k + 1) % m));   // de randpunten liggen op de koorde tussen de hoekpunten (EdgeProfile)
                            for (int t = 0; t + 1 < prof.Count; t++)
                            {
                                Vector3 pa = Vector3.Lerp(qa, qb, prof[t].t).normalized, pb = Vector3.Lerp(qa, qb, prof[t + 1].t).normalized;
                                float ta = strip ? GroundField(pa, g) : g, tb = strip ? GroundField(pb, g) : g, ba = Mathf.Min(prof[t].h, ta), bb = Mathf.Min(prof[t + 1].h, tb);   // de bovenkant: het plateau, of waar de weg in het vak steekt de grond daar
                                if (ta - ba < 0.01f && tb - bb < 0.01f) continue;
                                TB("sokkel").Quad(pa * (Planet.R + ta), pb * (Planet.R + tb), pb * (Planet.R + bb), pa * (Planet.R + ba), (ca + cb) * 0.5f - c.dir);
                            }
                        }
                    }
                if (pole) PoleMarker(PM, c, g, i == Planet.North);
                if (start && !pole && Dev.Starts)   // startplek (ontwikkelaarshulp) (op een pool staat de poolpaal): lichtblauw vak met een witte paal van 30 m en een vlaggetje
                {
                    Vector3 top = c.dir * (RG + 30f);
                    for (int k = 0; k < 6; k++) { Vector3 a = Vector3.Lerp(c.corners[k], c.dir, 0.93f) * (RG + 0.3f), b = Vector3.Lerp(c.corners[(k + 1) % 6], c.dir, 0.93f) * (RG + 0.3f); sm.Tri(a, b, top, (a + b) * 0.5f - c.dir * RG + c.dir * 0.05f); }
                    Vector3 fl = c.dir * (RG + 27f) + c.east * 6f, fb = c.dir * (RG + 24f);
                    sm.Tri(top, fl, fb, c.north); sm.Tri(top, fb, fl, -c.north);
                    for (int k = 0; k < 6; k++) { Vector3 a = c.corners[k], b = c.corners[(k + 1) % 6]; Vector3 am = (a + b) * 0.5f; Vector3 ai = Vector3.Lerp(a, c.dir, 0.08f), bi = Vector3.Lerp(b, c.dir, 0.08f); sm.Quad(a * (Planet.R + hk[k]), b * (Planet.R + hk[(k + 1) % 6]), bi * (RG + 0.25f), ai * (RG + 0.25f), am - c.dir); }
                }
                if (pent && Dev.Pentagons)   // baken (ontwikkelaarshulp, samen met de vervorming): een vijfzijdige zuil op een vijfhoekige sokkel (de vorm van het vak zelf), licht steen met een amber punt; de naam erboven (PlanetBuilder.Beacons.cs)
                {
                    Vector3 P(int k, float t, float h) => Vector3.Lerp(c.corners[k % 5], c.dir, t).normalized * (RG + h);
                    void Ring(MeshBuilder mb, float t0, float h0, float t1, float h1)   // de mantel tussen twee vijfhoeken
                    {
                        for (int k = 0; k < 5; k++)
                        {
                            Vector3 a0 = P(k, t0, h0), b0 = P(k + 1, t0, h0), a1 = P(k, t1, h1), b1 = P(k + 1, t1, h1);
                            Vector3 nrm = ((a0 + b0) * 0.5f - c.dir * Vector3.Dot((a0 + b0) * 0.5f, c.dir)).normalized;
                            mb.Quad(a0, b0, b1, a1, nrm);
                        }
                    }
                    void Lid(MeshBuilder mb, float t, float h) { Vector3 mid = c.dir * (RG + h); for (int k = 0; k < 5; k++) mb.Tri(mid, P(k + 1, t, h), P(k, t, h), c.dir); }
                    var stone = PM("baken"); var glow = PM("bakenlicht");
                    float gb = hc - g;   // vanaf het midden van het vak
                    Ring(stone, 0.5f, gb - 0.4f, 0.5f, gb + 0.9f); Lid(stone, 0.5f, gb + 0.9f);              // sokkel
                    Ring(stone, 0.56f, gb + 0.9f, 0.6f, gb + 1.5f); Lid(stone, 0.6f, gb + 1.5f);             // trede
                    Ring(stone, 0.8f, gb + 1.5f, 0.88f, gb + 24f);                                           // de zuil, iets taps
                    Ring(glow, 0.88f, gb + 24f, 0.86f, gb + 25.2f);                                          // een lichtende band
                    for (int k = 0; k < 5; k++) glow.Tri(P(k, 0.86f, gb + 25.2f), P(k + 1, 0.86f, gb + 25.2f), c.dir * (RG + gb + 29.5f), ((P(k, 0.86f, gb + 25.2f) + P(k + 1, 0.86f, gb + 25.2f)) * 0.5f - c.dir * (RG + gb + 25.2f)).normalized + c.dir * 0.3f);   // de punt
                }
                if (pent && Dev.Pentagons)   // opstaand randje rondom, zodat de vijfhoek ook van dichtbij opvalt
                    for (int k = 0; k < 5; k++) { Vector3 a = c.corners[k], b = c.corners[(k + 1) % 5]; Vector3 am = (a + b) * 0.5f; Vector3 ai = Vector3.Lerp(a, c.dir, 0.08f), bi = Vector3.Lerp(b, c.dir, 0.08f); pm.Quad(a * (Planet.R + hk[k]), b * (Planet.R + hk[(k + 1) % 5]), bi * (RG + 0.25f), ai * (RG + 0.25f), am - c.dir); }
            }
            foreach (var kv in terrB)
            {
                var tg = new GameObject("Terrein " + kv.Key + " " + (c0 / ChunkN)); tg.transform.SetParent(groundRoot, false); made.Add(tg); tg.layer = Layers.Bodem;
                var tmesh = kv.Value.Build(); tg.AddComponent<MeshFilter>().sharedMesh = tmesh; tg.AddComponent<MeshRenderer>().sharedMaterial = Mats.Get("p_terr_" + kv.Key, TerrColor(kv.Key));
                tg.AddComponent<MeshCollider>().sharedMesh = tmesh;
            }
            if (soil != null) SoilMesh(soil, soilCell, c0 / ChunkN, made);
            SeaChunk(c0, Mathf.Min(n, c0 + ChunkN), groundRoot, made);   // water op het zeeniveau (PlanetNature.cs)
            if (pm.v.Count > 0)
            {
                var pg = new GameObject("Vijfhoeken " + (c0 / ChunkN)); pg.transform.SetParent(groundRoot, false); made.Add(pg); pg.layer = Layers.Bodem;
                var pmesh = pm.Build(); pg.AddComponent<MeshFilter>().sharedMesh = pmesh; pg.AddComponent<MeshRenderer>().sharedMaterial = Mats.Get("p_vijfhoek", new Color(1f, 0.5f, 0.1f));
                pg.AddComponent<MeshCollider>().sharedMesh = pmesh;
            }
            if (sm.v.Count > 0)
            {
                var sg = new GameObject("Startplekken " + (c0 / ChunkN)); sg.transform.SetParent(groundRoot, false); made.Add(sg); sg.layer = Layers.Bodem;
                var smesh = sm.Build(); sg.AddComponent<MeshFilter>().sharedMesh = smesh; sg.AddComponent<MeshRenderer>().sharedMaterial = Mats.Get("p_start", new Color(0.55f, 0.8f, 1f));
                sg.AddComponent<MeshCollider>().sharedMesh = smesh;
            }
            foreach (var kv in polar)
            {
                var g = new GameObject("Pool " + kv.Key); g.transform.SetParent(groundRoot, false); made.Add(g); g.layer = Layers.Bodem;
                var mesh = kv.Value.Build(); g.AddComponent<MeshFilter>().sharedMesh = mesh; g.AddComponent<MeshRenderer>().sharedMaterial = Mats.Get("p_pool_" + kv.Key, PoleColor(kv.Key));
                if (!kv.Key.StartsWith("vlag")) g.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
            Color grass = new Color(0.64f, 0.78f, 0.45f), hot = new Color(1f, 0.6f, 0.2f);
            for (int k = 0; k < Steps; k++)
            {
                if (ms[k].v.Count == 0) continue;
                var go = new GameObject("Bodem " + (c0 / ChunkN) + " " + k); go.transform.SetParent(groundRoot, false); made.Add(go); go.layer = Layers.Bodem;
                var mesh = ms[k].Build(); go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Get("p_bodem" + k, Color.Lerp(grass, hot, k / (float)(Steps - 1)));
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
        }
    }

    // Poolmarkering: ijswit vak met een rood-wit gestreepte paal van 40 m, een gouden knop en een vlag met N (noord) of Z (zuid) die naar het oosten wappert
    const float PoleH = 40f;
    // kleuren van het terrein (zoals TC0-TC3 in de webversie): gras, dieper groen omhoog, grijsgroen boven de 16 m, aarde op steile flanken
    static Color TerrColor(string k)
    {
        if (NatureColor(k, out var nc)) return nc;
        if (k == "aarde") return new Color(0.545f, 0.482f, 0.345f);
        if (k == "sokkel") return new Color(0.61f, 0.58f, 0.53f);
        if (k == "zand") return new Color(0.86f, 0.79f, 0.55f);
        float h = (int.Parse(k.Substring(1)) + 0.5f) * 3f;
        Color c0 = new Color(0.64f, 0.78f, 0.45f), c1 = new Color(0.373f, 0.58f, 0.278f), c3 = new Color(0.604f, 0.643f, 0.541f);
        return Color.Lerp(Color.Lerp(c0, c1, Mathf.SmoothStep(0f, 1f, h / 5f)), c3, Mathf.SmoothStep(0f, 1f, (h - 16f) / 12f) * 0.6f);
    }

    static void PoleMarker(System.Func<string, MeshBuilder> M, Planet.Cell c, float g, bool north)
    {
        Vector3 o = c.dir * (Planet.R + g), u = c.dir, e = c.east, n = c.north;
        System.Func<float, float, Vector3> ring = (ang, r) => e * (Mathf.Cos(ang) * r) + n * (Mathf.Sin(ang) * r);
        // voet: een lage ijsrand rondom
        for (int k = 0; k < c.corners.Length; k++) { Vector3 a = c.corners[k], b = c.corners[(k + 1) % c.corners.Length], am = (a + b) * 0.5f, ai = Vector3.Lerp(a, c.dir, 0.08f), bi = Vector3.Lerp(b, c.dir, 0.08f); M("ijs").Quad(a * (Planet.R + g), b * (Planet.R + g), bi * ((Planet.R + g) + 0.25f), ai * ((Planet.R + g) + 0.25f), am - c.dir); }
        // paal: zeshoekig, in tien banden rood en wit
        const float r0 = 0.45f; const int Bands = 10;
        for (int s = 0; s < Bands; s++)
        {
            float y0 = s * PoleH / Bands, y1 = (s + 1) * PoleH / Bands; var mb = M(s % 2 == 0 ? "rood" : "wit");
            for (int k = 0; k < 6; k++) { float a0 = k * Mathf.PI / 3f, a1 = (k + 1) * Mathf.PI / 3f; Vector3 p0 = ring(a0, r0), p1 = ring(a1, r0); mb.Quad(o + p0 + u * y0, o + p1 + u * y0, o + p1 + u * y1, o + p0 + u * y1, p0 + p1); }
        }
        // gouden knop: twee zeshoekige punten tegen elkaar
        Vector3 kc = o + u * (PoleH + 0.8f);
        for (int k = 0; k < 6; k++) { float a0 = k * Mathf.PI / 3f, a1 = (k + 1) * Mathf.PI / 3f; Vector3 p0 = kc + ring(a0, 0.9f), p1 = kc + ring(a1, 0.9f); M("goud").Tri(p0, p1, kc + u * 0.9f, (p0 + p1) * 0.5f - kc + u * 0.5f); M("goud").Tri(p0, p1, kc - u * 0.9f, (p0 + p1) * 0.5f - kc - u * 0.5f); }
        // vlag: wit met een grote N (noordpool, blauw) of Z (zuidpool, rood), aan beide kanten leesbaar
        const float fl = 9f, fh = 6f; float top = PoleH - 0.6f, bot = top - fh;
        { Vector3 a = o + u * bot + e * r0, b = o + u * bot + e * (r0 + fl), cc = o + u * top + e * (r0 + fl), d = o + u * top + e * r0; M("vlagwit").Quad(a, b, cc, d, n); M("vlagwit").Quad(a, b, cc, d, -n); }
        // de letter in vlagcoördinaten (x vanaf de paal, y vanaf de onderkant), als vierhoeken
        float x0 = 2.6f, x1 = 6.4f, yB = 0.9f, yT = 5.1f, sw = 0.85f; var q = new List<Vector2[]>();
        if (north)
        {
            q.Add(new[] { new Vector2(x0, yB), new Vector2(x0 + sw, yB), new Vector2(x0 + sw, yT), new Vector2(x0, yT) });
            q.Add(new[] { new Vector2(x1 - sw, yB), new Vector2(x1, yB), new Vector2(x1, yT), new Vector2(x1 - sw, yT) });
            q.Add(new[] { new Vector2(x1 - sw, yB), new Vector2(x1, yB), new Vector2(x0 + sw, yT), new Vector2(x0, yT) });
        }
        else
        {
            q.Add(new[] { new Vector2(x0, yT - sw), new Vector2(x1, yT - sw), new Vector2(x1, yT), new Vector2(x0, yT) });
            q.Add(new[] { new Vector2(x0, yB), new Vector2(x1, yB), new Vector2(x1, yB + sw), new Vector2(x0, yB + sw) });
            q.Add(new[] { new Vector2(x0, yB + sw), new Vector2(x0 + sw * 1.2f, yB + sw), new Vector2(x1, yT - sw), new Vector2(x1 - sw * 1.2f, yT - sw) });
        }
        string lk = north ? "vlagblauw" : "vlagrood";
        for (int side = -1; side <= 1; side += 2)   // van de noordkant (+n) loopt x naar rechts; van de andere kant gespiegeld, zodat de letter goed leesbaar is
            foreach (var p4 in q)
            {
                var w = new Vector3[4];
                for (int k = 0; k < 4; k++) { float xx = side > 0 ? p4[k].x : fl - p4[k].x; w[k] = o + u * (bot + p4[k].y) + e * (r0 + xx) + n * (side * 0.04f); }
                M(lk).Quad(w[0], w[1], w[2], w[3], n * side);
            }
    }
    static Color PoleColor(string k)
    {
        switch (k)
        {
            case "ijs": return new Color(0.92f, 0.96f, 1f); case "rood": return new Color(0.8f, 0.15f, 0.15f); case "wit": return new Color(0.97f, 0.97f, 0.95f);
            case "baken": return new Color(0.88f, 0.86f, 0.8f); case "bakenlicht": return new Color(0.98f, 0.72f, 0.2f);
            case "goud": return new Color(0.9f, 0.72f, 0.25f); case "vlagrood": return new Color(0.68f, 0.11f, 0.16f); case "vlagwit": return Color.white; case "vlagblauw": return new Color(0.13f, 0.27f, 0.55f);
            default: return Color.gray;
        }
    }
}
