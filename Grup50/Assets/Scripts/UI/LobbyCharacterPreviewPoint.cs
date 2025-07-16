using UnityEngine;
using Unity.Netcode;
using Unity.Services.Authentication;

public class LobbyCharacterPreviewPoint : NetworkBehaviour
{
    [Header("Preview Settings")]
    [SerializeField] private Transform characterSpawnPoint;
    [SerializeField] private GameObject baseCharacterPrefab;
    [SerializeField] private bool isLocalPlayerPreview = false;
    [SerializeField] private int previewPointIndex = 0;
    
    [Header("UI Elements")]
    [SerializeField] private GameObject playerNameplate;
    [SerializeField] private TMPro.TMP_Text playerNameText;
    [SerializeField] private GameObject readyIndicator;
    
    [Header("Ready Indicator Colors")]
    [SerializeField] private Color notReadyColor = Color.red;
    [SerializeField] private Color readyColor = Color.green;
    [SerializeField] private Renderer readyIndicatorRenderer;
    
    private GameObject currentCharacterPreview;
    private PreviewCharacterLoader previewCharacterLoader;
    private PlayerSessionData playerSessionData;
    private string assignedPlayerGuid;
    private PlayerSessionInfo? currentPlayerInfo;
    private bool isInitialized = false;
    
    private void Start()
    {
        InitializePreviewPoint();
    }
    
    private void InitializePreviewPoint()
    {
        // Don't initialize until we're in a lobby
        if (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            // Try again in 1 second
            Invoke(nameof(InitializePreviewPoint), 1f);
            return;
        }
        
        // Get PlayerSessionData
        playerSessionData = FindFirstObjectByType<PlayerSessionData>();
        if (playerSessionData == null)
        {
            Debug.LogWarning("PlayerSessionData not found! Retrying in 1 second...");
            Invoke(nameof(InitializePreviewPoint), 1f);
            return;
        }
        
        // Subscribe to session data events
        playerSessionData.OnPlayerSessionUpdated += OnPlayerSessionUpdated;
        playerSessionData.OnPlayerCharacterChanged += OnPlayerCharacterChanged;
        playerSessionData.OnPlayerReadyChanged += OnPlayerReadyChanged;
        playerSessionData.OnPlayerJoined += OnPlayerJoined;
        
        // Auto-find ready indicator renderer if not assigned
        if (readyIndicatorRenderer == null && readyIndicator != null)
        {
            readyIndicatorRenderer = readyIndicator.GetComponent<Renderer>();
            if (readyIndicatorRenderer == null)
                readyIndicatorRenderer = readyIndicator.GetComponentInChildren<Renderer>();
        }
        
        // Initialize preview character
        CreatePreviewCharacter();
        
        // Start hidden until a player is assigned
        SetPreviewVisible(false);
        
        isInitialized = true;
        
        Debug.Log($"Preview point {previewPointIndex} initialized. isLocalPlayerPreview: {isLocalPlayerPreview}");
        
        // Only assign players if this is a local player preview
        if (isLocalPlayerPreview)
        {
            // Local player preview should show immediately
            Debug.Log($"Preview point {previewPointIndex}: Assigning local player immediately");
            AssignPlayerToPreviewPoint();
        }
        else
        {
            // Other player previews should wait for actual players to join
            Debug.Log($"Preview point {previewPointIndex}: Waiting for other players to join...");
            // Add a small delay to ensure the PlayerSessionData is fully initialized
            Invoke(nameof(CheckForOtherPlayers), 1f);
        }
    }
    
    private void CreatePreviewCharacter()
    {
        if (baseCharacterPrefab == null)
        {
            Debug.LogError("Base character prefab not assigned!");
            return;
        }
        
        // Destroy existing preview if any
        if (currentCharacterPreview != null)
        {
            DestroyImmediate(currentCharacterPreview);
        }
        
        // Instantiate base character at spawn point
        Transform spawnTransform = characterSpawnPoint != null ? characterSpawnPoint : transform;
        currentCharacterPreview = Instantiate(baseCharacterPrefab, spawnTransform.position, spawnTransform.rotation, transform);
        
        // Remove any network components from preview (it's just visual)
        var networkComponents = currentCharacterPreview.GetComponentsInChildren<NetworkBehaviour>();
        foreach (var netComp in networkComponents)
        {
            DestroyImmediate(netComp);
        }
        
        var networkObject = currentCharacterPreview.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            DestroyImmediate(networkObject);
        }
        
