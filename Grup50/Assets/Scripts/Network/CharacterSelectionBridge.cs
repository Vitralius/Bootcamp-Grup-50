using UnityEngine;
using Unity.Netcode;
using Unity.Services.Authentication;

/// <summary>
/// Bridges character selection data from lobby (PlayerSessionData) to gameplay (CharacterLoader).
/// Handles transferring character selections when transitioning from lobby to game.
/// </summary>
public class CharacterSelectionBridge : NetworkBehaviour
{
    public static CharacterSelectionBridge Instance { get; private set; }
    
    private PlayerSessionData playerSessionData;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    private void Start()
    {
        InitializeBridge();
    }
    
    private void InitializeBridge()
    {
        // Get PlayerSessionData
        playerSessionData = FindFirstObjectByType<PlayerSessionData>();
        if (playerSessionData == null)
        {
            Debug.LogError("CharacterSelectionBridge: PlayerSessionData not found! Bridge will not work.");
            return;
        }
        
        // Subscribe to PlayerSessionData events
        playerSessionData.OnPlayerCharacterChanged += OnPlayerCharacterChangedInSession;
        playerSessionData.OnPlayerReadyChanged += OnPlayerReadyChangedInSession;
        playerSessionData.OnPlayerJoined += OnPlayerJoinedSession;
        playerSessionData.OnPlayerLeft += OnPlayerLeftSession;
        
        Debug.Log("CharacterSelectionBridge: Initialized successfully with PlayerSessionData");
    }
    
    private void OnPlayerCharacterChangedInSession(string playerGuid, int characterId)
    {
        Debug.Log($"CharacterSelectionBridge: Player {playerGuid} changed character to {characterId}");
        
        // Store this information for when we transition to gameplay
        // The actual character application will happen in TransferSessionDataToGameplay()
    }
    
    private void OnPlayerReadyChangedInSession(string playerGuid, bool isReady)
    {
        Debug.Log($"CharacterSelectionBridge: Player {playerGuid} ready state changed to {isReady}");
        
        // Store this information for lobby management
        // The bridge doesn't need to do anything special here, 
        // as ready state is handled by the lobby system
    }
    
    private void OnPlayerJoinedSession(string playerGuid)
    {
        Debug.Log($"CharacterSelectionBridge: Player {playerGuid} joined session");
    }
    
    private void OnPlayerLeftSession(string playerGuid)
    {
        Debug.Log($"CharacterSelectionBridge: Player {playerGuid} left session");
    }
    
    public void TransferSessionDataToGameplay()
    {
        if (!IsServer || playerSessionData == null) return;
        
        // When transitioning from lobby to gameplay, ensure character data is properly transferred
        var connectedSessions = playerSessionData.GetConnectedPlayerSessions();
        
        foreach (var session in connectedSessions)
        {
            // In the actual game, you'd want to apply the character data to the player's actual character
            // This would typically be done in the game scene, not the lobby
            
            var characterData = CharacterRegistry.Instance?.GetCharacterByID(session.selectedCharacterId);
            if (characterData != null)
            {
                Debug.Log($"Player {session.playerName} will spawn with character {characterData.characterName}");
                
                // Here you would typically:
                // 1. Find the player's NetworkObject in the game world
                // 2. Get the CharacterLoader component
                // 3. Apply the character data
                
                // For now, we'll just log this information
                ApplyCharacterDataToPlayer(session.playerId.ToString(), characterData);
            }
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void ApplyCharacterDataToPlayerServerRpc(string playerGuid, int characterId)
    {
        if (!IsServer) return;
        
        var characterData = CharacterRegistry.Instance?.GetCharacterByID(characterId);
        if (characterData != null)
        {
            ApplyCharacterDataToPlayer(playerGuid, characterData);
        }
    }
    
    private void ApplyCharacterDataToPlayer(string playerGuid, CharacterData characterData)
    {
        // Find all CharacterLoader components in the scene
        var characterLoaders = FindObjectsByType<CharacterLoader>(FindObjectsSortMode.None);
        
        foreach (var loader in characterLoaders)
        {
            // Check if this CharacterLoader belongs to the player with the given GUID
            // This is a simplified approach - in a real game you'd have proper player identification
            
            if (loader.IsOwner && loader.IsLocalPlayer)
            {
                // Apply the character data
                loader.LoadCharacter(characterData);
                Debug.Log($"Applied character data {characterData.characterName} to player {playerGuid}");
                break;
            }
        }
    }
    
    /// <summary>
    /// Gets the PlayerSessionData instance
    /// </summary>
    /// <returns>PlayerSessionData instance</returns>
    public PlayerSessionData GetPlayerSessionData()
    {
        return playerSessionData;
    }
    
    /// <summary>
    /// Gets character selection data for a specific player
    /// </summary>
    /// <param name="playerGuid">Player GUID</param>
    /// <returns>Character ID, or -1 if not found</returns>
    public int GetPlayerCharacterSelection(string playerGuid)
    {
        if (playerSessionData == null) return -1;
        
        var session = playerSessionData.GetPlayerSession(playerGuid);
        return session?.selectedCharacterId ?? -1;
    }
    
    /// <summary>
    /// Gets all player character selections
    /// </summary>
    /// <returns>Dictionary of player GUID to character ID</returns>
    public System.Collections.Generic.Dictionary<string, int> GetAllPlayerSelections()
    {
        var selections = new System.Collections.Generic.Dictionary<string, int>();
        
        if (playerSessionData != null)
        {
            var sessions = playerSessionData.GetConnectedPlayerSessions();
            foreach (var session in sessions)
            {
                selections[session.playerId.ToString()] = session.selectedCharacterId;
            }
        }
        
        return selections;
    }
    
    public override void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        
        // Unsubscribe from events
        if (playerSessionData != null)
        {
            playerSessionData.OnPlayerCharacterChanged -= OnPlayerCharacterChangedInSession;
            playerSessionData.OnPlayerReadyChanged -= OnPlayerReadyChangedInSession;
            playerSessionData.OnPlayerJoined -= OnPlayerJoinedSession;
            playerSessionData.OnPlayerLeft -= OnPlayerLeftSession;
        }
        
        base.OnDestroy();
    }
}