using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds intentional 3D collision geometry from a Sprite's 2D physics shape.
/// Concave polygons are ear-clipped into convex triangular prisms so a dynamic
/// Rigidbody can retain the authored outline instead of receiving one convex hull.
/// </summary>
[DisallowMultipleComponent]
public sealed class CargoColliderBuilder : MonoBehaviour
{
    [SerializeField] private SpriteRenderer sourceRenderer;
    [SerializeField] private Transform generatedColliderRoot;
    [SerializeField] private BoxCollider proximityTrigger;

    private readonly List<Mesh> runtimeMeshes = new List<Mesh>();
    private readonly List<Collider> generatedColliders = new List<Collider>();

    public int GeneratedColliderCount => generatedColliders.Count;
    public Transform GeneratedColliderRoot => generatedColliderRoot;
    public BoxCollider ProximityTrigger => proximityTrigger;

    public void ConfigureReferences(
        SpriteRenderer renderer,
        Transform colliderRoot,
        BoxCollider proximity)
    {
        sourceRenderer = renderer;
        generatedColliderRoot = colliderRoot;
        proximityTrigger = proximity;
    }

    public bool Rebuild(CargoItemData data)
    {
        ClearGenerated();

        if (data == null)
        {
            Debug.LogError($"{name}: cannot build Cargo collider without CargoItemData.", this);
            return false;
        }

        if (sourceRenderer == null || generatedColliderRoot == null)
        {
            Debug.LogError($"{name}: CargoColliderBuilder references are incomplete.", this);
            return false;
        }

        Sprite sprite = sourceRenderer.sprite;
        if (sprite == null)
        {
            Debug.LogError($"{name}: cannot build Cargo collider without a Sprite.", this);
            return false;
        }

        bool success = data.autoSizeColliderFromSprite
            ? BuildFromSpritePhysicsShape(sprite, data)
            : BuildManualBox(data);

        if (!success)
        {
            Debug.LogError(
                $"{name}: failed to build configured 3D collider for '{data.cargoName}'. " +
                "Fix the Sprite Physics Shape or use the explicit collider size override.", this);
            return false;
        }

        ConfigureProximity(data);
        return true;
    }

