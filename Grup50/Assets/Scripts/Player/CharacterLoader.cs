using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using StarterAssets;

[RequireComponent(typeof(ThirdPersonController))]
public class CharacterLoader : NetworkBehaviour
{
    [Header("Character Loading")]
    [Tooltip("Current character data applied to this controller")]
    [SerializeField] private CharacterData currentCharacterData;
    
    [Header("Visual Components")]
    [Tooltip("Parent object that contains the character mesh")]
    [SerializeField] private Transform characterMeshParent;
    
    [Tooltip("Animator component to update")]
    [SerializeField] private Animator characterAnimator;
    
    [Tooltip("Audio source for character sounds")]
    [SerializeField] private AudioSource audioSource;
    
    // Network variable to sync character selection across clients
    private NetworkVariable<int> selectedCharacterID = new NetworkVariable<int>(-1, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Owner);
    
    // Reference to the ThirdPersonController component
    private ThirdPersonController thirdPersonController;
    
    // Default character mesh to restore if needed
    private GameObject originalCharacterMesh;
    private RuntimeAnimatorController originalAnimatorController;
    
    private void Awake()
    {
        thirdPersonController = GetComponent<ThirdPersonController>();
        
        // Store original mesh and animator for fallback
        if (characterMeshParent != null && characterMeshParent.childCount > 0)
        {
            originalCharacterMesh = characterMeshParent.GetChild(0).gameObject;
        }
        
        if (characterAnimator != null)
        {
            originalAnimatorController = characterAnimator.runtimeAnimatorController;
        }
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Subscribe to character ID changes for all clients
        selectedCharacterID.OnValueChanged += OnCharacterIDChanged;
        
        // Subscribe to PlayerSessionData events for character selection integration
        if (PlayerSessionData.Instance != null)
        {
            PlayerSessionData.Instance.OnPlayerCharacterChanged += OnPlayerCharacterChangedInSession;
        }
        
        // For character loading, prioritize external systems over multiple attempts
        // This prevents race conditions from multiple loading sources
        
        // Only load character if we already have a synchronized network variable value
        if (selectedCharacterID.Value != -1)
        {
            Debug.Log($"CharacterLoader: Loading character {selectedCharacterID.Value} from synchronized network variable");
            LoadCharacterByID(selectedCharacterID.Value);
        }
        
        // NOTE: Character loading from session data and bridge is now handled by:
        // 1. SpawnManager after spawning
        // 2. SceneTransitionManager after scene load
        // This eliminates race conditions from multiple loading attempts
    }
    
    public override void OnNetworkDespawn()
    {
        selectedCharacterID.OnValueChanged -= OnCharacterIDChanged;
        
        // Unsubscribe from PlayerSessionData events
        if (PlayerSessionData.Instance != null)
        {
            PlayerSessionData.Instance.OnPlayerCharacterChanged -= OnPlayerCharacterChangedInSession;
        }
        
        base.OnNetworkDespawn();
    }
    
    /// <summary>
    /// Loads character data and applies it to the ThirdPersonController
    /// </summary>
    /// <param name="characterData">Character data to apply</param>
    public void LoadCharacter(CharacterData characterData)
    {
        if (characterData == null)
        {
            Debug.LogWarning("CharacterLoader: Attempted to load null character data");
            return;
        }
        
        currentCharacterData = characterData;
        
        // Update network variable if we're the owner
        if (IsOwner && IsSpawned)
        {
            selectedCharacterID.Value = characterData.characterID;
        }
        
        ApplyCharacterData(characterData);
    }
    
    /// <summary>
    /// Loads character by ID (used for network synchronization)
    /// </summary>
    /// <param name="characterID">ID of character to load</param>
    public void LoadCharacterByID(int characterID)
    {
        // This would typically load from a character database/registry
        // For now, we'll need to implement a character registry system
        CharacterData characterData = FindCharacterDataByID(characterID);
        
        if (characterData != null)
        {
            currentCharacterData = characterData;
            ApplyCharacterData(characterData);
        }
        else
        {
            Debug.LogWarning($"CharacterLoader: Could not find character with ID {characterID}");
        }
    }
    
