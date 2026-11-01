using System.Collections.Generic;
using UnityEngine;

// De startstad: een stadje rond een van de 20 startplekken (de lichtblauwe vakken met de witte paal en het vlaggetje),
// naar de stadsgenerator van de webversie (08j-generator.js, "dorp" en "ringstad"). Vast bepaald: dezelfde startplek geeft
// hetzelfde stadje.
//  - het midden: de vlag als monument op een plein (één fontein), daaromheen winkels met woningen erboven;
//  - op 4 vakken een verlaagde zeshoekige ring voor voetgangers en fietsers (een heel niveau diep, in een bak met taluds), met
//    een pad omhoog naar het plein en zes paden haaks naar buiten, die onder de ringweg door gaan (zes bruggen);
//  - tussen die ring en de ringweg (op 7 vakken) een kerktoren, een stadhuis, parkjes en huizen;
//  - de ringweg is een laan; drie hoofdstraten onder 120° kruisen haar: naar buiten de stad uit (doodlopend in een ronde kop) en
//    twee vakken naar binnen tot een keerkop, als kleine straatjes binnen de rondweg;
//  - daarbuiten rijtjes woningen langs de wegen, een kantorenbuurtje, parken, een meertje en een heuvel met een molen.
public static partial class PlanetCity
{
    public static void StartTown(int s)
    {
        StartVer = StartTownVersion; StartCell = s; startFresh = true;   // nog de ongewijzigde startstad: wordt bij een nieuwe versie opnieuw gebouwd
        byte keepStyle = DragStyle; bool keepOne = DragOneWay; DragStyle = 0; DragOneWay = false;
        var S = Planet.Cells[s]; var rnd = new System.Random(s * 7919 + 13);
        // afstanden in vakken vanaf de vlag
        var dist = new Dictionary<int, int> { { s, 0 } }; var q = new Queue<int>(); q.Enqueue(s);
        while (q.Count > 0) { int c = q.Dequeue(); if (dist[c] >= 13) continue; foreach (int j in Planet.Cells[c].nb) if (!dist.ContainsKey(j)) { dist[j] = dist[c] + 1; q.Enqueue(j); } }
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

        var keep = new HashSet<int>();   // vakken die vrij blijven: een schuin pad loopt over hun gedeelde rand
        // 1. het plein rond de vlag (het vlagvak zelf blijft vrij: daar staat de paal), behalve in de richting van het pad omhoog
        var spoke = LinePlan(cellAt(lane[0], 4f * P), cellAt(lane[0], 1f * P)) ?? new List<int>();
        foreach (int c in ring(1)) if (!spoke.Contains(c)) put(c, "plein");

        // 2. de verlaagde ring voor voetgangers en fietsers op 4 vakken: gesloten lus, een heel niveau omlaag (zoals slepen met
        //    Page Down); een pad van de ring omhoog naar het plein
        var r4 = ring(4); var loop4 = new List<int>(r4); loop4.Add(r4[0]);
        bool keepBike = DragBike; DragBike = true;   // de ring en de paden ervandaan: fiets- en voetpaden
        int keepLevel = DragLevel; DragLevel = -2; PlaceLine(loop4, "pad"); SetLevel(loop4, -2, "pad"); DragLevel = keepLevel;
        if (spoke.Count >= 4) { PlaceLine(spoke, "pad"); SetLevel(new List<int> { spoke[1] }, -1, "pad"); }   // halverwege een half niveau

        // 3. ring 2, aan het plein: winkels met woningen erboven
        foreach (int c in ring(2)) { if (!free(c)) continue; put(c, "winkel"); int up = rnd.Next(1, 3); for (int f = 0; f < up; f++) Place(c, "woning"); }

        // 4. de rondweg op 7 vakken: een laan als gesloten lus; drie hoofdstraten (gewone wegen) die de laan kruisen: naar buiten de stad uit,
        //    en twee vakken naar binnen tot een keerkop (kleine straatjes binnen de rondweg)
        var r7 = ring(7); var loop = new List<int>(r7); loop.Add(r7[0]); DragStyle = 2; PlaceLine(loop, "weg"); DragStyle = 0;
        foreach (float ang in street) { var l = LinePlan(cellAt(ang, 5f * P), cellAt(ang, 12.6f * P)); if (l != null) PlaceLine(l, "weg"); }

        // 5. zes paden vanuit het midden van de zijden van de verlaagde ring, haaks naar buiten (schuin over het raster: van vak naar
        //    vak twee stappen verder, over de rand ertussen). Ze blijven een heel niveau diep tot voorbij de ringweg, die er met een brug
        //    overheen gaat, en komen dan in twee stappen boven.
        float diag = Mathf.Sqrt(3f) * P;
        for (int k = 0; k < 6; k++)
        {
            float ang = a0 + Mathf.PI / 6f + k * Mathf.PI / 3f; var l = new List<int> { cellAt(ang, 2f * diag) };
            Vector3 want = S.east * Mathf.Cos(ang) + S.north * Mathf.Sin(ang);
            for (int m = 0; m < 4; m++)   // telkens het vak twee stappen verder dat het best in de richting ligt (over de rand tussen twee buren)
            {
                int c0 = l[l.Count - 1], best = -1; float bd = -2f; Vector3 d0 = Planet.Cells[c0].dir;
                foreach (int j in Planet.Cells[c0].nb) foreach (int x in Planet.Cells[j].nb)
                {
                    if (x == c0 || IsNeighbour(c0, x) || Between(c0, x).Count != 2) continue;
                    float dd = Vector3.Dot(Tan(Planet.Cells[x].dir - d0, d0), want); if (dd > bd) { bd = dd; best = x; }
                }
                if (best < 0) break; l.Add(best);
            }
            if (l.Count < 5) continue;
            if (!r4.Contains(l[0])) continue; bool ok = true; for (int m = 1; m < l.Count; m++) if (!free(l[m])) ok = false; if (!ok) continue;
            for (int m = 1; m < l.Count; m++) foreach (int b in Between(l[m - 1], l[m])) keep.Add(b);
            DragLevel = -2; PlaceLine(l, "pad"); SetLevel(new List<int> { l[1], l[2] }, -2, "pad"); SetLevel(new List<int> { l[3] }, -1, "pad"); DragLevel = keepLevel;
        }

        DragBike = keepBike;

        // 6. tussen de plein-winkels en de ringweg: een kerktoren en een stadhuis, verder parkjes en huizen
        bool kerk = false, stadhuis = false;
        foreach (var kv in dist)
        {
            int c = kv.Key, d = kv.Value; if (d < 3 || d > 6 || !free(c) || keep.Contains(c)) continue; float ang = Ang(S, c);
            if (!kerk && d == 5 && dAng(ang, lane[1] + 0.25f) < 10f) { put(c, "blok"); Place(c, "toren"); kerk = true; continue; }   // de kerk: stenen voet met de toren erop
            if (!stadhuis && d == 5 && dAng(ang, lane[2] + 0.25f) < 10f) { for (int f = 0; f < 3; f++) Place(c, "kantoor"); Place(c, "dak"); stadhuis = true; continue; }   // het stadhuis
            put(c, rnd.NextDouble() < 0.6 ? "park" : "woning");
        }

        // 7. het meer (een paar vakken) en de heuvel met de molen, elk in een eigen hoek tussen de straten
        int lakeC = cellAt(lane[0] + 0.25f, 10f * P); var lake = new List<int> { lakeC }; foreach (int j in Planet.Cells[lakeC].nb) if (rnd.NextDouble() < 0.55) lake.Add(j);
        foreach (int c in lake) if (free(c) && dist.TryGetValue(c, out int dl) && dl >= 9 && !keep.Contains(c)) Place(c, "meer");
        int hill = cellAt(lane[1] - 0.3f, 10.6f * P);
        foreach (var kv in dist)
        {
            float d = Vector3.Distance(Planet.Cells[kv.Key].dir, Planet.Cells[hill].dir) * Planet.R / P;   // in vakken
            if (d < 3.2f && (free(kv.Key) || IsPath(kv.Key))) SetTerr(kv.Key, Mathf.Round(Mathf.Lerp(7f, 0.5f, d / 3.2f) * 2f) / 2f);
        }
        put(hill, "molen");

        // 8. de rest, van binnen naar buiten
        foreach (var kv in dist)
        {
            int c = kv.Key, d = kv.Value; if (!free(c) || keep.Contains(c) || d < 8 || d > 13) continue;
            bool road = false, path = false; foreach (int j in Planet.Cells[c].nb) { if (IsRoad(j)) road = true; if (IsPath(j)) path = true; }
            float ang = Ang(S, c), h = G(c);
            bool business = dAng(ang, lane[2]) < 34f && d <= 10;   // kantorenbuurtje tussen twee hoofdstraten
            if (h > 0.6f) { if (rnd.NextDouble() < 0.6) put(c, "park"); continue; }   // op de heuvel: groen
            if (d == 8 && road)   // langs de buitenkant van de ringweg: rijtjeshuizen, bij de hoofdstraten winkels
            {
                bool nearStreet = false; foreach (float sa in street) if (dAng(ang, sa) < 14f) nearStreet = true;
                if (business) { int fl = 3 + rnd.Next(0, 3); for (int f = 0; f < fl; f++) Place(c, "kantoor"); }
                else { put(c, nearStreet ? "winkel" : "woning"); if (rnd.NextDouble() < 0.45) Place(c, "woning"); }
                continue;
            }
            if (road && d <= 12)   // langs de hoofdstraten
            {
                if (business) { int fl = 2 + rnd.Next(0, 3); for (int f = 0; f < fl; f++) Place(c, "kantoor"); }
                else { put(c, "woning"); if (rnd.NextDouble() < 0.3) Place(c, "woning"); }
                continue;
            }
            if (path && d <= 11) { put(c, rnd.NextDouble() < 0.7 ? "park" : "woning"); continue; }
            if (d <= 10) { put(c, rnd.NextDouble() < 0.75 ? "park" : "woning"); continue; }
            if (d <= 12 && rnd.NextDouble() < 0.45) put(c, "park");   // aan de rand: wat losse bosjes
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
            bool empty = true; foreach (var kv in Cells) if (Vector3.Distance(Planet.Cells[kv.Key].dir, Planet.Cells[st].dir) * Planet.R < 14f * 15.6f) { empty = false; break; }
            if (empty && d > bf) { bf = d; bestFree = st; }
        }
        return bestFree >= 0 ? bestFree : best;
    }
}
