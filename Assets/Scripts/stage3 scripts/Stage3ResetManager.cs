using UnityEngine;

public class Stage3ResetManager : MonoBehaviour
{
    private MovingPlatformTrigger[] traps;
    private BoulderSpawner[] spawners;

    void Awake()
    {
        traps = FindObjectsOfType<MovingPlatformTrigger>();
        spawners = FindObjectsOfType<BoulderSpawner>();
    }

    public void ResetStage()
    {
        // reset traps
        foreach (var trap in traps)
        {
            trap.ResetPlatform();
        }

        // reset all boulder spawners
        foreach (var spawner in spawners)
        {
            spawner.ResetAllBoulders();
        }
    }
}
