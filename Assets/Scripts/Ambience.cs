using UnityEngine;

// Sfeergeluid, naar de browserversie: vogels (opname, luider bij groen, stil 's nachts), wind (ruis door een filter,
// sterker als je hoog kijkt), en zachte stadsruis bij wegen. Voetstappen zitten in de wandelaar (Footsteps).
// De vogelopname staat in Assets/Resources/vogels.mp3; ontbreekt die, dan zijn er alleen de berekende geluiden.
public class Ambience : MonoBehaviour
{
    public static bool Muted;
    AudioSource birds, wind, city;
    float mixT;

    static AudioClip Noise(int seconds, float lowpass)
    {
        // bruine ruis (laag, zacht) met een eenvoudig eenpolig filter
        int sr = 22050, n = sr * seconds; var d = new float[n]; float last = 0f; var rnd = new System.Random(7);
        for (int i = 0; i < n; i++) { float w = (float)rnd.NextDouble() * 2f - 1f; last += lowpass * (w - last); d[i] = last; }
        float mx = 0.001f; for (int i = 0; i < n; i++) mx = Mathf.Max(mx, Mathf.Abs(d[i])); for (int i = 0; i < n; i++) d[i] = d[i] / mx * 0.8f;
        var clip = AudioClip.Create("ruis", n, 1, sr, false); clip.SetData(d, 0); return clip;
    }

    static AudioSource Loop(GameObject go, AudioClip clip, float vol)
    {
        var a = go.AddComponent<AudioSource>(); a.clip = clip; a.loop = true; a.volume = vol; a.spatialBlend = 0f; a.playOnAwake = false; if (clip != null) a.Play(); return a;
    }

    void Start()
    {
        var vogels = Resources.Load<AudioClip>("vogels");
        birds = Loop(gameObject, vogels, 0f);
        wind = Loop(gameObject, Noise(4, 0.02f), 0f);
        city = Loop(gameObject, Noise(4, 0.08f), 0f);
    }

    void Update()
    {
        mixT -= Time.deltaTime; if (mixT > 0f) return; mixT = 0.5f;
        var cam = Camera.main; if (cam == null || !Planet.Ready) return;
        float night = DayNight.Night;
        // wat staat er in de buurt van de camera? (vakken binnen ~80 m)
        int c0 = Planet.Nearest(cam.transform.position); int green = 0, built = 0;
        foreach (int j in Planet.Cells[c0].nb)
        {
            Count(j, ref green, ref built);
            foreach (int k in Planet.Cells[j].nb) Count(k, ref green, ref built);
        }
        Count(c0, ref green, ref built);
        float height = cam.transform.position.magnitude - Planet.R;
        float far = Mathf.Clamp01(1f - (height - 40f) / 200f);   // hoog boven de stad hoor je weinig
        float up = Mathf.Clamp01(Vector3.Dot(cam.transform.forward, cam.transform.position.normalized));   // omhoog kijken = wind
        float vol = Muted ? 0f : 1f;
        // omgevingsgeluid alleen waar iets staat: vogels bij bomen en parken, stadsruis bij gebouwen; op een leeg veld alleen wind
        if (birds != null) birds.volume = vol * Mathf.Min(1f, green / 6f) * 0.65f * far * (1f - 0.92f * night);
        if (wind != null) wind.volume = vol * (0.04f + 0.12f * up + 0.15f * Mathf.Clamp01(height / 200f)) * (1f - PlanetSky.Space);   // de ruis van de wind: half zo hard als eerst, in de ruimte geen wind
        if (city != null) city.volume = vol * Mathf.Min(1f, built / 8f) * 0.09f * far;   // stadsruis bij gebouwen, half zo hard als eerst
    }

    static void Count(int i, ref int green, ref int built)
    {
        if (!PlanetCity.Cells.TryGetValue(i, out var l) || l.Count == 0) return;
        string b = l[0];
        if (b == "park") green++;   // bomen
        else if (b != "weg" && b != "pad" && b != "wegdeel" && b != "meer" && b != "water" && b != "kuil") built++;   // een gebouw
    }
}

// Voetstappen, om de zoveel meter, harder bij rennen. Elke stap is een hak en daarna een bal van de voet. Op asfalt een doffe
// plof met een droog tikje, op gras en zand een korte ritseling van korreltjes. Zes varianten per ondergrond, nooit twee keer
// dezelfde achter elkaar. De klanken komen uit Sounds (Tone, Noise).
public static class Footsteps
{
    static AudioClip[] hardClips, softClips; static float dist; static int lastK = -1;
    public static void Reset() { hardClips = softClips = null; dist = 0f; lastK = -1; }
    static void Make()
    {
        hardClips = new AudioClip[6]; softClips = new AudioClip[6]; int sr = Sounds.SR;
        for (int c = 0; c < 6; c++)
        {
            var r = new System.Random(30 + c); float toe = 0.055f + (float)r.NextDouble() * 0.03f;
            var d = new float[(int)(sr * 0.2f)];
            Sounds.Tone(d, 0f, 0.06f, 110f + c * 6f, 70f, 0, 0.5f, 0.002f);                                                       // hak: doffe plof
            Sounds.Noise(d, r, 0f, 0.025f, 2, 2600f + c * 150f, 1800f, 1.8f, 0.35f, false, 0.001f);                             // droog tikje
            Sounds.Noise(d, r, toe, 0.05f, 2, 1500f + c * 120f, 1100f, 1.2f, 0.18f, false, 0.002f);                              // bal van de voet
            Sounds.Tone(d, toe, 0.04f, 95f, 70f, 0, 0.2f, 0.002f);
            Sounds.Peak(d, 0.7f); hardClips[c] = AudioClip.Create("stap hard" + c, d.Length, 1, sr, false); hardClips[c].SetData(d, 0);

            d = new float[(int)(sr * 0.24f)];
            Sounds.Tone(d, 0f, 0.07f, 80f, 55f, 0, 0.35f, 0.004f);
            for (int k = 0; k < 9; k++) Sounds.Noise(d, r, (float)r.NextDouble() * 0.06f, 0.012f, 2, 2500f + (float)r.NextDouble() * 2500f, 2200f, 3f, 0.22f, false, 0.001f);   // korreltjes
            Sounds.Noise(d, r, 0f, 0.09f, 2, 3200f, 2000f, 0.8f, 0.12f, false, 0.008f);                                         // gras ritselt
            for (int k = 0; k < 6; k++) Sounds.Noise(d, r, toe + (float)r.NextDouble() * 0.06f, 0.01f, 2, 2800f + (float)r.NextDouble() * 2000f, 2400f, 3f, 0.15f, false, 0.001f);
            Sounds.Peak(d, 0.7f); softClips[c] = AudioClip.Create("stap zacht" + c, d.Length, 1, sr, false); softClips[c].SetData(d, 0);
        }
    }
    public static void Walked(AudioSource src, float metres, bool running, bool hard)
    {
        if (src == null || Ambience.Muted) return; if (hardClips == null) Make();
        dist += metres; float step = running ? 1.25f : 0.75f;
        if (dist < step) return; dist = 0f;
        int k = Random.Range(0, 5); if (k >= lastK) k++; lastK = k;   // niet twee keer dezelfde
        src.pitch = 0.94f + Random.value * 0.12f;
        src.PlayOneShot((hard ? hardClips : softClips)[k], (running ? 0.2f : 0.12f) * (hard ? 1f : 0.85f));
    }
}
