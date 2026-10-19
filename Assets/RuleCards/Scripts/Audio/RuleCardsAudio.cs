using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// RuleCards sound: one-shots (2D and 3D), keep-alive loops, music per scene with crossfades and
/// dialogue ducking, ambience, a per-scene reverb zone that follows the listener, UI click sounds,
/// and footsteps for the player. Clips live in Resources/Audio/{Sfx,Music,Ambience}. Installs itself.
/// Music and UI bypass the reverb; everything in the world gets it.
/// </summary>
public sealed class RuleCardsAudio : MonoBehaviour
{
    private static RuleCardsAudio instance;
    private static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

    private AudioSource[] pool2D, pool3D;
    private int next2D, next3D;
    private AudioSource ui, ambience;
    private AudioSource[] music = new AudioSource[2];
    private int musicIndex;
    private string musicName;
    private float musicFade = 1f, duck = 1f;
    private AudioReverbZone reverb;

    private sealed class Loop { public AudioSource source; public float target, lastTouch; }
    private readonly Dictionary<string, Loop> loops = new Dictionary<string, Loop>();

    private int lastHot;
    private static float lastUiAt = -1f;

    // ------------------------------------------------------------------ install
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; cache.Clear(); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;
        var go = new GameObject("RuleCards_Audio");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<RuleCardsAudio>();
    }

    private AudioSource NewSource(string name, bool spatial, bool reverbOn)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.spatialBlend = spatial ? 1f : 0f;
        s.rolloffMode = AudioRolloffMode.Logarithmic;
        s.minDistance = 2.5f;
        s.maxDistance = 40f;
        s.dopplerLevel = 0f;
        s.bypassReverbZones = !reverbOn;
        s.reverbZoneMix = reverbOn ? 1f : 0f;
        return s;
    }

    private void Awake()
    {
        pool2D = new AudioSource[10];
        for (int i = 0; i < pool2D.Length; i++) pool2D[i] = NewSource("Sfx2D_" + i, false, true);
        pool3D = new AudioSource[14];
        for (int i = 0; i < pool3D.Length; i++) pool3D[i] = NewSource("Sfx3D_" + i, true, true);
        ui = NewSource("UI", false, false);
        ui.ignoreListenerPause = true; // menu clicks still sound while paused
        for (int i = 0; i < 2; i++)
        {
            music[i] = NewSource("Music_" + i, false, false);
            music[i].loop = true;
            music[i].ignoreListenerPause = true;
            music[i].priority = 0;
        }
        ambience = NewSource("Ambience", false, true);
        ambience.loop = true;
        ambience.reverbZoneMix = .4f;

        var rz = new GameObject("ReverbZone");
        rz.transform.SetParent(transform, false);
        reverb = rz.AddComponent<AudioReverbZone>();
        reverb.minDistance = 5000f;
        reverb.maxDistance = 6000f;
        reverb.reverbPreset = AudioReverbPreset.Off;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    // ------------------------------------------------------------------ clips
    public static AudioClip Clip(string name)
    {
        AudioClip c;
        if (cache.TryGetValue(name, out c)) return c;
        c = Resources.Load<AudioClip>("Audio/Sfx/" + name);
        if (c == null) c = Resources.Load<AudioClip>("Audio/Music/" + name);
        if (c == null) c = Resources.Load<AudioClip>("Audio/Ambience/" + name);
        cache[name] = c;
        return c;
    }

    public static bool Has(string name) { return Clip(name) != null; }

    private static float Sfx { get { return RuleCardsSettings.SfxVolume; } }

    /// <summary>2D one-shot. Returns false when the clip doesn't exist (callers can fall back).</summary>
    public static bool Play(string name, float volume = 1f, float pitch = 1f, float jitter = .04f)
    {
        AudioClip c = Clip(name);
        if (instance == null || c == null) return false;
        AudioSource s = instance.pool2D[instance.next2D];
        instance.next2D = (instance.next2D + 1) % instance.pool2D.Length;
        s.pitch = pitch * (1f + Random.Range(-jitter, jitter));
        s.PlayOneShot(c, volume * Sfx);
        return true;
    }

    /// <summary>3D one-shot at a world position.</summary>
    public static bool PlayAt(string name, Vector3 position, float volume = 1f, float pitch = 1f, float jitter = .04f, float minDistance = 2.5f)
    {
        AudioClip c = Clip(name);
        if (instance == null || c == null) return false;
        AudioSource s = instance.pool3D[instance.next3D];
        instance.next3D = (instance.next3D + 1) % instance.pool3D.Length;
        s.transform.position = position;
        s.minDistance = minDistance;
        s.pitch = pitch * (1f + Random.Range(-jitter, jitter));
        s.PlayOneShot(c, volume * Sfx);
        return true;
    }

    /// <summary>Random variant: name_0 … name_{count-1}.</summary>
    public static bool PlayRandom(string name, int count, float volume = 1f, Vector3? at = null, float pitch = 1f)
    {
        string n = name + "_" + Random.Range(0, count);
        return at.HasValue ? PlayAt(n, at.Value, volume, pitch, .08f) : Play(n, volume, pitch, .08f);
    }

    /// <summary>Menu/UI sound: never paused, never reverberated.</summary>
    public static void Ui(float volume = .55f, float pitch = 1f)
    {
        AudioClip c = Clip("ui_click");
        if (instance == null || c == null || Time.unscaledTime - lastUiAt < .07f) return;
        lastUiAt = Time.unscaledTime;
        instance.ui.pitch = pitch;
        instance.ui.PlayOneShot(c, volume * Sfx);
    }

    /// <summary>
    /// Keep-alive loop: call every frame with the volume you want (0 to fade it out). A loop that
    /// isn't touched for a moment fades out by itself. Pass a position for a 3D loop.
    /// </summary>
    public static void LoopAt(string key, string clip, float volume, Vector3? position = null, float pitch = 1f)
    {
        if (instance == null) return;
        Loop l;
        if (!instance.loops.TryGetValue(key, out l) || l.source == null)
        {
            AudioClip c = Clip(clip);
            if (c == null) return;
            l = new Loop { source = instance.NewSource("Loop_" + key, position.HasValue, true) };
            l.source.clip = c;
            l.source.loop = true;
            l.source.volume = 0f;
            instance.loops[key] = l;
        }
        if (position.HasValue) l.source.transform.position = position.Value;
        l.source.pitch = pitch;
        l.target = Mathf.Clamp01(volume);
        l.lastTouch = Time.unscaledTime;
    }

    // ------------------------------------------------------------------ music
    /// <summary>Crossfade to a music track (no-op if it's already playing).</summary>
    public static void PlayMusic(string name, bool loop = true)
    {
        if (instance == null || instance.musicName == name) return;
        AudioClip c = Clip(name);
        instance.musicName = name;
        instance.musicIndex = 1 - instance.musicIndex;
        AudioSource s = instance.music[instance.musicIndex];
        s.clip = c;
        s.loop = loop;
        s.volume = 0f;
        if (c != null) s.Play();
        instance.musicFade = 0f;
    }

    private static string MusicFor(string scene)
    {
        switch (scene)
        {
            case "RuleCards_Title":
            case "ArchiveHub": return "title";
            case "Stage_01_KineticLink":
            case "Stage_02_InvertGravity":
            case "Stage_03_SolidifyAir": return "stages";
            case "Repository_Final": return "repository";
            case "Stage_04_Escape": return "escape";
            default: return null;
        }
    }

    private static AudioReverbPreset ReverbFor(string scene)
    {
        switch (scene)
        {
            case "ArchiveHub": return AudioReverbPreset.Auditorium;
            case "Stage_01_KineticLink": return AudioReverbPreset.StoneCorridor;
            case "Stage_02_InvertGravity": return AudioReverbPreset.Cave;
            case "Stage_03_SolidifyAir": return AudioReverbPreset.Hangar;
            case "Repository_Final": return AudioReverbPreset.Concerthall;
            case "Stage_04_Escape": return AudioReverbPreset.Arena;
            default: return AudioReverbPreset.Off;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive) return;
        string m = MusicFor(scene.name);
        if (m != null) PlayMusic(m);
        reverb.reverbPreset = ReverbFor(scene.name);
        foreach (Loop l in loops.Values) if (l.source != null) { l.source.Stop(); l.source.volume = 0f; }

        AudioClip amb = null; // creaking ambience removed (too distracting); music carries the mood
        if (amb != null) { if (ambience.clip != amb || !ambience.isPlaying) { ambience.clip = amb; ambience.Play(); } }
        else ambience.Stop();

        if (!RuleCardsScenes.IsGameplay(scene.name)) return;
        // Footsteps for whoever carries the CharacterController.
        foreach (CharacterController cc in FindObjectsOfType<CharacterController>())
            if (cc.GetComponent<RuleCardsFootsteps>() == null) cc.gameObject.AddComponent<RuleCardsFootsteps>();
        // Stone grinding on every moving door.
        foreach (ArchiveSlidingDoor d in FindObjectsOfType<ArchiveSlidingDoor>())
            if (d.GetComponent<RuleCardsStoneMover>() == null) d.gameObject.AddComponent<RuleCardsStoneMover>();
        foreach (PressurePlateDoor p in FindObjectsOfType<PressurePlateDoor>())
            if (p.door != null && p.door.GetComponent<RuleCardsStoneMover>() == null) p.door.gameObject.AddComponent<RuleCardsStoneMover>();
    }

    // ------------------------------------------------------------------ update
    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // Music crossfade + duck under dialogue/cutscenes and while paused.
        musicFade = Mathf.MoveTowards(musicFade, 1f, dt / 1.8f);
        float duckTarget = RuleCardsStory.Blocking ? .45f : RuleCardsPauseMenu.IsPaused ? .6f : 1f;
        duck = Mathf.MoveTowards(duck, duckTarget, dt * 1.5f);
        float mv = RuleCardsSettings.MusicVolume * .55f * duck;
        music[musicIndex].volume = mv * musicFade;
        AudioSource other = music[1 - musicIndex];
        other.volume = mv * (1f - musicFade);
        if (musicFade >= 1f && other.isPlaying) other.Stop();
        ambience.volume = .5f * Sfx;

        // Keep-alive loops fade toward their target; untouched loops fade out.
        foreach (Loop l in loops.Values)
        {
            if (l.source == null) continue;
            float target = Time.unscaledTime - l.lastTouch > .15f ? 0f : l.target;
            l.source.volume = Mathf.MoveTowards(l.source.volume, target * Sfx, dt * 2.5f);
            if (l.source.volume > .001f && !l.source.isPlaying) l.source.Play();
            else if (l.source.volume <= .001f && l.source.isPlaying) l.source.Stop();
        }

        // IMGUI button presses: hotControl turns non-zero when a control is grabbed.
        int hot = GUIUtility.hotControl;
        if (hot != 0 && lastHot == 0 && Input.GetMouseButton(0)) Ui();
        lastHot = hot;
    }

    private void LateUpdate()
    {
        // The reverb zone sits on the listener, so whatever the listener hears gets the room.
        AudioListener listener = FindListener();
        if (listener != null) reverb.transform.position = listener.transform.position;
    }

    private AudioListener cachedListener;
    private AudioListener FindListener()
    {
        if (cachedListener != null && cachedListener.isActiveAndEnabled) return cachedListener;
        cachedListener = FindObjectOfType<AudioListener>();
        return cachedListener;
    }

    // ------------------------------------------------------------------ name mapping for older code
    /// <summary>Maps the Escape stage's synth names onto real clips. False = keep the synth.</summary>
    public static bool PlayFx(string name, float volume, float pitch)
    {
        switch (name)
        {
            case "land": return Has("land"); // footsteps component plays landings
            case "link": return Play("connected", Mathf.Clamp01(volume * 1.6f), pitch);
            case "flip": return Has("whoosh"); // ArchiveGravityPlayer.SetInverted plays it
            case "air": return Play("connected", Mathf.Clamp01(volume * .8f), 1.15f * pitch);
            case "airfade": return Play("whoosh", volume * .5f, 1.7f * pitch);
            case "crumble": return PlayRandom("crumble", 5, Mathf.Clamp01(volume * 1.3f), null, pitch);
            case "respawn": return Play("respawn", Mathf.Clamp01(volume * 1.5f), pitch);
            case "checkpoint": return Play("solved", Mathf.Clamp01(volume * 1.2f), pitch);
            case "deny": return Play("deny", Mathf.Clamp01(volume * 1.6f), pitch);
        }
        return false;
    }
}

