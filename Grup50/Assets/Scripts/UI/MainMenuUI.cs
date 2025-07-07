using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainMenuUI : MonoBehaviour
{
    [Header("Main Menu UI")]
    [SerializeField] private Button createLobbyButton;
    [SerializeField] private Button joinLobbyButton;
    [SerializeField] private Button quitGameButton;
    
    [Header("Join Lobby Panel")]
    [SerializeField] private GameObject joinLobbyPanel;
    [SerializeField] private TMP_InputField lobbyCodeInputField;
    [SerializeField] private Button confirmJoinButton;
    [SerializeField] private Button cancelJoinButton;
    
    [Header("Lobby State UI")]
    [SerializeField] private GameObject lobbyStatePanel;
    [SerializeField] private TMP_Text lobbyCodeDisplayText;
    [SerializeField] private Button copyLobbyCodeButton;
    [SerializeField] private TMP_Text playersListText;
    [SerializeField] private Button readyButton;
    [SerializeField] private TMP_Text readyButtonText;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button leaveLobbyButton;
    
    [Header("Status UI")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject loadingPanel;
    
    [Header("Game Title")]
    [SerializeField] private TMP_Text gameTitleText;
    
    private bool isReady = false;
    private LobbyReadySystem readySystem;
    
    private void Start()
    {
        SetupUI();
        SubscribeToEvents();
        UpdateStatusText("Oynamaya başlamak için bir lobi oluşturun veya lobiye katılın.");
        
        UpdateUIState();
    }
    
    private void SetupUI()
    {
        // Setup main menu buttons
        if (createLobbyButton != null)
            createLobbyButton.onClick.AddListener(OnCreateLobbyClicked);
        
        if (joinLobbyButton != null)
            joinLobbyButton.onClick.AddListener(OnJoinLobbyClicked);
        
        if (quitGameButton != null)
            quitGameButton.onClick.AddListener(OnQuitGameClicked);
        
        // Setup join lobby panel buttons
        if (confirmJoinButton != null)
            confirmJoinButton.onClick.AddListener(OnConfirmJoinClicked);
        
        if (cancelJoinButton != null)
            cancelJoinButton.onClick.AddListener(OnCancelJoinClicked);
        
        // Setup lobby state buttons
        if (copyLobbyCodeButton != null)
            copyLobbyCodeButton.onClick.AddListener(OnCopyLobbyCodeClicked);
        
        if (readyButton != null)
            readyButton.onClick.AddListener(OnReadyButtonClicked);
        
        if (startGameButton != null)
            startGameButton.onClick.AddListener(OnStartGameClicked);
        
        if (leaveLobbyButton != null)
            leaveLobbyButton.onClick.AddListener(OnLeaveLobbyClicked);
        
        // Setup input field
        if (lobbyCodeInputField != null)
        {
            lobbyCodeInputField.characterLimit = 6;
            lobbyCodeInputField.contentType = TMP_InputField.ContentType.Alphanumeric;
            lobbyCodeInputField.onValueChanged.AddListener(OnLobbyCodeInputChanged);
        }
        
        // Set initial UI state
        if (joinLobbyPanel != null)
            joinLobbyPanel.SetActive(false);
        
        if (loadingPanel != null)
            loadingPanel.SetActive(false);
        
        // Set game title
        if (gameTitleText != null)
            gameTitleText.text = "Unity Multiplayer Game";
        
        SetButtonsInteractable(true);
    }
    
    private void SubscribeToEvents()
    {
        if (MultiplayerManager.Instance != null)
        {
            MultiplayerManager.Instance.OnLobbyCodeGenerated += OnLobbyCodeGenerated;
            MultiplayerManager.Instance.OnLobbyJoined += OnLobbyJoined;
            MultiplayerManager.Instance.OnLobbyLeft += OnLobbyLeft;
            MultiplayerManager.Instance.OnLobbyError += OnLobbyError;
            MultiplayerManager.Instance.OnLobbyPlayersUpdated += OnLobbyPlayersUpdated;
        }
        
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.OnSceneTransitionStarted += OnSceneTransitionStarted;
            SceneTransitionManager.Instance.OnSceneTransitionFailed += OnSceneTransitionFailed;
        }
        
        
        InitializeReadySystem();
    }
    
    private void InitializeReadySystem()
    {
        if (readySystem == null)
        {
            readySystem = FindFirstObjectByType<LobbyReadySystem>();
        }
        
        if (readySystem != null)
        {
            readySystem.OnPlayerReadyChanged += OnPlayerReadyChanged;
            readySystem.OnAllPlayersReadyChanged += OnAllPlayersReadyChanged;
        }
    }
    
    private void UnsubscribeFromEvents()
    {
        if (MultiplayerManager.Instance != null)
        {
            MultiplayerManager.Instance.OnLobbyCodeGenerated -= OnLobbyCodeGenerated;
            MultiplayerManager.Instance.OnLobbyJoined -= OnLobbyJoined;
            MultiplayerManager.Instance.OnLobbyLeft -= OnLobbyLeft;
            MultiplayerManager.Instance.OnLobbyError -= OnLobbyError;
            MultiplayerManager.Instance.OnLobbyPlayersUpdated -= OnLobbyPlayersUpdated;
        }
        
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.OnSceneTransitionStarted -= OnSceneTransitionStarted;
            SceneTransitionManager.Instance.OnSceneTransitionFailed -= OnSceneTransitionFailed;
        }
        
        if (readySystem != null)
        {
            readySystem.OnPlayerReadyChanged -= OnPlayerReadyChanged;
            readySystem.OnAllPlayersReadyChanged -= OnAllPlayersReadyChanged;
        }
    }
    
    private async void OnCreateLobbyClicked()
    {
        if (MultiplayerManager.Instance == null)
        {
            UpdateStatusText("MultiplayerManager not found!");
            return;
        }
        
        SetButtonsInteractable(false);
        ShowLoading(true);
        UpdateStatusText("Lobi Oluşturuluyor...");
        
        string lobbyCode = await MultiplayerManager.Instance.CreateLobby();
        
        if (string.IsNullOrEmpty(lobbyCode))
        {
            SetButtonsInteractable(true);
            ShowLoading(false);
            UpdateStatusText("Lobi oluşturulamadı!");
        }
    }
    
    private void OnJoinLobbyClicked()
    {
        if (joinLobbyPanel != null)
        {
            joinLobbyPanel.SetActive(true);
            
            // Focus on input field
            if (lobbyCodeInputField != null)
            {
                lobbyCodeInputField.text = "";
                lobbyCodeInputField.Select();
                lobbyCodeInputField.ActivateInputField();
            }
            
            // Update confirm button state
            UpdateConfirmJoinButtonState();
        }
    }
    
    private async void OnConfirmJoinClicked()
    {
        if (MultiplayerManager.Instance == null)
        {
            UpdateStatusText("MultiplayerManager not found!");
            return;
        }
        
        if (lobbyCodeInputField == null || string.IsNullOrEmpty(lobbyCodeInputField.text))
        {
            UpdateStatusText("Lütfen Lobi Kodu Girin");
            return;
        }
        
        string inputCode = lobbyCodeInputField.text.ToUpper().Trim();
        
        SetButtonsInteractable(false);
        ShowLoading(true);
        UpdateStatusText($"Lobi'ye katılınıyor {inputCode}...");
        
        if (joinLobbyPanel != null)
            joinLobbyPanel.SetActive(false);
        
        bool success = await MultiplayerManager.Instance.JoinLobbyByCode(inputCode);
        
        if (!success)
        {
            SetButtonsInteractable(true);
            ShowLoading(false);
            UpdateStatusText("Lobi'ye katılamadı");
        }
    }
    
    private void OnCancelJoinClicked()
    {
        if (joinLobbyPanel != null)
            joinLobbyPanel.SetActive(false);
    }
    
    private void OnQuitGameClicked()
    {
        Application.Quit();
        
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
    
    private void OnLobbyCodeInputChanged(string value)
    {
        UpdateConfirmJoinButtonState();
    }
    
    private void UpdateConfirmJoinButtonState()
    {
        if (confirmJoinButton != null && lobbyCodeInputField != null)
        {
            bool hasValidInput = !string.IsNullOrEmpty(lobbyCodeInputField.text) && 
                               lobbyCodeInputField.text.Trim().Length >= 4;
            confirmJoinButton.interactable = hasValidInput;
        }
    }
    
    private void OnLobbyCodeGenerated(string lobbyCode)
    {
        UpdateStatusText($"Lobi Kodu: {lobbyCode}");
        UpdateUIState();
    }
    
    private void OnLobbyJoined(string lobbyCode)
    {
        UpdateStatusText($"Lobi Kodu: {lobbyCode}");
        UpdateUIState();
    }
    
    private void OnLobbyLeft()
    {
        UpdateStatusText("Lobiden Çıkıldı");
        isReady = false;
        UpdateUIState();
    }
    
    private void OnLobbyPlayersUpdated(List<string> playerNames)
    {
        UpdatePlayersList(playerNames);
        UpdateStartGameButton();
    }
    
    private void OnLobbyError(string errorMessage)
    {
        UpdateStatusText($"Error: {errorMessage}");
        SetButtonsInteractable(true);
        ShowLoading(false);
    }
    
    private void OnSceneTransitionStarted(string sceneName)
    {
        UpdateStatusText($"Yükleniyor {sceneName}...");
    }
    
    private void OnSceneTransitionFailed(string sceneName)
    {
        UpdateStatusText($"Failed to load {sceneName}");
        SetButtonsInteractable(true);
        ShowLoading(false);
    }
    
    private void OnPlayerReadyChanged(string playerId, bool ready)
    {
        // Update local ready state if it's our player
        if (playerId == Unity.Services.Authentication.AuthenticationService.Instance.PlayerId)
        {
            isReady = ready;
            UpdateReadyButton();
        }
        
        // Update UI elements that depend on ready status
        UpdateStartGameButton();
        UpdatePlayersList();
    }
    
    private void OnAllPlayersReadyChanged(bool allReady)
    {
        UpdateStartGameButton();
        if (allReady && readySystem != null)
        {
            UpdateStatusText("Herkes Hazır! Başlıyabilirsiniz.");
        }
    }
    
    private void SetButtonsInteractable(bool interactable)
    {
        if (createLobbyButton != null)
            createLobbyButton.interactable = interactable;
        
        if (joinLobbyButton != null)
            joinLobbyButton.interactable = interactable;
        
        if (quitGameButton != null)
            quitGameButton.interactable = interactable;
    }
    
    private void ShowLoading(bool show)
    {
        if (loadingPanel != null)
            loadingPanel.SetActive(show);
    }
    
    private void UpdateUIState()
    {
        bool inLobby = MultiplayerManager.Instance != null && MultiplayerManager.Instance.IsInLobby();
        
        // Show/hide main menu vs lobby panels
        SetMainMenuButtonsVisible(!inLobby);
        if (lobbyStatePanel != null)
            lobbyStatePanel.SetActive(inLobby);
        
        if (inLobby)
        {
            UpdateLobbyInfo();
            UpdatePlayersList();
            UpdateReadyButton();
            UpdateStartGameButton();
        }
    }
    
    private void SetMainMenuButtonsVisible(bool visible)
    {
        if (createLobbyButton != null)
            createLobbyButton.gameObject.SetActive(visible);
        
        if (joinLobbyButton != null)
            joinLobbyButton.gameObject.SetActive(visible);
    }
    
    private void UpdateLobbyInfo()
    {
        if (MultiplayerManager.Instance != null && lobbyCodeDisplayText != null)
        {
            string lobbyCode = MultiplayerManager.Instance.GetCurrentLobbyCode();
            lobbyCodeDisplayText.text = string.IsNullOrEmpty(lobbyCode) ? "No Code" : $"Code: {lobbyCode}";
        }
    }
    
    private void UpdatePlayersList(List<string> playerNames = null)
    {
        if (playersListText != null)
        {
            if (playerNames == null && MultiplayerManager.Instance != null)
            {
                playerNames = MultiplayerManager.Instance.GetLobbyPlayerNames();
            }
            
            if (playerNames == null || playerNames.Count == 0)
            {
                playersListText.text = "No players";
            }
            else
            {
                playersListText.text = $"Oyuncular: ({playerNames.Count}/4):\n" + string.Join("\n", playerNames);
            }
        }
    }
    
    private void UpdateReadyButton()
    {
        if (readyButton != null && readyButtonText != null)
        {
            readyButtonText.text = isReady ? "Hazır" : "Hazır Değil";
            
            var colors = readyButton.colors;
            colors.normalColor = isReady ? Color.red : Color.green;
            readyButton.colors = colors;
        }
    }
    
    private void UpdateStartGameButton()
    {
        if (startGameButton != null)
        {
            bool isHost = Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsHost;
            bool hasPlayers = MultiplayerManager.Instance?.GetLobbyPlayerNames().Count > 0;
            bool allReady = readySystem != null && readySystem.AreAllPlayersReady();
            
            Debug.Log($"UpdateStartGameButton: isHost={isHost}, hasPlayers={hasPlayers}, allReady={allReady}");
            
            startGameButton.gameObject.SetActive(isHost);
            startGameButton.interactable = hasPlayers && allReady;
        }
    }
    
    // Lobby button handlers
    private void OnCopyLobbyCodeClicked()
    {
        if (MultiplayerManager.Instance != null)
        {
            MultiplayerManager.Instance.CopyLobbyCodeToClipboard();
            UpdateStatusText("Lobi Kodu Kopyalandı!");
        }
    }
    
    private void OnReadyButtonClicked()
    {
        if (readySystem != null)
        {
            isReady = !isReady;
            readySystem.SetPlayerReady(isReady);
            
            // UpdateReadyButton and status will be handled by OnPlayerReadyChanged event
        }
    }
    
    private void OnStartGameClicked()
    {
        if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsHost)
        {
            if (readySystem != null && readySystem.AreAllPlayersReady())
            {
                UpdateStatusText("Başlıyor...");
                
                if (SceneTransitionManager.Instance != null)
                {
                    SceneTransitionManager.Instance.TransitionToGame();
                }
            }
            else
            {
                UpdateStatusText("Hazır olmuyanlar var!");
            }
        }
        else
        {
            UpdateStatusText("Sadece kurucu oyunu başlatabilir!");
        }
    }
    
    private async void OnLeaveLobbyClicked()
    {
        if (MultiplayerManager.Instance != null)
        {
            UpdateStatusText("Lobiden çıkılıyor...");
            await MultiplayerManager.Instance.LeaveLobby();
        }
    }
    
    private void UpdateStatusText(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
            Debug.Log($"MainMenu Status: {message}");
        }
    }
    
    private void OnDestroy()
    {
        UnsubscribeFromEvents();
        
        // Clean up button listeners
        if (createLobbyButton != null)
            createLobbyButton.onClick.RemoveAllListeners();
        
        if (joinLobbyButton != null)
            joinLobbyButton.onClick.RemoveAllListeners();
        
        if (quitGameButton != null)
            quitGameButton.onClick.RemoveAllListeners();
        
        if (confirmJoinButton != null)
            confirmJoinButton.onClick.RemoveAllListeners();
        
        if (cancelJoinButton != null)
            cancelJoinButton.onClick.RemoveAllListeners();
        
        if (lobbyCodeInputField != null)
            lobbyCodeInputField.onValueChanged.RemoveAllListeners();
        
        // Clean up lobby button listeners
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