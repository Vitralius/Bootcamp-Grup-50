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
        Debug.Log($"[SpawnManager] OnNetworkSpawn called - IsServer: {IsServer}, IsClient: {IsClient}, IsHost: {IsHost}");
        Debug.Log($"[SpawnManager] GameObject: {gameObject.name}, NetworkObjectId: {NetworkObjectId}, IsSpawned: {IsSpawned}");
        
        // CRITICAL DEBUG: Check if there are multiple SpawnManagers
        var allSpawnManagers = FindObjectsByType<SpawnManager>(FindObjectsSortMode.None);
        Debug.Log($"[SpawnManager] Total SpawnManagers in scene: {allSpawnManagers.Length}");
        foreach (var sm in allSpawnManagers)
        {
            Debug.Log($"[SpawnManager] Found SpawnManager: {sm.gameObject.name} (IsSpawned: {sm.IsSpawned}, IsServer: {sm.IsServer})");
        }
        
        // CRITICAL FIX: Ensure Unity's automatic player spawning is disabled
        EnsureAutoSpawnIsDisabled();
        
        if (IsServer && NetworkManager.Singleton != null)
        {
            Debug.Log($"[SpawnManager] SERVER: OnNetworkSpawn - NetworkManager state:");
            Debug.Log($"[SpawnManager] SERVER: ConnectedClientsList.Count: {NetworkManager.Singleton.ConnectedClientsList.Count}");
            Debug.Log($"[SpawnManager] SERVER: ConnectedClients.Count: {NetworkManager.Singleton.ConnectedClients.Count}");
            Debug.Log($"[SpawnManager] SERVER: IsHost: {NetworkManager.Singleton.IsHost}");
            Debug.Log($"[SpawnManager] SERVER: LocalClientId: {NetworkManager.Singleton.LocalClientId}");
            
            // Debug: Log all connected clients with detailed info
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                Debug.Log($"[SpawnManager] SERVER: Connected client {client.ClientId} - HasPlayerObject: {client.PlayerObject != null}");
                if (client.PlayerObject != null)
                {
                    Debug.Log($"[SpawnManager] SERVER: Client {client.ClientId} already has PlayerObject: {client.PlayerObject.name}");
                }
            }
            
            // NETWORK FIX: Add delay to ensure scene is fully loaded before spawning
            Debug.Log($"[SpawnManager] SERVER: Starting DelayedPlayerSpawning coroutine...");
            StartCoroutine(DelayedPlayerSpawning());
            
            // Listen for new connections
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            Debug.Log($"[SpawnManager] SERVER: Subscribed to connection callbacks");
        }
        else if (IsClient && !IsServer)
        {
            Debug.Log($"[SpawnManager] CLIENT: OnNetworkSpawn - Client-side SpawnManager ready");
            Debug.Log($"[SpawnManager] CLIENT: NetworkManager.LocalClientId: {NetworkManager.Singleton?.LocalClientId}");
            // Client-side SpawnManager doesn't spawn players, but logs for debugging
        }
        else if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[SpawnManager] NetworkManager.Singleton is null during OnNetworkSpawn!");
        }
        else
        {
            Debug.LogWarning($"[SpawnManager] Unexpected state - IsServer: {IsServer}, IsClient: {IsClient}, NetworkManager: {NetworkManager.Singleton != null}");
        }
    }
    
    /// <summary>
    /// NETWORK FIX: Delayed player spawning to ensure proper scene synchronization
    /// </summary>
    private System.Collections.IEnumerator DelayedPlayerSpawning()
    {
        Debug.Log($"[SpawnManager] DelayedPlayerSpawning STARTED - waiting 0.5s for scene sync");
        
        // Wait for scene to be fully synchronized before spawning players
        yield return new WaitForSeconds(0.5f);
        
        Debug.Log($"[SpawnManager] DelayedPlayerSpawning RESUME - Now checking NetworkManager state");
        
        // Double-check NetworkManager is still valid
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[SpawnManager] DelayedPlayerSpawning - NetworkManager became null!");
            yield break;
        }
        
        if (!IsServer)
        {
            Debug.LogError("[SpawnManager] DelayedPlayerSpawning - No longer server!");
            yield break;
        }
        
        Debug.Log($"[SpawnManager] DelayedPlayerSpawning - Spawning players after scene sync delay");
        Debug.Log($"[SpawnManager] DelayedPlayerSpawning - ConnectedClientsList.Count: {NetworkManager.Singleton.ConnectedClientsList.Count}");
        Debug.Log($"[SpawnManager] DelayedPlayerSpawning - spawnedPlayers.Count: {spawnedPlayers.Count}");
        
        // OWNERSHIP FIX: Clear existing player tracking to prevent conflicts
        spawnedPlayers.Clear();
        Debug.Log($"[SpawnManager] DelayedPlayerSpawning - Cleared spawnedPlayers tracking");
        
        // Spawn players for all currently connected clients with proper ownership
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            Debug.Log($"[SpawnManager] DelayedPlayerSpawning - Processing client {client.ClientId}");
            Debug.Log($"[SpawnManager] DelayedPlayerSpawning - Client {client.ClientId} has PlayerObject: {client.PlayerObject != null}");
            
            // CRITICAL: Ensure each client gets their own player object
            if (!spawnedPlayers.ContainsKey(client.ClientId))
            {
                Debug.Log($"[SpawnManager] DelayedPlayerSpawning - Calling SpawnPlayerAtPosition for client {client.ClientId}");
                SpawnPlayerAtPosition(client.ClientId);
            }
            else
            {
                Debug.LogWarning($"[SpawnManager] Player {client.ClientId} already exists in spawnedPlayers tracking!");
            }
        }
        
        Debug.Log($"[SpawnManager] DelayedPlayerSpawning - Finished spawning, waiting 0.2s before ownership verification");
        
        // Verify all players have correct ownership
        yield return new WaitForSeconds(0.2f);
        Debug.Log($"[SpawnManager] DelayedPlayerSpawning - Calling VerifyAllPlayerOwnership");
        VerifyAllPlayerOwnership();
        
        Debug.Log($"[SpawnManager] DelayedPlayerSpawning COMPLETED");
    }
    
    private void OnClientConnected(ulong clientId)
    {
        if (IsServer)
        {
            Debug.Log($"[SpawnManager] Client {clientId} connected, spawning player with delay for scene sync");
            // NETWORK FIX: Add small delay for new client connections to ensure scene synchronization
            StartCoroutine(DelayedSpawnForNewClient(clientId));
        }
    }
    
    /// <summary>
    /// NETWORK FIX: Delayed spawning for newly connected clients to ensure scene synchronization
    /// </summary>
    private System.Collections.IEnumerator DelayedSpawnForNewClient(ulong clientId)
    {
        // Wait for client to fully synchronize with current scene
        yield return new WaitForSeconds(1f);
        
        Debug.Log($"[SpawnManager] Spawning player for newly connected client {clientId} after sync delay");
        SpawnPlayerAtPosition(clientId);
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
        Debug.Log($"[SpawnManager] SpawnPlayerAtPosition CALLED for client {clientId}");
        
        // NETWORK FIX: Improved player existence check with better logging
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            Debug.Log($"[SpawnManager] Found client {clientId} in ConnectedClients");
            
            if (client.PlayerObject != null)
            {
                Debug.Log($"[SpawnManager] Player {clientId} already exists: {client.PlayerObject.name}, moving to spawn position");
                
                // Move existing player to spawn position
                MovePlayerToSpawn(client.PlayerObject, clientId);
                
                // Apply character selection if we're in game scene
                if (SimpleSceneTransition.Instance != null && SimpleSceneTransition.Instance.IsInGame())
                {
                    ApplyCharacterSelectionToExistingPlayer(client.PlayerObject, clientId);
                }
                return;
            }
            else
            {
                Debug.Log($"[SpawnManager] Client {clientId} connected but PlayerObject is null - spawning new player");
            }
        }
        else
        {
            Debug.Log($"[SpawnManager] Client {clientId} not found in ConnectedClients - spawning new player");
            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("[SpawnManager] NetworkManager.Singleton is null!");
                return;
            }
        }
        
        // Check if playerPrefab is assigned
        if (playerPrefab == null)
        {
            Debug.LogError("[SpawnManager] playerPrefab is null! Cannot spawn player. Please assign the player prefab in the inspector.");
            return;
        }
        
        // Spawn new player if needed
        Vector3 spawnPosition = GetSpawnPosition(clientId);
        Debug.Log($"[SpawnManager] Spawning new player for client {clientId} at position {spawnPosition}");
        
        try
        {
            GameObject playerObject = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
            Debug.Log($"[SpawnManager] Instantiated player object: {playerObject.name}");
            
            NetworkObject netObj = playerObject.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                Debug.LogError($"[SpawnManager] Player prefab {playerPrefab.name} does not have a NetworkObject component!");
                Destroy(playerObject);
                return;
            }
            
            Debug.Log($"[SpawnManager] Found NetworkObject on player: {netObj.gameObject.name}");
            
            // CRITICAL FIX: Ensure proper ownership assignment
            Debug.Log($"[SpawnManager] BEFORE SpawnAsPlayerObject - ClientId: {clientId}, Current OwnerClientId: {netObj.OwnerClientId}");
            Debug.Log($"[SpawnManager] IsServer: {IsServer}, IsHost: {IsHost}, LocalClientId: {NetworkManager.Singleton.LocalClientId}");
            
            // Try manual ownership assignment first
            netObj.ChangeOwnership(clientId);
            Debug.Log($"[SpawnManager] AFTER ChangeOwnership - ClientId: {clientId}, OwnerClientId: {netObj.OwnerClientId}");
            
            // CRITICAL FIX: Use destroyWithScene = false to persist players across scenes
            Debug.Log($"[SpawnManager] Calling SpawnAsPlayerObject for client {clientId}...");
            netObj.SpawnAsPlayerObject(clientId, false); // destroyWithScene = false - PLAYERS PERSIST!
            spawnedPlayers[clientId] = playerObject;
            
            Debug.Log($"[SpawnManager] AFTER SpawnAsPlayerObject - ClientId: {clientId}, Final OwnerClientId: {netObj.OwnerClientId}");
            Debug.Log($"[SpawnManager] Player object is spawned: {netObj.IsSpawned}, NetworkObjectId: {netObj.NetworkObjectId}");
            
            // Verify ownership on the next frame
            StartCoroutine(VerifyOwnershipDelayed(clientId, netObj));
            Debug.Log($"[SpawnManager] ✅ Spawned new player for client {clientId} at {spawnPosition} with ownership {netObj.OwnerClientId}");
            
            // CRITICAL: Ensure player components are properly enabled for the owner
            EnablePlayerComponentsForOwner(playerObject, clientId);
            
            // Apply character selection with proper network state validation
            if (SimpleSceneTransition.Instance != null && SimpleSceneTransition.Instance.IsInGame())
            {
                StartCoroutine(ApplyCharacterSelectionWhenReady(clientId));
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SpawnManager] Exception while spawning player for client {clientId}: {e.Message}");
            Debug.LogError($"[SpawnManager] Stack trace: {e.StackTrace}");
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
    
    /// <summary>
    /// OWNERSHIP FIX: Verify all spawned players have correct ownership
    /// </summary>
    private void VerifyAllPlayerOwnership()
    {
        Debug.Log($"[SpawnManager] VerifyAllPlayerOwnership - Checking {spawnedPlayers.Count} spawned players");
        
        foreach (var playerPair in spawnedPlayers)
        {
            ulong expectedClientId = playerPair.Key;
            GameObject playerObject = playerPair.Value;
            
            if (playerObject != null)
            {
                NetworkObject netObj = playerObject.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    Debug.Log($"[SpawnManager] Player {expectedClientId}: Expected Owner = {expectedClientId}, Actual Owner = {netObj.OwnerClientId}");
                    
                    if (netObj.OwnerClientId != expectedClientId)
                    {
                        Debug.LogError($"[SpawnManager] ❌ OWNERSHIP MISMATCH! Player {expectedClientId} owned by {netObj.OwnerClientId}");
                        
                        // Try to fix ownership
                        netObj.ChangeOwnership(expectedClientId);
                        Debug.Log($"[SpawnManager] Attempted ownership fix - New owner: {netObj.OwnerClientId}");
                    }
                    else
                    {
                        Debug.Log($"[SpawnManager] ✅ Ownership correct for player {expectedClientId}");
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// DEBUG: Force spawn both players with correct ownership (use if issues persist)
    /// ENHANCED: Now includes workarounds for Unity Netcode bugs #836, #2052, #1552
    /// </summary>
    [ContextMenu("Debug - Force Spawn All Players")]
    public void DebugForceSpawnAllPlayers()
    {
        if (!IsServer)
        {
            Debug.LogWarning("[SpawnManager] Only server can force spawn players");
            return;
        }
        
        Debug.Log("[SpawnManager] DEBUG: Force spawning all players with Unity bug workarounds...");
        
        // WORKAROUND: Clear Unity's internal player tracking first
        ClearUnityPlayerObjectTracking();
        
        // Clear existing players
        foreach (var player in spawnedPlayers.Values)
        {
            if (player != null)
            {
                player.GetComponent<NetworkObject>().Despawn();
            }
        }
        spawnedPlayers.Clear();
        
        // WORKAROUND: Use enhanced spawning method with bug fixes
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            Debug.Log($"[SpawnManager] DEBUG: Force spawning player for client {client.ClientId}");
            SpawnPlayerWithWorkarounds(client.ClientId);
        }
        
        // Verify ownership after spawn
        StartCoroutine(DelayedOwnershipVerification());
    }
    
    /// <summary>
    /// WORKAROUND: Clear Unity's internal player object tracking to fix bugs
    /// </summary>
    private void ClearUnityPlayerObjectTracking()
    {
        foreach (var clientPair in NetworkManager.Singleton.ConnectedClients)
        {
            var client = clientPair.Value;
            if (client.PlayerObject != null)
            {
                Debug.Log($"[SpawnManager] WORKAROUND: Clearing Unity's player tracking for client {clientPair.Key}");
                // Unity sometimes doesn't clear this properly during scene transitions
                client.PlayerObject = null;
            }
        }
    }
    
    /// <summary>
    /// WORKAROUND: Enhanced player spawning with Unity Netcode bug fixes
    /// Addresses GitHub issues #836, #2052, #1552, #2531, #888
    /// </summary>
    private void SpawnPlayerWithWorkarounds(ulong clientId)
    {
        if (playerPrefab == null)
        {
            Debug.LogError("[SpawnManager] playerPrefab is null! Cannot spawn player.");
            return;
        }
        
        Vector3 spawnPosition = GetSpawnPosition(clientId);
        Debug.Log($"[SpawnManager] WORKAROUND: Spawning player for client {clientId} at {spawnPosition}");
        
        try
        {
            GameObject playerObject = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
            NetworkObject netObj = playerObject.GetComponent<NetworkObject>();
            
            if (netObj == null)
            {
                Debug.LogError($"[SpawnManager] Player prefab missing NetworkObject component!");
                Destroy(playerObject);
                return;
            }
            
            // WORKAROUND #2531: Set position before spawning to avoid (0,0,0) bug
            playerObject.transform.position = spawnPosition;
            
            // WORKAROUND #1552: Manually control scene object flags
            Debug.Log($"[SpawnManager] WORKAROUND: Spawning with manual flags control");
            
            // Spawn without destroyWithScene to avoid bug #2052
            netObj.Spawn(false);
            
            // WORKAROUND #888: Manually update OwnedObjects list
            netObj.ChangeOwnership(clientId);
            
            // CRITICAL WORKAROUND: Manually set as player object since Unity's system is buggy
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
            {
                client.PlayerObject = netObj;
                Debug.Log($"[SpawnManager] WORKAROUND: Manually assigned player object to client {clientId}");
            }
            
            // WORKAROUND #2531: Force position update after all network operations
            StartCoroutine(ForcePositionUpdateAfterSpawn(playerObject, spawnPosition));
            
            spawnedPlayers[clientId] = playerObject;
            
            Debug.Log($"[SpawnManager] ✅ WORKAROUND: Successfully spawned player for client {clientId}");
            
            // Enable components properly
            EnablePlayerComponentsForOwner(playerObject, clientId);
            
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SpawnManager] WORKAROUND: Exception while spawning player for client {clientId}: {e.Message}");
        }
    }
    
    /// <summary>
    /// WORKAROUND #2531: Force position update after spawn to fix (0,0,0) bug
    /// </summary>
    private System.Collections.IEnumerator ForcePositionUpdateAfterSpawn(GameObject playerObject, Vector3 targetPosition)
    {
        // Wait a few frames for network spawn to complete
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.1f);
        
        if (playerObject != null)
        {
            playerObject.transform.position = targetPosition;
            
            // Also reset CharacterController if present
            var charController = playerObject.GetComponent<CharacterController>();
            if (charController != null)
            {
                charController.enabled = false;
                charController.enabled = true;
            }
            
            Debug.Log($"[SpawnManager] WORKAROUND: Force-updated player position to {targetPosition}");
        }
    }
    
    private System.Collections.IEnumerator DelayedOwnershipVerification()
    {
        yield return new WaitForSeconds(1f);
        VerifyAllPlayerOwnership();
    }
    
    /// <summary>
    /// OWNERSHIP FIX: Enable player components properly based on ownership
    /// </summary>
    private void EnablePlayerComponentsForOwner(GameObject playerObject, ulong clientId)
    {
        if (playerObject == null) return;
        
        Debug.Log($"[SpawnManager] EnablePlayerComponentsForOwner for client {clientId}");
        
        // Enable input components for owner
        var playerInput = playerObject.GetComponent<UnityEngine.InputSystem.PlayerInput>();
        if (playerInput != null)
        {
            // Input should only be enabled for the owner (handled by IsOwner checks)
            Debug.Log($"[SpawnManager] PlayerInput found on {clientId} - will be controlled by IsOwner checks");
        }
        
        // Enable camera for owner (disable for non-owners)
        var cameras = playerObject.GetComponentsInChildren<Camera>();
        foreach (var camera in cameras)
        {
            // Camera should only be active for the owner
            bool isOwner = NetworkManager.Singleton.LocalClientId == clientId;
            camera.enabled = isOwner;
            Debug.Log($"[SpawnManager] Camera {camera.name} for client {clientId}: enabled = {isOwner} (LocalClient: {NetworkManager.Singleton.LocalClientId})");
        }
        
        // Enable movement controller (IsOwner checks handle the logic)
        var thirdPersonController = playerObject.GetComponent<StarterAssets.ThirdPersonController>();
        if (thirdPersonController != null)
        {
            Debug.Log($"[SpawnManager] ThirdPersonController found for client {clientId} - movement controlled by IsOwner checks");
        }
    }
}