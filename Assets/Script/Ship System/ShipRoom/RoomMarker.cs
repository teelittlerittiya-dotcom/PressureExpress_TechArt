using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

using PressureExpress.Framework;

public class RoomMarker : NetworkBehaviour, IFixedUpdateable
{
    [Header("Configuration")]
    [SerializeField] private RoomTypeSO roomConfig;

    private int oxygenDrainObjectCount = 0;
    private readonly Dictionary<CargoController, HashSet<int>> cargoTriggerContacts = new Dictionary<CargoController, HashSet<int>>();

    [Header("CurrentState")]
    public NetworkVariable<float> currentWater = new NetworkVariable<float>(0f);
    public NetworkVariable<float> currentTemp = new NetworkVariable<float>();
    public NetworkVariable<float> currentPressure = new NetworkVariable<float>();
    public NetworkVariable<int> currentHitPoints = new NetworkVariable<int>();
    [Header("Room Function")]
    public bool isBallastTank = false;

    public List<GameObject> currentObjectInRoom = new List<GameObject>();
    public NetworkVariable<int> activeLeaksCount = new NetworkVariable<int>(0);

    [SerializeField] private GameObject leakPrefab;
    [Header("Leak Spawn Settings")]
    [SerializeField] private float leakInwardOffset = 1.0f;
    [SerializeField] private float maxReachHeight = 1.5f;
    [SerializeField] private float minReachHeight = -1.5f;

    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private Color gizmoColor = Color.yellow;
    private List<GameObject> spawnedObjects = new List<GameObject>();

    float timeTemp;
    private Collider roomCollider;
    private void Start()
    {
        if (roomCollider == null) roomCollider = GetComponent<Collider>();
        if (roomCollider == null) roomCollider = GetComponentInChildren<Collider>();

        if (NetworkHelper.IsOffline)
        {
            SpawnRoomContents();
            if (roomConfig != null) InitializeRoom(roomConfig);
            if (UpdateManager.Instance != null)
            {
                UpdateManager.Instance.RegisterFixedUpdateable(this);
            }
        }
    }

