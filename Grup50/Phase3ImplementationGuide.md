# Phase 3 Implementation Guide - Polish & Basic Features

## Overview
This guide covers implementing UI systems, visual effects, audio, and game state management to polish the survivorlike experience. Focus on essential features that enhance player feedback and game feel.

## 🖥️ Step 1: Basic UI System

### Files to Create:
- `Assets/Scripts/UI/GameplayUI.cs` (New)
- `Assets/Scripts/UI/HealthBar.cs` (New)  
- `Assets/Scripts/UI/ExperienceBar.cs` (New)
- `Assets/Scripts/UI/WaveDisplay.cs` (New)

### Implementation Steps:

#### 1.1 Create Gameplay UI Canvas:

```csharp
// Create Canvas Structure:
Canvas (Screen Space - Overlay)
├── HealthBar Panel (Top-left)
│   ├── Health Fill Image (Red)
│   └── Health Text (100/100)
├── ExperienceBar Panel (Bottom-center)
│   ├── XP Background Image
│   ├── XP Fill Image (Cyan)
│   └── Level Text (Level 5)
├── WaveInfo Panel (Top-right)
│   ├── Wave Text (Wave 3)
│   └── Enemy Count Text (12 Enemies)
└── Debug Panel (Optional)
    ├── FPS Counter
    └── Player Stats
```

#### 1.2 HealthBar.cs Implementation:

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HealthBar : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private Image healthFillImage;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private HealthComponent targetHealthComponent;
    
    [Header("Visual Settings")]
    [SerializeField] private Color fullHealthColor = Color.green;
    [SerializeField] private Color lowHealthColor = Color.red;
    [SerializeField] private float lowHealthThreshold = 0.3f;
    
    void Start()
    {
        // Find player's health component if not assigned
        if (targetHealthComponent == null)
        {
            var player = FindObjectOfType<ExperienceComponent>();
            if (player != null)
                targetHealthComponent = player.GetComponent<HealthComponent>();
        }
        
        // Subscribe to health changes
        if (targetHealthComponent != null)
        {
            targetHealthComponent.OnHealthChanged += UpdateHealthDisplay;
            UpdateHealthDisplay(targetHealthComponent.currentHealth, targetHealthComponent.maxHealth);
        }
    }
    
    private void UpdateHealthDisplay(float currentHealth, float maxHealth)
    {
        if (healthFillImage != null)
        {
            float healthPercent = currentHealth / maxHealth;
            healthFillImage.fillAmount = healthPercent;
            
            // Color based on health percentage
            Color targetColor = Color.Lerp(lowHealthColor, fullHealthColor, 
                healthPercent / lowHealthThreshold);
            healthFillImage.color = targetColor;
        }
        
        if (healthText != null)
        {
            healthText.text = $"{currentHealth:F0}/{maxHealth:F0}";
        }
    }
    
    void OnDestroy()
    {
        if (targetHealthComponent != null)
            targetHealthComponent.OnHealthChanged -= UpdateHealthDisplay;
    }
}
```

#### 1.3 ExperienceBar.cs Implementation:

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ExperienceBar : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private Image xpFillImage;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI xpText;
    [SerializeField] private ExperienceComponent targetExperienceComponent;
    
    [Header("Visual Settings")]
    [SerializeField] private Color xpBarColor = Color.cyan;
    [SerializeField] private AnimationCurve levelUpAnimation;
    
    void Start()
    {
        // Find player's experience component if not assigned
        if (targetExperienceComponent == null)
        {
            targetExperienceComponent = FindObjectOfType<ExperienceComponent>();
        }
        
        // Subscribe to experience events
        if (targetExperienceComponent != null)
        {
            targetExperienceComponent.OnXPChanged += UpdateXPDisplay;
            targetExperienceComponent.OnLevelUp += OnLevelUp;
            UpdateXPDisplay(targetExperienceComponent.CurrentXP, targetExperienceComponent.XPToNextLevel);
        }
    }
    
    private void UpdateXPDisplay(float currentXP, float xpToNext)
    {
        if (xpFillImage != null)
        {
            float xpPercent = currentXP / xpToNext;
            xpFillImage.fillAmount = xpPercent;
        }
        
        if (levelText != null)
        {
            levelText.text = $"Level {targetExperienceComponent.CurrentLevel}";
        }
        
        if (xpText != null)
        {
            xpText.text = $"{currentXP:F0}/{xpToNext:F0} XP";
        }
    }
    
    private void OnLevelUp(int newLevel)
    {
        // Level up animation/effect
        StartCoroutine(PlayLevelUpEffect());
    }
    
    private System.Collections.IEnumerator PlayLevelUpEffect()
    {
        // Simple scale animation
        Vector3 originalScale = transform.localScale;
        
        for (float t = 0; t < 1f; t += Time.deltaTime * 3f)
        {
            float scale = 1f + (0.2f * levelUpAnimation.Evaluate(t));
            transform.localScale = originalScale * scale;
            yield return null;
        }
        
        transform.localScale = originalScale;
    }
    
    void OnDestroy()
    {
        if (targetExperienceComponent != null)
        {
            targetExperienceComponent.OnXPChanged -= UpdateXPDisplay;
            targetExperienceComponent.OnLevelUp -= OnLevelUp;
        }
    }
}
```

