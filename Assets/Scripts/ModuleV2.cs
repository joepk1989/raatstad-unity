using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Modules v2, zoals in de webversie (08c-modules-v2.js): een gebied van 1 of 7 zeshoekvakken (straal k = 0 of 1) met
// verdiepingen van 3 m. Alles ligt op het fijne driehoekraster (zijde R/n, standaard 9/4 = 2,25 m): vloeren zijn driehoeken,
// muren liggen op ribben tussen twee rasterpunten, trappen lopen van een rasterpunt in een van zes richtingen omhoog.
//
//   { v: 2, id, name, cat, k, n, lv: [ { f: { 'i,j,t': vloer }, w: { 'i,j,d': { m, o } }, s: [ { a: [i, j], d, n, w, k, side, off } ],
//                                        r: { 'q,r': 'hip' }, o: [ { t, x, z, rot } ] } ], edges, cells, tris, paths, codes }
//
// Rasterpunt (i, j) ligt op P(i, j) = (i·ts·√3/2, i·ts/2 + j·ts), in het lokale vlak (x, z) van de webversie. Het JSON-formaat
// blijft precies gelijk, zodat modules uit de webversie hier direct werken (en andersom).
public class ModWall { public string m = "brick", o = ""; public ModWall Copy() => new ModWall { m = m, o = o }; }
public class ModStair
{
    public int ai, aj, d; public float n = 2f, w = 2f, h, off; public string k = ""; public int side; public bool hasSide; public int q, r;
    public int Side => hasSide ? side : 1;   // wenteltrap en bordestrap: zonder side is het 1
    public ModStair Copy() => (ModStair)MemberwiseClone();
}
public class ModObj { public string t = "bench"; public float x, z, rot, h; public bool hasH; public ModObj Copy() => (ModObj)MemberwiseClone(); }
public class ModLevel
{
    public Dictionary<(int, int, int), string> f = new Dictionary<(int, int, int), string>();
    public Dictionary<(int, int, int), ModWall> w = new Dictionary<(int, int, int), ModWall>();
    public List<ModStair> s = new List<ModStair>();
    public Dictionary<(int, int), string> r = new Dictionary<(int, int), string>();
    public List<ModObj> o = new List<ModObj>();
    public bool Empty => f.Count == 0 && w.Count == 0 && s.Count == 0 && r.Count == 0 && o.Count == 0;
    public ModLevel Copy()
    {
        var L = new ModLevel { f = new Dictionary<(int, int, int), string>(f), r = new Dictionary<(int, int), string>(r) };
        foreach (var kv in w) L.w[kv.Key] = kv.Value.Copy(); foreach (var S in s) L.s.Add(S.Copy()); foreach (var O in o) L.o.Add(O.Copy());
        return L;
    }
}

public class ModuleV2
{
    public string id = "", name = "Nieuwe module", cat = "Overig"; public int k, n = 4;
    public List<ModLevel> lv = new List<ModLevel> { new ModLevel() };
    public Dictionary<string, string> codes;
    public Dictionary<string, object> extra = new Dictionary<string, object>();   // velden die we niet zelf gebruiken (edges, cells, was, lib …): blijven bewaard
    public int ver; public bool mir, user;   // alleen in het spel: versie (bij elke wijziging hoger), gespiegelde kopie, door de speler bewaard

    public ModuleV2 Copy()
    {
        var m = (ModuleV2)MemberwiseClone(); m.lv = new List<ModLevel>(); foreach (var L in lv) m.lv.Add(L.Copy());
        m.codes = codes != null ? new Dictionary<string, string>(codes) : null; m.extra = new Dictionary<string, object>(extra); return m;
    }

