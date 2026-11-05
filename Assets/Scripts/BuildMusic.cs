using System.Collections.Generic;
using UnityEngine;

// Bouwmuziek: een deuntje als je op de planeet bent (dichtbij; bij uitzoomen gaat het over in de ruimtemuziek). Berekend,
// geen opname, in vier stijlen (menu, onthouden in PlayerPrefs "bouwmuziek"; 0 = uit):
//   Vrolijk  120 bpm, D A Bm G: marimba op de vijftonige ladder, getokkelde akkoorden op de achtertel, bas, zacht slagwerk.
//   Chill     84 bpm met swing, Gmaj7 F#m7 Em7 Dmaj7: elektrische piano, ronde bas, rimshot, zacht geruis van een plaat.
//   8-bit    140 bpm, D Bm G A: blokgolven zoals een oude spelcomputer, snelle arpeggio's, driehoeksbas, ruisslagwerk, echo.
//   Folk     100 bpm, D G D A / Bm G D A: getokkelde gitaar, een fluitje, tamboerijn en bas.
//   Synth     90 bpm, Fmaj9 Em7 Dm7 Cmaj7: zaagtandpads die op de bas trom ademen, synthbas, plukkende lead, arpeggio's,
//            het sinusfiguurtje met echo, bas trom, handklap en hihat.
// Elke stijl heeft een opbouw van 52-60 maten (2-2,5 minuut, naadloos rond, Form): intro, couplet, refrein met andere akkoorden,
// een rustig stuk met een zacht arpeggio, een aanloop, het couplet een octaaf hoger met een tegenmelodie, refrein en een slot dat op de
// intro aansluit; zes melodiezinnen (eigen ritmes, kleine stapjes, op de tel een akkoordtoon). Een andere stijl kiezen rekent op de achtergrond het nieuwe stuk uit; dan vloeit het oude weg. 's Nachts zachter.
public class BuildMusic : RenderedMusic
{
    public static readonly string[] StyleNames = { "Uit", "Vrolijk", "Chill", "8-bit", "Folk", "Synth" };
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
        switch (rendered) { case 2: return Chill(); case 3: return Chip(); case 4: return Folk(); case 5: return Synth(); default: return Happy(); }
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

    // ---------- opbouw: delen van een stuk ----------
    // Een deel: een akkoordenschema van 4 maten, per blok van 4 maten een melodiezin (-1 = geen) en de lagen die meespelen:
    // d slagwerk, h half slagwerk, b bas, c akkoorden, x tegenmelodie, s zacht arpeggio, f roffel in de laatste maat,
    // r aanloop (ruis die opzwelt) in de laatste twee maten, o melodie een octaaf hoger.
    struct Part
    {
        public int prog; public string on; public int[] mel;
        public Part(int prog, string on, params int[] mel) { this.prog = prog; this.on = on; this.mel = mel; }
        public bool Has(char c) => on.IndexOf(c) >= 0;
        public int Bars => mel.Length * 4;
    }
    // het vaste verloop: intro, couplet (twice: twee keer), refrein, rustig stuk met een zacht arpeggio, aanloop, couplet hoger met
    // tegenmelodie, refrein, slot (sluit aan op de intro)
    static Part[] Form(bool twice)
    {
        var l = new List<Part> { new Part(0, "cs", -1), new Part(0, "dbc", 0, 1) };
        if (twice) l.Add(new Part(0, "dbcx", 0, 1));
        l.AddRange(new[] { new Part(1, "dbcf", 2, 3), new Part(2, "cs", 4, 5), new Part(0, "hbcr", -1), new Part(0, "dbcxo", 0, 1), new Part(1, "dbcxf", 2, 3), new Part(0, "cs", 1) });
        return l.ToArray();
    }
    static int TotalBars(Part[] parts) { int n = 0; foreach (var p in parts) n += p.Bars; return n; }

    // akkoord: eerst de grondtoon voor de bas, dan de ligging
    static int[] C(int root, params int[] v) { var a = new int[v.Length + 1]; a[0] = root; v.CopyTo(a, 1); return a; }
    static int[] Voicing(int[] ch) { var v = new int[ch.Length - 1]; System.Array.Copy(ch, 1, v, 0, v.Length); return v; }

