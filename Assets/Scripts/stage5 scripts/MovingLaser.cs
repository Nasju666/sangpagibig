using UnityEngine;

/// <summary>
/// Laser that moves back and forth across an area.
/// Player must time their crossing when laser passes by.
/// </summary>
public class MovingLaser : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Start position (local space)")]
    public Vector3 startOffset = Vector3.zero;
    
    [Tooltip("End position (local space)")]
    public Vector3 endOffset = new Vector3(10f, 0f, 0f);
    
    [Tooltip("Movement speed")]
    public float moveSpeed = 3f;
    
    [Header("Movement Pattern")]
    public MovementType movementType = MovementType.PingPong;
    
    [Tooltip("Pause duration at each end (for PingPong with pause)")]
    public float pauseDuration = 0f;
    
    public enum MovementType
    {
        PingPong,       // Smoothly back and forth
        Teleport,       // Snap back to start when reaching end
        Delayed         // Pause at each end
    }
    
    [Header("Laser Settings")]
    public LaserBeam laser;
    
    private Vector3 startPos;
    private Vector3 endPos;
    private bool movingToEnd = true;
    private float pauseTimer = 0f;
    
    void Start()
    {
        startPos = transform.position + startOffset;
        endPos = transform.position + endOffset;
        transform.position = startPos;
    }
    
    void Update()
    {
        // Handle pause
        if (pauseTimer > 0f)
        {
            pauseTimer -= Time.deltaTime;
            return;
        }
        
        Vector3 target = movingToEnd ? endPos : startPos;
        
        switch (movementType)
        {
            case MovementType.PingPong:
            case MovementType.Delayed:
                transform.position = Vector3.MoveTowards(
                    transform.position, 
                    target, 
                    moveSpeed * Time.deltaTime
                );
                
                if (Vector3.Distance(transform.position, target) < 0.01f)
                {
                    movingToEnd = !movingToEnd;
                    if (movementType == MovementType.Delayed)
                    {
                        pauseTimer = pauseDuration;
                    }
                }
                break;
                
            case MovementType.Teleport:
                transform.position = Vector3.MoveTowards(
                    transform.position, 
                    endPos, 
                    moveSpeed * Time.deltaTime
                );
                
                if (Vector3.Distance(transform.position, endPos) < 0.01f)
                {
                    transform.position = startPos;
                }
                break;
        }
    }
    
    // Visual helper in editor
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 gizmoStart = Application.isPlaying ? startPos : transform.position + startOffset;
        Vector3 gizmoEnd = Application.isPlaying ? endPos : transform.position + endOffset;
        
        Gizmos.DrawLine(gizmoStart, gizmoEnd);
        Gizmos.DrawWireSphere(gizmoStart, 0.3f);
        Gizmos.DrawWireSphere(gizmoEnd, 0.3f);
    }
    
    public void ResetPosition()
    {
        transform.position = startPos;
        movingToEnd = true;
        pauseTimer = 0f;
    }
}
