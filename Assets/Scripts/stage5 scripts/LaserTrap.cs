using UnityEngine;
using System.Collections;

/// <summary>
/// LASER TRAP - When player enters trigger zone, laser shoots towards them!
/// The laser animates from the emitter towards the player, then kills them.
/// 
/// Setup:
/// 1. Create empty GameObject, add this script
/// 2. Add a child cube for the laser beam visual
/// 3. Add a BoxCollider (Is Trigger) for the detection zone
/// </summary>
public class LaserTrap : MonoBehaviour
{
    [Header("Laser Visual")]
    [Tooltip("The laser beam object (a stretched cube)")]
    public Transform laserBeam;
    
    [Tooltip("Where the laser shoots FROM")]
    public Transform laserOrigin;
    
    [Header("Laser Settings")]
    [Tooltip("How fast the laser shoots towards player")]
    public float laserSpeed = 20f;
    
    [Tooltip("Delay before laser fires after player enters")]
    public float fireDelay = 0.3f;
    
    [Tooltip("How long laser stays visible after hitting")]
    public float laserDuration = 0.5f;
    
    [Header("Visuals")]
    public Color laserColor = Color.red;
    public float laserWidth = 0.3f;
    
    [Header("Audio (Optional)")]
    public AudioSource audioSource;
    public AudioClip chargeSound;
    public AudioClip fireSound;
    
    // Private
    private bool isFiring = false;
    private Vector3 originalScale;
    private Renderer laserRenderer;
    private BoxCollider laserCollider;
    
    void Start()
    {
        // Hide laser at start
        if (laserBeam != null)
        {
            originalScale = laserBeam.localScale;
            laserBeam.localScale = new Vector3(laserWidth, laserWidth, 0); // Length 0
            laserRenderer = laserBeam.GetComponent<Renderer>();
            laserCollider = laserBeam.GetComponent<BoxCollider>();
            
            // Set color
            if (laserRenderer != null)
            {
                laserRenderer.material.color = laserColor;
                // Make it glow
                laserRenderer.material.EnableKeyword("_EMISSION");
                laserRenderer.material.SetColor("_EmissionColor", laserColor * 2f);
            }
            
            // Disable collider until firing
            if (laserCollider != null)
                laserCollider.enabled = false;
                
            laserBeam.gameObject.SetActive(false);
        }
    }
    
    // Player enters the danger zone
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (isFiring) return;
        
        // Fire laser at player!
        StartCoroutine(FireLaserAtPlayer(other.transform));
    }
    
    IEnumerator FireLaserAtPlayer(Transform player)
    {
        isFiring = true;
        
        // Calculate direction to player
        Vector3 origin = laserOrigin != null ? laserOrigin.position : transform.position;
        Vector3 direction = (player.position - origin).normalized;
        float distance = Vector3.Distance(origin, player.position);
        
        // Play charge sound
        if (audioSource != null && chargeSound != null)
            audioSource.PlayOneShot(chargeSound);
        
        // Warning flash
        if (laserBeam != null)
        {
            laserBeam.gameObject.SetActive(true);
            // Quick warning flashes
            for (int i = 0; i < 3; i++)
            {
                laserBeam.gameObject.SetActive(true);
                yield return new WaitForSeconds(0.05f);
                laserBeam.gameObject.SetActive(false);
                yield return new WaitForSeconds(0.05f);
            }
        }
        
        // Wait before firing
        yield return new WaitForSeconds(fireDelay);
        
        // Play fire sound
        if (audioSource != null && fireSound != null)
            audioSource.PlayOneShot(fireSound);
        
        // FIRE! Animate laser extending towards player
        if (laserBeam != null)
        {
            laserBeam.gameObject.SetActive(true);
            laserBeam.position = origin;
            laserBeam.rotation = Quaternion.LookRotation(direction);
            
            // Enable collider
            if (laserCollider != null)
                laserCollider.enabled = true;
            
            // Animate laser extending
            float currentLength = 0f;
            float targetLength = distance + 2f; // Overshoot a bit
            
            while (currentLength < targetLength)
            {
                currentLength += laserSpeed * Time.deltaTime;
                
                // Scale laser to current length
                laserBeam.localScale = new Vector3(laserWidth, laserWidth, currentLength);
                
                // Move laser so it extends from origin
                laserBeam.position = origin + direction * (currentLength / 2f);
                
                yield return null;
            }
            
            // Keep laser visible briefly
            yield return new WaitForSeconds(laserDuration);
            
            // Hide laser
            laserBeam.gameObject.SetActive(false);
            if (laserCollider != null)
                laserCollider.enabled = false;
        }
        
        isFiring = false;
    }
    
    // Called when laser hits player
    public void OnLaserHitPlayer(GameObject player)
    {
        if (RespawnManager.Instance != null)
        {
            Debug.Log("⚡ Laser hit player! Respawning...");
            RespawnManager.Instance.Respawn(player);
        }
    }
    
    // Reset trap (call on respawn)
    public void ResetTrap()
    {
        StopAllCoroutines();
        isFiring = false;
        
        if (laserBeam != null)
        {
            laserBeam.gameObject.SetActive(false);
            laserBeam.localScale = new Vector3(laserWidth, laserWidth, 0);
        }
    }
}

/// <summary>
/// Attach this to the laser beam object to detect player hits
/// </summary>
public class LaserBeamHit : MonoBehaviour
{
    public LaserTrap trap;
    
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        
        // Direct respawn
        if (RespawnManager.Instance != null)
        {
            Debug.Log("⚡ Laser hit player!");
            RespawnManager.Instance.Respawn(other.gameObject);
        }
    }
}
