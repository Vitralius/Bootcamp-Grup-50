using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

// DEPRECATED: This system conflicts with PlayerSessionData. 
// Use PlayerSessionData instead for character selection.
// Keeping this file for reference but marking as obsolete.
[System.Obsolete("Use PlayerSessionData instead for character selection")]
public class CharacterSelectionManager : NetworkBehaviour
{
    [Header("Character Selection Settings")]
    [Tooltip("Allow multiple players to select the same character")]
    public bool allowDuplicateSelections = false;
    
    [Tooltip("Default character ID to assign to players who haven't selected")]
    public int defaultCharacterID = 0;
    
    // Network variables to store each player's character selection
    // Using a dictionary would be ideal, but NetworkVariable doesn't support it directly
    // So we'll use a custom networked list structure
    private NetworkList<PlayerCharacterSelection> playerSelections;
    
    // Events for UI and other systems to subscribe to
    public System.Action<ulong, int> OnPlayerCharacterChanged;
    public System.Action OnSelectionValidationChanged;
    
    // Static instance for easy access
    public static CharacterSelectionManager Instance { get; private set; }
    
    [System.Serializable]
    public struct PlayerCharacterSelection : INetworkSerializable, System.IEquatable<PlayerCharacterSelection>
    {
        public ulong playerId;
        public int characterId;
        public bool isReady;
        
