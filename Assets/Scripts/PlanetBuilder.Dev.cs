using System.Collections.Generic;
using UnityEngine;

// De ontwikkelaarsconsole (toets `): de schakelaars van Dev (vijfhoeken en vervorming, hoekpunten, vakinfo, statistieken, log),
// alles opnieuw tekenen, en de laatste meldingen uit de log. Dezelfde schakelaars staan onder Instellingen › Ontwikkelaar
// (startscherm en raatmenu, PlanetBuilder.Settings.cs). De planeetgrootte staat onder Instellingen › Planeet.
public partial class PlanetBuilder
{
    bool devOpen; int devScroll;   // devScroll: de eerste zichtbare schakelaar in de console

    void DevKeys() { if (Inp.Down(K.Backquote)) devOpen = !devOpen; }

    // de keuzes (ook voor Instellingen › Ontwikkelaar); withConsole: met de knop naar de console (in het spel, niet op het startscherm)
    List<MItem> DevItems(bool withConsole)
    {
        string On(bool b) => b ? "aan" : "uit";
        var l = new List<MItem>();
        l.Add(new MItem { head = true, name = "Weergave" });
        l.Add(new MItem { name = "Vijfhoeken en vervorming", value = On(Dev.Pentagons), on = Dev.Pentagons, sub = "Oranje vijfhoeken met baken, de grond kleurt mee met de vervorming", act = () => Dev.Pentagons = !Dev.Pentagons });
        l.Add(new MItem { name = "Startplekken", value = On(Dev.Starts), on = Dev.Starts, sub = "De twintig startplekken met paal, vlag en bordje", act = () => Dev.Starts = !Dev.Starts });
        l.Add(new MItem { name = "Hoekpunten", value = On(Dev.Verts), on = Dev.Verts, sub = "Bij het terreingereedschap rond de muis", act = () => Dev.Verts = !Dev.Verts });
        l.Add(new MItem { name = "Vakinfo bij de muis", value = On(Dev.CellInfo), on = Dev.CellInfo, sub = "Nummer, hoogte, vervorming, stapel", act = () => Dev.CellInfo = !Dev.CellInfo });
        l.Add(new MItem { name = "Statistieken", value = On(Dev.Stats), on = Dev.Stats, sub = "Fps, vakken, leven, camera", act = () => Dev.Stats = !Dev.Stats });
        l.Add(new MItem { name = "Meldingen in beeld", value = On(Dev.Log), on = Dev.Log, sub = Dev.Errors + " fouten, " + Dev.Warnings + " waarschuwingen", act = () => Dev.Log = !Dev.Log });
        l.Add(new MItem { head = true, name = "Terreinproeven (niet onthouden)" });
        l.Add(new MItem { name = "Terrein door wegen", value = On(PlanetCity.RoadShape), on = PlanetCity.RoadShape, sub = "Uit: geen dijken onder wegen; hoger terrein wordt met een talud afgegraven", act = () => { PlanetCity.RoadShape = !PlanetCity.RoadShape; Dev.Redraw(); Say("Terrein door wegen " + On(PlanetCity.RoadShape)); } });
        l.Add(new MItem { name = "Talud langs wegen", value = On(PlanetCity.RoadSlopes), on = PlanetCity.RoadSlopes, sub = "Uit: de kant naast de weg bijna loodrecht in plaats van een schuin talud", act = () => { PlanetCity.RoadSlopes = !PlanetCity.RoadSlopes; Dev.Redraw(); Say("Talud langs wegen " + On(PlanetCity.RoadSlopes)); } });
        l.Add(new MItem { head = true, name = "Hulpmiddelen" });
        l.Add(new MItem { name = "Alles opnieuw tekenen", sub = "De hele planeet", act = () => { Dev.Redraw(); Say("Alles opnieuw getekend"); } });
        if (withConsole) l.Add(new MItem { name = "Console", value = "`", sub = "Dezelfde schakelaars in een venster, met de log", act = () => { menuOpen = false; devOpen = true; } });
        return l;
    }

    void DevGui(float W, float H)
    {
        if (Dev.CellInfo && hasHover && !walking && !Modal) CellInfoLabel();
        if (Dev.Stats) StatsPanel(W);
        if (Dev.Log) LogLines(W, H, 6, new Rect(G, H - 12f - 44f - 10f - 6 * 17f, Mathf.Min(720f, W - 2f * G), 6 * 17f));
        if (devOpen) Console(W, H);
    }

