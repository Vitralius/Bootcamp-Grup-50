using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

public class SessionManager : MonoBehaviour
{
    [Header("Session Settings")]
    [SerializeField] private int maxPlayers = 4;
    [SerializeField] private string sessionName = "GameSession";
    
    [Header("UI References")]
    [SerializeField] private GameObject lobbyUI;
    [SerializeField] private GameObject gameUI;
    
    // Events
    public static event Action<string> OnSessionCreated;
    public static event Action OnSessionJoined;
    public static event Action<string> OnSessionError;
    
    // Private fields
    private ISession currentSession;
    private NetworkManager networkManager;
    private bool isInitialized = false;
    
    private void Awake()
    {
        networkManager = GetComponent<NetworkManager>();
        if (networkManager == null)
            networkManager = FindFirstObjectByType<NetworkManager>();
    }
    
    private async void Start()
    {
        await InitializeUnityServices();
    }
    
    /// <summary>
    /// Initialize Unity Gaming Services and authenticate player
    /// </summary>
    private async Task InitializeUnityServices()
    {
        try
        {
            // Initialize Unity Services
            await UnityServices.InitializeAsync();
            
            // Authenticate anonymously
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"Signed in as: {AuthenticationService.Instance.PlayerId}");
            }
            
            isInitialized = true;
            Debug.Log("Unity Gaming Services initialized successfully");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to initialize Unity Gaming Services: {e}");
            OnSessionError?.Invoke($"Initialization failed: {e.Message}");
        }
    }
    
    /// <summary>
    /// Create a new game session with invite code
    /// </summary>
    public async void CreateSession()
    {
        if (!isInitialized)
        {
            OnSessionError?.Invoke("Services not initialized");
            return;
        }
        
        try
        {
            // First, try to leave any existing session
            await LeaveCurrentSessionSilently();
            
            var sessionOptions = new SessionOptions()
            {
                MaxPlayers = maxPlayers,
                Name = sessionName + "_" + System.Guid.NewGuid().ToString("N")[..8], // Make session name unique
                IsPrivate = false // Set to true if you want private sessions only
            };
            
            currentSession = await MultiplayerService.Instance.CreateOrJoinSessionAsync(sessionOptions.Name, sessionOptions);
            
            // Start as host in Netcode
            networkManager.StartHost();
            
            // Get join code for the session
            string joinCode = currentSession.Code;
            
            Debug.Log($"Session created with join code: {joinCode}");
            OnSessionCreated?.Invoke(joinCode);
            
            // Switch UI
            lobbyUI?.SetActive(false);
            gameUI?.SetActive(true);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to create session: {e}");
            OnSessionError?.Invoke($"Failed to create session: {e.Message}");
        }
    }
    
    /// <summary>
    /// Join an existing session using invite code
    /// </summary>
    public async void JoinSession(string joinCode)
    {
        if (!isInitialized)
        {
            OnSessionError?.Invoke("Services not initialized");
            return;
        }
        
        if (string.IsNullOrEmpty(joinCode))
        {
            OnSessionError?.Invoke("Join code cannot be empty");
            return;
        }
        
        try
        {
            // First, try to leave any existing session
            await LeaveCurrentSessionSilently();
            
            currentSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode);
            
            // Get the host's connection info and start as client
            // Note: You might need to implement relay or use direct connection
            networkManager.StartClient();
            
            Debug.Log($"Joined session with code: {joinCode}");
            OnSessionJoined?.Invoke();
            
            // Switch UI
            lobbyUI?.SetActive(false);
            gameUI?.SetActive(true);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to join session: {e}");
            OnSessionError?.Invoke($"Failed to join session: {e.Message}");
        }
    }
    
    /// <summary>
    /// Leave current session
    /// </summary>
    public async void LeaveSession()
    {
        try
        {
            if (currentSession != null)
            {
                await currentSession.LeaveAsync();
                currentSession = null;
            }
            
            // Stop networking
            if (networkManager.IsHost)
                networkManager.Shutdown();
            else if (networkManager.IsClient)
                networkManager.Shutdown();
            
            // Switch UI back
            lobbyUI?.SetActive(true);
            gameUI?.SetActive(false);
            
            Debug.Log("Left session");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to leave session: {e}");
        }
    }
    
    /// <summary>
    /// Check if currently in a session
    /// </summary>
    public bool IsInSession()
    {
        return currentSession != null;
    }
    
    /// <summary>
    /// Get current session ID if available
    /// </summary>
    public string GetCurrentSessionId()
    {
        return currentSession?.Id ?? string.Empty;
    }
    
    /// <summary>
    /// Silently leave current session without UI updates (used for cleanup)
    /// </summary>
    private async Task LeaveCurrentSessionSilently()
    {
        try
        {
            if (currentSession != null)
            {
                await currentSession.LeaveAsync();
                currentSession = null;
                Debug.Log("Left previous session silently");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Error during silent session cleanup: {e.Message}");
            // Don't throw - this is cleanup, continue anyway
            currentSession = null; // Reset anyway
        }
    }
    
    private void OnDestroy()
    {
        // Clean up session when object is destroyed
        if (currentSession != null)
        {
            _ = Task.Run(async () => await currentSession.LeaveAsync());
        }
    }
}