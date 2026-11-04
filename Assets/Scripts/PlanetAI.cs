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
public static class PlanetAI
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
        public float Score => rule + Mathf.Clamp(beauty, -3f, 3f) * 0.9f;
    }
    // een gepland recht stuk: de vakken zelf, of van vak a naar vak b (LinePlan bij het leggen)
    public class Planned { public int a, b; public List<int> cells; public string type, what; public bool bike; public byte style; }

    public const string Version = "1 (4 nov 2026)";   // ophogen bij een nieuwe versie van de planner, voor het scoreboek (ai-scores.txt)
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
    public static string Begin(int start, int seed)
    {
        if (Planet.IsPole(start) || System.Array.IndexOf(Planet.Starts, start) >= 0) return "Niet op een pool of een startplek: kies een ander vak";
        if (PlanetCity.Cells.ContainsKey(start) && !Wild(start)) return "Kies een leeg vak (of wild bos) als beginpunt";
        if (PlanetCity.Wet(start)) return "Niet in het water: kies een vak op het land";
        if (Slope(start) > 4f) return "Te steil om te beginnen: kies een vlakker vak";
        Reset(); Start = start; rnd = new System.Random(seed);
        dist[start] = 0; var q = new Queue<int>(); q.Enqueue(start);
        while (q.Count > 0) { int c = q.Dequeue(); if (dist[c] >= MaxRange + 1) continue; foreach (int j in Planet.Cells[c].nb) if (!dist.ContainsKey(j)) { dist[j] = dist[c] + 1; q.Enqueue(j); } }
        var S = Planet.Cells[start]; Vector3 n0 = Planet.Cells[S.nb[0]].dir - S.dir; n0 -= S.dir * Vector3.Dot(n0, S.dir);
        a0 = Mathf.Atan2(Vector3.Dot(n0, S.north), Vector3.Dot(n0, S.east));
        Skeleton(); core = 0; return null;
    }
    public static void Reset() { Start = -1; Owned.Clear(); Moves = Rejected = 0; dist.Clear(); core = -1; coreRetry = 0; coreRoad = plein = -1; rings.Clear(); reserved.Clear(); queue.Clear(); banned.Clear(); cands = null; think = 0; }

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
    static bool Forbidden(int c) => Planet.IsPole(c) || System.Array.IndexOf(Planet.Starts, c) >= 0;
    static float Slope(int c) { float g = PlanetCity.G(c), m = 0f; foreach (int j in Planet.Cells[c].nb) m = Mathf.Max(m, Mathf.Abs(PlanetCity.G(j) - g)); return m; }
    static bool Land(int c) => !Forbidden(c) && !PlanetCity.Wet(c) && Slope(c) <= 4f && PlanetCity.G(c) < 48f && PlanetCity.RotOf(c) < 0;   // op het land, niet te steil, niet in de sneeuw, niet op een rotonde
    static bool Buildable(int c) => Empty(c) && !reserved.ContainsKey(c) && Land(c);   // een leeg vak waar iets op kan (niet op het raamwerk)
    static bool Clearable(int c) => Wild(c) && !reserved.ContainsKey(c) && Land(c);     // bomen kappen om er te bouwen
    static bool GroundRoad(int c) => PlanetCity.IsRoad(c) && !PlanetCity.Underpass(c) && PlanetCity.RotOf(c) < 0 && (Owned.Contains(c) || PlanetCity.StyleOf(c) == 0);
    static bool NearRoad(int c) { foreach (int j in Planet.Cells[c].nb) if (PlanetCity.IsRoad(j) && !PlanetCity.Underpass(j)) return true; return false; }
    static bool NearWalk(int c) { foreach (int j in Planet.Cells[c].nb) if ((PlanetCity.IsRoad(j) && !PlanetCity.Underpass(j)) || PlanetCity.IsPath(j)) return true; return false; }
    static bool NearType(int c, string t) { foreach (int j in Planet.Cells[c].nb) if (PlanetCity.Cells.TryGetValue(j, out var l) && l.Contains(t)) return true; return false; }
    static bool IsBuilding(List<string> l) => l.Count > 0 && l[0] != "weg" && l[0] != "wegdeel" && l[0] != "park" && l[0] != "pad" && l[0] != "water" && l[0] != "meer";
    // buren met een gebouw (geen weg, pad of park: anders kweekt een park steeds nieuwe parken) en buren met een park
    static int BuiltNb(int c) { int n = 0; foreach (int j in Planet.Cells[c].nb) if (PlanetCity.Cells.TryGetValue(j, out var l) && IsBuilding(l)) n++; return n; }
    static int ParkNb(int c) { int n = 0; foreach (int j in Planet.Cells[c].nb) if (PlanetCity.Cells.TryGetValue(j, out var l) && l.Count == 1 && l[0] == "park") n++; return n; }
    static int D(int c) => dist.TryGetValue(c, out int d) ? d : 999;
    static bool InRange(int c, float extra = 0f) => D(c) <= Range + extra;
    static int Floors(int c) { int n = 0; if (PlanetCity.Cells.TryGetValue(c, out var l)) foreach (var b in l) if (b == "woning" || b == "kantoor" || b == "winkel") n++; return n; }
    static int TargetFloors(int d) => d <= 1 ? 4 : d <= 2 ? 3 : d <= 4 ? 2 : 1;
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
        for (int k = 0; k < 6; k++) { var s = Side(k, CanalR); if (s != null) foreach (int c in s) if (!reserved.ContainsKey(c)) reserved[c] = "gracht"; }
    }
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
                foreach (int h in PlanetCity.Between(line[k - 1], c)) if (!Free(h) && !PlanetCity.IsHalf(h)) return false;
            bool walk = GroundRoad(c) || (!road && PlanetCity.IsPath(c));
            if (k == 0) { if (first == 0 ? !Free(c) : first == 1 ? !walk : !(Free(c) || walk)) return false; continue; }
            if (Free(c)) { if (PlanetCity.Wet(c) || PlanetCity.G(c) > 60f) return false; fresh++; }
            else if (road && last && GroundRoad(c) && (Owned.Contains(c) ? PlanetCity.LinkCount(c) <= 2 : PlanetCity.DeadEnd(c))) joins = true;   // aansluiten op een eigen straat, of op een doodlopende weg van de speler
            else if (road && !last && Owned.Contains(c) && GroundRoad(c) && PlanetCity.LinkCount(c) <= 2 && (pattern || !PlanetCity.DeadEnd(c))) { }   // een eigen straat kruisen: een kruispunt
            else if (!road && Owned.Contains(c) && GroundRoad(c)) { if (last) joins = true; }   // een pad over een eigen straat: een oversteek (ook als eind)
            else if (!road && Owned.Contains(c) && PlanetCity.IsPath(c) && (last || pattern)) joins = true;   // aansluiten op een eigen pad
            else return false;
            if (k > 0) steep = Mathf.Max(steep, Mathf.Abs(PlanetCity.G(c) - PlanetCity.G(line[k - 1])));
        }
        if (fresh == 0) return false;
        if (steep > 2.6f && (!road || PlanetCity.BorePlan(line, "weg").Count == 0)) return false;   // te steil, en geen tunnel mogelijk
        if (pattern) return true;
        // niet vlak langs een andere straat (geen twee straten naast elkaar), behalve waar hij aansluit of kruist; het eerste nieuwe vak
        // van een zijstraat raakt altijd de buren van het kruispunt, dat telt niet. Een pad niet naast een ander pad. Niet over het raamwerk.
        for (int k = 2; k < line.Count; k++)
        {
            int c = line[k]; if (!Free(c)) continue;
            if (reserved.ContainsKey(c)) return false;   // het raamwerk (ringen, spaken, gracht) blijft vrij voor zijn eigen stuk
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
        return new Move { kind = "lijn", type = "weg", line = line, what = what, joins = joins, rule = bonus + 0.3f * open + (joins ? 2.2f : 0f) - 0.25f * Mathf.Max(0, line.Count - 6) - 0.3f * dg + (float)rnd.NextDouble() * 0.3f };
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
        while (open.Count > 0)
        {
            var (cc, c) = open.Min; open.Remove(open.Min); if (cc > cost[c] || cc > limit || cc >= endCost) break;
            foreach (int j in Planet.Cells[c].nb)
            {
                if (j == a || Forbidden(j) || !InRange(j, 0.5f)) continue; bool g = goals.Contains(j);
                float step;
                if (Free(j)) { if (PlanetCity.Wet(j) || PlanetCity.G(j) > 60f) continue; step = reserved.ContainsKey(j) ? 0.6f : 1f; }
                else if (g) step = 1f;
                else if (Owned.Contains(j) && GroundRoad(j) && PlanetCity.LinkCount(j) <= 2) step = 2.5f;   // een eigen straat kruisen
                else continue;
                float dg = Mathf.Abs(PlanetCity.G(j) - PlanetCity.G(c)); if (dg > 2.6f) continue; step += dg * 0.3f;
                int p = prev[c]; if (p >= 0) { float dot = Vector3.Dot(Tangent(p, c), Tangent(c, j)); step += dot > 0.95f ? 0f : dot > 0.3f ? 0.6f : 4f; }
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

    // ---------- patronen: ringen, gracht en spaken uit het raamwerk, als geplande rechte stukken ----------
    static void Plan(Planned p, bool front = false) { if (front) queue.Insert(0, p); else queue.Add(p); }
    static void PlanLine(int a, int b, string type, byte style, bool bike, string what) => Plan(new Planned { a = a, b = b, type = type, style = style, bike = bike, what = what });
    // het volgende geplande stuk dat kan; wat niet kan krijgt een omweg (Dijkstra) of vervalt
    static Move NextQueued()
    {
        while (queue.Count > 0)
        {
            var p = queue[0]; queue.RemoveAt(0);
            var line = p.cells ?? PlanetCity.LinePlan(p.a, p.b); if (line == null || line.Count < 2) continue;
            if (p.type == "water")   // de gracht: alleen de vrije vakken, in stukken
            {
                var run = new List<int>(); var runs = new List<List<int>>();
                foreach (int c in line) { if (Free(c) && !PlanetCity.Wet(c) && !Forbidden(c)) run.Add(c); else { if (run.Count >= 2) runs.Add(run); run = new List<int>(); } }
                if (run.Count >= 2) runs.Add(run); if (runs.Count == 0) continue;
                var m = new Move { kind = "lijn", type = "water", line = runs[0], what = p.what, rule = 8f };
                if (runs.Count > 1) { m.rest = new List<Planned>(); for (int i = 1; i < runs.Count; i++) m.rest.Add(new Planned { cells = runs[i], type = "water", what = p.what }); }
                if (!Banned(m)) return m; continue;
            }
            if (!CheckLine(line, p.type, out bool joins, 2, true))
            {
                if (p.type != "weg" || p.cells != null) continue;   // een pad of een stuk route dat niet kan: vervalt
                int a = line[0], b = line[line.Count - 1]; if (!(Free(a) || GroundRoad(a))) continue;
                var path = Route(a, new HashSet<int> { b }, line.Count * 2.5f + 6f); if (path == null) continue;   // een omweg om het obstakel heen
                var runs = Runs(path, "weg", p.style, p.what + " (omweg)"); for (int i = runs.Count - 1; i >= 0; i--) Plan(runs[i], true);
                continue;
            }
            var mv = new Move { kind = "lijn", type = p.type, bike = p.bike, style = p.style, line = line, what = p.what, joins = joins, rule = 8f };
            if (!Banned(mv)) return mv;
        }
        return null;
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
            rings.Add(r);
            if (i == 1) for (int k = 0; k < 6; k++) PlanLine(Corner(k, CanalR), Corner(k + 1, CanalR), "water", 0, false, "graaft de gracht rond het centrum");
            for (int k = 0; k < 6; k++) PlanLine(Corner(k, r), Corner(k + 1, r), "weg", 1, false, "legt de ringweg op " + r + " vakken (deel " + (k + 1) + " van 6)");
            if (i == 0) for (int k = 1; k < 6; k += 2) PlanLine(Corner(k, 2), Corner(k, 4), "pad", 0, true, "legt een fietspad naar de ringweg");   // tussen de hoofdstraat en de zijstraten door
            else for (int k = i % 2; k < 6; k += 2) if (!PlanetCity.IsRoad(Corner(k, (RingR[i - 1] + r) / 2))) PlanLine(Corner(k, RingR[i - 1]), Corner(k, r), "weg", 0, false, "legt een spaak naar de nieuwe ring");
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
                    m = new Move { kind = "lijn", type = "weg", style = 2, line = best, what = "legt de hoofdstraat (een laan) door het centrum", rule = 10f }; break;
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
        float parkPen = parks > bld * 0.4f ? -3f : parks > bld * 0.25f ? -1.2f : 0f;   // genoeg groen: liever bouwen
        float pathWant = pads < bld * 0.12f ? 0.8f : pads < bld * 0.25f ? 0f : -1.5f;
        float shopWant = shops < homes * 0.12f ? 1.6f : shops < homes * 0.2f ? 0.4f : -1f;
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
                    if (d <= 2) cands.Add(new Move { kind = "blok", type = "kantoor", cell = c, what = "bouwt een kantoor in het centrum", rule = 1.2f + inward + nb + noise });
                    if (road && pleinen < 1 + Owned.Count / 40 && !Within(c, "plein", 3)) cands.Add(new Move { kind = "blok", type = "plein", cell = c, what = d <= 2 ? "legt een plein aan" : "legt een plein aan als hart van de wijk", rule = 1.3f + 0.5f * inward + nb + noise });
                    if (d >= 1 && d <= 3 && torens < 1 + Owned.Count / 120 && Owned.Count > 12) cands.Add(new Move { kind = "blok", type = "blok", cell = c, what = "zet een sokkel neer voor een toren", rule = 1.4f + nb + noise });
                    if (rel > 0.55f && molens < 1 + Owned.Count / 150 && Owned.Count > 30 && built == 1) cands.Add(new Move { kind = "blok", type = "molen", cell = c, what = "zet een molen aan de rand van de stad", rule = 1.3f + noise });
                    cands.Add(new Move { kind = "blok", type = "park", cell = c, what = "legt een parkje aan de straat", rule = 0.3f + nb + (hole ? 0.6f : 0f) + parkPen - 0.4f * ParkNb(c) + noise });
                    if (NearType(c, "plein") || NearType(c, "park")) pathStarts.Add(c);
                }
                else if (built >= 2) cands.Add(new Move { kind = "blok", type = "park", cell = c, what = hole ? "vult een gat met een park" : "legt een park aan tussen de huizen", rule = 0.9f + nb + (hole ? 0.8f : 0f) + parkPen - 0.4f * ParkNb(c) + noise });
                else if (built == 1 && rel > 0.5f) cands.Add(new Move { kind = "blok", type = "park", cell = c, what = "plant bomen aan de rand", rule = 0.1f + parkPen - 0.4f * ParkNb(c) + noise });
                if (!walk && rel > 0.45f && built == 1 && PlanetCity.G(c) < 3f && meren < 1 + Owned.Count / 120 && Owned.Count > 40 && !Within(c, "meer", 4) && !Within(c, "water", 2)) cands.Add(new Move { kind = "blok", type = "meer", cell = c, what = "graaft een meertje in de laagte", rule = 1.1f + noise });
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
                if (top == "woning" && fl == 1 && rel > 0.55f && daken < 1 + Owned.Count / 30) cands.Add(new Move { kind = "blok", type = "dak", cell = c, what = "zet een groot boerendak op het huis", rule = 0.8f + noise });
                if (top == "blok") cands.Add(new Move { kind = "blok", type = "toren", cell = c, what = "zet een toren op de sokkel, als blikvanger", rule = 3f + noise });
                // een rotonde op een eigen kruispunt met ruimte eromheen (het vak en de zes ernaast vrij of weg), niet vlak bij een andere
                int roadNb = 0; foreach (int j in Planet.Cells[c].nb) if (PlanetCity.IsRoad(j)) roadNb++;
                if (GroundRoad(c) && roadNb >= 3 && rotondes < 1 + Owned.Count / 90 && Owned.Count > 50)
                {
                    bool ok = !PlanetCity.Underpass(c);   // zoals PlaceRoundabout: het vak en de zes ernaast leeg, of alleen een weg of pad
                    foreach (int j in Planet.Cells[c].nb) { if (!(Empty(j) || PlanetCity.IsWalk(j) || PlanetCity.IsHalf(j)) || PlanetCity.Underpass(j) || PlanetCity.RotOf(j) >= 0) ok = false; foreach (int k2 in Planet.Cells[j].nb) if (PlanetCity.IsRot(k2)) ok = false; }
                    if (ok) cands.Add(new Move { kind = "blok", type = "rotonde", cell = c, what = "maakt een rotonde van het kruispunt", rule = 1.4f + (reserved.TryGetValue(c, out var rk) && rk.StartsWith("ring") ? 0.5f : 0f) + noise });
                }
            }
        }
        // straten: verlengen, aftakken, aansluiten
        float roadWant = frontier <= 3 ? 2.2f : frontier <= 8 ? 0.6f : -1.4f;
        var ends = new List<int>(); foreach (int c in roads) if (PlanetCity.DeadEnd(c)) ends.Add(c);
        var goals = new HashSet<int>(); foreach (int c in roads) if (PlanetCity.LinkCount(c) <= 2) goals.Add(c);
        foreach (var kv in dist) if (kv.Value <= R && !Owned.Contains(kv.Key) && PlanetCity.DeadEnd(kv.Key) && PlanetCity.StyleOf(kv.Key) == 0) goals.Add(kv.Key);   // ook een doodlopende weg van de speler
        foreach (int e in ends)
        {
            var p = PlanetCity.Partners(e); if (p.Count != 1) continue; Vector3 t = Tangent(p[0], e); int before = cands.Count;
            var line = Line(e, t, 3 + rnd.Next(3), "weg", out bool joins);
            if (line != null) { var m = RoadMove(line, joins, joins ? "verbindt twee straten met elkaar" : "verlengt de straat naar het " + Compass(e, t), roadWant + 0.5f); if (m != null) cands.Add(m); }
            // een doodlopend eind met Dijkstra naar de dichtstbijzijnde straat (niet zijn eigen buur): een rondje, in rechte stukken
            var g2 = new HashSet<int>(goals); g2.Remove(e); g2.Remove(p[0]); foreach (int j in Planet.Cells[e].nb) g2.Remove(j);
            var path = Route(e, g2, 10f);
            if (path != null && path.Count >= 3)
            {
                var runs = Runs(path, "weg", 0, "sluit de straat aan (vervolg)");
                var m = new Move { kind = "lijn", type = "weg", line = runs[0].cells, joins = runs.Count == 1, what = runs.Count == 1 ? "sluit de straat aan op een andere straat" : "sluit de straat aan, met een bocht", rule = roadWant + 2.2f - 0.15f * path.Count + (float)rnd.NextDouble() * 0.3f };
                if (runs.Count > 1) m.rest = runs.GetRange(1, runs.Count - 1);
                if (CheckLine(m.line, "weg", out _, 1, true)) cands.Add(m);
            }
            // opnieuw indelen: een eigen doodlopend stukje dat niet verder kan en niets ontsluit gaat weer weg (de buur blijft netjes:
            // een kruispunt wordt een bocht of een recht stuk, of het stukje wordt vak voor vak korter)
            if (cands.Count == before && !reserved.ContainsKey(e) && Opens(new List<int> { e }) == 0)
                cands.Add(new Move { kind = "blok", type = "gum", cell = e, what = "ruimt een doodlopend stukje straat op", rule = 0.9f + (PlanetCity.LinkCount(p[0]) >= 3 ? 0.5f : 0f) + (float)rnd.NextDouble() * 0.3f });
        }
        // een eigen pad dat nergens heen gaat (geen huizen ernaast, geen park of plein aan het eind) gaat ook weg
        foreach (int e in pathEnds)
        {
            if (BuiltNb(e) > 0 || NearType(e, "park") || NearType(e, "plein") || PlanetCity.IsBike(e)) continue;
            cands.Add(new Move { kind = "blok", type = "gum", cell = e, what = "haalt een pad weg dat nergens heen gaat", rule = 0.7f + (float)rnd.NextDouble() * 0.3f });
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
                var m = RoadMove(line, joins, "legt een nieuwe straat naar het " + Compass(c, dir), roadWant + 0.1f); if (m != null) cands.Add(m);
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
        if (banned.Count > 0) cands.RemoveAll(Banned);
        cands.Sort((x, y) => y.rule.CompareTo(x.rule));
        Status = cands.Count + " mogelijke zetten, " + frontier + " vrije kavels";
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

    public static void Commit(Move m)
    {
        Moves++; think = 0; cands = null;
        if (m.line != null) foreach (int c in m.line) Owned.Add(c); else Owned.Add(m.cell);
        if (m.type == "rotonde") foreach (int j in Planet.Cells[m.cell].nb) Owned.Add(j);
        if (m.rest != null) for (int i = m.rest.Count - 1; i >= 0; i--) Plan(m.rest[i], true);   // het vervolg van de route eerst
    }
    public static void Reject(Move m) { Rejected++; think = 0; cands = null; banned.Add(KeyOf(m)); }
}