    // een melodiezin van 4 maten: eigen ritmes, kleine stapjes, op de tel een akkoordtoon; home: eindigt op D, anders op E (spanning)
    static List<(int step, int note, int len)> Phrase(Song s, int[][] prog, int[] scale, string[] rhythms, bool home)
    {
        int Near(int pc, int from) { int best = from, bd = 99; for (int c = 0; c < scale.Length; c++) if (((scale[c] - pc) % 12 + 12) % 12 == 0 && Mathf.Abs(c - from) < bd) { bd = Mathf.Abs(c - from); best = c; } return best; }
        var ph = new List<(int, int, int)>(); int idx = scale.Length / 2 - 1;
        for (int bar = 0; bar < 4; bar++)
        {
            string r = bar == 3 ? "x.x.x..." : rhythms[s.rnd.Next(rhythms.Length)];   // de zin eindigt rustig
            var ch = Voicing(prog[bar]);
            for (int s8 = 0; s8 < 8; s8++)
            {
                if (r[s8] != 'x') continue;
                idx = Mathf.Clamp(idx + s.rnd.Next(-2, 3), 0, scale.Length - 1);
                if (s8 % 2 == 0) idx = Near(ch[s.rnd.Next(ch.Length)], idx);
                if (bar == 3 && s8 == 4) idx = Near(home ? 62 : 64, scale.Length / 2);
                int len = 1; while (s8 + len < 8 && r[s8 + len] == '.' && len < 4) len++;
                ph.Add((bar * 8 + s8, scale[idx], len));
            }
        }
        return ph;
    }
    // zes zinnen: 0 en 1 op het couplet, 2 en 3 op het refrein, 4 en 5 op het rustige stuk
    static List<(int step, int note, int len)>[] Phrases(Song s, int[][][] progs, int[] scale, string[] rhythms)
    {
        var a = new List<(int step, int note, int len)>[6];
        for (int k = 0; k < 6; k++) a[k] = Phrase(s, progs[k / 2], scale, rhythms, k % 2 == 1);
        return a;
    }

    // het stuk spelen: bar(tijd, maat in het deel, akkoord, deel) voor de begeleiding, play(tijd, toon, lengte in achtsten, deel)
    // voor de melodie; een bekken als het slagwerk invalt en een aanloop waar 'r' staat
    static void Arrange(Song s, Part[] parts, int[][][] progs, List<(int step, int note, int len)>[] phrases, double beat,
        System.Action<double, int, int[], Part> bar, System.Action<double, int, int, Part> play, System.Func<double, double> swing = null)
    {
        double t = 0; bool drums = true;
        foreach (var p in parts)
        {
            for (int k = 0; k < p.Bars; k++) bar(t + k * 4 * beat, k, progs[p.prog][k % 4], p);
            for (int blk = 0; blk < p.mel.Length; blk++)
                if (p.mel[blk] >= 0)
                    foreach (var (st, note, len) in phrases[p.mel[blk]]) { double tt = t + blk * 16 * beat + st * beat / 2; if (swing != null) tt = swing(tt); play(tt, note + (p.Has('o') ? 12 : 0), len, p); }
            if (p.Has('d') && !drums) s.Add(t, Crash(0.05f, s.rnd), 0.6f);
            if (p.Has('r')) s.Add(t + (p.Bars - 2) * 4 * beat, Riser(8 * beat, 0.045f, s.rnd), 0.5f);
            drums = p.Has('d'); t += p.Bars * 4 * beat;
        }
    }
    // figuurtje met echo (gemeten aan een opname: drie korte, aangehouden sinustonen achter elkaar, legato, en een echo op de tel):
    // de toon boven een hoge akkoordtoon (55 ms), de akkoordtoon zelf (G5–E6, 100 ms), dan een lagere akkoordtoon (140 ms, het
    // luidst); de echo herhaalt het zes keer in het midden, elke keer ×0,56 (−5 dB) en een tikje doffer
    static void EchoFigure(Song s, double t, double beat, int[] ch, int[] scale, float amp)
    {
        var v = Voicing(ch); int top = -1, low = -1;
        foreach (int n in v) for (int o = 12; o <= 36; o += 12) { int m = n + o; if (m >= 79 && m <= 88 && m > top) top = m; }
        if (top < 0) return;
        foreach (int n in v) for (int o = 12; o <= 36; o += 12) { int m = n + o; if (m <= top - 4 && m >= top - 10 && m > low) low = m; }
        int grace = top + 1; while (System.Array.IndexOf(scale, grace % 12) < 0) grace++;   // de volgende toon van de toonladder
        var fig = new float[(int)(SR * 1.0)];
        void Put(float[] x, int at) { for (int j = 0; j < x.Length && at + j < fig.Length; j++) fig[at + j] += x[j]; }
        Put(SineNote(Hz(grace), amp * 0.8f, 0.055), 0);
        Put(SineNote(Hz(top), amp * 0.75f, 0.1), (int)(SR * 0.055));
        if (low > 0) Put(SineNote(Hz(low), amp, 0.14), (int)(SR * 0.185));
        float lp = 0f, k = 1f;
        for (int r = 0; r < 7; r++)
        {
            if (r > 0) { k = 0.8f; for (int j = 0; j < fig.Length; j++) { lp += k * (fig[j] - lp); fig[j] = lp * 0.56f; } }   // elke herhaling zachter en een tikje doffer
            s.Add(t + r * beat, fig, r % 2 == 1 ? 0.45f : 0.55f);
        }
    }

