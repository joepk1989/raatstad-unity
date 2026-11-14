using System;
using System.Collections.Generic;
using UnityEngine;

// Een module v2 als mesh, zoals mv2Build in de webversie: vloeren als driehoeken (met trapgaten), muren met deur, raam,
// poort en open doorgang, hoekposten, trappen (recht, bordes, wenteltrap, helling, roltrap, tribune, schans), daken en
// voorwerpen. Eén mesh met een submesh per materiaal, in de lokale ruimte van de module: x = x van de webversie,
// y omhoog, z = −z van de webversie (Unity is linkshandig; zo ziet een module er precies zo uit als in de webversie).
// Per module, versie, spiegeling en dekking (wat er bovenop staat) één keer gebouwd en daarna hergebruikt.
public static class ModuleMesh
{
    // ---------- cache ----------
    public class Built { public Mesh mesh; public Material[] mats; public string id; public int ver; }
    static readonly Dictionary<string, Built> cache = new Dictionary<string, Built>();
    // stacked: de module staat op een andere (begane grond krijgt een plafond eronder); cover: bit per vak (MV2.AreaCells) waar op het dak iets staat
    public static Built Get(ModuleV2 m, bool stacked, int cover)
    {
        string key = m.id + "|" + m.ver + "|" + (m.mir ? 1 : 0) + "|" + (stacked ? 1 : 0) + "|" + cover;
        if (cache.TryGetValue(key, out var b) && b.mesh != null) return b;
        var stale = new List<string>(); foreach (var kv in cache) if (kv.Value.id == m.id && kv.Value.ver != m.ver) stale.Add(kv.Key);
        foreach (var s in stale) { if (cache[s].mesh != null) UnityEngine.Object.Destroy(cache[s].mesh); cache.Remove(s); }
        b = Build(m, stacked, cover); b.id = m.id; b.ver = m.ver; cache[key] = b; return b;
    }
    // parts: wat er getekend wordt (1 vloeren, 2 muren, 4 trappen, 8 daken, 16 voorwerpen), voor de voorbeelden in de Raatmaker
    public const int Floors = 1, WallsP = 2, Stairs = 4, Roofs = 8, Objs = 16, All = 31;
    public static Built Build(ModuleV2 m, bool stacked = false, int cover = 0, int parts = All)
    {
        var bd = new Bld(m, stacked, cover) { parts = parts }; try { bd.Run(); } catch (Exception e) { Debug.LogWarning("Module " + m.name + ": " + e.Message + "\n" + e.StackTrace); }
        return bd.Finish();
    }

    // ---------- materialen ----------
    static readonly HashSet<string> Clear = new HashSet<string> { "facade", "f_glass", "w_glass", "pane", "water", "f_water", "net" };
    public static Color ColOf(string key)
    {
        if (key.StartsWith("f_") || key.StartsWith("w_"))
        {
            string k = key.Substring(2); var c = MV2.Col(k);
            if (k == "glass") c.a = key[0] == 'f' ? 0.45f : 0.4f; if (k == "curtain") c = MV2.Hex("#8aaec8"); if (k == "water") c = new Color(0.24f, 0.5f, 0.62f, 0.85f);
            return c;
        }
        if (key.StartsWith("c_")) return MV2.Hex("#" + key.Substring(2));
        switch (key)
        {
            case "ceiling": return MV2.Hex("#e9e6df"); case "slab": return MV2.Hex("#bcbcb6"); case "frame": return MV2.Hex("#c9ced3"); case "trim": return MV2.Hex("#4a4f55");
            case "roof": return MV2.Hex("#8c4b3a"); case "step": return MV2.Hex("#b7b1a5"); case "ramptop": return MV2.Hex("#a7a298"); case "trunk": return MV2.Hex("#6b4a2e");
            case "leaf0": return MV2.Hex("#4f8a3c"); case "leaf1": return MV2.Hex("#5f9a48"); case "leaf2": return MV2.Hex("#3f7a34"); case "bench": return MV2.Hex("#9b6a3c");
            case "lamp": return MV2.Hex("#2b3138"); case "glow": return MV2.Hex("#ffe6a8"); case "water": return new Color(0.24f, 0.5f, 0.62f, 0.85f); case "bin": return MV2.Hex("#3f5f4a");
            case "metal": return MV2.Hex("#9aa1a6"); case "lijst": return MV2.Hex("#e3ddd0"); case "plint": return MV2.Hex("#4f4a45"); case "lit": return LitDay; case "facade": return new Color(0.5f, 0.66f, 0.78f, 0.72f); case "spandrel": return MV2.Hex("#2b3a46"); case "pane": return new Color(0.66f, 0.83f, 0.92f, 0.38f); case "net": return new Color(0.85f, 0.86f, 0.84f, 0.45f);
        }
        return new Color(0.7f, 0.7f, 0.68f);
    }
    // glas glad en een beetje metaalachtig: de zon glanst erin en de lucht spiegelt
    static readonly Dictionary<string, (float smooth, float metal)> Shine = new Dictionary<string, (float, float)> { ["w_curtain"] = (0.92f, 0.35f), ["facade"] = (0.94f, 0.3f), ["w_glass"] = (0.95f, 0.1f), ["f_glass"] = (0.95f, 0.1f), ["pane"] = (0.95f, 0.1f), ["frame"] = (0.6f, 0.6f), ["spandrel"] = (0.7f, 0.2f) };
    static readonly HashSet<Material> shone = new HashSet<Material>(), textured = new HashSet<Material>();
    // verlichte ruiten: overdag gewoon glas, 's avonds warm licht (ModuleLights, ook bij donker weer)
    public static readonly Color LitDay = new Color(0.4f, 0.53f, 0.63f), LitNight = new Color(1f, 0.84f, 0.55f);
    public static Material Mat(string key)
    {
        if (key == "glow" || key == "lit") return Mats.GetUnlit("mod_" + key, ColOf(key));
        var mt = Mats.Get("mod_" + key, ColOf(key));
        if (textured.Add(mt)) ModuleTex.Apply(key, mt);   // getekende textuur op ware schaal (baksteen, stucwerk, klinkers …)
        if (Shine.TryGetValue(key, out var sh) && shone.Add(mt)) { if (mt.HasProperty("_Smoothness")) mt.SetFloat("_Smoothness", sh.smooth); if (mt.HasProperty("_Metallic")) mt.SetFloat("_Metallic", sh.metal); }
        return mt;
    }

    // ---------- de bouwer ----------
    class Bld
    {
        readonly ModuleV2 m; readonly bool stacked; readonly int cover; readonly float ts; public int parts = All;
        readonly Dictionary<string, MeshBuilder> mb = new Dictionary<string, MeshBuilder>(); readonly List<string> order = new List<string>();
        const float LH = MV2.LV_H, T = MV2.WALL_T;
        public Bld(ModuleV2 m, bool stacked, int cover) { this.m = m; this.stacked = stacked; this.cover = cover; ts = MV2.Ts(m); }
        MeshBuilder M(string k) { if (!mb.TryGetValue(k, out var b)) { b = new MeshBuilder(); mb[k] = b; order.Add(k); } return b; }
        static Vector3 U(float x, float y, float z) => new Vector3(x, y, -z);   // webversie → Unity
        static Vector3 U(Vector2 p, float y) => new Vector3(p.x, y, -p.y);

        public Built Finish()
        {
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, name = "Module " + m.name };
            var v = new List<Vector3>(); var n = new List<Vector3>(); var keys = new List<string>();
            foreach (var k in order) { if (mb[k].v.Count == 0) continue; keys.Add(k); }
            // doorzichtig als laatste (glas, water): dan tekent de rest eerst
            keys.Sort((a, b) => (Clear.Contains(a) ? 1 : 0).CompareTo(Clear.Contains(b) ? 1 : 0));
            var subs = new List<int[]>();
            foreach (var k in keys) { var b = mb[k]; int o = v.Count; v.AddRange(b.v); n.AddRange(b.n); var t = new int[b.t.Count]; for (int i = 0; i < t.Length; i++) t[i] = b.t[i] + o; subs.Add(t); }
            // texturecoördinaten in meters: vlakke vlakken van boven (x, z), de rest langs het vlak (horizontaal, dan omhoog of langs
            // de helling), zodat een baksteen- of klinkerpatroon doorloopt over muren, verdiepingen en buurvlakken (ModuleTex.cs)
            var uv = new List<Vector2>(v.Count);
            for (int i = 0; i < v.Count; i++)
            {
                Vector3 p = v[i], nn = n[i];
                if (Mathf.Abs(nn.y) > 0.95f) { uv.Add(new Vector2(p.x, p.z)); continue; }
                Vector3 t = Vector3.Cross(Vector3.up, nn).normalized, bt = Vector3.Cross(nn, t); uv.Add(new Vector2(Vector3.Dot(p, t), Vector3.Dot(p, bt)));
            }
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.subMeshCount = subs.Count; for (int i = 0; i < subs.Count; i++) mesh.SetTriangles(subs[i], i, false); mesh.RecalculateBounds();
            var mats = new Material[keys.Count]; for (int i = 0; i < keys.Count; i++) mats[i] = Mat(keys[i]);
            return new Built { mesh = mesh, mats = mats };
        }

