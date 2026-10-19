using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Story runner for The Archive: dialogue box with portrait + typewriter chirps, cutscene camera with
/// letterbox, Glyph's barks, tiered hints (H) and the lore reader. Lines live in
/// Resources/Story/story.txt. Cutscenes, dialogue and reading lore lock the player and pause the run
/// timer; barks and hints don't. Installs itself; no scene edits.
/// </summary>
public sealed class RuleCardsStory : MonoBehaviour
{
    // ------------------------------------------------------------------ data
    private sealed class Line { public string speaker, text, cmd; public string[] args = new string[0]; }

    private sealed class Sequence
    {
        public string id, kind = "dialogue";
        public readonly List<Line> lines = new List<Line>();
        public bool Blocking { get { return kind == "cutscene" || kind == "dialogue"; } }
        public bool HasCommand(string cmd, string arg)
        {
            foreach (Line l in lines) if (l.cmd == cmd && l.args.Length > 0 && l.args[0] == arg) return true;
            return false;
        }
    }

    private sealed class Pending { public Sequence seq; public Action done; public float at; public bool force; }
    private sealed class BarkLine { public string title, text, speaker; public float duration, t; public bool hint; }

    private const string SeenPrefix = "RuleCards.Story.Seen.";
    private static Dictionary<string, Sequence> sequences;
    private static readonly Dictionary<string, Vector3> anchors = new Dictionary<string, Vector3>();
    private static RuleCardsStory instance;

    /// <summary>Hint context override (e.g. "node_II" while a Repository console is open). Null = scene.</summary>
    public static string HintContext;

    // ------------------------------------------------------------------ runtime state
    private readonly List<Pending> queue = new List<Pending>();
    private Sequence active;
    private Action activeDone;
    private bool lockedByStory, pendingEnd;
    private int lineIndex;
    private Line current;
    private float typed, lineShownAt, waitUntil = -1f, skipHold;
    private int chirpCount;

    private readonly Queue<BarkLine> barkQueue = new Queue<BarkLine>();
    private BarkLine bark;
    private readonly HashSet<string> barkedThisScene = new HashSet<string>();
    private readonly Dictionary<string, int> hintTiers = new Dictionary<string, int>();

    private RuleCardsLore.Entry loreOpen;
    private bool loreWasNew;
    private float loreOpenedAt;

    // camera
    private enum ShotKind { Player, Look, Orbit, Glyph }
    private Camera cutCam, playerCam;
    private ShotKind shot = ShotKind.Player;
    private Vector3 shotTarget, camPos, fromPos;
    private Quaternion camRot, fromRot;
    private float shotTime, orbitRadius = 7f, orbitHeight = 2.5f, orbitStartAngle;
    private float letterbox, fade;

    // audio
    private AudioSource voice;
    private AudioClip[] glyphChirps, archiveChirps;

