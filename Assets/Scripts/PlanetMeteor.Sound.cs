using UnityEngine;

// Het geluid van een meteoor, bij de eerste meteoor berekend (geen opnames, op de achtergrond: MakeClips), in stereo. Lagen zoals een echte inslag:
// - vlucht: een brullend, knetterend gesis dat aanzwelt in de atmosfeer en hoger wordt als hij nadert; daar een dubbele
//   supersone knal (N-golf) als hij de atmosfeer in duikt;
// - inslag: de klap (N-golf met een knal van ruis en de weerkaatsing op de grond), de dreun (zakkende lage toon met
//   verzadiging), brokstukken die neerkletteren (honderden tikjes en plofjes, verspreid over links en rechts) en gerommel
//   met echo's van ver weg;
// - water: een plons bij een inslag in het water, een aanzwellend geruis met geborrel als een krater volloopt.
// Afstand: zachter en dof: hoge tonen verdwijnen eerst (een laagdoorlaatfilter per laag). Geen vertraging: de klap klinkt
// tegelijk met de inslag.
// Dichtbij overheerst de klap, ver weg alleen het rommelen. Geluid uit (Ambience.Muted) zet alles uit.
// Twee opnames (Assets/Resources/Meteoor, LICENTIE.txt) gaan erbovenop: de aanloop klinkt tijdens de vlucht en is zo gestart dat
// zijn klap precies op de inslag valt (RisePeak zoekt die klap in de opname); de dreun is de hoofdklap bij de inslag, in plaats
// van de berekende dreun. Beide lager naarmate de meteoor groter is en dof op afstand. Zonder opnames: alleen de berekende lagen.
public partial class PlanetMeteor
{
    const int SR = Sounds.SR;
    static AudioClip crackClip, boomClip, rumbleClip, debrisClip, roarClip, floodClip, splashClip;
    static void ResetClips() { crackClip = boomClip = rumbleClip = debrisClip = roarClip = floodClip = splashClip = null; baking = null; riseRec = boomRec = null; risePeak = -1f; }
    static AudioClip riseRec, boomRec; static float risePeak = -1f;
    static void LoadRecordings()
    {
        if (riseRec == null) riseRec = Resources.Load<AudioClip>("Meteoor/aanloop");
        if (boomRec == null) boomRec = Resources.Load<AudioClip>("Meteoor/inslag");
        foreach (var c in new[] { riseRec, boomRec }) if (c != null && c.loadState == AudioDataLoadState.Unloaded) c.LoadAudioData();   // niet vooraf geladen (preloadAudioData uit)
    }
    // waar in de aanloop de klap zit: het eerste stukje van 20 ms dat 60% van het hardste haalt
    static float RisePeak()
    {
        if (risePeak >= 0f) return risePeak;
        var c = riseRec; risePeak = c.length * 0.6f;
        if (c.loadState != AudioDataLoadState.Loaded) c.LoadAudioData();
        var d = new float[c.samples * c.channels]; if (!c.GetData(d, 0)) return risePeak;
        int win = Mathf.Max(1, c.frequency / 50) * c.channels, nw = d.Length / win; var e = new float[nw]; float mx = 0f;
        for (int w = 0; w < nw; w++) { float s2 = 0f; for (int i = w * win; i < (w + 1) * win; i++) s2 += d[i] * d[i]; e[w] = s2; mx = Mathf.Max(mx, s2); }
        for (int w = 0; w < nw; w++) if (e[w] >= 0.6f * mx) { risePeak = (float)w * win / c.channels / c.frequency; break; }
        return risePeak;
    }

