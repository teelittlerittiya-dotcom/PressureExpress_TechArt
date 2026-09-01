using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FuelLevelUI : MonoBehaviour
{
    [Header("Fuel UI Elements")]
    public Slider fuelLevelSlider;
    public TextMeshProUGUI fuelLevelText;

    private void Start()
    {
        FuelSystemManager.Instance.OnFuelLevelChanged.AddListener(UpdateUI);
        UpdateUI();
    }
    private void OnEnable()
    {
        if (FuelSystemManager.Instance == null) return;
        FuelSystemManager.Instance.OnFuelLevelChanged.AddListener(UpdateUI);
    }
    private void OnDisable()
    {
        FuelSystemManager.Instance.OnFuelLevelChanged.RemoveListener(UpdateUI);
    }

    private void UpdateUI()
    {
        if (FuelSystemManager.Instance == null) return;
        float currentFuel = FuelSystemManager.Instance.currentFuelLevel.Value;
        float maxFuel = FuelSystemManager.Instance.maxFuelLevel;

        if (fuelLevelSlider != null)
        {
            fuelLevelSlider.value = currentFuel / maxFuel;
        }

        if (fuelLevelText != null)
        {
            fuelLevelText.text = $"{(currentFuel / maxFuel) * 100:F1}%";
        }
    }
}