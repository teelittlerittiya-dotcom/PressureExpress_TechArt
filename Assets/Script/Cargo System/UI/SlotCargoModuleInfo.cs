using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SlotCargoModuleInfo : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private Slider durabilitySlider;
    [SerializeField] private TextMeshProUGUI percentText;     // เช่น "100%"
    [SerializeField] private TextMeshProUGUI valueText;       // เช่น "50/100"
    [SerializeField] private TextMeshProUGUI moduleStateText; // เช่น "Impact : Normal"

    // ฟังก์ชันสำหรับอัปเดตข้อมูลใน Slot นี้
    public void UpdateSlot(CargoModule module, float currentValue)
    {
        float minValue = module.GetMinValue();
        float maxValue = module.GetMaxValue();
        float percent = module.GetNormalizedValue(currentValue);
        float percentDisplay = percent * 100f;

        // 1. Slider
        if (durabilitySlider != null) 
        {
            durabilitySlider.minValue = minValue;
            durabilitySlider.maxValue = maxValue;
            durabilitySlider.value = Mathf.Clamp(currentValue, minValue, maxValue);
        }

        // 2. Text Percent
        if (percentText != null)
        {
            percentText.text = $"{percentDisplay:F0}%";
        }

        // 3. Text Value/Max
        if (valueText != null)
        {
            valueText.text = $"{currentValue:F1}  [{minValue:F0} .. {maxValue:F0}]";
        }

        // 4. Module Name : Visual Name
        if (moduleStateText != null)
        {
            string moduleName = module.GetType().Name.Replace("Module", ""); // ตัดคำว่า Module ออก
            string stateName = module.GetStateName(currentValue);
            
            // เปลี่ยนสีข้อความตามสถานะ (Optional: ใส่ logic สีเพิ่มได้)
            string colorHex = percentDisplay > 50 ? "#00FF00" : (percentDisplay > 25 ? "#FFFF00" : "#FF0000");
            
            moduleStateText.text = $"<b>{moduleName}</b> : <color={colorHex}>{stateName}</color>";
        }
    }
}
