using UnityEngine;

public class MachineData : ScriptableObject
{
    public int machineLevel = 1;
    
    public string machineName;
    public Sprite machineSprite;
    public string controlsResource;

    //float upgradeCost = 1000;
    public virtual void UpgradeMachine() //virtual = เผื่อแก้เพิ่ม
    {
        //เงื่อนไขเช็คอัพเกรด ที่ใช้เหมือนกันในทุก machine
        //เช่น (if เงิน >= upgradeCost)
        machineLevel++;
    }
    
    public MachineData Clone()
    {
        MachineData clone = ScriptableObject.CreateInstance<MachineData>();

        clone.machineLevel = machineLevel;
        clone.machineName = machineName;
        clone.machineSprite = machineSprite;
        clone.controlsResource = controlsResource;
        
        return clone;
    }
}
