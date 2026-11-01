using System.Collections.Generic;
using UnityEngine;

// De hele planeet vol met wat er nu is: voorbeeldstadjes (Sample, met winkels, pleinen en torens) verspreid over de bol, op ruim
// 15 vakken van elkaar, en daartussen platteland: heuvels (terrein tot 30 m, vlak rond de stadjes), bossen, meertjes in de laagtes,
// molens en boerderijtjes. Vast bepaald door de seed.
public static partial class PlanetCity
{
    public static string FillTimes = ""; static int linked;
    public static readonly List<int> FillTowns = new List<int>();   // de middens van de stadjes van de laatste FillPlanet
    public static int FillPlanet(int seed = 11) { var e = FillSteps(null, seed); while (e.MoveNext()) { } return FillTowns.Count; }
    // in stappen, zodat een laadscherm kan bewegen: prog(deel 0..1, wat er gebeurt); na elke stap één beeld
    public static System.Collections.IEnumerator FillSteps(System.Action<float, string> prog, int seed = 11)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew(); ClearAll();
        var rnd = new System.Random(seed); int n = Planet.Cells.Length;
        var order = new int[n]; for (int i = 0; i < n; i++) order[i] = i;
        for (int i = n - 1; i > 0; i--) { int j = rnd.Next(i + 1); (order[i], order[j]) = (order[j], order[i]); }
        float cosMin = Mathf.Cos(15.5f * 15.6f / Planet.R);
        var towns = FillTowns; towns.Clear();
        for (int oi = 0; oi < n; oi++)
        {
            int c = order[oi]; Vector3 d = Planet.Cells[c].dir; bool ok = true;
            foreach (int t in towns) if (Vector3.Dot(d, Planet.Cells[t].dir) > cosMin) { ok = false; break; }
            if (ok && Sample(c, rnd.Next(), true) == null) { towns.Add(c); prog?.Invoke(0.75f * oi / n, "Stadjes bouwen (" + towns.Count + ")"); yield return null; }
        }
        // wegen tussen de stadjes (voor het platteland, dat er dan omheen komt)
        var roadDist = new Dictionary<int, int>();   // afstand in vakken tot een verbindingsweg (tot 6)
        {
            var edges = TownTree(towns); var q = new Queue<int>(); int e = 0; linked = 0;
            foreach (var (a, b) in edges)
            {
                var line = Connect(a, b); e++;
                if (line != null) linked++;
                if (line != null) foreach (int c in line) if (!roadDist.ContainsKey(c)) { roadDist[c] = 0; q.Enqueue(c); }
                prog?.Invoke(0.75f + 0.15f * e / edges.Count, "Wegen tussen de stadjes (" + e + "/" + edges.Count + ")"); if (e % 3 == 0) yield return null;
            }
            while (q.Count > 0) { int c = q.Dequeue(); if (roadDist[c] >= 6) continue; foreach (int j in Planet.Cells[c].nb) if (!roadDist.ContainsKey(j)) { roadDist[j] = roadDist[c] + 1; q.Enqueue(j); } }
        }
        prog?.Invoke(0.9f, "Platteland: heuvels, bossen en meertjes"); yield return null;
        long tTowns = sw.ElapsedMilliseconds;
        // platteland: alles wat nog leeg is
        for (int c = 0; c < n; c++)
        {
            if (Cells.ContainsKey(c) || Planet.IsPole(c)) continue;
            Vector3 d = Planet.Cells[c].dir;
            float near = 1e9f; foreach (int t in towns) near = Mathf.Min(near, Mathf.Acos(Mathf.Clamp(Vector3.Dot(d, Planet.Cells[t].dir), -1f, 1f)));
            float flag = 1e9f; foreach (int st in Planet.Starts) flag = Mathf.Min(flag, Mathf.Acos(Mathf.Clamp(Vector3.Dot(d, Planet.Cells[st].dir), -1f, 1f)));
            near *= Planet.R / 15.6f; flag *= Planet.R / 15.6f;   // in vakken
            if (flag < 2.5f) continue;   // rond de vlaggen van de startplekken blijft het vrij
            float k = Mathf.Clamp01((Mathf.Min(near, flag + 4f) - 8.5f) / 5f);   // vlak bij de stadjes, heuvels verder weg
            if (roadDist.TryGetValue(c, out int rd)) k = Mathf.Min(k, Mathf.Clamp01((rd - 1.5f) / 4f));   // en vlak langs de wegen ertussen
            float h = Mathf.Max(0f, (Noise(d, 1f / 140f, 0f) - 0.4f) / 0.6f) * 30f * k;
            if (h > 0.3f) SetTerr(c, h);
            float wet = Noise(d, 1f / 90f, 37f), wood = Noise(d, 1f / 70f, 71f), r = (float)rnd.NextDouble();
            if (fieldCells.Contains(c) && NearSeg(d, Planet.Cells[c].radius)) { if (wood > 0.46f && r < 0.6f) Place(c, "park"); continue; }   // op het talud van een verdiepte weg alleen bomen (een gebouw staat plat op een sokkel, een meer ook)
            if (h < 0.5f && wet > 0.66f && k > 0.4f) { SetTerr(c, SeaLevel - 1f - (wet - 0.66f) * 22f * k); continue; }   // laagte onder het zeeniveau: een meer (PlanetNature.cs), tot zo'n 8 m diep
            if (h < 0.5f && wet > 0.6f && k > 0.4f) { SetTerr(c, SeaLevel + 0.3f); continue; }   // de oever eromheen: vlak, net boven het water
            else if (wood > 0.56f) { if (r < 0.85f) Place(c, "park"); }
            else if (r < 0.006f && h < 18f) Place(c, "molen");
            else if (r < 0.02f && h < 12f) Place(c, "woning");   // een boerderijtje
            else if (wood > 0.46f && r < 0.35f) Place(c, "park");
        }
        TouchAll(); prog?.Invoke(1f, "Klaar"); FillTimes = "verbonden " + linked + "/" + (towns.Count - 1) + ", stadjes en wegen " + tTowns + " ms, platteland " + (sw.ElapsedMilliseconds - tTowns) + " ms"; yield break;
    }
    // de kortste verbindingen die alle stadjes aan elkaar knopen (minimaal opspannende boom, Prim, over de hoek tussen de middens)
    static List<(int, int)> TownTree(List<int> towns)
    {
        var edges = new List<(int, int)>(); int T = towns.Count; if (T < 2) return edges;
        var inT = new bool[T]; inT[0] = true;
        for (int it = 1; it < T; it++)
        {
            float best = float.MaxValue; int ba = -1, bb = -1;
            for (int a = 0; a < T; a++) if (inT[a]) for (int b = 0; b < T; b++) if (!inT[b])
                    { float d = -Vector3.Dot(Planet.Cells[towns[a]].dir, Planet.Cells[towns[b]].dir); if (d < best) { best = d; ba = a; bb = b; } }
            inT[bb] = true; edges.Add((towns[ba], towns[bb]));
        }
        return edges;
    }
    // een weg van stadje a naar stadje b: de goedkoopste route over lege vakken (Dijkstra), van een gewoon wegvak van a (liefst een
    // doodlopend eind van een hoofdweg) naar een van b; een bocht kost wat extra, zodat hij zoveel mogelijk rechtdoor loopt. Niet
    // langs de verdiepte weg (zijn talud), niet door de polen of de vlaggen. Gelegd zoals slepen (PlaceLine), op het maaiveld.
    static List<int> Connect(int a, int b)
    {
        float span = Mathf.Acos(Mathf.Clamp(Vector3.Dot(Planet.Cells[a].dir, Planet.Cells[b].dir), -1f, 1f)) * Planet.R / 15.6f;   // in vakken
        bool InTown(int c, int t) => Vector3.Dot(Planet.Cells[c].dir, Planet.Cells[t].dir) > Mathf.Cos(8.5f * 15.6f / Planet.R);
        bool Joinable(int c) { if (!IsRoad(c) || Underpass(c) || WalkLevel(c) != 0 || fieldCells.Contains(c)) return false; int n = 0; foreach (int j in Partners(c)) n++; return n <= 2; }
        bool Free(int c) => !Cells.ContainsKey(c) && !Planet.IsPole(c) && System.Array.IndexOf(Planet.Starts, c) < 0 && !fieldCells.Contains(c);
        var cost = new Dictionary<int, float>(); var prev = new Dictionary<int, int>(); var open = new SortedSet<(float, int)>();
        var goal = new HashSet<int>();
        foreach (var kv in Cells)
        {
            int c = kv.Key; if (!Joinable(c)) continue;
            if (InTown(c, b)) goal.Add(c);
            else if (InTown(c, a)) { int n = 0; foreach (int j in Partners(c)) n++; float c0 = n <= 1 ? 0f : 4f; cost[c] = c0; prev[c] = -1; open.Add((c0, c)); }
        }
        if (goal.Count == 0 || open.Count == 0) return null;
        Vector3 Step(int x, int y) { Vector3 d = Planet.Cells[y].dir - Planet.Cells[x].dir; return (d - Planet.Cells[x].dir * Vector3.Dot(d, Planet.Cells[x].dir)).normalized; }
        int end = -1; float limit = span * 3f + 20f;
        while (open.Count > 0)
        {
            var (cc, c) = open.Min; open.Remove(open.Min); if (cc > cost[c]) continue; if (cc > limit) break;
            foreach (int j in Planet.Cells[c].nb)
            {
                bool g = goal.Contains(j); if (!g && !Free(j)) continue;
                if (prev[c] < 0 && !Free(j) && !g) continue;
                float step = 1f; int p = prev[c];
                if (p >= 0) { float dot = Vector3.Dot(Step(p, c), Step(c, j)); step += dot > 0.95f ? 0f : dot > 0.3f ? 0.6f : 4f; }   // rechtdoor, flauwe bocht (60 graden), scherpe bocht
                float nc = cc + step;
                if (g) { if (p < 0) continue; cost[j] = nc; prev[j] = c; end = j; break; }   // niet direct van stadje naar stadje zonder lege vakken
                if (cost.TryGetValue(j, out float old) && old <= nc) continue;
                cost[j] = nc; prev[j] = c; open.Add((nc, j));
            }
            if (end >= 0) break;
        }
        if (end < 0) return null;
        var line = new List<int>(); for (int c = end; c >= 0; c = prev[c]) line.Add(c); line.Reverse();
        byte ks = DragStyle; bool ko = DragOneWay, ku = DragUnder; int kl = DragLevel; DragStyle = 0; DragOneWay = false; DragUnder = false; DragLevel = 0;
        PlaceLine(line, "weg");
        DragStyle = ks; DragOneWay = ko; DragUnder = ku; DragLevel = kl;
        return line;
    }
    // vloeiende ruis op de bol (0..1): drie Perlin-vlakken door het punt, rond 0,5 opgerekt
    static float Noise(Vector3 d, float f, float off)
    {
        Vector3 p = d * (Planet.R * f);
        float v = (Mathf.PerlinNoise(p.x + off, p.y + 3.1f) + Mathf.PerlinNoise(p.y + 7.7f, p.z + off) + Mathf.PerlinNoise(p.z + off * 0.5f, p.x + 13.3f)) / 3f;
        return Mathf.Clamp01((v - 0.5f) * 1.8f + 0.5f);
    }
}
