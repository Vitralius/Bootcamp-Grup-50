using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using System.Linq;

/// <summary>
/// Clean and Simple Scene Transition Manager for Unity Netcode
/// Handles transitions from lobby to game scene with player data persistence
/// Based on Unity Netcode best practices
/// </summary>
public class SimpleSceneTransition : NetworkBehaviour
{
    public static SimpleSceneTransition Instance { get; private set; }
    
    [Header("Scene Configuration")]
    [SerializeField] private string lobbySceneName = "SampleScene";
    [SerializeField] private string gameSceneName = "Playground";
    
    [Header("Player Data Storage")]
    private NetworkVariable<bool> isTransitioning = new NetworkVariable<bool>(false);
    
    [System.Serializable]
    public struct PlayerData
    {
        public ulong clientId;
        public int characterId;
        public string playerName;
    }
    
    // Store player data for scene transition
    private System.Collections.Generic.Dictionary<ulong, PlayerData> playerDataCache = 
        new System.Collections.Generic.Dictionary<ulong, PlayerData>();
    
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
        if (NetworkManager.Singleton?.SceneManager != null)
        {
            // Subscribe to scene events for proper timing
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoadCompleted;
            Debug.Log("[SimpleSceneTransition] Subscribed to scene events");
        }
    }
    
    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton?.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoadCompleted;
        }
    }
    
    /// <summary>
    /// Start game transition from lobby to game scene
    /// Only server/host can call this
    /// </summary>
    public void StartGameTransition()
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("[SimpleSceneTransition] Only server can start game transition");
            return;
        }
        
        if (isTransitioning.Value)
        {
            Debug.LogWarning("[SimpleSceneTransition] Transition already in progress");
            return;
        }
        
        Debug.Log("[SimpleSceneTransition] Starting game transition...");
        
        // 1. Cache all player data before transition
        CachePlayerData();
        
        // 2. Set transitioning state
        isTransitioning.Value = true;
        
        // 3. Load game scene
        LoadGameScene();
    }
    
    /// <summary>
    /// Return to lobby scene
    /// Only server/host can call this
    /// </summary>
    public void ReturnToLobby()
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("[SimpleSceneTransition] Only server can return to lobby");
            return;
        }
        
        Debug.Log("[SimpleSceneTransition] Returning to lobby...");
        
        isTransitioning.Value = true;
        
        var status = NetworkManager.Singleton.SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Single);
        if (status == SceneEventProgressStatus.Started)
        {
            Debug.Log($"[SimpleSceneTransition] Loading lobby scene: {lobbySceneName}");
        }
        else
        {
            Debug.LogError($"[SimpleSceneTransition] Failed to load lobby scene: {status}");
            isTransitioning.Value = false;
        }
    }
    
    /// <summary>
    /// Cache player data from current session before scene transition
    /// </summary>
    private void CachePlayerData()
    {
        playerDataCache.Clear();
        
        var playerSessionData = PlayerSessionData.Instance;
        if (playerSessionData == null)
        {
            Debug.LogWarning("[SimpleSceneTransition] PlayerSessionData not found, using basic caching");
            CacheBasicPlayerData();
            return;
        }
        
        var sessions = playerSessionData.GetConnectedPlayerSessions();
        Debug.Log($"[SimpleSceneTransition] Caching data for {sessions.Count} players");
        
        // CRITICAL DEBUG: Show all available sessions
        Debug.Log($"[SimpleSceneTransition] Available sessions ({sessions.Count}):");
        foreach (var session in sessions)
        {
            Debug.Log($"  - Player: '{session.playerName}' (ID: {session.playerId}, Character: {session.selectedCharacterId})");
            Debug.Log($"    ⚠️ CHARACTER SELECTION DEBUG: Player {session.playerId} has character ID {session.selectedCharacterId}");
        }
        
        // CRITICAL FIX: Cache data for ALL connected clients, not just sessions
        Debug.Log($"[SimpleSceneTransition] Caching data for all {NetworkManager.Singleton.ConnectedClients.Count} connected clients");
        
        // CRITICAL DEBUG: Show all connected clients
        Debug.Log($"[SimpleSceneTransition] Connected clients ({NetworkManager.Singleton.ConnectedClients.Count}):");
        foreach (var clientPair in NetworkManager.Singleton.ConnectedClients)
        {
            Debug.Log($"  - Client ID: {clientPair.Key}");
        }
        
        foreach (var clientPair in NetworkManager.Singleton.ConnectedClients)
        {
            var clientId = clientPair.Key;
            var client = clientPair.Value;
            
            // Try to find session data for this client
            // CRITICAL FIX: We need to map clientId to the authentication playerId, not compare them directly
            Debug.Log($"[SimpleSceneTransition] Looking for session for clientId {clientId}...");
            
            PlayerSessionInfo matchingSession = default;
            
            // First, try to get the session using the PlayerSessionData mapping
            if (playerSessionData != null)
            {
                // Get all sessions and try to find one that belongs to this client
                foreach (var session in sessions)
                {
                    Debug.Log($"    🔍 Checking session: playerId='{session.playerId}', characterId={session.selectedCharacterId}");
                }
                
                // MULTIPLAYER FIX: Use the clientIdToGuid mapping to find sessions for all clients
                if (clientId == 0) // Host client - use the working method
                {
                    var currentSession = playerSessionData.GetCurrentPlayerSession();
                    if (currentSession.HasValue)
                    {
                        matchingSession = currentSession.Value;
                        Debug.Log($"    ✅ Found host session: playerId='{matchingSession.playerId}', characterId={matchingSession.selectedCharacterId}");
                    }
                    else
                    {
                        Debug.LogError($"    ❌ No current player session found for host");
                    }
                }
                else
                {
                    // For other clients, use the clientIdToGuid mapping
                    Debug.Log($"    🔍 Looking for non-host client {clientId} using GetPlayerSessionByClientId...");
                    var clientSession = playerSessionData.GetPlayerSessionByClientId(clientId);
                    if (clientSession.HasValue)
                    {
                        matchingSession = clientSession.Value;
                        Debug.Log($"    ✅ Found client {clientId} session: playerId='{matchingSession.playerId}', characterId={matchingSession.selectedCharacterId}");
                    }
                    else
                    {
                        Debug.LogWarning($"    ⚠️ No session found for client {clientId} via GetPlayerSessionByClientId");
                    }
                }
            }
            
            var playerData = new PlayerData
            {
                clientId = clientId,
                characterId = 0, // Default
                playerName = $"Player_{clientId}" // Default
            };
            
            // CRITICAL FIX: Only use session data if a valid session was found
            if (!matchingSession.Equals(default) && matchingSession.selectedCharacterId >= 0)
            {
                playerData.characterId = matchingSession.selectedCharacterId;
                
                // Only use session name if it's not empty
                if (!string.IsNullOrEmpty(matchingSession.playerName.ToString()))
                {
                    playerData.playerName = matchingSession.playerName.ToString();
                }
                
                Debug.Log($"[SimpleSceneTransition] ✅ Found session for client {clientId}: Character={playerData.characterId}, Name='{playerData.playerName}'");
                Debug.Log($"    🎯 SESSION DEBUG: matchingSession.selectedCharacterId = {matchingSession.selectedCharacterId}");
            }
            else
            {
                Debug.LogError($"[SimpleSceneTransition] ❌ No valid session found for client {clientId}, using defaults");
                Debug.LogError($"    🎯 SESSION DEBUG: matchingSession.Equals(default) = {matchingSession.Equals(default)}");
                Debug.LogError($"    🎯 SESSION DEBUG: matchingSession.selectedCharacterId = {matchingSession.selectedCharacterId}");
                Debug.LogError($"    🎯 SESSION DEBUG: This means character selection was not saved properly!");
            }
            
            playerDataCache[clientId] = playerData;
            Debug.Log($"[SimpleSceneTransition] Cached player data - Client: {clientId}, Character: {playerData.characterId}, Name: '{playerData.playerName}'");
        }
        
        Debug.Log($"[SimpleSceneTransition] Player data caching completed: {playerDataCache.Count} entries");
    }
    
    /// <summary>
    /// Basic player data caching when PlayerSessionData is not available
    /// </summary>
    private void CacheBasicPlayerData()
    {
        foreach (var clientPair in NetworkManager.Singleton.ConnectedClients)
        {
            var clientId = clientPair.Key;
            var client = clientPair.Value;
            
            var playerData = new PlayerData
            {
                clientId = clientId,
                characterId = 0, // Default character
                playerName = $"Player_{clientId}"
            };
            
            playerDataCache[clientId] = playerData;
            Debug.Log($"[SimpleSceneTransition] Basic cache - Client: {clientId}");
        }
    }
    
    /// <summary>
    /// Load the game scene using NetworkSceneManager
    /// </summary>
    private void LoadGameScene()
    {
        var status = NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
        
        if (status == SceneEventProgressStatus.Started)
        {
            Debug.Log($"[SimpleSceneTransition] Loading game scene: {gameSceneName}");
        }
        else
        {
            Debug.LogError($"[SimpleSceneTransition] Failed to load game scene: {status}");
            isTransitioning.Value = false;
        }
    }
    
    /// <summary>
    /// Called when scene loading is completed on all clients
    /// This is the perfect time to spawn players with their data
    /// </summary>
    private void OnSceneLoadCompleted(string sceneName, LoadSceneMode loadSceneMode, System.Collections.Generic.List<ulong> clientsCompleted, System.Collections.Generic.List<ulong> clientsTimedOut)
    {
        Debug.Log($"[SimpleSceneTransition] Scene load completed: {sceneName}");
        Debug.Log($"[SimpleSceneTransition] Clients completed: {clientsCompleted.Count}, Timed out: {clientsTimedOut.Count}");
        
        if (!NetworkManager.Singleton.IsServer)
            return;
        
        // Only handle game scene loading
        if (sceneName == gameSceneName)
        {
            Debug.Log("[SimpleSceneTransition] Game scene loaded, spawning players...");
            StartCoroutine(SpawnPlayersAfterSceneLoad());
        }
        else if (sceneName == lobbySceneName)
        {
            Debug.Log("[SimpleSceneTransition] Lobby scene loaded");
            isTransitioning.Value = false;
        }
    }
    
    /// <summary>
    /// Spawn players with their cached data after scene loads
    /// </summary>
    private System.Collections.IEnumerator SpawnPlayersAfterSceneLoad()
    {
        // Wait a frame to ensure scene is fully initialized
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.1f);
        
        // Find the GameSpawnManager to handle player spawning
        Debug.Log("[SimpleSceneTransition] Looking for GameSpawnManager...");
        var spawnManager = FindFirstObjectByType<GameSpawnManager>();
        if (spawnManager == null)
        {
            Debug.LogError("[SimpleSceneTransition] GameSpawnManager not found in game scene!");
            
            // Try alternative search methods
            var allSpawnManagers = FindObjectsByType<GameSpawnManager>(FindObjectsSortMode.None);
            Debug.Log($"[SimpleSceneTransition] Found {allSpawnManagers.Length} GameSpawnManager objects via FindObjectsByType");
            
            isTransitioning.Value = false;
            yield break;
        }
        
        Debug.Log($"[SimpleSceneTransition] Found GameSpawnManager: {spawnManager.name}");
        Debug.Log($"[SimpleSceneTransition] Cached player data count: {playerDataCache.Count}");
        
        // Show all cached data
        foreach (var kvp in playerDataCache)
        {
            var data = kvp.Value;
            Debug.Log($"[SimpleSceneTransition] Cached data - Client: {data.clientId}, Character: {data.characterId}, Name: '{data.playerName}'");
        }
        
        // Spawn players with their cached data
        foreach (var playerData in playerDataCache.Values)
        {
            Debug.Log($"[SimpleSceneTransition] DETAILED: About to spawn player - Client: {playerData.clientId}, Character: {playerData.characterId}, Name: '{playerData.playerName}'");
            
            // CRITICAL DEBUG: Verify character data exists before spawning
            if (playerData.characterId >= 0)
            {
                var characterData = CharacterRegistry.Instance?.GetCharacterByID(playerData.characterId);
                if (characterData != null)
                {
                    Debug.Log($"[SimpleSceneTransition] ✅ Character data exists for ID {playerData.characterId}: '{characterData.characterName}'");
                }
                else
                {
                    Debug.LogError($"[SimpleSceneTransition] ❌ Character data NOT found for ID {playerData.characterId}!");
                }
            }
            else
            {
                Debug.LogWarning($"[SimpleSceneTransition] ⚠️ Player {playerData.clientId} has invalid character ID: {playerData.characterId} (negative)");
            }
            
            Debug.Log($"[SimpleSceneTransition] Calling SpawnPlayerWithData - Client: {playerData.clientId}, Character: {playerData.characterId}, Name: '{playerData.playerName}'");
            spawnManager.SpawnPlayerWithData(playerData.clientId, playerData.characterId, playerData.playerName);
        }
        
        // Transition complete
        isTransitioning.Value = false;
        Debug.Log("[SimpleSceneTransition] Game transition completed successfully!");
    }
    
    /// <summary>
    /// Get cached player data for a specific client
    /// </summary>
    public PlayerData? GetPlayerData(ulong clientId)
    {
        if (playerDataCache.TryGetValue(clientId, out var data))
        {
            return data;
        }
        return null;
    }
    
    /// <summary>
    /// Check if currently transitioning
    /// </summary>
    public bool IsTransitioning()
    {
        return isTransitioning.Value;
    }
    
    /// <summary>
    /// Get current scene type
    /// </summary>
    public bool IsInLobby()
    {
        return SceneManager.GetActiveScene().name == lobbySceneName;
    }
    
    public bool IsInGame()
    {
        return SceneManager.GetActiveScene().name == gameSceneName;
    }
    
    /// <summary>
    /// Debug method to manually trigger player spawning (for testing)
    /// </summary>
    [ContextMenu("Debug - Manual Spawn Players")]
    public void DebugManualSpawnPlayers()
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("[SimpleSceneTransition] Only server can manually spawn players");
            return;
        }
        
        Debug.Log("[SimpleSceneTransition] Manual spawn triggered");
        StartCoroutine(SpawnPlayersAfterSceneLoad());
    }
    
    /// <summary>
    /// Debug method to check current state
    /// </summary>
    [ContextMenu("Debug - Check Transition State")]
    public void DebugCheckTransitionState()
    {
        Debug.Log("=== SimpleSceneTransition State ===");
        Debug.Log($"Current scene: {SceneManager.GetActiveScene().name}");
        Debug.Log($"Is transitioning: {isTransitioning.Value}");
        Debug.Log($"Is in lobby: {IsInLobby()}");
        Debug.Log($"Is in game: {IsInGame()}");
        Debug.Log($"Cached player data: {playerDataCache.Count}");
        Debug.Log($"NetworkManager IsServer: {NetworkManager.Singleton?.IsServer}");
        
        foreach (var kvp in playerDataCache)
        {
            var data = kvp.Value;
            Debug.Log($"  - Client {data.clientId}: Character {data.characterId}, Name '{data.playerName}'");
        }
        
        Debug.Log("==================================");
    }
}