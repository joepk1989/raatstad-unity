using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// De stad op de planeet: per vak een stapel blokken; een vak waarvan het onderste blok "weg" is, is een wegvak.
// Wegvakken sluiten vanzelf op elkaar aan (asfalt van hart naar hart, stoep eromheen). Alles wordt in het
// assenstelsel van het vak gebouwd, zodat gebouwen overal recht op de bol staan. De bodem is één mesh per
// deel (1024 vakken); blokken en wegen worden per deel opnieuw gemaakt als er iets verandert.
[Serializable] public class PCellSave { public int i; public string[] items; }
[Serializable] public class PSaveData { public int m; public float r; public List<PCellSave> cells = new List<PCellSave>(); public List<int> links = new List<int>(); public List<int> style = new List<int>(), under = new List<int>(), oneway = new List<int>(), terr = new List<int>(); }   // paren: vak/stijl, vak/onder (1 meer, 2 water, 3 kuil), van/naar, vak/terreinhoogte in cm

public static partial class PlanetCity
{
    public static Dictionary<int, List<string>> Cells = new Dictionary<int, List<string>>();
    public const int ChunkN = 1024;
    static Transform root, groundRoot;
    static readonly Dictionary<int, Transform> chunks = new Dictionary<int, Transform>();
    static readonly HashSet<int> dirty = new HashSet<int>();

    public static void ResetAll() { Cells = new Dictionary<int, List<string>>(); Terr.Clear(); cnA = cnB = edN = null; Links.Clear(); partners.Clear(); Style.Clear(); Under.Clear(); OneWay.Clear(); DragStyle = 0; DragOneWay = false; Lights = true; chunks.Clear(); groundChunks.Clear(); dirty.Clear(); root = null; groundRoot = null; grid = null; GridOn = true; Slot = 1; roadPieces.Clear(); roadRoot = null; }
    public static bool IsRoad(int i) => Cells.TryGetValue(i, out var l) && l.Count > 0 && l[0] == "weg";
    public static bool IsPath(int i) => Cells.TryGetValue(i, out var l) && l.Count == 1 && l[0] == "pad";
    public static bool IsWalk(int i) => IsRoad(i) || IsPath(i);

    // ---------- terrein ----------
    // Eén hoogte per vak (0 … 30 m), als de low-poly stand van de webversie (07b-terrein.js): een vak is een waaier van driehoeken
    // met het midden op de hoogte van het vak en elk hoekpunt op de laagste van de drie vakken die daar samenkomen. Eén verhoogd
    // vak is zo een puntje, een groep een plateau met schuine randen. Een vak met iets erop is een vlak plateau op zijn eigen
    // hoogte met een stenen sokkel tot op het maaiveld; weg- en padvakken volgen de helling naar hun buren.
    public static readonly Dictionary<int, float> Terr = new Dictionary<int, float>();
    public const float TerrTop = 30f;
    public static float G(int i) => Terr.TryGetValue(i, out var h) ? h : 0f;
    public static bool Plateau(int i) => Cells.TryGetValue(i, out var l) && l.Count > 0 && !IsWalk(i);
    public static bool SetTerr(int i, float h)   // geeft true als de hoogte echt veranderde
    {
        h = Mathf.Clamp(Mathf.Round(h * 100f) / 100f, 0f, TerrTop); if (Mathf.Abs(h - G(i)) < 0.005f) return false;
        if (h > 0.004f) Terr[i] = h; else Terr.Remove(i);
        Touch(i); foreach (int p in Partners(i)) dirty.Add(p / ChunkN);   // een schuine weg helt ook naar het vak twee stappen verder
        return true;
    }
    // per hoek van een vak de twee buren die daar ook samenkomen, en per rand (hoek k → k+1) de buur aan de overkant
    static int[][] cnA, cnB, edN;
    static void Topo()
    {
        if (cnA != null && cnA.Length == Planet.Cells.Length) return;
        int N = Planet.Cells.Length; cnA = new int[N][]; cnB = new int[N][]; edN = new int[N][];
        for (int i = 0; i < N; i++)
        {
            var c = Planet.Cells[i]; int m = c.corners.Length; cnA[i] = new int[m]; cnB[i] = new int[m]; edN[i] = new int[m];
            for (int k = 0; k < m; k++)
            {
                int b1 = -1, b2 = -1; float d1 = -2f, d2 = -2f, de = -2f; Vector3 em = c.corners[k] + c.corners[(k + 1) % m];
                foreach (int j in c.nb)
                {
                    float d = Vector3.Dot(Planet.Cells[j].dir, c.corners[k]);
                    if (d > d1) { b2 = b1; d2 = d1; b1 = j; d1 = d; } else if (d > d2) { b2 = j; d2 = d; }
                    float dd = Vector3.Dot(Planet.Cells[j].dir, em); if (dd > de) { de = dd; edN[i][k] = j; }
                }
                cnA[i][k] = b1; cnB[i][k] = b2;
            }
        }
    }
    public static float CornerH(int i, int k) { Topo(); return Mathf.Min(G(i), Mathf.Min(G(cnA[i][k]), G(cnB[i][k]))); }
    public static float OwnCorner(int i, int k) => Plateau(i) ? G(i) : CornerH(i, k);   // zoals het vak zelf getekend wordt
    public static float MinCorner(int i) { float m = G(i); for (int k = 0; k < Planet.Cells[i].corners.Length; k++) m = Mathf.Min(m, CornerH(i, k)); return m; }
    // hoogte van het oppervlak van vak i (maaiveld, of de weg die er helt) onder een wereldpunt
    public static float SurfaceH(int i, Vector3 world) { var fr = new Frame(i); Vector3 d = world - fr.o; return G(i) + fr.H(Vector3.Dot(d, fr.e), Vector3.Dot(d, fr.n)); }

    // Verbindingen tussen weg- en padvakken. Twee wegvakken naast elkaar zijn alleen verbonden als ze in dezelfde lijn
    // gesleept zijn of met een klik aan elkaar gezet; zo kunnen twee wegen naast elkaar lopen zonder in elkaar over te gaan.
    public static readonly HashSet<long> Links = new HashSet<long>();
    static readonly Dictionary<int, List<int>> partners = new Dictionary<int, List<int>>();
    static readonly List<int> none = new List<int>();
    static long Key(int i, int j) => i < j ? ((long)i << 20) | (uint)j : ((long)j << 20) | (uint)i;
    public static void ClearLinks() { Links.Clear(); partners.Clear(); Style.Clear(); Under.Clear(); OneWay.Clear(); }
    public static bool Linked(int i, int j) => Links.Contains(Key(i, j));
    // Per wegvak: stijl (1 = brede weg, 2 = laan; ontbreekt = gewone weg), brug (wat eronder ligt: meer, water of kuil) en
    // eenrichtingsverkeer per verbinding (alleen van i naar j)
    public static readonly Dictionary<int, byte> Style = new Dictionary<int, byte>();
    public static readonly Dictionary<int, string> Under = new Dictionary<int, string>();
    public static readonly HashSet<long> OneWay = new HashSet<long>();
    public static byte DragStyle; public static bool DragOneWay; public static bool Lights = true;   // instellingen voor wat je nu sleept
    static long DKey(int i, int j) => ((long)i << 20) | (uint)j;
    public static int Flow(int i, int j) => OneWay.Contains(DKey(i, j)) ? 1 : OneWay.Contains(DKey(j, i)) ? -1 : 0;   // +1: alleen van i naar j, -1: alleen van j naar i
    public static bool CanDrive(int i, int j) => Flow(i, j) >= 0;
    public static byte StyleOf(int i) => Style.TryGetValue(i, out var s) ? s : (byte)0;
    static void SetStyle(int i) { if (DragStyle == 0) Style.Remove(i); else Style[i] = DragStyle; }
    // de vakken waarmee i verbonden is (buren, of vakken op afstand 2 recht door de hoek: dan loopt de weg over twee halve vakken)
    public static List<int> Partners(int i) => partners.TryGetValue(i, out var l) ? l : none;
    public static bool Adjacent2(int i, int j) => System.Array.IndexOf(Planet.Cells[i].nb, j) >= 0 || System.Array.IndexOf(Planet.Cells[i].across, j) >= 0;
    public static void Link(int i, int j)
    {
        if (i == j || !Adjacent2(i, j) || !Links.Add(Key(i, j))) return;
        if (!partners.TryGetValue(i, out var a)) partners[i] = a = new List<int>(6); a.Add(j);
        if (!partners.TryGetValue(j, out var b)) partners[j] = b = new List<int>(6); b.Add(i);
    }
    public static void Unlink(int i)
    {
        if (!partners.TryGetValue(i, out var a)) return;
        foreach (int j in a) { Links.Remove(Key(i, j)); OneWay.Remove(DKey(i, j)); OneWay.Remove(DKey(j, i)); if (partners.TryGetValue(j, out var b)) b.Remove(i); if (!IsNeighbour(i, j)) ClearHalves(i, j); }
        partners.Remove(i);
    }
    static bool IsNeighbour(int i, int j) => System.Array.IndexOf(Planet.Cells[i].nb, j) >= 0;
    // de twee vakken tussen i en j (afstand 2): de gemeenschappelijke buren
    public static List<int> Between(int i, int j) { var l = new List<int>(2); foreach (int x in Planet.Cells[i].nb) if (IsNeighbour(j, x)) l.Add(x); return l; }
    // Het bezette deel van een half vak: de kant naar de weg toe, tot de middenlijn van het vak (de weg loopt langs de rand tussen twee halve vakken)
    public static List<Vector3> HalfDirs(int i, Vector3 n, bool free = false)   // free: de andere helft (de vrije kant, van de weg af)
    {
        var c = Planet.Cells[i]; float sc = Vector3.Dot(c.dir, n), sg = sc < 0f ? -1f : 1f, lim = Mathf.Abs(sc), fs = free ? -1f : 1f; var o = new List<Vector3>(); int m = c.corners.Length;
        for (int k = 0; k < m; k++)
        {
            Vector3 a = c.corners[k], b = c.corners[(k + 1) % m]; float fa = fs * (sg * Vector3.Dot(a, n) - lim), fb = fs * (sg * Vector3.Dot(b, n) - lim);
            if (fa <= 0f) o.Add(a);
            if ((fa <= 0f) != (fb <= 0f)) o.Add(Vector3.Lerp(a, b, fa / (fa - fb)).normalized);
        }
        return o;
    }
    // het vlak van de weg die over dit halve vak loopt (normaal), uit de verbonden vakken ernaast
    public static bool HalfNormal(int i, out Vector3 n)
    {
        n = Vector3.zero;
        foreach (int a in Planet.Cells[i].nb) { if (!IsWalk(a)) continue; foreach (int b in Partners(a)) { if (b == a || IsNeighbour(a, b) || !Between(a, b).Contains(i)) continue; n = Vector3.Cross(Planet.Cells[a].dir, Planet.Cells[b].dir).normalized; return true; } }
        return false;
    }
    // een half vak: onderste "blok" is "wegdeel" (de weg bezet de ene helft); daarboven mogen blokken staan, op de vrije helft
    public static bool IsHalf(int i) => Cells.TryGetValue(i, out var l) && l.Count >= 1 && l[0] == "wegdeel";
    static void ClearHalves(int i, int j)
    {
        foreach (int h in Between(i, j)) if (IsHalf(h)) { var l = Cells[h]; if (l.Count > 1) l.RemoveAt(0); else Cells.Remove(h); Touch(h); }   // met bebouwing erop: wordt een gewoon vak
    }
    static void MarkHalf(int h)
    {
        if (!Cells.TryGetValue(h, out var l) || l.Count == 0) { Cells[h] = new List<string> { "wegdeel" }; Touch(h); return; }
        string b = l[0]; if (Buildable(b)) { l.Insert(0, "wegdeel"); Touch(h); }   // bestaande bebouwing schuift naar de vrije helft
    }
    static bool Buildable(string t) => t == "woning" || t == "kantoor" || t == "park" || t == "dak" || t == "blok" || t == "winkel" || t == "plein" || t == "toren";
    public static bool LinkedRoad(int i, int j) => IsRoad(j) && Linked(i, j);
    public static bool LinkedWalk(int i, int j) => IsWalk(j) && Linked(i, j);
    public static int LinkCount(int i) { int n = 0; foreach (int j in Partners(i)) if (IsWalk(j)) n++; return n; }
    // een heel vak leegmaken (rechtermuisknop)
    public static bool Clear(int i)
    {
        if (!Cells.TryGetValue(i, out var l)) return false;
        if (IsHalf(i)) { if (l.Count <= 1) return false; l.RemoveRange(1, l.Count - 1); Touch(i); return true; }   // alleen wat op de vrije helft staat
        if (Under.TryGetValue(i, out var under)) { Cells[i] = new List<string> { under }; Under.Remove(i); Style.Remove(i); Unlink(i); Touch(i); return true; }   // een brug: het water of de kuil komt terug
        Cells.Remove(i); Style.Remove(i); Unlink(i); Touch(i); return true;
    }
    public static float StackHeight(int i) { float h = 0f; if (Cells.TryGetValue(i, out var l)) foreach (var t in l) h += Blocks.Height(t); return h; }
    public static int RoadCount() { int n = 0; foreach (var kv in Cells) if (kv.Value.Count > 0 && kv.Value[0] == "weg") n++; return n; }

    public static Dictionary<int, List<string>> Clone(Dictionary<int, List<string>> src)
    {
        var d = new Dictionary<int, List<string>>(); foreach (var kv in src) d[kv.Key] = new List<string>(kv.Value); return d;
    }

