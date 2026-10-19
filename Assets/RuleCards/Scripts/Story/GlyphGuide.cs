using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Glyph, the Archive's index construct. Floats at the edge of your view, moves in front of you when it
/// talks, and renders its own portrait for the dialogue box. Uses Resources/Story/GlyphModel when that
/// prefab exists (children named "Ring…" spin, "Eye" pulses); otherwise builds a runic placeholder.
/// </summary>
public sealed class GlyphGuide : MonoBehaviour
{
    public static GlyphGuide Instance { get; private set; }
    /// <summary>Set by the story every frame: Glyph is talking (comes into view and faces you).</summary>
    public static bool Speaking;
    /// <summary>Set by the story every frame: a cutscene shot is framing Glyph.</summary>
    public static bool Spotlight;

    private enum Mode { Follow, Hidden, Leaving }
    private Mode mode = Mode.Follow;

    private Transform model, ringA, ringB, core;
    private Transform[] motes = new Transform[0];
    private Renderer[] renderers = new Renderer[0];
    private Material eyeMaterial;
    private Color eyeEmission = new Color(.35f, 1f, 1f) * 1.5f;
    private Light glow;
    private Camera cam;
    private Vector3 velocity;
    private float scale = 1f, targetScale = 1f, pulse, leaveT;
    private Vector3 leaveFrom, leaveDir;
    private bool placed;

    private RenderTexture portrait;
    private Camera portraitCam;
    private int portraitRequestFrame = -10;

    public float EyePulse { get { return Mathf.Clamp01(pulse); } }

    /// <summary>The live portrait (null until first requested).</summary>
    public Texture PortraitTexture { get { return portraitCam != null && mode != Mode.Hidden ? portrait : null; } }

    // ------------------------------------------------------------------ lifecycle
    public static void Spawn(bool hidden)
    {
        if (Instance != null) Destroy(Instance.gameObject);
        var go = new GameObject("Glyph_Guide");
        SceneManager.MoveGameObjectToScene(go, SceneManager.GetActiveScene());
        Instance = go.AddComponent<GlyphGuide>();
        Instance.Build();
        if (hidden) { Instance.mode = Mode.Hidden; Instance.scale = Instance.targetScale = 0f; Instance.ApplyScale(); }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (portrait != null) { portrait.Release(); Destroy(portrait); }
    }

    public void Hide() { mode = Mode.Hidden; targetScale = 0f; }

    public void Appear()
    {
        mode = Mode.Follow;
        targetScale = 1f;
        if (scale < .05f) { scale = .05f; placed = false; }
        pulse = 2.5f;
        RuleCardsAudio.PlayAt("glyph_appear", transform.position, .7f);
    }

    /// <summary>Glyph turns back into the Archive and fades out.</summary>
    public void Leave(bool instant)
    {
        if (instant) { Hide(); scale = 0f; ApplyScale(); return; }
        mode = Mode.Leaving;
        leaveT = 0f;
        leaveFrom = transform.position;
        Vector3 up = Up();
        Vector3 back = cam != null ? -Vector3.ProjectOnPlane(cam.transform.forward, up).normalized : -transform.forward;
        leaveDir = (back * 1f + up * .35f).normalized;
        pulse = 2f;
    }

    public void Pulse(float amount) { pulse = Mathf.Max(pulse, amount); Ripple(); }

    public void RequestPortrait() { portraitRequestFrame = Time.frameCount; }

    // ------------------------------------------------------------------ model
    private void Build()
    {
        model = new GameObject("Model").transform;
        model.SetParent(transform, false);
        GameObject custom = Resources.Load<GameObject>("Story/GlyphModel");
        if (custom != null) BuildFromPrefab(custom);
        else BuildPlaceholder();

        renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        foreach (Collider c in GetComponentsInChildren<Collider>()) { c.enabled = false; Destroy(c); }

        glow = new GameObject("GlyphLight").AddComponent<Light>();
        glow.transform.SetParent(transform, false);
        glow.transform.localPosition = new Vector3(0f, .1f, .45f);
        glow.type = LightType.Point;
        glow.color = new Color(.45f, 1f, 1f);
        glow.range = 2.2f;
        glow.intensity = .35f;
        glow.shadows = LightShadows.None;
    }

