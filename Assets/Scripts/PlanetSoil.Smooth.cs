using System.Collections.Generic;
using UnityEngine;

// Terrein low poly, zacht, rond of extra rond (Instellingen > Terrein, onthouden in PlayerPrefs).
// - Low poly: elke driehoek zijn eigen normaal (zoals altijd).
// - Zacht: gedeelde normalen per hoekpunt; de vorm blijft hetzelfde, alleen de belichting loopt vloeiend door.
// - Rond (en extra rond): ook de vorm wordt afgerond: elke bodemdriehoek van een gewoon vak wordt in 9 (25) stukjes verdeeld op een gebogen vlak
//   door de zachte normalen van zijn drie hoekpunten (PN-driehoek). Dezelfde hoogtes per vak, alleen glooiend in plaats van puntig.
// De normaal komt uit een veld dat alleen van de plek afhangt (zoals SoilColor): per vak de helling van het vlak door de middens
// van zijn buren, gemengd naar de afstand tot die middens. Een gebogen rand hangt alleen af van zijn twee eindpunten en hun normalen,
// dus twee delen (ChunkN) en twee buurvakken sluiten naadloos aan.
// Strak blijven: wegen, sokkels en wanden (zitten niet in de bodemmesh), bebouwde vakken en vakken met een weg, pad, geul, gracht of
// tunnel (SmoothCell). Een rand naar zo'n vak blijft recht, zodat de buur er precies op aansluit. Een driehoek die meer dan
// SmoothCrease van het veld afwijkt houdt zijn eigen normaal.
// Botsen (lopen, rijden, de muis) gaat op de grove mesh: die volgt de hoogtes van de spellogica (H, SurfaceH). Bij Rond tekent de
// bodem een eigen fijne mesh; omschakelen tekent niets anders opnieuw (FillSoil per bestaand deel).
public static partial class PlanetCity
{
    public static readonly string[] SoilDetailNames = { "low poly", "zacht", "rond", "extra rond" };
    const string SmoothKey = "raatstad-terrein-zacht";
    static int soilDetail = -1;
    public static int SoilDetail
    {
        get { if (soilDetail < 0) soilDetail = Mathf.Clamp(PlayerPrefs.GetInt(SmoothKey, 0), 0, 3); return soilDetail; }
        set
        {
            value = Mathf.Clamp(value, 0, 3); if (value == SoilDetail) return;
            soilDetail = value; PlayerPrefs.SetInt(SmoothKey, value); PlayerPrefs.Save();
            if (soilMat != null) soilMat.SetFloat("_SoilSmooth", value > 0 ? 1f : 0f);   // de soort ook naar de zachte normaal (Bodem.shader)
            var t0 = Time.realtimeSinceStartup; int n = 0; statSmooth = statTris = statCell = statCrease = 0; StatBulge = 0f;
            var dead = new List<MeshFilter>();
            foreach (var kv in soilMeshes) { if (kv.Key == null) { dead.Add(kv.Key); continue; } FillSoil(kv.Key, kv.Value); n++; }
            foreach (var m in dead) soilMeshes.Remove(m);
            Debug.Log("Terrein " + SoilDetailNames[value] + ": " + n + " delen, " + statSmooth + " van " + statTris + " driehoeken zacht (vak niet: " + statCell + ", knik: " + statCrease + "), afwijking hooguit " + StatBulge.ToString("0.00") + " m, in " + ((Time.realtimeSinceStartup - t0) * 1000f).ToString("0") + " ms");
        }
    }
    public static bool SmoothSoil => SoilDetail > 0;
    const float SmoothCrease = 50f;   // graden: een driehoek die verder van het veld afwijkt houdt zijn eigen normaal (een berghelling wijkt tot zo'n 40° af)
    static int RoundN => SoilDetail >= 3 ? 5 : 3;   // Rond: elke rand in 3 stukjes, een driehoek in 9; extra rond: in 5, een driehoek in 25
    static int statSmooth, statTris, statCell, statCrease; public static float StatBulge;   // hoeveel Rond hooguit van de grove driehoek afwijkt (m)

    class SoilPart { public Mesh coarse, fine, wire; public Vector3[] v; public int[] cells; }

