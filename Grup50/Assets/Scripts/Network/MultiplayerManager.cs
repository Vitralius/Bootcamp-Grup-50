using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Threading.Tasks;

public class MultiplayerManager : MonoBehaviour
{
    public static MultiplayerManager Instance { get; private set; }
    
    [Header("Lobby Settings")]
    [SerializeField] private int maxPlayersPerLobby = 4;
    [SerializeField] private string lobbyName = "Game Lobby";
    
    public event Action<string> OnLobbyCodeGenerated;
    public event Action<string> OnLobbyJoined;
    public event Action OnLobbyLeft;
    public event Action<string> OnLobbyError;
    public event Action<List<string>> OnLobbyPlayersUpdated;
    
    private Lobby currentLobby;
    private string currentLobbyCode;
    private UnityTransport transport;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        transport = FindFirstObjectByType<UnityTransport>();
        if (transport == null)
        {
            Debug.LogError("Unity Transport component not found. Make sure it's attached to NetworkManager.");
        }
    }
    
    private async void Start()
    {
        await InitializeUnityServices();
    }
    
    private async Task InitializeUnityServices()
    {
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
                Debug.Log("Unity Services Initialized");
            }
            
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"Signed in as: {AuthenticationService.Instance.PlayerId}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to initialize Unity Services: {e.Message}");
            OnLobbyError?.Invoke($"Service initialization failed: {e.Message}");
        }
    }
    
    public async Task<string> CreateLobby()
    {
        try
        {
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
            
            // Create Relay allocation
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayersPerLobby - 1);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            
            // Configure Unity Transport with Relay
            transport.SetRelayServerData(allocation.RelayServer.IpV4, (ushort)allocation.RelayServer.Port, 
                allocation.AllocationIdBytes, allocation.Key, allocation.ConnectionData);
            
            // Create lobby with Relay integration
            CreateLobbyOptions options = new CreateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { "RelayJoinCode", new DataObject(DataObject.VisibilityOptions.Member, joinCode) }
                }
            };
            
            currentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayersPerLobby, options);
            currentLobbyCode = currentLobby.LobbyCode;
            
            // Start NetworkManager as Host
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.StartHost();
            }
            else
            {
                Debug.LogError("NetworkManager.Singleton is null when trying to start host");
                OnLobbyError?.Invoke("NetworkManager not available");
                return null;
            }
            
            Debug.Log($"Lobby created with code: {currentLobbyCode}");
            OnLobbyCodeGenerated?.Invoke(currentLobbyCode);
            
            // Start heartbeat to keep lobby alive
            StartLobbyHeartbeat();
            
            return currentLobbyCode;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to create lobby: {e.Message}");
            OnLobbyError?.Invoke($"Failed to create lobby: {e.Message}");
            return null;
        }
    }
    
    public async Task<bool> JoinLobbyByCode(string lobbyCode)
    {
        try
        {
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
            
            // Join lobby
            currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode);
            currentLobbyCode = lobbyCode;
            
            // Get Relay join code from lobby data
            string relayJoinCode = currentLobby.Data["RelayJoinCode"].Value;
            
            // Join Relay allocation
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);
            
            // Configure Unity Transport with Relay
            transport.SetRelayServerData(joinAllocation.RelayServer.IpV4, (ushort)joinAllocation.RelayServer.Port,
                joinAllocation.AllocationIdBytes, joinAllocation.Key, joinAllocation.ConnectionData, joinAllocation.HostConnectionData);
            
            // Update player data with allocation ID
            UpdatePlayerOptions updateOptions = new UpdatePlayerOptions
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    { "RelayAllocationId", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, joinAllocation.AllocationId.ToString()) }
                }
            };
            
            await LobbyService.Instance.UpdatePlayerAsync(currentLobby.Id, AuthenticationService.Instance.PlayerId, updateOptions);
            
            // Start NetworkManager as Client
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.StartClient();
            }
            else
            {
                Debug.LogError("NetworkManager.Singleton is null when trying to start client");
                OnLobbyError?.Invoke("NetworkManager not available");
                return false;
            }
            
            Debug.Log($"Joined lobby: {lobbyCode}");
            OnLobbyJoined?.Invoke(lobbyCode);
            
            // Start polling for lobby updates
            StartLobbyPolling();
            
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to join lobby: {e.Message}");
            OnLobbyError?.Invoke($"Failed to join lobby: {e.Message}");
            return false;
        }
    }
    
    public async Task LeaveLobby()
    {
        try
        {
            if (currentLobby != null)
            {
                await LobbyService.Instance.RemovePlayerAsync(currentLobby.Id, AuthenticationService.Instance.PlayerId);
                
                // Stop NetworkManager
                if (NetworkManager.Singleton != null)
                {
                    if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient)
                    {
                        NetworkManager.Singleton.Shutdown();
                    }
                }
                else
                {
                    Debug.LogWarning("NetworkManager.Singleton is null when trying to shutdown");
                }
                
                currentLobby = null;
                currentLobbyCode = null;
                
                StopLobbyHeartbeat();
                StopLobbyPolling();
                
                Debug.Log("Left lobby");
                OnLobbyLeft?.Invoke();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to leave lobby: {e.Message}");
            OnLobbyError?.Invoke($"Failed to leave lobby: {e.Message}");
        }
    }
    
    public void CopyLobbyCodeToClipboard()
    {
        if (!string.IsNullOrEmpty(currentLobbyCode))
        {
            GUIUtility.systemCopyBuffer = currentLobbyCode;
            Debug.Log($"Lobby code copied to clipboard: {currentLobbyCode}");
        }
    }
    
    public string GetCurrentLobbyCode()
    {
        return currentLobbyCode;
    }
    
    public bool IsInLobby()
    {
        return currentLobby != null;
    }
    
    public List<string> GetLobbyPlayerNames()
    {
        List<string> playerNames = new List<string>();
        if (currentLobby != null)
        {
            foreach (var player in currentLobby.Players)
            {
                playerNames.Add(player.Id);
            }
        }
        return playerNames;
    }
    
    private void StartLobbyHeartbeat()
    {
        if (currentLobby == null) return;
        
        InvokeRepeating(nameof(SendLobbyHeartbeat), 0f, 15f);
    }
    
    private void StopLobbyHeartbeat()
    {
        CancelInvoke(nameof(SendLobbyHeartbeat));
    }
    
    private async void SendLobbyHeartbeat()
    {
        if (currentLobby != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            try
            {
                await LobbyService.Instance.SendHeartbeatPingAsync(currentLobby.Id);
            }
            catch (Exception e)
            {
                Debug.LogError($"Heartbeat failed: {e.Message}");
            }
        }
    }
    
    private void StartLobbyPolling()
    {
        if (currentLobby == null) return;
        
        InvokeRepeating(nameof(PollLobbyUpdates), 0f, 2f);
    }
    
    private void StopLobbyPolling()
    {
        CancelInvoke(nameof(PollLobbyUpdates));
    }
    
    private async void PollLobbyUpdates()
    {
        if (currentLobby != null)
        {
            try
            {
                currentLobby = await LobbyService.Instance.GetLobbyAsync(currentLobby.Id);
                OnLobbyPlayersUpdated?.Invoke(GetLobbyPlayerNames());
            }
            catch (Exception e)
            {
                Debug.LogError($"Lobby polling failed: {e.Message}");
            }
        }
    }
    
    private void OnDestroy()
    {
        StopLobbyHeartbeat();
        StopLobbyPolling();
    }
    
    private async void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && currentLobby != null)
        {
            await LeaveLobby();
        }
    }
}