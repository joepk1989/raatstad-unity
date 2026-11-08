using UnityEngine;

// Muziek in de ruimte: als je zo ver uitzoomt dat je de bol ziet (PlanetSky.Space), komt er rustig een muziekje op en bij
// inzoomen gaat het weer weg (het loopt dan stil en gaat later verder waar het was). Berekend, geen opname: zachte akkoorden
// met hier en daar een zwevende synthtoon op een zestonige toonladder (zes tonen, zoals een zeshoek), en een galm die heen en
// weer kaatst. Vier versies achter elkaar, die in elkaar overvloeien (samen 140 s, naadloos rond):
//   Thuis    D groot 9, B klein 9, G groot 7 #11, A add9; 8 s per akkoord; tonen D E F# A B C#
//   Diep     E klein 9, C groot 9, A klein 9, D add9; 10 s per akkoord, trager en lager; tonen G A B D E F#
//   Helder   A groot 9, F# klein 9, D groot 9, E sus2; 8 s per akkoord; hogere, kortere tonen als een traag arpeggio
//   Zwevend  C groot 7, F groot 7 #11, A klein 9, G6; 9 s per akkoord; lange tonen met een zachte octaaf erboven
// Een reis: elke versie begint met hetzelfde thema van vier tonen in zijn eigen toonladder; de energie loopt op van Thuis via
// Diep naar Helder (een zacht pulserend arpeggio en glans op de akkoorden) en zakt in Zwevend weer terug; bij elke overgang
// zwelt een zachte 'ruimtewind' aan waar je doorheen lijkt te vliegen. Zwevend eindigt op G, dat terugvalt op D: de lus sluit.
// Het rekenen gebeurt bij de start op de achtergrond. Geluid uit (Ambience.Muted) zet ook de muziek uit.
public class SpaceMusic : RenderedMusic
{
    protected override string Name => "ruimtemuziek";
    protected override float Volume => 0.35f;
    protected override float Target => PlanetSky.Space;

    const double TwoPi = System.Math.PI * 2.0;

    // een versie: akkoorden (elk één keer), hoe lang per akkoord, de toonladder van de zwevende tonen, om de hoeveel seconden
    // er misschien een toon komt (en hoe vaak), octaaf, hoe lang een toon blijft, hoe traag hij aangaat, en een octaaf erboven
    class Part
    {
        public int[][] chords; public double chordLen = 8, step = 1, prob = 0.35, holdMin = 1.5, holdMax = 2.5, attack = 0.45, energy = 0.3; public int[] scale; public int oct = -12; public bool shimmer; public int seed;
        public double Len => chords.Length * chordLen;
    }

    static readonly Part[] Parts = {
        new Part { seed = 6, chords = new[] { new[] { 50, 57, 61, 64, 66 }, new[] { 47, 54, 57, 61, 62 }, new[] { 43, 50, 54, 59, 61 }, new[] { 45, 52, 59, 61, 64 } },
                   scale = new[] { 74, 76, 78, 81, 83, 85, 86, 88 } },                                                         // Thuis
        new Part { seed = 17, energy = 0.45, chordLen = 10, prob = 0.25, holdMin = 2, holdMax = 3.5, attack = 0.7,
                   chords = new[] { new[] { 52, 55, 59, 62, 66 }, new[] { 48, 55, 59, 62, 64 }, new[] { 45, 52, 55, 59, 60 }, new[] { 50, 54, 57, 62, 64 } },
                   scale = new[] { 67, 69, 71, 74, 76, 78, 79, 81 } },                                                         // Diep
        new Part { seed = 29, energy = 1.0, step = 0.5, prob = 0.45, holdMin = 0.6, holdMax = 1.0, attack = 0.15, oct = 0,
                   chords = new[] { new[] { 45, 52, 56, 59, 61 }, new[] { 42, 49, 52, 56, 57 }, new[] { 50, 54, 57, 61, 64 }, new[] { 40, 47, 52, 54, 59 } },
                   scale = new[] { 69, 71, 73, 76, 78, 80, 81, 83 } },                                                         // Helder
        new Part { seed = 41, energy = 0.6, chordLen = 9, step = 1.5, prob = 0.4, holdMin = 2.5, holdMax = 3.5, shimmer = true,
                   chords = new[] { new[] { 48, 55, 59, 64, 67 }, new[] { 41, 53, 57, 59, 64 }, new[] { 45, 52, 55, 59, 60 }, new[] { 43, 50, 55, 59, 64 } },
                   scale = new[] { 72, 74, 76, 79, 81, 83, 84, 86 } } };                                                       // Zwevend

