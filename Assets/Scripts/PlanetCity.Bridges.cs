using System.Collections.Generic;
using UnityEngine;

// Bruggen en onderdoorgangen: koker (TubeSeg), betonblok (BridgeBlock), portalen, schuine kruisingen (Crossing) en UnderMesh.
public static partial class PlanetCity
{
    // ---------- onderdoorgang: de onderdelen (naar het voorbeeld overpass_path) ----------
    static float tubeW = 4.5f;   // halve breedte van vloer en plafond van de koker: 4,5 m voor een pad (zoals overpass_path), 6 m voor een weg
    // zeshoekig profiel (overpass_path): vloer en plafond tubeW breed, op halve hoogte onder 30° breder (bij 3 m hoogte ±5,37)
    static Vector2[] TubeProf(float fl, float top)
    {
        float c = (top - fl) * 0.5f * 0.577f, ym = (fl + top) * 0.5f, w = tubeW, wm = tubeW + c;
        return new[] { new Vector2(-w, fl), new Vector2(w, fl), new Vector2(wm, ym), new Vector2(w, top), new Vector2(-w, top), new Vector2(-wm, ym) };
    }
    // een rechte koker van p0 naar p1 (in het vlak van 'flat': x langs e, z langs n), vloer van f0 naar f1, plafond op top
    // scheve brug: de richting van de weg erover (in het vlak van de tekening); de uiteinden van koker en portaal lopen daarmee
    // evenwijdig, tot 45° scheef (Vector2.zero: recht)
    static Vector2 skewRoad = Vector2.zero;
    static float SkewK(Vector2 axis, Vector2 tg)
    {
        if (skewRoad == Vector2.zero) return 0f; float rt = Vector2.Dot(skewRoad, tg); if (Mathf.Abs(rt) < 0.2f) return 0f;
        return Mathf.Clamp(Vector2.Dot(skewRoad, axis) / rt, -1f, 1f);
    }
    static void TubeSeg(System.Func<string, MeshBuilder> M, Frame flat, Vector2 p0, Vector2 p1, float f0, float f1, float top, bool skew0 = true, bool skew1 = true)
    {
        Vector2 dd = (p1 - p0).normalized, tg = new Vector2(-dd.y, dd.x), pm = (p0 + p1) * 0.5f; float k = SkewK(dd, tg);
        var a = TubeProf(f0, top); var b = TubeProf(f1, top);
        System.Func<Vector2, Vector2, Vector3> At = (pos, q) => { float kk = (pos == p0 ? skew0 : skew1) ? k : 0f; Vector2 x = pos + tg * q.x + dd * (q.x * kk); return flat.P(x.x, q.y, x.y); };
        Vector3 axis = flat.P(pm.x, ((f0 + f1) * 0.5f + top) * 0.5f, pm.y);
        for (int v = 0; v < 6; v++)
        {
            int w = (v + 1) % 6; Vector3 q0 = At(p0, a[v]), q1 = At(p0, a[w]), q2 = At(p1, b[w]), q3 = At(p1, b[v]);
            M(v == 0 ? "steen" : "tunnel").Quad(q0, q1, q2, q3, axis - (q0 + q1 + q2 + q3) * 0.25f);   // naar binnen gekeerd
        }
    }
    // de hoogte van het maaiveld zoals het getekend wordt (BuildGround) op punt dir: het waaierdriehoekje van het vak daar, met de
    // hoogtefunctie langs wegen op een niveau
    public static float TerrainAt(Vector3 dir)
    {
        int i0 = Planet.Nearest(dir * Planet.R); if (TerrainIn(i0, dir, out float h0)) return h0;
        foreach (int j in Planet.Cells[i0].nb) if (TerrainIn(j, dir, out float h1)) return h1;   // het dichtstbijzijnde midden is niet altijd het vak waar het punt in ligt (vakken zijn niet overal even groot)
        return G(i0);
    }
    static bool TerrainIn(int i, Vector3 dir, out float h)
    {
        h = 0f; var c = Planet.Cells[i]; int m = c.corners.Length;
        Vector3 v = (dir - c.dir) * Planet.R; Vector2 q = new Vector2(Vector3.Dot(v, c.east), Vector3.Dot(v, c.north));
        float hc = 0f; for (int k = 0; k < m; k++) hc += CornerBase(i, k); hc = (levelled.Contains(i) ? hc / m : G(i)) + CenterOfs1(i);
        for (int k = 0; k < m; k++)
        {
            Vector2 L1 = c.local[k], L2 = c.local[(k + 1) % m]; float det = L1.x * L2.y - L1.y * L2.x; if (Mathf.Abs(det) < 1e-6f) continue;
            float b1 = (q.x * L2.y - q.y * L2.x) / det, b2 = (L1.x * q.y - L1.y * q.x) / det; if (b1 < -0.02f || b2 < -0.02f || b1 + b2 > 1.02f) continue;
            if (fieldCells.Contains(i) && !FlatGround(i) && NearSeg(c.dir, c.radius)) { h = GroundField(dir, hc * (1f - b1 - b2) + CornerBase(i, k) * b1 + CornerBase(i, (k + 1) % m) * b2); return true; }
            if (FlatGround(i)) { h = G(i); return true; }
            h = hc * (1f - b1 - b2) + OwnCorner(i, k) * b1 + OwnCorner(i, (k + 1) % m) * b2; return true;
        }
        return false;
    }
    // Het betonblok van een brug over een weg of pad op een niveau (model overpass_path): één blok van portaal tot portaal, los van
    // het terrein (dat is een hoogteveld en kan niet tegelijk grond boven en een koker eronder hebben; de bak loopt gewoon onder de
    // brug door en verdwijnt in het blok). Doorsnede: de grote zeshoek van het portaal (onder ±(tubeW+3,75) op 2,17 m onder de vloer,
    // op halve hoogte 30° breder), bovenop vlak op dekhoogte (y = 0) met gras, minstens zo breed als de bak bovenaan (trench), zodat
    // er naast de weg geen gat in het dek valt. De koker zelf (TubeSeg) en de portalen (Portal) tekenen de binnenkant en de randen.
    static void BridgeBlock(System.Func<string, MeshBuilder> M, Frame flat, Vector2 p0, Vector2 p1, float fl, float top, float trench)
    {
        Vector2 dd = (p1 - p0).normalized, tg = new Vector2(-dd.y, dd.x); float k = SkewK(dd, tg);
        float yB = fl - 2.17f, yO = top + 2.17f, co = (yO - yB) * 0.5f * 0.577f, ym = (yB + yO) * 0.5f, wb = tubeW + 3.75f, wm = wb + co;
        float wt = Mathf.Max(wm - co * (0f - ym) / (yO - ym), trench), yt = -0.04f;   // tot de rand van de bak (daar sluit het maaiveld aan), net onder het wegdek
        var o = new[] { new Vector2(wb, yB), new Vector2(wm, ym), new Vector2(wt, yt), new Vector2(-wt, yt), new Vector2(-wm, ym), new Vector2(-wb, yB) };
        var t = TubeProf(fl, top);   // (-w,fl) (w,fl) (wm,ym) (w,top) (-w,top) (-wm,ym)
        // op de bol (zoals de wegen): het platte vlak van de tekening raakt de bol alleen in het midden; 13 m opzij ligt het maaiveld al
        // zo'n 15 cm lager, en dan staat de rand van het blok erboven uit
        float r0 = (flat.o).magnitude;
        System.Func<Vector2, Vector2, Vector3> At = (pos, q) => { Vector2 x = pos + tg * q.x + dd * (q.x * k); return flat.P(x.x, q.y, x.y).normalized * (r0 + q.y); };
        Vector2 pm = (p0 + p1) * 0.5f; Vector3 axis = flat.P(pm.x, ym, pm.y);
        // de bovenkant gesnapt op het terrein: op een raster neemt elk punt de hoogte van het maaiveld daar (TerrainAt), nooit lager dan
        // het dek; zo sluit de rand precies aan, ook als het terrein glooit. De zijkanten eronder volgen dezelfde bovenrand.
        const int nA = 8, nX = 12;
        System.Func<float, float, Vector3> Top = (fa, fx) =>
        {
            Vector2 pos = Vector2.Lerp(p0, p1, fa); float xx = Mathf.Lerp(-wt, wt, fx); Vector2 x = pos + tg * xx + dd * (xx * k);
            Vector3 d = flat.P(x.x, 0f, x.y).normalized; return d * Mathf.Max(r0 + yt, Planet.R + TerrainAt(d) - 0.05f);
        };
        for (int a = 0; a < nA; a++)
            for (int b = 0; b < nX; b++)
                M("bodem0").Quad(Top(a / (float)nA, b / (float)nX), Top((a + 1) / (float)nA, b / (float)nX), Top((a + 1) / (float)nA, (b + 1) / (float)nX), Top(a / (float)nA, (b + 1) / (float)nX), flat.u);   // dezelfde kleur als het terrein
        for (int v = 0; v < 5; v++)   // de buitenkant: twee schuine zijden per kant (de bovenste tot de gesnapte rand)
        {
            if (v == 2) continue;
            for (int a = 0; a < nA; a++)
            {
                float fa0 = a / (float)nA, fa1 = (a + 1) / (float)nA; Vector2 pa = Vector2.Lerp(p0, p1, fa0), pb = Vector2.Lerp(p0, p1, fa1);
                Vector3 q0 = v == 3 ? Top(fa0, 0f) : At(pa, o[v]), q1 = v == 3 ? Top(fa1, 0f) : At(pb, o[v]);
                Vector3 q3 = v == 1 ? Top(fa0, 1f) : At(pa, o[v + 1]), q2 = v == 1 ? Top(fa1, 1f) : At(pb, o[v + 1]);
                M("steen").Quad(q0, q1, q2, q3, (q0 + q1 + q2 + q3) * 0.25f - axis);
            }
        }
        int[] map = { 1, 2, 3, 4, 5, 0 };   // welk hoekpunt van de koker bij welk hoekpunt van de buitenkant hoort
        foreach (var (pe, sg) in new[] { (p0, -1f), (p1, 1f) })   // de kopse kanten, van de buitenkant tot de koker (achter het portaal)
            for (int v = 0; v < 6; v++)
            {
                int w = (v + 1) % 6; Vector3 a0 = At(pe, o[v]), a1 = At(pe, o[w]), b1 = At(pe, t[map[w]]), b0 = At(pe, t[map[v]]);
                M("steen").Quad(a0, a1, b1, b0, flat.D(dd.x * sg, 0f, dd.y * sg));
            }
    }
    // portaal: een zeshoekige betonnen lijst met een scherpe voorrand 1,44 m voor de opening (m, naar buiten nr); van die rand loopt
    // het beton schuin terug naar de grote zeshoek achter en naar de opening, onder de vloer 2,17 m diep en boven tot net boven het dek
    static void Portal(System.Func<string, MeshBuilder> M, Frame flat, Vector2 m, Vector2 nr, float yE, float top)
    {
        Vector2 tg = new Vector2(-nr.y, nr.x);
        float yB = yE - 2.17f, yO = top + 2.17f, co = (yO - yB) * 0.5f * 0.577f, ym = (yB + yO) * 0.5f;   // bovenkant 2,17 m boven het plafond: een borstwering van 1,09 m boven het dek (zoals overpass_path)
        System.Func<float, Vector2[]> Hx = hw => new[] { new Vector2(-hw, yB), new Vector2(hw, yB), new Vector2(hw + co, ym), new Vector2(hw, yO), new Vector2(-hw, yO), new Vector2(-hw - co, ym) };
        var inn = TubeProf(yE, top); var ridge = Hx(tubeW + 1.25f); var outr = Hx(tubeW + 3.75f);   // voorrand en grote zeshoek: onder en boven 1,25 en 3,75 m buiten de koker (overpass_path: ±5,75 en ±8,25)
        float k = SkewK(nr, tg);
        System.Func<Vector2, float, Vector3> Pf = (q, d) => { Vector2 x = m + nr * (d + k * q.x) + tg * q.x; return flat.P(x.x, q.y, x.y); };
        Vector3 ctr = Pf(new Vector2(0f, ym), 0.72f);
        for (int v = 0; v < 6; v++)
        {
            int w = (v + 1) % 6;
            M("steen").Quad(Pf(inn[v], 0f), Pf(inn[w], 0f), Pf(outr[w], 0f), Pf(outr[v], 0f), flat.D(-nr.x, 0, -nr.y));   // achterkant
            Vector3 o0 = Pf(outr[v], 0f), o1 = Pf(outr[w], 0f), o2 = Pf(ridge[w], 1.44f), o3 = Pf(ridge[v], 1.44f);
            M("steen").Quad(o0, o1, o2, o3, (o0 + o1 + o2 + o3) * 0.25f - ctr);   // schuin naar buiten
            Vector3 i0 = Pf(inn[v], 0f), i1 = Pf(inn[w], 0f), i2 = Pf(ridge[w], 1.44f), i3 = Pf(ridge[v], 1.44f);
            M("steen").Quad(i0, i1, i2, i3, ctr - (i0 + i1 + i2 + i3) * 0.25f);   // schuin naar de opening
        }
    }

