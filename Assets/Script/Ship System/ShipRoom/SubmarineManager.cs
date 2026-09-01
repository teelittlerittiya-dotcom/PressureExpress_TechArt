using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PressureExpress.Framework;

public class SubmarineManager : NetworkBehaviour, IUpdateable, IFixedUpdateable
{
    private static SubmarineManager instance;
    public static SubmarineManager Instance => instance ?? ServiceLocator.Get<SubmarineManager>();

    private List<DoorConnection> allDoors = new List<DoorConnection>();
    public List<RoomMarker> allRooms = new List<RoomMarker>();

    [Header("Submarine State")]
    public NetworkVariable<float> submarineOxygen = new NetworkVariable<float>(100f);
    public NetworkVariable<float> submarineTemperature = new NetworkVariable<float>(25f);
    public NetworkVariable<float> submarinePressure = new NetworkVariable<float>(0f);

    [Header("Temperature Config")]
    public float seaTemperature = 5f;
    public float equalizeSpeed = 0.5f;
    public float maxTemp = 100f;
    public float minTemp = 0f;

    [Header("Pressure & Depth Config")]
    public float maxPressure = 100f;
    [SerializeField] private NetworkVariable<float> currentDepth = new NetworkVariable<float>(0f);
    public float CurrentDepth { get => currentDepth.Value; set => currentDepth.Value = value; }
    public float safeDepth = 100f;
    public float pressurePer10m = 0.5f;
    public float pressureRecoverRate = 0.5f;
    public float leakPressureIncreaseRate = 2.0f;
    public float alertThreshold = 80f;

    [Header("Ballast System (ลอย/จม)")]
    public NetworkVariable<float> ballastWater = new NetworkVariable<float>(50f);
    public float maxBallast = 100f;
    public float neutralBallast = 50f;
    public float leakWeightMultiplier = 0.5f;
    public float neutralWeight = 200f;

    [Header("Game Over Settings")]
    public float waterGameOverThreshold = 0.7f;
    public float minTempLimit = 5f;
    public float maxTempLimit = 95f;
    public float gameOverDelay = 10f;
    private float criticalTimer = 0f;
    public NetworkVariable<bool> isInCriticalState = new NetworkVariable<bool>(false);
    public NetworkVariable<FixedString128Bytes> failureReason = new NetworkVariable<FixedString128Bytes>(new FixedString128Bytes(""));
    [SerializeField] private GameObject loseUI;
    [SerializeField] private TMP_Text resonToLost;
    public bool isPressureAlerting => submarinePressure.Value >= alertThreshold;

    [Header("Simulation Settings")]
    [SerializeField] private float simulationInterval = 0.1f;
    public float SimulationInterval => simulationInterval;
    private float timer;
    private float lastPumpTime = 0f;

    [Header("Water Pump Audio")]
    [Tooltip("Looping clip played while the pump is actively moving water (in or out).")]
    [SerializeField] private AudioClip pumpStartClip;

    [Tooltip("One-shot clip played when the pump stops.")]
    [SerializeField] private AudioClip pumpStopClip;

    [Tooltip("Short click / button-press clip played instantly when the player " +
             "presses the Pump In or Pump Out button.")]
    [SerializeField] private AudioClip pumpButtonClickClip;

    [Tooltip("Volume of the button-click SFX.")]
    [SerializeField, Range(0f, 1f)] private float pumpClickVolume = 0.9f;

    [Tooltip("Volume of the pump SFX (before spatial processing).")]
    [SerializeField, Range(0f, 1f)] private float pumpVolume = 0.7f;

    [Tooltip("World-space Transform where the pump AudioSource is positioned.\n" +
             "Drag in the pump machine GameObject so the sound is spatialized correctly.")]
    [SerializeField] private Transform pumpAudioPosition;

    private NetworkVariable<bool> isPumpRunning = new NetworkVariable<bool>(false);

    // Client-side pump audio runtime references.
    private AudioSource pumpAudioSource;
    private SFXSource pumpSFXSource;
    private bool localPumpPlaying;

    // Non-spatial AudioSource for the instant button-click feedback.
    private AudioSource pumpClickAudioSource;

    [Header("Leak Alarm")]
    [Tooltip("Looping alarm clip played when any leak is active.")]
    [SerializeField] private AudioClip leakAlarmClip;

    [Tooltip("Volume of the leak alarm.")]
    [SerializeField, Range(0f, 1f)] private float alarmVolume = 0.8f;

