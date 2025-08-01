using UnityEngine;
using Unity.Netcode;
using Unity.Cinemachine;

/// <summary>
/// Ensures all required network and camera components exist in the scene
/// </summary>
public class SceneSetupManager : NetworkBehaviour
{
    [Header("Required Network Components")]
    [SerializeField] private bool autoCreateNetworkComponents = true;
    [SerializeField] private bool autoCreateCameras = true;
    
    [Header("Camera Settings")]
    [SerializeField] private GameObject cinemachineCameraPrefab;
    [SerializeField] private string cameraName = "Player Follow Camera";
    [SerializeField] private float cameraDistance = 10f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    
    void Awake()
    {
        // Ensure required components exist before anything else starts
        EnsureRequiredComponents();
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        if (showDebugLogs)
        {
            LogSceneStatus();
        }
        
        // Create cameras if needed
        if (autoCreateCameras && IsServer)
        {
            EnsureCamerasExist();
        }
    }
    
    /// <summary>
    /// Ensure all required network components exist in the scene
    /// </summary>
    private void EnsureRequiredComponents()
    {
        if (!autoCreateNetworkComponents) return;
        
        // Ensure PlayerSessionData exists
        if (PlayerSessionData.Instance == null)
        {
            GameObject sessionDataObj = new GameObject("PlayerSessionData");
            sessionDataObj.AddComponent<NetworkObject>();
            sessionDataObj.AddComponent<PlayerSessionData>();
            DontDestroyOnLoad(sessionDataObj);
            
            if (showDebugLogs)
                Debug.Log("[SceneSetupManager] ✅ Created missing PlayerSessionData");
        }
        
        // Ensure PersistentCharacterCache exists
        if (PersistentCharacterCache.Instance == null)
        {
            GameObject cacheObj = new GameObject("PersistentCharacterCache");
            cacheObj.AddComponent<PersistentCharacterCache>();
            DontDestroyOnLoad(cacheObj);
            
            if (showDebugLogs)
                Debug.Log("[SceneSetupManager] ✅ Created missing PersistentCharacterCache");
        }
    }
    
    /// <summary>
    /// Ensure Cinemachine cameras exist for multiplayer
    /// </summary>
    private void EnsureCamerasExist()
    {
        // Check if any Cinemachine cameras already exist
        var existingCameras = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
        
        if (existingCameras.Length == 0)
        {
            CreateDefaultCinemachineCamera();
        }
        else if (showDebugLogs)
        {
            Debug.Log($"[SceneSetupManager] Found {existingCameras.Length} existing Cinemachine cameras");
        }
    }
    
    /// <summary>
    /// Create a default Cinemachine camera for the scene
    /// </summary>
    private void CreateDefaultCinemachineCamera()
    {
        // Create main camera if it doesn't exist
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            GameObject mainCamObj = new GameObject("Main Camera");
            mainCamera = mainCamObj.AddComponent<Camera>();
            mainCamera.tag = "MainCamera";
            
            if (showDebugLogs)
                Debug.Log("[SceneSetupManager] ✅ Created Main Camera");
        }
        
        // Create Cinemachine Brain on main camera
        var cinemachineBrain = mainCamera.GetComponent<CinemachineBrain>();
        if (cinemachineBrain == null)
        {
            cinemachineBrain = mainCamera.gameObject.AddComponent<CinemachineBrain>();
            if (showDebugLogs)
                Debug.Log("[SceneSetupManager] ✅ Added CinemachineBrain to Main Camera");
        }
        
        // Create Virtual Camera
        GameObject vcamObj = new GameObject(cameraName);
        var virtualCamera = vcamObj.AddComponent<CinemachineCamera>();
        
        // Set up camera properties
        virtualCamera.Priority = 10;
        
        // Add Third Person Follow component
        var thirdPersonFollow = virtualCamera.gameObject.AddComponent<CinemachineThirdPersonFollow>();
        thirdPersonFollow.CameraDistance = cameraDistance;
        thirdPersonFollow.CameraSide = 1f; // Right side
        // Note: Collision settings can be configured in Inspector if needed
        
        virtualCamera.Follow = null; // Will be set by players
        virtualCamera.LookAt = null; // Will be set by players
        
        if (showDebugLogs)
            Debug.Log("[SceneSetupManager] ✅ Created default Cinemachine Virtual Camera");
    }
    
    /// <summary>
    /// Log the current status of scene components
    /// </summary>
    private void LogSceneStatus()
    {
        Debug.Log("=== Scene Setup Status ===");
        
        // Check NetworkManager
        Debug.Log($"NetworkManager: {(NetworkManager.Singleton != null ? "✅ Found" : "❌ Missing")}");
        
        // Check PlayerSessionData
        Debug.Log($"PlayerSessionData: {(PlayerSessionData.Instance != null ? "✅ Found" : "❌ Missing")}");
        
        // Check PersistentCharacterCache
        Debug.Log($"PersistentCharacterCache: {(PersistentCharacterCache.Instance != null ? "✅ Found" : "❌ Missing")}");
        
        // Check cameras
        var mainCamera = Camera.main;
        Debug.Log($"Main Camera: {(mainCamera != null ? "✅ Found" : "❌ Missing")}");
        
        var cinemachineCameras = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
        Debug.Log($"Cinemachine Cameras: {(cinemachineCameras.Length > 0 ? $"✅ Found {cinemachineCameras.Length}" : "❌ None found")}");
        
        var cinemachineBrain = mainCamera?.GetComponent<CinemachineBrain>();
        Debug.Log($"Cinemachine Brain: {(cinemachineBrain != null ? "✅ Found" : "❌ Missing")}");
        
        Debug.Log("========================");
    }
    
    /// <summary>
    /// Context menu method to manually check scene setup
    /// </summary>
    [ContextMenu("Check Scene Setup")]
    public void ManualCheckSetup()
    {
        LogSceneStatus();
    }
    
    /// <summary>
    /// Context menu method to manually create missing components
    /// </summary>
    [ContextMenu("Create Missing Components")]
    public void ManualCreateComponents()
    {
        EnsureRequiredComponents();
        EnsureCamerasExist();
        LogSceneStatus();
    }
}