using UnityEngine;

public class MouseLook : MonoBehaviour
{
    [Header("Sensitivity (1 - 100)")]
    [Range(1, 500)]
    public int sensitivity = 50;

    [Header("Look Smoothing")]
    [Range(1f, 200f)]
    public float lookSmoothness = 10f;

    float mouseSensitivity;
    float xRotation;

    float smoothX;
    float smoothY;

    Transform playerBody;

    void Awake()
    {
        playerBody = transform.parent;
        UpdateSensitivity();
    }

    void OnEnable()
    {
        LockCursor();
    }

    void OnDisable()
    {
        UnlockCursor();
    }

    /// <summary>
    /// Set this to true when game is paused to stop mouse look and cursor locking
    /// </summary>
    public static bool isPaused = false;

    void Update()
    {
        // Don't process mouse look when paused
        if (isPaused)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }
        
        // Safety: re-lock if Unity unlocks it
        if (Cursor.lockState != CursorLockMode.Locked)
            LockCursor();

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        smoothX = Mathf.Lerp(smoothX, mouseX, lookSmoothness * Time.deltaTime);
        smoothY = Mathf.Lerp(smoothY, mouseY, lookSmoothness * Time.deltaTime);

        xRotation -= smoothY * Time.deltaTime;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        playerBody.Rotate(Vector3.up * smoothX * Time.deltaTime);
    }

    void OnValidate()
    {
        UpdateSensitivity();
    }

    void UpdateSensitivity()
    {
        float t = sensitivity / 100f;
        mouseSensitivity = Mathf.Lerp(80f, 600f, t * t);
    }

    void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
