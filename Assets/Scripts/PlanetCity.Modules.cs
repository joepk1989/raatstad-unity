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
                SetTerr(i, g + (i == c ? 0f : d.sqrMagnitude / (2f * Planet.R)));   // het hele gebied op één hoogte, in het raakvlak van het midden
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
        if (lv <= 0 || area == null) return null;
        var mb = new MeshBuilder();
        foreach (int i in area)
        {
            int below = 0; float bg = G(i);
            foreach (var J in ModulesAt(i)) { var JM = J.M; if (JM == null) continue; int t = J.lv + MV2.MetaOf(JM).fl; if (J.lv < lv && t <= lv && t > below) { below = t; bg = G(J.c); } }
            var cell = Planet.Cells[i]; Vector3 dir = cell.dir; float tt = Vector3.Dot(floor, up) / Vector3.Dot(dir, up);
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
    class ModGo { public GameObject go, poles; public string sig; }
    static readonly Dictionary<string, ModGo> modGos = new Dictionary<string, ModGo>();
    public static int ModuleCount => modGos.Count;
    public static void SyncModules()
    {
        if (Planet.Cells == null) return;
        if (modRoot == null) { foreach (var g in modGos.Values) { if (g.go != null) Object.Destroy(g.go); if (g.poles != null) Object.Destroy(g.poles); } modGos.Clear(); modRoot = new GameObject("Modules").transform; modRoot.gameObject.AddComponent<ModuleLights>(); }
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
            if (e != null) { if (e.go != null) Object.Destroy(e.go); if (e.poles != null) Object.Destroy(e.poles); }
            var b = ModuleMesh.Get(M, I.lv > 0, cover);
            var go = new GameObject("Module " + M.name); go.layer = Layers.Gebouw; go.transform.SetParent(modRoot, false);
            ModPose(I.c, I.rot, I.lv, out var pos, out var q, out var sc); go.transform.SetPositionAndRotation(pos, q); go.transform.localScale = sc;
            go.AddComponent<MeshFilter>().sharedMesh = b.mesh; go.AddComponent<MeshRenderer>().sharedMaterials = b.mats;
            go.AddComponent<MeshCollider>().sharedMesh = b.mesh;
            GameObject pg = null;
            if (poles != null) { pg = new GameObject("Palen " + M.name); pg.layer = Layers.Gebouw; pg.transform.SetParent(modRoot, false); pg.transform.position = porg; pg.AddComponent<MeshFilter>().sharedMesh = poles; pg.AddComponent<MeshRenderer>().sharedMaterial = ModuleMesh.Mat("slab"); pg.AddComponent<MeshCollider>().sharedMesh = poles; }
            modGos[I.Key] = new ModGo { go = go, poles = pg, sig = sig };
        }
        var gone = new List<string>(); foreach (var kv in modGos) if (!seen.Contains(kv.Key)) gone.Add(kv.Key);
        foreach (var k in gone) { if (modGos[k].go != null) Object.Destroy(modGos[k].go); if (modGos[k].poles != null) Object.Destroy(modGos[k].poles); modGos.Remove(k); }
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
}
