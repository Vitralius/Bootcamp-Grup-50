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
        UpdateStatusText("Ready to create or join lobby");
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
            UpdateStatusText("MultiplayerManager not found!");
            return;
        }
        
        SetButtonsInteractable(false);
        UpdateStatusText("Creating lobby...");
        
        string lobbyCode = await MultiplayerManager.Instance.CreateLobby();
        
        if (string.IsNullOrEmpty(lobbyCode))
        {
            SetButtonsInteractable(true);
            UpdateStatusText("Failed to create lobby");
        }
    }
    
    private async void OnJoinLobbyClicked()
    {
        if (MultiplayerManager.Instance == null)
        {
            UpdateStatusText("MultiplayerManager not found!");
            return;
        }
        
        if (lobbyCodeInputField == null || string.IsNullOrEmpty(lobbyCodeInputField.text))
        {
            UpdateStatusText("Please enter a lobby code");
            return;
        }
        
        string inputCode = lobbyCodeInputField.text.ToUpper().Trim();
        
        SetButtonsInteractable(false);
        UpdateStatusText($"Joining lobby {inputCode}...");
        
        bool success = await MultiplayerManager.Instance.JoinLobbyByCode(inputCode);
        
        if (!success)
        {
            SetButtonsInteractable(true);
            UpdateStatusText("Failed to join lobby");
        }
    }
    
    private async void OnLeaveLobbyClicked()
    {
        if (MultiplayerManager.Instance == null)
        {
            UpdateStatusText("MultiplayerManager not found!");
            return;
        }
        
        SetButtonsInteractable(false);
        UpdateStatusText("Leaving lobby...");
        
        await MultiplayerManager.Instance.LeaveLobby();
    }
    
    private void OnCopyLobbyCodeClicked()
    {
        if (MultiplayerManager.Instance == null)
        {
            UpdateStatusText("MultiplayerManager not found!");
            return;
        }
        
        MultiplayerManager.Instance.CopyLobbyCodeToClipboard();
        UpdateStatusText("Lobby code copied to clipboard!");
    }
    
    private void OnLobbyCodeGenerated(string lobbyCode)
    {
        UpdateLobbyCodeDisplay(lobbyCode);
        UpdateStatusText($"Lobby created! Code: {lobbyCode}");
        UpdateUIState();
    }
    
    private void OnLobbyJoined(string lobbyCode)
    {
        UpdateLobbyCodeDisplay(lobbyCode);
        UpdateStatusText($"Joined lobby: {lobbyCode}");
        UpdateUIState();
    }
    
    private void OnLobbyLeft()
    {
        UpdateLobbyCodeDisplay("");
        UpdateStatusText("Left lobby");
        UpdatePlayersList(new List<string>());
        UpdateUIState();
    }
    
    private void OnLobbyError(string errorMessage)
    {
        UpdateStatusText($"Error: {errorMessage}");
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
                lobbyCodeDisplayText.text = "No lobby code";
            }
            else
            {
                lobbyCodeDisplayText.text = $"Lobby Code: {lobbyCode}";
            }
        }
    }
    
    private void UpdatePlayersList(List<string> playerNames)
    {
        if (playersListText != null)
        {
            if (playerNames == null || playerNames.Count == 0)
            {
                playersListText.text = "No players";
            }
            else
            {
                playersListText.text = $"Players ({playerNames.Count}):\n" + string.Join("\n", playerNames);
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