#### 1.4 WaveDisplay.cs Implementation:

```csharp
using UnityEngine;
using TMPro;

public class WaveDisplay : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private TextMeshProUGUI waveText;
    [SerializeField] private TextMeshProUGUI enemyCountText;
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private WaveManager targetWaveManager;
    
    void Start()
    {
        // Find wave manager if not assigned
        if (targetWaveManager == null)
        {
            targetWaveManager = FindObjectOfType<WaveManager>();
        }
        
        // Subscribe to wave events
        if (targetWaveManager != null)
        {
            targetWaveManager.OnWaveStarted += OnWaveStarted;
        }
    }
    
    void Update()
    {
        if (targetWaveManager != null)
        {
            // Update wave info
            if (waveText != null)
                waveText.text = $"Wave {targetWaveManager.CurrentWave}";
            
            if (enemyCountText != null)
                enemyCountText.text = $"{targetWaveManager.CurrentEnemyCount} Enemies";
            
            // Update time (optional)
            if (timeText != null)
            {
                int minutes = Mathf.FloorToInt(Time.time / 60f);
                int seconds = Mathf.FloorToInt(Time.time % 60f);
                timeText.text = $"{minutes:00}:{seconds:00}";
            }
        }
    }
    
    private void OnWaveStarted(int waveNumber)
    {
        // Wave start notification
        Debug.Log($"🌊 Wave {waveNumber} Started!");
    }
    
    void OnDestroy()
    {
        if (targetWaveManager != null)
            targetWaveManager.OnWaveStarted -= OnWaveStarted;
    }
}
```

## ✨ Step 2: Visual Effects System

### Files to Create:
- `Assets/Scripts/VFX/EffectManager.cs` (New)
- `Assets/Scripts/VFX/MuzzleFlash.cs` (New)
- `Assets/Scripts/VFX/HitEffect.cs` (New)
- `Assets/Scripts/VFX/DeathEffect.cs` (New)

### Implementation Steps:

#### 2.1 Create Effect Prefabs:

```
Effect Prefabs to Create:
1. MuzzleFlash Prefab:
   - Particle System (burst of sparks)
   - Light component (brief flash)
   - Audio Source (gunshot sound)

2. HitEffect Prefab:
   - Particle System (impact sparks)
   - Decal projector (bullet hole)
   - Audio Source (hit sound)

3. DeathEffect Prefab:
   - Particle System (explosion/smoke)
   - Screen shake trigger
   - Audio Source (death sound)
```

