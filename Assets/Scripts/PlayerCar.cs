using UnityEngine;

// Zelf rijden (J): de speler stapt in een auto (de Cybertruck uit Resources/Autos, anders een low-poly auto) op de weg die het
// dichtst bij de muis of de camera ligt. Vrije fysica zoals in GTA: een Rigidbody met zwaartekracht naar het middelpunt van de bol,
// een botsvorm (twee dozen) die tegen alles botst wat een MeshCollider heeft (terrein, wegen, bruggen, gebouwen, bomen, palen), en
// per wiel een vering (een straal omlaag langs de auto, veer en demper) met banden die grip hebben naar de last op het wiel
// (wrijvingscirkel). Zo rolt hij terug op een helling, glijdt hij van een te steile flank, vliegt hij van een schans en kan hij
// over de kop slaan (R of vanzelf na een paar seconden: weer rechtop). In een tunnel is de vloer van de koker (InBore) het vangnet
// voor de vering. Hij staat tijdens de autostap in de lijst van het verkeer als losse auto, zodat botsen met het verkeer (Collide)
// en de andere bestuurders (stoppen, eromheen) vanzelf werken; wat Collide aan snelheid en plek verandert gaat naar de Rigidbody.
public partial class PlanetLife
{
    Car player; Rigidbody prb; float playerSlip, pThrottle, pSteer, pSteerDeg, pFlipT; bool pHand, playerInList; Vector3 pInPos, pInVel, pLastVel;
    readonly float[] pLen = new float[4], pShow = new float[4]; readonly bool[] pOn = new bool[4];
    public Transform PlayerRoot => player != null ? player.root : null;
    public float PlayerSpeed => prb != null ? Vector3.Dot(prb.linearVelocity, player.root.forward) : 0f;   // m/s, negatief achteruit
    public float PlayerSlip => playerSlip;   // dwars glijden (m/s)
    public bool PlayerAir => player != null && player.air;
    public float PlayerSteer => pSteerDeg;
    public string PlayerWall = "";   // waar de auto het laatst hard tegenaan botste (voor een test)
    const float PMass = 2900f, PMount = 1.0f, PRest = 0.78f, PSpring = 30000f, PDampIn = 2600f, PDampOut = 4200f, PAntiRoll = 9000f, PMu = 1.15f;   // zacht: ~1 Hz, 24 cm inveren bij stilstand, ruim 60 cm slag
    static readonly Vector3[] PWheels = { new Vector3(-0.84f, 0f, 1.95f), new Vector3(0.84f, 0f, 1.95f), new Vector3(-0.84f, 0f, -1.73f), new Vector3(0.84f, 0f, -1.73f) };
    float pWheelR = 0.36f;
    static PhysicsMaterial pMat;

    // het verkeer ziet de speler alleen tijdens zijn eigen stap (anders ruimen het aanvullen en wegsturen hem op); Collide werkt op
    // vel en de plek van root, het verschil gaat daarna naar de Rigidbody
    void PlayerIn()
    {
        if (player == null || player.root == null || prb == null || playerInList) return;
        Vector3 p = player.root.position, up = p.normalized;
        player.fwd = Flat(player.root.forward, up).normalized; if (player.fwd.sqrMagnitude < 0.5f) player.fwd = Flat(player.root.up, up).normalized;
        player.vel = Flat(prb.linearVelocity, up); player.v = Vector3.Dot(player.vel, player.fwd);
        player.hp = 1e9f; player.burnT = -1f; player.dead = false; player.free = true; player.b = player.a = player.c = player.n = Planet.Nearest(p);
        pInPos = p; pInVel = player.vel;
        HitPeople(player);
        cars.Add(player); playerInList = true;
    }
    void PlayerOut()
    {
        if (!playerInList) return; cars.Remove(player); playerInList = false;
        if (player.root == null || prb == null) return;
        Vector3 dv = player.vel - pInVel; if (dv.sqrMagnitude > 1e-6f) prb.linearVelocity += dv;
        Vector3 dp = player.root.position - pInPos; if (dp.sqrMagnitude > 1e-8f) { player.root.position = pInPos; prb.position += dp; }
    }

