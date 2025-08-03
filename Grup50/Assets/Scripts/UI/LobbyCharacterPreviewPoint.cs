using UnityEngine;
using Unity.Netcode;
using Unity.Services.Authentication;

public class LobbyCharacterPreviewPoint : NetworkBehaviour
{
    [Header("Preview Settings")]
    [SerializeField] private Transform characterSpawnPoint;
    [SerializeField] private GameObject baseCharacterPrefab;
    [SerializeField] private GameObject existingCharacterPreview; // Use existing preview instead of instantiating
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
    private UltraSimpleMeshSwapper characterLoader;
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
        
        // Initialize preview character but keep it hidden
        CreatePreviewCharacter();
        
        // Start hidden until a player is assigned
        SetPreviewVisible(false);
        
        isInitialized = true;
        
        Debug.Log($"[PREVIEW-{previewPointIndex}] ✅ Initialized. isLocalPlayerPreview: {isLocalPlayerPreview}, GameObject: {gameObject.name}");
        
        // Only assign players if this is a local player preview
        if (isLocalPlayerPreview)
        {
            // Local player preview should show immediately when local player is ready
            Debug.Log($"[PREVIEW-{previewPointIndex}] 🏠 LOCAL preview waiting for local player session...");
            Invoke(nameof(TryAssignLocalPlayer), 0.5f);
        }
        else
        {
            // Other player previews start hidden and wait for actual players to join
            Debug.Log($"[PREVIEW-{previewPointIndex}] 👥 OTHER preview waiting for other players...");
            // Don't do any automatic assignment - wait for events
        }
    }
    
    private void CreatePreviewCharacter()
    {
        // First, try to use existing character preview if assigned
        if (existingCharacterPreview != null)
        {
            Debug.Log($"[PREVIEW-{previewPointIndex}] Using existing character preview: {existingCharacterPreview.name}");
            currentCharacterPreview = existingCharacterPreview;
        }
        else if (baseCharacterPrefab != null)
        {
            // Only instantiate if no existing preview is assigned
            Debug.Log($"[PREVIEW-{previewPointIndex}] No existing preview assigned, instantiating from prefab");
            
            // Destroy existing preview if any (NETWORK SAFE)
            if (currentCharacterPreview != null && currentCharacterPreview != existingCharacterPreview)
            {
                DestroyPreviewSafely(currentCharacterPreview);
                currentCharacterPreview = null;
            }
            
            // Instantiate base character at spawn point
            Transform spawnTransform = characterSpawnPoint != null ? characterSpawnPoint : transform;
            currentCharacterPreview = Instantiate(baseCharacterPrefab, spawnTransform.position, spawnTransform.rotation, transform);
            
            // CRITICAL: Remove ALL network components from preview (it's just visual)
            RemoveAllNetworkComponents(currentCharacterPreview);
            
            // Disable movement components (preview should be static)
            var controller = currentCharacterPreview.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
            }
            
            // Remove any movement scripts to keep it simple (but preserve UltraSimpleMeshSwapper)
            var movementScripts = currentCharacterPreview.GetComponents<MonoBehaviour>();
            foreach (var script in movementScripts)
            {
                if (script.GetType().Name.Contains("Controller") || script.GetType().Name.Contains("Movement"))
                {
                    // Don't disable UltraSimpleMeshSwapper - we need it!
                    if (script.GetType() != typeof(UltraSimpleMeshSwapper))
                    {
                        script.enabled = false;
                    }
                }
            }
        }
        else
        {
            Debug.LogError($"[PREVIEW-{previewPointIndex}] No character preview assigned and no base prefab available!");
            return;
        }
        
        // Get or add UltraSimpleMeshSwapper with proper initialization order
        characterLoader = currentCharacterPreview.GetComponent<UltraSimpleMeshSwapper>();
        if (characterLoader == null)
        {
            Debug.Log($"[PREVIEW-{previewPointIndex}] UltraSimpleMeshSwapper not found, adding new one");
            characterLoader = currentCharacterPreview.AddComponent<UltraSimpleMeshSwapper>();
        }
        else
        {
            Debug.Log($"[PREVIEW-{previewPointIndex}] Found existing UltraSimpleMeshSwapper");
        }
        
        // CRITICAL: Set preview mode BEFORE any other operations
        characterLoader.SetPreviewMode(true);
        
        // Wait a frame to ensure component is properly initialized
        StartCoroutine(DelayedCharacterLoaderSetup());
        
        // Debug the component structure
        var allRenderers = currentCharacterPreview.GetComponentsInChildren<Renderer>();
        Debug.Log($"[PREVIEW-{previewPointIndex}] Found {allRenderers.Length} renderers in character preview:");
        foreach (var renderer in allRenderers)
        {
            Debug.Log($"  - {renderer.GetType().Name}: {renderer.name} (enabled: {renderer.enabled})");
        }
        
        Debug.Log($"[PREVIEW-{previewPointIndex}] Character preview setup initiated: {currentCharacterPreview.name}");
    }
    
    /// <summary>
    /// Delayed setup to ensure proper initialization order
    /// </summary>
    private System.Collections.IEnumerator DelayedCharacterLoaderSetup()
    {
        // Wait one frame to ensure all components are initialized
        yield return null;
        
        if (characterLoader != null)
        {
            Debug.Log($"[PREVIEW-{previewPointIndex}] Character loader setup complete after delay");
            
            // If we already have a player assigned, try to load their character
            if (!string.IsNullOrEmpty(assignedPlayerGuid) && currentPlayerInfo.HasValue)
            {
                Debug.Log($"[PREVIEW-{previewPointIndex}] Reloading character {currentPlayerInfo.Value.selectedCharacterId} after setup delay");
                characterLoader.LoadCharacterByID(currentPlayerInfo.Value.selectedCharacterId);
            }
        }
        else
        {
            Debug.LogError($"[PREVIEW-{previewPointIndex}] Character loader is null after delayed setup!");
        }
    }
    
    private void TryAssignLocalPlayer()
    {
        if (!isLocalPlayerPreview || !isInitialized || playerSessionData == null)
        {
            Debug.Log($"[PREVIEW-{previewPointIndex}] TryAssignLocalPlayer failed - isLocal: {isLocalPlayerPreview}, init: {isInitialized}, sessionData: {playerSessionData != null}");
            return;
        }
            
        Debug.Log($"[PREVIEW-{previewPointIndex}] 🔍 TryAssignLocalPlayer: Attempting to get current player session...");
        
        var currentSession = playerSessionData.GetCurrentPlayerSession();
        if (currentSession.HasValue)
        {
            Debug.Log($"[PREVIEW-{previewPointIndex}] ✅ Found local player session: {currentSession.Value.playerId}");
            Debug.Log($"[PREVIEW-{previewPointIndex}] 🎯 Player Name: {currentSession.Value.playerName}");
            Debug.Log($"[PREVIEW-{previewPointIndex}] 🎮 Character ID: {currentSession.Value.selectedCharacterId}");
            Debug.Log($"[PREVIEW-{previewPointIndex}] 🔌 Is Connected: {currentSession.Value.isConnected}");
            Debug.Log($"[PREVIEW-{previewPointIndex}] ✓ Is Ready: {currentSession.Value.isReady}");
            Debug.Log($"[PREVIEW-{previewPointIndex}] 📞 Calling AssignPlayerToPreviewPoint()...");
            AssignPlayerToPreviewPoint();
        }
        else
        {
            Debug.Log($"[PREVIEW-{previewPointIndex}] ❌ GetCurrentPlayerSession() returned null");
            
            // Try to debug why it's null
            try
            {
                string currentPlayerId = Unity.Services.Authentication.AuthenticationService.Instance.PlayerId;
                Debug.Log($"[PREVIEW-{previewPointIndex}] 🔍 Current PlayerId from AuthService: {currentPlayerId}");
                
                var allSessions = playerSessionData.GetConnectedPlayerSessions();
                Debug.Log($"[PREVIEW-{previewPointIndex}] 🔍 Total connected sessions: {allSessions.Count}");
                
                for (int i = 0; i < allSessions.Count; i++)
                {
                    var session = allSessions[i];
                    Debug.Log($"[PREVIEW-{previewPointIndex}] 📋 Session {i}: {session.playerId} ({session.playerName})");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PREVIEW-{previewPointIndex}] ❌ Error during debug check: {e.Message}");
            }
            
            Debug.Log($"[PREVIEW-{previewPointIndex}] ⏰ No local player session yet, retrying in 1s...");
            Invoke(nameof(TryAssignLocalPlayer), 1f);
        }
    }
    
    private void CheckForOtherPlayers()
    {
        // This method is now only called by events, not during initialization
        if (!isLocalPlayerPreview && playerSessionData != null)
        {
            var otherPlayerCount = GetOtherPlayerCount();
            Debug.Log($"[PREVIEW-{previewPointIndex}] Event-triggered check: {otherPlayerCount} other players found");
            
            // Only assign if we don't already have someone assigned and there are other players
            if (string.IsNullOrEmpty(assignedPlayerGuid) && otherPlayerCount > 0 && previewPointIndex < otherPlayerCount)
            {
                Debug.Log($"[PREVIEW-{previewPointIndex}] Attempting assignment due to player join event...");
                AssignPlayerToPreviewPoint();
            }
            else
            {
                Debug.Log($"[PREVIEW-{previewPointIndex}] No assignment needed (already assigned: {!string.IsNullOrEmpty(assignedPlayerGuid)}, count: {otherPlayerCount})");
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
        if (playerSessionData == null || !isInitialized) 
        {
            Debug.LogError($"[PREVIEW-{previewPointIndex}] ❌ AssignPlayerToPreviewPoint failed - sessionData: {playerSessionData != null}, init: {isInitialized}");
            return;
        }
        
        Debug.Log($"[PREVIEW-{previewPointIndex}] 🚀 AssignPlayerToPreviewPoint started");
        Debug.Log($"[PREVIEW-{previewPointIndex}] 🎯 Current assignedPlayerGuid: '{assignedPlayerGuid}'");
        
        // Clear previous assignment
        string previousAssignment = assignedPlayerGuid;
        assignedPlayerGuid = null;
        SetPreviewVisible(false);
        
        Debug.Log($"[PREVIEW-{previewPointIndex}] 🧹 Cleared previous assignment (was: '{previousAssignment}'). isLocalPlayerPreview: {isLocalPlayerPreview}");
        
        if (isLocalPlayerPreview)
        {
            Debug.Log($"[PREVIEW-{previewPointIndex}] 🏠 Processing LOCAL player preview...");
            
            // This preview point shows the local player ONLY
            var currentSession = playerSessionData.GetCurrentPlayerSession();
            if (currentSession.HasValue)
            {
                string newGuid = currentSession.Value.playerId.ToString().Trim();
                Debug.Log($"[PREVIEW-{previewPointIndex}] 🎯 Setting assignedPlayerGuid to: '{newGuid}'");
                
                assignedPlayerGuid = newGuid;
                
                Debug.Log($"[PREVIEW-{previewPointIndex}] 📞 Calling UpdatePreviewForPlayer...");
                UpdatePreviewForPlayer(currentSession.Value);
                
                Debug.Log($"[PREVIEW-{previewPointIndex}] 👁️ Setting preview visible to true...");
                SetPreviewVisible(true);
                
                Debug.Log($"[PREVIEW-{previewPointIndex}] ✅ SUCCESS: Assigned LOCAL player {assignedPlayerGuid}");
                Debug.Log($"[PREVIEW-{previewPointIndex}] 🔍 Final check - assignedPlayerGuid: '{assignedPlayerGuid}'");
                
                // Verify assignment worked
                if (string.IsNullOrEmpty(assignedPlayerGuid))
                {
                    Debug.LogError($"[PREVIEW-{previewPointIndex}] ❌ ASSIGNMENT FAILED: assignedPlayerGuid is null/empty after assignment!");
                }
            }
            else
            {
                Debug.LogError($"[PREVIEW-{previewPointIndex}] ❌ No local player session found in AssignPlayerToPreviewPoint");
                // Try to debug why it's null
                try
                {
                    string currentPlayerId = Unity.Services.Authentication.AuthenticationService.Instance.PlayerId;
                    Debug.LogError($"[PREVIEW-{previewPointIndex}] 🔍 Current player ID from auth: '{currentPlayerId}'");
                    
                    var allSessions = playerSessionData.GetConnectedPlayerSessions();
                    Debug.LogError($"[PREVIEW-{previewPointIndex}] 🔍 Total sessions available: {allSessions.Count}");
                    
                    foreach (var session in allSessions)
                    {
                        Debug.LogError($"[PREVIEW-{previewPointIndex}] 🔍 Session: {session.playerId} ({session.playerName})");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[PREVIEW-{previewPointIndex}] ❌ Exception during debugging: {e.Message}");
                }
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
                string localPlayerId = localSession.Value.playerId.ToString().Trim();
                
                foreach (var session in allSessions)
                {
                    string sessionPlayerId = session.playerId.ToString().Trim();
                    
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
                assignedPlayerGuid = targetSession.playerId.ToString().Trim();
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
        if (characterLoader == null) return;
        
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
        
        // Load character data using UltraSimpleMeshSwapper
        if (characterLoader != null)
        {
            if (CharacterRegistry.Instance != null)
            {
                Debug.Log($"[PREVIEW-{previewPointIndex}] Loading character {playerInfo.selectedCharacterId} for player {playerInfo.playerName}");
                bool success = characterLoader.LoadCharacterByID(playerInfo.selectedCharacterId);
                
                if (!success)
                {
                    Debug.LogWarning($"[PREVIEW-{previewPointIndex}] Character loading failed, will retry...");
                    // The UltraSimpleMeshSwapper will handle retries automatically
                }
            }
            else
            {
                Debug.LogWarning($"[PREVIEW-{previewPointIndex}] CharacterRegistry not found, retrying in 1s...");
                // Retry after CharacterRegistry might be ready
                StartCoroutine(RetryCharacterLoad(playerInfo.selectedCharacterId, 3));
            }
        }
        else
        {
            Debug.LogError($"[PREVIEW-{previewPointIndex}] Character loader is null! Cannot load character preview.");
        }
        
        Debug.Log($"Updated preview point {previewPointIndex} for player {playerInfo.playerName} with character {playerInfo.selectedCharacterId}");
    }
    
    /// <summary>
    /// Retry character loading when dependencies aren't ready
    /// </summary>
    private System.Collections.IEnumerator RetryCharacterLoad(int characterId, int maxRetries)
    {
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            yield return new WaitForSeconds(1f);
            
            if (characterLoader != null && CharacterRegistry.Instance != null)
            {
                Debug.Log($"[PREVIEW-{previewPointIndex}] Retrying character load {characterId} (attempt {attempt + 1})");
                bool success = characterLoader.LoadCharacterByID(characterId);
                
                if (success)
                {
                    Debug.Log($"[PREVIEW-{previewPointIndex}] Character load retry successful!");
                    yield break;
                }
            }
        }
        
        Debug.LogError($"[PREVIEW-{previewPointIndex}] Failed to load character {characterId} after {maxRetries} retry attempts");
    }
    
    private void SetPreviewVisible(bool visible)
    {
        Debug.Log($"[PREVIEW-{previewPointIndex}] Setting visibility: {visible}");
        
        if (characterLoader != null)
        {
            characterLoader.SetVisible(visible);
        }
        
        if (playerNameplate != null)
        {
            playerNameplate.SetActive(visible);
        }
        
        if (readyIndicator != null)
        {
            readyIndicator.SetActive(visible);
        }
        
        // Also hide the entire character preview object if needed
        if (currentCharacterPreview != null)
        {
            currentCharacterPreview.SetActive(visible);
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
            // For non-local preview points, only reassign if we need to accommodate new players
            // But be more conservative - only if we don't have anyone assigned yet
            if (string.IsNullOrEmpty(assignedPlayerGuid))
            {
                Debug.Log($"[PREVIEW-{previewPointIndex}] No one assigned, checking for new other players...");
                AssignPlayerToPreviewPoint();
            }
            else
            {
                Debug.Log($"[PREVIEW-{previewPointIndex}] Already have player assigned, not reassigning");
            }
        }
        else if (isLocalPlayerPreview && string.IsNullOrEmpty(assignedPlayerGuid))
        {
            // Local player preview needs to assign local player if not assigned yet
            var currentSession = playerSessionData.GetCurrentPlayerSession();
            if (currentSession.HasValue && currentSession.Value.playerId.ToString() == sessionInfo.playerId.ToString())
            {
                Debug.Log($"[PREVIEW-{previewPointIndex}] Local player session available, assigning...");
                AssignPlayerToPreviewPoint();
            }
        }
    }
    
    private void OnPlayerCharacterChanged(string playerGuid, int characterId)
    {
        Debug.Log($"[PREVIEW-{previewPointIndex}] 🎮 CHARACTER CHANGED EVENT:");
        Debug.Log($"[PREVIEW-{previewPointIndex}] 🎯 Event player GUID: '{playerGuid}'");
        Debug.Log($"[PREVIEW-{previewPointIndex}] 🆔 Character ID: {characterId}");
        Debug.Log($"[PREVIEW-{previewPointIndex}] 🔗 Our assigned player: '{assignedPlayerGuid ?? "none"}'");
        Debug.Log($"[PREVIEW-{previewPointIndex}] 🏠 Is local preview: {isLocalPlayerPreview}");
        Debug.Log($"[PREVIEW-{previewPointIndex}] 🎲 Preview point index: {previewPointIndex}");
        Debug.Log($"[PREVIEW-{previewPointIndex}] 🔢 String comparison: '{playerGuid}' == '{assignedPlayerGuid}' = {playerGuid == assignedPlayerGuid}");
        
        // Additional debugging for GUID comparison
        if (!string.IsNullOrEmpty(playerGuid) && !string.IsNullOrEmpty(assignedPlayerGuid))
        {
            Debug.Log($"[PREVIEW-{previewPointIndex}] 🔍 GUID Length check: Event={playerGuid.Length}, Assigned={assignedPlayerGuid.Length}");
            Debug.Log($"[PREVIEW-{previewPointIndex}] 🔍 GUID Trim check: Event='{playerGuid.Trim()}', Assigned='{assignedPlayerGuid.Trim()}'");
        }
        
        // Use trimmed string comparison to avoid whitespace issues
        bool isMatch = !string.IsNullOrEmpty(playerGuid) && !string.IsNullOrEmpty(assignedPlayerGuid) && 
                      playerGuid.Trim() == assignedPlayerGuid.Trim();
        
        if (isMatch)
        {
            Debug.Log($"[PREVIEW-{previewPointIndex}] ✅ MATCH! This character change is for us!");
            
            if (characterLoader != null)
            {
                Debug.Log($"[PREVIEW-{previewPointIndex}] 🔄 Loading character {characterId} for assigned player");
                characterLoader.LoadCharacterByID(characterId);
                Debug.Log($"[PREVIEW-{previewPointIndex}] 🎯 Character loading completed");
                
                // Force visibility update
                if (currentPlayerInfo.HasValue)
                {
                    var updatedInfo = currentPlayerInfo.Value;
                    updatedInfo.selectedCharacterId = characterId;
                    currentPlayerInfo = updatedInfo;
                    SetPreviewVisible(true);
                }
            }
            else
            {
                Debug.LogError($"[PREVIEW-{previewPointIndex}] ❌ Character loader is null!");
                Debug.LogError($"[PREVIEW-{previewPointIndex}] 🔍 Current character preview object: {(currentCharacterPreview != null ? currentCharacterPreview.name : "NULL")}");
            }
        }
        else
        {
            Debug.Log($"[PREVIEW-{previewPointIndex}] ❌ NO MATCH: Character change not for us");
            Debug.Log($"[PREVIEW-{previewPointIndex}] 📝 Event player: '{playerGuid}' vs Our player: '{assignedPlayerGuid ?? "none"}'");
            
            // If this is a local preview and we have no assigned player, try to assign now
            if (isLocalPlayerPreview && string.IsNullOrEmpty(assignedPlayerGuid))
            {
                Debug.Log($"[PREVIEW-{previewPointIndex}] 🔄 Local preview has no assignment, attempting to assign now");
                AssignPlayerToPreviewPoint();
            }
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
            
            if (isNewOtherPlayer && string.IsNullOrEmpty(assignedPlayerGuid))
            {
                Debug.Log($"[PREVIEW-{previewPointIndex}] New OTHER player joined and we have no assignment - checking...");
                AssignPlayerToPreviewPoint();
            }
            else if (isNewOtherPlayer)
            {
                Debug.Log($"[PREVIEW-{previewPointIndex}] New OTHER player joined but we already have {assignedPlayerGuid}");
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
    
    /// <summary>
    /// Removes all network components from a preview object to prevent Netcode warnings
    /// </summary>
    private void RemoveAllNetworkComponents(GameObject previewObject)
    {
        if (previewObject == null) return;
        
        Debug.Log($"[PREVIEW-{previewPointIndex}] Removing network components from {previewObject.name}");
        
        // Remove NetworkBehaviour components (like ClientNetworkAnimator, ClientNetworkTransform)
        var networkBehaviours = previewObject.GetComponentsInChildren<NetworkBehaviour>();
        foreach (var netBehaviour in networkBehaviours)
        {
            Debug.Log($"[PREVIEW-{previewPointIndex}] Disabling NetworkBehaviour: {netBehaviour.GetType().Name}");
            // NETWORK FIX: Disable component instead of destroying it to prevent network errors
            netBehaviour.enabled = false;
        }
        
        // Remove NetworkObject components (NETWORK SAFE)
        var networkObjects = previewObject.GetComponentsInChildren<NetworkObject>();
        foreach (var netObject in networkObjects)
        {
            Debug.Log($"[PREVIEW-{previewPointIndex}] Disabling NetworkObject on {netObject.name}");
            // NETWORK FIX: Disable instead of destroy to prevent "Invalid Destroy" errors
            netObject.enabled = false;
        }
        
        // Remove any remaining network-related components by name
        var allComponents = previewObject.GetComponentsInChildren<Component>();
        foreach (var component in allComponents)
        {
            if (component != null && 
                (component.GetType().Name.Contains("Network") || 
                 component.GetType().Name.Contains("Client") ||
                 component.GetType().Namespace != null && component.GetType().Namespace.Contains("Unity.Netcode")))
            {
                // Don't remove critical components
                if (!(component is Transform || component is Renderer || component is Animator || component is SkinnedMeshRenderer))
                {
                    Debug.Log($"[PREVIEW-{previewPointIndex}] Disabling network-related component: {component.GetType().Name}");
                    // NETWORK FIX: Disable instead of destroy to prevent network errors
                    if (component is Behaviour behaviour)
                    {
                        behaviour.enabled = false;
                    }
                }
            }
        }
        
        Debug.Log($"[PREVIEW-{previewPointIndex}] Network component removal complete for {previewObject.name}");
    }
    
    /// <summary>
    /// NETWORK SAFE: Properly destroy preview objects without causing network errors
    /// </summary>
    private void DestroyPreviewSafely(GameObject previewObject)
    {
        if (previewObject == null) return;
        
        Debug.Log($"[PREVIEW-{previewPointIndex}] Safely destroying preview: {previewObject.name}");
        
        // First, check if this object has NetworkObject component
        NetworkObject netObj = previewObject.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            // CRITICAL FIX: Never destroy NetworkObjects on client - this causes the "Invalid Destroy" error
            if (netObj.IsSpawned)
            {
                Debug.LogWarning($"[PREVIEW-{previewPointIndex}] Cannot destroy spawned NetworkObject {previewObject.name} - this should not happen for previews!");
                // Just disable it instead
                previewObject.SetActive(false);
                return;
            }
            else
            {
                // Safe to destroy non-spawned NetworkObjects
                Debug.Log($"[PREVIEW-{previewPointIndex}] NetworkObject not spawned, safe to destroy");
            }
        }
        
        // Remove all network components first (safely)
        RemoveAllNetworkComponents(previewObject);
        
        // Now safe to destroy the pure preview object
        if (Application.isPlaying)
        {
            Destroy(previewObject);
        }
        else
        {
            DestroyImmediate(previewObject);
        }
        
        Debug.Log($"[PREVIEW-{previewPointIndex}] Preview destroyed safely");
    }
    
    /// <summary>
    /// Public method to ensure character loader is created and ready
    /// Used by MainMenuUI fallback system
    /// </summary>
    public UltraSimpleMeshSwapper GetOrCreateCharacterLoader()
    {
        if (characterLoader == null && currentCharacterPreview != null)
        {
            Debug.Log($"[PREVIEW-{previewPointIndex}] GetOrCreateCharacterLoader: Creating missing character loader");
            
            // First check if component already exists
            characterLoader = currentCharacterPreview.GetComponent<UltraSimpleMeshSwapper>();
            if (characterLoader == null)
            {
                // Check if the object has a SkinnedMeshRenderer before adding the component
                var renderer = currentCharacterPreview.GetComponentInChildren<SkinnedMeshRenderer>();
                if (renderer != null)
                {
                    characterLoader = currentCharacterPreview.AddComponent<UltraSimpleMeshSwapper>();
                    Debug.Log($"[PREVIEW-{previewPointIndex}] Added UltraSimpleMeshSwapper to {currentCharacterPreview.name}");
                }
                else
                {
                    Debug.LogError($"[PREVIEW-{previewPointIndex}] Cannot add UltraSimpleMeshSwapper - no SkinnedMeshRenderer found on {currentCharacterPreview.name}");
                    return null;
                }
            }
            
            if (characterLoader != null)
            {
                characterLoader.SetPreviewMode(true);
                Debug.Log($"[PREVIEW-{previewPointIndex}] Character loader ready with preview mode enabled");
            }
        }
        else if (currentCharacterPreview == null)
        {
            Debug.LogError($"[PREVIEW-{previewPointIndex}] Cannot create character loader - currentCharacterPreview is null");
        }
        
        return characterLoader;
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
            DestroyPreviewSafely(currentCharacterPreview);
            currentCharacterPreview = null;
        }
        
        base.OnDestroy();
    }
}