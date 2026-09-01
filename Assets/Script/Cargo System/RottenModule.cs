using UnityEngine;
[CreateAssetMenu(menuName = "Cargo/Modules/Rotten (Freshness)")]
public class RottenModule : CargoModule
{
    public float maxFreshness = 100f;
    public float decayRatePerSecond = 0.5f;

    public override CargoModuleId ModuleId => CargoModuleId.Freshness;
    public override float GetMaxValue() => maxFreshness;
    public override System.Type GetModuleType() => typeof(RottenModule);
}
