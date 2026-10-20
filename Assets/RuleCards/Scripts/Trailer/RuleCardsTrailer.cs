using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Media;
#endif

/// <summary>
/// Trailer recorder (editor only). RuleCards > Trailer menu queues shots and enters Play mode; this director
/// loads each shot's scene, hides the story/UI, drives a camera (or the player, for first-person shots),
/// steps time at exactly 60 fps and encodes 1920×1080 H.264 clips into Builds/Trailer/clips/.
/// IMGUI never reaches the capture camera's render texture, so no HUD appears in the footage.
/// Preview mode skips the video and only renders a still every half second (fast framing checks).
/// </summary>
[DefaultExecutionOrder(-500)]
public sealed class RuleCardsTrailer : MonoBehaviour
{
    public const string QueueKey = "RuleCards.Trailer.Queue";
    public const string PreviewKey = "RuleCards.Trailer.Preview";
    public const int Width = 1920, Height = 1080, Fps = 60;

    public static bool Active { get; private set; }

    // ------------------------------------------------------------------ context for shot scripts
    public sealed class Ctx
    {
        public RuleCardsTrailer director;
        public Camera cam;                 // the capture camera
        public Camera playerCam;
        public CharacterController player;
        public ArchiveGravityPlayer motor;
        public CardInventory cards;
        public EscapeStage escape;
        public float t;                    // shot time (negative during pre-roll)
        public float dt;
        public readonly Dictionary<string, object> vars = new Dictionary<string, object>();
        public bool firstPerson;
        /// <summary>Glyph faces and talks to the capture camera (re-applied after the story's own update).</summary>
        public bool glyphSpeaking;

        public void Note(string text) { director.log.AppendLine("    " + director.shot.id + " @" + t.ToString("0.00") + ": " + text); }

        private int followIndex;
        /// <summary>Walk/run along waypoints (body-centre heights are ignored), turning toward the next one.</summary>
        public void Follow(Vector3[] points, bool sprint, float turnRate = 5f, float pitch = 3f)
        {
            if (player == null || points == null || points.Length == 0) return;
            Vector3 p = player.transform.position;
            while (followIndex < points.Length - 1 && new Vector2(points[followIndex].x - p.x, points[followIndex].z - p.z).magnitude < 1.6f) followIndex++;
            Vector3 d = points[followIndex] - p;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            Look(yaw, pitch, turnRate);
            Move(1f, 0f, sprint);
        }

        public T Get<T>(string key) where T : class { object o; return vars.TryGetValue(key, out o) ? o as T : null; }

        /// <summary>Turn the player's view toward a world point, easing (rate per second; 0 = snap).</summary>
        public void LookAt(Vector3 target, float rate = 6f)
        {
            if (motor == null || playerCam == null) return;
            Vector3 eye = playerCam.transform.position;
            Vector3 d = target - eye;
            if (d.sqrMagnitude < .0001f) return;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            if (motor.IsInverted) pitch = -pitch;
            Look(yaw, pitch, rate);
        }

        public void Look(float yaw, float pitch, float rate = 6f)
        {
            if (motor == null) return;
            float k = rate <= 0f ? 1f : 1f - Mathf.Exp(-rate * dt);
            motor.SetLook(Mathf.LerpAngle(motor.Yaw, yaw, k), Mathf.Lerp(motor.Pitch, pitch, k));
        }

        public void Move(float forward, float strafe = 0f, bool sprint = false)
        {
            if (motor == null) return;
            motor.ExternalMove = new Vector2(strafe, forward);
            motor.ExternalSprint = sprint;
        }

        public void Jump() { if (motor != null) motor.ExternalJump = true; }

        /// <summary>Cinematic camera pose.</summary>
        public void Pose(Vector3 position, Vector3 lookAt, float fov = 50f, float roll = 0f)
        {
            cam.transform.position = position;
            Vector3 d = lookAt - position;
            if (d.sqrMagnitude > .0001f) cam.transform.rotation = Quaternion.LookRotation(d, Vector3.up) * Quaternion.Euler(0f, 0f, roll);
            cam.fieldOfView = fov;
        }

