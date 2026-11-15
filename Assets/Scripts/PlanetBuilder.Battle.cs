using System.Collections.Generic;
using UnityEngine;

// Tankgevecht (J, dan 3): jij in de tank tegen een tank van de tegenstander (EnemyTank, roestrood) die zo'n 140 m verderop verschijnt.
// Elke tank heeft 100 pantser; een voltreffer kost 34 (drie keer raak is uit), een inslag vlakbij tot 26 (binnen 7 m, minder naarmate
// verder weg). Bij 0 is de tank uitgeschakeld: een grote ontploffing, zwartgeblakerd, de koepel vliegt eraf, nog een paar ontploffingen
// na, en hij blijft branden en roken zolang het gevecht duurt. Toeschouwer (T): de AI (TankBrain, dezelfde als die van de tegenstander) neemt je tank over tot je weer op T drukt; met Tab volgt
// de camera een andere tank. Zo kun je de AI ook tegen zichzelf zien vechten. Na afloop: N nog een keer, J stoppen.
public partial class PlanetBuilder
{
    bool battle, spectate; float playerHp, hurtT = -9f, battleEndT; string battleResult; int battleWins, battleLosses, watchIdx;
    readonly List<EnemyTank> enemies = new List<EnemyTank>(); readonly TankBrain playerBrain = new TankBrain();
    class WreckFx { public Transform root, turret; public float t, next; public float[] booms; public Light light; }
    readonly List<WreckFx> wrecks = new List<WreckFx>(); static Material burnt;
    public const float TankArmor = 100f, HitDamage = 34f, SplashR = 7f, SplashDamage = 26f;
    // voor een test
    public bool BattleOn => battle; public float BattlePlayerHp => playerHp; public List<EnemyTank> BattleEnemies => enemies; public string BattleResult => battleResult;
    public readonly List<string> FireLog = new List<string>();
    public readonly int[] BattleStats = new int[4]; public readonly float[] BattleMiss = new float[2]; public readonly int[] BattleMissN = new int[2];   // schoten en treffers (schade): jij, jij, tegenstander, tegenstander
    Transform shellOwner;   // wie de granaat schoot die nu ontploft
    public bool Spectating => spectate; public TankBrain PlayerBrain => playerBrain;
    public void BattleSpectate(bool on) { if (spectate != on) ToggleSpectate(); }

    void StartBattle()
    {
        StartDrive(true); if (!driving || PlanetLife.I == null || PlanetLife.I.PlayerRoot == null) return;
        battle = true; spectate = false; watchIdx = 0; playerHp = TankArmor; battleResult = null; PlayerCar.Frozen = false; PlayerCar.Watch = null;
        SpawnEnemy();
        Say("Tankgevecht: zoek de rode tank en schiet hem uit · T toeschouwer");
    }

    void EndBattle()
    {
        if (!battle) return;
        foreach (var e in enemies) if (e != null) Destroy(e.gameObject);
        enemies.Clear(); ClearWrecks();
        if (mode == 3) mode = 0;
        battle = spectate = false; battleResult = null; PlayerCar.Frozen = false; PlayerCar.Auto = false; PlayerCar.Watch = null;
    }

    void RestartBattle()
    {
        var L = PlanetLife.I; if (L == null || L.PlayerRoot == null) return;
        foreach (var e in enemies) if (e != null) Destroy(e.gameObject);
        enemies.Clear(); ClearWrecks();
        if (playerHp <= 0f)   // je tank was uitgeschakeld: een nieuwe op dezelfde plek
        {
            Transform me = L.PlayerRoot; Vector3 pos = me.position, f = me.forward;
            var root = L.AddPlayer(pos + pos.normalized * 0.5f, f, true); playerCar.Enter(root, Cam);
        }
        playerHp = TankArmor; battleResult = null; PlayerCar.Frozen = false; PlayerCar.Watch = null; watchIdx = 0;
        if (!spectate) PlayerCar.Auto = false;
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        SpawnEnemy(); Say("Nog een keer: zoek de rode tank");
    }

