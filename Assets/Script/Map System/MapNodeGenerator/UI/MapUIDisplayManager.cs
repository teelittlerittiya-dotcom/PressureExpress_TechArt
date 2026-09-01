using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using UnityEngine.Serialization;

public class MapUIDisplayManager : MonoBehaviour
{
    [FormerlySerializedAs("mapManager")] [Header("Ref")]
    public MapNodeManager mapNodeManager;
    public GameObject mapNodeSlotPrefab;
    public UIMapConnector mapConnector;
    
    private Dictionary<MapNode<MapData>, MapNodeSlotUI> nodeUITable = new Dictionary<MapNode<MapData>, MapNodeSlotUI>();
    
    [Header("UI Containers")]
    public Transform layersContainer;

    private void Awake()
    {
        if (mapNodeManager == null)
        {
            mapNodeManager = UnityEngine.Object.FindFirstObjectByType<MapNodeManager>(FindObjectsInactive.Include);
        }
    }

    private void OnEnable()
    {
        if (mapNodeManager == null)
        {
            mapNodeManager = UnityEngine.Object.FindFirstObjectByType<MapNodeManager>(FindObjectsInactive.Include);
        }

        if (mapNodeManager != null && mapNodeManager.startNode != null)
        {
            DisplayMap();
        }
    }

    [ContextMenu("Display Generated Map")]
    public void DisplayMap()
    {
        if (layersContainer == null) return;

        if (mapNodeManager == null || mapNodeManager.startNode == null)
        {
            Debug.LogWarning("[MapUIDisplayManager] MapNodeManager or startNode is missing.");
            return;
        }

        for (int i = layersContainer.childCount - 1; i >= 0; i--)
        {
            GameObject child = layersContainer.GetChild(i).gameObject;

            if (Application.isPlaying)
            {
                child.transform.SetParent(null);
                Destroy(child);
            }
            else DestroyImmediate(child);
        }

        nodeUITable.Clear();

        HashSet<MapNode<MapData>> displayedNodes = new HashSet<MapNode<MapData>>();
        Queue<(MapNode<MapData> node, int depth)> queue = new Queue<(MapNode<MapData> node, int depth)>();

        queue.Enqueue((mapNodeManager.startNode, 0));
        displayedNodes.Add(mapNodeManager.startNode);

        Dictionary<int, Transform> layerContainers = new Dictionary<int, Transform>();

        Transform lineRoot = layersContainer.parent.Find("ConnectionRoot");
        if (lineRoot == null)
        {
            lineRoot = new GameObject("ConnectionRoot").transform;
            lineRoot.SetParent(layersContainer.parent, false);
            lineRoot.gameObject.AddComponent<RectTransform>().SetAsLastSibling();
        }

        if (mapConnector != null)
            mapConnector.Initialize(lineRoot);

        while (queue.Count > 0)
        {
            var (currentNode, currentDepth) = queue.Dequeue();

            if (!layerContainers.ContainsKey(currentDepth))
            {
                GameObject newLayerGO = new GameObject($"MapLayer_{currentDepth}");
                newLayerGO.transform.SetParent(layersContainer);

                RectTransform rect = newLayerGO.AddComponent<RectTransform>();
                rect.localScale = Vector3.one;

                HorizontalLayoutGroup hlg = newLayerGO.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing = 50;
                hlg.childAlignment = TextAnchor.MiddleCenter;

                ContentSizeFitter csf = newLayerGO.AddComponent<ContentSizeFitter>();
                csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                layerContainers.Add(currentDepth, newLayerGO.transform);
            }

            Transform parentLayer = layerContainers[currentDepth];

            GameObject nodeSlotGO = Instantiate(mapNodeSlotPrefab, parentLayer);
            MapNodeSlotUI nodeUI = nodeSlotGO.GetComponent<MapNodeSlotUI>();

            if (nodeUI != null) nodeUI.Setup(currentNode);
            nodeUITable.Add(currentNode, nodeUI);

            foreach (var childNode in currentNode.Children)
            {
                if (!displayedNodes.Contains(childNode))
                {
                    queue.Enqueue((childNode, currentDepth + 1));
                    displayedNodes.Add(childNode);
                }
            }
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(layersContainer.GetComponent<RectTransform>());

        foreach (var layer in layerContainers.Values) 
            LayoutRebuilder.ForceRebuildLayoutImmediate(layer.GetComponent<RectTransform>());

        if (mapConnector != null)
        {
            mapConnector.ClearAndRedraw(nodeUITable);
            Debug.Log("displaying lines...");
        }

        Debug.Log("Map UI displayed");
    }
}