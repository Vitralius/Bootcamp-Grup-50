using Unity.Netcode;
using UnityEngine;

/// <summary>
/// XPOrb - Collectible experience orb that spawns when enemies die
/// Automatically moves toward players and grants XP when collected
/// </summary>
public class XPOrb : NetworkBehaviour
{
    [Header("XP Orb Settings")]
    [SerializeField] private float xpValue = 10f;
    [SerializeField] private float attractionRange = 5f;
    [SerializeField] private float attractionSpeed = 8f;
    [SerializeField] private float collectionRange = 1f;
    [SerializeField] private float lifetime = 30f; // Orb despawns after 30 seconds
    
    [Header("Movement")]
    [SerializeField] private float floatHeight = 0.5f;
    [SerializeField] private float floatSpeed = 2f;
    [SerializeField] private float randomMovement = 0.5f;
    
    [Header("Visual")]
    [SerializeField] private GameObject visualEffect;
    [SerializeField] private bool useBuiltInVisual = true;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    
    // State
    private Transform targetPlayer;
    private bool isBeingAttracted = false;
    private bool isCollected = false;
    private float spawnTime;
    private Vector3 originalPosition;
    private Vector3 randomOffset;
    
    // Components
    private Rigidbody rb;
    private Collider orbCollider;
    
    // Network sync
    private NetworkVariable<bool> networkIsCollected = new NetworkVariable<bool>(false);

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Subscribe to network variable changes
        networkIsCollected.OnValueChanged += OnNetworkCollectedChanged;
        
        // Initialize orb
        InitializeOrb();
        
        // Set spawn time
        spawnTime = Time.time;
        
