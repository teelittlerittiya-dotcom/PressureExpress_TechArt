using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class Fuel : MonoBehaviour
{
    public enum Mode { Normal, Safe }

    [Header("Mode")]
    public Mode currentMode = Mode.Normal;

    [Header("Fuel")]
    public float fuelLevel = 0.75f;

    [Header("Items")]
    public List<FuelItemData> insertedItems = new List<FuelItemData>();

    [Header("Preview")]
    float previewFuel;
    float previewEfficiency;
    float previewStress;

    [Header("UI")]
    public TMP_Text fuelText;
    public TMP_Text outputText;
    public TMP_Text modeText;

    void Start()
    {
        UpdateUI();
    }

    public void InsertItem(FuelItemData item)
    {
        insertedItems.Add(item);
        CalculatePreview();
    }

    void CalculatePreview()
    {
        previewFuel = 0;
        previewEfficiency = 0;
        previewStress = 0;

        foreach (var item in insertedItems)
        {
            previewFuel += item.fuelValue;
            previewEfficiency += item.engineEfficiency;
            previewStress += item.stressImpact;
        }

        if (currentMode == Mode.Safe)
        {
            previewFuel *= 0.6f;
            previewEfficiency = 0;
            previewStress = 0;
        }

        UpdateUI();
    }

    
    public void StartConversion()
    {
        fuelLevel += previewFuel;
        fuelLevel = Mathf.Clamp01(fuelLevel);

        insertedItems.Clear();

        CalculatePreview();
    }

    
    public void SetNormalMode()
    {
        currentMode = Mode.Normal;
        CalculatePreview();
    }

    public void SetSafeMode()
    {
        currentMode = Mode.Safe;
        CalculatePreview();
    }

    
    void UpdateUI()
    {
        fuelText.text = "FUEL LEVEL: " + (fuelLevel * 100f).ToString("0") + "%";

        outputText.text =
            "Fuel Output: " + (previewFuel * 100f).ToString("0") + "%\n" +
            "+" + previewEfficiency.ToString("0") + "% Engine Efficiency\n" +
            "Stress: " + previewStress.ToString("0");

        modeText.text = "Mode: " + currentMode.ToString();
    }
}
