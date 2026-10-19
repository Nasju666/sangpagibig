using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The literal collapse: every box-shaped EscapeCrumble floor/ceiling is cut into ~2 m tiles at
/// runtime. Tiles just ahead of the collapse front glow red and tremble, then break loose and fall
/// once the front passes them. Edge trims are cut per tile so they fall with their piece.
/// Owned and driven by <see cref="EscapeStage"/>; tiles are ordered along the route for cheap updates.
/// </summary>
public sealed class CollapseFloor
{
    private sealed class Tile
    {
        public Transform transform;
        public Renderer renderer;
        public Collider collider;
        public Renderer[] trims;
        public Vector3 position;
        public Quaternion rotation;
        public float s;               // route distance of the tile centre
        public int state;             // 0 intact, 1 falling, 2 gone
        public float timer;
        public Vector3 velocity, spin;
        public Color baseColor;
    }

    public float warnDistance = 7f;
    public Color warnColor = new Color(.9f, .12f, .25f);

    private readonly List<Tile> tiles = new List<Tile>();
    private readonly List<Tile> falling = new List<Tile>();
    private int nextToFall;
    private MaterialPropertyBlock block;
    private float nextBurst, nextSound;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public int Count { get { return tiles.Count; } }

    /// <summary>Cuts every tileable crumble piece. Returns the pieces that could not be tiled (cylinders etc).</summary>
    public List<EscapeCrumble> Build(EscapeCrumble[] pieces, float tileSize, System.Func<Vector3, float> project, Transform parent)
    {
        block = new MaterialPropertyBlock();
        var leftovers = new List<EscapeCrumble>();
        foreach (EscapeCrumble piece in pieces)
        {
            if (piece == null) continue;
            if (!Tileable(piece.gameObject)) { leftovers.Add(piece); continue; }
            Cut(piece.gameObject, tileSize, project, parent);
            Object.Destroy(piece);
        }
        tiles.Sort((a, b) => a.s.CompareTo(b.s));
        nextToFall = 0;
        return leftovers;
    }

    private static bool Tileable(GameObject go)
    {
        var box = go.GetComponent<BoxCollider>();
        var mf = go.GetComponent<MeshFilter>();
        return box != null && mf != null && mf.sharedMesh != null && mf.sharedMesh.name == "Cube"
            && Quaternion.Angle(go.transform.rotation, Quaternion.identity) < 1f;
    }

    private void Cut(GameObject source, float tileSize, System.Func<Vector3, float> project, Transform parent)
    {
        var col = source.GetComponent<BoxCollider>();
        var ren = source.GetComponent<Renderer>();
        Bounds b = col.bounds;
        Material material = ren != null ? ren.sharedMaterial : null;
        Color baseColor = material != null && material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId) : Color.white;

        // Remember the edge trims (EdgeGlow children) so each tile can carry its own piece of them.
        Material trimMaterial = null;
        bool underside = false;
        var trimChildren = new List<GameObject>();
        foreach (Transform child in source.transform)
        {
            if (child.name != "EdgeGlow") continue;
            var tr = child.GetComponent<Renderer>();
            if (tr != null) trimMaterial = tr.sharedMaterial;
            underside = child.position.y < b.center.y;
            trimChildren.Add(child.gameObject);
        }
        foreach (GameObject g in trimChildren) Object.Destroy(g);
        if (ren != null) ren.enabled = false;
        col.enabled = false;

