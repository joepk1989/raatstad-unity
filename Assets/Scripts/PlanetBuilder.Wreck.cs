using System.Collections.Generic;
using UnityEngine;

// Slopen door ertegenaan te rijden (zelf rijden, PlayerCar.cs). Elk beeld kijkt WreckTick wat er vlak voor het voertuig staat
// (PlanetCity.PropsNear, het register van PlanetCity.Wreck.cs), en breekt het af voordat de botsvorm geraakt wordt:
// - lantaarns, palen van verkeerslichten en bankjes: auto en tank rijden ze omver; ze klappen om in de rijrichting, schuiven nog
//   even mee en blijven liggen; na een halve minuut zakken ze weg. De auto verliest een beetje snelheid.
// - bomen: de tank duwt elke boom om (hij valt langzaam en dan steeds sneller, met zijn kroon op de grond). Een auto rijdt alleen
//   struiken plat en knakt een dunne berk boven de 40 km/u; tegen een loofboom of den botst hij gewoon (die zijn te stevig).
// - gebouwen: alleen de tank. Het vak gaat echt uit de stad (Clear, een module met al zijn vakken), als één stap in de undo; het
//   gebouw zakt in een stofwolk in elkaar en laat puin achter. Opslaan pas als je even niets sloopt of uitstapt (geen hapering).
// Mensen dichtbij schrikken (gillen, vlak naast een omvallende boom of instortend huis duiken ze weg), dieren vluchten
// (PlanetLife.LastIncident, waar PlanetAnimals op let).
public partial class PlanetBuilder
{
    class Fall
    {
        public Transform t; public Vector3 pivot, up, axis, slide; public Quaternion rot0; public float ang, w, maxAng, h, life, age, sink;
        public bool house, rest, ownMeshes = true; public float sinkDepth;
    }
    readonly List<Fall> falls = new List<Fall>();
    readonly List<PlanetCity.Prop> wreckNear = new List<PlanetCity.Prop>();
    Transform wreckFor; Bounds wreckBox; Rigidbody wreckRb;
    bool wreckSave; float wreckSaveT; public int WreckProps, WreckTrees, WreckHouses;   // tellers (voor een test)
    public float WreckWorstMs;   // het duurste sloopmoment in ms (voor een test)

