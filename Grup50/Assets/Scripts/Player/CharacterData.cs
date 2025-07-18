using UnityEngine;

[CreateAssetMenu(fileName = "New Character", menuName = "Character System/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Character Info")]
    [Tooltip("Display name of the character")]
    public string characterName = "Character";
    
    [Tooltip("Brief description of the character")]
    [TextArea(2, 4)]
    public string description = "Character description";
    
    [Tooltip("Character icon for UI")]
    public Sprite characterIcon;
    
    [Tooltip("Character preview image for selection screen")]
    public Sprite characterPreview;
    
    [Header("Visual Data")]
    [Tooltip("Character prefab to instantiate (optional - for completely different models)")]
    [SerializeField] public GameObject characterPrefab;
    
    /// <summary>
    /// Gets the character prefab for this character
    /// </summary>
    /// <returns>Character prefab GameObject</returns>
    public GameObject GetCharacterPrefab()
    {
        return characterPrefab;
    }
    
    [Tooltip("Skeletal mesh for SkinnedMeshRenderer (main body mesh)")]
    public Mesh skeletalMesh;
    
    [Tooltip("Additional meshes for outfit parts (head, torso, legs, etc.)")]
    public Mesh[] outfitMeshes;
    
    [Tooltip("Character materials to apply")]
    public Material[] characterMaterials;
    
    [Tooltip("Animator controller for character")]
    public RuntimeAnimatorController animatorController;
    
    [Tooltip("Character scale multiplier")]
    public Vector3 characterScale = Vector3.one;
    
    [Header("Mesh Replacement Settings")]
    [Tooltip("Target SkinnedMeshRenderer names to replace (leave empty to replace all)")]
    public string[] targetRendererNames;
    
    [Tooltip("Use optimized mesh (better performance, requires same bone structure)")]
    public bool useOptimizedMesh = true;
    
    [Header("Movement Stats")]
    [Tooltip("Move speed of the character in m/s")]
    public float moveSpeed = 2.0f;
    
    [Tooltip("Sprint speed of the character in m/s")]
    public float sprintSpeed = 5.335f;
    
    [Tooltip("How fast the character turns to face movement direction")]
    [Range(0.0f, 0.3f)]
    public float rotationSmoothTime = 0.12f;
    
    [Tooltip("Acceleration and deceleration")]
    public float speedChangeRate = 10.0f;
    
    [Header("Crouching")]
    [Tooltip("Enable hold-to-crouch (true) or toggle-to-crouch (false)")]
    public bool holdToCrouch = true;
    
    [Tooltip("Speed multiplier when crouching")]
    [Range(0.1f, 1.0f)]
    public float crouchSpeedMultiplier = 0.5f;
    
    [Header("Sliding")]
    [Tooltip("Minimum speed required to start sliding (m/s)")]
    public float slideSpeedThreshold = 3.0f;
    
    [Tooltip("Maximum slide speed (m/s)")]
    public float maxSlideSpeed = 8.0f;
    
    [Tooltip("How fast the character decelerates while sliding")]
    public float slideDeceleration = 2.0f;
    
    [Tooltip("Minimum slide duration in seconds")]
    public float minSlideDuration = 0.3f;
    
    [Tooltip("Momentum multiplier when jumping from slide")]
    [Range(0.5f, 2.0f)]
    public float slideJumpMomentumMultiplier = 1.2f;
    
    [Tooltip("How responsive steering is while sliding (0 = no steering, 1 = full control)")]
    [Range(0.0f, 1.0f)]
    public float slideSteeringControl = 0.3f;
    
    [Tooltip("Speed boost multiplier when entering slide")]
    [Range(1.0f, 2.0f)]
    public float slideEntryBoost = 1.3f;
    
    [Header("Jump & Double Jump")]
    [Tooltip("The height the player can jump")]
    public float jumpHeight = 1.2f;
    
    [Tooltip("Enable double jump ability")]
    public bool enableDoubleJump = false;
    
    [Tooltip("Maximum number of jumps allowed (1 = single jump, 2 = double jump)")]
    [Range(1, 3)]
    public int maxJumps = 2;
    
    [Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
    public float gravity = -15.0f;
    
    [Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
    public float jumpTimeout = 0.50f;
    
    [Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
    public float fallTimeout = 0.15f;
    
    [Header("Camera Settings")]
    [Tooltip("How much to lower camera when crouching")]
    public float crouchCameraOffset = 0.5f;
    
    [Tooltip("How much to raise camera when sprinting")]
    public float sprintCameraOffset = 0.3f;
    
    [Tooltip("Speed of camera transition when crouching/uncrouching/sprinting")]
    public float cameraLerpSpeed = 5f;
    
    [Tooltip("FOV increase when sprinting")]
    public float sprintFOVIncrease = 10f;
    
    [Tooltip("Enable sprint screen shake")]
    public bool enableSprintScreenShake = true;
    
    [Tooltip("Sprint screen shake intensity")]
    [Range(0f, 1f)]
    public float sprintShakeIntensity = 0.3f;
    
    [Header("Ground Detection")]
    [Tooltip("Useful for rough ground")]
    public float groundedOffset = -0.14f;
    
    [Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
    public float groundedRadius = 0.28f;
    
    [Header("Camera Constraints")]
    [Tooltip("How far in degrees can you move the camera up")]
    public float topClamp = 70.0f;
    
    [Tooltip("How far in degrees can you move the camera down")]
    public float bottomClamp = -30.0f;
    
    [Header("Audio")]
    [Tooltip("Landing audio clip")]
    public AudioClip landingAudioClip;
    
    [Tooltip("Footstep audio clips")]
    public AudioClip[] footstepAudioClips;
    
    [Tooltip("Footstep audio volume")]
    [Range(0, 1)] 
    public float footstepAudioVolume = 0.5f;
    
    [Header("Special Abilities")]
    [Tooltip("Custom abilities this character can use")]
    public string[] specialAbilities;
    
    [Tooltip("Unique identifier for this character")]
    public int characterID = 0;
    
    /// <summary>
    /// Gets the character prefab (use this instead of direct field access)
    /// </summary>
    public GameObject CharacterPrefab => characterPrefab;
}