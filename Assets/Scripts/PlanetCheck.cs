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
            if (!Adjacent2(a, b)) p.Add($"verbinding {a}-{b} tussen vakken die geen buren zijn");
            if (!Partners(a).Contains(b) || !Partners(b).Contains(a)) p.Add($"verbinding {a}-{b} ontbreekt bij de partners");
        }
        foreach (var kv in partners) foreach (int j in kv.Value) if (!Links.Contains(Key(kv.Key, j))) p.Add($"partner {kv.Key}-{j} zonder verbinding");
        foreach (long k in TunnelLinks) if (!Links.Contains(k)) p.Add($"tunnel {(int)(k >> 20)}-{(int)(k & 0xFFFFF)} zonder verbinding");
        foreach (long k in OneWay) { int a = (int)(k >> 20), b = (int)(k & 0xFFFFF); if (!Links.Contains(Key(a, b))) p.Add($"eenrichting {a}>{b} zonder verbinding"); if (OneWay.Contains(DKey(b, a))) p.Add($"eenrichting {a}-{b} in beide richtingen (doodlopend voor iedereen)"); }
        foreach (int c in Rot) if (!IsRoad(c)) p.Add($"rotonde {c} is geen weg");
        foreach (var kv in Cells) if (kv.Value.Count > 1 && (kv.Value.Contains("weg") || kv.Value.Contains("pad"))) p.Add($"vak {kv.Key} heeft een weg of pad met iets erop: {string.Join(",", kv.Value)}");
        return p;
    }
    static string Desc(int i) => Cells.TryGetValue(i, out var l) && l.Count > 0 ? string.Join(",", l) : "leeg";
}
