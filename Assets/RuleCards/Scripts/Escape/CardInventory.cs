using System.Collections.Generic;
using UnityEngine;

public enum RuleCardKind { KineticLink = 0, InvertGravity = 1, SolidifyAir = 2 }

/// <summary>Shared names/colours/ids for the three Rule Cards.</summary>
public static class RuleCardInfo
{
    public const int Count = 3;
    public static readonly string[] Ids = { "KineticLink", "InvertGravity", "SolidifyAir" };
    public static readonly string[] Names = { "KINETIC LINK", "INVERT GRAVITY", "SOLIDIFY AIR" };
    public static readonly Color[] Colors = {
        new Color(1f, .3f, .85f), new Color(.6f, .5f, 1f), new Color(1f, .85f, .4f)
    };
    public static int Mask(RuleCardKind kind) { return 1 << (int)kind; }
    public const int AllMask = 7;
}

/// <summary>
/// Holdable Rule Card inventory for the escape stage. 1/2/3 or the scroll wheel select a card,
/// right mouse uses it. Requires an <see cref="ArchiveGravityPlayer"/> on the same object.
///   Kinetic Link   — pull yourself to an aimed <see cref="EscapeAnchor"/>.
///   Invert Gravity — while standing, flip onto the ceiling (or back).
///   Solidify Air   — fix air into a platform ahead of you; three at a time, each fades.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public sealed class CardInventory : MonoBehaviour
{
    [Header("Materials (assigned by EscapeStage)")]
    public Material cardTemplate;
    public Material airSolidMaterial;
    public Material airGhostMaterial;

    [Header("Kinetic Link")]
    public float kineticRange = 22f;
    public float kineticAimCone = 14f;
    public float kineticSpeed = 15f;

    [Header("Solidify Air")]
    public int airBudget = 3;
    public float airLifetime = 9f;
    public Vector3 airSize = new Vector3(3f, .4f, 3f);
    public float airMinAhead = 2.2f, airMaxAhead = 3.8f;

    public RuleCardKind Selected { get { return (RuleCardKind)selected; } }
    public bool Owns(RuleCardKind kind) { return owned[(int)kind]; }
    public float CooldownFraction(RuleCardKind kind)
    {
        int i = (int)kind;
        float left = cooldownUntil[i] - Time.time;
        return left <= 0f ? 0f : Mathf.Clamp01(left / Cooldowns[i]);
    }
    public int ActivePlatforms { get { return airPlatforms.Count; } }
    public bool Tethering { get { return tethering; } }
    public int AllowedMask { get; private set; }

    private static readonly float[] Cooldowns = { .6f, .8f, .35f };
    private readonly bool[] owned = new bool[RuleCardInfo.Count];
    private readonly float[] cooldownUntil = new float[RuleCardInfo.Count];
    private int selected;
    private int lastMask = -1;

    private EscapeStage stage;
    private CharacterController controller;
    private ArchiveGravityPlayer motor;
    private Camera view;

    // held card
    private Transform heldRoot;
    private Renderer heldRenderer;
    private Material heldMaterial;
    private TextMesh heldLabel;
    private bool heldModel;
    private Light heldGlow;
    private float useKick, swapDip;
    private Vector2 sway;
    private static readonly Vector3 HeldRest = new Vector3(.2f, -.155f, .45f);

    // kinetic
    private bool tethering;
    private Vector3 tetherFrom, tetherTo;
    private float tetherT, tetherDuration, tetherArc;
    private int stuckFrames;
    private EscapeAnchor focusAnchor;
    private LineRenderer tetherLine;
    private ParticleSystem tetherTrail;
    private Material lineMaterial;

    // air
    private sealed class AirPlatform { public GameObject go; public Renderer renderer; public float born; }
    private readonly List<AirPlatform> airPlatforms = new List<AirPlatform>();
    private GameObject airPreview;
    private bool previewValid;
    private Vector3 previewCenter;
    private Quaternion previewRotation;
    private string airFailReason = "No room to solidify there.";

    public void Init(EscapeStage owner, ArchiveGravityPlayer gravityMotor)
    {
        stage = owner;
        motor = gravityMotor;
        controller = GetComponent<CharacterController>();
        view = motor != null && motor.viewCamera != null ? motor.viewCamera : GetComponentInChildren<Camera>();
        bool any = false;
        for (int i = 0; i < RuleCardInfo.Count; i++) { owned[i] = RuleCardsProgress.HasCard(RuleCardInfo.Ids[i]); any |= owned[i]; }
        if (!any || !owned[0] || !owned[1] || !owned[2])
        {
            // Normal play always arrives with all three; playing the scene directly in the editor may not.
            for (int i = 0; i < RuleCardInfo.Count; i++) owned[i] = true;
            Debug.Log("[RuleCards] Escape stage: not every card is saved yet, lending all three for this run.");
        }
        BuildHeldCard();
        BuildTetherLine();
    }

    // ------------------------------------------------------------------ input
    private void Update()
    {
        if (stage == null || view == null) return;
        bool blocked = MouseLook.isPaused || Time.timeScale == 0f || stage.InputBlocked;
        AllowedMask = stage.AllowedMaskAt(transform.position);
        if (AllowedMask != lastMask)
        {
            if ((AllowedMask & (1 << selected)) == 0)
                for (int i = 0; i < RuleCardInfo.Count; i++)
                    if ((AllowedMask & (1 << i)) != 0 && owned[i]) { Select(i); break; }
            lastMask = AllowedMask;
        }

        UpdateTether(Time.deltaTime);
        UpdateAirPlatforms();
        if (!blocked)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) Select(0);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) Select(1);
            else if (Input.GetKeyDown(KeyCode.Alpha3)) Select(2);
            float scroll = Input.mouseScrollDelta.y;
            if (scroll > .1f) Select((selected + RuleCardInfo.Count - 1) % RuleCardInfo.Count);
            else if (scroll < -.1f) Select((selected + 1) % RuleCardInfo.Count);
        }

        focusAnchor = null;
        foreach (EscapeAnchor a in EscapeAnchor.All) a.Highlighted = false;
        bool allowed = (AllowedMask & (1 << selected)) != 0 && owned[selected];
        if (!blocked && allowed && selected == (int)RuleCardKind.KineticLink && !tethering)
        {
            focusAnchor = FindAnchor();
            if (focusAnchor != null) focusAnchor.Highlighted = true;
        }
        UpdateAirPreview(!blocked && allowed && selected == (int)RuleCardKind.SolidifyAir && !tethering);

        if (!blocked && Input.GetMouseButtonDown(1)) Use();
        AnimateHeldCard(Time.deltaTime, allowed);
    }

    /// <summary>Trailer autopilot: switch to a card and use it straight away (ignores input blocking and cooldown).</summary>
    public bool TrailerUse(RuleCardKind kind)
    {
        Select((int)kind);
        cooldownUntil[selected] = 0f;
        if (tethering) return false;
        bool used = kind == RuleCardKind.KineticLink ? UseKinetic() : kind == RuleCardKind.InvertGravity ? UseGravity() : UseAir();
        if (used) useKick = 1f;
        return used;
    }

    public void TrailerSelect(RuleCardKind kind) { Select((int)kind); }

    private void Select(int index)
    {
        if (index == selected) return;
        selected = index;
        swapDip = 1f;
        RefreshHeldCard();
    }

    private void Use()
    {
        var kind = (RuleCardKind)selected;
        if (!owned[selected]) { stage.Notify("You have not recovered " + RuleCardInfo.Names[selected] + ".", true); return; }
        if ((AllowedMask & (1 << selected)) == 0) { stage.Notify("Rule interference: " + RuleCardInfo.Names[selected] + " will not hold here.", true); return; }
        if (Time.time < cooldownUntil[selected] || tethering) return;

        bool used = false;
        switch (kind)
        {
            case RuleCardKind.KineticLink: used = UseKinetic(); break;
            case RuleCardKind.InvertGravity: used = UseGravity(); break;
            case RuleCardKind.SolidifyAir: used = UseAir(); break;
        }
        if (!used) return;
        cooldownUntil[selected] = Time.time + Cooldowns[selected];
        useKick = 1f;
        stage.OnCardUsed(kind);
    }

    // ------------------------------------------------------------------ kinetic
    private EscapeAnchor FindAnchor()
    {
        Vector3 eye = view.transform.position, forward = view.transform.forward;
        EscapeAnchor best = null;
        float bestAngle = kineticAimCone;
        foreach (EscapeAnchor anchor in EscapeAnchor.All)
        {
            Vector3 to = anchor.transform.position - eye;
            float distance = to.magnitude;
            float range = anchor.maxRange > 0f ? Mathf.Min(anchor.maxRange, kineticRange) : kineticRange;
            if (distance > range || distance < .5f) continue;
            float angle = Vector3.Angle(forward, to);
            if (angle > bestAngle) continue;
            RaycastHit hit;
            if (Physics.Raycast(eye, to / distance, out hit, distance - .6f, ~0, QueryTriggerInteraction.Ignore)
                && hit.collider != controller && !hit.collider.transform.IsChildOf(transform)) continue;
            bestAngle = angle;
            best = anchor;
        }
        return best;
    }

    private bool UseKinetic()
    {
        if (motor != null && motor.IsInverted) { stage.Notify("Kinetic Link needs normal gravity.", true); return false; }
        EscapeAnchor target = focusAnchor != null ? focusAnchor : FindAnchor();
        if (target == null) { stage.Notify("No anchor in sight. Aim at a pink anchor.", true); return false; }
        tethering = true;
        tetherFrom = transform.position;
        tetherTo = target.landingPoint;
        float distance = Vector3.Distance(tetherFrom, tetherTo);
        tetherDuration = Mathf.Clamp(distance / kineticSpeed, .35f, 1.3f);
        tetherArc = 1.2f + .12f * distance;
        tetherT = 0f;
        stuckFrames = 0;
        if (motor != null) { motor.MovementLocked = true; motor.FovKick = 14f; }
        if (tetherLine != null) tetherLine.enabled = true;
        EscapeFX.Sfx("link", .55f);
        EscapeFX.Burst(target.transform.position, RuleCardInfo.Colors[0], 30, 5f, .35f, .6f);
        if (tetherTrail == null) tetherTrail = EscapeFX.Emitter("FX_TetherTrail", transform, RuleCardInfo.Colors[0], Color.white, 90f, .45f, .6f, .18f, ParticleSystemShapeType.Sphere, Vector3.one * .6f);
        if (tetherTrail != null) tetherTrail.Play();
        return true;
    }

    private void UpdateTether(float dt)
    {
        if (!tethering) { if (tetherLine != null) tetherLine.enabled = false; return; }
        tetherT = Mathf.Min(1f, tetherT + dt / tetherDuration);
        float e = Mathf.SmoothStep(0f, 1f, tetherT);
        Vector3 desired = Vector3.Lerp(tetherFrom, tetherTo, e) + Vector3.up * (tetherArc * 4f * e * (1f - e));
        Vector3 delta = desired - transform.position;
        Vector3 before = transform.position;
        controller.Move(delta);
        if (delta.magnitude > .05f && (transform.position - before).magnitude < delta.magnitude * .25f) stuckFrames++;
        else stuckFrames = 0;
        if (tetherLine != null)
        {
            tetherLine.SetPosition(0, heldRoot != null ? heldRoot.position : view.transform.position);
            tetherLine.SetPosition(1, tetherTo + Vector3.up * .8f);
        }
        if (tetherT >= 1f || stuckFrames > 4) EndTether();
    }

    public void EndTether()
    {
        if (!tethering) return;
        tethering = false;
        if (motor != null) motor.MovementLocked = false;
        if (tetherLine != null) tetherLine.enabled = false;
        if (tetherTrail != null) tetherTrail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        EscapeFX.Burst(transform.position + Vector3.down * .9f, RuleCardInfo.Colors[0], 18, 3f, .25f, .45f);
    }

    // ------------------------------------------------------------------ gravity
    private bool UseGravity()
    {
        if (motor == null) return false;
        if (!motor.IsGrounded) { stage.Notify("Stand on a surface to flip gravity.", true); return false; }
        Vector3 feet = transform.position + motor.GravityDirection * (controller.height * .5f - .05f);
        motor.SetInverted(!motor.IsInverted);
        motor.FovKick = 7f;
        EscapeFX.Sfx("flip", .6f, motor.IsInverted ? 1.1f : .9f);
        EscapeFX.Ring(feet, Vector3.up, RuleCardInfo.Colors[1], 3.2f);
        EscapeFX.Burst(transform.position, RuleCardInfo.Colors[1], 24, 2.5f, .3f, .7f);
        return true;
    }

    // ------------------------------------------------------------------ air
    private bool ComputeAirPlacement(out Vector3 center, out Quaternion rotation)
    {
        Transform eye = view.transform;
        Vector3 flat = Vector3.ProjectOnPlane(eye.forward, Vector3.up);
        if (flat.sqrMagnitude < 1e-4f) flat = transform.forward;
        flat.Normalize();
        rotation = Quaternion.LookRotation(flat, Vector3.up);

        float ahead = airMaxAhead;
        RaycastHit hit;
        if (Physics.Raycast(eye.position, eye.forward, out hit, airMaxAhead + 3f, ~0, QueryTriggerInteraction.Ignore)
            && hit.collider != controller)
        {
            float flatDistance = Vector3.ProjectOnPlane(hit.point - transform.position, Vector3.up).magnitude;
            ahead = Mathf.Clamp(flatDistance - 1.6f, airMinAhead, airMaxAhead);
        }
        float feet = transform.position.y + controller.center.y - controller.height * .5f;
        // Look up = higher platform, look down = lower, level = same height as your feet.
        float flatLength = Mathf.Max(.05f, Vector3.ProjectOnPlane(eye.forward, Vector3.up).magnitude);
        float eyeRise = eye.forward.y / flatLength * ahead;
        float top = feet + Mathf.Clamp(eyeRise, -2.5f, 1.1f);
        center = transform.position + flat * ahead;
        center.y = top - airSize.y * .5f;

        Vector3 half = airSize * .5f;
        bool anyInZone = false;
        for (int attempt = 0; attempt < 4; attempt++)
        {
            bool inZone = (stage.AllowedMaskAt(center) & RuleCardInfo.Mask(RuleCardKind.SolidifyAir)) != 0;
            anyInZone |= inZone;
            if (inZone && !Physics.CheckBox(center, half * .98f, rotation, ~0, QueryTriggerInteraction.Ignore)) return true;
            center += flat * .8f;
        }
        airFailReason = anyInZone ? "No room to solidify there." : "The air won't hold beyond this point.";
        return false;
    }

    private void UpdateAirPreview(bool show)
    {
        if (!show)
        {
            if (airPreview != null) airPreview.SetActive(false);
            previewValid = false;
            return;
        }
        if (airPreview == null)
        {
            airPreview = GameObject.CreatePrimitive(PrimitiveType.Cube);
            airPreview.name = "Air_PlacementPreview";
            DestroyCollider(airPreview);
            var r = airPreview.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (airGhostMaterial != null) r.sharedMaterial = airGhostMaterial;
        }
        previewValid = ComputeAirPlacement(out previewCenter, out previewRotation);
        airPreview.SetActive(previewValid);
        if (previewValid)
        {
            airPreview.transform.SetPositionAndRotation(previewCenter, previewRotation);
            airPreview.transform.localScale = airSize;
        }
    }

    private bool UseAir()
    {
        Vector3 center;
        Quaternion rotation;
        if (!ComputeAirPlacement(out center, out rotation)) { stage.Notify(airFailReason, true); return false; }
        if (airPlatforms.Count >= airBudget) RemovePlatform(0);
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Air_SolidField";
        go.transform.SetPositionAndRotation(center, rotation);
        go.transform.localScale = airSize;
        var renderer = go.GetComponent<Renderer>();
        if (airSolidMaterial != null) renderer.sharedMaterial = airSolidMaterial;
        airPlatforms.Add(new AirPlatform { go = go, renderer = renderer, born = Time.time });
        go.AddComponent<AirMaterialize>().target = airSize;
        EscapeFX.Sfx("air", .5f, Random.Range(.94f, 1.08f));
        EscapeFX.Burst(center, RuleCardInfo.Colors[2], 26, 3f, .3f, .6f);
        EscapeFX.Ring(center + Vector3.up * .25f, Vector3.up, RuleCardInfo.Colors[2], 2f, 32);
        return true;
    }

    private void UpdateAirPlatforms()
    {
        for (int i = airPlatforms.Count - 1; i >= 0; i--)
        {
            AirPlatform p = airPlatforms[i];
            if (p.go == null) { airPlatforms.RemoveAt(i); continue; }
            float age = Time.time - p.born;
            float left = airLifetime - age;
            if (left <= 0f) { RemovePlatform(i); continue; }
            // Blink faster during the last two seconds so the player sees it going.
            if (left < 2f) p.renderer.enabled = Mathf.Repeat(age * Mathf.Lerp(4f, 12f, 1f - left / 2f), 1f) > .35f;
        }
    }

    private void RemovePlatform(int index)
    {
        if (index < 0 || index >= airPlatforms.Count) return;
        if (airPlatforms[index].go != null)
        {
            EscapeFX.Burst(airPlatforms[index].go.transform.position, RuleCardInfo.Colors[2], 14, 1.5f, .25f, .5f, .3f);
            EscapeFX.Sfx("airfade", .3f);
            Destroy(airPlatforms[index].go);
        }
        airPlatforms.RemoveAt(index);
    }

    public void ClearAll()
    {
        EndTether();
        for (int i = airPlatforms.Count - 1; i >= 0; i--) RemovePlatform(i);
        for (int i = 0; i < cooldownUntil.Length; i++) cooldownUntil[i] = 0f;
    }

    // ------------------------------------------------------------------ held card visual
    private void BuildHeldCard()
    {
        if (view == null) return;
        heldRoot = new GameObject("HeldRuleCard").transform;
        heldRoot.SetParent(view.transform, false);
        heldRoot.localPosition = HeldRest;
        heldRoot.localRotation = Quaternion.Euler(-6f, -16f, 5f);

        // Prefer the sculpted Rule Card model (Art/Resources/RuleCardsHeldCard); fall back to a slab.
        var model = Resources.Load<GameObject>("RuleCardsHeldCard");
        if (model != null)
        {
            var body = Instantiate(model, heldRoot, false);
            body.name = "CardBody";
            foreach (Collider c in body.GetComponentsInChildren<Collider>()) Destroy(c);
            // Model is 1 m tall with its pivot at the base and its face on +Z: shrink, centre, face the camera.
            body.transform.localScale = Vector3.one * .096f;
            body.transform.localPosition = new Vector3(0f, -.048f, 0f);
            body.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            heldRenderer = body.GetComponentInChildren<Renderer>();
            heldModel = true;
            // A small glow so the card reads against the dark archive.
            var glowObject = new GameObject("CardGlow");
            glowObject.transform.SetParent(heldRoot, false);
            glowObject.transform.localPosition = new Vector3(-.02f, .02f, -.08f);
            heldGlow = glowObject.AddComponent<Light>();
            heldGlow.type = LightType.Point;
            heldGlow.range = .5f;
            heldGlow.intensity = 1.2f;
            heldGlow.shadows = LightShadows.None;
        }
        else
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "CardBody";
            DestroyCollider(body);
            body.transform.SetParent(heldRoot, false);
            body.transform.localScale = new Vector3(.062f, .096f, .003f);
            heldRenderer = body.GetComponent<Renderer>();
        }
        heldRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        heldRenderer.receiveShadows = false;
        if (heldModel)
        {
            heldMaterial = new Material(heldRenderer.sharedMaterial) { name = "HeldCard_Runtime" };
            heldRenderer.sharedMaterial = heldMaterial;
        }
        else if (cardTemplate != null)
        {
            heldMaterial = new Material(cardTemplate) { name = "HeldCard_Runtime" };
            if (heldMaterial.HasProperty("_BaseMap")) heldMaterial.SetTexture("_BaseMap", Texture2D.whiteTexture);
            heldRenderer.sharedMaterial = heldMaterial;
        }
        else heldMaterial = heldRenderer.material;

        var labelObject = new GameObject("CardLabel");
        labelObject.transform.SetParent(heldRoot, false);
        labelObject.transform.localPosition = heldModel ? new Vector3(0f, -.027f, -.0105f) : new Vector3(0f, 0f, -.0025f);
        heldLabel = labelObject.AddComponent<TextMesh>();
        heldLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelObject.GetComponent<MeshRenderer>().sharedMaterial = heldLabel.font.material;
        heldLabel.anchor = TextAnchor.MiddleCenter;
        heldLabel.alignment = TextAlignment.Center;
        heldLabel.fontSize = 48;
        heldLabel.characterSize = heldModel ? .0011f : .0016f;
        RefreshHeldCard();
    }

    private void RefreshHeldCard()
    {
        if (heldMaterial != null)
        {
            Color c = RuleCardInfo.Colors[selected];
            if (heldMaterial.HasProperty("_BaseColor")) heldMaterial.SetColor("_BaseColor", heldModel ? Color.Lerp(Color.white, c, .35f) : c * .6f);
            if (heldMaterial.HasProperty("_Color")) heldMaterial.SetColor("_Color", c);
            if (heldMaterial.HasProperty("_EmissionColor"))
            {
                heldMaterial.EnableKeyword("_EMISSION");
                heldMaterial.SetColor("_EmissionColor", c * 1.6f);
            }
        }
        if (heldLabel != null)
        {
            heldLabel.text = RuleCardInfo.Names[selected].Replace(" ", "\n");
            heldLabel.color = Color.white;
        }
    }

    private void AnimateHeldCard(float dt, bool allowed)
    {
        if (heldRoot == null) return;
        useKick = Mathf.MoveTowards(useKick, 0f, dt * 4f);
        swapDip = Mathf.MoveTowards(swapDip, 0f, dt * 3.5f);
        // Lag behind mouse movement a little so the card feels held, not glued to the screen.
        if (!MouseLook.isPaused && Time.timeScale > 0f)
            sway = Vector2.Lerp(sway, new Vector2(-Input.GetAxis("Mouse X"), -Input.GetAxis("Mouse Y")) * .012f, dt * 10f);
        sway = Vector2.ClampMagnitude(sway, .03f);
        float bob = Mathf.Sin(Time.time * 2.2f) * .004f;
        heldRoot.localPosition = HeldRest + new Vector3(sway.x, bob - swapDip * .09f + sway.y, useKick * .05f);
        heldRoot.localRotation = Quaternion.Euler(-6f - useKick * 18f + sway.y * 300f, -16f + sway.x * 300f, 5f);
        if (heldMaterial != null && heldMaterial.HasProperty("_EmissionColor"))
        {
            Color c = RuleCardInfo.Colors[selected];
            float ready = 1f - CooldownFraction((RuleCardKind)selected);
            float glow = allowed ? Mathf.Lerp(.35f, 1.8f, ready) + useKick * 2f : .15f;
            if (heldModel) glow *= 2.6f;
            if (heldGlow != null) { heldGlow.color = c; heldGlow.intensity = .5f + .9f * glow / 2.6f; }
            heldMaterial.SetColor("_EmissionColor", c * glow);
        }
    }

    private void BuildTetherLine()
    {
        tetherLine = gameObject.AddComponent<LineRenderer>();
        tetherLine.positionCount = 2;
        tetherLine.startWidth = .05f;
        tetherLine.endWidth = .12f;
        tetherLine.useWorldSpace = true;
        tetherLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tetherLine.receiveShadows = false;
        if (cardTemplate != null)
        {
            lineMaterial = new Material(cardTemplate) { name = "KineticTether_Runtime" };
            Color c = RuleCardInfo.Colors[0];
            if (lineMaterial.HasProperty("_BaseColor")) lineMaterial.SetColor("_BaseColor", c);
            if (lineMaterial.HasProperty("_EmissionColor")) { lineMaterial.EnableKeyword("_EMISSION"); lineMaterial.SetColor("_EmissionColor", c * 3f); }
            tetherLine.sharedMaterial = lineMaterial;
        }
        tetherLine.enabled = false;
    }

    private static void DestroyCollider(GameObject go)
    {
        var c = go.GetComponent<Collider>();
        if (c != null) { c.enabled = false; Destroy(c); }
    }

    private void OnDestroy()
    {
        ClearAll();
        if (airPreview != null) Destroy(airPreview);
        if (tetherTrail != null) Destroy(tetherTrail.gameObject);
        if (heldMaterial != null) Destroy(heldMaterial);
        if (lineMaterial != null) Destroy(lineMaterial);
    }
}

/// <summary>Grows a freshly solidified Air platform from a sliver to full size.</summary>
public sealed class AirMaterialize : MonoBehaviour
{
    public Vector3 target = Vector3.one;
    private float t;
    private void Start() { transform.localScale = new Vector3(target.x * .2f, target.y * .2f, target.z * .2f); }
    private void Update()
    {
        t = Mathf.Min(1f, t + Time.deltaTime / .16f);
        float e = 1f - (1f - t) * (1f - t);
        float overshoot = 1f + .08f * Mathf.Sin(t * Mathf.PI);
        transform.localScale = Vector3.Scale(target, new Vector3(e * overshoot, Mathf.Max(.2f, e), e * overshoot));
        if (t >= 1f) { transform.localScale = target; Destroy(this); }
    }
}
