using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(AudioSource))]
public class BoulderRollingSound : MonoBehaviour
{
    [Header("Audio")]
    public AudioClip rollingSound;   // 👈 drag clip here
    public float minSpeed = 1f;
    public float maxSpeed = 25f;

    public float minPitch = 0.8f;
    public float maxPitch = 1.2f;

    public float minVolume = 0.2f;
    public float maxVolume = 1f;

    private Rigidbody rb;
    private AudioSource audioSource;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();

        audioSource.clip = rollingSound;
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.dopplerLevel = 0f;
    }

    void FixedUpdate()
    {
        if (rollingSound == null) return;

        float speed = rb.velocity.magnitude;

        if (speed > minSpeed)
        {
            if (!audioSource.isPlaying)
                audioSource.Play();

            float t = Mathf.InverseLerp(minSpeed, maxSpeed, speed);
            audioSource.pitch = Mathf.Lerp(minPitch, maxPitch, t);
            audioSource.volume = Mathf.Lerp(minVolume, maxVolume, t);
        }
        else
        {
            StopSound();
        }
    }

    void StopSound()
    {
        if (audioSource.isPlaying)
            audioSource.Stop();
    }

    void OnDestroy()
    {
        StopSound();
    }
}
