using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BoulderSpawner : MonoBehaviour
{
    public GameObject boulderPrefab;
    public Transform spawnPoint;

    [Header("Timing")]
    public float startDelay = 1.5f;     // delay before spawning starts
    public float spawnInterval = 2f;    // delay between each boulder

    [Header("Force")]
    public float spawnForce = 18f;

    private List<GameObject> spawnedBoulders = new List<GameObject>();
    private Coroutine spawnRoutine;

    void Start()
    {
        StartSpawning();
    }

    public void StartSpawning()
    {
        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);

        spawnRoutine = StartCoroutine(SpawnLoop());
    }

    IEnumerator SpawnLoop()
    {
        // ⏳ initial delay
        yield return new WaitForSeconds(startDelay);

        while (true)
        {
            SpawnBoulder();
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    void SpawnBoulder()
    {
        GameObject boulder = Instantiate(
            boulderPrefab,
            spawnPoint.position,
            spawnPoint.rotation
        );

        spawnedBoulders.Add(boulder);

        Rigidbody rb = boulder.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.AddForce(spawnPoint.forward * spawnForce, ForceMode.VelocityChange);
        }
    }

    public void UnregisterBoulder(GameObject boulder)
    {
        spawnedBoulders.Remove(boulder);
    }

    public void ResetAllBoulders()
    {
        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);

        foreach (GameObject boulder in spawnedBoulders)
        {
            if (boulder != null)
                Destroy(boulder);
        }

        spawnedBoulders.Clear();

        // restart with delay again
        StartSpawning();
    }

    public void StopSpawner(bool destroySpawner = false)
{
    if (spawnRoutine != null)
    {
        StopCoroutine(spawnRoutine);
        spawnRoutine = null;
    }

    foreach (GameObject b in spawnedBoulders)
    {
        if (b != null)
            Destroy(b);
    }

    spawnedBoulders.Clear();

    if (destroySpawner)
        Destroy(gameObject);
}
}
