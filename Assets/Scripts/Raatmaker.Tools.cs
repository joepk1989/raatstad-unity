using System.Collections.Generic;
using UnityEngine;

// Raatmaker: het raster en de gereedschappen (zoals 08d-module-maker.js). Alles in de lokale coördinaten van de webversie (x, z).
public partial class Raatmaker
{
    float Ts => MV2.Ts(m);
    ModLevel Lvl => m.lv[lv];
    Vector2 P(int i, int j) => MV2.P(i, j, Ts);
    Vector2 P(Vector2Int a) => MV2.P(a.x, a.y, Ts);
    static readonly float SQ3 = Mathf.Sqrt(3f);

    // ---------- raster ----------
    (int, int, int) TriAt(Vector2 p) { float t = Ts, fi = p.x / (t * SQ3 / 2f), fj = p.y / t - fi / 2f; int i = Mathf.FloorToInt(fi), j = Mathf.FloorToInt(fj); return (i, j, (fi - i) + (fj - j) < 1f ? 0 : 1); }
    Vector2Int VertexAt(Vector2 p)
    {
        float t = Ts, fi = p.x / (t * SQ3 / 2f), fj = p.y / t - fi / 2f; Vector2Int b = default; float bd = 1e9f;
        for (int a = 0; a <= 1; a++) for (int c = 0; c <= 1; c++) { int i = Mathf.FloorToInt(fi) + a, j = Mathf.FloorToInt(fj) + c; float d = (P(i, j) - p).magnitude; if (d < bd) { bd = d; b = new Vector2Int(i, j); } }
        return b;
    }
    static (int, int, int)[] Around(int i, int j) => new[] { (i, j, 0), (i - 1, j, 0), (i, j - 1, 0), (i - 1, j, 1), (i, j - 1, 1), (i - 1, j - 1, 1) };
    Vector2[] Tri((int, int, int) id) => MV2.TriVerts(id.Item1, id.Item2, id.Item3, Ts);
    Vector2 Cen((int, int, int) id) => MV2.Centroid(Tri(id));
    bool OkTri((int, int, int) id) { var c = Cen(id); return MV2.InArea(m, c.x, c.y); }
    bool InArea(Vector2 p) => MV2.InArea(m, p.x, p.y);
    // driehoeken van het penseel rond p: zeshoek rond het rasterpunt, één driehoek, of (delen van) het vak
    static readonly string[] Brushes = { "hex", "tri", "cell", "core", "ring" }, BrushNames = { "Zeshoek", "Driehoek", "Heel vak", "Kern ¾", "Rand" };
    List<(int, int, int)> BrushTris(Vector2 p)
    {
        var o = new List<(int, int, int)>();
        if (brush == "hex") { var v = VertexAt(p); foreach (var id in Around(v.x, v.y)) if (OkTri(id)) o.Add(id); return o; }
        if (brush == "tri") { var id = TriAt(p); if (OkTri(id)) o.Add(id); return o; }
        var cell = MV2.CellOf(p.x, p.y); if (MV2.HexDist(cell.x, cell.y) > m.k) return o; var c = MV2.CellXZ(cell.x, cell.y); float t = Ts;
        int iM = Mathf.CeilToInt(MV2.R / (t * SQ3 / 2f)) + 2, jM = Mathf.CeilToInt(MV2.R / t) + iM, ci = Mathf.RoundToInt(c.x / (t * SQ3 / 2f)), cj = Mathf.RoundToInt(c.y / t - ci / 2f);
        bool InHex(Vector2 q, float rad) { for (int s = 0; s < 6; s++) if (Vector2.Dot(q - c, MV2.FN(s)) > rad + 1e-4f) return false; return true; }
        for (int i = ci - iM; i <= ci + iM; i++) for (int j = cj - jM; j <= cj + jM; j++) for (int tt = 0; tt < 2; tt++)
        {
            var q = Cen((i, j, tt)); if (!InHex(q, MV2.INNER)) continue; bool core = InHex(q, MV2.INNER * 0.75f);
            if (brush == "cell" || (brush == "core" ? core : !core)) o.Add((i, j, tt));
        }
        return o;
    }

