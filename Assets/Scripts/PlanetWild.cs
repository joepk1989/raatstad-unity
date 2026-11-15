using System.Collections.Generic;
using UnityEngine;

// Natuurplaneet (menu): de hele bol zonder stad of weg. Continenten en oceanen uit grove ruis, bergketens met kammen tot TerrTop,
// rivieren die van de heuvels naar zee lopen (een geul onder het zeeniveau), meren in de laagtes. Het klimaat volgt de breedte en
// de hoogte: ijskappen en zee-ijs bij de polen, naaldbos in het koude noorden en zuiden, gemengd bos en heide in het gematigde deel,
// woestijn met cactussen en palmstranden rond de evenaar, rotsblokken op steile hellingen en boven de boomgrens.
// De landschappen zijn gewone blokken op één vak die in het terrein liggen (OpenGround): "ijs", "woestijn", "heide", "palmen" en
// "rotsen"; ijs, woestijn en heide kleuren de grond van het vak (WildTag). Bouw je er iets op, dan wijkt de natuur. Vast bepaald door de seed.
public static partial class PlanetCity
{
    public static bool IsWild(string t) => t == "ijs" || t == "woestijn" || t == "heide" || t == "droogheide" || t == "palmen" || t == "rotsen";
    public static int WildRivers;

    public static System.Collections.IEnumerator WildSteps(System.Action<float, string> prog, int seed = 7)
    {
        ClearAll(); FillTowns.Clear(); HeathVer = HeathVersion; var rnd = new System.Random(seed); int n = Planet.Cells.Length; float o = seed * 17.3f % 500f;
        var hh = new float[n];
        prog?.Invoke(0f, "Continenten en oceanen"); yield return null;
        for (int c = 0; c < n; c++)
        {
            Vector3 d = Planet.Cells[c].dir;
            float e = Noise(d, 1f / 300f, o) * 0.65f + Noise(d, 1f / 110f, o + 11f) * 0.35f - 0.48f, h;
            if (e < 0f) h = Mathf.Max(TerrLow + 0.5f, SeaLevel - 0.6f - (-e) * 70f);   // de zee: flauw van de kust naar het diepe
            else
            {
                h = e * 45f + (Noise(d, 1f / 35f, o + 3f) - 0.5f) * 2f * Mathf.Clamp01(e * 10f);   // heuvelland, met wat kleine bulten
                float mt = Mathf.Max(0f, (Noise(d, 1f / 200f, o + 91f) - 0.55f) / 0.45f) * Mathf.Clamp01(e * 6f), ridge = 1f - Mathf.Abs(2f * Noise(d, 1f / 95f, o + 17f) - 1f);
                if (mt > 0f) h = Mathf.Max(h, Mathf.Pow(mt, 0.75f) * (50f + 45f * ridge));   // bergketens, niet aan de kust
            }
            float flag = 1e9f; foreach (int st in Planet.Starts) flag = Mathf.Min(flag, Mathf.Acos(Mathf.Clamp(Vector3.Dot(d, Planet.Cells[st].dir), -1f, 1f)) * Planet.R / 15.6f);
            if (flag < 2.5f) h = 0.5f;   // de startplekken en polen op een vlak stukje land
            hh[c] = h;
            if (c % 4000 == 3999) { prog?.Invoke(0.3f * c / n, "Continenten en oceanen"); yield return null; }
        }
        // rivieren: vanaf een bron in de heuvels steeds naar het laagste buurvak tot in zee; vast in een kuil: daar een meertje
        prog?.Invoke(0.3f, "Rivieren"); yield return null;
        var river = new HashSet<int>(); WildRivers = 0;
        for (int tries = 0; tries < 4000 && WildRivers < 40; tries++)
        {
            int s = rnd.Next(n); if (hh[s] < 6f || hh[s] > 30f || river.Contains(s)) continue;
            bool near = false; foreach (int j in Planet.Cells[s].nb) if (river.Contains(j)) near = true; if (near) continue;
            var path = new List<int>(); var seen = new HashSet<int>(); int cur = s; bool sea = false;
            for (int step = 0; step < 220; step++)
            {
                path.Add(cur); seen.Add(cur);
                if (hh[cur] < SeaLevel || river.Contains(cur) && step > 0) { sea = true; break; }   // in zee of in een andere rivier
                int best = -1; float bh = float.MaxValue;
                foreach (int j in Planet.Cells[cur].nb) if (!seen.Contains(j) && hh[j] < bh) { bh = hh[j]; best = j; }
                if (best < 0 || bh > hh[cur] + 1.5f) break;   // een kuil: hier eindigt hij in een meertje
                cur = best;
            }
            if (path.Count < 6) continue;
            foreach (int p in path) { if (Planet.IsPole(p) || System.Array.IndexOf(Planet.Starts, p) >= 0) continue; river.Add(p); hh[p] = Mathf.Min(hh[p], SeaLevel - (sea ? 1.3f : 2.5f)); }
            if (!sea) foreach (int j in Planet.Cells[cur].nb) hh[j] = Mathf.Min(hh[j], SeaLevel - 1.5f);
            WildRivers++;
        }
        // meren in de laagtes
        for (int c = 0; c < n; c++)
        {
            if (hh[c] <= SeaLevel || hh[c] > 5f) continue; Vector3 d = Planet.Cells[c].dir; float wet = Noise(d, 1f / 70f, o + 37f);
            if (wet > 0.72f) hh[c] = SeaLevel - 1f - (wet - 0.72f) * 25f;
        }
        for (int c = 0; c < n; c++) if (!Planet.IsPole(c)) SetTerr(c, hh[c]);
        // klimaat en begroeiing
        prog?.Invoke(0.4f, "IJs, bossen, woestijn en heide"); yield return null;
        for (int c = 0; c < n; c++)
        {
            if (c % 3000 == 2999) { prog?.Invoke(0.4f + 0.6f * c / n, "IJs, bossen, woestijn en heide"); yield return null; }
            if (Planet.IsPole(c) || System.Array.IndexOf(Planet.Starts, c) >= 0 || river.Contains(c)) continue;
            Vector3 d = Planet.Cells[c].dir; float h = hh[c], r = (float)rnd.NextDouble();
            LatLon(d, out float lat, out _);
            float T = 1f - Mathf.Abs(lat) / 80f - Mathf.Max(0f, h) / 110f + (Noise(d, 1f / 150f, o + 5f) - 0.5f) * 0.25f;   // warmte: breedte, hoogte en wat ruis
            float W = Noise(d, 1f / 120f, o + 33f);   // vocht
            if (h < SeaLevel) { if (T < 0.06f && h > SeaLevel - 6f) { SetTerr(c, SeaLevel + 0.25f); Place(c, "ijs"); } continue; }   // zee-ijs bij de polen
            float slope = 0f; bool coast = false; foreach (int j in Planet.Cells[c].nb) { slope = Mathf.Max(slope, Mathf.Abs(hh[j] - h) / 15.6f); if (hh[j] < SeaLevel) coast = true; }
            if (T < 0.14f) Place(c, "ijs");
            else if (h > SnowLine) { if (r < 0.12f) Place(c, "rotsen"); }
            else if (h > TreeLine) { if (r < 0.3f) Place(c, "rotsen"); }
            else if (T > 0.66f && coast && h < 2.5f) { if (r < 0.8f) Place(c, "palmen"); }
            else if (T > 0.76f && W < 0.45f) Place(c, "woestijn");
            else if (slope > 0.9f) { if (r < 0.45f) Place(c, "rotsen"); }
            else if (W > 0.55f) { if (r < 0.9f) Place(c, "park"); }
            else if (W < 0.36f && T > 0.32f && T < 0.72f) { if (r < 0.85f) Place(c, "heide"); }
            else if (W > 0.45f) { if (r < 0.4f) Place(c, "park"); }
            else if (r < 0.04f) Place(c, "rotsen");
        }
        TouchAll(); prog?.Invoke(1f, "Klaar");
    }

