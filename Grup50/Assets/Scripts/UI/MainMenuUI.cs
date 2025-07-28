using System.Collections.Generic;
using System.Linq;
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
    
    [Header("Character Selection")]
    [SerializeField] private Button previousCharacterButton;
    [SerializeField] private Button nextCharacterButton;
    [SerializeField] private TMP_Text currentCharacterNameText;
    [SerializeField] private GameObject characterPreviewArea;
    
    [Header("Status UI")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject loadingPanel;
    
    private StatusTextManager statusManager;
    
    [Header("Game Title")]
    [SerializeField] private TMP_Text gameTitleText;
    
    private bool isReady = false;
    private LobbyReadySystem readySystem;
    private PlayerSessionData playerSessionData;
    
    // Character selection
    private List<CharacterData> availableCharacters;
    private int currentCharacterIndex = 0;
    private CharacterData currentSelectedCharacter;
    
    private void Start()
    {
        SetupStatusManager();
        SetupUI();
        SubscribeToEvents();
        statusManager.ShowReadyToStart();
        
        // Initialize character selection early
        InitializeCharacterSelection();
        
        UpdateUIState();
    }
    
    private void SetupStatusManager()
    {
        statusManager = statusText.GetComponent<StatusTextManager>();
        if (statusManager == null)
        {
            statusManager = statusText.gameObject.AddComponent<StatusTextManager>();
        }
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
        // Update game title
        if (gameTitleText != null)
            gameTitleText.text = LocalizationManager.Instance.GetLocalizedText("game_title");
            
        // Always update ready button text (whether in lobby or not)
        UpdateReadyButton();
        
        // Update lobby-specific content if in lobby
        if (MultiplayerManager.Instance != null && MultiplayerManager.Instance.IsInLobby())
        {
            UpdateLobbyInfo();
            UpdatePlayersList();
        }
        
        // Refresh all UI state
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
        
        // Setup character selection buttons
        if (previousCharacterButton != null)
            previousCharacterButton.onClick.AddListener(OnPreviousCharacterClicked);
        
        if (nextCharacterButton != null)
            nextCharacterButton.onClick.AddListener(OnNextCharacterClicked);
        
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
            gameTitleText.text = LocalizationManager.Instance.GetLocalizedText("game_title");
        
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
        
        // Note: SimpleSceneTransition uses NetworkSceneManager events, no manual subscription needed
        
        
        InitializeReadySystem();
        // PlayerSessionData will be initialized when lobby is created/joined
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
        LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
        
        if (MultiplayerManager.Instance != null)
        {
            MultiplayerManager.Instance.OnLobbyCodeGenerated -= OnLobbyCodeGenerated;
            MultiplayerManager.Instance.OnLobbyJoined -= OnLobbyJoined;
            MultiplayerManager.Instance.OnLobbyLeft -= OnLobbyLeft;
            MultiplayerManager.Instance.OnLobbyError -= OnLobbyError;
            MultiplayerManager.Instance.OnLobbyPlayersUpdated -= OnLobbyPlayersUpdated;
        }
        
        // Note: SimpleSceneTransition doesn't require manual event unsubscription
        
        if (readySystem != null)
        {
            readySystem.OnPlayerReadyChanged -= OnPlayerReadyChanged;
            readySystem.OnAllPlayersReadyChanged -= OnAllPlayersReadyChanged;
        }
        
        // Unsubscribe from PlayerSessionData events
        if (playerSessionData != null)
        {
            playerSessionData.OnPlayerSessionUpdated -= OnPlayerSessionUpdated;
            playerSessionData.OnPlayerCharacterChanged -= OnPlayerCharacterChanged;
            playerSessionData.OnPlayerReadyChanged -= OnPlayerReadyChangedFromSession;
        }
    }
    
    private async void OnCreateLobbyClicked()
    {
        if (MultiplayerManager.Instance == null)
        {
            statusManager.ShowMultiplayerManagerNotFound();
            return;
        }
        
        SetButtonsInteractable(false);
        ShowLoading(true);
        statusManager.ShowCreatingLobby();
        
        string lobbyCode = await MultiplayerManager.Instance.CreateLobby();
        
        if (string.IsNullOrEmpty(lobbyCode))
        {
            SetButtonsInteractable(true);
            ShowLoading(false);
            statusManager.ShowFailedToCreateLobby();
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
            statusManager.ShowMultiplayerManagerNotFound();
            return;
        }
        
        if (lobbyCodeInputField == null || string.IsNullOrEmpty(lobbyCodeInputField.text))
        {
            statusManager.ShowEnterLobbyCode();
            return;
        }
        
        string inputCode = lobbyCodeInputField.text.ToUpper().Trim();
        
        SetButtonsInteractable(false);
        ShowLoading(true);
        statusManager.ShowJoiningLobby(inputCode);
        
        if (joinLobbyPanel != null)
            joinLobbyPanel.SetActive(false);
        
        bool success = await MultiplayerManager.Instance.JoinLobbyByCode(inputCode);
        
        if (!success)
        {
            SetButtonsInteractable(true);
            ShowLoading(false);
            statusManager.ShowFailedToJoinLobby();
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
        statusManager.ShowLobbyCode(lobbyCode);
        
        // Delay initialization to ensure NetworkManager is fully ready
        Invoke(nameof(DelayedInitializeNetworkComponents), 0.5f);
        
        UpdateUIState();
    }
    
    private void OnLobbyJoined(string lobbyCode)
    {
        statusManager.ShowLobbyCode(lobbyCode);
        
        // Delay initialization to ensure NetworkManager is fully ready
        Invoke(nameof(DelayedInitializeNetworkComponents), 0.5f);
        
        UpdateUIState();
    }
    
    private void OnLobbyLeft()
    {
        statusManager.ShowLeftLobby();
        isReady = false;
        SetButtonsInteractable(true);
        ShowLoading(false);
        
        // Hide join lobby panel if it's open
        if (joinLobbyPanel != null)
            joinLobbyPanel.SetActive(false);
            
        UpdateUIState();
    }
    
    private void OnLobbyPlayersUpdated(List<string> playerNames)
    {
        UpdatePlayersList(playerNames);
        UpdateStartGameButton();
    }
    
    private void OnLobbyError(string errorMessage)
    {
        statusManager.ShowError(errorMessage);
        SetButtonsInteractable(true);
        ShowLoading(false);
    }
    
    // Scene transition events are now handled automatically by SimpleSceneTransition
    
    private void OnPlayerReadyChanged(string playerId, bool ready)
    {
        Debug.Log($"OnPlayerReadyChanged: {playerId} = {ready}");
        
        // Update local ready state if it's our player
        string localPlayerId = Unity.Services.Authentication.AuthenticationService.Instance.PlayerId;
        if (playerId == localPlayerId)
        {
            Debug.Log($"Updating local ready state from {isReady} to {ready}");
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
            statusManager.ShowEveryoneReady();
        }
    }
    
    private void SetButtonsInteractable(bool interactable)
    {
        Debug.Log($"Setting buttons interactable: {interactable}");
        
        if (createLobbyButton != null)
        {
            createLobbyButton.interactable = interactable;
            Debug.Log($"Create button interactable: {createLobbyButton.interactable}");
        }
        
        if (joinLobbyButton != null)
        {
            joinLobbyButton.interactable = interactable;
            Debug.Log($"Join button interactable: {joinLobbyButton.interactable}");
        }
        
        if (quitGameButton != null)
        {
            quitGameButton.interactable = interactable;
            Debug.Log($"Quit button interactable: {quitGameButton.interactable}");
        }
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
            UpdateCharacterSelection();
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
            if (string.IsNullOrEmpty(lobbyCode))
            {
                lobbyCodeDisplayText.text = LocalizationManager.Instance.GetLocalizedText("error_no_code");
            }
            else
            {
                lobbyCodeDisplayText.text = string.Format("{0}: {1}", LocalizationManager.Instance.GetLocalizedText("lobby_code_label"), lobbyCode);
            }
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
                playersListText.text = LocalizationManager.Instance.GetLocalizedText("error_no_players");
            }
            else
            {
                string playersLabel = string.Format("{0}: ({1}/4)", LocalizationManager.Instance.GetLocalizedText("players_list"), playerNames.Count);
                playersListText.text = playersLabel + "\n" + string.Join("\n", playerNames);
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
            
            // Show current status (what the player IS, not what clicking will do)
            readyButtonText.text = isReady ? LocalizationManager.Instance.GetLocalizedText("menu_ready") : LocalizationManager.Instance.GetLocalizedText("menu_not_ready");
            
            var colors = readyButton.colors;
            colors.normalColor = isReady ? Color.green : Color.red;
            readyButton.colors = colors;
            
            Debug.Log($"Ready button updated: isReady={isReady}, text='{readyButtonText.text}'");
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
            statusManager.ShowLobbyCodeCopied();
        }
    }
    
    private void OnReadyButtonClicked()
    {
        bool newReadyState = !isReady;
        Debug.Log($"Ready button clicked: changing from {isReady} to {newReadyState}");
        
        // Update both systems
        if (readySystem != null)
        {
            readySystem.SetPlayerReady(newReadyState);
        }
        
        if (playerSessionData != null)
        {
            playerSessionData.SetPlayerReady(newReadyState);
        }
        
        // Update local state immediately for responsiveness
        isReady = newReadyState;
        UpdateReadyButton();
        
        // Note: OnPlayerReadyChanged event will confirm the state change from server
    }
    
    private void OnStartGameClicked()
    {
        if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsHost)
        {
            if (readySystem != null && readySystem.AreAllPlayersReady())
            {
                statusManager.ShowStarting();
                
                if (SimpleSceneTransition.Instance != null)
                {
                    SimpleSceneTransition.Instance.StartGameTransition();
                }
                else
                {
                    Debug.LogError("SimpleSceneTransition.Instance not found!");
                }
            }
            else
            {
                statusManager.ShowPlayersNotReady();
            }
        }
        else
        {
            statusManager.ShowOnlyHostCanStart();
        }
    }
    
    private async void OnLeaveLobbyClicked()
    {
        if (MultiplayerManager.Instance != null)
        {
            statusManager.ShowLeavingLobby();
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
    
    private void InitializePlayerSessionData()
    {
        // Only initialize when we're in a lobby (NetworkManager is ready)
        if (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            return; // Don't create network components until NetworkManager is ready
        }
        
        try
        {
            // Get or create player session data
            playerSessionData = FindFirstObjectByType<PlayerSessionData>();
            if (playerSessionData == null)
            {
                GameObject sessionDataGO = new GameObject("PlayerSessionData");
                playerSessionData = sessionDataGO.AddComponent<PlayerSessionData>();
                Debug.Log("Created new PlayerSessionData component");
            }
            
            // Subscribe to PlayerSessionData events
            if (playerSessionData != null)
            {
                playerSessionData.OnPlayerSessionUpdated += OnPlayerSessionUpdated;
                playerSessionData.OnPlayerCharacterChanged += OnPlayerCharacterChanged;
                playerSessionData.OnPlayerReadyChanged += OnPlayerReadyChangedFromSession;
                Debug.Log("Subscribed to PlayerSessionData events");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to initialize PlayerSessionData: {e.Message}");
        }
    }
    
    private void DelayedInitializeNetworkComponents()
    {
        Debug.Log("Attempting delayed network component initialization...");
        
        // Initialize character selection first (doesn't need network)
        InitializeCharacterSelection();
        
        // Then initialize network components
        InitializePlayerSessionData();
        
        // Update UI to reflect new state
        UpdateCharacterSelection();
    }
    
    private void InitializeCharacterSelection()
    {
        Debug.Log("Initializing character selection...");
        
        // Get all available characters from the registry
        if (CharacterRegistry.Instance != null)
        {
            availableCharacters = CharacterRegistry.Instance.GetAllCharacters().ToList();
            if (availableCharacters.Count > 0)
            {
                currentCharacterIndex = 0;
                currentSelectedCharacter = availableCharacters[currentCharacterIndex];
                
                Debug.Log($"Initialized character selection with {availableCharacters.Count} characters. Current: {currentSelectedCharacter.characterName}");
                
                // Update the UI immediately
                if (currentCharacterNameText != null)
                {
                    currentCharacterNameText.text = currentSelectedCharacter.characterName;
                }
                
                // Set initial character in session data if available
                if (playerSessionData != null && Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
                {
                    playerSessionData.SetPlayerCharacter(currentSelectedCharacter.characterID);
                }
            }
            else
            {
                Debug.LogWarning("No characters found in CharacterRegistry!");
            }
        }
        else
        {
            Debug.LogWarning("CharacterRegistry not found! Character selection will not work.");
        }
    }
    
    private void UpdateCharacterSelection()
    {
        if (currentSelectedCharacter != null && currentCharacterNameText != null)
        {
            currentCharacterNameText.text = currentSelectedCharacter.characterName;
        }
        
        // Update button states - only enable if we have characters and we're in a lobby
        bool inLobby = MultiplayerManager.Instance != null && MultiplayerManager.Instance.IsInLobby();
        bool hasMultipleCharacters = availableCharacters != null && availableCharacters.Count > 1;
        bool shouldEnableButtons = inLobby && hasMultipleCharacters;
        
        Debug.Log($"UpdateCharacterSelection: inLobby={inLobby}, hasMultipleCharacters={hasMultipleCharacters}, shouldEnableButtons={shouldEnableButtons}");
        
        if (previousCharacterButton != null)
        {
            previousCharacterButton.interactable = shouldEnableButtons;
            Debug.Log($"Previous character button interactable: {previousCharacterButton.interactable}");
        }
        
        if (nextCharacterButton != null)
        {
            nextCharacterButton.interactable = shouldEnableButtons;
            Debug.Log($"Next character button interactable: {nextCharacterButton.interactable}");
        }
    }
    
    private void OnPreviousCharacterClicked()
    {
        if (availableCharacters == null || availableCharacters.Count <= 1) return;
        
        currentCharacterIndex = (currentCharacterIndex - 1 + availableCharacters.Count) % availableCharacters.Count;
        currentSelectedCharacter = availableCharacters[currentCharacterIndex];
        
        Debug.Log($"🔄 MAINMENU: Previous character clicked - {currentSelectedCharacter.characterName} (ID: {currentSelectedCharacter.characterID})");
        Debug.Log($"🔄 MAINMENU: Current character index: {currentCharacterIndex}/{availableCharacters.Count}");
        
        // Update session data
        if (playerSessionData != null)
        {
            Debug.Log($"🔄 MAINMENU: PlayerSessionData found - calling SetPlayerCharacter({currentSelectedCharacter.characterID})");
            
            // Check if we're in a lobby and networked
            if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
            {
                Debug.Log($"🔄 MAINMENU: Network is active - setting character via PlayerSessionData");
                playerSessionData.SetPlayerCharacter(currentSelectedCharacter.characterID);
            }
            else
            {
                Debug.LogWarning($"🔄 MAINMENU: Network not active - cannot sync character selection");
            }
        }
        else
        {
            Debug.LogError("🔄 MAINMENU: playerSessionData is NULL! Character preview will not update.");
            Debug.LogError($"🔄 MAINMENU: NetworkManager status: {(Unity.Netcode.NetworkManager.Singleton != null ? "Active" : "NULL")}");
        }
        
        // Update local UI immediately
        UpdateCharacterSelection();
        
        // FALLBACK: If preview doesn't update via events, try direct update
        Invoke(nameof(FallbackUpdatePreview), 0.2f);
        
        Debug.Log($"🔄 MAINMENU: Character selection complete for {currentSelectedCharacter.characterName}");
    }
    
    private void OnNextCharacterClicked()
    {
        if (availableCharacters == null || availableCharacters.Count <= 1) return;
        
        currentCharacterIndex = (currentCharacterIndex + 1) % availableCharacters.Count;
        currentSelectedCharacter = availableCharacters[currentCharacterIndex];
        
        Debug.Log($"🔄 MAINMENU: Next character clicked - {currentSelectedCharacter.characterName} (ID: {currentSelectedCharacter.characterID})");
        Debug.Log($"🔄 MAINMENU: Current character index: {currentCharacterIndex}/{availableCharacters.Count}");
        
        // Update session data
        if (playerSessionData != null)
        {
            Debug.Log($"🔄 MAINMENU: PlayerSessionData found - calling SetPlayerCharacter({currentSelectedCharacter.characterID})");
            
            // Check if we're in a lobby and networked
            if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
            {
                Debug.Log($"🔄 MAINMENU: Network is active - setting character via PlayerSessionData");
                playerSessionData.SetPlayerCharacter(currentSelectedCharacter.characterID);
            }
            else
            {
                Debug.LogWarning($"🔄 MAINMENU: Network not active - cannot sync character selection");
            }
        }
        else
        {
            Debug.LogError("🔄 MAINMENU: playerSessionData is NULL! Character preview will not update.");
            Debug.LogError($"🔄 MAINMENU: NetworkManager status: {(Unity.Netcode.NetworkManager.Singleton != null ? "Active" : "NULL")}");
        }
        
        // Update local UI immediately
        UpdateCharacterSelection();
        
        // FALLBACK: If preview doesn't update via events, try direct update
        Invoke(nameof(FallbackUpdatePreview), 0.2f);
        
        Debug.Log($"🔄 MAINMENU: Character selection complete for {currentSelectedCharacter.characterName}");
    }
    
    private void OnPlayerSessionUpdated(PlayerSessionInfo sessionInfo)
    {
        // Update UI when any player session changes
        UpdatePlayersList();
        UpdateStartGameButton();
    }
    
    private void OnPlayerCharacterChanged(string playerGuid, int characterId)
    {
        Debug.Log($"Player {playerGuid} changed character to {characterId}");
        // Character previews will be updated automatically by LobbyCharacterPreviewPoint
    }
    
    private void OnPlayerReadyChangedFromSession(string playerGuid, bool isReady)
    {
        Debug.Log($"Player {playerGuid} ready state changed to {isReady}");
        UpdateStartGameButton();
    }
    
    public CharacterData GetCurrentSelectedCharacter()
    {
        return currentSelectedCharacter;
    }
    
    private void FallbackUpdatePreview()
    {
        Debug.Log($"🔄 MAINMENU: FallbackUpdatePreview called");
        
        if (currentSelectedCharacter == null)
        {
            Debug.LogWarning($"🔄 MAINMENU: No current selected character for fallback");
            return;
        }
        
        // Try to find and directly update the local preview point
        var previewPoints = FindObjectsByType<LobbyCharacterPreviewPoint>(FindObjectsSortMode.None);
        Debug.Log($"🔄 MAINMENU: Found {previewPoints.Length} preview points");
        
        foreach (var previewPoint in previewPoints)
        {
            // Look for the local player preview point
            if (previewPoint.name.Contains("Local") || previewPoint.transform.GetSiblingIndex() == 0)
            {
                Debug.Log($"🔄 MAINMENU: Found potential local preview point: {previewPoint.name}");
                
                // First try to get or create the character loader through the preview point
                Debug.Log($"🔄 MAINMENU: Getting or creating character loader through LobbyCharacterPreviewPoint");
                var previewLoader = previewPoint.GetOrCreateCharacterLoader();
                if (previewLoader != null)
                {
                    Debug.Log($"🔄 MAINMENU: Found existing UltraSimpleMeshSwapper, loading character");
                    previewLoader.LoadCharacterByID(currentSelectedCharacter.characterID);
                    previewLoader.SetVisible(true);
                    Debug.Log($"🔄 MAINMENU: Fallback character loading completed");
                    return;
                }
                else
                {
                    Debug.LogWarning($"🔄 MAINMENU: No UltraSimpleMeshSwapper found on {previewPoint.name}, checking for character preview objects...");
                    
                    // Try to find a character preview object that we can add the component to
                    var characterPreviewObjects = previewPoint.GetComponentsInChildren<Transform>();
                    foreach (var obj in characterPreviewObjects)
                    {
                        // Look for objects that might be character models (have SkinnedMeshRenderer)
                        var skinnedRenderer = obj.GetComponent<SkinnedMeshRenderer>();
                        if (skinnedRenderer != null)
                        {
                            Debug.Log($"🔄 MAINMENU: Found character object {obj.name} with SkinnedMeshRenderer, adding UltraSimpleMeshSwapper");
                            previewLoader = obj.gameObject.AddComponent<UltraSimpleMeshSwapper>();
                            previewLoader.SetPreviewMode(true);
                            
                            // Wait a frame for initialization then load character
                            StartCoroutine(DelayedFallbackLoad(previewLoader, currentSelectedCharacter.characterID));
                            return;
                        }
                    }
                    
                    Debug.LogWarning($"🔄 MAINMENU: No suitable character preview object found on {previewPoint.name}");
                }
            }
        }
        
        Debug.LogWarning($"🔄 MAINMENU: No suitable preview point found for fallback update");
    }
    
    /// <summary>
    /// Delayed loading for fallback preview when component is created dynamically
    /// </summary>
    private System.Collections.IEnumerator DelayedFallbackLoad(UltraSimpleMeshSwapper loader, int characterID)
    {
        // Wait a frame for the component to initialize
        yield return null;
        
        if (loader != null)
        {
            Debug.Log($"🔄 MAINMENU: Loading character {characterID} after delayed initialization");
            bool success = loader.LoadCharacterByID(characterID);
            if (success)
            {
                loader.SetVisible(true);
                Debug.Log($"🔄 MAINMENU: Delayed fallback character loading completed successfully");
            }
            else
            {
                Debug.LogWarning($"🔄 MAINMENU: Delayed fallback character loading failed");
            }
        }
        else
        {
            Debug.LogError($"🔄 MAINMENU: Loader became null during delayed loading");
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
        
        // Clean up character selection button listeners
        if (previousCharacterButton != null)
            previousCharacterButton.onClick.RemoveAllListeners();
        
        if (nextCharacterButton != null)
            nextCharacterButton.onClick.RemoveAllListeners();
    }
}