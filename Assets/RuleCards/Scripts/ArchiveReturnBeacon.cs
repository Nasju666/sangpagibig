using UnityEngine;

public class ArchiveReturnBeacon : MonoBehaviour
{
    public StageReturn portal;
    public Material lockedMaterial,readyMaterial;
    public Renderer[] cueRenderers;
    public TextMesh label;
    public Light cueLight;
    public bool IsReady { get; private set; }
    private GameObject generatedCue;
    private bool ownsMaterials;
    private bool stateInitialized;

    void Start()
    {
        EnsureCue(portal != null ? portal : GetComponent<StageReturn>());
    }

    public void EnsureCue(StageReturn source)
    {
        if (portal == null) portal = source;
        if (portal == null) return;
        if (cueRenderers == null || cueRenderers.Length == 0) BuildRuntimeCue();
        RefreshState();
    }

    private void BuildRuntimeCue()
    {
        Renderer surface = portal.GetComponentInChildren<Renderer>();
        if (surface == null || surface.sharedMaterial == null) return;
        Bounds bounds = surface.bounds;
        generatedCue = new GameObject("Return_ArchiveColorCue");
        generatedCue.transform.SetParent(transform, false);
        Vector3 scale = transform.lossyScale;
        generatedCue.transform.localScale = new Vector3(
            1f / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
            1f / Mathf.Max(0.001f, Mathf.Abs(scale.y)),
            1f / Mathf.Max(0.001f, Mathf.Abs(scale.z)));
        lockedMaterial = CreateCueMaterial(surface.sharedMaterial, "Return_RuntimeAmber", new Color(1f, 0.58f, 0.12f));
        readyMaterial = CreateCueMaterial(surface.sharedMaterial, "Return_RuntimeGreen", new Color(0.15f, 1f, 0.42f));
        ownsMaterials = true;
        var renderers = new System.Collections.Generic.List<Renderer>();
        float frontZ = bounds.min.z - 0.06f;
        float halfWidth = Mathf.Max(1f, bounds.extents.x);
        float height = Mathf.Max(2.5f, bounds.size.y);
        float centerY = bounds.min.y + height * 0.5f;
        for (int side = -1; side <= 1; side += 2)
            renderers.Add(CreateBar("Return_Trim_" + side,
                new Vector3(bounds.center.x + side * (halfWidth + 0.1f), centerY, frontZ),
                new Vector3(0.09f, height, 0.09f), 0f));
        renderers.Add(CreateBar("Return_TrimCrown",
            new Vector3(bounds.center.x, bounds.min.y + height + 0.08f, frontZ),
            new Vector3(halfWidth * 2f + 0.25f, 0.09f, 0.09f), 0f));
        // Arrows only on actual floor, not over an empty chasm.
        for (int row = 0; row < 4; row++)
        {
            float z = bounds.min.z - 5.5f + row * 1.25f;
            RaycastHit hit;
            if (!Physics.Raycast(new Vector3(bounds.center.x, bounds.min.y + 7f, z),
                Vector3.down, out hit, 14f, ~0, QueryTriggerInteraction.Ignore)) continue;
            for (int side = -1; side <= 1; side += 2)
                renderers.Add(CreateBar("Return_Arrow_" + row + "_" + side,
                    new Vector3(bounds.center.x + side * 0.25f, hit.point.y + 0.025f, z),
                    new Vector3(0.065f, 0.025f, 0.75f), side < 0 ? 45f : -45f));
        }
        cueRenderers = renderers.ToArray();
        if (label == null)
        {
            foreach (var text in UnityEngine.Object.FindObjectsOfType<TextMesh>())
                if (text.gameObject.scene == gameObject.scene && text.text.Contains("RETURN TO THE ARCHIVE")
                    && Vector3.Distance(text.transform.position, bounds.center) < 9f) { label = text; break; }
        }
        if (label == null)
        {
            var textObject = new GameObject("Return_ArchiveStatusLabel");
            textObject.transform.SetParent(generatedCue.transform, false);
            textObject.transform.position = new Vector3(bounds.center.x, bounds.min.y + height + 0.7f, frontZ - 0.05f);
            label = textObject.AddComponent<TextMesh>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 64;
            label.characterSize = 0.08f;
        }
        cueLight = new GameObject("Return_ArchiveBeaconLight").AddComponent<Light>();
        cueLight.transform.SetParent(generatedCue.transform, false);
        cueLight.transform.position = new Vector3(bounds.center.x, centerY, frontZ - 1f);
        cueLight.type = LightType.Point;
        cueLight.range = 8f;
        cueLight.intensity = 2f;
        cueLight.shadows = LightShadows.None;
    }

    private Material CreateCueMaterial(Material template, string materialName, Color color)
    {
        var material = new Material(template) { name = materialName };
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", Texture2D.whiteTexture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", Texture2D.whiteTexture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(color.r * 0.65f, color.g * 0.65f, color.b * 0.65f, 1f));
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * 1.4f);
        material.DisableKeyword("_NORMALMAP");
        material.EnableKeyword("_EMISSION");
        return material;
    }

    private Renderer CreateBar(string objectName, Vector3 position, Vector3 size, float yaw)
    {
        var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bar.name = objectName;
        var collider = bar.GetComponent<Collider>();
        collider.enabled = false;
        Destroy(collider);
        bar.transform.SetParent(generatedCue.transform, false);
        bar.transform.position = position;
        bar.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        bar.transform.localScale = size;
        var renderer = bar.GetComponent<Renderer>();
        renderer.sharedMaterial = lockedMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    void OnDestroy()
    {
        if (!ownsMaterials) return;
        if (lockedMaterial != null) Destroy(lockedMaterial);
        if (readyMaterial != null) Destroy(readyMaterial);
    }
    void Update(){RefreshState();}
public void RefreshState()
    {
        if (portal == null) return;
        bool ready = RuleCardsProgress.HasCard(portal.requiredCard);
        if (stateInitialized && ready == IsReady) return;
        IsReady = ready;
        stateInitialized = true;
        var material = IsReady ? readyMaterial : lockedMaterial;
        if (material != null && cueRenderers != null)
            foreach (var r in cueRenderers) if (r != null) r.sharedMaterial = material;
        Color color = IsReady ? new Color(.15f, 1f, .42f) : new Color(1f, .58f, .12f);
        if (label != null)
        {
            label.color = color;
            label.text = IsReady ? "RETURN TO THE ARCHIVE\nREADY • PRESS E"
                : "RETURN TO THE ARCHIVE\nRECOVER THE RULE CARD";
        }
        if (cueLight != null) cueLight.color = color;
    }
}