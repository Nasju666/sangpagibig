using UnityEngine;

public class MovingPlatform : MonoBehaviour
{
    public Vector3 moveDirection = Vector3.up;
    public float moveDistance = 4f;
    public float speed = 2f;

    [Header("Timing")]
    public float startDelay = 0f;   // seconds before movement starts
    public float cycleOffset = 0f;  // phase offset for desync

    Vector3 startPos;
    Vector3 lastPosition;
    float timer;

    public Vector3 DeltaMovement { get; private set; }

    void Start()
    {
        startPos = transform.position;
        lastPosition = transform.position;
        timer = -startDelay; // negative = waiting
    }

    void Update()
    {
        timer += Time.deltaTime;

        // Not started yet → no movement
        if (timer < 0f)
        {
            DeltaMovement = Vector3.zero;
            lastPosition = transform.position;
            return;
        }

        float t = (timer + cycleOffset) * speed;
        float offset = Mathf.PingPong(t, moveDistance);

        transform.position = startPos + moveDirection.normalized * offset;

        DeltaMovement = transform.position - lastPosition;
        lastPosition = transform.position;
    }
}