    // ---- custom model (Resources/Story/GlyphModel): Artifact core + six *RunePlate bones
    private Transform artifact;
    private Transform[] plates = new Transform[0];
    private Vector3[] plateBasePos, plateCenter, plateAxis;
    private Quaternion[] plateBaseRot;
    private Quaternion artifactBaseRot;
    private Material[] skinMaterials = new Material[0];
    private Color skinEmission = Color.white * 1.3f;
    private float talk;

    // voice rings: thin cyan circles that ripple out of the eye as Glyph speaks
    private LineRenderer[] voiceRings = new LineRenderer[0];
    private float[] ringAge = new float[0];
    private int nextRing;
    private float lastRingAt = -10f;

    private void BuildFromPrefab(GameObject prefab)
    {
        // The model's eye looks down its -Z; turn it so Glyph's forward is the eye.
        Transform pivot = new GameObject("Pivot").transform;
        pivot.SetParent(model, false);
        pivot.localRotation = Quaternion.Euler(0f, 180f, 0f);
        GameObject go = Instantiate(prefab);
        go.name = "GlyphModel";
        go.transform.SetParent(pivot, false);

        Material shared = Resources.Load<Material>("Story/GlyphMaterial");
        var mats = new System.Collections.Generic.List<Material>();
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (shared != null) r.sharedMaterial = shared;
            Material inst = r.material; // own instance so the glow can pulse
            if (inst.HasProperty("_EmissionColor")) { skinEmission = inst.GetColor("_EmissionColor"); mats.Add(inst); }
            var skin = r as SkinnedMeshRenderer;
            if (skin != null) skin.updateWhenOffscreen = true; // bones move procedurally
        }
        skinMaterials = mats.ToArray();

        // Normalise to ~0.5 m and centre it on Glyph's origin.
        Bounds b = new Bounds();
        bool any = false;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
        if (any && b.size.magnitude > .001f)
        {
            go.transform.localScale *= .5f / Mathf.Max(b.size.x, b.size.y, b.size.z);
            any = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            go.transform.position += model.position - b.center;
        }

