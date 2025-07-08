using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LocalizedText : MonoBehaviour
{
    [Header("Localization Key")]
    public string localizationKey;
    
    [Header("Text Format (optional)")]
    public bool useStringFormat = false;
    
    [Header("Dynamic Content")]
    [Tooltip("Check this if the text content is managed dynamically by scripts")]
    public bool isDynamicContent = false;
    
    private Text legacyText;
    private TextMeshProUGUI tmpText;
    
    void Awake()
    {
        legacyText = GetComponent<Text>();
        tmpText = GetComponent<TextMeshProUGUI>();
        
        if (legacyText == null && tmpText == null)
        {
            Debug.LogError($"LocalizedText on {gameObject.name} requires either Text or TextMeshProUGUI component");
        }
    }
    
    void Start()
    {
        if (!isDynamicContent)
        {
            UpdateText();
        }
    }
    
    void OnEnable()
    {
        if (!isDynamicContent)
        {
            LocalizationManager.OnLanguageChanged += UpdateText;
        }
    }
    
    void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= UpdateText;
    }
    
    void UpdateText()
    {
        if (LocalizationManager.Instance != null && !string.IsNullOrEmpty(localizationKey) && !isDynamicContent)
        {
            string localizedString = LocalizationManager.Instance.GetLocalizedText(localizationKey);
            
            if (legacyText != null)
            {
                legacyText.text = localizedString;
            }
            else if (tmpText != null)
            {
                tmpText.text = localizedString;
            }
        }
    }
    
    public void SetText(string key)
    {
        localizationKey = key;
        UpdateText();
    }
    
    public void SetTextWithFormat(string key, params object[] args)
    {
        if (LocalizationManager.Instance != null)
        {
            string localizedString = LocalizationManager.Instance.GetLocalizedText(key);
            string formattedString = string.Format(localizedString, args);
            
            if (legacyText != null)
            {
                legacyText.text = formattedString;
            }
            else if (tmpText != null)
            {
                tmpText.text = formattedString;
            }
        }
    }
}