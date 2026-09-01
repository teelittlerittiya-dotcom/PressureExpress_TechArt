using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using GiantGrey.TileWorldCreator;

public class MapGenerate : MonoBehaviour
{
    [Header("Tile World Creator Settings")]
    [SerializeField] private TileWorldCreatorManager twcManager;

    [Header("Prefabs & References")]
    [SerializeField] private GameObject exitPosPrefab;
    [SerializeField] private GameObject subMarine;
    [SerializeField] private Transform parentTransform;

    [Header("Race Condition Protection")]
    private bool isGenerating = false;
    public bool IsGenerating => isGenerating;
    private CancellationTokenSource generationCts;

    private Transform mapHolder;

    private static readonly Vector2[] CardinalDirections = new Vector2[]
    {
        Vector2.up, Vector2.down, Vector2.left, Vector2.right
    };

    private void Awake()
    {
        if (twcManager == null)
        {
            twcManager = FindAnyObjectByType<TileWorldCreatorManager>();
        }
    }

    private void OnDestroy()
    {
        generationCts?.Cancel();
        generationCts?.Dispose();
    }

    public void GenMap(int seed, MapNode<MapData> currentNode)
    {
        GenMapAsync(seed, currentNode, this.GetCancellationTokenOnDestroy()).Forget();
    }

    public async UniTask GenMapAsync(int seed, MapNode<MapData> currentNode, CancellationToken cancellationToken = default)
    {
        generationCts?.Cancel();
        generationCts?.Dispose();
        generationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, this.GetCancellationTokenOnDestroy());
        CancellationToken token = generationCts.Token;

        if (isGenerating)
        {
            await UniTask.WaitWhile(() => isGenerating, cancellationToken: token);
        }

        isGenerating = true;

