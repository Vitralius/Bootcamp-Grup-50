using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class MapObjectManager : NetworkBehaviour
{
    [Header("Map Configuration")]
    [SerializeField] private string currentMapName = "Map1";
    
    [Header("Map-Specific Objects")]
    [SerializeField] private MapObjectData[] mapObjects;
    
    [System.Serializable]
    public class MapObjectData
    {
        public string mapName;
        public GameObject[] objectsToSpawn;
        public Transform[] spawnPositions;
        public bool spawnOnStart = true;
    }
    
    private List<NetworkObject> spawnedObjects = new List<NetworkObject>();
    
    public override void OnNetworkSpawn()
    {
        // Only server spawns map objects
        if (IsServer)
        {
            SpawnMapObjects();
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void SpawnMapObjectsServerRpc()
    {
        SpawnMapObjects();
    }
    
    private void SpawnMapObjects()
    {
        if (!IsServer) return;
        
        // Find objects for current map
        MapObjectData currentMapData = System.Array.Find(mapObjects, data => data.mapName == currentMapName);
        
        if (currentMapData == null)
        {
            Debug.LogWarning($"[MapObjectManager] No objects defined for map: {currentMapName}");
            return;
        }
        
        // Spawn objects for this map
        for (int i = 0; i < currentMapData.objectsToSpawn.Length && i < currentMapData.spawnPositions.Length; i++)
        {
            GameObject prefab = currentMapData.objectsToSpawn[i];
            Transform spawnPoint = currentMapData.spawnPositions[i];
            
            if (prefab != null && spawnPoint != null)
            {
                GameObject spawnedObj = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
                
                NetworkObject networkObj = spawnedObj.GetComponent<NetworkObject>();
                if (networkObj != null)
                {
                    networkObj.Spawn();
                    spawnedObjects.Add(networkObj);
                    
                    Debug.Log($"[MapObjectManager] Spawned {prefab.name} on {currentMapName}");
                }
            }
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void DespawnMapObjectsServerRpc()
    {
        DespawnMapObjects();
    }
    
    private void DespawnMapObjects()
    {
        if (!IsServer) return;
        
        foreach (NetworkObject obj in spawnedObjects)
        {
            if (obj != null)
            {
                obj.Despawn();
            }
        }
        
        spawnedObjects.Clear();
        Debug.Log($"[MapObjectManager] Despawned all objects from {currentMapName}");
    }
    
    public void SetMap(string mapName)
    {
        if (currentMapName != mapName)
        {
            // Despawn current map objects
            if (spawnedObjects.Count > 0)
            {
                DespawnMapObjects();
            }
            
            // Set new map and spawn its objects
            currentMapName = mapName;
            SpawnMapObjects();
        }
    }
    
    // Public methods for external control
    public string GetCurrentMap()
    {
        return currentMapName;
    }
    
    public void RespawnMapObjects()
    {
        if (IsServer)
        {
            DespawnMapObjects();
            SpawnMapObjects();
        }
    }
}