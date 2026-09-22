using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 4f;
    public float sprintSpeed = 7f;
    public float crouchSpeed = 2f;
    public float acceleration = 10f;

    [Header("Jumping & Gravity")]
    public float jumpHeight = 1.5f;
    public float gravity = -20f;
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
float coyoteTimer;

    CharacterController controller;
    Vector3 velocity;
    Vector3 moveVelocity;

    bool isGrounded;
    bool isCrouching;

    MovingPlatform currentPlatform;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        GroundCheck();
        Crouch();
        Move();
        GravityAndJump();
        AutoJump();
        ApplyPlatformMovement();
    }

    // =============================
    // MOVEMENT
    // =============================
    void Move()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        Vector3 input = (transform.right * x + transform.forward * z).normalized;

        bool sprintInput = Input.GetKey(KeyCode.LeftShift);
        bool sprinting = sprintInput && !isCrouching && input.magnitude > 0.1f;

        float speed = walkSpeed;
        if (isCrouching) speed = crouchSpeed;
        else if (sprinting) speed = sprintSpeed;

        Vector3 targetVelocity = input * speed;
        moveVelocity = Vector3.Lerp(
            moveVelocity,
            targetVelocity,
            acceleration * Time.deltaTime
        );

        controller.Move(moveVelocity * Time.deltaTime);
    }

    // =============================
    // GRAVITY & JUMP
    // =============================
    void GravityAndJump()
    {
        if (isGrounded && velocity.y < 0)
            velocity.y = -2f;

        if (Input.GetKeyDown(KeyCode.Space) && coyoteTimer > 0f && !isCrouching)

            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            coyoteTimer = 0f;

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    // =============================
    // GROUND CHECK
    // =============================
   void GroundCheck()
{
    bool groundedByController = controller.isGrounded;

    bool groundedBySphere = Physics.SphereCast(
        transform.position + Vector3.up * 0.1f,
        controller.radius * 0.9f,
        Vector3.down,
        out RaycastHit hit,
        controller.height / 2 + groundCheckDistance
    );

    isGrounded = groundedByController || groundedBySphere;

    if (isGrounded)
    {
        coyoteTimer = coyoteTime;
        currentPlatform = hit.collider != null
            ? hit.collider.GetComponentInParent<MovingPlatform>()
            : null;
    }
    else
    {
        coyoteTimer -= Time.deltaTime;
        if (coyoteTimer <= 0f)
            currentPlatform = null;
    }
}

    // =============================
    // CROUCH
    // =============================
    void Crouch()
    {
        isCrouching = Input.GetKey(KeyCode.LeftControl);

        float targetHeight = isCrouching ? crouchHeight : standingHeight;
        controller.height = Mathf.Lerp(
            controller.height,
            targetHeight,
            crouchSmooth * Time.deltaTime
        );
    }

    // =============================
    // AUTO JUMP
    // =============================
    void AutoJump()
    {
        if (!isGrounded || moveVelocity.magnitude < 0.1f || isCrouching)
            return;

        RaycastHit hit;
        Vector3 origin = transform.position + Vector3.up * 0.3f;

        if (Physics.Raycast(origin, transform.forward, out hit, autoJumpCheckDistance, obstacleLayer))
        {
            float obstacleHeight = hit.collider.bounds.max.y - transform.position.y;
            if (obstacleHeight > 0.3f && obstacleHeight < 1.2f)
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }

    // =============================
    // PUSH RIGIDBODIES
    // =============================
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
    // =============================
    // PUBLIC STATES
    // =============================
    public bool IsGrounded => isGrounded;
    public bool IsMoving => moveVelocity.magnitude > 0.1f;
    public bool IsCrouching => isCrouching;
    public bool IsSprinting =>
        Input.GetKey(KeyCode.LeftShift) &&
        !isCrouching &&
        IsMoving;
}
