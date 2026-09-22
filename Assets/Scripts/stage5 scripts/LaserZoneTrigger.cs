using UnityEngine;

/// <summary>
/// Activates laser grid when player enters the trigger zone.
/// Place this on an invisible trigger collider at the entrance of the laser area.
/// </summary>
public class LaserZoneTrigger : MonoBehaviour
{
    [Header("Lasers to Activate")]
    [Tooltip("Laser grids to activate when player enters")]
    public LaserGrid[] laserGrids;
    
    [Tooltip("Individual lasers to activate")]
    public LaserBeam[] individualLasers;
    
    [Tooltip("Moving lasers to activate")]
    public MovingLaser[] movingLasers;
    
    [Header("Settings")]
    [Tooltip("Delay before lasers activate after trigger")]
    public float activationDelay = 0.5f;
    
    [Tooltip("Deactivate lasers when player leaves the zone")]
    public bool deactivateOnExit = false;
    
    [Tooltip("Only trigger once (won't reset)")]
    public bool triggerOnce = false;
    
    [Header("Audio/Visual Feedback")]
    public AudioSource warningSound;
    public GameObject warningLight; // Optional flashing light
    
    private bool hasTriggered = false;
    private bool isActive = false;
    
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (triggerOnce && hasTriggered) return;
        
        hasTriggered = true;
        StartCoroutine(ActivateLasers());
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (!deactivateOnExit) return;
        
        DeactivateLasers();
    }
    
    System.Collections.IEnumerator ActivateLasers()
    {
        // Warning feedback
        if (warningSound != null)
            warningSound.Play();
        
        if (warningLight != null)
            warningLight.SetActive(true);
        
        // Wait for delay
        yield return new WaitForSeconds(activationDelay);
        
        // Activate all laser grids
        foreach (var grid in laserGrids)
        {
            if (grid != null)
                grid.enabled = true;
        }
        
        // Activate individual lasers
        foreach (var laser in individualLasers)
        {
            if (laser != null)
                laser.SetActive(true);
        }
        
        // Activate moving lasers
        foreach (var movingLaser in movingLasers)
        {
            if (movingLaser != null)
                movingLaser.enabled = true;
        }
        
        isActive = true;
        
        if (warningLight != null)
            warningLight.SetActive(false);
    }
    
    void DeactivateLasers()
    {
        StopAllCoroutines();
        
        foreach (var grid in laserGrids)
        {
            if (grid != null)
                grid.enabled = false;
        }
        
        foreach (var laser in individualLasers)
        {
            if (laser != null)
                laser.SetActive(false);
        }
        
        foreach (var movingLaser in movingLasers)
        {
            if (movingLaser != null)
                movingLaser.enabled = false;
        }
        
        isActive = false;
    }
    
    // Call this on respawn
    public void ResetTrigger()
    {
        hasTriggered = false;
        DeactivateLasers();
    }
}
