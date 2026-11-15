using System.Collections.Generic;
using UnityEngine;

// De tank (J, dan 2): de koepel en het kanon draaien naar waar de camera naartoe kijkt (het vizier in het midden van het beeld), met
// voorhouden voor de zwaartekracht; een klik schiet een granaat (120 m/s, valt naar het middelpunt van de planeet; raakt hij na 3 s
// niets, dan ontploft hij in de lucht). Bij de inslag een flits, vuurbal, rook en brokstukken, en mensen en auto's in de buurt vliegen
// weg (PlanetLife.Blast). Er verdwijnt niets (geen wegen, geen huizen: huizen beschadigen komt later). Op de plek van de inslag een
// spoor (Soot): op terrein een kuiltje met een zeshoekige wal, op wegen, gebouwen en plantenbakken een roetvlek. De stad zelf
// verandert niet: geen undo-stap, niet opslaan, niets opnieuw tekenen (dat maakte elke inslag traag).
public partial class PlanetBuilder
{
    class Shell { public Vector3 pos, vel; public float t; public Transform vis, owner; }
    class Puff { public Transform tr; public Vector3 vel; public float t, life, size; public bool grav, fire; }
    readonly List<Shell> shells = new List<Shell>(); readonly List<Puff> puffs = new List<Puff>();
    float reload; Vector3 aimPoint; bool hasAim; static AudioClip shotClip, boomClip; static Mesh puffMesh;
    public const float ShellSpeed = 120f; const float ShellLife = 3f, ReloadTime = 1.4f;   // de planeet is klein (baansnelheid zo'n 77 m/s): een granaat die niets raakt ontploft na 3 s (zo'n 360 m) in de lucht
    public int TankShots, TankHits, TankClickShots; public Vector3 TankLastHit; public float TankAimErr; public string TankAimInfo = "";   // voor een test
    public static bool AutoFire;   // voor een test: schieten zonder klik
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetTankStatics() { shotClip = boomClip = null; puffMesh = null; AutoFire = false; }