    // ---------- afspelen ----------
    // een laag: eigen bron met laagdoorlaatfilter; weg na afloop
    AudioSource Layer(GameObject on, AudioClip clip, float vol, float pitch, float cutoff, float delay, float pan)
    {
        var go = new GameObject(clip.name); go.transform.SetParent(on.transform, false);
        var a = go.AddComponent<AudioSource>(); a.playOnAwake = false; a.spatialBlend = 0f; a.clip = clip;
        a.volume = Mathf.Clamp01(vol); a.pitch = pitch; a.panStereo = Mathf.Clamp(pan, -1f, 1f);
        var lp = go.AddComponent<AudioLowPassFilter>(); lp.cutoffFrequency = Mathf.Clamp(cutoff, 200f, 22000f); lp.lowpassResonanceQ = 1f;
        a.PlayDelayed(Mathf.Max(0f, delay)); return a;
    }
    // inverse afstand vanaf ref (p > 1: valt sneller weg)
    static float Fall(float dist, float refD, float p = 1f) => Mathf.Pow(refD / (refD + Mathf.Max(0f, dist - refD)), p);
    static float Far(float dist) => Mathf.Clamp01(dist / 1400f);   // 0 dichtbij, 1 ver: dof
    static float Pan(Vector3 at)
    {
        var cam = Cam(); if (cam == null) return 0f;
        Vector3 d = at - cam.transform.position; float m = d.magnitude; return m < 1f ? 0f : Vector3.Dot(d / m, cam.transform.right) * 0.7f;
    }
    static float Quiet => 1f - 0.75f * PlanetSky.Space;   // in de ruimte hoor je bijna niets

    void ImpactSound(Vector3 at, int b, float dist, bool splash, float gain = 1f)
    {
        if (Ambience.Muted) return;
        var go = new GameObject("Inslaggeluid"); go.transform.SetParent(transform, false); Destroy(go, 16f);
        float delay = 0f, far = Far(dist), pan = Pan(at), q = Quiet * gain, big = 1f + 0.5f * b;
        if (boomRec != null) Layer(go, boomRec, Fall(dist, 180f * big) * (0.85f + 0.075f * b) * q, 1.1f - 0.12f * b, Mathf.Lerp(22000f, 650f, far), delay, pan * 0.6f);   // de opgenomen dreun
        if (!Baked()) return;
        Layer(go, crackClip, Fall(dist, 45f * big, 1.6f) * q * (boomRec != null ? 0.6f : 1f), 1.12f - 0.1f * b, Mathf.Lerp(20000f, 700f, far), delay, pan);
        if (boomRec == null) Layer(go, boomClip, Fall(dist, 140f * big) * (0.8f + 0.1f * b) * q, 1.08f - 0.13f * b, Mathf.Lerp(9000f, 380f, far), delay + 0.01f, pan * 0.6f);
        Layer(go, rumbleClip, Fall(dist, 420f * big) * (0.55f + 0.15f * b) * q, 1f - 0.08f * b, Mathf.Lerp(3000f, 600f, far), delay + 0.25f, 0f);
        if (!splash) Layer(go, debrisClip, Fall(dist, 40f * big, 1.5f) * (0.75f + 0.1f * b) * q, 1.06f - 0.06f * b, Mathf.Lerp(16000f, 900f, far), delay + 0.2f, pan * 0.4f);
        else Layer(go, splashClip, Fall(dist, 60f * big, 1.3f) * q, 1.05f - 0.1f * b, Mathf.Lerp(16000f, 900f, far), delay + 0.05f, pan * 0.5f);
    }
    void FloodSound(Vector3 at, int b)
    {
        if (Ambience.Muted) return;
        if (!Baked()) return;
        var cam = Cam(); float dist = cam != null ? Vector3.Distance(cam.transform.position, at) : 400f;
        var go = new GameObject("Waterstroom"); go.transform.SetParent(transform, false); Destroy(go, 10f);
        Layer(go, floodClip, Fall(dist, 70f * (1f + 0.5f * b), 1.3f) * Quiet, 1.05f - 0.08f * b, Mathf.Lerp(16000f, 900f, Far(dist)), 0f, Pan(at));
    }