#### 2.2 EffectManager.cs Implementation:

```csharp
using UnityEngine;

public class EffectManager : MonoBehaviour
{
    public static EffectManager Instance;
    
    [Header("Effect Prefabs")]
    [SerializeField] private GameObject muzzleFlashPrefab;
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private GameObject deathEffectPrefab;
    [SerializeField] private GameObject xpOrbCollectEffectPrefab;
    
    [Header("Screen Shake")]
    [SerializeField] private float shakeDuration = 0.1f;
    [SerializeField] private float shakeIntensity = 0.3f;
    
    private Camera mainCamera;
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        mainCamera = Camera.main;
    }
    
    public void PlayMuzzleFlash(Vector3 position, Vector3 direction)
    {
        if (muzzleFlashPrefab != null)
        {
            GameObject effect = Instantiate(muzzleFlashPrefab, position, Quaternion.LookRotation(direction));
            Destroy(effect, 1f);
        }
    }
    
    public void PlayHitEffect(Vector3 position, Vector3 normal)
    {
        if (hitEffectPrefab != null)
        {
            GameObject effect = Instantiate(hitEffectPrefab, position, Quaternion.LookRotation(normal));
            Destroy(effect, 2f);
        }
    }
    
    public void PlayDeathEffect(Vector3 position)
    {
        if (deathEffectPrefab != null)
        {
            GameObject effect = Instantiate(deathEffectPrefab, position, Quaternion.identity);
            Destroy(effect, 3f);
        }
        
        // Screen shake for dramatic effect
        StartCoroutine(ScreenShake());
    }
    
    public void PlayXPCollectEffect(Vector3 position)
    {
        if (xpOrbCollectEffectPrefab != null)
        {
            GameObject effect = Instantiate(xpOrbCollectEffectPrefab, position, Quaternion.identity);
            Destroy(effect, 1f);
        }
    }
    
    private System.Collections.IEnumerator ScreenShake()
    {
        if (mainCamera == null) yield break;
        
        Vector3 originalPosition = mainCamera.transform.localPosition;
        float elapsed = 0f;
        
        while (elapsed < shakeDuration)
        {
            float x = Random.Range(-1f, 1f) * shakeIntensity;
            float y = Random.Range(-1f, 1f) * shakeIntensity;
            
            mainCamera.transform.localPosition = originalPosition + new Vector3(x, y, 0);
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        mainCamera.transform.localPosition = originalPosition;
    }
}
```

## 🔊 Step 3: Audio System

### Files to Create:
- `Assets/Scripts/Audio/AudioManager.cs` (New)
- `Assets/Scripts/Audio/SoundEffect.cs` (New)

### Implementation Steps:

#### 3.1 AudioManager.cs Implementation:

```csharp
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;
    
    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource voiceSource;
    
    [Header("Music")]
    [SerializeField] private AudioClip backgroundMusic;
    [SerializeField] private AudioClip menuMusic;
    
    [Header("SFX")]
    [SerializeField] private AudioClip[] weaponSounds;
    [SerializeField] private AudioClip[] enemySounds;
    [SerializeField] private AudioClip levelUpSound;
    [SerializeField] private AudioClip xpCollectSound;
    [SerializeField] private AudioClip waveStartSound;
    
    [Header("Settings")]
    [SerializeField] private float masterVolume = 1f;
    [SerializeField] private float musicVolume = 0.7f;
    [SerializeField] private float sfxVolume = 1f;
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        PlayBackgroundMusic();
    }
    
    public void PlayBackgroundMusic()
    {
        if (musicSource != null && backgroundMusic != null)
        {
            musicSource.clip = backgroundMusic;
            musicSource.volume = musicVolume * masterVolume;
            musicSource.loop = true;
            musicSource.Play();
        }
    }
    
    public void PlayWeaponSound(int weaponType = 0)
    {
        if (weaponSounds != null && weaponSounds.Length > weaponType)
        {
            PlaySFX(weaponSounds[weaponType]);
        }
    }
    
    public void PlayEnemySound(int soundIndex = 0)
    {
        if (enemySounds != null && enemySounds.Length > soundIndex)
        {
            PlaySFX(enemySounds[soundIndex]);
        }
    }
    
    public void PlayLevelUpSound()
    {
        PlaySFX(levelUpSound);
    }
    
    public void PlayXPCollectSound()
    {
        PlaySFX(xpCollectSound);
    }
    
    public void PlayWaveStartSound()
    {
        PlaySFX(waveStartSound);
    }
    
    private void PlaySFX(AudioClip clip)
    {
        if (sfxSource != null && clip != null)
        {
            sfxSource.PlayOneShot(clip, sfxVolume * masterVolume);
        }
    }
    
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        UpdateVolumeSettings();
    }
    
    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        UpdateVolumeSettings();
    }
    
    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
    }
    
    private void UpdateVolumeSettings()
    {
        if (musicSource != null)
            musicSource.volume = musicVolume * masterVolume;
    }
}
```

