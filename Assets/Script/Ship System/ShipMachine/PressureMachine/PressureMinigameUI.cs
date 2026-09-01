using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MoreMountains.Feedbacks;

using PressureExpress.Framework;

public class PressureMinigameUI : MinigameBaseUI, IUpdateable
{
    [Header("Needle")]
    public RectTransform needle;
    public float needleSpeed = 5f;

    [Header("Zone Sliders (4-Layer System)")]
    [Tooltip("Bottom layer – shows Good zone end. Sibling order: lowest.")]
    public Slider goodBackSlider;
    [Tooltip("Layer above goodback – shows Great zone end.")]
    public Slider greatSlider;
    [Tooltip("Layer above great – covers up to Great zone start.")]
    public Slider goodFrontSlider;
    [Tooltip("Top layer – covers the non-zone start area. Sibling order: highest.")]
    public Slider bgSlider;

    [Header("Zone Range Settings")]
    [Tooltip("Minimum possible value for the gauge.")]
    public float gaugeMinValue = 0f;
    [Tooltip("Maximum possible value for the gauge.")]
    public float gaugeMaxValue = 10f;

    [Header("Good Zone Settings")]
    [Tooltip("Minimum size of the Good zone.")]
    public float goodZoneMinSize = 2f;
    [Tooltip("Maximum size of the Good zone.")]
    public float goodZoneMaxSize = 5f;

    [Header("Great Zone Settings")]
    [Tooltip("Minimum size of the Great zone (must be <= Good zone).")]
    public float greatZoneMinSize = 0.5f;
    [Tooltip("Maximum size of the Great zone.")]
    public float greatZoneMaxSize = 2f;

    [Header("Needle Angle Mapping")]
    [Tooltip("Needle rotation angle when value = gaugeMinValue.")]
    public float needleMinAngle = -90f;
    [Tooltip("Needle rotation angle when value = gaugeMaxValue.")]
    public float needleMaxAngle = 90f;

    [Header("Timing")]
    public float pauseDuration = 0.5f;

    [Header("UI System")]
    public Slider pressureSlider;
    public Image alertOverlay;
    public TextMeshProUGUI stressText;

    [Header("Minigame Text Feedback")]
    public TextMeshProUGUI hitPromptText; 
    public TextMeshProUGUI feedbackText; 

    


    [Header("Feedbacks")]
    [SerializeField] private MMF_Player feedback_open;
    [SerializeField] private MMF_Player feedback_success, feedback_great, feedback_fail;

    private float currentValue;
    private bool movingForward = true;

    private float goodZoneStart;
    private float goodZoneEnd;
    private float greatZoneStart;
    private float greatZoneEnd;

    private bool isPaused = false;
    private float pauseTimer = 0f;
    internal PressureMachine machine;

    private static readonly float[] curveUI = { 0f, 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f };
    private static readonly float[] curveSlider = { 0f, 0.8f, 1.5f, 2.25f, 3.32f, 5f, 6.7f, 7.75f, 8.54f, 9.22f, 10f };

    protected override void Awake()
    {
        base.Awake();
        ValidateReferences();
        DisableSliderInteraction(goodBackSlider);
        DisableSliderInteraction(greatSlider);
        DisableSliderInteraction(goodFrontSlider);
        DisableSliderInteraction(bgSlider);
    }

