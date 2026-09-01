using UnityEngine;
using Unity.Netcode;
using UnityEngine.Events;

public class DoorConnection : NetworkBehaviour
{
    [Header("Connections")]
    public RoomMarker roomA, roomB;

    [Header("State")]
    public NetworkVariable<bool> isOpen = new NetworkVariable<bool>(false);

    [Header("Flow Rate")]
    public float flowSpeed = 50f;

    [SerializeField] private UnityEvent OnDoorInteract;

    public override void OnNetworkSpawn()
    {
        isOpen.OnValueChanged += OnDoorStateChanged;
    }

    public override void OnNetworkDespawn()
    {
        isOpen.OnValueChanged -= OnDoorStateChanged;
    }

    private void OnDoorStateChanged(bool previousValue, bool newValue)
    {
        OnDoorInteract?.Invoke();
    }

    public RoomMarker GetOtherRoom(RoomMarker current)
    {
        if (current == roomA) return roomB;
        if (current == roomB) return roomA;
        return null;
    }

    [ContextMenu("Open Da Door")]
    public void ToggleDoor()
    {
        if (IsServer)
        {
            isOpen.Value = !isOpen.Value;
        }
        else
        {
            ToggleDoorServerRpc();
        }
    }

    [Rpc(SendTo.Server)]
    private void ToggleDoorServerRpc()
    {
        isOpen.Value = !isOpen.Value;
    }
}