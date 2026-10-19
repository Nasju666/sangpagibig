using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Automatic build check. Only runs when the player is started with "-smoketest"
/// (RuleCards > Release > 5. Smoke Test Last Build). Loads every build scene in order, lets each run a few
/// seconds, screenshots it, measures FPS and counts errors, writes SmokeTest/summary.txt next to Player.log,
/// wipes the PlayerPrefs it touched and quits.
/// </summary>
public sealed class RuleCardsSmokeTest : MonoBehaviour
{
    private const float SettleSeconds = 7f, MeasureSeconds = 4f;
    private int errors, warnings;
    private readonly StringBuilder log = new StringBuilder();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-smoketest") < 0) return;
        PlayerPrefs.DeleteAll();
        var go = new GameObject("RuleCards_SmokeTest");
        DontDestroyOnLoad(go);
        go.AddComponent<RuleCardsSmokeTest>();
    }

    private void OnEnable() { Application.logMessageReceived += OnLog; }
    private void OnDisable() { Application.logMessageReceived -= OnLog; }

    private void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            errors++;
            if (errors <= 40) log.AppendLine("    [" + type + "] " + message.Split('\n')[0] + (string.IsNullOrEmpty(stack) ? "" : "  @ " + stack.Split('\n')[0]));
        }
        else if (type == LogType.Warning) warnings++;
    }

    private IEnumerator Start()
    {
        string dir = Path.Combine(Application.persistentDataPath, "SmokeTest");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        var summary = new StringBuilder();
        summary.AppendLine("RuleCards smoke test  " + System.DateTime.Now);
        summary.AppendLine(Application.productName + " v" + Application.version + "   " + Screen.width + "x" + Screen.height + "   " +
                           SystemInfo.graphicsDeviceName + " (" + SystemInfo.graphicsMemorySize + " MB)   quality " + QualitySettings.names[QualitySettings.GetQualityLevel()]);
        summary.AppendLine();

        int count = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < count; i++)
        {
            string name = Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
            int errorsBefore = errors;
            log.Length = 0;
            float loadStart = Time.realtimeSinceStartup;
            if (SceneManager.GetActiveScene().buildIndex != i)
            {
                AsyncOperation op = SceneManager.LoadSceneAsync(i);
                while (!op.isDone) yield return null;
            }
            float loadTime = Time.realtimeSinceStartup - loadStart;

            yield return new WaitForSecondsRealtime(SettleSeconds);
            int frames = 0; float t0 = Time.realtimeSinceStartup, worst = 0f;
            while (Time.realtimeSinceStartup - t0 < MeasureSeconds) { frames++; worst = Mathf.Max(worst, Time.unscaledDeltaTime); yield return null; }
            float fps = frames / (Time.realtimeSinceStartup - t0);

            yield return Shot(dir, i.ToString("00") + "_" + name);
            if (i == 0) yield return TitleCredits(dir);

            summary.AppendLine(string.Format("{0,-24} load {1,5:0.0}s   avg {2,5:0} fps   worst frame {3,4:0} ms   errors {4}",
                name, loadTime, fps, worst * 1000f, errors - errorsBefore));
            if (log.Length > 0) summary.Append(log);
        }

        summary.AppendLine();
        summary.AppendLine("Total errors: " + errors + "   warnings: " + warnings);
        File.WriteAllText(Path.Combine(dir, "summary.txt"), summary.ToString());
        Debug.Log("[SmokeTest] done\n" + summary);
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Application.Quit();
    }

    private static IEnumerator Shot(string dir, string file)
    {
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(dir, file + ".png"));
        yield return null;
        yield return null;
    }

    /// <summary>Opens the title's Credits panel for one screenshot (private flag, so via reflection).</summary>
    private static IEnumerator TitleCredits(string dir)
    {
        var menu = FindObjectOfType<RuleCardsTitleMenu>();
        var field = typeof(RuleCardsTitleMenu).GetField("creditsOpen", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (menu == null || field == null) yield break;
        field.SetValue(menu, true);
        yield return new WaitForSecondsRealtime(.5f);
        yield return Shot(dir, "00_Title_Credits");
        field.SetValue(menu, false);
    }
}
