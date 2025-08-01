using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

/// <summary>
/// Clean Game Spawn Manager for Unity Netcode
/// Spawns players at specific spawn points with IDs 0-1-2-3-4-5 etc.
/// Based on Unity Netcode best practices for multiplayer spawning
/// </summary>
public class GameSpawnManager : NetworkBehaviour
{
    public static GameSpawnManager Instance { get; private set; }
    
    [Header("Player Spawning")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private Vector3 defaultSpawnPosition = new Vector3(0, 2, 0);
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    
    // Track spawned players
    private Dictionary<ulong, GameObject> spawnedPlayers = new Dictionary<ulong, GameObject>();
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        
        // Validate spawn points
        ValidateSpawnPoints();
    }
    
    public override void OnNetworkSpawn()
    {
        if (showDebugLogs)
        {
            Debug.Log($"[GameSpawnManager] NetworkSpawn - IsServer: {IsServer}, IsClient: {IsClient}");
            Debug.Log($"[GameSpawnManager] Available spawn points: {spawnPoints?.Length ?? 0}");
        }
    }
    
    /// <summary>
    /// Spawn a player with specific data at designated spawn point
    /// Only server can call this
    /// </summary>
    public void SpawnPlayerWithData(ulong clientId, int characterId, string playerName)
    {
        Debug.Log($"[GameSpawnManager] SpawnPlayerWithData CALLED - Client: {clientId}, Character: {characterId}, Name: {playerName}");
        Debug.Log($"[GameSpawnManager] Current state - IsServer: {NetworkManager.Singleton?.IsServer}, PlayerPrefab: {playerPrefab != null}");
        
        // CRITICAL DEBUG: Verify character data
        if (characterId >= 0)
        {
            var characterData = CharacterRegistry.Instance?.GetCharacterByID(characterId);
            if (characterData != null)
            {
                Debug.Log($"[GameSpawnManager] ✅ Found character data for ID {characterId}: '{characterData.characterName}'");
            }
            else
            {
                Debug.LogError($"[GameSpawnManager] ❌ No character data found for ID {characterId}! Check CharacterRegistry.");
            }
        }
        else
        {
            Debug.LogWarning($"[GameSpawnManager] ⚠️ Character ID is {characterId} (negative - invalid), player will use default character");
        }
        
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("[GameSpawnManager] Only server can spawn players");
            return;
        }
        
        if (playerPrefab == null)
        {
            Debug.LogError("[GameSpawnManager] Player prefab is not assigned!");
            return;
        }
        
        // Check if player already spawned
        if (spawnedPlayers.ContainsKey(clientId))
        {
            Debug.LogWarning($"[GameSpawnManager] Player {clientId} already spawned");
            return;
        }
        
        // Get spawn position based on client ID
        Vector3 spawnPosition = GetSpawnPosition(clientId);
        Quaternion spawnRotation = GetSpawnRotation(clientId);
        
        if (showDebugLogs)
        {
            Debug.Log($"[GameSpawnManager] Spawning player - ClientId: {clientId}, Character: {characterId}, Position: {spawnPosition}");
        }
        
        try
        {
            // Instantiate player
            GameObject playerObject = Instantiate(playerPrefab, spawnPosition, spawnRotation);
            
            // Get NetworkObject component
            NetworkObject networkObject = playerObject.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogError("[GameSpawnManager] Player prefab missing NetworkObject component!");
                Destroy(playerObject);
                return;
            }
            
            // CRITICAL FIX: Check NetworkObject state before spawning
            Debug.Log($"[GameSpawnManager] BEFORE SpawnAsPlayerObject - ClientId: {clientId}, OwnerClientId: {networkObject.OwnerClientId}");
            Debug.Log($"[GameSpawnManager] NetworkObject IsSpawned: {networkObject.IsSpawned}, NetworkObjectId: {networkObject.NetworkObjectId}");
            
