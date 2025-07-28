using System;
using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class DamagingObject : NetworkBehaviour
{
    [Header("Damage Settings")]
    [SerializeField] private float damageAmount = 25f;
    
    // Public property for accessing damage
    public float damage 
    { 
        get => damageAmount; 
        set => damageAmount = value; 
    }
    [SerializeField] private float damageDelay = 0.5f;
    [SerializeField] private bool dealDamageOnce = false;
    [SerializeField] private bool dealDamageOverTime = false;
    [SerializeField] private float damageInterval = 1f;
    
    [Header("Target Settings")]
    [SerializeField] private bool damagePlayersOnly = false;
    [SerializeField] private bool damageDestructibleObjects = true;
    
    [Header("Effect Settings")]
    [SerializeField] private bool showDebugLogs = true;
    
    private HashSet<ulong> damagedPlayers = new HashSet<ulong>();
    private Dictionary<ulong, float> playerDamageTimers = new Dictionary<ulong, float>();
    private HashSet<GameObject> damagedObjects = new HashSet<GameObject>();
    private Dictionary<GameObject, float> objectDamageTimers = new Dictionary<GameObject, float>();
    
    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        
        Debug.Log($"[DamagingObject] Trigger entered by: {other.gameObject.name} with tag: {other.tag}");
        
        // Check if we should damage this object
        bool isPlayer = other.CompareTag("Player");
        bool hasHealthComponent = other.GetComponent<HealthComponent>() != null;
        
        if (isPlayer && hasHealthComponent)
        {
            // Handle player damage (always damage players if they have health)
            HandlePlayerDamage(other);
        }
        else if (!isPlayer && hasHealthComponent && damageDestructibleObjects && !damagePlayersOnly)
        {
            // Handle destructible object damage (only if not player-only mode)
            HandleObjectDamage(other.gameObject);
        }
    }
    
    private void HandlePlayerDamage(Collider playerCollider)
    {
        NetworkObject networkObject = playerCollider.GetComponent<NetworkObject>();
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
                    DealDamageToPlayer(playerId, playerCollider.gameObject);
                    damagedPlayers.Add(playerId);
                }
            }
            else if (dealDamageOverTime)
            {
                if (!playerDamageTimers.ContainsKey(playerId))
                {
                    playerDamageTimers[playerId] = 0f;
                    DealDamageToPlayer(playerId, playerCollider.gameObject);
                }
            }
            else
            {
                DealDamageToPlayer(playerId, playerCollider.gameObject);
            }
        }
    }
    
    private void HandleObjectDamage(GameObject obj)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[DamagingObject] Destructible object {obj.name} entered damage zone");
        }
        
        if (dealDamageOnce)
        {
            if (!damagedObjects.Contains(obj))
            {
                DealDamageToObject(obj);
                damagedObjects.Add(obj);
            }
        }
        else if (dealDamageOverTime)
        {
            if (!objectDamageTimers.ContainsKey(obj))
            {
                objectDamageTimers[obj] = 0f;
                DealDamageToObject(obj);
            }
        }
        else
        {
            DealDamageToObject(obj);
        }
    }
    
    private void OnTriggerStay(Collider other)
    {
        if (!IsServer || !dealDamageOverTime) return;
        
        bool isPlayer = other.CompareTag("Player");
        bool hasHealthComponent = other.GetComponent<HealthComponent>() != null;
        
        if (isPlayer && hasHealthComponent)
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
        else if (!isPlayer && hasHealthComponent && damageDestructibleObjects && !damagePlayersOnly)
        {
            GameObject obj = other.gameObject;
            if (objectDamageTimers.ContainsKey(obj))
            {
                objectDamageTimers[obj] += Time.deltaTime;
                
                if (objectDamageTimers[obj] >= damageInterval)
                {
                    DealDamageToObject(obj);
                    objectDamageTimers[obj] = 0f;
                }
            }
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;
        
        bool isPlayer = other.CompareTag("Player");
        
        if (isPlayer)
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
        else
        {
            // Handle destructible object exit
            GameObject obj = other.gameObject;
            if (objectDamageTimers.ContainsKey(obj))
            {
                objectDamageTimers.Remove(obj);
                
                if (showDebugLogs)
                {
                    Debug.Log($"[DamagingObject] Object {obj.name} exited damage zone");
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
    
    private void DealDamageToObject(GameObject obj)
    {
        if (!IsServer) return;
        
        HealthComponent healthComponent = obj.GetComponent<HealthComponent>();
        if (healthComponent != null)
        {
            if (showDebugLogs)
            {
                Debug.Log($"[DamagingObject] Dealing {damageAmount} damage to object {obj.name}");
            }
            
            // Deal damage to the object - use 0 as attacker ID for environmental damage
            healthComponent.TakeDamageServerRpc(damageAmount, 0);
            
            ShowObjectDamageEffectClientRpc(obj.name, damageAmount);
        }
        else
        {
            Debug.LogWarning($"[DamagingObject] Object {obj.name} doesn't have HealthComponent!");
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
    
    [ClientRpc]
    private void ShowObjectDamageEffectClientRpc(string objectName, float damage)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[DamagingObject] Showing damage effect for object {objectName} - {damage} damage");
        }
        
        // TODO: Add object damage visual effects here
        // - Particle effects at object location
        // - Damage numbers floating up
        // - Sound effects
        // - Object shake/wobble animation
    }
    
    public void ResetDamageState()
    {
        if (!IsServer) return;
        
        damagedPlayers.Clear();
        playerDamageTimers.Clear();
        damagedObjects.Clear();
        objectDamageTimers.Clear();
        
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
        
        GUILayout.BeginArea(new Rect(10, 200, 300, 180));
        GUILayout.Box("Damaging Object Debug");
        GUILayout.Label($"Damage Amount: {damageAmount}");
        GUILayout.Label($"Players Only: {damagePlayersOnly}");
        GUILayout.Label($"Damage Objects: {damageDestructibleObjects}");
        GUILayout.Label($"Damage Once: {dealDamageOnce}");
        GUILayout.Label($"Damage Over Time: {dealDamageOverTime}");
        GUILayout.Label($"Players Damaged: {damagedPlayers.Count}");
        GUILayout.Label($"Players in Zone: {playerDamageTimers.Count}");
        GUILayout.Label($"Objects Damaged: {damagedObjects.Count}");
        GUILayout.Label($"Objects in Zone: {objectDamageTimers.Count}");
        
        if (GUILayout.Button("Reset Damage State"))
        {
            ResetDamageState();
        }
        
        GUILayout.EndArea();
    }
}