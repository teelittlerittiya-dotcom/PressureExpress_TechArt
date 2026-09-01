using Unity.Netcode;
using UnityEngine;
using PressureExpress.Framework;

public class OxygenSystemManager : OxygenSystemBase, IUpdateable
{
    private static OxygenSystemManager instance;
    public static OxygenSystemManager Instance => instance ?? ServiceLocator.Get<OxygenSystemManager>();

    [Header("Leak System (Penalty)")]
    public float oxygenLossPerLeak = 1.5f;

    public override float OxygenLevel
    {
        get => SubmarineManager.Instance != null ? SubmarineManager.Instance.submarineOxygen.Value : 0f;
        set
        {
            if (SubmarineManager.Instance != null)
            {
                SubmarineManager.Instance.submarineOxygen.Value = value;
            }
        }
    }

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

    public override void OnDestroy()
    {
        if (instance == this)
        {
            ServiceLocator.Unregister<OxygenSystemManager>(this);
            instance = null;
        }
        if (UpdateManager.Instance != null)
        {
            UpdateManager.Instance.UnregisterUpdateable(this);
        }
        base.OnDestroy();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (MachineManager.Instance != null)
        {
            MachineManager.Instance.OxygenSystem = this;
        }
        if (IsServer)
        {
            if (SubmarineManager.Instance != null)
            {
                SubmarineManager.Instance.submarineOxygen.Value = maxOxygen;
            }
            if (UpdateManager.Instance != null)
            {
                UpdateManager.Instance.RegisterUpdateable(this);
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        if (UpdateManager.Instance != null)
        {
            UpdateManager.Instance.UnregisterUpdateable(this);
        }
        base.OnNetworkDespawn();
    }

    public void OnUpdate()
    {
        if (!IsServer) return;

        HandleOxygenDecay();

        if (heavyLoadTimer > 0)
        {
            heavyLoadTimer -= Time.deltaTime;
        }
    }

    private void HandleOxygenDecay()
    {
        int activeLeaks = 0;
        if (SubmarineManager.Instance != null)
        {
            activeLeaks = SubmarineManager.Instance.GetTotalLeakCount();
        }

        float totalDecay = CalculateDecayAmount(activeLeaks, oxygenLossPerLeak);

        if (SubmarineManager.Instance != null)
        {
            SubmarineManager.Instance.submarineOxygen.Value -= totalDecay * Time.deltaTime;
            SubmarineManager.Instance.submarineOxygen.Value = Mathf.Clamp(SubmarineManager.Instance.submarineOxygen.Value, 0, maxOxygen);
        }
    }

    public void GenerateOxygen(bool isBoosting)
    {
        if (!IsServer) return;

        bool actualBoost;
        float amount = CalculateOxygenGain(isBoosting, out actualBoost);

        if (SubmarineManager.Instance != null)
        {
            SubmarineManager.Instance.submarineOxygen.Value += amount;
            SubmarineManager.Instance.submarineOxygen.Value = Mathf.Clamp(SubmarineManager.Instance.submarineOxygen.Value, 0, maxOxygen);
        }
        if (actualBoost)
        {
            heavyLoadTimer = 5f;
        }
    }
}