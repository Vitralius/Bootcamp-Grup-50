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
        if (IsServer)
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
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            if (client.PlayerObject != null)
            {
                // Move existing player to spawn position
                MovePlayerToSpawn(client.PlayerObject, clientId);
                return;
            }
        }
        
        // Spawn new player if needed
        Vector3 spawnPosition = GetSpawnPosition(clientId);
        
        if (playerPrefab != null)
        {
            GameObject playerObject = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
            NetworkObject netObj = playerObject.GetComponent<NetworkObject>();
            netObj.SpawnAsPlayerObject(clientId);
            spawnedPlayers[clientId] = playerObject;
        }
    }
    
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
}