    // ---------- muren ----------
    (Vector2 a, Vector2 b) Seg((int, int, int) k) => MV2.WallSeg(k, Ts);
    static float DistSeg(Vector2 p, Vector2 a, Vector2 b) { Vector2 d = b - a; float l2 = Mathf.Max(1e-9f, d.sqrMagnitude), t = Mathf.Clamp01(Vector2.Dot(p - a, d) / l2); return (a + d * t - p).magnitude; }
    bool NearestWall(Vector2 p, float maxD, out (int, int, int) key)
    {
        key = default; float bd = maxD; bool any = false;
        foreach (var kv in Lvl.w) { if (kv.Key.Item3 < 0 || kv.Key.Item3 > 5) continue; var (a, b) = Seg(kv.Key); float d = DistSeg(p, a, b); if (d < bd) { bd = d; key = kv.Key; any = true; } }
        return any;
    }
    // ribben tussen twee rasterpunten op één lijn (richting d12, n stappen): 0–5 langs het raster, 6–11 haaks erop
    static List<(int, int, int)> SegsBetween(Vector2Int a, int d12, int n)
    {
        var o = new List<(int, int, int)>(); var D = MV2.DIR12[d12]; int i = a.x, j = a.y;
        int k = d12 < 6 ? d12 % 3 : 3 + (d12 - 6) % 3; bool fw = d12 < 6 ? d12 < 3 : d12 < 9;
        for (int s = 0; s < n; s++) { int ni = i + D.x, nj = j + D.y; o.Add(fw ? (i, j, k) : (ni, nj, k)); i = ni; j = nj; }
        return o;
    }
    // vanuit rasterpunt a naar punt p: beste richting en het aantal stappen (muren: twaalf richtingen, trappen: zes)
    (int d, int n) DirSteps(Vector2Int a, Vector2 p, bool twelve)
    {
        Vector2 pa = P(a); int bd = 0, bn = 1; float bs = 1e9f; bool any = false;
        for (int d = 0; d < (twelve ? 12 : 6); d++)
        {
            var D = MV2.DIR12[d]; Vector2 u = P(a.x + D.x, a.y + D.y) - pa; float l = u.magnitude, al = Vector2.Dot(p - pa, u) / l, lat = Mathf.Abs((p - pa).x * -u.y + (p - pa).y * u.x) / l;
            if (al < 0f) continue; float sc = lat - al * 0.05f; if (!any || sc < bs) { any = true; bs = sc; bd = d; bn = Mathf.Max(1, Mathf.RoundToInt(al / l)); }
        }
        return (bd, bn);
    }
    // een rib van rasterpunt p naar q (buren): de muur erop, als die je tegenhoudt (dicht of raam)
    bool Blocked(Vector2Int p, Vector2Int q)
    {
        var diff = q - p;
        for (int d = 0; d < 3; d++)
        {
            (int, int, int)? key = diff == MV2.DIRW[d] ? (p.x, p.y, d) : -diff == MV2.DIRW[d] ? (q.x, q.y, d) : ((int, int, int)?)null;
            if (key.HasValue && Lvl.w.TryGetValue(key.Value, out var W)) return W.o != "door" && W.o != "gate" && W.o != "open";
        }
        return false;
    }
    // de drie buurdriehoeken met de gedeelde rib
    static IEnumerable<((int, int, int) nb, Vector2Int a, Vector2Int b)> TriNb((int, int, int) id)
    {
        var (i, j, t) = id;
        if (t == 0) { yield return ((i, j - 1, 1), new Vector2Int(i, j), new Vector2Int(i + 1, j)); yield return ((i - 1, j, 1), new Vector2Int(i, j), new Vector2Int(i, j + 1)); yield return ((i, j, 1), new Vector2Int(i + 1, j), new Vector2Int(i, j + 1)); }
        else { yield return ((i + 1, j, 0), new Vector2Int(i + 1, j), new Vector2Int(i + 1, j + 1)); yield return ((i, j + 1, 0), new Vector2Int(i + 1, j + 1), new Vector2Int(i, j + 1)); yield return ((i, j, 0), new Vector2Int(i, j + 1), new Vector2Int(i + 1, j)); }
    }
    // vullen: alle driehoeken met dezelfde vloer (of leeg) tot aan de muren en de rand van het gebied
    List<(int, int, int)> FillRegion(Vector2 p)
    {
        // een lange muur (haaks op het raster) snijdt driehoeken doormidden: daar gaat het vullen alleen verder aan de kant waar het binnenkwam
        var o = new List<(int, int, int)>(); var s = TriAt(p); if (!OkTri(s)) return o; var lw = MV2.LongWalls(Lvl, Ts);
        float Side((int, int, int) id, Vector2 at) => MV2.CrossedBy(lw, Cen(id), Ts, out var wa, out var wb, true) ? MV2.SideOf(wa, wb, at) : 0f;
        Lvl.f.TryGetValue(s, out var want); var seen = new HashSet<((int, int, int), float)> { (s, Side(s, p)) }; var q = new Queue<((int, int, int) t, float sd)>(); q.Enqueue((s, Side(s, p)));
        var got = new HashSet<(int, int, int)>();
        while (q.Count > 0 && o.Count < 4000)
        {
            var (c, sd) = q.Dequeue(); if (got.Add(c)) o.Add(c);
            foreach (var (nb, a, b) in TriNb(c))
            {
                if (!OkTri(nb) || Blocked(a, b)) continue; Vector2 em = (P(a.x, a.y) + P(b.x, b.y)) * 0.5f;
                float es = Side(c, em); if (sd != 0f && es != 0f && es != sd) continue;   // de ribbe ligt aan de andere kant van de muur
                float ns = Side(nb, em); if (ns == 0f && MV2.CrossedBy(lw, Cen(nb), Ts, out _, out _, true)) ns = sd;   // dezelfde muur loopt door
                if (seen.Contains((nb, ns))) continue; Lvl.f.TryGetValue(nb, out var mt); if (mt != want) continue;
                seen.Add((nb, ns)); q.Enqueue((nb, ns));
            }
        }
        return o;
    }
    // kamer: muren op alle zes zijden van het vak (bestaande muren blijven)
    List<(int, int, int)> RoomWalls(Vector2 p)
    {
        var o = new List<(int, int, int)>(); var cell = MV2.CellOf(p.x, p.y); if (MV2.HexDist(cell.x, cell.y) > m.k) return o; var c = MV2.CellXZ(cell.x, cell.y); int per = Mathf.RoundToInt(MV2.R / Ts);
        for (int s = 0; s < 6; s++)
        {
            float a0 = (30f + 60f * s) * Mathf.Deg2Rad, a1 = (90f + 60f * s) * Mathf.Deg2Rad; Vector2 p0 = c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * MV2.R, p1 = c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * MV2.R;
            var a = VertexAt(p0); int d6 = DirOf(p1 - p0);
            for (int i = 0; i < per; i++) { var D = MV2.DIR6[d6]; var nx = a + D; o.Add(d6 < 3 ? (a.x, a.y, d6 % 3) : (nx.x, nx.y, d6 % 3)); a = nx; }
        }
        return o;
    }
    int DirOf(Vector2 d) { int best = 0; float bd = -2f; for (int k = 0; k < 6; k++) { var D = MV2.DIR6[k]; Vector2 u = P(D.x, D.y); float c = Vector2.Dot(d.normalized, u.normalized); if (c > bd) { bd = c; best = k; } } return best; }

