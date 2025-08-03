using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

[RequireComponent(typeof(HealthComponent))]
public class MapSpecificDestructible : NetworkBehaviour
{
    [Header("Map Restrictions")]
    [SerializeField] private string[] allowedMaps = { "Map1" };
    [SerializeField] private bool destroyOnWrongMap = true;
    [SerializeField] private bool disableOnWrongMap = false;
    
    [Header("Map Detection")]
    [SerializeField] private MapDetectionMethod detectionMethod = MapDetectionMethod.SceneName;
    [SerializeField] private string customMapIdentifier = "";
    
    public enum MapDetectionMethod
    {
        SceneName,          // Use Unity scene name
        CustomIdentifier,   // Use custom string
        MapManager         // Use MapObjectManager
    }
    
    private HealthComponent healthComponent;
    private DestructibleObject destructibleObject;
    private bool isValidForCurrentMap = true;
    
    public override void OnNetworkSpawn()
    {
        healthComponent = GetComponent<HealthComponent>();
        destructibleObject = GetComponent<DestructibleObject>();
        
        // Check if this object should exist on current map
        CheckMapValidity();
        
        if (!isValidForCurrentMap)
        {
            HandleInvalidMap();
        }
    }
    
    private void CheckMapValidity()
    {
        string currentMap = GetCurrentMapName();
        isValidForCurrentMap = System.Array.Exists(allowedMaps, map => map.Equals(currentMap, System.StringComparison.OrdinalIgnoreCase));
        
        Debug.Log($"[MapSpecificDestructible] {gameObject.name} checking map '{currentMap}' - Valid: {isValidForCurrentMap}");
    }
    
    private string GetCurrentMapName()
    {
        switch (detectionMethod)
        {
            case MapDetectionMethod.SceneName:
                return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                
            case MapDetectionMethod.CustomIdentifier:
                return customMapIdentifier;
                
            case MapDetectionMethod.MapManager:
                MapObjectManager mapManager = FindFirstObjectByType<MapObjectManager>();
                return mapManager != null ? mapManager.GetCurrentMap() : "Unknown";
                
            default:
                return "Unknown";
        }
    }
    
    private void HandleInvalidMap()
    {
        if (!IsServer) return;
        
        if (destroyOnWrongMap)
        {
            Debug.Log($"[MapSpecificDestructible] Destroying {gameObject.name} - not valid for current map");
            
            // Despawn the object
            GetComponent<NetworkObject>().Despawn();
        }
        else if (disableOnWrongMap)
        {
            Debug.Log($"[MapSpecificDestructible] Disabling {gameObject.name} - not valid for current map");
            
            // Disable the object
            DisableObjectClientRpc();
        }
    }
    
    [ClientRpc]
    private void DisableObjectClientRpc()
    {
        // Disable rendering and collision
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            renderer.enabled = false;
        }
        
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider collider in colliders)
        {
            collider.enabled = false;
        }
        
        // Disable components
        if (healthComponent != null)
            healthComponent.enabled = false;
            
        if (destructibleObject != null)
            destructibleObject.enabled = false;
    }
    
    [ClientRpc]
    private void EnableObjectClientRpc()
    {
        // Re-enable rendering and collision
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            renderer.enabled = true;
        }
        
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider collider in colliders)
        {
            collider.enabled = true;
        }
        
        // Re-enable components
        if (healthComponent != null)
            healthComponent.enabled = true;
            
        if (destructibleObject != null)
            destructibleObject.enabled = true;
    }
    
    // Public methods for runtime map changes
    public void AddAllowedMap(string mapName)
    {
        if (!System.Array.Exists(allowedMaps, map => map.Equals(mapName, System.StringComparison.OrdinalIgnoreCase)))
        {
            List<string> mapList = new List<string>(allowedMaps);
            mapList.Add(mapName);
            allowedMaps = mapList.ToArray();
        }
    }
    
    public void RemoveAllowedMap(string mapName)
    {
        List<string> mapList = new List<string>(allowedMaps);
        mapList.RemoveAll(map => map.Equals(mapName, System.StringComparison.OrdinalIgnoreCase));
        allowedMaps = mapList.ToArray();
    }
    
    public bool IsValidForCurrentMap()
    {
        return isValidForCurrentMap;
    }
    
    public string[] GetAllowedMaps()
    {
        return allowedMaps;
    }
    
    // Call this when switching maps
    public void RefreshMapValidity()
    {
        if (IsServer)
        {
            CheckMapValidity();
            
            if (!isValidForCurrentMap)
            {
                HandleInvalidMap();
            }
            else if (disableOnWrongMap)
            {
                // Re-enable if it was disabled
                EnableObjectClientRpc();
            }
        }
    }
    
    // Debug method
    [ContextMenu("Debug - Check Map Validity")]
    private void DebugCheckMapValidity()
    {
        CheckMapValidity();
        Debug.Log($"Current Map: {GetCurrentMapName()}, Valid: {isValidForCurrentMap}, Allowed Maps: [{string.Join(", ", allowedMaps)}]");
    }
}