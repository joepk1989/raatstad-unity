using System.Collections.Generic;
using UnityEngine;

// Het terrein: hoogte per vak (Terr, SetTerr), hoekpunten, en de hoogtefunctie langs wegen op een niveau (GroundField, LineGround).
public static partial class PlanetCity
{
    // ---------- terrein ----------
    // Eén hoogte per vak (0 … 30 m), als de low-poly stand van de webversie (07b-terrein.js): een vak is een waaier van driehoeken
    // met het midden op de hoogte van het vak en elk hoekpunt op de laagste van de drie vakken die daar samenkomen. Eén verhoogd
    // vak is zo een puntje, een groep een plateau met schuine randen. Een vak met iets erop is een vlak plateau op zijn eigen
    // hoogte met een stenen sokkel tot op het maaiveld; weg- en padvakken volgen de helling naar hun buren.
    public static readonly Dictionary<int, float> Terr = new Dictionary<int, float>();
    public const float TerrTop = 90f, TerrLow = -12f;   // bergen tot 90 m (rots en sneeuw, PlanetNature.cs); afgraven kan tot 12 m onder het gewone maaiveld
    // Nieuwe opbouw: een wegvak met een profielhoogte (RoadH) geeft die als G (alles wat de weghoogte leest blijft werken); het
    // terrein zelf (Terrain) blijft onaangeroerd en is de basis voor de hoekpunten en de hoogtefunctie.
    public static float G(int i) => RoadH.Count > 0 && RoadH.TryGetValue(i, out var rh) ? rh : Terrain(i);
    public static float Terrain(int i) => LakeG(i, Terr.TryGetValue(i, out var h) ? h : 0f);   // een meer ligt vanzelf onder het zeeniveau (PlanetNature.cs)
    public static bool Plateau(int i) => Cells.TryGetValue(i, out var l) && l.Count > 0 && !IsWalk(i) && !OpenGround(l) && !PathHalf(i) && ((RoadShape && RoadH.Count == 0) || !IsHalf(i) || HalfBuilt(i));   // zonder terrein door wegen is een half wegvak met alleen groen geen plaat (die stak over de weg uit); met een gebouw wel (anders zweeft het). Nieuwe opbouw (RoadH): een half wegvak zonder gebouw is gewoon terrein, de wegstrook komt uit de hoogtefunctie
    static bool HalfBuilt(int i) { var l = Cells[i]; for (int k = 1; k < l.Count; k++) if (l[k] != "park" && l[k] != "bos") return true; return false; }   // bomen, meren en grachten liggen in het terrein (PlanetNature.cs)
    public static bool SetTerr(int i, float h)   // geeft true als de hoogte echt veranderde
    {
        h = Mathf.Clamp(Mathf.Round(h * 100f) / 100f, TerrLow, TerrTop); if (Mathf.Abs(h - G(i)) < 0.005f) return false;
        if (Mathf.Abs(h) > 0.004f) Terr[i] = h; else Terr.Remove(i);
        Touch(i); foreach (int p in Partners(i)) dirty.Add(p / ChunkN);   // een schuine weg helt ook naar het vak twee stappen verder
        return true;
    }
    // per hoek van een vak de twee buren die daar ook samenkomen, en per rand (hoek k → k+1) de buur aan de overkant
    static int[][] cnA, cnB, edN;
    static void Topo()
    {
        if (cnA != null && cnA.Length == Planet.Cells.Length) return;
        int N = Planet.Cells.Length; cnA = new int[N][]; cnB = new int[N][]; edN = new int[N][];
        for (int i = 0; i < N; i++)
        {
            var c = Planet.Cells[i]; int m = c.corners.Length; cnA[i] = new int[m]; cnB[i] = new int[m]; edN[i] = new int[m];
            for (int k = 0; k < m; k++)
            {
                int b1 = -1, b2 = -1; float d1 = -2f, d2 = -2f, de = -2f; Vector3 em = c.corners[k] + c.corners[(k + 1) % m];
                foreach (int j in c.nb)
                {
                    float d = Vector3.Dot(Planet.Cells[j].dir, c.corners[k]);
                    if (d > d1) { b2 = b1; d2 = d1; b1 = j; d1 = d; } else if (d > d2) { b2 = j; d2 = d; }
                    float dd = Vector3.Dot(Planet.Cells[j].dir, em); if (dd > de) { de = dd; edN[i][k] = j; }
                }
                cnA[i][k] = b1; cnB[i][k] = b2;
            }
        }
    }
    // Hoogte van een hoekpunt: de laagste van de drie vakken die er samenkomen (voor alle drie hetzelfde, zodat alles naadloos
    // aansluit). Rond een onderdoorgang snijdt de geul erin (Carve), en tellen de uitgegraven padvakken zelf niet mee: zo blijven
    // de vakken naast de geul vlak en lopen alleen de zijkanten van de geul schuin af (zoals het voorbeeld overpass_path).
    // Een weg- of padvak op een ander niveau (Page Up/Down) zet zijn hoekpunten op zijn eigen hoogte; grenzen er meer weg- of
    // padvakken aan het hoekpunt, dan het gemiddelde daarvan (zo volgt de grond de weg die van hart tot hart helt). De buren
    // kantelen dan langs hun eigen driehoeken van dat hoekpunt naar hun hart: een geul of dijk volgens het raster, zoals het
    // voorbeeld overpass_path.
    public static float CornerH(int i, int k)
    {
        float h = CornerBase(i, k); Vector3 cd = Planet.Cells[i].corners[k];
        if (fillN > 0) h = Mathf.Max(h, Fill(cd));
        if (carved.Count > 0) h = Mathf.Min(h, Carve(cd));
        return h;
    }
    static readonly HashSet<int> levelled = new HashSet<int>();   // weg- en padvakken op een ander niveau (bijgewerkt met de geulen)
    // idem zonder de geul: het maaiveld op het hoekpunt
    static float CornerBase(int i, int k) => CornerBase0(i, k) + (CornerOfs.Count > 0 && CornerOfs.TryGetValue(CornerKey(i, k), out var o) ? o : 0f);
    // Zelf verzette hoekpunten (gereedschap Terrein, stand Rand): per gedeeld hoekpunt (de drie vakken eromheen) een verschuiving omhoog
    // of omlaag op het maaiveld; de vakken eromheen hellen mee
    public static readonly Dictionary<long, float> CornerOfs = new Dictionary<long, float>();
    public static long CornerKey(int i, int k)
    {
        Topo(); int a = cnA[i][k], b = cnB[i][k]; int x = Mathf.Min(i, Mathf.Min(a, b)), z = Mathf.Max(i, Mathf.Max(a, b)), y = i + a + b - x - z;
        return ((long)x << 40) | ((long)y << 20) | (uint)z;
    }
    public static float CornerOfs1(int i, int k) => CornerOfs.TryGetValue(CornerKey(i, k), out var o) ? o : 0f;
    // het midden van een vak zelf verzet (een doorkruislijn: twee tegenoverliggende hoekpunten en het midden)
    public static readonly Dictionary<int, float> CenterOfs = new Dictionary<int, float>();
    public static float CenterOfs1(int i) => CenterOfs.Count > 0 && CenterOfs.TryGetValue(i, out var o) ? o : 0f;
    // de doorkruislijn van hoekpunt k door het midden naar hoekpunt k+3 van vak i omhoog of omlaag (alleen een zeshoek)
    public static void NudgeDiagonal(int i, int k, float d)
    {
        int m = Planet.Cells[i].corners.Length; if (m != 6) return;
        foreach (int kk in new[] { k, (k + 3) % 6 })
        {
            long key = CornerKey(i, kk); float v = Mathf.Clamp((CornerOfs.TryGetValue(key, out var o) ? o : 0f) + d, -20f, 20f);
            if (Mathf.Abs(v) < 0.01f) CornerOfs.Remove(key); else CornerOfs[key] = v;
            Topo(); foreach (int c in new[] { cnA[i][kk], cnB[i][kk] }) { Touch(c); foreach (int j in Planet.Cells[c].nb) Touch(j); }
        }
        float vc = Mathf.Clamp(CenterOfs1(i) + d, -20f, 20f); if (Mathf.Abs(vc) < 0.01f) CenterOfs.Remove(i); else CenterOfs[i] = vc;
        Touch(i); foreach (int j in Planet.Cells[i].nb) Touch(j);
    }
    // de rand k (van hoekpunt k naar k+1) van vak i omhoog of omlaag (meter); de vakken eromheen opnieuw tekenen
    public static void NudgeEdge(int i, int k, float d)
    {
        int m = Planet.Cells[i].corners.Length;
        foreach (int kk in new[] { k, (k + 1) % m })
        {
            long key = CornerKey(i, kk); float v = Mathf.Clamp((CornerOfs.TryGetValue(key, out var o) ? o : 0f) + d, -20f, 20f);
            if (Mathf.Abs(v) < 0.01f) CornerOfs.Remove(key); else CornerOfs[key] = v;
            Topo(); foreach (int c in new[] { i, cnA[i][kk], cnB[i][kk] }) { Touch(c); foreach (int j in Planet.Cells[c].nb) Touch(j); }
        }
    }
    // Terrein door wegen (ontwikkelaar, Instellingen › Ontwikkelaar): uit tekent het terrein zonder taluds en dijken langs wegen
    // en zonder wegvlakken op de hoekpunten; alleen onder de weg zelf wordt het uitgesneden tot net onder het wegdek (GroundField)
    public static bool RoadShape = true;
    // Talud langs wegen (ontwikkelaar): uit is de kant naast de weg bijna loodrecht (FK) in plaats van 1 op 1,5
    public static bool RoadSlopes = true;
    static float CornerBase0(int i, int k)
    {
        Topo(); int a = cnA[i][k], b = cnB[i][k];
        if (RoadShape && RoadCorner(i, a, b, out float rh)) return rh;
        bool qi = Quay(i), qa = Quay(a), qb = Quay(b); DamSkip(i, a, b, ref qi, ref qa, ref qb);   // een gracht heeft kademuren: de buren lopen niet naar de bodem af; een weg over een meer is een dam (PlanetNature.cs)
        if (carved.Count == 0 && !qi && !qa && !qb) return Mathf.Min(Terrain(i), Mathf.Min(Terrain(a), Terrain(b)));   // (het terrein: de weg zelf komt uit de hoogtefunctie)
        float h = float.MaxValue; if (!carved.Contains(i) && !qi) h = Terrain(i); if (!carved.Contains(a) && !qa) h = Mathf.Min(h, Terrain(a)); if (!carved.Contains(b) && !qb) h = Mathf.Min(h, Terrain(b));
        if (h == float.MaxValue) h = Mathf.Min(Terrain(i), Mathf.Min(Terrain(a), Terrain(b)));
        return h;
    }
    // Een hoekpunt waar twee verbonden gewone weg- of padvakken samenkomen ligt op de weg (het gemiddelde van die vakken: de weg loopt
    // van hart tot hart), niet op het laagste van de drie vakken: anders zakt de grond op een helling onder de weg weg en ligt de weg
    // in een kom. Wegen op een niveau, bruggen en geulen regelen hun hoekpunten zelf (GroundField, Carve).
    static bool RoadCorner(int i, int a, int b, out float h)
    {
        if (SkewPathCorner(i, a, b, out h) || SkewPathCorner(a, i, b, out h) || SkewPathCorner(b, i, a, out h)) return true;
        h = 0f; bool wi = PlainWalk(i), wa = PlainWalk(a), wb = PlainWalk(b); if ((wi ? 1 : 0) + (wa ? 1 : 0) + (wb ? 1 : 0) < 2) return false;
        bool ia = wi && wa && Partners(i).Contains(a), ib = wi && wb && Partners(i).Contains(b), ab = wa && wb && Partners(a).Contains(b);
        float s = 0f; int n = 0;
        if (ia || ib) { s += G(i); n++; } if (ia || ab) { s += G(a); n++; } if (ib || ab) { s += G(b); n++; }
        if (n < 2) return false; h = s / n;
        // alleen paden: het terrein ernaast blijft liggen. Langs een helling gaf de weghoogte een vlakke berm tot op het hoekpunt (9 m
        // opzij) en daarna een steile wand; nu het gewone vlak door de drie vakken, nooit hoger dan het pad (anders zakt het erin)
        if (!IsRoad(i) && !IsRoad(a) && !IsRoad(b)) h = Mathf.Min(h, (G(i) + G(a) + G(b)) / 3f);
        return true;
    }
    // Een leeg half vak naast een schuin pad (alleen paden eroverheen, niets op de vrije helft) is geen plaat: het terrein loopt er
    // gewoon door. Als plateau werd een helling onder een schuin pad een rij vlakke platen.
    static bool PathHalf(int h)
    {
        if (!Cells.TryGetValue(h, out var l) || l.Count != 1 || l[0] != "wegdeel") return false; bool any = false;
        foreach (int a in Planet.Cells[h].nb) foreach (int b in Partners(a))
            if (Linked(a, b) && !IsNeighbour(a, b) && Between(a, b).Contains(h)) { if (!IsPath(a) || !IsPath(b)) return false; any = true; }
        return any;
    }
    // Het hoekpunt van padvak x tussen de twee halve vakken van een schuin pad (x naar y, twee stappen) ligt op de lijn van het pad,
    // een derde van x naar y: daar op padhoogte, anders zweeft het pad of zakt het weg tussen de vakken
    static bool SkewPathCorner(int x, int p, int q, out float h)
    {
        h = 0f; if (!IsPath(x) || !PlainWalk(x) || !PathHalf(p) || !PathHalf(q)) return false;
        foreach (int y in Partners(x))
            if (!IsNeighbour(x, y) && IsPath(y) && PlainWalk(y)) { var bt = Between(x, y); if (bt.Contains(p) && bt.Contains(q)) { h = G(x) + (G(y) - G(x)) / 3f; return true; } }
        return false;
    }
    static bool PlainWalk(int x) => IsWalk(x) && !Underpass(x) && !levelled.Contains(x) && !carved.Contains(x) && !preCarve.ContainsKey(x) && !Quay(x);
    public static float OwnCorner(int i, int k) => FlatGround(i) ? G(i) : fieldCells.Contains(i) && NearSeg(Planet.Cells[i].corners[k]) ? GroundField(Planet.Cells[i].corners[k], FieldBase(i, k)) : CornerH(i, k);
    // het gewone terrein onder hoekpunt k zonder de wegen: een bebouwd vak (ook waar een weg in steekt) houdt zijn eigen hoogte
    static float FieldBase(int i, int k) => Plateau(i) ? G(i) : CornerBase(i, k);
    // Steekt een weg (asfalt en stoep, in een bocht, op een kruispunt of een spits eiland) in dit bebouwde vak? Dan ligt het binnen die
    // strook op weghoogte en loopt het daarbuiten onder het talud terug naar het plateau (geen sokkel of terrein door de weg).
    static readonly Dictionary<int, (int ver, bool v)> stripCache = new Dictionary<int, (int, bool)>();
    static bool StripCut(int i)
    {
        if (!fieldCells.Contains(i) || !Plateau(i)) return false;
        if (stripCache.TryGetValue(i, out var sc) && sc.ver == fieldVer) return sc.v;
        var c = Planet.Cells[i]; int m = c.corners.Length; bool hit = false;
        for (int k = 0; k < m * 2 && !hit; k++)
        {
            Vector3 dir = k < m ? c.corners[k] : (c.corners[k - m] + c.corners[(k - m + 1) % m] + c.dir * 0.5f).normalized, w = dir * Planet.R;   // de hoeken en punten net binnen de randen
            foreach (var q in segsUse)   // (tijdens het tekenen alleen de stukken in de buurt)
            {
                Vector3 ab = q.b - q.a; float l2 = ab.sqrMagnitude; if (l2 < 1e-4f) continue; float u = Mathf.Clamp01(Vector3.Dot(w - q.a, ab) / l2);
                Vector3 dv = w - (q.a + ab * u); if (dv.sqrMagnitude > 80f * 80f) continue; dv -= dir * Vector3.Dot(dv, dir); if (dv.magnitude < q.hw - 1.7f) { hit = true; break; }   // hw is de stoep plus 1,5 m
            }
            if (!hit) foreach (var q in capsUse) if (CapDist(q, w) < q.hw - 1.7f) { hit = true; break; }
        }
        stripCache[i] = (fieldVer, hit); return hit;
    }   // zoals het vak zelf getekend wordt: een bebouwd vak plat (met een sokkel tot op het talud)
    public static float MinCornerNoCut(int i) { Topo(); float m = G(i); for (int k = 0; k < Planet.Cells[i].corners.Length; k++) { int a = cnA[i][k], b = cnB[i][k]; if (!carved.Contains(a)) m = Mathf.Min(m, G(a)); if (!carved.Contains(b)) m = Mathf.Min(m, G(b)); } return m; }
    // een vlak plateau op de eigen hoogte (bebouwd), behalve een half wegvak dat in een geul ligt
    static bool FlatGround(int i) => Plateau(i) && !InCut(i) && !StripCut(i) && !((IsHalf(i) || SlopePark(i)) && Cells[i].Count == 1 && fieldCells.Contains(i) && NearSeg(Planet.Cells[i].dir));   // een half wegvak of een park langs een weg op een niveau volgt de hoogtefunctie
    // een park (bomen) op een vak waar het talud van een weg op een niveau doorheen loopt: geen plateau, de bomen staan op het talud
    static bool SlopePark(int i) => Cells.TryGetValue(i, out var l) && l.Count == 1 && l[0] == "park" && fieldCells.Contains(i) && NearSeg(Planet.Cells[i].dir);
    static bool InCut(int i) => cuts.Count > 0 && IsHalf(i) && Carve(Planet.Cells[i].dir) < G(i) - 0.5f;

