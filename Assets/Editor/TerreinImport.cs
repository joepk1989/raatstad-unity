using UnityEditor;
using UnityEngine;

// De bodemtextures in Resources/Terrein (gemaakt met Tools/terreintextures.py) zijn geen kleuren maar gegevens: elk kanaal is
// het reliëf van één bodemsoort. Dus lineair (geen sRGB), herhalend, met mipmaps en zonder kleurcompressie die kanalen mengt.
public class TerreinImport : AssetPostprocessor
{
    public override uint GetVersion() => 1;

    void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').Contains("/Resources/Terrein/")) return;
        var imp = (TextureImporter)assetImporter;
        imp.textureType = TextureImporterType.Default;
        imp.sRGBTexture = false;
        imp.alphaSource = TextureImporterAlphaSource.FromInput;
        imp.alphaIsTransparency = false;
        imp.wrapMode = TextureWrapMode.Repeat;
        imp.mipmapEnabled = true;
        imp.filterMode = FilterMode.Trilinear;
        imp.anisoLevel = 4;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
    }
}