    public Transform AddPlayer(Vector3 pos, Vector3 fwd)
    {
        RemovePlayer();
        var c = new Car { free = true, persona = 0, b = Planet.Nearest(pos), hp = 1e9f };
        c.a = c.c = c.n = c.b;
        var root = new GameObject("Eigen auto").transform; root.SetParent(transform, false); root.gameObject.layer = Layers.Mens; c.root = root;
        if (ModelCar.Build(root, "cybertruck")) { c.type = "cybertruck"; pWheelR = ModelCar.WheelRadius("cybertruck"); }
        else { c.type = LowPolyCar.Build(root); pWheelR = 0.36f; }
        c.wheels = root.GetComponent<CarWheels>();
        Vector3 up = pos.normalized; c.fwd = Flat(fwd, up).normalized; c.vel = Vector3.zero;
        // de grond net onder de plek (niet van hoog: in een tunnel zou hij dan op de berg komen)
        float alt = pos.magnitude - Planet.R;
        if (UnityEngine.Physics.Raycast(up * (Planet.R + alt + 2f), -up, out RaycastHit hit, 12f, Layers.Grond, QueryTriggerInteraction.Ignore)) alt = hit.point.magnitude - Planet.R;
        root.position = up * (Planet.R + alt + 0.25f); root.rotation = Geo.Look(c.fwd, up);
        Shape(root);
        prb = root.gameObject.AddComponent<Rigidbody>();
        prb.mass = PMass; prb.useGravity = false; prb.interpolation = RigidbodyInterpolation.Interpolate;
        prb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; prb.linearDamping = 0f; prb.angularDamping = 0.4f; prb.maxAngularVelocity = 14f;
        prb.centerOfMass = new Vector3(0f, 0.62f, 0.1f);
        for (int k = 0; k < 4; k++) { pLen[k] = PRest; pOn[k] = false; }
        pSteerDeg = 0f; pFlipT = 0f; pLastVel = Vector3.zero; playerSlip = 0f;
        player = c; return root;
    }
    public string LastRemove = "";   // voor een test: wie de auto het laatst weghaalde
    public bool PlayerLost => player != null && player.root == null;   // het object is weg, de speler niet
    public void RemovePlayer()
    {
        if (player == null) return; LastRemove = System.Environment.StackTrace; PlayerOut();
        player.dead = true;   // andere auto's die hem nog onthouden (voorligger, vastgezeten achter) zien hem zo als weg
        if (player.root != null) Destroy(player.root.gameObject); player = null; prb = null;
    }

    // de botsvorm: een lage doos over de hele lengte (boven de wielen, de vering doet de grond) en de cabine erboven
    static void Shape(Transform root)
    {
        var b = new Bounds(Vector3.zero, Vector3.zero); bool any = false;
        foreach (var r in root.GetComponentsInChildren<Renderer>())
        {
            var w = r.bounds;
            for (int k = 0; k < 8; k++)
            {
                Vector3 q = root.InverseTransformPoint(new Vector3((k & 1) == 0 ? w.min.x : w.max.x, (k & 2) == 0 ? w.min.y : w.max.y, (k & 4) == 0 ? w.min.z : w.max.z));
                if (!any) { b = new Bounds(q, Vector3.zero); any = true; } else b.Encapsulate(q);
            }
        }
        if (!any) b = new Bounds(new Vector3(0f, 0.9f, 0f), new Vector3(2f, 1.8f, 5f));
        // de wereldbegrenzing van een gedraaide mesh is ruim: iets kleiner nemen
        float wdt = Mathf.Min(b.size.x * 0.92f, 2.1f), len = b.size.z * 0.94f, top = Mathf.Min(b.max.y, 2.2f) - 0.05f, low = 0.5f, mid = Mathf.Lerp(low, top, 0.5f);
        if (pMat == null) pMat = new PhysicsMaterial("auto") { dynamicFriction = 0.25f, staticFriction = 0.3f, bounciness = 0.05f, frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum };
        // tussen de wielen laag (bodemvrijheid), voor en achter de wielen hoger: anders stoot de overhang op een stoeprand of randje
        // voordat het wiel er met de vering overheen kan
        float zf = PWheels[0].z + 0.35f, zr = PWheels[2].z - 0.35f, over = 0.9f;
        var a = root.gameObject.AddComponent<BoxCollider>(); a.center = new Vector3(b.center.x, (low + mid) * 0.5f, (zf + zr) * 0.5f); a.size = new Vector3(wdt, mid - low, zf - zr); a.sharedMaterial = pMat;
        var ov = root.gameObject.AddComponent<BoxCollider>(); ov.center = new Vector3(b.center.x, (over + mid) * 0.5f, b.center.z); ov.size = new Vector3(wdt, Mathf.Max(0.1f, mid - over), len); ov.sharedMaterial = pMat;
        var cb = root.gameObject.AddComponent<BoxCollider>(); cb.center = new Vector3(b.center.x, (mid + top) * 0.5f, b.center.z - len * 0.05f); cb.size = new Vector3(wdt * 0.9f, top - mid, len * 0.55f); cb.sharedMaterial = pMat;
    }

