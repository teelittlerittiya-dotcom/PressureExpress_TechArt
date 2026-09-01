using UnityEngine;

public class ExitPoint : MonoBehaviour
{
    public static bool isTransitioningNode = false;
    private bool hasTriggered = false;

    private int targetExitIndex;
    MapNode<MapData> mapNode;

    public static void ResetTransitionFlag()
    {
        isTransitioningNode = false;
    }

    public void setExitMapNode(int targetExitIndex, MapNode<MapData> node)
    {
        this.targetExitIndex = targetExitIndex;
        this.mapNode = node;
        this.hasTriggered = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered || isTransitioningNode) return;

        // Check if the collider or its root object belongs to the Player or Submarine
        bool isPlayerOrSub = other.CompareTag("Player") ||
                             other.transform.root.CompareTag("Player") ||
                             other.GetComponentInParent<SubmarineCollision>() != null ||
                             other.GetComponentInParent<SubmarineManager>() != null;

        if (isPlayerOrSub)
        {
            hasTriggered = true;
            isTransitioningNode = true;

            // Disable all colliders on this exit point immediately to prevent repeated triggers
            foreach (var col in GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
            }

            if (AnalyticManager.instance != null && mapNode != null && mapNode.Data != null)
            {
                AnalyticManager.instance.UpdateNode(mapNode.Data.mapType.ToString());
            }

            if (NavigationGameManager.instance != null)
            {
                NavigationGameManager.instance.MoveToNextNode(targetExitIndex);
            }
            else
            {
                var nav = Object.FindFirstObjectByType<NavigationGameManager>();
                if (nav != null)
                {
                    nav.MoveToNextNode(targetExitIndex);
                }
                else
                {
                    Debug.LogWarning("[ExitPoint] NavigationGameManager is null!");
                    isTransitioningNode = false;
                }
            }
        }
    }
}