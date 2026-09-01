using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CursorIntentProvider))]
public sealed class CargoGrabController : NetworkBehaviour
{
    private const int HoverRaycastCapacity = 16;
    private static readonly List<CargoGrabController> ActiveControllerList = new List<CargoGrabController>();

    [Header("Setup")]
    [SerializeField] private GripConfiguration configuration;
    [SerializeField] private CursorIntentProvider cursorIntentProvider;
    [SerializeField] private LayerMask grabbableLayer;

    private readonly NetworkVariable<CargoHoldState> replicatedHoldState = new NetworkVariable<CargoHoldState>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private PlayerHand registeredHand;
    private uint localTransitionSequence;
    private uint localIntentSequence;
    private uint lastServerTransitionSequence;
    private double lastServerIntentTime;
    private double hardReachExceededSince = -1d;
    private float nextGrabAttemptTime;
    private float nextIntentSendTime;
    private float lastIntentKeepaliveTime;
    private Vector2 lastSentIntent;
    private bool hasSentIntent;
    private bool wasPointerHeld;
    private readonly RaycastHit[] hoverRaycastHits = new RaycastHit[HoverRaycastCapacity];
    private CargoController localHoveredCargo;

    public static IReadOnlyList<CargoGrabController> ActiveControllers => ActiveControllerList;
    public GripConfiguration Configuration => configuration;
    public CursorIntentProvider CursorIntentProvider => cursorIntentProvider;
    public PlayerHand RegisteredHand => registeredHand;
    public CargoHoldState CurrentHoldState => replicatedHoldState.Value;
    public bool IsHolding => replicatedHoldState.Value.IsActive;
    public bool IsLocalPointerHeld { get; private set; }
    public bool IsLocalPointerWithinReach { get; private set; }
    public Vector2 LastServerAppliedForce { get; private set; }
    public Vector2 LastServerError { get; private set; }
    public Vector3 LastServerGripPoint { get; private set; }

