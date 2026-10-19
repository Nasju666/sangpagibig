using UnityEngine;

/// <summary>
/// Cheap ambient motion for set dressing: local spin, vertical bob, pendulum sway and an emission /
/// light pulse. Purely visual — decor objects carrying this have no colliders.
/// </summary>
public sealed class DecorMotion : MonoBehaviour
{
    [Header("Spin (degrees/sec, local axes)")]
    public Vector3 spin;
    [Header("Bob")]
    public float bobHeight;
    public float bobSpeed = .5f;
    [Header("Pendulum sway (degrees)")]
    public float swayAngle;
    public float swaySpeed = .6f;
    [Header("Glow pulse")]
    public float pulseAmount;
    public float pulseSpeed = 1.2f;
    public Color emissionColor = Color.white;
    public float emissionIntensity = 1.5f;

    private Vector3 origin;
    private Quaternion baseRotation;
    private float phase;
    private Renderer[] renderers;
    private Light[] lights;
    private float[] lightBase;
    private MaterialPropertyBlock block;
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    private void Start()
    {
        origin = transform.localPosition;
        baseRotation = transform.localRotation;
        Vector3 p = transform.position;
        phase = Mathf.Repeat(p.x * 12.9898f + p.y * 4.1f + p.z * 78.233f, 6.283f);
        if (pulseAmount > 0f)
        {
            renderers = GetComponentsInChildren<Renderer>();
            lights = GetComponentsInChildren<Light>();
            lightBase = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++) lightBase[i] = lights[i].intensity;
            block = new MaterialPropertyBlock();
        }
    }

    private void Update()
    {
        float t = Time.time;
        Quaternion rotation = baseRotation;
        if (spin != Vector3.zero) rotation = rotation * Quaternion.Euler(spin * t);
        if (swayAngle > 0f)
        {
            float a = Mathf.Sin(t * swaySpeed + phase) * swayAngle;
            float b = Mathf.Sin(t * swaySpeed * .73f + phase * 1.7f) * swayAngle * .6f;
            rotation = Quaternion.Euler(a, 0f, b) * rotation;
        }
        transform.localRotation = rotation;
        if (bobHeight > 0f)
            transform.localPosition = origin + Vector3.up * (Mathf.Sin(t * bobSpeed + phase) * bobHeight);

        if (pulseAmount > 0f)
        {
            float k = 1f + Mathf.Sin(t * pulseSpeed + phase) * pulseAmount;
            foreach (Renderer r in renderers)
            {
                if (r == null || r is ParticleSystemRenderer) continue;
                r.GetPropertyBlock(block);
                block.SetColor(EmissionId, emissionColor * (emissionIntensity * k));
                r.SetPropertyBlock(block);
            }
            for (int i = 0; i < lights.Length; i++) if (lights[i] != null) lights[i].intensity = lightBase[i] * k;
        }
    }
}
