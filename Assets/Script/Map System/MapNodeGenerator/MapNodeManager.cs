using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

public enum MapType
{
    Blank,
    SpawnPoint,
    Destinaton,
    Treasure,
    Danger,
    Mystery
}

public class MapNodeManager : NetworkBehaviour
{
    [Header("Map Nodes Configuration")]
    public NetworkVariable<int> mapTreeSeed = new NetworkVariable<int>(0);
    public NetworkVariable<int> currentNodeIndex = new NetworkVariable<int>(-1);

    public List<MapNode<MapData>> allNodes = new List<MapNode<MapData>>();

    public MapNode<MapData> startNode;
    public List<MapNode<MapData>> destinationNodes;

    [Space]
    [Header("Possible Map Data(s)")]
    [SerializeField] private List<MapData> spawnPointMapData;
    [SerializeField] private List<MapData> destinationMapData;
    [SerializeField] private List<MapData> blankMapData;
    [SerializeField] private List<MapData> treasureMapData;
    [SerializeField] private List<MapData> dangerMapData;
    [SerializeField] private List<MapData> mysteryMapData;

    [Header("Difficulty Settings")]
    public MapDifficultySetting currentMapDifficulty;

    [Header("Generation Rules")]
    public Dictionary<MapType, List<MapType>> childGenerationRules = new Dictionary<MapType, List<MapType>>()
    {
        { MapType.SpawnPoint, new List<MapType> { MapType.Blank, MapType.Treasure, MapType.Mystery } },
        { MapType.Blank, new List<MapType> { MapType.Blank, MapType.Treasure, MapType.Danger, MapType.Mystery } },
        { MapType.Treasure, new List<MapType> { MapType.Blank, MapType.Mystery } },
        { MapType.Danger, new List<MapType> { MapType.Blank, MapType.Danger } },
        { MapType.Mystery, new List<MapType> { MapType.Blank, MapType.Treasure, MapType.Danger, MapType.Mystery } },
        { MapType.Destinaton, new List<MapType> { } }
    };

    public MapNode<MapData> GetCurrentNode()
    {
        if (currentNodeIndex.Value >= 0 && currentNodeIndex.Value < allNodes.Count)
            return allNodes[currentNodeIndex.Value];
        return null;
    }

    public void GenerateNewMap(int seed)
    {
        Random.InitState(seed);
        ResetAllNodes();

        GetRandomSpawnPointNode();

        if (IsServer) currentNodeIndex.Value = 0;

        GetRandomDestinationNode();

        int mainBranchCount = Random.Range(Mathf.Min(currentMapDifficulty.maxChildPerNodes, 2), currentMapDifficulty.maxChildPerNodes + 1);
        for (int i = 0; i < mainBranchCount; i++)
        {
            MapNode<MapData> branchHeadNode = GetRandomNextNode(startNode);

            if (branchHeadNode != null)
            {
                startNode.Children.Add(branchHeadNode);
                GenerateChildrenRecursive(branchHeadNode, 1);
            }
        }
        ConnectLeafNodesToDestinations(startNode);
    }

    private void GetRandomDestinationNode()
    {
        if (destinationMapData.Count > 0)
        {
            for (int i = 0; i < currentMapDifficulty.destinationCount; i++)
            {
                MapData rndDestinationMapData = destinationMapData[GetRandomIndexFromList(destinationMapData.Count)];
                var destNode = new MapNode<MapData>(rndDestinationMapData);
                destinationNodes.Add(destNode);
                allNodes.Add(destNode);
            }
        }
    }

    private void GetRandomSpawnPointNode()
    {
        if (spawnPointMapData.Count > 0)
        {
            MapData rndSpawnPointMapData = spawnPointMapData[GetRandomIndexFromList(spawnPointMapData.Count)];
            startNode = new MapNode<MapData>(rndSpawnPointMapData);
            allNodes.Add(startNode);
        }
    }

    private MapNode<MapData> GetRandomNextNode(MapNode<MapData> _currentNode)
    {
        if (_currentNode.Data.mapType == MapType.Destinaton) return null;
        if (_currentNode.Children.Count >= currentMapDifficulty.maxChildPerNodes) return null;

        MapData nextMapData = GetRandomMapData(_currentNode.Data.mapType);

        if (nextMapData != null)
        {
            var newNode = new MapNode<MapData>(nextMapData);
            allNodes.Add(newNode);
            return newNode;
        }
        return null;
    }

