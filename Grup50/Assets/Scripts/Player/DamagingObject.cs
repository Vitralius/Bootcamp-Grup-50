using System;
using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class DamagingObject : NetworkBehaviour
{
    [Header("Damage Settings")]
    [SerializeField] private float damageAmount = 25f;
    [SerializeField] private float damageDelay = 0.5f;
    [SerializeField] private bool dealDamageOnce = false;
    [SerializeField] private bool dealDamageOverTime = false;
    [SerializeField] private float damageInterval = 1f;
    
    [Header("Effect Settings")]
    [SerializeField] private bool showDebugLogs = true;
    
    private HashSet<ulong> damagedPlayers = new HashSet<ulong>();
    private Dictionary<ulong, float> playerDamageTimers = new Dictionary<ulong, float>();
    
    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        
        Debug.Log($"[DamagingObject] Trigger entered by: {other.gameObject.name} with tag: {other.tag}");
        
        if (other.CompareTag("Player"))
        {
            NetworkObject networkObject = other.GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                ulong playerId = networkObject.OwnerClientId;
                
                if (showDebugLogs)
                {
                    Debug.Log($"[DamagingObject] Player {playerId} entered damage zone");
                }
                
                if (dealDamageOnce)
                {
                    if (!damagedPlayers.Contains(playerId))
                    {
                        DealDamageToPlayer(playerId, other.gameObject);
                        damagedPlayers.Add(playerId);
                    }
                }
                else if (dealDamageOverTime)
                {
                    if (!playerDamageTimers.ContainsKey(playerId))
                    {
                        playerDamageTimers[playerId] = 0f;
                        DealDamageToPlayer(playerId, other.gameObject);
                    }
                }
                else
                {
                    DealDamageToPlayer(playerId, other.gameObject);
                }
            }
        }
    }
    
    private void OnTriggerStay(Collider other)
    {
        if (!IsServer || !dealDamageOverTime) return;
        
        if (other.CompareTag("Player"))
        {
            NetworkObject networkObject = other.GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                ulong playerId = networkObject.OwnerClientId;
                
                if (playerDamageTimers.ContainsKey(playerId))
                {
                    playerDamageTimers[playerId] += Time.deltaTime;
                    
                    if (playerDamageTimers[playerId] >= damageInterval)
                    {
                        DealDamageToPlayer(playerId, other.gameObject);
                        playerDamageTimers[playerId] = 0f;
                    }
                }
            }
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;
        
        if (other.CompareTag("Player"))
        {
            NetworkObject networkObject = other.GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                ulong playerId = networkObject.OwnerClientId;
                
                if (showDebugLogs)
                {
                    Debug.Log($"[DamagingObject] Player {playerId} exited damage zone");
                }
                
                if (playerDamageTimers.ContainsKey(playerId))
                {
                    playerDamageTimers.Remove(playerId);
                }
            }
        }
    }
    
    private void DealDamageToPlayer(ulong playerId, GameObject playerObject)
    {
        if (!IsServer) return;
        
        HealthComponent healthComponent = playerObject.GetComponent<HealthComponent>();
        if (healthComponent != null)
        {
            if (showDebugLogs)
            {
                Debug.Log($"[DamagingObject] Dealing {damageAmount} damage to player {playerId}");
            }
            
            healthComponent.TakeDamageServerRpc(damageAmount, 0);
            
            ShowDamageEffectClientRpc(playerId, damageAmount);
        }
        else
        {
            Debug.LogWarning($"[DamagingObject] Player {playerId} doesn't have HealthComponent!");
        }
    }
    
    [ClientRpc]
    private void ShowDamageEffectClientRpc(ulong playerId, float damage)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[DamagingObject] Showing damage effect for player {playerId} - {damage} damage");
        }
        
        // TODO: Add visual effects here
        // - Particle effects
        // - Screen shake for the damaged player
        // - Damage numbers
        // - Sound effects
    }
    
    public void ResetDamageState()
    {
        if (!IsServer) return;
        
        damagedPlayers.Clear();
        playerDamageTimers.Clear();
        
        if (showDebugLogs)
        {
            Debug.Log("[DamagingObject] Damage state reset");
        }
    }
    
    [ContextMenu("Debug - Reset Damage State")]
    private void DebugResetDamageState()
    {
        if (IsServer)
        {
            ResetDamageState();
        }
        else
        {
            Debug.Log("Only server can reset damage state");
        }
    }
    
    [ContextMenu("Debug - Show Current Settings")]
    private void DebugShowSettings()
    {
        Debug.Log($"[DamagingObject] Settings - Damage: {damageAmount}, Delay: {damageDelay}, Once: {dealDamageOnce}, DoT: {dealDamageOverTime}, Interval: {damageInterval}");
    }
    
    private void OnGUI()
    {
        if (!IsServer || !showDebugLogs) return;
        
        GUILayout.BeginArea(new Rect(10, 200, 300, 150));
        GUILayout.Box("Damaging Object Debug");
        GUILayout.Label($"Damage Amount: {damageAmount}");
        GUILayout.Label($"Damage Once: {dealDamageOnce}");
        GUILayout.Label($"Damage Over Time: {dealDamageOverTime}");
        GUILayout.Label($"Players Damaged: {damagedPlayers.Count}");
        GUILayout.Label($"Players in Zone: {playerDamageTimers.Count}");
        
        if (GUILayout.Button("Reset Damage State"))
        {
            ResetDamageState();
        }
        
        GUILayout.EndArea();
    }
}