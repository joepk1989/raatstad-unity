using System.Collections.Generic;
using UnityEngine;

// Soorten dorpen voor Hele planeet vol (PlanetFill.cs), zodat niet elk stadje hetzelfde is:
// - gehucht: één weg erdoor, een paar boerderijen met een groot dak, een molen en een boomgaard;
// - kerkdorp: een lintdorp, één rechte hoofdstraat met de kerk op een pleintje ernaast, winkels in het hart;
// - brinkdorp: een driehoekige brink (gras, bomen, een plein met fontein) binnen drie straten langs de assen van het raster;
// - marktstad: het rijke voorbeeldstadje (Sample) met ringweg, verdiepte hoofdweg en bruggen;
// - stad (een paar per planeet): een ruit van 12 bij 12 vakken met straten om de 3 vakken langs twee assen van het raster (blokken
//   met hoeken van 60° en 120°), hoge kantoren en torens in het centrum, winkels en woningen naar buiten toe.
// Alles met de gewone bouwstenen (PlaceLine, Place); vast bepaald door de seed. De wegen naar buiten legt FillSteps (Connect),
// vanaf de doodlopende einden, zodat er zo min mogelijk blijven.
public static partial class PlanetCity
{
    public enum Village { Gehucht, Kerkdorp, Brinkdorp, Marktstad, Stad }
    public static readonly List<Village> FillKinds = new List<Village>();   // het soort van elk stadje in FillTowns
    // hoeveel vakken rond het midden het dorp nodig heeft (voor de afstand tussen de dorpen)
    public static int VillageRadius(Village v) => v == Village.Gehucht ? 3 : v == Village.Marktstad ? 7 : v == Village.Stad ? 10 : 5;
    public static string VillageName(Village v) => v == Village.Gehucht ? "gehucht" : v == Village.Kerkdorp ? "kerkdorp" : v == Village.Brinkdorp ? "brinkdorp" : v == Village.Marktstad ? "marktstad" : "stad";

    // het soort voor een plek: aan de voet van de bergen alleen een gehucht, verder naar kans
    public static Village PickVillage(System.Random rnd, Vector3 d)
    {
        double r = rnd.NextDouble();
        if (MountainMask(d) > 0f) return Village.Gehucht;
        return r < 0.25 ? Village.Gehucht : r < 0.6 ? Village.Kerkdorp : r < 0.85 ? Village.Brinkdorp : Village.Marktstad;
    }

