using System.Collections.Generic;
using UnityEngine;

// Een futuristische tank (eigen ontwerp, low poly in de stijl van de stad: zeshoekige doorsneden en schuine vlakken). Romp met
// overhangende flanken boven de rupsen, rupsen met schorten en bewegende schakels, een zeshoekige koepel die los draait en een
// railkanon met lichtende rails. Assen: x rechts, y omhoog, z vooruit, meters, oorsprong op de grond in het midden.
// Build geeft een TankRig terug: koepel (draait om y), kanon (kantelt om x), loopmond, en de rupsen die met de snelheid meelopen.
public static class TankModel
{
    public const float Length = 7.0f, Width = 3.5f, TrackX0 = 1.08f, TrackX1 = 1.72f, WheelR = 0.45f;
    public static readonly Vector3[] Wheels = { new Vector3(-1.4f, 0f, 2.3f), new Vector3(1.4f, 0f, 2.3f), new Vector3(-1.4f, 0f, 0f), new Vector3(1.4f, 0f, 0f), new Vector3(-1.4f, 0f, -2.3f), new Vector3(1.4f, 0f, -2.3f) };
    static readonly Color Hull = new Color(0.25f, 0.28f, 0.31f), Dark = new Color(0.11f, 0.12f, 0.14f), Tread = new Color(0.17f, 0.18f, 0.19f), Glow = new Color(0.2f, 0.92f, 1f), Lamp = new Color(1f, 0.97f, 0.88f), Rear = new Color(1f, 0.2f, 0.18f);

