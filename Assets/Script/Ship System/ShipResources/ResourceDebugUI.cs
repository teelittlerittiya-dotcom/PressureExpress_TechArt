using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResourceDebugUI : MonoBehaviour
{
    public static ResourceDebugUI Instance;

    public GameObject panel;
    public TextMeshProUGUI titleText;
    public Slider valueSlider;
    public TextMeshProUGUI valueText;

    private string currentResource;

    [Header("Debug UI Machine Status")]
    [SerializeField] private TextMeshProUGUI machineStatusText;
    [SerializeField] private bool debugUIShowing = false;

    private void Update()
    {
        ResourceManager rm = ResourceManager.Instance;
        machineStatusText.text = $"Machine Status\n\tTemperature {rm.temperature} \n\tOxygen {rm.oxygen.Value.ToString("0.#")}/{rm.maxOxygen.Value.ToString("0.#")}\n\tPressure {rm.pressure} N\n\tPower {rm.power}%\n\tWater Level {rm.waterLevel} m.";

        if(Input.GetKeyDown(KeyCode.T)) CloseUI();
        
        
        if (Input.GetKeyDown(KeyCode.F3)) debugUIShowing = !debugUIShowing;
        machineStatusText.gameObject.SetActive(debugUIShowing);
    }

    void Awake()
    {
        Instance = this;
        panel.SetActive(false);
    }

    public void OpenUI(MachineData data)
    {
        panel.SetActive(true);
        titleText.text = data.machineName;
        currentResource = data.controlsResource;

        valueSlider.minValue = 0; 
        valueSlider.maxValue = 100;
        valueSlider.value = GetCurrentValue();
        valueText.text = valueSlider.value.ToString("F1");
    }
    
    public void OnSliderChanged()
    {
        valueText.text = valueSlider.value.ToString("F1");
        SetCurrentValue(valueSlider.value);
    }

    public void CloseUI()
    {
        panel.SetActive(false);
    }

    float GetCurrentValue()
    {
        var rm = ResourceManager.Instance;
        return rm.oxygen.Value;
    }

    void SetCurrentValue(float val)
    {
        var rm = ResourceManager.Instance;
        rm.oxygen.Value = val;
        // 
        // switch (currentResource)
        // {
        //     case "pressure": rm.pressure = val; break;
        //     case "temperature": rm.temperature = val; break;
        //     case "oxygen": rm.oxygen = val; break;
        //     case "waterLevel": rm.waterLevel = val; break;
        //     case "power": rm.power = val; break;
        // }
    }
}
