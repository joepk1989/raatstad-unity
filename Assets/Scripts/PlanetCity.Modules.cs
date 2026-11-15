using System.Collections.Generic;
using UnityEngine;

// Modules op de bol. Een exemplaar staat in de vakinhoud (Cells) als tekst, zodat opslaan, undo en Snapshot/Restore vanzelf meegaan:
//   middelste vak:  "m|<id>|<draaiing 0-5>|<spiegeling 0/1>|<laag>"
//   de zes buren van een module van 7 vakken:  "m^|<middelste vak>|<laag>"
// De laag is in verdiepingen van 3 m (0 = op de grond, op een sokkel op de hoogte van het terrein zoals de blokken).
// Een module ligt in het raakvlak van het middelste vak: lokale x wijst naar de buur in de richting van de draaiing, de schaal
// volgt de echte afstand tussen de vakken (een kleine speling, zodat de randen net binnen de echte vakranden blijven).
// Op de 12 vijfhoeken komen geen modules.
public static partial class PlanetCity
{
    public static bool IsModItem(string s) => s != null && s.Length > 1 && s[0] == 'm' && (s[1] == '|' || s[1] == '^');
    public struct ModInst
    {
        public int c, rot, lv; public string id; public bool mir;
        public ModuleV2 M => ModuleLib.Data(id, mir);
        public string Key => c + "|" + lv;
    }
    public static string ModItem(string id, int rot, bool mir, int lv) => "m|" + id + "|" + rot + "|" + (mir ? 1 : 0) + "|" + lv;
    static string ModPart(int c, int lv) => "m^|" + c + "|" + lv;
    static bool ParseMod(string s, int c, out ModInst I)
    {
        I = default; if (s == null || !s.StartsWith("m|")) return false; var p = s.Split('|'); if (p.Length < 5) return false;
        I.c = c; I.id = p[1]; int.TryParse(p[2], out I.rot); I.mir = p[3] == "1"; int.TryParse(p[4], out I.lv); return true;
    }
    public static bool HasModule(int i) { if (!Cells.TryGetValue(i, out var l)) return false; foreach (var s in l) if (IsModItem(s)) return true; return false; }
    // alle exemplaren die vak i bedekken (van laag naar hoog)
    public static List<ModInst> ModulesAt(int i)
    {
        var o = new List<ModInst>(); if (!Cells.TryGetValue(i, out var l)) return o;
        foreach (var s in l)
        {
            if (ParseMod(s, i, out var I)) { o.Add(I); continue; }
            if (s.StartsWith("m^|")) { var p = s.Split('|'); if (p.Length >= 3 && int.TryParse(p[1], out int c) && int.TryParse(p[2], out int lv) && FindMod(c, lv, out var J)) o.Add(J); }
        }
        o.Sort((a, b) => a.lv.CompareTo(b.lv)); return o;
    }
    static bool FindMod(int c, int lv, out ModInst I)
    {
        I = default; if (!Cells.TryGetValue(c, out var l)) return false;
        foreach (var s in l) if (ParseMod(s, c, out I) && I.lv == lv) return true; return false;
    }
    // de bovenkant (in lagen) van wat er op vak i staat aan modules, en het bovenste exemplaar
    public static bool ModTop(int i, out int top, out ModInst inst)
    {
        top = 0; inst = default; bool any = false;
        foreach (var I in ModulesAt(i)) { var M = I.M; int t = I.lv + (M != null ? MV2.MetaOf(M).fl : 1); if (!any || t > top || (t == top && I.lv > inst.lv)) { top = t; inst = I; any = true; } }
        return any;
    }

