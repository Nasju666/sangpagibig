using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class BackgroundMusic : MonoBehaviour
{
    [Header("Music")]
    public AudioClip musicClip;
    [Range(0f, 1f)]
    public float volume = 0.6f;
    public bool loop = true;
    public bool playOnStart = true;

    [Header("Persistence")]
    public bool dontDestroyOnLoad = true;

    private AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        audioSource.clip = musicClip;
        audioSource.volume = volume;
        audioSource.loop = loop;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // always 2D

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        if (playOnStart && musicClip != null)
            audioSource.Play();
    }

    public void Play()
    {
        if (!audioSource.isPlaying)
            audioSource.Play();
    }

    public void Stop()
    {
        audioSource.Stop();
    }

    public void SetVolume(float value)
    {
        audioSource.volume = Mathf.Clamp01(value);
    }
}
