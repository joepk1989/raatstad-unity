using System.Collections.Generic;
using UnityEngine;

// Aangereden of weggeblazen mensen worden een ragdoll (lappenpop): een Rigidbody met botsvormen op elf botten van de Humanoid
// (heup, borst, hoofd, armen en benen in twee delen), aan elkaar met CharacterJoints. De Animator staat dan uit; de klap geeft
// benen en bovenlijf elk hun eigen snelheid, zodat hij draait zoals bij een echte aanrijding (benen weggemaaid, bovenlijf op de motorkap).
// Zwaartekracht naar het middelpunt van de bol; botsen alleen met terrein, wegen en gebouwen (Layers.Grond), niet met zichzelf of
// met auto's. Ligt hij een paar seconden stil, dan staat hij op: de Animator gaat weer aan en de botten vloeien in ruim een seconde
// van de ligpose naar de stilstaande animatie; daarna loopt hij terug naar zijn route. Alleen wie geraakt wordt en in de buurt van
// de camera is, hooguit tien tegelijk; de rest (blokjesmensen, ver weg) ligt even zoals vroeger (downT).
public partial class PlanetLife
{
    const int MaxRags = 10; const float RagView = 220f;

    // de klap: vLegs voor heup en benen, vBody voor bovenlijf, armen en hoofd; lukt een ragdoll niet, dan downT seconden liggen
    bool Knock(Person a, Vector3 vLegs, Vector3 vBody, float downT)
    {
        a.paused = false; a.partner = null;
        var cam = Camera.main; bool near = cam == null || (cam.transform.position - a.root.position).sqrMagnitude < RagView * RagView;
        if (a.rb != null && near && Lappenpop.Make(a, vLegs, vBody)) { a.downT = Lappenpop.Marker; return true; }
        a.downT = downT; return false;
    }

    // aangereden door een auto (het verkeer in Totale chaos of de eigen auto)
    void KnockByCar(Person a, Car c)
    {
        Vector3 up = a.root.position.normalized, v = Flat(Vel(c), up); float s = Mathf.Min(v.magnitude, 22f); Vector3 dir = v.sqrMagnitude > 1e-4f ? v.normalized : Flat(c.root.forward, up).normalized;
        Vector3 d = Flat(a.root.position - c.root.position, up); d -= dir * Vector3.Dot(d, dir); Vector3 side = d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.zero;   // opzij weg van de as van de auto
        Vector3 jit = Flat(Random.insideUnitSphere, up) * 0.5f, f = dir * s;
        Knock(a, f * 1.1f + up * (0.6f + s * 0.16f) + side * s * 0.12f + jit, f * 0.78f + up * (0.9f + s * 0.3f) + side * s * 0.08f + jit, 5f);
        float k = Mathf.Min(1f, (s - 2f) / 10f); if (k > 0f) { Aanrijdgeluid.Klap(a.root.position + up * 0.8f, k); Aanrijdgeluid.Grunt(a.root.position + up * 0.9f, k, a); }   // klap op de motorkap en een kreun
    }

    // weggeblazen door een inslag of ontploffing; k: 0 (ver weg) tot 1 (er vlak naast)
    void KnockBlast(Person a, Vector3 at, float k, float downT)
    {
        Vector3 up = a.root.position.normalized, d = Flat(a.root.position - at, up); Vector3 away = d.sqrMagnitude > 0.01f ? d.normalized : Flat(Random.onUnitSphere, up).normalized;
        Vector3 v = away * Mathf.Lerp(2f, 14f, k) + up * Mathf.Lerp(1.5f, 9f, k);
        Knock(a, v * 1.1f, v * 0.9f + up * 0.6f, downT);
        if (k > 0.3f) Aanrijdgeluid.Scream(a.root.position + up * 1.5f, "horror", a); else Aanrijdgeluid.Grunt(a.root.position + up * 0.9f, 0.5f, a);
    }

    // vasthouden: ligt hij als ragdoll (dan doet PlanetLife.Update niets met hem)
    static bool Ragged(Person a) => Lappenpop.Holds(a);
}

public class Lappenpop : MonoBehaviour
{
    public const float Marker = 1e4f;   // downT zolang de ragdoll ligt (zo slaat HitPeople hem over)
    static readonly List<Lappenpop> active = new List<Lappenpop>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { active.Clear(); mat = null; }
    static PhysicsMaterial mat;
    public static int Count { get { int n = 0; foreach (var l in active) if (l != null && l.phys) n++; return n; } }
    public static bool Holds(Person a) { foreach (var l in active) if (l != null && l.phys && l.who == a) return true; return false; }

