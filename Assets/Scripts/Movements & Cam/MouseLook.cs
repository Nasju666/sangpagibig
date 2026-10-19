using UnityEngine;

/// <summary>
/// First-person look. Frame-rate independent (mouse deltas are already per-frame, so no deltaTime),
/// reads sensitivity / invert / smoothing / FOV-independent settings from RuleCardsSettings, and adds a
/// small strafe tilt. The camera's position is owned by HeadBob; this script only owns rotation.
/// </summary>
public class MouseLook : MonoBehaviour
{
    [Header("Legacy (kept for scene compatibility; RuleCardsSettings drives the real values)")]
    [Range(1, 500)] public int sensitivity = 50;
    [Range(1f, 200f)] public float lookSmoothness = 10f;

    [Header("Feel")]
    public float maxPitch = 88f;
    public float strafeTilt = 1.6f;

    /// <summary>Set this to true when game is paused to stop mouse look and cursor locking.</summary>
    public static bool isPaused = false;

    Transform playerBody;
    float targetPitch, pitch, targetYawDelta, roll;

    void Awake()
    {
        playerBody = transform.parent;
        float x = transform.localEulerAngles.x;
        pitch = targetPitch = x > 180f ? x - 360f : x;
    }

    void OnEnable() { LockCursor(); }
    void OnDisable() { UnlockCursor(); }

    void Update()
    {
        if (isPaused)
        {
            // Cutscenes/dialogue hold the player but keep the cursor captured.
            bool story = RuleCardsStory.Blocking;
            Cursor.lockState = story ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !story;
            targetYawDelta = 0f;
            return;
        }
        if (Cursor.lockState != CursorLockMode.Locked) LockCursor();

        float deg = RuleCardsSettings.LookDegreesPerUnit;
        float dx = Input.GetAxisRaw("Mouse X") * deg;
        float dy = Input.GetAxisRaw("Mouse Y") * deg * (RuleCardsSettings.InvertY ? -1f : 1f);

        targetPitch = Mathf.Clamp(targetPitch - dy, -maxPitch, maxPitch);
        targetYawDelta += dx;

        // Smoothing as a time constant: 0 = raw, 1 = ~60 ms of easing. Same feel at any frame rate.
        float smooth = RuleCardsSettings.LookSmoothing;
        float k = smooth <= .01f ? 1f : 1f - Mathf.Exp(-Time.unscaledDeltaTime / (smooth * .06f));
        float yawStep = targetYawDelta * k;
        targetYawDelta -= yawStep;
        pitch = Mathf.Lerp(pitch, targetPitch, k);
        if (playerBody != null) playerBody.Rotate(Vector3.up * yawStep, Space.Self);

        float strafe = Input.GetAxisRaw("Horizontal");
        roll = Mathf.Lerp(roll, -strafe * strafeTilt * RuleCardsSettings.CameraMotion, 1f - Mathf.Exp(-8f * Time.deltaTime));
        transform.localRotation = Quaternion.Euler(pitch, 0f, roll);
    }

    void LockCursor() { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
    void UnlockCursor() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
}
