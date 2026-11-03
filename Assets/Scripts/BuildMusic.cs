using System.Collections.Generic;
using UnityEngine;

// Bouwmuziek: een deuntje als je op de planeet bent (dichtbij; bij uitzoomen gaat het over in de ruimtemuziek). Berekend,
// geen opname, in vier stijlen (menu, onthouden in PlayerPrefs "bouwmuziek"; 0 = uit):
//   Vrolijk  120 bpm, D A Bm G: marimba op de vijftonige ladder, getokkelde akkoorden op de achtertel, bas, zacht slagwerk.
//   Chill     84 bpm met swing, Gmaj7 F#m7 Em7 Dmaj7: elektrische piano, ronde bas, rimshot, zacht geruis van een plaat.
//   8-bit    140 bpm, D Bm G A: blokgolven zoals een oude spelcomputer, snelle arpeggio's, driehoeksbas, ruisslagwerk, echo.
//   Folk     100 bpm, D G D A / Bm G D A: getokkelde gitaar, een fluitje, tamboerijn en bas.
// Elke stijl: 16 maten, een melodie in vier zinnen A A' B A (eigen ritmes, kleine stapjes, op de tel een akkoordtoon),
// naadloos rond. Een andere stijl kiezen rekent op de achtergrond het nieuwe stuk uit; dan vloeit het oude weg. 's Nachts zachter.
public class BuildMusic : RenderedMusic
{
    public static readonly string[] StyleNames = { "Uit", "Vrolijk", "Chill", "8-bit", "Folk" };
    public static int Style { get; private set; } = 1;
    public static void SetStyle(int s) { Style = Mathf.Clamp(s, 0, StyleNames.Length - 1); PlayerPrefs.SetInt("bouwmuziek", Style); }

    protected override string Name => "bouwmuziek";
    protected override float Volume => 0.22f;
    protected override float Target => Style == 0 ? 0f : (1f - PlanetSky.Space) * (1f - 0.4f * DayNight.Night);

    int rendered;   // de stijl die nu speelt of wordt uitgerekend
    void Awake() { Style = Mathf.Clamp(PlayerPrefs.GetInt("bouwmuziek", 1), 0, StyleNames.Length - 1); rendered = Mathf.Max(1, Style); }
    protected override void Tick() { if (Style != 0 && Style != rendered) { rendered = Style; Rerender(); } }

    protected override float[] Render()
    {
        switch (rendered) { case 2: return Chill(); case 3: return Chip(); case 4: return Folk(); default: return Happy(); }
    }

