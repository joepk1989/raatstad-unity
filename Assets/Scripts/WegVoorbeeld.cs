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
        if (!File.Exists(Flag)) return; mode = File.ReadAllText(Flag).Trim(); File.Delete(Flag); PlanetCity.Ground2 = false; PlanetCity.ProfileFirst = false; for (bool again = true; again;) { again = false; if (mode.EndsWith(" stroken")) { mode = mode.Substring(0, mode.Length - 8); again = true; } if (mode.EndsWith(" halfv3")) { PlanetCity.G3Half = true; PlanetCity.SetGround3(true); mode = mode.Substring(0, mode.Length - 7); again = true; } if (mode.EndsWith(" oudegrond")) { PlanetCity.SetGround3(false); mode = mode.Substring(0, mode.Length - 10); again = true; } if (mode.EndsWith(" v2")) { PlanetCity.Ground2 = true; mode = mode.Substring(0, mode.Length - 3); again = true; } if (mode.EndsWith(" geennaden")) { mode = mode.Substring(0, mode.Length - 10); again = true; } if (mode.EndsWith(" profiel")) { PlanetCity.ProfileFirst = true; mode = mode.Substring(0, mode.Length - 8); again = true; } }   // "... v2": met Terrein v2; "... geennaden": zonder naden   // tekst "fiets": de fietspaden en de startstad
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
        cam = Camera.main; if (pb != null) pb.enabled = false; PlanetCity.SoilWire = true;   // de terreinlijnen aan bij elke test (de instelling blijft daarna ook in het spel aan)
        if (pb != null) foreach (string f in new[] { "home", "welcome" }) { var fi = typeof(PlanetBuilder).GetField(f, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public); if (fi != null && fi.FieldType == typeof(bool)) fi.SetValue(pb, false); }   // geen startscherm: het voorbeeld bouwt zelf
        if (mode == "pool")   // het hologram boven de noordpool (PoleHolo.cs), van opzij, van onderaf en van dichtbij
        {
            yield return new WaitForSeconds(1.5f); int np = Planet.North; var pc = Planet.Cells[np]; Vector3 hp = pc.dir * (Planet.R + PlanetCity.G(np) + 88f);
            yield return ShotAt("pool_opzij", hp, pc.east * 95f + pc.dir * 5f);
            yield return ShotAt("pool_onder", hp, pc.north * 70f - pc.dir * 70f);
            yield return new WaitForSeconds(2.2f); yield return ShotAt("pool_dichtbij", hp, (pc.east + pc.north) * 32f); { Vector3 gp = pc.dir * (Planet.R + PlanetCity.G(np) + 45f); yield return ShotAt("pool_grond", gp, pc.east * 190f + pc.dir * 10f); } { Vector3 gp2 = pc.dir * (Planet.R + PlanetCity.G(np)); yield return ShotAt("pool_rand", gp2, pc.east * 30f + pc.north * 12f + pc.dir * 22f); }   // het vak eronder en het hologram erboven
            File.WriteAllText(Path.Combine(Dir, "klaar.txt"), "klaar"); if (pb != null) pb.enabled = true; yield break;
        }
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

        int home = At(Planet.North, 1.0f, 160f);   // tests bouwen bij de noordpool: daar opent het spel, dus ze zijn meteen te zien
        float D60 = Mathf.PI / 3f;
        if (mode == "fiets") { yield return Bikes(home); yield break; }
        if (mode == "rotonde") { yield return Roundabouts(home); yield break; }
        if (mode == "schuin") { yield return Diagonals(home); yield break; }
        if (mode == "sleep") { yield return SleepTest(home); yield break; }
        if (mode == "hoek") { yield return Hoeken(home); yield break; }
        if (mode == "terrein") { yield return TerreinTest(home); yield break; }
        if (mode == "dal") { yield return DalTest(home); yield break; }
        if (mode == "mijnstad") { yield return MijnStad(); yield break; }
        if (mode.StartsWith("stad ")) { yield return StadNakijken(mode); yield break; }
        if (mode.StartsWith("melding")) { yield return Melding(mode); yield break; }
        if (mode == "hoogtetest") { yield return Hoogtetest(); yield break; }
        if (mode == "natuurtest") { yield return Hoogtetest(true); yield break; }
        if (mode == "inspel") { yield return Inspel(); yield break; }
        if (mode == "volplaneet") { yield return VolPlaneet(); yield break; }   // Hele planeet vol met Terrein v2: profiel, laden, herbouw en een klik   // Terrein v2 zoals in het spel: wegen een voor een, opslaan en laden, de schakelaar   // de natuurplaneet met de 30 wegen (test 11 van het ontwerp)
        if (mode.StartsWith("ontwerp")) { yield return Ontwerp(home, mode); yield break; }
        if (mode == "starttijd") { yield return StartTijd(); yield break; }   // de startstad met Terrein v2: herbouwtijd per stap en de traagste vakken
        if (mode == "zee") { yield return ZeeTest(home); yield break; }
        if (mode == "aiterrein") { yield return AiTerrein(); yield break; }
        if (mode == "aisteden") { yield return AiSteden(); yield break; }
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
    // Sleeptest: precies wat de muis doet bij loslaten (PlanetBuilder: LinePlan, PlaceLine, SetLevel, DipUnder), in lastige gevallen:
    // terug over een bestaande weg, half eroverheen, schuin, op een niveau, eenrichting, paden en fietspaden, over een kruising, een rotonde.
    // Na elke stap: CheckGraph, of de lijn helemaal weg is en aan elkaar zit, of de rest niet verandert, en of tekenen goed gaat.
    static System.Text.StringBuilder sl;
    static int sleepFouten;
    static int Drag(int a, int b, string t, int lv = 0)
    {
        var plan = PlanetCity.LinePlan(a, b); if (plan == null) return -1;
        bool du = PlanetCity.DragUnder; PlanetCity.DragLevel = lv; int ok = 0;
        if (t == "weg" && lv != 0) PlanetCity.DragUnder = lv <= -2;
        ok = PlanetCity.PlaceLine(plan, t); PlanetCity.DragUnder = du;
        if (ok > 0) PlanetCity.SetLevel(plan, lv, t);
        PlanetCity.DragLevel = 0; return ok;
    }
    static string Fp() { var l = new List<long>(PlanetCity.Links); l.Sort(); long h = 17; foreach (long k in l) h = h * 31 + k; return PlanetCity.Links.Count + ":" + h; }
    static void Verwacht(string nm, bool goed, string waarom) { if (!goed) { sleepFouten++; sl.Append("   FOUT ").Append(nm).Append(": ").AppendLine(waarom); } }
    static List<int> laatste;   // de vakken van de vorige gesleepte lijn
    static void Terug(string nm, string t, int lv = 0, bool zelfde = true) { if (laatste != null && laatste.Count >= 2) Stap(nm, laatste[laatste.Count - 1], laatste[0], t, lv, zelfde); }   // terug vanaf het eind van de vorige lijn naar het begin
    static void Stap(string nm, int a, int b, string t, int lv = 0, bool zelfde = false)
    {
        string voor = Fp(); var oud = new HashSet<long>(PlanetCity.Links); var oudT = new HashSet<long>(PlanetCity.TunnelLinks); var plan = PlanetCity.LinePlan(a, b); int ok = Drag(a, b, t, lv);
        sl.Append(nm).Append(": ").Append(ok).Append('/').Append(plan == null ? 0 : plan.Count).Append(" vakken").Append(PlanetCity.LastNote != null ? "  (" + PlanetCity.LastNote + ")" : "").Append("  [").Append(plan == null ? "" : string.Join(",", plan)).Append("]").AppendLine();
        foreach (string f in PlanetCity.CheckGraph()) Verwacht(nm, false, f);
        if (plan != null)
        {
            System.Func<int, bool> walk = t == "weg" ? (System.Func<int, bool>)PlanetCity.IsRoad : PlanetCity.IsWalk;
            foreach (int c in plan) Verwacht(nm, walk(c), "vak " + c + " van de lijn is geen " + t);
            for (int k = 1; k < plan.Count; k++)
            {
                int x = plan[k - 1], y = plan[k]; bool con = PlanetCity.Linked(x, y);
                if (!con) foreach (int h in PlanetCity.Partners(x)) if (PlanetCity.Linked(h, y)) con = true;   // via een kruispunt op de rand of een oversteek
                Verwacht(nm, con, "lijn onderbroken tussen " + x + " en " + y);
            }
        }
        if (zelfde && Fp() != voor)
        {
            var bij = new List<string>(); foreach (long k in PlanetCity.Links) if (!oud.Contains(k)) bij.Add((k >> 20) + "-" + (k & 0xFFFFF) + (PlanetCity.TunnelLinks.Contains(k) ? "t" : ""));
            var weg = new List<string>(); foreach (long k in oud) if (!PlanetCity.Links.Contains(k)) weg.Add((k >> 20) + "-" + (k & 0xFFFFF) + (oudT.Contains(k) ? "t" : ""));
            Verwacht(nm, false, "het net veranderde terwijl de lijn er al lag: erbij " + string.Join(" ", bij) + ", weg " + string.Join(" ", weg) + "; lijn " + string.Join(",", plan));
        }
        try { PlanetCity.Rebuild(); } catch (System.Exception e) { Verwacht(nm, false, "tekenen: " + e.GetType().Name + " " + e.Message); }
        laatste = plan;
    }
    IEnumerator SleepTest(int home)
    {
        sl = new System.Text.StringBuilder(); sleepFouten = 0; float D60 = Mathf.PI / 3f, D30 = Mathf.PI / 6f; Hc = home;
        var errs = new System.Text.StringBuilder(); Application.LogCallback cb = (msg, st, type) => { if (type == LogType.Exception || type == LogType.Error) errs.Append(type).Append(": ").Append(msg).AppendLine(); };
        Application.logMessageReceived += cb;
        int[] c = new int[8]; for (int k = 0; k < 8; k++) c[k] = Go(home, k * Mathf.PI / 4f, k == 0 ? 0f : 170f);
        // 1. recht: heen, terug, nog eens heen, half terug voorbij het begin
        int a1 = Go(c[0], 0f, -50f), b1 = Go(c[0], 0f, 50f);
        Stap("recht heen", a1, b1, "weg"); Terug("recht terug", "weg"); Terug("recht nog eens heen", "weg");
        Stap("half terug voorbij het begin", Go(c[0], 0f, 10f), Go(c[0], 0f, -80f), "weg");
        Stap("vanuit het midden naar het eind", Go(c[0], 0f, 0f), b1, "weg", 0, true);
        // 2. schuin (30°): heen en terug
        int a2 = Go(c[1], D30, -50f), b2 = Go(c[1], D30, 50f);
        Stap("schuin heen", a2, b2, "weg"); Terug("schuin terug", "weg"); Stap("schuin half terug", Go(c[1], D30, 20f), Go(c[1], D30, -70f), "weg");
        // 3. kruising: X, dan beide wegen terug, dan dwars terug
        int a3 = Go(c[2], 0f, -50f), b3 = Go(c[2], 0f, 50f), e3 = Go(c[2], D60, -50f), f3 = Go(c[2], D60, 50f);
        Stap("X eerste", a3, b3, "weg"); var x1 = laatste; Stap("X tweede", e3, f3, "weg"); Terug("X tweede terug", "weg"); laatste = x1; Terug("X eerste terug", "weg");
        // 4. eenrichting: heen, en terug slepen draait de richting om
        int a4 = Go(c[3], 0f, -40f), b4 = Go(c[3], 0f, 40f);
        PlanetCity.DragOneWay = true; Stap("eenrichting heen", a4, b4, "weg"); Terug("eenrichting terug", "weg", 0, false); PlanetCity.DragOneWay = false;
        Stap("tweerichting erover", a4, b4, "weg");
        // 5. op een niveau (+2) en verdiept (-2): heen en terug
        int a5 = Go(c[4], 0f, -50f), b5 = Go(c[4], 0f, 50f), a6 = Go(c[4], D60 * 2f, 30f), b6 = Go(Go(c[4], D60 * 2f, 30f), 0f, 100f);
        Stap("hoog heen", a5, b5, "weg", 2); Terug("hoog terug", "weg", 2); var h1 = laatste; Stap("laag heen", a6, b6, "weg", -2); Terug("laag terug", "weg", -2);
        laatste = h1; Terug("hoog terug op maaiveld (andere hoogte)", "weg", 0, false);
        // 6. breed over gewoon terug, laan
        int a7 = Go(c[5], 0f, -50f), b7 = Go(c[5], 0f, 50f);
        Stap("gewoon", a7, b7, "weg"); PlanetCity.DragStyle = 1; Terug("breed terug eroverheen", "weg"); PlanetCity.DragStyle = 2; Stap("laan half", Go(c[5], 0f, 0f), a7, "weg"); PlanetCity.DragStyle = 0;
        // 7. paden en fietspaden heen en terug, pad terug over een weg
        int a8 = Go(c[6], 0f, -50f), b8 = Go(c[6], 0f, 50f), e8 = Go(c[6], D60, -40f), f8 = Go(c[6], D60, 40f);
        Stap("pad heen", a8, b8, "pad"); Terug("pad terug", "pad"); PlanetCity.DragBike = true; Terug("fietspad terug eroverheen", "pad"); PlanetCity.DragBike = false;
        Stap("weg over het pad", e8, f8, "weg"); Terug("pad terug over de weg", "pad", 0, false);
        // 8. rotonde en terug slepen over een arm
        int r = c[7]; Leg(r, 0f, 0); Leg(r, Mathf.PI, 0); sl.Append("rotonde: ").AppendLine(PlanetCity.PlaceRoundabout(r) ?? "ok");
        Stap("arm naar de rotonde terug", Go(r, 0f, 60f), Go(r, 0f, 14f), "weg", 0, true); Stap("arm van de rotonde af", Go(r, Mathf.PI, 14f), Go(r, Mathf.PI, 70f), "weg");
        Stap("dwars over de rotonde", Go(r, D60, 60f), Go(r, D60 + Mathf.PI, 60f), "weg");
        // 9. ongelijkvloers (U) terug over de eigen brug
        int a9 = Go(home, Mathf.PI / 8f, 330f), b9 = Go(a9, 0f, 100f), e9 = Go(Go(a9, 0f, 50f), D60, -40f), f9 = Go(Go(a9, 0f, 50f), D60, 40f);
        Stap("onder: eerste weg", a9, b9, "weg"); var o1 = laatste; PlanetCity.DragUnder = true; Stap("onder: tweede eronder", e9, f9, "weg"); Terug("onder: tweede terug met U", "weg"); PlanetCity.DragUnder = false;
        Terug("onder: tweede terug zonder U", "weg"); laatste = o1; Terug("onder: eerste terug", "weg");
        // 10. twee wegen in naast elkaar liggende rijen en een verticale (schuine) weg erdoorheen, op beide mogelijke plekken
        int t0 = Go(home, -Mathf.PI / 8f, 330f), t1 = Planet.Cells[t0].nb[0]; int tA = -1;
        foreach (int j in Planet.Cells[t0].nb) if (Vector3.Dot(Dh(t0, Mathf.PI / 2f), (Planet.Cells[j].dir - Planet.Cells[t0].dir).normalized) > 0.3f) { tA = j; break; }   // het vak in de rij erboven
        Stap("rijen: onderste", Go(t0, 0f, -70f), Go(t0, 0f, 70f), "weg"); if (tA >= 0) Stap("rijen: bovenste", Go(tA, 0f, -70f), Go(tA, 0f, 70f), "weg");
        // haaks op de rijen: telkens het across-vak dat het meest haaks op de rij ligt (over de rand tussen twee buren)
        Vector3 rowDir = (Planet.Cells[Go(t0, 0f, 47f)].dir - Planet.Cells[t0].dir).normalized;
        System.Func<int, int, int> Haaks = (c0, steps) => { int x = c0; for (int k = 0; k < Mathf.Abs(steps); k++) { int best = x; float bd = -2f; Vector3 u = Planet.Cells[x].dir, want = Vector3.Cross(u, rowDir).normalized * Mathf.Sign(steps); foreach (int j in Planet.Cells[x].across) { if (j < 0) continue; float d = Vector3.Dot((Planet.Cells[j].dir - u).normalized, want); if (d > bd) { bd = d; best = j; } } x = best; } return x; };
        Stap("rijen: verticaal 1", Haaks(t0, -3), Haaks(t0, 3), "weg"); Terug("rijen: verticaal 1 terug", "weg");
        int t2 = Go(t0, 0f, 47f); Stap("rijen: verticaal 2", Haaks(t2, -3), Haaks(t2, 3), "weg"); Terug("rijen: verticaal 2 terug", "weg");
        int t3 = Planet.Cells[t2].nb[0]; foreach (int j in Planet.Cells[t2].nb) if (Mathf.Abs(Vector3.Dot((Planet.Cells[j].dir - Planet.Cells[t2].dir).normalized, rowDir)) > 0.9f) { t3 = j; break; }   // een vak verder in de rij: de andere pariteit
        Stap("rijen: verticaal 3", Haaks(t3, -3), Haaks(t3, 3), "weg"); Terug("rijen: verticaal 3 terug", "weg");
        int t4 = Go(t0, 0f, 125f); Stap("rijen: verticaal pad", Haaks(t4, -3), Haaks(t4, 3), "pad"); Terug("rijen: verticaal pad terug", "pad");
        Application.logMessageReceived -= cb;
        PlanetCity.Rebuild(); PlanetCity.Rebuild(); yield return null; yield return null;
        foreach (var (nm, cc) in new[] { ("sleep_recht", c[0]), ("sleep_schuin", c[1]), ("sleep_kruising", c[2]), ("sleep_niveau", c[4]), ("sleep_rotonde", c[7]), ("sleep_onder", Go(a9, 0f, 50f)) }) yield return Shot(nm, cc, Dh(cc, 0.3f), 70f, 70f);
        yield return Shot("sleep_rijen_1", t0, Dh(t0, 0f), 45f, 89f); yield return Shot("sleep_rijen_2", t2, Dh(t2, 0f), 45f, 89f); yield return Shot("sleep_rijen_3", t3, Dh(t3, 0f), 45f, 89f);
        File.WriteAllText(Path.Combine(Dir, "sleep.txt"), "fouten: " + sleepFouten + "\n" + (errs.Length > 0 ? "CONSOLE:\n" + errs + "\n" : "") + sl);
        File.WriteAllText(Path.Combine(Dir, "klaar.txt"), "klaar"); if (pb != null) pb.enabled = true;
    }
    // scherpe hoeken: een schuine weg (30°) die uit een kruispunt van een rechte weg komt, als gewone weg en als laan, en paden die
    // schuin op een weg of kruising uitkomen
    // Terreintest: een heuvelachtige proefstad (golvend terrein tot 10 m, een rooster van straten in drie richtingen plus schuine wegen,
    // huizen op elk leeg vak ernaast), dan per straal van bovenaf: steekt het terrein ergens boven het wegdek uit (fout A), of valt een
    // straal door de grond heen (een gat tussen terreinstukken, fout B)? De ergste plekken op de foto.
    IEnumerator TerreinTest(int home)
    {
        float D30 = Mathf.PI / 6f, D60 = Mathf.PI / 3f; Hc = home; bouw = new System.Text.StringBuilder();
        var area = new List<int>(Nearby(home, 200f));
        foreach (int i in area)
        {
            Vector3 v = (Planet.Cells[i].dir - Planet.Cells[home].dir) * Planet.R; float x = Vector3.Dot(v, Dh(home, 0f)), y = Vector3.Dot(v, Dh(home, Mathf.PI / 2f));
            PlanetCity.SetTerr(i, Mathf.Round((5f + 3f * Mathf.Sin(x / 37f) + 2.5f * Mathf.Cos(y / 29f + 0.7f) + 1.5f * Mathf.Sin((x + y) / 17f)) * 4f) / 4f);
        }
        foreach (float ang in new[] { 0f, D60, 2f * D60 }) for (int k = -3; k <= 3; k++) { int c = Go(home, ang + Mathf.PI / 2f, k * 47f); Weg("rooster " + k, Go(c, ang, -150f), Go(c, ang, 150f), (byte)(k == 0 ? 2 : 0)); }
        foreach (float ang in new[] { D30, D30 + D60 }) Weg("schuin", Go(home, ang, -160f), Go(home, ang, 160f), 0);
        PlanetCity.DragBike = true; PadH("fietspad", Go(home, -D30, -150f), Go(home, -D30, 150f)); PlanetCity.DragBike = false;
        var rnd = new System.Random(3);
        foreach (int i in area) if (!PlanetCity.Cells.ContainsKey(i) && Vector3.Distance(Planet.Cells[i].dir, Planet.Cells[home].dir) * Planet.R < 170f) { int r = rnd.Next(10); PlanetCity.Place(i, r < 6 ? "woning" : r < 8 ? "park" : r < 9 ? "winkel" : "kantoor"); }
        PlanetCity.Rebuild(); PlanetCity.Rebuild(); yield return null; yield return null; yield return null;
        yield return Scan(home, 165f, "terrein");
        File.WriteAllText(Path.Combine(Dir, "klaar.txt"), "klaar"); if (pb != null) pb.enabled = true;
    }
    static int scanWorst = -1;   // de ergste plek van de laatste scan (een gat, anders terrein boven de weg), of -1
    // Na de test blijft Play draaien: de camera vliegt naar de plek waar het om gaat, zodat je het meteen zelf ziet (PlanetBuilder.FlyTo)
    void Breng(int cell, string say)
    {
        if (pb == null || cell < 0) return; pb.enabled = true;
        var m = typeof(PlanetBuilder).GetMethod("FlyTo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (m != null) m.Invoke(pb, new object[] { Planet.Cells[cell].dir, true, say });
    }
    IEnumerator Scan(int home, float R, string name, List<int> centers = null, float r = 0f)
    {
        // scannen: punten om de 0,7 m over het hele gebied (of rond elk van de centers, straal r)
        var foutA = new List<(Vector3 p, float d)>(); var foutAHit = new Dictionary<Vector3, string>(); var foutB = new List<Vector3>(); var foutC = new List<(Vector3 p, float d)>(); int nA = 0, nRoad = 0, nGround = 0, nC = 0; float maxC = 0f; string worstC = "";
        var discs = new List<(int c, float R)>(); if (centers == null) discs.Add((home, R)); else foreach (int cc in centers) discs.Add((cc, r));
        foreach (var disc in discs)
        {
        float Rd = disc.R; Vector3 c0 = Planet.Cells[disc.c].dir, e = Dh(disc.c, 0f), n = Vector3.Cross(c0, e).normalized; const float step = 0.7f;
        for (float x = -Rd; x <= Rd; x += step) for (float y = -Rd; y <= Rd; y += step)
        {
            if (x * x + y * y > Rd * Rd) continue;
            Vector3 dir = (c0 * Planet.R + e * x + n * y).normalized; int cell = Planet.Nearest(dir * Planet.R);
            if (PlanetCity.Under.ContainsKey(cell) || PlanetCity.IsHalf(cell) && false) continue;
            Vector3 top = dir * (Planet.R + 150f);   // boven de hoogste berg (90 m)
            bool road = Physics.Raycast(top, -dir, out RaycastHit hr, 180f, 1 << Layers.Weg, QueryTriggerInteraction.Ignore);
            bool ground = Physics.Raycast(top, -dir, out RaycastHit hg, 180f, (1 << Layers.Bodem) | (1 << Layers.Water), QueryTriggerInteraction.Ignore);
            float hRoad = road ? hr.point.magnitude - Planet.R : 0f, hGround = ground ? hg.point.magnitude - Planet.R : 0f;
            if (road) { nRoad++; if (ground && hGround > hRoad + 0.03f && hGround < hRoad + 4f) { nA++; foutA.Add((dir, hGround - hRoad)); foutAHit[dir] = hg.collider.name + " (laag " + hg.collider.gameObject.layer + ")"; } }   // (meer dan 4 m erboven: een tunnel of brug)
            if (!road && ground && hg.collider.gameObject.layer == Layers.Bodem)   // C: de mesh tegenover de hoogtefunctie H (stap 2 van Terrein v2 bouwt de mesh uit H: dan 0)
            {
                nC++; float hH = PlanetCity.H(dir), lo = hH, hi = hH; foreach (var sd in new[] { e, -e, n, -n }) { float hs = PlanetCity.H((dir * Planet.R + sd * 0.05f).normalized); lo = Mathf.Min(lo, hs); hi = Mathf.Max(hi, hs); } float dh = hGround < lo ? lo - hGround : hGround > hi ? hGround - hi : 0f; if (dh > 0.1f) { foutC.Add((dir, dh)); if (dh > maxC) { maxC = dh; worstC = "mesh " + hGround.ToString("0.00") + " (" + hg.collider.name + ") H " + hH.ToString("0.00") + " vak " + Planet.Nearest(dir * Planet.R) + " | " + PlanetCity.HInfo(dir); } }
            }
            if (!road)   // ook schuin (30° uit vier richtingen): een kier in een wand zie je alleen van opzij
            {
                nGround++; int all = (1 << Layers.Bodem) | (1 << Layers.Water) | (1 << Layers.Gebouw) | (1 << Layers.Weg); bool hole = !Physics.Raycast(top, -dir, 180f, all, QueryTriggerInteraction.Ignore);
                if (!hole && ground) for (int q = 0; q < 4 && !hole; q++)
                {
                    Vector3 side = (q == 0 ? e : q == 1 ? -e : q == 2 ? n : -n), from = hg.point + dir * 25f + side * 14.4f;   // naar het geraakte punt toe, onder 60° met de grond
                    if (Vector3.Dot(hg.normal, (from - hg.point).normalized) < 0.15f) continue;   // een steile helling die van deze kant af kijkt: de straal raakt de achterkant (die telt niet mee), geen gat
                    if (!Physics.Raycast(from, (hg.point - from).normalized, out RaycastHit hs, 35f, all, QueryTriggerInteraction.Ignore)) hole = true;
                    else if (Vector3.Distance(hs.point, hg.point) > 0.6f && (hs.point - from).magnitude > (hg.point - from).magnitude + 0.3f) hole = true;   // de straal ging verder dan het punt: erdoorheen
                }
                if (hole) foutB.Add(dir);
            }
        }
        }
        { if (PlanetCity.HeightTestNote != "") bouw.AppendLine(PlanetCity.HeightTestNote); var gd = new System.Text.StringBuilder(); var gs = PlanetCity.GradeStats(gd); bouw.Append(gd); bouw.Append("hellingen: steilste ").Append((gs.max * 100f).ToString("0.0")).Append("%, ").Append(gs.over).Append(" van ").Append(gs.total).Append(" verbindingen steiler dan ").Append((PlanetCity.MaxGrade * 100f).ToString("0")).AppendLine("%"); }
        bouw.Append("gescand: ").Append(nRoad).Append(" punten op de weg, ").Append(nGround).Append(" ernaast; Terrein v2 ").AppendLine(PlanetCity.Ground2 ? "aan" : "uit");
        if (PlanetCity.Ground2) { var cc = new HashSet<int>(); foreach (var disc in discs) for (int ci = 0; ci < Planet.Cells.Length; ci++) if (Vector3.Angle(Planet.Cells[ci].dir, Planet.Cells[disc.c].dir) * Mathf.Deg2Rad * Planet.R < disc.R + 10f) cc.Add(ci); bouw.AppendLine(PlanetCity.Ground2Check(cc)); }
        bouw.Append("A terrein boven het wegdek: ").Append(nA).Append(" punten (").Append(nRoad > 0 ? (100f * nA / nRoad).ToString("0.00") : "0").AppendLine("%)");
        bouw.Append("B gaten in het terrein: ").Append(foutB.Count).AppendLine(" punten");
        bouw.Append("C mesh wijkt af van H: ").Append(foutC.Count).Append(" van ").Append(nC).Append(" punten (").Append(nC > 0 ? (100f * foutC.Count / nC).ToString("0.00") : "0").Append("%), hoogstens ").Append(maxC.ToString("0.00")).AppendLine(" m");
        if (worstC != "") bouw.Append("  ergste C: ").AppendLine(worstC);
        {   // per vak, ergste eerst
            var perC = new Dictionary<int, (int n, float max, Vector3 p)>(); foreach (var f in foutC) { int c = Planet.Nearest(f.p * Planet.R); perC.TryGetValue(c, out var t); perC[c] = (t.n + 1, Mathf.Max(t.max, f.d), f.d > t.max ? f.p : t.p); }
            var lc = new List<KeyValuePair<int, (int n, float max, Vector3 p)>>(perC); lc.Sort((p1, p2) => (p2.Value.n * p2.Value.max).CompareTo(p1.Value.n * p1.Value.max));
            foreach (var kv in lc.GetRange(0, Mathf.Min(8, lc.Count))) bouw.Append("  C vak ").Append(kv.Key).Append(": ").Append(kv.Value.n).Append(" punten, tot ").Append(kv.Value.max.ToString("0.00")).Append(" m; ").Append(Desc(kv.Key)).AppendLine();
            for (int k = 0; k < Mathf.Min(3, lc.Count); k++) { var pC = lc[k].Value.p; Physics.Raycast(pC * (Planet.R + 150f), -pC, out RaycastHit hm, 180f, 1 << Layers.Bodem); bouw.Append("  C punt ").Append(k + 1).Append(": mesh ").Append((hm.point.magnitude - Planet.R).ToString("0.00")).Append(" (").Append(hm.collider ? hm.collider.name : "-").Append(") ").Append(PlanetCity.HInfo(pC)).AppendLine(); int cc = Planet.Nearest(pC * Planet.R); var cl = Planet.Cells[cc]; int kb = 0; for (int q = 1; q < cl.corners.Length; q++) if (Vector3.Dot(cl.corners[q], pC) > Vector3.Dot(cl.corners[kb], pC)) kb = q; bouw.Append("    punt: ").AppendLine(PlanetCity.GroundInfo(pC)); bouw.Append("    driehoek: ").AppendLine(TriInfo(hm)); bouw.Append("    hoek ").Append(kb).Append(" (").Append(PlanetCity.OwnCorner(cc, kb).ToString("0.00")).Append("): ").AppendLine(PlanetCity.GroundInfo(cl.corners[kb])); }
            cShots = new List<int>(); for (int k = 0; k < Mathf.Min(3, lc.Count); k++) cShots.Add(lc[k].Key);
            if (PlanetCity.Ground2) { var dump = new System.Text.StringBuilder(); for (int k = 0; k < Mathf.Min(3, lc.Count); k++) dump.AppendLine(PlanetCity.Ground2Dump(lc[k].Key)); if (lc.Count > 0) foreach (int nb in Planet.Cells[lc[0].Key].nb) dump.Append("BUUR ").AppendLine(PlanetCity.Ground2Dump(nb)); dumpB = dump; }   // (ook de buren van het ergste vak: een randpunt dat van twee kanten verschilt)
        }
        // groeperen per vak, ergste eerst
        var perA = new Dictionary<int, (int n, float max, Vector3 p)>(); foreach (var f in foutA) { int c = Planet.Nearest(f.p * Planet.R); perA.TryGetValue(c, out var t); perA[c] = (t.n + 1, Mathf.Max(t.max, f.d), t.n == 0 ? f.p : t.p); }
        var perB = new Dictionary<int, (int n, Vector3 p)>(); foreach (var f in foutB) { int c = Planet.Nearest(f * Planet.R); perB.TryGetValue(c, out var t); perB[c] = (t.n + 1, t.n == 0 ? f : t.p); }
        var la = new List<KeyValuePair<int, (int n, float max, Vector3 p)>>(perA); la.Sort((p1, p2) => (p2.Value.n * p2.Value.max).CompareTo(p1.Value.n * p1.Value.max));
        var lb = new List<KeyValuePair<int, (int n, Vector3 p)>>(perB); lb.Sort((p1, p2) => p2.Value.n.CompareTo(p1.Value.n));
        scanWorst = lb.Count > 0 ? lb[0].Key : la.Count > 0 ? la[0].Key : -1;
        for (int k = 0; k < Mathf.Min(3, la.Count); k++) { var pA = la[k].Value.p; Vector3 tp = pA * (Planet.R + 60f); Physics.Raycast(tp, -pA, out RaycastHit h1, 80f, 1 << Layers.Weg); Physics.Raycast(tp, -pA, out RaycastHit h2, 80f, 1 << Layers.Bodem); bouw.Append("  punt ").Append(k + 1).Append(": weg ").Append((h1.point.magnitude - Planet.R).ToString("0.00")).Append(" (").Append(h1.collider != null ? h1.collider.transform.parent?.name + "/" + h1.collider.name : "-").Append("), grond ").Append((h2.point.magnitude - Planet.R).ToString("0.00")).Append("; ").AppendLine(PlanetCity.GroundInfo(pA)); }
        foreach (var kv in la.GetRange(0, Mathf.Min(12, la.Count))) bouw.Append("  A vak ").Append(kv.Key).Append(": ").Append(kv.Value.n).Append(" punten, tot ").Append(kv.Value.max.ToString("0.00")).Append(" m; ").Append(Desc(kv.Key)).Append(foutAHit.TryGetValue(kv.Value.p, out var hitName) ? " | geraakt: " + hitName : "").AppendLine();
        if (dumpB != null) { for (int k = 0; k < Mathf.Min(3, lb.Count); k++) dumpB.Append("GAT ").AppendLine(PlanetCity.Ground2Dump(lb[k].Key)); for (int k = 0; k < Mathf.Min(2, la.Count); k++) dumpB.Append("BOVEN WEG ").AppendLine(PlanetCity.Ground2Dump(la[k].Key)); File.WriteAllText(Path.Combine(Dir, name + "_dump.txt"), dumpB.ToString()); dumpB = null; }
        foreach (var kv in lb.GetRange(0, Mathf.Min(12, lb.Count))) bouw.Append("  B vak ").Append(kv.Key).Append(": ").Append(kv.Value.n).Append(" punten; ").Append(Desc(kv.Key)).AppendLine();
        File.WriteAllText(Path.Combine(Dir, name + ".txt"), bouw.ToString());
        yield return Shot(name + "_overzicht", home, Dh(home, 0.3f), R * 1.6f, 60f);
        for (int k = 0; k < Mathf.Min(5, la.Count); k++) yield return Shot(name + "_A" + (k + 1), la[k].Key, Dh(la[k].Key, 0.7f), 26f, 40f);
        for (int k = 0; k < Mathf.Min(4, lb.Count); k++) yield return Shot(name + "_B" + (k + 1), lb[k].Key, Dh(lb[k].Key, 0.7f), 24f, 55f);
        for (int k = 0; k < cShots.Count; k++) yield return Shot(name + "_C" + (k + 1), cShots[k], Dh(cShots[k], 0.7f), 26f, 40f);
    }
    // de geraakte driehoek van de mesh: zijn drie hoekpunten (hoogte en vak) en de hoogtefunctie daar
    static string TriInfo(RaycastHit h)
    {
        var mc = h.collider as MeshCollider; if (mc == null || mc.sharedMesh == null || h.triangleIndex < 0) return "-";
        var mesh = mc.sharedMesh; var tr = mesh.triangles; var vs = mesh.vertices; var sb = new System.Text.StringBuilder(); Vector3 sum = Vector3.zero;
        for (int q = 0; q < 3; q++)
        {
            Vector3 w = mc.transform.TransformPoint(vs[tr[h.triangleIndex * 3 + q]]); Vector3 d = w.normalized; sum += d;
            sb.Append(q == 0 ? "" : " / ").Append((w.magnitude - Planet.R).ToString("0.00")).Append(" vak ").Append(Planet.Nearest(w)).Append(" H ").Append(PlanetCity.H(d).ToString("0.00"));
            sb.Append("\n      hoekpunt ").Append(q).Append(": ").Append(PlanetCity.GroundInfo(d));
        }
        sb.Append("\n      midden: ").Append(PlanetCity.GroundInfo(sum.normalized));
        return sb.ToString();
    }
    System.Text.StringBuilder dumpB;
    List<int> cShots = new List<int>();   // de vakken waar de mesh het meest van H afwijkt (voor de foto's)
    // de grootste steden die de AI-bouwer bewaarde (ai-runs, per bouwstijl de grootste) laden en scannen
    IEnumerator AiSteden()
    {
        string dir = Path.Combine(Application.persistentDataPath, "ai-runs"); var files = new List<FileInfo>(new DirectoryInfo(dir).GetFiles("*.json"));
        files.Sort((x1, x2) => x2.Length.CompareTo(x1.Length)); var pick = new List<FileInfo>(); var styles = new HashSet<string>();
        foreach (var f in files) { string st = f.Name.Split('_').Length > 2 ? f.Name.Split('_')[2] : f.Name; if (styles.Add(st)) pick.Add(f); if (pick.Count >= 5) break; }
        var sum = new System.Text.StringBuilder();
        for (int q = 0; q < pick.Count; q++)
        {
            PlanetCity.ClearAll(); if (!PlanetCity.Restore(File.ReadAllText(pick[q].FullName))) { sum.Append(pick[q].Name).AppendLine(": niet te laden"); continue; }
            PlanetCity.TouchAll(); PlanetCity.Rebuild(); PlanetCity.RebuildGrid(); yield return null; yield return null;
            Vector3 m = Vector3.zero; int nr = 0; foreach (var kv in PlanetCity.Cells) if (PlanetCity.IsRoad(kv.Key)) { m += Planet.Cells[kv.Key].dir; nr++; }
            int c = Planet.Nearest(m.normalized * Planet.R); Hc = c; bouw = new System.Text.StringBuilder(pick[q].Name + "\n");
            yield return Scan(c, 230f, "stad" + (q + 1));
            var rl = bouw.ToString().Split('\n');
            sum.Append("stad").Append(q + 1).Append(" ").Append(pick[q].Name).Append(": ").Append(nr).Append(" wegvakken; ").AppendLine((rl.Length > 3 ? rl[2] + " | " + rl[3] : ""));
            File.WriteAllText(Path.Combine(Dir, "aisteden.txt"), sum.ToString());
        }
        File.WriteAllText(Path.Combine(Dir, "klaar.txt"), "klaar"); if (pb != null) pb.enabled = true;
    }
    // De laatst bewaarde stad van de speler (steden/<id>.json) laden en scannen rond elke bocht, kruising, doodlopend eind en
    // landhoofd op ongelijk terrein (daar sluit het terrein het lastigst aan).
    // Een opgeslagen stad nakijken ("stad <id>", het bestand in de map steden; alleen lezen, niets wordt bewaard): opnieuw tekenen, een
    // scan langs de wegen (hooguit 60 plekken, minstens 40 m uit elkaar) en de vakken waar het maaiveld in het midden het verst onder
    // de vakhoogte ligt terwijl er iets op staat (zweeft daar iets?), met foto's van de ergste drie en twee overzichten.
    IEnumerator StadNakijken(string mode)
    {
        string paar = null; { int pi = mode.IndexOf(" paar="); if (pi >= 0) { paar = mode.Substring(pi + 6).Trim(); mode = mode.Substring(0, pi) + " einden"; } } if (mode.Contains(" breed")) { PlanetCity.DragStyle = 1; mode = mode.Replace(" breed", ""); }
        bool opslaan = mode.EndsWith(" opslaan"); if (opslaan) mode = mode.Substring(0, mode.Length - 8);
        var lijnen = new List<List<int>>(); while (true) { int li = mode.IndexOf(" lijn="); if (li < 0) break; int le = mode.IndexOf(' ', li + 6); string ls = le < 0 ? mode.Substring(li + 6) : mode.Substring(li + 6, le - li - 6); var ln = new List<int>(); foreach (var ps in ls.Split('.')) ln.Add(int.Parse(ps)); lijnen.Add(ln); mode = mode.Remove(li, (le < 0 ? mode.Length : le) - li); }   // "lijn=a.b.c": die weg opnieuw profileren (zoals na het slepen), niet opslaan
        var fotos = new List<int>(); { int fi = mode.IndexOf(" foto="); if (fi >= 0) { foreach (var ps in mode.Substring(fi + 6).Trim().Split(',')) fotos.Add(int.Parse(ps)); mode = mode.Substring(0, fi); } }
        string mode0 = mode; if (mode.EndsWith(" overwater")) mode = mode.Substring(0, mode.Length - 10);
        bool einden = mode.EndsWith(" einden"); if (einden) mode = mode.Substring(0, mode.Length - 7); bool schuin = mode.EndsWith(" schuin"); if (schuin) mode = mode.Substring(0, mode.Length - 7); bool brug = mode.EndsWith(" brug"); if (brug) mode = mode.Substring(0, mode.Length - 5);
        string id = mode.Substring(4).Trim(), file = Path.Combine(CityFiles.Dir, id + ".json"); bouw = new System.Text.StringBuilder(id + "\n");
        if (!File.Exists(file)) { File.WriteAllText(Path.Combine(Dir, "stad.txt"), "GEEN BESTAND " + file); File.WriteAllText(Path.Combine(Dir, "klaar.txt"), "klaar"); yield break; }
        PlanetCity.Ground2 = true; PlanetCity.ClearAll(); bool ok = PlanetCity.Restore(File.ReadAllText(file));
        var sw = System.Diagnostics.Stopwatch.StartNew(); PlanetCity.TouchAll(); PlanetCity.Rebuild(); PlanetCity.RebuildGrid(); long ms = sw.ElapsedMilliseconds; yield return null; yield return null;
        var log = new System.Text.StringBuilder();
        log.Append(id).Append(": laden ").Append(ok ? "gelukt" : "MISLUKT").Append(", ").Append(PlanetCity.Cells.Count).Append(" vakken met inhoud, RoadH ").Append(PlanetCity.RoadH.Count).Append(", tekenen ").Append(ms).AppendLine(" ms");
        foreach (var ln in lijnen) { PlanetCity.LastNote = null; PlanetCity.Reprofile(ln); log.Append("opnieuw geprofileerd: ").Append(string.Join(".", ln)).Append(", melding: ").AppendLine(PlanetCity.LastNote ?? "-"); }
        if (lijnen.Count > 0) { PlanetCity.Rebuild(); PlanetCity.Rebuild(); yield return null; yield return null; }
        if (opslaan && lijnen.Count > 0)   // "opslaan": de stad met de opnieuw geprofileerde wegen in zijn eigen bestand bewaren (de laatst geopende stad blijft dezelfde)
        {
            var ci = CityFiles.All.Find(cc => cc.id == id); string keepLast = PlayerPrefs.GetString("raatstad-laatste-stad", "");
            bool okS = ci != null && CityFiles.Save(ci, null, PlanetCity.Snapshot()); PlayerPrefs.SetString("raatstad-laatste-stad", keepLast); PlayerPrefs.Save();
            log.Append("opgeslagen in ").Append(id).Append(": ").AppendLine(okS ? "ja" : "NEE");
        }
        if (fotos.Count > 0)   // "foto=a,b": van schuin boven en van opzij bij die vakken, met wat daar ligt
        {
            var sb2 = new System.Text.StringBuilder();
            foreach (int fc in fotos)
            {
                var cc = Planet.Cells[fc]; sb2.Append(fc).Append(": ").Append(Desc(fc)).Append(", terrein ").Append(PlanetCity.Terrain(fc).ToString("0.0")).Append(", nat ").Append(PlanetCity.Wet(fc)).Append(", partners");
                foreach (int q in PlanetCity.Partners(fc)) sb2.Append(' ').Append(q).Append(PlanetCity.IsSpan(fc, q) ? "(brug)" : PlanetCity.IsBore(fc, q) ? "(tunnel)" : PlanetCity.LowerLinks.Contains(fc < q ? ((long)fc << 20) | (uint)q : ((long)q << 20) | (uint)fc) ? "(onder)" : "");
                sb2.Append("; buren"); foreach (int j in cc.nb) sb2.Append(' ').Append(j).Append('=').Append(PlanetCity.Terrain(j).ToString("0.0")).Append(PlanetCity.Wet(j) ? "nat" : ""); sb2.AppendLine();
                Vector3 gp = cc.dir * (Planet.R + PlanetCity.G(fc));
                yield return ShotAt("stad_foto_" + fc + "_boven", gp, cc.north * 35f + cc.dir * 55f);
                yield return ShotAt("stad_foto_" + fc + "_opzij", gp, cc.east * 45f + cc.dir * 12f); yield return ShotAt("stad_foto_" + fc + "_laag", gp, cc.east * 26f + cc.north * 14f - cc.dir * 2f);
            }
            File.WriteAllText(Path.Combine(Dir, "stad.txt"), log.ToString() + sb2.ToString()); File.WriteAllText(Path.Combine(Dir, "klaar.txt"), "klaar"); if (pb != null) pb.enabled = true; yield break;
        }
        if (mode0.EndsWith(" overwater"))   // welke wegverbindingen liggen boven water: per verbinding de vakken, hoogtes, en wat ze zijn
        {
            var seen = new HashSet<long>(); int nw = 0;
            foreach (var kv in PlanetCity.Cells)
            {
                int a0 = kv.Key; if (!PlanetCity.IsWalk(a0)) continue;
                foreach (int b0 in PlanetCity.Partners(a0))
                {
                    long key = ((long)Mathf.Min(a0, b0) << 20) | (uint)Mathf.Max(a0, b0); if (!seen.Add(key)) continue;
                    Vector3 da = Planet.Cells[a0].dir, db = Planet.Cells[b0].dir; int wet = 0; var wc = new List<int>();
                    for (int t = 0; t <= 10; t++) { int x = Planet.Nearest(Vector3.Slerp(da, db, t / 10f) * Planet.R); if (PlanetCity.Terrain(x) < PlanetCity.SeaLevel - 0.05f) { wet++; if (!wc.Contains(x)) wc.Add(x); } }
                    if (wet == 0) continue; nw++;
                    log.Append("  ").Append(a0).Append("-").Append(b0).Append(": ").Append(wet).Append("/11 meetpunten boven water (vakken ").Append(string.Join(",", wc)).Append("), weg ").Append(PlanetCity.G(a0).ToString("0.0")).Append(" en ").Append(PlanetCity.G(b0).ToString("0.0"))
                       .Append(PlanetCity.IsSpan(a0, b0) ? ", BRUG" : PlanetCity.IsBore(a0, b0) ? ", tunnel" : "").Append(", partners ").Append(PlanetCity.Partners(a0).Count).Append("/").Append(PlanetCity.Partners(b0).Count).Append(", ").Append(Desc(a0)).AppendLine();
                }
            }
            log.Insert(0, "verbindingen boven water: " + nw + System.Environment.NewLine);
            File.WriteAllText(Path.Combine(Dir, "stad.txt"), log.ToString()); File.WriteAllText(Path.Combine(Dir, "klaar.txt"), "klaar"); if (pb != null) pb.enabled = true; yield break;
        }
        if (brug) { yield return BrugProef(log, schuin, einden, paar); File.WriteAllText(Path.Combine(Dir, "stad.txt"), log.ToString()); File.WriteAllText(Path.Combine(Dir, "klaar.txt"), "klaar"); if (pb != null) pb.enabled = true; yield break; }
        var centers = new List<int>(); var gezakt = new List<(float d, int c)>(); int first = -1;
        foreach (var kv in PlanetCity.Cells)
        {
            int i = kv.Key; if (kv.Value == null || kv.Value.Count == 0) continue; if (first < 0) first = i;
            if (PlanetCity.IsWalk(i))
            {
                if (centers.Count >= 60) continue; bool far = true;
                foreach (int c in centers) if (Vector3.Distance(Planet.Cells[c].dir, Planet.Cells[i].dir) * Planet.R < 40f) { far = false; break; }
                if (far) centers.Add(i); continue;
            }
            float d = PlanetCity.G(i) - PlanetCity.TerrainAt(Planet.Cells[i].dir); if (d > 0.3f) gezakt.Add((d, i));
        }
        gezakt.Sort((x, y) => y.d.CompareTo(x.d));
        log.Append("vakken met inhoud waar het maaiveld in het midden meer dan 0,3 m onder de vakhoogte ligt: ").Append(gezakt.Count).AppendLine();
        for (int k = 0; k < Mathf.Min(8, gezakt.Count); k++) log.Append("  vak ").Append(gezakt[k].c).Append(": ").Append(gezakt[k].d.ToString("0.00")).Append(" m; ").AppendLine(Desc(gezakt[k].c));
        File.WriteAllText(Path.Combine(Dir, "stad.txt"), log.ToString());
        if (centers.Count > 0) { Hc = centers[0]; yield return Scan(centers[0], 0f, "stad_scan", centers, 24f); log.Append(bouw.ToString()); }
        for (int k = 0; k < Mathf.Min(3, gezakt.Count); k++) { int c = gezakt[k].c; yield return Shot("stad_gezakt" + k, c, Dh(c, 0.6f), 38f, 22f); }
        int mid = centers.Count > 0 ? centers[centers.Count / 2] : first;
        if (mid >= 0) { yield return Shot("stad_overzicht0", mid, Dh(mid, 0.4f), 260f, 50f); yield return Shot("stad_overzicht1", mid, Dh(mid, 2.5f), 120f, 30f); }
        File.WriteAllText(Path.Combine(Dir, "stad.txt"), log.ToString());
        File.WriteAllText(Path.Combine(Dir, "klaar.txt"), "klaar"); if (pb != null) pb.enabled = true;
    }
    // "stad <id> brug": op een paar plekken een weg recht over het water slepen (zoals de speler: LinePlan, PlaceLine, SetLevel, Reprofile)
    // en opschrijven wat het voorbeeld zegt en wat er komt. Elke proef begint weer bij de opgeslagen stad.
    IEnumerator BrugProef(System.Text.StringBuilder log, bool schuin, bool einden, string paar = null)
    {
        string snap = PlanetCity.Snapshot(); int done = 0; var used = new List<int>(); int shot = 0;
        int Walk(int c, Vector3 to, int steps)
        {
            for (int s = 0; s < steps; s++) { Vector3 cd = Planet.Cells[c].dir; int best = -1; float bd = -2f; foreach (int j in Planet.Cells[c].nb) { if (j < 0) continue; float d = Vector3.Dot((Planet.Cells[j].dir - cd).normalized, to); if (d > bd) { bd = d; best = j; } } if (best < 0) break; c = best; }
            return c;
        }
        if (einden)   // tussen twee wegeinden die tegenover elkaar aan het water liggen
        {
            var ends = new List<int>(); foreach (var kv in PlanetCity.Cells) if (PlanetCity.IsRoad(kv.Key) && PlanetCity.Partners(kv.Key).Count == 1) ends.Add(kv.Key);
            log.Append("wegeinden: ").Append(ends.Count).AppendLine();
            var wl = new List<int>(); for (int i = 0; i < Planet.Cells.Length; i++) if (PlanetCity.Terrain(i) < PlanetCity.SeaLevel - 0.05f) wl.Add(i);
            foreach (int e in ends) { float md = 9999f; foreach (int w in wl) md = Mathf.Min(md, Vector3.Distance(Planet.Cells[e].dir, Planet.Cells[w].dir) * Planet.R); log.Append("  eind ").Append(e).Append(" G ").Append(PlanetCity.G(e).ToString("0.0")).Append(", water op ").Append(md.ToString("0")).Append(" m; andere einden:"); foreach (int f in ends) if (f != e) { float dd = Vector3.Distance(Planet.Cells[e].dir, Planet.Cells[f].dir) * Planet.R; if (dd < 320f) log.Append(' ').Append(f).Append('(').Append(dd.ToString("0")).Append(" m)"); } log.AppendLine(); }
            var pairs = new List<(int a, int b)>();
            if (paar != null) { var ab = paar.Split('-'); pairs.Add((int.Parse(ab[0]), int.Parse(ab[1]))); }
            else
            {   // de schuine weg die nu als dam door het water ligt (halve vakken onder het zeeniveau): weghalen en opnieuw slepen tussen de einden die overblijven
                var seen2 = new HashSet<long>(); var dams = new List<(int a, int b)>();
                foreach (var kv in PlanetCity.Cells) { int c = kv.Key; if (!PlanetCity.IsRoad(c)) continue; foreach (int q in PlanetCity.Partners(c)) { if (q < c || Vector3.Distance(Planet.Cells[c].dir, Planet.Cells[q].dir) * Planet.R < 20f) continue; bool w = false; foreach (int hx in PlanetCity.Between(c, q)) if (PlanetCity.Terrain(hx) < PlanetCity.SeaLevel - 0.05f) w = true; if (w) dams.Add((c, q)); } }
                foreach (var dm in dams)
                {
                    log.Append("schuine weg door het water: ").Append(dm.a).Append('-').Append(dm.b).Append(" (terrein ").Append(PlanetCity.Terrain(dm.a).ToString("0.0")).Append(" en ").Append(PlanetCity.Terrain(dm.b).ToString("0.0")).Append("), partners ");
                    foreach (int q in PlanetCity.Partners(dm.a)) log.Append(q).Append(' '); log.Append("| "); foreach (int q in PlanetCity.Partners(dm.b)) log.Append(q).Append(' '); log.AppendLine();
                }
                if (dams.Count > 0)
                {
                    var dm = dams[0]; int pa = -1, pb = -1; foreach (int q in PlanetCity.Partners(dm.a)) if (q != dm.b) pa = q; foreach (int q in PlanetCity.Partners(dm.b)) if (q != dm.a) pb = q;
                    if (pa >= 0 && pb >= 0 && PlanetCity.Partners(dm.a).Count == 2 && PlanetCity.Partners(dm.b).Count == 2)
                    {
                        PlanetCity.Clear(dm.a); PlanetCity.Clear(dm.b); PlanetCity.Reprofile(); snap = PlanetCity.Snapshot(); pairs.Add((pa, pb));
                        log.Append("weggehaald: ").Append(dm.a).Append(" en ").Append(dm.b).Append("; opnieuw slepen van ").Append(pa).Append(" naar ").Append(pb).AppendLine();
                    }
                }
            }
            if (paar == null) foreach (int e in ends)   // rechtdoor vanaf het wegeind: de eerste weg aan de overkant van het water
            {
                int pr = PlanetCity.Partners(e)[0]; Vector3 dir = (Planet.Cells[e].dir - Planet.Cells[pr].dir).normalized; int c = e; bool wet = false;
                for (int st = 0; st < 14; st++) { c = Walk(c, dir, 1); if (PlanetCity.Terrain(c) < PlanetCity.SeaLevel - 0.05f) wet = true; if (PlanetCity.IsRoad(c)) { if (wet) pairs.Add((e, c)); break; } }
            }
            if (pairs.Count == 0) for (int x = 0; x < ends.Count; x++) for (int y = x + 1; y < ends.Count; y++) { float dd = Vector3.Distance(Planet.Cells[ends[x]].dir, Planet.Cells[ends[y]].dir) * Planet.R; if (dd <= 320f && dd >= 20f) pairs.Add((ends[x], ends[y])); }
            log.Append("rechtdoor over het water naar een weg: "); foreach (var pp in pairs) log.Append(pp.a).Append('-').Append(pp.b).Append(' '); log.AppendLine();
            foreach (var pp in pairs)
            {
                int a = pp.a, b = pp.b; float dist = Vector3.Distance(Planet.Cells[a].dir, Planet.Cells[b].dir) * Planet.R;
                var line = PlanetCity.LinePlan(a, b); if (line == null || line.Count < 3) continue; if (line[line.Count - 1] != b && Vector3.Distance(Planet.Cells[line[line.Count - 1]].dir, Planet.Cells[b].dir) * Planet.R < 30f) line.Add(b); int nat = 0; foreach (int c in line) if (PlanetCity.Terrain(c) < PlanetCity.SeaLevel - 0.05f) nat++;

                log.Append("proef ").Append(done).Append(": van wegeind ").Append(a).Append(" naar wegeind ").Append(b).Append(", ").Append(line.Count).Append(" vakken, ").Append(nat).AppendLine(" in het water");
                for (int q = 0; q < line.Count; q++) { int c = line[q]; log.Append("   ").Append(c).Append(" terrein ").Append(PlanetCity.Terrain(c).ToString("0.0")).Append(" G ").Append(PlanetCity.G(c).ToString("0.0")).Append(PlanetCity.CanRoad(c, "weg") ? "" : " KAN NIET").Append(PlanetCity.Cells.TryGetValue(c, out var l0) && l0.Count > 0 ? " " + l0[0] : "").Append(q > 0 && Vector3.Distance(Planet.Cells[line[q - 1]].dir, Planet.Cells[c].dir) * Planet.R > 20f ? " (schuine stap)" : "").Append(" partners ").Append(PlanetCity.Partners(c).Count).AppendLine(); }
                string ps = ""; foreach (var t in PlanetCity.BorePlan(line, "weg")) ps += (t.span ? "brug " : "tunnel ") + t.a + "-" + t.b + " (" + t.ha.ToString("0.0") + " en " + t.hb.ToString("0.0") + ") ";
                log.Append("   voorbeeld: ").AppendLine(ps == "" ? "geen brug of tunnel" : ps);
                log.Append("   waterlijn: ").AppendLine(PlanetCity.WaterLineDebug(line)); log.Append("   verbreden: ").AppendLine(PlanetCity.WidenDbg);
                PlanetCity.LastNote = null; int ok = PlanetCity.PlaceLine(line, "weg"); PlanetCity.SetLevel(line, 0, "weg"); log.Append(PlanetCity.LineDebug(line)); PlanetCity.Reprofile(line);
                int spans = 0; foreach (long key in PlanetCity.SpanLinks) { int u = (int)(key >> 20), v = (int)(key & 0xFFFFF); if (line.Contains(u) && line.Contains(v)) spans++; }
                log.Append("   gelegd: ").Append(ok).Append('/').Append(line.Count).Append(" vakken, bruggen op de lijn ").Append(spans).Append(", melding: ").AppendLine(PlanetCity.LastNote ?? "-");
                if (paar != null) { PlanetCity.Rebuild(); PlanetCity.Rebuild(); yield return null; yield return null; foreach (int ec in new[] { a, b }) { var ce = Planet.Cells[ec]; Vector3 gp = ce.dir * (Planet.R + PlanetCity.G(ec)); yield return ShotAt("stad_eind_" + ec, gp, ce.east * 26f + ce.north * 14f - ce.dir * 2f); yield return ShotAt("stad_eind_" + ec + "_b", gp, -ce.east * 26f - ce.north * 14f + ce.dir * 1f); } } if (shot < 3) { PlanetCity.Rebuild(); PlanetCity.Rebuild(); yield return null; yield return null; int mc = line[line.Count / 2]; yield return Shot("stad_einden" + shot, mc, Quaternion.AngleAxis(60f, Planet.Cells[mc].dir) * (Planet.Cells[b].dir - Planet.Cells[a].dir).normalized, 80f, 28f); shot++; }
                PlanetCity.ClearAll(); PlanetCity.Restore(snap); done++; if (done >= 8) break;
            }
            PlanetCity.TouchAll(); PlanetCity.Rebuild(); yield return null; yield break;
        }
        var wets = new List<int>(); for (int i = 0; i < Planet.Cells.Length; i++) if (PlanetCity.Terrain(i) < PlanetCity.SeaLevel - 0.05f && !PlanetCity.Cells.ContainsKey(i)) wets.Add(i);
        log.Append("vakken onder water: ").Append(wets.Count).AppendLine();
        foreach (int w in wets)
        {
            if (done >= 6) break; bool far = true; foreach (int u in used) if (Vector3.Distance(Planet.Cells[u].dir, Planet.Cells[w].dir) * Planet.R < 90f) { far = false; break; }
            if (!far) continue;
            var nb = Planet.Cells[w].nb; if (nb.Length != 6) continue;
            for (int k = 0; k < 3; k++)
            {
                if (nb[k] < 0 || nb[k + 1] < 0) continue; Vector3 dir = (Planet.Cells[nb[k]].dir - Planet.Cells[w].dir).normalized; if (schuin) dir = (dir + (Planet.Cells[nb[k + 1]].dir - Planet.Cells[w].dir).normalized).normalized;   // schuin: tussen twee zeshoekrijen in
                int a = w, b = w, na = 0, nb2 = 0;
                while (na < 12 && PlanetCity.Terrain(a) < PlanetCity.SeaLevel - 0.05f) { a = Walk(a, -dir, 1); na++; }
                while (nb2 < 12 && PlanetCity.Terrain(b) < PlanetCity.SeaLevel - 0.05f) { b = Walk(b, dir, 1); nb2++; }
                if (na >= 12 || nb2 >= 12) continue;   // te breed: de zee
                a = Walk(a, -dir, 2); b = Walk(b, dir, 2);
                var line = PlanetCity.LinePlan(a, b); if (line == null || line.Count < 4) continue;
                log.Append("proef ").Append(done).Append(": van ").Append(a).Append(" naar ").Append(b).Append(", ").Append(line.Count).Append(" vakken, water ").Append(na + nb2 - 1).AppendLine(" breed");
                foreach (int c in line) log.Append("   ").Append(c).Append(" terrein ").Append(PlanetCity.Terrain(c).ToString("0.0")).Append(PlanetCity.Wet(c) ? " nat" : "").Append(PlanetCity.CanRoad(c, "weg") ? "" : " KAN NIET").Append(PlanetCity.Cells.TryGetValue(c, out var l0) && l0.Count > 0 ? " " + l0[0] : "").AppendLine();
                string ps = ""; foreach (var t in PlanetCity.BorePlan(line, "weg")) ps += (t.span ? "brug " : "tunnel ") + t.a + "-" + t.b + " (" + t.ha.ToString("0.0") + " en " + t.hb.ToString("0.0") + ") ";
                log.Append("   voorbeeld: ").AppendLine(ps == "" ? "geen brug of tunnel" : ps);
                PlanetCity.LastNote = null; int ok = PlanetCity.PlaceLine(line, "weg"); PlanetCity.SetLevel(line, 0, "weg"); PlanetCity.Reprofile(line);
                int spans = 0; foreach (long key in PlanetCity.SpanLinks) { int x = (int)(key >> 20), y = (int)(key & 0xFFFFF); if (line.Contains(x) && line.Contains(y)) spans++; }
                log.Append("   gelegd: ").Append(ok).Append('/').Append(line.Count).Append(" vakken, bruggen op de lijn ").Append(spans).Append(", melding: ").AppendLine(PlanetCity.LastNote ?? "-");
                if (shot < 2) { PlanetCity.Rebuild(); PlanetCity.Rebuild(); yield return null; yield return null; yield return Shot("stad_brugproef" + shot, w, Quaternion.AngleAxis(60f, Planet.Cells[w].dir) * dir, 70f, 25f); shot++; }
                PlanetCity.ClearAll(); PlanetCity.Restore(snap); done++; used.Add(w); break;
            }
        }
        PlanetCity.TouchAll(); PlanetCity.Rebuild(); yield return null;
    }
    IEnumerator MijnStad()
    {
        var files = new List<FileInfo>(new DirectoryInfo(Path.Combine(Application.persistentDataPath, "steden")).GetFiles("*.json")); files.RemoveAll(f => f.Name.EndsWith(".info.json"));
        files.Sort((x1, x2) => x2.LastWriteTime.CompareTo(x1.LastWriteTime));
        var bug = files.Find(f => { string info = Path.ChangeExtension(f.FullName, null) + ".info.json"; return File.Exists(info) && File.ReadAllText(info).ToLower().Contains("bug"); });
        if (bug != null) { files.Remove(bug); files.Insert(0, bug); }   // de teststad van de speler ("bug planet") gaat voor
        bouw = new System.Text.StringBuilder(files[0].Name + "\n");
        PlanetCity.ClearAll(); PlanetCity.Restore(File.ReadAllText(files[0].FullName)); PlanetCity.TouchAll(); PlanetCity.Rebuild(); PlanetCity.RebuildGrid(); yield return null; yield return null;
        var centers = new List<int>();
        foreach (var kv in PlanetCity.Cells)
        {
            int i = kv.Key; if (!PlanetCity.IsWalk(i)) continue; var ps = PlanetCity.Partners(i); bool special = ps.Count != 2;
            if (ps.Count == 2) { Vector3 d = Planet.Cells[i].dir, t0 = Planet.Cells[ps[0]].dir - d, t1 = Planet.Cells[i].dir - Planet.Cells[ps[1]].dir; special = Vector3.Dot(t0.normalized, -t1.normalized) > -0.95f || PlanetCity.IsBore(i, ps[0]) || PlanetCity.IsBore(i, ps[1]); }
            float g = PlanetCity.G(i), dg = 0f; foreach (int j in Planet.Cells[i].nb) dg = Mathf.Max(dg, Mathf.Abs(PlanetCity.G(j) - g)); if (dg < (special ? 0.5f : 0.3f)) continue;   // ook een recht stuk op een helling
            bool far = true; foreach (int c in centers) if (Vector3.Distance(Planet.Cells[c].dir, Planet.Cells[i].dir) * Planet.R < 20f) { far = false; break; }
            if (far) centers.Add(i);
        }
        bouw.Append(centers.Count).AppendLine(" plekken (bocht, kruising, eind, landhoofd op ongelijk terrein)"); Hc = centers.Count > 0 ? centers[0] : 0;
        if (centers.Count > 0) yield return Scan(centers[0], 0f, "mijnstad", centers, 24f);
        int worstOn = scanWorst, worstOff = -1;
        PlanetCity.RoadShape = false; PlanetCity.TouchAll(); PlanetCity.Rebuild(); yield return null; yield return null;
        { var keep = bouw; bouw = new System.Text.StringBuilder("kaal (terrein door wegen uit)\n"); yield return Scan(centers[0], 0f, "mijnstad_uit", centers, 24f); bouw = keep; worstOff = scanWorst; }
        { int nw = 0; foreach (var kv in new List<KeyValuePair<int, List<string>>>(PlanetCity.Cells)) { if (!PlanetCity.IsRoad(kv.Key) || nw >= 3) continue; int wj = -1; foreach (int j in Planet.Cells[kv.Key].nb) if (PlanetCity.Wet(j)) wj = j; if (wj < 0) continue; nw++; yield return Shot("bug_water_uit" + nw, kv.Key, (Planet.Cells[wj].dir - Planet.Cells[kv.Key].dir).normalized, 30f, 25f); } }
        foreach (long k in PlanetCity.TunnelLinks) { int a = (int)(k >> 20), b = (int)(k & 0xFFFFF); if (!PlanetCity.IsSpan(a, b)) continue; yield return Shot("mijnstad_kaal" + a, a, (Planet.Cells[b].dir - Planet.Cells[a].dir).normalized, 28f, 30f); break; }
        PlanetCity.RoadShape = true; PlanetCity.TouchAll(); PlanetCity.Rebuild(); yield return null;
        if (files[0].Name.Contains("181837")) { yield return Shot("bug_ijs_aan", 18, (Planet.Cells[67].dir - Planet.Cells[18].dir).normalized, 26f, 25f); }
        int nk = 0; foreach (long k in PlanetCity.TunnelLinks) { int a = (int)(k >> 20), b = (int)(k & 0xFFFFF); if (!PlanetCity.IsSpan(a, b) || nk >= 6) continue; nk++; foreach (int e in new[] { a, b }) { int o = e == a ? b : a; yield return Shot("mijnstad_kop" + e, e, (Planet.Cells[o].dir - Planet.Cells[e].dir).normalized, 28f, 30f); } }
        Breng(worstOn >= 0 ? worstOn : worstOff >= 0 ? worstOff : centers.Count > 0 ? centers[0] : -1, worstOn >= 0 || worstOff >= 0 ? "Hier klopt het terrein nog niet" : "Geen fouten gevonden; dit is het lastigste stuk terrein");
        File.WriteAllText(Path.Combine(Dir, "klaar.txt"), "klaar"); if (pb != null) pb.enabled = true;
    }
    // de AI-bouwer laten bouwen op de heuvel uit zijn testopstelling (AiTest), dan scannen
    IEnumerator AiTerrein()
    {
        var S0 = Planet.Cells[Planet.North]; Vector3 d0 = S0.east * Mathf.Cos(1f) + S0.north * Mathf.Sin(1f);
        int start = Planet.Nearest((S0.dir * Mathf.Cos(170f / Planet.R) + d0 * Mathf.Sin(170f / Planet.R)).normalized * Planet.R); var S = Planet.Cells[start]; Hc = start; bouw = new System.Text.StringBuilder();
        for (int i = 0; i < Planet.Cells.Length; i++)
        {
            Vector3 w = Planet.Cells[i].dir * Planet.R - S.dir * Planet.R; if (w.magnitude > 400f) continue;
            float x = Vector3.Dot(w, S.east), y = Vector3.Dot(w, S.north);
            float hill = Mathf.Max(0f, 22f - 0.25f * Vector2.Distance(new Vector2(x, y), new Vector2(110f, 40f))), lake = Mathf.Max(0f, 5f - 0.12f * Vector2.Distance(new Vector2(x, y), new Vector2(-90f, -70f)));
            float h = hill - lake + Mathf.PerlinNoise(x * 0.01f + 3f, y * 0.01f) * 3f; if (Mathf.Abs(h) > 0.3f) PlanetCity.SetTerr(i, Mathf.Round(h * 4f) / 4f);
        }
        PlanetCity.Rebuild(); PlanetCity.RebuildGrid(); yield return null;
        var F = (System.Func<string, System.Reflection.FieldInfo>)(nm => typeof(PlanetBuilder).GetField(nm, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));
        pb.enabled = true; ((List<string>)F("undo").GetValue(pb)).Clear();
        string why = PlanetAI.Begin(start, 5, 0); if (why != null) { File.WriteAllText(Path.Combine(Dir, "aiterrein.txt"), "NIET: " + why); File.WriteAllText(Path.Combine(Dir, "klaar.txt"), "klaar"); yield break; }
        F("aiOn").SetValue(pb, true); F("aiPaused").SetValue(pb, false); F("aiTempo").SetValue(pb, 3); F("aiPinCell").SetValue(pb, start); F("aiNext").SetValue(pb, Time.time); F("aiWhat").SetValue(pb, "test"); F("aiBeauty0").SetValue(pb, 0); F("aiStartT").SetValue(pb, Time.time); F("aiUndoN").SetValue(pb, 0); F("aiFollow").SetValue(pb, false);
        float t0 = Time.time; while (Time.time - t0 < 90f && !(bool)F("aiPaused").GetValue(pb)) yield return null;
        F("aiPaused").SetValue(pb, true); for (int k = 0; k < 120 && (bool)F("rebuilding").GetValue(pb); k++) yield return null;
        pb.enabled = false; PlanetCity.Rebuild(); yield return null; yield return null;
        bouw.Append("AI: ").Append(PlanetAI.Moves).AppendLine(" zetten");
        yield return Scan(start, 190f, "aiterrein");
        { int c = 13383; foreach (int j in Planet.Cells[c].nb) { yield return Shot("detail_" + j + "_boven", j, Dh(j, 0f), 22f, 89f); } yield return Shot("detail_laag", c, Dh(c, 2.6f), 30f, 20f); yield return Shot("detail_laag2", c, Dh(c, 0.5f), 30f, 20f); }
        { var hits = new System.Text.StringBuilder(); var c = Planet.Cells[13383]; foreach (int j in c.nb) { var cj = Planet.Cells[j]; for (int k = 0; k < cj.corners.Length; k++) { Vector3 d = (cj.corners[k] * 0.7f + cj.dir * 0.3f).normalized, top = d * (Planet.R + 60f); var all = Physics.RaycastAll(top, -d, 80f, ~0, QueryTriggerInteraction.Ignore); System.Array.Sort(all, (x1, x2) => x1.distance.CompareTo(x2.distance)); hits.Append(j).Append('/').Append(k).Append(": "); foreach (var h in all) hits.Append(h.collider.name).Append('@').Append((h.point.magnitude - Planet.R).ToString("0.00")).Append(' '); hits.AppendLine(); } } File.WriteAllText(Path.Combine(Dir, "detail.txt"), hits.ToString()); }
        PlanetCity.RoadField = false; PlanetCity.TouchAll(); PlanetCity.Rebuild(); yield return null; yield return null; bouw = new System.Text.StringBuilder("ZONDER gewone wegen in de hoogtefunctie\n");
        yield return Scan(start, 190f, "aiterrein_uit"); PlanetCity.RoadField = true; PlanetCity.TouchAll(); PlanetCity.Rebuild();
        File.WriteAllText(Path.Combine(Dir, "klaar.txt"), "klaar"); pb.enabled = true;
    }
    // Daltest: een dal van zo'n 13 m diep met een beek, daarover een weg (recht), een fietspad en een weg die het dal schuin (90°) kruist;
    // wordt elk een brug over het dal (SpanLinks), klopt de graaf, en wat zegt het voorbeeld vooraf (BorePlan)?
    IEnumerator DalTest(int home)
    {
        Hc = home; var log = new System.Text.StringBuilder(); float D90 = Mathf.PI / 2f;
        foreach (int i in new List<int>(Nearby(home, 260f)))
        {
            Vector3 v = (Planet.Cells[i].dir - Planet.Cells[home].dir) * Planet.R; float y = Vector3.Dot(v, Dh(home, 0f)), x = Vector3.Dot(v, Dh(home, Mathf.PI / 2f));
            float w = x > 0f ? 32f : 20f, dd = x > 0f ? 14f : 10f;   // aan de ene kant een breed dal, aan de andere een smal (zoals een beekdal)
            float side = x < -130f ? -0.15f * (x + 130f) : 0f;   // het smalle dal op een berghelling (de landhoofden op een schuine helling)
            PlanetCity.SetTerr(i, Mathf.Round((9f - dd * Mathf.Exp(-y * y / (w * w)) + side) * 4f) / 4f);
        }
        var lines = new List<(string nm, int a, int b, string t, bool bike)> {
            ("weg recht", Go(home, 0f, -110f), Go(home, 0f, 110f), "weg", false),
            ("fietspad", Go(Go(home, D90, 70f), 0f, -110f), Go(Go(home, D90, 70f), 0f, 110f), "pad", true),
            ("weg schuin", Go(Go(home, D90, -90f), Mathf.PI / 6f, -120f), Go(Go(home, D90, -90f), Mathf.PI / 6f, 120f), "weg", false),
            ("weg smal dal", Go(Go(home, D90, -170f), 0f, -90f), Go(Go(home, D90, -170f), 0f, 90f), "weg", false) };
        foreach (var l in lines)
        {
            PlanetCity.DragBike = l.bike; var plan = PlanetCity.LinePlan(l.a, l.b);
            var pre = PlanetCity.BorePlan(plan, l.t); string ps = ""; foreach (var t in pre) ps += (t.span ? "brug " : "tunnel ") + t.a + "-" + t.b + " ";
            PlanetCity.LastNote = ""; int ok = Drag(l.a, l.b, l.t); int nb = PlanetCity.BoreLine(plan); PlanetCity.DragBike = false;
            int sp = 0; foreach (long k in PlanetCity.TunnelLinks) if (PlanetCity.IsSpan((int)(k >> 20), (int)(k & 0xFFFFF))) sp++;
            log.Append(l.nm).Append(": ").Append(plan.Count).Append(" vakken, gelegd ").Append(ok).Append(", voorbeeld [").Append(ps).Append("], lang ").Append(nb).Append(", bruggen totaal ").Append(sp).Append(", ").AppendLine(PlanetCity.LastNote);
        }
        foreach (string p in PlanetCity.CheckGraph()) log.Append("  GRAAF ").AppendLine(p);
        PlanetCity.Rebuild(); PlanetCity.Rebuild(); yield return null; yield return null; yield return null;
        // de hoogte van het wegdek midden op elke brug (SurfaceH vanaf een landhoofd) tegen het dek
        foreach (long k in PlanetCity.TunnelLinks)
        {
            int a = (int)(k >> 20), b = (int)(k & 0xFFFFF); if (!PlanetCity.IsSpan(a, b)) continue;
            Vector3 mid = (Planet.Cells[a].dir + Planet.Cells[b].dir).normalized * Planet.R; PlanetCity.InBore(mid * 1.02f, out float deck);
            log.Append("  brug ").Append(a).Append('-').Append(b).Append(": G ").Append(PlanetCity.G(a).ToString("0.0")).Append(" / ").Append(PlanetCity.G(b).ToString("0.0"))
               .Append(", midden SurfaceH(a) ").Append(PlanetCity.SurfaceH(a, mid).ToString("0.00")).Append(" SurfaceH(b) ").Append(PlanetCity.SurfaceH(b, mid).ToString("0.00"))
               .Append(", dek ").Append(deck.ToString("0.00")).Append(", maaiveld ").Append(PlanetCity.TerrainAt(mid.normalized).ToString("0.00")).AppendLine();
            log.Append("    midden: ").AppendLine(PlanetCity.GroundInfo(mid.normalized));
            foreach (int st in Planet.Starts) foreach (int e in new[] { a, b }) if (Vector3.Distance(Planet.Cells[st].dir, Planet.Cells[e].dir) * Planet.R < 60f) log.Append("    startplek ").Append(st).Append(" bij ").Append(e).Append(" (Dev.Starts ").Append(Dev.Starts).AppendLine(")");
            foreach (int e in new[] { a, b }) { log.Append("    landhoofd ").Append(e).Append(": ").AppendLine(Desc(e)); foreach (int j in Planet.Cells[e].nb) if (PlanetCity.Cells.ContainsKey(j)) log.Append("      buur ").Append(j).Append(": ").AppendLine(Desc(j)); }
            for (int q = 1; q < 8; q++) { Vector3 pq = Vector3.Slerp(Planet.Cells[a].dir, Planet.Cells[b].dir, q / 8f); int cq = Planet.Nearest(pq * Planet.R); log.Append("    ").Append(q).Append("/8 vak ").Append(cq).Append(" G ").Append(PlanetCity.G(cq).ToString("0.00")).Append(" maaiveld ").Append(PlanetCity.TerrainAt(pq).ToString("0.00")).Append(" ").AppendLine(Desc(cq)); }
        }

        File.WriteAllText(Path.Combine(Dir, "dal.txt"), log.ToString());
        bouw = new System.Text.StringBuilder(); yield return Scan(home, 230f, "dalscan");
        foreach (long k in PlanetCity.TunnelLinks) { int a = (int)(k >> 20), b = (int)(k & 0xFFFFF); if (!PlanetCity.IsSpan(a, b)) continue; yield return Shot("dal_kop" + a, a, (Planet.Cells[b].dir - Planet.Cells[a].dir).normalized, 30f, 22f); }
        yield return Shot("dal_zij", Go(home, D90, 140f), Dh(home, -D90), 60f, 12f);
        yield return Shot("dal_dichtbij", Go(home, 0f, 30f), Dh(home, -D90 + 0.5f), 35f, 10f);
        yield return Shot("dal_boven", home, Dh(home, 0.4f), 170f, 50f);
        yield return Shot("dal_schuin", Go(home, D90, -90f), Dh(home, D90 + 0.5f), 70f, 18f);
        File.WriteAllText(Path.Combine(Dir, "klaar.txt"), "klaar"); if (pb != null) pb.enabled = true;
    }
    // een weg over open water slepen: welke hoogte krijgen de wegvakken en het terrein eromheen?
    IEnumerator ZeeTest(int home)
    {
        Hc = home; var log = new System.Text.StringBuilder(); float D90 = Mathf.PI / 2f;
        foreach (int i in new List<int>(Nearby(home, 220f)))
        {
            Vector3 v = (Planet.Cells[i].dir - Planet.Cells[home].dir) * Planet.R; float y = Vector3.Dot(v, Dh(home, 0f)), x = Vector3.Dot(v, Dh(home, D90));
            PlanetCity.SetTerr(i, Mathf.Abs(y) > 50f ? 2f : x > 0f ? -6f : -2f);   // een zeestraat: diep aan de ene kant, ondiep aan de andere
        }
        foreach (var (nm, x0) in new[] { ("diep", 70f), ("ondiep", -70f) })
        {
            int a = Go(Go(home, D90, x0), 0f, -95f), b = Go(Go(home, D90, x0), 0f, 95f); var plan = PlanetCity.LinePlan(a, b);
            string ps = ""; foreach (var t in PlanetCity.BorePlan(plan, "weg")) ps += (t.span ? "brug " : "tunnel ") + t.a + "-" + t.b + " ";
            PlanetCity.LastNote = ""; int ok = Drag(a, b, "weg"); PlanetCity.BoreLine(plan);
            log.Append(nm).Append(": voorbeeld [").Append(ps).Append("] gelegd ").Append(ok).Append(", ").Append(PlanetCity.LastNote).Append(", hoogtes: ").AppendLine(string.Join(" ", plan.ConvertAll(c => PlanetCity.IsWalk(c) ? PlanetCity.G(c).ToString("0.0") : "(" + PlanetCity.G(c).ToString("0.0") + ")")));
        }
        { int a = Go(Go(home, 0f, 0f), D90, -40f), b = Go(Go(home, 0f, 0f), D90, 40f); Drag(a, b, "weg"); var plan = PlanetCity.LinePlan(a, b); log.Append("open water: ").AppendLine(string.Join(" ", plan.ConvertAll(c => PlanetCity.G(c).ToString("0.0")))); }
        PlanetCity.Rebuild(); PlanetCity.Rebuild(); yield return null; yield return null;
        File.WriteAllText(Path.Combine(Dir, "zee.txt"), log.ToString());
        yield return Shot("zee", home, Dh(home, 0.5f), 150f, 45f);
        yield return Shot("zee_dam", Go(home, D90, -70f), Dh(home, D90), 45f, 20f);
        File.WriteAllText(Path.Combine(Dir, "klaar.txt"), "klaar"); if (pb != null) pb.enabled = true;
    }
    static string Desc(int c)
    {
        string own = PlanetCity.Cells.TryGetValue(c, out var l) ? string.Join("+", l) : "leeg"; var nb = new System.Text.StringBuilder();
        foreach (int j in Planet.Cells[c].nb) nb.Append(PlanetCity.Cells.TryGetValue(j, out var lj) ? lj[0] : "-").Append(' ');
        return own + " (G " + PlanetCity.G(c).ToString("0.0") + ", partners " + PlanetCity.Partners(c).Count + ", buren " + nb + ")";
    }
    IEnumerator Hoeken(int home)
    {
        float D30 = Mathf.PI / 6f, D60 = Mathf.PI / 3f; Hc = home; bouw = new System.Text.StringBuilder();
        int h1 = home, h2 = Go(home, 0f, 170f), h3 = Go(home, Mathf.PI, 170f), h4 = Go(home, Mathf.PI / 2f, 170f), h5 = Go(home, -Mathf.PI / 2f, 170f);
        Weg("h1 recht", Go(h1, 0f, -60f), Go(h1, 0f, 60f), 0); Weg("h1 schuin", h1, Go(h1, D30, 70f), 0);
        Weg("h2 recht", Go(h2, 0f, -60f), Go(h2, 0f, 60f), 0); Weg("h2 laan schuin", h2, Go(h2, D30 + Mathf.PI, 70f), 2);
        Weg("h3 schuin door", Go(h3, D30, -60f), Go(h3, D30, 60f), 0); Weg("h3 recht", h3, Go(h3, 0f, 60f), 0);
        Weg("h4 recht", Go(h4, 0f, -60f), Go(h4, 0f, 60f), 0); Weg("h4 zij", h4, Go(h4, D60 * 2f, 60f), 0); PadH("h4 pad schuin naar de kruising", Go(h4, -D30 - D60, 55f), h4);
        Weg("h5 recht", Go(h5, 0f, -60f), Go(h5, 0f, 60f), 0); PadH("h5 pad schuin op de weg", Go(h5, -D30 - D60, 55f), h5); PlanetCity.DragBike = true; PadH("h5 fietspad schuin", Go(Go(h5, 0f, 40f), D30 + D60, 55f), Go(h5, 0f, 40f)); PlanetCity.DragBike = false;
        int h6 = Go(home, Mathf.PI * 0.75f, 330f), h7 = Go(home, -Mathf.PI * 0.75f, 330f);
        Weg("h6 recht", Go(h6, 0f, -60f), Go(h6, 0f, 60f), 0); Weg("h6 zij 60", h6, Go(h6, D60, 60f), 0); PadH("h6 pad tussen de armen (30)", Go(h6, D30, 55f), h6); PadH("h6 pad tussen de armen (120)", Go(h6, D60 * 2f, 55f), h6);
        Weg("h7 recht", Go(h7, 0f, -60f), Go(h7, 0f, 60f), 0); Weg("h7 zij 120", h7, Go(h7, D60 * 2f, 60f), 0); PlanetCity.DragBike = true; PadH("h7 fietspad tussen de armen (60)", Go(h7, D60, 55f), h7); PlanetCity.DragBike = false; PadH("h7 pad schuin (-30)", Go(h7, -D30, 55f), h7);
        // op een helling (6 m op 100 m) met huizen eromheen: een T, een spitse splitsing en een bocht
        int h8 = Go(home, Mathf.PI * 0.25f, 480f), h9 = Go(h8, 0f, 150f), h10 = Go(h8, Mathf.PI, 150f);
        foreach (int i in new List<int>(Nearby(h8, 260f))) { float x = Vector3.Dot((Planet.Cells[i].dir - Planet.Cells[h8].dir) * Planet.R, Dh(h8, Mathf.PI / 2f)); PlanetCity.SetTerr(i, Mathf.Clamp(6f + x * 0.06f, 0f, 20f)); }
        Weg("h8 recht a", h8, Go(h8, 0f, 60f), 0); Weg("h8 recht b", h8, Go(h8, Mathf.PI, 60f), 0); Weg("h8 zij", h8, Go(h8, Mathf.PI / 2f, 60f), 0);
        Weg("h9 recht a", h9, Go(h9, 0f, 60f), 0); Weg("h9 recht b", h9, Go(h9, Mathf.PI, 60f), 0); Weg("h9 schuin", h9, Go(h9, D30, 70f), 0);
        Weg("h10 a", Go(h10, Mathf.PI, 60f), h10, 0); Weg("h10 b", h10, Go(h10, D60 * 2f, 60f), 0);   // een bocht van 60°
        foreach (int c in new[] { h8, h9, h10 }) foreach (int i in new List<int>(Nearby(c, 30f))) if (!PlanetCity.Cells.ContainsKey(i)) PlanetCity.Place(i, "woning");
        PlanetCity.Rebuild(); PlanetCity.Rebuild(); yield return null; yield return null;
        foreach (bool rf in new[] { false, true, false, true }) { PlanetCity.RoadField = rf; var sw = System.Diagnostics.Stopwatch.StartNew(); PlanetCity.TouchAll(); PlanetCity.Rebuild(); bouw.Append("alles opnieuw tekenen (wegen in de hoogtefunctie ").Append(rf ? "aan" : "uit").Append("): ").Append(sw.ElapsedMilliseconds).AppendLine(" ms"); }
        File.WriteAllText(Path.Combine(Dir, "bouw.txt"), bouw.ToString());
        foreach (var (nm, c) in new[] { ("hoek_8", h8), ("hoek_9", h9), ("hoek_10", h10), ("hoek_6", h6), ("hoek_7", h7), ("hoek_1", h1), ("hoek_2", h2), ("hoek_3", h3), ("hoek_4", h4), ("hoek_5", h5), ("hoek_5b", Go(h5, 0f, 40f)) }) { yield return Shot(nm + "_boven", c, Dh(c, 0f), 40f, 89f); yield return Shot(nm + "_schuin", c, Dh(c, 0.6f), 30f, 40f); }
        File.WriteAllText(Path.Combine(Dir, "klaar.txt"), "klaar"); if (pb != null) pb.enabled = true;
    }
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
    IEnumerator StartTijd()
    {
        PlanetCity.Ground2 = true; PlanetCity.Slot = 9; PlanetCity.ClearAll(); var log = new System.Text.StringBuilder(); var sw = System.Diagnostics.Stopwatch.StartNew();
        PlanetCity.StartTown(Planet.North); long tPl = sw.ElapsedMilliseconds; long tPr = PlanetCity.RoadH.Count;   // (StartTown maakt zelf het profiel)
        PlanetCity.G2Reset(); PlanetCity.G2CellMs = new Dictionary<int, long>(); sw.Restart(); PlanetCity.TouchAll(); PlanetCity.Rebuild(); long tRb = sw.ElapsedMilliseconds;
        log.Append("plaatsen met profiel ").Append(tPl).Append(" ms, RoadH ").Append(tPr).Append(" vakken, herbouwen ").Append(tRb).AppendLine(" ms").AppendLine(PlanetCity.G2Stats()).AppendLine(PlanetCity.G2Steps()).AppendLine(PlanetCity.UpdateTimes);
        var l = new List<KeyValuePair<int, long>>(PlanetCity.G2CellMs); l.Sort((a, b) => b.Value.CompareTo(a.Value)); double tk = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        for (int k = 0; k < Mathf.Min(15, l.Count); k++) { int c = l[k].Key; PlanetCity.UseNearPub(c); log.Append("  vak ").Append(c).Append(": ").Append((l[k].Value * tk).ToString("0")).Append(" ms, stukken ").Append(PlanetCity.SegsNear).Append(", koppen ").Append(PlanetCity.CapsNear).Append("; ").AppendLine(Desc(c)); }
        PlanetCity.G2CellMs = null;
        log.Append("na de eerste bouw: ").AppendLine(PlanetCity.Ground2EdgeCheck());
        // het geheugen per vak (Ground2Cache): nog eens alles zonder wijziging, dan een krater naast het traagste vak, dan hetzelfde zonder geheugen
        {
            PlanetCity.G2Reset(); PlanetCity.Ground2Tris = 0; sw.Restart(); PlanetCity.TouchAll(); PlanetCity.Rebuild();
            log.Append("nog eens, niets veranderd: ").Append(sw.ElapsedMilliseconds).Append(" ms; ").AppendLine(PlanetCity.G2Stats());
            int c0 = l[0].Key, cj = Planet.Cells[c0].nb[0]; foreach (int j in Planet.Cells[c0].nb) if (!PlanetCity.Cells.ContainsKey(j)) { cj = j; break; }
            PlanetCity.SetTerr(cj, PlanetCity.Terrain(cj) - 3f); PlanetCity.Touch(cj);
            PlanetCity.G2Reset(); PlanetCity.Ground2Tris = 0; sw.Restart(); PlanetCity.Rebuild();
            log.Append("krater in vak ").Append(cj).Append(" (alleen de geraakte delen): ").Append(sw.ElapsedMilliseconds).Append(" ms; ").AppendLine(PlanetCity.G2Stats());
            PlanetCity.G2Reset(); PlanetCity.Ground2Tris = 0; PlanetCity.TouchAll(); PlanetCity.Rebuild(); long trA = PlanetCity.Ground2Tris;
            PlanetCity.G2CacheWrong = new List<string>(); PlanetCity.TouchAll(); PlanetCity.Rebuild(); log.Append("uit het geheugen maar anders: ").Append(PlanetCity.G2CacheWrong.Count).Append(": ").AppendLine(string.Join(", ", PlanetCity.G2CacheWrong.GetRange(0, Mathf.Min(20, PlanetCity.G2CacheWrong.Count)))); PlanetCity.G2CacheWrong = null; log.AppendLine(PlanetCity.Ground2EdgeCheck());
            PlanetCity.Ground2Cache = false; PlanetCity.Ground2CacheClear(); PlanetCity.G2Reset(); PlanetCity.Ground2Tris = 0; sw.Restart(); PlanetCity.TouchAll(); PlanetCity.Rebuild(); long trB = PlanetCity.Ground2Tris;
            PlanetCity.Ground2Cache = true;
            log.Append("alles met geheugen ").Append(trA).Append(" driehoeken, zonder geheugen ").Append(trB).Append(" (").Append(sw.ElapsedMilliseconds).Append(" ms): ").AppendLine(trA == trB ? "gelijk" : "ONGELIJK");
        }
        File.WriteAllText(Path.Combine(Dir, "starttijd.txt"), log.ToString()); File.WriteAllText(Path.Combine(Dir, "klaar.txt"), "klaar"); yield break;
    }
    IEnumerator Startstad()
    {
        int st = PlanetCity.FreeStart(Planet.Cells[Planet.North].dir); Hc = st; PlanetCity.StartTown(st);
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
        int H = At(Planet.North, 1.0f, 160f); Hc = H; Vector3 up = Planet.Cells[H].dir, fw = Dh(H, 0f), rt = Vector3.Cross(up, fw);
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

    // Een melding van de speler naspelen (vlag "melding [nummer] [uit]"): zijn stad (stad.json bij de melding) en precies zijn camera,
    // met of zonder Terrein door wegen; een foto, een scan rond het vak waar hij naar keek, en dan blijft Play daar staan.
    IEnumerator Melding(string mode)
    {
        var parts = mode.Split(' '); string root = Path.Combine(Application.persistentDataPath, "meldingen");
        var dirs = new List<DirectoryInfo>(new DirectoryInfo(root).GetDirectories()); dirs.RemoveAll(d => !File.Exists(Path.Combine(d.FullName, "melding.json")));
        dirs.Sort((a, b) => b.LastWriteTime.CompareTo(a.LastWriteTime)); var dir = dirs[0];
        foreach (string p in parts) if (int.TryParse(p, out int nr)) { var f = dirs.Find(d => d.Name.StartsWith(nr + "-")); if (f != null) dir = f; }
        bool uit = System.Array.IndexOf(parts, "uit") >= 0; PlanetCity.RoadShape = !uit; PlanetCity.RoadSlopes = System.Array.IndexOf(parts, "geentalud") < 0;
        string js = File.ReadAllText(Path.Combine(dir.FullName, "melding.json"));
        float Num(string key, int from) { int i = js.IndexOf("\"" + key + "\"", from); i = js.IndexOf(':', i) + 1; int j = i; while (j < js.Length && "-0123456789.eE ".IndexOf(js[j]) >= 0) j++; return float.Parse(js.Substring(i, j - i).Trim(), System.Globalization.CultureInfo.InvariantCulture); }
        int cp = js.IndexOf("\"positie\""), cr = js.IndexOf("\"rotatie\""), cv = js.IndexOf("\"kijktNaarVak\"");
        Vector3 pos = new Vector3(Num("x", cp), Num("y", cp), Num("z", cp)); var rot = new Quaternion(Num("x", cr), Num("y", cr), Num("z", cr), Num("w", cr)); int vak = (int)Num("kijktNaarVak", cv); if (System.Array.IndexOf(parts, "zonderwegen") >= 0) Dev.HideRoads = true;   /* "zonderwegen": de ontwikkelaarsschakelaar Wegen verbergen */ if (System.Array.IndexOf(parts, "zoom") >= 0) { int cq = js.IndexOf("\"kijktNaarPunt\""); Vector3 lp = new Vector3(Num("x", cq), Num("y", cq), Num("z", cq)); pos = Vector3.Lerp(pos, lp, 0.62f); }   // "zoom": dichter naar het punt waar de camera naar keek
        PlanetCity.ClearAll(); PlanetCity.Restore(File.ReadAllText(Path.Combine(dir.FullName, "stad.json"))); foreach (string p in parts) if (p.StartsWith("trap=")) { var ab = p.Substring(5).Split('-'); PlanetCity.SetStair(int.Parse(ab[0]), int.Parse(ab[1])); }   /* "trap=a-b": die verbinding als trap */ foreach (string p in parts) if (p.StartsWith("sleeptrap=")) { var ab = p.Substring(10).Split('-'); int a = int.Parse(ab[0]), b = int.Parse(ab[1]); PlanetCity.SetStair(a, b); PlanetCity.ProfileAll(new List<List<int>>()); PlanetCity.Stairs.Clear(); int made = PlanetCity.MarkStairs(new List<int> { a, b }); File.WriteAllText(Path.Combine(Dir, "melding_trap.txt"), "MarkStairs: " + made + " trap(pen); hoogtes " + PlanetCity.RoadH[a].ToString("0.00") + " en " + PlanetCity.RoadH[b].ToString("0.00")); }   /* "sleeptrap=a-b": zoals het slepen het doet (MarkStairs met de hoogtes van voor de lijn) */ if (PlanetCity.ProfileFirst) { PlanetCity.HeightTestNote = PlanetCity.ProfileAll(new List<List<int>>()); PlanetCity.UpdateCuts(); }   // "melding N profiel": de stad met de nieuwe opbouw (het profiel over alle wegen, de kunstwerken blijven)
        PlanetCity.G2Reset(); PlanetCity.TouchAll(); PlanetCity.Rebuild(); PlanetCity.RebuildGrid(); yield return null; yield return null;
        cam.transform.position = pos; cam.transform.rotation = rot; cam.fieldOfView = 60f; yield return null; yield return null;
        { long nt = 0; foreach (var mf in FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)) if (mf.gameObject.layer == Layers.Bodem && mf.sharedMesh != null) nt += mf.sharedMesh.triangles.Length / 3; File.WriteAllText(Path.Combine(Dir, "melding_driehoeken.txt"), "stukken " + PlanetCity.G2Pieces + " met " + PlanetCity.G2PieceV + " hoekpunten, onderverdeeld " + PlanetCity.G2Refines + " keer; bij wegen: op een rechte lijn " + PlanetCity.G2ColEdge + " op de vakrand en " + PlanetCity.G2ColIn + " binnenin, echte hoeken " + PlanetCity.G2CornEdge + " en " + PlanetCity.G2CornIn + "; bodem: " + nt + " driehoeken, Terrein v2 " + (PlanetCity.Ground2 ? "aan" : "uit") + (PlanetCity.RoadH.Count > 0 ? ", profiel" : "")); }   // de hele bodem
        ScreenCapture.CaptureScreenshot(Path.Combine(Dir, "melding_" + dir.Name + (uit ? "_uit" : "") + ".png")); yield return null; yield return null;
        Hc = vak; bouw = new System.Text.StringBuilder(dir.Name + (uit ? " (terrein door wegen uit)" : "") + "\n"); yield return Scan(vak, 45f, "melding");
        var dbg = new System.Text.StringBuilder();
        foreach (int i in Nearby(vak, 60f)) if (PlanetCity.IsHalf(i))
        {
            dbg.Append(i).Append(": ").AppendLine(PlanetCity.GroundInfo(Planet.Cells[i].dir));
            dbg.Append("   hoeken eigen: "); for (int k = 0; k < Planet.Cells[i].corners.Length; k++) dbg.Append(PlanetCity.OwnCorner(i, k).ToString("0.00")).Append(' ');
            dbg.Append(" | CornerH: "); for (int k = 0; k < Planet.Cells[i].corners.Length; k++) dbg.Append(PlanetCity.CornerH(i, k).ToString("0.00")).Append(' ');
            dbg.AppendLine(); foreach (int j in Planet.Cells[i].nb) dbg.Append("   buur ").Append(j).Append(": ").AppendLine(Desc(j));
        }
        File.WriteAllText(Path.Combine(Dir, "melding_halve.txt"), dbg.ToString());
        if (vak == 159) { yield return Shot("melding_half158", 158, (Planet.Cells[202].dir - Planet.Cells[158].dir).normalized * -1f, 22f, 12f); yield return Shot("melding_half158b", 158, (Planet.Cells[201].dir - Planet.Cells[158].dir).normalized * -1f, 22f, 8f); }
        File.WriteAllText(Path.Combine(Dir, "klaar.txt"), "klaar");
        Breng(vak, "Hier was je melding");
    }
    // de hoogtetest (Nieuwe stad op het startscherm) met de wegen van startpunt naar startpunt: hoeveel wegen, tunnels en bruggen, de graaf,
    // een scan langs alle wegen en foto's van een paar landschappen
    // De testvolgorde van docs/terrein-wegen-oplossing.md (deel 11), één test per keer: "ontwerp 1" ... "ontwerp 12". Altijd met
    // Terrein v2 en een wegprofiel (RoadH); elke test één handeling, dan de scan, de randcontrole, de hellingen en foto's.
    // Test 12 (ontwerp deel 11): boetseren naast en onder een weg zoals het penseel (SetTerr op het terrein, dan Changed: Reprofile en
    // herbouwen). Een heuvel naast de weg in stappen omhoog, dan een kuil; dan een bult onder de weg en weer weg. Na elke stap de scan, de
    // weghoogte bij de heuvel en een foto. De weg mag meebewegen (hooguit 10%), maar nooit grond op de weg, gaten of een sprong in de mesh.
    IEnumerator Boetsel12(int home, List<int> centers)
    {
        float D90 = Mathf.PI / 2f; int hill = Go(home, D90, 22f), bump = Go(home, 0f, 30f), near = Go(home, 0f, 0f);
        void Blob(int c, float r, float dh)   // een ronde bult (cos²) rond vak c, dh hoger dan het vlakke land op 20 m
        {
            foreach (int i in new List<int>(Nearby(c, r)))
            {
                float d = Vector3.Angle(Planet.Cells[i].dir, Planet.Cells[c].dir) * Mathf.Deg2Rad * Planet.R; if (d > r) continue;
                float w = Mathf.Cos(d / r * Mathf.PI * 0.5f); PlanetCity.SetTerr(i, 20f + dh * w * w);
            }
        }
        var steps = new (string nm, int c, float r, float dh)[] { ("heuvel4", hill, 20f, 4f), ("heuvel8", hill, 20f, 8f), ("heuvel12", hill, 20f, 12f), ("kuil8", hill, 20f, -8f), ("vlak", hill, 20f, 0f), ("bult6", bump, 14f, 6f), ("bultweg", bump, 14f, 0f) };
        for (int k = 0; k < steps.Length; k++)
        {
            var st = steps[k]; Blob(st.c, st.r, st.dh); PlanetCity.Reprofile(); PlanetCity.Rebuild(); PlanetCity.RebuildGrid(); yield return null; yield return null;
            bouw.AppendLine().Append("== stap ").Append(k + 1).Append(": ").Append(st.nm).Append(" (weg bij de heuvel ").Append(PlanetCity.G(near).ToString("0.00")).Append(", terrein ").Append(PlanetCity.Terrain(near).ToString("0.00"))
                .Append("; weg bij de bult ").Append(PlanetCity.G(bump).ToString("0.00")).Append(", terrein ").Append(PlanetCity.Terrain(bump).ToString("0.00")).AppendLine(")");
            yield return Scan(home, 0f, "ontwerp12_" + st.nm, centers, 22f);
            int fc = st.c == hill ? near : bump; yield return Shot("ontwerp12_" + st.nm, fc, Quaternion.AngleAxis(-35f, Planet.Cells[fc].dir) * Dh(fc, D90), 55f, 25f);
        }
    }
    IEnumerator Ontwerp(int home, string mode)
    {
        int nr = 1; int.TryParse(mode.Substring(7).Trim(), out nr); PlanetCity.Ground2 = true; Hc = home; bouw = new System.Text.StringBuilder();
        float D90 = Mathf.PI / 2f; var lines = new List<List<int>>(); var centers = new List<int>();
        void Terrein(System.Func<float, float, float> f, float r)   // hoogte als functie van x (langs de weg) en y (dwars), afgerond op 5 cm
        {
            foreach (int i in new List<int>(Nearby(home, r)))
            {
                Vector3 v = (Planet.Cells[i].dir - Planet.Cells[home].dir) * Planet.R; float x = Vector3.Dot(v, Dh(home, 0f)), y = Vector3.Dot(v, Dh(home, D90));
                PlanetCity.SetTerr(i, Mathf.Round(f(x, y) * 20f) / 20f);
            }
        }
        void Lijn(string nm, int a, int b, int lvl = 0)   // lvl: niveau in halve stappen (2 = 4,5 m: over een andere weg heen)
        {
            var l = PlanetCity.LinePlan(a, b); if (l == null || l.Count < 2) { bouw.Append(nm).AppendLine(": GEEN LIJN"); return; }
            int ok = PlanetCity.PlaceLine(l, "weg"); PlanetCity.SetLevel(l, lvl, "weg"); lines.Add(new List<int>(l)); centers.AddRange(l);
            bouw.Append(nm).Append(": ").Append(ok).Append('/').Append(l.Count).Append(" vakken").AppendLine();
        }
        switch (nr)
        {
            case 1: Terrein((x, y) => 20f + 0.25f * x, 120f); Lijn("weg op een helling", Go(home, 0f, -30f), Go(home, 0f, 30f)); break;   // 1. één weg op een helling van 0,35 m/m
            case 2: Terrein((x, y) => 8f * Mathf.Exp(-x * x / (20f * 20f)), 120f); Lijn("insnijding", Go(home, 0f, -75f), Go(home, 0f, 75f)); break;   // 2. over een heuvel van 8 m (flank tot 34%: de weg op 10% snijdt er tot zo'n 4 m in)
            case 3: Terrein((x, y) => 8f - 8f * Mathf.Exp(-x * x / (20f * 20f)), 120f); Lijn("dijk", Go(home, 0f, -75f), Go(home, 0f, 75f)); break;   // 3. over een laagte van 8 m (de weg op 10% ligt er tot zo'n 4 m boven: een dijk)
            case 4: Terrein((x, y) => 20f + (Mathf.Abs(x + 45f) < 14f ? 14f : 0f) - (Mathf.Abs(x - 45f) < 14f ? 14f : 0f), 140f); Lijn("keermuur", Go(home, 0f, -105f), Go(home, 0f, 105f)); break;   // 4. op 20 m een blok van 14 m hoog en een kuil van 14 m diep, elk 28 m breed (hele vakken; de hellingbegrenzer verdeelt het verschil over de vakken ervoor en erna, zo blijft er zo'n 7 m over: bak en sokkel): de weg op 10% kan ze niet volgen, dus een bak en een sokkel van zo'n 7 m (6 tot 9 m: wanden)
            case 5: Terrein((x, y) => 20f - 20f * Mathf.Exp(-x * x / (40f * 40f)), 160f); Lijn("dalbrug", Go(home, 0f, -120f), Go(home, 0f, 120f)); break;   // 5. laagte van 20 m
            case 6: Terrein((x, y) => 30f * Mathf.Exp(-x * x / (40f * 40f)), 160f); Lijn("tunnel", Go(home, 0f, -120f), Go(home, 0f, 120f)); break;   // 6. berg van 30 m
            case 7: Terrein((x, y) => 20f + 0.2f * x + 0.1f * y, 140f); Lijn("arm 1", Go(home, 0f, -60f), Go(home, 0f, 60f)); Lijn("arm 2", Go(home, D90, -60f), Go(home, D90, 60f)); break;   // 7. kruispunt op een helling
            case 8: Terrein((x, y) => 20f, 140f); Lijn("onder", Go(home, 0f, -60f), Go(home, 0f, 60f)); Lijn("over", Go(home, D90, -60f), Go(home, D90, 60f), 2); break;   // 8. onderdoorgang: een weg op +4,5 m over een weg op het maaiveld
            case 9: Terrein((x, y) => 20f - 20f * Mathf.Exp(-(x + 30f) * (x + 30f) / (25f * 25f)) + 30f * Mathf.Exp(-(x - 50f) * (x - 50f) / (30f * 30f)), 170f); Lijn("brug naar portaal", Go(home, 0f, -120f), Go(home, 0f, 120f)); break;   // 9. een dal van 20 m vlak voor een berg van 30 m (melding 8)
            case 13: Terrein((x, y) => x < 0f ? 20f : 0f, 240f); { var pl = PlanetCity.LinePlan(Go(home, 0f, -75f), Go(home, 0f, 215f)); string ps = ""; foreach (var t in PlanetCity.BorePlan(pl, "weg")) ps += (t.span ? "brug " : "tunnel ") + t.a + "-" + t.b + " (" + t.ha.ToString("0.0") + " en " + t.hb.ToString("0.0") + ") "; bouw.AppendLine("voorbeeld vooraf: " + ps); } Lijn("van hoog naar laag", Go(home, 0f, -75f), Go(home, 0f, 215f)); break;   // 13. van een hoogvlakte op 20 m naar laag terrein: een afdalende brug
            case 15: Terrein((x, y) => Mathf.Abs(x) < 40f ? -4f : 2f, 240f); { var pl = PlanetCity.LinePlan(Go(home, 0f, -100f), Go(home, 0f, 100f)); string ps = ""; foreach (var t in PlanetCity.BorePlan(pl, "weg")) ps += (t.span ? "brug " : "tunnel ") + t.a + "-" + t.b + " (" + t.ha.ToString("0.0") + " en " + t.hb.ToString("0.0") + ") "; bouw.AppendLine("voorbeeld vooraf: " + ps); } Lijn("over het water", Go(home, 0f, -100f), Go(home, 0f, 100f)); break;   // 15. over een water van 80 m breed: een brug
            case 14: Terrein((x, y) => x < 0f ? 7f : 0f, 240f); Lijn("lage rand", Go(home, 0f, -75f), Go(home, 0f, 120f)); break;   // 14. een rand van 7 m: ook een brug
            case 12: Terrein((x, y) => 20f, 140f); Lijn("boetseren", Go(home, 0f, -60f), Go(home, 0f, 60f)); break;   // 12. boetseren naast en onder een weg (Boetsel12)
            case 10: Terrein((x, y) => 5f + 30f * Mathf.Exp(-(x * x + y * y) / (50f * 50f)), 170f); Lijn("arm 1", Go(home, 0f, -120f), Go(home, 0f, 120f)); Lijn("arm 2", Go(home, Mathf.PI / 3f, -120f), Go(home, Mathf.PI / 3f, 120f)); break;   // 10. kruispunt in de berg (arm 2 onder 60°: een rasteras, want een schuine weg wordt geen tunnel): twee wegen door een berg van 30 m, kruising in het hart
            default: bouw.AppendLine("onbekende test " + nr); break;
        }
        PlanetCity.HeightTestNote = PlanetCity.ProfileAll(lines); PlanetCity.UpdateCuts(); PlanetCity.TouchAll();
        PlanetCity.Rebuild(); PlanetCity.Rebuild(); PlanetCity.RebuildGrid(); yield return null; yield return null;
        bouw.AppendLine(PlanetCity.HeightTestNote); bouw.Append("stukken zonder geldig oor: ").Append(PlanetCity.G2NoEar).AppendLine(); if (PlanetCity.Ground3) { var wy = new Dictionary<string, int>(); foreach (int cc in new HashSet<int>(centers)) foreach (int j in new List<int>(Planet.Cells[cc].nb) { cc }) { string y = PlanetCity.V3Why(j); if (y.StartsWith("stuk")) y = "stuk met wand, bak of hard eind"; wy[y] = (wy.TryGetValue(y, out int n0) ? n0 : 0) + 1; } bouw.Append("stroken (vakken op en naast de weg): "); foreach (var kv in wy) bouw.Append(kv.Key).Append(" ").Append(kv.Value).Append("; "); bouw.AppendLine(); }
        if (centers.Count > 0) { Hc = centers[centers.Count / 2]; yield return Scan(Hc, 0f, "ontwerp" + nr + "_scan", centers, 22f); }
        if (nr == 12) yield return Boetsel12(home, centers);
        File.WriteAllText(Path.Combine(Dir, "ontwerp" + nr + ".txt"), bouw.ToString());
        if (centers.Count > 0)
        {
            int mid = centers[centers.Count / 2];
            yield return Shot("ontwerp" + nr + "_zij", mid, Dh(mid, D90), 60f, 18f);
            yield return Shot("ontwerp" + nr + "_schuin", mid, Quaternion.AngleAxis(35f, Planet.Cells[mid].dir) * Dh(mid, 0f), 70f, 30f);
            yield return Shot("ontwerp" + nr + "_laag", centers[Mathf.Min(1, centers.Count - 1)], Dh(mid, 0f), 25f, 8f);
            {   // de stukken van het wegvak op een kwart van de lijn en van zijn buren, voor een tekening
                int q4 = centers[centers.Count / 4]; var cellsD = new List<int> { q4 }; foreach (int j in Planet.Cells[q4].nb) cellsD.Add(j); if (PlanetCity.G3Half) { cellsD.Clear(); foreach (int cc in new HashSet<int>(centers)) foreach (int j in Planet.Cells[cc].nb) if (PlanetCity.IsHalf(j) && !cellsD.Contains(j)) cellsD.Add(j); }   // (proef: alle halve wegvakken)
                var dsb = new System.Text.StringBuilder();
                foreach (int cd in cellsD)
                {
                    var cc = Planet.Cells[cd]; Vector3 rel = cc.dir * Planet.R - Planet.Cells[q4].dir * Planet.R; var c0 = Planet.Cells[q4];
                    { var cl = Planet.Cells[cd]; dsb.Append("hoeken"); foreach (var lv in cl.local) dsb.Append(' ').Append(lv.x.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)).Append(',').Append(lv.y.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)); dsb.AppendLine(); } dsb.Append("vak ").Append(cd).Append(' ').Append(Vector3.Dot(rel, c0.east).ToString("F3", System.Globalization.CultureInfo.InvariantCulture)).Append(' ').Append(Vector3.Dot(rel, c0.north).ToString("F3", System.Globalization.CultureInfo.InvariantCulture)).Append(' ').Append(Vector3.Dot(cc.east, c0.east).ToString("F4", System.Globalization.CultureInfo.InvariantCulture)).Append(' ').Append(Vector3.Dot(cc.east, c0.north).ToString("F4", System.Globalization.CultureInfo.InvariantCulture)).Append(' ').Append(PlanetCity.IsWalk(cd) ? "weg" : "-").AppendLine();
                    PlanetCity.G2DumpCell = cd; PlanetCity.G2DumpSb = dsb; PlanetCity.Ground2CacheClear(); PlanetCity.Touch(cd); PlanetCity.Rebuild();
                }
                PlanetCity.G2DumpCell = -1; PlanetCity.G2DumpSb = null; File.WriteAllText(Path.Combine(Dir, "ontwerp" + nr + "_stukken.txt"), dsb.ToString());
            }
            { int q4 = centers[centers.Count / 4]; Dev.HideRoads = true; yield return Shot("ontwerp" + nr + "_boven_zonderweg", q4, Dh(q4, 0f), 34f, 80f); Dev.HideRoads = false; yield return Shot("ontwerp" + nr + "_boven_metweg", q4, Dh(q4, 0f), 34f, 80f); }   // recht van boven op een kwart van de lijn, zonder en met de weg: de driehoeken van de grond
            if (nr == 4) { int bk = Go(home, 0f, -45f), br = Go(home, 0f, 45f); yield return Shot("ontwerp4_bak", bk, Quaternion.AngleAxis(40f, Planet.Cells[bk].dir) * Dh(bk, 0f), 45f, 22f); yield return Shot("ontwerp4_brug", br, Quaternion.AngleAxis(-50f, Planet.Cells[br].dir) * Dh(br, 0f), 70f, 12f); }   // de bak door het blok en de brug over de kuil
            int e0 = centers[0], e1 = centers[centers.Count - 1];   // de twee einden van dichtbij, van opzij, en hun driehoekjes
            {   // wat ligt er voorbij elk eind: stralen langs de as van de weg (afstand, wat geraakt, hoogte, en H)
                var pr = new System.Text.StringBuilder();
                foreach (var (e, sg) in new[] { (e0, -1f), (e1, 1f) })
                    for (float dd = 4f; dd <= 20f; dd += 2f)
                    {
                        Vector3 d = (Planet.Cells[e].dir * Planet.R + Dh(e, 0f) * sg * dd).normalized;
                        bool hit = Physics.Raycast(d * (Planet.R + 150f), -d, out RaycastHit hg, 180f, ~0, QueryTriggerInteraction.Ignore);
                        pr.Append("eind ").Append(e).Append(' ').Append(dd).Append(" m: ").Append(hit ? hg.collider.name + " laag " + hg.collider.gameObject.layer + " h " + (hg.point.magnitude - Planet.R).ToString("0.00") : "NIETS").Append(" | H ").Append(PlanetCity.H(d).ToString("0.00")).AppendLine();
                    }
                File.WriteAllText(Path.Combine(Dir, "ontwerp" + nr + "_stralen.txt"), pr.ToString());
            }
            yield return Shot("ontwerp" + nr + "_eind1", e0, Quaternion.AngleAxis(60f, Planet.Cells[e0].dir) * Dh(e0, 0f), 30f, 20f);
            yield return Shot("ontwerp" + nr + "_eind2", e1, Quaternion.AngleAxis(-120f, Planet.Cells[e1].dir) * Dh(e1, 0f), 30f, 20f); yield return Shot("ontwerp" + nr + "_eind2boven", e1, Dh(e1, 0f), 22f, 65f); yield return Shot("ontwerp" + nr + "_eind1boven", e0, Dh(e0, 0f), 22f, 65f);
            File.WriteAllText(Path.Combine(Dir, "ontwerp" + nr + "_einden.txt"), PlanetCity.Ground2Dump(e0) + "\n" + PlanetCity.Ground2Dump(e1));
        }
        File.WriteAllText(Path.Combine(Dir, "klaar.txt"), "klaar"); if (pb != null) pb.enabled = true; Breng(Hc, "Ontwerptest " + nr);   // de camera naar de test (bij de noordpool), zodat hij meteen te zien is
    }
    // Terrein v2 in het spel: de natuurplaneet, dan de 30 wegen een voor een zoals een speler ze sleept (PlayerRoads: profiel per sleep,
    // met de tijd), dan opslaan en laden (Snapshot, Restore: het profiel komt terug), de schakelaar uit (het profiel weg) en weer aan,
    // en dan de scan en foto's zoals de natuurtest.
    IEnumerator Inspel()
    {
        var log = new System.Text.StringBuilder(); PlanetCity.Ground2 = true; PlanetCity.ProfileFirst = false;
        yield return PlanetCity.NatuurTestSteps(null, 11, false);
        var ms = new List<float>(); float t0 = Time.realtimeSinceStartup; yield return PlanetCity.PlayerRoads(null, ms);
        float mx = 0f, sum = 0f; foreach (float m in ms) { mx = Mathf.Max(mx, m); sum += m; }
        log.Append("wegen ").Append(PlanetCity.HeightTestRoadCount).Append(" aangekomen, ").Append(ms.Count).Append(" slepen; profiel per sleep gemiddeld ").Append(ms.Count > 0 ? (sum / ms.Count).ToString("0") : "-").Append(" ms, hooguit ").Append(mx.ToString("0")).Append(" ms; RoadH ").Append(PlanetCity.RoadH.Count).AppendLine();
        int nt = 0, nb = 0; foreach (long k in PlanetCity.TunnelLinks) { int a = (int)(k >> 20), b = (int)(k & 0xFFFFF); if (PlanetCity.IsSpan(a, b)) nb++; else if (PlanetCity.IsBore(a, b)) nt++; }
        log.Append(nt).Append(" tunnels, ").Append(nb).AppendLine(" dalbruggen");
        // opslaan en laden
        var keep = new Dictionary<int, float>(PlanetCity.RoadH); string snap = PlanetCity.Snapshot();
        PlanetCity.ClearAll(); var sw = System.Diagnostics.Stopwatch.StartNew(); bool ok = PlanetCity.Restore(snap); long tl = sw.ElapsedMilliseconds;
        int diff = 0; var dd = new System.Text.StringBuilder();
        foreach (var kv in keep) if (!PlanetCity.RoadH.TryGetValue(kv.Key, out var v) || Mathf.Abs(v - kv.Value) > 0.01f)
        {
            diff++; int c = kv.Key; dd.Append("  vak ").Append(c).Append(": voor ").Append(kv.Value.ToString("0.00")).Append(", na ").Append(PlanetCity.RoadH.TryGetValue(c, out var v2) ? v2.ToString("0.00") : "-").Append(", terrein ").Append(PlanetCity.Terrain(c).ToString("0.00")).Append("; ").Append(Desc(c)).Append("; partners");
            foreach (int j in PlanetCity.Partners(c)) dd.Append(' ').Append(j).Append(PlanetCity.IsBore(c, j) ? (PlanetCity.IsSpan(c, j) ? "(dalbrug)" : "(tunnel)") : "").Append(PlanetCity.Underpass(j) ? "(onderdoorgang)" : "");
            dd.AppendLine();
        }
        log.Append("laden ").Append(ok ? "gelukt" : "MISLUKT").Append(" in ").Append(tl).Append(" ms; RoadH ").Append(PlanetCity.RoadH.Count).Append(", ").Append(diff).AppendLine(" vakken met een andere weghoogte dan voor het opslaan"); log.Append(dd);
        // de schakelaar
        { PlanetCity.Ground2Tris = 0; var sw1 = System.Diagnostics.Stopwatch.StartNew(); PlanetCity.TouchAll(); PlanetCity.Rebuild(); log.Append("bodem: ").Append(PlanetCity.Ground2Tris).Append(" driehoeken in ").Append(sw1.ElapsedMilliseconds).AppendLine(" ms"); }
        PlanetCity.TouchAll(); PlanetCity.Rebuild(); PlanetCity.Rebuild(); PlanetCity.RebuildGrid(); yield return null; yield return null;
        File.WriteAllText(Path.Combine(Dir, "inspel.txt"), log.ToString());
        var centers = new List<int>(); foreach (var kv in PlanetCity.Cells) { if (!PlanetCity.IsRoad(kv.Key)) continue; bool far = true; foreach (int c in centers) if (Vector3.Distance(Planet.Cells[c].dir, Planet.Cells[kv.Key].dir) * Planet.R < 40f) { far = false; break; } if (far) centers.Add(kv.Key); }
        if (centers.Count > 0) { Hc = centers[0]; bouw = new System.Text.StringBuilder(); yield return Scan(centers[0], 0f, "inspel_scan", centers, 24f); log.Append(bouw.ToString()); }
        for (int st = 0; st < 2; st++) yield return Shot("inspel_" + st, Planet.Starts[st], Dh(Planet.Starts[st], 0.4f), 260f, 50f);
        File.WriteAllText(Path.Combine(Dir, "inspel.txt"), log.ToString());
        File.WriteAllText(Path.Combine(Dir, "klaar.txt"), "klaar"); if (pb != null) pb.enabled = true;
    }
    // Hele planeet vol met Terrein v2 (standaard): de tijd van het profiel over alle wegen, opslaan en laden, een volledige herbouw, en
    // een klik (een nieuwe weg van 6 vakken op een leeg stuk, zoals PlanetBuilder: PlaceLine, SetLevel, Reprofile, Rebuild).
    IEnumerator VolPlaneet()
    {
        var log = new System.Text.StringBuilder(); PlanetCity.ClearAll();   // (Terrein v2 aan of uit naar de vlag: "volplaneet v2" of "volplaneet")
        var sw = System.Diagnostics.Stopwatch.StartNew(); var e = PlanetCity.FillSteps(null); while (e.MoveNext()) { } long tf = sw.ElapsedMilliseconds;
        int nw = 0; foreach (var kv in PlanetCity.Cells) if (PlanetCity.IsWalk(kv.Key)) nw++;
        sw.Restart(); PlanetCity.Reprofile(); long tp = sw.ElapsedMilliseconds;
        log.Append("planeet vol in ").Append(tf).Append(" ms; ").Append(nw).Append(" weg- en padvakken; profiel ").Append(tp).Append(" ms; RoadH ").Append(PlanetCity.RoadH.Count).AppendLine();
        PlanetCity.G2Reset(); sw.Restart(); PlanetCity.TouchAll(); PlanetCity.Rebuild(); long tr = sw.ElapsedMilliseconds; log.Append("volledige herbouw ").Append(tr).Append(" ms; Terrein v2 ").AppendLine(PlanetCity.Ground2 ? "aan" : "uit");
        if (PlanetCity.Ground2) log.Append("  ").AppendLine(PlanetCity.G2Stats()).Append("  ").AppendLine(PlanetCity.G2Steps());
        string snap = PlanetCity.Snapshot(); PlanetCity.ClearAll(); sw.Restart(); PlanetCity.Restore(snap); long tl = sw.ElapsedMilliseconds;
        sw.Restart(); PlanetCity.Rebuild(); long tl2 = sw.ElapsedMilliseconds; log.Append("laden ").Append(tl).Append(" ms, daarna herbouw ").Append(tl2).AppendLine(" ms");
        // klikken: een nieuwe weg op een leeg stuk ver van de wegen, vijf keer
        var times = new List<long>(); var rnd = new System.Random(5);
        for (int k = 0; k < 5; k++)
        {
            int a = -1; for (int t = 0; t < 2000 && a < 0; t++) { int c = rnd.Next(Planet.Cells.Length); if (PlanetCity.Cells.ContainsKey(c) || Planet.IsPole(c)) continue; bool free = true; foreach (int j in Nearby(c, 90f)) if (PlanetCity.IsWalk(j) || (PlanetCity.Cells.TryGetValue(j, out var lj) && lj.Count > 0 && lj[0] != "park")) { free = false; break; } if (free) a = c; }   // (geen weg of gebouw binnen 90 m; bos mag)
            if (a < 0) break; int b = Go(a, 0f, 75f); var line = PlanetCity.LinePlan(a, b); if (line == null || line.Count < 3) continue;
            PlanetCity.G2Reset(); sw.Restart(); long tpl = 0, tsl = 0; if (PlanetCity.PlaceLine(line, "weg") > 0) { tpl = sw.ElapsedMilliseconds; PlanetCity.SetLevel(line, 0, "weg"); tsl = sw.ElapsedMilliseconds - tpl; if (PlanetCity.Ground2) PlanetCity.Reprofile(line); else { PlanetCity.DipUnder(line); PlanetCity.BoreLine(line); } } long tpk = sw.ElapsedMilliseconds; PlanetCity.Rebuild(); long tk = sw.ElapsedMilliseconds;
            log.Append("  PlaceLine ").Append(tpl).Append(" ms, SetLevel ").Append(tsl).AppendLine(" ms");
            if (PlanetCity.Ground2 && k == 0) log.Append("  ").AppendLine(PlanetCity.G2Stats()).Append("  ").AppendLine(PlanetCity.G2Steps()).Append("  ").AppendLine(PlanetCity.UpdateTimes);
            times.Add(tk); log.Append("klik ").Append(k + 1).Append(": profiel ").Append(tpk).Append(" ms (waarvan ProfileAll ").Append(PlanetCity.LastProfileOnlyMs).Append(" ms), met herbouw ").Append(tk).Append(" ms (").Append(line.Count).AppendLine(" vakken)");
        }
        File.WriteAllText(Path.Combine(Dir, "volplaneet.txt"), log.ToString());
        PlanetCity.RebuildGrid(); yield return null; yield return null;
        yield return Shot("volplaneet_0", Planet.Starts[0], Dh(Planet.Starts[0], 0.4f), 260f, 50f);
        File.WriteAllText(Path.Combine(Dir, "klaar.txt"), "klaar"); if (pb != null) pb.enabled = true;
    }
    IEnumerator Hoogtetest(bool natuur = false)
    {
        string nm = natuur ? "natuurtest" : "hoogtetest";
        var log = new System.Text.StringBuilder(); float t0 = Time.realtimeSinceStartup;
        void Fase(string w) { File.AppendAllText(Path.Combine(Dir, "fase.txt"), (Time.realtimeSinceStartup - t0).ToString("0.0") + " s: " + w + "\n"); }
        Fase("start"); yield return natuur ? PlanetCity.NatuurTestSteps((f, w) => Fase(w + " " + f.ToString("0.00"))) : PlanetCity.HeightTestSteps((f, w) => Fase(w + " " + f.ToString("0.00"))); Fase("planeet en wegen");
        log.Append("klaar in ").Append((Time.realtimeSinceStartup - t0).ToString("0.0")).Append(" s; wegen ").Append(PlanetCity.HeightTestRoadCount).AppendLine(" van startpunt naar startpunt aangekomen");
        int nt = 0, nb = 0, nr = 0; foreach (long k in PlanetCity.TunnelLinks) { int a = (int)(k >> 20), b = (int)(k & 0xFFFFF); if (PlanetCity.IsSpan(a, b)) nb++; else if (PlanetCity.IsBore(a, b)) nt++; }
        foreach (var kv in PlanetCity.Cells) if (PlanetCity.IsRoad(kv.Key)) nr++;
        log.Append(nr).Append(" wegvakken, ").Append(nt).Append(" tunnels, ").Append(nb).AppendLine(" dalbruggen");
        foreach (string p in PlanetCity.CheckGraph()) log.Append("  GRAAF ").AppendLine(p);
        PlanetCity.Rebuild(); Fase("rebuild 1"); PlanetCity.Rebuild(); PlanetCity.RebuildGrid(); yield return null; yield return null; Fase("rebuild 2");
        {   // voor de vergelijking met en zonder Terrein v2: driehoeken van de bodem, en de tijd van een volledige herbouw
            var swr = System.Diagnostics.Stopwatch.StartNew(); PlanetCity.TouchAll(); PlanetCity.Rebuild(); long tr = swr.ElapsedMilliseconds;
            long ntri = 0, nv = 0; foreach (var mf in FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)) if (mf.gameObject.layer == Layers.Bodem && mf.sharedMesh != null) { ntri += mf.sharedMesh.triangles.Length / 3; nv += mf.sharedMesh.vertexCount; }
            log.Append("bodem: ").Append(ntri).Append(" driehoeken, ").Append(nv).Append(" hoekpunten; volledige herbouw ").Append(tr).Append(" ms; Terrein v2 ").AppendLine(PlanetCity.Ground2 ? "aan" + (PlanetCity.RoadH.Count > 0 ? " met profiel" : "") : "uit");
        }
        File.WriteAllText(Path.Combine(Dir, nm + ".txt"), log.ToString());
        var centers = new List<int>(); foreach (var kv in PlanetCity.Cells) { if (!PlanetCity.IsRoad(kv.Key)) continue; bool far = true; foreach (int c in centers) if (Vector3.Distance(Planet.Cells[c].dir, Planet.Cells[kv.Key].dir) * Planet.R < 45f) { far = false; break; } if (far) centers.Add(kv.Key); }
        Hc = centers[0]; bouw = new System.Text.StringBuilder(); Fase("scan"); yield return Scan(centers[0], 0f, nm + "_scan", centers, 24f); int worst = scanWorst; Fase("scan klaar"); { int nu = 0; foreach (var kv in PlanetCity.RoadH) if (PlanetCity.Underground(kv.Key) && nu < 3) { nu++; yield return Shot("hoogtetest_ondergronds" + nu, kv.Key, Dh(kv.Key, 0.7f), 6f, 4f); } }   // de ondergrondse kruispunten (nieuwe opbouw)
        { int nl2 = 0; foreach (var kv in PlanetCity.RoadH) { if (nl2 >= 3) break; foreach (int p in PlanetCity.Partners(kv.Key)) if (PlanetCity.IsSpan(kv.Key, p)) { nl2++; Vector3 fw = (Planet.Cells[p].dir - Planet.Cells[kv.Key].dir).normalized; yield return Shot("hoogtetest_landhoofd" + nl2, kv.Key, Quaternion.AngleAxis(50f, Planet.Cells[kv.Key].dir) * fw, 28f, 25f); break; } } }   // landhoofden van dalbruggen
        for (int s = 0; s < 4; s++) yield return Shot(nm + "_" + s, Planet.Starts[s], Dh(Planet.Starts[s], 0.4f), 260f, 50f);
        if (natuur)   // vaste plekken om met en zonder Terrein v2 te vergelijken: dichtbij langs de wegen (dezelfde vakken, dezelfde camera)
        {
            var spots = new List<int>(); foreach (var kv in PlanetCity.Cells) { if (!PlanetCity.IsRoad(kv.Key)) continue; bool far = true; foreach (int c in spots) if (Vector3.Distance(Planet.Cells[c].dir, Planet.Cells[kv.Key].dir) * Planet.R < 150f) { far = false; break; } if (far) spots.Add(kv.Key); }
            spots.Sort(); for (int k = 0; k < Mathf.Min(6, spots.Count); k++) { int c = spots[k * spots.Count / Mathf.Min(6, spots.Count)]; yield return Shot(nm + "_plek" + k, c, Quaternion.AngleAxis(40f, Planet.Cells[c].dir) * Dh(c, 0f), 55f, 30f); }
        }
        log.Append(bouw.ToString()); File.WriteAllText(Path.Combine(Dir, nm + ".txt"), log.ToString());
        File.WriteAllText(Path.Combine(Dir, "klaar.txt"), "klaar");
        Breng(worst >= 0 ? worst : Planet.Starts[0], worst >= 0 ? "Hier klopt het terrein nog niet" : "De hoogtetest met wegen");
    }
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
