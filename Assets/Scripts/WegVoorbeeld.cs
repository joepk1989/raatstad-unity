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
        if (!File.Exists(Flag)) return; File.Delete(Flag);
        Application.runInBackground = true;
        new GameObject("Wegvoorbeeld").AddComponent<WegVoorbeeld>();
    }

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
