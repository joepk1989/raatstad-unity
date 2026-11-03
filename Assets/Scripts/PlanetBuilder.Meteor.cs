using UnityEngine;

// Experimenteel: een meteoor (PlanetMeteor.cs) op het midden van het beeld, ook vanuit de ruimte. Undo pas bij de inslag:
// Push vlak voor de krater, zodat Ctrl+Z de krater en alles wat erin stond in één keer terugzet.
public partial class PlanetBuilder
{
    int meteorSize = 1;   // 0 klein (1 vak), 1 middel (7), 2 groot (19)
    static readonly int[] MeteorCells = { 1, 7, 19 };
    string MeteorSub => PlanetMeteor.SizeNames[meteorSize] + ": een krater van " + MeteorCells[meteorSize] + " vak" + (meteorSize > 0 ? "ken" : "") + " (Ctrl+Z terug)";

    void Meteor()
    {
        int c = Planet.Nearest(focus);
        PlanetMeteor.Launch(c, meteorSize, Cam, (cell, size) =>
        {
            Push();
            PlanetMeteor.Crater(cell, size, () => { PlanetCity.RebuildGrid(); Changed(); Sounds.Instead("meteoor"); Say("De wand is doorgebroken: het water stroomt de krater in"); });
            PlanetCity.RebuildGrid(); Changed(); Sounds.Instead("meteoor");   // geen bouwgeluid: de inslag klinkt al (PlanetMeteor.Sound.cs)
            Say("Inslag! Een krater van " + MeteorCells[size] + " vak" + (size > 0 ? "ken" : "") + " (Ctrl+Z zet alles terug)");
        });
        Say("Meteoor " + PlanetMeteor.SizeNames[meteorSize].ToLower() + " op weg naar het midden van het beeld…");
    }
}