    public void SetPlayerInput(float throttle, float steer, bool hand) { pThrottle = throttle; pSteer = steer; pHand = hand; }

    // weer rechtop, iets boven de plek
    public void PlayerFlip()
    {
        if (prb == null) return;
        Vector3 p = prb.position, up = p.normalized, f = Flat(player.root.forward, up); if (f.sqrMagnitude < 1e-4f) f = Flat(player.root.up, up); if (f.sqrMagnitude < 1e-4f) f = Flat(Vector3.forward, up);
        prb.position = p + up * 1.5f; prb.rotation = Geo.Look(f.normalized, up); prb.linearVelocity = Vector3.zero; prb.angularVelocity = Vector3.zero;
        pFlipT = 0f;
    }

    // de grond onder een wiel: een straal langs de auto omlaag; in een tunnel de vloer van de koker als die niets raakt
    bool WheelHit(Vector3 mount, Vector3 down, float len, out Vector3 point, out Vector3 normal, out float dist)
    {
        if (UnityEngine.Physics.Raycast(mount, down, out RaycastHit hit, len, Layers.Grond, QueryTriggerInteraction.Ignore))
        { point = hit.point; normal = hit.normal; dist = hit.distance; return true; }
        point = normal = Vector3.zero; dist = 0f;
        if (!PlanetCity.InBore(mount, out float fl)) return false;
        Vector3 up = mount.normalized; float cosA = Vector3.Dot(-down, up); if (cosA < 0.3f) return false;
        dist = (mount.magnitude - (Planet.R + fl)) / cosA; if (dist < 0f || dist > len) return false;
        point = mount + down * dist; normal = up; return true;
    }

