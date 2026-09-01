using Unity.Netcode;
using UnityEngine;
using PressureExpress.Framework;

public class OxygenMachineInstance : MachineInstance
{
    [Header("Machine Config")]
    public float fuelCostPerUse = 5f;
    [Header("Machine Indicator")]
    public GameObject fuelIndicator;
    private float ballastDrainAccumulator = 0f;

    public bool isFuelLoaded
    {
        get
        {
            var machineMgr = MachineManager.Instance;
            return machineMgr != null && machineMgr.HasFuelForMachine();
        }
    }

    private bool isLocalUser = false;

    private void Awake()
    {
        machineUIType = MachineUIType.OxygenPump;
    }

    private void Update()
    {
        if (!isLocalUser) return;
        UpdateFuelVisual();
    }

    [Header("UI Reference")]
    public OxygenMachineMinigameUI minigameScript;

    private void UpdateFuelVisual()
    {
        if (fuelIndicator != null) fuelIndicator.SetActive(isFuelLoaded);
    }

    protected override void OnMachineUIOpened(GameObject uiInstance)
    {
        isLocalUser = true;

        if (uiInstance != null)
        {
            minigameScript = uiInstance.GetComponent<OxygenMachineMinigameUI>();
            if (minigameScript == null) minigameScript = uiInstance.GetComponentInChildren<OxygenMachineMinigameUI>();
            if (minigameScript != null)
            {
                minigameScript.machine = this;
                minigameScript.UpdateUI();
            }
            else
            {
                var controller = OxygenMachineController.Instance;
                if (controller != null)
                {
                    controller.EnterMinigame(this, uiInstance);
                }
            }
        }
    }

    protected override void OnMachineUIClosed()
    {
        isLocalUser = false;
        if (minigameScript == null)
        {
            var controller = OxygenMachineController.Instance;
            if (controller != null)
            {
                controller.ExitMinigame();
            }
        }
        minigameScript = null;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SubmitGenerateOxygenServerRpc(bool isBoosting)
    {
        if (!isUsing.Value || !isFuelLoaded) return;
        var machineMgr = MachineManager.Instance;
        if (machineMgr != null)
        {
            machineMgr.ProcessOxygenGeneration(isBoosting, fuelCostPerUse);
        }
    }

    public void RequestDrainBallast(float amount)
    {
        ballastDrainAccumulator += amount;
        if (ballastDrainAccumulator >= 1f)
        {
            DrainBallastServerRpc(ballastDrainAccumulator);
            ballastDrainAccumulator = 0f;
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void DrainBallastServerRpc(float amount)
    {
        if (PressureExpress.Tutorial.TutorialManager.Instance != null || (SubmarineManager.Instance != null && SubmarineManager.Instance.isTutorialMode))
        {
            return;
        }

        var subMgr = SubmarineManager.Instance;
        if (subMgr != null)
        {
            subMgr.AdjustBallast(-amount);
        }
    }
}