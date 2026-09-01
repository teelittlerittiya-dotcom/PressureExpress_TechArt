using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CargoColliderBuilder))]
public sealed class CargoController : NetworkBehaviour
{
    private const float StateEpsilon = 0.0001f;

    [Header("Cargo Definition")]
    public CargoItemData cargoItemData;

    [Header("Required 2.5D Hierarchy")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Transform vfxAnchor;
    [SerializeField] private Transform uiAnchor;
    [SerializeField] private CargoProximitySensor proximitySensor;
    [SerializeField] private CargoColliderBuilder colliderBuilder;
    [SerializeField] private CargoPolishController polishController;

    [Header("Runtime Simulation")]
    [SerializeField, Min(0.02f)] private float statusTickInterval = 0.1f;
    [SerializeField, Min(0.1f)] private float roomQueryInterval = 0.5f;
    [SerializeField, Min(0.0001f)] private float planarTolerance = 0.001f;

    [Header("Protection")]
    [SerializeField, Min(0f)] private float initialInvincibilityDuration = 3f;

    [Header("UI Configuration")]
    [SerializeField] private UICargoInfo uiCargoInfoPrefab;
    [SerializeField] private Vector3 uiOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private float uiXOffset;

    public RoomMarker currentRoomMarker;

    private readonly NetworkVariable<CargoRuntimeState> replicatedState = new NetworkVariable<CargoRuntimeState>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly Dictionary<CargoModuleId, CargoModule> moduleCache = new Dictionary<CargoModuleId, CargoModule>();
    private CargoRuntimeState offlineState;
    private Rigidbody body;
    private UICargoInfo uiInstance;
    private bool definitionPrepared;
    private bool isInitialized;
    private bool isLocalPointerHovering;
    private bool isLocalDebugStatusUIVisible;
    private float invincibilityRemaining;
    private float lockedWorldZ;
    private float statusAccumulator;
    private float roomQueryAccumulator;
    private float lastImpactFixedTime = float.NegativeInfinity;

    public bool IsInitialized => isInitialized;
    public Rigidbody Body => body;
    public SpriteRenderer SpriteRenderer => spriteRenderer;
    public CargoColliderBuilder ColliderBuilder => colliderBuilder;
    public Transform VisualRoot => visualRoot;
    public Transform VfxAnchor => vfxAnchor;
    public CargoRuntimeState CurrentRuntimeState => IsSpawned ? replicatedState.Value : offlineState;
    public bool IsInvincible => invincibilityRemaining > StateEpsilon;
    public float InvincibilityRemaining => Mathf.Max(0f, invincibilityRemaining);
    public bool IsLocalDebugStatusUIVisible => isLocalDebugStatusUIVisible;
    public bool IsLocalPointerHovering => isLocalPointerHovering;

    public event Action<CargoRuntimeState> RuntimeStateChanged;
    public event Action<bool> LocalPointerHoverChanged;
    public event Action<CargoImpactPresentationEvent> ImpactPresentationRequested;

    private bool HasSimulationAuthority => !NetworkHelper.IsListening || IsServer;

    private void Awake()
    {
        CacheReferences();
        SpriteRenderOrderPolicy.ApplyCargo(visualRoot, spriteRenderer);
        lockedWorldZ = transform.position.z;
    }

    private void Start()
    {
        if (!IsSpawned && NetworkHelper.IsOffline)
        {
            if (PrepareDefinition()) InitializeAuthoritativeState();
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        replicatedState.OnValueChanged += OnReplicatedStateChanged;

        if (!PrepareDefinition()) return;

        if (IsServer)
        {
            InitializeAuthoritativeState();
        }
        else
        {
            OnRuntimeStateChanged(default, replicatedState.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        replicatedState.OnValueChanged -= OnReplicatedStateChanged;
        CleanupRoomRegistration();
        invincibilityRemaining = 0f;
        isLocalDebugStatusUIVisible = false;
        SetLocalPointerHover(false);
        base.OnNetworkDespawn();
    }

    public override void OnDestroy()
    {
        CleanupRoomRegistration();
        base.OnDestroy();
    }

    private void FixedUpdate()
    {
        if (!definitionPrepared) return;

        if (HasSimulationAuthority)
        {
            EnforcePlanarInvariant();
            AdvanceInvincibility(Time.fixedDeltaTime);
        }

        if (!isInitialized || !HasSimulationAuthority) return;

        roomQueryAccumulator += Time.fixedDeltaTime;
        if (roomQueryAccumulator >= roomQueryInterval)
        {
            roomQueryAccumulator = 0f;
            ResolveCurrentRoom();
        }

        statusAccumulator += Time.fixedDeltaTime;
        if (statusAccumulator >= statusTickInterval)
        {
            float elapsed = statusAccumulator;
            statusAccumulator = 0f;
            SimulateStatuses(elapsed);
        }
    }

    public void ConfigureReferences(
        Transform newVisualRoot,
        SpriteRenderer newSpriteRenderer,
        Transform newVfxAnchor,
        Transform newUiAnchor,
        CargoProximitySensor newProximitySensor,
        CargoColliderBuilder newColliderBuilder)
    {
        visualRoot = newVisualRoot;
        spriteRenderer = newSpriteRenderer;
        vfxAnchor = newVfxAnchor;
        uiAnchor = newUiAnchor;
        proximitySensor = newProximitySensor;
        colliderBuilder = newColliderBuilder;
        CacheReferences();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheReferences();

        if (cargoItemData == null)
        {
            error = "CargoItemData is missing.";
            return false;
        }

        if (!cargoItemData.ValidateDefinition(out error)) return false;

        if (body == null || colliderBuilder == null || polishController == null)
        {
            error = "Rigidbody, CargoColliderBuilder or CargoPolishController is missing.";
            return false;
        }

        if (!polishController.ValidateConfiguration(out error)) return false;

        if (visualRoot == null || spriteRenderer == null || vfxAnchor == null || uiAnchor == null || proximitySensor == null)
        {
            error = "VisualRoot, SpriteRenderer, VFXAnchor, UIAnchor or ProximityTrigger reference is missing.";
            return false;
        }

        if ((transform.localScale - Vector3.one).sqrMagnitude > 0.000001f)
        {
            error = "Cargo physics/network root scale must be Vector3.one.";
            return false;
        }

        if (GetComponent<Rigidbody2D>() != null || GetComponent<Collider2D>() != null || GetComponent<Joint2D>() != null)
        {
            error = "Cargo prefab still contains 2D physics components.";
            return false;
        }

        if (!BuildModuleCache(out error)) return false;

        error = null;
        return true;
    }

    public float GetCurrentValue(Type type)
    {
        CargoModuleId id = CargoModuleUtility.FromType(type);
        if (id == CargoModuleId.Unknown) return 0f;

        CargoRuntimeState state = CurrentRuntimeState;
        if (state.Has(id)) return state.Get(id);

        return moduleCache.TryGetValue(id, out CargoModule module) ? module.GetMaxValue() : 0f;
    }

    public float GetCurrentValue<T>() => GetCurrentValue(typeof(T));

    public void SetCurrentValue<T>(float value)
    {
        SetCurrentValue(CargoModuleUtility.FromType(typeof(T)), value);
    }

    public void SetCurrentValue(CargoModuleId id, float value)
    {
        if (!HasSimulationAuthority || !isInitialized || id == CargoModuleId.Unknown) return;
        if (!moduleCache.TryGetValue(id, out CargoModule module)) return;

        CargoRuntimeState state = CurrentRuntimeState;
        float clamped = module.ClampValue(value);
        if (Mathf.Abs(state.Get(id) - clamped) <= StateEpsilon) return;

        state.Set(id, clamped);
        state.Revision++;
        CommitState(state);
    }

    public void ApplyDamage(float damageAmount)
    {
        if (damageAmount <= 0f || !HasSimulationAuthority || IsInvincible) return;
        if (!moduleCache.ContainsKey(CargoModuleId.Impact)) return;
        SetCurrentValue(CargoModuleId.Impact, GetCurrentValue<ImpactModule>() - damageAmount);
    }

    public void AssignRoom(RoomMarker room)
    {
        if (!HasSimulationAuthority || currentRoomMarker == room) return;
        currentRoomMarker = room;
    }

    public void NotifyRoomExit(RoomMarker room)
    {
        if (!HasSimulationAuthority || currentRoomMarker != room) return;
        currentRoomMarker = null;
        roomQueryAccumulator = roomQueryInterval;
    }

    public void SetLocalPointerHover(bool isHovering)
    {
        if (isLocalPointerHovering == isHovering) return;
        isLocalPointerHovering = isHovering;
        RefreshLocalStatusUI();
        LocalPointerHoverChanged?.Invoke(isHovering);
    }

    public void SetLocalDebugStatusUIVisible(bool isVisible)
    {
        isLocalDebugStatusUIVisible = isVisible;
        RefreshLocalStatusUI();
    }

    public void GrantInvincibility(float durationSeconds)
    {
        if (!HasSimulationAuthority) return;
        invincibilityRemaining = Mathf.Max(invincibilityRemaining, Mathf.Max(0f, durationSeconds));
    }

    public void ClearInvincibility()
    {
        if (!HasSimulationAuthority) return;
        invincibilityRemaining = 0f;
    }

    private bool ShouldShowLocalStatusUI()
    {
        // Debug Mode gates the local status UI; hover still controls whether an
        // individual cargo panel is visible at this moment.
        return isLocalDebugStatusUIVisible && isLocalPointerHovering;
    }

    private void RefreshLocalStatusUI()
    {
        if (uiInstance == null) return;

        bool shouldShow = ShouldShowLocalStatusUI();
        uiInstance.gameObject.SetActive(shouldShow);
        if (shouldShow) uiInstance.UpdateUIValues(this);
    }

    private void CacheReferences()
    {
        if (body == null) body = GetComponent<Rigidbody>();
        if (colliderBuilder == null) colliderBuilder = GetComponent<CargoColliderBuilder>();
        if (polishController == null) polishController = GetComponent<CargoPolishController>();
        if (visualRoot == null)
        {
            Transform candidate = transform.Find("VisualRoot");
            if (candidate != null) visualRoot = candidate;
        }
        if (spriteRenderer == null && visualRoot != null) spriteRenderer = visualRoot.GetComponentInChildren<SpriteRenderer>(true);
        if (vfxAnchor == null && visualRoot != null) vfxAnchor = visualRoot.Find("VFXAnchor");
        if (uiAnchor == null) uiAnchor = transform.Find("UIAnchor");
        if (proximitySensor == null) proximitySensor = GetComponentInChildren<CargoProximitySensor>(true);

        SpriteRenderOrderPolicy.ApplyCargo(visualRoot, spriteRenderer);
    }

    private bool PrepareDefinition()
    {
        if (definitionPrepared) return true;
        if (!ValidateConfiguration(out string error))
        {
            Debug.LogError($"{name}: Cargo initialization failed: {error}", this);
            enabled = false;
            return false;
        }

        gameObject.name = $"Cargo ({cargoItemData.cargoName})";
        lockedWorldZ = transform.position.z;
        visualRoot.localScale = Vector3.one * cargoItemData.cargoScale;
        spriteRenderer.sprite = cargoItemData.defaultSprite;

        colliderBuilder.ConfigureReferences(spriteRenderer, colliderBuilder.GeneratedColliderRoot, colliderBuilder.ProximityTrigger);
        if (!colliderBuilder.Rebuild(cargoItemData))
        {
            enabled = false;
            return false;
        }

        proximitySensor.Configure(this);
        ConfigureRigidbody();
        SetupUI();
        polishController.InitializePresentation();

        definitionPrepared = true;
        return true;
    }

    private bool BuildModuleCache(out string error)
    {
        moduleCache.Clear();
        foreach (CargoModule module in cargoItemData.GetModules())
        {
            if (module == null)
            {
                error = "CargoItemData contains a null module.";
                return false;
            }

            CargoModuleId id = CargoModuleUtility.FromModule(module);
            if (id == CargoModuleId.Unknown || moduleCache.ContainsKey(id))
            {
                error = $"Unsupported or duplicate module: {module.GetType().Name}.";
                return false;
            }

            moduleCache.Add(id, module);
        }

        error = null;
        return true;
    }

    private void ConfigureRigidbody()
    {
        body.mass = Mathf.Max(0.01f, cargoItemData.mass);
        body.constraints = RigidbodyConstraints.FreezePositionZ
                         | RigidbodyConstraints.FreezeRotationX
                         | RigidbodyConstraints.FreezeRotationY;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (NetworkHelper.IsOffline)
        {
            body.isKinematic = false;
            body.useGravity = true;
        }
    }

    private void InitializeAuthoritativeState()
    {
        if (!HasSimulationAuthority) return;

        ResolveCurrentRoom();
        CargoRuntimeState state = default;
        state.Initialized = true;

        foreach (KeyValuePair<CargoModuleId, CargoModule> pair in moduleCache)
        {
            CargoModuleId id = pair.Key;
            CargoModule module = pair.Value;
            state.ModuleMask |= CargoModuleUtility.ToMask(id);

            float initialValue = module.GetMaxValue();
            if (id == CargoModuleId.Temperature && module is TemperatureModule temperature)
            {
                initialValue = currentRoomMarker != null ? currentRoomMarker.currentTemp.Value : temperature.idealTemp;
            }
            else if (id == CargoModuleId.Pressure && module is PressureModule pressure)
            {
                initialValue = currentRoomMarker != null ? currentRoomMarker.currentPressure.Value : pressure.minPressure;
            }

            state.Set(id, module.ClampValue(initialValue));
        }

        state.Revision = CurrentRuntimeState.Revision + 1;
        isInitialized = true;
        CommitState(state);
        GrantInvincibility(initialInvincibilityDuration);
    }

    private void SimulateStatuses(float deltaTime)
    {
        CargoRuntimeState state = CurrentRuntimeState;
        bool changed = false;

        if (moduleCache.TryGetValue(CargoModuleId.Freshness, out CargoModule freshnessBase)
            && freshnessBase is RottenModule freshness)
        {
            changed |= SetStateValue(
                ref state,
                CargoModuleId.Freshness,
                state.Freshness - freshness.decayRatePerSecond * deltaTime,
                freshness);
        }

        if (moduleCache.TryGetValue(CargoModuleId.Temperature, out CargoModule temperatureBase)
            && temperatureBase is TemperatureModule temperature)
        {
            float target = currentRoomMarker != null ? currentRoomMarker.currentTemp.Value : temperature.idealTemp;
            float blend = 1f - Mathf.Exp(-Mathf.Max(0f, temperature.heatTransferRate) * deltaTime);
            changed |= SetStateValue(
                ref state,
                CargoModuleId.Temperature,
                Mathf.Lerp(state.Temperature, target, blend),
                temperature);
        }

        if (moduleCache.TryGetValue(CargoModuleId.Pressure, out CargoModule pressureBase)
            && pressureBase is PressureModule pressure)
        {
            float target = currentRoomMarker != null ? currentRoomMarker.currentPressure.Value : pressure.minPressure;
            changed |= SetStateValue(
                ref state,
                CargoModuleId.Pressure,
                Mathf.MoveTowards(state.Pressure, target, Mathf.Max(0f, pressure.pressureChangeRate) * deltaTime),
                pressure);
        }

        if (!changed) return;
        state.Revision++;
        CommitState(state);
    }

    private static bool SetStateValue(ref CargoRuntimeState state, CargoModuleId id, float value, CargoModule module)
    {
        float clamped = module.ClampValue(value);
        if (Mathf.Abs(state.Get(id) - clamped) <= StateEpsilon) return false;
        state.Set(id, clamped);
        return true;
    }

    private void CommitState(CargoRuntimeState state)
    {
        if (IsSpawned)
        {
            if (!IsServer) return;
            replicatedState.Value = state;
        }
        else
        {
            CargoRuntimeState previous = offlineState;
            offlineState = state;
            OnRuntimeStateChanged(previous, state);
        }
    }

    private void OnReplicatedStateChanged(CargoRuntimeState previous, CargoRuntimeState current)
    {
        OnRuntimeStateChanged(previous, current);
    }

    private void OnRuntimeStateChanged(CargoRuntimeState previous, CargoRuntimeState current)
    {
        isInitialized = current.Initialized;
        RuntimeStateChanged?.Invoke(current);

        if (ShouldShowLocalStatusUI() && uiInstance != null) uiInstance.UpdateUIValues(this);
    }

    private void SetupUI()
    {
        if (uiCargoInfoPrefab == null || uiInstance != null || uiAnchor == null) return;

        uiInstance = Instantiate(uiCargoInfoPrefab, uiAnchor);
        uiInstance.transform.localScale = Vector3.one;
        uiInstance.SetupUI(this);
        uiInstance.ConfigureWorldPresentation(transform, uiOffset + Vector3.right * uiXOffset);
        RefreshLocalStatusUI();
    }

    private void ResolveCurrentRoom()
    {
        if (!HasSimulationAuthority || colliderBuilder == null) return;

        Bounds cargoBounds = colliderBuilder.GetWorldBounds();
        Vector3 halfExtents = cargoBounds.extents;
        if (halfExtents.sqrMagnitude < 0.000001f) halfExtents = Vector3.one * 0.05f;

        Collider[] overlaps = Physics.OverlapBox(
            cargoBounds.center,
            halfExtents,
            Quaternion.identity,
            ~0,
            QueryTriggerInteraction.Collide);

        RoomMarker best = null;
        float bestDistance = float.PositiveInfinity;
        foreach (Collider overlap in overlaps)
        {
            RoomMarker room = overlap.GetComponentInParent<RoomMarker>();
            if (room == null) room = overlap.GetComponentInChildren<RoomMarker>();
            if (room == null) continue;

            float distance = (room.transform.position - transform.position).sqrMagnitude;
            if (distance < bestDistance || (Mathf.Approximately(distance, bestDistance)
                && (best == null || room.GetInstanceID() < best.GetInstanceID())))
            {
                best = room;
                bestDistance = distance;
            }
        }

        if (best == null)
        {
            RoomMarker[] rooms = FindObjectsByType<RoomMarker>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
            foreach (RoomMarker room in rooms)
            {
                if (!room.ContainsPoint(transform.position)) continue;
                float distance = (room.transform.position - transform.position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    best = room;
                    bestDistance = distance;
                }
            }
        }

        if (best != null) currentRoomMarker = best;
    }

    private void CleanupRoomRegistration()
    {
        RoomMarker room = currentRoomMarker;
        currentRoomMarker = null;
        if (room != null) room.UnregisterCargo(this);
    }

    private void EnforcePlanarInvariant()
    {
        if (body == null) return;

        Vector3 position = body.position;
        if (Mathf.Abs(position.z - lockedWorldZ) > planarTolerance)
        {
            position.z = lockedWorldZ;
            body.position = position;
        }

        Vector3 velocity = body.linearVelocity;
        if (Mathf.Abs(velocity.z) > planarTolerance)
        {
            velocity.z = 0f;
            body.linearVelocity = velocity;
        }

        Vector3 angularVelocity = body.angularVelocity;
        if (Mathf.Abs(angularVelocity.x) > planarTolerance || Mathf.Abs(angularVelocity.y) > planarTolerance)
        {
            angularVelocity.x = 0f;
            angularVelocity.y = 0f;
            body.angularVelocity = angularVelocity;
        }
    }

    private void AdvanceInvincibility(float deltaTime)
    {
        if (!isInitialized || invincibilityRemaining <= 0f) return;
        invincibilityRemaining = Mathf.Max(0f, invincibilityRemaining - Mathf.Max(0f, deltaTime));
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!isInitialized || !HasSimulationAuthority) return;
        if (Mathf.Approximately(lastImpactFixedTime, Time.fixedTime)) return;
        lastImpactFixedTime = Time.fixedTime;

        float impactSpeed = collision.relativeVelocity.magnitude;
        float feedbackStrength = Mathf.Max(0f, impactSpeed * body.mass);
        Vector3 contactPoint = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;

        if (IsSpawned && IsServer)
        {
            PlayImpactFeedbackClientRpc(feedbackStrength, contactPoint);
        }
        else
        {
            RequestImpactPresentation(feedbackStrength, contactPoint);
        }

        if (moduleCache.TryGetValue(CargoModuleId.Impact, out CargoModule impactBase)
            && impactBase is ImpactModule impact
            && impactSpeed > impact.damageThreshold)
        {
            ApplyDamage((impactSpeed - impact.damageThreshold) * body.mass);
        }
    }

    [ClientRpc]
    private void PlayImpactFeedbackClientRpc(float strength, Vector3 contactPoint)
    {
        RequestImpactPresentation(strength, contactPoint);
    }

    private void RequestImpactPresentation(float strength, Vector3 contactPoint)
    {
        ImpactPresentationRequested?.Invoke(new CargoImpactPresentationEvent(strength, contactPoint));
    }
}
