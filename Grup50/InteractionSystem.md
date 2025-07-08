# Unity Interaction System Design

## Overview

This document outlines the best practices for implementing interactable objects in Unity with multiplayer networking support using Unity Netcode for GameObjects.

## Recommended Hybrid Approach

The best practice in 2024 is a **hybrid approach** combining interfaces with components for maximum flexibility and Unity integration.

## Core Interface Definition

```csharp
public interface IInteractable
{
    string InteractionPrompt { get; }
    bool CanInteract { get; }
    void Interact(GameObject interactor);
}
```

### Optional Extended Interface

```csharp
public interface IInteractable
{
    string InteractionPrompt { get; }
    bool CanInteract { get; }
    float InteractionRange { get; }
    Sprite InteractionIcon { get; }
    
    void Interact(GameObject interactor);
    void OnInteractionStart(GameObject interactor);
    void OnInteractionEnd(GameObject interactor);
}
```

## Component-Based Implementation Examples

### Door Example

```csharp
using Unity.Netcode;
using UnityEngine;

public class Door : NetworkBehaviour, IInteractable
{
    [SerializeField] private NetworkVariable<bool> isOpen = new NetworkVariable<bool>();
    [SerializeField] private Animator doorAnimator;
    [SerializeField] private AudioSource doorSound;
    
    public string InteractionPrompt => isOpen.Value ? "Close Door" : "Open Door";
    public bool CanInteract => true;
    public float InteractionRange => 2f;
    
    public void Interact(GameObject interactor)
    {
        if (IsServer) // Server authority
        {
            ToggleDoor();
        }
        else
        {
            ToggleDoorServerRpc();
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void ToggleDoorServerRpc()
    {
        ToggleDoor();
    }
    
    private void ToggleDoor()
    {
        isOpen.Value = !isOpen.Value;
        UpdateDoorVisuals();
    }
    
    private void UpdateDoorVisuals()
    {
        if (doorAnimator != null)
        {
            doorAnimator.SetBool("IsOpen", isOpen.Value);
        }
        
        if (doorSound != null)
        {
            doorSound.Play();
        }
    }
    
    public override void OnNetworkSpawn()
    {
        isOpen.OnValueChanged += OnDoorStateChanged;
        UpdateDoorVisuals(); // Sync initial state
    }
    
    private void OnDoorStateChanged(bool previous, bool current)
    {
        UpdateDoorVisuals();
    }
}
```

### Loot Pickup Example

```csharp
using Unity.Netcode;
using UnityEngine;

public class LootPickup : NetworkBehaviour, IInteractable
{
    [SerializeField] private string itemName;
    [SerializeField] private int itemQuantity = 1;
    [SerializeField] private GameObject visualObject;
    
    public string InteractionPrompt => $"Pick up {itemName} ({itemQuantity})";
    public bool CanInteract => IsSpawned;
    public float InteractionRange => 1.5f;
    
    public void Interact(GameObject interactor)
    {
        if (IsServer)
        {
            CollectItem(interactor);
        }
        else
        {
            CollectItemServerRpc(interactor.GetComponent<NetworkObject>().NetworkObjectId);
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void CollectItemServerRpc(ulong interactorId)
    {
        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(interactorId, out NetworkObject interactorObject))
        {
            CollectItem(interactorObject.gameObject);
        }
    }
    
    private void CollectItem(GameObject interactor)
    {
        // Add item to player inventory
        var inventory = interactor.GetComponent<PlayerInventory>();
        if (inventory != null)
        {
            inventory.AddItem(itemName, itemQuantity);
        }
        
        // Destroy the pickup
        NetworkObject.Despawn(true);
    }
}
```

### Chest Example

```csharp
using Unity.Netcode;
using UnityEngine;

public class Chest : NetworkBehaviour, IInteractable
{
    [SerializeField] private NetworkVariable<bool> isOpen = new NetworkVariable<bool>();
    [SerializeField] private Animator chestAnimator;
    [SerializeField] private GameObject[] lootItems;
    
    public string InteractionPrompt => isOpen.Value ? "Close Chest" : "Open Chest";
    public bool CanInteract => true;
    public float InteractionRange => 2f;
    
    public void Interact(GameObject interactor)
    {
        if (IsServer)
        {
            ToggleChest();
        }
        else
        {
            ToggleChestServerRpc();
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void ToggleChestServerRpc()
    {
        ToggleChest();
    }
    
    private void ToggleChest()
    {
        isOpen.Value = !isOpen.Value;
        
        if (chestAnimator != null)
        {
            chestAnimator.SetBool("IsOpen", isOpen.Value);
        }
        
        // Show/hide loot items
        foreach (var item in lootItems)
        {
            item.SetActive(isOpen.Value);
        }
    }
    
    public override void OnNetworkSpawn()
    {
        isOpen.OnValueChanged += OnChestStateChanged;
    }
    
    private void OnChestStateChanged(bool previous, bool current)
    {
        if (chestAnimator != null)
        {
            chestAnimator.SetBool("IsOpen", current);
        }
        
        foreach (var item in lootItems)
        {
            item.SetActive(current);
        }
    }
}
```

## Player Interaction System

