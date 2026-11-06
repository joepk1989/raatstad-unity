using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Een opgeslagen stad: naam, wanneer, een paar cijfers en waar hij vandaan komt (from: een oud opslagslot)
[Serializable] public class CityInfo { public string id, name, from; public long created, saved; public int cells = -1, blocks = -1, beauty = -1; }

// Meerdere steden, elk in de map steden in persistentDataPath: <id>.json (de stad zelf, PlanetCity.Snapshot), <id>.info.json
// (CityInfo) en <id>.png (een kleine schermafbeelding). De oude slots planeet1..3.json worden de eerste keer gekopieerd als
// "Stad 1".."Stad 3"; de oude bestanden zelf worden nooit beschreven. Tijdens het spelen schrijft PlanetCity.Save naar een
// werkbestand (Slot 0: planeet0.json), opslaan als stad gaat hierlangs (Save).
public static class CityFiles
{
    public static string Dir => Path.Combine(Application.persistentDataPath, "steden");
    static string DataPath(string id) => Path.Combine(Dir, id + ".json");
    static string InfoPath(string id) => Path.Combine(Dir, id + ".info.json");
    static string ThumbPath(string id) => Path.Combine(Dir, id + ".png");
    const string LastKey = "raatstad-laatste-stad";

    public static CityInfo Current;   // de stad die nu open is (null: op het startscherm, nog niets gekozen)
    static List<CityInfo> list;
    static readonly Dictionary<string, Texture2D> thumbs = new Dictionary<string, Texture2D>();
    static readonly Dictionary<string, long> thumbVer = new Dictionary<string, long>();
    public static void ResetStatics() { Current = null; list = null; thumbs.Clear(); thumbVer.Clear(); }

    // alle steden, de laatst bewaarde eerst
    public static List<CityInfo> All { get { if (list == null) Scan(); return list; } }
    public static void Scan()
    {
        list = new List<CityInfo>();
        try
        {
            Directory.CreateDirectory(Dir);
            foreach (var f in Directory.GetFiles(Dir, "*.info.json"))
            {
                try { var c = JsonUtility.FromJson<CityInfo>(File.ReadAllText(f)); if (c != null && !string.IsNullOrEmpty(c.id) && File.Exists(DataPath(c.id))) list.Add(c); }
                catch (Exception e) { Debug.LogWarning("Stad overgeslagen (" + Path.GetFileName(f) + "): " + e.Message); }
            }
            Import();
        }
        catch (Exception e) { Debug.LogWarning("Steden lezen mislukt: " + e.Message); }
        list.Sort((a, b) => b.saved.CompareTo(a.saved));
    }
    // de oude slots overnemen (kopie, het origineel blijft staan zoals het is)
    static void Import()
    {
        for (int n = 1; n <= 3; n++)
        {
            string from = "planeet" + n + ".json", src = Path.Combine(Application.persistentDataPath, from);
            if (!File.Exists(src) || list.Exists(c => c.from == from)) continue;
            string id = "oud-slot-" + n; if (!File.Exists(DataPath(id))) File.Copy(src, DataPath(id));
            long t = File.GetLastWriteTime(src).Ticks;
            var info = new CityInfo { id = id, name = "Stad " + n, from = from, created = t, saved = t };
            try { var sd = JsonUtility.FromJson<PSaveData>(File.ReadAllText(src)); if (sd != null) info.cells = sd.cells.Count; } catch { }
            WriteText(InfoPath(id), JsonUtility.ToJson(info)); list.Add(info);
        }
    }

    public static CityInfo Last
    {
        get
        {
            var all = All; if (all.Count == 0) return null;
            string id = PlayerPrefs.GetString(LastKey, ""); var c = all.Find(x => x.id == id);
            return c ?? all[0];
        }
    }

