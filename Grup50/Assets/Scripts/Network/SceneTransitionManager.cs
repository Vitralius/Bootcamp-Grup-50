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
        if (NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoadEventCompleted;
        }
    }
    
    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoadEventCompleted;
        }
    }
    
    public void TransitionToMainMenu()
    {
        if (NetworkManager.Singleton.IsServer)
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
        if (NetworkManager.Singleton.IsServer)
        {
            LoadSceneServerRpc(gameSceneName);
        }
        else
        {
            Debug.LogWarning("Only the server can initiate scene transitions");
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void LoadSceneServerRpc(string sceneName)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("LoadSceneServerRpc called but not server");
            return;
        }
        
        try
        {
            OnSceneTransitionStarted?.Invoke(sceneName);
            
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
    
    private void OnSceneLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        Debug.Log($"Scene load event completed for {sceneName}. Clients completed: {clientsCompleted.Count}, Timed out: {clientsTimedOut.Count}");
        
        if (clientsTimedOut.Count > 0)
        {
            Debug.LogWarning($"Some clients timed out during scene load: {string.Join(", ", clientsTimedOut)}");
        }
        
        // Apply character selections after scene transition to game scene
        if (IsServer && sceneName == gameSceneName)
        {
            Debug.Log($"Game scene loaded, applying character selections to all players...");
            ApplyCharacterSelectionsAfterSceneLoad();
        }
        
        OnSceneTransitionCompleted?.Invoke(sceneName);
    }
    
    private void ApplyCharacterSelectionsAfterSceneLoad()
    {
        // Find the CharacterSelectionBridge to transfer character data
        var characterBridge = CharacterSelectionBridge.Instance;
        if (characterBridge != null)
        {
            Debug.Log("Transferring character selection data to gameplay...");
            characterBridge.TransferSessionDataToGameplay();
        }
        else
        {
            Debug.LogWarning("CharacterSelectionBridge not found! Character selections will not be applied.");
        }
        
        // Also apply character data through SpawnManager if available
        var spawnManager = FindFirstObjectByType<SpawnManager>();
        if (spawnManager != null)
        {
            Debug.Log("Requesting SpawnManager to apply character selections...");
            ApplyCharacterSelectionsToSpawnedPlayersServerRpc();
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
                foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
                {
                    if (client.PlayerObject != null)
                    {
                        var characterLoader = client.PlayerObject.GetComponent<CharacterLoader>();
                        if (characterLoader != null)
                        {
                            var characterData = CharacterRegistry.Instance?.GetCharacterByID(session.selectedCharacterId);
                            if (characterData != null)
                            {
                                Debug.Log($"Applying character {characterData.characterName} to player {session.playerName} (ClientID: {client.ClientId})");
                                
                                // Apply character on the server first
                                characterLoader.LoadCharacter(characterData);
                                
                                // Then sync to the specific client
                                ApplyCharacterToClientRpc(session.selectedCharacterId, new ClientRpcParams
                                {
                                    Send = new ClientRpcSendParams
                                    {
                                        TargetClientIds = new ulong[] { client.ClientId }
                                    }
                                });
                            }
                            break; // Move to next session
                        }
                    }
                }
            }
        }
    }
    
    [ClientRpc]
    private void ApplyCharacterToClientRpc(int characterId, ClientRpcParams clientRpcParams = default)
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
                    Debug.Log($"[CLIENT] Applied character {characterData.characterName} to local player after scene transition");
                }
                break;
            }
        }
    }
    
    
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