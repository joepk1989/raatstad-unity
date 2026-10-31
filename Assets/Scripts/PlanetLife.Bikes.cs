using System.Collections.Generic;
using UnityEngine;

// Fietsers op de fiets- en voetpaden (PlanetCity.Bike): een low-poly fiets met een trappende fietser, rechts houdend, van vak
// naar vak zoals de mensen lopen, maar alleen over fietspaden (ook door een onderdoorgang of schuin onder een brug door).
public partial class PlanetLife
{
    class Rider { public Transform root, wheelF, wheelB; public Person who; public int a, b, prev; public float t, speed, pedal; }
    readonly List<Rider> riders = new List<Rider>();
    readonly List<int> bikeCells = new List<int>();
    static Mesh frameMesh, wheelMesh;
    static readonly Color[] FrameColors = { new Color(0.15f, 0.16f, 0.18f), new Color(0.75f, 0.12f, 0.12f), new Color(0.15f, 0.35f, 0.65f), new Color(0.9f, 0.9f, 0.88f), new Color(0.2f, 0.5f, 0.3f), new Color(0.85f, 0.6f, 0.15f) };

    void RefreshBikes() { bikeCells.Clear(); foreach (int c in PlanetCity.Bike) if (PlanetCity.IsBike(c)) bikeCells.Add(c); }

    static int NextBike(int a, int prev)
    {
        var opts = new List<int>(6); foreach (int j in PlanetCity.Partners(a)) if (j != prev && PlanetCity.IsBike(j)) opts.Add(j);
        if (opts.Count == 0) return prev >= 0 && PlanetCity.IsBike(prev) ? prev : a;   // doodlopend: keren
        return opts[Random.Range(0, opts.Count)];
    }

    // de fiets: een frame van dunne balkjes (lengte langs z) en twee wielen als twaalfhoekige schijven
    static void BikeMeshes()
    {
        if (frameMesh != null) return;
        var f = new MeshBuilder();
        void Bar(Vector3 a, Vector3 b, float w)
        {
            Vector3 d = (b - a).normalized, s = Vector3.Cross(d, Vector3.right).sqrMagnitude > 1e-4f ? Vector3.Cross(d, Vector3.right).normalized * w : Vector3.up * w, u = Vector3.right * w;
            Vector3[] p = { a - s - u, a + s - u, a + s + u, a - s + u, b - s - u, b + s - u, b + s + u, b - s + u }; Vector3 m = (a + b) * 0.5f;
            int[][] q = { new[] { 0, 1, 5, 4 }, new[] { 1, 2, 6, 5 }, new[] { 2, 3, 7, 6 }, new[] { 3, 0, 4, 7 } };
            foreach (var k in q) f.Quad(p[k[0]], p[k[1]], p[k[2]], p[k[3]], (p[k[0]] + p[k[2]]) * 0.5f - m);
        }
        Vector3 rear = new Vector3(0, 0.34f, -0.52f), front = new Vector3(0, 0.34f, 0.52f), crank = new Vector3(0, 0.32f, -0.02f), seat = new Vector3(0, 0.82f, -0.2f), head = new Vector3(0, 0.88f, 0.38f);
        Bar(rear, crank, 0.018f); Bar(crank, seat, 0.02f); Bar(seat, rear, 0.016f); Bar(seat + new Vector3(0, -0.06f, 0), head, 0.02f); Bar(crank, head, 0.022f); Bar(head, front, 0.018f);
        Bar(new Vector3(-0.08f, 0.86f, -0.26f), new Vector3(0.08f, 0.86f, -0.14f), 0.04f);   // zadel
        Bar(new Vector3(-0.28f, 0.98f, 0.36f), new Vector3(0.28f, 0.98f, 0.36f), 0.015f); Bar(head, new Vector3(0, 0.98f, 0.36f), 0.018f);   // stuur
        frameMesh = f.Build();
        var w = new MeshBuilder(); const int N = 12; const float R = 0.34f, T = 0.025f;
        for (int k = 0; k < N; k++)
        {
            float a0 = k * Mathf.PI * 2f / N, a1 = (k + 1) * Mathf.PI * 2f / N;
            Vector3 p0 = new Vector3(0, Mathf.Sin(a0), Mathf.Cos(a0)) * R, p1 = new Vector3(0, Mathf.Sin(a1), Mathf.Cos(a1)) * R;
            w.Quad(p0 + Vector3.left * T, p1 + Vector3.left * T, p1 + Vector3.right * T, p0 + Vector3.right * T, (p0 + p1) * 0.5f);   // loopvlak van de band
            foreach (float sx in new[] { -T, T }) w.Quad(p0 * 0.8f + Vector3.right * sx, p1 * 0.8f + Vector3.right * sx, p1 + Vector3.right * sx, p0 + Vector3.right * sx, Vector3.right * sx);   // de zijkanten van de band: een zwarte ring
            w.Tri(Vector3.zero, p0 * 0.25f + Vector3.left * T, p1 * 0.25f + Vector3.left * T, Vector3.left); w.Tri(Vector3.zero, p0 * 0.25f + Vector3.right * T, p1 * 0.25f + Vector3.right * T, Vector3.right);   // naaf
            if (k % 3 == 0) { Vector3 sp = p0 * 0.95f; w.Quad(sp + Vector3.left * 0.008f, sp + Vector3.right * 0.008f, Vector3.right * 0.008f, Vector3.left * 0.008f, Vector3.Cross(sp, Vector3.right)); }   // spaak
        }
        wheelMesh = w.Build();
    }

