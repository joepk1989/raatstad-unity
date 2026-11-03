using System.Collections.Generic;
using UnityEngine;

// Experimenteel: een meteoor uit de ruimte. Een gloeiende rots met een vuurstaart en een rookspoor komt schuin aan, gezien
// vanaf de camera, en wordt feller in de atmosfeer (onder zo'n 400 m). Bij de inslag: een flits, een zeshoekige schokgolf over
// het terrein, stof, vuur en brokstukken en de camera schudt. Het geluid staat in PlanetMeteor.Sound.cs. De krater (Crater) maakt
// PlanetBuilder.Meteor.cs bij de inslag, met undo ervoor.
// Maat 0, 1, 2: een kom van 1, 7 of 19 vakken (zoals de penselen) met een opgehoogde rand eromheen. Op droog land blijft de kom
// droog (de bodem net boven het zeeniveau, de rand hoger); ligt er water vlak naast, dan breekt de wand door en stroomt de kom
// na een paar seconden vol; een inslag in het water zelf is meteen nat.
[DefaultExecutionOrder(-1000)]
public partial class PlanetMeteor : MonoBehaviour
{
    static PlanetMeteor I;
    public static readonly string[] SizeNames = { "Klein", "Middel", "Groot" };
    public static int Flying => I == null ? 0 : I.rocks.Count;
    static readonly float[] RockR = { 2.5f, 4.5f, 7f }, Depth = { 5f, 9f, 14f };
    const float CellW = 15.6f;

    class Rock
    {
        public Vector3 from, to; public float t, dur; public int cell, size; public System.Action<int, int> hit;
        public GameObject go; public Transform halo; public Material rockMat, haloMat; public TrailRenderer fire, smoke; public Light glow;
        public AudioSource roar; public AudioLowPassFilter roarLp; public bool sonic;
    }
    class Flood { public float t; public Vector3 at; public int size; public List<(int c, float dry, float wet)> cells; public System.Action done; }
    readonly List<Flood> floods = new List<Flood>();
    class Blast { public Vector3 at; public int cell, size; public float t; public LineRenderer ring; public Light flash; public Transform ball; public Material ballMat; public Vector3[] hex; }
    readonly List<Rock> rocks = new List<Rock>();
    readonly List<Blast> blasts = new List<Blast>();
    ParticleSystem dustPs, firePs, debrisPs; Mesh ico; Material addMat, alphaMat;
    float shake, shakeT; Vector3 shakeOfs, shaken; ParticleSystem.Particle[] parts = new ParticleSystem.Particle[1500];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { I = null; ResetClips(); }
    static PlanetMeteor Get()
    {
        if (I == null) { I = new GameObject("Meteoren").AddComponent<PlanetMeteor>(); }
        return I;
    }

    // een meteoor op vak cell; hit(cell, size) bij de inslag (daar komt de krater)
    public static void Launch(int cell, int size, Camera cam, System.Action<int, int> hit)
    {
        var M = Get(); size = Mathf.Clamp(size, 0, 2); var c = Planet.Cells[cell]; Vector3 up = c.dir;
        Vector3 to = up * (Planet.R + PlanetCity.TerrainAt(up));
        Vector3 cp = cam != null ? cam.transform.position : to + up * 300f, cr = cam != null ? cam.transform.right : c.east;
        Vector3 side = Vector3.ProjectOnPlane(cr, up).normalized, back = Vector3.ProjectOnPlane(cp - to, up).normalized;
        if (side.sqrMagnitude < 0.5f) side = c.east; if (back.sqrMagnitude < 0.5f) back = c.north;
        float L = Mathf.Clamp(Vector3.Distance(cp, to) * 1.6f, 900f, 2600f), s = Random.value < 0.5f ? -1f : 1f;
        Vector3 from = to + (up * 0.72f + side * s * 0.6f + back * 0.25f).normalized * L;   // schuin, van opzij en iets van de camera af: je ziet hem het beeld in vliegen
        var r = new Rock { from = from, to = to, dur = Mathf.Clamp(L / 520f, 2.2f, 4.5f), cell = cell, size = size, hit = hit };
        M.MakeRock(r); M.rocks.Add(r); M.StartRoar(r);
        Sounds.Fx("whoosh", 0.8f);
    }

