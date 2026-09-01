using Unity.Netcode;
using UnityEngine;
using PressureExpress.Framework;

public enum PumpMode { Drain, Ballast }

public class PumpMachine : MachineInstance
{
    [Header("UI")]
    public DrainPumpMinigame minigameScript;

    public NetworkVariable<PumpMode> currentMode = new NetworkVariable<PumpMode>(PumpMode.Drain);

    [Header("Button Press Audio")]
    [SerializeField] private AudioClip buttonClickClip;
    [SerializeField, Range(0f, 1f)] private float clickVolume = 0.9f;

    private AudioSource clickAudioSource;

    private void Awake()
    {
        machineUIType = MachineUIType.WaterPump;

        clickAudioSource = gameObject.AddComponent<AudioSource>();
        clickAudioSource.playOnAwake = false;
        clickAudioSource.loop = false;
        clickAudioSource.spatialBlend = 0f;
        clickAudioSource.volume = clickVolume;
    }

    private void PlayClickLocal()
    {
        if (buttonClickClip != null && clickAudioSource != null)
        {
            clickAudioSource.PlayOneShot(buttonClickClip, clickVolume);
        }
    }

    protected override void OnMachineUIOpened(GameObject uiInstance)
    {
        if (uiInstance != null)
        {
            minigameScript = uiInstance.GetComponent<DrainPumpMinigame>();
            if (minigameScript == null) minigameScript = uiInstance.GetComponentInChildren<DrainPumpMinigame>();
            if (minigameScript != null)
            {
                minigameScript.parentMachine = this;
            }
        }
    }

    protected override void OnMachineUIClosed()
    {
        minigameScript = null;
    }

    public void ToggleMode()
    {
        ToggleModeServerRpc();
    }

    [Rpc(SendTo.Server)]
    private void ToggleModeServerRpc()
    {
        currentMode.Value = (currentMode.Value == PumpMode.Drain) ? PumpMode.Ballast : PumpMode.Drain;
    }

    public void PumpWater(float power, bool isFilling = false)
    {
        PumpWaterRpc(power, isFilling);
        PlayClickLocal();
    }

    [Rpc(SendTo.Server)]
    private void PumpWaterRpc(float power, bool isFilling, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;

        if (!isUsing.Value) return;

        if (NetworkManager.Singleton.ConnectedClients[sender].PlayerObject.NetworkObjectId != currentPlayerId.Value)
            return;

        var subMgr = SubmarineManager.Instance;
        if (subMgr == null) return;

        if (currentMode.Value == PumpMode.Drain)
        {
            subMgr.ReduceGlobalWater(power);
        }
        else
        {
            float finalAmount = isFilling ? power : -power;
            subMgr.AdjustBallast(finalAmount);
        }
    }
}