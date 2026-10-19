using UnityEngine;

/// <summary>
/// Animated torch / brazier fire: a layered particle flame that sways in a drifting "draught",
/// rising embers, and a flickering point light. Built at runtime so the prefab stays tiny.
/// </summary>
public sealed class DecorFlame : MonoBehaviour
{
    public Material particleMaterial;
    public Color coreColor = new Color(1f, .93f, .7f);
    public Color midColor = new Color(1f, .55f, .18f);
    public Color tipColor = new Color(.75f, .16f, .05f);
    public Color lightColor = new Color(1f, .62f, .32f);
    public float size = 1f;
    public float lightIntensity = 1.7f;
    public float lightRange = 7f;
    [Tooltip("How far the flame leans in the draught.")]
    public float swayStrength = .55f;
    public bool castLight = true;

    private ParticleSystem flame, core, embers;
    private Light glow;
    private float seed;

    private void Start()
    {
        if (particleMaterial == null) particleMaterial = Resources.Load<Material>("RuleCardsParticle");
        seed = Mathf.Repeat(transform.position.x * 3.17f + transform.position.z * 7.31f, 100f);
        flame = Layer("Flame", midColor, tipColor, .55f * size, .9f * size, .42f, 26f, .7f);
        core = Layer("Core", coreColor, midColor, .3f * size, .55f * size, .3f, 22f, .45f);
        embers = Embers();
        if (castLight)
        {
            var go = new GameObject("FlameLight");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * .25f * size;
            glow = go.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = lightColor;
            glow.range = lightRange;
            glow.intensity = lightIntensity;
            glow.shadows = LightShadows.None;
            glow.renderMode = LightRenderMode.Auto;
        }
    }

    private ParticleSystem Layer(string name, Color a, Color b, float minSize, float maxSize, float life, float rate, float speed)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * .7f, life);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * .6f * size, speed * size);
        main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 60;
        var emission = ps.emission;
        emission.rateOverTime = rate;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 6f;
        shape.radius = .05f * size;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, .55f), new Keyframe(.25f, 1f), new Keyframe(1f, .1f)));
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, .7f), new GradientColorKey(b * .6f, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(.9f, .12f), new GradientAlphaKey(.55f, .6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = .35f * size;
        noise.frequency = 1.6f;
        noise.scrollSpeed = 1.1f;
        noise.damping = true;
        var force = ps.forceOverLifetime;
        force.enabled = true;
        force.space = ParticleSystemSimulationSpace.World;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = particleMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.sortingFudge = name == "Core" ? -1f : 0f;
        ps.Play();
        return ps;
    }

    private ParticleSystem Embers()
    {
        var go = new GameObject("Embers");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.8f, 1.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(.5f * size, 1.2f * size);
        main.startSize = new ParticleSystem.MinMaxCurve(.02f * size, .045f * size);
        main.startColor = new ParticleSystem.MinMaxGradient(coreColor, midColor);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 30;
        var emission = ps.emission;
        emission.rateOverTime = 3.5f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 18f;
        shape.radius = .06f * size;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = .6f;
        noise.frequency = 2f;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, .6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = particleMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
        return ps;
    }

    private void Update()
    {
        float t = Time.time;
        // Slow wandering draught + quick gusts: leans the whole flame and makes it lick sideways.
        float wx = (Mathf.PerlinNoise(seed, t * .45f) - .5f) * 2f + Mathf.Sin(t * 2.3f + seed) * .25f;
        float wz = (Mathf.PerlinNoise(seed + 17f, t * .4f) - .5f) * 2f;
        Vector3 wind = new Vector3(wx, 0f, wz) * swayStrength;
        SetWind(flame, wind);
        SetWind(core, wind * .6f);
        SetWind(embers, wind * 1.4f);
        if (glow != null)
        {
            float n = Mathf.PerlinNoise(seed + 5f, t * 7f);
            float slow = Mathf.PerlinNoise(seed + 9f, t * 1.3f);
            glow.intensity = lightIntensity * (.78f + .3f * n + .12f * slow);
            glow.transform.localPosition = new Vector3(wx * .05f, .25f * size, wz * .05f);
        }
    }

    private static void SetWind(ParticleSystem ps, Vector3 wind)
    {
        if (ps == null) return;
        var force = ps.forceOverLifetime;
        force.x = wind.x;
        force.y = 0f;
        force.z = wind.z;
    }
}