    [Tooltip("Full-screen UI Image used for the red danger pulse overlay.\n" +
             "Create a Canvas > Image stretched to fill the screen, set its Color alpha to 0.")]
    [SerializeField] private Image dangerOverlay;

    [Tooltip("Speed of the red pulse animation (higher = faster pulse).")]
    [SerializeField] private float pulseSpeed = 3f;

    [Tooltip("Maximum alpha for the red pulse (0 = invisible, 1 = fully opaque).")]
    [SerializeField, Range(0f, 1f)] private float pulseMaxAlpha = 0.35f;

    [Header("Low Oxygen Warning")]
    [Tooltip("Oxygen percentage (0–100) below which the warning activates.")]
    [SerializeField] private float oxygenWarningThreshold = 25f;

    [Tooltip("Looping alarm clip played when oxygen drops below the threshold.")]
    [SerializeField] private AudioClip lowOxygenAlarmClip;

    [Tooltip("Volume of the low-oxygen alarm.")]
    [SerializeField, Range(0f, 1f)] private float lowOxygenAlarmVolume = 0.85f;

    [Tooltip("Pulse speed for the low-oxygen overlay (separate from leak pulse).")]
    [SerializeField] private float oxygenPulseSpeed = 4f;

    [Tooltip("Max alpha for the low-oxygen overlay pulse.")]
    [SerializeField, Range(0f, 1f)] private float oxygenPulseMaxAlpha = 0.4f;

    [Tooltip("Overlay tint colour for the low-oxygen warning.\n" +
             "Default is a blue-ish tint to distinguish from the red leak warning.")]
    [SerializeField] private Color oxygenOverlayColor = new Color(0.2f, 0.4f, 1f, 1f);

    [Header("Temperature Warning")]
    [Tooltip("Temperature below this value triggers the warning (should be above minTempLimit).")]
    [SerializeField] private float tempWarningMin = 15f;

    [Tooltip("Temperature above this value triggers the warning (should be below maxTempLimit).")]
    [SerializeField] private float tempWarningMax = 80f;

    [Tooltip("Looping alarm clip played when temperature is outside the safe range.")]
    [SerializeField] private AudioClip tempAlarmClip;

    [Tooltip("Volume of the temperature alarm.")]
    [SerializeField, Range(0f, 1f)] private float tempAlarmVolume = 0.8f;

    [Tooltip("Pulse speed for the temperature overlay.")]
    [SerializeField] private float tempPulseSpeed = 3.5f;

    [Tooltip("Max alpha for the temperature overlay pulse.")]
    [SerializeField, Range(0f, 1f)] private float tempPulseMaxAlpha = 0.35f;

    [Tooltip("Overlay tint colour for the temperature warning.\n" +
             "Default is orange to distinguish from red (leak) and blue (oxygen).")]
    [SerializeField] private Color tempOverlayColor = new Color(1f, 0.5f, 0f, 1f);

    [Header("Low Fuel Warning")]
    [Tooltip("Fuel percentage (0–1, e.g. 0.2 = 20%) below which the warning activates.")]
    [SerializeField] private float fuelWarningThreshold = 0.2f;

    [Tooltip("Looping alarm clip played when fuel drops below the threshold.")]
    [SerializeField] private AudioClip fuelAlarmClip;

    [Tooltip("Volume of the low-fuel alarm.")]
    [SerializeField, Range(0f, 1f)] private float fuelAlarmVolume = 0.8f;

    [Tooltip("Pulse speed for the low-fuel overlay.")]
    [SerializeField] private float fuelPulseSpeed = 2.5f;

    [Tooltip("Max alpha for the low-fuel overlay pulse.")]
    [SerializeField, Range(0f, 1f)] private float fuelPulseMaxAlpha = 0.35f;

    [Tooltip("Overlay tint colour for the low-fuel warning.\n" +
             "Default is yellow to distinguish from other warnings.")]
    [SerializeField] private Color fuelOverlayColor = new Color(1f, 0.9f, 0.1f, 1f);

    [Header("High Pressure Warning")]
    [Tooltip("Pressure value above which the warning activates.\n" +
             "Defaults to the existing alertThreshold field.")]
    [SerializeField] private bool usePressureAlertThreshold = true;

    [Tooltip("Custom pressure warning threshold (used only if usePressureAlertThreshold is false).")]
    [SerializeField] private float pressureWarningThreshold = 80f;

