using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PressureExpress.Framework;

public class OxygenMachineMinigameUI : MinigameBaseUI, IUpdateable
{
    [HideInInspector] public OxygenMachineInstance machine;

    [Header("UI References")]
    public Slider waterSlider;
    public Transform visualHandle;
    public Image waterBar;
    public TMP_Text oxygenText;
    public TMP_Text fuelText;
    public TMP_Text boostStateText;
    public Button generateButton;
    public Button boostButton;

    [Header("Water System Settings")]
    public float currentWater = 0f;
    public float maxWater = 100f;
    public float waterFillSpeed = 80f;
    public float waterDecay = 15f;
    public float ballastToOxygenRatio = 1.0f;

    [Header("Slider Settings")]
    public float sliderReturnSpeed = 3f;

    [Header("Visual Handle Settings (Optional)")]
    public float handleUpY = 0f;
    public float handleDownY = -150f;
    public float handleSmooth = 10f;

    [Header("Boost System")]
    private bool isBoosting = false;

    [Header("Audio")]
    public AudioClip waterInflowClip;
    [Range(0f, 1f)] public float inflowVolume = 0.7f;
    private AudioSource inflowAudioSource;
    private bool isInflowPlaying = false;

    private bool isWaterFull = false;
    private bool canGenerate = false;

    protected override void Awake()
    {
        base.Awake();
        SetupAudio();
        SetupButtonListeners();
        ValidateReferences();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        ResetWater();
        UpdateBoostUI();

        if (UpdateManager.Instance != null)
        {
            UpdateManager.Instance.RegisterUpdateable(this);
        }
        UpdateUI();
    }

    private void OnDisable()
    {
        StopInflowAudio();
        if (UpdateManager.Instance != null)
        {
            UpdateManager.Instance.UnregisterUpdateable(this);
        }
    }

    private void OnDestroy()
    {
        StopInflowAudio();
        if (UpdateManager.Instance != null)
        {
            UpdateManager.Instance.UnregisterUpdateable(this);
        }
    }

    protected override void OnMinigameUpdate() { }

    public void OnUpdate()
    {
        if (machine == null) return;

        if (IsTutorialOpen)
        {
            StopInflowAudio();
            UpdateUI();
            return;
        }

        HandleSliderInput();
        HandleWater();
        HandleInflowAudio();
        UpdateVisualHandle();
        UpdateUI();
    }

    private void SetupAudio()
    {
        if (inflowAudioSource == null)
        {
            inflowAudioSource = gameObject.AddComponent<AudioSource>();
            inflowAudioSource.playOnAwake = false;
            inflowAudioSource.loop = true;
            inflowAudioSource.spatialBlend = 0f;
            inflowAudioSource.volume = inflowVolume;
        }
    }

    private void SetupButtonListeners()
    {
        if (generateButton != null)
        {
            generateButton.onClick.RemoveListener(OnClickGenerate);
            generateButton.onClick.AddListener(OnClickGenerate);
        }
        if (boostButton != null)
        {
            boostButton.onClick.RemoveListener(OnClickBoost);
            boostButton.onClick.AddListener(OnClickBoost);
        }
    }

    private void HandleSliderInput()
    {
        if (waterSlider == null) return;

        if (!Input.GetMouseButton(0))
        {
            waterSlider.value = Mathf.Lerp(waterSlider.value, 0f, Time.deltaTime * sliderReturnSpeed);
        }
    }

    private void HandleWater()
    {
        if (isWaterFull) return;

        float sliderVal = (waterSlider != null) ? waterSlider.value : 0f;

        if (sliderVal > 0.05f)
        {
            if (machine != null && machine.isFuelLoaded && SubmarineManager.Instance != null)
            {
                bool isTutorial = PressureExpress.Tutorial.TutorialManager.Instance != null || SubmarineManager.Instance.isTutorialMode;
                float availableBallast = SubmarineManager.Instance.GetBallastWaterLevel();

                if (availableBallast > 0.1f || isTutorial)
                {
                    float oxygenWaterNeeded = sliderVal * waterFillSpeed * Time.deltaTime;
                    float ballastToDrain = oxygenWaterNeeded * ballastToOxygenRatio;

                    if (!isTutorial && availableBallast < ballastToDrain)
                    {
                        ballastToDrain = availableBallast;
                        oxygenWaterNeeded = ballastToDrain / ballastToOxygenRatio;
                    }

                    currentWater += oxygenWaterNeeded;

                    if (!isTutorial)
                    {
                        machine.RequestDrainBallast(ballastToDrain);
                    }
                }
            }
        }
        else
        {
            currentWater -= waterDecay * Time.deltaTime;
        }

        currentWater = Mathf.Clamp(currentWater, 0, maxWater);

        if (currentWater >= maxWater)
        {
            isWaterFull = true;
            canGenerate = true;
            currentWater = maxWater;
            StopInflowAudio();
        }
    }

