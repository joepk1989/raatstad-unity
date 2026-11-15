using System.Collections.Generic;
using UnityEngine;

// Tankgevecht (oorlogsmodus: raatmenu, startscherm, of J en dan 3): twee teams. Jouw team is jij plus 0 tot 4 tanks van de AI
// (bondgenoten, in de kleur van je eigen tank), de tegenstander 1 tot 5 tanks (roestrood); te kiezen in het menu (tankTeam, tankFoes,
// bewaard in PlayerPrefs). De tegenstanders verschijnen zo'n 140 m verderop naast elkaar, je bondgenoten naast en achter je. Elke tank
// (EnemyTank, bestuurd door TankBrain) kiest steeds de dichtstbijzijnde tank van het andere team. Schade alleen aan het andere team:
// pantser 100, een voltreffer kost 34 (drie keer raak is uit), een inslag vlakbij tot 26 (binnen 7 m). Bij 0 is een tank uitgeschakeld:
// een grote ontploffing, zwartgeblakerd, de koepel vliegt eraf, nog een paar ontploffingen na, en hij blijft branden en roken zolang het
// gevecht duurt. Ben jij uitgeschakeld terwijl je team nog rijdt, dan kijk je mee met een bondgenoot. Gewonnen als alle tegenstanders
// uit zijn, verloren als je hele team uit is. Toeschouwer (T): de AI neemt je tank over tot je weer op T drukt; met Tab volgt de camera
// een andere tank. Na afloop: N nog een keer, J stoppen.
public partial class PlanetBuilder
{
    bool battle, spectate; float playerHp, hurtT = -9f, battleEndT, retargetT; string battleResult; int battleWins, battleLosses, watchIdx;
    int tankTeam = 1, tankFoes = 1; bool battleSizeLoaded;   // jouw team (met jou) en het aantal tegenstanders, 1 tot 5
    readonly List<EnemyTank> enemies = new List<EnemyTank>(); readonly TankBrain playerBrain = new TankBrain();   // alle tanks van de AI, beide teams
    class WreckFx { public Transform root, turret; public float t, next; public float[] booms; public Light light; }
    readonly List<WreckFx> wrecks = new List<WreckFx>(); static Material burnt;
    public const float TankArmor = 100f, HitDamage = 34f, SplashR = 7f, SplashDamage = 26f;
    // voor een test
    public bool BattleOn => battle; public float BattlePlayerHp => playerHp; public List<EnemyTank> BattleEnemies => enemies; public string BattleResult => battleResult;
    public void BattleSize(int team, int foes) { battleSizeLoaded = true; tankTeam = Mathf.Clamp(team, 1, 5); tankFoes = Mathf.Clamp(foes, 1, 5); }
    public readonly List<string> FireLog = new List<string>();
    public readonly int[] BattleStats = new int[4]; public readonly float[] BattleMiss = new float[2]; public readonly int[] BattleMissN = new int[2];   // schoten en treffers (schade): jouw team, jouw team, tegenstander, tegenstander
    Transform shellOwner;   // wie de granaat schoot die nu ontploft
    public bool Spectating => spectate; public TankBrain PlayerBrain => playerBrain;
    public void BattleSpectate(bool on) { if (spectate != on) ToggleSpectate(); }
    void LoadBattleSize() { if (battleSizeLoaded) return; battleSizeLoaded = true; tankTeam = Mathf.Clamp(PlayerPrefs.GetInt("tankTeam", 1), 1, 5); tankFoes = Mathf.Clamp(PlayerPrefs.GetInt("tankFoes", 1), 1, 5); }
    void SaveBattleSize() { PlayerPrefs.SetInt("tankTeam", tankTeam); PlayerPrefs.SetInt("tankFoes", tankFoes); PlayerPrefs.Save(); }
    string BattleName { get { LoadBattleSize(); return tankTeam + " tegen " + tankFoes; } }
    string BattleSub => (tankTeam == 1 ? "Jij" : "Jij en " + (tankTeam - 1) + (tankTeam == 2 ? " bondgenoot" : " bondgenoten")) + " tegen " + tankFoes + (tankFoes == 1 ? " tank" : " tanks") + " van de AI";
    // de keuzes in het raatmenu (Oorlogsmodus)
    List<MItem> BattleMenuItems(bool space)
    {
        LoadBattleSize();
        var opts = new[] { "1", "2", "3", "4", "5" };
        return new List<MItem> {
            new MItem { name = "Tankgevecht " + BattleName, sub = BattleSub + " · T toeschouwer", danger = true,
                        act = () => { menuOpen = false; if (space) { distGoal = LandDist; battleAfterLanding = true; } else StartBattle(); } },
            new MItem { name = "Jouw team", sub = "Tanks in jouw team, jij meegeteld", opts = opts, sel = tankTeam - 1, pick = k => { tankTeam = k + 1; SaveBattleSize(); } },
            new MItem { name = "Tegenstanders", sub = "Tanks van de tegenstander", opts = opts, sel = tankFoes - 1, pick = k => { tankFoes = k + 1; SaveBattleSize(); } } };
    }

