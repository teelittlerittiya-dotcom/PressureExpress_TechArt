using UnityEngine;
using PressureExpress.Framework;

public class PressureSystemManager : MonoBehaviour
{
    private static PressureSystemManager instance;
    public static PressureSystemManager Instance => instance ?? ServiceLocator.Get<PressureSystemManager>();

    [Header("Stress / Pressure Settings")]
    public float currentStress = 0f;
    public float maxStress = 100f;

    [Header("Depth System")]
    public float currentDepth = 0f;
    public float safeDepth = 100f;

    [Header("Stress Rate")]
    public float stressPer10m = 0.5f;
    public float stressRecoverRate = 0.5f;

    [Header("Alert & Failure")]
    public float alertThreshold = 80f;
    public bool isAlerting = false;

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

    public void OnDestroy()
    {
        if (instance == this)
        {
            ServiceLocator.Unregister<PressureSystemManager>(this);
            instance = null;
        }
    }

    private void Update()
    {
        HandleStressByDepth();

        isAlerting = (currentStress >= alertThreshold);

        if (currentStress >= maxStress)
        {
            TriggerFailureState();
        }
    }

    private void HandleStressByDepth()
    {
        if (currentDepth > safeDepth)
        {
            float diff = currentDepth - safeDepth;
            float rate = (diff / 10f) * stressPer10m;
            currentStress += rate * Time.deltaTime;
        }
        else
        {
            currentStress -= stressRecoverRate * Time.deltaTime;
        }

        currentStress = Mathf.Clamp(currentStress, 0, maxStress);
    }

    public void ChangeStress(float amount)
    {
        currentStress += amount;
        currentStress = Mathf.Clamp(currentStress, 0, maxStress);
    }

    private void TriggerFailureState()
    {
        var waterSys = WaterSystemManager.Instance;
        if (waterSys != null)
        {
            waterSys.AddLeak();
        }

        currentStress = 70f;
    }
}