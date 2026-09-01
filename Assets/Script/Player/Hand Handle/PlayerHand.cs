using Unity.Netcode;
using UnityEngine;

public enum PlayerHandDisplayState : byte
{
    Cursor = 0,
    Preview = 1,
    Holding = 2
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[DefaultExecutionOrder(10000)]
public sealed class PlayerHand : NetworkBehaviour
{
    [Header("Visuals")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite hoverSprite;
    [SerializeField] private Sprite defaultSprite;

    private CargoGrabController ownerGrabController;
    private Transform ownerTransform;
    private Rigidbody body;
    private Collider[] handColliders;
    private CargoHoldState presentedHoldState;
    private bool localPointerHeld;
    private bool localPointerWithinReach;
    private bool localCargoHovered;
    private bool visualOrientationCached;
    private bool isOnLeftSide;
    private bool rightFacingFlipX;
    private Vector3 rightFacingVisualEuler;

    public readonly NetworkVariable<ulong> ownerId = new NetworkVariable<ulong>(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public Rigidbody Body => body;
    public bool IsReady => ownerGrabController != null && ownerTransform != null && body != null;
    public CargoGrabController OwnerGrabController => ownerGrabController;
    public PlayerHandDisplayState DisplayState { get; private set; }
    public bool HasActiveInteractionCollider { get; private set; }

    public static bool ShouldRenderForPeer(bool isOwner, bool isHolding)
    {
        return isOwner || isHolding;
    }

    private void Awake()
    {
        CacheVisualOrientation();
        SpriteRenderOrderPolicy.ApplyHand(transform, spriteRenderer);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        CachePhysics();

        if (body == null)
        {
            Debug.LogError($"{name}: no 3D Rigidbody found on PlayerHand.", this);
            enabled = false;
            return;
        }

        body.isKinematic = true;
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
        body.interpolation = RigidbodyInterpolation.None;

        RefreshInteractionState();

        ownerId.OnValueChanged += OnOwnerIdChanged;
        TryResolveOwner();
    }

    public override void OnNetworkDespawn()
    {
        ownerId.OnValueChanged -= OnOwnerIdChanged;

        CargoGrabController previousOwner = ownerGrabController;
        ownerGrabController = null;
        ownerTransform = null;
        previousOwner?.UnregisterHand(this);
        base.OnNetworkDespawn();
    }

    private void OnDisable()
    {
        CargoGrabController previousOwner = ownerGrabController;
        ownerGrabController = null;
        ownerTransform = null;
        previousOwner?.UnregisterHand(this);
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
    }

    public override void OnGainedOwnership()
    {
        base.OnGainedOwnership();
        RefreshInteractionState();
    }

    public override void OnLostOwnership()
    {
        base.OnLostOwnership();
        localPointerHeld = false;
        localPointerWithinReach = false;
        localCargoHovered = false;
        RefreshInteractionState();
    }

    private void Update()
    {
        if (ownerGrabController == null) TryResolveOwner();
    }

    private void LateUpdate()
    {
        if (TryGetHeldGripPoint(out Vector3 heldPoint))
        {
            // Cargo is interpolated independently on each peer. Resolving the hand from the same
            // interpolated transform once per rendered frame avoids Rigidbody/NetworkTransform writers
            // fighting each other and guarantees a zero visual gap without a physics joint.
            SetWorldPosition(heldPoint);
        }
        else
        {
            // Run after the follow camera's LateUpdate. Projecting the cursor before the camera
            // settles causes a one-frame screen-space oscillation even while the mouse is still.
            UpdateFreeHandPosition();
        }

        HandleFlipping();
    }

    public void Initialize(CargoGrabController owner)
    {
        if (owner == null || ownerGrabController == owner) return;

        ownerGrabController?.UnregisterHand(this);
        ownerGrabController = owner;
        ownerTransform = owner.transform;
        owner.RegisterHand(this);

        RefreshInteractionState();
    }

    public bool IsValidForOwner(CargoGrabController controller)
    {
        return controller != null
               && IsSpawned
               && NetworkObject != null
               && NetworkObject.IsSpawned
               && NetworkObject.OwnerClientId == controller.OwnerClientId
               && ownerId.Value == controller.NetworkObjectId;
    }

    public bool TryFindCargoContact(
        CargoHoldSolver cargo,
        float tolerance,
        out Vector3 contactPoint)
    {
        CachePhysics();
        if (cargo == null)
        {
            contactPoint = default;
            return false;
        }

        return GripContactUtility.TryFindContact(
            handColliders,
            cargo.GetSolidCargoColliders(),
            tolerance,
            out contactPoint);
    }

    public void ApplyHoldState(CargoHoldState state)
    {
        presentedHoldState = state;
        RefreshInteractionState();
    }

    public void ApplyLocalPointerState(
        bool pointerHeld,
        bool pointerWithinReach,
        bool cargoHovered)
    {
        if (!IsLocallyControlled) return;
        bool reachableCargoHovered = cargoHovered && pointerWithinReach;
        if (localPointerHeld == pointerHeld
            && localPointerWithinReach == pointerWithinReach
            && localCargoHovered == reachableCargoHovered)
        {
            return;
        }

        localPointerHeld = pointerHeld;
        localPointerWithinReach = pointerWithinReach;
        localCargoHovered = reachableCargoHovered;
        RefreshInteractionState();
    }

    private void OnOwnerIdChanged(ulong previous, ulong current)
    {
        if (ownerGrabController != null && ownerGrabController.NetworkObjectId != current)
        {
            CargoGrabController previousOwner = ownerGrabController;
            ownerGrabController = null;
            ownerTransform = null;
            previousOwner.UnregisterHand(this);
        }

        TryResolveOwner();
    }

    private void TryResolveOwner()
    {
        if (ownerId.Value == ulong.MaxValue || NetworkManager == null || NetworkManager.SpawnManager == null) return;

        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(ownerId.Value, out NetworkObject ownerObject))
        {
            Initialize(ownerObject.GetComponent<CargoGrabController>());
        }
    }

    private bool TryGetHeldGripPoint(out Vector3 worldPoint)
    {
        worldPoint = default;
        CargoHoldState state = ownerGrabController != null
            ? ownerGrabController.CurrentHoldState
            : presentedHoldState;
        if (!state.IsActive || !state.Cargo.TryGet(out NetworkObject cargoObject)) return false;

        worldPoint = cargoObject.transform.TransformPoint(
            new Vector3(state.LocalGrabPoint.x, state.LocalGrabPoint.y, 0f));
        Rigidbody cargoBody = cargoObject.GetComponent<Rigidbody>();
        worldPoint.z = cargoBody != null ? cargoBody.position.z : cargoObject.transform.position.z;
        return true;
    }

    private void UpdateFreeHandPosition()
    {
        if (!IsOwner || presentedHoldState.IsActive || body == null || ownerTransform == null
            || ownerGrabController == null)
            return;

        CursorIntentProvider intent = ownerGrabController.CursorIntentProvider;
        if (intent == null || !intent.RefreshIntent()) return;

        Vector2 freeIntent = intent.CurrentWorldIntent;
        SetWorldPosition(new Vector3(freeIntent.x, freeIntent.y, ownerTransform.position.z));
    }

    private void SetWorldPosition(Vector3 worldPosition)
    {
        if (body != null) body.position = worldPosition;
        else transform.position = worldPosition;
    }

    private void RefreshInteractionState()
    {
        RefreshColliderPolicy();
        RefreshPresentation();
    }

    private void RefreshColliderPolicy()
    {
        CachePhysics();
        if (handColliders == null) return;

        bool isHolding = presentedHoldState.IsActive;
        bool localInteractionProbe = IsLocallyControlled && localPointerHeld && localPointerWithinReach;
        bool serverRemoteValidationProbe = IsSpawned && IsServer && !IsOwner;
        bool shouldEnable = isHolding || localInteractionProbe || serverRemoteValidationProbe;

        foreach (Collider handCollider in handColliders)
        {
            if (handCollider == null) continue;
            handCollider.isTrigger = true;
            handCollider.enabled = shouldEnable;
        }

        HasActiveInteractionCollider = shouldEnable;
    }

    private void RefreshPresentation()
    {
        if (spriteRenderer == null) return;

        bool isHolding = presentedHoldState.IsActive;
        bool showPreview = IsLocallyControlled && (localCargoHovered || localPointerHeld);
        DisplayState = isHolding
            ? PlayerHandDisplayState.Holding
            : showPreview
                ? PlayerHandDisplayState.Preview
                : PlayerHandDisplayState.Cursor;

        spriteRenderer.sprite = DisplayState == PlayerHandDisplayState.Cursor ? defaultSprite : hoverSprite;
        spriteRenderer.enabled = ShouldRenderForPeer(IsLocallyControlled, isHolding);
    }

    private void CachePhysics()
    {
        if (body == null) body = GetComponent<Rigidbody>();
        if (handColliders == null || handColliders.Length == 0)
        {
            handColliders = GetComponentsInChildren<Collider>(true);
        }
    }

    private void HandleFlipping()
    {
        if (spriteRenderer == null || ownerTransform == null) return;

        CacheVisualOrientation();
        float centerOffset = transform.position.x - ownerTransform.position.x;
        if (centerOffset < -0.001f) isOnLeftSide = true;
        else if (centerOffset > 0.001f) isOnLeftSide = false;

        // Keep the authored right-facing sprite pose intact. The left-side pose is a
        // half-turn around Y, so a default (0, 0, 120) becomes exactly (0, 180, 120)
        // without changing the authored X flip or mirroring the Z angle.
        spriteRenderer.flipX = rightFacingFlipX;
        Vector3 visualEuler = rightFacingVisualEuler;
        if (isOnLeftSide) visualEuler.y = 180f;
        spriteRenderer.transform.localRotation = Quaternion.Euler(visualEuler);
    }

    private void CacheVisualOrientation()
    {
        if (visualOrientationCached || spriteRenderer == null) return;

        rightFacingFlipX = spriteRenderer.flipX;
        rightFacingVisualEuler = spriteRenderer.transform.localEulerAngles;
        rightFacingVisualEuler.z = Mathf.DeltaAngle(0f, rightFacingVisualEuler.z);
        visualOrientationCached = true;
    }

    private bool IsLocallyControlled
    {
        get
        {
            if (!IsSpawned) return true;
            if (IsOwner) return true;

            // During the ownership handoff, IsOwner can lag one callback behind the replicated
            // owner id. Keep the local gameplay cursor visible without exposing remote free cursors.
            return NetworkManager != null
                   && NetworkManager.IsClient
                   && NetworkObject != null
                   && NetworkObject.OwnerClientId == NetworkManager.LocalClientId;
        }
    }
}
