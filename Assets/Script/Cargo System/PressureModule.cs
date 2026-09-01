using System;
using UnityEngine;

[Serializable]
[CreateAssetMenu(menuName = "Cargo/Modules/Pressure")]
public class PressureModule : CargoModule
{
    public float minPressure = 0f;
    public float maxPressure = 500f;
    public float pressureChangeRate = 10f;

    public override CargoModuleId ModuleId => CargoModuleId.Pressure;
    public override float GetMinValue() => minPressure;
    public override float GetMaxValue() => maxPressure;
    public override System.Type GetModuleType() => typeof(PressureModule);
}
