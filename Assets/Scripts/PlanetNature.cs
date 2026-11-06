using System.Collections.Generic;
using UnityEngine;

// Natuur: bomen los op het terrein, zonder grasplaat en zonder sokkel. Een vak met alleen "park" is geen plateau: het terrein loopt
// er gewoon door (boetseren kan er ook) en elke boom staat op de grond onder zijn eigen stam. Vier soorten, low poly en zeshoekig,
// op ware grootte: loofboom (8-11 m), den (11-15 m), berk (9-12 m) en struik. De soort hangt af van de hoogte (hoger meer naaldbomen),
// de helling (steiler dan 35° alleen struiken), de breedte (naar de polen meer dennen) en een grove ruis, zodat een bos vooral uit
// één soort bestaat.
public static partial class PlanetCity
{
    // geen plateau (geen sokkel): bomen, een meer en een gracht liggen in het terrein
    static bool OpenGround(List<string> l) => l.Count == 1 && (l[0] == "park" || l[0] == "meer" || l[0] == "water" || IsWild(l[0]));

    // de bomen van vak i; roof: een daktuin bovenop een gebouw (op hoogte h, kleine boompjes), anders op het terrein
    static void Trees(System.Func<string, MeshBuilder> M, Frame fr, int i, int hash, float h, bool roof)
    {
        var rnd = new System.Random(hash ^ 0x5bd1e995); var c = Planet.Cells[i];
        float minC = 0f; for (int k = 0; k < c.corners.Length; k++) minC = Mathf.Min(minC, CornerBase(i, k) - G(i));
        float wood = Noise(c.dir, 1f / 110f, 53f);
        // zeven plekken: het midden en een ring van zes, wat verschoven; een bos laat er een paar open
        int want = roof ? 2 + rnd.Next(2) : 4 + rnd.Next(4);
        float a0 = (float)rnd.NextDouble() * Mathf.PI * 2f;
        for (int s = 0; s < 7; s++)
        {
            if (rnd.Next(7 - s) >= want) continue; want--;
            float a = a0 + s * Mathf.PI / 3f + ((float)rnd.NextDouble() - 0.5f) * 0.6f, rr = s == 6 ? (float)rnd.NextDouble() * 0.15f : 0.48f + ((float)rnd.NextDouble() - 0.5f) * 0.18f;
            if (roof) rr *= 0.8f;
            float x = Mathf.Cos(a) * rr * fr.rad, z = Mathf.Sin(a) * rr * fr.rad, th = h, slope = 0f;
            if (!roof)
            {
                Vector3 w = fr.o + fr.e * x + fr.n * z; float gt = TerrainAt(w.normalized) - G(i);
                if (gt < minC - 0.6f) continue;   // niet in de bak van een verdiepte weg
                float gx = TerrainAt((w + fr.e * 1.5f).normalized) - TerrainAt((w - fr.e * 1.5f).normalized), gz = TerrainAt((w + fr.n * 1.5f).normalized) - TerrainAt((w - fr.n * 1.5f).normalized);
                slope = new Vector2(gx, gz).magnitude / 3f; th = gt - 0.2f - slope * 0.5f;   // de stam iets de grond in, aan de lage kant geen kier
            }
            float H = G(i) + th, sc = 0.8f + (float)rnd.NextDouble() * 0.45f, r = (float)rnd.NextDouble();
            int kind;
            if (roof) { kind = r < 0.5f ? 3 : 0; sc *= 0.55f; }
            else if (slope > 0.7f) { if (r > 0.5f) continue; kind = 3; }
            else if (r < 0.12f) kind = 3;
            else
            {
                LatLon(c.dir, out float lat, out _); float pine = Mathf.Clamp01((H - 6f) / 14f) * 0.8f + (wood > 0.62f ? 0.45f : 0f) + Mathf.Clamp01((Mathf.Abs(lat) - 45f) / 20f), birch = wood < 0.38f ? 0.45f : 0.1f, q = (float)rnd.NextDouble();
                kind = q < pine ? 1 : q < pine + birch ? 2 : 0;
            }
            if (!roof && H > TreeLine) continue;   // boven de boomgrens niets
            if (!roof && H > TreeLine - 12f && kind != 3) { kind = 1; sc *= Mathf.Lerp(1f, 0.6f, (H - TreeLine + 12f) / 12f); }   // eronder alleen dennen, steeds kleiner
            float rot = (float)rnd.NextDouble() * Mathf.PI / 3f;
            TreeAt(M, fr, kind, x, z, th, sc, rot, rnd);
        }
    }

