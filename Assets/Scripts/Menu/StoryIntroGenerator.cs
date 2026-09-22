using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

/// <summary>
/// STORY INTRO GENERATOR
/// Creates a cinematic story intro with typewriter text effect.
/// Plays after player clicks START, before Tutorial begins.
/// 
/// Style matches RetroMenuGenerator (black background, white text, retro feel)
/// </summary>
public class StoryIntroGenerator : MonoBehaviour
{
    [Header("Story Settings")]
    [TextArea(10, 20)]
    [SerializeField] private string storyText = @"Life is unpredictable...

One moment, you're living your ordinary life.

The next, you wake up in a dark room.

A voice speaks through the static:

'You want to know if life is worth it?'

'Then prove it.'

'Survive the trials ahead...'

'...or become another forgotten soul.'

[PRESS ANY KEY TO CONTINUE]";
    
    [Header("Next Scene")]
    [SerializeField] private string nextSceneName = "Tutorial";
    
    [Header("Typewriter Effect")]
    [SerializeField] private float typeSpeed = 0.04f;
    [SerializeField] private float lineDelay = 0.3f;
    [SerializeField] private float startDelay = 1f;
    
    [Header("Visual Settings")]
    [SerializeField] private Color backgroundColor = Color.black;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private int fontSize = 36;
    
    [Header("Audio")]
    [SerializeField] private AudioClip typeSound;
    [SerializeField] private AudioClip ambientMusic;
    [SerializeField] private float typeSoundVolume = 0.3f;
    
    // Generated UI
    private Canvas storyCanvas;
    private TMP_Text storyTextUI;
    private AudioSource audioSource;
    private AudioSource musicSource;
    
    private bool isTyping = true;
    private bool canSkip = false;
    private Coroutine typewriterCoroutine;
    
    void Start()
    {
        GenerateStoryUI();
        StartCoroutine(PlayStorySequence());
    }
    
    void Update()
    {
        // Allow skip after some text has appeared
        if (canSkip && Input.anyKeyDown)
        {
            if (isTyping)
            {
                // Skip to end of current text
                StopCoroutine(typewriterCoroutine);
                storyTextUI.text = storyText;
                isTyping = false;
            }
            else
            {
                // Proceed to next scene
                LoadNextScene();
            }
        }
    }
    
    void GenerateStoryUI()
    {
        // Create Canvas
        GameObject canvasObj = new GameObject("StoryCanvas");
        canvasObj.transform.SetParent(transform);
        
        storyCanvas = canvasObj.AddComponent<Canvas>();
        storyCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        storyCanvas.sortingOrder = 100;
        
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // Background
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(storyCanvas.transform, false);
        
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        
        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = backgroundColor;
        
        // Story Text
        GameObject textObj = new GameObject("StoryText");
        textObj.transform.SetParent(storyCanvas.transform, false);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.1f, 0.1f);
        textRect.anchorMax = new Vector2(0.9f, 0.9f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        storyTextUI = textObj.AddComponent<TextMeshProUGUI>();
        storyTextUI.text = "";
        storyTextUI.fontSize = fontSize;
        storyTextUI.color = textColor;
        storyTextUI.alignment = TextAlignmentOptions.Center;
        storyTextUI.lineSpacing = 20;
        
        // Add slight glow effect for retro feel
        storyTextUI.fontMaterial.EnableKeyword("GLOW_ON");
        
        // Audio Sources
        GameObject audioObj = new GameObject("Audio");
        audioObj.transform.SetParent(transform);
        
        audioSource = audioObj.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.volume = typeSoundVolume;
        
        musicSource = audioObj.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.volume = 0.3f;
        
        if (ambientMusic != null)
        {
            musicSource.clip = ambientMusic;
            musicSource.Play();
        }
        
        // Create Skip Button
        CreateSkipButton();
        
        // Unlock cursor so button is clickable
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        Debug.Log("✅ Story Intro Generated!");
    }
    