    /// <summary>
    /// Applies character data to the ThirdPersonController component
    /// </summary>
    /// <param name="characterData">Character data to apply</param>
    private void ApplyCharacterData(CharacterData characterData)
    {
        if (thirdPersonController == null || characterData == null)
            return;
        
        // Apply movement stats
        thirdPersonController.MoveSpeed = characterData.moveSpeed;
        thirdPersonController.SprintSpeed = characterData.sprintSpeed;
        thirdPersonController.RotationSmoothTime = characterData.rotationSmoothTime;
        thirdPersonController.SpeedChangeRate = characterData.speedChangeRate;
        
        // Apply crouching settings
        thirdPersonController.HoldToCrouch = characterData.holdToCrouch;
        thirdPersonController.CrouchSpeedMultiplier = characterData.crouchSpeedMultiplier;
        
        // Apply sliding settings
        thirdPersonController.SlideSpeedThreshold = characterData.slideSpeedThreshold;
        thirdPersonController.MaxSlideSpeed = characterData.maxSlideSpeed;
        thirdPersonController.SlideDeceleration = characterData.slideDeceleration;
        thirdPersonController.MinSlideDuration = characterData.minSlideDuration;
        thirdPersonController.SlideJumpMomentumMultiplier = characterData.slideJumpMomentumMultiplier;
        thirdPersonController.SlideSteeringControl = characterData.slideSteeringControl;
        thirdPersonController.SlideEntryBoost = characterData.slideEntryBoost;
        
        // Apply jump settings
        thirdPersonController.JumpHeight = characterData.jumpHeight;
        thirdPersonController.EnableDoubleJump = characterData.enableDoubleJump;
        thirdPersonController.MaxJumps = characterData.maxJumps;
        thirdPersonController.Gravity = characterData.gravity;
        thirdPersonController.JumpTimeout = characterData.jumpTimeout;
        thirdPersonController.FallTimeout = characterData.fallTimeout;
        
        // Apply camera settings
        thirdPersonController.CrouchCameraOffset = characterData.crouchCameraOffset;
        thirdPersonController.SprintCameraOffset = characterData.sprintCameraOffset;
        thirdPersonController.CameraLerpSpeed = characterData.cameraLerpSpeed;
        thirdPersonController.SprintFOVIncrease = characterData.sprintFOVIncrease;
        thirdPersonController.EnableSprintScreenShake = characterData.enableSprintScreenShake;
        thirdPersonController.SprintShakeIntensity = characterData.sprintShakeIntensity;
        
        // Apply ground detection settings
        thirdPersonController.GroundedOffset = characterData.groundedOffset;
        thirdPersonController.GroundedRadius = characterData.groundedRadius;
        
        // Apply camera constraints
        thirdPersonController.TopClamp = characterData.topClamp;
        // Note: BottomClamp property might need to be added to ThirdPersonController
        
        // Apply audio settings
        thirdPersonController.LandingAudioClip = characterData.landingAudioClip;
        thirdPersonController.FootstepAudioClips = characterData.footstepAudioClips;
        thirdPersonController.FootstepAudioVolume = characterData.footstepAudioVolume;
        
        // Apply visual changes
        ApplyVisualChanges(characterData);
        
        Debug.Log($"CharacterLoader: Applied character data for '{characterData.characterName}'");
    }
    
