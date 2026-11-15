using UnityEditor;

// Voertuigen uit Assets/Resources/Voertuigen (de K3-tank, origineel in Assets/ThirdParty/Military/K3NGMBT): normal maps als normal
// map importeren; het model op ware grootte (meters), zonder botsvormen (die maakt PlayerCar zelf), de materialen met hun naam uit
// het .mtl-bestand (ModelTank vervangt ze op naam door eigen URP-materialen).
class VoertuigenImport : AssetPostprocessor
{
    public override uint GetVersion() => 1;
    bool Mine => assetPath.Replace('\\', '/').Contains("Resources/Voertuigen/");
    void OnPreprocessTexture()
    {
        if (!Mine) return;
        var ti = (TextureImporter)assetImporter;
        if (assetPath.EndsWith("_N.png")) ti.textureType = TextureImporterType.NormalMap;
        ti.maxTextureSize = 2048;
    }
    void OnPreprocessModel()
    {
        if (!Mine) return;
        var mi = (ModelImporter)assetImporter;
        mi.globalScale = 1f; mi.useFileScale = false; mi.addCollider = false; mi.importCameras = false; mi.importLights = false;
        mi.animationType = ModelImporterAnimationType.None; mi.importAnimation = false;
        mi.isReadable = true;   // de rupsen buigen mee met de vering (TankRig.Suspend past de punten aan)
    }
}
