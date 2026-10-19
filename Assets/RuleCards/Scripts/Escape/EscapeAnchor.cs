using System.Collections.Generic;
using UnityEngine;

/// <summary>Kinetic Link target. Aim at it and use the Kinetic card to be pulled to <see cref="landingPoint"/>.</summary>
public sealed class EscapeAnchor : MonoBehaviour
{
    public static readonly List<EscapeAnchor> All = new List<EscapeAnchor>();

    [Tooltip("World position the player's body centre is pulled to.")]
    public Vector3 landingPoint;
    public Color color = new Color(1f, .3f, .85f);
    [Tooltip("Max link distance for this anchor (0 = the card's default range).")]
    public float maxRange;

    public bool Highlighted { get; set; }

    private Renderer surface;
    private Material material;
    private Light glow;
    private Vector3 baseScale;
    private Renderer[] modelRenderers;
    private MaterialPropertyBlock block;

    private void Awake()
    {
        surface = GetComponent<Renderer>();
        if (surface != null) material = surface.material;
        baseScale = transform.localScale;
        // Sculpted model children (added by the decorator) pulse with the anchor colour.
        var list = new List<Renderer>();
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
            if (r != surface && !(r is ParticleSystemRenderer)) list.Add(r);
        modelRenderers = list.ToArray();
        block = new MaterialPropertyBlock();
        glow = gameObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.range = 6f;
        glow.color = color;
        glow.shadows = LightShadows.None;
    }

    private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    private void OnDisable() { All.Remove(this); }

    private void Update()
    {
        float pulse = .6f + .4f * Mathf.Sin(Time.time * (Highlighted ? 9f : 3f));
        transform.Rotate(0f, 70f * Time.deltaTime, 0f, Space.World);
        transform.localScale = baseScale * (Highlighted ? 1.25f + .08f * pulse : 1f);
        glow.intensity = Highlighted ? 3f + 2f * pulse : 1.2f + .8f * pulse;
        foreach (Renderer r in modelRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetColor("_EmissionColor", color * ((Highlighted ? 5f : 2.4f) * pulse));
            r.SetPropertyBlock(block);
        }
        if (material != null)
        {
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * (Highlighted ? 4f : 2f) * pulse);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = color;
        Gizmos.DrawLine(transform.position, landingPoint);
        Gizmos.DrawWireSphere(landingPoint, .5f);
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
