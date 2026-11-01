using System.Collections.Generic;
using UnityEngine;

// Rijgedrag van de webversie (11-autos.js, overdracht § 4, 5, 12–14). Netjes verkeer volgt zijn rijlijn en komt los als hij vastzit
// achter een auto die niet meer rijdt (achteruit, dan eromheen). In de stand "Totale chaos" krijgt elke auto een persona: de racer
// haalt in over de linker strook, de ontwijker stuurt om obstakels heen in plaats van te remmen, de jager gaat achter mensen aan.
// Te snel in een bocht: slippen en glijden (op een kruispunt soms de lucht in); auto's botsen als gerichte dozen, krijgen schade,
// roken, branden en ontploffen. Een auto die los van zijn rijlijn tot stilstand komt zoekt zelf de weg terug.
public partial class PlanetLife
{
    public static bool Chaos;
    // ongelukjes voor de teller linksboven: botsingen tussen auto's en mensen die omver gereden worden, en waar het laatste gebeurde
    public int Crashes, Knocked; public Vector3 LastIncident; public float LastIncidentT = -99f;
    bool chaosWas;
    static AudioClip boomClip, hornClip, beepClip;
    static Mesh ico;
    ParticleSystem smokePs, firePs;

    // per persona (0 netjes, 1 racer, 2 ontwijker, 3 jager): doelsnelheid, op een lang recht stuk, grip in de bocht
    static readonly float[] PTarget = { 0f, 26f, 20f, 17f }, PLong = { 0f, 42f, 32f, 30f }, PGrip = { 9f, 6.5f, 9.5f, 7.5f };
    const float HL = 2.3f, HW = 0.95f, INERT = (4f * HL * HL + 4f * HW * HW) / 12f;

    void GivePersona(Car c)
    {
        float r = Random.value; c.persona = !Chaos ? 0 : r < 0.35f ? 1 : r < 0.75f ? 2 : 3;
        c.ovOther = null; c.ovT = c.dodgeT = c.huntT = c.huntRest = 0f; c.prey = null; if (c.aroundT <= 0f) c.latT = 0f;
    }

    static Vector3 Flat(Vector3 v, Vector3 up) => v - up * Vector3.Dot(v, up);
    static Vector3 Vel(Car c) => c.free ? c.vel : c.fwd * c.v;