    // ---------- het gebied op de bol ----------
    // assen van het raakvlak voor draaiing rot: A = lokale x, B = lokale z van de webversie, up = omhoog
    public static void ModAxes(int c, int rot, out Vector3 A, out Vector3 B, out Vector3 up)
    {
        var cell = Planet.Cells[c]; up = cell.dir; Vector3 d0 = Planet.Cells[cell.nb[0]].dir - up; Vector3 A0 = (d0 - up * Vector3.Dot(d0, up)).normalized, B0 = Vector3.Cross(up, A0);
        float th = -rot * Mathf.PI / 3f; A = A0 * Mathf.Cos(th) + B0 * Mathf.Sin(th); B = Vector3.Cross(up, A);
    }
    // de vakken van het gebied in de volgorde van MV2.AreaCells (eerst het midden); null als het niet past (vijfhoek)
    public static int[] ModArea(int c, int k, int rot)
    {
        if (c < 0 || c >= Planet.Cells.Length) return null; var cell = Planet.Cells[c]; if (cell.nb.Length != 6) return null;
        if (k <= 0) return new[] { c };
        ModAxes(c, rot, out var A, out var B, out var up); var area = MV2.AreaCells(1); var o = new int[area.Count]; o[0] = c;
        for (int idx = 1; idx < area.Count; idx++)
        {
            var p = MV2.CellXZ(area[idx].x, area[idx].y); Vector3 want = (A * p.x + B * p.y).normalized; int best = -1; float bd = -2f;
            foreach (int j in cell.nb) { Vector3 d = Planet.Cells[j].dir - up; d -= up * Vector3.Dot(d, up); float dot = Vector3.Dot(d.normalized, want); if (dot > bd) { bd = dot; best = j; } }
            if (best < 0 || bd < 0.8f || Planet.Cells[best].nb.Length != 6) return null;
            for (int q = 0; q < idx; q++) if (o[q] == best) return null;
            o[idx] = best;
        }
        return o;
    }
    // plek, stand en schaal van een exemplaar (de mesh is gebouwd in Unity-coördinaten: lokale z = −z van de webversie)
    public static void ModPose(int c, int rot, int lv, out Vector3 pos, out Quaternion q, out Vector3 scale, float? baseG = null)
    {
        ModAxes(c, rot, out var A, out var B, out var up); var cell = Planet.Cells[c];
        pos = up * (Planet.R + (baseG ?? G(c)) + lv * MV2.LV_H);
        q = Quaternion.LookRotation(-B, up);
        float sum = 0f; foreach (int j in cell.nb) { Vector3 d = (Planet.Cells[j].dir - up) * Planet.R; sum += (d - up * Vector3.Dot(d, up)).magnitude; }
        float s = sum / cell.nb.Length / (MV2.R * MV2.SQ3) * 0.995f;
        scale = new Vector3(s, 1f, s);
    }

    // ---------- plaatsen en weghalen ----------
    static bool FreeGround(int i) => !Cells.TryGetValue(i, out var l) || l.Count == 0 || (l.Count == 1 && (l[0] == "park" || IsWild(l[0])));
    // Twee plaatsingswijzen (te kiezen in PlanetBuilder.Modules, toets T):
    //  terrein volgen: de module staat op de hoogte van zijn vak (of bovenop de modules daar); lift zet hem zoveel lagen hoger,
    //                  op palen boven wat eronder staat (een lagere module, een schuin dak of de grond)
    //  vaste hoogte:   de module komt op hoogte fixedH (stappen van 3 m, over de hele planeet gelijk); het terrein van zijn vakken
    //                  gaat naar die hoogte (sokkel of keermuur), zodat modules op dezelfde hoogte precies aansluiten
    public const float FixedStep = 3f;
    public static float FixedSnap(float g) => Mathf.Max(0f, Mathf.Round(g / FixedStep) * FixedStep);
    // kan module m hier (middelste vak c, draaiing rot)? lv: de laag waarop hij komt
    public static string CanPlaceModule(ModuleV2 m, int c, int rot, out int lv, int lift = 0, float? fixedH = null)
    {
        lv = 0; if (m == null) return "Geen module gekozen";
        if (c < 0 || c >= Planet.Cells.Length) return "Hier niet";
        if (Planet.Cells[c].nb.Length != 6) return "Op een vijfhoek komen geen modules";
        var area = ModArea(c, m.k, rot); if (area == null) return "Hier past de module niet: er ligt een vijfhoek in het gebied";
        int withMod = 0, maxTop = 0; bool uneven = false; var tops = new int[area.Length]; var flat = new bool[area.Length];
        for (int k = 0; k < area.Length; k++)
        {
            int i = area[k]; flat[k] = true;
            if (ModTop(i, out int t, out var I)) { if (withMod > 0 && t != maxTop) uneven = true; withMod++; tops[k] = t; maxTop = Mathf.Max(maxTop, t); var M = I.M; flat[k] = M != null && MV2.MetaOf(M).stack; continue; }
            if (RotOf(i) >= 0) return "Hier ligt een rotonde";
            if (!FreeGround(i)) { Cells.TryGetValue(i, out var l); return l[0] == "weg" || l[0] == "wegdeel" ? "Hier ligt een weg" : l[0] == "pad" ? "Hier ligt een pad" : l[0] == "water" || l[0] == "meer" ? "Hier ligt water" : "Hier staat al iets"; }
        }
        if (withMod > 0 && withMod < area.Length) uneven = true;
        if (withMod == 0 && fixedH.HasValue) { if (fixedH.Value < 0f) return "Onder het zeeniveau kan niet"; if (lv + MV2.MetaOf(m).fl > MV2.MAX_LV) return "Te hoog"; return null; }   // vaste hoogte: het terrein gaat erheen
        lv = maxTop + Mathf.Max(0, lift);
        if (uneven && lift == 0) return "Niet overal even hoog: zet hem een laag hoger (PgUp), dan komt hij op palen";
        for (int k = 0; k < area.Length; k++)
        {
            if (tops[k] == lv && !flat[k]) return "Op een schuin dak of open module kun je niet bouwen";
            if (!flat[k] && tops[k] < lv && tops[k] + 2 > lv) return "Te dicht boven een schuin dak: nog een laag hoger (PgUp)";
        }
        if (lv + MV2.MetaOf(m).fl > MV2.MAX_LV) return "Te hoog: hooguit " + MV2.MAX_LV + " lagen";
        if (lv == 0 && G(c) < SeaLevel - 0.01f) return "Hier staat water";
        return null;
    }
    public static string PlaceModule(string id, int c, int rot, bool mir, int lift = 0, float? fixedH = null)
    {
        var m = ModuleLib.Data(id, mir); string why = CanPlaceModule(m, c, rot, out int lv, lift, fixedH); if (why != null) return why;
        var area = ModArea(c, m.k, rot); rot = ((rot % 6) + 6) % 6;
        bool ground = true; foreach (int i in area) if (HasModule(i)) ground = false;
        if (ground)
        {
            float g = fixedH ?? G(c); var up = Planet.Cells[c].dir; bool level = lv == 0;   // op palen (lift) blijft het terrein zoals het is
            foreach (int i in area)
            {
                if (Cells.TryGetValue(i, out var l)) l.Clear();   // bomen en wilde natuur wijken
                if (!level) continue;
                Vector3 d = (Planet.Cells[i].dir - up) * Planet.R; d -= up * Vector3.Dot(d, up);
                SetTerr(i, g);   // het hele gebied op één hoogte (de module volgt de bol, WarpMesh)
            }
        }
        for (int k = 0; k < area.Length; k++)
        {
            int i = area[k]; if (!Cells.TryGetValue(i, out var l)) { l = new List<string>(); Cells[i] = l; }
            l.Add(k == 0 ? ModItem(id, rot, mir, lv) : ModPart(c, lv)); Touch(i);
        }
        return null;
    }