    // ---------- trappen ----------
    // richting uit het driehoekje onder de muis: links van de lijn (sd 1) langs de eerste rib van die sector, rechts (−1) langs de tweede
    int SectorDir(Vector2Int a, Vector2 p, int sd)
    {
        Vector2 pa = P(a); float ph = Mathf.Repeat(Mathf.Atan2(p.y - pa.y, p.x - pa.x) - Mathf.PI / 6f, Mathf.PI * 2f), S60 = Mathf.PI / 3f;
        int d0 = Mathf.FloorToInt(ph / S60) % 6; float fr = ph / S60 - Mathf.Floor(ph / S60);
        return sd > 0 ? d0 : sd < 0 ? (d0 + 1) % 6 : (fr < 0.5f ? d0 : (d0 + 1) % 6);
    }
    int StairSide => tool == "ramp" ? 0 : tool == "ustair" ? usd : tool == "spiral" ? wsd : ssd;
    ModStair NewStair(Vector2Int a, int d, int n)
    {
        if (tool == "spiral") return new ModStair { ai = a.x, aj = a.y, d = d, n = 1, w = 1.5f, k = "spiral", side = wsd, hasSide = true };
        if (tool == "ramp") return new ModStair { ai = a.x, aj = a.y, d = d, n = n, w = rampW, k = "ramp" };
        if (tool == "ustair") return new ModStair { ai = a.x, aj = a.y, d = d, n = n, w = sw, k = "u", side = usd, hasSide = true, off = usd * sw / 2f };
        var S = new ModStair { ai = a.x, aj = a.y, d = d, n = n, w = sw, k = "stair" }; if (ssd != 0) S.off = ssd * sw / 2f; return S;
    }
    ModStair HoverStair(Vector2 p) { var a = VertexAt(p); return NewStair(a, SectorDir(a, p, StairSide), 3); }
    ModStair HexRampAt(Vector2 p)
    {
        var cell = MV2.CellOf(p.x, p.y); var c = MV2.CellXZ(cell.x, cell.y); int side = 0; float bd = -1e9f;
        for (int s = 0; s < 6; s++) { float d = Vector2.Dot(p - c, MV2.FN(s)); if (d > bd) { bd = d; side = s; } }
        return new ModStair { ai = 0, aj = 0, d = 0, n = 1, w = 9f, k = "hexramp", q = cell.x, r = cell.y, side = side, hasSide = true };
    }
    List<Vector2> StairPoly(ModStair S)
    {
        var D = MV2.DIR6[Mathf.Clamp(S.d, 0, 5)]; Vector2 a = P(S.ai, S.aj); float t = Ts, dx = D.x * t * SQ3 / 2f, dz = D.x * t / 2f + D.y * t, dl = Mathf.Max(1e-6f, Mathf.Sqrt(dx * dx + dz * dz));
        Vector2 u = new Vector2(dx / dl, dz / dl), nn = new Vector2(-u.y, u.x); float len = (S.n > 0 ? S.n : 1f) * t, w = S.w > 0 ? S.w : 1.2f, off = S.off;
        Vector2 At(float al, float lat) => a + u * al + nn * (lat + off);
        if (S.k == "hexramp")
        {
            var c = MV2.CellXZ(S.q, S.r); Vector2 e = -MV2.FN(((S.side % 6) + 6) % 6), l = new Vector2(-e.y, e.x); float IN = MV2.INNER; Vector2 Pt(float uu, float lat) => c + e * uu + l * lat;
            return new List<Vector2> { Pt(-IN, -4.5f), Pt(0, -9f), Pt(IN, -4.5f), Pt(IN, 4.5f), Pt(0, 9f), Pt(-IN, 4.5f) };
        }
        if (S.k == "spiral") { float rr = 1.9f / Mathf.Cos(Mathf.PI / 6f), a0 = Mathf.Atan2(u.y, u.x); var o = new List<Vector2>(); for (int i = 0; i < 6; i++) { float an = a0 + i * Mathf.PI / 3f; o.Add(a + new Vector2(Mathf.Cos(an), Mathf.Sin(an)) * rr); } return o; }
        if (S.k == "u") { float sd = S.Side, oA = -sd * w / 2f, oB = sd * w + sd * w / 2f, cut = 2f * w / SQ3; return new List<Vector2> { At(0, oA), At(0, oB), At(len + 0.25f + cut, oB), At(len + 0.25f, oA) }; }
        if (S.k == "stair" || string.IsNullOrEmpty(S.k)) return new List<Vector2> { At(0, -w / 2f), At(0, w / 2f), At(len, w / 2f), At(len + w * SQ3 / 2f, 0), At(len, -w / 2f) };
        return new List<Vector2> { At(0, -w / 2f), At(0, w / 2f), At(len, w / 2f), At(len, -w / 2f) };
    }
    static bool InPoly(Vector2 p, List<Vector2> v)
    {
        bool c = false; for (int i = 0, j = v.Count - 1; i < v.Count; j = i++) if ((v[i].y > p.y) != (v[j].y > p.y) && p.x < (v[j].x - v[i].x) * (p.y - v[i].y) / (v[j].y - v[i].y) + v[i].x) c = !c; return c;
    }
    int StairAt(Vector2 p) { for (int s = Lvl.s.Count - 1; s >= 0; s--) if (InPoly(p, StairPoly(Lvl.s[s]))) return s; return -1; }