        /// <summary>0..1 progress of the shot time between a and b, smoothstepped.</summary>
        public float Ease(float a, float b) { return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, t)); }
        public float Lin(float a, float b) { return Mathf.Clamp01(Mathf.InverseLerp(a, b, t)); }
        /// <summary>True on the single frame where shot time crosses <paramref name="at"/>.</summary>
        private readonly HashSet<float> fired = new HashSet<float>();
        public bool At(float at) { if (t < at || fired.Contains(at)) return false; fired.Add(at); return t - at < .25f; }
    }

    public sealed class Shot
    {
        public string id, scene;
        public float duration = 5f, preroll = 1.2f;
        public bool firstPerson;
        public Action<Ctx> setup;
        public Action<Ctx> tick;   // every frame from -preroll to duration (camera pose for cinematic shots)
        public Action<Ctx> teardown;
    }

    // ------------------------------------------------------------------ boot
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
#if UNITY_EDITOR
        string queue = SessionState.GetString(QueueKey, "");
        if (string.IsNullOrEmpty(queue)) return;
        bool preview = SessionState.GetBool(PreviewKey, false);
        SessionState.EraseString(QueueKey);
        SessionState.EraseBool(PreviewKey);
        Active = true;
        RuleCardsStory.Suppressed = true;
        var go = new GameObject("RuleCards_TrailerDirector");
        DontDestroyOnLoad(go);
        var d = go.AddComponent<RuleCardsTrailer>();
        d.preview = preview;
        d.ids = queue.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
