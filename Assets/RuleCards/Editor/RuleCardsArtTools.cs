using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Import settings for the converted Rodin models + their texture sets.</summary>
public sealed class RuleCardsArtPostprocessor : AssetPostprocessor
{
    public const string ModelFolder = "Assets/RuleCards/Art/Models";

    private void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(ModelFolder)) return;
        var importer = (ModelImporter)assetImporter;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importBlendShapes = false;
        importer.animationType = ModelImporterAnimationType.None;
        importer.isReadable = false;
        importer.addCollider = false;
        importer.importNormals = ModelImporterNormals.Import;
        importer.meshOptimizationFlags = MeshOptimizationFlags.Everything;
        importer.generateSecondaryUV = false;
    }

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(ModelFolder)) return;
        var importer = (TextureImporter)assetImporter;
        importer.mipmapEnabled = true;
        importer.anisoLevel = 4;
        if (assetPath.EndsWith("_Normal.png"))
        {
            importer.textureType = TextureImporterType.NormalMap;
        }
        else
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = !assetPath.EndsWith("_MetalSmooth.png");
            importer.alphaSource = assetPath.EndsWith("_MetalSmooth.png") ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
        }
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
    }
}

/// <summary>
/// Art pipeline menu: build URP materials + prefabs from the converted models, switch the renderer
/// to Forward+ (lots of small decor lights), and dump scene geometry for decoration planning.
/// </summary>
public static class RuleCardsArtTools
{
    public const string ArtRoot = "Assets/RuleCards/Art";
    public const string MaterialFolder = ArtRoot + "/Materials";
    public const string PrefabFolder = ArtRoot + "/Prefabs";
    public const string ResourcesFolder = ArtRoot + "/Resources";

    public static readonly string[] Scenes =
    {
        "Assets/RuleCards/Levels/RuleCards_Title.unity",
        "Assets/RuleCards/Levels/ArchiveHub.unity",
        "Assets/RuleCards/Levels/Stage_01_KineticLink.unity",
        "Assets/RuleCards/Levels/Stage_02_InvertGravity.unity",
        "Assets/RuleCards/Levels/Stage_03_SolidifyAir.unity",
        "Assets/RuleCards/Levels/Repository_Final.unity",
        "Assets/RuleCards/Levels/Stage_04_Escape.unity",
    };

    // name -> emission intensity (0 = no emission map)
    private static readonly Dictionary<string, float> Models = new Dictionary<string, float>
    {
        { "ArchiveShelf", 0f }, { "Archway", 1.8f }, { "BrokenBridge", 1.4f }, { "CrystalCluster", 2.6f },
        { "Medallion", 1.6f }, { "FloatingBook", 1.6f }, { "Bulkhead", 1.6f }, { "GuardianStatue", 0f },
        { "KineticAnchor", 2.2f }, { "ScribeStatue", 0f }, { "Pillar", 1.6f }, { "Railing", 0f },
        { "Rubble", 1.4f }, { "RuleCard", 2.0f }, { "RuneTerminal", 1.8f }, { "WallLedge", 1.4f },
        { "Chandelier", 2.0f }, { "Pebbles", 0f }, { "ExitGate", 2.4f }, { "RuneRing", 2.2f },
    };

    [MenuItem("RuleCards/Art/1. Build Model Prefabs + Render Settings", false, 200)]
    public static void BuildAll()
    {
        BuildPrefabs();
        ApplyRenderSettings();
        DumpPrefabBounds();
        Debug.Log("[RuleCards Art] Prefabs + render settings done.");
    }

    public static void BuildPrefabs()
    {
        Ensure(MaterialFolder); Ensure(PrefabFolder); Ensure(ResourcesFolder);
        Material particle = ParticleMaterial();
        Material iron = SimpleLit("MAT_ART_Iron", new Color(.075f, .07f, .068f), .85f, .45f);

        foreach (var kv in Models)
        {
            string name = kv.Key;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{RuleCardsArtPostprocessor.ModelFolder}/{name}.fbx");
            if (model == null) { Debug.LogWarning("[RuleCards Art] Missing model " + name); continue; }
            Material mat = ModelMaterial(name, kv.Value);

            var root = new GameObject("ART_" + name);
            var body = (GameObject)PrefabUtility.InstantiatePrefab(model);
            body.name = "Model";
            body.transform.SetParent(root.transform, false);
            foreach (Renderer r in body.GetComponentsInChildren<Renderer>())
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
            Decorate(name, root, body, mat, iron);
            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/ART_{name}.prefab");
            Object.DestroyImmediate(root);
        }

        BuildTorch(iron, particle);
        // Runtime copy of the card model for the Escape stage's held card.
        string held = ResourcesFolder + "/RuleCardsHeldCard.prefab";
        AssetDatabase.DeleteAsset(held);
        AssetDatabase.CopyAsset($"{PrefabFolder}/ART_RuleCard.prefab", held);
        AssetDatabase.SaveAssets();
    }