    // één boom van soort kind (0 loofboom, 1 den, 2 berk, 3 struik) op (x, z), stam vanaf hoogte th
    static void TreeAt(System.Func<string, MeshBuilder> M, Frame fr, int kind, float x, float z, float th, float sc, float rot, System.Random rnd)
    {
        switch (kind)
        {
            case 0:   // loofboom: rechte stam, een zeshoekige kroon (smal onder, breed in het midden, afgeplat boven)
                { string leaf = rnd.Next(3) == 0 ? "loof2" : "blad";
                Prism(M("stam"), fr, x, z, rot, 0.28f * sc, 0.2f * sc, th, th + 3.4f * sc, false, false);
                Prism(M(leaf), fr, x, z, rot, 1.6f * sc, 3.2f * sc, th + 2.6f * sc, th + 5.4f * sc, false, true);
                Prism(M(leaf), fr, x, z, rot, 3.2f * sc, 1.6f * sc, th + 5.4f * sc, th + 8.8f * sc, true, false); }
                break;
            case 1:   // den: korte stam, drie kegels op elkaar
                Prism(M("stam"), fr, x, z, rot, 0.3f * sc, 0.22f * sc, th, th + 2.6f * sc, false, false);
                Prism(M("naald"), fr, x, z, rot, 3.0f * sc, 0f, th + 1.6f * sc, th + 7.0f * sc, false, true);
                Prism(M("naald"), fr, x, z, rot + 0.5f, 2.3f * sc, 0f, th + 5.0f * sc, th + 10.4f * sc, false, true);
                Prism(M("naald"), fr, x, z, rot, 1.5f * sc, 0f, th + 8.6f * sc, th + 13.2f * sc, false, true);
                break;
            case 2:   // berk: lange witte stam, een smalle hoge kroon
                Prism(M("berk"), fr, x, z, rot, 0.18f * sc, 0.13f * sc, th, th + 6.2f * sc, false, false);
                Prism(M("berkblad"), fr, x, z, rot, 0.9f * sc, 1.9f * sc, th + 4.2f * sc, th + 7.6f * sc, false, true);
                Prism(M("berkblad"), fr, x, z, rot, 1.9f * sc, 0.6f * sc, th + 7.6f * sc, th + 11.0f * sc, true, false);
                break;
            default:   // struik: een lage zeshoekige bol
                Prism(M("struik"), fr, x, z, rot, 0.7f * sc, 1.4f * sc, th, th + 0.7f * sc, false, false);
                Prism(M("struik"), fr, x, z, rot, 1.4f * sc, 0.5f * sc, th + 0.7f * sc, th + 1.6f * sc, true, false);
                break;
        }
    }

    // zeshoekig prisma of afgeknotte kegel om (cx, cz), gedraaid over rot; r1 = 0 is een punt; deksels boven en/of onder
    static void Prism(MeshBuilder m, Frame f, float cx, float cz, float rot, float r0, float r1, float y0, float y1, bool capTop, bool capBottom)
    {
        Vector3 bot = f.P(cx, y0, cz), top = f.P(cx, y1, cz);
        for (int k = 0; k < 6; k++)
        {
            float a = rot + k * Mathf.PI / 3f, b = a + Mathf.PI / 3f, am = (a + b) * 0.5f;
            float ca = Mathf.Cos(a), sa = Mathf.Sin(a), cb = Mathf.Cos(b), sb = Mathf.Sin(b);
            Vector3 p0 = f.P(cx + ca * r0, y0, cz + sa * r0), p1 = f.P(cx + cb * r0, y0, cz + sb * r0);
            Vector3 outw = f.D(Mathf.Cos(am), (r0 - r1) / Mathf.Max(0.01f, y1 - y0), Mathf.Sin(am));
            if (r1 <= 0.001f) m.Tri(p0, p1, top, outw);
            else
            {
                Vector3 q0 = f.P(cx + ca * r1, y1, cz + sa * r1), q1 = f.P(cx + cb * r1, y1, cz + sb * r1);
                m.Quad(p0, p1, q1, q0, outw);
                if (capTop) m.Tri(top, q0, q1, f.u);
            }
            if (capBottom) m.Tri(bot, p1, p0, -f.u);
        }
    }

