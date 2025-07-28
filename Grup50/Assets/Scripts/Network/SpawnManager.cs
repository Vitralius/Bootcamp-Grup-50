using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class SpawnManager : NetworkBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private Vector3 defaultSpawnPosition = new Vector3(0, 5, 0);
    [SerializeField] private GameObject playerPrefab;
    
    private Dictionary<ulong, GameObject> spawnedPlayers = new Dictionary<ulong, GameObject>();
    
    public override void OnNetworkSpawn()
    {
        // CRITICAL FIX: Ensure Unity's automatic player spawning is disabled
        EnsureAutoSpawnIsDisabled();
        
        if (IsServer && NetworkManager.Singleton != null)
        {
            // Spawn players for all currently connected clients
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                SpawnPlayerAtPosition(client.ClientId);
            }
            
            // Listen for new connections
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
        else if (IsServer && NetworkManager.Singleton == null)
        {
            Debug.LogError("SpawnManager: NetworkManager.Singleton is null on server");
        }
    }
    
    private void OnClientConnected(ulong clientId)
    {
        if (IsServer)
        {
            SpawnPlayerAtPosition(clientId);
        }
    }
    
    private void OnClientDisconnected(ulong clientId)
    {
        if (IsServer && spawnedPlayers.ContainsKey(clientId))
        {
            if (spawnedPlayers[clientId] != null)
            {
                spawnedPlayers[clientId].GetComponent<NetworkObject>().Despawn();
            }
            spawnedPlayers.Remove(clientId);
        }
    }
    
    private void SpawnPlayerAtPosition(ulong clientId)
    {
        // Check if player already exists (from previous scene)
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            if (client.PlayerObject != null)
            {
                // Move existing player to spawn position
                MovePlayerToSpawn(client.PlayerObject, clientId);
                
                // Apply character selection if we're in game scene
                if (SceneTransitionManager.Instance != null && SceneTransitionManager.Instance.IsInGame())
                {
                    ApplyCharacterSelectionToExistingPlayer(client.PlayerObject, clientId);
                }
                return;
            }
        }
        
        // Spawn new player if needed
        Vector3 spawnPosition = GetSpawnPosition(clientId);
        
        if (playerPrefab != null)
        {
            GameObject playerObject = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
            NetworkObject netObj = playerObject.GetComponent<NetworkObject>();
            
            // CRITICAL FIX: Ensure proper ownership assignment
            Debug.Log($"[SpawnManager] BEFORE SpawnAsPlayerObject - ClientId: {clientId}, Current OwnerClientId: {netObj.OwnerClientId}");
            Debug.Log($"[SpawnManager] IsServer: {IsServer}, IsHost: {IsHost}, LocalClientId: {NetworkManager.Singleton.LocalClientId}");
            
            // Try manual ownership assignment first
            netObj.ChangeOwnership(clientId);
            Debug.Log($"[SpawnManager] AFTER ChangeOwnership - ClientId: {clientId}, OwnerClientId: {netObj.OwnerClientId}");
            
            netObj.SpawnAsPlayerObject(clientId, true); // destroyWithScene = true
            spawnedPlayers[clientId] = playerObject;
            
            Debug.Log($"[SpawnManager] AFTER SpawnAsPlayerObject - ClientId: {clientId}, Final OwnerClientId: {netObj.OwnerClientId}");
            
            // Verify ownership on the next frame
            StartCoroutine(VerifyOwnershipDelayed(clientId, netObj));
            Debug.Log($"Spawned new player for client {clientId} at position {spawnPosition}");
            
            // Apply character selection with proper network state validation
            if (SceneTransitionManager.Instance != null && SceneTransitionManager.Instance.IsInGame())
            {
                StartCoroutine(ApplyCharacterSelectionWhenReady(clientId));
            }
        }
    }
    
    private System.Collections.IEnumerator ApplyCharacterSelectionWhenReady(ulong clientId)
    {
        // Wait for NetworkVariable synchronization and PlayerSessionData to be ready
        int maxAttempts = 50; // 5 seconds max wait
        int attempts = 0;
        
        while (attempts < maxAttempts)
        {
            // Check if NetworkManager and PlayerSessionData are ready
            if (NetworkManager.Singleton != null && 
                PlayerSessionData.Instance != null && 
                spawnedPlayers.TryGetValue(clientId, out GameObject playerObject) &&
                playerObject != null)
            {
                var characterLoader = playerObject.GetComponent<UltraSimpleMeshSwapper>();
                if (characterLoader != null && characterLoader.IsSpawned)
                {
                    // NetworkVariable is ready, apply character data
                    ApplyCharacterSelectionToSinglePlayer(clientId, playerObject);
                    yield break;
                }
            }
            
            attempts++;
            yield return new WaitForSeconds(0.1f); // Check every 100ms
        }
        
        Debug.LogWarning($"SpawnManager: Failed to apply character selection to player {clientId} after {maxAttempts} attempts");
    }
    
    private void ApplyCharacterSelectionToSinglePlayer(ulong clientId, GameObject playerObject)
    {
        var characterLoader = playerObject.GetComponent<UltraSimpleMeshSwapper>();
        if (characterLoader == null)
        {
            Debug.LogWarning($"Player {clientId} does not have UltraSimpleMeshSwapper component!");
            return;
        }
        
        // Get character selection from PlayerSessionData
        var playerSessionData = PlayerSessionData.Instance;
        if (playerSessionData == null)
        {
            Debug.LogWarning("PlayerSessionData not found, cannot apply character selection");
            return;
        }
        
        // CRITICAL FIX: Get the SPECIFIC client's session instead of local player's session
        var clientSession = playerSessionData.GetPlayerSessionByClientId(clientId);
        
        // Use PersistentCharacterCache as primary source
        var persistentCache = PersistentCharacterCache.Instance;
        int selectedCharacterId = 0;
        
        if (persistentCache != null)
        {
            int cachedId = persistentCache.GetCachedCharacterSelection(clientId);
            if (cachedId > 0) // PersistentCharacterCache returns -1 for no selection, but we want > 0 for valid characters
            {
                selectedCharacterId = cachedId;
                Debug.Log($"SpawnManager: Got cached character selection for client {clientId}: {selectedCharacterId}");
            }
        }
        
        // Fallback: Try to find in CLIENT-SPECIFIC session (NOT local session!)
        if (selectedCharacterId == 0 && clientSession.HasValue)
        {
            selectedCharacterId = clientSession.Value.selectedCharacterId;
            Debug.Log($"SpawnManager: Using client {clientId} session character: {selectedCharacterId}");
        }
        
        // Second fallback: Try GUID-based lookup if still empty
        if (selectedCharacterId == 0 && clientSession.HasValue && persistentCache != null)
        {
            int guidCachedId = persistentCache.GetCachedCharacterSelectionByGuid(clientSession.Value.playerId.ToString());
            if (guidCachedId > 0)
            {
                selectedCharacterId = guidCachedId;
                Debug.Log($"SpawnManager: Using GUID-cached character for client {clientId}: {selectedCharacterId}");
            }
        }
        
        // If we have a character selection, apply it
        if (selectedCharacterId != 0)
        {
            var characterData = CharacterRegistry.Instance?.GetCharacterByID(selectedCharacterId);
            if (characterData != null)
            {
                Debug.Log($"SpawnManager: Applying character {characterData.characterName} to client {clientId}");
                
                // Cache the selection for backup
                if (persistentCache != null)
                {
                    persistentCache.CacheCharacterSelection(clientId, selectedCharacterId);
                    if (clientSession.HasValue)
                    {
                        persistentCache.CacheCharacterSelectionByGuid(clientSession.Value.playerId.ToString(), selectedCharacterId);
                    }
                }
                
                // Use NetworkVariable system for proper character sync
                characterLoader.SetNetworkCharacterId(selectedCharacterId);
                Debug.Log($"SpawnManager: Character loading initiated for client {clientId} with character {selectedCharacterId}");
            }
            else
            {
                Debug.LogWarning($"SpawnManager: Character ID {selectedCharacterId} not found in CharacterRegistry for client {clientId}");
            }
        }
        else
        {
            Debug.LogWarning($"SpawnManager: No character selection found for client {clientId}");
            
            // Debug: Show all available sessions
            var connectedSessions = playerSessionData.GetConnectedPlayerSessions();
            Debug.Log($"SpawnManager: Available sessions ({connectedSessions.Count}):");
            foreach (var session in connectedSessions)
            {
                Debug.Log($"  - {session.playerName} (ID: {session.playerId}, Character: {session.selectedCharacterId})");
            }
        }
    }
    
    
    private void ApplyCharacterSelectionToExistingPlayer(NetworkObject playerObject, ulong clientId)
    {
        // Use the same logic as single player application
        ApplyCharacterSelectionToSinglePlayer(clientId, playerObject.gameObject);
    }
    
    public void ApplyCharacterSelectionsToAllPlayers()
    {
        if (!IsServer) return;
        
        Debug.Log($"SpawnManager: ApplyCharacterSelectionsToAllPlayers called");
        
        // Use a simpler approach: Apply to all connected clients directly
        if (NetworkManager.Singleton == null)
        {
            Debug.LogWarning("SpawnManager: NetworkManager is null");
            return;
        }
        
        foreach (var clientPair in NetworkManager.Singleton.ConnectedClients)
        {
            var clientId = clientPair.Key;
            var client = clientPair.Value;
            
            if (client.PlayerObject != null)
            {
                Debug.Log($"SpawnManager: Applying character selection to client {clientId}");
                ApplyCharacterSelectionToSinglePlayer(clientId, client.PlayerObject.gameObject);
            }
            else
            {
                Debug.LogWarning($"SpawnManager: Client {clientId} has no PlayerObject");
            }
        }
    }
    
    // REMOVED: Old ClientRpc system replaced with NetworkVariable system in UltraSimpleMeshSwapper
    
    private void MovePlayerToSpawn(NetworkObject playerObject, ulong clientId)
    {
        Vector3 spawnPosition = GetSpawnPosition(clientId);
        
        // Move player to spawn position
        playerObject.transform.position = spawnPosition;
        
        // Reset CharacterController if present
        var charController = playerObject.GetComponent<CharacterController>();
        if (charController != null)
        {
            charController.enabled = false;
            charController.enabled = true;
        }
        
        Debug.Log($"Moved player {clientId} to spawn position: {spawnPosition}");
    }
    
    private Vector3 GetSpawnPosition(ulong clientId)
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            int spawnIndex = (int)(clientId % (ulong)spawnPoints.Length);
            return spawnPoints[spawnIndex].position;
        }
        
        return defaultSpawnPosition;
    }
    
    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }
    
    /// <summary>
    /// Ensure Unity's automatic player spawning is disabled to prevent ownership conflicts
    /// </summary>
    private void EnsureAutoSpawnIsDisabled()
    {
        if (NetworkManager.Singleton != null)
        {
            var networkConfig = NetworkManager.Singleton.NetworkConfig;
            
            // Force disable automatic player spawning
            if (networkConfig.PlayerPrefab != null || networkConfig.AutoSpawnPlayerPrefabClientSide)
            {
                Debug.LogWarning("[SpawnManager] Disabling Unity's automatic player spawning to prevent conflicts");
                networkConfig.PlayerPrefab = null;
                networkConfig.AutoSpawnPlayerPrefabClientSide = false;
                Debug.Log("[SpawnManager] ✅ Automatic player spawning disabled - SpawnManager has full control");
            }
            else
            {
                Debug.Log("[SpawnManager] ✅ Automatic player spawning already disabled");
            }
        }
    }
    
    /// <summary>
    /// Verify ownership is correctly assigned after spawning
    /// </summary>
    private System.Collections.IEnumerator VerifyOwnershipDelayed(ulong expectedClientId, NetworkObject netObj)
    {
        yield return new WaitForSeconds(0.5f); // Wait for network sync
        
        if (netObj != null)
        {
            Debug.Log($"[SpawnManager] OWNERSHIP VERIFICATION - Expected: {expectedClientId}, Actual: {netObj.OwnerClientId}");
            
            if (netObj.OwnerClientId != expectedClientId)
            {
                Debug.LogError($"[SpawnManager] ❌ OWNERSHIP MISMATCH! Client {expectedClientId} should own their player but it's owned by {netObj.OwnerClientId}");
                
                // Try to fix ownership
                if (IsServer)
                {
                    Debug.Log($"[SpawnManager] Attempting to fix ownership...");
                    netObj.ChangeOwnership(expectedClientId);
                    Debug.Log($"[SpawnManager] After fix attempt - OwnerClientId: {netObj.OwnerClientId}");
                }
            }
            else
            {
                Debug.Log($"[SpawnManager] ✅ Ownership is CORRECT for client {expectedClientId}");
            }
        }
    }
}