        public PlayerCharacterSelection(ulong playerId, int characterId, bool isReady = false)
        {
            this.playerId = playerId;
            this.characterId = characterId;
            this.isReady = isReady;
        }
        
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref playerId);
            serializer.SerializeValue(ref characterId);
            serializer.SerializeValue(ref isReady);
        }
        
        public bool Equals(PlayerCharacterSelection other)
        {
            return playerId == other.playerId && characterId == other.characterId && isReady == other.isReady;
        }
        
        public override bool Equals(object obj)
        {
            return obj is PlayerCharacterSelection other && Equals(other);
        }
        
        public override int GetHashCode()
        {
            return System.HashCode.Combine(playerId, characterId, isReady);
        }
        
        public static bool operator ==(PlayerCharacterSelection left, PlayerCharacterSelection right)
        {
            return left.Equals(right);
        }
        
        public static bool operator !=(PlayerCharacterSelection left, PlayerCharacterSelection right)
        {
            return !left.Equals(right);
        }
    }
    
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
        
        // Initialize the network list
        playerSelections = new NetworkList<PlayerCharacterSelection>();
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Subscribe to network list changes
        playerSelections.OnListChanged += OnPlayerSelectionsChanged;
        
        // If we're the server, initialize default selections for connected players
        if (IsServer)
        {
            InitializeDefaultSelections();
        }
    }
    
    public override void OnNetworkDespawn()
    {
        if (playerSelections != null)
        {
            playerSelections.OnListChanged -= OnPlayerSelectionsChanged;
        }
        
        base.OnNetworkDespawn();
    }
    
    public override void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        
        base.OnDestroy();
    }
    
    /// <summary>
    /// Initializes default character selections for all connected players
    /// </summary>
    private void InitializeDefaultSelections()
    {
        if (!IsServer) return;
        
        var connectedClients = NetworkManager.Singleton.ConnectedClientsIds;
        
        foreach (var clientId in connectedClients)
        {
            if (!HasPlayerSelection(clientId))
            {
                var defaultSelection = new PlayerCharacterSelection(clientId, -1, false); // -1 means no selection
                playerSelections.Add(defaultSelection);
            }
        }
    }
    
    /// <summary>
    /// Requests a character selection change (called by clients)
    /// </summary>
    /// <param name="characterId">ID of character to select</param>
    public void RequestCharacterSelection(int characterId)
    {
        if (!IsSpawned) return;
        
        RequestCharacterSelectionServerRpc(characterId);
    }
    
    /// <summary>
    /// Server RPC to handle character selection requests
    /// </summary>
    /// <param name="characterId">Requested character ID</param>
    /// <param name="serverRpcParams">RPC parameters containing sender info</param>
    [ServerRpc(RequireOwnership = false)]
    private void RequestCharacterSelectionServerRpc(int characterId, ServerRpcParams serverRpcParams = default)
    {
        var senderId = serverRpcParams.Receive.SenderClientId;
        
        // Validate the character selection
        if (!IsValidCharacterSelection(senderId, characterId))
        {
            // Send rejection back to client
            RejectCharacterSelectionClientRpc(characterId, "Character selection not valid", 
                new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { senderId } } });
            return;
        }
        
        // Update or add the player's selection
        UpdatePlayerSelection(senderId, characterId, false); // Not ready by default when changing selection
        
        Debug.Log($"CharacterSelectionManager: Player {senderId} selected character {characterId}");
    }
    
    /// <summary>
    /// Client RPC to handle character selection rejections
    /// </summary>
    /// <param name="characterId">Rejected character ID</param>
    /// <param name="reason">Reason for rejection</param>
    [ClientRpc]
    private void RejectCharacterSelectionClientRpc(int characterId, string reason, ClientRpcParams clientRpcParams = default)
    {
        Debug.LogWarning($"CharacterSelectionManager: Character selection {characterId} rejected: {reason}");
        // UI systems can subscribe to handle rejection feedback
    }
    
    /// <summary>
    /// Updates a player's character selection in the network list
    /// </summary>
    /// <param name="playerId">Player ID</param>
    /// <param name="characterId">Character ID</param>
    /// <param name="isReady">Ready state</param>
    private void UpdatePlayerSelection(ulong playerId, int characterId, bool isReady)
    {
        if (!IsServer) return;
        
        // Find existing selection
        for (int i = 0; i < playerSelections.Count; i++)
        {
            if (playerSelections[i].playerId == playerId)
            {
                playerSelections[i] = new PlayerCharacterSelection(playerId, characterId, isReady);
                return;
            }
        }
        
        // Add new selection if not found
        playerSelections.Add(new PlayerCharacterSelection(playerId, characterId, isReady));
    }
    
    /// <summary>
    /// Validates if a character selection is allowed
    /// </summary>
    /// <param name="playerId">Player making the selection</param>
    /// <param name="characterId">Character ID to validate</param>
    /// <returns>True if selection is valid</returns>
    private bool IsValidCharacterSelection(ulong playerId, int characterId)
    {
        // Check if character exists in registry
        if (CharacterRegistry.Instance == null || !CharacterRegistry.Instance.HasCharacter(characterId))
        {
            return false;
        }
        
        // If duplicates are not allowed, check if character is already selected
        if (!allowDuplicateSelections)
        {
            foreach (var selection in playerSelections)
            {
                if (selection.playerId != playerId && selection.characterId == characterId)
                {
                    return false; // Character already selected by another player
                }
            }
        }
        
        return true;
    }
    
    /// <summary>
    /// Sets a player's ready state
    /// </summary>
    /// <param name="isReady">Ready state to set</param>
    public void SetPlayerReady(bool isReady)
    {
        if (!IsSpawned) return;
        
        SetPlayerReadyServerRpc(isReady);
    }
    
    /// <summary>
    /// Server RPC to handle ready state changes
    /// </summary>
    /// <param name="isReady">Ready state</param>
    /// <param name="serverRpcParams">RPC parameters</param>
    [ServerRpc(RequireOwnership = false)]
    private void SetPlayerReadyServerRpc(bool isReady, ServerRpcParams serverRpcParams = default)
    {
        var senderId = serverRpcParams.Receive.SenderClientId;
        
        // Find and update the player's ready state
        for (int i = 0; i < playerSelections.Count; i++)
        {
            if (playerSelections[i].playerId == senderId)
            {
                var selection = playerSelections[i];
                selection.isReady = isReady;
                playerSelections[i] = selection;
                return;
            }
        }
        
        // If player not found, add them with default character
        playerSelections.Add(new PlayerCharacterSelection(senderId, defaultCharacterID, isReady));
    }
    
    /// <summary>
    /// Gets a player's current character selection
    /// </summary>
    /// <param name="playerId">Player ID</param>
    /// <returns>Character ID, or -1 if no selection</returns>
    public int GetPlayerCharacterSelection(ulong playerId)
    {
        foreach (var selection in playerSelections)
        {
            if (selection.playerId == playerId)
            {
                return selection.characterId;
            }
        }
        
        return -1; // No selection found
    }
    
    /// <summary>
    /// Gets a player's ready state
    /// </summary>
    /// <param name="playerId">Player ID</param>
    /// <returns>True if player is ready</returns>
    public bool IsPlayerReady(ulong playerId)
    {
        foreach (var selection in playerSelections)
        {
            if (selection.playerId == playerId)
            {
                return selection.isReady;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Checks if a player has made a character selection
    /// </summary>
    /// <param name="playerId">Player ID</param>
    /// <returns>True if player has selected a character</returns>
    public bool HasPlayerSelection(ulong playerId)
    {
        return GetPlayerCharacterSelection(playerId) != -1;
    }
    
    /// <summary>
    /// Gets all current player selections
    /// </summary>
    /// <returns>Dictionary of player ID to character ID</returns>
    public Dictionary<ulong, int> GetAllPlayerSelections()
    {
        var selections = new Dictionary<ulong, int>();
        
        foreach (var selection in playerSelections)
        {
            if (selection.characterId != -1)
            {
                selections[selection.playerId] = selection.characterId;
            }
        }
        
        return selections;
    }
    
    /// <summary>
    /// Checks if all connected players are ready
    /// </summary>
    /// <returns>True if all players are ready</returns>
    public bool AreAllPlayersReady()
    {
        var connectedClients = NetworkManager.Singleton.ConnectedClientsIds;
        
        foreach (var clientId in connectedClients)
        {
            if (!IsPlayerReady(clientId) || !HasPlayerSelection(clientId))
            {
                return false;
            }
        }
        
        return connectedClients.Count > 0; // Ensure there's at least one player
    }
    
    /// <summary>
    /// Gets characters that are currently available for selection
    /// </summary>
    /// <param name="excludePlayerId">Player ID to exclude from duplicate check</param>
    /// <returns>Array of available character IDs</returns>
    public int[] GetAvailableCharacters(ulong excludePlayerId = 0)
    {
        if (CharacterRegistry.Instance == null)
            return new int[0];
        
        var allCharacters = CharacterRegistry.Instance.GetAllCharacters().Select(c => c.characterID).ToArray();
        
        if (allowDuplicateSelections)
        {
            return allCharacters;
        }
        
        // Filter out characters selected by other players
        var selectedByOthers = new HashSet<int>();
        foreach (var selection in playerSelections)
        {
            if (selection.playerId != excludePlayerId && selection.characterId != -1)
            {
                selectedByOthers.Add(selection.characterId);
            }
        }
        
        return allCharacters.Where(id => !selectedByOthers.Contains(id)).ToArray();
    }
    
    /// <summary>
    /// Clears all character selections (Server only)
    /// </summary>
    public void ClearAllSelections()
    {
        if (!IsServer) return;
        
        playerSelections.Clear();
        InitializeDefaultSelections();
    }
    
    /// <summary>
    /// Removes a player's selection when they disconnect
    /// </summary>
    /// <param name="playerId">Player ID to remove</param>
    public void RemovePlayerSelection(ulong playerId)
    {
        if (!IsServer) return;
        
        for (int i = playerSelections.Count - 1; i >= 0; i--)
        {
            if (playerSelections[i].playerId == playerId)
            {
                playerSelections.RemoveAt(i);
                break;
            }
        }
    }
    
    /// <summary>
    /// Network list change callback
    /// </summary>
    /// <param name="changeEvent">Change event data</param>
    private void OnPlayerSelectionsChanged(NetworkListEvent<PlayerCharacterSelection> changeEvent)
    {
        // Notify listeners about selection changes
        switch (changeEvent.Type)
        {
            case NetworkListEvent<PlayerCharacterSelection>.EventType.Add:
            case NetworkListEvent<PlayerCharacterSelection>.EventType.Value:
                var selection = changeEvent.Value;
                OnPlayerCharacterChanged?.Invoke(selection.playerId, selection.characterId);
                break;
        }
        
        // Notify about validation changes
        OnSelectionValidationChanged?.Invoke();
    }
    
    /// <summary>
    /// Gets selection info for debugging
    /// </summary>
    /// <returns>Debug string</returns>
    public string GetSelectionDebugInfo()
    {
        var info = $"Character Selections ({playerSelections.Count} players):\n";
        
        foreach (var selection in playerSelections)
        {
            var characterName = "None";
            if (selection.characterId != -1 && CharacterRegistry.Instance != null)
            {
                var characterData = CharacterRegistry.Instance.GetCharacterByID(selection.characterId);
                if (characterData != null)
                {
                    characterName = characterData.characterName;
                }
            }
            
            info += $"Player {selection.playerId}: {characterName} (ID: {selection.characterId}) - Ready: {selection.isReady}\n";
        }
        
        return info;
    }
}