using Unity.Netcode;
using UnityEngine;

public class PlayerSpawnFix : NetworkBehaviour
{
    [Header("Spawn Position")]
    [SerializeField] private Vector3 spawnOffset = new Vector3(0, 5, 0);
    
    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // Move player to a safe spawn position
            Vector3 safePosition = transform.position + spawnOffset;
            
            // Apply immediately
            transform.position = safePosition;
            
            // If this has a CharacterController, reset it
            CharacterController charController = GetComponent<CharacterController>();
            if (charController != null)
            {
                charController.enabled = false;
                charController.enabled = true;
            }
            
            Debug.Log($"Player spawned at: {safePosition}");
        }
    }
}