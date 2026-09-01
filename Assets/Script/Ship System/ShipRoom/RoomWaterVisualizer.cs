using Bundos.WaterSystem;
using UnityEngine;

public class RoomWaterVisualizer : MonoBehaviour
{
    [SerializeField] private RoomMarker roomMarker;
    [SerializeField] private Water waterMesh;
    [SerializeField] private BoxCollider roomCollider;
    [SerializeField] private float lerpSpeed = 5f;

    private static System.Collections.Generic.List<RoomWaterVisualizer> allVisualizers = new System.Collections.Generic.List<RoomWaterVisualizer>();

    public RoomMarker RoomMarker => roomMarker;
    public Bundos.WaterSystem.Water WaterMesh => waterMesh;
    public BoxCollider RoomCollider => roomCollider;

    public void TriggerRipple(Vector3 contactPoint, bool sink)
    {
        if (waterMesh != null)
        {
            waterMesh.Ripple(contactPoint, sink);
        }
    }

    private void OnEnable()
    {
        if (!allVisualizers.Contains(this)) allVisualizers.Add(this);
    }

    private void OnDisable()
    {
        allVisualizers.Remove(this);
    }

    /// <summary>
    /// Returns the water level Y at world position, or float.MinValue if not in water bounds.
    /// </summary>
    public static bool TryGetWaterSurfaceY(Vector3 worldPos, out float surfaceY, out RoomWaterVisualizer visualizer)
    {
        surfaceY = float.MinValue;
        visualizer = null;

        for (int i = 0; i < allVisualizers.Count; i++)
        {
            var vis = allVisualizers[i];
            if (vis == null || vis.roomMarker == null || vis.roomCollider == null) continue;

            float waterRatio = vis.roomMarker.currentWater.Value / 100f;
            if (waterRatio <= 0f) continue;

            Bounds b = vis.roomCollider.bounds;
            // Check XZ within room bounds (expand slightly for smooth detection)
            if (worldPos.x >= b.min.x - 0.2f && worldPos.x <= b.max.x + 0.2f &&
                worldPos.z >= b.min.z - 1.0f && worldPos.z <= b.max.z + 1.0f)
            {
                float calculatedSurfaceY = b.min.y + (b.size.y * waterRatio);
                if (worldPos.y <= calculatedSurfaceY + 0.5f && worldPos.y >= b.min.y - 1.0f)
                {
                    surfaceY = calculatedSurfaceY;
                    visualizer = vis;
                    return true;
                }
            }
        }
        return false;
    }

    private void Start()
    {
        if (roomCollider == null)
        {
            Debug.LogError($"{name}: roomCollider (BoxCollider) is not assigned. Water level will not render.", this);
        }

        if (waterMesh == null) return;

        waterMesh.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (roomMarker == null || waterMesh == null || roomCollider == null) return;

        float waterRatio = roomMarker.currentWater.Value / 100f;

        waterMesh.gameObject.SetActive(waterRatio > 0f);
        if (waterRatio <= 0f) return;

        Bounds bounds = roomCollider.bounds;

        // Position 3D water mesh at the bottom-front-left of room collider bounds
        Vector3 targetPosition = new Vector3(
            bounds.min.x,
            bounds.min.y,
            bounds.min.z
        );
        waterMesh.transform.position = targetPosition;

        // Calculate 3D scale based on room bounds (Width X, Water Height Y, Depth Z)
        int n = waterMesh.numSprings;
        float meshLocalWidth = Mathf.Max(n - 1, 1f);
        float meshLocalDepth = Mathf.Max(waterMesh.depth, 0.001f);
        float targetWorldHeight = bounds.size.y * waterRatio;

        Vector3 parentScale = waterMesh.transform.parent != null
            ? waterMesh.transform.parent.lossyScale
            : Vector3.one;

        Vector3 targetLocalScale = new Vector3(
            bounds.size.x / (meshLocalWidth * parentScale.x),
            targetWorldHeight / parentScale.y,
            bounds.size.z / (meshLocalDepth * parentScale.z)
        );

        waterMesh.transform.localScale = Vector3.Lerp(
            waterMesh.transform.localScale,
            targetLocalScale,
            Time.deltaTime * lerpSpeed
        );
    }
}