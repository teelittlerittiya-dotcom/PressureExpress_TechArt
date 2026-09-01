using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using PressureExpress.Framework;

public class FuelSystemManager : ShipSystemBase, IFuelSource
{
    private static FuelSystemManager instance;
    public static FuelSystemManager Instance => instance ?? ServiceLocator.Get<FuelSystemManager>();

    [Header("Fuel Events")]
    public UnityEvent OnFuelLevelChanged;

    public float CurrentFuelLevel => CurrentValue;
    public float MaxFuelLevel => MaxValue;
    public NetworkVariable<float> currentFuelLevel => currentResourceValue;
    public float maxFuelLevel => maxResourceValue;

    protected override void Awake()
    {
        if (instance == null)
        {
            instance = this;
            ServiceLocator.Register(this);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
        base.Awake();
    }

    public override void OnDestroy()
    {
        if (instance == this)
        {
            ServiceLocator.Unregister<FuelSystemManager>(this);
            instance = null;
        }
        base.OnDestroy();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (MachineManager.Instance != null)
        {
            MachineManager.Instance.FuelSystem = this;
        }
    }

    protected override void OnValueChanged(float previousValue, float newValue)
    {
        OnFuelLevelChanged?.Invoke();
    }

    public void ConsumeFuel(float amount)
    {
        Consume(amount);
    }

    public void AddFuel(float amount)
    {
        Add(amount);
    }

    public bool UseFuel(float amount)
    {
        return TryConsume(amount);
    }

    [Rpc(SendTo.Server)]
    public void TryUseFuelRpc(float amount)
    {
        ConsumeFuel(amount);
    }
}
