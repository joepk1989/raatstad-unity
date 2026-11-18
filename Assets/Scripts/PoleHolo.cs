using UnityEngine;

// De polen als hologram: een grote N (noordpool, blauw) of Z (zuidpool, rood) die hoog boven het ijs zweeft, zonder paal, in een
// zeshoekige lijst. Licht gevend en doorschijnend (opgeteld licht, geen schaduw, geen botsing), draait langzaam rond, deint een
// beetje op en neer en flikkert zacht. Gemaakt per bodemdeel door BuildGround0 (PlanetCity.Ground.cs) en daarmee ook weggehaald.
public class PoleHolo : MonoBehaviour
{
    const float Height = 75f;      // de onderkant van de lijst boven het ijs
    const float LetterH = 16f;     // hoogte van de letter
    const float Depth = 1.4f;      // dikte van de letter en de lijst
    const float FrameR = 13f, FrameW = 0.7f;   // de zeshoekige lijst: straal tot de buitenhoek, breedte van de rand
    static Material matN, matZ;

    Vector3 basePos, up, rimPos; Quaternion rimRot; Transform rim; float phase; Material mat; Color col;
    const float RimW = 0.45f, RimUp = 0.12f;   // breedte van de rand langs de binnenkant van de vakgrens, en hoe ver boven de grond

    public static GameObject Make(Transform parent, Planet.Cell c, float ground, bool north)
    {
        var g = new GameObject(north ? "Pool hologram N" : "Pool hologram Z"); g.transform.SetParent(parent, false); g.layer = Layers.Bodem;
        Vector3 up = c.dir;
        g.transform.position = up * (Planet.R + ground + Height + FrameR);
        g.transform.rotation = Quaternion.LookRotation(c.north, up);
        g.AddComponent<MeshFilter>().sharedMesh = BuildMesh(north);
        var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = Mat(north);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        var h = g.AddComponent<PoleHolo>(); h.basePos = g.transform.position; h.up = up; h.phase = north ? 0f : 1.7f; h.mat = mr.sharedMaterial; h.col = Tint(north);
        // op de grond: een smalle oplichtende rand langs de vakgrens (geen gekleurd vak), in dezelfde kleur, iets zachter
        var rg = new GameObject("Pool rand"); rg.transform.SetParent(g.transform, false);   // (zelfde plek en stand als het hologram: de punten van RimMesh staan in zijn coördinaten) rg.layer = Layers.Bodem;
        rg.AddComponent<MeshFilter>().sharedMesh = RimMesh(c, g.transform);
        var rr = rg.AddComponent<MeshRenderer>(); rr.sharedMaterial = Mat(north); rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; rr.receiveShadows = false;
        h.rim = rg.transform; h.rimPos = rg.transform.position; h.rimRot = rg.transform.rotation;
        return g;
    }

    static Color Tint(bool north) => north ? new Color(0.35f, 0.75f, 1f, 0.55f) : new Color(1f, 0.42f, 0.38f, 0.55f);

