using System;
using System.Collections.Generic;
using UnityEngine;

// Getekende texturen voor modules, zoals mvTex/modernMats in de webversie (08c-modules-v2.js): per materiaal een herhalend patroon
// op ware schaal (wm × hm meter), bij het eerste gebruik in het spel getekend. De meshes hebben texturecoördinaten in meters
// (ModuleMesh.Finish), dus mainTextureScale = 1/wm, 1/hm. Glas en water houden alleen hun kleur.
public static class ModuleTex
{
    static readonly Dictionary<string, (Texture2D tex, Vector2 size)> cache = new Dictionary<string, (Texture2D, Vector2)>();

    public static void Apply(string key, Material m)
    {
        var t = Get(key); if (t.tex == null) return;
        m.mainTexture = t.tex; m.mainTextureScale = new Vector2(1f / t.size.x, 1f / t.size.y);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white); m.color = Color.white;   // de kleur zit in de textuur
    }
    public static (Texture2D tex, Vector2 size) Get(string key)
    {
        if (cache.TryGetValue(key, out var c) && c.tex != null) return c;
        c = Make(key); cache[key] = c; return c;
    }

    // ---------- tekenen ----------
    class P
    {
        public int w, h; public float ppm; public Color[] px; public System.Random rnd;
        public P(float wm, float hm, float ppm, int seed) { this.ppm = ppm; w = Mathf.Max(8, Mathf.RoundToInt(wm * ppm)); h = Mathf.Max(8, Mathf.RoundToInt(hm * ppm)); px = new Color[w * h]; rnd = new System.Random(seed); }
        public float R() => (float)rnd.NextDouble();
        public void Fill(Color c) { for (int i = 0; i < px.Length; i++) px[i] = c; }
        // rechthoek in meters (x0, y0) – (x1, y1), herhalend over de randen
        public void Rect(float x0, float y0, float x1, float y1, Color c, float a = 1f)
        {
            int i0 = Mathf.RoundToInt(x0 * ppm), i1 = Mathf.RoundToInt(x1 * ppm), j0 = Mathf.RoundToInt(y0 * ppm), j1 = Mathf.RoundToInt(y1 * ppm);
            for (int j = j0; j < j1; j++) for (int i = i0; i < i1; i++) { int x = ((i % w) + w) % w, y = ((j % h) + h) % h; px[y * w + x] = a >= 1f ? c : Color.Lerp(px[y * w + x], c, a); }
        }
        // korrels: lichter en donkerder vlekjes
        public void Grain(float amt, int n, float size = 1.5f)
        {
            for (int k = 0; k < n; k++)
            {
                int x = rnd.Next(w), y = rnd.Next(h), r = Mathf.Max(1, Mathf.RoundToInt(size * (0.5f + R()))); float v = (R() - 0.5f) * 2f * amt;
                for (int j = 0; j < r; j++) for (int i = 0; i < r; i++) { int xx = (x + i) % w, yy = (y + j) % h; var o = px[yy * w + xx]; px[yy * w + xx] = new Color(Mathf.Clamp01(o.r + v), Mathf.Clamp01(o.g + v), Mathf.Clamp01(o.b + v), o.a); }
            }
        }
        // per pixel een beetje ruis (voor stucwerk, beton, asfalt)
        public void Noise(float amt) { for (int i = 0; i < px.Length; i++) { float v = (R() - 0.5f) * 2f * amt; var o = px[i]; px[i] = new Color(Mathf.Clamp01(o.r + v), Mathf.Clamp01(o.g + v), Mathf.Clamp01(o.b + v), o.a); } }
        public Texture2D Done(string name)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "Module " + name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            t.SetPixels(px); t.Apply(true, true); return t;
        }
    }
    static Color H(string hex) => MV2.Hex(hex);
    static Color Vary(Color c, float k) => new Color(Mathf.Clamp01(c.r + k), Mathf.Clamp01(c.g + k), Mathf.Clamp01(c.b + k), c.a);

    // metselwerk: stenen bw × bh, voeg jw, halfsteens verband
    static (Texture2D, Vector2) Brick(string name, Color stone, Color mortar, float bw, float bh, float jw, int cols, int rows, float vary, int seed)
    {
        float wm = bw * cols, hm = bh * rows; var p = new P(wm, hm, 160f, seed); p.Fill(mortar); p.Noise(0.02f);
        for (int r = 0; r < rows; r++) for (int c = -1; c <= cols; c++)
        {
            float x = (c + (r % 2) * 0.5f) * bw, y = r * bh; var col = Vary(stone, (p.R() - 0.5f) * vary);
            p.Rect(x + jw / 2f, y + jw / 2f, x + bw - jw / 2f, y + bh - jw / 2f, col);
        }
        p.Grain(0.04f, p.w * p.h / 40, 1.2f);
        return (p.Done(name), new Vector2(wm, hm));
    }
    // tegels of platen: s × s (of sw × sh), voeg jw
    static (Texture2D, Vector2) Tiles(string name, Color tile, Color joint, float sw, float sh, float jw, int cols, int rows, float vary, int seed, bool offset = false, float ppm = 128f)
    {
        float wm = sw * cols, hm = sh * rows; var p = new P(wm, hm, ppm, seed); p.Fill(joint);
        for (int r = 0; r < rows; r++) for (int c = -1; c <= cols; c++)
        {
            float x = (c + (offset ? (r % 2) * 0.5f : 0f)) * sw, y = r * sh; var col = Vary(tile, (p.R() - 0.5f) * vary);
            p.Rect(x + jw / 2f, y + jw / 2f, x + sw - jw / 2f, y + sh - jw / 2f, col);
        }
        p.Noise(0.015f); p.Grain(0.03f, p.w * p.h / 60, 1.5f);
        return (p.Done(name), new Vector2(wm, hm));
    }
    // planken of lamellen langs x (lengte pl) of staand (vertical)
    static (Texture2D, Vector2) Planks(string name, Color wood, Color gap, float pw, float pl, int n, float vary, int seed, bool vertical)
    {
        float wm = vertical ? pw * n : pl * 2f, hm = vertical ? pl : pw * n; var p = new P(wm, hm, 160f, seed); p.Fill(gap);
        for (int k = 0; k < n; k++)
        {
            var col = Vary(wood, (p.R() - 0.5f) * vary);
            if (vertical) p.Rect(k * pw + 0.006f, 0f, (k + 1) * pw - 0.006f, hm, col);
            else { float sh = p.R() * pl; for (int s = -1; s < 3; s++) { var c2 = Vary(col, (p.R() - 0.5f) * vary * 0.6f); p.Rect(sh + s * pl + 0.004f, k * pw + 0.004f, sh + (s + 1) * pl - 0.004f, (k + 1) * pw - 0.004f, c2); } }
        }
        // nerf: dunne lichtere en donkerdere streepjes langs de plank
        for (int i = 0; i < p.w * p.h / 18; i++) { int x = p.rnd.Next(p.w), y = p.rnd.Next(p.h), len = 4 + p.rnd.Next(14); float v = (p.R() - 0.5f) * 0.08f; for (int s = 0; s < len; s++) { int xx = vertical ? x : (x + s) % p.w, yy = vertical ? (y + s) % p.h : y; var o = p.px[yy * p.w + xx]; p.px[yy * p.w + xx] = Vary(o, v); } }
        return (p.Done(name), new Vector2(wm, hm));
    }
    static (Texture2D, Vector2) Plain(string name, Color c, float wm, float amt, float grain, int seed)
    {
        var p = new P(wm, wm, 96f, seed); p.Fill(c); p.Noise(amt); p.Grain(grain, p.w * p.h / 25, 2f); return (p.Done(name), new Vector2(wm, wm));
    }

    static (Texture2D, Vector2) Make(string key)
    {
        switch (key)
        {
            // ---- gevels ----
            case "w_brick": return Brick(key, H("#8f4a37"), H("#9e968a"), 0.21f, 0.0625f, 0.01f, 5, 8, 0.18f, 1);
            case "w_darkbrick": return Brick(key, H("#3a3734"), H("#8e8a83"), 0.49f, 0.05f, 0.008f, 2, 10, 0.12f, 2);
            case "w_plaster": return Plain(key, H("#f1eee8"), 2f, 0.015f, 0.03f, 3);
            case "ceiling": return Plain(key, H("#e9e6df"), 2f, 0.01f, 0.02f, 4);
            case "w_concrete": case "slab":
                {
                    var p = new P(2.4f, 1.2f, 96f, 5); p.Fill(H(key == "slab" ? "#bcbcb6" : "#b8b7b0")); p.Noise(0.03f); p.Grain(0.05f, p.w * p.h / 20, 2f);
                    var line = H("#7d7c76"); p.Rect(0f, 0f, 2.4f, 0.015f, line, 0.6f); p.Rect(0f, 0f, 0.015f, 1.2f, line, 0.6f);
                    foreach (var (fx, fy) in new[] { (0.3f, 0.3f), (0.9f, 0.3f), (1.5f, 0.3f), (2.1f, 0.3f), (0.3f, 0.9f), (0.9f, 0.9f), (1.5f, 0.9f), (2.1f, 0.9f) }) p.Rect(fx - 0.012f, fy - 0.012f, fx + 0.012f, fy + 0.012f, line, 0.8f);   // ankergaatjes
                    return (p.Done(key), new Vector2(2.4f, 1.2f));
                }
            case "w_panel":
                {
                    var p = new P(3.6f, 3f, 64f, 6); for (int i = 0; i < 3; i++) p.Rect(i * 1.2f, 0f, (i + 1) * 1.2f, 3f, Vary(H("#4b5157"), (p.R() - 0.5f) * 0.06f));
                    for (int i = 0; i <= 3; i++) p.Rect(i * 1.2f - 0.02f, 0f, i * 1.2f + 0.02f, 3f, H("#1d2023")); p.Rect(0f, 0f, 3.6f, 0.03f, H("#1d2023")); p.Rect(0f, 2.97f, 3.6f, 3f, H("#1d2023"));
                    p.Noise(0.01f); return (p.Done(key), new Vector2(3.6f, 3f));
                }
            case "w_slats": return Planks(key, H("#a8743f"), H("#2a1f17"), 0.09f, 3f, 10, 0.12f, 7, true);
            case "w_wood": case "bench": return Planks(key, H("#9a6a3c"), H("#5a3a22"), 0.15f, 3f, 6, 0.1f, 8, false);
            case "w_corten":
                {
                    var p = new P(2f, 3f, 64f, 9); p.Fill(H("#97512e")); p.Noise(0.04f); p.Grain(0.08f, p.w * p.h / 6, 2.5f);
                    for (int y = 0; y < 3; y++) p.Rect(0f, y, 2f, y + 0.02f, H("#4a2010"), 0.7f); return (p.Done(key), new Vector2(2f, 3f));
                }
            case "w_stone": case "step": return Tiles(key, H(key == "step" ? "#b7b1a5" : "#bdb5a5"), H("#8e877a"), 0.6f, 0.3f, 0.012f, 4, 6, 0.1f, 10, true);
            case "w_hedge":
                {
                    var p = new P(1.5f, 1.5f, 96f, 11); p.Fill(H("#4a8236")); p.Grain(0.12f, p.w * p.h / 3, 2.5f); p.Grain(-0.1f, p.w * p.h / 10, 1.5f); return (p.Done(key), new Vector2(1.5f, 1.5f));
                }
            // ---- vloeren ----
            case "f_paver": case "ramptop": return Tiles(key, H(key == "ramptop" ? "#a7a298" : "#8a5141"), H(key == "ramptop" ? "#7b776f" : "#5e3a30"), 0.2f, 0.1f, 0.01f, 8, 16, 0.16f, 12, true, 160f);
            case "f_tile": return Tiles(key, H("#cbc8bd"), H("#9d9a90"), 0.3f, 0.3f, 0.008f, 6, 6, 0.05f, 13);
            case "f_wood": return Planks(key, H("#a97c52"), H("#6b4a2e"), 0.15f, 1.2f, 8, 0.1f, 14, false);
            case "f_parquet":
                {   // visgraat: per kolom schuine latjes (zoals de webversie)
                    var p = new P(1.2f, 1.2f, 128f, 15); p.Fill(H("#6e4a2c"));
                    float lw = 0.3f, pw = 0.08f; for (int y = 0; y < p.h; y++) for (int x = 0; x < p.w; x++)
                    {
                        float mx = x / p.ppm, my = y / p.ppm; int col = Mathf.FloorToInt(mx / lw); float lx = mx - col * lw, sgn = col % 2 == 0 ? 1f : -1f;
                        float d = my + sgn * (lx - lw / 2f); int band = Mathf.FloorToInt(d / pw); float f = d / pw - band;
                        float k = (float)((band * 73856093 ^ col * 19349663) & 1023) / 1023f;
                        p.px[y * p.w + x] = f < 0.07f ? H("#4a301c") : Vary(H("#9a6a3e"), (k - 0.5f) * 0.12f);
                    }
                    p.Grain(0.03f, p.w * p.h / 30, 1.2f); return (p.Done(key), new Vector2(1.2f, 1.2f));
                }
            case "f_carpet": return Plain(key, H("#8b9397"), 1f, 0.04f, 0.02f, 16);
            case "f_concrete": return Tiles(key, H("#c4c6c2"), H("#9a9c98"), 3f, 3f, 0.01f, 1, 1, 0.02f, 17, false, 64f);
            case "f_polished": return Plain(key, H("#c9c8c3"), 2f, 0.012f, 0.04f, 18);
            case "f_stonetile": return Tiles(key, H("#d9d0bf"), H("#a39a8a"), 0.6f, 0.6f, 0.01f, 2, 2, 0.08f, 19);
            case "f_grass":
                {
                    var p = new P(2f, 2f, 96f, 20); p.Fill(H("#6fa356")); p.Noise(0.03f); p.Grain(0.08f, p.w * p.h / 4, 1.5f); p.Grain(-0.06f, p.w * p.h / 8, 2.5f); return (p.Done(key), new Vector2(2f, 2f));
                }
            case "f_asphalt": return Plain(key, H("#3c4045"), 2f, 0.04f, 0.05f, 21);
            case "f_roof": return Plain(key, H("#5a5d59"), 2f, 0.05f, 0.06f, 22);   // grind op een plat dak
            case "roof":
                {   // dakpannen: rijen van 0,33 m, pannen van 0,25 m breed met een schaduwrand onderaan
                    var p = new P(1f, 0.99f, 160f, 23); p.Fill(H("#8c4b3a"));
                    for (int r = 0; r < 3; r++) for (int c = -1; c <= 4; c++)
                    {
                        float x = (c + (r % 2) * 0.5f) * 0.25f, y = r * 0.33f; var col = Vary(H("#94503d"), (p.R() - 0.5f) * 0.12f);
                        p.Rect(x + 0.006f, y + 0.03f, x + 0.25f - 0.006f, y + 0.33f, col); p.Rect(x, y, x + 0.25f, y + 0.03f, H("#5a2c20"));
                        p.Rect(x + 0.1f, y + 0.06f, x + 0.15f, y + 0.33f, Vary(col, 0.05f), 0.5f);   // de bolling van de pan
                    }
                    p.Grain(0.03f, p.w * p.h / 40, 1.2f); return (p.Done(key), new Vector2(1f, 0.99f));
                }
        }
        return (null, Vector2.one);
    }
}