    void DriveCar(Car c, float dt)
    {
        int p = c.persona; bool chaos = p > 0;
        float len = Mathf.Max(1f, ArcLen(c.a, c.b, c.c)), bendNow = Bend(c.a, c.b, c.c), bendNext = Bend(c.b, c.c, c.n);
        bool straight = bendNow < 0.05f && bendNext < 0.05f;
        Vector3 pos = c.root.position, up = pos.normalized, right = Vector3.Cross(up, c.fwd);
        float want;
        if (!chaos) want = c.speed * Mathf.Lerp(1f, 0.45f, Mathf.Max(bendNow * (c.s < 0.6f ? 1f : 0.3f), c.s > 0.6f ? bendNext : 0f));
        else
        {
            want = straight ? PLong[p] : PTarget[p];
            if (bendNow > 0.3f) want = Mathf.Min(want, 9f + 6f * (1f - bendNow));
            if (bendNext > 0.3f) want = Mathf.Min(want, Mathf.Sqrt(81f + 2f * 7.5f * c.late * (1f - c.s) * len));   // afremmen voor wat komt (soms te laat)
        }

        // de auto vlak vooruit op dezelfde strook
        Car lead = null; float leadD = 1e9f, leadLat = 0f;
        foreach (var o in cars)
        {
            if (o == c || o.dead) continue; Vector3 d = o.root.position - pos; float ahead = Vector3.Dot(d, c.fwd);
            if (ahead < 0.5f || ahead > 26f) continue; float lat = Vector3.Dot(d, right); if (Mathf.Abs(lat) > 2.2f || Mathf.Abs(Vector3.Dot(d, up)) > 2.5f) continue;
            if (ahead < leadD) { lead = o; leadD = ahead; leadLat = lat; }
        }
        float leadV = lead == null ? 0f : Vel(lead).magnitude;
        bool ignoreLead = lead != null && (c.dodgeT > 0f || lead == c.stuckCar || lead == c.ovOther);
        bool head = lead != null && leadD < 9f && (lead.free || lead.burnT >= 0f || (Mathf.Abs(lead.v) < 0.5f && !lead.blocked && !lead.atLight));
        c.blocked = lead != null && leadD < 11f && !ignoreLead;
        if (c.blocked) want = Mathf.Min(want, Mathf.Max(0f, (leadD - 4.5f) * (chaos ? 1.6f : 1.2f)));

        c.atLight = false;
        // de rotonde op (de volgende boog is de rotonde): voorrang aan wie er al rijdt en naar onze inrit toe komt
        if (!chaos && c.c != c.b && PlanetCity.IsRot(c.c) && !PlanetCity.IsRot(c.b))
        {
            PlanetCity.RotPath(c.b, c.c, c.n, Lane(c.b, c.c), Lane(c.c, c.n), out Vector3 C, out Vector3 e, out Vector3 nn, out float f0, out _, out float r0, out _);
            Vector3 entry = C + (e * Mathf.Cos(f0) + nn * Mathf.Sin(f0)) * r0; bool give = false;
            foreach (var o in cars)
            {
                if (o == c || o.dead || o.free || o.b != c.c || o.a == c.b) continue; Vector3 d = entry - o.root.position;
                if (d.magnitude < 13f && Vector3.Dot(d, o.fwd) > -1.5f) { give = true; break; }
            }
            if (give) { float d = Mathf.Max(0f, (1f - c.s) * len - 5f); want = Mathf.Min(want, Mathf.Sqrt(2f * 4f * d)); c.atLight = true; }   // achter de haaientanden wachten, niet op de ring
        }
        // verkeerslicht aan het eind van deze boog: remmen voor rood, en voor geel als dat nog kan
        if (c.c != c.b && Traffic.IsJunction(c.c))
        {
            var ts = Traffic.State(c.c, c.b);
            if (ts != Traffic.S.Groen)
            {
                float d = Mathf.Max(0f, (1f - c.s) * len - 1.2f);
                if (ts == Traffic.S.Rood || d > c.v * c.v / 10f) { want = Mathf.Min(want, Mathf.Sqrt(2f * 4f * d)); c.atLight = true; }
            }
        }

        // persona's
        if (p == 1)   // racer: inhalen over de linker strook
        {
            if (c.ovOther == null && lead != null && leadD > 3f && c.v > 3f && leadV < c.v - 0.5f && bendNow < 0.05f && c.aroundT <= 0f && c.backT <= 0f)
            { c.ovOther = lead; c.ovT = 3.5f; c.latT = -2.7f; Horn(c); }
            if (c.ovOther != null) { c.ovT -= dt; if (c.ovT <= 0f && (c.ovOther.dead || Vector3.Dot(c.ovOther.root.position - pos, c.fwd) < -7f || c.ovT < -8f)) { c.ovOther = null; c.latT = 0f; } }
        }
        else if (p == 2)   // ontwijker: opzij in plaats van remmen
        {
            if (c.dodgeT > 0f) { c.dodgeT -= dt; if (c.dodgeT <= 0f) c.latT = 0f; }
            else
            {
                float side = 0f;
                foreach (var a in People)
                {
                    Vector3 d = a.root.position - pos; float ah = Vector3.Dot(d, c.fwd), lt = Vector3.Dot(d, right);
                    if (ah > 3f && ah < 6f + 0.9f * c.v && Mathf.Abs(lt) < 1.6f) { side = lt >= 0f ? 1f : -1f; break; }
                }
                if (side == 0f && lead != null && leadD > 3f && leadD < 14f && leadV < c.v - 0.5f) side = leadLat >= 0f ? 1f : -1f;
                if (side != 0f) { c.dodgeT = Random.Range(1.4f, 1.6f); c.latT = Mathf.Clamp(c.lat - side * 2.4f, -4.5f, 4.5f); }
            }
        }
        else if (p == 3)   // jager: het dichtstbijzijnde doelwit in een kegel vooruit
        {
            if (c.huntRest > 0f) c.huntRest -= dt;
            else if (c.huntT <= 0f)
            {
                Transform best = null; float bd = 1e9f;
                void Try(Transform t)
                {
                    Vector3 d = t.position - pos; float ah = Vector3.Dot(d, c.fwd); if (ah < 2.5f || ah > 34f) return;
                    if (Mathf.Abs(Vector3.Dot(d, right)) > 3f + 0.45f * ah) return; if (ah < bd) { bd = ah; best = t; }
                }
                foreach (var a in People) if (a.downT <= 0f) Try(a.root);
                if (walker != null && walker.isActiveAndEnabled) Try(walker.transform);
                if (best != null) { c.prey = best; c.huntT = 6f; Horn(c); }
            }
            if (c.huntT > 0f)
            {
                c.huntT -= dt;
                if (c.prey == null || c.huntT <= 0f) { c.huntT = 0f; c.huntRest = 5f; c.prey = null; c.latT = 0f; }
                else { c.latT = Mathf.Clamp(c.lat + Vector3.Dot(c.prey.position - pos, right), -4.5f, 5.5f); want = Mathf.Min(want, 16f); }
            }
        }

        // vastzitten achter een auto die niet meer rijdt: wachten, achteruit, eromheen
        if (c.backT > 0f)
        {
            c.backT -= dt; c.beepT -= dt;
            if (c.type == "bestel" && c.beepT <= 0f) { c.beepT = 0.6f; OneShot(c, beepClip, 0.5f); }
            if (c.backT <= 0f) { c.aroundT = chaos ? 3.5f : 5f; c.oppWait = 0f; }
        }
        else if (c.aroundT > 0f || c.stuckCar != null)
        {
            bool opp = false;
            if (!chaos && c.latT == 0f && c.oppWait < 12f)   // netjes verkeer wacht op een tegenligger
                foreach (var o in cars)
                {
                    if (o == c || o.free) continue; Vector3 d = o.root.position - pos; float ah = Vector3.Dot(d, c.fwd), lt = -Vector3.Dot(d, right);
                    if (Vector3.Dot(o.fwd, c.fwd) < -0.5f && ah > -3f && ah < 32f && lt > 0.8f && lt < 5f) { opp = true; break; }
                }
            if (opp) { c.oppWait += dt; want = 0f; }
            else { c.latT = -2.7f; c.aroundT -= dt; want = Mathf.Min(want, chaos ? 14f : 4.5f); if (!c.blocked) want = Mathf.Max(want, 2.5f); }
            c.aroundAll += dt;
            bool passed = c.stuckCar == null || c.stuckCar.dead || Vector3.Dot(c.stuckCar.root.position - pos, c.fwd) < -6.5f;
            if ((c.aroundT <= 0f && passed) || c.aroundAll > 25f) { c.aroundT = 0f; c.stuckCar = null; c.latT = 0f; c.aroundAll = 0f; }
        }
        else if (head && c.v < 1.2f)
        {
            c.waitT += dt;
            if (c.waitT > (chaos ? 1.2f : 4f))
            {
                c.waitT = 0f; c.stuckCar = lead; c.aroundAll = 0f; bool clear = true;
                foreach (var o in cars)
                {
                    if (o == c) continue; Vector3 d = o.root.position - pos; float ah = Vector3.Dot(d, c.fwd);
                    if (ah < 0f && ah > -7.5f && Mathf.Abs(Vector3.Dot(d, right)) < 1.9f) { clear = false; break; }
                }
                if (clear && !chaos) foreach (var a in People) if (Vector3.Distance(a.root.position, pos) < 5f) { clear = false; break; }
                if (clear) { c.backT = chaos ? 1f : 1.6f; c.beepT = 0f; } else { c.aroundT = chaos ? 3.5f : 5f; c.oppWait = 0f; }
            }
        }
        else c.waitT = 0f;

        // snelheid
        if (c.burnT >= 0f) want = 0f;
        if (c.ghostT > 0f) want = Mathf.Min(want, 2.5f);
        if (c.backT > 0f) c.v = Mathf.MoveTowards(c.v, chaos ? -3f : -1.6f, 8f * dt);
        else
        {
            float acc = !chaos ? 4f : p == 1 && straight ? 12f : 7f, brk = c.burnT >= 0f ? 3f : 9f;
            c.v = Mathf.MoveTowards(c.v, want, (want > c.v ? acc : brk) * dt);
        }
        c.lat = Mathf.MoveTowards(c.lat, c.latT, (chaos ? 3f : 1.5f) * dt);
        c.s += c.v * dt / len; if (c.s < 0f) { c.s = 0f; c.v = 0f; }
        while (c.s >= 1f)
        {
            c.s -= 1f; c.a = c.b; c.b = c.c; c.c = PlanetCity.IsRoad(c.n) ? c.n : NextRoad(c.b, c.a); c.n = NextRoad(c.c, c.b);
            c.late = Random.value < 0.4f ? Random.Range(1.6f, 3.2f) : 1f;   // per stuk weg: 40 % remt te laat
        }
        if (c.audio != null) c.audio.pitch = 0.85f + Mathf.Abs(c.v) / 22f;

        // slippen: te snel in de bocht
        if (chaos && c.v > 7f && bendNow > 0.3f && c.s > 0.15f && c.s < 0.85f)
        {
            float kappa = Mathf.Min(1f, bendNow) * Mathf.PI * 0.5f / len, ratio = c.v * c.v * kappa / PGrip[p];
            bool ring = Traffic.IsJunction(c.b);   // een kruispunt geldt als rotonde
            if (ratio > 1f && Random.value < (ratio - 1f) * 1.1f * (ring ? 2.2f : 1f) * dt)
            {
                GoFree(c, c.fwd * c.v * 1.05f, (Random.value < 0.5f ? -1f : 1f) * Random.Range(0.8f, 2.4f), 3.6f);
                if (ring && Random.value < 0.6f) { c.air = true; c.vy = Random.Range(2.5f, 5.5f); c.tumbleAxis = Random.onUnitSphere; c.tumbleW = Random.Range(120f, 360f); }
            }
        }
    }