    // Driehoekje k van vak c (midden, hoek k, hoek k+1) langs een weg op een niveau: gesneden langs de knikken van de hoogtefunctie
    // (de rand van de wegbodem, de voet van de schuine kant, het begin en eind van elk stuk weg), dan elk stuk op de hoogte uit
    // GroundField. Binnen een stuk is de functie (vrijwel) een vlak, dus de kanten worden strakke vlakken met rechte randen, zoals
    // het voorbeeld overpass_path; gedeelde randen krijgen in beide vakken dezelfde snijpunten en hoogten.
    // De snijlijnen van de hoogtefunctie in driehoekje k van vak c (midden hc0, hoeken h1 en h2 als vlak): de rand van de wegbodem, de
    // voet van het talud, het begin en eind van elk stuk, de zijden van elke kop, de vlakke stukken; met de stukken (ls) en koppen (lc)
    // die hier meetellen. Voor LineGround en voor Terrein v2 (PlanetGround2.cs: de randpunten en de vakken).
    static List<Lin> FieldLines(Planet.Cell c, int k, float hc0, float h1, float h2, List<Seg> ls, List<Cap> lc)
    {
        int m = c.corners.Length; Vector2 L1 = c.local[k], L2 = c.local[(k + 1) % m]; float det = L1.x * L2.y - L1.y * L2.x; var lines = new List<Lin>(); if (Mathf.Abs(det) < 1e-6f) return lines;
        Vector3 o3 = c.dir * Planet.R;
        Lin B = new Lin { a = hc0, x = ((h1 - hc0) * L2.y - (h2 - hc0) * L1.y) / det, y = ((h2 - hc0) * L1.x - (h1 - hc0) * L2.x) / det };
        foreach (var q in segsUse)
        {
            SegLins(c, q, out Lin T, out Lin S, out float len); if (len < 1e-3f) continue;
            if (Mathf.Abs(T.a) > q.hw + FieldReach + c.radius + 1f || S.a < -c.radius - FieldReach - 1f || S.a > len + c.radius + FieldReach + 1f) continue;
            ls.Add(q); Lin mT = Lin.C(0f) - T;
            if (q.wall)   // sokkel of bak (nieuwe opbouw): de wegrand, de wand op WallOut (met de schuine aanloop aan een grens), het eind; de wand zelf is een eigen mesh
            {
                var fw = new List<Lin> { T - Lin.C(q.hw), T + Lin.C(q.hw), S, S - Lin.C(len), S + Lin.C(EndSlack), S - Lin.C(len + EndSlack), T - Lin.C(q.hw + WallOut), T + Lin.C(q.hw + WallOut) };
                if (q.tap0) { fw.Add(T - S - Lin.C(q.hw)); fw.Add(mT - S - Lin.C(q.hw)); }
                if (q.tap1) { fw.Add(T + S - Lin.C(q.hw + len)); fw.Add(mT + S - Lin.C(q.hw + len)); }
                lines.AddRange(fw); continue;
            }
            float hr = Mathf.Lerp(q.ha, q.hb, Mathf.Clamp01(S.a / len)) - 0.05f, dz = Mathf.Max(0.3f, Mathf.Abs(hr - hc0)) / FK;   // het wegdek (5 cm onder de weg)
            // de voet van de kant loopt schuin als de weg helt (hij ligt waar wegdek + talud het terrein raakt): een rechte lijn in (S, T)
            float sg = hr < hc0 ? 1f : -1f; Lin gap = (Lin.C(q.ha - 0.05f) + S * ((q.hb - q.ha) / len) - B) * (sg / FK);
            Lin toeP = T - Lin.C(q.hw) + gap, toeM = T + Lin.C(q.hw) - gap;
            // ook op EndSlack voorbij elk eind: daar houdt het stuk op (GroundField), zonder kop is dat een klif (landhoofd, portaal); anders
            // liep de mesh van het eind schuin naar de verste hoek van de veelhoek en week hij meters van de hoogtefunctie af
            // en op het bereik van het veld (FieldReach) naast het stuk: verder reikt het talud niet, een hoger verschil is daar een klif
            if (RoadH.Count > 0)   // maaiveldstuk (nieuwe opbouw): de wegrand, de voet, het eind en de kegel voorbij het eind (graten lat - hw = over, en zijn voet); geen stroken van 2 cm meer
            {
                lines.AddRange(new[] { T - Lin.C(q.hw), T + Lin.C(q.hw), toeP, toeM, S, S - Lin.C(len), T - Lin.C(q.hw + FieldReach), T + Lin.C(q.hw + FieldReach) });
                if (q.hardA) lines.Add(S + Lin.C(EndSlack)); else lines.AddRange(new[] { S + Lin.C(FieldReach), T + S - Lin.C(q.hw), mT + S - Lin.C(q.hw) });
                if (q.hardB) lines.Add(S - Lin.C(len + EndSlack)); else lines.AddRange(new[] { S - Lin.C(len + FieldReach), T - S + Lin.C(len - q.hw), mT - S + Lin.C(len - q.hw) });
                continue;
            }
            var fs = new List<Lin> { T - Lin.C(q.hw), T + Lin.C(q.hw), toeP, toeM, S, S - Lin.C(len), S + Lin.C(dz), S - Lin.C(len + dz), S + Lin.C(EndSlack + 0.02f), S - Lin.C(len + EndSlack + 0.02f), T - Lin.C(q.hw + FieldReach + 0.02f), T + Lin.C(q.hw + FieldReach + 0.02f) };   // (2 cm voorbij de grens: op de grens zelf telt het stuk nog mee, de wand zit ertussen)
            lines.AddRange(fs);
        }
        foreach (var q in capsUse)
        {
            if ((o3 - q.c).magnitude > q.hw * 1.2f + FieldReach + c.radius + 1f) continue;
            lc.Add(q);
            float sg = q.h - 0.05f < hc0 ? 1f : -1f;
            foreach (var n in q.n)
            {
                Lin N = new Lin { a = Vector3.Dot(o3 - q.c, n), x = Vector3.Dot(c.east, n), y = Vector3.Dot(c.north, n) };
                Vector3 pn = Vector3.Cross(q.dir, n).normalized; Lin P = new Lin { a = Vector3.Dot(o3 - q.c, pn), x = Vector3.Dot(c.east, pn), y = Vector3.Dot(c.north, pn) };
                if (q.wall) { lines.AddRange(new[] { N - Lin.C(q.hw), N, P, N - Lin.C(q.hw + q.wo) }); continue; }   // sokkel- of bakkop: de wegrand, de graten, de wand (eigen mesh)
                Lin toe = N - Lin.C(q.hw) - (B - Lin.C(q.h - 0.05f)) * (sg / FK);   // de voet: waar wegdek + talud het terrein raakt
                var fc = RoadH.Count > 0 ? new List<Lin> { N - Lin.C(q.hw), toe, N, P, N - Lin.C(q.hw + FieldReach) } : new List<Lin> { N - Lin.C(q.hw), toe, N, P, N - Lin.C(q.hw + FieldReach + 0.02f) };   // (ook het bereik van het veld: daarbuiten een klif)
                lines.AddRange(fc);   // de zijde, de voet, de graat naar de hoek, de vouw in een bocht
            }
        }
        foreach (var q in flatsUse)
        {
            Vector3 ab = q.b - q.a; float len = ab.magnitude; if (len < 1e-3f) continue;
            Vector3 along = Tan(ab, c.dir), across = Vector3.Cross(c.dir, along).normalized;
            Lin T = new Lin { a = Vector3.Dot(o3 - q.a, across), x = Vector3.Dot(c.east, across), y = Vector3.Dot(c.north, across) };
            Lin S = new Lin { a = Vector3.Dot(o3 - q.a, along), x = Vector3.Dot(c.east, along), y = Vector3.Dot(c.north, along) };
            if (Mathf.Abs(T.a) > q.hw + c.radius + 1f || S.a < -c.radius - 1f || S.a > len + c.radius + 1f) continue;
            lines.AddRange(new[] { T - Lin.C(q.hw), T + Lin.C(q.hw), S, S - Lin.C(len) });
        }
        return lines;
    }