    public Bounds GetWorldBounds()
    {
        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        bool hasBounds = false;

        foreach (Collider collider in generatedColliders)
        {
            if (collider == null || !collider.enabled) continue;
            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return bounds;
    }

    private bool BuildFromSpritePhysicsShape(Sprite sprite, CargoItemData data)
    {
        int shapeCount = sprite.GetPhysicsShapeCount();
        if (shapeCount <= 0) return false;

        int triangleCount = 0;
        Bounds localBounds = new Bounds(Vector3.zero, Vector3.zero);
        bool hasBounds = false;
        List<Vector2> shape = new List<Vector2>(32);

        for (int shapeIndex = 0; shapeIndex < shapeCount; shapeIndex++)
        {
            shape.Clear();
            sprite.GetPhysicsShape(shapeIndex, shape);
            RemoveDuplicateClosingPoint(shape);
            if (shape.Count < 3) continue;

            List<Vector2> rootPoints = new List<Vector2>(shape.Count);
            foreach (Vector2 point in shape)
            {
                Vector3 worldPoint = sourceRenderer.transform.TransformPoint(new Vector3(point.x, point.y, 0f));
                Vector3 rootPoint = transform.InverseTransformPoint(worldPoint);
                rootPoint.x += data.colliderOffset.x;
                rootPoint.y += data.colliderOffset.y;
                rootPoints.Add(new Vector2(rootPoint.x, rootPoint.y));

                if (!hasBounds)
                {
                    localBounds = new Bounds(rootPoint, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    localBounds.Encapsulate(rootPoint);
                }
            }

            List<Triangle2D> triangles = Triangulate(rootPoints);
            if (triangles.Count == 0) return false;

            triangleCount += triangles.Count;
            if (triangleCount > data.maxGeneratedColliderTriangles)
            {
                Debug.LogError(
                    $"{name}: Sprite Physics Shape requires {triangleCount} collider triangles, " +
                    $"above the configured limit {data.maxGeneratedColliderTriangles}.", this);
                return false;
            }

            foreach (Triangle2D triangle in triangles)
            {
                CreateTrianglePrism(triangle, data.colliderDepth, data.physicsMaterial, generatedColliders.Count);
            }
        }

        return generatedColliders.Count > 0 && hasBounds;
    }

    private bool BuildManualBox(CargoItemData data)
    {
        if (data.colliderSizeOverride.x <= 0f || data.colliderSizeOverride.y <= 0f) return false;

        GameObject child = CreateColliderChild("ManualBoxCollider");
        BoxCollider box = child.AddComponent<BoxCollider>();
        box.center = new Vector3(data.colliderOffset.x, data.colliderOffset.y, 0f);
        box.size = new Vector3(data.colliderSizeOverride.x, data.colliderSizeOverride.y, data.colliderDepth);
        box.sharedMaterial = data.physicsMaterial;
        generatedColliders.Add(box);
        return true;
    }

    private void CreateTrianglePrism(Triangle2D triangle, float depth, PhysicsMaterial material, int index)
    {
        float halfDepth = Mathf.Max(0.025f, depth * 0.5f);
        Vector3[] vertices =
        {
            new Vector3(triangle.A.x, triangle.A.y, -halfDepth),
            new Vector3(triangle.B.x, triangle.B.y, -halfDepth),
            new Vector3(triangle.C.x, triangle.C.y, -halfDepth),
            new Vector3(triangle.A.x, triangle.A.y, halfDepth),
            new Vector3(triangle.B.x, triangle.B.y, halfDepth),
            new Vector3(triangle.C.x, triangle.C.y, halfDepth)
        };

        int[] indices =
        {
            0, 2, 1,
            3, 4, 5,
            0, 1, 4, 0, 4, 3,
            1, 2, 5, 1, 5, 4,
            2, 0, 3, 2, 3, 5
        };

        Mesh mesh = new Mesh
        {
            name = $"{name}_CargoCollider_{index:00}"
        };
        mesh.vertices = vertices;
        mesh.triangles = indices;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        runtimeMeshes.Add(mesh);

        GameObject child = CreateColliderChild($"Collider_{index:00}");
        MeshCollider collider = child.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
        collider.convex = true;
        collider.isTrigger = false;
        collider.sharedMaterial = material;
        generatedColliders.Add(collider);
    }

    private GameObject CreateColliderChild(string childName)
    {
        GameObject child = new GameObject(childName);
        child.layer = gameObject.layer;
        child.transform.SetParent(generatedColliderRoot, false);
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;
        return child;
    }

    private void ConfigureProximity(CargoItemData data)
    {
        if (proximityTrigger == null) return;

        Bounds localBounds = CalculateGeneratedLocalBounds();
        float padding = Mathf.Max(0f, data.proximityPadding);
        proximityTrigger.center = localBounds.center;
        proximityTrigger.size = new Vector3(
            Mathf.Max(0.1f, localBounds.size.x + padding * 2f),
            Mathf.Max(0.1f, localBounds.size.y + padding * 2f),
            Mathf.Max(data.colliderDepth, localBounds.size.z) + padding * 2f);
        proximityTrigger.isTrigger = true;
    }

    private Bounds CalculateGeneratedLocalBounds()
    {
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool hasBounds = false;

        foreach (Collider collider in generatedColliders)
        {
            if (collider == null) continue;
            Bounds worldBounds = collider.bounds;
            Vector3 localCenter = transform.InverseTransformPoint(worldBounds.center);
            Vector3 localSize = transform.InverseTransformVector(worldBounds.size);
            localSize = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
            Bounds current = new Bounds(localCenter, localSize);
            if (!hasBounds)
            {
                bounds = current;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(current.min);
                bounds.Encapsulate(current.max);
            }
        }

        return bounds;
    }

    private void ClearGenerated()
    {
        generatedColliders.Clear();

        if (generatedColliderRoot != null)
        {
            for (int i = generatedColliderRoot.childCount - 1; i >= 0; i--)
            {
                GameObject child = generatedColliderRoot.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }

        foreach (Mesh mesh in runtimeMeshes)
        {
            if (mesh == null) continue;
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
        }
        runtimeMeshes.Clear();
    }

    private void OnDestroy()
    {
        foreach (Mesh mesh in runtimeMeshes)
        {
            if (mesh != null) Destroy(mesh);
        }
        runtimeMeshes.Clear();
    }

    private static void RemoveDuplicateClosingPoint(List<Vector2> points)
    {
        if (points.Count > 2 && (points[0] - points[points.Count - 1]).sqrMagnitude < 0.000001f)
        {
            points.RemoveAt(points.Count - 1);
        }
    }

    private static List<Triangle2D> Triangulate(List<Vector2> input)
    {
        List<Triangle2D> result = new List<Triangle2D>();
        if (input == null || input.Count < 3) return result;

        List<Vector2> points = new List<Vector2>(input);
        if (SignedArea(points) < 0f) points.Reverse();

        List<int> remaining = new List<int>(points.Count);
        for (int i = 0; i < points.Count; i++) remaining.Add(i);

        int guard = points.Count * points.Count;
        while (remaining.Count > 3 && guard-- > 0)
        {
            bool clipped = false;
            for (int i = 0; i < remaining.Count; i++)
            {
                int previousIndex = remaining[(i - 1 + remaining.Count) % remaining.Count];
                int currentIndex = remaining[i];
                int nextIndex = remaining[(i + 1) % remaining.Count];
                Vector2 a = points[previousIndex];
                Vector2 b = points[currentIndex];
                Vector2 c = points[nextIndex];

                if (Cross(b - a, c - b) <= 0.000001f) continue;

                bool containsPoint = false;
                for (int p = 0; p < remaining.Count; p++)
                {
                    int testIndex = remaining[p];
                    if (testIndex == previousIndex || testIndex == currentIndex || testIndex == nextIndex) continue;
                    if (PointInTriangle(points[testIndex], a, b, c))
                    {
                        containsPoint = true;
                        break;
                    }
                }

                if (containsPoint) continue;
                result.Add(new Triangle2D(a, b, c));
                remaining.RemoveAt(i);
                clipped = true;
                break;
            }

            if (!clipped) return new List<Triangle2D>();
        }

        if (remaining.Count == 3)
        {
            result.Add(new Triangle2D(points[remaining[0]], points[remaining[1]], points[remaining[2]]));
        }

        return result;
    }

    private static float SignedArea(List<Vector2> points)
    {
        float area = 0f;
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 a = points[i];
            Vector2 b = points[(i + 1) % points.Count];
            area += a.x * b.y - b.x * a.y;
        }
        return area * 0.5f;
    }

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float c1 = Cross(b - a, p - a);
        float c2 = Cross(c - b, p - b);
        float c3 = Cross(a - c, p - c);
        const float epsilon = 0.000001f;
        return c1 >= -epsilon && c2 >= -epsilon && c3 >= -epsilon;
    }

    private readonly struct Triangle2D
    {
        public readonly Vector2 A;
        public readonly Vector2 B;
        public readonly Vector2 C;

        public Triangle2D(Vector2 a, Vector2 b, Vector2 c)
        {
            A = a;
            B = b;
            C = c;
        }
    }
}
