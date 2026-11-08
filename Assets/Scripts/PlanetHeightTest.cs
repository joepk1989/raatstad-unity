using UnityEngine;

// Terreintest (Nieuwe stad op het startscherm): een lege planeet om wegen, verdiepte wegen, bruggen en tunnels over grote hoogtes en
// dieptes te slepen. De bol is verdeeld in de 20 vlakken van de icosaëder (rond Planet.Starts, zo'n 220 m van midden tot rand); elk
// vlak heeft één landschap, gemeten in het raakvlak van zijn midden (x naar het oosten, y naar het noorden, in meters). Langs de
// randen tussen de vlakken loopt het terrein terug naar 0: vlakke stroken om van het ene landschap naar het andere te rijden.
// Het vlak van de noordpool (waar de camera begint) heeft de steilste hellingen; de zuidpool een tafelberg op 90 m.
public static partial class PlanetCity
{
    public static readonly string[] HeightTestNames =
    {
        "Steilwand en steile hellingen", "Bergmassief", "Hoogvlakte met droog ravijn", "Hoogvlakte met waterkloof", "Diep dal tussen kammen",
        "Terrassen van 9 m", "Helling van 10%", "Krater", "Naaldpieken", "Lange bergkam", "Binnenzee", "Heuvelland", "Canyonland",
        "Vulkaan", "Trap van 15 m tot 90 m", "Eilanden", "Dubbel dal", "Bergpas", "Rotsrichels van 30 m", "Tafelberg op 90 m",
    };

    public static System.Collections.IEnumerator HeightTestSteps(System.Action<float, string> prog)
    {
        ClearAll(); FillTowns.Clear(); int n = Planet.Cells.Length, S = Planet.Starts.Length;
        // landschap per vlak: de noordpool de steilste hellingen, de zuidpool de tafelberg, de rest op volgorde
        var kind = new int[S]; int next = 1;
        for (int s = 0; s < S; s++) kind[s] = Planet.Starts[s] == Planet.North ? 0 : Planet.Starts[s] == Planet.South ? 19 : next++;
        prog?.Invoke(0f, "Bergen, dalen en ravijnen"); yield return null;
        for (int c = 0; c < n; c++)
        {
            if (Planet.IsPole(c)) continue;
            Vector3 d = Planet.Cells[c].dir; int s1 = 0; float a1 = -2f, a2 = -2f;
            for (int s = 0; s < S; s++) { float a = Vector3.Dot(d, Planet.Cells[Planet.Starts[s]].dir); if (a > a1) { a2 = a1; a1 = a; s1 = s; } else if (a > a2) a2 = a; }
            float border = (Mathf.Acos(Mathf.Clamp(a2, -1f, 1f)) - Mathf.Acos(Mathf.Clamp(a1, -1f, 1f))) * 0.5f * Planet.R;   // afstand tot de rand van het vlak
            var mid = Planet.Cells[Planet.Starts[s1]];
            float x = Vector3.Dot(d, mid.east) * Planet.R, y = Vector3.Dot(d, mid.north) * Planet.R;
            float h = HeightTestAt(kind[s1], x, y, d) * Smooth(8f, 40f, border);
            if (Mathf.Abs(h) > 0.05f) SetTerr(c, h);
            if (c % 4000 == 3999) { prog?.Invoke((float)c / n, "Bergen, dalen en ravijnen"); yield return null; }
        }
        TouchAll();
        yield return HeightTestRoads(prog);   // wegen van startpunt naar startpunt (PlanetHeightTest.Roads.cs)
        prog?.Invoke(1f, "Klaar");
    }

