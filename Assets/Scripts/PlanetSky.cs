using UnityEngine;

// De lucht van de planeet. Dichtbij een skybox (Lucht.shader) met een verloop van de kim naar boven, de zon met een gloed,
// oranje bij zonsopgang en -ondergang en sterren 's nachts; de kleuren volgen DayNight en Weather (de kim is de luchtkleur
// van DayNight, die ook de mist kleurt). Van ver weg, als je de bol ziet: de ruimte als achtergrond, een dunne blauwe rand
// (Atmosfeer.shader) en een laag wolken op 200 m (Wolken.shader) die langzaam om de as draait. Bij inzoomen vervagen de
// wolken en de rand. Alles hangt aan de hoogte van de camera boven de bol; onzichtbare delen worden niet getekend.
public class PlanetSky : MonoBehaviour
{
    public const float CloudAlt = 200f, AtmoTop = 260f, ScaleH = 40f;   // wolken en bovenkant van de lucht (m boven de bol), schaalhoogte
    Material sky, atmo, clouds; MeshRenderer atmoR, cloudR; Transform cloudT; Light sun; Camera cam;
    float spin;
    public static float Space;   // 0 = in de lucht, 1 = in de ruimte (ook voor de muziek en de wind)

    void Start()
    {
        cam = Camera.main;
        sky = new Material(Shader.Find("Raatstad/Lucht"));
        atmo = new Material(Shader.Find("Raatstad/Atmosfeer"));
        clouds = new Material(Shader.Find("Raatstad/Wolken"));
        RenderSettings.skybox = sky;
        if (cam != null) cam.clearFlags = CameraClearFlags.Skybox;
        Space = 0f; gameObject.AddComponent<SpaceMusic>(); gameObject.AddComponent<BuildMusic>();
        var ball = Sphere(96, 48);
        atmoR = Shell("Atmosfeer", ball, Planet.R + AtmoTop, atmo);
        cloudR = Shell("Wolken", ball, Planet.R + CloudAlt, clouds); cloudT = cloudR.transform;
        atmo.SetFloat("_R", Planet.R); atmo.SetFloat("_Ra", Planet.R + AtmoTop); atmo.SetFloat("_H", ScaleH); atmo.SetFloat("_Strength", 0.0045f);
        atmo.SetColor("_Color", new Color(0.32f, 0.58f, 1f));
    }

    MeshRenderer Shell(string name, Mesh m, float r, Material mat)
    {
        var go = new GameObject(name); go.transform.SetParent(transform, false); go.transform.localScale = Vector3.one * r;
        go.AddComponent<MeshFilter>().sharedMesh = m;
        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.enabled = false;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        return mr;
    }

    // een bol met straal 1 (lengte- en breedtecirkels; de wolken nemen hun patroon uit de richting, dus de polen geven niets raars)
    static Mesh Sphere(int lon, int lat)
    {
        var v = new Vector3[(lon + 1) * (lat + 1)]; var idx = new int[lon * lat * 6]; int n = 0;
        for (int j = 0; j <= lat; j++) for (int i = 0; i <= lon; i++)
        {
            float th = Mathf.PI * j / lat, ph = 2f * Mathf.PI * i / lon;
            v[j * (lon + 1) + i] = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
        }
        for (int j = 0; j < lat; j++) for (int i = 0; i < lon; i++)
        {
            int a = j * (lon + 1) + i, b = a + lon + 1;
            idx[n++] = a; idx[n++] = a + 1; idx[n++] = b; idx[n++] = a + 1; idx[n++] = b + 1; idx[n++] = b;
        }
        var m = new Mesh { name = "Bol" }; m.vertices = v; m.triangles = idx; m.RecalculateNormals(); m.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
        return m;
    }