        // Only server handles logic
        if (IsServer)
        {
            // Start lifetime countdown
            Invoke(nameof(DespawnOrb), lifetime);
        }
    }
    
    public override void OnNetworkDespawn()
    {
        // Unsubscribe from network variable changes
        if (networkIsCollected != null)
            networkIsCollected.OnValueChanged -= OnNetworkCollectedChanged;
            
        base.OnNetworkDespawn();
    }

    private void InitializeOrb()
    {
        // Store original position
        originalPosition = transform.position;
        
        // Generate random offset for floating movement
        randomOffset = new Vector3(
            Random.Range(-randomMovement, randomMovement),
            0f,
            Random.Range(-randomMovement, randomMovement)
        );
        
        // Get or add components
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        
        // Configure Rigidbody for floating movement
        rb.useGravity = false;
        rb.isKinematic = false;
        rb.linearDamping = 5f; // Add some drag for smooth movement
        
        // Get collider
        orbCollider = GetComponent<Collider>();
        if (orbCollider == null)
        {
            // Add a sphere collider if none exists
            SphereCollider sphereCol = gameObject.AddComponent<SphereCollider>();
            sphereCol.radius = collectionRange;
            sphereCol.isTrigger = true;
            orbCollider = sphereCol;
        }
        else
        {
            orbCollider.isTrigger = true;
        }
        
        // Create built-in visual if needed
        if (useBuiltInVisual && visualEffect == null)
        {
            CreateBuiltInVisual();
        }
        
        Debug.Log($"XPOrb: Initialized orb worth {xpValue} XP at {transform.position}");
    }

    private void CreateBuiltInVisual()
    {
        // Create a simple glowing sphere as visual
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.transform.SetParent(transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = Vector3.one * 0.3f; // Small sphere
        
        // Remove the collider from visual (we have our own)
        Collider visualCollider = visual.GetComponent<Collider>();
        if (visualCollider != null)
            DestroyImmediate(visualCollider);
        
        // Make it glow (basic material)
        Renderer renderer = visual.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material glowMat = new Material(Shader.Find("Standard"));
            glowMat.color = Color.cyan;
            glowMat.SetFloat("_Metallic", 0f);
            glowMat.SetFloat("_Glossiness", 0.8f);
            renderer.material = glowMat;
        }
        
        visualEffect = visual;
        
        // Add simple rotation animation
        XPOrbRotator rotator = visual.AddComponent<XPOrbRotator>();
    }

    void Update()
    {
        if (!IsServer || isCollected) return;
        
        // Check for nearby players
        FindNearestPlayer();
        
        // Update movement
        UpdateMovement();
        
        // Check for collection
        CheckForCollection();
    }

    private void FindNearestPlayer()
    {
        GameObject nearestPlayer = null;
        float nearestDistance = float.MaxValue;
        
        // Find all players with ExperienceComponent
        ExperienceComponent[] experienceComponents = FindObjectsByType<ExperienceComponent>(FindObjectsSortMode.None);
        
        foreach (ExperienceComponent expComp in experienceComponents)
        {
            if (expComp != null && expComp.IsOwner) // Only consider player owners
            {
                float distance = Vector3.Distance(transform.position, expComp.transform.position);
                if (distance < nearestDistance && distance <= attractionRange)
                {
                    nearestDistance = distance;
                    nearestPlayer = expComp.gameObject;
                }
            }
        }
        
        // Update target and attraction state
        if (nearestPlayer != null)
        {
            targetPlayer = nearestPlayer.transform;
            isBeingAttracted = true;
        }
        else
        {
            targetPlayer = null;
            isBeingAttracted = false;
        }
    }

    private void UpdateMovement()
    {
        if (isBeingAttracted && targetPlayer != null)
        {
            // Move toward player
            Vector3 direction = (targetPlayer.position - transform.position).normalized;
            Vector3 targetVelocity = direction * attractionSpeed;
            
            // Smooth velocity transition
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVelocity, Time.deltaTime * 5f);
        }
        else
        {
            // Floating movement
            float floatY = originalPosition.y + floatHeight + Mathf.Sin(Time.time * floatSpeed) * 0.2f;
            Vector3 targetPosition = new Vector3(
                originalPosition.x + randomOffset.x * Mathf.Sin(Time.time * 0.7f),
                floatY,
                originalPosition.z + randomOffset.z * Mathf.Cos(Time.time * 0.7f)
            );
            
            Vector3 direction = (targetPosition - transform.position);
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, direction * 2f, Time.deltaTime * 3f);
        }
    }

    private void CheckForCollection()
    {
        if (targetPlayer != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, targetPlayer.position);
            if (distanceToPlayer <= collectionRange)
            {
                CollectOrb();
            }
        }
    }

    private void CollectOrb()
    {
        if (isCollected) return;
        
        // Get experience component
        ExperienceComponent expComp = targetPlayer.GetComponent<ExperienceComponent>();
        if (expComp != null)
        {
            // Add XP to player
            expComp.AddXP(xpValue);
            
            Debug.Log($"XPOrb: Collected by {targetPlayer.name} for {xpValue} XP");
            
            // Mark as collected
            isCollected = true;
            networkIsCollected.Value = true;
            
            // Play collection effect
            PlayCollectionEffectClientRpc();
            
            // Despawn orb
            Invoke(nameof(DespawnOrb), 0.1f); // Small delay for effect
        }
    }

    /// <summary>
    /// Despawn the orb (server only)
    /// </summary>
    private void DespawnOrb()
    {
        if (!IsServer) return;
        
        Debug.Log($"XPOrb: Despawning orb (collected: {isCollected})");
        
        // Despawn from network
        GetComponent<NetworkObject>().Despawn();
    }

    /// <summary>
    /// Client RPC to play collection effect
    /// </summary>
    [ClientRpc]
    private void PlayCollectionEffectClientRpc()
    {
        // Play visual/audio collection effect
        if (visualEffect != null)
        {
            // Simple scale-down effect
            StartCoroutine(PlayCollectionAnimation());
        }
        
        // Play sound (if audio source exists)
        AudioSource audioSource = GetComponent<AudioSource>();
        if (audioSource != null && audioSource.clip != null)
        {
            audioSource.Play();
        }
        
        Debug.Log($"XPOrb: Playing collection effect");
    }

    private System.Collections.IEnumerator PlayCollectionAnimation()
    {
        Vector3 originalScale = visualEffect.transform.localScale;
        Vector3 targetScale = originalScale * 2f; // Scale up first
        
        // Scale up quickly
        float timer = 0f;
        while (timer < 0.1f)
        {
            timer += Time.deltaTime;
            float progress = timer / 0.1f;
            visualEffect.transform.localScale = Vector3.Lerp(originalScale, targetScale, progress);
            yield return null;
        }
        
        // Scale down to zero
        timer = 0f;
        while (timer < 0.1f)
        {
            timer += Time.deltaTime;
            float progress = timer / 0.1f;
            visualEffect.transform.localScale = Vector3.Lerp(targetScale, Vector3.zero, progress);
            yield return null;
        }
    }

    // Network variable change callback
    private void OnNetworkCollectedChanged(bool previousValue, bool newValue)
    {
        if (newValue && !previousValue)
        {
            isCollected = true;
            
            // Disable visual on all clients
            if (visualEffect != null)
                visualEffect.SetActive(false);
        }
    }

    // Trigger-based collection (backup method)
    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || isCollected) return;
        
        ExperienceComponent expComp = other.GetComponent<ExperienceComponent>();
        if (expComp != null && expComp.IsOwner)
        {
            CollectOrb();
        }
    }

    // Public methods for external control
    public void SetXPValue(float newXPValue)
    {
        xpValue = newXPValue;
    }

    public float GetXPValue()
    {
        return xpValue;
    }

    // Debug visualization
    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;
        
        // Attraction range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attractionRange);
        
        // Collection range
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, collectionRange);
        
        // Line to target player
        if (targetPlayer != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, targetPlayer.position);
        }
    }
}

/// <summary>
/// Simple component to rotate XP orb visual
/// </summary>
public class XPOrbRotator : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 45f;
    
    void Update()
    {
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
    }
}