using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Submarine/Room Type Config")]
public class RoomTypeSO : ScriptableObject
{
    public string typeName;

    [Header("Base Stats")]
    public int roomHitPoints = 5;
    [Tooltip("ความทนทานต่อแรงดันน้ำ")]
    public float structureIntegrity = 100f;

    [Header("Environmental Factors")]
    public float baseTemperature = 25f;

    [Header("Prop")]
    public List<RoomPropData> props; 
}

[System.Serializable]
public class RoomPropData
{
    public string propName;
    public GameObject prefab; 
    public Vector2 offset;    
}