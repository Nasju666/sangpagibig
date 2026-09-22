using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

/// <summary>
/// Manages room creation, joining, leaving, and player ready state.
/// Provides both auto-join and manual room management.
/// </summary>
public class LobbyManager : MonoBehaviourPunCallbacks
{
    [Header("Room Settings")]
    [Tooltip("Default room name if none specified")]
    public string defaultRoomName = "GameRoom";
    
    [Tooltip("Maximum players per room")]
    public byte maxPlayersPerRoom = 2;
    
    [Tooltip("Automatically join/create a room when connected")]
    public bool autoJoinRoom = false;

    [Header("Scene Loading")]
    [Tooltip("Scene to load when starting the game")]
    public string gameSceneName = "Tutorial";

    [Header("Player Spawning")]
    [Tooltip("Player prefab name in Resources folder")]
    public string playerPrefabName = "NetworkPlayer";
    
    [Tooltip("Spawn point for players (set in game scene)")]
    public Transform spawnPoint;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    // Ready system constants
    private const string PLAYER_READY_KEY = "IsReady";
    
    public static LobbyManager Instance { get; private set; }
    
    private bool hasSpawnedPlayer = false;

    // ==================== UNITY LIFECYCLE ====================

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Spawn player when game scene loads (not menu)
        if (PhotonNetwork.InRoom && MainMenuManager.IsMultiplayer && !hasSpawnedPlayer)
        {
            // Find spawn point in new scene
            var spawnObj = GameObject.FindGameObjectWithTag("SpawnPoint");
            if (spawnObj != null)
                spawnPoint = spawnObj.transform;
                
            SpawnPlayer();
        }
    }

    // ==================== READY SYSTEM ====================

    /// <summary>
    /// Toggle the local player's ready state
    /// </summary>
    public void ToggleReady()
    {
        bool currentReady = IsLocalPlayerReady();
        SetLocalPlayerReady(!currentReady);
    }

    /// <summary>
    /// Set the local player's ready state
    /// </summary>
    public void SetLocalPlayerReady(bool isReady)
    {
        if (!PhotonNetwork.InRoom) return;
        
        Hashtable props = new Hashtable
        {
            { PLAYER_READY_KEY, isReady }
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        
        Log($"Set ready state to: {isReady}");
        UpdateMenuReadyState();
    }

    /// <summary>
    /// Check if the local player is ready
    /// </summary>
    public bool IsLocalPlayerReady()
    {
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(PLAYER_READY_KEY, out object isReady))
        {
            return (bool)isReady;
        }
        return false;
    }

    /// <summary>
    /// Check if a specific player is ready
    /// </summary>
    public bool IsPlayerReady(Player player)
    {
        if (player.CustomProperties.TryGetValue(PLAYER_READY_KEY, out object isReady))
        {
            return (bool)isReady;
        }
        return false;
    }

    /// <summary>
    /// Check if all players in the room are ready
    /// </summary>
    public bool AreAllPlayersReady()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom.PlayerCount < 2)
            return false;

        foreach (Player player in PhotonNetwork.PlayerList)
        {
            if (!IsPlayerReady(player))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Get count of ready players
    /// </summary>
    public int GetReadyPlayerCount()
    {
        int count = 0;
        if (!PhotonNetwork.InRoom) return count;

        foreach (Player player in PhotonNetwork.PlayerList)
        {
            if (IsPlayerReady(player))
                count++;
        }
        return count;
    }

    // ==================== ROOM MANAGEMENT ====================

    /// <summary>
    /// Create a new room with the specified name
    /// </summary>
    public void CreateRoom(string roomName = "")
    {
        if (!PhotonNetwork.IsConnected)
        {
            Log("Not connected to Photon! Connect first.");
            UpdateMenuStatus("Not connected!");
            return;
        }

        string name = string.IsNullOrEmpty(roomName) ? defaultRoomName : roomName;
        
        RoomOptions options = new RoomOptions
        {
            MaxPlayers = maxPlayersPerRoom,
            IsVisible = true,
            IsOpen = true
        };

        Log($"Creating room: {name}");
        PhotonNetwork.CreateRoom(name, options);
    }

    /// <summary>
    /// Join an existing room by name
    /// </summary>
    public void JoinRoom(string roomName)
    {
        if (!PhotonNetwork.IsConnected)
        {
            Log("Not connected to Photon! Connect first.");
            UpdateMenuStatus("Not connected!");
            return;
        }

        Log($"Joining room: {roomName}");
        PhotonNetwork.JoinRoom(roomName);
    }

    /// <summary>
    /// Join a random available room, or create one if none exist
    /// </summary>
    public void JoinOrCreateRoom(string roomName = "")
    {
        if (!PhotonNetwork.IsConnected)
        {
            Log("Not connected to Photon! Connect first.");
            return;
        }

        string name = string.IsNullOrEmpty(roomName) ? defaultRoomName : roomName;
        
        RoomOptions options = new RoomOptions
        {
            MaxPlayers = maxPlayersPerRoom,
            IsVisible = true,
            IsOpen = true
        };

        Log($"Joining or creating room: {name}");
        PhotonNetwork.JoinOrCreateRoom(name, options, TypedLobby.Default);
    }

    /// <summary>
    /// Leave the current room
    /// </summary>
    public void LeaveRoom()
    {
        if (PhotonNetwork.InRoom)
        {
            Log("Leaving room...");
            SetLocalPlayerReady(false);
            PhotonNetwork.LeaveRoom();
            hasSpawnedPlayer = false;
        }
    }

    /// <summary>
    /// Start the multiplayer game (called by host only)
    /// </summary>
    public void StartMultiplayerGame()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Log("Only the host can start the game!");
            UpdateMenuStatus("Only host can start!");
            return;
        }

        if (PhotonNetwork.CurrentRoom.PlayerCount < 2)
        {
            Log("Need at least 2 players to start!");
            UpdateMenuStatus("Waiting for players...");
            return;
        }

        if (!AreAllPlayersReady())
        {
            Log("Not all players are ready!");
            UpdateMenuStatus("Waiting for all players to be ready...");
            return;
        }

        // Validate scene name
        if (string.IsNullOrEmpty(gameSceneName))
        {
            Log("ERROR: gameSceneName is empty! Set it in LobbyManager Inspector.");
            UpdateMenuStatus("Error: No scene configured!");
            Debug.LogError("[LobbyManager] gameSceneName is empty! Please set the scene name in the LobbyManager Inspector.");
            return;
        }

        Log($"Starting multiplayer game... Loading scene: {gameSceneName}");
        PhotonNetwork.LoadLevel(gameSceneName);
    }

    // ==================== CALLBACKS ====================

    public override void OnJoinedLobby()
    {
        Log("Joined lobby!");
        UpdateMenuStatus("Connected! Ready to play.");
        
        if (autoJoinRoom)
        {
            JoinOrCreateRoom();
        }
    }

    public override void OnJoinedRoom()
    {
        Log($"Joined room: {PhotonNetwork.CurrentRoom.Name}");
        Log($"Players in room: {PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers}");

        // Host is automatically ready, non-host needs to click ready
        if (PhotonNetwork.IsMasterClient)
        {
            SetLocalPlayerReady(true);
        }
        else
        {
            SetLocalPlayerReady(false);
        }
        
        UpdateMenuStatus($"In Room: {PhotonNetwork.CurrentRoom.Name}");
        UpdatePlayerCount();
        UpdateMenuReadyState();
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Log($"Failed to join room: {message}");
        UpdateMenuStatus($"Failed to join: {message}");
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Log($"Failed to create room: {message}");
        UpdateMenuStatus($"Failed to create: {message}");
    }

    public override void OnLeftRoom()
    {
        Log("Left room");
        hasSpawnedPlayer = false;
        UpdateMenuStatus("Left room");
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Log($"Player joined: {newPlayer.NickName}");
        UpdatePlayerCount();
        UpdateMenuStatus($"{newPlayer.NickName} joined!");
        UpdateMenuReadyState();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Log($"Player left: {otherPlayer.NickName}");
        UpdatePlayerCount();
        UpdateMenuStatus($"{otherPlayer.NickName} left");
        UpdateMenuReadyState();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        // When any player's properties change (including ready state)
        if (changedProps.ContainsKey(PLAYER_READY_KEY))
        {
            Log($"{targetPlayer.NickName} ready state changed to: {changedProps[PLAYER_READY_KEY]}");
            UpdateMenuReadyState();
            
            // Also update MultiplayerPanelGenerator if it exists
            var panelGen = FindObjectOfType<MultiplayerPanelGenerator>();
            if (panelGen != null)
            {
                panelGen.RefreshReadyUI();
            }
        }
    }

    // ==================== PLAYER SPAWNING ====================

    private void SpawnPlayer()
    {
        if (hasSpawnedPlayer) return;
        
        if (string.IsNullOrEmpty(playerPrefabName))
        {
            Log("No player prefab specified!");
            return;
        }

        Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        Quaternion spawnRot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
        
        // Add small random offset so players don't spawn on top of each other
        spawnPos += new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f));

        Log($"Spawning player at {spawnPos}");
        
        // This instantiates the player across the network
        PhotonNetwork.Instantiate(playerPrefabName, spawnPos, spawnRot);
        hasSpawnedPlayer = true;
    }

    // ==================== UI UPDATES ====================

    private void UpdateMenuStatus(string status)
    {
        var menu = FindObjectOfType<MainMenuManager>();
        if (menu != null)
        {
            menu.UpdateConnectionStatus(status);
        }
    }

    private void UpdatePlayerCount()
    {
        if (!PhotonNetwork.InRoom) return;
        
        var menu = FindObjectOfType<MainMenuManager>();
        if (menu != null)
        {
            menu.UpdatePlayerCount(
                PhotonNetwork.CurrentRoom.PlayerCount,
                PhotonNetwork.CurrentRoom.MaxPlayers
            );
        }
    }

    private void UpdateMenuReadyState()
    {
        var menu = FindObjectOfType<MainMenuManager>();
        if (menu != null)
        {
            bool isMaster = PhotonNetwork.IsMasterClient;
            bool isLocalReady = IsLocalPlayerReady();
            bool allReady = AreAllPlayersReady();
            int readyCount = GetReadyPlayerCount();
            int totalPlayers = PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.PlayerCount : 0;
            
            menu.UpdateReadyState(isMaster, isLocalReady, allReady, readyCount, totalPlayers);
        }
    }

    // ==================== HELPERS ====================

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[LobbyManager] {message}");
        }
    }
}