    void CellInfoLabel()
    {
        int i = hover; var c = Planet.Cells[i];
        string stack = PlanetCity.Cells.TryGetValue(i, out var l) && l.Count > 0 ? string.Join(", ", l) : "leeg";
        int links = 0; foreach (int j in PlanetCity.Partners(i)) links++;
        string t = "<b>#" + i + "</b>  " + (c.corners.Length == 5 ? "vijfhoek" : "zeshoek") + " · " + PlanetCity.G(i).ToString("0.00") + " m · vervorming " + c.distort.ToString("0.000")
                 + "\n" + stack + (links > 0 ? " · " + links + " verbinding" + (links == 1 ? "" : "en") : "");
        var m = Ui.Mouse; float w = Mathf.Max(Ui.Width(t.Split('\n')[0], Ui.T.Text, 11), Ui.Width(stack, Ui.T.Text, 11) + 90f) + 20f;
        var r = new Rect(Mathf.Min(m.x + 18f, Ui.W - w - 8f), Mathf.Max(8f, m.y - 54f), w, 40f);
        Ui.Box(r, new Color(0.02f, 0.05f, 0.1f, 0.92f), new Color(0.45f, 0.7f, 1f, 0.6f), 6f);
        Ui.Label(new Rect(r.x + 10f, r.y + 3f, w - 20f, 34f), t, Ui.T.Text, new Color(0.75f, 0.9f, 1f), TextAnchor.MiddleLeft, 11);
    }

    void StatsPanel(float W)
    {
        var L = PlanetLife.I; PlanetCity.LatLon(focus, out float lat, out float lon);
        string t = Mathf.RoundToInt(fps) + " fps  ·  " + PlanetCity.Cells.Count + " vakken  ·  "
                 + (L != null ? L.People.Count + " mensen, " + L.CarCount + " auto's, " + L.RiderCount + " fietsers  ·  " : "")
                 + "camera " + Mathf.RoundToInt(dist) + " m, " + Mathf.RoundToInt(pitch) + "°  ·  " + lat.ToString("0.0") + "°, " + lon.ToString("0.0") + "°"
                 + (Dev.Errors > 0 ? "  ·  <color=#F07A6A>" + Dev.Errors + " fouten</color>" : "");
        float w = Ui.Width(t, Ui.T.Text, 11) + 24f; var r = new Rect((W - w) * 0.5f, 12f + 48f + 8f, w, 24f);
        Ui.Box(r, new Color(0.02f, 0.05f, 0.1f, 0.88f), new Color(0.45f, 0.7f, 1f, 0.5f), 12f);
        Ui.Label(r, t, Ui.T.Text, new Color(0.75f, 0.9f, 1f), TextAnchor.MiddleCenter, 11);
    }

    void LogLines(float W, float H, int n, Rect r)
    {
        var lines = Dev.Lines; int from = Mathf.Max(0, lines.Count - n); float y = r.y + r.height - (lines.Count - from) * 17f;
        for (int k = from; k < lines.Count; k++)
        {
            var ln = lines[k]; Color col = ln.type == LogType.Warning ? new Color(1f, 0.8f, 0.4f) : ln.type == LogType.Log ? new Color(0.8f, 0.85f, 0.9f) : new Color(1f, 0.5f, 0.45f);
            Ui.Label(new Rect(r.x + 1f, y + 1f, r.width, 17f), ln.text, Ui.T.Text, new Color(0f, 0f, 0f, 0.7f), TextAnchor.MiddleLeft, 11);
            Ui.Label(new Rect(r.x, y, r.width, 17f), ln.text, Ui.T.Text, col, TextAnchor.MiddleLeft, 11);
            y += 17f;
        }
    }

