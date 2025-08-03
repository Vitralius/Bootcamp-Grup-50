using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MultiplayerLobbyUI : MonoBehaviour
{
    [Header("Lobby Controls")]
    [SerializeField] private Button createLobbyButton;
    [SerializeField] private Button joinLobbyButton;
    [SerializeField] private Button leaveLobbyButton;
    [SerializeField] private Button copyLobbyCodeButton;
    
    [Header("Lobby Code UI")]
    [SerializeField] private TMP_Text lobbyCodeDisplayText;
    [SerializeField] private TMP_InputField lobbyCodeInputField;
    
    [Header("Status UI")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text playersListText;
    
    [Header("UI Panels")]
    [SerializeField] private GameObject lobbyCreationPanel;
    [SerializeField] private GameObject lobbyJoinPanel;
    [SerializeField] private GameObject inLobbyPanel;
    
    private void Start()
    {
        SetupUI();
        SubscribeToEvents();
        UpdateUIState();
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
        // Refresh UI while preserving dynamic content
        UpdateUIState();
        
        // Update lobby code and player list if in lobby
        if (MultiplayerManager.Instance != null && MultiplayerManager.Instance.IsInLobby())
        {
            string lobbyCode = MultiplayerManager.Instance.GetCurrentLobbyCode();
            UpdateLobbyCodeDisplay(lobbyCode);
            
            var playerNames = MultiplayerManager.Instance.GetLobbyPlayerNames();
            UpdatePlayersList(playerNames);
        }
    }
    
    private void SetupUI()
    {
        // Setup button listeners
        if (createLobbyButton != null)
            createLobbyButton.onClick.AddListener(OnCreateLobbyClicked);
        
        if (joinLobbyButton != null)
            joinLobbyButton.onClick.AddListener(OnJoinLobbyClicked);
        
        if (leaveLobbyButton != null)
            leaveLobbyButton.onClick.AddListener(OnLeaveLobbyClicked);
        
        if (copyLobbyCodeButton != null)
            copyLobbyCodeButton.onClick.AddListener(OnCopyLobbyCodeClicked);
        
        // Setup input field
        if (lobbyCodeInputField != null)
        {
            lobbyCodeInputField.characterLimit = 6;
            lobbyCodeInputField.contentType = TMP_InputField.ContentType.Alphanumeric;
        }
        
        // Initialize UI text
        UpdateStatusText(LocalizationManager.Instance.GetLocalizedText("status_ready_to_start"));
        UpdateLobbyCodeDisplay("");
        UpdatePlayersList(new List<string>());
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
    }
    
    private async void OnCreateLobbyClicked()
    {
        if (MultiplayerManager.Instance == null)
        {
            UpdateStatusText(LocalizationManager.Instance.GetLocalizedText("error_multiplayer_manager_not_found"));
            return;
        }
        
        SetButtonsInteractable(false);
        UpdateStatusText(LocalizationManager.Instance.GetLocalizedText("status_creating_lobby"));
        
        string lobbyCode = await MultiplayerManager.Instance.CreateLobby();
        
        if (string.IsNullOrEmpty(lobbyCode))
        {
            SetButtonsInteractable(true);
            UpdateStatusText(LocalizationManager.Instance.GetLocalizedText("status_failed_create_lobby"));
        }
    }
    
    private async void OnJoinLobbyClicked()
    {
        if (MultiplayerManager.Instance == null)
        {
            UpdateStatusText(LocalizationManager.Instance.GetLocalizedText("error_multiplayer_manager_not_found"));
            return;
        }
        
        if (lobbyCodeInputField == null || string.IsNullOrEmpty(lobbyCodeInputField.text))
        {
            UpdateStatusText(LocalizationManager.Instance.GetLocalizedText("status_enter_lobby_code"));
            return;
        }
        
        string inputCode = lobbyCodeInputField.text.ToUpper().Trim();
        
        SetButtonsInteractable(false);
        UpdateStatusText(string.Format(LocalizationManager.Instance.GetLocalizedText("status_joining_lobby"), inputCode));
        
        bool success = await MultiplayerManager.Instance.JoinLobbyByCode(inputCode);
        
        if (!success)
        {
            SetButtonsInteractable(true);
            UpdateStatusText(LocalizationManager.Instance.GetLocalizedText("status_failed_join_lobby"));
        }
    }
    
    private async void OnLeaveLobbyClicked()
    {
        if (MultiplayerManager.Instance == null)
        {
            UpdateStatusText(LocalizationManager.Instance.GetLocalizedText("error_multiplayer_manager_not_found"));
            return;
        }
        
        SetButtonsInteractable(false);
        UpdateStatusText(LocalizationManager.Instance.GetLocalizedText("status_leaving_lobby"));
        
        await MultiplayerManager.Instance.LeaveLobby();
    }
    
    private void OnCopyLobbyCodeClicked()
    {
        if (MultiplayerManager.Instance == null)
        {
            UpdateStatusText(LocalizationManager.Instance.GetLocalizedText("error_multiplayer_manager_not_found"));
            return;
        }
        
        MultiplayerManager.Instance.CopyLobbyCodeToClipboard();
        UpdateStatusText(LocalizationManager.Instance.GetLocalizedText("status_lobby_code_copied"));
    }
    
    private void OnLobbyCodeGenerated(string lobbyCode)
    {
        UpdateLobbyCodeDisplay(lobbyCode);
        UpdateStatusText(string.Format(LocalizationManager.Instance.GetLocalizedText("status_lobby_code_format"), lobbyCode));
        UpdateUIState();
    }
    
    private void OnLobbyJoined(string lobbyCode)
    {
        UpdateLobbyCodeDisplay(lobbyCode);
        UpdateStatusText(string.Format(LocalizationManager.Instance.GetLocalizedText("status_lobby_code_format"), lobbyCode));
        UpdateUIState();
    }
    
    private void OnLobbyLeft()
    {
        UpdateLobbyCodeDisplay("");
        UpdateStatusText(LocalizationManager.Instance.GetLocalizedText("status_left_lobby"));
        UpdatePlayersList(new List<string>());
        UpdateUIState();
    }
    
    private void OnLobbyError(string errorMessage)
    {
        UpdateStatusText(string.Format(LocalizationManager.Instance.GetLocalizedText("error_generic"), errorMessage));
        SetButtonsInteractable(true);
    }
    
    private void OnLobbyPlayersUpdated(List<string> playerNames)
    {
        UpdatePlayersList(playerNames);
    }
    
    private void UpdateUIState()
    {
        bool inLobby = MultiplayerManager.Instance != null && MultiplayerManager.Instance.IsInLobby();
        
        // Update button states
        SetButtonsInteractable(true);
        
        if (createLobbyButton != null)
            createLobbyButton.interactable = !inLobby;
        
        if (joinLobbyButton != null)
            joinLobbyButton.interactable = !inLobby;
        
        if (leaveLobbyButton != null)
            leaveLobbyButton.interactable = inLobby;
        
        if (copyLobbyCodeButton != null)
            copyLobbyCodeButton.interactable = inLobby;
        
        if (lobbyCodeInputField != null)
            lobbyCodeInputField.interactable = !inLobby;
        
        // Update panel visibility
        if (lobbyCreationPanel != null)
            lobbyCreationPanel.SetActive(!inLobby);
        
        if (lobbyJoinPanel != null)
            lobbyJoinPanel.SetActive(!inLobby);
        
        if (inLobbyPanel != null)
            inLobbyPanel.SetActive(inLobby);
    }
    
    private void SetButtonsInteractable(bool interactable)
    {
        if (createLobbyButton != null)
            createLobbyButton.interactable = interactable;
        
        if (joinLobbyButton != null)
            joinLobbyButton.interactable = interactable;
        
        if (leaveLobbyButton != null)
            leaveLobbyButton.interactable = interactable && MultiplayerManager.Instance != null && MultiplayerManager.Instance.IsInLobby();
        
        if (copyLobbyCodeButton != null)
            copyLobbyCodeButton.interactable = interactable && MultiplayerManager.Instance != null && MultiplayerManager.Instance.IsInLobby();
    }
    
    private void UpdateStatusText(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
            Debug.Log($"Lobby Status: {message}");
        }
    }
    
    private void UpdateLobbyCodeDisplay(string lobbyCode)
    {
        if (lobbyCodeDisplayText != null)
        {
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
    
    private void UpdatePlayersList(List<string> playerNames)
    {
        if (playersListText != null)
        {
            if (playerNames == null || playerNames.Count == 0)
            {
                playersListText.text = LocalizationManager.Instance.GetLocalizedText("error_no_players");
            }
            else
            {
                string playersLabel = string.Format("{0} ({1}):", LocalizationManager.Instance.GetLocalizedText("players_list"), playerNames.Count);
                playersListText.text = playersLabel + "\n" + string.Join("\n", playerNames);
            }
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
        
        if (leaveLobbyButton != null)
            leaveLobbyButton.onClick.RemoveAllListeners();
        
        if (copyLobbyCodeButton != null)
            copyLobbyCodeButton.onClick.RemoveAllListeners();
    }
}