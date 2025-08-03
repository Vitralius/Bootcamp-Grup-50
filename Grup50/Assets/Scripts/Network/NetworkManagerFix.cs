using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Forces NetworkManager to disable automatic player spawning to prevent conflicts with SpawnManager
/// </summary>
public class NetworkManagerFix : MonoBehaviour
{
    [Header("Network Spawn Configuration")]
    [SerializeField] private bool disableAutoSpawn = true;
    [SerializeField] private bool showDebugLogs = true;
    
    void Awake()
    {
        // Apply fix before NetworkManager starts
        ApplyNetworkManagerFix();
    }
    
    void Start()
    {
        // Double-check the fix was applied
        if (showDebugLogs)
        {
            LogNetworkManagerSettings();
        }
    }
    
    /// <summary>
    /// Disable Unity's automatic player spawning to let SpawnManager handle it
    /// </summary>
    public void ApplyNetworkManagerFix()
    {
        if (NetworkManager.Singleton != null)
        {
            var networkConfig = NetworkManager.Singleton.NetworkConfig;
            
            if (disableAutoSpawn)
            {
                // CRITICAL FIX: Disable automatic player spawning
                networkConfig.PlayerPrefab = null;
                networkConfig.AutoSpawnPlayerPrefabClientSide = false;
                
                if (showDebugLogs)
                {
                    Debug.Log("[NetworkManagerFix] ✅ Disabled automatic player spawning");
                    Debug.Log($"[NetworkManagerFix] PlayerPrefab: {networkConfig.PlayerPrefab}");
                    Debug.Log($"[NetworkManagerFix] AutoSpawnPlayerPrefabClientSide: {networkConfig.AutoSpawnPlayerPrefabClientSide}");
                }
            }
        }
        else if (showDebugLogs)
        {
            Debug.LogWarning("[NetworkManagerFix] NetworkManager.Singleton is null - fix will be applied when available");
        }
    }
    
    /// <summary>
    /// Log current NetworkManager settings for debugging
    /// </summary>
    public void LogNetworkManagerSettings()
    {
        if (NetworkManager.Singleton != null)
        {
            var networkConfig = NetworkManager.Singleton.NetworkConfig;
            
            Debug.Log("=== NetworkManager Configuration ===");
            Debug.Log($"PlayerPrefab: {networkConfig.PlayerPrefab}");
            Debug.Log($"AutoSpawnPlayerPrefabClientSide: {networkConfig.AutoSpawnPlayerPrefabClientSide}");
            Debug.Log($"ForceSamePrefabs: {networkConfig.ForceSamePrefabs}");
            Debug.Log($"EnableSceneManagement: {networkConfig.EnableSceneManagement}");
            Debug.Log("====================================");
            
            // Check if settings are correct for custom spawning
            if (networkConfig.PlayerPrefab == null && !networkConfig.AutoSpawnPlayerPrefabClientSide)
            {
                Debug.Log("[NetworkManagerFix] ✅ Configuration is CORRECT for custom spawning");
            }
            else
            {
                Debug.LogError("[NetworkManagerFix] ❌ Configuration will cause conflicts with SpawnManager!");
                Debug.LogError($"  - PlayerPrefab should be null, currently: {networkConfig.PlayerPrefab}");
                Debug.LogError($"  - AutoSpawnPlayerPrefabClientSide should be false, currently: {networkConfig.AutoSpawnPlayerPrefabClientSide}");
            }
        }
        else
        {
            Debug.LogWarning("[NetworkManagerFix] Cannot log settings - NetworkManager.Singleton is null");
        }
    }
    
    /// <summary>
    /// Context menu method to manually apply the fix
    /// </summary>
    [ContextMenu("Apply Network Manager Fix")]
    public void ManualApplyFix()
    {
        ApplyNetworkManagerFix();
        LogNetworkManagerSettings();
    }
    
    /// <summary>
    /// Context menu method to check current settings
    /// </summary>
    [ContextMenu("Check Network Manager Settings")]
    public void ManualCheckSettings()
    {
        LogNetworkManagerSettings();
    }
    
    /// <summary>
    /// Called when NetworkManager becomes available
    /// </summary>
    void OnEnable()
    {
        // Apply fix when this component is enabled
        if (NetworkManager.Singleton != null)
        {
            ApplyNetworkManagerFix();
        }
    }
}