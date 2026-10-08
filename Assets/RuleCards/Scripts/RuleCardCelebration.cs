using UnityEngine;

/// <summary>Short, non-blocking feedback shared by every Rule Card pickup.</summary>
public sealed class RuleCardCelebration : MonoBehaviour
{
    private static RuleCardCelebration active;
    private const float Lifetime = 3.6f;
    private float elapsed;
    private string cardName;
    private AudioClip chime;
    private Material particleMaterial;
    private Mesh confettiMesh;
    private Light burstLight;
    private GUIStyle titleStyle;
    private GUIStyle nameStyle;
    private readonly Color[] palette = {
        new Color(0.15f, 1f, 1f), new Color(1f, 0.8f, 0.2f),
        new Color(0.95f, 0.3f, 1f), new Color(0.35f, 1f, 0.6f)
    };

    public static void Play(Vector3 position, string displayName)
    {
        if (!Application.isPlaying) return;
        if (active != null) Destroy(active.gameObject);
        var root = new GameObject("RuleCard_PickupCelebration");
        active = root.AddComponent<RuleCardCelebration>();
        active.Build(position, displayName);
    }

    private void Build(Vector3 position, string displayName)
    {
        cardName = string.IsNullOrEmpty(displayName) ? "Rule Card" : displayName;
        transform.position = position;
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader != null)
        {
            particleMaterial = new Material(shader) { name = "RuleCard_RuntimeParticles" };
            if (particleMaterial.HasProperty("_BaseColor")) particleMaterial.SetColor("_BaseColor", Color.white);
            if (particleMaterial.HasProperty("_Color")) particleMaterial.SetColor("_Color", Color.white);
            if (particleMaterial.HasProperty("_BaseMap")) particleMaterial.SetTexture("_BaseMap", Texture2D.whiteTexture);
            if (particleMaterial.HasProperty("_MainTex")) particleMaterial.SetTexture("_MainTex", Texture2D.whiteTexture);
            if (particleMaterial.HasProperty("_Cull")) particleMaterial.SetFloat("_Cull", 0f);
            if (particleMaterial.HasProperty("_EmissionColor")) particleMaterial.SetColor("_EmissionColor", new Color(0.3f, 0.5f, 0.5f));
            particleMaterial.EnableKeyword("_EMISSION");
            confettiMesh = new Mesh { name = "RuleCard_ConfettiQuad" };
            confettiMesh.vertices = new[] {
                new Vector3(-0.5f,-0.3f,0f), new Vector3(0.5f,-0.3f,0f),
                new Vector3(0.5f,0.3f,0f), new Vector3(-0.5f,0.3f,0f)
            };
            confettiMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            confettiMesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            confettiMesh.triangles = new[] { 0,1,2,0,2,3,2,1,0,3,2,0 };
            confettiMesh.RecalculateNormals();
            confettiMesh.RecalculateBounds();
            CreateBurst("Archive_Confetti", true, 80);
            CreateBurst("Archive_GlowBurst", false, 36);
        }

        burstLight = new GameObject("Archive_PickupGlow").AddComponent<Light>();
        burstLight.transform.SetParent(transform, false);
        burstLight.color = palette[0];
        burstLight.type = LightType.Point;
        burstLight.range = 4f;
        burstLight.intensity = 2f;
        burstLight.shadows = LightShadows.None;

