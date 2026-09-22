using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// MULTIPLAYER PANEL GENERATOR
/// Creates Mode Selection and Multiplayer Lobby panels
/// with the same retro style as RetroMenuGenerator.
/// 
/// Just attach to the same GameObject as RetroMenuGenerator!
/// </summary>
public class MultiplayerPanelGenerator : MonoBehaviourPunCallbacks
{
    [Header("Panel References (Auto-Generated)")]
    public GameObject modeSelectionPanel;
    public GameObject multiplayerLobbyPanel;
    
    [Header("Colors (Match Your Menu)")]
    [SerializeField] private Color backgroundColor = new Color(0, 0, 0, 0.95f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color buttonBorderColor = Color.white;
    [SerializeField] private Color buttonHoverColor = new Color(0.3f, 0.3f, 0.3f);
    [SerializeField] private Color inputFieldColor = new Color(0.1f, 0.1f, 0.1f);
    
    [Header("Game Settings")]
    [SerializeField] private string gameSceneName = "Tutorial";
    
    [Header("Audio")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip clickSound;
    
    // UI References
    private Canvas mainCanvas;
    private TMP_Text connectionStatusText;
    private TMP_Text playerCountText;
    private TMP_Text roomCodeDisplayText;
    private TMP_Text readyStatusText;
    private TMP_Text startButtonText;
    private TMP_InputField roomCodeInput;
    private Button startGameButton;
    private Button readyButton;
    
    // State
    private string currentRoomCode = "";
    public static bool IsMultiplayer { get; private set; } = false;
    
    private void Start()
    {
        // Wait a frame to ensure RetroMenuGenerator has created the Canvas
        StartCoroutine(InitializeAfterFrame());
    }
    
    private System.Collections.IEnumerator InitializeAfterFrame()
    {
        // Wait one frame for RetroMenuGenerator to create the Canvas
        yield return null;
        
        // Find the main canvas (created by RetroMenuGenerator)
        mainCanvas = FindObjectOfType<Canvas>();
        if (mainCanvas == null)
        {
            Debug.LogError("MultiplayerPanelGenerator: No Canvas found! Make sure RetroMenuGenerator is on this GameObject.");
            yield break;
        }
        
        // Find SFX source if not assigned
        if (sfxSource == null)
        {
            sfxSource = FindObjectOfType<AudioSource>();
        }
        
        GenerateMultiplayerPanels();
    }
    
    public void GenerateMultiplayerPanels()
    {
        CreateModeSelectionPanel();
        CreateMultiplayerLobbyPanel();
        
        // Hide panels initially
        if (modeSelectionPanel != null) modeSelectionPanel.SetActive(false);
        if (multiplayerLobbyPanel != null) multiplayerLobbyPanel.SetActive(false);
        
        Debug.Log("✅ Multiplayer Panels Generated!");
    }
    
    #region MODE SELECTION PANEL
    
    private void CreateModeSelectionPanel()
    {
        modeSelectionPanel = new GameObject("ModeSelectionPanel");
        modeSelectionPanel.transform.SetParent(mainCanvas.transform, false);
        
        RectTransform panelRect = modeSelectionPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        
        // Dark overlay background
        Image panelBg = modeSelectionPanel.AddComponent<Image>();
        panelBg.color = backgroundColor;
        
        // Title
        CreatePanelTitle(modeSelectionPanel.transform, "SELECT MODE", 0.75f);
        
        // Button Container
        GameObject buttonContainer = new GameObject("ButtonContainer");
        buttonContainer.transform.SetParent(modeSelectionPanel.transform, false);
        
        RectTransform btnContRect = buttonContainer.AddComponent<RectTransform>();
        btnContRect.anchorMin = new Vector2(0.5f, 0.35f);
        btnContRect.anchorMax = new Vector2(0.5f, 0.65f);
        btnContRect.sizeDelta = new Vector2(350, 200);
        
        VerticalLayoutGroup vlg = buttonContainer.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 25;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        
        // Buttons
        CreateRetroButton("SINGLE PLAYER", buttonContainer.transform, OnSinglePlayerClicked);
        CreateRetroButton("MULTIPLAYER", buttonContainer.transform, OnMultiplayerClicked);
        
        // Back Button
        CreateBackButton(modeSelectionPanel.transform, OnModeBackClicked, 0.12f);
    }
    
    #endregion
    
    #region MULTIPLAYER LOBBY PANEL
    
    private void CreateMultiplayerLobbyPanel()
    {
        multiplayerLobbyPanel = new GameObject("MultiplayerLobbyPanel");
        multiplayerLobbyPanel.transform.SetParent(mainCanvas.transform, false);
        
        RectTransform panelRect = multiplayerLobbyPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        
        // Dark overlay background
        Image panelBg = multiplayerLobbyPanel.AddComponent<Image>();
        panelBg.color = backgroundColor;
        
        // Title
        CreatePanelTitle(multiplayerLobbyPanel.transform, "MULTIPLAYER", 0.85f);
        
        // Connection Status
        connectionStatusText = CreateStatusText(multiplayerLobbyPanel.transform, "Connecting...", 0.72f);
        
        // Player Count
        playerCountText = CreateStatusText(multiplayerLobbyPanel.transform, "Players: 0/2", 0.65f);
        playerCountText.fontSize = 28;
        
        // Room Code Section
        CreateRoomCodeSection(multiplayerLobbyPanel.transform);
        
        // Action Buttons Container
        GameObject actionContainer = new GameObject("ActionButtons");
        actionContainer.transform.SetParent(multiplayerLobbyPanel.transform, false);
        
        RectTransform actionRect = actionContainer.AddComponent<RectTransform>();
        actionRect.anchorMin = new Vector2(0.5f, 0.25f);
        actionRect.anchorMax = new Vector2(0.5f, 0.42f);
        actionRect.sizeDelta = new Vector2(500, 120);
        
        HorizontalLayoutGroup hlg = actionContainer.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 30;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        
        // Create/Join Buttons
        CreateRetroButton("CREATE ROOM", actionContainer.transform, OnCreateRoomClicked, 200, 50);
        CreateRetroButton("JOIN ROOM", actionContainer.transform, OnJoinRoomClicked, 200, 50);
        
        // Ready Status Text
        readyStatusText = CreateStatusText(multiplayerLobbyPanel.transform, "Waiting for players...", 0.24f);
        readyStatusText.fontSize = 22;
        readyStatusText.color = new Color(0.8f, 0.8f, 0.8f);
        
        // Start Game Button (host only, disabled until all ready)
        GameObject startContainer = new GameObject("StartContainer");
        startContainer.transform.SetParent(multiplayerLobbyPanel.transform, false);
        
        RectTransform startContRect = startContainer.AddComponent<RectTransform>();
        startContRect.anchorMin = new Vector2(0.5f, 0.12f);
        startContRect.anchorMax = new Vector2(0.5f, 0.19f);
        startContRect.sizeDelta = new Vector2(300, 55);
        
        startGameButton = CreateRetroButton("WAITING...", startContainer.transform, OnStartGameClicked, 280, 50);
        startGameButton.interactable = false;
        startButtonText = startGameButton.GetComponentInChildren<TMP_Text>();
        
        // Ready Button (non-host players)
        GameObject readyContainer = new GameObject("ReadyContainer");
        readyContainer.transform.SetParent(multiplayerLobbyPanel.transform, false);
        
        RectTransform readyContRect = readyContainer.AddComponent<RectTransform>();
        readyContRect.anchorMin = new Vector2(0.5f, 0.12f);
        readyContRect.anchorMax = new Vector2(0.5f, 0.19f);
        readyContRect.sizeDelta = new Vector2(300, 55);
        
        readyButton = CreateRetroButton("READY?", readyContainer.transform, OnReadyClicked, 280, 50);
        readyButton.gameObject.SetActive(false); // Hidden initially, shown for non-host
        
        // Back Button
        CreateBackButton(multiplayerLobbyPanel.transform, OnLobbyBackClicked, 0.04f);
    }
    
    private void CreateRoomCodeSection(Transform parent)
    {
        GameObject roomSection = new GameObject("RoomCodeSection");
        roomSection.transform.SetParent(parent, false);
        
        RectTransform sectionRect = roomSection.AddComponent<RectTransform>();
        sectionRect.anchorMin = new Vector2(0.5f, 0.45f);
        sectionRect.anchorMax = new Vector2(0.5f, 0.58f);
        sectionRect.sizeDelta = new Vector2(400, 100);
        
        // Room Code Label
        GameObject labelObj = new GameObject("RoomCodeLabel");
        labelObj.transform.SetParent(roomSection.transform, false);
        
        RectTransform labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0, 0.6f);
        labelRect.anchorMax = new Vector2(1, 1f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        
        TMP_Text labelText = labelObj.AddComponent<TextMeshProUGUI>();
        labelText.text = "ROOM CODE:";
        labelText.fontSize = 24;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = textColor;
        
        // Room Code Display (shows generated code)
        roomCodeDisplayText = CreateStatusText(roomSection.transform, "----", 0.5f);
        roomCodeDisplayText.fontSize = 40;
        roomCodeDisplayText.fontStyle = FontStyles.Bold;
        roomCodeDisplayText.transform.SetParent(roomSection.transform, false);
        RectTransform displayRect = roomCodeDisplayText.GetComponent<RectTransform>();
        displayRect.anchorMin = new Vector2(0, 0.2f);
        displayRect.anchorMax = new Vector2(0.45f, 0.6f);
        displayRect.offsetMin = Vector2.zero;
        displayRect.offsetMax = Vector2.zero;
        
        // Room Code Input
        CreateRoomCodeInput(roomSection.transform);
    }
    
    private void CreateRoomCodeInput(Transform parent)
    {
        GameObject inputContainer = new GameObject("RoomCodeInput");
        inputContainer.transform.SetParent(parent, false);
        
        RectTransform inputContRect = inputContainer.AddComponent<RectTransform>();
        inputContRect.anchorMin = new Vector2(0.55f, 0.2f);
        inputContRect.anchorMax = new Vector2(1f, 0.6f);
        inputContRect.offsetMin = Vector2.zero;
        inputContRect.offsetMax = Vector2.zero;
        
        Image inputBg = inputContainer.AddComponent<Image>();
        inputBg.color = inputFieldColor;
        
        // Border
        Outline inputOutline = inputContainer.AddComponent<Outline>();
        inputOutline.effectColor = buttonBorderColor;
        inputOutline.effectDistance = new Vector2(2, 2);
        
        roomCodeInput = inputContainer.AddComponent<TMP_InputField>();
        roomCodeInput.characterLimit = 4;
        roomCodeInput.contentType = TMP_InputField.ContentType.Alphanumeric;
        
        // Text Area
        GameObject textArea = new GameObject("TextArea");
        textArea.transform.SetParent(inputContainer.transform, false);
        
        RectTransform textAreaRect = textArea.AddComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = new Vector2(10, 5);
        textAreaRect.offsetMax = new Vector2(-10, -5);
        
        // Placeholder
        GameObject placeholder = new GameObject("Placeholder");
        placeholder.transform.SetParent(textArea.transform, false);
        
        RectTransform placeholderRect = placeholder.AddComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = Vector2.zero;
        placeholderRect.offsetMax = Vector2.zero;
        
        TMP_Text placeholderText = placeholder.AddComponent<TextMeshProUGUI>();
        placeholderText.text = "Enter code";
        placeholderText.fontSize = 24;
        placeholderText.alignment = TextAlignmentOptions.Center;
        placeholderText.color = new Color(0.5f, 0.5f, 0.5f);
        placeholderText.fontStyle = FontStyles.Italic;
        
        // Input Text
        GameObject inputText = new GameObject("Text");
        inputText.transform.SetParent(textArea.transform, false);
        
        RectTransform inputTextRect = inputText.AddComponent<RectTransform>();
        inputTextRect.anchorMin = Vector2.zero;
        inputTextRect.anchorMax = Vector2.one;
        inputTextRect.offsetMin = Vector2.zero;
        inputTextRect.offsetMax = Vector2.zero;
        
        TMP_Text inputTextComponent = inputText.AddComponent<TextMeshProUGUI>();
        inputTextComponent.fontSize = 28;
        inputTextComponent.alignment = TextAlignmentOptions.Center;
        inputTextComponent.color = textColor;
        inputTextComponent.fontStyle = FontStyles.Bold;
        
        roomCodeInput.textViewport = textAreaRect;
        roomCodeInput.textComponent = inputTextComponent;
        roomCodeInput.placeholder = placeholderText;
        
        // Convert to uppercase on change
        roomCodeInput.onValueChanged.AddListener((text) => {
            if (roomCodeInput.text != text.ToUpper())
                roomCodeInput.text = text.ToUpper();
        });
    }
    
    #endregion
    
    #region UI HELPERS
    
    private void CreatePanelTitle(Transform parent, string title, float yAnchor)
    {
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(parent, false);
        
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, yAnchor);
        titleRect.anchorMax = new Vector2(0.5f, yAnchor + 0.1f);
        titleRect.sizeDelta = new Vector2(600, 80);
        
        TMP_Text titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = title;
        titleText.fontSize = 50;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = textColor;
        titleText.characterSpacing = 10;
    }
    
    private TMP_Text CreateStatusText(Transform parent, string text, float yAnchor)
    {
        GameObject textObj = new GameObject("StatusText");
        textObj.transform.SetParent(parent, false);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, yAnchor);
        textRect.anchorMax = new Vector2(0.5f, yAnchor + 0.05f);
        textRect.sizeDelta = new Vector2(500, 40);
        
        TMP_Text statusText = textObj.AddComponent<TextMeshProUGUI>();
        statusText.text = text;
        statusText.fontSize = 24;
        statusText.alignment = TextAlignmentOptions.Center;
        statusText.color = textColor;
        
        return statusText;
    }
    