    void StartBattle()
    {
        LoadBattleSize();
        StartDrive(true); if (!driving || PlanetLife.I == null || PlanetLife.I.PlayerRoot == null) return;
        battle = true; spectate = false; watchIdx = 0; playerHp = TankArmor; battleResult = null; PlayerCar.Frozen = false; PlayerCar.Watch = null; retargetT = 0f;
        SpawnTeams();
        Say("Tankgevecht " + BattleName + ": schiet de rode tanks uit · T toeschouwer");
    }

    void EndBattle()
    {
        if (!battle) return;
        foreach (var e in enemies) if (e != null) Destroy(e.gameObject);
        enemies.Clear(); ClearWrecks(); aiBox.Clear();
        if (mode == 3) mode = 0;
        battle = spectate = false; battleResult = null; PlayerCar.Frozen = false; PlayerCar.Auto = false; PlayerCar.Watch = null;
    }

    void RestartBattle()
    {
        var L = PlanetLife.I; if (L == null || L.PlayerRoot == null) return;
        foreach (var e in enemies) if (e != null) Destroy(e.gameObject);
        enemies.Clear(); ClearWrecks(); aiBox.Clear();
        if (playerHp <= 0f)   // je tank was uitgeschakeld: een nieuwe op dezelfde plek
        {
            Transform me = L.PlayerRoot; Vector3 pos = me.position, f = me.forward;
            var root = L.AddPlayer(pos + pos.normalized * 0.5f, f, true); playerCar.Enter(root, Cam);
        }
        playerHp = TankArmor; battleResult = null; PlayerCar.Frozen = false; PlayerCar.Watch = null; watchIdx = 0; retargetT = 0f;
        if (!spectate) PlayerCar.Auto = false;
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        SpawnTeams(); Say("Nog een keer: " + BattleName);
    }

