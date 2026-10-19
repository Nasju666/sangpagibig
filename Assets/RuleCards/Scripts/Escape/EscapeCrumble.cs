using UnityEngine;

/// <summary>
/// A floor/ceiling piece that falls apart once the collapse wall has completely passed it.
/// EscapeStage computes <see cref="SMax"/> (its far edge along the route) and restores it on respawn.
/// </summary>
public sealed class EscapeCrumble : MonoBehaviour
{
    public float SMax { get; set; }
    public bool Crumbled { get { return state != State.Intact; } }

    private enum State { Intact, Shaking, Falling, Gone }
    private State state = State.Intact;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private Collider shape;
    private Renderer surface;
    private Renderer[] trims = new Renderer[0];
    private float timer;
    private Vector3 spin;

    private void Awake()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
        shape = GetComponent<Collider>();
        surface = GetComponent<Renderer>();
        var all = new System.Collections.Generic.List<Renderer>(GetComponentsInChildren<Renderer>(true));
        all.Remove(surface);
        trims = all.ToArray();
        spin = new Vector3(Random.Range(-40f, 40f), Random.Range(-30f, 30f), Random.Range(-40f, 40f));
    }

    public void Crumble()
    {
        if (state != State.Intact) return;
        state = State.Shaking;
        timer = 0f;
        Bounds b = shape != null ? shape.bounds : surface != null ? surface.bounds : new Bounds(transform.position, Vector3.one);
        Camera view = Camera.main;
        bool near = view == null || Vector3.Distance(view.transform.position, b.center) < 45f;
        if (near)
        {
            EscapeFX.Chunks(b, surface != null ? surface.sharedMaterial : null, Mathf.Clamp(Mathf.RoundToInt(b.size.x * b.size.z / 18f), 4, 14));
            EscapeFX.Burst(new Vector3(b.center.x, b.max.y, b.center.z), new Color(1f, .25f, .55f, .8f), 30, 4f, .5f, 1f, .4f);
            EscapeFX.Sfx("crumble", .35f, Random.Range(.85f, 1.1f));
        }
    }

    public void Restore()
    {
        state = State.Intact;
        timer = 0f;
        transform.SetPositionAndRotation(startPosition, startRotation);
        if (shape != null) shape.enabled = true;
        if (surface != null) surface.enabled = true;
        foreach (Renderer r in trims) if (r != null) r.enabled = true;
    }

    private void Update()
    {
        if (state == State.Intact || state == State.Gone) return;
        timer += Time.deltaTime;
        if (state == State.Shaking)
        {
            transform.position = startPosition + Random.insideUnitSphere * .08f;
            if (timer > .45f)
            {
                state = State.Falling;
                timer = 0f;
                if (shape != null) shape.enabled = false;
            }
            return;
        }
        float drop = 9f * timer * timer;
        transform.position = startPosition + Vector3.down * drop;
        transform.rotation = startRotation * Quaternion.Euler(spin * timer);
        if (timer > 1.6f)
        {
            state = State.Gone;
            if (surface != null) surface.enabled = false;
            foreach (Renderer r in trims) if (r != null) r.enabled = false;
        }
    }
}
