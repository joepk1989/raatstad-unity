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
        if (mode == "meten") { yield return MeetHier(); yield break; }
        if (mode == "autos") { yield return Autos(); yield break; }
        if (mode == "kijk")   // wat er standaard geladen wordt: slot, startstad, en het beeld van de camera bij het begin
        {
            var info = new System.Text.StringBuilder(); info.Append("slot ").Append(PlanetCity.Slot).Append(" vakken ").Append(PlanetCity.Cells.Count).Append(" startVer ").Append(PlanetCity.StartVer).Append(" startCell ").Append(PlanetCity.StartCell).Append(" noordpool ").Append(Planet.North).Append(" zuidpool ").Append(Planet.South).AppendLine();
            int lanen = 0; foreach (var kv in PlanetCity.Cells) if (PlanetCity.IsRoad(kv.Key) && PlanetCity.StyleOf(kv.Key) == 2) lanen++; info.Append("laanvakken ").Append(lanen).AppendLine();
            float best = 1e9f; int bc = -1; foreach (var kv in PlanetCity.Cells) if (kv.Value.Count > 0 && kv.Value[0] == "plein") { float d = Vector3.Distance(Planet.Cells[kv.Key].dir, Planet.Cells[Planet.North].dir) * Planet.R; if (d < best) { best = d; bc = kv.Key; } }
            info.Append("dichtstbijzijnde plein bij de noordpool: ").Append(bc).Append(" op ").Append(best.ToString("0")).Append(" m").AppendLine();
            File.WriteAllText(Path.Combine(Dir, "kijk.txt"), info.ToString());
            if (pb != null) pb.enabled = true; yield return null; yield return null; yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(Dir, "kijk.png")); yield return null; yield return null;
            File.WriteAllText(Path.Combine(Dir, "klaar.txt"), "klaar"); yield break;
        }   // de meting op de stad die nu geladen is (jouw eigen teststad)
        PlanetCity.Slot = 9; DayNight.Hour = 12f; DayNight.Speed = 0f;
        PlanetCity.ClearAll(); PlanetCity.DragLevel = 0; PlanetCity.DragUnder = false; PlanetCity.DragOneWay = false;

        int home = At(Planet.South, 1.0f, 160f);
        float D60 = Mathf.PI / 3f;
        if (mode == "fiets") { yield return Bikes(home); yield break; }
        if (mode == "rotonde") { yield return Roundabouts(home); yield break; }
        if (mode == "schuin") { yield return Diagonals(home); yield break; }
        if (mode == "verkeer") { yield return Verkeer(home); yield break; }
        if (mode == "stad" || mode == "stadmeten") { yield return Teststad(home, mode == "stadmeten"); yield break; }
        if (mode == "start" || mode == "startmeten") { yield return Startstad(); if (mode == "startmeten") { if (pb != null) pb.enabled = false; yield return MeetHier(); } yield break; }
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
        // een rotonde op een bestaand kruispunt: recht (vier armen), met een schuine weg erdoor, naast een kruispunt, en op een T
        int r3 = At(home, 2f * D60, 170f), r4 = At(home, 4f * D60, 170f), r5c = At(home, 3f * D60, 170f), r6 = At(home, 1f * D60, 300f);
        Leg(r3, 0f, 0); Leg(r3, 2f * D60, 0); Leg(r3, Mathf.PI, 0); Leg(r3, 5f * D60, 0);
        Leg(r4, 0f, 0); Leg(r4, Mathf.PI, 0); Leg(r4, Mathf.PI / 2f, 0); Leg(r4, -Mathf.PI / 2f, 0);
        Leg(r5c, 0f, 0); Leg(r5c, 2f * D60, 0); Leg(r5c, 4f * D60, 0);
        Leg(r6, 0f, 1); Leg(r6, Mathf.PI, 1); Leg(r6, D60 * 2f, 0);
        int r5 = Planet.Cells[r5c].nb[0];
        var msg = new System.Text.StringBuilder();
        foreach (var (nm, c) in new[] { ("r3", r3), ("r4", r4), ("r5", r5), ("r6", r6) }) msg.Append(nm).Append(": ").Append(PlanetCity.PlaceRoundabout(c) ?? "ok").AppendLine();
        File.WriteAllText(Path.Combine(Dir, "plaatsen.txt"), msg.ToString());
        PlanetCity.Rebuild(); PlanetCity.Rebuild(); yield return null; yield return null;
        foreach (var (nm, c) in new[] { ("r3", r3), ("r4", r4), ("r5", r5), ("r6", r6) }) { yield return Shot(nm + "_boven", c, Dir2(c, 0f), 55f, 89f); yield return Shot(nm + "_schuin", c, Dir2(c, 0.4f), 40f, 35f); }
        foreach (var (nm, c) in new[] { ("r1", r1), ("r2", r2) })
        {
            yield return Shot(nm + "_boven", c, Dir2(c, 0f), 50f, 89f); yield return Shot(nm + "_schuin", c, Dir2(c, 0.4f), 40f, 35f); yield return Shot(nm + "_laag", c, Dir2(c, 1.4f), 26f, 14f);
        }
        DayNight.Speed = 1f;
        for (int t = 0; t < 4; t++) { float w = Time.time + 6f; while (Time.time < w) yield return null; yield return Shot("r1_autos" + t, r1, Dir2(r1, 0.2f), 42f, 55f); }
        // voetgangers bij de rotondes: van welke arm naar welke (om de rotonde heen, niet omkeren)
        var walks = new System.Text.StringBuilder(); var seen = new HashSet<string>(); float until = Time.time + 25f;
        while (Time.time < until && PlanetLife.I != null)
        {
            foreach (var p in PlanetLife.I.People) if ((p.cellA == r1 || p.cellA == r2) && p.cellB != p.cellA && seen.Add(p.GetHashCode() + ":" + p.prevCell + ">" + p.cellB))
                walks.Append("rotonde ").Append(p.cellA).Append(": van ").Append(p.prevCell).Append(" naar ").Append(p.cellB).Append(p.prevCell == p.cellB ? "  OMKEREN" : "").AppendLine();
            yield return null;
        }
        File.WriteAllText(Path.Combine(Dir, "voetgangers.txt"), walks.ToString());
        yield return Shot("r2_mensen", r2, Dir2(r2, 0.3f), 30f, 45f);
        var info = new System.Text.StringBuilder(); foreach (int c in new[] { r1, r2 }) { info.Append("rotonde ").Append(c).Append(" armen:"); foreach (int j in PlanetCity.Partners(c)) info.Append(' ').Append(j).Append(PlanetCity.IsRoad(j) ? "W" : "P"); info.AppendLine(); }
        File.WriteAllText(Path.Combine(Dir, "rotondes.txt"), info.ToString());
        File.WriteAllText(Path.Combine(Dir, "klaar.txt"), "klaar");
        if (pb != null) pb.enabled = true;
    }
    // schuine wegen (30°, over halve vakken): kruisen, over een rechte weg, een T op een rechte weg, een bocht, huizen erlangs, naast elkaar
    IEnumerator Diagonals(int home)
    {
        float D60 = Mathf.PI / 3f, D30 = Mathf.PI / 6f;
        int s1 = home, s2 = At(home, 0f, 170f), s3 = At(home, 2f * D60, 170f), s4 = At(home, 4f * D60, 170f), s5 = At(home, 3f * D60, 170f), s6 = At(home, 1f * D60, 170f);
        Leg(s1, D30, 0); Leg(s1, D30 + Mathf.PI, 0); Leg(s1, D30 + 2f * D60, 0); Leg(s1, D30 + 2f * D60 + Mathf.PI, 0);
        Leg(s2, 0f, 0); Leg(s2, Mathf.PI, 0); RoadLine(At(s2, Mathf.PI / 2f, 60f), At(s2, -Mathf.PI / 2f, 60f));
        Leg(s3, 0f, 0); Leg(s3, Mathf.PI, 0); RoadLine(At(s3, Mathf.PI / 2f, 60f), s3);
        Leg(s4, D30, 0); Leg(s4, Mathf.PI, 0);
        Leg(s5, D30, 0); Leg(s5, D30 + Mathf.PI, 0);
        foreach (int c in new[] { At(s5, D30 + Mathf.PI / 2f, 12f), At(s5, D30 - Mathf.PI / 2f, 12f), At(At(s5, D30, 27f), D30 + Mathf.PI / 2f, 12f), At(At(s5, D30, 27f), D30 - Mathf.PI / 2f, 12f) }) PlanetCity.Place(c, "woning");
        Leg(s6, D30, 0); Leg(s6, D30 + Mathf.PI, 0); int s6b = At(s6, D30 + Mathf.PI / 2f, 16f); Leg(s6b, D30, 0); Leg(s6b, D30 + Mathf.PI, 0);
        PlanetCity.Rebuild(); PlanetCity.Rebuild(); yield return null; yield return null;
        foreach (var (nm, c) in new[] { ("s1_kruisen", s1), ("s2_over_recht", s2), ("s3_T_op_recht", s3), ("s4_bocht", s4), ("s5_huizen", s5), ("s6_naast_elkaar", s6) })
        { yield return Shot(nm + "_boven", c, Dir2(c, 0f), 55f, 89f); yield return Shot(nm + "_schuin", c, Dir2(c, 0.4f), 40f, 35f); }
        File.WriteAllText(Path.Combine(Dir, "klaar.txt"), "klaar");
        if (pb != null) pb.enabled = true;
    }
    // verkeer met fysica: een brede weg met twee gewone zijwegen (kruising en T), eerst zonder lichten (voorrang), dan met;
    // per fase: botsingen, hoe ver auto's van hun rijlijn raken, wie rijdt, wacht of vaststaat, en het gebruik van de linker strook
    IEnumerator Verkeer(int home)
    {
        float D60 = Mathf.PI / 3f; Hc = home; bouw = new System.Text.StringBuilder(); int x1 = Go(home, 0f, 55f), x2 = Go(home, Mathf.PI, 55f);
        Weg("breed oost", home, Go(home, 0f, 150f), 1); Weg("breed west", home, Go(home, Mathf.PI, 150f), 1);
        Weg("X a", x1, Go(x1, D60, 80f), 0); Weg("X b", x1, Go(x1, D60 + Mathf.PI, 80f), 0);   // kruising onder 60°
        Weg("T", x2, Go(x2, 2f * D60, 80f), 0);                                              // T
        File.WriteAllText(Path.Combine(Dir, "bouw.txt"), bouw.ToString());
        PlanetCity.Rebuild(); PlanetCity.Rebuild(); yield return null; yield return null;
        DayNight.Speed = 1f; var log = new System.Text.StringBuilder(); PlanetLife.CarBoost = File.Exists(Path.Combine(Application.persistentDataPath, "verkeer_rustig.txt")) ? 6 : 14;
        { float w0 = Time.time + 14f; while (Time.time < w0) yield return null; }   // de auto's laten verschijnen
        foreach (bool lights in new[] { false, true })
        {
            PlanetCity.Lights = lights; int crash0 = PlanetLife.I.Crashes; float maxOff = 0f; int mv = 0, wt = 0, st = 0, fr = 0, l0 = 0, wd = 0, n = 0;
            float until = Time.time + 90f, next = Time.time + 1f; int shot = 0;
            while (Time.time < until)
            {
                if (Time.time >= next)
                {
                    next += 1f; PlanetLife.I.Diag(out float o, out int a, out int b, out int c, out int d, out int e, out int f);
                    if (Time.time > until - 85f) { maxOff = Mathf.Max(maxOff, o); mv += a; wt += b; st += c; fr += d; l0 += e; wd += f; n++; }
                    if (o > 1.5f) log.Append("  t=").Append((45f - (until - Time.time)).ToString("0")).Append(" van de lijn ").Append(o.ToString("0.0")).Append(": ").Append(PlanetLife.I.Worst).AppendLine();
                    if (c > 0 && (int)(90f - (until - Time.time)) % 4 == 0) log.Append("  t=").Append((90f - (until - Time.time)).ToString("0")).Append(" vast: ").Append(c).Append(' ').Append(PlanetLife.I.Stuck).AppendLine();
                }
                if (Time.time > until - 85f + shot * 25f && shot < 3) { yield return Shot((lights ? "lichten_" : "voorrang_") + shot, shot == 1 ? x2 : x1, Dh(x1, 0.3f), 45f, 60f); shot++; }
                yield return null;
            }
            log.Insert(0, (lights ? "met lichten" : "zonder lichten") + ": auto's " + PlanetLife.I.CarCount + ", botsingen " + (PlanetLife.I.Crashes - crash0) + ", max van de rijlijn " + maxOff.ToString("0.00") + " m, per seconde gemiddeld rijden " + (mv / (float)n).ToString("0.0") + " wachten " + (wt / (float)n).ToString("0.0") + " vast " + (st / (float)n).ToString("0.0") + " los " + (fr / (float)n).ToString("0.0") + ", linker strook " + l0 + "/" + wd + "\n");
        }
        File.WriteAllText(Path.Combine(Dir, "verkeer.txt"), log.ToString() + PlanetLife.I.CrashLog);
        File.WriteAllText(Path.Combine(Dir, "klaar.txt"), "klaar");
        if (pb != null) pb.enabled = true;
    }
    // De teststad: één vaste opstelling waarop altijd getest wordt (menu Raatstad > Teststad). Een brede hoofdas van 300 m met een
    // rotonde in het midden en keerkoppen aan de einden; daarop een scherpe X met een gewone weg (60°), een haakse T (schuine weg) en een
    // laan die haaks kruist; een ringetje van gewone wegen met een schuine weg erdoor; een fietspad dat de hoofdas gelijkvloers oversteekt,
    // een voetpad dat eronderdoor gaat en een pad naar de rotonde; één zijweg over een heuvel; huizen en winkels. Met "meten" draait
    // daarna de verkeersmeting (zoals Verkeer) op deze stad.
    static int Hc;   // het centrum: alle richtingen in de teststad zijn hoeken in het kompas van dit vak (de hoek 0 van At/Dir2 verschilt per vak)
    static Vector3 Dh(int c, float ang) { Vector3 d = Dir2(Hc, ang), u = Planet.Cells[c].dir; d -= u * Vector3.Dot(d, u); return d.normalized; }
    static int Go(int c, float ang, float r) { var S = Planet.Cells[c]; return Planet.Nearest((S.dir * Mathf.Cos(r / Planet.R) + Dh(c, ang) * Mathf.Sin(r / Planet.R)).normalized * Planet.R); }
    static System.Text.StringBuilder bouw;
    static int Weg(string nm, int a, int b, byte st)
    {
        var l = PlanetCity.LinePlan(a, b); if (l == null || l.Count < 2) { bouw.Append(nm).AppendLine(": GEEN LIJN"); return -1; }
        PlanetCity.DragStyle = st; int ok = PlanetCity.PlaceLine(l, "weg"); PlanetCity.SetLevel(l, 0, "weg"); PlanetCity.DragStyle = 0;
        bouw.Append(nm).Append(": ").Append(ok).Append('/').Append(l.Count).Append(" vakken").AppendLine(PlanetCity.LastNote != null ? "  " + PlanetCity.LastNote : ""); return l[l.Count - 1];
    }
    static void PadH(string nm, int a, int b)
    {
        var l = PlanetCity.LinePlan(a, b); if (l == null || l.Count < 2) { bouw.Append(nm).AppendLine(": GEEN LIJN"); return; }
        int ok = PlanetCity.PlaceLine(l, "pad"); PlanetCity.SetLevel(l, 0, "pad"); bouw.Append(nm).Append(": ").Append(ok).Append('/').Append(l.Count).Append(" vakken").AppendLine();
    }
    IEnumerator Teststad(int H, bool meten)
    {
        Hc = H; bouw = new System.Text.StringBuilder(); float D60 = Mathf.PI / 3f, D30 = Mathf.PI / 6f, R90 = Mathf.PI / 2f;
        int x1 = Go(H, 0f, 70f), x2 = Go(H, Mathf.PI, 70f), x3 = Go(H, 0f, 125f);
        // heuvel (12 m) onder de zijweg van de T, voordat de wegen komen: die volgen het terrein
        int hc = Go(x2, R90, 48f);
        foreach (int i in new List<int>(Nearby(hc, 55f))) { float d = Vector3.Distance(Planet.Cells[i].dir, Planet.Cells[hc].dir) * Planet.R, k = Mathf.Cos(d / 55f * Mathf.PI * 0.5f); PlanetCity.SetTerr(i, 12f * k * k); }
        // hoofdas (breed) met keerkoppen, en de rotonde in het midden
        Weg("hoofdas oost", H, Go(H, 0f, 150f), 1); Weg("hoofdas west", H, Go(H, Mathf.PI, 150f), 1); bouw.Append("rotonde: ").AppendLine(PlanetCity.PlaceRoundabout(H) ?? "ok");
        // scherpe X: een gewone weg onder 60° over de hoofdas
        int xs = Weg("X zuidwest", x1, Go(x1, D60 + Mathf.PI, 60f), 0); Weg("X noordoost", x1, Go(x1, D60, 60f), 0);
        // haakse T: een schuine weg (30°) die op de hoofdas uitkomt, over de heuvel
        Weg("T over de heuvel", Go(x2, R90, 75f), x2, 0);
        // laan die haaks kruist
        Weg("laan", Go(x3, R90, 55f), Go(x3, -R90, 55f), 2);
        // ringetje van gewone wegen (zeshoek met zijden van 40 m) aan het eind van de X-arm, met een schuine weg erdoor
        float a0 = Mathf.PI; int rc = Go(xs, a0 + D60, 40f), ck = xs;
        for (int k = 0; k < 6 && ck >= 0; k++) ck = Weg("ring " + k, ck, Go(ck, a0 + k * D60, 40f), 0);
        Weg("schuin door de ring", Go(rc, D30 + D60, 75f), Go(rc, D30 + D60 + Mathf.PI, 75f), 0);
        // paden: fietspad gelijkvloers over de hoofdas (zebra), voetpad eronderdoor, fietspad naar de rotonde
        int cz = Go(H, Mathf.PI, 35f), cu = Go(H, 0f, 35f);
        PlanetCity.DragBike = true; PadH("fietspad oversteek", Go(cz, R90, 40f), Go(cz, -R90, 40f)); PlanetCity.DragBike = false;
        PlanetCity.DragUnder = true; PadH("voetpad onderdoor", Go(cu, R90, 40f), Go(cu, -R90, 40f)); PlanetCity.DragUnder = false;
        int rp = Go(H, -R90 - D30 + 0.01f, 14f); PlanetCity.DragBike = true; PadH("fietspad naar rotonde", rp, Go(rp, -R90 - D30, 50f)); PlanetCity.DragBike = false;
        // huizen en winkels: om het ringetje, langs de hoofdas en bij de rotonde
        for (int k = 0; k < 6; k++) { PlanetCity.Place(Go(rc, a0 + D30 + k * D60, 23f), "woning"); PlanetCity.Place(Go(rc, a0 + D30 + k * D60, 52f), k % 2 == 0 ? "kantoor" : "woning"); }
        for (int k = 1; k <= 3; k++) foreach (float side in new[] { R90, -R90 }) { PlanetCity.Place(Go(Go(H, 0f, 85f + k * 12f), side, 12f), k == 2 ? "winkel" : "woning"); PlanetCity.Place(Go(Go(H, Mathf.PI, 88f + k * 12f), side, 12f), "woning"); }
        foreach (float ang in new[] { D60, 2f * D60, 4f * D60, 5f * D60 }) PlanetCity.Place(Go(H, ang, 26f), "winkel");
        PlanetCity.Rebuild(); PlanetCity.Rebuild(); yield return null; yield return null;
        File.WriteAllText(Path.Combine(Dir, "bouw.txt"), bouw.ToString());
        yield return Shot("stad_boven", H, Dh(H, 0f), 230f, 89f);
        yield return Shot("stad_schuin", Go(H, Mathf.PI, 40f), Dh(H, 0.2f), 150f, 40f);
        yield return Shot("stad_ring", rc, Dh(rc, 0.5f), 70f, 55f);
        yield return Shot("stad_rotonde", H, Dh(H, 1.2f), 45f, 45f);
        yield return Shot("stad_heuvel", hc, Dh(hc, -0.6f), 70f, 30f);
        if (pb != null)
        {
            var fi = typeof(PlanetBuilder).GetField("focus", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (fi != null && fi.FieldType == typeof(Vector3)) fi.SetValue(pb, Planet.Cells[H].dir);
        }
        if (meten) yield return Meet(new[] { (H, "rotonde"), (x1, "X"), (x2, "T") });
        File.WriteAllText(Path.Combine(Dir, "klaar.txt"), "klaar");
        DayNight.Speed = 1f; if (pb != null) pb.enabled = true;
    }
    // Verkeer meten op de stad die geladen is, zonder iets te veranderen: de drie drukste kruispunten (meeste armen, ver uit elkaar)
    // komen op de schermafbeeldingen; het rapport staat in wegvoorbeeld/verkeer.txt
    IEnumerator MeetHier()
    {
        var js = new List<(int c, int n)>();
        foreach (var kv in PlanetCity.Cells) if (kv.Value.Count > 0 && kv.Value[0] == "weg") { int n = 0; foreach (int p in PlanetCity.Partners(kv.Key)) if (PlanetCity.IsRoad(p)) n++; if (n >= 3 || PlanetCity.IsRot(kv.Key)) js.Add((kv.Key, n + (PlanetCity.IsRot(kv.Key) ? 10 : 0))); }
        js.Sort((p, q) => q.n.CompareTo(p.n)); var spots = new List<(int c, string nm)>();
        foreach (var j in js) { bool far = true; foreach (var sp in spots) if (Vector3.Distance(Planet.Cells[sp.c].dir, Planet.Cells[j.c].dir) * Planet.R < 60f) far = false; if (far) spots.Add((j.c, "kruispunt" + (spots.Count + 1))); if (spots.Count == 3) break; }
        if (spots.Count == 0) { File.WriteAllText(Path.Combine(Dir, "verkeer.txt"), "Geen kruispunt gevonden: is er een stad geladen?\n"); File.WriteAllText(Path.Combine(Dir, "klaar.txt"), "klaar"); if (pb != null) pb.enabled = true; yield break; }
        Hc = spots[0].c; yield return Meet(spots.ToArray());
        File.WriteAllText(Path.Combine(Dir, "klaar.txt"), "klaar"); if (pb != null) pb.enabled = true;
    }
    // de startstad bouwen op de startplek bij de zuidpool en fotograferen (overzicht, het meer, een brug, het plein)
    IEnumerator Startstad()
    {
        int st = PlanetCity.FreeStart(Planet.Cells[Planet.South].dir); Hc = st; PlanetCity.StartTown(st);
        PlanetCity.Rebuild(); PlanetCity.Rebuild(); yield return null; yield return null;
        int lake = -1, bridge = -1;
        foreach (var kv in PlanetCity.Cells) { if (kv.Value.Count > 0 && kv.Value[0] == "meer" && lake < 0) lake = kv.Key; if (PlanetCity.IsRoad(kv.Key) && PlanetCity.Under.ContainsKey(kv.Key) && bridge < 0) bridge = kv.Key; }
        yield return Shot("start_boven", st, Dh(st, 0f), 240f, 89f);
        yield return Shot("start_schuin", st, Dh(st, 0.4f), 160f, 38f);
        yield return Shot("start_plein", st, Dh(st, 1.0f), 60f, 40f);
        if (lake >= 0) { yield return Shot("start_meer_boven", lake, Dh(lake, 0f), 40f, 89f); yield return Shot("start_meer", lake, Dh(lake, 0.7f), 30f, 25f); }
        if (bridge >= 0) { yield return Shot("start_brug", bridge, Dh(bridge, 0.5f), 35f, 30f); yield return Shot("start_brug_laag", bridge, Dh(bridge, 2.0f), 22f, 12f); }
        int jx = -1; foreach (var kv in PlanetCity.Cells) if (PlanetCity.IsRoad(kv.Key)) { int n = 0; foreach (int p2 in PlanetCity.Partners(kv.Key)) if (PlanetCity.IsRoad(p2)) n++; if (n >= 3) { jx = kv.Key; break; } }
        if (jx >= 0)
        {
            PlanetCity.Lights = true; yield return Shot("start_kruising_lichten", jx, Dh(jx, 0.5f), 32f, 55f); yield return Shot("start_kruising_dichtbij", jx, Dh(jx, 0.5f), 14f, 70f);
            PlanetCity.Lights = false; PlanetCity.TouchAll(); PlanetCity.Rebuild(); yield return null; yield return null; yield return Shot("start_kruising_zonder", jx, Dh(jx, 0.5f), 32f, 55f); yield return Shot("start_kruising_zonder_dichtbij", jx, Dh(jx, 0.5f), 14f, 70f);
            PlanetCity.Lights = true; PlanetCity.TouchAll(); PlanetCity.Rebuild();
        }
        File.WriteAllText(Path.Combine(Dir, "klaar.txt"), "klaar"); DayNight.Speed = 1f; if (pb != null) pb.enabled = true;
    }
    // de zeven auto's naast elkaar op een leeg stuk bij de zuidpool: schuin van voren, schuin van achteren, dichtbij een wiel, en een auto
    // die stuurt (de voorwielen schuin, de spaken gedraaid)
    IEnumerator Autos()
    {
        PlanetCity.Slot = 9; PlanetCity.ClearAll(); PlanetCity.Rebuild(); DayNight.Hour = 11f; DayNight.Speed = 0f; PlanetLife.CarsOn = false; PlanetLife.PeopleOn = false;
        int H = At(Planet.South, 1.0f, 160f); Hc = H; Vector3 up = Planet.Cells[H].dir, fw = Dh(H, 0f), rt = Vector3.Cross(up, fw);
        var root = new GameObject("Autostudio").transform; var paints = new[] { "#3d5a80", "#d9534f", "#4a90a4", "#d3d8dc", "#7bb356", "#f2f3f5", "#f2c230" };
        CarWheels turner = null;
        for (int k = 0; k < LowPolyCar.Types.Length; k++)
        {
            var t = new GameObject(LowPolyCar.Types[k]).transform; t.SetParent(root, false);
            Vector3 p = (up * Planet.R + rt * ((k - 3) * 3.4f)).normalized * Planet.R; t.position = p; t.rotation = Geo.Look(fw, p.normalized);
            LowPolyCar.Build(t, LowPolyCar.Types[k], LowPolyCar.Hex(paints[k])); if (k == 0) turner = t.GetComponent<CarWheels>();
        }
        yield return null;
        yield return ShotAt("autos_voor", up * Planet.R, fw * 14f + up * 5f - rt * 4f);
        yield return ShotAt("autos_achter", up * Planet.R, -fw * 14f + up * 5f + rt * 4f);
        Vector3 c0 = (up * Planet.R + rt * (-3 * 3.4f)).normalized * Planet.R;
        yield return ShotAt("auto_sedan_voor", c0, fw * 6f + up * 2f + rt * 3.5f);
        yield return ShotAt("auto_sedan_zij", c0, rt * 6f + up * 1.2f);
        for (int k = 1; k < LowPolyCar.Types.Length; k++) { Vector3 ck = (up * Planet.R + rt * ((k - 3) * 3.4f)).normalized * Planet.R; yield return ShotAt("auto_" + LowPolyCar.Types[k], ck, fw * 5.5f + up * 2.2f - rt * 4f); }
        if (turner != null) { turner.Drive(0.45f, 30f, 0f); yield return null; yield return ShotAt("auto_sedan_stuurt", c0, fw * 3.5f + up * 1.6f - rt * 4.2f); yield return ShotAt("auto_sedan_stuurt_boven", c0, up * 6f + fw * 0.5f); }
        File.WriteAllText(Path.Combine(Dir, "klaar.txt"), "klaar"); PlanetLife.CarsOn = true; PlanetLife.PeopleOn = true; if (pb != null) pb.enabled = true;
    }
    IEnumerator ShotAt(string name, Vector3 target, Vector3 offset)
    {
        cam.transform.position = target + offset; cam.transform.rotation = Quaternion.LookRotation(target + target.normalized * 0.7f - cam.transform.position, target.normalized); cam.fieldOfView = 40f;
        yield return null; yield return null; ScreenCapture.CaptureScreenshot(Path.Combine(Dir, name + ".png")); yield return null; yield return null;
    }
    static IEnumerable<int> Nearby(int c, float r)
    {
        var seen = new HashSet<int> { c }; var q = new Queue<int>(); q.Enqueue(c);
        while (q.Count > 0) { int i = q.Dequeue(); yield return i; foreach (int j in Planet.Cells[i].nb) if (seen.Add(j) && Vector3.Distance(Planet.Cells[j].dir, Planet.Cells[c].dir) * Planet.R <= r) q.Enqueue(j); }
    }
    // de verkeersmeting: 90 s zonder en 90 s met lichten; botsingen, van de lijn, rijden/wachten/vast, linker strook; schermafbeeldingen bij de punten
    IEnumerator Meet((int c, string nm)[] spots)
    {
        DayNight.Speed = 1f; var log = new System.Text.StringBuilder(); PlanetLife.CarBoost = File.Exists(Path.Combine(Application.persistentDataPath, "verkeer_druk.txt")) ? 12 : 0;
        { float w0 = Time.time + 15f; while (Time.time < w0) yield return null; }   // de auto's laten verschijnen
        foreach (bool lights in new[] { false, true })
        {
            PlanetCity.Lights = lights; int crash0 = PlanetLife.I.Crashes; float maxOff = 0f; int mv = 0, wt = 0, st = 0, fr = 0, l0 = 0, wd = 0, n = 0;
            float until = Time.time + 90f, next = Time.time + 1f; int shot = 0;
            while (Time.time < until)
            {
                if (Time.time >= next)
                {
                    next += 1f; PlanetLife.I.Diag(out float o, out int a, out int b, out int c, out int d, out int e, out int f);
                    if (Time.time > until - 85f) { maxOff = Mathf.Max(maxOff, o); mv += a; wt += b; st += c; fr += d; l0 += e; wd += f; n++; }
                    if (o > 1.5f) log.Append("  t=").Append((90f - (until - Time.time)).ToString("0")).Append(" van de lijn ").Append(o.ToString("0.0")).Append(": ").Append(PlanetLife.I.Worst).AppendLine();
                    if (c > 0 && (int)(90f - (until - Time.time)) % 4 == 0) log.Append("  t=").Append((90f - (until - Time.time)).ToString("0")).Append(" vast: ").Append(c).Append(' ').Append(PlanetLife.I.Stuck).AppendLine();
                }
                if (Time.time > until - 85f + shot * 25f && shot < spots.Length) { var sp = spots[shot]; yield return Shot((lights ? "lichten_" : "voorrang_") + sp.nm, sp.c, Dh(sp.c, 0.3f), 45f, 60f); shot++; }
                yield return null;
            }
            log.Insert(0, (lights ? "met lichten" : "zonder lichten") + ": auto's " + PlanetLife.I.CarCount + ", botsingen " + (PlanetLife.I.Crashes - crash0) + ", max van de rijlijn " + maxOff.ToString("0.00") + " m, per seconde gemiddeld rijden " + (mv / (float)n).ToString("0.0") + " wachten " + (wt / (float)n).ToString("0.0") + " vast " + (st / (float)n).ToString("0.0") + " los " + (fr / (float)n).ToString("0.0") + ", linker strook " + l0 + "/" + wd + "\n");
        }
        File.WriteAllText(Path.Combine(Dir, "verkeer.txt"), log.ToString() + PlanetLife.I.CrashLog);
    }
    static void PadLine(int a, int b) { var l = PlanetCity.LinePlan(a, b); if (l == null || l.Count < 2) return; PlanetCity.PlaceLine(l, "pad"); PlanetCity.SetLevel(l, 0, "pad"); }
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
