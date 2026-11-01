using System.Collections.Generic;
using UnityEngine;

// Leven op de planeet: mensen lopen over de stoepen van wegvak naar wegvak (rechts houden), groeten elkaar,
// doen een praatje (met brabbeltaal en spreekballon) en groeten de speler. Auto's rijden over het asfalt.
// Alles staat recht op de bol: "omhoog" is de richting vanuit het middelpunt.
public partial class PlanetLife : MonoBehaviour
{
    public static PlanetLife I;
    public readonly List<Person> People = new List<Person>();
    class Car   // rijdt over een boog: rand(a,b) → hart b → rand(b,c); los daarvan (free) glijdt hij vrij over de bol (zie PlanetLife.Chaos)
    {
        public Transform root; public int a, b, c, n; public float s, speed, v; public AudioSource audio; public Vector3 fwd; public string type;
        public int persona; public float lat, latT;   // persona 0 netjes, 1 racer, 2 ontwijker, 3 jager; lat: zijwaarts naast de rijlijn (negatief = links)
        public float ovT, dodgeT, huntT, huntRest, waitT, backT, aroundT, aroundAll, oppWait, ghostT, stillT, freeT, blendT, dmgCool, burnT = -1f, beepT, fxAcc, hp = 100f, late = 1f;
        public bool free, air, blocked, atLight, dead; public Vector3 vel, tumbleAxis, fromPos; public float vy, w, fric, tumbleW; public Quaternion tumble = Quaternion.identity, fromRot;
        public Car ovOther, stuckCar; public Transform prey;
        public CarWheels wheels; public float steerDeg;   // de wielen (draaien en meesturen)
        public int lane = 1; public float waitPri, idleT; public string why = ""; public Car leadCar; public bool retreat;   // retreat: achteruit om een tegenligger ruimte te geven, daarna terug naar de eigen strook   // idleT: hoe lang hij al stilstaat   // rijstrook op een brede weg (0 links, 1 rechts); hoe lang hij al voorrang geeft
    }
    readonly List<Car> cars = new List<Car>();
    public int CarCount => cars.Count;
    readonly List<int> roads = new List<int>(), walks = new List<int>();
    static AudioClip hum;
    public static int CarBoost;   // extra auto's (voor het wegvoorbeeld)
    public static bool PeopleOn = true, CarsOn = true;   // aan/uit in het tijdpaneel en het menu
    public static void ResetStatics() { hum = null; PeopleOn = true; CarsOn = true; CarBoost = 0; Chaos = false; boomClip = hornClip = beepClip = null; ico = null; }
    float roadsT, spawnT, idleT = 4f;
    PlanetWalker walker;

    void Awake() { I = this; if (GetComponent<Traffic>() == null) gameObject.AddComponent<Traffic>(); }

    void RefreshRoads()
    {
        roads.Clear(); walks.Clear(); foreach (var kv in PlanetCity.Cells) { if (kv.Value.Count > 0 && kv.Value[0] == "weg") { roads.Add(kv.Key); if (!PlanetCity.IsRot(kv.Key)) walks.Add(kv.Key); } else if (kv.Value.Count == 1 && kv.Value[0] == "pad") walks.Add(kv.Key); }
    }

    // volgend wegvak vanaf a, liefst niet terug naar prev
    static int NextRoad(int a, int prev, bool walk = false)
    {
        var opts = new List<int>(6); foreach (int j in PlanetCity.Partners(a)) if ((walk ? PlanetCity.IsWalk(j) : PlanetCity.IsRoad(j) && PlanetCity.CanDrive(a, j)) && j != prev) opts.Add(j);   // auto's volgen het eenrichtingsverkeer; voetgangers lopen om een rotonde heen (RotWalk)   // auto's volgen het eenrichtingsverkeer
        if (!walk && prev >= 0 && prev != a && !PlanetCity.IsRot(a) && opts.Count > 1)   // auto's: een scherpe draai (meer dan 100°) op een kruising is niet te rijden; die slaan ze over als er een andere weg is
        {
            Vector3 d1 = (D(a) - D(prev)).normalized; var ok = new List<int>(opts.Count);
            foreach (int j in opts) if (Vector3.Dot(d1, (D(j) - D(a)).normalized) > -0.17f) ok.Add(j);
            if (ok.Count > 0) opts = ok;
        }
        if (opts.Count == 0) return prev >= 0 && (walk ? PlanetCity.LinkedWalk(a, prev) : PlanetCity.LinkedRoad(a, prev) && PlanetCity.CanDrive(a, prev)) ? prev : a;
        return opts[Random.Range(0, opts.Count)];
    }

