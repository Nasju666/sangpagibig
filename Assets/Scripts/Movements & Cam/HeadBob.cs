using UnityEngine;

public class HeadBob : MonoBehaviour
{
    public float walkBobSpeed = 8f;
    public float sprintBobSpeed = 14f;
    public float walkBobAmount = 0.05f;
    public float sprintBobAmount = 0.1f;
    public float crouchBobAmount = 0.02f;

    Vector3 startPos;
    float timer;

    PlayerMovement player;

    void Start()
    {
        startPos = transform.localPosition;
        player = GetComponentInParent<PlayerMovement>();
    }

    void Update()
    {
        if (!player || !player.IsGrounded || !player.IsMoving)
        {
            timer = 0;
            transform.localPosition = Vector3.Lerp(transform.localPosition, startPos, 10f * Time.deltaTime);
            return;
        }

        float speed = player.IsSprinting ? sprintBobSpeed : walkBobSpeed;
        float amount = player.IsSprinting ? sprintBobAmount : walkBobAmount;

        if (player.IsCrouching)
            amount = crouchBobAmount;

        timer += Time.deltaTime * speed;
        transform.localPosition = startPos + Vector3.up * Mathf.Sin(timer) * amount;
    }
}
