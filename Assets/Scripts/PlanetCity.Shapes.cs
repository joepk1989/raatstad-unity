using System.Collections.Generic;
using UnityEngine;

// Bouwstenen voor de meshes: Frame (assenstelsel van een vak), muren, daken, kegels, molenwieken, stroken.
public static partial class PlanetCity
{
    // ---------- blokken en wegen per deel ----------
    class Frame
    {
        public Vector3 o, e, u, n; public Vector2[] poly; public float rad;
        // weg- en padvakken: per aangesloten buur de halve vector ernaartoe (de rand) en het halve hoogteverschil; null = vlak
        Vector2[] sq; float[] sd;
        // bolstand (wegknopen): elk punt wordt op de bol gelegd in plaats van in het raakvlak, zodat het precies aansluit op de linten van de wegen
        bool sphere; Vector3 cdir; float cg;
        public Frame(Frame b, Vector3 o2, Vector2[] p2, float r2) { u = b.u; e = b.e; n = b.n; o = o2; poly = p2; rad = r2; }
        public Frame(int i, bool onSphere = false, bool under = false)
        {
            var c = Planet.Cells[i]; u = c.dir; e = c.east; n = c.north; o = c.dir * (Planet.R + G(i)); poly = c.local; rad = c.radius;
            sphere = onSphere; cdir = c.dir; cg = G(i);
            if (!IsWalk(i)) return;
            var q = new List<Vector2>(); var d = new List<float>(); bool any = false; float g = G(i);
            if (under && Low.TryGetValue(i, out var lo)) { g = cg = lo; o = c.dir * (Planet.R + g); }   // het pad onder de brug
            foreach (int j in Partners(i))
            {
                if (!IsWalk(j) || UnderLink(i, j) != under) continue;   // de weg op de brug en het pad eronder hellen elk naar hun eigen buren
                Vector3 w = (Planet.Cells[j].dir - c.dir) * Planet.R; float gj = LevelFor(j, i);
                q.Add(new Vector2(Vector3.Dot(w, e), Vector3.Dot(w, n)) * 0.5f); d.Add((gj - g) * 0.5f); if (Mathf.Abs(gj - g) > 0.002f) any = true;
            }
            if (any) { sq = q.ToArray(); sd = d.ToArray(); }
        }
        // Hoogte boven het hart van het vak op (x, z): langs elke arm lineair van het hart naar de rand, waar de weg op de
        // gemiddelde hoogte van de twee vakken ligt. Tussen armen in mengt het naar de arm waar het punt het meest in ligt.
        public Frame Flat() { sq = null; sd = null; return this; }
        // een tunnel door een berg (PlanetTunnels.cs): het vlak een stuk hoger (dy) en in een rechte helling langs e (kx per meter)
        Vector2 incl;
        public Frame Lift(float dy) { cg += dy; o += u * dy; return this; }
        public Frame Incline(float kx) { sq = null; sd = null; incl = new Vector2(kx, 0f); return this; }
        public Frame At(Vector3 dir) { if (dir == cdir) return this; cdir = dir; o = dir * (Planet.R + cg); sq = null; sd = null; return this; }   // het hart van de knoop op de rand (NodeDir), vlak   // zonder de helling naar de buren (die rekent de padknoop zelf)
        public bool Sloped => sq != null;   // helt mee met een buur: vlakken in kleine stukjes, zodat ze de gebogen hoogte volgen
        public float H(float x, float z)
        {
            if (incl != Vector2.zero) return incl.x * x + incl.y * z;
            if (sq == null) return 0f; Vector2 p = new Vector2(x, z); float pl = p.magnitude; if (pl < 1e-3f) return 0f;
            float h = 0f, ws = 0f;
            for (int k = 0; k < sq.Length; k++)
            {
                float L = sq[k].magnitude, cs = Vector2.Dot(p, sq[k]) / (pl * L); if (cs <= 0f) continue;
                float w = cs * cs; w *= w; w *= w;   // cos^8
                if (pl > L) { w *= w; w *= w; }   // voorbij de rand (een lange spitse kruising): cos^32, zodat de schuine arm ernaast (30°) niet meetelt
                h += w * Mathf.Max(0f, Vector2.Dot(p, sq[k]) / (L * L)) * sd[k]; ws += w;   // voorbij de rand helt het door (een spitse kruising loopt tot 22 m door, AcuteTrim): anders vlak terwijl de weg verder stijgt
            }
            return ws > 1e-9f ? h / ws : 0f;
        }
        public Vector3 P(float x, float y, float z) => sphere ? (cdir * Planet.R + e * x + n * z).normalized * (Planet.R + cg + y + H(x, z)) : o + e * x + u * (y + H(x, z)) + n * z;
        public Vector3 D(float x, float y, float z) => e * x + u * y + n * z;
    }

