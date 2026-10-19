using UnityEngine;

/// <summary>
/// Camera position: a soft figure-eight bob that scales with real movement speed, plus a landing
/// dip that springs back. Respects Settings ▸ Camera (head bob toggle, camera motion amount).
/// </summary>
public class HeadBob : MonoBehaviour
{
    public float walkBobSpeed = 8f;
    public float sprintBobSpeed = 13f;
    public float walkBobAmount = 0.045f;
    public float sprintBobAmount = 0.075f;
    public float crouchBobAmount = 0.02f;

    Vector3 startPos;
    float timer, blend, dip, dipVelocity;
    PlayerMovement player;

    void Start()
    {
        startPos = transform.localPosition;
        player = GetComponentInParent<PlayerMovement>();
        if (player != null) player.Landed += OnLanded;
    }

    void OnDestroy() { if (player != null) player.Landed -= OnLanded; }

    void OnLanded(float impact)
    {
        // Harder landings dip further (impact in m/s).
        dipVelocity -= Mathf.Clamp(impact * .045f, 0f, .9f) * RuleCardsSettings.CameraMotion;
    }

    void Update()
    {
        if (player == null) return;
        float dt = Time.deltaTime;
        bool bobbing = RuleCardsSettings.HeadBob && player.IsGrounded && player.IsMoving;
        float speed01 = Mathf.Clamp01(player.PlanarSpeed / Mathf.Max(.1f, player.sprintSpeed));
        blend = Mathf.MoveTowards(blend, bobbing ? 1f : 0f, dt * 4f);

        float freq = player.IsSprinting ? sprintBobSpeed : walkBobSpeed;
        float amount = player.IsCrouching ? crouchBobAmount : Mathf.Lerp(walkBobAmount, sprintBobAmount, player.IsSprinting ? 1f : 0f);
        if (bobbing) timer += dt * freq * Mathf.Lerp(.6f, 1f, speed01);

        Vector3 bob = new Vector3(Mathf.Cos(timer) * amount * .55f, Mathf.Abs(Mathf.Sin(timer)) * amount * 1.4f - amount * .7f, 0f) * blend;

        // Critically-damped-ish spring for the landing dip.
        dipVelocity += (-dip * 90f - dipVelocity * 16f) * dt;
        dip += dipVelocity * dt;
        dip = Mathf.Clamp(dip, -.35f, .1f);

        transform.localPosition = startPos + bob + Vector3.up * dip;
    }
}
