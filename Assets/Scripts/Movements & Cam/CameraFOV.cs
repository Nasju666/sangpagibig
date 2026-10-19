using UnityEngine;

/// <summary>Field of view from Settings, widening a little while sprinting.</summary>
public class CameraFOV : MonoBehaviour
{
    [Tooltip("Legacy; Settings ▸ Camera ▸ Field of view is used instead.")] public float normalFOV = 60f;
    [Tooltip("Degrees added while sprinting.")] public float sprintBoost = 8f;
    [HideInInspector] public float sprintFOV = 75f;
    public float fovSpeed = 8f;

    Camera cam;
    PlayerMovement player;

    void Start()
    {
        cam = GetComponent<Camera>();
        player = GetComponentInParent<PlayerMovement>();
        if (cam != null) cam.fieldOfView = RuleCardsSettings.FieldOfView;
    }

    void Update()
    {
        if (cam == null) return;
        float target = RuleCardsSettings.FieldOfView + (player != null && player.IsSprinting ? sprintBoost : 0f);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, target, 1f - Mathf.Exp(-fovSpeed * Time.deltaTime));
    }
}
