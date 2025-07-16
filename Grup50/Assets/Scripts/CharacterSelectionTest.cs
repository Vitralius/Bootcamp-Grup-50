using UnityEngine;
using Unity.Netcode;

public class CharacterSelectionTest : NetworkBehaviour
{
    [Header("Testing")]
    [Tooltip("Character ID to test (0, 1, 2, etc.)")]
    public int testCharacterID = 0;
    
    [Header("UI")]
    [Tooltip("Show test UI buttons")]
    public bool showTestUI = true;
    
    private void Update()
    {
        // Only allow input for the owner/local player
        if (!IsOwner) return;
        
        // Keyboard shortcuts for testing
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SelectCharacter(0);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SelectCharacter(1);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            SelectCharacter(2);
        }
        else if (Input.GetKeyDown(KeyCode.R))
        {
            SetReady(!CharacterSelectionManager.Instance.IsPlayerReady(NetworkManager.Singleton.LocalClientId));
        }
    }
    
    private void OnGUI()
    {
        if (!showTestUI || !IsOwner) return;
        
        // Only show UI for local player
        GUILayout.BeginArea(new Rect(10, 10, 300, 400));
        
        GUILayout.Label("Character Selection Test", GUI.skin.box);
        GUILayout.Space(10);
        
        // Character selection buttons
        if (CharacterRegistry.Instance != null)
        {
            var allCharacters = CharacterRegistry.Instance.GetAllCharacters();
            
            GUILayout.Label("Select Character:");
            for (int i = 0; i < allCharacters.Length; i++)
            {
                var character = allCharacters[i];
                bool isSelected = CharacterSelectionManager.Instance.GetPlayerCharacterSelection(NetworkManager.Singleton.LocalClientId) == character.characterID;
                
                GUI.color = isSelected ? Color.green : Color.white;
                
                if (GUILayout.Button($"{character.characterName} (ID: {character.characterID})"))
                {
                    SelectCharacter(character.characterID);
                }
            }
            
            GUI.color = Color.white;
        }
        
        GUILayout.Space(10);
        
        // Ready state
        bool isReady = CharacterSelectionManager.Instance.IsPlayerReady(NetworkManager.Singleton.LocalClientId);
        GUI.color = isReady ? Color.green : Color.red;
        
        if (GUILayout.Button(isReady ? "Ready ✓" : "Not Ready ✗"))
        {
            SetReady(!isReady);
        }
        
        GUI.color = Color.white;
        GUILayout.Space(10);
        
        // Debug info
        GUILayout.Label("Debug Info:", GUI.skin.box);
        
        if (CharacterSelectionManager.Instance != null)
        {
            GUILayout.Label($"Local Client ID: {NetworkManager.Singleton.LocalClientId}");
            GUILayout.Label($"Selected Character: {CharacterSelectionManager.Instance.GetPlayerCharacterSelection(NetworkManager.Singleton.LocalClientId)}");
            GUILayout.Label($"Is Ready: {CharacterSelectionManager.Instance.IsPlayerReady(NetworkManager.Singleton.LocalClientId)}");
            GUILayout.Label($"All Players Ready: {CharacterSelectionManager.Instance.AreAllPlayersReady()}");
        }
        
        GUILayout.Space(10);
        
        // Keyboard shortcuts info
        GUILayout.Label("Keyboard Shortcuts:", GUI.skin.box);
        GUILayout.Label("1, 2, 3 - Select character");
        GUILayout.Label("R - Toggle ready");
        
        GUILayout.EndArea();
    }
    
    /// <summary>
    /// Selects a character by ID
    /// </summary>
    /// <param name="characterID">Character ID to select</param>
    private void SelectCharacter(int characterID)
    {
        if (CharacterSelectionManager.Instance != null)
        {
            CharacterSelectionManager.Instance.RequestCharacterSelection(characterID);
            Debug.Log($"CharacterSelectionTest: Requested character {characterID}");
        }
        else
        {
            Debug.LogError("CharacterSelectionTest: CharacterSelectionManager not found!");
        }
    }
    
    /// <summary>
    /// Sets the ready state
    /// </summary>
    /// <param name="ready">Ready state to set</param>
    private void SetReady(bool ready)
    {
        if (CharacterSelectionManager.Instance != null)
        {
            CharacterSelectionManager.Instance.SetPlayerReady(ready);
            Debug.Log($"CharacterSelectionTest: Set ready to {ready}");
        }
        else
        {
            Debug.LogError("CharacterSelectionTest: CharacterSelectionManager not found!");
        }
    }
    
    /// <summary>
    /// Test method to directly load a character on this player
    /// </summary>
    [ContextMenu("Test Load Character")]
    public void TestLoadCharacter()
    {
        var characterLoader = GetComponent<CharacterLoader>();
        if (characterLoader != null && CharacterRegistry.Instance != null)
        {
            var characterData = CharacterRegistry.Instance.GetCharacterByID(testCharacterID);
            if (characterData != null)
            {
                characterLoader.LoadCharacter(characterData);
                Debug.Log($"CharacterSelectionTest: Loaded character {characterData.characterName}");
            }
            else
            {
                Debug.LogError($"CharacterSelectionTest: Character with ID {testCharacterID} not found!");
            }
        }
        else
        {
            Debug.LogError("CharacterSelectionTest: CharacterLoader or CharacterRegistry not found!");
        }
    }
}