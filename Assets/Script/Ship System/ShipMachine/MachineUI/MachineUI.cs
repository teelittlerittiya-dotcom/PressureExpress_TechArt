using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class MachineUI : interactableUI
{
    public MachineData parentMachineData;
    public Transform parentMachinePrefab;
    
    [Header("UI")]
    [SerializeField] TextMeshProUGUI txtMachineName;
    [SerializeField] private float yOffset = 1;
    void Update()
    {
        if (parentMachinePrefab != null)
            this.transform.position = new Vector2(parentMachinePrefab.position.x, yOffset + parentMachinePrefab.position.y);
        if (transform.rotation != new Quaternion(0, 0, 0, 0))
            transform.rotation = new Quaternion(0, 0, 0, 0);
    }

    public void Init(MachineData newMachineData, Transform newCargoPrefab, float newYOffset = 1f)
    {
        parentMachineData = newMachineData;
        parentMachinePrefab = newCargoPrefab;
        txtMachineName.text = $"{parentMachineData.machineName} Lv.{parentMachineData.machineLevel}";
        yOffset = newYOffset;
    }
    public override void UpdateUI()
    {
        if (parentMachineData == null) return;
        if (txtMachineName.text != null)
            txtMachineName.text = $"{parentMachineData.machineName} Lv.{parentMachineData.machineLevel}";
    }
}
