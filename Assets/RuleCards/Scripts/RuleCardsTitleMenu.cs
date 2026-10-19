using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Title screen for the RuleCards game (scene RuleCards_Title, first in the build).
/// Continue / New Game / Quit, with saved progress shown. Pure IMGUI so it matches the in-game consoles.
/// </summary>
public sealed class RuleCardsTitleMenu : MonoBehaviour
{
    public string gameTitle = "THE ARCHIVE";
    public string subtitle = "RULE CARDS";

    private static readonly string[] RuleNames = { "KINETIC LINK", "INVERT GRAVITY", "SOLIDIFY AIR" };
    private static readonly string[] RuleIds = { "KineticLink", "InvertGravity", "SolidifyAir" };
    private static readonly Color[] RuleColors = {
        new Color(1f, .3f, .85f), new Color(.6f, .5f, 1f), new Color(1f, .85f, .4f)
    };

    private bool confirmNewGame;
    private float shownAt;
    private bool backdrop;

    // Backup: if the component is ever missing from the title scene, add it at load.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != RuleCardsScenes.Title) return;
        foreach (RuleCardsTitleMenu existing in FindObjectsOfType<RuleCardsTitleMenu>(true))
            if (existing.gameObject.scene == scene) return;
        var root = new GameObject("RuleCards_TitleMenu");
        SceneManager.MoveGameObjectToScene(root, scene);
        root.AddComponent<RuleCardsTitleMenu>();
    }

    private void Start()
    {
        shownAt = Time.unscaledTime;
        backdrop = GameObject.Find("DECOR_Generated") != null;
        Time.timeScale = 1f;
        MouseLook.isPaused = false;
        AudioListener.pause = false;
    }

    private void Update()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (RuleCardsProgress.HasAnyProgress) Continue();
            else NewGame();
        }
        else if (Input.GetKeyDown(KeyCode.Escape)) confirmNewGame = false;
    }

    private void Continue() { RuleCardsScenes.Load(RuleCardsScenes.Resolve(RuleCardsScenes.Repository) == RuleCardsScenes.Escape ? RuleCardsScenes.Escape : RuleCardsScenes.Hub); }

    private void NewGame()
    {
        RuleCardsProgress.ResetAll();
        RuleCardsScenes.Load(RuleCardsScenes.Hub);
    }

    private static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnGUI()
    {
        float vw, vh;
        Matrix4x4 previous = ResponsiveIMGUI.Begin(out vw, out vh);
        float t = Time.unscaledTime;
        float intro = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - shownAt) / 1.2f));

        float w = Mathf.Min(560f, vw - 32f);
        float x = (vw - w) * .5f;
        float y = vh * .16f;
        if (backdrop)
        {
            // The 3D archive diorama shows through: fade in from black, keep a soft panel behind the text.
            RepositoryGUI.Fill(new Rect(0f, 0f, vw, vh), new Color(.008f, .022f, .035f, Mathf.Lerp(1f, .18f, intro)));
            RepositoryGUI.Fill(new Rect(x - 28f, y - 24f, w + 56f, vh - y - 24f), new Color(.006f, .016f, .028f, .55f * intro));
        }
        else
        {
            RepositoryGUI.Fill(new Rect(0f, 0f, vw, vh), new Color(.008f, .022f, .035f, 1f));
            DrawDriftingCards(vw, vh, t, intro);
        }
        GUI.Label(new Rect(x, y, w, 64f), gameTitle,
            RepositoryGUI.Text(Mathf.Clamp(Mathf.RoundToInt(vw / 18f), 30, 52), TextAnchor.MiddleCenter, new Color(.45f, 1f, 1f, intro), true));
        GUI.Label(new Rect(x, y + 62f, w, 26f), subtitle,
            RepositoryGUI.Text(16, TextAnchor.MiddleCenter, new Color(.8f, .9f, 1f, .8f * intro)));

        // Rule chips: filled once the card is collected.
        float chipW = Mathf.Min(150f, (w - 24f) / 3f), chipY = y + 104f;
        float chipX = vw * .5f - (chipW * 3f + 24f) * .5f;
        for (int i = 0; i < 3; i++)
        {
            Rect chip = new Rect(chipX + i * (chipW + 12f), chipY, chipW, 30f);
            bool has = RuleCardsProgress.HasCard(RuleIds[i]);
            if (has) RepositoryGUI.Fill(chip, RepositoryGUI.WithAlpha(RuleColors[i], .85f * intro));
            RepositoryGUI.Frame(chip, RepositoryGUI.WithAlpha(RuleColors[i], intro), 2f);
            GUI.Label(chip, RuleNames[i], RepositoryGUI.Text(12, TextAnchor.MiddleCenter,
                has ? new Color(.03f, .03f, .06f, intro) : RepositoryGUI.WithAlpha(RuleColors[i], .8f * intro), true));
        }

        string status;
        if (RuleCardsProgress.IsGameComplete)
        {
            status = "YOU ESCAPED THE ARCHIVE";
            float best = RuleCardsProgress.BestEscapeTime;
            if (best > 0f) status += "  •  Best escape " + (Mathf.RoundToInt(best) / 60) + ":" + (Mathf.RoundToInt(best) % 60).ToString("00");
        }
        else if (RuleCardsProgress.IsRepositoryComplete)
            status = "The Repository is restored. The escape remains.";
        else if (RuleCardsProgress.HasAnyProgress)
            status = "Rule Cards recovered: " + RuleCardsProgress.CardsCollected + " / " + RuleCardsProgress.TotalCards;
        else status = "The Archive is waiting.";
        GUI.Label(new Rect(x, chipY + 40f, w, 24f), status,
            RepositoryGUI.Text(14, TextAnchor.MiddleCenter, new Color(1f, .85f, .45f, intro)));

        if (intro < .6f) { ResponsiveIMGUI.End(previous); return; }

        bool hasProgress = RuleCardsProgress.HasAnyProgress;
        float bw = Mathf.Min(300f, w), bx = vw * .5f - bw * .5f, by = Mathf.Max(chipY + 84f, vh * .5f);
        if (hasProgress)
        {
            if (GUI.Button(new Rect(bx, by, bw, 44f), "CONTINUE (Enter)")) Continue();
            by += 54f;
            if (GUI.Button(new Rect(bx, by, bw, 44f), confirmNewGame ? "CLICK AGAIN — ERASE AND START OVER" : "NEW GAME"))
            {
                if (confirmNewGame) NewGame();
                else confirmNewGame = true;
            }
            if (confirmNewGame)
                GUI.Label(new Rect(x, by + 46f, w, 20f), "Erases every Rule Card, stage and best time.",
                    RepositoryGUI.Text(12, TextAnchor.MiddleCenter, new Color(1f, .5f, .4f)));
            by += confirmNewGame ? 74f : 54f;
        }
        else
        {
            if (GUI.Button(new Rect(bx, by, bw, 44f), "NEW GAME (Enter)")) NewGame();
            by += 54f;
        }
        if (GUI.Button(new Rect(bx, by, bw, 44f), "QUIT")) Quit();

        GUI.Label(new Rect(x, vh - 40f, w, 24f), "WASD move • Mouse look • E interact • Esc pause",
            RepositoryGUI.Text(12, TextAnchor.MiddleCenter, new Color(.55f, .62f, .7f)));
        ResponsiveIMGUI.End(previous);
    }

    /// <summary>Nine slow card outlines drifting behind the title, three per rule colour.</summary>
    private static void DrawDriftingCards(float vw, float vh, float t, float intro)
    {
        for (int i = 0; i < 9; i++)
        {
            float speed = .018f + .006f * (i % 3);
            float fx = Mathf.Repeat(i * .137f + t * speed, 1.2f) - .1f;
            float fy = .12f + .76f * Mathf.Repeat(i * .391f, 1f) + .03f * Mathf.Sin(t * .7f + i);
            float h = 70f + 22f * (i % 3), wCard = h * .62f;
            Rect card = new Rect(fx * vw - wCard * .5f, fy * vh - h * .5f, wCard, h);
            Color c = RuleColors[i % 3];
            RepositoryGUI.Fill(card, RepositoryGUI.WithAlpha(c, .06f * intro));
            RepositoryGUI.Frame(card, RepositoryGUI.WithAlpha(c, .28f * intro), 2f);
        }
    }
}
