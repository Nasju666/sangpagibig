using UnityEngine;
using Photon.Pun;

/// <summary>
/// Network player component that syncs position and rotation.
/// Attach to your player prefab along with PhotonView and PhotonTransformView.
/// Place the prefab in a Resources folder for network instantiation.
/// </summary>
[RequireComponent(typeof(PhotonView))]
public class NetworkPlayer : MonoBehaviourPun, IPunObservable
{
    [Header("Components to Disable for Remote Players")]
    [Tooltip("These components will be disabled for players we don't control")]
    public MonoBehaviour[] localOnlyScripts;
    
    [Tooltip("These GameObjects will be disabled for remote players (e.g., camera)")]
    public GameObject[] localOnlyObjects;

    [Header("Sync Settings")]
    [Tooltip("How fast to interpolate remote player positions")]
    public float positionLerpSpeed = 10f;
    
    [Tooltip("How fast to interpolate remote player rotations")]
    public float rotationLerpSpeed = 10f;

    [Header("Player Info")]
    public string playerName;

    // Network sync variables
    private Vector3 networkPosition;
    private Quaternion networkRotation;

    private void Awake()
    {
        // Set player name
        playerName = photonView.Owner.NickName;
        if (string.IsNullOrEmpty(playerName))
        {
            playerName = $"Player {photonView.Owner.ActorNumber}";
        }
    }

    private void Start()
    {
        if (photonView.IsMine)
        {
            // This is OUR player - enable local controls
            Debug.Log($"[NetworkPlayer] This is MY player: {playerName}");
            
            // Set player nickname if not set
            if (string.IsNullOrEmpty(PhotonNetwork.NickName))
            {
                PhotonNetwork.NickName = $"Player_{Random.Range(1000, 9999)}";
            }
            
            // Explicitly enable local player components
            EnableLocalComponents();
        }
        else
        {
            // This is a REMOTE player - disable local-only components
            Debug.Log($"[NetworkPlayer] Remote player spawned: {playerName}");
            DisableLocalComponents();
        }

        // Initialize network position
        networkPosition = transform.position;
        networkRotation = transform.rotation;
    }

    /// <summary>
    /// Enable components for the local player
    /// </summary>
    private void EnableLocalComponents()
    {
        // Enable scripts
        foreach (var script in localOnlyScripts)
        {
            if (script != null)
            {
                script.enabled = true;
            }
        }

        // Enable GameObjects (like camera)
        foreach (var obj in localOnlyObjects)
        {
            if (obj != null)
            {
                obj.SetActive(true);
            }
        }

        // Enable player movement
        var playerMovement = GetComponent<PlayerMovement>();
        if (playerMovement != null)
        {
            playerMovement.enabled = true;
            Debug.Log("[NetworkPlayer] PlayerMovement enabled for local player");
        }

        var characterController = GetComponent<CharacterController>();
        if (characterController != null)
        {
            characterController.enabled = true;
        }

        // Enable camera if present
        var camera = GetComponentInChildren<Camera>();
        if (camera != null)
        {
            camera.enabled = true;
        }

        // Enable audio listener
        var audioListener = GetComponentInChildren<AudioListener>();
        if (audioListener != null)
        {
            audioListener.enabled = true;
        }

        // Enable MouseLook if present
        var mouseLook = GetComponentInChildren<MouseLook>();
        if (mouseLook != null)
        {
            mouseLook.enabled = true;
        }
    }

    private void Update()
    {
        // Only interpolate for remote players
        if (!photonView.IsMine)
        {
            // Smoothly move towards network position
            transform.position = Vector3.Lerp(
                transform.position, 
                networkPosition, 
                positionLerpSpeed * Time.deltaTime
            );
            
            // Smoothly rotate towards network rotation
            transform.rotation = Quaternion.Lerp(
                transform.rotation, 
                networkRotation, 
                rotationLerpSpeed * Time.deltaTime
            );
        }
    }

    /// <summary>
    /// Disable components that should only run on the local player
    /// </summary>
    private void DisableLocalComponents()
    {
        // Disable scripts
        foreach (var script in localOnlyScripts)
        {
            if (script != null)
            {
                script.enabled = false;
            }
        }

        // Disable GameObjects (like camera)
        foreach (var obj in localOnlyObjects)
        {
            if (obj != null)
            {
                obj.SetActive(false);
            }
        }

        // Also disable common local-player components
        var playerMovement = GetComponent<PlayerMovement>();
        if (playerMovement != null)
        {
            playerMovement.enabled = false;
        }

        var characterController = GetComponent<CharacterController>();
        if (characterController != null)
        {
            characterController.enabled = false;
        }

        // Disable audio listener if present
        var audioListener = GetComponentInChildren<AudioListener>();
        if (audioListener != null)
        {
            audioListener.enabled = false;
        }
    }

    // ==================== PHOTON SERIALIZATION ====================

    /// <summary>
    /// Called by Photon to sync data between players
    /// </summary>
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // We own this player: send our position and rotation
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
        }
        else
        {
            // Network player: receive their position and rotation
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
        }
    }

    // ==================== PUBLIC METHODS ====================

    /// <summary>
    /// Check if this is the local player
    /// </summary>
    public bool IsLocalPlayer => photonView.IsMine;

    /// <summary>
    /// Get the PhotonView component
    /// </summary>
    public PhotonView PhotonView => photonView;
}
