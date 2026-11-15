using System.Collections.Generic;
using UnityEngine;

// Sloopbaar: losse dingen in de getekende stad (lantaarns, palen, bankjes, bomen, struiken) en gebouwen. Alles staat samengevoegd
// in grote meshes per deel (BuildChunk) of per stuk weg (Piece). Tijdens het tekenen onthoudt PropBegin/PropEnd welke hoekpunten
// van welke mesh bij één ding horen; Break haalt die hoekpunten uit de grote mesh (ze vallen op één punt, de botsvorm volgt) en
// geeft een los object terug met precies dezelfde vorm, dat kan omvallen of instorten (PlanetBuilder.Wreck.cs).
// Wat stuk is blijft weg zolang het spel draait, ook als het deel opnieuw getekend wordt (propBroken, op de plek). Gebouwen gaan
// echt uit de stad (PlanetBuilder.Wreck.cs: Clear, met undo en opslaan); daarvoor is propBroken niet nodig.
public static partial class PlanetCity
{
    public const byte PropPaal = 1, PropBank = 2, PropStruik = 3, PropBerk = 4, PropBoom = 5, PropDen = 6, PropGebouw = 10;
    public struct PropPart { public MeshFilter mf; public int v0, v1, sub; }   // sub: submesh (module), -1 de enige
    public class Prop
    {
        public Vector3 at, up; public float r, h, crownR, crownY; public byte kind; public int cell = -1; public long id; public bool gone;
        public GameObject owner; public readonly List<PropPart> parts = new List<PropPart>();
    }
    class OpenProp { public Prop p; public readonly List<(string key, MeshBuilder b, int n)> start = new List<(string, MeshBuilder, int)>(); }

