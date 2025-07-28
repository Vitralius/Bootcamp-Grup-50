using Unity.Netcode;
using UnityEngine;
using System;

/// <summary>
/// ExperienceComponent - Handles XP collection, leveling, and progression for survivorlike gameplay
/// Integrates with existing character systems and provides network synchronization
/// </summary>
public class ExperienceComponent : NetworkBehaviour
{
    [Header("Experience Settings")]
    [SerializeField] private float baseXPRequirement = 100f;
    [SerializeField] private float xpRequirementMultiplier = 1.5f;
    [SerializeField] private int maxLevel = 10;
    [SerializeField] private float xpCollectionRange = 3f;
    
    [Header("Level-Up Bonuses")]
    [SerializeField] private float healthBonusPerLevel = 20f;
    [SerializeField] private float speedBonusPerLevel = 0.2f;
    [SerializeField] private float damageBonusPerLevel = 0.15f; // 15% damage increase per level
    
    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private bool verboseLogging = false;
    
    // Network synchronized variables
    private NetworkVariable<float> networkCurrentXP = new NetworkVariable<float>(0f, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server);
    
    private NetworkVariable<int> networkCurrentLevel = new NetworkVariable<int>(1, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server);
    
    private NetworkVariable<float> networkXPToNextLevel = new NetworkVariable<float>(100f, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server);
    
    // Components
    private HealthComponent healthComponent;
    private StarterAssets.ThirdPersonController playerController;
    
    // Local tracking
    private float initialHealth;
    private float initialMoveSpeed;
    private float initialSprintSpeed;
    
    // Events
    public event Action<int> OnLevelUp;
    public event Action<float> OnXPGained;
    public event Action<float, float> OnXPChanged; // current, required
    
    // Public properties for UI
    public float CurrentXP => networkCurrentXP.Value;
    public int CurrentLevel => networkCurrentLevel.Value;
    public float XPToNextLevel => networkXPToNextLevel.Value;
    public float XPProgress => CurrentXP / XPToNextLevel;
    public bool IsMaxLevel => CurrentLevel >= maxLevel;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Subscribe to network variable changes
        networkCurrentXP.OnValueChanged += OnNetworkXPChanged;
        networkCurrentLevel.OnValueChanged += OnNetworkLevelChanged;
        networkXPToNextLevel.OnValueChanged += OnNetworkXPRequirementChanged;
        
        // Initialize components
        InitializeComponents();
        
