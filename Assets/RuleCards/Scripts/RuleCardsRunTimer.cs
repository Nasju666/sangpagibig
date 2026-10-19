using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Whole-game run timer: counts unpaused play time in every gameplay scene (hub, stages 1–3,
/// Repository, Escape) from New Game until the Archive is escaped, with a split per scene.
/// Saved in PlayerPrefs, so quitting and pressing Continue carries the run on. Installs itself.
/// </summary>
public sealed class RuleCardsRunTimer : MonoBehaviour
{
    private const string Prefix = "RuleCards.Run.";
    private const string TotalKey = Prefix + "Total";
    private const string FinishedKey = Prefix + "Finished";
    private const string PartialKey = Prefix + "Partial";
    private const string StartedKey = Prefix + "Started";
    private const string BestKey = Prefix + "Best";

    /// <summary>Scene name → label, in play order (used for the split line).</summary>
    public static readonly string[][] Splits =
    {
        new[] { "Stage_01_KineticLink", "Stage 1" },
        new[] { "Stage_02_InvertGravity", "Stage 2" },
        new[] { "Stage_03_SolidifyAir", "Stage 3" },
        new[] { "Repository_Final", "Repository" },
        new[] { "Stage_04_Escape", "Escape" },
        new[] { "ArchiveHub", "Hub" },
    };

    private static RuleCardsRunTimer instance;
    private float pending;          // time not yet written to PlayerPrefs
    private string pendingScene;
    private float nextFlush;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;
        var go = new GameObject("RuleCards_RunTimer");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<RuleCardsRunTimer>();
        SceneManager.activeSceneChanged += (a, b) => Flush();
    }

    // ------------------------------------------------------------------ queries

    public static bool Finished { get { return PlayerPrefs.GetInt(FinishedKey, 0) == 1; } }

    /// <summary>True when the run began on a save that already had progress (older build), so the total is incomplete.</summary>
    public static bool Partial { get { return PlayerPrefs.GetInt(PartialKey, 0) == 1; } }

    public static float Total { get { return PlayerPrefs.GetFloat(TotalKey, 0f) + (instance != null ? instance.pending : 0f); } }

    public static float Split(string scene)
    {
        float t = PlayerPrefs.GetFloat(Prefix + "Split." + scene, 0f);
        if (instance != null && instance.pendingScene == scene) t += instance.pending;
        return t;
    }

    public static float BestRun { get { return PlayerPrefs.GetFloat(BestKey, 0f); } }

    public static string Format(float seconds)
    {
        int s = Mathf.FloorToInt(seconds);
        return s >= 3600 ? (s / 3600) + ":" + (s / 60 % 60).ToString("00") + ":" + (s % 60).ToString("00")
                         : (s / 60) + ":" + (s % 60).ToString("00");
    }

    /// <summary>"Stage 1 4:12 • Stage 2 6:40 • …" for every scene that has time on it.</summary>
    public static string SplitLine()
    {
        var sb = new System.Text.StringBuilder();
        foreach (string[] split in Splits)
        {
            float t = Split(split[0]);
            if (t < 1f) continue;
            if (sb.Length > 0) sb.Append("   •   ");
            sb.Append(split[1]).Append(' ').Append(Format(t));
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------------ lifecycle

    /// <summary>New Game: wipe the run (best run is wiped with the rest of the save by ResetAll).</summary>
    public static void ResetRun()
    {
        if (instance != null) { instance.pending = 0f; instance.pendingScene = null; }
        PlayerPrefs.DeleteKey(TotalKey);
        PlayerPrefs.DeleteKey(FinishedKey);
        PlayerPrefs.DeleteKey(PartialKey);
        PlayerPrefs.DeleteKey(BestKey);
        foreach (string[] split in Splits) PlayerPrefs.DeleteKey(Prefix + "Split." + split[0]);
        PlayerPrefs.SetInt(StartedKey, 1);
        PlayerPrefs.Save();
    }

    /// <summary>Stops the clock at the escape. Returns true when the full run is a new best.</summary>
    public static bool FinishRun()
    {
        Flush();
        if (Finished) return false;
        PlayerPrefs.SetInt(FinishedKey, 1);
        bool best = false;
        float total = PlayerPrefs.GetFloat(TotalKey, 0f);
        if (!Partial && total > 0f && (BestRun <= 0f || total < BestRun))
        {
            PlayerPrefs.SetFloat(BestKey, total);
            best = true;
        }
        PlayerPrefs.Save();
        return best;
    }

    private static void Flush()
    {
        if (instance == null || instance.pending <= 0f || string.IsNullOrEmpty(instance.pendingScene)) return;
        PlayerPrefs.SetFloat(TotalKey, PlayerPrefs.GetFloat(TotalKey, 0f) + instance.pending);
        string key = Prefix + "Split." + instance.pendingScene;
        PlayerPrefs.SetFloat(key, PlayerPrefs.GetFloat(key, 0f) + instance.pending);
        PlayerPrefs.Save();
        instance.pending = 0f;
    }

    private void Update()
    {
        string scene = SceneManager.GetActiveScene().name;
        if (!RuleCardsScenes.IsGameplay(scene) || Finished || MouseLook.isPaused || RuleCardsStory.Blocking || Time.timeScale <= 0f) return;
        if (PlayerPrefs.GetInt(StartedKey, 0) == 0)
        {
            // First run on this save since the timer existed: earlier stages weren't timed.
            PlayerPrefs.SetInt(StartedKey, 1);
            if (RuleCardsProgress.HasAnyProgress) PlayerPrefs.SetInt(PartialKey, 1);
            PlayerPrefs.Save();
        }
        if (pendingScene != scene) { Flush(); pendingScene = scene; }
        pending += Mathf.Min(Time.unscaledDeltaTime, .25f); // ignore load hitches
        if (Time.unscaledTime >= nextFlush) { nextFlush = Time.unscaledTime + 5f; Flush(); }
    }

    private void OnApplicationQuit() { Flush(); }
    private void OnApplicationPause(bool paused) { if (paused) Flush(); }
}