    // een vak waar verkeer vandaan naar b mag komen
    static int PrevRoad(int b)
    {
        var opts = new List<int>(6); foreach (int j in PlanetCity.Partners(b)) if (PlanetCity.IsRoad(j) && PlanetCity.CanDrive(j, b)) opts.Add(j);
        return opts.Count > 0 ? opts[Random.Range(0, opts.Count)] : NextRoad(b, -1);
    }

    // plek op de bol tussen vak a en b, met een zijwaartse afstand (rechts van de looprichting)
    static void Where(int a, int b, float t, float side, float h, out Vector3 pos, out Vector3 fwd, out Vector3 up)
    {
        Vector3 da = Planet.Cells[a].dir, db = Planet.Cells[b].dir;
        Vector3 d = a == b ? da : Vector3.Slerp(da, db, t); up = d.normalized;
        Vector3 chord = a == b ? Planet.Cells[a].north : (db - da) * Planet.R;
        fwd = (chord - up * Vector3.Dot(chord, up)); if (fwd.sqrMagnitude < 1e-8f) fwd = Planet.Cells[a].east; fwd.Normalize();
        Vector3 right = Vector3.Cross(up, fwd);
        pos = up * (Planet.R + h) + right * side;
    }

    Person SpawnPerson()
    {
        var a = global::People.MakePerson(transform);
        a.cellA = walks[Random.Range(0, walks.Count)]; a.prevCell = -1; a.cellB = NextRoad(a.cellA, -1, true); a.ct = Random.value;
        PlacePerson(a, 0f); return a;
    }

    void PlacePerson(Person a, float dt)
    {
        bool ra = PlanetCity.IsRoad(a.cellA) && !PlanetCity.UnderLink(a.cellA, a.cellB), rb = PlanetCity.IsRoad(a.cellB) && !PlanetCity.UnderLink(a.cellB, a.cellA);   // onder een brug: op het pad
        float side = ra && rb ? (PlanetCity.WalkOffset(a.cellA) + PlanetCity.WalkOffset(a.cellB)) * 0.5f : ra ? PlanetCity.WalkOffset(a.cellA) * 0.5f : rb ? PlanetCity.WalkOffset(a.cellB) * 0.5f : PlanetCity.IsBike(a.cellA) && PlanetCity.IsBike(a.cellB) ? 2.4f : 0.35f;   // op een fietspad: rechts op de strook voor voetgangers
        float hgt = Mathf.Lerp(ra ? 0.25f : 0.11f, rb ? 0.25f : 0.11f, a.ct);   // op de stoep (25 cm) of op een pad
        Vector3 pos, fwd, up;
        if (PlanetCity.IsRot(a.cellA) && a.prevCell >= 0 && a.prevCell != a.cellA) { RotWalk(a.prevCell, a.cellA, a.cellB, a.ct, out pos, out fwd, out up); pos += up * (PlanetCity.G(a.cellA) + 0.25f); }   // over de stoep om de rotonde
        else
        {
            float ct = PlanetCity.IsRot(a.cellB) ? a.ct * RotWalkIn(a.cellA, a.cellB) : a.ct;   // naar een rotonde: tot de stoep eromheen
            Where(a.cellA, a.cellB, ct, side, hgt, out pos, out fwd, out up);
            pos += up * (ct < 0.5f ? PlanetCity.SurfaceH(a.cellA, a.cellB, pos) : PlanetCity.SurfaceH(a.cellB, a.cellA, pos));
        }   // terreinhoogte en de helling van de weg
        // De route (van vak naar vak, met de sprongetjes van bochten en randen) is alleen het doel: de voetganger stuurt ernaartoe,
        // met een grens aan versnellen, en wijkt uit voor wie te dichtbij komt. Zo loopt hij vloeiend, ook om een bocht en bij keren.
        if (dt <= 0f || !a.hasAt) { a.at = pos; a.vel = Vector3.zero; a.hasAt = true; a.lag = 0f; }
        else
        {
            Vector3 to = pos - a.at; to -= up * Vector3.Dot(to, up); float dist = to.magnitude; a.lag = dist;
            Vector3 want = a.paused || dist < 0.02f ? Vector3.zero : to / dist * Mathf.Min(a.speed * 1.25f, dist * 3f);
            foreach (var o in People)   // uitwijken: wegduwen van wie binnen 0,9 m loopt
            {
                if (o == a || !o.hasAt) continue; Vector3 d = a.at - o.at; d -= up * Vector3.Dot(d, up); float m = d.magnitude;
                if (m < 0.9f && m > 1e-3f) want += d / m * (0.9f - m) * 2.5f;
            }
            a.vel -= up * Vector3.Dot(a.vel, up); a.vel = Vector3.MoveTowards(a.vel, want, 4f * dt);
            a.at = (a.at + a.vel * dt).normalized * pos.magnitude;   // op de hoogte van de route (de stoep of het pad)
        }
        a.root.position = a.at;
        Vector3 face = a.vel.sqrMagnitude > 0.04f ? a.vel - up * Vector3.Dot(a.vel, up) : fwd;
        if (a.paused && a.partner != null) { Vector3 tp = a.partner.root.position - a.at; tp -= up * Vector3.Dot(tp, up); if (tp.sqrMagnitude > 1e-6f) face = tp; }
        Quaternion target = Geo.Look(face.sqrMagnitude > 1e-8f ? face.normalized : fwd, up);
        a.root.rotation = dt <= 0f ? target : Quaternion.Slerp(a.root.rotation, target, Mathf.Min(1f, dt * 6f));
    }

