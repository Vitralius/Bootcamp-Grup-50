using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(HealthComponent))]
public class DestructibleObject : NetworkBehaviour
{
    [Header("Destruction Settings")]
    [SerializeField] private GameObject destructionEffect; // Particle effect when destroyed
    [SerializeField] private AudioClip destructionSound; // Sound when destroyed
    [SerializeField] private float destructionDelay = 0.1f; // Delay before disappearing
    [SerializeField] private bool dropItems = false; // Should drop items when destroyed
    [SerializeField] private GameObject[] itemDrops; // Items to drop
    [SerializeField] private int minDropCount = 1;
    [SerializeField] private int maxDropCount = 3;
    [SerializeField] private float dropForce = 5f; // Force applied to dropped items
    
    [Header("Visual Feedback")]
    [SerializeField] private bool enableDamageFlash = true;
    [SerializeField] private Color damageFlashColor = Color.red;
    [SerializeField] private float flashDuration = 0.1f;
    
    private HealthComponent healthComponent;
    private Renderer objectRenderer;
    private Color originalColor;
    private AudioSource audioSource;
    
    public override void OnNetworkSpawn()
    {
        healthComponent = GetComponent<HealthComponent>();
        objectRenderer = GetComponent<Renderer>();
        audioSource = GetComponent<AudioSource>();
        
        if (objectRenderer != null && enableDamageFlash)
        {
            originalColor = objectRenderer.material.color;
        }
        
        // Subscribe to health events
        healthComponent.OnHealthChanged += OnHealthChanged;
        healthComponent.OnDied += OnObjectDestroyed;
    }
    
    public override void OnNetworkDespawn()
    {
        if (healthComponent != null)
        {
            healthComponent.OnHealthChanged -= OnHealthChanged;
            healthComponent.OnDied -= OnObjectDestroyed;
        }
    }
    
    private void OnHealthChanged(float currentHealth, float maxHealth)
    {
        // Flash red when taking damage
        if (enableDamageFlash && IsClient)
        {
            StartCoroutine(DamageFlash());
        }
    }
    
    private void OnObjectDestroyed()
    {
        if (IsServer)
        {
            // Handle destruction effects and item drops on server
            HandleDestructionServerRpc();
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void HandleDestructionServerRpc()
    {
        // Play destruction effects on all clients
        PlayDestructionEffectsClientRpc();
        
        // Drop items if enabled
        if (dropItems && itemDrops.Length > 0)
        {
            DropItems();
        }
        
        // Destroy the object after a short delay
        Invoke(nameof(DestroyObject), destructionDelay);
    }
    
    [ClientRpc]
    private void PlayDestructionEffectsClientRpc()
    {
        // Play destruction particle effect
        if (destructionEffect != null)
        {
            GameObject effect = Instantiate(destructionEffect, transform.position, transform.rotation);
            Destroy(effect, 5f); // Clean up after 5 seconds
        }
        
        // Play destruction sound
        if (destructionSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(destructionSound);
        }
    }
    
    private void DropItems()
    {
        if (!IsServer) return;
        
        int dropCount = Random.Range(minDropCount, maxDropCount + 1);
        
        for (int i = 0; i < dropCount; i++)
        {
            if (itemDrops.Length > 0)
            {
                GameObject itemPrefab = itemDrops[Random.Range(0, itemDrops.Length)];
                Vector3 dropPosition = transform.position + Vector3.up * 0.5f;
                Vector3 randomDirection = Random.insideUnitSphere.normalized;
                randomDirection.y = Mathf.Abs(randomDirection.y); // Keep drops above ground
                
                GameObject droppedItem = Instantiate(itemPrefab, dropPosition, Quaternion.identity);
                
                // Apply force to the dropped item if it has a Rigidbody
                Rigidbody rb = droppedItem.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.AddForce(randomDirection * dropForce, ForceMode.Impulse);
                }
                
                // Spawn the dropped item on the network if it's a NetworkObject
                NetworkObject networkObject = droppedItem.GetComponent<NetworkObject>();
                if (networkObject != null)
                {
                    networkObject.Spawn();
                }
            }
        }
    }
    
    private System.Collections.IEnumerator DamageFlash()
    {
        if (objectRenderer != null)
        {
            objectRenderer.material.color = damageFlashColor;
            yield return new WaitForSeconds(flashDuration);
            objectRenderer.material.color = originalColor;
        }
    }
    
    private void DestroyObject()
    {
        if (IsServer)
        {
            GetComponent<NetworkObject>().Despawn();
        }
    }
    
    // Public methods for external scripts
    public void SetMaxHealth(float health)
    {
        if (healthComponent != null)
        {
            // You might need to add this method to HealthComponent
            // healthComponent.SetMaxHealth(health);
        }
    }
    
    public void SetDropItems(bool shouldDrop)
    {
        dropItems = shouldDrop;
    }
    
    public void AddItemDrop(GameObject item)
    {
        System.Collections.Generic.List<GameObject> drops = new System.Collections.Generic.List<GameObject>(itemDrops);
        drops.Add(item);
        itemDrops = drops.ToArray();
    }
}