    protected override float[] Render()
    {
        double total = 0; foreach (var p in Parts) total += p.Len;
        int n = (int)(SR * total); var L = new float[n]; var R = new float[n];
        double t0 = 0;
        foreach (var p in Parts) { Chords(L, R, n, p, t0); Notes(L, R, n, p, t0); Pulse(L, R, n, p, t0); Swell(L, R, n, t0, 0.035 + 0.02 * p.energy, p.seed); t0 += p.Len; }
        return Echo(L, R, n);
    }

    // akkoorden: elke toon twee keer, iets ontstemd en links/rechts, met boventonen; akkoorden lopen in 4 s in elkaar over,
    // ook van de ene versie naar de volgende
    static void Chords(float[] L, float[] R, int n, Part p, double start)
    {
        const double fade = 2.0;
        for (int c = 0; c < p.chords.Length; c++)
        {
            var ch = p.chords[c]; double t0 = start + c * p.chordLen;
            for (int v = 0; v <= ch.Length; v++)
            {
                int m = v < ch.Length ? ch[v] : ch[0] - 12;   // de laatste: de grondtoon een octaaf lager
                double amp = (v < ch.Length ? 0.05 : 0.07) * System.Math.Pow(0.93, (m - 43) / 2.0);
                for (int side = -1; side <= 1; side += 2)
                {
                    double f = Hz(m) * System.Math.Pow(2.0, side * 4.0 / 1200.0), w = TwoPi * f, lfo = v * 1.7 + side;
                    float pl = side < 0 ? 0.68f : 0.32f, pr = 1f - pl;
                    int i0 = (int)((t0 - fade) * SR), i1 = (int)((t0 + p.chordLen + fade) * SR);
                    for (int i = i0; i < i1; i++)
                    {
                        double t = (double)i / SR, e;
                        if (t < t0 + fade) e = System.Math.Sin((t - t0 + fade) / (4.0 * fade) * System.Math.PI); else if (t > t0 + p.chordLen - fade) e = System.Math.Cos((t - t0 - p.chordLen + fade) / (4.0 * fade) * System.Math.PI); else e = 1.0;   // sin² + cos² = 1: gelijk volume tijdens de overgang
                        e *= e;
                        double s = System.Math.Sin(w * t) + (0.3 + 0.15 * p.energy) * System.Math.Sin(2.0 * w * t) + 0.1 * System.Math.Sin(3.0 * w * t) + 0.1 * p.energy * System.Math.Sin(4.0 * w * t);   // meer energie: meer glans
                        s *= amp * e * (1.0 + 0.18 * System.Math.Sin(TwoPi * 0.11 * t + lfo));
                        int k = ((i % n) + n) % n; L[k] += (float)(s * pl); R[k] += (float)(s * pr);
                    }
                }
            }
        }
    }

    // zwevende synthtonen: soms een toon uit de zestonige ladder, traag aan (geen tik), aangehouden, warm (sinus met een vleugje
    // octaaf en kwint) en met een langzaam vibrato; ze drijven mee op de akkoorden
    static void Notes(float[] L, float[] R, int n, Part p, double start)
    {
        var rnd = new System.Random(p.seed); int last = 3;
        // het thema: vier tonen (trap 2, 4, 5, 4 van de ladder), in elke versie op dezelfde plek, wat langer aangehouden
        int[] theme = { 2, 4, 5, 4 }; double[] themeAt = { 0.6, 2.1, 3.6, 5.1 }, themeHold = { 1.3, 1.3, 1.4, 2.6 };
        for (int k = 0; k < theme.Length; k++) Tone(L, R, n, p, start + themeAt[k], p.scale[theme[k]] + p.oct, 0.06, themeHold[k], 0.35f + 0.3f * (k % 2));
        last = theme[theme.Length - 1];
        for (double at = start + 8.0; at < start + p.Len - 1.0; at += p.step)
        {
            if (rnd.NextDouble() > p.prob) continue;
            last = Mathf.Clamp(last + rnd.Next(-2, 3), 0, p.scale.Length - 1);   // kleine stapjes: een melodietje
            Tone(L, R, n, p, at, p.scale[last] + p.oct, 0.045 + 0.025 * rnd.NextDouble(), p.holdMin + rnd.NextDouble() * (p.holdMax - p.holdMin), 0.3f + 0.4f * (float)rnd.NextDouble());
        }
    }

