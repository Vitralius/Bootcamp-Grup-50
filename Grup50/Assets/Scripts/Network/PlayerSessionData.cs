using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Collections;

[System.Serializable]
public struct PlayerSessionInfo : INetworkSerializable, System.IEquatable<PlayerSessionInfo>
{
    public FixedString64Bytes playerId;
    public FixedString64Bytes playerName;
    public int selectedCharacterId;
    public bool isReady;
    public bool isConnected;
    
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref playerId);
        serializer.SerializeValue(ref playerName);
        serializer.SerializeValue(ref selectedCharacterId);
        serializer.SerializeValue(ref isReady);
        serializer.SerializeValue(ref isConnected);
    }
    
    public PlayerSessionInfo(string id, string name, int characterId = 0, bool ready = false, bool connected = true)
    {
        playerId = new FixedString64Bytes(id);
        playerName = new FixedString64Bytes(name);
        selectedCharacterId = characterId;
        isReady = ready;
        isConnected = connected;
    }
    
    public bool Equals(PlayerSessionInfo other)
    {
        return playerId.Equals(other.playerId) &&
               playerName.Equals(other.playerName) &&
               selectedCharacterId == other.selectedCharacterId &&
               isReady == other.isReady &&
               isConnected == other.isConnected;
    }
    
    public override bool Equals(object obj)
    {
        return obj is PlayerSessionInfo other && Equals(other);
    }
    
    public override int GetHashCode()
    {
        return System.HashCode.Combine(playerId, playerName, selectedCharacterId, isReady, isConnected);
    }
}

public class PlayerSessionData : NetworkBehaviour
{
    public static PlayerSessionData Instance { get; private set; }
    
    private NetworkList<PlayerSessionInfo> playerSessions;
    private Dictionary<string, string> playerIdToGuid = new Dictionary<string, string>();
    
    // Track clientId to playerGuid mapping for character selection fix
    private Dictionary<ulong, string> clientIdToGuid = new Dictionary<ulong, string>();
    