    void GoFree(Car c, Vector3 vel, float w, float fric)
    {
        Vector3 up = c.root.position.normalized;
        c.free = true; c.vel = Flat(vel, up); c.w = w; c.fric = fric; c.air = false; c.vy = 0f; c.stillT = c.freeT = 0f; c.tumble = Quaternion.identity;
        c.fwd = Flat(c.fwd, up).normalized; c.ovOther = null; c.stuckCar = null; c.aroundT = c.backT = c.waitT = 0f; c.blendT = 0f;
    }

    // hoogte van de grond onder een wereldpunt: het wegdek of pad, anders het maaiveld
    static float GroundAt(Vector3 p)
    {
        int i = Planet.Nearest(p);
        return PlanetCity.IsWalk(i) ? PlanetCity.SurfaceH(i, p) + 0.1f : PlanetCity.TerrainAt(p.normalized);
    }
    static bool Wall(Vector3 probe, float g)
    {
        int i = Planet.Nearest(probe);
        if (PlanetCity.Cells.TryGetValue(i, out var l) && l.Count > 0 && !PlanetCity.IsWalk(i) && PlanetCity.StackHeight(i) > 0.5f) return true;
        return GroundAt(probe) > g + 0.9f;
    }

    // los van de rijlijn: glijden, tollen, vliegen; tegen gebouwen en steil terrein terugstuiteren
    void UpdateFree(Car c, float dt)
    {
        Vector3 p = c.root.position; Vector3 up = p.normalized;
        c.freeT += dt;
        if (c.air) c.vy -= 9.81f * dt;
        p += c.vel * dt + up * (c.vy * dt);
        up = p.normalized; c.vel = Flat(c.vel, up);
        float g = GroundAt(p), h = p.magnitude - Planet.R - g, sp = c.vel.magnitude;
        if (c.air)
        {
            c.tumble = Quaternion.AngleAxis(c.tumbleW * dt, c.tumbleAxis) * c.tumble;
            if (h <= 0f && c.vy < 0f) { c.air = false; c.vy = 0f; c.tumble = Quaternion.identity; c.fric = 6f; c.dmgCool = 0f; Damage(c, 30f); }
        }
        if (!c.air)
        {
            p = up * (Planet.R + g); sp = Mathf.Max(0f, sp - c.fric * dt); c.vel = sp > 1e-4f ? c.vel.normalized * sp : Vector3.zero;
            c.w = Mathf.MoveTowards(c.w, 0f, 1.5f * dt);
        }
        if (sp > 0.5f && Wall(p + c.vel / sp * 1.8f, g)) { c.vel *= -0.25f; if (sp >= 3f) Damage(c, sp * 1.2f); c.fric = 6f; }
        Vector3 f = Quaternion.AngleAxis(c.w * Mathf.Rad2Deg * dt, up) * Flat(c.fwd, up); if (f.sqrMagnitude > 1e-6f) c.fwd = f.normalized;
        c.root.position = p; c.root.rotation = Geo.Look(c.fwd, up) * c.tumble;
        if (!c.air && sp < 0.3f) c.stillT += dt; else c.stillT = 0f;
        if (c.burnT < 0f && !c.air && (c.stillT > 2.5f || c.freeT > 12f)) Rejoin(c);
        if (c.freeT > 40f) c.dead = true;   // nergens een weg te vinden
    }