    [Tooltip("Looping alarm clip played when pressure exceeds the safe threshold.")]
    [SerializeField] private AudioClip pressureAlarmClip;

    [Tooltip("Volume of the high-pressure alarm.")]
    [SerializeField, Range(0f, 1f)] private float pressureAlarmVolume = 0.85f;

    [Tooltip("Pulse speed for the high-pressure overlay.")]
    [SerializeField] private float pressurePulseSpeed = 5f;

    [Tooltip("Max alpha for the high-pressure overlay pulse.")]
    [SerializeField, Range(0f, 1f)] private float pressurePulseMaxAlpha = 0.4f;

    [Tooltip("Overlay tint colour for the high-pressure warning.\n" +
             "Default is magenta/purple to distinguish from other warnings.")]
    [SerializeField] private Color pressureOverlayColor = new Color(0.8f, 0.2f, 1f, 1f);

    // Alarm units (replaces loose AudioSource & state tracking logic)
    private AlarmUnit leakAlarm;
    private AlarmUnit oxygenAlarm;
    private AlarmUnit tempAlarm;
    private AlarmUnit fuelAlarm;
    private AlarmUnit pressureAlarm;

    // State machine
    private StateMachine submarineStateMachine;

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
            return;
        }

        // Initialize DRY AlarmUnits
        leakAlarm = new AlarmUnit(gameObject, leakAlarmClip, alarmVolume);
        oxygenAlarm = new AlarmUnit(gameObject, lowOxygenAlarmClip, lowOxygenAlarmVolume);
        tempAlarm = new AlarmUnit(gameObject, tempAlarmClip, tempAlarmVolume);
        fuelAlarm = new AlarmUnit(gameObject, fuelAlarmClip, fuelAlarmVolume);
        pressureAlarm = new AlarmUnit(gameObject, pressureAlarmClip, pressureAlarmVolume);

        // Create a spatialized AudioSource for the water pump.
        SetupPumpAudio();

        // Create a plain 2D AudioSource for the instant button-click feedback.
        pumpClickAudioSource = gameObject.AddComponent<AudioSource>();
        pumpClickAudioSource.playOnAwake = false;
        pumpClickAudioSource.loop = false;
        pumpClickAudioSource.spatialBlend = 0f;
        pumpClickAudioSource.volume = pumpClickVolume;

        // Initialize state machine
        submarineStateMachine = StateMachineFactory.Create();
        submarineStateMachine.ChangeState(new SubmarineNormalState(this));
    }

    public override void OnDestroy()
    {
        if (instance == this)
        {
            ServiceLocator.Unregister<SubmarineManager>(this);
            instance = null;
        }

        if (UpdateManager.Instance != null)
        {
            UpdateManager.Instance.UnregisterUpdateable(this);
            UpdateManager.Instance.UnregisterFixedUpdateable(this);
        }

        base.OnDestroy();
    }

    private void Start()
    {
        if (loseUI != null) loseUI.SetActive(false);

        if (NetworkHelper.IsOffline)
        {
            allRooms.Clear();
            allRooms.AddRange(FindObjectsByType<RoomMarker>(FindObjectsSortMode.None));

            allDoors.Clear();
            allDoors.AddRange(FindObjectsByType<DoorConnection>(FindObjectsSortMode.None));

            ResetSubmarineState();

            if (UpdateManager.Instance != null)
            {
                UpdateManager.Instance.RegisterUpdateable(this);
                UpdateManager.Instance.RegisterFixedUpdateable(this);
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        allRooms.Clear();
        allRooms.AddRange(FindObjectsByType<RoomMarker>(FindObjectsSortMode.None));

        allDoors.Clear();
        allDoors.AddRange(FindObjectsByType<DoorConnection>(FindObjectsSortMode.None));

        if (IsServer)
        {
            ResetSubmarineState();
        }
        else
        {
            if (loseUI != null) loseUI.SetActive(false);
        }

        // Register centrally with the UpdateManager for performance
        UpdateManager.Instance.RegisterUpdateable(this);
        UpdateManager.Instance.RegisterFixedUpdateable(this);
    }

    public void ResetSubmarineState()
    {
        submarineOxygen.Value = 100f;
        submarineTemperature.Value = 25f;
        submarinePressure.Value = 0f;
        currentDepth.Value = 0f;
        ballastWater.Value = 50f;
        isInCriticalState.Value = false;
        failureReason.Value = new FixedString128Bytes("");
        criticalTimer = 0f;
        timer = 0f;

        if (loseUI != null) loseUI.SetActive(false);

        SetInitialBallastBalance();

        // Reset all rooms
        foreach (var room in allRooms)
        {
            if (room != null)
            {
                room.ResetRoom();
            }
        }

        CharacterController2D.canMove = true;
        if (CanvasManager.Instance != null)
        {
            CanvasManager.Instance.CloseCurrentUI();
        }

        var allPlayers = Object.FindObjectsByType<CharacterController2D>(FindObjectsSortMode.None);
        foreach (var player in allPlayers)
        {
            if (player != null)
            {
                player.SetInteractingState(false);
            }
        }

        var allMachines = Object.FindObjectsByType<MachineInstance>(FindObjectsSortMode.None);
        foreach (var machine in allMachines)
        {
            if (machine != null)
            {
                machine.ResetMachine();
            }
        }

        // Destroy any leftover leaks
        ClearAllLeaks();

        // Reset State Machine
        ChangeSubmarineState(StateMachineFactory.SubmarineStateTypes.Normal);
    }

    public void ClearAllLeaks()
    {
        var allHullLeaks = Object.FindObjectsByType<HullLeak>(FindObjectsSortMode.None);
        foreach (var leak in allHullLeaks)
        {
            if (leak == null) continue;
            NetworkObject netObj = leak.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn(true);
            }
            else
            {
                Destroy(leak.gameObject);
            }
        }

        var allWaterLeaks = Object.FindObjectsByType<WaterLeak>(FindObjectsSortMode.None);
        foreach (var leak in allWaterLeaks)
        {
            if (leak == null) continue;
            NetworkObject netObj = leak.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn(true);
            }
            else
            {
                Destroy(leak.gameObject);
            }
        }

        foreach (var room in allRooms)
        {
            if (room != null)
            {
                room.activeLeaksCount.Value = 0;
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        if (UpdateManager.Instance != null)
        {
            UpdateManager.Instance.UnregisterUpdateable(this);
            UpdateManager.Instance.UnregisterFixedUpdateable(this);
        }
    }

    // Called by the UpdateManager
    public void OnUpdate()
    {
        UpdateWarningsAndAlarms();
        submarineStateMachine.OnUpdate();
        UpdatePumpAudioState();
    }

    // Called by the UpdateManager
    public void OnFixedUpdate()
    {
        if (!IsServer) return;

        timer += Time.fixedDeltaTime;
        if (timer >= simulationInterval)
        {
            SimulateSubmarine();
            UpdateGlobalSystems();
            timer = 0f;
        }

        submarineStateMachine.OnFixedUpdate();
    }

    public void ChangeSubmarineState(StateMachineFactory.SubmarineStateTypes stateType)
    {
        switch (stateType)
        {
            case StateMachineFactory.SubmarineStateTypes.Normal:
                submarineStateMachine.ChangeState(new SubmarineNormalState(this));
                break;
            case StateMachineFactory.SubmarineStateTypes.Alert:
                submarineStateMachine.ChangeState(new SubmarineAlertState(this));
                break;
            case StateMachineFactory.SubmarineStateTypes.Critical:
                submarineStateMachine.ChangeState(new SubmarineCriticalState(this));
                break;
            case StateMachineFactory.SubmarineStateTypes.GameOver:
                submarineStateMachine.ChangeState(new SubmarineGameOverState(this));
                break;
        }
    }

    private void UpdateWarningsAndAlarms()
    {
        int leakCount = GetTotalLeakCount();
        bool hasLeaks = leakCount > 0;
        bool lowOxygen = submarineOxygen.Value < oxygenWarningThreshold;

        float temp = submarineTemperature.Value;
        bool badTemp = temp < tempWarningMin || temp > tempWarningMax;

        bool lowFuel = false;
        if (MachineManager.Instance != null && MachineManager.Instance.FuelSystem != null)
        {
            float fuelLevel = MachineManager.Instance.GetCurrentFuelLevel();
            float maxFuel = MachineManager.Instance.FuelSystem.maxFuelLevel;
            lowFuel = maxFuel > 0f && (fuelLevel / maxFuel) < fuelWarningThreshold;
        }

        float pressureThreshold = usePressureAlertThreshold ? alertThreshold : pressureWarningThreshold;
        bool highPressure = submarinePressure.Value >= pressureThreshold;

        // Toggle alarms
        if (hasLeaks) leakAlarm.Start(); else leakAlarm.Stop();
        if (lowOxygen) oxygenAlarm.Start(); else oxygenAlarm.Stop();
        if (badTemp) tempAlarm.Start(); else tempAlarm.Stop();
        if (lowFuel) fuelAlarm.Start(); else fuelAlarm.Stop();
        if (highPressure) pressureAlarm.Start(); else pressureAlarm.Stop();

        // Pulsing overlay
        UpdateDangerOverlay(hasLeaks, lowOxygen, badTemp, lowFuel, highPressure);
    }

    public bool IsAnyWarningActive()
    {
        bool hasLeaks = GetTotalLeakCount() > 0;
        bool lowOxygen = submarineOxygen.Value < oxygenWarningThreshold;

        float temp = submarineTemperature.Value;
        bool badTemp = temp < tempWarningMin || temp > tempWarningMax;

        bool lowFuel = false;
        if (MachineManager.Instance != null && MachineManager.Instance.FuelSystem != null)
        {
            float fuelLevel = MachineManager.Instance.GetCurrentFuelLevel();
            float maxFuel = MachineManager.Instance.FuelSystem.maxFuelLevel;
            lowFuel = maxFuel > 0f && (fuelLevel / maxFuel) < fuelWarningThreshold;
        }

        float pressureThreshold = usePressureAlertThreshold ? alertThreshold : pressureWarningThreshold;
        bool highPressure = submarinePressure.Value >= pressureThreshold;

        return hasLeaks || lowOxygen || badTemp || lowFuel || highPressure;
    }

    public bool IsCriticalConditionActive()
    {
        bool oxygenEmpty = submarineOxygen.Value <= 0f;
        float totalWater = GetTotalWaterLevel();
        float maxCapacity = GetMaxWaterCapacity();
        bool fuelEmpty = MachineManager.Instance != null && MachineManager.Instance.FuelSystem != null && MachineManager.Instance.FuelSystem.currentFuelLevel.Value <= 0;
        bool boatFlooded = totalWater >= (maxCapacity * waterGameOverThreshold);
        bool tempCritical = submarineTemperature.Value <= minTempLimit || submarineTemperature.Value >= maxTempLimit;

        return oxygenEmpty || fuelEmpty || boatFlooded || tempCritical;
    }

    public string GetCriticalFailureReason()
    {
        bool oxygenEmpty = submarineOxygen.Value <= 0f;
        float totalWater = GetTotalWaterLevel();
        float maxCapacity = GetMaxWaterCapacity();
        bool fuelEmpty = MachineManager.Instance != null && MachineManager.Instance.FuelSystem != null && MachineManager.Instance.FuelSystem.currentFuelLevel.Value <= 0;
        bool boatFlooded = totalWater >= (maxCapacity * waterGameOverThreshold);
        bool tempCritical = submarineTemperature.Value <= minTempLimit || submarineTemperature.Value >= maxTempLimit;

        if (oxygenEmpty) return "OXYGEN DEPLETED";
        if (fuelEmpty) return "Empty Fuel";
        if (boatFlooded) return "SUBMARINE FLOODED";
        if (tempCritical) return "EXTREME TEMPERATURE";
        return "";
    }

    public void SetCriticalTimer(float val) => criticalTimer = val;
    public float GetCriticalTimer() => criticalTimer;
    public void IncrementCriticalTimer(float amount) => criticalTimer += amount;
    public void SetFailureReason(string reason) => failureReason.Value = reason;

    [Header("Tutorial Mode Guard")]
    public bool isTutorialMode = false;
    private bool IsTutorialActive => isTutorialMode || PressureExpress.Tutorial.TutorialManager.Instance != null;

    public void TriggerGameOverServer()
    {
        if (IsTutorialActive) return;

        TriggerGameOver();
        ChangeSubmarineState(StateMachineFactory.SubmarineStateTypes.GameOver);
    }

    private void UpdateGlobalSystems()
    {
        if (IsTutorialActive)
        {
            if (submarineOxygen.Value < 40f) submarineOxygen.Value = 40f;
            if (submarineTemperature.Value < 20f || submarineTemperature.Value > 70f)
                submarineTemperature.Value = Mathf.Clamp(submarineTemperature.Value, 20f, 70f);
            if (submarinePressure.Value > 50f) submarinePressure.Value = 50f;
        }
        float waterLevel = GetTotalWaterLevel();
        int totalLeaks = GetTotalLeakCount();

        bool hasLeak = totalLeaks > 0;
        bool isTooDeep = currentDepth.Value > safeDepth;

        float targetTemp = seaTemperature;
        float tempChangeRate = equalizeSpeed * simulationInterval;

        if (hasLeak) tempChangeRate *= 5f;

        submarineTemperature.Value = Mathf.MoveTowards(submarineTemperature.Value, targetTemp, tempChangeRate);
        submarineTemperature.Value = Mathf.Clamp(submarineTemperature.Value, minTemp, maxTemp);

        if (hasLeak || isTooDeep)
        {
            float totalIncrease = 0f;

            if (isTooDeep)
            {
                float diff = currentDepth.Value - safeDepth;
                totalIncrease += (diff / 10f) * pressurePer10m;
            }
            if (hasLeak)
            {
                totalIncrease += leakPressureIncreaseRate * totalLeaks;
            }

            submarinePressure.Value += totalIncrease * simulationInterval;
        }
        else
        {
            submarinePressure.Value = Mathf.MoveTowards(submarinePressure.Value, 0f, pressureRecoverRate * simulationInterval);
        }

        submarinePressure.Value = Mathf.Clamp(submarinePressure.Value, 0f, maxPressure);

        if (submarinePressure.Value >= maxPressure)
        {
            TriggerPressureFailure();
        }
        foreach (var room in allRooms)
        {
            room.currentTemp.Value = submarineTemperature.Value;
            room.currentPressure.Value = submarinePressure.Value;
        }

        if (isPumpRunning.Value && Time.time - lastPumpTime > 0.5f)
        {
            StopPump();
        }
    }

    private void SetInitialBallastBalance()
    {
        var ballastTanks = allRooms.Where(r => r.isBallastTank).ToList();

        if (ballastTanks.Count > 0)
        {
            float startingWaterPerTank = 50f;
            neutralWeight = ballastTanks.Count * startingWaterPerTank;
        }
    }

    public void AddTemperature(float amount)
    {
        if (!IsServer) return;
        submarineTemperature.Value = Mathf.Clamp(submarineTemperature.Value + amount, minTemp, maxTemp);
    }

    public void SimulateSubmarine()
    {
        foreach (var door in allDoors)
        {
            if (door.isOpen.Value && door.roomA != null && door.roomB != null)
            {
                EqualizeRooms(door.roomA, door.roomB, door.flowSpeed * simulationInterval);
            }
        }
    }

    private void TriggerPressureFailure()
    {
        if (allRooms.Count > 0)
        {
            int randIndex = Random.Range(0, allRooms.Count);
            allRooms[randIndex].SpawnLeak(Vector2.zero);
        }
        submarinePressure.Value = 75f;
    }

    public void ChangePressure(float amount)
    {
        if (!IsServer) return;
        submarinePressure.Value = Mathf.Clamp(submarinePressure.Value + amount, 0f, maxPressure);
    }

    private void EqualizeRooms(RoomMarker r1, RoomMarker r2, float maxFlowAmount)
    {
        float w1 = r1.currentWater.Value;
        float w2 = r2.currentWater.Value;
        if (Mathf.Abs(w1 - w2) < 0.1f) return;
        float diff = w1 - w2;
        float transfer = Mathf.Sign(diff) * Mathf.Min(Mathf.Abs(diff) / 2f, maxFlowAmount);
        r1.AdjustWater(-transfer);
        r2.AdjustWater(transfer);
    }

    public float GetTotalWaterLevel()
    {
        float total = 0f;
        foreach (var room in allRooms) total += room.currentWater.Value;
        return total;
    }

    public float GetMaxWaterCapacity() { return allRooms.Count * 100f; }

    public void ChangeBallastWater(float amount)
    {
        if (!IsServer) return;
        ballastWater.Value = Mathf.Clamp(ballastWater.Value + amount, 0f, maxBallast);
    }

    public float GetBallastWaterLevel()
    {
        return allRooms.Where(r => r.isBallastTank).Sum(r => r.currentWater.Value);
    }

    public float GetLeakWaterLevel()
    {
        return allRooms.Where(r => !r.isBallastTank).Sum(r => r.currentWater.Value);
    }

    public void AdjustBallast(float amount)
    {
        if (!IsServer) return;

        var ballastTanks = allRooms.Where(r => r.isBallastTank).ToList();
        if (ballastTanks.Count == 0) return;

        float amountPerTank = amount / ballastTanks.Count;
        foreach (var tank in ballastTanks)
        {
            tank.AdjustWater(amountPerTank);
        }

        SetPumpRunning(true);
    }

    public int GetTotalLeakCount()
    {
        int totalLeaks = 0;
        foreach (var room in allRooms)
        {
            totalLeaks += room.activeLeaksCount.Value;
        }
        return totalLeaks;
    }

    public void ReduceGlobalWater(float amount)
    {
        if (!IsServer) return;
        var floodedRooms = allRooms
            .Where(r => !r.isBallastTank && r.currentWater.Value > 0)
            .OrderByDescending(r => r.currentWater.Value)
            .ToList();

        float remaining = amount; 

        foreach (var room in floodedRooms)
        {
            if (remaining <= 0) break;

            float drain = Mathf.Min(room.currentWater.Value, remaining);
            room.AdjustWater(-drain); 
            remaining -= drain;
        }

        SetPumpRunning(true);
    }

    public void RequestPumpIn(float amount)
    {
        PlayPumpClickLocal();
        RequestPumpInServerRpc(amount);
    }

    public void RequestPumpOut(float amount)
    {
        PlayPumpClickLocal();
        RequestPumpOutServerRpc(amount);
    }

    private void PlayPumpClickLocal()
    {
        if (pumpButtonClickClip != null && pumpClickAudioSource != null)
        {
            pumpClickAudioSource.PlayOneShot(pumpButtonClickClip, pumpClickVolume);
        }
    }

    [Rpc(SendTo.Server)]
    private void RequestPumpInServerRpc(float amount)
    {
        AdjustBallast(amount);
    }

    [Rpc(SendTo.Server)]
    private void RequestPumpOutServerRpc(float amount)
    {
        ReduceGlobalWater(amount);
    }

    private void TriggerGameOver()
    {
        if (IsTutorialActive) return;

        Debug.LogError("GAME OVER: " + failureReason.Value);
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            GameOverRpc(failureReason.Value.ToString());
        }
        else
        {
            if (loseUI != null) loseUI.SetActive(true);
            if (resonToLost != null) resonToLost.text = failureReason.Value.ToString();
        }
    }

    [Rpc(SendTo.Everyone)]
    private void GameOverRpc(string reason)
    {
        if (loseUI != null) loseUI.SetActive(true);
        if (resonToLost != null) resonToLost.text = reason;
    }

    public void RequestPlayAgain()
    {
        if (NetworkHelper.IsListening)
        {
            if (IsServer)
            {
                PlayAgainServer();
            }
            else
            {
                PlayAgainServerRpc();
            }
        }
        else
        {
            ResetSubmarineState();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    [Rpc(SendTo.Server)]
    private void PlayAgainServerRpc()
    {
        PlayAgainServer();
    }

    private void PlayAgainServer()
    {
        ResetSubmarineState();
        HideGameOverUIRpc();

        string currentSceneName = SceneManager.GetActiveScene().name;
        if (NetworkHelper.IsListening && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(currentSceneName, LoadSceneMode.Single);
        }
        else
        {
            SceneManager.LoadScene(currentSceneName);
        }
    }

    [Rpc(SendTo.Everyone)]
    private void HideGameOverUIRpc()
    {
        if (loseUI != null) loseUI.SetActive(false);
    }

    private void SetupPumpAudio()
    {
        Transform anchor = pumpAudioPosition != null ? pumpAudioPosition : transform;

        GameObject pumpGO;
        if (anchor == transform)
        {
            pumpGO = new GameObject("WaterPumpAudio");
            pumpGO.transform.SetParent(transform);
            pumpGO.transform.localPosition = Vector3.zero;
        }
        else
        {
            pumpGO = anchor.gameObject;
        }

        pumpAudioSource = pumpGO.GetComponent<AudioSource>();
        if (pumpAudioSource == null)
            pumpAudioSource = pumpGO.AddComponent<AudioSource>();

        pumpAudioSource.playOnAwake = false;
        pumpAudioSource.loop = true;
        pumpAudioSource.spatialBlend = 0f;
        pumpAudioSource.volume = pumpVolume;

        pumpSFXSource = pumpGO.GetComponent<SFXSource>();
        if (pumpSFXSource == null)
            pumpSFXSource = pumpGO.AddComponent<SFXSource>();

        pumpSFXSource.SetBaseVolume(pumpVolume);
    }

    private void SetPumpRunning(bool running)
    {
        if (!IsServer) return;

        if (running)
        {
            lastPumpTime = Time.time;
            if (!isPumpRunning.Value)
            {
                isPumpRunning.Value = true;
            }
        }
    }

    public void StopPump()
    {
        if (!IsServer) return;

        if (isPumpRunning.Value)
        {
            isPumpRunning.Value = false;
            PlayPumpStopSfxRpc();
        }
    }

    private void UpdatePumpAudioState()
    {
        bool shouldPlay = isPumpRunning.Value;

        if (shouldPlay && !localPumpPlaying)
        {
            StartPumpAudio();
        }
        else if (!shouldPlay && localPumpPlaying)
        {
            StopPumpAudio();
        }
    }

    private void StartPumpAudio()
    {
        localPumpPlaying = true;

        if (pumpStartClip != null && pumpAudioSource != null)
        {
            pumpAudioSource.clip = pumpStartClip;
            pumpAudioSource.Play();
        }
    }

    private void StopPumpAudio()
    {
        localPumpPlaying = false;

        if (pumpAudioSource != null && pumpAudioSource.isPlaying)
        {
            pumpAudioSource.Stop();
        }
    }

    [Rpc(SendTo.Everyone)]
    private void PlayPumpStopSfxRpc()
    {
        if (pumpStopClip == null) return;

        if (pumpSFXSource != null)
        {
            pumpSFXSource.PlayOneShot(pumpStopClip);
        }
        else if (pumpAudioSource != null)
        {
            pumpAudioSource.PlayOneShot(pumpStopClip, pumpVolume);
        }
    }

    private float CalculatePulse(float speed, float maxAlpha)
    {
        return (Mathf.Sin(Time.time * speed) * 0.5f + 0.5f) * maxAlpha;
    }

    private void UpdateDangerOverlay(bool hasLeaks, bool lowOxygen, bool badTemp, bool lowFuel, bool highPressure)
    {
        if (dangerOverlay == null) return;

        if (hasLeaks || lowOxygen || badTemp || lowFuel || highPressure)
        {
            float maxAlpha = 0f;
            Color chosenColor = Color.clear;

            if (hasLeaks)
            {
                float alpha = CalculatePulse(pulseSpeed, pulseMaxAlpha);
                if (alpha > maxAlpha)
                {
                    maxAlpha = alpha;
                    chosenColor = new Color(1f, 0f, 0f, alpha);
                }
            }

            if (lowOxygen)
            {
                float alpha = CalculatePulse(oxygenPulseSpeed, oxygenPulseMaxAlpha);
                if (alpha > maxAlpha)
                {
                    maxAlpha = alpha;
                    chosenColor = new Color(oxygenOverlayColor.r, oxygenOverlayColor.g, oxygenOverlayColor.b, alpha);
                }
            }

            if (badTemp)
            {
                float alpha = CalculatePulse(tempPulseSpeed, tempPulseMaxAlpha);
                if (alpha > maxAlpha)
                {
                    maxAlpha = alpha;
                    chosenColor = new Color(tempOverlayColor.r, tempOverlayColor.g, tempOverlayColor.b, alpha);
                }
            }

            if (lowFuel)
            {
                float alpha = CalculatePulse(fuelPulseSpeed, fuelPulseMaxAlpha);
                if (alpha > maxAlpha)
                {
                    maxAlpha = alpha;
                    chosenColor = new Color(fuelOverlayColor.r, fuelOverlayColor.g, fuelOverlayColor.b, alpha);
                }
            }

            if (highPressure)
            {
                float alpha = CalculatePulse(pressurePulseSpeed, pressurePulseMaxAlpha);
                if (alpha > maxAlpha)
                {
                    maxAlpha = alpha;
                    chosenColor = new Color(pressureOverlayColor.r, pressureOverlayColor.g, pressureOverlayColor.b, alpha);
                }
            }

            dangerOverlay.color = chosenColor;
        }
        else
        {
            Color c = dangerOverlay.color;
            c.a = Mathf.MoveTowards(c.a, 0f, Time.deltaTime * 2f);
            dangerOverlay.color = c;
        }
    }
}