    Rider SpawnRider()
    {
        BikeMeshes();
        var r = new Rider { a = bikeCells[Random.Range(0, bikeCells.Count)], prev = -1, t = Random.value, speed = 4f + Random.value * 2f, pedal = Random.value * 6.28f };
        r.b = NextBike(r.a, -1);
        r.root = new GameObject("Fietser").transform; r.root.SetParent(transform, false); r.root.gameObject.layer = Layers.Mens;
        int k = Random.Range(0, FrameColors.Length);
        void Part(string n, Mesh m, Material mat, Vector3 pos, out Transform tr) { var go = new GameObject(n); go.transform.SetParent(r.root, false); go.transform.localPosition = pos; go.layer = Layers.Mens; go.AddComponent<MeshFilter>().sharedMesh = m; go.AddComponent<MeshRenderer>().sharedMaterial = mat; tr = go.transform; }
        Part("frame", frameMesh, Mats.Get("fiets" + k, FrameColors[k]), Vector3.zero, out _);
        var band = Mats.Get("fietsband", new Color(0.08f, 0.08f, 0.09f));
        Part("voorwiel", wheelMesh, band, new Vector3(0, 0.34f, 0.52f), out r.wheelF); Part("achterwiel", wheelMesh, band, new Vector3(0, 0.34f, -0.52f), out r.wheelB);
        // de fietser: een gewoon mens, op het zadel, met de handen aan het stuur
        r.who = global::People.MakePerson(r.root); r.who.sit = true; r.who.root.localRotation = Quaternion.identity;
        r.who.root.localPosition = r.who.rb != null ? new Vector3(0, 0.84f - 0.5f, -0.28f) : new Vector3(0, 0.84f - 0.9f * r.who.scale, -0.2f);   // een realistisch figuur zit (de heup op stoelhoogte, 0,5 m)
        if (r.who.audio != null) Object.Destroy(r.who.audio);
        PlaceRider(r, 0f); return r;
    }

    void PlaceRider(Rider r, float dt)
    {
        bool ra = PlanetCity.IsRoad(r.a) && !PlanetCity.UnderLink(r.a, r.b), rb = PlanetCity.IsRoad(r.b) && !PlanetCity.UnderLink(r.b, r.a);
        Where(r.a, r.b, r.t, 0.8f, 0.12f, out Vector3 pos, out Vector3 fwd, out Vector3 up);   // rechts op het fietsdeel
        pos += up * (r.t < 0.5f ? PlanetCity.SurfaceH(r.a, r.b, pos) : PlanetCity.SurfaceH(r.b, r.a, pos)) + up * ((ra || rb) ? 0.15f : 0f);
        r.root.position = dt <= 0f ? pos : Vector3.Lerp(r.root.position, pos, Mathf.Min(1f, dt * 8f)); Quaternion target = Geo.Look(fwd, up);   // vloeiend, geen sprongetje op een vakrand
        r.root.rotation = dt <= 0f ? target : Quaternion.Slerp(r.root.rotation, target, Mathf.Min(1f, dt * 6f));
    }

    void UpdateBikes(float dt)
    {
        int target = PeopleOn ? Mathf.Clamp(bikeCells.Count / 3, 0, 20) : 0;
        if (riders.Count < target && bikeCells.Count > 0 && Random.value < dt * 2f) riders.Add(SpawnRider());
        while (riders.Count > target) { Destroy(riders[riders.Count - 1].root.gameObject); riders.RemoveAt(riders.Count - 1); }
        for (int i = riders.Count - 1; i >= 0; i--)
        {
            var r = riders[i];
            if (!PlanetCity.IsBike(r.a)) { Destroy(r.root.gameObject); riders.RemoveAt(i); continue; }   // het fietspad is weg
            if (!PlanetCity.IsBike(r.b)) { r.b = NextBike(r.a, r.prev); if (r.b == r.a) continue; }
            float len = Mathf.Max(1f, Vector3.Distance(Planet.Cells[r.a].dir, Planet.Cells[r.b].dir) * Planet.R);
            r.t += r.speed * dt / len;
            while (r.t >= 1f) { r.t -= 1f; r.prev = r.a; r.a = r.b; r.b = NextBike(r.a, r.prev); if (r.a == r.b) { r.t = 0f; break; } }
            // trappen en de wielen laten draaien (een wiel van 0,34 m: 1/0,34 rad per meter)
            float spin = r.speed * dt / 0.34f * Mathf.Rad2Deg; r.wheelF.Rotate(Vector3.right, spin, Space.Self); r.wheelB.Rotate(Vector3.right, spin, Space.Self);
            if (r.who.rb != null) { global::People.Animate(r.who, dt, false); PlaceRider(r, dt); continue; }
            r.pedal += r.speed * dt * 2.2f; float sw = Mathf.Sin(r.pedal) * 20f;   // de benen gaan op en neer rond de trapas (zonder knie: schuin naar voren)
            r.who.legL.localRotation = Quaternion.Euler(-52f + sw, 0, 0); r.who.legR.localRotation = Quaternion.Euler(-52f - sw, 0, 0);
            r.who.armL.localRotation = Quaternion.Euler(-68f, 0, 0); r.who.armR.localRotation = Quaternion.Euler(-68f, 0, 0);
            PlaceRider(r, dt);
        }
    }
}