    static void LineGround(Planet.Cell c, int k, float hc0, float h1, float h2, System.Action<Vector3, Vector3, Vector3, float, float, float, bool> emit)
    {
        int m = c.corners.Length; Vector2 L1 = c.local[k], L2 = c.local[(k + 1) % m]; Vector3 D0 = c.dir, D1 = c.corners[k], D2 = c.corners[(k + 1) % m];
        float det = L1.x * L2.y - L1.y * L2.x; if (Mathf.Abs(det) < 1e-6f) return;
        var polys = new List<List<Vector2>> { new List<Vector2> { Vector2.zero, L1, L2 } };
        // het gewone terrein als lineaire functie van de vlakke coördinaten (zoals Base hieronder): de voet van een kant ligt waar wegdek plus talud dat raakt
        var ls = new List<Seg>(); var lc = new List<Cap>();   // de stukken en koppen die hier iets doen (de rest overslaan bij elk punt)
        foreach (var f in FieldLines(c, k, hc0, h1, h2, ls, lc))
        {
            var next = new List<List<Vector2>>(); foreach (var pl in polys) if (!SplitBy(pl, f, next)) next.Add(pl); polys = next;
        }
        System.Func<Vector2, Vector3> Dir = q2 => { float b1 = (q2.x * L2.y - q2.y * L2.x) / det, b2 = (L1.x * q2.y - L1.y * q2.x) / det; return (D0 * (1f - b1 - b2) + D1 * b1 + D2 * b2).normalized; };
        System.Func<Vector2, float> Base = q2 => { float b1 = (q2.x * L2.y - q2.y * L2.x) / det, b2 = (L1.x * q2.y - L1.y * q2.x) / det; return hc0 * (1f - b1 - b2) + h1 * b1 + h2 * b2; };
        foreach (var pl in polys)
        {
            int n = pl.Count; var P = new Vector3[n]; var H = new float[n]; Vector2 ctr = Vector2.zero;
            for (int v = 0; v < n; v++) { Vector3 d = Dir(pl[v]); H[v] = GroundField(d, Base(pl[v]), out _, ls, lc); P[v] = d * (Planet.R + H[v]); ctr += pl[v]; }
            for (int v = 1; v + 1 < n; v++) Refine(pl[0], pl[v], pl[v + 1], P[0], P[v], P[v + 1], H[0], H[v], H[v + 1], 0);
        }
        // Waar twee taluds of koppen elkaar raken knikt de functie midden in een stuk (die lijn wordt niet gesneden): ligt het midden van
        // een driehoekje meer dan 6 cm naast het vlak door zijn hoeken, dan in drie stukken via dat midden (geen nieuwe punten op de randen,
        // dus geen kieren met de buren), tot drie keer. Zo geen vouwen en geen losse steile (bruine) driehoekjes meer.
        void Refine(Vector2 a, Vector2 b, Vector2 cc, Vector3 Pa, Vector3 Pb, Vector3 Pc, float ha, float hb, float hc2, int depth)
        {
            Vector2 m2 = (a + b + cc) / 3f; Vector3 dm = Dir(m2); float hm = GroundField(dm, Base(m2), out bool steep, ls, lc);
            if (depth < 3 && Mathf.Abs(hm - (ha + hb + hc2) / 3f) > 0.06f && (b - a).sqrMagnitude + (cc - a).sqrMagnitude > 0.5f)
            {
                Vector3 Pm = dm * (Planet.R + hm);
                Refine(a, b, m2, Pa, Pb, Pm, ha, hb, hm, depth + 1); Refine(b, cc, m2, Pb, Pc, Pm, hb, hc2, hm, depth + 1); Refine(cc, a, m2, Pc, Pa, Pm, hc2, ha, hm, depth + 1);
                return;
            }
            emit(Pa, Pb, Pc, ha, hb, hc2, steep);
        }
    }

