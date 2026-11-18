using System.Collections.Generic;
using UnityEngine;

// Grond bij wegen, nieuwe opzet (v3, "stroken"). De oude opzet sneed de grond langs oneindige lijnen van elk wegstuk (wegrand, voet
// van het talud, einden, kegels) in precies vlakke stukjes; waar die lijnen vlak naast elkaar liepen ontstonden snippers van een
// paar centimeter en bundels naalddunne driehoeken. Hier:
//  - De hoogte (GroundV3): onder de weg ligt de grond net onder het wegdek; ernaast gaat hij over een vaste breedte (G3Min, of zo
//    breed als het talud nodig heeft) gelijkmatig over in het terrein. De afstand is die tot het dichtstbijzijnde punt van de wegen in
//    de buurt, dus een bocht, een eind en een kruispunt gaan vanzelf goed.
//  - De verdeling (V3Polys): elk wegstuk krijgt zijn eigen strook van het vak, begrensd door de deellijnen met de stukken waar het
//    aan vastzit (recht: een dwarslijn op het hart van elk wegvak). Binnen die strook lopen rijen evenwijdig aan de weg, om de G3Row
//    meter, en één dwarslijn halverwege. Buiten de stroken blijft het vak zoals het was.
// Geldt voor vakken waar alleen gewone wegstukken in de buurt liggen (V3Cell): geen keermuur, bak, tunnelinrit, weg op een niveau of
// geul. Daar blijft de oude opzet; de rand tussen twee vakken is in beide opzetten dezelfde gedeelde lijn (EdgeProfile), dus het
// sluit altijd.
public static partial class PlanetCity
{
    public static bool Ground3 = true;   // standaard aan; uit met de ontwikkelaarsschakelaar (Instellingen › Ontwikkelaar › Schone grond bij wegen), bij elke start van Play weer aan
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Ground3Default() { Ground3 = true; fieldV3 = false; v3Cache.Clear(); v3WallCache.Clear(); }
    public static void SetGround3(bool on) { Ground3 = on; v3Cache.Clear(); v3WallCache.Clear(); Ground2CacheClear(); nearCache.Clear(); UpdateCuts(); }   // (het bereik van elk stuk verandert mee: de veldvakken opnieuw)

    // G3Min: de breedte van de overgang naast de weg (ongeveer een half vak: dan volgt de grond tussen de zeshoekpunten hem vanzelf)
    // G3Rows: aantal rijen evenwijdig aan de weg in de overgang. 0 (de keuze van 17 november 2026): alleen de wegrand is een snede, geen
    // rijen, dwarslijnen of extra verdeling bij bochten en einden; de grond loopt van de zeshoekpunten glooiend naar de weg.
    public static bool G3Half = true;   // ook halve wegvakken naast een schuine weg met de nieuwe opzet (uit: de oude opzet daar, om te vergelijken)
    const float G3Row = 2f, G3Min = 8f; const int G3MaxRows = 10; public static int G3Rows = 0;
    static bool fieldV3;   // GroundField rekent met de nieuwe hoogte (gezet door wie het vak kiest: Ground2Cell0, H, EdgeProfile1)
    static readonly Dictionary<int, (int ver, bool v)> v3Cache = new Dictionary<int, (int, bool)>();

