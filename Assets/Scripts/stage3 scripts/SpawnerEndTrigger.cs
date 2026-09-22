using UnityEngine;

public class SpawnerEndTrigger : MonoBehaviour
{
    public BoulderSpawner spawner;
    public bool destroySpawnerOnTrigger = true;

    [Header("Portal")]
    public GameObject portalToEnable;   // 👈 assign disabled portal here

    private bool triggered;

    private void OnTriggerEnter(Collider other)
    {
        if (triggered) return;
        if (!other.CompareTag("Player")) return;

        triggered = true;

        // stop / destroy spawner
        if (spawner != null)
            spawner.StopSpawner(destroySpawnerOnTrigger);

        // enable portal
        if (portalToEnable != null)
            portalToEnable.SetActive(true);

        // destroy this trigger
        Destroy(gameObject);
    }
}
