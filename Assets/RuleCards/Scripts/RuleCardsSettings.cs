using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player settings (look, display, audio), saved in PlayerPrefs and applied everywhere:
/// MouseLook / ArchiveGravityPlayer read the look values, CameraFOV and HeadBob read the camera
/// values, RuleCardsPostFX reads brightness and bloom, FrameRateLimiter applies the frame cap.
/// </summary>
public static class RuleCardsSettings
{
    private const string P = "RuleCards.Settings.";
    public static readonly int[] FrameCaps = { 30, 60, 120, 144, 165, 240, 0 };

    public static event Action Changed;

    // ---------------------------------------------------------------- controls
    /// <summary>Multiplier on the base look speed (1 = default).</summary>
    public static float Sensitivity { get { return F("Sensitivity", 1f); } set { Set("Sensitivity", Mathf.Clamp(value, .1f, 4f)); } }
    public static bool InvertY { get { return B("InvertY", false); } set { Set("InvertY", value); } }
    /// <summary>0 = raw mouse, 1 = heavy smoothing.</summary>
    public static float LookSmoothing { get { return F("LookSmoothing", .15f); } set { Set("LookSmoothing", Mathf.Clamp01(value)); } }

    // ---------------------------------------------------------------- camera
    public static float FieldOfView { get { return F("FieldOfView", 70f); } set { Set("FieldOfView", Mathf.Clamp(value, 55f, 100f)); } }
    public static bool HeadBob { get { return B("HeadBob", true); } set { Set("HeadBob", value); } }
    /// <summary>Scales camera shake, landing dip and strafe tilt (0 = off).</summary>
    public static float CameraMotion { get { return F("CameraMotion", 1f); } set { Set("CameraMotion", Mathf.Clamp01(value)); } }

    // ---------------------------------------------------------------- display
    /// <summary>Exposure offset in stops, -1..+1.</summary>
    public static float Brightness { get { return F("Brightness", 0f); } set { Set("Brightness", Mathf.Clamp(value, -1f, 1f)); } }
    public static float Bloom { get { return F("Bloom", 1f); } set { Set("Bloom", Mathf.Clamp(value, 0f, 1.5f)); } }
    public static int FrameCapIndex { get { return Mathf.Clamp(I("FrameCap", 1), 0, FrameCaps.Length - 1); } set { Set("FrameCap", Mathf.Clamp(value, 0, FrameCaps.Length - 1)); } }
    public static bool VSync { get { return B("VSync", false); } set { Set("VSync", value); } }
    public static bool ShowFps { get { return B("ShowFps", false); } set { Set("ShowFps", value); } }
    public static int Quality { get { return Mathf.Clamp(I("Quality", QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length - 1); } set { Set("Quality", value); } }
    public static bool Fullscreen { get { return B("Fullscreen", Screen.fullScreen); } set { Set("Fullscreen", value); } }

    // ---------------------------------------------------------------- audio
    public static float MasterVolume { get { return F("MasterVolume", 1f); } set { Set("MasterVolume", Mathf.Clamp01(value)); } }
    public static float MusicVolume { get { return F("MusicVolume", .8f); } set { Set("MusicVolume", Mathf.Clamp01(value)); } }
    public static float SfxVolume { get { return F("SfxVolume", 1f); } set { Set("SfxVolume", Mathf.Clamp01(value)); } }

    /// <summary>Degrees of rotation per unit of Unity's "Mouse X/Y" axis — frame-rate independent.</summary>
    public static float LookDegreesPerUnit { get { return 2.8f * Sensitivity; } }

    public static string FrameCapLabel(int index) { int cap = FrameCaps[index]; return cap == 0 ? "Unlimited" : cap + " FPS"; }

    // ---------------------------------------------------------------- apply

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        // First launch of a build: pick a quality level the GPU can carry instead of always High Fidelity.
        if (!Application.isEditor && !PlayerPrefs.HasKey(P + "Quality")) Set("Quality", AutoQuality());
        if (QualitySettings.GetQualityLevel() != Quality) QualitySettings.SetQualityLevel(Quality, true);
        ApplyFrameRate();
        AudioListener.volume = MasterVolume;
        var host = new GameObject("RuleCards_Settings");
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<RuleCardsFpsCounter>();
    }

    /// <summary>Frame cap + vsync. FrameRateLimiter calls this in every scene.</summary>
    public static void ApplyFrameRate()
    {
        QualitySettings.vSyncCount = VSync ? 1 : 0;
        int cap = FrameCaps[FrameCapIndex];
        Application.targetFrameRate = VSync ? -1 : (cap == 0 ? -1 : cap);
    }

