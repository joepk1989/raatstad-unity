using System.Collections.Generic;
using UnityEngine;

// De AI-bouwer: een planner die vak voor vak een stad laat groeien vanaf één aangewezen vak, met gewone bouwzetten
// (een weg of pad slepen, een blok plaatsen), net als een speler. Los van de interface: PlanetBuilder.AI.cs voert de zetten uit
// via Push/Changed, zodat undo, geluid en voorbeeld werken. Later kan deze planner de tegenstander in het duel worden.
//
// Werkwijze:
//  1. Raamwerk (Skeleton, bij de start vastgelegd): een zeshoekig stratenplan rond het startvak, langs de assen van het raster:
//     ringwegen op 4, 7, 10 en 13 vakken (brede weg), spaken van ring naar ring om en om (60° uit elkaar), een gracht op 5 vakken.
//     Die vakken blijven vrij (geen gebouwen) tot het stuk aan de beurt is, zodat de patronen op elkaar aansluiten.
//  2. Kern (vast): de hoofdstraat (laan) door het startvak tot aan de eerste ring, een plein ernaast, een fiets- en voetpadring op 2 vakken
//     (zes rechte stukken, met oversteken), winkels aan het plein, een zijstraat onder 60° die op het midden van een ringzijde uitkomt.
//  3. Patronen zodra de stad groot genoeg is (Patterns): de ringzijden, de gracht (vóór de spaken, zodat die er met een brug overheen gaan),
//     de spaken, fietspaden van de padring naar de eerste ring. Elk stuk is één zet (een rechte sleep). Kan een stuk niet (water, een vak
//     van de speler), dan zoekt Dijkstra (Route, zoals Connect in PlanetFill) een omweg over vrije vakken met een bochtstraf, en die
//     wordt opgeknipt in rechte stukken.
//  4. Daartussen kandidaten binnen het bereik (groeit met wat er gebouwd is): woningen aan een straat of pad, winkels (naar het aantal
//     woningen, bij een plein), kantoren en meer lagen in het hart, pleinen als wijkcentrum, parken ertussen, een toren op een blok,
//     molens en boerendaken aan de rand, een meertje in een laagte, een rotonde op een kruispunt; straten verlengen, aftakken onder
//     60°/90°/120°, doodlopende einden met Dijkstra aansluiten op de dichtstbijzijnde straat; steegjes (voetpaden) tussen de huizen.
//  5. Elke kandidaat krijgt een score uit eenvoudige regels; de beste blok- en padzetten krijgen daarbovenop het effect op de schoonheid
//     (Beauty, zoals het voorbeeld bij de muis). Het terrein telt mee: niet in water, niet op een steile helling; een weg door een berg
//     wordt vanzelf een tunnel (BoreLine), over een eigen weg een kruispunt, over de gracht een brug.
// Alleen lege vakken en wild bos (parkvakken zonder bebouwing van de speler ernaast: de bomen wijken voor een straat, of worden gekapt
// als losse zet); wat de speler bouwde blijft staan (een straat mag wel aansluiten op een doodlopende weg van de speler).
public static partial class PlanetAI
{
    public class Move
    {
        public string kind;          // "lijn" (weg, pad of gracht slepen), "blok" (één vak)
        public string type;          // weg, pad, water, rotonde, gum, of het blok: woning, winkel, kantoor, park, plein, toren, molen, dak, blok, meer
        public string what;          // wat de AI doet, voor het paneel
        public int cell = -1;        // het vak (blok)
        public List<int> line;       // de vakken van een weg, pad of gracht, in volgorde
        public bool bike;            // pad: fiets- en voetpad
        public byte style;           // weg: 0 gewoon, 1 breed, 2 laan
        public string next;          // gum (bomen kappen): wat er daarna komt, voor de schoonheid vooraf
        public List<Planned> rest;   // vervolgstukken van een route (na deze zet vooraan in de rij)
        public float rule, beauty;   // score uit de regels, en het effect op de schoonheid
        public bool joins;           // sluit aan op een bestaande weg of pad
        public bool sym;             // na deze zet dezelfde zet rondom het centrum (symmetrie)
        public int clearFor = -1;    // gum om ruimte te maken voor een rotonde op dit kruispunt
        public bool queued;          // kwam uit de rij met geplande stukken (dan geen nieuwe kopieën)
        public float Score => rule + Mathf.Clamp(beauty, -3f, 3f) * 0.9f;
    }
    // een gepland recht stuk: de vakken zelf, of van vak a naar vak b (LinePlan bij het leggen); strict: een symmetrische kopie,
    // die moet aan een straat beginnen en mag niet langs een andere straat lopen. Een blok: kind "blok" met één vak in cells.
    public class Planned { public int a, b; public List<int> cells; public string type, what, kind; public bool bike, strict; public byte style; }

    // ---------- bouwstijlen: de voorkeur van de AI ----------
    public enum Style { Tuinstad, Grachtenstad, Modern, Dorp }
    public static readonly string[] StyleNames = { "Tuinstad", "Grachtenstad", "Moderne stad", "Dorp" };
    public static readonly string[] StyleInfo = {
        "Laag en groen: veel parken, lanen, rotondes en fietspaden",
        "Grachten rondom, smalle hoge huizen, pleinen en winkels aan het water",
        "Hoog in het midden: kantoren, torens en brede wegen",
        "Laag en ruim: boerendaken, molens, gewone straten en meertjes" };
    public static Style Kind = Style.Tuinstad;
    struct Prefs { public int[] floors; public float park, shop, path, office; public byte ring, main; public bool canal; public int molen, toren, meer, rot; }
    static Prefs Pref => Kind switch
    {
        // floors: hoogte per afstand (hart, 1, 2, tot 4, verder); park/shop/path: hoeveel groen, winkels, paden per gebouw; ring/main: soort weg
        Style.Grachtenstad => new Prefs { floors = new[] { 4, 4, 3, 3, 2 }, park = 0.18f, shop = 0.25f, path = 0.15f, office = 0.3f, ring = 0, main = 2, canal = true, molen = 250, toren = 100, meer = 200, rot = 140 },
        Style.Modern => new Prefs { floors = new[] { 8, 6, 5, 3, 2 }, park = 0.15f, shop = 0.2f, path = 0.08f, office = 1f, ring = 1, main = 1, canal = false, molen = 9999, toren = 60, meer = 250, rot = 70 },
        Style.Dorp => new Prefs { floors = new[] { 2, 2, 1, 1, 1 }, park = 0.3f, shop = 0.12f, path = 0.1f, office = 0f, ring = 0, main = 0, canal = false, molen = 60, toren = 200, meer = 80, rot = 9999 },
        _ => new Prefs { floors = new[] { 3, 3, 2, 2, 1 }, park = 0.4f, shop = 0.15f, path = 0.25f, office = 0.3f, ring = 0, main = 2, canal = false, molen = 150, toren = 150, meer = 100, rot = 60 },
    };

    public const string Version = "4 (6 nov 2026)";   // ophogen bij een nieuwe versie van de planner, voor het scoreboek (ai-scores.txt)
    public static int Start = -1;
    public static readonly HashSet<int> Owned = new HashSet<int>();   // de vakken die de AI bebouwde
    public static int Moves, Rejected;
    public const int MaxRange = 16;
    const float P = 15.6f;
    static System.Random rnd = new System.Random(3);
    static readonly Dictionary<int, int> dist = new Dictionary<int, int>();   // afstand in vakken tot het startvak
    static float a0;              // richting van buur 0 van het startvak (hoek in het raakvlak)
    static int mainK;             // de as van de hoofdstraat (0..5)
    static int core, coreRetry;   // de hoeveelste zet van de kern (daarna -1: vrij groeien)
    static int coreRoad = -1;     // het vak van de hoofdstraat op afstand 2, voor de zijstraat
    static int plein = -1;
    static readonly HashSet<int> rings = new HashSet<int>();   // de ringen (straal) die al gepland zijn
    static readonly Dictionary<int, string> reserved = new Dictionary<int, string>();   // raamwerk: vak -> "ring4", "spaak7", "gracht"
    static readonly List<Planned> queue = new List<Planned>();
    static readonly HashSet<string> banned = new HashSet<string>();   // zetten die bij het plaatsen mislukten: niet nog eens proberen (geen lus)
    static string KeyOf(Move m) => m.type + "|" + (m.line != null ? string.Join(",", m.line) : m.cell.ToString());
    static bool Banned(Move m) => m != null && banned.Contains(KeyOf(m));
    public static float Range => Mathf.Clamp(3.2f + Mathf.Sqrt(Owned.Count) * 0.8f, 4.5f, MaxRange);   // begint bij de eerste ring (4 vakken)
    public static int RangeCells => Mathf.RoundToInt(Range);
    public static bool Active => Start >= 0;
    static readonly int[] RingR = { 4, 7, 10, 13 }, RingNeed = { 30, 80, 170, 300 };
    const int CanalR = 5;

