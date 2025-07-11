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
    
    public void TriggerDamageShake(float damageAmount)
    {
        if (!enableScreenShake) return;
        
        float normalizedDamage = Mathf.Clamp01(damageAmount / 100f);
        float shakeStrength = damageShakeForce * normalizedDamage * shakeIntensity;
        
        impulseSource.DefaultVelocity = damageShakeDirection * shakeStrength;
        impulseSource.GenerateImpulse();
    }
    
    public void TriggerDeathShake()
    {
        if (!enableScreenShake) return;
        
        float shakeStrength = deathShakeForce * shakeIntensity;
        
        impulseSource.DefaultVelocity = deathShakeDirection * shakeStrength;
        impulseSource.GenerateImpulse();
    }
    
    public void TriggerHealShake(float healAmount)
    {
        if (!enableScreenShake) return;
        
        float normalizedHeal = Mathf.Clamp01(healAmount / 100f);
        float shakeStrength = healShakeForce * normalizedHeal * shakeIntensity;
        
        impulseSource.DefaultVelocity = healShakeDirection * shakeStrength;
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
}