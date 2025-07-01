using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class ButtonAction : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private TMP_Text joinCodeDisplay;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button createSessionButton;
    [SerializeField] private Button joinSessionButton;
    [SerializeField] private Button leaveSessionButton;
    [SerializeField] private Button copyJoinCodeButton;
    
    private SessionManager sessionManager;
    
    void Start()
    {
        sessionManager = FindFirstObjectByType<SessionManager>();
        if (sessionManager == null)
        {
            Debug.LogError("SessionManager not found!");
            return;
        }
        
        // Subscribe to session events
        SessionManager.OnSessionCreated += OnSessionCreated;
        SessionManager.OnSessionJoined += OnSessionJoined;
        SessionManager.OnSessionError += OnSessionError;
        
        // Initialize UI state
        UpdateUIState("Ready to create or join session");
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        SessionManager.OnSessionCreated -= OnSessionCreated;
        SessionManager.OnSessionJoined -= OnSessionJoined;
        SessionManager.OnSessionError -= OnSessionError;
    }
    
    public void CreateSession()
    {
        UpdateUIState("Creating session...");
        sessionManager.CreateSession();
    }
    
    public void JoinSession()
    {
        if (joinCodeInput == null || string.IsNullOrEmpty(joinCodeInput.text))
        {
            UpdateUIState("Please enter a join code");
            return;
        }
        
        UpdateUIState("Joining session...");
        sessionManager.JoinSession(joinCodeInput.text.ToUpper());
    }
    
    public void LeaveSession()
    {
        UpdateUIState("Leaving session...");
        sessionManager.LeaveSession();
    }
    
    public void CopyJoinCode()
    {
        if (joinCodeDisplay != null && !string.IsNullOrEmpty(joinCodeDisplay.text))
        {
            GUIUtility.systemCopyBuffer = joinCodeDisplay.text;
            UpdateUIState("Join code copied to clipboard!");
            
            // Brief visual feedback
            if (copyJoinCodeButton != null)
            {
                StartCoroutine(CopyButtonFeedback());
            }
        }
        else
        {
            UpdateUIState("No join code to copy!");
        }
    }
    
    private System.Collections.IEnumerator CopyButtonFeedback()
    {
        // Store original text
        TMP_Text buttonText = copyJoinCodeButton.GetComponentInChildren<TMP_Text>();
        string originalText = buttonText?.text ?? "Copy";
        
        // Change button text briefly
        if (buttonText != null)
        {
            buttonText.text = "Copied!";
            yield return new WaitForSeconds(1.5f);
            buttonText.text = originalText;
        }
    }
    
    private void OnSessionCreated(string joinCode)
    {
        if (joinCodeDisplay != null)
            joinCodeDisplay.text = joinCode;
        
        UpdateUIState($"Session created! Share code: {joinCode}");
        SetButtonsInteractable(false, false, true);
    }
    
    private void OnSessionJoined()
    {
        UpdateUIState("Successfully joined session!");
        SetButtonsInteractable(false, false, true);
    }
    
    private void OnSessionError(string error)
    {
        UpdateUIState($"Error: {error}");
        SetButtonsInteractable(true, true, false);
    }
    
    private void UpdateUIState(string message)
    {
        if (statusText != null)
            statusText.text = message;
        
        Debug.Log($"Session Status: {message}");
    }
    
    private void SetButtonsInteractable(bool create, bool join, bool leave)
    {
        if (createSessionButton != null)
            createSessionButton.interactable = create;
        if (joinSessionButton != null)
            joinSessionButton.interactable = join;
        if (leaveSessionButton != null)
            leaveSessionButton.interactable = leave;
        
        // Copy button is only active when there's a join code to copy
        if (copyJoinCodeButton != null)
        {
            bool hasJoinCode = joinCodeDisplay != null && !string.IsNullOrEmpty(joinCodeDisplay.text);
            copyJoinCodeButton.interactable = hasJoinCode;
        }
    }
}
