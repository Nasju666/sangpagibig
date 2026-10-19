using UnityEngine;

/// <summary>Applies the frame-rate cap / vsync from Settings in every scene (was a fixed 60 FPS).</summary>
public class FrameRateLimiter : MonoBehaviour
{
    void Awake() { RuleCardsSettings.ApplyFrameRate(); }
}
