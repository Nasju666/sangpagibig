using UnityEngine;

public class ChaseCharacter : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Animator animator;
    public AudioSource audioSource;
    public PlayerDeath playerDeath;   // 🔥 use your existing death system

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 5f;
    public float chaseRange = 10f;
    public float deathRange = 0.75f;
    public float groundHeight = 0f;

    [Header("Audio Clips")]
    public AudioClip runningSound;
    public AudioClip[] growlSounds;

    private bool isPlayingRunSound;
    private float nextGrowlTime;
    private Vector3 startPosition;
private Quaternion startRotation;

    private void Start()
{
    startPosition = transform.position;
    startRotation = transform.rotation;
}
    private void Update()
    {
        if (player == null) return;

        float distance = Vector3.Distance(player.position, transform.position);

        if (distance <= deathRange)
        {
            if (playerDeath != null)
                playerDeath.Die();
            return;
        }

        HandleChase(distance);
    }

    private void HandleChase(float distance)
    {
        Vector3 currentPos = transform.position;
        currentPos.y = groundHeight;

        if (distance <= chaseRange)
        {
            Vector3 targetPos = player.position;
            targetPos.y = groundHeight;

            Vector3 dir = (targetPos - currentPos).normalized;

            transform.position = Vector3.MoveTowards(
                currentPos,
                targetPos,
                moveSpeed * Time.deltaTime
            );

            if (dir != Vector3.zero)
            {
                Quaternion rot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    rot,
                    rotationSpeed * Time.deltaTime
                );
            }

            // 🎬 Animation
            animator.SetBool("isRunning", true);

            // 🔊 Running loop
            if (!isPlayingRunSound && runningSound != null)
            {
                audioSource.clip = runningSound;
                audioSource.loop = true;
                audioSource.Play();
                isPlayingRunSound = true;
            }

            // 👹 Growls
            if (growlSounds.Length > 0 && Time.time >= nextGrowlTime)
            {
                audioSource.PlayOneShot(
                    growlSounds[Random.Range(0, growlSounds.Length)]
                );
                nextGrowlTime = Time.time + Random.Range(3f, 6f);
            }
        }
        else
        {
            transform.position = currentPos;

            animator.SetBool("isRunning", false);

            if (isPlayingRunSound)
            {
                audioSource.Stop();
                isPlayingRunSound = false;
            }
        }
    }
    public void ResetMonster()
{
    transform.position = startPosition;
    transform.rotation = startRotation;

    // stop animation
    if (animator != null)
        animator.SetBool("isRunning", false);

    // stop audio
    if (audioSource != null)
        audioSource.Stop();

    // stop physics movement if any
    Rigidbody rb = GetComponent<Rigidbody>();
    if (rb != null)
    {
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }
}

}