    // plaatsen; geeft een melding terug als het niet kan (anders null)
    public static string Place(int i, string type, bool autoLink = true)
    {
        if (!Cells.TryGetValue(i, out var l)) { l = new List<string>(); Cells[i] = l; }
        if (type == "weg")
        {
            if (l.Count == 1 && l[0] == "wegdeel") l.Clear();
            if (l.Count == 1 && (l[0] == "meer" || l[0] == "water" || l[0] == "kuil")) { Under[i] = l[0]; l[0] = "weg"; }   // een brug over water of een uitgraving
            if (l.Count > 0 && !(l.Count == 1 && l[0] == "weg")) return "Eerst het vak leegmaken (rechtermuisknop) voor een weg";
            if (l.Count == 0) l.Add("weg");
            SetStyle(i); if (autoLink) foreach (int j in Planet.Cells[i].nb) if (IsWalk(j)) Link(i, j);
            Touch(i); return null;
        }
        if (type == "pad" && l.Count == 0) { l.Add("pad"); if (autoLink) foreach (int j in Planet.Cells[i].nb) if (IsWalk(j)) Link(i, j); Touch(i); return null; }
        if (type == "pad" && l.Count == 1 && l[0] == "pad") { if (autoLink) foreach (int j in Planet.Cells[i].nb) if (IsWalk(j)) Link(i, j); Touch(i); return null; }
        if (l.Count > 0 && l[0] == "weg") return "Op een weg kun je niet bouwen; haal de weg eerst weg (rechtermuisknop)";
        if (l.Count > 0 && l[0] == "wegdeel" && !Buildable(type)) return "Hier loopt een weg overheen: op de vrije helft passen alleen gebouwen en parken";
        if (l.Count >= 8) return "Hoger kan niet";
        if (l.Count > 0 && (l[l.Count - 1] == "toren" || l[l.Count - 1] == "molen") && type != "water" && type != "meer") return "Op een " + l[l.Count - 1] + " kun je niets meer zetten";
        if ((type == "water" || type == "meer") && l.Count > 0) l.Clear();
        if (l.Count > 0 && (l[l.Count - 1] == "water" || l[l.Count - 1] == "meer")) l.Clear();
        l.Add(type); Touch(i); return null;
    }

    // Een gesleepte lijn: de vakken worden weg (of pad) en alleen onderling verbonden, in volgorde
    public static int PlaceLine(List<int> line, string type)
    {
        int ok = 0;
        for (int k = 0; k < line.Count; k++) { if (Place(line[k], type, false) == null) ok++; }
        for (int k = 1; k < line.Count; k++)
        {
            int a = line[k - 1], b = line[k]; if (!IsWalk(a) || !IsWalk(b)) continue;
            Link(a, b);
            if (IsRoad(a) && IsRoad(b))   // eenrichting in de sleeprichting, of weer tweerichting
            {
                OneWay.Remove(DKey(a, b)); OneWay.Remove(DKey(b, a)); if (DragOneWay) OneWay.Add(DKey(a, b));
            }
            if (!IsNeighbour(a, b)) foreach (int h in Between(a, b)) MarkHalf(h);
        }
        foreach (int i in line) Touch(i);
        return ok;
    }

    public static bool RemoveTop(int i)
    {
        if (!Cells.TryGetValue(i, out var l) || l.Count == 0) return false;
        if (IsHalf(i) && l.Count <= 1) return false;
        if (Under.TryGetValue(i, out var under)) { Cells[i] = new List<string> { under }; Under.Remove(i); Style.Remove(i); Unlink(i); Touch(i); return true; }
        l.RemoveAt(l.Count - 1); if (l.Count == 0) { Cells.Remove(i); Style.Remove(i); Unlink(i); } Touch(i); return true;
    }

    public static void Touch(int i) { dirty.Add(i / ChunkN); foreach (int j in Planet.Cells[i].nb) dirty.Add(j / ChunkN); }
    public static void TouchAll() { for (int c = 0; c * ChunkN < Planet.Cells.Length; c++) dirty.Add(c); }

    public static int Slot = 1;   // drie opslagslots: planeet1.json, planeet2.json, planeet3.json
    static string FilePath => Path.Combine(Application.persistentDataPath, "planeet" + Slot + ".json");
    public static bool SlotExists(int n) => File.Exists(Path.Combine(Application.persistentDataPath, "planeet" + n + ".json"));

    public static void Save()
    {
        try
        {
            var sd = new PSaveData { m = Planet.M, r = Planet.R };
            foreach (var kv in Cells) if (kv.Value.Count > 0) sd.cells.Add(new PCellSave { i = kv.Key, items = kv.Value.ToArray() });
            foreach (long k in Links) { sd.links.Add((int)(k >> 20)); sd.links.Add((int)(k & 0xFFFFF)); }
            foreach (var kv in Style) if (IsRoad(kv.Key)) { sd.style.Add(kv.Key); sd.style.Add(kv.Value); }
            foreach (var kv in Under) { sd.under.Add(kv.Key); sd.under.Add(kv.Value == "meer" ? 1 : kv.Value == "water" ? 2 : 3); }
            foreach (long k in OneWay) { sd.oneway.Add((int)(k >> 20)); sd.oneway.Add((int)(k & 0xFFFFF)); }
            foreach (var kv in Terr) { sd.terr.Add(kv.Key); sd.terr.Add(Mathf.RoundToInt(kv.Value * 100f)); }
            File.WriteAllText(FilePath, JsonUtility.ToJson(sd));
        }
        catch (Exception e) { Debug.LogWarning("Opslaan mislukt: " + e.Message); }
    }

