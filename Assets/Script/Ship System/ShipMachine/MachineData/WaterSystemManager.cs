using UnityEngine;
using UnityEngine.Events;
using PressureExpress.Framework;

public class WaterSystemManager : MonoBehaviour
{
    private static WaterSystemManager instance;
    public static WaterSystemManager Instance => instance ?? ServiceLocator.Get<WaterSystemManager>();

    [Header("Water Settings")]
    public float currentWaterLevel = 0f;
    public float maxWaterLevel = 100f;
    public float leakRatePerSecond = 5f;

    public int activeLeaks = 0;

    public UnityEvent OnWaterLevelChanged;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            ServiceLocator.Register(this);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            ServiceLocator.Unregister<WaterSystemManager>(this);
            instance = null;
        }
    }

    private void Update()
    {
        if (activeLeaks > 0 && currentWaterLevel < maxWaterLevel)
        {
            currentWaterLevel += (leakRatePerSecond * activeLeaks) * Time.deltaTime;
            currentWaterLevel = Mathf.Clamp(currentWaterLevel, 0, maxWaterLevel);
            OnWaterLevelChanged?.Invoke();
        }
    }

    public void ReduceWater(float amount)
    {
        currentWaterLevel -= amount;
        currentWaterLevel = Mathf.Clamp(currentWaterLevel, 0, maxWaterLevel);
        OnWaterLevelChanged?.Invoke();
    }

    public void AddLeak() { activeLeaks++; }
    public void RemoveLeak() { activeLeaks = Mathf.Max(0, activeLeaks - 1); }
}