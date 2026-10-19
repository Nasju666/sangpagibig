using UnityEditor;
using UnityEngine;

/// <summary>Import settings for Glyph's model and textures, and the GlyphMaterial the guide uses at runtime.</summary>
public sealed class RuleCardsGlyphImport : AssetPostprocessor
{
    public const string TextureFolder = "Assets/RuleCards/Art/Story/Glyph/";
    public const string ModelPath = "Assets/RuleCards/Art/Resources/Story/GlyphModel.fbx";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(TextureFolder)) return;
        RuleCardsGlyphSetup.Configure((TextureImporter)assetImporter, assetPath);
    }

    private void OnPreprocessModel()
    {
        if (assetPath != ModelPath) return;
        var mi = (ModelImporter)assetImporter;
        mi.materialImportMode = ModelImporterMaterialImportMode.None; // GlyphMaterial is assigned at runtime
        mi.importAnimation = false;
        mi.animationType = ModelImporterAnimationType.Generic;
        mi.importCameras = false;
        mi.importLights = false;
    }
}

[InitializeOnLoad]
public static class RuleCardsGlyphSetup
{
    private const string MaterialPath = "Assets/RuleCards/Art/Resources/Story/GlyphMaterial.mat";

    static RuleCardsGlyphSetup() { EditorApplication.delayCall += () => Ensure(false); }

    [MenuItem("RuleCards/Story/Rebuild Glyph Material")]
    private static void Rebuild() { Ensure(true); }

    /// <summary>Returns true when the importer already had the right settings.</summary>
    public static bool Configure(TextureImporter ti, string path)
    {
        bool ok = true;
        if (path.EndsWith("Glyph_Normal.png") && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ok = false; }
        if (path.EndsWith("Glyph_MetallicSmoothness.png") && ti.sRGBTexture) { ti.sRGBTexture = false; ok = false; }
        if (ti.maxTextureSize != 2048) { ti.maxTextureSize = 2048; ok = false; }
        return ok;
    }

    private static Texture2D Tex(string file)
    {
        string path = RuleCardsGlyphImport.TextureFolder + file;
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti != null && !Configure(ti, path)) ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static void Ensure(bool force)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!force && AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null) return;
        Texture2D baseMap = Tex("Glyph_BaseMap.png");
        if (baseMap == null) return; // model not imported yet
        Texture2D normal = Tex("Glyph_Normal.png");
        Texture2D metal = Tex("Glyph_MetallicSmoothness.png");
        Texture2D emission = Tex("Glyph_Emission.png");
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) { Debug.LogWarning("[RuleCards] URP Lit shader not found for GlyphMaterial."); return; }

        var m = new Material(shader) { name = "GlyphMaterial" };
        m.SetTexture("_BaseMap", baseMap);
        m.SetColor("_BaseColor", Color.white);
        if (normal != null) { m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
        if (metal != null)
        {
            m.SetTexture("_MetallicGlossMap", metal);
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetFloat("_Metallic", 1f);
            m.SetFloat("_Smoothness", .85f);
            m.SetFloat("_SmoothnessTextureChannel", 0f);
        }
        if (emission != null)
        {
            m.SetTexture("_EmissionMap", emission);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(1f, 1f, 1f) * 1.3f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        Material existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (existing != null) { existing.CopyPropertiesFromMaterial(m); existing.shaderKeywords = m.shaderKeywords; EditorUtility.SetDirty(existing); }
        else AssetDatabase.CreateAsset(m, MaterialPath);
        AssetDatabase.SaveAssets();
        Debug.Log("[RuleCards] GlyphMaterial ready (" + MaterialPath + ").");
    }
}
