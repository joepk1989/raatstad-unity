using System.Collections.Generic;
using UnityEngine;

// Low-poly voertuigen, naar de webversie (11-0-lowpoly-voertuigen.js, overdracht § 3). Kantige auto's in dezelfde stijl als de
// gebouwen: elke carrosserie is een romp van doorsneden langs de lengte, en elke doorsnede is een zeshoek (onderkant, gordellijn,
// dak), dus overal schuine vlakken. Ruiten liggen als dunne platen op de romp, wielkasten als donkere halve zeshoeken over de flank.
// Daarbij bumpers, kentekenplaten, spiegels, deurnaden en lampen om de hoeken; de wielen zijn losse delen die draaien en meesturen (CarWheels).
// Alle typen delen de maten van het rijmodel: 4,7 m lang, wielen op ±1,45 m langs en ±0,83 m dwars, wielstraal 0,36 m.
// Doorsnede: [x, yOnder, yGordel, yDak, bOnder, bGordel, bDak] (b = halve breedte). x is de lengte (voor = +x), z de breedte; in
// Unity wordt dat z (vooruit) en x (opzij). Eigen ontwerpen, geen merken.
public static class LowPolyCar
{
    public static readonly string[] Types = { "sedan", "coupe", "suv", "stad", "bestel", "taxi" };
    static readonly float[] Weight = { 0.22f, 0.14f, 0.2f, 0.24f, 0.12f, 0.08f };
    // lakkleuren: veel variatie, zoals echt verkeer (veel grijs, wit en zwart, daarnaast blauw, rood, groen, en een enkele felle)
    static readonly string[] PaintHex = {
        "#f2f3f5", "#e6e8ea", "#c9ced4", "#a9b0b8", "#8a9199", "#5c636b", "#3b4047", "#2b2f36", "#1c1f24", "#101215",   // wit, zilver, grijs, zwart
        "#3d5a80", "#2a4a7f", "#1f3b63", "#4a90a4", "#5aa9e6", "#7fb3d5", "#264653", "#1b6f8a",                             // blauw
        "#d9534f", "#b3262e", "#8c1c25", "#5a1f2a", "#e63946", "#c0392b",                                                     // rood
        "#7bb356", "#2d6a4f", "#40916c", "#1b4332", "#8aa66a", "#556b2f",                                                     // groen
        "#e8c547", "#f4a261", "#e07a3f", "#d4a017", "#f6bd60",                                                                // geel en oranje
        "#b8a9c9", "#6d597a", "#9b5de5", "#e5989b", "#ffcad4",                                                                // paars en roze
        "#b5a36a", "#a68a64", "#7f5539", "#ddb892", "#c2b280",                                                                // beige en bruin
        "#00b4a6", "#0081a7", "#83c5be" };                                                                                     // turquoise
    static readonly Color White = Hex("#fff7d6"), Red = Hex("#ff3b30");

    class Parts { public Mesh body, glass, trim, glow, plate; public List<(Mesh m, Color c)> lamps = new List<(Mesh, Color)>(); public bool metal; }
    static Mesh tyreMesh, rimMesh, hubMesh;   // één wiel (rechts, as langs x, hart in de oorsprong), gedeeld door alle auto's
    static readonly Dictionary<string, Parts> cache = new Dictionary<string, Parts>();

    public static void ResetStatics() { cache.Clear(); tyreMesh = rimMesh = hubMesh = null; }

