using Unity.Netcode;
using UnityEngine;
using PressureExpress.Framework;

public class MachineInstance : NetworkBehaviour
{
    [SerializeField] protected string machineName = "Unnamed Ship Machine";

    [Header("UI Configuration")]
    [SerializeField] protected MachineUIType machineUIType = MachineUIType.None;
    [SerializeField] protected GameObject interactionUI;
    protected GameObject activeUIInstance;

    [Header("Interaction SFX")]
    [Tooltip("Sound played when the machine UI opens (local player only).")]
    [SerializeField] private AudioClip openUISFX;

    [Tooltip("Sound played when the machine UI closes (local player only).")]
    [SerializeField] private AudioClip closeUISFX;

    [Tooltip("Volume for open/close UI sounds.")]
    [SerializeField, Range(0f, 1f)] private float uiSFXVolume = 0.8f;

    public const ulong NoPlayer = ulong.MaxValue;

    public bool canInteract;

    public NetworkVariable<bool> isUsing = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    protected NetworkVariable<ulong> currentPlayerId = new NetworkVariable<ulong>(NoPlayer,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    protected virtual void OnTriggerEnter(Collider other)
    {
        if (isUsing.Value) return;

        if (other.CompareTag("Player"))
        {
            var p = other.GetComponent<CharacterController2D>();
            if (p != null)
            {
                canInteract = true;
                p.SetInteractableObject(this);

                if (p.IsOwner && interactionUI != null)
                {
                    interactionUI.SetActive(true);
                }
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer)
        {
            isUsing.Value = false;
            currentPlayerId.Value = NoPlayer;
        }
    }

    public void ResetMachine()
    {
        canInteract = false;
        if (IsServer)
        {
            isUsing.Value = false;
            currentPlayerId.Value = NoPlayer;
        }
        if (interactionUI != null)
        {
            interactionUI.SetActive(false);
        }
        if (activeUIInstance != null)
        {
            Destroy(activeUIInstance);
            activeUIInstance = null;
        }
    }

    protected virtual void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            var p = other.GetComponent<CharacterController2D>();
            if (p != null)
            {
                canInteract = false;
                p.ClearInteractableObject(this);

                if (p.IsOwner && interactionUI != null)
                {
                    interactionUI.SetActive(false);
                }

                if (IsServer && isUsing.Value && currentPlayerId.Value == p.NetworkObjectId)
                {
                    StopInteractServerLogic();
                }
            }
        }
    }

    public void OnInteract(CharacterController2D player)
    {
        if (!canInteract || isUsing.Value) return;
        RequestInteractRpc(player.NetworkObjectId);
    }

    public void OnStopInteract(CharacterController2D player)
    {
        RequestStopInteractRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    protected void RequestInteractRpc(ulong requesterObjectId)
    {
        if (isUsing.Value) return;

        isUsing.Value = true;
        currentPlayerId.Value = requesterObjectId;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(requesterObjectId,
                out NetworkObject playerObj))
        {
            var playerComponent = playerObj.GetComponent<CharacterController2D>();
            if (playerComponent != null)
            {
                playerComponent.SetInteractingState(true);
            }

            StartMachineRpc(playerObj.OwnerClientId, RpcTarget.Single(playerObj.OwnerClientId, RpcTargetUse.Temp));
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    protected void RequestStopInteractRpc(RpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        if (!isUsing.Value || currentPlayerId.Value == NoPlayer) return;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(currentPlayerId.Value,
                out NetworkObject playerObj))
        {
            if (playerObj.OwnerClientId == senderClientId)
            {
                StopInteractServerLogic();
            }
        }
    }

    protected void StopInteractServerLogic()
    {
        if (currentPlayerId.Value != NoPlayer)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(currentPlayerId.Value,
                    out NetworkObject playerObj))
            {
                var playerComponent = playerObj.GetComponent<CharacterController2D>();
                if (playerComponent != null)
                {
                    playerComponent.SetInteractingState(false);
                }

                StopMachineRpc(playerObj.OwnerClientId, RpcTarget.Single(playerObj.OwnerClientId, RpcTargetUse.Temp));
            }
        }

        isUsing.Value = false;
        currentPlayerId.Value = NoPlayer;
    }

    private float machineUsageTime = 0f;

    [Rpc(SendTo.SpecifiedInParams)]
    protected virtual void StartMachineRpc(ulong targetClientId, RpcParams rpcParams = default)
    {
        if (NetworkManager.Singleton.LocalClientId != targetClientId) return;
        if (interactionUI != null) interactionUI.SetActive(false);
        machineUsageTime = Time.time;

        if (CanvasManager.Instance != null && machineUIType != MachineUIType.None)
        {
            activeUIInstance = CanvasManager.Instance.OpenMachineUI(machineUIType, this);
        }

        OnMachineUIOpened(activeUIInstance);

        // Play open UI sound at the machine's position.
        var spatialAudio = SpatialAudioManager.Instance;
        if (openUISFX != null && spatialAudio != null)
            spatialAudio.PlaySFXAtPosition(openUISFX, transform.position, uiSFXVolume);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    protected virtual void StopMachineRpc(ulong targetClientId, RpcParams rpcParams = default)
    {
        if (NetworkManager.Singleton.LocalClientId != targetClientId) return;
        if (canInteract && interactionUI != null) interactionUI.SetActive(true);

        OnMachineUIClosed();

        var canvas = CanvasManager.Instance;
        if (canvas != null && activeUIInstance != null)
        {
            canvas.CloseMachineUI(activeUIInstance);
            activeUIInstance = null;
        }

        var analytic = AnalyticManager.Instance;
        if (analytic != null)
        {
            analytic.UpdateMachine(machineName, Time.time - machineUsageTime);
        }

        // Play close UI sound at the machine's position.
        var spatialAudio = SpatialAudioManager.Instance;
        if (closeUISFX != null && spatialAudio != null)
            spatialAudio.PlaySFXAtPosition(closeUISFX, transform.position, uiSFXVolume);
    }

    protected virtual void OnMachineUIOpened(GameObject uiInstance) { }
    protected virtual void OnMachineUIClosed() { }

    public void OnExitUIButtonClicked()
    {
        RequestStopInteractRpc();
    }
}