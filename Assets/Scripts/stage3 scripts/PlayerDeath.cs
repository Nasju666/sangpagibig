using UnityEngine;
using System.Collections;

public class PlayerDeath : MonoBehaviour
{
    public Stage3ResetManager stageResetManager;
    public Transform respawnPoint;

    private bool isDead;

    public void Die()
    {
        if (isDead) return;
        isDead = true;
        StartCoroutine(DeathRoutine());
    }

    IEnumerator DeathRoutine()
    {
        yield return new WaitForSeconds(0.5f);

        if (stageResetManager != null)
            stageResetManager.ResetStage();

        if (respawnPoint != null)
        {
            transform.position = respawnPoint.position;

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
                rb.velocity = Vector3.zero;
        }

        isDead = false;
    }
}