    // één natuurkundestap (FixedUpdate)
    public void PhysPlayer(float dt)
    {
        var c = player; if (c == null || c.root == null || prb == null || dt <= 0f) return;
        Vector3 pos = prb.position, up = pos.normalized; Quaternion rot = prb.rotation;
        Vector3 cu = rot * Vector3.up, cf = rot * Vector3.forward;
        prb.AddForce(-up * 9.81f, ForceMode.Acceleration);   // zwaartekracht: naar het middelpunt van de planeet
        Vector3 vel = prb.linearVelocity; float vf = Vector3.Dot(vel, cf), sp = vel.magnitude;
        prb.AddForce(-vel * sp * 0.00045f, ForceMode.Acceleration);   // luchtweerstand

        // sturen: minder uitslag bij hoge snelheid
        float maxDeg = Mathf.Lerp(34f, 8f, Mathf.Abs(vf) / 40f);
        pSteerDeg = Mathf.MoveTowards(pSteerDeg, pSteer * maxDeg, 160f * dt);
        // gas, remmen, achteruit: elektrisch, veel trekkracht onderin, tot zo'n 180 km/u; achteruit tot 43 km/u
        float accel = 0f; bool brake = false;
        if (pThrottle > 0.05f) { if (vf < -0.8f) brake = true; else accel = pThrottle * 9f * Mathf.Clamp01(1f - vf / 50f); }
        else if (pThrottle < -0.05f) { if (vf > 0.8f) brake = true; else accel = pThrottle * 6f * Mathf.Clamp01(1f + vf / 12f); }
        bool idle = !brake && Mathf.Abs(pThrottle) <= 0.05f;

        var pt = new Vector3[4]; var nm = new Vector3[4]; var load = new float[4]; var comp = new float[4]; var mounts = new Vector3[4]; int on = 0;
        float maxLen = PRest + pWheelR;
        for (int k = 0; k < 4; k++)
        {
            mounts[k] = pos + rot * new Vector3(PWheels[k].x, PMount, PWheels[k].z);
            pOn[k] = WheelHit(mounts[k], -cu, maxLen, out pt[k], out nm[k], out float d);
            if (!pOn[k]) { pLen[k] = PRest; continue; }
            float len = Mathf.Max(0f, d - pWheelR); comp[k] = PRest - len;
            float sv = (pLen[k] - len) / dt; float f = PSpring * comp[k] + (sv > 0f ? PDampIn : PDampOut) * sv;   // veer en demper (uitveren trager dan inveren)
            if (len < 0.14f) f += (0.14f - len) * PSpring * 12f;   // aanslag
            load[k] = Mathf.Max(0f, f); pLen[k] = len; on++;
        }
        // stabilisatorstang: links en rechts samen
        for (int k = 0; k < 4; k += 2)
        {
            float ar = (comp[k] - comp[k + 1]) * PAntiRoll;
            if (pOn[k]) load[k] = Mathf.Max(0f, load[k] + ar); if (pOn[k + 1]) load[k + 1] = Mathf.Max(0f, load[k + 1] - ar);
        }
        c.air = on == 0;
        float m4 = PMass / 4f, slip = 0f;
        for (int k = 0; k < 4; k++)
        {
            if (!pOn[k]) continue;
            prb.AddForceAtPosition(cu * load[k], mounts[k]);
            Vector3 n = nm[k];
            Vector3 wf = k < 2 ? Quaternion.AngleAxis(pSteerDeg, cu) * cf : cf; wf -= n * Vector3.Dot(wf, n); if (wf.sqrMagnitude < 1e-6f) continue; wf.Normalize();
            Vector3 wr = Vector3.Cross(n, wf);
            Vector3 pv = prb.GetPointVelocity(pt[k]); float vl = Vector3.Dot(pv, wf), vs = Vector3.Dot(pv, wr);
            bool rear = k >= 2, hand = pHand && rear;
            float grip = PMu * load[k] * (hand ? 0.4f : 1f);
            float fLat = -vs * m4 / 0.09f;   // de band duwt het dwars glijden weg
            float fLong;
            if (brake || hand) fLong = -Mathf.Sign(vl) * Mathf.Min(Mathf.Abs(vl) * m4 / 0.05f, m4 * (hand && !brake ? 9f : 13f));
            else if (accel != 0f) fLong = m4 * accel;
            else if (idle && sp < 1.2f) fLong = -vl * m4 / 0.08f;   // stilstaan: hij blijft staan (parkeerrem), ook op een helling zolang de band het houdt
            else fLong = idle ? -Mathf.Sign(vl) * m4 * 0.5f : 0f;   // uitrollen
            // wrijvingscirkel: samen nooit meer dan de grip
            float tot = Mathf.Sqrt(fLat * fLat + fLong * fLong); if (tot > grip && tot > 0f) { float s = grip / tot; fLat *= s; fLong *= s; }
            prb.AddForceAtPosition(wf * fLong + wr * fLat, pt[k] + cu * 0.25f);   // iets boven het wegdek: hij helt in de bocht, maar slaat niet zomaar om
            if (rear) slip = Mathf.Max(slip, Mathf.Abs(vs));
        }
        playerSlip = slip;
        // in de lucht een beetje bijsturen (zoals in GTA): W/S kantelt de neus, A/D rolt
        if (on == 0) prb.AddRelativeTorque(new Vector3(pThrottle * 1.6f, 0f, -pSteer * 1.6f), ForceMode.Acceleration);

        // klappen: een plotselinge verandering van snelheid die niet van de banden komt
        Vector3 dv = prb.linearVelocity - pLastVel; float jolt = dv.magnitude;
        if (jolt > 3.5f) { Sounds.Fx("crunch", pos, Mathf.Clamp01(jolt / 14f), 12f); PlayerWall = $"klap {jolt:0.0} m/s"; }
        pLastVel = prb.linearVelocity;
        if (slip > 4f && sp > 5f && on > 0) Sounds.Fx("squeal", pos, Mathf.Clamp01(slip / 10f), 12f);

        // over de kop of op zijn kant en (bijna) stil: na 3 s vanzelf weer rechtop
        if (Vector3.Dot(cu, up) < 0.35f && sp < 2f) { pFlipT += dt; if (pFlipT > 3f) PlayerFlip(); } else pFlipT = 0f;
        // vangnet: door de grond gezakt (buiten een tunnel): terug op het terrein
        float alt = pos.magnitude - Planet.R;
        if (!PlanetCity.InBore(pos, out _) && alt < PlanetCity.TerrainAt(up) - 4f && alt < PlanetCity.FloorH(Planet.Nearest(pos)) - 4f)
        {
            if (UnityEngine.Physics.Raycast(up * (Planet.R + PlanetCity.TerrainAt(up) + 30f), -up, out RaycastHit h, 60f, Layers.Grond, QueryTriggerInteraction.Ignore))
            { prb.position = h.point + up * 0.8f; prb.linearVelocity = Flat(prb.linearVelocity, up); PlayerWall = "door de grond gezakt, teruggezet"; FellThrough++; }
        }
    }
    public int FellThrough;