    static Vector2 C(Frame f, int i, float k) => f.poly[i % f.poly.Length] * k;
    static void Wall(MeshBuilder m, Frame f, float k, float y0, float y1)
    {
        for (int i = 0; i < f.poly.Length; i++) { Vector2 a = C(f, i, k), b = C(f, i + 1, k), mid = (a + b) * 0.5f; m.Quad(f.P(a.x, y0, a.y), f.P(b.x, y0, b.y), f.P(b.x, y1, b.y), f.P(a.x, y1, a.y), f.D(mid.x, 0, mid.y)); }
    }
    static void Top(MeshBuilder m, Frame f, float k, float y)
    {
        for (int i = 0; i < f.poly.Length; i++) { Vector2 a = C(f, i, k), b = C(f, i + 1, k); m.Tri(f.P(0, y, 0), f.P(a.x, y, a.y), f.P(b.x, y, b.y), f.u); }
    }
    static void Windows(MeshBuilder m, Frame f, float k, float y0, float y1, float f0, float f1)
    {
        for (int i = 0; i < f.poly.Length; i++)
        {
            Vector2 a = C(f, i, k), b = C(f, i + 1, k), outw = ((a + b) * 0.5f).normalized;
            Vector2 p0 = Vector2.Lerp(a, b, f0) + outw * 0.04f, p1 = Vector2.Lerp(a, b, f1) + outw * 0.04f;
            m.Quad(f.P(p0.x, y0, p0.y), f.P(p1.x, y0, p1.y), f.P(p1.x, y1, p1.y), f.P(p0.x, y1, p0.y), f.D(outw.x, 0, outw.y));
        }
    }
    static void Cone(MeshBuilder m, Frame f, float cx, float cz, float r, float y0, float y1, float rTop)
    {
        for (int i = 0; i < 6; i++)
        {
            float a0 = i * Mathf.PI / 3f, a1 = (i + 1) * Mathf.PI / 3f, am = (a0 + a1) * 0.5f;
            Vector3 b0 = f.P(cx + Mathf.Cos(a0) * r, y0, cz + Mathf.Sin(a0) * r), b1 = f.P(cx + Mathf.Cos(a1) * r, y0, cz + Mathf.Sin(a1) * r);
            Vector3 outw = f.D(Mathf.Cos(am), (r - rTop) / Mathf.Max(0.01f, y1 - y0), Mathf.Sin(am));
            if (rTop <= 0.001f) m.Tri(b0, b1, f.P(cx, y1, cz), outw);
            else m.Quad(b0, b1, f.P(cx + Mathf.Cos(a1) * rTop, y1, cz + Mathf.Sin(a1) * rTop), f.P(cx + Mathf.Cos(a0) * rTop, y1, cz + Mathf.Sin(a0) * rTop), outw);
        }
    }
    static void Roof(MeshBuilder m, Frame f, float k, float y1)
    {
        Vector3 apex = f.P(0, y1 + 2.4f, 0);
        for (int i = 0; i < f.poly.Length; i++) { Vector2 a = C(f, i, k), b = C(f, i + 1, k), mid = (a + b) * 0.5f; m.Tri(f.P(a.x, y1, a.y), f.P(b.x, y1, b.y), apex, f.D(mid.x, 0.6f * mid.magnitude, mid.y)); }
    }
    static void Cap6(MeshBuilder m, Frame f, float r, float y)
    {
        for (int i = 0; i < 6; i++) { float a0 = i * Mathf.PI / 3f, a1 = (i + 1) * Mathf.PI / 3f; m.Tri(f.P(0, y, 0), f.P(Mathf.Cos(a0) * r, y, Mathf.Sin(a0) * r), f.P(Mathf.Cos(a1) * r, y, Mathf.Sin(a1) * r), f.u); }
    }
    static void Disc(MeshBuilder m, Frame f, float r, float y)
    {
        int nr = f.Sloped ? Mathf.Max(1, Mathf.CeilToInt(r / 0.8f)) : 1, ns = f.Sloped ? 24 : 12;
        System.Func<int, int, Vector3> Pt = (k, i) => { float rr = r * k / nr, a = i * Mathf.PI * 2f / ns; return f.P(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr); };
        for (int i = 0; i < ns; i++)
        {
            m.Tri(f.P(0, y, 0), Pt(1, i), Pt(1, i + 1), f.u);
            for (int k = 1; k < nr; k++) m.Quad(Pt(k, i), Pt(k + 1, i), Pt(k + 1, i + 1), Pt(k, i + 1), f.u);
        }
    }
    static void Skirt(MeshBuilder m, Frame f, float r, float y0, float y1)
    {
        for (int i = 0; i < 12; i++) { float a0 = i * Mathf.PI / 6f, a1 = (i + 1) * Mathf.PI / 6f, am = (a0 + a1) * 0.5f; m.Quad(f.P(Mathf.Cos(a0) * r, y0, Mathf.Sin(a0) * r), f.P(Mathf.Cos(a1) * r, y0, Mathf.Sin(a1) * r), f.P(Mathf.Cos(a1) * r, y1, Mathf.Sin(a1) * r), f.P(Mathf.Cos(a0) * r, y1, Mathf.Sin(a0) * r), f.D(Mathf.Cos(am), 0, Mathf.Sin(am))); }
    }
    // een balkje op (cx, cz), lang langs richting ang (halve lengte hl, halve breedte hw), van y0 tot y1
    static void Box(MeshBuilder m, Frame f, float cx, float cz, float ang, float hl, float hw, float y0, float y1)
    {
        Vector2 c = new Vector2(cx, cz), d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * hl, r = new Vector2(-Mathf.Sin(ang), Mathf.Cos(ang)) * hw;
        Vector2[] p = { c - d - r, c + d - r, c + d + r, c - d + r };
        for (int i = 0; i < 4; i++) { Vector2 a = p[i], b = p[(i + 1) % 4], o = (a + b) * 0.5f - c; m.Quad(f.P(a.x, y0, a.y), f.P(b.x, y0, b.y), f.P(b.x, y1, b.y), f.P(a.x, y1, a.y), f.D(o.x, 0, o.y)); }
        m.Quad(f.P(p[0].x, y1, p[0].y), f.P(p[1].x, y1, p[1].y), f.P(p[2].x, y1, p[2].y), f.P(p[3].x, y1, p[3].y), f.u);
    }
    // spitse torenspits op de vakvorm (schaal k), van y tot y + hgt
    static void Spire(MeshBuilder m, Frame f, float k, float y, float hgt)
    {
        Vector3 apex = f.P(0, y + hgt, 0);
        for (int i = 0; i < f.poly.Length; i++) { Vector2 a = C(f, i, k), b = C(f, i + 1, k), mid = (a + b) * 0.5f; m.Tri(f.P(a.x, y, a.y), f.P(b.x, y, b.y), apex, f.D(mid.x, 0.3f * mid.magnitude, mid.y)); }
    }
    // gestreepte zonneluifel boven een etalage: schuin naar buiten en omlaag, aan beide kanten zichtbaar
    static void Awning(System.Func<string, MeshBuilder> M, Frame f, float k, float y, string stripe)
    {
        for (int i = 0; i < f.poly.Length; i++)
        {
            Vector2 a = C(f, i, k), b = C(f, i + 1, k), outw = ((a + b) * 0.5f).normalized; const int S = 6;
            for (int s = 0; s < S; s++)
            {
                Vector2 p0 = Vector2.Lerp(a, b, 0.1f + 0.8f * s / S), p1 = Vector2.Lerp(a, b, 0.1f + 0.8f * (s + 1) / S), q0 = p0 + outw * 1.1f, q1 = p1 + outw * 1.1f;
                var mb = M(s % 2 == 0 ? stripe : "doek");
                Vector3 A = f.P(p0.x, y, p0.y), B = f.P(p1.x, y, p1.y), Cc = f.P(q1.x, y - 0.45f, q1.y), D = f.P(q0.x, y - 0.45f, q0.y);
                mb.Quad(A, B, Cc, D, f.D(outw.x * 0.4f, 1f, outw.y * 0.4f)); mb.Quad(A, B, Cc, D, f.D(-outw.x * 0.4f, -1f, -outw.y * 0.4f));
            }
        }
    }