    Car SpawnCar()
    {
        var c = new Car { b = roads[Random.Range(0, roads.Count)], speed = 7f + Random.value * 4f, s = Random.value, lane = Random.Range(0, 2) };
        c.a = PrevRoad(c.b); c.c = NextRoad(c.b, c.a); c.n = NextRoad(c.c, c.b); c.v = c.speed;
        var root = new GameObject("Auto").transform; root.SetParent(transform, false); c.root = root; root.gameObject.layer = Layers.Mens;
        c.type = LowPolyCar.Build(root);   // een van de zeven low-poly modellen van de webversie
        c.wheels = root.GetComponent<CarWheels>();
        GivePersona(c);
        if (hum == null) hum = MakeHum();
        c.audio = root.gameObject.AddComponent<AudioSource>(); c.audio.clip = hum; c.audio.loop = true; c.audio.spatialBlend = 1f; c.audio.rolloffMode = AudioRolloffMode.Linear; c.audio.minDistance = 4f; c.audio.maxDistance = 70f; c.audio.volume = 0.35f; c.audio.pitch = 0.9f + c.speed / 25f; c.audio.Play();
        PlaceCar(c); return c;
    }

    // Elektrisch gezoem: zaagtand met een kwint erboven, laag gefilterd, plus wat bandengeruis
    static AudioClip MakeHum()
    {
        int sr = 22050, n = sr * 2; var d = new float[n]; var rnd = new System.Random(5); float ph1 = 0, ph2 = 0, lp = 0, nz = 0;
        for (int i = 0; i < n; i++)
        {
            ph1 += 110f / sr; if (ph1 > 1f) ph1 -= 1f; ph2 += 165f / sr; if (ph2 > 1f) ph2 -= 1f;
            float x = (ph1 * 2f - 1f) * 0.5f + Mathf.Sin(ph2 * 6.2832f) * 0.4f; lp += 0.06f * (x - lp);
            float w = (float)rnd.NextDouble() * 2f - 1f; nz += 0.12f * (w - nz);
            d[i] = lp * 0.8f + nz * 0.25f;
        }
        var clip = AudioClip.Create("zoem", n, 1, sr, false); clip.SetData(d, 0); return clip;
    }