    // ---------- JSON ----------
    static (int, int, int) K3(string s) { var p = s.Split(','); return p.Length >= 3 ? (int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2])) : (0, 0, 0); }
    static (int, int) K2(string s) { var p = s.Split(','); return p.Length >= 2 ? (int.Parse(p[0]), int.Parse(p[1])) : (0, 0); }
    public static ModuleV2 FromJson(string json) => FromObj(ModuleJson.Parse(json) as Dictionary<string, object>);
    public static ModuleV2 FromObj(Dictionary<string, object> d)
    {
        if (d == null) return null;
        var m = new ModuleV2 { lv = new List<ModLevel>() };
        foreach (var kv in d)
        {
            switch (kv.Key)
            {
                case "v": break;
                case "id": m.id = ModuleJson.S(kv.Value, "") ?? ""; break;
                case "name": m.name = ModuleJson.S(kv.Value, "Module"); break;
                case "cat": m.cat = ModuleJson.S(kv.Value, "Overig"); break;
                case "k": m.k = Mathf.Clamp(ModuleJson.I(kv.Value), 0, 1); break;
                case "n": m.n = ModuleJson.I(kv.Value, 4); break;
                case "codes": if (kv.Value is Dictionary<string, object> cd) { m.codes = new Dictionary<string, string>(); foreach (var c in cd) m.codes[c.Key] = ModuleJson.S(c.Value, "leeg"); } break;
                case "lv":
                    if (kv.Value is List<object> ll) foreach (var lo in ll)
                    {
                        var L = new ModLevel(); m.lv.Add(L); if (!(lo is Dictionary<string, object> ld)) continue;
                        if (ld.TryGetValue("f", out var fo) && fo is Dictionary<string, object> fd) foreach (var f in fd) L.f[K3(f.Key)] = ModuleJson.S(f.Value, "concrete");
                        if (ld.TryGetValue("w", out var wo) && wo is Dictionary<string, object> wd) foreach (var w in wd) if (w.Value is Dictionary<string, object> wv) L.w[K3(w.Key)] = new ModWall { m = ModuleJson.S(wv.TryGetValue("m", out var mm) ? mm : null, "brick"), o = ModuleJson.S(wv.TryGetValue("o", out var oo) ? oo : null, "") };
                        if (ld.TryGetValue("r", out var ro) && ro is Dictionary<string, object> rd) foreach (var r in rd) L.r[K2(r.Key)] = ModuleJson.S(r.Value, "hip");
                        if (ld.TryGetValue("s", out var so) && so is List<object> sl) foreach (var sv in sl)
                        {
                            if (!(sv is Dictionary<string, object> sd)) continue; var S = new ModStair();
                            if (sd.TryGetValue("a", out var a) && a is List<object> al && al.Count >= 2) { S.ai = ModuleJson.I(al[0]); S.aj = ModuleJson.I(al[1]); }
                            object x;
                            if (sd.TryGetValue("d", out x)) S.d = ModuleJson.I(x); if (sd.TryGetValue("n", out x)) S.n = ModuleJson.F(x, 2f); if (sd.TryGetValue("w", out x)) S.w = ModuleJson.F(x, 2f);
                            if (sd.TryGetValue("k", out x)) S.k = ModuleJson.S(x, "") ?? ""; if (sd.TryGetValue("h", out x)) S.h = ModuleJson.F(x); if (sd.TryGetValue("off", out x)) S.off = ModuleJson.F(x);
                            if (sd.TryGetValue("side", out x) && x is double) { S.side = ModuleJson.I(x); S.hasSide = true; }
                            if (sd.TryGetValue("q", out x)) S.q = ModuleJson.I(x); if (sd.TryGetValue("r", out x)) S.r = ModuleJson.I(x);
                            L.s.Add(S);
                        }
                        if (ld.TryGetValue("o", out var oo2) && oo2 is List<object> ol) foreach (var ov in ol)
                        {
                            if (!(ov is Dictionary<string, object> od)) continue; var O = new ModObj(); object x;
                            if (od.TryGetValue("t", out x)) O.t = ModuleJson.S(x, "bench"); if (od.TryGetValue("x", out x)) O.x = ModuleJson.F(x); if (od.TryGetValue("z", out x)) O.z = ModuleJson.F(x);
                            if (od.TryGetValue("rot", out x)) O.rot = ModuleJson.F(x); if (od.TryGetValue("h", out x)) { O.h = ModuleJson.F(x); O.hasH = true; }
                            L.o.Add(O);
                        }
                    }
                    break;
                default: m.extra[kv.Key] = kv.Value; break;
            }
        }
        if (m.lv.Count == 0) m.lv.Add(new ModLevel());
        if (m.n != 1 && m.n != 2 && m.n != 4 && m.n != 8) m.n = 4;
        return m;
    }
    public string ToJson()
    {
        var d = new Dictionary<string, object> { ["v"] = 2, ["id"] = id, ["name"] = name, ["cat"] = cat, ["k"] = k, ["n"] = n };
        var lvs = new List<object>();
        foreach (var L in lv)
        {
            var f = new Dictionary<string, object>(); foreach (var kv in L.f) f[kv.Key.Item1 + "," + kv.Key.Item2 + "," + kv.Key.Item3] = kv.Value;
            var w = new Dictionary<string, object>(); foreach (var kv in L.w) w[kv.Key.Item1 + "," + kv.Key.Item2 + "," + kv.Key.Item3] = new Dictionary<string, object> { ["m"] = kv.Value.m, ["o"] = kv.Value.o ?? "" };
            var s = new List<object>();
            foreach (var S in L.s)
            {
                var sd = new Dictionary<string, object> { ["a"] = new List<object> { S.ai, S.aj }, ["d"] = S.d, ["n"] = S.n, ["w"] = S.w };
                if (!string.IsNullOrEmpty(S.k)) sd["k"] = S.k; if (S.hasSide) sd["side"] = S.side; if (S.off != 0f) sd["off"] = S.off; if (S.h != 0f) sd["h"] = S.h;
                if (S.k == "hexramp") { sd["q"] = S.q; sd["r"] = S.r; }
                s.Add(sd);
            }
            var r = new Dictionary<string, object>(); foreach (var kv in L.r) r[kv.Key.Item1 + "," + kv.Key.Item2] = kv.Value;
            var o = new List<object>();
            foreach (var O in L.o) { var od = new Dictionary<string, object> { ["t"] = O.t, ["x"] = O.x, ["z"] = O.z, ["rot"] = O.rot }; if (O.hasH) od["h"] = O.h; o.Add(od); }
            lvs.Add(new Dictionary<string, object> { ["f"] = f, ["w"] = w, ["s"] = s, ["r"] = r, ["o"] = o });
        }
        d["lv"] = lvs;
        foreach (var key in new[] { "edges", "cells", "tris", "paths" }) d[key] = extra.TryGetValue(key, out var ev) ? ev : new List<object>();
        var cd = new Dictionary<string, object>(); foreach (var kv in MV2.Codes(this)) cd[kv.Key] = kv.Value; d["codes"] = cd;
        foreach (var kv in extra) if (!d.ContainsKey(kv.Key)) d[kv.Key] = kv.Value;
        return ModuleJson.Write(d);
    }
}

