using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using StarterAssets;

/// <summary>
/// Enhanced Enemy AI for survivorlike gameplay
/// Combines original patrol behavior with chase behavior for player hunting
/// </summary>
public class Enemy : NetworkBehaviour
{
    [Header("Enemy Behavior")]
    [SerializeField] private EnemyBehaviorType behaviorType = EnemyBehaviorType.ChasePlayer;
    [SerializeField] private float detectionRange = 10f;
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private float attackCooldown = 1.5f;
    
    [Header("Patrol Settings (Legacy)")]
    [SerializeField] private GameObject patrolPoints;
    
    [Header("Combat")]
    [SerializeField] private LayerMask playerLayerMask = 1 << 6; // Default Player layer
    [SerializeField] private float xpReward = 15f;
    [SerializeField] private GameObject xpOrbPrefab;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    
    // Components
    private NavMeshAgent navMeshAgent;
    private HealthComponent healthComponent;
    private DamagingObject damagingObject;
    
    // Chase behavior
    private Transform targetPlayer;
    private float lastAttackTime;
    private bool isPlayerInRange;
    
    // Legacy patrol behavior
    private GameObject[] patrolPoints_array;
    private int patrolCounter;
    private int targetPatrolPoint;
    private bool isArrivedAtPatrol;
    
    // Network sync
    private NetworkVariable<Vector3> networkPosition = new NetworkVariable<Vector3>();
    private NetworkVariable<bool> networkIsActive = new NetworkVariable<bool>(true);

    public enum EnemyBehaviorType
    {
        ChasePlayer,
        Patrol,
        Hybrid // Chase when player in range, patrol otherwise
    }

    void Start()
    {
        InitializeComponents();
        InitializeBehavior();
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Subscribe to network variable changes
        networkPosition.OnValueChanged += OnNetworkPositionChanged;
        networkIsActive.OnValueChanged += OnNetworkActiveChanged;
        
        // Initialize network position
        if (IsServer)
        {
            networkPosition.Value = transform.position;
        }
    }
    
    public override void OnNetworkDespawn()
    {
        // Unsubscribe from network variable changes
        if (networkPosition != null)
            networkPosition.OnValueChanged -= OnNetworkPositionChanged;
        if (networkIsActive != null)
            networkIsActive.OnValueChanged -= OnNetworkActiveChanged;
            
        base.OnNetworkDespawn();
    }

    private void InitializeComponents()
    {
        // Get NavMeshAgent
        navMeshAgent = GetComponent<NavMeshAgent>();
        if (navMeshAgent == null)
        {
            navMeshAgent = gameObject.AddComponent<NavMeshAgent>();
        }
        navMeshAgent.speed = moveSpeed;
        
        // Get or add HealthComponent
        healthComponent = GetComponent<HealthComponent>();
        if (healthComponent == null)
        {
            healthComponent = gameObject.AddComponent<HealthComponent>();
            // HealthComponent will handle its own initialization
        }
        
        // Get or add DamagingObject for collision damage
        damagingObject = GetComponent<DamagingObject>();
        if (damagingObject == null)
        {
            damagingObject = gameObject.AddComponent<DamagingObject>();
            // Configure DamagingObject to damage players only and deal damage once per contact
        }
        
        // Subscribe to death events
        if (healthComponent != null)
        {
            healthComponent.OnDied += OnEnemyDeath;
        }
    }

    private void InitializeBehavior()
    {
        switch (behaviorType)
        {
            case EnemyBehaviorType.Patrol:
            case EnemyBehaviorType.Hybrid:
                InitializePatrolBehavior();
                break;
        }
    }

    private void InitializePatrolBehavior()
    {
        if (patrolPoints != null)
        {
            isArrivedAtPatrol = true;
            patrolCounter = patrolPoints.transform.childCount;
            patrolPoints_array = new GameObject[patrolCounter];
            SetPatrolPoints();
            targetPatrolPoint = NextPatrolPoint();
        }
    }

    void Update()
    {
        if (!IsServer) return; // Only server controls enemy behavior
        
        switch (behaviorType)
        {
            case EnemyBehaviorType.ChasePlayer:
                UpdateChasePlayerBehavior();
                break;
            case EnemyBehaviorType.Patrol:
                UpdatePatrolBehavior();
                break;
            case EnemyBehaviorType.Hybrid:
                UpdateHybridBehavior();
                break;
        }
        
        // Update network position if changed significantly
        if (Vector3.Distance(transform.position, networkPosition.Value) > 0.5f)
        {
            networkPosition.Value = transform.position;
        }
    }

