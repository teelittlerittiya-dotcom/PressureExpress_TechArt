public interface IFuelSource
{
    float CurrentFuelLevel { get; }
    float MaxFuelLevel { get; }
    void AddFuel(float amount);
    bool UseFuel(float amount);
}
