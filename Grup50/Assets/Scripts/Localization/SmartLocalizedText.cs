using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SmartLocalizedText : MonoBehaviour
{
    [Header("Localization")]
    public string localizationKey;
    
    [Header("Smart String Tokens")]
    [SerializeField] private List<TokenData> tokens = new List<TokenData>();
    
    private Text legacyText;
    private TextMeshProUGUI tmpText;
    private string lastProcessedText;
    private Dictionary<string, object> tokenValues = new Dictionary<string, object>();
    
    [System.Serializable]
    public class TokenData
    {
        public string tokenName;
        public TokenType type;
        public string stringValue;
        public int intValue;
        public float floatValue;
        public bool boolValue;
    }
    
    public enum TokenType
    {
        String,
        Integer,
        Float,
        Boolean
    }
    
    void Awake()
    {
        legacyText = GetComponent<Text>();
        tmpText = GetComponent<TextMeshProUGUI>();
        
        if (legacyText == null && tmpText == null)
        {
            Debug.LogError($"SmartLocalizedText on {gameObject.name} requires either Text or TextMeshProUGUI component");
        }
        
        InitializeTokens();
    }
    
    void Start()
    {
        UpdateText();
    }
    
    void OnEnable()
    {
        LocalizationManager.OnLanguageChanged += OnLanguageChanged;
    }
    
    void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
    }
    
    private void InitializeTokens()
    {
        tokenValues.Clear();
        foreach (var token in tokens)
        {
            switch (token.type)
            {
                case TokenType.String:
                    tokenValues[token.tokenName] = token.stringValue;
                    break;
                case TokenType.Integer:
                    tokenValues[token.tokenName] = token.intValue;
                    break;
                case TokenType.Float:
                    tokenValues[token.tokenName] = token.floatValue;
                    break;
                case TokenType.Boolean:
                    tokenValues[token.tokenName] = token.boolValue;
                    break;
            }
        }
    }
    
    private void OnLanguageChanged()
    {
        UpdateText();
    }
    
    public void UpdateText()
    {
        if (LocalizationManager.Instance == null || string.IsNullOrEmpty(localizationKey))
            return;
            
        string localizedText = LocalizationManager.Instance.GetLocalizedText(localizationKey);
        string processedText = ProcessSmartString(localizedText);
        
        if (legacyText != null)
        {
            legacyText.text = processedText;
        }
        else if (tmpText != null)
        {
            tmpText.text = processedText;
        }
        
        lastProcessedText = processedText;
    }
    
    private string ProcessSmartString(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
            
        // Pattern to match {tokenName} or {tokenName:format}
        string pattern = @"\{(\w+)(?::([^}]+))?\}";
        
        return Regex.Replace(text, pattern, (match) =>
        {
            string tokenName = match.Groups[1].Value;
            string format = match.Groups[2].Value;
            
            if (tokenValues.ContainsKey(tokenName))
            {
                object value = tokenValues[tokenName];
                
                if (!string.IsNullOrEmpty(format))
                {
                    return FormatValue(value, format);
                }
                else
                {
                    return value.ToString();
                }
            }
            
            // If token not found, return the original placeholder
            Debug.LogWarning($"Token '{tokenName}' not found in SmartLocalizedText on {gameObject.name}");
            return match.Value;
        });
    }
    
    private string FormatValue(object value, string format)
    {
        try
        {
            if (value is int intVal)
            {
                return intVal.ToString(format);
            }
            else if (value is float floatVal)
            {
                return floatVal.ToString(format);
            }
            else if (value is string strVal)
            {
                return strVal;
            }
            else if (value is bool boolVal)
            {
                return format.Contains("|") ? 
                    format.Split('|')[boolVal ? 0 : 1] : 
                    boolVal.ToString();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error formatting value {value} with format {format}: {e.Message}");
        }
        
        return value.ToString();
    }
    
    public void SetToken(string tokenName, object value)
    {
        tokenValues[tokenName] = value;
        
        // Update the token data for inspector display
        var existingToken = tokens.Find(t => t.tokenName == tokenName);
        if (existingToken != null)
        {
            UpdateTokenData(existingToken, value);
        }
        else
        {
            // Add new token
            var newToken = new TokenData { tokenName = tokenName };
            UpdateTokenData(newToken, value);
            tokens.Add(newToken);
        }
        
        UpdateText();
    }
    
    private void UpdateTokenData(TokenData token, object value)
    {
        switch (value)
        {
            case string s:
                token.type = TokenType.String;
                token.stringValue = s;
                break;
            case int i:
                token.type = TokenType.Integer;
                token.intValue = i;
                break;
            case float f:
                token.type = TokenType.Float;
                token.floatValue = f;
                break;
            case bool b:
                token.type = TokenType.Boolean;
                token.boolValue = b;
                break;
            default:
                token.type = TokenType.String;
                token.stringValue = value.ToString();
                break;
        }
    }
    
    public void SetTokens(Dictionary<string, object> newTokens)
    {
        foreach (var kvp in newTokens)
        {
            tokenValues[kvp.Key] = kvp.Value;
        }
        UpdateText();
    }
    
    public string GetCurrentText()
    {
        return lastProcessedText;
    }
    
    // Quick helper methods for common use cases
    public void SetPlayerCount(int current, int max)
    {
        SetToken("currentPlayers", current);
        SetToken("maxPlayers", max);
    }
    
    public void SetLobbyCode(string code)
    {
        SetToken("lobbyCode", code);
    }
    
    public void SetSceneName(string sceneName)
    {
        SetToken("sceneName", sceneName);
    }
    
    public void SetReadyStatus(bool isReady)
    {
        SetToken("isReady", isReady);
    }
}