    // Kan vak i met de nieuwe opzet: een veldvak zonder plateau waar alleen gewone wegstukken en koppen in de buurt liggen
    static bool V3Cell(int i)
    {
        if (!Ground3 || i < 0 || !fieldCells.Contains(i)) return false;
        if (v3Cache.TryGetValue(i, out var e) && e.ver == fieldVer) return e.v;
        var su = segsUse; var fu = flatsUse; var cu = capsUse; bool ok;
        try
        {
            UseNear(i); var c = Planet.Cells[i];
            ok = !Plateau(i) && !FlatGround(i) && (G3Half || !IsHalf(i)) && NearSeg(c.dir, c.radius) && flatsUse.Count == 0 && (segsUse.Count > 0 || capsUse.Count > 0);
            if (ok) foreach (var q in segsUse) if (q.bak) { ok = false; break; }   // (een stuk met een keermuur telt hier als gewoon stuk: de muur staat strak langs de wegrand, V3WallLins)
            if (ok && cuts.Count > 0) { var near = CutsNear(c); if (near != null && near.Count > 0) ok = false; }
            if (ok && Underpass(i)) ok = false;   // (boven een tunnel of onder een brug mag: de mantel van de tunnel snijdt BoreTri eruit, en onder een dek blijft het terrein liggen)   // (een wegvak dat meer dan een half niveau van het terrein af ligt telt als 'op een niveau', maar is hier een gewoon stuk zolang het geen wand heeft)
        }
        finally { segsUse = su; flatsUse = fu; capsUse = cu; }
        v3Cache[i] = (fieldVer, ok); return ok;
    }

    // voor de test: waarom vak i niet met de nieuwe opzet kan
    public static string V3Why(int i)
    {
        if (!Ground3) return "schakelaar uit"; if (!fieldCells.Contains(i)) return "geen veldvak"; var su = segsUse; var fu = flatsUse; var cu = capsUse;
        try
        {
            UseNear(i); var c = Planet.Cells[i]; if (Plateau(i)) return "plateau"; if (IsHalf(i) && !G3Half) return "half wegvak"; if (FlatGround(i)) return "vlak"; if (!NearSeg(c.dir, c.radius)) return "buiten bereik"; if (flatsUse.Count > 0) return "vlakke wegstukken " + flatsUse.Count;
            foreach (var q in segsUse) if (q.bak) return "stuk " + q.ia + "-" + q.ib + " bak";
            if (cuts.Count > 0) { var nr = CutsNear(c); if (nr != null && nr.Count > 0) return "geul"; } if (Underpass(i)) return "onderdoorgang";
            return "ja";
        }
        finally { segsUse = su; flatsUse = fu; capsUse = cu; }
    }
    // De hoogte van de grond op punt dir bij gewone wegen (segsUse, capsUse), met het terrein base daar.
    static readonly List<float> g3D = new List<float>(), g3H = new List<float>();
    static float GroundV3(Vector3 dir, float base_, out bool slope)
    {
        slope = false; Vector3 w = dir * Planet.R; g3D.Clear(); g3H.Clear(); float dmin = float.MaxValue, inH = float.MaxValue; bool wallNear = false;
        foreach (var q in segsUse)
        {
            Vector3 ab = q.b - q.a; float l2 = ab.sqrMagnitude; if (l2 < 1e-4f) continue; float ur = Vector3.Dot(w - q.a, ab) / l2, u = Mathf.Clamp01(ur);
            if ((q.hardA || q.hardB) && ((q.hardA && ur * Mathf.Sqrt(l2) < -wallBias) || (q.hardB && (ur - 1f) * Mathf.Sqrt(l2) > wallBias)))
            {   // voorbij een hard eind (een tunnelportaal): de berg blijft staan; alleen onder de weg die daar de tunnel in duikt telt zijn hoogte mee
                // (binnen de zeshoek van een kruispunt lag de grond anders boven de afdalende weg)
                Vector3 dx = w - (q.a + ab * ur); dx -= dir * Vector3.Dot(dx, dir); float over = (ur < 0f ? -ur : ur - 1f) * Mathf.Sqrt(l2);
                if (dx.magnitude <= q.hw && over < 14f) { float hx = Mathf.LerpUnclamped(q.ha, q.hb, ur); if (hx < inH) inH = hx; }
                continue;
            }
            Vector3 dv = w - (q.a + ab * u); if (dv.sqrMagnitude > 60f * 60f) continue; dv -= dir * Vector3.Dot(dv, dir);
            float d = dv.magnitude - q.hw, hr = Mathf.Lerp(q.ha, q.hb, u); if (d > 60f) continue;
            g3D.Add(d); g3H.Add(hr); if (d < dmin) { dmin = d; wallNear = q.wall; } if (d <= wallBias && hr < inH) inH = hr;
        }
        foreach (var q in capsUse)
        {
            Vector3 dv = w - q.c; if (dv.sqrMagnitude > 60f * 60f) continue; float hr = q.h + Vector3.Dot(dv, q.g); dv -= dir * Vector3.Dot(dv, dir);
            float d = CapDist(q, w) - q.hw; if (d > 60f) continue;
            g3D.Add(d); g3H.Add(hr); if (d < dmin) { dmin = d; wallNear = q.wall; } if (d <= wallBias && hr < inH) inH = hr;
        }
        if (g3D.Count == 0) return base_;
        float road;
        if (dmin <= wallBias) road = inH;   // op de weg (wallBias: een punt precies op de wegrand hoort bij de weg of bij het terrein, naar wie het vraagt): waar twee wegen over elkaar liggen wint de laagste
        else
        {   // ernaast: de weghoogte van het dichtstbijzijnde punt, vloeiend gemengd met wat bijna even dichtbij ligt (anders een sprong op de deellijn tussen twee armen)
            float sw = 0f, sh = 0f;
            for (int x = 0; x < g3D.Count; x++) { float k = 1f - (g3D[x] - dmin) / 2f; if (k <= 0f) continue; k *= k; sw += k; sh += k * g3H[x]; }
            road = sh / sw;
        }
        float t = road - 0.05f;   // 5 cm onder het wegdek
        if (!RoadShape && t > base_) return base_;   // terrein door wegen uit: niets ophogen
        if (dmin <= wallBias) return t;
        // Naast de weg: een talud dat glooiend begint (het hoogteverschil over G3Min) en hooguit zo steil is als het talud (FK, 1 op 1,5),
        // tot het het terrein raakt; bij een dijk tilt het de grond op, bij een insnijding graaft het af. Alleen waar het profiel een sokkel
        // of bak maakt (meer dan 6 m van het terrein, SplitWalls: q.wall) blijft het terrein staan en staat er een keermuur langs de wegrand.
        if (wallNear) return base_;
        float dz = Mathf.Abs(base_ - t), k2 = Mathf.Min(RoadSlopes ? FK : 8f, Mathf.Max(dz / (RoadSlopes ? G3Min : 0.6f), 0.02f)), dd = Mathf.Max(0f, dmin);
        float h = base_ < t ? Mathf.Max(base_, t - dd * k2) : Mathf.Min(base_, t + dd * k2);
        slope = Mathf.Abs(h - base_) > 0.05f && dz > 0.4f; return h;
    }

