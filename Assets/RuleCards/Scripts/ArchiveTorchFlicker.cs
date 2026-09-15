using UnityEngine;

public class ArchiveTorchFlicker : MonoBehaviour
{
    public Light torchLight;
    public Transform outerFlame;
    
    public Transform upperFlame;
    public Transform flameTip;
public Transform innerFlame;
    public float baseIntensity = 2.2f;
    public float intensityVariation = 0.25f;
    public float flickerSpeed = 3.5f;
    private Vector3 outerBaseScale;
    
    private Vector3 outerBasePosition;
    
    private Vector3 upperBaseScale;
    private Vector3 upperBasePosition;
    private Vector3 tipBaseScale;
    private Vector3 tipBasePosition;
private Vector3 innerBasePosition;
private Vector3 innerBaseScale;
    private float seed;

void Awake()
    {
        seed = transform.position.sqrMagnitude * 0.137f;
        CacheTransform(outerFlame, out outerBaseScale, out outerBasePosition);
        CacheTransform(innerFlame, out innerBaseScale, out innerBasePosition);
        CacheTransform(upperFlame, out upperBaseScale, out upperBasePosition);
        CacheTransform(flameTip, out tipBaseScale, out tipBasePosition);
    }

private static void CacheTransform(Transform target, out Vector3 baseScale, out Vector3 basePosition)
    {
        if (target != null)
        {
            baseScale = target.localScale;
            basePosition = target.localPosition;
        }
        else
        {
            baseScale = Vector3.one;
            basePosition = Vector3.zero;
        }
    }


void Update()
    {
        float time = Time.time * flickerSpeed;
        float noise = Mathf.PerlinNoise(seed, time);
        float fastNoise = Mathf.PerlinNoise(seed + 11.3f, time * 1.85f);
        float tipNoise = Mathf.PerlinNoise(seed + 27.9f, time * 2.4f);

        if (torchLight != null)
            torchLight.intensity = baseIntensity + (noise - 0.5f) * intensityVariation;

        float pulse = 0.88f + noise * 0.24f;
        float sway = Mathf.Sin(time * 1.7f + seed) * 0.085f + (fastNoise - 0.5f) * 0.07f;
        float lift = (fastNoise - 0.5f) * 0.10f;

        AnimateFlame(outerFlame, outerBaseScale, outerBasePosition, sway, lift, pulse, -240f);
        AnimateFlame(innerFlame, innerBaseScale, innerBasePosition, -sway * 0.65f, -lift * 0.35f, 0.92f + fastNoise * 0.16f, 180f);
        AnimateFlame(upperFlame, upperBaseScale, upperBasePosition, sway * 1.65f, lift * 1.35f, 0.86f + tipNoise * 0.28f, -310f);
        AnimateFlame(flameTip, tipBaseScale, tipBasePosition, -sway * 2.1f, lift * 1.8f, 0.82f + tipNoise * 0.36f, 390f);
    }

private static void AnimateFlame(Transform target, Vector3 baseScale, Vector3 basePosition, float sway, float lift, float pulse, float rotationMultiplier)
    {
        if (target == null) return;
        target.localScale = new Vector3(baseScale.x / pulse, baseScale.y * pulse, baseScale.z / pulse);
        target.localPosition = basePosition + new Vector3(sway, lift, 0f);
        target.localRotation = Quaternion.Euler(0f, 0f, sway * rotationMultiplier);
    }

}