        // Disable movement components (preview should be static)
        var controller = currentCharacterPreview.GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.enabled = false;
        }
        
        // Remove any movement scripts to keep it simple
        var movementScripts = currentCharacterPreview.GetComponents<MonoBehaviour>();
        foreach (var script in movementScripts)
        {
            if (script.GetType().Name.Contains("Controller") || script.GetType().Name.Contains("Movement"))
            {
                script.enabled = false;
            }
        }
        
        // Get or add PreviewCharacterLoader
        previewCharacterLoader = currentCharacterPreview.GetComponent<PreviewCharacterLoader>();
        if (previewCharacterLoader == null)
        {
            previewCharacterLoader = currentCharacterPreview.AddComponent<PreviewCharacterLoader>();
        }
        
        Debug.Log($"Created character preview at point {previewPointIndex}");
    }
    
    private void CheckForOtherPlayers()
    {
        if (!isLocalPlayerPreview && playerSessionData != null)
        {
            var otherPlayerCount = GetOtherPlayerCount();
            Debug.Log($"[PREVIEW-{previewPointIndex}] Initial check: {otherPlayerCount} other players found");
            
            // Only assign if there are actually other players AND this preview point has a player to show
            if (otherPlayerCount > 0 && previewPointIndex < otherPlayerCount)
            {
                Debug.Log($"[PREVIEW-{previewPointIndex}] Attempting initial assignment...");
                AssignPlayerToPreviewPoint();
            }
            else
            {
                Debug.Log($"[PREVIEW-{previewPointIndex}] Staying hidden - not enough other players for this index");
            }
        }
    }
    
    private int GetOtherPlayerCount()
    {
        if (playerSessionData == null) return 0;
        
        var allSessions = playerSessionData.GetConnectedPlayerSessions();
        var localSession = playerSessionData.GetCurrentPlayerSession();
        
        if (!localSession.HasValue)
        {
            Debug.Log($"[PREVIEW-{previewPointIndex}] No local session found for other player count");
            return 0;
        }
        
        string localPlayerId = localSession.Value.playerId.ToString();
        int otherPlayerCount = 0;
        
        foreach (var session in allSessions)
        {
            string sessionPlayerId = session.playerId.ToString();
            // Skip local player
            if (sessionPlayerId != localPlayerId)
            {
                otherPlayerCount++;
            }
        }
        
        Debug.Log($"[PREVIEW-{previewPointIndex}] GetOtherPlayerCount: {otherPlayerCount} (Total: {allSessions.Count}, Local: {localPlayerId})");
        return otherPlayerCount;
    }
    
    private void AssignPlayerToPreviewPoint()
    {
        if (playerSessionData == null || !isInitialized) return;
        
        // Clear previous assignment
        assignedPlayerGuid = null;
        SetPreviewVisible(false);
        
        Debug.Log($"[PREVIEW-{previewPointIndex}] Assigning player. isLocalPlayerPreview: {isLocalPlayerPreview}");
        
        if (isLocalPlayerPreview)
        {
            // This preview point shows the local player ONLY
            var currentSession = playerSessionData.GetCurrentPlayerSession();
            if (currentSession.HasValue)
            {
                assignedPlayerGuid = currentSession.Value.playerId.ToString();
                UpdatePreviewForPlayer(currentSession.Value);
                SetPreviewVisible(true);
                Debug.Log($"[PREVIEW-{previewPointIndex}] ✓ Assigned LOCAL player {assignedPlayerGuid}");
            }
            else
            {
                Debug.Log($"[PREVIEW-{previewPointIndex}] ✗ No local player session found");
            }
        }
        else
        {
            // This preview point shows OTHER players only - strict filtering
            var allSessions = playerSessionData.GetConnectedPlayerSessions();
            var localSession = playerSessionData.GetCurrentPlayerSession();
            
            Debug.Log($"[PREVIEW-{previewPointIndex}] Checking other players. Total sessions: {allSessions.Count}");
            
            // Create list of OTHER players only (excluding local player)
            var otherPlayerSessions = new System.Collections.Generic.List<PlayerSessionInfo>();
            
            if (localSession.HasValue)
            {
                string localPlayerId = localSession.Value.playerId.ToString();
                
                foreach (var session in allSessions)
                {
                    string sessionPlayerId = session.playerId.ToString();
                    
                    if (sessionPlayerId != localPlayerId)
                    {
                        otherPlayerSessions.Add(session);
                        Debug.Log($"[PREVIEW-{previewPointIndex}] Found other player: {sessionPlayerId}");
                    }
                    else
                    {
                        Debug.Log($"[PREVIEW-{previewPointIndex}] Skipped local player: {sessionPlayerId}");
                    }
                }
            }
            
            Debug.Log($"[PREVIEW-{previewPointIndex}] Total OTHER players: {otherPlayerSessions.Count}");
            
            // CRITICAL: Only assign if we have enough other players for this specific preview point index
            if (otherPlayerSessions.Count > 0 && previewPointIndex < otherPlayerSessions.Count)
            {
                var targetSession = otherPlayerSessions[previewPointIndex];
                assignedPlayerGuid = targetSession.playerId.ToString();
                UpdatePreviewForPlayer(targetSession);
                SetPreviewVisible(true);
                Debug.Log($"[PREVIEW-{previewPointIndex}] ✓ Assigned OTHER player {assignedPlayerGuid}");
            }
            else
            {
                Debug.Log($"[PREVIEW-{previewPointIndex}] ✗ NOT ENOUGH other players. Need: {previewPointIndex + 1}, Have: {otherPlayerSessions.Count}");
                // Explicitly stay hidden - this is correct behavior
            }
        }
    }
    
    private void UpdatePreviewForPlayer(PlayerSessionInfo playerInfo)
    {
        if (previewCharacterLoader == null) return;
        
        currentPlayerInfo = playerInfo;
        
        // Update nameplate
        if (playerNameText != null)
        {
            playerNameText.text = playerInfo.playerName.ToString();
        }
        
        if (playerNameplate != null)
        {
            playerNameplate.SetActive(true);
        }
        
        // Update ready indicator
        if (readyIndicator != null)
        {
            readyIndicator.SetActive(true); // Always show the indicator
            
            // Change color based on ready state
            if (readyIndicatorRenderer != null)
            {
                Color targetColor = playerInfo.isReady ? readyColor : notReadyColor;
                readyIndicatorRenderer.material.color = targetColor;
                Debug.Log($"Preview point {previewPointIndex}: Updated ready indicator color to {targetColor} (isReady: {playerInfo.isReady})");
            }
        }
        
        // Load character data using PreviewCharacterLoader
        if (CharacterRegistry.Instance != null)
        {
            previewCharacterLoader.LoadCharacterByID(playerInfo.selectedCharacterId);
        }
        else
        {
            Debug.LogWarning("CharacterRegistry not found, cannot load character preview");
            // Still try to show something
            previewCharacterLoader.ResetToDefault();
        }
        
        Debug.Log($"Updated preview point {previewPointIndex} for player {playerInfo.playerName} with character {playerInfo.selectedCharacterId}");
    }
    
    private void SetPreviewVisible(bool visible)
    {
        if (previewCharacterLoader != null)
        {
            previewCharacterLoader.SetVisible(visible);
        }
        
        if (playerNameplate != null)
        {
            playerNameplate.SetActive(visible);
        }
    }
    
    private void OnPlayerSessionUpdated(PlayerSessionInfo sessionInfo)
    {
        Debug.Log($"[PREVIEW-{previewPointIndex}] Session updated - {sessionInfo.playerId}, assigned: {assignedPlayerGuid ?? "none"}");
        
        if (sessionInfo.playerId.ToString() == assignedPlayerGuid)
        {
            // Update our assigned player's data
            Debug.Log($"[PREVIEW-{previewPointIndex}] Updating assigned player data");
            UpdatePreviewForPlayer(sessionInfo);
        }
        else if (!isLocalPlayerPreview)
        {
            // For non-local preview points, carefully check if we need to reassign
            // This handles new players joining and players leaving
            Debug.Log($"[PREVIEW-{previewPointIndex}] Checking for reassignment...");
            AssignPlayerToPreviewPoint();
        }
        else if (isLocalPlayerPreview && string.IsNullOrEmpty(assignedPlayerGuid))
        {
            // Local player preview might need to assign local player if not assigned yet
            Debug.Log($"[PREVIEW-{previewPointIndex}] Local preview checking for assignment...");
            AssignPlayerToPreviewPoint();
        }
    }
    
    private void OnPlayerCharacterChanged(string playerGuid, int characterId)
    {
        Debug.Log($"Preview point {previewPointIndex}: Character changed for player {playerGuid} to character {characterId}. Assigned player: {assignedPlayerGuid}");
        
        if (playerGuid == assignedPlayerGuid && previewCharacterLoader != null)
        {
            Debug.Log($"Preview point {previewPointIndex}: Loading character {characterId} for assigned player");
            previewCharacterLoader.LoadCharacterByID(characterId);
        }
    }
    
    private void OnPlayerReadyChanged(string playerGuid, bool isReady)
    {
        if (playerGuid == assignedPlayerGuid)
        {
            UpdateReadyIndicator(isReady);
        }
    }
    
    private void UpdateReadyIndicator(bool isReady)
    {
        if (readyIndicator != null)
        {
            readyIndicator.SetActive(true); // Always show the indicator
            
            // Change color based on ready state
            if (readyIndicatorRenderer != null)
            {
                Color targetColor = isReady ? readyColor : notReadyColor;
                readyIndicatorRenderer.material.color = targetColor;
                Debug.Log($"Preview point {previewPointIndex}: Updated ready indicator color to {targetColor} (isReady: {isReady})");
            }
        }
        else
        {
            Debug.LogWarning($"Preview point {previewPointIndex}: Ready indicator is null!");
        }
    }
    
    private void OnPlayerJoined(string playerGuid)
    {
        Debug.Log($"[PREVIEW-{previewPointIndex}] Player joined - {playerGuid}");
        
        // Only react if this is not a local player preview
        if (!isLocalPlayerPreview)
        {
            // Check if this is actually a different player (not the local player)
            var localSession = playerSessionData.GetCurrentPlayerSession();
            bool isNewOtherPlayer = !localSession.HasValue || playerGuid != localSession.Value.playerId.ToString();
            
            if (isNewOtherPlayer)
            {
                Debug.Log($"[PREVIEW-{previewPointIndex}] New OTHER player joined - reassigning...");
                AssignPlayerToPreviewPoint();
            }
            else
            {
                Debug.Log($"[PREVIEW-{previewPointIndex}] Local player event, ignoring...");
            }
        }
        else
        {
            Debug.Log($"[PREVIEW-{previewPointIndex}] Local preview ignoring join event");
        }
    }
    
    public void SetPreviewPointIndex(int index)
    {
        previewPointIndex = index;
        AssignPlayerToPreviewPoint();
    }
    
    public void SetAsLocalPlayerPreview(bool isLocal)
    {
        isLocalPlayerPreview = isLocal;
        AssignPlayerToPreviewPoint();
    }
    
    public string GetAssignedPlayerGuid()
    {
        return assignedPlayerGuid;
    }
    
    public PlayerSessionInfo? GetCurrentPlayerInfo()
    {
        return currentPlayerInfo;
    }
    
    public override void OnDestroy()
    {
        if (playerSessionData != null)
        {
            playerSessionData.OnPlayerSessionUpdated -= OnPlayerSessionUpdated;
            playerSessionData.OnPlayerCharacterChanged -= OnPlayerCharacterChanged;
            playerSessionData.OnPlayerReadyChanged -= OnPlayerReadyChanged;
            playerSessionData.OnPlayerJoined -= OnPlayerJoined;
        }
        
        if (currentCharacterPreview != null)
        {
            DestroyImmediate(currentCharacterPreview);
        }
        
        base.OnDestroy();
    }
}