using System.Threading.Tasks;
using UnityEngine;

// Berekende muziek (SpaceMusic, BuildMusic): het stuk wordt bij de start op de achtergrond uitgerekend (Render) en speelt dan
// als lus. Het volume schuift in een paar seconden naar Target × Volume; op nul staat het stil en later gaat het verder waar
// het was. Rerender rekent een nieuw stuk uit (bijvoorbeeld een andere stijl): het oude vloeit weg, dan begint het nieuwe.
// Geluid uit (Ambience.Muted) zet ook de muziek uit.
public abstract class RenderedMusic : MonoBehaviour
{
    protected const int SR = 32000;
    protected abstract string Name { get; }
    protected abstract float Volume { get; }
    protected abstract float Target { get; }   // 0..1
    protected abstract float[] Render();         // stereo, om en om links en rechts
    protected virtual void Tick() { }

    Task<float[]> job; float[] pending; bool redo; AudioSource src; float vol;

    protected virtual void Start() { job = Task.Run(Render); }
    protected void Rerender() { if (job == null) job = Task.Run(Render); else redo = true; }   // rekent hij al, dan daarna nog een keer

    void Update()
    {
        Tick();
        if (job != null && job.IsCompleted)
        {
            if (job.IsFaulted) Debug.LogWarning(Name + ": " + job.Exception?.InnerException?.Message); else if (!redo) pending = job.Result;   // al weer veranderd: dit stuk overslaan
            job = redo ? Task.Run(Render) : null; redo = false;
        }
        if (pending != null && (src == null || vol <= 0f))
        {
            var clip = AudioClip.Create(Name, pending.Length / 2, 2, SR, false); clip.SetData(pending, 0); pending = null;
            if (src == null) { src = gameObject.AddComponent<AudioSource>(); src.loop = true; src.volume = 0f; src.spatialBlend = 0f; src.playOnAwake = false; }
            else { src.Stop(); if (src.clip != null) Destroy(src.clip); }
            src.clip = clip;
        }
        if (src == null) return;
        float target = Ambience.Muted || pending != null ? 0f : Mathf.Clamp01(Target) * Volume;
        vol = Mathf.MoveTowards(vol, target, Time.unscaledDeltaTime * Volume / (pending != null ? 0.8f : 2.5f));   // in een paar seconden op en af, sneller weg voor een nieuw stuk
        src.volume = vol;
        if (vol <= 0f) { if (src.isPlaying) src.Pause(); }
        else if (!src.isPlaying) { if (src.time > 0f) src.UnPause(); else src.Play(); }
    }

    protected static double Hz(int midi) => 440.0 * System.Math.Pow(2.0, (midi - 69) / 12.0);

    // links en rechts om en om, op een piek van 0,8
    protected static float[] Interleave(float[] l, float[] r)
    {
        float mx = 0.001f; for (int i = 0; i < l.Length; i++) mx = Mathf.Max(mx, Mathf.Abs(l[i]), Mathf.Abs(r[i]));
        var d = new float[l.Length * 2]; float g = 0.8f / mx;
        for (int i = 0; i < l.Length; i++) { d[2 * i] = l[i] * g; d[2 * i + 1] = r[i] * g; }
        return d;
    }
}
