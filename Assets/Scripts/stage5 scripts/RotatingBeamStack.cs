using UnityEngine;

/// <summary>
/// Controls multiple rotating beams stacked vertically.
/// Creates a challenging obstacle where beams rotate at different speeds/directions.
/// </summary>
public class RotatingBeamStack : MonoBehaviour
{
    [Header("Beam References")]
    [Tooltip("Array of rotating beam children - assign in order from bottom to top")]
    public RotatingBeam[] beams;
    
    [Header("Stack Settings")]
    [Tooltip("Base rotation speed for the first beam")]
    public float baseSpeed = 60f;
    
    [Tooltip("Speed multiplier for each subsequent beam")]
    public float speedMultiplier = 1.5f;
    
    [Tooltip("Alternate directions for each beam")]
    public bool alternateDirections = true;
    
    [Header("Dynamic Difficulty")]
    [Tooltip("If true, speeds up over time")]
    public bool progressiveDifficulty = false;
    public float difficultyIncreaseRate = 5f; // Speed increase per second
    public float maxSpeedMultiplier = 3f;
    
    private float currentDifficultyMultiplier = 1f;
    
    void Start()
    {
        InitializeBeams();
    }
    
    void Update()
    {
        if (progressiveDifficulty)
        {
            currentDifficultyMultiplier += difficultyIncreaseRate * Time.deltaTime / 60f;
            currentDifficultyMultiplier = Mathf.Min(currentDifficultyMultiplier, maxSpeedMultiplier);
            UpdateBeamSpeeds();
        }
    }
    
    void InitializeBeams()
    {
        for (int i = 0; i < beams.Length; i++)
        {
            if (beams[i] == null) continue;
            
            // Calculate speed for this beam
            float speed = baseSpeed * Mathf.Pow(speedMultiplier, i);
            beams[i].SetRotationSpeed(speed);
            
            // Alternate direction if enabled
            if (alternateDirections)
            {
                beams[i].SetRotationDirection(i % 2 == 0 ? 1f : -1f);
            }
        }
    }
    
    void UpdateBeamSpeeds()
    {
        for (int i = 0; i < beams.Length; i++)
        {
            if (beams[i] == null) continue;
            
            float speed = baseSpeed * Mathf.Pow(speedMultiplier, i) * currentDifficultyMultiplier;
            beams[i].SetRotationSpeed(speed);
        }
    }
    
    // Reset difficulty when player respawns
    public void ResetDifficulty()
    {
        currentDifficultyMultiplier = 1f;
        InitializeBeams();
    }
}
