using System.IO;
using UnityEditor;

// Na een scriptwijziging (ook als Unity niet vooraan staat): Play stoppen, opnieuw compileren en Play weer starten,
// zodat de nieuwe code meteen in het spel te zien is. Stoppen slaat niets op; de stad komt bij de start uit het bestand.
// Aan/uit: menu Raatstad > Automatisch herstarten.
[InitializeOnLoad]
static class AutoHerstart
{
    const string FlagKey = "raatstad-herstart-play", OnKey = "raatstad-autoherstart";
    static FileSystemWatcher watcher; static volatile bool changed; static double settleAt;

    static bool On { get => EditorPrefs.GetBool(OnKey, true); set => EditorPrefs.SetBool(OnKey, value); }

    static AutoHerstart()
    {
        watcher = new FileSystemWatcher(Path.GetFullPath("Assets/Scripts"), "*.cs") { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName };
        FileSystemEventHandler mark = (s, e) => changed = true;
        watcher.Changed += mark; watcher.Created += mark; watcher.Deleted += mark; watcher.Renamed += (s, e) => changed = true;
        watcher.EnableRaisingEvents = true;
        EditorApplication.update += Tick;
        EditorApplication.delayCall += ResumePlay;   // na het hercompileren: Play weer aan als die aan stond
    }

    static void Tick()
    {
        if (!changed || !On) return;
        double now = EditorApplication.timeSinceStartup;
        if (settleAt == 0) { settleAt = now + 1.0; return; }   // even wachten tot alle bestanden geschreven zijn
        if (now < settleAt) return;
        if (EditorApplication.isPlaying) { SessionState.SetBool(FlagKey, true); EditorApplication.isPlaying = false; return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        changed = false; settleAt = 0;
        AssetDatabase.Refresh();
        EditorApplication.delayCall += ResumePlay;   // niets te compileren (geen echte wijziging): dan meteen weer spelen
    }

    static void ResumePlay()
    {
        if (!SessionState.GetBool(FlagKey, false)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += ResumePlay; return; }
        if (EditorUtility.scriptCompilationFailed) { SessionState.SetBool(FlagKey, false); return; }   // fouten: niet spelen, eerst de Console
        SessionState.SetBool(FlagKey, false);
        if (!EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = true;
    }

    [MenuItem("Raatstad/Automatisch herstarten")]
    static void Toggle() { On = !On; }
    [MenuItem("Raatstad/Automatisch herstarten", true)]
    static bool ToggleCheck() { Menu.SetChecked("Raatstad/Automatisch herstarten", On); return true; }
}
