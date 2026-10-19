using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[System.Serializable]
public struct EscapeCheckpoint
{
    public string label;
    public Vector3 position;
    public float yaw;
}

[System.Serializable]
public struct EscapeZone
{
    public string label;
    [TextArea(1, 3)] public string hint;
    public Vector3 center;
    public Vector3 size;
    [Tooltip("1 = Kinetic Link, 2 = Invert Gravity, 4 = Solidify Air")]
    public int cardMask;
    public bool Contains(Vector3 p)
    {
        Vector3 d = p - center, h = size * .5f;
        return Mathf.Abs(d.x) <= h.x && Mathf.Abs(d.y) <= h.y && Mathf.Abs(d.z) <= h.z;
    }
}

/// <summary>
/// Stage 04 — The Unwritten Archive. A first-person escape back through the whole Archive:
/// a collapse front follows the route behind the player, floors crumble once it passes,
/// checkpoints respawn you, and each section only accepts the Rule Card it was built for.
/// The editor tool RuleCards > Build Escape Stage fills in all the data below.
/// </summary>
public sealed class EscapeStage : MonoBehaviour
{
    public const string SceneName = "Stage_04_Escape";

    [Header("Route (player body-centre path, start → exit)")]
    public Vector3[] route = new Vector3[0];
    [Tooltip("Collapse speed multiplier per route segment (segment i = route[i] → route[i+1]).")]
    public float[] pace = new float[0];
    public EscapeCheckpoint[] checkpoints = new EscapeCheckpoint[0];
    public EscapeZone[] zones = new EscapeZone[0];

    [Header("Exit")]
    public Vector3 exitCenter;
    public Vector3 exitSize = new Vector3(8f, 6f, 3f);

    [Header("Collapse")]
    public float collapseSpeed = 1.9f;
    public float collapseStartDelay = 6f;
    public float respawnGap = 14f;
    [Tooltip("Floors/ceilings are cut into tiles of about this size; tiles fall as the collapse reaches them.")]
    public float collapseTileSize = 2f;
    [Tooltip("How far ahead of the collapse tiles start glowing red and trembling.")]
    public float collapseWarnDistance = 7f;
    public float fallBelow = 7f, fallAbove = 12f;

    [Header("Materials")]
    public Material cardTemplate;
    public Material airSolidMaterial;
    public Material airGhostMaterial;
    public Material collapseTemplate;
    public Material particleMaterial;

    [Header("Atmosphere")]
    public bool applyAtmosphere = true;
    public Color fogColor = new Color(.035f, .03f, .07f);
    public float fogDensity = .0075f;
    public Color skyAmbient = new Color(.22f, .25f, .42f), equatorAmbient = new Color(.15f, .12f, .26f), groundAmbient = new Color(.05f, .045f, .08f);

    public bool InputBlocked { get { return finished || respawnFlash > .55f; } }

    // route data
    private float[] cumulative = new float[0];
    private float routeLength;
    private float[] checkpointS = new float[0];

    // player
    private CharacterController player;
    private ArchiveGravityPlayer motor;
    private CardInventory inventory;
    private float playerS;
    private int checkpoint;
    private int zoneIndex = -2;
    private readonly HashSet<int> zoneIntroduced = new HashSet<int>();

    // collapse
    private float wallS, wallHoldUntil;
    private GameObject wall;
    private Material wallMaterial;
    private Light wallLight;
    private AudioSource rumble;
    private EscapeCrumble[] crumbles = new EscapeCrumble[0];
    private ParticleSystem wallSparks, motes;
    private CollapseFloor floor;

    // flow
    private float runTime;
    private bool newBestRun;
    private int falls;
    private bool finished;
    private bool awaitingEpilogue; // finished, but Glyph's farewell is still playing
    private float finishedAt;
    private bool newBest;
    private float respawnFlash;
    private string banner = "", bannerSub = "";
    private float bannerUntil;
    private string notice = "";
    private bool noticeError;
    private float noticeUntil;

