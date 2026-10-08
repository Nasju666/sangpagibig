using UnityEngine;

// A parked ceiling core opens the transit gate. Crossing secures the gate so
// the same core can be recovered and carried onward without trapping the player.
public class ArchiveTransitSeal : MonoBehaviour
{
    public ArchiveStageMechanics stage;
    public Collider ceilingPlate;
    public GameObject gate;
    public Vector3 platePosition;
    public float crossingZ=19f;
    public bool IsLatched { get; private set; }
    public bool IsOccupied { get; private set; }
    private Bounds passageBounds;
    private bool passageCached, hasPreviousPosition, sawEntrySide, wasPowered;
    private float previousPlayerZ, securedMessageUntil;
    private GUIStyle statusStyle;
    float contactTime;
    void Update(){Evaluate(Time.deltaTime);}
public void Evaluate(float dt)
    {
        if (stage == null || stage.core == null || stage.core.Body == null || stage.player == null) return;
        CachePassage();
        var core = stage.core;
        Vector3 delta = core.Body.position - platePosition;
        RaycastHit hit;
        bool touching = Physics.Raycast(core.Body.position, Vector3.up, out hit, 1.2f, ~0,
            QueryTriggerInteraction.Ignore) && hit.collider == ceilingPlate;
        bool stable = !core.Held && core.GravityUp && Mathf.Abs(delta.x) < .85f
            && Mathf.Abs(delta.z) < .85f && touching && core.Body.velocity.sqrMagnitude < 2.25f;
        contactTime = stable ? contactTime + dt : 0f;
        IsOccupied = contactTime >= 1f;
        wasPowered |= IsOccupied;

        Bounds playerBounds = stage.player.bounds;
        float currentZ = playerBounds.center.z;
        float exitZ = passageCached ? passageBounds.max.z + playerBounds.extents.z + .02f : crossingZ;
        float entryZ = passageCached ? passageBounds.min.z - playerBounds.extents.z : crossingZ - .5f;
        if (currentZ <= entryZ) sawEntrySide = true;
        bool crossedExit = hasPreviousPosition && previousPlayerZ < exitZ && currentZ >= exitZ;
        bool withinPassage = !passageCached || (playerBounds.min.x >= passageBounds.min.x - .05f
            && playerBounds.max.x <= passageBounds.max.x + .05f
            && playerBounds.max.y >= passageBounds.min.y && playerBounds.min.y <= passageBounds.max.y);
        var door = gate != null ? gate.GetComponent<ArchiveSlidingDoor>() : null;

        // Crossing the physical far edge is sufficient; the animation need not be 100% finished.
        // Remember prior switch power, so retrieving the core on this same frame cannot erase the crossing.
        if (!IsLatched && wasPowered && sawEntrySide && crossedExit && withinPassage
            && IsPassageClear(door, playerBounds))
        {
            IsLatched = true;
            securedMessageUntil = Time.unscaledTime + 6f;
        }
        previousPlayerZ = currentZ;
        hasPreviousPosition = true;
        if (door != null) { gate.SetActive(true); door.SetOpen(IsOccupied || IsLatched); }
        else if (gate != null) gate.SetActive(!IsOccupied && !IsLatched);
    }

    private void CachePassage()
    {
        if (passageCached || gate == null) return;
        foreach (var shape in gate.GetComponentsInChildren<Collider>(true))
        {
            if (shape.isTrigger || !shape.enabled) continue;
            if (!passageCached) { passageBounds = shape.bounds; passageCached = true; }
            else passageBounds.Encapsulate(shape.bounds);
        }
    }

    private bool IsPassageClear(ArchiveSlidingDoor door, Bounds playerBounds)
    {
        if (gate == null) return true;
        if (door == null) return !gate.activeInHierarchy || IsOccupied;
        foreach (var shape in gate.GetComponentsInChildren<Collider>())
        {
            if (shape.isTrigger || !shape.enabled) continue;
            // The upward-moving leaf must clear the player's body, not finish its whole animation.
            if (shape.bounds.min.y + stage.player.skinWidth < playerBounds.max.y) return false;
        }
        return door.Progress > 0f;
    }

    public string GateStatus
    {
        get
        {
            if (IsLatched) return "GATE SECURED — RECOVER CORE";
            if (IsOccupied) return "CROSS THE OPEN PASSAGE TO SECURE THE GATE";
            if (wasPowered) return "GATE NOT SECURED — RETURN CORE TO CEILING SWITCH";
            return "PARK THE CORE ON THE CEILING SWITCH";
        }
    }

    void OnGUI()
    {
        if (stage == null || stage.player == null || MouseLook.isPaused) return;
        float gateZ = passageCached ? passageBounds.center.z : crossingZ;
        if (Mathf.Abs(stage.player.transform.position.z - gateZ) > 10f
            && (!IsLatched || Time.unscaledTime > securedMessageUntil)) return;
        if (statusStyle == null) statusStyle = new GUIStyle(GUI.skin.box);
        statusStyle.alignment = TextAnchor.MiddleCenter;
        statusStyle.wordWrap = true;
        statusStyle.fontSize = 17;
        statusStyle.normal.textColor = IsLatched ? new Color(.2f, 1f, .5f) : new Color(1f, .8f, .35f);
        float width, height;
        Matrix4x4 previous = ResponsiveIMGUI.Begin(out width, out height);
        float panelWidth = Mathf.Min(520f, Mathf.Max(1f, width - 24f));
        GUI.Box(new Rect((width - panelWidth) * .5f, Mathf.Min(86f, height * .2f), panelWidth, 72f),
            "TRANSIT GATE\n" + GateStatus, statusStyle);
        ResponsiveIMGUI.End(previous);
    }
public void ResetSeal()
    {
        IsLatched = false;
        IsOccupied = false;
        contactTime = 0f;
        wasPowered = false;
        sawEntrySide = false;
        hasPreviousPosition = false;
        passageCached = false;
        securedMessageUntil = 0f;
        if (gate != null)
        {
            gate.SetActive(true);
            var door = gate.GetComponent<ArchiveSlidingDoor>();
            if (door != null) door.SnapClosed();
        }
    }
}