    // ------------------------------------------------------------------ install
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; sequences = null; anchors.Clear(); HintContext = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;
        var go = new GameObject("RuleCards_Story");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<RuleCardsStory>();
    }

    private void Awake()
    {
        voice = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 0f;
        voice.volume = .5f;
        glyphChirps = new AudioClip[8];
        for (int i = 0; i < glyphChirps.Length; i++) glyphChirps[i] = MakeChirp("glyph" + i, 820f + i * 95f, .05f, false);
        archiveChirps = new AudioClip[4];
        for (int i = 0; i < archiveChirps.Length; i++) archiveChirps[i] = MakeChirp("archive" + i, 92f + i * 14f, .16f, true);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    // ------------------------------------------------------------------ public API
    /// <summary>True while a cutscene, dialogue or lore page holds the player (run timer paused).</summary>
    public static bool Blocking
    {
        get { return instance != null && ((instance.active != null && instance.active.Blocking) || instance.loreOpen != null); }
    }

    /// <summary>True while a cutscene owns the camera (letterboxed).</summary>
    public static bool InCutscene { get { return instance != null && instance.active != null && instance.active.kind == "cutscene"; } }

    public static bool Has(string id) { LoadScript(); return sequences.ContainsKey(id); }

    public static bool Seen(string id) { return PlayerPrefs.GetInt(SeenPrefix + id, 0) == 1; }

    /// <summary>Queue a sequence. With <paramref name="once"/>, a sequence already seen on this save is skipped
    /// (and <paramref name="onDone"/> runs at once). <paramref name="force"/> starts even if something else holds the player.</summary>
    public static void Play(string id, Action onDone = null, bool once = true, float delay = 0f, bool force = false)
    {
        LoadScript();
        Sequence seq;
        if (instance == null || !sequences.TryGetValue(id, out seq) || (once && Seen(id)))
        {
            if (onDone != null) onDone();
            return;
        }
        if (!seq.Blocking) { instance.EnqueueBark(seq, false); return; }
        instance.queue.Add(new Pending { seq = seq, done = onDone, at = Time.unscaledTime + delay, force = force });
    }

    /// <summary>Glyph says a bark sequence (once per scene visit).</summary>
    public static void Bark(string id)
    {
        LoadScript();
        Sequence seq;
        if (instance == null || !sequences.TryGetValue(id, out seq)) return;
        if (!instance.barkedThisScene.Add(id)) return;
        instance.EnqueueBark(seq, false);
    }

    /// <summary>Named world position a script can expose for camera shots (e.g. "finale", "exit").</summary>
    public static void SetAnchor(string name, Vector3 position) { anchors[name] = position; }

    /// <summary>Open a lore page (locks the player, pauses the timer).</summary>
    public static void OpenLore(RuleCardsLore.Entry entry)
    {
        if (instance == null || entry == null || Blocking || instance.pendingRelease) return;
        if (Time.unscaledTime - instance.loreClosedAt < .4f) return; // the closing E must not reopen it
        instance.loreWasNew = !RuleCardsLore.Found(entry.id);
        RuleCardsLore.MarkFound(entry.id);
        instance.loreOpen = entry;
        instance.loreOpenedAt = Time.unscaledTime;
        instance.Lock();
        RuleCardsLore.PlayPage();
    }

    /// <summary>New Game: every sequence and lore page becomes unseen.</summary>
    public static void ResetAll()
    {
        LoadScript();
        foreach (string id in sequences.Keys) PlayerPrefs.DeleteKey(SeenPrefix + id);
        RuleCardsLore.ResetAll();
        PlayerPrefs.Save();
    }

    // ------------------------------------------------------------------ script parsing
    private static void LoadScript()
    {
        if (sequences != null) return;
        sequences = new Dictionary<string, Sequence>();
        TextAsset asset = Resources.Load<TextAsset>("Story/story");
        if (asset == null) { Debug.LogWarning("[RuleCards Story] Resources/Story/story.txt not found."); return; }
        Sequence cur = null;
        foreach (string raw in asset.text.Split('\n'))
        {
            string l = raw.Trim();
            if (l.Length == 0 || l[0] == '#') continue;
            if (l.StartsWith("==="))
            {
                cur = new Sequence { id = l.Substring(3).Trim() };
                sequences[cur.id] = cur;
                continue;
            }
            if (cur == null) continue;
            if (l[0] == '[')
            {
                string[] parts = l.Trim('[', ']', ' ').Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;
                string c = parts[0].ToLowerInvariant();
                if (c == "cutscene" || c == "dialogue" || c == "bark" || c == "hint") { cur.kind = c; continue; }
                var args = new string[parts.Length - 1];
                Array.Copy(parts, 1, args, 0, args.Length);
                cur.lines.Add(new Line { cmd = c, args = args });
                continue;
            }
            int colon = l.IndexOf(':');
            if (colon > 0 && colon <= 12)
            {
                string who = l.Substring(0, colon).Trim();
                if (who.Length > 0 && who == who.ToUpperInvariant())
                {
                    cur.lines.Add(new Line { speaker = who, text = l.Substring(colon + 1).Trim() });
                    continue;
                }
            }
            // Continuation of the previous line.
            if (cur.lines.Count > 0 && cur.lines[cur.lines.Count - 1].text != null) cur.lines[cur.lines.Count - 1].text += " " + l;
            else cur.lines.Add(new Line { speaker = "NARRATOR", text = l });
        }
    }

    // ------------------------------------------------------------------ scene flow
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive) return;
        LoadScript();
        // A scene change ends whatever was running.
        if (active != null) { active = null; current = null; activeDone = null; pendingEnd = false; }
        if (loreOpen != null) loreOpen = null;
        if (lockedByStory) { lockedByStory = false; MouseLook.isPaused = false; }
        DestroyCamera();
        queue.Clear();
        barkQueue.Clear();
        bark = null;
        barkedThisScene.Clear();
        hintTiers.Clear();
        anchors.Clear();
        HintContext = null;
        letterbox = 0f;

        if (!RuleCardsScenes.IsGameplay(scene.name)) return;

        string id;
        if (scene.name == RuleCardsScenes.Hub)
        {
            if (RuleCardsProgress.IsGameComplete) id = "hub_after_escape";
            else if (RuleCardsProgress.IsRepositoryComplete) id = null;
            else
            {
                int c = RuleCardsProgress.CardsCollected;
                id = c == 0 ? "intro" : c == 1 ? "hub_one_card" : c == 2 ? "hub_two_cards" : "hub_three_cards";
            }
        }
        else id = "enter_" + scene.name;

        Sequence seq = null;
        bool fresh = id != null && sequences.TryGetValue(id, out seq) && !Seen(id);
        // Glyph waits out of sight when its first line is an entrance.
        GlyphGuide.Spawn(fresh && seq.HasCommand("glyph", "appear"));
        RuleCardsLore.SpawnFor(scene.name);
        if (fresh) Play(id, null, true, scene.name == RuleCardsScenes.Escape ? .2f : 1.1f);
    }

    // ------------------------------------------------------------------ update
    private void Update()
    {
        if (RuleCardsPauseMenu.IsPaused) return;
        voice.volume = .5f * RuleCardsSettings.SfxVolume;
        float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);

        if (pendingEnd) FinishActive();
        if (loreOpen != null) { ArchiveModal.MarkOpen(); UpdateLore(); }

        if (active == null && loreOpen == null) TryStartQueued();
        if (active != null)
        {
            if (active.Blocking) ArchiveModal.MarkOpen();
            RunActive(dt);
        }

        UpdateBark(dt);
        HintKey();
        letterbox = Mathf.MoveTowards(letterbox, InCutscene ? 1f : 0f, dt * 2.5f);
        fade = Mathf.MoveTowards(fade, 0f, dt * 2.8f);
        GlyphGuide.Speaking = (current != null && current.speaker == "GLYPH" && active != null) || (bark != null && !Blocking);
        GlyphGuide.Spotlight = InCutscene && shot == ShotKind.Glyph;
    }

    private void LateUpdate()
    {
        if (Blocking)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        UpdateCamera(Mathf.Min(Time.unscaledDeltaTime, .1f));
    }

    private void TryStartQueued()
    {
        for (int i = 0; i < queue.Count; i++)
        {
            Pending p = queue[i];
            if (Time.unscaledTime < p.at) continue;
            if (!p.force && (ArchiveModal.IsOpen || MouseLook.isPaused)) return;
            queue.RemoveAt(i);
            Begin(p.seq, p.done);
            return;
        }
    }

    private void Lock()
    {
        if (!MouseLook.isPaused) { lockedByStory = true; MouseLook.isPaused = true; }
    }

    private void Unlock()
    {
        if (lockedByStory && !RuleCardsPauseMenu.IsPaused) MouseLook.isPaused = false;
        lockedByStory = false;
    }

    private void Begin(Sequence seq, Action done)
    {
        active = seq;
        activeDone = done;
        lineIndex = -1;
        current = null;
        waitUntil = -1f;
        skipHold = 0f;
        PlayerPrefs.SetInt(SeenPrefix + seq.id, 1);
        PlayerPrefs.Save();
        Lock();
        bark = null; // dialogue takes over from any bark
        if (seq.kind == "cutscene") BeginCamera();
        Next();
    }

    private void Next()
    {
        lineIndex++;
        while (lineIndex < active.lines.Count && active.lines[lineIndex].cmd != null)
        {
            if (!Exec(active.lines[lineIndex], false)) return; // a wait: resume later
            lineIndex++;
        }
        if (lineIndex >= active.lines.Count) { current = null; pendingEnd = true; return; }
        current = active.lines[lineIndex];
        typed = 0f;
        chirpCount = 0;
        lineShownAt = Time.unscaledTime;
        if (current.speaker == "ARCHIVE") RuleCardsAudio.Play("archive_voice", .9f, 1f, .02f);
    }

    /// <summary>Runs a command line; returns false when the sequence should pause (wait).</summary>
    private bool Exec(Line l, bool instant)
    {
        switch (l.cmd)
        {
            case "wait":
                if (instant) return true;
                float s;
                waitUntil = Time.unscaledTime + (l.args.Length > 0 && float.TryParse(l.args[0], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out s) ? s : 1f);
                current = null;
                return false;
            case "glyph":
                if (l.args.Length == 0 || GlyphGuide.Instance == null) return true;
                if (l.args[0] == "hide") GlyphGuide.Instance.Hide();
                else if (l.args[0] == "appear") GlyphGuide.Instance.Appear();
                else if (l.args[0] == "leave") GlyphGuide.Instance.Leave(instant);
                return true;
            case "shot":
                if (instant || l.args.Length == 0) return true;
                SetShot(l.args);
                return true;
        }
        return true;
    }

    private void RunActive(float dt)
    {
        if (pendingEnd) return;
        if (active.Blocking)
        {
            if (Input.GetKey(KeyCode.Escape)) { skipHold += dt; if (skipHold >= .7f) { Skip(); return; } }
            else skipHold = 0f;
        }
        if (waitUntil > 0f)
        {
            if (Time.unscaledTime >= waitUntil) { waitUntil = -1f; Next(); }
            return;
        }
        if (current == null) return;

        int len = current.text.Length;
        if (typed < len)
        {
            int before = Mathf.FloorToInt(typed);
            typed = Mathf.Min(len, typed + dt * CharsPerSecond(current.speaker));
            int after = Mathf.FloorToInt(typed);
            for (int i = before; i < after; i++) Chirp(current.speaker, current.text[i]);
        }
        if (AdvancePressed())
        {
            if (typed < len) typed = len;
            else Next();
        }
    }

    private bool AdvancePressed()
    {
        if (Time.unscaledTime - lineShownAt < .15f) return false;
        return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return)
            || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetMouseButtonDown(0);
    }

    private static float CharsPerSecond(string speaker)
    {
        return speaker == "ARCHIVE" ? 22f : speaker == "NARRATOR" ? 38f : 46f;
    }

    private void Skip()
    {
        // Keep the world consistent: apply Glyph's remaining state changes.
        for (int i = Mathf.Max(0, lineIndex); i < active.lines.Count; i++)
            if (active.lines[i].cmd == "glyph") Exec(active.lines[i], true);
        current = null;
        waitUntil = -1f;
        pendingEnd = true;
    }

    /// <summary>Ends the sequence one frame after the last key press, so that press can't also trigger gameplay.</summary>
    private void FinishActive()
    {
        pendingEnd = false;
        bool wasCutscene = active != null && active.kind == "cutscene";
        active = null;
        current = null;
        skipHold = 0f;
        if (wasCutscene) { DestroyCamera(); fade = 1f; }
        Unlock();
        Action done = activeDone;
        activeDone = null;
        if (done != null) done();
    }

    // ------------------------------------------------------------------ lore reader
    private void UpdateLore()
    {
        if (Time.unscaledTime - loreOpenedAt < .25f) return;
        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space)
            || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
        {
            loreOpen = null;
            // Release next frame so this key press doesn't also act in the world.
            pendingRelease = true;
            loreClosedFrame = Time.frameCount;
            loreClosedAt = Time.unscaledTime;
        }
    }

    private bool pendingRelease;
    private int loreClosedFrame = -10;
    private float loreClosedAt = -10f;

    // ------------------------------------------------------------------ barks & hints
    private void EnqueueBark(Sequence seq, bool hint)
    {
        foreach (Line l in seq.lines)
        {
            if (l.text == null) continue;
            barkQueue.Enqueue(new BarkLine { speaker = l.speaker, text = l.text, title = "Glyph", duration = 2.6f + l.text.Length * .045f });
        }
    }

    private void UpdateBark(float dt)
    {
        // Unlock on a later frame than the closing key press, so the press can't act in the world too.
        if (pendingRelease && Time.frameCount > loreClosedFrame) { pendingRelease = false; Unlock(); }
        if (Blocking) return; // barks wait while dialogue holds the screen
        if (bark == null && barkQueue.Count > 0) { bark = barkQueue.Dequeue(); bark.t = 0f; chirpCount = 0; }
        if (bark == null) return;
        int before = Mathf.FloorToInt(bark.t * 52f);
        bark.t += dt;
        int after = Mathf.Min(bark.text.Length, Mathf.FloorToInt(bark.t * 52f));
        for (int i = Mathf.Max(0, before); i < after; i++) Chirp("GLYPH", bark.text[i], .55f);
        if (bark.t > bark.duration + .4f) bark = null;
    }

    private void HintKey()
    {
        if (!Input.GetKeyDown(KeyCode.H) || Blocking) return;
        string scene = SceneManager.GetActiveScene().name;
        if (!RuleCardsScenes.IsGameplay(scene)) return;
        Sequence seq;
        string key = string.IsNullOrEmpty(HintContext) ? scene : HintContext;
        if (!sequences.TryGetValue("hint_" + key, out seq) && !sequences.TryGetValue("hint_" + (key = scene), out seq)) return;
        var lines = new List<string>();
        foreach (Line l in seq.lines) if (l.text != null) lines.Add(l.text);
        if (lines.Count == 0) return;
        int tier;
        hintTiers.TryGetValue(key, out tier);
        int index = Mathf.Min(tier, lines.Count - 1);
        hintTiers[key] = tier + 1;
        barkQueue.Clear();
        bark = new BarkLine
        {
            speaker = "GLYPH", text = lines[index], hint = true,
            title = "Hint  " + (index + 1) + " / " + lines.Count, duration = 5f + lines[index].Length * .05f
        };
        chirpCount = 0;
        if (GlyphGuide.Instance != null) GlyphGuide.Instance.Pulse(1.5f);
        RuleCardsAudio.Ui(.45f, 1.35f);
    }

    // ------------------------------------------------------------------ voice
    private void Chirp(string speaker, char c, float volume = 1f)
    {
        if (speaker == "NARRATOR" || char.IsWhiteSpace(c) || char.IsPunctuation(c)) return;
        chirpCount++;
        if (speaker == "ARCHIVE")
        {
            if (chirpCount % 3 != 1) return;
            voice.pitch = UnityEngine.Random.Range(.92f, 1.04f);
            voice.PlayOneShot(archiveChirps[(c * 7) % archiveChirps.Length], .55f * volume);
            return;
        }
        if (chirpCount % 2 != 1) return;
        voice.pitch = UnityEngine.Random.Range(.96f, 1.06f);
        voice.PlayOneShot(glyphChirps[(c * 13) % glyphChirps.Length], .16f * volume);
        if (GlyphGuide.Instance != null) GlyphGuide.Instance.Pulse(.6f);
    }

    /// <summary>Little runic blip: square-ish tone with a fast decay (Glyph) or a low hum (the Archive).</summary>
    private static AudioClip MakeChirp(string name, float freq, float length, bool low)
    {
        const int rate = 44100;
        int n = Mathf.CeilToInt(rate * length);
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float env = low ? Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / length)) * .9f : Mathf.Exp(-t * 55f) * Mathf.Clamp01(t * 900f);
            float f = freq * (low ? 1f : 1f + .18f * Mathf.Exp(-t * 80f)); // tiny downward chirp
            float ph = 2f * Mathf.PI * f * t;
            float s = low ? Mathf.Sin(ph) * .6f + Mathf.Sin(ph * .5f) * .4f + Mathf.Sin(ph * 3.01f) * .08f
                          : Mathf.Clamp(Mathf.Sin(ph) * 2.2f, -1f, 1f) * .5f + Mathf.Sin(ph * 2f) * .15f;
            data[i] = s * env * .6f;
        }
        AudioClip clip = AudioClip.Create("RuleCards_Chirp_" + name, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // ------------------------------------------------------------------ camera
    private Camera FindPlayerCamera()
    {
        Camera main = Camera.main;
        if (main != null) return main;
        foreach (Camera c in Camera.allCameras) if (c != cutCam && c.targetTexture == null) return c;
        return null;
    }

    private Vector3 UpVector()
    {
        if (playerCam != null && playerCam.transform.parent != null) return playerCam.transform.parent.up;
        return Vector3.up;
    }

    private void BeginCamera()
    {
        playerCam = FindPlayerCamera();
        if (playerCam == null) return;
        if (cutCam == null)
        {
            var go = new GameObject("RuleCards_CutsceneCam");
            SceneManager.MoveGameObjectToScene(go, SceneManager.GetActiveScene());
            cutCam = go.AddComponent<Camera>();
        }
        cutCam.CopyFrom(playerCam);
        cutCam.targetTexture = null;
        cutCam.depth = playerCam.depth + 10f;
        var data = cutCam.GetUniversalAdditionalCameraData();
        var source = playerCam.GetUniversalAdditionalCameraData();
        if (data != null)
        {
            data.renderPostProcessing = true;
            if (source != null) { data.antialiasing = source.antialiasing; data.renderShadows = source.renderShadows; }
        }
        camPos = fromPos = playerCam.transform.position;
        camRot = fromRot = playerCam.transform.rotation;
        shot = ShotKind.Player;
        shotTime = 0f;
        fade = 1f; // open from black so the cut is soft
        // The swell only opens the story (New Game intro); later cutscenes start quietly.
        if (active != null && active.id == "intro") RuleCardsAudio.Play("stinger", .5f, 1f, 0f);
    }

    private void DestroyCamera()
    {
        if (cutCam != null) Destroy(cutCam.gameObject);
        cutCam = null;
        shot = ShotKind.Player;
    }

    private void SetShot(string[] args)
    {
        if (cutCam == null) return;
        string kind = args[0].ToLowerInvariant();
        fromPos = camPos;
        fromRot = camRot;
        shotTime = 0f;
        if (kind == "player") { shot = ShotKind.Player; return; }
        if (kind == "glyph") { shot = GlyphGuide.Instance != null ? ShotKind.Glyph : ShotKind.Player; return; }
        Vector3 target;
        if (args.Length < 2 || !ResolveTarget(args[1], out target)) { shot = ShotKind.Player; return; }
        shotTarget = target;
        if (kind == "orbit")
        {
            shot = ShotKind.Orbit;
            orbitRadius = args.Length > 2 ? ParseFloat(args[2], 7f) : 7f;
            orbitHeight = args.Length > 3 ? ParseFloat(args[3], 2.5f) : 2.5f;
            Vector3 d = camPos - target;
            orbitStartAngle = Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg;
        }
        else shot = ShotKind.Look;
    }

    private static float ParseFloat(string s, float fallback)
    {
        float v;
        return float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) ? v : fallback;
    }

    private static bool ResolveTarget(string name, out Vector3 position)
    {
        position = Vector3.zero;
        if (name.StartsWith("@portal:"))
        {
            string scene = name.Substring(8);
            foreach (StagePortal p in FindObjectsOfType<StagePortal>())
                if (p.destinationScene == scene) { position = p.transform.position + Vector3.up * 1.6f; return true; }
            return false;
        }
        if (name == "@pickup")
        {
            foreach (RuleCardPickup p in FindObjectsOfType<RuleCardPickup>()) { position = p.transform.position; return true; }
            return false;
        }
        if (name == "@return")
        {
            foreach (StageReturn r in FindObjectsOfType<StageReturn>()) { position = r.transform.position + Vector3.up * 1.4f; return true; }
            return false;
        }
        if (name == "@nodes")
        {
            int n = 0;
            foreach (string node in new[] { "ArchiveNode_A", "ArchiveNode_B", "ArchiveNode_C" })
            {
                GameObject go = GameObject.Find(node);
                if (go != null) { position += go.transform.position; n++; }
            }
            if (n == 0) return false;
            position /= n;
            return true;
        }
        if (name.StartsWith("@")) return anchors.TryGetValue(name.Substring(1), out position);
        GameObject named = GameObject.Find(name);
        if (named == null) return false;
        position = named.transform.position;
        return true;
    }

    private void UpdateCamera(float dt)
    {
        if (cutCam == null) return;
        if (playerCam == null) { DestroyCamera(); return; }
        shotTime += dt;
        Vector3 eye = playerCam.transform.position, up = UpVector();
        Vector3 pos = eye;
        Quaternion rot = playerCam.transform.rotation;
        switch (shot)
        {
            case ShotKind.Look:
            {
                Vector3 to = shotTarget - eye;
                if (to.sqrMagnitude > .01f)
                {
                    rot = Quaternion.LookRotation(to, up);
                    pos = eye + to.normalized * Mathf.Min(.6f, shotTime * .12f); // slow push-in
                }
                break;
            }
            case ShotKind.Glyph:
            {
                GlyphGuide g = GlyphGuide.Instance;
                if (g != null)
                {
                    Vector3 to = g.transform.position - eye;
                    if (to.sqrMagnitude > .01f) rot = Quaternion.LookRotation(to, up);
                }
                break;
            }
            case ShotKind.Orbit:
            {
                float a = (orbitStartAngle + shotTime * 7f) * Mathf.Deg2Rad;
                Vector3 want = shotTarget + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * orbitRadius + Vector3.up * orbitHeight;
                Vector3 dir = want - shotTarget;
                float dist = dir.magnitude;
                RaycastHit hit;
                if (dist > .01f && Physics.SphereCast(shotTarget, .3f, dir / dist, out hit, dist, ~0, QueryTriggerInteraction.Ignore))
                    want = shotTarget + dir / dist * Mathf.Max(1f, hit.distance - .2f);
                pos = want;
                rot = Quaternion.LookRotation(shotTarget - pos, Vector3.up);
                break;
            }
        }
        float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(shotTime / 1.1f));
        camPos = Vector3.Lerp(fromPos, pos, k);
        camRot = Quaternion.Slerp(fromRot, rot, k);
        cutCam.transform.SetPositionAndRotation(camPos, camRot);
        cutCam.fieldOfView = playerCam.fieldOfView;
    }

    // ------------------------------------------------------------------ GUI
    private void OnGUI()
    {
        bool showDialogue = active != null && active.Blocking && current != null;
        bool showBark = bark != null && !Blocking && !RuleCardsPauseMenu.IsPaused;
        if (letterbox <= 0f && fade <= 0f && !showDialogue && !showBark && loreOpen == null && skipHold <= 0f) return;

        int oldDepth = GUI.depth;
        GUI.depth = -150;
        float vw, vh;
        Matrix4x4 previous = ResponsiveIMGUI.Begin(out vw, out vh);

        if (letterbox > 0f)
        {
            float bar = vh * .1f * Mathf.SmoothStep(0f, 1f, letterbox);
            RepositoryGUI.Fill(new Rect(0f, 0f, vw, bar), Color.black);
            RepositoryGUI.Fill(new Rect(0f, vh - bar, vw, bar), Color.black);
        }
        if (showDialogue)
        {
            if (current.speaker == "ARCHIVE") DrawArchiveVoice(vw, vh);
            else DrawDialogue(vw, vh);
        }
        if (showBark) DrawBark(vw, vh);
        if (loreOpen != null) DrawLore(vw, vh);
        if (skipHold > 0f && active != null) DrawSkip(vw, vh);
        if (fade > 0f) RepositoryGUI.Fill(new Rect(0f, 0f, vw, vh), new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 1f, fade)));

        ResponsiveIMGUI.End(previous);
        GUI.depth = oldDepth;
    }

    /// <summary>Typewriter text that doesn't reflow: the hidden remainder is laid out but transparent.</summary>
    private static string Typed(string text, float typed)
    {
        int n = Mathf.Clamp(Mathf.FloorToInt(typed), 0, text.Length);
        if (n >= text.Length) return text;
        return text.Substring(0, n) + "<color=#00000000>" + text.Substring(n) + "</color>";
    }

    private void DrawDialogue(float vw, float vh)
    {
        bool narrator = current.speaker == "NARRATOR";
        bool glyph = current.speaker == "GLYPH";
        float w = Mathf.Min(880f, vw - 40f), h = 150f;
        Rect panel = new Rect((vw - w) * .5f, vh - h - Mathf.Max(28f, vh * .1f * letterbox - 40f), w, h);
        RuleCardsUI.Panel(panel, .97f);

        float textX = panel.x + 30f;
        if (glyph)
        {
            Rect portrait = new Rect(panel.x + 16f, panel.y + 16f, h - 32f, h - 32f);
            DrawPortrait(portrait);
            textX = portrait.xMax + 22f;

            // Name plate on the panel's top edge.
            var style = RuleCardsUI.Title(15, RuleCardsUI.Cyan, TextAnchor.MiddleCenter);
            float nw = style.CalcSize(new GUIContent("Glyph")).x + 40f;
            Rect plate = new Rect(textX - 6f, panel.y - 15f, nw, 28f);
            RepositoryGUI.Fill(plate, new Color(.02f, .035f, .05f, 1f));
            RepositoryGUI.Frame(plate, RepositoryGUI.WithAlpha(RuleCardsUI.Gold, .8f), 1f);
            RuleCardsUI.Diamond(new Vector2(plate.x, plate.center.y), 5f, RuleCardsUI.Gold);
            RuleCardsUI.Diamond(new Vector2(plate.xMax, plate.center.y), 5f, RuleCardsUI.Gold);
            GUI.Label(plate, "Glyph", style);
        }

        Rect body = new Rect(textX, panel.y + (glyph ? 24f : 22f), panel.xMax - textX - 30f, panel.height - 52f);
        GUIStyle textStyle = narrator ? RuleCardsUI.ProseItalic(21, new Color(.88f, .91f, .97f), TextAnchor.MiddleCenter)
                                      : RuleCardsUI.Prose(21, RuleCardsUI.Text, TextAnchor.UpperLeft);
        GUI.Label(body, Typed(current.text, typed), textStyle);

        // Footer: continue gem + controls.
        bool done = typed >= current.text.Length;
        GUI.Label(new Rect(panel.x, panel.yMax - 30f, panel.width - 46f, 20f), "Space  ·  continue        Hold Esc  ·  skip",
            RuleCardsUI.Label(12, RepositoryGUI.WithAlpha(RuleCardsUI.Muted, .8f), TextAnchor.MiddleRight));
        if (done)
        {
            float blink = .55f + .45f * Mathf.Sin(Time.unscaledTime * 6f);
            RuleCardsUI.Diamond(new Vector2(panel.xMax - 28f, panel.yMax - 20f + Mathf.Sin(Time.unscaledTime * 4f) * 2f), 8f, RuleCardsUI.GoldBright, blink);
        }
        if (glyph && GlyphGuide.Instance != null) GlyphGuide.Instance.RequestPortrait();
    }

    private void DrawArchiveVoice(float vw, float vh)
    {
        // The Archive speaks over everything: a dark band, wide-spaced Cinzel.
        float cy = vh * .44f;
        RuleCardsUI.Gradient(new Rect(0f, cy - 90f, vw, 90f), new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, .7f));
        RuleCardsUI.Gradient(new Rect(0f, cy, vw, 90f), new Color(0f, 0f, 0f, .7f), new Color(0f, 0f, 0f, 0f));
        GUI.Label(new Rect(40f, cy - 50f, vw - 80f, 70f), Typed(current.text, typed),
            RuleCardsUI.Title(Mathf.RoundToInt(Mathf.Clamp(vw / 30f, 24f, 38f)), new Color(1f, .78f, .5f), TextAnchor.MiddleCenter));
        GUI.Label(new Rect(0f, cy + 26f, vw, 20f), "T H E   A R C H I V E", RuleCardsUI.Label(12, RepositoryGUI.WithAlpha(RuleCardsUI.Gold, .8f), TextAnchor.MiddleCenter));
        if (typed >= current.text.Length)
            GUI.Label(new Rect(0f, vh - 44f - vh * .1f * letterbox, vw, 20f), "Space  ·  continue        Hold Esc  ·  skip",
                RuleCardsUI.Label(12, RepositoryGUI.WithAlpha(RuleCardsUI.Muted, .8f), TextAnchor.MiddleCenter));
    }

    private void DrawPortrait(Rect r)
    {
        RepositoryGUI.Fill(r, new Color(.015f, .028f, .04f, 1f));
        Texture tex = GlyphGuide.Instance != null ? GlyphGuide.Instance.PortraitTexture : null;
        if (tex != null) GUI.DrawTexture(new Rect(r.x + 2f, r.y + 2f, r.width - 4f, r.height - 4f), tex, ScaleMode.ScaleAndCrop);
        else DrawEmblem(r);
        RepositoryGUI.Frame(r, RepositoryGUI.WithAlpha(RuleCardsUI.Gold, .75f), 1.5f);
        RepositoryGUI.Frame(new Rect(r.x + 4f, r.y + 4f, r.width - 8f, r.height - 8f), RepositoryGUI.WithAlpha(RuleCardsUI.Gold, .2f), 1f);
    }

    /// <summary>Fallback portrait: a drawn rune-ring emblem with Glyph's eye.</summary>
    private static void DrawEmblem(Rect r)
    {
        Vector2 c = r.center;
        float s = r.width, t = Time.unscaledTime;
        for (int i = 0; i < 10; i++)
        {
            float a = (i * 36f + t * 30f) * Mathf.Deg2Rad;
            RuleCardsUI.Diamond(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * .45f) * s * .36f, s * .07f, RuleCardsUI.Cyan, .75f);
        }
        RuleCardsUI.Diamond(c, s * .42f, new Color(.11f, .14f, .18f), 1f);
        RuleCardsUI.Diamond(c, s * .3f, new Color(.07f, .09f, .12f), 1f);
        float pulse = GlyphGuide.Instance != null ? GlyphGuide.Instance.EyePulse : 0f;
        RuleCardsUI.Diamond(c, s * (.11f + .03f * pulse), RuleCardsUI.Cyan, .8f + .2f * pulse);
    }

    private void DrawBark(float vw, float vh)
    {
        float w = Mathf.Min(400f, vw - 32f);
        float x = vw - w - 16f, y = 40f;
        GUIStyle textStyle = RuleCardsUI.Prose(17, RuleCardsUI.Text, TextAnchor.UpperLeft);
        float textW = w - 84f;
        float textH = Mathf.Max(40f, textStyle.CalcHeight(new GUIContent(bark.text), textW));
        Rect card = new Rect(x, y, w, textH + 46f);
        float alpha = Mathf.Clamp01(bark.t / .2f) * Mathf.Clamp01((bark.duration + .4f - bark.t) / .4f);
        Color prevColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        RuleCardsUI.HudCard(card);
        Rect portrait = new Rect(card.x + 12f, card.y + 12f, 52f, 52f);
        DrawPortrait(portrait);
        if (GlyphGuide.Instance != null) GlyphGuide.Instance.RequestPortrait();
        GUI.Label(new Rect(portrait.xMax + 12f, card.y + 8f, textW, 18f), bark.title,
            RuleCardsUI.Label(12, bark.hint ? RuleCardsUI.GoldBright : RuleCardsUI.Cyan));
        GUI.Label(new Rect(portrait.xMax + 12f, card.y + 28f, textW, textH), Typed(bark.text, bark.t * 52f), textStyle);
        if (bark.hint)
            GUI.Label(new Rect(card.x, card.yMax - 20f, card.width - 12f, 16f), "H  ·  next hint",
                RuleCardsUI.Label(11, RepositoryGUI.WithAlpha(RuleCardsUI.Muted, .8f), TextAnchor.MiddleRight));
        GUI.color = prevColor;
    }

    private void DrawSkip(float vw, float vh)
    {
        float k = Mathf.Clamp01(skipHold / .7f);
        Rect r = new Rect(vw - 196f, vh * .1f * letterbox + 16f, 176f, 26f);
        RuleCardsUI.HudCard(r);
        RepositoryGUI.Fill(new Rect(r.x + 1f, r.yMax - 3f, (r.width - 2f) * k, 2f), RuleCardsUI.GoldBright);
        GUI.Label(r, "Skipping…", RuleCardsUI.Label(12, RuleCardsUI.Text, TextAnchor.MiddleCenter));
    }

    private void DrawLore(float vw, float vh)
    {
        RepositoryGUI.Fill(new Rect(0f, 0f, vw, vh), new Color(0f, .01f, .02f, .6f));
        float w = Mathf.Min(660f, vw - 40f);
        GUIStyle bodyStyle = RuleCardsUI.Prose(19, new Color(.9f, .92f, .96f), TextAnchor.UpperLeft);
        float bodyH = bodyStyle.CalcHeight(new GUIContent(loreOpen.body), w - 96f);
        float h = Mathf.Clamp(bodyH + 180f, 280f, vh - 40f);
        Rect panel = new Rect((vw - w) * .5f, (vh - h) * .5f, w, h);
        RuleCardsUI.Panel(panel);
        int index = RuleCardsLore.IndexOf(loreOpen.id);
        GUI.Label(new Rect(panel.x, panel.y + 22f, w, 18f),
            "Fragment " + (index + 1) + " of " + RuleCardsLore.All.Count + "   ·   " + RuleCardsLore.PlaceName(loreOpen.scene),
            RuleCardsUI.Label(13, RuleCardsUI.Gold, TextAnchor.MiddleCenter));
        GUI.Label(new Rect(panel.x + 30f, panel.y + 42f, w - 60f, 44f), loreOpen.title, RuleCardsUI.Title(26, RuleCardsUI.Text));
        RuleCardsUI.Divider(panel.center.x, panel.y + 94f, w - 160f, RuleCardsUI.Gold);
        GUI.Label(new Rect(panel.x + 48f, panel.y + 114f, w - 96f, h - 170f), loreOpen.body, bodyStyle);
        if (loreWasNew)
            GUI.Label(new Rect(panel.x + 30f, panel.yMax - 40f, w * .5f, 20f), "Glyph added this to the Index",
                RuleCardsUI.Label(12, RuleCardsUI.GoldBright));
        GUI.Label(new Rect(panel.x, panel.yMax - 40f, w - 30f, 20f), "E  ·  close",
            RuleCardsUI.Label(12, RuleCardsUI.Muted, TextAnchor.MiddleRight));
    }
}