    /// <summary>Lowest level for cards with under 1.5 GB VRAM, middle under 3.5 GB, otherwise the project default.</summary>
    public static int AutoQuality()
    {
        int levels = QualitySettings.names.Length, current = QualitySettings.GetQualityLevel();
        int vram = SystemInfo.graphicsMemorySize;
        if (vram > 0 && vram < 1500) return 0;
        if (vram > 0 && vram < 3500) return Mathf.Min(current, Mathf.Max(0, levels / 2));
        return current;
    }

    public static void ApplyQuality(int level)
    {
        Quality = level;
        QualitySettings.SetQualityLevel(level, true);
        ApplyFrameRate(); // quality levels carry their own vsync setting
    }

    public static void ApplyFullscreen(bool on)
    {
        Fullscreen = on;
        Screen.fullScreenMode = on ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
    }

    public static void ApplyVolume(float v) { MasterVolume = v; AudioListener.volume = v; }

    public static void ResetToDefaults()
    {
        foreach (string k in new[] { "Sensitivity", "InvertY", "LookSmoothing", "FieldOfView", "HeadBob", "CameraMotion",
                                     "Brightness", "Bloom", "FrameCap", "VSync", "ShowFps", "MasterVolume", "MusicVolume", "SfxVolume" })
            PlayerPrefs.DeleteKey(P + k);
        PlayerPrefs.Save();
        ApplyFrameRate();
        AudioListener.volume = MasterVolume;
        Raise();
    }

    // ---------------------------------------------------------------- storage helpers
    private static float F(string k, float d) { return PlayerPrefs.GetFloat(P + k, d); }
    private static int I(string k, int d) { return PlayerPrefs.GetInt(P + k, d); }
    private static bool B(string k, bool d) { return PlayerPrefs.GetInt(P + k, d ? 1 : 0) == 1; }
    private static void Set(string k, float v) { if (!Mathf.Approximately(F(k, float.NaN), v)) { PlayerPrefs.SetFloat(P + k, v); Raise(); } }
    private static void Set(string k, int v) { if (I(k, int.MinValue) != v) { PlayerPrefs.SetInt(P + k, v); Raise(); } }
    private static void Set(string k, bool v) { Set(k, v ? 1 : 0); }
    private static void Raise() { if (Changed != null) Changed(); }

    public static void Save() { PlayerPrefs.Save(); }
}

/// <summary>Optional corner FPS readout (Settings ▸ Display ▸ Show FPS).</summary>
public sealed class RuleCardsFpsCounter : MonoBehaviour
{
    private float smoothed;
    private void Update() { float dt = Time.unscaledDeltaTime; if (dt > 0f) smoothed = Mathf.Lerp(smoothed <= 0f ? 1f / dt : smoothed, 1f / dt, .08f); }
    private void OnGUI()
    {
        if (!RuleCardsSettings.ShowFps) return;
        GUI.depth = -200;
        var style = RepositoryGUI.Text(13, TextAnchor.MiddleRight, smoothed >= 55f ? new Color(.45f, 1f, .6f) : smoothed >= 30f ? new Color(1f, .85f, .4f) : new Color(1f, .4f, .4f), true);
        GUI.Label(new Rect(Screen.width - 110f, 6f, 100f, 20f), Mathf.RoundToInt(smoothed) + " FPS", style);
    }
}

/// <summary>
/// The Settings panel (IMGUI), shared by the title screen and the pause menu.
/// Call <see cref="Draw"/> from OnGUI inside ResponsiveIMGUI; it returns false when the player closes it.
/// </summary>
public static class RuleCardsSettingsMenu
{
    private static int tab;
    private static readonly string[] Tabs = { "Controls", "Camera", "Display", "Audio" };
    private static Resolution[] resolutions;
    /// <summary>Frame the panel was closed on, so the same Esc press doesn't also close the pause menu.</summary>
    public static int LastClosedFrame = -10;

