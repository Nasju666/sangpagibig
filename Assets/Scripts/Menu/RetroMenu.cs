using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// RETRO PIXEL-ART MENU GENERATOR
/// Creates a Memory Leak / 8-bit style menu with:
/// - Black background with floating star particles
/// - Pixelated title with glitch effect
/// - White-bordered retro buttons
/// - Music and click sound effects
/// 
/// Just attach to an empty GameObject in a new scene!
/// </summary>
public class RetroMenuGenerator : MonoBehaviour
{
    [Header("Game Settings")]
    [SerializeField] private string gameTitle = "DEATH.EXE";
    [SerializeField] private string gameSceneName = "Tutorial";
    
    [Header("Star Background")]
    [SerializeField] private int numberOfStars = 100;
    [SerializeField] private float starTwinkleSpeed = 2f;
    
    [Header("Audio (Assign in Inspector)")]
    [SerializeField] private AudioClip menuMusic;
    [SerializeField] private AudioClip clickSound;
    [SerializeField] private AudioClip hoverSound;
    
    [Header("Colors")]
    [SerializeField] private Color backgroundColor = Color.black;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color buttonBorderColor = Color.white;
    [SerializeField] private Color buttonHoverColor = new Color(0.3f, 0.3f, 0.3f);
    
    // Private references
    private Canvas mainCanvas;
    private GameObject optionsPanel;
    private AudioSource musicSource;
    private AudioSource sfxSource;
    
    // UI References for settings
    private Slider masterVolumeSlider;
    private Slider musicVolumeSlider;
    private Slider sfxVolumeSlider;
    
    private void Awake()
    {
        GenerateRetroMenu();
    }
    
    public void GenerateRetroMenu()
    {
        SetupCamera();
        SetupEventSystem();
        CreateStarBackground();
        CreateUI();
        SetupAudio();
        
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        Debug.Log("✅ Retro Menu Generated!");
    }
    
    #region CAMERA SETUP
    