    /// <summary>
    /// Applies visual changes such as skeletal mesh replacement and materials
    /// </summary>
    /// <param name="characterData">Character data containing visual information</param>
    private void ApplyVisualChanges(CharacterData characterData)
    {
        // OPTIMIZED: Use skeletal mesh replacement for same-skeleton characters
        // This is the most efficient method and maintains perfect animation compatibility
        if (characterData.skeletalMesh != null)
        {
            Debug.Log($"CharacterLoader: Using skeletal mesh replacement for {characterData.characterName} (same skeleton system)");
            ReplaceSkeletalMesh(characterData);
        }
        else
        {
            Debug.LogError($"CharacterLoader: No skeletalMesh specified for {characterData.characterName}. " +
                         "You MUST provide a skeletal mesh that uses the same skeleton structure for character replacement.");
            
            // For debugging: Log current character data state
            Debug.LogError($"CharacterLoader: CharacterData debug info for {characterData.characterName}:");
            Debug.LogError($"  - SkeletalMesh: {(characterData.skeletalMesh != null ? characterData.skeletalMesh.name : "NULL")}");
            Debug.LogError($"  - AnimatorController: {(characterData.animatorController != null ? characterData.animatorController.name : "NULL")}");
            Debug.LogError($"  - Validation: {characterData.ValidationError}");
            
            return;
        }
        
        // Apply additional outfit meshes if available (only works with same skeleton)
        if (characterData.outfitMeshes != null && characterData.outfitMeshes.Length > 0)
        {
            ApplyOutfitMeshes(characterData);
        }
        
        // Apply animator controller AFTER skeleton/mesh changes to ensure proper setup
        if (characterData.animatorController != null && characterAnimator != null)
        {
            characterAnimator.runtimeAnimatorController = characterData.animatorController;
            
            // Force animator to rebind after skeleton/mesh changes
            characterAnimator.Rebind();
            Debug.Log($"CharacterLoader: Applied and rebound animator controller: {characterData.animatorController.name}");
        }
        
        // Apply materials
        ApplyMaterials(characterData);
        
        // Apply character scale
        if (characterMeshParent != null && characterData.characterScale != Vector3.one)
        {
            characterMeshParent.localScale = characterData.characterScale;
        }
    }
    
    
    /// <summary>
    /// Logs information about the skeleton structure for debugging
    /// </summary>
    private void LogSkeletonInfo(GameObject character)
    {
        SkinnedMeshRenderer[] renderers = character.GetComponentsInChildren<SkinnedMeshRenderer>();
        
        foreach (SkinnedMeshRenderer renderer in renderers)
        {
            if (renderer == null) continue;
            
            Debug.Log($"CharacterLoader: SkinnedMeshRenderer '{renderer.name}':");
            Debug.Log($"  - Mesh: {(renderer.sharedMesh != null ? renderer.sharedMesh.name : "NULL")}");
            Debug.Log($"  - Bones: {renderer.bones.Length}");
            Debug.Log($"  - Root Bone: {(renderer.rootBone != null ? renderer.rootBone.name : "NULL")}");
            Debug.Log($"  - Bounds: {renderer.localBounds}");
            
            // Log first few bones for debugging
            for (int i = 0; i < Mathf.Min(5, renderer.bones.Length); i++)
            {
                if (renderer.bones[i] != null)
                {
                    Debug.Log($"    Bone {i}: {renderer.bones[i].name}");
                }
            }
        }
    }
    
    /// <summary>
    /// Fixes common SkinnedMeshRenderer issues after instantiation
    /// </summary>
    private void FixSkinnedMeshRenderers(GameObject character)
    {
        SkinnedMeshRenderer[] renderers = character.GetComponentsInChildren<SkinnedMeshRenderer>();
        
        foreach (SkinnedMeshRenderer renderer in renderers)
        {
            if (renderer == null || renderer.sharedMesh == null) continue;
            
            // Ensure proper bounds
            renderer.localBounds = renderer.sharedMesh.bounds;
            
            // Reset bounds center if offset
            var bounds = renderer.localBounds;
            if (bounds.center.magnitude > 0.1f)  // If center is significantly offset
            {
                bounds.center = Vector3.zero;
                renderer.localBounds = bounds;
            }
            
            // Force renderer refresh
            renderer.enabled = false;
            renderer.enabled = true;
            
            Debug.Log($"CharacterLoader: Fixed SkinnedMeshRenderer on {renderer.name}");
        }
    }
    
