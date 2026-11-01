using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Voorbeeld van wegen met verschillende breedtes die op een knoop aansluiten: rechtdoor van breed naar gewoon, een T-splitsing,
// een laan die een gewone weg wordt, een knik en een kruispunt. Draait als wegvoorbeeld.txt in persistentDataPath staat (menu
// Raatstad > Wegvoorbeeld; AutoHerstart start dan Play). Bouwt in testslot 9 bij de zuidpool, maakt schermafbeeldingen in
// wegvoorbeeld/ en laat de stad daarna staan om zelf rond te kijken (de eigen stad staat veilig in zijn slot).
public class WegVoorbeeld : MonoBehaviour
{
    static string Flag => Path.Combine(Application.persistentDataPath, "wegvoorbeeld.txt");
    static string Dir => Path.Combine(Application.persistentDataPath, "wegvoorbeeld");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!File.Exists(Flag)) return; mode = File.ReadAllText(Flag).Trim(); File.Delete(Flag);   // tekst "fiets": de fietspaden en de startstad
        Application.runInBackground = true;
        new GameObject("Wegvoorbeeld").AddComponent<WegVoorbeeld>();
    }

    static string mode = ""; readonly List<int> junctions = new List<int>();
    Camera cam; PlanetBuilder pb;

    IEnumerator Start()
    {
        if (Directory.Exists(Dir)) foreach (var f in Directory.GetFiles(Dir)) File.Delete(f); else Directory.CreateDirectory(Dir);
        for (int k = 0; k < 600 && (pb = FindAnyObjectByType<PlanetBuilder>()) == null; k++) yield return null;
        yield return null; yield return null;
        cam = Camera.main; if (pb != null) pb.enabled = false;
        PlanetCity.Slot = 9; DayNight.Hour = 12f; DayNight.Speed = 0f;
        PlanetCity.ClearAll(); PlanetCity.DragLevel = 0; PlanetCity.DragUnder = false; PlanetCity.DragOneWay = false;

        int home = At(Planet.South, 1.0f, 160f);
        float D60 = Mathf.PI / 3f;
        if (mode == "fiets") { yield return Bikes(home); yield break; }
        if (mode == "rotonde") { yield return Roundabouts(home); yield break; }
        var scenes = new (string name, int c)[] { ("1_rechtdoor_breed_naar_gewoon", home), ("2_T_breed_met_gewone_zijweg", At(home, 0f, 150f)), ("3_laan_naar_gewone_weg", At(home, 2f * D60, 150f)), ("4_knik_breed_naar_gewoon", At(home, 4f * D60, 150f)), ("5_kruispunt_breed_en_gewoon", At(home, 3f * D60, 150f)) };
        int endBroad = Leg(scenes[0].c, 0f, 1), endNormal = Leg(scenes[0].c, Mathf.PI, 0);
        Leg(scenes[1].c, 0f, 1); Leg(scenes[1].c, Mathf.PI, 1); Leg(scenes[1].c, D60, 0);
        Leg(scenes[2].c, 0f, 2); Leg(scenes[2].c, Mathf.PI, 0);
        Leg(scenes[3].c, 0f, 1); Leg(scenes[3].c, 2f * D60, 0);
        Leg(scenes[4].c, 0f, 1); Leg(scenes[4].c, Mathf.PI, 1); Leg(scenes[4].c, D60, 0); Leg(scenes[4].c, 4f * D60, 0);
        PlanetCity.DragStyle = 0;
        PlanetCity.Rebuild(); PlanetCity.Rebuild();
        yield return null; yield return null;

        foreach (var s in scenes)
        {
            Vector3 f = Dir2(s.c, 0.4f);
            yield return Shot(s.name + "_a", s.c, f, 60f, 88f);
            yield return Shot(s.name + "_b", s.c, f, 38f, 32f);
            yield return Shot(s.name + "_c", s.c, -Dir2(s.c, -0.5f), 30f, 25f);
        }
        foreach (var e in new[] { ("6_kop_brede_weg", endBroad), ("7_kop_gewone_weg", endNormal) })
        {
            if (e.Item2 < 0) continue; Vector3 f = Dir2(e.Item2, 0.4f);
            yield return Shot(e.Item1 + "_a", e.Item2, f, 30f, 88f);
            yield return Shot(e.Item1 + "_b", e.Item2, f, 22f, 35f);
        }
        File.WriteAllText(Path.Combine(Dir, "klaar.txt"), "klaar");

        // zelf rondkijken: de bediening weer aan, met de camera bij het eerste voorbeeld
        if (pb != null)
        {
            var fi = typeof(PlanetBuilder).GetField("focus", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (fi != null && fi.FieldType == typeof(Vector3)) fi.SetValue(pb, Planet.Cells[home].dir);
            pb.enabled = true;
        }
        DayNight.Speed = 1f;
    }

    // fiets- en voetpaden: een Y, een T, een schuin pad (30°) op een recht pad, een fietspad op een gewoon pad en op een weg,
    // en de startstad met zijn fietspaden; schermafbeeldingen bij elke kruising en knik van een fietspad
    IEnumerator Bikes(int home)
    {
        float D60 = Mathf.PI / 3f, D30 = Mathf.PI / 6f;
        var scenes = new (string name, int c)[] { ("f1_Y", home), ("f2_T", At(home, 0f, 150f)), ("f3_schuin_op_recht", At(home, 2f * D60, 150f)), ("f4_fiets_op_pad_en_weg", At(home, 4f * D60, 150f)) };
        PlanetCity.DragBike = true;
        Pad(scenes[0].c, 0f); Pad(scenes[0].c, 2f * D60); Pad(scenes[0].c, 4f * D60);
        Pad(scenes[1].c, 0f); Pad(scenes[1].c, Mathf.PI); Pad(scenes[1].c, D60);
        Pad(scenes[2].c, 0f); Pad(scenes[2].c, Mathf.PI); Pad(scenes[2].c, D30); Pad(scenes[2].c, Mathf.PI + 3f * D30);
        Pad(scenes[3].c, 0f); PlanetCity.DragBike = false; Pad(scenes[3].c, Mathf.PI); Leg(At(scenes[3].c, D60 * 2f, 40f), 0f, 0);
        // een weg over een fietspad: recht en schuin (30°); gelijkvloers een oversteek
        int c5 = At(home, 1f * D60, 150f), c6 = At(home, 5f * D60, 150f);
        PlanetCity.DragBike = true; Pad(c5, 0f); Pad(c5, Mathf.PI); Pad(c6, 0f); Pad(c6, Mathf.PI); PlanetCity.DragBike = false;
        RoadLine(At(c5, D60, 45f), At(c5, D60 + Mathf.PI, 45f)); RoadLine(At(c6, Mathf.PI / 2f, 45f), At(c6, -Mathf.PI / 2f, 45f));
        // een fietspad over een bestaande weg: recht en schuin; gelijkvloers een oversteek
        int c7 = At(home, 0.5f * D60, 300f), c8 = At(home, 2.5f * D60, 300f);
        RoadLine(At(c7, 0f, 45f), At(c7, Mathf.PI, 45f)); RoadLine(At(c8, 0f, 45f), At(c8, Mathf.PI, 45f));
        PlanetCity.DragBike = true;
        { var l = PlanetCity.LinePlan(At(c7, D60, 45f), At(c7, D60 + Mathf.PI, 45f)); PlanetCity.PlaceLine(l, "pad"); PlanetCity.SetLevel(l, 0, "pad"); }
        { var l = PlanetCity.LinePlan(At(c8, Mathf.PI / 2f, 45f), At(c8, -Mathf.PI / 2f, 45f)); PlanetCity.PlaceLine(l, "pad"); PlanetCity.SetLevel(l, 0, "pad"); }
        PlanetCity.DragBike = false;
        var extra = new List<(string, int)> { ("f5_weg_over_fietspad", c5), ("f6_schuine_weg_over_fietspad", c6), ("f7_fietspad_over_weg", c7), ("f8_schuin_fietspad_over_weg", c8) };
        PlanetCity.DragBike = false;
        int st = Planet.Starts[3]; PlanetCity.StartTown(st);
        PlanetCity.Rebuild(); PlanetCity.Rebuild(); yield return null; yield return null;
        foreach (var s in scenes) { yield return Shot(s.name + "_a", s.c, Dir2(s.c, 0.4f), 45f, 88f); yield return Shot(s.name + "_b", s.c, Dir2(s.c, 0.4f), 30f, 35f); }
        foreach (var e in extra) { yield return Shot(e.Item1 + "_a", e.Item2, Dir2(e.Item2, 0.4f), 45f, 88f); yield return Shot(e.Item1 + "_b", e.Item2, Dir2(e.Item2, 0.4f), 30f, 35f); }
        yield return Shot("s0_startstad", st, Dir2(st, 0.4f), 150f, 70f);
        // de kruisingen en knikken van fietspaden in de startstad
        int n = 0;
        foreach (var kv in PlanetCity.Cells)
        {
            int i = kv.Key; if (!PlanetCity.IsBike(i) || (Planet.Cells[i].dir - Planet.Cells[st].dir).magnitude * Planet.R > 200f) continue;
            var nb = new List<int>(); foreach (int j in PlanetCity.Partners(i)) if (PlanetCity.IsWalk(j)) nb.Add(j);
            bool bend = nb.Count == 2 && Vector3.Dot((Planet.Cells[nb[0]].dir - Planet.Cells[i].dir).normalized, (Planet.Cells[nb[1]].dir - Planet.Cells[i].dir).normalized) > -0.95f;
            if (nb.Count == 2 && !bend) continue;
            yield return Shot("s" + (++n) + "_" + (nb.Count == 2 ? "knik" : "kruising" + nb.Count), i, Dir2(i, 0.4f), 26f, 60f);
            if (mode == "fiets") junctions.Add(i);
            if (n >= 10) break;
        }
        // van boven, met en zonder terrein: steekt het terrein door het pad?
        var gr = typeof(PlanetCity).GetField("groundRoot", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)?.GetValue(null) as Transform;
        var info = new System.Text.StringBuilder();
        for (int k = 0; k < junctions.Count; k++)
        {
            int i = junctions[k]; info.Append("s").Append(k + 1).Append(" vak ").Append(i).Append(" G=").Append(PlanetCity.G(i).ToString("0.00")).Append(" bike=").Append(PlanetCity.IsBike(i)).Append(" [").Append(string.Join(",", PlanetCity.Cells[i])).Append("] buren:");
            foreach (int j in PlanetCity.Partners(i)) info.Append(' ').Append(j).Append(PlanetCity.IsBike(j) ? "F" : "P").Append(System.Array.IndexOf(Planet.Cells[i].nb, j) >= 0 ? "" : "(schuin)").Append(" G=").Append(PlanetCity.G(j).ToString("0.00"));
            info.AppendLine();
            yield return Shot("t" + (k + 1) + "_boven", i, Dir2(i, 0f), 30f, 89f);
            if (gr != null) { gr.gameObject.SetActive(false); yield return Shot("t" + (k + 1) + "_zonder_terrein", i, Dir2(i, 0f), 30f, 89f); gr.gameObject.SetActive(true); }
        }
        File.WriteAllText(Path.Combine(Dir, "kruisingen.txt"), info.ToString());
        // paden onder een weg door: bruggen over een pad en schuine kruisingen
        int nu = 0;
        foreach (var kv in PlanetCity.Under)
        {
            if (kv.Value != "pad" || (Planet.Cells[kv.Key].dir - Planet.Cells[st].dir).magnitude * Planet.R > 200f) continue;
            yield return Shot("u" + (++nu) + "_brug_boven", kv.Key, Dir2(kv.Key, 0f), 32f, 89f); yield return Shot("u" + nu + "_brug_schuin", kv.Key, Dir2(kv.Key, 0.5f), 26f, 30f);
            if (nu >= 3) break;
        }
        var xs = PlanetCity.EdgeCrossings();
        for (int k = 0; k < Mathf.Min(3, xs.Count); k++) { int c0 = xs[k].u; yield return Shot("x" + (k + 1) + "_schuin_boven", c0, Dir2(c0, 0f), 34f, 89f); yield return Shot("x" + (k + 1) + "_schuin", c0, Dir2(c0, 0.5f), 28f, 30f); }
        // padvakken zonder fietsdeel die aan een fietspad vastzitten
        var sb = new System.Text.StringBuilder();
        foreach (var kv in PlanetCity.Cells)
        {
            int i = kv.Key; if (!PlanetCity.IsWalk(i) || PlanetCity.IsRoad(i) || PlanetCity.IsBike(i)) continue;
            bool touch = false; foreach (int j in PlanetCity.Partners(i)) if (PlanetCity.IsBike(j)) touch = true; if (!touch) continue;
            sb.Append(i).Append(" [").Append(string.Join(",", kv.Value)).Append("] partners:");
            foreach (int j in PlanetCity.Partners(i)) sb.Append(' ').Append(j).Append(PlanetCity.IsBike(j) ? "F" : PlanetCity.IsRoad(j) ? "W" : PlanetCity.IsWalk(j) ? "P" : "?").Append(System.Array.IndexOf(Planet.Cells[i].nb, j) >= 0 ? "" : "(schuin)");
            sb.Append(" under=").Append(PlanetCity.Under.ContainsKey(i) ? PlanetCity.Under[i] : "-").AppendLine();
        }
        File.WriteAllText(Path.Combine(Dir, "fietsgaten.txt"), sb.ToString());
        File.WriteAllText(Path.Combine(Dir, "klaar.txt"), "klaar");
        if (pb != null) pb.enabled = true;
    }
    // rotondes: met vier gewone wegen (eerst de wegen, dan de rotonde), en een rotonde waar daarna een brede weg, een gewone weg
    // en een fietspad naartoe gesleept worden; daarna rijden er auto's
    IEnumerator Roundabouts(int home)
    {
        float D60 = Mathf.PI / 3f; int r1 = home, r2 = At(home, 0f, 160f);
        Leg(r1, 0f, 0); Leg(r1, 2f * D60, 0); Leg(r1, Mathf.PI, 0); Leg(r1, 5f * D60, 0); PlanetCity.PlaceRoundabout(r1);
        PlanetCity.PlaceRoundabout(r2);
        var rn = Planet.Cells[r2].nb;   // naar een vak van de ring slepen
        System.Func<int, int> Out = j => Planet.Nearest((Planet.Cells[j].dir * 5f - Planet.Cells[r2].dir * 4f).normalized * Planet.R);   // 4 vakken verder naar buiten
        RoadLine(Out(rn[1]), rn[1]); PlanetCity.DragStyle = 1; RoadLine(Out(rn[3]), rn[3]); PlanetCity.DragStyle = 0; RoadLine(Out(rn[5]), rn[5]);
        PlanetCity.DragBike = true; { var l = PlanetCity.LinePlan(rn[4], Out(rn[4])); PlanetCity.PlaceLine(l, "pad"); PlanetCity.SetLevel(l, 0, "pad"); } PlanetCity.DragBike = false;
        PlanetCity.Rebuild(); PlanetCity.Rebuild(); yield return null; yield return null;
        foreach (var (nm, c) in new[] { ("r1", r1), ("r2", r2) })
        {
            yield return Shot(nm + "_boven", c, Dir2(c, 0f), 50f, 89f); yield return Shot(nm + "_schuin", c, Dir2(c, 0.4f), 40f, 35f); yield return Shot(nm + "_laag", c, Dir2(c, 1.4f), 26f, 14f);
        }
        DayNight.Speed = 1f;
        for (int t = 0; t < 4; t++) { float w = Time.time + 6f; while (Time.time < w) yield return null; yield return Shot("r1_autos" + t, r1, Dir2(r1, 0.2f), 42f, 55f); }
        var info = new System.Text.StringBuilder(); foreach (int c in new[] { r1, r2 }) { info.Append("rotonde ").Append(c).Append(" armen:"); foreach (int j in PlanetCity.Partners(c)) info.Append(' ').Append(j).Append(PlanetCity.IsRoad(j) ? "W" : "P"); info.AppendLine(); }
        File.WriteAllText(Path.Combine(Dir, "rotondes.txt"), info.ToString());
        File.WriteAllText(Path.Combine(Dir, "klaar.txt"), "klaar");
        if (pb != null) pb.enabled = true;
    }
    static void RoadLine(int a, int b) { var l = PlanetCity.LinePlan(a, b); if (l == null || l.Count < 2) return; PlanetCity.PlaceLine(l, "weg"); PlanetCity.SetLevel(l, 0, "weg"); }
    static void Pad(int c, float ang, float len = 60f)
    {
        var l = PlanetCity.LinePlan(c, At(c, ang, len)); if (l == null || l.Count < 2) return;
        PlanetCity.PlaceLine(l, "pad"); PlanetCity.SetLevel(l, 0, "pad");
    }

    // een weg van len meter vanaf vak c in richting ang (0 = naar de eerste buur), in stijl st (0 weg, 1 brede weg, 2 laan)
    static int Leg(int c, float ang, byte st, float len = 70f)   // geeft het laatste vak terug (het doodlopende eind)
    {
        var l = PlanetCity.LinePlan(c, At(c, ang, len)); if (l == null || l.Count < 2) return -1;
        PlanetCity.DragStyle = st; PlanetCity.PlaceLine(l, "weg"); PlanetCity.SetLevel(l, 0, "weg"); return l[l.Count - 1];
    }
    static Vector3 Dir2(int c, float ang)
    {
        var S = Planet.Cells[c]; Vector3 n0 = Planet.Cells[S.nb[0]].dir - S.dir; n0 -= S.dir * Vector3.Dot(n0, S.dir);
        float a0 = Mathf.Atan2(Vector3.Dot(n0, S.north), Vector3.Dot(n0, S.east));
        return (S.east * Mathf.Cos(a0 + ang) + S.north * Mathf.Sin(a0 + ang)).normalized;
    }
    static int At(int c, float ang, float r) { var S = Planet.Cells[c]; return Planet.Nearest((S.dir * Mathf.Cos(r / Planet.R) + Dir2(c, ang) * Mathf.Sin(r / Planet.R)).normalized * Planet.R); }

    IEnumerator Shot(string name, int c, Vector3 fwd, float dist, float pitch)
    {
        Vector3 up = Planet.Cells[c].dir, target = up * (Planet.R + PlanetCity.G(c) + 0.5f);
        Vector3 pos = target + (-fwd * Mathf.Cos(pitch * Mathf.Deg2Rad) + up * Mathf.Sin(pitch * Mathf.Deg2Rad)) * dist;
        cam.transform.position = pos; cam.transform.rotation = Quaternion.LookRotation(target - pos, pitch > 80f ? fwd : up); cam.fieldOfView = 60f;
        yield return null; yield return null;
        ScreenCapture.CaptureScreenshot(Path.Combine(Dir, name + ".png"));
        yield return null; yield return null;
    }
}
