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
        
        // Initialize preview character
        CreatePreviewCharacter();
        
        // Start hidden until a player is assigned
        SetPreviewVisible(false);
        
        isInitialized = true;
        
        Debug.Log($"Preview point {previewPointIndex} initialized. isLocalPlayerPreview: {isLocalPlayerPreview}");
        
        // Try to assign player (might not have any players yet)
        AssignPlayerToPreviewPoint();
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
    
    private void AssignPlayerToPreviewPoint()
    {
        if (playerSessionData == null || !isInitialized) return;
        
        // Clear previous assignment
        assignedPlayerGuid = null;
        SetPreviewVisible(false);
        
        Debug.Log($"Assigning player to preview point {previewPointIndex}. isLocalPlayerPreview: {isLocalPlayerPreview}");
        
        if (isLocalPlayerPreview)
        {
            // This preview point shows the local player
            var currentSession = playerSessionData.GetCurrentPlayerSession();
            if (currentSession.HasValue)
            {
                assignedPlayerGuid = currentSession.Value.playerId.ToString();
                UpdatePreviewForPlayer(currentSession.Value);
                SetPreviewVisible(true);
                Debug.Log($"Assigned local player {assignedPlayerGuid} to preview point {previewPointIndex}");
            }
            else
            {
                Debug.Log($"No local player session found for preview point {previewPointIndex}");
            }
        }
        else
        {
            // This preview point shows other players
            var allSessions = playerSessionData.GetConnectedPlayerSessions();
            var localSession = playerSessionData.GetCurrentPlayerSession();
            
            Debug.Log($"Preview point {previewPointIndex}: Found {allSessions.Count} connected sessions");
            
            int otherPlayerIndex = 0;
            foreach (var session in allSessions)
            {
                // Skip local player
                if (localSession.HasValue && session.playerId.ToString() == localSession.Value.playerId.ToString())
                {
                    Debug.Log($"Skipping local player {session.playerId} for preview point {previewPointIndex}");
                    continue;
                }
                
                if (otherPlayerIndex == previewPointIndex)
                {
                    assignedPlayerGuid = session.playerId.ToString();
                    UpdatePreviewForPlayer(session);
                    SetPreviewVisible(true);
                    Debug.Log($"Assigned other player {assignedPlayerGuid} to preview point {previewPointIndex}");
                    break;
                }
                
                otherPlayerIndex++;
            }
            
            if (assignedPlayerGuid == null)
            {
                Debug.Log($"No other player found for preview point {previewPointIndex} (index {previewPointIndex})");
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
            readyIndicator.SetActive(playerInfo.isReady);
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
        if (sessionInfo.playerId.ToString() == assignedPlayerGuid)
        {
            UpdatePreviewForPlayer(sessionInfo);
        }
        else
        {
            // Player list might have changed, reassign preview points
            AssignPlayerToPreviewPoint();
        }
    }
    
    private void OnPlayerCharacterChanged(string playerGuid, int characterId)
    {
        if (playerGuid == assignedPlayerGuid && previewCharacterLoader != null)
        {
            previewCharacterLoader.LoadCharacterByID(characterId);
        }
    }
    
    private void OnPlayerReadyChanged(string playerGuid, bool isReady)
    {
        if (playerGuid == assignedPlayerGuid && readyIndicator != null)
        {
            readyIndicator.SetActive(isReady);
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
        }
        
        if (currentCharacterPreview != null)
        {
            DestroyImmediate(currentCharacterPreview);
        }
        
        base.OnDestroy();
    }
}