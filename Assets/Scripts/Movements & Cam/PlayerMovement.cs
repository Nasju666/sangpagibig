using System;
using UnityEngine;

/// <summary>
/// First-person controller. Same speeds and jump height as before (levels are tuned to them), but:
/// working coyote time + jump buffering, variable jump height (tap = short hop, hold = full jump),
/// frame-rate-independent acceleration with lighter air control, no sticking to ceilings,
/// trigger volumes no longer count as ground, can't stand up into a ceiling, and a Landed event.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 4f;
    public float sprintSpeed = 7f;
    public float crouchSpeed = 2f;
    [Tooltip("How quickly you reach target speed on the ground (higher = snappier).")]
    public float acceleration = 10f;
    [Tooltip("Braking when you let go of the keys.")]
    public float deceleration = 14f;
    [Range(0f, 1f)] public float airControl = .6f;

    [Header("Jumping & Gravity")]
    public float jumpHeight = 1.5f;
    public float gravity = -20f;
    [Tooltip("Upward speed is multiplied by this when Space is released early (short hop).")]
    [Range(.2f, 1f)] public float jumpCutMultiplier = .5f;
    public float maxFallSpeed = 40f;
    public float autoJumpCheckDistance = 0.6f;
    public LayerMask obstacleLayer;

    [Header("Crouch")]
    public float crouchHeight = 1f;
    public float standingHeight = 2f;
    public float crouchSmooth = 8f;

    [Header("Ground Check")]
    public float groundCheckDistance = 0.2f;

    [Header("Physics Interaction")]
    public float pushForce = 5f;

    [Header("Jump Reliability")]
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.12f;

    /// <summary>Raised on touchdown with the impact speed (m/s).</summary>
    public event Action<float> Landed;

    CharacterController controller;
    Vector3 velocity;
    Vector3 moveVelocity;
    float coyoteTimer, jumpBufferTimer;
    bool isGrounded, wasGrounded, isCrouching, jumping;
    MovingPlatform currentPlatform;

    void Awake() { controller = GetComponent<CharacterController>(); }

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, .05f);
        GroundCheck(dt);
        Crouch(dt);
        Move(dt);
        GravityAndJump(dt);
        AutoJump();
        ApplyPlatformMovement();
    }

    // ---------------------------------------------------------------- movement
    void Move(float dt)
    {
        // No input while paused or while a cutscene/dialogue holds the player.
        float x = MouseLook.isPaused ? 0f : Input.GetAxisRaw("Horizontal");
        float z = MouseLook.isPaused ? 0f : Input.GetAxisRaw("Vertical");
        Vector3 input = Vector3.ClampMagnitude(transform.right * x + transform.forward * z, 1f);

        float speed = isCrouching ? crouchSpeed : IsSprinting ? sprintSpeed : walkSpeed;
        Vector3 target = input * speed;

        float rate = input.sqrMagnitude > .01f ? acceleration : deceleration;
        if (!isGrounded) rate *= airControl;
        moveVelocity = Vector3.Lerp(moveVelocity, target, 1f - Mathf.Exp(-rate * dt));

        controller.Move(moveVelocity * dt);
    }

    // ---------------------------------------------------------------- gravity & jump
    void GravityAndJump(float dt)
    {
        if (isGrounded && velocity.y < 0f) { velocity.y = -2f; jumping = false; }

        if (!MouseLook.isPaused && Input.GetKeyDown(KeyCode.Space)) jumpBufferTimer = jumpBufferTime;
        else jumpBufferTimer -= dt;

        if (jumpBufferTimer > 0f && coyoteTimer > 0f && !isCrouching)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            jumping = true;
        }

        // Let go of Space while still rising → shorter hop.
        if (jumping && velocity.y > 0f && !Input.GetKey(KeyCode.Space))
        {
            velocity.y *= jumpCutMultiplier;
            jumping = false;
        }

        velocity.y = Mathf.Max(velocity.y + gravity * dt, -maxFallSpeed);
        CollisionFlags flags = controller.Move(velocity * dt);
        if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0f) { velocity.y = 0f; jumping = false; } // bonk, don't stick
    }

    // ---------------------------------------------------------------- ground check
    void GroundCheck(float dt)
    {
        bool groundedBySphere = Physics.SphereCast(
            transform.position + Vector3.up * 0.1f,
            controller.radius * 0.9f,
            Vector3.down,
            out RaycastHit hit,
            controller.height / 2 + groundCheckDistance,
            ~0, QueryTriggerInteraction.Ignore);

        isGrounded = (controller.isGrounded || groundedBySphere) && velocity.y <= .5f;

        if (isGrounded && !wasGrounded && Landed != null) Landed(Mathf.Abs(Mathf.Min(0f, lastAirSpeed)));
        lastAirSpeed = velocity.y;
        wasGrounded = isGrounded;

        if (isGrounded)
        {
            coyoteTimer = coyoteTime;
            currentPlatform = hit.collider != null ? hit.collider.GetComponentInParent<MovingPlatform>() : null;
        }
        else
        {
            coyoteTimer -= dt;
            if (coyoteTimer <= 0f) currentPlatform = null;
        }
    }
    float lastAirSpeed;

    // ---------------------------------------------------------------- crouch
    void Crouch(float dt)
    {
        bool wantCrouch = !MouseLook.isPaused && Input.GetKey(KeyCode.LeftControl);
        if (!wantCrouch && isCrouching)
        {
            // Don't stand up into a ceiling.
            float r = controller.radius * .95f;
            Vector3 bottom = transform.position + Vector3.up * (r - controller.height * .5f + controller.center.y);
            if (Physics.SphereCast(bottom, r, Vector3.up, out _, standingHeight - r * 2f + .05f, ~0, QueryTriggerInteraction.Ignore))
                wantCrouch = true;
        }
        isCrouching = wantCrouch;

        float targetHeight = isCrouching ? crouchHeight : standingHeight;
        controller.height = Mathf.Lerp(controller.height, targetHeight, 1f - Mathf.Exp(-crouchSmooth * dt));
    }

    // ---------------------------------------------------------------- auto jump
    void AutoJump()
    {
        if (!isGrounded || moveVelocity.magnitude < 0.1f || isCrouching) return;
        Vector3 origin = transform.position + Vector3.up * 0.3f;
        if (Physics.Raycast(origin, transform.forward, out RaycastHit hit, autoJumpCheckDistance, obstacleLayer, QueryTriggerInteraction.Ignore))
        {
            float obstacleHeight = hit.collider.bounds.max.y - transform.position.y;
            if (obstacleHeight > 0.3f && obstacleHeight < 1.2f)
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }

    // ---------------------------------------------------------------- push rigidbodies
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody rb = hit.collider.attachedRigidbody;
        if (rb == null || rb.isKinematic) return;
        if (hit.moveDirection.y < -0.3f) return;
        if (moveVelocity.magnitude < 0.1f) return;
        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z).normalized;
        float speedFactor = Mathf.Clamp01(moveVelocity.magnitude / sprintSpeed);
        rb.AddForce(pushDir * pushForce * speedFactor, ForceMode.Force);
    }

    void ApplyPlatformMovement()
    {
        if (currentPlatform == null) return;
        controller.Move(currentPlatform.DeltaMovement);
    }

    // ---------------------------------------------------------------- public state
    public bool IsGrounded => isGrounded;
    public bool IsMoving => moveVelocity.magnitude > 0.1f;
    public bool IsCrouching => isCrouching;
    public float PlanarSpeed => new Vector3(moveVelocity.x, 0f, moveVelocity.z).magnitude;
    public bool IsSprinting =>
        !MouseLook.isPaused && Input.GetKey(KeyCode.LeftShift) && !isCrouching &&
        Input.GetAxisRaw("Vertical") > 0.1f; // sprint forward only, so strafing/backpedal stay controlled
}
