using UnityEngine;
using Unity.Netcode;
using PressureExpress.Framework;

public class PressureMachine : MachineInstance
{
    [Header("UI")]
    public PressureMinigameUI minigameScript;

    [Header("Pressure Balance")]
    [Tooltip("Pressure reduced when the player hits the Great (green) zone.")]
    public float greatReduction = 25f;
    [Tooltip("Pressure reduced when the player hits the Good (gray) zone.")]
    public float goodReduction = 15f;
    [Tooltip("Pressure added when the player misses both zones.")]
    public float failPenalty = 5f;

    private void Awake()
    {
        machineUIType = MachineUIType.PressureGame;
    }

    protected override void OnMachineUIOpened(GameObject uiInstance)
    {
        if (uiInstance != null)
        {
            minigameScript = uiInstance.GetComponent<PressureMinigameUI>();
            if (minigameScript == null) minigameScript = uiInstance.GetComponentInChildren<PressureMinigameUI>();
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

    /// <summary>
    /// Called by the UI to report which zone the player hit.
    /// 0 = Fail, 1 = Good, 2 = Great.
    /// </summary>
    public void SubmitResult(int hitTier)
    {
        SubmitResultServerRpc(hitTier);
    }

    [Rpc(SendTo.Server)]
    private void SubmitResultServerRpc(int hitTier, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;

        if (!isUsing.Value) return;

        if (NetworkManager.Singleton.ConnectedClients[sender].PlayerObject.NetworkObjectId != currentPlayerId.Value)
            return;

        var subMgr = SubmarineManager.Instance;
        if (subMgr != null)
        {
            float pressureChange = hitTier switch
            {
                2 => -greatReduction,   // Great zone
                1 => -goodReduction,    // Good zone
                _ =>  failPenalty       // Miss
            };
            subMgr.ChangePressure(pressureChange);
        }
    }
}