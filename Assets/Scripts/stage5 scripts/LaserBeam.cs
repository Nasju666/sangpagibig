using UnityEngine;

/// <summary>
/// Individual laser beam that can be toggled on/off.
/// Uses a LineRenderer for visual and a trigger collider for damage.
/// </summary>
public class LaserBeam : MonoBehaviour
{
    [Header("Laser Components")]
    [Tooltip("LineRenderer for laser visual")]
    public LineRenderer laserLine;
    
    [Tooltip("Collider for laser damage (BoxCollider recommended)")]
    public Collider laserCollider;
    
    [Header("Laser Appearance")]
    public Color laserColor = Color.red;
    public float laserWidth = 0.2f;
    
    [Header("Visual Effects")]
    [Tooltip("Optional particle effect at laser origin")]
    public ParticleSystem originParticles;
    
    [Tooltip("Optional particle effect at laser end")]
    public ParticleSystem endParticles;
    
    [Header("Audio")]
    public AudioSource laserSound;
    
    private bool isActive = true;
    private Material laserMaterial;
    
    void Start()
    {
        // Setup laser visual
        if (laserLine != null)
        {
            laserLine.startWidth = laserWidth;
            laserLine.endWidth = laserWidth;
            laserMaterial = laserLine.material;
            UpdateLaserColor();
        }
        
        UpdateLaserState();
    }
    
    void UpdateLaserColor()
    {
        if (laserMaterial != null)
        {
            laserMaterial.SetColor("_Color", laserColor);
            laserMaterial.SetColor("_EmissionColor", laserColor * 2f);
        }
    }
    
    public void SetActive(bool active)
    {
        isActive = active;
        UpdateLaserState();
    }
    
    public void Toggle()
    {
        isActive = !isActive;
        UpdateLaserState();
    }
    
    void UpdateLaserState()
    {
        if (laserLine != null)
            laserLine.enabled = isActive;
        
        if (laserCollider != null)
            laserCollider.enabled = isActive;
        
        // Particles
        if (originParticles != null)
        {
            if (isActive) originParticles.Play();
            else originParticles.Stop();
        }
        
        if (endParticles != null)
        {
            if (isActive) endParticles.Play();
            else endParticles.Stop();
        }
        
        // Audio
        if (laserSound != null)
        {
            if (isActive && !laserSound.isPlaying)
                laserSound.Play();
            else if (!isActive && laserSound.isPlaying)
                laserSound.Stop();
        }
    }
    
    public bool IsActive()
    {
        return isActive;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!isActive) return;
        if (!other.CompareTag("Player")) return;
        
        // Kill player and respawn at checkpoint/teleporter
        if (RespawnManager.Instance != null)
        {
            Debug.Log("⚡ Player hit by laser! Respawning...");
            RespawnManager.Instance.Respawn(other.gameObject);
        }
    }
}
