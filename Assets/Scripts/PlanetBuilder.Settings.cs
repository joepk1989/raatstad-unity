using System.Collections.Generic;
using UnityEngine;

// Instellingen: één indeling in secties, dezelfde op het startscherm (tabbladen, StartScreen.cs) en in het raatmenu
// (Instellingen › sectie, PlanetBuilder.HexMenu.cs). Beide lezen SettingsSections, zodat ze niet uit elkaar kunnen lopen.
// Beeld, Geluid, Leven, Planeet, Uitleg en Ontwikkelaar (DevItems, PlanetBuilder.Dev.cs).
// Geluid, mensen, auto's, verkeerslichten, chaos en raster worden onthouden in PlayerPrefs: LoadSettings bij het starten,
// SaveSettings na elke keer dat de speler er een omzet (ook met G, de geluidsknop en het tijdpaneel). Draait er een
// automatische test (geen startscherm, HomeBoot), dan wordt niets gelezen of bewaard.
public partial class PlanetBuilder
{
    class SetSec { public string name, sub; public System.Func<List<MItem>> items; }
    int hexSec = -1;   // in het raatmenu: de open sectie van Instellingen (-1: de lijst met secties)
    int setTab;        // op het startscherm: het open tabblad

    static readonly float[] UiSizes = { 0.9f, 1f, 1.15f, 1.3f };
    static readonly int[] PlanetSizes = { 30, 46, 64, 92, 138 };   // M; 46 is het origineel (Planet.BaseM)
    static string OnOff(bool b) => b ? "aan" : "uit";
    static string Km(float m) => (m / 1000f).ToString("0.0").Replace('.', ',');
    // planeetgrootte als tekst: "46 (origineel) · 3,8 km rond"
    static string SizeName(int mm) => mm + (mm == Planet.BaseM ? " (origineel)" : mm < Planet.BaseM ? " (kleiner)" : " (groter)");
    static int WantSize { get { int w = PlayerPrefs.GetInt("raatstad-dev-planeet", Planet.BaseM); return System.Array.IndexOf(PlanetSizes, w) >= 0 ? w : Planet.BaseM; } }

