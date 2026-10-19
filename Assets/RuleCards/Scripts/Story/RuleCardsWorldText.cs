using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Restyles the scenes' 3D TextMesh signs at load: theme fonts (Cinzel titles, Alegreya clues,
/// Alegreya Sans SC labels) and a depth-tested shader so signs no longer show through walls.
/// Control-instruction boards are hidden; Glyph explains those instead (stage entry dialogue + H).
/// </summary>
public static class RuleCardsWorldText
{
    private static Shader shader;
    private static readonly Dictionary<Font, Material> materials = new Dictionary<Font, Material>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { shader = null; materials.Clear(); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Font.textureRebuilt -= OnFontRebuilt;
        Font.textureRebuilt += OnFontRebuilt;
        Restyle(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { Restyle(scene); }

    private static void OnFontRebuilt(Font font)
    {
        Material m;
        if (materials.TryGetValue(font, out m) && m != null && font.material != null) m.mainTexture = font.material.mainTexture;
    }

    /// <summary>Control boards ("E : anchor polarity …", "Aim + E : toggle …") — Glyph covers these now.</summary>
    public static bool IsControlBoard(string text)
    {
        return text.Contains("Hold Left Mouse") || text.Contains("Aim + E") || text.Contains("E : anchor");
    }

    private static Material MaterialFor(Font font)
    {
        Material m;
        if (materials.TryGetValue(font, out m) && m != null) return m;
        if (shader == null) shader = Resources.Load<Shader>("Shaders/RuleCardsWorldText");
        if (shader == null || font.material == null) return null;
        m = new Material(shader) { name = "WorldText_" + font.name, mainTexture = font.material.mainTexture };
        m.SetFloat("_Glow", 1.25f);
        materials[font] = m;
        return m;
    }

    /// <summary>Cinzel for titles, Alegreya for mixed-case clues, Alegreya Sans SC Bold for caps labels.</summary>
    public static Font FontFor(TextMesh tm, out bool prose)
    {
        string text = tm.text ?? "";
        bool title = tm.characterSize >= .1f;
        prose = !title && text != text.ToUpperInvariant();
        return title ? RuleCardsUI.Display : prose ? RuleCardsUI.Body : RuleCardsUI.CapsBold;
    }

    private static void Restyle(Scene scene)
    {
        if (!scene.IsValid() || !RuleCardsScenes.IsGameplay(scene.name)) return;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (TextMesh tm in root.GetComponentsInChildren<TextMesh>(true))
            {
                MeshRenderer r = tm.GetComponent<MeshRenderer>();
                if (r == null) continue;
                string text = tm.text ?? "";
                if (IsControlBoard(text)) { r.enabled = false; continue; }

                bool prose;
                Font font = FontFor(tm, out prose);
                if (font == null) continue;
                tm.font = font;
                tm.fontStyle = FontStyle.Normal;
                if (prose) tm.lineSpacing = Mathf.Max(tm.lineSpacing, 1.1f);
                Material m = MaterialFor(font);
                if (m != null) r.sharedMaterial = m;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
    }
}
