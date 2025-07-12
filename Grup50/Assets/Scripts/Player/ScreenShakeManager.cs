using UnityEngine;
using Unity.Cinemachine;

public class ScreenShakeManager : MonoBehaviour
{
    [Header("Screen Shake Settings")]
    [SerializeField] private bool enableScreenShake = true;
    [SerializeField] private float shakeIntensity = 1.0f;
    
    [Header("Damage Shake Settings")]
    [SerializeField] private float damageShakeForce = 1.0f;
    [SerializeField] private Vector3 damageShakeDirection = Vector3.up;
    
    [Header("Death Shake Settings")]
    [SerializeField] private float deathShakeForce = 2.0f;
    [SerializeField] private Vector3 deathShakeDirection = Vector3.up;
    
    [Header("Heal Shake Settings")]
    [SerializeField] private float healShakeForce = 0.5f;
    [SerializeField] private Vector3 healShakeDirection = Vector3.up;
    
    [Header("Sprint Shake Settings")]
    [SerializeField] private float sprintShakeForce = 0.2f;
    [SerializeField] private Vector3 sprintShakeDirection = Vector3.right;
    [SerializeField] private float sprintShakeInterval = 0.1f;
    
    [Header("Shake Range Settings")]
    [SerializeField] private float minShakeRange = 0.3f;
    [SerializeField] private float maxShakeRange = 1.0f;
    
    private float lastSprintShakeTime;
    
    private CinemachineImpulseSource impulseSource;
    
    void Awake()
    {
        impulseSource = GetComponent<CinemachineImpulseSource>();
        
        if (impulseSource == null)
        {
            impulseSource = gameObject.AddComponent<CinemachineImpulseSource>();
        }
        
        ConfigureImpulseSource();
    }
    
    void ConfigureImpulseSource()
    {
        // Configure the impulse definition
        var impulseDefinition = impulseSource.ImpulseDefinition;
        
        // Set basic impulse properties
        impulseSource.DefaultVelocity = damageShakeDirection;
        
        // Configure the impulse signal using only valid properties
        impulseDefinition.ImpulseChannel = 1;
        impulseDefinition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
        impulseDefinition.CustomImpulseShape = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0));
        impulseDefinition.ImpulseDuration = 0.2f;
        impulseDefinition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
        impulseDefinition.AmplitudeGain = 1.0f;
        impulseDefinition.FrequencyGain = 1.0f;
        impulseDefinition.DissipationDistance = 100f;
    }
    
    public void TriggerDamageShake(float damageAmount, float intensityMultiplier = 1.0f)
    {
        if (!enableScreenShake) return;
        
        float normalizedDamage = Mathf.Clamp01(damageAmount / 100f);
        float shakeStrength = damageShakeForce * normalizedDamage * shakeIntensity * intensityMultiplier;
        
        Vector3 randomDirection = GetRandomizedDirection(damageShakeDirection);
        impulseSource.DefaultVelocity = randomDirection * shakeStrength;
        impulseSource.GenerateImpulse();
    }
    
    public void TriggerDeathShake(float intensityMultiplier = 1.0f)
    {
        if (!enableScreenShake) return;
        
        float shakeStrength = deathShakeForce * shakeIntensity * intensityMultiplier;
        
        Vector3 randomDirection = GetRandomizedDirection(deathShakeDirection);
        impulseSource.DefaultVelocity = randomDirection * shakeStrength;
        impulseSource.GenerateImpulse();
    }
    
    public void TriggerHealShake(float healAmount, float intensityMultiplier = 1.0f)
    {
        if (!enableScreenShake) return;
        
        float normalizedHeal = Mathf.Clamp01(healAmount / 100f);
        float shakeStrength = healShakeForce * normalizedHeal * shakeIntensity * intensityMultiplier;
        
        Vector3 randomDirection = GetRandomizedDirection(healShakeDirection);
        impulseSource.DefaultVelocity = randomDirection * shakeStrength;
        impulseSource.GenerateImpulse();
    }
    
    public void TriggerSprintShake(float sprintIntensity)
    {
        if (!enableScreenShake) return;
        
        // Only trigger shake at intervals to avoid too many impulses
        if (Time.time - lastSprintShakeTime < sprintShakeInterval) return;
        
        lastSprintShakeTime = Time.time;
        
        float shakeStrength = sprintShakeForce * sprintIntensity * shakeIntensity;
        
        Vector3 randomDirection = GetRandomizedDirection(sprintShakeDirection);
        impulseSource.DefaultVelocity = randomDirection * shakeStrength;
        impulseSource.GenerateImpulse();
    }
    
    public void SetScreenShakeEnabled(bool enabled)
    {
        enableScreenShake = enabled;
    }
    
    public void SetShakeIntensity(float intensity)
    {
        shakeIntensity = Mathf.Clamp01(intensity);
    }
    
    public bool IsScreenShakeEnabled()
    {
        return enableScreenShake;
    }
    
    public float GetShakeIntensity()
    {
        return shakeIntensity;
    }
    
    public void SetShakeRange(float minRange, float maxRange)
    {
        minShakeRange = Mathf.Max(0f, minRange);
        maxShakeRange = Mathf.Max(minShakeRange, maxRange);
    }
    
    public float GetMinShakeRange()
    {
        return minShakeRange;
    }
    
    public float GetMaxShakeRange()
    {
        return maxShakeRange;
    }
    
    private Vector3 GetRandomizedDirection(Vector3 baseDirection)
    {
        // Generate a random magnitude within the specified range
        float randomMagnitude = UnityEngine.Random.Range(minShakeRange, maxShakeRange);
        
        // Create random direction components within the range
        Vector3 randomDirection = new Vector3(
            RandomizeComponentInRange(baseDirection.x, randomMagnitude),
            RandomizeComponentInRange(baseDirection.y, randomMagnitude),
            RandomizeComponentInRange(baseDirection.z, randomMagnitude)
        );
        
        return randomDirection;
    }
    
    private float RandomizeComponentInRange(float component, float rangeMagnitude)
    {
        if (component == 0f) return 0f;
        
        // Generate random value between -rangeMagnitude and +rangeMagnitude
        // But only for axes that are active (non-zero in baseDirection)
        float randomValue = UnityEngine.Random.Range(-rangeMagnitude, rangeMagnitude);
        
        // Apply the sign and proportional scaling of the original component
        return randomValue * Mathf.Sign(component);
    }
}