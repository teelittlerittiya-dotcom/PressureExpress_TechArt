using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PressureExpress.Framework;

public class FuelConverterMinigameUI : MinigameBaseUI, IUpdateable
{
    [HideInInspector] public FuelConverterMachine machine;

    [Header("UI References")]
    public Slider convertProgressBar;
    public Image modeImage;
    public TMP_Text modeText;
    public TMP_Text statusText;
    public TMP_Text pendingFuelText;

    [Header("Buttons")]
    public Button modeButton;
    public Button acceptYesButton;
    public Button acceptNoButton;

    [Header("Mode Sprites")]
    public Sprite normalSprite;
    public Sprite safeSprite;

    [Header("Confirmation Panel (Optional)")]
    public GameObject acceptPanel;

    protected override void Awake()
    {
        base.Awake();
        ValidateReferences();
        SetupButtonListeners();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (machine == null)
        {
            machine = Object.FindFirstObjectByType<FuelConverterMachine>();
        }

        if (UpdateManager.Instance != null)
        {
            UpdateManager.Instance.RegisterUpdateable(this);
        }
        UpdateUI();
    }

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

    protected override void OnMinigameUpdate() { }

    public void OnUpdate()
    {
        if (machine == null) return;
        UpdateUI();
    }

    public void UpdateUI()
    {
        if (machine == null) return;

        bool isSafe = machine.IsSafeMode;

        // Button displays the target mode to switch into
        if (modeImage != null)
        {
            Sprite s = isSafe ? normalSprite : safeSprite;
            if (s != null) modeImage.sprite = s;
        }

        if (modeText != null)
        {
            modeText.text = isSafe ? "Normal mode" : "Safe mode";
        }

        if (statusText != null)
        {
            statusText.text = isSafe ? $"Convert Speed:-{machine.SafeModeMultiple}" : "temp:+50%";
        }

        if (pendingFuelText != null)
        {
            pendingFuelText.text = $"Stock: {machine.pendingFuelValue.Value}";
        }
    }

    public void OnClickToggleMode()
    {
        if (IsTutorialOpen) return;

        if (machine != null)
        {
            machine.ToggleMode();
            machine.PlayClickLocal();
            UpdateUI();
        }
    }

    public override void ToggleTutorial()
    {
        if (machine != null)
        {
            machine.CancelConversionProgress();
        }

        base.ToggleTutorial();
    }

    public void OnClickOpenAcceptPanel()
    {
        if (IsTutorialOpen) return;

        if (acceptPanel != null)
        {
            acceptPanel.SetActive(true);
            if (modeButton != null) modeButton.gameObject.SetActive(false);
        }
        else
        {
            OnClickToggleMode();
        }
    }

    public void OnClickConfirmYes()
    {
        if (IsTutorialOpen) return;

        OnClickToggleMode();
        if (acceptPanel != null) acceptPanel.SetActive(false);
        if (modeButton != null) modeButton.gameObject.SetActive(true);
    }

    public void OnClickConfirmNo()
    {
        if (acceptPanel != null) acceptPanel.SetActive(false);
        if (modeButton != null) modeButton.gameObject.SetActive(true);
    }

    private void SetupButtonListeners()
    {
        if (modeButton != null)
        {
            modeButton.onClick.RemoveAllListeners();
            if (acceptPanel != null)
            {
                modeButton.onClick.AddListener(OnClickOpenAcceptPanel);
            }
            else
            {
                modeButton.onClick.AddListener(OnClickToggleMode);
            }
        }
        if (acceptYesButton != null)
        {
            acceptYesButton.onClick.RemoveAllListeners();
            acceptYesButton.onClick.AddListener(OnClickConfirmYes);
        }
        if (acceptNoButton != null)
        {
            acceptNoButton.onClick.RemoveAllListeners();
            acceptNoButton.onClick.AddListener(OnClickConfirmNo);
        }
    }

    private void ValidateReferences()
    {
        if (convertProgressBar == null)
            Debug.LogWarning($"[{nameof(FuelConverterMinigameUI)}] 'convertProgressBar' is not bound on '{gameObject.name}'.", this);
        if (modeImage == null)
            Debug.LogWarning($"[{nameof(FuelConverterMinigameUI)}] 'modeImage' is not bound on '{gameObject.name}'.", this);
        if (modeText == null)
            Debug.LogWarning($"[{nameof(FuelConverterMinigameUI)}] 'modeText' is not bound on '{gameObject.name}'.", this);
        if (statusText == null)
            Debug.LogWarning($"[{nameof(FuelConverterMinigameUI)}] 'statusText' is not bound on '{gameObject.name}'.", this);
        if (pendingFuelText == null)
            Debug.LogWarning($"[{nameof(FuelConverterMinigameUI)}] 'pendingFuelText' is not bound on '{gameObject.name}'.", this);
        if (modeButton == null)
            Debug.LogWarning($"[{nameof(FuelConverterMinigameUI)}] 'modeButton' is not bound on '{gameObject.name}'.", this);
    }
}
