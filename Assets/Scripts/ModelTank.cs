using System.Collections.Generic;
using UnityEngine;

// De K3-tank (Assets/Resources/Voertuigen/K3NGMBT, origineel in Assets/ThirdParty/Military/K3NGMBT) als speelbare tank: het model
// komt als OBJ binnen met een object per onderdeel, allemaal met dezelfde oorsprong. Hier krijgen koepel, kanon en wielen een eigen
// draaipunt (een leeg object op de juiste plek, de onderdelen eronder), en eigen URP-materialen met de textures. Camouflagenetten
// staan uit. Lukt het niet (bestand weg, of het model komt als één geheel binnen), dan geeft Build null en wordt het de eigen tank
// (TankModel). Assen in de tank: x rechts, y omhoog, z vooruit (de loop wijst naar +z).
public static class ModelTank
{
    const string Path = "Voertuigen/K3NGMBT/";
    static Material body, track, glass;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { body = track = glass = null; }

    // onderdelen die met de koepel meedraaien, en die met het kanon meekantelen (naam begint met)
    static readonly string[] TurretParts = { "Turret", "CommandTower", "ThermalVisor", "KAZ", "DaewooK4", "K6HMG", "Machinegun", "RocketLauncher", "SmokeLauncher", "Camo_Net_Turret", "Flagpole" };
    static readonly string[] GunParts = { "Gun", "Barrel", "Camo_Net_Gun" };