    // ---------- palen onder een module die hoger staat dan wat eronder is ----------
    // per vak van het gebied een zeshoekige paal (0,45 m) van de bovenkant van wat eronder staat tot onder de vloer van de module
    public static Mesh PoleMesh(int c, int[] area, int lv, float baseG, out Vector3 origin, out string sig)
    {
        var up = Planet.Cells[c].dir; Vector3 floor = up * (Planet.R + baseG + lv * MV2.LV_H); origin = floor; sig = "";
        if (area == null) return null;
        var mb = new MeshBuilder();
        if (lv <= 0)
        {
            // Fundering: waar de grond onder de module wegloopt (een helling naar een verdiept pad, een talud) een keermuur langs de
            // rand van het vak, van de vloer tot onder de laagste grond. Op vlakke grond komt er niets.
            foreach (int i in area)
            {
                var cell = Planet.Cells[i]; int nc = cell.corners.Length; float low = baseG; var hs = new float[nc];
                for (int k = 0; k < nc; k++)
                {
                    hs[k] = Mathf.Min(CornerH(i, k), TerrainAt(cell.corners[k])); low = Mathf.Min(low, hs[k]);
                    low = Mathf.Min(low, TerrainAt((cell.corners[k] + cell.corners[(k + 1) % nc]).normalized));
                }
                if (low > baseG - 0.25f) continue;
                sig += i + ":f" + Mathf.RoundToInt(low * 10f) + ",";
                float rt = Planet.R + baseG + 0.03f, rb = Planet.R + low - 0.6f;
                for (int k = 0; k < nc; k++)
                {
                    Vector3 ca = Vector3.Lerp(cell.corners[k], cell.dir, 0.02f).normalized, cb = Vector3.Lerp(cell.corners[(k + 1) % nc], cell.dir, 0.02f).normalized;
                    mb.Quad(ca * rb - origin, cb * rb - origin, cb * rt - origin, ca * rt - origin, (ca + cb) * 0.5f - cell.dir);
                }
            }
            return mb.v.Count > 0 ? mb.Build() : null;
        }
        foreach (int i in area)
        {
            int below = 0; float bg = G(i);
            foreach (var J in ModulesAt(i)) { var JM = J.M; if (JM == null) continue; int t = J.lv + MV2.MetaOf(JM).fl; if (J.lv < lv && t <= lv && t > below) { below = t; bg = G(J.c); } }
            var cell = Planet.Cells[i]; Vector3 dir = cell.dir; float tt = floor.magnitude;   // de vloer volgt de bol (WarpMesh)
            float h0 = Planet.R + bg + below * MV2.LV_H, h1 = tt - MV2.FLOOR_T - 0.05f; if (h1 - h0 < 0.6f) continue;
            sig += i + ":" + Mathf.RoundToInt(h0 * 10f) + ",";
            Vector3 e = cell.east, n = cell.north; const float r = 0.45f; var ring = new Vector3[6];
            for (int k = 0; k < 6; k++) { float a = k * Mathf.PI / 3f; ring[k] = (e * Mathf.Cos(a) + n * Mathf.Sin(a)) * r; }
            for (int k = 0; k < 6; k++)
            {
                Vector3 a0 = dir * h0 + ring[k] - origin, b0 = dir * h0 + ring[(k + 1) % 6] - origin, a1 = dir * h1 + ring[k] - origin, b1 = dir * h1 + ring[(k + 1) % 6] - origin;
                mb.Quad(a0, b0, b1, a1, ring[k] + ring[(k + 1) % 6]);
            }
        }
        return mb.v.Count > 0 ? mb.Build() : null;
    }

