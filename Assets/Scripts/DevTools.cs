using System.Collections.Generic;
using UnityEngine;

// Ontwikkelaarsschakelaars (console met de toets `, en Instellingen › Ontwikkelaar in het raatmenu). Alles standaard uit en
// onthouden in PlayerPrefs. De laatste meldingen uit Unity's log worden bewaard, zodat je ze in het spel kunt lezen.
public static class Dev
{
    static bool Get(string k) => PlayerPrefs.GetInt("raatstad-dev-" + k, 0) == 1;
    static void Set(string k, bool v) { PlayerPrefs.SetInt("raatstad-dev-" + k, v ? 1 : 0); }

    // de twaalf vijfhoeken fel oranje met een randje, en de grondkleur eromheen naar hoe vervormd een zeshoek is
    // (c.distort, acht tinten van gras naar oranje): de vervorming hoort bij de vijfhoek, dus één schakelaar
    public static bool Pentagons { get => Get("vijfhoeken"); set { Set("vijfhoeken", value); Redraw(); } }
    public static bool Distort => Pentagons;
    // de twintig startplekken: lichtblauw vak, witte paal met vlaggetje, en een naambordje
    public static bool Starts { get => Get("startplekken"); set { Set("startplekken", value); Redraw(); } }
    // bij het terreingereedschap de hoekpunten rond de muis (wit, oranje verzet, blauw het midden)
    public static bool Verts { get => Get("hoekpunten"); set => Set("hoekpunten", value); }
    // naast de muis: nummer, hoogte, vervorming, stapel en verbindingen van het vak
    public static bool CellInfo { get => Get("vakinfo"); set => Set("vakinfo", value); }
    // fps, vakken, leven en camera
    public static bool Stats { get => Get("statistieken"); set => Set("statistieken", value); }
    // de laatste waarschuwingen en fouten uit de log in beeld
    public static bool Log { get => Get("log"); set => Set("log", value); }

    // alles opnieuw tekenen (na een schakelaar die de grond verandert)
    public static void Redraw() { if (!Application.isPlaying) return; PlanetCity.TouchAll(); PlanetCity.Rebuild(); PlanetCity.RebuildGrid(); }

    // ---- de log: de laatste regels, waarschuwingen en fouten tellen apart
    public struct Line { public string text; public LogType type; public float t; }
    public static readonly List<Line> Lines = new List<Line>();
    public static int Errors, Warnings;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Hook()
    {
        Lines.Clear(); Errors = Warnings = 0;
        Application.logMessageReceived -= OnLog; Application.logMessageReceived += OnLog;
    }
    static void OnLog(string msg, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Errors++; else if (type == LogType.Warning) Warnings++;
        string first = msg; int nl = first.IndexOf('\n'); if (nl > 0) first = first.Substring(0, nl);
        if (first.Length > 160) first = first.Substring(0, 157) + "…";
        Lines.Add(new Line { text = first, type = type, t = Time.realtimeSinceStartup });
        if (Lines.Count > 40) Lines.RemoveAt(0);
    }
}