    public static bool Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return false;
            var sd = JsonUtility.FromJson<PSaveData>(File.ReadAllText(FilePath)); if (sd == null || sd.m != Planet.M) return false;
            Cells = new Dictionary<int, List<string>>();
            foreach (var c in sd.cells) if (c.items != null && c.items.Length > 0 && c.i >= 0 && c.i < Planet.Cells.Length) Cells[c.i] = new List<string>(c.items);
            ClearLinks();
            if (sd.links != null && sd.links.Count > 0) { for (int k = 0; k + 1 < sd.links.Count; k += 2) Link(sd.links[k], sd.links[k + 1]); }
            else foreach (var kv in Cells) if (IsWalk(kv.Key)) foreach (int j in Planet.Cells[kv.Key].nb) if (IsWalk(j)) Link(kv.Key, j);   // oude opslag: alles naast elkaar verbonden
            if (sd.style != null) for (int k = 0; k + 1 < sd.style.Count; k += 2) Style[sd.style[k]] = (byte)sd.style[k + 1];
            if (sd.under != null) for (int k = 0; k + 1 < sd.under.Count; k += 2) Under[sd.under[k]] = sd.under[k + 1] == 1 ? "meer" : sd.under[k + 1] == 2 ? "water" : "kuil";
            if (sd.oneway != null) for (int k = 0; k + 1 < sd.oneway.Count; k += 2) OneWay.Add(DKey(sd.oneway[k], sd.oneway[k + 1]));
            Terr.Clear();
            if (sd.terr != null) for (int k = 0; k + 1 < sd.terr.Count; k += 2) if (sd.terr[k] >= 0 && sd.terr[k] < Planet.Cells.Length && sd.terr[k + 1] > 0) Terr[sd.terr[k]] = Mathf.Min(TerrTop, sd.terr[k + 1] / 100f);
            // oude heuvelblokken onderaan een stapel worden terreinhoogte (3 m elk), net als terrMigrate in de webversie
            foreach (int k in new List<int>(Cells.Keys))
            {
                var l = Cells[k]; int n = 0; while (n < l.Count && l[n] == "heuvel") n++;
                if (n == 0) continue;
                Terr[k] = Mathf.Min(TerrTop, G(k) + n * 3f); l.RemoveRange(0, n); if (l.Count == 0) Cells.Remove(k);
            }
            TouchAll(); RebuildGrid(); return true;
        }
        catch (Exception e) { Debug.LogWarning("Laden mislukt: " + e.Message); return false; }
    }

    // Een rechte lijn van vakken van a naar b: de richting wordt op een veelvoud van 30° gelegd (zoals in de browser) en de lijn
    // volgt de grootcirkel; elke stap kiest de buur die het dichtst bij die cirkel blijft. In de 6 buurrichtingen is dat een
    // kaarsrechte rij, in de 6 tussenrichtingen een lichte trap.
    public static List<int> LinePlan(int a, int b)
    {
        var A = Planet.Cells[a]; Vector3 da = A.dir, db = Planet.Cells[b].dir;
        Vector3 t = db - da; t -= da * Vector3.Dot(t, da); if (t.sqrMagnitude < 1e-8f) return null; t.Normalize();
        // de 12 richtingen: naar de buren (6) en daar precies tussenin (6); op een vijfhoek 5 + 5
        var dirs = new List<Vector3>();
        foreach (int j in A.nb) { Vector3 d = Planet.Cells[j].dir - da; d -= da * Vector3.Dot(d, da); dirs.Add(d.normalized); }
        foreach (int j in A.across) { if (j < 0) continue; Vector3 d = Planet.Cells[j].dir - da; d -= da * Vector3.Dot(d, da); dirs.Add(d.normalized); }   // door de hoek: over twee halve vakken
        Vector3 ray = dirs[0]; float bestDot = -2f; foreach (var d in dirs) { float v = Vector3.Dot(d, t); if (v > bestDot) { bestDot = v; ray = d; } }
        Vector3 n = Vector3.Cross(da, ray).normalized;
        float total = Mathf.Acos(Mathf.Clamp(Vector3.Dot(da, db), -1f, 1f));
        var plan = new List<int> { a }; int cur = a;
        for (int step = 0; step < 120; step++)
        {
            Vector3 dc = Planet.Cells[cur].dir, fwd = Vector3.Cross(n, dc).normalized; int best = -1; float bs = -1e9f;
            var cc = Planet.Cells[cur]; var cand = new List<int>(cc.nb); foreach (int x in cc.across) if (x >= 0) cand.Add(x);
            foreach (int j in cand)
            {
                Vector3 dj = Planet.Cells[j].dir; float prog = Vector3.Dot(dj - dc, fwd); if (prog <= 1e-6f) continue;
                float off = Mathf.Abs(Vector3.Dot(dj, n)) * Planet.R;   // afstand tot de grootcirkel in meters
                float sc = -off * 4f + prog * 0.5f; if (sc > bs) { bs = sc; best = j; }   // vooral: op de lijn blijven
            }
            if (best < 0) break;
            float along = Mathf.Acos(Mathf.Clamp(Vector3.Dot(da, Planet.Cells[best].dir), -1f, 1f));
            if (along > total + 0.4f * 15.6f / Planet.R) break;
            plan.Add(best); cur = best; if (cur == b) break;
        }
        return plan;
    }


    // Een voorbeeldstadje rond vak s: een ringweg, twee hoofdwegen erdoorheen, huizen en kantoren langs de wegen,
    // parken, een meertje en een paar paden. Vast bepaald (zelfde stad bij elke keer).
    public static void Sample(int s)
    {
        byte keepStyle = DragStyle; bool keepOne = DragOneWay; DragStyle = 0; DragOneWay = false;   // het voorbeeld heeft gewone tweerichtingswegen
        var S = Planet.Cells[s]; var rnd = new System.Random(7);
        // afstanden (in vakken) vanaf s
        var dist = new Dictionary<int, int> { { s, 0 } }; var q = new Queue<int>(); q.Enqueue(s);
        while (q.Count > 0) { int c = q.Dequeue(); if (dist[c] >= 7) continue; foreach (int j in Planet.Cells[c].nb) if (!dist.ContainsKey(j)) { dist[j] = dist[c] + 1; q.Enqueue(j); } }
        System.Func<Vector3, int> at = dir => Planet.Nearest(dir * Planet.R);
        System.Func<float, float, int> cellAt = (ang, r) => at((S.dir * Mathf.Cos(r / Planet.R) + (S.east * Mathf.Cos(ang) + S.north * Mathf.Sin(ang)) * Mathf.Sin(r / Planet.R)).normalized);
        // twee hoofdwegen door het midden: in de richting van buur 0 en haaks daarop
        Vector3 n0 = Planet.Cells[S.nb[0]].dir - S.dir; n0 -= S.dir * Vector3.Dot(n0, S.dir); float a0 = Mathf.Atan2(Vector3.Dot(n0, S.north), Vector3.Dot(n0, S.east));
        foreach (float ang in new[] { a0, a0 + Mathf.PI / 2f })
        {
            var line = LinePlan(cellAt(ang + Mathf.PI, 6.2f * 15.6f), cellAt(ang, 6.2f * 15.6f)); if (line != null) PlaceLine(line, "weg");
        }
        // ringweg op afstand 4: de vakken op afstand 4, rondom gesorteerd, als gesloten lijn
        var ring = new List<int>(); foreach (var kv in dist) if (kv.Value == 4) ring.Add(kv.Key);
        ring.Sort((x, y) => Ang(S, x).CompareTo(Ang(S, y)));
        var ringLine = new List<int>(ring); ringLine.Add(ring[0]); PlaceLine(ringLine, "weg");
        // bebouwing: langs de wegen huizen (buiten) en kantoren (binnen de ring), parken in de rest, een meertje aan één kant
        float lakeAng = a0 + Mathf.PI / 4f; int lakeC = cellAt(lakeAng, 2.3f * 15.6f);
        foreach (var kv in dist)
        {
            int c = kv.Key, d = kv.Value; if (Cells.ContainsKey(c) || d == 0) continue;
            bool nearRoad = false; foreach (int j in Planet.Cells[c].nb) if (IsRoad(j)) { nearRoad = true; break; }
            float lakeD = Vector3.Distance(Planet.Cells[c].dir, Planet.Cells[lakeC].dir) * Planet.R;
            if (lakeD < 19f) { Place(c, "meer"); continue; }
            if (d > 6) continue;
            if (nearRoad)
            {
                if (d <= 2) { int fl = 2 + rnd.Next(0, 2); for (int f = 0; f < fl; f++) Place(c, "kantoor"); }
                else if (d <= 5 && rnd.NextDouble() < 0.8) { Place(c, "woning"); if (rnd.NextDouble() < 0.3) Place(c, "woning"); }
                else if (rnd.NextDouble() < 0.5) Place(c, "park");
            }
            else if (d <= 5 && rnd.NextDouble() < 0.55) Place(c, "park");
        }
        // een paar paden van de ring naar binnen door het groen
        for (int k = 0; k < 3; k++)
        {
            float ang = a0 + Mathf.PI / 3f + k * Mathf.PI * 2f / 3f; var pl = LinePlan(cellAt(ang, 3.6f * 15.6f), cellAt(ang, 1.4f * 15.6f));
            if (pl != null) { var free = new List<int>(); foreach (int c in pl) if (!Cells.ContainsKey(c) || IsPath(c) || (Cells[c].Count == 1 && Cells[c][0] == "park")) { if (Cells.ContainsKey(c) && Cells[c][0] == "park") Cells.Remove(c); free.Add(c); } PlaceLine(free, "pad"); foreach (int c in free) foreach (int j in Planet.Cells[c].nb) if (IsRoad(j)) Link(c, j); }
        }
        DragStyle = keepStyle; DragOneWay = keepOne; TouchAll();
    }
    // één fontein per plein: alleen op het pleinvak met het laagste nummer van een aaneengesloten groep pleinvakken
    static bool IsPlein(int i) => Cells.TryGetValue(i, out var l) && l.Count > 0 && l[0] == "plein";
    static bool FountainHere(int i)
    {
        var seen = new HashSet<int> { i }; var q = new Queue<int>(); q.Enqueue(i);
        while (q.Count > 0 && seen.Count < 200) { int c = q.Dequeue(); if (c < i) return false; foreach (int j in Planet.Cells[c].nb) if (IsPlein(j) && seen.Add(j)) q.Enqueue(j); }
        return true;
    }
    static float Ang(Planet.Cell S, int c) { Vector3 d = Planet.Cells[c].dir - S.dir; return Mathf.Atan2(Vector3.Dot(d, S.north), Vector3.Dot(d, S.east)); }

    // ---------- bodem ----------
    static readonly Dictionary<int, List<GameObject>> groundChunks = new Dictionary<int, List<GameObject>>();
    public static void Init(Transform parent)
    {
        root = new GameObject("Planeetstad").transform; root.SetParent(parent, false);
        groundRoot = new GameObject("Bodem").transform; groundRoot.SetParent(parent, false);
        chunks.Clear(); groundChunks.Clear();
        for (int c0 = 0; c0 < Planet.Cells.Length; c0 += ChunkN) BuildGround(c0 / ChunkN);
        BuildGrid(parent); RoadsInit(parent);
    }

    // Rasterlijnen langs alle vakranden (één lijnenmesh, 6 cm boven de bodem). G = aan/uit; in wandelmodus verborgen.
    static GameObject grid; public static bool GridOn = true;
    static void BuildGrid(Transform parent)
    {
        grid = new GameObject("Raster"); grid.transform.SetParent(parent, false); grid.AddComponent<MeshFilter>().sharedMesh = GridMesh();
        var mr = grid.AddComponent<MeshRenderer>(); mr.sharedMaterial = Raatstad.MatGrid; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
    }
    // elk vak tekent zijn eigen rand op zijn eigen hoeken (een plateau op zijn hoogte, het maaiveld op de hoogte van de hoekpunten)
    static Mesh GridMesh()
    {
        var v = new List<Vector3>(); var idx = new List<int>();
        for (int i = 0; i < Planet.Cells.Length; i++)
        {
            var c = Planet.Cells[i]; int m = c.corners.Length;
            for (int k = 0; k < m; k++) { idx.Add(v.Count); v.Add(c.corners[k] * (Planet.R + OwnCorner(i, k) + 0.06f)); idx.Add(v.Count); v.Add(c.corners[(k + 1) % m] * (Planet.R + OwnCorner(i, (k + 1) % m) + 0.06f)); }
        }
        var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; mesh.SetVertices(v); mesh.SetIndices(idx, MeshTopology.Lines, 0); mesh.RecalculateBounds(); return mesh;
    }
    public static void RebuildGrid()
    {
        if (grid == null) return; var mf = grid.GetComponent<MeshFilter>(); var old = mf.sharedMesh; mf.sharedMesh = GridMesh(); if (old != null) UnityEngine.Object.Destroy(old);
    }
    public static void ShowGrid(bool on) { if (grid != null) grid.SetActive(on); }

    // De bodem van één deel. Vakken met een meer of een uitgraving krijgen geen bodem (die zit er anders doorheen).
    static void BuildGround(int ch)
    {
        if (groundChunks.TryGetValue(ch, out var olds)) { foreach (var g in olds) if (g != null) UnityEngine.Object.Destroy(g); }
        var made = new List<GameObject>(); groundChunks[ch] = made;
        int n = Planet.Cells.Length, c0 = ch * ChunkN;
        {
            // De bodem per vak; de kleur loopt van gewoon gras (regelmatige zeshoek) naar fel oranje (het meest vervormd, naast een vijfhoek)
            const int Steps = 8; var ms = new MeshBuilder[Steps]; for (int k = 0; k < Steps; k++) ms[k] = new MeshBuilder();
            var pm = new MeshBuilder();   // de vijfhoeken zelf
            var sm = new MeshBuilder();   // de startplekken
            var polar = new Dictionary<string, MeshBuilder>();   // de polen, per kleur
            System.Func<string, MeshBuilder> PM = k => { if (!polar.TryGetValue(k, out var x)) { x = new MeshBuilder(); polar[k] = x; } return x; };
            var terrB = new Dictionary<string, MeshBuilder>();   // verhoogd terrein per hoogteband, aarde op steile flanken, sokkels
            System.Func<string, MeshBuilder> TB = k => { if (!terrB.TryGetValue(k, out var x)) { x = new MeshBuilder(); terrB[k] = x; } return x; };
            Topo();
            for (int i = c0; i < Mathf.Min(n, c0 + ChunkN); i++)
            {
                var c = Planet.Cells[i]; int m = c.corners.Length; bool pent = m == 5;
                bool cut = (Cells.TryGetValue(i, out var stack) && stack.Count > 0 && (stack[0] == "meer" || stack[0] == "kuil"))
                        || (Under.TryGetValue(i, out var und) && (und == "meer" || und == "kuil"));   // meer, uitgraving of brug daarover: geen bodem
                float g = G(i), RG = Planet.R + g; bool plat = Plateau(i);
                // hoogte van het midden en de hoeken; onder een weg of pad een kom tot de laagste hoek, zodat het maaiveld nooit door de hellende weg steekt
                var hk = new float[m]; for (int k = 0; k < m; k++) hk[k] = plat ? g : CornerH(i, k);
                float hc = g; if (IsWalk(i)) foreach (float x in hk) hc = Mathf.Min(hc, x);
                float kk = Mathf.Pow(Mathf.Clamp01((c.distort - 0.04f) / 0.125f), 1.4f);
                bool start = System.Array.IndexOf(Planet.Starts, i) >= 0, pole = Planet.IsPole(i), special = pole || pent || start;
                var mb0 = pole ? PM("ijs") : pent ? pm : start ? sm : ms[Mathf.Clamp(Mathf.RoundToInt(kk * (Steps - 1)), 0, Steps - 1)];
                if (!cut)
                {
                    Vector3 mid = c.dir * (Planet.R + hc);
                    for (int k = 0; k < m; k++)
                    {
                        float ha = hk[k], hb = hk[(k + 1) % m]; Vector3 A = c.corners[k] * (Planet.R + ha), B = c.corners[(k + 1) % m] * (Planet.R + hb);
                        var mb = mb0;
                        if (!special && Mathf.Max(hc, Mathf.Max(ha, hb)) > 0.03f)   // verhoogd: kleur naar de hoogte, aarde waar het steiler is dan zo'n 45°
                        {
                            float up = Mathf.Abs(Vector3.Dot(Vector3.Cross(A - mid, B - mid).normalized, c.dir));
                            mb = up < 0.72f ? TB("aarde") : TB("t" + Mathf.Clamp(Mathf.FloorToInt((hc + ha + hb) / 9f), 0, 9));
                        }
                        mb.Tri(mid, A, B, c.dir);
                    }
                }
                // sokkel: een plateau boven het maaiveld krijgt stenen wanden tot op het maaiveld ernaast (tussen twee plateaus: een keermuur)
                if ((plat || cut) && g > 0.01f)
                    for (int k = 0; k < m; k++)
                    {
                        int j = edN[i][k]; if (Plateau(j) && G(j) >= g - 0.01f) continue;
                        float ha = CornerH(i, k), hb = CornerH(i, (k + 1) % m); if (g - ha < 0.01f && g - hb < 0.01f) continue;
                        Vector3 a = c.corners[k], b = c.corners[(k + 1) % m];
                        TB("sokkel").Quad(a * RG, b * RG, b * (Planet.R + hb), a * (Planet.R + ha), (a + b) * 0.5f - c.dir);
                    }
                if (pole) PoleMarker(PM, c, g);
                if (start)   // startplek: lichtblauw vak met een witte paal van 30 m en een vlaggetje
                {
                    Vector3 top = c.dir * (RG + 30f);
                    for (int k = 0; k < 6; k++) { Vector3 a = Vector3.Lerp(c.corners[k], c.dir, 0.93f) * (RG + 0.3f), b = Vector3.Lerp(c.corners[(k + 1) % 6], c.dir, 0.93f) * (RG + 0.3f); sm.Tri(a, b, top, (a + b) * 0.5f - c.dir * RG + c.dir * 0.05f); }
                    Vector3 fl = c.dir * (RG + 27f) + c.east * 6f, fb = c.dir * (RG + 24f);
                    sm.Tri(top, fl, fb, c.north); sm.Tri(top, fb, fl, -c.north);
                    for (int k = 0; k < 6; k++) { Vector3 a = c.corners[k], b = c.corners[(k + 1) % 6]; Vector3 am = (a + b) * 0.5f; Vector3 ai = Vector3.Lerp(a, c.dir, 0.08f), bi = Vector3.Lerp(b, c.dir, 0.08f); sm.Quad(a * (Planet.R + hk[k]), b * (Planet.R + hk[(k + 1) % 6]), bi * (RG + 0.25f), ai * (RG + 0.25f), am - c.dir); }
                }
                if (pent)   // baken: een slanke oranje punt van 60 m, zodat je de vijfhoeken ook vanuit de ruimte vindt
                {
                    Vector3 apex = c.dir * (RG + 60f);
                    for (int k = 0; k < 5; k++) { Vector3 a = Vector3.Lerp(c.corners[k], c.dir, 0.7f) * (RG + 0.3f), b = Vector3.Lerp(c.corners[(k + 1) % 5], c.dir, 0.7f) * (RG + 0.3f); pm.Tri(a, b, apex, (a + b) * 0.5f - c.dir * RG + c.dir * 0.05f); }
                }
                if (pent)   // opstaand randje rondom, zodat de vijfhoek ook van dichtbij opvalt
                    for (int k = 0; k < 5; k++) { Vector3 a = c.corners[k], b = c.corners[(k + 1) % 5]; Vector3 am = (a + b) * 0.5f; Vector3 ai = Vector3.Lerp(a, c.dir, 0.08f), bi = Vector3.Lerp(b, c.dir, 0.08f); pm.Quad(a * (Planet.R + hk[k]), b * (Planet.R + hk[(k + 1) % 5]), bi * (RG + 0.25f), ai * (RG + 0.25f), am - c.dir); }
            }
            foreach (var kv in terrB)
            {
                var tg = new GameObject("Terrein " + kv.Key + " " + (c0 / ChunkN)); tg.transform.SetParent(groundRoot, false); made.Add(tg); tg.layer = Layers.Bodem;
                var tmesh = kv.Value.Build(); tg.AddComponent<MeshFilter>().sharedMesh = tmesh; tg.AddComponent<MeshRenderer>().sharedMaterial = Mats.Get("p_terr_" + kv.Key, TerrColor(kv.Key));
                tg.AddComponent<MeshCollider>().sharedMesh = tmesh;
            }
            if (pm.v.Count > 0)
            {
                var pg = new GameObject("Vijfhoeken " + (c0 / ChunkN)); pg.transform.SetParent(groundRoot, false); made.Add(pg); pg.layer = Layers.Bodem;
                var pmesh = pm.Build(); pg.AddComponent<MeshFilter>().sharedMesh = pmesh; pg.AddComponent<MeshRenderer>().sharedMaterial = Mats.Get("p_vijfhoek", new Color(1f, 0.5f, 0.1f));
                pg.AddComponent<MeshCollider>().sharedMesh = pmesh;
            }
            if (sm.v.Count > 0)
            {
                var sg = new GameObject("Startplekken " + (c0 / ChunkN)); sg.transform.SetParent(groundRoot, false); made.Add(sg); sg.layer = Layers.Bodem;
                var smesh = sm.Build(); sg.AddComponent<MeshFilter>().sharedMesh = smesh; sg.AddComponent<MeshRenderer>().sharedMaterial = Mats.Get("p_start", new Color(0.55f, 0.8f, 1f));
                sg.AddComponent<MeshCollider>().sharedMesh = smesh;
            }
            foreach (var kv in polar)
            {
                var g = new GameObject("Pool " + kv.Key); g.transform.SetParent(groundRoot, false); made.Add(g); g.layer = Layers.Bodem;
                var mesh = kv.Value.Build(); g.AddComponent<MeshFilter>().sharedMesh = mesh; g.AddComponent<MeshRenderer>().sharedMaterial = Mats.Get("p_pool_" + kv.Key, PoleColor(kv.Key));
                if (!kv.Key.StartsWith("vlag")) g.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
            Color grass = new Color(0.64f, 0.78f, 0.45f), hot = new Color(1f, 0.6f, 0.2f);
            for (int k = 0; k < Steps; k++)
            {
                if (ms[k].v.Count == 0) continue;
                var go = new GameObject("Bodem " + (c0 / ChunkN) + " " + k); go.transform.SetParent(groundRoot, false); made.Add(go); go.layer = Layers.Bodem;
                var mesh = ms[k].Build(); go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Get("p_bodem" + k, Color.Lerp(grass, hot, k / (float)(Steps - 1)));
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
        }
    }

    // Poolmarkering: ijswit vak met een rood-wit gestreepte paal van 40 m, een gouden knop en een Nederlandse vlag die naar het oosten wappert
    const float PoleH = 40f;
    // kleuren van het terrein (zoals TC0-TC3 in de webversie): gras, dieper groen omhoog, grijsgroen boven de 16 m, aarde op steile flanken
    static Color TerrColor(string k)
    {
        if (k == "aarde") return new Color(0.545f, 0.482f, 0.345f);
        if (k == "sokkel") return new Color(0.61f, 0.58f, 0.53f);
        float h = (int.Parse(k.Substring(1)) + 0.5f) * 3f;
        Color c0 = new Color(0.64f, 0.78f, 0.45f), c1 = new Color(0.373f, 0.58f, 0.278f), c3 = new Color(0.604f, 0.643f, 0.541f);
        return Color.Lerp(Color.Lerp(c0, c1, Mathf.SmoothStep(0f, 1f, h / 5f)), c3, Mathf.SmoothStep(0f, 1f, (h - 16f) / 12f) * 0.6f);
    }

    static void PoleMarker(System.Func<string, MeshBuilder> M, Planet.Cell c, float g)
    {
        Vector3 o = c.dir * (Planet.R + g), u = c.dir, e = c.east, n = c.north;
        System.Func<float, float, Vector3> ring = (ang, r) => e * (Mathf.Cos(ang) * r) + n * (Mathf.Sin(ang) * r);
        // voet: een lage ijsrand rondom
        for (int k = 0; k < c.corners.Length; k++) { Vector3 a = c.corners[k], b = c.corners[(k + 1) % c.corners.Length], am = (a + b) * 0.5f, ai = Vector3.Lerp(a, c.dir, 0.08f), bi = Vector3.Lerp(b, c.dir, 0.08f); M("ijs").Quad(a * (Planet.R + g), b * (Planet.R + g), bi * ((Planet.R + g) + 0.25f), ai * ((Planet.R + g) + 0.25f), am - c.dir); }
        // paal: zeshoekig, in tien banden rood en wit
        const float r0 = 0.45f; const int Bands = 10;
        for (int s = 0; s < Bands; s++)
        {
            float y0 = s * PoleH / Bands, y1 = (s + 1) * PoleH / Bands; var mb = M(s % 2 == 0 ? "rood" : "wit");
            for (int k = 0; k < 6; k++) { float a0 = k * Mathf.PI / 3f, a1 = (k + 1) * Mathf.PI / 3f; Vector3 p0 = ring(a0, r0), p1 = ring(a1, r0); mb.Quad(o + p0 + u * y0, o + p1 + u * y0, o + p1 + u * y1, o + p0 + u * y1, p0 + p1); }
        }
        // gouden knop: twee zeshoekige punten tegen elkaar
        Vector3 kc = o + u * (PoleH + 0.8f);
        for (int k = 0; k < 6; k++) { float a0 = k * Mathf.PI / 3f, a1 = (k + 1) * Mathf.PI / 3f; Vector3 p0 = kc + ring(a0, 0.9f), p1 = kc + ring(a1, 0.9f); M("goud").Tri(p0, p1, kc + u * 0.9f, (p0 + p1) * 0.5f - kc + u * 0.5f); M("goud").Tri(p0, p1, kc - u * 0.9f, (p0 + p1) * 0.5f - kc - u * 0.5f); }
        // vlag: rood, wit, blauw (van boven naar beneden), van beide kanten zichtbaar
        const float fl = 9f, fh = 6f; float top = PoleH - 0.6f; string[] st = { "vlagrood", "vlagwit", "vlagblauw" };
        for (int s = 0; s < 3; s++)
        {
            float y1 = top - s * fh / 3f, y0 = y1 - fh / 3f; Vector3 a = o + u * y0 + e * r0, b = o + u * y0 + e * (r0 + fl), cc = o + u * y1 + e * (r0 + fl), d = o + u * y1 + e * r0;
            M(st[s]).Quad(a, b, cc, d, n); M(st[s]).Quad(a, b, cc, d, -n);
        }
    }
    static Color PoleColor(string k)
    {
        switch (k)
        {
            case "ijs": return new Color(0.92f, 0.96f, 1f); case "rood": return new Color(0.8f, 0.15f, 0.15f); case "wit": return new Color(0.97f, 0.97f, 0.95f);
            case "goud": return new Color(0.9f, 0.72f, 0.25f); case "vlagrood": return new Color(0.68f, 0.11f, 0.16f); case "vlagwit": return Color.white; case "vlagblauw": return new Color(0.13f, 0.27f, 0.55f);
            default: return Color.gray;
        }
    }

    // ---------- blokken en wegen per deel ----------
    class Frame
    {
        public Vector3 o, e, u, n; public Vector2[] poly; public float rad;
        // weg- en padvakken: per aangesloten buur de halve vector ernaartoe (de rand) en het halve hoogteverschil; null = vlak
        Vector2[] sq; float[] sd;
        // bolstand (wegknopen): elk punt wordt op de bol gelegd in plaats van in het raakvlak, zodat het precies aansluit op de linten van de wegen
        bool sphere; Vector3 cdir; float cg;
        public Frame(Frame b, Vector3 o2, Vector2[] p2, float r2) { u = b.u; e = b.e; n = b.n; o = o2; poly = p2; rad = r2; }
        public Frame(int i, bool onSphere = false)
        {
            var c = Planet.Cells[i]; u = c.dir; e = c.east; n = c.north; o = c.dir * (Planet.R + G(i)); poly = c.local; rad = c.radius;
            sphere = onSphere; cdir = c.dir; cg = G(i);
            if (!IsWalk(i)) return;
            var q = new List<Vector2>(); var d = new List<float>(); bool any = false; float g = G(i);
            foreach (int j in Partners(i))
            {
                if (!IsWalk(j)) continue; Vector3 w = (Planet.Cells[j].dir - c.dir) * Planet.R;
                q.Add(new Vector2(Vector3.Dot(w, e), Vector3.Dot(w, n)) * 0.5f); d.Add((G(j) - g) * 0.5f); if (Mathf.Abs(G(j) - g) > 0.002f) any = true;
            }
            if (any) { sq = q.ToArray(); sd = d.ToArray(); }
        }
        // Hoogte boven het hart van het vak op (x, z): langs elke arm lineair van het hart naar de rand, waar de weg op de
        // gemiddelde hoogte van de twee vakken ligt. Tussen armen in mengt het naar de arm waar het punt het meest in ligt.
        public float H(float x, float z)
        {
            if (sq == null) return 0f; Vector2 p = new Vector2(x, z); float pl = p.magnitude; if (pl < 1e-3f) return 0f;
            float h = 0f, ws = 0f;
            for (int k = 0; k < sq.Length; k++)
            {
                float L = sq[k].magnitude, cs = Vector2.Dot(p, sq[k]) / (pl * L); if (cs <= 0f) continue;
                float w = cs * cs; w *= w; w *= w;   // cos^8
                h += w * Mathf.Clamp01(Vector2.Dot(p, sq[k]) / (L * L)) * sd[k]; ws += w;
            }
            return ws > 1e-9f ? h / ws : 0f;
        }
        public Vector3 P(float x, float y, float z) => sphere ? (cdir * Planet.R + e * x + n * z).normalized * (Planet.R + cg + y + H(x, z)) : o + e * x + u * (y + H(x, z)) + n * z;
        public Vector3 D(float x, float y, float z) => e * x + u * y + n * z;
    }

    static Vector2 C(Frame f, int i, float k) => f.poly[i % f.poly.Length] * k;
    static void Wall(MeshBuilder m, Frame f, float k, float y0, float y1)
    {
        for (int i = 0; i < f.poly.Length; i++) { Vector2 a = C(f, i, k), b = C(f, i + 1, k), mid = (a + b) * 0.5f; m.Quad(f.P(a.x, y0, a.y), f.P(b.x, y0, b.y), f.P(b.x, y1, b.y), f.P(a.x, y1, a.y), f.D(mid.x, 0, mid.y)); }
    }
    static void Top(MeshBuilder m, Frame f, float k, float y)
    {
        for (int i = 0; i < f.poly.Length; i++) { Vector2 a = C(f, i, k), b = C(f, i + 1, k); m.Tri(f.P(0, y, 0), f.P(a.x, y, a.y), f.P(b.x, y, b.y), f.u); }
    }
    static void Windows(MeshBuilder m, Frame f, float k, float y0, float y1, float f0, float f1)
    {
        for (int i = 0; i < f.poly.Length; i++)
        {
            Vector2 a = C(f, i, k), b = C(f, i + 1, k), outw = ((a + b) * 0.5f).normalized;
            Vector2 p0 = Vector2.Lerp(a, b, f0) + outw * 0.04f, p1 = Vector2.Lerp(a, b, f1) + outw * 0.04f;
            m.Quad(f.P(p0.x, y0, p0.y), f.P(p1.x, y0, p1.y), f.P(p1.x, y1, p1.y), f.P(p0.x, y1, p0.y), f.D(outw.x, 0, outw.y));
        }
    }
    static void Cone(MeshBuilder m, Frame f, float cx, float cz, float r, float y0, float y1, float rTop)
    {
        for (int i = 0; i < 6; i++)
        {
            float a0 = i * Mathf.PI / 3f, a1 = (i + 1) * Mathf.PI / 3f, am = (a0 + a1) * 0.5f;
            Vector3 b0 = f.P(cx + Mathf.Cos(a0) * r, y0, cz + Mathf.Sin(a0) * r), b1 = f.P(cx + Mathf.Cos(a1) * r, y0, cz + Mathf.Sin(a1) * r);
            Vector3 outw = f.D(Mathf.Cos(am), (r - rTop) / Mathf.Max(0.01f, y1 - y0), Mathf.Sin(am));
            if (rTop <= 0.001f) m.Tri(b0, b1, f.P(cx, y1, cz), outw);
            else m.Quad(b0, b1, f.P(cx + Mathf.Cos(a1) * rTop, y1, cz + Mathf.Sin(a1) * rTop), f.P(cx + Mathf.Cos(a0) * rTop, y1, cz + Mathf.Sin(a0) * rTop), outw);
        }
    }
    static void Roof(MeshBuilder m, Frame f, float k, float y1)
    {
        Vector3 apex = f.P(0, y1 + 2.4f, 0);
        for (int i = 0; i < f.poly.Length; i++) { Vector2 a = C(f, i, k), b = C(f, i + 1, k), mid = (a + b) * 0.5f; m.Tri(f.P(a.x, y1, a.y), f.P(b.x, y1, b.y), apex, f.D(mid.x, 0.6f * mid.magnitude, mid.y)); }
    }
    static void Cap6(MeshBuilder m, Frame f, float r, float y)
    {
        for (int i = 0; i < 6; i++) { float a0 = i * Mathf.PI / 3f, a1 = (i + 1) * Mathf.PI / 3f; m.Tri(f.P(0, y, 0), f.P(Mathf.Cos(a0) * r, y, Mathf.Sin(a0) * r), f.P(Mathf.Cos(a1) * r, y, Mathf.Sin(a1) * r), f.u); }
    }
    static void Disc(MeshBuilder m, Frame f, float r, float y)
    {
        for (int i = 0; i < 12; i++) { float a0 = i * Mathf.PI / 6f, a1 = (i + 1) * Mathf.PI / 6f; m.Tri(f.P(0, y, 0), f.P(Mathf.Cos(a0) * r, y, Mathf.Sin(a0) * r), f.P(Mathf.Cos(a1) * r, y, Mathf.Sin(a1) * r), f.u); }
    }
    static void Skirt(MeshBuilder m, Frame f, float r, float y0, float y1)
    {
        for (int i = 0; i < 12; i++) { float a0 = i * Mathf.PI / 6f, a1 = (i + 1) * Mathf.PI / 6f, am = (a0 + a1) * 0.5f; m.Quad(f.P(Mathf.Cos(a0) * r, y0, Mathf.Sin(a0) * r), f.P(Mathf.Cos(a1) * r, y0, Mathf.Sin(a1) * r), f.P(Mathf.Cos(a1) * r, y1, Mathf.Sin(a1) * r), f.P(Mathf.Cos(a0) * r, y1, Mathf.Sin(a0) * r), f.D(Mathf.Cos(am), 0, Mathf.Sin(am))); }
    }
    // een balkje op (cx, cz), lang langs richting ang (halve lengte hl, halve breedte hw), van y0 tot y1
    static void Box(MeshBuilder m, Frame f, float cx, float cz, float ang, float hl, float hw, float y0, float y1)
    {
        Vector2 c = new Vector2(cx, cz), d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * hl, r = new Vector2(-Mathf.Sin(ang), Mathf.Cos(ang)) * hw;
        Vector2[] p = { c - d - r, c + d - r, c + d + r, c - d + r };
        for (int i = 0; i < 4; i++) { Vector2 a = p[i], b = p[(i + 1) % 4], o = (a + b) * 0.5f - c; m.Quad(f.P(a.x, y0, a.y), f.P(b.x, y0, b.y), f.P(b.x, y1, b.y), f.P(a.x, y1, a.y), f.D(o.x, 0, o.y)); }
        m.Quad(f.P(p[0].x, y1, p[0].y), f.P(p[1].x, y1, p[1].y), f.P(p[2].x, y1, p[2].y), f.P(p[3].x, y1, p[3].y), f.u);
    }
    // spitse torenspits op de vakvorm (schaal k), van y tot y + hgt
    static void Spire(MeshBuilder m, Frame f, float k, float y, float hgt)
    {
        Vector3 apex = f.P(0, y + hgt, 0);
        for (int i = 0; i < f.poly.Length; i++) { Vector2 a = C(f, i, k), b = C(f, i + 1, k), mid = (a + b) * 0.5f; m.Tri(f.P(a.x, y, a.y), f.P(b.x, y, b.y), apex, f.D(mid.x, 0.3f * mid.magnitude, mid.y)); }
    }
    // gestreepte zonneluifel boven een etalage: schuin naar buiten en omlaag, aan beide kanten zichtbaar
    static void Awning(System.Func<string, MeshBuilder> M, Frame f, float k, float y, string stripe)
    {
        for (int i = 0; i < f.poly.Length; i++)
        {
            Vector2 a = C(f, i, k), b = C(f, i + 1, k), outw = ((a + b) * 0.5f).normalized; const int S = 6;
            for (int s = 0; s < S; s++)
            {
                Vector2 p0 = Vector2.Lerp(a, b, 0.1f + 0.8f * s / S), p1 = Vector2.Lerp(a, b, 0.1f + 0.8f * (s + 1) / S), q0 = p0 + outw * 1.1f, q1 = p1 + outw * 1.1f;
                var mb = M(s % 2 == 0 ? stripe : "doek");
                Vector3 A = f.P(p0.x, y, p0.y), B = f.P(p1.x, y, p1.y), Cc = f.P(q1.x, y - 0.45f, q1.y), D = f.P(q0.x, y - 0.45f, q0.y);
                mb.Quad(A, B, Cc, D, f.D(outw.x * 0.4f, 1f, outw.y * 0.4f)); mb.Quad(A, B, Cc, D, f.D(-outw.x * 0.4f, -1f, -outw.y * 0.4f));
            }
        }
    }

    // Molenwieken: vier armen met een zeil, in het xy-vlak rond de as (z = naar voren). Eén gedeelde mesh voor alle molens.
    static Mesh wiekMesh, zeilMesh;
    const float WiekL = 6.3f;
    static void WiekMeshes()
    {
        if (wiekMesh != null && zeilMesh != null) return;
        var w = new MeshBuilder(); var z = new MeshBuilder();
        for (int k = 0; k < 4; k++)
        {
            float t = k * Mathf.PI / 2f; Vector3 u = new Vector3(Mathf.Cos(t), Mathf.Sin(t), 0f), p = new Vector3(-u.y, u.x, 0f);
            Vector3 a = -p * 0.09f, b = p * 0.09f, c = u * WiekL + p * 0.09f, d = u * WiekL - p * 0.09f, fz = new Vector3(0, 0, 0.18f);
            w.Quad(a + fz, b + fz, c + fz, d + fz, Vector3.forward); w.Quad(a + fz, b + fz, c + fz, d + fz, Vector3.back);
            w.Quad(a, d, d + fz, a + fz, -p); w.Quad(b, c, c + fz, b + fz, p);
            Vector3 s0 = u * 1.1f + p * 0.12f, s1 = u * WiekL + p * 0.12f, s2 = u * WiekL + p * 1.45f, s3 = u * 1.1f + p * 1.45f, zz = new Vector3(0, 0, 0.1f);
            z.Quad(s0 + zz, s1 + zz, s2 + zz, s3 + zz, Vector3.forward); z.Quad(s0 + zz, s1 + zz, s2 + zz, s3 + zz, Vector3.back);
            for (float r = 1.9f; r < WiekL; r += 1.1f) { Vector3 l0 = u * r + p * 0.1f, l1 = u * r + p * 1.47f, lw = u * 0.06f, lz = new Vector3(0, 0, 0.14f); w.Quad(l0 - lw + lz, l0 + lw + lz, l1 + lw + lz, l1 - lw + lz, Vector3.forward); w.Quad(l0 - lw + lz, l0 + lw + lz, l1 + lw + lz, l1 - lw + lz, Vector3.back); }   // dwarslatten
        }
        for (int k = 0; k < 6; k++) { float a0 = k * Mathf.PI / 3f, a1 = (k + 1) * Mathf.PI / 3f; Vector3 p0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0) * 0.45f, p1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0) * 0.45f, hz = new Vector3(0, 0, 0.35f); w.Tri(hz, p0 + hz, p1 + hz, Vector3.forward); w.Quad(p0, p1, p1 + hz, p0 + hz, (p0 + p1)); }   // naaf
        wiekMesh = w.Build(); zeilMesh = z.Build();
    }
    static void MakeWieken(Transform parent, Vector3 pos, Quaternion rot, int hash)
    {
        WiekMeshes();
        var pivot = new GameObject("Wieken"); pivot.transform.SetParent(parent, false); pivot.transform.SetPositionAndRotation(pos, rot);
        pivot.transform.Rotate(0f, 0f, (hash & 255) / 255f * 90f, Space.Self);
        var sp = pivot.AddComponent<Wieken>(); sp.Speed = 38f + ((hash >> 8) & 15);
        foreach (var (mesh, key) in new[] { (wiekMesh, "wiek"), (zeilMesh, "zeil") })
        {
            var c = new GameObject(key); c.transform.SetParent(pivot.transform, false); c.layer = Layers.Gebouw;
            c.AddComponent<MeshFilter>().sharedMesh = mesh; c.AddComponent<MeshRenderer>().sharedMaterial = Mats.Get("p_" + key, ColorOf(key));
        }
    }

    static void Strip(MeshBuilder m, Frame f, Vector2 d, Vector2 r, float s0, float s1, float hw, float y)
    {
        Vector2 a = d * s0 - r * hw, b = d * s0 + r * hw, c = d * s1 + r * hw, e = d * s1 - r * hw;
        m.Quad(f.P(a.x, y, a.y), f.P(b.x, y, b.y), f.P(c.x, y, c.y), f.P(e.x, y, e.y), f.u);
    }
    static void StripSides(MeshBuilder m, Frame f, Vector2 d, Vector2 r, float s0, float s1, float hw, float y0, float y1)
    {
        for (int sg = -1; sg <= 1; sg += 2)
        {
            Vector2 a = d * s0 + r * (hw * sg), b = d * s1 + r * (hw * sg);
            m.Quad(f.P(a.x, y0, a.y), f.P(b.x, y0, b.y), f.P(b.x, y1, b.y), f.P(a.x, y1, a.y), f.D(r.x * sg, 0, r.y * sg));
        }
    }

    // Een wegvak: één nette omtrek per laag (stoep, asfalt) in plaats van losse schijven en stroken. De omtrek wordt
    // rondom bemonsterd: in elke richting de verste rand van de armen naar de buurwegen, met een kern die bochten
    // afrondt. Daarop: stoeprand, middenstreepjes op rechte armen, een eilandje op kruisingen en een lantaarn.
    // De richting van de weg door een buurvak dat zelf een "schuin recht stuk" is (twee aansluitingen die 120° of 180° maken),
    // uitgedrukt in het assenstelsel van dit vak en wijzend dit vak in. Geeft false als de buur gewoon via zijn hart gaat.
    // Wereldrichting van de weg door een schuin recht wegvak (zie ChordDirOf); false als het vak via zijn hart gaat
    public static bool ChordDirWorld(int j, out Vector3 dir)
    {
        dir = Vector3.zero; var arms = new List<Vector3>();
        foreach (int k in Partners(j)) if (IsRoad(k)) arms.Add(((Planet.Cells[j].dir + Planet.Cells[k].dir) * 0.5f).normalized);
        if (arms.Count != 2) return false;
        Vector3 cj = Planet.Cells[j].dir; if (Vector3.Dot((arms[0] - cj).normalized, (arms[1] - cj).normalized) >= -0.3f) return false;
        dir = (arms[1] - arms[0]).normalized; return true;
    }

    static bool ChordDirOf(int j, Frame fr, Vector2 e, out Vector2 dir)
    {
        dir = (-e).normalized; var arms = new List<Vector3>();
        foreach (int k in Partners(j)) if (IsRoad(k)) arms.Add(((Planet.Cells[j].dir + Planet.Cells[k].dir) * 0.5f).normalized * Planet.R);
        if (arms.Count != 2) return false;
        Vector3 cj = Planet.Cells[j].dir * Planet.R; Vector3 a0 = arms[0] - cj, a1 = arms[1] - cj;
        if (Vector3.Dot(a0.normalized, a1.normalized) >= -0.3f) return false;
        Vector3 w = arms[1] - arms[0]; Vector2 d = new Vector2(Vector3.Dot(w, fr.e), Vector3.Dot(w, fr.n)).normalized;
        if (Vector2.Dot(d, -e) < 0f) d = -d; dir = d; return true;
    }

    // ---------- wegen ----------
    // Afmetingen per stijl: halve breedte van het asfalt en van asfalt + stoep (m). 0 = gewone weg, 1 = brede weg, 2 = laan (met middenberm)
    static float hwA = 2.5f, hwS = 4.5f; static byte styleNow; static bool bridgeNow;
    static void Dims(byte style, out float a, out float s) { if (style == 1) { a = 4.2f; s = 6.2f; } else if (style == 2) { a = 4.4f; s = 6.0f; } else { a = 2.5f; s = 4.5f; } }
    // Profiel zoals in de webversie: asfalt net boven de grond, een schuine stoeprand van 30 cm en de stoep 25 cm hoog,
    // met een rand die tot onder de grond doorloopt. De kern van een kruising ligt 5 mm lager dan de armen.
    const float AY = 0.10f, ACY = 0.095f, SY = 0.25f, KW = 0.3f, LY = 0.11f, SKB = -0.35f;
    // Zijkant van stoep en pad tot onder het maaiveld: dieper waar het vak in een kom ligt of de weg helt
    static float skb = SKB;
    static float SkirtDepth(int i)
    {
        float g = G(i), s = 0f; foreach (int j in Partners(i)) if (IsWalk(j)) s = Mathf.Max(s, Mathf.Abs(G(j) - g) * 0.5f);
        return SKB - Mathf.Max(0f, g - MinCorner(i)) - s;
    }
    // schuine stoeprand van de rand van het asfalt (a0-a1, hoogte ya) naar de stoep (b0-b1, hoogte SY)
    static void Kerb(MeshBuilder m, Frame fr, Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1, float ya)
    {
        Vector2 o = (b0 + b1 - a0 - a1) * 0.5f;
        m.Quad(fr.P(a0.x, ya, a0.y), fr.P(a1.x, ya, a1.y), fr.P(b1.x, SY, b1.y), fr.P(b0.x, SY, b0.y), fr.D(-o.x, KW, -o.y));
    }
    // stuk stoep tussen een binnenlijn (i0-i1) en een buitenlijn (o0-o1), met een zijkant omlaag langs de buitenlijn
    static void Walk(MeshBuilder m, Frame fr, Vector2 i0, Vector2 i1, Vector2 o0, Vector2 o1)
    {
        m.Quad(fr.P(i0.x, SY, i0.y), fr.P(i1.x, SY, i1.y), fr.P(o1.x, SY, o1.y), fr.P(o0.x, SY, o0.y), fr.u);
        Vector2 w = (o0 + o1 - i0 - i1) * 0.5f;
        m.Quad(fr.P(o0.x, skb, o0.y), fr.P(o1.x, skb, o1.y), fr.P(o1.x, SY, o1.y), fr.P(o0.x, SY, o0.y), fr.D(w.x, 0, w.y));
    }
    public static float LaneOffset(int i) { byte s = StyleOf(i); return s == 1 ? 2.5f : s == 2 ? 2.9f : 1.25f; }   // rijbaan van de auto, rechts van het midden
    public static float WalkOffset(int i) { byte s = StyleOf(i); return s == 0 ? 3.5f : 5.2f; }                       // looplijn van voetgangers op de stoep
    static Vector2 PoleAt(Vector2 e, Vector2 d) { Vector2 r = new Vector2(d.y, -d.x); return e + d * 3.2f + r * (hwS - 0.7f); }   // verkeerslicht: rechts van de arm, na het zebrapad
    // wereldpositie (bovenkant) van het verkeerslicht bij vak i voor verkeer dat uit buur p komt
    public static bool PolePos(int i, int p, out Vector3 pos)
    {
        pos = Vector3.zero; if (!IsRoad(i)) return false;
        var fr = new Frame(i); float a, s; Dims(StyleOf(i), out a, out s); float old = hwS; hwS = s;
        Vector3 d3 = Planet.Cells[p].dir * Planet.R - fr.o; Vector2 q = new Vector2(Vector3.Dot(d3, fr.e), Vector3.Dot(d3, fr.n)) * 0.5f; Vector2 d = (-q).normalized;   // armen van een kruising lopen naar het hart
        Vector2 pp = PoleAt(q, d); hwS = old; pos = fr.P(pp.x, 3.75f, pp.y); return true;
    }

    // pijl op het asfalt die langs d wijst (eenrichtingsverkeer)
    static void Arrow(System.Func<string, MeshBuilder> M, Frame fr, Vector2 c, Vector2 d, float len)
    {
        d = d.normalized; Vector2 r = new Vector2(d.y, -d.x); const float y = LY + 0.001f;
        Vector2 b0 = c - d * len * 0.5f, b1 = c + d * len * 0.1f, tip = c + d * len * 0.5f;
        M("lijn").Quad(fr.P(b0.x - r.x * 0.14f, y, b0.y - r.y * 0.14f), fr.P(b0.x + r.x * 0.14f, y, b0.y + r.y * 0.14f), fr.P(b1.x + r.x * 0.14f, y, b1.y + r.y * 0.14f), fr.P(b1.x - r.x * 0.14f, y, b1.y - r.y * 0.14f), fr.u);
        M("lijn").Tri(fr.P(b1.x - r.x * 0.55f, y, b1.y - r.y * 0.55f), fr.P(b1.x + r.x * 0.55f, y, b1.y + r.y * 0.55f), fr.P(tip.x, y, tip.y), fr.u);
    }
    // middenberm van een laan met bomen, van a naar b
    static void Median(System.Func<string, MeshBuilder> M, Frame fr, Vector2 a, Vector2 b)
    {
        Vector2 d = b - a; float len = d.magnitude; if (len < 2f) return; d /= len; Vector2 r = new Vector2(d.y, -d.x); const float w = 1.1f;
        M("rand").Quad(fr.P(a.x - r.x * (w + 0.2f), 0.2f, a.y - r.y * (w + 0.2f)), fr.P(a.x + r.x * (w + 0.2f), 0.2f, a.y + r.y * (w + 0.2f)), fr.P(b.x + r.x * (w + 0.2f), 0.2f, b.y + r.y * (w + 0.2f)), fr.P(b.x - r.x * (w + 0.2f), 0.2f, b.y - r.y * (w + 0.2f)), fr.u);
        M("gras").Quad(fr.P(a.x - r.x * w, 0.23f, a.y - r.y * w), fr.P(a.x + r.x * w, 0.23f, a.y + r.y * w), fr.P(b.x + r.x * w, 0.23f, b.y + r.y * w), fr.P(b.x - r.x * w, 0.23f, b.y - r.y * w), fr.u);
        for (float s = Mathf.Min(3.5f, len * 0.5f); s < len - 1.5f; s += 7f)
        {
            Vector2 p = a + d * s; Cone(M("stam"), fr, p.x, p.y, 0.2f, 0.2f, 2.3f, 0.15f); Cone(M("blad"), fr, p.x, p.y, 1.5f, 1.9f, 4.8f, 0f);
        }
    }
    // balustrade langs een brug van a naar b (twee kanten, zodat je haar ook van binnen ziet)
    static void Parapet(System.Func<string, MeshBuilder> M, Frame fr, Vector2 a, Vector2 b, Vector2 outward)
    {
        Vector3 pa = fr.P(a.x, SY, a.y), pb = fr.P(b.x, SY, b.y), ta = fr.P(a.x, 1.05f, a.y), tb = fr.P(b.x, 1.05f, b.y);
        M("rand").Quad(pa, pb, tb, ta, fr.D(outward.x, 0, outward.y)); M("rand").Quad(pa, pb, tb, ta, fr.D(-outward.x, 0, -outward.y));
        Vector2 d = (b - a).normalized, r = new Vector2(d.y, -d.x) * 0.12f;
        M("rand").Quad(fr.P(a.x - r.x, 1.05f, a.y - r.y), fr.P(a.x + r.x, 1.05f, a.y + r.y), fr.P(b.x + r.x, 1.05f, b.y + r.y), fr.P(b.x - r.x, 1.05f, b.y - r.y), fr.u);
    }
    // de stijlen en brug die bij het hele wegvak horen: pijler onder een brug
    static void BridgePillar(System.Func<string, MeshBuilder> M, Frame fr, int i)
    {
        float depth = Under.TryGetValue(i, out var u) && u == "kuil" ? -3f : -1.5f;
        Cone(M("steen"), fr, 0f, 0f, 1.1f, depth, -0.3f, 1.1f);
    }

    // De onderkant van een brug: het water, het meertje of de uitgraving waar de weg overheen gaat
    static void UnderMesh(Dictionary<string, MeshBuilder> mb, Frame fr, int i, int hash)
    {
        System.Func<string, MeshBuilder> M = k => { if (!mb.TryGetValue(k, out var x)) { x = new MeshBuilder(); mb[k] = x; } return x; };
        if (!Under.TryGetValue(i, out var u)) return;
        if (u == "meer") Lake(mb, fr, i, 0f, hash);
        else if (u == "water") { Top(M("water"), fr, 1.0f, 0.03f); Wall(M("water"), fr, 1.0f, -0.2f, 0.03f); }
        else
        {
            for (int e2 = 0; e2 < fr.poly.Length; e2++)
            {
                Vector2 a2 = C(fr, e2, 1.0f), b2 = C(fr, e2 + 1, 1.0f), a3 = C(fr, e2, 0.55f), b3 = C(fr, e2 + 1, 0.55f), m2 = (a2 + b2) * 0.5f;
                M("gras").Quad(fr.P(a2.x, 0f, a2.y), fr.P(b2.x, 0f, b2.y), fr.P(b3.x, -3f, b3.y), fr.P(a3.x, -3f, a3.y), fr.D(-m2.x, 2f, -m2.y));
            }
            Top(M("gras"), fr, 0.55f, -3f);
        }
    }

    // Een knoop van het wegennet (kruising, knik, doodlopend eind of los vak). ends: per arm het punt op de as waar het lint
    // van die arm ophoudt (NodeTrim van het hart, in de richting van de buur ids[a]); de armen lopen naar het hart, zoals in de
    // webversie alle wegen door de knooppunten gaan. Binnen die afstand tekent de knoop alles: asfalt, stoepranden en stoep.
    static void RoadNode(Dictionary<string, MeshBuilder> mb, Frame fr, int i, List<Vector2> ends, List<int> ids)
    {
        System.Func<string, MeshBuilder> M = k => { if (!mb.TryGetValue(k, out var x)) { x = new MeshBuilder(); mb[k] = x; } return x; };
        styleNow = StyleOf(i); Dims(styleNow, out hwA, out hwS); bridgeNow = Under.ContainsKey(i); skb = SkirtDepth(i);
        int na = ends.Count; var dirs = new List<Vector2>(); for (int a = 0; a < na; a++) dirs.Add((-ends[a]).normalized);
        if (na == 0)   // los wegvak: een rond pleintje asfalt met stoeprand en stoep eromheen
        {
            float R0 = hwA + 2.1f; var c0 = Circle(Vector2.zero, R0, 0f, Mathf.PI * 2f, 40); var c1 = Circle(Vector2.zero, R0 + KW, 0f, Mathf.PI * 2f, 40); var c2 = Circle(Vector2.zero, R0 + hwS - hwA, 0f, Mathf.PI * 2f, 40);
            for (int k = 0; k < 40; k++) { M("asfalt").Tri(fr.P(0, AY, 0), fr.P(c0[k].x, AY, c0[k].y), fr.P(c0[k + 1].x, AY, c0[k + 1].y), fr.u); Kerb(M("rand"), fr, c0[k], c0[k + 1], c1[k], c1[k + 1], AY); Walk(M("stoep"), fr, c1[k], c1[k + 1], c2[k], c2[k + 1]); }
            return;
        }
        // Kruising, doodlopend eind of scherpe knik, zoals in de webversie: alle armen samen zijn één vlak asfalt. Tussen twee
        // opeenvolgende armen (tegen de klok in) loopt de stoep door met een stoephoek: een kwadratische boog die aan beide
        // stoepranden raakt, met hun snijpunt als trekpunt; recht als de randen (bijna) in elkaars verlengde liggen; rond om
        // het hart als de hoek groter is dan 200° (buitenkant van een knik) of bij een doodlopend eind (een ronde kop).
        bool signals = Lights && na >= 3;
        var order = new List<int>(); for (int a = 0; a < na; a++) order.Add(a);
        System.Func<int, float> angOf = a => Mathf.Atan2(-dirs[a].y, -dirs[a].x);
        order.Sort((x, y) => angOf(x).CompareTo(angOf(y)));
        // punt op arm a: lat opzij (+ = naar de volgende arm, tegen de klok in), s de arm in vanaf de vakrand
        System.Func<int, float, float, Vector2> P = (a, lat, s) =>
        {
            Vector2 e = ends[a], d = dirs[a], r = new Vector2(d.y, -d.x), en = e.normalized, p0 = e + r * lat; float den = Vector2.Dot(d, en);
            return p0 + d * ((Mathf.Abs(den) < 1e-4f ? 0f : Vector2.Dot(e - p0, en) / den) + s);
        };
        // per hoek (van arm order[c] naar arm order[c+1]): soort (0 boog, 1 recht, 2 rond), waar de stoepranden ophouden, middelpunt en straal
        var kind = new int[na]; var sA = new float[na]; var sB = new float[na]; var cc = new Vector2[na]; var rr = new float[na];
        float maxPerp = 0f; for (int a = 0; a < na; a++) maxPerp = Mathf.Max(maxPerp, Mathf.Abs(Vector2.Dot(ends[a], new Vector2(dirs[a].y, -dirs[a].x))));
        System.Func<int, float, Vector2, float, float> hitS = (a, lat, c, rad) =>   // waar de lijn op afstand lat van arm a de cirkel (c, rad) voor het eerst raakt
        {
            Vector2 B = P(a, lat, 0f) - c; float bd = Vector2.Dot(B, dirs[a]), disc = bd * bd - B.sqrMagnitude + rad * rad;
            return Mathf.Max(0.3f, -bd - (disc > 0f ? Mathf.Sqrt(disc) : 0f));
        };
        for (int c = 0; c < na; c++)
        {
            int a = order[c], b = order[(c + 1) % na];
            float phi = na == 1 ? Mathf.PI * 2f : Mathf.Repeat(angOf(b) - angOf(a), Mathf.PI * 2f);
            if (na == 1 || phi > Mathf.PI * 200f / 180f)
            {
                kind[c] = 2;
                if (na == 1) { cc[c] = P(a, 0f, Vector2.Dot(-P(a, 0f, 0f), dirs[a])); rr[c] = hwA + (styleNow == 0 ? 2.1f : 0.3f); }   // ronde kop op de as, waar de weg het dichtst bij het hart komt
                else { cc[c] = Vector2.zero; rr[c] = hwA + maxPerp; }   // buitenkant van een knik: een cirkel om het hart die precies aan beide stoepranden raakt (vecBend)
                sA[c] = hitS(a, hwA, cc[c], rr[c]); sB[c] = hitS(b, -hwA, cc[c], rr[c]);
                continue;
            }
            Vector2 pa = P(a, hwA, 0f), pb = P(b, -hwA, 0f); float cr = Cross(dirs[a], dirs[b]);
            float t = Mathf.Abs(cr) > 0.05f ? Cross(pb - pa, dirs[b]) / cr : -1f, u = Mathf.Abs(cr) > 0.05f ? Cross(pb - pa, dirs[a]) / cr : -1f;   // snijpunt van de twee stoepranden
            if (t > 0.4f && u > 0.4f && t < 30f && u < 30f) { kind[c] = 0; sA[c] = Mathf.Max(0.4f, t - 3f); sB[c] = Mathf.Max(0.4f, u - 3f); }
            else { kind[c] = 1; sA[c] = Mathf.Max(0.4f, Vector2.Dot(-pa, dirs[a])); sB[c] = Mathf.Max(0.4f, Vector2.Dot(-pb, dirs[b])); }
        }
        // de stoephoek van hoek c op afstand lat van de armen (hwA: rand van het asfalt, hwA+KW: bovenkant stoeprand, hwS: buitenkant stoep)
        System.Func<int, float, Vector2[]> corner = (c, lat) =>
        {
            int a = order[c], b = order[(c + 1) % na];
            if (kind[c] == 2)
            {
                float rad = rr[c] + lat - hwA; Vector2 p = P(a, lat, lat <= hwA + 0.01f ? sA[c] : hitS(a, lat, cc[c], rad)), q = P(b, -lat, lat <= hwA + 0.01f ? sB[c] : hitS(b, -lat, cc[c], rad));
                float a0 = Mathf.Atan2(p.y - cc[c].y, p.x - cc[c].x), a1 = Mathf.Atan2(q.y - cc[c].y, q.x - cc[c].x); if (a1 <= a0 + 0.01f) a1 += Mathf.PI * 2f;
                var arc = Circle(cc[c], rad, a0, a1, Mathf.Max(6, Mathf.CeilToInt((a1 - a0) / 0.15f))); arc[0] = p; arc[arc.Length - 1] = q; return arc;
            }
            Vector2 P0 = P(a, lat, sA[c]), Q0 = P(b, -lat, sB[c]); var pts = new Vector2[11];
            float crs = Cross(dirs[a], dirs[b]), tt = Mathf.Abs(crs) > 0.05f ? Cross(Q0 - P0, dirs[b]) / crs : -1f, uu = Mathf.Abs(crs) > 0.05f ? Cross(Q0 - P0, dirs[a]) / crs : -1f;
            bool curve = kind[c] == 0 && tt > 0.2f && uu > 0.2f && tt < 25f && uu < 25f, crossed = kind[c] == 0 && Mathf.Abs(crs) > 0.05f && tt < 0f && uu < 0f; Vector2 X = P0 + dirs[a] * tt;
            // crossed: de twee randen kruisen elkaar al vóór de hoek (scherpe hoek, buitenkant stoep): de hoek krimpt tot dat kruispunt
            for (int k = 0; k <= 10; k++) { float f = k / 10f, g = 1f - f; pts[k] = curve ? P0 * (g * g) + X * (2f * g * f) + Q0 * (f * f) : crossed ? X : Vector2.Lerp(P0, Q0, f); }
            return pts;
        };
        // asfalt: één waaier vanuit het hart (bij een doodlopend eind vanuit het midden van de kop) langs de hele omtrek
        Vector2 hub = na == 1 ? cc[0] : Vector2.zero; var rim = new List<Vector2>();
        for (int c = 0; c < na; c++) { int a = order[c]; rim.Add(P(a, -hwA, 0f)); rim.Add(P(a, hwA, 0f)); rim.AddRange(corner(c, hwA)); }
        for (int k = 0; k < rim.Count; k++) { Vector2 p = rim[k], q = rim[(k + 1) % rim.Count]; M("asfalt").Tri(fr.P(hub.x, AY, hub.y), fr.P(p.x, AY, p.y), fr.P(q.x, AY, q.y), fr.u); }
        // stoeprand en stoep: langs de zijkanten van elke arm tot waar de hoek begint, dan om de hoek heen
        float bestPhi = -1f; Vector2 lampAt = Vector2.zero;
        for (int c = 0; c < na; c++)
        {
            int a = order[c], b = order[(c + 1) % na];
            SideWalk(M, fr, P, a, 1f, sA[c]); SideWalk(M, fr, P, b, -1f, sB[c]);
            var k0 = corner(c, hwA); var k1 = corner(c, hwA + KW); var k2 = corner(c, hwS);
            int nk = Mathf.Min(k0.Length, Mathf.Min(k1.Length, k2.Length));
            for (int k = 0; k + 1 < nk; k++) { Kerb(M("rand"), fr, k0[k], k0[k + 1], k1[k], k1[k + 1], AY); Walk(M("stoep"), fr, k1[k], k1[k + 1], k2[k], k2[k + 1]); }
            float phi = na == 1 ? 7f : Mathf.Repeat(angOf(b) - angOf(a), Mathf.PI * 2f);
            if (phi > bestPhi) { bestPhi = phi; var lc = corner(c, hwS - 0.6f); lampAt = lc[lc.Length / 2]; }
        }
        if (na != 2) { Cone(M("paal"), fr, lampAt.x, lampAt.y, 0.08f, SY, 5.2f, 0.06f); Cone(M("lamp"), fr, lampAt.x, lampAt.y, 0.28f, 4.9f, 5.35f, 0.2f); }   // lantaarn op de breedste hoek (kruising of kop)
        // per arm: markering, pijlen, middenberm, zebrapaden, verkeerslichten en brugleuningen
        for (int c = 0; c < na; c++)
        {
            int a = order[c], cp = (c + na - 1) % na; Vector2 e = ends[a], d = dirs[a], r = new Vector2(d.y, -d.x);
            float L = Mathf.Min(sA[c], sB[cp]);   // zo ver loopt de arm recht door (tot de eerste stoephoek)
            if (na == 1) L = Vector2.Dot(cc[0] - P(a, 0f, 0f), d);
            int flow = Flow(i, ids[a]);   // +1: verkeer verlaat dit vak langs deze arm, -1: komt erlangs binnen
            if (flow != 0) Arrow(M, fr, e + d * Mathf.Min(L * 0.5f, 4.5f), flow > 0 ? -d : d, 2.2f);
            else if (na <= 2 && styleNow != 2) for (float s0 = 0.6f; s0 + 1.2f < L - 0.5f; s0 += 2.6f) { Vector2 a0 = e + d * s0, a1 = e + d * (s0 + 1.4f); M("lijn").Quad(fr.P(a0.x - r.x * 0.07f, LY, a0.y - r.y * 0.07f), fr.P(a0.x + r.x * 0.07f, LY, a0.y + r.y * 0.07f), fr.P(a1.x + r.x * 0.07f, LY, a1.y + r.y * 0.07f), fr.P(a1.x - r.x * 0.07f, LY, a1.y - r.y * 0.07f), fr.u); }
            if (styleNow == 1 && na <= 2) for (int ln = -1; ln <= 1; ln += 2) for (float s0 = 0.6f; s0 + 1.2f < L - 0.5f; s0 += 2.6f) { Vector2 off = r * (ln * hwA * 0.5f); Vector2 a0 = e + d * s0 + off, a1 = e + d * (s0 + 1.4f) + off; M("lijn").Quad(fr.P(a0.x - r.x * 0.05f, LY, a0.y - r.y * 0.05f), fr.P(a0.x + r.x * 0.05f, LY, a0.y + r.y * 0.05f), fr.P(a1.x + r.x * 0.05f, LY, a1.y + r.y * 0.05f), fr.P(a1.x - r.x * 0.05f, LY, a1.y - r.y * 0.05f), fr.u); }
            if (styleNow == 2 && (na == 1 || na >= 3)) Median(M, fr, e + d * 0.2f, e + d * Mathf.Max(0.2f, L - 1f));
            if (na >= 3 && L > 3f) { int zm = Mathf.FloorToInt((hwA - 0.5f) / 0.95f); for (int z = -zm; z <= zm; z++) { Vector2 off = r * (z * 0.95f); Vector2 a0 = e + d * 0.6f + off - r * 0.28f, b0 = e + d * 0.6f + off + r * 0.28f, c0 = e + d * 2.6f + off + r * 0.28f, e0 = e + d * 2.6f + off - r * 0.28f; M("lijn").Quad(fr.P(a0.x, LY, a0.y), fr.P(b0.x, LY, b0.y), fr.P(c0.x, LY, c0.y), fr.P(e0.x, LY, e0.y), fr.u); } }
            if (signals && CanDrive(ids[a], i)) { Vector2 pp = PoleAt(e, d); Cone(M("paal"), fr, pp.x, pp.y, 0.08f, SY, 3.9f, 0.06f); Cone(M("paal"), fr, pp.x, pp.y, 0.24f, 3.3f, 3.95f, 0.24f); }
            if (bridgeNow) { Parapet(M, fr, P(a, -hwS, 0f), P(a, -hwS, sB[cp]), -r); Parapet(M, fr, P(a, hwS, 0f), P(a, hwS, sA[c]), r); }
        }
    }
    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    static Vector2[] Circle(Vector2 c, float rad, float a0, float a1, int n)
    {
        var p = new Vector2[n + 1]; for (int k = 0; k <= n; k++) { float a = Mathf.Lerp(a0, a1, k / (float)n); p[k] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad; }
        return p;
    }
    // stoeprand en stoep langs één zijkant (side +1: naar de volgende arm, -1: naar de vorige) van arm a, van de vakrand tot s1
    static void SideWalk(System.Func<string, MeshBuilder> M, Frame fr, System.Func<int, float, float, Vector2> P, int a, float side, float s1)
    {
        for (float s0 = 0f; s0 < s1 - 1e-3f; s0 += 0.5f)
        {
            float s2 = Mathf.Min(s1, s0 + 0.5f);
            Kerb(M("rand"), fr, P(a, side * hwA, s0), P(a, side * hwA, s2), P(a, side * (hwA + KW), s0), P(a, side * (hwA + KW), s2), AY);
            Walk(M("stoep"), fr, P(a, side * (hwA + KW), s0), P(a, side * (hwA + KW), s2), P(a, side * hwS, s0), P(a, side * hwS, s2));
        }
    }

    // Een meertje: zand aan de kant, water iets lager, riet langs de oever. Grenst een buurvak ook aan een meer,
    // dan loopt het water door tot de gedeelde rand, zodat meerdere vakken één meer vormen.
    static void Lake(Dictionary<string, MeshBuilder> mb, Frame fr, int i, float h, int hash)
    {
        System.Func<string, MeshBuilder> M = k => { if (!mb.TryGetValue(k, out var x)) { x = new MeshBuilder(); mb[k] = x; } return x; };
        var c = Planet.Cells[i]; int n = fr.poly.Length;
        // per rand: open naar een buurmeer? De buur met dezelfde richting als de rand (middelpunt van de rand ligt richting die buur)
        var open = new bool[n];
        for (int e = 0; e < n; e++)
        {
            Vector2 mid = (fr.poly[e] + fr.poly[(e + 1) % n]) * 0.5f; int best = -1; float bd = -1f;
            foreach (int j in c.nb) { Vector3 d3 = Planet.Cells[j].dir * Planet.R - fr.o; Vector2 q = new Vector2(Vector3.Dot(d3, fr.e), Vector3.Dot(d3, fr.n)).normalized; float d = Vector2.Dot(q, mid.normalized); if (d > bd) { bd = d; best = j; } }
            open[e] = best >= 0 && Cells.TryGetValue(best, out var l) && l.Count > 0 && l[0] == "meer";
        }
        // waterrand: hoekpunt op de rand als beide aangrenzende randen open zijn, anders naar binnen (golvend)
        var wp = new Vector2[n]; var rnd = new System.Random(hash);
        for (int k = 0; k < n; k++)
        {
            bool both = open[(k + n - 1) % n] && open[k];
            float kk = both ? 1.0f : (open[(k + n - 1) % n] || open[k]) ? 0.9f : 0.68f + (float)rnd.NextDouble() * 0.1f;
            wp[k] = fr.poly[k] * kk;
        }
        float wy = h - 0.35f;
        for (int k = 0; k < n; k++)
        {
            Vector2 a = wp[k], b = wp[(k + 1) % n], am = open[k] ? (fr.poly[k] + fr.poly[(k + 1) % n]) * 0.5f : (a + b) * 0.5f * 0.95f;
            M("water").Tri(fr.P(0, wy, 0), fr.P(a.x, wy, a.y), fr.P(am.x, wy, am.y), fr.u); M("water").Tri(fr.P(0, wy, 0), fr.P(am.x, wy, am.y), fr.P(b.x, wy, b.y), fr.u);
            // oever: een helling van het zand (h+0.03) naar onder water (wy-0.3), en riet op gesloten randen
            Vector2 oa = fr.poly[k] * 0.98f, ob = fr.poly[(k + 1) % n] * 0.98f;
            if (!open[k]) {
                // buitenrand (zand, iets de bodem in) en de oever: van de rand schuin omlaag tot onder water
                Vector2 pa = fr.poly[k], pb = fr.poly[(k + 1) % n], pm = (pa + pb) * 0.5f;
                M("zand").Quad(fr.P(pa.x, -0.25f, pa.y), fr.P(pb.x, -0.25f, pb.y), fr.P(pb.x, h + 0.03f, pb.y), fr.P(pa.x, h + 0.03f, pa.y), fr.D(pm.x, 0, pm.y));
                M("zand").Quad(fr.P(pa.x, h + 0.03f, pa.y), fr.P(pb.x, h + 0.03f, pb.y), fr.P(ob.x, h + 0.02f, ob.y), fr.P(oa.x, h + 0.02f, oa.y), fr.u);
                M("zand").Quad(fr.P(oa.x, h + 0.02f, oa.y), fr.P(ob.x, h + 0.02f, ob.y), fr.P(b.x, wy - 0.3f, b.y), fr.P(a.x, wy - 0.3f, a.y), fr.D(-am.x, 0.5f * am.magnitude, -am.y));
                int reeds = 2 + rnd.Next(0, 3);
                for (int r = 0; r < reeds; r++) { float t = 0.15f + (float)rnd.NextDouble() * 0.7f; Vector2 rp = Vector2.Lerp(a, b, t) * (0.97f + (float)rnd.NextDouble() * 0.06f); Cone(M("riet"), fr, rp.x, rp.y, 0.12f, wy - 0.1f, h + 0.9f + (float)rnd.NextDouble() * 0.5f, 0f); } }
        }
    }

    // Een pad: smal, zandkleurig, sluit aan op buurpaden en wegen
    static void Footpath(Dictionary<string, MeshBuilder> mb, Frame fr, int i)
    {
        System.Func<string, MeshBuilder> M = k => { if (!mb.TryGetValue(k, out var x)) { x = new MeshBuilder(); mb[k] = x; } return x; };
        skb = SkirtDepth(i); Disc(M("pad"), fr, 1.3f, 0.1f); Skirt(M("pad"), fr, 1.3f, skb, 0.1f);
        foreach (int j in Partners(i))
        {
            if (!IsWalk(j)) continue;
            Vector3 d3 = Planet.Cells[j].dir * Planet.R - fr.o;
            Vector2 q = new Vector2(Vector3.Dot(d3, fr.e), Vector3.Dot(d3, fr.n)) * 0.5f; float L = q.magnitude; Vector2 d = q / L, r = new Vector2(d.y, -d.x);
            float L2 = IsRoad(j) ? L - 4.4f : L;   // tot aan de stoep van een weg
            Strip(M("pad"), fr, d, r, 0f, L2, 1.1f, 0.105f); StripSides(M("pad"), fr, d, r, 0f, L2, 1.1f, skb, 0.105f);
        }
    }

    static Color ColorOf(string k)
    {
        switch (k)
        {
            case "muur": case "muur0": return new Color(0.93f, 0.86f, 0.72f); case "muur1": return new Color(0.72f, 0.42f, 0.32f); case "muur2": return new Color(0.88f, 0.88f, 0.84f); case "muur3": return new Color(0.55f, 0.5f, 0.45f);
            case "dak": case "dak0": return new Color(0.65f, 0.30f, 0.22f); case "dak1": return new Color(0.35f, 0.33f, 0.36f);
            case "paal": return new Color(0.25f, 0.27f, 0.3f); case "rand": return new Color(0.7f, 0.68f, 0.62f); case "lamp": return new Color(0.85f, 0.85f, 0.8f);
            case "kantoor": return new Color(0.58f, 0.68f, 0.78f); case "glas": return new Color(0.20f, 0.30f, 0.40f);
            case "plat": return new Color(0.50f, 0.50f, 0.53f); case "gras": return new Color(0.52f, 0.74f, 0.33f);
            case "water": return new Color(0.20f, 0.50f, 0.78f); case "zand": return new Color(0.86f, 0.79f, 0.55f);
            case "pad": return new Color(0.82f, 0.76f, 0.63f); case "blad": return new Color(0.20f, 0.50f, 0.22f);
            case "steen": return new Color(0.62f, 0.60f, 0.58f); case "stoep": return new Color(0.88f, 0.84f, 0.72f);
            case "asfalt": return new Color(0.27f, 0.28f, 0.30f); case "lijn": return Color.white;
            case "modder": return new Color(0.45f, 0.38f, 0.26f); case "riet": return new Color(0.45f, 0.6f, 0.25f);
            case "etalage": return new Color(0.55f, 0.68f, 0.75f); case "doek": return new Color(0.95f, 0.93f, 0.88f);
            case "luifel0": return new Color(0.78f, 0.2f, 0.18f); case "luifel1": return new Color(0.2f, 0.5f, 0.32f); case "luifel2": return new Color(0.18f, 0.35f, 0.62f);
            case "bord": return new Color(0.95f, 0.70f, 0.23f); case "klinker": return new Color(0.70f, 0.55f, 0.45f); case "hout": return new Color(0.52f, 0.36f, 0.22f);
            case "baksteen": return new Color(0.62f, 0.33f, 0.25f); case "leien": return new Color(0.24f, 0.27f, 0.32f); case "klok": return new Color(0.95f, 0.95f, 0.9f); case "goud": return new Color(0.9f, 0.72f, 0.25f);
            case "molen": return new Color(0.30f, 0.30f, 0.28f); case "kap": return new Color(0.42f, 0.36f, 0.26f); case "wiek": return new Color(0.35f, 0.22f, 0.15f); case "zeil": return new Color(0.92f, 0.9f, 0.84f);
            default: return new Color(0.40f, 0.28f, 0.18f);
        }
    }

    public static void Rebuild()
    {
        if (dirty.Count == 0) return;
        foreach (int ch in dirty) BuildChunk(ch);
        dirty.Clear();
        RebuildRoads();   // het wegennet: alleen de lijnen en knopen die veranderd zijn
    }

    // ---------- bouwvoorbeeld (het spookblok van de webversie) ----------
    // Kan blok 'type' op vak i? null = ja, anders de reden. Dezelfde regels als Place (voor blokken, niet voor wegen), zonder iets te veranderen.
    public static string CanPlace(int i, string type)
    {
        if (!Cells.TryGetValue(i, out var l) || l.Count == 0) return null;
        if (type == "pad" && l.Count == 1 && l[0] == "pad") return null;
        if (l[0] == "weg") return "Op een weg kun je niet bouwen; haal de weg eerst weg (rechtermuisknop)";
        if (l[0] == "wegdeel" && !Buildable(type)) return "Hier loopt een weg overheen: op de vrije helft passen alleen gebouwen en parken";
        if (type == "water" || type == "meer") return null;   // water vervangt wat er staat
        if (l[l.Count - 1] == "water" || l[l.Count - 1] == "meer") return null;
        if (l.Count >= 8) return "Hoger kan niet";
        if (l[l.Count - 1] == "toren" || l[l.Count - 1] == "molen") return "Op een " + l[l.Count - 1] + " kun je niets meer zetten";
        return null;
    }
    // De vorm van wat Place(i, type) zou toevoegen, of bij de gum de hele stapel, als één mesh rond het hart van het vak (origin).
    // null als er niets te tonen is (een wegvak: dat tekent het wegennet).
    public static Mesh GhostMesh(int i, string type, bool erase, out Vector3 origin)
    {
        origin = Planet.Cells[i].dir * (Planet.R + G(i));
        Cells.TryGetValue(i, out var cur); var list = cur != null ? new List<string>(cur) : new List<string>(); int from;
        if (erase) { if (list.Count == 0 || list[0] == "weg") return null; from = 0; }
        else
        {
            if (list.Count > 0 && list[0] == "weg") return null;
            if (type == "water" || type == "meer" || (list.Count > 0 && (list[list.Count - 1] == "water" || list[list.Count - 1] == "meer"))) { bool half = list.Count > 0 && list[0] == "wegdeel"; list.Clear(); if (half) list.Add("wegdeel"); }
            from = list.Count; list.Add(type);
        }
        var mb = new Dictionary<string, MeshBuilder>(); BuildCell(mb, i, list, new List<(Vector3, Quaternion, int)>(), from);
        var all = new MeshBuilder();
        foreach (var b in mb.Values) { int o = all.v.Count; foreach (var p in b.v) all.v.Add(p - origin); all.n.AddRange(b.n); foreach (int t in b.t) all.t.Add(o + t); }
        return all.v.Count == 0 ? null : all.Build();
    }

    // Eén vak tekenen: de stapel blokken (vanaf blok 'from'; het bouwvoorbeeld tekent alleen het nieuwe blok bovenop)
    static void BuildCell(Dictionary<string, MeshBuilder> mb, int i, List<string> list, List<(Vector3 pos, Quaternion rot, int hash)> mills, int from)
    {
        System.Func<string, MeshBuilder> M = k => { if (!mb.TryGetValue(k, out var x)) { x = new MeshBuilder(); mb[k] = x; } return x; };
        var fr = new Frame(i);
            if (list[0] == "weg") { if (Under.ContainsKey(i)) { UnderMesh(mb, fr, i, i * 73856093); BridgePillar(M, fr, i); } return; }   // de weg zelf tekent het wegennet (PlanetRoads.cs); hier alleen wat onder een brug ligt
            if (list[0] == "wegdeel")   // half vak naast een schuine weg: de helft naar de weg toe is zandvloer; wat erop gebouwd is staat op de vrije helft
            {
                var rest = list.GetRange(1, list.Count - 1);
                if (!HalfNormal(i, out var hn)) { if (rest.Count == 0) { if (from == 0) Top(M("zand"), fr, 0.97f, 0.06f); return; } }   // zonder bekende weg: gewoon een vak
                else
                {
                    var hd = HalfDirs(i, hn); var lp = new List<Vector3>();
                    foreach (var d in hd) { Vector3 w = d * Planet.R - fr.o; lp.Add(new Vector3(Vector3.Dot(w, fr.e), 0f, Vector3.Dot(w, fr.n))); }
                    if (from == 0) for (int q = 1; q + 1 < lp.Count; q++) M("zand").Tri(fr.P(lp[0].x, 0.06f, lp[0].z), fr.P(lp[q].x, 0.06f, lp[q].z), fr.P(lp[q + 1].x, 0.06f, lp[q + 1].z), fr.u);
                    if (rest.Count == 0) return;
                    var fd = HalfDirs(i, hn, true);
                    if (fd.Count >= 3)
                    {
                        Vector3 cen = Vector3.zero; foreach (var d in fd) cen += d; cen = (cen / fd.Count).normalized; Vector3 o2 = cen * (Planet.R + G(i));
                        var pl = new Vector2[fd.Count]; float rs = 0f;
                        for (int k2 = 0; k2 < fd.Count; k2++) { Vector3 w = fd[k2] * Planet.R - o2; pl[k2] = new Vector2(Vector3.Dot(w, fr.e), Vector3.Dot(w, fr.n)) * 0.86f; rs += pl[k2].magnitude; }
                        fr = new Frame(fr, o2, pl, rs / fd.Count);
                    }
                }
                list = rest; from = Mathf.Max(0, from - 1);
            }
            if (list.Count == 1 && list[0] == "meer") { Lake(mb, fr, i, 0f, i * 73856093); return; }
            if (list.Count == 1 && list[0] == "pad") { Footpath(mb, fr, i); return; }
            float h = 0f; int hash = i * 73856093;
            for (int k = 0; k < list.Count; k++)
            {
                string t = list[k]; bool top = k == list.Count - 1; float h1 = h + Blocks.Height(t), h0 = k == 0 ? -0.2f : h;   // onderste blok iets de bodem in (de bol loopt af naar de hoeken)
                if (k < from) { h = h1; continue; }   // bouwvoorbeeld: alleen het nieuwe blok bovenop
                switch (t)
                {
                    case "woning":
                        { string mu = "muur" + ((hash >> 3) & 3), dk = "dak" + ((hash >> 7) & 1);
                        Wall(M(mu), fr, 0.78f, h0, h1); Windows(M("glas"), fr, 0.78f, h + 1.0f, h + 2.0f, 0.18f, 0.40f); Windows(M("glas"), fr, 0.78f, h + 1.0f, h + 2.0f, 0.60f, 0.82f);
                        if (top) { Roof(M(dk), fr, 0.84f, h1); Top(M(dk), fr, 0.84f, h1 - 0.02f); } else Top(M(mu), fr, 0.78f, h1); }
                        break;
                    case "kantoor":
                        Wall(M("kantoor"), fr, 0.9f, h0, h1); Windows(M("glas"), fr, 0.9f, h + 0.5f, h + 1.2f, 0.08f, 0.92f); Windows(M("glas"), fr, 0.9f, h + 1.7f, h + 2.4f, 0.08f, 0.92f);
                        Top(M("plat"), fr, 0.9f, h1); if (top) { Wall(M("plat"), fr, 0.9f, h1, h1 + 0.25f); Top(M("plat"), fr, 0.9f, h1 + 0.25f); }
                        break;
                    case "dak":
                        Cone(M("dak"), fr, 0, 0, fr.rad * 0.95f, h, h1, fr.rad * 0.5f); Top(M("dak"), fr, 0.5f, h1);
                        break;
                    case "park":
                        Top(M("gras"), fr, 0.98f, h + 0.05f); if (k == 0) Wall(M("gras"), fr, 0.98f, -0.2f, h + 0.05f);
                        for (int j = 0; j < 3; j++)
                        {
                            float a = ((hash >> (j * 5)) & 31) / 31f * 6.28f + j * 2.1f, rr = (0.25f + ((hash >> (j * 3 + 2)) & 7) / 7f * 0.45f) * fr.rad;
                            float tx = Mathf.Cos(a) * rr, tz = Mathf.Sin(a) * rr;
                            Cone(M("stam"), fr, tx, tz, 0.22f, h + 0.05f, h + 1.2f, 0.18f); Cone(M("blad"), fr, tx, tz, 1.5f, h + 1.0f, h + 4.0f, 0f);
                        }
                        break;
                    case "water":
                        Top(M("water"), fr, 1.0f, h + 0.03f); if (k == 0) Wall(M("water"), fr, 1.0f, -0.2f, h + 0.03f);
                        break;
                    case "meer":
                        Lake(mb, fr, i, h, hash);
                        break;
                    case "kuil":   // uitgraving: de bodem 3 m lager, zeshoekige grashellingen van de rand naar beneden
                        for (int e2 = 0; e2 < fr.poly.Length; e2++)
                        {
                            Vector2 a2 = C(fr, e2, 1.0f), b2 = C(fr, e2 + 1, 1.0f), a3 = C(fr, e2, 0.55f), b3 = C(fr, e2 + 1, 0.55f), m2 = (a2 + b2) * 0.5f;
                            M("gras").Quad(fr.P(a2.x, h, a2.y), fr.P(b2.x, h, b2.y), fr.P(b3.x, h1, b3.y), fr.P(a3.x, h1, a3.y), fr.D(-m2.x, 2f, -m2.y));
                        }
                        Top(M("gras"), fr, 0.55f, h1);
                        break;
                    case "pad":
                        Wall(M("pad"), fr, 0.72f, h0, h1); Top(M("pad"), fr, 0.72f, h1);
                        break;
                    case "heuvel":
                        Cone(M("gras"), fr, 0, 0, fr.rad * 1.0f, h0, h1, fr.rad * 0.55f); Cap6(M("gras"), fr, fr.rad * 0.55f, h1);
                        break;
                    case "blok":
                        Wall(M("steen"), fr, 0.96f, h0, h1); Top(M("steen"), fr, 0.96f, h1);
                        break;
                    case "winkel":   // winkel: grote etalages (lichten 's avonds op), een gestreepte luifel en een naambord; erboven kan gewoon gewoond worden
                        { string mu = "muur" + ((hash >> 3) & 3);
                        Wall(M(mu), fr, 0.86f, h0, h1); Windows(M("etalage"), fr, 0.86f, h + 0.3f, h + 2.0f, 0.14f, 0.86f); Windows(M("bord"), fr, 0.86f, h + 2.45f, h + 2.85f, 0.3f, 0.7f);
                        Awning(M, fr, 0.87f, h + 2.35f, "luifel" + ((hash >> 5) & 0xffff) % 3);
                        Top(M(top ? "plat" : mu), fr, 0.86f, h1); if (top) { Wall(M("plat"), fr, 0.86f, h1, h1 + 0.3f); Top(M("plat"), fr, 0.86f, h1 + 0.3f); } }
                        break;
                    case "plein":   // plein: klinkers, een fontein in het midden, bankjes eromheen en twee lantaarns
                        { Top(M("klinker"), fr, 1.0f, h1); Wall(M("klinker"), fr, 1.0f, h0, h1); Top(M("steen"), fr, 0.62f, h1 + 0.005f); Top(M("klinker"), fr, 0.5f, h1 + 0.01f);
                        if (k > 0 || FountainHere(i)) { Skirt(M("steen"), fr, 2.4f, h1, h1 + 0.55f); Disc(M("steen"), fr, 2.4f, h1 + 0.55f); Disc(M("water"), fr, 2.1f, h1 + 0.57f);
                        Cone(M("steen"), fr, 0f, 0f, 0.3f, h1 + 0.55f, h1 + 1.6f, 0.22f); Cone(M("steen"), fr, 0f, 0f, 0.25f, h1 + 1.45f, h1 + 1.75f, 1.0f); Disc(M("water"), fr, 0.9f, h1 + 1.76f); }
                        float a0 = (hash & 63) / 63f * Mathf.PI * 2f;
                        for (int j = 0; j < 3; j++)
                        {
                            float a = a0 + j * Mathf.PI * 2f / 3f, rr = fr.rad * 0.6f, bx = Mathf.Cos(a) * rr, bz = Mathf.Sin(a) * rr, tang = a + Mathf.PI / 2f;
                            Box(M("hout"), fr, bx, bz, tang, 0.9f, 0.24f, h1 + 0.42f, h1 + 0.5f);
                            Box(M("hout"), fr, bx + Mathf.Cos(a) * 0.24f, bz + Mathf.Sin(a) * 0.24f, tang, 0.9f, 0.04f, h1 + 0.5f, h1 + 0.95f);
                            for (int s2 = -1; s2 <= 1; s2 += 2) Box(M("paal"), fr, bx + Mathf.Cos(tang) * 0.75f * s2, bz + Mathf.Sin(tang) * 0.75f * s2, tang, 0.05f, 0.2f, h1, h1 + 0.42f);
                            if (j < 2) { float la = a + Mathf.PI / 3f, lx = Mathf.Cos(la) * fr.rad * 0.78f, lz = Mathf.Sin(la) * fr.rad * 0.78f; Cone(M("paal"), fr, lx, lz, 0.08f, h1, h1 + 4.2f, 0.06f); Cone(M("lamp"), fr, lx, lz, 0.28f, h1 + 3.9f, h1 + 4.45f, 0f); }
                        } }
                        break;
                    case "toren":   // kerktoren: stenen voet, bakstenen schacht met klok en galmgaten, leien spits met een gouden knop
                        Wall(M("steen"), fr, 0.55f, h0, h + 2.2f); Top(M("steen"), fr, 0.55f, h + 2.2f);
                        Wall(M("baksteen"), fr, 0.4f, h + 2.2f, h1 - 0.3f); Windows(M("klok"), fr, 0.4f, h1 - 4.6f, h1 - 3.5f, 0.3f, 0.7f); Windows(M("glas"), fr, 0.4f, h1 - 2.5f, h1 - 1.0f, 0.36f, 0.64f);
                        Wall(M("steen"), fr, 0.45f, h1 - 0.3f, h1); Top(M("steen"), fr, 0.45f, h1);
                        if (top) { Spire(M("leien"), fr, 0.42f, h1, 8f); Cone(M("goud"), fr, 0f, 0f, 0.14f, h1 + 7.6f, h1 + 8.6f, 0f); }
                        break;
                    case "molen":   // molen: taps toelopende romp, een kap en vier draaiende wieken, met het gezicht naar een willekeurige kant
                        { float r0 = fr.rad * 0.5f, r1 = fr.rad * 0.3f, a = ((hash >> 4) & 7) / 8f * Mathf.PI * 2f; Vector2 fd = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        Cone(M("steen"), fr, 0f, 0f, r0 * 1.06f, h0, h + 0.7f, r0 * 1.04f);
                        Cone(M("molen"), fr, 0f, 0f, r0, h + 0.7f, h1 - 1.6f, r1); Cone(M("kap"), fr, 0f, 0f, r1 * 1.18f, h1 - 1.6f, h1 + 0.8f, 0.2f);
                        Box(M("hout"), fr, fd.x * (r0 - 0.1f), fd.y * (r0 - 0.1f), a, 0.35f, 0.6f, h + 0.7f, h + 2.6f);   // deur
                        Box(M("wiek"), fr, fd.x * (r1 + r0 + 0.5f) * 0.5f, fd.y * (r1 + r0 + 0.5f) * 0.5f, a, (r0 + 0.5f - r1) * 0.5f + 0.5f, 0.15f, h1 - 1.15f, h1 - 0.85f);   // as
                        float hub = r0 + 0.5f; mills.Add((fr.P(fd.x * hub, h1 - 1.0f, fd.y * hub), Geo.Look(fr.D(fd.x, 0f, fd.y), fr.u), hash)); }
                        break;
                }
                h = h1;
            }
    }

    static void BuildChunk(int ch)
    {
        BuildGround(ch);
        if (chunks.TryGetValue(ch, out var old) && old != null) UnityEngine.Object.Destroy(old.gameObject);
        chunks.Remove(ch);
        var mb = new Dictionary<string, MeshBuilder>();
        System.Func<string, MeshBuilder> M = k => { if (!mb.TryGetValue(k, out var x)) { x = new MeshBuilder(); mb[k] = x; } return x; };
        int i0 = ch * ChunkN, i1 = Mathf.Min(Planet.Cells.Length, i0 + ChunkN);
        var mills = new List<(Vector3 pos, Quaternion rot, int hash)>();   // molenwieken: losse objecten die draaien
        for (int i = i0; i < i1; i++) if (Cells.TryGetValue(i, out var list) && list.Count > 0) BuildCell(mb, i, list, mills, 0);
        if (mb.Count == 0) return;
        var go = new GameObject("Deel " + ch); go.transform.SetParent(root, false); chunks[ch] = go.transform;
        foreach (var kv in mb)
        {
            if (kv.Value.v.Count == 0) continue;
            var child = new GameObject(kv.Key); child.transform.SetParent(go.transform, false);
            child.layer = (kv.Key == "asfalt" || kv.Key == "stoep" || kv.Key == "rand" || kv.Key == "lijn" || kv.Key == "paal" || kv.Key == "lamp" || kv.Key == "pad") ? Layers.Weg : Layers.Gebouw;
            var mesh = kv.Value.Build(); child.AddComponent<MeshFilter>().sharedMesh = mesh; child.AddComponent<MeshRenderer>().sharedMaterial = kv.Key == "lamp" || kv.Key == "etalage" ? Mats.GetUnlit("p_" + kv.Key, ColorOf(kv.Key)) : Mats.Get("p_" + kv.Key, ColorOf(kv.Key));
            if (kv.Key != "water" && kv.Key != "zand" && kv.Key != "lijn" && kv.Key != "lamp" && kv.Key != "riet" && kv.Key != "doek" && !kv.Key.StartsWith("luifel")) child.AddComponent<MeshCollider>().sharedMesh = mesh;
        }
        foreach (var ml in mills) MakeWieken(go.transform, ml.pos, ml.rot, ml.hash);
    }
}

// Draaiende molenwieken: harder bij storm, wat sneller bij regen, en een beetje onregelmatig zoals echte wind
public class Wieken : MonoBehaviour
{
    public float Speed = 40f; float gust;
    void Update()
    {
        gust = Mathf.Lerp(gust, Mathf.PerlinNoise(Time.time * 0.15f, Speed) - 0.5f, Time.deltaTime);
        float s = Speed * (1f + gust * 0.6f + Weather.Storm * 1.6f + Weather.Rain * 0.3f);
        transform.Rotate(0f, 0f, -s * Time.deltaTime, Space.Self);
    }
}