    // de tegenstanders zo'n 140 m verderop naast elkaar (16 m uit elkaar), je bondgenoten naast en iets achter je; elk op een vrije plek
    void SpawnTeams()
    {
        var L = PlanetLife.I; Transform me = L.PlayerRoot; Vector3 up = me.position.normalized, f = me.forward - up * Vector3.Dot(me.forward, up); f.Normalize();
        Vector3 side = Vector3.Cross(up, f);
        // de richting van de tegenstanders: rechtuit als daar plek is, anders een andere kant
        Vector3 dir = f;
        for (int tries = 0; tries < 12; tries++) { Vector3 d = Quaternion.AngleAxis(tries == 0 ? 0f : Random.Range(-150f, 150f), up) * f; if (Free(Ahead(up, d, 140f))) { dir = d; break; } }
        Vector3 fc = Ahead(up, dir, 140f), fu = fc.normalized, fside = Vector3.Cross(fu, dir);
        for (int i = 0; i < tankFoes; i++)
        {
            Vector3 at = FreeNear(Ahead(fu, fside, (i - (tankFoes - 1) * 0.5f) * 16f));
            Vector3 au = at.normalized, face = up - au * Vector3.Dot(up, au);
            var e = EnemyTank.Spawn(transform, at, face.sqrMagnitude > 1e-8f ? face.normalized : -dir, 1); e.fire = EnemyFire; enemies.Add(e);
        }
        for (int i = 1; i < tankTeam; i++)   // bondgenoten: om en om links en rechts van je, iets naar achteren
        {
            float s = ((i + 1) / 2) * 16f * (i % 2 == 1 ? -1f : 1f);
            Vector3 at = FreeNear(Ahead(Ahead(up, side, s).normalized, -f, 8f));
            var e = EnemyTank.Spawn(transform, at, f, 0); e.fire = EnemyFire; e.reload = 2f; enemies.Add(e);
        }
    }
    static Vector3 Ahead(Vector3 up, Vector3 dir, float dist) { dir = dir - up * Vector3.Dot(dir, up); dir = dir.sqrMagnitude > 1e-10f ? dir.normalized : Vector3.Cross(up, Vector3.right).normalized; return (up * Mathf.Cos(dist / Planet.R) + dir * Mathf.Sin(dist / Planet.R)).normalized * Planet.R; }
    static bool Free(Vector3 at)
    {
        Vector3 u = at.normalized; int c = Planet.Nearest(u * Planet.R); if (c < 0 || PlanetCity.Wet(c)) return false;
        if (PlanetCity.Cells.ContainsKey(c) && !PlanetCity.IsRoad(c)) return false;
        return !Physics.CheckSphere(u * (Planet.R + PlanetCity.TerrainAt(u) + 2.5f), 4f, (1 << Layers.Gebouw) | (1 << Layers.Mens), QueryTriggerInteraction.Ignore);
    }
    // een vrije plek bij at (in kringen eromheen zoeken), op de grond
    static Vector3 FreeNear(Vector3 at)
    {
        Vector3 u = at.normalized, t = Vector3.Cross(u, Mathf.Abs(u.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
        for (int k = 0; k < 30; k++)
        {
            Vector3 q = k == 0 ? at : Ahead(u, Quaternion.AngleAxis(k * 47f, u) * t, 6f + k * 2f);
            if (Free(q)) { Vector3 qu = q.normalized; return qu * (Planet.R + PlanetCity.TerrainAt(qu)); }
        }
        return u * (Planet.R + PlanetCity.TerrainAt(u));
    }

    void EnemyFire(EnemyTank e, Vector3 dir)
    {
        FireLog.Add($"{(e.team == 1 ? "tegenstander" : "bondgenoot")}: afwijking {e.brain.FireErr:0.0}° ({e.brain.FireInfo})");
        Shoot(e.rig, dir, e.rb != null ? e.rb.linearVelocity : Vector3.zero, e.transform);
        TankRig.Recoil(e.rb, dir);   // terugslag
    }

    // de dichtstbijzijnde tank van het andere team (team: van wie zoekt), met zijn Rigidbody
    Transform Nearest(Vector3 from, int team, out Rigidbody rb)
    {
        Transform best = null; rb = null; float bd = float.MaxValue; var L = PlanetLife.I;
        if (team == 1 && playerHp > 0f && L != null && L.PlayerRoot != null) { bd = Vector3.Distance(from, L.PlayerRoot.position); best = L.PlayerRoot; rb = best.GetComponent<Rigidbody>(); }
        foreach (var e in enemies) if (e != null && !e.Dead && e.team != team) { float d = Vector3.Distance(from, e.transform.position); if (d < bd) { bd = d; best = e.transform; rb = e.rb; } }
        return best;
    }
    int Alive(int team) { int n = team == 0 && playerHp > 0f ? 1 : 0; foreach (var e in enemies) if (e != null && !e.Dead && e.team == team) n++; return n; }

    // elk beeld (vanuit TankTick, als je in de tank zit). true: de speler bestuurt zijn tank nu niet zelf (uitgeschakeld of toeschouwer)
    bool BattleTick(PlanetLife L, float dt)
    {
        if (!battle) return false;
        Transform me = L.PlayerRoot; var myRb = me != null ? me.GetComponent<Rigidbody>() : null;
        retargetT -= dt;
        if (retargetT <= 0f) { retargetT = 0.5f; foreach (var e in enemies) if (e != null && !e.Dead) { e.target = Nearest(e.transform.position, e.team, out var trb); e.targetRb = trb; } }
        WreckTick(dt);
        foreach (var e in enemies) if (e != null && !e.Dead) AiWreck(e, dt);
        if (Inp.Down(K.T) && playerHp > 0f && battleResult == null) ToggleSpectate();
        if (Inp.Down(K.Tab))   // de camera naar een andere tank
        {
            if (!spectate && playerHp > 0f && battleResult == null) ToggleSpectate();
            var list = Watchable(L); watchIdx = (watchIdx + 1) % list.Count; PlayerCar.Watch = list[watchIdx] == me ? null : list[watchIdx];
        }
        // uitgeschakeld en de gevolgde tank is ook uit: mee met een bondgenoot die nog rijdt
        if (playerHp <= 0f && battleResult == null)
        {
            var w = PlayerCar.Watch != null ? PlayerCar.Watch.GetComponent<EnemyTank>() : null;
            if (w == null || w.Dead) { EnemyTank pick = null; foreach (var e in enemies) if (e != null && !e.Dead && e.team == 0) { pick = e; break; } if (pick != null) PlayerCar.Watch = pick.transform; }
        }
        // afgelopen?
        if (battleResult == null)
        {
            if (Alive(0) == 0) { battleResult = "Verloren"; battleLosses++; battleEndT = Time.time + 2.5f; }
            else if (Alive(1) == 0) { battleResult = "Gewonnen"; battleWins++; battleEndT = Time.time + 2.5f; }
        }
        if (battleResult != null && Time.time > battleEndT)
        {
            if (Cursor.lockState == CursorLockMode.Locked) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (Inp.Down(K.N)) RestartBattle();
        }
        if (playerHp <= 0f) return true;   // uitgeschakeld (PlayerCar.Frozen houdt de toetsen tegen)
        if (!spectate) return false;
        // de AI bestuurt je tank: dezelfde bestuurder als de andere tanks, met dezelfde herlaadtijd
        var tgt = Nearest(me.position, 0, out var tgtRb);
        playerBrain.Think(me, myRb, L.PlayerTankRig, tgt, tgtRb, dt, reload <= 0f);
        PlayerCar.Auto = true; PlayerCar.AutoThrottle = playerBrain.throttle; PlayerCar.AutoSteer = playerBrain.steer; PlayerCar.AutoHand = false;
        if (playerBrain.fire)
        {
            FireLog.Add($"jij: afwijking {playerBrain.FireErr:0.0}° ({playerBrain.FireInfo})");
            reload = EnemyTank.ReloadTime + Random.Range(0f, 0.8f); TankShots++;
            Vector3 d = playerBrain.ShotDir(L.PlayerTankRig);
            Shoot(L.PlayerTankRig, d, L.PlayerVelocity, me); L.PlayerRecoil(d);
        }
        return true;
    }

    // de tanks van de AI rijden net als jouw tank lantaarns, palen, bankjes, struiken en bomen omver (PlanetBuilder.Wreck.cs: Knock);
    // gebouwen niet (daar wijken ze voor uit, TankBrain)
    readonly Dictionary<EnemyTank, Bounds> aiBox = new Dictionary<EnemyTank, Bounds>(); public int AiWrecked;   // voor een test
    void AiWreck(EnemyTank e, float dt)
    {
        if (e.rb == null) return;
        Vector3 v = e.rb.linearVelocity; float sp = v.magnitude; if (sp < 0.8f) return;
        var r = e.transform; if (!aiBox.TryGetValue(e, out var box)) { box = LocalBox(r); aiBox[e] = box; }
        Vector3 lv = r.InverseTransformDirection(v); float ahead = sp * dt * 2f + 0.35f;
        float zMin = box.min.z - (lv.z < 0f ? ahead : 0.05f), zMax = box.max.z + (lv.z > 0f ? ahead : 0.05f);
        float xMin = box.min.x - (lv.x < 0f ? ahead * 0.5f : 0.05f), xMax = box.max.x + (lv.x > 0f ? ahead * 0.5f : 0.05f);
        PlanetCity.PropsNear(Planet.Nearest(r.position), wreckNear);
        var keep = wreckRb; wreckRb = e.rb;   // Knock remt het voertuig dat erop botst een heel klein beetje af
        foreach (var p in wreckNear)
        {
            if (p.kind == PlanetCity.PropGebouw) continue;
            Vector3 lp = r.InverseTransformPoint(p.at);
            if (lp.y > box.max.y || lp.y + p.h < box.min.y + 0.15f) continue;
            bool crown = p.crownR > 0f && lp.y + p.crownY < box.max.y;
            float rr = crown ? Mathf.Max(p.r, p.crownR) : p.r;
            float cx = Mathf.Clamp(lp.x, xMin, xMax), cz = Mathf.Clamp(lp.z, zMin, zMax);
            if ((cx - lp.x) * (cx - lp.x) + (cz - lp.z) * (cz - lp.z) > rr * rr) continue;
            Knock(p, v, true); AiWrecked++;
        }
        wreckRb = keep;
    }

    List<Transform> Watchable(PlanetLife L)
    {
        var list = new List<Transform> { L.PlayerRoot }; foreach (var e in enemies) if (e != null && e.team == 0) list.Add(e.transform);
        foreach (var e in enemies) if (e != null && e.team == 1) list.Add(e.transform); return list;
    }

    void ToggleSpectate()
    {
        spectate = !spectate;
        if (spectate) Say("Toeschouwer: de AI rijdt je tank · T zelf besturen · Tab andere tank volgen");
        else { PlayerCar.Auto = false; PlayerCar.Watch = null; watchIdx = 0; Say("Je bestuurt je tank weer zelf"); }
    }

    // schade van een inslag: een voltreffer op een tank, en de tanks in de buurt; alleen aan het andere team dan de schutter
    void BattleDamage(Vector3 p, Collider col)
    {
        if (!battle) return;
        var hit = col != null ? col.GetComponentInParent<TankRig>() : null;
        var L = PlanetLife.I;
        var shooter = shellOwner != null ? shellOwner.GetComponent<EnemyTank>() : null;
        int team = shooter != null ? shooter.team : shellOwner != null ? 0 : -1;   // -1: onbekend, raakt iedereen
        bool byEnemy = team == 1; float dealt = 0f;
        if (team >= 0) { var foe = Nearest(p, team, out _); if (foe != null) { BattleMiss[byEnemy ? 1 : 0] += Vector3.Distance(foe.position, p); BattleMissN[byEnemy ? 1 : 0]++; } }   // voor een test
        foreach (var e in enemies)
        {
            if (e == null || e.Dead || e.team == team) continue;
            float d = Harm(e.transform, hit, p); if (d <= 0f) continue;
            e.hp -= d; dealt += d; if (e.rb != null) e.rb.AddForce((e.transform.position - p).normalized * d * 0.012f, ForceMode.VelocityChange);   // een duw (bij het zwaartepunt: geen kanteling)
            if (e.Dead) { e.hp = 0f; KnockOut(e.transform, e.rig); }
        }
        if (L != null && L.PlayerRoot != null && playerHp > 0f && team != 0)
        {
            float d = Harm(L.PlayerRoot, hit, p);
            if (d > 0f)
            {
                playerHp -= d; dealt += d; hurtT = Time.time; Shake(0.7f);
                if (playerHp <= 0f)
                {
                    playerHp = 0f; KnockOut(L.PlayerRoot, L.PlayerTankRig); PlayerCar.Frozen = true; PlayerCar.Auto = false; spectate = false;
                    if (Alive(0) > 0) Say("Uitgeschakeld: je kijkt mee met je team · Tab andere tank");
                }
            }
        }
        if (dealt > 0f) BattleStats[byEnemy ? 3 : 1]++;
    }
    static float Harm(Transform t, TankRig hit, Vector3 p)
    {
        if (hit != null && hit.transform == t) return HitDamage;
        float d = Vector3.Distance(p, t.position + t.up * 1.4f); return d < SplashR ? SplashDamage * (1f - d / SplashR) : 0f;
    }

    // uitgeschakeld: een grote ontploffing, zwartgeblakerd, de koepel vliegt eraf, nog een paar ontploffingen na, en hij blijft branden
    // en roken zolang het gevecht duurt
    void KnockOut(Transform root, TankRig rig)
    {
        if (root == null) return;
        Vector3 p = root.position + root.up * 2f, up = root.position.normalized;
        Burst(p, up, 3.4f, 14, true);
        for (int k = 0; k < 16; k++) Smoke(p + Random.insideUnitSphere * 2f, up * Random.Range(2f, 6f) + Random.insideUnitSphere * 2.5f, Random.Range(2.2f, 3.8f), Random.Range(3f, 5.5f));
        for (int k = 0; k < 20; k++) Debris(p, up * Random.Range(6f, 16f) + Random.insideUnitSphere * 9f);
        PlayAt(BoomClip(), p, 1f); PlayAt(BoomClip(), p, 0.9f);
        if (Cam != null && Vector3.Distance(Cam.transform.position, p) < 90f) Shake(0.9f);
        if (burnt == null) { burnt = new Material(Shader.Find("Universal Render Pipeline/Lit")); burnt.SetColor("_BaseColor", new Color(0.09f, 0.085f, 0.08f)); burnt.SetFloat("_Smoothness", 0.1f); }
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true)) { var ms = r.sharedMaterials; for (int k = 0; k < ms.Length; k++) ms[k] = burnt; r.sharedMaterials = ms; }
        var fx = new WreckFx { root = root };
        // de koepel (met het kanon) vliegt eraf: een los stuk met zwaartekracht naar het middelpunt, dat op de grond blijft liggen
        if (rig != null && rig.turret != null)
        {
            var tur = rig.turret; tur.SetParent(transform, true); tur.gameObject.layer = 0;
            var bc = tur.gameObject.AddComponent<BoxCollider>(); bc.center = tur.InverseTransformPoint(root.TransformPoint(rig.TopBox.center)); bc.size = rig.TopBox.size * 0.9f;
            foreach (var c in root.GetComponents<Collider>()) Physics.IgnoreCollision(bc, c);   // niet tegen de romp eronder
            var trb = tur.gameObject.AddComponent<Rigidbody>(); trb.mass = 6000f; trb.useGravity = false; trb.interpolation = RigidbodyInterpolation.Interpolate; trb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var rootRb = root.GetComponent<Rigidbody>();
            trb.linearVelocity = (rootRb != null ? rootRb.linearVelocity : Vector3.zero) + up * Random.Range(11f, 15f) + Random.insideUnitSphere * 3f;
            trb.angularVelocity = Random.insideUnitSphere * 4f;
            tur.gameObject.AddComponent<PlanetFall>(); fx.turret = tur;
        }
        // een oranje gloed van het vuur
        var lg = new GameObject("brand"); lg.transform.SetParent(root, false); lg.transform.localPosition = new Vector3(0f, 2.2f, 0f);
        var li = lg.AddComponent<Light>(); li.type = LightType.Point; li.color = new Color(1f, 0.5f, 0.2f); li.range = 16f; li.intensity = 3f; fx.light = li;
        fx.booms = new[] { Random.Range(0.6f, 1.1f), Random.Range(1.6f, 2.4f), Random.Range(3.2f, 4.5f) };
        wrecks.Add(fx);
    }