    List<SetSec> SettingsSections()
    {
        bool game = !home;
        var l = new List<SetSec>();
        l.Add(new SetSec { name = "Beeld", sub = "Interface, terrein en raster", items = () => {
            int si = 0; for (int k = 1; k < UiSizes.Length; k++) if (Mathf.Abs(UiSizes[k] - Ui.Size) < Mathf.Abs(UiSizes[si] - Ui.Size)) si = k;
            return new List<MItem> {
                new MItem { name = "Interfacegrootte", sub = "Knoppen, tekst en vensters", opts = new[] { "90%", "100%", "115%", "130%" }, sel = si, pick = k => Ui.Size = UiSizes[k] },
                new MItem { name = "Terreindetail", sub = "Low poly: strakke vlakken. Medium en high poly: glooiend, 9 of 25 keer zoveel driehoeken",
                            opts = PlanetCity.SoilDetailNames, sel = PlanetCity.SoilDetail, pick = k => { PlanetCity.SoilDetail = k; Say("Terreindetail " + PlanetCity.SoilDetailNames[k]); } },
                new MItem { name = "Terreinlijnen", value = OnOff(PlanetCity.SoilWire), on = PlanetCity.SoilWire, sub = "De driehoeken van het terrein als dunne lijnen", act = () => { PlanetCity.SoilWire = !PlanetCity.SoilWire; Say("Terreinlijnen " + OnOff(PlanetCity.SoilWire)); } },
                new MItem { name = "Raster", value = OnOff(PlanetCity.GridOn), on = PlanetCity.GridOn, sub = "De randen van de vakken (G)", act = () => { if (game) ToggleGrid(); else { PlanetCity.GridOn = !PlanetCity.GridOn; SaveSettings(); } } } }; } });
        l.Add(new SetSec { name = "Geluid", sub = "Geluid en bouwmuziek", items = () => new List<MItem> {
            new MItem { name = "Geluid", value = OnOff(!Babble.Muted), on = !Babble.Muted, sub = "Stemmen, verkeer, natuur en effecten", act = () => { if (game) ToggleSound(); else { Babble.Muted = !Babble.Muted; Ambience.Muted = Babble.Muted; SaveSettings(); } } },
            new MItem { name = "Bouwmuziek", sub = "Muziek tijdens het bouwen; de ruimte heeft haar eigen muziek",
                        opts = BuildMusic.StyleNames, sel = BuildMusic.Style, pick = k => { BuildMusic.SetStyle(k); Say("Bouwmuziek: " + BuildMusic.StyleNames[k]); } } } });
        l.Add(new SetSec { name = "Leven", sub = "Mensen, auto's en verkeer", items = () => new List<MItem> {
            new MItem { name = "Mensen", value = OnOff(PlanetLife.PeopleOn), on = PlanetLife.PeopleOn, sub = "Voetgangers en fietsers", act = () => { PlanetLife.PeopleOn = !PlanetLife.PeopleOn; SaveSettings(); } },
            new MItem { name = "Auto's", value = OnOff(PlanetLife.CarsOn), on = PlanetLife.CarsOn, sub = "Verkeer op de wegen", act = () => { PlanetLife.CarsOn = !PlanetLife.CarsOn; SaveSettings(); } },
            new MItem { name = "Verkeerslichten", value = OnOff(PlanetCity.Lights), on = PlanetCity.Lights, sub = "Op kruispunten van grote wegen", act = () => { if (game) ToggleLights(); else { PlanetCity.Lights = !PlanetCity.Lights; SaveSettings(); } } },   // op het startscherm getekend bij het openen van een stad
            new MItem { name = "Totale chaos", value = OnOff(PlanetLife.Chaos), on = PlanetLife.Chaos, sub = "Racers, ontwijkers en jagers op de weg",
                        act = () => { PlanetLife.Chaos = !PlanetLife.Chaos; SaveSettings(); Say(PlanetLife.Chaos ? "Totale chaos: racers, ontwijkers en jagers op de weg" : "Netjes verkeer"); } } } });
        l.Add(new SetSec { name = "Planeet", sub = "Planeetgrootte", items = () => {
            int want = WantSize, wi = System.Array.IndexOf(PlanetSizes, want);
            var names = new string[PlanetSizes.Length]; for (int k = 0; k < names.Length; k++) names[k] = PlanetSizes[k] + (PlanetSizes[k] == Planet.BaseM ? " (origineel)" : "");
            float rond = 2f * Mathf.PI * Planet.BaseR * want / Planet.BaseM;
            var p = new List<MItem> {
                new MItem { name = "Planeetgrootte", sub = "Groter is minder kromming", opts = names, sel = wi,
                            pick = k => { int nx = PlanetSizes[k]; PlayerPrefs.SetInt("raatstad-dev-planeet", nx); PlayerPrefs.Save(); Say("Planeetgrootte " + SizeName(nx) + (nx != Planet.M ? ": geldt na opnieuw starten" : "")); } },
                new MItem { note = true, name = (10 * want * want + 2).ToString("N0").Replace(',', '.') + " vakken, " + Km(rond) + " km rond. Geldt pas na opnieuw starten" + (want != Planet.M ? " (nu " + Planet.M + ")" : "") + ".\nElke grootte heeft eigen steden; die van " + Planet.M + " blijven bewaard." } };
            if (want != Planet.M) p.Add(new MItem { name = "Nu opnieuw starten", value = SizeName(want), sub = game ? "Je stad wordt eerst bewaard; daarna het startscherm met de steden van deze grootte" : "Daarna het startscherm met de steden van deze grootte", act = RestartForSize });
            return p; } });
        l.Add(new SetSec { name = "Uitleg", sub = "Welkom en bediening", items = () => {
            bool welcomeOff = PlayerPrefs.GetInt("raatstad-welkom-uit", 0) == 1;
            var u = new List<MItem> { new MItem { name = "Uitleg bij een nieuwe stad", value = OnOff(!welcomeOff), on = !welcomeOff, sub = "Het welkomstvenster met hoe je bouwt, loopt en het menu vindt", act = () => PlayerPrefs.SetInt("raatstad-welkom-uit", welcomeOff ? 0 : 1) } };
            if (game) u.Add(new MItem { name = "Bediening", value = "?", sub = "Alle toetsen op een rij", act = () => { menuOpen = false; helpOpen = true; } });
            return u; } });
        l.Add(new SetSec { name = "Ontwikkelaar", sub = "Hulp bij het bouwen van het spel", items = () => DevItems(game) });
        return l;
    }

