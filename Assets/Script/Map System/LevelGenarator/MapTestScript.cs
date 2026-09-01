using UnityEngine;
using Unity.Netcode;

public class MapTestScript : NetworkBehaviour
{
    [SerializeField] private MapGenerate mapGen;
    [SerializeField] private MapNodeManager mapNodeManager;

    private readonly NetworkVariable<int> currentGridSeed = new NetworkVariable<int>(0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    [SerializeField] private int numOfExit = 2;
    public int NumOfExit => numOfExit;

    private int lastGeneratedGridSeed = 0;

    private void Awake()
    {
        if (mapGen == null) mapGen = FindAnyObjectByType<MapGenerate>();
        if (mapNodeManager == null) mapNodeManager = FindAnyObjectByType<MapNodeManager>();
    }

    private void Update()
    {
        if (!IsServer && lastGeneratedGridSeed == 0)
        {
            TryGenerateMap();
        }
    }

    public override void OnNetworkSpawn()
    {
        currentGridSeed.OnValueChanged += OnNetworkDataChanged;
        if (mapNodeManager != null)
        {
            mapNodeManager.currentNodeIndex.OnValueChanged += OnNetworkDataChanged;
            mapNodeManager.mapTreeSeed.OnValueChanged += OnNetworkDataChanged;

            if (IsServer)
            {
                mapNodeManager.mapTreeSeed.Value = Random.Range(1, 100000);
                mapNodeManager.GenerateNewMap(mapNodeManager.mapTreeSeed.Value);

                mapNodeManager.currentNodeIndex.Value = 0;
                currentGridSeed.Value = Random.Range(1, 100000);
                TryGenerateMap();
            }
            else
            {
                TryGenerateMap();
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        currentGridSeed.OnValueChanged -= OnNetworkDataChanged;
        if (mapNodeManager != null)
        {
            mapNodeManager.currentNodeIndex.OnValueChanged -= OnNetworkDataChanged;
            mapNodeManager.mapTreeSeed.OnValueChanged -= OnNetworkDataChanged;
        }
    }

    private void OnNetworkDataChanged(int oldVal, int newVal)
    {
        TryGenerateMap();
    }

    private void TryGenerateMap()
    {
        if (PressureExpress.Tutorial.TutorialManager.Instance != null)
        {
            Debug.Log("[MapTestScript] Map generation skipped in Tutorial mode.");
            return;
        }

        if (mapNodeManager == null || mapGen == null) return;
        if (mapNodeManager.mapTreeSeed.Value == 0) return;
        if (currentGridSeed.Value == 0) return;
        if (mapNodeManager.currentNodeIndex.Value < 0) return;
        if (lastGeneratedGridSeed == currentGridSeed.Value) return;

        if (mapNodeManager.allNodes.Count == 0)
        {
            mapNodeManager.GenerateNewMap(mapNodeManager.mapTreeSeed.Value);
        }

        MapNode<MapData> node = mapNodeManager.GetCurrentNode();
        if (node != null)
        {
            mapGen.GenMap(currentGridSeed.Value, node);
            lastGeneratedGridSeed = currentGridSeed.Value;
        }
    }

    [Rpc(SendTo.Server)]
    public void CreateMapServerRpc()
    {
        currentGridSeed.Value = Random.Range(1, 100000);
    }

    public void RequestMoveToNode(int childIndex)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            RequestMoveToNodeServerRpc(childIndex);
        }
        else
        {
            MoveToNodeInternal(childIndex);
        }
    }

    [Rpc(SendTo.Server)]
    public void RequestMoveToNodeServerRpc(int childIndex)
    {
        MoveToNodeInternal(childIndex);
    }

    private void MoveToNodeInternal(int childIndex)
    {
        if (mapNodeManager == null) mapNodeManager = FindAnyObjectByType<MapNodeManager>();
        if (mapNodeManager == null) return;

        MapNode<MapData> currentNode = mapNodeManager.GetCurrentNode();
        if (currentNode != null && currentNode.Children.Count > childIndex)
        {
            MapNode<MapData> nextNode = currentNode.Children[childIndex];
            int newIndex = mapNodeManager.allNodes.IndexOf(nextNode);

            if (newIndex != -1)
            {
                mapNodeManager.currentNodeIndex.Value = newIndex;
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    CreateMapServerRpc();
                }
                else
                {
                    currentGridSeed.Value = Random.Range(1, 100000);
                    TryGenerateMap();
                }
            }
        }
    }
}