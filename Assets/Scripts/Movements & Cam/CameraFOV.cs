using UnityEngine;

public class CameraFOV : MonoBehaviour
{
    public float normalFOV = 60f;
    public float sprintFOV = 75f;
    public float fovSpeed = 8f;

    Camera cam;
    PlayerMovement player;

    void Start()
    {
        cam = GetComponent<Camera>();
        player = GetComponentInParent<PlayerMovement>();
    }

    void Update()
    {
        float targetFOV = player.IsSprinting ? sprintFOV : normalFOV;
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, fovSpeed * Time.deltaTime);
    }
}
