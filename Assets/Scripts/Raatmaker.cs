using System.Collections.Generic;
using UnityEngine;

// De Raatmaker: de module-editor in het spel (zoals 08d-module-maker.js van de webversie). Hij tekent in de studio (ModuleStudio,
// ver van de planeet, altijd helder om 13:00) met een eigen camera: 2D recht van boven of 3D om de module heen. Werkbalk links in
// groepen (kiezen · vloer · muren en openingen · trappen · dak en voorwerpen · gum), keuzes van het gereedschap rechts, verdiepingen
// linksonder. Vóór het klikken staat er al een voorbeeld van wat er komt (amber); wat de gum weghaalt licht rood op.
// Bewaren schrijft JSON in persistentDataPath/modules; een module uit de stad bewerken past al zijn exemplaren aan.
public partial class Raatmaker : MonoBehaviour
{
    public static bool IsOpen => I != null && I.open;
    static Raatmaker I;
    bool open; PlanetBuilder host; ModuleV2 m; string origId, savedId; bool dirty, meshDirty, confirmClose;
    int lv; string tool = "floor", fmat = "tile", wmat = "brick", obj = "bench", rkind = "hip", brush = "hex";
    float orot, sw = 1.95f, rampW = 1.2f; int ssd = 1, usd = 1, wsd = 1;
    bool v3d = true, upto; float yaw = 35f, pitch = 52f, dist = 42f, ortho = 16f; Vector3 target; Vector2 pan2;
    readonly List<string> undoS = new List<string>(), redoS = new List<string>();
    Transform root; Camera cam; MeshFilter modF, plateF, gridF, prevF, hiF; MeshRenderer modR, prevR, hiR; Mesh gridMesh;
    string msg; float msgT; bool ctrlHeld; int changes;
    bool hasHv; Vector2 hv; string dragKind; Vector2Int dragA; int dragD, dragN, dragObj = -1; Vector2 dragFrom; bool dragMoved, dragSnapped;
    bool selOn; Pick sel; Vector2 rAt; int camDrag; Vector2 camFrom; string prevKey;

    public static void Show(ModuleV2 src, PlanetBuilder host)
    {
        if (I == null) { var go = new GameObject("Raatmaker"); I = go.AddComponent<Raatmaker>(); }
        I.Open(src, host);
    }
    void Open(ModuleV2 src, PlanetBuilder h)
    {
        host = h; m = src != null ? (src.mir ? ModuleLib.Get(src.id) ?? src : src).Copy() : new ModuleV2 { id = "", name = "Nieuwe module", cat = "Overig" };
        m.mir = false; origId = src != null ? src.id : null; savedId = null; lv = 0; dirty = false; undoS.Clear(); redoS.Clear(); selOn = false; dragKind = null; confirmClose = false;
        tool = src == null ? "floor" : "select";
        EnsureStudio(); root.gameObject.SetActive(true); cam.enabled = true; if (host != null && host.Cam != null) host.Cam.enabled = false;
        open = true; meshDirty = true; Fit(); Say(src == null ? "Nieuwe module: kies links een gereedschap en teken op het raster" : "Bewerken: " + m.name + " · bewaren past alle exemplaren in de stad aan");
    }
    void Close()
    {
        open = false; confirmClose = false; if (cam != null) cam.enabled = false; if (root != null) root.gameObject.SetActive(false);
        if (host != null && host.Cam != null) host.Cam.enabled = true;
        GUIUtility.keyboardControl = 0;
        if (host != null && savedId != null) host.ModPick(savedId);
    }
    void Say(string s) { msg = s; msgT = Time.unscaledTime + 3.5f; }