    // ---- onthouden tussen sessies
    static bool Pref(string k, bool d) => PlayerPrefs.GetInt("raatstad-" + k, d ? 1 : 0) == 1;
    static void Pref(string k, bool v, bool _) => PlayerPrefs.SetInt("raatstad-" + k, v ? 1 : 0);
    bool settingsLoaded;
    void LoadSettings()
    {
        if (!home) return;   // automatische tests: de standaard, niets uit een vorige sessie
        settingsLoaded = true;
        Babble.Muted = Ambience.Muted = !Pref("geluid", true);
        PlanetLife.PeopleOn = Pref("mensen", true); PlanetLife.CarsOn = Pref("autos", true); PlanetLife.Chaos = Pref("chaos", false);
        PlanetCity.Lights = Pref("verkeerslichten", true); PlanetCity.GridOn = Pref("raster", true);
    }
    void SaveSettings()
    {
        if (!settingsLoaded) return;
        Pref("geluid", !Babble.Muted, true); Pref("mensen", PlanetLife.PeopleOn, true); Pref("autos", PlanetLife.CarsOn, true); Pref("chaos", PlanetLife.Chaos, true);
        Pref("verkeerslichten", PlanetCity.Lights, true); Pref("raster", PlanetCity.GridOn, true); PlayerPrefs.Save();
    }

    // ---- het raatmenu: Instellingen toont eerst de secties, een klik erop de instellingen ervan (met Terug)
    List<MItem> SettingsMenuItems()
    {
        var secs = SettingsSections();
        if (hexSec >= 0 && hexSec < secs.Count)
        {
            var l = new List<MItem> { new MItem { name = "‹ Terug", sub = "Naar de secties van Instellingen", act = () => { hexSec = -1; hexSubT = Time.unscaledTime; } } };
            l.AddRange(secs[hexSec].items()); return l;
        }
        var s = new List<MItem>();
        for (int k = 0; k < secs.Count; k++) { int kk = k; s.Add(new MItem { name = secs[k].name + " ›", sub = secs[k].sub, act = () => { hexSec = kk; hexSubT = Time.unscaledTime; } }); }
        return s;
    }

    // ---- het startscherm: tabbladen bovenin, eronder de instellingen van het open tabblad
    static float SetRowH(MItem m) => m.head ? 24f : m.note ? 38f : m.opts != null ? 84f : m.sub != null ? 42f : 36f;
    void SettingsPanel(Rect r)
    {
        var secs = SettingsSections(); setTab = Mathf.Clamp(setTab, 0, secs.Count - 1);
        var items = secs[setTab].items(); float list = 0f; foreach (var m in items) list += SetRowH(m);
        r.width = 560f; r.x = Mathf.Min(r.x, Ui.W - r.width - 24f);
        r.height = 14f + 42f + 36f + 14f + list + 8f; r.y = Mathf.Max(16f, Mathf.Min(r.y, Ui.H - r.height - 40f)); Ui.Panel(r, true, 16f);   // zo hoog als het tabblad, binnen het scherm
        float x = r.x + 18f, iw = r.width - 36f, y = r.y + 14f;
        PanelHead(r, "INSTELLINGEN", ref y);
        var names = new string[secs.Count]; for (int k = 0; k < secs.Count; k++) names[k] = secs[k].name;
        int hit = Ui.Seg(new Rect(x, y, iw, 36f), names, setTab); if (hit >= 0 && hit != setTab) { setTab = hit; Sounds.Fx("click"); }
        y += 36f + 14f;
        foreach (var m in items)
        {
            float h = SetRowH(m);
            if (m.head) Ui.Label(new Rect(x + 2f, y + 6f, iw, 16f), m.name.ToUpper(), Ui.T.Tiny);
            else if (m.opts != null)   // naam en uitleg, eronder de keuzes naast elkaar
            {
                const float TS = 30f; Ui.Tex(new Rect(x + 4f, y + 21f - TS * 0.45f, TS, TS * 0.9f), Ui.Hex(new Color(0.07f, 0.09f, 0.12f, 0.92f), new Color(1f, 1f, 1f, 0.3f), 1.2f));
                float tx = x + 46f; Ui.Label(new Rect(tx, y + 4f, iw - 46f, 20f), m.name, Ui.T.Bold, null, null, 14);
                if (m.sub != null) Ui.Label(new Rect(tx, y + 24f, iw - 46f, 16f), m.sub, Ui.T.Dim, null, null, 11);
                int c = Ui.Seg(new Rect(tx, y + 44f, iw - 46f, 32f), m.opts, m.sel); if (c >= 0 && c != m.sel) { Sounds.Fx("click"); m.pick?.Invoke(c); }
            }
            else if (m.note) Ui.Label(new Rect(x + 46f, y, iw - 46f, 36f), m.name, Ui.T.Dim, null, TextAnchor.UpperLeft, 11);
            else if (HexRow(new Rect(x, y, iw, h - 6f), m.name, m.sub, m.value, m.on, m.act == null)) m.act?.Invoke();
            y += h;
        }
    }
}