    private void ValidateReferences()
    {
        if (needle == null)
            Debug.LogWarning($"[{nameof(PressureMinigameUI)}] 'needle' is not bound on '{gameObject.name}'.", this);
        if (goodBackSlider == null)
            Debug.LogWarning($"[{nameof(PressureMinigameUI)}] 'goodBackSlider' is not bound on '{gameObject.name}'.", this);
        if (greatSlider == null)
            Debug.LogWarning($"[{nameof(PressureMinigameUI)}] 'greatSlider' is not bound on '{gameObject.name}'.", this);
        if (goodFrontSlider == null)
            Debug.LogWarning($"[{nameof(PressureMinigameUI)}] 'goodFrontSlider' is not bound on '{gameObject.name}'.", this);
        if (bgSlider == null)
            Debug.LogWarning($"[{nameof(PressureMinigameUI)}] 'bgSlider' is not bound on '{gameObject.name}'.", this);
        if (pressureSlider == null)
            Debug.LogWarning($"[{nameof(PressureMinigameUI)}] 'pressureSlider' is not bound on '{gameObject.name}'.", this);
        if (stressText == null)
            Debug.LogWarning($"[{nameof(PressureMinigameUI)}] 'stressText' is not bound on '{gameObject.name}'.", this);
    }

    // ================= START =================
    // Update logic is handled by IUpdateable.OnUpdate(), not MinigameBaseUI.Update()
    protected override void OnMinigameUpdate() { }

