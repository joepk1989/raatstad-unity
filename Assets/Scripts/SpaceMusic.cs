using UnityEngine;

// Muziek in de ruimte: als je zo ver uitzoomt dat je de bol ziet (PlanetSky.Space), komt er rustig een muziekje op en bij
// inzoomen gaat het weer weg (het loopt dan stil en gaat later verder waar het was). Berekend, geen opname: zachte akkoorden
// (D groot 9, B klein 9, G groot 7 #11, A add9; acht tellen per akkoord) met hier en daar een belletje op een zestonige
// toonladder (D E F# A B C#: zes tonen, zoals een zeshoek), en een galm die heen en weer kaatst. 64 seconden, naadloos rond;
// het rekenen gebeurt bij de start op de achtergrond. Geluid uit (Ambience.Muted) zet ook de muziek uit.
public class SpaceMusic : RenderedMusic
{
    protected override string Name => "ruimtemuziek";
    protected override float Volume => 0.35f;
    protected override float Target => PlanetSky.Space;

    protected override float[] Render()
    {
        const float Len = 64f; int n = (int)(SR * Len); var L = new float[n]; var R = new float[n];
        int[][] chords = { new[] { 50, 57, 61, 64, 66 }, new[] { 47, 54, 57, 61, 62 }, new[] { 43, 50, 54, 59, 61 }, new[] { 45, 52, 59, 61, 64 } };
        const double chordLen = 8.0, fade = 2.0; const double TwoPi = System.Math.PI * 2.0;

        // akkoorden: elke toon twee keer, iets ontstemd en links/rechts, met boventonen; akkoorden lopen in 4 s in elkaar over
        for (int c = 0; c < 8; c++)
        {
            var ch = chords[c % 4]; double t0 = c * chordLen;
            for (int v = 0; v <= ch.Length; v++)
            {
                int m = v < ch.Length ? ch[v] : ch[0] - 12;   // de laatste: de grondtoon een octaaf lager
                double amp = (v < ch.Length ? 0.05 : 0.07) * System.Math.Pow(0.93, (m - 43) / 2.0);
                for (int side = -1; side <= 1; side += 2)
                {
                    double f = Hz(m) * System.Math.Pow(2.0, side * 4.0 / 1200.0), w = TwoPi * f, lfo = v * 1.7 + side;
                    float pl = side < 0 ? 0.68f : 0.32f, pr = 1f - pl;
                    int i0 = (int)((t0 - fade) * SR), i1 = (int)((t0 + chordLen + fade) * SR);
                    for (int i = i0; i < i1; i++)
                    {
                        double t = (double)i / SR, e;
                        if (t < t0 + fade) e = System.Math.Sin((t - t0 + fade) / (4.0 * fade) * System.Math.PI); else if (t > t0 + chordLen - fade) e = System.Math.Cos((t - t0 - chordLen + fade) / (4.0 * fade) * System.Math.PI); else e = 1.0;   // sin² + cos² = 1: gelijk volume tijdens de overgang
                        e *= e;
                        double s = System.Math.Sin(w * t) + 0.3 * System.Math.Sin(2.0 * w * t) + 0.1 * System.Math.Sin(3.0 * w * t);
                        s *= amp * e * (1.0 + 0.18 * System.Math.Sin(TwoPi * 0.11 * t + lfo));
                        int k = ((i % n) + n) % n; L[k] += (float)(s * pl); R[k] += (float)(s * pr);
                    }
                }
            }
        }

        // belletjes: op de halve tel soms een toon uit de zestonige ladder, hoog en zacht, met een korte FM-klank
        int[] scale = { 74, 76, 78, 81, 83, 85, 86, 88 };
        var rnd = new System.Random(6); int last = 3;
        for (int step = 0; step < (int)(Len * 2); step++)
        {
            if (rnd.NextDouble() > 0.3) continue;
            last = Mathf.Clamp(last + rnd.Next(-2, 3), 0, scale.Length - 1);   // kleine stapjes: een melodietje
            double w = TwoPi * Hz(scale[last]), amp = 0.06 + 0.04 * rnd.NextDouble(); float pl = 0.25f + 0.5f * (float)rnd.NextDouble(), pr = 1f - pl;
            int s0 = (int)(step * 0.5 * SR), dur = (int)(4.0 * SR);
            for (int j = 0; j < dur; j++)
            {
                double t = (double)j / SR, env = System.Math.Min(1.0, t / 0.006) * System.Math.Exp(-t / 1.1);
                double s = System.Math.Sin(w * t + 1.4 * System.Math.Exp(-t / 0.35) * System.Math.Sin(3.5 * w * t)) * amp * env;
                int k = (s0 + j) % n; L[k] += (float)(s * pl); R[k] += (float)(s * pr);
            }
        }

        // galm: twee vertragingen die naar elkaar terugkaatsen, met een zacht filter; twee rondes zodat het einde op het begin aansluit
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
