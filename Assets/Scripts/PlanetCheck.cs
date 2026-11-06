using System.Collections.Generic;
using UnityEngine;

// Controle van het wegen- en padennet (voor tests): klopt de opslag nog met zichzelf? Geeft een lijst met problemen terug (leeg = goed).
//  - elke verbinding hoort bij twee weg- of padvakken die buren zijn of over een hoek (across) liggen, en staat bij beide als partner;
//  - geen verbinding naar een half wegvak (wegdeel) of naar een leeg vak;
//  - een tunnel- of onderdoorgangsverbinding en eenrichtingsverkeer horen bij een bestaande verbinding;
//  - een rotondevak is een weg.
public static partial class PlanetCity
{
    public static List<string> CheckGraph()
    {
        var p = new List<string>();
        foreach (long k in Links)
        {
            int a = (int)(k >> 20), b = (int)(k & 0xFFFFF);
            if (!IsWalk(a) || !IsWalk(b)) p.Add($"verbinding {a}-{b} naar een vak zonder weg of pad ({Desc(a)} / {Desc(b)})");
            if (!Adjacent2(a, b) && !IsBore(a, b)) p.Add($"verbinding {a}-{b} tussen vakken die geen buren zijn");   // een tunnel of dalbrug gaat verder
            if (!Partners(a).Contains(b) || !Partners(b).Contains(a)) p.Add($"verbinding {a}-{b} ontbreekt bij de partners");
        }
        foreach (var kv in partners) foreach (int j in kv.Value) if (!Links.Contains(Key(kv.Key, j))) p.Add($"partner {kv.Key}-{j} zonder verbinding");
        foreach (long k in TunnelLinks) if (!Links.Contains(k)) p.Add($"tunnel {(int)(k >> 20)}-{(int)(k & 0xFFFFF)} zonder verbinding");
        foreach (long k in OneWay) { int a = (int)(k >> 20), b = (int)(k & 0xFFFFF); if (!Links.Contains(Key(a, b))) p.Add($"eenrichting {a}>{b} zonder verbinding"); if (OneWay.Contains(DKey(b, a))) p.Add($"eenrichting {a}-{b} in beide richtingen (doodlopend voor iedereen)"); }
        foreach (int c in Rot) if (!IsRoad(c)) p.Add($"rotonde {c} is geen weg");
        foreach (var kv in Cells) if (kv.Value.Count > 1 && (kv.Value.Contains("weg") || kv.Value.Contains("pad"))) p.Add($"vak {kv.Key} heeft een weg of pad met iets erop: {string.Join(",", kv.Value)}");
        return p;
    }
    // wat de grond op punt dir bepaalt (voor de terreintest)
    public static string GroundInfo(Vector3 dir)
    {
        int i = Planet.Nearest(dir * Planet.R); var sb = new System.Text.StringBuilder();
        sb.Append("vak ").Append(i).Append(' ').Append(Desc(i)).Append(" G ").Append(G(i).ToString("0.00")).Append(" plateau ").Append(Plateau(i)).Append(" vlak ").Append(FlatGround(i)).Append(" strook ").Append(StripCut(i)).Append(" veld ").Append(fieldCells.Contains(i));
        if (fieldCells.Contains(i)) UseNear(i);
        try
        {
            Vector3 w = dir * Planet.R;
            foreach (var q in segsUse) { Vector3 ab = q.b - q.a; float l2 = ab.sqrMagnitude; if (l2 < 1e-4f) continue; float u = Vector3.Dot(w - q.a, ab) / l2; Vector3 dv = w - (q.a + ab * Mathf.Clamp01(u)); dv -= dir * Vector3.Dot(dv, dir); if (dv.magnitude < q.hw + 8f) sb.Append(" | seg ").Append(q.ia).Append('-').Append(q.ib).Append(" h ").Append(Mathf.Lerp(q.ha, q.hb, Mathf.Clamp01(u)).ToString("0.00")).Append(" lat ").Append(dv.magnitude.ToString("0.0")).Append("/").Append(q.hw.ToString("0.0")).Append(" u ").Append(u.ToString("0.00")); }
            foreach (var q in capsUse) { float d = CapDist(q, w); if (d < q.hw + 8f) sb.Append(" | kop ").Append(q.ic).Append(" h ").Append(q.h.ToString("0.00")).Append(" d ").Append(d.ToString("0.0")).Append("/").Append(q.hw.ToString("0.0")); }
            foreach (var q in flatsUse) { Vector3 ab = q.b - q.a; float l2 = ab.sqrMagnitude; if (l2 < 1e-4f) continue; float u = Mathf.Clamp01(Vector3.Dot(w - q.a, ab) / l2); Vector3 dv = w - (q.a + ab * u); dv -= dir * Vector3.Dot(dv, dir); if (dv.magnitude < q.hw + 4f) sb.Append(" | vlak ").Append(q.ia).Append('-').Append(q.ib).Append(" h ").Append(Mathf.Lerp(q.ha, q.hb, u).ToString("0.00")).Append(" lat ").Append(dv.magnitude.ToString("0.0")).Append("/").Append(q.hw.ToString("0.0")); }
            sb.Append(" | veld ").Append(GroundField(dir, Plateau(i) ? G(i) : G(i)).ToString("0.00"));
        }
        finally { UseAll(); }
        return sb.ToString();
    }
    static string Desc(int i) => Cells.TryGetValue(i, out var l) && l.Count > 0 ? string.Join(",", l) : "leeg";
}
