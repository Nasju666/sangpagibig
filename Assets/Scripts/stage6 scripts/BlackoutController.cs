using UnityEngine;
using System.Collections;

/// <summary>
/// BLACKOUT CONTROLLER
/// Controls the flickering lights mechanic for Stage 6.
/// Lights turn on/off randomly, making navigation dangerous in darkness.
/// 
/// Features:
/// - Random blackout intervals
/// - Warning flicker before blackout
/// - Gradual difficulty increase (shorter light periods)
/// </summary>
public class BlackoutController : MonoBehaviour
{
    [Header("Light References")]
    [Tooltip("All lights in the stage that will flicker")]
    public Light[] stageLights;
    
    [Tooltip("Ambient light color when lights are ON")]
    public Color ambientLightColor = new Color(0.3f, 0.3f, 0.3f);
    
    [Tooltip("Ambient light color when lights are OFF (darkness)")]
    public Color blackoutColor = new Color(0.02f, 0.02f, 0.02f);
    
    [Header("Timing")]
    [Tooltip("Time lights stay ON")]
    public float lightOnDuration = 3f;
    
    [Tooltip("Time lights stay OFF (blackout)")]
    public float blackoutDuration = 2f;
    
    [Tooltip("Warning flicker duration before blackout")]
    public float warningDuration = 0.5f;
    
    [Header("Difficulty Scaling")]
    [Tooltip("Reduce light-on time each cycle")]
    public float lightOnReduction = 0.1f;
    
    [Tooltip("Minimum light-on duration")]
    public float minLightOnDuration = 1f;
    
    [Tooltip("Increase blackout time each cycle")]
    public float blackoutIncrease = 0.1f;
    
    [Tooltip("Maximum blackout duration")]
    public float maxBlackoutDuration = 4f;
    
    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip warningSound;
    public AudioClip blackoutSound;
    public AudioClip lightsOnSound;
    
    [Header("Visual Effects")]
    [Tooltip("Optional screen overlay for darkness effect")]
    public CanvasGroup darknessOverlay;
    
    // State
    private bool isBlackout = false;
    private float currentLightOnDuration;
    private float currentBlackoutDuration;
    private Coroutine blackoutCoroutine;
    
    public static bool IsBlackout { get; private set; }
    
    void Start()
    {
        currentLightOnDuration = lightOnDuration;
        currentBlackoutDuration = blackoutDuration;
        
        // Start with lights on
        SetLightsState(true);
        
        // Begin blackout cycle
        blackoutCoroutine = StartCoroutine(BlackoutCycle());
    }
    
    IEnumerator BlackoutCycle()
    {
        while (true)
        {
            // LIGHTS ON PHASE
            SetLightsState(true);
            yield return new WaitForSeconds(currentLightOnDuration);
            
            // WARNING PHASE (flicker)
            yield return StartCoroutine(WarningFlicker());
            
            // BLACKOUT PHASE
            SetLightsState(false);
            yield return new WaitForSeconds(currentBlackoutDuration);
            
            // Increase difficulty
            currentLightOnDuration = Mathf.Max(minLightOnDuration, currentLightOnDuration - lightOnReduction);
            currentBlackoutDuration = Mathf.Min(maxBlackoutDuration, currentBlackoutDuration + blackoutIncrease);
        }
    }
    
    IEnumerator WarningFlicker()
    {
        if (warningSound != null && audioSource != null)
            audioSource.PlayOneShot(warningSound);
        
        float elapsed = 0f;
        float flickerRate = 0.1f;
        bool flickerState = true;
        
        while (elapsed < warningDuration)
        {
            flickerState = !flickerState;
            
            // Quick flicker
            foreach (var light in stageLights)
            {
                if (light != null)
                    light.enabled = flickerState;
            }
            
            // Flash ambient too
            RenderSettings.ambientLight = flickerState ? ambientLightColor : blackoutColor;
            
            elapsed += flickerRate;
            yield return new WaitForSeconds(flickerRate);
        }
    }
    
    void SetLightsState(bool lightsOn)
    {
        isBlackout = !lightsOn;
        IsBlackout = isBlackout;
        
        // Toggle all lights
        foreach (var light in stageLights)
        {
            if (light != null)
                light.enabled = lightsOn;
        }
        
        // Set ambient lighting
        RenderSettings.ambientLight = lightsOn ? ambientLightColor : blackoutColor;
        
        // Darkness overlay (if using UI overlay)
        if (darknessOverlay != null)
        {
            darknessOverlay.alpha = lightsOn ? 0f : 0.9f;
        }
        
        // Audio
        if (audioSource != null)
        {
            if (lightsOn && lightsOnSound != null)
                audioSource.PlayOneShot(lightsOnSound);
            else if (!lightsOn && blackoutSound != null)
                audioSource.PlayOneShot(blackoutSound);
        }
    }
    
    /// <summary>
    /// Reset blackout cycle (call on respawn)
    /// </summary>
    public void ResetBlackout()
    {
        if (blackoutCoroutine != null)
            StopCoroutine(blackoutCoroutine);
        
        currentLightOnDuration = lightOnDuration;
        currentBlackoutDuration = blackoutDuration;
        
        SetLightsState(true);
        blackoutCoroutine = StartCoroutine(BlackoutCycle());
    }
    
    /// <summary>
    /// Pause blackout (for cutscenes, etc.)
    /// </summary>
    public void PauseBlackout()
    {
        if (blackoutCoroutine != null)
            StopCoroutine(blackoutCoroutine);
        
        SetLightsState(true);
    }
    
    /// <summary>
    /// Resume blackout cycle
    /// </summary>
    public void ResumeBlackout()
    {
        blackoutCoroutine = StartCoroutine(BlackoutCycle());
    }
}
