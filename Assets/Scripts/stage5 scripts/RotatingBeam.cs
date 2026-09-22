using UnityEngine;

/// <summary>
/// Rotating beam obstacle that spins continuously.
/// Player must time their movement to avoid getting hit.
/// Attach to a parent object containing the beam collider.
/// </summary>
public class RotatingBeam : MonoBehaviour
{
    [Header("Rotation Settings")]
    [Tooltip("Rotation speed in degrees per second")]
    public float rotationSpeed = 90f;
    
    [Tooltip("Axis to rotate around (default: Y axis for horizontal spin)")]
    public Vector3 rotationAxis = Vector3.up;
    
    [Header("Difficulty Variations")]
    [Tooltip("If true, alternates rotation direction")]
    public bool alternateDirection = false;
    
    [Tooltip("Time before switching direction (if alternateDirection is true)")]
    public float directionSwitchInterval = 3f;
    
    [Header("Visual Feedback")]
    [Tooltip("Optional: Assign a material that glows when spinning fast")]
    public Renderer beamRenderer;
    public Color normalColor = Color.gray;
    public Color dangerColor = Color.red;
    
    private float currentDirection = 1f;
    private float switchTimer = 0f;
    
    void Update()
    {
        // Handle direction switching
        if (alternateDirection)
        {
            switchTimer += Time.deltaTime;
            if (switchTimer >= directionSwitchInterval)
            {
                currentDirection *= -1f;
                switchTimer = 0f;
            }
        }
        
        // Rotate the beam
        transform.Rotate(rotationAxis, rotationSpeed * currentDirection * Time.deltaTime);
        
        // Update visual feedback
        if (beamRenderer != null)
        {
            float speedRatio = Mathf.Abs(rotationSpeed) / 180f; // Normalize for glow
            beamRenderer.material.color = Color.Lerp(normalColor, dangerColor, speedRatio);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        
        // Try PlayerDeath first (if player has it)
        PlayerDeath playerDeath = other.GetComponent<PlayerDeath>();
        if (playerDeath != null)
        {
            playerDeath.Die();
            return;
        }
        
        // Fallback to RespawnManager
        if (RespawnManager.Instance != null)
        {
            RespawnManager.Instance.Respawn(other.gameObject);
        }
    }
    
    // For stacked/layered rotating beams
    public void SetRotationSpeed(float speed)
    {
        rotationSpeed = speed;
    }
    
    public void SetRotationDirection(float direction)
    {
        currentDirection = Mathf.Sign(direction);
    }
}
