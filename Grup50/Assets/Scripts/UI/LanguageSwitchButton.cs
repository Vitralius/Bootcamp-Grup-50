using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LanguageSwitchButton : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button languageButton;
    [SerializeField] private TextMeshProUGUI buttonText;
    
    void Start()
    {
        SetupButton();
        UpdateButtonText();
    }
    
    void OnEnable()
    {
        LocalizationManager.OnLanguageChanged += UpdateButtonText;
    }
    
    void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= UpdateButtonText;
    }
    
    private void SetupButton()
    {
        if (languageButton == null)
            languageButton = GetComponent<Button>();
        
        if (buttonText == null)
            buttonText = GetComponentInChildren<TextMeshProUGUI>();
        
        if (languageButton != null)
            languageButton.onClick.AddListener(OnLanguageButtonClicked);
    }
    
    private void OnLanguageButtonClicked()
    {
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.SwitchLanguage();
        }
    }
    
    private void UpdateButtonText()
    {
        if (LocalizationManager.Instance != null && buttonText != null)
        {
            // Show the opposite language name (what we'll switch TO)
            string targetLanguage = LocalizationManager.Instance.currentLanguage == LocalizationManager.Language.English ? "Turkish" : "English";
            string localizedKey = targetLanguage == "Turkish" ? "language_turkish" : "language_english";
            
            buttonText.text = LocalizationManager.Instance.GetLocalizedText(localizedKey);
        }
    }
    
    void OnDestroy()
    {
        if (languageButton != null)
            languageButton.onClick.RemoveAllListeners();
    }
}