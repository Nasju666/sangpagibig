using UnityEngine;

public class Relicc : MonoBehaviour
{
    [Header("Portal (Assign in Inspector)")]
    [SerializeField] GameObject portalToActivate;

    UIManager ui;
    RisingFloodd flood;

    void Awake()
    {
        // Auto-find systems (not shown in Inspector)
        ui = FindObjectOfType<UIManager>();
        flood = FindObjectOfType<RisingFloodd>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // Start flood sequence
        if (flood != null)
        {
            flood.StartFloodWithDelay();
        }

        // Show lava warning UI
        if (ui != null)
        {
            ui.ShowLavaWarning();
        }

        // Activate portal
        if (portalToActivate != null)
        {
            portalToActivate.SetActive(true);
        }
        else
        {
            Debug.LogWarning("Relicc: Portal not assigned in Inspector");
        }

        Destroy(gameObject);
    }
}
