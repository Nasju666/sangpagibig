using UnityEngine;

/// <summary>
/// Handles scene setup for multiplayer.
/// Disables the original scene player when in multiplayer mode,
/// so network-spawned players can work correctly.
/// Attach this to an empty GameObject in your game scene(s).
/// </summary>
public class MultiplayerSceneManager : MonoBehaviour
{
    [Header("Scene Setup")]
    [Tooltip("The original player in the scene (will be disabled in multiplayer)")]
    public GameObject scenePlayer;
    
    [Tooltip("Auto-find player by tag if not assigned")]
    public string playerTag = "Player";

    private void Awake()
    {
        // In multiplayer mode, disable the scene player
        // Network players will be spawned by LobbyManager instead
        if (MainMenuManager.IsMultiplayer)
        {
            DisableScenePlayer();
        }
    }

    private void DisableScenePlayer()
    {
        // Try to find player if not assigned
        if (scenePlayer == null)
        {
            scenePlayer = GameObject.FindGameObjectWithTag(playerTag);
        }

        if (scenePlayer != null)
        {
            Debug.Log($"[MultiplayerSceneManager] Disabling scene player: {scenePlayer.name}");
            scenePlayer.SetActive(false);
        }
        else
        {
            Debug.LogWarning("[MultiplayerSceneManager] No scene player found to disable.");
        }
    }
}
