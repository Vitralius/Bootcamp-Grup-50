using UnityEngine;
using System.Linq;

public class PreviewCharacterLoader : MonoBehaviour
{
    [Header("Preview Components")]
    [SerializeField] private SkinnedMeshRenderer skinnedMeshRenderer;
    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private Animator animator;
    
    [Header("Default Assets")]
    [SerializeField] private Mesh defaultMesh;
    [SerializeField] private Material[] defaultMaterials;
    [SerializeField] private RuntimeAnimatorController defaultAnimatorController;
    
    [Header("Preview Animation")]
    [SerializeField] private AnimationClip previewAnimationClip;
    [SerializeField] private AnimationClip[] fallbackAnimationClips;
    [SerializeField] private bool autoPlayPreviewAnimation = true;
    [SerializeField] private float previewAnimationDelay = 0.1f;
    
    private CharacterData currentCharacterData;
    
    private void Awake()
    {
        Debug.Log($"PreviewCharacterLoader: Awake on {gameObject.name}");
        
        // Auto-find components if not assigned
        if (skinnedMeshRenderer == null)
        {
            skinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            Debug.Log($"Found SkinnedMeshRenderer: {(skinnedMeshRenderer != null ? skinnedMeshRenderer.name : "NULL")}");
        }
        
        if (meshRenderer == null)
        {
            meshRenderer = GetComponentInChildren<MeshRenderer>();
            Debug.Log($"Found MeshRenderer: {(meshRenderer != null ? meshRenderer.name : "NULL")}");
        }
        
        if (meshFilter == null)
        {
            meshFilter = GetComponentInChildren<MeshFilter>();
            Debug.Log($"Found MeshFilter: {(meshFilter != null ? meshFilter.name : "NULL")}");
        }
        
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            Debug.Log($"Found Animator: {(animator != null ? animator.name : "NULL")}");
        }
        
        // Debug all found renderers
        var allRenderers = GetComponentsInChildren<Renderer>();
        Debug.Log($"All renderers found ({allRenderers.Length}): {string.Join(", ", allRenderers.Select(r => $"{r.GetType().Name}:{r.name}"))}");
        
        // Store defaults
        StoreDefaults();
        
        // Start with visibility disabled - will be enabled when character is loaded
        SetVisible(false);
        