    /// <summary>
    /// Replaces skeletal mesh on SkinnedMeshRenderer (OPTIMIZED for same skeleton system)
    /// </summary>
    private void ReplaceSkeletalMesh(CharacterData characterData)
    {
        // Look for SkinnedMeshRenderer in the character mesh parent and its children
        SkinnedMeshRenderer[] skinnedRenderers = null;
        
        if (characterMeshParent != null)
        {
            skinnedRenderers = characterMeshParent.GetComponentsInChildren<SkinnedMeshRenderer>();
        }
        
        // If not found, search in the entire character hierarchy
        if (skinnedRenderers == null || skinnedRenderers.Length == 0)
        {
            skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        }
        
        if (skinnedRenderers.Length == 0)
        {
            Debug.LogError($"CharacterLoader: No SkinnedMeshRenderer found for character {characterData.characterName}");
            return;
        }
        
        foreach (SkinnedMeshRenderer renderer in skinnedRenderers)
        {
            if (renderer == null) continue;
            
            // Check if this renderer should be replaced
            if (ShouldReplaceRenderer(renderer, characterData.targetRendererNames))
            {
                // Store original bone setup (same skeleton = bones are identical)
                Transform[] originalBones = renderer.bones;
                Transform originalRootBone = renderer.rootBone;
                
                Debug.Log($"CharacterLoader: Replacing mesh on {renderer.name} with {characterData.skeletalMesh.name}");
                
                // Validate mesh compatibility first
                if (!ValidateSameSkeleton(renderer, characterData.skeletalMesh))
                {
                    Debug.LogError($"CharacterLoader: Skeletal mesh {characterData.skeletalMesh.name} is not compatible with the same skeleton system");
                    continue;
                }
                
                // Replace the skeletal mesh
                renderer.sharedMesh = characterData.skeletalMesh;
                
                // OPTIMIZED: Since all characters use the same skeleton, bones are compatible
                // Keep the original bone setup for perfect animation compatibility
                if (originalBones != null && originalBones.Length > 0)
                {
                    renderer.bones = originalBones;
                    renderer.rootBone = originalRootBone;
                    Debug.Log($"CharacterLoader: Preserved {originalBones.Length} bones for {renderer.name} (same skeleton system)");
                }
                
                // Update bounds to match new mesh
                if (renderer.sharedMesh != null)
                {
                    renderer.localBounds = renderer.sharedMesh.bounds;
                    
                    // Reset bounds center to origin if needed
                    var bounds = renderer.localBounds;
                    if (bounds.center.magnitude > 0.1f)
                    {
                        bounds.center = Vector3.zero;
                        renderer.localBounds = bounds;
                        Debug.Log($"CharacterLoader: Reset bounds center for {renderer.name}");
                    }
                }
                
                // Force renderer refresh to ensure proper display
                renderer.enabled = false;
                renderer.enabled = true;
                
                Debug.Log($"CharacterLoader: ✅ Successfully replaced skeletal mesh on {renderer.name}");
            }
        }
        
        Debug.Log($"CharacterLoader: ✅ Skeletal mesh replacement completed for {characterData.characterName}");
    }
    
    /// <summary>
    /// Applies additional outfit meshes to specific renderers
    /// </summary>
    private void ApplyOutfitMeshes(CharacterData characterData)
    {
        SkinnedMeshRenderer[] skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        
        // Apply outfit meshes to specific renderers (if targetRendererNames is defined)
        for (int i = 0; i < characterData.outfitMeshes.Length && i < skinnedRenderers.Length; i++)
        {
            if (characterData.outfitMeshes[i] != null && skinnedRenderers[i] != null)
            {
                skinnedRenderers[i].sharedMesh = characterData.outfitMeshes[i];
                Debug.Log($"CharacterLoader: Applied outfit mesh {i} to {skinnedRenderers[i].name}");
            }
        }
    }
    
