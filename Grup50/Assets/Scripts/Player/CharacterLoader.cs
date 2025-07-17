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
            
            // Load character from session data if we're the owner
            if (IsOwner)
            {
                var currentSession = PlayerSessionData.Instance.GetCurrentPlayerSession();
                if (currentSession.HasValue && currentSession.Value.selectedCharacterId != 0)
                {
                    Debug.Log($"CharacterLoader: Loading character {currentSession.Value.selectedCharacterId} from session data");
                    LoadCharacterByID(currentSession.Value.selectedCharacterId);
                }
            }
        }
        
        // Load character if we already have data in network variable
        if (selectedCharacterID.Value != -1)
        {
            LoadCharacterByID(selectedCharacterID.Value);
        }
        
        // Also check if CharacterSelectionBridge has data for this player
        if (CharacterSelectionBridge.Instance != null && IsOwner)
        {
            var localSession = PlayerSessionData.Instance?.GetCurrentPlayerSession();
            if (localSession.HasValue)
            {
                string playerGuid = localSession.Value.playerId.ToString();
                int characterSelection = CharacterSelectionBridge.Instance.GetPlayerCharacterSelection(playerGuid);
                if (characterSelection != -1)
                {
                    Debug.Log($"CharacterLoader: Loading character {characterSelection} from bridge data");
                    LoadCharacterByID(characterSelection);
                }
            }
        }
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
        // CRITICAL: For different skeletons, always use complete prefab replacement
        // This ensures the skeleton and mesh are properly matched
        if (characterData.CharacterPrefab != null && characterMeshParent != null)
        {
            Debug.Log($"CharacterLoader: Using complete prefab replacement for {characterData.characterName}");
            ReplacePrefab(characterData);
        }
        // DEPRECATED: Skeletal mesh replacement only works for SAME skeleton structure
        // This approach is problematic for different characters with different bone structures
        else if (characterData.skeletalMesh != null)
        {
            Debug.LogWarning($"CharacterLoader: Using skeletal mesh replacement for {characterData.characterName}. " +
                           "This only works if the new mesh uses the SAME skeleton structure as the base character. " +
                           "For different skeletons, use CharacterPrefab instead.");
            ReplaceSkeletalMesh(characterData);
        }
        else
        {
            Debug.LogError($"CharacterLoader: No CharacterPrefab or skeletalMesh specified for {characterData.characterName}. " +
                         "For different characters, you MUST provide a CharacterPrefab with the complete skeleton + mesh.");
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
    /// Replaces the entire character prefab (includes complete skeleton replacement)
    /// </summary>
    private void ReplacePrefab(CharacterData characterData)
    {
        Debug.Log($"CharacterLoader: Starting complete prefab replacement for {characterData.characterName}");
        Debug.Log($"CharacterLoader: Removing existing skeleton and mesh...");
        
        // Remove existing character mesh/model (this includes the old skeleton)
        for (int i = characterMeshParent.childCount - 1; i >= 0; i--)
        {
            GameObject child = characterMeshParent.GetChild(i).gameObject;
            Debug.Log($"CharacterLoader: Destroying old character component: {child.name}");
            
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
        
        Debug.Log($"CharacterLoader: Instantiating new character prefab with new skeleton...");
        
        // Instantiate new character prefab (this includes the new skeleton)
        GameObject newCharacter = Instantiate(characterData.CharacterPrefab, characterMeshParent);
        newCharacter.transform.localPosition = Vector3.zero;
        newCharacter.transform.localRotation = Quaternion.identity;
        newCharacter.transform.localScale = characterData.characterScale;
        
        Debug.Log($"CharacterLoader: New character instantiated: {newCharacter.name}");
        
        // Update animator reference if the new prefab has one
        Animator newAnimator = newCharacter.GetComponentInChildren<Animator>();
        if (newAnimator != null)
        {
            characterAnimator = newAnimator;
            Debug.Log($"CharacterLoader: Found new animator in prefab: {newAnimator.name}");
            
            // Apply the character's animator controller if specified
            if (characterData.animatorController != null)
            {
                characterAnimator.runtimeAnimatorController = characterData.animatorController;
                Debug.Log($"CharacterLoader: Applied animator controller: {characterData.animatorController.name}");
            }
        }
        else
        {
            Debug.LogWarning($"CharacterLoader: No animator found in new prefab {characterData.CharacterPrefab.name}");
        }
        
        // Fix any SkinnedMeshRenderer issues in the new prefab
        FixSkinnedMeshRenderers(newCharacter);
        
        // Log skeleton information
        LogSkeletonInfo(newCharacter);
        
        Debug.Log($"CharacterLoader: ✅ Successfully replaced entire prefab (skeleton + mesh) with {characterData.CharacterPrefab.name}");
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
    /// Replaces skeletal mesh on SkinnedMeshRenderer (Unity's skeletal mesh equivalent)
    /// </summary>
    private void ReplaceSkeletalMesh(CharacterData characterData)
    {
        // Look for SkinnedMeshRenderer in the character mesh parent (Geometry) and its children
        SkinnedMeshRenderer[] skinnedRenderers = null;
        
        if (characterMeshParent != null)
        {
            // First try to find in the characterMeshParent and its children
            skinnedRenderers = characterMeshParent.GetComponentsInChildren<SkinnedMeshRenderer>();
        }
        
        // If not found, search in the entire character hierarchy
        if (skinnedRenderers == null || skinnedRenderers.Length == 0)
        {
            skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        }
        
        foreach (SkinnedMeshRenderer renderer in skinnedRenderers)
        {
            if (renderer == null) continue;
            
            // Check if this renderer should be replaced
            if (ShouldReplaceRenderer(renderer, characterData.targetRendererNames))
            {
                // Store original bone setup for non-optimized meshes
                Transform[] originalBones = renderer.bones;
                Transform originalRootBone = renderer.rootBone;
                
                // Replace the skeletal mesh
                renderer.sharedMesh = characterData.skeletalMesh;
                
                // CRITICAL FIX: Reassign bones for non-optimized meshes
                if (!characterData.useOptimizedMesh && originalBones != null && originalBones.Length > 0)
                {
                    // For non-optimized meshes, reassign the bones array
                    renderer.bones = originalBones;
                    renderer.rootBone = originalRootBone;
                    
                    Debug.Log($"CharacterLoader: Reassigned {originalBones.Length} bones to {renderer.name}");
                }
                else if (characterData.useOptimizedMesh)
                {
                    // For optimized meshes, clear bones array (Unity will handle automatically)
                    renderer.bones = new Transform[0];
                    Debug.Log($"CharacterLoader: Using optimized mesh for {renderer.name}, bones cleared");
                }
                
                // Update bounds to prevent culling issues
                if (renderer.sharedMesh != null)
                {
                    renderer.localBounds = renderer.sharedMesh.bounds;
                    
                    // CRITICAL FIX: Set the center of bounds to origin if it's offset
                    var bounds = renderer.localBounds;
                    if (bounds.center != Vector3.zero)
                    {
                        bounds.center = Vector3.zero;
                        renderer.localBounds = bounds;
                        Debug.Log($"CharacterLoader: Reset bounds center to origin for {renderer.name}");
                    }
                }
                
                // Force update the renderer to ensure visibility
                renderer.enabled = false;
                renderer.enabled = true;
                
                Debug.Log($"CharacterLoader: Successfully replaced skeletal mesh on {renderer.name} (found in {renderer.transform.parent?.name})");
            }
        }
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
    /// Validates bone mapping for non-optimized meshes
    /// </summary>
    private void ValidateBoneMapping(SkinnedMeshRenderer renderer, Transform[] originalBones)
    {
        // For advanced users: implement bone remapping logic here
        // This ensures that the new mesh's bone structure matches the original
        // For now, we'll just log a warning if bone counts don't match
        
        if (renderer.sharedMesh != null && renderer.sharedMesh.bindposes != null)
        {
            int newBoneCount = renderer.sharedMesh.bindposes.Length;
            int originalBoneCount = originalBones.Length;
            
            if (newBoneCount != originalBoneCount)
            {
                Debug.LogWarning($"CharacterLoader: Bone count mismatch on {renderer.name}. " +
                               $"Original: {originalBoneCount}, New: {newBoneCount}. " +
                               $"Consider using optimized meshes or implementing bone remapping.");
            }
        }
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