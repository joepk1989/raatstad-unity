using System.Collections.Generic;
using UnityEngine;

// Een verse krater (PlanetMeteor.cs) is niet groen: de kom is kale, donkere aarde met zwart verbrand gesteente, de rand
// lichtbruin opgeworpen puin, en daaromheen ligt een zwarte deken van roet en uitgeworpen gruis die naar buiten toe rafelt.
// Per vak een waarde in Scorch (bewaard in de opslag en in undo): 200 droge kom, 201 kom met water, 100 rand, 1-99 kans op roet
// per driehoekje. Een droge kom blijft droog, ook als hij dieper ligt dan het zeeniveau (DryPit, in Wet).
// NatureTag (PlanetNature.cs) vraagt hier de kleur van een driehoekje; water (SeaTag) gaat voor.
public static partial class PlanetCity
{
    public static readonly Dictionary<int, byte> Scorch = new Dictionary<int, byte>();
    public const byte ScorchFloor = 200, ScorchWet = 201, ScorchRim = 100;
    public static bool DryPit(int i) => Scorch.Count > 0 && Scorch.TryGetValue(i, out byte s) && (s == ScorchFloor || s == ScorchRim);
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetScorch() { Scorch.Clear(); }
    // het gemiddelde van de hoekpunten van vak i zoals ze getekend worden (een hoekpunt is het laagste van de drie vakken eromheen);
    // een meteoorkrater legt het midden van zijn vakken daar dichtbij, anders steekt een opgehoogd vak als een punt uit
    public static float CornerMean(int i) { int m = Planet.Cells[i].corners.Length; float s = 0f; for (int k = 0; k < m; k++) s += CornerBase(i, k); return s / m; }

    // de kleur van een driehoekje in een verschroeid vak; vlak (tag null) telt ook mee. Vast per driehoekje (uit de hoogtes), zodat het
    // patroon niet verspringt bij opnieuw tekenen
    static string ScorchTag(int i, string tag, float h0, float h1, float h2)
    {
        if (Scorch.Count == 0 || !Scorch.TryGetValue(i, out byte s) || Planet.IsPole(i)) return tag;
        uint hsh = (uint)i * 2654435761u ^ (uint)Mathf.RoundToInt(h0 * 37f) * 2246822519u ^ (uint)Mathf.RoundToInt(h1 * 41f) * 3266489917u ^ (uint)Mathf.RoundToInt(h2 * 43f) * 668265263u;
        hsh ^= hsh >> 15; hsh *= 2246822519u; hsh ^= hsh >> 13; int r = (int)(hsh % 100u);
        if (s >= ScorchFloor) return r < 30 ? "roet" : r < 45 ? "roetgrijs" : "kraterbodem";
        if (s >= ScorchRim) return r < 15 ? "roet" : r < 40 ? "kraterbodem" : "uitwerp";
        if (r < s) return r < s * 0.65f ? "roet" : "roetgrijs";
        return r < s + 15 ? "uitwerp" : tag;   // aan de rafelrand nog wat licht gruis op het gras
    }
    static bool ScorchColor(string k, out Color c)
    {
        switch (k)
        {
            case "kraterbodem": c = new Color(0.30f, 0.27f, 0.24f); return true;
            case "uitwerp": c = new Color(0.55f, 0.49f, 0.41f); return true;
            case "roet": c = new Color(0.12f, 0.11f, 0.10f); return true;
            case "roetgrijs": c = new Color(0.26f, 0.24f, 0.22f); return true;
            default: c = default; return false;
        }
    }
}
