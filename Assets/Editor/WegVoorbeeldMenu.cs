using System.IO;
using UnityEditor;
using UnityEngine;

// Menu Raatstad > Wegvoorbeeld: zet het verzoek klaar; AutoHerstart start Play en WegVoorbeeld bouwt de voorbeelden.
static class WegVoorbeeldMenu
{
    static void Ask(string mode) { File.WriteAllText(Path.Combine(Application.persistentDataPath, "wegvoorbeeld.txt"), mode); }
    [MenuItem("Raatstad/Wegvoorbeeld/Wegbreedtes")] static void Roads() => Ask("");
    [MenuItem("Raatstad/Wegvoorbeeld/Fietspaden en oversteken")] static void Bikes() => Ask("fiets");
    [MenuItem("Raatstad/Wegvoorbeeld/Rotondes")] static void Roundabouts() => Ask("rotonde");
}