    // null: gestart; anders de reden waarom niet
    // style < 0: de AI kiest zelf (vast bepaald door de seed)
    public static string Begin(int start, int seed, int style = -1)
    {
        string why = CanStart(start); if (why != null) return why;
        Reset(); Start = start; rnd = new System.Random(seed);
        Kind = style >= 0 ? (Style)style : (Style)rnd.Next(StyleNames.Length);
        dist[start] = 0; var q = new Queue<int>(); q.Enqueue(start);
        while (q.Count > 0) { int c = q.Dequeue(); if (dist[c] >= MaxRange + 1) continue; foreach (int j in Planet.Cells[c].nb) if (!dist.ContainsKey(j)) { dist[j] = dist[c] + 1; q.Enqueue(j); } }
        var S = Planet.Cells[start]; Vector3 n0 = Planet.Cells[S.nb[0]].dir - S.dir; n0 -= S.dir * Vector3.Dot(n0, S.dir);
        a0 = Mathf.Atan2(Vector3.Dot(n0, S.north), Vector3.Dot(n0, S.east));
        Skeleton(); core = 0; return null;
    }
    // kan de AI op dit vak beginnen? null = ja, anders de reden (ook voor het voorbeeld van de vlag)
    public static string CanStart(int start)
    {
        if (start < 0) return "Wijs een vak aan";
        if (PlanetCity.Cells.ContainsKey(start) && !WildStart(start)) return "Kies een leeg vak (of wild bos)";
        if (PlanetCity.Wet(start)) return "Niet in het water";
        if (Slope(start) > 4f) return "Te steil om te beginnen";
        return null;
    }
    // zoals Wild, maar zonder dat er al een AI-stad is (Owned is dan leeg)
    static bool WildStart(int c) { if (!PlanetCity.Cells.TryGetValue(c, out var l) || l.Count != 1 || l[0] != "park") return false; foreach (int j in Planet.Cells[c].nb) if (PlanetCity.Cells.TryGetValue(j, out var o) && o.Count > 0 && !(o.Count == 1 && o[0] == "park")) return false; return true; }
    public static void Reset() { Start = -1; Owned.Clear(); Origin.Clear(); PlanLog.Clear(); Moves = Rejected = 0; dist.Clear(); core = -1; coreRetry = 0; coreRoad = plein = -1; rings.Clear(); reserved.Clear(); queue.Clear(); banned.Clear(); rotCache.Clear(); hubs.Clear(); ringAt.Clear(); roomFor.Clear(); cands = null; think = 0; }

    // ---------- hulp ----------
    static bool Empty(int c) => !PlanetCity.Cells.ContainsKey(c);
    static bool OwnPark(int c) => Owned.Contains(c) && PlanetCity.Cells.TryGetValue(c, out var l) && l.Count == 1 && l[0] == "park";
    // wild bos: een parkvak dat niet van de AI is en nergens aan bebouwing of een weg van de speler grenst (dus natuur, geen stadspark)
    static bool Wild(int c)
    {
        if (Owned.Contains(c) || !PlanetCity.Cells.TryGetValue(c, out var l) || l.Count != 1 || l[0] != "park") return false;
        foreach (int j in Planet.Cells[c].nb) if (!Owned.Contains(j) && PlanetCity.Cells.TryGetValue(j, out var o) && o.Count > 0 && !(o.Count == 1 && o[0] == "park")) return false;
        return true;
    }
    static bool Free(int c) => Empty(c) || OwnPark(c) || Wild(c);   // waar een straat of pad kan komen: leeg, een eigen park of wild bos (de bomen wijken)
    // vakken waar de AI nooit bouwt; startplekken en polen zijn gewone bouwgrond (de startstad staat er ook op), dus nu niets
    static bool Forbidden(int c) => false;
    static float Slope(int c) { float g = PlanetCity.G(c), m = 0f; foreach (int j in Planet.Cells[c].nb) m = Mathf.Max(m, Mathf.Abs(PlanetCity.G(j) - g)); return m; }
    static bool Land(int c) => !Forbidden(c) && !PlanetCity.Wet(c) && Slope(c) <= 4f && PlanetCity.G(c) < 48f && PlanetCity.RotOf(c) < 0;   // op het land, niet te steil, niet in de sneeuw, niet op een rotonde
    static bool Buildable(int c) => Empty(c) && !reserved.ContainsKey(c) && Land(c);   // een leeg vak waar iets op kan (niet op het raamwerk)
    static bool Clearable(int c) => Wild(c) && !reserved.ContainsKey(c) && Land(c);     // bomen kappen om er te bouwen
    // rotondes: het midden (IsRot) en de zes ringvakken eromheen; een arm is een weg op een ringvak (aan het midden vast)
    static bool RotRing(int c) => PlanetCity.RotOf(c) >= 0 && !PlanetCity.IsRot(c);
    static bool Arm(int c) => PlanetCity.IsRoad(c) && RotRing(c);
    // ligt er een rotonde binnen zoveel stappen? Minstens 5 vakken (zo'n 78 m) van midden tot midden, anders staan ze te dicht op elkaar
    public const int RotGap = 5;
    static bool RotNear(int c, int steps)
    {
        var seen = new HashSet<int> { c }; var fr = new List<int> { c };
        for (int s = 0; s < steps; s++) { var nx = new List<int>(); foreach (int x in fr) foreach (int j in Planet.Cells[x].nb) if (seen.Add(j)) { if (PlanetCity.IsRot(j)) return true; nx.Add(j); } fr = nx; }
        return false;
    }
    // ligt er al een arm op een ringvak naast dit ringvak? Dan zouden twee wegen 60° uit elkaar in elkaar overlopen (minstens 120° tussen de armen)
    static bool ArmBeside(int c) { int r = PlanetCity.RotOf(c); foreach (int x in Planet.Cells[c].nb) if (x != c && PlanetCity.RotOf(x) == r && !PlanetCity.IsRot(x) && Arm(x) && PlanetCity.StyleOf(x) > 0) return true; return false; }   // naast een laan of brede weg geen nieuwe arm
    // een lijn die op het midden van een rotonde begint of eindigt: dat vak eraf, zodat hij op een ringvak aansluit (een arm wordt)
    static List<int> TrimRot(List<int> line)
    {
        if (line == null) return null; int a = 0, b = line.Count;
        if (b > 0 && PlanetCity.IsRot(line[0])) a = 1; if (b - a > 0 && PlanetCity.IsRot(line[b - 1])) b--;
        return a == 0 && b == line.Count ? line : line.GetRange(a, Mathf.Max(0, b - a));
    }
    static bool GroundRoad(int c) => PlanetCity.IsRoad(c) && !PlanetCity.Underpass(c) && PlanetCity.RotOf(c) < 0 && (Owned.Contains(c) || PlanetCity.StyleOf(c) == 0);
    static bool NearRoad(int c) { foreach (int j in Planet.Cells[c].nb) if (PlanetCity.IsRoad(j) && !PlanetCity.Underpass(j)) return true; return false; }
    static bool NearWalk(int c) { foreach (int j in Planet.Cells[c].nb) if ((PlanetCity.IsRoad(j) && !PlanetCity.Underpass(j)) || PlanetCity.IsPath(j)) return true; return false; }
    static bool NearType(int c, string t) { foreach (int j in Planet.Cells[c].nb) if (PlanetCity.Cells.TryGetValue(j, out var l) && l.Contains(t)) return true; return false; }
    static bool IsBuildingOrPark(List<string> l) => l.Count > 0 && (IsBuilding(l) || (l.Count == 1 && l[0] == "park"));
    static bool IsBuilding(List<string> l) => l.Count > 0 && l[0] != "weg" && l[0] != "wegdeel" && l[0] != "park" && l[0] != "pad" && l[0] != "water" && l[0] != "meer";
    // buren met een gebouw (geen weg, pad of park: anders kweekt een park steeds nieuwe parken) en buren met een park
    static int BuiltNb(int c) { int n = 0; foreach (int j in Planet.Cells[c].nb) if (PlanetCity.Cells.TryGetValue(j, out var l) && IsBuilding(l)) n++; return n; }
    static int ParkNb(int c) { int n = 0; foreach (int j in Planet.Cells[c].nb) if (PlanetCity.Cells.TryGetValue(j, out var l) && l.Count == 1 && l[0] == "park") n++; return n; }
    static int D(int c) => dist.TryGetValue(c, out int d) ? d : 999;
    static bool InRange(int c, float extra = 0f) => D(c) <= Range + extra;
    static int Floors(int c) { int n = 0; if (PlanetCity.Cells.TryGetValue(c, out var l)) foreach (var b in l) if (b == "woning" || b == "kantoor" || b == "winkel") n++; return n; }
    static int TargetFloors(int d) { var f = Pref.floors; return d <= 0 ? f[0] : d <= 1 ? f[1] : d <= 2 ? f[2] : d <= 4 ? f[3] : f[4]; }
    // symmetrie: vak c gedraaid om het startvak over k keer 60°
    static readonly Dictionary<long, int> rotCache = new Dictionary<long, int>();
    static int Turn(int c, int k)
    {
        k = ((k % 6) + 6) % 6; if (k == 0) return c; long key = (long)c * 8 + k;
        if (rotCache.TryGetValue(key, out int r)) return r;
        r = Planet.Nearest(Quaternion.AngleAxis(k * 60f, Planet.Cells[Start].dir) * Planet.Cells[c].dir * Planet.R); rotCache[key] = r; return r;
    }
    // de hoeken van de ringen, waar spaken en de hoofdstraat aansluiten: daar komt een rotonde
    static readonly HashSet<int> hubs = new HashSet<int>();
    static string Top(int c) => PlanetCity.Cells.TryGetValue(c, out var l) && l.Count > 0 ? l[l.Count - 1] : null;
    static int CellAt(float ang, float r)
    {
        var S = Planet.Cells[Start];
        return Planet.Nearest((S.dir * Mathf.Cos(r / Planet.R) + (S.east * Mathf.Cos(ang) + S.north * Mathf.Sin(ang)) * Mathf.Sin(r / Planet.R)).normalized * Planet.R);
    }
    static int Corner(int k, int r) => CellAt(a0 + k * Mathf.PI / 3f, r * P);   // hoek k (0..5) van de zeshoek met straal r vakken
    // richting (in het raakvlak) van vak a naar vak b
    static Vector3 Tangent(int a, int b) { Vector3 da = Planet.Cells[a].dir, t = Planet.Cells[b].dir - da; t -= da * Vector3.Dot(t, da); return t.sqrMagnitude < 1e-12f ? Vector3.zero : t.normalized; }
    static int Toward(int from, Vector3 t, float cells) => Planet.Nearest((Planet.Cells[from].dir * Mathf.Cos(cells * P / Planet.R) + t * Mathf.Sin(cells * P / Planet.R)).normalized * Planet.R);
    static Vector3 Rot(int at, Vector3 t, float deg) => Quaternion.AngleAxis(deg, Planet.Cells[at].dir) * t;
    static string Compass(int at, Vector3 t)
    {
        var c = Planet.Cells[at]; float a = Mathf.Atan2(Vector3.Dot(t, c.north), Vector3.Dot(t, c.east)) * Mathf.Rad2Deg; if (a < 0f) a += 360f;
        string[] n = { "oosten", "noordoosten", "noorden", "noordwesten", "westen", "zuidwesten", "zuiden", "zuidoosten" };
        return n[Mathf.RoundToInt(a / 45f) % 8];
    }
    static int Count(string t) { int n = 0; foreach (int c in Owned) if (PlanetCity.Cells.TryGetValue(c, out var l)) foreach (var b in l) if (b == t) n++; return n; }
    static bool Within(int c, string t, int steps)   // staat blok t binnen zoveel stappen van c?
    {
        var seen = new HashSet<int> { c }; var fr = new List<int> { c };
        for (int s = 0; s < steps; s++) { var nx = new List<int>(); foreach (int x in fr) foreach (int j in Planet.Cells[x].nb) if (seen.Add(j)) { if (PlanetCity.Cells.TryGetValue(j, out var l) && l.Contains(t)) return true; nx.Add(j); } fr = nx; }
        return false;
    }