    // het bovenste exemplaar op vak i weghalen (alle vakken ervan)
    public static bool RemoveModuleAt(int i)
    {
        if (!ModTop(i, out _, out var I)) return false; RemoveModule(I); return true;
    }
    public static void RemoveModule(ModInst I)
    {
        var M = I.M; var area = ModArea(I.c, M != null ? M.k : 0, I.rot) ?? new[] { I.c };
        string me = null; if (Cells.TryGetValue(I.c, out var l0)) foreach (var s in l0) if (ParseMod(s, I.c, out var J) && J.lv == I.lv) { me = s; break; }
        string part = ModPart(I.c, I.lv);
        foreach (int i in area) { if (!Cells.TryGetValue(i, out var l)) continue; if (i == I.c && me != null) l.Remove(me); else l.Remove(part); if (l.Count == 0) Cells.Remove(i); Touch(i); }
        // losse stukken (als het gebied niet meer klopt): elk vak rond het midden nakijken
        foreach (int j in Planet.Cells[I.c].nb) if (Cells.TryGetValue(j, out var l) && l.Remove(part)) { if (l.Count == 0) Cells.Remove(j); Touch(j); }
    }

    // ---------- tekenen: één object per exemplaar, met de gedeelde mesh van ModuleMesh ----------
    static Transform modRoot;
    class ModGo { public GameObject go, poles; public Mesh mesh; public string sig; }
    static void Drop(ModGo g) { if (g == null) return; if (g.go != null) { PropsForget(g.go); Object.Destroy(g.go); } if (g.poles != null) Object.Destroy(g.poles); if (g.mesh != null) Object.Destroy(g.mesh); }

