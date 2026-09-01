using System;
using Unity.Netcode;

[Flags]
public enum CargoModuleMask : byte
{
    None = 0,
    Impact = 1 << 0,
    Temperature = 1 << 1,
    Pressure = 1 << 2,
    Freshness = 1 << 3
}

public enum CargoModuleId : byte
{
    Impact = 0,
    Temperature = 1,
    Pressure = 2,
    Freshness = 3,
    Unknown = byte.MaxValue
}

/// <summary>
/// Compact, server-authored runtime state for every supported cargo status.
/// The module mask tells clients which values are meaningful for the current definition.
/// </summary>
[Serializable]
public struct CargoRuntimeState : INetworkSerializable, IEquatable<CargoRuntimeState>
{
    public bool Initialized;
    public CargoModuleMask ModuleMask;
    public float Impact;
    public float Temperature;
    public float Freshness;
    public float Pressure;
    public uint Revision;

    public bool Has(CargoModuleId id)
    {
        CargoModuleMask bit = CargoModuleUtility.ToMask(id);
        return bit != CargoModuleMask.None && (ModuleMask & bit) != 0;
    }

    public float Get(CargoModuleId id)
    {
        return id switch
        {
            CargoModuleId.Impact => Impact,
            CargoModuleId.Temperature => Temperature,
            CargoModuleId.Freshness => Freshness,
            CargoModuleId.Pressure => Pressure,
            _ => 0f
        };
    }

    public void Set(CargoModuleId id, float value)
    {
        switch (id)
        {
            case CargoModuleId.Impact:
                Impact = value;
                break;
            case CargoModuleId.Temperature:
                Temperature = value;
                break;
            case CargoModuleId.Freshness:
                Freshness = value;
                break;
            case CargoModuleId.Pressure:
                Pressure = value;
                break;
        }
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Initialized);

        byte mask = (byte)ModuleMask;
        serializer.SerializeValue(ref mask);
        if (serializer.IsReader)
        {
            ModuleMask = (CargoModuleMask)mask;
        }

        serializer.SerializeValue(ref Impact);
        serializer.SerializeValue(ref Temperature);
        serializer.SerializeValue(ref Freshness);
        serializer.SerializeValue(ref Pressure);
        serializer.SerializeValue(ref Revision);
    }

    public bool Equals(CargoRuntimeState other)
    {
        return Initialized == other.Initialized
            && ModuleMask == other.ModuleMask
            && Impact.Equals(other.Impact)
            && Temperature.Equals(other.Temperature)
            && Freshness.Equals(other.Freshness)
            && Pressure.Equals(other.Pressure)
            && Revision == other.Revision;
    }

    public override bool Equals(object obj) => obj is CargoRuntimeState other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Initialized, ModuleMask, Impact, Temperature, Freshness, Pressure, Revision);
}

public static class CargoModuleUtility
{
    public static CargoModuleId FromType(Type type)
    {
        if (type == typeof(ImpactModule)) return CargoModuleId.Impact;
        if (type == typeof(TemperatureModule)) return CargoModuleId.Temperature;
        if (type == typeof(RottenModule)) return CargoModuleId.Freshness;
        if (type == typeof(PressureModule)) return CargoModuleId.Pressure;
        return CargoModuleId.Unknown;
    }

    public static CargoModuleId FromModule(CargoModule module)
    {
        return module == null ? CargoModuleId.Unknown : module.ModuleId;
    }

    public static CargoModuleMask ToMask(CargoModuleId id)
    {
        return id switch
        {
            CargoModuleId.Impact => CargoModuleMask.Impact,
            CargoModuleId.Temperature => CargoModuleMask.Temperature,
            CargoModuleId.Freshness => CargoModuleMask.Freshness,
            CargoModuleId.Pressure => CargoModuleMask.Pressure,
            _ => CargoModuleMask.None
        };
    }
}