    public static TankRig Build(Transform root)
    {
        var prefab = Resources.Load<GameObject>(Path + "K3NGMBT"); if (prefab == null) return null;
        var model = Object.Instantiate(prefab, root, false); model.name = "K3";
        var parts = new Dictionary<string, Transform>();
        foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true)) parts[mf.gameObject.name] = mf.transform;
        if (!parts.ContainsKey("Turret") || !parts.ContainsKey("Barrel")) { Object.Destroy(model); return null; }
        Mats();
        foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (r.gameObject.name.StartsWith("Camo_Net")) { r.gameObject.SetActive(false); continue; }
            var ms = r.sharedMaterials; for (int k = 0; k < ms.Length; k++) { string n = ms[k] != null ? ms[k].name : ""; ms[k] = n.Contains("Track") ? track : n.Contains("Glass") ? glass : body; }
            r.sharedMaterials = ms;
        }
        // de maten in tank-coördinaten (het model staat in de oorsprong van root)
        Bounds B(string n) { var t = parts[n]; var b = t.GetComponent<MeshFilter>().sharedMesh.bounds; var c = root.InverseTransformPoint(t.TransformPoint(b.center)); var e = Vector3.Scale(b.extents, Abs(t.lossyScale)); return new Bounds(c, e * 2f); }
        // de loop hoort naar voren (+z) te wijzen; Unity's OBJ-import spiegelt een as, dan staat het model achterstevoren: omdraaien
        if (B("Barrel").center.z < B("Turret").center.z) model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        var tb = B("Turret"); var bb = B("Barrel"); var gb = parts.ContainsKey("Gun") ? B("Gun") : bb;
        var rig = root.gameObject.AddComponent<TankRig>();
        rig.turret = Pivot(root, "koepel", new Vector3(tb.center.x, tb.min.y, tb.center.z));
        rig.gun = Pivot(rig.turret, "kanon", new Vector3(bb.center.x, bb.center.y, gb.min.z + 0.4f), root);
        rig.muzzle = Pivot(rig.gun, "loopmond", new Vector3(bb.center.x, bb.center.y, bb.max.z), root);
        foreach (var kv in parts)
        {
            if (Starts(kv.Key, GunParts)) kv.Value.SetParent(rig.gun, true);
            else if (Starts(kv.Key, TurretParts)) kv.Value.SetParent(rig.turret, true);
        }
        // wielen: elk een draaipunt in zijn midden; links (x < 0) en rechts
        var road = new List<(Vector3 c, float r)>();
        foreach (var kv in parts)
        {
            if (!kv.Key.StartsWith("Wheel_")) continue;
            var wb = B(kv.Key); var piv = Pivot(root, "as " + kv.Key, wb.center); kv.Value.SetParent(piv, true);
            rig.AddWheel(piv, wb.center.x < 0f, Mathf.Max(0.1f, wb.extents.y));
            if (kv.Key.Contains("_Road_")) road.Add((wb.center, wb.extents.y));
        }
        // de vering: zes punten (voor, midden, achter; links en rechts) onder de loopwielen; de straal tot de grond loopt door de rups
        road.Sort((a, b) => b.c.z.CompareTo(a.c.z));
        float hubY = 0f; foreach (var w in road) hubY += w.c.y; hubY = road.Count > 0 ? hubY / road.Count : 0.37f;
        float zf = road.Count > 0 ? road[0].c.z : 2f, zr = road.Count > 0 ? road[road.Count - 1].c.z : -2f, xw = 0f; foreach (var w in road) xw = Mathf.Max(xw, Mathf.Abs(w.c.x)); if (xw <= 0f) xw = 1.4f;
        rig.PhysWheels = new[] { new Vector3(-xw, 0f, zf), new Vector3(xw, 0f, zf), new Vector3(-xw, 0f, (zf + zr) * 0.5f), new Vector3(xw, 0f, (zf + zr) * 0.5f), new Vector3(-xw, 0f, zr), new Vector3(xw, 0f, zr) };
        rig.WheelR = hubY;   // het hart van een loopwiel boven de onderkant van de rups
        rig.TrackHalf = xw;
        var hb = parts.ContainsKey("Hull") ? B("Hull") : tb;
        rig.HullBox = new Bounds(new Vector3(0f, (0.55f + hb.max.y) * 0.5f, hb.center.z), new Vector3(hb.size.x, hb.max.y - 0.55f, hb.size.z * 0.97f));
        rig.TopBox = new Bounds(new Vector3(tb.center.x, (tb.min.y + tb.max.y) * 0.5f + 0.05f, tb.center.z), new Vector3(tb.size.x * 0.9f, tb.size.y * 0.8f, tb.size.z * 0.9f));
        // de rupsen: elk een eigen materiaal waarvan de textuur meeschuift (u loopt langs de rups, zo'n 0,84 per meter)
        foreach (string n in new[] { "Track_L", "Track_R" })
        {
            if (!parts.TryGetValue(n, out var tt)) continue;
            var r = tt.GetComponent<MeshRenderer>(); var m = new Material(track); var ms = r.sharedMaterials; for (int k = 0; k < ms.Length; k++) ms[k] = m; r.sharedMaterials = ms;
            rig.SetTrack(B(n).center.x < 0f, m, 0.84f);
        }
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = root.gameObject.layer;
        return rig;
    }

    static bool Starts(string n, string[] pre) { foreach (var p in pre) if (n.StartsWith(p)) return true; return false; }
    static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    // een leeg object op een plek in tank-coördinaten (frame: root), onder parent
    static Transform Pivot(Transform parent, string name, Vector3 at, Transform frame = null)
    {
        var g = new GameObject(name).transform; g.SetParent(parent, false);
        g.position = (frame ?? parent).TransformPoint(at); g.rotation = (frame ?? parent).rotation; return g;
    }

    static void Mats()
    {
        if (body != null && track != null && glass != null) return;
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        Material M(string a, string n, string e, float smooth, float metal)
        {
            var m = new Material(lit); var ta = Resources.Load<Texture2D>(Path + a); if (ta != null) m.SetTexture("_BaseMap", ta);
            m.SetColor("_BaseColor", Color.white); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
            var tn = n != null ? Resources.Load<Texture2D>(Path + n) : null; if (tn != null) { m.SetTexture("_BumpMap", tn); m.EnableKeyword("_NORMALMAP"); }
            var te = e != null ? Resources.Load<Texture2D>(Path + e) : null; if (te != null) { m.SetTexture("_EmissionMap", te); m.SetColor("_EmissionColor", Color.white); m.EnableKeyword("_EMISSION"); }
            return m;
        }
        body = M("textures/baked/K3NGMBT_A_Default", "textures/K3NGMBT_N", "textures/K3NGMBT_EM", 0.35f, 0.2f);
        track = M("textures/K3NGMBT_Track_A", "textures/K3NGMBT_Track_N", null, 0.2f, 0.3f);
        glass = new Material(lit); glass.SetColor("_BaseColor", new Color(0.05f, 0.07f, 0.08f)); glass.SetFloat("_Smoothness", 0.9f);
    }
}
