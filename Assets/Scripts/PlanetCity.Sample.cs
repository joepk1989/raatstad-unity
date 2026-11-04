using System.Collections.Generic;
using UnityEngine;

// Een voorbeeldstadje rond een vak (Sample), voor de startstad en de volle planeet.
public static partial class PlanetCity
{
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
}
