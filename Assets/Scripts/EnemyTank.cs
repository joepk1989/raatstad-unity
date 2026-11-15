using UnityEngine;

// Tankgevecht (PlanetBuilder.Battle.cs). EnemyTank: een tank van de tegenstander, met hetzelfde model (ModelTank, anders TankModel),
// dezelfde botsvorm en dezelfde rupsfysica als de eigen tank (het tankdeel van PlanetLife.PhysPlayer: vering per loopwiel, de rupsen
// houden hem dwars tegen, draaien met een gewenste draaisnelheid), maar bestuurd door een TankBrain.
// TankBrain: de bestuurder. Hij kan elke tank besturen, ook die van de speler (toeschouwer, T): rijdt naar het doel tot zo'n 60 m,
// rijdt daar een eind opzij (een stilstaand doel is makkelijk te raken) en blijft niet te dichtbij; draait om gebouwen en water heen
// en rijdt achteruit als hij vastzit. De koepel richt op het doel met voorhouden voor de zwaartekracht en de snelheid van het doel;
// schieten alleen als hij het doel ziet (niets ertussen), de koepel goed staat en hij niet te ver is, met een beetje spreiding.
public class TankBrain
{
    public float throttle, steer; public bool fire; public string state = ""; public float FireErr; public string FireInfo = "";   // voor een test
    float stuckT, backT, backSteer, flankT, flankSide = 1f, avoid, seeT;
    public float Spread = 0.9f;   // graden spreiding bij het schieten

    public void Think(Transform self, Rigidbody rb, TankRig rig, Transform target, Rigidbody targetRb, float dt, bool canFire)
    {
        fire = false;
        if (self == null || rb == null || rig == null || target == null) { throttle = steer = 0f; state = "geen doel"; return; }
        Vector3 p = self.position, up = p.normalized, f = Flat(self.forward, up).normalized;
        Vector3 tp = target.position, to = tp - p, flat = Flat(to, up); float dist = to.magnitude;
        float v = Vector3.Dot(rb.linearVelocity, self.forward);

        // ---- rijden
        Vector3 want;
        flankT -= dt; if (flankT <= 0f) { flankT = Random.Range(5f, 9f); flankSide = Random.value < 0.5f ? -1f : 1f; }
        Vector3 side = Vector3.Cross(up, flat.normalized) * flankSide;
        float th;
        if (dist > 80f) { want = flat; th = 1f; state = "erheen"; }
        else if (dist < 32f) { want = -flat + side * 0.6f; th = 0.8f; state = "afstand nemen"; }
        else { want = side + flat.normalized * ((dist - 55f) / 40f); th = 0.55f; state = "opzij"; }
        want = Flat(want, up); if (want.sqrMagnitude < 1e-4f) want = f; want.Normalize();
        // gebouwen en water voor de neus: kijken waar het vrij is
        float look = 12f + Mathf.Abs(v) * 1.2f;
        if (Blocked(p, up, want, look))
        {
            Vector3 l = Quaternion.AngleAxis(-40f, up) * want, r = Quaternion.AngleAxis(40f, up) * want;
            bool bl = Blocked(p, up, l, look), br = Blocked(p, up, r, look);
            if (!bl && (br || avoid <= 0f)) { want = l; avoid = -1f; } else if (!br) { want = r; avoid = 1f; } else { want = Quaternion.AngleAxis(90f * (avoid >= 0f ? 1f : -1f), up) * want; }
            th = Mathf.Min(th, 0.5f); state += ", uitwijken";
        }
        else avoid = 0f;
        float ang = Vector3.SignedAngle(f, want, up);
        float st = Mathf.Clamp(ang / 35f, -1f, 1f);
        if (Mathf.Abs(ang) > 60f) th *= 0.15f;   // eerst draaien (op de plaats), dan rijden
        // vast: achteruit en weg
        if (backT > 0f) { backT -= dt; th = -0.8f; st = backSteer; state = "achteruit, vast"; }
        else if (Mathf.Abs(th) > 0.3f && Mathf.Abs(v) < 0.6f) { stuckT += dt; if (stuckT > 2.5f) { backT = 2f; backSteer = Random.value < 0.5f ? -1f : 1f; stuckT = 0f; } }
        else stuckT = Mathf.Max(0f, stuckT - dt);
        throttle = th; steer = st;

        // ---- richten: koepel (70°/s) en kanon (40°/s, -25° tot +8°) naar het doel, met voorhouden
        Vector3 aim = tp + target.up * 1.6f;
        float tt = Vector3.Distance(rig.gun.position, aim) / PlanetBuilder.ShellSpeed;
        if (targetRb != null) aim += targetRb.linearVelocity * tt * 0.8f;
        Vector3 hullUp = rig.turret.parent.up, ta = aim - rig.turret.position, tf = ta - hullUp * Vector3.Dot(ta, hullUp);
        float yawDes = Vector3.SignedAngle(rig.turret.parent.forward, tf, hullUp), yaw = rig.turret.localEulerAngles.y; if (yaw > 180f) yaw -= 360f;
        rig.turret.localRotation = Quaternion.Euler(0f, Mathf.MoveTowardsAngle(yaw, yawDes, 70f * dt), 0f);
        Vector3 g2 = aim - rig.gun.position; float d = g2.magnitude; tt = d / PlanetBuilder.ShellSpeed;
        Vector3 lift = rig.gun.position.normalized * (0.5f * 9.81f * tt * tt);
        Vector3 ld = rig.turret.InverseTransformDirection(g2 + lift); float pitchDes = Mathf.Clamp(-Mathf.Atan2(ld.y, new Vector2(ld.x, ld.z).magnitude) * Mathf.Rad2Deg, -25f, 8f);
        float pitch = rig.gun.localEulerAngles.x; if (pitch > 180f) pitch -= 360f;
        rig.gun.localRotation = Quaternion.Euler(Mathf.MoveTowards(pitch, pitchDes, 40f * dt), 0f, 0f);

        // ---- schieten: ziet hij het doel, staat de koepel goed en is het niet te ver?
        Vector3 m = rig.muzzle.position, tm = aim - m;
        bool see = !Physics.Raycast(m, tm.normalized, tm.magnitude - 3f, Layers.Muren, QueryTriggerInteraction.Ignore);
        seeT = see ? seeT + dt : 0f;
        float yawErr = Mathf.Abs(Mathf.DeltaAngle(Mathf.MoveTowardsAngle(yaw, yawDes, 70f * dt), yawDes)), pitchErr = Mathf.Abs(pitch - pitchDes);
        // het kanon moet echt op het doel staan: staat de tank scheef (een helling), dan haalt het kanon het soms niet (-25° tot +8°)
        float err = Vector3.Angle(rig.gun.forward, (g2 + lift).normalized);
        if (canFire && seeT > 0.6f && dist < 260f && yawErr < 2.5f && pitchErr < 2f && err < 1.2f) { fire = true; FireErr = err; FireInfo = $"koepel {yaw:0}°/{yawDes:0}°, kanon {pitch:0.0}°/{pitchDes:0.0}°, {dist:0} m"; }
        if (!see) state += ", ziet het doel niet";
    }

