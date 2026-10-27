using System.Collections.Generic;
using UnityEngine;

// De startstad: een stadje rond een van de 20 startplekken (de lichtblauwe vakken met de witte paal en het vlaggetje),
// naar de stadsgenerator van de webversie (08j-generator.js, "dorp" en "ringstad"). Vast bepaald: dezelfde startplek geeft
// hetzelfde stadje.
//  - het midden: de vlag als monument op een plein van zeven vakken (één fontein);
//  - ring 2: winkels met woningen erboven langs het plein, een kerktoren, een stadhuis en drie voetpaden naar het plein;
//  - een ringweg op 3 vakken, met drie hoofdstraten onder 120° naar buiten (doodlopend in een ronde kop) en daartussen paden;
//  - daarbuiten rijtjes woningen langs de wegen, een kantorenbuurtje, parken met paden, een meertje en een heuvel met een molen.
public static partial class PlanetCity
{
    public static void StartTown(int s)
    {
        byte keepStyle = DragStyle; bool keepOne = DragOneWay; DragStyle = 0; DragOneWay = false;
        var S = Planet.Cells[s]; var rnd = new System.Random(s * 7919 + 13);
        // afstanden in vakken vanaf de vlag
        var dist = new Dictionary<int, int> { { s, 0 } }; var q = new Queue<int>(); q.Enqueue(s);
        while (q.Count > 0) { int c = q.Dequeue(); if (dist[c] >= 9) continue; foreach (int j in Planet.Cells[c].nb) if (!dist.ContainsKey(j)) { dist[j] = dist[c] + 1; q.Enqueue(j); } }
        const float P = 15.6f;   // hart tot hart
        System.Func<float, float, int> cellAt = (ang, r) => Planet.Nearest((S.dir * Mathf.Cos(r / Planet.R) + (S.east * Mathf.Cos(ang) + S.north * Mathf.Sin(ang)) * Mathf.Sin(r / Planet.R)).normalized * Planet.R);
        System.Func<int, List<int>> ring = k => { var l = new List<int>(); foreach (var kv in dist) if (kv.Value == k) l.Add(kv.Key); l.Sort((x, y) => Ang(S, x).CompareTo(Ang(S, y))); return l; };
        // hoek tussen twee richtingen, en de richting van een vak gezien vanaf de vlag
        System.Func<float, float, float> dAng = (a, b) => Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, b * Mathf.Rad2Deg));
        Vector3 n0 = Planet.Cells[S.nb[0]].dir - S.dir; n0 -= S.dir * Vector3.Dot(n0, S.dir); float a0 = Mathf.Atan2(Vector3.Dot(n0, S.north), Vector3.Dot(n0, S.east));
        float[] street = { a0, a0 + Mathf.PI * 2f / 3f, a0 + Mathf.PI * 4f / 3f };   // de drie hoofdstraten, langs de assen van het raster
        float[] lane = { a0 + Mathf.PI / 3f, a0 + Mathf.PI, a0 + Mathf.PI * 5f / 3f };  // daartussen: paden, het meer, de heuvel, de kantoren
        System.Func<int, bool> free = c => !Cells.ContainsKey(c) && c != s;
        System.Func<int, string, bool> put = (c, t) => free(c) && Place(c, t) == null;

        // 1. het plein rond de vlag (het vlagvak zelf blijft vrij: daar staat de paal)
        foreach (int c in ring(1)) put(c, "plein");

        // 2. ringweg op 3 vakken, als gesloten lus
        var r3 = ring(3); var loop = new List<int>(r3); loop.Add(r3[0]); PlaceLine(loop, "weg");

        // 3. drie hoofdstraten vanaf de ringweg naar buiten, en paden in de richtingen daartussen
        foreach (float ang in street) { var l = LinePlan(cellAt(ang, 3f * P), cellAt(ang, 8.6f * P)); if (l != null) PlaceLine(l, "weg"); }
        foreach (float ang in lane)
        {
            var l = LinePlan(cellAt(ang, 2f * P), cellAt(ang, 6.4f * P)); if (l == null) continue;
            // het pad steekt de ringweg over (twee stukken, elk aan de weg verbonden) en houdt op bij iets anders
            var pad = new List<int>(); var all = new List<int>();
            foreach (int c in l)
            {
                if (IsRoad(c)) { if (pad.Count > 0) { PlaceLine(pad, "pad"); all.AddRange(pad); pad = new List<int>(); } continue; }
                if (free(c) || IsPath(c)) pad.Add(c); else break;
            }
            if (pad.Count > 0) { PlaceLine(pad, "pad"); all.AddRange(pad); }
            foreach (int c in all) foreach (int j in Planet.Cells[c].nb) if (IsRoad(j)) Link(c, j);
        }

        // 4. ring 2, aan het plein: winkels met woningen erboven, een kerktoren en een stadhuis
        foreach (int c in ring(2))
        {
            if (!free(c)) continue; float ang = Ang(S, c);
            if (dAng(ang, lane[1] + 0.35f) < 15f) { put(c, "blok"); Place(c, "toren"); continue; }   // de kerk: stenen voet met de toren erop
            if (dAng(ang, lane[2] + 0.35f) < 15f) { for (int f = 0; f < 3; f++) Place(c, "kantoor"); Place(c, "dak"); continue; }   // het stadhuis
            put(c, "winkel"); int up = rnd.Next(1, 3); for (int f = 0; f < up; f++) Place(c, "woning");
        }

        // 5. het meer (een paar vakken) en de heuvel met de molen, elk in een eigen hoek tussen de straten
        int lakeC = cellAt(lane[0] + 0.25f, 6f * P); var lake = new List<int> { lakeC }; foreach (int j in Planet.Cells[lakeC].nb) if (rnd.NextDouble() < 0.55) lake.Add(j);
        foreach (int c in lake) if (free(c) && dist.TryGetValue(c, out int dl) && dl >= 5) Place(c, "meer");
        int hill = cellAt(lane[1] - 0.3f, 6.6f * P);
        foreach (var kv in dist)
        {
            float d = Vector3.Distance(Planet.Cells[kv.Key].dir, Planet.Cells[hill].dir) * Planet.R / P;   // in vakken
            if (d < 3.2f && (free(kv.Key) || IsPath(kv.Key))) SetTerr(kv.Key, Mathf.Round(Mathf.Lerp(7f, 0.5f, d / 3.2f) * 2f) / 2f);
        }
        put(hill, "molen");

        // 6. de rest, van binnen naar buiten
        foreach (var kv in dist)
        {
            int c = kv.Key, d = kv.Value; if (!free(c) || d < 4 || d > 9) continue;
            bool road = false, path = false; foreach (int j in Planet.Cells[c].nb) { if (IsRoad(j)) road = true; if (IsPath(j)) path = true; }
            float ang = Ang(S, c), h = G(c);
            bool business = dAng(ang, lane[2]) < 34f && d <= 6;   // kantorenbuurtje tussen twee hoofdstraten
            if (h > 0.6f) { if (rnd.NextDouble() < 0.6) put(c, "park"); continue; }   // op de heuvel: groen
            if (d == 4 && road)   // langs de buitenkant van de ringweg: rijtjeshuizen, bij de hoofdstraten winkels
            {
                bool nearStreet = false; foreach (float sa in street) if (dAng(ang, sa) < 14f) nearStreet = true;
                if (business) { int fl = 3 + rnd.Next(0, 3); for (int f = 0; f < fl; f++) Place(c, "kantoor"); }
                else { put(c, nearStreet ? "winkel" : "woning"); if (rnd.NextDouble() < 0.45) Place(c, "woning"); }
                continue;
            }
            if (road && d <= 8)   // langs de hoofdstraten
            {
                if (business) { int fl = 2 + rnd.Next(0, 3); for (int f = 0; f < fl; f++) Place(c, "kantoor"); }
                else { put(c, "woning"); if (rnd.NextDouble() < 0.3) Place(c, "woning"); }
                continue;
            }
            if (path && d <= 7) { put(c, rnd.NextDouble() < 0.7 ? "park" : "woning"); continue; }
            if (d <= 6) { put(c, rnd.NextDouble() < 0.75 ? "park" : "woning"); continue; }
            if (d <= 8 && rnd.NextDouble() < 0.45) put(c, "park");   // aan de rand: wat losse bosjes
        }
        DragStyle = keepStyle; DragOneWay = keepOne; TouchAll(); RebuildGrid();
    }

    // de startplek het dichtst bij een punt waar nog niets staat (anders gewoon de dichtstbijzijnde)
    public static int FreeStart(Vector3 near)
    {
        int best = Planet.Starts[0], bestFree = -1; float bd = -2f, bf = -2f;
        foreach (int st in Planet.Starts)
        {
            float d = Vector3.Dot(Planet.Cells[st].dir, near.normalized); if (d > bd) { bd = d; best = st; }
            bool empty = true; foreach (var kv in Cells) if (Vector3.Distance(Planet.Cells[kv.Key].dir, Planet.Cells[st].dir) * Planet.R < 10f * 15.6f) { empty = false; break; }
            if (empty && d > bf) { bf = d; bestFree = st; }
        }
        return bestFree >= 0 ? bestFree : best;
    }
}