    public static TankRig Build(Transform root)
    {
        MeshBuilder hull = new MeshBuilder(), dark = new MeshBuilder(), glow = new MeshBuilder(), lamp = new MeshBuilder(), rear = new MeshBuilder();
        // ---- romp: doorsneden langs z, elk een zeshoek (onder smal tussen de rupsen, op de gordel breed over de rupsen, boven iets smaller)
        var S = new List<float[]> {   // z, yOnder, yGordel, yBoven, bOnder, bGordel, bBoven
            new[] { -3.45f, 0.62f, 1.02f, 1.42f, 1.0f, 1.66f, 1.42f },
            new[] { -3.2f, 0.48f, 1.08f, 1.6f, 1.06f, 1.76f, 1.5f },
            new[] { 2.35f, 0.48f, 1.08f, 1.6f, 1.06f, 1.76f, 1.5f },
            new[] { 3.5f, 0.72f, 0.98f, 1.12f, 0.98f, 1.62f, 1.3f } };   // schuin voorpantser
        Loft(hull, S);
        // lichtlijnen: langs de gordel, een lichtbalk voor en rood achter
        foreach (int sg in new[] { -1, 1 }) Bar(glow, new Vector3(sg * 1.775f, 1.08f, -3.1f), new Vector3(sg * 1.775f, 1.08f, 2.3f), 0.035f);
        Bar(lamp, new Vector3(-1.1f, 0.99f, 3.47f), new Vector3(1.1f, 0.99f, 3.47f), 0.04f);
        Bar(rear, new Vector3(-1.2f, 1.25f, -3.47f), new Vector3(1.2f, 1.25f, -3.47f), 0.045f);
        // ---- rupsen: per kant een zijaanzicht als achthoek, met een schort erover (donker) en een gloeilijn op de schort
        var prof = new[] { new Vector2(3.5f, 0.55f), new Vector2(3.2f, 0.12f), new Vector2(2.55f, 0f), new Vector2(-2.55f, 0f), new Vector2(-3.2f, 0.12f), new Vector2(-3.5f, 0.55f), new Vector2(-3.25f, 0.98f), new Vector2(3.25f, 0.98f) };
        foreach (int sg in new[] { -1, 1 })
        {
            Extrude(dark, prof, sg * TrackX0, sg * (TrackX1 - 0.02f));
            var skirt = new[] { new Vector2(3.15f, 0.5f), new Vector2(-3.15f, 0.5f), new Vector2(-3.35f, 0.98f), new Vector2(3.35f, 0.98f) };
            Extrude(hull, skirt, sg * (TrackX1 - 0.04f), sg * (TrackX1 + 0.05f));
            Bar(glow, new Vector3(sg * (TrackX1 + 0.06f), 0.62f, -2.9f), new Vector3(sg * (TrackX1 + 0.06f), 0.62f, 2.9f), 0.025f);
        }
        Put(root, "romp", hull, Mats.Get("tank_romp", Hull)); Put(root, "donker", dark, Mats.Get("tank_donker", Dark));
        Put(root, "gloed", glow, Mats.GetUnlit("tank_gloed", Glow)); Put(root, "lamp", lamp, Mats.GetUnlit("tank_lamp", Lamp)); Put(root, "achter", rear, Mats.GetUnlit("tank_rood", Rear));

        // ---- koepel: een zeshoekig prisma, langgerekt, schuine wanden, met een uitbouw achter
        var turret = new GameObject("koepel").transform; turret.SetParent(root, false); turret.localPosition = new Vector3(0f, 1.6f, -0.35f);
        MeshBuilder th = new MeshBuilder(), td = new MeshBuilder(), tg = new MeshBuilder();
        Vector3[] Ring(float r, float y, float zs, float zo) { var a = new Vector3[6]; for (int k = 0; k < 6; k++) { float ang = (k * 60f + 30f) * Mathf.Deg2Rad; a[k] = new Vector3(Mathf.Sin(ang) * r, y, Mathf.Cos(ang) * r * zs + zo); } return a; }
        var r0 = Ring(1.32f, 0f, 1.35f, 0f); var r1 = Ring(1.05f, 0.72f, 1.3f, -0.05f);
        Band(th, r0, r1); Cap(th, r1, true); Cap(th, r0, false);
        Box(th, new Vector3(0f, 0.32f, -1.85f), new Vector3(1.5f, 0.52f, 0.7f));   // uitbouw achter
        Box(td, new Vector3(0f, 0.05f, 0f), new Vector3(2.0f, 0.1f, 2.6f));        // de ring onder de koepel
        Box(td, new Vector3(0.45f, 0.78f, -0.4f), new Vector3(0.62f, 0.1f, 0.62f)); // luik
        Box(td, new Vector3(-0.55f, 0.85f, 0.35f), new Vector3(0.36f, 0.24f, 0.32f)); // sensor
        Box(tg, new Vector3(-0.55f, 0.86f, 0.52f), new Vector3(0.24f, 0.07f, 0.02f)); // het oog van de sensor
        Box(td, new Vector3(0.65f, 1.4f, -1.9f), new Vector3(0.03f, 1.3f, 0.03f));  // antenne
        foreach (int sg in new[] { -1, 1 }) Bar(tg, new Vector3(sg * 1.3f, 0.3f, -0.9f), new Vector3(sg * 1.3f, 0.3f, 1.0f), 0.025f);
        Put(turret, "koepel", th, Mats.Get("tank_romp", Hull)); Put(turret, "koepel donker", td, Mats.Get("tank_donker", Dark)); Put(turret, "koepel gloed", tg, Mats.GetUnlit("tank_gloed", Glow));
        // ---- kanon: kantelt om een as voor in de koepel; zeshoekige loop met twee lichtende rails en een mondstuk
        var gun = new GameObject("kanon").transform; gun.SetParent(turret, false); gun.localPosition = new Vector3(0f, 0.38f, 1.55f);
        MeshBuilder gh = new MeshBuilder(), gd = new MeshBuilder(), gg = new MeshBuilder();
        Box(gh, new Vector3(0f, 0f, 0.05f), new Vector3(0.9f, 0.46f, 0.5f));   // mantel
        Tube(gd, 0.15f, 0.12f, 0.2f, 4.4f);                                     // de loop
        foreach (int sg in new[] { -1, 1 }) Box(gh, new Vector3(sg * 0.2f, 0f, 2.3f), new Vector3(0.07f, 0.16f, 3.9f));   // rails
        foreach (int sg in new[] { -1, 1 }) Box(gg, new Vector3(sg * 0.245f, 0f, 2.3f), new Vector3(0.02f, 0.06f, 3.7f));
        Tube(gh, 0.24f, 0.24f, 4.35f, 4.85f);                                   // mondstuk
        Tube(gg, 0.255f, 0.255f, 4.5f, 4.56f); Tube(gg, 0.255f, 0.255f, 4.68f, 4.74f);   // lichtringen
        Put(gun, "kanon", gh, Mats.Get("tank_romp", Hull)); Put(gun, "loop", gd, Mats.Get("tank_donker", Dark)); Put(gun, "kanon gloed", gg, Mats.GetUnlit("tank_gloed", Glow));
        var muzzle = new GameObject("loopmond").transform; muzzle.SetParent(gun, false); muzzle.localPosition = new Vector3(0f, 0f, 4.9f);

        // ---- schakels: blokjes die over de omtrek van de rups lopen (de onderkant en voor- en achterkant zijn zichtbaar onder de schort)
        var rig = root.gameObject.AddComponent<TankRig>();
        rig.turret = turret; rig.gun = gun; rig.muzzle = muzzle;
        rig.PhysWheels = Wheels; rig.WheelR = WheelR; rig.TrackHalf = (TrackX0 + TrackX1) * 0.5f;
        rig.HullBox = new Bounds(new Vector3(0f, 1.05f, 0f), new Vector3(Width * 0.97f, 1.0f, Length * 0.96f)); rig.TopBox = new Bounds(new Vector3(0f, 2.0f, -0.4f), new Vector3(2.4f, 0.8f, 3.2f));
        var path = new List<Vector2>(prof); rig.SetPath(path);
        var tm = Mats.Get("tank_schakel", Tread);
        var cleat = new MeshBuilder(); Box(cleat, Vector3.zero, new Vector3(TrackX1 - TrackX0 + 0.06f, 0.07f, 0.16f)); var cm = cleat.Build();
        for (int side = 0; side < 2; side++)
            for (int k = 0; k < TankRig.Cleats; k++)
            {
                var g = new GameObject("schakel"); g.layer = root.gameObject.layer; g.transform.SetParent(root, false);
                g.AddComponent<MeshFilter>().sharedMesh = cm; g.AddComponent<MeshRenderer>().sharedMaterial = tm;
                rig.cleats[side].Add(g.transform);
            }
        rig.Move(0f, 0f);
        foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.layer = root.gameObject.layer;
        return rig;
    }

