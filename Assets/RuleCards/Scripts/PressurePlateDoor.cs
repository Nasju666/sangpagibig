using UnityEngine;

public class PressurePlateDoor : MonoBehaviour
{
    public Transform door;
    public Vector3 openOffset = new Vector3(0f, 6f, 0f);
    public float speed = 3f;
    
    public Rigidbody requiredWeight;
public Renderer indicator;
    private Vector3 closedPosition;
    
    private Collider plateZone;
    private Collider weightCollider;
private int weights;

void Start()
    {
        if (door != null) closedPosition = door.position;
        plateZone = GetComponent<Collider>();
        if (requiredWeight != null) weightCollider = requiredWeight.GetComponent<Collider>();
    }
void Update()
    {
        if (door == null) return;
        if (plateZone == null) plateZone = GetComponent<Collider>();
        if (weightCollider == null && requiredWeight != null)
            weightCollider = requiredWeight.GetComponent<Collider>();

        bool occupied = requiredWeight != null
                        && requiredWeight.gameObject.activeInHierarchy
                        && weightCollider != null
                        && plateZone != null
                        && plateZone.bounds.Intersects(weightCollider.bounds);

        Vector3 target = closedPosition + (occupied ? openOffset : Vector3.zero);
        door.position = Vector3.MoveTowards(door.position, target, speed * Time.deltaTime);

        if (indicator != null)
        {
            Color color = occupied ? new Color(0f, 1f, 1f) : new Color(0.25f, 0.08f, 0.05f);
            if (indicator.material.HasProperty("_BaseColor")) indicator.material.SetColor("_BaseColor", color);
            if (indicator.material.HasProperty("_EmissionColor")) indicator.material.SetColor("_EmissionColor", color * 2f);
        }
    }
void OnTriggerEnter(Collider other)
    {
        Rigidbody enteredBody = other.attachedRigidbody;
        if (enteredBody == null) return;
        if (requiredWeight != null && enteredBody != requiredWeight) return;
        weights++;
    }
void OnTriggerExit(Collider other)
    {
        Rigidbody exitedBody = other.attachedRigidbody;
        if (exitedBody == null) return;
        if (requiredWeight != null && exitedBody != requiredWeight) return;
        weights = Mathf.Max(0, weights - 1);
    }
}