#endif
    }

    private bool preview;
    private string[] ids = new string[0];
    private Ctx ctx;
    internal Shot shot;
    private bool running;
    private RenderTexture rt;
    private Texture2D frame;
    private string outDir;
    internal readonly StringBuilder log = new StringBuilder();

    public static string OutputFolder
    {
        get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Trailer", "clips")); }
    }

    private IEnumerator Start()
    {
        outDir = OutputFolder;
        Directory.CreateDirectory(outDir);
        int oldQuality = QualitySettings.GetQualityLevel();
        QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, true);
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        float oldVolume = AudioListener.volume;
        AudioListener.volume = 0f;
        rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1, name = "TrailerRT" };
        rt.Create();
        frame = new Texture2D(Width, Height, TextureFormat.RGBA32, false);

        log.AppendLine("RuleCards trailer " + (preview ? "PREVIEW" : "RECORD") + "  " + DateTime.Now);
        foreach (string id in ids)
        {
            Shot s = RuleCardsTrailerShots.Find(id);
            if (s == null) { log.AppendLine(id + ": unknown shot"); continue; }
            yield return Run(s);
        }

        File.WriteAllText(Path.Combine(outDir, preview ? "preview_log.txt" : "record_log.txt"), log.ToString());
        Time.captureFramerate = 0;
        AudioListener.volume = oldVolume;
        QualitySettings.SetQualityLevel(oldQuality, true);
        RuleCardsStory.Suppressed = false;
        Active = false;
        Debug.Log("[Trailer] done\n" + log);
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#endif
    }

    private IEnumerator Run(Shot s)
    {
        shot = s;
        Time.captureFramerate = 0;
        SceneManager.LoadScene(s.scene);
        yield return null;
        yield return null;
        Time.captureFramerate = Fps;

        ctx = new Ctx { director = this, firstPerson = s.firstPerson, dt = 1f / Fps };
        ctx.player = FindObjectOfType<CharacterController>();
        ctx.playerCam = Camera.main;
        if (ctx.player != null)
        {
            ctx.motor = ctx.player.GetComponent<ArchiveGravityPlayer>();
            ctx.cards = ctx.player.GetComponent<CardInventory>();
            if (ctx.motor != null) ctx.motor.ExternalControl = true;
        }
        ctx.escape = FindObjectOfType<EscapeStage>();
        MouseLook.isPaused = false;

        var camGo = new GameObject("Trailer_CaptureCam");
        SceneManager.MoveGameObjectToScene(camGo, SceneManager.GetActiveScene());
        ctx.cam = camGo.AddComponent<Camera>();
        if (ctx.playerCam != null)
        {
            ctx.cam.CopyFrom(ctx.playerCam);
            camGo.transform.SetPositionAndRotation(ctx.playerCam.transform.position, ctx.playerCam.transform.rotation);
        }
        ctx.cam.targetTexture = rt;
        ctx.cam.nearClipPlane = s.firstPerson ? .03f : .1f;
        ctx.cam.farClipPlane = Mathf.Max(ctx.cam.farClipPlane, 400f);
        var data = ctx.cam.GetUniversalAdditionalCameraData();
        if (data != null)
        {
            data.renderPostProcessing = true;
            data.renderShadows = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.volumeTrigger = camGo.transform;
            if (ctx.playerCam != null)
            {
                var src = ctx.playerCam.GetUniversalAdditionalCameraData();
                if (src != null) data.volumeLayerMask = src.volumeLayerMask;
            }
        }
        // The on-screen player camera stops drawing (it stays enabled so Camera.main and its scripts keep working).
        if (ctx.playerCam != null) ctx.playerCam.cullingMask = 0;
        if (preview) ctx.cam.enabled = false;

        if (s.setup != null) s.setup(ctx);

        // Pre-roll: simulate without capturing so physics, glows and the autopilot settle.
        running = true;
        int prerollFrames = Mathf.RoundToInt(s.preroll * Fps);
        for (int i = 0; i < prerollFrames; i++)
        {
            ctx.t = (i - prerollFrames) / (float)Fps;
            yield return null;
        }

        int frames = Mathf.RoundToInt(s.duration * Fps);
        string clipPath = Path.Combine(outDir, s.id + ".mp4");
        string stillDir = Path.Combine(outDir, "stills");
        Directory.CreateDirectory(stillDir);
        foreach (string old in Directory.GetFiles(stillDir, s.id + "_*.jpg")) File.Delete(old);
#if UNITY_EDITOR
        MediaEncoder encoder = null;
        if (!preview)
        {
            if (File.Exists(clipPath)) File.Delete(clipPath);
            var attrs = new VideoTrackAttributes
            {
                frameRate = new MediaRational(Fps),
                width = Width,
                height = Height,
                includeAlpha = false,
                bitRateMode = VideoBitrateMode.High
            };
            encoder = new MediaEncoder(clipPath, attrs);
        }
#endif
        int stillEvery = Fps / 2;
        float started = Time.realtimeSinceStartup;
        for (int i = 0; i < frames; i++)
        {
            ctx.t = i / (float)Fps;
            bool still = i % stillEvery == 0 || i == frames - 1;
            yield return new WaitForEndOfFrame();
            if (preview && !still) continue;
            if (preview) ctx.cam.Render();
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            frame.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
            frame.Apply(false);
            RenderTexture.active = prev;
#if UNITY_EDITOR
            if (encoder != null) encoder.AddFrame(frame);
#endif
            if (still) File.WriteAllBytes(Path.Combine(stillDir, s.id + "_" + (i / (float)Fps).ToString("00.0") + ".jpg"), frame.EncodeToJPG(82));
        }
#if UNITY_EDITOR
        if (encoder != null) encoder.Dispose();
#endif
        running = false;
        if (s.teardown != null) s.teardown(ctx);
        log.AppendLine(string.Format("{0,-20} {1} {2:0.0}s  {3} frames  took {4:0}s{5}", s.id, s.scene, s.duration, frames,
            Time.realtimeSinceStartup - started, ctx.player != null ? "  player end " + ctx.player.transform.position.ToString("F1") : ""));
        if (ctx.motor != null) ctx.motor.ExternalControl = false;
        GlyphGuide.CameraOverride = null;
        GlyphGuide.Spotlight = false;
        GlyphGuide.Speaking = false;
    }

    private void Update()
    {
        if (!running || ctx == null || shot == null) return;
        ctx.dt = Time.deltaTime;
        MouseLook.isPaused = false;
        if (ctx.motor != null) ctx.motor.ExternalMove = Vector2.zero;
        if (shot.tick != null) shot.tick(ctx);
    }

    private void LateUpdate()
    {
        if (!running || ctx == null) return;
        if (ctx.glyphSpeaking) { GlyphGuide.CameraOverride = ctx.cam; GlyphGuide.Speaking = true; }
        if (!ctx.firstPerson || ctx.playerCam == null) return;
        ctx.cam.transform.SetPositionAndRotation(ctx.playerCam.transform.position, ctx.playerCam.transform.rotation);
        ctx.cam.fieldOfView = ctx.playerCam.fieldOfView;
    }

    private void OnDestroy()
    {
        if (rt != null) { rt.Release(); Destroy(rt); }
        if (frame != null) Destroy(frame);
        Time.captureFramerate = 0;
        RuleCardsStory.Suppressed = false;
        Active = false;
    }
}
