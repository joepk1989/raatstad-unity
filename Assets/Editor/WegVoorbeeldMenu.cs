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
    [MenuItem("Raatstad/Wegvoorbeeld/Schuine wegen")] static void Diagonals() => Ask("schuin");
    [MenuItem("Raatstad/Wegvoorbeeld/Verkeer")] static void Traffic() => Ask("verkeer");
    [MenuItem("Raatstad/Teststad/Bouwen")] static void Town() => Ask("stad");
    [MenuItem("Raatstad/Teststad/Bouwen en verkeer meten")] static void TownMeasure() => Ask("stadmeten");
    [MenuItem("Raatstad/Teststad/Verkeer meten op de geladen stad")] static void MeasureHere() => Ask("meten");
    [MenuItem("Raatstad/Teststad/Startstad bouwen")] static void Start() => Ask("start");
    [MenuItem("Raatstad/Teststad/Auto's bekijken")] static void Cars() => Ask("autos");
    [MenuItem("Raatstad/Teststad/Startstad bouwen en verkeer meten")] static void StartMeasure() => Ask("startmeten");
}