    /// <summary>
    /// Applies materials to renderers
    /// </summary>
    private void ApplyMaterials(CharacterData characterData)
    {
        if (characterData.characterMaterials == null || characterData.characterMaterials.Length == 0)
            return;
            
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            if (renderer != null)
            {
                // Apply materials based on available slots
                Material[] newMaterials = new Material[renderer.materials.Length];
                for (int i = 0; i < newMaterials.Length; i++)
                {
                    newMaterials[i] = i < characterData.characterMaterials.Length 
                        ? characterData.characterMaterials[i] 
                        : characterData.characterMaterials[0]; // Use first material as fallback
                }
                renderer.materials = newMaterials;
            }
        }
    }
    
    /// <summary>
    /// Determines if a renderer should be replaced based on target names
    /// </summary>
    private bool ShouldReplaceRenderer(SkinnedMeshRenderer renderer, string[] targetNames)
    {
        // If no target names specified, replace all renderers
        if (targetNames == null || targetNames.Length == 0)
            return true;
            
        // Check if renderer name matches any target names
        foreach (string targetName in targetNames)
        {
            if (renderer.name.Contains(targetName))
                return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Validates that skeletal mesh is compatible with same-skeleton system
    /// </summary>
    private bool ValidateSameSkeleton(SkinnedMeshRenderer renderer, Mesh newMesh)
    {
        if (newMesh == null || newMesh.bindposes == null)
        {
            Debug.LogWarning($"CharacterLoader: New mesh has no bind poses");
            return false;
        }
        
        int newBoneCount = newMesh.bindposes.Length;
        int originalBoneCount = renderer.bones.Length;
        
        if (newBoneCount != originalBoneCount)
        {
            Debug.LogWarning($"CharacterLoader: Bone count mismatch on {renderer.name}. " +
                           $"Original: {originalBoneCount}, New: {newBoneCount}. " +
                           $"Same skeleton system requires identical bone structures.");
            return false;
        }
        
        Debug.Log($"CharacterLoader: ✅ Skeletal mesh validation passed - {newBoneCount} bones match");
        return true;
    }
    
    /// <summary>
    /// Finds character data by ID from the character registry
    /// </summary>
    /// <param name="characterID">ID of character to find</param>
    /// <returns>CharacterData if found, null otherwise</returns>
    private CharacterData FindCharacterDataByID(int characterID)
    {
        if (CharacterRegistry.Instance == null)
        {
            Debug.LogError("CharacterLoader: CharacterRegistry instance not found");
            return null;
        }
        
        return CharacterRegistry.Instance.GetCharacterByID(characterID);
    }
    
    /// <summary>
    /// Network callback when character ID changes
    /// </summary>
    /// <param name="oldValue">Previous character ID</param>
    /// <param name="newValue">New character ID</param>
    private void OnCharacterIDChanged(int oldValue, int newValue)
    {
        if (newValue != -1)
        {
            LoadCharacterByID(newValue);
        }
    }
    
    /// <summary>
    /// Callback when any player's character selection changes via PlayerSessionData
    /// </summary>
    /// <param name="playerGuid">Player GUID who changed character</param>
    /// <param name="characterId">New character ID</param>
    private void OnPlayerCharacterChangedInSession(string playerGuid, int characterId)
    {
        // Only apply to this player's character
        if (IsOwner && PlayerSessionData.Instance != null)
        {
            var currentSession = PlayerSessionData.Instance.GetCurrentPlayerSession();
            if (currentSession.HasValue && currentSession.Value.playerId.ToString() == playerGuid)
            {
                Debug.Log($"CharacterLoader: Player {playerGuid} changed character to {characterId}, loading...");
                LoadCharacterByID(characterId);
            }
        }
    }
    
    /// <summary>
    /// Gets the current character data
    /// </summary>
    /// <returns>Current CharacterData</returns>
    public CharacterData GetCurrentCharacterData()
    {
        return currentCharacterData;
    }
    
    /// <summary>
    /// Gets the current character ID
    /// </summary>
    /// <returns>Current character ID</returns>
    public int GetCurrentCharacterID()
    {
        return selectedCharacterID.Value;
    }
    
    /// <summary>
    /// Resets to original/default character
    /// </summary>
    public void ResetToDefault()
    {
        if (originalCharacterMesh != null && characterMeshParent != null)
        {
            // Preserve the PlayerCameraRoot (Cinemachine camera target) during reset
            GameObject playerCameraRoot = null;
            
            // Find and temporarily store the PlayerCameraRoot
            for (int i = 0; i < characterMeshParent.childCount; i++)
            {
                GameObject child = characterMeshParent.GetChild(i).gameObject;
                if (child.name == "PlayerCameraRoot")
                {
                    playerCameraRoot = child;
                    Debug.Log($"CharacterLoader: Found PlayerCameraRoot, preserving it during reset");
                    break;
                }
            }
            
            // Temporarily reparent the PlayerCameraRoot to avoid destruction
            if (playerCameraRoot != null)
            {
                playerCameraRoot.transform.SetParent(null);
            }
            
            // Clear current mesh
            for (int i = characterMeshParent.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(characterMeshParent.GetChild(i).gameObject);
            }
            
            // Restore original mesh
            GameObject restoredMesh = Instantiate(originalCharacterMesh, characterMeshParent);
            restoredMesh.transform.localPosition = Vector3.zero;
            restoredMesh.transform.localRotation = Quaternion.identity;
            restoredMesh.transform.localScale = Vector3.one;
            
            // Restore the PlayerCameraRoot after original mesh restoration
            if (playerCameraRoot != null)
            {
                playerCameraRoot.transform.SetParent(characterMeshParent);
                playerCameraRoot.transform.localPosition = new Vector3(0, 1.375f, 0);
                playerCameraRoot.transform.localRotation = Quaternion.identity;
                playerCameraRoot.transform.localScale = Vector3.one;
                Debug.Log($"CharacterLoader: Restored PlayerCameraRoot after reset");
            }
        }
        
        if (originalAnimatorController != null && characterAnimator != null)
        {
            characterAnimator.runtimeAnimatorController = originalAnimatorController;
        }
        
        currentCharacterData = null;
        
        if (IsOwner && IsSpawned)
        {
            selectedCharacterID.Value = -1;
        }
    }
}