using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Dieren op de grond: kuddes die grazen, rondlopen en wegrennen. Modellen met animaties (lopen, grazen, rennen, staan) uit het pakket
// "Ultimate Animated Animals" van Quaternius (CC0, Assets/ThirdParty/Animals). Rond de startstad leven herten in de bosgordel om
// de stad (BeltIn tot BeltOut vanaf het startvak) en koeien, paarden, ezels en alpaca's op het open gras en de heide daarbuiten
// (tot MeadowOut); zonder startstad (de natuurplaneet) herten in het bos en de rest op gras en heide, rond de kijker. Ze blijven van wegen, water en gebouwen af, rennen weg
// voor de wandelaar, mensen, auto's en een meteoor, en gaan weg waar gebouwd wordt.
// De vos van Unity AI (ForestFox.cs) loopt los hiervan.
public partial class PlanetAnimals
{
    public const float BeltIn = 180f, BeltOut = 360f;   // de bosgordel rond de startstad (GreenEdge: bos vanaf 12 vakken, tot 22 vakken), in meters vanaf het startvak
    const int MaxHerds = 8, MaxBeasts = 40;
    public int BeastCount { get { int n = 0; foreach (var h in herds) n += h.beasts.Count; return n; } }
    public int HerdCount => herds.Count;
    public string DescribeHerds()
    {
        var sb = new System.Text.StringBuilder(); int run = 0;
        foreach (var h in herds) { sb.Append(h.beasts.Count > 0 ? h.beasts[0].breed.name : "?").Append(' ').Append(h.beasts.Count).Append(", "); foreach (var b in h.beasts) if (b.st == SRun) run++; }
        int nb = 0, ok = 0; if (breeds != null) foreach (var b in breeds.Values) { nb++; if (b.ok) ok++; }
        return $"kuddes: {sb}rennen {run}; soorten geladen {ok}/{nb}";
    }
    public bool ClosestBeast(Vector3 dir, int skip, out Vector3 pos, out Vector3 fwd, out string name)
    {
        pos = fwd = Vector3.zero; name = null; var all = new List<Beast>(); foreach (var h in herds) all.AddRange(h.beasts);
        all.Sort((a, b) => (a.pos.normalized - dir.normalized).sqrMagnitude.CompareTo((b.pos.normalized - dir.normalized).sqrMagnitude));
        int idx = 0; string last = null;
        foreach (var b in all) { if (b.breed.name == last) continue; last = b.breed.name; if (idx++ < skip) continue; pos = b.pos; fwd = b.root.forward; name = b.breed.name; return true; }
        return false;
    }