    void WreckTick()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        FallTick(dt);
        // bewaren: als je uitstapt, of 6 s na het laatste gebouw als er niets meer getekend hoeft te worden
        if (wreckSave && !rebuilding && (!driving || Time.time - wreckSaveT > 6f)) { wreckSave = false; PlanetCity.Save(); beautyT = -99f; }
        var L = PlanetLife.I; if (!driving || L == null) return;
        var r = L.PlayerRoot; if (r == null) return;
        if (wreckFor != r) { wreckFor = r; wreckRb = r.GetComponent<Rigidbody>(); wreckBox = LocalBox(r); }
        if (wreckRb == null) return;
        Vector3 v = wreckRb.linearVelocity; float sp = v.magnitude; if (sp < 0.8f) return;
        bool tank = L.PlayerTank;
        Vector3 lv = r.InverseTransformDirection(v);
        float ahead = sp * dt * 2f + 0.35f;
        // de doos een stukje uitgerekt in de rijrichting (vooruit of achteruit)
        float zMin = wreckBox.min.z - (lv.z < 0f ? ahead : 0.05f), zMax = wreckBox.max.z + (lv.z > 0f ? ahead : 0.05f);
        float xMin = wreckBox.min.x - (lv.x < 0f ? ahead * 0.5f : 0.05f), xMax = wreckBox.max.x + (lv.x > 0f ? ahead * 0.5f : 0.05f);
        Vector3 front = r.TransformPoint(new Vector3(0f, 0f, lv.z >= 0f ? wreckBox.max.z : wreckBox.min.z));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        PlanetCity.PropsNear(Planet.Nearest(r.position), wreckNear);
        foreach (var p in wreckNear)
        {
            if (p.kind == PlanetCity.PropGebouw) continue;
            Vector3 lp = r.InverseTransformPoint(p.at);
            if (lp.y > wreckBox.max.y || lp.y + p.h < wreckBox.min.y + 0.15f) continue;   // eronder (een brug) of erboven
            bool crown = tank && p.crownR > 0f && lp.y + p.crownY < wreckBox.max.y;   // de koepel van de tank raakt de kroon
            float rr = crown ? Mathf.Max(p.r, p.crownR) : p.r;
            float cx = Mathf.Clamp(lp.x, xMin, xMax), cz = Mathf.Clamp(lp.z, zMin, zMax);
            if ((cx - lp.x) * (cx - lp.x) + (cz - lp.z) * (cz - lp.z) > rr * rr) continue;
            if (!CanBreak(p.kind, tank, sp)) continue;
            Knock(p, v, tank);
        }
        IntoBuildings(r, v, sp, front, tank);
        float ms = (float)sw.Elapsed.TotalMilliseconds; if (ms > WreckWorstMs) WreckWorstMs = ms;
    }

    static bool CanBreak(byte kind, bool tank, float sp)
    {
        if (tank) return true;
        switch (kind)
        {
            case PlanetCity.PropPaal: case PlanetCity.PropBank: case PlanetCity.PropStruik: return sp > 1.5f;
            case PlanetCity.PropBerk: return sp > 11f;   // boven de 40 km/u knakt een dunne berk
            default: return false;   // loofboom en den: de auto botst ertegen
        }
    }

    // de botsvorm van het voertuig (alle dozen samen), in zijn eigen assen
    static Bounds LocalBox(Transform r)
    {
        var b = new Bounds(new Vector3(0f, 0.9f, 0f), new Vector3(2f, 1.8f, 5f)); bool any = false;
        foreach (var bc in r.GetComponents<BoxCollider>())
        {
            var bb = new Bounds(bc.center, bc.size); if (!any) { b = bb; any = true; } else b.Encapsulate(bb);
        }
        b.Encapsulate(new Vector3(b.center.x, 0f, b.center.z));   // tot de grond: de wielen en rupsen raken een paaltje ook
        return b;
    }

    // ---------- een paal, bankje of boom omver ----------
    void Knock(PlanetCity.Prop p, Vector3 v, bool tank)
    {
        var t = PlanetCity.Break(p); if (t == null) return;
        bool tree = p.kind >= PlanetCity.PropStruik && p.kind <= PlanetCity.PropDen;
        Vector3 up = p.up, dir = v - up * Vector3.Dot(v, up); float sp = dir.magnitude; dir = sp > 0.01f ? dir / sp : t.forward;
        dir = (dir + Vector3.Cross(up, dir) * Random.Range(-0.25f, 0.25f)).normalized;
        var f = new Fall { t = t, pivot = p.at, up = up, rot0 = t.rotation, axis = Vector3.Cross(up, dir).normalized, h = Mathf.Max(0.5f, p.h) };
        float mass;
        if (p.kind == PlanetCity.PropStruik) { f.maxAng = 80f; f.w = Mathf.Min(5f, sp / 1.2f); f.life = 25f; f.slide = dir * sp * 0.15f; mass = 0.01f; WreckTrees++; }
        else if (tree) { f.maxAng = Random.Range(82f, 87f); f.w = tank ? 0.35f + sp * 0.03f : 0.8f; f.life = 45f; mass = p.kind == PlanetCity.PropBerk ? 0.06f : 0.025f; WreckTrees++; }
        else { f.maxAng = 88f; f.w = Mathf.Min(7f, sp / (f.h * 0.35f)); f.slide = dir * sp * 0.55f; f.life = 25f; mass = p.kind == PlanetCity.PropBank ? 0.04f : 0.05f; WreckProps++; }
        falls.Add(f); TrimFalls();
        // het voertuig verliest wat snelheid (de tank bijna niets)
        if (wreckRb != null) wreckRb.AddForce(-v * (tank ? mass * 0.25f : mass), ForceMode.VelocityChange);
        Sounds.Fx("crunch", p.at, tree ? 0.9f : 0.6f, 14f);
        for (int k = 0; k < (tree ? 4 : 3); k++) Debris(p.at + up * 0.4f, up * Random.Range(2f, 4f) + dir * sp * 0.4f + Random.insideUnitSphere * 2f);
        if (tree && p.kind != PlanetCity.PropStruik) for (int k = 0; k < 3; k++) Dust(p.at + up * 0.3f + Random.insideUnitSphere * 0.8f, up * 0.6f + Random.insideUnitSphere * 0.6f, Random.Range(0.8f, 1.4f), Random.Range(1.2f, 2f));
        PlanetLife.Startle(p.at, tree && p.kind != PlanetCity.PropStruik ? 3f : 0f, tree ? 25f : 15f);
    }
    void TrimFalls()
    {
        int lying = 0; for (int i = falls.Count - 1; i >= 0; i--) if (!falls[i].house && ++lying > 80 && falls[i].sink <= 0f) falls[i].sink = 0.001f;   // te veel: de oudste zakken weg
    }

    void FallTick(float dt)
    {
        for (int i = falls.Count - 1; i >= 0; i--)
        {
            var f = falls[i];
            if (f.t == null) { falls.RemoveAt(i); continue; }
            f.age += dt;
            if (f.house) { HouseStep(f, dt); if (f.t == null) { falls.RemoveAt(i); } continue; }
            if (!f.rest)
            {
                // een staaf die om zijn voet kantelt: steeds sneller (3g/2h · sin), en stuitert een keer zacht als hij ligt
                f.w += 1.5f * 9.81f / f.h * Mathf.Sin(f.ang * Mathf.Deg2Rad) * dt;
                f.ang += f.w * Mathf.Rad2Deg * dt;
                if (f.ang >= f.maxAng) { f.ang = f.maxAng; if (Mathf.Abs(f.w) > 1.2f) { f.w = -f.w * 0.18f; if (f.h > 4f) Sounds.Fx("crunch", f.pivot, 0.5f, 20f); } else { f.w = 0f; f.rest = true; } }
                if (f.ang < 0f) { f.ang = 0f; f.w = Mathf.Abs(f.w); }
            }
            // meegesleept: glijdt nog even door over de grond
            float s = f.slide.magnitude; if (s > 0.01f) { f.pivot += f.slide * dt; f.slide = f.slide * Mathf.Max(0f, 1f - 6f * dt / Mathf.Max(0.3f, s)); }
            if (f.age > f.life && f.sink <= 0f) f.sink = 0.001f;
            Vector3 pos = f.pivot;
            if (f.sink > 0f) { f.sink += dt; pos -= f.up * f.sink * 1.2f; if (f.sink > 2f) { DropFall(f); falls.RemoveAt(i); continue; } }
            f.t.SetPositionAndRotation(pos, Quaternion.AngleAxis(f.ang, f.axis) * f.rot0);
        }
    }
    static void DropFall(Fall f)
    {
        if (f.t == null) return;
        if (f.ownMeshes) foreach (var mf in f.t.GetComponentsInChildren<MeshFilter>()) if (mf.sharedMesh != null) Destroy(mf.sharedMesh);
        Destroy(f.t.gameObject);
    }

    // ---------- de tank rijdt een gebouw in (en auto en tank een hek of haag) ----------
    void IntoBuildings(Transform r, Vector3 v, float sp, Vector3 front, bool tank)
    {
        if (sp < 1.2f) return;
        int c0 = Planet.Nearest(front); if (c0 < 0) return;
        float alt = r.position.magnitude - Planet.R;
        Vector3 lv = r.InverseTransformDirection(v); bool fwd = lv.z >= 0f;
        float ahead = sp * Time.deltaTime * 2f + 0.6f;
        TryHouse(c0, r, v, alt, fwd, ahead, tank);
        foreach (int j in Planet.Cells[c0].nb) TryHouse(j, r, v, alt, fwd, ahead, tank);
    }
    void TryHouse(int j, Transform r, Vector3 v, float alt, bool fwd, float ahead, bool tank)
    {
        int wc = PlanetCity.WreckClass(j); if (wc == 0 || wc == 2 && !tank || wc == 1 && !tank && v.magnitude < 2f) return;
        if (Mathf.Abs(alt - PlanetCity.G(j)) > 3.5f) return;   // op een brug erover of in een geul eronder
        var c = Planet.Cells[j];
        Vector3 lp = r.InverseTransformPoint(c.dir * (Planet.R + PlanetCity.G(j)));
        float rr = c.radius * (PlanetCity.HasModule(j) ? 0.95f : 0.84f);
        float zMin = wreckBox.min.z - (fwd ? 0f : ahead), zMax = wreckBox.max.z + (fwd ? ahead : 0f);
        float cx = Mathf.Clamp(lp.x, wreckBox.min.x, wreckBox.max.x), cz = Mathf.Clamp(lp.z, zMin, zMax);
        if ((cx - lp.x) * (cx - lp.x) + (cz - lp.z) * (cz - lp.z) > rr * rr) return;
        Demolish(j, v, wc == 2);
    }

    readonly List<PlanetCity.Prop> houseProps = new List<PlanetCity.Prop>();
    void Demolish(int j, Vector3 v, bool big)
    {
        Push();
        Vector3 up = Planet.Cells[j].dir, dir = v - up * Vector3.Dot(v, up); dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.Cross(up, Vector3.up).normalized;
        Vector3 at = up * (Planet.R + PlanetCity.G(j)); float hgt = 6f, rad = Planet.Cells[j].radius;
        if (PlanetCity.HasModule(j))
        {
            var mods = PlanetCity.ModulesAt(j); hgt = 0f;
            foreach (var I in mods)
            {
                var M = I.M; float top = (I.lv + (M != null ? MV2.MetaOf(M).fl : 1)) * MV2.LV_H; hgt = Mathf.Max(hgt, top);
                var go = PlanetCity.TakeModuleObject(I);
                if (go != null) StartCollapse(go.transform, up, dir, top, false);
                PlanetCity.RemoveModule(I);
            }
            at = Planet.Cells[mods.Count > 0 ? mods[0].c : j].dir * (Planet.R + PlanetCity.G(j)); rad *= 2.2f;
        }
        else
        {
            hgt = Mathf.Max(3f, PlanetCity.StackHeight(j));
            PlanetCity.BuildingProps(j, houseProps);
            foreach (var p in houseProps) { var t = PlanetCity.Break(p, false); if (t != null) StartCollapse(t, up, dir, hgt, true); }
            PlanetCity.Clear(j);
        }
        if (!big)   // een hek of haag: wat splinters, een klap en weer door
        {
            WreckProps++;
            for (int k = 0; k < 8; k++) Debris(at + up * 0.6f, up * Random.Range(2f, 4f) + dir * Random.Range(1f, 4f) + Random.insideUnitSphere * 2f);
            Sounds.Fx("crunch", at, 0.8f, 16f); if (wreckRb != null) wreckRb.AddForce(-v * 0.05f, ForceMode.VelocityChange);
            PlanetLife.Startle(at, 0f, 15f);
            PlanetCity.UserEdit(); beautyVer++; PlanetCity.RebuildSoon(j); rebuilding = true; Sounds.Hush(); wreckSave = true; wreckSaveT = Time.time;
            return;
        }
        WreckHouses++;
        // stof, puin, een klap; het voertuig wordt even afgeremd
        for (int k = 0; k < 14; k++) { Vector3 o = Random.insideUnitSphere; o -= up * Vector3.Dot(o, up); Dust(at + o * rad * 0.9f + up * Random.Range(0.3f, hgt * 0.5f), up * Random.Range(0.8f, 2.5f) + o * 1.5f, Random.Range(2.5f, 4.5f), Random.Range(2.8f, 4.2f)); }
        for (int k = 0; k < 16; k++) Debris(at + up * Random.Range(0.5f, hgt * 0.6f), up * Random.Range(2f, 6f) + dir * Random.Range(0f, 4f) + Random.insideUnitSphere * 4f);
        Rubble(at, up, rad);
        Sounds.Fx("crunch", at, 1f, 30f); Sounds.Fx("thunder", at, 0.35f, 30f);
        if (Cam != null) { float d = Vector3.Distance(Cam.transform.position, at); if (d < 80f) Shake(Mathf.Lerp(0.35f, 0f, d / 80f)); }
        if (wreckRb != null) wreckRb.AddForce(-v * 0.22f, ForceMode.VelocityChange);
        PlanetLife.Startle(at, rad * 1.2f, 45f);
        PlanetLife.Blast(at, 1.2f);   // auto's die er vlak naast staan krijgen een duw en wat schade
        // de stad: tekenen per beeld, bewaren later (WreckTick)
        PlanetCity.UserEdit(); beautyVer++; PlanetCity.RebuildSoon(j); rebuilding = true; Sounds.Hush();
        wreckSave = true; wreckSaveT = Time.time;
        Say("Gesloopt! (Ctrl+Z na het uitstappen zet het terug)");
    }
    // bouwstof: licht zandgrijs, niet de donkere rook van een ontploffing
    void Dust(Vector3 at, Vector3 vel, float size, float life)
    {
        float k = Random.Range(0.55f, 0.75f);
        var tr = Blob("stof", Mats.Get("sloop_stof" + Mathf.RoundToInt(k * 10f), new Color(k, k * 0.93f, k * 0.82f)));
        tr.position = at; tr.localScale = Vector3.one * 0.01f;
        puffs.Add(new Puff { tr = tr, vel = vel, life = life, size = size });
    }
    void StartCollapse(Transform t, Vector3 up, Vector3 dir, float hgt, bool own)
    {
        var f = new Fall { t = t, house = true, pivot = t.position, rot0 = t.rotation, up = up, axis = Vector3.Cross(up, dir).normalized, h = hgt, life = 2.2f, ownMeshes = true, sinkDepth = hgt + 1.5f };
        falls.Add(f);
    }
    // instorten: wat schudden, kantelt een paar graden van de tank af en zakt in 2 s helemaal de grond in (achter de stofwolk)
    void HouseStep(Fall f, float dt)
    {
        float u = Mathf.Clamp01(f.age / f.life), e = u * u * (3f - 2f * u);
        Vector3 shake = Random.insideUnitSphere * 0.12f * (1f - u);
        f.t.SetPositionAndRotation(f.pivot - f.up * f.sinkDepth * e + shake, Quaternion.AngleAxis(7f * e, f.axis) * f.rot0);
        if (u >= 1f) { DropFall(f); f.t = null; }
    }
    // puin dat blijft liggen: een paar brokken op de plek van het gebouw; na een minuut zakken ze weg
    void Rubble(Vector3 at, Vector3 up, float rad)
    {
        var g = new GameObject("Puin").transform; g.SetPositionAndRotation(at, Quaternion.LookRotation(Vector3.Cross(up, Mathf.Abs(up.y) < 0.9f ? Vector3.up : Vector3.right), up));
        var mats = new[] { Mats.Get("tank_brok", new Color(0.24f, 0.2f, 0.17f)), Mats.Get("puin_steen", new Color(0.55f, 0.52f, 0.48f)), Mats.Get("puin_baksteen", new Color(0.6f, 0.34f, 0.26f)) };
        for (int k = 0; k < 14; k++)
        {
            var b = Blob("puin", mats[k % mats.Length]); b.SetParent(g, false);
            Vector2 q = Random.insideUnitCircle * rad * 0.7f; float s = Random.Range(0.5f, 1.6f);
            b.localPosition = new Vector3(q.x, s * 0.15f, q.y); b.localScale = new Vector3(s * Random.Range(1f, 1.8f), s * 0.6f, s * Random.Range(1f, 1.8f));
        }
        falls.Add(new Fall { t = g, pivot = at, up = up, rot0 = g.rotation, maxAng = 0f, rest = true, life = 60f, h = 1f, ownMeshes = false });
    }
}