    static Dictionary<string, MeshBuilder> propMb;   // het tekenwerk dat nu loopt; null: niets bijhouden (bouwvoorbeeld)
    static int propCell = -1;   // het vak dat BuildChunk nu tekent (-1: onbekend, dan zoekt PropsCommit het op)
    static readonly List<OpenProp> propStack = new List<OpenProp>();
    static readonly List<(Prop p, List<(string key, int v0, int v1)> r)> propPending = new List<(Prop, List<(string, int, int)>)>();
    static readonly HashSet<long> propBroken = new HashSet<long>();
    static readonly Dictionary<int, List<Prop>> propCells = new Dictionary<int, List<Prop>>();
    static readonly Dictionary<GameObject, List<Prop>> propOwners = new Dictionary<GameObject, List<Prop>>();
    public static int PropCount { get { int n = 0; foreach (var l in propOwners.Values) n += l.Count; return n; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetProps() { propMb = null; propCell = -1; propStack.Clear(); propPending.Clear(); propBroken.Clear(); propCells.Clear(); propOwners.Clear(); hidden.Clear(); }

    // een vaste sleutel voor de plek (de richting op de bol, op 25 cm), zodat hetzelfde ding na opnieuw tekenen herkend wordt
    static long PropId(byte kind, Vector3 at)
    {
        Vector3 d = at.normalized * Planet.R * 4f;
        long x = Mathf.RoundToInt(d.x) & 0xFFFFF, y = Mathf.RoundToInt(d.y) & 0xFFFFF, z = Mathf.RoundToInt(d.z) & 0xFFFFF;
        return (((x << 20) | y) << 20 | z) ^ ((long)kind << 60);
    }

    // begin van één ding; false: het is al stuk, niet tekenen (dan ook geen PropEnd)
    static bool PropBegin(byte kind, Vector3 at, Vector3 up, float r, float h, float crownR = 0f, float crownY = 0f)
    {
        if (kind != PropGebouw && propBroken.Count > 0 && propBroken.Contains(PropId(kind, at))) return false;
        var o = new OpenProp();
        if (propMb != null)
        {
            o.p = new Prop { at = at, up = up.normalized, r = r, h = h, crownR = crownR, crownY = crownY, kind = kind, cell = propCell };
            foreach (var kv in propMb) o.start.Add((kv.Key, kv.Value, kv.Value.v.Count));
        }
        propStack.Add(o); return true;
    }
    static void PropEnd()
    {
        if (propStack.Count == 0) return;
        var o = propStack[propStack.Count - 1]; propStack.RemoveAt(propStack.Count - 1);
        if (o.p == null || propMb == null) return;
        var ranges = new List<(string, int, int)>();
        foreach (var kv in propMb)
        {
            int n0 = 0; foreach (var s in o.start) if (s.b == kv.Value) { n0 = s.n; break; }
            if (kv.Value.v.Count > n0) ranges.Add((kv.Key, n0, kv.Value.v.Count));
        }
        if (ranges.Count > 0) propPending.Add((o.p, ranges));
    }

    // het tekenen van een deel of stuk weg begint (mb) en is klaar (owner met kinderen per sleutel, of null als er niets kwam)
    static void PropsStart(Dictionary<string, MeshBuilder> mb) { propMb = mb; propStack.Clear(); propPending.Clear(); propCell = -1; }
    static void PropsCommit(GameObject owner)
    {
        var mb = propMb; propMb = null; propStack.Clear(); propCell = -1;
        if (owner == null || propPending.Count == 0) { propPending.Clear(); return; }
        var mfs = new Dictionary<string, MeshFilter>();
        foreach (Transform ch in owner.transform) { var mf = ch.GetComponent<MeshFilter>(); if (mf != null) mfs[ch.name] = mf; }
        var list = new List<Prop>();
        foreach (var pr in propPending)
        {
            var p = pr.p; p.owner = owner;
            foreach (var r in pr.r) if (mfs.TryGetValue(r.key, out var mf)) p.parts.Add(new PropPart { mf = mf, v0 = r.v0, v1 = r.v1, sub = -1 });
            if (p.parts.Count == 0) continue;
            if (p.cell < 0) p.cell = Planet.Nearest(p.at);
            p.id = PropId(p.kind, p.at);
            if (!propCells.TryGetValue(p.cell, out var cl)) propCells[p.cell] = cl = new List<Prop>();
            cl.Add(p); list.Add(p);
        }
        propPending.Clear();
        if (list.Count > 0) propOwners[owner] = list;
    }
    // een deel of stuk weg wordt weggegooid: zijn dingen vergeten
    static void PropsForget(GameObject owner)
    {
        if (owner == null || !propOwners.TryGetValue(owner, out var list)) return;
        propOwners.Remove(owner);
        foreach (Transform ch in owner.transform) { var mf = ch.GetComponent<MeshFilter>(); if (mf != null && mf.sharedMesh != null) hidden.Remove(mf.sharedMesh); }
        foreach (var p in list) if (propCells.TryGetValue(p.cell, out var cl)) { cl.Remove(p); if (cl.Count == 0) propCells.Remove(p.cell); }
    }

    // alles wat nog heel is op vak c en zijn buren
    public static void PropsNear(int c, List<Prop> into)
    {
        into.Clear(); if (c < 0 || Planet.Cells == null) return;
        Near(c, into); foreach (int j in Planet.Cells[c].nb) Near(j, into);
    }
    static void Near(int c, List<Prop> into)
    {
        if (!propCells.TryGetValue(c, out var cl)) return;
        for (int k = cl.Count - 1; k >= 0; k--) { var p = cl[k]; if (p.owner == null) { cl.RemoveAt(k); continue; } if (!p.gone) into.Add(p); }
    }
    // de gebouwen (als prop) van vak c, voor het instorten
    public static void BuildingProps(int c, List<Prop> into)
    {
        into.Clear(); if (!propCells.TryGetValue(c, out var cl)) return;
        foreach (var p in cl) if (p.kind == PropGebouw && !p.gone && p.owner != null && p.cell == c) into.Add(p);
    }

    static readonly List<Vector3> bv = new List<Vector3>(), bn = new List<Vector3>();
    static readonly Dictionary<Mesh, int> hidden = new Dictionary<Mesh, int>();   // per grote mesh: hoeveel hoekpunten er al weg zijn
    // het ding uit de grote mesh halen en als los object teruggeven (draaipunt op de voet, y langs up). null als er niets meer was.
    public static Transform Break(Prop p, bool remember = true)
    {
        if (p == null || p.gone) return null; p.gone = true;
        if (remember && p.kind != PropGebouw) propBroken.Add(p.id);
        var rootT = new GameObject(p.kind == PropGebouw ? "Instortend gebouw" : "Omver").transform;
        Vector3 f = Vector3.Cross(p.up, Mathf.Abs(Vector3.Dot(p.up, Vector3.up)) < 0.9f ? Vector3.up : Vector3.right).normalized;
        rootT.SetPositionAndRotation(p.at, Quaternion.LookRotation(f, p.up));
        var touched = new HashSet<MeshFilter>(); bool any = false;
        var byMat = new Dictionary<Material, (List<Vector3> v, List<Vector3> n)>();
        foreach (var part in p.parts)
        {
            var mf = part.mf; if (mf == null || mf.sharedMesh == null) continue;
            var mesh = mf.sharedMesh; mesh.GetVertices(bv); mesh.GetNormals(bn);
            int v1 = Mathf.Min(part.v1, bv.Count); if (part.v0 >= v1) continue;
            var mr = mf.GetComponent<MeshRenderer>(); var mat = mr == null ? null : part.sub >= 0 ? (part.sub < mr.sharedMaterials.Length ? mr.sharedMaterials[part.sub] : null) : mr.sharedMaterial; if (mat == null) continue;
            if (!byMat.TryGetValue(mat, out var dst)) byMat[mat] = dst = (new List<Vector3>(), new List<Vector3>());
            var tr = mf.transform; Vector3 hide = bv[part.v0];
            for (int k = part.v0; k < v1; k++)
            {
                dst.v.Add(rootT.InverseTransformPoint(tr.TransformPoint(bv[k])));
                dst.n.Add(rootT.InverseTransformDirection(tr.TransformDirection(k < bn.Count ? bn[k] : Vector3.up)));
                bv[k] = hide;   // in de grote mesh: alle driehoeken van het ding op één punt (niet meer te zien)
            }
            mesh.SetVertices(bv); touched.Add(mf); any = true;
            hidden.TryGetValue(mesh, out int hn); hidden[mesh] = hn + (v1 - part.v0);
        }
        // de botsvorm volgt; is er van de mesh niets meer over, dan de botsvorm uit (een lege mesh kan PhysX niet maken)
        foreach (var mf in touched)
        {
            var mc = mf.GetComponent<MeshCollider>(); if (mc == null) continue;
            bool empty = hidden.TryGetValue(mf.sharedMesh, out int hn) && hn >= mf.sharedMesh.vertexCount;
            if (empty) { mc.enabled = false; continue; }
            mc.sharedMesh = null; mc.sharedMesh = mf.sharedMesh;
        }
        if (!any) { Object.Destroy(rootT.gameObject); return null; }
        foreach (var kv in byMat)
        {
            var m = new Mesh { indexFormat = kv.Value.v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            var tri = new int[kv.Value.v.Count - kv.Value.v.Count % 3]; for (int k = 0; k < tri.Length; k++) tri[k] = k;
            m.SetVertices(kv.Value.v); m.SetNormals(kv.Value.n); m.SetTriangles(tri, 0); m.RecalculateBounds();
            var g = new GameObject(kv.Key.name); g.transform.SetParent(rootT, false);
            g.AddComponent<MeshFilter>().sharedMesh = m; g.AddComponent<MeshRenderer>().sharedMaterial = kv.Key;
        }
        return rootT;
    }

    // ---------- gebouwen ----------
    // wat de tank kan inrijden: in code getekende gebouwen (niet een plein, pad, park of heuvel)
    public static bool IsBuildingBlock(string t) => t == "woning" || t == "kantoor" || t == "winkel" || t == "toren" || t == "molen" || t == "blok" || t == "dak";
    public static bool HasBuilding(int i) => WreckClass(i) == 2;
    // wat er op vak i te slopen valt: 0 niets, 1 een hek of haag (auto en tank), 2 een gebouw (alleen de tank)
    public static int WreckClass(int i)
    {
        if (!Cells.TryGetValue(i, out var l)) return 0;
        int w = 0;
        foreach (var t in l) if (IsBuildingBlock(t)) return 2;
        if (HasModule(i)) foreach (var I in ModulesAt(i)) w = Mathf.Max(w, ModWreck(I.M));
        return w;
    }
    static int ModWreck(ModuleV2 m)
    {
        if (m == null) return 0;
        bool fence = false, solid = false;
        foreach (var L in m.lv) foreach (var wl in L.w.Values) { if (wl.m == "fence" || wl.m == "hedge") fence = true; else solid = true; }
        if (solid || m.lv.Count > 1) return 2;
        return fence ? 1 : 0;
    }
    // het module-object van een exemplaar overnemen (het wordt niet meer door SyncModules weggegooid); de botsvorm gaat er meteen af
    public static GameObject TakeModuleObject(ModInst I)
    {
        if (!modGos.TryGetValue(I.Key, out var g) || g == null) return null;
        modGos.Remove(I.Key);
        if (g.poles != null) Object.Destroy(g.poles);
        if (g.go == null) return null;
        PropsForget(g.go);
        var mc = g.go.GetComponent<MeshCollider>(); if (mc != null) Object.Destroy(mc);
        return g.go;
    }

    // ---------- voorwerpen in modules (ModuleMesh.ObjRec) ----------
    static byte ModObjKind(string t)
    {
        switch (t)
        {
            case "tree": return PropBoom;
            case "bush": case "planter": return PropStruik;
            case "lamp": case "sign": return PropPaal;
            case "bench": case "table": case "bin": case "bike": case "charger": case "grave": case "slide": case "swing": return PropBank;
            default: return 0;
        }
    }
    static readonly List<Vector3> modV = new List<Vector3>();
    // de voorwerpen van een module-exemplaar in het register; wat al eerder is omgereden meteen weer weg
    static void ModuleProps(GameObject go, ModuleMesh.Built b, int c)
    {
        if (b == null || b.objs == null || b.objs.Count == 0) return;
        var mf = go.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) return;
        mf.sharedMesh.GetVertices(modV); Vector3 up = Planet.Cells[c].dir; var tr = go.transform;
        var list = new List<Prop>(); List<Prop> again = null;
        foreach (var o in b.objs)
        {
            byte kind = ModObjKind(o.t); if (kind == 0) continue;
            Vector3 sum = Vector3.zero; int n = 0; float lo = 1e9f;
            foreach (var (sub, v0, v1) in o.r) for (int k = v0; k < v1 && k < modV.Count; k++) { Vector3 w = tr.TransformPoint(modV[k]); sum += w; n++; lo = Mathf.Min(lo, w.magnitude); }
            if (n == 0) continue;
            Vector3 cen = sum / n, at = cen.normalized * lo; float hi = 0f;
            foreach (var (sub, v0, v1) in o.r) for (int k = v0; k < v1 && k < modV.Count; k++) hi = Mathf.Max(hi, tr.TransformPoint(modV[k]).magnitude - lo);
            var p = new Prop { at = at, up = at.normalized, kind = kind, h = Mathf.Max(0.5f, hi), owner = go, cell = Planet.Nearest(at) };
            p.r = kind == PropBoom ? 0.3f : kind == PropStruik ? 0.9f : kind == PropPaal ? 0.14f : 0.6f;
            if (kind == PropBoom) { p.crownR = 2f; p.crownY = 2.3f; }
            foreach (var (sub, v0, v1) in o.r) p.parts.Add(new PropPart { mf = mf, v0 = v0, v1 = v1, sub = sub });
            p.id = PropId(kind, at);
            if (!propCells.TryGetValue(p.cell, out var cl)) propCells[p.cell] = cl = new List<Prop>();
            cl.Add(p); list.Add(p);
            if (propBroken.Count > 0 && propBroken.Contains(p.id)) (again ??= new List<Prop>()).Add(p);
        }
        if (list.Count > 0) propOwners[go] = list;
        if (again != null) foreach (var p in again) { var t = Break(p, false); if (t != null) { foreach (var f in t.GetComponentsInChildren<MeshFilter>()) Object.Destroy(f.sharedMesh); Object.Destroy(t.gameObject); } }
    }
}
