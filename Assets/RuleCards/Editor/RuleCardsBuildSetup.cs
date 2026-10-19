using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Puts the RuleCards scenes in build order (Title → Hub → Stages → Repository) and drops the
/// leftover SampleScene. Applies once per machine automatically; re-run any time from
/// the menu: RuleCards > Apply Build Scene Order.
/// </summary>
[InitializeOnLoad]
public static class RuleCardsBuildSetup
{
    private const string AppliedKey = "RuleCards.BuildOrder.v1.";
    private const string RemovedScene = "Assets/Scenes/SampleScene.unity";
    private static readonly string[] Order = {
        "Assets/RuleCards/Levels/RuleCards_Title.unity",
        "Assets/RuleCards/Levels/ArchiveHub.unity",
        "Assets/RuleCards/Levels/Stage_01_KineticLink.unity",
        "Assets/RuleCards/Levels/Stage_02_InvertGravity.unity",
        "Assets/RuleCards/Levels/Stage_03_SolidifyAir.unity",
        "Assets/RuleCards/Levels/Repository_Final.unity"
    };

    static RuleCardsBuildSetup()
    {
        EditorApplication.delayCall += () => Apply(false);
    }

    [MenuItem("RuleCards/Apply Build Scene Order")]
    private static void ApplyFromMenu() { Apply(true); }

    private static void Apply(bool force)
    {
        string key = AppliedKey + Application.dataPath;
        if (!force && EditorPrefs.GetBool(key, false)) return;
        foreach (string path in Order)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null) continue;
            if (force) Debug.LogWarning("[RuleCards] Missing scene, build order not changed: " + path);
            return; // not imported yet; try again on the next reload
        }

        var scenes = new List<EditorBuildSettingsScene>();
        foreach (string path in Order) scenes.Add(new EditorBuildSettingsScene(path, true));
        foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            if (existing.path != RemovedScene && System.Array.IndexOf(Order, existing.path) < 0) scenes.Add(existing);
        EditorBuildSettings.scenes = scenes.ToArray();
        EditorPrefs.SetBool(key, true);
        Debug.Log("[RuleCards] Build order set: RuleCards_Title → ArchiveHub → Stages 1-3 → Repository_Final (SampleScene removed).");
    }
}
