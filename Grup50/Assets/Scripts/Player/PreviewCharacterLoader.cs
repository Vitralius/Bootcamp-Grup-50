using UnityEngine;

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
    
    private CharacterData currentCharacterData;
    
    private void Awake()
    {
        // Auto-find components if not assigned
        if (skinnedMeshRenderer == null)
            skinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        
        if (meshRenderer == null)
            meshRenderer = GetComponentInChildren<MeshRenderer>();
        
        if (meshFilter == null)
            meshFilter = GetComponentInChildren<MeshFilter>();
        
        if (animator == null)
            animator = GetComponent<Animator>();
        
        // Store defaults
        StoreDefaults();
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
        if (CharacterRegistry.Instance == null)
        {
            Debug.LogError("PreviewCharacterLoader: CharacterRegistry not found");
            ResetToDefault();
            return;
        }
        
        CharacterData characterData = CharacterRegistry.Instance.GetCharacterByID(characterID);
        if (characterData != null)
        {
            LoadCharacter(characterData);
        }
        else
        {
            Debug.LogWarning($"PreviewCharacterLoader: Character with ID {characterID} not found, trying to load first available character");
            // Try to load the first available character as fallback
            var allCharacters = CharacterRegistry.Instance.GetAllCharacters();
            if (allCharacters != null && allCharacters.Length > 0)
            {
                LoadCharacter(allCharacters[0]);
            }
            else
            {
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
        // Apply skeletal mesh to SkinnedMeshRenderer
        if (characterData.skeletalMesh != null && skinnedMeshRenderer != null)
        {
            skinnedMeshRenderer.sharedMesh = characterData.skeletalMesh;
            Debug.Log($"Applied skeletal mesh: {characterData.skeletalMesh.name}");
        }
        
        // Apply outfit mesh to MeshRenderer/MeshFilter
        if (characterData.outfitMeshes != null && characterData.outfitMeshes.Length > 0 && meshFilter != null)
        {
            meshFilter.sharedMesh = characterData.outfitMeshes[0]; // Use first outfit mesh
            Debug.Log($"Applied outfit mesh: {characterData.outfitMeshes[0].name}");
        }
        
        // Apply materials
        if (characterData.characterMaterials != null && characterData.characterMaterials.Length > 0)
        {
            if (skinnedMeshRenderer != null)
            {
                skinnedMeshRenderer.sharedMaterials = characterData.characterMaterials;
            }
            if (meshRenderer != null)
            {
                meshRenderer.sharedMaterials = characterData.characterMaterials;
            }
            Debug.Log($"Applied {characterData.characterMaterials.Length} materials");
        }
        
        // Apply animator controller
        if (characterData.animatorController != null && animator != null)
        {
            animator.runtimeAnimatorController = characterData.animatorController;
            Debug.Log($"Applied animator controller: {characterData.animatorController.name}");
        }
        
        // Apply scale
        transform.localScale = characterData.characterScale;
        
        Debug.Log($"PreviewCharacterLoader: Loaded character {characterData.characterName}");
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
        if (skinnedMeshRenderer != null)
            skinnedMeshRenderer.enabled = visible;
        if (meshRenderer != null)
            meshRenderer.enabled = visible;
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
}