    // de wielen draaien met de snelheid mee en sturen mee (per beeld)
    public void SpinPlayerWheels(float dt)
    {
        if (player == null || player.wheels == null) return;
        player.wheels.Drive(PlayerSpeed * dt * 0.36f / Mathf.Max(0.2f, pWheelR), pSteerDeg, dt);
        // de wielen veren zichtbaar mee: het hart van het wiel ligt de veerlengte onder het ophangpunt
        for (int k = 0; k < 4; k++)
        {
            float want = Mathf.Clamp(PMount - pLen[k] - player.wheels.BaseY(k), -0.3f, ModelCar.ArchUp - 0.03f);   // niet door de wielkast
            pShow[k] = Mathf.Lerp(pShow[k], want, 1f - Mathf.Exp(-dt * 30f)); player.wheels.Travel(k, pShow[k]);
        }
    }
}

// De bestuurder: toetsen, de camera (achter en boven de auto, of vanuit de bestuurder) en het motorgeluid
public class PlayerCar : MonoBehaviour
{
    Camera cam; Transform car; bool inside, wheelsShown = true; float dist = 9f, yawOff, pitchOff, lookT = -99f; Vector3 camFwd; bool hasCam;
    AudioSource motor, road; static AudioClip motorClip, roadClip;
    public bool Inside { get => inside; set => inside = value; }
    public static bool Auto; public static float AutoThrottle, AutoSteer; public static bool AutoHand;   // voor een test: rijden zonder toetsen
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { Auto = AutoHand = false; AutoThrottle = AutoSteer = 0f; motorClip = roadClip = null; ModelCar.ResetStatics(); }   // Play zonder herladen van de scripts
    public float Kmh => PlanetLife.I != null ? Mathf.Abs(PlanetLife.I.PlayerSpeed) * 3.6f : 0f;
    public bool Reverse => PlanetLife.I != null && PlanetLife.I.PlayerSpeed < -0.5f;
    public float Throttle { get; private set; }

    public void Enter(Transform car, Camera cam)
    {
        this.car = car; this.cam = cam; enabled = true; wheelsShown = true; hasCam = false; yawOff = pitchOff = 0f; Throttle = 0f;
        cam.transform.SetParent(null); cam.nearClipPlane = 0.1f; cam.fieldOfView = 65f;
        if (motorClip == null) MakeClips();
        if (motor == null) { motor = gameObject.AddComponent<AudioSource>(); motor.clip = motorClip; motor.loop = true; motor.spatialBlend = 0f; motor.playOnAwake = false; }
        if (road == null) { road = gameObject.AddComponent<AudioSource>(); road.clip = roadClip; road.loop = true; road.spatialBlend = 0f; road.playOnAwake = false; }
        motor.volume = road.volume = 0f; motor.Play(); road.Play();
        Lock(true);
    }
    public void Exit()
    {
        if (motor != null) motor.Stop(); if (road != null) road.Stop();
        if (cam != null) { cam.nearClipPlane = 0.3f; cam.fieldOfView = 60f; }
        car = null; enabled = false; Lock(false);
    }
    static void Lock(bool on) { Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !on; }