    // ---------- de krater: een zeshoekige kom met een opgehoogde rand, op hexvakken ----------
    // kom (size stappen), rand (size+1) en puin (size+2); gebouwen, wegen en bomen in kom en rand zijn weg; geeft het aantal verzette vakken.
    // flooded: als het water er later in stroomt (dan opnieuw tekenen en bewaren)
    public static int Crater(int c0, int size, System.Action flooded = null)
    {
        int b = Mathf.Clamp(size, 0, 2), rim = b + 1, outer = b + 2; float D = Depth[b], dryFloor = PlanetCity.SeaLevel + 0.4f;
        var cells = new List<(int c, int d)> { (c0, 0) }; var seen = new HashSet<int> { c0 }; int from = 0;
        for (int d = 1; d <= outer + 1; d++) { int to = cells.Count; for (int k = from; k < to; k++) foreach (int j in Planet.Cells[cells[k].c].nb) if (seen.Add(j)) cells.Add((j, d)); from = to; }
        // water: de inslag in het water zelf, of een zee, meer of gracht vlak naast de krater (vóór het verwoesten: een meer verdwijnt dan)
        bool inWater = PlanetCity.G(c0) < PlanetCity.SeaLevel, nearWater = false;
        foreach (var x in cells) if (PlanetCity.G(x.c) < PlanetCity.SeaLevel || (PlanetCity.Cells.TryGetValue(x.c, out var wl) && wl.Count > 0 && (wl[0] == "meer" || wl[0] == "water"))) nearWater = true;
        cells.RemoveAll(x => x.d > outer);
        // eerst verwoesten (een weg op een niveau zet zijn terrein terug, een brug geeft zijn water terug: dan nog eens)
        foreach (var x in cells) if (x.d <= rim) for (int n = 0; n < 4 && PlanetCity.Cells.ContainsKey(x.c); n++) if (!PlanetCity.Clear(x.c)) break;
        float baseH = 0f; int nb = 0; foreach (var x in cells) if (x.d <= rim) { baseH += PlanetCity.G(x.c); nb++; } baseH /= nb;
        // de kom zoals hij nat hoort; droog ligt de bodem net boven het water en gaat het tekort naar de rand, zodat de kom diep blijft
        var wet = new List<(int c, int d, float h)>(); float deficit = 0f;
        foreach (var x in cells)
        {
            if (x.d > rim && PlanetCity.Cells.ContainsKey(x.c) && (PlanetCity.Plateau(x.c) || PlanetCity.IsWalk(x.c))) continue;   // in het puin eromheen blijven gebouwen en wegen staan
            float h = PlanetCity.G(x.c), nh;
            if (x.d <= b) { float u = (float)x.d / rim; nh = Mathf.Lerp(h, baseH, 0.6f) - D * (1f - u * u); deficit = Mathf.Max(deficit, dryFloor - nh); }   // de kom: in het midden het diepst
            else if (x.d == rim) nh = Mathf.Max(h, baseH) + D * 0.35f;                                                                                        // de opgeworpen rand
            else nh = h + D * (b == 0 ? 0.06f : 0.12f);                                                                                                       // het puin eromheen
            wet.Add((x.c, x.d, nh));
        }
        int changed = 0; var later = new List<(int c, float dry, float wet)>();
        foreach (var x in wet)
        {
            float nh = x.h;
            if (!inWater) { if (x.d <= b) nh = Mathf.Max(nh, dryFloor); else nh += deficit * (x.d == rim ? 0.5f : 0.2f); }
            if (PlanetCity.SetTerr(x.c, nh)) changed++;
            PlanetCity.Touch(x.c);
            if (!inWater && nearWater && x.d <= b && x.h < nh - 0.01f) later.Add((x.c, PlanetCity.G(x.c), x.h));
        }
        if (later.Count > 0 && I != null) I.floods.Add(new Flood { t = 5f + b, at = Planet.Cells[c0].dir * Planet.R, size = b, cells = later, done = flooded });
        return changed;
    }
    // de wand breekt door: de kom zakt naar zijn natte diepte en het water stroomt erin (niet als het terrein intussen veranderde, bijvoorbeeld door undo)
    void UpdateFloods(float dt)
    {
        for (int k = floods.Count - 1; k >= 0; k--)
        {
            var f = floods[k]; f.t -= dt; if (f.t > 0f) continue; floods.RemoveAt(k);
            bool same = true; foreach (var x in f.cells) if (Mathf.Abs(PlanetCity.G(x.c) - x.dry) > 0.02f) { same = false; break; }
            if (!same) continue;
            foreach (var x in f.cells) { PlanetCity.SetTerr(x.c, x.wet); PlanetCity.Touch(x.c); }
            FloodSound(f.at, f.size);
            f.done?.Invoke();
        }
    }