    static void Tone(float[] L, float[] R, int n, Part p, double at, int midi, double amp, double hold, float pl)
    {
        double f = Hz(midi), ph = 0; float pr = 1f - pl;
        int s0 = (int)(at * SR), dur = (int)((hold + 3.0) * SR), tail = SR / 4;
        for (int j = 0; j < dur; j++)
        {
            double t = (double)j / SR, env = System.Math.Min(1.0, t / p.attack) * (t > hold ? System.Math.Exp(-(t - hold) / 0.6) : 1.0) * System.Math.Min(1.0, (dur - j) / (double)tail);   // het eind naar precies nul: geen tik
            ph += f * (1 + 0.003 * System.Math.Sin(TwoPi * 4.2 * t) * System.Math.Min(1.0, t / 0.8)) / SR;
            double s = System.Math.Sin(TwoPi * ph) + 0.12 * System.Math.Sin(2 * TwoPi * ph) + 0.05 * System.Math.Sin(3 * TwoPi * ph);
            if (p.shimmer) s += 0.25 * System.Math.Sin(2 * TwoPi * ph * 1.002) * System.Math.Min(1.0, t / 1.2);   // zachte octaaf erboven die later opkomt
            s *= amp * env;
            int k = (s0 + j) % n; L[k] += (float)(s * pl); R[k] += (float)(s * pr);
        }
    }

    // zacht pulserend arpeggio: om de halve seconde (een zestiende van een akkoord) een akkoordtoon een octaaf hoger, zachte
    // sinus met een lange staart, om en om links en rechts; hoe meer energie, hoe voller (Thuis af en toe, Helder bijna elke puls)
    static void Pulse(float[] L, float[] R, int n, Part p, double start)
    {
        var rnd = new System.Random(p.seed + 100); double stepT = p.chordLen / 16;
        for (int c = 0; c < p.chords.Length; c++)
            for (int k = 0; k < 16; k++)
            {
                if (rnd.NextDouble() > 0.15 + 0.75 * p.energy) continue;
                var ch = p.chords[c]; int up = k % 8 < 4 ? k % 4 : 3 - k % 4;   // op en neer door het akkoord
                double f = Hz(ch[1 + up] + 12), w = TwoPi * f, amp = 0.016 * (0.4 + 0.6 * p.energy) * (k % 4 == 0 ? 1.2 : 1.0);
                float pl = k % 2 == 0 ? 0.3f : 0.7f, pr = 1f - pl;
                int s0 = (int)((start + c * p.chordLen + k * stepT) * SR), dur = (int)(3.0 * SR), tail = SR / 5;
                for (int j = 0; j < dur; j++)
                {
                    double t = (double)j / SR, env = System.Math.Min(1.0, t / 0.035) * System.Math.Exp(-t / 0.7) * System.Math.Min(1.0, (dur - j) / (double)tail);   // zacht aan, het eind naar precies nul
                    double s = (System.Math.Sin(w * t) + 0.08 * System.Math.Sin(2 * w * t)) * amp * env;
                    int i = ((s0 + j) % n + n) % n; L[i] += (float)(s * pl); R[i] += (float)(s * pr);
                }
            }
    }

    // ruimtewind bij een overgang: ruis die in 4 s aanzwelt en helderder wordt (laagdoorlaat van 150 naar 2500 Hz), van links
    // naar rechts schuift en na de overgang in 2,5 s wegsterft
    static void Swell(float[] L, float[] R, int n, double at, double amp, int seed)
    {
        var rnd = new System.Random(seed + 200); int before = (int)(4.0 * SR), after = (int)(2.5 * SR); float a = 0f, b = 0f;
        for (int j = -before; j < after; j++)
        {
            double u = j < 0 ? 1.0 + j / (double)before : 1.0 - j / (double)after;   // 0 → 1 (op de overgang) → 0
            double fc = 150 + 2350 * u * u, k = 1 - System.Math.Exp(-TwoPi * fc / SR);
            float w = (float)(rnd.NextDouble() * 2 - 1); a += (float)k * (w - a); b += (float)k * (a - b);
            double env = j < 0 ? u * u * u : u * u, pan = 0.5 + 0.35 * (j / (double)(j < 0 ? before : after));
            double s = b * env * amp * 2.5;
            int i = (((int)(at * SR) + j) % n + n) % n; L[i] += (float)(s * (1 - pan)); R[i] += (float)(s * pan);
        }
    }

    // galm: twee vertragingen die naar elkaar terugkaatsen, met een zacht filter; twee rondes zodat het einde op het begin aansluit
    static float[] Echo(float[] L, float[] R, int n)
    {
        var oL = new float[n]; var oR = new float[n];
        int dL = (int)(SR * 0.75), dR = (int)(SR * 1.0); var bL = new float[dL]; var bR = new float[dR]; int pL = 0, pR = 0; float lpL = 0f, lpR = 0f;
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < n; i++)
            {
                float yl = bL[pL], yr = bR[pR];
                lpL += 0.25f * (yl - lpL); lpR += 0.25f * (yr - lpR);
                bL[pL] = L[i] * 0.5f + lpR * 0.5f; bR[pR] = R[i] * 0.5f + lpL * 0.5f;
                pL = (pL + 1) % dL; pR = (pR + 1) % dR;
                if (pass == 1) { oL[i] = L[i] + lpL * 0.6f; oR[i] = R[i] + lpR * 0.6f; }
            }
        return Interleave(oL, oR);
    }
}
