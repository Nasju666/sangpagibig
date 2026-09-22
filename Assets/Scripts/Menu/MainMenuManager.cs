using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Main Menu Manager - Handles all menu functionality including
/// scene transitions, settings, game mode selection, and UI interactions.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    [Header("Scene Settings")]
    [Tooltip("Name of the scene to load when Play is clicked")]
    [SerializeField] private string gameSceneName = "Tutorial";
    
    [Header("Main Menu UI")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private GameObject settingsPanel;
    
    [Header("Game Mode Selection")]
    [SerializeField] private GameObject modeSelectionPanel;
    [SerializeField] private Button singlePlayerButton;
    [SerializeField] private Button multiplayerButton;
    [SerializeField] private Button modeBackButton;
    
    [Header("Multiplayer Lobby")]
    [SerializeField] private GameObject multiplayerLobbyPanel;
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button joinRoomButton;
    [SerializeField] private Button lobbyBackButton;
    [SerializeField] private TMP_InputField roomCodeInput;
    [SerializeField] private TMP_Text connectionStatusText;
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private TMP_Text readyStatusText;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button readyButton;
    [SerializeField] private TMP_Text startButtonText;
    
    [Header("Audio")]
    [SerializeField] private AudioSource menuMusic;
    [SerializeField] private AudioSource buttonClickSound;
    
    [Header("Transition Settings")]
    [SerializeField] private float transitionDelay = 0.5f;
    [SerializeField] private Animator fadeAnimator;

    // Game mode
    public static bool IsMultiplayer { get; private set; } = false;
    public static string RoomCode { get; private set; } = "";
    
    private void Start()
    {
        // Ensure all panels are hidden at start
        HideAllPanels();
            
        // Setup button listeners
        SetupButtonListeners();
        
        // Start menu music if available
        if (menuMusic != null && !menuMusic.isPlaying)
            menuMusic.Play();
            
        // Unlock cursor for menu
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void HideAllPanels()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
        if (modeSelectionPanel != null)
            modeSelectionPanel.SetActive(false);
        if (multiplayerLobbyPanel != null)
            multiplayerLobbyPanel.SetActive(false);
    }
    
    private void SetupButtonListeners()
    {
        // Main menu buttons
        if (playButton != null)
            playButton.onClick.AddListener(OnPlayClicked);
        if (settingsButton != null)
            settingsButton.onClick.AddListener(OnSettingsClicked);
        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitClicked);
            
        // Mode selection buttons
        if (singlePlayerButton != null)
            singlePlayerButton.onClick.AddListener(OnSinglePlayerClicked);
        if (multiplayerButton != null)
            multiplayerButton.onClick.AddListener(OnMultiplayerClicked);
        if (modeBackButton != null)
            modeBackButton.onClick.AddListener(OnModeBackClicked);
            
        // Multiplayer lobby buttons
        if (createRoomButton != null)
            createRoomButton.onClick.AddListener(OnCreateRoomClicked);
        if (joinRoomButton != null)
            joinRoomButton.onClick.AddListener(OnJoinRoomClicked);
        if (lobbyBackButton != null)
            lobbyBackButton.onClick.AddListener(OnLobbyBackClicked);
        if (startGameButton != null)
            startGameButton.onClick.AddListener(OnStartMultiplayerGameClicked);
        if (readyButton != null)
            readyButton.onClick.AddListener(OnReadyClicked);
    }
    
    // ==================== MAIN MENU ====================
    
    /// <summary>
    /// Called when Play button is clicked - shows mode selection
    /// </summary>
    public void OnPlayClicked()
    {
        PlayButtonSound();
        if (modeSelectionPanel != null)
            modeSelectionPanel.SetActive(true);
    }
    
    /// <summary>
    /// Called when Settings button is clicked - opens settings panel
    /// </summary>
    public void OnSettingsClicked()
    {
        PlayButtonSound();
        if (settingsPanel != null)
            settingsPanel.SetActive(!settingsPanel.activeSelf);
    }
    
    /// <summary>
    /// Called when Quit button is clicked - exits the game
    /// </summary>
    public void OnQuitClicked()
    {
        PlayButtonSound();
        Debug.Log("Quitting game...");
        
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
    
    // ==================== MODE SELECTION ====================
    
    /// <summary>
    /// Start game in single player mode
    /// </summary>
    public void OnSinglePlayerClicked()
    {
        PlayButtonSound();
        IsMultiplayer = false;
        Debug.Log("Starting Single Player mode...");
        StartCoroutine(LoadGameScene());
    }
    
    /// <summary>
    /// Open multiplayer lobby
    /// </summary>
    public void OnMultiplayerClicked()
    {
        PlayButtonSound();
        IsMultiplayer = true;
        
        if (modeSelectionPanel != null)
            modeSelectionPanel.SetActive(false);
        if (multiplayerLobbyPanel != null)
            multiplayerLobbyPanel.SetActive(true);
            
        // Start connecting to Photon
        ConnectToPhoton();
    }
    
    /// <summary>
    /// Go back from mode selection to main menu
    /// </summary>
    public void OnModeBackClicked()
    {
        PlayButtonSound();
        if (modeSelectionPanel != null)
            modeSelectionPanel.SetActive(false);
    }
    
    // ==================== MULTIPLAYER LOBBY ====================
    
    private void ConnectToPhoton()
    {
        UpdateConnectionStatus("Connecting...");
        
        // Find and use PhotonConnector
        var connector = FindObjectOfType<PhotonConnector>();
        if (connector != null)
        {
            connector.Connect();
        }
        else
        {
            Debug.LogWarning("PhotonConnector not found! Make sure it's in the scene.");
            UpdateConnectionStatus("Error: No PhotonConnector");
        }
    }
    
    public void OnCreateRoomClicked()
    {
        PlayButtonSound();
        
        // Generate random room code
        RoomCode = GenerateRoomCode();
        
        var lobbyManager = FindObjectOfType<LobbyManager>();
        if (lobbyManager != null)
        {
            lobbyManager.autoJoinRoom = false;
            lobbyManager.maxPlayersPerRoom = 2;
            lobbyManager.CreateRoom(RoomCode);
        }
        
        UpdateConnectionStatus($"Room Created!\nCode: {RoomCode}");
        
        // Show room code to share with friend
        if (roomCodeInput != null)
            roomCodeInput.text = RoomCode;
    }
    
    public void OnJoinRoomClicked()
    {
        PlayButtonSound();
        
        if (roomCodeInput != null && !string.IsNullOrEmpty(roomCodeInput.text))
        {
            RoomCode = roomCodeInput.text.ToUpper();
            
            var lobbyManager = FindObjectOfType<LobbyManager>();
            if (lobbyManager != null)
            {
                lobbyManager.autoJoinRoom = false;
                lobbyManager.JoinRoom(RoomCode);
            }
            
            UpdateConnectionStatus($"Joining room: {RoomCode}...");
        }
        else
        {
            UpdateConnectionStatus("Enter a room code!");
        }
    }
    
    public void OnLobbyBackClicked()
    {
        PlayButtonSound();
        
        // Disconnect from Photon if connected
        var connector = FindObjectOfType<PhotonConnector>();
        if (connector != null && connector.IsConnected)
        {
            connector.Disconnect();
        }
        
        if (multiplayerLobbyPanel != null)
            multiplayerLobbyPanel.SetActive(false);
        if (modeSelectionPanel != null)
            modeSelectionPanel.SetActive(true);
    }
    
    public void OnStartMultiplayerGameClicked()
    {
        PlayButtonSound();
        
        var lobbyManager = FindObjectOfType<LobbyManager>();
        if (lobbyManager != null)
        {
            lobbyManager.StartMultiplayerGame();
        }
    }
    
    /// <summary>
    /// Called when Ready button is clicked (non-host players)
    /// </summary>
    public void OnReadyClicked()
    {
        PlayButtonSound();
        
        var lobbyManager = FindObjectOfType<LobbyManager>();
        if (lobbyManager != null)
        {
            lobbyManager.ToggleReady();
        }
    }
    
    // ==================== HELPERS ====================
    
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
    
    public void UpdateConnectionStatus(string status)
    {
        if (connectionStatusText != null)
            connectionStatusText.text = status;
    }
    
    public void UpdatePlayerCount(int count, int max)
    {
        if (playerCountText != null)
            playerCountText.text = $"Players: {count}/{max}";
    }
    
    /// <summary>
    /// Update ready state UI - called by LobbyManager
    /// </summary>
    public void UpdateReadyState(bool isMasterClient, bool isLocalReady, bool allReady, int readyCount, int totalPlayers)
    {
        // Update ready status text
        if (readyStatusText != null)
        {
            if (totalPlayers < 2)
                readyStatusText.text = "Waiting for players...";
            else
                readyStatusText.text = $"Ready: {readyCount}/{totalPlayers}";
        }
        
        if (isMasterClient)
        {
            // Host sees START GAME button
            if (startGameButton != null)
            {
                startGameButton.gameObject.SetActive(true);
                startGameButton.interactable = allReady && totalPlayers >= 2;
            }
            if (readyButton != null)
                readyButton.gameObject.SetActive(false);
                
            if (startButtonText != null)
            {
                if (totalPlayers < 2)
                    startButtonText.text = "WAITING...";
                else if (!allReady)
                    startButtonText.text = "WAITING...";
                else
                    startButtonText.text = "START GAME";
            }
        }
        else
        {
            // Non-host sees READY button
            if (startGameButton != null)
                startGameButton.gameObject.SetActive(false);
            if (readyButton != null)
            {
                readyButton.gameObject.SetActive(true);
                
                // Update ready button text
                var buttonText = readyButton.GetComponentInChildren<TMP_Text>();
                if (buttonText != null)
                {
                    buttonText.text = isLocalReady ? "READY ✓" : "READY?";
                }
            }
        }
    }
    
    /// <summary>
    /// Close the settings panel
    /// </summary>
    public void CloseSettings()
    {
        PlayButtonSound();
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }
    
    private System.Collections.IEnumerator LoadGameScene()
    {
        // Trigger fade out animation if available
        if (fadeAnimator != null)
            fadeAnimator.SetTrigger("FadeOut");
            
        yield return new WaitForSeconds(transitionDelay);
        
        // Load the game scene
        SceneManager.LoadScene(gameSceneName);
    }
    
    private void PlayButtonSound()
    {
        if (buttonClickSound != null)
            buttonClickSound.Play();
    }
    
    /// <summary>
    /// Load a specific scene by name (can be called from UI)
    /// </summary>
    public void LoadScene(string sceneName)
    {
        PlayButtonSound();
        StartCoroutine(LoadSceneWithDelay(sceneName));
    }
    
    private System.Collections.IEnumerator LoadSceneWithDelay(string sceneName)
    {
        if (fadeAnimator != null)
            fadeAnimator.SetTrigger("FadeOut");
            
        yield return new WaitForSeconds(transitionDelay);
        SceneManager.LoadScene(sceneName);
    }
}

