using System.Collections.Generic;
using UnityEngine;
using System.Linq; 

[CreateAssetMenu(menuName = "Cargo/Modules/Temperature")]
public class TemperatureModule : CargoModule
{
    public float minTemp = -50f;
    public float maxTemp = 50f;
    public float idealTemp = 0f; 
    public float heatTransferRate = 1f;

    [Header("Visual Config (Override Base)")]
    public List<VisualState> hotStates;  
    public List<VisualState> coldStates; 

    public override CargoModuleId ModuleId => CargoModuleId.Temperature;
    public override float GetMinValue() => minTemp;
    public override float GetMaxValue() => maxTemp; 
    public override System.Type GetModuleType() => typeof(TemperatureModule);
    public float GetTempPercent(float currentTemp)
    {
        if (currentTemp > idealTemp)
        {
            float range = maxTemp - idealTemp;
            if (range <= 0) return 0;
            return ((currentTemp - idealTemp) / range) * 100f;
        }
        else if (currentTemp < idealTemp)
        {
            float range = Mathf.Abs(minTemp - idealTemp);
            if (range <= 0) return 0;
            return (Mathf.Abs(currentTemp - idealTemp) / range) * 100f;
        }
        return 0f; 
    }

    private VisualState? GetCurrentVisualState(float currentTemp)
    {
        float percent = GetTempPercent(currentTemp);

        if (Mathf.Abs(percent) < 1f) return null;

        List<VisualState> targetStates = (currentTemp > idealTemp) ? hotStates : coldStates;
        if (targetStates == null || targetStates.Count == 0) return null;

        
        VisualState? selectedState = null;
        var sortedStates = targetStates.OrderBy(s => s.thresholdPercent);

        foreach (var state in sortedStates)
        {
            if (percent >= state.thresholdPercent)
            {
                selectedState = state;
            }
        }

        return selectedState;
    }
    public override Sprite GetSprite(float currentTemp)
    {
        var state = GetCurrentVisualState(currentTemp);
        return state.HasValue ? state.Value.sprite : null;
    }

    public override GameObject GetParticlePrefab(float currentTemp)
    {
        var state = GetCurrentVisualState(currentTemp);
        return state.HasValue ? state.Value.particlePrefab : null;
    }

    public override string GetStateName(float currentTemp)
    {
        var state = GetCurrentVisualState(currentTemp);
        return state.HasValue ? state.Value.stateName : "Normal";
    }
}
