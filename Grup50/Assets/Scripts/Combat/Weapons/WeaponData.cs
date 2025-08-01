using UnityEngine;

[CreateAssetMenu(fileName = "New Weapon", menuName = "Combat/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("Basic Stats")]
    [SerializeField] private string weaponName = "Basic Weapon";
    [SerializeField] private float damage = 10f;
    [SerializeField] private float fireRate = 1f; // Shots per second
    [SerializeField] private float projectileSpeed = 15f;
    [SerializeField] private float range = 10f;
    
    [Header("Projectile Configuration")]
    [SerializeField] private GameObject projectilePrefab; // Uses DamagingObject component
    [SerializeField] private int projectileCount = 1; // Number of projectiles per shot
    [SerializeField] private float spreadAngle = 0f; // Degrees spread for multiple projectiles
    [SerializeField] private float projectileLifetime = 5f;
    
    [Header("Targeting")]
    [SerializeField] private bool autoTarget = false; // For homing projectiles
    [SerializeField] private LayerMask targetLayers = -1; // What can be targeted
    
    [Header("Area Effects")]
    [SerializeField] private bool hasAreaDamage = false;
    [SerializeField] private float areaRadius = 0f;
    [SerializeField] private bool pierceTargets = false;
    [SerializeField] private int maxPierceCount = 0;
    
    [Header("Weapon Progression")]
    [SerializeField] private int maxLevel = 5;
    [SerializeField] private float damageScaling = 1.5f; // Multiplier per level
    [SerializeField] private float fireRateScaling = 0.2f; // Addition per level
    [SerializeField] private float rangeScaling = 1f; // Addition per level
    
    [Header("Visual & Audio")]
    [SerializeField] private GameObject muzzleFlashEffect;
    [SerializeField] private AudioClip fireSound;
    [SerializeField] private float soundVolume = 1f;
    
    // Properties for easy access
    public string WeaponName => weaponName;
    public float Damage => damage;
    public float FireRate => fireRate;
    public float ProjectileSpeed => projectileSpeed;
    public float Range => range;
    public GameObject ProjectilePrefab => projectilePrefab;
    public int ProjectileCount => projectileCount;
    public float SpreadAngle => spreadAngle;
    public float ProjectileLifetime => projectileLifetime;
    public bool AutoTarget => autoTarget;
    public LayerMask TargetLayers => targetLayers;
    public bool HasAreaDamage => hasAreaDamage;
    public float AreaRadius => areaRadius;
    public bool PierceTargets => pierceTargets;
    public int MaxPierceCount => maxPierceCount;
    public int MaxLevel => maxLevel;
    public GameObject MuzzleFlashEffect => muzzleFlashEffect;
    public AudioClip FireSound => fireSound;
    public float SoundVolume => soundVolume;
    
    /// <summary>
    /// Get scaled damage for specific weapon level
    /// </summary>
    public float GetDamageAtLevel(int level)
    {
        return damage + (damage * damageScaling * (level - 1));
    }
    
    /// <summary>
    /// Get scaled fire rate for specific weapon level
    /// </summary>
    public float GetFireRateAtLevel(int level)
    {
        return fireRate + (fireRateScaling * (level - 1));
    }
    
    /// <summary>
    /// Get scaled range for specific weapon level
    /// </summary>
    public float GetRangeAtLevel(int level)
    {
        return range + (rangeScaling * (level - 1));
    }
    
    /// <summary>
    /// Get fire interval (time between shots) for specific weapon level
    /// </summary>
    public float GetFireIntervalAtLevel(int level)
    {
        return 1f / GetFireRateAtLevel(level);
    }
    
    /// <summary>
    /// Get projectile count (can be modified by upgrades)
    /// </summary>
    public int GetProjectileCountAtLevel(int level)
    {
        // Some weapons might add projectiles per level
        return projectileCount;
    }
    
    /// <summary>
    /// Check if weapon can be upgraded further
    /// </summary>
    public bool CanUpgrade(int currentLevel)
    {
        return currentLevel < maxLevel;
    }
}