using UnityEngine;
using UnityEngine.UI;

public class PumpHandleVisualizer : MonoBehaviour
{
    [Header("Slider Settings")]
    public Slider waterSlider;
    public float sliderReturnSpeed = 3f;

    [Header("Pump Handle Settings")]
    public Transform handle;
    public float downY = -100f;
    public float upY = 0f;
    public float handleSmooth = 10f;

    private float smoothedValue;

    private void Start()
    {
        if (waterSlider != null)
        {
            smoothedValue = waterSlider.value;
        }
    }

    public void ResetSlider()
    {
        if (waterSlider != null)
        {
            waterSlider.value = 0f;
        }
        smoothedValue = 0f;
    }

    public float GetSliderValue()
    {
        return waterSlider != null ? waterSlider.value : 0f;
    }

    public void SetSliderValue(float value)
    {
        if (waterSlider != null)
        {
            waterSlider.value = value;
        }
    }

    public void Tick()
    {
        HandleSliderReturn();
        SmoothSlider();
        UpdatePumpVisual();
    }

    private void HandleSliderReturn()
    {
        if (waterSlider == null) return;

        if (!Input.GetMouseButton(0))
        {
            waterSlider.value = Mathf.Lerp(waterSlider.value, 0f, Time.deltaTime * sliderReturnSpeed);
        }
    }

    private void SmoothSlider()
    {
        if (waterSlider == null) return;
        smoothedValue = Mathf.Lerp(smoothedValue, waterSlider.value, Time.deltaTime * 10f);
    }

    private void UpdatePumpVisual()
    {
        if (handle == null) return;

        float targetY = Mathf.Lerp(upY, downY, smoothedValue);
        Vector3 pos = handle.localPosition;
        pos.y = Mathf.Lerp(pos.y, targetY, Time.deltaTime * handleSmooth);
        handle.localPosition = pos;
    }
}