    // ---------- vervormen naar de echte vakken ----------
    // Elk punt van de module ligt boven een plek in een van zijn vakken, in een van de zes driehoekjes tussen het midden en twee
    // hoekpunten. Dat punt gaat naar dezelfde plek in het echte driehoekje op de bol, en omhoog langs de richting van de bol daar.
    // Zo vallen de randen precies op de echte vakranden, delen buurmodules dezelfde hoekpunten en gaan gedeelde muren bij beide langs
    // dezelfde lijn omhoog: op elke hoogte naadloos, ook bovenaan een toren. De mesh is per exemplaar (wereldcoördinaten rond origin).
    // De rand van de module ligt 2 cm binnen de vakrand, zodat twee muren op dezelfde rand niet in elkaar flikkeren.
    public static Mesh WarpMesh(Mesh src, int c, int[] area, int k, int rot, float baseR, out Vector3 origin)
    {
        ModAxes(c, rot, out var A, out var B, out var up); origin = up * baseR;
        var ideal = MV2.AreaCells(k); int n = area.Length; var cen = new Vector2[n]; var dC = new Vector3[n]; var dK = new Vector3[n * 6];
        for (int idx = 0; idx < n; idx++)
        {
            cen[idx] = MV2.CellXZ(ideal[idx].x, ideal[idx].y); var cell = Planet.Cells[area[idx]]; dC[idx] = cell.dir;
            for (int kk = 0; kk < 6; kk++)
            {
                float an = (30f + 60f * kk) * Mathf.Deg2Rad; Vector2 pk = cen[idx] + new Vector2(Mathf.Cos(an), Mathf.Sin(an)) * MV2.R;
                Vector3 w = (up * Planet.R + A * pk.x + B * pk.y).normalized; Vector3 best = cell.corners[0]; float bd = -2f;
                foreach (var cd in cell.corners) { float dt = Vector3.Dot(cd, w); if (dt > bd) { bd = dt; best = cd; } }
                dK[idx * 6 + kk] = best;
            }
        }
        float shrink = 1f - 0.02f / (MV2.R * MV2.SQ3 / 2f * (1 + 2 * k));
        var v = src.vertices; var o = new Vector3[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            Vector2 pt = new Vector2(v[i].x, -v[i].z) * shrink; float y = v[i].y;
            var ax = MV2.CellOf(pt.x, pt.y); int idx = ideal.IndexOf(ax);
            if (idx < 0) { float bd = 1e9f; for (int q = 0; q < n; q++) { float dd = (pt - cen[q]).sqrMagnitude; if (dd < bd) { bd = dd; idx = q; } } }
            Vector2 d = pt - cen[idx]; int sct = Mathf.FloorToInt(Mathf.Repeat(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 30f, 360f) / 60f) % 6;
            float a0 = (30f + 60f * sct) * Mathf.Deg2Rad, a1 = (30f + 60f * (sct + 1)) * Mathf.Deg2Rad;
            Vector2 e1 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * MV2.R, e2 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * MV2.R;
            float det = e1.x * e2.y - e1.y * e2.x, u = (d.x * e2.y - d.y * e2.x) / det, w2 = (e1.x * d.y - e1.y * d.x) / det;
            Vector3 dir = (dC[idx] * (1f - u - w2) + dK[idx * 6 + sct] * u + dK[idx * 6 + (sct + 1) % 6] * w2).normalized;
            o[i] = dir * (baseR + y) - origin;
        }
        var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, name = src.name + " (op de bol)" };
        m.vertices = o; var uv = new List<Vector2>(); src.GetUVs(0, uv); if (uv.Count == o.Length) m.SetUVs(0, uv);
        m.subMeshCount = src.subMeshCount; for (int sm = 0; sm < src.subMeshCount; sm++) m.SetTriangles(src.GetTriangles(sm), sm, false);
        m.RecalculateNormals(); m.RecalculateBounds(); return m;   // elke driehoek heeft eigen hoekpunten: de normalen blijven vlak (low poly)
    }
    static readonly Dictionary<string, ModGo> modGos = new Dictionary<string, ModGo>();
    public static int ModuleCount => modGos.Count;
    public static void SyncModules()
    {
        if (Planet.Cells == null) return;
        if (modRoot == null) { foreach (var g in modGos.Values) Drop(g); modGos.Clear(); modRoot = new GameObject("Modules").transform; modRoot.gameObject.AddComponent<ModuleLights>(); }
        var want = new List<ModInst>(); var starts = new Dictionary<int, List<int>>();
        foreach (var kv in Cells) foreach (var s in kv.Value) if (ParseMod(s, kv.Key, out var I)) want.Add(I);
        var areas = new Dictionary<string, int[]>();
        foreach (var I in want)
        {
            var M = I.M; if (M == null) continue; var area = ModArea(I.c, M.k, I.rot); if (area == null) continue; areas[I.Key] = area;
            foreach (int i in area) { if (!starts.TryGetValue(i, out var l)) starts[i] = l = new List<int>(); l.Add(I.lv); }
        }
        var seen = new HashSet<string>();
        foreach (var I in want)
        {
            var M = I.M; if (M == null || !areas.TryGetValue(I.Key, out var area)) continue; seen.Add(I.Key);
            int topLv = I.lv + MV2.MetaOf(M).fl, cover = 0;
            for (int k = 0; k < area.Length; k++) if (starts.TryGetValue(area[k], out var l) && l.Contains(topLv)) cover |= 1 << k;
            var poles = PoleMesh(I.c, area, I.lv, G(I.c), out var porg, out var psig);
            string sig = I.id + "|" + M.ver + "|" + I.rot + "|" + I.mir + "|" + I.lv + "|" + cover + "|" + Mathf.RoundToInt(G(I.c) * 100f) + "|" + psig;
            if (modGos.TryGetValue(I.Key, out var e) && e.go != null && e.sig == sig) { if (poles != null) Object.Destroy(poles); continue; }
            Drop(e);
            var b = ModuleMesh.Get(M, I.lv > 0, cover);
            var go = new GameObject("Module " + M.name); go.layer = Layers.Gebouw; go.transform.SetParent(modRoot, false);
            var wm = WarpMesh(b.mesh, I.c, area, M.k, I.rot, Planet.R + G(I.c) + I.lv * MV2.LV_H, out var org); go.transform.position = org;
            go.AddComponent<MeshFilter>().sharedMesh = wm; go.AddComponent<MeshRenderer>().sharedMaterials = b.mats;
            go.AddComponent<MeshCollider>().sharedMesh = wm;
            ModuleProps(go, b, I.c);   // bomen, bankjes en lantaarns in de module los sloopbaar (PlanetCity.Wreck.cs)
            GameObject pg = null;
            if (poles != null) { pg = new GameObject("Palen " + M.name); pg.layer = Layers.Gebouw; pg.transform.SetParent(modRoot, false); pg.transform.position = porg; pg.AddComponent<MeshFilter>().sharedMesh = poles; pg.AddComponent<MeshRenderer>().sharedMaterial = ModuleMesh.Mat("slab"); pg.AddComponent<MeshCollider>().sharedMesh = poles; }
            modGos[I.Key] = new ModGo { go = go, poles = pg, mesh = wm, sig = sig };
        }
        var gone = new List<string>(); foreach (var kv in modGos) if (!seen.Contains(kv.Key)) gone.Add(kv.Key);
        foreach (var k in gone) { Drop(modGos[k]); modGos.Remove(k); }
    }
}

