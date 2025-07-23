using UnityEngine;
using Unity.Netcode;

/// <summary>
/// CleanCharacterLoader - Simple character mesh and material swapper
/// Replaces all complex character loading systems with one clean solution
/// Works with "Optimize Game Objects" enabled FBX files for same-skeleton mesh replacement
/// </summary>
public class UltraSimpleMeshSwapper : MonoBehaviour
{
    [Header("Target Components")]
    [SerializeField] private SkinnedMeshRenderer targetRenderer;
    [SerializeField] private Animator characterAnimator;
    
    [Header("Preview Mode")]
    [SerializeField] private bool isPreviewMode = false;
    
    [Header("Fallback Assets")]
    [SerializeField] private Mesh originalMesh;
    [SerializeField] private Material[] originalMaterials;
    [SerializeField] private RuntimeAnimatorController originalAnimatorController;
    
    [Header("Test Meshes (Legacy)")]
    [SerializeField] private Mesh boMesh;
    [SerializeField] private Mesh wizardMesh;
    
    // Current character data
    private CharacterData currentCharacterData;
    private bool isInitialized = false;
    
    /// <summary>
    /// Gets whether this is in preview mode
    /// </summary>
    public bool IsPreviewMode => isPreviewMode;
    
    /// <summary>
    /// Gets the current character data
    /// </summary>
    public CharacterData CurrentCharacterData => currentCharacterData;
    
    private void Awake()
    {
        // Don't initialize immediately - wait for proper setup
        Debug.Log($"CleanCharacterLoader: Awake on {gameObject.name} (Preview: {isPreviewMode})");
    }
    
    private void Start()
    {
        // Initialize in Start() to ensure all components are ready
        if (!isInitialized)
        {
            InitializeSafely();
        }
    }
    
    /// <summary>
    /// Safe initialization that can be called multiple times
    /// </summary>
    private void InitializeSafely()
    {
        if (isInitialized) return;
        
        InitializeComponents();
        StoreOriginals();
        isInitialized = true;
        
        Debug.Log($"CleanCharacterLoader: Successfully initialized on {gameObject.name} (Preview: {isPreviewMode})");
    }
    
