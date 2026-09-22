using UnityEngine;

/// <summary>
/// Makes a 3D object float and rotate in the menu scene.
/// Perfect for decorative elements or the game logo.
/// </summary>
public class FloatingObject : MonoBehaviour
{
    [Header("Float Settings")]
    [SerializeField] private float floatAmplitude = 0.3f;
    [SerializeField] private float floatSpeed = 1.5f;
    
    [Header("Rotation Settings")]
    [SerializeField] private bool enableRotation = true;
    [SerializeField] private Vector3 rotationSpeed = new Vector3(0f, 30f, 0f);
    
    [Header("Scale Pulse")]
    [SerializeField] private bool enableScalePulse = false;
    [SerializeField] private float pulseAmount = 0.1f;
    [SerializeField] private float pulseSpeed = 2f;
    
    private Vector3 startPosition;
    private Vector3 originalScale;
    private float timeOffset;
    
    private void Start()
    {
        startPosition = transform.position;
        originalScale = transform.localScale;
        
        // Add random offset so multiple objects don't sync
        timeOffset = Random.Range(0f, Mathf.PI * 2f);
    }
    
    private void Update()
    {
        // Float up and down
        float newY = startPosition.y + Mathf.Sin((Time.time + timeOffset) * floatSpeed) * floatAmplitude;
        transform.position = new Vector3(startPosition.x, newY, startPosition.z);
        
        // Rotate
        if (enableRotation)
        {
            transform.Rotate(rotationSpeed * Time.deltaTime);
        }
        
        // Scale pulse
        if (enableScalePulse)
        {
            float scaleMultiplier = 1f + Mathf.Sin((Time.time + timeOffset) * pulseSpeed) * pulseAmount;
            transform.localScale = originalScale * scaleMultiplier;
        }
    }
}
