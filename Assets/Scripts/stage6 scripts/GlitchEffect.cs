using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// GLITCH EFFECT
/// Creates screen glitch effects during blackout transitions.
/// Adds to the DEATH.EXE theme!
/// </summary>
public class GlitchEffect : MonoBehaviour
{
    [Header("Glitch Settings")]
    [Tooltip("Intensity of the glitch effect")]
    [Range(0f, 1f)]
    public float glitchIntensity = 0.5f;
    
    [Tooltip("How often glitches occur")]
    public float glitchFrequency = 0.1f;
    
    [Header("Visual Effects")]
    public bool enableColorShift = true;
    public bool enableScanLines = true;
    public bool enableScreenTear = true;
    
    [Header("UI Overlay")]
    public Canvas glitchCanvas;
    public Image scanLineOverlay;
    public Image colorShiftOverlay;
    
    [Header("Audio")]
    public AudioSource glitchAudio;
    public AudioClip[] glitchSounds;
    
    private bool isGlitching = false;
    private float nextGlitchTime;
    
    void Start()
    {
        if (glitchCanvas == null)
            CreateGlitchCanvas();
    }
    
    void Update()
    {
        // Trigger glitches during blackout transitions
        if (BlackoutController.IsBlackout && !isGlitching)
        {
            if (Time.time >= nextGlitchTime)
            {
                StartCoroutine(DoGlitch());
                nextGlitchTime = Time.time + Random.Range(0.5f, 2f);
            }
        }
    }
    
    void CreateGlitchCanvas()
    {
        GameObject canvasObj = new GameObject("GlitchCanvas");
        canvasObj.transform.SetParent(transform);
        
        glitchCanvas = canvasObj.AddComponent<Canvas>();
        glitchCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        glitchCanvas.sortingOrder = 999;
        
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        // Scan lines overlay
        if (enableScanLines)
        {
            GameObject scanObj = new GameObject("ScanLines");
            scanObj.transform.SetParent(glitchCanvas.transform, false);
            
            RectTransform scanRect = scanObj.AddComponent<RectTransform>();
            scanRect.anchorMin = Vector2.zero;
            scanRect.anchorMax = Vector2.one;
            scanRect.offsetMin = Vector2.zero;
            scanRect.offsetMax = Vector2.zero;
            
            scanLineOverlay = scanObj.AddComponent<Image>();
            scanLineOverlay.color = new Color(0, 0, 0, 0);
        }
        
        // Color shift overlay
        if (enableColorShift)
        {
            GameObject colorObj = new GameObject("ColorShift");
            colorObj.transform.SetParent(glitchCanvas.transform, false);
            
            RectTransform colorRect = colorObj.AddComponent<RectTransform>();
            colorRect.anchorMin = Vector2.zero;
            colorRect.anchorMax = Vector2.one;
            colorRect.offsetMin = Vector2.zero;
            colorRect.offsetMax = Vector2.zero;
            
            colorShiftOverlay = colorObj.AddComponent<Image>();
            colorShiftOverlay.color = new Color(1, 0, 0, 0);
        }
    }
    
    IEnumerator DoGlitch()
    {
        isGlitching = true;
        
        float duration = Random.Range(0.05f, 0.2f);
        float elapsed = 0f;
        
        // Play glitch sound
        if (glitchAudio != null && glitchSounds.Length > 0)
        {
            glitchAudio.PlayOneShot(glitchSounds[Random.Range(0, glitchSounds.Length)]);
        }
        
        while (elapsed < duration)
        {
            // Color shift
            if (colorShiftOverlay != null && enableColorShift)
            {
                Color glitchColor = new Color(
                    Random.value > 0.5f ? 1 : 0,
                    Random.value > 0.5f ? 1 : 0,
                    Random.value > 0.5f ? 1 : 0,
                    glitchIntensity * 0.3f
                );
                colorShiftOverlay.color = glitchColor;
                
                // Random offset
                colorShiftOverlay.rectTransform.anchoredPosition = new Vector2(
                    Random.Range(-10f, 10f),
                    Random.Range(-5f, 5f)
                );
            }
            
            // Scan lines
            if (scanLineOverlay != null && enableScanLines)
            {
                scanLineOverlay.color = new Color(0, 0, 0, 
                    Random.Range(0f, glitchIntensity * 0.5f));
            }
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Reset
        if (colorShiftOverlay != null)
        {
            colorShiftOverlay.color = new Color(0, 0, 0, 0);
            colorShiftOverlay.rectTransform.anchoredPosition = Vector2.zero;
        }
        
        if (scanLineOverlay != null)
        {
            scanLineOverlay.color = new Color(0, 0, 0, 0);
        }
        
        isGlitching = false;
    }
    
    /// <summary>
    /// Trigger a manual glitch (for cutscenes, etc.)
    /// </summary>
    public void TriggerGlitch()
    {
        if (!isGlitching)
            StartCoroutine(DoGlitch());
    }
    
    /// <summary>
    /// Heavy glitch effect (for death, etc.)
    /// </summary>
    public IEnumerator HeavyGlitch(float duration)
    {
        float originalIntensity = glitchIntensity;
        glitchIntensity = 1f;
        
        float elapsed = 0f;
        while (elapsed < duration)
        {
            StartCoroutine(DoGlitch());
            elapsed += 0.1f;
            yield return new WaitForSeconds(0.1f);
        }
        
        glitchIntensity = originalIntensity;
    }
}