    // de grondkleur van een landschapsvak (NatureTag)
    static string WildTag(int i)
    {
        if (!Cells.TryGetValue(i, out var l) || l.Count != 1) return null;
        switch (l[0]) { case "ijs": return "ijs"; case "woestijn": return "woestijn"; case "heide": return "heide"; case "droogheide": return "droogheide"; default: return null; }
    }

    // wat er op een landschapsvak staat; alles op de grond onder zijn eigen voet, niet onder water
    static void Wild(System.Func<string, MeshBuilder> M, Frame fr, int i, int hash, string t)
    {
        var rnd = new System.Random(hash ^ 0x2545F491);
        float Rnd() => (float)rnd.NextDouble();
        bool Spot(out float x, out float z, out float th, out float slope)
        {
            float a = Rnd() * Mathf.PI * 2f, rr = Mathf.Sqrt(Rnd()) * 0.85f * fr.rad; x = Mathf.Cos(a) * rr; z = Mathf.Sin(a) * rr;
            Vector3 w = fr.o + fr.e * x + fr.n * z; float gt = TerrainAt(w.normalized);
            float gx = TerrainAt((w + fr.e * 1.5f).normalized) - TerrainAt((w - fr.e * 1.5f).normalized), gz = TerrainAt((w + fr.n * 1.5f).normalized) - TerrainAt((w - fr.n * 1.5f).normalized);
            slope = new Vector2(gx, gz).magnitude / 3f; th = gt - G(i) - 0.15f - slope * 0.4f;
            return gt > SeaLevel + 0.15f;
        }
        switch (t)
        {
            case "ijs":   // hier en daar een opgestuwd ijsblok
                for (int s = 0, cnt = rnd.Next(4); s < cnt; s++)
                    if (Spot(out float x, out float z, out float th, out _)) { float sz = 0.6f + Rnd() * 1.1f; Prism(M("ijsblok"), fr, x, z, Rnd(), sz, sz * 0.7f, th, th + 0.4f + Rnd() * 1.5f, true, false); }
                break;
            case "woestijn":   // cactussen (saguaro met armen), dorre struikjes en een steen
                for (int s = 0, cnt = 1 + rnd.Next(3); s < cnt; s++)
                {
                    if (!Spot(out float x, out float z, out float th, out _)) continue;
                    float H = 3f + Rnd() * 2.8f, rot = Rnd(); Prism(M("cactus"), fr, x, z, rot, 0.38f, 0.32f, th, th + H, true, false);
                    for (int a = 0, arms = rnd.Next(3); a < arms; a++)
                    {
                        float ang = Rnd() * Mathf.PI * 2f, y = th + H * (0.4f + Rnd() * 0.25f); Vector2 sd = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 0.95f;
                        Tube(M("cactus"), fr, new Vector3(x, y, z), new Vector3(x + sd.x, y, z + sd.y), 0.22f, 0.22f);
                        Prism(M("cactus"), fr, x + sd.x, z + sd.y, rot, 0.22f, 0.19f, y - 0.25f, y + 1.1f + Rnd() * 0.6f, true, false);
                    }
                }
                for (int s = 0, cnt = 2 + rnd.Next(3); s < cnt; s++)
                    if (Spot(out float x, out float z, out float th, out _)) { float sz = 0.4f + Rnd() * 0.4f; Prism(M("droog"), fr, x, z, Rnd(), sz, sz * 0.4f, th, th + sz, true, false); }
                if (Rnd() < 0.5f && Spot(out float kx, out float kz, out float kth, out _)) Boulder(M, fr, kx, kz, kth, 0.6f + Rnd() * 0.7f, Rnd());
                break;
            case "heide":   // paarse struikjes dicht bij elkaar, soms een eenzame berk of den
                for (int s = 0, cnt = 10 + rnd.Next(6); s < cnt; s++)
                    if (Spot(out float x, out float z, out float th, out _)) { float sz = 0.45f + Rnd() * 0.4f; Prism(M("heideplant"), fr, x, z, Rnd(), sz, sz * 0.45f, th, th + 0.35f + Rnd() * 0.3f, true, false); }
                if (Rnd() < 0.3f && Spot(out float bx, out float bz, out float bth, out float bsl) && bsl < 0.7f) TreeAt(M, fr, Rnd() < 0.6f ? 2 : 1, bx, bz, bth, 0.7f + Rnd() * 0.3f, Rnd(), rnd);
                break;
            case "droogheide":   // droge heide: zandige grond, plukken geelbruin gras, een paar verdorde heidestruikjes, soms een den
                for (int s = 0, cnt = 9 + rnd.Next(6); s < cnt; s++)
                    if (Spot(out float x, out float z, out float th, out _)) { float sz = 0.35f + Rnd() * 0.35f; Prism(M("droog"), fr, x, z, Rnd(), sz, sz * 0.25f, th, th + 0.4f + Rnd() * 0.35f, true, false); }
                for (int s = 0, cnt = 2 + rnd.Next(4); s < cnt; s++)
                    if (Spot(out float x, out float z, out float th, out _)) { float sz = 0.35f + Rnd() * 0.3f; Prism(M("heidedor"), fr, x, z, Rnd(), sz, sz * 0.45f, th, th + 0.3f + Rnd() * 0.2f, true, false); }
                for (int s = 0, cnt = rnd.Next(4); s < cnt; s++)   // losse donkere struiken (jeneverbes, jonge eik) en hier en daar een eik of den
                {
                    if (!Spot(out float dx, out float dz, out float dth, out float dsl) || dsl > 0.7f) continue; float r = Rnd();
                    if (r < 0.55f) { float sz = 0.9f + Rnd() * 0.9f; Prism(M("struikdonker"), fr, dx, dz, Rnd(), sz * 0.7f, sz, dth - 0.1f, dth + sz * 0.9f, false, false); Prism(M("struikdonker"), fr, dx, dz, Rnd(), sz, sz * 0.5f, dth + sz * 0.9f, dth + sz * 1.9f, true, false); }
                    else TreeAt(M, fr, r < 0.8f ? 0 : 1, dx, dz, dth, 0.5f + Rnd() * 0.35f, Rnd(), rnd);
                }
                if (Rnd() < 0.12f && Spot(out float qx, out float qz, out float qth, out _)) Boulder(M, fr, qx, qz, qth, 0.4f + Rnd() * 0.4f, Rnd());
                break;
            case "palmen":   // kokospalmen die naar het water overhellen
                for (int s = 0, cnt = 2 + rnd.Next(3); s < cnt; s++) if (Spot(out float x, out float z, out float th, out _)) Palm(M, fr, x, z, th, 0.85f + Rnd() * 0.35f, rnd);
                break;
            case "rotsen":   // rotsblokken, een groot en een paar kleinere
                for (int s = 0, cnt = 2 + rnd.Next(4); s < cnt; s++)
                    if (Spot(out float x, out float z, out float th, out _)) Boulder(M, fr, x, z, th, (s == 0 ? 1.6f : 0.6f) + Rnd() * 1.2f, Rnd());
                break;
        }
    }