    void WreckTick(float dt)
    {
        for (int i = wrecks.Count - 1; i >= 0; i--)
        {
            var w = wrecks[i]; if (w.root == null) { if (w.turret != null) Destroy(w.turret.gameObject); wrecks.RemoveAt(i); continue; }
            float t0 = w.t; w.t += dt; w.next -= dt;
            Vector3 up = w.root.position.normalized, p = w.root.position + w.root.up * 2f;
            // nog een paar ontploffingen na (de munitie)
            foreach (float b in w.booms) if (t0 < b && w.t >= b)
            {
                Burst(p + Random.insideUnitSphere * 0.8f, up, Random.Range(1.4f, 2.2f), 7, true);
                for (int k = 0; k < 6; k++) Debris(p, up * Random.Range(5f, 11f) + Random.insideUnitSphere * 6f);
                PlayAt(BoomClip(), p, 0.7f);
            }
            if (w.light != null) w.light.intensity = (w.t < 20f ? 3f : 1.6f) * (0.7f + 0.3f * Mathf.PerlinNoise(w.t * 6f, i));   // flakkeren
            if (w.next > 0f) continue; w.next = w.t < 20f ? 0.14f : 0.3f;
            Smoke(p + w.root.up * 0.4f + Random.insideUnitSphere * 0.8f, up * Random.Range(2.5f, 4.5f) + Random.insideUnitSphere, Random.Range(1.6f, 2.8f), Random.Range(3.5f, 5f));
            if (Random.value < (w.t < 20f ? 0.5f : 0.25f)) Burst(p, up, Random.Range(0.5f, 0.9f), 2, true);   // vlammen
            if (w.turret != null && Random.value < 0.3f) Smoke(w.turret.position + up * 0.6f, up * Random.Range(1.5f, 3f), Random.Range(1f, 1.6f), Random.Range(2.5f, 3.5f));
        }
    }
    void ClearWrecks() { foreach (var w in wrecks) if (w.turret != null) Destroy(w.turret.gameObject); wrecks.Clear(); }