    private static void Decorate(string name, GameObject root, GameObject body, Material mat, Material iron)
    {
        switch (name)
        {
            case "Chandelier":
            {
                // Root pivot = ceiling hook. Body hangs below on a chain; the root sways like a pendulum.
                var hang = new GameObject("Body");
                hang.transform.SetParent(root.transform, false);
                hang.transform.localPosition = new Vector3(0f, -5f, 0f);
                body.transform.SetParent(hang.transform, false);
                var chain = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                chain.name = "Chain";
                Object.DestroyImmediate(chain.GetComponent<Collider>());
                chain.transform.SetParent(root.transform, false);
                chain.transform.localPosition = new Vector3(0f, -1.5f, 0f);
                chain.transform.localScale = new Vector3(.06f, 1.5f, .06f);
                chain.GetComponent<Renderer>().sharedMaterial = iron;
                AddLight(hang.transform, new Vector3(0f, .9f, 0f), new Color(.62f, 1f, .9f), 2.4f, 14f);
                var motion = root.AddComponent<DecorMotion>();
                motion.swayAngle = 1.6f; motion.swaySpeed = .55f;
                break;
            }
            case "CrystalCluster":
            {
                var light = AddLight(root.transform, new Vector3(0f, .9f, 0f), new Color(.35f, .95f, 1f), 1.4f, 5f);
                light.gameObject.SetActive(false); // the decorator switches on a few of these
                var motion = root.AddComponent<DecorMotion>();
                motion.pulseAmount = .22f; motion.pulseSpeed = 1.3f; motion.emissionIntensity = 2.6f;
                break;
            }
            case "RuneRing":
            {
                // Spin about the ring's centre, not its base pivot.
                var pivot = new GameObject("Spin");
                pivot.transform.SetParent(root.transform, false);
                pivot.transform.localPosition = new Vector3(0f, 1.45f, 0f);
                body.transform.SetParent(pivot.transform, false);
                body.transform.localPosition = new Vector3(0f, -1.45f, 0f);
                var motion = pivot.AddComponent<DecorMotion>();
                motion.spin = new Vector3(0f, 0f, 9f);
                break;
            }
            case "FloatingBook":
            {
                var motion = root.AddComponent<DecorMotion>();
                motion.bobHeight = .18f; motion.bobSpeed = .8f; motion.spin = new Vector3(0f, 7f, 0f);
                break;
            }
        }
    }

    private static void BuildTorch(Material iron, Material particle)
    {
        // Wall sconce: root sits on the wall surface, +Z points out of the wall.
        var root = new GameObject("ART_Torch");
        Part(PrimitiveType.Cube, root.transform, "Plate", new Vector3(0f, 0f, .03f), new Vector3(.2f, .36f, .06f), Vector3.zero, iron);
        Part(PrimitiveType.Cube, root.transform, "Arm", new Vector3(0f, .03f, .19f), new Vector3(.05f, .05f, .34f), new Vector3(-28f, 0f, 0f), iron);
        Part(PrimitiveType.Cylinder, root.transform, "Cup", new Vector3(0f, .14f, .34f), new Vector3(.17f, .05f, .17f), Vector3.zero, iron);
        Part(PrimitiveType.Cylinder, root.transform, "CupRim", new Vector3(0f, .19f, .34f), new Vector3(.21f, .012f, .21f), Vector3.zero, iron);
        var fire = new GameObject("Fire");
        fire.transform.SetParent(root.transform, false);
        fire.transform.localPosition = new Vector3(0f, .2f, .34f);
        var flame = fire.AddComponent<DecorFlame>();
        flame.particleMaterial = particle;
        flame.size = .75f;
        PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/ART_Torch.prefab");
        Object.DestroyImmediate(root);

        // Free-standing brazier version (pedestal + bowl).
        root = new GameObject("ART_Brazier");
        Part(PrimitiveType.Cylinder, root.transform, "Stem", new Vector3(0f, .5f, 0f), new Vector3(.14f, .5f, .14f), Vector3.zero, iron);
        Part(PrimitiveType.Cylinder, root.transform, "Foot", new Vector3(0f, .04f, 0f), new Vector3(.5f, .04f, .5f), Vector3.zero, iron);
        Part(PrimitiveType.Cylinder, root.transform, "Bowl", new Vector3(0f, 1.05f, 0f), new Vector3(.62f, .09f, .62f), Vector3.zero, iron);
        fire = new GameObject("Fire");
        fire.transform.SetParent(root.transform, false);
        fire.transform.localPosition = new Vector3(0f, 1.12f, 0f);
        flame = fire.AddComponent<DecorFlame>();
        flame.particleMaterial = particle;
        flame.size = 1.5f;
        flame.lightRange = 10f;
        flame.lightIntensity = 2.2f;
        PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/ART_Brazier.prefab");
        Object.DestroyImmediate(root);
    }

