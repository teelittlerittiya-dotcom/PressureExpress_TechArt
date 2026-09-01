using System.Collections.Generic;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CharacterController2D : NetworkBehaviour
{
    private static int movementLockCount = 0;
    public static bool CanMove => movementLockCount <= 0;

    public static void LockMovement() => movementLockCount++;
    public static void UnlockMovement() => movementLockCount = Mathf.Max(0, movementLockCount - 1);
    public static void ResetMovementLock() => movementLockCount = 0;

    public static bool canMove
    {
        get => CanMove;
        set
        {
            if (value) ResetMovementLock();
            else LockMovement();
        }
    }

    [SerializeField] private bool debugIsOwnerOverride = false;
    // One-way platform: store a reference to the platform collider for drop-through logic.
    private Collider currentPlatformCollider;

    [Header("Movement")]
    [SerializeField] float moveSpeed = 5f;
    [SerializeField] private float runSpeedBonus = 4f;
    public float CurrentRunSpeedBonus => netIsRunning.Value ? runSpeedBonus : 0;
    [SerializeField] float jumpForce = 7f;
    [SerializeField] private bool canJump = false;

    [Header("Ground Check")]
    [SerializeField] Transform groundCheck;
    [SerializeField] float groundCheckRadius = 0.1f;
    [SerializeField] LayerMask groundLayer;

    [Header("Ladder")]
    [SerializeField] LayerMask ladderLayer;
    [SerializeField] float climbSpeed = 3f;

    [Header("Water & Swimming")]
    [SerializeField] private float swimSpeed = 3.5f;
    [SerializeField] private float swimUpSpeed = 4f;
    [SerializeField] private float swimDownSpeed = 3.5f;
    [SerializeField] private float waterDrag = 4f;
    [SerializeField] private float surfaceJumpForce = 6.5f;
    [SerializeField] private float swimRotationSpeed = 15f;
    [SerializeField] private GameObject splashParticlePrefab;
    [SerializeField] private AudioSource waterAudioSource;
    [SerializeField] private AudioClip splashSound;
    private NetworkVariable<float> networkSwimAngleZ = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    [Header("One Way Platform")]
    [Tooltip("Only colliders on these layers can be dropped through / jumped up through. " +
             "Leave empty and one-way platform behaviour is disabled entirely.")]
    // Defaults to layer 14 ("Platform"). A LayerMask with no initialiser deserialises to
    // Nothing on prefabs saved before the field existed, which silently disables the feature.
    [SerializeField] LayerMask oneWayPlatformLayer = 1 << 14;
    [SerializeField] float dropThroughDuration = 0.25f;

    [Header("Footstep Audio")]
    [Tooltip("Looping footstep clip that plays while walking/running.")]
    [SerializeField] private AudioClip footstepClip;

    [Tooltip("Footstep volume while walking (0–1).")]
    [SerializeField, Range(0f, 1f)] private float walkStepVolume = 0.6f;

    [Tooltip("Footstep volume while running (0–1).")]
    [SerializeField, Range(0f, 1f)] private float runStepVolume = 0.85f;

    [Tooltip("AudioSource pitch while walking.")]
    [SerializeField] private float walkPitch = 1f;

    [Tooltip("AudioSource pitch while running (higher = faster cadence).")]
    [SerializeField] private float runPitch = 1.35f;

    [Header("Hand Setup")]
    [SerializeField] private GameObject handPrefab;

    private Rigidbody rb;
    [SerializeField] private Animator animator;
    private PlayerInputState currentInput;

    private NetworkVariable<Vector3> netPosition = new NetworkVariable<Vector3>(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool> netIsGrounded = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool> netIsClimbing = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool> netIsSwimming = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool> netIsWalking = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool> netIsRunning = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<float> networkFacingDirection = new NetworkVariable<float>(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private NetworkVariable<bool> netIsInteracting = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private MachineInstance _currentMachineInstance;
    private bool isDropping;
    private bool isTeleporting;
    private bool gameplayCursorRegistered;

    // The network root is also the Rigidbody root. Keep it positive-scale and rotate the
    // visual root 180 degrees around Y to mirror without changing transform handedness.
    private Transform visualRoot;
    private Transform[] visualDepthTransforms = System.Array.Empty<Transform>();
    private Quaternion visualRootBaseRotation;
    private bool visualMirrorApplied;
    private Vector3 gameplayRootScale;

    // Footstep runtime references (created in Awake).
    private AudioSource footstepAudioSource;
    private SFXSource footstepSFXSource;

    // Cached so drop-through does not GetComponent every tick, and so the
    // Physics.IgnoreCollision pair can always be undone (it is global engine state).
    private Collider bodyCollider;
    private Collider ignoredPlatform;

    private struct PlayerInputState
    {
        public float HorizontalInput;
        public float VerticalInput;
        public bool JumpPressed;
        public bool InteractPressed;
        public bool DropPressed;
        public bool RunPressed;
    }

    private void Awake()
    {
        SpriteRenderOrderPolicy.ApplyPlayer(transform);
        gameplayRootScale = new Vector3(
            Mathf.Abs(transform.localScale.x),
            Mathf.Abs(transform.localScale.y),
            Mathf.Abs(transform.localScale.z));
        transform.localScale = gameplayRootScale;
        CacheVisualFacing();

        footstepAudioSource = gameObject.AddComponent<AudioSource>();
        footstepAudioSource.playOnAwake = false;
        footstepAudioSource.loop = true;
        footstepAudioSource.spatialBlend = 0f;

        footstepSFXSource = gameObject.AddComponent<SFXSource>();
        footstepSFXSource.Initialize(footstepAudioSource);
    }

    private void OnEnable()
    {
        if (IsOwner || debugIsOwnerOverride || NetworkHelper.IsOffline)
        {
            ResetMovementLock();

            if (NetworkHelper.IsOffline)
            {
                RegisterGameplayCursor();
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner || debugIsOwnerOverride)
        {
            ResetMovementLock();
            RegisterGameplayCursor();
        }
        if (IsServer)
        {
            netIsInteracting.Value = false;
        }

        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError($"{name}: no 3D Rigidbody found. This prefab still has a " +
                           "Rigidbody2D - run Tools > Physics 2D to 3D Converter on it.", this);
            enabled = false;
            return;
        }
        bodyCollider = GetComponent<Collider>();

        if (GetComponent<UnderwaterVoiceAudioFilter>() == null)
        {
            gameObject.AddComponent<UnderwaterVoiceAudioFilter>();
        }

        if (oneWayPlatformLayer.value == 0)
            Debug.LogWarning($"{name}: oneWayPlatformLayer is empty - drop-through and " +
                             "jump-up-through platforms are disabled.", this);

        // Lock Z axis and all rotations for 2.5D gameplay
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;

        if (IsOwner || debugIsOwnerOverride)
        {
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.position = transform.position;
            netPosition.Value = transform.position;
            SpawnHandRpc(OwnerClientId);
            if (Camera.main != null)
            {
                PlayerCameraController cameraScript = Camera.main.GetComponent<PlayerCameraController>();
                if (cameraScript == null)
                {
                    cameraScript = Camera.main.gameObject.AddComponent<PlayerCameraController>();
                }
                cameraScript.SetTarget(transform);

                if (Camera.main.GetComponent<UnderwaterCameraEffect>() == null)
                {
                    Camera.main.gameObject.AddComponent<UnderwaterCameraEffect>();
                }
            }
        }
        else
        {
            rb.isKinematic = true;
        }
    }

    /// <summary>
    /// Called by the server right after SpawnAsPlayerObject to force all clients
    /// to the correct spawn position. This is needed because NetworkTransform is disabled.
    /// </summary>
    public void SetSpawnPosition(Vector3 position)
    {
        transform.position = position;
        rb.position = position;
        rb.linearVelocity = Vector3.zero;
        TeleplayerRpc(position);
    }

    [Rpc(SendTo.Server)]
    private void SpawnHandRpc(ulong clientId)
    {
        if (handPrefab == null) return;
        GameObject handInstance = Instantiate(handPrefab, transform.position, Quaternion.identity);
        NetworkObject netObj = handInstance.GetComponent<NetworkObject>();
        netObj.SpawnWithOwnership(clientId, destroyWithScene: true);
        PlayerHand handScript = handInstance.GetComponent<PlayerHand>();
        if (handScript != null)
        {
            handScript.ownerId.Value = GetComponent<NetworkObject>().NetworkObjectId;
        }
    }

    private void Update()
    {
        RestoreVisualFacingForAnimator();

        if (IsOwner || debugIsOwnerOverride)
        {
            HandleClientInput();
        }
        HandleFlipVisuals();
        HandleAnimation();
        HandleFootsteps();
    }

    private void LateUpdate()
    {
        ApplyVisualFacingAfterAnimation();
    }

    private void FixedUpdate()
    {
        if (IsOwner || debugIsOwnerOverride)
        {
            if (isTeleporting)
            {
                isTeleporting = false;
                netPosition.Value = rb.position;
                return;
            }
            ApplyMovement(currentInput);
            netPosition.Value = rb.position;
        }
        else
        {
            rb.position = Vector3.Lerp(rb.position, netPosition.Value, Time.fixedDeltaTime * 15f);
        }
    }
    [Rpc(SendTo.Everyone)]
    public void TeleplayerRpc(Vector3 position) 
    {
        transform.position = position;
        rb.position = position;
        rb.linearVelocity = Vector3.zero;
        isTeleporting = true;
        if (IsOwner || debugIsOwnerOverride)
        {
            netPosition.Value = position;
        }
    }

    private void HandleClientInput()
    {
        if (!canMove)
        {
            currentInput = new PlayerInputState();
            return;
        }

        currentInput = new PlayerInputState
        {
            HorizontalInput = Input.GetAxisRaw("Horizontal"),
            VerticalInput = Input.GetAxisRaw("Vertical"),
            JumpPressed = Input.GetKey(KeyCode.Space),
            InteractPressed = Input.GetKey(KeyCode.E),
            DropPressed = Input.GetKey(KeyCode.S),
            RunPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)
        };
    }

    public void SetInteractingState(bool state)
    {
        if (IsServer)
        {
            netIsInteracting.Value = state;
        }
        else if (NetworkHelper.IsOffline)
        {
            netIsInteracting.Value = state;
        }

        if (!state)
        {
            ResetMovementLock();
        }
    }

    private void ApplyMovement(PlayerInputState input)
    {
        if (netIsInteracting.Value)
        {
            rb.linearVelocity = Vector3.zero;
            netIsWalking.Value = false;
            netIsClimbing.Value = false;
            netIsSwimming.Value = false;
            rb.useGravity = true;
            return;
        }

        HandleInteract(input);

        bool grounded = Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundLayer);
        netIsGrounded.Value = grounded;

        bool isSwimmingNow = HandleWaterAndSwimming(input);

        if (!isSwimmingNow)
        {
            HandleClimb(input);
            HandleHorizontalMovement(input, inWater: RoomWaterVisualizer.TryGetWaterSurfaceY(transform.position, out _, out _));
            HandleJump(input, grounded);
        }

        HandleFlip(input);
        HandleDropThrough(input);
        UpdateOneWayPassthrough();
    }

    private bool HandleWaterAndSwimming(PlayerInputState input)
    {
        bool inWater = RoomWaterVisualizer.TryGetWaterSurfaceY(transform.position, out float waterSurfaceY, out RoomWaterVisualizer roomWater);
        float depth = inWater ? (waterSurfaceY - transform.position.y) : 0f;
        bool shouldSwim = inWater && depth > 0.4f;

        if (shouldSwim && !netIsClimbing.Value)
        {
            if (!netIsSwimming.Value)
            {
                netIsSwimming.Value = true;
                if (splashParticlePrefab != null)
                    Instantiate(splashParticlePrefab, new Vector3(transform.position.x, waterSurfaceY, transform.position.z), Quaternion.identity);
                if (waterAudioSource != null && splashSound != null)
                    waterAudioSource.PlayOneShot(splashSound);
                if (roomWater != null)
                    roomWater.TriggerRipple(new Vector3(transform.position.x, waterSurfaceY, transform.position.z), true);
            }
        }
        else if (netIsSwimming.Value && depth <= 0.15f)
        {
            netIsSwimming.Value = false;
            rb.useGravity = true;
            if (roomWater != null)
            {
                roomWater.TriggerRipple(new Vector3(transform.position.x, waterSurfaceY, transform.position.z), false);
            }
        }

        if (netIsSwimming.Value)
        {
            rb.useGravity = false;
            Vector3 currentVel = rb.linearVelocity;
            Vector3 targetVel = Vector3.zero;

            targetVel.x = input.HorizontalInput * swimSpeed;
            netIsWalking.Value = input.HorizontalInput != 0;

            if (input.JumpPressed || input.VerticalInput > 0.1f)
            {
                targetVel.y = swimUpSpeed;
            }
            else if (input.VerticalInput < -0.1f)
            {
                targetVel.y = -swimDownSpeed;
            }
            else
            {
                targetVel.y = 0.25f; // Mild buoyancy
            }

            if (depth < 0.6f && input.JumpPressed)
            {
                targetVel.y = surfaceJumpForce;
                netIsSwimming.Value = false;
                rb.useGravity = true;
            }

            rb.linearVelocity = Vector3.Lerp(currentVel, targetVel, Time.fixedDeltaTime * waterDrag);
            return true;
        }

        return false;
    }

    private void HandleHorizontalMovement(PlayerInputState input, bool inWater = false)
    {
        if (netIsClimbing.Value)
        {
            if (Mathf.Abs(input.HorizontalInput) > 0.1f)
            {
                netIsClimbing.Value = false;
                rb.useGravity = true;
            }
        }
        if (!netIsClimbing.Value)
        {
            netIsRunning.Value = input.RunPressed;
            float finalSpeed = moveSpeed + (input.RunPressed ? runSpeedBonus : 0);
            if (inWater) finalSpeed *= 0.7f; // Shallow water speed reduction

            Vector3 vel = rb.linearVelocity;
            vel.x = input.HorizontalInput * finalSpeed;
            rb.linearVelocity = vel;

            netIsWalking.Value = input.HorizontalInput != 0;
            if (animator != null) animator.SetFloat("climbSpeed", 1);
        }
    }

    private void HandleJump(PlayerInputState input, bool grounded)
    {
        if (input.JumpPressed && grounded && canJump)
        {
            if (rb.linearVelocity.y <= 0.1f)
            {
                Vector3 vel = rb.linearVelocity;
                vel.x = rb.linearVelocity.x;
                vel.y = jumpForce;
                rb.linearVelocity = vel;
            }
        }
    }

    private void HandleFlip(PlayerInputState input)
    {
        if (input.HorizontalInput != 0)
        {
            networkFacingDirection.Value = Mathf.Sign(input.HorizontalInput);
        }
    }

    private void HandleFlipVisuals()
    {
        if (netIsSwimming.Value)
        {
            if (IsOwner || debugIsOwnerOverride)
            {
                Camera mainCam = Camera.main;
                if (mainCam != null)
                {
                    Vector3 mouseScreenPos = Input.mousePosition;
                    mouseScreenPos.z = Mathf.Abs(mainCam.transform.position.z - transform.position.z);
                    Vector3 mouseWorldPos = mainCam.ScreenToWorldPoint(mouseScreenPos);

                    Vector3 dir = mouseWorldPos - transform.position;
                    dir.z = 0f;

                    if (dir.sqrMagnitude > 0.01f)
                    {
                        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                        float facingSign = Mathf.Sign(dir.x);

                        if (facingSign < 0f)
                        {
                            transform.localScale = gameplayRootScale;
                            Quaternion targetRot = Quaternion.Euler(0f, 0f, angle + 180f);
                            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * swimRotationSpeed);
                        }
                        else
                        {
                            transform.localScale = gameplayRootScale;
                            Quaternion targetRot = Quaternion.Euler(0f, 0f, angle);
                            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * swimRotationSpeed);
                        }

                        if (IsSpawned)
                        {
                            networkSwimAngleZ.Value = angle;
                            networkFacingDirection.Value = facingSign;
                        }
                    }
                }
            }
            else
            {
                float facingSign = networkFacingDirection.Value;
                float angle = networkSwimAngleZ.Value;

                if (facingSign < 0f)
                {
                    transform.localScale = gameplayRootScale;
                    Quaternion targetRot = Quaternion.Euler(0f, 0f, angle + 180f);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * swimRotationSpeed);
                }
                else
                {
                    transform.localScale = gameplayRootScale;
                    Quaternion targetRot = Quaternion.Euler(0f, 0f, angle);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * swimRotationSpeed);
                }
            }
        }
        else
        {
            float facingSign = networkFacingDirection.Value;
            transform.localScale = gameplayRootScale;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.identity, Time.deltaTime * swimRotationSpeed);
        }
    }

    private void CacheVisualFacing()
    {
        visualRoot = animator != null ? animator.transform : transform.Find("Anim-Body");
        if (visualRoot == null) return;

        visualRootBaseRotation = visualRoot.localRotation;
        List<Transform> depthTransforms = new List<Transform>();

        foreach (Transform child in visualRoot.GetComponentsInChildren<Transform>(true))
        {
            // A Y rotation mirrors X but also reverses Z. Counter-mirror each authored
            // depth offset so eye/body layers keep the same camera distance. Eyeballs
            // own their world-space position and correct their Z in their LateUpdate.
            if (child != visualRoot && child.GetComponent<PlayerEyeballs>() == null)
            {
                depthTransforms.Add(child);
            }
        }

        visualDepthTransforms = depthTransforms.ToArray();
    }

    private void RestoreVisualFacingForAnimator()
    {
        if (!visualMirrorApplied || visualRoot == null) return;

        foreach (Transform depthTransform in visualDepthTransforms)
        {
            MirrorVisualDepth(depthTransform);
        }

        visualRoot.localRotation = visualRootBaseRotation;
        visualMirrorApplied = false;
    }

    private void ApplyVisualFacingAfterAnimation()
    {
        bool faceLeft = networkFacingDirection.Value < 0f;
        if (!faceLeft || visualRoot == null) return;

        visualRoot.localRotation = visualRootBaseRotation * Quaternion.Euler(0f, 180f, 0f);
        foreach (Transform depthTransform in visualDepthTransforms)
        {
            MirrorVisualDepth(depthTransform);
        }

        visualMirrorApplied = true;
    }

    private static void MirrorVisualDepth(Transform visualTransform)
    {
        Vector3 localPosition = visualTransform.localPosition;
        localPosition.z = -localPosition.z;
        visualTransform.localPosition = localPosition;
    }

    private void HandleClimb(PlayerInputState input)
    {
        // CheckSphere, not OverlapSphere: only presence matters, and OverlapSphere
        // allocated a Collider[] every FixedUpdate.
        bool onLadder = Physics.CheckSphere(transform.position, 0.2f, ladderLayer,
                                            QueryTriggerInteraction.Collide);

        if (onLadder)
        {
            if (Mathf.Abs(input.VerticalInput) > 0.1f)
            {
                netIsClimbing.Value = true;
            }
        }
        else
        {
            netIsClimbing.Value = false;
        }

        if (netIsClimbing.Value)
        {
            rb.useGravity = false;
            Vector3 vel = rb.linearVelocity;
            vel.x = 0f;
            vel.y = input.VerticalInput * climbSpeed;
            rb.linearVelocity = vel;
            if (animator != null) animator.SetFloat("climbSpeed", Mathf.Abs(input.VerticalInput));
        }
        else
        {
            rb.useGravity = true;
        }
    }

    public void SetInteractableObject(MachineInstance machineInstance)
    {
        _currentMachineInstance = machineInstance;
    }

    public void ClearInteractableObject(MachineInstance machineInstance)
    {
        if (_currentMachineInstance == machineInstance)
        {
            _currentMachineInstance = null;
        }
    }

    private void HandleInteract(PlayerInputState input)
    {
        if (input.InteractPressed && !netIsInteracting.Value && _currentMachineInstance != null)
        {
            _currentMachineInstance.OnInteract(this);
        }
    }

    private void HandleDropThrough(PlayerInputState input)
    {
        if (input.DropPressed && currentPlatformCollider != null && !isDropping)
        {
            StartCoroutine(DropThroughPlatform(currentPlatformCollider));
        }
    }

    private IEnumerator DropThroughPlatform(Collider platformCollider)
    {
        if (bodyCollider == null || platformCollider == null) yield break;

        isDropping = true;
        ignoredPlatform = platformCollider;
        Physics.IgnoreCollision(bodyCollider, ignoredPlatform, true);

        yield return new WaitForSeconds(dropThroughDuration);

        RestorePlatformCollision();
        isDropping = false;
    }

    /// <summary>
    /// Physics.IgnoreCollision is global engine state - if it is not undone the platform
    /// stays permanently non-solid for this player. Called from the drop coroutine and
    /// from every teardown path, since a despawn/disable kills the coroutine mid-flight.
    /// </summary>
    private void RestorePlatformCollision()
    {
        if (bodyCollider != null && ignoredPlatform != null)
        {
            Physics.IgnoreCollision(bodyCollider, ignoredPlatform, false);
        }
        ignoredPlatform = null;
    }

    public override void OnNetworkDespawn()
    {
        RestorePlatformCollision();
        isDropping = false;
        UnregisterGameplayCursor();
        base.OnNetworkDespawn();
    }

    public override void OnDestroy()
    {
        UnregisterGameplayCursor();
        base.OnDestroy();
    }

    private void RegisterGameplayCursor()
    {
        if (gameplayCursorRegistered) return;

        gameplayCursorRegistered = true;
        CursorVisibilityController.EnterGameplay(this);
    }

    private void UnregisterGameplayCursor()
    {
        if (!gameplayCursorRegistered) return;

        gameplayCursorRegistered = false;
        CursorVisibilityController.ExitGameplay(this);
    }

    private void OnDisable()
    {
        RestoreVisualFacingForAnimator();
        RestorePlatformCollision();
        isDropping = false;
    }

    private void HandleAnimation()
    {
        if (animator == null) return;
        animator.SetBool("IsWalking", netIsWalking.Value);
        animator.SetBool("IsClimbing", netIsClimbing.Value);
        animator.SetBool("IsRunning", netIsRunning.Value);
    }

    // ─── Footstep Audio ───────────────────────────────────────────────
    /// <summary>
    /// Starts/stops a looping footstep AudioSource based on movement state.
    /// Runs on ALL clients via NetworkVariables so every player hears
    /// every other player's footsteps with SpatialAudioManager processing.
    /// The sound stops immediately when the character stops moving.
    /// </summary>
    private void HandleFootsteps()
    {
        if (footstepClip == null || footstepAudioSource == null) return;

        bool shouldPlaySteps = netIsWalking.Value && netIsGrounded.Value;

        if (shouldPlaySteps)
        {
            if (!footstepAudioSource.isPlaying)
            {
                footstepAudioSource.clip = footstepClip;
                footstepAudioSource.Play();
            }
            bool running = netIsRunning.Value;
            footstepAudioSource.pitch = running ? runPitch : walkPitch;
            footstepSFXSource.SetBaseVolume(running ? runStepVolume : walkStepVolume);
        }
        else
        {
            if (footstepAudioSource.isPlaying)
            {
                footstepAudioSource.Stop();
            }
        }
    }
    private void OnCollisionEnter(Collision collision)
    {
        if (!IsOwner && !debugIsOwnerOverride) return;

        HandleOneWayContact(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!IsOwner && !debugIsOwnerOverride) return;

        HandleOneWayContact(collision);
    }

    private void HandleOneWayContact(Collision collision)
    {
        Collider col = collision.collider;
        if (col == null) return;

        // Only one-way platforms take part. Without this filter the player latches the
        // ship floor and pressing S drops them out of the world.
        if (((1 << col.gameObject.layer) & oneWayPlatformLayer.value) == 0) return;

        // Moving up into the platform from below: pass through it. This restores what
        // PlatformEffector2D's surfaceArc used to do for free - PhysX has no equivalent,
        // so the 2D->3D port lost jump-up-through entirely until this was added back.
        if (rb != null && bodyCollider != null && !isDropping &&
            rb.linearVelocity.y > 0.01f &&
            bodyCollider.bounds.min.y < col.bounds.max.y)
        {
            if (ignoredPlatform != col)
            {
                RestorePlatformCollision();
                ignoredPlatform = col;
            }
            Physics.IgnoreCollision(bodyCollider, col, true);
            return;
        }

        // Standing ON it - a contact normal pointing up means the surface is below us.
        // Scan the whole manifold: contact 0 can be a lateral edge hit on a real landing.
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y >= 0.5f)
            {
                currentPlatformCollider = col;
                return;
            }
        }
    }

    /// <summary>
    /// Clears a jump-up-through ignore once the player is clear of the platform, either
    /// above it (the jump succeeded) or back below it (it did not). While the pair is
    /// ignored no collision callbacks fire for it, so this poll is the only way back.
    /// </summary>
    private void UpdateOneWayPassthrough()
    {
        if (isDropping || ignoredPlatform == null || bodyCollider == null) return;

        Bounds me = bodyCollider.bounds;
        Bounds platform = ignoredPlatform.bounds;

        if (me.min.y >= platform.max.y - 0.01f || me.max.y <= platform.min.y + 0.01f)
        {
            RestorePlatformCollision();
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (!IsOwner && !debugIsOwnerOverride) return;

        if (collision.collider != null && collision.collider == currentPlatformCollider)
        {
            currentPlatformCollider = null;
        }
    }
}