    // ---- de interface: je pantser en de stand (linksonder), markeringen bij de andere tanks (rood de tegenstanders, cyaan je team),
    // een rode flits als je geraakt wordt, de uitslag
    void BattleGui(float W, float H)
    {
        if (!battle || Cam == null) return;
        var L = PlanetLife.I;
        float ht = Time.time - hurtT; if (ht < 0.45f) { var o = GUI.color; GUI.color = new Color(1f, 0.15f, 0.1f, 0.3f * (1f - ht / 0.45f)); GUI.DrawTexture(new Rect(0f, 0f, W, H), Texture2D.whiteTexture); GUI.color = o; }
        // pantser: tien zeshoekjes; de stand: hoeveel tanks er in elk team nog rijden
        var r = new Rect(G, H - G - 112f, 250f, 112f); Ui.Panel(r, false, 14f);
        Ui.Label(new Rect(r.x + 14f, r.y + 8f, 200f, 20f), "PANTSER", Ui.T.Tiny, Ui.InkDim, TextAnchor.MiddleLeft, 10);
        Ui.Label(new Rect(r.x + 14f, r.y + 8f, r.width - 28f, 20f), $"gewonnen {battleWins} · verloren {battleLosses}", Ui.T.Tiny, Ui.InkDim, TextAnchor.MiddleRight, 10);
        int lit = Mathf.CeilToInt(playerHp / TankArmor * 10f - 1e-3f);
        Color hc = playerHp > 50f ? new Color(0.25f, 0.95f, 1f) : playerHp > 25f ? new Color(1f, 0.7f, 0.25f) : new Color(1f, 0.3f, 0.25f);
        for (int k = 0; k < 10; k++) Ui.Tex(new Rect(r.x + 14f + k * 22f, r.y + 34f, 19f, 21f), Ui.Hex(k < lit ? hc : new Color(1f, 1f, 1f, 0.08f), k < lit ? hc : Ui.Line, 1f, true));
        Color ally = new Color(0.25f, 0.95f, 1f, 0.95f), foeC = new Color(1f, 0.32f, 0.25f, 0.95f), gone = new Color(1f, 1f, 1f, 0.12f);
        int a0 = Alive(0), a1 = Alive(1);
        for (int k = 0; k < tankTeam; k++) Ui.Tex(new Rect(r.x + 14f + k * 13f, r.y + 64f, 11f, 12f), Ui.Hex(k < a0 ? ally : gone, ally, 1f, true));
        Ui.Label(new Rect(r.x + 14f + 5f * 13f + 4f, r.y + 60f, 40f, 20f), "tegen", Ui.T.Tiny, Ui.InkDim, TextAnchor.MiddleCenter, 10);
        for (int k = 0; k < tankFoes; k++) Ui.Tex(new Rect(r.x + 14f + 5f * 13f + 48f + k * 13f, r.y + 64f, 11f, 12f), Ui.Hex(k < a1 ? foeC : gone, foeC, 1f, true));
        string st = spectate ? "Toeschouwer: de AI rijdt · T zelf" : playerHp <= 0f ? "Uitgeschakeld" + (a0 > 0 ? " · je kijkt mee met je team" : "") : "T: toeschouwer (de AI rijdt)";
        Ui.Label(new Rect(r.x + 14f, r.y + 84f, r.width - 28f, 20f), st, Ui.T.Tiny, spectate ? Ui.Signal : Ui.InkDim, TextAnchor.MiddleLeft, 10);
        // de andere tanks: boven de tank een zeshoek met zijn pantser, buiten beeld (alleen tegenstanders) een zeshoek aan de rand
        foreach (var e in enemies)
        {
            if (e == null || (e.Dead && e.team == 0)) continue;
            Vector3 wp = e.transform.position + e.transform.up * 4.8f; float dist = L != null && L.PlayerRoot != null ? Vector3.Distance(L.PlayerRoot.position, e.transform.position) : 0f;
            Color ec = e.Dead ? new Color(0.6f, 0.6f, 0.6f, 0.7f) : e.team == 0 ? ally : foeC;
            Vector3 sp = Cam.WorldToScreenPoint(wp); Vector2 q = new Vector2(sp.x / Ui.S, (Screen.height - sp.y) / Ui.S);
            bool onScreen = sp.z > 0f && q.x > 30f && q.x < W - 30f && q.y > 30f && q.y < H - 30f;
            if (!onScreen)
            {
                if (e.team == 0 || e.Dead) continue;
                Vector3 lp = Cam.transform.InverseTransformPoint(wp); Vector2 d2 = new Vector2(lp.x, -lp.y); if (d2.sqrMagnitude < 1e-6f) d2 = Vector2.down; d2.Normalize();
                float t = Mathf.Min((W * 0.5f - 44f) / Mathf.Max(1e-3f, Mathf.Abs(d2.x)), (H * 0.5f - 44f) / Mathf.Max(1e-3f, Mathf.Abs(d2.y)));
                q = new Vector2(W * 0.5f, H * 0.5f) + d2 * t;
                Ui.Tex(new Rect(q.x - 11f, q.y - 12f, 22f, 24f), Ui.Hex(ec, ec, 1f, true));
            }
            else Ui.Tex(new Rect(q.x - 13f, q.y - 14f, 26f, 28f), Ui.Hex(new Color(0f, 0f, 0f, 0f), ec, 2.5f, true));
            if (!e.Dead) { int el = Mathf.CeilToInt(e.hp / TankArmor * 5f - 1e-3f); for (int k = 0; k < 5; k++) Ui.Tex(new Rect(q.x - 27f + k * 11f, q.y - 28f, 9f, 10f), Ui.Hex(k < el ? ec : new Color(1f, 1f, 1f, 0.1f), ec, 1f, true)); }
            Ui.Label(new Rect(q.x - 60f, q.y + 15f, 120f, 18f), e.Dead ? "uitgeschakeld" : $"{dist:0} m", Ui.T.Tiny, ec, TextAnchor.MiddleCenter, 11);
        }
        // toeschouwer: wat de AI van de gevolgde tank doet
        if (spectate || playerHp <= 0f)
        {
            var w = PlayerCar.Watch != null ? PlayerCar.Watch.GetComponent<EnemyTank>() : null;
            string who = w != null ? (w.team == 0 ? "Je team: " : "Tegenstander: ") + w.brain.state : "Jouw tank (AI): " + playerBrain.state;
            string t = who + "  ·  Tab andere tank" + (spectate ? "  ·  T zelf besturen" : ""); float tw = Ui.Width(t, Ui.T.Dim) + 40f;
            var br = new Rect((W - tw) * 0.5f, G, tw, 36f); Ui.Panel(br, false, 18f); Ui.Label(br, t, Ui.T.Dim, null, TextAnchor.MiddleCenter);
        }
        // de uitslag
        if (battleResult != null && Time.time > battleEndT)
        {
            var pr = new Rect((W - 360f) * 0.5f, (H - 170f) * 0.5f, 360f, 170f); Ui.Panel(pr, false, 16f);
            Ui.Label(new Rect(pr.x, pr.y + 14f, pr.width, 50f), battleResult, Ui.T.Title, battleResult == "Gewonnen" ? Ui.Signal : new Color(1f, 0.45f, 0.4f), TextAnchor.MiddleCenter, 34);
            Ui.Label(new Rect(pr.x, pr.y + 62f, pr.width, 20f), $"{BattleName} · gewonnen {battleWins} · verloren {battleLosses}", Ui.T.Dim, Ui.InkDim, TextAnchor.MiddleCenter);
            if (Ui.Button(new Rect(pr.x + 16f, pr.y + 104f, 158f, 46f), "N  Nog een keer")) RestartBattle();
            if (Ui.Button(new Rect(pr.x + 186f, pr.y + 104f, 158f, 46f), "J  Stoppen")) StopDrive();
        }
    }
}

// een los stuk (de koepel van een uitgeschakelde tank): zwaartekracht naar het middelpunt van de planeet
public class PlanetFall : MonoBehaviour
{
    Rigidbody rb;
    void Awake() { rb = GetComponent<Rigidbody>(); }
    void FixedUpdate() { if (rb != null) rb.AddForce(-rb.position.normalized * 9.81f, ForceMode.Acceleration); }
}