            // CRITICAL: Ensure NetworkObject is not already spawned
            if (networkObject.IsSpawned)
            {
                Debug.LogError($"[GameSpawnManager] NetworkObject for client {clientId} is already spawned! Cannot spawn again.");
                Destroy(playerObject);
                return;
            }
            
            // Spawn as player object first (this will also assign ownership)
            networkObject.SpawnAsPlayerObject(clientId, false); // destroyWithScene = false for persistence
            
            // Verify spawning was successful
            if (!networkObject.IsSpawned)
            {
                Debug.LogError($"[GameSpawnManager] Failed to spawn NetworkObject for client {clientId}!");
                Destroy(playerObject);
                return;
            }
            
            Debug.Log($"[GameSpawnManager] AFTER SpawnAsPlayerObject - ClientId: {clientId}, Final OwnerClientId: {networkObject.OwnerClientId}");
            Debug.Log($"[GameSpawnManager] NetworkObject IsSpawned: {networkObject.IsSpawned}, NetworkObjectId: {networkObject.NetworkObjectId}");
            
            // CRITICAL: Verify ownership is correct after spawn
            if (networkObject.OwnerClientId != clientId)
            {
                Debug.LogWarning($"[GameSpawnManager] Ownership mismatch after spawn! Expected: {clientId}, Actual: {networkObject.OwnerClientId}");
                networkObject.ChangeOwnership(clientId);
                Debug.Log($"[GameSpawnManager] Fixed ownership - New OwnerClientId: {networkObject.OwnerClientId}");
            }
            
            // CRITICAL WORKAROUND: Manually assign to client's PlayerObject if Unity's system fails
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
            {
                if (client.PlayerObject == null)
                {
                    client.PlayerObject = networkObject;
                    Debug.Log($"[GameSpawnManager] WORKAROUND: Manually assigned PlayerObject to client {clientId}");
                }
                else
                {
                    Debug.Log($"[GameSpawnManager] Client {clientId} already has PlayerObject: {client.PlayerObject.name}");
                }
            }
            
            // Track spawned player
            spawnedPlayers[clientId] = playerObject;
            
            // CRITICAL: Verify ownership immediately after spawn
            StartCoroutine(VerifyOwnershipAfterSpawn(networkObject, clientId));
            
            // Apply character data after spawn
            StartCoroutine(ApplyCharacterDataAfterSpawn(playerObject, clientId, characterId, playerName));
            
