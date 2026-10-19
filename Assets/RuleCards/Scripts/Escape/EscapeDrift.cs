using UnityEngine;

/// <summary>Slow tumble + bob for floating set-dressing debris (no colliders, purely visual).</summary>
public sealed class EscapeDrift : MonoBehaviour
{
    public Vector3 spin = new Vector3(4f, 9f, 3f);
    public float bobHeight = .4f;
    public float bobSpeed = .5f;
    private Vector3 origin;
    private float phase;

    private void Start()
    {
        origin = transform.position;
        phase = (origin.x * 12.9898f + origin.z * 78.233f) % 6.283f;
    }

    private void Update()
    {
        transform.Rotate(spin * Time.deltaTime, Space.World);
        transform.position = origin + Vector3.up * (Mathf.Sin(Time.time * bobSpeed + phase) * bobHeight);
    }
}
