using System.Collections.Generic;
using UnityEngine;
using System;

// 1. ปรับแก้ VisualState ให้รองรับทั้ง Sprite และ Particle
[Serializable]
public struct VisualState
{
    public string stateName;
    [Range(0, 100)]
    public float thresholdPercent;
    
    public Sprite sprite;           // ใช้สำหรับ Impact/Pressure
    public GameObject particlePrefab; // ใช้สำหรับ State อื่นๆ (เพิ่มใหม่)
}

// 2. แม่แบบ Module หลัก
public abstract class CargoModule : ScriptableObject, PressureExpress.Framework.ICargoModule
{
    [Header("Visual Configuration")]
    public List<VisualState> visualStates;
    
    public abstract CargoModuleId ModuleId { get; }
    public virtual float GetMinValue() => 0f;
    public abstract float GetMaxValue();
    public abstract System.Type GetModuleType(); 

    public virtual float ClampValue(float value) => Mathf.Clamp(value, GetMinValue(), GetMaxValue());

    public virtual float GetNormalizedValue(float currentValue)
    {
        float min = GetMinValue();
        float max = GetMaxValue();
        return max <= min ? 0f : Mathf.InverseLerp(min, max, currentValue);
    }

    // ฟังก์ชันเดิม: หา Sprite (สำหรับ Impact/Pressure)
    public virtual Sprite GetSprite(float currentValue)
    {
        if (visualStates == null || visualStates.Count == 0) return null;
        float percent = GetNormalizedValue(currentValue) * 100f;
        foreach (var state in visualStates)
            if (percent <= state.thresholdPercent)
                return state.sprite;
        return null;
    }

    // ฟังก์ชันใหม่: หา Particle Prefab (สำหรับ Module อื่นๆ)
    public virtual GameObject GetParticlePrefab(float currentValue)
    {
        if (visualStates == null || visualStates.Count == 0) return null;
        float percent = GetNormalizedValue(currentValue) * 100f;
        foreach (var state in visualStates)
            if (percent <= state.thresholdPercent)
                return state.particlePrefab;
        return null;
    }
    public virtual string GetStateName(float currentValue)
    {
        if (GetMaxValue() <= GetMinValue()) return "Unknown";
        if (visualStates == null || visualStates.Count == 0) return "Normal";

        float percent = GetNormalizedValue(currentValue) * 100f;

        // วนลูปหา State ที่ตรงกับเงื่อนไข
        foreach (var state in visualStates)
        {
            if (percent <= state.thresholdPercent)
            {
                return state.stateName;
            }
        }
        
        // ถ้าเกิน 100% หรือไม่เข้าเงื่อนไขเลย ให้เอาตัวสุดท้าย (หรือจะ return "Critical" ก็ได้)
        if (visualStates.Count > 0) return visualStates[visualStates.Count - 1].stateName;
        
        return "Normal";
    }
}