    private static void Part(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Vector3 euler, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.transform.localEulerAngles = euler;
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    private static Light AddLight(Transform parent, Vector3 pos, Color color, float intensity, float range)
    {
        var go = new GameObject("Glow");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        return l;
    }

    private static Material ModelMaterial(string name, float emission)
    {
        string path = $"{MaterialFolder}/MAT_ART_{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool create = mat == null;
        if (create) mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        string tex = RuleCardsArtPostprocessor.ModelFolder + "/" + name;
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "_Albedo.png"));
        mat.SetColor("_BaseColor", Color.white);
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "_Normal.png");
        mat.SetTexture("_BumpMap", normal);
        mat.SetFloat("_BumpScale", 1f);
        if (normal != null) mat.EnableKeyword("_NORMALMAP");
        var ms = AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "_MetalSmooth.png");
        mat.SetTexture("_MetallicGlossMap", ms);
        if (ms != null) mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        mat.SetFloat("_Smoothness", .85f);
        var em = emission > 0f ? AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "_Emission.png") : null;
        if (em != null)
        {
            mat.SetTexture("_EmissionMap", em);
            mat.SetColor("_EmissionColor", Color.white * emission);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            mat.SetColor("_EmissionColor", Color.black);
            mat.DisableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }
        if (create) AssetDatabase.CreateAsset(mat, path);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material SimpleLit(string name, Color color, float metallic, float smoothness)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>Additive soft-dot particle material in Resources (runtime dust + flames load it by name).</summary>
    public static Material ParticleMaterial()
    {
        string texPath = ResourcesFolder + "/RuleCardsSoftDot.png";
        if (!File.Exists(texPath))
        {
            const int n = 64;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + .5f) / n * 2f - 1f, dy = (y + .5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            File.WriteAllBytes(texPath, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(texPath);
            var ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.SaveAndReimport();
        }
        string path = ResourcesFolder + "/RuleCardsParticle.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
        mat.SetColor("_BaseColor", Color.white);
        // Transparent, additive.
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 2f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.SetOverrideTag("RenderType", "Transparent");
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>Forward+ so dozens of small torch/crystal lights don't hit the 8-lights-per-object limit.</summary>
    public static void ApplyRenderSettings()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets" }))
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
            if (data == null || data.renderingMode == RenderingMode.ForwardPlus) continue;
            data.renderingMode = RenderingMode.ForwardPlus;
            EditorUtility.SetDirty(data);
        }
        AssetDatabase.SaveAssets();
    }

    private static void Ensure(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        Ensure(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    // ------------------------------------------------------------------ geometry dump

    private static string DumpFolder { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "../ArtDump")); } }

    private static void DumpPrefabBounds()
    {
        Directory.CreateDirectory(DumpFolder);
        var sb = new StringBuilder("[\n");
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            Bounds? b = null;
            foreach (Renderer r in prefab.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                Bounds mb = TransformBounds(r.transform.localToWorldMatrix, mf.sharedMesh.bounds);
                if (b == null) b = mb; else { var x = b.Value; x.Encapsulate(mb); b = x; }
            }
            if (b == null) continue;
            sb.Append($"{{\"name\":\"{prefab.name}\",\"c\":{V(b.Value.center)},\"s\":{V(b.Value.size)}}},\n");
        }
        sb.Append("{}]\n");
        File.WriteAllText(Path.Combine(DumpFolder, "_prefabs.json"), sb.ToString());
    }

    private static Bounds TransformBounds(Matrix4x4 m, Bounds b)
    {
        var result = new Bounds(m.MultiplyPoint3x4(b.center), Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            Vector3 c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            result.Encapsulate(m.MultiplyPoint3x4(c));
        }
        return result;
    }

    [MenuItem("RuleCards/Art/Dump Scene Geometry (all stages)", false, 260)]
    public static void DumpScenes()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Directory.CreateDirectory(DumpFolder);
        string previous = SceneManager.GetActiveScene().path;
        foreach (string path in Scenes)
        {
            if (!File.Exists(path)) continue;
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            File.WriteAllText(Path.Combine(DumpFolder, scene.name + ".json"), Dump(scene));
        }
        if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        Debug.Log("[RuleCards Art] Scene geometry dumped to " + DumpFolder);
    }

    private static string Dump(Scene scene)
    {
        var sb = new StringBuilder();
        sb.Append("{\"scene\":\"").Append(scene.name).Append("\",\n\"fog\":").Append(RenderSettings.fog ? 1 : 0)
          .Append(",\"fogColor\":").Append(C(RenderSettings.fogColor)).Append(",\"fogDensity\":").Append(F(RenderSettings.fogDensity))
          .Append(",\n\"items\":[\n");
        bool first = true;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                GameObject go = t.gameObject;
                var parts = new List<string>();
                var mr = go.GetComponent<MeshRenderer>();
                var mf = go.GetComponent<MeshFilter>();
                if (mr != null)
                    parts.Add($"\"r\":{{\"on\":{(mr.enabled ? 1 : 0)},\"c\":{V(mr.bounds.center)},\"s\":{V(mr.bounds.size)},\"mesh\":\"{Esc(mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "")}\",\"mat\":\"{Esc(mr.sharedMaterial != null ? mr.sharedMaterial.name : "")}\"}}");
                foreach (Collider col in go.GetComponents<Collider>())
                {
                    string kind = col.GetType().Name.Replace("Collider", "");
                    string extra = "";
                    if (col is BoxCollider box) extra = $",\"bc\":{V(box.center)},\"bs\":{V(box.size)}";
                    parts.Add($"\"col\":{{\"k\":\"{kind}\",\"on\":{(col.enabled ? 1 : 0)},\"trig\":{(col.isTrigger ? 1 : 0)},\"c\":{V(col.bounds.center)},\"s\":{V(col.bounds.size)}{extra}}}");
                    break;
                }
                var light = go.GetComponent<Light>();
                if (light != null)
                    parts.Add($"\"light\":{{\"t\":\"{light.type}\",\"range\":{F(light.range)},\"i\":{F(light.intensity)},\"col\":{C(light.color)}}}");
                var scripts = new List<string>();
                foreach (MonoBehaviour mb in go.GetComponents<MonoBehaviour>())
                {
                    if (mb == null) continue;
                    string ns = mb.GetType().Namespace ?? "";
                    if (ns.StartsWith("UnityEngine") || ns.StartsWith("TMPro")) continue;
                    scripts.Add("\"" + mb.GetType().Name + "\"");
                }
                if (scripts.Count > 0) parts.Add("\"mb\":[" + string.Join(",", scripts) + "]");
                if (go.GetComponent<Camera>() != null) parts.Add("\"cam\":1");
                if (parts.Count == 0 && t.childCount > 0) continue; // plain group node
                if (!first) sb.Append(",\n");
                first = false;
                sb.Append("{\"p\":\"").Append(Esc(PathOf(t))).Append("\",\"act\":").Append(go.activeInHierarchy ? 1 : 0)
                  .Append(",\"pos\":").Append(V(t.position)).Append(",\"rot\":").Append(V(t.eulerAngles)).Append(",\"sc\":").Append(V(t.lossyScale));
                foreach (string p in parts) sb.Append(',').Append(p);
                sb.Append('}');
            }
        }
        sb.Append("\n]}\n");
        return sb.ToString();
    }

    private static string PathOf(Transform t)
    {
        string p = t.name;
        while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }

    private static string Esc(string s) { return s.Replace("\\", "\\\\").Replace("\"", "\\\""); }
    private static string F(float f) { return f.ToString("0.###", CultureInfo.InvariantCulture); }
    private static string V(Vector3 v) { return $"[{F(v.x)},{F(v.y)},{F(v.z)}]"; }
    private static string C(Color c) { return $"[{F(c.r)},{F(c.g)},{F(c.b)}]"; }
}