// Maten, raster en regels van modules v2 (gedeeld door het bouwen, de stad en de Raatmaker)
public static class MV2
{
    public const float LV_H = 3f, WALL_T = 0.25f, FLOOR_T = 0.2f, R = 9f; public const int MAX_LV = 16;   // hooguit 16 lagen van 3 m (48 m); de webversie had 8
    public static readonly float SQ3 = Mathf.Sqrt(3f), INNER = 9f * Mathf.Sqrt(3f) / 2f;
    public static readonly Vector2Int[] DIRW = { new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 1), new Vector2Int(2, -1), new Vector2Int(1, 1), new Vector2Int(-1, 2) };
    public static readonly Vector2Int[] DIR6 = { new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 1), new Vector2Int(-1, 0), new Vector2Int(0, -1), new Vector2Int(1, -1) };
    public static readonly Vector2Int[] DIR12 = { new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 1), new Vector2Int(-1, 0), new Vector2Int(0, -1), new Vector2Int(1, -1), new Vector2Int(2, -1), new Vector2Int(1, 1), new Vector2Int(-1, 2), new Vector2Int(-2, 1), new Vector2Int(-1, -1), new Vector2Int(1, -2) };
    public static readonly Vector2Int[] NEIGH = { new Vector2Int(1, 0), new Vector2Int(1, -1), new Vector2Int(0, -1), new Vector2Int(-1, 0), new Vector2Int(-1, 1), new Vector2Int(0, 1) };
    // zijde s van een vak: de normaal wijst naar NEIGH[s] (n), t loopt langs de rand
    public static Vector2 FN(int s) { float a = -s * Mathf.PI / 3f; return new Vector2(Mathf.Cos(a), Mathf.Sin(a)); }
    public static Vector2 FT(int s) { float a = -s * Mathf.PI / 3f; return new Vector2(Mathf.Sin(a), -Mathf.Cos(a)); }

    public static readonly string[] Floors = { "paver", "tile", "wood", "parquet", "carpet", "concrete", "polished", "stonetile", "grass", "water", "asphalt", "roof", "glass" };
    public static readonly string[] FloorNames = { "Klinkers", "Stoeptegels", "Houten vloer", "Visgraat parket", "Kantoorvloer", "Beton", "Gepolijst beton", "Natuursteen tegels", "Gras", "Water", "Asfalt", "Dakbedekking", "Glazen dak" };
    public static readonly string[] Walls = { "brick", "darkbrick", "plaster", "concrete", "panel", "slats", "corten", "curtain", "glass", "wood", "stone", "hedge", "fence" };
    public static readonly string[] WallNames = { "Baksteen", "Donkere steen", "Wit stucwerk", "Schoon beton", "Gevelpanelen", "Houten lamellen", "Cortenstaal", "Glasgevel", "Glaswand", "Hout", "Natuursteen", "Haag", "Hek" };
    public static readonly string[] Objects = { "tree", "bush", "bench", "lamp", "pillar", "table", "planter", "fountain", "bin", "bike", "shelf", "counter", "sign", "bed", "sofa", "desk", "kitchen", "screen", "car", "pump", "charger", "solar", "turbine", "chimney", "container", "goal", "grave", "boat", "shelter", "slide", "swing", "sheep" };
    public static readonly string[] ObjectNames = { "Boom", "Struik", "Bankje", "Lantaarn", "Zuil", "Tafel", "Plantenbak", "Fontein", "Prullenbak", "Fietsenrek", "Winkelstelling", "Toonbank", "Lichtreclame", "Bed", "Bank (binnen)", "Bureau", "Keukenblok", "Filmdoek", "Auto", "Tankpomp", "Laadpaal", "Zonnepanelen", "Windmolen", "Schoorsteen", "Container", "Doel", "Grafsteen", "Boot", "Abri", "Glijbaan", "Schommel", "Schaap" };
    public static readonly string[] Cats = { "Wonen", "Werken", "Winkels", "Horeca", "Winkelcentrum", "Openbaar", "Sport", "Vervoer", "Voorzieningen", "Groen", "Pleinen", "Verbindingen", "Bouwdelen", "Overig" };
    public static string NameOf(string[] keys, string[] names, string k) { int i = Array.IndexOf(keys, k); return i >= 0 ? names[i] : k; }

    // kleuren van de materialen (COL in de Raatmaker van de webversie)
    public static Color Col(string k)
    {
        switch (k)
        {
            case "paver": return Hex("#8a5141"); case "tile": return Hex("#cbc8bd"); case "wood": return Hex("#a97c52"); case "carpet": return Hex("#8b9397"); case "concrete": return Hex("#c4c6c2");
            case "grass": return Hex("#6fa356"); case "water": return Hex("#2d5a66"); case "asphalt": return Hex("#3c4045"); case "roof": return Hex("#5a5d59"); case "brick": return Hex("#9a5b45");
            case "plaster": return Hex("#ece8e0"); case "stone": return Hex("#bdb5a5"); case "hedge": return Hex("#5a9440"); case "fence": return Hex("#3b4248"); case "glass": return Hex("#bfe0f0");
            case "darkbrick": return Hex("#3d3a36"); case "panel": return Hex("#4b5157"); case "slats": return Hex("#a8743f"); case "corten": return Hex("#97512e"); case "curtain": return Hex("#6d8ea6");
            case "parquet": return Hex("#9a6a3e"); case "polished": return Hex("#c9c8c3"); case "stonetile": return Hex("#d9d0bf");
        }
        return new Color(0.7f, 0.7f, 0.68f);
    }
    public static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    public static float Ts(ModuleV2 m) => R / (m.n == 1 || m.n == 2 || m.n == 4 || m.n == 8 ? m.n : 4);
    public static Vector2 P(int i, int j, float ts) => new Vector2(i * ts * SQ3 / 2f, i * ts / 2f + j * ts);
    public static Vector2[] TriVerts(int i, int j, int t, float ts)
    {
        Vector2 A = P(i, j, ts), B = P(i + 1, j, ts), C = P(i, j + 1, ts), D = P(i + 1, j + 1, ts);
        return t != 0 ? new[] { B, D, C } : new[] { A, B, C };
    }
    public static Vector2 Centroid(Vector2[] v) => (v[0] + v[1] + v[2]) / 3f;
    public static int HexDist(int q, int r) => Mathf.Max(Mathf.Abs(q), Mathf.Max(Mathf.Abs(r), Mathf.Abs(-q - r)));
    public static Vector2Int CellOf(float x, float z)
    {
        float fq = (x * SQ3 / 3f - z / 3f) / R, fr = (2f / 3f * z) / R; int q = Mathf.RoundToInt(fq), r = Mathf.RoundToInt(fr), s = Mathf.RoundToInt(-fq - fr);
        float dq = Mathf.Abs(q - fq), dr = Mathf.Abs(r - fr), ds = Mathf.Abs(s + fq + fr);
        if (dq > dr && dq > ds) q = -r - s; else if (dr > ds) r = -q - s;
        return new Vector2Int(q, r);
    }
    public static Vector2 CellXZ(int q, int r) => new Vector2(R * SQ3 * (q + r / 2f), 1.5f * R * r);
    public static bool InArea(ModuleV2 m, float x, float z) { var c = CellOf(x, z); return HexDist(c.x, c.y) <= m.k; }
    // een rib op de buitenrand van het gebied ligt precies op de grens; die hoort erbij (marge 10 cm)
    public static bool NearArea(ModuleV2 m, float x, float z)
    {
        var c0 = CellOf(x, z); if (HexDist(c0.x, c0.y) <= m.k) return true;
        foreach (var d in DIR6)
        {
            int q = c0.x + d.x, r = c0.y + d.y; if (HexDist(q, r) > m.k) continue; var c = CellXZ(q, r); bool inside = true;
            for (int s = 0; s < 6; s++) { var f = FN(s); if ((x - c.x) * f.x + (z - c.y) * f.y > INNER + 0.1f) { inside = false; break; } }
            if (inside) return true;
        }
        return false;
    }
    public static List<(int q, int r, int s)> Boundary(int k)
    {
        var o = new List<(int, int, int)>();
        for (int q = -k; q <= k; q++) for (int r = -k; r <= k; r++) { if (HexDist(q, r) > k) continue; for (int s = 0; s < 6; s++) if (HexDist(q + NEIGH[s].x, r + NEIGH[s].y) > k) o.Add((q, r, s)); }
        return o;
    }
    public static List<Vector2Int> AreaCells(int k) { var o = new List<Vector2Int> { Vector2Int.zero }; if (k > 0) for (int s = 0; s < 6; s++) o.Add(NEIGH[s]); return o; }
    public static (Vector2 a, Vector2 b) WallSeg((int, int, int) id, float ts) { var D = DIRW[id.Item3]; return (P(id.Item1, id.Item2, ts), P(id.Item1 + D.x, id.Item2 + D.y, ts)); }

    // ---------- randcodes: per buitenrand wat er op de begane grond aan die rand ligt ----------
    public static Dictionary<string, string> Codes(ModuleV2 m)
    {
        var codes = new Dictionary<string, string>(); float ts = Ts(m); var L0 = m.lv[0];
        var walls = new List<(Vector2 p, string o, string m)>();
        foreach (var kv in L0.w) { if (kv.Key.Item3 < 0 || kv.Key.Item3 > 5) continue; var (a, b) = WallSeg(kv.Key, ts); walls.Add(((a + b) * 0.5f, kv.Value.o ?? "", kv.Value.m)); }
        var floors = new List<(Vector2 p, string m)>();
        foreach (var kv in L0.f) floors.Add((Centroid(TriVerts(kv.Key.Item1, kv.Key.Item2, kv.Key.Item3, ts)), kv.Value));
        foreach (var (q, r, s) in Boundary(m.k))
        {
            Vector2 c = CellXZ(q, r), fn = FN(s), ft = FT(s), mid = c + fn * INNER;
            bool Near(Vector2 p, float d) { var dd = p - mid; return Mathf.Abs(Vector2.Dot(dd, fn)) < d && Mathf.Abs(Vector2.Dot(dd, ft)) < R / 2f + 0.2f; }
            string code; var w = walls.FindAll(x => Near(x.p, 0.4f));
            if (w.Count > 0) code = w.Exists(x => x.o == "door" || x.o == "gate" || x.o == "open") ? "deur" : w.TrueForAll(x => x.m == "hedge") ? "groen" : "muur";
            else
            {
                var cnt = new Dictionary<string, int>(); var order = new List<string>();
                foreach (var f in floors) if (Near(f.p, 1.6f)) { string cd = f.m == "grass" ? "groen" : f.m == "water" ? "water" : f.m == "asphalt" ? "weg" : "stoep"; if (!cnt.ContainsKey(cd)) { cnt[cd] = 0; order.Add(cd); } cnt[cd]++; }
                code = "leeg"; int best = 0; foreach (var cd in order) if (cnt[cd] > best) { best = cnt[cd]; code = cd; }
            }
            codes[q + "," + r + "," + s] = code;
        }
        return codes;
    }

    // ---------- meta: wat de stad (schoonheid, AI, drukte, stapelen) over een module wil weten ----------
    public class Meta { public int fl, k, n; public float h; public string top; public bool stack, bouw; public int deur, weg; }
    static readonly HashSet<string> RoofGreen = new HashSet<string> { "tree", "bush", "planter", "solar" };
    public static bool RoofOnly(ModuleV2 m)
    {
        var L = m.lv; if (L.Count < 2) return false; var T = L[L.Count - 1];
        if (T.w.Count > 0 || T.s.Count > 0 || T.r.Count > 0 || T.f.Count == 0) return false;
        foreach (var o in T.o) if (!RoofGreen.Contains(o.t)) return false; return true;
    }
    static readonly Dictionary<ModuleV2, (int ver, Meta meta)> metaCache = new Dictionary<ModuleV2, (int, Meta)>();
    public static Meta MetaOf(ModuleV2 m)
    {
        if (m == null) return new Meta { top = "open" };
        if (metaCache.TryGetValue(m, out var c) && c.ver == m.ver) return c.meta;
        bool roofOnly = RoofOnly(m); int fl = m.lv.Count - (roofOnly ? 1 : 0);
        bool solid = false, kap = false; foreach (var L in m.lv) { foreach (var w in L.w.Values) if (w.m != "fence" && w.m != "hedge") solid = true; if (L.r.Count > 0) kap = true; }
        var cd = Codes(m); int deur = 0, weg = 0; foreach (var v in cd.Values) { if (v == "deur") deur++; else if (v == "weg") weg++; }
        string top = kap ? "kap" : roofOnly ? "plat" : "open";
        var meta = new Meta { fl = fl, k = m.k, n = m.k > 0 ? 7 : 1, h = fl * LV_H, top = top, stack = top == "plat", bouw = m.lv.Count > 1 || solid, deur = deur, weg = weg };
        metaCache[m] = (m.ver, meta); return meta;
    }

    // ---------- spiegelen: z → −z in de module, als gewone moduledata ----------
    // Rasterpunt P(i, j) wordt P(i, −i−j). Driehoek (i, j, 0) → (i, −i−j−1, 0), (i, j, 1) → (i, −i−j−2, 1). Een muur begint
    // gespiegeld (behalve richting 3) aan het andere eind. Trappen: richting d → 5 − d, off en side tegengesteld. Vakken (q, r) → (q + r, −r).
    public static ModuleV2 Mirror(ModuleV2 m0)
    {
        var m = m0.Copy(); int[] WMAP = { 2, 1, 0, 3, 5, 4 }; int[] D6 = { 5, 4, 3, 2, 1, 0 };
        foreach (var L in m.lv)
        {
            var f = new Dictionary<(int, int, int), string>(); foreach (var kv in L.f) { var (i, j, t) = kv.Key; f[(i, t != 0 ? -i - j - 2 : -i - j - 1, t)] = kv.Value; } L.f = f;
            var w = new Dictionary<(int, int, int), ModWall>();
            foreach (var kv in L.w)
            {
                var (i, j, d) = kv.Key; if (d < 0 || d > 5) continue; var D = DIRW[d];
                if (d == 3) w[(i, -i - j, 3)] = kv.Value; else { int a = i + D.x, b = j + D.y; w[(a, -a - b, WMAP[d])] = kv.Value; }
            }
            L.w = w;
            foreach (var S in L.s)
            {
                if (S.k == "hexramp") { int q = S.q, r = S.r; S.q = q + r; S.r = -r; S.side = (6 - S.side) % 6; continue; }
                int ai = S.ai, aj = S.aj; S.ai = ai; S.aj = -ai - aj; S.d = D6[Mathf.Clamp(S.d, 0, 5)]; if (S.off != 0f) S.off = -S.off;
                if (S.k == "spiral" || S.k == "u") { S.side = -S.Side; S.hasSide = true; } else if (S.hasSide && S.side != 0) S.side = -S.side;
            }
            var rr = new Dictionary<(int, int), string>();
            foreach (var kv in L.r) { var (q, r) = kv.Key; string v = kv.Value; if (v != null && v.StartsWith("gable") && v.Length > 5) v = "gable" + new[] { 2, 1, 0 }[(v[5] - '0') % 3]; rr[(q + r, -r)] = v; }
            L.r = rr;
            foreach (var o in L.o) { o.z = -o.z; o.rot = -o.rot; }
        }
        m.mir = true; return m;
    }

    // ---------- trapgat: welke vloerdriehoeken (op de verdieping boven trap S) weg moeten, en welk stuk blijft ----------
    // null: blijft; Hole met keep = halfvlak (nx, nz, c) waar x·nx + z·nz ≥ c blijft liggen (of null: helemaal weg);
    // fill: het stuk voorbij de bovenste tree wordt bordes (in het materiaal van de trap)
    public class Hole { public Vector3? keep, fill; }
    public static Func<Vector2[], Hole> StairHole(ModStair S, float ts)
    {
        var D = DIR6[Mathf.Clamp(S.d, 0, 5)]; Vector2 a = P(S.ai, S.aj, ts); float len = (S.n > 0 ? S.n : 2f) * ts, w = S.w > 0 ? S.w : 2f;
        float dx = D.x * ts * SQ3 / 2f, dz = D.x * ts / 2f + D.y * ts, dl = Mathf.Sqrt(dx * dx + dz * dz); if (dl < 1e-6f) dl = 1f;
        float ux = dx / dl, uz = dz / dl, nx = -uz, nz = ux, off = S.off;
        Vector2 Loc(Vector2 p) { float cx = p.x - a.x, cz = p.y - a.y; return new Vector2(cx * ux + cz * uz, cx * nx + cz * nz - off); }
        Vector3 KeepAl(float al0, float sign) => new Vector3(ux * sign, uz * sign, sign * (a.x * ux + a.y * uz + al0));
        if (S.k == "spiral")
        {
            float a0 = Mathf.Atan2(dz, dx), sd = S.Side;
            return v =>
            {
                var c = Centroid(v) - a; if (c.magnitude > 2.15f) return null;
                float ph = Mathf.Repeat(sd * (Mathf.Atan2(c.y, c.x) - a0), Mathf.PI * 2f);
                if (ph < Mathf.PI * 5f / 3f) return new Hole();
                float th = a0 + sd * Mathf.PI * 11f / 6f, nx2 = Mathf.Cos(th + sd * Mathf.PI / 2f), nz2 = Mathf.Sin(th + sd * Mathf.PI / 2f);
                return new Hole { keep = new Vector3(nx2, nz2, a.x * nx2 + a.y * nz2) };
            };
        }
        if (S.k == "u")
        {
            float sd = S.Side, c2 = sd * w, cut = 2f * w / SQ3, oA = -sd * w / 2f, oB = c2 + sd * w / 2f;
            Vector2 P0 = a + new Vector2(ux * (len + 0.25f) + nx * (oA + off), uz * (len + 0.25f) + nz * (oA + off)), P1 = a + new Vector2(ux * (len + 0.25f + cut) + nx * (oB + off), uz * (len + 0.25f + cut) + nz * (oB + off));
            Vector2 e = P1 - P0; float el = e.magnitude; if (el < 1e-6f) el = 1f; float qx = -e.y / el, qz = e.x / el; if (qx * ux + qz * uz < 0f) { qx = -qx; qz = -qz; }
            var beyond = new Vector3(qx, qz, P0.x * qx + P0.y * qz); float end = len + 0.25f + cut + 0.6f;
            return v =>
            {
                var l = Loc(Centroid(v)); float al = l.x, lat = l.y;
                if (Mathf.Abs(lat) < w / 2f + 0.05f && al > 0.3f * len && al < end) return new Hole { keep = beyond };
                if (Mathf.Abs(lat - c2) < w / 2f + 0.05f && al > -0.7f && al < end) return new Hole { keep = al < len / 2f ? KeepAl(0f, -1f) : beyond };
                return null;
            };
        }
        if (S.k == "stair" || S.k == "ramp" || S.k == "esc" || string.IsNullOrEmpty(S.k))
            return v =>
            {
                var l = Loc(Centroid(v)); float al = l.x, lat = l.y; if (!(al > 0.3f * len && al < len + 1.4f)) return null;
                float mn = 1e9f; foreach (var p in v) mn = Mathf.Min(mn, Loc(p).x); if (mn > len - 0.05f) return null;
                bool inBand = Mathf.Abs(lat) < w / 2f + 0.05f; if (!inBand) foreach (var p in v) if (Mathf.Abs(Loc(p).y) < w / 2f - 0.05f) { inBand = true; break; }
                return inBand ? new Hole { fill = KeepAl(len, 1f) } : null;
            };
        return v => null;
    }
    // een veelhoek afknippen op een halfvlak (x·nx + z·nz ≥ c)
    public static List<Vector2> ClipKeep(IList<Vector2> v, Vector3 k)
    {
        var o = new List<Vector2>(); float F(Vector2 p) => p.x * k.x + p.y * k.y - k.z;
        for (int i = 0; i < v.Count; i++)
        {
            Vector2 A = v[i], B = v[(i + 1) % v.Count]; float fa = F(A), fb = F(B);
            if (fa >= 0f) o.Add(A); if ((fa >= 0f) != (fb >= 0f)) { float s = fa / (fa - fb); o.Add(A + (B - A) * s); }
        }
        return o;
    }
    public static float TriArea2(Vector2 a, Vector2 b, Vector2 c) => Mathf.Abs((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)) / 2f;

    // straal van het dak op vak (q, r) van verdieping li: de kleinste zeshoek (in stappen van een rasterzijde) om de muren van dat vak heen
    public static float RoofFit(ModuleV2 m, int li, int q, int r)
    {
        var L = m.lv[li]; float ts = Ts(m); var c = CellXZ(q, r); float ap = 0f;
        float Hn(Vector2 p) { float mx = -1e9f; for (int k = 0; k < 6; k++) { float an = k * Mathf.PI / 3f; mx = Mathf.Max(mx, (p.x - c.x) * Mathf.Cos(an) + (p.y - c.y) * Mathf.Sin(an)); } return mx; }
        foreach (var kv in L.w)
        {
            if (kv.Value.m == "fence" || kv.Value.m == "hedge" || kv.Key.Item3 < 0 || kv.Key.Item3 > 5) continue; var (a, b) = WallSeg(kv.Key, ts);
            if (((a + b) * 0.5f - c).magnitude > R) continue; ap = Mathf.Max(ap, Mathf.Max(Hn(a), Hn(b)));
        }
        if (ap <= 0f) return R; return Mathf.Min(R, Mathf.Max(ts, Mathf.Ceil(ap / (SQ3 / 2f) / ts - 0.05f) * ts));
    }
}