    // ------------------------------------------------------------------ setup
    private void Start()
    {
        RuleCardsRunHud.StageTime = () => runTime;
        RuleCardsStory.SetAnchor("exit", exitCenter);
        BuildRoute();
        player = FindObjectOfType<CharacterController>();
        if (player == null) { Debug.LogError("[RuleCards] Escape stage has no player."); enabled = false; return; }

        foreach (MonoBehaviour b in player.GetComponents<MonoBehaviour>())
            if (b != null && b.GetType().Name == "PlayerFallRespawn") b.enabled = false;
        motor = player.GetComponent<ArchiveGravityPlayer>();
        if (motor == null) motor = player.gameObject.AddComponent<ArchiveGravityPlayer>();
        inventory = player.GetComponent<CardInventory>();
        if (inventory == null) inventory = player.gameObject.AddComponent<CardInventory>();
        inventory.cardTemplate = cardTemplate;
        inventory.airSolidMaterial = airSolidMaterial;
        inventory.airGhostMaterial = airGhostMaterial;
        inventory.Init(this, motor);
        EscapeFX.Init(particleMaterial, transform);
        if (applyAtmosphere) ApplyAtmosphere();
        motor.Landed += OnLanded;
        motes = EscapeFX.Emitter("FX_AmbientMotes", player.transform, new Color(.2f, .9f, 1f, .5f), new Color(1f, .3f, .7f, .4f),
            45f, 6f, .35f, .09f, ParticleSystemShapeType.Box, new Vector3(30f, 14f, 30f));

        // The collapse is the floor itself: box floors/ceilings become tiles that fall as the front passes.
        floor = new CollapseFloor { warnDistance = collapseWarnDistance };
        var tileRoot = new GameObject("Collapse_Tiles").transform;
        crumbles = floor.Build(FindObjectsOfType<EscapeCrumble>(), collapseTileSize, p => Project(p, 0f, routeLength), tileRoot).ToArray();
        foreach (EscapeCrumble c in crumbles) c.SMax = CrumbleExtent(c);

        BuildWall();
        Respawn(0, false);
        wallHoldUntil = Time.time + collapseStartDelay;
        Banner("THE ARCHIVE IS UNWRITING ITSELF", "Run. Your Rule Cards are the only way out.", 5f);
    }

    private void BuildRoute()
    {
        int n = route.Length;
        cumulative = new float[n];
        for (int i = 1; i < n; i++) cumulative[i] = cumulative[i - 1] + Vector3.Distance(route[i - 1], route[i]);
        routeLength = n > 0 ? cumulative[n - 1] : 0f;
        checkpointS = new float[checkpoints.Length];
        for (int i = 0; i < checkpoints.Length; i++) checkpointS[i] = Project(checkpoints[i].position, 0f, routeLength);
    }