    // Terreinlijnen (Instellingen, onthouden in PlayerPrefs): dunne donkere lijnen langs elke driehoek van de bodem, in elke stand
    const string WireKey = "raatstad-terrein-lijnen";
    static int soilWire = -1;
    public static bool SoilWire
    {
        get { if (soilWire < 0) soilWire = PlayerPrefs.GetInt(WireKey, 0) == 1 ? 1 : 0; return soilWire == 1; }
        set
        {
            if (value == SoilWire) return; soilWire = value ? 1 : 0; PlayerPrefs.SetInt(WireKey, soilWire); PlayerPrefs.Save();
            foreach (var kv in soilMeshes) if (kv.Key != null) Wire(kv.Key, kv.Value);
        }
    }
    static void Wire(MeshFilter mf, SoilPart part)
    {
        var old = mf.transform.Find("Terreinlijnen"); if (old != null) Object.Destroy(old.gameObject);
        if (part.wire != null) { Object.Destroy(part.wire); part.wire = null; }
        if (!SoilWire || mf.sharedMesh == null) return;
        var src = mf.sharedMesh; var v = src.vertices; var n = src.normals; var tris = src.triangles;
        var lv = new Vector3[v.Length]; for (int k = 0; k < v.Length; k++) lv[k] = v[k] + (k < n.Length ? n[k] : v[k].normalized) * 0.04f;   // net boven de grond
        var li = new int[tris.Length * 2];
        for (int t = 0; t + 2 < tris.Length; t += 3) { int a = tris[t], b = tris[t + 1], c = tris[t + 2]; int o = t * 2; li[o] = a; li[o + 1] = b; li[o + 2] = b; li[o + 3] = c; li[o + 4] = c; li[o + 5] = a; }
        var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, name = "terreinlijnen" };
        m.vertices = lv; m.SetIndices(li, MeshTopology.Lines, 0); m.bounds = src.bounds; part.wire = m;
        var go = new GameObject("Terreinlijnen"); go.transform.SetParent(mf.transform, false); go.layer = mf.gameObject.layer;
        go.AddComponent<MeshFilter>().sharedMesh = m;
        var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = Mats.GetUnlit("p_terreinlijnen", new Color(0.08f, 0.10f, 0.06f));
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
    }
    static readonly Dictionary<MeshFilter, SoilPart> soilMeshes = new Dictionary<MeshFilter, SoilPart>();   // de bodem per deel (zolang het deel bestaat)

    // na het bouwen van een deel (SoilMesh): coarse is de grove mesh met kleuren (ook de collider), cells het vak per hoekpunt
    static void RegisterSoil(MeshFilter mf, Mesh coarse, List<int> cells)
    {
        if (soilMeshes.Count > 0) { var dead = new List<MeshFilter>(); foreach (var k in soilMeshes.Keys) if (k == null) dead.Add(k); foreach (var k in dead) soilMeshes.Remove(k); }
        var part = new SoilPart { coarse = coarse, v = coarse.vertices, cells = cells.ToArray() }; soilMeshes[mf] = part;
        FillSoil(mf, part);
    }

    static void FillSoil(MeshFilter mf, SoilPart part)
    {
        if (part.fine != null) { Object.Destroy(part.fine); part.fine = null; }
        int d = SoilDetail;
        if (d >= 2) { part.fine = RoundMesh(part); mf.sharedMesh = part.fine; }
        else { ApplySoilNormals(part.coarse, part.cells, d == 1); mf.sharedMesh = part.coarse; }
        if (SoilWire || part.wire != null) Wire(mf, part);
    }

    // mag de bodem van vak i zacht en rond worden? (geen weg, pad, bebouwing, geul, gracht of tunnel)
    static bool SmoothCell(int i) => i >= 0 && !IsWalk(i) && !Plateau(i) && !fieldCells.Contains(i) && !(RoadH.Count > 0 && RoadH.ContainsKey(i))
        && !(carved.Count > 0 && carved.Contains(i)) && !(boreNear.Count > 0 && boreNear.ContainsKey(i)) && !Planet.IsPole(i) && !NoGround(i) && !Quay(i);

    // waarom vak i strak blijft (voor tests)
    public static string SmoothWhy(int i) => IsWalk(i) ? "weg" : Plateau(i) ? "plateau" : fieldCells.Contains(i) ? "veld" : RoadH.Count > 0 && RoadH.ContainsKey(i) ? "profiel" : carved.Contains(i) ? "geul" : boreNear.ContainsKey(i) ? "tunnel" : Planet.IsPole(i) ? "pool" : NoGround(i) ? "geengrond" : Quay(i) ? "kade" : "zacht";

    // de normaal van vak i: het vlak door de middens van zijn buren (som van de kruisproducten rondom)
    static Vector3 CellNormal(int i)
    {
        var c = Planet.Cells[i]; var nb = c.nb; int m = nb.Length;
        Vector3 Pi = c.dir * (Planet.R + G(i) + CenterOfs1(i)), s = Vector3.zero;
        for (int k = 0; k < m; k++)
        {
            int a = nb[k], b = nb[(k + 1) % m]; if (a < 0 || b < 0) continue;
            var ca = Planet.Cells[a]; var cb = Planet.Cells[b];
            Vector3 Pa = ca.dir * (Planet.R + G(a) + CenterOfs1(a)) - Pi, Pb = cb.dir * (Planet.R + G(b) + CenterOfs1(b)) - Pi;
            Vector3 x = Vector3.Cross(Pa, Pb); if (Vector3.Dot(x, c.dir) < 0f) x = -x; s += x;
        }
        return s.sqrMagnitude > 1e-8f ? s.normalized : c.dir;
    }

    // de zachte normaal op plek p (in vak i): de normalen van vak i en zijn buren, gewogen zoals SoilColor
    static Vector3 SmoothNormal(int i, Vector3 p, Dictionary<int, Vector3> memo)
    {
        Vector3 d = p.normalized, s = Vector3.zero; var c = Planet.Cells[i];
        void Add(int j)
        {
            if (j < 0) return;
            float dist = (Planet.Cells[j].dir - d).magnitude * Planet.R; if (dist >= SoilReach) return;
            float w = 1f - dist / SoilReach; w *= w;
            if (!memo.TryGetValue(j, out var nj)) { nj = CellNormal(j); memo[j] = nj; }
            s += nj * w;
        }
        Add(i); foreach (int j in c.nb) Add(j);
        return s.sqrMagnitude > 1e-10f ? s.normalized : d;
    }

    // Zacht of low poly: de normalen van de grove mesh (de hoekpunten zijn niet gedeeld)
    static void ApplySoilNormals(Mesh mesh, int[] cells, bool smooth)
    {
        var v = mesh.vertices; var nr = new Vector3[v.Length]; var tris = mesh.triangles;
        var memo = smooth ? new Dictionary<int, Vector3>() : null; var ok = smooth ? new Dictionary<int, bool>() : null;
        float cosC = Mathf.Cos(SmoothCrease * Mathf.Deg2Rad);
        for (int t = 0; t + 2 < tris.Length; t += 3)
        {
            int a = tris[t], b = tris[t + 1], c = tris[t + 2];
            Vector3 fn = Vector3.Cross(v[b] - v[a], v[c] - v[a]).normalized;
            nr[a] = nr[b] = nr[c] = fn;
            statTris++; if (!smooth || a >= cells.Length) continue;
            int ci = cells[a]; if (!ok.TryGetValue(ci, out bool can)) { can = SmoothCell(ci); ok[ci] = can; }
            if (!can) { statCell++; continue; }
            Vector3 na = SmoothNormal(ci, v[a], memo), nb = SmoothNormal(ci, v[b], memo), nc = SmoothNormal(ci, v[c], memo);
            if (Vector3.Dot(na, fn) < cosC || Vector3.Dot(nb, fn) < cosC || Vector3.Dot(nc, fn) < cosC) { statCrease++; continue; }   // een knik: strak laten
            nr[a] = na; nr[b] = nb; nr[c] = nc; statSmooth++;
        }
        mesh.normals = nr;
    }

    // ligt de rand a-b op de grens van vak i met een buur? Dan die buur (anders -1: een rand binnen het vak)
    static int BorderCell(int i, Vector3 a, Vector3 b)
    {
        Vector3 m = (a + b).normalized; var c = Planet.Cells[i]; float di = (c.dir - m).magnitude * Planet.R;
        int best = -1; float bd = float.MaxValue;
        foreach (int j in c.nb) { if (j < 0) continue; float dj = (Planet.Cells[j].dir - m).magnitude * Planet.R; if (dj < bd) { bd = dj; best = j; } }
        return bd - di < 0.4f ? best : -1;
    }

    // Rond: de fijne mesh van een deel
    static Mesh RoundMesh(SoilPart part)
    {
        var v = part.v; var cells = part.cells; var tris = part.coarse.triangles;
        var memoN = new Dictionary<int, Vector3>(); var memoS = new Dictionary<int, Soil>(); var ok = new Dictionary<int, bool>();
        bool Ok(int ci) { if (!ok.TryGetValue(ci, out bool can)) { can = SmoothCell(ci); ok[ci] = can; } return can; }
        float cosC = Mathf.Cos(SmoothCrease * Mathf.Deg2Rad);
        var V = new List<Vector3>(v.Length * 3); var N = new List<Vector3>(v.Length * 3); var C = new List<Color>(v.Length * 3); var X = new List<Vector4>(v.Length * 3); var T = new List<int>(tris.Length * 6);
        void Vert(Vector3 p, Vector3 n, int ci) { var so = SoilColor(ci, p, memoS); V.Add(p); N.Add(n); C.Add(so.c); X.Add(so.x); }
        int RN = RoundN; var idx = new int[RN + 1, RN + 1];
        for (int t = 0; t + 2 < tris.Length; t += 3)
        {
            int ia = tris[t], ib = tris[t + 1], ic = tris[t + 2]; int ci = cells[ia];
            Vector3 P1 = v[ia], P2 = v[ib], P3 = v[ic]; Vector3 fn = Vector3.Cross(P2 - P1, P3 - P1).normalized;
            statTris++;
            if (!Ok(ci))   // strak: de driehoek zoals hij is
            {
                statCell++; int b0 = V.Count; Vert(P1, fn, ci); Vert(P2, fn, ci); Vert(P3, fn, ci); T.Add(b0); T.Add(b0 + 1); T.Add(b0 + 2); continue;
            }
            Vector3 N1 = SmoothNormal(ci, P1, memoN), N2 = SmoothNormal(ci, P2, memoN), N3 = SmoothNormal(ci, P3, memoN);
            bool crease = Vector3.Dot(N1, fn) < cosC || Vector3.Dot(N2, fn) < cosC || Vector3.Dot(N3, fn) < cosC; if (crease) statCrease++; else statSmooth++;
            // een rand blijft recht als het vak aan de overkant strak blijft (dan sluit die er precies op aan)
            bool Curve(Vector3 a, Vector3 b) { int j = BorderCell(ci, a, b); return j < 0 || Ok(j); }
            bool c12 = Curve(P1, P2), c23 = Curve(P2, P3), c31 = Curve(P3, P1);
            Vector3 Ctl(Vector3 Pi, Vector3 Pj, Vector3 Ni, bool curve) => (2f * Pi + Pj - (curve ? Vector3.Dot(Pj - Pi, Ni) * Ni : Vector3.zero)) / 3f;
            Vector3 b210 = Ctl(P1, P2, N1, c12), b120 = Ctl(P2, P1, N2, c12), b021 = Ctl(P2, P3, N2, c23), b012 = Ctl(P3, P2, N3, c23), b102 = Ctl(P3, P1, N3, c31), b201 = Ctl(P1, P3, N1, c31);
            Vector3 E = (b210 + b120 + b021 + b012 + b102 + b201) / 6f, Vm = (P1 + P2 + P3) / 3f, b111 = E + (E - Vm) * 0.5f;
            for (int r = 0; r <= RN; r++)
                for (int s = 0; s <= RN - r; s++)
                {
                    float u = r / (float)RN, w2 = s / (float)RN, w = 1f - u - w2;   // u bij P2, w2 bij P3, w bij P1
                    Vector3 p;
                    if (r == 0 && s == 0) p = P1; else if (r == RN) p = P2; else if (s == RN) p = P3;
                    else p = P1 * (w * w * w) + P2 * (u * u * u) + P3 * (w2 * w2 * w2) + b210 * (3f * w * w * u) + b120 * (3f * w * u * u) + b201 * (3f * w * w * w2)
                         + b021 * (3f * u * u * w2) + b102 * (3f * w * w2 * w2) + b012 * (3f * u * w2 * w2) + b111 * (6f * u * w2 * w);
                    StatBulge = Mathf.Max(StatBulge, (p - (P1 * w + P2 * u + P3 * w2)).magnitude);
                    idx[r, s] = V.Count; Vert(p, crease ? fn : SmoothNormal(ci, p, memoN), ci);
                }
            for (int r = 0; r < RN; r++)
                for (int s = 0; s < RN - r; s++)
                {
                    T.Add(idx[r, s]); T.Add(idx[r + 1, s]); T.Add(idx[r, s + 1]);
                    if (s + 1 < RN - r) { T.Add(idx[r + 1, s]); T.Add(idx[r + 1, s + 1]); T.Add(idx[r, s + 1]); }
                }
        }
        var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, name = "bodem rond" };
        m.SetVertices(V); m.SetNormals(N); m.SetColors(C); m.SetUVs(0, X); m.SetTriangles(T, 0); m.RecalculateBounds();
        return m;
    }
}