    // vlucht: een lus die meeloopt met de rots
    void StartRoar(Rock r)
    {
        var go = new GameObject("Gebrul"); go.transform.SetParent(transform, false);
        r.roar = go.AddComponent<AudioSource>(); r.roar.clip = roarClip; r.roar.loop = true; r.roar.spatialBlend = 0f; r.roar.volume = 0f; r.roar.playOnAwake = false;
        r.roar.time = Random.Range(0f, roarClip.length * 0.9f);
        r.roarLp = go.AddComponent<AudioLowPassFilter>(); r.roarLp.cutoffFrequency = 800f;
        r.roar.Play();
        // de aanloop: zo gestart dat zijn klap op de inslag valt; is de vlucht korter, dan begint hij halverwege
        if (riseRec == null || Ambience.Muted || r.shower) return;   // in een regen niet: twintig aanlopen door elkaar
        float pitch = 1.06f - 0.08f * r.size, lead = r.dur - RisePeak() / pitch;
        var rg = new GameObject("Aanloop"); rg.transform.SetParent(transform, false); Destroy(rg, Mathf.Max(0f, lead) + riseRec.length / pitch + 0.5f);
        r.rise = Layer(rg, riseRec, 0f, pitch, 8000f, Mathf.Max(0f, lead), 0f);
        if (lead < 0f) { r.rise.Stop(); r.rise.time = Mathf.Min(-lead * pitch, riseRec.length - 0.05f); r.rise.Play(); }
    }
    void RoarUpdate(Rock r, Vector3 p, float atmo)
    {
        if (r.roar == null) return;
        var cam = Cam(); float dist = cam != null ? Vector3.Distance(cam.transform.position, p) : 800f;
        float fade = Mathf.Clamp01(r.t / 0.15f), big = 1f + 0.5f * r.size;
        r.roar.volume = Ambience.Muted ? 0f : Mathf.Clamp01((0.12f + 0.88f * atmo * atmo) * Fall(dist, 140f * big) * (0.55f + 0.2f * r.size) * fade * Quiet * (r.rise != null ? 0.55f : 1f) * (r.shower ? 0.6f : 1f));
        if (r.rise != null)   // de aanloop klinkt vanaf de plek van de inslag (daar valt zijn klap)
        {
            float dt0 = cam != null ? Vector3.Distance(cam.transform.position, r.to) : 800f;
            r.rise.volume = Ambience.Muted ? 0f : Mathf.Clamp01(Fall(dt0, 220f * big) * (0.75f + 0.1f * r.size) * Quiet);
            r.rise.panStereo = Pan(r.to) * 0.6f; var lp = r.rise.GetComponent<AudioLowPassFilter>(); if (lp != null) lp.cutoffFrequency = Mathf.Lerp(22000f, 700f, Far(dt0));
        }
        r.roar.pitch = (0.95f - 0.12f * r.size) * (0.75f + 0.55f * r.t * r.t);   // hoger als hij nadert
        r.roar.panStereo = Pan(p);
        r.roarLp.cutoffFrequency = Mathf.Lerp(1200f, 14000f, atmo) * Mathf.Lerp(1f, 0.25f, Far(dist));
        // de supersone knal: één keer, als hij de atmosfeer in duikt (twee klappen kort na elkaar: kop en staart van de schokgolf)
        if (!r.sonic && r.rise == null && !r.shower && atmo > 0.4f && !Ambience.Muted && Baked())   // met de aanloop niet: die heeft zijn eigen opbouw
        {
            r.sonic = true; var go = new GameObject("Supersoon"); go.transform.SetParent(transform, false); Destroy(go, 6f);
            float v = Fall(dist, 250f * big) * 0.6f * Quiet, d = 0f, cut = Mathf.Lerp(9000f, 1200f, Far(dist));
            Layer(go, crackClip, v, 0.8f - 0.06f * r.size, cut, d, Pan(p));
            Layer(go, crackClip, v * 0.75f, 0.84f - 0.06f * r.size, cut, d + 0.13f + 0.03f * r.size, Pan(p));
        }
    }
    void StopRoar(Rock r) { if (r.roar != null) Destroy(r.roar.gameObject); r.roar = null; }

    // ---------- berekenen ----------
    // het gebrul meteen (dat klinkt vanaf de start van de vlucht), de rest op een achtergrondthread tijdens de vlucht: geen hapering
    class Raw { public string name; public float[] d; }   // stereo om en om, al op sterkte gebracht
    static System.Threading.Tasks.Task<Raw[]> baking;
    static void MakeClips()
    {
        if (roarClip == null) roarClip = ToClip(Roar());
        LoadRecordings();
        if (crackClip == null && baking == null) baking = System.Threading.Tasks.Task.Run(() => new[] { Crack(), Boom(), Rumble(), DebrisClip(), FloodClip(), Splash() });
    }
    // klaar met rekenen? dan de clips maken (dat moet op de hoofdthread)
    static bool Baked()
    {
        if (crackClip != null) return true;
        if (baking == null || !baking.IsCompleted) return false;
        if (baking.IsFaulted) { Debug.LogWarning("Meteoorgeluid berekenen mislukt: " + baking.Exception?.GetBaseException().Message); baking = null; return false; }
        var r = baking.Result; baking = null;
        crackClip = ToClip(r[0]); boomClip = ToClip(r[1]); rumbleClip = ToClip(r[2]); debrisClip = ToClip(r[3]); floodClip = ToClip(r[4]); splashClip = ToClip(r[5]);
        return true;
    }
    static AudioClip ToClip(Raw r) { var c = AudioClip.Create(r.name, r.d.Length / 2, 2, SR, false); c.SetData(r.d, 0); return c; }

