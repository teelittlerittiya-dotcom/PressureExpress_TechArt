using UnityEngine;

public class NavigationGameManager : MonoBehaviour
{
    public static NavigationGameManager instance;

    [Header("MapManagerRef")]
    [SerializeField] MapTestScript mapGen;
    [SerializeField] MapNodeManager mapNode;
    [Header("UI Reference")]
    [SerializeField] private GameObject demoEndUIPanel;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (mapGen == null) mapGen = FindAnyObjectByType<MapTestScript>();
        if (mapNode == null) mapNode = FindAnyObjectByType<MapNodeManager>();

        if (demoEndUIPanel == null)
        {
            var wonObj = GameObject.Find("[UI] game won");
            if (wonObj != null) demoEndUIPanel = wonObj;
        }
    }

    public void MoveToNextNode(int exitIndex)
    {
        if (PressureExpress.Tutorial.TutorialManager.Instance != null)
        {
            Debug.Log("[NavigationGameManager] MoveToNextNode ignored in Tutorial mode.");
            ExitPoint.ResetTransitionFlag();
            return;
        }

        ResolveReferences();

        var currentNode = mapNode != null ? mapNode.GetCurrentNode() : null;

        if (currentNode != null && currentNode.GetChildCount() > exitIndex)
        {
            if (mapGen != null)
            {
                mapGen.RequestMoveToNode(exitIndex);
            }
        }
        else
        {
            // If the current node has no children (i.e. we completed the final destination node), trigger the game won UI
            if (currentNode != null && currentNode.GetChildCount() == 0)
            {
                ShowDemoEndUI();
            }
            ExitPoint.ResetTransitionFlag();
        }
    }

    public void ShowDemoEndUI()
    {
        ResolveReferences();

        if (demoEndUIPanel != null)
        {
            demoEndUIPanel.SetActive(true);
        }

        if (AnalyticManager.instance != null)
        {
            AnalyticManager.instance.SendAllData();
        }
    }
}