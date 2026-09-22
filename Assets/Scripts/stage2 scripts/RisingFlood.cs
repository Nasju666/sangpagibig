using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class RisingFlood : MonoBehaviour
{
    [Header("Flood Movement")]
    public float riseSpeed = 0.3f;

    bool isPaused;

    void Update()
    {
        if (isPaused) return;

        transform.Translate(Vector3.up * riseSpeed * Time.deltaTime);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    public void PauseFlood(float duration)
    {
        StopAllCoroutines();
        StartCoroutine(PauseCoroutine(duration));
    }

    IEnumerator PauseCoroutine(float duration)
    {
        isPaused = true;
        yield return new WaitForSeconds(duration);
        isPaused = false;
    }
}