    Person who; Animator anim; bool phys;
    readonly List<Rigidbody> bodies = new List<Rigidbody>(); readonly List<bool> legs = new List<bool>(); readonly List<Vector3> prevV = new List<Vector3>();
    readonly List<Object> parts = new List<Object>();   // joints, botsvormen en Rigidbodies, om weer weg te halen
    Transform hips, head; Transform[] bones; Quaternion[] saved; Vector3 savedHips; int hipsIdx = -1;
    float t, stillT, blendT = -1f;
    const float StillV = 0.35f, StillFor = 2.5f, MaxLie = 12f, Blend = 1.2f;

    public static bool Make(Person a, Vector3 vLegs, Vector3 vBody)
    {
        if (a.rb == null) return false;
        var l = a.rb.GetComponent<Lappenpop>();
        if (l != null) { if (l.phys) { l.Kick(vLegs, vBody, true); return true; } DestroyImmediate(l); }   // al aan het opstaan: opnieuw neer
        if (Count >= 10) return false;
        l = a.rb.gameObject.AddComponent<Lappenpop>();
        if (!l.Build(a)) { DestroyImmediate(l); return false; }
        l.Kick(vLegs, vBody, false); return true;
    }

    bool Build(Person a)
    {
        anim = a.rb.Anim; if (anim == null || !anim.isHuman) return false;
        Transform B(HumanBodyBones h) => anim.GetBoneTransform(h);
        hips = B(HumanBodyBones.Hips); head = B(HumanBodyBones.Head);
        Transform chest = B(HumanBodyBones.UpperChest); if (chest == null) chest = B(HumanBodyBones.Chest); if (chest == null) chest = B(HumanBodyBones.Spine);
        Transform lua = B(HumanBodyBones.LeftUpperArm), lla = B(HumanBodyBones.LeftLowerArm), lh = B(HumanBodyBones.LeftHand);
        Transform rua = B(HumanBodyBones.RightUpperArm), rla = B(HumanBodyBones.RightLowerArm), rh = B(HumanBodyBones.RightHand);
        Transform lul = B(HumanBodyBones.LeftUpperLeg), lll = B(HumanBodyBones.LeftLowerLeg), lf = B(HumanBodyBones.LeftFoot);
        Transform rul = B(HumanBodyBones.RightUpperLeg), rll = B(HumanBodyBones.RightLowerLeg), rf = B(HumanBodyBones.RightFoot);
        foreach (var x in new[] { hips, head, chest, lua, lla, lh, rua, rla, rh, lul, lll, lf, rul, rll, rf }) if (x == null) return false;
        bones = GetComponentsInChildren<Transform>();   // voor het opstaan (zonder de botsvormen)
        for (int i = 0; i < bones.Length; i++) if (bones[i] == hips) hipsIdx = i;
        who = a; anim.enabled = false;   // de pose van nu blijft staan; vanaf hier beweegt de fysica de botten
        if (mat == null) mat = new PhysicsMaterial("lappenpop") { dynamicFriction = 0.75f, staticFriction = 0.9f, bounciness = 0.05f, frictionCombine = PhysicsMaterialCombine.Average, bounceCombine = PhysicsMaterialCombine.Minimum };
        Transform root = a.root; Vector3 up = root.up, fw = root.forward, rt = root.right;
        float sf = Mathf.Clamp(Vector3.Distance(hips.position, head.position) / 0.62f, 0.5f, 1.4f);   // grootte (een kind is kleiner)
        float mk = sf * sf * sf;
        Vector3 shoulders = (lua.position + rua.position) * 0.5f;
        Vector3 Out(Transform from, Transform to, float extra) => to.position + (to.position - from.position).normalized * extra;

        var rHips = Body(hips, 12f * mk, true); Capsule(hips, lul.position - rt * 0.06f * sf, rul.position + rt * 0.06f * sf, 0.12f * sf);
        var rChest = Body(chest, 16f * mk, false); Capsule(chest, Vector3.Lerp(hips.position, shoulders, 0.3f), shoulders - up * 0.04f * sf, 0.14f * sf);
        var rHead = Body(head, 5f * mk, false); Sphere(head, head.position + up * 0.09f * sf, 0.11f * sf);
        Joint(chest, rHips, rt, fw, -25f, 30f, 15f, 15f);
        Joint(head, rChest, rt, fw, -40f, 25f, 25f, 30f);
        foreach (var (ua, la, hand) in new[] { (lua, lla, lh), (rua, rla, rh) })
        {
            var u = Body(ua, 2.5f * mk, false); Capsule(ua, ua.position, la.position, 0.05f * sf); Joint(ua, rChest, rt, fw, -60f, 100f, 70f, 40f);
            var l = Body(la, 2f * mk, false); Capsule(la, la.position, Out(la, hand, 0.09f * sf), 0.045f * sf); Joint(la, u, rt, fw, -10f, 120f, 5f, 5f);   // elleboog: de hand gaat naar voren
        }
        foreach (var (ul, ll, foot) in new[] { (lul, lll, lf), (rul, rll, rf) })
        {
            var u = Body(ul, 8f * mk, true); Capsule(ul, ul.position, ll.position, 0.075f * sf); Joint(ul, rHips, rt, fw, -20f, 90f, 30f, 20f);
            var l = Body(ll, 5f * mk, true); Capsule(ll, ll.position, Out(ll, foot, 0.02f * sf), 0.055f * sf); Joint(ll, u, rt, fw, -110f, 12f, 3f, 3f);   // knie: de voet gaat naar achteren
        }
        phys = true; t = 0f; stillT = 0f; active.Add(this);
        return true;
    }

