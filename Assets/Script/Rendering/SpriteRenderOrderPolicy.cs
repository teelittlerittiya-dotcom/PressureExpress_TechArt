using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Runtime source of truth for the 2.5D visual depth contract.
/// Physics/network roots remain on the gameplay plane; only visual owners move on Z.
/// </summary>
public static class SpriteRenderOrderPolicy
{
    public const float PlayerVisualZ = 0f;
    public const float CargoVisualZ = -0.05f;
    public const float HandVisualZ = -0.15f;
    // Eyeballs sit just in front of the rendered eye socket, but must never enter
    // the cargo depth band. The camera looks toward increasing Z from negative Z.
    public const float PlayerEyeballClosestSurfaceOffset = -0.01f;
    public const float PlayerEyeballFarthestSurfaceOffset = -0.003f;
    public const float PlayerEyeballCargoClearance = 0.005f;
    public const int VisualOwnerSortOrder = 0;
    public const int HandFallbackSortOrder = 100;

    private const string PlayerSortingLayer = "Player";
    private const string CargoSortingLayer = "Cargo";
    private const string HandSortingLayer = "Hand";

    public static void ApplyPlayer(Transform physicsRoot)
    {
        if (!Application.isPlaying || physicsRoot == null) return;

        SortingGroup group = GetOrAddSortingGroup(physicsRoot.gameObject);
        group.sortingLayerName = PlayerSortingLayer;
        group.sortingOrder = VisualOwnerSortOrder;

        // Player currently has no authored VisualRoot. Keep its network root on the plane and
        // make the existing child-local Z values the player's depth band.
        ApplyChildRendererLayer(physicsRoot, PlayerSortingLayer);
    }

    public static void ApplyCargo(Transform visualRoot, SpriteRenderer primaryRenderer)
    {
        if (!Application.isPlaying || visualRoot == null) return;

        Vector3 localPosition = visualRoot.localPosition;
        localPosition.z = CargoVisualZ;
        visualRoot.localPosition = localPosition;

        SortingGroup group = GetOrAddSortingGroup(visualRoot.gameObject);
        group.sortingLayerName = CargoSortingLayer;
        group.sortingOrder = VisualOwnerSortOrder;

        if (primaryRenderer != null)
        {
            primaryRenderer.sortingLayerName = CargoSortingLayer;
        }
    }

    public static void ApplyHand(Transform handRoot, SpriteRenderer handRenderer)
    {
        if (!Application.isPlaying || handRoot == null) return;

        if (handRenderer == null)
        {
            handRenderer = handRoot.GetComponentInChildren<SpriteRenderer>(true);
        }

        if (handRenderer != null)
        {
            Vector3 localPosition = handRenderer.transform.localPosition;
            localPosition.z = HandVisualZ;
            handRenderer.transform.localPosition = localPosition;
        }

        string layer = HasSortingLayer(HandSortingLayer) ? HandSortingLayer : CargoSortingLayer;
        SortingGroup group = GetOrAddSortingGroup(handRoot.gameObject);
        group.sortingLayerName = layer;
        group.sortingOrder = HasSortingLayer(HandSortingLayer)
            ? VisualOwnerSortOrder
            : HandFallbackSortOrder;

        if (handRenderer != null)
        {
            // The project does not have a Hand layer yet. Cargo is the closest gameplay layer,
            // while the high order keeps the hand above cargo until the dedicated layer is added.
            handRenderer.sortingLayerName = layer;
            if (layer == CargoSortingLayer)
            {
                handRenderer.sortingOrder = HandFallbackSortOrder;
            }
        }
    }

    public static float ClampPlayerEyeballWorldZ(float requestedWorldZ, float playerRootWorldZ)
    {
        float closestAllowedWorldZ = playerRootWorldZ + CargoVisualZ + PlayerEyeballCargoClearance;
        return Mathf.Max(requestedWorldZ, closestAllowedWorldZ);
    }

    private static SortingGroup GetOrAddSortingGroup(GameObject owner)
    {
        SortingGroup group = owner.GetComponent<SortingGroup>();
        return group != null ? group : owner.AddComponent<SortingGroup>();
    }

    private static void ApplyChildRendererLayer(Transform root, string layer)
    {
        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer renderer in renderers)
        {
            renderer.sortingLayerName = layer;
        }
    }

    private static bool HasSortingLayer(string layerName)
    {
        return Array.Exists(SortingLayer.layers, layer => layer.name == layerName);
    }
}
