using System.Collections.Generic;
using UnityEngine;

// Natuur: bomen los op het terrein, zonder grasplaat en zonder sokkel. Een vak met alleen "park" is geen plateau: het terrein loopt
// er gewoon door (boetseren kan er ook) en elke boom staat op de grond onder zijn eigen stam. Vier soorten, low poly en zeshoekig,
// op ware grootte: loofboom (8-11 m), den (11-15 m), berk (9-12 m) en struik. De soort hangt af van de hoogte (hoger meer naaldbomen),
// de helling (steiler dan 35° alleen struiken) en een grove ruis, zodat een bos vooral uit één soort bestaat.
public static partial class PlanetCity
{
    static bool LooseTrees(List<string> l) => l.Count == 1 && l[0] == "park";

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
                float pine = Mathf.Clamp01((H - 6f) / 14f) * 0.8f + (wood > 0.62f ? 0.45f : 0f), birch = wood < 0.38f ? 0.45f : 0.1f, q = (float)rnd.NextDouble();
                kind = q < pine ? 1 : q < pine + birch ? 2 : 0;
            }
            float rot = (float)rnd.NextDouble() * Mathf.PI / 3f;
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

    static bool NatureColor(string k, out Color c)
    {
        switch (k)
        {
            case "loof2": c = new Color(0.36f, 0.56f, 0.22f); return true;
            case "naald": c = new Color(0.13f, 0.34f, 0.21f); return true;
            case "berk": c = new Color(0.88f, 0.86f, 0.80f); return true;
            case "berkblad": c = new Color(0.50f, 0.66f, 0.28f); return true;
            case "struik": c = new Color(0.29f, 0.50f, 0.23f); return true;
            default: c = default; return false;
        }
    }
}
