using System.IO;
using UnityEditor;
using UnityEngine;

// Menu Raatstad > Wegvoorbeeld: zet het verzoek klaar; AutoHerstart start Play en WegVoorbeeld bouwt de voorbeelden.
static class WegVoorbeeldMenu
{
    [MenuItem("Raatstad/Wegvoorbeeld")]
    static void Run() { File.WriteAllText(Path.Combine(Application.persistentDataPath, "wegvoorbeeld.txt"), ""); }
}