    private void SetupCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            mainCamera = camObj.AddComponent<Camera>();
            camObj.AddComponent<AudioListener>();
        }
        
        mainCamera.transform.position = new Vector3(0, 0, -10);
        mainCamera.backgroundColor = backgroundColor;
        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.orthographic = true; // 2D style
        mainCamera.orthographicSize = 5;
    }
    
    #endregion
    
    #region EVENT SYSTEM
    
    private void SetupEventSystem()
    {
        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }
    }
    
    #endregion
    
    #region STAR BACKGROUND
    
    private void CreateStarBackground()
    {
        // Create a canvas for stars (behind UI)
        GameObject starCanvas = new GameObject("StarCanvas");
        Canvas canvas = starCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -1; // Behind main UI
        
        CanvasScaler scaler = starCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        // Create stars
        for (int i = 0; i < numberOfStars; i++)
        {
            CreateStar(starCanvas.transform);
        }
    }
    
    private void CreateStar(Transform parent)
    {
        GameObject star = new GameObject("Star");
        star.transform.SetParent(parent, false);
        
        RectTransform rect = star.AddComponent<RectTransform>();
        
        // Random position across screen
        float x = Random.Range(-960f, 960f);
        float y = Random.Range(-540f, 540f);
        rect.anchoredPosition = new Vector2(x, y);
        
        // Random size (small stars)
        float size = Random.Range(2f, 6f);
        rect.sizeDelta = new Vector2(size, size);
        
        Image starImage = star.AddComponent<Image>();
        starImage.color = Color.white;
        
        // Add twinkle effect
        TwinkleStar twinkle = star.AddComponent<TwinkleStar>();
        twinkle.Initialize(starTwinkleSpeed, Random.Range(0f, Mathf.PI * 2f));
    }
    
    #endregion
    
    #region UI CREATION
    
    private void CreateUI()
    {
        // Main Canvas
        GameObject canvasObj = new GameObject("MainCanvas");
        mainCanvas = canvasObj.AddComponent<Canvas>();
        mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        mainCanvas.sortingOrder = 0;
        
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // Create main menu
        CreateMainMenuPanel();
        
        // Create options panel (hidden)
        CreateOptionsPanel();
    }
    
    private void CreateMainMenuPanel()
    {
        GameObject menuPanel = new GameObject("MenuPanel");
        menuPanel.transform.SetParent(mainCanvas.transform, false);
        
        RectTransform menuRect = menuPanel.AddComponent<RectTransform>();
        menuRect.anchorMin = Vector2.zero;
        menuRect.anchorMax = Vector2.one;
        menuRect.offsetMin = Vector2.zero;
        menuRect.offsetMax = Vector2.zero;
        
        // Title
        CreateRetroTitle(menuPanel.transform);
        
        // Buttons Container
        GameObject buttonContainer = new GameObject("ButtonContainer");
        buttonContainer.transform.SetParent(menuPanel.transform, false);
        
        RectTransform btnRect = buttonContainer.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0.25f);
        btnRect.anchorMax = new Vector2(0.5f, 0.5f);
        btnRect.sizeDelta = new Vector2(300, 200);
        
        VerticalLayoutGroup vlg = buttonContainer.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 20;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        
        // Create retro buttons
        CreateRetroButton("START", buttonContainer.transform, OnStartClicked);
        CreateRetroButton("OPTIONS", buttonContainer.transform, OnOptionsClicked);
        CreateRetroButton("QUIT", buttonContainer.transform, OnQuitClicked);
    }
    
    private void CreateRetroTitle(Transform parent)
    {
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(parent, false);
        
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.7f);
        titleRect.anchorMax = new Vector2(0.5f, 0.9f);
        titleRect.sizeDelta = new Vector2(800, 150);
        
        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = gameTitle;
        titleText.fontSize = 80;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = textColor;
        
        // Add character spacing for that pixel look
        titleText.characterSpacing = 10;
        titleText.wordSpacing = 20;
        
        // Add glitch effect
        titleObj.AddComponent<RetroGlitchEffect>();
    }
    
    private void CreateRetroButton(string text, Transform parent, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObj = new GameObject(text + "Button");
        buttonObj.transform.SetParent(parent, false);
        
        RectTransform btnRect = buttonObj.AddComponent<RectTransform>();
        btnRect.sizeDelta = new Vector2(250, 50);
        
        // Button background (dark/transparent)
        Image btnBg = buttonObj.AddComponent<Image>();
        btnBg.color = new Color(0, 0, 0, 0.8f);
        
        // Add outline for border effect
        Outline outline = buttonObj.AddComponent<Outline>();
        outline.effectColor = buttonBorderColor;
        outline.effectDistance = new Vector2(3, 3);
        
        // Second outline for thicker border
        Outline outline2 = buttonObj.AddComponent<Outline>();
        outline2.effectColor = buttonBorderColor;
        outline2.effectDistance = new Vector2(-3, -3);
        
        Button button = buttonObj.AddComponent<Button>();
        
        // Button colors
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0, 0, 0, 0.8f);
        colors.highlightedColor = buttonHoverColor;
        colors.pressedColor = new Color(0.5f, 0.5f, 0.5f);
        colors.selectedColor = buttonHoverColor;
        colors.fadeDuration = 0.1f;
        button.colors = colors;
        
        button.onClick.AddListener(onClick);
        button.onClick.AddListener(PlayClickSound);
        
        // Button text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        TextMeshProUGUI btnText = textObj.AddComponent<TextMeshProUGUI>();
        btnText.text = text;
        btnText.fontSize = 28;
        btnText.fontStyle = FontStyles.Bold;
        btnText.alignment = TextAlignmentOptions.Center;
        btnText.color = textColor;
        btnText.characterSpacing = 5;
        
        // Add hover effect
        RetroButtonHover hoverEffect = buttonObj.AddComponent<RetroButtonHover>();
        hoverEffect.Initialize(this, btnText, textColor, new Color(0.8f, 0.8f, 0.8f));
    }
    
    #endregion
    
    #region OPTIONS PANEL
    
    private void CreateOptionsPanel()
    {
        optionsPanel = new GameObject("OptionsPanel");
        optionsPanel.transform.SetParent(mainCanvas.transform, false);
        
        RectTransform panelRect = optionsPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        
        // Dark overlay background
        Image panelBg = optionsPanel.AddComponent<Image>();
        panelBg.color = new Color(0, 0, 0, 0.95f);
        
        // Options Title
        CreateOptionsTitle(optionsPanel.transform);
        
        // Options Content
        CreateOptionsContent(optionsPanel.transform);
        
        // Back Button
        CreateBackButton(optionsPanel.transform);
        
        optionsPanel.SetActive(false);
    }
    
    private void CreateOptionsTitle(Transform parent)
    {
        GameObject titleObj = new GameObject("OptionsTitle");
        titleObj.transform.SetParent(parent, false);
        
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.8f);
        titleRect.anchorMax = new Vector2(0.5f, 0.9f);
        titleRect.sizeDelta = new Vector2(400, 80);
        
        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "OPTIONS";
        titleText.fontSize = 50;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = textColor;
        titleText.characterSpacing = 10;
    }
    
    private void CreateOptionsContent(Transform parent)
    {
        GameObject contentObj = new GameObject("OptionsContent");
        contentObj.transform.SetParent(parent, false);
        
        RectTransform contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.2f, 0.25f);
        contentRect.anchorMax = new Vector2(0.8f, 0.75f);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;
        
        VerticalLayoutGroup vlg = contentObj.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 40;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.padding = new RectOffset(50, 50, 30, 30);
        
        // Volume sliders
        masterVolumeSlider = CreateRetroSlider("MASTER VOLUME", contentObj.transform, 
            PlayerPrefs.GetFloat("MasterVolume", 1f), OnMasterVolumeChanged);
        
        musicVolumeSlider = CreateRetroSlider("MUSIC VOLUME", contentObj.transform,
            PlayerPrefs.GetFloat("MusicVolume", 1f), OnMusicVolumeChanged);
        
        sfxVolumeSlider = CreateRetroSlider("SFX VOLUME", contentObj.transform,
            PlayerPrefs.GetFloat("SFXVolume", 1f), OnSFXVolumeChanged);
        
        // Fullscreen toggle
        CreateRetroToggle("FULLSCREEN", contentObj.transform, Screen.fullScreen, OnFullscreenChanged);
    }
    
    private Slider CreateRetroSlider(string label, Transform parent, float defaultValue, UnityEngine.Events.UnityAction<float> onChanged)
    {
        GameObject container = new GameObject(label);
        container.transform.SetParent(parent, false);
        
        RectTransform containerRect = container.AddComponent<RectTransform>();
        containerRect.sizeDelta = new Vector2(0, 60);
        
        // Label
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(container.transform, false);
        
        RectTransform labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0, 0.5f);
        labelRect.anchorMax = new Vector2(0.4f, 1f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        
        TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
        labelText.text = label;
        labelText.fontSize = 24;
        labelText.alignment = TextAlignmentOptions.Left;
        labelText.color = textColor;
        
        // Slider container
        GameObject sliderContainer = new GameObject("SliderContainer");
        sliderContainer.transform.SetParent(container.transform, false);
        
        RectTransform sliderContainerRect = sliderContainer.AddComponent<RectTransform>();
        sliderContainerRect.anchorMin = new Vector2(0.45f, 0.2f);
        sliderContainerRect.anchorMax = new Vector2(0.85f, 0.8f);
        sliderContainerRect.offsetMin = Vector2.zero;
        sliderContainerRect.offsetMax = Vector2.zero;
        
        // Slider background
        Image sliderBg = sliderContainer.AddComponent<Image>();
        sliderBg.color = new Color(0.2f, 0.2f, 0.2f);
        
        // Add border
        Outline sliderOutline = sliderContainer.AddComponent<Outline>();
        sliderOutline.effectColor = buttonBorderColor;
        sliderOutline.effectDistance = new Vector2(2, 2);
        
        Slider slider = sliderContainer.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = defaultValue;
        
        // Fill
        GameObject fillArea = new GameObject("FillArea");
        fillArea.transform.SetParent(sliderContainer.transform, false);
        
        RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(5, 5);
        fillAreaRect.offsetMax = new Vector2(-5, -5);
        
        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        
        RectTransform fillRect = fill.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;
        
        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = textColor;
        
        slider.fillRect = fillRect;
        slider.onValueChanged.AddListener(onChanged);
        
        // Value text
        GameObject valueObj = new GameObject("Value");
        valueObj.transform.SetParent(container.transform, false);
        
        RectTransform valueRect = valueObj.AddComponent<RectTransform>();
        valueRect.anchorMin = new Vector2(0.87f, 0.3f);
        valueRect.anchorMax = new Vector2(1f, 0.7f);
        valueRect.offsetMin = Vector2.zero;
        valueRect.offsetMax = Vector2.zero;
        
        TextMeshProUGUI valueText = valueObj.AddComponent<TextMeshProUGUI>();
        valueText.text = Mathf.RoundToInt(defaultValue * 100) + "%";
        valueText.fontSize = 22;
        valueText.alignment = TextAlignmentOptions.Center;
        valueText.color = textColor;
        
        slider.onValueChanged.AddListener((val) => valueText.text = Mathf.RoundToInt(val * 100) + "%");
        
        return slider;
    }
    
    private void CreateRetroToggle(string label, Transform parent, bool defaultValue, UnityEngine.Events.UnityAction<bool> onChanged)
    {
        GameObject container = new GameObject(label);
        container.transform.SetParent(parent, false);
        
        RectTransform containerRect = container.AddComponent<RectTransform>();
        containerRect.sizeDelta = new Vector2(0, 50);
        
        // Label
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(container.transform, false);
        
        RectTransform labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0, 0);
        labelRect.anchorMax = new Vector2(0.6f, 1f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        
        TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
        labelText.text = label;
        labelText.fontSize = 24;
        labelText.alignment = TextAlignmentOptions.Left;
        labelText.color = textColor;
        
        // Toggle box
        GameObject toggleBox = new GameObject("ToggleBox");
        toggleBox.transform.SetParent(container.transform, false);
        
        RectTransform toggleRect = toggleBox.AddComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(0.65f, 0.15f);
        toggleRect.anchorMax = new Vector2(0.65f, 0.85f);
        toggleRect.sizeDelta = new Vector2(40, 0);
        
        Image toggleBg = toggleBox.AddComponent<Image>();
        toggleBg.color = new Color(0.1f, 0.1f, 0.1f);
        
        Outline toggleOutline = toggleBox.AddComponent<Outline>();
        toggleOutline.effectColor = buttonBorderColor;
        toggleOutline.effectDistance = new Vector2(2, 2);
        
        Toggle toggle = toggleBox.AddComponent<Toggle>();
        
        // Checkmark
        GameObject checkmark = new GameObject("Checkmark");
        checkmark.transform.SetParent(toggleBox.transform, false);
        
        RectTransform checkRect = checkmark.AddComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(0.15f, 0.15f);
        checkRect.anchorMax = new Vector2(0.85f, 0.85f);
        checkRect.offsetMin = Vector2.zero;
        checkRect.offsetMax = Vector2.zero;
        
        Image checkImage = checkmark.AddComponent<Image>();
        checkImage.color = textColor;
        
        toggle.targetGraphic = toggleBg;
        toggle.graphic = checkImage;
        toggle.isOn = defaultValue;
        toggle.onValueChanged.AddListener(onChanged);
    }
    
    private void CreateBackButton(Transform parent)
    {
        GameObject backBtn = new GameObject("BackButton");
        backBtn.transform.SetParent(parent, false);
        
        RectTransform backRect = backBtn.AddComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0.5f, 0.08f);
        backRect.anchorMax = new Vector2(0.5f, 0.15f);
        backRect.sizeDelta = new Vector2(200, 0);
        
        Image backBg = backBtn.AddComponent<Image>();
        backBg.color = new Color(0, 0, 0, 0.8f);
        
        Outline outline = backBtn.AddComponent<Outline>();
        outline.effectColor = buttonBorderColor;
        outline.effectDistance = new Vector2(2, 2);
        
        Button button = backBtn.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0, 0, 0, 0.8f);
        colors.highlightedColor = buttonHoverColor;
        button.colors = colors;
        
        button.onClick.AddListener(CloseOptions);
        button.onClick.AddListener(PlayClickSound);
        
        // Text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(backBtn.transform, false);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        TextMeshProUGUI backText = textObj.AddComponent<TextMeshProUGUI>();
        backText.text = "< BACK";
        backText.fontSize = 24;
        backText.fontStyle = FontStyles.Bold;
        backText.alignment = TextAlignmentOptions.Center;
        backText.color = textColor;
    }
    
    #endregion
    
    #region AUDIO
    
    private void SetupAudio()
    {
        GameObject audioObj = new GameObject("AudioManager");
        audioObj.transform.SetParent(transform);
        
        // Music
        musicSource = audioObj.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.volume = PlayerPrefs.GetFloat("MusicVolume", 1f);
        musicSource.playOnAwake = false;
        
        if (menuMusic != null)
        {
            musicSource.clip = menuMusic;
            musicSource.Play();
        }
        
        // SFX
        sfxSource = audioObj.AddComponent<AudioSource>();
        sfxSource.volume = PlayerPrefs.GetFloat("SFXVolume", 1f);
        sfxSource.playOnAwake = false;
    }
    
    public void PlayClickSound()
    {
        if (sfxSource != null && clickSound != null)
        {
            sfxSource.PlayOneShot(clickSound);
        }
    }
    
    public void PlayHoverSound()
    {
        if (sfxSource != null && hoverSound != null)
        {
            sfxSource.PlayOneShot(hoverSound);
        }
    }
    
    #endregion
    
    #region BUTTON CALLBACKS
    
    private void OnStartClicked()
    {
        // Show mode selection panel (Single Player / Multiplayer)
        var multiplayerPanel = FindObjectOfType<MultiplayerPanelGenerator>();
        if (multiplayerPanel != null)
        {
            multiplayerPanel.ShowModeSelection();
        }
        else
        {
            // Fallback: load game directly if no multiplayer panel
            Debug.Log("Starting game: " + gameSceneName);
            StartCoroutine(LoadGameWithFade());
        }
    }
    
    private System.Collections.IEnumerator LoadGameWithFade()
    {
        // Create fade overlay
        GameObject fadePanel = new GameObject("FadePanel");
        fadePanel.transform.SetParent(mainCanvas.transform, false);
        
        RectTransform fadeRect = fadePanel.AddComponent<RectTransform>();
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.offsetMin = Vector2.zero;
        fadeRect.offsetMax = Vector2.zero;
        
        Image fadeImage = fadePanel.AddComponent<Image>();
        fadeImage.color = new Color(0, 0, 0, 0);
        fadeImage.raycastTarget = true;
        
        // Fade to black
        float fadeTime = 0.5f;
        float elapsed = 0;
        
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            fadeImage.color = new Color(0, 0, 0, elapsed / fadeTime);
            
            // Also fade music
            if (musicSource != null)
                musicSource.volume = Mathf.Lerp(PlayerPrefs.GetFloat("MusicVolume", 1f), 0, elapsed / fadeTime);
            
            yield return null;
        }
        
        SceneManager.LoadScene(gameSceneName);
    }
    
    private void OnOptionsClicked()
    {
        if (optionsPanel != null)
            optionsPanel.SetActive(true);
    }
    
    private void CloseOptions()
    {
        if (optionsPanel != null)
            optionsPanel.SetActive(false);
        
        PlayerPrefs.Save();
    }
    
    private void OnQuitClicked()
    {
        Debug.Log("Quitting game...");
        
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
    
    #endregion
    
    #region SETTINGS CALLBACKS
    
    private void OnMasterVolumeChanged(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat("MasterVolume", value);
    }
    
    private void OnMusicVolumeChanged(float value)
    {
        if (musicSource != null)
            musicSource.volume = value;
        PlayerPrefs.SetFloat("MusicVolume", value);
    }
    
    private void OnSFXVolumeChanged(float value)
    {
        if (sfxSource != null)
            sfxSource.volume = value;
        PlayerPrefs.SetFloat("SFXVolume", value);
    }
    
    private void OnFullscreenChanged(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        PlayerPrefs.SetInt("Fullscreen", isFullscreen ? 1 : 0);
    }
    
    #endregion
}