    // zelf de weg terugvinden: het beste wegvak in de buurt, dan rustig terug naar de rijlijn overvloeien (geen sprong)
    void Rejoin(Car c)
    {
        Vector3 p = c.root.position, pd = p.normalized; int bb = -1, ba = -1; float bs = 1e9f;
        foreach (int r in roads)
        {
            float d = Vector3.Distance(Planet.Cells[r].dir, pd) * Planet.R; if (d > 60f) continue;
            foreach (int a in PlanetCity.Partners(r))
            {
                if (!PlanetCity.IsRoad(a) || !PlanetCity.CanDrive(a, r)) continue;
                float score = d - 5f * Vector3.Dot((Planet.Cells[r].dir - Planet.Cells[a].dir).normalized, c.fwd);
                if (score < bs) { bs = score; bb = r; ba = a; }
            }
        }
        if (bb < 0) { if (c.freeT > 20f) c.dead = true; return; }
        c.a = ba; c.b = bb; c.c = NextRoad(bb, ba); c.n = NextRoad(c.c, bb);
        float best = 1e9f, bestS = 0f;
        for (int k = 0; k <= 10; k++)
        {
            Arc(c.a, c.b, c.c, k / 10f, Lane(c.a, c.b), 0.1f, out Vector3 q, out _, out _);
            float d = (q.normalized - pd).sqrMagnitude; if (d < best) { best = d; bestS = k / 10f; }
        }
        c.s = bestS; c.v = 0f; c.free = false; c.air = false; c.fromPos = p; c.fromRot = c.root.rotation; c.blendT = 1.2f; c.ghostT = 3f;
        c.lat = c.latT = 0f; c.ovOther = null; c.stuckCar = null; c.prey = null; c.aroundT = c.backT = c.waitT = c.dodgeT = c.huntT = 0f;
    }