        Debug.Log($"PreviewCharacterLoader: Awake complete. Visibility set to false.");
    }
    
    private void StoreDefaults()
    {
        if (defaultMesh == null && meshFilter != null)
            defaultMesh = meshFilter.sharedMesh;
        
        if (defaultMaterials == null || defaultMaterials.Length == 0)
        {
            if (skinnedMeshRenderer != null)
                defaultMaterials = skinnedMeshRenderer.sharedMaterials;
            else if (meshRenderer != null)
                defaultMaterials = meshRenderer.sharedMaterials;
        }
        
        if (defaultAnimatorController == null && animator != null)
            defaultAnimatorController = animator.runtimeAnimatorController;
    }
    
    /// <summary>
    /// Loads character data and applies it to the preview
    /// </summary>
    /// <param name="characterData">Character data to apply</param>
    public void LoadCharacter(CharacterData characterData)
    {
        if (characterData == null)
        {
            Debug.LogWarning("PreviewCharacterLoader: Attempted to load null character data");
            ResetToDefault();
            return;
        }
        
        currentCharacterData = characterData;
        ApplyCharacterData(characterData);
    }
    
    /// <summary>
    /// Loads character by ID from the registry
    /// </summary>
    /// <param name="characterID">Character ID to load</param>
    public void LoadCharacterByID(int characterID)
    {
        Debug.Log($"PreviewCharacterLoader: LoadCharacterByID called with ID {characterID}");
        
        if (CharacterRegistry.Instance == null)
        {
            Debug.LogError("PreviewCharacterLoader: CharacterRegistry not found");
            ResetToDefault();
            return;
        }
        
        CharacterData characterData = CharacterRegistry.Instance.GetCharacterByID(characterID);
        if (characterData != null)
        {
            Debug.Log($"PreviewCharacterLoader: Found character data for {characterData.characterName}");
            LoadCharacter(characterData);
        }
        else
        {
            Debug.LogWarning($"PreviewCharacterLoader: Character with ID {characterID} not found, trying to load first available character");
            // Try to load the first available character as fallback
            var allCharacters = CharacterRegistry.Instance.GetAllCharacters();
            if (allCharacters != null && allCharacters.Length > 0)
            {
                Debug.Log($"PreviewCharacterLoader: Loading fallback character {allCharacters[0].characterName}");
                LoadCharacter(allCharacters[0]);
            }
            else
            {
                Debug.LogError("PreviewCharacterLoader: No characters available in registry");
                ResetToDefault();
            }
        }
    }
    
    /// <summary>
    /// Applies character data to the preview components
    /// </summary>
    /// <param name="characterData">Character data to apply</param>
    private void ApplyCharacterData(CharacterData characterData)
    {
        Debug.Log($"PreviewCharacterLoader: ApplyCharacterData for {characterData.characterName}");
        Debug.Log($"- SkinnedMeshRenderer: {(skinnedMeshRenderer != null ? skinnedMeshRenderer.name : "NULL")}");
        Debug.Log($"- MeshRenderer: {(meshRenderer != null ? meshRenderer.name : "NULL")}");
        Debug.Log($"- MeshFilter: {(meshFilter != null ? meshFilter.name : "NULL")}");
        Debug.Log($"- Animator: {(animator != null ? animator.name : "NULL")}");
        Debug.Log($"- CharacterData skeletal mesh: {(characterData.skeletalMesh != null ? characterData.skeletalMesh.name : "NULL")}");
        Debug.Log($"- CharacterData materials count: {(characterData.characterMaterials != null ? characterData.characterMaterials.Length : 0)}");
        
        bool appliedAnyChanges = false;
        
        // PRIORITY 1: Apply skeletal mesh to SkinnedMeshRenderer (this should be the main renderer)
        if (characterData.skeletalMesh != null && skinnedMeshRenderer != null)
        {
            skinnedMeshRenderer.sharedMesh = characterData.skeletalMesh;
            Debug.Log($"✓ Applied skeletal mesh: {characterData.skeletalMesh.name}");
            appliedAnyChanges = true;
            
            // Ensure the SkinnedMeshRenderer is enabled and visible
            skinnedMeshRenderer.enabled = true;
            
            // Hide any MeshRenderer since we're using SkinnedMeshRenderer
            if (meshRenderer != null)
            {
                meshRenderer.enabled = false;
                Debug.Log($"✓ Disabled MeshRenderer to avoid conflicts with SkinnedMeshRenderer");
            }
        }
        else
        {
            Debug.LogWarning($"✗ Cannot apply skeletal mesh - mesh: {characterData.skeletalMesh != null}, renderer: {skinnedMeshRenderer != null}");
            
            // FALLBACK: Try to apply to MeshRenderer/MeshFilter if SkinnedMeshRenderer fails
            if (characterData.skeletalMesh != null && meshFilter != null && meshRenderer != null)
            {
                meshFilter.sharedMesh = characterData.skeletalMesh;
                meshRenderer.enabled = true;
                Debug.Log($"✓ FALLBACK: Applied skeletal mesh to MeshRenderer/MeshFilter: {characterData.skeletalMesh.name}");
                appliedAnyChanges = true;
            }
        }
        
        // Apply outfit mesh to MeshRenderer/MeshFilter ONLY if we don't have a skeletal mesh in SkinnedMeshRenderer
        if (characterData.outfitMeshes != null && characterData.outfitMeshes.Length > 0 && meshFilter != null && 
            (skinnedMeshRenderer == null || skinnedMeshRenderer.sharedMesh == null))
        {
            meshFilter.sharedMesh = characterData.outfitMeshes[0]; // Use first outfit mesh
            if (meshRenderer != null)
                meshRenderer.enabled = true;
            Debug.Log($"✓ Applied outfit mesh: {characterData.outfitMeshes[0].name}");
            appliedAnyChanges = true;
        }
        
        // Apply materials - PRIORITY to SkinnedMeshRenderer
        if (characterData.characterMaterials != null && characterData.characterMaterials.Length > 0)
        {
            bool materialApplied = false;
            
            // Apply to SkinnedMeshRenderer FIRST (main renderer)
            if (skinnedMeshRenderer != null && skinnedMeshRenderer.enabled)
            {
                var oldMaterials = skinnedMeshRenderer.sharedMaterials;
                skinnedMeshRenderer.sharedMaterials = characterData.characterMaterials;
                Debug.Log($"✓ Applied {characterData.characterMaterials.Length} materials to SkinnedMeshRenderer");
                Debug.Log($"  Old materials: {string.Join(", ", oldMaterials.Select(m => m?.name ?? "NULL"))}");
                Debug.Log($"  New materials: {string.Join(", ", characterData.characterMaterials.Select(m => m?.name ?? "NULL"))}");
                materialApplied = true;
                appliedAnyChanges = true;
            }
            // Only apply to MeshRenderer if SkinnedMeshRenderer is not being used
            else if (meshRenderer != null && meshRenderer.enabled)
            {
                var oldMaterials = meshRenderer.sharedMaterials;
                meshRenderer.sharedMaterials = characterData.characterMaterials;
                Debug.Log($"✓ Applied {characterData.characterMaterials.Length} materials to MeshRenderer");
                Debug.Log($"  Old materials: {string.Join(", ", oldMaterials.Select(m => m?.name ?? "NULL"))}");
                Debug.Log($"  New materials: {string.Join(", ", characterData.characterMaterials.Select(m => m?.name ?? "NULL"))}");
                materialApplied = true;
                appliedAnyChanges = true;
            }
            
            if (!materialApplied)
            {
                Debug.LogWarning("✗ No enabled renderers found to apply materials to!");
            }
        }
        else
        {
            Debug.LogWarning($"✗ No materials to apply - materials: {characterData.characterMaterials != null}, count: {characterData.characterMaterials?.Length ?? 0}");
        }
        
        // Apply animator controller
        if (characterData.animatorController != null && animator != null)
        {
            animator.runtimeAnimatorController = characterData.animatorController;
            Debug.Log($"✓ Applied animator controller: {characterData.animatorController.name}");
            appliedAnyChanges = true;
            
            // Set preview animation after a short delay to let animator initialize
            if (autoPlayPreviewAnimation)
            {
                Invoke(nameof(SetPreviewAnimationParameters), previewAnimationDelay);
            }
        }
        
        // Apply scale
        transform.localScale = characterData.characterScale;
        
        // Final validation - ensure something is visible
        bool somethingVisible = (skinnedMeshRenderer != null && skinnedMeshRenderer.enabled && skinnedMeshRenderer.sharedMesh != null) ||
                               (meshRenderer != null && meshRenderer.enabled && meshFilter != null && meshFilter.sharedMesh != null);
        
        if (appliedAnyChanges && somethingVisible)
        {
            Debug.Log($"✅ PreviewCharacterLoader: Successfully loaded character {characterData.characterName}");
        }
        else
        {
            Debug.LogError($"❌ PreviewCharacterLoader: Character {characterData.characterName} failed to load properly!");
            Debug.LogError($"  - Changes applied: {appliedAnyChanges}");
            Debug.LogError($"  - Something visible: {somethingVisible}");
            Debug.LogError($"  - SkinnedMeshRenderer: {(skinnedMeshRenderer != null ? $"enabled={skinnedMeshRenderer.enabled}, mesh={skinnedMeshRenderer.sharedMesh?.name}" : "NULL")}");
            Debug.LogError($"  - MeshRenderer: {(meshRenderer != null ? $"enabled={meshRenderer.enabled}" : "NULL")}");
        }
    }
    
    /// <summary>
    /// Resets the preview to default appearance
    /// </summary>
    public void ResetToDefault()
    {
        currentCharacterData = null;
        
        // Reset mesh
        if (meshFilter != null && defaultMesh != null)
        {
            meshFilter.sharedMesh = defaultMesh;
        }
        
        // Reset materials
        if (defaultMaterials != null && defaultMaterials.Length > 0)
        {
            if (skinnedMeshRenderer != null)
                skinnedMeshRenderer.sharedMaterials = defaultMaterials;
            if (meshRenderer != null)
                meshRenderer.sharedMaterials = defaultMaterials;
        }
        
        // Reset animator
        if (animator != null && defaultAnimatorController != null)
        {
            animator.runtimeAnimatorController = defaultAnimatorController;
            
            // Set default preview animation parameters
            if (autoPlayPreviewAnimation)
            {
                Invoke(nameof(SetPreviewAnimationParameters), previewAnimationDelay);
            }
        }
        
        // Reset scale
        transform.localScale = Vector3.one;
        
        Debug.Log("PreviewCharacterLoader: Reset to default");
    }
    
    /// <summary>
    /// Gets the currently loaded character data
    /// </summary>
    /// <returns>Current character data or null if none loaded</returns>
    public CharacterData GetCurrentCharacterData()
    {
        return currentCharacterData;
    }
    
    /// <summary>
    /// Checks if a character is currently loaded
    /// </summary>
    /// <returns>True if character is loaded, false otherwise</returns>
    public bool HasCharacterLoaded()
    {
        return currentCharacterData != null;
    }
    
    /// <summary>
    /// Sets the preview visibility
    /// </summary>
    /// <param name="visible">Whether the preview should be visible</param>
    public void SetVisible(bool visible)
    {
        Debug.Log($"PreviewCharacterLoader: SetVisible({visible})");
        
        // Enable/disable the main renderer being used
        if (skinnedMeshRenderer != null)
        {
            skinnedMeshRenderer.enabled = visible;
            Debug.Log($"✓ SkinnedMeshRenderer enabled: {visible}");
        }
        
        // Only enable MeshRenderer if SkinnedMeshRenderer is null or disabled
        if (meshRenderer != null)
        {
            bool shouldEnableMeshRenderer = visible && (skinnedMeshRenderer == null || !skinnedMeshRenderer.enabled);
            meshRenderer.enabled = shouldEnableMeshRenderer;
            Debug.Log($"✓ MeshRenderer enabled: {shouldEnableMeshRenderer}");
        }
        
        // Also control the entire GameObject visibility if needed
        gameObject.SetActive(visible);
        Debug.Log($"✓ GameObject active: {visible}");
    }
    
    /// <summary>
    /// Enables or disables animation playback
    /// </summary>
    /// <param name="enabled">Whether animations should play</param>
    public void SetAnimationEnabled(bool enabled)
    {
        if (animator != null)
            animator.enabled = enabled;
    }
    
    /// <summary>
    /// Sets the animation parameters for preview (idle state)
    /// </summary>
    private void SetPreviewAnimationParameters()
    {
        if (animator == null || !animator.enabled)
        {
            Debug.LogWarning("PreviewCharacterLoader: Cannot set preview animation parameters - animator is null or disabled");
            return;
        }
        
        if (animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("PreviewCharacterLoader: No animator controller assigned!");
            return;
        }
        
        Debug.Log($"PreviewCharacterLoader: Setting preview animation parameters for idle state");
        
        // Set parameters for idle/standing state
        try
        {
            // Set Speed to 0 for idle/standing
            animator.SetFloat("Speed", 0f);
            Debug.Log($"✓ Set Speed parameter to 0");
            
            // Set DirectionX and DirectionY to 0 for standing still
            animator.SetFloat("DirectionX", 0f);
            animator.SetFloat("DirectionY", 0f);
            Debug.Log($"✓ Set DirectionX and DirectionY parameters to 0");
            
            // Set other common parameters for idle state
            animator.SetFloat("MotionSpeed", 0f);
            animator.SetBool("Grounded", true);
            animator.SetBool("Jump", false);
            animator.SetBool("FreeFall", false);
            animator.SetBool("Crouched", false);
            animator.SetBool("Sliding", false);
            animator.SetBool("DoubleJump", false);
            
            Debug.Log($"✅ PreviewCharacterLoader: Successfully set all animation parameters for idle preview");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"PreviewCharacterLoader: Failed to set some animation parameters: {e.Message}");
            // Try fallback approach if parameter setting fails
            TryFallbackAnimation();
        }
    }
    
    /// <summary>
    /// Plays the preview animation (legacy method, kept for backwards compatibility)
    /// </summary>
    private void PlayPreviewAnimation()
    {
        // For blend tree systems, use parameter setting instead
        SetPreviewAnimationParameters();
    }
    
    /// <summary>
    /// Attempts to play animation by name
    /// </summary>
    private bool TryPlayAnimationByName(string animationName, int layer = -1)
    {
        try
        {
            if (layer >= 0)
            {
                animator.Play(animationName, layer);
            }
            else
            {
                animator.Play(animationName);
            }
            Debug.Log($"✓ Playing preview animation: {animationName} (layer: {layer})");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Failed to play animation '{animationName}': {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Attempts to play animation using CrossFade
    /// </summary>
    private bool TryPlayAnimationByCrossFade(string animationName)
    {
        try
        {
            animator.CrossFade(animationName, 0.1f);
            Debug.Log($"✓ CrossFading to preview animation: {animationName}");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Failed to crossfade to animation '{animationName}': {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Sets the preview animation clip
    /// </summary>
    /// <param name="animationClip">Animation clip to play</param>
    public void SetPreviewAnimation(AnimationClip animationClip)
    {
        previewAnimationClip = animationClip;
        Debug.Log($"PreviewCharacterLoader: Set preview animation to: {(animationClip != null ? animationClip.name : "NULL")}");
        
        // Set parameters immediately if auto-play is enabled and animator is ready
        if (autoPlayPreviewAnimation && animator != null && animator.enabled)
        {
            SetPreviewAnimationParameters();
        }
    }
    
    /// <summary>
    /// Manually triggers the preview animation
    /// </summary>
    public void TriggerPreviewAnimation()
    {
        if (animator != null && animator.enabled)
        {
            SetPreviewAnimationParameters();
        }
    }
    
    /// <summary>
    /// Tries fallback animations if the primary one fails
    /// </summary>
    private void TryFallbackAnimation()
    {
        if (animator == null || !animator.enabled)
            return;
        
        Debug.Log("PreviewCharacterLoader: Trying fallback animations...");
        
        // Try assigned fallback animation clips first
        if (fallbackAnimationClips != null && fallbackAnimationClips.Length > 0)
        {
            foreach (AnimationClip fallbackClip in fallbackAnimationClips)
            {
                if (fallbackClip != null && fallbackClip != previewAnimationClip)
                {
                    if (TryPlayAnimationByName(fallbackClip.name) || 
                        TryPlayAnimationByName($"Base Layer.{fallbackClip.name}") ||
                        TryPlayAnimationByCrossFade(fallbackClip.name))
                    {
                        Debug.Log($"✓ Playing fallback animation clip: {fallbackClip.name}");
                        return;
                    }
                }
            }
        }
        
        // If no fallback clips work, try common animation names
        string[] commonAnimationNames = { 
            "Idle", "Mixamo_Idle", "idle", "IDLE", 
            "Base Layer.Idle", "Locomotion.Idle", "Stand--Idle",
            "Grounded", "Locomotion", "FreeFall"
        };
        
        foreach (string fallbackName in commonAnimationNames)
        {
            string currentAnimationName = previewAnimationClip?.name ?? "";
            if (fallbackName != currentAnimationName)
            {
                if (TryPlayAnimationByName(fallbackName) || 
                    TryPlayAnimationByName(fallbackName, 0) ||
                    TryPlayAnimationByCrossFade(fallbackName))
                {
                    Debug.Log($"✓ Playing fallback animation by name: {fallbackName}");
                    return;
                }
            }
        }
        
        // Last resort: Try to get any available state from the controller
        if (animator.runtimeAnimatorController != null)
        {
            Debug.Log("PreviewCharacterLoader: Attempting to find any available animation state...");
            
            // Get all states from the first layer
            if (animator.layerCount > 0)
            {
                var animatorController = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
                if (animatorController != null)
                {
                    var stateMachine = animatorController.layers[0].stateMachine;
                    if (stateMachine.states.Length > 0)
                    {
                        string firstStateName = stateMachine.states[0].state.name;
                        if (TryPlayAnimationByName(firstStateName))
                        {
                            Debug.Log($"✓ Playing first available state: {firstStateName}");
                            return;
                        }
                    }
                }
            }
        }
        
        Debug.LogWarning("PreviewCharacterLoader: No fallback animations worked");
    }
    
    /// <summary>
    /// Gets the current preview animation clip
    /// </summary>
    /// <returns>Current preview animation clip</returns>
    public AnimationClip GetPreviewAnimationClip()
    {
        return previewAnimationClip;
    }
    
    /// <summary>
    /// Gets the current preview animation name
    /// </summary>
    /// <returns>Current preview animation name or empty string if null</returns>
    public string GetPreviewAnimationName()
    {
        return previewAnimationClip != null ? previewAnimationClip.name : "";
    }
    
    /// <summary>
    /// Gets whether auto-play preview animation is enabled
    /// </summary>
    /// <returns>True if auto-play is enabled</returns>
    public bool IsAutoPlayEnabled()
    {
        return autoPlayPreviewAnimation;
    }
    
    /// <summary>
    /// Sets whether preview animation should auto-play
    /// </summary>
    /// <param name="autoPlay">Whether to auto-play preview animation</param>
    public void SetAutoPlayEnabled(bool autoPlay)
    {
        autoPlayPreviewAnimation = autoPlay;
        Debug.Log($"PreviewCharacterLoader: Auto-play preview animation set to: {autoPlay}");
    }
    
    /// <summary>
    /// Sets the fallback animation clips
    /// </summary>
    /// <param name="fallbackClips">Array of fallback animation clips</param>
    public void SetFallbackAnimations(AnimationClip[] fallbackClips)
    {
        fallbackAnimationClips = fallbackClips;
        Debug.Log($"PreviewCharacterLoader: Set {(fallbackClips != null ? fallbackClips.Length : 0)} fallback animation clips");
    }
    
    /// <summary>
    /// Gets the fallback animation clips
    /// </summary>
    /// <returns>Array of fallback animation clips</returns>
    public AnimationClip[] GetFallbackAnimations()
    {
        return fallbackAnimationClips;
    }
    
    /// <summary>
    /// Debug method to list all available animation states in the controller
    /// </summary>
    [ContextMenu("Debug Available Animation States")]
    public void DebugAvailableAnimationStates()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("PreviewCharacterLoader: No animator or controller available for debugging");
            return;
        }
        
        Debug.Log($"=== Animation States in {animator.runtimeAnimatorController.name} ===");
        Debug.Log($"Layer count: {animator.layerCount}");
        
        #if UNITY_EDITOR
        var animatorController = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
        if (animatorController != null)
        {
            for (int i = 0; i < animatorController.layers.Length; i++)
            {
                var layer = animatorController.layers[i];
                Debug.Log($"Layer {i}: {layer.name}");
                
                foreach (var state in layer.stateMachine.states)
                {
                    Debug.Log($"  - State: {state.state.name}");
                    if (state.state.motion != null)
                    {
                        Debug.Log($"    Motion: {state.state.motion.name}");
                    }
                }
            }
        }
        #endif
        
        Debug.Log("=== End Animation States ===");
    }
}