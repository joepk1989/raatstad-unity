using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Meldingen voor Claude-sessies: F8 (of Menu › Gereedschap › Melding maken) pauzeert het spel, je typt wat er mis is, en bij
// Opslaan komt in persistentDataPath/meldingen/<nummer>-<datum>/: beeld.png (zonder interface), beeld-met-interface.png,
// melding.json (tekst, camera, vak onder de muis, gereedschap, weer, planeet, de laatste 50 Console-regels) en stad.json
// (PlanetCity.Snapshot). Terugspelen: niveautest.txt met "melding 3" laadt die stad in testslot 9, zet camera, weer en tijd
// terug en maakt terug.png (MeldingTest hieronder).
public static class Meldingen
{
    public static int Last;   // het nummer van de laatst bewaarde melding
    public static string Root => Path.Combine(Application.persistentDataPath, "meldingen");

    [Serializable] public class LogLine { public string tijd, soort, tekst, stapel; }
    [Serializable] public class CamData
    {
        public string modus;   // bouwen, lopen, camera (vliegen) of ruimte
        public Vector3 positie, hoeken; public Quaternion rotatie; public float fov, hoogteBovenBol, hoogteBovenMaaiveld;
        public int kijktNaarVak = -1; public Vector3 kijktNaarPunt, focus, vooruit; public float pitch, afstand, camH, breedte, lengte;
    }
    [Serializable] public class CellData
    {
        public int id = -1; public bool terrIngesteld; public float terr, g, breedte, lengte; public string[] inhoud; public int[] buren; public Vector3 punt;
    }
    [Serializable] public class ToolData
    {
        public string blok, id, naam; public bool gum, ongelijkvloers, fiets, eenrichting, aanwijzen, aiBouwer;
        public int niveau, wegsoort, terreinModus, penseel;
    }
    [Serializable] public class WorldData
    {
        public string weer; public bool wisselend; public float wolken, mist, regen, sneeuw, storm, uur, tijdSnelheid, nacht;
        public int planeetM, vakken, bebouwd, slot; public float straal; public bool raster;
    }
    [Serializable] public class Data
    {
        public int nummer; public string tekst, tijd, stad, unity; public int schermBreed, schermHoog;
        public CamData camera = new CamData(); public CellData vak = new CellData(); public ToolData gereedschap = new ToolData(); public WorldData wereld = new WorldData();
        public List<LogLine> console = new List<LogLine>();
    }

    // ---- de Console: de laatste 50 regels (met de bovenkant van de stapel bij fouten)
    static readonly List<LogLine> log = new List<LogLine>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Hook() { log.Clear(); Application.logMessageReceived -= OnLog; Application.logMessageReceived += OnLog; }
    static void OnLog(string msg, string stack, LogType type)
    {
        bool bad = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
        string st = null;
        if (bad && !string.IsNullOrEmpty(stack)) { var parts = stack.Split('\n'); st = string.Join("\n", parts, 0, Mathf.Min(6, parts.Length)).Trim(); }
        if (msg != null && msg.Length > 600) msg = msg.Substring(0, 600) + "…";
        log.Add(new LogLine { tijd = DateTime.Now.ToString("HH:mm:ss"), soort = type.ToString(), tekst = msg, stapel = st });
        if (log.Count > 50) log.RemoveAt(0);
    }
    public static List<LogLine> RecentLog() => new List<LogLine>(log);