    // ---------- raamwerk: het zeshoekige stratenplan, bij de start vastgelegd ----------
    static List<int> Side(int k, int r) => PlanetCity.LinePlan(Corner(k, r), Corner(k + 1, r));
    static void Skeleton()
    {
        for (int i = 0; i < RingR.Length; i++)
        {
            int r = RingR[i];
            for (int k = 0; k < 6; k++) { var s = Side(k, r); if (s != null) foreach (int c in s) if (!reserved.ContainsKey(c)) reserved[c] = "ring" + r; }
            if (i > 0) for (int k = i % 2; k < 6; k += 2) { var s = PlanetCity.LinePlan(Corner(k, RingR[i - 1]), Corner(k, r)); if (s != null) foreach (int c in s) if (!reserved.ContainsKey(c)) reserved[c] = "spaak" + r; }
        }
        // de hoeken van de ringen 4, 7 en 10: het vak en de zes eromheen vrij houden voor een rotonde (zonder rotondes in deze stijl niet)
        hubs.Clear();
        if (Pref.rot < 9999)
            for (int i = Pref.canal ? 1 : 0; i < 3; i++) for (int k = 0; k < 6; k++)   // met een gracht op 5 vakken geen rotondes op ring 4 (water naast de ring zou ze tegenhouden): de gracht gaat helemaal rond
                {
                    // alleen de hoeken waar een spaak naar buiten vertrekt (ring 4 en 10 de oneven hoeken, ring 7 de even): daar komen de twee
                    // ringzijden en die spaak samen, drie armen op 120°, en die hoeken liggen allemaal minstens 7 vakken uit elkaar (RotGap 5)
                    if (k % 2 != (i == 1 ? 0 : 1)) continue;
                    int h = Corner(k, RingR[i]); hubs.Add(h);
                    foreach (int j in Planet.Cells[h].nb) if (!reserved.ContainsKey(j)) reserved[j] = "rotonde" + RingR[i];
                    if (!reserved.ContainsKey(h)) reserved[h] = "rotonde" + RingR[i];
                }
        // de gracht op 5 vakken, behalve vlak naast een rotondeplek (water naast de ring zou de rotonde tegenhouden): zes rechte stukken
        if (Pref.canal) for (int k = 0; k < 6; k++) { var s = Side(k, CanalR); if (s != null) foreach (int c in s) if (!reserved.ContainsKey(c) && !NearHub(c)) reserved[c] = "gracht"; }
    }
    static bool NearHub(int c) { if (hubs.Contains(c)) return true; foreach (int j in Planet.Cells[c].nb) if (hubs.Contains(j)) return true; return false; }
    // een brug: een straat over een eigen gracht of meertje (het spel maakt er zelf een brug of dam van, Under in PlanetCity.Place)
    static bool Bridgeable(int c) => Owned.Contains(c) && PlanetCity.Cells.TryGetValue(c, out var l) && l.Count == 1 && (l[0] == "water" || l[0] == "meer") && PlanetCity.RotOf(c) < 0;
    public static bool Reserved(int c) => reserved.ContainsKey(c);

    // Een straat of pad van a in richting t over len vakken: de lijn zoals slepen (LinePlan), bijgeknipt tot len vakken en gecontroleerd.
    // null als hij niet kan: buiten het bereik, water, een vak van de speler, te steil (zonder tunnel), een pool.
    static List<int> Line(int a, Vector3 t, int len, string type, out bool joins, bool fromWalk = true)
    {
        joins = false; if (t == Vector3.zero) return null;
        var plan = PlanetCity.LinePlan(a, Toward(a, t, len + 0.3f)); if (plan == null || plan.Count < 3) return null;
        if (plan.Count > len + 1) plan = plan.GetRange(0, len + 1);
        return CheckLine(plan, type, out joins, fromWalk ? 1 : 0) ? plan : null;
    }
    // first: 0 het eerste vak moet vrij zijn, 1 het moet een weg (of pad) zijn, 2 vrij of weg of pad. pattern: een stuk van het raamwerk of
    // een route (mag langs andere straten lopen, mag maar twee vakken lang zijn, mag een eigen doodlopend eind kruisen)
    static bool CheckLine(List<int> line, string type, out bool joins, int first, bool pattern = false)
    {
        joins = false; if (line == null || line.Count < (pattern ? 2 : 3)) return false; bool road = type == "weg";
        float steep = 0f; int fresh = 0;   // fresh: nieuwe vakken; zonder die ligt de lijn er al en verandert de zet niets
        for (int k = 0; k < line.Count; k++)
        {
            int c = line[k]; bool last = k == line.Count - 1;
            if (Forbidden(c) || !InRange(c, 0.5f)) return false;
            if (k > 0 && System.Array.IndexOf(Planet.Cells[line[k - 1]].nb, c) < 0)   // door een hoek (twee halve vakken): die moeten vrij zijn
                foreach (int h in PlanetCity.Between(line[k - 1], c)) if ((!Free(h) && !PlanetCity.IsHalf(h)) || (!pattern && reserved.ContainsKey(h))) return false;   // ook niet schuin over het vrijgehouden raamwerk
            if (!road && PlanetCity.RotOf(c) >= 0) return false;   // een pad niet op of over een rotonde
            bool walk = GroundRoad(c) || (k == 0 && Arm(c)) || (k == 0 && road && PlanetCity.IsRot(c)) || (!road && PlanetCity.IsPath(c));
            if (k > 0 && road && PlanetCity.IsRot(c)) { if (last) { if (!RotRing(line[k - 1])) return false; joins = true; } else if (!RotRing(line[k - 1]) || !RotRing(line[k + 1])) return false; continue; }   // het midden van een rotonde: alleen vanaf een ringvak (een arm), als eind of rechtdoor
            if (k > 0 && RotRing(c) && RotRing(line[k - 1]) && PlanetCity.RotOf(c) == PlanetCity.RotOf(line[k - 1])) return false;   // niet over twee ringvakken (een koorde over de rotonde)
            if (k > 0 && k < line.Count - 1 && RotRing(c) && !PlanetCity.IsRot(line[k + 1]) && !PlanetCity.IsRot(line[k - 1])) return false;   // door een ringvak alleen naar het midden toe of als eind
            if (k == 0) { if (first == 0 ? !Free(c) : first == 1 ? !walk : !(Free(c) || walk)) return false; continue; }
            if (Free(c)) { if (PlanetCity.Wet(c) || PlanetCity.G(c) > 60f) return false; fresh++; if (last && road && RotRing(c)) { if (ArmBeside(c)) return false; joins = true; } }   // eindigt op een ringvak: een nieuwe arm van de rotonde
            else if (road && last && GroundRoad(c) && (Owned.Contains(c) ? PlanetCity.LinkCount(c) <= 3 : PlanetCity.DeadEnd(c))) joins = true;   // aansluiten op een eigen straat (ook bij een T: dan een kruispunt), of op een doodlopende weg van de speler
            else if (road && !last && Owned.Contains(c) && GroundRoad(c) && PlanetCity.LinkCount(c) <= 2 && (pattern || !PlanetCity.DeadEnd(c))) { }   // een eigen straat kruisen: een kruispunt
            else if (!road && Owned.Contains(c) && GroundRoad(c)) { if (last) joins = true; }   // een pad over een eigen straat: een oversteek (ook als eind)
            else if (!road && Owned.Contains(c) && PlanetCity.IsPath(c) && (last || pattern)) joins = true;
            else if (road && !last && Bridgeable(c) && !Bridgeable(line[k - 1]) && !Bridgeable(line[k + 1])) fresh++;   // een brug over één watervak   // aansluiten op een eigen pad
            else return false;
            if (k > 0) steep = Mathf.Max(steep, Mathf.Abs(PlanetCity.G(c) - PlanetCity.G(line[k - 1])));
        }
        if (fresh == 0) return false;
        if (steep > 2.6f && (!road || PlanetCity.BorePlan(line, "weg").Count == 0)) return false;   // te steil, en geen tunnel mogelijk
        if (pattern) return true;
        // niet vlak langs een andere straat (geen twee straten naast elkaar), behalve waar hij aansluit of kruist; het eerste nieuwe vak
        // van een zijstraat raakt altijd de buren van het kruispunt, dat telt niet. Een pad niet naast een ander pad. Niet over het raamwerk.
        for (int k = 1; k < line.Count; k++) if (Free(line[k]) && reserved.ContainsKey(line[k])) return false;   // het raamwerk (ringen, spaken, gracht) blijft vrij voor zijn eigen stuk, ook het eerste nieuwe vak
        for (int k = 2; k < line.Count; k++)
        {
            int c = line[k]; if (!Free(c)) continue;
            foreach (int j in Planet.Cells[c].nb) if ((road ? PlanetCity.IsRoad(j) : PlanetCity.IsPath(j)) && !line.Contains(j) && !(k == line.Count - 1 && joins)) return false;
        }
        return true;
    }
    // hoeveel lege bouwvakken een nieuwe straat ontsluit
    static int Opens(List<int> line)
    {
        int n = 0; var seen = new HashSet<int>(line);
        foreach (int c in line) foreach (int j in Planet.Cells[c].nb) if (seen.Add(j) && (Buildable(j) || Clearable(j)) && !NearRoad(j) && InRange(j, 1f)) n++;
        return n;
    }
    static Move RoadMove(List<int> line, bool joins, string what, float bonus)
    {
        float dg = 0f; for (int k = 1; k < line.Count; k++) dg += Mathf.Abs(PlanetCity.G(line[k]) - PlanetCity.G(line[k - 1]));
        int open = Opens(line); if (open == 0 && !joins) return null;
        if (SharpBend(line)) return null;
        if (line.Exists(Bridgeable)) what += ", met een brug over het water";
        return new Move { kind = "lijn", type = "weg", line = line, what = what, joins = joins, rule = bonus + 0.3f * open + (joins ? 2.2f : 0f) - 0.25f * Mathf.Max(0, line.Count - 6) - 0.3f * dg + (float)rnd.NextDouble() * 0.3f };
    }
    // Maakt deze straat ergens een bocht van 120° (een vak met precies twee straten die bijna terug lopen, zoals een doodlopend eind dat
    // scherp afbuigt of op een ander doodlopend eind aansluit)? Zo'n zigzag rijdt niet en is lelijk; een kruispunt mag wel.
    static bool SharpBend(List<int> path)
    {
        for (int k = 0; k < path.Count; k++)
        {
            int c = path[k]; if (PlanetCity.IsRot(c)) continue; var nb = new List<int>();
            if (k > 0) nb.Add(path[k - 1]); if (k < path.Count - 1) nb.Add(path[k + 1]);
            foreach (int j in PlanetCity.Partners(c)) if (PlanetCity.IsRoad(j) && !nb.Contains(j)) nb.Add(j);
            if (nb.Count == 2 && Vector3.Dot(Tangent(nb[0], c), Tangent(c, nb[1])) < 0.3f) return true;
        }
        return false;
    }
    // een steegje: hoeveel huizen het langsloopt, en of het ergens heen gaat (een park, plein, pad of straat aan het eind)
    static Move PathMove(List<int> line, bool joins, bool bike, string what, float bonus)
    {
        int along = 0; var seen = new HashSet<int>(line); int end = line[line.Count - 1];
        foreach (int c in line) foreach (int j in Planet.Cells[c].nb) if (seen.Add(j) && PlanetCity.Cells.TryGetValue(j, out var l) && IsBuilding(l)) along++;
        bool goal = joins || NearType(end, "park") || NearType(end, "plein");
        if (along < 2 && !goal) return null;
        return new Move { kind = "lijn", type = "pad", bike = bike, line = line, what = what, joins = joins, rule = bonus + 0.35f * along + (goal ? 1f : 0f) - 0.2f * Mathf.Max(0, line.Count - 5) + (float)rnd.NextDouble() * 0.3f };
    }