    // rustig stuk: een zacht synth-arpeggio over de akkoordtonen (eerst waren dit belletjes)
    static void Sparkle(Song s, double t, double beat, int[] ch, float amp)
    {
        var v = Voicing(ch);
        for (int e = 0; e < 8; e++) if (e % 4 != 3) s.Add(t + e * beat / 2, SynthPluck(Hz(v[e % v.Length] + 12), amp * 0.8f, 0.6), e % 2 == 0 ? 0.3f : 0.7f);
    }

    // ---------- Vrolijk ----------
    static float[] Happy()
    {
        const double B = 0.5; var parts = Form(true); var s = new Song(TotalBars(parts) * 4 * B, 12);
        int[][][] progs = {
            new[] { C(38, 62, 66, 69), C(45, 61, 64, 69), C(47, 59, 62, 66), C(43, 59, 62, 67) },   // D A Bm G
            new[] { C(43, 59, 62, 67), C(45, 61, 64, 69), C(42, 61, 66, 69), C(47, 59, 62, 66) },   // G A F#m Bm
            new[] { C(40, 59, 64, 67), C(43, 59, 62, 67), C(38, 62, 66, 69), C(45, 61, 64, 69) } }; // Em G D A
        var phrases = Phrases(s, progs, new[] { 69, 71, 74, 76, 78, 81, 83, 86, 88 }, new[] { "x.x.xx.x", "x..xx.x.", "x.x.x...", "xx.x.x..", "x...x.xx", "x.xxx.x." });
        Arrange(s, parts, progs, phrases, B,
            (t, k, ch, p) =>
            {
                var v = Voicing(ch); bool last = k == p.Bars - 1;
                if (p.Has('c')) for (int b = 0; b < 4; b++) for (int i = 0; i < 3; i++) s.Add(t + b * B + B / 2 + i * 0.012, Pluck(Hz(v[i]), p.Has('d') ? 0.055f : 0.04f, s.rnd, 0.4985f), 0.3f + 0.2f * i);
                if (p.Has('b')) { s.Add(t, Bass(Hz(ch[0]), 0.2f, 0.22), 0.5f); s.Add(t + 2 * B, Bass(Hz(ch[0] + 7), 0.16f, 0.22), 0.5f); }
                if (p.Has('d') || p.Has('h')) { s.Add(t, Kick(0.25f), 0.5f); if (p.Has('d')) s.Add(t + 2 * B, Kick(0.2f), 0.5f); }
                if (p.Has('d')) { s.Add(t + B, Snap(0.07f, s.rnd), 0.55f); s.Add(t + 3 * B, Snap(0.07f, s.rnd), 0.45f); }
                if (p.Has('d') || p.Has('h')) for (int e = 0; e < 8; e++) s.Add(t + e * B / 2, Shaker(e % 2 == 1 ? 0.035f : 0.018f, s.rnd), 0.65f);
                if (p.Has('x')) for (int e = 0; e < 8; e++) s.Add(t + e * B / 2, Marimba(Hz(v[(e * 2 + k) % 3] + 12), 0.035f, 0.2), 0.75f);   // tegenmelodie: marimba-arpeggio
                if (p.Has('s')) Sparkle(s, t, B, ch, 0.025f);
                if (p.Has('f') && last) for (int e = 0; e < 8; e++) s.Add(t + 2 * B + e * B / 4, Snap(0.03f + 0.006f * e, s.rnd), 0.3f + 0.05f * e);
            },
            (t, m, len, p) => s.Add(t, Marimba(Hz(m), p.Has('d') ? 0.11f : 0.08f, 0.25 + 0.12 * len), 0.45f + 0.1f * s.Rand));
        return s.Finish(0.19, 0.23, 0.6f, 0.35f);
    }