        int nx = Mathf.Max(1, Mathf.RoundToInt(b.size.x / tileSize));
        int nz = Mathf.Max(1, Mathf.RoundToInt(b.size.z / tileSize));
        float sx = b.size.x / nx, sz = b.size.z / nz;
        for (int ix = 0; ix < nx; ix++)
            for (int iz = 0; iz < nz; iz++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = source.name + "_Tile";
                go.transform.SetParent(parent, false);
                Vector3 center = new Vector3(b.min.x + sx * (ix + .5f), b.center.y, b.min.z + sz * (iz + .5f));
                go.transform.position = center;
                // Hairline gap between tiles so the cracks read, without letting the player catch an edge.
                go.transform.localScale = new Vector3(sx - .02f, b.size.y, sz - .02f);
                var r = go.GetComponent<Renderer>();
                if (material != null) r.sharedMaterial = material;
                var t = new Tile
                {
                    transform = go.transform, renderer = r, collider = go.GetComponent<Collider>(),
                    position = center, rotation = Quaternion.identity, s = project(center), baseColor = baseColor
                };
                if (trimMaterial != null)
                {
                    var strips = new List<Renderer>();
                    float y = underside ? b.min.y - .03f : b.max.y + .03f;
                    const float w = .14f, h = .06f;
                    if (ix == 0) strips.Add(Strip(go.transform, new Vector3(b.min.x + w * .5f, y, center.z), new Vector3(w, h, sz), trimMaterial));
                    if (ix == nx - 1) strips.Add(Strip(go.transform, new Vector3(b.max.x - w * .5f, y, center.z), new Vector3(w, h, sz), trimMaterial));
                    if (iz == 0) strips.Add(Strip(go.transform, new Vector3(center.x, y, b.min.z + w * .5f), new Vector3(sx, h, w), trimMaterial));
                    if (iz == nz - 1) strips.Add(Strip(go.transform, new Vector3(center.x, y, b.max.z - w * .5f), new Vector3(sx, h, w), trimMaterial));
                    t.trims = strips.ToArray();
                }
                else t.trims = new Renderer[0];
                tiles.Add(t);
            }
    }

    private static Renderer Strip(Transform tile, Vector3 position, Vector3 size, Material material)
    {
        var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        strip.name = "EdgeGlow";
        var c = strip.GetComponent<Collider>(); c.enabled = false; Object.Destroy(c);
        strip.transform.position = position;
        strip.transform.localScale = size;
        strip.transform.SetParent(tile, true);
        var r = strip.GetComponent<Renderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return r;
    }

    /// <summary>Advance the collapse to route distance <paramref name="front"/>.</summary>
    public void Tick(float front, float dt, Vector3 listener)
    {
        // Drop everything the front has passed.
        while (nextToFall < tiles.Count && tiles[nextToFall].s < front)
        {
            Tile t = tiles[nextToFall++];
            if (t.state != 0) continue;
            Drop(t, listener);
        }

        // Warn the band just ahead of the front: red tint + tremble growing as it gets closer.
        for (int i = nextToFall; i < tiles.Count; i++)
        {
            Tile t = tiles[i];
            float ahead = t.s - front;
            if (ahead > warnDistance) break;
            if (t.state != 0) continue;
            float k = 1f - Mathf.Clamp01(ahead / warnDistance);
            Tint(t, Color.Lerp(t.baseColor, warnColor, k * k));
            t.transform.position = t.position + Random.insideUnitSphere * (.05f * k * k);
        }

        for (int i = falling.Count - 1; i >= 0; i--)
        {
            Tile t = falling[i];
            t.timer += dt;
            t.velocity += Vector3.down * 18f * dt;
            t.transform.position += t.velocity * dt;
            t.transform.Rotate(t.spin * dt, Space.World);
            if (t.timer > 2.4f)
            {
                SetVisible(t, false);
                t.state = 2;
                falling.RemoveAt(i);
            }
        }
    }

    private void Drop(Tile t, Vector3 listener)
    {
        t.state = 1;
        t.timer = 0f;
        t.collider.enabled = false;
        t.velocity = new Vector3(Random.Range(-.6f, .6f), Random.Range(-1.5f, .5f), Random.Range(-.6f, .6f));
        t.spin = new Vector3(Random.Range(-90f, 90f), Random.Range(-60f, 60f), Random.Range(-90f, 90f));
        Tint(t, warnColor);
        falling.Add(t);
        float distance = Vector3.Distance(listener, t.position);
        if (distance < 40f && Time.time >= nextBurst)
        {
            nextBurst = Time.time + .07f;
            EscapeFX.Burst(t.position + Vector3.up * .5f, new Color(1f, .3f, .5f, .8f), 10, 2.5f, .45f, .9f, .3f);
        }
        if (distance < 30f && Time.time >= nextSound)
        {
            nextSound = Time.time + .22f;
            EscapeFX.Sfx("crumble", Mathf.Lerp(.4f, .08f, distance / 30f), Random.Range(.8f, 1.15f));
        }
    }

    /// <summary>Respawn: rebuild every tile ahead of the new front; anything behind it stays gone.</summary>
    public void ResetTo(float front)
    {
        falling.Clear();
        nextToFall = 0;
        for (int i = 0; i < tiles.Count; i++)
        {
            Tile t = tiles[i];
            if (t.s < front)
            {
                t.state = 2;
                t.collider.enabled = false;
                SetVisible(t, false);
                nextToFall = i + 1;
            }
            else
            {
                t.state = 0;
                t.timer = 0f;
                t.transform.SetPositionAndRotation(t.position, t.rotation);
                t.collider.enabled = true;
                SetVisible(t, true);
                Tint(t, t.baseColor);
            }
        }
    }

    private void SetVisible(Tile t, bool visible)
    {
        t.renderer.enabled = visible;
        foreach (Renderer r in t.trims) if (r != null) r.enabled = visible;
    }

    private void Tint(Tile t, Color c)
    {
        t.renderer.GetPropertyBlock(block);
        block.SetColor(BaseColorId, c);
        block.SetColor(ColorId, c);
        t.renderer.SetPropertyBlock(block);
    }
}
