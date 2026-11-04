using System.Collections;
using UnityEngine;

// Experimenteel: een meteoor (PlanetMeteor.cs) op het midden van het beeld (menu, ook vanuit de ruimte) of op een aangewezen vak
// (MeteorAt in PlanetBuilder.Cursor.cs). Undo pas bij de inslag: Push vlak voor de krater, zodat Ctrl+Z de krater en alles wat
// erin stond in één keer terugzet. De inslag in stappen, zodat geen enkel beeld alles tegelijk doet: eerst alleen de effecten,
// een beeld later de undo-snapshot, daarna de krater; het tekenen gaat per beeld met een budget (RebuildTick) terwijl de kom
// wegzakt (PlanetMeteor morpht de terreinmesh), en pas als alles getekend is wordt de stad bewaard.
public partial class PlanetBuilder
{
    int meteorSize = 1;   // 0 klein (1 vak), 1 middel (7), 2 groot (19)
    static readonly int[] MeteorCells = { 1, 7, 19 };
    string MeteorSub => PlanetMeteor.SizeNames[meteorSize] + ": een krater van " + MeteorCells[meteorSize] + " vak" + (meteorSize > 0 ? "ken" : "") + " (Ctrl+Z terug)";

    void Meteor() => MeteorAt(Planet.Nearest(focus));

    // bij de inslag (PlanetMeteor roept dit aan): de krater een paar beelden later
    void MeteorHit(int cell, int size) => StartCoroutine(MeteorSteps(cell, size));
    IEnumerator MeteorSteps(int cell, int size)
    {
        yield return null; Push();
        yield return null;
        PlanetMeteor.Crater(cell, size, () => StartCoroutine(MeteorSettle(cell, "De wand is doorgebroken: het water stroomt de krater in")));
        yield return MeteorSettle(cell, "Inslag! Een krater van " + MeteorCells[size] + " vak" + (size > 0 ? "ken" : "") + " (Ctrl+Z zet alles terug)");
    }
    // zoals Changed, maar zonder meteen te tekenen en te bewaren: tekenen per beeld, bewaren als het klaar is
    IEnumerator MeteorSettle(int cell, string msg, bool save = true)
    {
        Sounds.After(Planet.Cells[cell].dir * Planet.R); Sounds.Instead("meteoor");   // geen bouwgeluid: de inslag klinkt al (PlanetMeteor.Sound.cs)
        PlanetCity.UserEdit(); beautyVer++; PlanetCity.RebuildSoon(cell); rebuilding = true; Say(msg);
        while (rebuilding) yield return null;
        if (save) { yield return null; PlanetCity.Save(); beautyT = -99f; }
    }

    // meteorenregen: een halve minuut lang meteoren rond het midden van het beeld, van alle kanten, de meeste klein. Eén Push aan het
    // begin, zodat Ctrl+Z de hele regen weghaalt (en ook de regen stopt); bewaren pas aan het eind
    bool showerOn; int showerUndo, showerHits;
    void MeteorShower() { if (showerOn) { Say("Er valt al een meteorenregen"); return; } StartCoroutine(ShowerSteps()); }
    bool ShowerAlive => showerOn && undo.Count >= showerUndo;
    IEnumerator ShowerSteps()
    {
        showerOn = true; showerHits = 0; Push(); showerUndo = undo.Count; int falling = 0;
        Vector3 c0 = focus.normalized, e = Vector3.Cross(c0, Mathf.Abs(c0.y) < 0.9f ? Vector3.up : Vector3.right).normalized, n = Vector3.Cross(c0, e);
        Say("Meteorenregen: een halve minuut lang (Ctrl+Z haalt alles weg)");
        for (float end = Time.time + 30f; Time.time < end && ShowerAlive;)
        {
            float a = Random.value * Mathf.PI * 2f, d = Mathf.Sqrt(Random.value) * 230f, r = Random.value;
            int cell = Planet.Nearest(c0 * Planet.R + (e * Mathf.Cos(a) + n * Mathf.Sin(a)) * d), size = r < 0.7f ? 0 : r < 0.95f ? 1 : 2;
            falling++;
            PlanetMeteor.Launch(cell, size, Cam, (c, s) => { falling--; if (ShowerAlive) StartCoroutine(ShowerHit(c, s)); }, true);
            yield return new WaitForSeconds(Random.Range(0.5f, 1.6f));
        }
        while (falling > 0 && ShowerAlive) yield return null;   // de laatste vallen nog
        while (rebuilding) yield return null;
        bool alive = ShowerAlive; showerOn = false;
        if (alive) { PlanetCity.Save(); beautyT = -99f; Say("Meteorenregen voorbij: " + showerHits + " inslagen (Ctrl+Z haalt alles weg)"); }
    }
    IEnumerator ShowerHit(int cell, int size)
    {
        yield return null; if (!ShowerAlive) yield break;
        showerHits++;
        PlanetMeteor.Crater(cell, size, () => StartCoroutine(MeteorSettle(cell, "De wand is doorgebroken: het water stroomt een krater in")));
        yield return MeteorSettle(cell, "Inslag " + showerHits, false);
    }
}