    // Positie op de boog door vak b: een kwadratische Bézier van het midden van rand a-b, via het hart van b, naar het midden van rand b-c.
    // Rechtdoor is dat een rechte lijn; in een bocht van 60° een vloeiende boog. De auto houdt rechts (zijwaarts langs de raaklijn).
    // Rijrichting op de rand tussen x en y: langs de schuine weg als een van beide een schuin recht stuk is, anders van hart naar hart
    static Vector3 D(int i) => PlanetCity.NodeDir(i);   // het hart van het vak (bij een schuine kruising op een rand: daar)
    static Vector3 EdgeDir(int x, int y)
    {
        Vector3 cc = (D(y) - D(x)).normalized, w;
        if (PlanetCity.ChordDirWorld(y, out w) || PlanetCity.ChordDirWorld(x, out w)) return Vector3.Dot(w, cc) < 0f ? -w : w;
        return cc;
    }
    // Positie op de weg door vak b: een kubische Bézier van het midden van rand a-b naar het midden van rand b-c, met als
    // raaklijnen de rijrichting op die randen. Recht of schuin recht wordt een rechte lijn; een bocht een vloeiende boog die
    // precies op het asfalt blijft. De auto houdt rechts (zijwaarts langs de raaklijn).
    static void Arc(int a, int b, int cc, float s, float side, float h, out Vector3 pos, out Vector3 fwd, out Vector3 up)
    {
        if (PlanetCity.IsRot(b) && a != b) { RotArc(a, b, cc, s, h, out pos, out fwd, out up); return; }
        if (a == cc && a != b) { UTurn(a, b, s, side, h, out pos, out fwd, out up); return; }
        Vector3 P0 = Mid(a, b), P3 = Mid(b, cc);
        Vector3 t0 = EdgeDir(a, b), t3 = EdgeDir(b, cc); float k = Vector3.Distance(P0, P3) * 0.4f;
        Vector3 P1 = P0 + t0 * k, P2 = P3 - t3 * k;
        if (a == cc) { P1 = D(b) + t0 * k * 0.5f; P2 = D(b) - t3 * k * 0.5f; }   // keren: lus door het hart
        float u = 1f - s;
        Vector3 d = P0 * (u * u * u) + P1 * (3f * u * u * s) + P2 * (3f * u * s * s) + P3 * (s * s * s); up = d.normalized;
        Vector3 tan = (P1 - P0) * (3f * u * u) + (P2 - P1) * (6f * u * s) + (P3 - P2) * (3f * s * s);
        fwd = tan - up * Vector3.Dot(tan, up); if (fwd.sqrMagnitude < 1e-12f) fwd = Planet.Cells[b].north; fwd.Normalize();
        Vector3 right = Vector3.Cross(up, fwd);
        pos = up * (Planet.R + h) + right * side;
    }
    // Voetgangers om een rotonde: over het midden van de stoep eromheen (straal RotW), de kortste kant van de arm waar ze vandaan komen
    // naar de arm waar ze heen gaan; naar de rotonde toe lopen ze tot die stoep
    const float RotW = PlanetCity.RotRo + PlanetCity.RotWalk * 0.5f;
    static float RotWalkIn(int a, int rot) => Mathf.Clamp01((Vector3.Distance(Planet.Cells[a].dir, Planet.Cells[rot].dir) * Planet.R - RotW) / Mathf.Max(1f, Vector3.Distance(Planet.Cells[a].dir, Planet.Cells[rot].dir) * Planet.R));
    static void RotAngles(int from, int rot, int to, out float f0, out float span)
    {
        var c = Planet.Cells[rot]; Vector3 d0 = Planet.Cells[from].dir - c.dir, d1 = Planet.Cells[to].dir - c.dir;
        f0 = Mathf.Atan2(Vector3.Dot(d0, c.north), Vector3.Dot(d0, c.east)); float f1 = Mathf.Atan2(Vector3.Dot(d1, c.north), Vector3.Dot(d1, c.east));
        span = Mathf.DeltaAngle(f0 * Mathf.Rad2Deg, f1 * Mathf.Rad2Deg) * Mathf.Deg2Rad; if (from == to) span = Mathf.PI;   // terug naar dezelfde arm: een halve ronde en weer terug zou raar zijn; dan maar half rond
    }
    static float RotWalkLen(int from, int rot, int to) { RotAngles(from, rot, to, out _, out float span); return Mathf.Max(1.5f, Mathf.Abs(span) * RotW); }
    static void RotWalk(int from, int rot, int to, float t, out Vector3 pos, out Vector3 fwd, out Vector3 up)
    {
        var c = Planet.Cells[rot]; RotAngles(from, rot, to, out float f0, out float span); float f = f0 + span * t;
        Vector3 rad = c.east * Mathf.Cos(f) + c.north * Mathf.Sin(f); up = (c.dir * Planet.R + rad * RotW).normalized; pos = up * Planet.R;
        Vector3 tg = (-c.east * Mathf.Sin(f) + c.north * Mathf.Cos(f)) * Mathf.Sign(span == 0f ? 1f : span); fwd = (tg - up * Vector3.Dot(tg, up)).normalized;
    }
    // het midden van de rand x–y, of bij een rotonde het punt op de cirkel van de rijbaan in de richting van de arm
    static Vector3 Mid(int x, int y)
    {
        if (PlanetCity.IsRot(y)) { Vector3 c = Planet.Cells[y].dir; Vector3 d = Planet.Cells[x].dir - c; d -= c * Vector3.Dot(d, c); return (c * Planet.R + d.normalized * PlanetCity.RotRc).normalized; }
        if (PlanetCity.IsRot(x)) return Mid(y, x);
        return ((D(x) + D(y)) * 0.5f).normalized;
    }
    // over de rotonde in vak b, van arm a naar arm cc (tegen de klok in, zie PlanetCity.RotPath)
    static void RotArc(int a, int b, int cc, float s, float h, out Vector3 pos, out Vector3 fwd, out Vector3 up)
    {
        PlanetCity.RotPath(a, b, cc, Lane(a, b), Lane(b, cc), out Vector3 C, out Vector3 e, out Vector3 n, out float f0, out float span, out float r0, out float r1);
        float f = f0 + span * s, r = Mathf.Lerp(r0, r1, s); Vector3 rad = e * Mathf.Cos(f) + n * Mathf.Sin(f);
        Vector3 p = C + rad * r; up = p.normalized; pos = up * (Planet.R + h);
        Vector3 t = (-e * Mathf.Sin(f) + n * Mathf.Cos(f)) * Mathf.Sign(span); fwd = (t - up * Vector3.Dot(t, up)).normalized;
    }
    // Keren in de ronde kop van een doodlopende weg (of terug dezelfde arm op): van de eigen strook naar de buitenkant zwenken, een halve
    // cirkel van UR om het hart van het vak (de kop is 4,6 m; de auto blijft erbinnen) en terug op de strook van de andere richting.
    const float UR = 3.3f;
    static void UTurnPts(int a, int b, float side, out Vector3 A0, out Vector3 A1, out Vector3 B0, out Vector3 B1, out Vector3 C, out Vector3 t0, out Vector3 r)
    {
        Vector3 ub = D(b); C = ub * Planet.R; t0 = EdgeDir(a, b); t0 = (t0 - ub * Vector3.Dot(t0, ub)).normalized; r = Vector3.Cross(ub, t0);
        Vector3 P0 = Mid(a, b) * Planet.R; A0 = P0 + r * side; A1 = C + r * UR; B0 = C - r * UR; B1 = P0 - r * side;
    }
    static float UTurnLen(int a, int b) { UTurnPts(a, b, Lane(a, b), out var A0, out var A1, out var B0, out var B1, out _, out _, out _); return (A1 - A0).magnitude + Mathf.PI * UR + (B1 - B0).magnitude; }
    static void UTurn(int a, int b, float s, float side, float h, out Vector3 pos, out Vector3 fwd, out Vector3 up)
    {
        UTurnPts(a, b, side, out var A0, out var A1, out var B0, out var B1, out var C, out var t0, out var r);
        float L1 = (A1 - A0).magnitude, L2 = Mathf.PI * UR, L3 = (B1 - B0).magnitude, d = Mathf.Clamp01(s) * (L1 + L2 + L3); Vector3 p, t;
        if (d <= L1) { p = Vector3.Lerp(A0, A1, d / Mathf.Max(0.01f, L1)); t = A1 - A0; }
        else if (d <= L1 + L2) { float f = (d - L1) / L2 * Mathf.PI; p = C + r * (UR * Mathf.Cos(f)) + t0 * (UR * Mathf.Sin(f)); t = t0 * Mathf.Cos(f) - r * Mathf.Sin(f); }
        else { p = Vector3.Lerp(B0, B1, (d - L1 - L2) / Mathf.Max(0.01f, L3)); t = B1 - B0; }
        up = p.normalized; fwd = (t - up * Vector3.Dot(t, up)).normalized; pos = up * (Planet.R + h);
    }
    static float ArcLen(int a, int b, int cc) => PlanetCity.IsRot(b) && a != b ? RotLen(a, b, cc) : a == cc && a != b ? UTurnLen(a, b) : (Vector3.Distance(D(a), D(b)) + Vector3.Distance(D(b), D(cc))) * 0.5f * Planet.R;
    static float RotLen(int a, int b, int cc) { PlanetCity.RotPath(a, b, cc, Lane(a, b), Lane(b, cc), out _, out _, out _, out _, out float span, out float r0, out float r1); return Mathf.Abs(span) * (r0 + r1) * 0.5f; }
    static float Bend(int a, int b, int cc)   // 0 = rechtdoor, 1 = haakse of scherpere bocht
    {
        if (PlanetCity.IsRot(b)) return 0.8f;   // over de rotonde: rustig
        Vector3 d1 = (D(b) - D(a)).normalized, d2 = (D(cc) - D(b)).normalized;
        return Mathf.Clamp01(Mathf.Acos(Mathf.Clamp(Vector3.Dot(d1, d2), -1f, 1f)) * Mathf.Rad2Deg / 90f);
    }