    private Button CreateRetroButton(string text, Transform parent, UnityEngine.Events.UnityAction onClick, float width = 280, float height = 55)
    {
        GameObject buttonObj = new GameObject(text.Replace(" ", "") + "Button");
        buttonObj.transform.SetParent(parent, false);
        
        RectTransform btnRect = buttonObj.AddComponent<RectTransform>();
        btnRect.sizeDelta = new Vector2(width, height);
        
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
    
    private void CreateBackButton(Transform parent, UnityEngine.Events.UnityAction onClick, float yAnchor)
    {
        GameObject backBtn = new GameObject("BackButton");
        backBtn.transform.SetParent(parent, false);
        
        RectTransform backRect = backBtn.AddComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0.5f, yAnchor);
        backRect.anchorMax = new Vector2(0.5f, yAnchor + 0.06f);
        backRect.sizeDelta = new Vector2(180, 45);
        
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
    
    public void ShowModeSelection()
    {
        if (modeSelectionPanel != null)
            modeSelectionPanel.SetActive(true);
    }
    
    private void OnSinglePlayerClicked()
    {
        IsMultiplayer = false;
        Debug.Log("Starting Single Player...");
        UnityEngine.SceneManagement.SceneManager.LoadScene(gameSceneName);
    }
    
    private void OnMultiplayerClicked()
    {
        IsMultiplayer = true;
        
        if (modeSelectionPanel != null)
            modeSelectionPanel.SetActive(false);
        if (multiplayerLobbyPanel != null)
            multiplayerLobbyPanel.SetActive(true);
        
        // Connect to Photon
        ConnectToPhoton();
    }
    
    private void OnModeBackClicked()
    {
        if (modeSelectionPanel != null)
            modeSelectionPanel.SetActive(false);
    }
    
    private void OnCreateRoomClicked()
    {
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            UpdateStatus("Not connected yet!");
            return;
        }
        
        // Generate room code
        currentRoomCode = GenerateRoomCode();
        
        RoomOptions options = new RoomOptions
        {
            MaxPlayers = 2,
            IsVisible = true,
            IsOpen = true
        };
        
        PhotonNetwork.CreateRoom(currentRoomCode, options);
        UpdateStatus("Creating room...");
        roomCodeDisplayText.text = currentRoomCode;
    }
    