        try
        {
            if (twcManager == null)
            {
                twcManager = FindAnyObjectByType<TileWorldCreatorManager>();
            }

            if (twcManager != null && twcManager.configuration != null)
            {
                await GenMapTileWorldCreatorAsync(seed, currentNode, token);
            }
            else
            {
                Debug.LogError("MapGenerate: TileWorldCreatorManager or Configuration is missing!");
            }
        }
        catch (System.OperationCanceledException)
        {
            Debug.Log("Map generation task was cancelled.");
        }
        finally
        {
            isGenerating = false;
        }
    }

    private async UniTask GenMapTileWorldCreatorAsync(int seed, MapNode<MapData> currentNode, CancellationToken token)
    {
        Configuration config = twcManager.configuration;
        if (config == null) return;

        twcManager.ResetConfiguration();

        mapHolder = (parentTransform != null) ? parentTransform : twcManager.transform;
        ClearMapHolderObjects();

        await UniTask.Yield(PlayerLoopTiming.Update, token);

        List<MapNode<MapData>> maps = currentNode?.Children;
        int exitCount = maps != null ? maps.Count : 0;

        ConfigureLayerSelectCount(config, "PointMarker", 1 + exitCount);
        ConfigureLayerSelectCount(config, "Start", 1);

        int currentSeed = seed;
        const int maxAttempts = 200;
        bool pathFound = false;

        Vector2 startCell = Vector2.zero;
        List<Vector2> exitCells = new List<Vector2>();

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            token.ThrowIfCancellationRequested();

            config.useGlobalRandomSeed = true;
            config.globalRandomSeed = currentSeed;

            twcManager.ExecuteBlueprintLayers();

            BlueprintLayer startLayer = config.GetBlueprintLayerByGuid(config.GetBlueprintLayerGuid("Start"));
            if (startLayer == null || startLayer.allPositions.Count == 0)
            {
                currentSeed++;
                continue;
            }

            startCell = startLayer.allPositions.First();

            string endGuid = config.GetBlueprintLayerGuid("End");
            if (string.IsNullOrEmpty(endGuid)) endGuid = config.GetBlueprintLayerGuid("Exit");

            BlueprintLayer endLayer = config.GetBlueprintLayerByGuid(endGuid);
            if (endLayer == null || endLayer.allPositions.Count < exitCount)
            {
                currentSeed++;
                continue;
            }

            exitCells = endLayer.allPositions.Take(exitCount).ToList();

            if (CanFindWay(startCell, exitCells, config))
            {
                pathFound = true;
                break;
            }

            currentSeed++;

            if (attempt % 10 == 0)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }

        if (!pathFound)
        {
            Debug.LogWarning("TileWorldCreator: No valid connected path found after max attempts. Proceeding with current seed.");
        }

        token.ThrowIfCancellationRequested();
        await UniTask.Yield(PlayerLoopTiming.Update, token);

        twcManager.ExecuteBuildLayers(ExecutionMode.FromScratch);

        await UniTask.Yield(PlayerLoopTiming.Update, token);

        if (twcManager.configuration.buildLayerFolders != null)
        {
            while (twcManager.configuration.buildLayerFolders.Any(f => f.buildLayers != null && f.buildLayers.Any(l => l != null && l.isEnabled && l.isExecuting)))
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }

        await UniTask.DelayFrame(15, cancellationToken: token);

        SetObstacleLayerAndTag(twcManager.gameObject);
        if (mapHolder != null && mapHolder.gameObject != twcManager.gameObject)
        {
            SetObstacleLayerAndTag(mapHolder.gameObject);
        }

        token.ThrowIfCancellationRequested();

        Transform targetMoveTransform = twcManager.transform;
        MapNetworkMovement movement = twcManager.GetComponentInParent<MapNetworkMovement>();
        if (movement != null)
        {
            movement.ResetMovement();
            targetMoveTransform = movement.transform;
        }
        else if (mapHolder != null && !mapHolder.IsChildOf(twcManager.transform))
        {
            targetMoveTransform = mapHolder;
        }

        float cellSize = config.cellSize;
        Vector3 startLocalInTwc = new Vector3(startCell.x * cellSize, 0f, startCell.y * cellSize);
        Vector3 startWorldPos = twcManager.transform.TransformPoint(startLocalInTwc);

        MoveStartPosToPlayer(targetMoveTransform, startWorldPos);

        float topOfMapY = config.height * cellSize;
        if (movement != null && subMarine != null)
        {
            movement.InitializeDepthSystem(topOfMapY, subMarine.transform);
        }

        SpawnExits(exitCells, maps, cellSize);

        ExitPoint.ResetTransitionFlag();

        await UniTask.Yield(PlayerLoopTiming.Update, token);
    }

    private void ConfigureLayerSelectCount(Configuration config, string layerName, int count)
    {
        string guid = config.GetBlueprintLayerGuid(layerName);
        if (string.IsNullOrEmpty(guid)) return;

        BlueprintLayer layer = config.GetBlueprintLayerByGuid(guid);
        if (layer?.tileMapModifiers == null) return;

        foreach (var modifier in layer.tileMapModifiers)
        {
            if (modifier is GiantGrey.TileWorldCreator.Select selectMod)
            {
                selectMod.selectionType = GiantGrey.TileWorldCreator.Select.SelectionType.Count;
                selectMod.count = count;
            }
        }
    }

    private void SpawnExits(List<Vector2> exitCells, List<MapNode<MapData>> maps, float cellSize)
    {
        if (exitPosPrefab == null || exitCells == null) return;

        for (int i = 0; i < exitCells.Count; i++)
        {
            Vector3 exitLocalInTwc = new Vector3(exitCells[i].x * cellSize, 0f, exitCells[i].y * cellSize);
            Vector3 exitWorldPos = twcManager.transform.TransformPoint(exitLocalInTwc);

            GameObject exitObj = Instantiate(exitPosPrefab, mapHolder);
            exitObj.transform.position = exitWorldPos;
            exitObj.transform.rotation = Quaternion.identity;
            exitObj.transform.localScale = Vector3.one;

            ExitPoint exitScript = exitObj.GetComponent<ExitPoint>();
            if (exitScript != null && maps != null && i < maps.Count && maps[i] != null)
            {
                exitScript.setExitMapNode(i, maps[i]);

                RadarWaypoint radarTarget = exitObj.GetComponent<RadarWaypoint>();
                if (radarTarget == null) radarTarget = exitObj.AddComponent<RadarWaypoint>();

                string label = (maps[i].Data != null) ? maps[i].Data.mapType.ToString() + " Exit" : "Exit";
                radarTarget.Setup(label);
            }
        }
    }

    private void MoveStartPosToPlayer(Transform targetTransform, Vector3 startWorldPos)
    {
        if (targetTransform == null || subMarine == null) return;
        Vector3 playerPos = subMarine.transform.position;
        Vector3 offset = playerPos - startWorldPos;
        targetTransform.position += offset;
    }

    private void ClearMapHolderObjects()
    {
        if (mapHolder != null)
        {
            for (int i = mapHolder.childCount - 1; i >= 0; i--)
            {
                GameObject child = mapHolder.GetChild(i).gameObject;
                Unity.Netcode.NetworkObject netObj = child.GetComponent<Unity.Netcode.NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                {
                    netObj.Despawn(true);
                }
                else
                {
                    if (Application.isPlaying) Destroy(child);
                    else DestroyImmediate(child);
                }
            }
        }

        var existingExits = Object.FindObjectsByType<ExitPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var exit in existingExits)
        {
            if (exit != null && exit.gameObject != null)
            {
                if (Application.isPlaying) Destroy(exit.gameObject);
                else DestroyImmediate(exit.gameObject);
            }
        }
    }

    private bool CanFindWay(Vector2 start, List<Vector2> exits, Configuration config)
    {
        if (exits == null || exits.Count == 0) return true;

        string pathGuid = config.GetBlueprintLayerGuid("ParthFinder");
        if (string.IsNullOrEmpty(pathGuid)) pathGuid = config.GetBlueprintLayerGuid("PathFinder");

        if (!string.IsNullOrEmpty(pathGuid))
        {
            BlueprintLayer pathLayer = config.GetBlueprintLayerByGuid(pathGuid);
            if (pathLayer != null && pathLayer.allPositions.Count > 0)
            {
                foreach (var exit in exits)
                {
                    if (!IsNearOrIn(exit, pathLayer.allPositions))
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        return true;
    }

    private bool IsNearOrIn(Vector2 pos, HashSet<Vector2> pathPositions)
    {
        if (pathPositions.Contains(pos)) return true;
        for (int i = 0; i < CardinalDirections.Length; i++)
        {
            if (pathPositions.Contains(pos + CardinalDirections[i])) return true;
        }
        return false;
    }

    private void SetObstacleLayerAndTag(GameObject root)
    {
        if (root == null) return;

        int layerIndex = LayerMask.NameToLayer("MapObstacle");
        if (layerIndex == -1) layerIndex = LayerMask.NameToLayer("mapObstacle");

        string targetTag = null;
        try
        {
            if (root.CompareTag("MapObstacle") || root.CompareTag("Untagged") || true)
            {
                targetTag = "MapObstacle";
            }
        }
        catch
        {
            try { targetTag = "mapObstacle"; } catch { targetTag = null; }
        }

        Transform[] allTransforms = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in allTransforms)
        {
            if (t.GetComponent<ExitPoint>() != null) continue;

            if (layerIndex != -1)
            {
                t.gameObject.layer = layerIndex;
            }

            if (!string.IsNullOrEmpty(targetTag))
            {
                try { t.gameObject.tag = targetTag; } catch { }
            }
        }
    }
}