using System.Collections.Generic;
using UnityEngine;

// Blauwdruk tijdens het opbouwen van een planeet (in plaats van een laadscherm): over de bol een donkerblauwe laag met een fijn
// zeshoekraster, en wat er bedacht wordt (dorpen, wegen, terrein) als lichtblauwe lijntekening op zijn hoogte. Tijdens het
// tekenen vervaagt de blauwdruk en komt de echte planeet eronder tevoorschijn (Fade), daarna verdwijnt hij (Hide).
public class PlanetBlueprint : MonoBehaviour
{
    static PlanetBlueprint inst;
    public static PlanetBlueprint I { get { if (inst == null) inst = new GameObject("Blauwdruk").AddComponent<PlanetBlueprint>(); return inst; } }

    MeshRenderer shellR, paperR, drawR; MeshFilter drawF; Material shellM, paperM, drawM;
    float alpha, target, fadeSpeed = 2f, drawT; int drawN = -1;
    static readonly Color Shell = new Color(0.04f, 0.13f, 0.3f), Paper = new Color(0.45f, 0.7f, 1f), Ink = new Color(0.72f, 0.9f, 1f);

    void Build()
    {
        if (shellR != null) return;
        // de laag: een bol net boven zeeniveau (de lege planeet eronder valt erachter weg)
        var sh = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(sh.GetComponent<Collider>()); sh.name = "Blauwdruk laag"; sh.transform.SetParent(transform, false);
        sh.transform.localScale = Vector3.one * 2f * (Planet.R + 0.5f); shellR = sh.GetComponent<MeshRenderer>();
        shellM = Raatstad.MakeMat(new Color(Shell.r, Shell.g, Shell.b, 0.5f), true); shellR.sharedMaterial = shellM; shellR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // het papier: alle vakranden van de bol, fijn en vaag (één keer gemaakt)
        var pv = new List<Vector3>(); var pi = new List<int>();
        foreach (var c in Planet.Cells) { int m = c.corners.Length; for (int k = 0; k < m; k++) { pi.Add(pv.Count); pv.Add(c.corners[k] * (Planet.R + 0.7f)); pi.Add(pv.Count); pv.Add(c.corners[(k + 1) % m] * (Planet.R + 0.7f)); } }
        paperR = LineObj("Blauwdruk raster", pv, pi, out _, out paperM, new Color(Paper.r, Paper.g, Paper.b, 0.18f));
        drawR = LineObj("Blauwdruk tekening", new List<Vector3>(), new List<int>(), out drawF, out drawM, new Color(Ink.r, Ink.g, Ink.b, 0.9f));
        shellM.renderQueue = 3000; paperM.renderQueue = 3001; drawM.renderQueue = 3002;   // eerst de laag, dan het raster, dan de tekening erbovenop
    }
    MeshRenderer LineObj(string name, List<Vector3> v, List<int> idx, out MeshFilter mf, out Material mat, Color col)
    {
        var go = new GameObject(name); go.transform.SetParent(transform, false); mf = go.AddComponent<MeshFilter>(); var mr = go.AddComponent<MeshRenderer>();
        var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; mesh.SetVertices(v); mesh.SetIndices(idx, MeshTopology.Lines, 0); mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 4f * Planet.R);
        mf.sharedMesh = mesh; mat = Raatstad.MakeMat(col, true); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false; return mr;
    }

    // de tekening: elk bebouwd vak als zeshoek op zijn hoogte (gebouwen een tweede, hogere rand) en elke weg als lijn van hart tot hart
    void Redraw()
    {
        var v = new List<Vector3>(); var idx = new List<int>();
        void Seg(Vector3 a, Vector3 b) { idx.Add(v.Count); v.Add(a); idx.Add(v.Count); v.Add(b); }
        foreach (var kv in PlanetCity.Cells)
        {
            if (kv.Value.Count == 0) continue; var c = Planet.Cells[kv.Key]; int m = c.corners.Length;
            float g = Mathf.Max(0f, PlanetCity.G(kv.Key)) + 1f, top = g + Mathf.Min(PlanetCity.StackHeight(kv.Key), 30f);
            bool walk = PlanetCity.IsWalk(kv.Key);
            for (int k = 0; k < m; k++)
            {
                Vector3 a = c.corners[k], b = c.corners[(k + 1) % m];
                if (!walk) Seg(Vector3.Lerp(c.dir, a, 0.9f).normalized * (Planet.R + g), Vector3.Lerp(c.dir, b, 0.9f).normalized * (Planet.R + g));
                if (top > g + 1f) { Seg(Vector3.Lerp(c.dir, a, 0.8f).normalized * (Planet.R + top), Vector3.Lerp(c.dir, b, 0.8f).normalized * (Planet.R + top)); if (k % 2 == 0) Seg(Vector3.Lerp(c.dir, a, 0.8f).normalized * (Planet.R + g), Vector3.Lerp(c.dir, a, 0.8f).normalized * (Planet.R + top)); }
            }
            if (walk) foreach (int j in PlanetCity.Partners(kv.Key)) if (j > kv.Key)
                    Seg(c.dir * (Planet.R + g + 0.5f), Planet.Cells[j].dir * (Planet.R + Mathf.Max(0f, PlanetCity.G(j)) + 1.5f));
        }
        var mesh = drawF.sharedMesh; mesh.Clear(); mesh.SetVertices(v); mesh.SetIndices(idx, MeshTopology.Lines, 0); mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 4f * Planet.R);
    }

    // aan: de blauwdruk komt op (de echte planeet erachter blijft zichtbaar door de halfdoorzichtige laag)
    public void Show() { Build(); gameObject.SetActive(true); target = 1f; fadeSpeed = 2.5f; drawN = -1; drawT = 0f; }
    // tijdens het tekenen: 1 = helemaal blauwdruk, 0 = de echte planeet
    public void Fade(float f) { target = Mathf.Clamp01(f); fadeSpeed = 4f; }
    public void Hide(float seconds = 0.8f) { target = 0f; fadeSpeed = 1f / Mathf.Max(0.05f, seconds); }
    // even oplichten en wegvagen (startstad, lege planeet)
    public void Flash(float seconds = 1.6f) { Show(); alpha = 1f; drawN = -1; Redraw(); target = 0f; fadeSpeed = 1f / seconds; }

    void Update()
    {
        if (shellR == null) return;
        alpha = Mathf.MoveTowards(alpha, target, fadeSpeed * Time.unscaledDeltaTime);
        if (target > 0f && Time.unscaledTime > drawT && PlanetCity.Cells.Count != drawN) { drawN = PlanetCity.Cells.Count; drawT = Time.unscaledTime + 0.25f; Redraw(); }   // bijtekenen wat er bedacht is
        float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 2.2f);
        SetA(shellM, Shell, 0.62f * alpha); SetA(paperM, Paper, 0.2f * alpha); SetA(drawM, Ink, 0.95f * alpha * pulse);
        bool on = alpha > 0.002f; shellR.enabled = paperR.enabled = drawR.enabled = on;
    }
    static void SetA(Material m, Color c, float a) { var k = new Color(c.r, c.g, c.b, a); m.color = k; if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", k); }
}
