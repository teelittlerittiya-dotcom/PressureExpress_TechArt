using UnityEngine;
using PressureExpress.Framework;

public class TemperatureMiniGame : MinigameBaseUI
{
    [Header("Valve Handle (Square)")]
    [SerializeField] private RectTransform valveHandle;
    [SerializeField] private float valveRotateSpeed = 1.2f;
    [SerializeField] private float maxValveRotation = 90f;

    private float valveValue;
    private bool dragging;
    private Vector2 lastMousePos;

    [Header("Resistance (ฝืดตอนแรก)")]
    [SerializeField] private float startResistance = 12f;
    [SerializeField] private float endResistance = 3f;
    [SerializeField] private float resistanceReduceSpeed = 0.4f;
    private float currentResistance;

    [Header("Needle (Gauge)")]
    [SerializeField] private RectTransform needle;
    [SerializeField] private float minNeedleAngle = -90f;
    [SerializeField] private float maxNeedleAngle = 90f;
    [SerializeField] private float needleSmooth = 8f;
    private float currentNeedleAngle;

    [Header("Pressure Value")]
    [SerializeField] private float pressure = 0f;
    [SerializeField] private float pressureIncreaseSpeed = 0.6f;
    [SerializeField] private float pressureDecreaseSpeed = 0.25f;

    [Header("Optional Pressure Bar")]
    [SerializeField] private Transform pressureBar;

    protected void Start()
    {
        currentResistance = startResistance;
    }

    public override void ResetMinigame()
    {
        base.ResetMinigame();
        pressure = 0f;
        valveValue = 0f;
        dragging = false;
        currentResistance = startResistance;
    }

    protected override void OnMinigameUpdate()
    {
        HandleValveInput();
        UpdateValveRotation();
        UpdatePressure();
        UpdateNeedle();
        UpdateBarScaleY(pressureBar, pressure);
    }

    private void HandleValveInput()
    {
        if (valveHandle == null) return;

        if (Input.GetMouseButtonDown(0) &&
           RectTransformUtility.RectangleContainsScreenPoint(valveHandle, Input.mousePosition))
        {
            dragging = true;
            lastMousePos = Input.mousePosition;
        }

        if (Input.GetMouseButtonUp(0))
        {
            dragging = false;
            currentResistance = Mathf.Lerp(currentResistance, startResistance, 0.5f);
        }

        if (!dragging)
        {
            valveValue = Mathf.Lerp(valveValue, 0f, Time.deltaTime * 5f);
            return;
        }

        Vector2 delta = (Vector2)Input.mousePosition - lastMousePos;
        lastMousePos = Input.mousePosition;

        currentResistance = Mathf.Lerp(
            currentResistance,
            endResistance,
            Time.deltaTime * resistanceReduceSpeed
        );

        float rotationForce = (delta.x * 0.01f) / Mathf.Max(0.01f, currentResistance);
        valveValue = Mathf.Clamp(valveValue + rotationForce, -1f, 1f);
    }

    private void UpdateValveRotation()
    {
        if (valveHandle == null) return;
        float targetAngle = valveValue * maxValveRotation;

        valveHandle.localRotation = Quaternion.Lerp(
            valveHandle.localRotation,
            Quaternion.Euler(0, 0, -targetAngle),
            Time.deltaTime * valveRotateSpeed
        );
    }

    private void UpdatePressure()
    {
        pressure += valveValue * pressureIncreaseSpeed * Time.deltaTime;

        if (Mathf.Abs(valveValue) < 0.05f)
            pressure -= pressureDecreaseSpeed * Time.deltaTime;

        pressure = Mathf.Clamp01(pressure);
        SetProgress(pressure);
    }

    private void UpdateNeedle()
    {
        if (needle == null) return;
        float targetAngle = Mathf.Lerp(minNeedleAngle, maxNeedleAngle, pressure);

        currentNeedleAngle = Mathf.Lerp(
            currentNeedleAngle,
            targetAngle,
            Time.deltaTime * needleSmooth
        );

        needle.localRotation = Quaternion.Euler(0, 0, currentNeedleAngle);
    }
}