    protected override void OnEnable()
    {
        base.OnEnable();
        currentValue = gaugeMinValue;
        movingForward = true;
        isPaused = false;
        pauseTimer = 0f;
        if (feedbackText != null) feedbackText.gameObject.SetActive(false);
        if (hitPromptText != null) hitPromptText.gameObject.SetActive(true);

        RandomizeZones();
        UpdateNeedleVisual();
        UpdateVisuals();

        if (feedback_open) feedback_open.PlayFeedbacks();

        if (UpdateManager.Instance != null)
        {
            UpdateManager.Instance.RegisterUpdateable(this);
        }
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

    public void OnUpdate()
    {
        if (IsTutorialOpen)
        {
            UpdateVisuals();
            return;
        }

        if (isPaused)
        {
            HandlePause();
        }
        else
        {
            MoveNeedle();
            HandleInput();
            UpdatePromptText();
        }

        UpdateVisuals();
    }

    // ================= NEEDLE (value-based, still rotates visually) =================
    void MoveNeedle()
    {
        float dir = movingForward ? 1f : -1f;
        currentValue += dir * needleSpeed * Time.deltaTime;

        if (currentValue >= gaugeMaxValue)
        {
            currentValue = gaugeMaxValue;
            movingForward = false;
        }
        else if (currentValue <= gaugeMinValue)
        {
            currentValue = gaugeMinValue;
            movingForward = true;
        }

        UpdateNeedleVisual();
    }

    /// <summary>
    /// Converts the current value to a rotation angle (with curve correction)
    /// and applies it to the needle.
    /// </summary>
    void UpdateNeedleVisual()
    {
        if (needle == null) return;

        // Map through the curve so the needle matches the curved slider positions.
        // Invert t (1-t) because the slider fill direction is opposite to the
        // needle's default rotation direction on the curved gauge.
        float mappedValue = MapValueToSlider(currentValue);
        float t = Mathf.InverseLerp(gaugeMinValue, gaugeMaxValue, mappedValue);
        float angle = Mathf.Lerp(needleMinAngle, needleMaxAngle, 1f - t);
        needle.localRotation = Quaternion.Euler(0, 0, angle);
    }

    // ================= ZONE RANDOMIZATION (4-Layer Slider) =================
    /// <summary>
    /// Randomizes Good and Great zone positions, then sets the 4 slider values.
    /// 
    /// Layer logic (top to bottom):
    ///   bg        → covers 0 to goodStart   (hides the start)
    ///   goodFront → covers 0 to greatStart  (hides area before great)
    ///   great     → covers 0 to greatEnd    (reveals great zone)
    ///   goodBack  → covers 0 to goodEnd     (reveals good zone)
    ///
    /// Visual result: goodStart–greatStart = Good, greatStart–greatEnd = Great,
    ///                greatEnd–goodEnd = Good, everything else = nothing.
    /// </summary>
    void RandomizeZones()
    {
        float range = gaugeMaxValue - gaugeMinValue;

        // --- Good zone: random start within the gauge, clamped so it fits ---
        float goodSize = Random.Range(goodZoneMinSize, goodZoneMaxSize);
        goodSize = Mathf.Min(goodSize, range); // safety clamp

        float goodStart = Random.Range(gaugeMinValue, gaugeMaxValue - goodSize);
        float goodEnd = goodStart + goodSize;

        goodZoneStart = goodStart;
        goodZoneEnd = goodEnd;

        // --- Great zone: fits inside the Good zone ---
        float greatMaxAllowed = Mathf.Min(greatZoneMaxSize, goodSize);
        float greatSize = Random.Range(greatZoneMinSize, Mathf.Max(greatZoneMinSize, greatMaxAllowed));
        greatSize = Mathf.Min(greatSize, goodSize); // safety clamp

        float greatOffset = Random.Range(0f, goodSize - greatSize);
        float greatStart = goodStart + greatOffset;
        float greatEnd = greatStart + greatSize;

        greatZoneStart = greatStart;
        greatZoneEnd = greatEnd;

        // --- Set slider values (normalized 0–1, with curve correction) ---
        ApplySliderValue(bgSlider, MapValueToSlider(goodStart) / range);
        ApplySliderValue(goodFrontSlider, MapValueToSlider(greatStart) / range);
        ApplySliderValue(greatSlider, MapValueToSlider(greatEnd) / range);
        ApplySliderValue(goodBackSlider, MapValueToSlider(goodEnd) / range);

        Debug.Log($"[PressureMinigameUI] Zones randomized — Good: {goodStart:F1}–{goodEnd:F1}, " +
                  $"Great: {greatStart:F1}–{greatEnd:F1} | " +
                  $"Slider values → bg:{goodStart / range:F2} gf:{greatStart / range:F2} " +
                  $"gt:{greatEnd / range:F2} gb:{goodEnd / range:F2}");
    }

    /// <summary>
    /// Safely sets a slider's normalized value.
    /// </summary>
    void ApplySliderValue(Slider slider, float normalizedValue)
    {
        if (slider == null) return;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = Mathf.Clamp01(normalizedValue);
    }

    /// <summary>
    /// Maps a linear gauge value (0–10) to the actual slider position on the
    /// curved arc using piecewise-linear interpolation of the calibration table.
    /// For example: UI value 4 → slider position ~3.32.
    /// </summary>
    float MapValueToSlider(float uiValue)
    {
        // Clamp to table range
        if (uiValue <= curveUI[0]) return curveSlider[0];
        if (uiValue >= curveUI[curveUI.Length - 1]) return curveSlider[curveSlider.Length - 1];

        // Find the two surrounding calibration points and lerp between them.
        for (int i = 0; i < curveUI.Length - 1; i++)
        {
            if (uiValue >= curveUI[i] && uiValue <= curveUI[i + 1])
            {
                float t = (uiValue - curveUI[i]) / (curveUI[i + 1] - curveUI[i]);
                return Mathf.Lerp(curveSlider[i], curveSlider[i + 1], t);
            }
        }

        // Fallback (should never reach here)
        return uiValue;
    }

    /// <summary>
    /// Disables user interaction on a slider (display-only).
    /// </summary>
    void DisableSliderInteraction(Slider slider)
    {
        if (slider == null) return;
        slider.interactable = false;
    }

    // ================= TEXT PROMPT =================
    void UpdatePromptText()
    {
        if (hitPromptText == null) return;

        bool isInGreatZone = currentValue >= greatZoneStart && currentValue <= greatZoneEnd;
        bool isInGoodZone  = currentValue >= goodZoneStart  && currentValue <= goodZoneEnd;

        if (isInGreatZone)
        {
            hitPromptText.text = "PERFECT!";
            hitPromptText.color = Color.green;
        }
        else if (isInGoodZone)
        {
            hitPromptText.text = "CLICK NOW!";
            hitPromptText.color = Color.white;
        }
        else
        {
            hitPromptText.text = "WAIT...";
            hitPromptText.color = Color.darkGoldenRod;
        }
    }

    // ================= INPUT (3-tier check: Great / Good / Fail) =================
    void HandleInput()
    {
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
        {
            bool isInGreatZone = currentValue >= greatZoneStart && currentValue <= greatZoneEnd;
            bool isInGoodZone  = currentValue >= goodZoneStart  && currentValue <= goodZoneEnd;

            // Determine result tier
            HitResult result;
            if (isInGreatZone)      result = HitResult.Great;
            else if (isInGoodZone)  result = HitResult.Good;
            else                    result = HitResult.Fail;

            // Play the appropriate one-shot sound (overlaps on rapid clicks).
            PlayHitSound(result);

            // Map result to tier int: 2 = Great, 1 = Good, 0 = Fail
            int hitTier = result switch
            {
                HitResult.Great => 2,
                HitResult.Good  => 1,
                _               => 0
            };

            // Network — send tier so server applies the correct pressure change
            if (machine != null)
            {
                machine.SubmitResult(hitTier);
            }

            if (result == HitResult.Great || result == HitResult.Good)
            {
                PressureExpress.Tutorial.TutorialManager.Instance?.ReportMachineCompleted(MachineUIType.PressureGame);
            }

            // Visual feedback — read actual values from the machine for accuracy
            if (feedbackText != null)
            {
                feedbackText.gameObject.SetActive(true);
                switch (result)
                {
                    case HitResult.Great:
                        float greatVal = machine != null ? machine.greatReduction : 25f;
                        feedbackText.text = $"GREAT!\n-{greatVal}%";
                        feedbackText.color = Color.green;
                        break;
                    case HitResult.Good:
                        float goodVal = machine != null ? machine.goodReduction : 15f;
                        feedbackText.text = $"GOOD!\n-{goodVal}%";
                        feedbackText.color = Color.white;
                        break;
                    default:
                        float failVal = machine != null ? machine.failPenalty : 5f;
                        feedbackText.text = $"FAIL!\n+{failVal}%";
                        feedbackText.color = Color.red;
                        break;
                }
            }

            if (hitPromptText != null) hitPromptText.gameObject.SetActive(false);

            RandomizeZones();
            isPaused = true;
        }
    }

    private void PlayHitSound(HitResult result)
    {
        switch (result)
        {
            case HitResult.Great:
                if (feedback_great) feedback_great.PlayFeedbacks();
                break;
            case HitResult.Good:
                if (feedback_success) feedback_success.PlayFeedbacks();
                break;
            default:
                if (feedback_fail) feedback_fail.PlayFeedbacks();
                break;
        }
    }

    void HandlePause()
    {
        pauseTimer += Time.deltaTime;

        // Needle keeps sweeping even during the feedback pause
        MoveNeedle();

        if (pauseTimer >= pauseDuration)
        {
            pauseTimer = 0f;
            isPaused = false;

            // Needle continues from its current position — no reset

            if (feedbackText != null) feedbackText.gameObject.SetActive(false);
            if (hitPromptText != null) hitPromptText.gameObject.SetActive(true);
        }
    }

    // ================= UI =================
    void UpdateVisuals()
    {
        if (SubmarineManager.Instance == null) return;

        float stress = SubmarineManager.Instance.submarinePressure.Value;
        float max = SubmarineManager.Instance.maxPressure;

        if (pressureSlider != null)
        {
            pressureSlider.value = stress / max;
        }

        if (stressText != null)
        {
            stressText.text = $"STRESS: {Mathf.RoundToInt(stress)}%";
        }

        if (alertOverlay != null)
        {
            if (SubmarineManager.Instance.isPressureAlerting)
            {
                alertOverlay.gameObject.SetActive(true);
                alertOverlay.color = new Color(1, 0, 0, Mathf.PingPong(Time.time * 2f, 0.4f));
            }
            else
            {
                alertOverlay.gameObject.SetActive(false);
            }
        }
    }

    // ================= RESULT TIER =================
    private enum HitResult { Great, Good, Fail }
}