    // eenvoudige filters
    struct LP { float y; public float Run(float x, float fc) { y += (1f - Mathf.Exp(-2f * Mathf.PI * fc / SR)) * (x - y); return y; } }
    struct BP   // toestandsvariabel filter (Chamberlin): banddoorlaat met resonantie
    {
        float lo, band; public float Run(float x, float fc, float q)
        { float f = 2f * Mathf.Sin(Mathf.PI * Mathf.Min(fc, SR * 0.2f) / SR); lo += f * band; float hi = x - lo - q * band; band += f * hi; return band; }
    }
    static float N(System.Random r) => (float)r.NextDouble() * 2f - 1f;
    static float Sat(float x, float k) => (float)System.Math.Tanh(x * k) / (float)System.Math.Tanh(k);
    // N-golf: een sprong omhoog, rechtlijnig naar beneden, terug naar nul (de vorm van een schokgolf)
    static float NWave(float t, float len) => t < 0f || t > len ? 0f : 1f - 2f * t / len;

    // de klap: N-golf, een knal van ruis en de weerkaatsing op de grond
    static Raw Crack()
    {
        int n = (int)(SR * 1.2f); var L = new float[n]; var R = new float[n];
        for (int ch = 0; ch < 2; ch++)
        {
            var rnd = new System.Random(3 + ch * 17); var lp = new LP(); var lp2 = new LP(); var hp = new LP(); var o = ch == 0 ? L : R;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR, w = N(rnd);
                float nw = NWave(t, 0.014f) + 0.5f * NWave(t - 0.031f - ch * 0.004f, 0.016f);
                float hiN = w - hp.Run(w, 900f);                                       // het scherpe deel van de knal
                float burst = hiN * (0.8f * Mathf.Exp(-t / 0.035f) + 0.18f * Mathf.Exp(-t / 0.3f));
                float body = lp2.Run(w, 400f) * 3f * Mathf.Exp(-t / 0.18f);           // de klap heeft ook gewicht
                o[i] = Sat(lp.Run(nw, 6000f) * 1.1f + burst + body, 1.6f);
            }
        }
        return Stereo("klap", L, R, 0.95f);
    }
    // de dreun: zakkende lage toon met boventoon, ruis die van helder naar dof gaat, verzadigd
    static Raw Boom()
    {
        int n = SR * 5; var L = new float[n]; var R = new float[n];
        for (int ch = 0; ch < 2; ch++)
        {
            var rnd = new System.Random(7 + ch * 31); var lp = new LP(); var lp2 = new LP(); float ph = 0f; var o = ch == 0 ? L : R;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR, f = 23f + 42f * Mathf.Exp(-t * 2.6f); ph += 2f * Mathf.PI * f / SR;
                float att = Mathf.Clamp01(t / 0.004f), w = N(rnd);
                float tone = (Mathf.Sin(ph) + 0.3f * Mathf.Sin(2f * ph + 0.4f)) * Mathf.Exp(-t / 1.4f);
                float noise = lp2.Run(lp.Run(w, 120f + 1100f * Mathf.Exp(-t * 4f)), 1800f) * 4f * (Mathf.Exp(-t / 0.6f) * 0.8f + 0.2f * Mathf.Exp(-t / 2f));
                o[i] = Sat(att * (tone * 0.9f + noise), 1.4f);
            }
        }
        return Stereo("dreun", L, R, 0.95f);
    }
    // het gerommel: bruine ruis die golft, en echo's van ver weg (om en om links en rechts)
    static Raw Rumble()
    {
        int n = SR * 11; var L = new float[n]; var R = new float[n];
        // eerst één doffe dreun als echo-kern
        int kn = SR * 2; var k = new float[kn]; { var rnd = new System.Random(5); var lp = new LP(); float ph = 0f;
            for (int i = 0; i < kn; i++) { float t = (float)i / SR; ph += 2f * Mathf.PI * (30f + 15f * Mathf.Exp(-t * 3f)) / SR; k[i] = (Mathf.Sin(ph) * 0.6f + lp.Run(N(rnd), 160f) * 5f) * Mathf.Clamp01(t / 0.03f) * Mathf.Exp(-t / 0.45f); } }
        float[] at = { 0.7f, 1.55f, 2.6f, 3.9f, 5.4f, 7.1f }, g = { 0.6f, 0.45f, 0.34f, 0.24f, 0.16f, 0.1f };
        for (int ch = 0; ch < 2; ch++)
        {
            var rnd = new System.Random(11 + ch * 13); var lp = new LP(); var lp2 = new LP(); float br = 0f, m = 0.6f, mT = 0.6f; var o = ch == 0 ? L : R;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR; br = (br + N(rnd) * 0.02f) * 0.998f;
                if (i % (SR / 6) == 0) mT = 0.35f + (float)rnd.NextDouble() * 0.65f; m += (mT - m) * 0.0003f;
                o[i] = lp2.Run(lp.Run(br, 220f), 140f) * 6f * m * Mathf.SmoothStep(0f, 1f, t / 0.6f) * Mathf.Exp(-t / 3.2f);
            }
            for (int e = 0; e < at.Length; e++)
            {
                float side = (e % 2 == 0) == (ch == 0) ? 1f : 0.45f; int s0 = (int)(at[e] * SR) + ch * (int)(0.006f * SR);
                for (int i = 0; i < kn && s0 + i < n; i++) o[s0 + i] += k[i] * g[e] * side;
            }
        }
        return Stereo("gerommel", L, R, 0.9f);
    }
    // brokstukken: honderden tikjes (steen op steen) en plofjes (aarde), vooral als het puin neerkomt
    static Raw DebrisClip()
    {
        int n = (int)(SR * 5.5f); var L = new float[n]; var R = new float[n]; var rnd = new System.Random(23);
        for (int e = 0; e < 320; e++)
        {
            float t0 = 0.35f + 4.6f * Mathf.Pow((float)rnd.NextDouble(), 1.7f), amp = Mathf.Pow((float)rnd.NextDouble(), 1.5f) * Mathf.Sqrt(1f - t0 / 5.2f);
            bool thud = rnd.NextDouble() < 0.18; float fc = thud ? 70f + (float)rnd.NextDouble() * 110f : 380f * Mathf.Pow(8f, (float)rnd.NextDouble());
            float tau = thud ? 0.05f + (float)rnd.NextDouble() * 0.05f : 0.004f + (float)rnd.NextDouble() * 0.025f, q = thud ? 0.6f : 0.12f + (float)rnd.NextDouble() * 0.2f;
            float pan = N(rnd) * 0.85f, gl = Mathf.Sqrt(0.5f * (1f - pan)), gr = Mathf.Sqrt(0.5f * (1f + pan)); if (thud) amp *= 1.6f;
            var bp = new BP(); int s0 = (int)(t0 * SR), len = (int)(tau * 6f * SR);
            for (int i = 0; i < len && s0 + i < n; i++)
            {
                float t = (float)i / SR, v = bp.Run(N(rnd) * Mathf.Exp(-t / tau), fc, q) * amp;
                L[s0 + i] += v * gl; R[s0 + i] += v * gr;
            }
        }
        // en het gruis: zacht geruis van zand dat neerdaalt
        for (int ch = 0; ch < 2; ch++) { var o = ch == 0 ? L : R; var bp = new BP(); var r2 = new System.Random(41 + ch);
            for (int i = 0; i < n; i++) { float t = (float)i / SR; o[i] += bp.Run(N(r2), 2600f, 0.5f) * 0.05f * Mathf.SmoothStep(0f, 1f, (t - 0.3f) / 0.6f) * Mathf.Exp(-Mathf.Max(0f, t - 1f) / 1.6f); } }
        return Stereo("brokstukken", L, R, 0.9f);
    }
    // vlucht: brullend gesis met geknetter, een naadloze lus van 4 s
    static Raw Roar()
    {
        int n = SR * 4, x = SR / 2; var L = new float[n]; var R = new float[n];
        for (int ch = 0; ch < 2; ch++)
        {
            var rnd = new System.Random(61 + ch * 7); var lp = new LP(); var hp = new LP(); var bp = new BP(); var cb = new BP(); var full = new float[n + x]; float crack = 0f;
            for (int i = 0; i < n + x; i++)
            {
                float t = (float)i / SR, w = N(rnd);
                float flutter = 0.75f + 0.15f * Mathf.Sin(2f * Mathf.PI * 3f * t) + 0.1f * Mathf.Sin(2f * Mathf.PI * 5.5f * t + ch);
                if (rnd.NextDouble() < 28.0 / SR) crack = 0.6f + (float)rnd.NextDouble() * 0.8f; crack *= 0.993f;   // geknetter
                full[i] = (lp.Run(w, 140f) * 3.5f + bp.Run(w, 520f, 0.7f) * 0.9f + (w - hp.Run(w, 3500f)) * 0.12f) * flutter + cb.Run(w * crack, 1800f, 0.3f) * 0.8f;
            }
            var o = ch == 0 ? L : R;   // het staartje over het begin heen laten vloeien: geen tik bij het herhalen
            for (int i = 0; i < n; i++) o[i] = i < x ? full[i] * ((float)i / x) + full[n + i] * (1f - (float)i / x) : full[i];
        }
        return Stereo("gebrul", L, R, 0.8f);
    }
    // de wand breekt door: aanzwellend geruis van stromend water met geborrel
    static Raw FloodClip()
    {
        int n = SR * 6; var L = new float[n]; var R = new float[n];
        for (int ch = 0; ch < 2; ch++)
        {
            var rnd = new System.Random(71 + ch); var lp = new LP(); var bp = new BP(); var bb = new BP(); var o = ch == 0 ? L : R; float bub = 0f, bf = 400f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR, w = N(rnd), env = Mathf.SmoothStep(0f, 1f, t / 1.2f) * Mathf.Exp(-Mathf.Max(0f, t - 2.5f) / 1.4f);
                if (rnd.NextDouble() < 14.0 / SR) { bub = 0.5f + (float)rnd.NextDouble(); bf = 250f + (float)rnd.NextDouble() * 600f; } bub *= 0.9993f; bf *= 1.00004f;   // een bel: kort, stijgend
                o[i] = (lp.Run(w, 900f) * 1.4f + bp.Run(w, 1600f, 0.8f) * 0.5f) * env + bb.Run(w * 0.2f + bub * 0.02f, bf, 0.04f) * bub * 0.5f * env;
            }
        }
        return Stereo("waterstroom", L, R, 0.85f);
    }
    // plons: de klap op het water, opspattend water dat terugvalt
    static Raw Splash()
    {
        int n = SR * 4; var L = new float[n]; var R = new float[n];
        for (int ch = 0; ch < 2; ch++)
        {
            var rnd = new System.Random(81 + ch); var lp = new LP(); var hp = new LP(); var o = ch == 0 ? L : R;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR, w = N(rnd);
                float hit = lp.Run(w, 300f) * 4f * Mathf.Exp(-t / 0.25f), spray = (w - hp.Run(w, 1200f)) * (0.5f * Mathf.Exp(-t / 0.15f) + 0.35f * Mathf.Exp(-Mathf.Pow((t - 1.6f) / 0.7f, 2f)));
                o[i] = Sat(hit + spray, 1.3f);
            }
        }
        return Stereo("plons", L, R, 0.9f);
    }

    static Raw Stereo(string name, float[] L, float[] R, float peak)
    {
        float mx = 1e-6f; for (int i = 0; i < L.Length; i++) mx = Mathf.Max(mx, Mathf.Abs(L[i]), Mathf.Abs(R[i]));
        var d = new float[L.Length * 2]; float k = peak / mx; for (int i = 0; i < L.Length; i++) { d[2 * i] = L[i] * k; d[2 * i + 1] = R[i] * k; }
        return new Raw { name = name, d = d };
    }
}