## 🎮 Step 4: Game State Management

### Files to Create:
- `Assets/Scripts/GameStates/GameStateManager.cs` (New)
- `Assets/Scripts/GameStates/GameOverUI.cs` (New)
- `Assets/Scripts/GameStates/PauseMenuUI.cs` (New)

### Implementation Steps:

#### 4.1 GameStateManager.cs Implementation:

```csharp
using Unity.Netcode;
using UnityEngine;

public class GameStateManager : NetworkBehaviour
{
    public static GameStateManager Instance;
    
    public enum GameState
    {
        MainMenu,
        Playing,
        Paused,
        GameOver,
        Victory
    }
    
    [Header("Game Settings")]
    [SerializeField] private float gameTimeLimit = 900f; // 15 minutes
    [SerializeField] private int waveCountForVictory = 10;
    
    // Network synchronized game state
    private NetworkVariable<GameState> currentGameState = new NetworkVariable<GameState>(GameState.Playing);
    private NetworkVariable<float> gameTime = new NetworkVariable<float>(0f);
    private NetworkVariable<bool> allPlayersDead = new NetworkVariable<bool>(false);
    
    // Events
    public System.Action<GameState> OnGameStateChanged;
    public System.Action OnGameOver;
    public System.Action OnVictory;
    
    public GameState CurrentState => currentGameState.Value;
    public float GameTime => gameTime.Value;
    public bool IsGameActive => CurrentState == GameState.Playing;
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        currentGameState.OnValueChanged += OnGameStateChangedNetwork;
        
        if (IsServer)
        {
            StartGame();
        }
    }
    
    void Update()
    {
        if (!IsServer || !IsGameActive) return;
        
        // Update game time
        gameTime.Value += Time.deltaTime;
        
        // Check win/lose conditions
        CheckGameConditions();
    }
    
    private void CheckGameConditions()
    {
        // Check time limit
        if (gameTime.Value >= gameTimeLimit)
        {
            TriggerVictory();
            return;
        }
        
        // Check wave victory condition
        WaveManager waveManager = FindObjectOfType<WaveManager>();
        if (waveManager != null && waveManager.CurrentWave >= waveCountForVictory)
        {
            TriggerVictory();
            return;
        }
        
        // Check if all players are dead
        if (AreAllPlayersDead())
        {
            TriggerGameOver();
        }
    }
    
    private bool AreAllPlayersDead()
    {
        HealthComponent[] playerHealths = FindObjectsOfType<HealthComponent>();
        
        foreach (var health in playerHealths)
        {
            // Check if this is a player (has ExperienceComponent)
            if (health.GetComponent<ExperienceComponent>() != null)
            {
                if (health.currentHealth > 0)
                    return false; // At least one player is alive
            }
        }
        
        return true; // All players are dead
    }
    
    public void StartGame()
    {
        if (!IsServer) return;
        
        currentGameState.Value = GameState.Playing;
        gameTime.Value = 0f;
        allPlayersDead.Value = false;
        
        Debug.Log("Game Started!");
    }
    
    public void PauseGame()
    {
        if (!IsServer) return;
        
        if (CurrentState == GameState.Playing)
        {
            currentGameState.Value = GameState.Paused;
            Time.timeScale = 0f;
        }
    }
    
    public void ResumeGame()
    {
        if (!IsServer) return;
        
        if (CurrentState == GameState.Paused)
        {
            currentGameState.Value = GameState.Playing;
            Time.timeScale = 1f;
        }
    }
    
    public void TriggerGameOver()
    {
        if (!IsServer) return;
        
        currentGameState.Value = GameState.GameOver;
        allPlayersDead.Value = true;
        
        Debug.Log("Game Over!");
        OnGameOver?.Invoke();
        
        // Stop wave spawning
        WaveManager waveManager = FindObjectOfType<WaveManager>();
        if (waveManager != null)
        {
            waveManager.StopWaves();
        }
    }
    
    public void TriggerVictory()
    {
        if (!IsServer) return;
        
        currentGameState.Value = GameState.Victory;
        
        Debug.Log("Victory!");
        OnVictory?.Invoke();
        
        // Stop wave spawning
        WaveManager waveManager = FindObjectOfType<WaveManager>();
        if (waveManager != null)
        {
            waveManager.StopWaves();
        }
    }
    
    public void RestartGame()
    {
        if (!IsServer) return;
        
        // Reset time scale
        Time.timeScale = 1f;
        
        // Reload scene or reset systems
        // This would typically reload the scene
        SceneTransitionManager.Instance?.LoadGameplayScene();
    }
    
    private void OnGameStateChangedNetwork(GameState previousState, GameState newState)
    {
        Debug.Log($"Game State Changed: {previousState} → {newState}");
        OnGameStateChanged?.Invoke(newState);
    }
    
    public override void OnNetworkDespawn()
    {
        if (currentGameState != null)
            currentGameState.OnValueChanged -= OnGameStateChangedNetwork;
            
        base.OnNetworkDespawn();
    }
    
    // Input handling
    void Update()
    {
        // Handle pause input (ESC key)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (CurrentState == GameState.Playing)
                PauseGame();
            else if (CurrentState == GameState.Paused)
                ResumeGame();
        }
    }
}
```

