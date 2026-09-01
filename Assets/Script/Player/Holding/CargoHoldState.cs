using System;
using Unity.Netcode;
using UnityEngine;

public enum CargoGripState : byte
{
    Released = 0,
    Holding = 1
}

public enum CargoReleaseReason : byte
{
    None = 0,
    PlayerRequested = 1,
    HardReach = 2,
    StaleIntent = 3,
    InvalidHand = 4,
    CargoDespawned = 5,
    PlayerDespawned = 6
}

/// <summary>
/// Replicated holder truth. One record lives on each Player NetworkObject; a Cargo can therefore
/// gather multiple independent records without a local-only holder dictionary becoming truth.
/// </summary>
public struct CargoHoldState : INetworkSerializable, IEquatable<CargoHoldState>
{
    public CargoGripState GripState;
    public CargoReleaseReason ReleaseReason;
    public NetworkObjectReference Cargo;
    public NetworkObjectReference Player;
    public NetworkObjectReference Hand;
    public Vector2 LocalGrabPoint;
    public Vector2 CursorIntent;
    public uint StateVersion;
    public uint LastAcceptedInputSequence;

    public bool IsActive => GripState == CargoGripState.Holding;

    public static CargoHoldState CreateHolding(
        NetworkObject cargo,
        NetworkObject player,
        NetworkObject hand,
        Vector2 localGrabPoint,
        Vector2 cursorIntent,
        uint stateVersion)
    {
        return new CargoHoldState
        {
            GripState = CargoGripState.Holding,
            ReleaseReason = CargoReleaseReason.None,
            Cargo = new NetworkObjectReference(cargo),
            Player = new NetworkObjectReference(player),
            Hand = new NetworkObjectReference(hand),
            LocalGrabPoint = localGrabPoint,
            CursorIntent = cursorIntent,
            StateVersion = stateVersion,
            LastAcceptedInputSequence = 0
        };
    }

    public static CargoHoldState CreateReleased(uint stateVersion, CargoReleaseReason reason)
    {
        return new CargoHoldState
        {
            GripState = CargoGripState.Released,
            ReleaseReason = reason,
            Cargo = new NetworkObjectReference((NetworkObject)null),
            Player = new NetworkObjectReference((NetworkObject)null),
            Hand = new NetworkObjectReference((NetworkObject)null),
            StateVersion = stateVersion
        };
    }

    public bool IsForCargo(ulong cargoNetworkObjectId)
    {
        return IsActive && Cargo.NetworkObjectId == cargoNetworkObjectId;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref GripState);
        serializer.SerializeValue(ref ReleaseReason);
        serializer.SerializeValue(ref Cargo);
        serializer.SerializeValue(ref Player);
        serializer.SerializeValue(ref Hand);
        serializer.SerializeValue(ref LocalGrabPoint);
        serializer.SerializeValue(ref CursorIntent);
        serializer.SerializeValue(ref StateVersion);
        serializer.SerializeValue(ref LastAcceptedInputSequence);
    }

    public bool Equals(CargoHoldState other)
    {
        return GripState == other.GripState
               && ReleaseReason == other.ReleaseReason
               && Cargo.Equals(other.Cargo)
               && Player.Equals(other.Player)
               && Hand.Equals(other.Hand)
               && LocalGrabPoint.Equals(other.LocalGrabPoint)
               && CursorIntent.Equals(other.CursorIntent)
               && StateVersion == other.StateVersion
               && LastAcceptedInputSequence == other.LastAcceptedInputSequence;
    }

    public override bool Equals(object obj)
    {
        return obj is CargoHoldState other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = (int)GripState;
            hash = (hash * 397) ^ (int)ReleaseReason;
            hash = (hash * 397) ^ Cargo.GetHashCode();
            hash = (hash * 397) ^ Player.GetHashCode();
            hash = (hash * 397) ^ Hand.GetHashCode();
            hash = (hash * 397) ^ LocalGrabPoint.GetHashCode();
            hash = (hash * 397) ^ CursorIntent.GetHashCode();
            hash = (hash * 397) ^ (int)StateVersion;
            hash = (hash * 397) ^ (int)LastAcceptedInputSequence;
            return hash;
        }
    }
}
