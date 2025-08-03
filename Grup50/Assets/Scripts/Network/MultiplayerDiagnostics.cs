using UnityEngine;
using Unity.Netcode;
using StarterAssets;

[System.Serializable]
public class PlayerDiagnostics
{
    public string playerName;
    public ulong clientId;
    public ulong ownerClientId;
    public bool isOwner;
    public bool isServer;
    public bool isHost;
    public bool networkSpawned;
    public bool inputEnabled;
    public bool cameraActive;
    public string cameraName;
    public bool canMove;
}

public class MultiplayerDiagnostics : NetworkBehaviour
{
    [Header("Diagnostic Info")]
    [SerializeField] private PlayerDiagnostics currentStats = new PlayerDiagnostics();
    [SerializeField] private bool enableContinuousLogging = false;
    [SerializeField] private float loggingInterval = 2f;
    
    private ThirdPersonController thirdPersonController;
    private StarterAssetsInputs starterAssetsInputs;
    private Camera playerCamera;
    private float lastLogTime;
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Get components
        thirdPersonController = GetComponent<ThirdPersonController>();
        starterAssetsInputs = GetComponent<StarterAssetsInputs>();
        
        // Initial diagnostic
        UpdateDiagnostics();
        LogCurrentState("OnNetworkSpawn");
        
        // Set up continuous logging if enabled
        if (enableContinuousLogging)
        {
            InvokeRepeating(nameof(ContinuousLogging), 1f, loggingInterval);
        }
    }
    
    void Update()
    {
        if (enableContinuousLogging && Time.time - lastLogTime > loggingInterval)
        {
            UpdateDiagnostics();
            lastLogTime = Time.time;
        }
    }
    
    private void UpdateDiagnostics()
    {
        currentStats.playerName = gameObject.name;
        currentStats.clientId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0;
        currentStats.ownerClientId = OwnerClientId;
        currentStats.isOwner = IsOwner;
        currentStats.isServer = IsServer;
        currentStats.isHost = IsHost;
        currentStats.networkSpawned = IsSpawned;
        
        // Check input components
        currentStats.inputEnabled = starterAssetsInputs != null && starterAssetsInputs.enabled;
        
        // Check camera
        playerCamera = Camera.main;
        if (thirdPersonController != null && thirdPersonController.VirtualCamera != null)
        {
            currentStats.cameraActive = thirdPersonController.VirtualCamera.isActiveAndEnabled;
            currentStats.cameraName = thirdPersonController.VirtualCamera.name;
        }
        else
        {
            currentStats.cameraActive = false;
            currentStats.cameraName = "No Virtual Camera";
        }
        
        // Check if player can move (based on ownership and input)
        currentStats.canMove = IsOwner && starterAssetsInputs != null && starterAssetsInputs.enabled;
    }
    
    private void ContinuousLogging()
    {
        UpdateDiagnostics();
        LogCurrentState("Continuous Check");
    }
    
    private void LogCurrentState(string context)
    {
        string ownershipStatus = IsOwner ? "OWNER" : "NON-OWNER";
        string networkStatus = IsSpawned ? "CONNECTED" : "DISCONNECTED";
        string inputStatus = currentStats.inputEnabled ? "ENABLED" : "DISABLED";
        string cameraStatus = currentStats.cameraActive ? "ACTIVE" : "INACTIVE";
        string movementStatus = currentStats.canMove ? "CAN MOVE" : "CANNOT MOVE";
        
        Debug.Log($"[MultiplayerDiagnostics] {context} - {currentStats.playerName} ({ownershipStatus}) - " +
                 $"Network: {networkStatus}, Input: {inputStatus}, Camera: {cameraStatus}, Movement: {movementStatus}");
        
        if (!IsOwner)
        {
            Debug.LogWarning($"[MultiplayerDiagnostics] ⚠️ {currentStats.playerName} is NOT OWNER! " +
                           $"ClientId: {currentStats.clientId}, OwnerClientId: {currentStats.ownerClientId}");
        }
        
        if (!currentStats.canMove)
        {
            Debug.LogError($"[MultiplayerDiagnostics] ❌ {currentStats.playerName} CANNOT MOVE! " +
                          $"IsOwner: {IsOwner}, InputEnabled: {currentStats.inputEnabled}");
        }
    }
    
    [ContextMenu("Force Diagnostics")]
    public void ForceDiagnostics()
    {
        UpdateDiagnostics();
        LogCurrentState("Manual Check");
        
        // Additional detailed logging
        Debug.Log($"[Detailed Diagnostics] NetworkManager LocalClientId: {(NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : -1)}");
        Debug.Log($"[Detailed Diagnostics] NetworkObject OwnerClientId: {OwnerClientId}");
        Debug.Log($"[Detailed Diagnostics] IsOwner: {IsOwner}");
        Debug.Log($"[Detailed Diagnostics] IsServer: {IsServer}");
        Debug.Log($"[Detailed Diagnostics] IsHost: {IsHost}");
        
        if (thirdPersonController != null)
        {
            Debug.Log($"[Detailed Diagnostics] ThirdPersonController found: {thirdPersonController != null}");
        }
        
        if (starterAssetsInputs != null)
        {
            Debug.Log($"[Detailed Diagnostics] StarterAssetsInputs enabled: {starterAssetsInputs.enabled}");
        }
    }
    
    [ContextMenu("Fix Ownership Issues")]
    public void FixOwnershipIssues()
    {
        if (!IsOwner)
        {
            Debug.LogError($"[MultiplayerDiagnostics] Cannot fix ownership for non-owner player {gameObject.name}");
            return;
        }
        
        // Try to fix common issues
        if (starterAssetsInputs != null && !starterAssetsInputs.enabled)
        {
            starterAssetsInputs.enabled = true;
            Debug.Log($"[MultiplayerDiagnostics] ✅ Enabled StarterAssetsInputs for {gameObject.name}");
        }
        
        var playerInput = GetComponent<UnityEngine.InputSystem.PlayerInput>();
        if (playerInput != null && !playerInput.enabled)
        {
            playerInput.enabled = true;
            Debug.Log($"[MultiplayerDiagnostics] ✅ Enabled PlayerInput for {gameObject.name}");
        }
        
        UpdateDiagnostics();
        LogCurrentState("After Fix Attempt");
    }
    
    [ContextMenu("Test Network Ownership")]
    public void TestNetworkOwnership()
    {
        Debug.Log($"[Network Ownership Test] Player: {gameObject.name}");
        Debug.Log($"[Network Ownership Test] NetworkManager.LocalClientId: {(NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : -1)}");
        Debug.Log($"[Network Ownership Test] NetworkObject.OwnerClientId: {OwnerClientId}");
        Debug.Log($"[Network Ownership Test] IsOwner: {IsOwner}");
        Debug.Log($"[Network Ownership Test] Expected: LocalClientId should equal OwnerClientId for this player");
        
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClientId == OwnerClientId)
        {
            Debug.Log($"[Network Ownership Test] ✅ Ownership is CORRECT for {gameObject.name}");
        }
        else
        {
            Debug.LogError($"[Network Ownership Test] ❌ Ownership is WRONG for {gameObject.name}! " +
                          $"This player should be owned by client {NetworkManager.Singleton?.LocalClientId} but is owned by {OwnerClientId}");
        }
    }
}