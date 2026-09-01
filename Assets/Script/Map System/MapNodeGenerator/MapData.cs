using UnityEngine;

[CreateAssetMenu(menuName = "Data/Map/MapData", fileName = "NewMapData")]
public class MapData : ScriptableObject 
{
    public GameObject mapGenerator; //แก้เป็น map gen class
    [Space]

    [Header("Settings")]
    public MapType mapType;

    [Space]
    public float waterTemp;
    public float waterPressure;
}
