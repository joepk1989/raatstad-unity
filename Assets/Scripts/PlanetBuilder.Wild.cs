using UnityEngine;

// Natuurplaneet (menu, zoals Hele planeet vol): de bol alleen met natuur (PlanetCity.WildSteps), achter de blauwdruk opgebouwd.
// Vervangt de stad die open is; Ctrl+Z zet hem terug.
public partial class PlanetBuilder
{
    void WildPlanet() { if (loading == null) StartCoroutine(WildRoutine()); }
    System.Collections.IEnumerator WildRoutine()
    {
        loading = "Natuurplaneet"; loadFrac = loadShown = 0f; catOpen = wheelOpen = menuOpen = false; PlanetBlueprint.I.Show();
        for (float t = 0f; t < 0.6f; t += Time.deltaTime) yield return null;
        Push(); var sw = System.Diagnostics.Stopwatch.StartNew();
        yield return PlanetCity.WildSteps((f, what) => { loadFrac = f * 0.3f; loading = what; }, Random.Range(1, 100000));
        loading = "Tekenen"; yield return PlanetCity.RebuildSteps(f => { loadFrac = 0.3f + f * 0.7f; PlanetBlueprint.I.Fade(1f - f); });
        PlanetCity.RebuildGrid(); PlanetCity.Save(); beautyT = -99f;
        PlanetBlueprint.I.Hide(); loading = null; Sounds.Instead("gen");
        Say("Natuurplaneet: zeeën, bergen, " + PlanetCity.WildRivers + " rivieren, ijs, woestijn en bos (" + (sw.ElapsedMilliseconds / 1000f).ToString("0") + " s; Ctrl+Z haalt alles weg)");
    }
}
