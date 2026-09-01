using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Data/Map/MapDifficultySetting", fileName = "NewMapDifficultySetting")]
public class MapDifficultySetting : ScriptableObject
{
    [Header("General Settings")]
    public int destinationCount=1;
    public int nodeCountToDestination=3;

    [Space]
    public int maxChildPerNodes=3;

    [Space]
    public int blankMapChance = 10;
    public int treasureMapChance = 10;
    public int dangerMapChance = 10;
    public int mysteryMapChance = 10;
    
}