// Bij elke start (Play, ook zonder domein-herladen): alle lijsten met vaknummers leeg. Na een andere planeetgrootte (ontwikkelaar)
// wijzen oude nummers anders naar vakken die niet bestaan (IndexOutOfRange in Touch bij het opbouwen). De stad wordt daarna toch geladen.
public static partial class PlanetCity
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCellCaches()
    {
        carved.Clear(); preCarve.Clear(); preLevel.Clear(); deckSide.Clear(); partners.Clear(); Style.Clear(); Under.Clear(); Low.Clear(); Bike.Clear();
        groundChunks.Clear(); lastBuilt.Clear(); groundCache.Clear(); Scorch.Clear(); Terr.Clear(); levelled.Clear(); CenterOfs.Clear(); stripCache.Clear();
        fieldCells.Clear(); nearCache.Clear(); chunks.Clear(); dirty.Clear(); gumSkip.Clear(); edgeMate.Clear(); Rot.Clear(); boreNear.Clear(); FillTowns.Clear();
        cutKeys.Clear(); cutSig = ""; bores.Clear(); modGos.Clear(); modRoot = null;
    }

    // ---------- wat staat er op een vak (één leesfunctie voor schoonheid, drukte, mensen, geluid en namen) ----------
    // Voor modules uit de categorie (ModuleV2.cat) en MV2.MetaOf, voor de oude blokken uit de naam. Onthouden per module, niet per vak.
    public enum Soort { Niets, Wonen, Werken, Winkel, Horeca, Openbaar, Plein, Groen, Water, Blikvanger, Weg, Pad }
    public struct VakInhoud
    {
        public Soort soort; public int lagen;        // lagen: de bovenkant van wat er staat, in verdiepingen
        public bool gebouw, blikvanger;              // blikvanger: kerk, toren, molen (een kerk blijft soort Openbaar)
        public bool module, groot, midden;           // groot: module van 7 vakken; midden: het middelste vak (of een module van één vak)
        public int middenVak;                        // het middelste vak van de module (anders het vak zelf)
        public int deur;                             // het buurvak waar de deur aan dit vak naar wijst; -1: geen deur aan dit vak
        public string id, naam;                      // module-id (of de naam van het oude blok) en de nette naam
    }
    class ModSoort { public int ver, fl; public Soort soort; public bool gebouw, blik; public List<(int idx, Vector2 dir)> deuren = new List<(int, Vector2)>(); }
    static readonly Dictionary<ModuleV2, ModSoort> soortCache = new Dictionary<ModuleV2, ModSoort>();
    static ModSoort SoortVan(ModuleV2 M)
    {
        if (soortCache.TryGetValue(M, out var s) && s.ver == M.ver) return s;
        var meta = MV2.MetaOf(M); string id = M.id ?? "";
        s = new ModSoort { ver = M.ver, fl = Mathf.Max(1, meta.fl), gebouw = meta.bouw };
        switch (M.cat)
        {
            case "Wonen": s.soort = Soort.Wonen; break;
            case "Werken": s.soort = Soort.Werken; break;
            case "Winkels": case "Winkelcentrum": s.soort = Soort.Winkel; break;
            case "Horeca": s.soort = Soort.Horeca; break;
            case "Openbaar": case "Sport": case "Voorzieningen": case "Vervoer": s.soort = Soort.Openbaar; break;
            case "Pleinen": s.soort = Soort.Plein; break;
            case "Groen": s.soort = Soort.Groen; break;
            default: s.soort = Soort.Niets; break;   // Bouwdelen, Verbindingen en eigen categorieën
        }
        if (id.StartsWith("toren-") || id.StartsWith("windmolen") || id.StartsWith("watertoren")) { s.soort = Soort.Blikvanger; s.blik = true; }
        else if (id.StartsWith("kerk")) s.blik = true;
        DeurenVan(M, s.deuren);
        soortCache[M] = s; return s;
    }
    // De deuren van een module: elke muur op de begane grond met een deur of opening (een hekje in een heg telt alleen als het gebouw
    // zelf geen deur heeft), met de richting naar buiten. Niet uit de randcodes: een deur hoeft niet aan de rand van het vak te zitten
    // (laanhuis, restaurant met terras, villa met tuin).
    static readonly HashSet<string> BuitenVloer = new HashSet<string> { "grass", "paver", "asphalt", "water", "gravel", "sand" };
    static bool Binnen(ModLevel L, Vector2 p, float ts)
    {
        foreach (var kv in L.f)
        {
            var v = MV2.TriVerts(kv.Key.Item1, kv.Key.Item2, kv.Key.Item3, ts);
            float d0 = MV2.SideOf(v[0], v[1], p), d1 = MV2.SideOf(v[1], v[2], p), d2 = MV2.SideOf(v[2], v[0], p);
            if ((d0 >= 0f && d1 >= 0f && d2 >= 0f) || (d0 <= 0f && d1 <= 0f && d2 <= 0f)) return !BuitenVloer.Contains(kv.Value);
        }
        return false;
    }
    static void DeurenVan(ModuleV2 M, List<(int idx, Vector2 dir)> o)
    {
        if (M.lv.Count == 0) return; var L0 = M.lv[0]; float ts = MV2.Ts(M); var area = MV2.AreaCells(M.k);
        Vector2 cen = Vector2.zero; int n = 0; var cand = new List<(Vector2 mid, Vector2 nr, bool hedge)>();
        foreach (var kv in L0.w)
        {
            if (kv.Key.Item3 < 0 || kv.Key.Item3 > 5) continue; var (a, b) = MV2.WallSeg(kv.Key, ts); Vector2 mid = (a + b) * 0.5f;
            bool hg = kv.Value.m == "hedge" || kv.Value.m == "fence"; if (!hg) { cen += mid; n++; }
            if (kv.Value.o == "door" || kv.Value.o == "gate" || kv.Value.o == "open") cand.Add((mid, new Vector2(-(b - a).y, (b - a).x).normalized, hg));
        }
        if (n > 0) cen /= n; bool solid = cand.Exists(x => !x.hedge);
        foreach (var d in cand)
        {
            if (solid && d.hedge) continue;
            bool inA = Binnen(L0, d.mid + d.nr * 0.9f, ts), inB = Binnen(L0, d.mid - d.nr * 0.9f, ts);
            float sg = inA && !inB ? -1f : inB && !inA ? 1f : Vector2.Dot(d.nr, d.mid - cen) < 0f ? -1f : 1f;
            int idx = area.IndexOf(MV2.CellOf(d.mid.x, d.mid.y)); if (idx < 0) idx = 0;
            o.Add((idx, d.nr * sg));
        }
    }
    public static bool ModHasDoor(ModuleV2 M) => M != null && SoortVan(M).deuren.Count > 0;
    // de buurvakken waar de deuren van module M (midden c, draaiing rot) op uitkomen. Een deur naar een hoekpunt (laanhuis: de punt
    // van de voortuin) komt op beide buren aan die hoek uit.
    public static List<(int vak, int buur)> ModDoorCells(ModuleV2 M, int c, int rot)
    {
        var o = new List<(int, int)>(); if (M == null) return o; var s = SoortVan(M); if (s.deuren.Count == 0) return o;
        var area = ModArea(c, M.k, rot); if (area == null) return o; ModAxes(c, rot, out var A, out var B, out _);
        foreach (var d in s.deuren)
        {
            if (d.idx >= area.Length) continue; int vak = area[d.idx]; var cell = Planet.Cells[vak]; Vector3 up = cell.dir, want = (A * d.dir.x + B * d.dir.y).normalized;
            foreach (int j in cell.nb)
            {
                if (System.Array.IndexOf(area, j) >= 0) continue;   // een ander vak van dezelfde module (binnenhof)
                Vector3 t = Planet.Cells[j].dir - up; t -= up * Vector3.Dot(t, up); if (Vector3.Dot(t.normalized, want) > 0.8f) o.Add((vak, j));
            }
        }
        return o;
    }
    // het exemplaar dat telt op vak i: het laagste met een soort (de begane grond, daar komen mensen binnen); top: de bovenkant in lagen
    static bool ModPick(int i, out ModInst pick, out ModSoort ps, out int top)
    {
        pick = default; ps = null; top = 0; var mods = ModulesAt(i); if (mods.Count == 0) return false;
        pick = mods[0];
        foreach (var I in mods)
        {
            var M = I.M; var s = M != null ? SoortVan(M) : null; top = Mathf.Max(top, I.lv + (s != null ? s.fl : 1));
            if (s != null && (ps == null || (ps.soort == Soort.Niets && s.soort != Soort.Niets))) { pick = I; ps = s; }
        }
        return true;
    }
    // alle deuren van de module die vak i bedekt: op welk vak de deur zit en naar welk buurvak hij wijst (bij een deur naar een
    // hoekpunt de buur met een weg of pad, anders de eerste)
    public static List<(int vak, int buur)> ModDeuren(int i)
    {
        var o = new List<(int, int)>(); if (!ModPick(i, out var I, out var s, out _) || s == null || s.deuren.Count == 0) return o;
        var all = ModDoorCells(I.M, I.c, I.rot); all.Sort((x, y) => (IsWalk(y.buur) ? 1 : 0).CompareTo(IsWalk(x.buur) ? 1 : 0));
        var seen = new HashSet<int>();
        foreach (var d in all)
        {
            bool dup = false; foreach (var e in o) if (e.Item1 == d.vak && (e.Item2 == d.buur || IsNeighbour(e.Item2, d.buur))) dup = true;   // dezelfde deur (twee buren aan één hoek)
            if (!dup) o.Add(d);
        }
        return o;
    }
    public static VakInhoud Inhoud(int i)
    {
        var o = new VakInhoud { middenVak = i, deur = -1, midden = true };
        if (!Cells.TryGetValue(i, out var l) || l.Count == 0) return o;
        if (ModPick(i, out var I, out var s, out int top))
        {
            o.module = true; o.id = I.id; o.naam = I.id; o.middenVak = I.c; o.midden = I.c == i; o.lagen = top; o.gebouw = true;
            if (s == null) return o;   // een module die niet (meer) in de bibliotheek staat
            var M = I.M; o.soort = s.soort; o.gebouw = s.gebouw; o.blikvanger = s.blik; o.naam = string.IsNullOrEmpty(M.name) ? I.id : M.name; o.groot = M.k > 0;
            foreach (var d in ModDeuren(i)) if (d.vak == i) { o.deur = d.buur; break; }
            return o;
        }
        o.id = o.naam = l[0]; float h = 0f;
        foreach (var t in l)
        {
            Soort k = Soort.Niets; bool bld = false;
            switch (t)
            {
                case "woning": k = Soort.Wonen; bld = true; break;
                case "kantoor": k = Soort.Werken; bld = true; break;
                case "winkel": k = Soort.Winkel; bld = true; break;
                case "toren": case "molen": k = Soort.Blikvanger; bld = true; o.blikvanger = true; break;
                case "blok": case "dak": bld = true; break;
                case "plein": k = Soort.Plein; break;
                case "park": k = Soort.Groen; break;
                case "water": case "meer": k = Soort.Water; break;
                case "weg": case "wegdeel": k = Soort.Weg; break;
                case "pad": k = Soort.Pad; break;
            }
            if (bld) { o.gebouw = true; h += Blocks.Height(t); }
            // het eerste gebouw telt; een gebouw gaat voor de grond eronder (park, plein)
            if (k != Soort.Niets && (o.soort == Soort.Niets || (bld && !IsBldSoort(o.soort)))) { o.soort = k; o.id = o.naam = t; }
        }
        o.lagen = o.gebouw ? Mathf.Max(1, Mathf.RoundToInt(h / MV2.LV_H)) : 0;
        return o;
    }
    static bool IsBldSoort(Soort k) => k == Soort.Wonen || k == Soort.Werken || k == Soort.Winkel || k == Soort.Horeca || k == Soort.Openbaar || k == Soort.Blikvanger;
    // de zichtbare hoogte boven de grond van het vak in meters (StackHeight kent alleen de oude blokken; het terrein leunt daarop)
    public static float TopHeight(int i)
    {
        float h = StackHeight(i);
        if (ModTop(i, out int top, out var I)) h = Mathf.Max(h, top * MV2.LV_H + G(I.c) - G(i));
        return h;
    }
}
