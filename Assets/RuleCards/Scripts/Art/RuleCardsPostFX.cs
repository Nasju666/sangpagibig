using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Global look for every RuleCards scene, installed automatically: a code-built post-processing
/// profile (ACES, bloom, grading, vignette, grain) tuned per scene, post-processing switched on for
/// every camera, and a soft drifting dust field around the camera. No scene edits needed.
/// </summary>
public sealed class RuleCardsPostFX : MonoBehaviour
{
    private struct Look
    {
        public float exposure, contrast, saturation, bloom, threshold, vignette, grain;
        public Color filter, bloomTint, shadows, highlights, dust;
        public float dustRate;
        public TonemappingMode tonemap;
    }

    private static RuleCardsPostFX instance;
    private Volume volume;
    private VolumeProfile profile;
    private ParticleSystem dust;
    private float nextCameraSweep;

    public static bool IsRuleCardsScene(string name)
    {
        return name == "RuleCards_Title" || name == "ArchiveHub" || name.StartsWith("Stage_0") || name == "Repository_Final";
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        bool wanted = IsRuleCardsScene(scene.name);
        if (!wanted)
        {
            if (instance != null) instance.gameObject.SetActive(false);
            return;
        }
        if (instance == null)
        {
            var go = new GameObject("RuleCards_PostFX");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<RuleCardsPostFX>();
            instance.Build();
        }
        instance.gameObject.SetActive(true);
        instance.currentScene = scene.name;
        instance.Apply(LookFor(scene.name), scene.name);
    }

    private static Look LookFor(string scene)
    {
        // Shared base: cool archive shadows, warm highlights, gentle bloom on anything above ~1.
        var l = new Look
        {
            tonemap = TonemappingMode.Neutral,
            exposure = .6f, contrast = 8f, saturation = 8f, bloom = .85f, threshold = .95f,
            vignette = .27f, grain = .12f,
            filter = new Color(.98f, 1f, 1.02f), bloomTint = new Color(.75f, 1f, 1f),
            shadows = new Color(.35f, .58f, .66f), highlights = new Color(1f, .83f, .66f),
            dust = new Color(.55f, .9f, 1f, .35f), dustRate = 22f
        };
        switch (scene)
        {
            case "RuleCards_Title":
                l.exposure = .75f; l.bloom = 1.1f; l.vignette = .34f; l.dustRate = 0f; break;
            case "Stage_01_KineticLink":
                l.bloomTint = new Color(1f, .7f, .95f); l.highlights = new Color(1f, .78f, .86f);
                l.dust = new Color(1f, .7f, .95f, .3f); break;
            case "Stage_02_InvertGravity":
                l.filter = new Color(.97f, .96f, 1.05f); l.bloomTint = new Color(.82f, .78f, 1f);
                l.shadows = new Color(.45f, .45f, .72f); l.dust = new Color(.8f, .75f, 1f, .3f); break;
            case "Stage_03_SolidifyAir":
                l.bloomTint = new Color(.7f, 1f, 1f); l.saturation = 10f; break;
            case "Repository_Final":
                l.highlights = new Color(1f, .8f, .55f); l.bloomTint = new Color(1f, .92f, .75f);
                l.dust = new Color(1f, .88f, .6f, .32f); l.bloom = 1f; break;
            case "Stage_04_Escape":
                // Stage 4 was tuned without tonemapping: keep its colours, just add bloom and a light grade.
                l.tonemap = TonemappingMode.None; l.exposure = .05f; l.contrast = 4f; l.grain = .08f;
                l.filter = new Color(1f, .97f, 1.03f); l.bloomTint = new Color(1f, .75f, .95f);
                l.shadows = new Color(.45f, .4f, .7f); l.vignette = .2f; l.dustRate = 0f; // Escape has its own motes
                break;
        }
        return l;
    }

    /// <summary>A fresh profile with the look for <paramref name="scene"/> (also used by the editor capture tool).</summary>
    public static VolumeProfile CreateProfile(string scene)
    {
        Look l = LookFor(scene);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "RuleCards_" + scene;
        var tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(l.tonemap);
        var bloom = profile.Add<Bloom>(true);
        bloom.scatter.Override(.68f);
        bloom.highQualityFiltering.Override(true);
        bloom.intensity.Override(l.bloom * RuleCardsSettings.Bloom);
        bloom.threshold.Override(l.threshold);
        bloom.tint.Override(l.bloomTint);
        var color = profile.Add<ColorAdjustments>(true);
        color.postExposure.Override(l.exposure + RuleCardsSettings.Brightness);
        color.contrast.Override(l.contrast);
        color.saturation.Override(l.saturation);
        color.colorFilter.Override(l.filter);
        var vignette = profile.Add<Vignette>(true);
        vignette.smoothness.Override(.42f);
        vignette.color.Override(new Color(.01f, .015f, .03f));
        vignette.intensity.Override(l.vignette);
        var split = profile.Add<SplitToning>(true);
        split.balance.Override(-12f);
        split.shadows.Override(l.shadows);
        split.highlights.Override(l.highlights);
        var grain = profile.Add<FilmGrain>(true);
        grain.type.Override(FilmGrainLookup.Thin1);
        grain.response.Override(.85f);
        grain.intensity.Override(l.grain);
        return profile;
    }

    private string currentScene;

    private void OnEnable() { RuleCardsSettings.Changed += Refresh; }
    private void OnDisable() { RuleCardsSettings.Changed -= Refresh; }
    /// <summary>Settings (brightness/bloom) changed: rebuild the profile for the current scene.</summary>
    private void Refresh()
    {
        if (string.IsNullOrEmpty(currentScene) || volume == null) return;
        if (profile != null) Destroy(profile);
        profile = CreateProfile(currentScene);
        volume.sharedProfile = profile;
    }

    private void Build()
    {
        volume = gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 50f;
        volume.weight = 1f;
        dust = BuildDust();
    }

    private void Apply(Look l, string scene)
    {
        if (profile != null) Destroy(profile);
        profile = CreateProfile(scene);
        volume.sharedProfile = profile;

        if (dust != null)
        {
            var main = dust.main;
            main.startColor = l.dust;
            var emission = dust.emission;
            emission.rateOverTime = l.dustRate;
            dust.Clear();
            if (l.dustRate > 0f) { dust.Play(); dust.Simulate(8f, true, true); dust.Play(); }
            else dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        nextCameraSweep = 0f;
    }

    private ParticleSystem BuildDust()
    {
        Material mat = Resources.Load<Material>("RuleCardsParticle");
        if (mat == null) return null;
        var go = new GameObject("AmbientDust");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.duration = 10f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, .12f);
        main.startSize = new ParticleSystem.MinMaxCurve(.025f, .07f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 600;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(28f, 12f, 28f);
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = .18f;
        noise.frequency = .25f;
        noise.scrollSpeed = .1f;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .3f), new GradientAlphaKey(1f, .7f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime >= nextCameraSweep)
        {
            nextCameraSweep = Time.unscaledTime + .5f;
            foreach (Camera cam in Camera.allCameras)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (cam.targetTexture != null) continue; // portraits/minimaps stay clean
                if (data != null && !data.renderPostProcessing) data.renderPostProcessing = true;
            }
        }
        Camera main = Camera.main;
        if (dust != null && main != null) dust.transform.position = main.transform.position;
    }
}
