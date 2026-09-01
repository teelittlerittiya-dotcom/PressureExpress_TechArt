using System.Collections;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CharacterController2D_1 : NetworkBehaviour
{
    [SerializeField] private bool debugIsOwnerOverride = false;
    // One-way platform: store platform collider for drop-through logic
    private Collider currentPlatformCollider;

    [Header("Movement")]
    [SerializeField] float moveSpeed = 5f;
    [SerializeField] private float runSpeedBonus = 4f;
    [SerializeField] float jumpForce = 7f;
    [SerializeField] private bool canJump = true;

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

    [Header("Grab System")]
    [SerializeField] Transform grabPoint;
    [SerializeField] Transform rayPoint;
    [SerializeField] float rayDistance = 1f;
    public float RayDistance => rayDistance;
    [SerializeField] LayerMask grabLayer;

    [Header("One Way Platform")]
    // Defaults to layer 14 ("Platform"). A LayerMask with no initialiser deserialises to
    // Nothing on prefabs saved before the field existed, which silently disables drop-through.
    [SerializeField] LayerMask oneWayPlatformLayer = 1 << 14;
    [SerializeField] int dropThroughTicks = 15;

    [Header("Hand Setup")]
    [SerializeField] private GameObject handPrefab;

    private Rigidbody rb;
    [SerializeField] private Animator animator;
    private PlayerInputState currentInput;

    private bool isGrounded;
    private bool isWalking;
    private bool isRunning;
    private bool isSwimming;
    private bool wasSwimming;
    private NetworkVariable<float> networkFacingDirection = new NetworkVariable<float>(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Interacting")]
    public bool isInteracting;
    private MachineInstance _currentMachineInstance;

    private int currentTick = 0;
    private const int BUFFER_SIZE = 1024;
    private PlayerInputState[] inputBuffer = new PlayerInputState[BUFFER_SIZE];
    private PlayerState[] stateBuffer = new PlayerState[BUFFER_SIZE];
    private PlayerState lastServerState;
    private bool hasReceivedInitialState = false;
    private int currentDropTimer = 0;

    private Vector3 targetPosition;
    private float syncLerpRate = 15f;

    // Cached so SimulateMovement does not GetComponent every tick (it runs once per
    // FixedUpdate plus once per replayed tick during reconciliation).
    private Collider bodyCollider;
    // The platform this player is currently allowed to pass through. Physics.IgnoreCollision
    // is global engine state, so the pair is tracked separately from currentPlatformCollider
    // (which OnCollisionExit nulls the moment we start falling through).
    private Collider ignoredPlatform;

    private struct PlayerInputState : INetworkSerializable
    {
        public int Tick;
        public float HorizontalInput;
        public float VerticalInput;
        public bool JumpPressed;
        public bool InteractPressed;
        public bool DropPressed;
        public bool RunPressed;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Tick);
            serializer.SerializeValue(ref HorizontalInput);
            serializer.SerializeValue(ref VerticalInput);
            serializer.SerializeValue(ref JumpPressed);
            serializer.SerializeValue(ref InteractPressed);
            serializer.SerializeValue(ref DropPressed);
            serializer.SerializeValue(ref RunPressed);
        }
    }

    private struct PlayerState : INetworkSerializable
    {
        public int Tick;
        public Vector3 Position;
        public Vector3 Velocity;
        public bool IsClimbing;
        public bool IsSwimming;
        public bool UseGravity;
        public int DropTimer;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Tick);
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Velocity);
            serializer.SerializeValue(ref IsClimbing);
            serializer.SerializeValue(ref IsSwimming);
            serializer.SerializeValue(ref UseGravity);
            serializer.SerializeValue(ref DropTimer);
        }
    }

    public override void OnNetworkSpawn()
    {
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

        // Lock Z axis and all rotations for 2.5D gameplay
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;

        if (IsOwner || debugIsOwnerOverride)
        {
            rb.interpolation = RigidbodyInterpolation.None;
            SpawnHandServerRpc(OwnerClientId);

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
        else if (!IsServer)
        {
            targetPosition = transform.position;
            rb.isKinematic = true;
        }

        if (IsServer)
        {
            lastServerState = new PlayerState
            {
                Tick = 0,
                Position = transform.position,
                Velocity = rb.linearVelocity,
                IsClimbing = false,
                IsSwimming = false,
                UseGravity = true,
                DropTimer = 0
            };
        }
    }

    [ServerRpc]
    private void SpawnHandServerRpc(ulong clientId)
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
        if (IsOwner || debugIsOwnerOverride)
        {
            currentInput = new PlayerInputState
            {
                HorizontalInput = Input.GetAxisRaw("Horizontal"),
                VerticalInput = Input.GetAxisRaw("Vertical"),
                JumpPressed = Input.GetKey(KeyCode.Space),
                InteractPressed = Input.GetKeyDown(KeyCode.E),
                DropPressed = Input.GetKey(KeyCode.S) && Input.GetKeyDown(KeyCode.Space),
                RunPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)
            };
        }
        else if (!IsServer)
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * syncLerpRate);
        }

        HandleFlipVisuals();
        HandleAnimation();
    }

    private void FixedUpdate()
    {
        if (IsOwner || debugIsOwnerOverride)
        {
            ClientTick();
        }

        if (IsServer)
        {
            ServerTick();
        }
    }

    private void ClientTick()
    {
        currentInput.Tick = currentTick;
        int bufferIndex = currentTick % BUFFER_SIZE;
        inputBuffer[bufferIndex] = currentInput;

        if (hasReceivedInitialState)
        {
            int serverTickIndex = lastServerState.Tick % BUFFER_SIZE;
            PlayerState predictedState = stateBuffer[serverTickIndex];

            float errorMargin = Vector3.Distance(predictedState.Position, lastServerState.Position);

            if (errorMargin > 0.05f)
            {
                transform.position = lastServerState.Position;
                rb.linearVelocity = lastServerState.Velocity;
                rb.useGravity = lastServerState.UseGravity;
                currentDropTimer = lastServerState.DropTimer;

                bool wasClimbing = lastServerState.IsClimbing;
                bool wasSwimmingState = lastServerState.IsSwimming;

                for (int i = lastServerState.Tick + 1; i <= currentTick; i++)
                {
                    int replayIndex = i % BUFFER_SIZE;
                    PlayerInputState replayInput = inputBuffer[replayIndex];

                    SimulateMovement(replayInput, ref wasClimbing, ref wasSwimmingState, isReplay: true);

                    transform.position += rb.linearVelocity * Time.fixedDeltaTime;

                    stateBuffer[replayIndex] = new PlayerState
                    {
                        Tick = i,
                        Position = transform.position,
                        Velocity = rb.linearVelocity,
                        IsClimbing = wasClimbing,
                        IsSwimming = wasSwimmingState,
                        UseGravity = rb.useGravity,
                        DropTimer = currentDropTimer
                    };
                }

                // The replay loop deliberately made no engine-state changes; bring the
                // IgnoreCollision pair in line with the reconciled timer exactly once.
                ReconcileDropIgnore();
            }
        }

        PlayerState prevBuffered = stateBuffer[(currentTick > 0 ? currentTick - 1 : 0) % BUFFER_SIZE];
        bool currentClimbState = prevBuffered.IsClimbing;
        bool currentSwimState = prevBuffered.IsSwimming;

        SimulateMovement(currentInput, ref currentClimbState, ref currentSwimState);

        // Once per tick, after the replay loop - not once per replayed tick from inside
        // SimulateMovement, which made reconciliation hitches far worse than they need to be.
        Physics.SyncTransforms();

        stateBuffer[bufferIndex] = new PlayerState
        {
            Tick = currentTick,
            Position = transform.position,
            Velocity = rb.linearVelocity,
            IsClimbing = currentClimbState,
            IsSwimming = currentSwimState,
            UseGravity = rb.useGravity,
            DropTimer = currentDropTimer
        };

        isSwimming = currentSwimState;

        SubmitInputServerRpc(currentInput);
        currentTick++;
    }

    private void ServerTick()
    {
    }

    [Rpc(SendTo.Server)]
    private void SubmitInputServerRpc(PlayerInputState input)
    {
        bool climbState = lastServerState.IsClimbing;
        bool swimState = lastServerState.IsSwimming;
        SimulateMovement(input, ref climbState, ref swimState);
        Physics.SyncTransforms();

        PlayerState newState = new PlayerState
        {
            Tick = input.Tick,
            Position = transform.position,
            Velocity = rb.linearVelocity,
            IsClimbing = climbState,
            IsSwimming = swimState,
            UseGravity = rb.useGravity,
            DropTimer = currentDropTimer
        };

        lastServerState = newState;
        UpdateClientStateClientRpc(newState);
    }

    [ClientRpc]
    private void UpdateClientStateClientRpc(PlayerState serverState)
    {
        if (IsOwner)
        {
            lastServerState = serverState;
            hasReceivedInitialState = true;
        }
        else if (!IsServer)
        {
            targetPosition = serverState.Position;
            if (rb != null)
            {
                rb.linearVelocity = serverState.Velocity;
            }
        }
    }

    private void SimulateMovement(PlayerInputState input, ref bool isClimbingLocal, ref bool isSwimmingLocal, bool isReplay = false)
    {
        if (isInteracting)
        {
            rb.linearVelocity = Vector3.zero;
            isWalking = false;
            isClimbingLocal = false;
            isSwimmingLocal = false;
            rb.useGravity = true;
            return;
        }

        if (input.InteractPressed && !isInteracting && _currentMachineInstance != null)
        {
            //currentInteractable.OnInteract(this);
        }

        isGrounded = Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundLayer);

        // Water detection & swimming physics
        bool inWater = RoomWaterVisualizer.TryGetWaterSurfaceY(transform.position, out float waterSurfaceY, out RoomWaterVisualizer roomWater);
        float depth = inWater ? (waterSurfaceY - transform.position.y) : 0f;
        bool shouldSwim = inWater && depth > 0.4f;

        if (shouldSwim && !isClimbingLocal)
        {
            if (!isSwimmingLocal)
            {
                isSwimmingLocal = true;
                if (!isReplay)
                {
                    if (splashParticlePrefab != null)
                        Instantiate(splashParticlePrefab, new Vector3(transform.position.x, waterSurfaceY, transform.position.z), Quaternion.identity);
                    if (waterAudioSource != null && splashSound != null)
                        waterAudioSource.PlayOneShot(splashSound);
                    if (roomWater != null)
                        roomWater.TriggerRipple(new Vector3(transform.position.x, waterSurfaceY, transform.position.z), true);
                }
            }
        }
        else if (isSwimmingLocal && depth <= 0.15f)
        {
            isSwimmingLocal = false;
            rb.useGravity = true;
            if (!isReplay && roomWater != null)
            {
                roomWater.TriggerRipple(new Vector3(transform.position.x, waterSurfaceY, transform.position.z), false);
            }
        }

        if (isSwimmingLocal)
        {
            rb.useGravity = false;
            Vector3 currentVel = rb.linearVelocity;
            Vector3 targetVel = Vector3.zero;

            targetVel.x = input.HorizontalInput * swimSpeed;
            isWalking = input.HorizontalInput != 0;

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
                targetVel.y = 0.25f; // Gentle buoyancy float
            }

            // Surface jump out of water if close to surface
            if (depth < 0.6f && input.JumpPressed)
            {
                targetVel.y = surfaceJumpForce;
                isSwimmingLocal = false;
                rb.useGravity = true;
            }

            rb.linearVelocity = Vector3.Lerp(currentVel, targetVel, Time.fixedDeltaTime * waterDrag);
        }
        else
        {
            bool onLadder = Physics.CheckSphere(transform.position, 0.2f, ladderLayer,
                                                QueryTriggerInteraction.Collide);
            if (onLadder)
            {
                if (Mathf.Abs(input.VerticalInput) > 0.1f)
                {
                    isClimbingLocal = true;
                    rb.useGravity = false;
                }
            }
            else if (!isClimbingLocal || Mathf.Abs(input.VerticalInput) == 0)
            {
                isClimbingLocal = false;
                rb.useGravity = true;
            }

            if (isClimbingLocal)
            {
                if (Mathf.Abs(input.HorizontalInput) > 0.1f)
                {
                    isClimbingLocal = false;
                    rb.useGravity = true;
                }
                Vector3 vel = rb.linearVelocity;
                vel.x = 0f;
                vel.y = input.VerticalInput * climbSpeed;
                rb.linearVelocity = vel;
            }
            else
            {
                isRunning = input.RunPressed;
                float currentRunBonus = isRunning ? runSpeedBonus : 0f;
                float finalSpeed = moveSpeed + currentRunBonus;
                if (inWater && depth > 0f) finalSpeed *= 0.7f; // Shallow water drag

                Vector3 vel = rb.linearVelocity;
                vel.x = input.HorizontalInput * finalSpeed;
                rb.linearVelocity = vel;
                isWalking = input.HorizontalInput != 0;
            }

            if (input.JumpPressed && isGrounded && canJump && !isClimbingLocal)
            {
                if (rb.linearVelocity.y <= 0.1f)
                {
                    Vector3 vel = rb.linearVelocity;
                    vel.y = jumpForce;
                    rb.linearVelocity = vel;
                }
            }
        }

        // Drop-through is edge-triggered: set the ignore once when the drop starts and clear
        // it once when the timer runs out. The old code re-asserted it every tick from inside
        // `if (currentPlatformCollider != null)`, so falling through the platform (which nulls
        // that reference via OnCollisionExit) meant the restoring call never ran and the
        // platform stayed passable forever.
        // During replay only the predicted STATE is advanced - Physics.IgnoreCollision is
        // global engine state and must not be toggled once per replayed tick.
        if (input.DropPressed && currentPlatformCollider != null && currentDropTimer <= 0)
        {
            ignoredPlatform = currentPlatformCollider;
            currentDropTimer = dropThroughTicks;
            if (!isReplay) ApplyDropIgnore();
        }
        else if (currentDropTimer > 0)
        {
            currentDropTimer--;
            if (currentDropTimer <= 0 && !isReplay) RestorePlatformCollision();
        }

        if (input.HorizontalInput != 0)
        {
            if (IsServer)
            {
                networkFacingDirection.Value = Mathf.Sign(input.HorizontalInput);
            }

            if (IsOwner)
            {
                transform.localScale = Vector3.one;
            }
        }
    }

    /// <summary>
    /// Undo the drop-through ignore pair. Physics.IgnoreCollision is global engine state,
    /// so this has to run on every teardown path or the platform stays permanently
    /// non-solid for this player.
    /// </summary>
    private void ApplyDropIgnore()
    {
        if (bodyCollider != null && ignoredPlatform != null)
        {
            Physics.IgnoreCollision(bodyCollider, ignoredPlatform, true);
        }
    }

    private void RestorePlatformCollision()
    {
        if (bodyCollider != null && ignoredPlatform != null)
        {
            Physics.IgnoreCollision(bodyCollider, ignoredPlatform, false);
        }
        ignoredPlatform = null;
        currentDropTimer = 0;
    }

    /// <summary>
    /// Brings the global IgnoreCollision pair back in line with the predicted drop timer.
    /// Called once after reconciliation, because the replay loop advances currentDropTimer
    /// without touching engine state - and because rolling the timer back to 0 directly
    /// would otherwise strand the ignore pair permanently (it is not part of PlayerState).
    /// </summary>
    private void ReconcileDropIgnore()
    {
        if (currentDropTimer > 0 && ignoredPlatform != null) ApplyDropIgnore();
        else RestorePlatformCollision();
    }

    public override void OnNetworkDespawn()
    {
        RestorePlatformCollision();
    }

    private void OnDisable()
    {
        RestorePlatformCollision();
    }

    private void HandleFlipVisuals()
    {
        if (isSwimming)
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
                            transform.localScale = new Vector3(1f, -1f, 1f);
                            Quaternion targetRot = Quaternion.Euler(0f, 0f, angle + 180f);
                            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * swimRotationSpeed);
                        }
                        else
                        {
                            transform.localScale = Vector3.one;
                            Quaternion targetRot = Quaternion.Euler(0f, 0f, angle);
                            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * swimRotationSpeed);
                        }

                        if (IsSpawned)
                        {
                            networkSwimAngleZ.Value = angle;
                            if (IsServer) networkFacingDirection.Value = facingSign;
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
                    transform.localScale = new Vector3(1f, -1f, 1f);
                    Quaternion targetRot = Quaternion.Euler(0f, 0f, angle + 180f);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * swimRotationSpeed);
                }
                else
                {
                    transform.localScale = Vector3.one;
                    Quaternion targetRot = Quaternion.Euler(0f, 0f, angle);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * swimRotationSpeed);
                }
            }
        }
        else
        {
            float facingSign = networkFacingDirection.Value;
            transform.localScale = new Vector3(facingSign < 0f ? -1f : 1f, 1f, 1f);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.identity, Time.deltaTime * swimRotationSpeed);
        }
    }

    private void HandleAnimation()
    {
        if (animator == null) return;
        animator.SetBool("IsWalking", isWalking);

        PlayerState currentState = stateBuffer[(currentTick > 0 ? currentTick - 1 : 0) % BUFFER_SIZE];
        bool currentClimb = currentState.IsClimbing;
        bool currentSwim = currentState.IsSwimming;

        animator.SetBool("IsClimbing", currentClimb);
        animator.SetBool("IsSwimming", currentSwim);
        animator.SetBool("IsRunning", isRunning);

        if (currentClimb)
        {
            animator.SetFloat("climbSpeed", Mathf.Abs(currentInput.VerticalInput));
        }
        else
        {
            animator.SetFloat("climbSpeed", 1);
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

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }

    // NOTE: deliberately NOT gated on IsOwner. SubmitInputServerRpc re-runs SimulateMovement
    // on the server for a body that is dynamic there, and IsOwner is false on the server for
    // every client-owned player - gating here would leave the server's currentPlatformCollider
    // permanently null, so the drop branch could never fire and every drop would mispredict.
    private void OnCollisionEnter(Collision collision)
    {
        Collider col = collision.collider;
        if (col == null) return;

        // Only one-way platforms may be dropped through. Without this filter the player
        // latches the ship floor and pressing S+Space drops them out of the world.
        if (((1 << col.gameObject.layer) & oneWayPlatformLayer.value) == 0) return;

        // And only when standing ON it - an upward contact normal means the surface is below
        // us. Scan the whole manifold: contact 0 can be a lateral edge hit on a real landing.
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y >= 0.5f)
            {
                currentPlatformCollider = col;
                return;
            }
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.collider != null && collision.collider == currentPlatformCollider)
        {
            currentPlatformCollider = null;
        }
    }
}