using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

// Evaluatie van een AI-stad, om versies van de planner te vergelijken: meetcijfers (Measure) en een archief per run in
// persistentDataPath/ai-runs (Archive): een plattegrond van bovenaf (PNG, een kleur per soort blok, de stad van de AI fel, de rest
// gedimd) en de hele stad als JSON (PlanetCity.Snapshot), zodat je hem later weer kunt openen. Los van de interface.
public static partial class PlanetAI
{
    public class Metrics
    {
        public int beauty, moves, cells, roads, deadEnds, roundabouts, buildings; public float asphalt, symmetry; public string rows;
        // het wegennet beoordeeld: losse stukken (niet aan het grootste net), scherpe bochten (120° of meer), kruispunten vlak naast elkaar,
        // straten die zij aan zij lopen zonder verbinding, rotondes te dicht bij elkaar (minder dan RotGap vakken)
        public int loose, sharp, twinX, sideBySide, rotClose;
        // één regel voor het scoreboek
        public string Line => "doodlopend " + deadEnds + ";rotondes " + roundabouts + ";losse stukken " + loose + ";scherpe bochten " + sharp + ";kruispunten naast elkaar " + twinX + ";zij aan zij " + sideBySide + ";rotondes te dicht " + rotClose + ";asfalt " + Mathf.RoundToInt(asphalt * 100f) + "%;symmetrie " + Mathf.RoundToInt(symmetry * 100f) + "%;" + rows;
    }

    public static Metrics Measure()
    {
        var b = Beauty.Compute(); var m = new Metrics { beauty = b.score, moves = Moves, cells = Owned.Count };
        int same = 0, tried = 0;
        foreach (int c in Owned)
        {
            if (PlanetCity.IsRoad(c)) { m.roads++; if (PlanetCity.DeadEnd(c)) m.deadEnds++; if (PlanetCity.IsRot(c)) m.roundabouts++; continue; }
            if (!PlanetCity.Cells.TryGetValue(c, out var l) || l.Count == 0 || l[0] == "pad") continue;
            if (IsBuilding(l)) m.buildings++;
            string t = Top(c);   // symmetrie: staat hetzelfde blok om de 60° rondom het centrum?
            for (int k = 1; k < 6; k++) { tried++; if (Top(Turn(c, k)) == t) same++; }
        }
        Review(m);
        m.asphalt = m.cells > 0 ? m.roads / (float)m.cells : 0f; m.symmetry = tried > 0 ? same / (float)tried : 0f;
        var sb = new StringBuilder(); foreach (var r in b.rows) sb.Append(r.name.ToLower() + " " + (r.v < 0f ? "-" : Mathf.RoundToInt(r.v * 100f).ToString()) + ",");
        m.rows = sb.ToString().TrimEnd(','); return m;
    }