    // Molenwieken: vier armen met een zeil, in het xy-vlak rond de as (z = naar voren). Eén gedeelde mesh voor alle molens.
    static Mesh wiekMesh, zeilMesh;
    const float WiekL = 6.3f;
    static void WiekMeshes()
    {
        if (wiekMesh != null && zeilMesh != null) return;
        var w = new MeshBuilder(); var z = new MeshBuilder();
        for (int k = 0; k < 4; k++)
        {
            float t = k * Mathf.PI / 2f; Vector3 u = new Vector3(Mathf.Cos(t), Mathf.Sin(t), 0f), p = new Vector3(-u.y, u.x, 0f);
            Vector3 a = -p * 0.09f, b = p * 0.09f, c = u * WiekL + p * 0.09f, d = u * WiekL - p * 0.09f, fz = new Vector3(0, 0, 0.18f);
            w.Quad(a + fz, b + fz, c + fz, d + fz, Vector3.forward); w.Quad(a + fz, b + fz, c + fz, d + fz, Vector3.back);
            w.Quad(a, d, d + fz, a + fz, -p); w.Quad(b, c, c + fz, b + fz, p);
            Vector3 s0 = u * 1.1f + p * 0.12f, s1 = u * WiekL + p * 0.12f, s2 = u * WiekL + p * 1.45f, s3 = u * 1.1f + p * 1.45f, zz = new Vector3(0, 0, 0.1f);
            z.Quad(s0 + zz, s1 + zz, s2 + zz, s3 + zz, Vector3.forward); z.Quad(s0 + zz, s1 + zz, s2 + zz, s3 + zz, Vector3.back);
            for (float r = 1.9f; r < WiekL; r += 1.1f) { Vector3 l0 = u * r + p * 0.1f, l1 = u * r + p * 1.47f, lw = u * 0.06f, lz = new Vector3(0, 0, 0.14f); w.Quad(l0 - lw + lz, l0 + lw + lz, l1 + lw + lz, l1 - lw + lz, Vector3.forward); w.Quad(l0 - lw + lz, l0 + lw + lz, l1 + lw + lz, l1 - lw + lz, Vector3.back); }   // dwarslatten
        }
        for (int k = 0; k < 6; k++) { float a0 = k * Mathf.PI / 3f, a1 = (k + 1) * Mathf.PI / 3f; Vector3 p0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0) * 0.45f, p1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0) * 0.45f, hz = new Vector3(0, 0, 0.35f); w.Tri(hz, p0 + hz, p1 + hz, Vector3.forward); w.Quad(p0, p1, p1 + hz, p0 + hz, (p0 + p1)); }   // naaf
        wiekMesh = w.Build(); zeilMesh = z.Build();
    }
    static void MakeWieken(Transform parent, Vector3 pos, Quaternion rot, int hash)
    {
        WiekMeshes();
        var pivot = new GameObject("Wieken"); pivot.transform.SetParent(parent, false); pivot.transform.SetPositionAndRotation(pos, rot);
        pivot.transform.Rotate(0f, 0f, (hash & 255) / 255f * 90f, Space.Self);
        var sp = pivot.AddComponent<Wieken>(); sp.Speed = 38f + ((hash >> 8) & 15);
        foreach (var (mesh, key) in new[] { (wiekMesh, "wiek"), (zeilMesh, "zeil") })
        {
            var c = new GameObject(key); c.transform.SetParent(pivot.transform, false); c.layer = Layers.Gebouw;
            c.AddComponent<MeshFilter>().sharedMesh = mesh; c.AddComponent<MeshRenderer>().sharedMaterial = Mats.Get("p_" + key, ColorOf(key));
        }
    }

    // Op een hellend vak is de hoogte tussen de armen gebogen (Frame.H): dan wordt een vlak een raster van stukjes van ~0,8 m,
    // anders steekt een groot vlak (het zand van een fietspad) door een smaller vlak dat er 1 cm boven hoort (het rode fietsdeel)
    static void Strip(MeshBuilder m, Frame f, Vector2 d, Vector2 r, float s0, float s1, float hw, float y)
    {
        int nu = f.Sloped ? Mathf.Max(1, Mathf.CeilToInt((s1 - s0) / 0.8f)) : 1, nv = f.Sloped ? Mathf.Max(1, Mathf.CeilToInt(2f * hw / 0.8f)) : 1;
        for (int u = 0; u < nu; u++) for (int v = 0; v < nv; v++)
        {
            float sa = Mathf.Lerp(s0, s1, u / (float)nu), sb = Mathf.Lerp(s0, s1, (u + 1) / (float)nu), la = Mathf.Lerp(-hw, hw, v / (float)nv), lb = Mathf.Lerp(-hw, hw, (v + 1) / (float)nv);
            Vector2 a = d * sa + r * la, b = d * sa + r * lb, c = d * sb + r * lb, e = d * sb + r * la;
            m.Quad(f.P(a.x, y, a.y), f.P(b.x, y, b.y), f.P(c.x, y, c.y), f.P(e.x, y, e.y), f.u);
        }
    }
    static void StripSides(MeshBuilder m, Frame f, Vector2 d, Vector2 r, float s0, float s1, float hw, float y0, float y1)
    {
        int nu = f.Sloped ? Mathf.Max(1, Mathf.CeilToInt((s1 - s0) / 0.8f)) : 1;
        for (int sg = -1; sg <= 1; sg += 2) for (int u = 0; u < nu; u++)
        {
            Vector2 a = d * Mathf.Lerp(s0, s1, u / (float)nu) + r * (hw * sg), b = d * Mathf.Lerp(s0, s1, (u + 1) / (float)nu) + r * (hw * sg);
            m.Quad(f.P(a.x, y0, a.y), f.P(b.x, y0, b.y), f.P(b.x, y1, b.y), f.P(a.x, y1, a.y), f.D(r.x * sg, 0, r.y * sg));
        }
    }

    // Een wegvak: één nette omtrek per laag (stoep, asfalt) in plaats van losse schijven en stroken. De omtrek wordt
    // rondom bemonsterd: in elke richting de verste rand van de armen naar de buurwegen, met een kern die bochten
    // afrondt. Daarop: stoeprand, middenstreepjes op rechte armen, een eilandje op kruisingen en een lantaarn.
    // De richting van de weg door een buurvak dat zelf een "schuin recht stuk" is (twee aansluitingen die 120° of 180° maken),
    // uitgedrukt in het assenstelsel van dit vak en wijzend dit vak in. Geeft false als de buur gewoon via zijn hart gaat.
    // Wereldrichting van de weg door een schuin recht wegvak (zie ChordDirOf); false als het vak via zijn hart gaat
    public static bool ChordDirWorld(int j, out Vector3 dir)
    {
        dir = Vector3.zero; var arms = new List<Vector3>();
        foreach (int k in Partners(j)) if (IsRoad(k)) arms.Add(((Planet.Cells[j].dir + Planet.Cells[k].dir) * 0.5f).normalized);
        if (arms.Count != 2) return false;
        Vector3 cj = Planet.Cells[j].dir; if (Vector3.Dot((arms[0] - cj).normalized, (arms[1] - cj).normalized) >= -0.3f) return false;
        dir = (arms[1] - arms[0]).normalized; return true;
    }

    static bool ChordDirOf(int j, Frame fr, Vector2 e, out Vector2 dir)
    {
        dir = (-e).normalized; var arms = new List<Vector3>();
        foreach (int k in Partners(j)) if (IsRoad(k)) arms.Add(((Planet.Cells[j].dir + Planet.Cells[k].dir) * 0.5f).normalized * Planet.R);
        if (arms.Count != 2) return false;
        Vector3 cj = Planet.Cells[j].dir * Planet.R; Vector3 a0 = arms[0] - cj, a1 = arms[1] - cj;
        if (Vector3.Dot(a0.normalized, a1.normalized) >= -0.3f) return false;
        Vector3 w = arms[1] - arms[0]; Vector2 d = new Vector2(Vector3.Dot(w, fr.e), Vector3.Dot(w, fr.n)).normalized;
        if (Vector2.Dot(d, -e) < 0f) d = -d; dir = d; return true;
    }
}