        var found = new System.Collections.Generic.List<Transform>();
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Artifact") artifact = t;
            else if (t.name.EndsWith("RunePlate")) found.Add(t);
        }
        plates = found.ToArray();
        int n = plates.Length;
        plateBasePos = new Vector3[n];
        plateBaseRot = new Quaternion[n];
        plateCenter = new Vector3[n];
        plateAxis = new Vector3[n];
        Vector3 coreWorld = artifact != null ? artifact.position : model.position;
        for (int i = 0; i < n; i++)
        {
            Transform t = plates[i];
            plateBasePos[i] = t.localPosition;
            plateBaseRot[i] = t.localRotation;
            Transform parent = t.parent;
            plateCenter[i] = parent != null ? parent.InverseTransformPoint(coreWorld) : coreWorld;
            Vector3 axis = parent != null ? parent.InverseTransformDirection(model.forward) : model.forward;
            plateAxis[i] = axis.sqrMagnitude > 1e-6f ? axis.normalized : Vector3.forward;
        }
        if (artifact != null) artifactBaseRot = artifact.localRotation;
        BuildVoiceRings();
    }

    private void BuildVoiceRings()
    {
        Material mat = Resources.Load<Material>("RuleCardsParticle");
        if (mat == null) return;
        voiceRings = new LineRenderer[3];
        ringAge = new float[3];
        for (int i = 0; i < 3; i++)
        {
            var go = new GameObject("VoiceRing" + i);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, .16f);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = mat;
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = 40;
            for (int k = 0; k < 40; k++)
            {
                float a = k / 40f * Mathf.PI * 2f;
                lr.SetPosition(k, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f));
            }
            lr.widthMultiplier = .012f;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.enabled = false;
            voiceRings[i] = lr;
            ringAge[i] = 99f;
        }
    }

    private void AnimateCustom(float t, float dt)
    {
        talk = Mathf.MoveTowards(talk, Speaking || Spotlight ? 1f : 0f, dt * 2.5f);
        float p = Mathf.Clamp01(pulse);
        // The halo of rune plates breathes, sways around the eye axis, and spreads out while Glyph talks.
        float sway = Mathf.Sin(t * .55f) * 8f + talk * Mathf.Sin(t * 2.6f) * 5f;
        for (int i = 0; i < plates.Length; i++)
        {
            if (plates[i] == null) continue;
            float ph = i * 1.13f;
            float expand = 1f + .035f * Mathf.Sin(t * 1.3f + ph) + .1f * talk + .1f * p;
            Quaternion q = Quaternion.AngleAxis(sway + Mathf.Sin(t * 1.7f + ph) * 2.5f, plateAxis[i]);
            plates[i].localPosition = plateCenter[i] + q * ((plateBasePos[i] - plateCenter[i]) * expand);
            plates[i].localRotation = q * plateBaseRot[i] * Quaternion.Euler(Mathf.Sin(t * 1.1f + ph) * 4f, Mathf.Sin(t * .8f + ph) * 4f, 0f);
        }
        if (artifact != null)
            artifact.localRotation = artifactBaseRot * Quaternion.Euler(Mathf.Sin(t * .9f) * 3f, Mathf.Sin(t * .6f) * 3f, Mathf.Sin(t * .7f) * 2f);

        // Runes and eye flicker brighter with each syllable.
        float glowK = .8f + talk * .35f * (.5f + .5f * Mathf.Sin(t * 13f)) + p * .55f + .08f * Mathf.Sin(t * 2.4f);
        foreach (Material m in skinMaterials) if (m != null) m.SetColor("_EmissionColor", skinEmission * glowK);

        for (int i = 0; i < voiceRings.Length; i++)
        {
            LineRenderer lr = voiceRings[i];
            if (lr == null) continue;
            ringAge[i] += dt;
            float k = ringAge[i] / .65f;
            lr.enabled = k < 1f && scale > .05f;
            if (!lr.enabled) continue;
            float r = Mathf.Lerp(.09f, .36f, Mathf.Sqrt(k));
            lr.transform.localScale = new Vector3(r, r, 1f);
            lr.widthMultiplier = .014f * (1f - k) + .003f;
            Color c = new Color(.45f, 1f, 1f, (1f - k) * .55f);
            lr.startColor = c;
            lr.endColor = c;
        }
    }

    /// <summary>Called on chirps: a voice ring ripples out of the eye.</summary>
    private void Ripple()
    {
        if (voiceRings.Length == 0 || Time.time - lastRingAt < .14f) return;
        lastRingAt = Time.time;
        ringAge[nextRing] = 0f;
        nextRing = (nextRing + 1) % voiceRings.Length;
    }

    private static Material Mat(Color baseColor, Color emission, float smooth, float metal)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var m = new Material(shader) { name = "Glyph_Runtime" };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseColor);
        if (m.HasProperty("_Color")) m.SetColor("_Color", baseColor);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
        if (emission.maxColorComponent > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        return m;
    }

    private Transform Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 euler, Vector3 size, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        Collider c = go.GetComponent<Collider>();
        if (c != null) { c.enabled = false; Destroy(c); }
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = euler;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go.transform;
    }

    private Transform Octahedron(Transform parent, Vector3 pos, Vector3 size, Material mat, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        Vector3[] v = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
        int[][] faces = { new[] { 0, 4, 3 }, new[] { 0, 3, 5 }, new[] { 0, 5, 2 }, new[] { 0, 2, 4 },
                          new[] { 1, 3, 4 }, new[] { 1, 5, 3 }, new[] { 1, 2, 5 }, new[] { 1, 4, 2 } };
        var verts = new Vector3[24];
        var tris = new int[24];
        for (int f = 0; f < 8; f++)
            for (int k = 0; k < 3; k++) { verts[f * 3 + k] = v[faces[f][k]] * .5f; tris[f * 3 + k] = f * 3 + k; }
        var mesh = new Mesh { name = "Glyph_Octahedron", vertices = verts, triangles = tris };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go.transform;
    }

    /// <summary>Runic placeholder: dark stone core, gold trim, cyan rune plates, one eye.</summary>
    private void BuildPlaceholder()
    {
        Material stone = Mat(new Color(.07f, .085f, .11f), Color.black, .55f, .15f);
        Material gold = Mat(new Color(.86f, .66f, .32f), new Color(.1f, .065f, .02f), .7f, .9f);
        Material rune = Mat(new Color(.2f, .9f, 1f), new Color(.3f, 1f, 1f) * .9f, .4f, 0f);
        eyeMaterial = Mat(new Color(.6f, 1f, 1f), eyeEmission, .9f, 0f);

        core = Octahedron(model, Vector3.zero, new Vector3(.27f, .36f, .27f), stone, "Core");
        // Carved rune band around the core's waist.
        for (int i = 0; i < 8; i++)
        {
            float a = i * 45f;
            Vector3 p = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, .095f);
            Part(PrimitiveType.Cube, core, p / .27f, new Vector3(0f, a, 0f), new Vector3(.05f / .27f, .018f / .36f, .01f / .27f), rune);
        }
        // Gold collar + eye.
        Part(PrimitiveType.Cylinder, model, new Vector3(0f, 0f, .085f), new Vector3(90f, 0f, 0f), new Vector3(.12f, .012f, .12f), gold);
        Transform eye = Part(PrimitiveType.Sphere, model, new Vector3(0f, 0f, .098f), Vector3.zero, new Vector3(.075f, .075f, .04f), eyeMaterial);
        eye.name = "Eye";

        ringA = new GameObject("RingA").transform;
        ringA.SetParent(model, false);
        ringA.localEulerAngles = new Vector3(12f, 0f, 0f);
        for (int i = 0; i < 10; i++)
        {
            float a = i * 36f;
            Vector3 p = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, .3f);
            Part(PrimitiveType.Cube, ringA, p, new Vector3(0f, a, 0f), new Vector3(.055f, .095f, .012f), i % 3 == 0 ? gold : rune);
        }
        ringB = new GameObject("RingB").transform;
        ringB.SetParent(model, false);
        ringB.localEulerAngles = new Vector3(68f, 0f, 20f);
        for (int i = 0; i < 6; i++)
        {
            float a = i * 60f;
            Vector3 p = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, .39f);
            Part(PrimitiveType.Cube, ringB, p, new Vector3(45f, a, 45f), new Vector3(.03f, .03f, .03f), gold);
        }
        motes = new Transform[3];
        for (int i = 0; i < 3; i++) motes[i] = Octahedron(model, new Vector3(0f, -.3f - i * .09f, 0f), Vector3.one * (.06f - i * .013f), i == 0 ? gold : rune, "Mote" + i);
    }

    // ------------------------------------------------------------------ motion
    private Vector3 Up()
    {
        if (cam != null && cam.transform.parent != null) return cam.transform.parent.up;
        return Vector3.up;
    }

    private void LateUpdate()
    {
        if (cam == null || !cam.isActiveAndEnabled)
        {
            cam = Camera.main;
            if (cam == null) return;
        }
        float dt = Time.deltaTime;
        float t = Time.time;
        Transform c = cam.transform;
        Vector3 up = Up();
        Vector3 fwd = Vector3.ProjectOnPlane(c.forward, up);
        fwd = fwd.sqrMagnitude > .01f ? fwd.normalized : c.forward;
        Vector3 right = Vector3.Cross(up, fwd).normalized;

        Vector3 desired;
        if (mode == Mode.Leaving)
        {
            leaveT += dt;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(leaveT / 3.2f));
            desired = leaveFrom + leaveDir * 7f * k + up * Mathf.Sin(leaveT * 2f) * .1f;
            transform.position = desired;
            targetScale = leaveT > 2.2f ? 0f : 1f;
            if (leaveT > 3.4f) mode = Mode.Hidden;
        }
        else
        {
            if (Spotlight) desired = c.position + c.forward * 1.35f - up * .06f;
            else if (Speaking) desired = c.position + fwd * 1.55f + right * .72f + up * .02f;
            else desired = c.position + fwd * .8f + right * 1.02f + up * .22f;
            // Don't float inside walls.
            Vector3 to = desired - c.position;
            RaycastHit hit;
            if (Physics.SphereCast(c.position, .12f, to.normalized, out hit, to.magnitude, ~0, QueryTriggerInteraction.Ignore))
                desired = c.position + to.normalized * Mathf.Max(.45f, hit.distance - .2f);
            desired += up * Mathf.Sin(t * 1.7f) * .045f;
            if (!placed) { transform.position = desired; velocity = Vector3.zero; placed = true; }
            else transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, Spotlight ? .22f : .42f, Mathf.Infinity, dt);
        }

        // Facing: look at you while talking, otherwise drift along with you.
        Vector3 face = (Speaking || Spotlight || mode == Mode.Leaving) ? c.position - transform.position : fwd;
        if (mode == Mode.Leaving && leaveT > 1.4f) face = leaveDir;
        if (face.sqrMagnitude > .001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face, up), 1f - Mathf.Exp(-6f * dt));

        // Idle animation.
        if (artifact != null || plates.Length > 0) AnimateCustom(t, dt);
        if (ringA != null) ringA.Rotate(0f, 42f * dt, 0f, Space.Self);
        if (ringB != null) ringB.Rotate(0f, -58f * dt, 0f, Space.Self);
        if (core != null) core.localRotation = Quaternion.Euler(Mathf.Sin(t * .9f) * 6f, Mathf.Sin(t * .6f) * 10f, 0f);
        for (int i = 0; i < motes.Length; i++)
            if (motes[i] != null)
            {
                motes[i].localPosition = new Vector3(Mathf.Sin(t * 1.3f + i) * .02f, -.3f - i * .09f + Mathf.Sin(t * 2.1f + i * 1.7f) * .025f, 0f);
                motes[i].Rotate(0f, 90f * dt, 0f, Space.Self);
            }

        pulse = Mathf.MoveTowards(pulse, 0f, dt * 2.5f);
        if (eyeMaterial != null && eyeMaterial.HasProperty("_EmissionColor"))
            eyeMaterial.SetColor("_EmissionColor", eyeEmission * (1f + pulse * .7f + .12f * Mathf.Sin(t * 3f)));
        if (glow != null) glow.intensity = (.35f + pulse * .35f) * scale;

        float targetSpeed = targetScale > scale ? 2.2f : 3f;
        scale = Mathf.MoveTowards(scale, targetScale, dt * targetSpeed);
        ApplyScale();
        UpdatePortrait();
    }

    private void ApplyScale()
    {
        // Slight overshoot on appear.
        float s = scale < 1f && targetScale >= 1f ? Mathf.Sin(scale * Mathf.PI * .5f) * (1f + .12f * Mathf.Sin(scale * Mathf.PI)) : scale;
        transform.localScale = Vector3.one * Mathf.Max(0.0001f, s);
        bool visible = scale > .01f;
        foreach (Renderer r in renderers) if (r != null) r.enabled = visible;
        if (glow != null) glow.enabled = visible;
    }

    // ------------------------------------------------------------------ portrait
    private void UpdatePortrait()
    {
        bool wanted = Time.frameCount - portraitRequestFrame <= 2 && mode != Mode.Hidden;
        if (!wanted)
        {
            if (portraitCam != null) portraitCam.enabled = false;
            return;
        }
        if (portraitCam == null)
        {
            portrait = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32) { name = "Glyph_Portrait" };
            var go = new GameObject("Glyph_PortraitCam");
            go.transform.SetParent(transform.parent, false);
            SceneManager.MoveGameObjectToScene(go, gameObject.scene);
            portraitCam = go.AddComponent<Camera>();
            portraitCam.targetTexture = portrait;
            portraitCam.clearFlags = CameraClearFlags.SolidColor;
            portraitCam.backgroundColor = new Color(.02f, .035f, .05f, 1f);
            portraitCam.fieldOfView = 34f;
            portraitCam.nearClipPlane = .05f;
            portraitCam.farClipPlane = 1.35f; // only Glyph: the world behind it is clipped away
            portraitCam.depth = -50f;
            var data = portraitCam.GetUniversalAdditionalCameraData();
            if (data != null) { data.renderPostProcessing = false; data.renderShadows = false; }
        }
        portraitCam.enabled = true;
        Vector3 up = Up();
        Vector3 front = transform.forward;
        portraitCam.transform.position = transform.position + front * .9f * Mathf.Max(.2f, scale) + up * .05f;
        portraitCam.transform.rotation = Quaternion.LookRotation(transform.position - portraitCam.transform.position, up);
    }
}