    // ---------- Dijkstra: een route voor een straat van a naar een van de doelen, over vrije vakken, met een bochtstraf ----------
    // Zoals Connect in PlanetFill: rechtdoor kost 1, een bocht van 60° 0,6 extra, scherper 4; het raamwerk is goedkoper (daar hoort
    // een straat), een eigen straat kruisen kost 1,5. Niet door water, de speler zijn vakken, een steile stap of buiten het bereik.
    static List<int> Route(int a, HashSet<int> goals, float limit)
    {
        var cost = new Dictionary<int, float> { { a, 0f } }; var prev = new Dictionary<int, int> { { a, -1 } }; var open = new SortedSet<(float, int)> { (0f, a) };
        int end = -1; float endCost = float.MaxValue;
        int start = -1; { var sp = PlanetCity.Partners(a); int nr = 0; foreach (int j in sp) if (PlanetCity.IsRoad(j)) { nr++; start = j; } if (nr != 1) start = -1; }
        while (open.Count > 0)
        {
            var (cc, c) = open.Min; open.Remove(open.Min); if (cc > cost[c] || cc > limit || cc >= endCost) break;
            foreach (int j in Planet.Cells[c].nb)
            {
                if (j == a || Forbidden(j) || !InRange(j, 0.5f)) continue; bool g = goals.Contains(j);
                if (!g && PlanetCity.RotOf(j) >= 0) continue;   // niet over een rotonde heen; een vrij ringvak kan wel een doel zijn (een nieuwe arm)
                float step;
                if (Free(j)) { if (PlanetCity.Wet(j) || PlanetCity.G(j) > 60f || (reserved.TryGetValue(j, out var rv) && rv == "gracht")) continue; step = reserved.ContainsKey(j) ? 0.6f : 1f; }   // niet over het tracé van de gracht (die komt er nog)
                else if (g) step = 1f;
                else if (Owned.Contains(j) && GroundRoad(j) && PlanetCity.LinkCount(j) <= 2) step = 2.5f;
                else if (Bridgeable(j) && !Bridgeable(c)) step = 3f;   // een brug over de gracht of een meertje (nooit twee watervakken achter elkaar)   // een eigen straat kruisen
                else continue;
                float dg = Mathf.Abs(PlanetCity.G(j) - PlanetCity.G(c)); if (dg > 2.6f) continue; step += dg * 0.3f;
                if (!g && Free(j)) foreach (int x in Planet.Cells[j].nb)   // niet pal langs een andere straat (zij aan zij): een vak naast een straat die niet het doel of de vorige stap is kost extra
                    if (x != c && x != a && !goals.Contains(x) && PlanetCity.IsRoad(x) && !PlanetCity.Linked(c, x)) { step += 2.5f; break; }
                int p = prev[c]; if (p < 0) p = start;   // bij het begin: de straat die er al ligt (een doodlopend eind buigt niet scherp af)
                if (p >= 0) { float dot = Vector3.Dot(Tangent(p, c), Tangent(c, j)); if (dot < 0.3f) continue; step += dot > 0.95f ? 0f : 0.6f; }   // geen bocht van 120° (een zigzag)
                float nc = cc + step;
                if (g) { if (nc < endCost) { endCost = nc; end = j; prev[j] = c; } continue; }
                if (cost.TryGetValue(j, out float old) && old <= nc) continue;
                cost[j] = nc; prev[j] = c; open.Add((nc, j));
            }
        }
        if (end < 0) return null;
        var path = new List<int>(); for (int c = end; c >= 0; c = prev[c]) path.Add(c); path.Reverse(); return path;
    }
    // een route opknippen in rechte stukken (elk één sleep); opeenvolgende stukken delen het hoekvak
    static List<Planned> Runs(List<int> path, string type, byte style, string what)
    {
        var runs = new List<Planned>(); int s = 0;
        for (int k = 2; k <= path.Count; k++)
        {
            bool bend = k < path.Count && Vector3.Dot(Tangent(path[k - 2], path[k - 1]), Tangent(path[k - 1], path[k])) < 0.95f;
            if (bend || k == path.Count) { runs.Add(new Planned { cells = path.GetRange(s, k - s), type = type, style = style, what = what }); s = k - 1; }
        }
        return runs;
    }

