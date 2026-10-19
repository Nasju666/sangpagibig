using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Bakes the world-sign restyle into the scenes, so the Scene view matches Play mode:
/// theme fonts, the depth-tested RuleCards/WorldText material, and control boards switched off
/// (Glyph explains those now). Menu: RuleCards > Story > Restyle World Signs (All Scenes).
/// </summary>
public static class RuleCardsWorldTextTool
{
    private const string MaterialFolder = "Assets/RuleCards/Art/Resources/Shaders";

    [MenuItem("RuleCards/Story/Restyle World Signs (All Scenes)")]
    public static void RestyleAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string current = SceneManager.GetActiveScene().path;
        int signs = 0, scenes = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/RuleCards/Levels" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!System.IO.File.ReadAllText(path).Contains("TextMesh:")) continue;
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int n = RestyleScene(scene);
            if (n > 0) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); scenes++; signs += n; }
        }
        if (!string.IsNullOrEmpty(current)) EditorSceneManager.OpenScene(current, OpenSceneMode.Single);
        AssetDatabase.SaveAssets();
        Debug.Log("[RuleCards] Restyled " + signs + " world signs in " + scenes + " scenes.");
    }

    private static int RestyleScene(Scene scene)
    {
        Shader shader = Shader.Find("RuleCards/WorldText");
        if (shader == null) { Debug.LogWarning("[RuleCards] RuleCards/WorldText shader missing."); return 0; }
        int n = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (TextMesh tm in root.GetComponentsInChildren<TextMesh>(true))
            {
                MeshRenderer r = tm.GetComponent<MeshRenderer>();
                if (r == null) continue;
                Undo.RecordObjects(new Object[] { tm, r, tm.gameObject }, "Restyle world sign");
                if (RuleCardsWorldText.IsControlBoard(tm.text ?? ""))
                {
                    tm.gameObject.SetActive(false);
                    n++;
                    continue;
                }
                bool prose;
                Font font = RuleCardsWorldText.FontFor(tm, out prose);
                if (font == null) continue;
                tm.font = font;
                tm.fontStyle = FontStyle.Normal;
                if (prose) tm.lineSpacing = Mathf.Max(tm.lineSpacing, 1.1f);
                r.sharedMaterial = MaterialAsset(shader, font);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                EditorUtility.SetDirty(tm);
                EditorUtility.SetDirty(r);
                n++;
            }
        return n;
    }

    private static Material MaterialAsset(Shader shader, Font font)
    {
        string path = MaterialFolder + "/WorldText_" + font.name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(shader) { name = "WorldText_" + font.name };
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = shader;
        if (font.material != null) m.mainTexture = font.material.mainTexture;
        m.SetFloat("_Glow", 1.25f);
        EditorUtility.SetDirty(m);
        return m;
    }
}
