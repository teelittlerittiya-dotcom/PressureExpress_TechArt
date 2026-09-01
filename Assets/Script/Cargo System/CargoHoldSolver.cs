using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CargoController))]
public sealed class CargoHoldSolver : NetworkBehaviour
{
    private struct HolderDebugSample
    {
        public Vector3 GripPoint;
        public Vector3 CursorIntent;
        public Vector3 ForceEnd;
        public bool BeyondSoftReach;
        public bool ForceClamped;
    }

    [SerializeField] private GripConfiguration configuration;

    private readonly Dictionary<ulong, HolderDebugSample> debugSamples = new Dictionary<ulong, HolderDebugSample>();
    private CargoController cargoController;
    private Rigidbody body;

    public GripConfiguration Configuration => configuration;
    public CargoController CargoController => cargoController;
    public Rigidbody Body => body;
    public int ActiveHolderCount => CountActiveHolders();

    private bool HasSimulationAuthority => IsSpawned ? IsServer : NetworkHelper.IsOffline;

    private void Awake()
    {
        CacheReferences();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        CacheReferences();

        if (!ValidateConfiguration(out string error))
        {
            Debug.LogError($"{name}: Cargo hold solver configuration is invalid: {error}", this);
            enabled = false;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer) ReleaseAllHolders(CargoReleaseReason.CargoDespawned);

        debugSamples.Clear();
        base.OnNetworkDespawn();
    }

    private void OnDisable()
    {
        if (IsServer) ReleaseAllHolders(CargoReleaseReason.CargoDespawned);
        debugSamples.Clear();
    }

    private void FixedUpdate()
    {
        if (!HasSimulationAuthority || configuration == null || body == null) return;

        debugSamples.Clear();
        IReadOnlyList<CargoGrabController> controllers = CargoGrabController.ActiveControllers;
        for (int index = 0; index < controllers.Count; index++)
        {
            CargoGrabController holder = controllers[index];
            if (holder == null || !holder.TryGetAuthoritativeHold(this, out CargoHoldState state, out PlayerHand hand))
            {
                continue;
            }

            Vector3 worldGripPoint = transform.TransformPoint(
                new Vector3(state.LocalGrabPoint.x, state.LocalGrabPoint.y, 0f));
            worldGripPoint.z = body.position.z;

            if (!holder.ServerValidateHoldTick(this, hand, worldGripPoint)) continue;

            Vector2 pointVelocity = GripForceModel.ProjectXY(body.GetPointVelocity(worldGripPoint));
            Vector2 gripPoint = GripForceModel.ProjectXY(worldGripPoint);
            GripForceResult result = GripForceModel.Calculate(
                state.CursorIntent,
                gripPoint,
                pointVelocity,
                configuration.PositionGain,
                configuration.VelocityGain,
                configuration.MaximumIntentSpeed,
                configuration.MaximumGripForce,
                configuration.HardReach);

            Vector3 planarForce = GripForceModel.ToWorld(result.Force, 0f);
            body.AddForceAtPosition(planarForce, worldGripPoint, ForceMode.Force);
            holder.ServerRecordAppliedForce(result, worldGripPoint);

            if (configuration.DrawDebugForces)
            {
                debugSamples[holder.OwnerClientId] = new HolderDebugSample
                {
                    GripPoint = worldGripPoint,
                    CursorIntent = GripForceModel.ToWorld(state.CursorIntent, worldGripPoint.z),
                    ForceEnd = worldGripPoint + planarForce * 0.02f,
                    BeyondSoftReach = Vector2.Distance(state.CursorIntent, gripPoint) > configuration.SoftReach,
                    ForceClamped = result.ForceClamped
                };
            }
        }
    }

    public void Configure(GripConfiguration newConfiguration)
    {
        configuration = newConfiguration;
    }

    public bool CanAcceptHolder(CargoGrabController candidate)
    {
        if (candidate == null || configuration == null) return false;
        if (candidate.IsHoldingCargo(NetworkObjectId)) return true;
        return CountActiveHolders() < configuration.MaximumHolders;
    }

    public Collider[] GetSolidCargoColliders()
    {
        CacheReferences();
        if (cargoController == null || cargoController.ColliderBuilder == null) return System.Array.Empty<Collider>();

        Transform root = cargoController.ColliderBuilder.GeneratedColliderRoot;
        if (root == null) return System.Array.Empty<Collider>();

        Collider[] all = root.GetComponentsInChildren<Collider>(true);
        List<Collider> solid = new List<Collider>(all.Length);
        foreach (Collider collider in all)
        {
            if (collider != null && collider.enabled && !collider.isTrigger) solid.Add(collider);
        }

        return solid.ToArray();
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
        if (cargoController == null || body == null)
        {
            error = "CargoController or Rigidbody is missing.";
            return false;
        }

        RigidbodyConstraints required = RigidbodyConstraints.FreezePositionZ
                                        | RigidbodyConstraints.FreezeRotationX
                                        | RigidbodyConstraints.FreezeRotationY;
        if ((body.constraints & required) != required)
        {
            error = "Cargo Rigidbody must freeze Position Z and Rotation X/Y.";
            return false;
        }

        error = null;
        return true;
    }

    private int CountActiveHolders()
    {
        int count = 0;
        IReadOnlyList<CargoGrabController> controllers = CargoGrabController.ActiveControllers;
        for (int index = 0; index < controllers.Count; index++)
        {
            CargoGrabController controller = controllers[index];
            if (controller != null && controller.IsHoldingCargo(NetworkObjectId)) count++;
        }

        return count;
    }

    private void CacheReferences()
    {
        if (cargoController == null) cargoController = GetComponent<CargoController>();
        if (body == null) body = GetComponent<Rigidbody>();
    }

    private void ReleaseAllHolders(CargoReleaseReason reason)
    {
        IReadOnlyList<CargoGrabController> controllers = CargoGrabController.ActiveControllers;
        for (int index = 0; index < controllers.Count; index++)
        {
            CargoGrabController controller = controllers[index];
            if (controller != null && controller.IsHoldingCargo(NetworkObjectId))
            {
                controller.ServerRelease(reason);
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (configuration == null || !configuration.DrawDebugForces) return;

        foreach (HolderDebugSample sample in debugSamples.Values)
        {
            Gizmos.color = sample.BeyondSoftReach ? new Color(1f, 0.5f, 0f) : Color.yellow;
            Gizmos.DrawLine(sample.GripPoint, sample.CursorIntent);
            Gizmos.DrawWireSphere(sample.CursorIntent, 0.04f);

            Gizmos.color = sample.ForceClamped ? Color.red : Color.cyan;
            Gizmos.DrawLine(sample.GripPoint, sample.ForceEnd);
            Gizmos.DrawWireSphere(sample.GripPoint, 0.035f);
        }
    }
}
