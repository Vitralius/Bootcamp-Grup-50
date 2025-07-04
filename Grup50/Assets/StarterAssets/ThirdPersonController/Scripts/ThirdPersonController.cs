 using Unity.Cinemachine;
 using Unity.Netcode;
 using UnityEngine;
#if ENABLE_INPUT_SYSTEM 
using UnityEngine.InputSystem;
#endif

/* Note: animations are called via the controller for both the character and capsule using animator null checks
 */

namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM 
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class ThirdPersonController : NetworkBehaviour
    {
        [Header("Player")]
        [Tooltip("Move speed of the character in m/s")]
        public float MoveSpeed = 2.0f;

        [Tooltip("Sprint speed of the character in m/s")]
        public float SprintSpeed = 5.335f;

        [Tooltip("How fast the character turns to face movement direction")]
        [Range(0.0f, 0.3f)]
        public float RotationSmoothTime = 0.12f;

        [Tooltip("Acceleration and deceleration")]
        public float SpeedChangeRate = 10.0f;

        [Space(10)]
        [Header("Crouching")]
        [Tooltip("Enable hold-to-crouch (true) or toggle-to-crouch (false)")]
        public bool HoldToCrouch = true;
        
        [Tooltip("Speed multiplier when crouching")]
        [Range(0.1f, 1.0f)]
        public float CrouchSpeedMultiplier = 0.5f;

        [Space(10)]
        [Header("Double Jump")]
        [Tooltip("Enable double jump ability")]
        public bool EnableDoubleJump = false;
        
        [Tooltip("Maximum number of jumps allowed (1 = single jump, 2 = double jump)")]
        [Range(1, 3)]
        public int MaxJumps = 2;

        [Space(10)]
        [Header("Camera")]
        [Tooltip("How much to lower camera when crouching")]
        public float CrouchCameraOffset = 0.5f;
        
        [Tooltip("Speed of camera transition when crouching/uncrouching")]
        public float CameraLerpSpeed = 5f;

        public AudioClip LandingAudioClip;
        public AudioClip[] FootstepAudioClips;
        [Range(0, 1)] public float FootstepAudioVolume = 0.5f;

        [Space(10)]
        [Tooltip("The height the player can jump")]
        public float JumpHeight = 1.2f;

        [Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
        public float Gravity = -15.0f;

        [Space(10)]
        [Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
        public float JumpTimeout = 0.50f;

        [Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
        public float FallTimeout = 0.15f;

        [Header("Player Grounded")]
        [Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
        public bool Grounded = true;

        [Tooltip("Useful for rough ground")]
        public float GroundedOffset = -0.14f;

        [Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
        public float GroundedRadius = 0.28f;

        [Tooltip("What layers the character uses as ground")]
        public LayerMask GroundLayers;

        [Header("Cinemachine")]
        [Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
        public GameObject CinemachineCameraTarget;

        [Tooltip("How far in degrees can you move the camera up")]
        public float TopClamp = 70.0f;

        [Tooltip("How far in degrees can you move the camera down")]
        public float BottomClamp = -30.0f;

        [Tooltip("Additional degress to override the camera. Useful for fine tuning camera position when locked")]
        public float CameraAngleOverride = 0.0f;

        [Tooltip("For locking the camera position on all axis")]
        public bool LockCameraPosition = false;

        // cinemachine
        private float _cinemachineTargetYaw;
        private float _cinemachineTargetPitch;

        // player
        private float _speed;
        private float _animationBlend;
        private float _targetRotation = 0.0f;
        private float _rotationVelocity;
        private float _verticalVelocity;
        private float _terminalVelocity = 53.0f;

        // timeout deltatime
        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;

        // animation IDs
        private int _animIDSpeed;
        private int _animIDGrounded;
        private int _animIDJump;
        private int _animIDFreeFall;
        private int _animIDMotionSpeed;
        private int _animIDCrouched;
        private int _animIDDoubleJump;

#if ENABLE_INPUT_SYSTEM 
        private PlayerInput _playerInput;
#endif
        private Animator _animator;
        private CharacterController _controller;
        private StarterAssetsInputs _input;
        private Camera _mainCamera;

        private const float _threshold = 0.01f;

        private bool _hasAnimator;
        
        // crouching variables
        private bool _isCrouching;
        private bool _crouchToggleState;
        private float _originalHeight;
        private float _originalCenterY;
        
        // double jump variables
        private int _jumpCount;
        
        // camera variables
        private float _originalCameraTargetY;
        private float _targetCameraY;

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
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<StarterAssetsInputs>();
#if ENABLE_INPUT_SYSTEM 
            _playerInput = GetComponent<PlayerInput>();
#else
			Debug.LogError( "Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif

            AssignAnimationIDs();

            // reset our timeouts on start
            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;
            
            // store original character controller values
            _originalHeight = _controller.height;
            _originalCenterY = _controller.center.y;
            
            // store original camera target position
            _originalCameraTargetY = CinemachineCameraTarget.transform.localPosition.y;
            _targetCameraY = _originalCameraTargetY;

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

        private void Awake()
        {

        }

        private void Update()
        {
            if (!IsOwner)
                return;
            
            if(!_hasAnimator)
                _hasAnimator = TryGetComponent(out _animator);

            // Handle menu toggle
            if (_input.menu)
            {
                _input.menu = false; // Reset the input
                _input.ToggleCursor();
            }

            JumpAndGravity();
            GroundedCheck();
            HandleCrouching();
            UpdateCameraPosition();
            Move();
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
            _animIDCrouched = Animator.StringToHash("Crouched");
            _animIDDoubleJump = Animator.StringToHash("DoubleJump");
        }

        private void HandleCrouching()
        {
            if (!Grounded) return; // Can only crouch when grounded
            
            bool wantsToCrouch = HoldToCrouch ? _input.crouch : _crouchToggleState;
            
            // Handle toggle crouch input
            if (!HoldToCrouch && _input.crouch)
            {
                _crouchToggleState = !_crouchToggleState;
                _input.crouch = false; // Reset input to prevent multiple toggles
            }
            
            // Handle sprint while crouched - try to uncrouch
            if (_isCrouching && _input.sprint)
            {
                if (CanUncrouch())
                {
                    // Force uncrouch by updating toggle state if needed
                    if (!HoldToCrouch)
                    {
                        _crouchToggleState = false;
                    }
                    StopCrouching();
                    // Sprint input will be processed in Move() method
                }
                else
                {
                    // Can't uncrouch, prevent sprinting
                    _input.sprint = false;
                }
            }
            
            // Try to crouch
            if (wantsToCrouch && !_isCrouching)
            {
                StartCrouching();
            }
            // Try to uncrouch
            else if (!wantsToCrouch && _isCrouching)
            {
                if (CanUncrouch())
                {
                    StopCrouching();
                }
            }
        }
        
        private void StartCrouching()
        {
            _isCrouching = true;
            
            // Stop sprinting when crouching
            _input.sprint = false;
            
            // Adjust character controller
            _controller.height = _originalHeight * 0.5f;
            _controller.center = new Vector3(_controller.center.x, _originalCenterY * 0.5f, _controller.center.z);
            
            // Set target camera position for crouching
            _targetCameraY = _originalCameraTargetY - CrouchCameraOffset;
            
            // Update animation
            if (_hasAnimator)
            {
                _animator.SetBool(_animIDCrouched, true);
            }
        }
        
        private void StopCrouching()
        {
            _isCrouching = false;
            
            // Restore character controller
            _controller.height = _originalHeight;
            _controller.center = new Vector3(_controller.center.x, _originalCenterY, _controller.center.z);
            
            // Restore target camera position
            _targetCameraY = _originalCameraTargetY;
            
            // Update animation
            if (_hasAnimator)
            {
                _animator.SetBool(_animIDCrouched, false);
            }
        }
        
        private void UpdateCameraPosition()
        {
            // Only update camera for owner
            if (!IsOwner)
                return;
                
            // Smoothly lerp camera target position
            Vector3 currentPos = CinemachineCameraTarget.transform.localPosition;
            float newY = Mathf.Lerp(currentPos.y, _targetCameraY, CameraLerpSpeed * Time.deltaTime);
            CinemachineCameraTarget.transform.localPosition = new Vector3(currentPos.x, newY, currentPos.z);
        }
        
        private bool CanUncrouch()
        {
            // Calculate the position where the character's head would be when standing
            Vector3 topPosition = transform.position + Vector3.up * (_originalHeight - _controller.radius);
            
            // Check for obstacles at head level using CheckSphere
            return !Physics.CheckSphere(topPosition, _controller.radius, GroundLayers, QueryTriggerInteraction.Ignore);
        }

        private void GroundedCheck()
        {
            // set sphere position, with offset
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset,
                transform.position.z);
            Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers,
                QueryTriggerInteraction.Ignore);

            // update animator if using character
            if (_hasAnimator)
            {
                _animator.SetBool(_animIDGrounded, Grounded);
            }
            
            // Reset jump count when grounded
            if (Grounded && _jumpCount > 0)
            {
                _jumpCount = 0;
            }
        }

        private void CameraRotation()
        {
            // Only process camera rotation for the owner
            if (!IsOwner || _mainCamera == null)
                return;
                
            // if there is an input and camera position is not fixed
            if (_input.look.sqrMagnitude >= _threshold && !LockCameraPosition)
            {
                //Don't multiply mouse input by Time.deltaTime;
                float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

                _cinemachineTargetYaw += _input.look.x * deltaTimeMultiplier;
                _cinemachineTargetPitch += _input.look.y * deltaTimeMultiplier;
            }

            // clamp our rotations so our values are limited 360 degrees
            _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

            // Cinemachine will follow this target
            CinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch + CameraAngleOverride,
                _cinemachineTargetYaw, 0.0f);
        }

        private void Move()
        {
            // set target speed based on move speed, sprint speed and if sprint is pressed
            float targetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;
            
            // Apply crouch speed multiplier
            if (_isCrouching)
            {
                targetSpeed *= CrouchSpeedMultiplier;
            }

            // a simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon

            // note: Vector2's == operator uses approximation so is not floating point error prone, and is cheaper than magnitude
            // if there is no input, set the target speed to 0
            if (_input.move == Vector2.zero) targetSpeed = 0.0f;

            // a reference to the players current horizontal velocity
            float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

            float speedOffset = 0.1f;
            float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

            // accelerate or decelerate to target speed
            if (currentHorizontalSpeed < targetSpeed - speedOffset ||
                currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                // creates curved result rather than a linear one giving a more organic speed change
                // note T in Lerp is clamped, so we don't need to clamp our speed
                _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude,
                    Time.deltaTime * SpeedChangeRate);

                // round speed to 3 decimal places
                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }

            _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * SpeedChangeRate);
            if (_animationBlend < 0.01f) _animationBlend = 0f;

            // normalise input direction
            Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

            // note: Vector2's != operator uses approximation so is not floating point error prone, and is cheaper than magnitude
            // if there is a move input rotate player when the player is moving
            if (_input.move != Vector2.zero)
            {
                _targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg +
                                  _mainCamera.transform.eulerAngles.y;
                float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity,
                    RotationSmoothTime);

                // rotate to face input direction relative to camera position
                transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
            }


            Vector3 targetDirection = Quaternion.Euler(0.0f, _targetRotation, 0.0f) * Vector3.forward;

            // move the player
            _controller.Move(targetDirection.normalized * (_speed * Time.deltaTime) +
                             new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);

            // update animator if using character
            if (_hasAnimator)
            {
                _animator.SetFloat(_animIDSpeed, _animationBlend);
                _animator.SetFloat(_animIDMotionSpeed, inputMagnitude);
            }
        }

        private void JumpAndGravity()
        {
            if (Grounded)
            {
                // reset the fall timeout timer
                _fallTimeoutDelta = FallTimeout;

                // update animator if using character
                if (_hasAnimator)
                {
                    _animator.SetBool(_animIDJump, false);
                    _animator.SetBool(_animIDFreeFall, false);
                    _animator.SetBool(_animIDDoubleJump, false);
                }

                // stop our velocity dropping infinitely when grounded
                if (_verticalVelocity < 0.0f)
                {
                    _verticalVelocity = -2f;
                }

                // Jump
                if (_input.jump && _jumpTimeoutDelta <= 0.0f)
                {
                    // Try to uncrouch before jumping if crouching
                    if (_isCrouching)
                    {
                        if (CanUncrouch())
                        {
                            StopCrouching();
                        }
                        else
                        {
                            // Can't uncrouch, so can't jump
                            _input.jump = false;
                            return;
                        }
                    }
                    
                    PerformJump();
                }

                // jump timeout
                if (_jumpTimeoutDelta >= 0.0f)
                {
                    _jumpTimeoutDelta -= Time.deltaTime;
                }
            }
            else
            {
                // reset the jump timeout timer
                _jumpTimeoutDelta = JumpTimeout;

                // fall timeout
                if (_fallTimeoutDelta >= 0.0f)
                {
                    _fallTimeoutDelta -= Time.deltaTime;
                }
                else
                {
                    // update animator if using character
                    if (_hasAnimator)
                    {
                        _animator.SetBool(_animIDFreeFall, true);
                    }
                }

                // Handle double jump when not grounded
                if (_input.jump && EnableDoubleJump && _jumpCount < MaxJumps)
                {
                    PerformJump();
                }
                else
                {
                    // if we are not grounded and can't double jump, do not jump
                    _input.jump = false;
                }
            }

            // apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
            if (_verticalVelocity < _terminalVelocity)
            {
                _verticalVelocity += Gravity * Time.deltaTime;
            }
        }
        
        private void PerformJump()
        {
            // Increment jump count
            _jumpCount++;
            
            // the square root of H * -2 * G = how much velocity needed to reach desired height
            _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);

            // update animator if using character
            if (_hasAnimator)
            {
                if (_jumpCount == 1)
                {
                    _animator.SetBool(_animIDJump, true);
                }
                else if (_jumpCount == 2 && EnableDoubleJump)
                {
                    _animator.SetBool(_animIDDoubleJump, true);
                }
            }
            
            _input.jump = false;
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

            // when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
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
                    AudioSource.PlayClipAtPoint(FootstepAudioClips[index], transform.TransformPoint(_controller.center), FootstepAudioVolume);
                }
            }
        }

        private void OnLand(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                AudioSource.PlayClipAtPoint(LandingAudioClip, transform.TransformPoint(_controller.center), FootstepAudioVolume);
            }
        }
    }
}