using System.Collections.Generic;
using UnityEngine;

// Het huisdak uit SketchUp (hexgrid city concept/dak.skp, geëxporteerd als raatstad-dak.json, hier Assets/Resources/Daken/dak.json):
// een zeshoekige dakrand van 30 cm over het hele vak, daarop een afgeknotte zeshoekige kap van 3 m met een plat bovenvlak en een
// dakkapel. In de export: driehoeken in cm, SketchUp-assen (z omhoog), het vak met een hoekpunt op (0, 0) en het midden op (0, 900).
// Opnieuw exporteren: het nieuwe bestand over dak.json zetten.
public static partial class PlanetCity
{
    // driehoeken in meters: x, y (in het vlak, midden op 0, een hoekpunt op (0, −9)), z omhoog; soort: dakvlak, rand, kozijn of glas.
    // Een staand vlak in de kap (de voorkant van een dakkapel) wordt een kozijn met een ruit erin: de ruit is hetzelfde vlak,
    // 22 % kleiner naar het midden van dat vlak en 3 cm naar buiten.
    public enum DakKind { Roof, Rim, Frame, Glass }
    public struct DakTri { public Vector3 a, b, c; public DakKind kind; public bool dormer; }
    static List<DakTri> dak; static bool dakTried;
    public static List<DakTri> DakShape()
    {
        if (dakTried) return dak; dakTried = true;
        var ta = Resources.Load<TextAsset>("Daken/dak"); if (ta == null) return null;
        try
        {
            if (!(ModuleJson.Parse(ta.text) is Dictionary<string, object> d) || !(d.TryGetValue("vorm", out var v) && v is Dictionary<string, object> vorm)) return null;
            var list = new List<DakTri>();
            foreach (var part in vorm.Values)
            {
                if (!(part is List<object> f)) continue;
                for (int i = 0; i + 8 < f.Count; i += 9)
                {
                    Vector3 P(int k) => new Vector3(ModuleJson.F(f[i + k]) / 100f, ModuleJson.F(f[i + k + 1]) / 100f - 9f, ModuleJson.F(f[i + k + 2]) / 100f);
                    var t = new DakTri { a = P(0), b = P(3), c = P(6) }; Vector3 n = Vector3.Cross(t.b - t.a, t.c - t.a);
                    float zmin = Mathf.Min(t.a.z, Mathf.Min(t.b.z, t.c.z));
                    t.kind = zmin > 0.31f && n.sqrMagnitude > 1e-8f && Mathf.Abs(n.normalized.z) < 0.2f ? DakKind.Frame : t.a.z <= 0.31f && t.b.z <= 0.31f && t.c.z <= 0.31f ? DakKind.Rim : DakKind.Roof;
                    list.Add(t);
                }
            }
            // de dakkapel: alles wat uit het dakvlak steekt (boven de rand, niet het platte bovenvlak, niet in een dakvlak van de kap)
            float slopeNz = 0f;
            foreach (var t in list) { float lo = Mathf.Min(t.a.z, Mathf.Min(t.b.z, t.c.z)), hi = Mathf.Max(t.a.z, Mathf.Max(t.b.z, t.c.z)); if (t.kind == DakKind.Roof && lo <= 0.31f && hi >= 2.99f) { slopeNz = Vector3.Cross(t.b - t.a, t.c - t.a).normalized.z; break; } }
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i]; float lo = Mathf.Min(t.a.z, Mathf.Min(t.b.z, t.c.z)), hi = Mathf.Max(t.a.z, Mathf.Max(t.b.z, t.c.z)); float nz = Mathf.Abs(Vector3.Cross(t.b - t.a, t.c - t.a).normalized.z);
                t.dormer = lo > 0.35f && hi < 2.95f && Mathf.Abs(nz - Mathf.Abs(slopeNz)) > 0.05f; list[i] = t;
            }
            // ruiten: per staand vlak (zelfde richting) naar het midden van dat vlak
            var groups = new Dictionary<Vector3Int, List<DakTri>>();
            foreach (var t in list) if (t.kind == DakKind.Frame) { var n = Vector3.Cross(t.b - t.a, t.c - t.a).normalized; var key = Vector3Int.RoundToInt(n * 20f); if (!groups.TryGetValue(key, out var g)) groups[key] = g = new List<DakTri>(); g.Add(t); }
            foreach (var g in groups.Values)
            {
                Vector3 cen = Vector3.zero; foreach (var t in g) cen += t.a + t.b + t.c; cen /= g.Count * 3;
                foreach (var t in g) { Vector3 n = Vector3.Cross(t.b - t.a, t.c - t.a).normalized * 0.03f; Vector3 S(Vector3 v) => cen + (v - cen) * 0.78f + n; list.Add(new DakTri { a = S(t.a), b = S(t.b), c = S(t.c), kind = DakKind.Glass, dormer = true }); }
            }
            dak = list.Count > 0 ? list : null;
        }
        catch (System.Exception e) { Debug.LogWarning("Dak niet te lezen: " + e.Message); }
        return dak;
    }
    // het dak met 1, 2 (tegenover elkaar) of 3 dakkapellen (om de 120°)
    static readonly List<DakTri>[] dakN = new List<DakTri>[4];
    public static List<DakTri> DakShape(int dormers)
    {
        var b = DakShape(); if (b == null || dormers <= 1) return b; dormers = Mathf.Min(dormers, 3);
        if (dakN[dormers] != null) return dakN[dormers];
        var o = new List<DakTri>(b);
        for (int k = 1; k < dormers; k++)
        {
            float an = k * Mathf.PI * 2f / dormers, cs = Mathf.Cos(an), sn = Mathf.Sin(an); Vector3 Rt(Vector3 v) => new Vector3(v.x * cs - v.y * sn, v.x * sn + v.y * cs, v.z);
            foreach (var t in b) if (t.dormer) o.Add(new DakTri { a = Rt(t.a), b = Rt(t.b), c = Rt(t.c), kind = t.kind, dormer = true });
        }
        return dakN[dormers] = o;
    }
    // hoeveel dakkapellen bij dit huis: vaak één, soms twee of drie
    public static int Dormers(int hash) { int h = ((hash >> 11) ^ (hash >> 3)) & 0x7fffffff; h %= 20; return h < 9 ? 1 : h < 16 ? 2 : 3; }
    // het dak op een blok: hoeken op de hoeken van het vak, straal k van het vak, onderkant op hoogte y
    static bool HouseRoof(MeshBuilder roof, MeshBuilder rim, MeshBuilder glass, Frame f, float k, float y, int hash)
    {
        var shape = DakShape(Dormers(hash)); if (shape == null) return false;
        float a0 = Mathf.Atan2(f.poly[0].y, f.poly[0].x), rot = a0 + Mathf.PI / 2f, cs = Mathf.Cos(rot), sn = Mathf.Sin(rot), s = f.rad * k / MV2.R;
        Vector2 R2(float x, float z) => new Vector2(x * cs - z * sn, x * sn + z * cs);
        foreach (var t in shape)
        {
            Vector2 pa = R2(t.a.x, t.a.y) * s, pb = R2(t.b.x, t.b.y) * s, pc = R2(t.c.x, t.c.y) * s; Vector3 n = Vector3.Cross(t.b - t.a, t.c - t.a); Vector2 nn = R2(n.x, n.y);
            (t.kind == DakKind.Glass ? glass : t.kind == DakKind.Roof ? roof : rim).Tri(f.P(pa.x, y + t.a.z, pa.y), f.P(pb.x, y + t.b.z, pb.y), f.P(pc.x, y + t.c.z, pc.y), f.D(nn.x, n.z, nn.y));
        }
        return true;
    }
}