    // ---------- soorten ----------
    class Breed
    {
        public string name, file; public float len, walk, run, shy; public bool forest; public int min, max;
        public GameObject asset; public AnimationClip[] clips; public float scale = 1f; public Quaternion fix = Quaternion.identity; public bool ok;
    }
    const int AIdle = 0, AWalk = 1, AEat = 2, ARun = 3;
    static Dictionary<string, Breed> breeds;
    static readonly string[] ForestBreeds = { "hert", "hert", "edelhert" }, MeadowBreeds = { "koe", "koe", "paard", "wit paard", "ezel", "alpaca" };   // de stier loopt bij de koeien
    static Breed B(string n) { if (breeds == null) MakeBreeds(); return breeds.TryGetValue(n, out var b) && b.ok ? b : null; }
    static void MakeBreeds()
    {
        breeds = new Dictionary<string, Breed>();
        void Add(string n, string file, float len, float walk, float run, float shy, bool forest, int min, int max)
        {
            var b = new Breed { name = n, file = file, len = len, walk = walk, run = run, shy = shy, forest = forest, min = min, max = max };
            Load(b); breeds[n] = b;
        }
        //  naam        bestand        lengte  loop  ren   schuw (m)  bos    kudde
        Add("hert", "Deer", 1.6f, 1.1f, 9f, 28f, true, 2, 4);
        Add("edelhert", "Stag", 2.0f, 1.1f, 9f, 32f, true, 1, 2);
        Add("koe", "Cow", 2.3f, 0.8f, 4f, 7f, false, 3, 6);
        Add("stier", "Bull", 2.5f, 0.8f, 4.5f, 6f, false, 1, 1);
        Add("paard", "Horse", 2.3f, 1.3f, 9f, 12f, false, 2, 4);
        Add("wit paard", "Horse_White", 2.3f, 1.3f, 9f, 12f, false, 1, 3);
        Add("ezel", "Donkey", 1.9f, 0.9f, 5f, 8f, false, 2, 3);
        Add("alpaca", "Alpaca", 1.7f, 1.0f, 6f, 10f, false, 3, 5);
    }
    // het model en de animaties laden; de maat en de looprichting meten aan de omvang van het model
    static void Load(Breed b)
    {
#if UNITY_EDITOR
        string path = "Assets/ThirdParty/Animals/Ultimate Animated Animals - July 2021/FBX/" + b.file + ".fbx";
        b.asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (b.asset == null) return;
        var clips = new List<AnimationClip>();
        foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path)) if (o is AnimationClip c && !c.name.StartsWith("__preview__")) clips.Add(c);
        AnimationClip Find(params string[] names)
        {
            foreach (var n in names) foreach (var c in clips) if (c.name == n || c.name.EndsWith("|" + n)) return c;
            return null;
        }
        b.clips = new[] { Find("Idle", "Idle_2"), Find("Walk"), Find("Eating", "Idle_Headlow", "Idle_2_HeadLow"), Find("Gallop") };
        if (b.clips[AIdle] == null || b.clips[AWalk] == null) return;
        for (int i = 0; i < 4; i++) if (b.clips[i] == null) b.clips[i] = b.clips[i == ARun ? AWalk : AIdle];
        // maat: de langste kant op de grond naar de echte lengte; ligt die langs x, dan een kwartslag draaien
        var go = Object.Instantiate(b.asset); go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var bd = new Bounds(); bool has = false;
        foreach (var r in go.GetComponentsInChildren<Renderer>()) { if (!has) { bd = r.bounds; has = true; } else bd.Encapsulate(r.bounds); }
        Object.DestroyImmediate(go);
        if (!has) return;
        float lx = bd.size.x, lz = bd.size.z; b.scale = b.len / Mathf.Max(0.01f, Mathf.Max(lx, lz));
        if (lx > lz) b.fix = Quaternion.Euler(0f, bd.center.x > 0f ? -90f : 90f, 0f);   // (de kop ligt aan de kant waar de meeste massa zit: een gok, de test kijkt)
        b.ok = true;
