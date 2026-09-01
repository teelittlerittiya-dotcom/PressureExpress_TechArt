using UnityEngine;

// 3. Impact Module (ความทนทาน)
[CreateAssetMenu(menuName = "Cargo/Modules/Impact (Durability)")]
public class ImpactModule : CargoModule
{
    public float maxDurability = 100f;
    public float damageThreshold = 5f; //แก้

    
    public override CargoModuleId ModuleId => CargoModuleId.Impact;
    public override float GetMaxValue() => maxDurability;
    public override System.Type GetModuleType() => typeof(ImpactModule);
}
