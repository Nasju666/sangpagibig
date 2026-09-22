using UnityEngine;

/// <summary>
/// STAGE 6 RESET MANAGER
/// Handles respawn and reset logic for the blackout stage.
/// </summary>
public class Stage6ResetManager : MonoBehaviour
{
    [Header("References")]
    public BlackoutController blackoutController;
    public GlitchEffect glitchEffect;
    
    [Header("Settings")]
    public bool resetBlackoutOnRespawn = true;
    public bool triggerGlitchOnDeath = true;
    
    void Start()
    {
        // Auto-find components if not assigned
        if (blackoutController == null)
            blackoutController = FindObjectOfType<BlackoutController>();
        
        if (glitchEffect == null)
            glitchEffect = FindObjectOfType<GlitchEffect>();
    }
    
    /// <summary>
    /// Called when player dies
    /// </summary>
    public void OnPlayerDeath()
    {
        if (triggerGlitchOnDeath && glitchEffect != null)
        {
            StartCoroutine(glitchEffect.HeavyGlitch(0.5f));
        }
    }
    
    /// <summary>
    /// Called when player respawns
    /// </summary>
    public void OnPlayerRespawn()
    {
        if (resetBlackoutOnRespawn && blackoutController != null)
        {
            blackoutController.ResetBlackout();
        }
    }
    
    /// <summary>
    /// Full stage reset
    /// </summary>
    public void ResetStage()
    {
        if (blackoutController != null)
            blackoutController.ResetBlackout();
        
        Debug.Log("✅ Stage 6 Reset Complete");
    }
}