    // ---------- Chill ----------
    static float[] Chill()
    {
        const double B = 60.0 / 84.0; var parts = Form(false); var s = new Song(TotalBars(parts) * 4 * B, 31);
        double Swing(double t) { double e = t / (B / 2); int k = (int)System.Math.Round(e); return System.Math.Abs(e - k) < 1e-6 && k % 2 == 1 ? (k - 1) * B / 2 + B * 0.62 : t; }   // de achtertel iets later
        int[][][] progs = {
            new[] { C(43, 55, 59, 62, 66), C(42, 54, 57, 61, 64), C(40, 52, 55, 59, 62), C(38, 50, 54, 57, 61) },   // Gmaj7 F#m7 Em7 Dmaj7
            new[] { C(36, 52, 55, 59, 64), C(47, 54, 57, 62, 66), C(45, 52, 55, 60, 64), C(38, 54, 57, 60, 64) },   // Cmaj7 Bm7 Am7 D9
            new[] { C(40, 55, 59, 62, 66), C(45, 55, 57, 60, 64), C(38, 54, 57, 61, 64), C(43, 54, 57, 59, 62) } }; // Em9 Am7 Dmaj7 Gmaj7
        var phrases = Phrases(s, progs, new[] { 69, 71, 74, 76, 78, 81, 83, 86 }, new[] { "x...x...", "x..x....", "..x.x...", "x.....x.", "x.x.....", "...x..x." });
        Arrange(s, parts, progs, phrases, B,
            (t, k, ch, p) =>
            {
                var v = Voicing(ch); bool groove = p.Has('d') || p.Has('h');
                if (p.Has('c')) for (int i = 0; i < v.Length; i++) { s.Add(t + i * 0.02, EPiano(Hz(v[i]), 0.05f, 1.6 * B, false), 0.35f + 0.1f * i); if (groove) s.Add(Swing(t + 2.5 * B) + i * 0.02, EPiano(Hz(v[i]), 0.04f, 1.3 * B, false), 0.35f + 0.1f * i); }
                if (p.Has('b')) { s.Add(t, Bass(Hz(ch[0]), 0.22f, 0.5), 0.5f); s.Add(Swing(t + 2.5 * B), Bass(Hz(ch[0] + 7), 0.15f, 0.35), 0.5f); }
                if (groove) { s.Add(t, Kick(0.22f), 0.5f); s.Add(Swing(t + 2.5 * B), Kick(0.15f), 0.5f); }
                if (p.Has('d')) { s.Add(t + B, Rim(0.06f, s.rnd), 0.55f); s.Add(t + 3 * B, Rim(0.06f, s.rnd), 0.55f); for (int e = 0; e < 8; e++) s.Add(Swing(t + e * B / 2), Shaker(e % 2 == 0 ? 0.02f : 0.012f, s.rnd), 0.7f); }
                if (p.Has('x') && k % 2 == 0) for (int e = 0; e < 4; e++) s.Add(Swing(t + (e * 2 + 1) * B / 2), SynthPluck(Hz(v[(e + k) % v.Length] + 12), 0.03f, 0.7), 0.25f + 0.5f * (e % 2));   // tegenmelodie: synthplukjes
                if (p.Has('s')) Sparkle(s, t, B, ch, 0.02f);
                if (k % 8 == 3 && !p.Has('r')) EchoFigure(s, Swing(t + 2 * B), B, ch, new[] { 7, 9, 11, 0, 2, 4, 6 }, p.Has('d') ? 0.05f : 0.06f);   // glazen figuurtje met echo aan het eind van een zin
                if (p.Has('f') && k == p.Bars - 1) for (int e = 0; e < 4; e++) s.Add(t + 3 * B + e * B / 4, Rim(0.03f + 0.008f * e, s.rnd), 0.4f + 0.1f * e);
            },
            (t, m, len, p) => s.Add(t, EPiano(Hz(m), p.Has('d') ? 0.09f : 0.07f, 0.3 + 0.3 * len, true), 0.4f + 0.2f * s.Rand), Swing);
        for (int k = 0; k < s.n / SR * 4; k++) s.Add(s.Rand * s.n / SR, Crackle(0.02f * s.Rand, s.rnd), s.Rand);   // geruis van een plaat
        return s.Finish(0.31, 0.37, 0.55f, 0.45f);
    }