    // de tegenstander: zo'n 140 m verderop, op een vrije plek (geen water, geen gebouw), met zijn neus naar je toe
    void SpawnEnemy()
    {
        var L = PlanetLife.I; Transform me = L.PlayerRoot; Vector3 up = me.position.normalized, f = me.forward - up * Vector3.Dot(me.forward, up); f.Normalize();
        Vector3 at = Vector3.zero, dir = f; bool ok = false;
        for (int tries = 0; tries < 40 && !ok; tries++)
        {
            float a = tries == 0 ? 0f : Random.Range(-150f, 150f), dist = Random.Range(125f, 160f);
            dir = Quaternion.AngleAxis(a, up) * f;
            Vector3 u = (up * Mathf.Cos(dist / Planet.R) + dir * Mathf.Sin(dist / Planet.R)).normalized;
            int c = Planet.Nearest(u * Planet.R); if (c < 0 || PlanetCity.Wet(c)) continue;
            if (PlanetCity.Cells.ContainsKey(c) && !PlanetCity.IsRoad(c)) continue;
            Vector3 g = u * (Planet.R + PlanetCity.TerrainAt(u) + 2.5f);
            if (Physics.CheckSphere(g, 4f, 1 << Layers.Gebouw, QueryTriggerInteraction.Ignore)) continue;
            at = u * (Planet.R + PlanetCity.TerrainAt(u)); ok = true;
        }
        if (!ok) at = (up * Mathf.Cos(140f / Planet.R) + f * Mathf.Sin(140f / Planet.R)).normalized * Planet.R;
        Vector3 au = at.normalized, back = up - au * Vector3.Dot(up, au);
        var e = EnemyTank.Spawn(transform, at, back.sqrMagnitude > 1e-8f ? back.normalized : -dir);
        e.fire = EnemyFire; enemies.Add(e);
    }

    void EnemyFire(EnemyTank e, Vector3 dir)
    {
        FireLog.Add($"tegenstander: afwijking {e.brain.FireErr:0.0}° ({e.brain.FireInfo})");
        Shoot(e.rig, dir, e.rb != null ? e.rb.linearVelocity : Vector3.zero, e.transform);
        TankRig.Recoil(e.rb, dir);   // terugslag
    }