    public static bool F8Down
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current; return kb != null && kb.f8Key.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.F8);
#endif
        }
    }

    // wat de camera ziet, zonder interface (zoals PlanetBuilder.Shot, maar op ware grootte)
    public static Texture2D CleanShot(Camera cam, int w, int h)
    {
        var rt = RenderTexture.GetTemporary(w, h, 24); var old = cam.targetTexture; var oldActive = RenderTexture.active;
        cam.targetTexture = rt; cam.Render(); cam.targetTexture = old;
        RenderTexture.active = rt; var t = new Texture2D(w, h, TextureFormat.RGB24, false); t.ReadPixels(new Rect(0, 0, w, h), 0, 0); t.Apply();
        RenderTexture.active = oldActive; RenderTexture.ReleaseTemporary(rt); return t;
    }

    static int NextNumber()
    {
        int n = 0;
        if (Directory.Exists(Root)) foreach (var d in Directory.GetDirectories(Root)) { string s = Path.GetFileName(d); int dash = s.IndexOf('-'); if (dash > 0 && int.TryParse(s.Substring(0, dash), out int k)) n = Mathf.Max(n, k); }
        return n + 1;
    }
    public static string FolderOf(int n)
    {
        if (!Directory.Exists(Root)) return null;
        foreach (var d in Directory.GetDirectories(Root)) if (Path.GetFileName(d).StartsWith(n + "-")) return d;
        return null;
    }

    // alles wegschrijven; geeft het nummer terug (of -1)
    public static int Write(Data d, string text, Texture2D withUi, Texture2D clean, string city)
    {
        try
        {
            d.nummer = NextNumber(); d.tekst = text; Last = d.nummer;
            string dir = Path.Combine(Root, d.nummer + "-" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")); Directory.CreateDirectory(dir);
            if (clean != null) File.WriteAllBytes(Path.Combine(dir, "beeld.png"), clean.EncodeToPNG());
            if (withUi != null) File.WriteAllBytes(Path.Combine(dir, "beeld-met-interface.png"), withUi.EncodeToPNG());
            File.WriteAllText(Path.Combine(dir, "melding.json"), JsonUtility.ToJson(d, true));
            File.WriteAllText(Path.Combine(dir, "stad.json"), city);
            return d.nummer;
        }
        catch (Exception e) { Debug.LogWarning("Melding bewaren mislukt: " + e.Message); return -1; }
    }
}

// ---- in het spel: de toets, het venster en de toestand van de bouwer
public partial class PlanetBuilder
{
    bool reportOpen, reportBusy; string reportText = ""; float reportScale = 1f; Texture2D reportUi, reportClean; Meldingen.Data reportData; string reportCity;
    int reportDone; float reportDoneT = -99f;   // de bevestiging na het bewaren: "Melding 3 bewaard"
    CursorLockMode reportLock; bool reportCursor;

    // in Update: true zolang de melding loopt (dan geen andere invoer)
    bool ReportUpdate()
    {
        if (!reportOpen && !reportBusy && Meldingen.F8Down) StartReport();
        return reportOpen || reportBusy;
    }
    void StartReport() { if (reportOpen || reportBusy) return; menuOpen = false; reportBusy = true; StartCoroutine(ReportRoutine()); }

    IEnumerator ReportRoutine()
    {
        yield return null;   // een beeld zonder het menu (als de melding uit het menu kwam)
        reportData = ReportState(); reportCity = PlanetCity.Snapshot();
        reportClean = Meldingen.CleanShot(Cam, Screen.width, Screen.height);
        yield return new WaitForEndOfFrame();
        reportUi = ScreenCapture.CaptureScreenshotAsTexture();
        reportScale = Time.timeScale; Time.timeScale = 0f;
        reportLock = Cursor.lockState; reportCursor = Cursor.visible; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        reportText = ""; reportOpen = true; reportBusy = false;
    }

    void ReportClose(bool save)
    {
        int n = save ? Meldingen.Write(reportData, reportText.Trim(), reportUi, reportClean, reportCity) : -1;
        if (reportUi != null) Destroy(reportUi); if (reportClean != null) Destroy(reportClean); reportUi = reportClean = null; reportData = null; reportCity = null;
        Time.timeScale = reportScale; Cursor.lockState = reportLock; Cursor.visible = reportCursor; reportOpen = false;
        if (n > 0) { reportDone = n; reportDoneT = Time.unscaledTime; Sounds.Fx("coin"); }   // alleen een bevestiging als het bewaren lukte (anders een waarschuwing in de Console)
    }

