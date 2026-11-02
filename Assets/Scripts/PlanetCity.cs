using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// De stad op de planeet: per vak een stapel blokken; een vak waarvan het onderste blok "weg" is, is een wegvak.
// Wegvakken sluiten vanzelf op elkaar aan (asfalt van hart naar hart, stoep eromheen). Alles wordt in het
// assenstelsel van het vak gebouwd, zodat gebouwen overal recht op de bol staan. De bodem is één mesh per
// deel (128 vakken); blokken en wegen worden per deel opnieuw gemaakt als er iets verandert.
[Serializable] public class PCellSave { public int i; public string[] items; }
[Serializable] public class PSaveData { public int m; public float r; public List<PCellSave> cells = new List<PCellSave>(); public List<int> links = new List<int>(); public List<int> style = new List<int>(), under = new List<int>(), oneway = new List<int>(), terr = new List<int>(), low = new List<int>(), tunnel = new List<int>(), lower = new List<int>(), precarve = new List<int>(), prelevel = new List<int>(), corner = new List<int>(), center = new List<int>(), bike = new List<int>(), rot = new List<int>(); public int startVer, startCell = -1; }   // startVer: de versie van de startstad als de stad nog de ongewijzigde startstad is (anders 0)   // corner: per hoekpunt drie vakken en de verschuiving in cm   // paren: vak/stijl, vak/onder (1 meer, 2 water, 3 kuil, 4 pad, 5 weg), van/naar, vak/terreinhoogte in cm

public static partial class PlanetCity
{
    public static Dictionary<int, List<string>> Cells = new Dictionary<int, List<string>>();
    public const int ChunkN = 128;   // klein genoeg dat een klik maar een klein stuk opnieuw tekent
    static Transform root, groundRoot;
    static readonly Dictionary<int, Transform> chunks = new Dictionary<int, Transform>();
    static readonly HashSet<int> dirty = new HashSet<int>();

    public static void ResetAll() { Cells = new Dictionary<int, List<string>>(); Terr.Clear(); CornerOfs.Clear(); CenterOfs.Clear(); cnA = cnB = edN = null; Links.Clear(); partners.Clear(); Style.Clear(); Bike.Clear(); Rot.Clear(); DragBike = false; Under.Clear(); Low.Clear(); OneWay.Clear(); TunnelLinks.Clear(); LowerLinks.Clear(); DragUnder = false; DragLevel = 0; DragStyle = 0; DragOneWay = false; Lights = true; StartVer = 0; StartCell = -1; startFresh = false; chunks.Clear(); groundChunks.Clear(); dirty.Clear(); root = null; groundRoot = null; grid = null; equator = null; GridOn = true; Slot = 1; roadPieces.Clear(); roadRoot = null; }
    public static bool IsRoad(int i) => Cells.TryGetValue(i, out var l) && l.Count > 0 && l[0] == "weg";
    public static bool IsPath(int i) => Cells.TryGetValue(i, out var l) && l.Count == 1 && l[0] == "pad";
    public static bool IsWalk(int i) => IsRoad(i) || IsPath(i);

    // ---------- terrein ----------
    // Eén hoogte per vak (0 … 30 m), als de low-poly stand van de webversie (07b-terrein.js): een vak is een waaier van driehoeken
    // met het midden op de hoogte van het vak en elk hoekpunt op de laagste van de drie vakken die daar samenkomen. Eén verhoogd
    // vak is zo een puntje, een groep een plateau met schuine randen. Een vak met iets erop is een vlak plateau op zijn eigen
    // hoogte met een stenen sokkel tot op het maaiveld; weg- en padvakken volgen de helling naar hun buren.
    public static readonly Dictionary<int, float> Terr = new Dictionary<int, float>();
    public const float TerrTop = 90f, TerrLow = -12f;   // bergen tot 90 m (rots en sneeuw, PlanetNature.cs); afgraven kan tot 12 m onder het gewone maaiveld
    public static float G(int i) => LakeG(i, Terr.TryGetValue(i, out var h) ? h : 0f);   // een meer ligt vanzelf onder het zeeniveau (PlanetNature.cs)
    public static bool Plateau(int i) => Cells.TryGetValue(i, out var l) && l.Count > 0 && !IsWalk(i) && !OpenGround(l);   // bomen, meren en grachten liggen in het terrein (PlanetNature.cs)
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
    static float CornerBase0(int i, int k)
    {
        Topo(); int a = cnA[i][k], b = cnB[i][k];
        bool qi = Quay(i), qa = Quay(a), qb = Quay(b); DamSkip(i, a, b, ref qi, ref qa, ref qb);   // een gracht heeft kademuren: de buren lopen niet naar de bodem af; een weg over een meer is een dam (PlanetNature.cs)
        if (carved.Count == 0 && !qi && !qa && !qb) return Mathf.Min(G(i), Mathf.Min(G(a), G(b)));
        float h = float.MaxValue; if (!carved.Contains(i) && !qi) h = G(i); if (!carved.Contains(a) && !qa) h = Mathf.Min(h, G(a)); if (!carved.Contains(b) && !qb) h = Mathf.Min(h, G(b));
        if (h == float.MaxValue) h = Mathf.Min(G(i), Mathf.Min(G(a), G(b)));
        return h;
    }
    public static float OwnCorner(int i, int k) => FlatGround(i) ? G(i) : fieldCells.Contains(i) && NearSeg(Planet.Cells[i].corners[k]) ? GroundField(Planet.Cells[i].corners[k], CornerBase(i, k)) : CornerH(i, k);   // zoals het vak zelf getekend wordt: een bebouwd vak plat (met een sokkel tot op het talud)
    public static float MinCornerNoCut(int i) { Topo(); float m = G(i); for (int k = 0; k < Planet.Cells[i].corners.Length; k++) { int a = cnA[i][k], b = cnB[i][k]; if (!carved.Contains(a)) m = Mathf.Min(m, G(a)); if (!carved.Contains(b)) m = Mathf.Min(m, G(b)); } return m; }
    // een vlak plateau op de eigen hoogte (bebouwd), behalve een half wegvak dat in een geul ligt
    static bool FlatGround(int i) => Plateau(i) && !InCut(i) && !((IsHalf(i) || SlopePark(i)) && Cells[i].Count == 1 && fieldCells.Contains(i) && NearSeg(Planet.Cells[i].dir));   // een half wegvak of een park langs een weg op een niveau volgt de hoogtefunctie
    // een park (bomen) op een vak waar het talud van een weg op een niveau doorheen loopt: geen plateau, de bomen staan op het talud
    static bool SlopePark(int i) => Cells.TryGetValue(i, out var l) && l.Count == 1 && l[0] == "park" && fieldCells.Contains(i) && NearSeg(Planet.Cells[i].dir);
    static bool InCut(int i) => cuts.Count > 0 && IsHalf(i) && Carve(Planet.Cells[i].dir) < G(i) - 0.5f;

    // ---------- de geul van een onderdoorgang ----------
    // Elk portaal snijdt een geul in het terrein, naar buiten toe: een bodem van 9 m breed op de hoogte van het pad, die na een
    // half vak over twee vakken (RampL) oploopt tot het maaiveld, met zijkanten onder 45°. Alleen omlaag, nooit omhoog.
    struct Cut { public Vector3 p, o, t; public float floor, ground, hw, skew, flat, ramp, end; public bool fill; }   // fill: een dijk onder een hoger gelegde weg (floor is dan de kruin)   // skew: het portaalvlak loopt scheef (evenwijdig aan de weg erover); flat, ramp, end: 0 is FlatL, RampL, tot voorbij de helling
    static readonly List<Cut> cuts = new List<Cut>();
    static readonly HashSet<int> carved = new HashSet<int>();
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

