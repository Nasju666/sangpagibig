using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class BackgroundMusicDucker : MonoBehaviour
{
    [Header("Volume")]
    [Range(0f, 1f)]
    public float normalVolume = 0.6f;

    [Range(0f, 1f)]
    public float minVolume = 0.15f;

    [Header("Ducking")]
    [Tooltip("How much each active sound reduces volume")]
    [Range(0.05f, 0.5f)]
    public float duckPerSound = 0.2f;

    public float fadeTime = 0.25f;

    AudioSource musicSource;
    Coroutine fadeRoutine;

    int activeSoundCount;

    void Awake()
    {
        musicSource = GetComponent<AudioSource>();
        musicSource.volume = normalVolume;
    }

    // 🔔 CALLED WHEN A SOUND STARTS
    public void RegisterSound()
    {
        activeSoundCount++;
        UpdateVolume();
    }

    // 🔕 CALLED WHEN A SOUND ENDS
    public void UnregisterSound()
    {
        activeSoundCount = Mathf.Max(0, activeSoundCount - 1);
        UpdateVolume();
    }

    void UpdateVolume()
    {
        float targetVolume =
            normalVolume - (activeSoundCount * duckPerSound);

        targetVolume = Mathf.Clamp(targetVolume, minVolume, normalVolume);
        StartFade(targetVolume);
    }

    void StartFade(float target)
    {
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(FadeRoutine(target));
    }

    IEnumerator FadeRoutine(float target)
    {
        float start = musicSource.volume;
        float t = 0f;

        while (t < fadeTime)
        {
            t += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(start, target, t / fadeTime);
            yield return null;
        }

        musicSource.volume = target;
    }
}
