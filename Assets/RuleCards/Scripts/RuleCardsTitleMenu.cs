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
    private bool settingsOpen;
    private bool creditsOpen;
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
        if (settingsOpen || creditsOpen || RuleCardsCredits.LastClosedFrame >= Time.frameCount - 1) return;
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

        // Left-hand column over the 3D diorama (the ring and the giant card stay visible on the right).
        float x0 = Mathf.Clamp(vw * .065f, 28f, 90f);
        float colW = Mathf.Min(440f, vw - x0 * 2f);
        if (backdrop)
        {
            RepositoryGUI.Fill(new Rect(0f, 0f, vw, vh), new Color(.008f, .022f, .035f, Mathf.Lerp(1f, .1f, intro)));
            float fadeW = Mathf.Min(vw, x0 + colW + 260f);
            RuleCardsUI.Gradient(new Rect(0f, 0f, fadeW, vh), new Color(.01f, .02f, .035f, .9f * intro), new Color(.01f, .02f, .035f, 0f), 32, true);
        }
        else
        {
            RepositoryGUI.Fill(new Rect(0f, 0f, vw, vh), new Color(.008f, .022f, .035f, 1f));
            DrawDriftingCards(vw, vh, t, intro);
        }

        float y = Mathf.Max(28f, vh * .12f);
        GUI.Label(new Rect(x0 + 2f, y, colW, 22f), Spaced(subtitle), RuleCardsUI.Label(15, RepositoryGUI.WithAlpha(RuleCardsUI.Gold, intro)));
        y += 22f;
        int titleSize = Mathf.RoundToInt(Mathf.Clamp(vh / 8.5f, 52f, 92f));
        GUI.Label(new Rect(x0 - 2f, y, colW + 200f, titleSize * 1.25f), gameTitle,
            RuleCardsUI.Title(titleSize, RepositoryGUI.WithAlpha(RuleCardsUI.Cyan, intro), TextAnchor.MiddleLeft));
        y += titleSize * 1.25f + 6f;
        LeftRule(x0, y, colW * .8f, intro);
        y += 18f;

        y += 8f;

        string status;
        if (RuleCardsProgress.IsGameComplete) status = "You escaped the Archive.";
        else if (RuleCardsProgress.IsRepositoryComplete) status = "The Repository is restored. The escape remains.";
        else if (RuleCardsProgress.HasAnyProgress) status = RuleCardsProgress.CardsCollected + " of " + RuleCardsProgress.TotalCards + " Rule Cards recovered.";
        else status = "The Archive is waiting.";
        GUI.Label(new Rect(x0, y, colW, 26f), status, RuleCardsUI.ProseItalic(19, RepositoryGUI.WithAlpha(RuleCardsUI.GoldBright, intro), TextAnchor.MiddleLeft));
        y += 30f;

        // Run time across every stage (live total while a run is going, final total once escaped), plus bests.
        float bestEscape = RuleCardsProgress.BestEscapeTime, bestRun = RuleCardsRunTimer.BestRun, total = RuleCardsRunTimer.Total;
        if (total >= 1f || bestEscape > 0f || bestRun > 0f)
        {
            float sx = x0;
            if (total >= 1f)
            {
                string label = RuleCardsRunTimer.Finished ? "Total time" : "Run so far";
                if (RuleCardsRunTimer.Partial) label += " *";
                StatLeft(new Rect(sx, y, 140f, 46f), label, RuleCardsRunTimer.Format(total), intro);
                sx += 150f;
            }
            if (bestEscape > 0f) { StatLeft(new Rect(sx, y, 140f, 46f), "Best escape", RuleCardsRunTimer.Format(bestEscape), intro); sx += 150f; }
            if (bestRun > 0f) StatLeft(new Rect(sx, y, 140f, 46f), "Best full run", RuleCardsRunTimer.Format(bestRun), intro);
            y += 54f;
            if (RuleCardsRunTimer.Partial && total >= 1f)
            {
                GUI.Label(new Rect(x0, y - 4f, colW, 18f), "* Started on an older save, so early stages weren't timed.",
                    RuleCardsUI.ProseItalic(13, RepositoryGUI.WithAlpha(RuleCardsUI.Muted, intro), TextAnchor.MiddleLeft));
                y += 18f;
            }
        }
        y += 8f;

        if (intro < .6f) { ResponsiveIMGUI.End(previous); return; }
        if (settingsOpen)
        {
            settingsOpen = RuleCardsSettingsMenu.Draw(vw, vh);
            ResponsiveIMGUI.End(previous);
            return;
        }
        if (creditsOpen)
        {
            creditsOpen = RuleCardsCredits.Draw(vw, vh);
            ResponsiveIMGUI.End(previous);
            return;
        }

        bool hasProgress = RuleCardsProgress.HasAnyProgress;
        float bw = Mathf.Min(300f, colW), bh = 40f;
        float by = Mathf.Max(y, vh * .56f);
        if (hasProgress)
        {
            if (MenuItem(new Rect(x0 - 14f, by, bw, bh), "Continue", "Enter", true)) Continue();
            by += bh + 4f;
            if (MenuItem(new Rect(x0 - 14f, by, bw, bh), confirmNewGame ? "Erase everything?" : "New Game", confirmNewGame ? "Click again" : null, false, confirmNewGame))
            {
                if (confirmNewGame) NewGame();
                else confirmNewGame = true;
            }
            by += bh + 4f;
            if (confirmNewGame)
            {
                GUI.Label(new Rect(x0 + 12f, by - 4f, colW, 20f), "Every Rule Card, stage and best time will be lost.",
                    RuleCardsUI.ProseItalic(14, RuleCardsUI.Danger, TextAnchor.MiddleLeft));
                by += 22f;
            }
        }
        else
        {
            if (MenuItem(new Rect(x0 - 14f, by, bw, bh), "New Game", "Enter", true)) NewGame();
            by += bh + 4f;
        }
        if (MenuItem(new Rect(x0 - 14f, by, bw, bh), "Settings", null, false)) { settingsOpen = true; confirmNewGame = false; }
        by += bh + 4f;
        if (MenuItem(new Rect(x0 - 14f, by, bw, bh), "Credits", null, false)) { creditsOpen = true; confirmNewGame = false; }
        by += bh + 4f;
        if (MenuItem(new Rect(x0 - 14f, by, bw, bh), "Quit", null, false)) Quit();

        GUI.Label(new Rect(x0, vh - 36f, vw - x0 * 2f, 20f), "WASD move   •   Mouse look   •   E interact   •   Esc pause",
            RuleCardsUI.Label(13, RepositoryGUI.WithAlpha(RuleCardsUI.Muted, .9f), TextAnchor.MiddleLeft));
        GUI.Label(new Rect(x0, vh - 36f, vw - x0 * 2f, 20f), "v" + Application.version,
            RuleCardsUI.Label(13, RepositoryGUI.WithAlpha(RuleCardsUI.Muted, .6f), TextAnchor.MiddleRight));
        ResponsiveIMGUI.End(previous);
    }

    private static readonly string[] Numerals = { "I", "II", "III" };

    private static string Spaced(string s)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in s) { sb.Append(c); sb.Append(c == ' ' ? "  " : " "); }
        return sb.ToString().TrimEnd();
    }

    private static void LeftRule(float x, float y, float width, float alpha)
    {
        RuleCardsUI.Diamond(new Vector2(x + 4f, y + .5f), 6f, RuleCardsUI.Gold, alpha);
        RuleCardsUI.Gradient(new Rect(x + 12f, y, width, 1f), RepositoryGUI.WithAlpha(RuleCardsUI.Gold, .8f * alpha), RepositoryGUI.WithAlpha(RuleCardsUI.Gold, 0f), 16, true);
    }

    private static void StatLeft(Rect r, string label, string value, float alpha)
    {
        GUI.Label(new Rect(r.x, r.y, r.width, 16f), label, RuleCardsUI.Label(13, RepositoryGUI.WithAlpha(RuleCardsUI.Muted, alpha)));
        GUI.Label(new Rect(r.x, r.y + 16f, r.width, 30f), value, RuleCardsUI.Number(24, RepositoryGUI.WithAlpha(RuleCardsUI.Text, alpha), TextAnchor.MiddleLeft));
    }

    /// <summary>A menu line: hover reveals a gold bar and tint; optional key hint on the right.</summary>
    private static string hoverLabel;

    private static bool MenuItem(Rect r, string label, string hint, bool primary, bool danger = false)
    {
        bool hover = r.Contains(Event.current.mousePosition);
        if (hover && hoverLabel != label) { hoverLabel = label; RuleCardsAudio.Ui(.22f, 1.6f); }
        else if (!hover && hoverLabel == label) hoverLabel = null;
        if (hover) RuleCardsUI.Gradient(r, new Color(.86f, .71f, .43f, .16f), new Color(.86f, .71f, .43f, 0f), 16, true);
        if (hover || primary) RepositoryGUI.Fill(new Rect(r.x, r.y + 8f, 3f, r.height - 16f), hover ? RuleCardsUI.GoldBright : RepositoryGUI.WithAlpha(RuleCardsUI.Gold, .6f));
        Color c = danger ? RuleCardsUI.Danger : hover ? Color.white : primary ? RuleCardsUI.GoldBright : RuleCardsUI.Text;
        GUIStyle labelStyle = RuleCardsUI.Title(20, c, TextAnchor.MiddleLeft);
        GUI.Label(new Rect(r.x + 14f, r.y, r.width - 20f, r.height), label, labelStyle);
        if (!string.IsNullOrEmpty(hint))
        {
            // Key chip sits just after the label, so it reads as part of the item.
            float lw = labelStyle.CalcSize(new GUIContent(label)).x;
            GUIStyle hintStyle = RuleCardsUI.Label(12, RuleCardsUI.Muted, TextAnchor.MiddleCenter);
            float hw = hintStyle.CalcSize(new GUIContent(hint)).x + 14f;
            Rect chip = new Rect(r.x + 14f + lw + 14f, r.y + r.height * .5f - 10f, hw, 20f);
            RepositoryGUI.Fill(chip, new Color(1f, 1f, 1f, .04f));
            RepositoryGUI.Frame(chip, RepositoryGUI.WithAlpha(RuleCardsUI.Muted, .45f), 1f);
            GUI.Label(chip, hint, hintStyle);
        }
        return GUI.Button(r, GUIContent.none, GUIStyle.none);
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