    // botsingen tussen auto's: gerichte dozen, scheidende-assentest
    void Collide()
    {
        for (int i = 0; i < cars.Count; i++)
            for (int j = i + 1; j < cars.Count; j++)
            {
                Car A = cars[i], B = cars[j]; if (A.dead || B.dead || A.ghostT > 0f || B.ghostT > 0f) continue;
                Vector3 pa = A.root.position, pb = B.root.position, d = pb - pa; if (d.sqrMagnitude > 36f) continue;
                Vector3 up = pa.normalized; if (Mathf.Abs(Vector3.Dot(d, up)) > 2.5f) continue;   // op een ander niveau (brug)
                d = Flat(d, up);
                Vector3 fa = Flat(A.root.forward, up).normalized, fb = Flat(B.root.forward, up).normalized, ra = Vector3.Cross(up, fa), rb = Vector3.Cross(up, fb);
                float best = 1e9f; Vector3 n = Vector3.zero; bool hit = true;
                foreach (var ax in new[] { fa, ra, fb, rb })
                {
                    float o = HL * Mathf.Abs(Vector3.Dot(fa, ax)) + HW * Mathf.Abs(Vector3.Dot(ra, ax)) + HL * Mathf.Abs(Vector3.Dot(fb, ax)) + HW * Mathf.Abs(Vector3.Dot(rb, ax)) - Mathf.Abs(Vector3.Dot(d, ax));
                    if (o <= 0f) { hit = false; break; } if (o < best) { best = o; n = ax; }
                }
                if (!hit) continue; if (Vector3.Dot(d, n) < 0f) n = -n;
                Vector3 va = Vel(A), vb = Vel(B); float impact = Vector3.Dot(va - vb, n);
                if (impact > 1.5f)
                {
                    if (!A.free) GoFree(A, va, 0f, 6f); if (!B.free) GoFree(B, vb, 0f, 6f); A.fric = B.fric = 6f;
                    float J = 1.25f * impact / 2f; A.vel -= n * J; B.vel += n * J;
                    Vector3 cp = (pa + pb) * 0.5f;
                    A.w += Vector3.Dot(Vector3.Cross(cp - pa, -n * J), up) / INERT; B.w += Vector3.Dot(Vector3.Cross(cp - pb, n * J), up) / INERT;
                    Damage(A, impact * 1.9f); Damage(B, impact * 1.9f); A.stillT = B.stillT = 0f; Crashes++; LastIncident = cp; LastIncidentT = Time.time;
                }
                if (A.free) A.root.position -= n * best * (B.free ? 0.5f : 1f);
                if (B.free) B.root.position += n * best * (A.free ? 0.5f : 1f);
                if (!A.free && !B.free) { if (Vector3.Dot(d, A.fwd) > 0f) A.v *= 0.5f; else B.v *= 0.5f; }
            }
    }

