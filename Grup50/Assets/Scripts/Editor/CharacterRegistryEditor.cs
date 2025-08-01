using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class CharacterRegistryEditor
{
    /// <summary>
    /// Auto-assigns unique IDs to characters that don't have them (Editor only)
    /// </summary>
    [MenuItem("Tools/Character System/Auto-Assign Character IDs")]
    public static void AutoAssignIDs()
    {
        var registry = CharacterRegistry.Instance;
        if (registry == null) 
        {
            Debug.LogError("CharacterRegistry not found. Please create a CharacterRegistry asset in Resources folder.");
            return;
        }
        
        var availableCharacters = registry.GetAllCharacters();
        var usedIDs = new HashSet<int>();
        int nextID = 0;
        
        // First pass: collect existing valid IDs
        foreach (var character in availableCharacters)
        {
            if (character != null && character.characterID >= 0)
            {
                usedIDs.Add(character.characterID);
            }
        }
        
        // Second pass: assign IDs to characters that need them
        foreach (var character in availableCharacters)
        {
            if (character != null && character.characterID < 0)
            {
                while (usedIDs.Contains(nextID))
                {
                    nextID++;
                }
                
                character.characterID = nextID;
                usedIDs.Add(nextID);
                
                EditorUtility.SetDirty(character);
            }
        }
        
        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets();
        
        Debug.Log($"CharacterRegistry: Auto-assigned IDs for characters");
    }
    
    /// <summary>
    /// Validates the registry and logs any issues (Editor only)
    /// </summary>
    [MenuItem("Tools/Character System/Validate Character Registry")]
    public static void ValidateRegistryEditor()
    {
        var registry = CharacterRegistry.Instance;
        if (registry == null) 
        {
            Debug.LogError("CharacterRegistry not found. Please create a CharacterRegistry asset in Resources folder.");
            return;
        }
        
        var issues = registry.ValidateRegistry();
        
        if (issues.Count == 0)
        {
            Debug.Log("CharacterRegistry: Validation passed - no issues found");
        }
        else
        {
            Debug.LogWarning($"CharacterRegistry: Validation found {issues.Count} issues:");
            foreach (var issue in issues)
            {
                Debug.LogWarning($"- {issue}");
            }
        }
    }
    
    /// <summary>
    /// Creates a new CharacterRegistry asset in the Resources folder
    /// </summary>
    [MenuItem("Tools/Character System/Create Character Registry")]
    public static void CreateCharacterRegistry()
    {
        // Check if one already exists
        var existing = Resources.Load<CharacterRegistry>("CharacterRegistry");
        if (existing != null)
        {
            Debug.LogWarning("CharacterRegistry already exists in Resources folder.");
            EditorGUIUtility.PingObject(existing);
            return;
        }
        
        // Create Resources folder if it doesn't exist
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }
        
        // Create the registry asset
        var registry = ScriptableObject.CreateInstance<CharacterRegistry>();
        AssetDatabase.CreateAsset(registry, "Assets/Resources/CharacterRegistry.asset");
        AssetDatabase.SaveAssets();
        
        Debug.Log("CharacterRegistry created at Assets/Resources/CharacterRegistry.asset");
        EditorGUIUtility.PingObject(registry);
    }
}