    public static bool Draw(float vw, float vh)
    {
        RuleCardsUI.ApplySkin();
        float pw = Mathf.Min(660f, vw - 24f), ph = Mathf.Min(540f, vh - 24f);
        Rect panel = new Rect((vw - pw) * .5f, (vh - ph) * .5f, pw, ph);
        RepositoryGUI.Fill(new Rect(0f, 0f, vw, vh), new Color(0f, .01f, .02f, .6f));
        RuleCardsUI.Panel(panel);
        GUI.Label(new Rect(panel.x, panel.y + 18f, pw, 38f), "Settings", RuleCardsUI.Title(30, RuleCardsUI.Cyan));
        RuleCardsUI.Divider(panel.center.x, panel.y + 62f, pw - 120f, RuleCardsUI.Gold);

        float tw = (pw - 64f) / Tabs.Length;
        for (int i = 0; i < Tabs.Length; i++)
        {
            Rect r = new Rect(panel.x + 32f + i * tw, panel.y + 76f, tw, 34f);
            if (GUI.Button(r, Tabs[i], i == tab ? RuleCardsUI.TabActive : RuleCardsUI.TabStyle)) tab = i;
        }
        RepositoryGUI.Fill(new Rect(panel.x + 32f, panel.y + 110f, pw - 64f, 1f), new Color(1f, 1f, 1f, .08f));

        float x = panel.x + 44f, w = pw - 88f, y = panel.y + 126f;
        switch (tab)
        {
            case 0:
                RuleCardsSettings.Sensitivity = Slider(ref y, x, w, "Mouse sensitivity", RuleCardsSettings.Sensitivity, .1f, 4f, RuleCardsSettings.Sensitivity.ToString("0.00") + "x");
                RuleCardsSettings.InvertY = Toggle(ref y, x, w, "Invert look (Y)", RuleCardsSettings.InvertY);
                RuleCardsSettings.LookSmoothing = Slider(ref y, x, w, "Look smoothing", RuleCardsSettings.LookSmoothing, 0f, 1f,
                    RuleCardsSettings.LookSmoothing <= .01f ? "Raw" : Mathf.RoundToInt(RuleCardsSettings.LookSmoothing * 100f) + "%");
                Note(ref y, x, w, "Move WASD • Sprint Shift • Jump Space (hold for a higher jump) • Crouch Ctrl • Interact E • Pause Esc");
                break;
            case 1:
                RuleCardsSettings.FieldOfView = Mathf.Round(Slider(ref y, x, w, "Field of view", RuleCardsSettings.FieldOfView, 55f, 100f, Mathf.RoundToInt(RuleCardsSettings.FieldOfView) + "°"));
                RuleCardsSettings.HeadBob = Toggle(ref y, x, w, "Head bob", RuleCardsSettings.HeadBob);
                RuleCardsSettings.CameraMotion = Slider(ref y, x, w, "Camera shake / tilt", RuleCardsSettings.CameraMotion, 0f, 1f, Mathf.RoundToInt(RuleCardsSettings.CameraMotion * 100f) + "%");
                Note(ref y, x, w, "Lower camera shake and turn off head bob if you get motion sick.");
                break;
            case 2:
                RuleCardsSettings.Brightness = Slider(ref y, x, w, "Brightness", RuleCardsSettings.Brightness, -1f, 1f,
                    (RuleCardsSettings.Brightness >= 0f ? "+" : "") + RuleCardsSettings.Brightness.ToString("0.0"));
                RuleCardsSettings.Bloom = Slider(ref y, x, w, "Glow (bloom)", RuleCardsSettings.Bloom, 0f, 1.5f, Mathf.RoundToInt(RuleCardsSettings.Bloom * 100f) + "%");
                int q = RuleCardsSettings.Quality;
                int nq = Cycle(ref y, x, w, "Graphics quality", q, QualitySettings.names.Length, QualitySettings.names[q]);
                if (nq != q) RuleCardsSettings.ApplyQuality(nq);
                int f = RuleCardsSettings.FrameCapIndex;
                int nf = Cycle(ref y, x, w, "Frame rate limit", f, RuleCardsSettings.FrameCaps.Length, RuleCardsSettings.VSync ? "VSync" : RuleCardsSettings.FrameCapLabel(f));
                if (nf != f) { RuleCardsSettings.FrameCapIndex = nf; RuleCardsSettings.ApplyFrameRate(); }
                bool vs = Toggle(ref y, x, w, "VSync", RuleCardsSettings.VSync);
                if (vs != RuleCardsSettings.VSync) { RuleCardsSettings.VSync = vs; RuleCardsSettings.ApplyFrameRate(); }
                bool fs = Toggle(ref y, x, w, "Fullscreen", Screen.fullScreen);
                if (fs != Screen.fullScreen) RuleCardsSettings.ApplyFullscreen(fs);
                ResolutionRow(ref y, x, w);
                RuleCardsSettings.ShowFps = Toggle(ref y, x, w, "Show FPS", RuleCardsSettings.ShowFps);
                break;
            case 3:
                float v = Slider(ref y, x, w, "Master volume", RuleCardsSettings.MasterVolume, 0f, 1f, Mathf.RoundToInt(RuleCardsSettings.MasterVolume * 100f) + "%");
                if (!Mathf.Approximately(v, RuleCardsSettings.MasterVolume)) RuleCardsSettings.ApplyVolume(v);
                RuleCardsSettings.MusicVolume = Slider(ref y, x, w, "Music", RuleCardsSettings.MusicVolume, 0f, 1f, Mathf.RoundToInt(RuleCardsSettings.MusicVolume * 100f) + "%");
                float sfx = Slider(ref y, x, w, "Sound effects", RuleCardsSettings.SfxVolume, 0f, 1f, Mathf.RoundToInt(RuleCardsSettings.SfxVolume * 100f) + "%");
                if (!Mathf.Approximately(sfx, RuleCardsSettings.SfxVolume)) { RuleCardsSettings.SfxVolume = sfx; RuleCardsAudio.Ui(.5f); }
                break;
        }

        float by = panel.yMax - 62f, bw = Mathf.Min(220f, (pw - 104f) * .5f);
        if (GUI.Button(new Rect(panel.x + 40f, by, bw, 38f), "Reset to defaults")) RuleCardsSettings.ResetToDefaults();
        bool close = GUI.Button(new Rect(panel.xMax - 40f - bw, by, bw, 38f), "Back   (Esc)", RuleCardsUI.PrimaryButton);
        if (Event.current != null && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape) { close = true; Event.current.Use(); }
        if (close) { RuleCardsSettings.Save(); LastClosedFrame = Time.frameCount; }
        return !close;
    }