    // de vakken waar de hoogtefunctie iets doet: een voorselectie, zodat de rest van de planeet (raster, bodem) niet langs alle
    // stukken weg hoeft; vanaf de vakken op een niveau naar buiten zolang een vak binnen bereik ligt
    static readonly HashSet<int> fieldCells = new HashSet<int>();
    // een brugvak boven een weg of pad op een niveau: vlak, met de koker erdoor (BuildGround)
    static bool FieldBridge(int i) => Underpass(i) && fieldCells.Contains(i) && UnderIsLevelled(i, Under[i] == "weg");
    static void FillFieldCells()
    {
        fieldCells.Clear(); if (!HasField) return;
        // per stuk weg (en kop) vanaf zijn eigen vakken naar buiten zolang een vak binnen bereik ligt; zo kijkt elk vak alleen naar de
        // stukken in de buurt (vroeger elk vak naar alle stukken van de planeet: op een volle planeet een tiende seconde per klik)
        var seen = new HashSet<int>(); var q = new Queue<int>();
        void Grow(int start, System.Func<Vector3, float, bool> near)
        {
            seen.Clear(); q.Clear(); q.Enqueue(start); seen.Add(start);
            while (q.Count > 0)
            {
                int i = q.Dequeue(); var c = Planet.Cells[i]; if (!near(c.dir * Planet.R, FieldReach + 1f + c.radius * 1.25f + 1f)) continue;   // (1,25: de verste hoek ligt verder dan de gemiddelde straal; anders rekent een vak net buiten het veld zijn randen zonder het veld)
                fieldCells.Add(i); foreach (int j in c.nb) if (seen.Add(j)) q.Enqueue(j);
            }
        }
        foreach (var sg in segs)
        {
            var g = sg;
            System.Func<Vector3, float, bool> near = (w, fr) => { Vector3 ab = g.b - g.a; float l2 = ab.sqrMagnitude, t = l2 < 1e-4f ? 0f : Mathf.Clamp01(Vector3.Dot(w - g.a, ab) / l2); return (w - (g.a + ab * t)).sqrMagnitude < (g.hw + fr) * (g.hw + fr); };
            Grow(g.ia, near); if (g.ib >= 0) Grow(g.ib, near);
        }
        foreach (var cp in caps) { var g = cp; Grow(g.ic, (w, fr) => (w - g.c).sqrMagnitude < (g.hw * 1.2f + fr) * (g.hw * 1.2f + fr)); }
    }
    // halve breedte van de weg met stoep (plus anderhalve meter), of van het pad: zo breed ligt de grond op weghoogte
    // hoeveel het keerrondje van een doodlopende weg buiten de gewone breedte (WalkHW) komt, plus een halve meter
    static float HeadExtra(int i) { Dims(StyleOf(i), out _, out float sw); float head = sw + (StyleOf(i) == 0 ? 2.1f : 0.3f) + 0.8f; return Mathf.Max(0f, head - WalkHW(i)); }
    static float WalkHW(int i) { if (!IsRoad(i)) return PathHW; Dims(StyleOf(i), out _, out float sw); return sw + 1.5f; }
    public static float WalkHalfWidth(string type) { if (type != "weg") return PathHW; Dims(DragStyle, out _, out float sw); return sw + 1.5f; }   // voor het voorbeeld
    // ---------- de hoogtefunctie langs wegen op een niveau ----------
    // ia, ib: de vakken aan de einden. wall (nieuwe opbouw, ontwerp 8.3): een sokkel- of bakstuk (weg meer dan 6 m boven of onder het
    // terrein): de grond op weghoogte tot WallOut buiten de wegrand en daarbuiten het terrein, met een wand als eigen mesh ertussen;
    // tap0/tap1: aan dat eind grenst het aan een maaiveldstuk en loopt de wand schuin aan vanaf de wegrand (zo begint hij op hoogte 0)
    // hardA/hardB: aan dat eind stopt het stuk hard (een tunnelportaal: de berg blijft erboven staan), zonder kegel voorbij het eind
    struct Seg { public Vector3 a, b; public float ha, hb, hw; public bool capA, capB, wall, tap0, tap1, hardA, hardB; public int ia, ib; }
    static readonly List<Seg> flats = new List<Seg>();   // gewone wegen bij een weg op een niveau: daaronder de grond op weghoogte
    static float FlatHW(int i) { Dims(StyleOf(i), out _, out float sw); return sw + 0.3f; }   // de weg met stoep, net iets breder   // van hart a naar hart b (wereldpunten), met de weghoogte aan beide einden; cap: daar zit een kop, het stuk stopt op het hart
    static readonly List<Seg> segs = new List<Seg>();
    // de kop van een doodlopende weg: een zeshoek (vijfhoek) rond het hart van het vak, zijden recht tegenover de buren, met de
    // grond op weghoogte tot op hw van het hart en daarbuiten onder 45° terug, net als langs de weg
    struct Cap { public Vector3 c, dir; public Vector3[] n; public float h, hw, wo; public int ic; public bool wall; }   // wall: sokkel- of bakkop (zie Seg); wo: de wand zo ver buiten de wegrand (WallOut, of minder als dat op een vakrand valt)
    static readonly List<Cap> caps = new List<Cap>();
    static bool HasField => segs.Count > 0 || caps.Count > 0;
    const float SteepUp = 0.766f;   // cos 40°: een talud steiler dan 40° is kale aarde, flauwer is gras
    static void AddCap(int i, float h, float hw)
    {
        var cell = Planet.Cells[i]; var n = new Vector3[cell.nb.Length];
        for (int k = 0; k < n.Length; k++) n[k] = Tan(Planet.Cells[cell.nb[k]].dir - cell.dir, cell.dir);
        caps.Add(new Cap { c = cell.dir * Planet.R, dir = cell.dir, n = n, h = h, hw = hw, ic = i });
    }
    // zeshoekige afstand tot het hart, in het raakvlak van de kop. Ver weg (de bol kromt onder het raakvlak door, aan de overkant van
    // de planeet komt er bijna 0 uit) telt de gewone afstand: anders tilt een kop met alle koppen in gebruik (UseAll) de grond aan de
    // overkant op
    static float CapDist(Cap q, Vector3 w)
    {
        Vector3 v = w - q.c; float far = v.magnitude; if (far > 80f) return far;
        float d = float.MinValue; foreach (var n in q.n) d = Mathf.Max(d, Vector3.Dot(v, n)); return d;
    }
    // de helling van de kant langs een weg op een niveau (hoogte per meter opzij; 1 = 45°), in te stellen met het schuifje Talud
    public static float FieldK = 1f / 1.5f;   // standaard 1 op 1,5 (34°), zoals het model overpass_path
    const float WallOut = 2.4f;   // de keermuur staat zo ver buiten de wegrand (de stoep en wat berm liggen nog op weghoogte)
    // Een punt precies op de wandlijn hoort bij de kant die erom vraagt: de veelhoek aan de binnenkant (de strook) rekent met +1 mm,
    // die aan de buitenkant (het terrein) met -1 mm; zo krijgen de wandquad en de grond aan beide kanten bit-gelijke punten (ontwerp 8.5)
    static float wallBias;
    // Het terrein zonder wegen op punt dir: het vlak over de waaierdriehoek (zoals de mesh), voor de indeling van de stukken (SplitWalls)
    static float BaseAt(Vector3 dir)
    {
        if (!CellAt(dir, out int i, out int k, out float b0, out float b1, out float b2)) return Terrain(i);
        if (Plateau(i)) return G(i); int m = Planet.Cells[i].corners.Length;
        return b0 * Center2(i) + b1 * CornerBase(i, k) + b2 * CornerBase(i, (k + 1) % m);
    }
    // Nieuwe opbouw (ontwerp 8.3): elk stuk in delen naar het hoogteverschil d tussen weg en terrein: |d| <= 6 m maaiveld (talud),
    // daarboven een sokkel (weg boven het terrein) of bak (eronder), de grens waar |d| door 6 gaat; delen korter dan 2 m gaan bij hun
    // buur. Een kop is sokkel of bak naar d in zijn hart.
    static void SplitWalls()
    {
        if (RoadH.Count == 0) return; const float Lim = 6f, MinPart = 2f; var outL = new List<Seg>();
        foreach (var q in segs)
        {
            Vector3 ab = q.b - q.a; float len = ab.magnitude; if (len < 0.5f) { outL.Add(q); continue; }
            int n = Mathf.Max(2, Mathf.CeilToInt(len) + 1); var d = new float[n];
            for (int v = 0; v < n; v++) { float t = v / (n - 1f); d[v] = Mathf.Lerp(q.ha, q.hb, t) - BaseAt((q.a + ab * t).normalized); }
            var parts = new List<(float t0, float t1, bool wall)>(); float tPrev = 0f; bool wPrev = Mathf.Abs(d[0]) > Lim;
            for (int v = 1; v < n; v++)
            {
                bool w = Mathf.Abs(d[v]) > Lim; if (w == wPrev) continue;
                float a0 = Mathf.Abs(d[v - 1]) - Lim, a1 = Mathf.Abs(d[v]) - Lim, tc = ((v - 1) + a0 / (a0 - a1)) / (n - 1f);
                parts.Add((tPrev, tc, wPrev)); tPrev = tc; wPrev = w;
            }
            parts.Add((tPrev, 1f, wPrev));
            for (bool again = true; again && parts.Count > 1;)   // te korte delen bij de buur
            {
                again = false;
                for (int v = 0; v < parts.Count; v++)
                {
                    if ((parts[v].t1 - parts[v].t0) * len >= MinPart) continue;
                    if (v + 1 < parts.Count) { parts[v + 1] = (parts[v].t0, parts[v + 1].t1, parts[v + 1].wall); parts.RemoveAt(v); }
                    else { parts[v - 1] = (parts[v - 1].t0, parts[v].t1, parts[v - 1].wall); parts.RemoveAt(v); }
                    again = true; break;
                }
            }
            if (parts.Count == 1) { var q1 = q; q1.wall = parts[0].wall; outL.Add(q1); continue; }
            for (int v = 0; v < parts.Count; v++)
            {
                var pt = parts[v]; var q2 = q;
                q2.a = (q.a + ab * pt.t0).normalized * Planet.R; q2.b = (q.a + ab * pt.t1).normalized * Planet.R; q2.ha = Mathf.Lerp(q.ha, q.hb, pt.t0); q2.hb = Mathf.Lerp(q.ha, q.hb, pt.t1);
                q2.wall = pt.wall; q2.tap0 = pt.wall && v > 0; q2.tap1 = pt.wall && v + 1 < parts.Count; q2.capA = v == 0 && q.capA; q2.capB = v + 1 == parts.Count && q.capB;
                outL.Add(q2);
            }
        }
        segs.Clear(); segs.AddRange(outL);
        // kop en armen horen bij elkaar: een kop is ook een wand als een arm daar als wand aankomt, en bij een wandkop wordt het eind van elke
        // maaiveldarm (minstens 3 m) wand, met de schuine aanloop naar het maaiveld; anders staat de zeshoekige wand van de kop in het talud van de arm
        bool AtA(Seg q, int i) => Vector3.Distance(q.a, Planet.Cells[i].dir * Planet.R) < 0.5f;
        bool AtB(Seg q, int i) => Vector3.Distance(q.b, Planet.Cells[i].dir * Planet.R) < 0.5f;
        var wallCaps = new HashSet<int>();
        for (int v = 0; v < caps.Count; v++)
        {
            bool w = Mathf.Abs(caps[v].h - BaseAt(caps[v].dir)) > Lim;
            if (!w) foreach (var q in segs) if (q.wall && ((q.ia == caps[v].ic && AtA(q, q.ia)) || (q.ib == caps[v].ic && AtB(q, q.ib)))) { w = true; break; }
            if (w) wallCaps.Add(caps[v].ic);
        }
        if (wallCaps.Count > 0)
        {
            const float Arm = 3f; outL.Clear();
            foreach (var q0 in segs)
            {
                var q = q0; if (q.wall) { outL.Add(q); continue; }
                Vector3 ab = q.b - q.a; float len = ab.magnitude; bool wa = wallCaps.Contains(q.ia) && AtA(q, q.ia), wb = wallCaps.Contains(q.ib) && AtB(q, q.ib);
                if (!wa && !wb) { outL.Add(q); continue; }
                if (len < Arm * 2f + MinPart || (wa && wb)) { q.wall = true; outL.Add(q); continue; }   // (te kort voor een eigen maaiveldstuk: helemaal wand)
                float t0 = wa ? Arm / len : 0f, t1 = wb ? 1f - Arm / len : 1f;
                if (wa) { var w1 = q; w1.b = (q.a + ab * t0).normalized * Planet.R; w1.hb = Mathf.Lerp(q.ha, q.hb, t0); w1.wall = true; w1.tap1 = true; w1.capB = false; outL.Add(w1); }
                { var m1 = q; m1.a = (q.a + ab * t0).normalized * Planet.R; m1.b = (q.a + ab * t1).normalized * Planet.R; m1.ha = Mathf.Lerp(q.ha, q.hb, t0); m1.hb = Mathf.Lerp(q.ha, q.hb, t1); m1.capA = m1.capA && !wa; m1.capB = m1.capB && !wb; outL.Add(m1); }
                if (wb) { var w2 = q; w2.a = (q.a + ab * t1).normalized * Planet.R; w2.ha = Mathf.Lerp(q.ha, q.hb, t1); w2.wall = true; w2.tap0 = true; w2.capA = false; outL.Add(w2); }
            }
            segs.Clear(); segs.AddRange(outL);
        }
        for (int v = 0; v < caps.Count; v++)
        {
            var q = caps[v]; q.wall = wallCaps.Contains(q.ic); q.wo = WallOut;
            // de wand van een kop is een zeshoek evenwijdig aan het vak: valt hij binnen 8 cm op een vakrand, dan zou de grond ernaast zijn wandpunten
            // voor randpunten aanzien (Ground2Cell snapt op 1 mm, randpunten op 1,5 cm); dan iets naar binnen
            var cell = Planet.Cells[q.ic];
            for (int it = 0; it < 3; it++) { bool near = false; foreach (int j in cell.nb) if (Mathf.Abs(q.hw + q.wo - 0.5f * Vector3.Distance(Planet.Cells[j].dir, cell.dir) * Planet.R) < 0.08f) near = true; if (!near) break; q.wo -= 0.25f; }
            caps[v] = q;
        }
    }
    // De lijnen van een stuk in de vlakke coördinaten van vak c: T de afstand opzij (positief aan één kant), S de afstand langs het stuk vanaf a
    static void SegLins(Planet.Cell c, Seg q, out Lin T, out Lin S, out float len)
    {
        Vector3 ab = q.b - q.a; len = ab.magnitude; Vector3 o3 = c.dir * Planet.R;
        Vector3 along = Tan(ab, c.dir), across = Vector3.Cross(c.dir, along).normalized;
        T = new Lin { a = Vector3.Dot(o3 - q.a, across), x = Vector3.Dot(c.east, across), y = Vector3.Dot(c.north, across) };
        S = new Lin { a = Vector3.Dot(o3 - q.a, along), x = Vector3.Dot(c.east, along), y = Vector3.Dot(c.north, along) };
    }
    // De wandlijnen (sokkels en bakken) bij vak c, zo gericht dat de binnenkant (de strook op weghoogte) negatief is: de rechte wanden op
    // WallOut buiten de wegrand, de schuine aanloop aan een grens met een maaiveldstuk (lat - hw = s), de zijden van een kop
    // grp (als gegeven): per lijn het nummer van zijn strook of kop; de strook is de snijding van alle halfvlakken (negatief) van die groep,
    // want een lijn alleen zegt buiten de zeshoek niets (haar verlengde loopt door)
    static List<Lin> WallLins(Planet.Cell c, List<Seg> ls, List<Cap> lc, List<int> grp = null)
    {
        var res = new List<Lin>(); Vector3 o3 = c.dir * Planet.R; int g = 0;
        void Add(Lin f) { res.Add(f); grp?.Add(g); }
        foreach (var q in ls)
        {
            if (!q.wall) continue; SegLins(c, q, out Lin T, out Lin S, out float len); Lin mT = Lin.C(0f) - T; g++;
            Add(T - Lin.C(q.hw + WallOut)); Add(mT - Lin.C(q.hw + WallOut)); Add(Lin.C(-EndSlack) - S); Add(S - Lin.C(len + EndSlack));   // (de einden horen bij de strook, ze zijn geen wand: daar verschilt de hoogte niet)
            if (q.tap0) { Add(T - S - Lin.C(q.hw)); Add(mT - S - Lin.C(q.hw)); }
            if (q.tap1) { Add(T + S - Lin.C(q.hw + len)); Add(mT + S - Lin.C(q.hw + len)); }
        }
        foreach (var q in lc) { if (!q.wall) continue; g++; foreach (var n in q.n) Add(new Lin { a = Vector3.Dot(o3 - q.c, n) - q.hw - q.wo, x = Vector3.Dot(c.east, n), y = Vector3.Dot(c.north, n) }); }
        return res;
    }
    static float FieldReach => Mathf.Min(14f / FK, 40f);   // tot zover reikt een talud (14 m hoogteverschil). Nieuwe opbouw (ontwerp 8.3): een stuk is maaiveld tot 6 m hoogteverschil in het hart (SplitWalls), daarboven een wand (sokkel of bak), een brug of een tunnel; het talud van een maaiveldstuk mag verder reiken, zodat het ook dwars op een helling en in de kegel voorbij het eind het terrein haalt
    static float FK => RoadSlopes ? FieldK : 8f;   // de steilte van de kant; Talud uit: bijna loodrecht (83°), geen sprong (een sprong kan het raster niet tekenen: gaten)   // tot zover naast de weg kan de grond nog omhoog of omlaag lopen (45°: twee niveaus plus de marge)
    public static void SetFieldK(float k)
    {
        if (Mathf.Abs(k - FieldK) < 1e-4f) return; FieldK = k;
        var seen = new HashSet<int>(); var ring = new List<int>(levelled);   // de vakken binnen drie stappen van een weg op een niveau opnieuw
        for (int r = 0; r < 3 && ring.Count > 0; r++) { var next = new List<int>(); foreach (int i in ring) if (seen.Add(i)) { Touch(i); foreach (int j in Planet.Cells[i].nb) next.Add(j); } ring = next; }
        Rebuild(); RebuildGrid();
    }
    static bool NearSeg(Vector3 dir, float extra = 0f)
    {
        Vector3 w = dir * Planet.R; float fr = FieldReach + 1f + extra * 1.25f;   // (extra: de straal van het vak; 1,25 voor de verste hoek)
        foreach (var q in segsUse) { Vector3 ab = q.b - q.a; float l2 = ab.sqrMagnitude, t = l2 < 1e-4f ? 0f : Mathf.Clamp01(Vector3.Dot(w - q.a, ab) / l2); if ((w - (q.a + ab * t)).sqrMagnitude < (q.hw + fr) * (q.hw + fr)) return true; }
        foreach (var q in capsUse) if ((w - q.c).sqrMagnitude < (q.hw * 1.2f + fr) * (q.hw * 1.2f + fr)) return true;
        return false;
    }
    // de grond op punt dir (eenheidsvector), met de gewone terreinhoogte base daar: verhoogde wegen eerst (de hoogste wint), dan
    // verdiepte (de laagste wint), dan de geulen van de onderdoorgangen
    public static float GroundField(Vector3 dir, float base_) => GroundField(dir, base_, out _);
    public static float GroundField(Vector3 dir, float base_, out bool slope) => GroundField(dir, base_, out slope, segsUse, capsUse);
    // Tijdens het tekenen van een vak alleen de stukken weg in de buurt (anders loopt elk meetpunt alle wegen van de planeet langs:
    // op een volle planeet minuten werk). Buiten BuildGround de hele lijst.
    static List<Seg> segsUse = segs, flatsUse = flats; static List<Cap> capsUse = caps;
    static readonly List<Seg> segsNear = new List<Seg>(), flatsNear = new List<Seg>(); static readonly List<Cap> capsNear = new List<Cap>();
    static int fieldVer;   // telt op bij elke nieuwe verzameling stukken (CollectCuts): dan klopt de onthouden buurt niet meer
    static readonly Dictionary<int, (int ver, List<Seg> s, List<Seg> f, List<Cap> c)> nearCache = new Dictionary<int, (int, List<Seg>, List<Seg>, List<Cap>)>();
    static void UseNear(int i)
    {
        if (nearCache.TryGetValue(i, out var nc) && nc.ver == fieldVer) { segsUse = nc.s; flatsUse = nc.f; capsUse = nc.c; return; }
        UseNear0(i); nearCache[i] = (fieldVer, new List<Seg>(segsNear), new List<Seg>(flatsNear), new List<Cap>(capsNear));
        segsUse = nearCache[i].s; flatsUse = nearCache[i].f; capsUse = nearCache[i].c;
    }
    static void UseNear0(int i)
    {
        var c = Planet.Cells[i]; Vector3 w = c.dir * Planet.R; float r = c.radius * 2f + 2f;
        float SegD(Seg q) { Vector3 ab = q.b - q.a; float l2 = ab.sqrMagnitude, t = l2 < 1e-4f ? 0f : Mathf.Clamp01(Vector3.Dot(w - q.a, ab) / l2); return (w - (q.a + ab * t)).magnitude; }
        segsNear.Clear(); foreach (var q in segs) if (SegD(q) < q.hw + FieldReach + r) segsNear.Add(q);
        flatsNear.Clear(); foreach (var q in flats) if (SegD(q) < q.hw + 20f / FieldK + r) flatsNear.Add(q);
        capsNear.Clear(); foreach (var q in caps) if ((w - q.c).magnitude < q.hw * 1.2f + FieldReach + r) capsNear.Add(q);
        segsUse = segsNear; flatsUse = flatsNear; capsUse = capsNear;
    }
    // Hoe ver een punt voorbij het eind van een stuk nog bij dat stuk hoort. Op de bol liggen twee stukken van een rechte weg nooit
    // precies op één lijn: aan de buitenkant van een hart viel een punt op de rand van de weg net buiten beide stukken (een paar cm),
    // daar gaf het veld het gewone terrein en stak een bruine driehoek boven een verdiepte weg uit.
    const float EndSlack = 0.25f;
    static void UseAll() { segsUse = segs; flatsUse = flats; capsUse = caps; }
    static float GroundField(Vector3 dir, float base_, out bool slope, List<Seg> segs, List<Cap> caps)   // slope: het punt ligt op de schuine kant van een weg
    {
        float h = base_; Vector3 w = dir * Planet.R; slope = false; float onLevelH = float.MaxValue, lowRoad = float.MaxValue;   // op de weg zelf van een weg op een niveau: de laagste daarvan; en de laagste weg die hier de grond omlaag haalt
        for (int pass = 0; pass < 2; pass++)
        {
            foreach (var q in segs)
            {
                Vector3 ab = q.b - q.a; float l2 = ab.sqrMagnitude; if (l2 < 1e-4f) continue; float len = Mathf.Sqrt(l2), u = Vector3.Dot(w - q.a, ab) / l2;
                Vector3 dv = w - (q.a + ab * u); if (dv.sqrMagnitude > 80f * 80f) continue;   // (ver weg: na het wegnemen van de radiale richting lijkt de overkant van de planeet vlakbij)
                dv -= dir * Vector3.Dot(dv, dir); float lat = dv.magnitude, over = Mathf.Max(0f, Mathf.Max(-u, u - 1f) * len);   // naast het stuk, en voorbij een eind
                float hr = Mathf.Lerp(q.ha, q.hb, Mathf.Clamp01(u)), rise;
                if (q.wall)   // sokkel of bak (nieuwe opbouw): de strook op weghoogte tot WallOut buiten de wegrand (aan een grens met een maaiveldstuk smaller: lat - hw <= s, zo begint de wand op 0), daarbuiten niets; de wand is een eigen mesh (Ground2Cell)
                {
                    float wo = WallOut; if (q.tap0) wo = Mathf.Min(wo, u * len); if (q.tap1) wo = Mathf.Min(wo, len - u * len);
                    if (lat > q.hw + wo + wallBias || over > EndSlack) continue; rise = 0f;
                }
                else if (RoadH.Count > 0)   // maaiveldstuk (nieuwe opbouw): het talud opzij en als kegel voorbij het eind, zodat de grond zonder sprong overgaat in het volgende stuk, een sokkel of een bak; aan een hard eind (portaal) geen kegel
                {
                    bool hard = u < 0f ? q.hardA : q.hardB;
                    if (lat > q.hw + FieldReach || over > (hard ? EndSlack : FieldReach)) continue; rise = Mathf.Max(Mathf.Max(0f, lat - q.hw), hard ? 0f : over) * FK;
                }
                else
                {
                    if (lat > q.hw + FieldReach || over > EndSlack) continue;   // (met wat speling: op het hart zelf valt een punt door afronding anders net buiten beide stukken) alleen naast het stuk: voorbij het eind doet de kop het, of het volgende stuk (een rechte kop gaf daar een trapje)
                    rise = Mathf.Max(0f, lat - q.hw) * FK;   // de kant
                }
                if (lat <= q.hw && over <= EndSlack) onLevelH = Mathf.Min(onLevelH, hr);
                if (!RoadShape && pass == 0) continue;   // terrein door wegen uit: niets opvullen (geen dijk), wel afgraven met een talud
                if (pass == 0 && hr > base_ + 0.01f) { float v = hr - rise - 0.05f; if (v > h) { h = v; slope = rise > 0f; } }        // talud (5 cm onder het wegdek: geen streepje gras op de weg)
                else if (pass == 1 && hr - 0.05f < base_) { float v = hr + rise - 0.05f; lowRoad = Mathf.Min(lowRoad, hr); if (v < h) { h = v; slope = rise > 0f; } }   // uitgraving: pas als het wegdek (5 cm onder de weg) onder het terrein ligt, dan loopt het veld zonder sprong door
            }
            foreach (var q in caps)
            {
                float cd = CapDist(q, w) - q.hw, rise = Mathf.Max(0f, cd) * FK;
                if (q.wall) { if (cd > q.wo + wallBias) continue; rise = 0f; }   // sokkel- of bakkop (nieuwe opbouw): de strook tot wo (WallOut), daarbuiten het terrein, de wand is een eigen mesh
                else if (rise > FieldReach * FK) continue;
                if (cd <= 0f) onLevelH = Mathf.Min(onLevelH, q.h);
                if (!RoadShape && pass == 0) continue;
                if (pass == 0 && q.h > base_ + 0.01f) { float v = q.h - rise - 0.05f; if (v > h) { h = v; slope = rise > 0f; } }   // een kop ligt net als de weg 5 cm onder het wegdek
                else if (pass == 1 && q.h - 0.05f < base_) { float v = q.h + rise - 0.05f; if (v < h) { h = v; slope = rise > 0f; } }
            }
        }
        // gewone wegen erbij: onder de weg de hoogte van die weg, ernaast hooguit het talud hoger of lager (zacht, zonder sprong),
        // zodat het talud van een weg op een niveau nooit over een gewone weg heen loopt
        if (RoadShape) foreach (var q in flatsUse)   // waar twee wegen over elkaar liggen wint de laagste (een helling naar een kruispunt, een weg onder een brug)
        {
            Vector3 ab = q.b - q.a; float l2 = ab.sqrMagnitude; if (l2 < 1e-4f) continue; float len = Mathf.Sqrt(l2), u = Vector3.Dot(w - q.a, ab) / l2;
            Vector3 dv = w - (q.a + ab * Mathf.Clamp01(u)); if (dv.sqrMagnitude > 80f * 80f) continue; dv -= dir * Vector3.Dot(dv, dir); float over = Mathf.Max(0f, Mathf.Max(-u, u - 1f) * len);
            float rise = Mathf.Max(Mathf.Max(0f, dv.magnitude - q.hw), over) * FK; if (rise > 20f) continue;
            float hr = Mathf.Lerp(q.ha, q.hb, Mathf.Clamp01(u)) - 0.03f; if (onLevelH <= hr + 0.3f) continue;
            // gaat hier een weg een heel niveau lager onder deze weg door (een brug), dan mag de bak eronder: alleen geen grond erboven
            float nh = lowRoad <= hr - LevelH + 0.1f ? Mathf.Min(h, hr + rise) : Mathf.Clamp(h, hr - rise, hr + rise);
            if (Mathf.Abs(nh - h) > 1e-4f) { h = nh; slope = rise > 0f; }
        }
        // op een weg zelf (binnen zijn strook) nooit hoger dan die weg: anders legt het talud of de kop van een hogere weg ernaast een
        // plaat gras over het asfalt (waar twee stroken elkaar raken wint de laagste weg, net als onder een brug)
        if (onLevelH < float.MaxValue && h > onLevelH - 0.05f) h = onLevelH - 0.05f;
        if (spans.Count > 0 && SpanBottom(w, out float sb)) h = Mathf.Min(h, sb);   // onder het dek van een brug over een dal
        if (carved.Count > 0 && cuts.Count > 0) h = Mathf.Min(h, Carve(dir));
        return h;
    }
    // ligt wat onder brugvak i door gaat op een eigen niveau (dan maakt het zijn eigen geul, zonder portaalhelling)?
    static bool UnderIsLevelled(int i, bool road)
    {
        var ends = road ? TunnelEnds(i) : new List<int>(); if (!road) foreach (int j in Partners(i)) if (IsPath(j)) ends.Add(j);
        if (ends.Count == 0) return false; foreach (int j in ends) if (WalkLevel(j) == 0) return false; return true;
    }
}
