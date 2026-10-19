using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// RuleCards > Build Escape Stage (Stage 04)
/// Copies LEVEL_Archive_Prototype_Blockout into Stage_04_Escape and turns it into the escape course:
/// removes the BLOCKOUT safety routes, adds the card-gated geometry, anchors, exit gate and the
/// EscapeStage system, moves the player to the Repository and adds the scene to the build.
/// The blockout itself is never modified. Re-run any time; it rebuilds Stage_04 from scratch.
/// All gap sizes were checked against ArchiveGravityPlayer (jump 1.3 m, sprint 7 m/s, gravity 22).
/// </summary>
public static class RuleCardsEscapeBuilder
{
    private const string Source = "Assets/RuleCards/Levels/LEVEL_Archive_Prototype_Blockout.unity";
    private const string Target = "Assets/RuleCards/Levels/Stage_04_Escape.unity";
    private const string MaterialDir = "Assets/RuleCards/Environment/Materials/";

    private static Dictionary<string, List<GameObject>> index;
    private static readonly List<string> missing = new List<string>();

    [MenuItem("RuleCards/Build Escape Stage (Stage 04)")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Build Escape Stage", "Stop Play mode first.", "OK");
            return;
        }
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Source) == null)
        {
            EditorUtility.DisplayDialog("Build Escape Stage", "Blockout scene not found:\n" + Source, "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        bool exists = AssetDatabase.LoadAssetAtPath<SceneAsset>(Target) != null;
        if (exists && !EditorUtility.DisplayDialog("Rebuild Stage 04?",
                "This replaces Stage_04_Escape.unity with a fresh build from the blockout.\nManual edits made inside Stage_04 will be lost.",
                "Rebuild", "Cancel")) return;
        if (exists) AssetDatabase.DeleteAsset(Target);
        if (!AssetDatabase.CopyAsset(Source, Target))
        {
            EditorUtility.DisplayDialog("Build Escape Stage", "Could not copy the blockout scene.", "OK");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(Target, OpenSceneMode.Single);
        BuildIndex(scene);
        missing.Clear();

        Material stone = Mat("MAT_ArchiveStone"), dark = Mat("MAT_ArchiveStoneDark"), cyan = Mat("MAT_CyanEmissive");
        Material airSolid = Mat("MAT_AirLightOnly_Stone"), airGhost = Mat("MAT_AirPlatform_Ghost");

        // ---- 1. Remove the blockout's safety routes so the cards are required.
        Disable("Blockout_Overview_Camera");
        Disable("Kinetic_BLOCKOUT_SafetyWalkway", "Kinetic_SafetyWalkway_Rails_L", "Kinetic_SafetyWalkway_Rails_R");
        Disable("Gravity_LowerFloor");
        for (int i = 0; i <= 4; i++) Disable("Gravity_FloatingSlab_" + i);
        for (int i = 0; i <= 8; i++) Disable("Gravity_BLOCKOUT_Stair_" + i);
        Disable("SolidAir_BLOCKOUT_PerimeterBypass", "SolidAir_Bypass_Rails_L", "SolidAir_Bypass_Rails_R");
        Disable("Hub_Bridge_East", "Hub_EastBridge_Rails_L", "Hub_EastBridge_Rails_R");
        Disable("Entry_Floor");
        // Ghost steps become visual hints only, moved onto the line a player actually hops along
        // (3.8 m Air reach from the Destination edge at z 166 toward the Start edge at z 144).
        float[] hintZ = { 162.2f, 157f, 151.8f, 146.6f };
        for (int i = 0; i <= 5; i++)
        {
            foreach (GameObject go in Find("SolidAir_GhostStep_" + i))
            {
                var c = go.GetComponent<Collider>(); if (c != null) c.enabled = false;
                var r = go.GetComponent<Renderer>(); if (r != null && airGhost != null) r.sharedMaterial = airGhost;
                if (i < hintZ.Length) go.transform.position = new Vector3(25f, 9.25f, hintZ[i]); else go.SetActive(false);
            }
            foreach (GameObject go in Find("SolidAir_GlowNode_" + i))
            {
                var c = go.GetComponent<Collider>(); if (c != null) c.enabled = false;
                if (i < hintZ.Length) go.transform.position = new Vector3(25f, 9.6f, hintZ[i]); else go.SetActive(false);
            }
        }

        // ---- 2. New geometry.
        var root = new GameObject("ESCAPE_Stage04").transform;
        var geo = Child(root, "Geometry");
        // Repository: link the circular floor to its approach.
        Box(geo, "Escape_R_Link", new Vector3(7.5f, 9f, 187.5f), new Vector3(10f, 1f, 4f), stone, true);
        // Gravity: bulkheads seal the floor; ceilings carry you over them upside-down.
        Box(geo, "Escape_G_Bulkhead1", new Vector3(34f, 5.15f, 103.5f), new Vector3(24f, 14.3f, 1f), dark, false);
        Box(geo, "Escape_G_Ceiling1", new Vector3(34f, 15.5f, 105f), new Vector3(16f, 1f, 14f), stone, true);
        Box(geo, "Escape_G_Island", new Vector3(34f, 5.5f, 96.25f), new Vector3(16f, 1f, 12.5f), stone, true);
        Box(geo, "Escape_G_Bulkhead2", new Vector3(34f, 4.15f, 88.5f), new Vector3(24f, 12.5f, 1f), dark, false);
        Box(geo, "Escape_G_Ceiling2", new Vector3(34f, 13.5f, 85f), new Vector3(16f, 1f, 18f), stone, true);
        Box(geo, "Escape_G_Landing", new Vector3(34f, 1.5f, 80f), new Vector3(16f, 1f, 12f), stone, true);
        Box(geo, "Escape_G_CeilingGlyph1", new Vector3(34f, 14.97f, 100f), new Vector3(14f, .05f, .3f), cyan, false, false);
        Box(geo, "Escape_G_CeilingGlyph2", new Vector3(34f, 12.97f, 91f), new Vector3(14f, .05f, .3f), cyan, false, false);
        // Kinetic: a raised pillar in the gap; both hops need a link.
        Box(geo, "Escape_K_Pillar", new Vector3(39f, -.75f, 54.75f), new Vector3(6f, 12.5f, 2.5f), stone, true);
        // Hub combo: the east bridge has broken. Solidify Air out over the gap (air only holds east of x=17),
        // then Kinetic Link to the anchor above the landing (it only reaches 15 m, so you must build out first).
        Box(geo, "Escape_H_BridgeEast", new Vector3(29f, 1.5f, 38f), new Vector3(8f, 1f, 5f), stone, true);
        Box(geo, "Escape_H_EastLanding", new Vector3(5.5f, .5f, 37f), new Vector3(5f, 1f, 8f), stone, true);
        // Entry: the floor's middle fell away; take the ceiling.
        Box(geo, "Escape_E_FloorFar", new Vector3(0f, 0f, -28f), new Vector3(16f, 1f, 12f), stone, false);
        Box(geo, "Escape_E_FloorNear", new Vector3(0f, 0f, -6f), new Vector3(16f, 1f, 8f), stone, true);
        Box(geo, "Escape_E_CeilingGlyph", new Vector3(0f, 9.47f, -16f), new Vector3(.3f, .05f, 12f), cyan, false, false);

        // Existing floors that crumble once the collapse passes them.
        string[] crumble = {
            "Repository_InnerFloor", "Repository_MainFloor", "Repository_Approach", "Nav_SolidAirToRepository_Connector",
            "SolidAir_Destination", "SolidAir_Start", "SolidAir_Approach", "Gravity_HighPlatform", "Gravity_Connector",
            "Kinetic_Destination_Platform", "Kinetic_Start_Platform", "Hub_Central_Platform", "Nav_EntryToHub_Connector", "Hub_Approach"
        };
        foreach (string n in crumble)
            foreach (GameObject go in Find(n))
                if (go.GetComponent<EscapeCrumble>() == null) go.AddComponent<EscapeCrumble>();

        // ---- 3. Kinetic anchors.
        var anchors = Child(root, "Anchors");
        Anchor(anchors, "Escape_K_AnchorA", new Vector3(39f, 9f, 54.75f), new Vector3(39f, 6.6f, 54.75f), cyan);
        Anchor(anchors, "Escape_K_AnchorB", new Vector3(39f, 6.5f, 43f), new Vector3(39f, 3.1f, 41.5f), cyan);
        Anchor(anchors, "Escape_H_AnchorC", new Vector3(5.5f, 5.6f, 37f), new Vector3(5.5f, 2.1f, 37f), cyan).maxRange = 15f;

        // ---- 4. Exit gate at the far end of the entry chamber.
        var exit = Child(root, "Exit");
        Box(exit, "Escape_Exit_PostL", new Vector3(-3.6f, 3.5f, -32f), new Vector3(.6f, 6f, .6f), cyan, false);
        Box(exit, "Escape_Exit_PostR", new Vector3(3.6f, 3.5f, -32f), new Vector3(.6f, 6f, .6f), cyan, false);
        Box(exit, "Escape_Exit_Lintel", new Vector3(0f, 6.7f, -32f), new Vector3(7.8f, .5f, .6f), cyan, false);
        Box(exit, "Escape_Exit_Glow", new Vector3(0f, 3.5f, -32.8f), new Vector3(6.6f, 6f, .1f), cyan, false, false);
        var exitLight = new GameObject("Escape_Exit_Light").AddComponent<Light>();
        exitLight.transform.SetParent(exit, false);
        exitLight.transform.position = new Vector3(0f, 3.5f, -30f);
        exitLight.type = LightType.Point;
        exitLight.color = new Color(.4f, 1f, 1f);
        exitLight.range = 14f;
        exitLight.intensity = 3f;

        // ---- 4b. Environment: atmosphere, readable edges, section gates, landmarks, set dressing.
        BuildEnvironment(root, stone, dark, cyan);

        // ---- 5. Player starts in the Repository.
        CharacterController player = Object.FindObjectOfType<CharacterController>(true);
        Vector3 spawn = new Vector3(0f, 10.7f, 199f);
        const float spawnYaw = 148.7f;
        if (player != null)
        {
            Transform t = player.transform;
            t.SetPositionAndRotation(spawn, Quaternion.Euler(0f, spawnYaw, 0f));
            if (PrefabUtility.IsPartOfPrefabInstance(t)) PrefabUtility.RecordPrefabInstancePropertyModifications(t);
        }
        else missing.Add("player (CharacterController)");

        // ---- 6. Escape system.
        var system = new GameObject("Escape_System");
        system.transform.SetParent(root, false);
        var stage = system.AddComponent<EscapeStage>();
        stage.route = new[] {
            new Vector3(0f, 10.7f, 204f), new Vector3(0f, 10.7f, 199f), new Vector3(3f, 10.6f, 191f), new Vector3(8f, 10.6f, 187.5f),
            new Vector3(13f, 10.6f, 181f), new Vector3(16f, 10.6f, 175f), new Vector3(25f, 10.6f, 170f), new Vector3(25f, 10.6f, 155f),
            new Vector3(25f, 10.6f, 140f), new Vector3(25f, 10.6f, 124f), new Vector3(34f, 10.6f, 112f), new Vector3(34f, 14f, 106f),
            new Vector3(34f, 7.1f, 99f), new Vector3(34f, 7.1f, 93f), new Vector3(34f, 12f, 87f), new Vector3(34f, 3.1f, 80f),
            new Vector3(36f, 3.1f, 70f), new Vector3(39f, 3.1f, 61f), new Vector3(39f, 6.6f, 54.75f), new Vector3(39f, 3.1f, 41.5f),
            new Vector3(29f, 3.1f, 38f), new Vector3(20f, 3.1f, 38f), new Vector3(12f, 3.1f, 38f), new Vector3(6f, 2f, 37f),
            new Vector3(0f, 1.3f, 30f), new Vector3(0f, 1.6f, 18f), new Vector3(0f, 1.6f, 4f), new Vector3(0f, 1.6f, -6f),
            new Vector3(0f, 8.5f, -16f), new Vector3(0f, 1.6f, -27f), new Vector3(0f, 1.6f, -32f)
        };
        stage.pace = new[] {
            1f, 1f, 1f, 1f, 1f, 1f, .55f, .55f, .8f, .8f,
            .55f, .55f, .55f, .55f, .55f, 1f, 1f, .6f, .6f, 1f,
            .6f, .6f, 1f, 1f, 1f, 1f, 1f, .6f, .6f, 1f
        };
        stage.checkpoints = new[] {
            Cp("The Repository", spawn, spawnYaw),
            Cp("Solidify Air", new Vector3(25f, 10.7f, 170f), 180f),
            Cp("High Platform", new Vector3(34f, 10.7f, 112f), 180f),
            Cp("Gravity Island", new Vector3(34f, 7.2f, 95f), 180f),
            Cp("Gravity Landing", new Vector3(34f, 3.2f, 80f), 180f),
            Cp("Kinetic Gap", new Vector3(39f, 3.2f, 62f), 180f),
            Cp("Kinetic Platform", new Vector3(39f, 3.2f, 41.5f), 270f),
            Cp("Central Hub", new Vector3(0f, 1.4f, 30f), 180f),
            Cp("Entry Chamber", new Vector3(0f, 1.6f, -5f), 180f)
        };
        stage.zones = new[] {
            Zone("SOLIDIFY AIR", "The bridge has dissolved. Right-click to fix the air into a platform. Three at a time; old ones fade.",
                new Vector3(25f, 10f, 149f), new Vector3(28f, 30f, 62f), 4),
            Zone("INVERT GRAVITY", "Walls seal the floor. Right-click while standing to fall onto the ceiling, cross over, then flip back down.",
                new Vector3(34f, 8f, 95f), new Vector3(28f, 36f, 46f), 2),
            Zone("KINETIC LINK", "Aim at a pink anchor and right-click to be pulled across the gap.",
                new Vector3(40.5f, 5f, 50f), new Vector3(15f, 30f, 44f), 1),
            Zone("AIR + KINETIC", "The bridge is gone. Build out over the gap, then link to the far anchor.",
                new Vector3(25f, 3f, 38f), new Vector3(16f, 16f, 12f), 5),
            Zone("INVERT GRAVITY", "The entry floor has fallen away. Take the ceiling.",
                new Vector3(0f, 4f, -18f), new Vector3(18f, 16f, 34f), 2)
        };
        stage.exitCenter = new Vector3(0f, 2f, -31.5f);
        stage.exitSize = new Vector3(7f, 4f, 2.5f);
        stage.cardTemplate = cyan;
        stage.airSolidMaterial = airSolid != null ? airSolid : stone;
        stage.airGhostMaterial = airGhost;
        stage.collapseTemplate = cyan;
        stage.particleMaterial = EscapeMaterial("MAT_Escape_Particles", cyan, Color.white, 0f, true);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AddToBuild();
        Selection.activeObject = system;
        EditorGUIUtility.PingObject(system);

        string report = "[RuleCards] Stage_04_Escape built from the blockout and added to the build after Repository_Final.";
        if (missing.Count > 0) report += "\nNot found (skipped): " + string.Join(", ", missing.ToArray());
        if (missing.Count > 0) Debug.LogWarning(report); else Debug.Log(report);
    }

    // ------------------------------------------------------------------ helpers
    private static void BuildIndex(Scene scene)
    {
        index = new Dictionary<string, List<GameObject>>();
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                List<GameObject> list;
                if (!index.TryGetValue(t.name, out list)) index[t.name] = list = new List<GameObject>();
                list.Add(t.gameObject);
            }
    }

    private static List<GameObject> Find(string name)
    {
        List<GameObject> list;
        if (index.TryGetValue(name, out list)) return list;
        missing.Add(name);
        return new List<GameObject>();
    }

    private static void Disable(params string[] names)
    {
        foreach (string n in names) foreach (GameObject go in Find(n)) go.SetActive(false);
    }

    private static Material Mat(string name)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + name + ".mat");
        if (m == null) missing.Add(name + ".mat");
        return m;
    }

    private static Transform Child(Transform parent, string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    private static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material material, bool crumble, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        go.transform.localScale = size;
        if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        if (crumble) go.AddComponent<EscapeCrumble>();
        return go;
    }

    private static EscapeAnchor Anchor(Transform parent, string name, Vector3 position, Vector3 landing, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(45f, 0f, 45f);
        go.transform.localScale = Vector3.one * .8f;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
        var anchor = go.AddComponent<EscapeAnchor>();
        anchor.landingPoint = landing;
        return anchor;
    }

    private static EscapeCheckpoint Cp(string label, Vector3 position, float yaw)
    {
        return new EscapeCheckpoint { label = label, position = position, yaw = yaw };
    }

    private static EscapeZone Zone(string label, string hint, Vector3 center, Vector3 size, int mask)
    {
        return new EscapeZone { label = label, hint = hint, center = center, size = size, cardMask = mask };
    }

    // ------------------------------------------------------------------ environment
    private const string EscapeMaterialDir = MaterialDir + "Escape/";
    private static readonly Color Cyan = new Color(.3f, 1f, 1f), Pink = new Color(1f, .3f, .85f), Violet = new Color(.6f, .5f, 1f), Gold = new Color(1f, .85f, .4f);

    private static void BuildEnvironment(Transform root, Material stone, Material dark, Material cyan)
    {
        var env = Child(root, "Environment");

        // Atmosphere (EscapeStage re-applies the same values at runtime).
        RenderSettings.skybox = null;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = new Color(.035f, .03f, .07f);
        RenderSettings.fogDensity = .0075f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.22f, .25f, .42f);
        RenderSettings.ambientEquatorColor = new Color(.15f, .12f, .26f);
        RenderSettings.ambientGroundColor = new Color(.05f, .045f, .08f);
        foreach (Light l in Object.FindObjectsOfType<Light>(true))
            if (l.type == LightType.Directional) { l.color = new Color(.55f, .65f, 1f); l.intensity = .6f; }

        Material trimCyan = EscapeMaterial("MAT_Escape_Trim_Cyan", cyan, Cyan, 1.1f, false);
        Material trimPink = EscapeMaterial("MAT_Escape_Trim_Pink", cyan, Pink, 1.1f, false);
        Material trimViolet = EscapeMaterial("MAT_Escape_Trim_Violet", cyan, Violet, 1.1f, false);
        Material trimGold = EscapeMaterial("MAT_Escape_Trim_Gold", cyan, Gold, 1.1f, false);
        Material beacon = EscapeMaterial("MAT_Escape_Beacon", cyan, Cyan, 1.8f, false);
        Material abyss = EscapeMaterial("MAT_Escape_Abyss", dark, new Color(.06f, .01f, .08f), .6f, false);

        // Glowing edges on every walkable surface, coloured by the card that section needs.
        string[] floors = {
            "Repository_Approach", "Nav_SolidAirToRepository_Connector", "SolidAir_Destination", "SolidAir_Start", "SolidAir_Approach",
            "Gravity_HighPlatform", "Gravity_Connector", "Kinetic_Destination_Platform", "Kinetic_Start_Platform",
            "Hub_Central_Platform", "Nav_EntryToHub_Connector", "Hub_Approach",
            "Escape_R_Link", "Escape_G_Island", "Escape_G_Landing", "Escape_K_Pillar", "Escape_H_BridgeEast", "Escape_H_EastLanding",
            "Escape_E_FloorFar", "Escape_E_FloorNear"
        };
        foreach (string n in floors)
            foreach (GameObject go in FindAny(n)) Trim(go, false, TrimFor(go.transform.position, trimCyan, trimPink, trimViolet, trimGold));
        foreach (string n in new[] { "Escape_G_Ceiling1", "Escape_G_Ceiling2" })
            foreach (GameObject go in FindAny(n)) Trim(go, true, trimViolet);

        // Section gates: an arch in the card's colour where each card section begins.
        Gate(env, "Gate_SolidifyAir", new Vector3(17.5f, 9.5f, 175f), Vector3.forward, 6.4f, 4.5f, trimGold, Gold);
        Gate(env, "Gate_InvertGravity", new Vector3(25f, 9.5f, 118f), Vector3.right, 6.4f, 4.5f, trimViolet, Violet);
        Gate(env, "Gate_KineticLink", new Vector3(34f, 2f, 72f), Vector3.right, 6.2f, 4.2f, trimPink, Pink);
        Gate(env, "Gate_HubCombo", new Vector3(33.6f, 2f, 38f), Vector3.forward, 6.4f, 4.2f, trimGold, Gold);
        Box(env, "Gate_Entry_GlowL", new Vector3(-4.42f, 2.5f, -1.8f), new Vector3(.08f, 5f, .9f), trimViolet, false, false);
        Box(env, "Gate_Entry_GlowR", new Vector3(4.42f, 2.5f, -1.8f), new Vector3(.08f, 5f, .9f), trimViolet, false, false);
        Box(env, "Gate_Entry_GlowTop", new Vector3(0f, 4.97f, -1.8f), new Vector3(9f, .06f, .9f), trimViolet, false, false);

        // Exit beacon: a tall beam visible from the whole route, so you always know where "out" is.
        var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beam.name = "Landmark_ExitBeacon";
        beam.transform.SetParent(env, false);
        beam.transform.position = new Vector3(0f, 30f, -36f);
        beam.transform.localScale = new Vector3(1.2f, 30f, 1.2f);
        Object.DestroyImmediate(beam.GetComponent<Collider>());
        beam.GetComponent<Renderer>().sharedMaterial = beacon;
        var beamLight = new GameObject("Landmark_ExitBeaconLight").AddComponent<Light>();
        beamLight.transform.SetParent(env, false);
        beamLight.transform.position = new Vector3(0f, 12f, -36f);
        beamLight.type = LightType.Point;
        beamLight.color = Cyan;
        beamLight.range = 30f;
        beamLight.intensity = 2.5f;

        // A faint glowing abyss far below, so pits read as deep instead of empty.
        Box(env, "Abyss_Glow", new Vector3(20f, -45f, 90f), new Vector3(520f, 1f, 640f), abyss, false, false);

        // Floating debris around the big voids (deterministic, never on the route).
        var debris = Child(env, "FloatingDebris");
        var rnd = new System.Random(4242);
        Bounds[] volumes = {
            new Bounds(new Vector3(25f, 8f, 155f), new Vector3(46f, 34f, 44f)),   // air pit
            new Bounds(new Vector3(34f, 6f, 98f), new Vector3(52f, 36f, 44f)),    // gravity void
            new Bounds(new Vector3(39f, 0f, 51f), new Vector3(40f, 26f, 18f)),    // kinetic gap
            new Bounds(new Vector3(18f, -4f, 30f), new Vector3(48f, 20f, 40f))    // hub underside
        };
        Bounds[] keepClear = {
            new Bounds(new Vector3(25f, 10f, 155f), new Vector3(16f, 10f, 46f)),
            new Bounds(new Vector3(34f, 8f, 95f), new Vector3(22f, 22f, 46f)),
            new Bounds(new Vector3(39f, 5f, 51f), new Vector3(16f, 14f, 30f)),
            new Bounds(new Vector3(18f, 3f, 37f), new Vector3(36f, 10f, 14f)),
            new Bounds(new Vector3(0f, 3f, 25f), new Vector3(12f, 10f, 60f))
        };
        int placed = 0;
        for (int attempt = 0; attempt < 600 && placed < 80; attempt++)
        {
            Bounds v = volumes[attempt % volumes.Length];
            var p = new Vector3(Lerp(rnd, v.min.x, v.max.x), Lerp(rnd, v.min.y, v.max.y), Lerp(rnd, v.min.z, v.max.z));
            bool blocked = false;
            foreach (Bounds k in keepClear) if (k.Contains(p)) { blocked = true; break; }
            if (blocked) continue;
            bool shard = rnd.NextDouble() < .18;
            float s = shard ? Lerp(rnd, .15f, .45f) : Lerp(rnd, .5f, 2.6f);
            var piece = Box(debris, shard ? "Debris_Shard" : "Debris_Block", p,
                shard ? new Vector3(s * .4f, s * 2.2f, s * .4f) : new Vector3(s, s * Lerp(rnd, .4f, 1f), s * Lerp(rnd, .6f, 1.4f)),
                shard ? trimCyan : (rnd.NextDouble() < .5 ? stone : dark), false, false);
            piece.transform.rotation = Quaternion.Euler(Lerp(rnd, 0f, 360f), Lerp(rnd, 0f, 360f), Lerp(rnd, 0f, 360f));
            var drift = piece.AddComponent<EscapeDrift>();
            drift.spin = new Vector3(Lerp(rnd, -12f, 12f), Lerp(rnd, -18f, 18f), Lerp(rnd, -12f, 12f));
            drift.bobHeight = Lerp(rnd, .15f, .7f);
            drift.bobSpeed = Lerp(rnd, .2f, .7f);
            placed++;
        }
    }

    private static Material TrimFor(Vector3 p, Material cyan, Material pink, Material violet, Material gold)
    {
        // Same boxes as the card zones (air, gravity, kinetic, hub combo, entry).
        if (In(p, new Vector3(25f, 10f, 149f), new Vector3(28f, 30f, 62f))) return gold;
        if (In(p, new Vector3(34f, 8f, 95f), new Vector3(28f, 36f, 46f))) return violet;
        if (In(p, new Vector3(40.5f, 5f, 50f), new Vector3(15f, 30f, 44f))) return pink;
        if (In(p, new Vector3(25f, 3f, 38f), new Vector3(16f, 16f, 12f))) return gold;
        if (In(p, new Vector3(0f, 4f, -18f), new Vector3(18f, 16f, 34f))) return violet;
        return cyan;
    }

    private static bool In(Vector3 p, Vector3 c, Vector3 s)
    {
        return Mathf.Abs(p.x - c.x) <= s.x * .5f && Mathf.Abs(p.y - c.y) <= s.y * .5f && Mathf.Abs(p.z - c.z) <= s.z * .5f;
    }

    /// <summary>Thin emissive strips along the four top (or bottom, for ceilings) edges of an axis-aligned box.</summary>
    private static void Trim(GameObject box, bool underside, Material material)
    {
        var col = box.GetComponent<BoxCollider>();
        if (col == null || material == null) return;
        if (Quaternion.Angle(box.transform.rotation, Quaternion.identity) > 1f) return;
        var mf = box.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null || mf.sharedMesh.name != "Cube") return; // cylinders etc. keep their look
        Bounds b = col.bounds;
        float y = underside ? b.min.y - .03f : b.max.y + .03f;
        const float w = .14f, h = .06f;
        var strips = new[] {
            new Bounds(new Vector3(b.center.x, y, b.min.z + w * .5f), new Vector3(b.size.x, h, w)),
            new Bounds(new Vector3(b.center.x, y, b.max.z - w * .5f), new Vector3(b.size.x, h, w)),
            new Bounds(new Vector3(b.min.x + w * .5f, y, b.center.z), new Vector3(w, h, b.size.z)),
            new Bounds(new Vector3(b.max.x - w * .5f, y, b.center.z), new Vector3(w, h, b.size.z))
        };
        foreach (Bounds s in strips)
        {
            var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = "EdgeGlow";
            Object.DestroyImmediate(strip.GetComponent<Collider>());
            strip.transform.position = s.center;
            strip.transform.localScale = s.size;
            var r = strip.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            strip.transform.SetParent(box.transform, true);
        }
    }

    private static void Gate(Transform parent, string name, Vector3 baseCenter, Vector3 spanAxis, float width, float height, Material material, Color lightColor)
    {
        var gate = Child(parent, name);
        Vector3 side = spanAxis.normalized * (width * .5f);
        Vector3 postSize = Mathf.Abs(spanAxis.x) > .5f ? new Vector3(.35f, height, .5f) : new Vector3(.5f, height, .35f);
        Vector3 lintelSize = Mathf.Abs(spanAxis.x) > .5f ? new Vector3(width + .35f, .3f, .5f) : new Vector3(.5f, .3f, width + .35f);
        Box(gate, name + "_PostA", baseCenter + side + Vector3.up * (height * .5f), postSize, material, false);
        Box(gate, name + "_PostB", baseCenter - side + Vector3.up * (height * .5f), postSize, material, false);
        Box(gate, name + "_Lintel", baseCenter + Vector3.up * (height + .15f), lintelSize, material, false, false);
        var l = new GameObject(name + "_Light").AddComponent<Light>();
        l.transform.SetParent(gate, false);
        l.transform.position = baseCenter + Vector3.up * (height - .5f);
        l.type = LightType.Point;
        l.color = lightColor;
        l.range = 9f;
        l.intensity = 2.2f;
    }

    /// <summary>Creates (or updates) a material asset under Materials/Escape so builds include it.</summary>
    private static Material EscapeMaterial(string name, Material template, Color color, float emission, bool particles)
    {
        if (!AssetDatabase.IsValidFolder(MaterialDir + "Escape")) AssetDatabase.CreateFolder(MaterialDir.TrimEnd('/'), "Escape");
        string path = EscapeMaterialDir + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool create = m == null;
        if (particles)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) return null;
            if (create) m = new Material(shader);
            else m.shader = shader;
            EscapeFX.ConfigureAdditive(m);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        }
        else
        {
            if (template == null) return null;
            if (create) m = new Material(template);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color * .55f);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", null);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                m.SetColor("_EmissionColor", color * emission);
            }
        }
        m.name = name;
        if (create) AssetDatabase.CreateAsset(m, path);
        else EditorUtility.SetDirty(m);
        return m;
    }

    private static float Lerp(System.Random r, float a, float b) { return a + (float)r.NextDouble() * (b - a); }

    /// <summary>Like Find, but includes objects this builder created after the index was built.</summary>
    private static List<GameObject> FindAny(string name)
    {
        var list = new List<GameObject>();
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name && t.gameObject.activeSelf) list.Add(t.gameObject);
        if (list.Count == 0) missing.Add(name);
        return list;
    }

    private static void AddToBuild()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(s => s.path == Target);
        int at = scenes.FindIndex(s => s.path.EndsWith("/Repository_Final.unity"));
        var entry = new EditorBuildSettingsScene(Target, true);
        if (at >= 0) scenes.Insert(at + 1, entry); else scenes.Add(entry);
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