        // ---- vormen (alles in webcoördinaten: (x, z) in het vlak, y omhoog) ----
        // bolle veelhoek (punten in het vlak) als prisma van y0 tot y1; top: ander materiaal voor het bovenvlak
        void Prism(string key, IList<Vector2> poly, float y0, float y1, string top = null)
        {
            int n = poly.Count; if (n < 3 || y1 - y0 < 1e-4f) return; Vector2 c = Vector2.zero; foreach (var p in poly) c += p; c /= n; var mt = M(top ?? key); var ms = M(key);
            for (int i = 1; i + 1 < n; i++) { mt.Tri(U(poly[0], y1), U(poly[i], y1), U(poly[i + 1], y1), Vector3.up); ms.Tri(U(poly[0], y0), U(poly[i], y0), U(poly[i + 1], y0), Vector3.down); }
            for (int i = 0; i < n; i++) { Vector2 a = poly[i], b = poly[(i + 1) % n], mid = (a + b) * 0.5f - c; ms.Quad(U(a, y0), U(b, y0), U(b, y1), U(a, y1), U(mid, 0f)); }
        }
        // muurblok tussen a en b, dik t, van y0 tot y1; off schuift hem dwars
        void Box(string key, Vector2 a, Vector2 b, float y0, float y1, float t, float off = 0f)
        {
            Vector2 d = b - a; float l = d.magnitude; if (l < 1e-5f) return; Vector2 nn = new Vector2(-d.y, d.x) / l, o = nn * off, h = nn * (t * 0.5f);
            Prism(key, new[] { a + o - h, b + o - h, b + o + h, a + o + h }, y0, y1);
        }
        // een bol lichaam uit punten (webcoördinaten) en vlakken; de voorkant wijst van het midden af
        void Solid(string key, IList<Vector3> P, IList<int[]> F)
        {
            Vector3 c = Vector3.zero; foreach (var p in P) c += p; c /= P.Count; var b = M(key);
            foreach (var f in F)
            {
                Vector3 fc = Vector3.zero; foreach (int i in f) fc += P[i]; fc /= f.Length; Vector3 outw = U(fc.x - c.x, fc.y - c.y, fc.z - c.z);
                for (int i = 1; i + 1 < f.Length; i++) b.Tri(U(P[f[0]].x, P[f[0]].y, P[f[0]].z), U(P[f[i]].x, P[f[i]].y, P[f[i]].z), U(P[f[i + 1]].x, P[f[i + 1]].y, P[f[i + 1]].z), outw);
            }
        }
        static readonly int[][] BoxF = { new[] { 0, 1, 3, 2 }, new[] { 4, 6, 7, 5 }, new[] { 0, 4, 5, 1 }, new[] { 2, 3, 7, 6 }, new[] { 0, 2, 6, 4 }, new[] { 1, 5, 7, 3 } };
        static List<Vector3> BoxP(float sx, float sy, float sz)
        {
            var P = new List<Vector3>(); for (int i = 0; i < 2; i++) for (int j = 0; j < 2; j++) for (int k = 0; k < 2; k++) P.Add(new Vector3((i - 0.5f) * sx, (j - 0.5f) * sy, (k - 0.5f) * sz)); return P;
        }
        // stang van p naar q (3D, webcoördinaten), doorsnede bw × bh
        void Bar(string key, Vector3 p, Vector3 q, float bw, float bh = -1f)
        {
            if (bh < 0f) bh = bw; Vector3 ax = q - p; float L = ax.magnitude; if (L < 0.01f) return; ax /= L;
            Vector3 s = Vector3.Cross(ax, Vector3.up); if (s.sqrMagnitude < 1e-6f) s = Vector3.right; s.Normalize(); Vector3 r = Vector3.Cross(s, ax).normalized;
            var P = new List<Vector3>(); foreach (var e in new[] { p, q }) for (int j = 0; j < 2; j++) for (int k = 0; k < 2; k++) P.Add(e + s * ((j - 0.5f) * bw) + r * ((k - 0.5f) * bh));
            Solid(key, P, new[] { new[] { 0, 1, 3, 2 }, new[] { 4, 6, 7, 5 }, new[] { 0, 4, 5, 1 }, new[] { 2, 3, 7, 6 }, new[] { 0, 2, 6, 4 }, new[] { 1, 5, 7, 3 } });
        }
        void Post(string key, Vector2 p, float y0, float y1, float r = 0.03f) => Cyl(key, p, r, r, y0, y1 - y0, 6);
        // kegelstomp (cilinder) met de voet op y0; n zijden (hoekpunt op +z, zoals CylinderGeometry)
        void Cyl(string key, Vector2 c, float rt, float rb, float y0, float h, int n, Func<Vector3, Vector3> xf = null)
        {
            var P = new List<Vector3>(); for (int k = 0; k < n; k++) { float a = k * Mathf.PI * 2f / n; P.Add(new Vector3(c.x + Mathf.Sin(a) * rb, y0, c.y + Mathf.Cos(a) * rb)); }
            for (int k = 0; k < n; k++) { float a = k * Mathf.PI * 2f / n; P.Add(new Vector3(c.x + Mathf.Sin(a) * rt, y0 + h, c.y + Mathf.Cos(a) * rt)); }
            if (xf != null) for (int i = 0; i < P.Count; i++) P[i] = xf(P[i]);
            var F = new List<int[]>(); var bot = new int[n]; var top = new int[n]; for (int k = 0; k < n; k++) { bot[k] = k; top[k] = n + k; F.Add(new[] { k, (k + 1) % n, n + (k + 1) % n, n + k }); }
            F.Add(bot); F.Add(top); Solid(key, P, F);
        }
        static readonly Vector3[] IcoP; static readonly int[][] IcoF;
        static Bld()
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            IcoP = new[] { new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0), new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t), new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1) };
            for (int i = 0; i < 12; i++) IcoP[i] = IcoP[i].normalized;
            IcoF = new[] { new[] { 0, 11, 5 }, new[] { 0, 5, 1 }, new[] { 0, 1, 7 }, new[] { 0, 7, 10 }, new[] { 0, 10, 11 }, new[] { 1, 5, 9 }, new[] { 5, 11, 4 }, new[] { 11, 10, 2 }, new[] { 10, 7, 6 }, new[] { 7, 1, 8 }, new[] { 3, 9, 4 }, new[] { 3, 4, 2 }, new[] { 3, 2, 6 }, new[] { 3, 6, 8 }, new[] { 3, 8, 9 }, new[] { 4, 9, 5 }, new[] { 2, 4, 11 }, new[] { 6, 2, 10 }, new[] { 8, 6, 7 }, new[] { 9, 8, 1 } };
        }
        void Ico(string key, Vector3 c, float r, Vector3 sc, Func<Vector3, Vector3> xf = null)
        {
            var P = new List<Vector3>(); foreach (var p in IcoP) { var q = c + Vector3.Scale(p * r, sc); P.Add(xf != null ? xf(q) : q); } Solid(key, P, IcoF);
        }
        // vlakke driehoeken op hoogte y, voorkant omhoog of omlaag
        void Flat(string key, List<Vector2[]> tris, float y, bool up)
        {
            var b = M(key); foreach (var v in tris) b.Tri(U(v[0], y), U(v[1], y), U(v[2], y), up ? Vector3.up : Vector3.down);
        }
        // vlak in beide richtingen zichtbaar
        void Both(string key, Vector3 a, Vector3 b, Vector3 c)
        {
            var mm = M(key); Vector3 A = U(a.x, a.y, a.z), B = U(b.x, b.y, b.z), C = U(c.x, c.y, c.z), nr = Vector3.Cross(B - A, C - A); mm.Tri(A, B, C, nr); mm.Tri(A, B, C, -nr);
        }

        public void Run()
        {
            int nLv = m.lv.Count, roofLi = nLv > 1 && MV2.RoofOnly(m) ? nLv - 1 : -1; var area = MV2.AreaCells(m.k);
            for (int li = 0; li < nLv; li++)
            {
                var L = m.lv[li]; float y0 = li * LH; bool lifted = li > 0 || stacked;
                // --- vloeren ---
                var holes = new List<Func<Vector2[], MV2.Hole>>(); if (li > 0) foreach (var S in m.lv[li - 1].s) holes.Add(MV2.StairHole(S, ts));
                var byMat = new Dictionary<string, List<Vector2[]>>(); var edgeCnt = new Dictionary<(Vector2Int, Vector2Int), int>();
                Vector2Int Q(Vector2 p) => new Vector2Int(Mathf.RoundToInt(p.x * 64f), Mathf.RoundToInt(p.y * 64f));
                (Vector2Int, Vector2Int) E(Vector2 p, Vector2 q) { var a = Q(p); var b = Q(q); return a.x < b.x || (a.x == b.x && a.y <= b.y) ? (a, b) : (b, a); }
                var longW = MV2.LongWalls(L, ts);
                foreach (var kv in L.f)
                {
                    var (i, j, t) = kv.Key; var v = MV2.TriVerts(i, j, t, ts); var cen = MV2.Centroid(v); string mat = kv.Value;
                    if (!MV2.InArea(m, cen.x, cen.y)) continue;
                    if (holes.Count > 0)
                    {
                        MV2.Hole hk = null; foreach (var h in holes) { hk = h(v); if (hk != null) break; }
                        if (hk != null)
                        {
                            if (hk.keep.HasValue)
                            {
                                var pl = MV2.ClipKeep(v, hk.keep.Value);
                                for (int q = 1; q + 1 < pl.Count; q++) if (MV2.TriArea2(pl[0], pl[q], pl[q + 1]) > 1e-4f) { if (!byMat.TryGetValue(mat, out var l2)) byMat[mat] = l2 = new List<Vector2[]>(); l2.Add(new[] { pl[0], pl[q], pl[q + 1] }); }
                            }
                            continue;
                        }
                    }
                    if (li == roofLi && Covered(cen, area)) continue;   // daar staat een module op: geen dakvloer
                    void AddTri(string mt, Vector2[] tv)
                    {
                        if (!byMat.TryGetValue(mt, out var lst)) byMat[mt] = lst = new List<Vector2[]>(); lst.Add(tv);
                        for (int e = 0; e < 3; e++) { var k2 = E(tv[e], tv[(e + 1) % 3]); edgeCnt.TryGetValue(k2, out int c0); edgeCnt[k2] = c0 + 1; }
                    }
                    // een lange muur (haaks op het raster) snijdt deze driehoek doormidden: elke helft krijgt de vloer van de buur aan zijn kant
                    // (geen buur: geen vloer), zodat een ruimte netjes tot aan de muur gevuld is en buiten leeg blijft
                    if (longW.Count > 0 && MV2.CrossedBy(longW, cen, ts, out var wa, out var wb))
                    {
                        int apex = -1; for (int e = 0; e < 3; e++) if (Mathf.Abs(MV2.SideOf(wa, wb, v[e])) < 0.5f) apex = e;
                        if (apex >= 0)
                        {
                            Vector2 V = v[apex], P1 = v[(apex + 1) % 3], P2 = v[(apex + 2) % 3], Mid = (P1 + P2) * 0.5f; string m1 = NbMat(L, V, P1, P2), m2 = NbMat(L, V, P2, P1);
                            if (m1 == null && m2 == null) { AddTri(mat, v); continue; }
                            if (m1 != null) AddTri(m1, new[] { V, P1, Mid }); if (m2 != null) AddTri(m2, new[] { V, Mid, P2 });
                            continue;
                        }
                    }
                    AddTri(mat, v);
                }
                if ((parts & ModuleMesh.Floors) == 0) byMat.Clear();
                foreach (var kv in byMat)
                {
                    float top = lifted ? y0 + 0.02f : (kv.Key == "water" ? -0.25f : 0.05f);
                    Flat("f_" + kv.Key, kv.Value, top, true);
                    if (lifted && kv.Key != "glass") Flat("ceiling", kv.Value, y0 - MV2.FLOOR_T, false);
                }
                if (lifted) foreach (var kv in byMat) foreach (var v in kv.Value) for (int e = 0; e < 3; e++) if (edgeCnt.TryGetValue(E(v[e], v[(e + 1) % 3]), out int c1) && c1 == 1) Box("slab", v[e], v[(e + 1) % 3], y0 - MV2.FLOOR_T, y0 + 0.02f, 0.06f);
                if (li == roofLi && (parts & ModuleMesh.WallsP) != 0) RoofTop(byMat, edgeCnt, li, y0, area);
                if ((parts & ModuleMesh.WallsP) != 0) { Corners(L, y0); Walls(L, li, y0); }
                if ((parts & ModuleMesh.Stairs) != 0) foreach (var S in L.s) Stair(S, li, y0);
                if ((parts & ModuleMesh.Roofs) != 0) Roofs(L, li, y0);
                if ((parts & ModuleMesh.Objs) != 0) foreach (var O in L.o)
                {
                    if (li == roofLi && Covered(new Vector2(O.x, O.z), area)) continue;
                    Obj(O, y0);
                }
            }
        }
        // Een plat dak: rondom een dakrand (attiek, 0,7 m) in het gevelmateriaal van de verdieping eronder met een afdekkap, behalve
        // waar een trap uitkomt of een module bovenop staat; en als er ruimte is een zeshoekige installatiekast.
        void RoofTop(Dictionary<string, List<Vector2[]>> byMat, Dictionary<(Vector2Int, Vector2Int), int> edgeCnt, int li, float y0, List<Vector2Int> area)
        {
            var below = m.lv[li - 1]; var cnt = new Dictionary<string, int>(); string wm = "slab"; int best = 0;
            foreach (var w in below.w.Values) { if (w.m == "fence" || w.m == "hedge" || w.m == "glass" || w.m == "curtain") continue; cnt.TryGetValue(w.m, out int n0); cnt[w.m] = n0 + 1; if (n0 + 1 > best) { best = n0 + 1; wm = "w_" + w.m; } }
            var stairs = new List<Vector2>(); foreach (var S in below.s) stairs.Add(MV2.P(S.ai, S.aj, ts));
            Vector2Int Q(Vector2 p) => new Vector2Int(Mathf.RoundToInt(p.x * 64f), Mathf.RoundToInt(p.y * 64f));
            int tris = 0; Vector2 sum = Vector2.zero;
            foreach (var kv in byMat)
            {
                if (kv.Key == "glass" || kv.Key == "water") continue;
                foreach (var v in kv.Value)
                {
                    tris++; sum += MV2.Centroid(v);
                    for (int e = 0; e < 3; e++)
                    {
                        Vector2 p = v[e], q = v[(e + 1) % 3]; var a2 = Q(p); var b2 = Q(q); var k2 = a2.x < b2.x || (a2.x == b2.x && a2.y <= b2.y) ? (a2, b2) : (b2, a2);
                        if (!edgeCnt.TryGetValue(k2, out int c1) || c1 != 1) continue;
                        Vector2 mid = (p + q) * 0.5f, nn = new Vector2(-(q - p).y, (q - p).x).normalized; bool skip = false;
                        foreach (var st in stairs) if ((st - mid).magnitude < 4f) skip = true;
                        if (Covered(mid + nn * 0.4f, area) || Covered(mid - nn * 0.4f, area)) skip = true;
                        if (skip) continue;
                        Vector2 cen = MV2.Centroid(v); float sd = Vector2.Dot(cen - mid, nn) > 0f ? -1f : 1f;   // naar buiten: weg van de driehoek
                        Box(wm, p, q, y0, y0 + 0.7f, T, sd * T / 2f); Box("lijst", p, q, y0 + 0.7f, y0 + 0.78f, T + 0.08f, sd * T / 2f);
                    }
                }
            }
            if (tris < 30) return;
            // installatiekast: een zeshoekig blok bij het midden, niet bij voorwerpen (zonnepanelen, groen) en niet onder een module erboven
            Vector2 c = sum / tris; foreach (var O in m.lv[li].o) if ((new Vector2(O.x, O.z) - c).magnitude < 3f) return;
            if (Covered(c, area)) return;
            var hex = new Vector2[6]; for (int k = 0; k < 6; k++) { float an = (30f + 60f * k) * Mathf.Deg2Rad; hex[k] = c + new Vector2(Mathf.Cos(an), Mathf.Sin(an)) * 1.1f; }
            Prism("slab", hex, y0, y0 + 1.3f); for (int k = 0; k < 6; k++) hex[k] = c + (hex[k] - c) * 1.12f; Prism("metal", hex, y0 + 1.3f, y0 + 1.4f);
        }
        bool Covered(Vector2 p, List<Vector2Int> area)
        {
            if (cover == 0) return false; var c = MV2.CellOf(p.x, p.y);
            for (int i = 0; i < area.Count; i++) if (area[i] == c) return (cover & (1 << i)) != 0; return false;
        }

        // --- hoeken: waar twee muren onder een hoek samenkomen een ronde hoekpost ---
        void Corners(ModLevel L, float y0)
        {
            var ends = new Dictionary<(int, int), List<(Vector2 dir, ModWall W)>>();
            foreach (var kv in L.w)
            {
                var W = kv.Value; if (W.m == "fence" || W.m == "glass") continue; var (i, j, d) = kv.Key; if (d < 0 || d > 5) continue; var D = MV2.DIRW[d];
                var (a, b) = MV2.WallSeg(kv.Key, ts); if (!MV2.NearArea(m, (a.x + b.x) / 2f, (a.y + b.y) / 2f)) continue;
                var u = (b - a).normalized;
                void Add((int, int) k, Vector2 dir) { if (!ends.TryGetValue(k, out var l)) ends[k] = l = new List<(Vector2, ModWall)>(); l.Add((dir, W)); }
                Add((i, j), u); Add((i + D.x, j + D.y), -u);
            }
            foreach (var kv in ends)
            {
                var L2 = kv.Value; if (L2.Count < 2) continue; bool bent = false;
                for (int x = 0; x < L2.Count && !bent; x++) for (int y = x + 1; y < L2.Count; y++) if (Vector2.Dot(L2[x].dir, L2[y].dir) > -0.99f) { bent = true; break; }
                if (!bent) continue;
                bool hedge = L2.TrueForAll(e => e.W.m == "hedge"); var W0 = L2.Find(e => e.W.m != "hedge").W ?? L2[0].W;
                float r = hedge ? 0.3f : T / 2f, h = hedge ? 1.4f : LH; var c = MV2.P(kv.Key.Item1, kv.Key.Item2, ts);
                Cyl(hedge ? "w_hedge" : W0.m == "curtain" ? "frame" : "w_" + W0.m, c, r * 1.1f, r * 1.1f, y0, h, 6);   // zeshoekige hoekpost
            }
        }

        // --- muren ---
        void Walls(ModLevel L, int li, float y0)
        {
            foreach (var kv in L.w)
            {
                var W = kv.Value; if (kv.Key.Item3 < 0 || kv.Key.Item3 > 5) continue; var (a, b) = MV2.WallSeg(kv.Key, ts); float len = (b - a).magnitude;
                if (!MV2.NearArea(m, (a.x + b.x) / 2f, (a.y + b.y) / 2f)) continue;
                string mat = "w_" + W.m; string o = W.o ?? "";
                if (W.m == "hedge" || W.m == "fence")
                {
                    string og = o != "window" ? o : ""; float gw = og == "door" ? Mathf.Min(1.4f, len - 0.6f) : og == "gate" ? Mathf.Max(1.5f, len - 0.5f) : og != "" ? len - 0.3f : 0f, g0 = (len - gw) / 2f / len;
                    var parts = gw > 0.1f ? new[] { (a, Vector2.Lerp(a, b, g0)), (Vector2.Lerp(a, b, 1f - g0), b) } : new[] { (a, b) };
                    foreach (var (pa, pb) in parts)
                    {
                        if ((pb - pa).magnitude < 0.05f) continue;
                        if (W.m == "hedge") { Box(mat, pa, pb, y0, y0 + 1.4f, 0.6f); continue; }
                        foreach (float f in new[] { 0f, 0.5f, 1f }) { var p = Vector2.Lerp(pa, pb, f); var q = Vector2.Lerp(pa, pb, Mathf.Min(1f, f + 0.02f)); if (f >= 1f) q = Vector2.Lerp(pa, pb, 0.98f); Box("metal", p, q, y0, y0 + 1.1f, 0.08f); }
                        Box("metal", pa, pb, y0 + 0.5f, y0 + 0.55f, 0.05f); Box("metal", pa, pb, y0 + 1.05f, y0 + 1.1f, 0.05f);
                    }
                    if (gw > 0.1f && W.m == "fence") foreach (var pp in new[] { Vector2.Lerp(a, b, g0), Vector2.Lerp(a, b, 1f - g0) }) Box("metal", pp, Vector2.Lerp(pp, b, 0.01f), y0, y0 + 1.25f, 0.12f);
                    continue;
                }
                bool glass = W.m == "glass";
                if (W.p && !glass && W.m != "curtain" && (li + 1 >= m.lv.Count || !m.lv[li + 1].w.ContainsKey(kv.Key)))
                { Pier(mat, a, y0, LH); Pier(mat, b, y0, LH); Box("lijst", a, b, y0 + LH, y0 + LH + 0.06f, T + 0.08f); }   // tuinmuur: steunpilaren
                if (o == "" && W.m == "curtain") { Curtain(a, b, y0, "w_curtain"); continue; }
                if (glass && m.cat != "Verbindingen" && (o == "" || o == "window") && Outer(L, a, b)) { Curtain(a, b, y0, "facade"); continue; }   // een glaswand aan de buitenkant: een glasgevel (een trappenhuis houdt doorzichtig glas)
                if (o == "") { Box(mat, a, b, y0, y0 + LH, glass ? 0.06f : T); if (glass) { Box("frame", a, b, y0, y0 + 0.12f, T); Box("frame", a, b, y0 + LH - 0.12f, y0 + LH, T); Mullions(a, b, y0 + 0.12f, y0 + LH - 0.12f, T); } else Details(L, li, kv.Key, a, b, "", a, b, 0f, 0f, y0); continue; }
                float ow = o == "door" ? Mathf.Min(1.2f, len - 0.6f) : o == "window" ? Mathf.Max(0.6f, len - 0.8f) : o == "gate" ? Mathf.Max(1.5f, len - 0.5f) : len - 0.3f;
                float oy0 = o == "window" ? 0.9f : 0f, oy1 = o == "door" ? 2.2f : o == "window" ? 2.15f : o == "gate" ? 2.8f : LH - 0.3f;
                float f0 = (len - ow) / 2f / len; Vector2 p0 = Vector2.Lerp(a, b, f0), p1 = Vector2.Lerp(a, b, 1f - f0);
                if (f0 > 0.01f) { Box(mat, a, p0, y0, y0 + LH, T); Box(mat, p1, b, y0, y0 + LH, T); }
                if (oy1 < LH - 0.01f) Box(mat, p0, p1, y0 + oy1, y0 + LH, T);
                if (oy0 > 0.01f) Box(mat, p0, p1, y0, y0 + oy0, T);
                if (o == "window")
                {
                    Box("frame", p0, p1, y0 + oy0 - 0.04f, y0 + oy0 + 0.04f, T + 0.1f); Box("frame", p0, p1, y0 + oy1 - 0.04f, y0 + oy1, T);
                    Box("frame", p0, Vector2.Lerp(p0, p1, 0.04f / ow), y0 + oy0, y0 + oy1, T); Box("frame", Vector2.Lerp(p1, p0, 0.04f / ow), p1, y0 + oy0, y0 + oy1, T);
                    Box(Lit((p0 + p1) * 0.5f, y0) ? "lit" : "pane", p0, p1, y0 + oy0, y0 + oy1, 0.02f);
                }
                if (o == "door")
                {
                    Box("trim", p0, p1, y0 + oy1 - 0.06f, y0 + oy1, T + 0.08f);
                    Box("trim", p0, Vector2.Lerp(p0, p1, 0.06f / ow), y0, y0 + oy1, T + 0.08f); Box("trim", Vector2.Lerp(p1, p0, 0.06f / ow), p1, y0, y0 + oy1, T + 0.08f);
                }
                if (o == "gate") Box("trim", p0, p1, y0 + oy1 - 0.1f, y0 + oy1, T + 0.1f);
                if (!glass) Details(L, li, kv.Key, a, b, o, p0, p1, oy0, oy1, y0);
            }
        }

        // Gevel aan de buitenkant (binnenmuren niet): plint onderaan (niet in een deur of doorgang), een lijst op elke verdiepingsvloer,
        // een bredere kroonlijst onder het dak, een vensterbank onder een raam en een luifel boven een buitendeur. Alles steekt alleen
        // naar buiten uit (de binnenkant van de muur blijft vlak).
        void Details(ModLevel L, int li, (int, int, int) key, Vector2 a, Vector2 b, string o, Vector2 p0, Vector2 p1, float oy0, float oy1, float y0)
        {
            int side = OuterSide(L, a, b); if (side == 0) return; Vector2 u = (b - a).normalized;
            bool topWall = li + 1 >= m.lv.Count || !m.lv[li + 1].w.ContainsKey(key);   // hierboven geen muur meer: onder het dak
            bool gap = o == "door" || o == "gate" || o == "open";
            if (li == 0 && !stacked) { if (gap) { if ((p0 - a).magnitude > 0.05f) Box("plint", a, p0, y0, y0 + 0.45f, T + 0.06f, side * 0.03f); if ((b - p1).magnitude > 0.05f) Box("plint", p1, b, y0, y0 + 0.45f, T + 0.06f, side * 0.03f); } else Box("plint", a, b, y0, y0 + 0.45f, T + 0.06f, side * 0.03f); }
            bool pitched = false; foreach (var rv in L.r.Values) if (rv == "half" || rv == "laan") pitched = true;   // die daken hebben een eigen overstek met boeiboord
            if (topWall && !pitched) Box("lijst", a - u * 0.12f, b + u * 0.12f, y0 + LH - 0.28f, y0 + LH, T + 0.24f, side * 0.12f);   // kroonlijst
            else Box("lijst", a, b, y0 + LH - 0.12f, y0 + LH + 0.02f, T + 0.1f, side * 0.05f);                     // verdiepingslijst
            if (o == "window") Box("lijst", p0 - u * 0.06f, p1 + u * 0.06f, y0 + oy0 - 0.08f, y0 + oy0 - 0.02f, T + 0.14f, side * 0.07f);   // vensterbank
            if (o == "door") Box("trim", p0 - u * 0.35f, p1 + u * 0.35f, y0 + oy1 + 0.18f, y0 + oy1 + 0.26f, 0.95f, side * (T / 2f + 0.47f));   // luifel
        }
        // aan welke kant van de muur is het buiten? +1: de kant van de normaal (−dz, dx), −1: de andere, 0: binnenmuur
        int OuterSide(ModLevel L, Vector2 a, Vector2 b)
        {
            Vector2 d = b - a, n = new Vector2(-d.y, d.x).normalized, mid = (a + b) * 0.5f;
            bool pa = Inside(L, mid + n * 0.7f), pb = Inside(L, mid - n * 0.7f);
            return pa == pb ? 0 : pa ? -1 : 1;
        }
        // ligt punt p binnen (een binnenvloer, geen bestrating, gras of water)?
        bool Inside(ModLevel L, Vector2 p)
        {
            float fi = p.x / (ts * MV2.SQ3 / 2f), fj = p.y / ts - fi / 2f; int i = Mathf.FloorToInt(fi), j = Mathf.FloorToInt(fj); int t = (fi - i) + (fj - j) < 1f ? 0 : 1;
            return L.f.TryGetValue((i, j, t), out var f) && f != "paver" && f != "tile" && f != "grass" && f != "asphalt" && f != "water";
        }
        // Een muur met steunpilaren (optie per muur, ModWall.p, voor een tuinmuur): op elk hoekpunt een zeshoekige steunpilaar die aan beide
        // kanten uitsteekt en iets boven de muur uitkomt, met een afdekkap; over de muur zelf een afdekband.
        readonly HashSet<(int, int, int)> piers = new HashSet<(int, int, int)>();
        void Pier(string mat, Vector2 p, float y0, float h)
        {
            if (!piers.Add((Mathf.RoundToInt(p.x * 20f), Mathf.RoundToInt(p.y * 20f), Mathf.RoundToInt(y0 * 10f)))) return;
            Cyl(mat, p, 0.34f, 0.34f, y0, h + 0.15f, 6); Cyl("lijst", p, 0.42f, 0.42f, y0 + h + 0.15f, 0.1f, 6); Cyl("lijst", p, 0.2f, 0.3f, y0 + h + 0.25f, 0.12f, 6);
        }

        // glasgevel (zoals de textuur in de webversie): per verdieping een donkere borstwering van 0,9 m, daarboven de ruit een paar cm
        // terug in de gevel, aluminium stijlen om de 1,5 m en regels op de borstwering en onder het plafond
        void Curtain(Vector2 a, Vector2 b, float y0, string glassKey)
        {
            Box("spandrel", a, b, y0, y0 + 0.9f, T * 0.6f);
            { Vector2 d = b - a; int n = Mathf.Max(1, Mathf.RoundToInt(d.magnitude / 1.5f)); for (int k = 0; k < n; k++) { Vector2 p = a + d * (k / (float)n), q = a + d * ((k + 1) / (float)n); Box(Lit((p + q) * 0.5f, y0) ? "lit" : glassKey, p, q, y0 + 0.9f, y0 + LH, 0.05f); } }
            Box("frame", a, b, y0 + 0.86f, y0 + 0.96f, T); Box("frame", a, b, y0 + LH - 0.1f, y0 + LH, T);
            Mullions(a, b, y0, y0 + LH, T);
        }
        // ligt de muur tussen binnen en buiten? (aan één kant een binnenvloer, aan de andere kant niets of bestrating, gras, water)
        bool Outer(ModLevel L, Vector2 a, Vector2 b)
        {
            Vector2 d = b - a, n = new Vector2(-d.y, d.x).normalized, mid = (a + b) * 0.5f;
            bool Inside(Vector2 p) { float fi = p.x / (ts * MV2.SQ3 / 2f), fj = p.y / ts - fi / 2f; int i = Mathf.FloorToInt(fi), j = Mathf.FloorToInt(fj); int t = (fi - i) + (fj - j) < 1f ? 0 : 1;
                return L.f.TryGetValue((i, j, t), out var f) && f != "paver" && f != "tile" && f != "grass" && f != "asphalt" && f != "water"; }
            return Inside(mid + n * 0.7f) != Inside(mid - n * 0.7f);
        }
        // brandt hier 's avonds licht? een vast deel van de ruiten (ongeveer een derde), per plek en verdieping
        static int SH(string t) { int h = 17; if (t != null) foreach (char ch in t) h = h * 31 + ch; return h; }   // vaste hash (niet per start anders)
        static bool Lit(Vector2 p, float y) => Mathf.Repeat(Mathf.Sin(p.x * 12.9898f + p.y * 78.233f + y * 37.719f) * 43758.5453f, 1f) < 0.34f;
        void Mullions(Vector2 a, Vector2 b, float y0, float y1, float t)
        {
            Vector2 d = b - a; float len = d.magnitude; if (len < 0.3f) return; Vector2 u = d / len; int n = Mathf.Max(1, Mathf.RoundToInt(len / 1.5f));
            for (int k = 0; k <= n; k++) { var p = a + d * (k / (float)n); Box("frame", p - u * 0.045f, p + u * 0.045f, y0, y1, t + 0.04f); }
        }

        // --- trappen en hellingen ---
        void Stair(ModStair S, int li, float y0)
        {
            var D = MV2.DIR6[Mathf.Clamp(S.d, 0, 5)]; Vector2 a = MV2.P(S.ai, S.aj, ts); float len = (S.n > 0 ? S.n : 2f) * ts, w = S.w > 0 ? S.w : 2f;
            float dx = D.x * ts * MV2.SQ3 / 2f, dz = D.x * ts / 2f + D.y * ts, dl = Mathf.Sqrt(dx * dx + dz * dz); if (dl < 1e-6f) dl = 1f;
            Vector2 u = new Vector2(dx / dl, dz / dl), nrm = new Vector2(-u.y, u.x); float rise = S.h > 0f ? S.h : LH, off = S.off;
            Vector2 At(float al, float lat) => a + u * al + nrm * (lat + off);
            Vector3 A3(Vector2 p, float y) => new Vector3(p.x, y, p.y);
            void EndFill(float yt, string key)
            {
                if (li + 1 >= m.lv.Count) return; var hole = MV2.StairHole(S, ts);
                foreach (var kv in m.lv[li + 1].f)
                {
                    var v = MV2.TriVerts(kv.Key.Item1, kv.Key.Item2, kv.Key.Item3, ts); var h = hole(v); if (h == null || !h.fill.HasValue) continue;
                    var pl = MV2.ClipKeep(v, h.fill.Value); if (pl.Count < 3) continue; float ar = 0f; for (int k = 1; k + 1 < pl.Count; k++) ar += MV2.TriArea2(pl[0], pl[k], pl[k + 1]);
                    if (ar < 0.02f) continue; Prism(key, pl, yt - MV2.FLOOR_T, yt + 0.02f);
                }
            }
            if (S.k == "spiral")
            {
                float sd = S.Side, a0 = Mathf.Atan2(u.y, u.x), Ap = 1.9f, Ac = 0.32f, TH = 0.26f; int NS = 12; float hs = rise / NS;
                float Th(int k) => a0 + sd * k * Mathf.PI / 6f;
                float Rad(float t, float Aa) { float d = Mathf.Repeat(t - a0 - Mathf.PI / 6f, Mathf.PI / 3f); if (d > Mathf.PI / 6f) d -= Mathf.PI / 3f; return Aa / Mathf.Cos(d); }
                Vector2 Pt(float t, float Aa) => a + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * Rad(t, Aa);
                for (int st = 0; st < NS; st++)
                {
                    float t0 = Th(st), t1 = Th(st + 1), top = y0 + (st + 1) * hs;
                    Prism("step", new[] { Pt(t0, Ac), Pt(t0, Ap), Pt(t1, Ap), Pt(t1, Ac) }, top - TH, top, "w_wood");
                }
                Cyl("metal", a, Ac / 0.866f, Ac / 0.866f, y0, rise + 1.05f, 6);
                for (int kk = 1; kk <= NS - 1; kk++)
                {
                    var A = Pt(Th(kk), Ap); float yk = y0 + kk * hs; Post("metal", A, yk - TH, yk + 0.95f);
                    if (kk < NS - 1) Bar("metal", A3(A, yk + 0.95f), A3(Pt(Th(kk + 1), Ap), yk + hs + 0.95f), 0.05f);
                }
                float topY = y0 + rise;
                for (int kk = 0; kk <= NS - 3; kk++) { var A = Pt(Th(kk), Ap); var B = Pt(Th(kk + 1), Ap); Post("metal", A, topY, topY + 0.95f); Bar("metal", A3(A, topY + 0.95f), A3(B, topY + 0.95f), 0.05f); }
                for (int kk = NS - 2; kk <= NS - 1; kk++) Post("metal", Pt(Th(kk), Ap), topY, topY + 0.95f);
                return;
            }
            if (S.k == "hexramp")
            {
                var c = MV2.CellXZ(S.q, S.r); var f = MV2.FN(((S.side % 6) + 6) % 6); Vector2 e = -f, l = new Vector2(-e.y, e.x); float IN = MV2.INNER;
                Vector2 Pt(float uu, float lat) => c + e * uu + l * lat; float Hz(float uu) => y0 + rise * (uu + IN) / (2f * IN);
                float top = y0 + rise; var rt = M("ramptop");
                void Tri3(Vector2 A, float ya, Vector2 B, float yb, Vector2 C, float yc) => rt.Tri(U(A, ya), U(B, yb), U(C, yc), Vector3.up);
                Tri3(Pt(-IN, -4.5f), Hz(-IN), Pt(IN, 4.5f), Hz(IN), Pt(-IN, 4.5f), Hz(-IN)); Tri3(Pt(-IN, -4.5f), Hz(-IN), Pt(IN, -4.5f), Hz(IN), Pt(IN, 4.5f), Hz(IN));
                Tri3(Pt(-IN, 4.5f), y0, Pt(IN, 4.5f), top, Pt(0, 9f), top); Tri3(Pt(-IN, -4.5f), y0, Pt(0, -9f), top, Pt(IN, -4.5f), top);
                void Quad2(Vector2 A, float ya0, float ya1, Vector2 B, float yb0, float yb1) { Both("slab", A3(A, ya0), A3(B, yb0), A3(B, yb1)); Both("slab", A3(A, ya0), A3(B, yb1), A3(A, ya1)); }
                Quad2(Pt(IN, 4.5f), y0, top, Pt(IN, -4.5f), y0, top);
                Quad2(Pt(0, 9f), y0, top, Pt(IN, 4.5f), y0, top); Quad2(Pt(IN, -4.5f), y0, top, Pt(0, -9f), y0, top);
                Quad2(Pt(-IN, 4.5f), y0, y0, Pt(0, 9f), y0, top); Quad2(Pt(0, -9f), y0, top, Pt(-IN, -4.5f), y0, y0);
                void Rail(Vector2 A, float ya, Vector2 B, float yb) { Bar("metal", A3(A, ya + 1f), A3(B, yb + 1f), 0.05f); Bar("metal", A3(A, ya + 0.47f), A3(B, yb + 0.47f), 0.03f); Post("metal", A, ya, ya + 1f); Post("metal", B, yb, yb + 1f); }
                foreach (float sg in new[] { 1f, -1f }) { Rail(Pt(-IN, sg * 4.5f), y0, Pt(0, sg * 9f), top); Rail(Pt(0, sg * 9f), top, Pt(IN, sg * 4.5f), top); }
                return;
            }
            if (S.k == "u")
            {
                float sd = S.Side, h1 = rise / 2f; int NH = Mathf.Max(2, Mathf.RoundToInt(h1 / 0.1875f)); float run = len / NH, rs = h1 / NH, TH = 0.3f, c2 = sd * w, yl = y0 + h1, yt = y0 + rise;
                for (int s = 0; s < NH; s++)
                {
                    float t1 = y0 + (s + 1) * rs; Box("step", At(s * run, 0f), At((s + 1) * run, 0f), Mathf.Max(y0, t1 - rs - TH), t1, w);
                    float t2 = yl + (s + 1) * rs, ua = len - s * run, ub = len - (s + 1) * run; Box("step", At(ua, c2), At(ub, c2), t2 - rs - TH, t2, w);
                }
                float oA = -sd * w / 2f, oB = c2 + sd * w / 2f, cut = 2f * w / MV2.SQ3;
                Prism("step", new[] { At(len, oA), At(len, oB), At(len + 0.25f + cut, oB), At(len + 0.25f, oA) }, yl - TH, yl);
                float eA = oA + sd * 0.05f, eB = oB - sd * 0.05f, mid = sd * w / 2f; Vector2 LP0 = At(len + 0.2f, eA), LP1 = At(len + 0.2f + cut, eB);
                Bar("metal", A3(At(0, eA), y0 + 0.95f), A3(At(len, eA), yl + 0.95f), 0.05f); Bar("metal", A3(At(0, mid), yt + 0.95f), A3(At(len, mid), yl + 0.95f), 0.05f); Bar("metal", A3(At(0, eB), yt + 0.95f), A3(At(len, eB), yl + 0.95f), 0.05f);
                Bar("metal", A3(At(len, eA), yl + 0.95f), A3(LP0, yl + 0.95f), 0.05f); Bar("metal", A3(LP0, yl + 0.95f), A3(LP1, yl + 0.95f), 0.05f); Bar("metal", A3(LP1, yl + 0.95f), A3(At(len, eB), yl + 0.95f), 0.05f);
                foreach (float f in new[] { 0f, 0.5f, 1f }) { Post("metal", At(f * len, eA), y0 + f * h1, y0 + f * h1 + 0.95f); Post("metal", At(f * len, eB), yl + (1f - f) * h1, yl + (1f - f) * h1 + 0.95f); }
                Post("metal", LP0, yl, yl + 0.95f); Post("metal", LP1, yl, yl + 0.95f);
                return;
            }
            if (S.k == "ramp")
            {
                float yb = y0 + 0.05f, yt = y0 + rise; Vector2 P0 = At(0, -w / 2f), P1 = At(0, w / 2f), P2 = At(len, w / 2f), P3 = At(len, -w / 2f);
                Solid("ramptop", new[] { A3(P0, y0), A3(P1, y0), A3(P2, y0), A3(P3, y0), A3(P0, yb), A3(P1, yb), A3(P2, yt), A3(P3, yt) },
                    new[] { new[] { 0, 1, 2, 3 }, new[] { 4, 5, 6, 7 }, new[] { 0, 1, 5, 4 }, new[] { 1, 2, 6, 5 }, new[] { 2, 3, 7, 6 }, new[] { 3, 0, 4, 7 } });
                EndFill(yt, "ramptop"); return;
            }
            if (S.k == "esc")
            {
                int N = Mathf.Max(2, Mathf.RoundToInt(rise / 0.2f)); float run = len / N, rs = rise / N, yt = y0 + rise;
                for (int s = 0; s < N; s++) { float top = y0 + (s + 1) * rs; Box("metal", At(s * run, 0f), At((s + 1) * run, 0f), top - 0.1f, top, w - 0.3f); }
                Bar("trim", A3(At(0, 0), y0 - 0.35f), A3(At(len, 0), yt - 0.35f), w, 0.55f);
                foreach (float sg in new[] { -1f, 1f })
                {
                    Vector2 A = At(-0.4f, sg * (w / 2f - 0.05f)), B = At(len + 0.4f, sg * (w / 2f - 0.05f));
                    Both("w_glass", A3(A, y0 + 0.05f), A3(B, yt + 0.05f), A3(B, yt + 1f)); Both("w_glass", A3(A, y0 + 0.05f), A3(B, yt + 1f), A3(A, y0 + 1f));
                    Bar("trim", A3(A, y0 + 1f), A3(B, yt + 1f), 0.05f);
                }
                EndFill(yt, "metal"); return;
            }
            if (S.k == "trib")
            {
                int N = Mathf.Max(2, Mathf.RoundToInt(len / 0.85f)); float run = len / N, rs = rise / N, yt = y0 + rise;
                for (int s = 0; s < N; s++) { float top = y0 + (s + 1) * rs; Box("step", At(s * run, 0f), At((s + 1) * run, 0f), y0, top, w); Box("w_wood", At(s * run + run * 0.55f, 0f), At((s + 1) * run - 0.05f, 0f), top, top + 0.04f, w - 0.2f); }
                Bar("metal", A3(At(len - 0.05f, -w / 2f), yt + 1f), A3(At(len - 0.05f, w / 2f), yt + 1f), 0.05f); foreach (float f in new[] { 0f, 0.5f, 1f }) Post("metal", At(len - 0.05f, -w / 2f + f * w), yt, yt + 1f);
                return;
            }
            {   // rechte steek: 16 treden per 3 m (0,1875 m), zwevend (0,3 m dik), boven een bordes tot de driehoekzijde
                int N = Mathf.Max(2, Mathf.RoundToInt(rise / 0.1875f)); float run = len / N, rs = rise / N, TH = 0.3f, yt = y0 + rise;
                for (int s = 0; s < N; s++) { float top = y0 + (s + 1) * rs; Box("step", At(s * run, 0f), At((s + 1) * run, 0f), Mathf.Max(y0, top - rs - TH), top, w); }
                EndFill(yt, "step");
                foreach (float sg in new[] { -1f, 1f })
                {
                    float e = sg * (w / 2f - 0.05f); Bar("metal", A3(At(0, e), y0 + 0.95f), A3(At(len, e), yt + 0.95f), 0.05f);
                    foreach (float f in new[] { 0f, 0.5f, 1f }) Post("metal", At(f * len, e), y0 + f * rise, y0 + f * rise + 0.95f);
                }
            }
        }

        // --- daken per vak: het dak volgt de muren van dat vak op deze verdieping, met 35 cm overstek ---
        void Roofs(ModLevel L, int li, float y0)
        {
            foreach (var kv in L.r)
            {
                var (q, r) = kv.Key; var c = MV2.CellXZ(q, r); string kind = kv.Value ?? "hip";
                float rr = MV2.RoofFit(m, li, q, r), ro = rr + (rr < MV2.R ? 0.35f : 0f), yb = y0 + LH, h = 5f * rr / MV2.R;
                Vector2 Vk(int k, float rad) { float an = (30f + 60f * k) * Mathf.Deg2Rad; return c + new Vector2(Mathf.Cos(an), Mathf.Sin(an)) * rad; }
                if (kind == "half") { HalfRoof(L, c, rr, ro - rr, yb); continue; }
                if (kind == "laan") { LaanRoof(L, c, ro, yb); continue; }
                if (kind == "flat") { var pl = new Vector2[6]; float rf = rr + (rr < MV2.R ? 0.15f : -0.1f); for (int k = 0; k < 6; k++) pl[k] = Vk(k, rf); Prism("slab", pl, yb, yb + 0.3f); continue; }
                if (kind == "hip" && PlanetCity.DakShape(PlanetCity.Dormers(Mathf.RoundToInt(c.x * 7f) * 31 + Mathf.RoundToInt(c.y * 13f) * 17 + li * 101 + SH(m.id))) is List<PlanetCity.DakTri> shape)   // schilddak: het huisdak uit SketchUp (PlanetCity.Daken.cs), op de maat van de muren
                {
                    float s2 = ro / MV2.R; var mr = M("roof"); var mrim = M("slab");
                    Vector3 W(Vector3 p) => U(c.x + p.x * s2, yb + p.z, c.y - p.y * s2);
                    var mg = M(Lit(c, yb) ? "lit" : "spandrel"); var mf = M("frame");
                    foreach (var t in shape) { Vector3 n = Vector3.Cross(t.b - t.a, t.c - t.a); (t.kind == PlanetCity.DakKind.Glass ? mg : t.kind == PlanetCity.DakKind.Frame ? mf : t.kind == PlanetCity.DakKind.Rim ? mrim : mr).Tri(W(t.a), W(t.b), W(t.c), U(n.x, n.z, -n.y)); }
                    continue;
                }
                var under = M("ceiling"); for (int k = 0; k < 6; k++) under.Tri(U(c, yb), U(Vk(k, ro), yb), U(Vk(k + 1, ro), yb), Vector3.down);
                var rf2 = M("roof");
                void RT(Vector3 A, Vector3 B, Vector3 C) => rf2.Tri(U(A.x, A.y, A.z), U(B.x, B.y, B.z), U(C.x, C.y, C.z), Vector3.up);
                Vector3 V3(int k, float y) { var p = Vk(((k % 6) + 6) % 6, ro); return new Vector3(p.x, y, p.y); }
                if (kind.StartsWith("gable") && kind.Length > 5)
                {
                    int ax = (kind[5] - '0') % 3; Vector3 Va = V3(ax, yb + h), Vb = V3(ax + 3, yb + h);
                    RT(Va, V3(ax + 1, yb), V3(ax + 2, yb)); RT(Va, V3(ax + 2, yb), Vb); RT(Vb, V3(ax + 4, yb), V3(ax + 5, yb)); RT(Vb, V3(ax + 5, yb), Va);
                    var cnt = new Dictionary<string, int>(); string wm = "brick"; int best = 0;
                    foreach (var w in L.w.Values) { if (w.m == "fence" || w.m == "hedge") continue; cnt.TryGetValue(w.m, out int n0); cnt[w.m] = n0 + 1; if (n0 + 1 > best) { best = n0 + 1; wm = w.m; } }
                    var gw = M("w_" + wm);
                    foreach (int k in new[] { ax, ax + 3 })
                    {
                        Vector2 Wl(int kk) => Vk(((kk % 6) + 6) % 6, rr); var Tp = Wl(k); float th = yb + h * rr / ro;
                        foreach (var (P1, P2) in new[] { (Wl(k + 5), Wl(k)), (Wl(k), Wl(k + 1)) }) { Vector2 mid = (P1 + P2) * 0.5f - c; gw.Tri(U(P1, yb), U(Tp, th), U(P2, yb), U(mid, 0f)); }
                    }
                }
                else { var top = new Vector3(c.x, yb + h, c.y); for (int k = 0; k < 6; k++) RT(top, V3(k, yb), V3(k + 1, yb)); }
            }
        }

        // de vloer van de buurdriehoek aan de ribbe a–b (de overstaande punt is c); null: geen vloer of buiten het gebied
        string NbMat(ModLevel L, Vector2 a, Vector2 b, Vector2 c)
        {
            Vector2 cen = (a + b + (a + b - c)) / 3f; if (!MV2.InArea(m, cen.x, cen.y)) return null;
            float fi = cen.x / (ts * MV2.SQ3 / 2f), fj = cen.y / ts - fi / 2f; int i = Mathf.FloorToInt(fi), j = Mathf.FloorToInt(fj); int t = (fi - i) + (fj - j) < 1f ? 0 : 1;
            return L.f.TryGetValue((i, j, t), out var f) ? f : null;
        }
        // Half schilddak: een huis op een halve zeshoek (drie driehoeken van 60°). Welke helft volgt uit de vloer van de verdieping,
        // dus draaien en spiegelen gaan vanzelf goed. De nok ligt halverwege de diepte; alle dakvlakken hebben dezelfde helling.
        void HalfRoof(ModLevel L, Vector2 c, float rr, float ov, float yb)
        {
            Vector2 sum = Vector2.zero; int nf = 0;
            foreach (var kv in L.f) { var cen = MV2.Centroid(MV2.TriVerts(kv.Key.Item1, kv.Key.Item2, kv.Key.Item3, ts)); if ((cen - c).magnitude < MV2.R) { sum += cen - c; nf++; } }
            Vector2 Vk(int k, float rad) { float an = (30f + 60f * (((k % 6) + 6) % 6)) * Mathf.Deg2Rad; return c + new Vector2(Mathf.Cos(an), Mathf.Sin(an)) * rad; }
            int a = 0; float bd = -1e9f; for (int k = 0; k < 6; k++) { float d = Vector2.Dot(sum, Vk(k + 1, 1f) + Vk(k + 2, 1f) - 2f * c); if (d > bd) { bd = d; a = k; } }
            var P0 = new[] { Vk(a, rr), Vk(a + 1, rr), Vk(a + 2, rr), Vk(a + 3, rr) };
            Vector2 A = P0[0], B = P0[1], C = P0[2], D = P0[3], u = (D - A).normalized, inn = ((B + C) * 0.5f - (A + D) * 0.5f);
            float H = Mathf.Abs(Vector2.Dot(inn, new Vector2(-u.y, u.x))); inn = new Vector2(-u.y, u.x) * Mathf.Sign(Vector2.Dot(inn, new Vector2(-u.y, u.x)));
            Vector2 R1 = A + u * (H / 2f * MV2.SQ3) + inn * (H / 2f), R2 = D - u * (H / 2f * MV2.SQ3) + inn * (H / 2f);
            const float slope = 0.9f; float h = H / 2f * slope, ye = yb - EAVE * slope; var rf = M("roof");
            // de voet EAVE buiten de muren (overstek), het dakvlak loopt door en komt daar lager uit
            var Pp = new Vector2[4]; for (int k = 0; k < 4; k++) Pp[k] = OutCorner(P0[(k + 3) % 4], P0[k], P0[(k + 1) % 4], EAVE);
            Vector3 T(Vector2 p, float y) => U(p, y);
            Vector3 eA = T(Pp[0], ye), eB = T(Pp[1], ye), eC = T(Pp[2], ye), eD = T(Pp[3], ye), r1 = T(R1, yb + h), r2 = T(R2, yb + h);
            rf.Tri(eA, eD, r2, Vector3.up); rf.Tri(eA, r2, r1, Vector3.up); rf.Tri(eC, eB, r1, Vector3.up); rf.Tri(eC, r1, r2, Vector3.up);
            rf.Tri(eB, eA, r1, Vector3.up); rf.Tri(eD, eC, r2, Vector3.up);
            Eaves(new[] { eA, eB, eC, eD }, c);
        }

        // Laandak: een huis op een zeshoek waar aan één hoek een driehoek af is (de voortuin achter een haakse gevel, zoals het
        // laanhuis). Schilddak met overal dezelfde helling (de rechte skeletlijnen van die vijfhoek, uitgerekend voor straal R);
        // de punt van de voortuin volgt uit de vloer, dus draaien en spiegelen gaan vanzelf.
        void LaanRoof(ModLevel L, Vector2 c, float R, float yb)
        {
            Vector2 sum = Vector2.zero;
            foreach (var kv in L.f) { var cen = MV2.Centroid(MV2.TriVerts(kv.Key.Item1, kv.Key.Item2, kv.Key.Item3, ts)); if ((cen - c).magnitude < MV2.R) sum += cen - c; }
            int k0 = 0; float bd = -1e9f; for (int k = 0; k < 6; k++) { float an = (30f + 60f * k) * Mathf.Deg2Rad; float d = -Vector2.Dot(sum, new Vector2(Mathf.Cos(an), Mathf.Sin(an))); if (d > bd) { bd = d; k0 = k; } }
            float a0 = (30f + 60f * k0) * Mathf.Deg2Rad; Vector2 u = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)), v = new Vector2(-u.y, u.x);
            const float slope = 0.75f; float a = R * MV2.SQ3 / 2f, t = R / 2f;
            // de voet van het dak ligt EAVE buiten de muren (overstek): elke hoek langs zijn deellijn naar buiten, het dakvlak loopt
            // gewoon door en komt daar EAVE × helling lager uit; de nok en de hoogste punt blijven waar ze waren
            var bp = new[] { new Vector2(-a, t), new Vector2(a, t), new Vector2(a, -t), new Vector2(0f, -R), new Vector2(-a, -t) };
            var Vb = new Vector3[5]; float ye = yb - EAVE * slope;
            for (int k = 0; k < 5; k++) { var o = OutCorner(bp[(k + 4) % 5], bp[k], bp[(k + 1) % 5], EAVE); Vb[k] = U(c + v * o.x + u * o.y, ye); }
            Vector3 Pt(float x, float y, float h) { var p = c + v * x + u * y; return U(p, yb + h * slope); }
            Vector3 A = Vb[0], B = Vb[1], C = Vb[2], D = Vb[3], E = Vb[4];
            Vector3 R1 = Pt(-0.2887f * R, -0.0774f * R, 0.5774f * R), R2 = Pt(0.2887f * R, -0.0774f * R, 0.5774f * R), X = Pt(0f, -0.244f * R, 0.744f * R);
            var rf = M("roof"); void Tr(Vector3 p, Vector3 q, Vector3 r) => rf.Tri(p, q, r, Vector3.up);
            Tr(A, B, R2); Tr(A, R2, X); Tr(A, X, R1);   // voorkant (de gevel aan de voortuin)
            Tr(E, A, R1); Tr(B, C, R2);                 // de zijkanten
            Tr(D, E, R1); Tr(D, R1, X); Tr(C, D, X); Tr(C, X, R2);   // de achterkant
            Eaves(Vb, c);
        }
        const float EAVE = 0.45f, FASCIA = 0.2f;
        // een hoek van een bolle veelhoek EAVE naar buiten, langs de deellijn (zodat elke zijde precies d naar buiten schuift)
        static Vector2 OutCorner(Vector2 prev, Vector2 p, Vector2 next, float d)
        {
            Vector2 u1 = (prev - p).normalized, u2 = (next - p).normalized, bis = (u1 + u2).normalized; float sh = Mathf.Abs(u1.x * bis.y - u1.y * bis.x);
            return p - bis * (d / Mathf.Max(0.2f, sh));
        }
        // de rand van een dak met overstek: een boeiboord (lijst) langs de voet en een vlakke onderkant terug naar de muren
        void Eaves(Vector3[] vb, Vector2 c)
        {
            var fb = M("lijst"); var under = M("ceiling"); Vector3 down = new Vector3(0f, FASCIA, 0f), mid = Vector3.zero; foreach (var p in vb) mid += p; mid /= vb.Length;
            for (int k = 0; k < vb.Length; k++)
            {
                Vector3 p = vb[k], q = vb[(k + 1) % vb.Length], outw = (p + q) * 0.5f - mid; outw.y = 0f;
                fb.Tri(p, q, q - down, outw); fb.Tri(p, q - down, p - down, outw);
                under.Tri(mid - down, p - down, q - down, Vector3.down);
            }
        }

        // --- voorwerpen ---
        void Obj(ModObj O, float y0)
        {
            float cr = Mathf.Cos(O.rot), sr = Mathf.Sin(O.rot);
            Vector3 X(Vector3 p) => new Vector3(O.x + p.x * cr - p.z * sr, y0 + p.y, O.z + p.x * sr + p.z * cr);
            void Bx(string k, float sx, float sy, float sz, float x, float y, float z, Quaternion? q = null)
            {
                var P = BoxP(sx, sy, sz); for (int i = 0; i < P.Count; i++) { var p = q.HasValue ? q.Value * P[i] : P[i]; P[i] = X(p + new Vector3(x, y, z)); } Solid(k, P, BoxF);
            }
            void Cl(string k, float rt, float rb, float h, float x, float y, float z, int n = 8) => Cyl(k, new Vector2(x, z), rt, rb, y, h, n, p => X(p));
            void Ic(string k, float r, Vector3 sc, float x, float y, float z) => Ico(k, new Vector3(x, y, z), r, sc, p => X(p));
            int hj = (Mathf.RoundToInt(O.x * 7f) * 31 + Mathf.RoundToInt(O.z * 13f) * 17) & 1023;
            string C(string hex) => "c_" + hex.TrimStart('#');
            switch (O.t)
            {
                case "tree": { float sc = 0.8f + (hj % 4) * 0.08f; Cl("trunk", 0.18f, 0.26f, 2.8f, 0, 0, 0, 6); Ic("leaf" + hj % 3, 2.1f * sc, Vector3.one, 0, 2.8f + 1.6f * sc, 0); break; }
                case "bush": Ic("leaf" + (hj + 1) % 3, 0.9f, new Vector3(1f, 0.8f, 1f), 0, 0.7f, 0); break;
                case "bench": Bx("bench", 1.8f, 0.06f, 0.45f, 0, 0.45f, 0); Bx("bench", 1.8f, 0.45f, 0.06f, 0, 0.72f, -0.22f); Bx("metal", 0.06f, 0.45f, 0.4f, -0.8f, 0.22f, 0); Bx("metal", 0.06f, 0.45f, 0.4f, 0.8f, 0.22f, 0); break;
                case "lamp": Cl("lamp", 0.05f, 0.08f, 4f, 0, 0, 0, 6); Cl("lamp", 0.28f, 0.12f, 0.18f, 0, 3.82f, 0, 6); Ic("glow", 0.22f, Vector3.one, 0, 4.05f, 0); break;
                case "pillar": Cl("w_stone", 0.3f, 0.35f, LH, 0, 0, 0, 6); Bx("w_stone", 0.9f, 0.12f, 0.9f, 0, LH - 0.06f, 0); break;
                case "hpillar": { float ph = (O.hasH ? O.h : 1f) * LH; Cl("slab", 0.45f, 0.5f, ph, 0, 0, 0, 6); break; }
                case "table": Cl("metal", 0.04f, 0.06f, 0.72f, 0, 0, 0, 6); Cl("bench", 0.62f, 0.62f, 0.04f, 0, 0.72f, 0, 6); for (int i = 0; i < 3; i++) { float an = i * Mathf.PI * 2f / 3f; Cl("metal", 0.2f, 0.2f, 0.45f, Mathf.Cos(an) * 0.9f, 0, Mathf.Sin(an) * 0.9f, 6); } break;
                case "planter":
                    Cl("slab", 0.78f, 0.68f, 0.5f, 0, 0, 0, 6); Ic("leaf2", 0.55f, new Vector3(1.2f, 0.7f, 1.2f), 0, 0.75f, 0);
                    { string[] fc = { "e64a3c", "f2b233", "e9e6dc", "d05a9a", "f28c33" }; for (int i = 0; i < 5; i++) { float an = i * 1.26f; Ic("c_" + fc[i], 0.09f, Vector3.one, Mathf.Cos(an) * 0.4f, 0.95f, Mathf.Sin(an) * 0.4f); } }
                    break;
                case "fountain": Cl("w_stone", 1.9f, 2f, 0.5f, 0, 0, 0, 6); Cl("water", 1.68f, 1.68f, 0.1f, 0, 0.42f, 0, 6); Cl("w_stone", 0.12f, 0.18f, 1.3f, 0, 0.4f, 0, 6); Cl("w_stone", 0.62f, 0.52f, 0.12f, 0, 1.6f, 0, 6); break;
                case "bin": Cl("bin", 0.26f, 0.23f, 0.9f, 0, 0, 0, 6); break;
                case "shelf":
                    Bx("w_panel", 2.4f, 1.9f, 0.5f, 0, 0.95f, 0); foreach (float y in new[] { 0.45f, 0.95f, 1.45f }) Bx("slab", 2.3f, 0.04f, 0.46f, 0, y, 0.02f);
                    { string[] cc = { "d94b3a", "3a7fd9", "e9c04a", "48a35a", "e6e1d8" }; for (int i = 0; i < 6; i++) Bx("c_" + cc[(hj + i * 37) % 5], 0.32f, 0.26f, 0.3f, -1f + i * 0.4f, 0.45f + 0.5f * (i % 3) + 0.13f, 0.04f); }
                    break;
                case "counter": Bx("w_slats", 2.2f, 1f, 0.7f, 0, 0.5f, 0); Bx("slab", 2.3f, 0.05f, 0.8f, 0, 1.02f, 0); Bx("metal", 0.35f, 0.3f, 0.3f, 0.6f, 1.2f, 0); break;
                case "sign": { Cl("metal", 0.05f, 0.06f, 2.6f, 0, 0, 0, 6); Bx("metal", 1.6f, 0.6f, 0.08f, 0, 2.6f, 0); string[] sc = { "ff4f6d", "39b6ff", "ffd23f", "5de07a" }; Bx("c_" + sc[hj % 4], 1.5f, 0.5f, 0.1f, 0, 2.6f, 0); break; }
                case "bike": for (int i = -2; i <= 2; i++) Bx("metal", 0.04f, 0.6f, 0.6f, i * 0.5f, 0.3f, 0); Bx("metal", 2.3f, 0.04f, 0.04f, 0, 0.6f, 0.28f); Bx("metal", 2.3f, 0.04f, 0.04f, 0, 0.6f, -0.28f); break;
                case "pump": Bx(C("e9ece8"), 0.9f, 1.5f, 0.55f, 0, 0.75f, 0); Bx(C("d9412e"), 0.95f, 0.35f, 0.6f, 0, 1.55f, 0); Bx(C("2b3138"), 0.5f, 0.35f, 0.05f, 0, 1f, 0.3f); Bx(C("2b3138"), 0.06f, 0.6f, 0.06f, 0.52f, 0.8f, 0.1f); break;
                case "charger": Bx(C("2f3a44"), 0.32f, 1.35f, 0.25f, 0, 0.68f, 0); Bx("glow", 0.2f, 0.05f, 0.27f, 0, 1.15f, 0); Bx(C("1c2228"), 0.05f, 0.4f, 0.05f, 0.2f, 0.7f, 0.1f); break;
                case "solar":
                    { var q = Quaternion.Euler(-0.42f * Mathf.Rad2Deg, 0, 0); Bx(C("203a63"), 3.4f, 0.05f, 1.6f, 0, 0.95f, 0, q); Bx(C("c9ced3"), 3.42f, 0.02f, 0.04f, 0, 0.98f, 0, q);
                      foreach (float x in new[] { -1.4f, 1.4f }) { Bx("metal", 0.06f, 1.3f, 0.06f, x, 0.65f, -0.55f); Bx("metal", 0.06f, 0.6f, 0.06f, x, 0.3f, 0.55f); } break; }
                case "turbine":
                    { string wt = C("eef0ee"); Cl(wt, 0.35f, 0.65f, 22f, 0, 0, 0, 6); Bx(wt, 1f, 1f, 2.6f, 0, 22f, 0.2f); Cl(wt, 0.3f, 0.45f, 0.6f, 0, 21.7f, 1.75f, 6);
                      for (int k = 0; k < 3; k++) { var q = Quaternion.Euler(0, 0, (k * Mathf.PI * 2f / 3f + hj * 0.01f) * Mathf.Rad2Deg); var P = BoxP(0.45f, 9.5f, 0.12f); for (int i = 0; i < P.Count; i++) P[i] = X(q * (P[i] + new Vector3(0, 4.9f, 0)) + new Vector3(0, 22f, 1.65f)); Solid(wt, P, BoxF); }
                      break; }
                case "chimney": Cl("w_brick", 0.9f, 1.3f, 16f, 0, 0, 0, 6); Cl(C("3a3a38"), 0.95f, 0.95f, 0.5f, 0, 16f, 0, 6); break;
                case "container": { string[] cc = { "3f7d4f", "2f5f9a", "7d7f80", "a8552f" }; Bx("c_" + cc[hj % 4], 2.4f, 1.3f, 1.4f, 0, 0.7f, 0); Bx(C("2a2e30"), 2.45f, 0.08f, 1.45f, 0, 1.38f, 0); break; }
                case "goal": { string wg = C("f3f4f1"); foreach (float x in new[] { -1.8f, 1.8f }) Bx(wg, 0.1f, 2f, 0.1f, x, 1f, 0); Bx(wg, 3.7f, 0.1f, 0.1f, 0, 2f, 0); Bx("net", 3.6f, 2f, 0.02f, 0, 1f, -0.5f, Quaternion.Euler(0.5f * Mathf.Rad2Deg, 0, 0)); break; }
                case "grave": Bx("w_stone", 0.65f, 0.85f, 0.14f, 0, 0.42f, -0.6f); Bx(C("8a8f86"), 0.75f, 0.08f, 1.7f, 0, 0.04f, 0.2f); break;
                case "boat": { string[] hc = { "f1f1ec", "2f5f9a", "a8402f" }; string hull = "c_" + hc[hj % 3]; Bx(hull, 4f, 0.6f, 1.6f, 0, 0.05f, 0); Bx(hull, 3.4f, 0.25f, 1.3f, 0, -0.35f, 0); Bx(C("9b6a3c"), 3.6f, 0.04f, 1.4f, 0, 0.36f, 0); Bx(C("eef0ee"), 1.4f, 0.8f, 1.1f, -0.6f, 0.78f, 0); Bx("w_glass", 1.42f, 0.3f, 1.12f, -0.6f, 0.95f, 0); break; }
                case "shelter": Bx("w_glass", 3f, 2.2f, 0.04f, 0, 1.15f, 0); foreach (float x in new[] { -1.5f, 1.5f }) Bx("metal", 0.08f, 2.4f, 0.08f, x, 1.2f, 0); Bx(C("2b3138"), 3.3f, 0.1f, 1.5f, 0, 2.45f, 0.55f); Bx("bench", 2.2f, 0.06f, 0.4f, 0, 0.5f, 0.3f); Bx("glow", 0.5f, 0.5f, 0.05f, 1.35f, 2.15f, 0.05f); break;
                case "slide": Bx("metal", 0.06f, 1.6f, 0.06f, -0.5f, 0.8f, -0.4f); Bx("metal", 0.06f, 1.6f, 0.06f, 0.5f, 0.8f, -0.4f); Bx(C("f2b233"), 1.1f, 0.08f, 0.9f, 0, 1.6f, -0.4f); Bx(C("e8453c"), 0.7f, 0.06f, 2.8f, 0, 0.85f, 0.75f, Quaternion.Euler(0.62f * Mathf.Rad2Deg, 0, 0)); break;
                case "swing":
                    foreach (float x in new[] { -1.3f, 1.3f }) foreach (float z in new[] { -0.6f, 0.6f }) Bx("metal", 0.08f, 2.4f, 0.08f, x, 1.15f, z * 0.6f, Quaternion.Euler((z > 0 ? -0.25f : 0.25f) * Mathf.Rad2Deg, 0, 0));
                    Bx("metal", 2.7f, 0.08f, 0.08f, 0, 2.3f, 0); foreach (float x in new[] { -0.6f, 0.6f }) { Bx(C("2b3138"), 0.03f, 1.8f, 0.03f, x - 0.2f, 1.4f, 0); Bx(C("2b3138"), 0.03f, 1.8f, 0.03f, x + 0.2f, 1.4f, 0); Bx(C("e8453c"), 0.5f, 0.05f, 0.25f, x, 0.5f, 0); }
                    break;
                case "sheep": Ic(C("ecebe2"), 0.42f, new Vector3(1.5f, 1f, 1f), 0, 0.65f, 0); Ic(C("2b2b28"), 0.18f, new Vector3(1.2f, 1f, 1f), 0.68f, 0.78f, 0); foreach (var (x, z) in new[] { (-0.35f, -0.2f), (-0.35f, 0.2f), (0.35f, -0.2f), (0.35f, 0.2f) }) Bx(C("2b2b28"), 0.08f, 0.4f, 0.08f, x, 0.2f, z); break;
                case "car":
                    { string[] cc = { "c8372d", "2f5f9a", "e9ece8", "2b3138", "3f7d4f", "d9a12e" }; string ck = "c_" + cc[hj % 6]; Bx(ck, 4.2f, 0.7f, 1.8f, 0, 0.55f, 0); Bx(ck, 2.2f, 0.6f, 1.6f, -0.2f, 1.15f, 0); Bx("w_glass", 2.22f, 0.45f, 1.62f, -0.2f, 1.17f, 0);
                      foreach (float x in new[] { -1.35f, 1.35f }) foreach (float z in new[] { -0.85f, 0.85f }) { var q = Quaternion.Euler(90f, 0, 0); Cyl(C("151719"), Vector2.zero, 0.34f, 0.34f, -0.12f, 0.24f, 10, p => X(q * p + new Vector3(x, 0.34f, z))); }
                      break; }
                case "bed": { string[] bc = { "4a6fa5", "b55a4a", "5f8f62" }; Bx("bench", 2f, 0.35f, 1.6f, 0, 0.18f, 0); Bx(C("f2f1ec"), 1.9f, 0.2f, 1.5f, 0.05f, 0.45f, 0); Bx("c_" + bc[hj % 3], 1.2f, 0.08f, 1.52f, 0.35f, 0.58f, 0); Bx("bench", 0.1f, 0.9f, 1.6f, -1f, 0.45f, 0); break; }
                case "sofa": { string[] sc = { "556b7a", "8a5a44", "3f5f4a" }; string k = "c_" + sc[hj % 3]; Bx(k, 2f, 0.45f, 0.9f, 0, 0.22f, 0); Bx(k, 2f, 0.5f, 0.22f, 0, 0.7f, -0.34f); foreach (float x in new[] { -0.95f, 0.95f }) Bx(k, 0.18f, 0.6f, 0.9f, x, 0.3f, 0); break; }
                case "desk": Bx(C("c9b28a"), 1.6f, 0.05f, 0.8f, 0, 0.74f, 0); foreach (float x in new[] { -0.75f, 0.75f }) foreach (float z in new[] { -0.35f, 0.35f }) Bx("metal", 0.04f, 0.72f, 0.04f, x, 0.36f, z); Bx(C("1c2228"), 0.6f, 0.38f, 0.04f, 0, 1f, -0.25f); Bx(C("8fc3e8"), 0.55f, 0.32f, 0.01f, 0, 1f, -0.22f); break;
                case "kitchen": Bx(C("f1f1ec"), 2.4f, 0.88f, 0.6f, 0, 0.44f, 0); Bx(C("2b3138"), 2.45f, 0.04f, 0.64f, 0, 0.9f, 0); Bx(C("c9ced3"), 0.5f, 0.02f, 0.4f, -0.6f, 0.93f, 0); Bx(C("f1f1ec"), 2.4f, 0.6f, 0.35f, 0, 1.75f, -0.12f); break;
                case "screen": Bx(C("f4f4f0"), 6f, 3f, 0.06f, 0, 2f, 0); Bx(C("1c1c1c"), 6.2f, 3.2f, 0.04f, 0, 2f, -0.04f); break;
                default: Bx("metal", 0.5f, 0.5f, 0.5f, 0, 0.25f, 0); break;
            }
        }
    }
}

// laat de verlichte ruiten van alle modules meegaan met de dag en de nacht (zoals de etalages in DayNight)
public class ModuleLights : MonoBehaviour
{
    void Update()
    {
        float n = Mathf.Clamp01(DayNight.Night * 1.6f + Weather.Cloud * 0.15f);
        ModuleMesh.Mat("lit").color = Color.Lerp(ModuleMesh.LitDay, ModuleMesh.LitNight, n);
    }
}
