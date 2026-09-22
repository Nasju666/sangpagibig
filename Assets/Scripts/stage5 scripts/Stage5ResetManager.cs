using UnityEngine;

/// <summary>
/// Reset manager for Stage 5.
/// Handles resetting all obstacles when player respawns.
/// </summary>
public class Stage5ResetManager : MonoBehaviour
{
    [Header("Obstacles to Reset")]
    public LaserGrid[] laserGrids;
    public MovingLaser[] movingLasers;
    public RotatingBeamStack[] beamStacks;
    
    [Header("Auto-Find")]
    [Tooltip("If true, automatically finds all Stage 5 obstacles in scene")]
    public bool autoFindObstacles = true;
    
    void Start()
    {
        if (autoFindObstacles)
        {
            FindAllObstacles();
        }
    }
    
    void FindAllObstacles()
    {
        laserGrids = FindObjectsOfType<LaserGrid>();
        movingLasers = FindObjectsOfType<MovingLaser>();
        beamStacks = FindObjectsOfType<RotatingBeamStack>();
        
        Debug.Log($"Stage5ResetManager found: {laserGrids.Length} laser grids, " +
                  $"{movingLasers.Length} moving lasers, {beamStacks.Length} beam stacks");
    }
    
    // Call this when player respawns
    public void ResetAllObstacles()
    {
        foreach (var grid in laserGrids)
        {
            if (grid != null)
                grid.ResetGrid();
        }
        
        foreach (var laser in movingLasers)
        {
            if (laser != null)
                laser.ResetPosition();
        }
        
        foreach (var stack in beamStacks)
        {
            if (stack != null)
                stack.ResetDifficulty();
        }
        
        Debug.Log("Stage 5 obstacles reset!");
    }
}
