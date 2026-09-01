using UnityEngine;

[CreateAssetMenu(menuName = "Fuel/Item")]
public class FuelItemData : ScriptableObject
{
    public string itemName;
    public float fuelValue;
    public float engineEfficiency;
    public float stressImpact;
}