    Meldingen.Data ReportState()
    {
        var d = new Meldingen.Data { tijd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), stad = CityName, unity = Application.unityVersion, schermBreed = Screen.width, schermHoog = Screen.height };
        var c = d.camera; var t = Cam.transform;
        c.modus = flying ? "camera" : walking ? "lopen" : inSpace ? "ruimte" : "bouwen";
        c.positie = t.position; c.rotatie = t.rotation; c.hoeken = t.eulerAngles; c.fov = Cam.fieldOfView;
        c.hoogteBovenBol = t.position.magnitude - Planet.R; c.hoogteBovenMaaiveld = c.hoogteBovenBol - PlanetCity.TerrainAt(t.position.normalized);
        c.focus = focus; c.vooruit = fwd; c.pitch = pitch; c.afstand = dist; c.camH = float.IsNaN(camH) ? 0f : camH;
        c.kijktNaarVak = Planet.Nearest(focus * Planet.R); c.kijktNaarPunt = focus * (Planet.R + c.camH);
        PlanetCity.LatLon(focus, out c.breedte, out c.lengte);
        int i = hasHover ? hover : walking ? Planet.Nearest(walker.transform.position) : -1;
        if (i >= 0)
        {
            var v = d.vak; v.id = i; v.terrIngesteld = PlanetCity.Terr.TryGetValue(i, out v.terr); v.g = PlanetCity.G(i);
            v.inhoud = PlanetCity.Cells.TryGetValue(i, out var l) ? l.ToArray() : new string[0]; v.buren = Planet.Cells[i].nb;
            v.punt = hasHover ? hoverPoint : walker.transform.position; PlanetCity.LatLon(Planet.Cells[i].dir, out v.breedte, out v.lengte);
        }
        var g = d.gereedschap; g.blok = CurBlock; g.id = Cur.id; g.naam = Cur.name; g.gum = eraser; g.niveau = PlanetCity.DragLevel; g.ongelijkvloers = PlanetCity.DragUnder;
        g.fiets = PlanetCity.DragBike; g.eenrichting = PlanetCity.DragOneWay; g.wegsoort = PlanetCity.DragStyle; g.terreinModus = terrMode; g.penseel = BrushSize(brush);
        g.aanwijzen = cursorMode; g.aiBouwer = aiOn;
        var w = d.wereld;
        if (Weather.I != null) { w.weer = Weather.I.Current.ToString(); w.wisselend = Weather.I.Wissel; }
        w.wolken = Weather.Cloud; w.mist = Weather.Fog; w.regen = Weather.Rain; w.sneeuw = Weather.Snow; w.storm = Weather.Storm;
        w.uur = DayNight.Hour; w.tijdSnelheid = DayNight.Speed; w.nacht = DayNight.Night;
        w.planeetM = Planet.M; w.straal = Planet.R; w.vakken = Planet.Cells.Length; w.bebouwd = PlanetCity.Cells.Count; w.slot = PlanetCity.Slot; w.raster = PlanetCity.GridOn;
        d.console = Meldingen.RecentLog();
        return d;
    }

    static GUIStyle reportStyle;
    void ReportGui(float W, float H)
    {
        if (!reportOpen) { ReportDone(W, H); return; }
        var e = Event.current;
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { e.Use(); ReportClose(false); return; }
        if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && !e.shift) { e.Use(); ReportClose(true); return; }
        if (reportStyle == null || reportStyle.font == null)
        {
            reportStyle = new GUIStyle { font = Resources.Load<Font>("Fonts/AtkinsonHyperlegible-Bold"), fontSize = 14, alignment = TextAnchor.UpperLeft, wordWrap = true, clipping = TextClipping.Clip, padding = new RectOffset(12, 12, 10, 10) };
            reportStyle.normal.textColor = reportStyle.focused.textColor = reportStyle.hover.textColor = Ui.Ink;
        }
        Ui.DimBack(0.45f);
        float w = 560f, iw = w - 36f, ph = reportUi != null ? iw * reportUi.height / Mathf.Max(1f, reportUi.width) : 0f; ph = Mathf.Min(ph, 220f);
        float h = 14f + 34f + 24f + ph + 12f + 120f + 14f + 40f + 18f;
        var r = new Rect((W - w) * 0.5f, Mathf.Max(16f, (H - h) * 0.5f), w, h); Ui.Panel(r, true, 16f);
        float x = r.x + 18f, y = r.y + 14f;
        Ui.Label(new Rect(x, y, iw, 28f), "MELDING VOOR CLAUDE", Ui.T.Title); y += 34f;
        Ui.Label(new Rect(x, y, iw, 18f), "Wat is er mis? Beeld, plek, stad en de Console gaan mee. Enter bewaart, Esc annuleert.", Ui.T.Dim, null, null, 12); y += 24f;
        if (reportUi != null) { float pw = ph * reportUi.width / Mathf.Max(1f, reportUi.height); GUI.DrawTexture(new Rect(x + (iw - pw) * 0.5f, y, pw, ph), reportUi, ScaleMode.ScaleToFit); y += ph + 12f; }
        var tr = new Rect(x, y, iw, 120f); Ui.Box(tr, new Color(0f, 0f, 0f, 0.35f), Ui.Signal, 10f);
        GUI.SetNextControlName("melding"); reportText = GUI.TextArea(tr, reportText, 2000, reportStyle);
        if (GUI.GetNameOfFocusedControl() != "melding") GUI.FocusControl("melding");
        y += 120f + 14f; float half = (iw - 8f) * 0.5f;
        if (Ui.Button(new Rect(x, y, half, 40f), "Annuleren")) { ReportClose(false); return; }
        if (Ui.Button(new Rect(x + half + 8f, y, half, 40f), "Opslaan", true)) { ReportClose(true); return; }
    }

    // midden in beeld, 2,5 s, vervaagt aan het eind
    void ReportDone(float W, float H)
    {
        float age = Time.unscaledTime - reportDoneT; if (age > 2.5f) return;
        float a = Mathf.Clamp01((2.5f - age) / 0.4f); string t = "Melding " + reportDone + " bewaard";
        float w = Ui.Width(t, Ui.T.Title) + 90f; var r = new Rect((W - w) * 0.5f, H * 0.38f, w, 56f);
        var old = GUI.color; GUI.color = new Color(1f, 1f, 1f, a);
        Ui.Panel(r, true, 16f); Ui.Tex(new Rect(r.x + 18f, r.y + 14f, 26f, 28f), Ui.Hex(Ui.Signal, Ui.Signal, 0f, true));
        Ui.Label(new Rect(r.x + 56f, r.y, w - 66f, 56f), t, Ui.T.Title, null, TextAnchor.MiddleLeft);
        GUI.color = old;
    }

    // voor MeldingTest ("melding maak"): dezelfde weg als F8, dan de tekst invullen en bewaren
    public IEnumerator ReportForTest(string text)
    {
        StartReport(); while (!reportOpen) yield return null;
        for (int k = 0; k < 3; k++) yield return null;   // het venster even in beeld
        reportText = text; ReportClose(true);
    }

    // terugspelen (MeldingTest): de bouwer staat stil, de camera precies zoals in de melding
    public static void ApplyReport(Meldingen.Data d, Camera cam)
    {
        var c = d.camera; cam.transform.SetPositionAndRotation(c.positie, c.rotatie); cam.fieldOfView = c.fov;
        DayNight.Hour = d.wereld.uur; DayNight.Speed = 0f;
        if (Weather.I != null && Enum.TryParse(d.wereld.weer, out Weather.Kind k)) Weather.I.Set(k, true);
        Weather.Cloud = d.wereld.wolken; Weather.Fog = d.wereld.mist; Weather.Rain = d.wereld.regen; Weather.Snow = d.wereld.sneeuw; Weather.Storm = d.wereld.storm;
    }
}