    void CreateSkipButton()
    {
        // Skip Button (bottom right corner)
        GameObject skipBtnObj = new GameObject("SkipButton");
        skipBtnObj.transform.SetParent(storyCanvas.transform, false);
        
        RectTransform skipRect = skipBtnObj.AddComponent<RectTransform>();
        skipRect.anchorMin = new Vector2(1, 0); // Bottom right
        skipRect.anchorMax = new Vector2(1, 0);
        skipRect.pivot = new Vector2(1, 0);
        skipRect.anchoredPosition = new Vector2(-50, 50);
        skipRect.sizeDelta = new Vector2(180, 50);
        
        // Button background
        Image skipBg = skipBtnObj.AddComponent<Image>();
        skipBg.color = new Color(0, 0, 0, 0.7f);
        
        // Border
        Outline outline = skipBtnObj.AddComponent<Outline>();
        outline.effectColor = textColor;
        outline.effectDistance = new Vector2(2, 2);
        
        // Button component
        Button skipButton = skipBtnObj.AddComponent<Button>();
        
        ColorBlock colors = skipButton.colors;
        colors.normalColor = new Color(0, 0, 0, 0.7f);
        colors.highlightedColor = new Color(0.3f, 0.3f, 0.3f);
        colors.pressedColor = new Color(0.5f, 0.5f, 0.5f);
        skipButton.colors = colors;
        
        skipButton.onClick.AddListener(OnSkipClicked);
        
        // Button text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(skipBtnObj.transform, false);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        TMP_Text btnText = textObj.AddComponent<TextMeshProUGUI>();
        btnText.text = "SKIP >>";
        btnText.fontSize = 22;
        btnText.fontStyle = FontStyles.Bold;
        btnText.alignment = TextAlignmentOptions.Center;
        btnText.color = textColor;
    }
    
    void OnSkipClicked()
    {
        // Skip directly to next scene
        if (typewriterCoroutine != null)
            StopCoroutine(typewriterCoroutine);
        
        LoadNextScene();
    }
    
    IEnumerator PlayStorySequence()
    {
        // Initial delay
        yield return new WaitForSeconds(startDelay);
        
        canSkip = true;
        
        // Start typewriter effect
        typewriterCoroutine = StartCoroutine(TypewriterEffect());
    }
    
    IEnumerator TypewriterEffect()
    {
        isTyping = true;
        storyTextUI.text = "";
        
        foreach (char c in storyText)
        {
            storyTextUI.text += c;
            
            // Play type sound for letters only
            if (char.IsLetterOrDigit(c) && typeSound != null)
            {
                audioSource.pitch = Random.Range(0.9f, 1.1f);
                audioSource.PlayOneShot(typeSound);
            }
            
            // Pause at line breaks
            if (c == '\n')
            {
                yield return new WaitForSeconds(lineDelay);
            }
            else
            {
                yield return new WaitForSeconds(typeSpeed);
            }
        }
        
        isTyping = false;
    }
    
    void LoadNextScene()
    {
        // Fade to black effect
        StartCoroutine(FadeAndLoad());
    }
    
    IEnumerator FadeAndLoad()
    {
        // Create fade overlay
        GameObject fadeObj = new GameObject("FadeOverlay");
        fadeObj.transform.SetParent(storyCanvas.transform, false);
        
        RectTransform fadeRect = fadeObj.AddComponent<RectTransform>();
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.offsetMin = Vector2.zero;
        fadeRect.offsetMax = Vector2.zero;
        
        Image fadeImage = fadeObj.AddComponent<Image>();
        fadeImage.color = new Color(0, 0, 0, 0);
        
        // Fade to black
        float fadeTime = 1f;
        float elapsed = 0f;
        
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float alpha = elapsed / fadeTime;
            fadeImage.color = new Color(0, 0, 0, alpha);
            
            // Fade music too
            if (musicSource != null)
                musicSource.volume = 0.3f * (1 - alpha);
            
            yield return null;
        }
        
        // Load next scene
        SceneManager.LoadScene(nextSceneName);
    }
    
    /// <summary>
    /// Call this from MainMenuManager when START is clicked
    /// </summary>
    public static void ShowStoryIntro()
    {
        SceneManager.LoadScene("StoryIntro");
    }
}