    public event Action<CargoHoldState> HoldStateChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        ActiveControllerList.Clear();
    }

    private void Awake()
    {
        CacheReferences();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        CacheReferences();

        if (!ActiveControllerList.Contains(this)) ActiveControllerList.Add(this);
        replicatedHoldState.OnValueChanged += OnReplicatedHoldStateChanged;

        if (IsServer && !replicatedHoldState.Value.IsActive)
        {
            replicatedHoldState.Value = CargoHoldState.CreateReleased(0, CargoReleaseReason.None);
        }

        OnReplicatedHoldStateChanged(default, replicatedHoldState.Value);
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer) ServerRelease(CargoReleaseReason.PlayerDespawned);

        ClearLocalCargoHover();
        registeredHand?.ApplyLocalPointerState(false, false, false);

        replicatedHoldState.OnValueChanged -= OnReplicatedHoldStateChanged;
        ActiveControllerList.Remove(this);
        registeredHand?.ApplyHoldState(CargoHoldState.CreateReleased(
            replicatedHoldState.Value.StateVersion,
            CargoReleaseReason.PlayerDespawned));
        registeredHand = null;
        base.OnNetworkDespawn();
    }

    public override void OnDestroy()
    {
        ClearLocalCargoHover();
        ActiveControllerList.Remove(this);
        base.OnDestroy();
    }

    public override void OnLostOwnership()
    {
        ClearLocalCargoHover();
        IsLocalPointerHeld = false;
        IsLocalPointerWithinReach = false;
        registeredHand?.ApplyLocalPointerState(false, false, false);
        base.OnLostOwnership();
    }

    private void Update()
    {
        if (!IsOwner || configuration == null || cursorIntentProvider == null)
        {
            ClearLocalCargoHover();
            IsLocalPointerHeld = false;
            IsLocalPointerWithinReach = false;
            registeredHand?.ApplyLocalPointerState(false, false, false);
            return;
        }

        bool pointerHeld = Input.GetMouseButton(0);
        IsLocalPointerHeld = pointerHeld;
        if (!pointerHeld && wasPointerHeld)
        {
            localTransitionSequence++;
            RequestReleaseRpc(localTransitionSequence);
        }

        bool hasValidIntent = cursorIntentProvider.RefreshIntent();
        IsLocalPointerWithinReach = hasValidIntent && cursorIntentProvider.IsPointerWithinInteractionReach;
        UpdateLocalCargoHover(IsLocalPointerWithinReach);
        registeredHand?.ApplyLocalPointerState(
            pointerHeld,
            IsLocalPointerWithinReach,
            localHoveredCargo != null);

        if (!hasValidIntent)
        {
            wasPointerHeld = pointerHeld;
            return;
        }

        if (pointerHeld && IsLocalPointerWithinReach && !IsHolding && Time.unscaledTime >= nextGrabAttemptTime)
        {
            nextGrabAttemptTime = Time.unscaledTime + configuration.GrabRetrySeconds;
            TryRequestGrabAtPointer();
        }

        if (pointerHeld && IsHolding)
        {
            SendCursorIntentIfNeeded();
        }

        wasPointerHeld = pointerHeld;
    }

    public void Configure(GripConfiguration newConfiguration, CursorIntentProvider newIntentProvider)
    {
        configuration = newConfiguration;
        cursorIntentProvider = newIntentProvider;
        if (cursorIntentProvider != null) cursorIntentProvider.Configure(configuration);
    }

    public LayerMask GetGrabbableLayer()
    {
        return grabbableLayer;
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheReferences();
        if (configuration == null)
        {
            error = "GripConfiguration is missing.";
            return false;
        }

        if (!configuration.ValidateConfiguration(out error)) return false;
        if (cursorIntentProvider == null)
        {
            error = "CursorIntentProvider is missing.";
            return false;
        }

        if (cursorIntentProvider.Configuration != configuration)
        {
            error = "CargoGrabController and CursorIntentProvider must use the same GripConfiguration.";
            return false;
        }

        if (grabbableLayer.value == 0)
        {
            error = "Grabbable layer mask is empty.";
            return false;
        }

        error = null;
        return true;
    }

    public void RegisterHand(PlayerHand hand)
    {
        if (hand == null) return;
        registeredHand = hand;
        hand.ApplyHoldState(replicatedHoldState.Value);
        if (IsOwner)
        {
            hand.ApplyLocalPointerState(
                IsLocalPointerHeld,
                IsLocalPointerWithinReach,
                localHoveredCargo != null);
        }
    }

    public void UnregisterHand(PlayerHand hand)
    {
        if (registeredHand != hand) return;
        registeredHand = null;
        if (IsServer) ServerRelease(CargoReleaseReason.InvalidHand);
    }

    public bool IsHoldingCargo(ulong cargoNetworkObjectId)
    {
        return replicatedHoldState.Value.IsForCargo(cargoNetworkObjectId);
    }

    public bool TryGetAuthoritativeHold(
        CargoHoldSolver cargo,
        out CargoHoldState state,
        out PlayerHand hand)
    {
        state = replicatedHoldState.Value;
        hand = null;

        if (!IsServer || cargo == null || !state.IsForCargo(cargo.NetworkObjectId)) return false;
        if (!state.Hand.TryGet(out NetworkObject handObject)) return false;

        hand = handObject.GetComponent<PlayerHand>();
        if (hand == null) return false;
        if (registeredHand == null) registeredHand = hand;
        return true;
    }

    public bool ServerValidateHoldTick(CargoHoldSolver cargo, PlayerHand hand, Vector3 worldGripPoint)
    {
        if (!IsServer || cargo == null || hand == null)
        {
            if (IsServer) ServerRelease(CargoReleaseReason.InvalidHand);
            return false;
        }

        CargoHoldState state = replicatedHoldState.Value;
        if (!state.IsForCargo(cargo.NetworkObjectId)
            || !hand.IsValidForOwner(this)
            || !GripForceModel.IsFinite(GripForceModel.ProjectXY(worldGripPoint)))
        {
            ServerRelease(CargoReleaseReason.InvalidHand);
            return false;
        }

        double now = Time.unscaledTimeAsDouble;
        if (now - lastServerIntentTime > configuration.StaleIntentTimeoutSeconds)
        {
            ServerRelease(CargoReleaseReason.StaleIntent);
            return false;
        }

        float reach = Vector2.Distance(state.CursorIntent, GripForceModel.ProjectXY(worldGripPoint));
        if (reach > configuration.HardReach)
        {
            if (hardReachExceededSince < 0d) hardReachExceededSince = now;
            if (now - hardReachExceededSince >= configuration.HardReachGraceSeconds)
            {
                ServerRelease(CargoReleaseReason.HardReach);
                return false;
            }
        }
        else
        {
            hardReachExceededSince = -1d;
        }

        return true;
    }

    public void ServerRecordAppliedForce(GripForceResult result, Vector3 worldGripPoint)
    {
        if (!IsServer) return;
        LastServerAppliedForce = result.Force;
        LastServerError = result.Error;
        LastServerGripPoint = worldGripPoint;
    }

    public void ServerRelease(CargoReleaseReason reason)
    {
        if (!IsServer || !replicatedHoldState.Value.IsActive) return;

        uint nextVersion = replicatedHoldState.Value.StateVersion + 1u;
        replicatedHoldState.Value = CargoHoldState.CreateReleased(nextVersion, reason);
        lastServerIntentTime = 0d;
        hardReachExceededSince = -1d;
        LastServerAppliedForce = Vector2.zero;
        LastServerError = Vector2.zero;
    }

    private void TryRequestGrabAtPointer()
    {
        if (!cursorIntentProvider.IsPointerWithinInteractionReach
            || registeredHand == null
            || !registeredHand.IsReady
            || !registeredHand.HasActiveInteractionCollider
            || !cursorIntentProvider.TryGetSelectionRay(out Ray ray))
        {
            return;
        }

        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                configuration.MaximumWorldCoordinate,
                grabbableLayer,
                QueryTriggerInteraction.Collide))
        {
            return;
        }

        NetworkObject target = ResolveGrabbableNetworkObject(hit.collider);
        if (target == null || !target.IsSpawned) return;

        CargoHoldSolver solver = target.GetComponent<CargoHoldSolver>();
        if (solver == null
            || !registeredHand.TryFindCargoContact(solver, configuration.GrabContactTolerance, out _))
        {
            // Clicking a distant Cargo is not enough. The real Hand collider must already touch
            // its solid generated collider before any request is sent.
            return;
        }

        localTransitionSequence++;
        RequestGrabRpc(new NetworkObjectReference(target), localTransitionSequence);
    }

    private void UpdateLocalCargoHover(bool pointerWithinReach)
    {
        CargoController nextHoveredCargo = pointerWithinReach ? FindCargoUnderPointer() : null;
        if (localHoveredCargo == nextHoveredCargo) return;

        if (localHoveredCargo != null) localHoveredCargo.SetLocalPointerHover(false);
        localHoveredCargo = nextHoveredCargo;
        if (localHoveredCargo != null) localHoveredCargo.SetLocalPointerHover(true);
    }

    private CargoController FindCargoUnderPointer()
    {
        if (!cursorIntentProvider.TryGetSelectionRay(out Ray ray)) return null;

        int hitCount = Physics.RaycastNonAlloc(
            ray,
            hoverRaycastHits,
            configuration.MaximumWorldCoordinate,
            grabbableLayer,
            QueryTriggerInteraction.Collide);

        CargoController nearestCargo = null;
        float nearestDistance = float.PositiveInfinity;
        for (int index = 0; index < hitCount; index++)
        {
            RaycastHit hit = hoverRaycastHits[index];
            if (hit.distance >= nearestDistance) continue;

            CargoController cargo = ResolveCargoController(hit.collider);
            if (cargo == null || !cargo.isActiveAndEnabled) continue;

            nearestCargo = cargo;
            nearestDistance = hit.distance;
        }

        return nearestCargo;
    }

    private void ClearLocalCargoHover()
    {
        if (localHoveredCargo != null) localHoveredCargo.SetLocalPointerHover(false);
        localHoveredCargo = null;
    }

    private void SendCursorIntentIfNeeded()
    {
        float now = Time.unscaledTime;
        if (now < nextIntentSendTime) return;

        Vector2 quantized = GripForceModel.Quantize(
            cursorIntentProvider.CurrentWorldIntent,
            configuration.IntentQuantization);
        bool changed = !hasSentIntent
                       || (quantized - lastSentIntent).sqrMagnitude
                       >= configuration.IntentChangeThreshold * configuration.IntentChangeThreshold;
        bool keepaliveDue = !hasSentIntent || now - lastIntentKeepaliveTime >= configuration.IntentKeepaliveSeconds;
        if (!changed && !keepaliveDue) return;

        nextIntentSendTime = now + 1f / configuration.IntentSendRate;
        lastIntentKeepaliveTime = now;
        lastSentIntent = quantized;
        hasSentIntent = true;
        localIntentSequence++;
        SubmitCursorIntentRpc(quantized, localIntentSequence);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestGrabRpc(
        NetworkObjectReference targetReference,
        uint requestSequence,
        RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId
            || !IsNewerSequence(requestSequence, lastServerTransitionSequence))
        {
            return;
        }

        lastServerTransitionSequence = requestSequence;
        if (replicatedHoldState.Value.IsActive || configuration == null) return;
        if (!targetReference.TryGet(out NetworkObject target) || target == null || !target.IsSpawned) return;
        if (((1 << target.gameObject.layer) & grabbableLayer.value) == 0) return;

        CargoHoldSolver solver = target.GetComponent<CargoHoldSolver>();
        CargoController cargo = target.GetComponent<CargoController>();
        PlayerHand hand = ResolveServerHand();
        if (solver == null || cargo == null || hand == null || !solver.CanAcceptHolder(this)) return;
        if (!cargo.IsInitialized || cargo.Body == null || cargo.Body.isKinematic) return;
        if (!hand.IsValidForOwner(this)) return;

        Physics.SyncTransforms();
        if (!hand.TryFindCargoContact(solver, configuration.GrabContactTolerance, out Vector3 contactPoint))
        {
            return;
        }

        contactPoint.z = cargo.Body.position.z;
        Vector2 playerPosition = GripForceModel.ProjectXY(transform.position);
        Vector2 contactPosition = GripForceModel.ProjectXY(contactPoint);
        if (Vector2.Distance(playerPosition, contactPosition) > configuration.InitialGrabRange) return;

        Vector3 localPoint3 = target.transform.InverseTransformPoint(contactPoint);
        Vector2 localPoint = new Vector2(localPoint3.x, localPoint3.y);
        Vector2 initialIntent = GripForceModel.ProjectXY(hand.Body.position);
        uint nextVersion = replicatedHoldState.Value.StateVersion + 1u;

        replicatedHoldState.Value = CargoHoldState.CreateHolding(
            target,
            NetworkObject,
            hand.NetworkObject,
            localPoint,
            initialIntent,
            nextVersion);

        lastServerIntentTime = Time.unscaledTimeAsDouble;
        hardReachExceededSince = -1d;
    }

    [Rpc(
        SendTo.Server,
        Delivery = RpcDelivery.Unreliable,
        InvokePermission = RpcInvokePermission.Owner)]
    private void SubmitCursorIntentRpc(
        Vector2 cursorIntent,
        uint inputSequence,
        RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId || configuration == null) return;

        CargoHoldState state = replicatedHoldState.Value;
        if (!state.IsActive || !IsNewerSequence(inputSequence, state.LastAcceptedInputSequence)) return;
        if (!GripForceModel.IsFinite(cursorIntent)) return;
        if (Mathf.Abs(cursorIntent.x) > configuration.MaximumWorldCoordinate
            || Mathf.Abs(cursorIntent.y) > configuration.MaximumWorldCoordinate)
        {
            return;
        }

        double now = Time.unscaledTimeAsDouble;
        double minimumInterval = 1d / (configuration.IntentSendRate * 2d);
        if (lastServerIntentTime > 0d && now - lastServerIntentTime < minimumInterval) return;

        Vector2 playerPosition = GripForceModel.ProjectXY(transform.position);
        cursorIntent = GripForceModel.ClampToRadius(cursorIntent, playerPosition, configuration.FreeHandRadius);
        cursorIntent = GripForceModel.Quantize(cursorIntent, configuration.IntentQuantization);

        state.CursorIntent = cursorIntent;
        state.LastAcceptedInputSequence = inputSequence;
        replicatedHoldState.Value = state;
        lastServerIntentTime = now;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestReleaseRpc(uint requestSequence, RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId
            || !IsNewerSequence(requestSequence, lastServerTransitionSequence))
        {
            return;
        }

        lastServerTransitionSequence = requestSequence;
        ServerRelease(CargoReleaseReason.PlayerRequested);
    }

    private PlayerHand ResolveServerHand()
    {
        if (registeredHand != null && registeredHand.IsValidForOwner(this)) return registeredHand;

        PlayerHand[] hands = FindObjectsByType<PlayerHand>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
        foreach (PlayerHand hand in hands)
        {
            if (!hand.IsValidForOwner(this)) continue;
            registeredHand = hand;
            return hand;
        }

        return null;
    }

    private void OnReplicatedHoldStateChanged(CargoHoldState previous, CargoHoldState current)
    {
        if (!current.IsActive)
        {
            hasSentIntent = false;
            nextIntentSendTime = 0f;
        }

        registeredHand?.ApplyHoldState(current);
        HoldStateChanged?.Invoke(current);
    }

    private void CacheReferences()
    {
        if (cursorIntentProvider == null) cursorIntentProvider = GetComponent<CursorIntentProvider>();
    }

    private static bool IsNewerSequence(uint candidate, uint previous)
    {
        return unchecked((int)(candidate - previous)) > 0;
    }

    private static NetworkObject ResolveGrabbableNetworkObject(Collider hitCollider)
    {
        if (hitCollider == null) return null;

        if (hitCollider.attachedRigidbody != null
            && hitCollider.attachedRigidbody.TryGetComponent(out NetworkObject bodyNetworkObject))
        {
            return bodyNetworkObject;
        }

        return hitCollider.GetComponentInParent<NetworkObject>();
    }

    private static CargoController ResolveCargoController(Collider hitCollider)
    {
        if (hitCollider == null) return null;

        if (hitCollider.attachedRigidbody != null
            && hitCollider.attachedRigidbody.TryGetComponent(out CargoController bodyCargo))
        {
            return bodyCargo;
        }

        return hitCollider.GetComponentInParent<CargoController>();
    }

    private void OnDrawGizmosSelected()
    {
        if (configuration == null || !configuration.DrawDebugForces || !replicatedHoldState.Value.IsActive) return;

        Gizmos.color = Color.magenta;
        Vector3 cursor = GripForceModel.ToWorld(replicatedHoldState.Value.CursorIntent, transform.position.z);
        Gizmos.DrawWireSphere(cursor, 0.05f);
        Gizmos.DrawLine(LastServerGripPoint, cursor);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(
            LastServerGripPoint,
            LastServerGripPoint + GripForceModel.ToWorld(LastServerAppliedForce, 0f) * 0.02f);
    }
}
