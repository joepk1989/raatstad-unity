using UnityEditor;

// De mensen (Assets/Resources/Mensen): figuren en animaties als Humanoid importeren. Zo past elke animatie op elk figuur (ook
// op de kinderen, die een ander skelet hebben) en haalt Unity de voorwaartse beweging uit de loopanimatie: de figuren lopen op
// de plek en de code verplaatst ze (anders schoof het lichaam een stap vooruit en sprong het aan het eind van de lus terug).
// De animaties herhalen zich als lus; de hoogte en de draaiing blijven in de houding.
class MensenImport : AssetPostprocessor
{
    public override uint GetVersion() => 2;   // ophogen als dit verandert: dan worden de bestanden opnieuw geïmporteerd
    bool Mine => assetPath.Replace('\\', '/').Contains("Resources/Mensen/");
    void OnPreprocessModel()
    {
        if (!Mine) return;
        var mi = (ModelImporter)assetImporter;
        mi.animationType = ModelImporterAnimationType.Human; mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
    }
    void OnPreprocessAnimation()
    {
        if (!Mine || !assetPath.Replace('\\', '/').Contains("/Animaties/")) return;
        var mi = (ModelImporter)assetImporter; var clips = mi.defaultClipAnimations;
        foreach (var c in clips)
        {
            c.loopTime = true; c.loopPose = false;
            c.lockRootRotation = true; c.keepOriginalOrientation = true;   // draaiing in de houding
            c.lockRootHeightY = true; c.keepOriginalPositionY = true;      // op en neer in de houding (voeten op de grond)
            c.lockRootPositionXZ = false;                                  // vooruit: eruit, als root motion (niet toegepast)
        }
        mi.clipAnimations = clips;
    }
}
