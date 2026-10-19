using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Top-left timer card shown in every gameplay scene (hub, stages 1–3, Repository, Escape): where you are, time spent in this scene, and the whole-run total. Stage HUDs stack under it
/// via <see cref="NextY"/>. Installs itself.
/// </summary>
public sealed class RuleCardsRunHud : MonoBehaviour
{
    public const float Margin = 16f;
    private const float Width = 300f, Height = 70f, Gap = 8f;

    private static RuleCardsRunHud instance;

    /// <summary>Optional per-attempt clock for the "Stage" slot (Escape uses its own attempt timer). Null = scene split.</summary>
    public static System.Func<float> StageTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;
        var go = new GameObject("RuleCards_RunHud");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<RuleCardsRunHud>();
    }

    private static int suppressedFrame = -10;

    /// <summary>Call every frame while a full-screen moment (ending, cutscene) owns the screen.</summary>
    public static void Suppress() { suppressedFrame = Time.frameCount; }

    /// <summary>True while the card is on screen (any gameplay scene, not paused, not suppressed).</summary>
    public static bool Visible
    {
        get
        {
            return RuleCardsScenes.IsGameplay(SceneManager.GetActiveScene().name) && !MouseLook.isPaused
                && Time.frameCount - suppressedFrame > 1;
        }
    }

    /// <summary>Virtual-space y where a stage's own HUD should start (under the timer card when shown).</summary>
    public static float NextY { get { return Visible ? Margin + Height + Gap : Margin; } }

    private static void Names(string scene, out string eyebrow, out string title)
    {
        switch (scene)
        {
            case "ArchiveHub": eyebrow = "Hub"; title = "The Archive"; break;
            case "Stage_01_KineticLink": eyebrow = "Stage I"; title = "Kinetic Link"; break;
            case "Stage_02_InvertGravity": eyebrow = "Stage II"; title = "Invert Gravity"; break;
            case "Stage_03_SolidifyAir": eyebrow = "Stage III"; title = "Solidify Air"; break;
            case "Repository_Final": eyebrow = "The Archive"; title = "Final Repository"; break;
            case "Stage_04_Escape": eyebrow = "Escape"; title = "The Unwritten Archive"; break;
            default: eyebrow = ""; title = scene; break;
        }
    }

    private void OnGUI()
    {
        if (!Visible) return;
        string scene = SceneManager.GetActiveScene().name;
        float vw, vh;
        Matrix4x4 previous = ResponsiveIMGUI.Begin(out vw, out vh);
        int oldDepth = GUI.depth;
        GUI.depth = 10; // behind puzzle consoles and menus

        string eyebrow, title;
        Names(scene, out eyebrow, out title);
        Rect box = new Rect(Margin, Margin, Mathf.Min(Width, vw - Margin * 2f), Height);
        RuleCardsUI.HudCard(box);
        float x = box.x + 14f, w = box.width - 28f;
        GUI.Label(new Rect(x, box.y + 7f, w, 14f), eyebrow, RuleCardsUI.Label(12, RuleCardsUI.Gold));
        GUI.Label(new Rect(x, box.y + 20f, w, 22f), title, RuleCardsUI.Title(16, RuleCardsUI.Text, TextAnchor.MiddleLeft));

        float y = box.y + 44f;
        bool done = RuleCardsRunTimer.Finished;
        if (!done || StageTime != null)
        {
            GUI.Label(new Rect(x, y, 44f, 20f), "Stage", RuleCardsUI.Label(12, RuleCardsUI.Muted));
            GUI.Label(new Rect(x + 42f, y, 80f, 20f), RuleCardsRunTimer.Format(StageTime != null ? StageTime() : RuleCardsRunTimer.Split(scene)),
                RuleCardsUI.Number(15, RuleCardsUI.Text, TextAnchor.MiddleLeft));
        }
        else GUI.Label(new Rect(x, y, 116f, 20f), "Run complete", RuleCardsUI.Label(12, RuleCardsUI.Gold));
        GUI.Label(new Rect(x + 120f, y, 44f, 20f), "Total", RuleCardsUI.Label(12, RuleCardsUI.Muted));
        GUI.Label(new Rect(x + 160f, y, 90f, 20f), RuleCardsRunTimer.Format(RuleCardsRunTimer.Total),
            RuleCardsUI.Number(15, RuleCardsUI.GoldBright, TextAnchor.MiddleLeft));

        GUI.depth = oldDepth;
        ResponsiveIMGUI.End(previous);
    }
}
