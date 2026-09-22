using UnityEngine;

/// <summary>
/// Rotates the camera slowly around a point to create a dynamic 3D menu background.
/// Attach this to a camera or an empty GameObject that the camera is parented to.
/// </summary>
public class MenuCameraRotator : MonoBehaviour
{
    [Header("Rotation Settings")]
    [Tooltip("Speed of rotation in degrees per second")]
    [SerializeField] private float rotationSpeed = 5f;
    
    [Tooltip("Axis to rotate around")]
    [SerializeField] private Vector3 rotationAxis = Vector3.up;
    
    [Header("Bobbing Effect")]
    [Tooltip("Enable subtle up/down bobbing motion")]
    [SerializeField] private bool enableBobbing = true;
    
    [Tooltip("How much to bob up and down")]
    [SerializeField] private float bobAmount = 0.5f;
    
    [Tooltip("Speed of the bobbing motion")]
    [SerializeField] private float bobSpeed = 1f;
    
    private Vector3 startPosition;
    private float bobTimer;
    
    private void Start()
    {
        startPosition = transform.position;
    }
    
    private void Update()
    {
        // Rotate around the specified axis
        transform.Rotate(rotationAxis * rotationSpeed * Time.deltaTime);
        
        // Apply bobbing effect
        if (enableBobbing)
        {
            bobTimer += Time.deltaTime * bobSpeed;
            float newY = startPosition.y + Mathf.Sin(bobTimer) * bobAmount;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }
    }
}
