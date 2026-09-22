using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// SKIP TUTORIAL BUTTON GENERATOR
/// Creates a "Skip Tutorial" button UI at runtime.
/// Works with TutorialManager to skip the tutorial when clicked.
/// 
/// Just attach to any GameObject in the Tutorial scene!
/// </summary>
public class SkipTutorialButton : MonoBehaviour
{
    [Header("Button Settings")]
    [SerializeField] private Vector2 buttonPosition = new Vector2(100, -50); // Top-left offset
    [SerializeField] private Vector2 buttonSize = new Vector2(200, 45);
    [SerializeField] private KeyCode skipKeyShortcut = KeyCode.Tab; // Press Tab to skip quickly
    
    [Header("Colors")]
    [SerializeField] private Color backgroundColor = new Color(0, 0, 0, 0.7f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color borderColor = Color.white;
    [SerializeField] private Color hoverColor = new Color(0.3f, 0.3f, 0.3f);
    
    [Header("Audio")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip clickSound;
    
    private Canvas skipCanvas;
    private Button skipButton;
    private TutorialManager tutorialManager;
    private bool isHidden = false;
    
    private void Start()
    {
        // Find TutorialManager
        tutorialManager = FindObjectOfType<TutorialManager>();
        
        if (tutorialManager == null)
        {
            Debug.LogWarning("SkipTutorialButton: No TutorialManager found in scene!");
            enabled = false;
            return;
        }
        
        // Setup EventSystem if not present
        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }
        
        GenerateSkipButton();
    }
    
    private void Update()
    {
        if (isHidden) return;
        
        // Keyboard shortcut to skip (works even with locked cursor)
        if (Input.GetKeyDown(skipKeyShortcut))
        {
            OnSkipClicked();
        }
    }
    
    private void GenerateSkipButton()
    {
        // Create Canvas
        GameObject canvasObj = new GameObject("SkipTutorialCanvas");
        canvasObj.transform.SetParent(transform);
        
        skipCanvas = canvasObj.AddComponent<Canvas>();
        skipCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        skipCanvas.sortingOrder = 50;
        
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // Create Button
        GameObject buttonObj = new GameObject("SkipButton");
        buttonObj.transform.SetParent(skipCanvas.transform, false);
        
        RectTransform btnRect = buttonObj.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0, 1); // Top-left anchor
        btnRect.anchorMax = new Vector2(0, 1);
        btnRect.pivot = new Vector2(0, 1);
        btnRect.anchoredPosition = buttonPosition;
        btnRect.sizeDelta = buttonSize;
        
        // Background
        Image btnBg = buttonObj.AddComponent<Image>();
        btnBg.color = backgroundColor;
        
        // Border
        Outline outline = buttonObj.AddComponent<Outline>();
        outline.effectColor = borderColor;
        outline.effectDistance = new Vector2(2, 2);
        
        // Button component
        skipButton = buttonObj.AddComponent<Button>();
        
        ColorBlock colors = skipButton.colors;
        colors.normalColor = backgroundColor;
        colors.highlightedColor = hoverColor;
        colors.pressedColor = new Color(0.5f, 0.5f, 0.5f);
        colors.fadeDuration = 0.1f;
        skipButton.colors = colors;
        
        skipButton.onClick.AddListener(OnSkipClicked);
        
        // Button Text - shows keyboard shortcut hint
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        TMP_Text btnText = textObj.AddComponent<TextMeshProUGUI>();
        btnText.text = $"SKIP TUTORIAL [{skipKeyShortcut}]";
        btnText.fontSize = 18;
        btnText.fontStyle = FontStyles.Bold;
        btnText.alignment = TextAlignmentOptions.Center;
        btnText.color = textColor;
        
        Debug.Log($"✅ Skip Tutorial Button Generated! Press [{skipKeyShortcut}] to skip.");
    }
    
    private void OnSkipClicked()
    {
        if (isHidden) return;
        
        // Play click sound
        if (sfxSource != null && clickSound != null)
        {
            sfxSource.PlayOneShot(clickSound);
        }
        
        // Call TutorialManager to skip
        if (tutorialManager != null)
        {
            tutorialManager.ConfirmSkip();
        }
        
        // Hide the button after clicking
        HideButton();
    }
    
    /// <summary>
    /// Called when tutorial is completed or skipped - hides the button
    /// </summary>
    public void HideButton()
    {
        isHidden = true;
        if (skipCanvas != null)
        {
            skipCanvas.gameObject.SetActive(false);
        }
    }
}
