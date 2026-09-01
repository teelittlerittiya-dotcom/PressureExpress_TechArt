using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

public class FuelOxygen : NetworkBehaviour, IFuelSource
{
    public static FuelOxygen Instance;

    [Header("Fuel Settings")]
    public NetworkVariable<float> currentFuelLevel = new NetworkVariable<float>(
        100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public float maxFuelLevel = 100f;
    public float consumeRatePerSecond = 2f;

    public UnityEvent OnFuelLevelChanged;
    public float CurrentFuelLevel => currentFuelLevel.Value;
    public float MaxFuelLevel => maxFuelLevel;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        base.OnDestroy();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            currentFuelLevel.Value = maxFuelLevel;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (IsServer)
            {
                AddFuel(20f);
            }
            else
            {
                RequestAddFuelServerRpc(20f);
            }
        }
    }

    [Rpc(SendTo.Server)]
    private void RequestAddFuelServerRpc(float amount)
    {
        AddFuel(amount);
    }

    public void AddFuel(float amount)
    {
        if (!IsServer) return;

        currentFuelLevel.Value += amount;
        currentFuelLevel.Value = Mathf.Clamp(currentFuelLevel.Value, 0, maxFuelLevel);

        OnFuelLevelChanged?.Invoke();
    }

    public bool UseFuel(float amount)
    {
        if (!IsServer) return false;

        if (currentFuelLevel.Value < amount)
            return false;

        currentFuelLevel.Value -= amount;
        OnFuelLevelChanged?.Invoke();
        return true;
    }
}