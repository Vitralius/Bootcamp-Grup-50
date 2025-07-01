using Unity.Cinemachine;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM 
using UnityEngine.InputSystem;
#endif

/* Rigidbody-based Third Person Controller for natural physics interactions
 * Converted from CharacterController to Rigidbody for better multiplayer physics
 */

namespace StarterAssets
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
#if ENABLE_INPUT_SYSTEM 
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class ThirdPersonControllerRB : NetworkBehaviour
    {
        [Header("Player Movement")]
        [Tooltip("Move speed of the character in m/s")]
        public float MoveSpeed = 4.0f;

        [Tooltip("Sprint speed of the character in m/s")]
        public float SprintSpeed = 6.0f;

        [Tooltip("How fast the character turns to face movement direction")]
        [Range(0.0f, 0.3f)]
        public float RotationSmoothTime = 0.12f;

        [Tooltip("Acceleration and deceleration")]
        public float SpeedChangeRate = 10.0f;

        [Header("Jump Settings")]
        [Tooltip("The height the player can jump")]
        public float JumpHeight = 1.2f;

        [Tooltip("Time required to pass before being able to jump again")]
        public float JumpTimeout = 0.50f;

        [Header("Physics Settings")]
        [Tooltip("Drag applied when moving")]
        public float MovementDrag = 5f;

        [Tooltip("Drag applied when not moving")]
        public float StoppingDrag = 15f;

        [Tooltip("Force multiplier for physics interactions")]
        public float PhysicsForceMultiplier = 1.0f;

        [Header("Ground Check")]
        [Tooltip("If the character is grounded or not")]
        public bool Grounded = true;

        [Tooltip("Useful for rough ground")]
        public float GroundedOffset = 0.1f;

        [Tooltip("The radius of the grounded check")]
        public float GroundedRadius = 0.5f;

        [Tooltip("What layers the character uses as ground")]
        public LayerMask GroundLayers = -1; // Default to all layers

        [Header("Cinemachine")]
        [Tooltip("The follow target set in the Cinemachine Virtual Camera")]
        public GameObject CinemachineCameraTarget;

        [Tooltip("How far in degrees can you move the camera up")]
        public float TopClamp = 70.0f;

        [Tooltip("How far in degrees can you move the camera down")]
        public float BottomClamp = -30.0f;

        [Header("Audio")]
        public AudioClip LandingAudioClip;
        public AudioClip[] FootstepAudioClips;
        [Range(0, 1)] public float FootstepAudioVolume = 0.5f;

        // Cinemachine
        private float _cinemachineTargetYaw;
        private float _cinemachineTargetPitch;

        // Player
        private float _speed;
        private float _animationBlend;
        private float _targetRotation = 0.0f;
        private float _rotationVelocity;

        // Timeout deltatime
        private float _jumpTimeoutDelta;

        // Animation IDs
        private int _animIDSpeed;
        private int _animIDGrounded;
        private int _animIDJump;
        private int _animIDFreeFall;
        private int _animIDMotionSpeed;

        // Components
#if ENABLE_INPUT_SYSTEM 
        private PlayerInput _playerInput;
#endif
        private Animator _animator;
        private Rigidbody _rigidbody;
        private CapsuleCollider _capsuleCollider;
        private StarterAssetsInputs _input;
        private Camera _mainCamera;

        private const float _threshold = 0.01f;
        private bool _hasAnimator;

        private bool IsCurrentDeviceMouse
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return _playerInput.currentControlScheme == "KeyboardMouse";
#else
				return false;
#endif
            }
        }

        public override void OnNetworkSpawn()
        {
            _cinemachineTargetYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;
            _hasAnimator = TryGetComponent(out _animator);
            _rigidbody = GetComponent<Rigidbody>();
            _capsuleCollider = GetComponent<CapsuleCollider>();
            _input = GetComponent<StarterAssetsInputs>();

#if ENABLE_INPUT_SYSTEM 
            _playerInput = GetComponent<PlayerInput>();
#else
			Debug.LogError("Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif

            AssignAnimationIDs();

            // Reset timeouts
            _jumpTimeoutDelta = JumpTimeout;

            // Configure Rigidbody
            _rigidbody.freezeRotation = true; // Prevent physics from rotating the player
            _rigidbody.useGravity = true; // Enable gravity
            _rigidbody.linearDamping = MovementDrag;
            _rigidbody.mass = 1f; // Standard mass

            if (IsOwner)
            {
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera").GetComponentInChildren<Camera>();
                
                // Set up Cinemachine camera to follow this player
                var virtualCamera = GameObject.FindFirstObjectByType<CinemachineCamera>();
                if (virtualCamera != null)
                {
                    virtualCamera.Follow = CinemachineCameraTarget.transform;
                    virtualCamera.LookAt = CinemachineCameraTarget.transform;
                }
                
                // Lock and hide cursor for local player
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                
                // Enable input for owner player
                if (_input != null)
                    _input.enabled = true;
                if (_playerInput != null)
                    _playerInput.enabled = true;
            }
            else
            {
                // Disable input components for non-owner players
                if (_input != null)
                    _input.enabled = false;
                if (_playerInput != null)
                    _playerInput.enabled = false;
            }
        }

        private void Update()
        {
            if (!IsOwner)
                return;
            
            _hasAnimator = TryGetComponent(out _animator);

            // Handle menu toggle
            if (_input.menu)
            {
                _input.menu = false; // Reset the input
                _input.ToggleCursor();
            }

            GroundedCheck();
            HandleJump();
        }

        private void FixedUpdate()
        {
            if (!IsOwner)
                return;

            Move();
            
            // Apply extra gravity if falling (common pattern for better game feel)
            if (!Grounded && _rigidbody.linearVelocity.y < 0)
            {
                _rigidbody.linearVelocity += Vector3.up * Physics.gravity.y * 2f * Time.fixedDeltaTime;
            }
        }

        private void LateUpdate()
        {
            if (!IsOwner)
                return;
                
            CameraRotation();
        }

        private void AssignAnimationIDs()
        {
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDGrounded = Animator.StringToHash("Grounded");
            _animIDJump = Animator.StringToHash("Jump");
            _animIDFreeFall = Animator.StringToHash("FreeFall");
            _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
        }

        private void GroundedCheck()
        {
            // Set sphere position, with offset
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset,
                transform.position.z);
            
            // Check for ground
            bool wasGrounded = Grounded;
            Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers,
                QueryTriggerInteraction.Ignore);

            // Debug ground detection
            if (wasGrounded != Grounded)
            {
                Debug.Log($"Grounded state changed: {Grounded}, Position: {spherePosition}, GroundLayers: {GroundLayers.value}");
            }

            // If no specific ground layers set, check for anything solid
            if (GroundLayers.value == 0)
            {
                Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, ~0, QueryTriggerInteraction.Ignore);
                Debug.LogWarning("GroundLayers not set! Using all layers for ground detection.");
            }

            // Update animator if using character
            if (_hasAnimator)
            {
                _animator.SetBool(_animIDGrounded, Grounded);
            }
        }

        private void CameraRotation()
        {
            // Only process camera rotation for the owner
            if (!IsOwner || _mainCamera == null)
                return;
                
            // If there is an input and camera position is not fixed
            if (_input.look.sqrMagnitude >= _threshold)
            {
                //Don't multiply mouse input by Time.deltaTime;
                float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

                _cinemachineTargetYaw += _input.look.x * deltaTimeMultiplier;
                _cinemachineTargetPitch += _input.look.y * deltaTimeMultiplier;
            }

            // Clamp our rotations so our values are limited 360 degrees
            _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

            // Cinemachine will follow this target
            CinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch,
                _cinemachineTargetYaw, 0.0f);
        }

        private void Move()
        {
            // Set target speed based on move speed, sprint speed and if sprint is pressed
            float targetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;
            
            // Normalize input direction
            Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

            // If there is no input, set the target speed to 0
            if (_input.move == Vector2.zero) 
            {
                targetSpeed = 0.0f;
                inputDirection = Vector3.zero;
            }

            // Get current horizontal velocity (ignore Y for gravity)
            Vector3 currentVelocity = _rigidbody.linearVelocity;
            Vector3 currentHorizontalVelocity = new Vector3(currentVelocity.x, 0.0f, currentVelocity.z);

            // Calculate movement direction relative to camera
            Vector3 targetDirection = Vector3.zero;
            if (inputDirection != Vector3.zero)
            {
                _targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg +
                                  _mainCamera.transform.eulerAngles.y;
                float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity,
                    RotationSmoothTime);

                // Rotate to face input direction relative to camera position
                transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
                
                targetDirection = Quaternion.Euler(0.0f, _targetRotation, 0.0f) * Vector3.forward;
            }

            // Calculate target velocity
            Vector3 targetHorizontalVelocity = targetDirection * targetSpeed;
            
            // Calculate velocity difference
            Vector3 velocityDifference = targetHorizontalVelocity - currentHorizontalVelocity;
            
            // Apply force to reach target velocity (instead of directly setting velocity)
            float forceMultiplier = Grounded ? SpeedChangeRate * 2f : SpeedChangeRate * 0.5f; // Less control in air
            _rigidbody.AddForce(velocityDifference * forceMultiplier, ForceMode.Acceleration);

            // Update animation values
            float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;
            _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.fixedDeltaTime * SpeedChangeRate);
            if (_animationBlend < 0.01f) _animationBlend = 0f;

            // Update animator if using character
            if (_hasAnimator)
            {
                _animator.SetFloat(_animIDSpeed, _animationBlend);
                _animator.SetFloat(_animIDMotionSpeed, inputMagnitude);
            }
        }

        private void HandleJump()
        {
            if (Grounded)
            {
                // Update animator if using character
                if (_hasAnimator)
                {
                    _animator.SetBool(_animIDJump, false);
                    _animator.SetBool(_animIDFreeFall, false);
                }

                // Jump
                if (_input.jump && _jumpTimeoutDelta <= 0.0f)
                {
                    // Much simpler jump - just add upward force
                    _rigidbody.AddForce(Vector3.up * JumpHeight, ForceMode.Impulse);
                    
                    Debug.Log($"Jump applied! Force: {JumpHeight}, Current Y velocity: {_rigidbody.linearVelocity.y}");

                    // Update animator if using character
                    if (_hasAnimator)
                    {
                        _animator.SetBool(_animIDJump, true);
                    }
                }

                // Jump timeout
                if (_jumpTimeoutDelta >= 0.0f)
                {
                    _jumpTimeoutDelta -= Time.deltaTime;
                }
            }
            else
            {
                // Reset the jump timeout timer
                _jumpTimeoutDelta = JumpTimeout;

                // Fall timeout
                if (_hasAnimator)
                {
                    _animator.SetBool(_animIDFreeFall, true);
                }

                // Reset jump input
                _input.jump = false;
            }
        }

        // Natural physics interactions for both host and client
        private void OnCollisionStay(Collision collision)
        {
            // Only owner can push objects
            if (!IsOwner) return;

            // Handle collision with pushable objects
            Rigidbody otherRB = collision.rigidbody;
            if (otherRB != null && !otherRB.isKinematic)
            {
                // Check if it's a NetworkRigidbody (networked object)
                NetworkRigidbody networkRB = otherRB.GetComponent<NetworkRigidbody>();
                
                if (IsServer)
                {
                    // Host: Apply push directly
                    ApplyPushForce(otherRB, collision);
                }
                else if (networkRB != null)
                {
                    // Client: Request push for networked objects
                    Vector3 pushDirection = collision.contacts[0].point - transform.position;
                    pushDirection.y = 0;
                    pushDirection.Normalize();
                    
                    RequestPushServerRpc(otherRB.GetComponent<NetworkObject>().NetworkObjectId, pushDirection, _rigidbody.linearVelocity.magnitude);
                }
                else
                {
                    // Client: Apply push directly for non-networked objects
                    ApplyPushForce(otherRB, collision);
                }
            }
        }

        private void ApplyPushForce(Rigidbody targetRB, Collision collision)
        {
            // Calculate push direction
            Vector3 pushDirection = collision.contacts[0].point - transform.position;
            pushDirection.y = 0; // Only push horizontally
            pushDirection.Normalize();

            // Apply natural physics force based on player velocity
            float pushForce = _rigidbody.linearVelocity.magnitude * PhysicsForceMultiplier;
            if (pushForce > 0.1f) // Only push if moving
            {
                targetRB.AddForce(pushDirection * pushForce, ForceMode.Force);
                Debug.Log($"Push applied: {pushForce} to {targetRB.name} - IsServer: {IsServer}");
            }
        }

        [ServerRpc]
        private void RequestPushServerRpc(ulong networkObjectId, Vector3 pushDirection, float playerSpeed)
        {
            // Server handles client push requests
            if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject networkObj))
            {
                Rigidbody targetRB = networkObj.GetComponent<Rigidbody>();
                if (targetRB != null && !targetRB.isKinematic)
                {
                    float pushForce = playerSpeed * PhysicsForceMultiplier;
                    if (pushForce > 0.1f)
                    {
                        targetRB.AddForce(pushDirection * pushForce, ForceMode.Force);
                        Debug.Log($"Server processed client push: {pushForce} to {targetRB.name}");
                    }
                }
            }
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        private void OnDrawGizmosSelected()
        {
            Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
            Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

            if (Grounded) Gizmos.color = transparentGreen;
            else Gizmos.color = transparentRed;

            // Ground check sphere
            Gizmos.DrawSphere(
                new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z),
                GroundedRadius);
        }

        private void OnFootstep(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                if (FootstepAudioClips.Length > 0)
                {
                    var index = Random.Range(0, FootstepAudioClips.Length);
                    AudioSource.PlayClipAtPoint(FootstepAudioClips[index], transform.TransformPoint(_capsuleCollider.center), FootstepAudioVolume);
                }
            }
        }

        private void OnLand(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                AudioSource.PlayClipAtPoint(LandingAudioClip, transform.TransformPoint(_capsuleCollider.center), FootstepAudioVolume);
            }
        }
    }
}