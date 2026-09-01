using Unity.Netcode;
using UnityEngine;
using PressureExpress.Framework;

public class HullLeak : NetworkBehaviour, IFixedUpdateable
{
    [Header("Settings")]
    [SerializeField] private float waterInflowRate = 5f;
    private RoomMarker targetRoom;

    public void InitializeLeak(RoomMarker room)
    {
        targetRoom = room;
    }

    private void Start()
    {
        if (NetworkHelper.IsOffline)
        {
            if (UpdateManager.Instance != null)
            {
                UpdateManager.Instance.RegisterFixedUpdateable(this);
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            UpdateManager.Instance.RegisterFixedUpdateable(this);
        }
    }

    public void OnFixedUpdate()
    {
        if (!NetworkHelper.HasServerAuthority || targetRoom == null) return;
        targetRoom.AdjustWater(waterInflowRate * Time.fixedDeltaTime);
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkHelper.HasServerAuthority && targetRoom != null)
        {
            targetRoom.RemoveLeak();
        }
        if (UpdateManager.Instance != null)
        {
            UpdateManager.Instance.UnregisterFixedUpdateable(this);
        }
    }

    public override void OnDestroy()
    {
        if (NetworkHelper.IsOffline && targetRoom != null)
        {
            targetRoom.RemoveLeak();
        }
        if (UpdateManager.Instance != null)
        {
            UpdateManager.Instance.UnregisterFixedUpdateable(this);
        }
        base.OnDestroy();
    }
}