    Rigidbody Body(Transform b, float mass, bool leg)
    {
        var r = b.gameObject.AddComponent<Rigidbody>(); r.mass = mass; r.useGravity = false; r.linearDamping = 0.05f; r.angularDamping = 0.6f;
        r.interpolation = RigidbodyInterpolation.Interpolate; r.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        r.excludeLayers = ~Layers.Grond; r.includeLayers = 0; r.maxDepenetrationVelocity = 3f; r.solverIterations = 12; r.solverVelocityIterations = 4;
        bodies.Add(r); legs.Add(leg); prevV.Add(Vector3.zero); return r;
    }
    // botsvormen op een eigen kindobject, zo gedraaid als het lichaamsdeel in de wereld (de assen van de botten zijn willekeurig)
    void Capsule(Transform b, Vector3 a, Vector3 z, float r)
    {
        var g = Part(b, (a + z) * 0.5f, z - a); var c = g.AddComponent<CapsuleCollider>(); float ls = Mathf.Max(1e-5f, g.transform.lossyScale.x);
        c.direction = 1; c.radius = r / ls; c.height = (Vector3.Distance(a, z) + 2f * r) / ls; c.sharedMaterial = mat;
    }
    void Sphere(Transform b, Vector3 at, float r)
    {
        var g = Part(b, at, b.up); var c = g.AddComponent<SphereCollider>(); c.radius = r / Mathf.Max(1e-5f, g.transform.lossyScale.x); c.sharedMaterial = mat;
    }
    GameObject Part(Transform b, Vector3 at, Vector3 axis)
    {
        var g = new GameObject("botsvorm"); g.layer = b.gameObject.layer; g.transform.SetParent(b, false);
        g.transform.SetPositionAndRotation(at, Quaternion.FromToRotation(Vector3.up, axis.sqrMagnitude > 1e-8f ? axis : Vector3.up));
        parts.Add(g); return g;
    }
    // twist om de as (rt: voor- en achterover), swing1 om de swingas (fw: zijwaarts), swing2 om de derde as (draaien)
    void Joint(Transform b, Rigidbody to, Vector3 twist, Vector3 swing, float lo, float hi, float s1, float s2)
    {
        var j = b.gameObject.AddComponent<CharacterJoint>(); j.connectedBody = to; j.anchor = Vector3.zero;
        j.axis = b.InverseTransformDirection(twist).normalized; j.swingAxis = b.InverseTransformDirection(swing).normalized;
        j.lowTwistLimit = new SoftJointLimit { limit = lo }; j.highTwistLimit = new SoftJointLimit { limit = hi };
        j.swing1Limit = new SoftJointLimit { limit = s1 }; j.swing2Limit = new SoftJointLimit { limit = s2 };
        j.enableProjection = true; j.enablePreprocessing = false; j.enableCollision = false;
        parts.Insert(0, j);   // joints eerst weghalen (een Rigidbody met een joint eraan mag niet weg)
    }

