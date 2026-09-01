using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using PressureExpress.Framework;

public class FuelConverterMachine : MachineInstance, IUpdateable
{
    [Header("Button Press Audio")]
    [SerializeField] private AudioClip buttonClickClip;
    [SerializeField, Range(0f, 1f)] private float clickVolume = 0.9f;

    [Header("Fuel Conversion Audio")]
    [SerializeField] private AudioClip fuelConvertLoopClip;
    [SerializeField, Range(0f, 1f)] private float convertLoopVolume = 0.7f;
    [SerializeField] private AudioClip conversionCompleteClip;
    [SerializeField, Range(0f, 1f)] private float completeVolume = 0.9f;

    private AudioSource convertLoopAudioSource;
    private bool isConvertLoopPlaying = false;
    private AudioSource clickAudioSource;

    [Header("Machine Stats")]
    public NetworkVariable<float> pendingFuelValue = new NetworkVariable<float>(300f);
    public float conversionTimeRequired = 3f;

    [Header("UI Reference")]
    public FuelConverterMinigameUI minigameScript;

    [Header("UI Fallback")]
    public Slider convertProgressBar;
    [SerializeField] private TMP_Text statusText, modeText, pendingFuel;

    [Header("Mode")]
    private NetworkVariable<bool> isSafeMode = new NetworkVariable<bool>(true);
    [SerializeField] private float safeModeMultiple = 1.5f;
    private float currentMultiple;
    public Image modeImage;
    public Sprite normalSprite;
    public Sprite safeSprite;

    public bool IsSafeMode => isSafeMode.Value;
    public float SafeModeMultiple => safeModeMultiple;

    private float holdTimer = 0f;
    private bool conversionInputBlocked = false;

    private void Awake()
    {
        machineUIType = MachineUIType.FuelConverter;

        clickAudioSource = gameObject.AddComponent<AudioSource>();
        clickAudioSource.playOnAwake = false;
        clickAudioSource.loop = false;
        clickAudioSource.spatialBlend = 0f;
        clickAudioSource.volume = clickVolume;

        convertLoopAudioSource = gameObject.AddComponent<AudioSource>();
        convertLoopAudioSource.playOnAwake = false;
        convertLoopAudioSource.loop = true;
        convertLoopAudioSource.spatialBlend = 0f;
        convertLoopAudioSource.volume = convertLoopVolume;
    }

    public void PlayClickLocal()
    {
        if (buttonClickClip != null && clickAudioSource != null)
        {
            clickAudioSource.PlayOneShot(buttonClickClip, clickVolume);
        }
    }

    public override void OnNetworkSpawn()
    {
        ResetHoldTimer();
        isSafeMode.OnValueChanged += OnvaluChangeSafe;
        pendingFuelValue.OnValueChanged += OnPendingFuelChanged;

        currentMultiple = isSafeMode.Value ? 1 : 0;
    }

    public override void OnNetworkDespawn()
    {
        isSafeMode.OnValueChanged -= OnvaluChangeSafe;
        pendingFuelValue.OnValueChanged -= OnPendingFuelChanged;
    }

    private void OnvaluChangeSafe(bool previousValue, bool newValue)
    {
        if (previousValue != newValue)
        {
            currentMultiple = newValue ? 1 : 0;
            UpdateConvertSoundPitch();
            UpdateUI();
            if (minigameScript != null) minigameScript.UpdateUI();
        }
    }

    private void OnPendingFuelChanged(float previousValue, float newValue)
    {
        UpdateUI();
        if (minigameScript != null) minigameScript.UpdateUI();
    }

    protected override void OnMachineUIOpened(GameObject uiInstance)
    {
        conversionInputBlocked = false;

        if (uiInstance != null)
        {
            minigameScript = uiInstance.GetComponent<FuelConverterMinigameUI>();
            if (minigameScript == null) minigameScript = uiInstance.GetComponentInChildren<FuelConverterMinigameUI>();
            if (minigameScript != null)
            {
                minigameScript.machine = this;
                minigameScript.UpdateUI();
            }
            else
            {
                BindUIReferences(uiInstance);
                BindUIButtons(uiInstance);
            }
        }
        UpdateUI();
        UpdateManager.Instance.RegisterUpdateable(this);
    }

