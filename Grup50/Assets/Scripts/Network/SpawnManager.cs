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
            netObj.SpawnAsPlayerObject(clientId);
            spawnedPlayers[clientId] = playerObject;
            
            Debug.Log($"Spawned new player for client {clientId} at position {spawnPosition}");
            
            // Apply character selection after a brief delay to ensure network sync
            if (SceneTransitionManager.Instance != null && SceneTransitionManager.Instance.IsInGame())
            {
                Invoke(nameof(DelayedCharacterApplication), 0.2f);
            }
        }
    }
    
    private void DelayedCharacterApplication()
    {
        // Apply character selections to all spawned players
        ApplyCharacterSelectionsToAllPlayers();
    }
    
    private void ApplyCharacterSelectionToExistingPlayer(NetworkObject playerObject, ulong clientId)
    {
        var characterLoader = playerObject.GetComponent<CharacterLoader>();
        if (characterLoader == null)
        {
            Debug.LogWarning($"Player {clientId} does not have CharacterLoader component!");
            return;
        }
        
        // Get character selection from PlayerSessionData
        var playerSessionData = PlayerSessionData.Instance;
        if (playerSessionData == null)
        {
            Debug.LogWarning("PlayerSessionData not found, cannot apply character selection");
            return;
        }
        
        var connectedSessions = playerSessionData.GetConnectedPlayerSessions();
        foreach (var session in connectedSessions)
        {
            // Find the session that corresponds to this client
            var connectedClient = NetworkManager.Singleton.ConnectedClients[clientId];
            if (connectedClient.PlayerObject == playerObject)
            {
                if (session.selectedCharacterId != 0) // 0 is default/no selection
                {
                    var characterData = CharacterRegistry.Instance?.GetCharacterByID(session.selectedCharacterId);
                    if (characterData != null)
                    {
                        Debug.Log($"Applying character {characterData.characterName} to existing player {session.playerName} (ClientID: {clientId})");
                        characterLoader.LoadCharacter(characterData);
                        
                        // Sync to the specific client
                        ApplyCharacterToPlayerClientRpc(session.selectedCharacterId, new ClientRpcParams
                        {
                            Send = new ClientRpcSendParams
                            {
                                TargetClientIds = new ulong[] { clientId }
                            }
                        });
                    }
                }
                break;
            }
        }
    }
    
    public void ApplyCharacterSelectionsToAllPlayers()
    {
        if (!IsServer) return;
        
        var playerSessionData = PlayerSessionData.Instance;
        if (playerSessionData == null)
        {
            Debug.LogWarning("PlayerSessionData not found, cannot apply character selections");
            return;
        }
        
        var connectedSessions = playerSessionData.GetConnectedPlayerSessions();
        Debug.Log($"SpawnManager: Applying character selections to {connectedSessions.Count} players");
        
        foreach (var session in connectedSessions)
        {
            if (session.selectedCharacterId != 0) // 0 is default/no selection
            {
                // Find the corresponding NetworkObject for this player
                foreach (var clientPair in NetworkManager.Singleton.ConnectedClients)
                {
                    var client = clientPair.Value;
                    if (client.PlayerObject != null)
                    {
                        var characterLoader = client.PlayerObject.GetComponent<CharacterLoader>();
                        if (characterLoader != null)
                        {
                            var characterData = CharacterRegistry.Instance?.GetCharacterByID(session.selectedCharacterId);
                            if (characterData != null)
                            {
                                Debug.Log($"SpawnManager: Applying character {characterData.characterName} to player {session.playerName} (ClientID: {client.ClientId})");
                                
                                // Apply character on the server
                                characterLoader.LoadCharacter(characterData);
                                
                                // Sync to the specific client
                                ApplyCharacterToPlayerClientRpc(session.selectedCharacterId, new ClientRpcParams
                                {
                                    Send = new ClientRpcSendParams
                                    {
                                        TargetClientIds = new ulong[] { client.ClientId }
                                    }
                                });
                                break; // Found the client for this session, move to next session
                            }
                        }
                    }
                }
            }
        }
    }
    
    [ClientRpc]
    private void ApplyCharacterToPlayerClientRpc(int characterId, ClientRpcParams clientRpcParams = default)
    {
        // On the client side, find the local player's CharacterLoader and apply the character
        var characterLoaders = FindObjectsByType<CharacterLoader>(FindObjectsSortMode.None);
        
        foreach (var loader in characterLoaders)
        {
            if (loader.IsOwner)
            {
                var characterData = CharacterRegistry.Instance?.GetCharacterByID(characterId);
                if (characterData != null)
                {
                    loader.LoadCharacter(characterData);
                    Debug.Log($"[CLIENT] SpawnManager applied character {characterData.characterName} to local player");
                }
                break;
            }
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