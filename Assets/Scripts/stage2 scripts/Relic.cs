using UnityEngine;

public class Relic : MonoBehaviour
{
    public float pauseDuration = 5f;

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        RisingFlood flood = FindObjectOfType<RisingFlood>();
        if (flood != null)
        {
            flood.PauseFlood(pauseDuration);
        }

        Destroy(gameObject);
    }
}
