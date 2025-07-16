using UnityEngine;
using Unity.Netcode;
using Unity.Services.Authentication;

public class CharacterSelectionBridge : NetworkBehaviour
{
    public static CharacterSelectionBridge Instance { get; private set; }
    
    private PlayerSessionData playerSessionData;
    private CharacterSelectionManager characterSelectionManager;
    
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
            Debug.LogError("PlayerSessionData not found! Bridge will not work.");
            return;
        }
        
        // Get CharacterSelectionManager
        characterSelectionManager = FindFirstObjectByType<CharacterSelectionManager>();
        if (characterSelectionManager == null)
        {
            Debug.LogError("CharacterSelectionManager not found! Bridge will not work.");
            return;
        }
        
        // Subscribe to PlayerSessionData events
        playerSessionData.OnPlayerCharacterChanged += OnPlayerCharacterChangedInSession;
        playerSessionData.OnPlayerReadyChanged += OnPlayerReadyChangedInSession;
        
        // Subscribe to CharacterSelectionManager events
        characterSelectionManager.OnPlayerCharacterChanged += OnPlayerCharacterChangedInManager;
        
        Debug.Log("CharacterSelectionBridge initialized successfully");
    }
    
    private void OnPlayerCharacterChangedInSession(string playerGuid, int characterId)
    {
        // Convert from PlayerSessionData to CharacterSelectionManager
        // We need to find the NetworkManager client ID for this player
        
        // For now, we'll use a simple approach - in a real game you'd need proper mapping
        // This is a limitation of bridging between two different systems
        
        var currentPlayerSession = playerSessionData.GetCurrentPlayerSession();
        if (currentPlayerSession.HasValue && currentPlayerSession.Value.playerId.ToString() == playerGuid)
        {
            // This is the local player changing character
            var localClientId = NetworkManager.Singleton.LocalClientId;
            
            // Update the old character selection manager
            if (characterSelectionManager != null)
            {
                characterSelectionManager.RequestCharacterSelection(characterId);
            }
        }
    }
    
    private void OnPlayerReadyChangedInSession(string playerGuid, bool isReady)
    {
        // Similar to character change, update the old system
        var currentPlayerSession = playerSessionData.GetCurrentPlayerSession();
        if (currentPlayerSession.HasValue && currentPlayerSession.Value.playerId.ToString() == playerGuid)
        {
            var localClientId = NetworkManager.Singleton.LocalClientId;
            
            if (characterSelectionManager != null)
            {
                characterSelectionManager.SetPlayerReady(isReady);
            }
        }
    }
    
    private void OnPlayerCharacterChangedInManager(ulong playerId, int characterId)
    {
        // This handles changes from the old system to the new system
        // In practice, you'd typically only use one system, but this provides compatibility
        
        Debug.Log($"Character changed in manager: Player {playerId} -> Character {characterId}");
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
    
    public PlayerSessionData GetPlayerSessionData()
    {
        return playerSessionData;
    }
    
    public CharacterSelectionManager GetCharacterSelectionManager()
    {
        return characterSelectionManager;
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
        }
        
        if (characterSelectionManager != null)
        {
            characterSelectionManager.OnPlayerCharacterChanged -= OnPlayerCharacterChangedInManager;
        }
        
        base.OnDestroy();
    }
}