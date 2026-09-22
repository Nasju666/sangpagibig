using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class RisingFloodd : MonoBehaviour
{
    [Header("Flood Settings")]
    public float riseSpeed = 0.5f;
    public float startDelay = 3f;

    [Header("References (Assign in Inspector)")]
    [SerializeField] UIManager ui;
    [SerializeField] CameraShake cameraShake;

    [Header("Audio")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip warningClip;

    bool isActive;

    void Start()
    {
        // Safety checks (no crashes)
        if (ui == null)
            Debug.LogError("RisingFloodd: UIManager not assigned");

        if (cameraShake == null)
            Debug.LogError("RisingFloodd: CameraShake not assigned");

        if (audioSource == null)
            Debug.LogError("RisingFloodd: AudioSource not assigned");

        if (warningClip == null)
            Debug.LogWarning("RisingFloodd: Warning AudioClip not assigned");
    }

    void Update()
    {
        if (!isActive) return;
        transform.Translate(Vector3.up * riseSpeed * Time.deltaTime);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    public void StartFloodWithDelay()
    {
        StartCoroutine(StartFloodCoroutine());
    }

    IEnumerator StartFloodCoroutine()
    {
        // UI
        if (ui != null)
        {
            ui.ShowLavaWarning();
            StartCoroutine(ui.Countdown(5));
        }

        // Audio
        if (audioSource != null && warningClip != null)
        {
            audioSource.clip = warningClip;
            audioSource.loop = true;
            audioSource.Play();
        }

        yield return new WaitForSeconds(startDelay);

        // Stop warning
        if (ui != null)
            ui.HideLavaWarning();

        if (audioSource != null)
            audioSource.Stop();

        // Camera shake
        if (cameraShake != null)
            StartCoroutine(cameraShake.Shake(0.5f, 0.6f));

        isActive = true;
    }
}