    // ---------- 8-bit ----------
    static float[] Chip()
    {
        const double B = 60.0 / 140.0; var parts = Form(true); var s = new Song(TotalBars(parts) * 4 * B, 8);
        int[][][] progs = {
            new[] { C(38, 62, 66, 69), C(35, 59, 62, 66), C(43, 55, 59, 62), C(45, 57, 61, 64) },   // D Bm G A
            new[] { C(43, 55, 59, 62), C(45, 57, 61, 64), C(42, 54, 57, 61), C(47, 59, 62, 66) },   // G A F#m Bm
            new[] { C(40, 55, 59, 64), C(36, 55, 60, 64), C(38, 54, 57, 62), C(45, 57, 61, 64) } }; // Em C D A
        var phrases = Phrases(s, progs, new[] { 74, 76, 78, 81, 83, 86, 88, 90 }, new[] { "x.xxx.x.", "xx.xx.x.", "x.x.xxx.", "xxx.x.x.", "x..xx.xx" });
        int[] bp = { 0, 0, 12, 0, 7, 0, 12, 7 };
        Arrange(s, parts, progs, phrases, B,
            (t, k, ch, p) =>
            {
                var v = Voicing(ch); bool groove = p.Has('d') || p.Has('h');
                if (p.Has('c')) for (int i = 0; i < 16; i++) s.Add(t + i * B / 4, Square(Hz(v[i % 3] + 12 * (i / 3 % 2)), p.Has('d') ? 0.018f : 0.013f, B / 4 * 0.85, 0.125, false), 0.65f);   // arpeggio
                if (p.Has('b')) for (int e = 0; e < 8; e++) s.Add(t + e * B / 2, Triangle(Hz(ch[0] + bp[e]), 0.13f, B / 2 * 0.85), 0.5f);
                if (groove) { s.Add(t, ChipKick(0.22f), 0.5f); s.Add(t + 2 * B, ChipKick(0.22f), 0.5f); for (int e = 0; e < 8; e++) s.Add(t + e * B / 2, NoiseHit(0.025f, 0.02, s.rnd), 0.4f); }
                if (p.Has('d')) { s.Add(t + 2.5 * B, ChipKick(0.14f), 0.5f); s.Add(t + B, NoiseHit(0.09f, 0.08, s.rnd), 0.5f); s.Add(t + 3 * B, NoiseHit(0.09f, 0.08, s.rnd), 0.5f); }
                if (p.Has('x')) for (int e = 0; e < 4; e++) s.Add(t + e * B, Square(Hz(v[(e + k) % 3] + 24), 0.02f, B * 0.5, 0.5, true), 0.3f);   // tegenmelodie: hoog piepje op de tel
                if (p.Has('s')) for (int e = 0; e < 8; e++) if (s.Rand < 0.3f) s.Add(t + e * B / 2, Square(Hz(v[s.rnd.Next(3)] + 24), 0.015f, B / 4, 0.25, false), s.Rand);
                if (p.Has('f') && k == p.Bars - 1) for (int e = 0; e < 8; e++) s.Add(t + 2 * B + e * B / 4, NoiseHit(0.04f + 0.006f * e, 0.03, s.rnd), 0.5f);
            },
            (t, m, len, p) => s.Add(t, Square(Hz(m), p.Has('d') ? 0.06f : 0.045f, len * B / 2 * 0.9, 0.25, true), 0.5f));
        return s.Finish(B * 0.75, B * 0.75, 0.3f, 0.3f);
    }

    // ---------- Folk ----------
    static float[] Folk()
    {
        const double B = 0.6; var parts = Form(false); var s = new Song(TotalBars(parts) * 4 * B, 5);
        int[] D = C(38, 50, 57, 62, 66), G = C(43, 43, 50, 55, 59), A = C(45, 45, 52, 57, 61), Bm = C(47, 47, 54, 59, 62), Em = C(40, 52, 55, 59, 64);
        int[][][] progs = { new[] { D, G, D, A }, new[] { Bm, G, D, A }, new[] { G, D, Em, A } };
        var phrases = Phrases(s, progs, new[] { 69, 71, 74, 76, 78, 81, 83, 86 }, new[] { "x.x.x.x.", "x..xx...", "x.xxx...", "x...x.x.", "xx.xx..." });
        int[] pick = { 0, 2, 1, 3, 2, 1, 3, 2 };   // duim op de grondtoon, dan de hoge snaren om en om
        Arrange(s, parts, progs, phrases, B,
            (t, k, ch, p) =>
            {
                var v = Voicing(ch);
                if (p.Has('c')) for (int e = 0; e < 8; e++) s.Add(t + e * B / 2, Pluck(Hz(v[pick[e]]), e == 0 ? 0.08f : 0.055f, s.rnd, 0.499f), 0.3f + 0.12f * pick[e]);
                if (p.Has('b')) { s.Add(t, Bass(Hz(ch[0]), 0.16f, 0.4), 0.5f); s.Add(t + 2 * B, Bass(Hz(ch[0] + 7), 0.13f, 0.4), 0.5f); }
                if (p.Has('d')) { s.Add(t + B, Tambourine(0.05f, s.rnd), 0.6f); s.Add(t + 3 * B, Tambourine(0.05f, s.rnd), 0.6f); }
                if (p.Has('d') || p.Has('h')) for (int e = 1; e < 8; e += 2) s.Add(t + e * B / 2, Shaker(0.012f, s.rnd), 0.65f);
                if (p.Has('x')) for (int e = 0; e < 16; e++) s.Add(t + e * B / 4, Pluck(Hz(v[3 - e % 2] + 12), 0.022f, s.rnd, 0.497f), 0.7f);   // tegenmelodie: mandoline die tremoleert
                if (p.Has('s')) Sparkle(s, t, B, ch, 0.018f);
                if (p.Has('f') && k == p.Bars - 1) for (int e = 0; e < 4; e++) s.Add(t + 3 * B + e * B / 4, Tambourine(0.03f + 0.01f * e, s.rnd), 0.6f);
            },
            (t, m, len, p) => s.Add(t, Flute(Hz(m), p.Has('d') ? 0.08f : 0.065f, len * B / 2 * 0.95, s.rnd), 0.55f));
        return s.Finish(0.23, 0.29, 0.55f, 0.4f);
    }

