using System.Collections.Generic;
using UnityEngine;

// Terrein low poly, medium poly of high poly (Instellingen > Terrein, onthouden in PlayerPrefs; low poly is de standaard).
// - Low poly: elke driehoek zijn eigen normaal (zoals altijd).
// - Medium en high poly (Rond): gedeelde normalen per hoekpunt en ook de vorm wordt afgerond: elke bodemdriehoek van een gewoon vak wordt in 9 (25) stukjes verdeeld op een gebogen vlak
//   door de zachte normalen van zijn drie hoekpunten (PN-driehoek). Dezelfde hoogtes per vak, alleen glooiend in plaats van puntig.
// De normaal komt uit een veld dat alleen van de plek afhangt (zoals SoilColor): per vak de helling van het vlak door de middens
// van zijn buren, gemengd naar de afstand tot die middens. Een gebogen rand hangt alleen af van zijn twee eindpunten en hun normalen,
// dus twee delen (ChunkN) en twee buurvakken sluiten naadloos aan.
// Strak blijven: wegen, sokkels en wanden (zitten niet in de bodemmesh), bebouwde vakken en vakken met een weg, pad, geul, gracht of
// tunnel (SmoothCell). Een rand naar zo'n vak blijft recht, zodat de buur er precies op aansluit. Een driehoek die meer dan
// SmoothCrease van het veld afwijkt houdt zijn eigen normaal.
// Botsen (lopen, rijden, de muis) gaat op de grove mesh: die volgt de hoogtes van de spellogica (H, SurfaceH). Bij Rond tekent de
// bodem een eigen fijne mesh; omschakelen tekent niets anders opnieuw (FillSoil per bestaand deel).
public static partial class PlanetCity
{
    public static readonly string[] SoilDetailNames = { "low poly", "medium poly", "high poly" };
    const string SmoothKey = "raatstad-terrein-detail";   // (de oude sleutel raatstad-terrein-zacht had vier standen)
    static int soilDetail = -1;
    public static int SoilDetail
    {
        get { if (soilDetail < 0) soilDetail = Mathf.Clamp(PlayerPrefs.GetInt(SmoothKey, 0), 0, 2); return soilDetail; }
        set
        {
            value = Mathf.Clamp(value, 0, 2); if (value == SoilDetail) return;
            soilDetail = value; PlayerPrefs.SetInt(SmoothKey, value); PlayerPrefs.Save();
            if (soilMat != null) soilMat.SetFloat("_SoilSmooth", value > 0 ? 1f : 0f);   // de soort ook naar de zachte normaal (Bodem.shader)
            var t0 = Time.realtimeSinceStartup; int n = 0; statSmooth = statTris = statCell = statCrease = 0; StatBulge = 0f;
            var dead = new List<MeshFilter>();
            foreach (var kv in soilMeshes) { if (kv.Key == null) { dead.Add(kv.Key); continue; } FillSoil(kv.Key, kv.Value); n++; }
            foreach (var m in dead) soilMeshes.Remove(m);
            Debug.Log("Terrein " + SoilDetailNames[value] + ": " + n + " delen, " + statSmooth + " van " + statTris + " driehoeken zacht (vak niet: " + statCell + ", knik: " + statCrease + "), nog rond te maken: " + roundTodo.Count + ", in " + ((Time.realtimeSinceStartup - t0) * 1000f).ToString("0") + " ms");
        }
    }
    public static bool SmoothSoil => SoilDetail > 0;
    const float SmoothCrease = 50f;   // graden: een driehoek die verder van het veld afwijkt houdt zijn eigen normaal (een berghelling wijkt tot zo'n 40° af)
    static int RoundN => SoilDetail >= 2 ? 5 : 3;   // medium poly: elke rand in 3 stukjes, een driehoek in 9; high poly: in 5, een driehoek in 25
    static int statSmooth, statTris, statCell, statCrease; public static float StatBulge;   // hoeveel Rond hooguit van de grove driehoek afwijkt (m)