    static float Lane(int x, int y, int lane = -1) => (PlanetCity.LaneOffset(x, lane) + PlanetCity.LaneOffset(y, lane)) * 0.5f;   // rijbaan: breder wegen, verder van het midden

    // een stap langs de boog van a naar b; aan het eind het volgende vak kiezen
    static bool Advance(ref int a, ref int b, ref int prev, ref float t, float speed, float dt, bool walk = false)
    {
        System.Func<int, bool> ok = walk ? (System.Func<int, bool>)PlanetCity.IsWalk : PlanetCity.IsRoad;
        if (a == b || !ok(a) || !ok(b)) { if (!ok(a)) return false; b = NextRoad(a, prev, walk); t = 0f; if (a == b) return true; }
        float len = walk && PlanetCity.IsRot(b) ? 1.5f : walk && PlanetCity.IsRot(a) && prev >= 0 ? RotWalkLen(prev, a, b) : Vector3.Distance(D(a), D(b)) * Planet.R;   // om een rotonde: over de stoep
        t += speed * dt / Mathf.Max(0.1f, len);
        while (t >= 1f) { t -= 1f; prev = a; a = b; b = NextRoad(a, prev, walk); if (a == b) { t = 0f; break; } }
        return true;
    }

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (walker == null) walker = FindAnyObjectByType<PlanetWalker>(FindObjectsInactive.Include);
        roadsT -= dt; if (roadsT <= 0f) { roadsT = 2f; RefreshRoads(); RefreshBikes(); }
        int targetP = PeopleOn ? Mathf.Clamp(walks.Count / 2, 0, 40) : 0, targetC = CarsOn ? Mathf.Clamp(roads.Count / 6 + CarBoost, 0, 16 + CarBoost) : 0;
        spawnT -= dt;
        if (spawnT <= 0f)
        {
            if (walks.Count > 0 && People.Count < targetP) { People.Add(SpawnPerson()); spawnT = 0.4f; }
            else if (roads.Count > 0 && cars.Count < targetC) { var nc = SpawnCar(); if (Crowded(nc)) Destroy(nc.root.gameObject); else cars.Add(nc); spawnT = 0.7f; }   // niet op een andere auto verschijnen
        }
        while (People.Count > targetP) { Destroy(People[People.Count - 1].root.gameObject); People.RemoveAt(People.Count - 1); }
        while (cars.Count > targetC) { Destroy(cars[cars.Count - 1].root.gameObject); cars.RemoveAt(cars.Count - 1); }