    // ---------- Synth ----------
    // 90 bpm in C: zachte pads van zaagtanden die op de bas trom 'ademen' (sidechain), een gefilterde synthbas, een plukkende
    // lead, arpeggio's, het sinusfiguurtje met echo en elektronisch slagwerk (bas trom, handklap, hihat)
    static float[] Synth()
    {
        const double B = 60.0 / 90.0; var parts = Form(false); var s = new Song(TotalBars(parts) * 4 * B, 47);
        int[][][] progs = {
            new[] { C(41, 57, 60, 64, 67), C(40, 55, 59, 62, 67), C(38, 57, 60, 62, 65), C(36, 55, 59, 60, 64) },   // Fmaj9 Em7 Dm7 Cmaj7
            new[] { C(45, 55, 60, 64, 67), C(41, 57, 60, 64, 69), C(43, 55, 59, 62, 64), C(40, 55, 59, 62, 67) },   // Am7 Fmaj7 G6 Em7
            new[] { C(38, 53, 57, 60, 64), C(43, 53, 57, 59, 64), C(36, 52, 55, 59, 62), C(45, 55, 59, 60, 64) } }; // Dm9 G13 Cmaj9 Am9
        var phrases = Phrases(s, progs, new[] { 67, 69, 72, 74, 76, 79, 81, 84 }, new[] { "x.x...x.", "x..x..x.", "x.....x.", "..x.x.x.", "x.x.x...", "x...x..." });
        int[] cmaj = { 0, 2, 4, 5, 7, 9, 11 };
        Arrange(s, parts, progs, phrases, B,
            (t, k, ch, p) =>
            {
                var v = Voicing(ch); bool groove = p.Has('d') || p.Has('h');
                if (p.Has('c')) { var pad = Pad(v, 4 * B, B, groove, p.Has('d') ? 0.045f : 0.055f, s.rnd); s.Add(t, pad, 0.3f); s.Add(t + 0.011, Pad(v, 4 * B, B, groove, p.Has('d') ? 0.045f : 0.055f, s.rnd), 0.7f); }
                if (p.Has('b')) { int[] bp = { 0, 0, 12, 0, 0, 12, 0, 7 }; for (int e = 0; e < 8; e++) s.Add(t + e * B / 2, SynthBass(Hz(ch[0] + bp[e]), B / 2 * 0.8, e % 2 == 0 ? 0.16f : 0.11f), 0.5f); }
                if (groove) { s.Add(t, Kick(0.26f), 0.5f); s.Add(t + 2 * B, Kick(0.24f), 0.5f); for (int e = 0; e < 8; e++) s.Add(t + e * B / 2, Hat(e % 2 == 1 ? 0.03f : 0.014f, s.rnd), 0.6f); }
                if (p.Has('d')) { s.Add(t + B, Clap(0.07f, s.rnd), 0.5f); s.Add(t + 3 * B, Clap(0.07f, s.rnd), 0.5f); s.Add(t + 3.5 * B, Kick(0.12f), 0.5f); }
                if (p.Has('x')) for (int e = 0; e < 8; e++) s.Add(t + e * B / 2, SynthPluck(Hz(v[(e + k * 2) % v.Length] + 12), 0.03f, 0.9), e % 2 == 0 ? 0.3f : 0.7f);   // tegenmelodie: arpeggio
                if (p.Has('s')) for (int e = 0; e < 16; e++) if (e % 3 != 2) s.Add(t + e * B / 4, SynthPluck(Hz(v[(e + k) % v.Length] + 24), 0.016f, 0.5), 0.2f + 0.6f * ((e / 2) % 2));   // zachte arpeggio's hoog
                if (k % 8 == 3 && !p.Has('r')) EchoFigure(s, t + 2 * B, B, ch, cmaj, p.Has('d') ? 0.05f : 0.06f);
                if (p.Has('f') && k == p.Bars - 1) for (int e = 0; e < 4; e++) s.Add(t + 3 * B + e * B / 4, Clap(0.03f + 0.012f * e, s.rnd), 0.5f);
            },
            (t, m, len, p) => s.Add(t, Lead(Hz(m), len * B / 2 * 0.9, p.Has('d') ? 0.06f : 0.05f), 0.45f + 0.1f * s.Rand));
        return s.Finish(0.27, 0.33, 0.55f, 0.4f);
    }

