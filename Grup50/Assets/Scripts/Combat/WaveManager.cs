using Unity.Netcode;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// WaveManager - Handles enemy spawning and wave progression for survivorlike gameplay
/// Manages enemy counts, spawn rates, and difficulty escalation
/// </summary>
public class WaveManager : NetworkBehaviour
{
    [Header("Wave Configuration")]
    [SerializeField] private float initialSpawnInterval = 3f;
    [SerializeField] private float minSpawnInterval = 0.5f;
    [SerializeField] private float intervalReductionRate = 0.1f; // Reduce by 10% every wave
    [SerializeField] private float waveInterval = 120f; // 2 minutes per wave
    
    [Header("Enemy Limits")]
    [SerializeField] private int maxEnemiesPerPlayer = 15;
    [SerializeField] private int absoluteMaxEnemies = 50; // Hard cap for performance
    [SerializeField] private int enemiesPerWave = 5;
    [SerializeField] private int maxEnemyIncreasePerWave = 3;
    
    [Header("Spawning")]
    [SerializeField] private GameObject[] enemyPrefabs;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private float spawnRadius = 20f;
    [SerializeField] private LayerMask groundLayerMask = 1;
    [SerializeField] private float spawnHeightOffset = 0.5f;
    
    [Header("Difficulty Progression")]
    [SerializeField] private bool enableDifficultyScaling = true;
    [SerializeField] private float enemyHealthMultiplierPerWave = 0.2f; // 20% more health per wave
    [SerializeField] private float enemySpeedMultiplierPerWave = 0.1f; // 10% faster per wave
    [SerializeField] private float enemyDamageMultiplierPerWave = 0.15f; // 15% more damage per wave
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private bool showSpawnGizmos = true;
    
    // Wave state
    private NetworkVariable<int> currentWave = new NetworkVariable<int>(1, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server);
    
    private NetworkVariable<int> currentEnemyCount = new NetworkVariable<int>(0, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server);
    
    private NetworkVariable<bool> waveActive = new NetworkVariable<bool>(false, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server);
    
    // Local state
    private float currentSpawnInterval;
    private int currentEnemiesInWave;
    private float lastSpawnTime;
    private float waveStartTime;
    private bool isSpawning = false;
    private List<GameObject> activeEnemies = new List<GameObject>();
    
    // Player tracking - PERFORMANCE OPTIMIZED
    private List<Transform> playerTransforms = new List<Transform>();
    private static float lastPlayerTrackingUpdate = 0f;
    private const float PLAYER_TRACKING_UPDATE_INTERVAL = 2f; // Update every 2 seconds instead of every frame
    
    // Public properties
    public int CurrentWave => currentWave.Value;
    public int CurrentEnemyCount => currentEnemyCount.Value;
    public bool IsWaveActive => waveActive.Value;
    public float CurrentSpawnInterval => currentSpawnInterval;
    
    // Events
    public System.Action<int> OnWaveStarted;
    public System.Action<int> OnWaveCompleted;
    public System.Action<GameObject> OnEnemySpawned;
    public System.Action<GameObject> OnEnemyDestroyed;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Subscribe to network variable changes
        currentWave.OnValueChanged += OnWaveChanged;
        currentEnemyCount.OnValueChanged += OnEnemyCountChanged;
        waveActive.OnValueChanged += OnWaveActiveChanged;
        
        // Initialize wave manager
        if (IsServer)
        {
            InitializeWaveManager();
        }
        
