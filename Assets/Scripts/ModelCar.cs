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
                for (int q = 1; q < n - 1; q++) { tris[mn].Add(b0); tris[mn].Add(b0 + q); tris[mn].Add(b0 + q + 1); }   // waaier
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