// De bibliotheek: standaardmodules uit Assets/Resources/Modules (omgezet uit de webversie met Tools/bibliotheek_omzetten.js)
// en eigen modules in persistentDataPath/modules (een eigen module met dezelfde id gaat voor).
public static class ModuleLib
{
    static Dictionary<string, ModuleV2> all; static List<ModuleV2> list;
    static readonly Dictionary<string, ModuleV2> mirrored = new Dictionary<string, ModuleV2>();
    public static int Version;   // hoger bij elke bewaarde module (de lade en de stad kijken hiernaar)
    public static string UserDir => Path.Combine(Application.persistentDataPath, "modules");

    static void Ensure()
    {
        if (all != null) return;
        all = new Dictionary<string, ModuleV2>(); list = new List<ModuleV2>();
        var idx = Resources.Load<TextAsset>("Modules/index");
        if (idx != null) foreach (var line in idx.text.Split('\n'))
        {
            string id = line.Trim(); if (id.Length == 0) continue;
            var ta = Resources.Load<TextAsset>("Modules/" + id); if (ta == null) continue;
            try { var m = ModuleV2.FromJson(ta.text); if (m == null) continue; m.id = id; Add(m); }
            catch (Exception e) { Debug.LogWarning("Module " + id + " niet te lezen: " + e.Message); }
        }
        try
        {
            if (Directory.Exists(UserDir)) foreach (var f in Directory.GetFiles(UserDir, "*.json"))
            {
                try { var m = ModuleV2.FromJson(File.ReadAllText(f)); if (m == null) continue; m.id = Path.GetFileNameWithoutExtension(f); m.user = true; Add(m); }
                catch (Exception e) { Debug.LogWarning("Module " + f + " niet te lezen: " + e.Message); }
            }
        }
        catch (Exception e) { Debug.LogWarning("Eigen modules: " + e.Message); }
    }
    static void Add(ModuleV2 m)
    {
        if (all.TryGetValue(m.id, out var old)) { list[list.IndexOf(old)] = m; m.ver = old.ver + 1; } else list.Add(m);
        all[m.id] = m;
    }
    public static IReadOnlyList<ModuleV2> All { get { Ensure(); return list; } }
    public static ModuleV2 Get(string id) { Ensure(); return id != null && all.TryGetValue(id, out var m) ? m : null; }
    // de moduledata voor een exemplaar: gespiegeld als dat zo geplaatst is (per versie onthouden)
    public static ModuleV2 Data(string id, bool mir)
    {
        var m = Get(id); if (m == null || !mir) return m;
        if (mirrored.TryGetValue(id, out var g) && g.ver == m.ver) return g;
        g = MV2.Mirror(m); g.ver = m.ver; mirrored[id] = g; return g;
    }
    // bewaren als eigen module (overschrijft een standaardmodule met dezelfde id: alle exemplaren in de stad volgen)
    public static void Save(ModuleV2 m)
    {
        Ensure();
        if (string.IsNullOrEmpty(m.id)) m.id = NewId(m.name);
        try { Directory.CreateDirectory(UserDir); File.WriteAllText(Path.Combine(UserDir, m.id + ".json"), m.ToJson()); }
        catch (Exception e) { Debug.LogWarning("Module bewaren mislukt: " + e.Message); }
        var c = m.Copy(); c.user = true; c.mir = false; Add(c); mirrored.Remove(m.id); Version++;
    }
    public static string NewId(string name)
    {
        Ensure(); string b = "eigen-" + Slug(name); string id = b; int n = 2; while (all.ContainsKey(id)) id = b + "-" + n++; return id;
    }
    public static string Slug(string s)
    {
        var sb = new System.Text.StringBuilder(); foreach (char c0 in (s ?? "").ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD))
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c0) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            if ((c0 >= 'a' && c0 <= 'z') || (c0 >= '0' && c0 <= '9')) sb.Append(c0); else if (sb.Length > 0 && sb[sb.Length - 1] != '-') sb.Append('-');
        }
        return sb.ToString().Trim('-') is string r && r.Length > 0 ? r : "module";
    }
}