    // de richting waarin het kanon nu wijst, met spreiding
    public Vector3 ShotDir(TankRig rig) => Quaternion.AngleAxis(Random.Range(-Spread, Spread), rig.gun.up) * Quaternion.AngleAxis(Random.Range(-Spread, Spread) * 0.6f, rig.gun.right) * rig.gun.forward;

    static bool Blocked(Vector3 p, Vector3 up, Vector3 dir, float len)
    {
        // te steil (meer dan zo'n 35°) omhoog of omlaag: daar niet heen
        float h0 = PlanetCity.TerrainAt(up), h1 = PlanetCity.TerrainAt((p + dir * 10f).normalized);
        if (Mathf.Abs(h1 - h0) > 7f) return true;
        if (Physics.SphereCast(p + up * 1.8f, 1.5f, dir, out _, len, 1 << Layers.Gebouw, QueryTriggerInteraction.Ignore)) return true;
        for (int k = 1; k <= 2; k++) { int c = Planet.Nearest((p + dir * len * k * 0.5f).normalized * Planet.R); if (c >= 0 && PlanetCity.Wet(c)) return true; }
        return false;
    }
    static Vector3 Flat(Vector3 v, Vector3 up) => v - up * Vector3.Dot(v, up);
}

public class EnemyTank : MonoBehaviour
{
    public TankRig rig; public Rigidbody rb; public float hp = 100f; public bool Dead => hp <= 0f;
    public readonly TankBrain brain = new TankBrain();
    public Transform target; public Rigidbody targetRb;
    public System.Action<EnemyTank, Vector3> fire;   // de PlanetBuilder laat de granaat vliegen
    public float reload = 4f;   // na het verschijnen even niet schieten
    public const float ReloadTime = 2.4f;
    float wheelR, rest, flipT; float[] len; Vector3[] pt, nm, mounts; float[] load, comp; bool[] on;
    const float PMount = 1f;
    static Material tint;

