using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// De studio: een plek ver van de planeet (20 km, aan de kant van de zon, zodat de planeet er nooit een schaduw op werpt) waar
// voorbeeldplaatjes van modules worden gemaakt en waar de Raatmaker tekent. Zolang een studiocamera tekent, staat de zon op
// 13:00 en helder (zoals de webversie de Raatmaker altijd bij helder weer om 13:00 laat zien); daarna komt het echte licht terug.
public static class ModuleStudio
{
    public static readonly Quaternion SunRot = Quaternion.Euler(50f, -30f, 0f);
    public static Vector3 Pos => -(SunRot * Vector3.forward) * 20000f;
    static readonly HashSet<Camera> cams = new HashSet<Camera>();
    static bool hooked; static Light sun;
    static Quaternion sRot; static float sInt; static Color sCol, sAmb, sLit; static bool sFog; static AmbientMode sMode;

    public static void Register(Camera c)
    {
        cams.Add(c);
        if (hooked) return; hooked = true;
        RenderPipelineManager.beginCameraRendering += Begin; RenderPipelineManager.endCameraRendering += End;
    }
    static void Begin(ScriptableRenderContext ctx, Camera c)
    {
        if (!cams.Contains(c)) return;
        if (sun == null) foreach (var l in Object.FindObjectsByType<Light>()) if (l.type == LightType.Directional) { sun = l; break; }
        if (sun != null) { sRot = sun.transform.rotation; sInt = sun.intensity; sCol = sun.color; sun.transform.rotation = SunRot; sun.intensity = 1.1f; sun.color = new Color(1f, 0.97f, 0.92f); }
        sAmb = RenderSettings.ambientLight; sFog = RenderSettings.fog; sMode = RenderSettings.ambientMode;
        RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.58f, 0.6f, 0.64f); RenderSettings.fog = false;
        var lit = ModuleMesh.Mat("lit"); sLit = lit.color; lit.color = ModuleMesh.LitDay;   // in de studio is het dag
    }
    static void End(ScriptableRenderContext ctx, Camera c)
    {
        if (!cams.Contains(c)) return;
        if (sun != null) { sun.transform.rotation = sRot; sun.intensity = sInt; sun.color = sCol; }
        RenderSettings.ambientLight = sAmb; RenderSettings.fog = sFog; RenderSettings.ambientMode = sMode;
        ModuleMesh.Mat("lit").color = sLit;
    }

    // ---------- voorbeeldplaatjes ----------
    static Camera thumbCam; static Mesh plate0, plate1;
    static readonly Dictionary<string, (int ver, RenderTexture rt)> thumbs = new Dictionary<string, (int, RenderTexture)>();
    static readonly List<ModuleV2> todo = new List<ModuleV2>();
    // het plaatje als het er al is (anders komt het in de rij en verschijnt het een paar beelden later)
    public static Texture Thumb(ModuleV2 m)
    {
        if (m == null) return null;
        if (thumbs.TryGetValue(m.id, out var t) && t.rt != null && t.ver == m.ver) return t.rt;
        if (!todo.Contains(m)) todo.Add(m);
        return t.rt;   // het oude plaatje zolang het nieuwe er nog niet is
    }
    // een paar plaatjes per beeld maken (vanuit Update, niet tijdens OnGUI)
    public static void Tick(int max = 3)
    {
        for (int n = 0; n < max && todo.Count > 0; n++) { var m = todo[todo.Count - 1]; todo.RemoveAt(todo.Count - 1); try { Render(m); } catch (System.Exception e) { Debug.LogWarning("Voorbeeldplaatje " + m.name + ": " + e.Message); } }
    }
    public static void Forget(string id) { if (thumbs.TryGetValue(id, out var t) && t.rt != null) { t.rt.Release(); Object.Destroy(t.rt); } thumbs.Remove(id); }
    public static Mesh Plate(int k)
    {
        if (k > 0 ? plate1 != null : plate0 != null) return k > 0 ? plate1 : plate0;
        var mb = new MeshBuilder(); float r = MV2.R * (1 + 2 * k) * 0.98f; var p = new Vector3[6];
        for (int i = 0; i < 6; i++) { float a = (30f + 60f * i) * Mathf.Deg2Rad; p[i] = new Vector3(Mathf.Cos(a) * r, 0f, -Mathf.Sin(a) * r); }
        for (int i = 0; i < 6; i++) { mb.Tri(Vector3.zero, p[i], p[(i + 1) % 6], Vector3.up); mb.Quad(p[i], p[(i + 1) % 6], p[(i + 1) % 6] + Vector3.down * 0.4f, p[i] + Vector3.down * 0.4f, (p[i] + p[(i + 1) % 6]) * 0.5f); }
        var mesh = mb.Build(); if (k > 0) plate1 = mesh; else plate0 = mesh; return mesh;
    }
    static void Render(ModuleV2 m)
    {
        if (thumbCam == null)
        {
            var go = new GameObject("Modulefoto"); thumbCam = go.AddComponent<Camera>(); thumbCam.enabled = false; thumbCam.orthographic = true;
            thumbCam.clearFlags = CameraClearFlags.SolidColor; thumbCam.backgroundColor = new Color(0f, 0f, 0f, 0f); thumbCam.nearClipPlane = 1f; thumbCam.farClipPlane = 1000f; Register(thumbCam);
        }
        var b = ModuleMesh.Get(m, false, 0);
        var mod = new GameObject("foto"); mod.transform.position = Pos;
        mod.AddComponent<MeshFilter>().sharedMesh = b.mesh; mod.AddComponent<MeshRenderer>().sharedMaterials = b.mats;
        var plate = new GameObject("plaat"); plate.transform.position = Pos + Vector3.down * 0.02f; plate.AddComponent<MeshFilter>().sharedMesh = Plate(m.k); plate.AddComponent<MeshRenderer>().sharedMaterial = Mats.Get("mod_plaat", MV2.Hex("#c8dca4"));
        float ext = MV2.R * (1 + 2 * m.k) * 1.05f, h = m.lv.Count * 3f + 2f, d = Mathf.Max(ext * 1.9f, h * 1.6f);
        thumbCam.orthographicSize = d / 2f; thumbCam.transform.position = Pos + new Vector3(d * 0.8f, d * 0.75f, -d * 0.8f); thumbCam.transform.LookAt(Pos + Vector3.up * h * 0.3f);
        if (!thumbs.TryGetValue(m.id, out var t) || t.rt == null) t.rt = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        thumbCam.targetTexture = t.rt; thumbCam.Render(); thumbCam.targetTexture = null;
        Object.DestroyImmediate(mod); Object.DestroyImmediate(plate);
        thumbs[m.id] = (m.ver, t.rt);
    }
}
