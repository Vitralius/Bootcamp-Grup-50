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
    [SerializeField] private bool keepDebugOnDeath = true;
    
    [Header("Audio Settings")]
    [SerializeField] private AudioClip[] hurtSounds;
    [SerializeField] private AudioClip[] deathSounds;
    [SerializeField] private AudioClip[] healSounds;
    [SerializeField] private float audioVolume = 1.0f;
    [SerializeField] private bool enableAudio = true;
    
    [Header("Screen Shake Settings")]
    [SerializeField] private bool enableScreenShake = true;
    [SerializeField] private float damageShakeIntensity = 1.0f;
    [SerializeField] private float deathShakeIntensity = 2.0f;
    [SerializeField] private float healShakeIntensity = 0.5f;
    
    [Header("Shake Range Settings")]
    [SerializeField] private float minShakeRange = 0.3f;
    [SerializeField] private float maxShakeRange = 1.0f;
    
    private NetworkVariable<float> currentHealth = new NetworkVariable<float>(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> isAlive = new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    private float lastDamageTime;
    private MonoBehaviour[] componentsToDisable;
    private Collider[] collidersToDisable;
    private AudioSource audioSource;
    private ScreenShakeManager screenShakeManager;
    
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
        
        // Setup audio and screen shake
        SetupAudioAndEffects();
        
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
        
        // Add ThirdPersonController if it exists (unless keepDebugOnDeath is true)
        var thirdPersonController = GetComponent<StarterAssets.ThirdPersonController>();
        if (thirdPersonController != null && !keepDebugOnDeath)
            componentsList.Add(thirdPersonController);
        
        // Add all colliders
        collidersList.AddRange(GetComponents<Collider>());
        
        componentsToDisable = componentsList.ToArray();
        collidersToDisable = collidersList.ToArray();
    }
    
    private void SetupAudioAndEffects()
    {
        // Setup audio source
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        ConfigureAudioSource();
        
        // Setup screen shake manager
        screenShakeManager = GetComponent<ScreenShakeManager>();
        if (screenShakeManager == null)
        {
            screenShakeManager = gameObject.AddComponent<ScreenShakeManager>();
        }
        
        // Sync settings
        screenShakeManager.SetScreenShakeEnabled(enableScreenShake);
        screenShakeManager.SetShakeRange(minShakeRange, maxShakeRange);
    }
    
    private void ConfigureAudioSource()
    {
        audioSource.volume = audioVolume;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1.0f; // 3D sound
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.maxDistance = 50f;
        audioSource.minDistance = 1f;
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
        Debug.Log($"[CLIENT] {gameObject.name} took {damage} damage from {attackerId}");
        
        // Play hurt sound
        if (enableAudio && hurtSounds.Length > 0)
        {
            int randomIndex = UnityEngine.Random.Range(0, hurtSounds.Length);
            PlayAudioClip(hurtSounds[randomIndex]);
        }
        
        // Trigger screen shake (only for the owner/local player)
        if (IsOwner && screenShakeManager != null)
        {
            screenShakeManager.TriggerDamageShake(damage, damageShakeIntensity);
        }
    }
    
    [ClientRpc]
    private void ShowHealEffectClientRpc(float healAmount)
    {
        Debug.Log($"[CLIENT] {gameObject.name} healed for {healAmount}");
        
        // Play heal sound
        if (enableAudio && healSounds.Length > 0)
        {
            int randomIndex = UnityEngine.Random.Range(0, healSounds.Length);
            PlayAudioClip(healSounds[randomIndex]);
        }
        
        // Trigger heal screen shake (only for the owner/local player)
        if (IsOwner && screenShakeManager != null)
        {
            screenShakeManager.TriggerHealShake(healAmount, healShakeIntensity);
        }
    }
    
    private void HandleDeath()
    {
        if (!IsServer) return;
        
        isAlive.Value = false;
        Debug.Log($"[SERVER] {gameObject.name} died!");
        
        // Play death effects on all clients
        ShowDeathEffectClientRpc();
        
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
    private void ShowDeathEffectClientRpc()
    {
        Debug.Log($"[CLIENT] {gameObject.name} died!");
        
        // Play death sound
        if (enableAudio && deathSounds.Length > 0)
        {
            int randomIndex = UnityEngine.Random.Range(0, deathSounds.Length);
            PlayAudioClip(deathSounds[randomIndex]);
        }
        
        // Trigger death screen shake (only for the owner/local player)
        if (IsOwner && screenShakeManager != null)
        {
            screenShakeManager.TriggerDeathShake(deathShakeIntensity);
        }
    }
    
    [ClientRpc]
    private void DisableObjectClientRpc()
    {
        Debug.Log($"[CLIENT] {gameObject.name} disabled due to death");
        
        // TODO: Hide object, show death effect // or do it in the event in specific object
        
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
        
        
        // TODO: Show object, play respawn effect // or do it in the event in specific object
        
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
    
    private void PlayAudioClip(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip, audioVolume);
        }
    }
    
    public void SetAudioEnabled(bool enabled)
    {
        enableAudio = enabled;
    }
    
    public void SetAudioVolume(float volume)
    {
        audioVolume = Mathf.Clamp01(volume);
        if (audioSource != null)
        {
            audioSource.volume = audioVolume;
        }
    }
    
    public void SetScreenShakeEnabled(bool enabled)
    {
        enableScreenShake = enabled;
        if (screenShakeManager != null)
        {
            screenShakeManager.SetScreenShakeEnabled(enabled);
        }
    }
    
    public bool IsAudioEnabled()
    {
        return enableAudio;
    }
    
    public bool IsScreenShakeEnabled()
    {
        return enableScreenShake;
    }
    
    public void SetDamageShakeIntensity(float intensity)
    {
        damageShakeIntensity = Mathf.Max(0f, intensity);
    }
    
    public void SetDeathShakeIntensity(float intensity)
    {
        deathShakeIntensity = Mathf.Max(0f, intensity);
    }
    
    public void SetHealShakeIntensity(float intensity)
    {
        healShakeIntensity = Mathf.Max(0f, intensity);
    }
    
    public float GetDamageShakeIntensity()
    {
        return damageShakeIntensity;
    }
    
    public float GetDeathShakeIntensity()
    {
        return deathShakeIntensity;
    }
    
    public float GetHealShakeIntensity()
    {
        return healShakeIntensity;
    }
    
    public void SetShakeRange(float minRange, float maxRange)
    {
        minShakeRange = Mathf.Max(0f, minRange);
        maxShakeRange = Mathf.Max(minShakeRange, maxRange);
        
        if (screenShakeManager != null)
        {
            screenShakeManager.SetShakeRange(minShakeRange, maxShakeRange);
        }
    }
    
    public float GetMinShakeRange()
    {
        return minShakeRange;
    }
    
    public float GetMaxShakeRange()
    {
        return maxShakeRange;
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