    private void InitializeComponents()
    {
        // Auto-find target renderer if not assigned
        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            
            // If still not found, try to find any renderer on this object
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<SkinnedMeshRenderer>();
            }
        }
        
        // Auto-find animator if not in preview mode
        if (!isPreviewMode && characterAnimator == null)
        {
            characterAnimator = GetComponent<Animator>();
            if (characterAnimator == null)
                characterAnimator = GetComponentInChildren<Animator>();
        }
        
        // Warn if critical components are missing
        if (targetRenderer == null)
        {
            Debug.LogWarning($"CleanCharacterLoader: No SkinnedMeshRenderer found on {gameObject.name}. Character loading will not work properly.");
        }
        
        Debug.Log($"CleanCharacterLoader: Found - Renderer: {(targetRenderer?.name ?? "None")}, Animator: {(characterAnimator?.name ?? "None")}");
    }
    
    private void StoreOriginals()
    {
        if (targetRenderer != null)
        {
            if (originalMesh == null)
                originalMesh = targetRenderer.sharedMesh;
            
            if (originalMaterials == null || originalMaterials.Length == 0)
                originalMaterials = targetRenderer.sharedMaterials;
        }
        
        if (!isPreviewMode && characterAnimator != null && originalAnimatorController == null)
            originalAnimatorController = characterAnimator.runtimeAnimatorController;
    }
    
    /// <summary>
    /// Loads character data and applies all changes
    /// </summary>
    public bool LoadCharacter(CharacterData characterData)
    {
        if (characterData == null)
        {
            Debug.LogWarning("CleanCharacterLoader: Cannot load null character data");
            return false;
        }
        
        // Ensure we're initialized before loading
        if (!isInitialized)
        {
            Debug.Log("CleanCharacterLoader: Not initialized yet, initializing now...");
            InitializeSafely();
        }
        
        if (!isInitialized)
        {
            Debug.LogError("CleanCharacterLoader: Initialization failed");
            return false;
        }
        
        if (targetRenderer == null)
        {
            Debug.LogError($"CleanCharacterLoader: Missing SkinnedMeshRenderer on {gameObject.name}. Cannot load character {characterData.characterName}");
            return false;
        }
        
        Debug.Log($"CleanCharacterLoader: Loading character '{characterData.characterName}'");
        
        currentCharacterData = characterData;
        bool success = true;
        
        // Always apply visual changes
        success &= SwapMesh(characterData.skeletalMesh);
        success &= ApplyMaterials(characterData.characterMaterials);
        
        // Apply non-preview changes
        if (!isPreviewMode)
        {
            success &= ApplyAnimatorController(characterData.animatorController);
            success &= ApplyCharacterStats(characterData);
        }
        
        if (success)
        {
            Debug.Log($"CleanCharacterLoader: ✅ Successfully loaded '{characterData.characterName}'");
        }
        else
        {
            Debug.LogWarning($"CleanCharacterLoader: ⚠️ Partial loading of '{characterData.characterName}'");
        }
        
        return success;
    }
    
    /// <summary>
    /// Loads character by ID from CharacterRegistry
    /// </summary>
    public bool LoadCharacterByID(int characterID)
    {
        // Handle registry not ready yet
        if (CharacterRegistry.Instance == null)
        {
            Debug.LogWarning($"CleanCharacterLoader: CharacterRegistry not ready, retrying in 0.5s for character ID {characterID}");
            StartCoroutine(RetryLoadCharacterByID(characterID, 3)); // Retry up to 3 times
            return false;
        }
        
        CharacterData characterData = CharacterRegistry.Instance.GetCharacterByID(characterID);
        if (characterData == null)
        {
            Debug.LogWarning($"CleanCharacterLoader: Character ID {characterID} not found in registry");
            return false;
        }
        
        return LoadCharacter(characterData);
    }
    
    /// <summary>
    /// Retry loading character by ID if registry isn't ready
    /// </summary>
    private System.Collections.IEnumerator RetryLoadCharacterByID(int characterID, int maxRetries)
    {
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            yield return new WaitForSeconds(0.5f);
            
            if (CharacterRegistry.Instance != null)
            {
                Debug.Log($"CleanCharacterLoader: CharacterRegistry now available, loading character {characterID} (attempt {attempt + 1})");
                LoadCharacterByID(characterID);
                yield break;
            }
        }
        
        Debug.LogError($"CleanCharacterLoader: Failed to load character {characterID} after {maxRetries} attempts - CharacterRegistry still not available");
    }
    
    /// <summary>
    /// Swaps mesh - REQUIRES "Optimize Game Objects" to be ENABLED on both FBX imports
    /// </summary>
    public bool SwapMesh(Mesh newMesh)
    {
        if (targetRenderer == null || newMesh == null)
        {
            Debug.LogWarning("CleanCharacterLoader: Cannot swap mesh - missing renderer or mesh");
            return false;
        }
        
        Debug.Log($"CleanCharacterLoader: Swapping from '{targetRenderer.sharedMesh?.name}' to '{newMesh.name}'");
        
        // THIS IS ALL YOU NEED IF "OPTIMIZE GAME OBJECTS" IS ENABLED!
        targetRenderer.sharedMesh = newMesh;
        
        // Update bounds
        targetRenderer.localBounds = newMesh.bounds;
        
        Debug.Log($"CleanCharacterLoader: ✅ Mesh swap complete!");
        return true;
    }
    
    /// <summary>
    /// Applies materials to the renderer
    /// </summary>
    public bool ApplyMaterials(Material[] materials)
    {
        if (targetRenderer == null)
        {
            Debug.LogWarning("CleanCharacterLoader: Cannot apply materials - targetRenderer is null");
            return false;
        }
        
        if (materials == null || materials.Length == 0)
        {
            Debug.Log("CleanCharacterLoader: No materials to apply");
            return true;
        }
        
        try
        {
            // Use sharedMaterials to get current material slots (safe for prefabs)
            Material[] currentMaterials = targetRenderer.sharedMaterials;
            if (currentMaterials == null || currentMaterials.Length == 0)
            {
                Debug.LogWarning("CleanCharacterLoader: Target renderer has no material slots");
                return false;
            }
            
            // Apply materials with proper slot handling
            Material[] newMaterials = new Material[currentMaterials.Length];
            for (int i = 0; i < newMaterials.Length; i++)
            {
                newMaterials[i] = i < materials.Length ? materials[i] : materials[0];
            }
            
            // Apply materials (this creates instance materials, safe for runtime)
            targetRenderer.materials = newMaterials;
            Debug.Log($"CleanCharacterLoader: ✅ Applied {materials.Length} materials to {newMaterials.Length} slots");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"CleanCharacterLoader: Error applying materials: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Applies animator controller (only in non-preview mode)
    /// </summary>
    public bool ApplyAnimatorController(RuntimeAnimatorController controller)
    {
        if (isPreviewMode || characterAnimator == null)
            return true;
        
        if (controller == null)
        {
            Debug.Log("CleanCharacterLoader: No animator controller to apply");
            return true;
        }
        
        characterAnimator.runtimeAnimatorController = controller;
        characterAnimator.Rebind();
        
        Debug.Log($"CleanCharacterLoader: ✅ Applied animator controller '{controller.name}'");
        return true;
    }
    
    /// <summary>
    /// Applies character stats to ThirdPersonController (only in non-preview mode)
    /// </summary>
    public bool ApplyCharacterStats(CharacterData characterData)
    {
        if (isPreviewMode)
            return true;
        
        // Check network ownership
        var networkBehaviour = GetComponent<NetworkBehaviour>();
        if (networkBehaviour != null && !networkBehaviour.IsOwner)
        {
            Debug.Log("CleanCharacterLoader: Skipping stats (not owner)");
            return true;
        }
        
        // Find ThirdPersonController
        var controller = GetComponent<StarterAssets.ThirdPersonController>();
        if (controller == null)
        {
            Debug.LogWarning("CleanCharacterLoader: No ThirdPersonController found");
            return false;
        }
        
        // Apply basic stats
        controller.MoveSpeed = characterData.moveSpeed;
        controller.SprintSpeed = characterData.sprintSpeed;
        controller.JumpHeight = characterData.jumpHeight;
        controller.Gravity = characterData.gravity;
        controller.GroundedRadius = characterData.groundedRadius;
        
        Debug.Log($"CleanCharacterLoader: ✅ Applied stats for '{characterData.characterName}'");
        return true;
    }
    
    /// <summary>
    /// Resets to original appearance
    /// </summary>
    public void ResetToDefault()
    {
        if (!isInitialized)
            return;
        
        // Reset mesh
        if (targetRenderer != null && originalMesh != null)
        {
            targetRenderer.sharedMesh = originalMesh;
            targetRenderer.localBounds = originalMesh.bounds;
        }
        
        // Reset materials
        if (targetRenderer != null && originalMaterials != null)
            targetRenderer.materials = originalMaterials;
        
        // Reset animator
        if (!isPreviewMode && characterAnimator != null && originalAnimatorController != null)
        {
            characterAnimator.runtimeAnimatorController = originalAnimatorController;
            characterAnimator.Rebind();
        }
        
        currentCharacterData = null;
        Debug.Log("CleanCharacterLoader: ✅ Reset to default");
    }
    
    /// <summary>
    /// Sets visibility for preview mode
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (targetRenderer != null)
            targetRenderer.enabled = visible;
        
        gameObject.SetActive(visible);
    }
    
    /// <summary>
    /// Sets preview mode
    /// </summary>
    public void SetPreviewMode(bool preview)
    {
        bool wasPreview = isPreviewMode;
        isPreviewMode = preview;
        
        // If preview mode changed, re-initialize to update components appropriately
        if (wasPreview != preview)
        {
            Debug.Log($"CleanCharacterLoader: Preview mode changed from {wasPreview} to {preview}, re-initializing...");
            isInitialized = false; // Force re-initialization
            InitializeSafely();
        }
    }
    
    // Legacy test methods (keep for backwards compatibility)
    [ContextMenu("Swap to Bo")]
    public void SwapToBo()
    {
        SwapMesh(boMesh);
    }
    
    [ContextMenu("Swap to Wizard")]
    public void SwapToWizard()
    {
        SwapMesh(wizardMesh);
    }
    
    [ContextMenu("Test Character Data Swap")]
    public void TestCharacterDataSwap()
    {
        if (CharacterRegistry.Instance != null)
        {
            var characters = CharacterRegistry.Instance.GetAllCharacters();
            if (characters != null && characters.Length > 0)
            {
                LoadCharacter(characters[0]);
            }
        }
    }
    
    [ContextMenu("Debug State")]
    public void DebugState()
    {
        Debug.Log($"CleanCharacterLoader State:\n" +
                 $"- Initialized: {isInitialized}\n" +
                 $"- Preview Mode: {isPreviewMode}\n" +
                 $"- Current Character: {(currentCharacterData?.characterName ?? "None")}\n" +
                 $"- Target Renderer: {(targetRenderer?.name ?? "None")}\n" +
                 $"- Current Mesh: {(targetRenderer?.sharedMesh?.name ?? "None")}\n" +
                 $"- Animator: {(characterAnimator?.name ?? "None")}");
    }
}