    // ---------- vlucht ----------
    void MakeRock(Rock r)
    {
        Ensure(); float rr = RockR[r.size];
        r.go = new GameObject("Meteoor");
        var mesh = new GameObject("Rots"); mesh.transform.SetParent(r.go.transform, false); mesh.transform.localScale = Vector3.one * rr * 2f;
        mesh.AddComponent<MeshFilter>().sharedMesh = RockMesh(); var mr = mesh.AddComponent<MeshRenderer>();
        r.rockMat = Raatstad.MakeMat(new Color(0.5f, 0.2f, 0.1f), true); mr.sharedMaterial = r.rockMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var halo = new GameObject("Gloed"); halo.transform.SetParent(r.go.transform, false); r.halo = halo.transform;
        halo.AddComponent<MeshFilter>().sharedMesh = ico; var hr = halo.AddComponent<MeshRenderer>(); r.haloMat = new Material(addMat); hr.sharedMaterial = r.haloMat; hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.fire = Trail(r.go, "Vuurstaart", addMat, rr * 2.2f, 0.9f, new[] { (new Color(1f, 0.95f, 0.7f), 0f), (new Color(1f, 0.55f, 0.12f), 0.35f), (new Color(0.7f, 0.12f, 0.03f), 1f) }, 1f);
        r.smoke = Trail(r.go, "Rookspoor", alphaMat, rr * 2.6f, 6f, new[] { (new Color(0.35f, 0.32f, 0.3f), 0f), (new Color(0.55f, 0.53f, 0.5f), 0.5f), (new Color(0.7f, 0.7f, 0.68f), 1f) }, 0.55f);
        r.glow = r.go.AddComponent<Light>(); r.glow.type = LightType.Point; r.glow.color = new Color(1f, 0.6f, 0.25f); r.glow.range = 120f; r.glow.intensity = 0f; r.glow.shadows = LightShadows.None;
        r.go.transform.position = r.from;
    }
    static TrailRenderer Trail(GameObject on, string name, Material m, float w, float time, (Color c, float t)[] keys, float a0)
    {
        var go = new GameObject(name); go.transform.SetParent(on.transform, false);
        var tr = go.AddComponent<TrailRenderer>(); tr.sharedMaterial = m; tr.time = time; tr.minVertexDistance = 3f; tr.widthMultiplier = w;
        tr.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.15f)); tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var g = new Gradient(); var ck = new GradientColorKey[keys.Length]; for (int k = 0; k < keys.Length; k++) ck[k] = new GradientColorKey(keys[k].c, keys[k].t);
        g.SetKeys(ck, new[] { new GradientAlphaKey(a0, 0f), new GradientAlphaKey(a0 * 0.6f, 0.4f), new GradientAlphaKey(0f, 1f) }); tr.colorGradient = g;
        return tr;
    }

    void Update()
    {
        if (Cam() != null && shakeOfs != Vector3.zero && Cam().transform.position == shaken) Cam().transform.position -= shakeOfs;   // het schudden van het vorige beeld eraf (als niemand de camera intussen zelf neerzette)
        shakeOfs = Vector3.zero;
        float dt = Time.deltaTime;
        for (int k = rocks.Count - 1; k >= 0; k--)
        {
            var r = rocks[k]; r.t = Mathf.Min(1f, r.t + dt / r.dur); float e = r.t * (0.55f + 0.45f * r.t);   // versnelt
            Vector3 p = Vector3.Lerp(r.from, r.to, e); r.go.transform.position = p; r.go.transform.Rotate(new Vector3(0.3f, 1f, 0.2f), 240f * dt);
            float alt = p.magnitude - Planet.R, atmo = 1f - Mathf.Clamp01((alt - 80f) / 420f), rr = RockR[r.size];
            r.rockMat.color = Color.Lerp(new Color(0.55f, 0.22f, 0.1f), new Color(1f, 0.92f, 0.65f), atmo);
            if (r.rockMat.HasProperty("_BaseColor")) r.rockMat.SetColor("_BaseColor", r.rockMat.color);
            r.halo.localScale = Vector3.one * rr * (2.6f + 3.5f * atmo) * 2f;
            var hc = new Color(1f, 0.55f + 0.3f * atmo, 0.2f + 0.3f * atmo, 0.2f + 0.6f * atmo); r.haloMat.SetColor("_BaseColor", hc);
            r.fire.widthMultiplier = rr * (1.4f + 2.2f * atmo); r.fire.time = 0.5f + 0.9f * atmo;
            r.smoke.emitting = atmo > 0.05f; r.glow.intensity = 6f * atmo * (1f + r.size); r.glow.range = 80f + 60f * r.size;
            RoarUpdate(r, p, atmo);
            if (r.t >= 1f) { Impact(r); rocks.RemoveAt(k); }
        }
        for (int k = blasts.Count - 1; k >= 0; k--) if (!UpdateBlast(blasts[k], dt)) blasts.RemoveAt(k);
        Debris(dt); UpdateFloods(dt);
    }

    void LateUpdate()
    {
        var cam = Cam(); if (cam == null || shake <= 0.001f) return;
        shakeT += Time.deltaTime; float a = shake * Mathf.Exp(-shakeT * 2.2f); if (a < 0.01f) { shake = 0f; return; }
        float t = Time.time * 31f; shakeOfs = (cam.transform.right * (Mathf.PerlinNoise(t, 0.3f) - 0.5f) + cam.transform.up * (Mathf.PerlinNoise(0.7f, t) - 0.5f)) * 2f * a;
        cam.transform.position += shakeOfs; shaken = cam.transform.position;
    }
    static Camera Cam() => Camera.main;

    // ---------- inslag ----------
    void Impact(Rock r)
    {
        Vector3 at = r.to, up = at.normalized; int b = r.size; float rc = (b + 1) * CellW;
        // eerst het licht en de deeltjes, dan de krater (die is in een klik klaar; het stof verbergt het tekenen)
        r.fire.transform.SetParent(null); r.smoke.transform.SetParent(null); r.fire.emitting = r.smoke.emitting = false;
        Destroy(r.fire.gameObject, r.fire.time + 0.5f); Destroy(r.smoke.gameObject, r.smoke.time + 0.5f); Destroy(r.go); Destroy(r.rockMat); Destroy(r.haloMat);
        var bl = new Blast { at = at, cell = r.cell, size = b };
        var lg = new GameObject("Flits"); lg.transform.position = at + up * 6f; bl.flash = lg.AddComponent<Light>(); bl.flash.type = LightType.Point; bl.flash.color = new Color(1f, 0.85f, 0.6f); bl.flash.range = rc * 9f; bl.flash.intensity = 40f; bl.flash.shadows = LightShadows.None;
        var ball = new GameObject("Vuurbal"); ball.transform.position = at; ball.AddComponent<MeshFilter>().sharedMesh = ico; var br = ball.AddComponent<MeshRenderer>(); bl.ballMat = new Material(addMat); br.sharedMaterial = bl.ballMat; br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; bl.ball = ball.transform;
        bl.ring = new GameObject("Schokgolf").AddComponent<LineRenderer>(); bl.ring.loop = true; bl.ring.useWorldSpace = true; bl.ring.sharedMaterial = addMat; bl.ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var c = Planet.Cells[r.cell]; const int per = 8; int nc = c.corners.Length; bl.hex = new Vector3[nc * per];   // zeshoekig, met de vakken mee (een vijfhoek op een icosaëderhoek)
        for (int k = 0; k < nc; k++) for (int s = 0; s < per; s++)
        {
            Vector3 a = Vector3.ProjectOnPlane(c.corners[k] - c.dir, up), n = Vector3.ProjectOnPlane(c.corners[(k + 1) % nc] - c.dir, up);
            bl.hex[k * per + s] = Vector3.Lerp(a, n, s / (float)per) / a.magnitude;
        }
        bl.ring.positionCount = bl.hex.Length;
        blasts.Add(bl);
        // stof, vuur, brokstukken
        Vector3 e = c.east, nn = c.north;
        for (int k = 0; k < 90 * (b + 1); k++)
        {
            Vector2 q = Random.insideUnitCircle; Vector3 o = (e * q.x + nn * q.y) * rc, out1 = (e * q.x + nn * q.y).normalized;
            Emit(dustPs, at + o + up * Random.Range(0f, 4f), out1 * Random.Range(4f, 22f) * (1f + 0.5f * b) + up * Random.Range(3f, 18f) * (1f + 0.4f * b), rc * Random.Range(0.25f, 0.55f), Color.Lerp(new Color(0.42f, 0.36f, 0.3f), new Color(0.7f, 0.66f, 0.6f), Random.value), Random.Range(5f, 10f));
        }
        for (int k = 0; k < 40 * (b + 1); k++)
            Emit(firePs, at + up * 2f + Random.insideUnitSphere * rc * 0.3f, (Random.onUnitSphere + up * 0.8f).normalized * Random.Range(6f, 26f) * (1f + 0.4f * b), rc * Random.Range(0.18f, 0.4f), Color.white, Random.Range(0.6f, 1.4f));
        for (int k = 0; k < 70 * (b + 1); k++)
        {
            Vector2 q = Random.insideUnitCircle.normalized; Vector3 dir = (e * q.x + nn * q.y) * Random.Range(0.3f, 1f) + up * Random.Range(0.6f, 1.6f);
            Emit(debrisPs, at + up * 1.5f + (e * q.x + nn * q.y) * Random.Range(0f, rc * 0.5f), dir * Random.Range(10f, 30f) * (1f + 0.25f * b), Random.Range(0.4f, 1.6f) * (1f + 0.5f * b), Color.Lerp(new Color(0.25f, 0.2f, 0.17f), new Color(0.5f, 0.42f, 0.35f), Random.value), 12f);
        }
        // de camera schudt, harder dichtbij
        var cam = Cam(); float dist = cam != null ? Vector3.Distance(cam.transform.position, at) : 500f;
        shake = Mathf.Max(shake * Mathf.Exp(-shakeT * 2.2f), (0.8f + 0.9f * b) * Mathf.Clamp01(rc * 7f / Mathf.Max(1f, dist))); shakeT = 0f;
        StopRoar(r);
        bool splash = PlanetCity.G(r.cell) < PlanetCity.SeaLevel;   // midden in het water
        PlanetLife.Blast(at, rc + CellW);
        r.hit?.Invoke(r.cell, b);
        ImpactSound(at, b, dist, splash);
    }

    bool UpdateBlast(Blast bl, float dt)
    {
        bl.t += dt; float rc = (bl.size + 1) * CellW, up01 = Mathf.Clamp01(bl.t / 1.8f);
        bl.flash.intensity = 40f * Mathf.Exp(-bl.t * 5f);
        float bt = Mathf.Clamp01(bl.t / 0.7f); bl.ball.localScale = Vector3.one * rc * (0.6f + 2.4f * Mathf.Sqrt(bt)) * 2f;
        bl.ballMat.SetColor("_BaseColor", new Color(1f, 0.75f - 0.35f * bt, 0.4f - 0.3f * bt, (1f - bt) * 0.9f));
        Vector3 up = bl.at.normalized; float rad = rc * (0.6f + 7f * (1f - (1f - up01) * (1f - up01)));
        for (int k = 0; k < bl.hex.Length; k++) { Vector3 d = (bl.at + bl.hex[k] * rad).normalized; bl.ring.SetPosition(k, d * (Planet.R + PlanetCity.TerrainAt(d) + 1.2f)); }
        bl.ring.widthMultiplier = 1f + rc * 0.3f * (1f - up01); Color rcol = new Color(1f, 0.85f, 0.6f, (1f - up01) * 0.85f); bl.ring.startColor = bl.ring.endColor = rcol;
        if (bl.t < 2f) return true;
        Destroy(bl.flash.gameObject); Destroy(bl.ball.gameObject); Destroy(bl.ballMat); Destroy(bl.ring.gameObject); return false;
    }

    // brokstukken vallen naar het midden van de bol en blijven niet zweven
    void Debris(float dt)
    {
        if (debrisPs == null || debrisPs.particleCount == 0) return;
        int n = debrisPs.GetParticles(parts);
        for (int k = 0; k < n; k++)
        {
            Vector3 p = parts[k].position, up = p.normalized; parts[k].velocity -= up * 9.81f * dt;
            if (Vector3.Dot(parts[k].velocity, up) < 0f && p.magnitude < Planet.R + PlanetCity.TerrainAt(up) + 0.2f) parts[k].remainingLifetime = 0f;
        }
        debrisPs.SetParticles(parts, n);
    }

    // ---------- deeltjes, materialen, geluid ----------
    void Ensure()
    {
        if (dustPs != null) return;
        ico = Ico(0f); addMat = PartMat(true); alphaMat = PartMat(false);
        var dg = new Gradient(); dg.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0.75f, 0f), new GradientAlphaKey(0.55f, 0.5f), new GradientAlphaKey(0f, 1f) });
        var fg = new Gradient(); fg.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.95f, 0.65f), 0f), new GradientColorKey(new Color(1f, 0.48f, 0.1f), 0.45f), new GradientColorKey(new Color(0.6f, 0.12f, 0.04f), 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
        var rg = new Gradient(); rg.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.9f), new GradientAlphaKey(0f, 1f) });
        dustPs = MakePs("Stof", alphaMat, dg, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 3f)), 0.9f);
        firePs = MakePs("Vuur", addMat, fg, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.3f)), 1.5f);
        debrisPs = MakePs("Brokstukken", Raatstad.MakeMat(Color.white, true), rg, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 1f)), 0f);
        MakeClips();
    }
    ParticleSystem MakePs(string name, Material m, Gradient g, AnimationCurve size, float drag)
    {
        var go = new GameObject(name); go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.playOnAwake = false; main.loop = true; main.maxParticles = 1500; main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f; main.startRotation3D = true; main.gravityModifier = 0f;
        var em = ps.emission; em.enabled = false; var sh = ps.shape; sh.enabled = false;
        var col = ps.colorOverLifetime; col.enabled = true; col.color = new ParticleSystem.MinMaxGradient(g);
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, size);
        if (drag > 0f) { var lv = ps.limitVelocityOverLifetime; lv.enabled = true; lv.limit = 1000f; lv.drag = drag; }
        var r = go.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Mesh; r.mesh = ico; r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play(); return ps;
    }
    static Material PartMat(bool add)
    {
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit"); if (sh == null) sh = Shader.Find("Sprites/Default");
        var m = new Material(sh);
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", add ? 2f : 0f); m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)(add ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = 3000;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        return m;
    }
    static void Emit(ParticleSystem ps, Vector3 at, Vector3 vel, float size, Color col, float life)
    {
        var e = new ParticleSystem.EmitParams { position = at, velocity = vel, startSize = size, startColor = col, startLifetime = life, rotation3D = new Vector3(Random.value, Random.value, Random.value) * 360f, angularVelocity3D = Random.insideUnitSphere * 120f, applyShapeToPosition = false };
        ps.Emit(e, 1);
    }
    // een icosaëder (driehoeken), voor de rots wat ongelijk
    static Mesh Ico(float jitter)
    {
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        var v = new[] { new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0), new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t), new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1) };
        int[] f = { 0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8, 3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1 };
        for (int k = 0; k < v.Length; k++) v[k] = v[k].normalized * 0.5f * (1f + (Random.value - 0.5f) * jitter);
        var mb = new MeshBuilder();
        for (int k = 0; k < f.Length; k += 3) { Vector3 a = v[f[k]], b = v[f[k + 1]], c = v[f[k + 2]]; mb.Tri(a, b, c, a + b + c); }
        return mb.Build();
    }
    Mesh rockMesh; Mesh RockMesh() => rockMesh != null ? rockMesh : (rockMesh = Ico(0.45f));
}