    // v, tris, cells: de grove mesh; nFlat: de normaal per driehoek; sn: de zachte normaal per hoekpunt (ook voor de vorm van Rond);
    // nSmooth: de normalen van Zacht; state per driehoek: 0 strak, 1 zacht, 2 knik; curve per driehoek: bit 0-2 welke rand gebogen is
    class SoilPart { public Mesh coarse, fine, wire; public Vector3[] v, nFlat, sn, nSmooth, nField; public int[] tris, cells; public byte[] state, curve; public Color[] col; public List<Vector4> ext; }

    // Terreinlijnen (Instellingen, onthouden in PlayerPrefs): dunne donkere lijnen langs elke driehoek van de bodem, in elke stand
    const string WireKey = "raatstad-terrein-lijnen";
    static int soilWire = -1;
    public static bool SoilWire
    {
        get { if (soilWire < 0) soilWire = PlayerPrefs.GetInt(WireKey, 0) == 1 ? 1 : 0; return soilWire == 1; }
        set
        {
            if (value == SoilWire) return; soilWire = value ? 1 : 0; PlayerPrefs.SetInt(WireKey, soilWire); PlayerPrefs.Save();
            foreach (var kv in soilMeshes) if (kv.Key != null) Wire(kv.Key, kv.Value);
        }
    }
    static void Wire(MeshFilter mf, SoilPart part)
    {
        var old = mf.transform.Find("Terreinlijnen"); if (old != null) Object.Destroy(old.gameObject);
        if (part.wire != null) { Object.Destroy(part.wire); part.wire = null; }
        if (!SoilWire || mf.sharedMesh == null) return;
        var src = mf.sharedMesh; var v = src.vertices; var n = src.normals; var tris = src.triangles;
        var lv = new Vector3[v.Length]; for (int k = 0; k < v.Length; k++) lv[k] = v[k] + (k < n.Length ? n[k] : v[k].normalized) * 0.04f;   // net boven de grond
        var li = new int[tris.Length * 2];
        for (int t = 0; t + 2 < tris.Length; t += 3) { int a = tris[t], b = tris[t + 1], c = tris[t + 2]; int o = t * 2; li[o] = a; li[o + 1] = b; li[o + 2] = b; li[o + 3] = c; li[o + 4] = c; li[o + 5] = a; }
        var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, name = "terreinlijnen" };
        m.vertices = lv; m.SetIndices(li, MeshTopology.Lines, 0); m.bounds = src.bounds; part.wire = m;
        var go = new GameObject("Terreinlijnen"); go.transform.SetParent(mf.transform, false); go.layer = mf.gameObject.layer;
        go.AddComponent<MeshFilter>().sharedMesh = m;
        var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = Mats.GetUnlit("p_terreinlijnen", new Color(0.08f, 0.10f, 0.06f));
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
    }
    static readonly Dictionary<MeshFilter, SoilPart> soilMeshes = new Dictionary<MeshFilter, SoilPart>();   // de bodem per deel (zolang het deel bestaat)

    // Rond bouwen kost tijd (9 of 25 keer zoveel driehoeken). Daarom: per beeld hooguit RoundNowMs meteen (een klik: het deel staat
    // direct rond in beeld), de rest staat eerst zacht (grove mesh) en wordt in de volgende beelden rond, het dichtst bij de camera
    // eerst (SoilTick, RoundTickMs per beeld). Laden en omschakelen wachten er dus niet op.
    const float RoundNowMs = 10f, RoundTickMs = 6f;
    static readonly List<MeshFilter> roundTodo = new List<MeshFilter>(); static int roundFrame = -1; static float roundUsed; static SoilRunner soilRunner;
    public static int RoundPending => roundTodo.Count;

    // na het bouwen van een deel (SoilMesh): coarse is de grove mesh met kleuren (ook de collider), cells het vak per hoekpunt
    static void RegisterSoil(MeshFilter mf, Mesh coarse, List<int> cells)
    {
        if (soilMeshes.Count > 0) { var dead = new List<MeshFilter>(); foreach (var k in soilMeshes.Keys) if (k == null) dead.Add(k); foreach (var k in dead) soilMeshes.Remove(k); }
        var part = new SoilPart { coarse = coarse, v = coarse.vertices, tris = coarse.triangles, cells = cells.ToArray() }; soilMeshes[mf] = part;
        FillSoil(mf, part);
    }

    static void FillSoil(MeshFilter mf, SoilPart part)
    {
        if (part.fine != null) { Object.Destroy(part.fine); part.fine = null; }
        int d = SoilDetail; roundTodo.Remove(mf);
        if (d == 0) { if (part.nFlat == null) Flat(part); part.coarse.normals = part.nFlat; mf.sharedMesh = part.coarse; }
        else
        {
            if (part.sn == null) Prepare(part);
            if (Time.frameCount != roundFrame) { roundFrame = Time.frameCount; roundUsed = 0f; }
            if (roundUsed < RoundNowMs) { var sw = System.Diagnostics.Stopwatch.StartNew(); part.fine = RoundMesh(part); mf.sharedMesh = part.fine; roundUsed += (float)sw.Elapsed.TotalMilliseconds; }
            else
            {
                part.coarse.normals = part.nSmooth; mf.sharedMesh = part.coarse;
                { roundTodo.Add(mf); if (soilRunner == null && Application.isPlaying) { var go = new GameObject("Bodem rond"); Object.DontDestroyOnLoad(go); soilRunner = go.AddComponent<SoilRunner>(); } }
            }
        }
        if (SoilWire || part.wire != null) Wire(mf, part);
    }

    // per beeld: een paar wachtende delen rond maken, het dichtst bij de camera eerst
    public static void SoilTick()
    {
        if (roundTodo.Count == 0) return;
        if (SoilDetail < 1) { roundTodo.Clear(); return; }
        var cam = Camera.main; Vector3 cp = cam != null ? cam.transform.position : Vector3.zero;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (roundTodo.Count > 0 && sw.Elapsed.TotalMilliseconds < RoundTickMs)
        {
            int best = -1; float bd = float.MaxValue;
            for (int k = roundTodo.Count - 1; k >= 0; k--)
            {
                var f = roundTodo[k]; if (f == null || !soilMeshes.ContainsKey(f)) { roundTodo.RemoveAt(k); if (best > k) best--; continue; }
                float dd = (soilMeshes[f].coarse.bounds.center + f.transform.position - cp).sqrMagnitude; if (dd < bd) { bd = dd; best = k; }
            }
            if (best < 0) break;
            var mf = roundTodo[best]; roundTodo.RemoveAt(best); var part = soilMeshes[mf];
            if (part.fine != null) Object.Destroy(part.fine);
            part.fine = RoundMesh(part); mf.sharedMesh = part.fine;
            if (SoilWire || part.wire != null) Wire(mf, part);
        }
    }

    // mag de bodem van vak i zacht en rond worden? (geen weg, pad, bebouwing, geul, gracht of tunnel)
    static bool SmoothCell(int i) => i >= 0 && !IsWalk(i) && !Plateau(i) && !fieldCells.Contains(i) && !(RoadH.ContainsKey(i))
        && !(carved.Count > 0 && carved.Contains(i)) && !(boreNear.Count > 0 && boreNear.ContainsKey(i)) && !Planet.IsPole(i) && !NoGround(i) && !Quay(i);

    // Vakken langs een weg of pad (Terrein v2: het veld is in veel kleine stukken gesneden, melding 15): de vorm blijft strak (hij sluit
    // precies op de weg en de buren aan), maar de belichting wordt zacht: per hoekpunt het gemiddelde van de driehoeken die daar samenkomen
    // en minder dan FieldCrease van elkaar afwijken (naar oppervlak gewogen, zodat een splinter de grond eromheen volgt). Een echte knik
    // (de bovenkant van een steil talud, een wand) blijft scherp.
    static bool FieldSoft(int i) => i >= 0 && !SmoothCell(i) && !Plateau(i) && !Planet.IsPole(i) && !NoGround(i) && !Quay(i) && !(boreNear.Count > 0 && boreNear.ContainsKey(i));
    const float FieldCrease = 70f;   // (ruim: een splinter in een talud van een paar centimeter staat steil, maar hoort bij de grond eromheen; het oppervlak weegt)

    // waarom vak i strak blijft (voor tests)
    public static string SmoothWhy(int i) => IsWalk(i) ? "weg" : Plateau(i) ? "plateau" : fieldCells.Contains(i) ? "veld" : RoadH.ContainsKey(i) ? "profiel" : carved.Contains(i) ? "geul" : boreNear.ContainsKey(i) ? "tunnel" : Planet.IsPole(i) ? "pool" : NoGround(i) ? "geengrond" : Quay(i) ? "kade" : "zacht";

    // de normaal van vak i: het vlak door de middens van zijn buren (som van de kruisproducten rondom)
    static Vector3 CellNormal(int i)
    {
        var c = Planet.Cells[i]; var nb = c.nb; int m = nb.Length;
        Vector3 Pi = c.dir * (Planet.R + G(i) + CenterOfs1(i)), s = Vector3.zero;
        for (int k = 0; k < m; k++)
        {
            int a = nb[k], b = nb[(k + 1) % m]; if (a < 0 || b < 0) continue;
            var ca = Planet.Cells[a]; var cb = Planet.Cells[b];
            Vector3 Pa = ca.dir * (Planet.R + G(a) + CenterOfs1(a)) - Pi, Pb = cb.dir * (Planet.R + G(b) + CenterOfs1(b)) - Pi;
            Vector3 x = Vector3.Cross(Pa, Pb); if (Vector3.Dot(x, c.dir) < 0f) x = -x; s += x;
        }
        return s.sqrMagnitude > 1e-8f ? s.normalized : c.dir;
    }

    // de zachte normaal op plek p (in vak i): de normalen van vak i en zijn buren, gewogen zoals SoilColor
    static Vector3 SmoothNormal(int i, Vector3 p, Dictionary<int, Vector3> memo)
    {
        Vector3 d = p.normalized, s = Vector3.zero; var c = Planet.Cells[i];
        void Add(int j)
        {
            if (j < 0) return;
            float dist = (Planet.Cells[j].dir - d).magnitude * Planet.R; if (dist >= SoilReach) return;
            float w = 1f - dist / SoilReach; w *= w;
            if (!memo.TryGetValue(j, out var nj)) { nj = CellNormal(j); memo[j] = nj; }
            s += nj * w;
        }
        Add(i); foreach (int j in c.nb) Add(j);
        return s.sqrMagnitude > 1e-10f ? s.normalized : d;
    }

    // de zachte normalen van het veld langs wegen (FieldSoft) per hoekpunt van de grove mesh, Vector3.zero voor de rest; null als er geen is.
    // fn: de normaal per driehoek (per hoekpunt opgeslagen, zoals nFlat)
    static Vector3[] FieldNormals(SoilPart part, Vector3[] fn)
    {
        var v = part.v; var tris = part.tris; var cells = part.cells; int nt = tris.Length / 3; Dictionary<long, List<int>> at = null; float[] area = null;
        long PK(Vector3 x) => unchecked(((long)Mathf.RoundToInt(x.x * 200f) * 73856093L) ^ ((long)Mathf.RoundToInt(x.y * 200f) * 19349663L) ^ ((long)Mathf.RoundToInt(x.z * 200f) * 83492791L));   // (op 5 mm)
        var soft = new Dictionary<int, bool>();
        for (int q = 0; q < nt; q++)
        {
            int a = tris[q * 3], b = tris[q * 3 + 1], c = tris[q * 3 + 2]; int ci = a < cells.Length ? cells[a] : -1; if (ci < 0) continue;
            if (!soft.TryGetValue(ci, out bool so)) { so = FieldSoft(ci); soft[ci] = so; } if (!so) continue;
            if (at == null) { at = new Dictionary<long, List<int>>(); area = new float[nt]; }
            area[q] = Vector3.Cross(v[b] - v[a], v[c] - v[a]).magnitude;
            foreach (int x in new[] { a, b, c }) { long k = PK(v[x]); if (!at.TryGetValue(k, out var l)) { l = new List<int>(6); at[k] = l; } l.Add(q); }
        }
        if (at == null) return null;
        var o = new Vector3[v.Length]; float cosF = Mathf.Cos(FieldCrease * Mathf.Deg2Rad);
        for (int q = 0; q < nt; q++)
        {
            int a0 = tris[q * 3]; int ci = a0 < cells.Length ? cells[a0] : -1; if (ci < 0 || !soft.TryGetValue(ci, out bool so) || !so) continue;
            Vector3 fq = fn[a0];
            for (int e = 0; e < 3; e++)   // het gemiddelde van de driehoeken op dit hoekpunt die minder dan FieldCrease van driehoek q afwijken
            {
                int vi = tris[q * 3 + e]; Vector3 sum = Vector3.zero;
                foreach (int t in at[PK(v[vi])]) { Vector3 ft = fn[tris[t * 3]]; if (Vector3.Dot(ft, fq) >= cosF) sum += ft * area[t]; }
                o[vi] = sum.sqrMagnitude > 1e-12f ? sum.normalized : fq;
            }
        }
        return o;
    }

    static void Flat(SoilPart part)
    {
        var v = part.v; var tris = part.tris; var nr = new Vector3[v.Length]; Dictionary<int, Vector3> memo = null;
        for (int t = 0; t + 2 < tris.Length; t += 3)
        {
            int a = tris[t], b = tris[t + 1], c = tris[t + 2]; Vector3 fn = Vector3.Cross(v[b] - v[a], v[c] - v[a]).normalized;
            if (Sliver(v[a], v[b], v[c]) && a < part.cells.Length)   // een splinter: belicht als de grond eromheen (geen lichte of donkere kras)
            {
                if (memo == null) memo = new Dictionary<int, Vector3>();
                Vector3 sn = SmoothNormal(part.cells[a], (v[a] + v[b] + v[c]) / 3f, memo); if (Vector3.Dot(sn, fn) > 0f) fn = sn;
            }
            nr[a] = nr[b] = nr[c] = fn;
        }
        // low poly langs een weg: elke driehoek het gemiddelde van de zachte normalen op zijn hoekpunten (nog steeds hoekig, maar zonder
        // splinters die anders belicht zijn dan de grond eromheen, melding 15)
        var fv = FieldNormals(part, nr); part.nField = fv;
        if (fv != null) for (int t = 0; t + 2 < tris.Length; t += 3) { int a = tris[t], b = tris[t + 1], c = tris[t + 2]; if (fv[a] == Vector3.zero) continue; Vector3 m = fv[a] + fv[b] + fv[c]; if (m.sqrMagnitude > 1e-12f) nr[a] = nr[b] = nr[c] = m.normalized; }
        part.nFlat = nr;
    }

    // ligt de rand a-b op de grens van vak i met een buur? Dan die buur (anders -1: een rand binnen het vak)
    static int BorderCell(int i, Vector3 a, Vector3 b)
    {
        Vector3 m = (a + b).normalized; var c = Planet.Cells[i]; float di = (c.dir - m).magnitude * Planet.R;
        int best = -1; float bd = float.MaxValue;
        foreach (int j in c.nb) { if (j < 0) continue; float dj = (Planet.Cells[j].dir - m).magnitude * Planet.R; if (dj < bd) { bd = dj; best = j; } }
        return bd - di < 0.4f ? best : -1;
    }

    // Zacht en Rond: eenmaal per deel de zachte normalen op de hoekpunten van de grove mesh, wat strak blijft en welke randen buigen.
    // Alles op een fijner punt (Rond) wordt hieruit gemengd: een rand hangt zo alleen af van zijn twee eindpunten (naadloos).
    static void Prepare(SoilPart part)
    {
        if (part.nFlat == null) Flat(part);
        var v = part.v; var tris = part.tris; var cells = part.cells; int nt = tris.Length / 3;
        var memo = new Dictionary<int, Vector3>(); var ok = new Dictionary<int, bool>();
        bool Ok(int ci) { if (!ok.TryGetValue(ci, out bool can)) { can = SmoothCell(ci); ok[ci] = can; } return can; }
        float cosC = Mathf.Cos(SmoothCrease * Mathf.Deg2Rad);
        var sn = new Vector3[v.Length]; var ns = (Vector3[])part.nFlat.Clone(); var state = new byte[nt]; var curve = new byte[nt];
        var fv = part.nField ?? (part.nField = FieldNormals(part, part.nFlat));
        for (int q = 0; q < nt; q++)
        {
            int a = tris[q * 3], b = tris[q * 3 + 1], c = tris[q * 3 + 2]; statTris++;
            int ci = a < cells.Length ? cells[a] : -1;
            if (fv != null && fv[a] != Vector3.zero)   // langs een weg: zachte normalen, rechte randen (Rond buigt dan niets: de vorm blijft)
            {
                sn[a] = ns[a] = fv[a]; sn[b] = ns[b] = fv[b]; sn[c] = ns[c] = fv[c]; state[q] = 1; curve[q] = 0; statSmooth++; continue;
            }
            if (ci < 0 || !Ok(ci)) { statCell++; continue; }
            Vector3 fn = part.nFlat[a]; Vector3 na = SmoothNormal(ci, v[a], memo), nb = SmoothNormal(ci, v[b], memo), nc = SmoothNormal(ci, v[c], memo);
            sn[a] = na; sn[b] = nb; sn[c] = nc;
            if (Vector3.Dot(na, fn) < cosC || Vector3.Dot(nb, fn) < cosC || Vector3.Dot(nc, fn) < cosC) { state[q] = 2; statCrease++; }   // een knik: eigen normaal
            else { state[q] = 1; ns[a] = na; ns[b] = nb; ns[c] = nc; statSmooth++; }
            // een rand blijft recht als het vak aan de overkant strak blijft (dan sluit die er precies op aan)
            bool Curve(Vector3 x, Vector3 y) { int j = BorderCell(ci, x, y); return j < 0 || Ok(j); }
            curve[q] = (byte)((Curve(v[a], v[b]) ? 1 : 0) | (Curve(v[b], v[c]) ? 2 : 0) | (Curve(v[c], v[a]) ? 4 : 0));
        }
        part.sn = sn; part.nSmooth = ns; part.state = state; part.curve = curve;
        part.col = part.coarse.colors; part.ext = new List<Vector4>(); part.coarse.GetUVs(0, part.ext);
    }

    // Rond: de fijne mesh van een deel. Plek op een gebogen vlak (PN-driehoek); normaal, kleur en extra gemengd uit de drie hoekpunten.
    static Mesh RoundMesh(SoilPart part)
    {
        var v = part.v; var tris = part.tris; var state = part.state; var sn = part.sn; var col = part.col; var ext = part.ext; int nt = tris.Length / 3;
        int RN = RoundN, per = (RN + 1) * (RN + 2) / 2, nv = 0, ni = 0;
        for (int q = 0; q < nt; q++) { if (state[q] == 0) { nv += 3; ni += 3; } else { nv += per; ni += RN * RN * 3; } }
        var V = new Vector3[nv]; var N = new Vector3[nv]; var C = new Color[nv]; var X = new Vector4[nv]; var T = new int[ni]; int o = 0, ti = 0;
        bool hasC = col != null && col.Length == v.Length, hasX = ext != null && ext.Count == v.Length;
        var idx = new int[RN + 1, RN + 1]; float inv = 1f / RN;
        for (int q = 0; q < nt; q++)
        {
            int ia = tris[q * 3], ib = tris[q * 3 + 1], ic = tris[q * 3 + 2]; Vector3 P1 = v[ia], P2 = v[ib], P3 = v[ic], fn = part.nFlat[ia];
            Color C1 = hasC ? col[ia] : default, C2 = hasC ? col[ib] : default, C3 = hasC ? col[ic] : default;
            Vector4 X1 = hasX ? ext[ia] : default, X2 = hasX ? ext[ib] : default, X3 = hasX ? ext[ic] : default;
            if (state[q] == 0)   // strak: de driehoek zoals hij is
            {
                V[o] = P1; V[o + 1] = P2; V[o + 2] = P3; N[o] = N[o + 1] = N[o + 2] = fn; C[o] = C1; C[o + 1] = C2; C[o + 2] = C3; X[o] = X1; X[o + 1] = X2; X[o + 2] = X3;
                T[ti++] = o; T[ti++] = o + 1; T[ti++] = o + 2; o += 3; continue;
            }
            Vector3 N1 = sn[ia], N2 = sn[ib], N3 = sn[ic]; bool smooth = state[q] == 1; byte cv = part.curve[q];
            bool c12 = (cv & 1) != 0, c23 = (cv & 2) != 0, c31 = (cv & 4) != 0;
            Vector3 b210 = (2f * P1 + P2 - (c12 ? Vector3.Dot(P2 - P1, N1) * N1 : Vector3.zero)) / 3f, b120 = (2f * P2 + P1 - (c12 ? Vector3.Dot(P1 - P2, N2) * N2 : Vector3.zero)) / 3f;
            Vector3 b021 = (2f * P2 + P3 - (c23 ? Vector3.Dot(P3 - P2, N2) * N2 : Vector3.zero)) / 3f, b012 = (2f * P3 + P2 - (c23 ? Vector3.Dot(P2 - P3, N3) * N3 : Vector3.zero)) / 3f;
            Vector3 b102 = (2f * P3 + P1 - (c31 ? Vector3.Dot(P1 - P3, N3) * N3 : Vector3.zero)) / 3f, b201 = (2f * P1 + P3 - (c31 ? Vector3.Dot(P3 - P1, N1) * N1 : Vector3.zero)) / 3f;
            Vector3 E = (b210 + b120 + b021 + b012 + b102 + b201) / 6f, Vm = (P1 + P2 + P3) / 3f, b111 = E + (E - Vm) * 0.5f;
            for (int r = 0; r <= RN; r++)
                for (int s = 0; s <= RN - r; s++)
                {
                    float u = r * inv, w2 = s * inv, w = 1f - u - w2;   // u bij P2, w2 bij P3, w bij P1
                    Vector3 p;
                    if (r == 0 && s == 0) p = P1; else if (r == RN) p = P2; else if (s == RN) p = P3;
                    else p = P1 * (w * w * w) + P2 * (u * u * u) + P3 * (w2 * w2 * w2) + b210 * (3f * w * w * u) + b120 * (3f * w * u * u) + b201 * (3f * w * w * w2)
                         + b021 * (3f * u * u * w2) + b102 * (3f * w * w2 * w2) + b012 * (3f * u * w2 * w2) + b111 * (6f * u * w2 * w);
                    idx[r, s] = o; V[o] = p; N[o] = smooth ? (N1 * w + N2 * u + N3 * w2).normalized : fn;
                    C[o] = C1 * w + C2 * u + C3 * w2; X[o] = X1 * w + X2 * u + X3 * w2; o++;
                }
            for (int r = 0; r < RN; r++)
                for (int s = 0; s < RN - r; s++)
                {
                    T[ti++] = idx[r, s]; T[ti++] = idx[r + 1, s]; T[ti++] = idx[r, s + 1];
                    if (s + 1 < RN - r) { T[ti++] = idx[r + 1, s]; T[ti++] = idx[r + 1, s + 1]; T[ti++] = idx[r, s + 1]; }
                }
        }
        var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, name = "bodem rond" };
        m.SetVertices(V); m.SetNormals(N); m.SetColors(C); m.SetUVs(0, X); m.SetTriangles(T, 0, false); m.RecalculateBounds();
        return m;
    }
}

// maakt per beeld een paar wachtende delen van de bodem rond (PlanetCity.SoilTick)
public class SoilRunner : MonoBehaviour { void Update() => PlanetCity.SoilTick(); }
