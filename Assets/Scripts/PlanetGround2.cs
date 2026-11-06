using System.Collections.Generic;
using UnityEngine;

// Terrein v2. Stap 1: één hoogtefunctie H(punt) voor de hele planeet. Alles wat een hoogte van het maaiveld nodig heeft (lopen,
// rijden, bomen, bruggen, de scan in de tests) vraagt het hier; de mesh (BuildGround) hoort hetzelfde te geven, en in stap 2 wordt
// de mesh rechtstreeks uit H gebouwd, met gedeelde punten op de vakranden, zodat twee buren nooit meer verschillen.
// De lagen, in vaste volgorde:
//  1. het basisterrein: per vak een vlak over de waaierdriehoek (midden op G plus CenterOfs, hoeken op CornerBase: de laagste van de
//     drie vakken, of de weghoogte tussen twee verbonden wegvakken, of de kade of dam); een bebouwd vak is vlak op G (plateau);
//  2. geulen en dijken van onderdoorgangen (Carve, Fill): de hoogste dijk erop, de laagste geul erin;
//  3. de hoogtefunctie langs wegen (GroundField): wegen op een niveau en gewone wegen op een helling, met talud of steile kant,
//     onder een weg nooit boven het wegdek, onder een dalbrug lager, en de geulen daar nog eens.
// Een vlak plateau (bebouwd, niet door een weg of geul gesneden) blijft overal G: zijn sokkel doet de rest.
public static partial class PlanetCity
{
    // Het vak waar punt dir in ligt, met de waaierdriehoek k (midden, hoek k, hoek k+1) en de gewichten (b0 het midden, b1 hoek k,
    // b2 hoek k+1). Het dichtstbijzijnde midden is niet altijd het vak waar het punt in ligt (vakken zijn niet overal even groot),
    // daarom ook de buren proberen.
    static bool CellAt(Vector3 dir, out int i, out int k, out float b0, out float b1, out float b2)
    {
        int i0 = Planet.Nearest(dir * Planet.R);
        if (TriAt(i0, dir, out k, out b0, out b1, out b2)) { i = i0; return true; }
        foreach (int j in Planet.Cells[i0].nb) if (TriAt(j, dir, out k, out b0, out b1, out b2)) { i = j; return true; }
        i = i0; k = 0; b0 = 1f; b1 = b2 = 0f; return false;
    }
    static bool TriAt(int i, Vector3 dir, out int k, out float b0, out float b1, out float b2)
    {
        var c = Planet.Cells[i]; int m = c.corners.Length; Vector3 v = (dir - c.dir) * Planet.R; Vector2 q = new Vector2(Vector3.Dot(v, c.east), Vector3.Dot(v, c.north));
        for (k = 0; k < m; k++)
        {
            Vector2 L1 = c.local[k], L2 = c.local[(k + 1) % m]; float det = L1.x * L2.y - L1.y * L2.x; if (Mathf.Abs(det) < 1e-6f) continue;
            b1 = (q.x * L2.y - q.y * L2.x) / det; b2 = (L1.x * q.y - L1.y * q.x) / det; b0 = 1f - b1 - b2;
            if (b1 >= -0.02f && b2 >= -0.02f && b0 >= -0.02f) return true;
        }
        k = 0; b0 = 1f; b1 = b2 = 0f; return false;
    }
    // de hoogte van het maaiveld op punt dir (eenheidsvector), in meters boven de bol
    public static float H(Vector3 dir)
    {
        if (!CellAt(dir, out int i, out int k, out float b0, out float b1, out float b2)) return G(i);
        var su = segsUse; var fu = flatsUse; var cu = capsUse;   // H kiest zelf zijn stukken weg; wie bezig was (BuildGround) krijgt de zijne terug
        try { return H(dir, i, k, b0, b1, b2); } finally { segsUse = su; flatsUse = fu; capsUse = cu; }
    }
    // idem, voor een punt waarvan bekend is in welk vak en welke waaierdriehoek het ligt (de mesh). Dezelfde keuzes als het tekenen
    // van een vak in BuildGround, in dezelfde volgorde: tot de mesh uit H gebouwd wordt (stap 2) is dit de enige manier om gelijk te blijven.
    static float H(Vector3 dir, int i, int k, float b0, float b1, float b2)
    {
        if (fieldCells.Contains(i)) UseNear(i); else UseAll();   // eerst: dezelfde stukken weg als het tekenen van dit vak (UseNear: de buurt); ook FlatGround en StripCut kijken ernaar
        if (FlatGround(i)) return G(i);
        var c = Planet.Cells[i]; int m = c.corners.Length, k1 = (k + 1) % m; float g = G(i);
        // (BuildGround kijkt hier naar FlatGround, niet naar Plateau: een half wegvak of een strook langs een weg volgt de hoogtefunctie)
        // langs een weg op een niveau of een weg op een helling: het gewone terrein als vlak, de hoogtefunctie erover (LineGround)
        if (fieldCells.Contains(i) && NearSeg(c.dir, c.radius))
        {
            float hc0 = 0f; if (levelled.Contains(i)) { for (int q = 0; q < m; q++) hc0 += FieldBase(i, q); hc0 /= m; } else hc0 = g;
            hc0 += CenterOfs1(i);
            return GroundField(dir, hc0 * b0 + FieldBase(i, k) * b1 + FieldBase(i, k1) * b2);   // (met aan het eind de geulen)
        }
        // bij een geul of dijk in de buurt: het maaiveld zonder geul als vlak, de hoogste dijk erop, de laagste geul erin (ExactGround)
        var near = carved.Count > 0 || fillN > 0 ? CutsNear(c) : null;
        if (near != null && near.Count > 0)
        {
            float hcb = g; if (IsWalk(i) && !levelled.Contains(i)) { hcb = 0f; for (int q = 0; q < m; q++) hcb += CornerBase(i, q); hcb /= m; }
            float h = hcb * b0 + CornerBase(i, k) * b1 + CornerBase(i, k1) * b2;
            if (fillN > 0) h = Mathf.Max(h, Fill(dir)); if (carved.Count > 0) h = Mathf.Min(h, Carve(dir));
            return h;
        }
        // het gewone vak: het midden op G plus de eigen verschuiving (een weg- of padvak in een kom tot zijn laagste hoek), de hoeken zoals de buren ze hebben
        float hk1 = OwnCorner(i, k), hk2 = OwnCorner(i, k1), hc = g + CenterOfs1(i);
        if (IsWalk(i) && !carved.Contains(i) && !levelled.Contains(i)) for (int q = 0; q < m; q++) hc = Mathf.Min(hc, OwnCorner(i, q));
        if (cuts.Count > 0) hc = Mathf.Min(hc, Carve(c.dir));
        return hc * b0 + hk1 * b1 + hk2 * b2;
    }
    // voor de test: de tussenstappen van H op punt dir
    public static string HInfo(Vector3 dir)
    {
        bool ok = CellAt(dir, out int i, out int k, out float b0, out float b1, out float b2); var c = Planet.Cells[i]; int m = c.corners.Length, k1 = (k + 1) % m;
        var su = segsUse; var fu = flatsUse; var cu = capsUse; if (fieldCells.Contains(i)) UseNear(i); else UseAll();
        var sb = new System.Text.StringBuilder(); sb.Append("vak ").Append(i).Append(ok ? "" : " (niet gevonden)").Append(" k ").Append(k).Append(" b ").Append(b0.ToString("0.00")).Append('/').Append(b1.ToString("0.00")).Append('/').Append(b2.ToString("0.00"));
        sb.Append(" G ").Append(G(i).ToString("0.00")).Append(" vlak ").Append(FlatGround(i)).Append(" plateau ").Append(Plateau(i)).Append(" weg ").Append(IsWalk(i)).Append(" niveau ").Append(levelled.Contains(i)).Append(" geul ").Append(carved.Contains(i));
        sb.Append(" veld ").Append(fieldCells.Contains(i)).Append('/').Append(NearSeg(c.dir, c.radius)).Append(" geulen ").Append(carved.Count > 0 || fillN > 0 ? CutsNear(c)?.Count ?? 0 : 0);
        sb.Append(" veldbasis ").Append(FieldBase(i, k).ToString("0.00")).Append('/').Append(FieldBase(i, k1).ToString("0.00")).Append(" hoeken ").Append(OwnCorner(i, k).ToString("0.00")).Append('/').Append(OwnCorner(i, k1).ToString("0.00"));
        sb.Append(" H ").Append(H(dir).ToString("0.00"));
        segsUse = su; flatsUse = fu; capsUse = cu; return sb.ToString();
    }
}
