using UnityEngine;

public class RelicButton : MonoBehaviour
{
    [Header("References")]
    public GameObject relicToSpawn;

    bool activated;
    UIManager ui;

    void Start()
    {
        ui = FindObjectOfType<UIManager>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (activated) return;
        if (!other.CompareTag("Player")) return;

        activated = true;

        // Spawn / reveal the relic
        if (relicToSpawn != null)
        {
            relicToSpawn.SetActive(true);
        }

        // Show UI feedback
        if (ui != null)
        {
            ui.ShowRelicUnlocked();
        }

        // Optional: disable button so it can't be reused
        gameObject.SetActive(false);
    }
}
