using UnityEngine;

public class MovingPlatformTrigger : MonoBehaviour
{
    public Transform platform;
    public Vector3 moveDirection = Vector3.right;
    public float moveDistance = 5f;
    public float moveSpeed = 2f;

    private Vector3 startPos;
    private Vector3 targetPos;
    private bool activated = false;

    void Start()
    {
        startPos = platform.position;
        targetPos = startPos + moveDirection.normalized * moveDistance;
    }

    void Update()
    {
        if (!activated) return;

        platform.position = Vector3.MoveTowards(
            platform.position,
            targetPos,
            moveSpeed * Time.deltaTime
        );
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            activated = true;
        }
    }

    // 🔁 CALLED ON RESPAWN
    public void ResetPlatform()
    {
        activated = false;
        platform.position = startPos;
    }
}