    static Vector3 Dir2(int a, int b) { Vector3 da = Planet.Cells[a].dir, t = Planet.Cells[b].dir - da; t -= da * Vector3.Dot(t, da); return t.normalized; }
    public static readonly List<(int a, int b)> SideBySide = new List<(int, int)>();   // de paren van de laatste beoordeling (voor een test of een markering)
    static bool SharePartner(int a, int b) { foreach (int x in PlanetCity.Partners(a)) if (PlanetCity.Linked(x, b)) return true; return false; }
    static void Review(Metrics m)
    {
        SideBySide.Clear();
        var roads = new List<int>(); foreach (int c in Owned) if (PlanetCity.IsRoad(c)) roads.Add(c);
        // losse stukken: samenhangende delen van het eigen wegennet (via de verbindingen), alles behalve het grootste telt
        var seen = new HashSet<int>(); var sizes = new List<int>();
        foreach (int c in roads)
        {
            if (!seen.Add(c)) continue; int n = 0; var q = new Queue<int>(); q.Enqueue(c);
            while (q.Count > 0) { int x = q.Dequeue(); n++; foreach (int j in PlanetCity.Partners(x)) if (PlanetCity.IsRoad(j) && seen.Add(j)) q.Enqueue(j); }
            sizes.Add(n);
        }
        sizes.Sort(); m.loose = 0; for (int i = 0; i < sizes.Count - 1; i++) m.loose += sizes[i];
        foreach (int c in roads)
        {
            var p = new List<int>(); foreach (int j in PlanetCity.Partners(c)) if (PlanetCity.IsRoad(j)) p.Add(j);
            if (p.Count == 2 && !PlanetCity.IsRot(c) && Vector3.Angle(Dir2(p[0], c), Dir2(c, p[1])) > 100f) m.sharp++;   // een bocht van 120° of scherper
            bool node = p.Count >= 3 && !PlanetCity.IsRot(c);
            foreach (int j in Planet.Cells[c].nb)
            {
                if (j < c || !PlanetCity.IsRoad(j)) continue;
                if (PlanetCity.Linked(c, j)) { if (node && !PlanetCity.IsRot(j) && PlanetCity.LinkCount(j) >= 3) m.twinX++; }
                else if (PlanetCity.RotOf(c) < 0 && PlanetCity.RotOf(j) < 0 && !PlanetCity.Underpass(c) && !PlanetCity.Underpass(j) && Owned.Contains(j) && !SharePartner(c, j))   // twee armen van hetzelfde kruispunt (60° uit elkaar) raken elkaar altijd: dat telt niet
                { m.sideBySide++; if (SideBySide.Count < 400) SideBySide.Add((c, j)); }
            }
            if (PlanetCity.IsRot(c) && RotNear(c, RotGap - 1)) m.rotClose++;
        }
    }
    static string RunDir => Path.Combine(Application.persistentDataPath, "ai-runs");
    // de plattegrond en de stad bewaren; geeft de bestandsnaam (zonder extensie) of null
    public static string Archive(string why)
    {
        if (Start < 0) return null;
        try
        {
            Directory.CreateDirectory(RunDir);
            string name = System.DateTime.Now.ToString("yyyyMMdd-HHmmss") + "_v" + Version.Split(' ')[0] + "_" + StyleNames[(int)Kind].ToLower().Replace(' ', '-') + "_" + Moves + "-zetten_" + why;
            File.WriteAllBytes(Path.Combine(RunDir, name + ".png"), Map(512).EncodeToPNG());
            File.WriteAllText(Path.Combine(RunDir, name + ".json"), PlanetCity.Snapshot());
            return name;
        }
        catch (System.Exception e) { Debug.LogWarning("Archief van de AI-stad niet bewaard: " + e.Message); return null; }
    }

