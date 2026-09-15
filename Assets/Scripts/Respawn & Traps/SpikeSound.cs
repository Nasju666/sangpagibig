using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SpikeSound : MonoBehaviour
{
    private AudioSource audioSource;
    private bool played;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    // 🔔 CALLED BY SpikeTrap
    public void PlayMoveSound()
    {
        if (played) return;

        played = true;
        audioSource.Play();
    }

    // 🔁 CALLED ON RESPAWN
    public void ResetSound()
    {
        played = false;
    }
}