    // ---------- klanken ----------
    // ---------- synthklanken (zaagtand en blokgolf door een filter, Chamberlin state-variable) ----------
    struct Svf
    {
        float low, band;
        public float Low(float x, double fc, float q) { float f = (float)(2 * System.Math.Sin(System.Math.PI * System.Math.Min(fc, SR / 6.5) / SR)); low += f * band; float high = x - low - q * band; band += f * high; return low; }
    }
    static float Saw(double ph) => (float)(2 * (ph - System.Math.Floor(ph)) - 1);

    // pad: per toon drie licht ontstemde zaagtanden, laagdoorlaat dat langzaam ademt, traag aan en uit; groove: zakt op elke tel
    // even weg (sidechain op de bas trom)
    static float[] Pad(int[] notes, double dur, double beat, bool groove, float amp, System.Random rnd)
    {
        int len = (int)(SR * (dur + 0.6)); var s = new float[len]; var flt = new Svf(); double lfo = rnd.NextDouble() * 6.28;
        var ph = new double[notes.Length * 3]; var inc = new double[ph.Length];
        for (int i = 0; i < ph.Length; i++) { ph[i] = rnd.NextDouble(); inc[i] = Hz(notes[i / 3]) * (1 + (i % 3 - 1) * 0.0045) / SR; }
        for (int j = 0; j < len; j++)
        {
            double t = (double)j / SR; float x = 0f;
            for (int i = 0; i < ph.Length; i++) { ph[i] += inc[i]; x += Saw(ph[i]); }
            x /= ph.Length * 0.5f;
            double fc = 700 + 450 * System.Math.Sin(lfo + t * 1.3);
            double env = System.Math.Min(1.0, t / 0.35) * (t > dur ? System.Math.Max(0.0, 1 - (t - dur) / 0.6) : 1.0);
            double duck = groove ? 1 - 0.55 * System.Math.Exp(-(t % beat) / 0.13) : 1;
            s[j] = flt.Low(x, fc, 0.9f) * (float)(env * duck) * amp;
        }
        return s;
    }

    static float[] SynthBass(double f, double dur, float amp)   // zaagtand met een sinus eronder, filter dat snel dichtgaat
    {
        int len = (int)(SR * (dur + 0.04)); var s = new float[len]; var flt = new Svf(); double ph = 0;
        for (int j = 0; j < len; j++)
        {
            double t = (double)j / SR; ph += f / SR;
            float x = Saw(ph) * 0.7f + (float)System.Math.Sin(2 * System.Math.PI * ph) * 0.8f;
            double env = System.Math.Min(1.0, t / 0.004) * (t > dur ? System.Math.Max(0.0, 1 - (t - dur) / 0.03) : 0.85 + 0.15 * System.Math.Exp(-t / 0.05));
            s[j] = flt.Low(x, 180 + 1400 * System.Math.Exp(-t / 0.07), 0.7f) * (float)env * amp;
        }
        return s;
    }

    static float[] SynthPluck(double f, float amp, double decay)   // plukje: zaagtand plus blokgolf, filter van helder naar dof
    {
        int len = (int)(SR * decay); var s = new float[len]; var flt = new Svf(); double ph = 0;
        for (int j = 0; j < len; j++)
        {
            double t = (double)j / SR; ph += f / SR;
            float x = Saw(ph) * 0.6f + ((ph % 1.0) < 0.5 ? 0.4f : -0.4f);
            double env = System.Math.Min(1.0, t / 0.003) * System.Math.Exp(-t / (decay * 0.3)) * System.Math.Min(1.0, (len - j) / (SR * 0.01));
            s[j] = flt.Low(x, 350 + 3200 * System.Math.Exp(-t / 0.06), 0.8f) * (float)env * amp;
        }
        return s;
    }