    // elk beeld: richten (als je in de tank zit), granaten laten vliegen, rook en brokstukken bijwerken
    void TankTick()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        var L = PlanetLife.I;
        if (driving && L != null && L.PlayerTank && L.PlayerTankRig != null && Cam != null)
        {
            reload -= dt;
            if (BattleTick(L, dt)) { }   // tankgevecht: uitgeschakeld, of de AI bestuurt je tank (toeschouwer)
            else
            {
                Aim(L.PlayerTankRig, dt);
                bool click = Cursor.lockState == CursorLockMode.Locked && Inp.LeftDown && PlayerCar.Clicked != Time.frameCount && !helpOpen;
                if ((click || AutoFire) && reload <= 0f) { if (click) TankClickShots++; AutoFire = false; Fire(L.PlayerTankRig); }
            }
        }
        SootTick(dt);
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
        hasAim = Ray(o, f, 700f, PlanetLife.I != null ? PlanetLife.I.PlayerRoot : null, out RaycastHit hit);   // ook op een tank, niet op je eigen
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
        { float gp = tr.gun.localEulerAngles.x; if (gp > 180f) gp -= 360f; TankAimInfo = $"doel {Vector3.Distance(at, aimPoint):0} m ({(hasAim ? "raak" : "lucht")}), kanon {gp:0.0}°, loopmond {tr.muzzle.localPosition}, loop vooruit·op {Vector3.Dot(dir, at.normalized):0.00}"; }
        TankAimErr = Vector3.Angle(Vector3.ProjectOnPlane(dir, at.normalized), Vector3.ProjectOnPlane(aimPoint - at, at.normalized));   // links-rechts: hoe ver de koepel nog van het vizier af staat
        var L = PlanetLife.I;
        Shoot(tr, dir, L != null ? L.PlayerVelocity : Vector3.zero, L != null ? L.PlayerRoot : null);
        if (L != null) L.PlayerRecoil(dir);   // terugslag
    }
    // een granaat uit de loop van een tank (ook die van de tegenstander); owner: de tank zelf, daar vliegt hij doorheen
    void Shoot(TankRig tr, Vector3 dir, Vector3 carV, Transform owner)
    {
        Vector3 at = tr.muzzle.position;
        var vis = MakeShellVis(); vis.position = at; vis.rotation = Quaternion.LookRotation(dir, at.normalized);
        shells.Add(new Shell { pos = at, vel = dir * ShellSpeed + carV, vis = vis, owner = owner });
        if (battle) { if (owner != null && owner.GetComponent<EnemyTank>() != null) BattleStats[2]++; else BattleStats[0]++; }
        // mondingsvuur en een wolkje
        Burst(at + dir * 0.6f, dir, 0.5f, 3, true);
        PlayAt(ShotClip(), at, 0.9f);
    }
    // een straal die ook tanks raakt (laag Mens), maar niet de tank skip
    static readonly RaycastHit[] rayHits = new RaycastHit[16];
    static bool Ray(Vector3 o, Vector3 d, float len, Transform skip, out RaycastHit best)
    {
        int n = Physics.RaycastNonAlloc(o, d, rayHits, len, Layers.Wijzen | Layers.Muren | (1 << Layers.Mens), QueryTriggerInteraction.Ignore);
        best = default; float bd = float.MaxValue; bool any = false;
        for (int k = 0; k < n; k++)
        {
            var h = rayHits[k]; if (h.distance >= bd) continue;
            if (h.collider.gameObject.layer == Layers.Mens && (h.collider.attachedRigidbody == null || h.collider.GetComponentInParent<TankRig>() == null)) continue;   // mensen en dergelijke: erdoor
            if (skip != null && h.collider.transform.IsChildOf(skip)) continue;
            best = h; bd = h.distance; any = true;
        }
        return any;
    }

    bool FlyShell(Shell s, float dt)
    {
        s.t += dt; if (s.t > ShellLife) { AirBurst(s.pos); return false; }
        Vector3 up = s.pos.normalized, nv = s.vel - up * 9.81f * dt, np = s.pos + (s.vel + nv) * 0.5f * dt;
        Vector3 seg = np - s.pos; float len = seg.magnitude;
        if (len > 1e-4f && Ray(s.pos, seg / len, len, s.owner, out RaycastHit hit))
        { shellOwner = s.owner; Explode(hit.point, hit.normal, hit.collider.gameObject.layer, hit.collider); shellOwner = null; return false; }
        // onder het terrein (een gat in de botsing): daar ontploffen
        float g = PlanetCity.TerrainAt(np.normalized); if (np.magnitude < Planet.R + g - 0.5f && !PlanetCity.InBore(np, out _)) { Explode(np.normalized * (Planet.R + g), np.normalized); return false; }
        s.pos = np; s.vel = nv;
        if (s.vis != null) { s.vis.position = np; s.vis.rotation = Quaternion.LookRotation(nv, up); }
        return true;
    }

    // een ontploffing in de lucht: alleen het effect
    void AirBurst(Vector3 p)
    {
        Vector3 up = p.normalized; Burst(p, up, 1.1f, 5, true);
        for (int k = 0; k < 5; k++) Smoke(p + Random.insideUnitSphere * 1.0f, Random.insideUnitSphere * 2f, Random.Range(1.4f, 2.2f), Random.Range(1.8f, 2.8f));
        PlayAt(BoomClip(), p, 0.8f);
    }

    void Explode(Vector3 p, Vector3 n, int layer = Layers.Bodem, Collider hitCol = null)
    {
        TankHits++; TankLastHit = p;
        BattleDamage(p, hitCol);   // tankgevecht: schade aan tanks in de buurt
        Vector3 up = p.normalized;
        // effecten: flits, vuurbal, rook en brokstukken
        var lg = new GameObject("flits"); lg.transform.position = p + up * 1.5f; var li = lg.AddComponent<Light>(); li.type = LightType.Point; li.color = new Color(1f, 0.62f, 0.3f); li.range = 30f; li.intensity = 8f; Destroy(lg, 0.18f);
        Burst(p + up * 0.6f, up, 1.6f, 7, true);
        for (int k = 0; k < 9; k++) Smoke(p + up * 0.8f + Random.insideUnitSphere * 1.2f, up * Random.Range(1.5f, 4f) + Random.insideUnitSphere * 1.5f, Random.Range(1.6f, 2.8f), Random.Range(2.2f, 3.6f));
        for (int k = 0; k < 10; k++) Debris(p + up * 0.5f, (up * Random.Range(5f, 11f) + Random.insideUnitSphere * 6f));
        PlayAt(BoomClip(), p, 1f);
        if (Cam != null) { float dist = Vector3.Distance(Cam.transform.position, p); if (dist < 60f) Shake(Mathf.Lerp(0.5f, 0f, dist / 60f)); }
        PlanetLife.Blast(p, 3f);
        // sporen: alleen iets om te zien, de stad zelf verandert niet (dus geen undo-stap, niet opslaan, niets opnieuw tekenen: dat
        // maakte elke inslag traag). Op terrein een kuiltje met een zeshoekige wal van opgeworpen aarde en roet in het midden, op een
        // weg, gebouw of plantenbak alleen een roetvlek. Een tank haalt niets weg (huizen beschadigen komt later).
        float above = p.magnitude - Planet.R - PlanetCity.TerrainAt(up);
        if (above > 25f || layer == Layers.Mens) return;   // hoog in de lucht of op een tank: geen spoor
        bool terrain = layer != Layers.Weg && layer != Layers.Gebouw && layer != Layers.Water;
        Soot(p, n, layer == Layers.Gebouw ? 1.1f : layer == Layers.Weg ? 1.8f : 1.3f, terrain);
    }

    // ---- sporen van een inslag: een platte, rafelige roetvlek net boven het oppervlak, op terrein met een wal eromheen. Alleen om
    // te zien: niet in de undo, niet bewaard; hooguit 80 (de oudste gaan weg), en weg als er niets meer onder zit.
    class SootMark { public GameObject g; public int miss; public Vector3 at; }
    readonly List<SootMark> soot = new List<SootMark>(); Transform sootRoot; float sootCheckT;
    void Soot(Vector3 p, Vector3 n, float r, bool pit = false)
    {
        if (sootRoot == null) sootRoot = new GameObject("Roetvlekken").transform;
        Vector3 t = Vector3.Cross(n, Mathf.Abs(Vector3.Dot(n, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up).normalized, b = Vector3.Cross(n, t);
        var mb = new MeshBuilder(); Vector3 c = p + n * 0.025f; const int N = 14;
        var outer = new Vector3[N]; var inner = new Vector3[N];
        for (int k = 0; k < N; k++)
        {
            float a = k * Mathf.PI * 2f / N, ro = r * Random.Range(0.75f, 1.25f), ri = ro * Random.Range(0.45f, 0.65f);
            Vector3 d = t * Mathf.Cos(a) + b * Mathf.Sin(a); outer[k] = c + d * ro; inner[k] = c + d * ri + n * 0.004f;
        }
        var core = new MeshBuilder();
        for (int k = 0; k < N; k++) { int k2 = (k + 1) % N; core.Tri(c + n * 0.004f, inner[k], inner[k2], n); mb.Quad(inner[k], outer[k], outer[k2], inner[k2], n); }
        var g = new GameObject("roet"); g.transform.SetParent(sootRoot, true);
        var a1 = new GameObject("rand"); a1.transform.SetParent(g.transform, true); a1.AddComponent<MeshFilter>().sharedMesh = mb.Build(); a1.AddComponent<MeshRenderer>().sharedMaterial = Mats.Get("tank_roetgrijs", new Color(0.2f, 0.19f, 0.18f));
        var a2 = new GameObject("kern"); a2.transform.SetParent(g.transform, true); a2.AddComponent<MeshFilter>().sharedMesh = core.Build(); a2.AddComponent<MeshRenderer>().sharedMaterial = Mats.Get("tank_roet", new Color(0.07f, 0.065f, 0.06f));
        if (pit)   // de wal: zes lage, schuine stukken rond het kuiltje (zeshoek, zoals alles in de stad), net iets onregelmatig
        {
            var wal = new MeshBuilder(); float ri = r * 0.95f, ro = r * 1.9f;
            Vector3 P(int k, float rad, float h) { float a = (k * 60f + 30f) * Mathf.Deg2Rad; return p + (t * Mathf.Cos(a) + b * Mathf.Sin(a)) * rad + n * h; }
            var hs = new float[6]; for (int k = 0; k < 6; k++) hs[k] = Random.Range(0.28f, 0.45f);
            for (int k = 0; k < 6; k++)
            {
                int k2 = (k + 1) % 6; float h0 = hs[k], h1 = hs[k2];
                Vector3 ib0 = P(k, ri, -0.05f), ib1 = P(k2, ri, -0.05f), it0 = P(k, ri + 0.25f, h0), it1 = P(k2, ri + 0.25f, h1), ot0 = P(k, ro - 0.45f, h0 * 0.8f), ot1 = P(k2, ro - 0.45f, h1 * 0.8f), ob0 = P(k, ro, -0.1f), ob1 = P(k2, ro, -0.1f);
                Vector3 mid = (P(k, ri, 0f) + P(k2, ri, 0f)) * 0.5f - p;
                wal.Quad(ib0, it0, it1, ib1, -mid);            // de binnenkant, naar het kuiltje toe
                wal.Quad(it0, ot0, ot1, it1, n);               // de top
                wal.Quad(ot0, ob0, ob1, ot1, mid);             // de buitenkant
            }
            var a3 = new GameObject("wal"); a3.transform.SetParent(g.transform, true); a3.AddComponent<MeshFilter>().sharedMesh = wal.Build(); a3.AddComponent<MeshRenderer>().sharedMaterial = Mats.Get("tank_aarde", new Color(0.36f, 0.31f, 0.25f));
        }
        soot.Add(new SootMark { g = g, at = p });
        if (soot.Count > 80) { if (soot[0].g != null) Destroy(soot[0].g); soot.RemoveAt(0); }   // de oudste sporen verdwijnen
    }
    void SootTick(float dt)
    {
        for (int i = soot.Count - 1; i >= 0; i--) if (soot[i].g == null) soot.RemoveAt(i);
        sootCheckT -= dt; if (sootCheckT > 0f) return; sootCheckT = 1f;
        for (int i = soot.Count - 1; i >= 0; i--)   // er zit niets meer onder (twee keer achter elkaar: tijdens het opnieuw tekenen ontbreekt een botsvorm even)
        {
            if (Physics.CheckSphere(soot[i].at, 0.35f, Layers.Grond, QueryTriggerInteraction.Ignore)) { soot[i].miss = 0; continue; }
            if (++soot[i].miss >= 3) { Destroy(soot[i].g); soot.RemoveAt(i); }
        }
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
        BattleGui(W, H); if (spectate || PlayerCar.Frozen) return;   // toeschouwer of uitgeschakeld: geen vizier
        Vector2 c = new Vector2(W * 0.5f, H * 0.5f); Color cy = new Color(0.25f, 0.95f, 1f, 0.9f);
        // het vizier: een zeshoek (zoals alles in de stad), met een klein zeshoekje in het midden
        var clear = new Color(0f, 0f, 0f, 0f);
        float hr = playerCar != null && playerCar.Inside ? 14f : 18f;
        Ui.Tex(new Rect(c.x - hr, c.y - hr * 1.1f, hr * 2f, hr * 2.2f), Ui.Hex(clear, cy, 3.5f, true));
        Ui.Tex(new Rect(c.x - 3f, c.y - 3.3f, 6f, 6.6f), Ui.Hex(cy, cy, 1f, true));
        Color or = new Color(1f, 0.55f, 0.2f, 0.95f);
        // herladen: zes zeshoekjes onder het vizier die een voor een oplichten
        float k2 = Mathf.Clamp01(1f - reload / ReloadTime); int lit = Mathf.FloorToInt(k2 * 6f + 1e-4f);
        for (int k = 0; k < 6; k++)
        {
            bool on = k < lit; Color col = k2 >= 1f ? cy : on ? or : new Color(1f, 1f, 1f, 0.12f);
            Ui.Tex(new Rect(c.x - 6f * 9f * 0.5f + k * 9f, c.y + hr * 1.1f + 8f, 7.5f, 8.25f), Ui.Hex(col, col, 1f, true));
        }
    }
}
