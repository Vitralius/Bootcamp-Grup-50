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
    
    private void OnEnable()
    {
        LocalizationManager.OnLanguageChanged += OnLanguageChanged;
    }
    
    private void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
    }
    
    private void OnLanguageChanged()
    {
        // Refresh all UI text while preserving dynamic content
        if (lobbyTitleText != null)
            lobbyTitleText.text = LocalizationManager.Instance.GetLocalizedText("game_title");
            
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
            lobbyTitleText.text = LocalizationManager.Instance.GetLocalizedText("game_title");
        
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
        LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
        
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
                if (string.IsNullOrEmpty(lobbyCode))
                {
                    lobbyCodeText.text = LocalizationManager.Instance.GetLocalizedText("error_no_code");
                }
                else
                {
                    lobbyCodeText.text = string.Format("{0}: {1}", LocalizationManager.Instance.GetLocalizedText("lobby_code_label"), lobbyCode);
                }
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
                playerCountText.text = string.Format("{0}: {1}/4", LocalizationManager.Instance.GetLocalizedText("players_list"), playerNames.Count);
            }
        }
    }
    
    private void UpdateReadyButton()
    {
        if (readyButton != null && readyButtonText != null)
        {
            // Disable LocalizedText component if it exists to prevent interference
            var localizedText = readyButtonText.GetComponent<LocalizedText>();
            if (localizedText != null)
            {
                localizedText.enabled = false;
            }
            
            // Show current status (what the player IS)
            readyButtonText.text = isReady ? LocalizationManager.Instance.GetLocalizedText("menu_ready") : LocalizationManager.Instance.GetLocalizedText("menu_not_ready");
            
            // Change button color based on ready state
            var colors = readyButton.colors;
            colors.normalColor = isReady ? Color.green : Color.red;
            readyButton.colors = colors;
            
            Debug.Log($"Lobby Ready button updated: isReady={isReady}, text='{readyButtonText.text}'");
        }
    }
    
    private void UpdateReadyStatus()
    {
        if (readyStatusText != null && readySystem != null)
        {
            int readyCount = readySystem.GetReadyPlayerCount();
            int totalCount = MultiplayerManager.Instance?.GetLobbyPlayerNames().Count ?? 0;
            
            readyStatusText.text = string.Format("{0}: {1}/{2}", LocalizationManager.Instance.GetLocalizedText("menu_ready"), readyCount, totalCount);
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
            UpdateStatusText(LocalizationManager.Instance.GetLocalizedText("status_lobby_code_copied"));
        }
    }
    
    private void OnReadyButtonClicked()
    {
        if (readySystem != null)
        {
            isReady = !isReady;
            readySystem.SetPlayerReady(isReady);
            
            UpdateReadyButton();
            UpdateStatusText(isReady ? LocalizationManager.Instance.GetLocalizedText("menu_ready") : LocalizationManager.Instance.GetLocalizedText("menu_not_ready"));
        }
    }
    
    private void OnStartGameClicked()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            if (readySystem != null && readySystem.AreAllPlayersReady())
            {
                UpdateStatusText(LocalizationManager.Instance.GetLocalizedText("status_starting"));
                
                if (SceneTransitionManager.Instance != null)
                {
                    SceneTransitionManager.Instance.TransitionToGame();
                }
            }
            else
            {
                UpdateStatusText(LocalizationManager.Instance.GetLocalizedText("status_players_not_ready"));
            }
        }
        else
        {
            UpdateStatusText(LocalizationManager.Instance.GetLocalizedText("status_only_host_can_start"));
        }
    }
    
    private async void OnLeaveLobbyClicked()
    {
        if (MultiplayerManager.Instance != null)
        {
            UpdateStatusText(LocalizationManager.Instance.GetLocalizedText("status_leaving_lobby"));
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
        UpdateStatusText(LocalizationManager.Instance.GetLocalizedText("status_left_lobby"));
    }
    
    private void OnLobbyError(string errorMessage)
    {
        UpdateStatusText(string.Format(LocalizationManager.Instance.GetLocalizedText("error_generic"), errorMessage));
    }
    
    private void OnSceneTransitionStarted(string sceneName)
    {
        UpdateStatusText(string.Format(LocalizationManager.Instance.GetLocalizedText("status_loading_scene"), sceneName));
        ShowLoading(true);
    }
    
    private void OnSceneTransitionFailed(string sceneName)
    {
        UpdateStatusText(string.Format(LocalizationManager.Instance.GetLocalizedText("error_failed_load_scene"), sceneName));
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