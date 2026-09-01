using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PressureExpress.Framework;

public class OxygenMachineController : MonoBehaviour
{
    private static OxygenMachineController instance;
    public static OxygenMachineController Instance => instance ?? ServiceLocator.Get<OxygenMachineController>();

    [Header("Pump Visualizer Component")]
    [SerializeField] private PumpHandleVisualizer pumpVisualizer;

    [Header("Water System Settings")]
    public float currentWater = 0f;
    public float maxWater = 100f;
    public float waterFillSpeed = 80f;
    public float waterDecay = 15f;
    [SerializeField] private float ballastToOxygenRatio = 1.0f;

    [Header("UI References")]
    public Image waterBar;
    public TMP_Text oxygenText;
    public TMP_Text fuelText;

    [Header("Boost System")]
    public TMP_Text boostStateText; 
    private bool isBoosting = false; 

    [Header("Water Inflow Audio")]
    [SerializeField] private AudioClip waterInflowClip;
    [SerializeField, Range(0f, 1f)] private float inflowVolume = 0.7f;

    private AudioSource inflowAudioSource;
    private bool isInflowPlaying = false;
    private bool canGenerate = false;
    private bool isWaterFull = false;
    private bool isUsing = false;

    private OxygenMachineInstance currentMachine;

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

        inflowAudioSource = gameObject.AddComponent<AudioSource>();
        inflowAudioSource.playOnAwake = false;
        inflowAudioSource.loop = true;
        inflowAudioSource.spatialBlend = 0f;
        inflowAudioSource.volume = inflowVolume;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            ServiceLocator.Unregister<OxygenMachineController>(this);
            instance = null;
        }
    }

    private void Start()
    {
        UpdateBoostUI();
    }

    private void Update()
    {
        if (!isUsing || currentMachine == null) return;

        if (pumpVisualizer != null)
        {
            pumpVisualizer.Tick();
        }

        HandleWater();
        HandleInflowAudio();
        UpdateUI();
    }

    public void EnterMinigame(OxygenMachineInstance machine, GameObject uiInstance = null)
    {
        currentMachine = machine;
        isUsing = true;

        if (uiInstance != null)
        {
            BindUIReferences(uiInstance);
            BindUIButtons(uiInstance);
        }

        ResetWater();
    }

    public void ExitMinigame()
    {
        isUsing = false;
        currentMachine = null;
        StopInflowAudio();
        ResetWater();
    }

    public void OnExitUIButtonClicked()
    {
        if (currentMachine != null)
        {
            currentMachine.OnExitUIButtonClicked();
        }
    }

    private void BindUIReferences(GameObject uiInstance)
    {
        if (uiInstance == null) return;

        pumpVisualizer = uiInstance.GetComponentInChildren<PumpHandleVisualizer>();

        Image[] images = uiInstance.GetComponentsInChildren<Image>(true);
        foreach (Image img in images)
        {
            if (img.name.Contains("bar", System.StringComparison.OrdinalIgnoreCase) || img.name.Contains("water", System.StringComparison.OrdinalIgnoreCase))
            {
                waterBar = img;
                break;
            }
        }

        TMP_Text[] tmpTexts = uiInstance.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in tmpTexts)
        {
            if (text.name.Contains("oxygen", System.StringComparison.OrdinalIgnoreCase) || text.name.Contains("o2", System.StringComparison.OrdinalIgnoreCase))
                oxygenText = text;
            else if (text.name.Contains("fuel", System.StringComparison.OrdinalIgnoreCase))
                fuelText = text;
            else if (text.name.Contains("boost", System.StringComparison.OrdinalIgnoreCase))
                boostStateText = text;
        }
    }

    private void BindUIButtons(GameObject uiInstance)
    {
        if (uiInstance == null) return;

        Button[] buttons = uiInstance.GetComponentsInChildren<Button>(true);
        foreach (Button btn in buttons)
        {
            btn.onClick.RemoveAllListeners();
            if (btn.name.Contains("exit", System.StringComparison.OrdinalIgnoreCase) || btn.name.Contains("close", System.StringComparison.OrdinalIgnoreCase))
            {
                btn.onClick.AddListener(OnExitUIButtonClicked);
            }
            else if (btn.name.Contains("boost", System.StringComparison.OrdinalIgnoreCase))
            {
                btn.onClick.AddListener(OnClickBoost);
            }
            else if (btn.name.Contains("generate", System.StringComparison.OrdinalIgnoreCase) || btn.name.Contains("create", System.StringComparison.OrdinalIgnoreCase))
            {
                btn.onClick.AddListener(OnClickGenerate);
            }
        }
    }

    public void OnClickBoost()
    {
        isBoosting = !isBoosting; 
        UpdateBoostUI();
    }

    private void UpdateBoostUI()
    {
        if (boostStateText != null)
        {
            boostStateText.text = isBoosting ? "BOOST: ON" : "BOOST: OFF";
            boostStateText.color = isBoosting ? Color.red : Color.green;
        }
    }

    private void HandleWater()
    {
        if (isWaterFull || pumpVisualizer == null) return;

        float sliderVal = pumpVisualizer.GetSliderValue();
        if (sliderVal > 0.1f)
        {
            if (currentMachine != null && currentMachine.isFuelLoaded && SubmarineManager.Instance != null)
            {
                float availableBallast = SubmarineManager.Instance.GetBallastWaterLevel();

                if (availableBallast > 0.1f)
                {
                    float oxygenWaterNeeded = sliderVal * waterFillSpeed * Time.deltaTime;
                    float ballastToDrain = oxygenWaterNeeded * ballastToOxygenRatio;

                    if (availableBallast < ballastToDrain)
                    {
                        ballastToDrain = availableBallast;
                        oxygenWaterNeeded = ballastToDrain / ballastToOxygenRatio;
                    }

                    currentWater += oxygenWaterNeeded;
                    currentMachine.RequestDrainBallast(ballastToDrain);
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

    public void OnClickGenerate()
    {
        if (!canGenerate || currentMachine == null || !currentMachine.isFuelLoaded) return;

        currentMachine.SubmitGenerateOxygenServerRpc(isBoosting);
        ResetWater();
    }

    public void ResetWater()
    {
        currentWater = 0f;
        if (pumpVisualizer != null) pumpVisualizer.ResetSlider();
        canGenerate = false;
        isWaterFull = false;
    }

    private void UpdateUI()
    {
        if (oxygenText != null && SubmarineManager.Instance != null)
        {
            float oxy = SubmarineManager.Instance.submarineOxygen.Value;
            oxygenText.text = "OXYGEN: " + oxy.ToString("F1") + "%";
        }
        if (fuelText != null && MachineManager.Instance != null)
        {
            float fuel = MachineManager.Instance.GetCurrentFuelLevel();
            fuelText.text = "FUEL: " + Mathf.RoundToInt(fuel);
        }
        if (waterBar != null)
        {
            waterBar.fillAmount = currentWater / maxWater;
        }
    }

    private void HandleInflowAudio()
    {
        if (pumpVisualizer == null) return;

        bool shouldPlay = !isWaterFull
                          && Input.GetMouseButton(0)
                          && pumpVisualizer.GetSliderValue() > 0.1f
                          && currentMachine != null
                          && currentMachine.isFuelLoaded;

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
}