    // ---------- een stuk in elkaar zetten ----------
    class Song
    {
        public readonly int n; public readonly float[] L, R; public readonly System.Random rnd;
        public Song(double len, int seed) { n = (int)(SR * len); L = new float[n]; R = new float[n]; rnd = new System.Random(seed); }
        public float Rand => (float)rnd.NextDouble();
        public void Add(double t0, float[] s, float pan)   // een geluidje op tijd t0 erbij (rond: wat over het eind gaat komt aan het begin)
        {
            int i0 = (int)(t0 * SR); float pl = 1f - pan, pr = pan;
            for (int j = 0; j < s.Length; j++) { int k = (i0 + j) % n; L[k] += s[j] * pl; R[k] += s[j] * pr; }
        }
        // galm: twee vertragingen die naar elkaar terugkaatsen, twee rondes zodat het einde op het begin aansluit
        public float[] Finish(double dl, double dr, float fb, float wet)
        {
            var oL = new float[n]; var oR = new float[n]; int a = (int)(SR * dl), b = (int)(SR * dr); var bl = new float[a]; var br = new float[b]; int pa = 0, pb = 0; float la = 0f, lb = 0f;
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < n; i++)
                {
                    float yl = bl[pa], yr = br[pb]; la += 0.4f * (yl - la); lb += 0.4f * (yr - lb);
                    bl[pa] = L[i] * 0.5f + lb * fb; br[pb] = R[i] * 0.5f + la * fb;
                    pa = (pa + 1) % a; pb = (pb + 1) % b;
                    if (pass == 1) { oL[i] = L[i] + la * wet; oR[i] = R[i] + lb * wet; }
                }
            return Interleave(oL, oR);
        }
    }

    // melodie in vier zinnen: A, A' (A een octaaf hoger als lift), B, A; play(tijd, toon, lengte in achtsten)
    static void Melody(Song s, int[][] progA, int[][] progB, int[] scale, string[] rhythms, double beat, bool lift, System.Action<double, int, int> play, System.Func<double, double> swing = null)
    {
        int Near(int pc, int from) { int best = from, bd = 99; for (int c = 0; c < scale.Length; c++) if (((scale[c] - pc) % 12 + 12) % 12 == 0 && Mathf.Abs(c - from) < bd) { bd = Mathf.Abs(c - from); best = c; } return best; }
        var phrases = new List<(int step, int note, int len)>[2];
        for (int p = 0; p < 2; p++)
        {
            phrases[p] = new List<(int, int, int)>(); int idx = scale.Length / 2 - 1; var prog = p == 0 ? progA : progB;
            for (int bar = 0; bar < 4; bar++)
            {
                string r = bar == 3 ? "x.x.x..." : rhythms[s.rnd.Next(rhythms.Length)];   // de zin eindigt rustig
                for (int s8 = 0; s8 < 8; s8++)
                {
                    if (r[s8] != 'x') continue;
                    idx = Mathf.Clamp(idx + s.rnd.Next(-2, 3), 0, scale.Length - 1);
                    if (s8 % 2 == 0) { int c = prog[bar][s.rnd.Next(prog[bar].Length)]; idx = Near(c, idx); }   // op de tel een akkoordtoon
                    if (bar == 3 && s8 == 4) idx = Near(p == 0 ? 64 : 62, scale.Length / 2);   // eind van de zin: E (spanning) of D (thuis)
                    int len = 1; while (s8 + len < 8 && r[s8 + len] == '.' && len < 4) len++;
                    phrases[p].Add((bar * 8 + s8, scale[idx], len));
                }
            }
        }
        int[] order = { 0, 0, 1, 0 };
        for (int ph = 0; ph < 4; ph++)
            foreach (var (step, note, len) in phrases[order[ph]])
            {
                int m = note + (lift && ph == 1 ? 12 : 0); if (ph == 3 && step == 28) m = scale[Near(62, scale.Length / 2)];   // het slot thuis op D
                double t = ph * 16 * beat + step * beat / 2; if (swing != null) t = swing(t);
                play(t, m, len);
            }
    }

    // ---------- Vrolijk ----------
    static float[] Happy()
    {
        const double B = 0.5; var s = new Song(64 * B, 12);
        int[][] ch = { new[] { 62, 66, 69 }, new[] { 61, 64, 69 }, new[] { 59, 62, 66 }, new[] { 59, 62, 67 } };   // D, A, Bm, G
        int[] roots = { 38, 45, 47, 43 };
        Melody(s, ch, ch, new[] { 69, 71, 74, 76, 78, 81, 83, 86, 88 }, new[] { "x.x.xx.x", "x..xx.x.", "x.x.x...", "xx.x.x..", "x...x.xx", "x.xxx.x." }, B, true,
            (t, m, len) => s.Add(t, Marimba(Hz(m), 0.11f, 0.25 + 0.12 * len), 0.45f + 0.1f * s.Rand));
        for (int bar = 0; bar < 16; bar++)
        {
            double t = bar * 4 * B; int c = bar % 4;
            for (int b = 0; b < 4; b++) for (int v = 0; v < 3; v++) s.Add(t + b * B + B / 2 + v * 0.012, Pluck(Hz(ch[c][v]), 0.055f, s.rnd, 0.4985f), 0.3f + 0.2f * v);
            s.Add(t, Bass(Hz(roots[c]), 0.2f, 0.22), 0.5f); s.Add(t + 2 * B, Bass(Hz(roots[c] + 7), 0.16f, 0.22), 0.5f);
            s.Add(t, Kick(0.25f), 0.5f); s.Add(t + 2 * B, Kick(0.2f), 0.5f);
            s.Add(t + B, Snap(0.07f, s.rnd), 0.55f); s.Add(t + 3 * B, Snap(0.07f, s.rnd), 0.45f);
            for (int e = 0; e < 8; e++) s.Add(t + e * B / 2, Shaker(e % 2 == 1 ? 0.035f : 0.018f, s.rnd), 0.65f);
        }
        return s.Finish(0.19, 0.23, 0.6f, 0.35f);
    }

    // ---------- Chill ----------
    static float[] Chill()
    {
        const double B = 60.0 / 84.0; var s = new Song(64 * B, 31);
        double Swing(double t) { double e = t / (B / 2); int k = (int)System.Math.Round(e); return System.Math.Abs(e - k) < 1e-6 && k % 2 == 1 ? (k - 1) * B / 2 + B * 0.62 : t; }   // de achtertel iets later
        int[][] ch = { new[] { 55, 59, 62, 66 }, new[] { 54, 57, 61, 64 }, new[] { 52, 55, 59, 62 }, new[] { 50, 54, 57, 61 } };   // Gmaj7, F#m7, Em7, Dmaj7
        int[] roots = { 43, 42, 40, 38 };
        Melody(s, ch, ch, new[] { 69, 71, 74, 76, 78, 81, 83, 86 }, new[] { "x...x...", "x..x....", "..x.x...", "x.....x.", "x.x.....", "...x..x." }, B, false,
            (t, m, len) => s.Add(t, EPiano(Hz(m), 0.09f, 0.3 + 0.3 * len, true), 0.4f + 0.2f * s.Rand), Swing);
        for (int bar = 0; bar < 16; bar++)
        {
            double t = bar * 4 * B; int c = bar % 4;
            for (int v = 0; v < 4; v++) { s.Add(t + v * 0.02, EPiano(Hz(ch[c][v]), 0.05f, 1.6 * B, false), 0.35f + 0.1f * v); s.Add(Swing(t + 2.5 * B) + v * 0.02, EPiano(Hz(ch[c][v]), 0.04f, 1.3 * B, false), 0.35f + 0.1f * v); }
            s.Add(t, Bass(Hz(roots[c]), 0.22f, 0.5), 0.5f); s.Add(Swing(t + 2.5 * B), Bass(Hz(roots[c] + 7), 0.15f, 0.35), 0.5f);
            s.Add(t, Kick(0.22f), 0.5f); s.Add(Swing(t + 2.5 * B), Kick(0.15f), 0.5f);
            s.Add(t + B, Rim(0.06f, s.rnd), 0.55f); s.Add(t + 3 * B, Rim(0.06f, s.rnd), 0.55f);
            for (int e = 0; e < 8; e++) s.Add(Swing(t + e * B / 2), Shaker(e % 2 == 0 ? 0.02f : 0.012f, s.rnd), 0.7f);
        }
        for (int k = 0; k < 220; k++) s.Add(s.Rand * s.n / SR, Crackle(0.02f * s.Rand, s.rnd), s.Rand);   // geruis van een plaat
        return s.Finish(0.31, 0.37, 0.55f, 0.45f);
    }

    // ---------- 8-bit ----------
    static float[] Chip()
    {
        const double B = 60.0 / 140.0; var s = new Song(64 * B, 8);
        int[][] ch = { new[] { 62, 66, 69 }, new[] { 59, 62, 66 }, new[] { 55, 59, 62 }, new[] { 57, 61, 64 } };   // D, Bm, G, A
        int[] roots = { 38, 35, 43, 45 };
        Melody(s, ch, ch, new[] { 74, 76, 78, 81, 83, 86, 88, 90 }, new[] { "x.xxx.x.", "xx.xx.x.", "x.x.xxx.", "xxx.x.x.", "x..xx.xx" }, B, true,
            (t, m, len) => s.Add(t, Square(Hz(m), 0.06f, len * B / 2 * 0.9, 0.25, true), 0.5f));
        for (int bar = 0; bar < 16; bar++)
        {
            double t = bar * 4 * B; int c = bar % 4;
            for (int k = 0; k < 16; k++) s.Add(t + k * B / 4, Square(Hz(ch[c][k % 3] + 12 * (k / 3 % 2)), 0.018f, B / 4 * 0.85, 0.125, false), 0.65f);   // arpeggio
            int[] bp = { 0, 0, 12, 0, 7, 0, 12, 7 };
            for (int e = 0; e < 8; e++) s.Add(t + e * B / 2, Triangle(Hz(roots[c] + bp[e]), 0.13f, B / 2 * 0.85), 0.5f);
            s.Add(t, ChipKick(0.22f), 0.5f); s.Add(t + 2 * B, ChipKick(0.22f), 0.5f); s.Add(t + 2.5 * B, ChipKick(0.14f), 0.5f);
            s.Add(t + B, NoiseHit(0.09f, 0.08, s.rnd), 0.5f); s.Add(t + 3 * B, NoiseHit(0.09f, 0.08, s.rnd), 0.5f);
            for (int e = 0; e < 8; e++) s.Add(t + e * B / 2, NoiseHit(0.025f, 0.02, s.rnd), 0.4f);
        }
        return s.Finish(B * 0.75, B * 0.75, 0.3f, 0.3f);
    }

    // ---------- Folk ----------
    static float[] Folk()
    {
        const double B = 0.6; var s = new Song(64 * B, 5);
        int[] D = { 50, 57, 62, 66 }, G = { 43, 50, 55, 59 }, A = { 45, 52, 57, 61 }, Bm = { 47, 54, 59, 62 };
        int[][] progA = { D, G, D, A }, progB = { Bm, G, D, A };
        Melody(s, progA, progB, new[] { 69, 71, 74, 76, 78, 81, 83, 86 }, new[] { "x.x.x.x.", "x..xx...", "x.xxx...", "x...x.x.", "xx.xx..." }, B, false,
            (t, m, len) => s.Add(t, Flute(Hz(m), 0.08f, len * B / 2 * 0.95, s.rnd), 0.55f));
        int[] pick = { 0, 2, 1, 3, 2, 1, 3, 2 };   // duim op de grondtoon, dan de hoge snaren om en om
        for (int bar = 0; bar < 16; bar++)
        {
            double t = bar * 4 * B; var c = (bar / 4 == 2 ? progB : progA)[bar % 4];
            for (int e = 0; e < 8; e++) s.Add(t + e * B / 2, Pluck(Hz(c[pick[e]]), e == 0 ? 0.08f : 0.055f, s.rnd, 0.499f), 0.3f + 0.12f * pick[e]);
            s.Add(t, Bass(Hz(c[0] - 12 < 36 ? c[0] : c[0] - 12), 0.16f, 0.4), 0.5f); s.Add(t + 2 * B, Bass(Hz((c[0] - 12 < 36 ? c[0] : c[0] - 12) + 7), 0.13f, 0.4), 0.5f);
            s.Add(t + B, Tambourine(0.05f, s.rnd), 0.6f); s.Add(t + 3 * B, Tambourine(0.05f, s.rnd), 0.6f);
            for (int e = 1; e < 8; e += 2) s.Add(t + e * B / 2, Shaker(0.012f, s.rnd), 0.65f);
        }
        return s.Finish(0.23, 0.29, 0.55f, 0.4f);
    }

    // ---------- klanken ----------
    static float[] Marimba(double f, float amp, double dur)
    {
        int len = (int)(SR * (dur + 0.6)); var s = new float[len]; double w = 2 * System.Math.PI * f;
        for (int j = 0; j < len; j++)
        {
            double t = (double)j / SR, env = System.Math.Min(1.0, t / 0.003) * System.Math.Exp(-t / 0.32);
            s[j] = (float)((System.Math.Sin(w * t) + 0.35 * System.Math.Exp(-t / 0.05) * System.Math.Sin(4.0 * w * t) + 0.12 * System.Math.Sin(2.0 * w * t)) * env * amp);
        }
        return s;
    }

    static float[] EPiano(double f, float amp, double dur, bool bright)   // elektrische piano: zachte FM met een tikje erin
    {
        int len = (int)(SR * (dur + 0.8)); var s = new float[len]; double w = 2 * System.Math.PI * f;
        for (int j = 0; j < len; j++)
        {
            double t = (double)j / SR, env = System.Math.Min(1.0, t / 0.004) * System.Math.Exp(-t / 1.3) * (t > dur ? System.Math.Exp(-(t - dur) / 0.15) : 1.0);
            double mod = (bright ? 1.1 : 0.7) * System.Math.Exp(-t / 0.5) * System.Math.Sin(w * t);
            double v = System.Math.Sin(w * t + mod) + 0.12 * System.Math.Exp(-t / 0.04) * System.Math.Sin(7.0 * w * t);
            s[j] = (float)(v * env * amp * (1.0 + 0.08 * System.Math.Sin(2 * System.Math.PI * 4.5 * t)));
        }
        return s;
    }

    static float[] Pluck(double f, float amp, System.Random rnd, float decay)   // Karplus-Strong: een snaar uit een buffertje ruis
    {
        int len = (int)(SR * 0.8), p = Mathf.Max(2, (int)(SR / f)); var buf = new float[p]; var s = new float[len];
        for (int j = 0; j < p; j++) buf[j] = (float)(rnd.NextDouble() * 2 - 1);
        for (int j = 0; j < len; j++) { int a = j % p, b = (j + 1) % p; float y = buf[a]; buf[a] = decay * (buf[a] + buf[b]); s[j] = y * amp * Mathf.Min(1f, (len - j) / (SR * 0.05f)); }
        return s;
    }

    static float[] Bass(double f, float amp, double decay)
    {
        int len = (int)(SR * (decay * 2.2)); var s = new float[len]; double w = 2 * System.Math.PI * f;
        for (int j = 0; j < len; j++) { double t = (double)j / SR, env = System.Math.Min(1.0, t / 0.005) * System.Math.Exp(-t / decay) * System.Math.Min(1.0, (len - j) / (SR * 0.02)); s[j] = (float)((System.Math.Sin(w * t) + 0.3 * System.Math.Sin(2 * w * t)) * env * amp); }
        return s;
    }

    static float[] Square(double f, float amp, double dur, double duty, bool vibrato)   // blokgolf, iets afgerond tegen het schrille
    {
        int len = (int)(SR * (dur + 0.02)); var s = new float[len]; double ph = 0; float lp = 0f;
        for (int j = 0; j < len; j++)
        {
            double t = (double)j / SR, fv = vibrato && t > 0.12 ? f * (1 + 0.006 * System.Math.Sin(2 * System.Math.PI * 6 * t)) : f;
            ph += fv / SR; float v = (ph % 1.0) < duty ? 1f : -1f; lp += 0.5f * (v - lp);
            s[j] = lp * amp * (t > dur ? 0f : 1f) * Mathf.Min(1f, j / (SR * 0.002f));
        }
        return s;
    }

    static float[] Triangle(double f, float amp, double dur)
    {
        int len = (int)(SR * dur); var s = new float[len]; double ph = 0;
        for (int j = 0; j < len; j++) { ph += f / SR; double x = ph % 1.0; s[j] = (float)((x < 0.5 ? 4 * x - 1 : 3 - 4 * x) * amp * System.Math.Min(1.0, (len - j) / (SR * 0.004))); }
        return s;
    }

    static float[] Flute(double f, float amp, double dur, System.Random rnd)   // fluitje: zacht, met vibrato en wat adem
    {
        int len = (int)(SR * (dur + 0.12)); var s = new float[len]; double ph = 0; float lp = 0f;
        for (int j = 0; j < len; j++)
        {
            double t = (double)j / SR, env = System.Math.Min(1.0, t / 0.05) * (t > dur ? System.Math.Exp(-(t - dur) / 0.04) : 1.0);
            ph += f * (1 + (t > 0.15 ? 0.005 * System.Math.Sin(2 * System.Math.PI * 5.2 * t) : 0)) / SR;
            float n = (float)(rnd.NextDouble() * 2 - 1); lp += 0.2f * (n - lp);
            s[j] = (float)((System.Math.Sin(2 * System.Math.PI * ph) + 0.18 * System.Math.Sin(4 * System.Math.PI * ph) + lp * 0.12) * env * amp);
        }
        return s;
    }

    static float[] Kick(float amp)
    {
        int len = (int)(SR * 0.25); var s = new float[len]; double ph = 0;
        for (int j = 0; j < len; j++) { double t = (double)j / SR, f = 45 + 75 * System.Math.Exp(-t / 0.03); ph += 2 * System.Math.PI * f / SR; s[j] = (float)(System.Math.Sin(ph) * System.Math.Exp(-t / 0.1) * amp); }
        return s;
    }

    static float[] ChipKick(float amp)   // bas trom van een spelcomputer: blokgolf die snel zakt
    {
        int len = (int)(SR * 0.12); var s = new float[len]; double ph = 0;
        for (int j = 0; j < len; j++) { double t = (double)j / SR; ph += (50 + 180 * System.Math.Exp(-t / 0.02)) / SR; s[j] = ((ph % 1.0) < 0.5 ? 1f : -1f) * (float)System.Math.Exp(-t / 0.04) * amp; }
        return s;
    }

    static float[] NoiseHit(float amp, double decay, System.Random rnd)
    {
        int len = (int)(SR * decay * 4); var s = new float[len]; float hold = 0f;
        for (int j = 0; j < len; j++) { if (j % 3 == 0) hold = (float)(rnd.NextDouble() * 2 - 1); s[j] = hold * Mathf.Exp(-j / (float)(SR * decay)) * amp; }   // grove ruis, zoals de oude chips
        return s;
    }

    static float[] Snap(float amp, System.Random rnd)   // vingerknip: korte ruis zonder het lage deel
    {
        int len = (int)(SR * 0.12); var s = new float[len]; float last = 0f, lp = 0f;
        for (int j = 0; j < len; j++) { float w = (float)(rnd.NextDouble() * 2 - 1); lp += 0.35f * (w - lp); float hp = lp - last; last = lp; s[j] = hp * 3f * Mathf.Exp(-j / (SR * 0.025f)) * amp; }
        return s;
    }

    static float[] Rim(float amp, System.Random rnd)   // rimshot: een houten tik
    {
        int len = (int)(SR * 0.08); var s = new float[len];
        for (int j = 0; j < len; j++) { double t = (double)j / SR; s[j] = (float)((System.Math.Sin(2 * System.Math.PI * 1650 * t) * 0.6 + (rnd.NextDouble() * 2 - 1) * 0.4) * System.Math.Exp(-t / 0.012) * amp); }
        return s;
    }

    static float[] Shaker(float amp, System.Random rnd)
    {
        int len = (int)(SR * 0.06); var s = new float[len]; float last = 0f;
        for (int j = 0; j < len; j++) { float w = (float)(rnd.NextDouble() * 2 - 1); float hp = w - last; last = w; float env = Mathf.Min(1f, j / (SR * 0.008f)) * Mathf.Exp(-j / (SR * 0.018f)); s[j] = hp * env * amp; }
        return s;
    }

    static float[] Tambourine(float amp, System.Random rnd)   // ruis met belletjes
    {
        int len = (int)(SR * 0.2); var s = new float[len]; float last = 0f;
        for (int j = 0; j < len; j++) { double t = (double)j / SR; float w = (float)(rnd.NextDouble() * 2 - 1); float hp = w - last; last = w; s[j] = (float)((hp * 0.7 + System.Math.Sin(2 * System.Math.PI * 5200 * t) * 0.3 * (rnd.NextDouble() > 0.5 ? 1 : 0.4)) * System.Math.Exp(-t / 0.05) * amp); }
        return s;
    }

    static float[] Crackle(float amp, System.Random rnd)
    {
        int len = (int)(SR * 0.004); var s = new float[len];
        for (int j = 0; j < len; j++) s[j] = (float)(rnd.NextDouble() * 2 - 1) * Mathf.Exp(-j / (SR * 0.0008f)) * amp;
        return s;
    }
}