        // Initialize XP system
        if (IsServer)
        {
            InitializeXPSystem();
        }
    }
    
    public override void OnNetworkDespawn()
    {
        // Unsubscribe from network variable changes
        if (networkCurrentXP != null)
            networkCurrentXP.OnValueChanged -= OnNetworkXPChanged;
        if (networkCurrentLevel != null)
            networkCurrentLevel.OnValueChanged -= OnNetworkLevelChanged;
        if (networkXPToNextLevel != null)
            networkXPToNextLevel.OnValueChanged -= OnNetworkXPRequirementChanged;
            
        base.OnNetworkDespawn();
    }

    private void InitializeComponents()
    {
        // Get health component
        healthComponent = GetComponent<HealthComponent>();
        if (healthComponent == null)
        {
            Debug.LogWarning($"ExperienceComponent: No HealthComponent found on {gameObject.name}");
        }
        else
        {
            initialHealth = healthComponent.MaxHealth;
        }
        
        // Get player controller
        playerController = GetComponent<StarterAssets.ThirdPersonController>();
        if (playerController == null)
        {
            Debug.LogWarning($"ExperienceComponent: No ThirdPersonController found on {gameObject.name}");
        }
        else
        {
            initialMoveSpeed = playerController.MoveSpeed;
            initialSprintSpeed = playerController.SprintSpeed;
        }
        
        if (verboseLogging)
        {
            Debug.Log($"ExperienceComponent: Initialized on {gameObject.name} - Health: {initialHealth}, MoveSpeed: {initialMoveSpeed}");
        }
    }

    private void InitializeXPSystem()
    {
        if (!IsServer) return;
        
        // Set initial values
        networkCurrentXP.Value = 0f;
        networkCurrentLevel.Value = 1;
        networkXPToNextLevel.Value = CalculateXPRequiredForLevel(2); // XP needed for level 2
        
        if (verboseLogging)
        {
            Debug.Log($"ExperienceComponent: XP System initialized - Level: {networkCurrentLevel.Value}, XP Required: {networkXPToNextLevel.Value}");
        }
    }

    /// <summary>
    /// Add XP to the player (Server-only)
    /// </summary>
    public void AddXP(float amount)
    {
        if (!IsServer)
        {
            Debug.LogWarning("ExperienceComponent: AddXP called on client - only server can modify XP");
            return;
        }
        
        if (IsMaxLevel)
        {
            if (verboseLogging)
                Debug.Log($"ExperienceComponent: Player at max level {maxLevel}, XP gain ignored");
            return;
        }
        
        float oldXP = networkCurrentXP.Value;
        networkCurrentXP.Value += amount;
        
        if (verboseLogging)
        {
            Debug.Log($"ExperienceComponent: Added {amount} XP. Total: {networkCurrentXP.Value}/{networkXPToNextLevel.Value}");
        }
        
        // Check for level up
        CheckLevelUp();
        
        // Trigger XP gained event
        OnXPGained?.Invoke(amount);
    }

    /// <summary>
    /// Check if player should level up
    /// </summary>
    private void CheckLevelUp()
    {
        if (!IsServer) return;
        
        while (networkCurrentXP.Value >= networkXPToNextLevel.Value && !IsMaxLevel)
        {
            LevelUp();
        }
    }

    /// <summary>
    /// Level up the player
    /// </summary>
    private void LevelUp()
    {
        if (!IsServer) return;
        
        int oldLevel = networkCurrentLevel.Value;
        
        // Subtract XP requirement from current XP
        networkCurrentXP.Value -= networkXPToNextLevel.Value;
        
        // Increase level
        networkCurrentLevel.Value++;
        
        // Calculate new XP requirement
        if (!IsMaxLevel)
        {
            networkXPToNextLevel.Value = CalculateXPRequiredForLevel(networkCurrentLevel.Value + 1);
        }
        else
        {
            networkXPToNextLevel.Value = 0f; // Max level reached
        }
        
        // Apply level-up bonuses
        ApplyLevelUpBonuses();
        
        Debug.Log($"ExperienceComponent: 🎉 LEVEL UP! {oldLevel} → {networkCurrentLevel.Value} (XP: {networkCurrentXP.Value}/{networkXPToNextLevel.Value})");
        
        // Trigger level up event
        OnLevelUp?.Invoke(networkCurrentLevel.Value);
        
        // Notify clients about level up
        NotifyLevelUpClientRpc(networkCurrentLevel.Value);
    }

    /// <summary>
    /// Apply stat bonuses when leveling up
    /// </summary>
    private void ApplyLevelUpBonuses()
    {
        if (!IsServer) return;
        
        int currentLevel = networkCurrentLevel.Value;
        
        // Apply health bonus
        if (healthComponent != null)
        {
            float newMaxHealth = initialHealth + (healthBonusPerLevel * (currentLevel - 1));
            healthComponent.SetMaxHealthServerRpc(newMaxHealth);
            
            if (verboseLogging)
                Debug.Log($"ExperienceComponent: Health increased to {newMaxHealth}");
        }
        
        // Apply speed bonuses
        if (playerController != null)
        {
            playerController.MoveSpeed = initialMoveSpeed + (speedBonusPerLevel * (currentLevel - 1));
            playerController.SprintSpeed = initialSprintSpeed + (speedBonusPerLevel * 1.5f * (currentLevel - 1)); // Sprint gets 1.5x bonus
            
            if (verboseLogging)
                Debug.Log($"ExperienceComponent: Speed increased - Move: {playerController.MoveSpeed}, Sprint: {playerController.SprintSpeed}");
        }
        
        // Damage bonus will be applied by weapon systems checking GetDamageMultiplier()
    }

    /// <summary>
    /// Get the current damage multiplier based on level
    /// </summary>
    public float GetDamageMultiplier()
    {
        return 1f + (damageBonusPerLevel * (CurrentLevel - 1));
    }

    /// <summary>
    /// Calculate XP required for a specific level
    /// </summary>
    private float CalculateXPRequiredForLevel(int level)
    {
        if (level <= 1) return 0f;
        
        return baseXPRequirement * Mathf.Pow(xpRequirementMultiplier, level - 2);
    }

    /// <summary>
    /// Collect nearby XP orbs (called by XP orbs when in range)
    /// </summary>
    public void CollectXPOrb(float xpAmount, Vector3 orbPosition)
    {
        if (!IsOwner) return; // Only owner can collect XP
        
        float distanceToOrb = Vector3.Distance(transform.position, orbPosition);
        if (distanceToOrb <= xpCollectionRange)
        {
            // Request XP addition from server
            RequestXPAdditionServerRpc(xpAmount);
            
            if (verboseLogging)
                Debug.Log($"ExperienceComponent: Collected XP orb worth {xpAmount} XP");
        }
    }

    /// <summary>
    /// Server RPC to add XP (called by clients)
    /// </summary>
    [ServerRpc]
    private void RequestXPAdditionServerRpc(float amount, ServerRpcParams serverRpcParams = default)
    {
        AddXP(amount);
    }

    /// <summary>
    /// Client RPC to notify about level up
    /// </summary>
    [ClientRpc]
    private void NotifyLevelUpClientRpc(int newLevel)
    {
        if (!IsOwner) return; // Only show UI for owner
        
        // Display level up notification (UI will be implemented later)
        Debug.Log($"🎉 LEVEL UP! You are now level {newLevel}!");
    }

    // Network variable change callbacks
    private void OnNetworkXPChanged(float previousValue, float newValue)
    {
        OnXPChanged?.Invoke(newValue, networkXPToNextLevel.Value);
    }

    private void OnNetworkLevelChanged(int previousValue, int newValue)
    {
        if (previousValue != newValue && newValue > previousValue)
        {
            // Level changed - apply visual/audio effects here if needed
            if (verboseLogging)
                Debug.Log($"ExperienceComponent: Level changed from {previousValue} to {newValue}");
        }
    }

    private void OnNetworkXPRequirementChanged(float previousValue, float newValue)
    {
        OnXPChanged?.Invoke(networkCurrentXP.Value, newValue);
    }

    // Debug and utility methods
    [ContextMenu("Add 50 XP (Debug)")]
    private void DebugAdd50XP()
    {
        if (IsServer)
        {
            AddXP(50f);
        }
        else
        {
            Debug.LogWarning("ExperienceComponent: Debug XP can only be added on server");
        }
    }

    [ContextMenu("Level Up (Debug)")]
    private void DebugLevelUp()
    {
        if (IsServer)
        {
            AddXP(networkXPToNextLevel.Value);
        }
        else
        {
            Debug.LogWarning("ExperienceComponent: Debug level up can only be triggered on server");
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;
        
        // XP collection range
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, xpCollectionRange);
    }

    /// <summary>
    /// Get debug information about current state
    /// </summary>
    public string GetDebugInfo()
    {
        return $"Level: {CurrentLevel}/{maxLevel} | XP: {CurrentXP:F1}/{XPToNextLevel:F1} | " +
               $"Progress: {(XPProgress * 100):F1}% | Damage Multiplier: x{GetDamageMultiplier():F2}";
    }
}