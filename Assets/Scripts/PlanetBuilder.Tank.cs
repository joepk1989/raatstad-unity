using System.Collections.Generic;
using UnityEngine;

// De tank (J, dan 2): de koepel en het kanon draaien naar waar de camera naartoe kijkt (het vizier in het midden van het beeld), met
// voorhouden voor de zwaartekracht; een klik schiet een granaat (150 m/s, valt naar het middelpunt van de planeet). Bij de inslag
// een flits, vuurbal, rook en brokstukken; er verdwijnt niets (geen wegen, geen huizen: huizen beschadigen komt later), kaal
// terrein krijgt een kuiltje en het vak en zijn buren worden zwartgeblakerd (PlanetCity.Scorch). Mensen en auto's in de buurt vliegen weg
// (PlanetLife.Blast). Elk schot is één stap in de undo (Ctrl+Z na het uitstappen zet het terug).
public partial class PlanetBuilder
{
    class Shell { public Vector3 pos, vel; public float t; public Transform vis; }
    class Puff { public Transform tr; public Vector3 vel; public float t, life, size; public bool grav, fire; }
    readonly List<Shell> shells = new List<Shell>(); readonly List<Puff> puffs = new List<Puff>();
    float reload; Vector3 aimPoint; bool hasAim; static AudioClip shotClip, boomClip; static Mesh puffMesh;
    const float ShellSpeed = 150f, ReloadTime = 1.4f;
    public int TankShots, TankHits, TankClickShots; public Vector3 TankLastHit; public float TankAimErr;   // voor een test
    public static bool AutoFire;   // voor een test: schieten zonder klik
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetTankStatics() { shotClip = boomClip = null; puffMesh = null; AutoFire = false; }

