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
// Versie 6: een groene rand tot 22 vakken (GreenEdge): glooiend terrein, bos dat naar buiten dichter wordt, open plekken met heide,
// twee meertjes in een laagte en boomgroepen tussen de buitenste huizen; langs de uitgaande straten blijft een strook vrij.
// Versie 5: aan een straat laanhuizen (haakse gevel, voortuin naar de laan; anders een huis met voortuin), en niets op of tegen
// een weg: elke module en elk park wordt getoetst (RoadClear) zodat muren, bomen en voorwerpen buiten de stoep blijven.
// Versie 3 (StartTownVersion 3): de gebouwen, pleinen en parkjes zijn modules uit de bibliotheek (Mod), met de deur naar het plein
// of naar de straat (FaceRot); wegen, paden, de verlaagde ring, de bruggen en het terrein zijn gelijk aan versie 2. Lukt een module
// niet (een vijfhoek, geen ruimte voor 7 vakken), dan komen er bomen.
public static partial class PlanetCity
{
    // Na het bouwen meteen het wegprofiel (Terrein v2): het startscherm, een nieuwe stad en een nieuwe versie roepen StartTown aan zonder
    // PlanetBuilder.Changed, en zonder profiel (RoadH leeg) tekent Terrein v2 de velden op de oude manier: 116 s in plaats van 6 s
    public static void StartTown(int s) { StartTown0(s); AddHeath(); Reprofile(); }   // met heidevelden buiten de groene rand (PlanetHeath.cs)
    static void StartTown0(int s)
    {
        StartVer = StartTownVersion; StartCell = s; startFresh = true;   // nog de ongewijzigde startstad: wordt bij een nieuwe versie opnieuw gebouwd
        byte keepStyle = DragStyle; bool keepOne = DragOneWay; DragStyle = 0; DragOneWay = false;
        var S = Planet.Cells[s]; var rnd = new System.Random(s * 7919 + 13);
        // afstanden in vakken vanaf de vlag (tot 22: de groene rand van versie 6)
        var dist = new Dictionary<int, int> { { s, 0 } }; var q = new Queue<int>(); q.Enqueue(s);
        while (q.Count > 0) { int c = q.Dequeue(); if (dist[c] >= 22) continue; foreach (int j in Planet.Cells[c].nb) if (!dist.ContainsKey(j)) { dist[j] = dist[c] + 1; q.Enqueue(j); } }
        const float P = 15.6f;   // hart tot hart
        System.Func<float, float, int> cellAt = (ang, r) => Planet.Nearest((S.dir * Mathf.Cos(r / Planet.R) + (S.east * Mathf.Cos(ang) + S.north * Mathf.Sin(ang)) * Mathf.Sin(r / Planet.R)).normalized * Planet.R);
        System.Func<int, List<int>> ring = k => { var l = new List<int>(); foreach (var kv in dist) if (kv.Value == k) l.Add(kv.Key); l.Sort((x, y) => Ang(S, x).CompareTo(Ang(S, y))); return l; };
        // hoek tussen twee richtingen, en de richting van een vak gezien vanaf de vlag
        System.Func<float, float, float> dAng = (a, b) => Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, b * Mathf.Rad2Deg));
        Vector3 n0 = Planet.Cells[S.nb[0]].dir - S.dir; n0 -= S.dir * Vector3.Dot(n0, S.dir); float a0 = Mathf.Atan2(Vector3.Dot(n0, S.north), Vector3.Dot(n0, S.east));
        float[] street = { a0, a0 + Mathf.PI * 2f / 3f, a0 + Mathf.PI * 4f / 3f };   // de drie hoofdstraten, langs de assen van het raster
        float[] lane = { a0 + Mathf.PI / 3f, a0 + Mathf.PI, a0 + Mathf.PI * 5f / 3f };  // daartussen: paden, het meer, de heuvel, de kantoren
        System.Func<int, bool> free = c => !Cells.ContainsKey(c) && c != s;
        System.Func<int, string, bool> put = (c, t) => free(c) && (t != "park" || RoadClear(null, c, 0)) && Place(c, t) == null;   // geen bomen op of tegen een weg

        var keep = new HashSet<int>();   // vakken die vrij blijven: een schuin pad loopt over hun gedeelde rand
        // een module uit de lijst, met de deur naar vak face (-1: willekeurig); geeft false als er geen past
        // Iedereen moet bij zijn voordeur kunnen komen: een gebouw komt alleen waar zijn deur uitkomt op een weg, een pad of een plein
        // op dezelfde hoogte (niet op het verdiepte fietspad, niet tegen een ander gebouw of in het gras). Is er geen zo'n buur, dan
        // komt er eerst een pleintje op een vrij buurvak dat zelf aan een weg, pad of plein ligt; lukt ook dat niet, dan geen gebouw.
        System.Func<int, bool> flatPath = j => { if (WalkLevel(j) != 0) return false; foreach (int x in Partners(j)) if (WalkLevel(x) != 0) return false; return true; };   // geen helling naar het verdiepte pad
        System.Func<int, int, bool> acc = (j, c) => Mathf.Abs(G(j) - G(c)) < 0.6f && ((IsRoad(j) && !Underpass(j)) || (IsPath(j) && flatPath(j)) || Inhoud(j).soort == Soort.Plein);
        System.Func<int, bool> hasAcc = c => { foreach (int j in Planet.Cells[c].nb) if (acc(j, c)) return true; return false; };
        System.Func<int, string[], int, bool> mod = (c, ids, face) =>
        {
            if (!free(c) || keep.Contains(c)) return false;
            for (int t = 0; t < 4; t++)
            {
                string id = ids[rnd.Next(ids.Length)]; bool mir = rnd.Next(2) == 0; var M = ModuleLib.Data(id, mir); if (M == null) continue;
                bool door = MV2.MetaOf(M).bouw && ModHasDoor(M);
                if (door && !hasAcc(c))
                {
                    int yard = -1; foreach (int n in Planet.Cells[c].nb) if (free(n) && !keep.Contains(n) && Mathf.Abs(G(n) - G(c)) < 0.6f && hasAcc(n)) { yard = n; break; }
                    if (yard < 0 || Place(yard, "plein") != null) return false;
                }
                int r0 = FaceRot(M, c, face, rnd);
                for (int dr = 0; dr < 6; dr++)   // eerst de deur naar de straat; houdt die draaiing de weg niet vrij, dan een andere
                {
                    int rot = (r0 + dr) % 6;
                    if (!RoadClear(M, c, rot)) continue;
                    if (door) { bool ok = false; foreach (var d in ModDoorCells(M, c, rot)) if (acc(d.buur, c)) { ok = true; break; } if (!ok) continue; }
                    if (PlaceModule(id, c, rot, mir) == null) return true;
                }
            }
            return false;
        };
        System.Func<int, int> street0 = c => { foreach (int j in Planet.Cells[c].nb) if (IsRoad(j)) return j; foreach (int j in Planet.Cells[c].nb) if (IsPath(j)) return j; return -1; };   // de straat (of het pad) naast een vak
        // 1. het plein rond de vlag (het vlagvak zelf blijft vrij: daar staat de paal), behalve in de richting van het pad omhoog
        var spoke = LinePlan(cellAt(lane[0], 4f * P), cellAt(lane[0], 1f * P)) ?? new List<int>();
        foreach (int c in ring(1)) if (!spoke.Contains(c) && !mod(c, Pleinen, s)) put(c, "plein");

        // 2. de verlaagde ring voor voetgangers en fietsers op 4 vakken: gesloten lus, een heel niveau omlaag (zoals slepen met
        //    Page Down); een pad van de ring omhoog naar het plein
        var r4 = ring(4); var loop4 = new List<int>(r4); loop4.Add(r4[0]);
        bool keepBike = DragBike; DragBike = true;   // de ring en de paden ervandaan: fiets- en voetpaden
        int keepLevel = DragLevel; DragLevel = -2; PlaceLine(loop4, "pad"); SetLevel(loop4, -2, "pad"); DragLevel = keepLevel;
        if (spoke.Count >= 4) { PlaceLine(spoke, "pad"); SetLevel(new List<int> { spoke[1] }, -1, "pad"); }   // halverwege een half niveau

        // 3. ring 2, aan het plein: winkels met woningen erboven
        foreach (int c in ring(2)) { if (!free(c)) continue; if (!mod(c, Winkels, s)) put(c, "park"); }   // winkels en horeca, de deur naar het plein

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
            if (!kerk && d == 5 && dAng(ang, lane[1] + 0.25f) < 10f) { kerk = mod(c, new[] { "kerk-toren-7l" }, s); if (kerk) continue; }   // de kerk
            if (!stadhuis && d == 5 && dAng(ang, lane[2] + 0.25f) < 10f) { stadhuis = mod(c, new[] { "huisarts-wit-2l", "bibliotheek-glas-2l", "kantoor-baksteen-3l" }, s); if (stadhuis) continue; }   // het stadhuis
            if (rnd.NextDouble() < 0.6) { if (rnd.NextDouble() < 0.5 || !mod(c, Groen, -1)) put(c, "park"); }   // parkjes: de helft een module, de rest bomen
            else if (!mod(c, street0(c) >= 0 ? Laan : Huizen, street0(c) >= 0 ? street0(c) : s) && !(street0(c) >= 0 && mod(c, Voortuin, street0(c)))) put(c, "park");   // aan een straat: een laanhuis (of een huis met voortuin)
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
        if (!mod(hill, new[] { "windmolen-modern" }, -1)) put(hill, "molen");

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
                if (!mod(c, business ? Kantoren : nearStreet ? Winkels : street0(c) >= 0 ? Laan : Huizen, street0(c)) && !(!business && street0(c) >= 0 && mod(c, Voortuin, street0(c)))) put(c, "park");
                continue;
            }
            if (road && d <= 12)   // langs de hoofdstraten
            {
                if (!mod(c, business ? Kantoren : street0(c) >= 0 ? Laan : Huizen, street0(c)) && !(!business && street0(c) >= 0 && mod(c, Voortuin, street0(c)))) put(c, "park");
                continue;
            }
            if (path && d <= 11) { if (rnd.NextDouble() < 0.7 ? (rnd.NextDouble() < 0.5 || !mod(c, Groen, -1)) : !mod(c, Villas, street0(c))) put(c, "park"); continue; }
            if (d <= 10) { if (rnd.NextDouble() < 0.75 ? (rnd.NextDouble() < 0.6 || !mod(c, Groen, -1)) : !mod(c, Villas, street0(c))) put(c, "park"); continue; }
            if (d <= 12 && rnd.NextDouble() < 0.45) put(c, "park");   // aan de rand: wat losse bosjes
        }
        GreenEdge(S, s, dist, street, lane, rnd, cellAt, free);
        DragStyle = keepStyle; DragOneWay = keepOne; TouchAll(); RebuildGrid();
    }

    // 9. (versie 6) De groene rand: buiten de rondweg licht glooiend terrein, bos dat naar buiten dichter wordt met open plekken,
    // heide, twee meertjes in een laagte en losse boomgroepen tussen de buitenste huizen. Langs de straten die de stad uit lopen
    // blijft een strook vrij om verder te bouwen; bij wegen en paden blijft alles weg (RoadClear) en het terrein vlak.
    static void GreenEdge(Planet.Cell S, int s, Dictionary<int, int> dist, float[] street, float[] lane, System.Random rnd, System.Func<float, float, int> cellAt, System.Func<int, bool> free)
    {
        const float P = 15.6f; float o = (s % 997) * 0.37f;
        System.Func<float, float, float> dAng = (a, b) => Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, b * Mathf.Rad2Deg));
        bool Corridor(int c)   // in het verlengde van een straat die de stad uit loopt: hier bouwt de speler verder
        {
            int d = dist[c]; if (d < 11) return false; float a = Ang(S, c);
            foreach (float sa in street) { float da = dAng(a, sa); if (da < 80f && d * Mathf.Sin(da * Mathf.Deg2Rad) < 1.8f) return true; }
            return false;
        }
        bool NearWalk(int c, int r)
        {
            var seen = new HashSet<int> { c }; var ring = new List<int> { c };
            for (int k = 0; k <= r; k++)
            {
                var next = new List<int>();
                foreach (int x in ring) { if (IsWalk(x) || IsHalf(x)) return true; if (k < r) foreach (int n in Planet.Cells[x].nb) if (seen.Add(n)) next.Add(n); }
                ring = next;
            }
            return false;
        }
        // twee meertjes in een laagte, tussen de straten in
        var lakes = new List<int>();
        foreach (var (ang, r) in new[] { (lane[0] - 0.3f, 16.5f), (lane[2] + 0.35f, 15.5f) })
        {
            int lc = cellAt(ang, r * P); if (!dist.ContainsKey(lc) || !free(lc) || NearWalk(lc, 2) || Corridor(lc)) continue;
            lakes.Add(lc); var cells = new List<int> { lc }; foreach (int j in Planet.Cells[lc].nb) if (rnd.NextDouble() < 0.5) cells.Add(j);
            foreach (int c in cells) if (free(c) && !NearWalk(c, 1) && !Corridor(c)) Place(c, "meer");
        }
        // licht glooiend terrein buiten de rondweg (tot 3,5 m), vlak bij wegen; rond een meertje een rand zodat het in een kom ligt
        foreach (var kv in dist)
        {
            int c = kv.Key, d = kv.Value; if (d < 9 || Terr.ContainsKey(c) || NearWalk(c, 1)) continue;
            if (Cells.TryGetValue(c, out var l) && !(l.Count == 1 && l[0] == "park")) continue;
            Vector3 dir = Planet.Cells[c].dir; float amp = Mathf.Clamp01((d - 9) / 5f) * 3.5f;
            if (NearWalk(c, 2)) amp *= 0.35f;
            float h = amp * (Noise(dir, 1f / 80f, o + 5f) * 0.75f + Noise(dir, 1f / 30f, o + 19f) * 0.25f);
            foreach (int lc in lakes) { float ld = Vector3.Distance(dir, Planet.Cells[lc].dir) * Planet.R / P; if (ld < 2.6f) h = Mathf.Max(h, Mathf.Lerp(1.6f, 0.4f, (ld - 1f) / 1.6f)); }
            if (h >= 0.5f) SetTerr(c, Mathf.Round(h * 2f) / 2f);
        }
        // bos, open plekken, heide en wat rotsen; binnen 12 vakken alleen losse boomgroepen tussen de huizen
        foreach (var kv in dist)
        {
            int c = kv.Key, d = kv.Value; if (d < 9 || !free(c) || Corridor(c) || NearWalk(c, 1) || !RoadClear(null, c, 0)) continue;
            Vector3 dir = Planet.Cells[c].dir; float f = Mathf.Clamp01((d - 11) / 9f), wood = Noise(dir, 1f / 90f, o + 3f), open = Noise(dir, 1f / 45f, o + 21f), heath = Noise(dir, 1f / 60f, o + 40f);
            if (d <= 12) { if (wood > 0.45f && rnd.NextDouble() < 0.35) Place(c, "park"); continue; }   // boomgroepen tussen de buitenste huizen
            if (open > 0.64f) { if (heath > 0.5f) Place(c, "heide"); else if (rnd.NextDouble() < 0.06) Place(c, "park"); continue; }   // een open plek: heide of weide
            if (G(c) > 2.5f && rnd.NextDouble() < 0.06) { Place(c, "rotsen"); continue; }
            if (rnd.NextDouble() < 0.3f + 0.65f * f + (wood - 0.5f) * 0.5f) Place(c, "park");   // bos, naar buiten toe dichter
        }
    }

    // de modules van de startstad (versie 3), per soort plek
    static readonly string[] Pleinen = { "plein-leeg", "plein-lantaarns", "plein-terras", "plein-zitkuil", "plein-fontein" };
    static readonly string[] Winkels = { "winkel-bakkerij-2l", "winkel-boetiek-1l", "winkel-bovenwoning-3l", "winkel-etalage-2l", "horeca-cafe-met-terras-2l", "horeca-restaurant-2l", "snackbar-frituur-1l" };
    static readonly string[] Huizen = { "rijhuis-baksteen-2l", "rijhuis-baksteen-3l", "rijhuis-wit-2l", "rijhuis-hout-2l", "rijhuis-natuursteen-2l", "stadswoning-wit-3l", "herenhuis-natuursteen-4l" };
    static readonly string[] Laan = { "laanhuis-wit-2l", "laanhuis-baksteen-2l", "laanhuis-donker-2l", "laanhuis-hout-2l" };   // zoals Joeps IT-kantoor: haakse gevel, voortuin naar de laan
    static readonly string[] Voortuin = { "voortuinhuis-wit-2l", "voortuinhuis-baksteen-2l", "voortuinhuis-hout-2l", "voortuinhuis-natuursteen-2l" };   // het huis op de achterste halve zeshoek, de tuin naar de straat
    static readonly string[] Villas = { "villa-baksteen-tuin-2l", "villa-wit-tuin-2l", "bungalow-baksteen-1l", "patiowoning-baksteen-1l", "studio-hout-1l" };
    static readonly string[] Kantoren = { "kantoor-glas-3l", "kantoor-glas-5l", "kantoor-beton-4l", "kantoor-baksteen-3l" };
    static readonly string[] Groen = { "park-plantsoen", "park-bankjes", "park-speeltuin", "tuin-rozentuin", "park-vijver" };
    // de draaiing waarbij de deur (of anders de stoep) van module M op vak c het best naar vak face wijst; -1: willekeurig
    static int FaceRot(ModuleV2 M, int c, int face, System.Random rnd)
    {
        if (face < 0 || M == null) return rnd.Next(6);
        var codes = MV2.Codes(M); var pts = new List<Vector2>();
        foreach (var d in SoortVan(M).deuren) pts.Add(d.dir * MV2.INNER);   // de echte deuren (ook als ze niet aan de rand zitten)
        if (pts.Count == 0) foreach (string want in new[] { "deur", "stoep", "groen" })
        {
            foreach (var kv in codes) if (kv.Value == want) { var p = kv.Key.Split(','); int q = int.Parse(p[0]), r = int.Parse(p[1]), sd = int.Parse(p[2]); pts.Add(MV2.CellXZ(q, r) + MV2.FN(sd) * MV2.INNER); }
            if (pts.Count > 0) break;
        }
        if (pts.Count == 0) return rnd.Next(6);
        Vector2 mean = Vector2.zero; foreach (var p in pts) mean += p; if (mean.magnitude < 0.5f) return rnd.Next(6);   // rondom hetzelfde: maakt niet uit
        Vector3 up = Planet.Cells[c].dir, to = Vector3.zero;
        bool road = IsRoad(face) || IsPath(face);   // alle straten (of paden) ernaast samen: een laan langs twee zijden wijst naar de hoek ertussen
        foreach (int j in Planet.Cells[c].nb) if (j == face || (road && (IsRoad(face) ? IsRoad(j) : IsPath(j)))) { var d = Planet.Cells[j].dir - up; to += (d - up * Vector3.Dot(d, up)).normalized; }
        if (to.sqrMagnitude < 1e-6f) { to = Planet.Cells[face].dir - up; to -= up * Vector3.Dot(to, up); }
        int best = 0; float bd = -9f;
        for (int rot = 0; rot < 6; rot++)
        {
            ModAxes(c, rot, out var A, out var B, out _); float sc = Vector3.Dot((A * mean.x + B * mean.y).normalized, to.normalized);
            if (sc > bd + 1e-4f) { bd = sc; best = rot; }
        }
        return best;
    }
    // Blijft de weg vrij als module M op vak c met draaiing rot staat? Muren en voorwerpen moeten buiten de stoep van elke weg of elk
    // pad in de buurt blijven (met een meter ruimte); vloeren mogen eronder doorlopen (de stoep ligt erbovenop).
    static float RoadSide(int i) { Dims(StyleOf(i), out _, out float sw); return sw; }
    static bool RoadClear(ModuleV2 M, int c, int rot)   // M null: een park (bomen tot 5 m van het midden)
    {
        Vector3 up = Planet.Cells[c].dir, o = up * Planet.R; ModAxes(c, rot, out var A, out var B, out _);
        var segs = new List<(Vector3 a, Vector3 b, float hw)>();
        foreach (var kv in Cells)
        {
            int i = kv.Key; if (!IsWalk(i) && !IsHalf(i)) continue; Vector3 pi = Planet.Cells[i].dir * Planet.R; if ((pi - o).magnitude > 60f) continue;
            float hw = IsRoad(i) ? RoadSide(i) : 3.5f; bool any = false;
            foreach (int j in Partners(i)) if (IsWalk(j) || IsHalf(j)) { segs.Add((pi, Planet.Cells[j].dir * Planet.R, hw)); any = true; }
            if (!any) segs.Add((pi, pi, hw));
        }
        if (segs.Count == 0) return true;
        float ts = M != null ? MV2.Ts(M) : 1f;
        bool Ok(Vector2 p, float extra)
        {
            Vector3 w = o + A * p.x + B * p.y;
            foreach (var (a, b, hw) in segs) { Vector3 ab = b - a; float t = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector3.Dot(w - a, ab) / ab.sqrMagnitude); if ((a + ab * t - w).magnitude < hw + extra) return false; }
            return true;
        }
        if (M == null) { if (!Ok(Vector2.zero, 2f)) return false; for (int k = 0; k < 6; k++) { float an = k * Mathf.PI / 3f; if (!Ok(new Vector2(Mathf.Cos(an), Mathf.Sin(an)) * 5f, 1f)) return false; } return true; }
        foreach (var L in M.lv)
        {
            foreach (var kv in L.w) { if (kv.Key.Item3 < 0 || kv.Key.Item3 > 5) continue; var (a, b) = MV2.WallSeg(kv.Key, ts); if (!Ok(a, 1f) || !Ok(b, 1f) || !Ok((a + b) * 0.5f, 1f)) return false; }
            foreach (var O in L.o) if (!Ok(new Vector2(O.x, O.z), 0.8f)) return false;
        }
        return true;
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
