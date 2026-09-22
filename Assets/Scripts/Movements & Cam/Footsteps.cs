using UnityEngine;

public class Footsteps : MonoBehaviour
{
    public AudioSource source;

    public AudioClip[] concrete;
    public AudioClip[] wood;
    public AudioClip[] metal;

    public float walkStepRate = 0.5f;
    public float sprintStepRate = 0.35f;
    public float crouchStepRate = 0.8f;

    PlayerMovement player;
    float stepTimer;

    void Start()
    {
        player = GetComponent<PlayerMovement>();
    }

    void Update()
    {
        if (!player.IsGrounded || !player.IsMoving)
            return;

        stepTimer -= Time.deltaTime;

        float rate = walkStepRate;
        if (player.IsSprinting) rate = sprintStepRate;
        if (player.IsCrouching) rate = crouchStepRate;

        if (stepTimer <= 0f)
        {
            PlayStep();
            stepTimer = rate;
        }
    }

    void PlayStep()
    {
        if (!Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 2f))
            return;

        AudioClip[] clips = concrete;

        switch (hit.collider.tag)
        {
            case "Wood": clips = wood; break;
            case "Metal": clips = metal; break;
        }

        if (clips.Length == 0) return;

        source.pitch = Random.Range(0.95f, 1.05f);
        source.volume = player.IsCrouching ? 0.25f : 0.45f;
        source.PlayOneShot(clips[Random.Range(0, clips.Length)]);
    }
}
