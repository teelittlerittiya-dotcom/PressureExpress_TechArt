using UnityEngine;
using Unity.Netcode;
using MoreMountains.Feedbacks;
using PressureExpress.Framework;

public class CoolantMachine : MachineInstance
{
    [Header("UI")]
    public CoolantMinigameUI minigameScript;

    [Header("FeedBack")]
    [SerializeField] private MMF_Player warningFeedBack;

    private void Awake()
    {
        machineUIType = MachineUIType.CoolantGame;
    }

    protected override void OnMachineUIOpened(GameObject uiInstance)
    {
        if (uiInstance != null)
        {
            minigameScript = uiInstance.GetComponent<CoolantMinigameUI>();
            if (minigameScript == null) minigameScript = uiInstance.GetComponentInChildren<CoolantMinigameUI>();
            if (minigameScript != null)
            {
                minigameScript.machine = this;
            }
        }
    }

    protected override void OnMachineUIClosed()
    {
        minigameScript = null;
    }

    public void ChangeTemperature(float amount)
    {
        var subMgr = SubmarineManager.Instance;
        if (subMgr != null && subMgr.GetTotalLeakCount() > 0)
        {
            if (warningFeedBack != null) warningFeedBack.PlayFeedbacks();
            return;
        }
        ChangeTemperatureRpc(amount);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ChangeTemperatureRpc(float amount, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;

        if (!isUsing.Value) return;

        if (NetworkManager.Singleton.ConnectedClients[sender].PlayerObject.NetworkObjectId != currentPlayerId.Value)
            return;

        var subMgr = SubmarineManager.Instance;
        if (subMgr != null && subMgr.GetTotalLeakCount() > 0)
        {
            return;
        }

        if (subMgr != null)
        {
            subMgr.AddTemperature(amount);
        }
    }
}