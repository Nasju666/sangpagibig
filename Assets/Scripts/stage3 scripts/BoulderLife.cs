using UnityEngine;

public class BoulderLife : MonoBehaviour
{
    private BoulderSpawner spawner;

    void Awake()
    {
        spawner = FindObjectOfType<BoulderSpawner>();
    }

    void OnDestroy()
    {
        if (spawner != null)
            spawner.UnregisterBoulder(gameObject);
    }
}
