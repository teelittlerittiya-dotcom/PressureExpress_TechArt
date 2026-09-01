using UnityEngine;
using Unity.Netcode;

public class ResourceManager : NetworkBehaviour
{
    public static ResourceManager Instance;

    [Header("Shared Resources")]
    public NetworkVariable<float> pressure = new NetworkVariable<float>(0f);
    public NetworkVariable<float> temperature = new NetworkVariable<float>(0f);
    public NetworkVariable<float> oxygen = new NetworkVariable<float>(0f);
    public NetworkVariable<float> waterLevel = new NetworkVariable<float>(0f);
    public NetworkVariable<float> power = new NetworkVariable<float>(0f);

    [Header("machineData")]
    public OxygenMachineData oxygenMachineData;
    public PressureMachineData pressureMachineData;
    public TemperatureMachineData temperatureMachineData;
    public WaterLevelMachineData waterLevelMachineData;
    public PowerMachineData powerMachineData;


    [Header("Machine")]
    //pressure function
    //temperature function
    public NetworkVariable<float> maxOxygen = new NetworkVariable<float>(100f);
    //water pump


    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    [ServerRpc]
    public void UpgrageOxygenHandleServerRPC()
    {
        oxygenMachineData.UpgradeMachine();
    }
    [ServerRpc]
    void UpgradePressureHandleServerRPC()
    {
        pressureMachineData.UpgradeMachine();
    }
    [ServerRpc]
    void UpgradeTemperatureHandleServerRPC()
    {
        temperatureMachineData.UpgradeMachine();
    }
    [ServerRpc]
    void UpgradeWaterLevelHandleServerRPC()
    {
        waterLevelMachineData.UpgradeMachine();
    }
    [ServerRpc]
    void UpgradePowerHandleServerRPC()
    {
        powerMachineData.UpgradeMachine();
    }
}