    private void UpdateChasePlayerBehavior()
    {
        FindNearestPlayer();
        
        if (targetPlayer != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, targetPlayer.position);
            
            if (distanceToPlayer <= detectionRange)
            {
                // Chase the player
                if (distanceToPlayer > attackRange)
                {
                    navMeshAgent.SetDestination(targetPlayer.position);
                    navMeshAgent.isStopped = false;
                    isPlayerInRange = false;
                }
                else
                {
                    // Player in attack range
                    navMeshAgent.isStopped = true;
                    isPlayerInRange = true;
                    AttackPlayer();
                }
            }
            else
            {
                // Player out of detection range
                navMeshAgent.isStopped = true;
                isPlayerInRange = false;
            }
        }
        else
        {
            // No player found
            navMeshAgent.isStopped = true;
            isPlayerInRange = false;
        }
    }

    private void UpdatePatrolBehavior()
    {
        if (patrolPoints_array == null || patrolPoints_array.Length == 0) return;
        
        if (isArrivedAtPatrol)
        {
            navMeshAgent.isStopped = true;
            patrolPoints_array[targetPatrolPoint].SetActive(false);
            targetPatrolPoint = NextPatrolPoint();
            patrolPoints_array[targetPatrolPoint].SetActive(true);
            navMeshAgent.isStopped = false;
            navMeshAgent.SetDestination(patrolPoints_array[targetPatrolPoint].transform.position);
            isArrivedAtPatrol = false;
        }
        else
        {
            isArrivedAtPatrol = CheckPatrolDistance();
        }
    }

    private void UpdateHybridBehavior()
    {
        FindNearestPlayer();
        
        if (targetPlayer != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, targetPlayer.position);
            
            if (distanceToPlayer <= detectionRange)
            {
                // Player detected - switch to chase behavior
                UpdateChasePlayerBehavior();
                return;
            }
        }
        
        // No player in range - use patrol behavior
        UpdatePatrolBehavior();
    }

    private void FindNearestPlayer()
    {
        GameObject nearestPlayer = null;
        float nearestDistance = float.MaxValue;
        
        // Find all players in scene
        ThirdPersonController[] players = FindObjectsByType<ThirdPersonController>(FindObjectsSortMode.None);
        
        foreach (ThirdPersonController player in players)
        {
            if (player != null && player.IsOwner) // Only consider player owners
            {
                float distance = Vector3.Distance(transform.position, player.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestPlayer = player.gameObject;
                }
            }
        }
        
        targetPlayer = nearestPlayer?.transform;
    }

    private void AttackPlayer()
    {
        if (Time.time - lastAttackTime >= attackCooldown)
        {
            lastAttackTime = Time.time;
            
            // DamagingObject will handle the actual damage when colliding
            // We could also implement a direct damage method here if needed
            Debug.Log($"Enemy attacking player at distance: {Vector3.Distance(transform.position, targetPlayer.position)}");
        }
    }

    private void OnEnemyDeath()
    {
        Debug.Log($"Enemy {gameObject.name} died");
        
        // Spawn XP orb
        if (IsServer)
        {
            SpawnXPOrb();
        }
        
        // Destroy enemy
        if (IsServer)
        {
            networkIsActive.Value = false;
            // Small delay before despawn to allow XP orb to spawn
            Invoke(nameof(DespawnEnemy), 0.1f);
        }
    }

    /// <summary>
    /// Spawn XP orb at enemy death location (Server only)
    /// </summary>
    private void SpawnXPOrb()
    {
        if (!IsServer) return;
        
        // Create XP orb if prefab is assigned
        if (xpOrbPrefab != null)
        {
            // Spawn slightly above enemy position
            Vector3 spawnPosition = transform.position + Vector3.up * 0.5f;
            
            GameObject orbInstance = Instantiate(xpOrbPrefab, spawnPosition, Quaternion.identity);
            
            // Set XP value
            XPOrb orbComponent = orbInstance.GetComponent<XPOrb>();
            if (orbComponent != null)
            {
                orbComponent.SetXPValue(xpReward);
            }
            
            // Spawn as NetworkObject
            NetworkObject networkObject = orbInstance.GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                networkObject.Spawn();
                Debug.Log($"Enemy: Spawned XP orb worth {xpReward} XP at {spawnPosition}");
            }
            else
            {
                Debug.LogWarning("Enemy: XP orb prefab missing NetworkObject component");
                Destroy(orbInstance);
            }
        }
        else
        {
            Debug.LogWarning("Enemy: No XP orb prefab assigned, skipping XP orb spawn");
        }
    }

    /// <summary>
    /// Despawn enemy from network (Server only)
    /// </summary>
    private void DespawnEnemy()
    {
        if (!IsServer) return;
        
        NetworkObject networkObject = GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.IsSpawned)
        {
            networkObject.Despawn();
        }
        else
        {
            // Fallback for non-networked enemies
            Destroy(gameObject);
        }
    }

    // Legacy patrol methods (preserved for backwards compatibility)
    public void SetPatrolPoints()
    {
        for (int i = 0; i < patrolCounter; i++)
        {
            patrolPoints_array[i] = patrolPoints.transform.GetChild(i).gameObject;
        }
    }
    
    public int NextPatrolPoint() { return Random.Range(0, patrolCounter); }
    public bool CheckPatrolDistance() { return Vector3.Distance(this.transform.position, patrolPoints_array[targetPatrolPoint].transform.position) < 1f; }

    // Network synchronization methods
    private void OnNetworkPositionChanged(Vector3 previousValue, Vector3 newValue)
    {
        if (!IsServer)
        {
            // Smooth movement for clients
            transform.position = Vector3.Lerp(transform.position, newValue, Time.deltaTime * 10f);
        }
    }

    private void OnNetworkActiveChanged(bool previousValue, bool newValue)
    {
        gameObject.SetActive(newValue);
    }

    // Public methods for external control
    public void SetBehaviorType(EnemyBehaviorType newBehavior)
    {
        behaviorType = newBehavior;
        InitializeBehavior();
    }

    public void SetStats(float newMoveSpeed, float newDamage, float newDetectionRange, float newAttackRange)
    {
        moveSpeed = newMoveSpeed;
        damage = newDamage;
        detectionRange = newDetectionRange;
        attackRange = newAttackRange;
        
        if (navMeshAgent != null)
            navMeshAgent.speed = moveSpeed;
        if (damagingObject != null)
            damagingObject.damage = damage;
    }

    // Debug visualization
    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;
        
        // Detection range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        
        // Attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        
        // Line to target player
        if (targetPlayer != null)
        {
            Gizmos.color = isPlayerInRange ? Color.red : Color.orange;
            Gizmos.DrawLine(transform.position, targetPlayer.position);
        }
    }
}
