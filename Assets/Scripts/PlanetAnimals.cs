using System.Collections.Generic;
using UnityEngine;

// Dieren rond de kijker. Net als de mensen (PlanetLife.Crowd.cs) alleen binnen een paar honderd meter van waar de camera naar kijkt
// (of rond de wandelaar, L), met een vast maximum; ver uitgezoomd en in de ruimte geen dieren. Wie te ver weg en buiten beeld is,
// verdwijnt; nieuwe komen buiten beeld in de buurt.
// Stap 1: vogels. Zwermen cirkelen boven bos, natuur en stad, zwerven langzaam rond en landen af en toe: kleine vogels op gras,
// heide en in het bos, duiven op pleinen en in de stad. Ze vliegen op voor de wandelaar, mensen, auto's en een meteoor, en als er
// gebouwd wordt waar ze zitten. 's Nachts zijn er weinig.
// De dieren zijn tijdelijke blokjesmodellen in de kleuren van de voorbeeldplaatjes van Unity AI (Assets/Characters).
public partial class PlanetAnimals : MonoBehaviour
{
    public static PlanetAnimals I;
    public static bool On = true;   // aan/uit (de test meet de fps met en zonder)
    const int MaxBirds = 60, MaxFlocks = 7;
    public int BirdCount { get { int n = 0; foreach (var f in flocks) n += f.birds.Count; return n; } }
    public int FlockCount => flocks.Count;
    // voor tests en meldingen: hoeveel zwermen vliegen, landen, zitten of vluchten
    public string Describe()
    {
        int fly = 0, land = 0, sit = 0, panic = 0;
        foreach (var f in flocks) { if (f.panic) panic++; if (f.st == St.Fly) fly++; else if (f.st == St.Land) land++; else sit++; }
        return $"vliegen {fly}, landen {land}, zitten {sit}, vluchten {panic}";
    }
    // de vogel het dichtst bij een richting (liefst een die zit)
    public bool Closest(Vector3 dir, out Vector3 pos, out Vector3 fwd, out bool sitting, bool preferSit = true)
    {
        pos = fwd = Vector3.zero; sitting = false; float best = float.MaxValue;
        foreach (var f in flocks) foreach (var b in f.birds)
        {
            bool s = f.st == St.Ground; float d = (b.pos.normalized - dir.normalized).magnitude * Planet.R - (s == preferSit ? 1000f : 0f);
            if (d < best) { best = d; pos = b.pos; fwd = b.root.forward; sitting = s; }
        }
        return best < float.MaxValue;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { I = null; On = true; kinds = null; breeds = null; town = false; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() { if (I == null) new GameObject("Dieren").AddComponent<PlanetAnimals>(); }

    void Awake() { I = this; }
    void OnDestroy() { ClearHerds(); if (I == this) I = null; }   // ook de PlayableGraphs van de dieren opruimen

    // ---------- soorten ----------
    class Kind
    {
        public string name; public Mesh body, wingL, wingR; public Material[] mats; public Material wingMat;
        public float size, speed, flap, glide;   // size: schaal van het model; speed m/s; flap: slagen per seconde; glide: deel van de tijd zweven
        public int min, max;                       // zwermgrootte
    }
    static Dictionary<string, Kind> kinds;
    static Kind K(string n) { if (kinds == null) MakeKinds(); return kinds[n]; }

    // een klein raamwerk voor een mesh met een submesh per kleur
    class Mb
    {
        public readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>(); public readonly List<int>[] t;
        public Mb(int subs) { t = new List<int>[subs]; for (int i = 0; i < subs; i++) t[i] = new List<int>(); }
        public void Tri(int s, Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            Vector3 nr = Vector3.Cross(b - a, c - a); if (nr.sqrMagnitude < 1e-12f) return;
            if (Vector3.Dot(nr, outward) < 0f) { var x = b; b = c; c = x; nr = -nr; }
            nr.Normalize(); int i = v.Count; v.Add(a); v.Add(b); v.Add(c); n.Add(nr); n.Add(nr); n.Add(nr); t[s].Add(i); t[s].Add(i + 1); t[s].Add(i + 2);
        }
        public void Both(int s, Vector3 a, Vector3 b, Vector3 c) { Vector3 nr = Vector3.Cross(b - a, c - a); Tri(s, a, b, c, nr); Tri(s, a, b, c, -nr); }   // van twee kanten zichtbaar
        // een ruit (uitgerekte achtvlak): middelpunt c, naar voren f, naar achter b, opzij w, omhoog u, omlaag d
        public void Gem(int s, Vector3 c, float f, float b, float w, float u, float d)
        {
            Vector3[] ring = { c + Vector3.forward * f, c + Vector3.right * w, c + Vector3.back * b, c + Vector3.left * w };
            Vector3 top = c + Vector3.up * u, bot = c + Vector3.down * d;
            for (int k = 0; k < 4; k++)
            {
                Vector3 p = ring[k], q = ring[(k + 1) % 4];
                Tri(s, top, p, q, (top + p + q) / 3f - c); Tri(s, bot, p, q, (bot + p + q) / 3f - c);
            }
        }
        public Mesh Build()
        {
            var m = new Mesh(); m.SetVertices(v); m.SetNormals(n); m.subMeshCount = t.Length;
            for (int i = 0; i < t.Length; i++) m.SetTriangles(t[i], i);
            m.RecalculateBounds(); return m;
        }
    }

    // een vogel: lijf, kop, snavel en staart (submesh 0 lijf, 1 kop, 2 snavel); vleugels apart zodat ze kunnen slaan.
    // Maten voor een vogel van 1 m lang; Kind.size schaalt naar de echte maat. x rechts, y omhoog, z naar voren.
    static Kind BirdKind(string name, Color body, Color head, Color beak, Color wing, float len, float span, float speed, float flap, float glide, int min, int max)
    {
        var mb = new Mb(3);
        mb.Gem(0, new Vector3(0f, 0f, 0f), 0.32f, 0.38f, 0.17f, 0.15f, 0.13f);   // lijf
        mb.Gem(1, new Vector3(0f, 0.11f, 0.33f), 0.13f, 0.09f, 0.1f, 0.1f, 0.08f);   // kop
        Vector3 b0 = new Vector3(0f, 0.1f, 0.44f);   // snavel: een spits piramidetje
        Vector3 tip = new Vector3(0f, 0.08f, 0.6f), l = b0 + new Vector3(-0.035f, 0f, 0f), r = b0 + new Vector3(0.035f, 0f, 0f), u = b0 + new Vector3(0f, 0.035f, 0f), d = b0 + new Vector3(0f, -0.03f, 0f);
        mb.Tri(2, tip, l, u, Vector3.left + Vector3.up); mb.Tri(2, tip, u, r, Vector3.right + Vector3.up); mb.Tri(2, tip, r, d, Vector3.right + Vector3.down); mb.Tri(2, tip, d, l, Vector3.left + Vector3.down);
        mb.Both(0, new Vector3(-0.05f, 0.03f, -0.3f), new Vector3(0.05f, 0.03f, -0.3f), new Vector3(0f, 0.04f, -0.62f));   // staart: een waaier
        mb.Both(0, new Vector3(0f, 0.04f, -0.62f), new Vector3(-0.14f, 0.03f, -0.58f), new Vector3(-0.05f, 0.03f, -0.3f));
        mb.Both(0, new Vector3(0f, 0.04f, -0.62f), new Vector3(0.14f, 0.03f, -0.58f), new Vector3(0.05f, 0.03f, -0.3f));
        Mesh W(float sx)   // vleugel vanaf de schouder naar buiten (sx -1 links, 1 rechts): een brede binnenvleugel en een spitse punt
        {
            var w = new Mb(1); float h = span / len * 0.5f;   // halve spanwijdte in modelmaat
            Vector3 a = new Vector3(0f, 0f, 0.14f), b = new Vector3(0f, 0f, -0.16f), c = new Vector3(sx * h * 0.5f, 0.02f, 0.1f), e = new Vector3(sx * h * 0.5f, 0.02f, -0.2f), f = new Vector3(sx * h, 0f, -0.12f);
            w.Both(0, a, b, e); w.Both(0, a, e, c); w.Both(0, c, e, f); return w.Build();
        }
        return new Kind
        {
            name = name, body = mb.Build(), wingL = W(-1f), wingR = W(1f), size = len, speed = speed, flap = flap, glide = glide, min = min, max = max,
            mats = new[] { Mats.Get("dier_" + name + "_lijf", body), Mats.Get("dier_" + name + "_kop", head), Mats.Get("dier_" + name + "_snavel", beak) },
            wingMat = Mats.Get("dier_" + name + "_vleugel", wing),
        };
    }
    static void MakeKinds()
    {
        kinds = new Dictionary<string, Kind>();
        // spreeuw: klein en donker, grote zwermen boven het land
        kinds["spreeuw"] = BirdKind("spreeuw", new Color(0.16f, 0.17f, 0.2f), new Color(0.13f, 0.14f, 0.17f), new Color(0.85f, 0.7f, 0.25f), new Color(0.2f, 0.21f, 0.25f), 0.22f, 0.4f, 11f, 9f, 0.15f, 8, 14);
        // duif: grijsblauw met een donkere kop, in de stad (naar Pigeon_Reference_v1)
        kinds["duif"] = BirdKind("duif", new Color(0.62f, 0.64f, 0.7f), new Color(0.4f, 0.43f, 0.5f), new Color(0.3f, 0.26f, 0.24f), new Color(0.55f, 0.57f, 0.63f), 0.33f, 0.65f, 9f, 6f, 0.3f, 5, 9);
    }

    // ---------- zwermen ----------
    class Bird
    {
        public Transform root, wingL, wingR; public Vector3 pos, vel, landAt, landUp; public float phase, ofsR, ofsH, flapT, peckT, hopT, groundT;
    }
    enum St { Fly, Land, Ground }
    class Flock
    {
        public Kind kind; public readonly List<Bird> birds = new List<Bird>(); public St st; public float stT, ang, spin, radius, alt;
        public Vector3 center, goal; public float groundH, wanderT; public int landCell; public string landSig; public bool city, panic;
    }
    readonly List<Flock> flocks = new List<Flock>();

    // ---------- de kijker (zoals PlanetLife.Crowd) ----------
    Camera viewCam; Vector3 viewAt = Vector3.up; float viewR = 300f, viewK = 1f, nearT, walkerT; PlanetWalker walker;
    Vector3 nearAt; bool hasNear;
    readonly List<int> nearLand = new List<int>(), nearCity = new List<int>(), nearGrass = new List<int>();
    void UpdateView()
    {
        if (walker == null && (walkerT -= Time.deltaTime) <= 0f) { walkerT = 2f; walker = FindAnyObjectByType<PlanetWalker>(FindObjectsInactive.Include); }
        bool walking = walker != null && walker.gameObject.activeInHierarchy;
        viewCam = walking && walker.Cam != null ? walker.Cam : Camera.main;
        if (walking) { viewAt = walker.transform.position.normalized; viewR = 200f; viewK = 1f; return; }
        if (viewCam == null) { viewK = 0f; return; }
        Vector3 o = viewCam.transform.position, d = viewCam.transform.forward; float R = Planet.R;
        float alt = o.magnitude - R, b = Vector3.Dot(o, d), disc = b * b - (o.sqrMagnitude - R * R);
        float t = disc > 0f ? -b - Mathf.Sqrt(disc) : -1f;
        viewAt = (t > 0f ? o + d * t : o).normalized;
        viewR = Mathf.Clamp(alt * 1.2f, 160f, 340f);
        viewK = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(380f, 700f, alt));
    }
    bool Visible(Vector3 p)
    {
        if (viewCam == null) return false;
        Vector3 v = viewCam.WorldToViewportPoint(p);
        return v.z > 0f && v.z < 450f && v.x > -0.1f && v.x < 1.1f && v.y > -0.1f && v.y < 1.1f;
    }
    float Dist(Vector3 a, Vector3 b) => (a.normalized - b.normalized).magnitude * Planet.R;
    bool Far(Vector3 p) => Dist(p, viewAt) > viewR * 1.3f;

    // wat er op een vak staat, voor vogels
    static bool Natural(List<string> l)
    {
        foreach (var t in l) if (t != "park" && t != "meer" && t != "heide" && t != "droogheide" && t != "rotsen" && t != "ijs" && t != "woestijn" && t != "palmen") return false;
        return true;
    }
    static bool Built(int c) => PlanetCity.Cells.TryGetValue(c, out var l) && l.Count > 0 && !Natural(l) && !PlanetCity.IsWalk(c);
    static bool Plaza(int c)
    {
        if (!PlanetCity.Cells.TryGetValue(c, out var l) || l.Count == 0) return false;
        foreach (var t in l) if (t == "plein" || t.StartsWith("m|plein")) return true;
        return false;
    }
    static bool Grass(int c)   // leeg gras, heide of bos: kleine vogels landen hier
    {
        if (PlanetCity.Wet(c) || PlanetCity.IsWalk(c)) return false;
        if (!PlanetCity.Cells.TryGetValue(c, out var l) || l.Count == 0) return true;
        return l.Count == 1 && (l[0] == "park" || l[0] == "heide" || l[0] == "droogheide");
    }
    static string Sig(int c) => PlanetCity.Cells.TryGetValue(c, out var l) ? string.Join(",", l) : "";

    // de vakken in de buurt (twee keer per seconde, of als de kijker ver is verschoven)
    void UpdateNear(float dt)
    {
        nearT -= dt;
        if (nearT > 0f && hasNear && Dist(nearAt, viewAt) < 30f) return;
        nearT = 0.5f; nearAt = viewAt; hasNear = true;
        nearLand.Clear(); nearCity.Clear(); nearGrass.Clear();
        float lim = viewR / Planet.R; var cells = Planet.Cells;
        for (int i = 0; i < cells.Length; i++)
        {
            if ((cells[i].dir - viewAt).sqrMagnitude > lim * lim) continue;
            if (PlanetCity.Terrain(i) < PlanetCity.SeaLevel - 0.2f && !PlanetCity.Cells.ContainsKey(i)) continue;   // open water: daar later meeuwen
            nearLand.Add(i);
            if (Built(i) || Plaza(i)) nearCity.Add(i);
            else if (Grass(i)) nearGrass.Add(i);
        }
    }

    int TargetFlocks()
    {
        if (!On || viewK <= 0.05f || nearLand.Count < 8) return 0;
        float night = DayNight.Night;   // 's nachts slapen de meeste vogels
        int n = Mathf.Clamp(nearLand.Count / 30, 1, MaxFlocks);
        return Mathf.Max(night > 0.6f ? 1 : 0, Mathf.RoundToInt(n * viewK * Mathf.Lerp(1f, 0.15f, night)));
    }

    // ---------- elk beeld ----------
    float scareT;
    void Update()
    {
        if (!Raatstad.PlanetMode || !Planet.Ready || Planet.Cells == null) { if (flocks.Count > 0 || herds.Count > 0) ClearAll(); return; }
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        UpdateView(); UpdateNear(dt);
        int target = TargetFlocks();
        // te veel: eerst wie buiten beeld is; uitgezoomd of uit: iedereen
        for (int i = flocks.Count - 1; i >= 0 && flocks.Count > target; i--)
            if (target == 0 && (viewK <= 0.05f || !On) || !FlockVisible(flocks[i])) RemoveFlock(i);
        // te ver weg en buiten beeld: weg (er komt elders een nieuwe)
        for (int i = flocks.Count - 1; i >= 0; i--) if (Far(flocks[i].center) && !FlockVisible(flocks[i])) RemoveFlock(i);
        // aanvullen, één per beeld; bij het vullen (niemand in de buurt) ook in beeld
        if (flocks.Count < target && BirdCount < MaxBirds) SpawnFlock(flocks.Count == 0);
        if ((scareT -= dt) <= 0f) { scareT = 0.2f; Scare(); }
        foreach (var f in flocks) StepFlock(f, dt);
        StepHerds(dt);
    }

    bool FlockVisible(Flock f) { foreach (var b in f.birds) if (Visible(b.pos)) return true; return false; }
    void RemoveFlock(int i) { foreach (var b in flocks[i].birds) if (b.root != null) Destroy(b.root.gameObject); flocks.RemoveAt(i); }
    public void ClearAll() { for (int i = flocks.Count - 1; i >= 0; i--) RemoveFlock(i); ClearHerds(); }

    float Ground(Vector3 dir) => Mathf.Max(PlanetCity.TerrainAt(dir), PlanetCity.SeaLevel);
    // de hoogte boven de grond waar een zwerm vliegt: boven de stad hoger (torens)
    static float FlyAlt(bool city) => city ? Random.Range(32f, 48f) : Random.Range(16f, 30f);

    void SpawnFlock(bool anyView)
    {
        bool city = nearCity.Count > 0 && Random.value < Mathf.Clamp(nearCity.Count / (float)Mathf.Max(1, nearLand.Count) * 1.6f, 0.15f, 0.85f);
        var list = city ? nearCity : nearGrass.Count > 0 ? nearGrass : nearLand; if (list.Count == 0) return;
        var kind = K(city ? "duif" : "spreeuw");
        for (int k = 0; k < 10; k++)
        {
            int c = list[Random.Range(0, list.Count)]; Vector3 dir = Planet.Cells[c].dir;
            float gh = Ground(dir), alt = FlyAlt(city); Vector3 p = dir * (Planet.R + gh + alt);
            if (!anyView && Visible(p)) continue;
            var f = new Flock { kind = kind, city = city, center = dir, goal = dir, groundH = gh, alt = alt, radius = Random.Range(10f, 22f), spin = (Random.value < 0.5f ? -1f : 1f) * Random.Range(0.25f, 0.45f), ang = Random.value * 6.28f, st = St.Fly, stT = Random.Range(8f, 25f), wanderT = Random.Range(8f, 20f), landCell = -1 };
            int n = Random.Range(kind.min, kind.max + 1); n = Mathf.Min(n, MaxBirds - BirdCount); if (n < 2) return;
            for (int i = 0; i < n; i++)
            {
                var b = MakeBird(kind); b.phase = i * 6.28f / n + Random.Range(-0.3f, 0.3f); b.ofsR = Random.Range(-4f, 4f); b.ofsH = Random.Range(-3f, 3f); b.flapT = Random.value * 10f;
                b.pos = FlyTarget(f, b); b.vel = Tangent(f, b) * kind.speed;
                f.birds.Add(b); Place(b, kind, false, 0f);
            }
            flocks.Add(f); return;
        }
    }

    Bird MakeBird(Kind k)
    {
        var b = new Bird();
        var go = new GameObject(k.name); go.transform.SetParent(transform, false); b.root = go.transform; go.transform.localScale = Vector3.one * k.size;
        var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = k.body; var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = k.mats; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Transform Wing(Mesh m, float x)
        {
            var w = new GameObject("vleugel"); w.transform.SetParent(go.transform, false); w.transform.localPosition = new Vector3(x, 0.07f, 0.05f);
            w.AddComponent<MeshFilter>().sharedMesh = m; var r = w.AddComponent<MeshRenderer>(); r.sharedMaterial = k.wingMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return w.transform;
        }
        b.wingL = Wing(k.wingL, -0.1f); b.wingR = Wing(k.wingR, 0.1f);
        return b;
    }

    // waar een vogel in de kring hoort te vliegen
    Vector3 FlyTarget(Flock f, Bird b)
    {
        Vector3 up = f.center, e = Vector3.Cross(Mathf.Abs(up.y) < 0.95f ? Vector3.up : Vector3.right, up).normalized, n = Vector3.Cross(up, e);
        float a = f.ang + b.phase, r = f.radius + b.ofsR + Mathf.Sin(Time.time * 0.37f + b.phase * 3f) * 2.5f;
        float h = f.groundH + f.alt + b.ofsH + Mathf.Sin(Time.time * 0.5f + b.phase * 2f) * 1.5f;
        return up * (Planet.R + h) + (e * Mathf.Cos(a) + n * Mathf.Sin(a)) * r;
    }
    Vector3 Tangent(Flock f, Bird b)
    {
        Vector3 up = f.center, e = Vector3.Cross(Mathf.Abs(up.y) < 0.95f ? Vector3.up : Vector3.right, up).normalized, n = Vector3.Cross(up, e);
        float a = f.ang + b.phase; return (-e * Mathf.Sin(a) + n * Mathf.Cos(a)) * Mathf.Sign(f.spin);
    }

    void StepFlock(Flock f, float dt)
    {
        f.ang += f.spin * dt * (f.panic ? 1.6f : 1f);
        f.stT -= dt;
        switch (f.st)
        {
            case St.Fly:
                // langzaam rondzwerven: het midden schuift naar een nieuw doel in de buurt
                if ((f.wanderT -= dt) <= 0f) { f.wanderT = Random.Range(10f, 25f); f.goal = WanderGoal(f); }
                float step = (f.panic ? 9f : 3.5f) * dt / Planet.R; f.center = Vector3.RotateTowards(f.center, f.goal, step, 0f).normalized;
                f.groundH = Mathf.Lerp(f.groundH, Ground(f.center), dt * 0.5f);
                if (f.panic && Dist(f.center, f.goal) < 5f) { f.panic = false; f.alt = FlyAlt(f.city); }
                if (f.stT <= 0f && !f.panic && DayNight.Night < 0.6f) { if (TryLand(f)) break; f.stT = Random.Range(10f, 20f); }
                foreach (var b in f.birds) FlyTo(f, b, FlyTarget(f, b), dt, false);
                break;
            case St.Land:
                bool all = true;
                foreach (var b in f.birds)
                {
                    if (b.groundT > 0f) { Peck(f, b, dt); continue; }
                    float d = (b.landAt - b.pos).magnitude;
                    if (d < 0.25f) { b.pos = b.landAt; b.vel = Vector3.zero; b.groundT = 0.01f; Place(b, f.kind, true, dt); continue; }
                    all = false; FlyTo(f, b, b.landAt, dt, true);
                }
                if (all) { f.st = St.Ground; f.stT = Random.Range(10f, 28f); }
                else if (f.stT < -20f) TakeOff(f, false);   // komt er niet: toch maar weer op
                break;
            case St.Ground:
                if (f.stT <= 0f || Sig(f.landCell) != f.landSig) { TakeOff(f, false); break; }   // klaar, of er is gebouwd
                foreach (var b in f.birds) Peck(f, b, dt);
                break;
        }
    }

    Vector3 WanderGoal(Flock f)
    {
        var list = f.city ? nearCity : nearGrass.Count > 0 ? nearGrass : nearLand;
        for (int k = 0; k < 6 && list.Count > 0; k++)
        {
            Vector3 d = Planet.Cells[list[Random.Range(0, list.Count)]].dir;
            if (Dist(d, f.center) < 120f) return d;
        }
        return f.center;
    }

    // een vogel vliegt naar een punt: draait geleidelijk bij, slaat met de vleugels of zweeft
    void FlyTo(Flock f, Bird b, Vector3 target, float dt, bool landing)
    {
        float max = f.kind.speed * (f.panic ? 1.5f : 1f);
        Vector3 to = target - b.pos; float d = to.magnitude;
        Vector3 want = d > 0.001f ? to / d * Mathf.Min(max, landing ? Mathf.Max(1.5f, d * 0.9f) : d * 0.8f + max * 0.6f) : Vector3.zero;
        b.vel = Vector3.Lerp(b.vel, want, dt * (landing ? 2.2f : 1.4f));
        Vector3 up = b.pos.normalized, np = b.pos + b.vel * dt;
        // nooit door de grond of een gebouw: minstens een paar meter boven het terrein (tijdens het landen tot op de grond)
        float gh = Ground(np.normalized), min = landing ? (Vector3.Dot(b.landAt, up) - Planet.R) : gh + 4f;
        float h = np.magnitude - Planet.R; if (h < min) np = np.normalized * (Planet.R + min);
        b.vel = (np - b.pos) / Mathf.Max(dt, 1e-4f); b.pos = np; b.groundT = 0f;
        Place(b, f.kind, false, dt);
    }

    // de vogel op zijn plek zetten: in de vliegrichting, licht hellend in de bocht; vleugels slaan of liggen langs het lijf
    void Place(Bird b, Kind k, bool sitting, float dt)
    {
        Vector3 up = b.pos.normalized, fwd = Vector3.ProjectOnPlane(b.vel, up);
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.ProjectOnPlane(b.root.forward, up); if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.Cross(up, Vector3.right);
        fwd.Normalize();
        float climb = Mathf.Clamp(Vector3.Dot(b.vel, up) / Mathf.Max(1f, k.speed), -0.6f, 0.6f);
        Quaternion q = Geo.Look(fwd, up) * Quaternion.Euler(-climb * 40f, 0f, 0f);
        if (!sitting)
        {
            q = Quaternion.Slerp(b.root.rotation, q, Mathf.Clamp01(dt * 6f));
            b.flapT += dt * k.flap;
            float glide = Mathf.Sin(b.flapT * 0.13f + b.phase) > 1f - 2f * k.glide ? 1f : 0f;   // af en toe zweven
            float ang = glide > 0f ? 4f : Mathf.Sin(b.flapT * 6.283f) * 45f + 8f;
            b.wingL.localRotation = Quaternion.Euler(0f, 0f, -ang); b.wingR.localRotation = Quaternion.Euler(0f, 0f, ang);
            b.wingL.localScale = b.wingR.localScale = Vector3.one;
        }
        else
        {
            b.wingL.localRotation = Quaternion.Euler(0f, 0f, 8f); b.wingR.localRotation = Quaternion.Euler(0f, 0f, -8f);
            b.wingL.localScale = b.wingR.localScale = new Vector3(0.22f, 1f, 0.9f);   // ingevouwen, plat op de rug
        }
        b.root.SetPositionAndRotation(b.pos + up * (sitting ? 0.13f * k.size : 0f), q);
    }

    // op de grond: pikken (kop omlaag), af en toe een stapje of huppel, rondkijken
    void Peck(Flock f, Bird b, float dt)
    {
        b.groundT += dt; Vector3 up = b.landUp.sqrMagnitude > 0.5f ? b.landUp : b.pos.normalized;
        if ((b.hopT -= dt) <= 0f)
        {
            b.hopT = Random.Range(1.2f, 4f);
            Vector3 e = Vector3.Cross(up, Random.onUnitSphere).normalized; Vector3 np = b.pos + e * Random.Range(0.15f, 0.4f);
            float gh = Vector3.Dot(b.landAt, up) - Vector3.Dot(np, up); np += up * gh;   // op dezelfde hoogte blijven
            if ((np - b.landAt).magnitude < 3f) { b.vel = (np - b.pos); b.pos = np; }
        }
        b.peckT += dt;
        Place(b, f.kind, true, dt);
        float peck = Mathf.Max(0f, Mathf.Sin(b.peckT * 2.2f + b.phase * 5f)); peck = peck * peck * peck * 35f;
        b.root.rotation = b.root.rotation * Quaternion.Euler(peck, 0f, 0f);
    }

    // een landingsplek: kleine vogels op gras, heide of in het bos; duiven op een plein, in de stad ook op gras
    bool TryLand(Flock f)
    {
        var list = new List<int>();
        foreach (int c in f.city ? nearCity : nearGrass)
        {
            if (Dist(Planet.Cells[c].dir, f.center) > 70f) continue;
            if (f.city ? !Plaza(c) : !Grass(c)) continue;
            list.Add(c);
        }
        if (f.city && list.Count == 0) foreach (int c in nearGrass) if (Dist(Planet.Cells[c].dir, f.center) < 70f) list.Add(c);
        if (list.Count == 0) return false;
        int cell = list[Random.Range(0, list.Count)]; var cl = Planet.Cells[cell];
        if (DangerNear(cl.dir * (Planet.R + Ground(cl.dir)), 25f)) return false;
        Vector3 e = Vector3.Cross(Mathf.Abs(cl.dir.y) < 0.95f ? Vector3.up : Vector3.right, cl.dir).normalized, n = Vector3.Cross(cl.dir, e);
        int ok = 0;
        foreach (var b in f.birds)
        {
            Vector3 spot = cl.dir; b.landUp = cl.dir;
            for (int k = 0; k < 6; k++)
            {
                float a = Random.value * 6.28f, r = Mathf.Sqrt(Random.value) * cl.radius * 0.6f;
                Vector3 d = (cl.dir * Planet.R + (e * Mathf.Cos(a) + n * Mathf.Sin(a)) * r).normalized;
                // de echte bovenkant (ook een plein of een dak): een straal van boven
                if (Physics.Raycast(d * (Planet.R + Ground(d) + 40f), -d, out var hit, 80f, Layers.Grond, QueryTriggerInteraction.Ignore) && Vector3.Dot(hit.normal, d) > 0.75f)
                { b.landAt = hit.point; b.landUp = hit.normal; ok++; goto found; }
                if (k == 5) { b.landAt = d * (Planet.R + Ground(d)); ok++; }
            }
            found:;
        }
        if (ok == 0) return false;
        foreach (var b in f.birds) { b.groundT = 0f; b.hopT = Random.Range(0.5f, 3f); }
        f.st = St.Land; f.stT = 0f; f.landCell = cell; f.landSig = Sig(cell); return true;
    }

    void TakeOff(Flock f, bool panic)
    {
        f.st = St.Fly; f.stT = Random.Range(20f, 45f); f.alt = FlyAlt(f.city) + (panic ? 12f : 0f); f.panic = panic;
        if (f.landCell >= 0) { f.center = Planet.Cells[f.landCell].dir; f.groundH = Ground(f.center); }
        if (panic) { f.goal = Vector3.RotateTowards(f.center, f.center + Random.onUnitSphere * 0.1f, 110f / Planet.R, 0f).normalized; }
        foreach (var b in f.birds) { b.groundT = 0f; b.vel = b.pos.normalized * Random.Range(2f, 4f) + Random.onUnitSphere * 1.5f; }
        f.landCell = -1;
    }

    // ---------- schrikken ----------
    readonly List<Vector3> danger = new List<Vector3>(); readonly List<float> dangerR = new List<float>(); readonly List<int> dangerKind = new List<int>();   // 0 de wandelaar, 1 een auto, 2 iemand anders
    void CollectDanger()
    {
        danger.Clear(); dangerR.Clear(); dangerKind.Clear();
        if (walker != null && walker.gameObject.activeInHierarchy) { danger.Add(walker.transform.position); dangerR.Add(12f); dangerKind.Add(0); }
        var life = PlanetLife.I;
        if (life != null)
        {
            var t = life.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                var c = t.GetChild(i); if (!c.gameObject.activeInHierarchy) continue;
                danger.Add(c.position); dangerR.Add(c.name == "Auto" ? 9f : c.name == "Fietser" ? 6f : 3.5f); dangerKind.Add(c.name == "Auto" ? 1 : 2);
            }
        }
    }
    bool DangerNear(Vector3 p, float extra)
    {
        for (int i = 0; i < danger.Count; i++) { float r = dangerR[i] + extra; if ((danger[i] - p).sqrMagnitude < r * r) return true; }
        return false;
    }
    void Scare()
    {
        CollectDanger();
        // een meteoor: alles in de wijde omtrek vliegt hoog en ver weg
        var life = PlanetLife.I; bool boom = PlanetMeteor.Flying > 0 || life != null && Time.time - life.LastIncidentT < 0.5f && life.LastIncidentT > 0f;
        ScareHerds(life != null && life.LastIncidentT > 0f && Time.time - life.LastIncidentT < 0.5f, life != null ? life.LastIncident : Vector3.zero);
        foreach (var f in flocks)
        {
            if (boom && !f.panic && (PlanetMeteor.Flying > 0 || Dist(life.LastIncident, f.center) < 300f)) { if (f.st == St.Fly) { f.panic = true; f.alt += 15f; f.goal = Vector3.RotateTowards(f.center, f.center + Random.onUnitSphere * 0.1f, 140f / Planet.R, 0f).normalized; } else TakeOff(f, true); continue; }
            if (f.st == St.Fly) continue;
            foreach (var b in f.birds) if (DangerNear(b.pos, 0f)) { TakeOff(f, true); break; }
        }
    }
}