    // een chaosauto (of een glijdende auto) rijdt mensen omver
    void HitPeople(Car c)
    {
        float sp = Vel(c).magnitude; if (sp < 3f || (c.persona == 0 && !c.free)) return;
        Vector3 p = c.root.position, up = p.normalized, f = Flat(c.root.forward, up).normalized, r = Vector3.Cross(up, f);
        foreach (var a in People)
        {
            if (a.downT > 0f) continue; Vector3 d = a.root.position - p; if (Mathf.Abs(Vector3.Dot(d, up)) > 1.2f) continue;
            if (Mathf.Abs(Vector3.Dot(d, f)) < 2.6f && Mathf.Abs(Vector3.Dot(d, r)) < 1.3f) { a.downT = 5f; a.paused = false; a.partner = null; Knocked++; LastIncident = a.root.position; LastIncidentT = Time.time; if (c.prey == a.root) c.huntT = 0.01f; }
        }
    }

    void Damage(Car c, float amt)
    {
        if (c.dmgCool > 0f || c.dead) return; c.dmgCool = 0.5f; c.hp -= amt;
        if (c.hp <= 0f && c.burnT < 0f) c.burnT = Random.Range(4.5f, 7.5f);   // brand: uitrollen, dan ontploffen
    }

    // rook en vuur bij schade
    void CarFx(Car c, float dt)
    {
        if (c.dmgCool > 0f) c.dmgCool -= dt; if (c.ghostT > 0f) c.ghostT -= dt;
        if (c.hp >= 65f && c.burnT < 0f) return;
        EnsureFx(); Vector3 p = c.root.position, up = p.normalized; var cam = Camera.main;
        bool near = cam == null || (cam.transform.position - p).sqrMagnitude < 260f * 260f;
        if (c.burnT >= 0f)
        {
            c.burnT -= dt;
            if (near)
            {
                c.fxAcc += 26f * dt;
                while (c.fxAcc >= 1f)
                {
                    c.fxAcc -= 1f; Vector3 at = p + up * 0.9f + Random.insideUnitSphere * 0.7f;
                    Emit(firePs, at, up * Random.Range(1.5f, 3f) + Random.insideUnitSphere * 0.6f, Random.Range(0.4f, 0.8f), Color.white, Random.Range(0.5f, 0.9f));
                    if (Random.value < 0.5f) Emit(smokePs, at + up * 0.5f, up * Random.Range(1.2f, 2f) + Random.insideUnitSphere * 0.4f, 0.55f, new Color(0.5f, 0.5f, 0.5f), Random.Range(2.2f, 3.2f));
                }
            }
            if (c.burnT <= 0f) Explode(c);
            return;
        }
        if (!near) return;
        bool dark = c.hp < 35f; c.fxAcc += (dark ? 9f : 4f) * dt;
        while (c.fxAcc >= 1f)
        {
            c.fxAcc -= 1f;
            Emit(smokePs, p + up * 0.8f + Flat(c.root.forward, up).normalized * 1.6f + Random.insideUnitSphere * 0.3f, up * Random.Range(1f, 1.8f) + Random.insideUnitSphere * 0.3f, 0.55f, dark ? new Color(0.45f, 0.45f, 0.45f) : Color.white, Random.Range(1.8f, 2.8f));
        }
    }

