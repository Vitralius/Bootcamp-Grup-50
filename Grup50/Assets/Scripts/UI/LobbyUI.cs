using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public class LobbyUI : MonoBehaviour
{
    [Header("Lobby Info")]
    [SerializeField] private TMP_Text lobbyCodeText;
    [SerializeField] private TMP_Text lobbyTitleText;
    [SerializeField] private Button copyLobbyCodeButton;
    
    [Header("Player List")]
    [SerializeField] private Transform playerListParent;
    [SerializeField] private GameObject playerListItemPrefab;
    [SerializeField] private TMP_Text playerCountText;
    
    [Header("Ready System")]
    [SerializeField] private Button readyButton;
    [SerializeField] private TMP_Text readyButtonText;
    [SerializeField] private TMP_Text readyStatusText;
    
    [Header("Game Controls")]
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button leaveLobbyButton;
    
    [Header("Status")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject loadingPanel;
    
    private List<GameObject> playerListItems = new List<GameObject>();
    private bool isReady = false;
    private LobbyReadySystem readySystem;
    
    private void Start()
    {
        SetupUI();
        SubscribeToEvents();
        
        // Get or create ready system
        readySystem = FindFirstObjectByType<LobbyReadySystem>();
        if (readySystem == null)
        {
            GameObject readySystemGO = new GameObject("LobbyReadySystem");
            readySystem = readySystemGO.AddComponent<LobbyReadySystem>();
        }
        
        UpdateUI();
    }
    
    private void SetupUI()
    {
        // Setup button listeners
        if (copyLobbyCodeButton != null)
            copyLobbyCodeButton.onClick.AddListener(OnCopyLobbyCodeClicked);
        
        if (readyButton != null)
            readyButton.onClick.AddListener(OnReadyButtonClicked);
        
        if (startGameButton != null)
            startGameButton.onClick.AddListener(OnStartGameClicked);
        
        if (leaveLobbyButton != null)
            leaveLobbyButton.onClick.AddListener(OnLeaveLobbyClicked);
        
        // Set initial UI state
        if (loadingPanel != null)
            loadingPanel.SetActive(false);
        
        if (lobbyTitleText != null)
            lobbyTitleText.text = "Game Lobby";
        
        UpdateReadyButton();
        UpdateStartGameButton();
    }
    
    private void SubscribeToEvents()
    {
        if (MultiplayerManager.Instance != null)
        {
            MultiplayerManager.Instance.OnLobbyPlayersUpdated += OnLobbyPlayersUpdated;
            MultiplayerManager.Instance.OnLobbyLeft += OnLobbyLeft;
            MultiplayerManager.Instance.OnLobbyError += OnLobbyError;
        }
        
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.OnSceneTransitionStarted += OnSceneTransitionStarted;
            SceneTransitionManager.Instance.OnSceneTransitionFailed += OnSceneTransitionFailed;
        }
    }
    
    private void UnsubscribeFromEvents()
    {
        if (MultiplayerManager.Instance != null)
        {
            MultiplayerManager.Instance.OnLobbyPlayersUpdated -= OnLobbyPlayersUpdated;
            MultiplayerManager.Instance.OnLobbyLeft -= OnLobbyLeft;
            MultiplayerManager.Instance.OnLobbyError -= OnLobbyError;
        }
        
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.OnSceneTransitionStarted -= OnSceneTransitionStarted;
            SceneTransitionManager.Instance.OnSceneTransitionFailed -= OnSceneTransitionFailed;
        }
    }
    
    private void UpdateUI()
    {
        UpdateLobbyInfo();
        UpdatePlayerList();
        UpdateReadyStatus();
        UpdateStartGameButton();
    }
    
    private void UpdateLobbyInfo()
    {
        if (MultiplayerManager.Instance != null)
        {
            string lobbyCode = MultiplayerManager.Instance.GetCurrentLobbyCode();
            
            if (lobbyCodeText != null)
            {
                lobbyCodeText.text = string.IsNullOrEmpty(lobbyCode) ? "No Code" : lobbyCode;
            }
        }
    }
    
    private void UpdatePlayerList()
    {
        // Clear existing player list items
        foreach (var item in playerListItems)
        {
            if (item != null)
                Destroy(item);
        }
        playerListItems.Clear();
        
        if (MultiplayerManager.Instance != null && playerListParent != null && playerListItemPrefab != null)
        {
            var playerNames = MultiplayerManager.Instance.GetLobbyPlayerNames();
            
            foreach (var playerName in playerNames)
            {
                GameObject playerItem = Instantiate(playerListItemPrefab, playerListParent);
                playerListItems.Add(playerItem);
                
                // Update player item UI
                var nameText = playerItem.GetComponentInChildren<TMP_Text>();
                if (nameText != null)
                {
                    nameText.text = playerName;
                }
                
                // Add ready status indicator if available
                var readyIndicator = playerItem.transform.Find("ReadyIndicator");
                if (readyIndicator != null && readySystem != null)
                {
                    bool isPlayerReady = readySystem.IsPlayerReady(playerName);
                    readyIndicator.gameObject.SetActive(isPlayerReady);
                }
            }
            
            // Update player count
            if (playerCountText != null)
            {
                playerCountText.text = $"Players: {playerNames.Count}/4";
            }
        }
    }
    
    private void UpdateReadyButton()
    {
        if (readyButton != null && readyButtonText != null)
        {
            readyButtonText.text = isReady ? "Not Ready" : "Ready";
            
            // Change button color based on ready state
            var colors = readyButton.colors;
            colors.normalColor = isReady ? Color.red : Color.green;
            readyButton.colors = colors;
        }
    }
    
    private void UpdateReadyStatus()
    {
        if (readyStatusText != null && readySystem != null)
        {
            int readyCount = readySystem.GetReadyPlayerCount();
            int totalCount = MultiplayerManager.Instance?.GetLobbyPlayerNames().Count ?? 0;
            
            readyStatusText.text = $"Ready: {readyCount}/{totalCount}";
        }
    }
    
    private void UpdateStartGameButton()
    {
        if (startGameButton != null)
        {
            // Only host can start the game
            bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
            bool allReady = readySystem != null && readySystem.AreAllPlayersReady();
            bool hasPlayers = MultiplayerManager.Instance?.GetLobbyPlayerNames().Count > 0;
            
            startGameButton.gameObject.SetActive(isHost);
            startGameButton.interactable = allReady && hasPlayers;
        }
    }
    
    private void OnCopyLobbyCodeClicked()
    {
        if (MultiplayerManager.Instance != null)
        {
            MultiplayerManager.Instance.CopyLobbyCodeToClipboard();
            UpdateStatusText("Lobby code copied to clipboard!");
        }
    }
    
    private void OnReadyButtonClicked()
    {
        if (readySystem != null)
        {
            isReady = !isReady;
            readySystem.SetPlayerReady(isReady);
            
            UpdateReadyButton();
            UpdateStatusText(isReady ? "You are ready!" : "You are not ready");
        }
    }
    
    private void OnStartGameClicked()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            if (readySystem != null && readySystem.AreAllPlayersReady())
            {
                UpdateStatusText("Starting game...");
                
                if (SceneTransitionManager.Instance != null)
                {
                    SceneTransitionManager.Instance.TransitionToGame();
                }
            }
            else
            {
                UpdateStatusText("Not all players are ready!");
            }
        }
        else
        {
            UpdateStatusText("Only the host can start the game!");
        }
    }
    
    private async void OnLeaveLobbyClicked()
    {
        if (MultiplayerManager.Instance != null)
        {
            UpdateStatusText("Leaving lobby...");
            await MultiplayerManager.Instance.LeaveLobby();
            
            // Return to main menu
            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.TransitionToMainMenu();
            }
        }
    }
    
    private void OnLobbyPlayersUpdated(List<string> playerNames)
    {
        UpdatePlayerList();
        UpdateReadyStatus();
        UpdateStartGameButton();
    }
    
    private void OnLobbyLeft()
    {
        UpdateStatusText("Left lobby");
    }
    
    private void OnLobbyError(string errorMessage)
    {
        UpdateStatusText($"Error: {errorMessage}");
    }
    
    private void OnSceneTransitionStarted(string sceneName)
    {
        UpdateStatusText($"Loading {sceneName}...");
        ShowLoading(true);
    }
    
    private void OnSceneTransitionFailed(string sceneName)
    {
        UpdateStatusText($"Failed to load {sceneName}");
        ShowLoading(false);
    }
    
    private void ShowLoading(bool show)
    {
        if (loadingPanel != null)
            loadingPanel.SetActive(show);
    }
    
    private void UpdateStatusText(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
            Debug.Log($"Lobby Status: {message}");
        }
    }
    
    private void OnDestroy()
    {
        UnsubscribeFromEvents();
        
        // Clean up button listeners
        if (copyLobbyCodeButton != null)
            copyLobbyCodeButton.onClick.RemoveAllListeners();
        
        if (readyButton != null)
            readyButton.onClick.RemoveAllListeners();
        
        if (startGameButton != null)
            startGameButton.onClick.RemoveAllListeners();
        
        if (leaveLobbyButton != null)
            leaveLobbyButton.onClick.RemoveAllListeners();
    }
}