    // Driehoekje k van vak c (midden, hoek k, hoek k+1) langs een weg op een niveau: gesneden langs de knikken van de hoogtefunctie
    // (de rand van de wegbodem, de voet van de schuine kant, het begin en eind van elk stuk weg), dan elk stuk op de hoogte uit
    // GroundField. Binnen een stuk is de functie (vrijwel) een vlak, dus de kanten worden strakke vlakken met rechte randen, zoals
    // het voorbeeld overpass_path; gedeelde randen krijgen in beide vakken dezelfde snijpunten en hoogten.
    static void LineGround(Planet.Cell c, int k, float hc0, float h1, float h2, System.Action<Vector3, Vector3, Vector3, float, float, float, bool> emit)
    {
        int m = c.corners.Length; Vector2 L1 = c.local[k], L2 = c.local[(k + 1) % m]; Vector3 D0 = c.dir, D1 = c.corners[k], D2 = c.corners[(k + 1) % m];
        float det = L1.x * L2.y - L1.y * L2.x; if (Mathf.Abs(det) < 1e-6f) return;
        Vector3 o3 = c.dir * Planet.R;
        var polys = new List<List<Vector2>> { new List<Vector2> { Vector2.zero, L1, L2 } };
        // het gewone terrein als lineaire functie van de vlakke coördinaten (zoals Base hieronder): de voet van een kant ligt waar wegdek plus talud dat raakt
        Lin B = new Lin { a = hc0, x = ((h1 - hc0) * L2.y - (h2 - hc0) * L1.y) / det, y = ((h2 - hc0) * L1.x - (h1 - hc0) * L2.x) / det };
        var ls = new List<Seg>(); var lc = new List<Cap>();   // de stukken en koppen die hier iets doen (de rest overslaan bij elk punt)
        foreach (var q in segsUse)
        {
            Vector3 ab = q.b - q.a; float len = ab.magnitude; if (len < 1e-3f) continue;
            Vector3 along = Tan(ab, c.dir), across = Vector3.Cross(c.dir, along).normalized;
            Lin T = new Lin { a = Vector3.Dot(o3 - q.a, across), x = Vector3.Dot(c.east, across), y = Vector3.Dot(c.north, across) };
            Lin S = new Lin { a = Vector3.Dot(o3 - q.a, along), x = Vector3.Dot(c.east, along), y = Vector3.Dot(c.north, along) };
            if (Mathf.Abs(T.a) > q.hw + FieldReach + c.radius + 1f || S.a < -c.radius - FieldReach - 1f || S.a > len + c.radius + FieldReach + 1f) continue;
            ls.Add(q);
            float hr = Mathf.Lerp(q.ha, q.hb, Mathf.Clamp01(S.a / len)) - 0.05f, dz = Mathf.Max(0.3f, Mathf.Abs(hr - hc0)) / FieldK;   // het wegdek (5 cm onder de weg)
            // de voet van de kant loopt schuin als de weg helt (hij ligt waar wegdek + talud het terrein raakt): een rechte lijn in (S, T)
            float sg = hr < hc0 ? 1f : -1f; Lin gap = (Lin.C(q.ha - 0.05f) + S * ((q.hb - q.ha) / len) - B) * (sg / FieldK);
            Lin toeP = T - Lin.C(q.hw) + gap, toeM = T + Lin.C(q.hw) - gap;
            foreach (var f in new[] { T - Lin.C(q.hw), T + Lin.C(q.hw), toeP, toeM, S, S - Lin.C(len), S + Lin.C(dz), S - Lin.C(len + dz) })
            {
                var next = new List<List<Vector2>>(); foreach (var pl in polys) if (!SplitBy(pl, f, next)) next.Add(pl); polys = next;
            }
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
                Lin toe = N - Lin.C(q.hw) - (B - Lin.C(q.h - 0.05f)) * (sg / FieldK);   // de voet: waar wegdek + talud het terrein raakt
                foreach (var f in new[] { N - Lin.C(q.hw), toe, N, P })   // de zijde, de voet, de graat naar de hoek, de vouw in een bocht
                {
                    var next = new List<List<Vector2>>(); foreach (var pl in polys) if (!SplitBy(pl, f, next)) next.Add(pl); polys = next;
                }
            }
        }
        foreach (var q in flatsUse)
        {
            Vector3 ab = q.b - q.a; float len = ab.magnitude; if (len < 1e-3f) continue;
            Vector3 along = Tan(ab, c.dir), across = Vector3.Cross(c.dir, along).normalized;
            Lin T = new Lin { a = Vector3.Dot(o3 - q.a, across), x = Vector3.Dot(c.east, across), y = Vector3.Dot(c.north, across) };
            Lin S = new Lin { a = Vector3.Dot(o3 - q.a, along), x = Vector3.Dot(c.east, along), y = Vector3.Dot(c.north, along) };
            if (Mathf.Abs(T.a) > q.hw + c.radius + 1f || S.a < -c.radius - 1f || S.a > len + c.radius + 1f) continue;
            foreach (var f in new[] { T - Lin.C(q.hw), T + Lin.C(q.hw), S, S - Lin.C(len) })
            {
                var next = new List<List<Vector2>>(); foreach (var pl in polys) if (!SplitBy(pl, f, next)) next.Add(pl); polys = next;
            }
        }
        System.Func<Vector2, Vector3> Dir = q2 => { float b1 = (q2.x * L2.y - q2.y * L2.x) / det, b2 = (L1.x * q2.y - L1.y * q2.x) / det; return (D0 * (1f - b1 - b2) + D1 * b1 + D2 * b2).normalized; };
        System.Func<Vector2, float> Base = q2 => { float b1 = (q2.x * L2.y - q2.y * L2.x) / det, b2 = (L1.x * q2.y - L1.y * q2.x) / det; return hc0 * (1f - b1 - b2) + h1 * b1 + h2 * b2; };
        foreach (var pl in polys)
        {
            int n = pl.Count; var P = new Vector3[n]; var H = new float[n]; Vector2 ctr = Vector2.zero;
            for (int v = 0; v < n; v++) { Vector3 d = Dir(pl[v]); H[v] = GroundField(d, Base(pl[v]), out _, ls, lc); P[v] = d * (Planet.R + H[v]); ctr += pl[v]; }
            ctr /= n; GroundField(Dir(ctr), Base(ctr), out bool steep, ls, lc);
            for (int v = 1; v + 1 < n; v++) emit(P[0], P[v], P[v + 1], H[0], H[v], H[v + 1], steep);
        }
    }
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
                if (!IsWalk(b)) continue; if (levelled.Contains(b) && b < a) continue;   // elk stuk één keer
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
                if (!IsWalk(b) || Underpass(b) || levelled.Contains(b) || (preCarve.ContainsKey(b) && b < a)) continue;
                AddWalkSeg(segs, new Seg { a = ad * Planet.R, b = Planet.Cells[b].dir * Planet.R, ha = G(a), hb = LevelFor(b, a), hw = hw, ia = a, ib = b });
            }
        }
        // gewone wegen en paden (niet op een niveau) in de buurt: onder zo'n weg ligt de grond op de hoogte van die weg, wat het talud
        // ernaast ook doet (zoals Cities: Skylines 2), anders loopt een talud bij een kruispunt over de weg heen
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
                    if (!IsWalk(b) || levelled.Contains(b) || !seen.Add(Key(a, b))) continue;
                    float fw = IsRoad(a) ? FlatHW(a) : Bike.Contains(a) ? 3.4f : 1.6f;   // een fiets- en voetpad is breder
                    AddWalkSeg(flats, new Seg { a = Planet.Cells[a].dir * Planet.R, b = Planet.Cells[b].dir * Planet.R, ha = G(a), hb = LevelFor(b, a), hw = fw, ia = a, ib = b });
                }
            }
        }
        fieldVer++;
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
                int i = q.Dequeue(); var c = Planet.Cells[i]; if (!near(c.dir * Planet.R, FieldReach + 1f + c.radius + 1f)) continue;
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
    struct Seg { public Vector3 a, b; public float ha, hb, hw; public bool capA, capB; public int ia, ib; }   // ia, ib: de vakken aan de einden
    static readonly List<Seg> flats = new List<Seg>();   // gewone wegen bij een weg op een niveau: daaronder de grond op weghoogte
    static float FlatHW(int i) { Dims(StyleOf(i), out _, out float sw); return sw + 0.3f; }   // de weg met stoep, net iets breder   // van hart a naar hart b (wereldpunten), met de weghoogte aan beide einden; cap: daar zit een kop, het stuk stopt op het hart
    static readonly List<Seg> segs = new List<Seg>();
    // de kop van een doodlopende weg: een zeshoek (vijfhoek) rond het hart van het vak, zijden recht tegenover de buren, met de
    // grond op weghoogte tot op hw van het hart en daarbuiten onder 45° terug, net als langs de weg
    struct Cap { public Vector3 c, dir; public Vector3[] n; public float h, hw; public int ic; }
    static readonly List<Cap> caps = new List<Cap>();
    static bool HasField => segs.Count > 0 || caps.Count > 0;
    const float SteepUp = 0.766f;   // cos 40°: een talud steiler dan 40° is kale aarde, flauwer is gras
    static void AddCap(int i, float h, float hw)
    {
        var cell = Planet.Cells[i]; var n = new Vector3[cell.nb.Length];
        for (int k = 0; k < n.Length; k++) n[k] = Tan(Planet.Cells[cell.nb[k]].dir - cell.dir, cell.dir);
        caps.Add(new Cap { c = cell.dir * Planet.R, dir = cell.dir, n = n, h = h, hw = hw, ic = i });
    }
    static float CapDist(Cap q, Vector3 w) { Vector3 v = w - q.c; float d = float.MinValue; foreach (var n in q.n) d = Mathf.Max(d, Vector3.Dot(v, n)); return d; }   // zeshoekige afstand tot het hart
    // de helling van de kant langs een weg op een niveau (hoogte per meter opzij; 1 = 45°), in te stellen met het schuifje Talud
    public static float FieldK = 1f / 1.5f;   // standaard 1 op 1,5 (34°), zoals het model overpass_path
    static float FieldReach => Mathf.Min(14f / FieldK, 40f);   // tot zover naast de weg kan de grond nog omhoog of omlaag lopen (45°: twee niveaus plus de marge)
    public static void SetFieldK(float k)
    {
        if (Mathf.Abs(k - FieldK) < 1e-4f) return; FieldK = k;
        var seen = new HashSet<int>(); var ring = new List<int>(levelled);   // de vakken binnen drie stappen van een weg op een niveau opnieuw
        for (int r = 0; r < 3 && ring.Count > 0; r++) { var next = new List<int>(); foreach (int i in ring) if (seen.Add(i)) { Touch(i); foreach (int j in Planet.Cells[i].nb) next.Add(j); } ring = next; }
        Rebuild(); RebuildGrid();
    }
    static bool NearSeg(Vector3 dir, float extra = 0f)
    {
        Vector3 w = dir * Planet.R; float fr = FieldReach + 1f + extra;
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
    static void UseAll() { segsUse = segs; flatsUse = flats; capsUse = caps; }
    static float GroundField(Vector3 dir, float base_, out bool slope, List<Seg> segs, List<Cap> caps)   // slope: het punt ligt op de schuine kant van een weg
    {
        float h = base_; Vector3 w = dir * Planet.R; slope = false; float onLevelH = float.MaxValue, lowRoad = float.MaxValue;   // op de weg zelf van een weg op een niveau: de laagste daarvan; en de laagste weg die hier de grond omlaag haalt
        for (int pass = 0; pass < 2; pass++)
        {
            foreach (var q in segs)
            {
                Vector3 ab = q.b - q.a; float l2 = ab.sqrMagnitude; if (l2 < 1e-4f) continue; float len = Mathf.Sqrt(l2), u = Vector3.Dot(w - q.a, ab) / l2;
                Vector3 dv = w - (q.a + ab * u); dv -= dir * Vector3.Dot(dv, dir); float lat = dv.magnitude, over = Mathf.Max(0f, Mathf.Max(-u, u - 1f) * len);   // naast het stuk, en voorbij een eind
                if (lat > q.hw + FieldReach || u < -1e-4f || u > 1f + 1e-4f) continue;   // (met wat speling: op het hart zelf valt een punt door afronding anders net buiten beide stukken) alleen naast het stuk: voorbij het eind doet de kop het, of het volgende stuk (een rechte kop gaf daar een trapje)
                float hr = Mathf.Lerp(q.ha, q.hb, Mathf.Clamp01(u)), rise = Mathf.Max(Mathf.Max(0f, lat - q.hw), over) * FieldK;   // de kant, of de kop
                if (lat <= q.hw) onLevelH = Mathf.Min(onLevelH, hr);
                if (pass == 0 && hr > base_ + 0.01f) { float v = hr - rise - 0.05f; if (v > h) { h = v; slope = rise > 0f; } }        // talud (5 cm onder het wegdek: geen streepje gras op de weg)
                else if (pass == 1 && hr - 0.05f < base_) { float v = hr + rise - 0.05f; lowRoad = Mathf.Min(lowRoad, hr); if (v < h) { h = v; slope = rise > 0f; } }   // uitgraving: pas als het wegdek (5 cm onder de weg) onder het terrein ligt, dan loopt het veld zonder sprong door
            }
            foreach (var q in caps)
            {
                float rise = Mathf.Max(0f, CapDist(q, w) - q.hw) * FieldK; if (rise > FieldReach * FieldK) continue; if (rise <= 0f) onLevelH = Mathf.Min(onLevelH, q.h);
                if (pass == 0 && q.h > base_ + 0.01f) { float v = q.h - rise - 0.05f; if (v > h) { h = v; slope = rise > 0f; } }   // een kop ligt net als de weg 5 cm onder het wegdek
                else if (pass == 1 && q.h - 0.05f < base_) { float v = q.h + rise - 0.05f; if (v < h) { h = v; slope = rise > 0f; } }
            }
        }
        // gewone wegen erbij: onder de weg de hoogte van die weg, ernaast hooguit het talud hoger of lager (zacht, zonder sprong),
        // zodat het talud van een weg op een niveau nooit over een gewone weg heen loopt
        foreach (var q in flatsUse)   // waar twee wegen over elkaar liggen wint de laagste (een helling naar een kruispunt, een weg onder een brug)
        {
            Vector3 ab = q.b - q.a; float l2 = ab.sqrMagnitude; if (l2 < 1e-4f) continue; float len = Mathf.Sqrt(l2), u = Vector3.Dot(w - q.a, ab) / l2;
            Vector3 dv = w - (q.a + ab * Mathf.Clamp01(u)); dv -= dir * Vector3.Dot(dv, dir); float over = Mathf.Max(0f, Mathf.Max(-u, u - 1f) * len);
            float rise = Mathf.Max(Mathf.Max(0f, dv.magnitude - q.hw), over) * FieldK; if (rise > 20f) continue;
            float hr = Mathf.Lerp(q.ha, q.hb, Mathf.Clamp01(u)) - 0.03f; if (onLevelH <= hr + 0.3f) continue;
            // gaat hier een weg een heel niveau lager onder deze weg door (een brug), dan mag de bak eronder: alleen geen grond erboven
            float nh = lowRoad <= hr - LevelH + 0.1f ? Mathf.Min(h, hr + rise) : Mathf.Clamp(h, hr - rise, hr + rise);
            if (Mathf.Abs(nh - h) > 1e-4f) { h = nh; slope = rise > 0f; }
        }
        if (carved.Count > 0 && cuts.Count > 0) h = Mathf.Min(h, Carve(dir));
        return h;
    }
    // ligt wat onder brugvak i door gaat op een eigen niveau (dan maakt het zijn eigen geul, zonder portaalhelling)?
    static bool UnderIsLevelled(int i, bool road)
    {
        var ends = road ? TunnelEnds(i) : new List<int>(); if (!road) foreach (int j in Partners(i)) if (IsPath(j)) ends.Add(j);
        if (ends.Count == 0) return false; foreach (int j in ends) if (WalkLevel(j) == 0) return false; return true;
    }
    // de hoogte van het maaiveld rond een pad: het gemiddelde van de buren die geen weg of pad zijn (null: er zijn er geen)
    static float? PadGround(int i) { float s = 0f; int n = 0; foreach (int j in Planet.Cells[i].nb) if (!IsWalk(j) && !Underpass(j)) { s += G(j); n++; } return n > 0 ? s / n : (float?)null; }
    // het niveau van een weg- of padvak ten opzichte van het maaiveld eromheen, in halve niveaus (0: gewoon, of een vak dat een geul omlaag zette)
    static int WalkLevel(int i)
    {
        if (!IsWalk(i) || Underpass(i) || preCarve.ContainsKey(i)) return 0; var pg = PadGround(i); if (!pg.HasValue) return 0;
        float d = (G(i) - pg.Value) / StepH; return d <= -0.5f ? Mathf.Min(-1, Mathf.RoundToInt(d)) : d >= 0.5f ? Mathf.Max(1, Mathf.RoundToInt(d)) : 0;
    }
    // Wegen en paden op een niveau zetten (Page Up/Down): lv halve niveaus (StepH) boven of onder het maaiveld eromheen. Lager: wat de
    // lijn kruist blijft liggen en de nieuwe weg of het pad gaat eronderdoor (een vak dat al onder een weg door ging, zakt mee).
    // Hoger: een bestaande weg die de lijn recht kruist, gaat onder de nieuwe weg door. Zo past één niveau lager of hoger altijd
    // precies onder of over een weg op het maaiveld. Een half niveau is te weinig om onder of over een weg te gaan: daar wordt het
    // een kruispunt (de weg helt er naartoe).
    public static float LevelTarget(int c, int lv) => (PadGround(c) ?? G(c)) + lv * StepH;   // de hoogte van vak c op niveau lv (voor het voorbeeld)
    // Voor het voorbeeld tijdens het slepen: wordt vak c van de lijn een brug? +1: de nieuwe weg gaat erover (dek op deck), -1: de
    // nieuwe gaat eronderdoor (de bestaande weg is het dek), 0: niets. Dezelfde regels als SetLevel, Place en PlaceLine.
    public static int CrossingPreview(int c, List<int> line, int lv, string type, out float deck)
    {
        deck = G(c); if (!IsWalk(c) || Underpass(c)) return 0; bool road = type == "weg";
        var other = new List<int>(); foreach (int j in Partners(c)) { if (!IsWalk(j) || line.Contains(j)) continue; int n = 0; foreach (int x in Partners(j)) if (x != c) n++; if (n >= 1) other.Add(j); }
        if (other.Count < 2) return 0;
        float target = LevelTarget(c, lv), diff = target - G(c);
        if (diff <= -(LevelH - 0.1f)) return -1;
        if (diff >= LevelH - 0.1f) { if (!road) return 0; deck = target; return 1; }
        if (road && IsPath(c)) { deck = target; return 1; }   // een weg over een pad: het pad gaat eronderdoor
        if (!road && IsRoad(c)) return -1;                    // een pad onder een weg door
        return 0;
    }
    // Een los geklikt vak op een niveau: kan dat hier? Niet op een weg of pad dat op een andere hoogte ligt, en niet naast een weg
    // op een andere hoogte die daar gewoon doorloopt (een kruising maak je door te slepen, dan gaat de weg eronderdoor of eroverheen).
    // Naast het eind van een weg mag wel: dat wordt de helling.
    public static string LevelClickProblem(int c, int lv, string type)
    {
        float target = LevelTarget(c, lv);
        if (IsWalk(c) && !Underpass(c) && Mathf.Abs(G(c) - target) > 0.3f) return "Dit vak is al een weg of pad op een andere hoogte: sleep de nieuwe eroverheen, dan gaat die eronderdoor of eroverheen";
        if (Underpass(c)) return "Dit is al een brug of onderdoorgang";
        foreach (int j in ClickLinks(c))
        {
            if (!IsWalk(j) || Underpass(j) || Mathf.Abs(G(j) - target) <= StepH + 0.05f) continue;
            int n = 0; foreach (int k in Partners(j)) if (k != c) n++;
            if (n >= 2) return "De weg hiernaast ligt op een andere hoogte en loopt door: sleep om eronderdoor of eroverheen te gaan";
        }
        return null;
    }
    // Wegen en paden die net gelegd zijn op hun niveau zetten (Page Up/Down): lv halve niveaus (StepH) boven of onder het
    // maaiveld eromheen, ook lv 0. Waar de lijn een bestaande weg kruist, beslist het hoogteverschil tussen de nieuwe en de
    // bestaande weg: minstens een heel niveau lager → de nieuwe gaat eronderdoor, minstens een heel niveau hoger → de bestaande
    // gaat eronderdoor (als die recht kruist), minder → een kruispunt op de hoogte van de bestaande weg (de nieuwe helt ernaartoe).
    // Een bestaande weg wordt dus nooit verzet. Een pad onder een weg door regelt Place al (onderdoorgang); een pad hoger dan een
    // weg kan nog niet (loopbrug).
    // Ongelijkvloers op het maaiveld (U, of een pad dat onder een weg door gaat): de lijn duikt alleen bij een kruising onder de
    // andere weg door, als een weg op een niveau (zodat de bak, de koker en het betonblok van het model overpass_path gelden in plaats
    // van de oude portaalgeul): de vakken naast de kruising een niveau lager, de vakken daarnaast een half niveau (de helling)
    public static void DipUnder(List<int> line)
    {
        if (line == null || line.Count < 3) return;
        var depth = new Dictionary<int, int>();   // index in de lijn → halve niveaus omlaag (2 = een niveau)
        bool Own(int c) { foreach (int j in Partners(c)) if (!line.Contains(j)) return false; return true; }   // hoort het vak ook bij een andere weg (kruispunt, aansluiting), dan blijft het liggen
        void Mark(int k, int d) { if (k < 0 || k >= line.Count || Underpass(line[k]) || !Own(line[k])) return; depth[k] = Mathf.Max(depth.TryGetValue(k, out var o) ? o : 0, d); }
        for (int k = 0; k < line.Count; k++)
        {
            int c = line[k];
            bool cross = Underpass(c) && ((PadUnder(c) && ((k > 0 && IsPath(line[k - 1])) || (k + 1 < line.Count && IsPath(line[k + 1])))) || (k > 0 && k + 1 < line.Count && TunnelLinks.Contains(Key(line[k - 1], line[k + 1]))));
            if (cross)
            {
                Mark(k - 1, 2); Mark(k + 1, 2); Mark(k - 2, 1); Mark(k + 2, 1);
                Low[c] = Mathf.Min(Low.TryGetValue(c, out var lo) ? lo : G(c), G(c) - LevelH); Touch(c);
            }
            if (k > 0 && LowerLinks.Contains(Key(line[k - 1], c))) { Mark(k - 1, 2); Mark(k, 2); Mark(k - 2, 1); Mark(k + 1, 1); }   // schuin onder een weg door
        }
        foreach (var kv in depth)
        {
            int c = line[kv.Key]; if (!IsWalk(c)) continue; float b = PadGround(c) ?? G(c);
            LevelTerr(c, b - kv.Value * StepH); Touch(c); foreach (int j in Partners(c)) Touch(j);
        }
    }
    public static void SetLevel(List<int> line, int lv, string type)
    {
        if (line == null) return; float carry = float.NaN; bool road = type == "weg";
        for (int k = 0; k < line.Count; k++)
        {
            int c = line[k]; if (!IsWalk(c)) continue;
            float? pg = PadGround(c); float b = pg ?? (float.IsNaN(carry) ? G(c) : carry); carry = b; float target = b + lv * StepH;
            if (Underpass(c))   // een kruising die het leggen net maakte (of die er al was): de nieuwe weg of het pad eronder op zijn niveau
            {
                bool padUnder = PadUnder(c);
                if (lv < 0 && road && padUnder) LastNote = "Een weg lager dan een pad: het pad gaat er nog onderdoor (een loopbrug kan nog niet)";
                else if (lv > 0 && !road && padUnder) LastNote = "Een pad hoger dan het maaiveld gaat nog onder een weg door (een loopbrug kan nog niet)";
                else if (lv > 0 && road && target > G(c) + 0.1f)   // het dek omhoog; eronder blijft de weg of het pad op zijn eigen hoogte (de geul wordt ondieper)
                {
                    float under = UnderLevel(c); LevelTerr(c, target); Low[c] = Mathf.Min(under, G(c) - UnderClear); Touch(c);
                    foreach (int j in Partners(c)) Touch(j);
                }
                else if (Low.TryGetValue(c, out var lo) && target < G(c) - LevelH + 0.1f) { Low[c] = Mathf.Min(lo, target); Touch(c); }
                continue;
            }
            // een bestaande weg (of pad) die hier gekruist wordt: zijn eigen verbindingen, niet die van de lijn
            // (een buur telt mee als die zelf verder loopt; een vak dat alleen aan c hangt is het vorige vak van een weg die vak voor
            // vak gebouwd wordt). Eén zo'n buur: de lijn sluit aan op het eind van een weg, dit vak wordt de helling. Twee of meer:
            // een weg loopt hier door. Aan het begin of eind van de lijn is dat een aansluiting op die weg, op zijn hoogte.
            var other = new List<int>();
            foreach (int j in Partners(c))
            {
                if (!IsWalk(j) || line.Contains(j)) continue; int n = 0; foreach (int x in Partners(j)) if (x != c) n++;
                if (n >= 1 || Underpass(j)) other.Add(j);
            }
            bool existing = other.Count >= 2 && Mathf.Abs(G(c) - target) > 0.3f;
            if (!existing) { if (Mathf.Abs(G(c) - target) > 0.005f || preCarve.ContainsKey(c)) LevelTerr(c, target); Touch(c); continue; }   // ook als de geul van een brug het vak al op die hoogte had gezet: dan is het een vak op een niveau, geen uitgegraven vak
            if (k == 0 || k == line.Count - 1) { if (Mathf.Abs(G(c) - target) >= LevelH - 0.1f) LastNote = "De weg sluit aan op een weg op een andere hoogte: hij helt ernaartoe"; continue; }
            float diff = target - G(c);
            if (diff <= -(LevelH - 0.1f))   // de nieuwe gaat onder de bestaande door
            {
                if (!road) continue;   // een pad onder een weg: Place maakte al een onderdoorgang, anders blijft het een kruising
                int p = k > 0 ? line[k - 1] : -1, q = k + 1 < line.Count ? line[k + 1] : -1;
                if (p < 0 || q < 0 || MidCell(p, q) != c || Under.ContainsKey(c) || other.Count != 2 || MidCell(other[0], other[1]) != c) {   // ook de bestaande weg recht door c (geen brug in een bocht)
                     LastNote = "Hier kon de weg niet onder de bestaande door (sleep er in een rechte lijn overheen): het werd een kruispunt"; continue; }
                foreach (int j in new[] { p, q }) { Links.Remove(Key(c, j)); OneWay.Remove(DKey(c, j)); OneWay.Remove(DKey(j, c)); partners[c].Remove(j); partners[j].Remove(c); Touch(j); }
                LinkTunnel(p, q); if (DragOneWay) OneWay.Add(DKey(p, q));
                Under[c] = "weg"; Low[c] = target; Touch(c);
            }
            else if (diff >= LevelH - 0.1f)   // de bestaande gaat onder de nieuwe door
            {
                if (!road) { LastNote = "Een pad over een weg heen kan nog niet (loopbrug): het werd een kruising"; continue; }
                int pp = k > 0 ? line[k - 1] : -1, qq = k + 1 < line.Count ? line[k + 1] : -1;
                if (pp < 0 || qq < 0 || MidCell(pp, qq) != c) { LastNote = "Een brug moet recht zijn: sleep de nieuwe weg in een rechte lijn over de bestaande (het werd een kruispunt)"; continue; }
                if (!PassUnder(c, other)) continue;
                LevelTerr(c, target); Touch(c);
            }
            else LastNote = "Te weinig hoogteverschil met de weg die je kruist (minstens een heel niveau): het werd een kruispunt op de hoogte van die weg";
        }
        CarvePaths();
    }
    // de eigen hoogte van wat onder brugvak c door gaat: zijn buren buiten de geul (hun hoogte van voor de geul)
    static float UnderLevel(int c)
    {
        float s = 0f; int n = 0; var ends = Under.TryGetValue(c, out var u) && u == "weg" ? TunnelEnds(c) : null;
        if (ends == null) { ends = new List<int>(); foreach (int j in Partners(c)) if (IsPath(j)) ends.Add(j); }
        foreach (int j in ends) { s += preCarve.TryGetValue(j, out var pre) ? pre : G(j); n++; }
        return n > 0 ? s / n : (PadGround(c) ?? G(c));
    }
    // een bestaande weg die hier gekruist wordt (other: zijn twee buren) gaat onder vak c door: een tunnel van de ene buur naar de
    // andere, zoals ongelijkvloers maar andersom; c wordt het dek. Lukt alleen als hij recht kruist.
    // loopt de weg (of het pad) recht door vak c? Een brug kan niet in een bocht of op een kruispunt liggen.
    static bool StraightThrough(int c) { var w = new List<int>(); foreach (int j in Partners(c)) if (IsWalk(j)) w.Add(j); return w.Count == 2 && MidCell(w[0], w[1]) == c; }
    static bool PassUnder(int c, List<int> other)
    {
        if (Under.ContainsKey(c)) return false;
        if (other.Count != 2 || MidCell(other[0], other[1]) != c) { LastNote = "Hier kon de bestaande weg niet onder de nieuwe door (hij kruist niet recht, of het is een kruispunt): het werd een kruispunt"; return false; }
        int a = other[0], b = other[1], f = Flow(a, c);
        foreach (int j in other) { Links.Remove(Key(c, j)); OneWay.Remove(DKey(c, j)); OneWay.Remove(DKey(j, c)); partners[c].Remove(j); partners[j].Remove(c); Touch(j); }
        LinkTunnel(a, b); if (f > 0) OneWay.Add(DKey(a, b)); else if (f < 0) OneWay.Add(DKey(b, a));
        Under[c] = "weg"; Low[c] = G(c); return true;
    }
    // hoort vak i bij de weg óver een onderdoorgang (een buur van het brugvak op de hoogte van het dek)? Die blijft uit de geul.
    static bool UpperRoad(int i) { if (!IsRoad(i)) return false; foreach (int j in Partners(i)) if (Underpass(j) && Mathf.Abs(G(j) - G(i)) < 0.3f) return true; return false; }
    // padvakken in een geul op de hoogte van de geul zetten (alleen omlaag)
    public static void CarvePaths()
    {
        cuts.Clear(); deckSide.Clear(); CollectCuts(cuts, deckSide); if (cuts.Count == 0) return;
        foreach (var kv in new List<int>(Cells.Keys)) if (IsWalk(kv) && !Underpass(kv) && !UpperRoad(kv) && !preLevel.ContainsKey(kv)) { float v = Carve(Planet.Cells[kv].dir); if (v < G(kv) - 0.01f) { if (!preCarve.ContainsKey(kv)) preCarve[kv] = G(kv); SetTerr(kv, v); } }   // paden en de onderste weg
    }
    // bij het bijwerken: de geulen opnieuw bepalen; wat verandert, wordt opnieuw getekend
    static string cutSig = "";
    static void UpdateCuts()
    {
        var orphan = new List<int>(); foreach (var kv in Under) if (kv.Value == "weg" && TunnelEnds(kv.Key).Count == 0) orphan.Add(kv.Key);   // brug zonder weg eronder: weg ermee
        foreach (int i in orphan) { if (Low.TryGetValue(i, out var lo0) && !IsRoad(i)) SetTerr(i, lo0); Under.Remove(i); Low.Remove(i); Touch(i); }
        cuts.Clear(); deckSide.Clear(); CollectCuts(cuts, deckSide); RestoreCarve();
        var now = new HashSet<int>(); var sb = new System.Text.StringBuilder(); var keys = new Dictionary<string, Vector3>();
        foreach (var q in cuts) { string ck = q.p.ToString("F2") + q.floor.ToString("F2") + q.ground.ToString("F2"); sb.Append(ck).Append('|'); keys[ck] = q.p; }
        foreach (int lc in levelled) now.Add(lc);   // een vak op een niveau: zijn buren rekenen hun hoekpunten zonder zijn hoogte (de geul of dijk komt uit de stroken)
        foreach (var kv in Cells) if (IsWalk(kv.Key) && !Underpass(kv.Key) && !UpperRoad(kv.Key) && cuts.Count > 0) { float v = Carve(Planet.Cells[kv.Key].dir); if (v < float.MaxValue && G(kv.Key) <= v + 0.02f && v < MaxGround(kv.Key) - 0.05f) now.Add(kv.Key); }
        string sig = sb.ToString(); if (sig == cutSig && now.SetEquals(carved)) return;
        cutSig = sig;
        // alleen opnieuw tekenen wat veranderde (vroeger de hele planeet, bij elke gumstreek): rond een geul die kwam of verdween, en
        // drie stappen rond een vak dat in of uit een geul of niveau ging
        var gone = new List<Vector3>(); foreach (var kv in cutKeys) if (!keys.ContainsKey(kv.Key)) gone.Add(kv.Value);
        foreach (var kv in keys) if (!cutKeys.ContainsKey(kv.Key)) gone.Add(kv.Value);
        cutKeys = keys;
        if (gone.Count > 0)
            for (int i = 0; i < Planet.Cells.Length; i++) { Vector3 w = Planet.Cells[i].dir * Planet.R; foreach (var gp in gone) if ((w - gp).sqrMagnitude < 75f * 75f) { Touch(i); break; } }
        var diff = new HashSet<int>(now); diff.SymmetricExceptWith(carved);
        var seen = new HashSet<int>(); var ring = new List<int>(diff);
        for (int r = 0; r < 4 && ring.Count > 0; r++) { var next = new List<int>(); foreach (int i in ring) if (seen.Add(i)) { Touch(i); foreach (int j in Planet.Cells[i].nb) next.Add(j); } ring = next; }
        carved.Clear(); carved.UnionWith(now);
    }
    static Dictionary<string, Vector3> cutKeys = new Dictionary<string, Vector3>();
    static float MaxGround(int i) { float g = float.MinValue; foreach (var q in cuts) if (!q.fill) g = Mathf.Max(g, q.ground); return g; }
    public static float FloorH(int i) { float m = Mathf.Min(G(i), MinCorner(i)); return Low.TryGetValue(i, out var lo) ? Mathf.Min(m, lo) : m; }   // het laagste punt in vak i
    public static float MinCorner(int i) { float m = G(i); for (int k = 0; k < Planet.Cells[i].corners.Length; k++) m = Mathf.Min(m, CornerH(i, k)); return m; }
    // hoogte van het oppervlak van vak i (maaiveld, of de weg die er helt) onder een wereldpunt
    public static float SurfaceH(int i, Vector3 world) { if (IsPath(i)) return PathSurfaceH(i, world); var fr = new Frame(i); Vector3 d = world - fr.o; return G(i) + fr.H(Vector3.Dot(d, fr.e), Vector3.Dot(d, fr.n)); }
    // idem voor iemand die van vak 'from' komt of erheen gaat: op het pad onder een brug is dat de hoogte van het pad
    public static float SurfaceH(int i, int from, Vector3 world)
    {
        if (!UnderLink(i, from)) return SurfaceH(i, world);
        var fr = new Frame(i, false, true); Vector3 d = world - fr.o; return LevelFor(i, from) + fr.H(Vector3.Dot(d, fr.e), Vector3.Dot(d, fr.n));
    }

    // Verbindingen tussen weg- en padvakken. Twee wegvakken naast elkaar zijn alleen verbonden als ze in dezelfde lijn
    // gesleept zijn of met een klik aan elkaar gezet; zo kunnen twee wegen naast elkaar lopen zonder in elkaar over te gaan.
    public static readonly HashSet<long> Links = new HashSet<long>();
    static readonly Dictionary<int, List<int>> partners = new Dictionary<int, List<int>>();
    static readonly List<int> none = new List<int>();
    static long Key(int i, int j) => i < j ? ((long)i << 20) | (uint)j : ((long)j << 20) | (uint)i;
    public static void ClearLinks() { Links.Clear(); partners.Clear(); Style.Clear(); Bike.Clear(); Rot.Clear(); Under.Clear(); Low.Clear(); OneWay.Clear(); TunnelLinks.Clear(); LowerLinks.Clear(); preCarve.Clear(); preLevel.Clear(); }
    public static bool Linked(int i, int j) => Links.Contains(Key(i, j));
    // Per wegvak: stijl (1 = brede weg, 2 = laan; ontbreekt = gewone weg), brug (wat eronder ligt: meer, water of kuil) en
    // eenrichtingsverkeer per verbinding (alleen van i naar j)
    public static readonly Dictionary<int, byte> Style = new Dictionary<int, byte>();
    public static readonly Dictionary<int, string> Under = new Dictionary<int, string>();
    public static readonly HashSet<long> OneWay = new HashSet<long>();
    // Onderdoorgang: een weg over een pad wordt een brug (Under = "pad"); het pad loopt eronder door op zijn eigen hoogte (Low),
    // in een betonnen sleuf. G is dan de hoogte van het brugdek, minstens UnderClear boven het pad.
    public static readonly Dictionary<int, float> Low = new Dictionary<int, float>();
    // Een niveau is 4,5 m: een pad één niveau lager (Page Down bij het slepen) past precies onder een weg door. Een onderdoorgang is
    // daarom altijd één niveau diep: 3,4 m vrije hoogte onder een dek van 1,08 m (zoals het voorbeeld overpass_path).
    public const float LevelH = 4.5f, StepH = LevelH * 0.5f;   // een niveau, en de stap van Page Up/Down: een half niveau
    const float UnderClear = LevelH, DeckT = 1.08f;
    public static bool Underpass(int i) => Under.TryGetValue(i, out var u) && (u == "pad" || u == "weg");
    static bool PadUnder(int i) => Under.TryGetValue(i, out var u) && u == "pad";
    // de hoogte van vak i gezien vanaf buur j: een pad dat de onderdoorgang in loopt, ziet het pad onder de brug
    public static float LevelFor(int i, int j) => PadUnder(i) && IsPath(j) && Low.TryGetValue(i, out var lo) ? lo : G(i);
    // hoort de verbinding i–j bij het pad onder de brug (en niet bij de weg erover)?
    public static bool UnderLink(int i, int j) => PadUnder(i) && IsPath(j);
    // Een weg en een pad die elkaar kruisen: de weg blijft op het maaiveld, het pad gaat er in een geul onderdoor (zoals het
    // voorbeeld overpass_path). Ligt het pad al diep genoeg, dan blijft het daar; anders zakt het tot UnderClear onder het dek.
    static void MakeUnderpass(int i, List<string> l)   // een weg over een pad
    {
        float ground = float.MinValue; foreach (int j in Planet.Cells[i].nb) if (!IsPath(j) && !Underpass(j)) ground = Mathf.Max(ground, G(j));
        float deck = ground > float.MinValue ? Mathf.Max(ground, G(i)) : G(i);
        Under[i] = "pad"; Low[i] = Mathf.Min(G(i), deck - UnderClear); l[0] = "weg"; LevelTerr(i, deck);
    }
    static bool DigUnder(int i)   // een pad over een weg
    {
        if (Under.ContainsKey(i)) return false;
        Under[i] = "pad"; Low[i] = G(i) - UnderClear; Touch(i); foreach (int j in Partners(i)) Touch(j); return true;
    }
    // Kruisingen uit een oudere opslag: een schuine weg die tussen twee padvakken door springt, wordt alsnog een onderdoorgang
    // (de weg gaat dan door een van die padvakken, net als bij het leggen)
    static void FixCrossings()
    {
        foreach (int h in new List<int>(Under.Keys))
        {
            if (!PadUnder(h)) continue; var rp = new List<int>(); foreach (int j in Partners(h)) if (IsRoad(j)) rp.Add(j);
            if (rp.Count != 2 || IsNeighbour(rp[0], rp[1]) || !Adjacent2(rp[0], rp[1]) || !Between(rp[0], rp[1]).Contains(h)) continue;
            int a = rp[0], c = rp[1]; int flow = Flow(a, h);
            foreach (int j in rp) { Links.Remove(Key(h, j)); OneWay.Remove(DKey(h, j)); OneWay.Remove(DKey(j, h)); partners[h].Remove(j); partners[j].Remove(h); }
            Cells[h] = new List<string> { "pad" }; Under.Remove(h); Style.Remove(h); if (Low.TryGetValue(h, out var lo)) Terr[h] = lo; Low.Remove(h);
            Link(a, c); if (flow > 0) OneWay.Add(DKey(a, c)); else if (flow < 0) OneWay.Add(DKey(c, a));
        }
        DigCrossings();
    }
    public static void DigTrench(int i) => CarvePaths();
    // brug weghalen: wat eronder lag komt terug (een pad weer op zijn eigen hoogte, met zijn padverbindingen)
    static void RestoreUnder(int i, string under)
    {
        Cells[i] = new List<string> { under }; Under.Remove(i); Style.Remove(i);
        if (under == "pad")
        {
            foreach (int j in new List<int>(Partners(i))) if (!IsPath(j)) { Links.Remove(Key(i, j)); OneWay.Remove(DKey(i, j)); OneWay.Remove(DKey(j, i)); partners[i].Remove(j); if (partners.TryGetValue(j, out var b)) b.Remove(i); }
            if (Low.TryGetValue(i, out var plo)) { float org = preLevel.TryGetValue(i, out var p0) ? p0 : G(i); preCarve.Remove(i); SetTerr(i, plo); preLevel[i] = org; }   // het pad op zijn eigen hoogte; weghalen zet later het maaiveld van voor de brug terug
            Low.Remove(i);
        }
        else if (under == "weg")   // de bovenste weg weg: de onderste weg loopt weer gewoon door dit vak, op zijn eigen hoogte
        {
            Unlink(i); var ends = new List<long>(); foreach (long k in TunnelLinks) { int a = (int)(k >> 20), b = (int)(k & 0xFFFFF); if (MidCell(a, b) == i) ends.Add(k); }
            foreach (long k in ends) { int a = (int)(k >> 20), b = (int)(k & 0xFFFFF); Links.Remove(k); TunnelLinks.Remove(k); partners[a].Remove(b); partners[b].Remove(a); Link(a, i); Link(i, b); }
            if (Low.TryGetValue(i, out var wlo)) { float org = preLevel.TryGetValue(i, out var p0) ? p0 : G(i); preCarve.Remove(i); SetTerr(i, wlo); preLevel[i] = org; }   // de onderste weg op zijn eigen hoogte; weghalen zet later het maaiveld terug
            Low.Remove(i);
        }
        else Unlink(i);
        Touch(i);
    }
    public static byte DragStyle; public static bool DragOneWay; public static bool Lights = true;   // instellingen voor wat je nu sleept
    // fiets- en voetpaden: een pad met een rood fietsdeel in het midden (DragBike: wat je nu legt is er een)
    public static readonly HashSet<int> Bike = new HashSet<int>(); public static bool DragBike;
    public static bool IsBike(int i) => Bike.Contains(i) && IsWalk(i);
    public static bool DragUnder;   // ongelijkvloers: de weg die je sleept gaat onder kruisende wegen door
    public static int DragLevel;    // het niveau van de weg of het pad dat je legt, in halve niveaus (StepH): 0 op het maaiveld, -4 tot +4 (Page Up/Down)
    // Een weg onder een andere weg door. Recht kruisen: de onderste weg verbindt het vak vóór met het vak na het kruisvak
    // (TunnelLinks, twee stappen recht door), zodat de twee wegen elkaar in de graaf niet raken; het kruisvak is de brug
    // (Under = "weg"). Schuin kruisen: de verbinding die onder de andere weg door gaat staat in LowerLinks.
    public static readonly HashSet<long> TunnelLinks = new HashSet<long>(), LowerLinks = new HashSet<long>();
    static void LinkTunnel(int a, int b)
    {
        if (a == b || !Links.Add(Key(a, b))) { TunnelLinks.Add(Key(a, b)); return; }
        TunnelLinks.Add(Key(a, b));
        if (!partners.TryGetValue(a, out var pa)) partners[a] = pa = new List<int>(6); pa.Add(b);
        if (!partners.TryGetValue(b, out var pb)) partners[b] = pb = new List<int>(6); pb.Add(a);
    }
    // het vak midden tussen a en b (twee stappen recht door)
    static int StraightMid(int a, int b) { var bt = Between(a, b); return bt.Count == 1 ? bt[0] : -1; }
    // het vak precies midden tussen a en b op een rechte lijn (twee stappen recht door, of twee keer schuin over een hoekpunt)
    static int MidCell(int a, int b)
    {
        int m = Planet.Nearest((Planet.Cells[a].dir + Planet.Cells[b].dir).normalized * Planet.R);
        Vector3 c = Planet.Cells[m].dir, va = Planet.Cells[a].dir - c, vb = Planet.Cells[b].dir - c;
        return Vector3.Dot(va.normalized, vb.normalized) < -0.97f ? m : -1;
    }
    // de richting van de weg over brugvak i (rechtdoor tussen twee wegburen, of naar de enige wegbuur); zero bij een kruispunt
    static Vector3 UpperRoadDir(int i)
    {
        var r = new List<int>(); foreach (int j in Partners(i)) if (IsRoad(j)) r.Add(j); Vector3 c = Planet.Cells[i].dir;
        if (r.Count == 2) return Tan(Planet.Cells[r[1]].dir - Planet.Cells[r[0]].dir, c);
        if (r.Count == 1) return Tan(Planet.Cells[r[0]].dir - c, c);
        return Vector3.zero;
    }
    // de buur van brugvak i in de richting van een opening (of -1 als de opening schuin over een hoekpunt gaat)
    static int OpeningNb(int i, Vector3 od) { Vector3 c = Planet.Cells[i].dir; foreach (int j in Planet.Cells[i].nb) if (Vector3.Dot(Tan(Planet.Cells[j].dir - c, c), od) > 0.99f) return j; return -1; }
    // de richting van de weg waarmee het portaal van die opening evenwijdig loopt (zero: recht)
    static Vector3 SkewFor(int i, Vector3 od) => OpeningNb(i, od) >= 0 ? UpperRoadDir(i) : Vector3.zero;
    static Vector2 Loc(Vector3 v, Vector3 e, Vector3 n) => v == Vector3.zero ? Vector2.zero : new Vector2(Vector3.Dot(v, e), Vector3.Dot(v, n)).normalized;
    // de richtingen waarin het pad of de onderste weg een brugvak in en uit gaat (naar een buur of schuin over een hoekpunt)
    static List<Vector3> Openings(int i)
    {
        var o = new List<Vector3>(); Vector3 c = Planet.Cells[i].dir;
        if (Under.TryGetValue(i, out var u) && u == "weg") { foreach (int j in TunnelEnds(i)) o.Add(Tan(Planet.Cells[j].dir - c, c)); }
        else foreach (int j in Partners(i)) if (IsPath(j)) o.Add(Tan(Planet.Cells[j].dir - c, c));
        return o;
    }
    // de wegvakken voor en na de brug in vak i (waar de onderste weg onder i door gaat)
    static List<int> TunnelEnds(int i) { var o = new List<int>(); foreach (long k in TunnelLinks) { int a = (int)(k >> 20), b = (int)(k & 0xFFFFF); if (MidCell(a, b) == i) { o.Add(a); o.Add(b); } } return o; }
    static long DKey(int i, int j) => ((long)i << 20) | (uint)j;
    public static int Flow(int i, int j) => OneWay.Contains(DKey(i, j)) ? 1 : OneWay.Contains(DKey(j, i)) ? -1 : 0;   // +1: alleen van i naar j, -1: alleen van j naar i
    public static bool CanDrive(int i, int j) => Flow(i, j) >= 0;
    static int styleOverride = -1;   // bouwvoorbeeld: elke weg in deze stijl tekenen (het wegtype dat je gaat leggen)
    public static byte StyleOf(int i) => styleOverride >= 0 ? (byte)styleOverride : Style.TryGetValue(i, out var s) ? s : (byte)0;
    static void SetStyle(int i) { if (DragStyle == 0) Style.Remove(i); else Style[i] = DragStyle; }
    // de vakken waarmee i verbonden is (buren, of vakken op afstand 2 recht door de hoek: dan loopt de weg over twee halve vakken)
    public static List<int> Partners(int i) => partners.TryGetValue(i, out var l) ? l : none;
    public static bool Adjacent2(int i, int j) => System.Array.IndexOf(Planet.Cells[i].nb, j) >= 0 || System.Array.IndexOf(Planet.Cells[i].across, j) >= 0;
    public static void Link(int i, int j)
    {
        if (i == j || !Adjacent2(i, j) || !Links.Add(Key(i, j))) return;
        if (!partners.TryGetValue(i, out var a)) partners[i] = a = new List<int>(6); a.Add(j);
        if (!partners.TryGetValue(j, out var b)) partners[j] = b = new List<int>(6); b.Add(i);
    }
    // Bij een klik: verbinden met de weg- en padvakken ernaast, maar nooit zo dat er een driehoekje ontstaat (drie vakken die
    // elk aan elkaar vastzitten geven een lusje in plaats van een weg). Eerst de losse eindjes, dan de rest.
    public static List<int> ClickLinks(int i)
    {
        var got = new List<int>(); foreach (int j in Partners(i)) if (IsWalk(j)) got.Add(j);
        for (int pass = 0; pass < 2; pass++)
            foreach (int j in Planet.Cells[i].nb)
            {
                if (!IsWalk(j) || got.Contains(j) || (LinkCount(j) <= 1) != (pass == 0)) continue;
                bool loop = false; foreach (int p in got) if (Linked(p, j)) loop = true;
                if (!loop) got.Add(j);
            }
        return got;
    }
    public static void Unlink(int i)
    {
        if (!partners.TryGetValue(i, out var a)) return;
        foreach (int j in a) { Links.Remove(Key(i, j)); TunnelLinks.Remove(Key(i, j)); LowerLinks.Remove(Key(i, j)); OneWay.Remove(DKey(i, j)); OneWay.Remove(DKey(j, i)); if (partners.TryGetValue(j, out var b)) b.Remove(i); if (!IsNeighbour(i, j)) ClearHalves(i, j); }
        partners.Remove(i);
    }
    static bool IsNeighbour(int i, int j) => System.Array.IndexOf(Planet.Cells[i].nb, j) >= 0;
    // de twee vakken tussen i en j (afstand 2): de gemeenschappelijke buren
    public static List<int> Between(int i, int j) { var l = new List<int>(2); foreach (int x in Planet.Cells[i].nb) if (IsNeighbour(j, x)) l.Add(x); return l; }
    // Het bezette deel van een half vak: de kant naar de weg toe, tot de middenlijn van het vak (de weg loopt langs de rand tussen twee halve vakken)
    public static List<Vector3> HalfDirs(int i, Vector3 n, bool free = false)   // free: de andere helft (de vrije kant, van de weg af)
    {
        var c = Planet.Cells[i]; float sc = Vector3.Dot(c.dir, n), sg = sc < 0f ? -1f : 1f, lim = Mathf.Abs(sc), fs = free ? -1f : 1f; var o = new List<Vector3>(); int m = c.corners.Length;
        for (int k = 0; k < m; k++)
        {
            Vector3 a = c.corners[k], b = c.corners[(k + 1) % m]; float fa = fs * (sg * Vector3.Dot(a, n) - lim), fb = fs * (sg * Vector3.Dot(b, n) - lim);
            if (fa <= 0f) o.Add(a);
            if ((fa <= 0f) != (fb <= 0f)) o.Add(Vector3.Lerp(a, b, fa / (fa - fb)).normalized);
        }
        return o;
    }
    // het vlak van de weg die over dit halve vak loopt (normaal), uit de verbonden vakken ernaast
    public static bool HalfNormal(int i, out Vector3 n)
    {
        n = Vector3.zero;
        foreach (int a in Planet.Cells[i].nb) { if (!IsWalk(a)) continue; foreach (int b in Partners(a)) { if (b == a || IsNeighbour(a, b) || !Between(a, b).Contains(i)) continue; n = Vector3.Cross(Planet.Cells[a].dir, Planet.Cells[b].dir).normalized; return true; } }
        return false;
    }
    // een half vak: onderste "blok" is "wegdeel" (de weg bezet de ene helft); daarboven mogen blokken staan, op de vrije helft
    public static bool IsHalf(int i) => Cells.TryGetValue(i, out var l) && l.Count >= 1 && l[0] == "wegdeel";
    static void ClearHalves(int i, int j)
    {
        foreach (int h in Between(i, j)) if (IsHalf(h) && !HalfUsed(h)) { var l = Cells[h]; if (l.Count > 1) l.RemoveAt(0); else Cells.Remove(h); Touch(h); }   // met bebouwing erop: wordt een gewoon vak; loopt er nog een andere schuine weg over, dan blijft het half
    }
    // loopt er nog een schuine verbinding over dit halve vak? (anders is het "wegdeel" een overblijfsel)
    static bool HalfUsed(int h)
    {
        foreach (int a in Planet.Cells[h].nb) foreach (int b in Partners(a)) if (b != a && Linked(a, b) && !IsNeighbour(a, b) && Between(a, b).Contains(h)) return true;
        return false;
    }
    // de gum of rechts klikken op een leeg half vak: de schuine weg (of het pad) die eroverheen loopt gaat weg, de vakken ernaast blijven
    static bool ClearOver(int h)
    {
        var over = new List<long>();
        foreach (int a in Planet.Cells[h].nb) foreach (int b in Partners(a)) if (a < b && Linked(a, b) && !IsNeighbour(a, b) && Between(a, b).Contains(h)) over.Add(Key(a, b));
        foreach (long k in over)
        {
            int a = (int)(k >> 20), b = (int)(k & 0xFFFFF);
            Links.Remove(k); TunnelLinks.Remove(k); LowerLinks.Remove(k); OneWay.Remove(DKey(a, b)); OneWay.Remove(DKey(b, a));
            partners[a].Remove(b); partners[b].Remove(a); ClearHalves(a, b); Touch(a); Touch(b);
        }
        if (IsHalf(h) && Cells[h].Count <= 1) Cells.Remove(h);   // nog een overblijfsel: ook weg
        Touch(h); return true;
    }
    static void MarkHalf(int h)
    {
        if (!Cells.TryGetValue(h, out var l) || l.Count == 0) { Cells[h] = new List<string> { "wegdeel" }; Touch(h); return; }
        string b = l[0]; if (Buildable(b)) { l.Insert(0, "wegdeel"); Touch(h); }   // bestaande bebouwing schuift naar de vrije helft
    }
    static bool Buildable(string t) => t == "woning" || t == "kantoor" || t == "park" || t == "dak" || t == "blok" || t == "winkel" || t == "plein" || t == "toren";
    public static bool LinkedRoad(int i, int j) => IsRoad(j) && Linked(i, j);
    public static bool LinkedWalk(int i, int j) => IsWalk(j) && Linked(i, j);
    public static int LinkCount(int i) { int n = 0; foreach (int j in Partners(i)) if (IsWalk(j)) n++; return n; }
    // een heel vak leegmaken (rechtermuisknop)
    // De gum over een brug. Een gumstreek die langs de weg of het pad onder een brug loopt, haalt die onderdoorgang weg en laat
    // de brug staan als gewoon stuk van de bovenste weg (het terrein blijft). Daarvoor onthoudt de streek bij elk gewist vak welke
    // brug erbij hoort (gumSkip); komt de streek daarna op die brug, dan gaat alleen het eronder weg. Een losse klik op een brug
    // (from < 0) haalt de brug zelf weg (Clear).
    static readonly HashSet<int> gumSkip = new HashSet<int>();
    public static bool ClearFrom(int i, int from)
    {
        if (from < 0) gumSkip.Clear();
        if (gumSkip.Contains(i) && Under.TryGetValue(i, out var u) && (u == "weg" || u == "pad"))
        {
            if (u == "weg") { var ends = new List<long>(); foreach (long k in TunnelLinks) { int a = (int)(k >> 20), b = (int)(k & 0xFFFFF); if (MidCell(a, b) == i) ends.Add(k); } foreach (long k in ends) { int a = (int)(k >> 20), b = (int)(k & 0xFFFFF); Links.Remove(k); TunnelLinks.Remove(k); partners[a].Remove(b); partners[b].Remove(a); Touch(a); Touch(b); } }
            else foreach (int j in new List<int>(Partners(i))) if (IsPath(j)) { Links.Remove(Key(i, j)); partners[i].Remove(j); if (partners.TryGetValue(j, out var b)) b.Remove(i); Touch(j); }
            Under.Remove(i); Low.Remove(i); Touch(i); return true;
        }
        // dit vak hoort bij wat onder een brug door gaat: die brug straks overslaan
        foreach (long k in TunnelLinks) { int a = (int)(k >> 20), b = (int)(k & 0xFFFFF); if (a == i || b == i) { int m = MidCell(a, b); if (m >= 0) gumSkip.Add(m); } }
        if (IsPath(i)) foreach (int j in Partners(i)) if (PadUnder(j)) gumSkip.Add(j);
        return Clear(i);
    }
    public static bool Clear(int i)
    {
        if (!Cells.TryGetValue(i, out var l)) return false;
        if (IsHalf(i)) { if (l.Count <= 1) return ClearOver(i); l.RemoveRange(1, l.Count - 1); Touch(i); return true; }   // eerst wat op de vrije helft staat, daarna de schuine weg erover
        if (Under.TryGetValue(i, out var under)) { RestoreUnder(i, under); return true; }   // een brug: het water of de kuil komt terug
        if (IsRot(i)) ClearRoundabout(i);
        bool walk = IsWalk(i); Cells.Remove(i); Style.Remove(i); Bike.Remove(i); Unlink(i); if (walk) RestoreLevel(i); Touch(i); return true;
    }
    public static float StackHeight(int i) { float h = 0f; if (Cells.TryGetValue(i, out var l)) foreach (var t in l) h += Blocks.Height(t); return h; }
    public static int RoadCount() { int n = 0; foreach (var kv in Cells) if (kv.Value.Count > 0 && kv.Value[0] == "weg") n++; return n; }

    public static Dictionary<int, List<string>> Clone(Dictionary<int, List<string>> src)
    {
        var d = new Dictionary<int, List<string>>(); foreach (var kv in src) d[kv.Key] = new List<string>(kv.Value); return d;
    }

    // plaatsen; geeft een melding terug als het niet kan (anders null)
    public static string Place(int i, string type, bool autoLink = true)
    {
        if (!Cells.TryGetValue(i, out var l)) { l = new List<string>(); Cells[i] = l; }
        if ((type == "weg" || type == "pad") && l.Count == 1 && l[0] == "park") l.Clear();   // een weg of pad kan altijd door een bos: de bomen wijken
        if (type == "weg")
        {
            if (l.Count == 1 && l[0] == "wegdeel") l.Clear();
            if (l.Count == 1 && (l[0] == "meer" || l[0] == "water" || l[0] == "kuil")) { Under[i] = l[0]; l[0] = "weg"; }   // een brug over water of een uitgraving
            if (l.Count == 1 && l[0] == "pad")   // een weg over een pad: gelijkvloers een oversteek (het pad sluit aan op de stoep); onderdoor met ongelijkvloers, of als de weg op een ander niveau ligt dan het pad
            { if (DragUnder || DragLevel != WalkLevel(i)) MakeUnderpass(i, l); else { l[0] = "weg"; Bike.Remove(i); } }
            if (l.Count > 0 && !(l.Count == 1 && l[0] == "weg")) return "Eerst het vak leegmaken (rechtermuisknop) voor een weg";
            if (l.Count == 0) l.Add("weg");
            SetStyle(i); if (autoLink) { foreach (int j in ClickLinks(i)) Link(i, j); CarvePaths(); }
            LinkRot(i);   // een weg op de ring van een rotonde sluit erop aan
            Touch(i); return null;
        }
        if (type == "rotonde") { if (l.Count == 0) Cells.Remove(i); return PlaceRoundabout(i); }
        if (RotOf(i) >= 0 && !IsRot(i) && type != "pad") { if (l.Count == 0) Cells.Remove(i); return "Hier ligt een rotonde: op de ring kun je niet bouwen"; }
        if (type == "pad" && l.Count == 1 && l[0] == "wegdeel") l.Clear();   // een pad over een schuine weg: het halve wegvak wordt een padvak (de brug komt op de rand)
        if (type == "pad" && l.Count == 1 && l[0] == "weg" && (Underpass(i) || ((DragUnder || DragLevel < 0) && WalkLevel(i) >= 0 && DigUnder(i))))   // een pad onder een weg door: met ongelijkvloers of een lager niveau (niet onder een verdiepte weg: daar kruist het pad op de hoogte van de weg)
        { if (autoLink) { foreach (int j in Planet.Cells[i].nb) if (IsPath(j)) Link(i, j); DigTrench(i); } Touch(i); return null; }
        if (type == "pad" && l.Count == 1 && l[0] == "weg")   // gelijkvloers: het pad sluit aan op de weg (een oversteek)
        { if (autoLink) foreach (int j in ClickLinks(i)) if (IsPath(j)) Link(i, j); Touch(i); return null; }
        if (type == "pad" && l.Count == 0) { l.Add("pad"); LinkRot(i); if (DragBike) Bike.Add(i); if (autoLink) { foreach (int j in ClickLinks(i)) Link(i, j); DigCrossings(); } Touch(i); return null; }
        if (type == "pad" && l.Count == 1 && l[0] == "pad") { if (DragBike && Bike.Add(i)) Touch(i); if (autoLink) foreach (int j in ClickLinks(i)) Link(i, j); Touch(i); return null; }
        if (l.Count > 0 && l[0] == "weg") return "Op een weg kun je niet bouwen; haal de weg eerst weg (rechtermuisknop)";
        if (l.Count > 0 && l[0] == "wegdeel" && !Buildable(type)) return "Hier loopt een weg overheen: op de vrije helft passen alleen gebouwen en parken";
        if (l.Count >= 8) return "Hoger kan niet";
        if (l.Count > 0 && (l[l.Count - 1] == "toren" || l[l.Count - 1] == "molen") && type != "water" && type != "meer") return "Op een " + l[l.Count - 1] + " kun je niets meer zetten";
        if (l.Count > 0 && NoStackOn(l[l.Count - 1]) is string why && type != "water" && type != "meer") return why;
        if ((type == "water" || type == "meer") && l.Count > 0) l.Clear();
        if (l.Count > 0 && (l[l.Count - 1] == "water" || l[l.Count - 1] == "meer")) l.Clear();
        l.Add(type); Touch(i); return null;
    }

    // Een gesleepte lijn: de vakken worden weg (of pad) en alleen onderling verbonden, in volgorde
    public static int PlaceLine(List<int> line, string type)
    {
        int ok = 0; LastNote = null;
        // ongelijkvloers: een bestaand wegvak midden in de lijn wordt een brug en de nieuwe weg gaat eronderdoor, tenzij de lijn daar
        // over die weg heen loopt (het vak ervoor of erna hoort bij dezelfde weg): dan wordt het een kruispunt, met een melding
        var under = new List<int>();
        if (type == "weg" && DragUnder)
            for (int k = 1; k + 1 < line.Count; k++)
            {
                int c = line[k], p = line[k - 1], q = line[k + 1]; if (!IsRoad(c)) continue;
                if (DragLevel != 0 && G(c) - ((PadGround(c) ?? G(c)) + DragLevel * StepH) < LevelH - 0.1f) continue;   // de weg die je kruist ligt niet een niveau hoger: geen tunnel, SetLevel maakt er een kruispunt van
                if (TunnelLinks.Contains(Key(p, q)) && !under.Contains(p)) { under.Add(c); continue; }   // daar gaat deze weg al onderdoor (opnieuw of terug gesleept)
                if ((Linked(p, c) || !IsRoad(p)) && (Linked(c, q) || !IsRoad(q)) && (Linked(p, c) || Linked(c, q))) continue;   // dit stuk ligt er al precies zo: niets te doen, geen melding
                if (!Under.ContainsKey(c) && StraightThrough(c) && !Linked(p, c) && !Linked(q, c) && !under.Contains(p) && Adjacent2(p, c) && Adjacent2(c, q) && MidCell(p, q) == c) under.Add(c);
                else LastNote = "Hier kon de weg niet onderdoor (hij loopt daar langs of over een bestaande weg of brug): het werd een kruispunt";
            }
        for (int k = 0; k < line.Count; k++) { if (under.Contains(line[k])) { ok++; continue; } if (Place(line[k], type, false) == null) ok++; }
        var cross = new List<int>();
        for (int k = 1; k < line.Count; k++)
        {
            int a = line[k - 1], b = line[k]; if (!IsWalk(a) || !IsWalk(b) || under.Contains(a) || under.Contains(b)) continue;
            if (type == "weg" && !(DragUnder && (under.Contains(a) || under.Contains(b))))   // schuin kruisen met een andere weg: met ongelijkvloers eronderdoor, anders een kruispunt
            {
                var bt = Between(a, b);
                if (bt.Count == 2 && IsRoad(bt[0]) && IsRoad(bt[1]) && Linked(bt[0], bt[1]))
                {
                    float newH = (PadGround(a) ?? G(a)) + DragLevel * StepH, oldH = Mathf.Max(G(bt[0]), G(bt[1]));
                    if (DragUnder) LowerLinks.Add(Key(a, b));
                    else if (newH - oldH >= LevelH - 0.1f) LowerLinks.Add(Key(bt[0], bt[1]));   // de bestaande weg ligt een niveau lager: hij gaat eronderdoor (een brug), niet verleggen
                    else if (IsNeighbour(a, b))   // een schuine weg springt over de rand a|b: die weg gaat nu door vak a (het kruispunt)
                    {
                        int c1 = bt[0], c2 = bt[1], f = Flow(c1, c2); Links.Remove(Key(c1, c2)); OneWay.Remove(DKey(c1, c2)); OneWay.Remove(DKey(c2, c1)); partners[c1].Remove(c2); partners[c2].Remove(c1); LowerLinks.Remove(Key(c1, c2));
                        Link(c1, a); Link(a, c2); if (f > 0) { OneWay.Add(DKey(c1, a)); OneWay.Add(DKey(a, c2)); } else if (f < 0) { OneWay.Add(DKey(c2, a)); OneWay.Add(DKey(a, c1)); }
                        Touch(c1); Touch(c2);
                    }
                    else   // deze lijn springt zelf over de rand tussen twee wegvakken: door het eerste daarvan (het kruispunt)
                    {
                        // welk van de twee: het vak waar deze weg al doorheen gaat (opnieuw of terug gesleept), anders vast het kleinste nummer
                        int h = Linked(a, bt[0]) && Linked(bt[0], b) ? bt[0] : Linked(a, bt[1]) && Linked(bt[1], b) ? bt[1] : Mathf.Min(bt[0], bt[1]);
                        Link(a, h); Link(h, b); if (DragOneWay) { OneWay.Add(DKey(a, h)); OneWay.Add(DKey(h, b)); }
                        continue;
                    }
                }
            }
            // een schuin pad springt over de rand tussen twee verbonden wegvakken: gelijkvloers sluit het pad aan beide kanten aan op
            // het eerste wegvak (een oversteek); met ongelijkvloers of een lager niveau gaat het eronderdoor (EdgeCrossings)
            if (type == "pad" && !DragUnder && DragLevel >= 0 && !IsNeighbour(a, b))
            {
                var br = Between(a, b);
                if (br.Count == 2 && IsRoad(br[0]) && IsRoad(br[1]) && Linked(br[0], br[1]))
                {
                    int h = Linked(a, br[0]) && Linked(b, br[0]) ? br[0] : Linked(a, br[1]) && Linked(b, br[1]) ? br[1] : Mathf.Min(br[0], br[1]);   // net als bij een weg: steeds hetzelfde vak
                    Link(a, h); Link(b, h); continue;
                }
            }
            // een schuine weg springt over de rand tussen twee verbonden padvakken: gelijkvloers blijft de weg recht en sluit het pad aan
            // beide kanten aan op wegvak a (een oversteek). Met ongelijkvloers, of op een ander niveau, gaat het pad eronderdoor (EdgeCrossings).
            if (type == "weg" && !DragUnder && !IsNeighbour(a, b))
            {
                var bp = Between(a, b);
                if (bp.Count == 2 && IsPath(bp[0]) && IsPath(bp[1]) && Linked(bp[0], bp[1]) && WalkLevel(bp[0]) == DragLevel)
                {
                    int p0 = bp[0], p1 = bp[1]; Links.Remove(Key(p0, p1)); partners[p0].Remove(p1); partners[p1].Remove(p0);
                    Link(p0, a); Link(p1, a); cross.Add(p0); cross.Add(p1);
                }
            }
            Link(a, b);
            if (IsRoad(a) && IsRoad(b))   // eenrichting in de sleeprichting, of weer tweerichting
            {
                OneWay.Remove(DKey(a, b)); OneWay.Remove(DKey(b, a)); if (DragOneWay) OneWay.Add(DKey(a, b));
            }
            if (!IsNeighbour(a, b)) foreach (int h in Between(a, b)) MarkHalf(h);
        }
        foreach (int c in under)   // de nieuwe weg onder het kruisvak door: van het vak ervoor naar het vak erna
        {
            int k = line.IndexOf(c), p = line[k - 1], q = line[k + 1]; LinkTunnel(p, q);
            Under[c] = "weg"; Low[c] = Mathf.Min(G(p), Mathf.Min(G(q), G(c) - UnderClear)); Touch(p); Touch(q);
        }
        foreach (int i in line) Touch(i); foreach (int i in cross) Touch(i);
        CarvePaths();   // de geulen van de onderdoorgangen in de lijn en van schuine kruisingen, één keer voor de hele lijn (rekent over de hele planeet)
        if (line.Count >= 2) { JoinEnd(line[0], line[1], type); JoinEnd(line[line.Count - 1], line[line.Count - 2], type); }
        return ok;
    }
    // Begint of eindigt een gesleepte lijn precies in het verlengde van een doodlopende weg (of pad) van dezelfde soort, dan horen ze
    // bij elkaar: verbinden (anders liggen er twee losse eindes met een kier ertussen). Op een ander niveau wordt het daar een helling.
    static void JoinEnd(int e, int inner, string type)
    {
        if (!IsWalk(e)) return;
        foreach (int x in Planet.Cells[e].nb)
        {
            if (x == inner || MidCell(x, inner) != e || Linked(e, x) || Underpass(x)) continue;   // het vak dat de lijn zou vervolgen
            bool same = type == "weg" ? IsRoad(x) : IsPath(x); if (!same) continue;
            int n = 0; foreach (int j in Partners(x)) if (IsWalk(j)) n++;
            if (n <= 1) { Link(e, x); Touch(e); Touch(x); }   // een doodlopend eind (of los vak): aansluiten
        }
    }

    public static string LastNote;   // een melding over de laatst gelegde lijn (of null)

    public static bool RemoveTop(int i)
    {
        if (!Cells.TryGetValue(i, out var l) || l.Count == 0) return false;
        if (IsHalf(i) && l.Count <= 1) return ClearOver(i);
        if (Under.TryGetValue(i, out var under)) { RestoreUnder(i, under); return true; }
        bool walk = IsWalk(i); l.RemoveAt(l.Count - 1); if (l.Count == 0) { Cells.Remove(i); Style.Remove(i); Bike.Remove(i); Unlink(i); if (walk) RestoreLevel(i); } Touch(i); return true;
    }

    public static void Touch(int i) { dirty.Add(i / ChunkN); foreach (int j in Planet.Cells[i].nb) dirty.Add(j / ChunkN); }
    // Alles weg (lege bouwgrond): de gegevens én alle getekende stukken, zodat er nooit iets van de oude stad blijft staan
    public static void ClearAll()
    {
        Cells = new Dictionary<int, List<string>>(); Terr.Clear(); CornerOfs.Clear(); CenterOfs.Clear(); ClearLinks();
        if (root != null) foreach (Transform t in root) UnityEngine.Object.Destroy(t.gameObject);
        chunks.Clear();
        if (roadRoot != null) foreach (Transform t in roadRoot) UnityEngine.Object.Destroy(t.gameObject);
        roadPieces.Clear();
        TouchAll(); Rebuild(); RebuildGrid();
    }
    public static void TouchAll() { for (int c = 0; c * ChunkN < Planet.Cells.Length; c++) dirty.Add(c); }

    public static int Slot = 1;   // drie opslagslots: planeet1.json, planeet2.json, planeet3.json
    // Een stad die nog de ongewijzigde startstad is, wordt bij het laden opnieuw gebouwd als de startstad in de code veranderd is
    // (StartTownVersion omhoog). Zodra je zelf iets bouwt (UserEdit) blijft je stad zoals hij is.
    public const int StartTownVersion = 2;
    public static int StartVer, StartCell = -1; static bool startFresh;
    public static void UserEdit() { if (startFresh) startFresh = false; else StartVer = 0; }
    static string FilePath => Path.Combine(Application.persistentDataPath, "planeet" + Slot + ".json");
    public static bool SlotExists(int n) => File.Exists(Path.Combine(Application.persistentDataPath, "planeet" + n + ".json"));

    public static void Save()
    {
        try { File.WriteAllText(FilePath, Snapshot()); }
        catch (Exception e) { Debug.LogWarning("Opslaan mislukt: " + e.Message); }
    }
    // de hele stad als tekst (zoals het opslagbestand): ook verbindingen, bruggen en de terreinhoogtes van voor een geul of niveau,
    // zodat ongedaan maken alles terugzet (Restore)
    public static string Snapshot()
    {
        {
            var sd = new PSaveData { m = Planet.M, r = Planet.R };
            foreach (var kv in Cells) if (kv.Value.Count > 0) sd.cells.Add(new PCellSave { i = kv.Key, items = kv.Value.ToArray() });
            foreach (long k in Links) { sd.links.Add((int)(k >> 20)); sd.links.Add((int)(k & 0xFFFFF)); }
            foreach (var kv in Style) if (IsRoad(kv.Key)) { sd.style.Add(kv.Key); sd.style.Add(kv.Value); }
            foreach (var kv in Under) { sd.under.Add(kv.Key); sd.under.Add(kv.Value == "meer" ? 1 : kv.Value == "water" ? 2 : kv.Value == "pad" ? 4 : kv.Value == "weg" ? 5 : 3); }
            foreach (long k in TunnelLinks) { sd.tunnel.Add((int)(k >> 20)); sd.tunnel.Add((int)(k & 0xFFFFF)); }
            foreach (long k in LowerLinks) { sd.lower.Add((int)(k >> 20)); sd.lower.Add((int)(k & 0xFFFFF)); }
            foreach (var kv in preCarve) { sd.precarve.Add(kv.Key); sd.precarve.Add(Mathf.RoundToInt(kv.Value * 100f)); }
            foreach (var kv in preLevel) { sd.prelevel.Add(kv.Key); sd.prelevel.Add(Mathf.RoundToInt(kv.Value * 100f)); }
            foreach (var kv in Low) { sd.low.Add(kv.Key); sd.low.Add(Mathf.RoundToInt(kv.Value * 100f)); }
            foreach (long k in OneWay) { sd.oneway.Add((int)(k >> 20)); sd.oneway.Add((int)(k & 0xFFFFF)); }
            foreach (var kv in Terr) { sd.terr.Add(kv.Key); sd.terr.Add(Mathf.RoundToInt(kv.Value * 100f)); }
            foreach (var kv in CornerOfs) { sd.corner.Add((int)(kv.Key >> 40)); sd.corner.Add((int)((kv.Key >> 20) & 0xFFFFF)); sd.corner.Add((int)(kv.Key & 0xFFFFF)); sd.corner.Add(Mathf.RoundToInt(kv.Value * 100f)); }
            foreach (var kv in CenterOfs) { sd.center.Add(kv.Key); sd.center.Add(Mathf.RoundToInt(kv.Value * 100f)); }
            foreach (int b in Bike) if (Cells.ContainsKey(b)) sd.bike.Add(b);
            foreach (int c in Rot) if (IsRoad(c)) sd.rot.Add(c);
            sd.startVer = StartVer; sd.startCell = StartCell;
            return JsonUtility.ToJson(sd);
        }
    }

    public static bool Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return false;
            if (!Restore(File.ReadAllText(FilePath))) return false;
            if (StartVer > 0 && StartVer < StartTownVersion) { int s = StartCell >= 0 && StartCell < Planet.Cells.Length ? StartCell : Planet.North; ClearAll(); StartTown(s); Save(); }   // de nieuwe startstad
            return true;
        }
        catch (Exception e) { Debug.LogWarning("Laden mislukt: " + e.Message); return false; }
    }
    public static bool Restore(string json)
    {
        try
        {
            var sd = JsonUtility.FromJson<PSaveData>(json); if (sd == null || sd.m != Planet.M) return false;
            StartVer = sd.startVer; StartCell = sd.startCell; startFresh = false;
            Cells = new Dictionary<int, List<string>>();
            foreach (var c in sd.cells) if (c.items != null && c.items.Length > 0 && c.i >= 0 && c.i < Planet.Cells.Length) Cells[c.i] = new List<string>(c.items);
            ClearLinks();
            if (sd.links != null && sd.links.Count > 0) { for (int k = 0; k + 1 < sd.links.Count; k += 2) Link(sd.links[k], sd.links[k + 1]); }
            else foreach (var kv in Cells) if (IsWalk(kv.Key)) foreach (int j in Planet.Cells[kv.Key].nb) if (IsWalk(j)) Link(kv.Key, j);   // oude opslag: alles naast elkaar verbonden
            if (sd.style != null) for (int k = 0; k + 1 < sd.style.Count; k += 2) Style[sd.style[k]] = (byte)sd.style[k + 1];
            if (sd.bike != null) foreach (int b in sd.bike) Bike.Add(b);
            if (sd.rot != null) foreach (int c in sd.rot) Rot.Add(c);
            if (sd.under != null) for (int k = 0; k + 1 < sd.under.Count; k += 2) Under[sd.under[k]] = sd.under[k + 1] == 1 ? "meer" : sd.under[k + 1] == 2 ? "water" : sd.under[k + 1] == 4 ? "pad" : sd.under[k + 1] == 5 ? "weg" : "kuil";
            if (sd.low != null) for (int k = 0; k + 1 < sd.low.Count; k += 2) Low[sd.low[k]] = sd.low[k + 1] / 100f;
            if (sd.tunnel != null) for (int k = 0; k + 1 < sd.tunnel.Count; k += 2) LinkTunnel(sd.tunnel[k], sd.tunnel[k + 1]);
            if (sd.lower != null) for (int k = 0; k + 1 < sd.lower.Count; k += 2) LowerLinks.Add(Key(sd.lower[k], sd.lower[k + 1]));
            if (sd.precarve != null) for (int k = 0; k + 1 < sd.precarve.Count; k += 2) preCarve[sd.precarve[k]] = sd.precarve[k + 1] / 100f;
            if (sd.prelevel != null) for (int k = 0; k + 1 < sd.prelevel.Count; k += 2) preLevel[sd.prelevel[k]] = sd.prelevel[k + 1] / 100f;
            if (sd.oneway != null) for (int k = 0; k + 1 < sd.oneway.Count; k += 2) OneWay.Add(DKey(sd.oneway[k], sd.oneway[k + 1]));
            Terr.Clear(); CornerOfs.Clear(); CenterOfs.Clear();
            if (sd.center != null) for (int k = 0; k + 1 < sd.center.Count; k += 2) CenterOfs[sd.center[k]] = sd.center[k + 1] / 100f;
            if (sd.corner != null) for (int k = 0; k + 3 < sd.corner.Count; k += 4) CornerOfs[((long)sd.corner[k] << 40) | ((long)sd.corner[k + 1] << 20) | (uint)sd.corner[k + 2]] = sd.corner[k + 3] / 100f;
            if (sd.terr != null) for (int k = 0; k + 1 < sd.terr.Count; k += 2) if (sd.terr[k] >= 0 && sd.terr[k] < Planet.Cells.Length && sd.terr[k + 1] != 0) Terr[sd.terr[k]] = Mathf.Clamp(sd.terr[k + 1] / 100f, TerrLow, TerrTop);
            // oude heuvelblokken onderaan een stapel worden terreinhoogte (3 m elk), net als terrMigrate in de webversie
            foreach (int k in new List<int>(Cells.Keys))
            {
                var l = Cells[k]; int n = 0; while (n < l.Count && l[n] == "heuvel") n++;
                if (n == 0) continue;
                Terr[k] = Mathf.Min(TerrTop, G(k) + n * 3f); l.RemoveRange(0, n); if (l.Count == 0) Cells.Remove(k);
            }
            FixCrossings();
            TouchAll(); RebuildGrid(); return true;
        }
        catch (Exception e) { Debug.LogWarning("Laden mislukt: " + e.Message); return false; }
    }

    // Een rechte lijn van vakken van a naar b: de richting wordt op een veelvoud van 30° gelegd (zoals in de browser) en de lijn
    // volgt de grootcirkel; elke stap kiest de buur die het dichtst bij die cirkel blijft. In de 6 buurrichtingen is dat een
    // kaarsrechte rij, in de 6 tussenrichtingen een lichte trap.
    public static List<int> LinePlan(int a, int b)
    {
        var A = Planet.Cells[a]; Vector3 da = A.dir, db = Planet.Cells[b].dir;
        Vector3 t = db - da; t -= da * Vector3.Dot(t, da); if (t.sqrMagnitude < 1e-8f) return null; t.Normalize();
        // de 12 richtingen: naar de buren (6) en daar precies tussenin (6); op een vijfhoek 5 + 5
        var dirs = new List<Vector3>();
        foreach (int j in A.nb) { Vector3 d = Planet.Cells[j].dir - da; d -= da * Vector3.Dot(d, da); dirs.Add(d.normalized); }
        foreach (int j in A.across) { if (j < 0) continue; Vector3 d = Planet.Cells[j].dir - da; d -= da * Vector3.Dot(d, da); dirs.Add(d.normalized); }   // door de hoek: over twee halve vakken
        Vector3 ray = dirs[0]; float bestDot = -2f; foreach (var d in dirs) { float v = Vector3.Dot(d, t); if (v > bestDot) { bestDot = v; ray = d; } }
        Vector3 n = Vector3.Cross(da, ray).normalized;
        float total = Mathf.Acos(Mathf.Clamp(Vector3.Dot(da, db), -1f, 1f));
        var plan = new List<int> { a }; int cur = a;
        for (int step = 0; step < 120; step++)
        {
            Vector3 dc = Planet.Cells[cur].dir, fwd = Vector3.Cross(n, dc).normalized; int best = -1; float bs = -1e9f;
            var cc = Planet.Cells[cur]; var cand = new List<int>(cc.nb); foreach (int x in cc.across) if (x >= 0) cand.Add(x);
            foreach (int j in cand)
            {
                Vector3 dj = Planet.Cells[j].dir; float prog = Vector3.Dot(dj - dc, fwd); if (prog <= 1e-6f) continue;
                float off = Mathf.Abs(Vector3.Dot(dj, n)) * Planet.R;   // afstand tot de grootcirkel in meters
                float sc = -off * 4f + prog * 0.5f;   // vooral: op de lijn blijven
                if (off < 9f && Linked(cur, j) && IsWalk(cur) && IsWalk(j)) sc += 24f;   // ligt er al een weg of pad langs de lijn: die volgen (terugslepen geeft dezelfde vakken, geen tweede weg ernaast)
                if (sc > bs) { bs = sc; best = j; }
            }
            if (best < 0) break;
            float along = Mathf.Acos(Mathf.Clamp(Vector3.Dot(da, Planet.Cells[best].dir), -1f, 1f));
            if (along > total + 0.4f * 15.6f / Planet.R) break;
            plan.Add(best); cur = best; if (cur == b) break;
        }
        return plan;
    }


    // Een voorbeeldstadje rond vak s: een ringweg, twee hoofdwegen erdoorheen, huizen en kantoren langs de wegen,
    // parken, een meertje en een paar paden. Vast bepaald (zelfde stad bij elke keer). De ene hoofdweg ligt een niveau lager in
    // een bak en gaat ongelijkvloers onder de ringweg en de andere hoofdweg door (drie bruggen, zoals het model overpass_path);
    // aan beide einden helt hij in twee vakken terug naar het maaiveld.
    // rich: met winkels, pleinen en torens erbij (voor de volle planeet, met een eigen seed per stadje)
    public static string Sample(int s, int seed = 7, bool rich = false)   // null: geplaatst; anders de reden waarom niet
    {
        var S = Planet.Cells[s]; var rnd = new System.Random(seed);
        // afstanden (in vakken) vanaf s
        var dist = new Dictionary<int, int> { { s, 0 } }; var q = new Queue<int>(); q.Enqueue(s);
        while (q.Count > 0) { int c = q.Dequeue(); if (dist[c] >= 7) continue; foreach (int j in Planet.Cells[c].nb) if (!dist.ContainsKey(j)) { dist[j] = dist[c] + 1; q.Enqueue(j); } }
        // alleen op een lege plek: binnen 7 vakken mag nog niets staan (ook geen pool of startplek)
        foreach (var kv in dist) if (Cells.ContainsKey(kv.Key) || Planet.IsPole(kv.Key) || System.Array.IndexOf(Planet.Starts, kv.Key) >= 0) return "Hier is geen ruimte voor een voorbeeldstad: er staat al iets binnen 7 vakken";
        byte keepStyle = DragStyle; bool keepOne = DragOneWay; DragStyle = 0; DragOneWay = false;   // het voorbeeld heeft gewone tweerichtingswegen
        System.Func<Vector3, int> at = dir => Planet.Nearest(dir * Planet.R);
        System.Func<float, float, int> cellAt = (ang, r) => at((S.dir * Mathf.Cos(r / Planet.R) + (S.east * Mathf.Cos(ang) + S.north * Mathf.Sin(ang)) * Mathf.Sin(r / Planet.R)).normalized);
        // twee hoofdwegen door het midden: in de richting van buur 0 en haaks daarop
        Vector3 n0 = Planet.Cells[S.nb[0]].dir - S.dir; n0 -= S.dir * Vector3.Dot(n0, S.dir); float a0 = Mathf.Atan2(Vector3.Dot(n0, S.north), Vector3.Dot(n0, S.east));
        var cross = LinePlan(cellAt(a0 + Mathf.PI, 6.2f * 15.6f), cellAt(a0, 6.2f * 15.6f)); if (cross != null) PlaceLine(cross, "weg");   // op het maaiveld, door de hoeken van de ring (daar kruispunten)
        // ringweg op afstand 4: de vakken op afstand 4, rondom gesorteerd, als gesloten lijn
        var ring = new List<int>(); foreach (var kv in dist) if (kv.Value == 4) ring.Add(kv.Key);
        ring.Sort((x, y) => Ang(S, x).CompareTo(Ang(S, y)));
        var ringLine = new List<int>(ring); ringLine.Add(ring[0]); PlaceLine(ringLine, "weg");
        // de verdiepte hoofdweg (niveau -1, zoals slepen met Page Down): onder de ring en de andere hoofdweg door
        var sunk = LinePlan(cellAt(a0 + Mathf.PI / 2f + Mathf.PI, 6.2f * 15.6f), cellAt(a0 + Mathf.PI / 2f, 6.2f * 15.6f));   // haaks: kruist de ring op een recht stuk (een brug kan niet in een bocht)
        if (sunk != null && sunk.Count > 6)
        {
            bool du = DragUnder; int dl = DragLevel; DragUnder = true; DragLevel = -2;
            PlaceLine(sunk, "weg"); SetLevel(sunk, -2, "weg");
            DragUnder = du; DragLevel = dl;
            // de hellingen aan de einden; ligt het tweede vak onder een brug (een schuine kruising of een onderdoorgang ernaast), dan blijft
            // dat op volle diepte (een brug heeft altijd dezelfde maat) en helt alleen het laatste vak, half zo diep
            var xs = EdgeCrossings();
            bool UnderBridge(int c)
            {
                foreach (var x in xs) if (x.swap ? (x.u == c || x.v == c) : (x.b1 == c || x.b2 == c)) return true;
                foreach (int j in Planet.Cells[c].nb) if (Underpass(j) && Under.TryGetValue(j, out var u) && u == "weg" && TunnelEnds(j).Contains(c)) return true;
                return false;
            }
            bool keepA = UnderBridge(sunk[1]), keepB = UnderBridge(sunk[sunk.Count - 2]);
            foreach (var (k, lv) in new[] { (0, keepA ? -1 : 0), (sunk.Count - 1, keepB ? -1 : 0), (1, keepA ? -2 : -1), (sunk.Count - 2, keepB ? -2 : -1) })
            {
                int c = sunk[k]; if (!IsRoad(c) || Underpass(c)) continue; LevelTerr(c, (PadGround(c) ?? G(c)) + lv * StepH); Touch(c);
            }
            CarvePaths();   // ook de weglijnen voor de hoogtefunctie (InSlope)
        }
        // bebouwing: langs de wegen huizen (buiten) en kantoren (binnen de ring), parken in de rest, een meertje aan één kant
        float lakeAng = a0 + Mathf.PI / 4f; int lakeC = cellAt(lakeAng, 2.3f * 15.6f);
        foreach (var kv in dist)
        {
            int c = kv.Key, d = kv.Value; if (Cells.ContainsKey(c) || d == 0) continue;
            bool nearRoad = false, nearSunk = InSlope(c); foreach (int j in Planet.Cells[c].nb) if (IsRoad(j)) { nearRoad = true; if (WalkLevel(j) != 0 || Underpass(j)) nearSunk = true; }
            if (nearSunk) { if (rnd.NextDouble() < 0.6) Place(c, "park"); continue; }   // naast de bak: bomen op het talud (geen gebouwen)
            float lakeD = Vector3.Distance(Planet.Cells[c].dir, Planet.Cells[lakeC].dir) * Planet.R;
            if (lakeD < 19f) { Place(c, "meer"); continue; }
            if (d > 6) continue;
            if (nearRoad)
            {
                if (d <= 2 && rich && rnd.NextDouble() < 0.25) Place(c, "toren");
                else if (d <= 2) { int fl = 2 + rnd.Next(0, 2); for (int f = 0; f < fl; f++) Place(c, "kantoor"); }
                else if (d == 3 && rich && rnd.NextDouble() < 0.4) Place(c, "winkel");
                else if (d <= 5 && rnd.NextDouble() < 0.8) { Place(c, "woning"); if (rnd.NextDouble() < 0.3) Place(c, "woning"); }
                else if (rnd.NextDouble() < 0.5) Place(c, "park");
            }
            else if (d <= 2 && rich && rnd.NextDouble() < 0.6) Place(c, "plein");
            else if (d <= 5 && rnd.NextDouble() < 0.55) Place(c, "park");
        }
        // een paar paden van de ring naar binnen door het groen
        for (int k = 0; k < 3; k++)
        {
            float ang = a0 + Mathf.PI / 3f + k * Mathf.PI * 2f / 3f; var pl = LinePlan(cellAt(ang, 3.6f * 15.6f), cellAt(ang, 1.4f * 15.6f));
            if (pl != null)
            {
                var free = new List<int>();
                foreach (int c in pl)
                {
                    bool bySunk = false; foreach (int j in Planet.Cells[c].nb) if (IsRoad(j) && (WalkLevel(j) != 0 || Underpass(j))) bySunk = true;
                    if (bySunk) break;   // niet de bak in: het pad houdt op voor het talud
                    if (!Cells.ContainsKey(c) || IsPath(c) || (Cells[c].Count == 1 && Cells[c][0] == "park")) { if (Cells.ContainsKey(c) && Cells[c][0] == "park") Cells.Remove(c); free.Add(c); }
                }
                if (free.Count >= 2) { PlaceLine(free, "pad"); foreach (int c in free) foreach (int j in Planet.Cells[c].nb) if (IsRoad(j) && WalkLevel(j) == 0) Link(c, j); }
            }
        }
        DragStyle = keepStyle; DragOneWay = keepOne; TouchAll(); return null;
    }
    // loopt het talud van een weg op een niveau door vak i (een hoekpunt of het hart ligt lager of hoger dan het gewone terrein)?
    static bool InSlope(int i)
    {
        var c = Planet.Cells[i]; if (!HasField || !NearSeg(c.dir)) return false;
        if (Mathf.Abs(GroundField(c.dir, G(i)) - G(i)) > 0.05f) return true;
        for (int k = 0; k < c.corners.Length; k++) { float b = CornerBase(i, k); if (Mathf.Abs(GroundField(c.corners[k], b) - b) > 0.05f) return true; }
        return false;
    }
    // één fontein per plein: alleen op het pleinvak met het laagste nummer van een aaneengesloten groep pleinvakken
    static bool IsPlein(int i) => Cells.TryGetValue(i, out var l) && l.Count > 0 && l[0] == "plein";
    static bool FountainHere(int i)
    {
        var seen = new HashSet<int> { i }; var q = new Queue<int>(); q.Enqueue(i);
        while (q.Count > 0 && seen.Count < 200) { int c = q.Dequeue(); if (c < i) return false; foreach (int j in Planet.Cells[c].nb) if (IsPlein(j) && seen.Add(j)) q.Enqueue(j); }
        return true;
    }
    static float Ang(Planet.Cell S, int c) { Vector3 d = Planet.Cells[c].dir - S.dir; return Mathf.Atan2(Vector3.Dot(d, S.north), Vector3.Dot(d, S.east)); }

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
        F(i); F(hcb0); foreach (float x in hb0) F(x); F(FieldK); F(special ? 1f : 0f); F(carved.Count > 0 && cuts.Count > 0 ? 1f : 0f); h ^= cutsHash;
        foreach (var q in segsUse) { V(q.a); V(q.b); F(q.ha); F(q.hb); F(q.hw); F(q.capA ? 1f : 0f); F(q.capB ? 1f : 0f); }
        foreach (var q in flatsUse) { V(q.a); V(q.b); F(q.ha); F(q.hb); F(q.hw); }
        foreach (var q in capsUse) { V(q.c); F(q.h); F(q.hw); }
        return h;
    }
    static void BuildGround(int ch) { try { BuildGround0(ch); } finally { UseAll(); } }   // na het tekenen weer alle wegstukken (UseNear)
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
            Topo();
            for (int i = c0; i < Mathf.Min(n, c0 + ChunkN); i++)
            {
                if (fieldCells.Contains(i)) UseNear(i); else UseAll();
                var c = Planet.Cells[i]; int m = c.corners.Length; bool pent = m == 5;
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
                var mb0 = pole ? PM("ijs") : pent ? pm : start ? sm : ms[Mathf.Clamp(Mathf.RoundToInt(kk * (Steps - 1)), 0, Steps - 1)];
                if (!cut)
                {
                    Vector3 mid = c.dir * (Planet.R + hc);
                    void Emit(Vector3 P0, Vector3 P1, Vector3 P2, float h0, float h1, float h2) => Emit2(P0, P1, P2, h0, h1, h2, false);
                    List<GTri> rec = null;   // de driehoekjes van een vak langs een weg op een niveau, om te onthouden (groundCache)
                    void Emit2(Vector3 P0, Vector3 P1, Vector3 P2, float h0, float h1, float h2, bool steep)   // steep: op de schuine kant van een weg (gras, of aarde als het steiler is dan 40°)
                    {
                        string tag = null; float up = Mathf.Abs(Vector3.Dot(Vector3.Cross(P1 - P0, P2 - P0).normalized, c.dir));
                        if (steep) { if (up < SteepUp) tag = "aarde"; }   // een talud is gras, alleen waar het steiler is dan 40° kale aarde
                        else if (!special && (Mathf.Max(h0, Mathf.Max(h1, h2)) > 0.03f || Mathf.Min(h0, Mathf.Min(h1, h2)) < -0.03f))   // verhoogd: kleur naar de hoogte, aarde waar het steiler is dan zo'n 45°
                            tag = up < 0.72f ? "aarde" : "t" + Mathf.Clamp(Mathf.FloorToInt((h0 + h1 + h2) / 9f), 0, 29);
                        if (!steep) tag = NatureTag(i, tag, h0, h1, h2, up);   // rots, sneeuw, strand en zeebodem (PlanetNature.cs)
                        (tag == null ? mb0 : TB(tag)).Tri(P0, P1, P2, c.dir); rec?.Add(new GTri { tag = tag, a = P0, b = P1, c = P2 });
                    }
                    // bij een geul of dijk: het maaiveld zonder geul als vlak, de geul of dijk komt er precies in (ExactGround)
                    if (fieldCells.Contains(i) && !plat && NearSeg(c.dir, c.radius))   // ook als een weg alleen de rand van het vak raakt (een schuine onderdoorgang)
                    {
                        var hb0 = new float[m]; float hcb0 = 0f; for (int k = 0; k < m; k++) { hb0[k] = CornerBase(i, k); hcb0 += hb0[k]; } hcb0 = (levelled.Contains(i) ? hcb0 / m : g) + CenterOfs1(i);   // het gewone terrein, zonder de weg
                        // hetzelfde als de vorige keer (zelfde hoogtes en dezelfde wegen in de buurt): de onthouden driehoekjes, niet opnieuw snijden
                        long key = FieldKey(i, hcb0, hb0, special);
                        if (groundCache.TryGetValue(i, out var gc) && gc.key == key) { foreach (var t in gc.tris) (t.tag == null ? mb0 : TB(t.tag)).Tri(t.a, t.b, t.c, c.dir); continue; }
                        rec = new List<GTri>();
                        for (int k = 0; k < m; k++) LineGround(c, k, hcb0, hb0[k], hb0[(k + 1) % m], Emit2);
                        groundCache[i] = (key, rec); rec = null;
                        continue;
                    }
                    var near = (carved.Count > 0 || fillN > 0) && !plat ? CutsNear(c) : null; float[] hbase = null; float hcb = g;
                    if (near != null && near.Count > 0)
                    {
                        hbase = new float[m]; for (int k = 0; k < m; k++) hbase[k] = CornerBase(i, k);
                        if (IsWalk(i) && !levelled.Contains(i)) { hcb = 0f; foreach (float x in hbase) hcb += x; hcb /= m; }
                    }
                    for (int k = 0; k < m; k++)
                    {
                        float ha = hk[k], hb = hk[(k + 1) % m]; Vector3 A = c.corners[k] * (Planet.R + ha), B = c.corners[(k + 1) % m] * (Planet.R + hb);
                        if (hasDeck) { Vector3 dw = (c.corners[k] + c.corners[(k + 1) % m]).normalized * Planet.R - dk.p; if (Vector3.Dot(dw, dk.o) - dk.skew * Vector3.Dot(dw, dk.t) < -0.5f) continue; }   // onder het dek van de brug
                        if (hbase != null) { ExactGround(c, k, near, hcb, hbase[k], hbase[(k + 1) % m], Emit); continue; }
                        Emit(mid, A, B, hc, ha, hb);
                    }
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
                // sokkel: een plateau boven het maaiveld krijgt stenen wanden tot op het maaiveld ernaast (tussen twee plateaus: een keermuur)
                if ((plat || cut) && !Quay(i))   // een gracht heeft kademuren in plaats van een sokkel (PlanetNature.cs)
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
                        Vector3 a = c.corners[k], b = c.corners[(k + 1) % m];
                        float ga = CornerH(i, k), gb = CornerH(i, (k + 1) % m);
                        if (HasField && NearSeg(c.dir, c.radius + 1f))   // naast een weg op een niveau (ook als alleen de rand van het vak erbij komt): de wand tot op het talud (dat kan lager liggen dan het oude maaiveld), in stukjes
                        {
                            float ba = CornerBase(i, k), bb = CornerBase(i, (k + 1) % m); const int N = 12;
                            for (int t = 0; t < N; t++)
                            {
                                float t0 = t / (float)N, t1 = (t + 1) / (float)N; Vector3 pa = Vector3.Lerp(a, b, t0).normalized, pb = Vector3.Lerp(a, b, t1).normalized;
                                float fa = Mathf.Min(Mathf.Lerp(ga, gb, t0), GroundField(pa, Mathf.Lerp(ba, bb, t0))), fb = Mathf.Min(Mathf.Lerp(ga, gb, t1), GroundField(pb, Mathf.Lerp(ba, bb, t1)));
                                float lo2 = Mathf.Min(fa, fb) - 0.4f;   // iets onder de grond: geen kier waar het talud tussen twee meetpunten knikt if (g - lo2 < 0.01f) continue;
                                TB("sokkel").Quad(pa * RG, pb * RG, pb * (Planet.R + lo2), pa * (Planet.R + lo2), (a + b) * 0.5f - c.dir);
                            }
                            continue;
                        }
                        if (g - ga < 0.01f && g - gb < 0.01f) continue;
                        TB("sokkel").Quad(a * RG, b * RG, b * (Planet.R + gb), a * (Planet.R + ga), (a + b) * 0.5f - c.dir);
                    }
                if (pole) PoleMarker(PM, c, g, i == Planet.North);
                if (start && !pole)   // startplek (op een pool staat de poolpaal): lichtblauw vak met een witte paal van 30 m en een vlaggetje
                {
                    Vector3 top = c.dir * (RG + 30f);
                    for (int k = 0; k < 6; k++) { Vector3 a = Vector3.Lerp(c.corners[k], c.dir, 0.93f) * (RG + 0.3f), b = Vector3.Lerp(c.corners[(k + 1) % 6], c.dir, 0.93f) * (RG + 0.3f); sm.Tri(a, b, top, (a + b) * 0.5f - c.dir * RG + c.dir * 0.05f); }
                    Vector3 fl = c.dir * (RG + 27f) + c.east * 6f, fb = c.dir * (RG + 24f);
                    sm.Tri(top, fl, fb, c.north); sm.Tri(top, fb, fl, -c.north);
                    for (int k = 0; k < 6; k++) { Vector3 a = c.corners[k], b = c.corners[(k + 1) % 6]; Vector3 am = (a + b) * 0.5f; Vector3 ai = Vector3.Lerp(a, c.dir, 0.08f), bi = Vector3.Lerp(b, c.dir, 0.08f); sm.Quad(a * (Planet.R + hk[k]), b * (Planet.R + hk[(k + 1) % 6]), bi * (RG + 0.25f), ai * (RG + 0.25f), am - c.dir); }
                }
                if (pent)   // baken: een slanke oranje punt van 60 m, zodat je de vijfhoeken ook vanuit de ruimte vindt
                {
                    Vector3 apex = c.dir * (RG + 60f);
                    for (int k = 0; k < 5; k++) { Vector3 a = Vector3.Lerp(c.corners[k], c.dir, 0.7f) * (RG + 0.3f), b = Vector3.Lerp(c.corners[(k + 1) % 5], c.dir, 0.7f) * (RG + 0.3f); pm.Tri(a, b, apex, (a + b) * 0.5f - c.dir * RG + c.dir * 0.05f); }
                }
                if (pent)   // opstaand randje rondom, zodat de vijfhoek ook van dichtbij opvalt
                    for (int k = 0; k < 5; k++) { Vector3 a = c.corners[k], b = c.corners[(k + 1) % 5]; Vector3 am = (a + b) * 0.5f; Vector3 ai = Vector3.Lerp(a, c.dir, 0.08f), bi = Vector3.Lerp(b, c.dir, 0.08f); pm.Quad(a * (Planet.R + hk[k]), b * (Planet.R + hk[(k + 1) % 5]), bi * (RG + 0.25f), ai * (RG + 0.25f), am - c.dir); }
            }
            foreach (var kv in terrB)
            {
                var tg = new GameObject("Terrein " + kv.Key + " " + (c0 / ChunkN)); tg.transform.SetParent(groundRoot, false); made.Add(tg); tg.layer = Layers.Bodem;
                var tmesh = kv.Value.Build(); tg.AddComponent<MeshFilter>().sharedMesh = tmesh; tg.AddComponent<MeshRenderer>().sharedMaterial = Mats.Get("p_terr_" + kv.Key, TerrColor(kv.Key));
                tg.AddComponent<MeshCollider>().sharedMesh = tmesh;
            }
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
            case "goud": return new Color(0.9f, 0.72f, 0.25f); case "vlagrood": return new Color(0.68f, 0.11f, 0.16f); case "vlagwit": return Color.white; case "vlagblauw": return new Color(0.13f, 0.27f, 0.55f);
            default: return Color.gray;
        }
    }

    // ---------- blokken en wegen per deel ----------
    class Frame
    {
        public Vector3 o, e, u, n; public Vector2[] poly; public float rad;
        // weg- en padvakken: per aangesloten buur de halve vector ernaartoe (de rand) en het halve hoogteverschil; null = vlak
        Vector2[] sq; float[] sd;
        // bolstand (wegknopen): elk punt wordt op de bol gelegd in plaats van in het raakvlak, zodat het precies aansluit op de linten van de wegen
        bool sphere; Vector3 cdir; float cg;
        public Frame(Frame b, Vector3 o2, Vector2[] p2, float r2) { u = b.u; e = b.e; n = b.n; o = o2; poly = p2; rad = r2; }
        public Frame(int i, bool onSphere = false, bool under = false)
        {
            var c = Planet.Cells[i]; u = c.dir; e = c.east; n = c.north; o = c.dir * (Planet.R + G(i)); poly = c.local; rad = c.radius;
            sphere = onSphere; cdir = c.dir; cg = G(i);
            if (!IsWalk(i)) return;
            var q = new List<Vector2>(); var d = new List<float>(); bool any = false; float g = G(i);
            if (under && Low.TryGetValue(i, out var lo)) { g = cg = lo; o = c.dir * (Planet.R + g); }   // het pad onder de brug
            foreach (int j in Partners(i))
            {
                if (!IsWalk(j) || UnderLink(i, j) != under) continue;   // de weg op de brug en het pad eronder hellen elk naar hun eigen buren
                Vector3 w = (Planet.Cells[j].dir - c.dir) * Planet.R; float gj = LevelFor(j, i);
                q.Add(new Vector2(Vector3.Dot(w, e), Vector3.Dot(w, n)) * 0.5f); d.Add((gj - g) * 0.5f); if (Mathf.Abs(gj - g) > 0.002f) any = true;
            }
            if (any) { sq = q.ToArray(); sd = d.ToArray(); }
        }
        // Hoogte boven het hart van het vak op (x, z): langs elke arm lineair van het hart naar de rand, waar de weg op de
        // gemiddelde hoogte van de twee vakken ligt. Tussen armen in mengt het naar de arm waar het punt het meest in ligt.
        public Frame Flat() { sq = null; sd = null; return this; }
        public Frame At(Vector3 dir) { if (dir == cdir) return this; cdir = dir; o = dir * (Planet.R + cg); sq = null; sd = null; return this; }   // het hart van de knoop op de rand (NodeDir), vlak   // zonder de helling naar de buren (die rekent de padknoop zelf)
        public bool Sloped => sq != null;   // helt mee met een buur: vlakken in kleine stukjes, zodat ze de gebogen hoogte volgen
        public float H(float x, float z)
        {
            if (sq == null) return 0f; Vector2 p = new Vector2(x, z); float pl = p.magnitude; if (pl < 1e-3f) return 0f;
            float h = 0f, ws = 0f;
            for (int k = 0; k < sq.Length; k++)
            {
                float L = sq[k].magnitude, cs = Vector2.Dot(p, sq[k]) / (pl * L); if (cs <= 0f) continue;
                float w = cs * cs; w *= w; w *= w;   // cos^8
                h += w * Mathf.Clamp01(Vector2.Dot(p, sq[k]) / (L * L)) * sd[k]; ws += w;
            }
            return ws > 1e-9f ? h / ws : 0f;
        }
        public Vector3 P(float x, float y, float z) => sphere ? (cdir * Planet.R + e * x + n * z).normalized * (Planet.R + cg + y + H(x, z)) : o + e * x + u * (y + H(x, z)) + n * z;
        public Vector3 D(float x, float y, float z) => e * x + u * y + n * z;
    }

    static Vector2 C(Frame f, int i, float k) => f.poly[i % f.poly.Length] * k;
    static void Wall(MeshBuilder m, Frame f, float k, float y0, float y1)
    {
        for (int i = 0; i < f.poly.Length; i++) { Vector2 a = C(f, i, k), b = C(f, i + 1, k), mid = (a + b) * 0.5f; m.Quad(f.P(a.x, y0, a.y), f.P(b.x, y0, b.y), f.P(b.x, y1, b.y), f.P(a.x, y1, a.y), f.D(mid.x, 0, mid.y)); }
    }
    static void Top(MeshBuilder m, Frame f, float k, float y)
    {
        for (int i = 0; i < f.poly.Length; i++) { Vector2 a = C(f, i, k), b = C(f, i + 1, k); m.Tri(f.P(0, y, 0), f.P(a.x, y, a.y), f.P(b.x, y, b.y), f.u); }
    }
    static void Windows(MeshBuilder m, Frame f, float k, float y0, float y1, float f0, float f1)
    {
        for (int i = 0; i < f.poly.Length; i++)
        {
            Vector2 a = C(f, i, k), b = C(f, i + 1, k), outw = ((a + b) * 0.5f).normalized;
            Vector2 p0 = Vector2.Lerp(a, b, f0) + outw * 0.04f, p1 = Vector2.Lerp(a, b, f1) + outw * 0.04f;
            m.Quad(f.P(p0.x, y0, p0.y), f.P(p1.x, y0, p1.y), f.P(p1.x, y1, p1.y), f.P(p0.x, y1, p0.y), f.D(outw.x, 0, outw.y));
        }
    }
    static void Cone(MeshBuilder m, Frame f, float cx, float cz, float r, float y0, float y1, float rTop)
    {
        for (int i = 0; i < 6; i++)
        {
            float a0 = i * Mathf.PI / 3f, a1 = (i + 1) * Mathf.PI / 3f, am = (a0 + a1) * 0.5f;
            Vector3 b0 = f.P(cx + Mathf.Cos(a0) * r, y0, cz + Mathf.Sin(a0) * r), b1 = f.P(cx + Mathf.Cos(a1) * r, y0, cz + Mathf.Sin(a1) * r);
            Vector3 outw = f.D(Mathf.Cos(am), (r - rTop) / Mathf.Max(0.01f, y1 - y0), Mathf.Sin(am));
            if (rTop <= 0.001f) m.Tri(b0, b1, f.P(cx, y1, cz), outw);
            else m.Quad(b0, b1, f.P(cx + Mathf.Cos(a1) * rTop, y1, cz + Mathf.Sin(a1) * rTop), f.P(cx + Mathf.Cos(a0) * rTop, y1, cz + Mathf.Sin(a0) * rTop), outw);
        }
    }
    static void Roof(MeshBuilder m, Frame f, float k, float y1)
    {
        Vector3 apex = f.P(0, y1 + 2.4f, 0);
        for (int i = 0; i < f.poly.Length; i++) { Vector2 a = C(f, i, k), b = C(f, i + 1, k), mid = (a + b) * 0.5f; m.Tri(f.P(a.x, y1, a.y), f.P(b.x, y1, b.y), apex, f.D(mid.x, 0.6f * mid.magnitude, mid.y)); }
    }
    static void Cap6(MeshBuilder m, Frame f, float r, float y)
    {
        for (int i = 0; i < 6; i++) { float a0 = i * Mathf.PI / 3f, a1 = (i + 1) * Mathf.PI / 3f; m.Tri(f.P(0, y, 0), f.P(Mathf.Cos(a0) * r, y, Mathf.Sin(a0) * r), f.P(Mathf.Cos(a1) * r, y, Mathf.Sin(a1) * r), f.u); }
    }
    static void Disc(MeshBuilder m, Frame f, float r, float y)
    {
        int nr = f.Sloped ? Mathf.Max(1, Mathf.CeilToInt(r / 0.8f)) : 1, ns = f.Sloped ? 24 : 12;
        System.Func<int, int, Vector3> Pt = (k, i) => { float rr = r * k / nr, a = i * Mathf.PI * 2f / ns; return f.P(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr); };
        for (int i = 0; i < ns; i++)
        {
            m.Tri(f.P(0, y, 0), Pt(1, i), Pt(1, i + 1), f.u);
            for (int k = 1; k < nr; k++) m.Quad(Pt(k, i), Pt(k + 1, i), Pt(k + 1, i + 1), Pt(k, i + 1), f.u);
        }
    }
    static void Skirt(MeshBuilder m, Frame f, float r, float y0, float y1)
    {
        for (int i = 0; i < 12; i++) { float a0 = i * Mathf.PI / 6f, a1 = (i + 1) * Mathf.PI / 6f, am = (a0 + a1) * 0.5f; m.Quad(f.P(Mathf.Cos(a0) * r, y0, Mathf.Sin(a0) * r), f.P(Mathf.Cos(a1) * r, y0, Mathf.Sin(a1) * r), f.P(Mathf.Cos(a1) * r, y1, Mathf.Sin(a1) * r), f.P(Mathf.Cos(a0) * r, y1, Mathf.Sin(a0) * r), f.D(Mathf.Cos(am), 0, Mathf.Sin(am))); }
    }
    // een balkje op (cx, cz), lang langs richting ang (halve lengte hl, halve breedte hw), van y0 tot y1
    static void Box(MeshBuilder m, Frame f, float cx, float cz, float ang, float hl, float hw, float y0, float y1)
    {
        Vector2 c = new Vector2(cx, cz), d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * hl, r = new Vector2(-Mathf.Sin(ang), Mathf.Cos(ang)) * hw;
        Vector2[] p = { c - d - r, c + d - r, c + d + r, c - d + r };
        for (int i = 0; i < 4; i++) { Vector2 a = p[i], b = p[(i + 1) % 4], o = (a + b) * 0.5f - c; m.Quad(f.P(a.x, y0, a.y), f.P(b.x, y0, b.y), f.P(b.x, y1, b.y), f.P(a.x, y1, a.y), f.D(o.x, 0, o.y)); }
        m.Quad(f.P(p[0].x, y1, p[0].y), f.P(p[1].x, y1, p[1].y), f.P(p[2].x, y1, p[2].y), f.P(p[3].x, y1, p[3].y), f.u);
    }
    // spitse torenspits op de vakvorm (schaal k), van y tot y + hgt
    static void Spire(MeshBuilder m, Frame f, float k, float y, float hgt)
    {
        Vector3 apex = f.P(0, y + hgt, 0);
        for (int i = 0; i < f.poly.Length; i++) { Vector2 a = C(f, i, k), b = C(f, i + 1, k), mid = (a + b) * 0.5f; m.Tri(f.P(a.x, y, a.y), f.P(b.x, y, b.y), apex, f.D(mid.x, 0.3f * mid.magnitude, mid.y)); }
    }
    // gestreepte zonneluifel boven een etalage: schuin naar buiten en omlaag, aan beide kanten zichtbaar
    static void Awning(System.Func<string, MeshBuilder> M, Frame f, float k, float y, string stripe)
    {
        for (int i = 0; i < f.poly.Length; i++)
        {
            Vector2 a = C(f, i, k), b = C(f, i + 1, k), outw = ((a + b) * 0.5f).normalized; const int S = 6;
            for (int s = 0; s < S; s++)
            {
                Vector2 p0 = Vector2.Lerp(a, b, 0.1f + 0.8f * s / S), p1 = Vector2.Lerp(a, b, 0.1f + 0.8f * (s + 1) / S), q0 = p0 + outw * 1.1f, q1 = p1 + outw * 1.1f;
                var mb = M(s % 2 == 0 ? stripe : "doek");
                Vector3 A = f.P(p0.x, y, p0.y), B = f.P(p1.x, y, p1.y), Cc = f.P(q1.x, y - 0.45f, q1.y), D = f.P(q0.x, y - 0.45f, q0.y);
                mb.Quad(A, B, Cc, D, f.D(outw.x * 0.4f, 1f, outw.y * 0.4f)); mb.Quad(A, B, Cc, D, f.D(-outw.x * 0.4f, -1f, -outw.y * 0.4f));
            }
        }
    }

    // Molenwieken: vier armen met een zeil, in het xy-vlak rond de as (z = naar voren). Eén gedeelde mesh voor alle molens.
    static Mesh wiekMesh, zeilMesh;
    const float WiekL = 6.3f;
    static void WiekMeshes()
    {
        if (wiekMesh != null && zeilMesh != null) return;
        var w = new MeshBuilder(); var z = new MeshBuilder();
        for (int k = 0; k < 4; k++)
        {
            float t = k * Mathf.PI / 2f; Vector3 u = new Vector3(Mathf.Cos(t), Mathf.Sin(t), 0f), p = new Vector3(-u.y, u.x, 0f);
            Vector3 a = -p * 0.09f, b = p * 0.09f, c = u * WiekL + p * 0.09f, d = u * WiekL - p * 0.09f, fz = new Vector3(0, 0, 0.18f);
            w.Quad(a + fz, b + fz, c + fz, d + fz, Vector3.forward); w.Quad(a + fz, b + fz, c + fz, d + fz, Vector3.back);
            w.Quad(a, d, d + fz, a + fz, -p); w.Quad(b, c, c + fz, b + fz, p);
            Vector3 s0 = u * 1.1f + p * 0.12f, s1 = u * WiekL + p * 0.12f, s2 = u * WiekL + p * 1.45f, s3 = u * 1.1f + p * 1.45f, zz = new Vector3(0, 0, 0.1f);
            z.Quad(s0 + zz, s1 + zz, s2 + zz, s3 + zz, Vector3.forward); z.Quad(s0 + zz, s1 + zz, s2 + zz, s3 + zz, Vector3.back);
            for (float r = 1.9f; r < WiekL; r += 1.1f) { Vector3 l0 = u * r + p * 0.1f, l1 = u * r + p * 1.47f, lw = u * 0.06f, lz = new Vector3(0, 0, 0.14f); w.Quad(l0 - lw + lz, l0 + lw + lz, l1 + lw + lz, l1 - lw + lz, Vector3.forward); w.Quad(l0 - lw + lz, l0 + lw + lz, l1 + lw + lz, l1 - lw + lz, Vector3.back); }   // dwarslatten
        }
        for (int k = 0; k < 6; k++) { float a0 = k * Mathf.PI / 3f, a1 = (k + 1) * Mathf.PI / 3f; Vector3 p0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0) * 0.45f, p1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0) * 0.45f, hz = new Vector3(0, 0, 0.35f); w.Tri(hz, p0 + hz, p1 + hz, Vector3.forward); w.Quad(p0, p1, p1 + hz, p0 + hz, (p0 + p1)); }   // naaf
        wiekMesh = w.Build(); zeilMesh = z.Build();
    }
    static void MakeWieken(Transform parent, Vector3 pos, Quaternion rot, int hash)
    {
        WiekMeshes();
        var pivot = new GameObject("Wieken"); pivot.transform.SetParent(parent, false); pivot.transform.SetPositionAndRotation(pos, rot);
        pivot.transform.Rotate(0f, 0f, (hash & 255) / 255f * 90f, Space.Self);
        var sp = pivot.AddComponent<Wieken>(); sp.Speed = 38f + ((hash >> 8) & 15);
        foreach (var (mesh, key) in new[] { (wiekMesh, "wiek"), (zeilMesh, "zeil") })
        {
            var c = new GameObject(key); c.transform.SetParent(pivot.transform, false); c.layer = Layers.Gebouw;
            c.AddComponent<MeshFilter>().sharedMesh = mesh; c.AddComponent<MeshRenderer>().sharedMaterial = Mats.Get("p_" + key, ColorOf(key));
        }
    }

    // Op een hellend vak is de hoogte tussen de armen gebogen (Frame.H): dan wordt een vlak een raster van stukjes van ~0,8 m,
    // anders steekt een groot vlak (het zand van een fietspad) door een smaller vlak dat er 1 cm boven hoort (het rode fietsdeel)
    static void Strip(MeshBuilder m, Frame f, Vector2 d, Vector2 r, float s0, float s1, float hw, float y)
    {
        int nu = f.Sloped ? Mathf.Max(1, Mathf.CeilToInt((s1 - s0) / 0.8f)) : 1, nv = f.Sloped ? Mathf.Max(1, Mathf.CeilToInt(2f * hw / 0.8f)) : 1;
        for (int u = 0; u < nu; u++) for (int v = 0; v < nv; v++)
        {
            float sa = Mathf.Lerp(s0, s1, u / (float)nu), sb = Mathf.Lerp(s0, s1, (u + 1) / (float)nu), la = Mathf.Lerp(-hw, hw, v / (float)nv), lb = Mathf.Lerp(-hw, hw, (v + 1) / (float)nv);
            Vector2 a = d * sa + r * la, b = d * sa + r * lb, c = d * sb + r * lb, e = d * sb + r * la;
            m.Quad(f.P(a.x, y, a.y), f.P(b.x, y, b.y), f.P(c.x, y, c.y), f.P(e.x, y, e.y), f.u);
        }
    }
    static void StripSides(MeshBuilder m, Frame f, Vector2 d, Vector2 r, float s0, float s1, float hw, float y0, float y1)
    {
        int nu = f.Sloped ? Mathf.Max(1, Mathf.CeilToInt((s1 - s0) / 0.8f)) : 1;
        for (int sg = -1; sg <= 1; sg += 2) for (int u = 0; u < nu; u++)
        {
            Vector2 a = d * Mathf.Lerp(s0, s1, u / (float)nu) + r * (hw * sg), b = d * Mathf.Lerp(s0, s1, (u + 1) / (float)nu) + r * (hw * sg);
            m.Quad(f.P(a.x, y0, a.y), f.P(b.x, y0, b.y), f.P(b.x, y1, b.y), f.P(a.x, y1, a.y), f.D(r.x * sg, 0, r.y * sg));
        }
    }

    // Een wegvak: één nette omtrek per laag (stoep, asfalt) in plaats van losse schijven en stroken. De omtrek wordt
    // rondom bemonsterd: in elke richting de verste rand van de armen naar de buurwegen, met een kern die bochten
    // afrondt. Daarop: stoeprand, middenstreepjes op rechte armen, een eilandje op kruisingen en een lantaarn.
    // De richting van de weg door een buurvak dat zelf een "schuin recht stuk" is (twee aansluitingen die 120° of 180° maken),
    // uitgedrukt in het assenstelsel van dit vak en wijzend dit vak in. Geeft false als de buur gewoon via zijn hart gaat.
    // Wereldrichting van de weg door een schuin recht wegvak (zie ChordDirOf); false als het vak via zijn hart gaat
    public static bool ChordDirWorld(int j, out Vector3 dir)
    {
        dir = Vector3.zero; var arms = new List<Vector3>();
        foreach (int k in Partners(j)) if (IsRoad(k)) arms.Add(((Planet.Cells[j].dir + Planet.Cells[k].dir) * 0.5f).normalized);
        if (arms.Count != 2) return false;
        Vector3 cj = Planet.Cells[j].dir; if (Vector3.Dot((arms[0] - cj).normalized, (arms[1] - cj).normalized) >= -0.3f) return false;
        dir = (arms[1] - arms[0]).normalized; return true;
    }

    static bool ChordDirOf(int j, Frame fr, Vector2 e, out Vector2 dir)
    {
        dir = (-e).normalized; var arms = new List<Vector3>();
        foreach (int k in Partners(j)) if (IsRoad(k)) arms.Add(((Planet.Cells[j].dir + Planet.Cells[k].dir) * 0.5f).normalized * Planet.R);
        if (arms.Count != 2) return false;
        Vector3 cj = Planet.Cells[j].dir * Planet.R; Vector3 a0 = arms[0] - cj, a1 = arms[1] - cj;
        if (Vector3.Dot(a0.normalized, a1.normalized) >= -0.3f) return false;
        Vector3 w = arms[1] - arms[0]; Vector2 d = new Vector2(Vector3.Dot(w, fr.e), Vector3.Dot(w, fr.n)).normalized;
        if (Vector2.Dot(d, -e) < 0f) d = -d; dir = d; return true;
    }

    // ---------- wegen ----------
    // Afmetingen per stijl: halve breedte van het asfalt en van asfalt + stoep (m). 0 = gewone weg, 1 = brede weg, 2 = laan (met middenberm)
    static float hwA = 2.5f, hwS = 4.5f; static byte styleNow; static bool bridgeNow;
    static void Dims(byte style, out float a, out float s) { if (style == 1) { a = 4.2f; s = 6.2f; } else if (style == 2) { a = 4.4f; s = 6.0f; } else { a = 2.5f; s = 4.5f; } }
    // Profiel zoals in de webversie: asfalt net boven de grond, een schuine stoeprand van 30 cm en de stoep 25 cm hoog,
    // met een rand die tot onder de grond doorloopt. De kern van een kruising ligt 5 mm lager dan de armen.
    const float AY = 0.10f, ACY = 0.095f, SY = 0.25f, KW = 0.3f, LY = 0.11f, SKB = -0.35f;
    // Zijkant van stoep en pad tot onder het maaiveld: dieper waar het vak in een kom ligt of de weg helt
    static float skb = SKB;
    static float SkirtDepth(int i)
    {
        if (Underpass(i)) return SKB;   // een brugdek: geen rok tot in de sleuf
        float g = G(i), s = 0f; foreach (int j in Partners(i)) if (IsWalk(j) && !UnderLink(i, j)) s = Mathf.Max(s, Mathf.Abs(LevelFor(j, i) - g) * 0.5f);
        return SKB - Mathf.Max(0f, g - MinCorner(i)) - s;
    }
    // schuine stoeprand van de rand van het asfalt (a0-a1, hoogte ya) naar de stoep (b0-b1, hoogte SY)
    static void Kerb(MeshBuilder m, Frame fr, Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1, float ya)
    {
        Vector2 o = (b0 + b1 - a0 - a1) * 0.5f;
        m.Quad(fr.P(a0.x, ya, a0.y), fr.P(a1.x, ya, a1.y), fr.P(b1.x, SY, b1.y), fr.P(b0.x, SY, b0.y), fr.D(-o.x, KW, -o.y));
    }
    // stuk stoep tussen een binnenlijn (i0-i1) en een buitenlijn (o0-o1), met een zijkant omlaag langs de buitenlijn
    static void Walk(MeshBuilder m, Frame fr, Vector2 i0, Vector2 i1, Vector2 o0, Vector2 o1)
    {
        m.Quad(fr.P(i0.x, SY, i0.y), fr.P(i1.x, SY, i1.y), fr.P(o1.x, SY, o1.y), fr.P(o0.x, SY, o0.y), fr.u);
        Vector2 w = (o0 + o1 - i0 - i1) * 0.5f;
        m.Quad(fr.P(o0.x, skb, o0.y), fr.P(o1.x, skb, o1.y), fr.P(o1.x, SY, o1.y), fr.P(o0.x, SY, o0.y), fr.D(w.x, 0, w.y));
    }
    public static float LaneOffset(int i, int lane = -1) { byte s = StyleOf(i); return s == 1 ? (lane < 0 ? 2.5f : lane == 0 ? 1.05f : 3.15f) : s == 2 ? 2.9f : 1.35f; }   // rijbaan van de auto, rechts van het midden; een brede weg heeft twee stroken per richting (0 links, 1 rechts)
    public static float WalkOffset(int i) { byte s = StyleOf(i); return s == 0 ? 3.5f : 5.2f; }                       // looplijn van voetgangers op de stoep
    static Vector2 PoleAt(Vector2 e, Vector2 d) { Vector2 r = new Vector2(d.y, -d.x); return e - d * 0.8f + r * (hwS - 0.6f); }   // verkeerslicht: rechts op de stoep van de arm, net voor de stopstreep (buiten de kruising, anders staat hij bij een scherpe hoek op het asfalt van een andere arm)
    // wereldpositie (bovenkant) van het verkeerslicht bij vak i voor verkeer dat uit buur p komt
    public static bool PolePos(int i, int p, out Vector3 pos)
    {
        pos = Vector3.zero; if (!IsRoad(i)) return false;
        var fr = new Frame(i).At(NodeDir(i)); float a, s; Dims(ArmStyle(i, p), out a, out s); float old = hwS; hwS = s;
        Vector3 d3 = NodeDir(p) * Planet.R - fr.o; Vector2 q = new Vector2(Vector3.Dot(d3, fr.e), Vector3.Dot(d3, fr.n)); q = q.normalized * NodeTrim(i, p, RoadNb(i).Count); Vector2 d = (-q).normalized;   // armen van een kruising lopen naar het hart; de arm begint waar de knoop ophoudt (zoals RoadNode)
        Vector2 pp = PoleAt(q, d); hwS = old; pos = fr.P(pp.x, 3.75f, pp.y); return true;
    }

    // pijl op het asfalt die langs d wijst (eenrichtingsverkeer)
    static void Arrow(System.Func<string, MeshBuilder> M, Frame fr, Vector2 c, Vector2 d, float len)
    {
        d = d.normalized; Vector2 r = new Vector2(d.y, -d.x); const float y = LY + 0.001f;
        Vector2 b0 = c - d * len * 0.5f, b1 = c + d * len * 0.1f, tip = c + d * len * 0.5f;
        M("lijn").Quad(fr.P(b0.x - r.x * 0.14f, y, b0.y - r.y * 0.14f), fr.P(b0.x + r.x * 0.14f, y, b0.y + r.y * 0.14f), fr.P(b1.x + r.x * 0.14f, y, b1.y + r.y * 0.14f), fr.P(b1.x - r.x * 0.14f, y, b1.y - r.y * 0.14f), fr.u);
        M("lijn").Tri(fr.P(b1.x - r.x * 0.55f, y, b1.y - r.y * 0.55f), fr.P(b1.x + r.x * 0.55f, y, b1.y + r.y * 0.55f), fr.P(tip.x, y, tip.y), fr.u);
    }
    // middenberm van een laan met bomen, van a naar b
    static void Median(System.Func<string, MeshBuilder> M, Frame fr, Vector2 a, Vector2 b)
    {
        Vector2 d = b - a; float len = d.magnitude; if (len < 0.05f) return; d /= len; Vector2 r = new Vector2(d.y, -d.x); const float w = 1.1f, wr = w + 0.2f;
        M("rand").Quad(fr.P(a.x - r.x * wr, 0.2f, a.y - r.y * wr), fr.P(a.x + r.x * wr, 0.2f, a.y + r.y * wr), fr.P(b.x + r.x * wr, 0.2f, b.y + r.y * wr), fr.P(b.x - r.x * wr, 0.2f, b.y - r.y * wr), fr.u);
        M("gras").Quad(fr.P(a.x - r.x * w, 0.23f, a.y - r.y * w), fr.P(a.x + r.x * w, 0.23f, a.y + r.y * w), fr.P(b.x + r.x * w, 0.23f, b.y + r.y * w), fr.P(b.x - r.x * w, 0.23f, b.y - r.y * w), fr.u);
        foreach (int sg in new[] { -1, 1 }) { Vector2 pa = a + r * (sg * wr), pb = b + r * (sg * wr); M("rand").Quad(fr.P(pa.x, AY, pa.y), fr.P(pb.x, AY, pb.y), fr.P(pb.x, 0.2f, pb.y), fr.P(pa.x, 0.2f, pa.y), fr.D(r.x * sg, 0, r.y * sg)); }   // de zijkanten
        // de kop rond: een halve cirkel om b, met de rand, het gras en de zijkant
        const int nk = 10;
        for (int k = 0; k < nk; k++)
        {
            float t0 = Mathf.PI * k / nk, t1 = Mathf.PI * (k + 1) / nk;
            Vector2 u0 = r * Mathf.Cos(t0) + d * Mathf.Sin(t0), u1 = r * Mathf.Cos(t1) + d * Mathf.Sin(t1);
            Vector2 o0 = b + u0 * wr, o1 = b + u1 * wr, g0 = b + u0 * w, g1 = b + u1 * w;
            M("rand").Quad(fr.P(g0.x, 0.2f, g0.y), fr.P(g1.x, 0.2f, g1.y), fr.P(o1.x, 0.2f, o1.y), fr.P(o0.x, 0.2f, o0.y), fr.u);
            M("gras").Tri(fr.P(b.x, 0.23f, b.y), fr.P(g0.x, 0.23f, g0.y), fr.P(g1.x, 0.23f, g1.y), fr.u);
            Vector2 um = (u0 + u1) * 0.5f; M("rand").Quad(fr.P(o0.x, AY, o0.y), fr.P(o1.x, AY, o1.y), fr.P(o1.x, 0.2f, o1.y), fr.P(o0.x, 0.2f, o0.y), fr.D(um.x, 0, um.y));
        }
        for (float s = Mathf.Min(3.5f, len * 0.5f); len >= 2f && s < len - 1.5f; s += 7f)
        {
            Vector2 p = a + d * s; Cone(M("stam"), fr, p.x, p.y, 0.2f, 0.2f, 2.3f, 0.15f); Cone(M("blad"), fr, p.x, p.y, 1.5f, 1.9f, 4.8f, 0f);
        }
    }
    // balustrade langs een brug van a naar b: een muurtje van 24 cm dik en 1,05 m hoog op de lijn a-b, met een bovenkant en
    // kopse kanten (geen wand zonder dikte)
    static void Parapet(System.Func<string, MeshBuilder> M, Frame fr, Vector2 a, Vector2 b, Vector2 outward)
    {
        Vector2 d = b - a; if (d.sqrMagnitude < 1e-4f) return; d.Normalize();
        Vector2 o = outward.sqrMagnitude > 1e-6f ? outward.normalized * 0.12f : new Vector2(d.y, -d.x) * 0.12f;
        const float top = 1.05f;
        Vector2 ao = a + o, bo = b + o, ai = a - o, bi = b - o;
        Vector3 Pt(Vector2 q, float y) => fr.P(q.x, y, q.y);
        M("rand").Quad(Pt(ao, SY), Pt(bo, SY), Pt(bo, top), Pt(ao, top), fr.D(o.x, 0, o.y));       // buitenkant
        M("rand").Quad(Pt(ai, SY), Pt(bi, SY), Pt(bi, top), Pt(ai, top), fr.D(-o.x, 0, -o.y));     // binnenkant
        M("rand").Quad(Pt(ai, top), Pt(bi, top), Pt(bo, top), Pt(ao, top), fr.u);                   // bovenkant
        M("rand").Quad(Pt(ai, SY), Pt(ao, SY), Pt(ao, top), Pt(ai, top), fr.D(-d.x, 0, -d.y));     // kopse kant bij a
        M("rand").Quad(Pt(bi, SY), Pt(bo, SY), Pt(bo, top), Pt(bi, top), fr.D(d.x, 0, d.y));       // kopse kant bij b
    }
    // de stijlen en brug die bij het hele wegvak horen: pijler onder een brug
    static void BridgePillar(System.Func<string, MeshBuilder> M, Frame fr, int i)
    {
        if (Underpass(i)) return;   // het pad loopt eronderdoor
        float depth = Under.TryGetValue(i, out var u) && u == "kuil" ? -3f : -1.5f;
        Cone(M("steen"), fr, 0f, 0f, 1.1f, depth, -0.3f, 1.1f);
    }

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

    // Een knoop van het wegennet (kruising, knik, doodlopend eind of los vak). ends: per arm het punt op de as waar het lint
    // van die arm ophoudt (NodeTrim van het hart, in de richting van de buur ids[a]); de armen lopen naar het hart, zoals in de
    // webversie alle wegen door de knooppunten gaan. Binnen die afstand tekent de knoop alles: asfalt, stoepranden en stoep.
    static void RoadNode(Dictionary<string, MeshBuilder> mb, Frame fr, int i, List<Vector2> ends, List<int> ids)
    {
        System.Func<string, MeshBuilder> M = k => { if (!mb.TryGetValue(k, out var x)) { x = new MeshBuilder(); mb[k] = x; } return x; };
        styleNow = StyleOf(i); Dims(styleNow, out hwA, out hwS); bridgeNow = Under.ContainsKey(i) && !Underpass(i);   // een onderdoorgang heeft geen balustrade: de portalen liggen gelijk met het maaiveld
        skb = SkirtDepth(i);
        int na = ends.Count; var dirs = new List<Vector2>(); for (int a = 0; a < na; a++) dirs.Add((-ends[a]).normalized);
        if (na == 0)   // los wegvak: een rond pleintje asfalt met stoeprand en stoep eromheen
        {
            float R0 = hwA + 2.1f; var c0 = Circle(Vector2.zero, R0, 0f, Mathf.PI * 2f, 40); var c1 = Circle(Vector2.zero, R0 + KW, 0f, Mathf.PI * 2f, 40); var c2 = Circle(Vector2.zero, R0 + hwS - hwA, 0f, Mathf.PI * 2f, 40);
            for (int k = 0; k < 40; k++) { M("asfalt").Tri(fr.P(0, AY, 0), fr.P(c0[k].x, AY, c0[k].y), fr.P(c0[k + 1].x, AY, c0[k + 1].y), fr.u); Kerb(M("rand"), fr, c0[k], c0[k + 1], c1[k], c1[k + 1], AY); Walk(M("stoep"), fr, c1[k], c1[k + 1], c2[k], c2[k + 1]); }
            return;
        }
        // elke arm zijn eigen stijl en breedte (die van de lijn erachter): een brede weg die op een gewone aansluit, versmalt
        // in de stoephoeken van de knoop
        var stA = new byte[na]; var wA = new float[na]; var wS = new float[na];
        for (int a = 0; a < na; a++) { stA[a] = ids != null && a < ids.Count ? ArmStyle(i, ids[a]) : styleNow; Dims(stA[a], out wA[a], out wS[a]); }
        System.Action<int> Use = a => { hwA = wA[a]; hwS = wS[a]; };   // de maten van arm a voor SideWalk, PoleAt en de rest
        // laag: 0 rand van het asfalt, 1 bovenkant stoeprand, 2 buitenkant stoep, 3 plek van de lantaarn
        System.Func<int, int, float> Lat = (a, layer) => layer == 0 ? wA[a] : layer == 1 ? wA[a] + KW : layer == 2 ? wS[a] : wS[a] - 0.6f;
        // Kruising, doodlopend eind of scherpe knik, zoals in de webversie: alle armen samen zijn één vlak asfalt. Tussen twee
        // opeenvolgende armen (tegen de klok in) loopt de stoep door met een stoephoek: een kwadratische boog die aan beide
        // stoepranden raakt, met hun snijpunt als trekpunt; recht als de randen (bijna) in elkaars verlengde liggen; rond om
        // het hart als de hoek groter is dan 200° (buitenkant van een knik) of bij een doodlopend eind (een ronde kop).
        bool signals = Lights && na >= 3;
        var order = new List<int>(); for (int a = 0; a < na; a++) order.Add(a);
        System.Func<int, float> angOf = a => Mathf.Atan2(-dirs[a].y, -dirs[a].x);
        order.Sort((x, y) => angOf(x).CompareTo(angOf(y)));
        // punt op arm a: lat opzij (+ = naar de volgende arm, tegen de klok in), s de arm in vanaf de vakrand
        System.Func<int, float, float, Vector2> P = (a, lat, s) =>
        {
            Vector2 e = ends[a], d = dirs[a], r = new Vector2(d.y, -d.x), en = e.normalized, p0 = e + r * lat; float den = Vector2.Dot(d, en);
            return p0 + d * ((Mathf.Abs(den) < 1e-4f ? 0f : Vector2.Dot(e - p0, en) / den) + s);
        };
        // per hoek (van arm order[c] naar arm order[c+1]): soort (0 boog, 1 recht, 2 rond), waar de stoepranden ophouden, middelpunt
        // en straal van een ronde hoek (rb + lat: de cirkel die op afstand lat aan de arm raakt)
        bool taper = false; var nArc = new int[na]; var kind = new int[na]; var sA = new float[na]; var sB = new float[na]; var cc = new Vector2[na]; var rb = new float[na];
        float maxPerp = 0f; for (int a = 0; a < na; a++) maxPerp = Mathf.Max(maxPerp, Mathf.Abs(Vector2.Dot(ends[a], new Vector2(dirs[a].y, -dirs[a].x))));
        System.Func<int, float, Vector2, float, float> hitS = (a, lat, c, rad) =>   // waar de lijn op afstand lat van arm a de cirkel (c, rad) voor het eerst raakt
        {
            Vector2 B = P(a, lat, 0f) - c; float bd = Vector2.Dot(B, dirs[a]), disc = bd * bd - B.sqrMagnitude + rad * rad;
            return Mathf.Max(0.3f, -bd - (disc > 0f ? Mathf.Sqrt(disc) : 0f));
        };
        for (int c = 0; c < na; c++)
        {
            int a = order[c], b = order[(c + 1) % na];
            float phi = na == 1 ? Mathf.PI * 2f : Mathf.Repeat(angOf(b) - angOf(a), Mathf.PI * 2f);
            if (na == 1 || phi > Mathf.PI * 200f / 180f)
            {
                kind[c] = 2;
                if (na == 1) { cc[c] = P(a, 0f, Vector2.Dot(-P(a, 0f, 0f), dirs[a])); rb[c] = stA[a] == 0 ? 2.1f : 0.3f; }   // ronde kop op de as, waar de weg het dichtst bij het hart komt
                else { cc[c] = Vector2.zero; rb[c] = maxPerp; }   // buitenkant van een knik: een cirkel om het hart die precies aan beide stoepranden raakt (vecBend)
                // de rechte stoep houdt op waar de eerste laag de cirkel raakt (de buitenkant); elke laag loopt daarvandaan recht door tot
                // zijn eigen cirkel (anders blijft er een driehoekje gras tussen de rechte en de ronde stoep)
                sA[c] = Mathf.Min(hitS(a, wA[a], cc[c], rb[c] + wA[a]), hitS(a, wS[a], cc[c], rb[c] + wS[a])); sB[c] = Mathf.Min(hitS(b, -wA[b], cc[c], rb[c] + wA[b]), hitS(b, -wS[b], cc[c], rb[c] + wS[b]));
                Vector2 h0 = P(a, wA[a], hitS(a, wA[a], cc[c], rb[c] + wA[a])) - cc[c], h1 = P(b, -wA[b], hitS(b, -wA[b], cc[c], rb[c] + wA[b])) - cc[c];
                float g0 = Mathf.Atan2(h0.y, h0.x), g1 = Mathf.Atan2(h1.y, h1.x); if (g1 <= g0 + 0.01f) g1 += Mathf.PI * 2f; nArc[c] = Mathf.Max(6, Mathf.CeilToInt((g1 - g0) / 0.15f));   // alle lagen evenveel punten
                continue;
            }
            Vector2 pa = P(a, wA[a], 0f), pb = P(b, -wA[b], 0f); float cr = Cross(dirs[a], dirs[b]);
            float t = Mathf.Abs(cr) > 0.05f ? Cross(pb - pa, dirs[b]) / cr : -1f, u = Mathf.Abs(cr) > 0.05f ? Cross(pb - pa, dirs[a]) / cr : -1f;   // snijpunt van de twee stoepranden
            if (t > 0.4f && u > 0.4f && t < 30f && u < 30f) { kind[c] = 0; sA[c] = Mathf.Max(0.4f, t - 3f); sB[c] = Mathf.Max(0.4f, u - 3f); }
            else { kind[c] = 1; sA[c] = Mathf.Max(0.4f, Vector2.Dot(-pa, dirs[a])); sB[c] = Mathf.Max(0.4f, Vector2.Dot(-pb, dirs[b])); }
            if (kind[c] == 1 && Mathf.Abs(wA[a] - wA[b]) + Mathf.Abs(wS[a] - wS[b]) > 0.01f) { sA[c] = sB[c] = 0.3f; taper = true; }   // andere breedte in elkaars verlengde: de rand loopt schuin over het hele knoopvak
        }
        // de stoephoek van hoek c in een laag (zie Lat); bij armen van verschillende breedte loopt de hoek van de ene maat naar de andere
        System.Func<int, int, Vector2[]> corner = (c, layer) =>
        {
            int a = order[c], b = order[(c + 1) % na]; float la = Lat(a, layer), lb = Lat(b, layer);
            if (kind[c] == 2)
            {
                float ra = rb[c] + la, rq = rb[c] + lb;
                Vector2 p = P(a, la, hitS(a, la, cc[c], ra)), q = P(b, -lb, hitS(b, -lb, cc[c], rq));
                float a0 = Mathf.Atan2(p.y - cc[c].y, p.x - cc[c].x), a1 = Mathf.Atan2(q.y - cc[c].y, q.x - cc[c].x); if (a1 <= a0 + 0.01f) a1 += Mathf.PI * 2f;
                int n = nArc[c]; var arc = new Vector2[n + 3];
                for (int k = 0; k <= n; k++) { float f = k / (float)n, an = Mathf.Lerp(a0, a1, f); arc[k + 1] = cc[c] + new Vector2(Mathf.Cos(an), Mathf.Sin(an)) * Mathf.Lerp(ra, rq, f); }
                arc[1] = p; arc[n + 1] = q; arc[0] = P(a, la, sA[c]); arc[n + 2] = P(b, -lb, sB[c]); return arc;   // eerst recht door vanaf het eind van de rechte stoep
            }
            Vector2 P0 = P(a, la, sA[c]), Q0 = P(b, -lb, sB[c]); var pts = new Vector2[11];
            float crs = Cross(dirs[a], dirs[b]), tt = Mathf.Abs(crs) > 0.05f ? Cross(Q0 - P0, dirs[b]) / crs : -1f, uu = Mathf.Abs(crs) > 0.05f ? Cross(Q0 - P0, dirs[a]) / crs : -1f;
            bool curve = kind[c] == 0 && tt > 0.2f && uu > 0.2f && tt < 25f && uu < 25f, crossed = kind[c] == 0 && Mathf.Abs(crs) > 0.05f && tt < 0f && uu < 0f; Vector2 X = P0 + dirs[a] * tt;
            // crossed: de twee randen kruisen elkaar al vóór de hoek (scherpe hoek, buitenkant stoep): de hoek krimpt tot dat kruispunt
            for (int k = 0; k <= 10; k++) { float f = k / 10f, g = 1f - f; pts[k] = curve ? P0 * (g * g) + X * (2f * g * f) + Q0 * (f * f) : crossed ? X : Vector2.Lerp(P0, Q0, f); }
            return pts;
        };
        // asfalt: één waaier vanuit het hart (bij een doodlopend eind vanuit het midden van de kop) langs de hele omtrek
        Vector2 hub = na == 1 ? cc[0] : Vector2.zero; var rim = new List<Vector2>();
        for (int c = 0; c < na; c++) { int a = order[c]; rim.Add(P(a, -wA[a], 0f)); rim.Add(P(a, wA[a], 0f)); rim.AddRange(corner(c, 0)); }
        for (int k = 0; k < rim.Count; k++) { Vector2 p = rim[k], q = rim[(k + 1) % rim.Count]; M("asfalt").Tri(fr.P(hub.x, AY, hub.y), fr.P(p.x, AY, p.y), fr.P(q.x, AY, q.y), fr.u); }
        // stoeprand en stoep: langs de zijkanten van elke arm tot waar de hoek begint, dan om de hoek heen
        float bestPhi = -1f; Vector2 lampAt = Vector2.zero;
        for (int c = 0; c < na; c++)
        {
            int a = order[c], b = order[(c + 1) % na];
            Use(a); SideWalk(M, fr, P, a, 1f, sA[c]); Use(b); SideWalk(M, fr, P, b, -1f, sB[c]);
            var k0 = corner(c, 0); var k1 = corner(c, 1); var k2 = corner(c, 2);
            int nk = Mathf.Min(k0.Length, Mathf.Min(k1.Length, k2.Length));
            for (int k = 0; k + 1 < nk; k++) { Kerb(M("rand"), fr, k0[k], k0[k + 1], k1[k], k1[k + 1], AY); Walk(M("stoep"), fr, k1[k], k1[k + 1], k2[k], k2[k + 1]); }
            float phi = na == 1 ? 7f : Mathf.Repeat(angOf(b) - angOf(a), Mathf.PI * 2f);
            if (phi > bestPhi) { bestPhi = phi; var lc = corner(c, 3); lampAt = lc[lc.Length / 2]; }
        }
        if (na != 2) { Cone(M("paal"), fr, lampAt.x, lampAt.y, 0.08f, SY, 5.2f, 0.06f); Cone(M("lamp"), fr, lampAt.x, lampAt.y, 0.28f, 4.9f, 5.35f, 0.2f); }   // lantaarn op de breedste hoek (kruising of kop)
        // per arm: markering, pijlen, middenberm, zebrapaden, verkeerslichten en brugleuningen
        for (int c = 0; c < na; c++)
        {
            int a = order[c], cp = (c + na - 1) % na; Vector2 e = ends[a], d = dirs[a], r = new Vector2(d.y, -d.x); byte st = stA[a]; Use(a);
            float L = Mathf.Min(sA[c], sB[cp]);   // zo ver loopt de arm recht door (tot de eerste stoephoek)
            if (na == 1) L = Vector2.Dot(cc[0] - P(a, 0f, 0f), d);
            if (na == 2 && taper) L = Vector2.Dot(-e, d);   // overgang: de markering loopt door tot het hart
            int flow = Flow(i, ids[a]);   // +1: verkeer verlaat dit vak langs deze arm, -1: komt erlangs binnen
            if (flow != 0) Arrow(M, fr, e + d * Mathf.Min(L * 0.5f, 4.5f), flow > 0 ? -d : d, 2.2f);
            else if (na <= 2 && st != 2) for (float s0 = 0.6f; s0 + 1.2f < L - 0.5f; s0 += 2.6f) { Vector2 a0 = e + d * s0, a1 = e + d * (s0 + 1.4f); M("lijn").Quad(fr.P(a0.x - r.x * 0.07f, LY, a0.y - r.y * 0.07f), fr.P(a0.x + r.x * 0.07f, LY, a0.y + r.y * 0.07f), fr.P(a1.x + r.x * 0.07f, LY, a1.y + r.y * 0.07f), fr.P(a1.x - r.x * 0.07f, LY, a1.y - r.y * 0.07f), fr.u); }
            if (st == 1 && na <= 2) for (int ln = -1; ln <= 1; ln += 2) for (float s0 = 0.6f; s0 + 1.2f < L - 0.5f; s0 += 2.6f) { Vector2 off = r * (ln * hwA * 0.5f); Vector2 a0 = e + d * s0 + off, a1 = e + d * (s0 + 1.4f) + off; M("lijn").Quad(fr.P(a0.x - r.x * 0.05f, LY, a0.y - r.y * 0.05f), fr.P(a0.x + r.x * 0.05f, LY, a0.y + r.y * 0.05f), fr.P(a1.x + r.x * 0.05f, LY, a1.y + r.y * 0.05f), fr.P(a1.x - r.x * 0.05f, LY, a1.y - r.y * 0.05f), fr.u); }
            bool medEnd = na == 2 && stA[order[(c + 1) % na]] != 2;   // een laan die in de knoop overgaat in een smallere weg: de berm houdt daar op
            if (st == 2 && (na == 1 || na >= 3 || medEnd)) Median(M, fr, e, e + d * Mathf.Max(0.2f, na >= 3 ? Mathf.Min(L - 1f, 1.2f) : L - 1f));   // op een kruising houdt de berm op bij de monding: daar slaat het verkeer af
            // zebrapaden, stopstrepen en haaientanden staan op de arm buiten de knoop (ChainMesh: daar is de weg recht, ook bij scherpe hoeken)
            if (signals && CanDrive(ids[a], i)) { Vector2 pp = PoleAt(e, d); Cone(M("paal"), fr, pp.x, pp.y, 0.08f, SY, 3.9f, 0.06f); Cone(M("paal"), fr, pp.x, pp.y, 0.24f, 3.3f, 3.95f, 0.24f); }
            if (bridgeNow) { Parapet(M, fr, P(a, -hwS, 0f), P(a, -hwS, sB[cp]), -r); Parapet(M, fr, P(a, hwS, 0f), P(a, hwS, sA[c]), r); }
        }
    }
    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    static Vector2[] Circle(Vector2 c, float rad, float a0, float a1, int n)
    {
        var p = new Vector2[n + 1]; for (int k = 0; k <= n; k++) { float a = Mathf.Lerp(a0, a1, k / (float)n); p[k] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad; }
        return p;
    }
    // stoeprand en stoep langs één zijkant (side +1: naar de volgende arm, -1: naar de vorige) van arm a, van de vakrand tot s1
    static void SideWalk(System.Func<string, MeshBuilder> M, Frame fr, System.Func<int, float, float, Vector2> P, int a, float side, float s1)
    {
        for (float s0 = 0f; s0 < s1 - 1e-3f; s0 += 0.5f)
        {
            float s2 = Mathf.Min(s1, s0 + 0.5f);
            Kerb(M("rand"), fr, P(a, side * hwA, s0), P(a, side * hwA, s2), P(a, side * (hwA + KW), s0), P(a, side * (hwA + KW), s2), AY);
            Walk(M("stoep"), fr, P(a, side * (hwA + KW), s0), P(a, side * (hwA + KW), s2), P(a, side * hwS, s0), P(a, side * hwS, s2));
        }
    }

    // Een meertje: zand aan de kant, water iets lager, riet langs de oever. Grenst een buurvak ook aan een meer,
    // dan loopt het water door tot de gedeelde rand, zodat meerdere vakken één meer vormen.
    static void Lake(Dictionary<string, MeshBuilder> mb, Frame fr, int i, float h, int hash)
    {
        System.Func<string, MeshBuilder> M = k => { if (!mb.TryGetValue(k, out var x)) { x = new MeshBuilder(); mb[k] = x; } return x; };
        var c = Planet.Cells[i]; int n = fr.poly.Length;
        // per rand: open naar een buurmeer? De buur met dezelfde richting als de rand (middelpunt van de rand ligt richting die buur)
        var open = new bool[n];
        for (int e = 0; e < n; e++)
        {
            Vector2 mid = (fr.poly[e] + fr.poly[(e + 1) % n]) * 0.5f; int best = -1; float bd = -1f;
            foreach (int j in c.nb) { Vector3 d3 = Planet.Cells[j].dir * Planet.R - fr.o; Vector2 q = new Vector2(Vector3.Dot(d3, fr.e), Vector3.Dot(d3, fr.n)).normalized; float d = Vector2.Dot(q, mid.normalized); if (d > bd) { bd = d; best = j; } }
            open[e] = best >= 0 && Cells.TryGetValue(best, out var l) && l.Count > 0 && l[0] == "meer";
        }
        // waterrand: hoekpunt op de rand als beide aangrenzende randen open zijn, anders naar binnen (golvend)
        var wp = new Vector2[n]; var rnd = new System.Random(hash);
        for (int k = 0; k < n; k++)
        {
            float kk = (open[(k + n - 1) % n] || open[k]) ? 1.0f : 0.68f + (float)rnd.NextDouble() * 0.1f;   // naast een open rand precies op het hoekpunt (anders blijft er een driehoekje open tussen de buurmeren)
            wp[k] = fr.poly[k] * kk;
        }
        float wy = h - 0.35f;
        for (int k = 0; k < n; k++)
        {
            Vector2 a = wp[k], b = wp[(k + 1) % n], am = open[k] ? (fr.poly[k] + fr.poly[(k + 1) % n]) * 0.5f : (a + b) * 0.5f * 0.95f;
            M("water").Tri(fr.P(0, wy, 0), fr.P(a.x, wy, a.y), fr.P(am.x, wy, am.y), fr.u); M("water").Tri(fr.P(0, wy, 0), fr.P(am.x, wy, am.y), fr.P(b.x, wy, b.y), fr.u);
            // oever: een helling van het zand (h+0.03) naar onder water (wy-0.3), en riet op gesloten randen
            Vector2 oa = fr.poly[k] * 0.98f, ob = fr.poly[(k + 1) % n] * 0.98f;
            if (!open[k]) {
                // buitenrand (zand, iets de bodem in) en de oever: van de rand schuin omlaag tot onder water
                Vector2 pa = fr.poly[k], pb = fr.poly[(k + 1) % n], pm = (pa + pb) * 0.5f;
                M("zand").Quad(fr.P(pa.x, -0.25f, pa.y), fr.P(pb.x, -0.25f, pb.y), fr.P(pb.x, h + 0.03f, pb.y), fr.P(pa.x, h + 0.03f, pa.y), fr.D(pm.x, 0, pm.y));
                M("zand").Quad(fr.P(pa.x, h + 0.03f, pa.y), fr.P(pb.x, h + 0.03f, pb.y), fr.P(ob.x, h + 0.02f, ob.y), fr.P(oa.x, h + 0.02f, oa.y), fr.u);
                M("zand").Quad(fr.P(oa.x, h + 0.02f, oa.y), fr.P(ob.x, h + 0.02f, ob.y), fr.P(b.x, wy - 0.3f, b.y), fr.P(a.x, wy - 0.3f, a.y), fr.D(-am.x, 0.5f * am.magnitude, -am.y));
                int reeds = 2 + rnd.Next(0, 3);
                for (int r = 0; r < reeds; r++) { float t = 0.15f + (float)rnd.NextDouble() * 0.7f; Vector2 rp = Vector2.Lerp(a, b, t) * (0.97f + (float)rnd.NextDouble() * 0.06f); Cone(M("riet"), fr, rp.x, rp.y, 0.12f, wy - 0.1f, h + 0.9f + (float)rnd.NextDouble() * 0.5f, 0f); } }
        }
    }

    static Color ColorOf(string k)
    {
        switch (k)
        {
            case "muur": case "muur0": return new Color(0.93f, 0.86f, 0.72f); case "muur1": return new Color(0.72f, 0.42f, 0.32f); case "muur2": return new Color(0.88f, 0.88f, 0.84f); case "muur3": return new Color(0.55f, 0.5f, 0.45f);
            case "dak": case "dak0": return new Color(0.65f, 0.30f, 0.22f); case "dak1": return new Color(0.35f, 0.33f, 0.36f);
            case "paal": return new Color(0.25f, 0.27f, 0.3f); case "rand": return new Color(0.7f, 0.68f, 0.62f); case "lamp": return new Color(0.85f, 0.85f, 0.8f);
            case "kantoor": return new Color(0.58f, 0.68f, 0.78f); case "glas": return new Color(0.20f, 0.30f, 0.40f);
            case "plat": return new Color(0.50f, 0.50f, 0.53f); case "gras": return new Color(0.52f, 0.74f, 0.33f);
            case "bodem0": return new Color(0.64f, 0.78f, 0.45f);   // hetzelfde materiaal als het gewone terrein (p_bodem0), voor de bovenkant van een brugblok
            case "water": return new Color(0.20f, 0.50f, 0.78f); case "zand": return new Color(0.86f, 0.79f, 0.55f);
            case "pad": return new Color(0.82f, 0.76f, 0.63f); case "fietspad": return new Color(0.62f, 0.27f, 0.22f); case "blad": return new Color(0.20f, 0.50f, 0.22f);
            case "steen": return new Color(0.62f, 0.60f, 0.58f); case "tunnel": return new Color(0.30f, 0.30f, 0.31f); case "stoep": return new Color(0.88f, 0.84f, 0.72f);
            case "asfalt": return new Color(0.27f, 0.28f, 0.30f); case "lijn": return Color.white;
            case "modder": return new Color(0.45f, 0.38f, 0.26f); case "riet": return new Color(0.45f, 0.6f, 0.25f);
            case "etalage": return new Color(0.55f, 0.68f, 0.75f); case "doek": return new Color(0.95f, 0.93f, 0.88f);
            case "luifel0": return new Color(0.78f, 0.2f, 0.18f); case "luifel1": return new Color(0.2f, 0.5f, 0.32f); case "luifel2": return new Color(0.18f, 0.35f, 0.62f);
            case "bord": return new Color(0.95f, 0.70f, 0.23f); case "klinker": return new Color(0.70f, 0.55f, 0.45f); case "hout": return new Color(0.52f, 0.36f, 0.22f);
            case "baksteen": return new Color(0.62f, 0.33f, 0.25f); case "leien": return new Color(0.24f, 0.27f, 0.32f); case "klok": return new Color(0.95f, 0.95f, 0.9f); case "goud": return new Color(0.9f, 0.72f, 0.25f);
            case "molen": return new Color(0.30f, 0.30f, 0.28f); case "kap": return new Color(0.42f, 0.36f, 0.26f); case "wiek": return new Color(0.35f, 0.22f, 0.15f); case "zeil": return new Color(0.92f, 0.9f, 0.84f);
            default: return NatureColor(k, out var nc) ? nc : new Color(0.40f, 0.28f, 0.18f);
        }
    }

    // Rebuild in stappen (voor een laadscherm): een paar delen per beeld, prog(deel 0..1)
    public static System.Collections.IEnumerator RebuildSteps(System.Action<float> prog)
    {
        UpdateCuts(); var todo = new List<int>(dirty); dirty.Clear(); gridV = null;   // alles nieuw: het raster ook helemaal
        for (int k = 0; k < todo.Count; k++) { BuildChunk(todo[k]); if (k % 6 == 5) { prog?.Invoke((k + 1f) / todo.Count); yield return null; } }
        foreach (int ch in new List<int>(dirty)) BuildChunk(ch); dirty.Clear();   // wat er intussen nog bij kwam
        RebuildRoads();
    }
    // Rebuild in stukjes voor een klik: eerst het deel waar het gebeurde (Focus) en de wegen, de rest met een tijdbudget per beeld;
    // zo staat wat je bouwt meteen in beeld. true als alles bij is. Bij een nieuwe wijziging opnieuw beginnen (RebuildSoon).
    public static int Focus = -1; static bool midRebuild;
    public static void RebuildSoon(int focus) { Focus = focus; midRebuild = false; }
    public static bool RebuildSome(float budgetMs)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew(); bool first = !midRebuild;
        if (first) { UpdateCuts(); if (dirty.Count == 0) return true; midRebuild = true; }
        var order = new List<int>(dirty); int fc = Focus >= 0 ? Focus / ChunkN : 0; order.Sort((a, b) => Mathf.Abs(a - fc).CompareTo(Mathf.Abs(b - fc)));   // het deel van de klik eerst
        foreach (int ch in order) { BuildChunk(ch); lastBuilt.Add(ch); dirty.Remove(ch); if (sw.ElapsedMilliseconds >= budgetMs) break; }
        if (first) RebuildRoads();
        if (dirty.Count > 0) return false;
        midRebuild = false; return true;
    }
    public static void Rebuild()
    {
        midRebuild = false;
        UpdateCuts();
        if (dirty.Count == 0) return;
        foreach (int ch in dirty) { BuildChunk(ch); lastBuilt.Add(ch); }
        dirty.Clear();
        RebuildRoads();   // het wegennet: alleen de lijnen en knopen die veranderd zijn
    }

    // ---------- bouwvoorbeeld (het spookblok van de webversie) ----------
    // Kan blok 'type' op vak i? null = ja, anders de reden. Dezelfde regels als Place (voor blokken, niet voor wegen), zonder iets te veranderen.
    public static string CanPlace(int i, string type)
    {
        if (type == "rotonde") return RotOf(i) >= 0 ? null : RotFree(i) ? null : "Hier staat iets in de weg";
        if (RotOf(i) >= 0 && !IsRot(i)) return "Hier ligt een rotonde: op de ring kun je niet bouwen";
        if (!Cells.TryGetValue(i, out var l) || l.Count == 0) return null;
        if (type == "pad" && l.Count == 1 && l[0] == "pad") return null;
        if (l[0] == "weg") return "Op een weg kun je niet bouwen; haal de weg eerst weg (rechtermuisknop)";
        if (l[0] == "wegdeel" && !Buildable(type)) return "Hier loopt een weg overheen: op de vrije helft passen alleen gebouwen en parken";
        if (type == "water" || type == "meer") return null;   // water vervangt wat er staat
        if (l[l.Count - 1] == "water" || l[l.Count - 1] == "meer") return null;
        if (l.Count >= 8) return "Hoger kan niet";
        if (l[l.Count - 1] == "toren" || l[l.Count - 1] == "molen") return "Op een " + l[l.Count - 1] + " kun je niets meer zetten";
        return NoStackOn(l[l.Count - 1]);
    }
    // op wegen, paden, bomen (park) en daken kun je niets stapelen; null = wel
    static string NoStackOn(string top) => top == "park" ? "Op bomen kun je niets zetten" : top == "dak" ? "Op een dak kun je niets zetten" : top == "pad" ? "Op een pad kun je niet bouwen" : null;
    // De vorm van wat Place(i, type) zou toevoegen, of bij de gum de hele stapel, als één mesh rond het hart van het vak (origin).
    // null als er niets te tonen is (een wegvak: dat tekent het wegennet).
    public static Mesh GhostMesh(int i, string type, bool erase, out Vector3 origin)
    {
        origin = Planet.Cells[i].dir * (Planet.R + G(i));
        if (type == "rotonde" && !erase) { var rmb = new Dictionary<string, MeshBuilder>(); var ra = new List<int>(); foreach (int j in Planet.Cells[i].nb) if (IsRoad(j)) ra.Add(j); RoundaboutMesh(rmb, i, ra); return MergeMesh(rmb, origin); }
        Cells.TryGetValue(i, out var cur); var list = cur != null ? new List<string>(cur) : new List<string>(); int from;
        if (erase) { if (list.Count == 0 || list[0] == "weg") return null; from = 0; }
        else
        {
            if (list.Count > 0 && list[0] == "weg") return null;
            if (type == "water" || type == "meer" || (list.Count > 0 && (list[list.Count - 1] == "water" || list[list.Count - 1] == "meer"))) { bool half = list.Count > 0 && list[0] == "wegdeel"; list.Clear(); if (half) list.Add("wegdeel"); }
            from = list.Count; list.Add(type);
        }
        var mb = new Dictionary<string, MeshBuilder>(); BuildCell(mb, i, list, new List<(Vector3, Quaternion, int)>(), from);
        return MergeMesh(mb, origin);
    }
    // alle stukken van een tekening samen tot één mesh rond origin (voor het bouwvoorbeeld, dat één doorzichtige kleur heeft)
    static Mesh MergeMesh(Dictionary<string, MeshBuilder> mb, Vector3 origin)
    {
        var all = new MeshBuilder();
        foreach (var b in mb.Values) { int o = all.v.Count; foreach (var p in b.v) all.v.Add(p - origin); all.n.AddRange(b.n); foreach (int t in b.t) all.t.Add(o + t); }
        return all.v.Count == 0 ? null : all.Build();
    }

    // Eén vak tekenen: de stapel blokken (vanaf blok 'from'; het bouwvoorbeeld tekent alleen het nieuwe blok bovenop)
    static void BuildCell(Dictionary<string, MeshBuilder> mb, int i, List<string> list, List<(Vector3 pos, Quaternion rot, int hash)> mills, int from)
    {
        System.Func<string, MeshBuilder> M = k => { if (!mb.TryGetValue(k, out var x)) { x = new MeshBuilder(); mb[k] = x; } return x; };
        var fr = new Frame(i);
            if (list[0] == "weg") { if (Under.ContainsKey(i)) { UnderMesh(mb, fr, i, i * 73856093); BridgePillar(M, fr, i); } return; }   // de weg zelf tekent het wegennet (PlanetRoads.cs); hier alleen wat onder een brug ligt
            if (list[0] == "wegdeel")   // half vak naast een schuine weg: de helft naar de weg toe is gewoon de grond (geen plaat erop); wat erop gebouwd is staat op de vrije helft
            {
                var rest = list.GetRange(1, list.Count - 1);
                if (InCut(i)) { if (rest.Count == 0) return; }   // in de geul van een onderdoorgang: niets
                else if (!HalfNormal(i, out var hn)) { if (rest.Count == 0) return; }   // zonder bekende weg: gewoon een vak, de grond ligt er al
                else
                {
                    var hd = HalfDirs(i, hn); var lp = new List<Vector3>();
                    foreach (var d in hd) { Vector3 w = d * Planet.R - fr.o; lp.Add(new Vector3(Vector3.Dot(w, fr.e), 0f, Vector3.Dot(w, fr.n))); }
                    if (rest.Count == 0) return;
                    var fd = HalfDirs(i, hn, true);
                    if (fd.Count >= 3)
                    {
                        Vector3 cen = Vector3.zero; foreach (var d in fd) cen += d; cen = (cen / fd.Count).normalized; Vector3 o2 = cen * (Planet.R + G(i));
                        var pl = new Vector2[fd.Count]; float rs = 0f;
                        for (int k2 = 0; k2 < fd.Count; k2++) { Vector3 w = fd[k2] * Planet.R - o2; pl[k2] = new Vector2(Vector3.Dot(w, fr.e), Vector3.Dot(w, fr.n)) * 0.86f; rs += pl[k2].magnitude; }
                        fr = new Frame(fr, o2, pl, rs / fd.Count);
                    }
                }
                list = rest; from = Mathf.Max(0, from - 1);
            }
            if (list.Count == 1 && list[0] == "meer") return;   // een meer is diep terrein onder het zeeniveau (PlanetNature.cs)
            if (list.Count == 1 && list[0] == "pad") return;   // paden tekent het wegennet (PlanetPaths.cs)
            float h = 0f; int hash = i * 73856093;
            for (int k = 0; k < list.Count; k++)
            {
                string t = list[k]; bool top = k == list.Count - 1; float h1 = h + Blocks.Height(t), h0 = k == 0 ? -0.2f : h;   // onderste blok iets de bodem in (de bol loopt af naar de hoeken)
                if (k < from) { h = h1; continue; }   // bouwvoorbeeld: alleen het nieuwe blok bovenop
                switch (t)
                {
                    case "woning":
                        { string mu = "muur" + ((hash >> 3) & 3), dk = "dak" + ((hash >> 7) & 1);
                        Wall(M(mu), fr, 0.78f, h0, h1); Windows(M("glas"), fr, 0.78f, h + 1.0f, h + 2.0f, 0.18f, 0.40f); Windows(M("glas"), fr, 0.78f, h + 1.0f, h + 2.0f, 0.60f, 0.82f);
                        if (top) { Roof(M(dk), fr, 0.84f, h1); Top(M(dk), fr, 0.84f, h1 - 0.02f); } else Top(M(mu), fr, 0.78f, h1); }
                        break;
                    case "kantoor":
                        Wall(M("kantoor"), fr, 0.9f, h0, h1); Windows(M("glas"), fr, 0.9f, h + 0.5f, h + 1.2f, 0.08f, 0.92f); Windows(M("glas"), fr, 0.9f, h + 1.7f, h + 2.4f, 0.08f, 0.92f);
                        Top(M("plat"), fr, 0.9f, h1); if (top) { Wall(M("plat"), fr, 0.9f, h1, h1 + 0.25f); Top(M("plat"), fr, 0.9f, h1 + 0.25f); }
                        break;
                    case "dak":
                        Cone(M("dak"), fr, 0, 0, fr.rad * 0.95f, h, h1, fr.rad * 0.5f); Top(M("dak"), fr, 0.5f, h1);
                        break;
                    case "park":
                        if (k > 0) Top(M("gras"), fr, 0.98f, h + 0.05f);   // daktuin; op de grond staan de bomen los op het terrein
                        Trees(M, fr, i, hash, h + 0.05f, k > 0);   // PlanetNature.cs
                        break;
                    case "water":
                        Canal(M, i); break;   // gracht: bodem en kademuren, het water komt van het zeeniveau (PlanetNature.cs)
                    case "meer":
                        break;
                    case "kuil":   // uitgraving: de bodem 3 m lager, zeshoekige grashellingen van de rand naar beneden
                        for (int e2 = 0; e2 < fr.poly.Length; e2++)
                        {
                            Vector2 a2 = C(fr, e2, 1.0f), b2 = C(fr, e2 + 1, 1.0f), a3 = C(fr, e2, 0.55f), b3 = C(fr, e2 + 1, 0.55f), m2 = (a2 + b2) * 0.5f;
                            M("gras").Quad(fr.P(a2.x, h, a2.y), fr.P(b2.x, h, b2.y), fr.P(b3.x, h1, b3.y), fr.P(a3.x, h1, a3.y), fr.D(-m2.x, 2f, -m2.y));
                        }
                        Top(M("gras"), fr, 0.55f, h1);
                        break;
                    case "pad":
                        Wall(M("pad"), fr, 0.72f, h0, h1); Top(M("pad"), fr, 0.72f, h1);
                        break;
                    case "heuvel":
                        Cone(M("gras"), fr, 0, 0, fr.rad * 1.0f, h0, h1, fr.rad * 0.55f); Cap6(M("gras"), fr, fr.rad * 0.55f, h1);
                        break;
                    case "blok":
                        Wall(M("steen"), fr, 0.96f, h0, h1); Top(M("steen"), fr, 0.96f, h1);
                        break;
                    case "winkel":   // winkel: grote etalages (lichten 's avonds op), een gestreepte luifel en een naambord; erboven kan gewoon gewoond worden
                        { string mu = "muur" + ((hash >> 3) & 3);
                        Wall(M(mu), fr, 0.86f, h0, h1); Windows(M("etalage"), fr, 0.86f, h + 0.3f, h + 2.0f, 0.14f, 0.86f); Windows(M("bord"), fr, 0.86f, h + 2.45f, h + 2.85f, 0.3f, 0.7f);
                        Awning(M, fr, 0.87f, h + 2.35f, "luifel" + ((hash >> 5) & 0xffff) % 3);
                        Top(M(top ? "plat" : mu), fr, 0.86f, h1); if (top) { Wall(M("plat"), fr, 0.86f, h1, h1 + 0.3f); Top(M("plat"), fr, 0.86f, h1 + 0.3f); } }
                        break;
                    case "plein":   // plein: klinkers, een fontein in het midden, bankjes eromheen en twee lantaarns
                        { Top(M("klinker"), fr, 1.0f, h1); Wall(M("klinker"), fr, 1.0f, h0, h1); Top(M("steen"), fr, 0.62f, h1 + 0.005f); Top(M("klinker"), fr, 0.5f, h1 + 0.01f);
                        if (k > 0 || FountainHere(i)) { Skirt(M("steen"), fr, 2.4f, h1, h1 + 0.55f); Disc(M("steen"), fr, 2.4f, h1 + 0.55f); Disc(M("water"), fr, 2.1f, h1 + 0.57f);
                        Cone(M("steen"), fr, 0f, 0f, 0.3f, h1 + 0.55f, h1 + 1.6f, 0.22f); Cone(M("steen"), fr, 0f, 0f, 0.25f, h1 + 1.45f, h1 + 1.75f, 1.0f); Disc(M("water"), fr, 0.9f, h1 + 1.76f); }
                        float a0 = (hash & 63) / 63f * Mathf.PI * 2f;
                        for (int j = 0; j < 3; j++)
                        {
                            float a = a0 + j * Mathf.PI * 2f / 3f, rr = fr.rad * 0.6f, bx = Mathf.Cos(a) * rr, bz = Mathf.Sin(a) * rr, tang = a + Mathf.PI / 2f;
                            Box(M("hout"), fr, bx, bz, tang, 0.9f, 0.24f, h1 + 0.42f, h1 + 0.5f);
                            Box(M("hout"), fr, bx + Mathf.Cos(a) * 0.24f, bz + Mathf.Sin(a) * 0.24f, tang, 0.9f, 0.04f, h1 + 0.5f, h1 + 0.95f);
                            for (int s2 = -1; s2 <= 1; s2 += 2) Box(M("paal"), fr, bx + Mathf.Cos(tang) * 0.75f * s2, bz + Mathf.Sin(tang) * 0.75f * s2, tang, 0.05f, 0.2f, h1, h1 + 0.42f);
                            if (j < 2) { float la = a + Mathf.PI / 3f, lx = Mathf.Cos(la) * fr.rad * 0.78f, lz = Mathf.Sin(la) * fr.rad * 0.78f; Cone(M("paal"), fr, lx, lz, 0.08f, h1, h1 + 4.2f, 0.06f); Cone(M("lamp"), fr, lx, lz, 0.28f, h1 + 3.9f, h1 + 4.45f, 0f); }
                        } }
                        break;
                    case "toren":   // kerktoren: stenen voet, bakstenen schacht met klok en galmgaten, leien spits met een gouden knop
                        Wall(M("steen"), fr, 0.55f, h0, h + 2.2f); Top(M("steen"), fr, 0.55f, h + 2.2f);
                        Wall(M("baksteen"), fr, 0.4f, h + 2.2f, h1 - 0.3f); Windows(M("klok"), fr, 0.4f, h1 - 4.6f, h1 - 3.5f, 0.3f, 0.7f); Windows(M("glas"), fr, 0.4f, h1 - 2.5f, h1 - 1.0f, 0.36f, 0.64f);
                        Wall(M("steen"), fr, 0.45f, h1 - 0.3f, h1); Top(M("steen"), fr, 0.45f, h1);
                        if (top) { Spire(M("leien"), fr, 0.42f, h1, 8f); Cone(M("goud"), fr, 0f, 0f, 0.14f, h1 + 7.6f, h1 + 8.6f, 0f); }
                        break;
                    case "molen":   // molen: taps toelopende romp, een kap en vier draaiende wieken, met het gezicht naar een willekeurige kant
                        { float r0 = fr.rad * 0.5f, r1 = fr.rad * 0.3f, a = ((hash >> 4) & 7) / 8f * Mathf.PI * 2f; Vector2 fd = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        Cone(M("steen"), fr, 0f, 0f, r0 * 1.06f, h0, h + 0.7f, r0 * 1.04f);
                        Cone(M("molen"), fr, 0f, 0f, r0, h + 0.7f, h1 - 1.6f, r1); Cone(M("kap"), fr, 0f, 0f, r1 * 1.18f, h1 - 1.6f, h1 + 0.8f, 0.2f);
                        Box(M("hout"), fr, fd.x * (r0 - 0.1f), fd.y * (r0 - 0.1f), a, 0.35f, 0.6f, h + 0.7f, h + 2.6f);   // deur
                        Box(M("wiek"), fr, fd.x * (r1 + r0 + 0.5f) * 0.5f, fd.y * (r1 + r0 + 0.5f) * 0.5f, a, (r0 + 0.5f - r1) * 0.5f + 0.5f, 0.15f, h1 - 1.15f, h1 - 0.85f);   // as
                        float hub = r0 + 0.5f; mills.Add((fr.P(fd.x * hub, h1 - 1.0f, fd.y * hub), Geo.Look(fr.D(fd.x, 0f, fd.y), fr.u), hash)); }
                        break;
                }
                h = h1;
            }
    }

    static void BuildChunk(int ch)
    {
        BuildGround(ch);
        if (chunks.TryGetValue(ch, out var old) && old != null) UnityEngine.Object.Destroy(old.gameObject);
        chunks.Remove(ch);
        var mb = new Dictionary<string, MeshBuilder>();
        System.Func<string, MeshBuilder> M = k => { if (!mb.TryGetValue(k, out var x)) { x = new MeshBuilder(); mb[k] = x; } return x; };
        int i0 = ch * ChunkN, i1 = Mathf.Min(Planet.Cells.Length, i0 + ChunkN);
        var mills = new List<(Vector3 pos, Quaternion rot, int hash)>();   // molenwieken: losse objecten die draaien
        try
        {
            for (int i = i0; i < i1; i++) if (Cells.TryGetValue(i, out var list) && list.Count > 0)
            {
                if (fieldCells.Contains(i)) UseNear(i); else UseAll();   // alleen de wegen in de buurt (een brug meet het terrein op zo'n honderd punten)
                BuildCell(mb, i, list, mills, 0);
            }
        }
        finally { UseAll(); }
        if (mb.Count == 0) return;
        var go = new GameObject("Deel " + ch); go.transform.SetParent(root, false); chunks[ch] = go.transform;
        foreach (var kv in mb)
        {
            if (kv.Value.v.Count == 0) continue;
            var child = new GameObject(kv.Key); child.transform.SetParent(go.transform, false);
            child.layer = (kv.Key == "asfalt" || kv.Key == "stoep" || kv.Key == "rand" || kv.Key == "lijn" || kv.Key == "paal" || kv.Key == "lamp" || kv.Key == "pad") ? Layers.Weg : Layers.Gebouw;
            bool wet = kv.Key == "water" || kv.Key == "zand"; if (wet) child.layer = Layers.Water;   // een meer heeft geen bodem: het water en de oever zijn om aan te wijzen (weghalen), niet om op te lopen
            var mesh = kv.Value.Build(); child.AddComponent<MeshFilter>().sharedMesh = mesh; child.AddComponent<MeshRenderer>().sharedMaterial = kv.Key == "lamp" || kv.Key == "etalage" ? Mats.GetUnlit("p_" + kv.Key, ColorOf(kv.Key)) : Mats.Get("p_" + kv.Key, ColorOf(kv.Key));
            if (wet || kv.Key != "lijn" && kv.Key != "lamp" && kv.Key != "riet" && kv.Key != "doek" && !kv.Key.StartsWith("luifel")) child.AddComponent<MeshCollider>().sharedMesh = mesh;
        }
        foreach (var ml in mills) MakeWieken(go.transform, ml.pos, ml.rot, ml.hash);
    }
}

// Draaiende molenwieken: harder bij storm, wat sneller bij regen, en een beetje onregelmatig zoals echte wind
public class Wieken : MonoBehaviour
{
    public float Speed = 40f; float gust;
    void Update()
    {
        gust = Mathf.Lerp(gust, Mathf.PerlinNoise(Time.time * 0.15f, Speed) - 0.5f, Time.deltaTime);
        float s = Speed * (1f + gust * 0.6f + Weather.Storm * 1.6f + Weather.Rain * 0.3f);
        transform.Rotate(0f, 0f, -s * Time.deltaTime, Space.Self);
    }
}