    void Kick(Vector3 vLegs, Vector3 vBody, bool add)
    {
        for (int i = 0; i < bodies.Count; i++)
        {
            var r = bodies[i]; if (r == null) continue; Vector3 v = legs[i] ? vLegs : vBody;
            r.linearVelocity = add ? r.linearVelocity + v * 0.7f : v; r.angularVelocity += Random.insideUnitSphere * 2f;
            r.WakeUp();
        }
        stillT = 0f; if (add) t = Mathf.Min(t, MaxLie - 4f);
    }

    void FixedUpdate()
    {
        if (!phys) return;
        float dt = Time.fixedDeltaTime, vmax = 0f; t += dt;
        float hard = 0f; Vector3 at = Vector3.zero;
        for (int i = 0; i < bodies.Count; i++)
        {
            var r = bodies[i]; if (r == null) continue; Vector3 v = r.linearVelocity;
            if (t > 0.05f) { float dv = Vector3.Dot(v - prevV[i], r.position.normalized); if (dv > hard) { hard = dv; at = r.position; } }   // de val plotseling gestuit: op de grond neergekomen
            prevV[i] = v;
            r.AddForce(-r.position.normalized * 9.81f, ForceMode.Acceleration); if (i < 2) vmax = Mathf.Max(vmax, v.magnitude);   // stil: heup en borst (een arm of been kan blijven trillen)
        }
        if (hard > 3.5f) Aanrijdgeluid.Plof(at, hard / 12f);   // zoals de webversie: plof bij een harde val
        stillT = vmax < StillV ? stillT + dt : 0f;
    }

    void Update()
    {
        if (!phys) return;
        if (who == null || who.root == null) { Destroy(this); return; }
        if (who.downT < Marker * 0.5f) { Strip(); anim.enabled = true; Destroy(this); return; }   // iemand anders zette hem terug (weggehaald of elders neergezet)
        if ((stillT > StillFor && t > 1.5f) || t > MaxLie) StandUp();
    }

    void Strip()
    {
        phys = false; active.Remove(this);
        foreach (var o in parts) if (o != null) DestroyImmediate(o);
        foreach (var r in bodies) if (r != null) DestroyImmediate(r);
        parts.Clear(); bodies.Clear(); legs.Clear(); prevV.Clear();
    }

    // opstaan: de ligpose bewaren, de fysica weg, de figuur op de plek van de heup zetten en de animatie weer aan
    void StandUp()
    {
        Vector3 hp = hips.position, up = hp.normalized;
        Vector3 face = head.position - hp; face -= up * Vector3.Dot(face, up);
        if (face.sqrMagnitude < 1e-4f) { face = who.root.forward; face -= up * Vector3.Dot(face, up); }
        Vector3 ground = hp - up * 0.12f; bool found = Physics.Raycast(hp + up * 1.5f, -up, out RaycastHit hit, 4f, Layers.Grond, QueryTriggerInteraction.Ignore);
        if (found) ground = hit.point;
        bool lost = !found && (hp.magnitude < Planet.R - 150f || Vector3.Distance(hp, who.root.position) > 60f);   // door de grond of heel ver weg
        saved = new Quaternion[bones.Length]; for (int i = 0; i < bones.Length; i++) saved[i] = bones[i] != null ? bones[i].rotation : Quaternion.identity;
        savedHips = hp;
        Strip();
        if (!lost)
        {
            who.root.SetPositionAndRotation(ground, Geo.Look(face.normalized, up));
            who.at = ground; who.hasAt = true; who.vel = Vector3.zero; who.lag = 0f;
            blendT = 0f;
        }
        else who.hasAt = false;   // zomaar terug op zijn route
        who.downT = 0f; who.paused = true; who.pauseT = 1.6f; who.partner = null;   // eerst even stilstaan, dan verder lopen
        anim.enabled = true;
        if (lost) Destroy(this);
    }

    // na de Animator: van de ligpose naar de animatie vloeien (ouders voor kinderen, zo staat GetComponentsInChildren)
    void LateUpdate()
    {
        if (phys || blendT < 0f || bones == null) return;
        blendT += Time.deltaTime; float k = Mathf.SmoothStep(0f, 1f, blendT / Blend);
        for (int i = 0; i < bones.Length; i++)
        {
            var b = bones[i]; if (b == null) continue;
            b.rotation = Quaternion.Slerp(saved[i], b.rotation, k);
            if (i == hipsIdx) b.position = Vector3.Lerp(savedHips, b.position, k);
        }
        if (k >= 1f) Destroy(this);
    }

    void OnDestroy()
    {
        active.Remove(this);   // (met de hele figuur weg: de onderdelen gaan vanzelf mee)
    }
}
