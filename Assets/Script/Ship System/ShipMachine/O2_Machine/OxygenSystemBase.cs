using Unity.Netcode;
using UnityEngine;

public abstract class OxygenSystemBase : NetworkBehaviour
{
    [Header("Oxygen Level Configuration")]
    public float maxOxygen = 100f;

    [Header("Decay Rate Settings")]
    public float baseDecayRate = 0.1f;
    public float perCrewDecay = 0.1f;
    public float engineConsumption = 0.2f;
    public int crewCount = 0;

    [Header("Production Config")]
    public float minOxygenGain = 5f;
    public float maxOxygenGain = 15f;
    public float boostMultiplier = 1.5f;
    public float lowO2Threshold = 20f;
    public float heavyLoadO2Penalty = 2f;

    protected float heavyLoadTimer = 0f;

    public abstract float OxygenLevel { get; set; }

    protected virtual void Update()
    {
        if (heavyLoadTimer > 0)
        {
            heavyLoadTimer -= Time.deltaTime;
        }
    }

    protected float CalculateDecayAmount(int activeLeaks, float oxygenLossPerLeak)
    {
        float totalDecay = baseDecayRate + (crewCount * perCrewDecay) + engineConsumption;
        if (heavyLoadTimer > 0)
        {
            totalDecay += heavyLoadO2Penalty;
        }
        if (activeLeaks > 0)
        {
            totalDecay += (activeLeaks * oxygenLossPerLeak);
        }
        return totalDecay;
    }

    protected float CalculateOxygenGain(bool isBoosting, out bool actualBoost)
    {
        actualBoost = isBoosting;
        if (OxygenLevel <= lowO2Threshold)
        {
            actualBoost = false;
        }

        float amount = Random.Range(minOxygenGain, maxOxygenGain);
        if (actualBoost)
        {
            amount *= boostMultiplier;
        }
        return amount;
    }
}