    private MapData GetRandomMapData(MapType parentType)
    {
        if (!childGenerationRules.TryGetValue(parentType, out List<MapType> allowedChildTypes)) return null;

        List<(MapType type, int chance)> mapChances = new List<(MapType, int)>
        {
            (MapType.Blank, currentMapDifficulty.blankMapChance),
            (MapType.Treasure, currentMapDifficulty.treasureMapChance),
            (MapType.Danger, currentMapDifficulty.dangerMapChance),
            (MapType.Mystery, currentMapDifficulty.mysteryMapChance)
        };

        List<(MapType type, int chance)> validChances = new List<(MapType, int)>();
        int totalChance = 0;

        foreach (var item in mapChances)
        {
            if (allowedChildTypes.Contains(item.type) && item.chance > 0)
            {
                validChances.Add(item);
                totalChance += item.chance;
            }
        }

        if (totalChance == 0) return null;

        int randomPoint = Random.Range(0, totalChance);
        MapType selectedMapType = MapType.Blank;
        int currentChance = 0;

        foreach (var item in validChances)
        {
            currentChance += item.chance;
            if (randomPoint < currentChance)
            {
                selectedMapType = item.type;
                break;
            }
        }

        List<MapData> mapDataList = selectedMapType switch
        {
            MapType.Blank => blankMapData,
            MapType.Treasure => treasureMapData,
            MapType.Danger => dangerMapData,
            MapType.Mystery => mysteryMapData,
            _ => null
        };

        if (mapDataList != null && mapDataList.Count > 0)
        {
            return mapDataList[GetRandomIndexFromList(mapDataList.Count)];
        }

        return null;
    }

    void GenerateChildrenRecursive(MapNode<MapData> parentNode, int currentDepth)
    {
        if (currentDepth >= currentMapDifficulty.nodeCountToDestination) return;
        int childCount = Random.Range(1, currentMapDifficulty.maxChildPerNodes + 1);

        for (int i = 0; i < childCount; i++)
        {
            MapNode<MapData> nextNode = GetRandomNextNode(parentNode);

            if (nextNode != null)
            {
                parentNode.Children.Add(nextNode);
                GenerateChildrenRecursive(nextNode, currentDepth + 1);
            }
        }
    }

    private void ConnectLeafNodesToDestinations(MapNode<MapData> root)
    {
        if (destinationNodes.Count == 0) return;

        for (int i = 0; i < root.Children.Count; i++)
        {
            MapNode<MapData> branch = root.Children[i];
            MapNode<MapData> destination = destinationNodes[i % destinationNodes.Count];
            List<MapNode<MapData>> leafNodes = FindLeafNodesInBranch(branch, currentMapDifficulty.nodeCountToDestination);

            foreach (var leaf in leafNodes)
            {
                if (!leaf.Children.Contains(destination)) leaf.Children.Add(destination);
            }
        }
    }

    private List<MapNode<MapData>> FindLeafNodesInBranch(MapNode<MapData> branchHead, int maxDepth)
    {
        List<MapNode<MapData>> leaves = new List<MapNode<MapData>>();

        Stack<(MapNode<MapData> node, int depth)> stack = new Stack<(MapNode<MapData> node, int depth)>();
        stack.Push((branchHead, 1));

        while (stack.Count > 0)
        {
            var (current, depth) = stack.Pop();
            if (current.Children.Count == 0 || depth == maxDepth)
            {
                leaves.Add(current);
            }
            if (depth < maxDepth)
            {
                foreach (var child in current.Children)
                {
                    stack.Push((child, depth + 1));
                }
            }
        }
        return leaves;
    }

    [ContextMenu("[Debug] Reset Map")]
    void ResetAllNodes()
    {
        startNode = null;
        destinationNodes = new List<MapNode<MapData>>();
        allNodes.Clear();
    }

    int GetRandomIndexFromList(int listCount)
    {
        return Random.Range(0, listCount);
    }
}