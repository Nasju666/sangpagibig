using UnityEngine;

public class BoulderKill : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        PlayerDeath player = other.GetComponent<PlayerDeath>();
        if (player != null)
            player.Die();
    }
}
