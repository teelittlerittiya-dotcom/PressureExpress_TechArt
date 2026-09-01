using Unity.Netcode;
using UnityEngine;
using PressureExpress.Framework;

public class MapNavigationMachine : MachineInstance
{
    [Header("UI Reference")]
    private ShipDriveMinigameUI minigameScript;
    private MapMoveController mapMoveController;
    private bool isLocalUser = false;

    protected virtual void Awake()
    {
        machineUIType = MachineUIType.MapNavigation;
        machineName = "Map Navigation Machine";
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        FindController();
    }

    private void FindController()
    {
        if (mapMoveController == null)
        {
            mapMoveController = Object.FindFirstObjectByType<MapMoveController>(FindObjectsInactive.Include);
        }
    }

    protected override void OnMachineUIOpened(GameObject uiInstance)
    {
        base.OnMachineUIOpened(uiInstance);
        isLocalUser = true;

        if (uiInstance != null)
        {
            minigameScript = uiInstance.GetComponent<ShipDriveMinigameUI>();
            if (minigameScript == null) minigameScript = uiInstance.GetComponentInChildren<ShipDriveMinigameUI>();
            if (minigameScript != null)
            {
                minigameScript.machine = this;
            }
        }

        FindController();
        if (mapMoveController != null)
        {
            mapMoveController.EnterDriveMode(this);
        }
    }

    protected override void OnMachineUIClosed()
    {
        base.OnMachineUIClosed();
        isLocalUser = false;
        minigameScript = null;

        if (mapMoveController != null)
        {
            mapMoveController.ExitDriveMode();
        }
    }
}