/// <summary>Footsteps, jump and landing sounds from the body's actual motion (works with every controller).</summary>
public sealed class RuleCardsFootsteps : MonoBehaviour
{
    private CharacterController cc;
    private Vector3 lastPos;
    private bool wasGrounded = true, primed;
    private float stepDist, airTime, lastVertical;

    private void Awake() { cc = GetComponent<CharacterController>(); }

    private bool Grounded(Vector3 up)
    {
        Vector3 c = cc.bounds.center;
        float half = cc.height * .5f * Mathf.Abs(transform.lossyScale.y);
        return Physics.SphereCast(c, cc.radius * .8f, -up, out _, half - cc.radius * .8f + .22f, ~0, QueryTriggerInteraction.Ignore);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || cc == null || !cc.enabled) return;
        Vector3 up = transform.up;
        Vector3 pos = transform.position;
        if (!primed) { lastPos = pos; primed = true; return; }
        Vector3 v = (pos - lastPos) / dt;
        lastPos = pos;
        if (v.sqrMagnitude > 40f * 40f) { stepDist = 0f; return; } // teleport / respawn

        float vertical = Vector3.Dot(v, up);
        float planar = Vector3.ProjectOnPlane(v, up).magnitude;
        bool grounded = Grounded(up);
        bool locked = MouseLook.isPaused;