    static float Smooth(float a, float b, float v) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, v));
    static float Cone(float x, float y, float cx, float cy, float rad, float top, float pow = 1f) => top * Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / rad), pow);

    static float HeightTestAt(int k, float x, float y, Vector3 d)
    {
        float r = Mathf.Sqrt(x * x + y * y), ax = Mathf.Abs(x), ay = Mathf.Abs(y);
        switch (k)
        {
            case 0:   // noordpool: vlak rond de pool; naar het noorden links een loodrechte wand van 90 m (één vak), rechts 150% (90 m over 60 m); naar het zuiden 50% tot 60 m
                if (r < 45f) return 0f;
                if (y > 50f) return x < 0f ? (y > 62f ? 90f : 0f) : Mathf.Clamp01((y - 50f) / 60f) * 90f;
                if (y < -50f) return Mathf.Clamp01((-50f - y) / 120f) * 60f;
                return 0f;
            case 1: return Mathf.Max(Cone(x, y, 0f, 0f, 170f, 90f, 0.85f), Cone(x, y, 90f, 70f, 100f, 70f, 0.85f));   // bergmassief met een tweede top
            case 2:   // hoogvlakte op 36 m, droog ravijn (bodem 4 m) van west naar oost
                return Mathf.Lerp(4f, 36f, Smooth(14f, 30f, ay));
            case 3:   // hoogvlakte op 30 m, kloof met water (−8 m) van zuid naar noord
                return Mathf.Lerp(-8f, 30f, Smooth(16f, 34f, ax));
            case 4:   // dal onder het zeeniveau (−10 m) tussen twee kammen van 45 m
                return ax < 40f ? -10f : Mathf.Lerp(-10f, 45f, Smooth(40f, 105f, ax));
            case 5: return Mathf.Clamp(Mathf.Floor((y + 160f) / 40f) * 9f, 0f, 63f);   // terrassen van 9 m, van zuid naar noord
            case 6: return Mathf.Clamp((y + 170f) * 0.1f, 0f, 30f);   // helling van 10%
            case 7: return r < 60f ? Mathf.Lerp(-12f, 18f, Smooth(0f, 60f, r)) : Mathf.Lerp(18f, 0f, Smooth(60f, 150f, r));   // krater met rand
            case 8: return Mathf.Max(Cone(x, y, -60f, 0f, 70f, 90f), Cone(x, y, 70f, 30f, 60f, 90f));   // naaldpieken
            case 9: return 80f * Smooth(70f, 10f, ay);   // lange bergkam van west naar oost, 80 m hoog
            case 10: return r < 120f ? -10f : Mathf.Lerp(-10f, 12f, Smooth(120f, 150f, r));   // binnenzee met een steile oever
            case 11: return Mathf.Max(0f, (Noise(d, 1f / 45f, 5f) - 0.3f) / 0.7f * 32f);   // heuvelland
            case 12:   // canyonland: plateau op 45 m met kloven in drie richtingen (60°)
            {
                float g = 1e9f; for (int a = 0; a < 3; a++) { float t = a * Mathf.PI / 3f; g = Mathf.Min(g, Mathf.Abs(-Mathf.Sin(t) * x + Mathf.Cos(t) * y)); }
                return Mathf.Lerp(2f, 45f, Smooth(10f, 22f, g));
            }
            case 13: return r < 30f ? Mathf.Lerp(50f, 90f, Smooth(10f, 30f, r)) : Cone(x, y, 0f, 0f, 170f, 90f * 1.21f, 1.3f);   // vulkaan met krater bovenin
            case 14: return Mathf.Clamp(Mathf.Floor((y + 140f) / 40f) * 15f, 0f, 90f);   // trap van 15 m tot 90 m
            case 15:   // eilanden in zee
            {
                float h = -9f; foreach (var p in new[] { new Vector2(-70f, 40f), new Vector2(60f, 70f), new Vector2(30f, -70f), new Vector2(-50f, -80f), new Vector2(100f, -10f) })
                    h = Mathf.Max(h, Cone(x, y, p.x, p.y, 45f, 40f) - 9f + (Vector2.Distance(new Vector2(x, y), p) < 45f ? 9f * Smooth(45f, 30f, Vector2.Distance(new Vector2(x, y), p)) : 0f));
                return h;
            }
            case 16:   // dubbel dal: twee dalen op −6 m met een kam van 40 m ertussen en hoogland van 40 m erbuiten
                return Mathf.Lerp(-6f, 40f, Smooth(18f, 55f, Mathf.Abs(ax - 70f)));
            case 17:   // bergpas: twee toppen van 90 m met een zadel op 40 m (de pas van zuid naar noord)
                return Mathf.Max(Mathf.Max(Cone(x, y, -90f, 0f, 120f, 90f), Cone(x, y, 90f, 0f, 120f, 90f)), 40f * Smooth(110f, 20f, ay) * Smooth(150f, 60f, ax));
            case 18: return Mathf.Clamp(Mathf.Floor((x + 150f) / 70f) * 30f, 0f, 90f);   // rotsrichels: wanden van 30 m, van west naar oost
            case 19: return r < 45f ? 0f : 90f * Smooth(60f, 75f, r);   // tafelberg op 90 m met steile wanden rond een vlak dal bij de pool
        }
        return 0f;
    }
}