    // ---------- zeeniveau ----------
    // Overal op de planeet hetzelfde waterpeil: waar het maaiveld lager ligt dan SeaLevel staat water (afgraven met Terrein maakt
    // dus een meer of een zee). Wegen tellen niet mee: een wegvak, en de geul of het talud van een weg op een niveau, blijft droog.
    // Per vak een vlakke zeshoek op het peil over het hele vak; het terrein dat erboven uitsteekt maakt vanzelf de oever, en buurvakken
    // sluiten naadloos aan (zelfde hoekpunten, zelfde hoogte).
    public const float SeaLevel = -1f;
    // het natuurlijke maaiveld op hoekpunt k van vak i: het laagste van de drie vakken eromheen, zonder wegen en paden
    static float SeaCorner(int i, int k)
    {
        Topo(); float h = float.MaxValue;
        foreach (int j in new[] { i, cnA[i][k], cnB[i][k] }) if (!IsWalk(j)) h = Mathf.Min(h, G(j));
        return h;
    }
    // graaft de hoogtefunctie hier (een weg lager dan het terrein ernaast)? Dan geen water in het vak.
    static bool DugHere(int i) => G(i) + CenterOfs1(i) < SeaLevel && GroundField(Planet.Cells[i].dir, G(i), out _, segs, caps) < G(i) - 0.1f;   // (alleen onder het zeeniveau van belang; alle wegstukken, los van UseNear)
    public static bool Wet(int i)
    {
        if (Quay(i)) return true;
        if (IsWalk(i) || carved.Contains(i) || levelled.Contains(i)) return false;
        if (fieldCells.Contains(i) && NearSeg(Planet.Cells[i].dir, Planet.Cells[i].radius) && DugHere(i)) return false;   // de geul van een verdiepte weg loopt niet vol; een dam of een weg langs het water laat de zee wel liggen
        if (Cells.TryGetValue(i, out var l) && l.Count > 0 && l[0] == "kuil") return false;
        if (DryPit(i)) return false;   // een droge meteoorkrater (PlanetCity.Scorch.cs)
        if (G(i) + CenterOfs1(i) < SeaLevel) return true;
        for (int k = 0; k < Planet.Cells[i].corners.Length; k++) if (SeaCorner(i, k) < SeaLevel) return true;
        return false;
    }
    // ---------- meer en gracht ----------
    // Meer: het vak ligt vanzelf op LakeBed (G), zonder het opgeslagen terrein te veranderen; de buren lopen er schuin naartoe af
    // (de hoekpunten nemen het laagste vak) en het water komt van het zeeniveau. Een weg over een meer is een dam (het vak is dan een
    // weg en ligt weer op zijn eigen hoogte); haal je de weg weg, dan is het meer er weer.
    // Gracht: het terrein blijft liggen; het vak krijgt een bodem op CanalBed en bakstenen kademuren naar elke buur die geen gracht is
    // (de buren houden hun hoogte: CornerBase0 slaat een gracht over). Een brug over een gracht houdt de gracht eronder.
    public const float LakeBed = SeaLevel - 2.5f, CanalBed = SeaLevel - 2f;
    static float LakeG(int i, float h) => h > LakeBed && Cells.TryGetValue(i, out var l) && l.Count == 1 && l[0] == "meer" ? LakeBed : h;
    static bool IsLake(int i) => Cells.TryGetValue(i, out var l) && l.Count == 1 && l[0] == "meer";
    // een dam: op een hoekpunt waar een weg of pad aan een meer grenst telt het meer niet mee, zodat de grond onder de weg niet in
    // het meer zakt; het meer loopt daar schuin op naar de dam
    static void DamSkip(int i, int a, int b, ref bool qi, ref bool qa, ref bool qb)
    {
        bool li = IsLake(i), la = IsLake(a), lb = IsLake(b); if (!li && !la && !lb) return;
        if (!IsWalk(i) && !IsWalk(a) && !IsWalk(b)) return;
        qi |= li; qa |= la; qb |= lb;
    }
    public static bool Quay(int i) => (Cells.TryGetValue(i, out var l) && l.Count == 1 && l[0] == "water") || (Under.TryGetValue(i, out var u) && u == "water");
    static void Canal(System.Func<string, MeshBuilder> M, int i)
    {
        Topo(); var c = Planet.Cells[i]; int m = c.corners.Length; float rb = Planet.R + CanalBed; Vector3 mid = c.dir * rb;
        for (int k = 0; k < m; k++)
        {
            Vector3 a = c.corners[k], b = c.corners[(k + 1) % m];
            M("zeebodem").Tri(mid, a * rb, b * rb, c.dir);
            if (Quay(edN[i][k])) continue;   // open naar de volgende gracht
            float ha = CornerBase(i, k), hb = CornerBase(i, (k + 1) % m); if (ha <= CanalBed + 0.01f && hb <= CanalBed + 0.01f) continue;
            Vector3 inward = c.dir - (a + b) * 0.5f;
            M("baksteen").Quad(a * rb, b * rb, b * (Planet.R + Mathf.Max(hb, CanalBed)), a * (Planet.R + Mathf.Max(ha, CanalBed)), inward);
        }
    }