    // opgeteld licht, beide kanten zichtbaar, schrijft niet in de diepte (zoals de vonken van de meteoor)
    static Material Mat(bool north)
    {
        ref Material m = ref north ? ref matN : ref matZ;
        if (m != null) return m;
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit"); if (sh == null) sh = Shader.Find("Sprites/Default");
        m = new Material(sh) { name = north ? "Hologram N" : "Hologram Z" };
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f); if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 2f);   // 2: optellen
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f); if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = 3100;
        var c = Tint(north); if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); m.color = c;
        return m;
    }

    // De rand op de grond: per zijde van het vak een smalle strook aan de binnenkant, op de grondhoogte van de hoeken (wereldcoördinaten
    // omgerekend naar die van het hologram, maar de rand draait en deint niet mee: Update zet hem elke keer terug)
    static Mesh RimMesh(Planet.Cell c, Transform t)
    {
        var mb = new MeshBuilder(); int m = c.corners.Length;
        Vector3 P(Vector3 d) { d = d.normalized; return t.InverseTransformPoint(d * (Planet.R + PlanetCity.TerrainAt(d) + RimUp)); }
        Vector3 In(Vector3 corner) => Vector3.Lerp(corner, c.dir, RimW / Mathf.Max(1f, (corner - c.dir).magnitude * Planet.R));
        for (int k = 0; k < m; k++)
        {
            Vector3 a = c.corners[k], b = c.corners[(k + 1) % m];
            mb.Quad(P(a), P(b), P(In(b)), P(In(a)), t.InverseTransformDirection(c.dir));
        }
        return mb.Build();
    }

    // De letter (drie balken, zoals op de oude vlag) en de zeshoekige lijst eromheen, als blokken met dikte; x opzij, y omhoog, z de dikte
    static Mesh BuildMesh(bool north)
    {
        var mb = new MeshBuilder();
        float s = LetterH / 4.2f, cx = 4.5f, cy = 3.0f;   // vlagcoördinaten van de oude letter: x 2,6..6,4 en y 0,9..5,1
        Vector2 L(float x, float y) => new Vector2((x - cx) * s, (y - cy) * s);
        float x0 = 2.6f, x1 = 6.4f, yB = 0.9f, yT = 5.1f, sw = 0.85f;
        var bars = north
            ? new[] {
                new[] { L(x0, yB), L(x0 + sw, yB), L(x0 + sw, yT), L(x0, yT) },
                new[] { L(x1 - sw, yB), L(x1, yB), L(x1, yT), L(x1 - sw, yT) },
                new[] { L(x1 - sw, yB), L(x1, yB), L(x0 + sw, yT), L(x0, yT) } }
            : new[] {
                new[] { L(x0, yT - sw), L(x1, yT - sw), L(x1, yT), L(x0, yT) },
                new[] { L(x0, yB), L(x1, yB), L(x1, yB + sw), L(x0, yB + sw) },
                new[] { L(x0, yB + sw), L(x0 + sw * 1.2f, yB + sw), L(x1, yT - sw), L(x1 - sw * 1.2f, yT - sw) } };
        foreach (var b in bars) Slab(mb, b, Depth);
        // de zeshoekige lijst: zes balken tussen de hoeken, een punt boven en onder
        for (int k = 0; k < 6; k++)
        {
            float a0 = Mathf.PI / 2f + k * Mathf.PI / 3f, a1 = a0 + Mathf.PI / 3f;
            Vector2 o0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * FrameR, o1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * FrameR;
            Vector2 i0 = o0 * ((FrameR - FrameW) / FrameR), i1 = o1 * ((FrameR - FrameW) / FrameR);
            Slab(mb, new[] { i0, o0, o1, i1 }, Depth * 0.6f);
        }
        return mb.Build();
    }

    // een vierhoek in het vlak (x, y) als blok van dikte d: voor, achter en de vier zijkanten
    static void Slab(MeshBuilder mb, Vector2[] q, float d)
    {
        var f = new Vector3[4]; var bk = new Vector3[4];
        for (int k = 0; k < 4; k++) { f[k] = new Vector3(q[k].x, q[k].y, -d * 0.5f); bk[k] = new Vector3(q[k].x, q[k].y, d * 0.5f); }
        mb.Quad(f[0], f[1], f[2], f[3], Vector3.back); mb.Quad(bk[3], bk[2], bk[1], bk[0], Vector3.forward);
        for (int k = 0; k < 4; k++) { int k1 = (k + 1) % 4; Vector3 mid = (f[k] + f[k1]) * 0.5f; mb.Quad(f[k], bk[k], bk[k1], f[k1], new Vector3(mid.x, mid.y, 0f)); }
    }

    void Update()
    {
        float t = Time.time + phase;
        transform.position = basePos + up * (Mathf.Sin(t * 0.9f) * 1.2f);                 // deinen
        transform.rotation = Quaternion.AngleAxis(Time.deltaTime * 14f, up) * transform.rotation;   // langzaam rond
        if (rim != null) { rim.position = rimPos; rim.rotation = rimRot; }   // de rand op de grond blijft liggen
        if (mat != null)   // zacht flikkeren, af en toe een korte hapering
        {
            float a = col.a * (0.82f + 0.18f * Mathf.Sin(t * 7.3f)) * (Mathf.Repeat(t, 5.3f) < 0.08f ? 0.35f : 1f);
            var c = new Color(col.r, col.g, col.b, a); if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c); else mat.color = c;
        }
    }
}
