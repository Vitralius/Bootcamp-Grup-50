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
        
        OnSceneTransitionCompleted?.Invoke(sceneName);
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