    // ---------- voorwerpen ----------
    static readonly Dictionary<string, Vector2> Foot = new Dictionary<string, Vector2> { ["bench"] = new Vector2(1.8f, 0.5f), ["shelf"] = new Vector2(2.4f, 0.5f), ["counter"] = new Vector2(2.3f, 0.8f), ["bike"] = new Vector2(2.4f, 0.64f), ["sign"] = new Vector2(1.6f, 0.12f), ["solar"] = new Vector2(3.4f, 1.6f), ["container"] = new Vector2(2.4f, 1.4f), ["goal"] = new Vector2(3.7f, 1f), ["boat"] = new Vector2(4.2f, 1.7f), ["shelter"] = new Vector2(3f, 1.4f), ["car"] = new Vector2(4.3f, 1.9f), ["bed"] = new Vector2(2f, 1.6f), ["sofa"] = new Vector2(2.1f, 0.9f), ["desk"] = new Vector2(1.6f, 0.8f), ["kitchen"] = new Vector2(2.4f, 0.65f), ["screen"] = new Vector2(6f, 0.2f), ["swing"] = new Vector2(2.7f, 1.2f) };
    static readonly Dictionary<string, float> Rad = new Dictionary<string, float> { ["tree"] = 1.7f, ["bush"] = 0.9f, ["lamp"] = 0.2f, ["pillar"] = 0.45f, ["table"] = 1.1f, ["planter"] = 0.75f, ["fountain"] = 1.9f, ["bin"] = 0.25f, ["pump"] = 0.6f, ["charger"] = 0.3f, ["turbine"] = 1f, ["chimney"] = 1.25f, ["grave"] = 0.5f, ["slide"] = 0.8f, ["sheep"] = 0.6f, ["hpillar"] = 0.5f };
    int ObjectAt(Vector2 p, float r = 0.9f)
    {
        int best = -1; float bd = 1e9f;
        for (int i = 0; i < Lvl.o.Count; i++)
        {
            var o = Lvl.o[i]; float d;
            if (Foot.TryGetValue(o.t, out var f)) { Vector2 u = new Vector2(Mathf.Cos(o.rot), Mathf.Sin(o.rot)), dd = p - new Vector2(o.x, o.z); float al = Vector2.Dot(dd, u), lat = -dd.x * u.y + dd.y * u.x; if (Mathf.Abs(al) > f.x / 2f + 0.15f || Mathf.Abs(lat) > f.y / 2f + 0.3f) continue; d = Mathf.Abs(lat) + Mathf.Max(0f, Mathf.Abs(al) - f.x / 2f); }
            else { d = (p - new Vector2(o.x, o.z)).magnitude; if (d > Mathf.Max(r, Rad.TryGetValue(o.t, out var rr) ? rr : 0.5f)) continue; }
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }
    // tegen de muur: kasten, banken, bedden, bureaus en keukens draaien met hun rug naar de dichtstbijzijnde muur en schuiven ertegenaan
    static readonly Dictionary<string, (float back, bool z)> Back = new Dictionary<string, (float, bool)> { ["sofa"] = (0.45f, true), ["kitchen"] = (0.32f, true), ["desk"] = (0.4f, true), ["shelf"] = (0.27f, true), ["counter"] = (0.4f, true), ["bench"] = (0.28f, true), ["bed"] = (1.05f, false), ["screen"] = (0.08f, true) };
    ModObj ObjPose(string t, Vector2 p)
    {
        if (Back.TryGetValue(t, out var B) && !ctrlHeld)
        {
            float bd = B.back + 1f; Vector2 bf = default, bn = default; bool any = false;
            foreach (var kv in Lvl.w)
            {
                if (kv.Value.o == "open" || kv.Value.o == "gate" || kv.Value.o == "door" || kv.Key.Item3 < 0 || kv.Key.Item3 > 5) continue; var (a, b) = Seg(kv.Key); Vector2 d = b - a; float u = Mathf.Clamp01(Vector2.Dot(p - a, d) / d.sqrMagnitude); Vector2 f = a + d * u; float dist = (p - f).magnitude;
                if (dist > bd) continue; Vector2 n = new Vector2(-d.y, d.x).normalized; if (Vector2.Dot(p - a, n) < 0f) n = -n; bd = dist; bf = f; bn = n; any = true;
            }
            if (any) { float off = MV2.WALL_T / 2f + B.back + 0.02f; var q = bf + bn * off; return new ModObj { t = t, x = q.x, z = q.y, rot = Mathf.Repeat(B.z ? Mathf.Atan2(-bn.x, bn.y) : Mathf.Atan2(bn.y, bn.x), Mathf.PI * 2f) }; }
        }
        return new ModObj { t = t, x = Mathf.Round(p.x * 4f) / 4f, z = Mathf.Round(p.y * 4f) / 4f, rot = orot };
    }

    // ---------- wat ligt er onder de muis (gum en kiezen): voorwerp → muur → trap → dak → vloer ----------
    struct Pick { public string kind; public int idx; public (int, int, int) key; public (int, int) cell; public string Name; }
    bool PickAt(Vector2 p, out Pick pk)
    {
        pk = default; int oi = ObjectAt(p, 0.8f); if (oi >= 0) { pk = new Pick { kind = "o", idx = oi, Name = MV2.NameOf(MV2.Objects, MV2.ObjectNames, Lvl.o[oi].t) }; return true; }
        if (NearestWall(p, 0.6f, out var wk)) { var W = Lvl.w[wk]; pk = new Pick { kind = "w", key = wk, Name = MV2.NameOf(MV2.Walls, MV2.WallNames, W.m) + (string.IsNullOrEmpty(W.o) ? "" : " met " + OpeningName(W.o).ToLower()) }; return true; }
        int si = StairAt(p); if (si >= 0) { pk = new Pick { kind = "s", idx = si, Name = StairName(Lvl.s[si].k) }; return true; }
        var c = MV2.CellOf(p.x, p.y); if (Lvl.r.ContainsKey((c.x, c.y))) { pk = new Pick { kind = "r", cell = (c.x, c.y), Name = RoofName(Lvl.r[(c.x, c.y)]) }; return true; }
        var t = TriAt(p); if (Lvl.f.TryGetValue(t, out var fm)) { pk = new Pick { kind = "f", key = t, Name = "vloer (" + MV2.NameOf(MV2.Floors, MV2.FloorNames, fm).ToLower() + ")" }; return true; }
        return false;
    }
    void RemovePick(Pick pk)
    {
        switch (pk.kind)
        {
            case "o": if (pk.idx < Lvl.o.Count) Lvl.o.RemoveAt(pk.idx); break;
            case "w": Lvl.w.Remove(pk.key); break;
            case "s": if (pk.idx < Lvl.s.Count) Lvl.s.RemoveAt(pk.idx); break;
            case "r": Lvl.r.Remove(pk.cell); break;
            case "f": Lvl.f.Remove(pk.key); break;
        }
    }
    static string OpeningName(string o) => o == "door" ? "Deur" : o == "window" ? "Raam" : o == "gate" ? "Poort" : o == "open" ? "Open" : "Dicht";
    static string StairName(string k) => k == "spiral" ? "wenteltrap" : k == "u" ? "bordestrap" : k == "ramp" ? "helling" : k == "hexramp" ? "schans" : k == "esc" ? "roltrap" : k == "trib" ? "tribune" : "trap";
    static readonly string[] RoofKinds = { "hip", "gable0", "gable1", "gable2", "flat", "half" }, RoofNames = { "Schilddak", "Zadeldak /", "Zadeldak |", "Zadeldak \\", "Plat dak", "Half schilddak" };
    static string RoofName(string k) { int i = System.Array.IndexOf(RoofKinds, k); return i >= 0 ? RoofNames[i].ToLower() : "dak"; }
}