    // het venster: links de schakelaars (zeshoekjes), rechts de log
    void Console(float W, float H)
    {
        float w = Mathf.Min(860f, W - 40f), h = Mathf.Clamp(H - 12f - 48f - 40f - 90f, 260f, 520f); var r = new Rect((W - w) * 0.5f, 12f + 48f + 40f, w, h);
        Ui.Box(r, new Color(0.02f, 0.05f, 0.1f, 0.95f), new Color(0.45f, 0.7f, 1f, 0.6f), 14f); Ui.Hit(r);
        float x = r.x + 18f, y = r.y + 14f;
        Ui.Tex(new Rect(x, y + 6f, 16f, 14.4f), Ui.Hex(new Color(0.45f, 0.7f, 1f), Color.clear, 0f));
        Ui.Label(new Rect(x + 24f, y, 300f, 28f), "ONTWIKKELAAR", Ui.T.Title, new Color(0.75f, 0.9f, 1f));
        Ui.Label(new Rect(x + 24f, y, w - 90f, 28f), "` sluit", Ui.T.Dim, null, TextAnchor.MiddleRight, 11);
        if (Ui.RoundButton(new Rect(r.xMax - 18f - 28f, y, 28f, 28f), "sluit")) devOpen = false;
        y += 40f; float lw = 330f;
        // de schakelaars: wat niet past schuift met het muiswiel (alleen hele regels, met een balkje rechts ervan)
        var items = DevItems(false); items.RemoveAll(m => m.head || m.note); float top = y, room = r.yMax - 12f - top; int fit = Mathf.Max(1, Mathf.FloorToInt((room + 4f) / 40f)), max = Mathf.Max(0, items.Count - fit);
        var lr0 = new Rect(x, top, lw + 14f, room); if (lr0.Contains(Ui.Mouse) && Inp.Scroll != 0f) devScroll -= Mathf.RoundToInt(Mathf.Sign(Inp.Scroll));
        devScroll = Mathf.Clamp(devScroll, 0, max);
        for (int k = devScroll; k < Mathf.Min(items.Count, devScroll + fit); k++) { var m = items[k]; if (HexRow(new Rect(x, y, lw, 36f), m.name, null, m.value, m.on)) m.act?.Invoke(); y += 40f; }
        if (max > 0)
        {
            var bar = new Rect(x + lw + 4f, top, 4f, fit * 40f - 4f); Ui.Box(bar, new Color(1f, 1f, 1f, 0.08f), Color.clear, 2f);
            float bh = bar.height * fit / items.Count; Ui.Box(new Rect(bar.x, bar.y + (bar.height - bh) * devScroll / max, 4f, bh), new Color(0.45f, 0.7f, 1f, 0.8f), Color.clear, 2f);
        }
        // rechts: de log
        var lr = new Rect(x + lw + 24f, r.y + 54f, w - lw - 60f, h - 70f);
        Ui.Box(lr, new Color(0f, 0f, 0f, 0.35f), Color.clear, 8f);
        Ui.Label(new Rect(lr.x + 10f, lr.y - 20f, lr.width, 18f), "LOG  <color=#A8B2AB>" + Dev.Errors + " fouten · " + Dev.Warnings + " waarschuwingen</color>", Ui.T.Tiny, new Color(0.75f, 0.9f, 1f), null, 10);
        if (Dev.Lines.Count == 0) Ui.Label(new Rect(lr.x + 10f, lr.y + 8f, lr.width - 20f, 18f), "Nog niets gemeld", Ui.T.Dim, null, null, 11);
        else LogLines(W, H, Mathf.FloorToInt((lr.height - 12f) / 17f), new Rect(lr.x + 10f, lr.y + 6f, lr.width - 20f, lr.height - 12f));
    }

    // opnieuw starten voor een andere planeetgrootte (Instellingen › Planeet, PlanetBuilder.Settings.cs): in de editor Play uit en meteen weer aan (bij het stoppen wordt de stad bewaard,
    // StartScreen.OnApplicationQuit); in een gebouwd spel kan dat niet, dan vragen we het spel zelf opnieuw te starten
    void RestartForSize()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.playModeStateChanged += AgainPlay; UnityEditor.EditorApplication.isPlaying = false;
#else
        Say("Sluit het spel en start het opnieuw: dan geldt de nieuwe grootte");
#endif
    }
#if UNITY_EDITOR
    static void AgainPlay(UnityEditor.PlayModeStateChange s)
    {
        if (s != UnityEditor.PlayModeStateChange.EnteredEditMode) return;
        UnityEditor.EditorApplication.playModeStateChanged -= AgainPlay; UnityEditor.EditorApplication.delayCall += () => UnityEditor.EditorApplication.isPlaying = true;
    }
#endif
}
