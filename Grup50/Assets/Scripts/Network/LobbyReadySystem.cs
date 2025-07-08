using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Services.Authentication;

public class LobbyReadySystem : NetworkBehaviour
{
    private NetworkVariable<int> readyPlayerCount = new NetworkVariable<int>(0);
    private Dictionary<string, bool> playerReadyStates = new Dictionary<string, bool>();
    
    public event System.Action<string, bool> OnPlayerReadyChanged;
    public event System.Action<bool> OnAllPlayersReadyChanged;
    
    public override void OnNetworkSpawn()
    {
        readyPlayerCount.OnValueChanged += OnReadyCountChanged;
    }
    
    public override void OnNetworkDespawn()
    {
        readyPlayerCount.OnValueChanged -= OnReadyCountChanged;
    }
    
    public void SetPlayerReady(bool ready)
    {
        string playerId = AuthenticationService.Instance.PlayerId;
        Debug.Log($"Setting player ready: {playerId} = {ready}");
        SetPlayerReadyServerRpc(playerId, ready);
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void SetPlayerReadyServerRpc(string playerId, bool ready)
    {
        Debug.Log($"[SERVER] SetPlayerReadyServerRpc: {playerId} = {ready}");
        
        if (!playerReadyStates.ContainsKey(playerId))
        {
            playerReadyStates[playerId] = false;
            Debug.Log($"[SERVER] Added new player to ready states: {playerId}");
        }
        
        bool wasReady = playerReadyStates[playerId];
        playerReadyStates[playerId] = ready;
        
        // Update ready count
        if (ready && !wasReady)
        {
            readyPlayerCount.Value++;
            Debug.Log($"[SERVER] Incremented ready count to {readyPlayerCount.Value}");
        }
        else if (!ready && wasReady)
        {
            readyPlayerCount.Value--;
            Debug.Log($"[SERVER] Decremented ready count to {readyPlayerCount.Value}");
        }
        
        // Notify clients about player ready state change
        NotifyPlayerReadyChangedClientRpc(playerId, ready);
        
        // Check if all players are ready
        bool allReady = AreAllPlayersReady();
        NotifyAllPlayersReadyChangedClientRpc(allReady);
        
        Debug.Log($"[SERVER] Player {playerId} ready state: {ready}. Total ready: {readyPlayerCount.Value}");
        
        // Debug: Print all player states
        foreach (var kvp in playerReadyStates)
        {
            Debug.Log($"[SERVER] Player {kvp.Key}: {(kvp.Value ? "Ready" : "Not Ready")}");
        }
    }
    
    [ClientRpc]
    private void NotifyPlayerReadyChangedClientRpc(string playerId, bool ready)
    {
        Debug.Log($"[CLIENT] NotifyPlayerReadyChangedClientRpc: {playerId} = {ready}");
        OnPlayerReadyChanged?.Invoke(playerId, ready);
    }
    
    [ClientRpc]
    private void NotifyAllPlayersReadyChangedClientRpc(bool allReady)
    {
        OnAllPlayersReadyChanged?.Invoke(allReady);
    }
    
    public bool IsPlayerReady(string playerId)
    {
        return playerReadyStates.ContainsKey(playerId) && playerReadyStates[playerId];
    }
    
    public int GetReadyPlayerCount()
    {
        return readyPlayerCount.Value;
    }
    
    public bool AreAllPlayersReady()
    {
        if (MultiplayerManager.Instance == null) return false;
        
        var playerNames = MultiplayerManager.Instance.GetLobbyPlayerNames();
        if (playerNames.Count == 0) return false;
        
        Debug.Log($"Checking ready status for {playerNames.Count} players:");
        foreach (var playerName in playerNames)
        {
            bool ready = IsPlayerReady(playerName);
            Debug.Log($"Player {playerName}: {(ready ? "Ready" : "Not Ready")}");
            if (!ready)
            {
                return false;
            }
        }
        
        Debug.Log("All players are ready!");
        return true;
    }
    
    public void ResetAllPlayers()
    {
        if (!IsServer) return;
        
        playerReadyStates.Clear();
        readyPlayerCount.Value = 0;
        
        NotifyAllPlayersReadyChangedClientRpc(false);
    }
    
    public void RemovePlayer(string playerId)
    {
        if (!IsServer) return;
        
        if (playerReadyStates.ContainsKey(playerId))
        {
            if (playerReadyStates[playerId])
            {
                readyPlayerCount.Value--;
            }
            
            playerReadyStates.Remove(playerId);
            
            bool allReady = AreAllPlayersReady();
            NotifyAllPlayersReadyChangedClientRpc(allReady);
        }
    }
    
    private void OnReadyCountChanged(int previousValue, int newValue)
    {
        Debug.Log($"Ready player count changed from {previousValue} to {newValue}");
    }
    
    public Dictionary<string, bool> GetAllPlayerReadyStates()
    {
        return new Dictionary<string, bool>(playerReadyStates);
    }
}