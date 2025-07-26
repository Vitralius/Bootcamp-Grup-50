using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class SceneTransitionManager : NetworkBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }
    
    [Header("Scene Names - Configure in Inspector")]
    [SerializeField] private string mainMenuSceneName = "SampleScene";
    [SerializeField] private string gameSceneName = "Playground";
    
    public event Action<string> OnSceneTransitionStarted;
    public event Action<string> OnSceneTransitionCompleted;
    public event Action<string> OnSceneTransitionFailed;
    
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
    
    public override void OnNetworkSpawn()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            // CRITICAL FIX: Use OnSynchronizeComplete instead of OnLoadEventCompleted for better timing
            NetworkManager.Singleton.SceneManager.OnSynchronizeComplete += OnSceneSynchronizeCompleted;
            Debug.Log("SceneTransitionManager: Subscribed to OnSynchronizeComplete");
        }
        else
        {
            Debug.LogError("SceneTransitionManager: NetworkManager or SceneManager is null on spawn");
        }
    }
    
    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            // CRITICAL FIX: Update unsubscribe to match new event
            NetworkManager.Singleton.SceneManager.OnSynchronizeComplete -= OnSceneSynchronizeCompleted;
        }
    }
    
    public void TransitionToMainMenu()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            LoadSceneServerRpc(mainMenuSceneName);
        }
        else
        {
            // Non-networked transition for clients leaving multiplayer
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
    
    
    public void TransitionToGame()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            // CRITICAL FIX: Cache all character selections BEFORE scene transition
            CacheAllCharacterSelections();
            LoadSceneServerRpc(gameSceneName);
        }
        else if (NetworkManager.Singleton != null)
        {
            Debug.LogWarning("Only the server can initiate scene transitions");
        }
        else
        {
            Debug.LogError("NetworkManager.Singleton is null when trying to transition to game");
        }
    }
    
    /// <summary>
    /// Cache all character selections before scene transition to prevent data loss
    /// </summary>
    private void CacheAllCharacterSelections()
    {
        Debug.Log("SceneTransitionManager: Caching all character selections before transition");
        
        var playerSessionData = PlayerSessionData.Instance;
        var persistentCache = PersistentCharacterCache.Instance;
        
        if (playerSessionData == null || persistentCache == null)
        {
            Debug.LogWarning("SceneTransitionManager: Cannot cache selections - missing PlayerSessionData or PersistentCharacterCache");
            return;
        }
        
        var connectedSessions = playerSessionData.GetConnectedPlayerSessions();
        Debug.Log($"SceneTransitionManager: Found {connectedSessions.Count} player sessions to cache");
        
        foreach (var session in connectedSessions)
        {
            if (session.selectedCharacterId > 0) // Only cache valid character selections
            {
                // Cache by player GUID
                persistentCache.CacheCharacterSelectionByGuid(session.playerId.ToString(), session.selectedCharacterId);
                
                // Also try to cache by client ID if we can find it
                if (NetworkManager.Singleton != null)
                {
                    foreach (var clientPair in NetworkManager.Singleton.ConnectedClients)
                    {
                        // This is a best-effort mapping - the exact mapping logic might need adjustment
                        // based on your authentication/session system
                        var clientId = clientPair.Key;
                        persistentCache.CacheCharacterSelection(clientId, session.selectedCharacterId);
                        Debug.Log($"SceneTransitionManager: Cached character {session.selectedCharacterId} for player {session.playerName} (ClientID: {clientId})");
                        break; // For now, just map to the first available client - this might need refinement
                    }
                }
            }
        }
        
        Debug.Log("SceneTransitionManager: Character selection caching completed");
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void LoadSceneServerRpc(string sceneName)
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetworkManager.Singleton is null in LoadSceneServerRpc");
            return;
        }
        
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("LoadSceneServerRpc called but not server");
            return;
        }
        
        try
        {
            OnSceneTransitionStarted?.Invoke(sceneName);
            
            if (NetworkManager.Singleton.SceneManager == null)
            {
                Debug.LogError("NetworkManager.SceneManager is null");
                OnSceneTransitionFailed?.Invoke(sceneName);
                return;
            }
            
            var sceneLoadStatus = NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            
            if (sceneLoadStatus != SceneEventProgressStatus.Started)
            {
                Debug.LogError($"Failed to start scene load for {sceneName}. Status: {sceneLoadStatus}");
                OnSceneTransitionFailed?.Invoke(sceneName);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Exception during scene transition to {sceneName}: {e.Message}");
            OnSceneTransitionFailed?.Invoke(sceneName);
        }
    }
    
    private void OnSceneSynchronizeCompleted(ulong clientId)
    {
        // CRITICAL FIX: OnSynchronizeComplete is called per client, so we track all clients
        Debug.Log($"Scene synchronization completed for client {clientId}");
        
        // Only apply character data on server when it's the game scene
        if (IsServer && GetCurrentSceneName() == gameSceneName)
        {
            Debug.Log($"Game scene synchronized for client {clientId}, checking if all clients ready for character application...");
            
            // Use coroutine to ensure all NetworkVariables are fully synchronized
            StartCoroutine(ApplyCharacterSelectionsWithDelay());
        }
        
        OnSceneTransitionCompleted?.Invoke(GetCurrentSceneName());
    }
    
    /// <summary>
    /// IMPROVED FIX: Coroutine with better network state validation for character data application
    /// </summary>
    private System.Collections.IEnumerator ApplyCharacterSelectionsWithDelay()
    {
        // Wait for network synchronization and all required systems to be ready
        int maxAttempts = 30; // 3 seconds max wait
        int attempts = 0;
        
        while (attempts < maxAttempts)
        {
            // Check if PlayerSessionData and required components are ready
            if (PlayerSessionData.Instance != null && 
                NetworkManager.Singleton != null &&
                NetworkManager.Singleton.ConnectedClientsIds.Count > 0)
            {
                bool allPlayersReady = true;
                
                // Check if all player objects have their NetworkVariables synchronized
                foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
                {
                    if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) && 
                        client.PlayerObject != null)
                    {
                        var characterLoader = client.PlayerObject.GetComponent<UltraSimpleMeshSwapper>();
                        if (characterLoader == null || !characterLoader.IsSpawned)
                        {
                            allPlayersReady = false;
                            break;
                        }
                    }
                }
                
                if (allPlayersReady)
                {
                    Debug.Log("SceneTransitionManager: All players ready, applying character selections");
                    ApplyCharacterSelectionsAfterSceneLoad();
                    yield break;
                }
            }
            
            attempts++;
            yield return new WaitForSeconds(0.1f); // Check every 100ms
        }
        
        Debug.LogWarning("SceneTransitionManager: Timeout waiting for network sync, applying character selections anyway");
        ApplyCharacterSelectionsAfterSceneLoad();
    }
    
    private void ApplyCharacterSelectionsAfterSceneLoad()
    {
        // IMPROVED: Direct character application through SpawnManager - no need for bridge
        var spawnManager = FindFirstObjectByType<SpawnManager>();
        if (spawnManager != null)
        {
            Debug.Log("Requesting SpawnManager to apply character selections...");
            ApplyCharacterSelectionsToSpawnedPlayersServerRpc();
        }
        else
        {
            Debug.LogWarning("SpawnManager not found! Character selections will not be applied.");
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void ApplyCharacterSelectionsToSpawnedPlayersServerRpc()
    {
        if (!IsServer) return;
        
        // Get all spawned player objects and apply their character selections
        var playerSessionData = PlayerSessionData.Instance;
        if (playerSessionData == null)
        {
            Debug.LogWarning("PlayerSessionData not found, cannot apply character selections");
            return;
        }
        
        var connectedSessions = playerSessionData.GetConnectedPlayerSessions();
        Debug.Log($"Found {connectedSessions.Count} connected player sessions");
        
        foreach (var session in connectedSessions)
        {
            if (session.selectedCharacterId != 0) // 0 is default/no selection
            {
                // Find the corresponding NetworkObject for this player
                if (NetworkManager.Singleton == null) continue;
                
                foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
                {
                    if (client.PlayerObject != null)
                    {
                        var characterLoader = client.PlayerObject.GetComponent<UltraSimpleMeshSwapper>();
                        if (characterLoader != null)
                        {
                            var characterData = CharacterRegistry.Instance?.GetCharacterByID(session.selectedCharacterId);
                            if (characterData != null)
                            {
                                Debug.Log($"Applying character {characterData.characterName} to player {session.playerName} (ClientID: {client.ClientId})");
                                
                                // CRITICAL FIX: Use NetworkVariable system for proper character sync
                                characterLoader.SetNetworkCharacterId(session.selectedCharacterId);
                            }
                            break; // Move to next session
                        }
                    }
                }
            }
        }
    }
    
    // REMOVED: Old ClientRpc system replaced with NetworkVariable system in UltraSimpleMeshSwapper
    
    
    public string GetCurrentSceneName()
    {
        return SceneManager.GetActiveScene().name;
    }
    
    public bool IsInMainMenu()
    {
        return GetCurrentSceneName() == mainMenuSceneName;
    }
    
    
    public bool IsInGame()
    {
        return GetCurrentSceneName() == gameSceneName;
    }
    
    public void SetSceneNames(string mainMenu, string game)
    {
        mainMenuSceneName = mainMenu;
        gameSceneName = game;
    }
}