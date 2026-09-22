using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// PAUSE MENU GENERATOR
/// Creates a pause menu UI at runtime with:
/// - Resume, Settings, Credits, Quit buttons
/// - Settings panel with Controls, Fullscreen, Window Mode
/// - Credits panel
/// 
/// Just attach to any GameObject in your stage scenes!
/// Press ESC to toggle pause menu.
/// </summary>
public class PauseMenuGenerator : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private KeyCode pauseKey = KeyCode.Escape;
    [SerializeField] private string mainMenuSceneName = "Main Menu";
    
    [Header("Colors (Match Your Menu Style)")]
    [SerializeField] private Color backgroundColor = new Color(0, 0, 0, 0.95f);
    [SerializeField] private Color panelColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color buttonBorderColor = Color.white;
    [SerializeField] private Color buttonHoverColor = new Color(0.3f, 0.3f, 0.3f);
    
    [Header("Credits Text")]
    [TextArea(5, 10)]
    [SerializeField] private string creditsText = "CREDITS\n\nGame Development Team\n\nProgramming\n- Developer 1\n- Developer 2\n\nArt & Design\n- Artist 1\n\nMusic & Sound\n- Audio Designer";
    
    [Header("Audio")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip clickSound;
    
    // Generated UI References
    private Canvas pauseCanvas;
    private GameObject pausePanel;
    private GameObject settingsPanel;
    private GameObject creditsPanel;
    
    // Settings UI
    private Toggle fullscreenToggle;
    private TMP_Dropdown windowModeDropdown;
    
    // State
    private bool isPaused = false;
    private bool wasTimeScaleZero = false;
    
    private void Start()
    {
        GeneratePauseMenu();
    }
    
    private void Update()
    {
        if (Input.GetKeyDown(pauseKey))
        {
            TogglePause();
        }
    }
    
    public void TogglePause()
    {
        if (isPaused)
            Resume();
        else
            Pause();
    }
    
    public void Pause()
    {
        isPaused = true;
        wasTimeScaleZero = Time.timeScale == 0;
        Time.timeScale = 0f;
        
        // Tell MouseLook to stop fighting for cursor
        MouseLook.isPaused = true;
        
        pausePanel.SetActive(true);
        settingsPanel.SetActive(false);
        creditsPanel.SetActive(false);
        
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    
    public void Resume()
    {
        isPaused = false;
        if (!wasTimeScaleZero)
            Time.timeScale = 1f;
        
        pausePanel.SetActive(false);
        settingsPanel.SetActive(false);
        creditsPanel.SetActive(false);
        
        // Let MouseLook control cursor again
        MouseLook.isPaused = false;
        
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    
    #region GENERATE UI
    
    private void GeneratePauseMenu()
    {
        // Ensure EventSystem exists for UI interaction
        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }
        
        // Create Canvas
        GameObject canvasObj = new GameObject("PauseMenuCanvas");
        canvasObj.transform.SetParent(transform);
        
        pauseCanvas = canvasObj.AddComponent<Canvas>();
        pauseCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        pauseCanvas.sortingOrder = 100; // On top of everything
        
        canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObj.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // Create main pause panel
        CreatePausePanel();
        
        // Create settings panel
        CreateSettingsPanel();
        
        // Create credits panel
        CreateCreditsPanel();
        
        // Hide all panels initially
        pausePanel.SetActive(false);
        settingsPanel.SetActive(false);
        creditsPanel.SetActive(false);
        
        Debug.Log("✅ Pause Menu Generated!");
    }
    
    private void CreatePausePanel()
    {
        pausePanel = new GameObject("PausePanel");
        pausePanel.transform.SetParent(pauseCanvas.transform, false);
        
        RectTransform panelRect = pausePanel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        
        // Dark semi-transparent background
        Image panelBg = pausePanel.AddComponent<Image>();
        panelBg.color = backgroundColor;
        
        // Title
        CreateText(pausePanel.transform, "PAUSED", 60, FontStyles.Bold, 
            new Vector2(0.5f, 0.8f), new Vector2(400, 80));
        
        // Button Container
        GameObject buttonContainer = new GameObject("ButtonContainer");
        buttonContainer.transform.SetParent(pausePanel.transform, false);
        
        RectTransform btnContRect = buttonContainer.AddComponent<RectTransform>();
        btnContRect.anchorMin = new Vector2(0.5f, 0.3f);
        btnContRect.anchorMax = new Vector2(0.5f, 0.7f);
        btnContRect.sizeDelta = new Vector2(300, 350);
        
        VerticalLayoutGroup vlg = buttonContainer.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 20;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        
        // Buttons
        CreateRetroButton("RESUME", buttonContainer.transform, OnResumeClicked);
        CreateRetroButton("SETTINGS", buttonContainer.transform, OnSettingsClicked);
        CreateRetroButton("CREDITS", buttonContainer.transform, OnCreditsClicked);
        CreateRetroButton("QUIT TO MENU", buttonContainer.transform, OnQuitClicked);
    }
    
    private void CreateSettingsPanel()
    {
        settingsPanel = new GameObject("SettingsPanel");
        settingsPanel.transform.SetParent(pauseCanvas.transform, false);
        
        RectTransform panelRect = settingsPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        
        Image panelBg = settingsPanel.AddComponent<Image>();
        panelBg.color = backgroundColor;
        
        // Title
        CreateText(settingsPanel.transform, "SETTINGS", 50, FontStyles.Bold,
            new Vector2(0.5f, 0.85f), new Vector2(400, 70));
        
        // Settings Container
        GameObject settingsContainer = new GameObject("SettingsContainer");
        settingsContainer.transform.SetParent(settingsPanel.transform, false);
        
        RectTransform contRect = settingsContainer.AddComponent<RectTransform>();
        contRect.anchorMin = new Vector2(0.3f, 0.3f);
        contRect.anchorMax = new Vector2(0.7f, 0.75f);
        contRect.offsetMin = Vector2.zero;
        contRect.offsetMax = Vector2.zero;
        
        VerticalLayoutGroup vlg = settingsContainer.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 30;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        
        // Controls Section
        CreateControlsSection(settingsContainer.transform);
        
        // Fullscreen Toggle
        CreateFullscreenToggle(settingsContainer.transform);
        
        // Window Mode Dropdown
        CreateWindowModeDropdown(settingsContainer.transform);
        
        // Back Button
        CreateBackButton(settingsPanel.transform, OnSettingsBackClicked);
    }
    
    private void CreateControlsSection(Transform parent)
    {
        GameObject controlsSection = new GameObject("ControlsSection");
        controlsSection.transform.SetParent(parent, false);
        
        RectTransform sectRect = controlsSection.AddComponent<RectTransform>();
        sectRect.sizeDelta = new Vector2(500, 200);
        
        VerticalLayoutGroup vlg = controlsSection.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        
        // Controls Header
        CreateSettingLabel(controlsSection.transform, "CONTROLS", 28, FontStyles.Bold);
        
        // Control bindings
        CreateSettingLabel(controlsSection.transform, "Movement: WASD", 20, FontStyles.Normal);
        CreateSettingLabel(controlsSection.transform, "Look: Mouse", 20, FontStyles.Normal);
        CreateSettingLabel(controlsSection.transform, "Jump: Space", 20, FontStyles.Normal);
        CreateSettingLabel(controlsSection.transform, "Crouch: Left Ctrl", 20, FontStyles.Normal);
        CreateSettingLabel(controlsSection.transform, "Sprint: Left Shift", 20, FontStyles.Normal);
        CreateSettingLabel(controlsSection.transform, "Pause: ESC", 20, FontStyles.Normal);
    }
    
    private void CreateFullscreenToggle(Transform parent)
    {
        GameObject toggleRow = new GameObject("FullscreenRow");
        toggleRow.transform.SetParent(parent, false);
        
        RectTransform rowRect = toggleRow.AddComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(400, 50);
        
        HorizontalLayoutGroup hlg = toggleRow.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 20;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        
        // Label
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(toggleRow.transform, false);
        
        RectTransform labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.sizeDelta = new Vector2(200, 40);
        
        TMP_Text labelText = labelObj.AddComponent<TextMeshProUGUI>();
        labelText.text = "FULLSCREEN";
        labelText.fontSize = 24;
        labelText.color = textColor;
        labelText.alignment = TextAlignmentOptions.Left;
        
        // Toggle
        GameObject toggleObj = new GameObject("Toggle");
        toggleObj.transform.SetParent(toggleRow.transform, false);
        
        RectTransform toggleRect = toggleObj.AddComponent<RectTransform>();
        toggleRect.sizeDelta = new Vector2(50, 50);
        
        Image toggleBg = toggleObj.AddComponent<Image>();
        toggleBg.color = new Color(0.2f, 0.2f, 0.2f);
        
        Outline toggleOutline = toggleObj.AddComponent<Outline>();
        toggleOutline.effectColor = buttonBorderColor;
        toggleOutline.effectDistance = new Vector2(2, 2);
        
        fullscreenToggle = toggleObj.AddComponent<Toggle>();
        fullscreenToggle.isOn = Screen.fullScreen;
        fullscreenToggle.onValueChanged.AddListener(OnFullscreenToggled);
        
        // Checkmark
        GameObject checkmark = new GameObject("Checkmark");
        checkmark.transform.SetParent(toggleObj.transform, false);
        
        RectTransform checkRect = checkmark.AddComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(0.2f, 0.2f);
        checkRect.anchorMax = new Vector2(0.8f, 0.8f);
        checkRect.offsetMin = Vector2.zero;
        checkRect.offsetMax = Vector2.zero;
        
        Image checkImage = checkmark.AddComponent<Image>();
        checkImage.color = textColor;
        
        fullscreenToggle.graphic = checkImage;
        fullscreenToggle.targetGraphic = toggleBg;
    }
    
    private void CreateWindowModeDropdown(Transform parent)
    {
        GameObject dropdownRow = new GameObject("WindowModeRow");
        dropdownRow.transform.SetParent(parent, false);
        
        RectTransform rowRect = dropdownRow.AddComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(400, 50);
        
        HorizontalLayoutGroup hlg = dropdownRow.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 20;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        
        // Label
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(dropdownRow.transform, false);
        
        RectTransform labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.sizeDelta = new Vector2(200, 40);
        
        TMP_Text labelText = labelObj.AddComponent<TextMeshProUGUI>();
        labelText.text = "WINDOW MODE";
        labelText.fontSize = 24;
        labelText.color = textColor;
        labelText.alignment = TextAlignmentOptions.Left;
        
        // Create simple dropdown using our style
        GameObject dropdownObj = new GameObject("Dropdown");
        dropdownObj.transform.SetParent(dropdownRow.transform, false);
        
        RectTransform dropRect = dropdownObj.AddComponent<RectTransform>();
        dropRect.sizeDelta = new Vector2(200, 40);
        
        Image dropBg = dropdownObj.AddComponent<Image>();
        dropBg.color = new Color(0.15f, 0.15f, 0.15f);
        
        Outline dropOutline = dropdownObj.AddComponent<Outline>();
        dropOutline.effectColor = buttonBorderColor;
        dropOutline.effectDistance = new Vector2(2, 2);
        
        windowModeDropdown = dropdownObj.AddComponent<TMP_Dropdown>();
        
        // Create template structure for dropdown
        CreateDropdownTemplate(dropdownObj);
        
        // Add options
        windowModeDropdown.ClearOptions();
        windowModeDropdown.AddOptions(new System.Collections.Generic.List<string> 
        { 
            "Fullscreen", 
            "Windowed", 
            "Borderless" 
        });
        
        // Set current value
        if (Screen.fullScreenMode == FullScreenMode.ExclusiveFullScreen)
            windowModeDropdown.value = 0;
        else if (Screen.fullScreenMode == FullScreenMode.Windowed)
            windowModeDropdown.value = 1;
        else
            windowModeDropdown.value = 2;
        
        windowModeDropdown.onValueChanged.AddListener(OnWindowModeChanged);
    }
    
    private void CreateDropdownTemplate(GameObject dropdownObj)
    {
        // Caption Text
        GameObject captionObj = new GameObject("Caption");
        captionObj.transform.SetParent(dropdownObj.transform, false);
        
        RectTransform captionRect = captionObj.AddComponent<RectTransform>();
        captionRect.anchorMin = Vector2.zero;
        captionRect.anchorMax = Vector2.one;
        captionRect.offsetMin = new Vector2(10, 5);
        captionRect.offsetMax = new Vector2(-30, -5);
        
        TMP_Text captionText = captionObj.AddComponent<TextMeshProUGUI>();
        captionText.fontSize = 20;
        captionText.color = textColor;
        captionText.alignment = TextAlignmentOptions.Left;
        
        windowModeDropdown.captionText = captionText;
        
        // Arrow
        GameObject arrowObj = new GameObject("Arrow");
        arrowObj.transform.SetParent(dropdownObj.transform, false);
        
        RectTransform arrowRect = arrowObj.AddComponent<RectTransform>();
        arrowRect.anchorMin = new Vector2(1, 0.5f);
        arrowRect.anchorMax = new Vector2(1, 0.5f);
        arrowRect.sizeDelta = new Vector2(20, 20);
        arrowRect.anchoredPosition = new Vector2(-15, 0);
        
        TMP_Text arrowText = arrowObj.AddComponent<TextMeshProUGUI>();
        arrowText.text = "▼";
        arrowText.fontSize = 16;
        arrowText.color = textColor;
        arrowText.alignment = TextAlignmentOptions.Center;
        
        // Template
        GameObject templateObj = new GameObject("Template");
        templateObj.transform.SetParent(dropdownObj.transform, false);
        
        RectTransform templateRect = templateObj.AddComponent<RectTransform>();
        templateRect.anchorMin = new Vector2(0, 0);
        templateRect.anchorMax = new Vector2(1, 0);
        templateRect.pivot = new Vector2(0.5f, 1);
        templateRect.sizeDelta = new Vector2(0, 120);
        
        Image templateBg = templateObj.AddComponent<Image>();
        templateBg.color = new Color(0.1f, 0.1f, 0.1f);
        
        ScrollRect scrollRect = templateObj.AddComponent<ScrollRect>();
        
        // Viewport
        GameObject viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(templateObj.transform, false);
        
        RectTransform viewportRect = viewportObj.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        
        viewportObj.AddComponent<Image>().color = Color.clear;
        viewportObj.AddComponent<Mask>().showMaskGraphic = false;
        
        // Content
        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(viewportObj.transform, false);
        
        RectTransform contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = new Vector2(0, 40);
        
        // Item Template
        GameObject itemObj = new GameObject("Item");
        itemObj.transform.SetParent(contentObj.transform, false);
        
        RectTransform itemRect = itemObj.AddComponent<RectTransform>();
        itemRect.anchorMin = new Vector2(0, 0.5f);
        itemRect.anchorMax = new Vector2(1, 0.5f);
        itemRect.sizeDelta = new Vector2(0, 40);
        
        Toggle itemToggle = itemObj.AddComponent<Toggle>();
        
        // Item Background (for highlight)
        Image itemBg = itemObj.AddComponent<Image>();
        itemBg.color = Color.clear;
        
        // Item Checkmark (optional)
        GameObject itemCheckObj = new GameObject("Item Checkmark");
        itemCheckObj.transform.SetParent(itemObj.transform, false);
        
        RectTransform checkRect = itemCheckObj.AddComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(0, 0.5f);
        checkRect.anchorMax = new Vector2(0, 0.5f);
        checkRect.sizeDelta = new Vector2(20, 20);
        checkRect.anchoredPosition = new Vector2(15, 0);
        
        Image checkImage = itemCheckObj.AddComponent<Image>();
        checkImage.color = textColor;
        
        // Item Label
        GameObject itemLabelObj = new GameObject("Item Label");
        itemLabelObj.transform.SetParent(itemObj.transform, false);
        
        RectTransform labelRect = itemLabelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(35, 5);
        labelRect.offsetMax = new Vector2(-10, -5);
        
        TMP_Text itemText = itemLabelObj.AddComponent<TextMeshProUGUI>();
        itemText.fontSize = 18;
        itemText.color = textColor;
        itemText.alignment = TextAlignmentOptions.Left;
        
        itemToggle.targetGraphic = itemBg;
        itemToggle.graphic = checkImage;
        
        // Setup dropdown references
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        
        windowModeDropdown.template = templateRect;
        windowModeDropdown.itemText = itemText;
        
        templateObj.SetActive(false);
    }
    
    private void CreateCreditsPanel()
    {
        creditsPanel = new GameObject("CreditsPanel");
        creditsPanel.transform.SetParent(pauseCanvas.transform, false);
        
        RectTransform panelRect = creditsPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        
        Image panelBg = creditsPanel.AddComponent<Image>();
        panelBg.color = backgroundColor;
        
        // Credits Text
        GameObject textObj = new GameObject("CreditsText");
        textObj.transform.SetParent(creditsPanel.transform, false);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.2f, 0.25f);
        textRect.anchorMax = new Vector2(0.8f, 0.85f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        TMP_Text credits = textObj.AddComponent<TextMeshProUGUI>();
        credits.text = creditsText;
        credits.fontSize = 28;
        credits.color = textColor;
        credits.alignment = TextAlignmentOptions.Center;
        credits.lineSpacing = 10;
        
        // Back Button
        CreateBackButton(creditsPanel.transform, OnCreditsBackClicked);
    }
    
    #endregion
    
    #region UI HELPERS
    
    private TMP_Text CreateText(Transform parent, string text, int fontSize, FontStyles style, 
        Vector2 anchor, Vector2 size)
    {
        GameObject textObj = new GameObject("Text_" + text);
        textObj.transform.SetParent(parent, false);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(anchor.x, anchor.y);
        textRect.anchorMax = new Vector2(anchor.x, anchor.y);
        textRect.sizeDelta = size;
        
        TMP_Text tmpText = textObj.AddComponent<TextMeshProUGUI>();
        tmpText.text = text;
        tmpText.fontSize = fontSize;
        tmpText.fontStyle = style;
        tmpText.alignment = TextAlignmentOptions.Center;
        tmpText.color = textColor;
        
        return tmpText;
    }
    
    private void CreateSettingLabel(Transform parent, string text, int fontSize, FontStyles style)
    {
        GameObject labelObj = new GameObject("Label_" + text);
        labelObj.transform.SetParent(parent, false);
        
        RectTransform labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.sizeDelta = new Vector2(400, fontSize + 10);
        
        TMP_Text labelText = labelObj.AddComponent<TextMeshProUGUI>();
        labelText.text = text;
        labelText.fontSize = fontSize;
        labelText.fontStyle = style;
        labelText.color = textColor;
        labelText.alignment = TextAlignmentOptions.Left;
    }
    
    private Button CreateRetroButton(string text, Transform parent, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObj = new GameObject(text.Replace(" ", "") + "Button");
        buttonObj.transform.SetParent(parent, false);
        
        RectTransform btnRect = buttonObj.AddComponent<RectTransform>();
        btnRect.sizeDelta = new Vector2(280, 55);
        
        // Button background
        Image btnBg = buttonObj.AddComponent<Image>();
        btnBg.color = new Color(0, 0, 0, 0.8f);
        
        // Border outlines
        Outline outline = buttonObj.AddComponent<Outline>();
        outline.effectColor = buttonBorderColor;
        outline.effectDistance = new Vector2(3, 3);
        
        Outline outline2 = buttonObj.AddComponent<Outline>();
        outline2.effectColor = buttonBorderColor;
        outline2.effectDistance = new Vector2(-3, -3);
        
        Button button = buttonObj.AddComponent<Button>();
        
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0, 0, 0, 0.8f);
        colors.highlightedColor = buttonHoverColor;
        colors.pressedColor = new Color(0.5f, 0.5f, 0.5f);
        colors.selectedColor = buttonHoverColor;
        colors.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
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
        
        TMP_Text btnText = textObj.AddComponent<TextMeshProUGUI>();
        btnText.text = text;
        btnText.fontSize = 26;
        btnText.fontStyle = FontStyles.Bold;
        btnText.alignment = TextAlignmentOptions.Center;
        btnText.color = textColor;
        btnText.characterSpacing = 3;
        
        return button;
    }
    
    private void CreateBackButton(Transform parent, UnityEngine.Events.UnityAction onClick)
    {
        GameObject backBtn = new GameObject("BackButton");
        backBtn.transform.SetParent(parent, false);
        
        RectTransform backRect = backBtn.AddComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0.5f, 0.1f);
        backRect.anchorMax = new Vector2(0.5f, 0.1f);
        backRect.sizeDelta = new Vector2(180, 50);
        
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
        
        button.onClick.AddListener(onClick);
        button.onClick.AddListener(PlayClickSound);
        
        // Text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(backBtn.transform, false);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        TMP_Text backText = textObj.AddComponent<TextMeshProUGUI>();
        backText.text = "< BACK";
        backText.fontSize = 22;
        backText.fontStyle = FontStyles.Bold;
        backText.alignment = TextAlignmentOptions.Center;
        backText.color = textColor;
    }
    
    private void PlayClickSound()
    {
        if (sfxSource != null && clickSound != null)
        {
            sfxSource.PlayOneShot(clickSound);
        }
    }
    
    #endregion
    
    #region BUTTON CALLBACKS
    
    private void OnResumeClicked()
    {
        Resume();
    }
    
    private void OnSettingsClicked()
    {
        pausePanel.SetActive(false);
        settingsPanel.SetActive(true);
    }
    
    private void OnCreditsClicked()
    {
        pausePanel.SetActive(false);
        creditsPanel.SetActive(true);
    }
    
    private void OnQuitClicked()
    {
        Time.timeScale = 1f; // Reset time scale before loading
        SceneManager.LoadScene(mainMenuSceneName);
    }
    
    private void OnSettingsBackClicked()
    {
        settingsPanel.SetActive(false);
        pausePanel.SetActive(true);
    }
    
    private void OnCreditsBackClicked()
    {
        creditsPanel.SetActive(false);
        pausePanel.SetActive(true);
    }
    
    private void OnFullscreenToggled(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        PlayerPrefs.SetInt("Fullscreen", isFullscreen ? 1 : 0);
        
        // Sync dropdown
        if (windowModeDropdown != null)
        {
            windowModeDropdown.value = isFullscreen ? 0 : 1;
        }
    }
    
    private void OnWindowModeChanged(int index)
    {
        switch (index)
        {
            case 0: // Fullscreen
                Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen;
                if (fullscreenToggle != null) fullscreenToggle.isOn = true;
                break;
            case 1: // Windowed
                Screen.fullScreenMode = FullScreenMode.Windowed;
                if (fullscreenToggle != null) fullscreenToggle.isOn = false;
                break;
            case 2: // Borderless
                Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                if (fullscreenToggle != null) fullscreenToggle.isOn = true;
                break;
        }
        
        PlayerPrefs.SetInt("WindowMode", index);
    }
    
    #endregion
    
    private void OnDestroy()
    {
        // Ensure time scale is reset if destroyed while paused
        if (isPaused)
        {
            Time.timeScale = 1f;
        }
    }
}
