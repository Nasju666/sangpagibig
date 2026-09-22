using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class WallPushSound : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public AudioClip pushSound;   // 👈 drag audio clip here

    [Header("Movement")]
    public float minMoveSpeed = 0.02f;
    public float directionThreshold = 0.3f;

    [Header("Audio")]
    public float minPitch = 0.9f;
    public float maxPitch = 1.1f;
    public float minVolume = 0.3f;
    public float maxVolume = 1f;

    private AudioSource audioSource;
    private Vector3 lastPosition;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        audioSource.clip = pushSound;
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.dopplerLevel = 0f;

        lastPosition = transform.position;
    }

    void Update()
    {
        if (player == null || pushSound == null) return;

        Vector3 movement = transform.position - lastPosition;
        float speed = movement.magnitude / Time.deltaTime;
        lastPosition = transform.position;

        if (speed < minMoveSpeed)
        {
            StopSound();
            return;
        }

        Vector3 moveDir = movement.normalized;
        Vector3 toPlayerDir = (player.position - transform.position).normalized;

        float dot = Vector3.Dot(moveDir, toPlayerDir);

        if (dot > directionThreshold)
        {
            if (!audioSource.isPlaying)
                audioSource.Play();

            float t = Mathf.Clamp01(speed);
            audioSource.volume = Mathf.Lerp(minVolume, maxVolume, t);
            audioSource.pitch = Mathf.Lerp(minPitch, maxPitch, t);
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

    void OnDisable()
    {
        StopSound();
    }
}
