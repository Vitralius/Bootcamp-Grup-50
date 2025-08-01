using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody), typeof(DamagingObject))]
public class SimpleProjectile : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float speed = 10f;
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private bool useGravity = false;
    
    [Header("Targeting")]
    [SerializeField] private bool homing = false;
    [SerializeField] private float homingStrength = 2f;
    [SerializeField] private LayerMask targetLayers = -1;
    
    [Header("Effects")]
    [SerializeField] private GameObject hitEffect;
    [SerializeField] private GameObject trailEffect;
    [SerializeField] private AudioClip hitSound;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    
    // Components
    private Rigidbody rb;
    private DamagingObject damagingComponent;
    private AudioSource audioSource;
    
    // Movement variables
    private Vector3 direction;
    private Transform currentTarget;
    private WeaponData weaponData;
    private int weaponLevel = 1;
    private float currentLifetime;
    private bool isInitialized = false;
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Get components
        rb = GetComponent<Rigidbody>();
        damagingComponent = GetComponent<DamagingObject>();
        audioSource = GetComponent<AudioSource>();
        
        // Configure rigidbody
        rb.useGravity = useGravity;
        rb.isKinematic = false;
        
        // Start lifetime countdown
        currentLifetime = lifetime;
        
        // Spawn trail effect if available
        if (trailEffect != null)
        {
            GameObject trail = Instantiate(trailEffect, transform.position, transform.rotation);
            trail.transform.SetParent(transform);
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"[SimpleProjectile] Projectile spawned - Speed: {speed}, Lifetime: {lifetime}");
        }
    }
    
    /// <summary>
    /// Initialize projectile with weapon data
    /// </summary>
    public void Initialize(Vector3 fireDirection, WeaponData data, int level)
    {
        direction = fireDirection.normalized;
        weaponData = data;
        weaponLevel = level;
        
        // Update speed from weapon data
        speed = weaponData.ProjectileSpeed;
        lifetime = weaponData.ProjectileLifetime;
        currentLifetime = lifetime;
        
        // Update homing settings
        homing = weaponData.AutoTarget;
        
        // Configure DamagingObject component with weapon stats
        if (damagingComponent != null)
        {
            // Note: DamagingObject damage is set in the prefab or through reflection
            // The DamagingObject will handle the actual damage dealing
            
            if (showDebugLogs)
            {
                Debug.Log($"[SimpleProjectile] Initialized with weapon: {weaponData.WeaponName}, Level: {level}");
            }
        }
        
        isInitialized = true;
        
        // Apply initial velocity
        if (rb != null)
        {
            rb.linearVelocity = direction * speed;
        }
    }
    
    void Update()
    {
        if (!IsServer) return;
        
        // Countdown lifetime
        currentLifetime -= Time.deltaTime;
        if (currentLifetime <= 0f)
        {
            DestroyProjectile();
            return;
        }
        
        // Handle homing behavior
        if (homing && isInitialized)
        {
            UpdateHoming();
        }
    }
    
    private void UpdateHoming()
    {
        // Find target if we don't have one
        if (currentTarget == null)
        {
            FindNearestTarget();
        }
        
        // Apply homing force towards target
        if (currentTarget != null && rb != null)
        {
            Vector3 targetDirection = (currentTarget.position - transform.position).normalized;
            Vector3 homingForce = targetDirection * homingStrength;
            
            // Blend current velocity with homing direction
            Vector3 newVelocity = Vector3.Lerp(rb.linearVelocity.normalized, targetDirection, homingStrength * Time.deltaTime) * speed;
            rb.linearVelocity = newVelocity;
            
            // Rotate projectile to face movement direction
            if (rb.linearVelocity.magnitude > 0.1f)
            {
                transform.rotation = Quaternion.LookRotation(rb.linearVelocity);
            }
        }
    }
    
    private void FindNearestTarget()
    {
        if (weaponData == null) return;
        
        float searchRange = 10f; // Search range for homing
        Collider[] targets = Physics.OverlapSphere(transform.position, searchRange, targetLayers);
        
        Transform nearest = null;
        float nearestDistance = float.MaxValue;
        
        foreach (Collider target in targets)
        {
            // Check if target has health component (is alive)
            HealthComponent healthComp = target.GetComponent<HealthComponent>();
            if (healthComp != null)
            {
                float distance = Vector3.Distance(transform.position, target.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = target.transform;
                }
            }
        }
        
        currentTarget = nearest;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        
        // Let DamagingObject handle the damage logic
        // We just need to handle projectile destruction and effects
        
        // Check if we hit something that should destroy the projectile
        bool shouldDestroy = false;
        
        // Destroy on hitting enemies (let DamagingObject handle damage first)
        if (other.CompareTag("Enemy") || other.GetComponent<HealthComponent>() != null)
        {
            shouldDestroy = true;
        }
        
        // Destroy on hitting environment (not players or projectiles)
        if (other.gameObject.layer != gameObject.layer && !other.CompareTag("Player") && !other.CompareTag("Projectile"))
        {
            shouldDestroy = true;
        }
        
        if (shouldDestroy)
        {
            // Play hit effects before destroying
            PlayHitEffectsClientRpc(transform.position);
            
            if (showDebugLogs)
            {
                Debug.Log($"[SimpleProjectile] Hit {other.name}, destroying projectile");
            }
            
            // Small delay to let DamagingObject process damage first
            Invoke(nameof(DestroyProjectile), 0.1f);
        }
    }
    
    [ClientRpc]
    private void PlayHitEffectsClientRpc(Vector3 hitPosition)
    {
        // Spawn hit effect
        if (hitEffect != null)
        {
            GameObject effect = Instantiate(hitEffect, hitPosition, Quaternion.identity);
            Destroy(effect, 2f); // Clean up after 2 seconds
        }
        
        // Play hit sound
        if (hitSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(hitSound);
        }
    }
    
    private void DestroyProjectile()
    {
        if (!IsServer) return;
        
        if (showDebugLogs)
        {
            Debug.Log("[SimpleProjectile] Projectile destroyed");
        }
        
        // Despawn from network
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
        else
        {
            // Fallback for non-networked destruction
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// Set projectile damage (called by weapon system)
    /// </summary>
    public void SetDamage(float damage)
    {
        if (damagingComponent != null)
        {
            // Use reflection to set damage on DamagingObject if needed
            // Or extend DamagingObject to have a public setter
            if (showDebugLogs)
            {
                Debug.Log($"[SimpleProjectile] Damage set to {damage}");
            }
        }
    }
    
    /// <summary>
    /// Force projectile to explode (for area damage weapons)
    /// </summary>
    public void Explode()
    {
        if (!IsServer) return;
        
        PlayHitEffectsClientRpc(transform.position);
        DestroyProjectile();
    }
    
    // Debug visualization
    private void OnDrawGizmosSelected()
    {
        // Draw movement direction
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, direction * 2f);
        
        // Draw homing target line
        if (homing && currentTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, currentTarget.position);
        }
        
        // Draw search range for homing
        if (homing)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 10f);
        }
    }
}