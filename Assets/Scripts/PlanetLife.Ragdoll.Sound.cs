using UnityEngine;

// Geluid bij aanrijdingen, als de webversie (13-camera-modus-geluid.js: carHit, grunt, scream, impact): drie opnames uit de
// webversie in Assets/Resources/Aanrijding (klap.mp3: een klap op de motorkap, grunts.mp3: kreunen, schreeuw.mp3: kreten),
// waarvan steeds één stuk [begin, duur] klinkt, in 3D op de plek; een vrouw of kind iets hoger. Een plof als een ragdoll hard
// neerkomt is zelf berekend (bruine ruis door een laagdoorlaatfilter). Geluid uit (Ambience.Muted) zet alles uit.
public static class Aanrijdgeluid
{
    static readonly float[][] KlapHard = { new[] { 0f, 1.0f }, new[] { 1.08f, 0.89f }, new[] { 2.05f, 2.29f }, new[] { 4.42f, 1.89f }, new[] { 6.39f, 0.85f }, new[] { 7.32f, 0.79f }, new[] { 8.19f, 0.79f }, new[] { 9.06f, 0.72f } };
    static readonly float[][] KlapLicht = { new[] { 9.86f, 0.63f }, new[] { 10.57f, 0.69f }, new[] { 11.34f, 0.66f }, new[] { 12.08f, 0.57f }, new[] { 12.73f, 0.67f }, new[] { 13.48f, 0.65f } };
    static readonly float[] Grunts = { 0.47f, 0.35f, 1.51f, 0.31f, 3.28f, 0.3f, 4.55f, 0.32f, 5.59f, 0.35f, 6.87f, 0.42f, 8.06f, 0.51f, 9.41f, 0.36f, 11.03f, 0.34f, 12.13f, 0.37f, 13.32f, 0.39f, 14.49f, 0.39f, 15.79f, 0.43f, 17.09f, 0.31f, 18.12f, 0.32f, 19.14f, 0.33f, 20.31f, 0.35f, 21.45f, 0.37f, 22.54f, 0.36f, 23.62f, 0.31f, 24.79f, 0.5f, 25.87f, 0.39f, 27.16f, 0.37f, 28.23f, 0.37f, 30.14f, 0.39f, 31.12f, 0.68f, 32.4f, 0.59f, 33.82f, 0.69f, 35.29f, 0.36f, 36.48f, 0.69f, 37.87f, 0.46f, 39.05f, 0.45f, 40.31f, 0.46f };
    static readonly float[][] ScrPaniek = { new[] { 0f, 2.05f }, new[] { 2.3f, 2.35f }, new[] { 4.9f, 1.9f }, new[] { 7.05f, 2.45f }, new[] { 9.75f, 2.75f }, new[] { 12.75f, 2.6f } };
    static readonly float[][] ScrKort = { new[] { 15.6f, 0.5f }, new[] { 16.35f, 0.32f }, new[] { 16.92f, 0.45f } };
    static readonly float[][] ScrHorror = { new[] { 17.62f, 5.08f } };

    static AudioClip klap, grunt, scr, plof; static bool loaded;
    static float lastKlap = -9f, lastGrunt = -9f, lastScr = -9f, lastPlof = -9f; static int scrLive; static float scrFree;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { klap = grunt = scr = plof = null; loaded = false; lastKlap = lastGrunt = lastScr = lastPlof = -9f; scrLive = 0; scrFree = 0f; }

    static void Load()
    {
        if (loaded) return; loaded = true;
        klap = Resources.Load<AudioClip>("Aanrijding/klap"); grunt = Resources.Load<AudioClip>("Aanrijding/grunts"); scr = Resources.Load<AudioClip>("Aanrijding/schreeuw");
        // plof: 0,2 s bruine ruis door een laagdoorlaatfilter van 260 Hz, snel aan en uitstervend
        int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000, n = (int)(rate * 0.2f); var d = new float[n];
        float b = 0f, lp = 0f, k = 1f - Mathf.Exp(-2f * Mathf.PI * 260f / rate), peak = 1e-6f; var rnd = new System.Random(7);
        for (int i = 0; i < n; i++) { b = (b + 0.02f * ((float)rnd.NextDouble() * 2f - 1f)) / 1.02f; lp += (b - lp) * k; float t = i / (float)rate; d[i] = lp * Mathf.Min(1f, t / 0.01f) * Mathf.Exp(-t / 0.045f); peak = Mathf.Max(peak, Mathf.Abs(d[i])); }
        for (int i = 0; i < n; i++) d[i] /= peak;
        plof = AudioClip.Create("plof", n, 1, rate, false); plof.SetData(d, 0);
    }

