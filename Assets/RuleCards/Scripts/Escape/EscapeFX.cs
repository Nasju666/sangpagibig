using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime VFX + procedural SFX for the escape stage. Everything is generated in code (particles,
/// soft-dot texture, synthesized sounds) so no extra art assets are needed. Call <see cref="Init"/> once.
/// </summary>
public static class EscapeFX
{
    private static Material particleMaterial;
    private static Texture2D softDot;
    private static AudioSource audio;
    private static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    private static Transform root;

    public static void Init(Material particleTemplate, Transform owner)
    {
        root = owner;
        if (softDot == null) softDot = MakeSoftDot(64);
        Material source = particleTemplate;
        if (source == null)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (s != null) source = new Material(s);
        }
        if (source != null)
        {
            particleMaterial = new Material(source) { name = "EscapeFX_Particles" };
            ConfigureAdditive(particleMaterial);
            if (particleMaterial.HasProperty("_BaseMap")) particleMaterial.SetTexture("_BaseMap", softDot);
            if (particleMaterial.HasProperty("_MainTex")) particleMaterial.SetTexture("_MainTex", softDot);
            if (particleMaterial.HasProperty("_BaseColor")) particleMaterial.SetColor("_BaseColor", Color.white);
        }
        if (audio == null || audio.gameObject == null)
        {
            audio = owner.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
        }
    }

    // ------------------------------------------------------------------ particles
    private static ParticleSystem MakeSystem(string name, Vector3 position, out ParticleSystemRenderer renderer)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        if (root != null) go.transform.SetParent(root, true);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = particleMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        var main = ps.main;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        return ps;
    }

    /// <summary>One-shot spherical burst.</summary>
    public static void Burst(Vector3 position, Color color, int count, float speed, float size, float life, float gravity = 0f)
    {
        if (particleMaterial == null) return;
        ParticleSystemRenderer r;
        var ps = MakeSystem("FX_Burst", position, out r);
        var main = ps.main;
        main.duration = .2f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * .5f, life);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * .3f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * .5f, size);
        main.startColor = color;
        main.gravityModifier = gravity;
        main.maxParticles = count + 4;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = .2f;
        FadeOut(ps);
        ps.Play();
        Object.Destroy(ps.gameObject, main.duration + life + .2f);
    }

    /// <summary>Flat expanding ring (gravity flips, landings). Normal = the ring's facing axis.</summary>
    public static void Ring(Vector3 position, Vector3 normal, Color color, float radius, int count = 48)
    {
        if (particleMaterial == null) return;
        ParticleSystemRenderer r;
        var ps = MakeSystem("FX_Ring", position, out r);
        ps.transform.rotation = Quaternion.FromToRotation(Vector3.forward, normal);
        var main = ps.main;
        main.duration = .1f;
        main.loop = false;
        main.startLifetime = .55f;
        main.startSpeed = radius / .5f;
        main.startSize = .22f;
        main.startColor = color;
        main.maxParticles = count + 4;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = .3f;
        shape.radiusThickness = 0f;
        shape.arcMode = ParticleSystemShapeMultiModeValue.Loop;
        FadeOut(ps);
        ps.Play();
        Object.Destroy(ps.gameObject, 1.2f);
    }

    /// <summary>A looping emitter you own (collapse front, tether trail, ambient motes).</summary>
    public static ParticleSystem Emitter(string name, Transform parent, Color a, Color b, float rate, float life, float speed, float size, ParticleSystemShapeType shapeType, Vector3 shapeScale)
    {
        if (particleMaterial == null) return null;
        ParticleSystemRenderer r;
        var ps = MakeSystem(name, parent != null ? parent.position : Vector3.zero, out r);
        if (parent != null) ps.transform.SetParent(parent, true);
        ps.transform.localRotation = Quaternion.identity;
        var main = ps.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * .6f, life);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * .4f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * .4f, size);
        main.startColor = new ParticleSystem.MinMaxGradient(a, b);
        main.maxParticles = Mathf.CeilToInt(rate * life) + 16;
        var emission = ps.emission;
        emission.rateOverTime = rate;
        var shape = ps.shape;
        shape.shapeType = shapeType;
        shape.scale = shapeScale;
        FadeOut(ps);
        ps.Play();
        return ps;
    }

    private static void FadeOut(ParticleSystem ps)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .12f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, .2f));
    }

    // ------------------------------------------------------------------ chunks (crumbling floors)
    public static void Chunks(Bounds area, Material material, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "FX_Chunk";
            var c = go.GetComponent<Collider>(); if (c != null) { c.enabled = false; Object.Destroy(c); }
            if (root != null) go.transform.SetParent(root, true);
            go.transform.position = new Vector3(Random.Range(area.min.x, area.max.x), area.max.y - .1f, Random.Range(area.min.z, area.max.z));
            go.transform.rotation = Random.rotation;
            go.transform.localScale = Vector3.one * Random.Range(.25f, .8f);
            var r = go.GetComponent<Renderer>();
            if (material != null) r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<EscapeChunk>().velocity = new Vector3(Random.Range(-2f, 2f), Random.Range(1f, 4f), Random.Range(-2f, 2f));
        }
    }

    // ------------------------------------------------------------------ audio
    public static void Sfx(string name, float volume = .5f, float pitch = 1f)
    {
        if (audio == null) return;
        AudioClip clip;
        if (!clips.TryGetValue(name, out clip) || clip == null) { clip = Synth(name); clips[name] = clip; }
        if (clip == null) return;
        audio.pitch = pitch;
        audio.PlayOneShot(clip, volume);
    }

    private static AudioClip Synth(string name)
    {
        const int rate = 22050;
        float length;
        switch (name)
        {
            case "link": length = .55f; break;
            case "flip": length = .7f; break;
            case "air": length = .45f; break;
            case "airfade": length = .35f; break;
            case "land": length = .3f; break;
            case "crumble": length = .9f; break;
            case "respawn": length = .8f; break;
            case "checkpoint": length = 1.1f; break;
            case "deny": length = .22f; break;
            default: return null;
        }
        var samples = new float[(int)(rate * length)];
        var rnd = new System.Random(name.GetHashCode());
        float noise = 0f, phase = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)rate, k = t / length;
            float white = (float)rnd.NextDouble() * 2f - 1f;
            float v = 0f;
            switch (name)
            {
                case "link": // rising electric zip
                    phase += (220f + 900f * k * k) / rate;
                    v = (Mathf.Sin(phase * Mathf.PI * 2f) * .5f + Mathf.Sign(Mathf.Sin(phase * Mathf.PI * 4f)) * .12f) * (1f - k) * Mathf.Clamp01(t / .01f);
                    break;
                case "flip": // filtered whoosh sweeping down then up
                    noise = Mathf.Lerp(noise, white, .05f + .25f * Mathf.Abs(Mathf.Sin(k * Mathf.PI)));
                    phase += (180f - 90f * Mathf.Sin(k * Mathf.PI)) / rate;
                    v = (noise * 1.6f + Mathf.Sin(phase * Mathf.PI * 2f) * .35f) * Mathf.Sin(k * Mathf.PI);
                    break;
                case "air": // crystalline chime
                    v = (Mathf.Sin(t * 1318.5f * Mathf.PI * 2f) + .6f * Mathf.Sin(t * 1975.5f * Mathf.PI * 2f) + .3f * Mathf.Sin(t * 2637f * Mathf.PI * 2f)) * Mathf.Exp(-t * 9f) * .35f;
                    break;
                case "airfade":
                    v = Mathf.Sin(t * (900f - 500f * k) * Mathf.PI * 2f) * (1f - k) * .25f;
                    break;
                case "land":
                    noise = Mathf.Lerp(noise, white, .08f);
                    v = (noise * 1.5f + Mathf.Sin(t * 70f * Mathf.PI * 2f) * .6f) * Mathf.Exp(-t * 16f);
                    break;
                case "crumble":
                    noise = Mathf.Lerp(noise, white, .12f);
                    v = noise * 2f * Mathf.Exp(-t * 3f) * (Random01(rnd) > .985f ? 2.5f : 1f) * .6f;
                    break;
                case "respawn": // reversed whoosh into a low tone
                    noise = Mathf.Lerp(noise, white, .04f + .3f * k);
                    v = (noise * 1.2f * k + Mathf.Sin(t * 110f * Mathf.PI * 2f) * .3f) * Mathf.Sin(k * Mathf.PI);
                    break;
                case "checkpoint":
                    float n1 = Mathf.Sin(t * 659.25f * Mathf.PI * 2f) * Mathf.Exp(-t * 4f);
                    float t2 = Mathf.Max(0f, t - .12f);
                    float n2 = t > .12f ? Mathf.Sin(t2 * 987.77f * Mathf.PI * 2f) * Mathf.Exp(-t2 * 4f) : 0f;
                    v = (n1 + n2) * .3f;
                    break;
                case "deny":
                    v = Mathf.Sign(Mathf.Sin(t * 140f * Mathf.PI * 2f)) * .15f * (1f - k);
                    break;
            }
            samples[i] = Mathf.Clamp(v, -1f, 1f) * Mathf.Clamp01((length - t) / .03f);
        }
        var clip = AudioClip.Create("EscapeSfx_" + name, samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static float Random01(System.Random r) { return (float)r.NextDouble(); }

    /// <summary>URP Particles/Unlit as transparent additive glow (works for runtime-created materials too).</summary>
    public static void ConfigureAdditive(Material m)
    {
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 2f);
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private static Texture2D MakeSoftDot(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "EscapeFX_SoftDot", wrapMode = TextureWrapMode.Clamp };
        var px = new Color[size * size];
        float half = (size - 1) * .5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                float a = Mathf.Clamp01(1f - d);
                px[y * size + x] = new Color(1f, 1f, 1f, a * a);
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }
}

/// <summary>A falling, tumbling fragment with no collider. Destroys itself.</summary>
public sealed class EscapeChunk : MonoBehaviour
{
    public Vector3 velocity;
    private Vector3 spin;
    private float age;

    private void Start() { spin = Random.insideUnitSphere * 300f; }

    private void Update()
    {
        age += Time.deltaTime;
        velocity += Vector3.down * 14f * Time.deltaTime;
        transform.position += velocity * Time.deltaTime;
        transform.Rotate(spin * Time.deltaTime, Space.World);
        if (age > 2.2f) transform.localScale = Vector3.MoveTowards(transform.localScale, Vector3.zero, Time.deltaTime * 2f);
        if (age > 3f) Destroy(gameObject);
    }
}
