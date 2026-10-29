using System.Collections.Generic;
using UnityEngine;

// Low-poly voertuigen, naar de webversie (11-0-lowpoly-voertuigen.js, overdracht § 3). Kantige auto's in dezelfde stijl als de
// gebouwen: elke carrosserie is een romp van doorsneden langs de lengte, en elke doorsnede is een zeshoek (onderkant, gordellijn,
// dak), dus overal schuine vlakken. Ruiten liggen als dunne platen op de romp, wielkasten als donkere halve zeshoeken over de flank.
// Alle typen delen de maten van het rijmodel: 4,7 m lang, wielen op ±1,45 m langs en ±0,83 m dwars, wielstraal 0,36 m.
// Doorsnede: [x, yOnder, yGordel, yDak, bOnder, bGordel, bDak] (b = halve breedte). x is de lengte (voor = +x), z de breedte; in
// Unity wordt dat z (vooruit) en x (opzij). Eigen ontwerpen, geen merken.
public static class LowPolyCar
{
    public static readonly string[] Types = { "sedan", "coupe", "suv", "wedge", "stad", "bestel", "taxi" };
    static readonly float[] Weight = { 0.2f, 0.12f, 0.18f, 0.1f, 0.2f, 0.12f, 0.08f };
    static readonly string[] PaintHex = { "#e8c547", "#d9534f", "#4a90a4", "#f2f3f5", "#3d5a80", "#7bb356", "#e07a3f", "#2b2f36", "#b8a9c9", "#c9ced4", "#5a1f2a", "#b5a36a", "#1c1f24" };
    static readonly Color White = Hex("#fff7d6"), Red = Hex("#ff3b30");

    class Parts { public Mesh body, glass, trim, glow, wheel, rim; public List<(Mesh m, Color c)> lamps = new List<(Mesh, Color)>(); public bool metal; }
    static readonly Dictionary<string, Parts> cache = new Dictionary<string, Parts>();