    public static EnemyTank Spawn(Transform parent, Vector3 pos, Vector3 fwd)
    {
        var root = new GameObject("Tank van de tegenstander").transform; root.SetParent(parent, false); root.gameObject.layer = Layers.Mens;
        var trg = ModelTank.Build(root) ?? TankModel.Build(root);
        // een andere tint, zodat je hem herkent: roestrood
        if (ModelTank.Body != null)
        {
            if (tint == null) { tint = new Material(ModelTank.Body); tint.SetColor("_BaseColor", new Color(1f, 0.55f, 0.48f)); }
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true)) { var ms = r.sharedMaterials; for (int k = 0; k < ms.Length; k++) if (ms[k] == ModelTank.Body) ms[k] = tint; r.sharedMaterials = ms; }
        }
        var e = root.gameObject.AddComponent<EnemyTank>(); e.rig = trg; e.wheelR = trg.WheelR;
        int nw = trg.PhysWheels.Length;
        e.rest = PMount - e.wheelR + PlanetLife.TankMass * 9.81f / nw / PlanetLife.TankSpring;
        e.len = new float[nw]; e.pt = new Vector3[nw]; e.nm = new Vector3[nw]; e.mounts = new Vector3[nw]; e.load = new float[nw]; e.comp = new float[nw]; e.on = new bool[nw];
        for (int k = 0; k < nw; k++) e.len[k] = e.rest;
        Vector3 up = pos.normalized; fwd = (fwd - up * Vector3.Dot(fwd, up)).normalized;
        float alt = PlanetCity.TerrainAt(up);
        if (Physics.Raycast(up * (Planet.R + alt + 30f), -up, out RaycastHit hit, 80f, Layers.Grond, QueryTriggerInteraction.Ignore)) alt = hit.point.magnitude - Planet.R;
        root.position = up * (Planet.R + alt + 0.4f); root.rotation = Geo.Look(fwd, up);
        PlanetLife.TankShape(root, trg);
        var rb = root.gameObject.AddComponent<Rigidbody>();
        rb.mass = PlanetLife.TankMass; rb.useGravity = false; rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; rb.linearDamping = 0f; rb.angularDamping = 0.4f; rb.maxAngularVelocity = 14f;
        rb.centerOfMass = new Vector3(0f, PlanetLife.TankComY, 0.1f);
        e.rb = rb; return e;
    }

    void Update()
    {
        if (rb == null || rig == null) return;
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        float v = Vector3.Dot(rb.linearVelocity, transform.forward), w = Vector3.Dot(rb.angularVelocity, transform.up);
        rig.Move((v + w * rig.TrackHalf) * dt, (v - w * rig.TrackHalf) * dt);
        rig.Suspend(comp, PlanetLife.TankMass * 9.81f / comp.Length / PlanetLife.TankSpring, dt);   // loopwielen en rups veren mee
        if (Dead) { brain.throttle = brain.steer = 0f; return; }
        reload -= dt;
        brain.Think(transform, rb, rig, target, targetRb, dt, reload <= 0f);
        if (brain.fire && fire != null) { reload = ReloadTime + Random.Range(0f, 0.8f); fire(this, brain.ShotDir(rig)); }
    }

    // één natuurkundestap: het tankdeel van PlanetLife.PhysPlayer (zelfde getallen)
    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime; if (rb == null || rig == null || dt <= 0f) return;
        Vector3 pos = rb.position, up = pos.normalized; Quaternion rot = rb.rotation;
        Vector3 cu = rot * Vector3.up, cf = rot * Vector3.forward;
        rb.AddForce(-up * 9.81f, ForceMode.Acceleration);
        Vector3 vel = rb.linearVelocity; float vf = Vector3.Dot(vel, cf), sp = vel.magnitude;
        rb.AddForce(-vel * sp * 0.00045f, ForceMode.Acceleration);
        float th = Dead ? 0f : brain.throttle, st = Dead ? 0f : brain.steer;
        float top = PlanetLife.TankTop, acc = PlanetLife.TankAccel, mass = PlanetLife.TankMass, spring = PlanetLife.TankSpring;
        float accel = 0f; bool brake = false;
        if (th > 0.05f) { if (vf < -0.8f) brake = true; else accel = th * acc * Mathf.Clamp01(1f - vf / top); }
        else if (th < -0.05f) { if (vf > 0.8f) brake = true; else accel = th * acc * 0.67f * Mathf.Clamp01(1f + vf / PlanetLife.TankBack); }
        bool idle = !brake && Mathf.Abs(th) <= 0.05f;
        var wh = rig.PhysWheels; int nw = wh.Length, n = 0; float maxLen = rest + wheelR;
        for (int k = 0; k < nw; k++)
        {
            mounts[k] = pos + rot * new Vector3(wh[k].x, PMount, wh[k].z);
            on[k] = PlanetLife.WheelHit(mounts[k], -cu, maxLen, out pt[k], out nm[k], out float d); comp[k] = 0f; load[k] = 0f;
            if (!on[k]) { len[k] = rest; continue; }
            float l = Mathf.Max(0f, d - wheelR); comp[k] = rest - l;
            float sv = (len[k] - l) / dt; float f = spring * comp[k] + (sv > 0f ? PlanetLife.TankDampIn : PlanetLife.TankDampOut) * sv;
            if (l < 0.14f) f += (0.14f - l) * spring * 12f;
            load[k] = Mathf.Max(0f, f); len[k] = l; n++;
        }
        for (int k = 0; k + 1 < nw; k += 2)
        {
            float ar = (comp[k] - comp[k + 1]) * PlanetLife.TankAntiRoll;
            if (on[k]) load[k] = Mathf.Max(0f, load[k] + ar); if (on[k + 1]) load[k + 1] = Mathf.Max(0f, load[k + 1] - ar);
        }
        float m4 = mass / nw;
        if (n > 0)
        {
            float want = st * Mathf.Lerp(1.2f, 0.6f, Mathf.Abs(vf) / top) * (vf < -0.5f ? -1f : 1f), wNow = Vector3.Dot(rb.angularVelocity, cu);
            rb.AddTorque(cu * Mathf.Clamp((want - wNow) * 12f, -10f, 10f) * n / nw, ForceMode.Acceleration);
        }
        for (int k = 0; k < nw; k++)
        {
            if (!on[k]) continue;
            rb.AddForceAtPosition(cu * load[k], mounts[k]);
            Vector3 nn = nm[k], wf = cf - nn * Vector3.Dot(cf, nn); if (wf.sqrMagnitude < 1e-6f) continue; wf.Normalize();
            Vector3 wr = Vector3.Cross(nn, wf), pv = rb.linearVelocity; float vl = Vector3.Dot(pv, wf), vs = Vector3.Dot(pv, wr);
            float grip = PlanetLife.TankMu * load[k], fLat = -vs * m4 / 0.09f, fLong;
            if (brake) fLong = -Mathf.Sign(vl) * Mathf.Min(Mathf.Abs(vl) * m4 / 0.05f, m4 * 13f);
            else if (accel != 0f) fLong = m4 * accel;
            else if (idle && sp < 1.2f) fLong = -vl * m4 / 0.08f;
            else fLong = idle ? -Mathf.Sign(vl) * m4 * 0.5f : 0f;
            float tot = Mathf.Sqrt(fLat * fLat + fLong * fLong); if (tot > grip && tot > 0f) { float s = grip / tot; fLat *= s; fLong *= s; }
            rb.AddForceAtPosition(wf * fLong, pt[k] + cu * 0.25f); rb.AddForceAtPosition(wr * fLat, pt[k] + cu * Vector3.Dot(rb.worldCenterOfMass - pt[k], cu));   // dwars op de hoogte van het zwaartepunt: kantelt niet in een bocht
        }
        // over de kop of op zijn kant: na 2 s weer rechtop, ook als hij nog schuift (een wrak blijft liggen)
        if (!Dead && Vector3.Dot(cu, up) < 0.35f && sp < 6f) { flipT += dt; if (flipT > 2f) { Vector3 fw = cf - up * Vector3.Dot(cf, up); rb.position = pos + up * 1.5f; rb.rotation = Geo.Look(fw.sqrMagnitude > 1e-4f ? fw.normalized : Vector3.Cross(up, Vector3.right), up); rb.linearVelocity = rb.angularVelocity = Vector3.zero; flipT = 0f; } }
        else flipT = 0f;
        // vangnet: door de grond gezakt
        float alt = pos.magnitude - Planet.R;
        if (!PlanetCity.InBore(pos, out _) && alt < PlanetCity.TerrainAt(up) - 4f && Physics.Raycast(up * (Planet.R + PlanetCity.TerrainAt(up) + 30f), -up, out RaycastHit h, 60f, Layers.Grond, QueryTriggerInteraction.Ignore))
        { rb.position = h.point + up * 0.8f; rb.linearVelocity = vel - up * Vector3.Dot(vel, up); }
    }
}