    // waarom een gepland stuk niet kon (voor een test; hooguit 60 regels)
    public static readonly List<string> PlanLog = new List<string>();
    static void Note(string s) { if (PlanLog.Count < 60) PlanLog.Add(Moves + ": " + s); }
    static string Why(List<int> line)
    {
        var sb = new System.Text.StringBuilder();
        for (int k = 0; k < line.Count; k++)
        {
            int c = line[k]; string w = Free(c) ? (PlanetCity.Wet(c) ? "nat" : k > 0 && Mathf.Abs(PlanetCity.G(c) - PlanetCity.G(line[k - 1])) > 2.6f ? "steil" : "vrij")
                : PlanetCity.IsRoad(c) ? (Owned.Contains(c) ? "weg" + PlanetCity.LinkCount(c) + (PlanetCity.IsRot(c) ? "R" : RotRing(c) ? "r" : "") : "speler") : PlanetCity.Cells.TryGetValue(c, out var l) ? string.Join("+", l) : "?";
            if (!InRange(c, 0.5f)) w += "/buiten";
            if (k > 0 && System.Array.IndexOf(Planet.Cells[line[k - 1]].nb, c) < 0) { w += "/schuin"; foreach (int h in PlanetCity.Between(line[k - 1], c)) if (!Free(h) && !PlanetCity.IsHalf(h)) w += "(" + (PlanetCity.Cells.TryGetValue(h, out var hl) ? string.Join("+", hl) : "?") + ")"; }
            sb.Append(w).Append(k < line.Count - 1 ? " " : "");
        }
        return sb.ToString();
    }
    // ---------- patronen: ringen, gracht en spaken uit het raamwerk, als geplande rechte stukken ----------
    static void Plan(Planned p, bool front = false) { if (front) queue.Insert(0, p); else queue.Add(p); }
    static void PlanLine(int a, int b, string type, byte style, bool bike, string what) => Plan(new Planned { a = a, b = b, type = type, style = style, bike = bike, what = what });
    // het volgende geplande stuk dat kan; wat niet kan krijgt een omweg (Dijkstra) of vervalt
    static Move NextQueued()
    {
        while (queue.Count > 0)
        {
            var p = queue[0]; queue.RemoveAt(0);
            if (p.kind == "blok")   // een symmetrische kopie van een blikvanger
            {
                int c = p.cells[0]; if (!Buildable(c) || (p.type != "meer" && !NearWalk(c))) continue;
                var bm = new Move { kind = "blok", type = p.type, cell = c, what = p.what, rule = 8f, queued = true };
                if (!Banned(bm)) return bm; continue;
            }
            var line = p.type == "weg" ? (p.cells ?? PlanetCity.LinePlan(p.a, p.b)) : TrimRot(p.cells ?? PlanetCity.LinePlan(p.a, p.b)); if (line == null || line.Count < 2) continue;   // een straat sleept hij tot het midden van een rotonde, zoals een speler
            if (p.type == "pad") { int a2 = 0, b2 = line.Count; while (a2 < b2 && PlanetCity.RotOf(line[a2]) >= 0) a2++; while (b2 > a2 && PlanetCity.RotOf(line[b2 - 1]) >= 0) b2--; line = line.GetRange(a2, b2 - a2); if (line.Count < 2) continue; }   // een pad stopt voor de rotonde (geen fietspad over de ring)
            if (p.strict)   // een symmetrische kopie van een straat: aan een straat beginnen, niet langs een andere straat, iets ontsluiten of aansluiten
            {
                if (p.type == "weg" ? !CheckLine(line, "weg", out bool sj, 1) : !CheckLine(line, p.type, out sj, 1)) continue;
                var sm = p.type == "weg" ? RoadMove(line, sj, p.what, 8f) : PathMove(line, sj, p.bike, p.what, 8f); if (sm == null) continue;
                sm.rule = 8f; sm.style = p.style; sm.queued = true; if (!Banned(sm)) return sm; continue;
            }
            if (p.type == "water")   // de gracht: alleen de vrije vakken, in stukken
            {
                var run = new List<int>(); var runs = new List<List<int>>();
                foreach (int c in line) { if (Free(c) && !PlanetCity.Wet(c) && !Forbidden(c) && !NearHub(c)) run.Add(c); else { if (run.Count >= 2) runs.Add(run); run = new List<int>(); } }
                if (run.Count >= 2) runs.Add(run); if (runs.Count == 0) continue;
                var m = new Move { kind = "lijn", type = "water", line = runs[0], what = p.what, rule = 8f, queued = true };
                if (runs.Count > 1) { m.rest = new List<Planned>(); for (int i = 1; i < runs.Count; i++) m.rest.Add(new Planned { cells = runs[i], type = "water", what = p.what }); }
                if (!Banned(m)) return m; continue;
            }
            if (!CheckLine(line, p.type, out bool joins, 2, true))
            {
                Note(p.what + ": past niet (" + Why(line) + ")");
                if (p.type != "weg" || p.cells != null) continue;   // een pad of een stuk route dat niet kan: vervalt
                int a = line[0], b = line[line.Count - 1]; if (!(Free(a) || GroundRoad(a) || Arm(a))) { Note(p.what + ": vervalt, begin is geen straat"); continue; }
                var path = Route(a, new HashSet<int> { b }, line.Count * 2.5f + 6f); if (path == null || SharpBend(path)) { Note(p.what + ": vervalt, geen omweg" + (path != null ? " zonder scherpe bocht" : "")); continue; }   // een omweg om het obstakel heen
                var runs = Runs(path, "weg", p.style, p.what + " (omweg)"); for (int i = runs.Count - 1; i >= 0; i--) Plan(runs[i], true);
                continue;
            }
            var mv = new Move { kind = "lijn", type = p.type, bike = p.bike, style = p.style, line = line, what = p.what, joins = joins, rule = 8f, queued = true };
            if (!Banned(mv)) return mv;
        }
        return null;
    }
    // Vrijgeven: wat het raamwerk vrijhield maar leeg bleef (de ring of spaak ging met een omweg, er kwam geen rotonde) wordt weer
    // gewone bouwgrond zodra dat stuk klaar is, anders blijven er lege gaten in de stad. Een rotondeplek pas 40 zetten na zijn ring.
    static readonly Dictionary<int, int> ringAt = new Dictionary<int, int>();
    static readonly Dictionary<int, (int hub, int at)> roomFor = new Dictionary<int, (int, int)>();   // straal -> zet waarop de ring gepland werd
    static void Release()
    {
        if (queue.Count > 0 || reserved.Count == 0) return;
        List<int> free = null;
        foreach (var kv in reserved)
        {
            string v = kv.Value; int r = 0; int n = v.Length; while (n > 0 && char.IsDigit(v[n - 1])) n--;
            if (n < v.Length) int.TryParse(v.Substring(n), out r);
            string kind = v.Substring(0, n);
            bool done = kind == "gracht" ? rings.Contains(RingR[0]) || !Pref.canal : rings.Contains(r);
            if (done && kind == "rotonde") { int ri = System.Array.IndexOf(RingR, r); int nr = ri >= 0 && ri + 1 < RingR.Length ? RingR[ri + 1] : r; done = rings.Contains(nr); int at = ringAt.TryGetValue(nr, out int a) ? a : 0;   // pas na de volgende ring: dan komt de spaak naar buiten, de derde arm
                 int hub = -1; foreach (int h in hubs) if (h == kv.Key || System.Array.IndexOf(Planet.Cells[h].nb, kv.Key) >= 0) { hub = h; break; } done = done && ((hub >= 0 && PlanetCity.IsRot(hub)) || Moves - at > 40); if (hub >= 0 && !PlanetCity.IsRot(hub) && RotNear(hub, RotGap - 1)) done = true; }   // een rotonde vlakbij: hier komt er geen meer
            if (kind == "ruimte") done = !roomFor.TryGetValue(kv.Key, out var rf) || PlanetCity.IsRot(rf.hub) || Moves - rf.at > 20;
            if (done && Free(kv.Key)) (free ??= new List<int>()).Add(kv.Key);
        }
        if (free != null) foreach (int c in free) reserved.Remove(c);
    }
    // nieuwe patronen als de stad eraan toe is: per ring de zes zijden (brede weg), de gracht vóór de eerste spaken, de spaken, fietspaden
    static void Patterns()
    {
        int n = Owned.Count; float R = Range;
        for (int i = 0; i < RingR.Length; i++)
        {
            int r = RingR[i]; if (rings.Contains(r) || R < r + 0.5f) continue;
            if (n < RingNeed[i])   // nog niet groot genoeg, tenzij de bebouwing de ring al bereikt heeft (anders blijven er lege gaten)
            {
                int reached = 0; foreach (var kv in reserved) if (kv.Value == "ring" + r && BuiltNb(kv.Key) >= 2) reached++;
                if (reached < 4) continue;
            }
            rings.Add(r); ringAt[r] = Moves;
            if (i == 0 && Pref.canal) for (int k = 0; k < 6; k++) PlanLine(Corner(k, CanalR), Corner(k + 1, CanalR), "water", 0, false, "graaft de gracht rond het centrum (deel " + (k + 1) + " van 6)");   // meteen na de eerste ring: straten naar buiten gaan er met een brug overheen
            byte rs = i == 0 ? Pref.ring : (byte)0;   // de eerste ring in de soort van de stijl, de buitenste ringen gewone straten
            for (int k = 0; k < 6; k++) PlanLine(Corner(k, r), Corner(k + 1, r), "weg", rs, false, "legt de ringweg op " + r + " vakken (deel " + (k + 1) + " van 6)");
            if (i == 0) { for (int k = 0; k < 6; k += 2) if (k != mainK % 6 && k != (mainK + 3) % 6) PlanLine(Corner(k, 2), Corner(k, 4), "pad", 0, true, "legt een fietspad naar de ringweg"); }   // naar de hoeken zonder spaak naar buiten: daar komt geen rotonde (een fietspad hoort niet op de ring)
            else for (int k = i % 2; k < 6; k += 2) if (!PlanetCity.IsRoad(Corner(k, (RingR[i - 1] + r) / 2))) PlanLine(Corner(k, RingR[i - 1]), Corner(k, r), "weg", 0, false, "legt een spaak naar de nieuwe ring");
            // de straat op de hoofdas verder naar buiten, tot de nieuwe ring (zodat hij niet doodloopt op de vorige)
            if (i > 0) foreach (int k in new[] { mainK, mainK + 3 }) PlanLine(Corner(k, RingR[i - 1]), Corner(k, r), "weg", Pref.main, false, "trekt de hoofdstraat door naar de nieuwe ring");
            return;   // één patroon tegelijk
        }
    }

    // bomen kappen op wild bos, om daarna 'next' te bouwen (één zet, zoals de gum van de speler)
    static Move Cut(int c, string next, float rule = 10f) => new Move { kind = "blok", type = "gum", next = next, cell = c, what = "kapt bomen voor " + (next == "plein" ? "een plein" : next == "winkel" ? "een winkel" : next == "kantoor" ? "een kantoor" : "een woning"), rule = rule };

