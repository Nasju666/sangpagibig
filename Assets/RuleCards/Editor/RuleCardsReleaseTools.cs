using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Release helpers: RuleCards > Release > ...
///  1. Apply Player Settings   - names, version, icon, window mode.
///  2. Move Unused Resources   - anything inside a "Resources" folder ships even if unused; this renames
///                               the old game's / Photon demos' Resources folders so they stay in the
///                               project but out of the build.
///  3. Build Windows           - 64-bit player into <project>/Builds/, plus a size report.
/// </summary>
public static class RuleCardsReleaseTools
{
    public const string Company = "Nasju";
    public const string Product = "Rule Cards - The Archive";
    public const string Version = "1.0.0";
    public const string ExeName = "RuleCards";
    private const string IconPath = "Assets/RuleCards/Art/Icon/RuleCards_Icon.png";

    // Resources folders that the game really loads from at runtime. Everything else gets renamed.
    private static readonly string[] KeepResources =
    {
        "Assets/RuleCards/Art/Resources",
        "Assets/Photon/PhotonUnityNetworking/Resources", // PhotonServerSettings; PUN looks it up if touched
        "Assets/TextMesh Pro/Resources",
        "Assets/Sketchfab For Unity/Dependencies/Resources", // the Sketchfab editor plugin loads its UI from here
    };

    private static readonly string[] UnusedShippedAssets =
    {
        "Assets/RuleCards/Art/Resources/Audio/Ambience/creaks.ogg",
    };

    [MenuItem("RuleCards/Release/1. Apply Player Settings", false, 100)]
    public static void ApplyPlayerSettings()
    {
        PlayerSettings.companyName = Company;
        PlayerSettings.productName = Product;
        PlayerSettings.bundleVersion = Version;
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, "com.nasju.rulecards");

        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.defaultIsNativeResolution = true;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = false;
        PlayerSettings.visibleInBackground = false;
        PlayerSettings.allowFullscreenSwitch = true;
        PlayerSettings.forceSingleInstance = true;
        PlayerSettings.usePlayerLog = true;
        PlayerSettings.SplashScreen.showUnityLogo = PlayerSettings.SplashScreen.showUnityLogo; // licence decides
        PlayerSettings.SplashScreen.backgroundColor = new Color(.008f, .022f, .035f);
        PlayerSettings.SplashScreen.unityLogoStyle = PlayerSettings.SplashScreen.UnityLogoStyle.LightOnDark;

        AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
        if (importer != null)
        {
            bool dirty = importer.textureType != TextureImporterType.Default || importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed;
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            if (dirty) importer.SaveAndReimport();
        }
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        if (icon != null)
        {
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { icon });
            int n = PlayerSettings.GetIconSizesForTargetGroup(BuildTargetGroup.Standalone).Length;
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Standalone, Enumerable.Repeat(icon, n).ToArray());
        }
        else Debug.LogWarning("[RuleCards] Icon not found at " + IconPath);

        AssetDatabase.SaveAssets();
        Debug.Log("[RuleCards] Player settings: " + Company + " / " + Product + " v" + Version +
                  ", icon " + (icon != null ? "set" : "MISSING") + ", fullscreen window at native resolution.");
    }

    [MenuItem("RuleCards/Release/2. Move Unused Resources Out of Build", false, 101)]
    public static void MoveUnusedResources()
    {
        var moves = new List<KeyValuePair<string, string>>();
        string root = Path.GetDirectoryName(Application.dataPath).Replace('\\', '/') + "/";
        foreach (string full in Directory.GetDirectories(Application.dataPath, "Resources", SearchOption.AllDirectories).OrderBy(d => d))
        {
            string path = full.Replace('\\', '/').Substring(root.Length);
            if (!AssetDatabase.IsValidFolder(path)) continue;
            if (path.Contains("/Editor/") || KeepResources.Contains(path)) continue;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string target = parent == "Assets" ? "Assets/_OldGame_Resources_NotInBuild" : parent + "/Resources_NotInBuild";
            moves.Add(new KeyValuePair<string, string>(path, target));
        }
        foreach (string a in UnusedShippedAssets)
            if (AssetDatabase.LoadAssetAtPath<Object>(a) != null)
                moves.Add(new KeyValuePair<string, string>(a, "Assets/RuleCards/Art/Unused/" + Path.GetFileName(a)));

        if (moves.Count == 0) { Debug.Log("[RuleCards] Nothing to move: no unused Resources folders left."); return; }
        string list = string.Join("\n", moves.Select(m => m.Key));
        Debug.Log("[RuleCards] Unused Resources found:\n" + list);
        if (!Application.isBatchMode && !EditorUtility.DisplayDialog("Move unused Resources out of the build",
                "These are inside Resources folders, so they ship in every build. They will be renamed/moved (nothing is deleted):\n\n" + list,
                "Move them", "Cancel")) return;

        if (!AssetDatabase.IsValidFolder("Assets/RuleCards/Art/Unused")) AssetDatabase.CreateFolder("Assets/RuleCards/Art", "Unused");
        int ok = 0;
        foreach (var m in moves)
        {
            string err = AssetDatabase.MoveAsset(m.Key, m.Value);
            if (string.IsNullOrEmpty(err)) { ok++; Debug.Log("[RuleCards] Moved " + m.Key + "  ->  " + m.Value); }
            else Debug.LogWarning("[RuleCards] Could not move " + m.Key + ": " + err);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[RuleCards] Moved " + ok + "/" + moves.Count + " unused Resources out of the build.");
    }

    public static string BuildFolder
    {
        get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", ExeName + "_" + PlayerSettings.bundleVersion)); }
    }

    [MenuItem("RuleCards/Release/3. Build Windows (64-bit)", false, 102)]
    public static void BuildWindows()
    {
        string[] scenes = EditorBuildSettings.scenes.Where(e => e.enabled).Select(e => e.path).ToArray();
        if (scenes.Length == 0) { Debug.LogError("[RuleCards] No scenes enabled in Build Settings."); return; }
        string dir = BuildFolder;
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(dir, ExeName + ".exe"),
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None,
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result == BuildResult.Succeeded) { CopyLicences(dir); MoveDoNotShip(dir); }
        string reportPath = WriteReport(report, scenes, dir);
        BuildSummary s = report.summary;
        string line = "[RuleCards] Build " + s.result + ": " + Mb(FolderSize(dir)) + " on disk, " + s.totalErrors + " errors, " +
                      s.totalWarnings + " warnings, " + s.totalTime.TotalSeconds.ToString("0") + " s.  " + dir + "\nReport: " + reportPath;
        if (s.result == BuildResult.Succeeded) Debug.Log(line); else Debug.LogError(line);
        if (s.result == BuildResult.Succeeded && !Application.isBatchMode) EditorUtility.RevealInFinder(options.locationPathName);
    }

    /// <summary>Burst writes "<product>_BurstDebugInformation_DoNotShip" next to the exe; park it outside the folder you zip.</summary>
    private static void MoveDoNotShip(string dir)
    {
        string parent = Path.GetDirectoryName(dir);
        foreach (string d in Directory.GetDirectories(dir, "*DoNotShip*"))
        {
            string target = Path.Combine(parent, Path.GetFileName(dir) + "_" + Path.GetFileName(d));
            if (Directory.Exists(target)) Directory.Delete(target, true);
            Directory.Move(d, target);
        }
    }

    [MenuItem("RuleCards/Release/4. Run Last Build", false, 103)]
    public static void RunLastBuild()
    {
        string exe = Path.Combine(BuildFolder, ExeName + ".exe");
        if (!File.Exists(exe)) { Debug.LogWarning("[RuleCards] No build at " + exe); return; }
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe) { WorkingDirectory = BuildFolder, UseShellExecute = true });
        Debug.Log("[RuleCards] Started " + exe + "  (log: %USERPROFILE%/AppData/LocalLow/" + PlayerSettings.companyName + "/" + PlayerSettings.productName + "/Player.log)");
    }

    [MenuItem("RuleCards/Release/5. Smoke Test Last Build (auto, ~2 min)", false, 104)]
    public static void SmokeTestLastBuild()
    {
        string exe = Path.Combine(BuildFolder, ExeName + ".exe");
        if (!File.Exists(exe)) { Debug.LogWarning("[RuleCards] No build at " + exe); return; }
        StopRunningBuild();
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, "-smoketest")
            { WorkingDirectory = BuildFolder, UseShellExecute = true });
        Debug.Log("[RuleCards] Smoke test started. Results: %USERPROFILE%/AppData/LocalLow/" + PlayerSettings.companyName + "/" + PlayerSettings.productName + "/SmokeTest/summary.txt");
    }

    [MenuItem("RuleCards/Release/6. Stop Running Build", false, 105)]
    public static void StopRunningBuild()
    {
        foreach (var p in System.Diagnostics.Process.GetProcessesByName(ExeName))
        {
            try { p.Kill(); Debug.Log("[RuleCards] Stopped " + ExeName + " (pid " + p.Id + ")"); } catch (System.Exception e) { Debug.LogWarning(e.Message); }
        }
    }

    /// <summary>Ships CREDITS.txt and the font licences (the SIL OFL asks for them to travel with the fonts).</summary>
    private static void CopyLicences(string dir)
    {
        string project = Path.GetDirectoryName(Application.dataPath);
        string lic = Path.Combine(dir, "Licenses");
        Directory.CreateDirectory(lic);
        string credits = Path.Combine(project, "Assets/RuleCards/Art/CREDITS.txt");
        if (File.Exists(credits)) File.Copy(credits, Path.Combine(dir, "CREDITS.txt"), true);
        string fonts = Path.Combine(project, "Assets/RuleCards/Art/Resources/Fonts");
        if (Directory.Exists(fonts))
            foreach (string f in Directory.GetFiles(fonts, "OFL-*.txt")) File.Copy(f, Path.Combine(lic, Path.GetFileName(f)), true);
    }

    private static string WriteReport(BuildReport report, string[] scenes, string dir)
    {
        var sb = new StringBuilder();
        BuildSummary s = report.summary;
        sb.AppendLine("RuleCards build report");
        sb.AppendLine("Result:   " + s.result);
        sb.AppendLine("Product:  " + PlayerSettings.companyName + " / " + PlayerSettings.productName + " v" + PlayerSettings.bundleVersion);
        sb.AppendLine("Output:   " + s.outputPath);
        sb.AppendLine("On disk:  " + Mb(FolderSize(dir)));
        sb.AppendLine("Time:     " + s.totalTime);
        sb.AppendLine("Errors:   " + s.totalErrors + "   Warnings: " + s.totalWarnings);
        sb.AppendLine();
        sb.AppendLine("Scenes:");
        foreach (string sc in scenes) sb.AppendLine("  " + sc);

        sb.AppendLine();
        sb.AppendLine("Messages:");
        foreach (BuildStep step in report.steps)
            foreach (BuildStepMessage m in step.messages)
                if (m.type == LogType.Error || m.type == LogType.Exception || m.type == LogType.Warning)
                    sb.AppendLine("  [" + m.type + "] " + m.content.Split('\n')[0]);

        var bySource = new Dictionary<string, ulong>();
        var byFolder = new Dictionary<string, ulong>();
        foreach (PackedAssets pack in report.packedAssets)
            foreach (PackedAssetInfo info in pack.contents)
            {
                string src = string.IsNullOrEmpty(info.sourceAssetPath) ? "(built-in / generated)" : info.sourceAssetPath;
                bySource[src] = (bySource.TryGetValue(src, out ulong a) ? a : 0) + info.packedSize;
                string folder = TopFolder(src);
                byFolder[folder] = (byFolder.TryGetValue(folder, out ulong b) ? b : 0) + info.packedSize;
            }

        sb.AppendLine();
        sb.AppendLine("Packed size by folder:");
        foreach (var kv in byFolder.OrderByDescending(k => k.Value)) sb.AppendLine("  " + Mb((long)kv.Value).PadLeft(10) + "  " + kv.Key);
        sb.AppendLine();
        sb.AppendLine("Largest 60 assets:");
        foreach (var kv in bySource.OrderByDescending(k => k.Value).Take(60)) sb.AppendLine("  " + Mb((long)kv.Value).PadLeft(10) + "  " + kv.Key);
        sb.AppendLine();
        sb.AppendLine("Every asset outside Assets/RuleCards (check these are meant to ship):");
        foreach (var kv in bySource.Where(k => k.Key.StartsWith("Assets/") && !k.Key.StartsWith("Assets/RuleCards/")).OrderBy(k => k.Key))
            sb.AppendLine("  " + Mb((long)kv.Value).PadLeft(10) + "  " + kv.Key);

        string path = Path.Combine(Path.GetDirectoryName(dir), ExeName + "_" + PlayerSettings.bundleVersion + "_BuildReport.txt");
        File.WriteAllText(path, sb.ToString());
        return path;
    }

    private static string TopFolder(string src)
    {
        if (!src.StartsWith("Assets/")) return src.StartsWith("Packages/") ? string.Join("/", src.Split('/').Take(2)) : src;
        string[] parts = src.Split('/');
        int depth = src.StartsWith("Assets/RuleCards/") ? 4 : 2;
        return string.Join("/", parts.Take(Mathf.Min(depth, parts.Length - 1)));
    }

    private static long FolderSize(string dir)
    {
        if (!Directory.Exists(dir)) return 0;
        return new DirectoryInfo(dir).GetFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
    }

    private static string Mb(long bytes) { return (bytes / 1048576.0).ToString("0.00") + " MB"; }
}
