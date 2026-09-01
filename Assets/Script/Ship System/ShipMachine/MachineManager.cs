using Unity.Netcode;
using UnityEngine;
using PressureExpress.Framework;

public class MachineManager : NetworkBehaviour
{
    private static MachineManager instance;
    public static MachineManager Instance => instance ?? ServiceLocator.Get<MachineManager>();

    [Header("Ship Systems")]
    private FuelSystemManager fuelSystem;
    public FuelSystemManager FuelSystem { get => fuelSystem ?? ServiceLocator.Get<FuelSystemManager>(); set => fuelSystem = value; }

    private OxygenSystemManager oxygenSystem;
    public OxygenSystemManager OxygenSystem { get => oxygenSystem ?? ServiceLocator.Get<OxygenSystemManager>(); set => oxygenSystem = value; }

    [SerializeField] private MapNetworkMovement movementSystem;

    [Header("Fuel Consumption Settings")]
    [SerializeField] private float distancePerConsume = 100f;
    [SerializeField] private float fuelPerDistance = 1f;
    [SerializeField] private float movementHeatPerDistance = 0.5f;
    [SerializeField] private float traveledDistanceAccumulator = 0f;

    [Header("Oxygen Settings")]
    public float oxygenCoolingPerUse = 5f;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            ServiceLocator.Register(this);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    public override void OnDestroy()
    {
        if (instance == this)
        {
            ServiceLocator.Unregister<MachineManager>(this);
            instance = null;
        }
        base.OnDestroy();
    }

    public void ProcessMovementConsumption(float distanceMoved)
    {
        if (!IsServer || distanceMoved <= 0) return;

        var subManager = SubmarineManager.Instance;
        if (subManager != null)
        {
            subManager.AddTemperature(distanceMoved * movementHeatPerDistance);
        }

        traveledDistanceAccumulator += distanceMoved;
        if (traveledDistanceAccumulator >= distancePerConsume)
        {
            var fuel = FuelSystem;
            if (fuel != null)
            {
                fuel.ConsumeFuel(fuelPerDistance);
            }
            traveledDistanceAccumulator = 0f;
        }
    }

    public bool CanShipMove()
    {
        var fuel = FuelSystem;
        return fuel != null && fuel.currentFuelLevel.Value > 0;
    }

    public bool HasFuelForMachine()
    {
        var fuel = FuelSystem;
        return fuel != null && fuel.currentFuelLevel.Value > 0;
    }

    public float GetCurrentFuelLevel()
    {
        var fuel = FuelSystem;
        return fuel != null ? fuel.currentFuelLevel.Value : 0f;
    }

    public void ProcessOxygenGeneration(bool isBoosting, float fuelCost)
    {
        if (!IsServer) return;

        var fuel = FuelSystem;
        var o2 = OxygenSystem;
        if (fuel == null || o2 == null) return;

        if (fuel.currentFuelLevel.Value >= fuelCost)
        {
            fuel.ConsumeFuel(fuelCost);
            o2.GenerateOxygen(isBoosting);

            var subManager = SubmarineManager.Instance;
            if (subManager != null)
            {
                float coolingAmount = isBoosting ? (oxygenCoolingPerUse * 1.5f) : oxygenCoolingPerUse;
                subManager.AddTemperature(-coolingAmount);
            }
        }
    }
}