        for (int i = People.Count - 1; i >= 0; i--)
        {
            var a = People[i];
            if (a.greetCool > 0) a.greetCool -= dt; if (a.waveT > 0) a.waveT -= dt;
            if (!PlanetCity.IsWalk(a.cellA)) { Destroy(a.root.gameObject); People.RemoveAt(i); continue; }   // de weg is weg
            if (a.downT > 0f) { a.downT -= dt; PlacePerson(a, 0f); a.root.rotation = Quaternion.AngleAxis(-88f, a.root.right) * a.root.rotation; continue; }   // omvergereden: even liggen
            if (a.paused)
            {
                a.pauseT -= dt; if (a.pauseT <= 0f) { a.paused = false; a.partner = null; a.chatT = 0f; }
                else if (a.partner != null && a.chatT > 0f)
                {
                    a.chatT -= dt; a.talkT -= dt;
                    if (a.talkT <= 0f) { float dur = Chatter.I != null ? Chatter.I.Opine(a) : 0f; float len = Babble.Say(a, dur > 0f ? dur : 0.8f + Random.value * 1.8f, a.voice, false); a.talkT = 1e6f; if (a.partner.chatT > 0f) a.partner.talkT = len + 0.3f + Random.value * 0.6f; }
                }
                global::People.Animate(a, dt, false); PlacePerson(a, dt); continue;
            }
            Advance(ref a.cellA, ref a.cellB, ref a.prevCell, ref a.ct, a.speed * Mathf.Clamp(1.5f - a.lag / 1.5f, 0.15f, 1f), dt, true);   // loopt hij achter op zijn route (uitwijken), dan wacht de route a.phase += a.speed * dt * 5.2f;
            global::People.Animate(a, dt, a.cellA != a.cellB); PlacePerson(a, dt);
            a.socT -= dt; if (a.socT <= 0f) { a.socT = 0.4f; Social(a); }
        }
        if (Chaos != chaosWas) { chaosWas = Chaos; foreach (var c in cars) GivePersona(c); }
        MakeChaosClips();
        for (int i = cars.Count - 1; i >= 0; i--)
        {
            var c = cars[i];
            if (c.dead || (!c.free && (!PlanetCity.IsRoad(c.b) || (c.c == c.b && c.s > 0.5f)))) { Destroy(c.root.gameObject); cars.RemoveAt(i); continue; }   // ontploft, weg weg, of doodlopend eenrichtingsstuk
            if (c.free) UpdateFree(c, dt);
            else
            {
                if (!PlanetCity.IsRoad(c.a)) c.a = c.b; if (!PlanetCity.IsRoad(c.c)) { c.c = NextRoad(c.b, c.a); c.n = NextRoad(c.c, c.b); }
                DriveCar(c, dt);
            }
            CarFx(c, dt); HitPeople(c);
            if (c.audio != null) c.audio.volume = Ambience.Muted ? 0f : 0.35f;
        }
        Collide();   // altijd: auto's zijn vaste lichamen, ook netjes verkeer kan elkaar een duw geven