    private void OnJoinRoomClicked()
    {
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            UpdateStatus("Not connected yet!");
            return;
        }
        
        if (roomCodeInput != null && !string.IsNullOrEmpty(roomCodeInput.text))
        {
            currentRoomCode = roomCodeInput.text.ToUpper();
            PhotonNetwork.JoinRoom(currentRoomCode);
            UpdateStatus($"Joining {currentRoomCode}...");
        }
        else
        {
            UpdateStatus("Enter a room code!");
        }
    }
    
    private void OnStartGameClicked()
    {
        var lobbyManager = LobbyManager.Instance;
        if (lobbyManager != null)
        {
            lobbyManager.StartMultiplayerGame();
        }
        else if (PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom.PlayerCount >= 2)
        {
            PhotonNetwork.LoadLevel(gameSceneName);
        }
    }
    
    private void OnReadyClicked()
    {
        PlayClickSound();
        var lobbyManager = LobbyManager.Instance;
        if (lobbyManager != null)
        {
            lobbyManager.ToggleReady();
        }
    }
    
    private void OnLobbyBackClicked()
    {
        // Leave room if in one
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }
        
        if (multiplayerLobbyPanel != null)
            multiplayerLobbyPanel.SetActive(false);
        if (modeSelectionPanel != null)
            modeSelectionPanel.SetActive(true);
        
        // Reset display
        roomCodeDisplayText.text = "----";
        if (roomCodeInput != null) roomCodeInput.text = "";
        UpdatePlayerCount(0, 2);
    }
    
    #endregion
    
    #region PHOTON
    
    private void ConnectToPhoton()
    {
        if (PhotonNetwork.IsConnected)
        {
            UpdateStatus("Connected!");
            return;
        }
        
        UpdateStatus("Connecting...");
        PhotonNetwork.GameVersion = "1.0";
        PhotonNetwork.ConnectUsingSettings();
    }
    
    public override void OnConnectedToMaster()
    {
        UpdateStatus("Connected! Create or join a room.");
        PhotonNetwork.JoinLobby();
    }
    
    public override void OnJoinedRoom()
    {
        UpdateStatus($"In room: {PhotonNetwork.CurrentRoom.Name}");
        roomCodeDisplayText.text = PhotonNetwork.CurrentRoom.Name;
        UpdatePlayerCount(PhotonNetwork.CurrentRoom.PlayerCount, 2);
    }
    
    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        UpdateStatus($"Failed: {message}");
    }
    
    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        UpdateStatus($"Failed: {message}");
    }
    
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        UpdateStatus($"{newPlayer.NickName} joined!");
        UpdatePlayerCount(PhotonNetwork.CurrentRoom.PlayerCount, 2);
    }
    
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        UpdateStatus($"{otherPlayer.NickName} left");
        UpdatePlayerCount(PhotonNetwork.CurrentRoom.PlayerCount, 2);
        UpdateReadyUI();
    }
    
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        // Update UI when any player's ready state changes
        UpdateReadyUI();
    }
    
    public override void OnLeftRoom()
    {
        UpdateStatus("Left room");
        UpdatePlayerCount(0, 2);
    }
    
    public override void OnDisconnected(DisconnectCause cause)
    {
        UpdateStatus($"Disconnected: {cause}");
    }
    
    #endregion
    
    #region HELPERS
    
    private string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        char[] code = new char[4];
        for (int i = 0; i < 4; i++)
        {
            code[i] = chars[Random.Range(0, chars.Length)];
        }
        return new string(code);
    }
    
    private void UpdateStatus(string status)
    {
        if (connectionStatusText != null)
            connectionStatusText.text = status;
    }
    
    private void UpdatePlayerCount(int current, int max)
    {
        if (playerCountText != null)
            playerCountText.text = $"Players: {current}/{max}";
        
        UpdateReadyUI();
    }
    
    /// <summary>
    /// Public method to refresh ready UI from external scripts (like LobbyManager)
    /// </summary>
    public void RefreshReadyUI()
    {
        UpdateReadyUI();
    }
    
    private void UpdateReadyUI()
    {
        if (!PhotonNetwork.InRoom) return;
        
        var lobbyManager = LobbyManager.Instance;
        bool isMaster = PhotonNetwork.IsMasterClient;
        int playerCount = PhotonNetwork.CurrentRoom.PlayerCount;
        
        bool isLocalReady = lobbyManager != null ? lobbyManager.IsLocalPlayerReady() : false;
        bool allReady = lobbyManager != null ? lobbyManager.AreAllPlayersReady() : false;
        int readyCount = lobbyManager != null ? lobbyManager.GetReadyPlayerCount() : 0;
        
        // Update ready status text
        if (readyStatusText != null)
        {
            if (playerCount < 2)
                readyStatusText.text = "Waiting for players...";
            else
                readyStatusText.text = $"Ready: {readyCount}/{playerCount}";
        }
        
        if (isMaster)
        {
            // Host sees Start Game button
            if (startGameButton != null)
            {
                startGameButton.gameObject.SetActive(true);
                startGameButton.interactable = allReady && playerCount >= 2;
            }
            if (readyButton != null)
                readyButton.gameObject.SetActive(false);
                
            if (startButtonText != null)
            {
                if (playerCount < 2)
                    startButtonText.text = "WAITING...";
                else if (!allReady)
                    startButtonText.text = "WAITING...";
                else
                    startButtonText.text = "START GAME";
            }
        }
        else
        {
            // Non-host sees Ready button
            if (startGameButton != null)
                startGameButton.gameObject.SetActive(false);
            if (readyButton != null)
            {
                readyButton.gameObject.SetActive(true);
                var buttonText = readyButton.GetComponentInChildren<TMP_Text>();
                if (buttonText != null)
                {
                    buttonText.text = isLocalReady ? "READY ✓" : "READY?";
                }
            }
        }
    }
    
    #endregion
}