            if (showDebugLogs)
            {
                Debug.Log($"[GameSpawnManager] ✅ Successfully spawned player {clientId} at spawn point {GetSpawnPointIndex(clientId)}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GameSpawnManager] Error spawning player {clientId}: {e.Message}");
        }
    }
    
    /// <summary>
    /// CRITICAL: Verify and fix ownership after spawn
    /// </summary>
    private System.Collections.IEnumerator VerifyOwnershipAfterSpawn(NetworkObject networkObject, ulong clientId)
    {
        // Wait a frame for network sync
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.1f);
        
        if (networkObject == null || !networkObject.IsSpawned)
        {
            Debug.LogError($"[GameSpawnManager] NetworkObject for client {clientId} is null or not spawned!");
            yield break;
        }
        
        Debug.Log($"[GameSpawnManager] OWNERSHIP VERIFICATION - Expected: {clientId}, Actual: {networkObject.OwnerClientId}");
        
        if (networkObject.OwnerClientId != clientId)
        {
            Debug.LogError($"[GameSpawnManager] ❌ OWNERSHIP MISMATCH! Client {clientId} should own their player but it's owned by {networkObject.OwnerClientId}");
            
            // Try to fix ownership
            if (IsServer)
            {
                Debug.Log($"[GameSpawnManager] Attempting to fix ownership for client {clientId}...");
                networkObject.ChangeOwnership(clientId);
                
                // Wait another frame and check again
                yield return new WaitForEndOfFrame();
                Debug.Log($"[GameSpawnManager] After ownership fix attempt - OwnerClientId: {networkObject.OwnerClientId}");
                
                if (networkObject.OwnerClientId == clientId)
                {
                    Debug.Log($"[GameSpawnManager] ✅ Ownership successfully fixed for client {clientId}");
                }
                else
                {
                    Debug.LogError($"[GameSpawnManager] ❌ Failed to fix ownership for client {clientId}");
                }
            }
        }
        else
        {
            Debug.Log($"[GameSpawnManager] ✅ Ownership is CORRECT for client {clientId}");
        }
    }
    
    /// <summary>
    /// Apply character data to spawned player after network spawn completes
    /// </summary>
    private System.Collections.IEnumerator ApplyCharacterDataAfterSpawn(GameObject playerObject, ulong clientId, int characterId, string playerName)
    {
        // Wait for network object to be fully spawned
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.1f);
        
        if (playerObject == null)
        {
            Debug.LogWarning($"[GameSpawnManager] Player object destroyed before character data could be applied");
            yield break;
        }
        
        // Apply character selection if valid
        Debug.Log($"⚡ [GameSpawnManager] Applying character data - Client: {clientId}, CharacterId: {characterId}");
        
        if (characterId > 0)
        {
            var characterLoader = playerObject.GetComponent<UltraSimpleMeshSwapper>();
            if (characterLoader != null)
            {
                Debug.Log($"⚡ [GameSpawnManager] Found UltraSimpleMeshSwapper on player {clientId}");
                
                // CRITICAL: Wait for NetworkObject to be fully spawned before setting character
                var networkObject = playerObject.GetComponent<NetworkObject>();
                if (networkObject != null && networkObject.IsSpawned)
                {
                    Debug.Log($"⚡ [GameSpawnManager] NetworkObject is spawned, setting character {characterId} via SetNetworkCharacterId");
                    characterLoader.SetNetworkCharacterId(characterId);
                    
                    Debug.Log($"✅ [GameSpawnManager] Character {characterId} applied to player {clientId}");
                }
                else
                {
                    // NetworkObject not ready yet, try again later
                    Debug.LogWarning($"⚠️ [GameSpawnManager] NetworkObject not ready (IsSpawned: {networkObject?.IsSpawned}), will retry character application for client {clientId}");
                    StartCoroutine(RetryCharacterApplication(playerObject, clientId, characterId));
                }
            }
            else
            {
                Debug.LogError($"❌ [GameSpawnManager] Player {clientId} missing UltraSimpleMeshSwapper component! Character loading will fail.");
                
                // Debug all components on the player object
                var allComponents = playerObject.GetComponents<Component>();
                Debug.LogError($"❌ [GameSpawnManager] Player {clientId} has {allComponents.Length} components:");
                foreach (var comp in allComponents)
                {
                    Debug.LogError($"  - {comp.GetType().Name}");
                }
            }
        }
        else
        {
            Debug.LogWarning($"⚠️ [GameSpawnManager] Character ID {characterId} is negative for client {clientId} - player will use default appearance");
        }
        
        // Set player name if available
        if (!string.IsNullOrEmpty(playerName))
        {
            // You can implement player name display here if needed
            if (showDebugLogs)
            {
                Debug.Log($"[GameSpawnManager] Set player name '{playerName}' for client {clientId}");
            }
        }
        
        // Enable player components properly
        EnablePlayerComponents(playerObject, clientId);
    }
    
    /// <summary>
    /// Retry character application if NetworkObject wasn't ready initially
    /// </summary>
    private System.Collections.IEnumerator RetryCharacterApplication(GameObject playerObject, ulong clientId, int characterId)
    {
        int maxRetries = 10; // 1 second max wait (10 * 0.1s)
        int retries = 0;
        
        while (retries < maxRetries)
        {
            yield return new WaitForSeconds(0.1f);
            retries++;
            
            if (playerObject == null)
            {
                Debug.LogWarning($"[GameSpawnManager] Player object destroyed during character retry for client {clientId}");
                yield break;
            }
            
            var networkObject = playerObject.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.IsSpawned)
            {
                var characterLoader = playerObject.GetComponent<UltraSimpleMeshSwapper>();
                if (characterLoader != null)
                {
                    characterLoader.SetNetworkCharacterId(characterId);
                    Debug.Log($"[GameSpawnManager] ✅ Successfully applied character {characterId} to player {clientId} after {retries} retries");
                    yield break;
                }
            }
        }
        
        Debug.LogError($"[GameSpawnManager] ❌ Failed to apply character {characterId} to player {clientId} after {maxRetries} retries");
    }
    
    /// <summary>
    /// Get spawn position based on client ID and spawn points
    /// Uses spawn point index = clientId % spawnPoints.Length
    /// </summary>
    private Vector3 GetSpawnPosition(ulong clientId)
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            int spawnIndex = GetSpawnPointIndex(clientId);
            
            if (spawnIndex < spawnPoints.Length && spawnPoints[spawnIndex] != null)
            {
                return spawnPoints[spawnIndex].position;
            }
            else
            {
                Debug.LogWarning($"[GameSpawnManager] Spawn point {spawnIndex} is null, using default position");
            }
        }
        
        // Fallback to default position with offset
        return defaultSpawnPosition + new Vector3((float)clientId * 2f, 0, 0);
    }
    
    /// <summary>
    /// Get spawn rotation based on client ID and spawn points
    /// </summary>
    private Quaternion GetSpawnRotation(ulong clientId)
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            int spawnIndex = GetSpawnPointIndex(clientId);
            
            if (spawnIndex < spawnPoints.Length && spawnPoints[spawnIndex] != null)
            {
                return spawnPoints[spawnIndex].rotation;
            }
        }
        
        return Quaternion.identity;
    }
    
    /// <summary>
    /// Get spawn point index for a client ID
    /// Cycles through available spawn points: 0, 1, 2, 3, 4, 5, 0, 1, 2...
    /// </summary>
    private int GetSpawnPointIndex(ulong clientId)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            return 0;
        
        return (int)(clientId % (ulong)spawnPoints.Length);
    }
    
    /// <summary>
    /// Enable player components based on ownership
    /// </summary>
    private void EnablePlayerComponents(GameObject playerObject, ulong clientId)
    {
        if (playerObject == null) return;
        
        Debug.Log($"[GameSpawnManager] EnablePlayerComponents for client {clientId} - LocalClientId: {NetworkManager.Singleton.LocalClientId}");
        
        // Enable camera for owner only
        var cameras = playerObject.GetComponentsInChildren<Camera>();
        bool isLocalPlayer = NetworkManager.Singleton.LocalClientId == clientId;
        
        Debug.Log($"[GameSpawnManager] Found {cameras.Length} cameras for client {clientId}, isLocalPlayer: {isLocalPlayer}");
        
        foreach (var camera in cameras)
        {
            camera.enabled = isLocalPlayer;
            camera.gameObject.SetActive(isLocalPlayer);
            
            if (showDebugLogs)
            {
                Debug.Log($"[GameSpawnManager] Camera {camera.name} for client {clientId}: enabled = {isLocalPlayer}");
            }
        }
        
        // Enable audio listener for owner only
        var audioListeners = playerObject.GetComponentsInChildren<AudioListener>();
        foreach (var listener in audioListeners)
        {
            listener.enabled = isLocalPlayer;
            
            if (showDebugLogs)
            {
                Debug.Log($"[GameSpawnManager] AudioListener for client {clientId}: enabled = {isLocalPlayer}");
            }
        }
        
        // Log final state
        if (showDebugLogs)
        {
            Debug.Log($"[GameSpawnManager] Component setup complete for client {clientId} (LocalPlayer: {isLocalPlayer})");
        }
    }
    
    /// <summary>
    /// Despawn a specific player
    /// Only server can call this
    /// </summary>
    public void DespawnPlayer(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("[GameSpawnManager] Only server can despawn players");
            return;
        }
        
        if (spawnedPlayers.TryGetValue(clientId, out GameObject playerObject))
        {
            if (playerObject != null)
            {
                var networkObject = playerObject.GetComponent<NetworkObject>();
                if (networkObject != null && networkObject.IsSpawned)
                {
                    networkObject.Despawn();
                }
            }
            
            spawnedPlayers.Remove(clientId);
            
            if (showDebugLogs)
            {
                Debug.Log($"[GameSpawnManager] Despawned player {clientId}");
            }
        }
    }
    
    /// <summary>
    /// Despawn all players
    /// Only server can call this
    /// </summary>
    public void DespawnAllPlayers()
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("[GameSpawnManager] Only server can despawn all players");
            return;
        }
        
        var clientIds = new List<ulong>(spawnedPlayers.Keys);
        foreach (var clientId in clientIds)
        {
            DespawnPlayer(clientId);
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"[GameSpawnManager] Despawned all players");
        }
    }
    
    /// <summary>
    /// Get spawned player GameObject for a client
    /// </summary>
    public GameObject GetSpawnedPlayer(ulong clientId)
    {
        spawnedPlayers.TryGetValue(clientId, out GameObject player);
        return player;
    }
    
    /// <summary>
    /// Get number of spawned players
    /// </summary>
    public int GetSpawnedPlayerCount()
    {
        return spawnedPlayers.Count;
    }
    
    /// <summary>
    /// Validate spawn points setup
    /// </summary>
    private void ValidateSpawnPoints()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("[GameSpawnManager] No spawn points assigned! Players will use default positions.");
            return;
        }
        
        int validSpawnPoints = 0;
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i] != null)
            {
                validSpawnPoints++;
            }
            else
            {
                Debug.LogWarning($"[GameSpawnManager] Spawn point {i} is null!");
            }
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"[GameSpawnManager] Spawn points validation: {validSpawnPoints}/{spawnPoints.Length} valid spawn points");
        }
    }
    
    /// <summary>
    /// Debug method to manually spawn players (for testing)
    /// </summary>
    [ContextMenu("Debug - Spawn Test Players")]
    public void DebugSpawnTestPlayers()
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("[GameSpawnManager] Only server can spawn test players");
            return;
        }
        
        Debug.Log("=== Debug Spawn Test Players ===");
        Debug.Log($"Connected clients: {NetworkManager.Singleton.ConnectedClientsList.Count}");
        
        // Show all connected clients first
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            Debug.Log($"Found connected client: {client.ClientId}");
            Debug.Log($"  - Already spawned: {spawnedPlayers.ContainsKey(client.ClientId)}");
            Debug.Log($"  - Has PlayerObject: {client.PlayerObject != null}");
        }
        
        // Spawn players for all connected clients
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (!spawnedPlayers.ContainsKey(client.ClientId))
            {
                Debug.Log($"Spawning test player for client {client.ClientId}");
                SpawnPlayerWithData(client.ClientId, 0, $"TestPlayer_{client.ClientId}");
            }
            else
            {
                Debug.Log($"Client {client.ClientId} already has spawned player");
            }
        }
        
        Debug.Log("=================================");
    }
    
    /// <summary>
    /// Debug method to check if GameSpawnManager is being called at all
    /// </summary>
    [ContextMenu("Debug - Check Spawn Manager State")]
    public void DebugCheckSpawnManagerState()
    {
        Debug.Log("=== GameSpawnManager State Debug ===");
        Debug.Log($"IsServer: {NetworkManager.Singleton?.IsServer}");
        Debug.Log($"IsClient: {NetworkManager.Singleton?.IsClient}");
        Debug.Log($"IsHost: {NetworkManager.Singleton?.IsHost}");
        Debug.Log($"Connected clients: {NetworkManager.Singleton?.ConnectedClientsList?.Count ?? 0}");
        Debug.Log($"Spawned players tracked: {spawnedPlayers.Count}");
        Debug.Log($"Player prefab assigned: {playerPrefab != null}");
        Debug.Log($"Spawn points assigned: {spawnPoints?.Length ?? 0}");
        
        if (NetworkManager.Singleton != null)
        {
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                Debug.Log($"Client {client.ClientId}: HasPlayerObject = {client.PlayerObject != null}");
                if (spawnedPlayers.TryGetValue(client.ClientId, out GameObject playerObj))
                {
                    Debug.Log($"  - Tracked in spawnedPlayers: {playerObj?.name ?? "NULL"}");
                }
                else
                {
                    Debug.Log($"  - NOT tracked in spawnedPlayers");
                }
            }
        }
        
        Debug.Log("=====================================");
    }
    
    /// <summary>
    /// Debug method to show spawn point info
    /// </summary>
    [ContextMenu("Debug - Show Spawn Points")]
    public void DebugShowSpawnPoints()
    {
        Debug.Log("=== GameSpawnManager Spawn Points ===");
        
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.Log("No spawn points assigned");
            return;
        }
        
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i] != null)
            {
                Debug.Log($"Spawn Point {i}: {spawnPoints[i].name} at {spawnPoints[i].position}");
            }
            else
            {
                Debug.Log($"Spawn Point {i}: NULL");
            }
        }
        
        Debug.Log("=====================================");
    }
    
    /// <summary>
    /// Debug method to check and fix ownership issues
    /// </summary>
    [ContextMenu("Debug - Fix All Player Ownership")]
    public void DebugFixAllPlayerOwnership()
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("[GameSpawnManager] Only server can fix ownership");
            return;
        }
        
        Debug.Log("=== GameSpawnManager Ownership Debug ===");
        
        foreach (var clientPair in NetworkManager.Singleton.ConnectedClients)
        {
            var clientId = clientPair.Key;
            var client = clientPair.Value;
            
            Debug.Log($"Client {clientId}: HasPlayerObject = {client.PlayerObject != null}");
            
            if (client.PlayerObject != null)
            {
                var networkObj = client.PlayerObject;
                Debug.Log($"  - NetworkObjectId: {networkObj.NetworkObjectId}");
                Debug.Log($"  - OwnerClientId: {networkObj.OwnerClientId}");
                Debug.Log($"  - IsSpawned: {networkObj.IsSpawned}");
                
                if (networkObj.OwnerClientId != clientId)
                {
                    Debug.LogError($"  - ❌ OWNERSHIP MISMATCH! Should be {clientId}, is {networkObj.OwnerClientId}");
                    Debug.Log($"  - Attempting to fix...");
                    networkObj.ChangeOwnership(clientId);
                    Debug.Log($"  - After fix: OwnerClientId = {networkObj.OwnerClientId}");
                }
                else
                {
                    Debug.Log($"  - ✅ Ownership is correct");
                }
            }
            else
            {
                Debug.LogWarning($"  - ❌ Client {clientId} has no PlayerObject!");
                
                // Try to find orphaned player object
                if (spawnedPlayers.TryGetValue(clientId, out GameObject playerObj))
                {
                    var netObj = playerObj.GetComponent<NetworkObject>();
                    if (netObj != null)
                    {
                        Debug.Log($"  - Found orphaned player object, reassigning...");
                        client.PlayerObject = netObj;
                        netObj.ChangeOwnership(clientId);
                        Debug.Log($"  - Reassigned: OwnerClientId = {netObj.OwnerClientId}");
                    }
                }
            }
        }
        
        Debug.Log("==========================================");
    }
}