using UnityEngine;
using UnityEngine.UI;
using PressureExpress.Framework;

public class OxygenPumpMinigame : MinigameBaseUI
{
    [Header("Slider Input")]
    [SerializeField] private Slider pumpSlider;
    [SerializeField] private float sliderReturnSpeed = 2.5f;

    [Header("Pump Visual")]
    [SerializeField] private Transform pumpHandle;
    [SerializeField] private float pumpDownPos = -0.15f;
    [SerializeField] private float pumpUpPos = 0f;
    [SerializeField] private float pumpSmooth = 10f;

    [Header("Oxygen")]
    [SerializeField] private float oxygenIncreaseSpeed = 0.6f;
    [SerializeField] private float oxygenDecreaseSpeed = 0.3f;
    private float oxygen;

    [Header("Resistance")]
    [SerializeField] private float startResistance = 12f;
    [SerializeField] private float endResistance = 3f;
    private float currentResistance;

    [Header("UI")]
    [SerializeField] private Transform oxygenBar;

    private float smoothedSliderValue;

    protected void Start()
    {
        currentResistance = startResistance;
        if (pumpSlider != null)
        {
            smoothedSliderValue = pumpSlider.value;
        }
    }

    public override void ResetMinigame()
    {
        base.ResetMinigame();
        oxygen = 0f;
        if (pumpSlider != null) pumpSlider.value = 0f;
    }

    protected override void OnMinigameUpdate()
    {
        UpdateResistance();
        HandleSliderReturn();
        UpdateSliderSmoothing();
        UpdatePumpVisual();
        UpdateOxygen();
        UpdateBarScaleY(oxygenBar, oxygen);
    }

    private void HandleSliderReturn()
    {
        if (pumpSlider != null && !Input.GetMouseButton(0))
        {
            pumpSlider.value = Mathf.Lerp(
                pumpSlider.value,
                0f,
                Time.deltaTime * sliderReturnSpeed
            );
        }
    }

    private void UpdateResistance()
    {
        currentResistance = Mathf.Lerp(
            startResistance,
            endResistance,
            oxygen
        );
    }

    private void UpdateSliderSmoothing()
    {
        if (pumpSlider == null) return;
        smoothedSliderValue = Mathf.Lerp(
            smoothedSliderValue,
            pumpSlider.value,
            Time.deltaTime * currentResistance
        );
    }

    private void UpdatePumpVisual()
    {
        if (pumpHandle == null) return;
        float targetY = Mathf.Lerp(pumpUpPos, pumpDownPos, smoothedSliderValue);

        Vector3 pos = pumpHandle.localPosition;
        pos.y = Mathf.Lerp(pos.y, targetY, Time.deltaTime * pumpSmooth);
        pumpHandle.localPosition = pos;
    }

    private void UpdateOxygen()
    {
        if (pumpSlider != null && pumpSlider.value > 0.05f)
        {
            oxygen += pumpSlider.value * oxygenIncreaseSpeed * Time.deltaTime;
        }
        else
        {
            oxygen -= oxygenDecreaseSpeed * Time.deltaTime;
        }

        oxygen = Mathf.Clamp01(oxygen);
        SetProgress(oxygen);
    }
}
