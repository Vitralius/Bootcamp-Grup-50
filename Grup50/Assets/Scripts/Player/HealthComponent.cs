using System;
using UnityEngine;
using Unity.Netcode;

public class HealthComponent : NetworkBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float respawnDelay = 3f;
    [SerializeField] private float invincibilityDuration = 1f;
    [SerializeField] private bool enableRespawn = true;
    [SerializeField] private bool disableOnDeath = true;
    
    private NetworkVariable<float> currentHealth = new NetworkVariable<float>(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> isAlive = new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    private float lastDamageTime;
    private MonoBehaviour[] componentsToDisable;
    private Collider[] collidersToDisable;
    
    public event Action<float, float> OnHealthChanged;
    public event Action OnDied;
    public event Action OnRevived;
    
    public float CurrentHealth => currentHealth.Value;
    public float MaxHealth => maxHealth;
    public bool IsAlive => isAlive.Value;
    
    public override void OnNetworkSpawn()
    {
        // Cache components that might need to be disabled on death
        CacheComponents();
        
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
        
        Debug.Log($"[HealthComponent] Object {gameObject.name} spawned with {currentHealth.Value}/{maxHealth} health");
    }
    
    public override void OnNetworkDespawn()
    {
        currentHealth.OnValueChanged -= OnHealthValueChanged;
        isAlive.OnValueChanged -= OnAliveStateChanged;
    }
    
    private void CacheComponents()
    {
        // Cache common components that should be disabled on death
        var componentsList = new System.Collections.Generic.List<MonoBehaviour>();
        var collidersList = new System.Collections.Generic.List<Collider>();
        
        // Add ThirdPersonController if it exists
        var thirdPersonController = GetComponent<StarterAssets.ThirdPersonController>();
        if (thirdPersonController != null)
            componentsList.Add(thirdPersonController);
        
        // Add all colliders
        collidersList.AddRange(GetComponents<Collider>());
        
        componentsToDisable = componentsList.ToArray();
        collidersToDisable = collidersList.ToArray();
    }
    
    private void OnHealthValueChanged(float previousValue, float newValue)
    {
        Debug.Log($"[HealthComponent] {gameObject.name} health changed: {previousValue} -> {newValue}");
        OnHealthChanged?.Invoke(newValue, maxHealth);
        
        if (newValue <= 0 && previousValue > 0)
        {
            Debug.Log($"[HealthComponent] {gameObject.name} died!");
            OnDied?.Invoke();
        }
    }
    
    private void OnAliveStateChanged(bool previousValue, bool newValue)
    {
        Debug.Log($"[HealthComponent] {gameObject.name} alive state changed: {previousValue} -> {newValue}");
        
        if (newValue && !previousValue)
        {
            Debug.Log($"[HealthComponent] {gameObject.name} revived!");
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
            Debug.Log($"[SERVER] {gameObject.name} is invincible, damage ignored");
            return;
        }
        
        lastDamageTime = Time.time;
        
        float newHealth = Mathf.Max(0, currentHealth.Value - damage);
        currentHealth.Value = newHealth;
        
        Debug.Log($"[SERVER] {gameObject.name} took {damage} damage from {attackerId}. Health: {newHealth}/{maxHealth}");
        
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
        
        Debug.Log($"[SERVER] {gameObject.name} healed for {healAmount}. Health: {newHealth}/{maxHealth}");
        
        // Show heal effect on all clients
        ShowHealEffectClientRpc(healAmount);
    }
    
    [ClientRpc]
    private void ShowDamageEffectClientRpc(float damage, ulong attackerId)
    {
        Debug.Log($"[CLIENT] {gameObject.name} took {damage} damage from {attackerId} - Visual effect here");
        // TODO: Add visual damage effects later (particle effects, screen shake, etc.)
    }
    
    [ClientRpc]
    private void ShowHealEffectClientRpc(float healAmount)
    {
        Debug.Log($"[CLIENT] {gameObject.name} healed for {healAmount} - Visual effect here");
        // TODO: Add visual heal effects later (particle effects, green numbers, etc.)
    }
    
    private void HandleDeath()
    {
        if (!IsServer) return;
        
        isAlive.Value = false;
        Debug.Log($"[SERVER] {gameObject.name} died!");
        
        if (disableOnDeath)
        {
            // Disable object components and interactions
            DisableObjectClientRpc();
        }
        
        if (enableRespawn)
        {
            Debug.Log($"[SERVER] {gameObject.name} respawning in {respawnDelay} seconds...");
            // Schedule respawn
            Invoke(nameof(RespawnObject), respawnDelay);
        }
    }
    
    [ClientRpc]
    private void DisableObjectClientRpc()
    {
        Debug.Log($"[CLIENT] {gameObject.name} disabled due to death");
        
        // Disable cached MonoBehaviour components
        foreach (var component in componentsToDisable)
        {
            if (component != null)
            {
                component.enabled = false;
            }
        }
        
        // Disable CharacterController separately
        var characterController = GetComponent<CharacterController>();
        if (characterController != null)
        {
            characterController.enabled = false;
        }
        
        // Disable Rigidbody separately (freeze it)
        var rigidbody = GetComponent<Rigidbody>();
        if (rigidbody != null)
        {
            rigidbody.isKinematic = true;
        }
        
        // Disable colliders
        foreach (var collider in collidersToDisable)
        {
            if (collider != null)
            {
                collider.enabled = false;
            }
        }
        
        // TODO: Hide object, show death effect
        
        if (IsOwner)
        {
            Debug.Log("You died!");
        }
    }
    
    private void RespawnObject()
    {
        if (!IsServer) return;
        
        // Reset health and alive state
        currentHealth.Value = maxHealth;
        isAlive.Value = true;
        
        Debug.Log($"[SERVER] {gameObject.name} respawned with full health");
        
        // Re-enable object
        EnableObjectClientRpc();
    }
    
    [ClientRpc]
    private void EnableObjectClientRpc()
    {
        Debug.Log($"[CLIENT] {gameObject.name} enabled after respawn");
        
        // Re-enable cached MonoBehaviour components
        foreach (var component in componentsToDisable)
        {
            if (component != null)
            {
                component.enabled = true;
            }
        }
        
        // Re-enable CharacterController separately
        var characterController = GetComponent<CharacterController>();
        if (characterController != null)
        {
            characterController.enabled = true;
        }
        
        // Re-enable Rigidbody separately (unfreeze it)
        var rigidbody = GetComponent<Rigidbody>();
        if (rigidbody != null)
        {
            rigidbody.isKinematic = false;
        }
        
        // Re-enable colliders
        foreach (var collider in collidersToDisable)
        {
            if (collider != null)
            {
                collider.enabled = true;
            }
        }
        
        // TODO: Show object, play respawn effect
        
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
    
    public void SetRespawnEnabled(bool enabled)
    {
        enableRespawn = enabled;
    }
    
    public void SetDisableOnDeath(bool disable)
    {
        disableOnDeath = disable;
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