    public override void OnNetworkSpawn() 
    {
        if (roomCollider == null) roomCollider = GetComponent<Collider>();
        if (roomCollider == null) roomCollider = GetComponentInChildren<Collider>();

        if (IsServer)
        {
            SpawnRoomContents();
            InitializeRoom(roomConfig);
            UpdateManager.Instance.RegisterFixedUpdateable(this);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (UpdateManager.Instance != null)
        {
            UpdateManager.Instance.UnregisterFixedUpdateable(this);
        }
    }

    public override void OnDestroy()
    {
        if (UpdateManager.Instance != null)
        {
            UpdateManager.Instance.UnregisterFixedUpdateable(this);
        }
        base.OnDestroy();
    }

    public void OnFixedUpdate()
    {
        if (!NetworkHelper.HasServerAuthority) return;
        SimulateEnvironment();
    }

    public void InitializeRoom(RoomTypeSO data)
    {
        currentTemp.Value = data.baseTemperature;
        currentPressure.Value = data.structureIntegrity;
        currentHitPoints.Value = data.roomHitPoints;
        if (isBallastTank)
        {
            currentWater.Value = 50f; 
        }
        else
        {
            currentWater.Value = 0f; 
        }
    }

    private void OnTriggerEnter(Collider collision)
    {
        if (!NetworkHelper.HasServerAuthority) return;

        if (collision.gameObject.GetComponent<CharacterController2D>())
        {
            currentObjectInRoom.Add(collision.gameObject);
            timeTemp = Time.time;
        }

        CargoController enteredCargo = collision.GetComponentInParent<CargoController>();
        if (enteredCargo != null)
        {
            GameObject cargo = enteredCargo.gameObject;
            if (!cargoTriggerContacts.TryGetValue(enteredCargo, out HashSet<int> contacts))
            {
                contacts = new HashSet<int>();
                cargoTriggerContacts.Add(enteredCargo, contacts);
            }

            bool added = contacts.Count == 0;
            contacts.Add(collision.GetInstanceID());
            if (added) currentObjectInRoom.Add(cargo);
            enteredCargo.AssignRoom(this);

            if (added && enteredCargo.cargoItemData != null && enteredCargo.cargoItemData.isDrainOxigen)
            {
                oxygenDrainObjectCount++;
            }
        }
    }

    private void OnTriggerExit(Collider collision)
    {
        if (!NetworkHelper.HasServerAuthority) return;

        if (collision.gameObject.GetComponent<CharacterController2D>())
        {
            currentObjectInRoom.Remove(collision.gameObject);
            var analytic = AnalyticManager.Instance;
            if (analytic != null)
            {
                analytic.UpdateRoom(roomConfig.name, analytic.GetTimeDuration(timeTemp));
            }
        }

        CargoController exitedCargo = collision.GetComponentInParent<CargoController>();
        if (exitedCargo != null)
        {
            GameObject cargo = exitedCargo.gameObject;
            bool removed = false;
            if (cargoTriggerContacts.TryGetValue(exitedCargo, out HashSet<int> contacts))
            {
                contacts.Remove(collision.GetInstanceID());
                if (contacts.Count == 0)
                {
                    cargoTriggerContacts.Remove(exitedCargo);
                    removed = currentObjectInRoom.Remove(cargo);
                }
            }
            if (removed) exitedCargo.NotifyRoomExit(this);

            if (removed && exitedCargo.cargoItemData != null && exitedCargo.cargoItemData.isDrainOxigen)
            {
                oxygenDrainObjectCount = Mathf.Max(0, oxygenDrainObjectCount - 1);
            }
        }
    }

    public void ResetRoom()
    {
        if (roomConfig != null)
        {
            InitializeRoom(roomConfig);
        }
        else
        {
            currentWater.Value = isBallastTank ? 50f : 0f;
            currentTemp.Value = 25f;
            currentPressure.Value = 100f;
            currentHitPoints.Value = 100;
        }
        activeLeaksCount.Value = 0;
        currentObjectInRoom.Clear();
        cargoTriggerContacts.Clear();
        oxygenDrainObjectCount = 0;
    }

    public void SpawnLeak(Vector3 impactPoint)
    {
        if (!NetworkHelper.HasServerAuthority || leakPrefab == null) return;

        if (roomCollider == null) roomCollider = GetComponent<Collider>();
        if (roomCollider == null) roomCollider = GetComponentInChildren<Collider>();

        Vector3 spawnPos = impactPoint;

        if (roomCollider != null)
        {
            Bounds bounds = roomCollider.bounds;
            Vector3 roomCenter = bounds.center;

            float marginX = Mathf.Min(0.8f, bounds.extents.x * 0.25f);
            float marginY = Mathf.Min(0.5f, bounds.extents.y * 0.25f);

            Vector3 innerMin = bounds.min + new Vector3(marginX, marginY, -50f);
            Vector3 innerMax = bounds.max - new Vector3(marginX, marginY, -50f);

            if (impactPoint == Vector3.zero)
            {
                spawnPos = new Vector3(roomCenter.x, roomCenter.y + minReachHeight, transform.position.z);
            }
            else
            {
                Vector3 wallContact = bounds.ClosestPoint(impactPoint);
                Vector3 inwardDir = roomCenter - wallContact;
                inwardDir.z = 0f;

                if (inwardDir.sqrMagnitude > 0.001f)
                {
                    inwardDir.Normalize();
                }
                else
                {
                    inwardDir = Vector3.up;
                }

                float effectiveOffset = Mathf.Max(leakInwardOffset, 1.2f);
                spawnPos = wallContact + (inwardDir * effectiveOffset);
            }

            float minY = roomCenter.y + minReachHeight;
            float maxY = roomCenter.y + maxReachHeight;
            if (minY > maxY) { float temp = minY; minY = maxY; maxY = temp; }
            spawnPos.y = Mathf.Clamp(spawnPos.y, minY, maxY);

            spawnPos.x = Mathf.Clamp(spawnPos.x, innerMin.x, innerMax.x);
            spawnPos.y = Mathf.Clamp(spawnPos.y, innerMin.y, innerMax.y);
        }

        spawnPos.z = transform.position.z;

        GameObject leakObj = Instantiate(leakPrefab, spawnPos, Quaternion.identity);
        NetworkObject netObj = leakObj.GetComponent<NetworkObject>();
        if (netObj != null && NetworkHelper.IsListening)
        {
            netObj.Spawn();
            NetworkObject parentNetObj = GetComponent<NetworkObject>();
            if (parentNetObj != null && parentNetObj.IsSpawned)
            {
                netObj.TrySetParent(parentNetObj);
            }
        }
        else
        {
            leakObj.transform.SetParent(transform);
        }

        HullLeak leak = leakObj.GetComponent<HullLeak>();
        if (leak != null)
        {
            leak.InitializeLeak(this);
            activeLeaksCount.Value++;
        }
    }

    public bool ContainsPoint(Vector3 point)
    {
        if (roomCollider == null) roomCollider = GetComponent<Collider>();
        if (roomCollider == null) roomCollider = GetComponentInChildren<Collider>();
        if (roomCollider == null) return false;

        Bounds b = roomCollider.bounds;
        point.z = b.center.z;
        return b.Contains(point);
    }

    public void UnregisterCargo(CargoController cargo)
    {
        if (cargo == null) return;

        bool hadContacts = cargoTriggerContacts.Remove(cargo);
        int removedObjects = currentObjectInRoom.RemoveAll(item => item == cargo.gameObject);
        if ((hadContacts || removedObjects > 0)
            && cargo.cargoItemData != null
            && cargo.cargoItemData.isDrainOxigen)
        {
            oxygenDrainObjectCount = Mathf.Max(0, oxygenDrainObjectCount - 1);
        }
    }

    public void RemoveLeak()
    {
        if (!NetworkHelper.HasServerAuthority) return;
        if (activeLeaksCount.Value > 0) activeLeaksCount.Value--;
    }
    private void SimulateEnvironment()
    {
        if (oxygenDrainObjectCount > 0)
        {
            SubmarineManager.Instance.submarineOxygen.Value -= oxygenDrainObjectCount * 0.1f;
        }
    }

    private void SpawnRoomContents()
    {
        if (roomConfig == null) return;

        foreach (var propData in roomConfig.props)
        {
            if (propData.prefab == null) continue;

            Vector3 spawnPos = transform.position + (Vector3)propData.offset;
            GameObject propInstance = Instantiate(propData.prefab, spawnPos, Quaternion.identity);

            NetworkObject netObj = propInstance.GetComponent<NetworkObject>();
            if (netObj != null && NetworkHelper.IsListening)
            {
                netObj.Spawn();
                netObj.TrySetParent(GetComponent<NetworkObject>());
            }
            else
            {
                propInstance.transform.SetParent(transform);
            }

            spawnedObjects.Add(propInstance);
        }
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, 0.5f);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up, roomConfig != null ? roomConfig.typeName : "Unassigned Room");
#endif

        if (roomConfig != null)
        {
            Gizmos.color = Color.green;
            foreach (var prop in roomConfig.props)
            {
                Vector3 propPos = transform.position + (Vector3)prop.offset;
                Gizmos.DrawWireCube(propPos, Vector3.one * 0.5f);
            }
        }
    }

    public void AdjustWater(float amount)
    {
        if (!IsServer) return;
        currentWater.Value = Mathf.Clamp(currentWater.Value + amount, 0f, 100f);
    }
}
