using UnityEngine;
using System.Collections;

/// <summary>
/// Controls a grid of lasers with various patterns.
/// Lasers toggle on/off in patterns the player must memorize/react to.
/// </summary>
public class LaserGrid : MonoBehaviour
{
    [Header("Laser References")]
    [Tooltip("All laser beams in this grid")]
    public LaserBeam[] lasers;
    
    [Header("Pattern Settings")]
    public LaserPattern pattern = LaserPattern.Alternating;
    
    [Tooltip("Time between pattern changes")]
    public float patternInterval = 1.5f;
    
    [Tooltip("Warning time before lasers activate (visual flicker)")]
    public float warningTime = 0.5f;
    
    [Header("Difficulty")]
    [Tooltip("Minimum interval (speeds up over time if progressive)")]
    public float minInterval = 0.5f;
    
    [Tooltip("Speed up rate per cycle")]
    public float speedUpRate = 0.95f;
    
    [Header("Audio")]
    public AudioSource warningSound;
    public AudioSource activateSound;
    
    public enum LaserPattern
    {
        Alternating,    // Every other laser toggles
        Wave,           // Lasers activate in sequence
        Random,         // Random lasers toggle
        AllOnOff,       // All lasers toggle together
        Chase           // One safe spot moves through grid
    }
    
    private int currentPatternState = 0;
    private float currentInterval;
    private Coroutine patternCoroutine;
    
    void Start()
    {
        currentInterval = patternInterval;
        patternCoroutine = StartCoroutine(RunPattern());
    }
    
    IEnumerator RunPattern()
    {
        while (true)
        {
            // Warning phase
            if (warningSound != null)
                warningSound.Play();
            
            yield return StartCoroutine(WarningFlicker());
            
            // Activate pattern
            if (activateSound != null)
                activateSound.Play();
            
            ApplyPattern();
            
            // Wait for interval
            yield return new WaitForSeconds(currentInterval);
            
            // Speed up
            currentInterval = Mathf.Max(minInterval, currentInterval * speedUpRate);
            
            // Advance pattern state
            currentPatternState++;
        }
    }
    
    IEnumerator WarningFlicker()
    {
        float elapsed = 0f;
        float flickerRate = 0.1f;
        
        while (elapsed < warningTime)
        {
            // Flicker lasers that are about to change
            for (int i = 0; i < lasers.Length; i++)
            {
                if (WillLaserChange(i))
                {
                    lasers[i].Toggle();
                }
            }
            
            yield return new WaitForSeconds(flickerRate);
            elapsed += flickerRate;
        }
    }
    
    bool WillLaserChange(int index)
    {
        // Predict if this laser will change state
        switch (pattern)
        {
            case LaserPattern.Alternating:
                return true;
            case LaserPattern.Wave:
                return index == (currentPatternState + 1) % lasers.Length;
            case LaserPattern.AllOnOff:
                return true;
            default:
                return true;
        }
    }
    
    void ApplyPattern()
    {
        switch (pattern)
        {
            case LaserPattern.Alternating:
                ApplyAlternatingPattern();
                break;
            case LaserPattern.Wave:
                ApplyWavePattern();
                break;
            case LaserPattern.Random:
                ApplyRandomPattern();
                break;
            case LaserPattern.AllOnOff:
                ApplyAllOnOffPattern();
                break;
            case LaserPattern.Chase:
                ApplyChasePattern();
                break;
        }
    }
    
    void ApplyAlternatingPattern()
    {
        for (int i = 0; i < lasers.Length; i++)
        {
            bool shouldBeOn = (i + currentPatternState) % 2 == 0;
            lasers[i].SetActive(shouldBeOn);
        }
    }
    
    void ApplyWavePattern()
    {
        for (int i = 0; i < lasers.Length; i++)
        {
            // Only one laser off at a time (safe spot)
            bool shouldBeOn = i != currentPatternState % lasers.Length;
            lasers[i].SetActive(shouldBeOn);
        }
    }
    
    void ApplyRandomPattern()
    {
        for (int i = 0; i < lasers.Length; i++)
        {
            lasers[i].SetActive(Random.value > 0.5f);
        }
    }
    
    void ApplyAllOnOffPattern()
    {
        bool allOn = currentPatternState % 2 == 0;
        for (int i = 0; i < lasers.Length; i++)
        {
            lasers[i].SetActive(allOn);
        }
    }
    
    void ApplyChasePattern()
    {
        // Safe spot moves through the grid
        int safeSpot = currentPatternState % lasers.Length;
        for (int i = 0; i < lasers.Length; i++)
        {
            lasers[i].SetActive(i != safeSpot);
        }
    }
    
    // Reset on respawn
    public void ResetGrid()
    {
        if (patternCoroutine != null)
            StopCoroutine(patternCoroutine);
        
        currentInterval = patternInterval;
        currentPatternState = 0;
        
        // Turn all lasers off briefly
        foreach (var laser in lasers)
        {
            laser.SetActive(false);
        }
        
        patternCoroutine = StartCoroutine(RunPattern());
    }
    
    public void SetPattern(LaserPattern newPattern)
    {
        pattern = newPattern;
        currentPatternState = 0;
    }
}
