using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// Een auto uit een bestand in Resources/Autos (gemaakt uit een .blend met Tools/blend_naar_auto.py): carrosserie en één wiel,
// in auto-coördinaten (x rechts, y op, z vooruit, meters, oorsprong op het wegdek). Per vlak een submesh per materiaal; vlakke
// vlakken krijgen hun eigen normaal (low poly), gladde de normalen uit het bestand. De wielen draaien en sturen mee (CarWheels).
public static class ModelCar
{
    class Part { public Mesh mesh; public List<(string name, Color c, float metal, float rough, bool emit)> mats = new List<(string, Color, float, float, bool)>(); }
    class Model { public Part body, wheel; public List<Vector3> wheels = new List<Vector3>(); public float radius = 0.4f; }
    static readonly Dictionary<string, Model> cache = new Dictionary<string, Model>();
    public static void ResetStatics() { cache.Clear(); }

    public static bool Has(string name) => Load(name) != null;
    public static float WheelRadius(string name) { var m = Load(name); return m != null ? m.radius : 0.36f; }

    // de auto als kind van parent; false als het bestand er niet is
    public static bool Build(Transform parent, string name)
    {
        var M = Load(name); if (M == null) return false;
        Put(parent, M.body, name);
        var cw = parent.gameObject.GetComponent<CarWheels>(); if (cw == null) cw = parent.gameObject.AddComponent<CarWheels>();
        for (int k = 0; k < M.wheels.Count && k < 4; k++)
        {
            Vector3 c = M.wheels[k];
            var axle = new GameObject("as").transform; axle.SetParent(parent, false); axle.localPosition = c; axle.gameObject.layer = parent.gameObject.layer;
            var wheel = new GameObject("wiel").transform; wheel.SetParent(axle, false); wheel.gameObject.layer = parent.gameObject.layer;
            Put(wheel, M.wheel, name);
            cw.Set(k, axle, wheel, c.x < 0f, c.z > 0f);   // het wiel in het bestand is een rechterwiel; links draait de as het om
        }
        cw.Drive(0f, 0f, 0f);
        return true;
    }

    public const float ArchUp = 0.2f;   // zoveel ruimte boven de band in de wielkast (het wiel mag niet verder omhoog veren)

