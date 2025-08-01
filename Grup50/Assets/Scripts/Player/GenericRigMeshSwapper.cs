using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// GenericRigMeshSwapper - For Generic animation type rigs
/// Handles bone mapping by name for mesh replacement
/// </summary>
public class GenericRigMeshSwapper : MonoBehaviour
{
    [Header("Target Renderer")]
    [SerializeField] private SkinnedMeshRenderer targetRenderer;
    
    [Header("Source Templates")]
    [SerializeField] private SkinnedMeshRenderer boTemplate;
    [SerializeField] private SkinnedMeshRenderer wizardTemplate;
    
    private void Start()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
    }
    
    /// <summary>
    /// Swaps mesh from a template renderer with proper bone mapping
    /// </summary>
    public void SwapFromTemplate(SkinnedMeshRenderer sourceTemplate)
    {
        if (targetRenderer == null || sourceTemplate == null)
        {
            Debug.LogError("GenericRigMeshSwapper: Missing target or source renderer");
            return;
        }
        
        Debug.Log($"GenericRigMeshSwapper: Swapping from template '{sourceTemplate.name}'");
        Debug.Log($"GenericRigMeshSwapper: Source bones: {sourceTemplate.bones?.Length ?? 0}");
        Debug.Log($"GenericRigMeshSwapper: Target bones: {targetRenderer.bones?.Length ?? 0}");
        
        // Store the new mesh
        Mesh newMesh = sourceTemplate.sharedMesh;
        
        // Create bone mapping dictionary from target hierarchy
        Dictionary<string, Transform> targetBoneMap = new Dictionary<string, Transform>();
        Transform[] allTargetTransforms = transform.GetComponentsInChildren<Transform>();
        
        foreach (Transform t in allTargetTransforms)
        {
            if (!targetBoneMap.ContainsKey(t.name))
                targetBoneMap[t.name] = t;
        }
        
        Debug.Log($"GenericRigMeshSwapper: Found {targetBoneMap.Count} target transforms");
        
        // Map source bones to target bones by name
        Transform[] newBones = new Transform[sourceTemplate.bones.Length];
        int successfulMappings = 0;
        
        for (int i = 0; i < sourceTemplate.bones.Length; i++)
        {
            if (sourceTemplate.bones[i] != null)
            {
                string boneName = sourceTemplate.bones[i].name;
                
                if (targetBoneMap.TryGetValue(boneName, out Transform targetBone))
                {
                    newBones[i] = targetBone;
                    successfulMappings++;
                }
                else
                {
                    Debug.LogWarning($"GenericRigMeshSwapper: Could not find bone '{boneName}' in target hierarchy");
                    
                    // Try common name variations
                    string[] variations = {
                        boneName.Replace("mixamorig:", ""),
                        "mixamorig:" + boneName,
                        boneName.Replace("_", ""),
                        boneName.Replace(".", "_")
                    };
                    
                    foreach (string variation in variations)
                    {
                        if (targetBoneMap.TryGetValue(variation, out Transform variantBone))
                        {
                            newBones[i] = variantBone;
                            successfulMappings++;
                            Debug.Log($"GenericRigMeshSwapper: Found bone using variation '{variation}' for '{boneName}'");
                            break;
                        }
                    }
                }
            }
        }
        
        Debug.Log($"GenericRigMeshSwapper: Mapped {successfulMappings}/{sourceTemplate.bones.Length} bones");
        
        if (successfulMappings == 0)
        {
            Debug.LogError("GenericRigMeshSwapper: No bones could be mapped! Check bone names.");
            return;
        }
        
        // Apply the changes
        targetRenderer.sharedMesh = newMesh;
        targetRenderer.bones = newBones;
        
        // Set root bone if source has one
        if (sourceTemplate.rootBone != null)
        {
            string rootBoneName = sourceTemplate.rootBone.name;
            if (targetBoneMap.TryGetValue(rootBoneName, out Transform targetRootBone))
            {
                targetRenderer.rootBone = targetRootBone;
                Debug.Log($"GenericRigMeshSwapper: Set root bone to '{rootBoneName}'");
            }
        }
        
        // Update bounds
        if (targetRenderer.sharedMesh != null)
        {
            targetRenderer.localBounds = targetRenderer.sharedMesh.bounds;
        }
        
        // Force refresh
        targetRenderer.enabled = false;
        targetRenderer.enabled = true;
        
        Debug.Log($"GenericRigMeshSwapper: ✅ Mesh swap completed successfully!");
    }
    
    [ContextMenu("Swap to Bo")]
    public void SwapToBo()
    {
        SwapFromTemplate(boTemplate);
    }
    
    [ContextMenu("Swap to Wizard")]
    public void SwapToWizard()
    {
        SwapFromTemplate(wizardTemplate);
    }
    
    /// <summary>
    /// Debug method to compare bone structures
    /// </summary>
    [ContextMenu("Debug Bone Structures")]
    public void DebugBoneStructures()
    {
        Debug.Log("=== TARGET RENDERER BONES ===");
        if (targetRenderer?.bones != null)
        {
            for (int i = 0; i < targetRenderer.bones.Length; i++)
            {
                Debug.Log($"Target[{i}]: {targetRenderer.bones[i]?.name ?? "NULL"}");
            }
        }
        
        Debug.Log("=== BO TEMPLATE BONES ===");
        if (boTemplate?.bones != null)
        {
            for (int i = 0; i < boTemplate.bones.Length; i++)
            {
                Debug.Log($"Bo[{i}]: {boTemplate.bones[i]?.name ?? "NULL"}");
            }
        }
        
        Debug.Log("=== WIZARD TEMPLATE BONES ===");
        if (wizardTemplate?.bones != null)
        {
            for (int i = 0; i < wizardTemplate.bones.Length; i++)
            {
                Debug.Log($"Wizard[{i}]: {wizardTemplate.bones[i]?.name ?? "NULL"}");
            }
        }
    }
}