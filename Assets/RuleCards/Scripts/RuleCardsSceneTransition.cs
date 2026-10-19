using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Shared arrival feedback; never changes controls, saved progress or the frame limiter.</summary>
public sealed class RuleCardsSceneTransition : MonoBehaviour
{
    private static RuleCardsSceneTransition instance;
    private float elapsed = 10f;
    private string sceneTitle;
    private AudioSource source;
    private AudioClip arrivalSound;
    private GUIStyle titleStyle;
    private const float Duration = 2.3f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;
        instance = new GameObject("RuleCards_SceneArrival").AddComponent<RuleCardsSceneTransition>();
        DontDestroyOnLoad(instance.gameObject);
    }

    private void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0.28f;
        arrivalSound = CreateArrivalSound();
        source.clip = arrivalSound;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive) return;
        BeginArrival(scene.name);
    }

    public void BeginArrival(string sceneName)
    {
        switch (sceneName)
        {
            case "ArchiveHub": sceneTitle = RuleCardsProgress.IsGameComplete ? "THE ARCHIVE — ESCAPED" : "THE ARCHIVE"; break;
            case "Stage_01_KineticLink": sceneTitle = "KINETIC LINK"; break;
            case "Stage_02_InvertGravity": sceneTitle = "INVERT GRAVITY"; break;
            case "Stage_03_SolidifyAir": sceneTitle = "SOLIDIFY AIR"; break;
            case "Repository_Final": sceneTitle = "THE FINAL REPOSITORY"; break;
            case "Stage_04_Escape": sceneTitle = "THE UNWRITTEN ARCHIVE"; break;
            default: elapsed = Duration; source.Stop(); return;
        }
        elapsed = 0f;
        source.Stop();
        if (!RuleCardsAudio.Has("portal")) source.Play(); // portals play their own sound now
    }

    private void Update()
    {
        if (elapsed < Duration) elapsed += Time.unscaledDeltaTime;
    }

    private AudioClip CreateArrivalSound()
    {
        const int rate = 22050;
        var samples = new float[(int)(rate * 1.35f)];
        var random = new System.Random(831);
        float smoothNoise = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)rate;
            smoothNoise = Mathf.Lerp(smoothNoise, (float)random.NextDouble() * 2f - 1f, 0.13f);
            float whooshEnvelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.8f));
            float whoosh = smoothNoise * whooshEnvelope * 0.28f;
            float age = t - 0.24f;
            float tone = 0f;
            if (age > 0f)
            {
                float envelope = Mathf.Clamp01(age / 0.025f) * Mathf.Exp(-age * 4.5f);
                tone = (Mathf.Sin(age * 659.25f * Mathf.PI * 2f)
                    + 0.55f * Mathf.Sin(age * 987.77f * Mathf.PI * 2f)) * envelope * 0.18f;
            }
            samples[i] = (whoosh + tone) * Mathf.Clamp01((1.35f - t) / 0.1f);
        }
        var clip = AudioClip.Create("Archive_PortalArrival", samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void OnGUI()
    {
        if (elapsed >= Duration) return;
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label) {
                alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true
            };
            titleStyle.normal.textColor = new Color(0.4f, 1f, 1f);
        }
        float width, height;
        Matrix4x4 previous = ResponsiveIMGUI.Begin(out width, out height);
        Color oldColor = GUI.color;
        int oldDepth = GUI.depth;
        GUI.depth = -90;
        float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 1.05f));
        GUI.color = new Color(0.005f, 0.018f, 0.025f, fade);
        GUI.DrawTexture(new Rect(0f, 0f, width, height), Texture2D.whiteTexture);

        // A single expanding cyan rune ripple, not a flashing full-screen strobe.
        float progress = Mathf.Clamp01(elapsed / 1.1f);
        float radius = Mathf.Lerp(28f, Mathf.Min(width, height) * 0.42f, progress);
        float rippleAlpha = Mathf.Sin(progress * Mathf.PI) * 0.55f;
        Vector2 center = new Vector2(width * 0.5f, height * 0.48f);
        GUI.color = new Color(0.15f, 1f, 1f, rippleAlpha);
        for (int i = 0; i < 32; i++)
        {
            float a = i * Mathf.PI * 2f / 32f;
            float b = (i + 0.7f) * Mathf.PI * 2f / 32f;
            Vector2 from = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            Vector2 to = center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius;
            DrawLine(from, to, 2f);
            if (i % 4 == 0)
                DrawLine(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (radius + 7f),
                    center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (radius + 19f), 2f);
        }
        float titleAlpha = Mathf.Clamp01(elapsed / 0.2f) * Mathf.Clamp01((Duration - elapsed) / 0.55f);
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.wordWrap = true;
        titleStyle.normal.textColor = new Color(0.4f, 1f, 1f);
        titleStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(width / 32f), 16, 28);
        GUI.color = new Color(1f, 1f, 1f, titleAlpha);
        GUI.Label(new Rect(12f, height * 0.7f, width - 24f, Mathf.Min(80f, height * 0.2f)),
            sceneTitle, titleStyle);
        GUI.depth = oldDepth;
        ResponsiveIMGUI.End(previous);
        GUI.color = oldColor;
    }

private static void DrawLine(Vector2 from, Vector2 to, float thickness)
    {
        Matrix4x4 previous = GUI.matrix;
        float angle = Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;
        GUI.matrix = previous * Matrix4x4.TRS(new Vector3(from.x, from.y, 0f),
            Quaternion.Euler(0f, 0f, angle), Vector3.one);
        GUI.DrawTexture(new Rect(0f, -thickness * 0.5f, Vector2.Distance(from, to), thickness),
            Texture2D.whiteTexture);
        GUI.matrix = previous;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (arrivalSound != null) Destroy(arrivalSound);
        if (instance == this) instance = null;
    }
}