    // ear clipping in het vlak van de veelhoek: telkens een bolle hoek zonder andere punten erin afknippen
    static void Triangulate(Vector3[] pv, int[] idx, Vector3 fn, int b0, List<int> tris)
    {
        int n = idx.Length;
        if (n == 3) { tris.Add(b0); tris.Add(b0 + 1); tris.Add(b0 + 2); return; }
        Vector3 ax = Vector3.Cross(fn, Mathf.Abs(fn.x) < 0.9f ? Vector3.right : Vector3.up).normalized, ay = Vector3.Cross(fn, ax);
        var p2 = new Vector2[n]; for (int q = 0; q < n; q++) p2[q] = new Vector2(Vector3.Dot(pv[idx[q]], ax), Vector3.Dot(pv[idx[q]], ay));
        float area = 0f; for (int q = 0; q < n; q++) { Vector2 a = p2[q], b = p2[(q + 1) % n]; area += a.x * b.y - b.x * a.y; }
        float sg = area >= 0f ? 1f : -1f;
        var rest = new List<int>(); for (int q = 0; q < n; q++) rest.Add(q);
        int guard = n * n;
        while (rest.Count > 3 && guard-- > 0)
        {
            bool cut = false;
            for (int k = 0; k < rest.Count; k++)
            {
                int ia = rest[(k + rest.Count - 1) % rest.Count], ib = rest[k], ic = rest[(k + 1) % rest.Count];
                Vector2 a = p2[ia], b = p2[ib], c = p2[ic];
                if (((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x)) * sg <= 1e-9f) continue;   // hol of plat: geen oor
                bool inside = false;
                foreach (int o in rest) { if (o == ia || o == ib || o == ic) continue; if (In(p2[o], a, b, c)) { inside = true; break; } }
                if (inside) continue;
                tris.Add(b0 + ia); tris.Add(b0 + ib); tris.Add(b0 + ic); rest.RemoveAt(k); cut = true; break;
            }
            if (!cut) break;
        }
        if (rest.Count >= 3) for (int q = 1; q < rest.Count - 1; q++) { tris.Add(b0 + rest[0]); tris.Add(b0 + rest[q]); tris.Add(b0 + rest[q + 1]); }   // wat overblijft (of een vreemd vlak): waaier
    }
    static bool In(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y), d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y), d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
        bool neg = d1 < 0f || d2 < 0f || d3 < 0f, pos = d1 > 0f || d2 > 0f || d3 > 0f; return !(neg && pos);
    }

    static void Put(Transform parent, Part p, string model)
    {
        var go = new GameObject("deel"); go.transform.SetParent(parent, false); go.layer = parent.gameObject.layer;
        go.AddComponent<MeshFilter>().sharedMesh = p.mesh;
        var mats = new Material[p.mats.Count];
        for (int i = 0; i < mats.Length; i++)
        {
            var (n, c, metal, rough, emit) = p.mats[i];
            if (emit) { mats[i] = Mats.GetUnlit("auto_" + model + "_" + n, c); continue; }
            if (metal > 0.3f) c = Color.Lerp(c, Color.white, metal * 0.3f);   // geborsteld staal: lichter, want zonder reflectieprobe wordt metaal donker
            var m = Mats.Get("auto_" + model + "_" + n, c);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal * 0.3f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", Mathf.Clamp01(1f - rough) * 0.8f);
            mats[i] = m;
        }
        go.AddComponent<MeshRenderer>().sharedMaterials = mats;
    }

    static Model Load(string name)
    {
        if (cache.TryGetValue(name, out var M) && (M == null || M.body.mesh != null)) return M;
        var ta = Resources.Load<TextAsset>("Autos/" + name);
        if (ta == null) { cache[name] = null; return null; }
        M = new Model(); var lines = ta.text.Split('\n'); int li = 0;
        float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
        Part ReadPart()
        {
            var h = lines[li++].Trim().Split(' '); int nv = int.Parse(h[2]), nf = int.Parse(h[3]), nm = int.Parse(h[4]);
            var P = new Part();
            for (int k = 0; k < nm; k++) { var t = lines[li++].Trim().Split(' '); P.mats.Add((t[1], new Color(F(t[2]), F(t[3]), F(t[4])), F(t[5]), F(t[6]), t[7] == "1")); }
            var pv = new Vector3[nv]; var pn = new Vector3[nv];
            for (int k = 0; k < nv; k++) { var t = lines[li++].Trim().Split(' '); pv[k] = new Vector3(F(t[1]), F(t[2]), F(t[3])); pn[k] = new Vector3(F(t[4]), F(t[5]), F(t[6])); }
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var tris = new List<int>[nm]; for (int k = 0; k < nm; k++) tris[k] = new List<int>();
            for (int k = 0; k < nf; k++)
            {
                var t = lines[li++].Trim().Split(' '); int mn = Mathf.Clamp(int.Parse(t[1]), 0, nm - 1); bool smooth = t[2] == "1";
                int n = t.Length - 3; if (n < 3) continue; var idx = new int[n];
                for (int q = 0; q < n; q++) idx[n - 1 - q] = int.Parse(t[3 + q]);   // omgekeerde volgorde: Blender is rechtshandig, Unity linkshandig
                Vector3 fn = Vector3.zero;   // de normaal van het vlak (Newell, ook voor een vijf- of meerhoek)
                for (int q = 0; q < n; q++) { Vector3 a = pv[idx[q]], b = pv[idx[(q + 1) % n]]; fn += Vector3.Cross(a, b); }
                fn = fn.normalized;   // na het omkeren wijst hij naar buiten, net als Cross(b-a, c-a) in Unity
                int b0 = verts.Count;
                for (int q = 0; q < n; q++) { verts.Add(pv[idx[q]]); norms.Add(smooth ? pn[idx[q]] : fn); }
                Triangulate(pv, idx, fn, b0, tris[mn]);   // ook holle vlakken (een waaier gaf een driehoek dwars over de wielkast)
            }
            var mesh = new Mesh { name = name }; mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.subMeshCount = nm;
            for (int k = 0; k < nm; k++) mesh.SetTriangles(tris[k], k);
            mesh.RecalculateBounds(); P.mesh = mesh; return P;
        }
        M.body = ReadPart();
        while (li < lines.Length && lines[li].StartsWith("wheel")) { var t = lines[li++].Trim().Split(' '); M.wheels.Add(new Vector3(F(t[1]), F(t[2]), F(t[3]))); }
        M.wheel = ReadPart();
        if (M.wheels.Count > 0) M.radius = M.wheels[0].y;
        cache[name] = M; return M;
    }
}
