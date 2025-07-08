using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class StatusTextManager : MonoBehaviour
{
    [Header("Text Component")]
    [SerializeField] private TextMeshProUGUI statusText;
    
    [Header("Current Status")]
    [SerializeField] private string currentStatusKey;
    [SerializeField] private Dictionary<string, object> currentTokens = new Dictionary<string, object>();
    
    private SmartLocalizedText smartText;
    
    void Awake()
    {
        if (statusText == null)
            statusText = GetComponent<TextMeshProUGUI>();
            
        // Add SmartLocalizedText component if it doesn't exist
        smartText = GetComponent<SmartLocalizedText>();
        if (smartText == null)
        {
            smartText = gameObject.AddComponent<SmartLocalizedText>();
        }
    }
    
    void OnEnable()
    {
        LocalizationManager.OnLanguageChanged += OnLanguageChanged;
    }
    
    void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
    }
    
    private void OnLanguageChanged()
    {
        // Refresh current status with current tokens
        if (!string.IsNullOrEmpty(currentStatusKey))
        {
            UpdateStatusWithTokens(currentStatusKey, currentTokens);
        }
    }
    
    public void UpdateStatus(string statusKey)
    {
        currentStatusKey = statusKey;
        currentTokens.Clear();
        
        smartText.localizationKey = statusKey;
        smartText.UpdateText();
        
        Debug.Log($"Status updated to: {statusKey}");
    }
    
    public void UpdateStatusWithTokens(string statusKey, Dictionary<string, object> tokens)
    {
        currentStatusKey = statusKey;
        currentTokens = new Dictionary<string, object>(tokens);
        
        smartText.localizationKey = statusKey;
        smartText.SetTokens(tokens);
        
        Debug.Log($"Status updated to: {statusKey} with {tokens.Count} tokens");
    }
    
    // Convenience methods for common status updates
    public void ShowReadyToStart()
    {
        UpdateStatus("status_ready_to_start");
    }
    
    public void ShowCreatingLobby()
    {
        UpdateStatus("status_creating_lobby");
    }
    
    public void ShowFailedToCreateLobby()
    {
        UpdateStatus("status_failed_create_lobby");
    }
    
    public void ShowJoiningLobby(string lobbyCode)
    {
        var tokens = new Dictionary<string, object>
        {
            {"lobbyCode", lobbyCode}
        };
        UpdateStatusWithTokens("status_joining_lobby", tokens);
    }
    
    public void ShowFailedToJoinLobby()
    {
        UpdateStatus("status_failed_join_lobby");
    }
    
    public void ShowLobbyCode(string lobbyCode)
    {
        var tokens = new Dictionary<string, object>
        {
            {"lobbyCode", lobbyCode}
        };
        UpdateStatusWithTokens("status_lobby_code_format", tokens);
    }
    
    public void ShowLeftLobby()
    {
        UpdateStatus("status_left_lobby");
    }
    
    public void ShowLoadingScene(string sceneName)
    {
        var tokens = new Dictionary<string, object>
        {
            {"sceneName", sceneName}
        };
        UpdateStatusWithTokens("status_loading_scene", tokens);
    }
    
    public void ShowEveryoneReady()
    {
        UpdateStatus("status_everyone_ready");
    }
    
    public void ShowPlayersCount(int currentPlayers, int maxPlayers = 4)
    {
        var tokens = new Dictionary<string, object>
        {
            {"currentPlayers", currentPlayers},
            {"maxPlayers", maxPlayers}
        };
        UpdateStatusWithTokens("status_players_count", tokens);
    }
    
    public void ShowLobbyCodeCopied()
    {
        UpdateStatus("status_lobby_code_copied");
    }
    
    public void ShowStarting()
    {
        UpdateStatus("status_starting");
    }
    
    public void ShowPlayersNotReady()
    {
        UpdateStatus("status_players_not_ready");
    }
    
    public void ShowOnlyHostCanStart()
    {
        UpdateStatus("status_only_host_can_start");
    }
    
    public void ShowLeavingLobby()
    {
        UpdateStatus("status_leaving_lobby");
    }
    
    public void ShowError(string errorMessage)
    {
        var tokens = new Dictionary<string, object>
        {
            {"errorMessage", errorMessage}
        };
        UpdateStatusWithTokens("error_generic", tokens);
    }
    
    public void ShowFailedToLoadScene(string sceneName)
    {
        var tokens = new Dictionary<string, object>
        {
            {"sceneName", sceneName}
        };
        UpdateStatusWithTokens("error_failed_load_scene", tokens);
    }
    
    public void ShowMultiplayerManagerNotFound()
    {
        UpdateStatus("error_multiplayer_manager_not_found");
    }
    
    public void ShowEnterLobbyCode()
    {
        UpdateStatus("status_enter_lobby_code");
    }
}