    public static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    // een auto als kind van parent (oorsprong op het wegdek, vooruit = +z); type en kleur willekeurig volgens de kansen van de webversie
    public static string Build(Transform parent)
    {
        float r = Random.value, acc = 0f; int t = 0; for (; t < Types.Length - 1; t++) { acc += Weight[t]; if (r < acc) break; }
        string type = Types[t];
        Color paint = type == "taxi" ? Hex("#f2c230") : type == "wedge" ? Hex("#d3d8dc") : Hex(PaintHex[Random.Range(0, PaintHex.Length)]);
        Build(parent, type, paint); return type;
    }
    public static void Build(Transform parent, string type, Color paint)
    {
        if (!cache.TryGetValue(type, out var P)) { P = Make(type); cache[type] = P; }
        Put(parent, P.body, Mats.Get("lp_" + ColorUtility.ToHtmlStringRGB(paint) + (P.metal ? "m" : ""), paint));
        Put(parent, P.glass, Mats.Get("lp_glas", Hex("#1d2b36")));
        Put(parent, P.trim, Mats.Get("lp_donker", Hex("#25282c")));
        if (P.glow != null) Put(parent, P.glow, Mats.GetUnlit("lp_gloed", Hex("#ff2a3a")));
        foreach (var (m, c) in P.lamps) Put(parent, m, Mats.GetUnlit("lp_lamp" + ColorUtility.ToHtmlStringRGB(c), c));
        Put(parent, P.wheel, Mats.Get("lp_band", Hex("#1b1d20")));
        Put(parent, P.rim, Mats.Get("lp_velg", Hex("#9aa0a6")));
    }
    static void Put(Transform parent, Mesh m, Material mat)
    {
        if (m == null) return;
        var go = new GameObject("deel"); go.transform.SetParent(parent, false); go.layer = parent.gameObject.layer;
        go.AddComponent<MeshFilter>().sharedMesh = m; go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    // ---------- romp en onderdelen (in de maten van de webversie) ----------
    static Vector3 U(float x, float y, float z) => new Vector3(z, y, x);   // webversie (lengte, hoogte, breedte) → Unity
    static Vector3[] Sec(float[] s) => new[] { U(s[0], s[1], -s[4]), U(s[0], s[2], -s[5]), U(s[0], s[3], -s[6]), U(s[0], s[3], s[6]), U(s[0], s[2], s[5]), U(s[0], s[1], s[4]) };
    static void Hull(MeshBuilder mb, List<float[]> S)
    {
        for (int i = 0; i + 1 < S.Count; i++)
        {
            var A = Sec(S[i]); var B = Sec(S[i + 1]); Vector3 c = U((S[i][0] + S[i + 1][0]) * 0.5f, (S[i][1] + S[i][3] + S[i + 1][1] + S[i + 1][3]) * 0.25f, 0f);
            for (int k = 0; k < 6; k++) { int k2 = (k + 1) % 6; Tri(mb, A[k], A[k2], B[k2], c); Tri(mb, A[k], B[k2], B[k], c); }
        }
        foreach (var (j, o) in new[] { (0, 1), (S.Count - 1, S.Count - 2) })   // kopse kanten: weg van de buurdoorsnede
        {
            var C = Sec(S[j]); Vector3 rf = U(S[o][0], (S[j][1] + S[j][3]) * 0.5f, 0f);
            for (int k = 1; k < 5; k++) Tri(mb, C[0], C[k], C[k + 1], rf);
        }
    }
    static void Tri(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 c, Vector3 inside) => mb.Tri(a, b, c, (a + b + c) / 3f - inside);
    // doorsnede op lengte x (lineair tussen de doorsneden)
    static float[] At(List<float[]> S, float x)
    {
        for (int i = 0; i + 1 < S.Count; i++)
        {
            float[] a = S[i], b = S[i + 1]; float lo = Mathf.Min(a[0], b[0]), hi = Mathf.Max(a[0], b[0]);
            if (x >= lo - 1e-6f && x <= hi + 1e-6f) { float t = (x - a[0]) / (Mathf.Abs(b[0] - a[0]) < 1e-6f ? 1f : b[0] - a[0]); var r = new float[7]; for (int k = 0; k < 7; k++) r[k] = a[k] + (b[k] - a[k]) * t; return r; }
        }
        return (float[])(Mathf.Abs(x - S[0][0]) < Mathf.Abs(x - S[S.Count - 1][0]) ? S[0] : S[S.Count - 1]).Clone();
    }
    static float WAt(float[] s, float y) => y <= s[2] ? s[4] + (s[5] - s[4]) * (y - s[1]) / Nz(s[2] - s[1]) : s[5] + (s[6] - s[5]) * (y - s[2]) / Nz(s[3] - s[2]);
    static float Nz(float v) => Mathf.Abs(v) < 1e-6f ? 1f : v;
    static List<float> XsBetween(List<float[]> S, float x0, float x1)
    {
        var l = new List<float> { x0, x1 }; foreach (var s in S) if (s[0] > Mathf.Min(x0, x1) + 0.01f && s[0] < Mathf.Max(x0, x1) - 0.01f) l.Add(s[0]);
        l.Sort(); return l;
    }
    // zijruit: een plaatje net buiten de flank, tussen de fracties f0 en f1 van gordellijn tot dak
    static void WinSide(MeshBuilder mb, List<float[]> S, float x0, float x1, float f0, float f1, float o = 0.012f)
    {
        var l = new List<float[]>(); foreach (float x in XsBetween(S, x0, x1)) { var s = At(S, x); float y0 = s[2] + (s[3] - s[2]) * f0, y1 = s[2] + (s[3] - s[2]) * f1, w0 = WAt(s, y0) + o, w1 = WAt(s, y1) + o; l.Add(new[] { x, y0, y0, y1, w0, w0, w1 }); }
        Hull(mb, l);
    }
    // ruit op het dakvlak (voorruit, achterruit): een plaat net boven het dak, ingesprongen van de randen
    static void WinTop(MeshBuilder mb, List<float[]> S, float x0, float x1, float inset, float up = 0.014f)
    {
        var l = new List<float[]>(); foreach (float x in XsBetween(S, x0, x1)) { var s = At(S, x); float w = Mathf.Max(0.05f, s[6] - inset); l.Add(new[] { x, s[3] - 0.06f, s[3] - 0.06f, s[3] + up, w, w, w }); }
        Hull(mb, l);
    }
    // strook over de flank tussen twee hoogtes, iets buiten de romp; volgt de knik op de gordellijn en blijft onder het dak
    static float[] Strip(float[] s, float x, float y0, float y1, float o)
    {
        y0 = Mathf.Max(s[1], y0); y1 = Mathf.Max(y0 + 0.01f, Mathf.Min(s[3] - 0.05f, y1)); float ym = Mathf.Min(y1, Mathf.Max(y0, s[2]));
        return new[] { x, y0, ym, y1, WAt(s, y0) + o, WAt(s, ym) + o, WAt(s, y1) + o };
    }
    static void Band(MeshBuilder mb, List<float[]> S, float x0, float x1, float y0, float y1, float o = 0.014f)
    {
        var l = new List<float[]>(); foreach (float x in XsBetween(S, x0, x1)) l.Add(Strip(At(S, x), x, y0, y1, o)); Hull(mb, l);
    }
    // wielkast: een donkere halve zeshoek boven het wiel, over de flank
    static void Arch(MeshBuilder mb, List<float[]> S, float wx, float top)
    {
        var l = new List<float[]>(); foreach (var (x, y1) in new[] { (wx - 0.74f, 0.42f), (wx - 0.5f, top), (wx + 0.5f, top), (wx + 0.74f, 0.42f) }) l.Add(Strip(At(S, x), x, 0.3f, y1, 0.018f));
        Hull(mb, l);
    }
    static void Box(MeshBuilder mb, float sx, float sy, float sz, float x, float y, float z)
    {
        Vector3 c = U(x, y, z), h = new Vector3(sz, sy, sx) * 0.5f;
        Vector3 P(int i, int j, int k) => c + new Vector3(i * h.x, j * h.y, k * h.z);
        mb.Quad(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), Vector3.forward); mb.Quad(P(-1, -1, -1), P(1, -1, -1), P(1, 1, -1), P(-1, 1, -1), Vector3.back);
        mb.Quad(P(1, -1, -1), P(1, -1, 1), P(1, 1, 1), P(1, 1, -1), Vector3.right); mb.Quad(P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), Vector3.left);
        mb.Quad(P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1), P(-1, 1, 1), Vector3.up); mb.Quad(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), Vector3.down);
    }
    // wiel: band als tienhoekige schijf, velg als achthoek op de buitenkant
    static void Wheel(MeshBuilder tyre, MeshBuilder rim, float wx, float wz)
    {
        const float R = 0.36f, Wd = 0.24f; float s = Mathf.Sign(wz); Vector3 c = U(wx, R, wz);
        Vector3 Pt(int n, int i, float r, float side) { float a = i * Mathf.PI * 2f / n; return c + new Vector3(side, Mathf.Sin(a) * r, Mathf.Cos(a) * r); }
        for (int i = 0; i < 10; i++)
        {
            Vector3 a0 = Pt(10, i, R, -Wd / 2), a1 = Pt(10, i + 1, R, -Wd / 2), b0 = Pt(10, i, R, Wd / 2), b1 = Pt(10, i + 1, R, Wd / 2);
            tyre.Quad(a0, a1, b1, b0, (a0 + a1) * 0.5f - c - new Vector3(-Wd / 2, 0, 0));
            tyre.Tri(c + new Vector3(-Wd / 2, 0, 0), a0, a1, Vector3.left); tyre.Tri(c + new Vector3(Wd / 2, 0, 0), b0, b1, Vector3.right);
        }
        Vector3 rc = c + new Vector3(s * (Wd / 2 + 0.01f), 0, 0), dir = new Vector3(s, 0, 0);
        for (int i = 0; i < 8; i++) rim.Tri(rc, Pt(8, i, R * 0.62f, s * (Wd / 2 + 0.01f)), Pt(8, i + 1, R * 0.62f, s * (Wd / 2 + 0.01f)), dir);
    }

    static List<float[]> L(params float[][] a) => new List<float[]>(a);
    static Parts Make(string type)
    {
        MeshBuilder body = new MeshBuilder(), glass = new MeshBuilder(), trim = new MeshBuilder(), glow = new MeshBuilder(), tyre = new MeshBuilder(), rim = new MeshBuilder();
        var lamps = new Dictionary<Color, MeshBuilder>();
        void Lamp(Color c, float sx, float sy, float sz, float x, float y, float z) { if (!lamps.TryGetValue(c, out var mb)) { mb = new MeshBuilder(); lamps[c] = mb; } Box(mb, sx, sy, sz, x, y, z); }
        // auto met een losse cabine: romp en cabine in lak, ruiten op de cabine, wielkasten op de romp
        void Car(List<float[]> B, List<float[]> C, float[] front, float[] rear, float[] side, float arch = 0.8f)
        {
            Hull(body, B); Hull(body, C);
            if (front != null) WinTop(glass, C, front[0], front[1], 0.07f);
            if (rear != null) WinTop(glass, C, rear[0], rear[1], 0.07f);
            if (side != null) WinSide(glass, C, side[0], side[1], 0.1f, 0.86f);
            foreach (float wx in new[] { -1.45f, 1.45f }) Arch(trim, B, wx, arch);
        }
        bool metal = false;
        if (type == "sedan")   // hoekige sedan: lange motorkap, schuine ruiten, lichtlijn op de gordellijn
        {
            var B = L(new[] { -2.36f, 0.44f, 0.64f, 0.84f, 0.66f, 0.84f, 0.72f }, new[] { -2.15f, 0.3f, 0.64f, 0.95f, 0.76f, 0.92f, 0.8f }, new[] { 0.6f, 0.3f, 0.64f, 0.93f, 0.78f, 0.93f, 0.8f }, new[] { 2.2f, 0.32f, 0.6f, 0.76f, 0.74f, 0.9f, 0.72f }, new[] { 2.37f, 0.42f, 0.56f, 0.66f, 0.6f, 0.78f, 0.58f });
            var C = L(new[] { -1.62f, 0.92f, 0.92f, 0.95f, 0.79f, 0.79f, 0.77f }, new[] { -1.08f, 0.93f, 0.93f, 1.42f, 0.79f, 0.79f, 0.6f }, new[] { -0.05f, 0.93f, 0.93f, 1.42f, 0.79f, 0.79f, 0.6f }, new[] { 0.64f, 0.92f, 0.92f, 0.94f, 0.79f, 0.79f, 0.77f });
            Car(B, C, new[] { 0.6f, -0.02f }, new[] { -1.58f, -1.1f }, new[] { -1.45f, 0.45f });
            foreach (int sg in new[] { -1, 1 }) Box(glow, 3.95f, 0.03f, 0.02f, -0.05f, 0.64f, sg * 0.935f);
            Band(trim, B, -2.2f, 2.25f, 0.3f, 0.42f);
            foreach (int sg in new[] { -1, 1 }) Lamp(White, 0.06f, 0.07f, 0.44f, 2.31f, 0.64f, sg * 0.5f);
            Lamp(Red, 0.05f, 0.06f, 1.4f, -2.35f, 0.74f, 0f);
        }
        else if (type == "coupe")   // lage wig: platte neus, steile achterkant, donkere streep over de flanken
        {
            var B = L(new[] { -2.36f, 0.42f, 0.62f, 0.86f, 0.7f, 0.88f, 0.76f }, new[] { -2.1f, 0.3f, 0.6f, 0.93f, 0.8f, 0.94f, 0.82f }, new[] { 0.6f, 0.3f, 0.58f, 0.88f, 0.82f, 0.95f, 0.8f }, new[] { 2.0f, 0.3f, 0.55f, 0.66f, 0.8f, 0.93f, 0.76f }, new[] { 2.37f, 0.38f, 0.5f, 0.56f, 0.68f, 0.86f, 0.64f });
            var C = L(new[] { -1.92f, 0.92f, 0.92f, 0.94f, 0.8f, 0.8f, 0.78f }, new[] { -1.2f, 0.91f, 0.91f, 1.28f, 0.8f, 0.8f, 0.6f }, new[] { -0.15f, 0.89f, 0.89f, 1.28f, 0.8f, 0.8f, 0.6f }, new[] { 0.8f, 0.87f, 0.87f, 0.89f, 0.8f, 0.8f, 0.78f });
            Car(B, C, new[] { 0.75f, -0.12f }, new[] { -1.88f, -1.22f }, new[] { -1.65f, 0.6f }, 0.74f);
            Band(trim, B, -2.0f, 1.7f, 0.44f, 0.52f); Band(trim, B, -2.3f, 2.3f, 0.3f, 0.36f);
            foreach (int sg in new[] { -1, 1 }) foreach (float z in new[] { 0.6f, 0.38f }) Lamp(White, 0.05f, 0.08f, 0.17f, 2.33f, 0.5f, sg * z);
            foreach (int sg in new[] { -1, 1 }) Lamp(Red, 0.05f, 0.07f, 0.5f, -2.36f, 0.7f, sg * 0.5f);
        }
        else if (type == "suv")   // hoge SUV: brede kast, groot glasvlak, donkere onderkant en dakrails
        {
            var B = L(new[] { -2.33f, 0.38f, 0.74f, 1.1f, 0.84f, 0.95f, 0.88f }, new[] { -2.18f, 0.34f, 0.74f, 1.13f, 0.88f, 0.97f, 0.92f }, new[] { 1.0f, 0.34f, 0.74f, 1.11f, 0.88f, 0.97f, 0.92f }, new[] { 2.15f, 0.36f, 0.72f, 1.0f, 0.86f, 0.95f, 0.86f }, new[] { 2.37f, 0.48f, 0.7f, 0.9f, 0.78f, 0.9f, 0.76f });
            var C = L(new[] { -2.12f, 1.1f, 1.1f, 1.72f, 0.91f, 0.91f, 0.8f }, new[] { 0.52f, 1.1f, 1.1f, 1.7f, 0.91f, 0.91f, 0.8f }, new[] { 1.1f, 1.08f, 1.08f, 1.1f, 0.91f, 0.91f, 0.9f });
            Car(B, C, new[] { 1.02f, 0.56f }, null, new[] { -1.95f, 0.85f }, 0.92f);
            Hull(glass, L(new[] { -2.135f, 1.2f, 1.2f, 1.62f, 0.75f, 0.75f, 0.66f }, new[] { -2.12f, 1.2f, 1.2f, 1.62f, 0.75f, 0.75f, 0.66f }));   // achterruit
            Band(trim, B, -2.25f, 2.2f, 0.34f, 0.56f);
            foreach (int sg in new[] { -1, 1 }) Box(trim, 2.4f, 0.06f, 0.06f, -0.8f, 1.74f, sg * 0.66f);
            Box(trim, 0.05f, 0.2f, 0.9f, 2.36f, 0.78f, 0f);
            foreach (int sg in new[] { -1, 1 }) { Lamp(White, 0.05f, 0.05f, 0.42f, 2.33f, 0.9f, sg * 0.6f); Lamp(Red, 0.05f, 0.07f, 0.36f, -2.34f, 0.95f, sg * 0.62f); }
        }
        else if (type == "wedge")   // stalen wig-pickup: één schuin vlak van neus tot dak, scherpe wielkasten, lichtbalken
        {
            metal = true;
            var B = L(new[] { -2.37f, 0.42f, 0.98f, 1.3f, 0.84f, 0.95f, 0.84f }, new[] { -2.22f, 0.32f, 1.0f, 1.36f, 0.88f, 0.98f, 0.86f }, new[] { -0.15f, 0.32f, 1.02f, 1.84f, 0.88f, 0.98f, 0.6f }, new[] { 2.18f, 0.32f, 0.86f, 0.94f, 0.88f, 0.98f, 0.88f }, new[] { 2.38f, 0.44f, 0.74f, 0.86f, 0.8f, 0.94f, 0.86f });
            Hull(body, B);
            WinTop(glass, B, 1.78f, -0.08f, 0.1f); WinSide(glass, B, 1.5f, -0.95f, 0.12f, 0.8f);
            foreach (float wx in new[] { -1.45f, 1.45f }) Arch(trim, B, wx, 0.98f);
            Band(trim, B, -2.3f, 2.32f, 0.32f, 0.5f);
            Lamp(White, 0.04f, 0.045f, 1.66f, 2.395f, 0.8f, 0f); Lamp(Red, 0.04f, 0.05f, 1.62f, -2.385f, 1.22f, 0f);
        }
        else if (type == "stad" || type == "taxi")   // kleine hatchback (de taxi met een daklicht)
        {
            var B = L(new[] { -2.32f, 0.4f, 0.62f, 0.84f, 0.7f, 0.86f, 0.74f }, new[] { -2.15f, 0.3f, 0.62f, 0.92f, 0.76f, 0.88f, 0.78f }, new[] { 0.55f, 0.3f, 0.62f, 0.9f, 0.78f, 0.89f, 0.78f }, new[] { 2.1f, 0.3f, 0.6f, 0.8f, 0.76f, 0.87f, 0.74f }, new[] { 2.33f, 0.4f, 0.56f, 0.7f, 0.64f, 0.8f, 0.64f });
            var C = L(new[] { -1.58f, 0.9f, 0.9f, 0.93f, 0.79f, 0.79f, 0.77f }, new[] { -1.18f, 0.91f, 0.91f, 1.42f, 0.79f, 0.79f, 0.64f }, new[] { 0.12f, 0.9f, 0.9f, 1.42f, 0.79f, 0.79f, 0.64f }, new[] { 0.74f, 0.88f, 0.88f, 0.9f, 0.79f, 0.79f, 0.77f });
            Car(B, C, new[] { 0.7f, 0.16f }, new[] { -1.55f, -1.2f }, new[] { -1.4f, 0.5f });
            Band(trim, B, -2.25f, 2.2f, 0.3f, 0.42f);
            if (type == "taxi") Lamp(Hex("#ffe08a"), 0.3f, 0.14f, 0.6f, -0.5f, 1.5f, 0f);
            foreach (int sg in new[] { -1, 1 }) { Lamp(White, 0.06f, 0.1f, 0.3f, 2.31f, 0.64f, sg * 0.55f); Lamp(Red, 0.06f, 0.1f, 0.3f, -2.33f, 0.68f, sg * 0.56f); }
        }
        else   // bestelbus
        {
            var B = L(new[] { -2.34f, 0.36f, 0.9f, 2.0f, 0.84f, 0.93f, 0.84f }, new[] { -2.2f, 0.3f, 0.9f, 2.05f, 0.86f, 0.95f, 0.86f }, new[] { 0.95f, 0.3f, 0.9f, 2.05f, 0.86f, 0.95f, 0.86f }, new[] { 1.5f, 0.3f, 0.9f, 1.04f, 0.86f, 0.95f, 0.88f }, new[] { 2.2f, 0.3f, 0.82f, 0.93f, 0.84f, 0.93f, 0.84f }, new[] { 2.36f, 0.4f, 0.7f, 0.82f, 0.76f, 0.88f, 0.76f });
            Hull(body, B);
            WinTop(glass, B, 1.45f, 1.0f, 0.09f); WinSide(glass, B, 1.0f, 1.42f, 0.12f, 0.85f);
            foreach (float wx in new[] { -1.45f, 1.45f }) Arch(trim, B, wx, 0.82f);
            Band(trim, B, -2.25f, 2.25f, 0.3f, 0.44f);
            foreach (int sg in new[] { -1, 1 }) { Lamp(White, 0.06f, 0.12f, 0.3f, 2.3f, 0.66f, sg * 0.6f); Lamp(Red, 0.06f, 0.14f, 0.24f, -2.36f, 0.75f, sg * 0.66f); }
        }
        foreach (var (wx, wz) in new[] { (1.45f, 0.83f), (1.45f, -0.83f), (-1.45f, 0.83f), (-1.45f, -0.83f) }) Wheel(tyre, rim, wx, wz);
        var P = new Parts { body = body.Build(), glass = glass.Build(), trim = trim.Build(), glow = glow.v.Count > 0 ? glow.Build() : null, wheel = tyre.Build(), rim = rim.Build(), metal = metal };
        foreach (var kv in lamps) P.lamps.Add((kv.Value.Build(), kv.Key));
        return P;
    }
}