    // elk beeld (vanuit TankTick, als je in de tank zit). true: de speler bestuurt zijn tank nu niet zelf (uitgeschakeld of toeschouwer)
    bool BattleTick(PlanetLife L, float dt)
    {
        if (!battle) return false;
        Transform me = L.PlayerRoot; var myRb = me != null ? me.GetComponent<Rigidbody>() : null;
        foreach (var e in enemies) if (e != null) { e.target = playerHp > 0f ? me : null; e.targetRb = myRb; }
        WreckTick(dt);
        if (Inp.Down(K.T) && playerHp > 0f && battleResult == null) ToggleSpectate();
        if (Inp.Down(K.Tab))   // de camera naar een andere tank (alleen als toeschouwer)
        {
            if (!spectate && playerHp > 0f && battleResult == null) ToggleSpectate();
            var list = Watchable(L); watchIdx = (watchIdx + 1) % list.Count; PlayerCar.Watch = list[watchIdx] == me ? null : list[watchIdx];
        }
        // afgelopen?
        if (battleResult == null)
        {
            if (playerHp <= 0f) { battleResult = "Verloren"; battleLosses++; battleEndT = Time.time + 2.5f; }
            else if (enemies.TrueForAll(x => x == null || x.Dead)) { battleResult = "Gewonnen"; battleWins++; battleEndT = Time.time + 2.5f; }
        }
        if (battleResult != null && Time.time > battleEndT)
        {
            if (Cursor.lockState == CursorLockMode.Locked) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (Inp.Down(K.N)) RestartBattle();
        }
        if (playerHp <= 0f) return true;   // uitgeschakeld (PlayerCar.Frozen houdt de toetsen tegen)
        if (!spectate) return false;
        // de AI bestuurt je tank: dezelfde bestuurder als die van de tegenstander, met dezelfde herlaadtijd
        EnemyTank tgt = null; float bd = float.MaxValue;
        foreach (var e in enemies) if (e != null && !e.Dead) { float d = Vector3.Distance(e.transform.position, me.position); if (d < bd) { bd = d; tgt = e; } }
        playerBrain.Think(me, myRb, L.PlayerTankRig, tgt != null ? tgt.transform : null, tgt != null ? tgt.rb : null, dt, reload <= 0f);
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

    List<Transform> Watchable(PlanetLife L)
    {
        var list = new List<Transform> { L.PlayerRoot }; foreach (var e in enemies) if (e != null) list.Add(e.transform); return list;
    }

    void ToggleSpectate()
    {
        spectate = !spectate;
        if (spectate) Say("Toeschouwer: de AI rijdt je tank · T zelf besturen · Tab andere tank volgen");
        else { PlayerCar.Auto = false; PlayerCar.Watch = null; watchIdx = 0; Say("Je bestuurt je tank weer zelf"); }
    }

    // schade van een inslag: een voltreffer op een tank, en de tanks in de buurt
    void BattleDamage(Vector3 p, Collider col)
    {
        if (!battle) return;
        var hit = col != null ? col.GetComponentInParent<TankRig>() : null;
        bool byEnemy = shellOwner != null && shellOwner.GetComponent<EnemyTank>() != null; float dealt = 0f;
        { Transform foe = null; float fd = float.MaxValue; var L0 = PlanetLife.I;   // voor een test: hoe ver de inslag van het doel was
          if (byEnemy) { if (L0 != null && L0.PlayerRoot != null) foe = L0.PlayerRoot; } else foreach (var e in enemies) if (e != null) { float d0 = Vector3.Distance(e.transform.position, p); if (d0 < fd) { fd = d0; foe = e.transform; } }
          if (foe != null) { BattleMiss[byEnemy ? 1 : 0] += Vector3.Distance(foe.position, p); BattleMissN[byEnemy ? 1 : 0]++; } }
        foreach (var e in enemies)
        {
            if (e == null || e.Dead) continue;
            float d = Harm(e.transform, hit, p); if (d <= 0f) continue;
            e.hp -= d; dealt += d; if (e.rb != null) e.rb.AddForce((e.transform.position - p).normalized * d * 0.012f, ForceMode.VelocityChange);   // een duw (bij het zwaartepunt: geen kanteling)
            if (e.Dead) { e.hp = 0f; KnockOut(e.transform, e.rig); }
        }
        var L = PlanetLife.I;
        if (L != null && L.PlayerRoot != null && playerHp > 0f)
        {
            float d = Harm(L.PlayerRoot, hit, p);
            if (d > 0f)
            {
                playerHp -= d; dealt += d; hurtT = Time.time; Shake(0.7f);
                if (playerHp <= 0f) { playerHp = 0f; KnockOut(L.PlayerRoot, L.PlayerTankRig); PlayerCar.Frozen = true; PlayerCar.Auto = false; spectate = false; }
            }
        }
        CountHit(byEnemy, dealt);
    }
    void CountHit(bool byEnemy, float dealt) { if (dealt > 0f) BattleStats[byEnemy ? 3 : 1]++; }
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

    // ---- de interface: je pantser (linksonder), markeringen bij de tegenstander, een rode flits als je geraakt wordt, de uitslag
    void BattleGui(float W, float H)
    {
        if (!battle || Cam == null) return;
        var L = PlanetLife.I;
        float ht = Time.time - hurtT; if (ht < 0.45f) { var o = GUI.color; GUI.color = new Color(1f, 0.15f, 0.1f, 0.3f * (1f - ht / 0.45f)); GUI.DrawTexture(new Rect(0f, 0f, W, H), Texture2D.whiteTexture); GUI.color = o; }
        // pantser: tien zeshoekjes
        var r = new Rect(G, H - G - 92f, 250f, 92f); Ui.Panel(r, false, 14f);
        Ui.Label(new Rect(r.x + 14f, r.y + 8f, 200f, 20f), "PANTSER", Ui.T.Tiny, Ui.InkDim, TextAnchor.MiddleLeft, 10);
        Ui.Label(new Rect(r.x + 14f, r.y + 8f, r.width - 28f, 20f), $"gewonnen {battleWins} · verloren {battleLosses}", Ui.T.Tiny, Ui.InkDim, TextAnchor.MiddleRight, 10);
        int lit = Mathf.CeilToInt(playerHp / TankArmor * 10f - 1e-3f);
        Color hc = playerHp > 50f ? new Color(0.25f, 0.95f, 1f) : playerHp > 25f ? new Color(1f, 0.7f, 0.25f) : new Color(1f, 0.3f, 0.25f);
        for (int k = 0; k < 10; k++) Ui.Tex(new Rect(r.x + 14f + k * 22f, r.y + 34f, 19f, 21f), Ui.Hex(k < lit ? hc : new Color(1f, 1f, 1f, 0.08f), k < lit ? hc : Ui.Line, 1f, true));
        string st = spectate ? "Toeschouwer: de AI rijdt · T zelf" : playerHp <= 0f ? "Uitgeschakeld" : "T: toeschouwer (de AI rijdt)";
        Ui.Label(new Rect(r.x + 14f, r.y + 62f, r.width - 28f, 20f), st, Ui.T.Tiny, spectate ? Ui.Signal : Ui.InkDim, TextAnchor.MiddleLeft, 10);
        // de tegenstanders: boven de tank een zeshoek met zijn pantser, buiten beeld een zeshoek aan de rand in zijn richting
        foreach (var e in enemies)
        {
            if (e == null) continue;
            Vector3 wp = e.transform.position + e.transform.up * 4.8f; float dist = L != null && L.PlayerRoot != null ? Vector3.Distance(L.PlayerRoot.position, e.transform.position) : 0f;
            Color ec = e.Dead ? new Color(0.6f, 0.6f, 0.6f, 0.7f) : new Color(1f, 0.32f, 0.25f, 0.95f);
            Vector3 sp = Cam.WorldToScreenPoint(wp); Vector2 q = new Vector2(sp.x / Ui.S, (Screen.height - sp.y) / Ui.S);
            bool onScreen = sp.z > 0f && q.x > 30f && q.x < W - 30f && q.y > 30f && q.y < H - 30f;
            if (!onScreen)
            {
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
        if (spectate)
        {
            var w = PlayerCar.Watch != null ? PlayerCar.Watch.GetComponent<EnemyTank>() : null;
            string who = w != null ? "Tegenstander: " + w.brain.state : "Jouw tank (AI): " + playerBrain.state;
            string t = who + "  ·  Tab andere tank  ·  T zelf besturen"; float tw = Ui.Width(t, Ui.T.Dim) + 40f;
            var br = new Rect((W - tw) * 0.5f, G, tw, 36f); Ui.Panel(br, false, 18f); Ui.Label(br, t, Ui.T.Dim, null, TextAnchor.MiddleCenter);
        }
        // de uitslag
        if (battleResult != null && Time.time > battleEndT)
        {
            var pr = new Rect((W - 360f) * 0.5f, (H - 170f) * 0.5f, 360f, 170f); Ui.Panel(pr, false, 16f);
            Ui.Label(new Rect(pr.x, pr.y + 14f, pr.width, 50f), battleResult, Ui.T.Title, battleResult == "Gewonnen" ? Ui.Signal : new Color(1f, 0.45f, 0.4f), TextAnchor.MiddleCenter, 34);
            Ui.Label(new Rect(pr.x, pr.y + 62f, pr.width, 20f), $"gewonnen {battleWins} · verloren {battleLosses}", Ui.T.Dim, Ui.InkDim, TextAnchor.MiddleCenter);
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