    void Explode(Car c)
    {
        if (c.dead) return; c.dead = true; EnsureFx(); Vector3 p = c.root.position, up = p.normalized;
        for (int k = 0; k < 22; k++) { Vector3 dir = (Random.onUnitSphere + up * 0.6f).normalized; Emit(firePs, p + up * 0.8f, dir * Random.Range(1.5f, 6.5f), Random.Range(0.6f, 1.2f), Color.white, Random.Range(0.5f, 1f)); }
        for (int k = 0; k < 16; k++) Emit(smokePs, p + up + Random.insideUnitSphere * 1.2f, up * Random.Range(0.8f, 2f) + Random.insideUnitSphere * 1.5f, 1.1f, new Color(0.5f, 0.5f, 0.5f), Random.Range(3f, 4.5f));
        if (!Ambience.Muted) AudioSource.PlayClipAtPoint(boomClip, p, 1f);
        foreach (var a in People) if (Vector3.Distance(a.root.position, p) < 7f) { a.downT = 6f; a.paused = false; a.partner = null; }
        foreach (var o in cars)
        {
            if (o == c || o.dead) continue; Vector3 d = o.root.position - p; if (d.magnitude > 5.5f) continue;
            o.dmgCool = 0f; Damage(o, 35f); if (!o.free) GoFree(o, Vel(o), 0f, 6f); o.vel += Flat(d, up).normalized * 4f; o.w += Random.Range(-1.5f, 1.5f);   // kettingreactie
        }
    }

    void Horn(Car c) => OneShot(c, hornClip, 0.7f);
    static void OneShot(Car c, AudioClip clip, float vol) { if (!Ambience.Muted && c.audio != null && clip != null) c.audio.PlayOneShot(clip, vol); }

