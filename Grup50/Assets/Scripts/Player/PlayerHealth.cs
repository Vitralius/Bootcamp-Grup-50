using System;
using UnityEngine;
using Unity.Netcode;

public class inPlayerHealth : NetworkBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float respawnDelay = 3f;
    [SerializeField] private float invincibilityDuration = 1f;
    
    private NetworkVariable<float> currentHealth = new NetworkVariable<float>(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> isAlive = new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    private float lastDamageTime;
    private StarterAssets.ThirdPersonController playerController;
    private CharacterController characterController;
    
    public event Action<float, float> OnHealthChanged;
    public event Action OnDied;
    public event Action OnRevived;
    
    public float CurrentHealth => currentHealth.Value;
    public float MaxHealth => maxHealth;
    public bool IsAlive => isAlive.Value;
    
    public override void OnNetworkSpawn()
    {
        // Get component references
        playerController = GetComponent<StarterAssets.ThirdPersonController>();
        characterController = GetComponent<CharacterController>();
        
        if (IsServer)
        {
            currentHealth.Value = maxHealth;
            isAlive.Value = true;
        }
        
        currentHealth.OnValueChanged += OnHealthValueChanged;
        isAlive.OnValueChanged += OnAliveStateChanged;
        
        // Initialize for existing values (late joiners)
        if (IsClient)
        {
            OnHealthChanged?.Invoke(currentHealth.Value, maxHealth);
        }
        
        Debug.Log($"[PlayerHealth] Player {OwnerClientId} spawned with {currentHealth.Value}/{maxHealth} health");
    }
    
    public override void OnNetworkDespawn()
    {
        currentHealth.OnValueChanged -= OnHealthValueChanged;
        isAlive.OnValueChanged -= OnAliveStateChanged;
    }
    
    private void OnHealthValueChanged(float previousValue, float newValue)
    {
        Debug.Log($"[PlayerHealth] Player {OwnerClientId} health changed: {previousValue} -> {newValue}");
        OnHealthChanged?.Invoke(newValue, maxHealth);
        
        if (newValue <= 0 && previousValue > 0)
        {
            Debug.Log($"[PlayerHealth] Player {OwnerClientId} died!");
            OnDied?.Invoke();
        }
    }
    
    private void OnAliveStateChanged(bool previousValue, bool newValue)
    {
        Debug.Log($"[PlayerHealth] Player {OwnerClientId} alive state changed: {previousValue} -> {newValue}");
        
        if (newValue && !previousValue)
        {
            Debug.Log($"[PlayerHealth] Player {OwnerClientId} revived!");
            OnRevived?.Invoke();
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void TakeDamageServerRpc(float damage, ulong attackerId = 0)
    {
        if (!IsServer || !isAlive.Value || damage <= 0) return;
        
        // Check invincibility frames
        if (Time.time - lastDamageTime < invincibilityDuration)
        {
            Debug.Log($"[SERVER] Player {OwnerClientId} is invincible, damage ignored");
            return;
        }
        
        lastDamageTime = Time.time;
        
        float newHealth = Mathf.Max(0, currentHealth.Value - damage);
        currentHealth.Value = newHealth;
        
        Debug.Log($"[SERVER] Player {OwnerClientId} took {damage} damage from {attackerId}. Health: {newHealth}/{maxHealth}");
        
        // Show damage effect on all clients
        ShowDamageEffectClientRpc(damage, attackerId);
        
        if (newHealth <= 0 && isAlive.Value)
        {
            HandleDeath();
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void HealServerRpc(float healAmount)
    {
        if (!IsServer || !isAlive.Value || healAmount <= 0) return;
        
        float newHealth = Mathf.Min(maxHealth, currentHealth.Value + healAmount);
        currentHealth.Value = newHealth;
        
        Debug.Log($"[SERVER] Player {OwnerClientId} healed for {healAmount}. Health: {newHealth}/{maxHealth}");
        
        // Show heal effect on all clients
        ShowHealEffectClientRpc(healAmount);
    }
    
    [ClientRpc]
    private void ShowDamageEffectClientRpc(float damage, ulong attackerId)
    {
        Debug.Log($"[CLIENT] Player {OwnerClientId} took {damage} damage from {attackerId} - Visual effect here");
        // TODO: Add visual damage effects later (particle effects, screen shake, etc.)
    }
    
    [ClientRpc]
    private void ShowHealEffectClientRpc(float healAmount)
    {
        Debug.Log($"[CLIENT] Player {OwnerClientId} healed for {healAmount} - Visual effect here");
        // TODO: Add visual heal effects later (particle effects, green numbers, etc.)
    }
    
    private void HandleDeath()
    {
        if (!IsServer) return;
        
        isAlive.Value = false;
        Debug.Log($"[SERVER] Player {OwnerClientId} died! Respawning in {respawnDelay} seconds...");
        
        // Disable player movement and interactions
        DisablePlayerClientRpc();
        
        // Schedule respawn
        Invoke(nameof(RespawnPlayer), respawnDelay);
    }
    
    [ClientRpc]
    private void DisablePlayerClientRpc()
    {
        Debug.Log($"[CLIENT] Player {OwnerClientId} disabled due to death");
        
        // Disable player controls
        if (playerController != null)
        {
            playerController.enabled = false;
        }
        
        // Disable character controller to prevent physics interactions
        if (characterController != null)
        {
            characterController.enabled = false;
        }
        
        // TODO: Hide character, show death effect
        
        if (IsOwner)
        {
            Debug.Log("You died! Respawning soon...");
        }
    }
    
    private void RespawnPlayer()
    {
        if (!IsServer) return;
        
        // Reset health and alive state
        currentHealth.Value = maxHealth;
        isAlive.Value = true;
        
        Debug.Log($"[SERVER] Player {OwnerClientId} respawned with full health");
        
        // Re-enable player
        EnablePlayerClientRpc();
    }
    
    [ClientRpc]
    private void EnablePlayerClientRpc()
    {
        Debug.Log($"[CLIENT] Player {OwnerClientId} enabled after respawn");
        
        // Re-enable player controls
        if (playerController != null)
        {
            playerController.enabled = true;
        }
        
        // Re-enable character controller
        if (characterController != null)
        {
            characterController.enabled = true;
        }
        
        // TODO: Show character, play respawn effect
        
        if (IsOwner)
        {
            Debug.Log("You respawned!");
        }
    }
    
    // Debug methods for testing
    [ContextMenu("Debug - Take 20 Damage")]
    public void DebugTakeDamage()
    {
        if (IsServer)
        {
            TakeDamageServerRpc(20f);
        }
        else
        {
            Debug.Log("Only server can execute debug damage in this context");
        }
    }
    
    [ContextMenu("Debug - Heal 30")]
    public void DebugHeal()
    {
        if (IsServer)
        {
            HealServerRpc(30f);
        }
        else
        {
            Debug.Log("Only server can execute debug heal in this context");
        }
    }
    
    // Public methods for other systems to use
    public void DealDamage(float damage, ulong attackerId = 0)
    {
        TakeDamageServerRpc(damage, attackerId);
    }
    
    public void RestoreHealth(float healAmount)
    {
        HealServerRpc(healAmount);
    }
    
    public float GetHealthPercentage()
    {
        return currentHealth.Value / maxHealth;
    }
    
    // For debugging in Inspector
    private void OnGUI()
    {
        if (!IsOwner) return;
        
        GUI.Box(new Rect(10, 10, 200, 80), "");
        GUI.Label(new Rect(15, 15, 190, 20), $"Health: {currentHealth.Value:F1}/{maxHealth}");
        GUI.Label(new Rect(15, 35, 190, 20), $"Alive: {isAlive.Value}");
        GUI.Label(new Rect(15, 55, 190, 20), $"Health %: {GetHealthPercentage():P1}");
        
        // Debug buttons
        if (GUI.Button(new Rect(10, 100, 80, 30), "Take 20 Dmg"))
        {
            DealDamage(20f);
        }
        
        if (GUI.Button(new Rect(100, 100, 80, 30), "Heal 30"))
        {
            RestoreHealth(30f);
        }
    }
}