    // Kan er in vak i een keermuur langs de wegrand staan: een stuk of kop met een sokkel of bak (q.wall), of een hard eind (tunnelportaal) in de buurt
    static readonly Dictionary<int, (int ver, bool v)> v3WallCache = new Dictionary<int, (int, bool)>();
    static bool V3HasWall(int i)
    {
        if (v3WallCache.TryGetValue(i, out var e) && e.ver == fieldVer) return e.v;
        bool any = false; foreach (var q in segsUse) if (q.wall || q.hardA || q.hardB) { any = true; break; } if (!any) foreach (var q in capsUse) if (q.wall) { any = true; break; }
        v3WallCache[i] = (fieldVer, any); return any;
    }
    // De wandlijnen van vak c in de nieuwe opzet: de twee wegranden van elk stuk en de zijden van elke kop, zo gericht dat de weg negatief is;
    // grp: per lijn het nummer van zijn stuk of kop (de weg is de snijding van de halfvlakken van die groep)
    static List<Lin> V3WallLins(Planet.Cell c, List<int> grp, List<bool> noQuad)
    {
        var res = new List<Lin>(); Vector3 o3 = c.dir * Planet.R; int g = 0;
        foreach (var q in segsUse)
        {
            SegLins(c, q, out Lin T, out Lin S, out float len); if (len < 1e-3f) continue;
            if (Mathf.Abs(T.a) > q.hw + c.radius * 1.3f || S.a < -c.radius * 1.3f - 12f || S.a > len + c.radius * 1.3f + 12f) continue;
            res.Add(T - Lin.C(q.hw)); grp?.Add(g); noQuad?.Add(false); res.Add(Lin.C(-q.hw) - T); grp?.Add(g); noQuad?.Add(false);
            if (q.hardA) { res.Add(Lin.C(0f) - S); grp?.Add(g); noQuad?.Add(true); }   // het harde eind: daar staat het portaal, geen wand
            if (q.hardB) { res.Add(S - Lin.C(len)); grp?.Add(g); noQuad?.Add(true); }
            g++;
        }
        foreach (var cp in capsUse)
        {
            Vector3 dc = o3 - cp.c; if (dc.magnitude > cp.hw * 1.2f + c.radius * 1.3f) continue;
            foreach (var n in cp.n) { res.Add(new Lin { a = Vector3.Dot(dc, n) - cp.hw, x = Vector3.Dot(c.east, n), y = Vector3.Dot(c.north, n) }); grp?.Add(g); noQuad?.Add(false); }
            g++;
        }
        return res;
    }