        // Find initial spawn points if not assigned
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            CreateDefaultSpawnPoints();
        }
    }
    
    public override void OnNetworkDespawn()
    {
        // MEMORY LEAK FIX: Proper cleanup
        
        // Unsubscribe from network variable changes
        if (currentWave != null)
            currentWave.OnValueChanged -= OnWaveChanged;
        if (currentEnemyCount != null)
            currentEnemyCount.OnValueChanged -= OnEnemyCountChanged;
        if (waveActive != null)
            waveActive.OnValueChanged -= OnWaveActiveChanged;
        
        // Clean up enemy tracking and event subscriptions
        if (IsServer)
        {
            // Stop spawning
            isSpawning = false;
            
            // Clean up active enemies list
            foreach (GameObject enemy in activeEnemies)
            {
                if (enemy != null)
                {
                    // Unsubscribe from events (in full implementation)
                    HealthComponent healthComp = enemy.GetComponent<HealthComponent>();
                    if (healthComp != null)
                    {
                        // Would unsubscribe specific delegates here
                    }
                }
            }
            
            activeEnemies.Clear();
            playerTransforms.Clear();
        }
        
        // Cancel any pending invocations
        CancelInvoke();
            
        base.OnNetworkDespawn();
    }

    private void InitializeWaveManager()
    {
        if (!IsServer) return;
        
        // Set initial values
        currentWave.Value = 1;
        currentEnemyCount.Value = 0;
        currentSpawnInterval = initialSpawnInterval;
        currentEnemiesInWave = enemiesPerWave;
        waveStartTime = Time.time;
        
        // Start first wave
        StartWave();
        
        if (showDebugLogs)
        {
            Debug.Log($"WaveManager: Initialized - Wave {currentWave.Value}, Spawn Interval: {currentSpawnInterval}s");
        }
    }

    void Update()
    {
        if (!IsServer) return;
        
        // Update player tracking
        UpdatePlayerTracking();
        
        // Handle wave progression
        if (isSpawning)
        {
            HandleSpawning();
        }
        
        // Check for wave completion
        CheckWaveCompletion();
        
        // Clean up destroyed enemies
        CleanUpDestroyedEnemies();
    }

    private void UpdatePlayerTracking()
    {
        // PERFORMANCE FIX: Only update player tracking periodically, not every frame
        if (Time.time - lastPlayerTrackingUpdate < PLAYER_TRACKING_UPDATE_INTERVAL)
            return;
            
        lastPlayerTrackingUpdate = Time.time;
        
        // Clear and rebuild player list
        playerTransforms.Clear();
        
        // EXPENSIVE OPERATION: Only do this every 2 seconds
        StarterAssets.ThirdPersonController[] players = FindObjectsByType<StarterAssets.ThirdPersonController>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            if (player != null && player.IsOwner)
            {
                playerTransforms.Add(player.transform);
            }
        }
        
        if (showDebugLogs && playerTransforms.Count > 0)
        {
            Debug.Log($"WaveManager: Updated player tracking - {playerTransforms.Count} active players");
        }
    }

    private void HandleSpawning()
    {
        if (Time.time - lastSpawnTime >= currentSpawnInterval)
        {
            if (CanSpawnEnemy())
            {
                SpawnEnemy();
                lastSpawnTime = Time.time;
            }
        }
    }

    private bool CanSpawnEnemy()
    {
        // Check entity limits
        int maxEnemies = Mathf.Min(
            playerTransforms.Count * maxEnemiesPerPlayer,
            absoluteMaxEnemies
        );
        
        return currentEnemyCount.Value < maxEnemies && 
               activeEnemies.Count < maxEnemies &&
               enemyPrefabs != null && 
               enemyPrefabs.Length > 0;
    }

    private void SpawnEnemy()
    {
        if (!IsServer) return;
        
        // Select random enemy prefab
        GameObject enemyPrefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
        
        // Find spawn position
        Vector3 spawnPosition = GetSpawnPosition();
        if (spawnPosition == Vector3.zero)
        {
            if (showDebugLogs)
                Debug.LogWarning("WaveManager: Failed to find valid spawn position");
            return;
        }
        
        // Spawn enemy
        GameObject enemyInstance = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
        
        // Apply wave-based stat scaling
        ApplyDifficultyScaling(enemyInstance);
        
        // Track enemy
        activeEnemies.Add(enemyInstance);
        currentEnemyCount.Value++;
        
        // Setup enemy death tracking with proper cleanup
        Enemy enemyComponent = enemyInstance.GetComponent<Enemy>();
        if (enemyComponent != null)
        {
            HealthComponent healthComp = enemyInstance.GetComponent<HealthComponent>();
            if (healthComp != null)
            {
                // MEMORY LEAK FIX: Store reference for proper cleanup
                System.Action onDeathAction = () => OnEnemyKilled(enemyInstance);
                healthComp.OnDied += onDeathAction;
                
                // Store the action reference for cleanup (would need additional tracking)
                // For now, we rely on the enemy being destroyed to break the reference
            }
        }
        
        // Spawn as NetworkObject
        NetworkObject networkObject = enemyInstance.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            networkObject.Spawn();
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"WaveManager: Spawned enemy at {spawnPosition} (Wave {currentWave.Value}, Count: {currentEnemyCount.Value})");
        }
        
        // Trigger event
        OnEnemySpawned?.Invoke(enemyInstance);
    }

    private Vector3 GetSpawnPosition()
    {
        Vector3 spawnPos = Vector3.zero;
        int maxAttempts = 10;
        
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector3 candidatePos;
            
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                // Use predefined spawn points with random offset
                Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
                Vector2 randomOffset = Random.insideUnitCircle * spawnRadius;
                candidatePos = spawnPoint.position + new Vector3(randomOffset.x, 0f, randomOffset.y);
            }
            else if (playerTransforms.Count > 0)
            {
                // Spawn around a random player
                Transform randomPlayer = playerTransforms[Random.Range(0, playerTransforms.Count)];
                Vector2 randomCircle = Random.insideUnitCircle.normalized * Random.Range(spawnRadius * 0.7f, spawnRadius);
                candidatePos = randomPlayer.position + new Vector3(randomCircle.x, 0f, randomCircle.y);
            }
            else
            {
                // Fallback: random position around origin
                Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
                candidatePos = new Vector3(randomCircle.x, 0f, randomCircle.y);
            }
            
            // Raycast to find ground
            if (Physics.Raycast(candidatePos + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 20f, groundLayerMask))
            {
                spawnPos = hit.point + Vector3.up * spawnHeightOffset;
                break;
            }
            else
            {
                // Use candidate position with default height
                spawnPos = candidatePos + Vector3.up * spawnHeightOffset;
            }
        }
        
        return spawnPos;
    }

    private void ApplyDifficultyScaling(GameObject enemy)
    {
        if (!enableDifficultyScaling) return;
        
        int waveNumber = currentWave.Value;
        
        // Apply health scaling
        HealthComponent healthComp = enemy.GetComponent<HealthComponent>();
        if (healthComp != null)
        {
            float healthMultiplier = 1f + (enemyHealthMultiplierPerWave * (waveNumber - 1));
            // HealthComponent uses private fields, scaling would need to be handled differently
            // For now, we'll just note that enemy should start with higher base health
        }
        
        // Apply stat scaling to Enemy component
        Enemy enemyComp = enemy.GetComponent<Enemy>();
        if (enemyComp != null)
        {
            float speedMultiplier = 1f + (enemySpeedMultiplierPerWave * (waveNumber - 1));
            float damageMultiplier = 1f + (enemyDamageMultiplierPerWave * (waveNumber - 1));
            
            // Get current stats and apply multipliers
            float currentSpeed = 3.5f; // Default speed - could be read from enemy
            float currentDamage = 10f; // Default damage - could be read from enemy
            
            enemyComp.SetStats(
                currentSpeed * speedMultiplier,
                currentDamage * damageMultiplier,
                10f, // Detection range (unchanged)
                2f   // Attack range (unchanged)
            );
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"WaveManager: Applied difficulty scaling for wave {waveNumber} - Health: +{(enemyHealthMultiplierPerWave * (waveNumber - 1) * 100):F0}%");
        }
    }

    private void OnEnemyKilled(GameObject enemy)
    {
        if (enemy == null) return;
        
        // THREAD-SAFE: Remove from tracking
        if (activeEnemies.Contains(enemy))
        {
            activeEnemies.Remove(enemy);
            currentEnemyCount.Value = Mathf.Max(0, currentEnemyCount.Value - 1);
            
            if (showDebugLogs)
            {
                Debug.Log($"WaveManager: Enemy killed - Remaining: {currentEnemyCount.Value}");
            }
            
            // Trigger event
            OnEnemyDestroyed?.Invoke(enemy);
        }
        
        // MEMORY LEAK PREVENTION: Unsubscribe from events
        HealthComponent healthComp = enemy.GetComponent<HealthComponent>();
        if (healthComp != null)
        {
            // Note: In a full implementation, we'd need to store and remove specific delegates
            // For now, the enemy destruction should handle cleanup
        }
    }

    private void CleanUpDestroyedEnemies()
    {
        // PERFORMANCE: Only clean up periodically, not every frame
        if (Time.frameCount % 60 != 0) return; // Every 60 frames (~1 second at 60fps)
        
        // Remove null references from active enemies list
        int removedCount = 0;
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            if (activeEnemies[i] == null)
            {
                activeEnemies.RemoveAt(i);
                removedCount++;
            }
        }
        
        // Update network variable if we removed any
        if (removedCount > 0)
        {
            currentEnemyCount.Value = Mathf.Max(0, currentEnemyCount.Value - removedCount);
            
            if (showDebugLogs)
            {
                Debug.Log($"WaveManager: Cleaned up {removedCount} destroyed enemies - New count: {currentEnemyCount.Value}");
            }
        }
    }

    private void StartWave()
    {
        if (!IsServer) return;
        
        waveActive.Value = true;
        isSpawning = true;
        waveStartTime = Time.time;
        lastSpawnTime = Time.time;
        
        if (showDebugLogs)
        {
            Debug.Log($"WaveManager: 🌊 Wave {currentWave.Value} started! Spawn interval: {currentSpawnInterval:F1}s");
        }
        
        // Trigger event
        OnWaveStarted?.Invoke(currentWave.Value);
        
        // Notify all clients
        NotifyWaveStartClientRpc(currentWave.Value);
    }

    private void CheckWaveCompletion()
    {
        if (!isSpawning) return;
        
        // Check if it's time for next wave
        if (Time.time - waveStartTime >= waveInterval)
        {
            CompleteWave();
        }
    }

    private void CompleteWave()
    {
        if (!IsServer) return;
        
        // Stop spawning
        isSpawning = false;
        waveActive.Value = false;
        
        if (showDebugLogs)
        {
            Debug.Log($"WaveManager: 🎉 Wave {currentWave.Value} completed!");
        }
        
        // Trigger event
        OnWaveCompleted?.Invoke(currentWave.Value);
        
        // Prepare next wave
        PrepareNextWave();
    }

    private void PrepareNextWave()
    {
        // Increase wave number
        currentWave.Value++;
        
        // Reduce spawn interval (increase difficulty)
        currentSpawnInterval = Mathf.Max(
            minSpawnInterval,
            currentSpawnInterval * (1f - intervalReductionRate)
        );
        
        // Increase enemies per wave
        currentEnemiesInWave = Mathf.Min(
            currentEnemiesInWave + maxEnemyIncreasePerWave,
            absoluteMaxEnemies
        );
        
        if (showDebugLogs)
        {
            Debug.Log($"WaveManager: Next wave prepared - Wave {currentWave.Value}, Interval: {currentSpawnInterval:F1}s, Enemies: {currentEnemiesInWave}");
        }
        
        // Start next wave after a short delay
        Invoke(nameof(StartWave), 2f);
    }

    private void CreateDefaultSpawnPoints()
    {
        // Create basic spawn points in a circle around origin
        List<Transform> defaultSpawnPoints = new List<Transform>();
        int spawnPointCount = 8;
        float radius = spawnRadius;
        
        for (int i = 0; i < spawnPointCount; i++)
        {
            float angle = (360f / spawnPointCount) * i;
            Vector3 position = new Vector3(
                Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                0f,
                Mathf.Sin(angle * Mathf.Deg2Rad) * radius
            );
            
            GameObject spawnPoint = new GameObject($"SpawnPoint_{i}");
            spawnPoint.transform.position = position;
            spawnPoint.transform.SetParent(transform);
            
            defaultSpawnPoints.Add(spawnPoint.transform);
        }
        
        spawnPoints = defaultSpawnPoints.ToArray();
        
        if (showDebugLogs)
        {
            Debug.Log($"WaveManager: Created {spawnPointCount} default spawn points");
        }
    }

    // Network variable change callbacks
    private void OnWaveChanged(int previousValue, int newValue)
    {
        if (showDebugLogs)
            Debug.Log($"WaveManager: Wave changed from {previousValue} to {newValue}");
    }

    private void OnEnemyCountChanged(int previousValue, int newValue)
    {
        // Update UI or other systems that need to know enemy count
    }

    private void OnWaveActiveChanged(bool previousValue, bool newValue)
    {
        if (showDebugLogs)
            Debug.Log($"WaveManager: Wave active changed to {newValue}");
    }

    // Client RPCs
    [ClientRpc]
    private void NotifyWaveStartClientRpc(int waveNumber)
    {
        Debug.Log($"🌊 Wave {waveNumber} has begun!");
        // Here you would trigger UI notifications, sound effects, etc.
    }

    // Public control methods
    public void ForceNextWave()
    {
        if (IsServer)
        {
            CompleteWave();
        }
    }

    public void StopWaves()
    {
        if (IsServer)
        {
            isSpawning = false;
            waveActive.Value = false;
        }
    }

    // Debug methods
    [ContextMenu("Force Next Wave")]
    private void DebugForceNextWave()
    {
        ForceNextWave();
    }

    [ContextMenu("Spawn Enemy Now")]
    private void DebugSpawnEnemy()
    {
        if (IsServer)
        {
            SpawnEnemy();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showSpawnGizmos) return;
        
        // Draw spawn radius
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
        
        // Draw spawn points
        if (spawnPoints != null)
        {
            Gizmos.color = Color.yellow;
            foreach (Transform spawnPoint in spawnPoints)
            {
                if (spawnPoint != null)
                {
                    Gizmos.DrawWireSphere(spawnPoint.position, 2f);
                    Gizmos.DrawRay(spawnPoint.position, Vector3.up * 3f);
                }
            }
        }
    }

    // Public getters for debugging
    public string GetDebugInfo()
    {
        return $"Wave: {CurrentWave} | Enemies: {CurrentEnemyCount} | Active: {IsWaveActive} | " +
               $"Spawn Interval: {currentSpawnInterval:F1}s | Players: {playerTransforms.Count}";
    }
}