    // Row grid: label column | control column | value column, with a faint rule under each row.
    private const float RowH = 38f, LabelW = .4f, ControlX = .42f, ControlW = .42f;

    private static void RowLabel(float y, float x, float w, string label)
    {
        GUI.Label(new Rect(x, y, w * LabelW, 28f), label, RuleCardsUI.Label(17, RuleCardsUI.Text));
        RepositoryGUI.Fill(new Rect(x, y + RowH - 6f, w, 1f), new Color(1f, 1f, 1f, .05f));
    }

    private static void RowValue(float y, float x, float w, string shown)
    {
        GUI.Label(new Rect(x + w * (ControlX + ControlW), y, w * (1f - ControlX - ControlW), 28f), shown,
            RuleCardsUI.Label(15, RuleCardsUI.GoldBright, TextAnchor.MiddleRight));
    }

    private static float Slider(ref float y, float x, float w, string label, float value, float min, float max, string shown)
    {
        RowLabel(y, x, w, label);
        float v = GUI.HorizontalSlider(new Rect(x + w * ControlX, y + 11f, w * ControlW - 12f, 18f), value, min, max);
        RowValue(y, x, w, shown);
        y += RowH;
        return v;
    }

    private static bool Toggle(ref float y, float x, float w, string label, bool value)
    {
        RowLabel(y, x, w, label);
        Rect r = new Rect(x + w * ControlX, y + 1f, 96f, 26f);
        if (GUI.Button(r, value ? "On" : "Off", value ? RuleCardsUI.PrimaryButton : GUI.skin.button)) value = !value;
        y += RowH;
        return value;
    }

    private static int Cycle(ref float y, float x, float w, string label, int index, int count, string shown)
    {
        RowLabel(y, x, w, label);
        float cx = x + w * ControlX, cw = w * ControlW + w * (1f - ControlX - ControlW);
        if (Arrow(new Rect(cx, y + 1f, 28f, 26f), "\u2039")) index = (index - 1 + count) % count;
        GUI.Label(new Rect(cx + 32f, y, cw - 64f, 28f), shown, RuleCardsUI.Label(16, RuleCardsUI.GoldBright, TextAnchor.MiddleCenter));
        if (Arrow(new Rect(cx + cw - 28f, y + 1f, 28f, 26f), "\u203A")) index = (index + 1) % count;
        y += RowH;
        return index;
    }

    private static void ResolutionRow(ref float y, float x, float w)
    {
        if (resolutions == null)
        {
            var unique = new List<Resolution>();
            foreach (Resolution r in Screen.resolutions)
                if (!unique.Exists(u => u.width == r.width && u.height == r.height)) unique.Add(r);
            resolutions = unique.ToArray();
        }
        if (resolutions.Length == 0) return;
        int current = 0;
        for (int i = 0; i < resolutions.Length; i++)
            if (resolutions[i].width == Screen.width && resolutions[i].height == Screen.height) current = i;
        int next = Cycle(ref y, x, w, "Resolution", current, resolutions.Length, Screen.width + " × " + Screen.height);
        if (next != current) Screen.SetResolution(resolutions[next].width, resolutions[next].height, Screen.fullScreenMode);
    }

    private static void Note(ref float y, float x, float w, string text)
    {
        GUI.Label(new Rect(x, y + 8f, w, 44f), text, RuleCardsUI.ProseItalic(15, RuleCardsUI.Muted, TextAnchor.UpperLeft));
        y += 50f;
    }

    /// <summary>Small framed button with a large chevron (the skin's button font is too small for one).</summary>
    private static bool Arrow(Rect r, string glyph)
    {
        bool clicked = GUI.Button(r, GUIContent.none);
        bool hover = r.Contains(Event.current.mousePosition);
        GUI.Label(new Rect(r.x, r.y - 3f, r.width, r.height), glyph, RuleCardsUI.Prose(26, hover ? Color.white : RuleCardsUI.GoldBright, TextAnchor.MiddleCenter));
        return clicked;
    }
}
