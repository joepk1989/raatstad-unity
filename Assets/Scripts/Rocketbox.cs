using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Realistische mensen uit de Microsoft Rocketbox Avatar Library (MIT, Assets/Resources/Mensen): zes figuren (vrouw, man, kind)
// met de animaties van dezelfde bibliotheek (lopen op de plek, stilstaan, zwaaien, zitten). Elk figuur speelt ze af via een
// PlayableGraph met een mixer; People.Animate zet de gewichten. Zonder de bestanden blijven de oude blokjesmensen.
public static class Rocketbox
{
    static readonly string[] Adults = { "Female_Adult_01", "Female_Adult_02", "Male_Adult_01", "Male_Adult_02" }, Kids = { "Female_Child_01", "Male_Child_01" };
    static bool tried; static bool ok;
    static readonly Dictionary<string, GameObject> models = new Dictionary<string, GameObject>();
    static readonly Dictionary<string, Material[]> mats = new Dictionary<string, Material[]>();
    static readonly Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();
    public static void ResetStatics() { tried = false; ok = false; models.Clear(); mats.Clear(); clips.Clear(); }

    public static bool Available { get { if (!tried) Load(); return ok; } }
    static void Load()
    {
        tried = true;
        foreach (var n in Adults) { var g = Resources.Load<GameObject>("Mensen/" + n + "/" + n); if (g != null) models[n] = g; }
        foreach (var n in Kids) { var g = Resources.Load<GameObject>("Mensen/" + n + "/" + n); if (g != null) models[n] = g; }
        foreach (var n in new[] { "f_idle_breathe_01", "m_idle_breathe_01", "f_walk_neutral", "m_walk_neutral", "f_wave_01", "m_wave_01", "f_sit_chair_breathe_01", "m_sit_chair_breathe_01" })
            foreach (var c in Resources.LoadAll<AnimationClip>("Mensen/Animaties/" + n)) if (!c.name.StartsWith("__preview__")) { clips[n] = c; break; }
        ok = models.Count > 0 && clips.ContainsKey("f_walk_neutral") && clips.ContainsKey("m_walk_neutral");
        if (!ok) Debug.LogWarning("Rocketbox: figuren of animaties niet gevonden (" + models.Count + " figuren, " + clips.Count + " animaties); de oude mensen blijven");
    }

    // een figuur als kind van root (voeten op root, vooruit = +z); geeft de besturing terug
    public static RbPerson Spawn(Transform root, bool female, bool kid)
    {
        if (!Available) return null;
        var pool = new List<string>(); foreach (var n in kid ? Kids : Adults) if (models.ContainsKey(n) && n.StartsWith(female ? "Female" : "Male")) pool.Add(n);
        if (pool.Count == 0) foreach (var n in models.Keys) pool.Add(n);
        string name = pool[Random.Range(0, pool.Count)];
        var go = Object.Instantiate(models[name], root, false); go.name = "Figuur";
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = root.gameObject.layer;
        foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) { r.sharedMaterials = Materials(name, r.sharedMaterials); r.updateWhenOffscreen = false; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; }
        // op ware grootte: de bibliotheek is in centimeters of meters, afhankelijk van de import
        var b = Bounds(go); float h = b.size.y; if (h > 10f) go.transform.localScale = Vector3.one * 0.01f;
        string g = name.StartsWith("Female") ? "f_" : "m_";
        var walk = Clip(g + "walk_neutral"); var anim = go.GetComponent<Animator>();
        // hoe snel de loopanimatie loopt: bij een Humanoid weet Unity dat (de voorwaartse beweging, maal de grootte van het figuur)
        float natural = anim != null && anim.isHuman && walk != null && walk.averageSpeed.magnitude > 0.1f ? walk.averageSpeed.magnitude * anim.humanScale : WalkSpeed(go, walk, name);
        var rb = go.AddComponent<RbPerson>();
        rb.Init(Clip(g + "idle_breathe_01"), walk, Clip(g + "wave_01"), Clip(g + "sit_chair_breathe_01"), natural);
        return rb;
    }
    static AnimationClip Clip(string n) => clips.TryGetValue(n, out var c) ? c : null;
    // Hoe snel loopt de animatie op de plek eigenlijk? Een voet die op de grond staat schuift met de loopsnelheid naar achteren:
    // de animatie afspelen en die snelheid meten, per figuur en animatie één keer (een kind zet kleinere stappen).
    static readonly Dictionary<string, float> walkSpeed = new Dictionary<string, float>();
    static float WalkSpeed(GameObject go, AnimationClip clip, string name)
    {
        if (clip == null) return 1.35f; string key = name + "|" + clip.name; if (walkSpeed.TryGetValue(key, out float v)) return v;
        Transform foot = null; foreach (var t in go.GetComponentsInChildren<Transform>()) if (t.name.EndsWith("L Foot")) { foot = t; break; }
        if (foot == null) { walkSpeed[key] = 1.35f; return 1.35f; }
        const int N = 90; var p = new Vector3[N]; float minH = float.MaxValue;
        for (int k = 0; k < N; k++) { clip.SampleAnimation(go, clip.length * k / N); p[k] = go.transform.InverseTransformPoint(foot.position); p[k].Scale(go.transform.lossyScale); minH = Mathf.Min(minH, p[k].y); }
        float sum = 0f; int n = 0; float dt = clip.length / N;
        for (int k = 0; k < N; k++)
        {
            Vector3 a = p[k], b = p[(k + 1) % N]; if (a.y > minH + 0.03f || b.y > minH + 0.03f) continue;   // alleen als de voet op de grond staat
            Vector2 d = new Vector2(b.x - a.x, b.z - a.z); sum += d.magnitude / dt; n++;
        }
        v = n > 3 ? Mathf.Clamp(sum / n, 0.5f, 2.5f) : 1.35f; walkSpeed[key] = v; return v;
    }
    static Bounds Bounds(GameObject go) { var b = new Bounds(go.transform.position, Vector3.zero); foreach (var r in go.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds); return b; }

    // de materialen: de textures staan als PNG naast het figuur (kleur van lijf, hoofd, en haar/wimpers met doorzichtige delen)
    static Material[] Materials(string name, Material[] src)
    {
        string key = name + "|" + src.Length; if (mats.TryGetValue(key, out var m)) return m;
        var lit = Shader.Find("Universal Render Pipeline/Lit"); var outp = new Material[src.Length];
        Texture2D Tex(string part) { foreach (var t in Resources.LoadAll<Texture2D>("Mensen/" + name)) if (t.name.EndsWith(part + "_color")) return t; return null; }
        Texture2D body = Tex("body"), head = Tex("head"), opa = Tex("opacity");
        for (int k = 0; k < src.Length; k++)
        {
            string n = src[k] != null ? src[k].name.ToLowerInvariant() : "";
            var mat = new Material(lit) { name = "rb_" + name + "_" + n }; mat.SetFloat("_Smoothness", 0.15f); mat.SetColor("_BaseColor", Color.white);
            Texture2D tex = n.Contains("head") ? head : (n.Contains("opacity") || n.Contains("hair") || n.Contains("lash")) ? (opa ?? head) : body;
            if (tex != null) mat.SetTexture("_BaseMap", tex);
            if (tex != null && tex == opa) { mat.SetFloat("_AlphaClip", 1f); mat.SetFloat("_Cutoff", 0.5f); mat.EnableKeyword("_ALPHATEST_ON"); mat.SetFloat("_Cull", 0f); }
            outp[k] = mat;
        }
        mats[key] = outp; return outp;
    }
}

