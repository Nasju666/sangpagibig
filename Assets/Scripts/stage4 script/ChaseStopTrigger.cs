using UnityEngine;

/// <summary>
/// CHASE STOP TRIGGER
/// When the player enters this trigger zone, the monster stops chasing.
/// 
/// Setup:
/// 1. Create an empty GameObject where you want the chase to stop
/// 2. Add a BoxCollider (or any collider) and set "Is Trigger" = true
/// 3. Add this script
/// 4. Assign the ChaseCharacter reference
/// </summary>
public class ChaseStopTrigger : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The ChaseCharacter script on the monster")]
    public ChaseCharacter monster;
    
    [Header("Options")]
    [Tooltip("Should the monster be destroyed when player reaches safe zone?")]
    public bool destroyMonster = false;
    
    [Tooltip("Should the monster reset to start position?")]
    public bool resetMonster = false;
    
    [Tooltip("Delay before stopping chase (gives player time to fully enter)")]
    public float stopDelay = 0f;
    
    [Header("Audio (Optional)")]
    public AudioClip safeZoneSound;
    
    private bool triggered = false;
    
    private void OnTriggerEnter(Collider other)
    {
        // Only respond to player
        if (!other.CompareTag("Player")) return;
        if (triggered) return;
        
        triggered = true;
        
        if (stopDelay > 0)
            Invoke(nameof(StopChase), stopDelay);
        else
            StopChase();
    }
    
    private void StopChase()
    {
        if (monster == null)
        {
            // Try to find it if not assigned
            monster = FindObjectOfType<ChaseCharacter>();
        }
        
        if (monster != null)
        {
            if (destroyMonster)
            {
                Destroy(monster.gameObject);
            }
            else if (resetMonster)
            {
                monster.ResetMonster();
                monster.enabled = false; // Stop the chase script
            }
            else
            {
                // Just disable chasing
                monster.enabled = false;
            }
        }
        
        // Play safe zone sound
        if (safeZoneSound != null)
        {
            AudioSource.PlayClipAtPoint(safeZoneSound, transform.position);
        }
        
        Debug.Log("✅ Monster chase stopped! Player reached safe zone.");
    }
    
    /// <summary>
    /// Call this to reset the trigger (e.g., when player dies and respawns)
    /// </summary>
    public void ResetTrigger()
    {
        triggered = false;
        if (monster != null)
        {
            monster.enabled = true;
        }
    }
    
    private void OnDrawGizmos()
    {
        // Show the trigger zone in editor
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