        chime = CreateChime();
        var sound = gameObject.AddComponent<AudioSource>();
        sound.playOnAwake = false;
        sound.spatialBlend = 0f; // Audible at any pickup/camera distance.
        sound.volume = 0.35f;
        sound.clip = chime;
        sound.Play();
    }

    private void CreateBurst(string objectName, bool confetti, int count)
    {
        var root = new GameObject(objectName);
        root.transform.SetParent(transform, false);
        var particles = root.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.1f;
        main.maxParticles = count;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = confetti ? 2.6f : 0.75f;
        main.startSize = confetti ? 0.12f : 0.07f;
        main.gravityModifier = confetti ? 0.55f : 0f;
        main.startRotation3D = confetti;
        var emission = particles.emission;
        emission.enabled = false;
        var shape = particles.shape;
        shape.enabled = false;
        var rotation = particles.rotationOverLifetime;
        rotation.enabled = confetti;
        rotation.separateAxes = true;
        rotation.x = 2.2f;
        rotation.y = 3.8f;
        rotation.z = 3f;
        var fade = particles.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] {
            new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f)
        }, new[] {
            new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f),
            new GradientAlphaKey(0f, 1f)
        });
        fade.color = gradient;
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = particleMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        if (confetti)
        {
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = confettiMesh;
        }
        else renderer.renderMode = ParticleSystemRenderMode.Billboard;

        // Local random generator does not perturb the game's global Random state.
        var random = new System.Random(confetti ? 731 : 457);
        particles.Play();
        for (int i = 0; i < count; i++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float speed = 1.4f + (float)random.NextDouble() * 2.3f;
            var emit = new ParticleSystem.EmitParams();
            emit.position = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.12f;
            emit.velocity = new Vector3(Mathf.Cos(angle) * speed,
                confetti ? 2.8f + (float)random.NextDouble() * 2f : ((float)random.NextDouble() - 0.5f) * 3f,
                Mathf.Sin(angle) * speed);
            emit.startColor = confetti ? palette[i % palette.Length] : palette[0];
            emit.startSize = confetti ? 0.08f + (float)random.NextDouble() * 0.09f : 0.06f;
            emit.startLifetime = confetti ? 1.7f + (float)random.NextDouble() * 0.9f : 0.65f;
            emit.rotation3D = new Vector3(i * 31f, i * 53f, i * 17f);
            particles.Emit(emit, 1);
        }
    }

    private AudioClip CreateChime()
    {
        const int rate = 22050;
        var samples = new float[(int)(rate * 1.55f)];
        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
        for (int i = 0; i < samples.Length; i++)
        {
            float time = i / (float)rate;
            float sample = 0f;
            for (int note = 0; note < notes.Length; note++)
            {
                float age = time - note * 0.13f;
                if (age < 0f) continue;
                float envelope = Mathf.Clamp01(age / 0.012f) * Mathf.Exp(-age * 5f);
                float phase = age * notes[note] * Mathf.PI * 2f;
                sample += (Mathf.Sin(phase) + 0.18f * Mathf.Sin(phase * 2f)) * envelope * 0.19f;
            }
            samples[i] = sample * Mathf.Clamp01((1.55f - time) / 0.12f);
        }
        var clip = AudioClip.Create("RuleCard_ArchiveChime", samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        if (burstLight != null)
        {
            burstLight.intensity = 2f * Mathf.Clamp01(1f - elapsed / 0.6f);
            if (elapsed >= 0.6f) burstLight.enabled = false;
        }
        if (elapsed >= Lifetime) Destroy(gameObject);
    }

    private void OnGUI()
    {
        if (elapsed >= Lifetime) return;
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label) {
                alignment = TextAnchor.MiddleCenter, fontSize = 22, fontStyle = FontStyle.Bold
            };
            titleStyle.normal.textColor = palette[0];
            nameStyle = new GUIStyle(GUI.skin.label) {
                alignment = TextAnchor.MiddleCenter, fontSize = 18, wordWrap = true
            };
            nameStyle.normal.textColor = Color.white;
        }
        float width, height;
        Matrix4x4 previous = ResponsiveIMGUI.Begin(out width, out height);
        Color oldColor = GUI.color;
        int oldDepth = GUI.depth;
        GUI.depth = -100;
        float alpha = Mathf.Clamp01(elapsed / 0.12f) * Mathf.Clamp01((Lifetime - elapsed) / 0.55f);
        float panelWidth = Mathf.Min(500f, Mathf.Max(1f, width - 24f));
        float y = Mathf.Min(75f, height * 0.17f) - (1f - Mathf.Clamp01(elapsed / 0.22f)) * 12f;
        var rect = new Rect((width - panelWidth) * 0.5f, y, panelWidth, 94f);
        GUI.color = new Color(0.02f, 0.05f, 0.09f, alpha * 0.94f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = new Color(0.15f, 1f, 1f, alpha);
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 3f), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 1f, 1f, alpha);
        titleStyle.fontSize = panelWidth < 320f ? 16 : 22;
        GUI.Label(new Rect(rect.x + 8f, rect.y + 10f, rect.width - 16f, 30f),
            "RULE CARD ARCHIVED", titleStyle);
        GUI.Label(new Rect(rect.x + 8f, rect.y + 42f, rect.width - 16f, 42f), cardName, nameStyle);
        GUI.depth = oldDepth;
        ResponsiveIMGUI.End(previous);
        GUI.color = oldColor;
    }

    private void OnDestroy()
    {
        if (active == this) active = null;
        if (chime != null) Destroy(chime);
        if (particleMaterial != null) Destroy(particleMaterial);
        if (confettiMesh != null) Destroy(confettiMesh);
    }
}
