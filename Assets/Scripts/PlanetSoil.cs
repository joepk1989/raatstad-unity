using System.Collections.Generic;
using UnityEngine;

// Bodem met textures: het terrein van een deel is één mesh met de shader Raatstad/Bodem (Assets/Resources/Terrein) in plaats van
// een effen materiaal per soort. Gras, rots, sneeuw, aarde, zand en zeebodem rekent de shader per pixel uit (hoogte, helling,
// waterlijn); wat bij het vak hoort (ijs, woestijn, heide, water) komt mee als vertexkleur. Elk hoekpunt krijgt het gewogen
// gemiddelde van de vakken eromheen (SoilColor), zodat een overgang op de rand tussen twee vakken ligt en daar zacht verloopt.
// De gewichten hangen alleen af van de plek, dus twee delen (ChunkN) sluiten naadloos aan. De driehoeken houden hun eigen normaal:
// het blijft low poly. Bomen, struiken en ander groen krijgen Raatstad/Natuur: hun eigen kleur met wat variatie.
public static partial class PlanetCity
{
    static Material soilMat; static readonly Dictionary<string, Material> natureMats = new Dictionary<string, Material>();
    static Texture2D soilA, soilB;

    static bool LoadSoilTex()
    {
        if (soilA == null) soilA = Resources.Load<Texture2D>("Terrein/BodemA");
        if (soilB == null) soilB = Resources.Load<Texture2D>("Terrein/BodemB");
        return soilA != null && soilB != null;
    }

    // het materiaal van de bodem (null als de shader of de textures ontbreken: dan de oude effen kleuren)
    static Material SoilMat()
    {
        if (soilMat != null) return soilMat;
        var sh = Shader.Find("Raatstad/Bodem"); if (sh == null || !sh.isSupported || !LoadSoilTex()) return null;
        var m = new Material(sh) { name = "p_bodemmix" };
        m.SetTexture("_BodemA", soilA); m.SetTexture("_BodemB", soilB);
        m.SetVector("_Center", groundRoot != null ? groundRoot.position : Vector3.zero);
        m.SetFloat("_PlanetR", Planet.R); m.SetFloat("_Sea", SeaLevel); m.SetFloat("_Snow", SnowLine);
        // de kleuren van de effen materialen (TerrColor, NatureColor), zodat er één bron is
        m.SetColor("_Gras0", new Color(0.64f, 0.78f, 0.45f)); m.SetColor("_Gras1", new Color(0.373f, 0.58f, 0.278f)); m.SetColor("_Gras3", new Color(0.604f, 0.643f, 0.541f));
        m.SetColor("_Aarde", TerrColor("aarde")); m.SetColor("_Zand", TerrColor("zand"));
        foreach (var k in new[] { "rots", "sneeuw", "zeebodem", "woestijn", "heide" }) { NatureColor(k, out var c); m.SetColor("_" + char.ToUpper(k[0]) + k.Substring(1), c); }
        NatureColor("ijs", out var ijs); m.SetColor("_IJs", ijs); NatureColor("heideplant", out var hb); m.SetColor("_HeideBloem", hb);
        soilMat = m; return m;
    }

    // de soorten die de bodemshader zelf tekent; de rest (sokkel, een verschroeide krater) houdt zijn eigen materiaal
    static bool SoilTag(string t)
    {
        if (t == null) return true;
        switch (t)
        {
            case "aarde": case "rots": case "sneeuw": case "ijs": case "woestijn": case "heide": case "zand": case "zeebodem": return true;
            case "roet": case "roetgrijs": case "kraterbodem": case "uitwerp": return true;   // een verse krater: de shader verdeelt roet en puin zelf
            default: return t.Length > 1 && t[0] == 't' && char.IsDigit(t[1]);
        }
    }