    public static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    // een auto als kind van parent (oorsprong op het wegdek, vooruit = +z); type en kleur willekeurig volgens de kansen van de webversie
    public static string Build(Transform parent)
    {
        float r = Random.value, acc = 0f; int t = 0; for (; t < Types.Length - 1; t++) { acc += Weight[t]; if (r < acc) break; }
        string type = Types[t];
        Color paint = type == "taxi" ? Hex("#f2c230") : RandomPaint();
        Build(parent, type, paint); return type;
    }
    public static Color RandomPaint() => Hex(PaintHex[Random.Range(0, PaintHex.Length)]);
    public static void Build(Transform parent, string type, Color paint)
    {
        if (!cache.TryGetValue(type, out var P) || P.body == null) { P = Make(type); cache[type] = P; }   // ook als Unity de mesh na een vorige Play heeft opgeruimd
        Put(parent, P.body, Mats.Get("lp_" + ColorUtility.ToHtmlStringRGB(paint) + (P.metal ? "m" : ""), paint));
        Put(parent, P.glass, Mats.Get("lp_glas", Hex("#1d2b36")));
        Put(parent, P.trim, Mats.Get("lp_donker", Hex("#25282c")));
        if (P.glow != null) Put(parent, P.glow, Mats.GetUnlit("lp_gloed", Hex("#ff2a3a")));
        foreach (var (m, c) in P.lamps) Put(parent, m, Mats.GetUnlit("lp_lamp" + ColorUtility.ToHtmlStringRGB(c), c));
        if (P.plate != null) Put(parent, P.plate, Mats.Get("lp_plaat", Hex("#e9e6d8")));
        // de wielen: per wiel een stuuras (draait om de hoogte-as) met daarin het wiel (draait om zijn as); links gespiegeld
        if (tyreMesh == null) MakeWheel();
        var cw = parent.gameObject.GetComponent<CarWheels>(); if (cw == null) cw = parent.gameObject.AddComponent<CarWheels>(); int n = 0;
        foreach (var (wx, wz) in new[] { (1.45f, 0.83f), (1.45f, -0.83f), (-1.45f, 0.83f), (-1.45f, -0.83f) })
        {
            var axle = new GameObject("as").transform; axle.SetParent(parent, false); axle.localPosition = U(wx, WR, wz); axle.gameObject.layer = parent.gameObject.layer;
            var wheel = new GameObject("wiel").transform; wheel.SetParent(axle, false); wheel.gameObject.layer = parent.gameObject.layer;
            Put(wheel, tyreMesh, Mats.Get("lp_band", Hex("#1b1d20"))); Put(wheel, rimMesh, Mats.Get("lp_velg", Hex("#a9afb5"))); Put(wheel, hubMesh, Mats.Get("lp_donker", Hex("#25282c")));
            cw.Set(n++, axle, wheel, wz < 0f, wx > 0f);
        }
        cw.Drive(0f, 0f, 0f);
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
    // wielkast: een donkere halve zeshoek strak om het wiel (straal WR + 10 cm), over de flank
    const float ArchR = WR + 0.1f;
    static void Arch(MeshBuilder mb, List<float[]> S, float wx, float top)
    {
        float h = ArchR * 0.866f;
        var l = new List<float[]>(); foreach (var (x, y1) in new[] { (wx - ArchR, WR), (wx - ArchR * 0.5f, WR + h), (wx + ArchR * 0.5f, WR + h), (wx + ArchR, WR) }) l.Add(Strip(At(S, x), x, 0.28f, y1, 0.018f));
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
    // Het wiel (rechts, buitenkant = +x, hart in de oorsprong): een band van twaalf vlakken met afgeschuinde schouders, een velg als
    // zeshoekige ring met zes spaken naar een zeshoekige naaf (zo zie je hem draaien), en daarachter de donkere binnenkant.
    const float WR = 0.36f, WW = 0.24f;
    static void MakeWheel()
    {
        MeshBuilder tyre = new MeshBuilder(), rim = new MeshBuilder(), hub = new MeshBuilder(); const int N = 12;
        Vector3 Pt(int n, float i, float r, float x) { float a = i * Mathf.PI * 2f / n; return new Vector3(x, Mathf.Sin(a) * r, Mathf.Cos(a) * r); }
        float h = WW / 2f, sh = 0.035f, rs = WR * 0.86f;   // schouder: de band loopt aan de zijkant schuin in naar rs
        for (int i = 0; i < N; i++)
        {
            Vector3 o0 = Pt(N, i, rs, -h), o1 = Pt(N, i + 1, rs, -h), a0 = Pt(N, i, WR, -h + sh), a1 = Pt(N, i + 1, WR, -h + sh), b0 = Pt(N, i, WR, h - sh), b1 = Pt(N, i + 1, WR, h - sh), p0 = Pt(N, i, rs, h), p1 = Pt(N, i + 1, rs, h);
            Vector3 m = Pt(N, i + 0.5f, 1f, 0f);
            tyre.Quad(a0, a1, b1, b0, m); tyre.Quad(o0, o1, a1, a0, m - Vector3.right); tyre.Quad(b0, b1, p1, p0, m + Vector3.right);
            tyre.Tri(new Vector3(-h, 0, 0), o0, o1, Vector3.left); tyre.Tri(new Vector3(h - 0.03f, 0, 0), p0, p1, Vector3.right);   // binnenkant en het donkere vlak achter de velg
        }
        float x = h + 0.006f, rO = WR * 0.72f, rI = WR * 0.6f, rH = WR * 0.2f;
        for (int i = 0; i < 6; i++)
        {
            Vector3 o0 = Pt(6, i, rO, x - 0.02f), o1 = Pt(6, i + 1, rO, x - 0.02f), i0 = Pt(6, i, rI, x), i1 = Pt(6, i + 1, rI, x);
            rim.Quad(o0, o1, i1, i0, Vector3.right);   // de ring, iets schuin naar buiten
            float a = i + 0.5f; Vector3 d = Pt(6, a, 1f, 0f), t = Vector3.Cross(Vector3.right, d).normalized * 0.035f;
            Vector3 s0 = Pt(6, a, rH, x + 0.01f), s1 = Pt(6, a, rI + 0.01f, x);
            rim.Quad(s0 - t, s1 - t, s1 + t, s0 + t, Vector3.right);   // spaak
            hub.Tri(new Vector3(x + 0.03f, 0, 0), Pt(6, i, rH, x + 0.015f), Pt(6, i + 1, rH, x + 0.015f), Vector3.right);   // naaf
        }
        tyreMesh = tyre.Build(); rimMesh = rim.Build(); hubMesh = hub.Build();
    }

    static List<float[]> L(params float[][] a) => new List<float[]>(a);
    static Parts Make(string type)
    {
        MeshBuilder body = new MeshBuilder(), glass = new MeshBuilder(), trim = new MeshBuilder(), glow = new MeshBuilder(), plate = new MeshBuilder();
        Color Amber = Hex("#ffb020");
        var lamps = new Dictionary<Color, MeshBuilder>();
        void Lamp(Color c, float sx, float sy, float sz, float x, float y, float z) { if (!lamps.TryGetValue(c, out var mb)) { mb = new MeshBuilder(); lamps[c] = mb; } Box(mb, sx, sy, sz, x, y, z); }
        // buitenspiegel: een blokje in de lak met een donkere achterkant, op de flank van de cabine net achter de voorruit
        void Mirror(List<float[]> C, float x)
        {
            var sc = At(C, x); foreach (int sg in new[] { -1, 1 }) { float z = sg * (sc[5] + 0.11f); Box(body, 0.12f, 0.1f, 0.16f, x, sc[2] + 0.1f, z); Box(trim, 0.02f, 0.08f, 0.13f, x - 0.065f, sc[2] + 0.1f, z); Box(trim, 0.06f, 0.03f, 0.08f, x, sc[2] + 0.06f, sg * (sc[5] + 0.03f)); }
        }
        // bumpers (donker, steken iets uit), kentekenplaten, knipperlichten op de voorhoeken en achterlichten die om de hoek lopen
        void Dress(List<float[]> B, float yF, float yR, float bumper = 0.42f)
        {
            float xf = B[B.Count - 1][0], xr = B[0][0], wf = At(B, xf - 0.05f)[4], wr = At(B, xr + 0.05f)[4];
            Box(trim, 0.08f, 0.13f, 2f * wf - 0.04f, xf + 0.01f, bumper, 0f); Box(trim, 0.08f, 0.13f, 2f * wr - 0.04f, xr - 0.01f, bumper, 0f);   // vlak met de carrosserie
            Box(plate, 0.02f, 0.1f, 0.38f, xf + 0.055f, bumper, 0f); Box(plate, 0.02f, 0.1f, 0.38f, xr - 0.055f, bumper, 0f);
            // lichtlijnen: wit over de hele neus net onder de kap, rood over de hele achterkant (strak, zoals de Cybertruck)
            var nf = B[B.Count - 1]; var nr = B[0];
            float yL = Mathf.Clamp(Mathf.Max(nf[2], yF), nf[1] + 0.08f, nf[3] - 0.035f), wL = Mathf.Min(nf[4], nf[6], WAt(nf, yL)) * 2f - 0.12f;
            Lamp(White, 0.02f, 0.045f, wL, xf + 0.008f, yL, 0f);
            float yR2 = Mathf.Clamp(Mathf.Max(nr[2], yR), nr[1] + 0.08f, nr[3] - 0.04f), wR2 = Mathf.Min(nr[4], nr[6], WAt(nr, yR2)) * 2f - 0.1f;
            Lamp(Red, 0.02f, 0.05f, wR2, xr - 0.008f, yR2, 0f);
            foreach (int sg in new[] { -1, 1 })
            {
                var sf = At(B, xf - 0.16f); float yf = Mathf.Clamp(yF, sf[1] + 0.03f, sf[3] - 0.03f); Lamp(Amber, 0.14f, 0.05f, 0.02f, xf - 0.16f, yf, sg * (WAt(sf, yf) + 0.012f));
                var sr = At(B, xr + 0.16f); float yr = Mathf.Clamp(yR, sr[1] + 0.03f, sr[3] - 0.03f); Lamp(Red, 0.2f, 0.07f, 0.02f, xr + 0.12f, yr, sg * (WAt(sr, yr) + 0.012f));
            }
        }
        // auto met een losse cabine: romp en cabine in lak, ruiten op de cabine, wielkasten op de romp
        void Car(List<float[]> B, List<float[]> C, float[] front, float[] rear, float[] side, float arch = 0.8f)
        {
            Hull(body, B); Hull(body, C);
            if (front != null) WinTop(glass, C, front[0], front[1], 0.07f);
            if (rear != null) WinTop(glass, C, rear[0], rear[1], 0.07f);
            if (side != null)   // twee zijruiten met een B-stijl ertussen
            {
                float mid = (side[0] + side[1]) * 0.5f; WinSide(glass, C, side[0], mid - 0.05f, 0.1f, 0.86f); WinSide(glass, C, mid + 0.05f, side[1], 0.1f, 0.86f);
                Band(trim, B, mid - 0.012f, mid + 0.012f, 0.4f, At(B, mid)[2] - 0.03f, 0.016f);   // deurnaad onder de B-stijl
                Band(trim, B, side[1] + 0.12f, side[1] + 0.144f, 0.4f, At(B, side[1] + 0.12f)[2] - 0.03f, 0.016f);   // voorkant van de voordeur
                Mirror(C, side[1]);
            }
            foreach (float wx in new[] { -1.45f, 1.45f }) Arch(trim, B, wx, arch);
        }
        bool metal = false;
        if (type == "sedan")   // hoekige sedan: lange motorkap, schuine ruiten, lichtlijn op de gordellijn
        {
            var B = L(new[] { -2.36f, 0.44f, 0.64f, 0.84f, 0.66f, 0.84f, 0.72f }, new[] { -2.15f, 0.3f, 0.64f, 0.95f, 0.76f, 0.92f, 0.8f }, new[] { 0.6f, 0.3f, 0.64f, 0.93f, 0.78f, 0.93f, 0.8f }, new[] { 2.2f, 0.32f, 0.6f, 0.76f, 0.74f, 0.9f, 0.72f }, new[] { 2.37f, 0.42f, 0.56f, 0.66f, 0.6f, 0.78f, 0.58f });
            var C = L(new[] { -1.62f, 0.92f, 0.92f, 0.95f, 0.79f, 0.79f, 0.77f }, new[] { -1.08f, 0.93f, 0.93f, 1.42f, 0.79f, 0.79f, 0.6f }, new[] { -0.05f, 0.93f, 0.93f, 1.42f, 0.79f, 0.79f, 0.6f }, new[] { 0.64f, 0.92f, 0.92f, 0.94f, 0.79f, 0.79f, 0.77f });
            Car(B, C, new[] { 0.6f, -0.02f }, new[] { -1.58f, -1.1f }, new[] { -1.45f, 0.45f });
            foreach (int sg in new[] { -1, 1 }) foreach (var (x0, x1) in new[] { (-2.2f, -2.25f + 0.0f), (-0.66f, 0.66f), (2.24f, 2.2f) }) if (x1 - x0 > 0.1f) Box(glow, x1 - x0, 0.03f, 0.02f, (x0 + x1) * 0.5f, 0.64f, sg * 0.935f);   // lichtlijn op de gordellijn, tussen de wielkasten
            Band(trim, B, -2.2f, 2.25f, 0.3f, 0.35f);
            Dress(B, 0.6f, 0.74f);
        }
        else if (type == "coupe")   // lage wig: platte neus, steile achterkant, donkere streep over de flanken
        {
            var B = L(new[] { -2.36f, 0.42f, 0.62f, 0.86f, 0.7f, 0.88f, 0.76f }, new[] { -2.1f, 0.3f, 0.6f, 0.93f, 0.8f, 0.94f, 0.82f }, new[] { 0.6f, 0.3f, 0.58f, 0.88f, 0.82f, 0.95f, 0.8f }, new[] { 2.0f, 0.3f, 0.55f, 0.66f, 0.8f, 0.93f, 0.76f }, new[] { 2.37f, 0.38f, 0.5f, 0.56f, 0.68f, 0.86f, 0.64f });
            var C = L(new[] { -1.92f, 0.92f, 0.92f, 0.94f, 0.8f, 0.8f, 0.78f }, new[] { -1.2f, 0.91f, 0.91f, 1.28f, 0.8f, 0.8f, 0.6f }, new[] { -0.15f, 0.89f, 0.89f, 1.28f, 0.8f, 0.8f, 0.6f }, new[] { 0.8f, 0.87f, 0.87f, 0.89f, 0.8f, 0.8f, 0.78f });
            Car(B, C, new[] { 0.75f, -0.12f }, new[] { -1.88f, -1.22f }, new[] { -1.65f, 0.6f }, 0.74f);
            Band(trim, B, -2.0f, 1.7f, 0.44f, 0.52f); Band(trim, B, -2.3f, 2.3f, 0.3f, 0.36f);
            Dress(B, 0.5f, 0.7f, 0.4f);
        }
        else if (type == "suv")   // hoge SUV: brede kast, groot glasvlak, donkere onderkant en dakrails
        {
            var B = L(new[] { -2.33f, 0.38f, 0.74f, 1.1f, 0.84f, 0.95f, 0.88f }, new[] { -2.18f, 0.34f, 0.74f, 1.13f, 0.88f, 0.97f, 0.92f }, new[] { 1.0f, 0.34f, 0.74f, 1.11f, 0.88f, 0.97f, 0.92f }, new[] { 2.15f, 0.36f, 0.72f, 1.0f, 0.86f, 0.95f, 0.86f }, new[] { 2.37f, 0.48f, 0.7f, 0.9f, 0.78f, 0.9f, 0.76f });
            var C = L(new[] { -2.12f, 1.1f, 1.1f, 1.72f, 0.91f, 0.91f, 0.8f }, new[] { 0.52f, 1.1f, 1.1f, 1.7f, 0.91f, 0.91f, 0.8f }, new[] { 1.1f, 1.08f, 1.08f, 1.1f, 0.91f, 0.91f, 0.9f });
            Car(B, C, new[] { 1.02f, 0.56f }, null, new[] { -1.95f, 0.85f }, 0.92f);
            Hull(glass, L(new[] { -2.135f, 1.2f, 1.2f, 1.62f, 0.75f, 0.75f, 0.66f }, new[] { -2.12f, 1.2f, 1.2f, 1.62f, 0.75f, 0.75f, 0.66f }));   // achterruit
            Band(trim, B, -2.25f, 2.2f, 0.34f, 0.42f);
            foreach (int sg in new[] { -1, 1 }) Box(trim, 2.4f, 0.06f, 0.06f, -0.8f, 1.74f, sg * 0.66f);
            Box(trim, 0.05f, 0.2f, 0.9f, 2.36f, 0.78f, 0f);
            Dress(B, 0.9f, 0.95f, 0.46f);
        }
        else if (type == "stad" || type == "taxi")   // kleine hatchback (de taxi met een daklicht)
        {
            var B = L(new[] { -2.32f, 0.4f, 0.62f, 0.84f, 0.7f, 0.86f, 0.74f }, new[] { -2.15f, 0.3f, 0.62f, 0.92f, 0.76f, 0.88f, 0.78f }, new[] { 0.55f, 0.3f, 0.62f, 0.9f, 0.78f, 0.89f, 0.78f }, new[] { 2.1f, 0.3f, 0.6f, 0.8f, 0.76f, 0.87f, 0.74f }, new[] { 2.33f, 0.4f, 0.56f, 0.7f, 0.64f, 0.8f, 0.64f });
            var C = L(new[] { -1.58f, 0.9f, 0.9f, 0.93f, 0.79f, 0.79f, 0.77f }, new[] { -1.18f, 0.91f, 0.91f, 1.42f, 0.79f, 0.79f, 0.64f }, new[] { 0.12f, 0.9f, 0.9f, 1.42f, 0.79f, 0.79f, 0.64f }, new[] { 0.74f, 0.88f, 0.88f, 0.9f, 0.79f, 0.79f, 0.77f });
            Car(B, C, new[] { 0.7f, 0.16f }, new[] { -1.55f, -1.2f }, new[] { -1.4f, 0.5f });
            Band(trim, B, -2.25f, 2.2f, 0.3f, 0.35f);
            if (type == "taxi") Lamp(Hex("#ffe08a"), 0.3f, 0.14f, 0.6f, -0.5f, 1.5f, 0f);
            Dress(B, 0.62f, 0.68f);
        }
        else   // bestelbus
        {
            var B = L(new[] { -2.34f, 0.36f, 0.9f, 2.0f, 0.84f, 0.93f, 0.84f }, new[] { -2.2f, 0.3f, 0.9f, 2.05f, 0.86f, 0.95f, 0.86f }, new[] { 0.95f, 0.3f, 0.9f, 2.05f, 0.86f, 0.95f, 0.86f }, new[] { 1.5f, 0.3f, 0.9f, 1.04f, 0.86f, 0.95f, 0.88f }, new[] { 2.2f, 0.3f, 0.82f, 0.93f, 0.84f, 0.93f, 0.84f }, new[] { 2.36f, 0.4f, 0.7f, 0.82f, 0.76f, 0.88f, 0.76f });
            Hull(body, B);
            WinTop(glass, B, 1.45f, 1.0f, 0.09f); WinSide(glass, B, 1.0f, 1.42f, 0.12f, 0.85f);
            foreach (float wx in new[] { -1.45f, 1.45f }) Arch(trim, B, wx, 0.82f);
            Band(trim, B, -2.25f, 2.25f, 0.3f, 0.35f);
            Dress(B, 0.66f, 0.75f, 0.42f); Mirror(B, 1.42f);
            Band(trim, B, -2.36f, 1.2f, 0.98f, 1.0f, 0.016f);   // naad van de laadruimte
            Box(trim, 0.012f, 0.95f, 0.02f, -2.345f, 1.4f, 0f);   // achterdeuren
        }
        var P = new Parts { body = body.Build(), glass = glass.Build(), trim = trim.Build(), glow = glow.v.Count > 0 ? glow.Build() : null, plate = plate.Build(), metal = metal };
        foreach (var kv in lamps) P.lamps.Add((kv.Value.Build(), kv.Key));
        return P;
    }
}

// De wielen van een auto: per wiel een stuuras en het wiel zelf. Drive rolt ze over de afgelegde afstand en zet de voorwielen in de
// stuurhoek (rustig bijdraaiend).
public class CarWheels : MonoBehaviour
{
    readonly Transform[] axle = new Transform[4], wheel = new Transform[4]; readonly bool[] left = new bool[4], front = new bool[4];
    float roll, steer;
    public void Set(int k, Transform a, Transform w, bool isLeft, bool isFront) { axle[k] = a; wheel[k] = w; left[k] = isLeft; front[k] = isFront; }
    // vering: het wiel op en neer ten opzichte van zijn plek in het model (m, omhoog positief)
    readonly float[] baseY = new float[4]; bool hasBase;
    public float BaseY(int k) { Base(); return baseY[k]; }
    public void Travel(int k, float dy) { if (axle[k] == null) return; Base(); var p = axle[k].localPosition; p.y = baseY[k] + dy; axle[k].localPosition = p; }
    void Base() { if (hasBase) return; hasBase = true; for (int k = 0; k < 4; k++) if (axle[k] != null) baseY[k] = axle[k].localPosition.y; }
    public void Drive(float dist, float steerDeg, float dt)
    {
        roll = Mathf.Repeat(roll + dist / 0.36f * Mathf.Rad2Deg, 360f); steer = dt <= 0f ? steerDeg : Mathf.MoveTowards(steer, steerDeg, 160f * dt);
        for (int k = 0; k < 4; k++)
        {
            if (axle[k] == null) continue;
            axle[k].localRotation = Quaternion.Euler(0f, (front[k] ? steer : 0f) + (left[k] ? 180f : 0f), 0f);
            wheel[k].localRotation = Quaternion.Euler(left[k] ? -roll : roll, 0f, 0f);
        }
    }
}