    // ---------- de kern: de eerste zetten staan vast ----------
    static Move CoreMove()
    {
        while (core >= 0)
        {
            int step = core++; Move m = null;
            switch (step)
            {
                case 0:   // de hoofdstraat (laan) door het startvak, van ring tot ring (4 vakken aan elke kant), langs de as met het beste terrein
                {
                    List<int> best = null; float bs = -1e9f;
                    for (int k = 0; k < 6; k++)   // eerst van ring tot ring; lukt dat nergens, dan korter (3 vakken aan elke kant)
                    {
                        float ang = a0 + k * Mathf.PI / 3f;
                        var line = k < 3 ? PlanetCity.LinePlan(Corner(k + 3, 4), Corner(k, 4)) : PlanetCity.LinePlan(CellAt(ang + Mathf.PI, 3.2f * P), CellAt(ang, 3.2f * P));
                        if (line == null || !line.Contains(Start) || !CheckLine(line, "weg", out _, 0, true)) continue;
                        float sc = Opens(line) + (k < 3 ? 10f : 0f) + (float)rnd.NextDouble(); if (sc > bs) { bs = sc; best = line; mainK = k % 3; }
                    }
                    if (best == null) { core = -1; return null; }   // geen hoofdstraat mogelijk: dan maar vrij groeien
                    int si = best.IndexOf(Start); coreRoad = best[si + 2 < best.Count ? si + 2 : si - 2];
                    // de hoofdas verder naar buiten ook vrijhouden (tot de buitenste ring), zodat de hoofdstraat later door kan
                    for (int i = 1; i < RingR.Length; i++) foreach (int k in new[] { mainK, mainK + 3 }) { var ax = PlanetCity.LinePlan(Corner(k, RingR[i - 1]), Corner(k, RingR[i])); if (ax != null) foreach (int c in ax) if (!reserved.ContainsKey(c)) reserved[c] = "as" + RingR[i]; }
                    m = new Move { kind = "lijn", type = "weg", style = Pref.main, line = best, what = Pref.main == 2 ? "legt de hoofdstraat (een laan) door het centrum" : Pref.main == 1 ? "legt een brede hoofdstraat door het centrum" : "legt de dorpsstraat door het centrum", rule = 10f }; break;
                }
                case 1:   // het plein naast de hoofdstraat, bij het startvak
                {
                    int best = -1; float bs = -1f;
                    foreach (int j in Planet.Cells[Start].nb) if ((Buildable(j) || Clearable(j)) && NearRoad(j)) { float sc = BuiltNb(j) + (Buildable(j) ? 1f : 0f) + (float)rnd.NextDouble(); if (sc > bs) { bs = sc; best = j; } }
                    if (best < 0) break;
                    if (Clearable(best) && coreRetry++ < 2) { core--; m = Cut(best, "plein"); break; }   // eerst de bomen weg, dan nog eens deze stap
                    if (Clearable(best)) break;
                    plein = best; m = new Move { kind = "blok", type = "plein", cell = best, what = "legt een plein aan bij het centrum", rule = 10f }; break;
                }
                case 2:   // de fiets- en voetpadring op 2 vakken rond het centrum (zes stukken, over de hoofdstraat heen: oversteken)
                    for (int k = 0; k < 6; k++) PlanLine(Corner(k, 2), Corner(k + 1, 2), "pad", 0, true, "legt een fiets- en voetpad rond het centrum (deel " + (k + 1) + " van 6)");
                    m = NextQueued(); break;
                case 3: case 4: case 5:   // winkels aan het plein, liefst ook aan de straat
                {
                    if (plein < 0) break; int best = -1; float bs = -1f;
                    foreach (int j in Planet.Cells[plein].nb) if ((Buildable(j) || Clearable(j)) && NearWalk(j)) { float sc = BuiltNb(j) + (NearRoad(j) ? 1f : 0f) + (Buildable(j) ? 1f : 0f) + (float)rnd.NextDouble(); if (sc > bs) { bs = sc; best = j; } }
                    if (best < 0) break;
                    if (Clearable(best) && coreRetry++ < 6) { core--; m = Cut(best, "winkel"); break; }
                    if (Clearable(best)) break;
                    m = new Move { kind = "blok", type = "winkel", cell = best, what = "bouwt winkels aan het plein", rule = 10f }; break;
                }
                case 6:   // een zijstraat onder 60° vanaf de hoofdstraat: twee vakken, tot het midden van een zijde van de eerste ring
                {
                    if (coreRoad < 0 || !GroundRoad(coreRoad)) break;
                    var rp = PlanetCity.Partners(coreRoad); if (rp.Count == 0) break;
                    Vector3 t = Tangent(rp[0], coreRoad); Move bestM = null;
                    foreach (float deg in new[] { 60f, -60f, 120f, -120f })
                    {
                        var line = Line(coreRoad, Rot(coreRoad, t, deg), 2, "weg", out bool joins); if (line == null) continue;
                        var mv = RoadMove(line, joins, "legt een zijstraat naar het " + Compass(coreRoad, Rot(coreRoad, t, deg)), 6f);
                        if (mv != null && (bestM == null || mv.rule > bestM.rule)) bestM = mv;
                    }
                    if (bestM != null) bestM.sym = true;   // en dezelfde zijstraat rondom het centrum, zodra daar een straat ligt
                    m = bestM; break;
                }
                default: core = -1; break;
            }
            if (m != null && !Banned(m)) return m;
        }
        return null;
    }

    // ---------- vrij groeien: kandidaten en hun score ----------
    static List<Move> cands; static int think; static Beauty.Result before;
    public static string Status = "";   // voor het paneel: waar de planner mee bezig is

