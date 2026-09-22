using UnityEngine;

public class LavaFlow : MonoBehaviour
{
    [Header("Flow")]
    public Vector2 flowSpeed = new Vector2(0.05f, 0.02f);

    [Header("Glow")]
    public Gradient glowGradient;
    public float glowIntensity = 5f;
    public float pulseSpeed = 2f;

    [Header("Heat")]
    [Range(0f, 1f)]
    public float heat = 1f; // 0 = cooling, 1 = very hot
    public Color coldColor = new Color(0.4f, 0f, 0f);
    public Color hotColor = Color.yellow;

    [Header("Lava Light")]
    public Light lavaLight;
    public float baseLightIntensity = 2f;
    public float flickerAmount = 0.4f;
    public float flickerSpeed = 5f;

    [Header("Performance")]
    public bool optimizeForMobile = false;

    private Material mat;

    void Start()
    {
        mat = GetComponent<Renderer>().material;

        // Optional mobile tuning
        if (optimizeForMobile)
        {
            glowIntensity *= 0.6f;
            pulseSpeed *= 0.7f;
            flickerAmount *= 0.5f;
        }
    }

    void Update()
    {
        // 1. Texture flow
        Vector2 offset = flowSpeed * Time.time;
        mat.mainTextureOffset = offset;

        // 2. Glow pulse
        float pulse = Mathf.Sin(Time.time * pulseSpeed) * 0.5f + 1f;

        // 3. Color over time (gradient)
        Color gradientColor =
            glowGradient.Evaluate(Mathf.PingPong(Time.time * 0.1f, 1f));

        // 4. Heat-based color
        Color heatColor = Color.Lerp(coldColor, hotColor, heat);

        // 5. Final emission
        Color finalGlow = gradientColor * heatColor;
        mat.SetColor("_EmissionColor", finalGlow * pulse * glowIntensity);

        // 6. Light flicker
        if (lavaLight != null)
        {
            float flicker =
                Mathf.Sin(Time.time * flickerSpeed) * flickerAmount;

            lavaLight.intensity =
                baseLightIntensity * heat + flicker;
        }
    }
}