    static void Put(Transform parent, string name, MeshBuilder mb, Material m)
    {
        if (mb.v.Count == 0) return;
        var g = new GameObject(name); g.transform.SetParent(parent, false); g.layer = parent.gameObject.layer;
        g.AddComponent<MeshFilter>().sharedMesh = mb.Build(); g.AddComponent<MeshRenderer>().sharedMaterial = m;
    }
    static Vector3[] Sec(float[] s) => new[] { new Vector3(-s[4], s[1], s[0]), new Vector3(-s[5], s[2], s[0]), new Vector3(-s[6], s[3], s[0]), new Vector3(s[6], s[3], s[0]), new Vector3(s[5], s[2], s[0]), new Vector3(s[4], s[1], s[0]) };
    static void Loft(MeshBuilder mb, List<float[]> S)
    {
        for (int i = 0; i + 1 < S.Count; i++) Band(mb, Sec(S[i]), Sec(S[i + 1]));
        Cap(mb, Sec(S[0]), false); Cap(mb, Sec(S[S.Count - 1]), true);
    }
    // een band tussen twee ringen met evenveel punten; de buitenkant wijst weg van de as door het midden van beide ringen
    static void Band(MeshBuilder mb, Vector3[] A, Vector3[] B)
    {
        Vector3 ca = Mid(A), cb = Mid(B); int n = A.Length;
        for (int k = 0; k < n; k++)
        {
            int k2 = (k + 1) % n; Vector3 c = (ca + cb) * 0.5f, f = (A[k] + A[k2] + B[k] + B[k2]) * 0.25f, axis = (cb - ca).normalized; Vector3 o = f - c; o -= axis * Vector3.Dot(o, axis);
            mb.Quad(A[k], A[k2], B[k2], B[k], o);
        }
    }
    // een kopse kant: front = de kant waar de normaal naartoe wijst (true: naar de volgende ring toe, dus de laatste ring dicht)
    static void Cap(MeshBuilder mb, Vector3[] R, bool front)
    {
        Vector3 c = Mid(R), n = Vector3.Cross(R[1] - R[0], R[2] - R[0]).normalized;
        Vector3 o = front ? Vector3.forward : Vector3.back;
        if (Mathf.Abs(n.z) < 0.3f) o = front ? Vector3.up : Vector3.down;   // een liggende ring (koepel): boven en onder
        for (int k = 0; k < R.Length; k++) mb.Tri(c, R[k], R[(k + 1) % R.Length], o);
    }
    static Vector3 Mid(Vector3[] R) { Vector3 s = Vector3.zero; foreach (var p in R) s += p; return s / R.Length; }
    // een vlak profiel in (z, y) uitgetrokken over x van x0 tot x1
    static void Extrude(MeshBuilder mb, Vector2[] P, float x0, float x1)
    {
        int n = P.Length; Vector2 c = Vector2.zero; foreach (var p in P) c += p; c /= n;
        for (int k = 0; k < n; k++)
        {
            Vector2 a = P[k], b = P[(k + 1) % n]; Vector2 m = (a + b) * 0.5f - c;
            mb.Quad(new Vector3(x0, a.y, a.x), new Vector3(x0, b.y, b.x), new Vector3(x1, b.y, b.x), new Vector3(x1, a.y, a.x), new Vector3(0f, m.y, m.x));
        }
        float sx = Mathf.Sign(x1 - x0);
        for (int k = 1; k + 1 < n; k++)
        {
            mb.Tri(new Vector3(x1, P[0].y, P[0].x), new Vector3(x1, P[k].y, P[k].x), new Vector3(x1, P[k + 1].y, P[k + 1].x), Vector3.right * sx);
            mb.Tri(new Vector3(x0, P[0].y, P[0].x), new Vector3(x0, P[k].y, P[k].x), new Vector3(x0, P[k + 1].y, P[k + 1].x), Vector3.left * sx);
        }
    }
    // een zeshoekige buis langs z van z0 tot z1, straal r0 achter en r1 voor, met dichte uiteinden
    static void Tube(MeshBuilder mb, float r0, float r1, float z0, float z1)
    {
        var A = new Vector3[6]; var B = new Vector3[6];
        for (int k = 0; k < 6; k++) { float a = k * 60f * Mathf.Deg2Rad; A[k] = new Vector3(Mathf.Cos(a) * r0, Mathf.Sin(a) * r0, z0); B[k] = new Vector3(Mathf.Cos(a) * r1, Mathf.Sin(a) * r1, z1); }
        Band(mb, A, B); Cap(mb, A, false); Cap(mb, B, true);
    }
    // een dunne staaf van a naar b (doorsnede s bij s)
    static void Bar(MeshBuilder mb, Vector3 a, Vector3 b, float s)
    {
        Vector3 c = (a + b) * 0.5f, d = b - a; float l = d.magnitude; if (l < 1e-4f) return;
        var q = Quaternion.LookRotation(d / l, Mathf.Abs(Vector3.Dot(d / l, Vector3.up)) > 0.9f ? Vector3.forward : Vector3.up);
        Box(mb, c, new Vector3(s, s, l), q);
    }
    static void Box(MeshBuilder mb, Vector3 c, Vector3 size) => Box(mb, c, size, Quaternion.identity);
    static void Box(MeshBuilder mb, Vector3 c, Vector3 size, Quaternion q)
    {
        Vector3 h = size * 0.5f;
        Vector3 P(int i, int j, int k) => c + q * new Vector3(i * h.x, j * h.y, k * h.z);
        Vector3 F = q * Vector3.forward, Rt = q * Vector3.right, Up = q * Vector3.up;
        mb.Quad(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), F); mb.Quad(P(-1, -1, -1), P(1, -1, -1), P(1, 1, -1), P(-1, 1, -1), -F);
        mb.Quad(P(1, -1, -1), P(1, -1, 1), P(1, 1, 1), P(1, 1, -1), Rt); mb.Quad(P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), -Rt);
        mb.Quad(P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1), P(-1, 1, 1), Up); mb.Quad(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), -Up);
    }
}

