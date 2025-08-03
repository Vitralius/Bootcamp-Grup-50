using System.Collections.Generic;
using UnityEngine;
using System;

public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance { get; private set; }
    
    [Header("Language JSON Files")]
    public TextAsset englishJson;
    public TextAsset turkishJson;
    
    [Header("Current Language")]
    public Language currentLanguage = Language.English;
    
    private Dictionary<string, string> localizedText = new Dictionary<string, string>();
    
    public static event Action OnLanguageChanged;
    
    public enum Language
    {
        English,
        Turkish
    }
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadLanguage(currentLanguage);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    public void LoadLanguage(Language language)
    {
        currentLanguage = language;
        localizedText.Clear();
        
        TextAsset jsonFile = language == Language.English ? englishJson : turkishJson;
        
        if (jsonFile != null)
        {
            try
            {
                LocalizationData data = JsonUtility.FromJson<LocalizationData>(jsonFile.text);
                
                foreach (var item in data.items)
                {
                    localizedText[item.key] = item.value;
                }
                
                Debug.Log($"Loaded {localizedText.Count} localized strings for {language}");
                OnLanguageChanged?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load localization file for {language}: {e.Message}");
            }
        }
        else
        {
            Debug.LogError($"No JSON file assigned for {language}");
        }
    }
    
    public string GetLocalizedText(string key)
    {
        if (localizedText.ContainsKey(key))
        {
            return localizedText[key];
        }
        
        Debug.LogWarning($"Localization key '{key}' not found. Using key as fallback.");
        return key;
    }
    
    public void SwitchLanguage()
    {
        Language newLanguage = currentLanguage == Language.English ? Language.Turkish : Language.English;
        LoadLanguage(newLanguage);
    }
    
    public void SetLanguage(Language language)
    {
        if (language != currentLanguage)
        {
            LoadLanguage(language);
        }
    }
    
    void Start()
    {
        string savedLanguage = PlayerPrefs.GetString("Language", "English");
        Language language = savedLanguage == "Turkish" ? Language.Turkish : Language.English;
        LoadLanguage(language);
    }
    
    void OnDestroy()
    {
        if (Instance == this)
        {
            PlayerPrefs.SetString("Language", currentLanguage.ToString());
        }
    }
}

[System.Serializable]
public class LocalizationData
{
    public LocalizationItem[] items;
}

[System.Serializable]
public class LocalizationItem
{
    public string key;
    public string value;
}