    // elk beeld: richten (als je in de tank zit), granaten laten vliegen, rook en brokstukken bijwerken
    void TankTick()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        var L = PlanetLife.I;
        if (driving && L != null && L.PlayerTank && L.PlayerTankRig != null && Cam != null)
        {
            Aim(L.PlayerTankRig, dt);
            reload -= dt;
            bool click = Cursor.lockState == CursorLockMode.Locked && Inp.LeftDown && PlayerCar.Clicked != Time.frameCount && !helpOpen;
            if ((click || AutoFire) && reload <= 0f) { if (click) TankClickShots++; AutoFire = false; Fire(L.PlayerTankRig); }
        }
        for (int i = shells.Count - 1; i >= 0; i--) if (!FlyShell(shells[i], dt)) { if (shells[i].vis != null) Destroy(shells[i].vis.gameObject); shells.RemoveAt(i); }
        for (int i = puffs.Count - 1; i >= 0; i--)
        {
            var p = puffs[i]; p.t += dt;
            if (p.t >= p.life || p.tr == null) { if (p.tr != null) Destroy(p.tr.gameObject); puffs.RemoveAt(i); continue; }
            Vector3 up = p.tr.position.normalized;
            if (p.grav) p.vel -= up * 9.81f * dt; else p.vel *= Mathf.Exp(-dt * 1.6f);
            p.tr.position += p.vel * dt;
            float u = p.t / p.life, s = p.fire ? p.size * Mathf.Sin(Mathf.Min(1f, u * 1.6f) * Mathf.PI) : p.grav ? p.size : p.size * Mathf.Sqrt(Mathf.Min(1f, u * 3f)) * (u > 0.7f ? (1f - u) / 0.3f : 1f);
            p.tr.localScale = Vector3.one * Mathf.Max(0.001f, s);
            if (p.grav) p.tr.Rotate(p.vel * dt * 40f, Space.World);
        }
    }

    // de koepel naar het doel (om zijn eigen as, 70°/s) en het kanon omhoog of omlaag (40°/s, -25° tot +8°), met voorhouden
    void Aim(TankRig tr, float dt)
    {
        var c = Cam.transform; Vector3 o = c.position, f = c.forward;
        hasAim = Physics.Raycast(o, f, out RaycastHit hit, 700f, Layers.Wijzen | Layers.Muren, QueryTriggerInteraction.Ignore);
        aimPoint = hasAim ? hit.point : o + f * 500f;
        Vector3 hullUp = tr.turret.parent.up;
        Vector3 to = aimPoint - tr.turret.position; Vector3 flat = to - hullUp * Vector3.Dot(to, hullUp);
        float yawDes = Vector3.SignedAngle(tr.turret.parent.forward, flat, hullUp), yaw = tr.turret.localEulerAngles.y; if (yaw > 180f) yaw -= 360f;
        tr.turret.localRotation = Quaternion.Euler(0f, Mathf.MoveTowardsAngle(yaw, yawDes, 70f * dt), 0f);
        // voorhouden: de granaat zakt over de afstand d zo'n ½·g·t² (t = d / v)
        Vector3 g2 = aimPoint - tr.gun.position; float d = g2.magnitude, tt = d / ShellSpeed; Vector3 lift = tr.gun.position.normalized * (0.5f * 9.81f * tt * tt);
        Vector3 ld = tr.turret.InverseTransformDirection(g2 + lift); float pitchDes = Mathf.Clamp(-Mathf.Atan2(ld.y, new Vector2(ld.x, ld.z).magnitude) * Mathf.Rad2Deg, -25f, 8f);
        float pitch = tr.gun.localEulerAngles.x; if (pitch > 180f) pitch -= 360f;
        tr.gun.localRotation = Quaternion.Euler(Mathf.MoveTowards(pitch, pitchDes, 40f * dt), 0f, 0f);
    }

    void Fire(TankRig tr)
    {
        reload = ReloadTime; TankShots++;
        Vector3 at = tr.muzzle.position, dir = tr.gun.forward;
        TankAimErr = Vector3.Angle(Vector3.ProjectOnPlane(dir, at.normalized), Vector3.ProjectOnPlane(aimPoint - at, at.normalized));   // links-rechts: hoe ver de koepel nog van het vizier af staat
        var L = PlanetLife.I; Vector3 carV = L != null ? L.PlayerVelocity : Vector3.zero;
        var vis = MakeShellVis(); vis.position = at; vis.rotation = Quaternion.LookRotation(dir, at.normalized);
        shells.Add(new Shell { pos = at, vel = dir * ShellSpeed + carV, vis = vis });
        if (L != null) L.PlayerKick(-dir * 26000f * 2.2f, at);   // terugslag
        // mondingsvuur en een wolkje
        Burst(at + dir * 0.6f, dir, 0.5f, 3, true);
        PlayAt(ShotClip(), at, 0.9f);
    }

    bool FlyShell(Shell s, float dt)
    {
        s.t += dt; if (s.t > 7f) return false;
        Vector3 up = s.pos.normalized, nv = s.vel - up * 9.81f * dt, np = s.pos + (s.vel + nv) * 0.5f * dt;
        Vector3 seg = np - s.pos; float len = seg.magnitude;
        if (len > 1e-4f && Physics.Raycast(s.pos, seg / len, out RaycastHit hit, len, Layers.Wijzen | Layers.Muren, QueryTriggerInteraction.Ignore))
        { Explode(hit.point, hit.normal); return false; }
        // onder het terrein (een gat in de botsing): daar ontploffen
        float g = PlanetCity.TerrainAt(np.normalized); if (np.magnitude < Planet.R + g - 0.5f && !PlanetCity.InBore(np, out _)) { Explode(np.normalized * (Planet.R + g), np.normalized); return false; }
        s.pos = np; s.vel = nv;
        if (s.vis != null) { s.vis.position = np; s.vis.rotation = Quaternion.LookRotation(nv, up); }
        return true;
    }

    void Explode(Vector3 p, Vector3 n)
    {
        TankHits++; TankLastHit = p;
        Vector3 up = p.normalized;
        // effecten: flits, vuurbal, rook en brokstukken
        var lg = new GameObject("flits"); lg.transform.position = p + up * 1.5f; var li = lg.AddComponent<Light>(); li.type = LightType.Point; li.color = new Color(1f, 0.62f, 0.3f); li.range = 30f; li.intensity = 8f; Destroy(lg, 0.18f);
        Burst(p + up * 0.6f, up, 1.6f, 7, true);
        for (int k = 0; k < 9; k++) Smoke(p + up * 0.8f + Random.insideUnitSphere * 1.2f, up * Random.Range(1.5f, 4f) + Random.insideUnitSphere * 1.5f, Random.Range(1.6f, 2.8f), Random.Range(2.2f, 3.6f));
        for (int k = 0; k < 10; k++) Debris(p + up * 0.5f, (up * Random.Range(5f, 11f) + Random.insideUnitSphere * 6f));
        PlayAt(BoomClip(), p, 1f);
        if (Cam != null) { float dist = Vector3.Distance(Cam.transform.position, p); if (dist < 60f) Shake(Mathf.Lerp(0.5f, 0f, dist / 60f)); }
        PlanetLife.Blast(p, 3f);
        // schade: één stap in de undo
        int c = Planet.Nearest(p); if (c < 0) return;
        float above = p.magnitude - Planet.R - PlanetCity.G(c);
        if (above > 25f) return;   // hoog in de lucht (een flank van een berg in de verte): geen schade aan het vak eronder
        Push();
        // een tank haalt niets weg (geen wegen, geen huizen; huizen beschadigen komt later): alleen een kuiltje in kaal terrein,
        // en niet naast een weg, pad of gebouw (dan zou de rand ervan meezakken)
        bool bare = !PlanetCity.Cells.ContainsKey(c) && !PlanetCity.Wet(c);
        foreach (int j in Planet.Cells[c].nb) if (PlanetCity.Cells.ContainsKey(j)) bare = false;
        if (bare) PlanetCity.SetTerr(c, PlanetCity.G(c) - 0.6f);
        Blacken(c, 70); foreach (int j in Planet.Cells[c].nb) Blacken(j, 25);
        Changed(); Sounds.Hush();
    }
    static void Blacken(int c, byte v)
    {
        if (!PlanetCity.Scorch.TryGetValue(c, out byte old) || (old < v && old < PlanetCity.ScorchRim)) PlanetCity.Scorch[c] = v;
        PlanetCity.Touch(c);
    }

    // ---- effecten (eenvoudige blokjes en bollen, geen deeltjessysteem)
    Transform MakeShellVis()
    {
        var g = new GameObject("granaat"); var mf = g.AddComponent<MeshFilter>(); var mb = new MeshBuilder();
        Vector3 h = new Vector3(0.06f, 0.06f, 0.7f);
        Vector3 P(int i, int j, int k) => new Vector3(i * h.x, j * h.y, k * h.z);
        mb.Quad(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), Vector3.forward); mb.Quad(P(-1, -1, -1), P(1, -1, -1), P(1, 1, -1), P(-1, 1, -1), Vector3.back);
        mb.Quad(P(1, -1, -1), P(1, -1, 1), P(1, 1, 1), P(1, 1, -1), Vector3.right); mb.Quad(P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), Vector3.left);
        mb.Quad(P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1), P(-1, 1, 1), Vector3.up); mb.Quad(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), Vector3.down);
        mf.sharedMesh = mb.Build(); g.AddComponent<MeshRenderer>().sharedMaterial = Mats.GetUnlit("tank_gloed", new Color(0.2f, 0.92f, 1f));
        return g.transform;
    }
    static Mesh PuffMesh()
    {
        if (puffMesh != null) return puffMesh;
        // een icosaëder: low poly, past bij de stad
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        var v = new[] { new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0), new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t), new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1) };
        int[] f = { 0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8, 3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1 };
        var mb = new MeshBuilder(); for (int k = 0; k < f.Length; k += 3) { Vector3 a = v[f[k]].normalized * 0.5f, b = v[f[k + 1]].normalized * 0.5f, c = v[f[k + 2]].normalized * 0.5f; mb.Tri(a, b, c, a + b + c); }
        return puffMesh = mb.Build();
    }
    Transform Blob(string name, Material m)
    {
        var g = new GameObject(name); g.AddComponent<MeshFilter>().sharedMesh = PuffMesh(); g.AddComponent<MeshRenderer>().sharedMaterial = m;
        g.transform.rotation = Random.rotation; return g.transform;
    }
    void Burst(Vector3 at, Vector3 dir, float size, int n, bool fire)
    {
        for (int k = 0; k < n; k++)
        {
            var tr = Blob("vuur", Mats.GetUnlit(k % 2 == 0 ? "tank_vuur" : "tank_vuur2", k % 2 == 0 ? new Color(1f, 0.6f, 0.15f) : new Color(1f, 0.85f, 0.4f)));
            tr.position = at + Random.insideUnitSphere * size * 0.4f;
            puffs.Add(new Puff { tr = tr, vel = (dir * 3f + Random.insideUnitSphere * 2.5f) * size, life = Random.Range(0.25f, 0.45f), size = size * Random.Range(1.4f, 2.4f), fire = true });
        }
        Smoke(at, dir * 2f, size * 1.6f, 1.6f);
    }
    void Smoke(Vector3 at, Vector3 vel, float size, float life)
    {
        float grey = Random.Range(0.22f, 0.42f);
        var tr = Blob("rook", Mats.Get("tank_rook" + Mathf.RoundToInt(grey * 10f), new Color(grey, grey * 0.97f, grey * 0.94f)));
        tr.position = at; tr.localScale = Vector3.one * 0.01f;
        puffs.Add(new Puff { tr = tr, vel = vel, life = life, size = size });
    }
    void Debris(Vector3 at, Vector3 vel)
    {
        var tr = Blob("brok", Mats.Get("tank_brok", new Color(0.24f, 0.2f, 0.17f))); tr.position = at;
        puffs.Add(new Puff { tr = tr, vel = vel, life = Random.Range(1.2f, 2f), size = Random.Range(0.15f, 0.35f), grav = true });
    }
    float shakeT, shakeA;
    void Shake(float a) { shakeA = Mathf.Max(shakeA, a); shakeT = 0.4f; }
    // na de camera van de auto (LateUpdate): even schudden
    void LateUpdate()
    {
        if (shakeT <= 0f || Cam == null) return;
        shakeT -= Time.deltaTime; float k = Mathf.Clamp01(shakeT / 0.4f) * shakeA;
        Cam.transform.position += Random.insideUnitSphere * k * 0.35f;
    }

    // ---- geluid, zelf berekend: de knal van het kanon (scherp en laag) en de dreun van de inslag
    static AudioClip ShotClip() => shotClip != null ? shotClip : (shotClip = Bang("kanon", 1.1f, 0.012f, 0.35f, 90f));
    static AudioClip BoomClip() => boomClip != null ? boomClip : (boomClip = Bang("inslag", 1.8f, 0.03f, 0.6f, 55f));
    static AudioClip Bang(string name, float len, float attack, float decay, float tone)
    {
        int sr = 22050, n = (int)(sr * len); var d = new float[n]; var rnd = new System.Random(name.Length * 31 + 7); float lp = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr, env = t < attack ? t / attack : Mathf.Exp(-(t - attack) / decay);
            float x = (float)rnd.NextDouble() * 2f - 1f; lp += 0.25f * (x - lp); lp2 += 0.05f * (lp - lp2);
            float crack = t < 0.06f ? x * (1f - t / 0.06f) * 0.6f : 0f;   // de scherpe knal vooraan
            float thump = Mathf.Sin(2f * Mathf.PI * tone * t * (1f - t * 0.3f)) * Mathf.Exp(-t / (decay * 0.6f));
            d[i] = Mathf.Clamp((lp * 0.9f + lp2 * 2.2f + crack) * env + thump * 0.7f, -1f, 1f) * 0.85f;
        }
        var c = AudioClip.Create(name, n, 1, sr, false); c.SetData(d, 0); return c;
    }
    static void PlayAt(AudioClip clip, Vector3 at, float vol)
    {
        if (Ambience.Muted || clip == null) return;
        var g = new GameObject("knal"); g.transform.position = at; var a = g.AddComponent<AudioSource>(); a.clip = clip; a.volume = vol; a.spatialBlend = 0.85f;
        a.minDistance = 12f; a.maxDistance = 600f; a.rolloffMode = AudioRolloffMode.Logarithmic; a.dopplerLevel = 0f; a.Play(); Destroy(g, clip.length + 0.1f);
    }

    // het vizier in het midden van het beeld, een stipje waar het kanon nu op richt, en de herlaadbalk
    void TankGui(float W, float H)
    {
        var L = PlanetLife.I; if (L == null || !L.PlayerTank || L.PlayerTankRig == null || Cam == null) return;
        Vector2 c = new Vector2(W * 0.5f, H * 0.5f); Color cy = new Color(0.25f, 0.95f, 1f, 0.9f);
        void Px(float x, float y, float w, float h, Color col) { var o = GUI.color; GUI.color = col; GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture); GUI.color = o; }
        if (playerCar == null || !playerCar.Inside)
        {
            float r = 16f; for (int k = 0; k < 6; k++) { float a = (k * 60f + 30f) * Mathf.Deg2Rad; Px(c.x + Mathf.Cos(a) * r - 2f, c.y + Mathf.Sin(a) * r - 2f, 4f, 4f, cy); }
        }
        Px(c.x - 1f, c.y - 7f, 2f, 14f, cy); Px(c.x - 7f, c.y - 1f, 14f, 2f, cy);
        // waar het kanon nu op wijst (draait de koepel nog bij, dan staat het stipje ernaast)
        var tr = L.PlayerTankRig; float d = Vector3.Distance(tr.muzzle.position, aimPoint);
        Vector3 sp = Cam.WorldToScreenPoint(tr.muzzle.position + tr.gun.forward * d);
        if (sp.z > 0f) { float x = sp.x, y = H - sp.y; Px(x - 3f, y - 3f, 6f, 6f, new Color(1f, 0.55f, 0.2f, 0.95f)); }
        float k2 = Mathf.Clamp01(1f - reload / ReloadTime);
        Px(c.x - 30f, c.y + 26f, 60f, 4f, new Color(0f, 0f, 0f, 0.35f)); Px(c.x - 30f, c.y + 26f, 60f * k2, 4f, k2 >= 1f ? cy : new Color(1f, 0.55f, 0.2f, 0.9f));
    }
}