    private void UpdateVisualHandle()
    {
        if (visualHandle == null) return;
        if (waterSlider != null && visualHandle == waterSlider.handleRect) return;

        float sliderVal = (waterSlider != null) ? waterSlider.value : 0f;
        float targetY = Mathf.Lerp(handleUpY, handleDownY, sliderVal);
        Vector3 pos = visualHandle.localPosition;
        pos.y = Mathf.Lerp(pos.y, targetY, Time.deltaTime * handleSmooth);
        visualHandle.localPosition = pos;
    }

    public void OnClickGenerate()
    {
        if (IsTutorialOpen || !canGenerate || machine == null || !machine.isFuelLoaded) return;

        machine.SubmitGenerateOxygenServerRpc(isBoosting);
        PressureExpress.Tutorial.TutorialManager.Instance?.ReportMachineCompleted(MachineUIType.OxygenPump);
        ResetWater();
    }

    public void OnClickBoost()
    {
        if (IsTutorialOpen) return;
        isBoosting = !isBoosting;
        UpdateBoostUI();
    }

    public void ResetWater()
    {
        currentWater = 0f;
        canGenerate = false;
        isWaterFull = false;
        if (waterSlider != null) waterSlider.value = 0f;
        StopInflowAudio();
    }

    private void UpdateBoostUI()
    {
        if (boostStateText != null)
        {
            boostStateText.text = isBoosting ? "BOOST: ON" : "BOOST: OFF";
            boostStateText.color = isBoosting ? Color.red : Color.green;
        }
    }

    public void UpdateUI()
    {
        if (oxygenText != null && SubmarineManager.Instance != null)
        {
            float oxy = SubmarineManager.Instance.submarineOxygen.Value;
            oxygenText.text = $"OXYGEN: {oxy:F1}%";
        }

        if (fuelText != null && MachineManager.Instance != null)
        {
            float fuel = MachineManager.Instance.GetCurrentFuelLevel();
            fuelText.text = $"FUEL: {Mathf.RoundToInt(fuel)}";
        }

        if (waterBar != null)
        {
            if (waterBar.type != Image.Type.Filled)
            {
                waterBar.type = Image.Type.Filled;
                waterBar.fillMethod = Image.FillMethod.Vertical;
                waterBar.fillOrigin = 0;
            }
            waterBar.fillAmount = currentWater / maxWater;
        }

        if (generateButton != null)
        {
            generateButton.interactable = canGenerate && machine != null && machine.isFuelLoaded;
        }
    }

    private void HandleInflowAudio()
    {
        float sliderVal = (waterSlider != null) ? waterSlider.value : 0f;
        bool hasBallastWater = SubmarineManager.Instance != null && SubmarineManager.Instance.GetBallastWaterLevel() > 0.1f;

        bool shouldPlay = !isWaterFull
                          && sliderVal > 0.05f
                          && machine != null
                          && machine.isFuelLoaded
                          && hasBallastWater;

        if (shouldPlay && !isInflowPlaying)
        {
            StartInflowAudio();
        }
        else if (!shouldPlay && isInflowPlaying)
        {
            StopInflowAudio();
        }
    }

    private void StartInflowAudio()
    {
        if (waterInflowClip == null || inflowAudioSource == null) return;

        inflowAudioSource.clip = waterInflowClip;
        inflowAudioSource.volume = inflowVolume;
        inflowAudioSource.Play();
        isInflowPlaying = true;
    }

    private void StopInflowAudio()
    {
        if (inflowAudioSource != null && inflowAudioSource.isPlaying)
        {
            inflowAudioSource.Stop();
        }
        isInflowPlaying = false;
    }

    private void ValidateReferences()
    {
        if (waterSlider == null)
            Debug.LogWarning($"[{nameof(OxygenMachineMinigameUI)}] 'waterSlider' is not bound on '{gameObject.name}'.", this);
        if (waterBar == null)
            Debug.LogWarning($"[{nameof(OxygenMachineMinigameUI)}] 'waterBar' is not bound on '{gameObject.name}'.", this);
        if (oxygenText == null)
            Debug.LogWarning($"[{nameof(OxygenMachineMinigameUI)}] 'oxygenText' is not bound on '{gameObject.name}'.", this);
        if (generateButton == null)
            Debug.LogWarning($"[{nameof(OxygenMachineMinigameUI)}] 'generateButton' is not bound on '{gameObject.name}'.", this);
    }
}