    static void Gather()
    {
        cands = new List<Move>(); float R = Range;
        // wat er al staat (alleen eigen bouw): voor winkels per woning, pleinen, torens, parken en paden
        int shops = Count("winkel"), homes = Count("woning"), pleinen = Count("plein"), torens = Count("toren"), molens = Count("molen"), parks = Count("park"), pads = Count("pad"), daken = Count("dak"), meren = Count("meer"), rotondes = 0;
        int bld = homes + Count("kantoor") + shops;
        foreach (int c in Owned) if (PlanetCity.IsRot(c)) rotondes++;
        var pf = Pref;   // de voorkeuren van de bouwstijl
        float parkPen = parks > bld * pf.park * 1.6f ? -3f : parks > bld * pf.park ? -1.2f : 0f;   // genoeg groen: liever bouwen
        float pathWant = pads < bld * pf.path * 0.5f ? 0.8f : pads < bld * pf.path ? 0f : -1.5f;
        float shopWant = shops < homes * pf.shop * 0.6f ? 1.6f : shops < homes * pf.shop ? 0.4f : -1f;
        int officeR = Kind == Style.Modern ? 4 : 2;
        int frontier = 0; var roads = new List<int>(); var pathEnds = new List<int>(); var pathStarts = new List<int>();
        foreach (var kv in dist)
        {
            int c = kv.Key, d = kv.Value; if (d > R) continue;
            if (Owned.Contains(c) && GroundRoad(c)) roads.Add(c);
            if (Owned.Contains(c) && PlanetCity.IsPath(c) && PlanetCity.LinkCount(c) <= 1) pathEnds.Add(c);
            float rel = d / Mathf.Max(3f, R), inward = 0.9f * (1f - rel), nb = Mathf.Min(3, BuiltNb(c)) * 0.3f, noise = (float)rnd.NextDouble() * 0.35f;
            if (Buildable(c))
            {
                bool road = NearRoad(c), walk = road || NearWalk(c); int built = BuiltNb(c); bool hole = built >= 4;
                if (walk)
                {
                    frontier++;
                    cands.Add(new Move { kind = "blok", type = "woning", cell = c, what = road ? "bouwt een woning aan de straat" : "bouwt een woning aan het pad", rule = 1f + inward + nb + (hole ? 0.8f : 0f) + noise });
                    cands.Add(new Move { kind = "blok", type = "winkel", cell = c, what = NearType(c, "plein") ? "bouwt een winkel aan het plein" : "bouwt een winkel in de straat", rule = 0.6f + 0.5f * inward + nb + (NearType(c, "plein") ? 1f : 0f) + shopWant + noise });
                    if (d <= officeR && pf.office > 0f) cands.Add(new Move { kind = "blok", type = "kantoor", cell = c, what = "bouwt een kantoor in het centrum", rule = 0.9f + inward + nb + pf.office + noise });
                    if (road && pleinen < 1 + Owned.Count / (Kind == Style.Grachtenstad ? 30 : 40) && !Within(c, "plein", 3)) cands.Add(new Move { kind = "blok", type = "plein", cell = c, what = d <= 2 ? "legt een plein aan" : "legt een plein aan als hart van de wijk", rule = 1.3f + 0.5f * inward + nb + noise });
                    if (d >= 1 && d <= 3 && torens < 1 + Owned.Count / pf.toren && Owned.Count > 12) cands.Add(new Move { kind = "blok", type = "blok", cell = c, what = "zet een sokkel neer voor een toren", rule = 1.4f + (Kind == Style.Modern ? 1.5f : 0f) + nb + noise });
                    if (rel > 0.55f && pf.molen < 9999 && molens < 1 + Owned.Count / pf.molen && Owned.Count > 30 && built == 1) cands.Add(new Move { kind = "blok", type = "molen", cell = c, what = "zet een molen aan de rand van de stad", rule = 1.3f + noise });
                    cands.Add(new Move { kind = "blok", type = "park", cell = c, what = "legt een parkje aan de straat", rule = 0.3f + nb + (hole ? 0.6f : 0f) + parkPen - 0.4f * ParkNb(c) + noise });
                    if (NearType(c, "plein") || NearType(c, "park")) pathStarts.Add(c);
                }
                else if (built >= 2) cands.Add(new Move { kind = "blok", type = "park", cell = c, what = hole ? "vult een gat met een park" : "legt een park aan tussen de huizen", rule = 0.9f + nb + (hole ? 0.8f : 0f) + parkPen - 0.4f * ParkNb(c) + noise });
                else if (built == 1 && rel > 0.5f) cands.Add(new Move { kind = "blok", type = "park", cell = c, what = "plant bomen aan de rand", rule = 0.1f + parkPen - 0.4f * ParkNb(c) + noise });
                if (!walk && rel > 0.45f && built == 1 && PlanetCity.G(c) < 3f && meren < 1 + Owned.Count / pf.meer && Owned.Count > 40 && !Within(c, "meer", 4) && !Within(c, "water", 2)) cands.Add(new Move { kind = "blok", type = "meer", cell = c, what = "graaft een meertje in de laagte", rule = 1.1f + noise });
                if (built >= 1 && (NearType(c, "plein") || NearType(c, "park")) && !walk) pathStarts.Add(c);
            }
            else if (Clearable(c) && NearWalk(c))   // wild bos aan een straat: kappen om er te bouwen (iets minder graag dan een leeg vak)
            {
                frontier++; bool road = NearRoad(c); int built = BuiltNb(c);
                string next = d <= 2 ? "kantoor" : (shopWant > 0f && (NearType(c, "plein") || rnd.NextDouble() < 0.3)) ? "winkel" : "woning";
                cands.Add(Cut(c, next, 0.7f + inward + nb + (built >= 4 ? 0.8f : 0f) + (road ? 0f : -0.2f) + noise));
            }
            else if (Owned.Contains(c))
            {
                string top = Top(c); int fl = Floors(c);
                if ((top == "woning" || top == "kantoor" || top == "winkel") && fl < TargetFloors(d) && NearWalk(c))
                {
                    string add = top == "kantoor" ? "kantoor" : "woning";
                    cands.Add(new Move { kind = "blok", type = add, cell = c, what = add == "kantoor" ? "bouwt het kantoor een verdieping hoger" : top == "winkel" ? "zet een woning boven de winkel" : "zet er een verdieping op", rule = 0.9f + inward + 0.3f * (TargetFloors(d) - fl) + noise });
                }
                if (top == "woning" && fl == 1 && rel > (Kind == Style.Dorp ? 0.2f : 0.55f) && daken < 1 + Owned.Count / (Kind == Style.Dorp ? 8 : 30)) cands.Add(new Move { kind = "blok", type = "dak", cell = c, what = "zet een groot boerendak op het huis", rule = 0.8f + noise });
                if (top == "blok") cands.Add(new Move { kind = "blok", type = "toren", cell = c, what = "zet een toren op de sokkel, als blikvanger", rule = 3f + noise });
                // een rotonde op een druk kruispunt: drie of meer armen, liefst op een hoek van een ring (daar is de ruimte vrijgehouden)
                // en met brede wegen of lanen; het vak en de zes ernaast leeg of alleen een weg of pad (zoals PlaceRoundabout)
                int roadNb = 0, busy = 0; bool loose = false; foreach (int j in Planet.Cells[c].nb) if (PlanetCity.IsWalk(j)) { if (!PlanetCity.Linked(c, j)) loose = true; else if (PlanetCity.IsRoad(j)) { roadNb++; if (PlanetCity.StyleOf(j) > 0) busy++; } }   // alleen armen die echt op dit kruispunt aansluiten; een weg die er langs loopt zou de rotonde in getrokken worden
                bool hub = hubs.Contains(c);
                if (GroundRoad(c) && roadNb >= 3 && !loose && pf.rot < 9999 && !RotNear(c, RotGap - 1) && (hub || (rotondes < 1 + Owned.Count / pf.rot && Owned.Count > 50)))
                {
                    bool ok = !PlanetCity.Underpass(c) && PlanetCity.Cells[c].Count == 1;
                    foreach (int j in Planet.Cells[c].nb)   // twee armen naast elkaar (60° uit elkaar) alleen als het gewone straten zijn: met een laan of brede weg erbij lopen ze in elkaar over
                        if (PlanetCity.IsRoad(j) && PlanetCity.Linked(c, j)) foreach (int x in Planet.Cells[j].nb) if (x != c && System.Array.IndexOf(Planet.Cells[c].nb, x) >= 0 && PlanetCity.IsRoad(x) && PlanetCity.Linked(c, x) && (PlanetCity.StyleOf(j) > 0 || PlanetCity.StyleOf(x) > 0)) ok = false;
                    var clear = new List<int>();   // eigen gebouwen of parken op de ring: die mag hij eerst weghalen (opnieuw indelen), die van de speler niet
                    foreach (int j in Planet.Cells[c].nb)
                    {
                        if (PlanetCity.IsPath(j)) ok = false;   // een pad op de ring zou een fietspad over het asfalt van de rotonde worden
                        bool walk1 = PlanetCity.IsWalk(j) && PlanetCity.Cells[j].Count == 1;
                        if (!(Empty(j) || walk1 || PlanetCity.IsHalf(j)))
                        { if (Owned.Contains(j) && !PlanetCity.IsWalk(j) && PlanetCity.Cells.TryGetValue(j, out var jl) && IsBuildingOrPark(jl)) clear.Add(j); else ok = false; }
                        if (PlanetCity.Underpass(j) || PlanetCity.RotOf(j) >= 0) ok = false;
                        foreach (int k2 in Planet.Cells[j].nb) if (PlanetCity.IsRot(k2)) ok = false;
                    }
                    if (ok && clear.Count > 0)   // eerst ruimte maken: één eigen vak per zet, hooguit twee voor een kruispunt met vier armen of een brede weg, één voor een ringhoek
                    {
                        if (clear.Count <= (hub ? 2 : roadNb >= 4 || busy > 0 ? 2 : 0))
                            cands.Add(new Move { kind = "blok", type = "gum", cell = clear[0], clearFor = c, what = "maakt ruimte voor een rotonde (haalt een eigen " + (Top(clear[0]) ?? "vak") + " weg)", rule = 0.8f + (hub ? 2.2f : 0f) + 0.4f * busy + 0.5f * (roadNb - 3) + noise });
                        ok = false;
                    }
                    if (ok) cands.Add(new Move { kind = "blok", type = "rotonde", cell = c, what = hub ? "maakt een rotonde waar de wegen samenkomen" : "maakt een rotonde van het drukke kruispunt", rule = 1.2f + (hub ? 2.5f : 0f) + 0.4f * busy + 0.5f * (roadNb - 3) + noise });
                }
            }
        }
        // straten: verlengen, aftakken, aansluiten
        float roadWant = frontier <= 3 ? 2.2f : frontier <= 8 ? 0.6f : -1.4f;
        var ends = new List<int>(); foreach (int c in roads) if (PlanetCity.DeadEnd(c)) ends.Add(c);
        var rotFree = new List<int>();   // vrije ringvakken van eigen rotondes: daar kan een straat als nieuwe arm op aansluiten
        foreach (int c in Owned) if (PlanetCity.IsRot(c)) foreach (int j in Planet.Cells[c].nb) { if (Arm(j) && PlanetCity.LinkCount(j) == 1) ends.Add(j); else if (Empty(j)) rotFree.Add(j); }
        float deadBoost = ends.Count > 2 + roads.Count / 25 ? 2f : 0.5f;   // te veel doodlopende straten: eerst aansluiten
        var goals = new HashSet<int>(); foreach (int c in roads) if (PlanetCity.LinkCount(c) <= 3) goals.Add(c);
        foreach (int j in rotFree) goals.Add(j);
        foreach (var kv in dist) if (kv.Value <= R && !Owned.Contains(kv.Key) && PlanetCity.DeadEnd(kv.Key) && PlanetCity.StyleOf(kv.Key) == 0) goals.Add(kv.Key);   // ook een doodlopende weg van de speler
        foreach (int e in ends)
        {
            var p = PlanetCity.Partners(e); if (p.Count != 1) continue; Vector3 t = Tangent(p[0], e); int before = cands.Count;
            var line = Line(e, t, 3 + rnd.Next(3), "weg", out bool joins);
            if (line != null) { var m = RoadMove(line, joins, joins ? "verbindt twee straten met elkaar" : "verlengt de straat naar het " + Compass(e, t), roadWant + 0.5f + (joins ? deadBoost : -0.5f)); if (m != null) cands.Add(m); }
            // een doodlopend eind met Dijkstra naar de dichtstbijzijnde straat (niet zijn eigen buur): een rondje, in rechte stukken
            var g2 = new HashSet<int>(goals); g2.Remove(e); g2.Remove(p[0]); foreach (int j in Planet.Cells[e].nb) g2.Remove(j);
            if (RotRing(e)) foreach (int j in Planet.Cells[PlanetCity.RotOf(e)].nb) g2.Remove(j);   // een arm niet terug naar zijn eigen rotonde
            var path = Route(e, g2, 14f);
            if (path != null && path.Count >= 3 && RotRing(path[path.Count - 1])) path.Add(PlanetCity.RotOf(path[path.Count - 1]));   // tot het midden van de rotonde, zoals een speler sleept
            if (path != null && path.Count >= 3 && !SharpBend(path))
            {
                var runs = Runs(path, "weg", 0, "sluit de straat aan (vervolg)");
                var m = new Move { kind = "lijn", type = "weg", line = runs[0].cells, joins = runs.Count == 1, what = runs.Count == 1 ? "sluit de doodlopende straat aan op een andere straat" : "sluit de doodlopende straat aan, met een bocht", rule = Mathf.Max(roadWant, 0f) + 2.2f + deadBoost - 0.12f * path.Count + (float)rnd.NextDouble() * 0.3f };
                if (runs.Count > 1) m.rest = runs.GetRange(1, runs.Count - 1);
                if (CheckLine(m.line, "weg", out _, 1, true)) cands.Add(m);
            }
            // opnieuw indelen: een eigen doodlopend stukje dat niet verder kan en niets ontsluit gaat weer weg (de buur blijft netjes:
            // een kruispunt wordt een bocht of een recht stuk, of het stukje wordt vak voor vak korter)
            bool stub = PlanetCity.LinkCount(p[0]) >= 3;   // een stompje van één vak aan een kruispunt
            if ((cands.Count == before || stub) && !reserved.ContainsKey(e) && Opens(new List<int> { e }) == 0 && BuiltNb(e) == 0)
                cands.Add(new Move { kind = "blok", type = "gum", cell = e, what = "ruimt een doodlopend stukje straat op", rule = 0.9f + (stub ? 0.5f + deadBoost * 0.5f : 0f) + (float)rnd.NextDouble() * 0.3f });
        }
        // een eigen pad dat nergens heen gaat (geen huizen ernaast, geen park of plein aan het eind) gaat ook weg
        foreach (int e in pathEnds)
        {
            if (BuiltNb(e) > 0 || NearType(e, "park") || NearType(e, "plein") || PlanetCity.IsBike(e)) continue;
            cands.Add(new Move { kind = "blok", type = "gum", cell = e, what = "haalt een pad weg dat nergens heen gaat", rule = 0.7f + (float)rnd.NextDouble() * 0.3f });
        }
        // losse stukken straat (niet verbonden met het grootste deel van het eigen wegennet, bijvoorbeeld een ring waar de spaken niet
        // bij kwamen): met Dijkstra verbinden met de rest van de stad, en dat gaat voor
        {
            var seenC = new HashSet<int>(); var comps = new List<List<int>>();
            foreach (int c in roads) { if (!seenC.Add(c)) continue; var comp = new List<int>(); var q = new Queue<int>(); q.Enqueue(c); while (q.Count > 0) { int x = q.Dequeue(); comp.Add(x); foreach (int j in PlanetCity.Partners(x)) if (PlanetCity.IsRoad(j) && seenC.Add(j)) q.Enqueue(j); } comps.Add(comp); }
            if (comps.Count > 1)
            {
                comps.Sort((x, y) => y.Count.CompareTo(x.Count));
                var mainGoals = new HashSet<int>(); foreach (int c in comps[0]) if (GroundRoad(c) && PlanetCity.LinkCount(c) <= 3) mainGoals.Add(c);
                for (int ci = 1; ci < comps.Count && ci <= 3; ci++)
                {
                    List<int> best = null;
                    foreach (int c in comps[ci])
                    {
                        if (!GroundRoad(c) || PlanetCity.LinkCount(c) > 2) continue;
                        var path = Route(c, mainGoals, 22f); if (path == null || path.Count < 3 || SharpBend(path)) continue;
                        if (best == null || path.Count < best.Count) best = path;
                    }
                    if (best == null) continue;
                    var runs = Runs(best, "weg", 0, "verbindt een los stuk straat met de stad (vervolg)");
                    var m = new Move { kind = "lijn", type = "weg", line = runs[0].cells, joins = runs.Count == 1, what = "verbindt een los stuk straat met de stad", rule = 5f + 0.1f * comps[ci].Count - 0.1f * best.Count };
                    if (runs.Count > 1) m.rest = runs.GetRange(1, runs.Count - 1);
                    if (CheckLine(m.line, "weg", out _, 1, true)) cands.Add(m);
                }
            }
        }
        // zijstraten: vanaf een recht stuk, hooguit een handvol plekken per zet
        Shuffle(roads); int tries = 0;
        foreach (int c in roads)
        {
            if (tries >= 10) break; var p = PlanetCity.Partners(c); if (p.Count != 2 || PlanetCity.IsHalf(c)) continue; tries++;
            Vector3 t = Tangent(p[0], c);
            foreach (float deg in new[] { 60f, -60f, 90f, -90f, 120f, -120f })
            {
                Vector3 dir = Rot(c, t, deg); var line = Line(c, dir, 3 + rnd.Next(3), "weg", out bool joins); if (line == null) continue;
                var m = RoadMove(line, joins, "legt een nieuwe straat naar het " + Compass(c, dir), roadWant + 0.1f + (joins ? deadBoost * 0.5f : frontier <= 3 ? 0f : -0.8f)); if (m != null) { m.sym = true; cands.Add(m); }
            }
        }
        // steegjes: een padeinde rechtdoor, of een nieuw pad vanaf een plein of park tussen de huizen door (ook dwars over een straat)
        foreach (int e in pathEnds)
        {
            var p = PlanetCity.Partners(e); Vector3 t = Vector3.zero; foreach (int j in p) if (PlanetCity.IsPath(j)) t = Tangent(j, e);
            if (t == Vector3.zero) continue;
            var line = Line(e, t, 2 + rnd.Next(3), "pad", out bool joins);
            if (line != null) { var m = PathMove(line, joins, PlanetCity.IsBike(e), joins ? "sluit het pad aan" : "trekt het pad door naar het " + Compass(e, t), pathWant + 0.4f); if (m != null) cands.Add(m); }
        }
        Shuffle(pathStarts); tries = 0;
        foreach (int s in pathStarts)
        {
            if (tries >= 8) break; tries++;
            foreach (int j in Planet.Cells[s].nb)
            {
                Vector3 dir = Tangent(s, j); var line = Line(s, dir, 2 + rnd.Next(3), "pad", out bool joins, false); if (line == null) continue;
                var m = PathMove(line, joins, false, "legt een steegje tussen de huizen", pathWant); if (m != null) cands.Add(m);
            }
        }
        // symmetrie: een blok dat rondom het centrum (om de 60°) al net zo staat, krijgt een bonus
        foreach (var m in cands)
        {
            if (m.kind != "blok" || m.type == "gum" || m.type == "rotonde") continue; int same = 0;
            for (int k = 1; k < 6; k++) { int o = Turn(m.cell, k); if (Owned.Contains(o) && Top(o) == m.type) same++; }
            m.rule += 0.3f * same;
        }
        if (banned.Count > 0) cands.RemoveAll(Banned);
        cands.Sort((x, y) => y.rule.CompareTo(x.rule));
        Status = cands.Count + " mogelijke zetten, " + frontier + " vrije kavels, " + ends.Count + " doodlopend";
    }
    static void Shuffle(List<int> l) { for (int i = l.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); (l[i], l[j]) = (l[j], l[i]); } }

    // Denkt in stukjes (hooguit budgetMs per aanroep): eerst de kern of een gepland patroon, anders de kandidaten en de schoonheid
    // vooraf voor de beste blok- en padzetten. true als de keuze klaar is; best is null als er niets kan.
    public static bool Think(float budgetMs, out Move best)
    {
        best = null; var sw = System.Diagnostics.Stopwatch.StartNew();
        if (think == 0)
        {
            var cm = CoreMove(); if (cm != null) { best = cm; return true; }
            var q = NextQueued(); if (q != null) { best = q; return true; }
            Patterns(); q = NextQueued(); if (q != null) { best = q; return true; }
            Release();
            before = Beauty.Compute(); Gather(); think = 1;
            if (sw.ElapsedMilliseconds >= budgetMs) return false;
        }
        // de schoonheid vooraf voor de beste blok- en padzetten (één voor één: elk kost een rondje over de hele stad)
        int k = 0;
        foreach (var m in cands)
        {
            if (m.type == "weg" || m.type == "rotonde") continue; if (k++ >= 7) break; if (m.beauty != 0f) continue;
            m.beauty = m.line != null ? BeautyDelta(m.line, m.type) : BeautyDelta(m.cell, m.type == "gum" ? m.next ?? "gum" : m.type); if (m.beauty == 0f) m.beauty = 1e-4f;
            if (sw.ElapsedMilliseconds >= budgetMs) return false;
        }
        think = 0;
        foreach (var m in cands) if (best == null || m.Score > best.Score) best = m;
        return true;
    }
    // het effect van één blok op de schoonheid, zoals het voorbeeld bij de muis (PlanetBuilder.BeautyPreview)
    static float BeautyDelta(int cell, string type) => BeautyDelta(new List<int> { cell }, type);
    static float BeautyDelta(List<int> line, string type)
    {
        if (before == null) before = Beauty.Compute();
        var cells = PlanetCity.Cells; var had = new Dictionary<int, List<string>>();
        foreach (int c in line)
        {
            if (had.ContainsKey(c)) continue; cells.TryGetValue(c, out var l); had[c] = l;
            if (type == "gum") { cells.Remove(c); continue; }   // weghalen
            if (type == "pad" && l != null && !(l.Count == 1 && l[0] == "park")) continue;   // over een straat: het vak blijft wat het is
            var nl = l != null ? new List<string>(l) : new List<string>(); if (nl.Count == 1 && nl[0] == "park") nl.Clear(); if (type == "water" || type == "meer") nl.Clear(); nl.Add(type); cells[c] = nl;   // op bos: de bomen wijken (een pad, of na het kappen)
        }
        Beauty.Result after; try { after = Beauty.Compute(); } finally { foreach (var kv in had) { if (kv.Value != null) cells[kv.Key] = kv.Value; else cells.Remove(kv.Key); } }
        return after.raw - before.raw;
    }

    // na Ctrl+Z van de speler (of Alles terug): welke vakken zijn nog van de AI? Wat weg is telt niet meer mee; is alles weg, dan
    // begint hij weer bij de kern. Het raamwerk blijft.
    public static void Resync()
    {
        Owned.RemoveWhere(c => !PlanetCity.Cells.ContainsKey(c)); cands = null; think = 0; queue.Clear();
        if (Owned.Count == 0) { core = 0; coreRetry = 0; coreRoad = plein = -1; rings.Clear(); ringAt.Clear(); }
        else { var keep = new HashSet<int>(); foreach (int r in rings) { bool any = false; foreach (var kv in reserved) if (kv.Value == "ring" + r && PlanetCity.IsRoad(kv.Key)) { any = true; break; } if (any) keep.Add(r); } rings.IntersectWith(keep); }   // een ring die weg is mag opnieuw
    }
    // per vak: welke zet het legde (de eerste keer), om het wegennet achteraf te kunnen beoordelen (PlanetAI.Eval.cs)
    public static readonly Dictionary<int, string> Origin = new Dictionary<int, string>();
    public static void Commit(Move m)
    {
        Moves++; think = 0; cands = null;
        { string why = m.what ?? m.type; int cut = why.IndexOf(" ("); if (cut > 0) why = why.Substring(0, cut); if (m.line != null) foreach (int c in m.line) { if (!Origin.ContainsKey(c)) Origin[c] = why; } else if (!Origin.ContainsKey(m.cell)) Origin[m.cell] = why; }
        if (m.line != null) foreach (int c in m.line) Owned.Add(c); else Owned.Add(m.cell);
        if (m.type == "rotonde") foreach (int j in Planet.Cells[m.cell].nb) Owned.Add(j);
        if (m.rest != null) for (int i = m.rest.Count - 1; i >= 0; i--) Plan(m.rest[i], true);   // het vervolg van de route eerst
        if (m.clearFor >= 0)   // ruimte maken voor een rotonde: de ring van dat kruispunt blijft vrij tot de rotonde er ligt (of 20 zetten voorbij zijn)
            foreach (int j in Planet.Cells[m.clearFor].nb) if (!reserved.ContainsKey(j) || reserved[j] == "ruimte") { reserved[j] = "ruimte"; roomFor[j] = (m.clearFor, Moves); }
        if (m.queued) return;
        // symmetrie: dezelfde zijstraat om de 60° rondom het centrum, een blikvanger om de 120°
        if (m.sym && m.line != null)
            for (int k = 1; k < 6; k++) Plan(new Planned { a = Turn(m.line[0], k), b = Turn(m.line[m.line.Count - 1], k), type = m.type, style = m.style, bike = m.bike, strict = true, what = m.what.Replace("legt een", "legt rondom een") + " (symmetrisch)" });
        if (m.line == null && (m.type == "plein" || m.type == "blok" || m.type == "molen" || m.type == "meer") && D(m.cell) >= 1)
            foreach (int k in new[] { 2, 4 }) Plan(new Planned { kind = "blok", cells = new List<int> { Turn(m.cell, k) }, type = m.type, what = m.what + " (symmetrisch)" });
    }
    public static void Reject(Move m) { Rejected++; think = 0; cands = null; banned.Add(KeyOf(m)); }
}
