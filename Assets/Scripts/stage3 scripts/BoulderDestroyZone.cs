using UnityEngine;

public class BoulderDestroyZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("BoulderDestroy"))
        {
            Destroy(other.gameObject);
        }
    }
}