    // ---------- bergen ----------
    // Terrein tot TerrTop (90 m). Boven de 30 m loopt het grijsgroen over in rots, steile flanken boven de 22 m zijn kale rots,
    // en boven de 54 m ligt sneeuw (op steile flanken pas boven de 72 m). Bomen tot de boomgrens (Trees).
    public const float SnowLine = 54f, TreeLine = 48f;
    static string NatureTag(int i, string tag, float h0, float h1, float h2, float up)
    {
        string sc = ScorchTag(i, tag, h0, h1, h2); if (sc != tag) return SeaTag(i, sc, h0, h1, h2);   // een verse krater (PlanetCity.Scorch.cs), ook op sneeuw; alleen water gaat voor
        string wild = WildTag(i);   // ijs, woestijn of heide kleurt de grond van het hele vak (PlanetWild.cs)
        if (tag == null) return wild == null ? null : SeaTag(i, wild, h0, h1, h2);
        float ha = (h0 + h1 + h2) / 3f;
        if (ha > SnowLine && (up > 0.6f || ha > SnowLine + 18f)) return "sneeuw";
        if (wild != null && (tag != "aarde" || wild == "ijs")) return SeaTag(i, wild, h0, h1, h2);
        if (tag == "aarde" && ha > 22f) return "rots";
        return SeaTag(i, tag, h0, h1, h2);
    }
    // hoogteband t10..t29 (30-90 m): van grijsgroen naar rots
    static Color MountainBand(float h)
    {
        Color top = new Color(0.512f, 0.618f, 0.436f), rock = new Color(0.56f, 0.54f, 0.50f);   // top: de kleur van TerrColor op 30 m
        return Color.Lerp(top, rock, Mathf.SmoothStep(0f, 1f, (h - 30f) / 20f));
    }

