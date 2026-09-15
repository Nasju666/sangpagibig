using UnityEngine;

public class PressurePlateDoor : MonoBehaviour
{
    public Transform door;
    public Vector3 openOffset = new Vector3(0f, 6f, 0f);
    public float speed = 3f;
    public Renderer indicator;
    private Vector3 closedPosition;
    private int weights;

    void Start() { if (door != null) closedPosition = door.position; }
    void Update()
    {
        if (door == null) return;
        Vector3 target = closedPosition + (weights > 0 ? openOffset : Vector3.zero);
        door.position = Vector3.MoveTowards(door.position, target, speed * Time.deltaTime);
    }
    void OnTriggerEnter(Collider other) { if (other.attachedRigidbody != null) weights++; }
    void OnTriggerExit(Collider other) { if (other.attachedRigidbody != null) weights = Mathf.Max(0, weights - 1); }
}