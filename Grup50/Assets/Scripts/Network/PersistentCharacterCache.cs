using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Persistent character data cache to backup character selections across scene transitions
/// This provides a fallback system if NetworkVariables fail to sync properly
/// </summary>
public class PersistentCharacterCache : MonoBehaviour
{
    public static PersistentCharacterCache Instance { get; private set; }
    
    // Cache character selections by client ID
    private Dictionary<ulong, int> characterSelections = new Dictionary<ulong, int>();
    
    // Cache character selections by player GUID (from PlayerSessionData)
    private Dictionary<string, int> guidCharacterSelections = new Dictionary<string, int>();
    
    private void Awake()
    {
        // Singleton pattern with DontDestroyOnLoad
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        Debug.Log("PersistentCharacterCache: Initialized and marked as DontDestroyOnLoad");
    }
    
    /// <summary>
    /// Cache character selection by client ID
    /// </summary>
    public void CacheCharacterSelection(ulong clientId, int characterId)
    {
        characterSelections[clientId] = characterId;
        Debug.Log($"PersistentCharacterCache: Cached character {characterId} for client {clientId}");
        
        // Also save to PlayerPrefs for persistence across game sessions
        PlayerPrefs.SetInt($"CharacterCache_Client_{clientId}", characterId);
        PlayerPrefs.Save();
    }
    
    /// <summary>
    /// Cache character selection by player GUID
    /// </summary>
    public void CacheCharacterSelectionByGuid(string playerGuid, int characterId)
    {
        guidCharacterSelections[playerGuid] = characterId;
        Debug.Log($"PersistentCharacterCache: Cached character {characterId} for player GUID {playerGuid}");
        
        // Also save to PlayerPrefs for persistence across game sessions
        PlayerPrefs.SetInt($"CharacterCache_GUID_{playerGuid}", characterId);
        PlayerPrefs.Save();
    }
    
    /// <summary>
    /// Get cached character selection by client ID
    /// </summary>
    public int GetCachedCharacterSelection(ulong clientId)
    {
        if (characterSelections.ContainsKey(clientId))
        {
            return characterSelections[clientId];
        }
        
        // Fallback: Check PlayerPrefs
        int cachedValue = PlayerPrefs.GetInt($"CharacterCache_Client_{clientId}", -1);
        if (cachedValue != -1)
        {
            characterSelections[clientId] = cachedValue; // Update memory cache
            Debug.Log($"PersistentCharacterCache: Restored character {cachedValue} for client {clientId} from PlayerPrefs");
            return cachedValue;
        }
        
        return -1; // No cached selection found
    }
    
    /// <summary>
    /// Get cached character selection by player GUID
    /// </summary>
    public int GetCachedCharacterSelectionByGuid(string playerGuid)
    {
        if (guidCharacterSelections.ContainsKey(playerGuid))
        {
            return guidCharacterSelections[playerGuid];
        }
        
        // Fallback: Check PlayerPrefs
        int cachedValue = PlayerPrefs.GetInt($"CharacterCache_GUID_{playerGuid}", -1);
        if (cachedValue != -1)
        {
            guidCharacterSelections[playerGuid] = cachedValue; // Update memory cache
            Debug.Log($"PersistentCharacterCache: Restored character {cachedValue} for player GUID {playerGuid} from PlayerPrefs");
            return cachedValue;
        }
        
        return -1; // No cached selection found
    }
    
    /// <summary>
    /// Populate cache from PlayerSessionData
    /// </summary>
    public void PopulateCacheFromSessionData()
    {
        var playerSessionData = PlayerSessionData.Instance;
        if (playerSessionData == null)
        {
            Debug.LogWarning("PersistentCharacterCache: PlayerSessionData not found, cannot populate cache");
            return;
        }
        
        var connectedSessions = playerSessionData.GetConnectedPlayerSessions();
        Debug.Log($"PersistentCharacterCache: Populating cache from {connectedSessions.Count} player sessions");
        
        foreach (var session in connectedSessions)
        {
            if (session.selectedCharacterId != 0) // 0 = no selection
            {
                CacheCharacterSelectionByGuid(session.playerId.ToString(), session.selectedCharacterId);
            }
        }
    }
    
    /// <summary>
    /// Apply cached character selections to all spawned players
    /// </summary>
    public void ApplyCachedSelectionsToPlayers()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("PersistentCharacterCache: Can only apply cached selections on server");
            return;
        }
        
        Debug.Log("PersistentCharacterCache: Applying cached character selections to spawned players");
        
        var characterLoaders = FindObjectsByType<UltraSimpleMeshSwapper>(FindObjectsSortMode.None);
        
        foreach (var loader in characterLoaders)
        {
            var networkObject = loader.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.IsSpawned)
            {
                ulong clientId = networkObject.OwnerClientId;
                int cachedCharacterId = GetCachedCharacterSelection(clientId);
                
                if (cachedCharacterId != -1 && loader.GetNetworkCharacterId() == -1)
                {
                    Debug.Log($"PersistentCharacterCache: Applying cached character {cachedCharacterId} to client {clientId}");
                    loader.SetNetworkCharacterId(cachedCharacterId);
                }
            }
        }
    }
    
    /// <summary>
    /// Clear all cached data
    /// </summary>
    public void ClearCache()
    {
        characterSelections.Clear();
        guidCharacterSelections.Clear();
        
        // Clear PlayerPrefs cache (Note: This clears ALL character cache entries)
        var keys = new List<string>();
        for (int i = 0; i < 10; i++) // Assume max 10 clients
        {
            keys.Add($"CharacterCache_Client_{i}");
        }
        
        foreach (var key in keys)
        {
            if (PlayerPrefs.HasKey(key))
            {
                PlayerPrefs.DeleteKey(key);
            }
        }
        
        PlayerPrefs.Save();
        Debug.Log("PersistentCharacterCache: Cleared all cached data");
    }
    
    /// <summary>
    /// Get debug information about cached selections
    /// </summary>
    public string GetCacheDebugInfo()
    {
        var info = "=== PERSISTENT CHARACTER CACHE ===\n";
        info += $"Client ID Cache ({characterSelections.Count} entries):\n";
        
        foreach (var kvp in characterSelections)
        {
            var characterData = CharacterRegistry.Instance?.GetCharacterByID(kvp.Value);
            string characterName = characterData?.characterName ?? "Unknown";
            info += $"  Client {kvp.Key}: Character {kvp.Value} ({characterName})\n";
        }
        
        info += $"\nPlayer GUID Cache ({guidCharacterSelections.Count} entries):\n";
        foreach (var kvp in guidCharacterSelections)
        {
            var characterData = CharacterRegistry.Instance?.GetCharacterByID(kvp.Value);
            string characterName = characterData?.characterName ?? "Unknown";
            info += $"  GUID {kvp.Key}: Character {kvp.Value} ({characterName})\n";
        }
        
        return info;
    }
    
    [ContextMenu("Debug Cache Info")]
    public void DebugCacheInfo()
    {
        Debug.Log(GetCacheDebugInfo());
    }
    
    [ContextMenu("Populate Cache from Session Data")]
    public void DebugPopulateCache()
    {
        PopulateCacheFromSessionData();
    }
    
    [ContextMenu("Apply Cached Selections")]
    public void DebugApplyCache()
    {
        ApplyCachedSelectionsToPlayers();
    }
}