        UpdateBikes(dt);

        // af en toe een opmerking van iemand dichtbij de camera
        idleT -= dt;
        if (idleT <= 0f && Chatter.I != null && Camera.main != null)
        {
            idleT = 4f + Random.value * 5f; Vector3 cp = Camera.main.transform.position; var near = new List<Person>();
            foreach (var a in People) if (!a.paused && Vector3.Distance(a.root.position, cp) < 28f) near.Add(a);
            if (near.Count > 0 && Chatter.I.LiveCount < 3) { var a = near[Random.Range(0, near.Count)]; float dur = Chatter.I.Opine(a); if (dur > 0f) Babble.Say(a, dur * 0.8f, a.voice, false); }
        }
    }

    void Social(Person a)
    {
        if (a.greetCool > 0f) return;
        if (walker != null && walker.gameObject.activeInHierarchy && a.friendly && Vector3.Distance(walker.transform.position, a.root.position) < 2.8f)
        {
            Babble.Say(a, 0f, a.voice, true); if (Chatter.I != null) Chatter.I.Greet(a); a.waveT = 1.4f; a.greetCool = 25f; return;
        }
        foreach (var o in People)
        {
            if (o == a || o.paused || o.greetCool > 0f) continue;
            float d = Vector3.Distance(o.root.position, a.root.position); if (d > 2.4f || d < 0.4f) continue;
            if (Vector3.Dot(a.root.forward, o.root.forward) > -0.2f) continue;   // alleen tegemoet
            float r = Random.value;
            if (a.friendly && o.friendly && r < 0.15f)
            {
                float t = 3f + Random.value * 5f;
                foreach (var pq in new[] { (a, o), (o, a) }) { var p = pq.Item1; var q = pq.Item2; p.paused = true; p.pauseT = t; p.chatT = t; p.partner = q; p.greetCool = 40f + Random.value * 30f; p.waveT = 0.9f; p.talkT = p == a ? 0.3f : 1e6f; }
                return;
            }
            if (r < 0.6f)
            {
                a.waveT = o.waveT = 1.4f; a.greetCool = o.greetCool = 18f + Random.value * 20f;
                Babble.Say(a, 0f, a.voice, true); if (Chatter.I != null) { Chatter.I.Greet(a); Chatter.I.GreetLater(o, 0.45f); }
                return;
            }
            a.greetCool = 6f; return;
        }
    }
}
