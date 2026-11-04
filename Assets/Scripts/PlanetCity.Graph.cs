using System.Collections.Generic;
using UnityEngine;

// De wegengraaf: verbindingen (Links), onderdoorgangen (Under), stijl, eenrichting, tunnelverbindingen, halve vakken en LinePlan.
public static partial class PlanetCity
{
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
        return Vector3.Dot(va.normalized, vb.normalized) < -0.97f && Adjacent2(a, m) && Adjacent2(m, b) ? m : -1;   // alleen twee stappen: een tunnel door een berg (verder weg) heeft geen middenvak
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
}