#region HELPER COMPONENTS

/// <summary>
/// Makes stars twinkle with random fade in/out
/// </summary>
public class TwinkleStar : MonoBehaviour
{
    private float speed;
    private float offset;
    private Image image;
    
    public void Initialize(float twinkleSpeed, float timeOffset)
    {
        speed = twinkleSpeed;
        offset = timeOffset;
        image = GetComponent<Image>();
    }
    
    private void Update()
    {
        if (image != null)
        {
            float alpha = (Mathf.Sin((Time.time + offset) * speed) + 1f) / 2f;
            alpha = Mathf.Lerp(0.2f, 1f, alpha); // Keep minimum visibility
            image.color = new Color(1, 1, 1, alpha);
        }
    }
}

/// <summary>
/// Retro glitch effect for title text
/// </summary>
public class RetroGlitchEffect : MonoBehaviour
{
    private TextMeshProUGUI text;
    private RectTransform rectTransform;
    private Vector3 originalPos;
    private float glitchTimer;
    private float nextGlitchTime;
    
    private void Start()
    {
        text = GetComponent<TextMeshProUGUI>();
        rectTransform = GetComponent<RectTransform>();
        originalPos = rectTransform.anchoredPosition;
        SetNextGlitchTime();
    }
    
    private void Update()
    {
        glitchTimer += Time.deltaTime;
        
        if (glitchTimer >= nextGlitchTime)
        {
            StartCoroutine(DoGlitch());
            glitchTimer = 0;
            SetNextGlitchTime();
        }
    }
    
