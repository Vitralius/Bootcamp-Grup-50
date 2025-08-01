using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "CharacterRegistry", menuName = "Character System/Character Registry")]
public class CharacterRegistry : ScriptableObject
{
    [Header("Available Characters")]
    [Tooltip("List of all available characters in the game")]
    [SerializeField] private List<CharacterData> availableCharacters = new List<CharacterData>();
    
    // Static instance for easy access
    private static CharacterRegistry instance;
    
    /// <summary>
    /// Gets the singleton instance of the character registry
    /// </summary>
    public static CharacterRegistry Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<CharacterRegistry>("CharacterRegistry");
                if (instance == null)
                {
                    Debug.LogError("CharacterRegistry: No CharacterRegistry found in Resources folder. Please create one.");
                }
            }
            return instance;
        }
    }
    
    /// <summary>
    /// Gets all available characters
    /// </summary>
    /// <returns>Array of all available CharacterData</returns>
    public CharacterData[] GetAllCharacters()
    {
        return availableCharacters.ToArray();
    }
    
    /// <summary>
    /// Gets a character by its ID
    /// </summary>
    /// <param name="characterID">ID of the character to find</param>
    /// <returns>CharacterData if found, null otherwise</returns>
    public CharacterData GetCharacterByID(int characterID)
    {
        return availableCharacters.FirstOrDefault(character => character.characterID == characterID);
    }
    
    /// <summary>
    /// Gets a character by its name
    /// </summary>
    /// <param name="characterName">Name of the character to find</param>
    /// <returns>CharacterData if found, null otherwise</returns>
    public CharacterData GetCharacterByName(string characterName)
    {
        return availableCharacters.FirstOrDefault(character => 
            character.characterName.Equals(characterName, System.StringComparison.OrdinalIgnoreCase));
    }
    
    /// <summary>
    /// Checks if a character with the given ID exists
    /// </summary>
    /// <param name="characterID">ID to check</param>
    /// <returns>True if character exists, false otherwise</returns>
    public bool HasCharacter(int characterID)
    {
        return availableCharacters.Any(character => character.characterID == characterID);
    }
    
    /// <summary>
    /// Gets the total number of available characters
    /// </summary>
    /// <returns>Number of available characters</returns>
    public int GetCharacterCount()
    {
        return availableCharacters.Count;
    }
    
    /// <summary>
    /// Gets character IDs that are currently available for selection
    /// </summary>
    /// <param name="excludeIDs">IDs to exclude from the result (e.g., already selected characters)</param>
    /// <returns>Array of available character IDs</returns>
    public int[] GetAvailableCharacterIDs(int[] excludeIDs = null)
    {
        var availableIDs = availableCharacters.Select(character => character.characterID);
        
        if (excludeIDs != null && excludeIDs.Length > 0)
        {
            availableIDs = availableIDs.Where(id => !excludeIDs.Contains(id));
        }
        
        return availableIDs.ToArray();
    }
    
    /// <summary>
    /// Gets the default character (first one in the list or with ID 0)
    /// </summary>
    /// <returns>Default CharacterData</returns>
    public CharacterData GetDefaultCharacter()
    {
        // Try to find character with ID 0 first
        var defaultChar = GetCharacterByID(0);
        if (defaultChar != null)
            return defaultChar;
        
        // Otherwise return the first character
        return availableCharacters.FirstOrDefault();
    }
    
    /// <summary>
    /// Validates that all characters have unique IDs
    /// </summary>
    /// <returns>True if all IDs are unique, false otherwise</returns>
    public bool ValidateUniqueIDs()
    {
        var ids = availableCharacters.Select(character => character.characterID).ToList();
        return ids.Count == ids.Distinct().Count();
    }
    
    /// <summary>
    /// Adds a character to the registry (Editor only)
    /// </summary>
    /// <param name="characterData">Character to add</param>
    public void AddCharacter(CharacterData characterData)
    {
        if (characterData == null)
        {
            Debug.LogWarning("CharacterRegistry: Attempted to add null character data");
            return;
        }
        
        if (HasCharacter(characterData.characterID))
        {
            Debug.LogWarning($"CharacterRegistry: Character with ID {characterData.characterID} already exists");
            return;
        }
        
        availableCharacters.Add(characterData);
        
        #if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        #endif
    }
    
    /// <summary>
    /// Removes a character from the registry (Editor only)
    /// </summary>
    /// <param name="characterID">ID of character to remove</param>
    public void RemoveCharacter(int characterID)
    {
        var characterToRemove = GetCharacterByID(characterID);
        if (characterToRemove != null)
        {
            availableCharacters.Remove(characterToRemove);
            
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }
    }
    
    /// <summary>
    /// Validates the registry for any issues
    /// </summary>
    /// <returns>List of validation issues found</returns>
    public List<string> ValidateRegistry()
    {
        var issues = new List<string>();
        
        // Check for null characters
        if (availableCharacters.Any(character => character == null))
        {
            issues.Add("Registry contains null character data entries");
        }
        
        // Check for duplicate IDs
        if (!ValidateUniqueIDs())
        {
            issues.Add("Registry contains duplicate character IDs");
        }
        
        // Check for empty names
        if (availableCharacters.Any(character => character != null && string.IsNullOrEmpty(character.characterName)))
        {
            issues.Add("Registry contains characters with empty names");
        }
        
        // Check for negative IDs
        if (availableCharacters.Any(character => character != null && character.characterID < 0))
        {
            issues.Add("Registry contains characters with negative IDs");
        }
        
        return issues;
    }
    
    
    private void OnValidate()
    {
        // Ensure no duplicate IDs during editing
        #if UNITY_EDITOR
        if (!ValidateUniqueIDs())
        {
            Debug.LogWarning("CharacterRegistry: Duplicate character IDs detected. Please ensure all characters have unique IDs.");
        }
        #endif
    }
}