    void LateUpdate()
    {
        if (cam == null) { cam = Camera.main; if (cam == null) return; }
        if (sun == null) foreach (var l in FindObjectsByType<Light>()) if (l.type == LightType.Directional) { sun = l; break; }
        if (cam.clearFlags != CameraClearFlags.Skybox) cam.clearFlags = CameraClearFlags.Skybox;
        if (RenderSettings.skybox != sky) RenderSettings.skybox = sky;

        Vector3 cp = cam.transform.position; float r = cp.magnitude; Vector3 up = r > 1f ? cp / r : Vector3.up;
        float ground = Planet.R + Mathf.Max(0f, PlanetCity.TerrainAt(up)), alt = r - Planet.R;
        Vector3 sunDir = sun != null ? -sun.transform.forward : Vector3.up;

        // hoe ver weg: 0 = in de lucht, 1 = in de ruimte; de wolken en de rand komen iets later
        float space = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(320f, 1100f, alt)); Space = space;
        float cloudFade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(480f, 1000f, alt));
        float rimFade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(260f, 800f, alt));

        // kleuren dichtbij (zelfde dag/nacht als DayNight: hoogte van de zon uit het uur)
        float elev = Mathf.Sin((DayNight.Hour - 6f) / 24f * 2f * Mathf.PI), night = DayNight.Night, cloud = Weather.Cloud;
        Color horizon = cam.backgroundColor;
        Color zen = Color.Lerp(Color.Lerp(new Color(0.24f, 0.3f, 0.55f), new Color(0.2f, 0.42f, 0.86f), Mathf.Clamp01(elev * 3f)), new Color(0.006f, 0.01f, 0.035f), night);
        zen = Color.Lerp(zen, horizon * 0.92f, cloud * 0.8f);
        float dusk = Mathf.Clamp01(1f - Mathf.Abs(elev - 0.05f) * 4.5f) * (1f - cloud * 0.75f);
        float hmu = r > ground ? -Mathf.Sqrt(Mathf.Max(0f, 1f - (ground / r) * (ground / r))) : 0f;   // de kim zakt als je hoger komt
        sky.SetVector("_Up", up); sky.SetVector("_SunDir", sunDir); sky.SetFloat("_HorizonMu", Mathf.Max(hmu, -0.95f));
        sky.SetColor("_Zenith", zen); sky.SetColor("_Horizon", horizon);
        sky.SetColor("_Dusk", new Color(1f, 0.42f, 0.14f, dusk * 0.85f));
        sky.SetColor("_SunCol", sun != null ? sun.color : Color.white);
        sky.SetFloat("_SunVis", (1f - night) * (1f - cloud * 0.95f));
        sky.SetFloat("_Stars", Mathf.Clamp01((night - 0.55f) * 2.5f) * (1f - cloud * 0.95f));
        sky.SetFloat("_Space", space);

        // de rand om de bol
        atmoR.enabled = rimFade > 0.01f;
        if (atmoR.enabled) { atmo.SetVector("_SunDir", sunDir); atmo.SetFloat("_Fade", rimFade); }

        // de wolkenlaag draait langzaam om de as (een rondje in 25 minuten) en verandert langzaam van vorm
        spin = Mathf.Repeat(spin + Time.deltaTime * 0.24f, 360f);
        cloudR.enabled = cloudFade > 0.01f && r > Planet.R + CloudAlt + 20f;
        if (cloudR.enabled)
        {
            cloudT.rotation = Quaternion.AngleAxis(spin, Planet.Axis);
            clouds.SetVector("_SunDir", sunDir); clouds.SetFloat("_Fade", cloudFade);
            clouds.SetFloat("_Cover", Mathf.Clamp01(cloud * 1.1f)); clouds.SetFloat("_Shift", Time.time * 0.004f);
            clouds.SetColor("_Tint", Color.Lerp(Color.white, new Color(0.55f, 0.57f, 0.62f), Weather.Storm * 0.8f + Weather.Rain * 0.2f));
        }

        // de mist van Weather hoort bij de grond: hoger op schuift hij weg, in de ruimte is hij uit
        if (RenderSettings.fog)
        {
            float k = Mathf.Max(0f, alt - 200f);
            if (space > 0.85f) RenderSettings.fog = false;
            else { RenderSettings.fogStartDistance += k * 1.5f; RenderSettings.fogEndDistance += k * 3f; }
        }
    }

    void OnDestroy()
    {
        if (sky != null) Destroy(sky); if (atmo != null) Destroy(atmo); if (clouds != null) Destroy(clouds);
    }
}
