using System.Collections.Generic;
using UnityEngine;

// De krater zakt weg in plaats van in één beeld te verspringen. Het terrein krijgt meteen zijn nieuwe hoogtes en wordt per beeld
// met een budget opnieuw getekend (PlanetBuilder.MeteorSettle); elk vers getekend stuk grond rond de krater wordt hier, nog vóór
// het in beeld komt (LateUpdate), terug naar de oude vorm geduwd en zakt dan in een paar seconden naar de nieuwe (de rand komt
// omhoog). Het verschil per hoekpunt is het verschil per vak, zacht verdeeld over de vakken eromheen; aan het eind staat de mesh
// weer precies zoals PlanetCity hem tekende. Undo of een ander stuk werk ertussen (het midden heeft niet meer de nieuwe hoogte):
// meteen stoppen. Alleen de zichtbare mesh beweegt; de botsing (MeshCollider) heeft al de nieuwe vorm.
public partial class PlanetMeteor
{
    class Morph
    {
        public int c0; public float t, dur, finalH, cosR; public Vector3 dir; public Vector3[] cdir; public float[] cdh; public Bounds box;
        public readonly Dictionary<Mesh, MorphMesh> meshes = new Dictionary<Mesh, MorphMesh>(); public readonly HashSet<Mesh> skip = new HashSet<Mesh>();
    }
    class MorphMesh { public Vector3[] fin, cur, ofs; public int[] idx; }
    readonly List<Morph> morphs = new List<Morph>(); Transform groundRoot;   // een meteorenregen: meer kraters tegelijk

    void BeginMorph(int c0, int size, Dictionary<int, float> delta, float reach)
    {
        Vector3 dir = Planet.Cells[c0].dir; float R = Planet.R;
        var morph = new Morph { c0 = c0, dur = 2.2f + 0.5f * size, finalH = PlanetCity.G(c0), dir = dir, cosR = Mathf.Cos(reach / R), cdir = new Vector3[delta.Count], cdh = new float[delta.Count] };
        int k = 0; foreach (var kv in delta) { morph.cdir[k] = Planet.Cells[kv.Key].dir; morph.cdh[k] = kv.Value; k++; }
        morph.box = new Bounds(dir * R, Vector3.one * (reach * 2f + 60f));
        for (int i = morphs.Count - 1; i >= 0; i--) if (morphs[i].box.Intersects(morph.box)) EndMorph(morphs[i]);   // overlappend: de vorige staat meteen klaar (twee vervormingen op één mesh botsen)
        morphs.Add(morph);
    }

    void UpdateMorph() { for (int i = morphs.Count - 1; i >= 0; i--) UpdateMorph(morphs[i]); }
    void UpdateMorph(Morph m)
    {
        m.t += Time.deltaTime; float f = Mathf.Clamp01(m.t / m.dur); f = 1f - (1f - f) * (1f - f) * (1f - f);   // snel weg, rustig tot stilstand
        if (f >= 1f || Mathf.Abs(PlanetCity.G(m.c0) - m.finalH) > 0.05f) { EndMorph(m); return; }
        if (groundRoot == null) { var g = GameObject.Find("Bodem"); if (g == null) return; groundRoot = g.transform; }
        foreach (Transform ch in groundRoot)
        {
            string n = ch.name; if (!(n.StartsWith("Bodem ") || n.StartsWith("Terrein ") || n.StartsWith("Vijfhoeken ") || n.StartsWith("Startplekken "))) continue;
            var r = ch.GetComponent<MeshRenderer>(); if (r == null || !r.bounds.Intersects(m.box)) continue;
            var mf = ch.GetComponent<MeshFilter>(); var mesh = mf != null ? mf.sharedMesh : null; if (mesh == null || m.skip.Contains(mesh)) continue;
            if (!m.meshes.TryGetValue(mesh, out var mm)) { mm = Capture(m, ch, mesh); if (mm == null) { m.skip.Add(mesh); continue; } m.meshes[mesh] = mm; }
            float k = 1f - f; for (int i = 0; i < mm.idx.Length; i++) mm.cur[mm.idx[i]] = mm.fin[mm.idx[i]] + mm.ofs[i] * k;
            mesh.vertices = mm.cur; mesh.RecalculateBounds();
        }
    }
    void EndMorph(Morph m)
    {
        foreach (var kv in m.meshes) if (kv.Key != null) { kv.Key.vertices = kv.Value.fin; kv.Key.RecalculateBounds(); }
        morphs.Remove(m);
    }

    // de hoekpunten van een vers getekend stuk grond binnen het bereik, met hoe ver ze terug moeten naar de oude vorm
    MorphMesh Capture(Morph m, Transform t, Mesh mesh)
    {
        var fin = mesh.vertices; var idx = new List<int>(); var ofs = new List<Vector3>();
        for (int i = 0; i < fin.Length; i++)
        {
            Vector3 w = t.TransformPoint(fin[i]), d = w.normalized; if (Vector3.Dot(d, m.dir) < m.cosR) continue;
            float dh = Delta(m, d); if (Mathf.Abs(dh) < 0.01f) continue;
            idx.Add(i); ofs.Add(t.InverseTransformVector(-d * dh));
        }
        if (idx.Count == 0) return null;
        return new MorphMesh { fin = fin, cur = (Vector3[])fin.Clone(), idx = idx.ToArray(), ofs = ofs.ToArray() };
    }
    // het hoogteverschil op een plek: de verschillen van de vakken eromheen, gewogen naar afstand (de buitenste ring heeft verschil 0)
    static float Delta(Morph m, Vector3 d)
    {
        float sw = 0f, s = 0f, sig = CellW * 0.55f, inv = Planet.R * Planet.R / (sig * sig);
        for (int k = 0; k < m.cdir.Length; k++)
        {
            float q = (m.cdir[k] - d).sqrMagnitude * inv; if (q > 6.25f) continue;   // verder dan 2,5 sigma telt niet mee
            float w = Mathf.Exp(-q); sw += w; s += w * m.cdh[k];
        }
        return sw > 1e-6f ? s / sw : 0f;
    }
}