```csharp
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class PlayerInteraction : NetworkBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] private LayerMask interactableLayer = -1;
    
    [Header("UI References")]
    [SerializeField] private GameObject interactionPrompt;
    [SerializeField] private Text promptText;
    
    private IInteractable currentInteractable;
    private Camera playerCamera;
    
    void Start()
    {
        playerCamera = Camera.main;
        if (interactionPrompt != null)
            interactionPrompt.SetActive(false);
    }
    
    void Update()
    {
        if (!IsOwner) return;
        
        DetectInteractables();
        
        if (Input.GetKeyDown(KeyCode.E) && currentInteractable != null)
        {
            currentInteractable.Interact(gameObject);
        }
    }
    
    private void DetectInteractables()
    {
        IInteractable newInteractable = null;
        
        // Raycast from camera
        Ray ray = playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
        
        if (Physics.Raycast(ray, out RaycastHit hit, interactionRange, interactableLayer))
        {
            newInteractable = hit.collider.GetComponent<IInteractable>();
            
            // Check if it's within interaction range
            if (newInteractable != null)
            {
                float distance = Vector3.Distance(transform.position, hit.point);
                if (distance > interactionRange)
                {
                    newInteractable = null;
                }
            }
        }
        
        // Update current interactable
        if (newInteractable != currentInteractable)
        {
            currentInteractable = newInteractable;
            UpdateInteractionUI();
        }
    }
    
    private void UpdateInteractionUI()
    {
        if (currentInteractable != null && currentInteractable.CanInteract)
        {
            interactionPrompt.SetActive(true);
            promptText.text = $"[E] {currentInteractable.InteractionPrompt}";
        }
        else
        {
            interactionPrompt.SetActive(false);
        }
    }
}
```

## Alternative Detection Methods

### Sphere Cast Detection

```csharp
private void DetectInteractables()
{
    Collider[] colliders = Physics.OverlapSphere(transform.position, interactionRange, interactableLayer);
    
    IInteractable closestInteractable = null;
    float closestDistance = float.MaxValue;
    
    foreach (var collider in colliders)
    {
        var interactable = collider.GetComponent<IInteractable>();
        if (interactable != null && interactable.CanInteract)
        {
            float distance = Vector3.Distance(transform.position, collider.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestInteractable = interactable;
            }
        }
    }
    
    if (closestInteractable != currentInteractable)
    {
        currentInteractable = closestInteractable;
        UpdateInteractionUI();
    }
}
```

### Trigger-Based Detection

```csharp
public class InteractionTrigger : MonoBehaviour
{
    private PlayerInteraction playerInteraction;
    
    void Start()
    {
        playerInteraction = GetComponentInParent<PlayerInteraction>();
    }
    
    void OnTriggerEnter(Collider other)
    {
        var interactable = other.GetComponent<IInteractable>();
        if (interactable != null)
        {
            playerInteraction.AddInteractable(interactable);
        }
    }
    
    void OnTriggerExit(Collider other)
    {
        var interactable = other.GetComponent<IInteractable>();
        if (interactable != null)
        {
            playerInteraction.RemoveInteractable(interactable);
        }
    }
}
```

## Architecture Benefits

### Interface Benefits
- **Generic Interaction**: Handle all interactables through common interface
- **Type Safety**: Compile-time checking of interaction contract
- **Extensibility**: Easy to add new interactable types
- **Polymorphism**: Different objects can implement interaction differently

### Component Benefits
- **Unity Integration**: Full Inspector support for configuration
- **Serialization**: Save/load interactable states
- **Network Support**: Built-in Netcode integration
- **Visual Scripting**: UnityEvents for non-programmers

### Multiplayer Considerations
- **Server Authority**: All state changes go through the server
- **Client Prediction**: Immediate feedback for better UX
- **Network Synchronization**: Automatic state sync across clients
- **Ownership Management**: Proper handling of object ownership

## Implementation Guidelines

### 1. Component Order
Ensure `NetworkObject` component is ordered before `NetworkBehaviour` components.

### 2. Authority Pattern
Use server authority for all state changes:
```csharp
if (IsServer)
{
    // Direct change
}
else
{
    // RPC to server
}
```

### 3. State Synchronization
Use `NetworkVariable` for synchronized state:
```csharp
private NetworkVariable<bool> state = new NetworkVariable<bool>();
```

### 4. Visual Feedback
Always provide immediate visual feedback even in multiplayer:
```csharp
// Immediate client-side feedback
PlayInteractionAnimation();

// Server-side validation
if (IsServer)
{
    ValidateAndApplyInteraction();
}
```

### 5. Error Handling
Always validate interaction conditions:
```csharp
public void Interact(GameObject interactor)
{
    if (!CanInteract) return;
    
    // Perform interaction
}
```

## File Structure

```
Assets/
├── Scripts/
│   ├── Interaction/
│   │   ├── IInteractable.cs
│   │   ├── PlayerInteraction.cs
│   │   └── InteractionTrigger.cs
│   └── Interactables/
│       ├── Door.cs
│       ├── LootPickup.cs
│       ├── Chest.cs
│       └── Switch.cs
```

## Testing

### Single Player Testing
1. Create test scene with various interactables
2. Test interaction detection range
3. Verify UI feedback
4. Test edge cases (multiple overlapping interactables)

### Multiplayer Testing
1. Test with multiple clients
2. Verify server authority
3. Test network synchronization
4. Check for race conditions
5. Validate ownership handling

## Performance Considerations

### Optimization Tips
- Use object pooling for frequently spawned/despawned items
- Implement spatial partitioning for many interactables
- Cache component references
- Use events instead of polling when possible
- Consider using `FixedUpdate` for physics-based interactions

### Memory Management
- Unsubscribe from events in `OnDestroy`
- Use weak references for callback systems
- Implement proper cleanup in `OnNetworkDespawn`

## Future Enhancements

### Advanced Features
- **Interaction Queues**: Handle multiple simultaneous interactions
- **Animation Integration**: Smooth interaction animations
- **Audio System**: Spatial audio for interactions
- **Visual Effects**: Particle systems for interactions
- **Accessibility**: Screen reader support for interaction prompts

### UI Improvements
- **World Space UI**: 3D interaction prompts
- **Progress Bars**: For long interactions
- **Context Menus**: Multiple interaction options
- **Tooltips**: Detailed interaction information