    static float[] Lead(double f, double dur, float amp)   // lead: twee ontstemde blokgolven (40%), filter met een kleine opening, vibrato
    {
        int len = (int)(SR * (dur + 0.12)); var s = new float[len]; var flt = new Svf(); double a = 0, b = 0.37;
        for (int j = 0; j < len; j++)
        {
            double t = (double)j / SR, vib = t > 0.15 ? 1 + 0.004 * System.Math.Sin(2 * System.Math.PI * 5.5 * t) : 1;
            a += f * vib * 1.003 / SR; b += f * vib * 0.997 / SR;
            float x = ((a % 1.0) < 0.4 ? 0.5f : -0.5f) + ((b % 1.0) < 0.4 ? 0.5f : -0.5f);
            double env = System.Math.Min(1.0, t / 0.01) * (t > dur ? System.Math.Exp(-(t - dur) / 0.04) : 0.8 + 0.2 * System.Math.Exp(-t / 0.1));
            s[j] = flt.Low(x, 900 + 1800 * System.Math.Exp(-t / 0.12), 0.75f) * (float)env * amp;
        }
        return s;
    }

    static float[] Clap(float amp, System.Random rnd)   // handklap: drie korte ruisstootjes en een staart, rond 1,2 kHz
    {
        int len = (int)(SR * 0.25); var s = new float[len]; float l1 = 0, l2 = 0;
        for (int j = 0; j < len; j++)
        {
            double t = (double)j / SR; float w = (float)(rnd.NextDouble() * 2 - 1);
            l1 += 0.25f * (w - l1); l2 += 0.05f * (l1 - l2); float bp = l1 - l2;   // grof banddoorlaat
            double env = t < 0.03 ? System.Math.Exp(-(t % 0.01) / 0.003) : System.Math.Exp(-(t - 0.03) / 0.06);
            s[j] = bp * 3f * (float)env * amp;
        }
        return s;
    }

    static float[] Hat(float amp, System.Random rnd)   // gesloten hihat: hoge ruis, kort
    {
        int len = (int)(SR * 0.05); var s = new float[len]; float last = 0f, l2 = 0f;
        for (int j = 0; j < len; j++) { float w = (float)(rnd.NextDouble() * 2 - 1), hp = w - last; last = w; l2 += 0.5f * (hp - l2); s[j] = (hp - l2 * 0.5f) * Mathf.Exp(-j / (SR * 0.012f)) * amp; }
        return s;
    }

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

    static float[] SineNote(double f, float amp, double dur)   // korte aangehouden sinustoon met een vleugje octaaf: 12 ms aan, 15 ms uit
    {
        int len = (int)(SR * (dur + 0.02)); var s = new float[len]; double w = 2 * System.Math.PI * f;
        for (int j = 0; j < len; j++)
        {
            double t = (double)j / SR, env = System.Math.Min(1.0, t / 0.012) * (t > dur ? System.Math.Max(0.0, 1 - (t - dur) / 0.015) : 1.0);
            s[j] = (float)((System.Math.Sin(w * t) + 0.06 * System.Math.Sin(2 * w * t)) * env * amp);
        }
        return s;
    }

    static float[] Riser(double dur, float amp, System.Random rnd)   // aanloop: ruis die opzwelt en helderder wordt
    {
        int len = (int)(SR * dur); var s = new float[len]; float lp = 0f;
        for (int j = 0; j < len; j++) { float u = j / (float)len, w = (float)(rnd.NextDouble() * 2 - 1); lp += (0.02f + 0.5f * u * u) * (w - lp); s[j] = (w - lp) * u * u * amp * Mathf.Min(1f, (len - j) / (SR * 0.01f)); }
        return s;
    }

    static float[] Crash(float amp, System.Random rnd)   // zacht bekken: hoge ruis die lang uitsuist
    {
        int len = (int)(SR * 2.0); var s = new float[len]; float last = 0f;
        for (int j = 0; j < len; j++) { float w = (float)(rnd.NextDouble() * 2 - 1), hp = w - last; last = w; s[j] = hp * Mathf.Exp(-j / (SR * 0.55f)) * Mathf.Min(1f, j / (SR * 0.003f)) * amp; }
        return s;
    }

    static float[] Crackle(float amp, System.Random rnd)
    {
        int len = (int)(SR * 0.004); var s = new float[len];
        for (int j = 0; j < len; j++) s[j] = (float)(rnd.NextDouble() * 2 - 1) * Mathf.Exp(-j / (SR * 0.0008f)) * amp;
        return s;
    }
}
