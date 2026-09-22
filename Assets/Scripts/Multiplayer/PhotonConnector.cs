using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// Handles connection to Photon servers.
/// Attach this to a GameObject in your first/menu scene.
/// </summary>
public class PhotonConnector : MonoBehaviourPunCallbacks
{
    [Header("Settings")]
    [Tooltip("Automatically connect when the game starts")]
    public bool autoConnect = true;
    
    [Tooltip("Game version for matchmaking (players with same version can play together)")]
    public string gameVersion = "1.0";

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    public static PhotonConnector Instance { get; private set; }

    // Connection state
    public bool IsConnected => PhotonNetwork.IsConnected;
    public bool IsInLobby => PhotonNetwork.InLobby;
    public bool IsInRoom => PhotonNetwork.InRoom;

    private void Awake()
    {
        // Singleton pattern
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

    private void Start()
    {
        if (autoConnect && !PhotonNetwork.IsConnected)
        {
            Connect();
        }
    }

    /// <summary>
    /// Connect to Photon servers
    /// </summary>
    public void Connect()
    {
        if (PhotonNetwork.IsConnected)
        {
            Log("Already connected to Photon!");
            return;
        }

        Log("Connecting to Photon...");
        PhotonNetwork.GameVersion = gameVersion;
        PhotonNetwork.ConnectUsingSettings();
    }

    /// <summary>
    /// Disconnect from Photon servers
    /// </summary>
    public void Disconnect()
    {
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
            Log("Disconnected from Photon");
        }
    }

    // ==================== CALLBACKS ====================

    public override void OnConnectedToMaster()
    {
        Log("Connected to Master Server!");
        
        // Automatically join the lobby after connecting
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        Log("Joined Lobby! Ready to create or join rooms.");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Log($"Disconnected from Photon. Reason: {cause}");
    }

    // ==================== HELPERS ====================

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[PhotonConnector] {message}");
        }
    }

    // Display connection status in game (optional)
private void OnGUI()
    {
        if (!showDebugLogs) return;

        float vw, vh;
        Matrix4x4 previous = ResponsiveIMGUI.Begin(out vw, out vh);
        float margin = Mathf.Clamp(vw * 0.015f, 8f, 18f);
        float width = Mathf.Min(320f, Mathf.Max(180f, vw - margin * 2f));
        GUILayout.BeginArea(new Rect(margin, margin, width, 110f), GUI.skin.box);
        GUILayout.Label($"Photon Status: {PhotonNetwork.NetworkClientState}");
        if (IsInRoom)
        {
            GUILayout.Label($"Room: {PhotonNetwork.CurrentRoom.Name}");
            GUILayout.Label($"Players: {PhotonNetwork.CurrentRoom.PlayerCount}");
        }
        GUILayout.EndArea();
        ResponsiveIMGUI.End(previous);
    }
}