    // een nieuwe stad (nog niet op schijf: dat gebeurt bij Save)
    public static CityInfo Create(string name)
    {
        string id = "stad-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"); int k = 2; string baseId = id;
        while (File.Exists(DataPath(id)) || File.Exists(InfoPath(id))) id = baseId + "-" + k++;
        long now = DateTime.Now.Ticks; return new CityInfo { id = id, name = CleanName(name), created = now, saved = now };
    }
    public static string CleanName(string s)
    {
        s = (s ?? "").Trim(); if (s.Length > 40) s = s.Substring(0, 40);
        return s.Length == 0 ? "Naamloze stad" : s;
    }
    // een naam die nog niet bestaat: "Nieuwe stad", "Nieuwe stad 2", ...
    public static string FreeName(string baseName)
    {
        var all = All; string n = baseName; int k = 2;
        while (all.Exists(c => string.Equals(c.name, n, StringComparison.OrdinalIgnoreCase))) n = baseName + " " + k++;
        return n;
    }

    // de stad die open is bewaren als c: data, cijfers en (als die er is) de schermafbeelding. json: de snapshot, als die al gemaakt is.
    public static bool Save(CityInfo c, Texture2D thumb, string json = null)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            WriteText(DataPath(c.id), json ?? PlanetCity.Snapshot());
            var b = Beauty.Compute(); c.cells = PlanetCity.Cells.Count; c.blocks = b != null ? b.blocks : -1; c.beauty = b != null ? b.score : -1;
            c.saved = DateTime.Now.Ticks; WriteText(InfoPath(c.id), JsonUtility.ToJson(c));
            if (thumb != null) SaveThumb(c, thumb);
            PlayerPrefs.SetString(LastKey, c.id); PlayerPrefs.Save();
            if (list != null) { list.RemoveAll(x => x.id == c.id); list.Insert(0, c); }
            return true;
        }
        catch (Exception e) { Debug.LogWarning("Stad opslaan mislukt: " + e.Message); return false; }
    }
    public static void SaveThumb(CityInfo c, Texture2D thumb)
    {
        try { File.WriteAllBytes(ThumbPath(c.id), thumb.EncodeToPNG()); thumbVer.Remove(c.id); }
        catch (Exception e) { Debug.LogWarning("Voorbeeld opslaan mislukt: " + e.Message); }
    }
    public static string Read(CityInfo c)
    {
        try { return File.ReadAllText(DataPath(c.id)); }
        catch (Exception e) { Debug.LogWarning("Stad lezen mislukt: " + e.Message); return null; }
    }
    // een stad weggooien: zijn bestanden gaan naar de map prullenbak (niet definitief weg; met de hand terug te zetten)
    public static bool Delete(CityInfo c)
    {
        try
        {
            string bin = Path.Combine(Dir, "prullenbak"); Directory.CreateDirectory(bin);
            string stamp = File.Exists(Path.Combine(bin, c.id + ".json")) ? "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") : "";
            foreach (var (src, ext) in new[] { (InfoPath(c.id), ".info.json"), (ThumbPath(c.id), ".png"), (DataPath(c.id), ".json") })
                if (File.Exists(src)) File.Move(src, Path.Combine(bin, c.id + stamp + ext));
            if (thumbs.TryGetValue(c.id, out var t) && t != null) UnityEngine.Object.Destroy(t);
            thumbs.Remove(c.id); thumbVer.Remove(c.id);
            list?.RemoveAll(x => x.id == c.id);
            if (Current != null && Current.id == c.id) Current = null;
            if (PlayerPrefs.GetString(LastKey, "") == c.id) { PlayerPrefs.DeleteKey(LastKey); PlayerPrefs.Save(); }
            return true;
        }
        catch (Exception e) { Debug.LogWarning("Stad verwijderen mislukt: " + e.Message); return false; }
    }
    public static void MarkOpened(CityInfo c) { Current = c; PlayerPrefs.SetString(LastKey, c.id); PlayerPrefs.Save(); }

    // de schermafbeelding van een stad (null als er nog geen is); onthouden tot het bestand verandert
    public static Texture2D Thumb(CityInfo c)
    {
        if (c == null) return null; string p = ThumbPath(c.id);
        long ver = File.Exists(p) ? File.GetLastWriteTimeUtc(p).Ticks : 0;
        if (thumbVer.TryGetValue(c.id, out long v) && v == ver) return thumbs.TryGetValue(c.id, out var t0) ? t0 : null;
        thumbVer[c.id] = ver; if (thumbs.TryGetValue(c.id, out var old) && old != null) UnityEngine.Object.Destroy(old); thumbs.Remove(c.id);
        if (ver == 0) return null;
        try { var t = new Texture2D(2, 2, TextureFormat.RGB24, false); if (t.LoadImage(File.ReadAllBytes(p))) { thumbs[c.id] = t; return t; } }
        catch (Exception e) { Debug.LogWarning("Voorbeeld lezen mislukt: " + e.Message); }
        return null;
    }

    static readonly string[] Months = { "jan", "feb", "mrt", "apr", "mei", "jun", "jul", "aug", "sep", "okt", "nov", "dec" };
    public static string When(long ticks)
    {
        var t = new DateTime(ticks); var now = DateTime.Now; string hm = t.ToString("HH:mm");
        if (t.Date == now.Date) return "vandaag " + hm;
        if (t.Date == now.Date.AddDays(-1)) return "gisteren " + hm;
        return t.Day + " " + Months[t.Month - 1] + (t.Year != now.Year ? " " + t.Year : "") + " " + hm;
    }
    public static string Stats(CityInfo c)
    {
        string s = c.cells >= 0 ? c.cells + " vakken" : "";
        if (c.beauty >= 0) s += (s.Length > 0 ? " · " : "") + "schoonheid " + c.beauty;
        return s;
    }

    // eerst naar een tijdelijk bestand, dan erover: een half geschreven stad bestaat nooit
    static void WriteText(string path, string text)
    {
        string tmp = path + ".tmp"; File.WriteAllText(tmp, text);
        if (File.Exists(path)) File.Replace(tmp, path, null); else File.Move(tmp, path);
    }
}
