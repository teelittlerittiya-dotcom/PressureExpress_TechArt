using UnityEngine;
using TMPro;

using PressureExpress.Framework;

public class CoolantMinigameUI : MinigameBaseUI, IUpdateable
{
    [HideInInspector] public CoolantMachine machine;

    [Header("Minigame Settings")]
    public float maxTempChangeRate = 20f;
    public KeyCode coolKey = KeyCode.LeftArrow;
    public KeyCode heatKey = KeyCode.RightArrow;

    [Header("Inertia Settings")]
    public float acceleration = 3f;
    public float deceleration = 1.5f;
    private float currentTempVelocity = 0f;

    [Header("Valve Audio")]
    [SerializeField] private AudioClip valveLoopClip;
    [SerializeField, Range(0f, 1f)] private float valveLoopVolume = 0.7f;

    private AudioSource valveLoopAudioSource;
    private bool isValveLoopPlaying = false;

    [Header("UI Elements")]
    public RectTransform valveWheel;
    public RectTransform gaugeNeedle;
    public TextMeshProUGUI tempText;

    [Header("Visual Settings")]
    public float valveMaxAngle = 90f;
    public float valveTurnSpeed = 10f;
    public float gaugeMinAngle = 60f;
    public float gaugeMaxAngle = -60f;

    private float targetValveAngle = 0f;

    protected override void Awake()
    {
        base.Awake();
        ValidateReferences();
        valveLoopAudioSource = gameObject.AddComponent<AudioSource>();
        valveLoopAudioSource.playOnAwake = false;
        valveLoopAudioSource.loop = true;
        valveLoopAudioSource.spatialBlend = 0f;
        valveLoopAudioSource.volume = valveLoopVolume;
    }

    private void ValidateReferences()
    {
        if (valveWheel == null)
            Debug.LogWarning($"[{nameof(CoolantMinigameUI)}] 'valveWheel' is not bound on '{gameObject.name}'.", this);
        if (gaugeNeedle == null)
            Debug.LogWarning($"[{nameof(CoolantMinigameUI)}] 'gaugeNeedle' is not bound on '{gameObject.name}'.", this);
        if (tempText == null)
            Debug.LogWarning($"[{nameof(CoolantMinigameUI)}] 'tempText' is not bound on '{gameObject.name}'.", this);
    }

    protected override void OnMinigameUpdate() { }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (UpdateManager.Instance != null)
        {
            UpdateManager.Instance.RegisterUpdateable(this);
        }
    }

    private void OnDisable()
    {
        StopValveLoop();
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
        if (IsTutorialOpen)
        {
            StopValveLoop();
            UpdateVisuals();
            return;
        }

        HandleValveInput();
        UpdateVisuals();
    }

    private void HandleValveInput()
    {
        bool keyHeld = false;

        if (Input.GetKey(heatKey))
        {
            currentTempVelocity = Mathf.Lerp(currentTempVelocity, maxTempChangeRate, Time.deltaTime * acceleration);
            targetValveAngle = -valveMaxAngle;
            keyHeld = true;
        }
        else if (Input.GetKey(coolKey))
        {
            currentTempVelocity = Mathf.Lerp(currentTempVelocity, -maxTempChangeRate, Time.deltaTime * acceleration);
            targetValveAngle = valveMaxAngle;
            keyHeld = true;
        }
        else
        {
            currentTempVelocity = Mathf.Lerp(currentTempVelocity, 0f, Time.deltaTime * deceleration);
            targetValveAngle = 0f;
        }

        // Start / stop the valve loop based on key state.
        if (keyHeld && !isValveLoopPlaying)
            StartValveLoop();
        else if (!keyHeld && isValveLoopPlaying)
            StopValveLoop();

        if (Mathf.Abs(currentTempVelocity) > 0.01f && machine != null)
        {
            machine.ChangeTemperature(currentTempVelocity * Time.deltaTime);
            PressureExpress.Tutorial.TutorialManager.Instance?.ReportMachineCompleted(MachineUIType.CoolantGame);
        }
    }

    private void UpdateVisuals()
    {
        if (valveWheel != null)
        {
            float currentAngle = valveWheel.localEulerAngles.z;
            if (currentAngle > 180) currentAngle -= 360;
            float newAngle = Mathf.Lerp(currentAngle, targetValveAngle, Time.deltaTime * valveTurnSpeed);
            valveWheel.localRotation = Quaternion.Euler(0, 0, newAngle);
        }

        if (SubmarineManager.Instance == null) return;

        float currentTemp = SubmarineManager.Instance.submarineTemperature.Value;

        if (gaugeNeedle != null)
        {
            float tempPercent = currentTemp / SubmarineManager.Instance.maxTemp;
            float targetNeedleAngle = Mathf.Lerp(gaugeMinAngle, gaugeMaxAngle, tempPercent);
            gaugeNeedle.localRotation = Quaternion.Euler(0, 0, targetNeedleAngle);
        }

        if (tempText != null) tempText.text = $"TEMP: {Mathf.RoundToInt(currentTemp)}°C";
    }

    // ═══════════════════════════════════════════════════════════════════
    // Valve Loop Audio
    // ═══════════════════════════════════════════════════════════════════

    private void StartValveLoop()
    {
        if (valveLoopClip == null || valveLoopAudioSource == null) return;

        valveLoopAudioSource.clip = valveLoopClip;
        valveLoopAudioSource.volume = valveLoopVolume;
        valveLoopAudioSource.Play();
        isValveLoopPlaying = true;
    }

    private void StopValveLoop()
    {
        if (valveLoopAudioSource != null && valveLoopAudioSource.isPlaying)
        {
            valveLoopAudioSource.Stop();
        }
        isValveLoopPlaying = false;
    }
}