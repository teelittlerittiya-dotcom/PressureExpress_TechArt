using UnityEngine;
using UnityEngine.UI;
using TMPro;

using PressureExpress.Framework;

public class DrainPumpMinigame : MinigameBaseUI, IUpdateable
{
    [HideInInspector] public PumpMachine parentMachine;


    // ─── Runtime ───────────────────────────────────────────────────────
    private int cachedNormalRoomCount = 0;
    private int cachedBallastRoomCount = 0;
    private bool isRoomCacheInitialized = false;
   

    [Header("UI Elements")]
    public Slider waterLevelSlider;
    public TextMeshProUGUI waterLevelText;
    public TextMeshProUGUI modeText; 

    [Header("Controls")]
    public KeyCode toggleModeKey = KeyCode.T;  
    public KeyCode pumpOutKey = KeyCode.Space; 
    public KeyCode pumpInKey = KeyCode.F;      
    public float pumpPowerPerMash = 10f;

    protected override void Awake()
    {
        base.Awake();
        ValidateReferences();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (UpdateManager.Instance != null)
        {
            UpdateManager.Instance.RegisterUpdateable(this);
        }
    }

    private void ValidateReferences()
    {
        if (waterLevelSlider == null)
            Debug.LogWarning($"[{nameof(DrainPumpMinigame)}] 'waterLevelSlider' is not bound on '{gameObject.name}'.", this);
        if (waterLevelText == null)
            Debug.LogWarning($"[{nameof(DrainPumpMinigame)}] 'waterLevelText' is not bound on '{gameObject.name}'.", this);
        if (modeText == null)
            Debug.LogWarning($"[{nameof(DrainPumpMinigame)}] 'modeText' is not bound on '{gameObject.name}'.", this);
    }

    // Update logic is handled by IUpdateable.OnUpdate(), not MinigameBaseUI.Update()
    protected override void OnMinigameUpdate() { }

    private void OnDisable()
    {
        if (UpdateManager.Instance != null)
        {
            UpdateManager.Instance.UnregisterUpdateable(this);
        }
    }

    private void OnDestroy()
    {
        if (UpdateManager.Instance != null)
        {
            UpdateManager.Instance.UnregisterUpdateable(this);
        }
    }

    public void OnUpdate()
    {
        if (parentMachine == null) return;

        UpdateUI();
        if (IsTutorialOpen) return;
        HandleInput();
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(toggleModeKey))
        {
            
            parentMachine.ToggleMode();
        }

        if (Input.GetKeyDown(pumpOutKey))
        {
            parentMachine.PumpWater(pumpPowerPerMash, false);
            PressureExpress.Tutorial.TutorialManager.Instance?.ReportMachineCompleted(MachineUIType.WaterPump);
        }

        if (Input.GetKeyDown(pumpInKey) && parentMachine.currentMode.Value == PumpMode.Ballast)
        {
            parentMachine.PumpWater(pumpPowerPerMash, true);
            PressureExpress.Tutorial.TutorialManager.Instance?.ReportMachineCompleted(MachineUIType.WaterPump);
        }
    }

    private void InitializeRoomCache()
    {
        if (SubmarineManager.Instance == null || SubmarineManager.Instance.allRooms == null) return;

        cachedNormalRoomCount = 0;
        cachedBallastRoomCount = 0;
        foreach (var room in SubmarineManager.Instance.allRooms)
        {
            if (room.isBallastTank) cachedBallastRoomCount++;
            else cachedNormalRoomCount++;
        }
        isRoomCacheInitialized = true;
    }

    private void UpdateUI()
    {
        if (SubmarineManager.Instance == null) return;

        if (!isRoomCacheInitialized)
        {
            InitializeRoomCache();
        }

        PumpMode currentMode = parentMachine.currentMode.Value;

        if (modeText != null)
        {
            modeText.text = $"MODE: {currentMode.ToString().ToUpper()}";
        }

        float currentWater = 0f;
        float maxWater = 100f;

        if (currentMode == PumpMode.Drain)
        {
            currentWater = SubmarineManager.Instance.GetLeakWaterLevel();
            maxWater = cachedNormalRoomCount > 0 ? cachedNormalRoomCount * 100f : 100f;
        }
        else if (currentMode == PumpMode.Ballast)
        {
            currentWater = SubmarineManager.Instance.GetBallastWaterLevel();
            maxWater = cachedBallastRoomCount > 0 ? cachedBallastRoomCount * 100f : 100f;
        }

        if (waterLevelSlider != null && maxWater > 0)
        {
            waterLevelSlider.value = currentWater / maxWater;
        }

        if (waterLevelText != null)
        {
            float percentage = (maxWater > 0) ? (currentWater / maxWater) * 100f : 0f;
            waterLevelText.text = currentMode == PumpMode.Drain
                ? $"Room Water: {percentage:F1}%"
                : $"Ballast Water: {percentage:F1}%";
        }
    }
    public void OnClickToggleMode()
    {
        if (IsTutorialOpen || parentMachine == null) return;
        parentMachine.ToggleMode();
    }

    public void OnClickPumpOut()
    {
        if (IsTutorialOpen || parentMachine == null) return;
        parentMachine.PumpWater(pumpPowerPerMash, false);
        PressureExpress.Tutorial.TutorialManager.Instance?.ReportMachineCompleted(MachineUIType.WaterPump);
    }

    public void OnClickPumpIn()
    {
        if (IsTutorialOpen || parentMachine == null) return;
        if (parentMachine.currentMode.Value == PumpMode.Ballast)
        {
            parentMachine.PumpWater(pumpPowerPerMash, true);
        }
    }
}