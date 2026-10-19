using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Scene names and safe scene loading for the RuleCards game.</summary>
public static class RuleCardsScenes
{
    public const string Title = "RuleCards_Title";
    public const string Hub = "ArchiveHub";
    public const string Repository = "Repository_Final";
    public const string Escape = "Stage_04_Escape";
    private static readonly string[] Gameplay = {
        Hub, "Stage_01_KineticLink", "Stage_02_InvertGravity", "Stage_03_SolidifyAir", Repository, Escape
    };

    public static bool EscapeAvailable { get { return Application.CanStreamedLevelBeLoaded(Escape); } }

    /// <summary>Once the Repository is done but the escape is not, the Repository portal leads to the escape.</summary>
    public static string Resolve(string sceneName)
    {
        if (sceneName == Repository && RuleCardsProgress.IsRepositoryComplete && !RuleCardsProgress.IsGameComplete && EscapeAvailable)
            return Escape;
        return sceneName;
    }

    public static bool IsGameplay(string sceneName) { return System.Array.IndexOf(Gameplay, sceneName) >= 0; }

    /// <summary>Always leaves pause state behind before switching scenes.</summary>
    public static void Load(string sceneName)
    {
        Time.timeScale = 1f;
        MouseLook.isPaused = false;
        AudioListener.pause = false;
        SceneManager.LoadScene(sceneName);
    }
}

/// <summary>
/// Consoles (calibration, kinetic matrix, Repository nodes, ending) call MarkOpen every frame they
/// are open, so the pause menu never steals the Esc press that is meant to close them.
/// </summary>
public static class ArchiveModal
{
    private static int lastOpenFrame = -10;
    public static void MarkOpen() { lastOpenFrame = Time.frameCount; }
    public static bool IsOpen { get { return Time.frameCount - lastOpenFrame <= 1; } }
}

/// <summary>Esc pause menu for every RuleCards gameplay scene. Installs itself; no scene setup needed.</summary>
public sealed class RuleCardsPauseMenu : MonoBehaviour
{
    private static RuleCardsPauseMenu instance;
    private bool paused;
    private bool confirmReset;
    private float previousTimeScale = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;
        instance = new GameObject("RuleCards_PauseMenu").AddComponent<RuleCardsPauseMenu>();
        DontDestroyOnLoad(instance.gameObject);
    }

    public static bool IsPaused { get { return instance != null && instance.paused; } }

    private void Awake() { SceneManager.sceneLoaded += OnSceneLoaded; }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this) instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive) return;
        if (paused) ForceUnpaused();
    }

    private void Update()
    {
        if (!RuleCardsScenes.IsGameplay(SceneManager.GetActiveScene().name)) { if (paused) ForceUnpaused(); return; }
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (paused) Resume();
        else if (!ArchiveModal.IsOpen) Pause();
    }

    private void Pause()
    {
        paused = true;
        confirmReset = false;
        previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        MouseLook.isPaused = true;
        AudioListener.pause = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Resume()
    {
        paused = false;
        confirmReset = false;
        Time.timeScale = previousTimeScale;
        MouseLook.isPaused = false;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void ForceUnpaused()
    {
        paused = false;
        confirmReset = false;
        Time.timeScale = 1f;
        MouseLook.isPaused = false;
        AudioListener.pause = false;
    }

    private void Leave(string sceneName)
    {
        paused = false;
        confirmReset = false;
        RuleCardsScenes.Load(sceneName);
    }

    private void OnGUI()
    {
        if (!paused) return;
        int oldDepth = GUI.depth;
        GUI.depth = -120;
        float vw, vh;
        Matrix4x4 previous = ResponsiveIMGUI.Begin(out vw, out vh);

        RepositoryGUI.Fill(new Rect(0f, 0f, vw, vh), new Color(0f, .01f, .02f, .78f));
        string scene = SceneManager.GetActiveScene().name;
        bool inHub = scene == RuleCardsScenes.Hub;
        int buttons = inHub ? 3 : 5;
        float pw = Mathf.Min(380f, vw - 24f);
        float ph = 128f + buttons * 48f + (confirmReset ? 26f : 0f);
        Rect panel = new Rect((vw - pw) * .5f, (vh - ph) * .5f, pw, ph);
        RepositoryGUI.Fill(panel, new Color(.03f, .05f, .09f, .97f));
        RepositoryGUI.Frame(panel, new Color(.3f, 1f, 1f, .7f), 2f);

        GUI.Label(new Rect(panel.x, panel.y + 12f, pw, 32f), "PAUSED",
            RepositoryGUI.Text(24, TextAnchor.MiddleCenter, new Color(.45f, 1f, 1f), true));
        GUI.Label(new Rect(panel.x, panel.y + 44f, pw, 22f),
            "Rule Cards " + RuleCardsProgress.CardsCollected + " / " + RuleCardsProgress.TotalCards
            + (RuleCardsProgress.IsGameComplete ? "  •  Archive restored" : ""),
            RepositoryGUI.Text(13, TextAnchor.MiddleCenter, new Color(1f, .85f, .45f)));

        float bw = pw - 64f, bx = panel.x + 32f, y = panel.y + 78f;
        if (GUI.Button(new Rect(bx, y, bw, 38f), "RESUME (Esc)")) Resume();
        y += 48f;
        if (!inHub)
        {
            if (GUI.Button(new Rect(bx, y, bw, 38f), "RESTART STAGE")) Leave(scene);
            y += 48f;
            if (GUI.Button(new Rect(bx, y, bw, 38f), "RETURN TO THE ARCHIVE")) Leave(RuleCardsScenes.Hub);
            y += 48f;
        }
        if (GUI.Button(new Rect(bx, y, bw, 38f), "MAIN MENU")) Leave(RuleCardsScenes.Title);
        y += 48f;
        if (GUI.Button(new Rect(bx, y, bw, 38f), confirmReset ? "CLICK AGAIN TO ERASE" : "RESET ALL PROGRESS"))
        {
            if (confirmReset) { RuleCardsProgress.ResetAll(); Leave(RuleCardsScenes.Title); }
            else confirmReset = true;
        }
        if (confirmReset)
            GUI.Label(new Rect(panel.x + 8f, y + 42f, pw - 16f, 22f), "Erases every Rule Card, stage and best time.",
                RepositoryGUI.Text(12, TextAnchor.MiddleCenter, new Color(1f, .5f, .4f)));

        // Swallow clicks so nothing underneath (consoles, drags) reacts while paused.
        if (Event.current != null && Event.current.isMouse) Event.current.Use();
        ResponsiveIMGUI.End(previous);
        GUI.depth = oldDepth;
    }
}
