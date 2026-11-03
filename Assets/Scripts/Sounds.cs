using System.Collections.Generic;
using UnityEngine;

// Geluidseffecten en omgevingsgeluid, naar de webversie (13-camera-modus-geluid.js, 15f-geluid-haken.js). Alles wordt bij de start
// berekend (geen opnames). Fx(naam, plek) speelt een kort geluid; zonder plek "in je oor", met plek zachter op afstand (inverse
// afstand vanaf ref meter, zoals Web Audio). Per naam een minimale tussentijd, zodat het niet ratelt.
// Bouwen: PlanetBuilder roept Before (in Push) en After (in Changed) aan; die tellen de inhoud van de stad: meer = place,
// minder = remove, alleen het terrein = earth. Omgeving: regen, windvlagen bij storm, donder na de bliksem, golfjes bij water
// en krekels 's nachts; alles valt weg als de camera hoog staat en in de ruimte (PlanetSky.Space). Geluid uit: Ambience.Muted.
public class Sounds : MonoBehaviour
{
    internal const int SR = 32000;
    static Sounds I;
    static readonly Dictionary<string, AudioClip[]> clips = new Dictionary<string, AudioClip[]>();
    static readonly Dictionary<string, float> lastT = new Dictionary<string, float>();
    static readonly Dictionary<string, float> gap = new Dictionary<string, float> {
        { "place", 0.05f }, { "remove", 0.05f }, { "earth", 0.15f }, { "deny", 0.25f }, { "click", 0.04f }, { "undo", 0.1f }, { "whoosh", 0.2f },
        { "gen", 1f }, { "coin", 0.6f }, { "cricket", 0.05f }, { "crunch", 0.12f }, { "squeal", 0.3f }, { "splash", 0.2f }, { "thunder", 1.5f } };
    static readonly Dictionary<string, float> level = new Dictionary<string, float> {
        { "place", 0.4f }, { "remove", 0.45f }, { "earth", 0.3f }, { "deny", 0.3f }, { "click", 0.12f }, { "undo", 0.3f }, { "whoosh", 0.22f },
        { "gen", 0.3f }, { "coin", 0.22f }, { "cricket", 0.1f }, { "crunch", 0.6f }, { "squeal", 0.3f }, { "splash", 0.35f }, { "thunder", 0.55f } };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { I = null; clips.Clear(); lastT.Clear(); has = false; pendKind = null; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() { if (I == null) new GameObject("Geluiden").AddComponent<Sounds>(); }

    AudioSource[] pool; int poolI;
    AudioSource rain, gust, water;
    float mixT, nearK, rainK, greenK, waterK, crickT, prevFlash, thunderT = -1f;

    void Awake()
    {
        I = this; DontDestroyOnLoad(gameObject);
        pool = new AudioSource[8];
        for (int k = 0; k < pool.Length; k++) { pool[k] = gameObject.AddComponent<AudioSource>(); pool[k].playOnAwake = false; pool[k].spatialBlend = 0f; }
        Make();
        rain = Loop(clips["rain"][0]); gust = Loop(clips["gust"][0]); water = Loop(clips["water"][0]);
    }

    AudioSource Loop(AudioClip c) { var a = gameObject.AddComponent<AudioSource>(); a.clip = c; a.loop = true; a.volume = 0f; a.spatialBlend = 0f; a.playOnAwake = false; a.Play(); return a; }

    // ---------- afspelen ----------

    public static void Fx(string name, float strength = 1f) => Play(name, null, strength, 0f);
    public static void Fx(string name, Vector3 pos, float strength = 1f, float refDist = 12f) => Play(name, pos, strength, refDist);
    public static void Click() => Fx("click");

    static void Play(string name, Vector3? pos, float strength, float refDist)
    {
        if (I == null || Ambience.Muted || !clips.TryGetValue(name, out var cs)) return;
        float now = Time.unscaledTime;
        if (lastT.TryGetValue(name, out var t) && now - t < (gap.TryGetValue(name, out var g) ? g : 0.05f)) return;
        lastT[name] = now;
        float vol = (level.TryGetValue(name, out var l) ? l : 0.3f) * Mathf.Clamp(strength, 0f, 1.5f), pan = 0f;
        var cam = Camera.main;
        if (pos.HasValue && cam != null)
        {
            Vector3 d = pos.Value - cam.transform.position; float dist = d.magnitude;
            vol *= refDist / (refDist + Mathf.Max(0f, dist - refDist));   // inverse afstand, zoals Web Audio
            if (refDist < 50f) vol *= 1f - PlanetSky.Space;              // geluid uit de stad: niet in de ruimte
            if (dist > 0.5f) pan = Mathf.Clamp(Vector3.Dot(d / dist, cam.transform.right), -1f, 1f) * 0.6f;
        }
        if (vol < 0.004f) return;
        var src = I.pool[I.poolI = (I.poolI + 1) % I.pool.Length];
        src.panStereo = pan; src.pitch = 0.96f + Random.value * 0.08f;
        src.PlayOneShot(cs[Random.Range(0, cs.Length)], vol);
    }

    // ---------- haken voor bouwen en slopen ----------

    static bool has; static int lastCount, lastHash; static float lastTerr;
    static string pendKind; static Vector3 pendPos;
    static void Measure(out int count, out int hash, out float terr)
    {
        count = 0; hash = 0; terr = 0f;
        foreach (var kv in PlanetCity.Cells) { count += kv.Value.Count; int h = kv.Key; foreach (var s in kv.Value) h = h * 31 + s.GetHashCode(); hash += h * 16777619; }
        count += PlanetCity.Links.Count;
        foreach (var kv in PlanetCity.Terr) terr += kv.Value * (1f + (kv.Key % 7));
    }
    // vóór een wijziging (PlanetBuilder.Push)
    public static void Before() { if (!Planet.Ready) return; Measure(out lastCount, out lastHash, out lastTerr); has = true; }
    // na een wijziging (PlanetBuilder.Changed): het geluid komt aan het eind van het beeld, tenzij er nog iets anders klinkt (undo, gen)
    public static void After(Vector3 pos)
    {
        if (!Planet.Ready) return;
        Measure(out int c, out int h, out float tr);
        if (has)
        {
            string kind = c > lastCount ? "place" : c < lastCount ? "remove" : h != lastHash ? "place" : Mathf.Abs(tr - lastTerr) > 1e-3f ? "earth" : null;
            if (kind != null) { pendKind = kind; pendPos = pos; }
        }
        lastCount = c; lastHash = h; lastTerr = tr; has = true;
    }
    // undo, gen en dergelijke gaan voor het tellen
    public static void Instead(string name) { pendKind = null; Fx(name); }

    void LateUpdate()
    {
        if (pendKind != null) { Fx(pendKind, pendPos, 1f, 150f); pendKind = null; }
        Surroundings();
    }

    // ---------- omgeving ----------

    void Surroundings()
    {
        var cam = Camera.main; bool muted = Ambience.Muted || cam == null || !Planet.Ready;
        float dt = Time.unscaledDeltaTime;
        if (!muted && (mixT -= dt) <= 0f) { mixT = 0.5f; Look(cam); }
        float space = 1f - PlanetSky.Space;
        float rainV = muted ? 0f : Weather.Rain * (1f - Weather.Snow) * rainK * space * 0.32f;
        float gustV = muted ? 0f : Mathf.Max(Weather.Storm, Weather.Rain * 0.35f) * (0.4f + 0.6f * rainK) * space * 0.3f;
        float waterV = muted ? 0f : waterK * nearK * space * 0.35f;
        rain.volume = Mathf.MoveTowards(rain.volume, rainV, dt * 0.3f);
        gust.volume = Mathf.MoveTowards(gust.volume, gustV, dt * 0.3f);
        water.volume = Mathf.MoveTowards(water.volume, waterV, dt * 0.3f);
        if (muted) { prevFlash = Weather.Flash; return; }

        // donder: na de bliksem, met wat vertraging
        if (Weather.Flash > 0.9f && prevFlash < 0.5f) thunderT = 0.4f + Random.value * 2.2f;
        prevFlash = Weather.Flash;
        if (thunderT >= 0f && (thunderT -= dt) < 0f) Fx("thunder", (0.5f + 0.5f * rainK) * space);

        // krekels 's nachts: elke 0,4 s een kans, luider bij groen, stil bij regen
        float night = DayNight.Night;
        if (night > 0.45f && (crickT -= dt) <= 0f)
        {
            crickT = 0.4f;
            if (Random.value < 0.5f * night * greenK)
            {
                Vector3 up = cam.transform.position.normalized, side = Vector3.Cross(up, cam.transform.forward).normalized; if (side.sqrMagnitude < 0.5f) side = cam.transform.right;
                Vector3 p = cam.transform.position + (Random.value < 0.5f ? side : -side) * (4f + Random.value * 14f) + Random.insideUnitSphere * 4f;
                Fx("cricket", p, nearK * (1f - Weather.Rain) * space * (0.5f + Random.value * 0.5f), 18f);   // ref 18 m: de afstand doet hier weinig, de hoogte wel
            }
        }
    }

    // wat is er in de buurt van de camera? (zoals Ambience: vakken binnen twee ringen)
    void Look(Camera cam)
    {
        Vector3 cp = cam.transform.position; int c0 = Planet.Nearest(cp);
        float ground = Planet.R + PlanetCity.TerrainAt(cp.normalized), h = cp.magnitude - ground;
        nearK = Mathf.Clamp01(1f - (h - 30f) / 220f);      // laag bij de grond: krekels, golfjes
        rainK = Mathf.Clamp01(1f - (h - 80f) / 500f);      // regen hoor je tot ver boven de stad
        float green = 0f, wet = 0f; int n = 0;
        var seen = new HashSet<int> { c0 };
        foreach (int j in Planet.Cells[c0].nb) { seen.Add(j); foreach (int k in Planet.Cells[j].nb) seen.Add(k); }
        foreach (int i in seen)
        {
            n++; float g = PlanetCity.G(i);
            if (PlanetCity.Cells.TryGetValue(i, out var l) && l.Count > 0)
            {
                string b = l[0];
                if (b == "meer" || b == "water" || b == "gracht") wet += 1f;
                else if (b == "park" || b == "bos" || b == "tuin") green += 1f;
            }
            else if (g < PlanetCity.SeaLevel) wet += 1f;
            else if (g < 48f) green += 0.6f;   // los terrein: gras, onder de boomgrens
        }
        greenK = Mathf.Clamp01(green / (n * 0.5f));
        waterK = Mathf.Clamp01(wet / 5f);
    }

    // ---------- de geluiden uitrekenen ----------

    static void Make()
    {
        if (clips.Count > 0) return;
        var r = new System.Random(11);
        clips["place"] = new[] { Clip("place", 0.16f, d => { Tone(d, 0f, 0.13f, 210f, 120f, 0, 0.8f); Tone(d, 0f, 0.09f, 640f, 420f, 1, 0.25f); Noise(d, r, 0f, 0.02f, 2, 2600f, 2600f, 3f, 0.5f, false); }) };
        clips["remove"] = Variants("remove", 3, 0.3f, (d, rr) => { Noise(d, rr, 0f, 0.28f, 0, 700f, 180f, 0.7f, 1f, true); Tone(d, 0f, 0.22f, 120f, 55f, 0, 0.6f); for (int k = 0; k < 5; k++) Noise(d, rr, 0.02f + (float)rr.NextDouble() * 0.2f, 0.012f, 2, 2200f + (float)rr.NextDouble() * 2500f, 2200f, 5f, 0.35f, false); });
        clips["earth"] = Variants("earth", 2, 0.22f, (d, rr) => { Noise(d, rr, 0f, 0.2f, 0, 420f, 150f, 0.7f, 1f, true); Tone(d, 0f, 0.14f, 90f, 60f, 0, 0.4f); });
        clips["deny"] = new[] { Clip("deny", 0.2f, d => { Tone(d, 0f, 0.075f, 170f, 170f, 2, 0.5f); Tone(d, 0.095f, 0.09f, 130f, 130f, 2, 0.5f); }) };
        clips["click"] = new[] { Clip("click", 0.035f, d => Tone(d, 0f, 0.03f, 1800f, 1500f, 0, 1f)) };
        clips["undo"] = new[] { Clip("undo", 0.24f, d => { Noise(d, r, 0f, 0.22f, 2, 2600f, 500f, 2f, 0.6f, false); Tone(d, 0f, 0.2f, 660f, 330f, 0, 0.45f); }) };
        clips["whoosh"] = new[] { Clip("whoosh", 0.28f, d => Noise(d, r, 0f, 0.26f, 2, 350f, 1900f, 1.5f, 1f, false, 0.4f)) };
        float[] ladder = { 261.6f, 293.7f, 329.6f, 392f, 440f, 523.3f, 587.3f, 659.3f };
        clips["gen"] = Variants("gen", 3, 0.75f, (d, rr) =>
        {
            Noise(d, rr, 0f, 0.26f, 2, 350f, 1900f, 1.5f, 0.5f, false, 0.4f);
            int s = rr.Next(0, 3); for (int k = 0; k < 5; k++) Tone(d, 0.12f + k * 0.07f, 0.35f, ladder[s + k], ladder[s + k], 1, 0.35f);
        });
        clips["coin"] = new[] { Clip("coin", 0.3f, d => { Tone(d, 0f, 0.08f, 880f, 880f, 1, 0.5f); Tone(d, 0.07f, 0.22f, 1318.6f, 1318.6f, 1, 0.5f); }) };
        clips["cricket"] = Variants("cricket", 3, 0.2f, (d, rr) => { float f = 4300f + (float)rr.NextDouble() * 600f; for (int k = 0; k < 3; k++) Tone(d, k * 0.055f, 0.03f, f, f * 0.98f, 0, 0.6f, 0.004f); });
        clips["crunch"] = Variants("crunch", 3, 0.6f, (d, rr) =>
        {
            Noise(d, rr, 0f, 0.4f, 0, 500f, 120f, 0.7f, 1f, true); Noise(d, rr, 0f, 0.08f, 1, 1400f, 1400f, 0.7f, 0.4f, false);
            foreach (float f in new[] { 1320f, 2210f, 3480f, 4950f }) Tone(d, 0.005f, 0.18f, f, f * 0.97f, 0, 0.12f, 0.001f);
            for (int k = 0; k < 6; k++) Tone(d, 0.08f + (float)rr.NextDouble() * 0.4f, 0.08f, 5200f + (float)rr.NextDouble() * 2500f, 5000f, 0, 0.06f, 0.001f);   // glas
        });
        clips["squeal"] = new[] { Clip("squeal", 0.6f, d => { Noise(d, r, 0f, 0.58f, 2, 1900f, 1500f, 14f, 0.8f, false, 0.08f); Tone(d, 0f, 0.58f, 1150f, 980f, 0, 0.18f, 0.08f); }) };
        clips["splash"] = new[] { Clip("splash", 0.55f, d => { Noise(d, r, 0f, 0.5f, 2, 1400f, 500f, 1f, 0.7f, false); Noise(d, r, 0f, 0.3f, 0, 300f, 120f, 0.7f, 0.6f, true); }) };
        clips["thunder"] = Variants("thunder", 2, 3.2f, (d, rr) =>
        {
            Noise(d, rr, 0f, 0.3f, 2, 1800f, 400f, 0.8f, 0.5f, false);
            int n = d.Length; var t = new float[n]; Noise(t, rr, 0f, 3.1f, 0, 260f, 55f, 0.7f, 1f, true, 0.15f);
            double ph = rr.NextDouble() * 6.0; for (int i = 0; i < n; i++) { float s = i / (float)SR; d[i] += t[i] * (0.7f + 0.3f * Mathf.Sin((float)ph + s * 7f) * Mathf.Sin(s * 2.3f)); }
        });
        clips["rain"] = new[] { LoopClip("regen", 6f, 0.5f, (d, rr) =>
        {
            Noise(d, rr, 0f, d.Length / (float)SR, 2, 2200f, 2200f, 0.5f, 0.5f, false, 0.001f, true);
            for (int k = 0; k < 220; k++) Noise(d, rr, (float)rr.NextDouble() * (d.Length / (float)SR - 0.02f), 0.008f, 2, 2000f + (float)rr.NextDouble() * 3000f, 2000f, 4f, 0.25f, false);
        }) };
        clips["gust"] = new[] { LoopClip("vlagen", 8f, 0.8f, (d, rr) =>
        {
            int n = d.Length; var t = new float[n]; Noise(t, rr, 0f, n / (float)SR, 0, 500f, 500f, 0.7f, 1f, true, 0.001f, true);
            double p1 = rr.NextDouble() * 6.3, p2 = rr.NextDouble() * 6.3;
            for (int i = 0; i < n; i++) { float s = i / (float)SR; float m = 0.5f + 0.3f * Mathf.Sin((float)p1 + s * Mathf.PI * 2f / 8f) + 0.2f * Mathf.Sin((float)p2 + s * Mathf.PI * 2f * 3f / 8f); d[i] = t[i] * m * m; }
        }) };
        clips["water"] = new[] { LoopClip("golfjes", 8f, 0.4f, (d, rr) =>
        {
            int n = d.Length; var lo = new float[n]; var hi = new float[n];
            Noise(lo, rr, 0f, n / (float)SR, 0, 450f, 450f, 0.7f, 1f, true, 0.001f, true);
            Noise(hi, rr, 0f, n / (float)SR, 2, 1200f, 1200f, 0.8f, 0.4f, false, 0.001f, true);
            for (int i = 0; i < n; i++) { float s = i / (float)SR, w = Mathf.Sin(s * Mathf.PI / 2.667f); w *= w; d[i] = lo[i] * (0.3f + 0.7f * w) + hi[i] * w * w * w; }   // drie golfjes in 8 s
        }) };
    }

    delegate void Fill(float[] d);
    delegate void FillR(float[] d, System.Random r);
    static AudioClip Clip(string name, float dur, Fill f)
    {
        var d = new float[Mathf.CeilToInt(dur * SR)]; f(d); Peak(d, 0.8f);
        var c = AudioClip.Create(name, d.Length, 1, SR, false); c.SetData(d, 0); return c;
    }
    static AudioClip[] Variants(string name, int count, float dur, FillR f)
    {
        var a = new AudioClip[count];
        for (int k = 0; k < count; k++) { var r = new System.Random(100 + k * 17 + name.Length); a[k] = Clip(name + k, dur, d => f(d, r)); }
        return a;
    }
    // een lus: wat langer uitrekenen en het eind over het begin laten vloeien, zodat er geen tik in zit
    static AudioClip LoopClip(string name, float dur, float fade, FillR f)
    {
        int n = Mathf.CeilToInt(dur * SR), m = Mathf.CeilToInt(fade * SR); var d = new float[n + m]; f(d, new System.Random(name.Length * 7));
        var o = new float[n];
        for (int i = 0; i < n; i++) o[i] = d[i];
        for (int i = 0; i < m; i++) { float k = i / (float)m; o[i] = d[i] * Mathf.Sqrt(k) + d[n + i] * Mathf.Sqrt(1f - k); }
        Peak(o, 0.8f);
        var c = AudioClip.Create(name, n, 1, SR, false); c.SetData(o, 0); return c;
    }
    internal static void Peak(float[] d, float p) { float mx = 1e-4f; foreach (float v in d) mx = Mathf.Max(mx, Mathf.Abs(v)); float g = p / mx; for (int i = 0; i < d.Length; i++) d[i] *= g; }

    // omhulsel: snel op (attack), dan exponentieel weg; hold houdt hem eerst vol (voor lussen)
    static float Env(float t, float dur, float attack, bool hold = false)
    {
        if (t < attack) return t / attack;
        if (hold) return t > dur - attack ? Mathf.Max(0f, (dur - t) / attack) : 1f;
        return Mathf.Exp(-6.5f * (t - attack) / Mathf.Max(1e-3f, dur - attack));
    }

    // toon met een glijdende toonhoogte; wave 0 sinus, 1 driehoek, 2 blokgolf
    internal static void Tone(float[] d, float start, float dur, float f0, float f1, int wave, float amp, float attack = 0.003f)
    {
        int i0 = (int)(start * SR), n = (int)(dur * SR); double ph = 0;
        for (int i = 0; i < n && i0 + i < d.Length; i++)
        {
            float t = i / (float)SR, f = f0 * Mathf.Pow(f1 / f0, t / dur); ph += f / SR; float x = (float)(ph - System.Math.Floor(ph));
            float v = wave == 0 ? Mathf.Sin(x * Mathf.PI * 2f) : wave == 1 ? 1f - 4f * Mathf.Abs(x - 0.5f) : (x < 0.5f ? 0.6f : -0.6f);
            d[i0 + i] += v * amp * Env(t, dur, attack);
        }
    }

    // ruis door een filter waarvan de frequentie glijdt; type 0 laagdoorlaat, 1 hoogdoorlaat, 2 banddoorlaat; brown: bruine ruis
    internal static void Noise(float[] d, System.Random r, float start, float dur, int type, float f0, float f1, float q, float amp, bool brown, float attack = 0.003f, bool hold = false)
    {
        int i0 = (int)(start * SR), n = (int)(dur * SR); float x1 = 0, x2 = 0, y1 = 0, y2 = 0, br = 0f;
        for (int i = 0; i < n && i0 + i < d.Length; i++)
        {
            float t = i / (float)SR, w = (float)r.NextDouble() * 2f - 1f;
            if (brown) { br = (br + 0.02f * w) / 1.02f; w = br * 3.5f; }
            float f = Mathf.Min(f0 * Mathf.Pow(f1 / f0, t / dur), SR * 0.45f), om = 2f * Mathf.PI * f / SR, cs = Mathf.Cos(om), al = Mathf.Sin(om) / (2f * q);
            float b0, b1, b2, a0 = 1f + al, a1 = -2f * cs, a2 = 1f - al;
            if (type == 0) { b0 = (1f - cs) / 2f; b1 = 1f - cs; b2 = b0; }
            else if (type == 1) { b0 = (1f + cs) / 2f; b1 = -(1f + cs); b2 = b0; }
            else { b0 = al; b1 = 0f; b2 = -al; }
            float y = (b0 * w + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2) / a0;
            x2 = x1; x1 = w; y2 = y1; y1 = y;
            d[i0 + i] += y * amp * Env(t, dur, attack, hold);
        }
    }
}