    void Update()
    {
        var L = PlanetLife.I; if (car == null || L == null) return;
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        float th = (Inp.Held(K.W) || Inp.Held(K.Up) ? 1f : 0f) - (Inp.Held(K.S) || Inp.Held(K.Down) ? 1f : 0f);
        float st = (Inp.Held(K.D) || Inp.Held(K.Right) ? 1f : 0f) - (Inp.Held(K.A) || Inp.Held(K.Left) ? 1f : 0f);
        Throttle = Mathf.MoveTowards(Throttle, Mathf.Max(0f, th), dt * 4f);
        if (Auto) { th = AutoThrottle; st = AutoSteer; Throttle = Mathf.MoveTowards(Throttle, Mathf.Max(0f, th), dt * 4f); }
        L.SetPlayerInput(th, st, Auto ? AutoHand : Inp.Held(K.Space));
        L.SpinPlayerWheels(dt);
        if (Inp.Down(K.R)) L.PlayerFlip();   // weer rechtop
        if (Inp.Down(K.C)) inside = !inside;
        float sc = Inp.Scroll;   // het wiel: dichterbij, en helemaal dichtbij de bestuurder
        if (sc > 0f) { if (!inside && dist <= 4.6f) inside = true; else if (!inside) dist = Mathf.Max(4.5f, dist * 0.88f); }
        else if (sc < 0f) { if (inside) { inside = false; dist = 4.6f; } else dist = Mathf.Min(22f, dist / 0.88f); }
        if (Inp.LeftDown && Cursor.lockState != CursorLockMode.Locked && !Ui.MouseOverUI) Lock(true);
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 d = Inp.Delta;
            if (d.sqrMagnitude > 0.5f) { yawOff = Mathf.Repeat(yawOff + d.x * 0.15f + 180f, 360f) - 180f; pitchOff = Mathf.Clamp(pitchOff - d.y * 0.1f, -25f, 45f); lookT = Time.time; }
        }
        // even niet met de muis gekeken en hij rijdt: de camera komt vanzelf weer achter de auto
        if (Time.time - lookT > 1.5f && Mathf.Abs(L.PlayerSpeed) > 2f) { float k = 1f - Mathf.Exp(-dt * 1.5f); yawOff = Mathf.Lerp(yawOff, 0f, k); pitchOff = Mathf.Lerp(pitchOff, 0f, k); }
        Sound(L, dt);
    }

    void FixedUpdate() { var L = PlanetLife.I; if (car != null && L != null) L.PhysPlayer(Time.fixedDeltaTime); }

    void LateUpdate()
    {
        if (car == null || cam == null) return;
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        Vector3 p = car.position, up = p.normalized, cf = car.forward - up * Vector3.Dot(car.forward, up);
        if (cf.sqrMagnitude < 1e-6f) cf = camFwd; cf.Normalize();
        if (!hasCam) { camFwd = cf; hasCam = true; }
        if (wheelsShown == inside) { wheelsShown = !inside; foreach (Transform ch in car) if (ch.name == "as") ch.gameObject.SetActive(!inside); }   // als bestuurder: geen wielen voor je neus (de auto heeft geen interieur)
        if (inside)
        {
            camFwd = cf;
            cam.nearClipPlane = 0.05f; cam.fieldOfView = 72f;
            cam.transform.position = car.TransformPoint(new Vector3(-0.35f, 1.78f, 1.1f));   // net boven de voorruit, aan de kant van de bestuurder: het model heeft geen interieur
            cam.transform.rotation = car.rotation * Quaternion.Euler(4f + pitchOff * 0.6f, yawOff, 0f);
            return;
        }
        cam.nearClipPlane = 0.1f; cam.fieldOfView = 65f;
        // de kijkrichting draait soepel mee met de auto (de plek zelf niet: anders zakt hij bij hoge snelheid ver achter)
        camFwd = camFwd - up * Vector3.Dot(camFwd, up); if (camFwd.sqrMagnitude < 1e-6f) camFwd = cf; camFwd.Normalize();
        camFwd = Vector3.Slerp(camFwd, cf, 1f - Mathf.Exp(-dt * 3.5f));
        Vector3 dir = Quaternion.AngleAxis(yawOff, up) * camFwd;
        float el = (14f + pitchOff) * Mathf.Deg2Rad;
        Vector3 look = p + up * 1.5f, pos = look - dir * (dist * Mathf.Cos(el)) + up * (dist * Mathf.Sin(el));
        // niet door een muur of de grond: dan dichterbij
        Vector3 to = pos - look; float len = to.magnitude;
        if (UnityEngine.Physics.SphereCast(look, 0.3f, to / len, out RaycastHit hit, len, Layers.Muren, QueryTriggerInteraction.Ignore)) pos = look + to / len * Mathf.Max(1.2f, hit.distance - 0.1f);
        float g = PlanetCity.TerrainAt(pos.normalized) + 0.6f; if (!PlanetCity.InBore(pos, out _) && pos.magnitude < Planet.R + g) pos = pos.normalized * (Planet.R + g);
        cam.transform.position = pos; cam.transform.rotation = Geo.Look((look - pos).normalized, up);
    }

    // ---- geluid: een elektromotor (gezoem en een fluittoon die met het toerental omhooggaan) en bandengeruis bij snelheid
    void Sound(PlanetLife L, float dt)
    {
        if (motor == null) return;
        float v = Mathf.Abs(L.PlayerSpeed), rpm = v / 45f;   // één versnelling: het toerental volgt de snelheid
        motor.pitch = Mathf.Lerp(motor.pitch, 0.45f + rpm * 2.3f + Throttle * 0.08f, Mathf.Min(1f, dt * 8f));
        float mv = Ambience.Muted ? 0f : 0.10f + 0.22f * Throttle + 0.18f * rpm;
        motor.volume = Mathf.Lerp(motor.volume, mv, Mathf.Min(1f, dt * 6f));
        road.pitch = 0.7f + rpm * 0.8f;
        road.volume = Ambience.Muted || L.PlayerAir ? 0f : Mathf.Clamp01(v / 30f) * 0.35f + Mathf.Clamp01(L.PlayerSlip / 8f) * 0.25f;
    }

    static void MakeClips()
    {
        int sr = 22050, n = sr;   // 1 s, alle tonen een heel aantal trillingen: naadloos te herhalen
        var d = new float[n]; var rnd = new System.Random(7); float lp = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr, w = 2f * Mathf.PI * t;
            float hum = Mathf.Sin(w * 90f) * 0.45f + Mathf.Sin(w * 180f) * 0.25f + Mathf.Sin(w * 270f) * 0.12f;   // het gezoem van de motor
            float whine = Mathf.Sin(w * 720f) * 0.16f + Mathf.Sin(w * 1080f) * 0.07f + Mathf.Sin(w * 1530f) * 0.04f;   // de fluittoon van de omvormer
            float x = (float)rnd.NextDouble() * 2f - 1f; lp += 0.08f * (x - lp);
            d[i] = (hum + whine) * (0.9f + 0.1f * Mathf.Sin(w * 6f)) * 0.7f + lp * 0.25f;
        }
        motorClip = AudioClip.Create("elektromotor", n, 1, sr, false); motorClip.SetData(d, 0);
        var e = new float[n]; lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float x = (float)rnd.NextDouble() * 2f - 1f; lp += 0.05f * (x - lp); lp2 += 0.3f * (lp - lp2);   // bruin geruis: banden op asfalt
            float fade = Mathf.Min(1f, Mathf.Min(i, n - 1 - i) / 400f);   // zachte naad
            e[i] = lp2 * 2.2f * fade + lp2 * 2.2f * (1f - fade) * 0.5f;
        }
        roadClip = AudioClip.Create("banden", n, 1, sr, false); roadClip.SetData(e, 0);
    }
}