    // De veelhoeken van waaierdriehoek k van vak c (vlakke coördinaten van het vak): de stroken langs de wegstukken, de rest ongedeeld
    static List<List<Vector2>> V3Polys(Planet.Cell c, int k)
    {
        int m = c.corners.Length; Vector2 L1 = c.local[k], L2 = c.local[(k + 1) % m];
        var free = new List<List<Vector2>> { new List<Vector2> { Vector2.zero, L1, L2 } }; var outp = new List<List<Vector2>>();
        // f <= 0 houden; geeft het deel binnen terug en zet het deel buiten in rest
        List<Vector2> Keep(List<Vector2> p, Lin f, List<List<Vector2>> rest)
        {
            float lo = float.MaxValue, hi = float.MinValue; foreach (var v in p) { float x = f.At(v); lo = Mathf.Min(lo, x); hi = Mathf.Max(hi, x); }
            if (hi <= 1e-3f) return p; if (lo >= -1e-3f) { rest?.Add(p); return null; }
            var a = ClipHalf(p, new Vector2(f.x, f.y), -f.a); var b = ClipHalf(p, new Vector2(-f.x, -f.y), f.a);
            if (b.Count >= 3) rest?.Add(b); return a.Count >= 3 ? a : null;
        }
        void Cut(List<List<Vector2>> ps, Lin f) { for (int x = ps.Count - 1; x >= 0; x--) { var rest = new List<List<Vector2>>(); var a = Keep(ps[x], f, rest); if (a == null || rest.Count == 0) continue; ps[x] = a; ps.AddRange(rest); } }
        var segs = segsUse; Vector3 o3 = c.dir * Planet.R;
        // eerst de koppen (een doodlopend eind met zijn keerrondje, een bocht, een kruispunt): de zeshoek rond het hart, zijden tegenover de buren
        foreach (var cp in capsUse)
        {
            Vector3 dc = o3 - cp.c; if (dc.magnitude > cp.hw * 1.2f + c.radius * 1.3f) continue;
            var hp = new List<Lin>(); foreach (var n in cp.n) hp.Add(new Lin { a = Vector3.Dot(dc, n) - cp.hw, x = Vector3.Dot(c.east, n), y = Vector3.Dot(c.north, n) });
            var next = new List<List<Vector2>>();
            var inCap = new List<List<Vector2>>();
            foreach (var p0 in free) { var cur = p0; foreach (var f in hp) { cur = Keep(cur, f, next); if (cur == null) break; } if (cur != null) inCap.Add(cur); }
            free = next;
            // binnen de kop de wegranden van de armen (onder de verharding, dus onzichtbaar): een kruispunt helt naar zijn armen, en zonder
            // punten daar lag de grond in het midden tot een halve meter boven het wegdek
            if (inCap.Count > 0) foreach (var q in segs)
            {
                if ((q.a - cp.c).sqrMagnitude > 0.01f && (q.b - cp.c).sqrMagnitude > 0.01f) continue; SegLins(c, q, out Lin Tq, out _, out float lq); if (lq < 1e-3f) continue;
                Cut(inCap, Tq - Lin.C(q.hw)); Cut(inCap, Tq + Lin.C(q.hw));
            }
            outp.AddRange(inCap);
        }
        for (int x = 0; x < segs.Count; x++)
        {
            var q = segs[x]; SegLins(c, q, out Lin T, out Lin S, out float len); if (len < 1e-3f) continue;
            int rows = G3Rows <= 0 ? 0 : Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(G3Min, SR(q)) / G3Row), 2, G3MaxRows); float Wr = rows * G3Row;
            if (Mathf.Abs(T.a) > q.hw + Wr + c.radius * 1.3f || S.a < -c.radius * 1.3f - Wr - 12f || S.a > len + c.radius * 1.3f + Wr + 12f) continue;
            Vector2 gS = new Vector2(S.x, S.y), gT = new Vector2(T.x, T.y), pa = -S.a * gS - T.a * gT, pb = pa + gS * len;
            var hp = new List<Lin> { T - Lin.C(q.hw + Wr), Lin.C(-(q.hw + Wr)) - T };
            // de deellijn met elk ander stuk dat aan hetzelfde eind vastzit; geen ander stuk: het eind, met de kop en de overgang erachter
            for (int e = 0; e < 2; e++)
            {
                Vector3 n3 = e == 0 ? q.a : q.b; Vector2 N = e == 0 ? pa : pb, uq = e == 0 ? gS : -gS; bool any = false;
                for (int y = 0; y < segs.Count; y++)
                {
                    if (y == x) continue; var r = segs[y]; bool ra = (r.a - n3).sqrMagnitude < 0.01f, rb = (r.b - n3).sqrMagnitude < 0.01f; if (!ra && !rb) continue;
                    SegLins(c, r, out _, out Lin Sr, out float lr); if (lr < 1e-3f) continue; Vector2 ur = new Vector2(Sr.x, Sr.y) * (ra ? 1f : -1f), nn = uq - ur; float nl = nn.magnitude; if (nl < 1e-3f) continue;
                    nn /= nl; hp.Add(new Lin { a = Vector2.Dot(nn, N), x = -nn.x, y = -nn.y }); any = true;
                }
                if (any) continue;
                if (e == 0 ? q.hardA : q.hardB) { hp.Add(e == 0 ? Lin.C(0f) - S : S - Lin.C(len)); continue; }
                float capR = q.hw; foreach (var cp in capsUse) if ((cp.c - n3).sqrMagnitude < 0.01f) capR = Mathf.Max(capR, cp.hw);
                hp.Add(e == 0 ? Lin.C(-(capR + Wr)) - S : S - Lin.C(len + capR + Wr));
            }
            var claimed = new List<List<Vector2>>(); var next = new List<List<Vector2>>();
            foreach (var p0 in free) { var cur = p0; foreach (var f in hp) { cur = Keep(cur, f, next); if (cur == null) break; } if (cur != null) claimed.Add(cur); }
            free = next; if (claimed.Count == 0) continue;
            // de rijen: de wegrand aan beide kanten en elke G3Row meter daarbuiten; één dwarslijn halverwege het stuk
            if (rows > 0)
            {
                Cut(claimed, T - Lin.C(q.hw)); Cut(claimed, T + Lin.C(q.hw));
                for (int j = 1; j < rows; j++) { Cut(claimed, T - Lin.C(q.hw + j * G3Row)); Cut(claimed, T + Lin.C(q.hw + j * G3Row)); }
                Cut(claimed, S - Lin.C(len * 0.5f));
            }
            outp.AddRange(claimed);
        }
        outp.AddRange(free); return outp;
    }
}