    static bool Quiet => Ambience.Muted;
    static float Pitch(Person a) => a == null ? 1f : a.kid ? 1.45f : a.female ? 1.18f : 0.92f;

    // een stuk van een opname in 3D; refDist: tot daar op volle sterkte, daarna zachter (zoals de PannerNode van de webversie)
    static AudioSource Piece(AudioClip c, Vector3 at, float off, float dur, float vol, float pitch, float refDist, float delay = 0f)
    {
        if (c == null || off >= c.length) return null;
        var go = new GameObject("aanrijdgeluid"); go.transform.position = at;
        var s = go.AddComponent<AudioSource>(); s.playOnAwake = false; s.clip = c; s.spatialBlend = 1f; s.rolloffMode = AudioRolloffMode.Logarithmic;
        s.minDistance = refDist; s.maxDistance = 300f; s.dopplerLevel = 0f; s.volume = Mathf.Clamp01(vol); s.pitch = pitch;
        s.time = off; double t0 = AudioSettings.dspTime + delay; s.PlayScheduled(t0); s.SetScheduledEndTime(t0 + Mathf.Min(dur, c.length - off) / pitch);
        Object.Destroy(go, delay + dur / pitch + 0.2f); return s;
    }

    // klap op de motorkap; strength 0..1 (snelheid); cars2: twee auto's (lager)
    public static void Klap(Vector3 at, float strength, bool cars2 = false)
    {
        if (Quiet) return; Load(); if (Time.time - lastKlap < 0.12f) return; lastKlap = Time.time;
        var list = strength > 0.45f ? KlapHard : KlapLicht; var p = list[Random.Range(0, list.Length)];
        Piece(klap, at, p[0], p[1], (cars2 ? 0.9f : 0.75f) * (0.35f + 0.65f * Mathf.Min(1f, strength)), (cars2 ? 0.72f : 1f) * Random.Range(0.92f, 1.08f), 5f);
    }

    // een kreun vlak na de klap
    public static void Grunt(Vector3 at, float strength, Person who)
    {
        if (Quiet) return; Load(); if (Time.time - lastGrunt < 0.25f) return; lastGrunt = Time.time;
        int k = Random.Range(0, Grunts.Length / 2);
        Piece(grunt, at, Grunts[k * 2], Grunts[k * 2 + 1], 0.55f + 0.45f * Mathf.Min(1f, strength), Pitch(who) * Random.Range(0.95f, 1.05f), 4f, 0.07f);
    }

    // kreet: "paniek", "kort" of "horror" (bij een ontploffing); hooguit drie tegelijk, niet vlak na elkaar
    public static void Scream(Vector3 at, string kind, Person who)
    {
        if (Quiet) return; Load(); if (Time.time >= scrFree) scrLive = 0;   // alle kreten zijn uit
        if (Time.time - lastScr < 0.6f || scrLive >= 3) return; lastScr = Time.time;
        var list = kind == "horror" ? ScrHorror : kind == "kort" ? ScrKort : ScrPaniek; var p = list[Random.Range(0, list.Length)];
        float pitch = Pitch(who) * (who != null && !who.kid && !who.female ? 1.09f : 1f) * Random.Range(0.94f, 1.06f);   // de webversie: man 1, vrouw 1,2, kind 1,35
        if (Piece(scr, at, p[0], p[1], 0.75f, pitch, 5f, 0.02f) != null) { scrLive++; scrFree = Mathf.Max(scrFree, Time.time + p[1] / pitch); }
    }

    // plof als een lichaam hard neerkomt; strength 0..1
    public static void Plof(Vector3 at, float strength)
    {
        if (Quiet) return; Load(); if (Time.time - lastPlof < 0.08f) return; lastPlof = Time.time;
        Piece(plof, at, 0f, plof.length, Mathf.Min(1f, strength) * 0.9f, Random.Range(0.85f, 1.15f), 6f);
    }
}