## 📋 Phase 3 Setup Checklist

### ✅ UI System Setup:
- [ ] Create UI Canvas with Screen Space - Overlay
- [ ] Add HealthBar component with fill image and text
- [ ] Add ExperienceBar component with XP fill and level display
- [ ] Add WaveDisplay component for wave information
- [ ] Connect UI components to player systems

### ✅ Visual Effects Setup:
- [ ] Create EffectManager GameObject
- [ ] Create particle effect prefabs (muzzle flash, hit, death)
- [ ] Add screen shake for impactful moments
- [ ] Integrate effects with weapon and enemy systems

### ✅ Audio System Setup:
- [ ] Create AudioManager GameObject with multiple AudioSources
- [ ] Add background music and sound effects
- [ ] Integrate audio triggers with game events
- [ ] Add volume control settings

### ✅ Game State Setup:
- [ ] Add GameStateManager to scene
- [ ] Create game over and victory UI panels
- [ ] Add pause menu functionality
- [ ] Implement win/lose condition checking

## 🎯 Phase 3 Success Criteria

### When Phase 3 is Complete:
- [x] Players can see health, XP, and wave information in UI
- [x] Visual and audio feedback for all major game events
- [x] Proper game over and victory conditions
- [x] Pause/resume functionality
- [x] Enhanced game feel through effects and sounds

This completes the polish phase, resulting in a fully featured survivorlike experience!