    public event Action<PlayerSessionInfo> OnPlayerSessionUpdated;
    public event Action<string> OnPlayerJoined;
    public event Action<string> OnPlayerLeft;
    public event Action<string, int> OnPlayerCharacterChanged;
    public event Action<string, bool> OnPlayerReadyChanged;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        playerSessions = new NetworkList<PlayerSessionInfo>();
    }
    
    public override void OnNetworkSpawn()
    {
        playerSessions.OnListChanged += OnPlayerSessionsChanged;
        
        // Register current player when spawning
        if (IsClient)
        {
            // Add a small delay to ensure authentication is ready
            Invoke(nameof(RegisterCurrentPlayer), 0.1f);
        }
    }
    
    private void RegisterCurrentPlayer()
    {
        // IMPROVED: Add network state validation before registration
        if (!IsSpawned)
        {
            Debug.LogWarning("PlayerSessionData: Cannot register player - not spawned yet. Retrying...");
            Invoke(nameof(RegisterCurrentPlayer), 0.5f);
            return;
        }
        
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("PlayerSessionData: Cannot register player - NetworkManager is null");
            return;
        }
        
        try
        {
            // Validate authentication service is ready
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                Debug.LogWarning("PlayerSessionData: Authentication not ready, retrying...");
                Invoke(nameof(RegisterCurrentPlayer), 0.5f);
                return;
            }
            
            string currentPlayerId = AuthenticationService.Instance.PlayerId;
            if (string.IsNullOrEmpty(currentPlayerId))
            {
                Debug.LogError("PlayerSessionData: Player ID is null or empty");
                return;
            }
            
            string playerGuid = GetOrCreatePlayerGuid(currentPlayerId);
            Debug.Log($"PlayerSessionData: Registering player {currentPlayerId} with GUID {playerGuid}");
            RegisterPlayerServerRpc(currentPlayerId, playerGuid);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"PlayerSessionData: Failed to register current player: {e.Message}");
            // Retry once more after delay
            Invoke(nameof(RegisterCurrentPlayer), 1f);
        }
    }
    
    public override void OnNetworkDespawn()
    {
        playerSessions.OnListChanged -= OnPlayerSessionsChanged;
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void RegisterPlayerServerRpc(string playerId, string playerGuid, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) 
        {
            Debug.LogWarning("PlayerSessionData: RegisterPlayerServerRpc called but not server");
            return;
        }
        
        if (!IsSpawned)
        {
            Debug.LogWarning("PlayerSessionData: RegisterPlayerServerRpc called but not spawned");
            return;
        }
        
        if (string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(playerGuid))
        {
            Debug.LogError($"PlayerSessionData: Invalid player data - PlayerId: {playerId}, GUID: {playerGuid}");
            return;
        }
        
        Debug.Log($"[SERVER] Registering player: {playerId} with GUID: {playerGuid}");
        
        playerIdToGuid[playerId] = playerGuid;
        
        // Map clientId to playerGuid for character selection fix
        ulong clientId = rpcParams.Receive.SenderClientId;
        clientIdToGuid[clientId] = playerGuid;
        
        // Check if player already exists (reconnection)
        int existingIndex = FindPlayerSessionIndex(playerGuid);
        if (existingIndex >= 0)
        {
            // Update existing player as connected
            PlayerSessionInfo existingSession = playerSessions[existingIndex];
            existingSession.isConnected = true;
            existingSession.playerId = new FixedString64Bytes(playerGuid); // Keep the GUID as ID
            playerSessions[existingIndex] = existingSession;
            
            Debug.Log($"[SERVER] Player {playerGuid} reconnected (ClientId: {clientId})");
        }
        else
        {
            // Add new player
            PlayerSessionInfo newSession = new PlayerSessionInfo(
                playerGuid,
                $"Player_{playerSessions.Count + 1}",
                0, // Default character
                false,
                true
            );
            
            playerSessions.Add(newSession);
            Debug.Log($"[SERVER] Added new player session: {playerGuid} (ClientId: {clientId})");
        }
        
        OnPlayerJoined?.Invoke(playerGuid);
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void UpdatePlayerCharacterServerRpc(string playerId, int characterId)
    {
        Debug.Log($"🔄 PLAYERSESSION: [SERVER] UpdatePlayerCharacterServerRpc RECEIVED - Player: {playerId}, Character: {characterId}");
        
        if (!IsServer) 
        {
            Debug.LogError($"🔄 PLAYERSESSION: [SERVER] UpdatePlayerCharacterServerRpc called but not server! IsServer = {IsServer}");
            return;
        }
        
        if (!IsSpawned)
        {
            Debug.LogError($"🔄 PLAYERSESSION: [SERVER] UpdatePlayerCharacterServerRpc called but not spawned! IsSpawned = {IsSpawned}");
            return;
        }
        
        if (string.IsNullOrEmpty(playerId))
        {
            Debug.LogError($"🔄 PLAYERSESSION: [SERVER] Invalid player ID in UpdatePlayerCharacterServerRpc: '{playerId}'");
            return;
        }
        
        if (characterId < 0)
        {
            Debug.LogError($"🔄 PLAYERSESSION: [SERVER] Invalid character ID {characterId} for player {playerId}");
            return;
        }
        
        Debug.Log($"🔄 PLAYERSESSION: [SERVER] All validations passed, processing request...");
        
        string playerGuid = GetPlayerGuid(playerId);
        if (string.IsNullOrEmpty(playerGuid)) 
        {
            Debug.LogError($"🔄 PLAYERSESSION: [SERVER] Could not find GUID for player {playerId}");
            return;
        }
        
        Debug.Log($"🔄 PLAYERSESSION: [SERVER] Found player GUID: {playerGuid}");
        
        int sessionIndex = FindPlayerSessionIndex(playerGuid);
        Debug.Log($"🔄 PLAYERSESSION: [SERVER] FindPlayerSessionIndex({playerGuid}) returned: {sessionIndex}");
        
        if (sessionIndex >= 0)
        {
            PlayerSessionInfo session = playerSessions[sessionIndex];
            int oldCharacterId = session.selectedCharacterId;
            session.selectedCharacterId = characterId;
            playerSessions[sessionIndex] = session;
            
            Debug.Log($"🔄 PLAYERSESSION: [SERVER] ✅ Updated character: {oldCharacterId} -> {characterId} for player {playerGuid}");
            Debug.Log($"    🎯 CRITICAL: Character ID successfully stored in session at index {sessionIndex}");
            
            OnPlayerCharacterChanged?.Invoke(playerGuid, characterId);
            Debug.Log($"    ✅ CRITICAL: OnPlayerCharacterChanged event fired");
        }
        else
        {
            Debug.LogError($"🔄 PLAYERSESSION: [SERVER] ❌ Could not find session for player GUID {playerGuid}");
            Debug.LogError($"    🎯 CRITICAL: Available sessions count: {playerSessions.Count}");
            for (int i = 0; i < playerSessions.Count; i++)
            {
                Debug.LogError($"    🎯 Session {i}: GUID='{playerSessions[i].playerId}', Name='{playerSessions[i].playerName}'");
            }
            Debug.LogError($"    🎯 CRITICAL: This means the player session was never created or GUID mismatch!");
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void UpdatePlayerReadyServerRpc(string playerId, bool isReady)
    {
        if (!IsServer) 
        {
            Debug.LogWarning("PlayerSessionData: UpdatePlayerReadyServerRpc called but not server");
            return;
        }
        
        if (!IsSpawned)
        {
            Debug.LogWarning("PlayerSessionData: UpdatePlayerReadyServerRpc called but not spawned");
            return;
        }
        
        if (string.IsNullOrEmpty(playerId))
        {
            Debug.LogError("PlayerSessionData: Invalid player ID in UpdatePlayerReadyServerRpc");
            return;
        }
        
        string playerGuid = GetPlayerGuid(playerId);
        if (string.IsNullOrEmpty(playerGuid)) 
        {
            Debug.LogError($"PlayerSessionData: Could not find GUID for player {playerId}");
            return;
        }
        
        int sessionIndex = FindPlayerSessionIndex(playerGuid);
        if (sessionIndex >= 0)
        {
            PlayerSessionInfo session = playerSessions[sessionIndex];
            session.isReady = isReady;
            playerSessions[sessionIndex] = session;
            
            Debug.Log($"[SERVER] Updated player {playerGuid} ready state to {isReady}");
            OnPlayerReadyChanged?.Invoke(playerGuid, isReady);
        }
    }
    
    public void SetPlayerCharacter(int characterId)
    {
        string currentPlayerId = AuthenticationService.Instance.PlayerId;
        Debug.Log($"🔄 PLAYERSESSION: SetPlayerCharacter called - Player: {currentPlayerId}, Character: {characterId}");
        Debug.Log($"    🎯 CRITICAL: AuthenticationService.Instance.PlayerId = '{currentPlayerId}'");
        Debug.Log($"    🎯 CRITICAL: IsServer = {IsServer}, IsSpawned = {IsSpawned}");
        Debug.Log($"    🎯 CRITICAL: About to call UpdatePlayerCharacterServerRpc({currentPlayerId}, {characterId})");
        
        UpdatePlayerCharacterServerRpc(currentPlayerId, characterId);
        
        Debug.Log($"    ✅ CRITICAL: UpdatePlayerCharacterServerRpc call completed");
    }
    
    public void SetPlayerReady(bool isReady)
    {
        string currentPlayerId = AuthenticationService.Instance.PlayerId;
        UpdatePlayerReadyServerRpc(currentPlayerId, isReady);
    }
    
    public void MarkPlayerDisconnected(string playerId)
    {
        if (!IsServer) return;
        
        string playerGuid = GetPlayerGuid(playerId);
        if (string.IsNullOrEmpty(playerGuid)) return;
        
        int sessionIndex = FindPlayerSessionIndex(playerGuid);
        if (sessionIndex >= 0)
        {
            PlayerSessionInfo session = playerSessions[sessionIndex];
            session.isConnected = false;
            playerSessions[sessionIndex] = session;
            
            Debug.Log($"[SERVER] Marked player {playerGuid} as disconnected");
            OnPlayerLeft?.Invoke(playerGuid);
        }
    }
    
    public PlayerSessionInfo? GetPlayerSession(string playerGuid)
    {
        int index = FindPlayerSessionIndex(playerGuid);
        return index >= 0 ? playerSessions[index] : null;
    }
    
    public PlayerSessionInfo? GetCurrentPlayerSession()
    {
        string currentPlayerId = AuthenticationService.Instance.PlayerId;
        string playerGuid = GetPlayerGuid(currentPlayerId);
        return !string.IsNullOrEmpty(playerGuid) ? GetPlayerSession(playerGuid) : null;
    }
    
    /// <summary>
    /// Get player session by clientId - CRITICAL FIX for character selection bug
    /// </summary>
    public PlayerSessionInfo? GetPlayerSessionByClientId(ulong clientId)
    {
        if (clientIdToGuid.ContainsKey(clientId))
        {
            string playerGuid = clientIdToGuid[clientId];
            return GetPlayerSession(playerGuid);
        }
        
        Debug.LogWarning($"PlayerSessionData: No playerGuid found for clientId {clientId}");
        return null;
    }
    
    public List<PlayerSessionInfo> GetAllPlayerSessions()
    {
        List<PlayerSessionInfo> sessions = new List<PlayerSessionInfo>();
        foreach (var session in playerSessions)
        {
            sessions.Add(session);
        }
        return sessions;
    }
    
    public List<PlayerSessionInfo> GetConnectedPlayerSessions()
    {
        List<PlayerSessionInfo> connectedSessions = new List<PlayerSessionInfo>();
        foreach (var session in playerSessions)
        {
            if (session.isConnected)
            {
                connectedSessions.Add(session);
            }
        }
        return connectedSessions;
    }
    
    public bool AreAllPlayersReady()
    {
        var connectedSessions = GetConnectedPlayerSessions();
        if (connectedSessions.Count == 0) return false;
        
        foreach (var session in connectedSessions)
        {
            if (!session.isReady)
            {
                return false;
            }
        }
        
        return true;
    }
    
    private int FindPlayerSessionIndex(string playerGuid)
    {
        for (int i = 0; i < playerSessions.Count; i++)
        {
            if (playerSessions[i].playerId.ToString() == playerGuid)
            {
                return i;
            }
        }
        return -1;
    }
    
    private string GetPlayerGuid(string playerId)
    {
        return playerIdToGuid.ContainsKey(playerId) ? playerIdToGuid[playerId] : null;
    }
    
    private string GetOrCreatePlayerGuid(string playerId)
    {
        if (playerIdToGuid.ContainsKey(playerId))
        {
            return playerIdToGuid[playerId];
        }
        
        // Check PlayerPrefs for existing GUID
        string persistentGuid = PlayerPrefs.GetString($"PlayerGuid_{playerId}", "");
        if (!string.IsNullOrEmpty(persistentGuid))
        {
            playerIdToGuid[playerId] = persistentGuid;
            return persistentGuid;
        }
        
        // Generate new GUID
        string newGuid = System.Guid.NewGuid().ToString();
        PlayerPrefs.SetString($"PlayerGuid_{playerId}", newGuid);
        PlayerPrefs.Save();
        
        playerIdToGuid[playerId] = newGuid;
        return newGuid;
    }
    
    private void OnPlayerSessionsChanged(NetworkListEvent<PlayerSessionInfo> changeEvent)
    {
        switch (changeEvent.Type)
        {
            case NetworkListEvent<PlayerSessionInfo>.EventType.Add:
                OnPlayerSessionUpdated?.Invoke(changeEvent.Value);
                break;
            case NetworkListEvent<PlayerSessionInfo>.EventType.RemoveAt:
                OnPlayerSessionUpdated?.Invoke(changeEvent.Value);
                break;
            case NetworkListEvent<PlayerSessionInfo>.EventType.Value:
                OnPlayerSessionUpdated?.Invoke(changeEvent.Value);
                break;
        }
    }
    
    public void ResetAllPlayerSessions()
    {
        if (!IsServer) return;
        
        for (int i = 0; i < playerSessions.Count; i++)
        {
            PlayerSessionInfo session = playerSessions[i];
            session.isReady = false;
            session.selectedCharacterId = 0;
            playerSessions[i] = session;
        }
        
        Debug.Log("[SERVER] Reset all player sessions");
    }
    
    /// <summary>
    /// Helper method to validate network state before operations
    /// </summary>
    private bool ValidateNetworkState(string methodName)
    {
        if (!IsSpawned)
        {
            Debug.LogWarning($"PlayerSessionData: {methodName} called but not spawned");
            return false;
        }
        
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError($"PlayerSessionData: {methodName} called but NetworkManager is null");
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Helper method to validate server state
    /// </summary>  
    private bool ValidateServerState(string methodName)
    {
        if (!ValidateNetworkState(methodName))
            return false;
            
        if (!IsServer)
        {
            Debug.LogWarning($"PlayerSessionData: {methodName} called but not server");
            return false;
        }
        
        return true;
    }
    
    public override void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        
        base.OnDestroy();
    }
}