#endif
    }

    // ---------- kuddes ----------
    class Beast
    {
        public Breed breed; public Transform root, model; public PlayableGraph graph; public AnimationMixerPlayable mix; public AnimationClipPlayable[] cp;
        public int anim = AIdle; public float[] w = new float[4]; public Vector3 pos, fwd, target; public int st, cell = -1; public float t, cellT, speed;
    }
    const int SIdle = 0, SEat = 1, SWalk = 2, SRun = 3;
    class Herd { public readonly List<Beast> beasts = new List<Beast>(); public bool forest; public int cell; public float moveT, spread; }
    readonly List<Herd> herds = new List<Herd>();
    readonly List<int> nearForest = new List<int>(), nearMeadow = new List<int>();

    // de gordel rond de startstad (als die er is: op het startvak staat iets)
    // (het startvak zelf is leeg, met de vlag: kijk of er gebouwen in de buurt staan; eens per seconde)
    static bool HasTown => town;
    static bool town;
    static void CheckTown()
    {
        town = false; int s = PlanetCity.StartCell; if (s < 0 || Planet.Cells == null || s >= Planet.Cells.Length) return;
        var seen = new HashSet<int> { s }; var ring = new List<int> { s }; int built = 0;
        for (int k = 0; k < 4 && built < 4; k++)
        {
            var next = new List<int>();
            foreach (int c in ring) foreach (int n in Planet.Cells[c].nb) if (seen.Add(n)) { next.Add(n); if (PlanetCity.Cells.TryGetValue(n, out var l) && l.Count > 0 && !Natural(l)) built++; }
            ring = next;
        }
        town = built >= 4;
    }
    // Rond de startstad: bosdieren (herten) in de gordel, in het bos (en op open plekken daartussen); weidedieren (koeien,
    // paarden, ezels, alpaca's) op het open gras en de heide buiten de gordel, tot MeadowOut. Zonder stad: bos en weide overal.
    public const float MeadowOut = 900f;
    static float FromTown(int c) => Vector3.Angle(Planet.Cells[PlanetCity.StartCell].dir, Planet.Cells[c].dir) * Mathf.Deg2Rad * Planet.R;
    static int Ground(int c)   // 0 niets voor dieren, 1 bos, 2 weide, 3 open plek in de gordel (alleen herten)
    {
        if (PlanetCity.IsWalk(c)) return 0;
        float h = PlanetCity.Terrain(c); if (h < PlanetCity.SeaLevel + 0.3f || h > PlanetCity.TreeLine) return 0;
        int k;
        if (!PlanetCity.Cells.TryGetValue(c, out var l) || l.Count == 0) k = 2;
        else if (l.Count == 1 && l[0] == "park") k = 1;
        else if (l.Count == 1 && (l[0] == "heide" || l[0] == "droogheide")) k = 2;
        else return 0;
        if (HasTown)
        {
            float d = FromTown(c);
            if (d >= BeltIn && d <= BeltOut) k = k == 1 ? 1 : 3;
            else if (d > BeltOut && d <= MeadowOut && k == 2) k = 2;
            else return 0;
        }
        return PlanetCity.Wet(c) ? 0 : k;
    }
    // herten: bos en open plekken (zonder stad ook de weide); weidedieren alleen de weide
    bool Fits(int c, Herd h) { int g = Ground(c); return h.forest ? g == 1 || g == 3 || g == 2 && !HasTown : g == 2; }

    void UpdateNearGround()
    {
        CheckTown(); nearForest.Clear(); nearMeadow.Clear();
        float lim = viewR / Planet.R;
        foreach (int i in nearLand)
        {
            if ((Planet.Cells[i].dir - viewAt).magnitude > lim) continue;
            int g = Ground(i); if (g == 1) nearForest.Add(i); else if (g == 2) nearMeadow.Add(i);
        }
    }

    int TargetHerds()
    {
        if (!On || viewK <= 0.05f) return 0;
        int n = (nearForest.Count + nearMeadow.Count) / 20;
        return Mathf.Clamp(Mathf.RoundToInt(n * viewK), 0, MaxHerds);
    }

    float groundNearT;
    void StepHerds(float dt)
    {
        if ((groundNearT -= dt) <= 0f || nearT >= 0.49f) { groundNearT = 1f; UpdateNearGround(); }
        int target = TargetHerds();
        for (int i = herds.Count - 1; i >= 0 && herds.Count > target; i--) if (target == 0 || !HerdVisible(herds[i])) RemoveHerd(i);
        for (int i = herds.Count - 1; i >= 0; i--) if (herds[i].beasts.Count == 0 || Far(herds[i].beasts[0].pos) && !HerdVisible(herds[i])) RemoveHerd(i);
        if (herds.Count < target && BeastCount < MaxBeasts) SpawnHerd(herds.Count == 0);
        foreach (var h in herds) StepHerd(h, dt);
    }
    bool HerdVisible(Herd h) { foreach (var b in h.beasts) if (Visible(b.pos + b.pos.normalized)) return true; return false; }
    void RemoveHerd(int i) { foreach (var b in herds[i].beasts) KillBeast(b); herds.RemoveAt(i); }
    void KillBeast(Beast b) { if (b.graph.IsValid()) b.graph.Destroy(); if (b.root != null) Destroy(b.root.gameObject); }
    void ClearHerds() { for (int i = herds.Count - 1; i >= 0; i--) RemoveHerd(i); }

    void SpawnHerd(bool anyView)
    {
        bool forest = nearForest.Count > 0 && (nearMeadow.Count == 0 || Random.value < Mathf.Clamp(nearForest.Count / (float)(nearForest.Count + nearMeadow.Count), 0.35f, 0.65f));
        var list = forest ? nearForest : nearMeadow; if (list.Count == 0) return;
        Breed br = null; for (int k = 0; k < 6 && br == null; k++) { var names = forest ? ForestBreeds : MeadowBreeds; br = B(names[Random.Range(0, names.Length)]); }
        if (br == null) return;
        for (int k = 0; k < 10; k++)
        {
            int c = list[Random.Range(0, list.Count)]; Vector3 p = Planet.Cells[c].dir * (Planet.R + PlanetCity.TerrainAt(Planet.Cells[c].dir));
            if (!anyView && Visible(p)) continue;
            var h = new Herd { forest = forest, cell = c, moveT = Random.Range(15f, 40f), spread = forest ? 9f : 12f };
            int n = Mathf.Min(Random.Range(br.min, br.max + 1), MaxBeasts - BeastCount);
            for (int i = 0; i < n; i++)
            {
                var b2 = br;
                if (br.name == "koe" && i == 0 && Random.value < 0.3f) b2 = B("stier") ?? br;   // soms een stier bij de koeien
                if (br.name == "hert" && i == 0 && Random.value < 0.4f) b2 = B("edelhert") ?? br;   // en een edelhert bij de herten
                if (!Spot(h, out Vector3 sp)) continue;
                var b = MakeBeast(b2); b.pos = sp; b.cell = Planet.Nearest(sp); b.fwd = Vector3.Cross(sp.normalized, Random.onUnitSphere).normalized;
                b.st = Random.value < 0.6f ? SEat : SIdle; b.t = Random.Range(1f, 8f); b.anim = b.st == SEat ? AEat : AIdle; b.w[b.anim] = 1f;
                h.beasts.Add(b); PlaceBeast(b, 0f);
            }
            if (h.beasts.Count > 0) herds.Add(h);
            return;
        }
    }

    // een plek in de kudde: rond het midden van de kudde, op passende grond
    bool Spot(Herd h, out Vector3 p)
    {
        var cl = Planet.Cells[h.cell]; Vector3 e = Vector3.Cross(Mathf.Abs(cl.dir.y) < 0.95f ? Vector3.up : Vector3.right, cl.dir).normalized, n = Vector3.Cross(cl.dir, e);
        for (int k = 0; k < 8; k++)
        {
            float a = Random.value * 6.28f, r = Mathf.Sqrt(Random.value) * h.spread;
            Vector3 d = (cl.dir * Planet.R + (e * Mathf.Cos(a) + n * Mathf.Sin(a)) * r).normalized;
            if (!Fits(Planet.Nearest(d * Planet.R), h)) continue;
            p = d * (Planet.R + PlanetCity.TerrainAt(d)); return true;
        }
        p = Vector3.zero; return false;
    }

    Beast MakeBeast(Breed br)
    {
        var b = new Beast { breed = br };
        var root = new GameObject(br.name); root.transform.SetParent(transform, false); b.root = root.transform;
        var m = Instantiate(br.asset, b.root, false); b.model = m.transform; b.model.localRotation = br.fix; b.model.localScale = Vector3.one * br.scale;
        foreach (var col in m.GetComponentsInChildren<Collider>()) Destroy(col);
        var an = m.GetComponentInChildren<Animator>(); if (an == null) an = m.AddComponent<Animator>();
        an.applyRootMotion = false; an.cullingMode = AnimatorCullingMode.CullCompletely; an.runtimeAnimatorController = null;
        b.graph = PlayableGraph.Create("dier"); b.graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        b.mix = AnimationMixerPlayable.Create(b.graph, 4); b.cp = new AnimationClipPlayable[4];
        for (int i = 0; i < 4; i++) { b.cp[i] = AnimationClipPlayable.Create(b.graph, br.clips[i]); b.cp[i].SetTime(Random.value * br.clips[i].length); b.graph.Connect(b.cp[i], 0, b.mix, i); }
        var output = AnimationPlayableOutput.Create(b.graph, "uit", an); output.SetSourcePlayable(b.mix);
        b.graph.Play();
        return b;
    }

    void StepHerd(Herd h, float dt)
    {
        // de kudde trekt af en toe een stukje verder (naar passende grond in de buurt)
        if ((h.moveT -= dt) <= 0f)
        {
            h.moveT = Random.Range(25f, 60f);
            var cl = Planet.Cells[h.cell]; int next = cl.nb[Random.Range(0, cl.nb.Length)];
            if (Random.value < 0.5f) { var c2 = Planet.Cells[next]; next = c2.nb[Random.Range(0, c2.nb.Length)]; }
            if (Fits(next, h)) h.cell = next;
        }
        bool allGone = true;
        foreach (var b in h.beasts) { StepBeast(h, b, dt); if (b.cell >= 0 && Fits(b.cell, h)) allGone = false; }
        if (allGone && !HerdVisible(h)) { foreach (var b in h.beasts) KillBeast(b); h.beasts.Clear(); }   // er is gebouwd: weg
    }

    void StepBeast(Herd h, Beast b, float dt)
    {
        var br = b.breed; b.t -= dt;
        switch (b.st)
        {
            case SIdle:
            case SEat:
                if (b.t <= 0f)
                {
                    if (Random.value < 0.55f && Spot(h, out Vector3 to)) { b.st = SWalk; b.target = to; b.t = 30f; }
                    else { b.st = Random.value < 0.65f ? SEat : SIdle; b.t = b.st == SEat ? Random.Range(5f, 14f) : Random.Range(2f, 6f); }
                }
                break;
            case SWalk:
            case SRun:
                float sp = b.st == SRun ? br.run : br.walk;
                Vector3 up = b.pos.normalized, to2 = Vector3.ProjectOnPlane(b.target - b.pos, up); float dist = to2.magnitude;
                if (dist < 0.4f || b.t <= 0f) { b.st = b.st == SRun ? SIdle : (Random.value < 0.7f ? SEat : SIdle); b.t = Random.Range(3f, 10f); break; }
                Vector3 want = to2 / dist;
                b.fwd = Vector3.RotateTowards(Vector3.ProjectOnPlane(b.fwd, up).normalized, want, (b.st == SRun ? 4f : 2f) * dt, 0f);
                // niet tegen een kuddegenoot aan
                foreach (var o in h.beasts) if (o != b) { Vector3 dd = b.pos - o.pos; float m = dd.magnitude; if (m < br.len * 0.8f && m > 0.01f) b.fwd = (b.fwd + Vector3.ProjectOnPlane(dd, up) / m * 0.3f).normalized; }
                Vector3 np = b.pos + b.fwd * sp * Mathf.Clamp01(Vector3.Dot(b.fwd, want) + 0.3f) * dt;
                Vector3 nd = np.normalized;
                if ((b.cellT -= dt) <= 0f || b.st == SRun) { b.cellT = 0.25f; int c = Planet.Nearest(np); if (c != b.cell && !Fits(c, h)) { b.st = SIdle; b.t = Random.Range(1f, 3f); break; } b.cell = c; }   // niet de weg, het water of een gebouw op
                b.pos = nd * (Planet.R + PlanetCity.TerrainAt(nd));
                break;
        }
        int want2 = b.st == SRun ? ARun : b.st == SWalk ? AWalk : b.st == SEat ? AEat : AIdle;
        if (want2 != b.anim) b.anim = want2;
        PlaceBeast(b, dt);
    }

    void PlaceBeast(Beast b, float dt)
    {
        Vector3 up = b.pos.normalized, f = Vector3.ProjectOnPlane(b.fwd, up); if (f.sqrMagnitude < 1e-6f) f = Vector3.Cross(up, Vector3.right);
        b.root.SetPositionAndRotation(b.pos, Geo.Look(f.normalized, up));
        // de animaties vloeiend overvloeien, en zelf laten herhalen
        float k = dt <= 0f ? 1f : Mathf.Clamp01(dt * 5f);
        for (int i = 0; i < 4; i++)
        {
            b.w[i] = Mathf.MoveTowards(b.w[i], i == b.anim ? 1f : 0f, k);
            b.mix.SetInputWeight(i, b.w[i]);
            var clip = b.breed.clips[i]; double t = b.cp[i].GetTime(); if (clip.length > 0f && t > clip.length) b.cp[i].SetTime(t % clip.length);
        }
    }

    // schrikken: de hele kudde rent weg van het gevaar (herten al van ver, koeien pas vlakbij)
    void ScareHerds(bool boom, Vector3 boomAt)
    {
        foreach (var h in herds)
        {
            Vector3 from = Vector3.zero; bool scared = false;
            foreach (var b in h.beasts)
            {
                if (boom && (boomAt - b.pos).magnitude < 250f) { from = boomAt; scared = true; break; }
                for (int i = 0; i < danger.Count; i++)
                {
                    float r = dangerKind[i] == 0 ? b.breed.shy : dangerKind[i] == 1 ? Mathf.Max(8f, b.breed.shy * 0.5f) : b.breed.shy * 0.35f;
                    if ((danger[i] - b.pos).sqrMagnitude < r * r) { from = danger[i]; scared = true; break; }
                }
                if (scared) break;
            }
            if (!scared) continue;
            foreach (var b in h.beasts)
            {
                Vector3 up = b.pos.normalized, away = Vector3.ProjectOnPlane(b.pos - from, up); if (away.sqrMagnitude < 0.01f) away = Vector3.Cross(up, Random.onUnitSphere);
                away = Quaternion.AngleAxis(Random.Range(-25f, 25f), up) * away.normalized;
                if (b.st == SRun && Vector3.Dot(away, Vector3.ProjectOnPlane(b.target - b.pos, up).normalized) > 0.5f) continue;   // rent al die kant op
                b.st = SRun; b.t = Random.Range(4f, 7f); b.target = b.pos + away * b.breed.run * 6f;
            }
            // de kudde verhuist mee: het midden schuift die kant op
            var hb = h.beasts[0]; int c = Planet.Nearest(hb.pos + Vector3.ProjectOnPlane(hb.pos - from, hb.pos.normalized).normalized * 30f);
            if (Fits(c, h)) h.cell = c;
        }
    }
}