// De bewegende delen van de tank: koepel en kanon (gericht door PlanetBuilder.Tank) en de schakels van de rupsen, die met de
// snelheid van elke rups meelopen (links en rechts apart: bij draaien op de plaats lopen ze tegen elkaar in).
public class TankRig : MonoBehaviour
{
    public const int Cleats = 30;
    public Transform turret, gun, muzzle;
    // voor de fysica (PlayerCar): de veerpunten (paren links, rechts), het hart van een wiel boven de grond, de halve spoorbreedte
    // en de botsvormen van romp en koepel
    public Vector3[] PhysWheels; public float WheelR = 0.45f, TrackHalf = 1.4f; public Bounds HullBox, TopBox;
    // wielen van een ingeladen model (ModelTank): draaien om hun as met de rups van hun kant mee
    readonly List<(Transform t, bool left, float r)> wheels = new List<(Transform, bool, float)>();
    public void AddWheel(Transform t, bool left, float r) { wheels.Add((t, left, r)); }
    // rupsen van een ingeladen model: de textuur schuift mee (k: textuurlengte per meter)
    readonly Material[] trackMat = new Material[2]; float trackK; readonly float[] trackOfs = new float[2];
    // terugslag: de rem van het kanon vangt het meeste op; de tank schuift een paar decimeter per seconde terug en wiegt even mee
    // (eerder een stoot op de loopmond, hoog en ver van het zwaartepunt: een schot opzij gooide hem op zijn kant)
    public static void Recoil(Rigidbody rb, Vector3 dir)
    {
        if (rb == null) return; Vector3 up = rb.rotation * Vector3.up, f = dir - up * Vector3.Dot(dir, up);
        rb.AddForce(-dir * 0.3f, ForceMode.VelocityChange);
        if (f.sqrMagnitude > 1e-4f) rb.AddTorque(Vector3.Cross(up, -f.normalized) * 0.1f, ForceMode.VelocityChange);
    }
    // de vering zichtbaar (ingeladen model): de loopwielen gaan op en neer met de veerpunten aan hun kant, en de onderkant van de
    // rups buigt mee (de punten van de rupsmesh onder het hart van de loopwielen schuiven mee omhoog of omlaag; naar boven toe minder).
    // Voor het voorste en achterste loopwiel loopt het uit naar het spanwiel en het aandrijfwiel, die blijven op hun plek.
    readonly List<(Transform t, Vector3 basePos, bool left)> roads = new List<(Transform, Vector3, bool)>();
    class TrackMesh { public Mesh mesh; public Vector3[] baseV, work; public float[] z, w; public bool left; public Vector3 up; }
    readonly List<TrackMesh> trackMeshes = new List<TrackMesh>(); float[] sus;
    public void AddRoad(Transform t, bool left) { roads.Add((t, t.localPosition, left)); }
    public void AddTrackMesh(MeshFilter mf, bool left, float hubY)
    {
        var m = Instantiate(mf.sharedMesh); m.MarkDynamic(); mf.sharedMesh = m;
        var v = m.vertices; var tm = new TrackMesh { mesh = m, baseV = v, work = (Vector3[])v.Clone(), z = new float[v.Length], w = new float[v.Length], left = left, up = mf.transform.InverseTransformVector(transform.up) };
        for (int i = 0; i < v.Length; i++)
        {
            Vector3 q = transform.InverseTransformPoint(mf.transform.TransformPoint(v[i])); tm.z[i] = q.z;
            tm.w[i] = q.y <= hubY ? 1f : Mathf.Clamp01(1f - (q.y - hubY) / 0.4f);
        }
        trackMeshes.Add(tm);
    }
    // comp: hoeveel elk veerpunt is ingeveerd (m); sag: hoeveel dat in rust is (daar tekent het model de wielen)
    public void Suspend(float[] comp, float sag, float dt)
    {
        if (comp == null || PhysWheels == null || (roads.Count == 0 && trackMeshes.Count == 0)) return;
        int n = Mathf.Min(comp.Length, PhysWheels.Length); if (sus == null || sus.Length != n) sus = new float[n];
        float k = 1f - Mathf.Exp(-dt * 25f);
        for (int i = 0; i < n; i++) sus[i] = Mathf.Lerp(sus[i], Mathf.Clamp(comp[i] - sag, -0.18f, 0.3f), k);
        foreach (var r in roads) if (r.t != null) r.t.localPosition = r.basePos + Vector3.up * Travel(r.left, r.basePos.z);
        foreach (var tm in trackMeshes)
        {
            for (int i = 0; i < tm.baseV.Length; i++) tm.work[i] = tm.w[i] > 0f ? tm.baseV[i] + tm.up * (Travel(tm.left, tm.z[i]) * tm.w[i]) : tm.baseV[i];
            tm.mesh.vertices = tm.work;
        }
    }
    // de uitslag van de vering op lengte z aan één kant: tussen de veerpunten lineair, voorbij het voorste en achterste punt naar 0
    float Travel(bool left, float z)
    {
        float zf = -1e9f, zr = 1e9f, vf = 0f, vr = 0f, za = 1e9f, va = 0f, zb = -1e9f, vb = 0f;
        for (int i = 0; i < sus.Length; i++)
        {
            if ((PhysWheels[i].x < 0f) != left) continue; float pz = PhysWheels[i].z;
            if (pz > zf) { zf = pz; vf = sus[i]; } if (pz < zr) { zr = pz; vr = sus[i]; }
            if (pz >= z && pz < za) { za = pz; va = sus[i]; } if (pz <= z && pz > zb) { zb = pz; vb = sus[i]; }
        }
        if (z > zf) return vf * Mathf.Clamp01(1f - (z - zf) / 0.9f);
        if (z < zr) return vr * Mathf.Clamp01(1f - (zr - z) / 0.9f);
        return za - zb > 1e-4f ? Mathf.Lerp(vb, va, (z - zb) / (za - zb)) : va;
    }
    public void SetTrack(bool left, Material m, float k) { trackMat[left ? 0 : 1] = m; trackK = k; }
    public readonly List<Transform>[] cleats = { new List<Transform>(), new List<Transform>() };
    readonly List<Vector2> path = new List<Vector2>(); readonly List<float> acc = new List<float>(); float total; readonly float[] run = new float[2];
    public void SetPath(List<Vector2> p)
    {
        path.Clear(); acc.Clear(); total = 0f;
        // iets buiten het profiel, zodat de schakels op de rups liggen
        Vector2 c = Vector2.zero; foreach (var q in p) c += q; c /= p.Count;
        foreach (var q in p) { Vector2 d = q - c; path.Add(q + new Vector2(Mathf.Sign(d.x) * 0.02f, Mathf.Sign(d.y) * 0.035f)); }
        for (int k = 0; k < path.Count; k++) { acc.Add(total); total += Vector2.Distance(path[k], path[(k + 1) % path.Count]); }
    }
    // left en right: afgelegde weg van elke rups (m) sinds de vorige keer
    public void Move(float left, float right)
    {
        foreach (var w in wheels) w.t.Rotate(Vector3.right, (w.left ? left : right) / w.r * Mathf.Rad2Deg, Space.Self);
        for (int k = 0; k < 2; k++)
        {
            if (trackMat[k] == null) continue;
            trackOfs[k] = Mathf.Repeat(trackOfs[k] + (k == 0 ? left : right) * trackK, 1f);
            trackMat[k].SetTextureOffset("_BaseMap", new Vector2(trackOfs[k], 0f));
        }
        if (total <= 0f) return;
        run[0] = Mathf.Repeat(run[0] + left, total); run[1] = Mathf.Repeat(run[1] + right, total);
        for (int side = 0; side < 2; side++)
        {
            float x = (side == 0 ? -1f : 1f) * (TankModel.TrackX0 + TankModel.TrackX1) * 0.5f;
            var L = cleats[side];
            for (int k = 0; k < L.Count; k++)
            {
                // de profielpunten lopen tegen de klok in van voor-onder naar achter; vooruit rijden: de onderkant loopt naar achter
                float s = Mathf.Repeat(k * total / L.Count + run[side], total);
                int i = 0; while (i + 1 < path.Count && acc[i + 1] <= s) i++;
                Vector2 a = path[i], b = path[(i + 1) % path.Count]; float seg = Vector2.Distance(a, b), t = seg > 1e-4f ? (s - acc[i]) / seg : 0f;
                Vector2 p = Vector2.Lerp(a, b, t), d = (b - a).normalized;
                L[k].localPosition = new Vector3(x, p.y, p.x);
                L[k].localRotation = Quaternion.LookRotation(new Vector3(0f, d.y, d.x), Vector3.Cross(new Vector3(0f, d.y, d.x), Vector3.right) * -1f);
            }
        }
    }
}