    // ---------- een schuine weg over een pad: de brug ligt op de rand tussen twee padvakken ----------
    // Een schuine weg (30°) springt van wegvak u naar wegvak v over de rand tussen b1 en b2. Zijn dat twee verbonden padvakken,
    // dan blijft de weg recht en gaat het pad eronderdoor: een koker van het midden van b1 naar het midden van b2 (15,6 m),
    // met de portalen in die vakmiddens, een dichte doos eromheen en zand bovenop.
    public struct Crossing { public int u, v, b1, b2; public bool swap, road; }   // swap: de schuine weg u–v gaat onder b1–b2 door; road: de onderste is een weg
    public static List<Crossing> EdgeCrossings()
    {
        var o = new List<Crossing>();
        foreach (long k in Links)
        {
            int a = (int)(k >> 20), b = (int)(k & 0xFFFFF); if (IsNeighbour(a, b) || TunnelLinks.Contains(k)) continue;
            var bt = Between(a, b); if (bt.Count != 2 || !Linked(bt[0], bt[1])) continue;
            if (IsPath(a) && IsPath(b)) { if (IsRoad(bt[0]) && IsRoad(bt[1])) o.Add(new Crossing { u = a, v = b, b1 = bt[0], b2 = bt[1], swap = true }); continue; }   // een schuin pad onder een rechte weg door
            if (!IsRoad(a) || !IsRoad(b)) continue;
            if (IsPath(bt[0]) && IsPath(bt[1])) o.Add(new Crossing { u = a, v = b, b1 = bt[0], b2 = bt[1] });
            else if (IsRoad(bt[0]) && IsRoad(bt[1]))
            {
                if (LowerLinks.Contains(Key(a, b))) o.Add(new Crossing { u = a, v = b, b1 = bt[0], b2 = bt[1], swap = true, road = true });
                else if (LowerLinks.Contains(Key(bt[0], bt[1]))) o.Add(new Crossing { u = a, v = b, b1 = bt[0], b2 = bt[1], road = true });
            }
        }
        return o;
    }
    public static void DigCrossings() => CarvePaths();
    // het vlak van een kruising: midden op de rand, e langs het pad (van b1 naar b2), n langs de weg; hoogte = de weg
    static Frame CrossFrame(Crossing x, out float lo, out float L)
    {
        Vector3 du = Planet.Cells[x.u].dir, dv = Planet.Cells[x.v].dir, d1 = Planet.Cells[x.b1].dir, d2 = Planet.Cells[x.b2].dir, up = (du + dv).normalized;
        Vector3 road = dv - du; road -= up * Vector3.Dot(road, up); road.Normalize();
        Vector3 path = d2 - d1; path -= up * Vector3.Dot(path, up) + road * Vector3.Dot(path, road); path.Normalize();
        float deck, low;
        if (x.swap) { deck = (G(x.b1) + G(x.b2)) * 0.5f; low = (G(x.u) + G(x.v)) * 0.5f; L = FlatL; var t = road; road = path; path = t; }   // koker langs de schuine weg
        else { deck = (G(x.u) + G(x.v)) * 0.5f; low = (G(x.b1) + G(x.b2)) * 0.5f; L = Vector3.Distance(d1, d2) * Planet.R * 0.5f; }
        lo = low - deck;
        var f = new Frame(new Frame(x.u), up * (Planet.R + deck), Planet.Cells[x.u].local, Planet.Cells[x.u].radius); f.u = up; f.e = path; f.n = road; return f;
    }
    public static Vector3 CrossPoint(Crossing x) => (Planet.Cells[x.u].dir + Planet.Cells[x.v].dir).normalized * Planet.R;
    static void CrossMesh(Dictionary<string, MeshBuilder> mb, Crossing x)
    {
        System.Func<string, MeshBuilder> M = k => { if (!mb.TryGetValue(k, out var y)) { y = new MeshBuilder(); mb[k] = y; } return y; };
        var f = CrossFrame(x, out float lo, out float L); float top = -DeckT; tubeW = x.road ? RoadHW : PathHW;
        TubeSeg(M, f, new Vector2(-L, 0f), new Vector2(L, 0f), lo, lo, top);
        Portal(M, f, new Vector2(-L, 0f), new Vector2(-1f, 0f), lo, top); Portal(M, f, new Vector2(L, 0f), new Vector2(1f, 0f), lo, top);
        int low = x.swap ? x.u : x.b1; bool lev = x.swap ? WalkLevel(x.u) != 0 && WalkLevel(x.v) != 0 : WalkLevel(x.b1) != 0 && WalkLevel(x.b2) != 0;
        if (lev) BridgeBlock(M, f, new Vector2(-L, 0f), new Vector2(L, 0f), lo, top, (x.road ? WalkHW(low) : PathHW) - lo / FieldK);   // de onderste op een niveau: het betonblok (overpass_path)
        tubeW = PathHW;
        if (x.swap || lev) return;   // de koker ligt onder de wegvakken b1 en b2: daar is gewoon grond
        // het dek: de helft van elk padvak aan de kant van de weg (tot de portalen), op de bol en op de hoogte van de weg, zodat
        // het naadloos aansluit op het maaiveld van de vakken ernaast
        float deckR = Planet.R + (G(x.u) + G(x.v)) * 0.5f; Vector3 X = f.o;
        foreach (var (b, sg) in new[] { (x.b1, -1f), (x.b2, 1f) })
        {
            var cs = Planet.Cells[b].corners; var poly = new List<Vector3>(); int nc = cs.Length;
            System.Func<Vector3, float> D = c => Vector3.Dot(c * Planet.R - X, f.e) * sg;   // afstand langs het pad, positief van de weg af
            for (int k = 0; k < nc; k++)
            {
                Vector3 c0 = cs[k], c1 = cs[(k + 1) % nc]; float d0 = D(c0) - L, d1 = D(c1) - L;
                if (d0 <= 0f) poly.Add(c0);
                if ((d0 <= 0f) != (d1 <= 0f)) poly.Add(Vector3.Lerp(c0, c1, d0 / (d0 - d1)).normalized);
            }
            for (int k = 1; k + 1 < poly.Count; k++) M("gras").Tri(poly[0] * deckR, poly[k] * deckR, poly[k + 1] * deckR, f.u);
        }
    }

