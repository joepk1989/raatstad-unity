using System.Collections.Generic;
using UnityEngine;

// Heidevelden over de hele bol (Joep, 14 november 2026: de weidedieren passen beter tussen de heide). Op lege grond buiten de
// startstad en haar bosgordel (22 vakken) komen vlekken "heide" (paarse struikjes, soms een berk of den, PlanetWild.cs) met
// grasveldjes ertussen, en velden droge heide ("droogheide": zandgrond, geelbruin gras, een den), uit vaste ruis: steeds hetzelfde patroon. Niet in het water, niet boven de boomgrens, niet op of naast een
// weg of pad, niet bij de polen en startplekken. Heide wijkt voor alles wat je bouwt (Place).
// Een nieuwe startstad krijgt de heide meteen (StartTown); een bestaande stad één keer bij het laden, alleen op lege vakken
// (HeathVer in de opslag), zodat wat je zelf gebouwd of weggehaald hebt blijft zoals het is.
public static partial class PlanetCity
{
    public const int HeathVersion = 2;   // 2: ook droge heide
    public static int HeathVer;
    public const int HeathFrom = 23;   // vanaf hoeveel vakken van de vlag (de groene rand van de startstad loopt tot 22)

    public static int AddHeath()
    {
        int was = HeathVer; HeathVer = HeathVersion;
        if (Planet.Cells == null) return 0;
        int s = StartCell >= 0 && StartCell < Planet.Cells.Length ? StartCell : -1;
        float townR = HeathFrom * 15.6f / Planet.R;   // als hoek
        int n = 0;
        for (int c = 0; c < Planet.Cells.Length; c++)
        {
            Vector3 d = Planet.Cells[c].dir;
            if (was == 1)   // een stad met de heide van versie 1: alleen een deel van die heide wordt droog, de rest blijft zoals hij is
            {
                if (Cells.TryGetValue(c, out var o) && o.Count == 1 && o[0] == "heide" && Dry(d) && (s < 0 || Vector3.Angle(d, Planet.Cells[s].dir) * Mathf.Deg2Rad >= townR)) { o[0] = "droogheide"; Touch(c); n++; }
                continue;
            }
            if (Cells.TryGetValue(c, out var l) && l.Count > 0) continue;
            if (Planet.IsPole(c)) continue;
            if (s >= 0 && Vector3.Angle(d, Planet.Cells[s].dir) * Mathf.Deg2Rad < townR) continue;
            bool nearStart = false; foreach (int st in Planet.Starts) if (Vector3.Angle(d, Planet.Cells[st].dir) * Mathf.Deg2Rad * Planet.R < 50f) nearStart = true;
            if (nearStart) continue;
            float h = Terrain(c); if (h < SeaLevel + 0.3f || h > TreeLine - 4f || Wet(c)) continue;
            bool walk = IsWalk(c) || IsHalf(c); foreach (int j in Planet.Cells[c].nb) if (IsWalk(j) || IsHalf(j)) walk = true;
            if (walk) continue;
            // grote velden met rafelige randen: grove ruis plus wat fijne
            float v = Noise(d, 1f / 95f, 311f) * 0.75f + Noise(d, 1f / 28f, 57f) * 0.25f;
            if (v < 0.46f) continue;
            if (Noise(d, 1f / 22f, 401f) > 0.74f) { if (Place(c, "park") == null) n++; continue; }   // bosjes in de heide
            if (Place(c, Dry(d) ? "droogheide" : "heide") == null) n++;
        }
        return n;
    }
    // een opgeslagen stad van voor de heide (of met oudere heide): één keer bijwerken; true als er iets veranderde
    public static bool UpgradeHeath()
    {
        if (HeathVer >= HeathVersion) return false;
        if (Cells.Count > 0 && !WildPlanet()) AddHeath();   // een lege stad blijft leeg, de Natuurplaneet heeft zijn eigen klimaat
        HeathVer = HeathVersion; return true;
    }
    // droge heide: eigen velden (andere ruis), vaak aan de rand van de natte heide
    static bool Dry(Vector3 d) => Noise(d, 1f / 60f, 913f) > 0.4f;   // het grootste deel droog, met vlekken donkere heide
    // een Natuurplaneet (PlanetWild.cs) heeft al zijn eigen klimaat: daar geen heidevelden bij
    static bool WildPlanet()
    {
        foreach (var kv in Cells) if (kv.Value.Count == 1 && (kv.Value[0] == "ijs" || kv.Value[0] == "woestijn" || kv.Value[0] == "palmen")) return true;
        return false;
    }
}