    public static string BuildVillage(int s, int seed, Village v)   // null: geplaatst; anders de reden waarom niet
    {
        if (v == Village.Marktstad) return Sample(s, seed, true);
        var S = Planet.Cells[s]; var rnd = new System.Random(seed); int R = VillageRadius(v);
        var dist = new Dictionary<int, int> { { s, 0 } }; var q = new Queue<int>(); q.Enqueue(s);
        while (q.Count > 0) { int c = q.Dequeue(); if (dist[c] >= R + 1) continue; foreach (int j in Planet.Cells[c].nb) if (!dist.ContainsKey(j)) { dist[j] = dist[c] + 1; q.Enqueue(j); } }
        foreach (var kv in dist) if (Cells.ContainsKey(kv.Key) || Planet.IsPole(kv.Key) || System.Array.IndexOf(Planet.Starts, kv.Key) >= 0) return "Hier is geen ruimte voor een dorp";
        byte keepStyle = DragStyle; bool keepOne = DragOneWay, keepBike = DragBike; int keepLevel = DragLevel; bool keepUnder = DragUnder;
        DragStyle = 0; DragOneWay = false; DragBike = false; DragLevel = 0; DragUnder = false;
        System.Func<float, float, int> cellAt = (ang, r) => Planet.Nearest((S.dir * Mathf.Cos(r / Planet.R) + (S.east * Mathf.Cos(ang) + S.north * Mathf.Sin(ang)) * Mathf.Sin(r / Planet.R)).normalized * Planet.R);
        Vector3 n0 = Planet.Cells[S.nb[0]].dir - S.dir; n0 -= S.dir * Vector3.Dot(n0, S.dir);
        float a0 = Mathf.Atan2(Vector3.Dot(n0, S.north), Vector3.Dot(n0, S.east)) + rnd.Next(6) * Mathf.PI / 3f;   // een willekeurige van de zes richtingen
        const float P = 15.6f, D60 = Mathf.PI / 3f;
        System.Func<int, bool> nearRoad = c => { foreach (int j in Planet.Cells[c].nb) if (IsRoad(j)) return true; return false; };
        System.Action<int, int> house = (c, fl) => { for (int f = 0; f < fl; f++) Place(c, "woning"); };
        int floors = 1 + rnd.Next(2);   // dit dorp: laag of wat hoger
        switch (v)
        {
            case Village.Gehucht:
            {
                var road = LinePlan(cellAt(a0 + Mathf.PI, (R + 0.4f) * P), cellAt(a0, (R + 0.4f) * P)); if (road != null) PlaceLine(road, "weg");
                int farms = 3 + rnd.Next(4), mill = rnd.Next(2);
                var spots = new List<int>(); foreach (var kv in dist) if (kv.Value <= R && !Cells.ContainsKey(kv.Key) && nearRoad(kv.Key)) spots.Add(kv.Key);
                Shuffle(spots, rnd);
                foreach (int c in spots) { if (farms-- <= 0) break; Place(c, "woning"); Place(c, "dak"); }   // boerderij: een groot puntdak
                foreach (var kv in dist)
                {
                    int c = kv.Key; if (Cells.ContainsKey(c) || kv.Value > R) continue; double r = rnd.NextDouble();
                    if (mill > 0 && kv.Value >= 2 && !nearRoad(c) && r < 0.3) { Place(c, "molen"); mill--; }
                    else if (r < 0.45) Place(c, "park");   // boomgaard en houtwal
                }
                break;
            }
            case Village.Kerkdorp:   // lintdorp: één rechte hoofdstraat, de kerk op een pleintje ernaast, huizen en winkels langs de straat
            {
                var road = LinePlan(cellAt(a0 + Mathf.PI, (R + 0.4f) * P), cellAt(a0, (R + 0.4f) * P)); if (road != null) PlaceLine(road, "weg");
                int church = cellAt(a0 + (rnd.Next(2) == 0 ? 1 : -1) * Mathf.PI / 2f, 0.9f * P);   // naast het midden, aan een kant van de straat
                if (!Cells.ContainsKey(church)) { Place(church, "toren"); foreach (int j in Planet.Cells[church].nb) if (!Cells.ContainsKey(j) && (nearRoad(j) || rnd.NextDouble() < 0.5)) Place(j, "plein"); }
                foreach (var kv in dist)
                {
                    int c = kv.Key, d = kv.Value; if (Cells.ContainsKey(c) || d > R) continue; double r = rnd.NextDouble();
                    if (nearRoad(c) && d <= 2 && r < 0.5) { Place(c, "winkel"); if (floors > 1) Place(c, "woning"); }   // in het hart: winkels met een bovenwoning
                    else if (nearRoad(c) && r < 0.9) house(c, floors + (d <= 2 && r < 0.3 ? 1 : 0));
                    else if (r < 0.4) Place(c, "park");   // achtertuinen en boomgaarden
                }
                break;
            }
            case Village.Brinkdorp:   // brinkdorp: een driehoekige brink (zijden van 6 vakken langs de assen van het raster) met huizen eromheen
            {
                var tri = new List<int>(); for (int k = 0; k < 3; k++) tri.Add(cellAt(a0 + Mathf.PI / 2f + k * 2f * D60, 2f * Mathf.Sqrt(3f) * P));
                for (int k = 0; k < 3; k++) { var side = LinePlan(tri[k], tri[(k + 1) % 3]); if (side != null) PlaceLine(side, "weg"); }
                Place(s, "plein");   // midden op de brink: een plein met fontein, eromheen gras en bomen
                var inside = new HashSet<int>(); { var q2 = new Queue<int>(); q2.Enqueue(s); inside.Add(s); while (q2.Count > 0) { int c = q2.Dequeue(); foreach (int j in Planet.Cells[c].nb) if (!IsRoad(j) && dist.ContainsKey(j) && dist[j] <= 3 && inside.Add(j)) q2.Enqueue(j); } }
                foreach (var kv in dist)
                {
                    int c = kv.Key, d = kv.Value; if (Cells.ContainsKey(c) || d > R) continue; double r = rnd.NextDouble();
                    if (inside.Contains(c)) { if (r < 0.6) Place(c, "park"); }
                    else if (nearRoad(c) && r < 0.12) Place(c, "winkel");
                    else if (nearRoad(c) && r < 0.85) house(c, floors);
                    else if (r < 0.35) Place(c, "park");
                }
                break;
            }
            case Village.Stad:
            {
                // vlakke coördinaten (meters) vanuit het midden naar een vak op de bol
                Vector2 eA = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * P, eB = new Vector2(Mathf.Cos(a0 + D60), Mathf.Sin(a0 + D60)) * P;
                System.Func<float, float, int> grid = (ga, gb) => { Vector2 v2 = eA * ga + eB * gb; return cellAt(Mathf.Atan2(v2.y, v2.x), v2.magnitude); };
                for (int k = -6; k <= 6; k += 3)
                {
                    var la = LinePlan(grid(-6, k), grid(6, k)); if (la != null) PlaceLine(la, "weg");
                    var lb = LinePlan(grid(k, -6), grid(k, 6)); if (lb != null) PlaceLine(lb, "weg");
                }
                foreach (var kv in dist)
                {
                    int c = kv.Key, d = kv.Value; if (Cells.ContainsKey(c) || d > R) continue; double r = rnd.NextDouble();
                    // binnen de ruit? (de vakken buiten de buitenste straten blijven leeg)
                    Vector3 w = Planet.Cells[c].dir - S.dir; Vector2 xy = new Vector2(Vector3.Dot(w, S.east), Vector3.Dot(w, S.north)) * Planet.R;
                    float det = eA.x * eB.y - eA.y * eB.x, ga = (xy.x * eB.y - xy.y * eB.x) / det, gb = (eA.x * xy.y - eA.y * xy.x) / det;
                    if (Mathf.Abs(ga) > 6.3f || Mathf.Abs(gb) > 6.3f) continue;
                    if (d <= 2) { if (r < 0.2) Place(c, "toren"); else if (r < 0.3) Place(c, "plein"); else { int fl = 4 + rnd.Next(4); for (int f = 0; f < fl; f++) Place(c, "kantoor"); } }
                    else if (d <= 5) { if (r < 0.1) Place(c, "park"); else if (r < 0.4) { Place(c, "winkel"); house(c, 1 + rnd.Next(2)); } else { int fl = 2 + rnd.Next(3); for (int f = 0; f < fl; f++) Place(c, "kantoor"); } }
                    else if (r < 0.15) Place(c, "park");
                    else house(c, 2 + rnd.Next(2));
                }
                break;
            }
        }
        // een straat die één vak naast een andere straat doodloopt (op de bol komt een lijn niet altijd precies uit): daar aansluiten
        foreach (var kv in dist)
        {
            int c = kv.Key; if (!DeadEnd(c)) continue;
            foreach (int j in Planet.Cells[c].nb) if (IsRoad(j) && !Linked(c, j) && WalkLevel(j) == 0 && !Underpass(j)) { Link(c, j); break; }
        }
        DragStyle = keepStyle; DragOneWay = keepOne; DragBike = keepBike; DragLevel = keepLevel; DragUnder = keepUnder; TouchAll(); return null;
    }

    static void Shuffle<T>(List<T> l, System.Random rnd) { for (int i = l.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); (l[i], l[j]) = (l[j], l[i]); } }
}