    private void SetNextGlitchTime()
    {
        nextGlitchTime = Random.Range(2f, 5f);
    }
    
    private System.Collections.IEnumerator DoGlitch()
    {
        int glitchFrames = Random.Range(2, 5);
        
        for (int i = 0; i < glitchFrames; i++)
        {
            // Random offset
            float offsetX = Random.Range(-10f, 10f);
            float offsetY = Random.Range(-5f, 5f);
            rectTransform.anchoredPosition = originalPos + new Vector3(offsetX, offsetY, 0);
            
            // Random color glitch
            if (text != null && Random.value > 0.5f)
            {
                text.color = Random.value > 0.5f ? Color.red : Color.cyan;
            }
            
            yield return new WaitForSeconds(0.05f);
        }
        
        // Reset
        rectTransform.anchoredPosition = originalPos;
        if (text != null)
            text.color = Color.white;
    }
}

/// <summary>
/// Retro button hover effect
/// </summary>
public class RetroButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private RetroMenuGenerator menuGenerator;
    private TextMeshProUGUI buttonText;
    private Color normalColor;
    private Color hoverColor;
    private string originalText;
    
    public void Initialize(RetroMenuGenerator generator, TextMeshProUGUI text, Color normal, Color hover)
    {
        menuGenerator = generator;
        buttonText = text;
        normalColor = normal;
        hoverColor = hover;
        
        if (buttonText != null)
            originalText = buttonText.text;
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (buttonText != null)
        {
            buttonText.color = hoverColor;
            buttonText.text = "> " + originalText + " <";
        }
        
        if (menuGenerator != null)
            menuGenerator.PlayHoverSound();
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        if (buttonText != null)
        {
            buttonText.color = normalColor;
            buttonText.text = originalText;
        }
    }
}

#endregion