    // ---------- de plattegrond: elk vak binnen het bereik als zeshoek (of vijfhoek) van bovenaf, in het raakvlak van het startvak ----------
    static Color MapColor(int c, out bool own)
    {
        own = Owned.Contains(c);
        if (!PlanetCity.Cells.TryGetValue(c, out var l) || l.Count == 0)
            return PlanetCity.Wet(c) ? new Color(0.36f, 0.58f, 0.80f) : Color.Lerp(new Color(0.62f, 0.75f, 0.50f), new Color(0.86f, 0.86f, 0.80f), Mathf.Clamp01(PlanetCity.G(c) / 50f));
        if (PlanetCity.IsRot(c)) return new Color(0.20f, 0.20f, 0.22f);
        if (PlanetCity.IsRoad(c)) { byte s = PlanetCity.StyleOf(c); return s == 2 ? new Color(0.30f, 0.40f, 0.30f) : s == 1 ? new Color(0.42f, 0.42f, 0.45f) : new Color(0.30f, 0.30f, 0.32f); }
        if (PlanetCity.IsPath(c)) return PlanetCity.IsBike(c) ? new Color(0.78f, 0.36f, 0.32f) : new Color(0.86f, 0.78f, 0.60f);
        string top = l[l.Count - 1]; int floors = 0; foreach (var b in l) if (b == "woning" || b == "kantoor" || b == "winkel") floors++;
        float dark = Mathf.Clamp01((floors - 1) * 0.12f);   // hoger: donkerder
        Color col;
        switch (l[0] == "wegdeel" && l.Count > 1 ? l[1] : top)
        {
            case "woning": col = new Color(0.78f, 0.42f, 0.34f); break;
            case "kantoor": col = new Color(0.42f, 0.56f, 0.78f); break;
            case "winkel": col = new Color(0.95f, 0.62f, 0.25f); break;
            case "plein": col = new Color(0.80f, 0.80f, 0.84f); break;   // lichtgrijs: anders lijkt het op een pad
            case "park": col = new Color(0.30f, 0.58f, 0.28f); break;
            case "water": case "meer": col = new Color(0.25f, 0.50f, 0.85f); break;
            case "toren": col = new Color(0.55f, 0.35f, 0.70f); break;
            case "molen": col = new Color(0.97f, 0.97f, 0.95f); break;
            case "dak": col = new Color(0.55f, 0.32f, 0.20f); break;
            case "blok": col = new Color(0.60f, 0.58f, 0.56f); break;
            case "wegdeel": col = new Color(0.36f, 0.36f, 0.38f); break;
            default: col = new Color(0.65f, 0.65f, 0.65f); break;
        }
        if (top == "winkel" || l.Contains("winkel")) col = Color.Lerp(col, new Color(0.95f, 0.62f, 0.25f), 0.5f);
        return Color.Lerp(col, Color.black, dark);
    }
    public static Texture2D Map(int size)
    {
        var S = Planet.Cells[Start]; float half = (MaxRange + 1.5f) * P, scale = size / (2f * half);
        var px = new Color32[size * size]; var bg = (Color32)new Color(0.12f, 0.14f, 0.17f); for (int i = 0; i < px.Length; i++) px[i] = bg;
        Vector2 To(Vector3 dir) { Vector3 v = dir * Planet.R - S.dir * Planet.R; return new Vector2((Vector3.Dot(v, S.east) + half) * scale, (Vector3.Dot(v, S.north) + half) * scale); }
        foreach (var kv in dist)
        {
            int c = kv.Key; var cell = Planet.Cells[c]; var col = MapColor(c, out bool own);
            if (!own && PlanetCity.Cells.ContainsKey(c)) col = Color.Lerp(col, new Color(0.5f, 0.5f, 0.5f), 0.6f);   // van de speler: gedimd
            else if (!own) col = Color.Lerp(col, bg, 0.35f);
            var poly = new Vector2[cell.corners.Length]; float x0 = 1e9f, x1 = -1e9f, y0 = 1e9f, y1 = -1e9f;
            for (int k = 0; k < poly.Length; k++) { poly[k] = To(cell.corners[k]); x0 = Mathf.Min(x0, poly[k].x); x1 = Mathf.Max(x1, poly[k].x); y0 = Mathf.Min(y0, poly[k].y); y1 = Mathf.Max(y1, poly[k].y); }
            Color32 edge = Color.Lerp(col, Color.black, 0.35f), fill = col; var inner = Shrink(poly, To(cell.dir), 0.86f);   // een randje, zodat je de vakken ziet
            for (int y = Mathf.Max(0, (int)y0); y <= Mathf.Min(size - 1, (int)y1); y++)
                for (int x = Mathf.Max(0, (int)x0); x <= Mathf.Min(size - 1, (int)x1); x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f); if (!Inside(poly, p)) continue;
                    px[y * size + x] = Inside(inner, p) ? fill : edge;
                }
        }
        // het startvak: een blauwe stip (de vlag van de AI)
        Vector2 sp = To(S.dir); int r = Mathf.Max(3, size / 120);
        for (int y = -r; y <= r; y++) for (int x = -r; x <= r; x++) if (x * x + y * y <= r * r) { int X = (int)sp.x + x, Y = (int)sp.y + y; if (X >= 0 && Y >= 0 && X < size && Y < size) px[Y * size + X] = new Color32(90, 194, 255, 255); }
        var tex = new Texture2D(size, size, TextureFormat.RGB24, false); tex.SetPixels32(px); tex.Apply(); return tex;
    }
    static bool Inside(Vector2[] poly, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) inside = !inside;
        return inside;
    }
    static Vector2[] Shrink(Vector2[] poly, Vector2 c, float k) { var o = new Vector2[poly.Length]; for (int i = 0; i < poly.Length; i++) o[i] = c + (poly[i] - c) * k; return o; }
}