    // ---------- deeltjes ----------
    void EnsureFx()
    {
        if (smokePs != null) return;
        if (ico == null) ico = MakeIco();
        var sg = new Gradient(); sg.SetKeys(new[] { new GradientColorKey(LowPolyCar.Hex("#3a3835"), 0f), new GradientColorKey(LowPolyCar.Hex("#9b9a96"), 1f) }, new[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(0.45f, 0.6f), new GradientAlphaKey(0f, 1f) });
        var fg = new Gradient(); fg.SetKeys(new[] { new GradientColorKey(LowPolyCar.Hex("#fff2a0"), 0f), new GradientColorKey(LowPolyCar.Hex("#ff7a1a"), 0.45f), new GradientColorKey(LowPolyCar.Hex("#a8200c"), 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
        smokePs = MakePs("Rook", false, sg, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 3.6f)));
        firePs = MakePs("Vuur", true, fg, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.2f)));
    }
    ParticleSystem MakePs(string name, bool add, Gradient g, AnimationCurve size)
    {
        var go = new GameObject(name); go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.playOnAwake = false; main.loop = true; main.maxParticles = 2000; main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f; main.startRotation3D = true; main.gravityModifier = 0f;
        var em = ps.emission; em.enabled = false; var sh = ps.shape; sh.enabled = false;
        var col = ps.colorOverLifetime; col.enabled = true; col.color = new ParticleSystem.MinMaxGradient(g);
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, size);
        var r = go.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Mesh; r.mesh = ico; r.sharedMaterial = PartMat(add);
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
        var e = new ParticleSystem.EmitParams { position = at, velocity = vel, startSize = size, startColor = col, startLifetime = life, rotation3D = new Vector3(Random.value, Random.value, Random.value) * 360f, angularVelocity3D = Random.insideUnitSphere * 90f, applyShapeToPosition = false };
        ps.Emit(e, 1);
    }
    static Mesh MakeIco()
    {
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        var v = new[] { new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0), new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t), new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1) };
        int[] f = { 0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8, 3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1 };
        var mb = new MeshBuilder(); float s = 0.5f / v[0].magnitude;
        for (int k = 0; k < f.Length; k += 3) { Vector3 a = v[f[k]] * s, b = v[f[k + 1]] * s, c = v[f[k + 2]] * s; mb.Tri(a, b, c, a + b + c); }
        return mb.Build();
    }

    // ---------- geluiden ----------
    static void MakeChaosClips()
    {
        if (boomClip != null) return;
        int sr = 22050;
        // boem: bruine ruis laagdoorlaat 900→70 Hz, bandruis 600→180, sinus 90→34 Hz en vijf metaaltikjes
        {
            int n = (int)(sr * 1.6f); var d = new float[n]; var rnd = new System.Random(9); float br = 0, lp = 0, ph = 0, b1 = 0, b2 = 0, peak = 1e-4f;
            float[] ticks = { 0.05f, 0.12f, 0.2f, 0.31f, 0.45f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr, k = Mathf.Clamp01(t / 0.9f), w = (float)rnd.NextDouble() * 2f - 1f;
                br = (br + 0.02f * w) / 1.02f; float fc = Mathf.Lerp(900f, 70f, k); lp += (1f - Mathf.Exp(-2f * Mathf.PI * fc / sr)) * (br * 8f - lp);
                float fb = Mathf.Lerp(600f, 180f, k); b1 += (1f - Mathf.Exp(-2f * Mathf.PI * fb * 1.4f / sr)) * (w - b1); b2 += (1f - Mathf.Exp(-2f * Mathf.PI * fb / 1.4f / sr)) * (w - b2);
                ph += 2f * Mathf.PI * Mathf.Lerp(90f, 34f, k) / sr;
                float s = (lp + (b1 - b2) * 0.6f + Mathf.Sin(ph) * 0.9f) * Mathf.Exp(-t * 2.6f) * Mathf.Min(1f, t * 300f);
                foreach (float tt in ticks) if (t > tt && t < tt + 0.03f) s += Mathf.Sin(2f * Mathf.PI * 2400f * (t - tt)) * (1f - (t - tt) / 0.03f) * 0.2f;
                d[i] = s; peak = Mathf.Max(peak, Mathf.Abs(s));
            }
            for (int i = 0; i < n; i++) d[i] *= 0.9f / peak;
            boomClip = AudioClip.Create("boem", n, 1, sr, false); boomClip.SetData(d, 0);
        }
        hornClip = Tone("claxon", 0.38f, new[] { 392f, 494f }, 0.25f);
        beepClip = Tone("piep", 0.14f, new[] { 1040f }, 0.3f);
    }
    static AudioClip Tone(string name, float dur, float[] freqs, float vol)
    {
        int sr = 22050, n = (int)(sr * dur); var d = new float[n]; float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr, s = 0f; foreach (float f in freqs) s += Mathf.Repeat(t * f, 1f) < 0.5f ? 1f : -1f;
            lp += 0.35f * (s / freqs.Length - lp); d[i] = lp * vol * Mathf.Min(1f, t * 200f) * Mathf.Min(1f, (dur - t) * 60f);
        }
        var c = AudioClip.Create(name, n, 1, sr, false); c.SetData(d, 0); return c;
    }
}
