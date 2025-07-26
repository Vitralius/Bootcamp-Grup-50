using UnityEngine;
using Unity.Netcode;

/// <summary>
/// CleanCharacterLoader - Simple character mesh and material swapper
/// Replaces all complex character loading systems with one clean solution
/// Works with "Optimize Game Objects" enabled FBX files for same-skeleton mesh replacement
/// </summary>
public class UltraSimpleMeshSwapper : NetworkBehaviour
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
    
    // CRITICAL FIX: NetworkVariable for character synchronization across scene transitions
    private NetworkVariable<int> networkCharacterId = new NetworkVariable<int>(-1, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server);
    
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
    
    public override void OnNetworkSpawn()
    {
        Debug.Log($"CleanCharacterLoader: OnNetworkSpawn called on {gameObject.name} (IsOwner: {IsOwner})");
        
        // CRITICAL FIX: Subscribe to NetworkVariable changes for character sync
        networkCharacterId.OnValueChanged += OnNetworkCharacterIdChanged;
        
        // Initialize safely when network spawns
        if (!isInitialized)
        {
            InitializeSafely();
        }
        
        // CRITICAL FIX: Delay NetworkVariable operations to prevent Unity 2024 sync bug
        StartCoroutine(DelayedNetworkVariableApplication());
    }
    
    /// <summary>
    /// CRITICAL FIX: Delayed NetworkVariable application to prevent Unity Netcode 2024 sync bug
    /// during scene transitions where NetworkVariables can desync if modified during client loading
    /// </summary>
    private System.Collections.IEnumerator DelayedNetworkVariableApplication()
    {
        // Wait for proper network synchronization - especially important during scene transitions
        int maxAttempts = 30; // 3 seconds max wait
        int attempts = 0;
        
        while (attempts < maxAttempts)
        {
            // Check if network is fully synchronized and ready
            if (NetworkManager.Singleton != null && 
                IsSpawned && 
                NetworkManager.Singleton.IsConnectedClient)
            {
                // Additional check: wait for scene to be fully loaded
                if (SceneTransitionManager.Instance != null && 
                    (SceneTransitionManager.Instance.IsInMainMenu() || SceneTransitionManager.Instance.IsInGame()))
                {
                    break; // Network is ready
                }
            }
            
            attempts++;
            yield return new WaitForSeconds(0.1f);
        }
        
        if (attempts >= maxAttempts)
        {
            Debug.LogWarning($"CleanCharacterLoader: Network sync timeout after {maxAttempts} attempts");
        }
        
        // Now safe to apply NetworkVariable values
        if (networkCharacterId.Value != -1)
        {
            Debug.Log($"CleanCharacterLoader: Applying character from NetworkVariable after sync delay: {networkCharacterId.Value}");
            LoadCharacterByID(networkCharacterId.Value);
        }
        // Load character data if it's set locally (fallback)
        else if (currentCharacterData != null)
        {
            Debug.Log($"CleanCharacterLoader: Loading local character data after sync delay");
            LoadCharacter(currentCharacterData);
        }
    }
    
    private void Start()
    {
        // Initialize in Start() as fallback for non-networked objects
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
        {
            originalAnimatorController = characterAnimator.runtimeAnimatorController;
            
            // CRITICAL ROOT MOTION FIX: Ensure base animator has root motion disabled
            // This prevents twitching from the start
            if (characterAnimator.applyRootMotion)
            {
                Debug.LogWarning("CleanCharacterLoader: Disabling Root Motion on base animator to prevent character twitching");
                characterAnimator.applyRootMotion = false;
            }
        }
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
    /// Retry loading character by ID if registry isn't ready - IMPROVED to prevent stacking
    /// </summary>
    private System.Collections.IEnumerator RetryLoadCharacterByID(int characterID, int maxRetries)
    {
        // Prevent multiple retry coroutines for the same character
        string retryKey = $"retry_{characterID}";
        if (retryInProgress.Contains(retryKey))
        {
            Debug.Log($"CleanCharacterLoader: Retry already in progress for character {characterID}");
            yield break;
        }
        
        retryInProgress.Add(retryKey);
        
        try
        {
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                yield return new WaitForSeconds(0.5f);
                
                // Check if object was destroyed or network despawned
                if (this == null || !IsSpawned)
                {
                    Debug.Log($"CleanCharacterLoader: Object destroyed during retry for character {characterID}");
                    yield break;
                }
                
                if (CharacterRegistry.Instance != null)
                {
                    Debug.Log($"CleanCharacterLoader: CharacterRegistry now available, loading character {characterID} (attempt {attempt + 1})");
                    LoadCharacterByID(characterID);
                    yield break;
                }
            }
            
            Debug.LogError($"CleanCharacterLoader: Failed to load character {characterID} after {maxRetries} attempts - CharacterRegistry still not available");
        }
        finally
        {
            retryInProgress.Remove(retryKey);
        }
    }
    
    // Track retry operations to prevent stacking
    private System.Collections.Generic.HashSet<string> retryInProgress = new System.Collections.Generic.HashSet<string>();
    
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
    /// IMPROVED to prevent animation twitching and movement conflicts
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
        
        // CRITICAL FIX: Prevent animation twitching by properly handling animator state transitions
        try
        {
            // Store current animation state before switching
            bool wasEnabled = characterAnimator.enabled;
            
            // Temporarily disable animator to prevent conflicts during transition
            characterAnimator.enabled = false;
            
            // Apply new controller
            characterAnimator.runtimeAnimatorController = controller;
            
            // Wait a frame before re-enabling to let Unity process the controller change
            StartCoroutine(DelayedAnimatorRestart(wasEnabled));
            
            Debug.Log($"CleanCharacterLoader: ✅ Applied animator controller '{controller.name}' with twitching prevention");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"CleanCharacterLoader: Error applying animator controller: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Delayed animator restart to prevent twitching when switching controllers
    /// </summary>
    private System.Collections.IEnumerator DelayedAnimatorRestart(bool wasEnabled)
    {
        // Wait for the end of frame to ensure controller is properly set
        yield return new WaitForEndOfFrame();
        
        if (characterAnimator != null && characterAnimator.runtimeAnimatorController != null)
        {
            // CRITICAL ROOT MOTION FIX: Ensure root motion is disabled for CharacterController compatibility
            // This prevents twitching caused by animation-movement conflicts
            characterAnimator.applyRootMotion = false;
            
            // Re-enable animator
            characterAnimator.enabled = wasEnabled;
            
            // CRITICAL: Use Rebind() after re-enabling, not before
            if (wasEnabled)
            {
                characterAnimator.Rebind();
                
                // Force animator to update to initial state
                characterAnimator.Update(0f);
                
                Debug.Log("CleanCharacterLoader: Animator restarted successfully after controller change (Root Motion disabled)");
            }
        }
    }
    
    /// <summary>
    /// Applies character stats to ThirdPersonController (only in non-preview mode)
    /// </summary>
    public bool ApplyCharacterStats(CharacterData characterData)
    {
        if (isPreviewMode)
        {
            Debug.Log("CleanCharacterLoader: Skipping stats application (preview mode)");
            return true;
        }
        
        // Find ThirdPersonController
        var controller = GetComponent<StarterAssets.ThirdPersonController>();
        if (controller == null)
        {
            Debug.LogWarning("CleanCharacterLoader: No ThirdPersonController found");
            return false;
        }
        
        // CRITICAL FIX: Always apply stats regardless of ownership
        // Stats should be applied on all clients for consistency
        Debug.Log($"CleanCharacterLoader: Applying stats for '{characterData.characterName}' (IsOwner: {IsOwner})");
        Debug.Log($"CleanCharacterLoader: BEFORE - MoveSpeed: {controller.MoveSpeed}, SprintSpeed: {controller.SprintSpeed}");
        
        // Apply basic stats
        controller.MoveSpeed = characterData.moveSpeed;
        controller.SprintSpeed = characterData.sprintSpeed;
        controller.SpeedChangeRate = characterData.speedChangeRate;
        controller.JumpHeight = characterData.jumpHeight;
        controller.Gravity = characterData.gravity;
        controller.GroundedRadius = characterData.groundedRadius;
        
        Debug.Log($"CleanCharacterLoader: AFTER - MoveSpeed: {controller.MoveSpeed}, SprintSpeed: {controller.SprintSpeed}");
        Debug.Log($"CleanCharacterLoader: ✅ Applied stats for '{characterData.characterName}' - MoveSpeed: {characterData.moveSpeed}, SprintSpeed: {characterData.sprintSpeed}, SpeedChangeRate: {characterData.speedChangeRate}");
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
    /// NetworkVariable change callback - handles character synchronization across scene transitions
    /// IMPROVED with validation and error handling
    /// </summary>
    private void OnNetworkCharacterIdChanged(int previousValue, int newValue)
    {
        Debug.Log($"CleanCharacterLoader: NetworkVariable character changed from {previousValue} to {newValue} on {gameObject.name}");
        
        // Validate the change
        if (newValue == previousValue)
        {
            Debug.Log("CleanCharacterLoader: Character ID unchanged, skipping");
            return;
        }
        
        if (newValue == -1)
        {
            Debug.Log("CleanCharacterLoader: Character ID reset to -1, clearing character");
            // Could reset to default character here if needed
            return;
        }
        
        // Check if we're still spawned and network is ready
        if (!IsSpawned || NetworkManager.Singleton == null)
        {
            Debug.LogWarning("CleanCharacterLoader: Not spawned or NetworkManager null, deferring character load");
            return;
        }
        
        // Load character when NetworkVariable changes
        Debug.Log($"CleanCharacterLoader: Loading character {newValue} due to NetworkVariable change");
        LoadCharacterByID(newValue);
    }
    
    /// <summary>
    /// Sets character ID via NetworkVariable (Server authoritative)
    /// IMPROVED with validation and error handling
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void SetNetworkCharacterIdServerRpc(int characterId, ServerRpcParams serverRpcParams = default)
    {
        if (!IsServer)
        {
            Debug.LogWarning("CleanCharacterLoader: SetNetworkCharacterIdServerRpc called but not server");
            return;
        }
        
        if (!IsSpawned)
        {
            Debug.LogWarning("CleanCharacterLoader: SetNetworkCharacterIdServerRpc called but not spawned");
            return;
        }
        
        Debug.Log($"CleanCharacterLoader: SetNetworkCharacterIdServerRpc called - Character: {characterId} for {gameObject.name}");
        
        // Validate character ID
        if (characterId < -1)
        {
            Debug.LogWarning($"CleanCharacterLoader: Invalid character ID {characterId}, ignoring");
            return;
        }
        
        // Only update if different
        if (networkCharacterId.Value != characterId)
        {
            networkCharacterId.Value = characterId;
            Debug.Log($"CleanCharacterLoader: NetworkVariable updated to {characterId}");
        }
        else
        {
            Debug.Log($"CleanCharacterLoader: Character ID {characterId} already set, skipping update");
        }
    }
    
    /// <summary>
    /// Public method to set character that works across network
    /// IMPROVED with validation and error handling
    /// </summary>
    public void SetNetworkCharacterId(int characterId)
    {
        if (!IsSpawned)
        {
            Debug.LogWarning($"CleanCharacterLoader: Cannot set character {characterId} - not spawned yet");
            return;
        }
        
        if (NetworkManager.Singleton == null)
        {
            Debug.LogWarning($"CleanCharacterLoader: Cannot set character {characterId} - NetworkManager is null");
            return;
        }
        
        if (IsServer)
        {
            Debug.Log($"CleanCharacterLoader: Setting NetworkVariable character {characterId} directly (Server)");
            
            // Validate character ID
            if (characterId < -1)
            {
                Debug.LogWarning($"CleanCharacterLoader: Invalid character ID {characterId}, ignoring");
                return;
            }
            
            // Only update if different
            if (networkCharacterId.Value != characterId)
            {
                networkCharacterId.Value = characterId;
                Debug.Log($"CleanCharacterLoader: NetworkVariable updated to {characterId}");
            }
        }
        else
        {
            Debug.Log($"CleanCharacterLoader: Requesting character {characterId} via ServerRpc (Client)");
            SetNetworkCharacterIdServerRpc(characterId);
        }
    }
    
    /// <summary>
    /// Gets the current network character ID
    /// </summary>
    public int GetNetworkCharacterId()
    {
        return networkCharacterId.Value;
    }
    
    public override void OnNetworkDespawn()
    {
        // Clean up NetworkVariable subscription
        if (networkCharacterId != null)
        {
            networkCharacterId.OnValueChanged -= OnNetworkCharacterIdChanged;
        }
        
        // Clear retry operations
        if (retryInProgress != null)
        {
            retryInProgress.Clear();
        }
        
        // Stop any running coroutines
        StopAllCoroutines();
        
        Debug.Log($"CleanCharacterLoader: OnNetworkDespawn - cleanup complete for {gameObject.name}");
        
        base.OnNetworkDespawn();
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