    // de normaal van een (scheef) portaalvlak met de opening in richting d: loodrecht op de weg (skewRoad), naar buiten
    static Vector2 PortalNormal(Vector2 d)
    {
        Vector2 tg = new Vector2(-d.y, d.x); float k = SkewK(d, tg); if (k == 0f) return d;
        Vector2 along = (tg + d * k).normalized, n = new Vector2(along.y, -along.x); return Vector2.Dot(n, d) < 0f ? -n : n;
    }
    // een veelhoek afsnijden: alleen het deel met dot(p, d) <= lim
    static List<Vector2> ClipHalf(List<Vector2> poly, Vector2 d, float lim)
    {
        var o = new List<Vector2>(); int n = poly.Count;
        for (int k = 0; k < n; k++)
        {
            Vector2 a = poly[k], b = poly[(k + 1) % n]; float fa = Vector2.Dot(a, d) - lim, fb = Vector2.Dot(b, d) - lim;
            if (fa <= 0f) o.Add(a);
            if ((fa <= 0f) != (fb <= 0f)) o.Add(Vector2.Lerp(a, b, fa / (fa - fb)));
        }
        return o;
    }
    // De onderkant van een brug: het water, het meertje of de uitgraving waar de weg overheen gaat
    static void UnderMesh(Dictionary<string, MeshBuilder> mb, Frame fr, int i, int hash)
    {
        System.Func<string, MeshBuilder> M = k => { if (!mb.TryGetValue(k, out var x)) { x = new MeshBuilder(); mb[k] = x; } return x; };
        if (!Under.TryGetValue(i, out var u)) return;
        if (u == "meer") { }   // een weg over een meer is een dam: wegen blijven droog (PlanetNature.cs)
        else if (u == "water") Canal(M, i);   // brug over een gracht
        else if (u == "pad" || u == "weg")   // onderdoorgang, naar het voorbeeld overpass_path: een dek van 1,08 m met een zeshoekige koker eronder en betonnen portalen
        {
            bool roadUnder = u == "weg"; var tends = roadUnder ? TunnelEnds(i) : null; tubeW = roadUnder ? RoadHW : PathHW;
            var uf = new Frame(i, false, true); var flat = new Frame(fr, fr.o, fr.poly, fr.rad); Topo();
            float top = -DeckT, lo = (Low.TryGetValue(i, out var lw) ? lw : G(i) - UnderClear) - G(i);   // onderkant van het dek en de vloer, ten opzichte van het dek
            var mids = new List<Vector2>(); var floors = new List<float>(); var skews = new List<Vector2>(); var nbs = new List<int>();
            foreach (var od in Openings(i)) { mids.Add(Loc(od, fr.e, fr.n) * FlatL); floors.Add(lo); skews.Add(Loc(SkewFor(i, od), fr.e, fr.n)); nbs.Add(OpeningNb(i, od)); }   // portaal 7,8 m vanaf het midden; vlakke vloer
            // bovenop: gras rond de weg, alleen binnen de portalen (daarbuiten ligt de geul); bij een scheef portaal ook het stukje buurvak ervoor
            bool deckGras = !FieldBridge(i);   // een vlak brugvak heeft de grond al op die hoogte
            var deck = new List<Vector2>(fr.poly); float deckR = Planet.R + G(i) - 0.03f; Vector3 cw = Planet.Cells[i].dir * Planet.R;
            for (int q = 0; q < mids.Count; q++)
            {
                skewRoad = skews[q]; Vector2 np = PortalNormal(mids[q].normalized); float lim = Vector2.Dot(mids[q], np); deck = ClipHalf(deck, np, lim);
                if (skewRoad == Vector2.zero || nbs[q] < 0 || !deckGras) continue;
                var nc = Planet.Cells[nbs[q]]; var np2 = new List<Vector2>(); foreach (var cn in nc.corners) { Vector3 d3 = cn * Planet.R - cw; np2.Add(new Vector2(Vector3.Dot(d3, fr.e), Vector3.Dot(d3, fr.n))); }
                var inner = ClipHalf(np2, np, lim);
                System.Func<Vector2, Vector3> W = p2 => (cw + fr.e * p2.x + fr.n * p2.y).normalized * deckR;
                for (int k = 1; k + 1 < inner.Count; k++) M("gras").Tri(W(inner[0]), W(inner[k]), W(inner[k + 1]), fr.u);
            }
            skewRoad = Vector2.zero;
            if (deckGras) for (int k = 1; k + 1 < deck.Count; k++) M("gras").Tri(fr.P(deck[0].x, -0.03f, deck[0].y), fr.P(deck[k].x, -0.03f, deck[k].y), fr.P(deck[k + 1].x, -0.03f, deck[k + 1].y), fr.u);
            if (!roadUnder) Disc(M("pad"), uf, 1.3f, 0.1f);
            var open = mids;
            System.Func<string, MeshBuilder> MM = M;
            if (open.Count == 2 && Vector2.Dot(mids[0].normalized, mids[1].normalized) < -0.95f)
            {
                skewRoad = skews[0]; TubeSeg(MM, flat, mids[0], mids[1], floors[0], floors[1], top);
                if (FieldBridge(i)) { int le = tends != null && tends.Count > 0 ? tends[0] : i; BridgeBlock(MM, flat, mids[0], mids[1], lo, top, (roadUnder ? WalkHW(le) : PathHW) - lo / FieldK); }   // het betonblok (overpass_path)
            }   // rechtdoor: één koker van rand tot rand
            else for (int q = 0; q < open.Count; q++)
            {
                skewRoad = skews[q]; TubeSeg(MM, flat, Vector2.zero, mids[q], lo, floors[q], top, false, true);
                if (open.Count == 1) { var pc = TubeProf(lo, top); Vector2 tg = new Vector2(-mids[q].normalized.y, mids[q].normalized.x); for (int v = 1; v < 5; v++) M("tunnel").Tri(flat.P(tg.x * pc[0].x, pc[0].y, tg.y * pc[0].x), flat.P(tg.x * pc[v].x, pc[v].y, tg.y * pc[v].x), flat.P(tg.x * pc[v + 1].x, pc[v + 1].y, tg.y * pc[v + 1].x), fr.D(mids[q].x, 0, mids[q].y)); }   // doodlopend: dicht in het midden
            }
            for (int q = 0; q < open.Count; q++) { skewRoad = skews[q]; Portal(MM, flat, mids[q], mids[q].normalized, floors[q], top); }
            tubeW = PathHW; skewRoad = Vector2.zero;
            foreach (int j in Partners(i))
            {
                if (!UnderLink(i, j)) continue;
                Vector3 d3 = Planet.Cells[j].dir * Planet.R - uf.o;
                Vector2 q = new Vector2(Vector3.Dot(d3, uf.e), Vector3.Dot(d3, uf.n)) * 0.5f; float L = q.magnitude; Vector2 d = q / L, r = new Vector2(d.y, -d.x);
                Strip(M("pad"), uf, d, r, 0f, L, 1.1f, 0.105f); StripSides(M("pad"), uf, d, r, 0f, L, 1.1f, -0.1f, 0.105f);
            }
        }
        else
        {
            for (int e2 = 0; e2 < fr.poly.Length; e2++)
            {
                Vector2 a2 = C(fr, e2, 1.0f), b2 = C(fr, e2 + 1, 1.0f), a3 = C(fr, e2, 0.55f), b3 = C(fr, e2 + 1, 0.55f), m2 = (a2 + b2) * 0.5f;
                M("gras").Quad(fr.P(a2.x, 0f, a2.y), fr.P(b2.x, 0f, b2.y), fr.P(b3.x, -3f, b3.y), fr.P(a3.x, -3f, a3.y), fr.D(-m2.x, 2f, -m2.y));
            }
            Top(M("gras"), fr, 0.55f, -3f);
        }
    }
}
