using UnityEngine;

[CreateAssetMenu(fileName = "New Machine Data", menuName = "Data/Machine/WaterLevel Machine Data")]
public class WaterLevelMachineData : MachineData
{
    [Header("Pump Settings")]
    public float pumpPowerPerMash = 2f; // เปอร์เซ็นต์น้ำที่ลดลงต่อการกด 1 ครั้ง
}