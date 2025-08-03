using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class AutoWeapon : NetworkBehaviour
{
    [Header("Weapon Configuration")]
    [SerializeField] private WeaponData weaponData;
    [SerializeField] private Transform firePoint; // Where projectiles spawn
    [SerializeField] private LayerMask enemyLayers = -1; // What to target
    
    [Header("Current Stats")]
    [SerializeField] private int currentLevel = 1;
    [SerializeField] private bool isActive = true;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    
    // Private variables
    private float fireTimer = 0f;
    private Transform nearestTarget;
    private Camera playerCamera;
    private AudioSource audioSource;
    
    // Network variables for multiplayer
    private NetworkVariable<bool> isWeaponActive = new NetworkVariable<bool>(true);
    private NetworkVariable<int> weaponLevel = new NetworkVariable<int>(1);
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Initialize weapon level
        if (IsServer)
        {
            weaponLevel.Value = currentLevel;
            isWeaponActive.Value = isActive;
        }
        
        // Set up audio source
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Find player camera if not assigned
        if (playerCamera == null && IsOwner)
        {
            playerCamera = Camera.main;
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"[AutoWeapon] {weaponData.WeaponName} initialized - Level {currentLevel}");
        }
    }
    
    void Update()
    {
        // Only process on owner client for input responsiveness
        if (!IsOwner || !isWeaponActive.Value || weaponData == null) return;
        
        // Update fire timer
        fireTimer += Time.deltaTime;
        
        // Check if we can fire
        float fireInterval = weaponData.GetFireIntervalAtLevel(weaponLevel.Value);
        if (fireTimer >= fireInterval)
        {
            TryFire();
            fireTimer = 0f;
        }
    }
    
    private void TryFire()
    {
        // Find nearest target
        Transform target = FindNearestTarget();
        
        if (target != null)
        {
            Vector3 fireDirection = GetFireDirection(target);
            
            if (IsServer)
            {
                FireProjectile(fireDirection);
            }
            else
            {
                FireProjectileServerRpc(fireDirection);
            }
        }
    }
    
    private Transform FindNearestTarget()
    {
        float detectionRange = weaponData.GetRangeAtLevel(weaponLevel.Value);
        Collider[] enemies = Physics.OverlapSphere(transform.position, detectionRange, enemyLayers);
        
        Transform nearest = null;
        float nearestDistance = float.MaxValue;
        
        foreach (Collider enemy in enemies)
        {
            // Check if enemy has health component (is alive)
            if (enemy.GetComponent<HealthComponent>() != null)
            {
                float distance = Vector3.Distance(transform.position, enemy.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = enemy.transform;
                }
            }
        }
        
        return nearest;
    }
    
    private Vector3 GetFireDirection(Transform target)
    {
        if (weaponData.AutoTarget && target != null)
        {
            // Auto-target mode: aim at target
            return (target.position - firePoint.position).normalized;
        }
        else
        {
            // Manual aiming mode: use camera direction or character forward
            if (playerCamera != null)
            {
                return playerCamera.transform.forward;
            }
            else
            {
                return transform.forward;
            }
        }
    }
    
    [ServerRpc]
    private void FireProjectileServerRpc(Vector3 direction)
    {
        FireProjectile(direction);
    }
    
    private void FireProjectile(Vector3 baseDirection)
    {
        if (!IsServer) return;
        
        int projectileCount = weaponData.GetProjectileCountAtLevel(weaponLevel.Value);
        float spreadAngle = weaponData.SpreadAngle;
        
        for (int i = 0; i < projectileCount; i++)
        {
            Vector3 fireDirection = baseDirection;
            
            // Apply spread for multiple projectiles
            if (projectileCount > 1)
            {
                float angleStep = spreadAngle / (projectileCount - 1);
                float currentAngle = -spreadAngle / 2f + (angleStep * i);
                fireDirection = Quaternion.AngleAxis(currentAngle, Vector3.up) * baseDirection;
            }
            
            // Spawn projectile
            GameObject projectile = Instantiate(weaponData.ProjectilePrefab, firePoint.position, Quaternion.LookRotation(fireDirection));
            
            // Configure projectile
            SimpleProjectile projectileScript = projectile.GetComponent<SimpleProjectile>();
            if (projectileScript != null)
            {
                projectileScript.Initialize(fireDirection, weaponData, weaponLevel.Value);
            }
            
            // Configure DamagingObject component
            DamagingObject damagingComponent = projectile.GetComponent<DamagingObject>();
            if (damagingComponent != null)
            {
                // Apply experience-based damage multiplier
                float baseDamage = weaponData.GetDamageAtLevel(weaponLevel.Value);
                float damageMultiplier = GetDamageMultiplier();
                float finalDamage = baseDamage * damageMultiplier;
                
                damagingComponent.damage = finalDamage;
                
                if (showDebugLogs)
                {
                    Debug.Log($"[AutoWeapon] Projectile damage: {baseDamage} × {damageMultiplier:F2} = {finalDamage:F1}");
                }
            }
            
            // Spawn projectile on network
            NetworkObject networkObj = projectile.GetComponent<NetworkObject>();
            if (networkObj != null)
            {
                networkObj.Spawn();
            }
        }
        
        // Play visual and audio effects
        PlayFireEffectsClientRpc();
        
        if (showDebugLogs)
        {
            Debug.Log($"[AutoWeapon] Fired {projectileCount} projectiles from {weaponData.WeaponName}");
        }
    }
    
    [ClientRpc]
    private void PlayFireEffectsClientRpc()
    {
        // Play muzzle flash effect
        if (weaponData.MuzzleFlashEffect != null)
        {
            GameObject effect = Instantiate(weaponData.MuzzleFlashEffect, firePoint.position, firePoint.rotation);
            Destroy(effect, 1f); // Clean up effect after 1 second
        }
        
        // Play fire sound
        if (weaponData.FireSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(weaponData.FireSound, weaponData.SoundVolume);
        }
    }
    
    /// <summary>
    /// Upgrade weapon to next level
    /// </summary>
    public void UpgradeWeapon()
    {
        if (!IsServer) return;
        
        if (weaponData.CanUpgrade(weaponLevel.Value))
        {
            weaponLevel.Value++;
            
            if (showDebugLogs)
            {
                Debug.Log($"[AutoWeapon] {weaponData.WeaponName} upgraded to level {weaponLevel.Value}");
            }
        }
    }
    
    /// <summary>
    /// Set weapon data for this auto weapon
    /// </summary>
    public void SetWeaponData(WeaponData newWeaponData)
    {
        if (newWeaponData == null)
        {
            Debug.LogWarning("AutoWeapon: Cannot set null weapon data");
            return;
        }
        
        weaponData = newWeaponData;
        if (showDebugLogs)
        {
            Debug.Log($"AutoWeapon: Set weapon data to '{weaponData.WeaponName}'");
        }
    }
    
    /// <summary>
    /// Get damage multiplier from ExperienceComponent (if available)
    /// </summary>
    private float GetDamageMultiplier()
    {
        ExperienceComponent expComponent = GetComponent<ExperienceComponent>();
        if (expComponent != null)
        {
            return expComponent.GetDamageMultiplier();
        }
        
        return 1f; // Default multiplier if no experience component
    }
    
    /// <summary>
    /// Set weapon active/inactive state
    /// </summary>
    public void SetWeaponActive(bool active)
    {
        if (IsServer)
        {
            isWeaponActive.Value = active;
        }
        else
        {
            SetWeaponActiveServerRpc(active);
        }
    }
    
    [ServerRpc]
    private void SetWeaponActiveServerRpc(bool active)
    {
        isWeaponActive.Value = active;
    }
    
    /// <summary>
    /// Get current weapon stats for UI display
    /// </summary>
    public WeaponStats GetCurrentStats()
    {
        return new WeaponStats
        {
            damage = weaponData.GetDamageAtLevel(weaponLevel.Value),
            fireRate = weaponData.GetFireRateAtLevel(weaponLevel.Value),
            range = weaponData.GetRangeAtLevel(weaponLevel.Value),
            level = weaponLevel.Value,
            maxLevel = weaponData.MaxLevel
        };
    }
    
    // Helper struct for weapon stats
    [System.Serializable]
    public struct WeaponStats
    {
        public float damage;
        public float fireRate;
        public float range;
        public int level;
        public int maxLevel;
    }
    
    // Debug visualization
    private void OnDrawGizmosSelected()
    {
        if (weaponData == null) return;
        
        // Draw weapon range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, weaponData.GetRangeAtLevel(currentLevel));
        
        // Draw fire direction
        if (firePoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(firePoint.position, firePoint.forward * 2f);
        }
    }
}