    private float CrumbleExtent(EscapeCrumble c)
    {
        Collider col = c.GetComponent<Collider>();
        Renderer ren = c.GetComponent<Renderer>();
        Bounds b = col != null ? col.bounds : ren != null ? ren.bounds : new Bounds(c.transform.position, Vector3.one);
        float max = 0f;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3(i % 2 == 0 ? b.min.x : b.max.x, (i / 2) % 2 == 0 ? b.min.y : b.max.y, i / 4 == 0 ? b.min.z : b.max.z);
            max = Mathf.Max(max, Project(corner, 0f, routeLength));
        }
        return max;
    }

    private void BuildWall()
    {
        wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Collapse_Front";
        Collider c = wall.GetComponent<Collider>();
        if (c != null) { c.enabled = false; Destroy(c); }
        wall.transform.localScale = new Vector3(70f, 70f, .6f);
        var r = wall.GetComponent<Renderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.enabled = false; // no wall: the collapse is shown by the floor breaking apart
        if (collapseTemplate != null)
        {
            wallMaterial = new Material(collapseTemplate) { name = "CollapseFront_Runtime" };
            if (wallMaterial.HasProperty("_BaseMap")) wallMaterial.SetTexture("_BaseMap", Texture2D.whiteTexture);
            if (wallMaterial.HasProperty("_BaseColor")) wallMaterial.SetColor("_BaseColor", new Color(.02f, 0f, .03f, 1f));
            if (wallMaterial.HasProperty("_EmissionColor"))
            {
                wallMaterial.EnableKeyword("_EMISSION");
                wallMaterial.SetColor("_EmissionColor", new Color(.9f, .05f, .4f) * 1.4f);
            }
            r.sharedMaterial = wallMaterial;
        }
        wallLight = new GameObject("Collapse_Light").AddComponent<Light>();
        wallLight.transform.SetParent(wall.transform, false);
        wallLight.transform.localPosition = new Vector3(0f, -.6f, 2.5f);
        wallLight.type = LightType.Point;
        wallLight.color = new Color(1f, .1f, .45f);
        wallLight.range = 12f;
        wallLight.intensity = 3f;
        wallLight.shadows = LightShadows.None;

        // Sparks stream off the front toward the player; the front "unwrites" the world as it moves.
        wallSparks = EscapeFX.Emitter("FX_CollapseSparks", wall.transform, new Color(1f, .15f, .5f, .9f), new Color(.6f, .2f, 1f, .8f),
            220f, 1.8f, 5f, .35f, ParticleSystemShapeType.Box, new Vector3(1f, 1f, .02f));
        if (wallSparks != null)
        {
            var shape = wallSparks.shape;
            shape.scale = new Vector3(16f, .4f, 2f);
            var velocity = wallSparks.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.z = new ParticleSystem.MinMaxCurve(-.5f, 1f);
            velocity.x = new ParticleSystem.MinMaxCurve(-.8f, .8f);
            velocity.y = new ParticleSystem.MinMaxCurve(-3f, 2f);
            var main = wallSparks.main;
            main.startSpeed = 0f;
        }

        rumble = gameObject.AddComponent<AudioSource>();
        rumble.clip = RuleCardsAudio.Has("rumble_loop") ? RuleCardsAudio.Clip("rumble_loop") : RumbleLoop();
        rumble.loop = true;
        rumble.spatialBlend = 0f;
        rumble.volume = 0f;
        rumble.Play();
    }

    // ------------------------------------------------------------------ route maths
    private Vector3 PointAt(float s)
    {
        int n = route.Length;
        if (n == 0) return Vector3.zero;
        if (s <= 0f) return route[0] + (n > 1 ? (route[0] - route[1]).normalized * -s : Vector3.zero);
        for (int i = 1; i < n; i++)
            if (s <= cumulative[i])
            {
                float len = cumulative[i] - cumulative[i - 1];
                return Vector3.Lerp(route[i - 1], route[i], len > 0f ? (s - cumulative[i - 1]) / len : 0f);
            }
        return route[n - 1];
    }

    private Vector3 DirectionAt(float s)
    {
        int n = route.Length;
        if (n < 2) return Vector3.forward;
        int seg = SegmentAt(s);
        Vector3 d = route[seg + 1] - route[seg];
        Vector3 flat = new Vector3(d.x, 0f, d.z);
        return flat.sqrMagnitude > .01f ? flat.normalized : (d.sqrMagnitude > 0f ? d.normalized : Vector3.forward);
    }

    private int SegmentAt(float s)
    {
        for (int i = 1; i < route.Length; i++) if (s <= cumulative[i]) return i - 1;
        return Mathf.Max(0, route.Length - 2);
    }

    /// <summary>Closest route distance to p, searching only segments overlapping [from, to].</summary>
    private float Project(Vector3 p, float from, float to)
    {
        float best = 0f, bestDistance = float.MaxValue;
        for (int i = 1; i < route.Length; i++)
        {
            if (cumulative[i] < from || cumulative[i - 1] > to) continue;
            Vector3 a = route[i - 1], b = route[i], ab = b - a;
            float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            float d = (a + ab * t - p).sqrMagnitude;
            if (d < bestDistance) { bestDistance = d; best = cumulative[i - 1] + t * Mathf.Sqrt(ab.sqrMagnitude); }
        }
        return best;
    }

    public int AllowedMaskAt(Vector3 p)
    {
        foreach (EscapeZone z in zones) if (z.Contains(p)) return z.cardMask;
        return 0;
    }

    private int ZoneAt(Vector3 p)
    {
        for (int i = 0; i < zones.Length; i++) if (zones[i].Contains(p)) return i;
        return -1;
    }

    // ------------------------------------------------------------------ update
    private void Update()
    {
        if (player == null) return;
        if (finished) { ArchiveModal.MarkOpen(); if (!awaitingEpilogue) UpdateEnding(); return; }
        if (RuleCardsStory.Blocking) return; // cutscenes freeze the collapse and the escape clock
        float dt = Time.deltaTime;
        respawnFlash = Mathf.MoveTowards(respawnFlash, 0f, dt * 1.6f);
        runTime += dt;

        Vector3 p = player.transform.position;
        playerS = Project(p, playerS - 20f, playerS + 30f);

        int zone = ZoneAt(p);
        if (zone != zoneIndex)
        {
            zoneIndex = zone;
            // Each section introduces itself once per run (no repeats when hovering on a border).
            if (zone >= 0 && !zoneIntroduced.Contains(zone) && !string.IsNullOrEmpty(zones[zone].label))
            {
                zoneIntroduced.Add(zone);
                Banner(zones[zone].label, zones[zone].hint, 6f);
                int mask = zones[zone].cardMask;
                if (mask > 0) RuleCardsStory.Bark(mask == 1 || mask == 2 || mask == 4 ? "escape_zone_" + mask : "escape_zone_combo");
            }
        }

        // checkpoints
        for (int i = checkpoint + 1; i < checkpoints.Length; i++)
        {
            if (playerS < checkpointS[i] - .5f) break;
            Vector3 d = checkpoints[i].position - p;
            d.y *= .5f;
            if (d.magnitude < 4.5f && (motor == null || !motor.IsInverted))
            {
                checkpoint = i;
                Notice("Checkpoint — " + checkpoints[i].label, false);
                EscapeFX.Sfx("checkpoint", .45f);
                EscapeFX.Ring(checkpoints[i].position + Vector3.down * .95f, Vector3.up, new Color(.3f, 1f, .7f), 4f, 64);
            }
        }

        // collapse front
        if (Time.time >= wallHoldUntil)
        {
            float gap = playerS - wallS;
            float speed = collapseSpeed * PaceAt(wallS);
            if (gap > 30f) speed = Mathf.Max(speed, 9f);
            else if (gap > 20f) speed = Mathf.Max(speed, 4.5f);
            wallS = Mathf.Min(wallS + speed * dt, routeLength);
        }
        PlaceWall();
        foreach (EscapeCrumble c in crumbles) if (!c.Crumbled && wallS > c.SMax + 1f) c.Crumble();
        if (floor != null) floor.Tick(wallS, dt, Camera.main != null ? Camera.main.transform.position : p);
        if (motor != null)
        {
            float closeness = Mathf.Clamp01(1f - (playerS - wallS) / 18f);
            motor.Shake = closeness * closeness * 1.4f;
        }

        // failure
        Vector3 rp = PointAt(playerS);
        // Normally the tile under you drops first and you fall; this only catches someone standing somewhere untiled.
        if (wallS >= playerS + 1.5f) { Fail("The floor gave way under you."); return; }
        if (p.y < rp.y - fallBelow || p.y > rp.y + fallAbove) { Fail(wallS > playerS - 5f ? "The floor collapsed under you." : motor != null && motor.IsInverted && p.y > rp.y ? "You fell into the sky." : "You fell into the void."); return; }

        // exit
        Bounds exit = new Bounds(exitCenter, exitSize);
        if (exit.Intersects(player.bounds)) Finish();
    }

    private float PaceAt(float s)
    {
        if (pace == null || pace.Length == 0) return 1f;
        int seg = Mathf.Clamp(SegmentAt(s), 0, pace.Length - 1);
        return pace[seg];
    }

    private void PlaceWall()
    {
        if (wall == null) return;
        Vector3 dir = DirectionAt(wallS);
        wall.transform.SetPositionAndRotation(PointAt(wallS), Quaternion.LookRotation(dir, Vector3.up));
        float gap = Mathf.Max(0f, playerS - wallS);
        float danger = Mathf.Clamp01(1f - gap / 22f);
        if (rumble != null) rumble.volume = (.08f + .5f * danger) * RuleCardsSettings.SfxVolume;
        RuleCardsAudio.LoopAt("heartbeat", "heartbeat_loop", danger * danger * .85f, null, .9f + .25f * danger);
        if (wallLight != null) wallLight.intensity = 2f + 3f * danger + Mathf.Sin(Time.time * 13f) * .6f;
    }

    private void Fail(string reason)
    {
        falls++;
        EscapeFX.Sfx("respawn", .5f);
        Respawn(checkpoint, true);
        Notice(reason + " Back to " + checkpoints[checkpoint].label + ".", true);
    }

    private void Respawn(int index, bool resetWall)
    {
        if (checkpoints.Length == 0) return;
        EscapeCheckpoint cp = checkpoints[index];
        if (inventory != null) inventory.ClearAll();
        if (motor != null) motor.Respawn(cp.position, false, cp.yaw);
        else
        {
            player.enabled = false;
            player.transform.SetPositionAndRotation(cp.position, Quaternion.Euler(0f, cp.yaw, 0f));
            player.enabled = true;
        }
        playerS = checkpointS[index];
        // The front may sit "before" the route start; PointAt extrapolates behind the first waypoint.
        if (resetWall)
        {
            wallS = checkpointS[index] - respawnGap;
            wallHoldUntil = Time.time + 2.5f;
            respawnFlash = 1f;
        }
        else wallS = checkpointS[index] - respawnGap - 4f;
        foreach (EscapeCrumble c in crumbles) if (c.Crumbled && c.SMax + 1f >= wallS) c.Restore();
        if (floor != null) floor.ResetTo(wallS);
        PlaceWall();
    }

    private void Finish()
    {
        if (finished) return;
        finished = true;
        EscapeFX.Sfx("checkpoint", .6f, .8f);
        EscapeFX.Burst(player.transform.position, new Color(.4f, 1f, 1f), 120, 9f, .45f, 1.6f);
        if (motor != null) motor.Shake = 0f;
        finishedAt = Time.unscaledTime;
        if (inventory != null) inventory.ClearAll();
        RuleCardsProgress.CompleteStage(RuleCardsProgress.EscapeStageId);
        newBest = RuleCardsProgress.SubmitEscapeTime(runTime);
        newBestRun = RuleCardsRunTimer.FinishRun();
        // Freeze the player without disabling the motor (that would hand control back to PlayerMovement).
        if (inventory != null) inventory.enabled = false;
        MouseLook.isPaused = true;
        if (rumble != null) rumble.Stop();
        if (wall != null) wall.SetActive(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        // Glyph's farewell first (first escape only), then the results screen.
        awaitingEpilogue = true;
        if (rumble != null) rumble.Stop();
        RuleCardsAudio.PlayMusic("ending", false);
        RuleCardsStory.Play("epilogue", () => { awaitingEpilogue = false; finishedAt = Time.unscaledTime; }, true, .9f, true);
    }

    private void UpdateEnding()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (Time.unscaledTime - finishedAt < 2.4f) return;
        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return)) RuleCardsScenes.Load(RuleCardsScenes.Hub);
    }

    // ------------------------------------------------------------------ messages (also used by CardInventory)
    public void Notify(string text, bool error)
    {
        Notice(text, error);
        if (error) EscapeFX.Sfx("deny", .25f);
    }

    private void OnLanded(float impact)
    {
        if (impact < 9f || motor == null) return;
        Vector3 feet = player.transform.position + motor.GravityDirection * (player.height * .5f - .05f);
        EscapeFX.Ring(feet, Vector3.up, new Color(.5f, .9f, 1f, .7f), Mathf.Clamp(impact * .12f, 1.2f, 3f), 28);
        EscapeFX.Sfx("land", Mathf.Clamp01(impact / 22f) * .6f);
    }

    private void ApplyAtmosphere()
    {
        RenderSettings.skybox = null;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = skyAmbient;
        RenderSettings.ambientEquatorColor = equatorAmbient;
        RenderSettings.ambientGroundColor = groundAmbient;
        foreach (Camera cam in FindObjectsOfType<Camera>())
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = fogColor;
        }
        foreach (Light l in FindObjectsOfType<Light>())
            if (l.type == LightType.Directional)
            {
                l.color = new Color(.55f, .65f, 1f);
                l.intensity = .6f;
            }
    }
    public void OnCardUsed(RuleCardKind kind) { }

    private void Notice(string text, bool error)
    {
        notice = text;
        noticeError = error;
        noticeUntil = Time.unscaledTime + (error ? 2.4f : 2f);
    }

    private void Banner(string title, string sub, float seconds)
    {
        banner = title;
        bannerSub = sub ?? "";
        bannerUntil = Time.unscaledTime + seconds;
    }

    // ------------------------------------------------------------------ GUI
    private void OnGUI()
    {
        if (player == null) return;
        float vw, vh;
        Matrix4x4 previous = ResponsiveIMGUI.Begin(out vw, out vh);
        int oldDepth = GUI.depth;
        if (finished) { RuleCardsRunHud.Suppress(); if (!awaitingEpilogue) { GUI.depth = -100; DrawEnding(vw, vh); } }
        else if (!MouseLook.isPaused)
        {
            DrawDanger(vw, vh);
            DrawHud(vw, vh);
            DrawHotbar(vw, vh);
            DrawBanner(vw, vh);
        }
        GUI.depth = oldDepth;
        ResponsiveIMGUI.End(previous);
    }

    private void DrawDanger(float vw, float vh)
    {
        float gap = Mathf.Max(0f, playerS - wallS);
        float danger = Mathf.Clamp01(1f - gap / 16f);
        float a = danger * (.25f + .1f * Mathf.Sin(Time.unscaledTime * 10f)) + respawnFlash * .3f;
        if (a <= .01f) return;
        Color c = new Color(.8f, .02f, .3f, a);
        float t = 60f;
        // Soft red edge glow that fades inward (was four hard bars).
        t = 90f;
        Color clear = new Color(c.r, c.g, c.b, 0f);
        RuleCardsUI.Gradient(new Rect(0f, 0f, vw, t), c, clear);
        RuleCardsUI.Gradient(new Rect(0f, vh - t, vw, t), clear, c);
        RuleCardsUI.Gradient(new Rect(0f, 0f, t, vh), c, clear, 0, true);
        RuleCardsUI.Gradient(new Rect(vw - t, 0f, t, vh), clear, c, 0, true);
        if (respawnFlash > 0f) RepositoryGUI.Fill(new Rect(0f, 0f, vw, vh), new Color(0f, 0f, 0f, Mathf.Clamp01(respawnFlash * 1.4f)));
    }

    private void DrawHud(float vw, float vh)
    {
        // crosshair
        RepositoryGUI.Fill(new Rect(vw * .5f - 2f, vh * .5f - 2f, 4f, 4f), new Color(1f, 1f, 1f, .8f));

        // Run timer card (stage + total) is drawn by RuleCardsRunHud; this card stacks under it.
        Rect box = new Rect(RuleCardsRunHud.Margin, RuleCardsRunHud.NextY, Mathf.Min(300f, vw - 32f), 46f);
        RuleCardsUI.HudCard(box);
        GUI.Label(new Rect(box.x + 14f, box.y + 6f, 40f, 18f), "Falls", RuleCardsUI.Label(12, RuleCardsUI.Muted));
        GUI.Label(new Rect(box.x + 56f, box.y + 6f, 60f, 18f), falls.ToString(), RuleCardsUI.Number(15, RuleCardsUI.Text, TextAnchor.MiddleLeft));
        GUI.Label(new Rect(box.x + 120f, box.y + 6f, box.width - 134f, 18f), "Collapse", RuleCardsUI.Label(12, RuleCardsUI.Muted, TextAnchor.MiddleRight));
        float progress = routeLength > 0f ? Mathf.Clamp01(playerS / routeLength) : 0f;
        Rect bar = new Rect(box.x + 14f, box.y + 30f, box.width - 28f, 4f);
        RepositoryGUI.Fill(bar, new Color(1f, 1f, 1f, .12f));
        RepositoryGUI.Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(wallS / Mathf.Max(1f, routeLength)), bar.height), new Color(.85f, .25f, .4f, .85f));
        RepositoryGUI.Fill(new Rect(bar.x + bar.width * progress - 1.5f, bar.y - 3f, 3f, bar.height + 6f), RuleCardsUI.GoldBright);

        if (Time.unscaledTime < noticeUntil && !string.IsNullOrEmpty(notice))
        {
            Rect r = new Rect((vw - Mathf.Min(560f, vw - 24f)) * .5f, vh * .5f + 40f, Mathf.Min(560f, vw - 24f), 30f);
            GUI.Label(r, notice, RepositoryGUI.Text(15, TextAnchor.MiddleCenter, noticeError ? new Color(1f, .55f, .4f) : new Color(.45f, 1f, .7f), true));
        }
    }

    private void DrawHotbar(float vw, float vh)
    {
        if (inventory == null) return;
        float w = Mathf.Min(150f, (vw - 48f) / 3f), h = 58f, gapX = 8f;
        float x0 = (vw - (w * 3f + gapX * 2f)) * .5f, y = vh - h - 16f;
        for (int i = 0; i < RuleCardInfo.Count; i++)
        {
            var kind = (RuleCardKind)i;
            Rect r = new Rect(x0 + i * (w + gapX), y, w, h);
            bool owned = inventory.Owns(kind);
            bool allowed = (inventory.AllowedMask & (1 << i)) != 0;
            bool selected = inventory.Selected == kind;
            Color c = RuleCardInfo.Colors[i];
            RepositoryGUI.Fill(r, new Color(.02f, .03f, .06f, selected ? .92f : .7f));
            if (owned && allowed) RepositoryGUI.Fill(new Rect(r.x, r.y, r.width, 4f), c);
            float cd = inventory.CooldownFraction(kind);
            if (cd > 0f) RepositoryGUI.Fill(new Rect(r.x, r.y + 4f, r.width * cd, r.height - 4f), new Color(1f, 1f, 1f, .1f));
            RepositoryGUI.Frame(r, selected ? Color.white : RepositoryGUI.WithAlpha(c, .5f), selected ? 2f : 1f);
            Color text = !owned ? new Color(.4f, .4f, .45f) : allowed ? c : new Color(.5f, .5f, .55f);
            GUI.Label(new Rect(r.x + 6f, r.y + 6f, 20f, 18f), (i + 1).ToString(), RepositoryGUI.Text(12, TextAnchor.UpperLeft, new Color(.8f, .8f, .85f), true));
            GUI.Label(new Rect(r.x + 4f, r.y + 10f, r.width - 8f, 26f), RuleCardInfo.Names[i], RepositoryGUI.Text(13, TextAnchor.MiddleCenter, text, true));
            string sub = !owned ? "NOT RECOVERED" : !allowed ? "INTERFERENCE"
                : kind == RuleCardKind.SolidifyAir ? "FIELDS " + inventory.ActivePlatforms + "/" + inventory.airBudget
                : kind == RuleCardKind.InvertGravity ? (motor != null && motor.IsInverted ? "CEILING" : "FLOOR") : "RMB • LINK";
            GUI.Label(new Rect(r.x + 4f, r.y + 34f, r.width - 8f, 18f), sub, RepositoryGUI.Text(10, TextAnchor.MiddleCenter, new Color(.75f, .8f, .9f)));
        }
        GUI.Label(new Rect(x0, y - 20f, w * 3f + gapX * 2f, 18f), "1 / 2 / 3 or scroll to select  •  Right mouse to use",
            RepositoryGUI.Text(11, TextAnchor.MiddleCenter, new Color(.6f, .66f, .75f)));
    }

    private void DrawBanner(float vw, float vh)
    {
        if (Time.unscaledTime > bannerUntil || string.IsNullOrEmpty(banner)) return;
        float alpha = Mathf.Clamp01((bannerUntil - Time.unscaledTime) / .6f);
        float w = Mathf.Min(640f, vw - 24f);
        Rect r = new Rect((vw - w) * .5f, Mathf.Min(110f, vh * .16f), w, 70f);
        RepositoryGUI.Fill(r, new Color(.01f, .02f, .04f, .78f * alpha));
        GUI.Label(new Rect(r.x, r.y + 6f, w, 30f), banner, RepositoryGUI.Text(20, TextAnchor.MiddleCenter, new Color(1f, .5f, .75f, alpha), true));
        GUI.Label(new Rect(r.x + 12f, r.y + 36f, w - 24f, 30f), bannerSub, RepositoryGUI.Text(13, TextAnchor.MiddleCenter, new Color(.9f, .92f, 1f, alpha)));
    }

    private void DrawEnding(float vw, float vh)
    {
        float t = Time.unscaledTime - finishedAt;
        float fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 1.2f));
        RepositoryGUI.Fill(new Rect(0f, 0f, vw, vh), new Color(.005f, .015f, .025f, .97f * fade));
        float a = Mathf.Clamp01((t - .8f) / .8f);
        if (a <= 0f) return;
        float w = Mathf.Min(760f, vw - 32f), x = (vw - w) * .5f, y = Mathf.Max(24f, vh * .14f), cx = vw * .5f;
        GUI.Label(new Rect(x, y, w, 20f), "T H E   U N W R I T T E N   A R C H I V E", RuleCardsUI.Label(14, RepositoryGUI.WithAlpha(RuleCardsUI.Gold, a), TextAnchor.MiddleCenter));
        GUI.Label(new Rect(x - 40f, y + 20f, w + 80f, 64f), "You Escaped the Archive",
            RuleCardsUI.Title(Mathf.RoundToInt(Mathf.Clamp(vw / 22f, 30f, 48f)), RepositoryGUI.WithAlpha(RuleCardsUI.Cyan, a)));
        RuleCardsUI.Divider(cx, y + 94f, Mathf.Min(420f, w), RuleCardsUI.Gold, a);
        for (int i = 0; i < 3; i++)
            RuleCardsUI.Diamond(new Vector2(cx - 26f + i * 26f, y + 112f), 9f, RuleCardInfo.Colors[i], a);
        GUI.Label(new Rect(x + 40f, y + 128f, w - 80f, 56f),
            "The Archive folded itself shut behind you. The three Rule Cards are still warm in your hand.",
            RuleCardsUI.ProseItalic(19, new Color(.86f, .9f, 1f, a), TextAnchor.UpperCenter));

        // Stat strip: this escape, falls, best escape, whole run.
        float best = RuleCardsProgress.BestEscapeTime, bestRun = RuleCardsRunTimer.BestRun;
        Rect strip = new Rect(x, y + 190f, w, 92f);
        RuleCardsUI.Panel(strip, a * .9f);
        float colW = strip.width / 4f;
        StatCell(new Rect(strip.x + colW * 0f, strip.y + 14f, colW, 64f), "Escape time", RuleCardsRunTimer.Format(runTime), null, RuleCardsUI.Text, a);
        StatCell(new Rect(strip.x + colW * 1f, strip.y + 14f, colW, 64f), "Falls", falls.ToString(), null, RuleCardsUI.Text, a);
        StatCell(new Rect(strip.x + colW * 2f, strip.y + 14f, colW, 64f), "Best escape", best > 0f ? RuleCardsRunTimer.Format(best) : "—",
            newBest ? "New best" : null, RuleCardsUI.GoldBright, a);
        StatCell(new Rect(strip.x + colW * 3f, strip.y + 14f, colW, 64f), "Full run", RuleCardsRunTimer.Format(RuleCardsRunTimer.Total),
            RuleCardsRunTimer.Partial ? "Partial" : newBestRun ? "New best" : bestRun > 0f ? "Best " + RuleCardsRunTimer.Format(bestRun) : null, RuleCardsUI.Cyan, a);
        for (int i = 1; i < 4; i++) RepositoryGUI.Fill(new Rect(strip.x + colW * i, strip.y + 18f, 1f, strip.height - 36f), new Color(1f, 1f, 1f, .08f * a));

        string splits = RuleCardsRunTimer.SplitLine();
        GUI.Label(new Rect(x, strip.yMax + 10f, w, 20f), splits, RuleCardsUI.Label(14, RepositoryGUI.WithAlpha(RuleCardsUI.Muted, a), TextAnchor.MiddleCenter));
        if (RuleCardsRunTimer.Partial)
            GUI.Label(new Rect(x, strip.yMax + 30f, w, 20f), "This run started mid-save, so earlier stages weren't timed. Start a New Game for a full time.",
                RuleCardsUI.ProseItalic(14, RepositoryGUI.WithAlpha(RuleCardsUI.Muted, a)));

        if (t < 2.4f) return;
        float bw = Mathf.Min(280f, (w - 24f) * .5f), by = Mathf.Min(strip.yMax + 64f, vh - 70f);
        if (GUI.Button(new Rect(cx - bw - 8f, by, bw, 44f), "Return to the Archive   (E)", RuleCardsUI.PrimaryButton)) RuleCardsScenes.Load(RuleCardsScenes.Hub);
        if (GUI.Button(new Rect(cx + 8f, by, bw, 44f), "Main Menu")) RuleCardsScenes.Load(RuleCardsScenes.Title);
    }

    private static void StatCell(Rect r, string label, string value, string tag, Color valueColor, float a)
    {
        GUI.Label(new Rect(r.x, r.y, r.width, 16f), label, RuleCardsUI.Label(13, RepositoryGUI.WithAlpha(RuleCardsUI.Muted, a), TextAnchor.MiddleCenter));
        GUI.Label(new Rect(r.x, r.y + 16f, r.width, 32f), value, RuleCardsUI.Number(26, RepositoryGUI.WithAlpha(valueColor, a)));
        if (!string.IsNullOrEmpty(tag))
            GUI.Label(new Rect(r.x, r.y + 48f, r.width, 16f), tag, RuleCardsUI.Label(12, RepositoryGUI.WithAlpha(tag == "New best" ? RuleCardsUI.GoldBright : RuleCardsUI.Muted, a), TextAnchor.MiddleCenter));
    }

    // ------------------------------------------------------------------ helpers
    private static AudioClip RumbleLoop()
    {
        const int rate = 22050;
        var samples = new float[rate * 2];
        var random = new System.Random(9931);
        float low = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)rate;
            low = Mathf.Lerp(low, (float)random.NextDouble() * 2f - 1f, .015f);
            samples[i] = low * 2.4f + .18f * Mathf.Sin(t * 41f * Mathf.PI * 2f);
        }
        // crossfade the loop seam
        int fadeLength = rate / 10;
        for (int i = 0; i < fadeLength; i++)
        {
            float k = i / (float)fadeLength;
            samples[i] = Mathf.Lerp(samples[samples.Length - fadeLength + i], samples[i], k);
        }
        var clip = AudioClip.Create("Collapse_Rumble", samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, .3f, .6f);
        for (int i = 1; i < route.Length; i++) Gizmos.DrawLine(route[i - 1], route[i]);
        Gizmos.color = new Color(.3f, 1f, .6f);
        foreach (EscapeCheckpoint cp in checkpoints) Gizmos.DrawWireSphere(cp.position, .6f);
        foreach (EscapeZone z in zones)
        {
            Gizmos.color = new Color(z.cardMask & 1, (z.cardMask >> 1) & 1, (z.cardMask >> 2) & 1, .5f);
            Gizmos.DrawWireCube(z.center, z.size);
        }
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(exitCenter, exitSize);
    }

    private void OnDestroy()
    {
        RuleCardsRunHud.StageTime = null;
        if (wallMaterial != null) Destroy(wallMaterial);
        if (motor != null) motor.Landed -= OnLanded;
        if (motes != null) Destroy(motes.gameObject);
        if (rumble != null && rumble.clip != null) Destroy(rumble.clip);
    }
}