    protected override void OnMachineUIClosed()
    {
        minigameScript = null;
        StopConvertLoop();
        ResetHoldTimer();
        conversionInputBlocked = false;
        if (UpdateManager.Instance != null)
        {
            UpdateManager.Instance.UnregisterUpdateable(this);
        }
    }

    private void BindUIReferences(GameObject uiInstance)
    {
        if (uiInstance == null) return;
        
        Slider slider = uiInstance.GetComponentInChildren<Slider>();
        if (slider != null) convertProgressBar = slider;

        TMP_Text[] tmpTexts = uiInstance.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in tmpTexts)
        {
            if (text.name.Contains("status", System.StringComparison.OrdinalIgnoreCase))
                statusText = text;
            else if (text.name.Contains("mode", System.StringComparison.OrdinalIgnoreCase))
                modeText = text;
            else if (text.name.Contains("pending", System.StringComparison.OrdinalIgnoreCase) || text.name.Contains("stock", System.StringComparison.OrdinalIgnoreCase) || text.name.Contains("fuel", System.StringComparison.OrdinalIgnoreCase))
                pendingFuel = text;
        }

        Image[] images = uiInstance.GetComponentsInChildren<Image>(true);
        foreach (Image img in images)
        {
            if (img.name.Contains("mode", System.StringComparison.OrdinalIgnoreCase) || img.name.Contains("icon", System.StringComparison.OrdinalIgnoreCase))
            {
                modeImage = img;
                break;
            }
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
            else if (btn.name.Contains("mode", System.StringComparison.OrdinalIgnoreCase) || btn.name.Contains("toggle", System.StringComparison.OrdinalIgnoreCase))
            {
                btn.onClick.AddListener(() => {
                    ToggleMode();
                    PlayClickLocal();
                });
            }
        }
    }

    public void OnUpdate()
    {
        if (minigameScript != null && minigameScript.IsTutorialOpen)
        {
            CancelConversionProgress();
            if (isConvertLoopPlaying)
                StopConvertLoop();
            return;
        }

        if (conversionInputBlocked)
        {
            if (Input.GetKey(KeyCode.Space)) return;
            conversionInputBlocked = false;
        }

        ConvertFuel();
    }

    public override void OnDestroy()
    {
        if (UpdateManager.Instance != null)
        {
            UpdateManager.Instance.UnregisterUpdateable(this);
        }
        base.OnDestroy();
    }

    private void ConvertFuel()
    {
        if (pendingFuelValue.Value > 0)
        {
            if (Input.GetKey(KeyCode.Space))
            {
                if (!isConvertLoopPlaying)
                    StartConvertLoop();
                float actualTimeRequired = isSafeMode.Value ? (conversionTimeRequired * safeModeMultiple) : conversionTimeRequired;

                holdTimer += Time.deltaTime;

                float progress = holdTimer / actualTimeRequired;
                if (convertProgressBar != null)
                    convertProgressBar.value = progress;
                if (minigameScript != null && minigameScript.convertProgressBar != null)
                    minigameScript.convertProgressBar.value = progress;

                if (holdTimer >= actualTimeRequired)
                {
                    CompleteConversion();
                }
            }
            else
            {
                if (isConvertLoopPlaying)
                    StopConvertLoop();

                ResetHoldTimer();
            }
        }
    }

    private void CompleteConversion()
    {
        CompleteConversionRpc();
        StopConvertLoop();
        if (conversionCompleteClip != null && clickAudioSource != null)
        {
            clickAudioSource.PlayOneShot(conversionCompleteClip, completeVolume);
        }
        ResetHoldTimer();
        PressureExpress.Tutorial.TutorialManager.Instance?.ReportMachineCompleted(MachineUIType.FuelConverter);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void CompleteConversionRpc(RpcParams rpcParams = default)
    {
        if (!isUsing.Value) return;

        ulong sender = rpcParams.Receive.SenderClientId;
        if (NetworkManager.Singleton.ConnectedClients[sender].PlayerObject.NetworkObjectId != currentPlayerId.Value)
            return;

        var fuelSys = FuelSystemManager.Instance;
        if (fuelSys != null)
        {
            float finalFuel = 10;

            pendingFuelValue.Value -= finalFuel;
            if (!isSafeMode.Value && SubmarineManager.Instance != null)
            {
                SubmarineManager.Instance.AddTemperature(10);
                finalFuel *= 2f;
            }
            fuelSys.AddFuel(finalFuel);
        }
    }

    private void ResetHoldTimer()
    {
        holdTimer = 0f;
        if (convertProgressBar != null)
            convertProgressBar.value = 0f;
        if (minigameScript != null && minigameScript.convertProgressBar != null)
            minigameScript.convertProgressBar.value = 0f;
    }

    public void CancelConversionProgress()
    {
        StopConvertLoop();
        ResetHoldTimer();
        conversionInputBlocked = true;
    }

    private void UpdateUI()
    {
        if (modeImage != null)
        {
            modeImage.sprite = isSafeMode.Value ? safeSprite : normalSprite;
        }
        if (modeText != null)
        {
            modeText.text = isSafeMode.Value ? "Safe mode" : "Normal mode";
        }
        if (statusText != null)
        {
            statusText.text = isSafeMode.Value ? $"Convert Speed:-{safeModeMultiple}" : "temp:+50%";
        }
        if (pendingFuel != null)
        {
            pendingFuel.text = $"Stock: {pendingFuelValue.Value}";
        }
    }

    public void ToggleMode()
    {
        if (NetworkHelper.IsListening)
        {
            ToggleModeServerRpc();
        }
        else
        {
            isSafeMode.Value = !isSafeMode.Value;
            currentMultiple = isSafeMode.Value ? 1 : 0;
            UpdateConvertSoundPitch();
            UpdateUI();
            if (minigameScript != null) minigameScript.UpdateUI();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ToggleModeServerRpc()
    {
        if (!IsServer) return;
        isSafeMode.Value = !isSafeMode.Value;
    }

    protected override void OnTriggerExit(Collider collision)
    {
        base.OnTriggerExit(collision);
        if (!IsServer) return;

        if (collision.CompareTag("Fuel"))
        {
            FuelItem item = collision.GetComponent<FuelItem>();

            if (item != null && item.transform.parent == null)
            {
                pendingFuelValue.Value += item.fuelValue;
                item.GetComponent<NetworkObject>().Despawn(true);
            }
        }
    }

    private void StartConvertLoop()
    {
        if (fuelConvertLoopClip == null || convertLoopAudioSource == null) return;

        convertLoopAudioSource.clip = fuelConvertLoopClip;
        convertLoopAudioSource.loop = false;
        convertLoopAudioSource.volume = convertLoopVolume;
        UpdateConvertSoundPitch();
        convertLoopAudioSource.Play();
        isConvertLoopPlaying = true;
    }

    private void UpdateConvertSoundPitch()
    {
        if (convertLoopAudioSource == null || fuelConvertLoopClip == null) return;

        float chargeDuration = isSafeMode.Value
            ? conversionTimeRequired * safeModeMultiple
            : conversionTimeRequired;

        if (chargeDuration > 0f && fuelConvertLoopClip.length > 0f)
        {
            convertLoopAudioSource.pitch = fuelConvertLoopClip.length / chargeDuration;
        }
    }

    private void StopConvertLoop()
    {
        if (convertLoopAudioSource != null && convertLoopAudioSource.isPlaying)
        {
            convertLoopAudioSource.Stop();
        }
        if (convertLoopAudioSource != null)
        {
            convertLoopAudioSource.pitch = 1f;
            convertLoopAudioSource.loop = false;
        }
        isConvertLoopPlaying = false;
    }
}
