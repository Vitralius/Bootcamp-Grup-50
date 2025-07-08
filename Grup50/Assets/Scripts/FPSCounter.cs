using UnityEngine;

public class FPSCounter : MonoBehaviour
{
    [Header("FPS Display Settings")]
    public bool showFPS = true;
    public float updateInterval = 0.5f; // How often to update FPS display
    
    [Header("Display Style")]
    public Color textColor = Color.white;
    public int fontSize = 20;
    public Vector2 position = new Vector2(10, 10);
    
    private float accum = 0.0f;
    private int frames = 0;
    private float timeleft;
    private float fps;
    private string fpsText = "";
    private GUIStyle style;

    void Start()
    {
        timeleft = updateInterval;
        
        // Initialize GUI style
        style = new GUIStyle();
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = textColor;
        style.fontSize = fontSize;
        style.alignment = TextAnchor.UpperLeft;
        
        // Add shadow for better visibility
        style.normal.background = null;
    }

    void Update()
    {
        if (!showFPS) return;
        
        timeleft -= Time.deltaTime;
        accum += Time.timeScale / Time.deltaTime;
        ++frames;

        // Update FPS display every interval
        if (timeleft <= 0.0f)
        {
            fps = accum / frames;
            float msec = 1000.0f / fps;
            
            // Format the display text
            fpsText = string.Format(LocalizationManager.Instance?.GetLocalizedText("fps_counter") ?? "{0:0.} FPS ({1:0.0}ms)", fps, msec);
            
            timeleft = updateInterval;
            accum = 0.0f;
            frames = 0;
        }
    }

    void OnGUI()
    {
        if (!showFPS || string.IsNullOrEmpty(fpsText)) return;
        
        // Update style color in case it changed
        style.normal.textColor = textColor;
        style.fontSize = fontSize;
        
        // Calculate text size for proper background
        Vector2 textSize = style.CalcSize(new GUIContent(fpsText));
        Rect rect = new Rect(position.x, position.y, textSize.x + 10, textSize.y + 5);
        
        // Draw semi-transparent background for better readability
        Color originalColor = GUI.color;
        GUI.color = new Color(0, 0, 0, 0.5f);
        GUI.Box(rect, "");
        GUI.color = originalColor;
        
        // Draw the FPS text
        GUI.Label(rect, fpsText, style);
    }

    // Optional: Toggle FPS display with a key press
    void LateUpdate()
    {
        if (Input.GetKeyDown(KeyCode.F3))
        {
            showFPS = !showFPS;
        }
    }
}