    // ---------- studio ----------
    void EnsureStudio()
    {
        if (root != null) return;
        root = new GameObject("Raatmakerstudio").transform; root.position = ModuleStudio.Pos;
        MeshFilter Part(string n, out MeshRenderer r, bool shadow) { var g = new GameObject(n); g.transform.SetParent(root, false); var f = g.AddComponent<MeshFilter>(); r = g.AddComponent<MeshRenderer>(); if (!shadow) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; } return f; }
        modF = Part("module", out modR, true);
        plateF = Part("plaat", out var pr, true); pr.sharedMaterial = Mats.Get("mod_plaat", MV2.Hex("#c8dca4")); plateF.transform.localPosition = Vector3.down * 0.02f;
        gridF = Part("raster", out var gr, false); gr.sharedMaterials = new[] { Raatstad.MakeMat(new Color(1f, 1f, 1f, 0.32f), true), Raatstad.MakeMat(new Color(0.95f, 0.7f, 0.2f, 0.95f), true) };
        prevF = Part("voorbeeld", out prevR, false);
        hiF = Part("oplichten", out hiR, false);
        var cg = new GameObject("Raatmakercamera"); cg.transform.SetParent(root, false); cam = cg.AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = MV2.Hex("#b8cfe0"); cam.nearClipPlane = 0.2f; cam.farClipPlane = 600f; cam.depth = 50f; cam.enabled = false; ModuleStudio.Register(cam);
    }
    static Material ghostA, ghostR, hiA, hiRed;
    static Material Ghost(bool red) { if (ghostA == null) { ghostA = Raatstad.MakeMat(new Color(0.949f, 0.698f, 0.2f, 0.5f), true); ghostR = Raatstad.MakeMat(new Color(0.9f, 0.25f, 0.2f, 0.6f), true); hiA = Raatstad.MakeMat(new Color(0.949f, 0.698f, 0.2f, 0.4f), true); hiRed = Raatstad.MakeMat(new Color(0.9f, 0.25f, 0.2f, 0.5f), true); } return red ? ghostR : ghostA; }
    void Rebuild()
    {
        meshDirty = false; var mm = m;
        if (!v3d || upto) { mm = m.Copy(); while (mm.lv.Count > lv + 1) mm.lv.RemoveAt(mm.lv.Count - 1); }
        var b = ModuleMesh.Build(mm); var old = modF.sharedMesh; modF.sharedMesh = b.mesh; modR.sharedMaterials = b.mats; if (old != null) Destroy(old);
        plateF.sharedMesh = ModuleStudio.Plate(m.k);
        // het raster van deze verdieping, met de rand van het gebied in amber
        var v = new List<Vector3>(); var a = new List<int>(); var e = new List<int>(); var seen = new HashSet<(Vector2Int, Vector2Int)>(); float t = Ts, y = lv * MV2.LV_H + 0.05f, ext = MV2.R * (1 + 2 * m.k) + t;
        int iM = Mathf.CeilToInt(ext / (t * SQ3 / 2f)) + 1, jM = Mathf.CeilToInt(ext / t) + iM;
        Vector2Int Qz(Vector2 p) => new Vector2Int(Mathf.RoundToInt(p.x * 100f), Mathf.RoundToInt(p.y * 100f));
        void Line(List<int> l, Vector2 p, Vector2 q) { int o = v.Count; v.Add(new Vector3(p.x, y, -p.y)); v.Add(new Vector3(q.x, y, -q.y)); l.Add(o); l.Add(o + 1); }
        for (int i = -iM; i <= iM; i++) for (int j = -jM; j <= jM; j++) for (int tt = 0; tt < 2; tt++)
        {
            var id = (i, j, tt); if (!OkTri(id)) continue; var tv = Tri(id);
            for (int k = 0; k < 3; k++) { Vector2 p = tv[k], q = tv[(k + 1) % 3]; var key = Qz(p).x < Qz(q).x || (Qz(p).x == Qz(q).x && Qz(p).y < Qz(q).y) ? (Qz(p), Qz(q)) : (Qz(q), Qz(p)); if (seen.Add(key)) Line(a, p, q); }
        }
        foreach (var (q, r, s) in MV2.Boundary(m.k)) { var c = MV2.CellXZ(q, r); float a0 = (30f - 60f * s) * Mathf.Deg2Rad, a1 = (-30f - 60f * s) * Mathf.Deg2Rad; Line(e, c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * MV2.R, c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * MV2.R); }
        if (gridMesh == null) gridMesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; gridMesh.Clear(); gridMesh.SetVertices(v); gridMesh.subMeshCount = 2;
        gridMesh.SetIndices(a, MeshTopology.Lines, 0); gridMesh.SetIndices(e, MeshTopology.Lines, 1); gridMesh.RecalculateBounds(); gridF.sharedMesh = gridMesh;
    }
    void Fit() { float ext = MV2.R * (1 + 2 * m.k); dist = ext * 2.7f + m.lv.Count * 2f; ortho = ext * 1.25f; pan2 = Vector2.zero; target = new Vector3(0f, Mathf.Min(lv, 3) * 1.5f, 0f); }

    // ---------- wijzigen, ongedaan maken ----------
    void Snap() { undoS.Add(m.ToJson()); if (undoS.Count > 80) undoS.RemoveAt(0); redoS.Clear(); }
    void Changed() { dirty = true; meshDirty = true; changes++; prevKey = null; }
    void Load(string json) { string id = m.id; m = ModuleV2.FromJson(json); m.id = id; lv = Mathf.Min(lv, m.lv.Count - 1); selOn = false; Changed(); }
    void Undo() { if (undoS.Count == 0) return; redoS.Add(m.ToJson()); Load(undoS[undoS.Count - 1]); undoS.RemoveAt(undoS.Count - 1); Say("Ongedaan gemaakt"); }
    void Redo() { if (redoS.Count == 0) return; undoS.Add(m.ToJson()); Load(redoS[redoS.Count - 1]); redoS.RemoveAt(redoS.Count - 1); Say("Opnieuw"); }
    void SetLevel(int n)
    {
        n = Mathf.Clamp(n, 0, MV2.MAX_LV - 1); if (n == lv) return;
        if (n > lv) { while (m.lv.Count <= n) m.lv.Add(new ModLevel()); }
        else { int top = m.lv.Count - 1; if (top > n && top == lv && m.lv[top].Empty && top > 0) m.lv.RemoveAt(top); }
        lv = n; selOn = false; meshDirty = true; prevKey = null; target.y = Mathf.Min(lv, 3) * 1.5f;
    }
    void CopyUp()
    {
        if (lv >= MV2.MAX_LV - 1) return; Snap(); while (m.lv.Count <= lv + 1) m.lv.Add(new ModLevel()); var L = Lvl; var U = m.lv[lv + 1];
        foreach (var kv in L.f) if (!U.f.ContainsKey(kv.Key)) U.f[kv.Key] = kv.Value; foreach (var kv in L.w) if (!U.w.ContainsKey(kv.Key)) U.w[kv.Key] = kv.Value.Copy();
        lv++; Changed(); Say("Verdieping gekopieerd naar boven");
    }
    void Save(bool asCopy)
    {
        if (string.IsNullOrWhiteSpace(m.name)) m.name = "Nieuwe module";
        m.id = asCopy || string.IsNullOrEmpty(origId) ? ModuleLib.NewId(m.name) : origId;
        // een lege bovenste verdieping hoort er niet bij
        while (m.lv.Count > 1 && m.lv[m.lv.Count - 1].Empty) m.lv.RemoveAt(m.lv.Count - 1); lv = Mathf.Min(lv, m.lv.Count - 1);
        ModuleLib.Save(m); origId = savedId = m.id; dirty = false; PlanetCity.SyncModules(); meshDirty = true;
        int n = 0; foreach (var kv in PlanetCity.Cells) foreach (var s in kv.Value) if (s.StartsWith("m|" + m.id + "|")) n++;
        Say("Bewaard: " + m.name + (n > 0 ? " · " + n + (n == 1 ? " exemplaar" : " exemplaren") + " in de stad bijgewerkt" : ""));
    }

    // ---------- invoer en camera ----------
    void Update()
    {
        if (!open) return;
        bool overUI = Ui.MouseOverUI || confirmClose; ctrlHeld = Inp.Held(K.Ctrl);
        // camera: wiel zoomt; middelste knop of Alt slepen draait (3D); rechts of Shift slepen schuift
        float sc = Inp.Scroll; if (sc != 0f && !overUI) { if (v3d) dist = Mathf.Clamp(dist * (1f - sc * 0.12f), 5f, 220f); else ortho = Mathf.Clamp(ortho * (1f - sc * 0.12f), 3f, 90f); }
        bool alt = Inp.Held(K.Alt), shift = Inp.Held(K.Shift);
        if (!overUI && camDrag == 0)
        {
            if (Inp.MiddleDown || (Inp.LeftDown && alt)) { camDrag = v3d ? 2 : 1; camFrom = Inp.Pos; }
            else if (Inp.LeftDown && shift && tool != "object") { camDrag = 1; camFrom = Inp.Pos; }
            else if (Inp.RightDown) { camDrag = 3; camFrom = rAt = Inp.Pos; }
        }
        if (camDrag != 0)
        {
            Vector2 d = Inp.Pos - camFrom; camFrom = Inp.Pos; bool held = camDrag == 3 ? Inp.RightHeld : Inp.MiddleHeld || Inp.LeftHeld;
            if (camDrag == 2) { yaw += d.x * 0.3f; pitch = Mathf.Clamp(pitch - d.y * 0.3f, 8f, 89f); }
            else if (camDrag == 1 || (camDrag == 3 && (Inp.Pos - rAt).magnitude > 5f)) Pan(d);
            if (!held) { if (camDrag == 3 && (Inp.Pos - rAt).magnitude <= 5f && hasHv) RightClick(hv); camDrag = 0; }
        }
        // de muis op het vlak van deze verdieping
        hasHv = false;
        if (!overUI || dragKind != null)
        {
            var ray = cam.ScreenPointToRay(Inp.Pos); float y = root.position.y + lv * MV2.LV_H;
            if (Mathf.Abs(ray.direction.y) > 1e-4f) { float t = (y - ray.origin.y) / ray.direction.y; if (t > 0f) { var w = ray.origin + ray.direction * t - root.position; hv = new Vector2(w.x, -w.z); hasHv = true; } }
        }
        if (camDrag == 0)
        {
            if (Inp.LeftDown && !overUI && hasHv) { GUIUtility.keyboardControl = 0; LeftDown(hv); }
            else if (dragKind != null && Inp.LeftHeld && hasHv) LeftDrag(hv);
            if (dragKind != null && !Inp.LeftHeld) LeftUp(hasHv ? hv : dragFrom);
        }
        if (meshDirty) Rebuild();
        Preview();
        // camera
        if (v3d)
        {
            cam.orthographic = false; cam.fieldOfView = 50f; var rot = Quaternion.Euler(pitch, yaw, 0f);
            cam.transform.SetPositionAndRotation(root.position + target + rot * new Vector3(0f, 0f, -dist), rot);
        }
        else { cam.orthographic = true; cam.orthographicSize = ortho; cam.transform.SetPositionAndRotation(root.position + new Vector3(pan2.x, lv * MV2.LV_H + 80f, pan2.y), Quaternion.Euler(90f, 0f, 0f)); }
    }
    void Pan(Vector2 d)
    {
        if (v3d) { Vector3 r = cam.transform.right, f = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized; target -= (r * d.x + f * d.y) * dist * 0.0016f; }
        else { float k = 2f * ortho / Screen.height; pan2 -= d * k; }
    }

    // ---------- gereedschappen ----------
    static readonly string[] StairTools = { "stair", "ustair", "ramp", "spiral" };
    void LeftDown(Vector2 p)
    {
        dragFrom = p; dragMoved = false; dragSnapped = false;
        switch (tool)
        {
            case "select":
                if (PickAt(p, out var pk)) { sel = pk; selOn = true; if (pk.kind == "o") { dragKind = "move"; dragObj = pk.idx; } } else selOn = false;
                prevKey = null; break;
            case "floor": Snap(); Paint(p, false); dragKind = "floor"; break;
            case "fill": { var reg = FillRegion(p); if (reg.Count == 0) { Say("Klik binnen het gebied"); break; } Snap(); foreach (var t in reg) Lvl.f[t] = fmat; Changed(); Say(reg.Count + " driehoeken gevuld"); break; }
            case "wall": dragKind = "wall"; dragA = VertexAt(p); dragN = 0; break;
            case "room": { var ws = RoomWalls(p); int n = 0; if (ws.Count > 0) Snap(); foreach (var k in ws) { var (a, b) = Seg(k); if (!Lvl.w.ContainsKey(k) && MV2.NearArea(m, (a.x + b.x) / 2f, (a.y + b.y) / 2f)) { Lvl.w[k] = new ModWall { m = wmat }; n++; } } if (n > 0) Changed(); Say(n > 0 ? n + " muurdelen" : ws.Count > 0 ? "Hier staan de muren al" : "Klik binnen het gebied"); break; }
            case "door": case "window": case "gate": case "open": case "close":
                if (NearestWall(p, 0.7f, out var wk)) { Snap(); Lvl.w[wk].o = tool == "close" ? "" : tool; Changed(); Say(OpeningName(tool == "close" ? "" : tool) + (tool == "close" ? ": de opening is dicht" : " gemaakt")); }
                else Say("Klik op een muur"); break;
            case "stair": case "ustair": case "ramp": case "spiral": { var S0 = HoverStair(p); dragKind = "stair"; dragA = new Vector2Int(S0.ai, S0.aj); dragD = S0.d; dragN = (int)S0.n; break; }
            case "hexramp": if (!InArea(p)) break; Snap(); Lvl.s.Add(HexRampAt(p)); Changed(); Say("Schans geplaatst: het hele vak loopt 3 m op"); break;
            case "roof":
                {
                    if (!InArea(p)) break; var c = MV2.CellOf(p.x, p.y); Snap();
                    if (Lvl.r.TryGetValue((c.x, c.y), out var rk) && rk == rkind) { Lvl.r.Remove((c.x, c.y)); Say("Dak weggehaald"); } else { Lvl.r[(c.x, c.y)] = rkind; Say(RoofNames[System.Array.IndexOf(RoofKinds, rkind)] + " op dit vak"); }
                    Changed(); break;
                }
            case "object":
                {
                    int oi = ObjectAt(p, 0.8f);
                    if (oi >= 0) { Snap(); Lvl.o[oi].rot = Mathf.Repeat(Lvl.o[oi].rot + (Inp.Held(K.Shift) ? -1f : 1f) * Mathf.PI / 6f, Mathf.PI * 2f); Changed(); Say("Gedraaid: " + Mathf.RoundToInt(Lvl.o[oi].rot * Mathf.Rad2Deg) + "°"); break; }
                    if (!InArea(p)) { Say("Klik binnen het gebied"); break; }
                    Snap(); Lvl.o.Add(ObjPose(obj, p)); Changed(); break;
                }
            case "erase": Snap(); EraseAt(p); dragKind = "erase"; break;
        }
    }
    void LeftDrag(Vector2 p)
    {
        if ((p - dragFrom).magnitude > 0.4f) dragMoved = true;
        switch (dragKind)
        {
            case "floor": Paint(p, false); break;
            case "erase": EraseAt(p); break;
            case "wall": { var (d, n) = DirSteps(dragA, p, true); dragD = d; dragN = (p - P(dragA)).magnitude < Ts * 0.4f ? 0 : n; break; }
            case "stair": if (dragMoved) { var (d, n) = DirSteps(dragA, p, false); dragD = d; dragN = tool == "spiral" ? 1 : Mathf.Clamp(n, 1, 8); } break;
            case "move":
                if (!dragMoved || dragObj < 0 || dragObj >= Lvl.o.Count) break; if (!dragSnapped) { Snap(); dragSnapped = true; }
                Lvl.o[dragObj].x = Mathf.Round(p.x * 20f) / 20f; Lvl.o[dragObj].z = Mathf.Round(p.y * 20f) / 20f; Changed(); break;
        }
    }
    void LeftUp(Vector2 p)
    {
        if (dragKind == "wall" && dragN > 0)
        {
            Snap(); int n = 0; foreach (var k in SegsBetween(dragA, dragD, dragN)) { var (a, b) = Seg(k); if (!MV2.NearArea(m, (a.x + b.x) / 2f, (a.y + b.y) / 2f)) continue; Lvl.w[k] = new ModWall { m = wmat }; n++; }
            Changed(); Say(n + " muurdelen · " + (n * Ts * (dragD < 6 ? 1f : SQ3)).ToString("0.##") + " m");
        }
        if (dragKind == "stair")
        {
            Snap(); var S = NewStair(dragA, dragD, dragMoved ? dragN : (tool == "spiral" ? 1 : 3)); Lvl.s.Add(S); Changed();
            Say(StairName(S.k).Substring(0, 1).ToUpper() + StairName(S.k).Substring(1) + " geplaatst" + (lv + 1 >= m.lv.Count ? " · ga een verdieping omhoog (PgUp) voor de vloer erboven" : ""));
        }
        dragKind = null; dragObj = -1;
    }
    void RightClick(Vector2 p)
    {
        if (tool == "floor") { Snap(); Paint(p, true); return; }
        if (tool == "fill") { var reg = FillRegion(p); if (reg.Count == 0 || !Lvl.f.ContainsKey(reg[0])) return; Snap(); foreach (var t in reg) Lvl.f.Remove(t); Changed(); Say(reg.Count + " driehoeken leeggemaakt"); return; }
        Snap(); if (!EraseAt(p)) undoS.RemoveAt(undoS.Count - 1);
    }
    void Paint(Vector2 p, bool erase)
    {
        bool any = false; foreach (var t in BrushTris(p)) { if (erase) any |= Lvl.f.Remove(t); else if (!Lvl.f.TryGetValue(t, out var cur) || cur != fmat) { Lvl.f[t] = fmat; any = true; } }
        if (any) Changed();
    }
    bool EraseAt(Vector2 p) { if (!PickAt(p, out var pk)) return false; RemovePick(pk); selOn = false; Changed(); Say(char.ToUpper(pk.Name[0]) + pk.Name.Substring(1) + " weggehaald"); return true; }

    // ---------- voorbeeld vóór het klikken ----------
    ModuleV2 Mini(System.Action<ModLevel> fill, bool withAbove = false)
    {
        var mm = new ModuleV2 { id = "__voorbeeld", k = m.k, n = m.n, lv = new List<ModLevel>() };
        for (int i = 0; i <= lv; i++) mm.lv.Add(new ModLevel());
        if (withAbove) { var up = new ModLevel(); if (lv + 1 < m.lv.Count) up.f = new Dictionary<(int, int, int), string>(m.lv[lv + 1].f); mm.lv.Add(up); }
        fill(mm.lv[lv]); return mm;
    }
    void ShowGhost(ModuleV2 mm, bool red, int parts = ModuleMesh.All)
    {
        var old = prevF.sharedMesh; if (old != null) Destroy(old);
        if (mm == null) { prevF.sharedMesh = null; prevR.enabled = false; return; }
        var b = ModuleMesh.Build(mm, false, 0, parts); prevF.sharedMesh = b.mesh; var mats = new Material[b.mesh.subMeshCount]; for (int i = 0; i < mats.Length; i++) mats[i] = Ghost(red); prevR.sharedMaterials = mats; prevR.enabled = true;
        prevF.transform.localPosition = Vector3.up * 0.015f;
    }
    void ShowTris(List<(int, int, int)> tris, bool red, List<Vector2> poly = null)
    {
        var old = hiF.sharedMesh; if (old != null) Destroy(old);
        if ((tris == null || tris.Count == 0) && poly == null) { hiF.sharedMesh = null; hiR.enabled = false; return; }
        var mb = new MeshBuilder(); float y = lv * MV2.LV_H + (lv > 0 ? 0.06f : 0.09f);
        if (tris != null) foreach (var t in tris) { var v = Tri(t); mb.Tri(new Vector3(v[0].x, y, -v[0].y), new Vector3(v[1].x, y, -v[1].y), new Vector3(v[2].x, y, -v[2].y), Vector3.up); }
        if (poly != null) for (int i = 1; i + 1 < poly.Count; i++) mb.Tri(new Vector3(poly[0].x, y, -poly[0].y), new Vector3(poly[i].x, y, -poly[i].y), new Vector3(poly[i + 1].x, y, -poly[i + 1].y), Vector3.up);
        Ghost(false); hiF.sharedMesh = mb.Build(); hiR.sharedMaterial = red ? hiRed : hiA; hiR.enabled = true;
    }
    // een rasterpunt als klein zeshoekje
    List<Vector2> Dot(Vector2 c, float r) { var o = new List<Vector2>(); for (int i = 0; i < 6; i++) { float a = i * Mathf.PI / 3f; o.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r); } return o; }
    void Preview()
    {
        string key = tool + "|" + lv + "|" + changes + "|" + dragKind + "|" + dragA + "|" + dragD + "|" + dragN + "|" + selOn + "|" + sel.kind + sel.idx + sel.key + sel.cell + "|" + fmat + wmat + obj + rkind + brush + orot + sw + rampW + ssd + usd + wsd + ctrlHeld + "|" + (hasHv ? Mathf.RoundToInt(hv.x * 8f) + "," + Mathf.RoundToInt(hv.y * 8f) : "-");
        if (key == prevKey) return; prevKey = key;
        ModuleV2 g = null; bool red = false; int parts = ModuleMesh.All; List<(int, int, int)> tris = null; bool tRed = false; List<Vector2> poly = null;
        if (hasHv && camDrag == 0)
        {
            Vector2 p = hv;
            switch (tool)
            {
                case "floor": tris = BrushTris(p); break;
                case "fill": tris = FillRegion(p); break;
                case "wall":
                    if (dragKind == "wall" && dragN > 0) { var segs = SegsBetween(dragA, dragD, dragN); g = Mini(L => { foreach (var k in segs) L.w[k] = new ModWall { m = wmat }; }); }
                    else poly = Dot(P(dragKind == "wall" ? dragA : VertexAt(p)), 0.35f);
                    break;
                case "room": { var ws = RoomWalls(p); if (ws.Count > 0) g = Mini(L => { foreach (var k in ws) if (!Lvl.w.ContainsKey(k)) L.w[k] = new ModWall { m = wmat }; }); break; }
                case "door": case "window": case "gate": case "open": case "close":
                    if (NearestWall(p, 0.7f, out var wk)) { var W = Lvl.w[wk]; g = Mini(L => L.w[wk] = new ModWall { m = W.m, o = tool == "close" ? "" : tool }); } break;
                case "stair": case "ustair": case "ramp": case "spiral":
                    {
                        var S = dragKind == "stair" ? NewStair(dragA, dragD, dragMoved ? dragN : (tool == "spiral" ? 1 : 3)) : HoverStair(p);
                        g = Mini(L => L.s.Add(S), true); parts = ModuleMesh.Stairs; poly = StairPoly(S); break;
                    }
                case "hexramp": if (InArea(p)) { var S = HexRampAt(p); g = Mini(L => L.s.Add(S)); parts = ModuleMesh.Stairs; } break;
                case "roof":
                    if (InArea(p)) { var c = MV2.CellOf(p.x, p.y); bool same = Lvl.r.TryGetValue((c.x, c.y), out var rk) && rk == rkind; g = Mini(L => { foreach (var kv in Lvl.w) L.w[kv.Key] = kv.Value; L.r[(c.x, c.y)] = rkind; }); parts = ModuleMesh.Roofs; red = same; }
                    break;
                case "object":
                    { int oi = ObjectAt(p, 0.8f); if (oi >= 0) { var O = Lvl.o[oi].Copy(); O.rot += Mathf.PI / 6f; g = Mini(L => L.o.Add(O)); } else if (InArea(p)) { var O = ObjPose(obj, p); g = Mini(L => L.o.Add(O)); } break; }
                case "erase": case "select":
                    if (tool == "select" && dragKind == "move") break;
                    if (PickAt(p, out var pk)) { red = tool == "erase"; GhostOfPick(pk, ref g, ref parts, ref tris); tRed = red; }
                    break;
            }
        }
        if (g == null && tris == null && selOn && tool == "select") GhostOfPick(sel, ref g, ref parts, ref tris);
        ShowGhost(g, red, parts); ShowTris(tris, tRed, poly);
    }
    void GhostOfPick(Pick pk, ref ModuleV2 g, ref int parts, ref List<(int, int, int)> tris)
    {
        switch (pk.kind)
        {
            case "o": if (pk.idx < Lvl.o.Count) { var O = Lvl.o[pk.idx].Copy(); g = Mini(L => L.o.Add(O)); } break;
            case "w": if (Lvl.w.TryGetValue(pk.key, out var W)) { var k = pk.key; g = Mini(L => L.w[k] = W.Copy()); parts = ModuleMesh.WallsP; } break;
            case "s": if (pk.idx < Lvl.s.Count) { var S = Lvl.s[pk.idx].Copy(); g = Mini(L => L.s.Add(S), true); parts = ModuleMesh.Stairs; } break;
            case "r": { var c = pk.cell; if (Lvl.r.TryGetValue(c, out var rk)) { g = Mini(L => { foreach (var kv in Lvl.w) L.w[kv.Key] = kv.Value; L.r[c] = rk; }); parts = ModuleMesh.Roofs; } break; }
            case "f": tris = new List<(int, int, int)> { pk.key }; break;
        }
    }

    void OnDestroy() { if (I == this) I = null; }
}