        if (grounded)
        {
            if (!wasGrounded && airTime > .28f && !locked)
            {
                float k = Mathf.Clamp01(-lastVertical / 14f);
                RuleCardsAudio.PlayAt("land", pos, .35f + .55f * k, 1f - .1f * k);
                RuleCardsAudio.PlayRandom("step", 5, .45f, pos);
                stepDist = 0f;
            }
            airTime = 0f;
            if (planar > .8f && !locked)
            {
                stepDist += planar * dt;
                bool run = planar > 5.2f;
                float stride = run ? 2.15f : 1.65f;
                if (stepDist >= stride)
                {
                    stepDist -= stride;
                    RuleCardsAudio.PlayRandom("step", 5, run ? .55f : .4f, pos - up * .8f, run ? 1.05f : .97f);
                }
            }
            else stepDist = Mathf.Min(stepDist, 1.2f); // first step comes quickly after you start moving
        }
        else
        {
            if (wasGrounded && vertical > 2.5f && !locked) RuleCardsAudio.PlayAt("jump", pos, .35f);
            airTime += dt;
            lastVertical = vertical;
        }
        wasGrounded = grounded;
    }
}

/// <summary>Grinding loop while a stone door/leaf moves, and a heavy settle when it stops.</summary>
public sealed class RuleCardsStoneMover : MonoBehaviour
{
    private Vector3 last;
    private bool moving, primed;
    private float still;
    private string key;

    private void Awake() { key = "stone_" + GetInstanceID(); }

    private void Update()
    {
        Vector3 p = transform.position;
        if (!primed || Time.timeSinceLevelLoad < 1f) { last = p; primed = true; return; } // ignore setup snaps
        float speed = Time.deltaTime > 0f ? (p - last).magnitude / Time.deltaTime : 0f;
        last = p;
        if (speed > .05f)
        {
            still = 0f;
            moving = true;
            RuleCardsAudio.LoopAt(key, "door_grind_loop", Mathf.Clamp01(.35f + speed * .12f), p, .9f);
        }
        else if (moving)
        {
            still += Time.deltaTime;
            if (still > .08f)
            {
                moving = false;
                RuleCardsAudio.PlayAt("door_stop", p, .8f, 1f, .05f, 4f);
            }
        }
    }
}