// Terugspelen voor sessies: niveautest.txt met "melding 3" (AutoHerstart start Play). Laadt stad.json in testslot 9, zet
// camera, weer en tijd terug en maakt terug.png in de map van de melding (plus terug.txt), dan stopt Play.
// "melding maak <tekst>" maakt zelf een melding langs dezelfde weg als F8 (om dat te testen).
// Verkeer, mensen en wolken lopen anders: die zijn niet hetzelfde als in beeld.png.
public class MeldingTest : MonoBehaviour
{
    static int Nr; static string Make;   // Make: "melding maak <tekst>" maakt zelf een melding (test van F8 zonder toetsenbord)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        string flag = Path.Combine(Application.persistentDataPath, "niveautest.txt");
        if (!File.Exists(flag)) return; string t = File.ReadAllText(flag).Trim().ToLowerInvariant(); if (!t.StartsWith("melding")) return;
        string rest = t.Substring(7).Trim(); Make = null;
        if (rest.StartsWith("maak")) Make = rest.Substring(4).Trim(); else if (!int.TryParse(rest, out Nr)) return;
        File.Delete(flag);
        Application.runInBackground = true;
        var g = new GameObject("Meldingtest"); DontDestroyOnLoad(g); g.AddComponent<MeldingTest>();
    }

    IEnumerator Start()
    {
        string dir = Meldingen.FolderOf(Nr), note = "";
        PlanetBuilder pb = null;
        for (int k = 0; k < 600 && (pb = FindAnyObjectByType<PlanetBuilder>()) == null; k++) yield return null;
        yield return null; yield return null;
        if (Make != null)
        {
            PlanetCity.Slot = 9;
            if (PlanetCity.Cells.Count == 0) { PlanetCity.StartTown(Planet.North); yield return PlanetCity.RebuildSteps(f => { }); }   // iets om te zien
            for (float t = 0f; t < 3f; t += Time.unscaledDeltaTime) yield return null;   // even spelen
            yield return pb.ReportForTest(Make.Length > 0 ? Make : "testmelding");
            for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime) yield return null;
            yield return new WaitForEndOfFrame(); string md = Meldingen.FolderOf(Meldingen.Last);   // de bevestiging in beeld
            if (md != null) { var sh = ScreenCapture.CaptureScreenshotAsTexture(); File.WriteAllBytes(Path.Combine(md, "na-bewaren.png"), sh.EncodeToPNG()); Destroy(sh); }
            Stop(); yield break;
        }
        if (dir == null || pb == null) { Debug.LogWarning("Melding " + Nr + " niet gevonden"); Stop(); yield break; }
        var d = JsonUtility.FromJson<Meldingen.Data>(File.ReadAllText(Path.Combine(dir, "melding.json")));
        pb.enabled = false; var cam = pb.Cam;
        int slot = PlanetCity.Slot; PlanetCity.Slot = 9;
        if (d.wereld.planeetM != Planet.M) note += "planeetgrootte verschilt: melding M=" + d.wereld.planeetM + ", nu M=" + Planet.M + "\n";
        bool ok = PlanetCity.Restore(File.ReadAllText(Path.Combine(dir, "stad.json")));
        if (!ok) note += "stad.json kon niet geladen worden\n"; else PlanetCity.Save();
        yield return PlanetCity.RebuildSteps(f => { });
        PlanetCity.RebuildGrid(); PlanetCity.ShowGrid(d.wereld.raster);
        PlanetBuilder.ApplyReport(d, cam);
        for (float t = 0f; t < 2f; t += Time.unscaledDeltaTime) { PlanetBuilder.ApplyReport(d, cam); yield return null; }   // lucht, mist en shaders bijtrekken
        PlanetBuilder.ApplyReport(d, cam); yield return null;
        var shot = Meldingen.CleanShot(cam, d.schermBreed > 0 ? d.schermBreed : Screen.width, d.schermHoog > 0 ? d.schermHoog : Screen.height);
        File.WriteAllBytes(Path.Combine(dir, "terug.png"), shot.EncodeToPNG()); Destroy(shot);
        File.WriteAllText(Path.Combine(dir, "terug.txt"), "melding " + Nr + " teruggespeeld " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " in slot 9\n" + (note == "" ? "geen bijzonderheden\n" : note));
        PlanetCity.Slot = slot;
        Stop();
    }

    static void Stop()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