// de animaties van één figuur: stilstaan, lopen, zwaaien en zitten, gemengd naar wat hij doet
public class RbPerson : MonoBehaviour
{
    PlayableGraph graph; AnimationMixerPlayable mix; AnimationClipPlayable[] cp = new AnimationClipPlayable[4]; float[] w = new float[4], want = new float[4];
    public float WalkClip => walkClipSpeed;
    public Animator Anim { get; private set; }   // voor de botten (handen en voeten aan stuur en trappers)
    float walkClipSpeed = 1.35f;   // zo snel loopt de animatie op de plek (m/s, gemeten): bij een andere snelheid sneller of trager afspelen
    public void Init(AnimationClip idle, AnimationClip walk, AnimationClip wave, AnimationClip sit, float natural)
    {
        walkClipSpeed = natural;
        var an = gameObject.GetComponent<Animator>(); if (an == null) an = gameObject.AddComponent<Animator>(); an.applyRootMotion = false; an.cullingMode = AnimatorCullingMode.CullCompletely; Anim = an;   // (geen ??: Unity-objecten zijn soms "nep-null")
        graph = PlayableGraph.Create("mens"); graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        mix = AnimationMixerPlayable.Create(graph, 4);
        var list = new[] { idle, walk, wave, sit ?? idle };
        for (int k = 0; k < 4; k++) { var c = list[k] ?? idle; cp[k] = AnimationClipPlayable.Create(graph, c); cp[k].SetTime(Random.value * (c != null ? c.length : 1f)); graph.Connect(cp[k], 0, mix, k); }
        var o = AnimationPlayableOutput.Create(graph, "uit", an); o.SetSourcePlayable(mix);
        want[0] = w[0] = 1f; Apply(); graph.Play();
    }
    void Apply() { for (int k = 0; k < 4; k++) mix.SetInputWeight(k, w[k]); }
    // walking: snelheid in m/s (0: stil); wave: zwaaien; sit: zitten (op de fiets)
    public void Set(float walking, bool wave, bool sit, float dt)
    {
        for (int k = 0; k < 4; k++) want[k] = 0f;
        if (sit) want[3] = 1f; else if (wave) want[2] = 1f; else if (walking > 0.05f) want[1] = 1f; else want[0] = 1f;
        float sum = 0f; for (int k = 0; k < 4; k++) { w[k] = Mathf.MoveTowards(w[k], want[k], dt * 5f); sum += w[k]; }
        if (sum > 0f) for (int k = 0; k < 4; k++) w[k] /= sum;
        Apply(); cp[1].SetSpeed(Mathf.Clamp(walking / walkClipSpeed, 0.3f, 3f));
        for (int k = 0; k < 4; k++) { var c = cp[k].GetAnimationClip(); if (c != null && c.length > 0f && cp[k].GetTime() > c.length) cp[k].SetTime(cp[k].GetTime() % c.length); }   // herhalen
    }
    void OnDestroy() { if (graph.IsValid()) graph.Destroy(); }
}
