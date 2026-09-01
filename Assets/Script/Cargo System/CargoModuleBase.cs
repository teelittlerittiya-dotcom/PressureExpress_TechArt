using UnityEngine;

namespace PressureExpress.Framework
{
    /// <summary>
    /// Base interface for scriptable cargo modules, enabling modular status evaluation across all cargo types.
    /// </summary>
    public interface ICargoModule
    {
        CargoModuleId ModuleId { get; }
        float GetMinValue();
        float GetMaxValue();
        float ClampValue(float value);
        float GetNormalizedValue(float currentValue);
        System.Type GetModuleType();
        Sprite GetSprite(float currentValue);
        GameObject GetParticlePrefab(float currentValue);
        string GetStateName(float currentValue);
    }
}