    // wat het vak zelf meebrengt: kleur r ijs, g woestijn, b heide, a water; extra (uv0) x roetdeken, y kraterkom, z kraterrand,
    // w helling van het vak (grootste hoogteverschil met een buur per 15,7 m: 1 is 45°), zodat een bergkam als geheel kaal is
    struct Soil { public Color c; public Vector4 x; }
    static Soil CellSoil(int i)
    {
        string w = WildTag(i);
        var s = new Soil { c = new Color(w == "ijs" ? 1f : 0f, w == "woestijn" ? 1f : 0f, w == "heide" ? 1f : 0f, Wet(i) ? 1f : 0f) };
        if (Scorch.Count > 0 && Scorch.TryGetValue(i, out byte sc) && !Planet.IsPole(i))
            s.x = sc >= ScorchFloor ? new Vector4(1f, 1f, 0f, 0f) : sc >= ScorchRim ? new Vector4(1f, 0f, 1f, 0f) : new Vector4(sc / 99f, 0f, 0f, 0f);
        float g = G(i), sl = 0f; foreach (int j in Planet.Cells[i].nb) sl = Mathf.Max(sl, Mathf.Abs(G(j) - g));
        s.x.w = sl / 15.7f;
        return s;
    }
    const float SoilReach = 14f;   // tot hoever een vak meetelt (de buren liggen 15,7 m verder; een hoekpunt ligt 9 m van de drie vakken eromheen)
    // de vertexkleur op plek p (in vak i): het gemiddelde van vak i en zijn buren, gewogen naar de afstand tot hun midden
    static Soil SoilColor(int i, Vector3 p, Dictionary<int, Soil> memo)
    {
        Vector3 d = p.normalized; var c = Planet.Cells[i]; Color s = default; Vector4 x = default; float sw = 0f;
        void Add(int j)
        {
            float dist = (Planet.Cells[j].dir - d).magnitude * Planet.R; if (dist >= SoilReach) return;
            float w = 1f - dist / SoilReach; w *= w;
            if (!memo.TryGetValue(j, out var cs)) { cs = CellSoil(j); memo[j] = cs; }
            s += cs.c * w; x += cs.x * w; sw += w;
        }
        Add(i); foreach (int j in c.nb) Add(j);
        if (sw > 1e-6f) return new Soil { c = s / sw, x = x / sw };
        return memo.TryGetValue(i, out var own) ? own : CellSoil(i);
    }

    // de bodem van een deel als één mesh (de driehoeken met hun vak, cells parallel aan de hoekpunten)
    static void SoilMesh(MeshBuilder mb, List<int> cells, int ch, List<GameObject> made)
    {
        if (mb.v.Count == 0) return;
        var memo = new Dictionary<int, Soil>(); var col = new List<Color>(mb.v.Count); var ext = new List<Vector4>(mb.v.Count);
        for (int k = 0; k < mb.v.Count; k++) { var so = SoilColor(cells[k], mb.v[k], memo); col.Add(so.c); ext.Add(so.x); }
        var go = new GameObject("Terrein bodem " + ch); go.transform.SetParent(groundRoot, false); made.Add(go); go.layer = Layers.Bodem;
        var mesh = mb.Build(); mesh.SetColors(col); mesh.SetUVs(0, ext);
        go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = SoilMat();
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    // groen en hout krijgen het natuurmateriaal (kleurvariatie en fijn reliëf); null: een gewoon materiaal
    static Material NatureMat(string k, Color c)
    {
        float fine, vary;
        switch (k)
        {
            case "blad": case "loof2": case "naald": case "berkblad": case "struik": case "palmblad": fine = 0.35f; vary = 0.6f; break;
            case "heideplant": case "riet": case "droog": case "cactus": case "gras": fine = 0.25f; vary = 0.5f; break;
            case "stam": case "palmstam": case "berk": fine = 0.45f; vary = 0.25f; break;
            default: return null;
        }
        if (natureMats.TryGetValue(k, out var m) && m != null) return m;
        var sh = Shader.Find("Raatstad/Natuur"); if (sh == null || !sh.isSupported || !LoadSoilTex()) return null;
        m = new Material(sh) { name = "p_" + k };
        m.SetColor("_BaseColor", c); m.SetTexture("_BodemA", soilA); m.SetTexture("_BodemB", soilB); m.SetFloat("_Fine", fine); m.SetFloat("_Vary", vary);
        natureMats[k] = m; return m;
    }
    // een mesh die het bodemmateriaal krijgt zonder eigen vak (de grasbovenkant van een brugblok): gewoon gras
    static void PlainSoil(Mesh m) { m.SetColors(new Color[m.vertexCount]); m.SetUVs(0, new Vector4[m.vertexCount]); }
    static Material MatOf(string k, Color c) => NatureMat(k, c) ?? Mats.Get("p_" + k, c);
}
