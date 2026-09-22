using UnityEngine;

/// <summary>
/// HIDDEN OBSTACLE
/// Obstacles that are only visible when lights are ON.
/// Becomes invisible (but still deadly!) during blackout.
/// 
/// Perfect for pits, spikes, or moving hazards.
/// </summary>
public class HiddenObstacle : MonoBehaviour
{
    [Header("Components")]
    [Tooltip("Renderers to hide during blackout")]
    public Renderer[] renderers;
    
    [Tooltip("Optional: Particles that show in light")]
    public ParticleSystem[] warningParticles;
    
    [Header("Settings")]
    [Tooltip("Is the collider still active during blackout?")]
    public bool deadlyInDarkness = true;
    
    [Tooltip("Show a faint glow even in darkness (mercy mode)")]
    public bool showFaintGlow = false;
    public float glowIntensity = 0.1f;
    
    [Header("Audio")]
    public AudioSource ambientSound;
    
    private Collider obstacleCollider;
    private Material[] originalMaterials;
    private Color[] originalColors;
    
    void Start()
    {
        obstacleCollider = GetComponent<Collider>();
        
        // Store original materials
        if (renderers.Length > 0)
        {
            originalMaterials = new Material[renderers.Length];
            originalColors = new Color[renderers.Length];
            
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    originalMaterials[i] = renderers[i].material;
                    originalColors[i] = renderers[i].material.color;
                }
            }
        }
    }
    
    void Update()
    {
        // Check blackout state
        bool isBlackout = BlackoutController.IsBlackout;
        
        // Update visibility
        UpdateVisibility(!isBlackout);
    }
    
    void UpdateVisibility(bool visible)
    {
        // Renderers
        foreach (var rend in renderers)
        {
            if (rend != null)
            {
                if (visible)
                {
                    rend.enabled = true;
                }
                else if (showFaintGlow)
                {
                    rend.enabled = true;
                    Color dim = rend.material.color;
                    dim.a = glowIntensity;
                    rend.material.color = dim;
                }
                else
                {
                    rend.enabled = false;
                }
            }
        }
        
        // Particles
        foreach (var ps in warningParticles)
        {
            if (ps != null)
            {
                if (visible && !ps.isPlaying)
                    ps.Play();
                else if (!visible && ps.isPlaying)
                    ps.Stop();
            }
        }
        
        // Collider (if not deadly in darkness)
        if (!deadlyInDarkness && obstacleCollider != null)
        {
            obstacleCollider.enabled = visible;
        }
        
        // Ambient sound
        if (ambientSound != null)
        {
            ambientSound.volume = visible ? 1f : 0.2f;
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        
        // Kill player
        PlayerDeath death = other.GetComponent<PlayerDeath>();
        if (death != null)
        {
            death.Die();
            return;
        }
        
        // Fallback respawn
        if (RespawnManager.Instance != null)
        {
            RespawnManager.Instance.Respawn(other.gameObject);
        }
    }
}