    // een rotsblok: een scheve zeshoekige knol, half in de grond
    static void Boulder(System.Func<string, MeshBuilder> M, Frame fr, float x, float z, float th, float s, float rot)
    {
        Prism(M("kei"), fr, x, z, rot, s, s * 0.55f, th - 0.3f * s, th + 0.75f * s, true, false);
        Prism(M("kei"), fr, x + s * 0.5f, z - s * 0.3f, rot + 0.4f, s * 0.55f, s * 0.3f, th - 0.2f * s, th + 0.45f * s, true, false);
    }

    // een kokospalm: een gebogen stam in vier stukken en zeven bladeren die uitwaaieren en afhangen
    static void Palm(System.Func<string, MeshBuilder> M, Frame fr, float x, float z, float th, float sc, System.Random rnd)
    {
        float la = (float)rnd.NextDouble() * Mathf.PI * 2f, H = (7f + (float)rnd.NextDouble() * 3f) * sc; Vector3 lean = new Vector3(Mathf.Cos(la), 0f, Mathf.Sin(la)) * 2.4f * sc;
        Vector3 prev = new Vector3(x, th, z);
        for (int s = 1; s <= 4; s++)
        {
            float t = s / 4f; Vector3 p = new Vector3(x, th + t * H, z) + lean * (t * t);
            Tube(M("palmstam"), fr, prev, p, Mathf.Lerp(0.32f, 0.2f, (s - 1) / 4f) * sc, Mathf.Lerp(0.32f, 0.2f, t) * sc); prev = p;
        }
        Vector3 top = prev; float a0 = (float)rnd.NextDouble() * 6.28f;
        for (int k = 0; k < 7; k++)
        {
            float a = a0 + k * Mathf.PI * 2f / 7f; Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), side = new Vector3(-d.z, 0f, d.x) * 0.55f * sc;
            Vector3 mid = top + d * 1.8f * sc + Vector3.up * 0.5f * sc, tip = top + d * 3.7f * sc - Vector3.up * 1.4f * sc;
            Vector3 T = fr.P(top.x, top.y, top.z), A = fr.P(mid.x + side.x, mid.y, mid.z + side.z), B = fr.P(mid.x - side.x, mid.y, mid.z - side.z), P = fr.P(tip.x, tip.y, tip.z);
            foreach (float up in new[] { 1f, -1f }) { M("palmblad").Tri(T, A, P, fr.u * up); M("palmblad").Tri(T, P, B, fr.u * up); }   // van boven en van onder zichtbaar
        }
    }

    // zeshoekige buis van a naar b (in de coördinaten van het vak: x oost, y omhoog, z noord), met een deksel op b
    static void Tube(MeshBuilder m, Frame f, Vector3 a, Vector3 b, float r0, float r1)
    {
        Vector3 ax = (b - a).normalized, u = Vector3.Cross(ax, Vector3.forward); if (u.sqrMagnitude < 0.01f) u = Vector3.Cross(ax, Vector3.right); u.Normalize(); Vector3 v = Vector3.Cross(ax, u);
        System.Func<Vector3, Vector3> W = p => f.P(p.x, p.y, p.z);
        for (int k = 0; k < 6; k++)
        {
            float t0 = k * Mathf.PI / 3f, t1 = t0 + Mathf.PI / 3f, tm = (t0 + t1) * 0.5f;
            Vector3 d0 = u * Mathf.Cos(t0) + v * Mathf.Sin(t0), d1 = u * Mathf.Cos(t1) + v * Mathf.Sin(t1), dm = u * Mathf.Cos(tm) + v * Mathf.Sin(tm);
            m.Quad(W(a + d0 * r0), W(a + d1 * r0), W(b + d1 * r1), W(b + d0 * r1), f.D(dm.x, dm.y, dm.z));
            m.Tri(W(b), W(b + d0 * r1), W(b + d1 * r1), f.D(ax.x, ax.y, ax.z));
        }
    }
}