    // de kleur van een stukje terrein in een nat vak: zand langs de waterlijn, donkere bodem in het diepe
    static string SeaTag(int i, string tag, float h0, float h1, float h2)
    {
        if (tag == null || Mathf.Min(h0, Mathf.Min(h1, h2)) > SeaLevel + 0.4f || !Wet(i)) return tag;
        return Mathf.Max(h0, Mathf.Max(h1, h2)) < SeaLevel - 1.5f ? "zeebodem" : "zand";
    }
    // het wateroppervlak van de vakken c0..c1 (één deel van de bodem)
    static void SeaChunk(int c0, int c1, Transform parent, List<GameObject> made)
    {
        var mb = new MeshBuilder(); float r = Planet.R + SeaLevel;
        for (int i = c0; i < c1; i++)
        {
            if (!Wet(i)) continue; var c = Planet.Cells[i]; int m = c.corners.Length; Vector3 mid = c.dir * r;
            for (int k = 0; k < m; k++) mb.Tri(mid, c.corners[k] * r, c.corners[(k + 1) % m] * r, c.dir);
        }
        if (mb.v.Count == 0) return;
        var go = new GameObject("Zee " + (c0 / ChunkN)); go.transform.SetParent(parent, false); go.layer = Layers.Water; made.Add(go);
        go.AddComponent<MeshFilter>().sharedMesh = mb.Build();
        var mat = Mats.Get("p_zee", new Color(0.16f, 0.42f, 0.62f, 0.78f)); if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.85f);
        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var reeds = new MeshBuilder();
        for (int i = c0; i < c1; i++) if (Wet(i) && !Quay(i)) Reeds(reeds, i);
        if (reeds.v.Count == 0) return;
        var rg = new GameObject("Riet " + (c0 / ChunkN)); rg.transform.SetParent(parent, false); made.Add(rg);
        rg.AddComponent<MeshFilter>().sharedMesh = reeds.Build(); rg.AddComponent<MeshRenderer>().sharedMaterial = MatOf("riet", ColorOf("riet"));
    }
    // riet langs de waterlijn: pollen op de plekken van het vak waar de grond net onder of boven het water ligt
    static void Reeds(MeshBuilder m, int i)
    {
        var fr = new Frame(i); var rnd = new System.Random(i * 7919 + 13);
        for (int s = 0; s < 10; s++)
        {
            float a = (float)rnd.NextDouble() * Mathf.PI * 2f, rr = (0.2f + (float)rnd.NextDouble() * 0.75f) * fr.rad, x = Mathf.Cos(a) * rr, z = Mathf.Sin(a) * rr;
            float gt = TerrainAt((fr.o + fr.e * x + fr.n * z).normalized); if (gt < SeaLevel - 0.5f || gt > SeaLevel + 0.15f) continue;
            float y0 = gt - G(i) - 0.1f;
            for (int t = 0; t < 4; t++)
            {
                float ox = x + ((float)rnd.NextDouble() - 0.5f) * 0.9f, oz = z + ((float)rnd.NextDouble() - 0.5f) * 0.9f;
                Cone(m, fr, ox, oz, 0.07f, y0, y0 + 1.3f + (float)rnd.NextDouble() * 0.8f, 0f);
            }
        }
    }

    static bool NatureColor(string k, out Color c)
    {
        switch (k)
        {
            case "zeebodem": c = new Color(0.47f, 0.45f, 0.36f); return true;
            case "rots": c = new Color(0.50f, 0.48f, 0.46f); return true;
            case "sneeuw": c = new Color(0.94f, 0.95f, 0.97f); return true;
            case "ijs": c = new Color(0.84f, 0.91f, 0.96f); return true;
            case "woestijn": c = new Color(0.86f, 0.71f, 0.47f); return true;
            case "heide": c = new Color(0.47f, 0.39f, 0.40f); return true;
            case "heideplant": c = new Color(0.60f, 0.36f, 0.58f); return true;
            case "ijsblok": c = new Color(0.78f, 0.89f, 0.98f); return true;
            case "cactus": c = new Color(0.30f, 0.50f, 0.30f); return true;
            case "droog": c = new Color(0.63f, 0.57f, 0.36f); return true;
            case "kei": c = new Color(0.61f, 0.59f, 0.56f); return true;
            case "palmstam": c = new Color(0.56f, 0.46f, 0.33f); return true;
            case "palmblad": c = new Color(0.27f, 0.56f, 0.21f); return true;
            case "loof2": c = new Color(0.36f, 0.56f, 0.22f); return true;
            case "naald": c = new Color(0.13f, 0.34f, 0.21f); return true;
            case "berk": c = new Color(0.88f, 0.86f, 0.80f); return true;
            case "berkblad": c = new Color(0.50f, 0.66f, 0.28f); return true;
            case "struik": c = new Color(0.29f, 0.50f, 0.23f); return true;
            default:
                if (ScorchColor(k, out c)) return true;
                if (k.Length > 1 && k[0] == 't' && int.TryParse(k.Substring(1), out int band) && band >= 10) { c = MountainBand((band + 0.5f) * 3f); return true; }
                c = default; return false;
        }
    }
}
