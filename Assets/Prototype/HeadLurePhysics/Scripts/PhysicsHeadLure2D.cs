using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public sealed class PhysicsHeadLure2D : MonoBehaviour
{
    public enum HorizontalCurveDirection
    {
        Left = -1,
        Right = 1
    }

    [Header("Attachment")]
    [SerializeField] private Transform socket;
    [SerializeField] private SpriteRenderer sourceBulbRenderer;
    [SerializeField] private Light2D sourceLight;

    [Header("Bulb Visual Prefab")]
    [SerializeField] private HeadLureBulbVisual bulbVisualPrefab;

    [Header("String Length & Physics")]
    [SerializeField, Min(3)] private int segmentCount = 7;
    [SerializeField, Min(0.02f)] private float segmentLength = 0.075f;
    [SerializeField, Min(0.003f)] private float nodeRadius = 0.02f;
    [SerializeField, Min(0.001f)] private float segmentMass = 0.035f;
    [SerializeField, Range(0f, 2f)] private float gravityScale = 0.18f;

    [Header("Authored String Shape")]
    [SerializeField] private HorizontalCurveDirection curveDirection = HorizontalCurveDirection.Right;
    [SerializeField, Range(-180f, 180f)] private float startAngleDegrees = 90f;
    [SerializeField, Range(-180f, 180f)] private float endAngleDegrees = -38f;
    [Tooltip("Fraction of links that stay straight before the string begins curving.")]
    [SerializeField, Range(0f, 0.8f)] private float straightFraction = 0.3f;

    [Header("Soft Shape Springs")]
    [Tooltip("Acceleration toward the authored up-and-over curve. This is a force, not a transform override.")]
    [SerializeField, Min(0f)] private float shapeStiffness = 75f;
    [SerializeField, Min(0f)] private float shapeDamping = 8f;
    [SerializeField, Range(0.05f, 1f)] private float tipShapeWeight = 0.42f;
    [SerializeField, Min(0f)] private float maxShapeAcceleration = 120f;
    [Tooltip("Soft spring frequency between every second node. This helps retain the curve without making it rigid.")]
    [SerializeField, Min(0f)] private float braceFrequency = 3f;
    [SerializeField, Range(0f, 1f)] private float braceDampingRatio = 0.45f;

    [Header("String Visual")]
    [Tooltip("Optional shared material for the rope. Leave empty to use a generated Sprites/Default material.")]
    [SerializeField] private Material ropeMaterial;
    [SerializeField] private Color ropeColor = new Color(0.25f, 0.9f, 1f, 1f);
    [SerializeField, Min(0.01f)] private float lineWidth = 0.045f;
    [SerializeField] private float renderZOffset = -0.02f;

    private Transform simulationRoot;
    private Rigidbody2D anchorBody;
    private Rigidbody2D[] nodes;
    private DistanceJoint2D[] distanceJoints;
    private SpringJoint2D[] braceJoints;
    private Vector2[] restLocalPoints;
    private Vector2[] previousTargetPositions;
    private LineRenderer lineRenderer;
    private SpriteRenderer bulbRenderer;
    private HeadLureBulbVisual bulbVisualInstance;
    private Material generatedLineMaterial;
    private float facingSign = 1f;

    public bool IsInitialized => anchorBody != null && nodes != null && nodes.Length == segmentCount;
    public int LinkCount => nodes?.Length ?? 0;
    public Rigidbody2D BulbBody => IsInitialized ? nodes[nodes.Length - 1] : null;
    public float BulbSpeed => BulbBody == null ? 0f : BulbBody.linearVelocity.magnitude;
    public HeadLureBulbVisual BulbVisual => bulbVisualInstance;
    public bool IsFacingLeft => facingSign < 0f;
    public float RestLength => segmentCount * segmentLength;
    public float CurrentLineWidth => lineRenderer != null ? lineRenderer.startWidth : lineWidth;
    public Material RopeMaterial => ropeMaterial;
    public Material ActiveRopeMaterial => lineRenderer != null ? lineRenderer.sharedMaterial : ropeMaterial;

    public void Configure(Transform headSocket, SpriteRenderer oldBulbRenderer, Light2D oldLight)
    {
        socket = headSocket;
        sourceBulbRenderer = oldBulbRenderer;
        sourceLight = oldLight;
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            Initialize();
        }
    }

    private void FixedUpdate()
    {
        if (!IsInitialized || socket == null)
        {
            return;
        }

        UpdateFacingSign();
        Vector3 socketPosition3D = socket.position;
        anchorBody.MovePosition(new Vector2(socketPosition3D.x, socketPosition3D.y));

        float inverseDeltaTime = 1f / Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        for (int i = 0; i < nodes.Length; i++)
        {
            Vector2 targetPosition = GetWorldTarget(restLocalPoints[i + 1]);
            Vector2 targetVelocity = (targetPosition - previousTargetPositions[i]) * inverseDeltaTime;
            previousTargetPositions[i] = targetPosition;

            float normalizedIndex = nodes.Length <= 1 ? 0f : i / (nodes.Length - 1f);
            float shapeWeight = Mathf.Lerp(1f, tipShapeWeight, normalizedIndex);
            Vector2 positionError = targetPosition - nodes[i].position;
            Vector2 relativeVelocity = nodes[i].linearVelocity - targetVelocity;
            Vector2 acceleration = (positionError * shapeStiffness - relativeVelocity * shapeDamping) * shapeWeight;
            acceleration = Vector2.ClampMagnitude(acceleration, maxShapeAcceleration);

            nodes[i].AddForce(acceleration * nodes[i].mass, ForceMode2D.Force);
        }
    }

    private void LateUpdate()
    {
        if (IsInitialized)
        {
            UpdateFacingSign();
            UpdateVisual();
        }
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            TearDown();
        }
    }

    private void OnDestroy()
    {
        TearDown();
    }

    private void OnValidate()
    {
        segmentCount = Mathf.Max(3, segmentCount);
        segmentLength = Mathf.Max(0.02f, segmentLength);
        nodeRadius = Mathf.Clamp(nodeRadius, 0.003f, segmentLength * 0.4f);
        segmentMass = Mathf.Max(0.001f, segmentMass);
        lineWidth = Mathf.Max(0.001f, lineWidth);
    }

    public float GetMaximumJointGap()
    {
        if (!IsInitialized)
        {
            return float.PositiveInfinity;
        }

        float maximumError = 0f;
        Rigidbody2D previousBody = anchorBody;
        for (int i = 0; i < nodes.Length; i++)
        {
            float currentDistance = Vector2.Distance(previousBody.position, nodes[i].position);
            maximumError = Mathf.Max(maximumError, Mathf.Abs(currentDistance - distanceJoints[i].distance));
            previousBody = nodes[i];
        }

        return maximumError;
    }

    public float GetMaximumShapeError()
    {
        if (!IsInitialized || socket == null)
        {
            return float.PositiveInfinity;
        }

        float maximumError = 0f;
        for (int i = 0; i < nodes.Length; i++)
        {
            Vector2 targetPosition = GetWorldTarget(restLocalPoints[i + 1]);
            maximumError = Mathf.Max(maximumError, Vector2.Distance(nodes[i].position, targetPosition));
        }

        return maximumError;
    }

    private void Initialize()
    {
        if (IsInitialized || socket == null)
        {
            return;
        }

        GameObject rootObject = new GameObject($"[Physics Head Lure] {name}");
        simulationRoot = rootObject.transform;
        simulationRoot.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        UpdateFacingSign();
        float renderZ = socket.position.z + renderZOffset;
        anchorBody = CreateAnchor(renderZ);

        Vector2[] worldPoints = BuildInitialCurvePoints();
        nodes = new Rigidbody2D[segmentCount];
        distanceJoints = new DistanceJoint2D[segmentCount];
        braceJoints = new SpringJoint2D[Mathf.Max(0, segmentCount - 1)];
        previousTargetPositions = new Vector2[segmentCount];
        var colliders = new List<Collider2D>(segmentCount);

        Rigidbody2D previousBody = anchorBody;
        for (int i = 0; i < segmentCount; i++)
        {
            bool isBulb = i == segmentCount - 1;
            GameObject nodeObject = new GameObject(isBulb ? "Bulb Node" : $"Node {i + 1:00}");
            nodeObject.transform.SetParent(simulationRoot, false);
            nodeObject.transform.position = new Vector3(worldPoints[i + 1].x, worldPoints[i + 1].y, renderZ);

            Rigidbody2D body = nodeObject.AddComponent<Rigidbody2D>();
            ConfigureDynamicBody(body, isBulb ? segmentMass * 1.8f : segmentMass);
            body.position = worldPoints[i + 1];

            CircleCollider2D collider = nodeObject.AddComponent<CircleCollider2D>();
            float bulbRadius = bulbVisualPrefab != null
                ? Mathf.Max(nodeRadius, bulbVisualPrefab.PhysicsRadius)
                : nodeRadius * 2.2f;
            collider.radius = isBulb ? bulbRadius : nodeRadius;
            colliders.Add(collider);

            DistanceJoint2D distanceJoint = nodeObject.AddComponent<DistanceJoint2D>();
            distanceJoint.connectedBody = previousBody;
            distanceJoint.autoConfigureConnectedAnchor = false;
            distanceJoint.anchor = Vector2.zero;
            distanceJoint.connectedAnchor = Vector2.zero;
            distanceJoint.autoConfigureDistance = false;
            distanceJoint.distance = Vector2.Distance(worldPoints[i], worldPoints[i + 1]);
            distanceJoint.maxDistanceOnly = false;
            distanceJoint.enableCollision = false;

            nodes[i] = body;
            distanceJoints[i] = distanceJoint;
            previousTargetPositions[i] = worldPoints[i + 1];

            if (i >= 1)
            {
                Rigidbody2D braceBody = i == 1 ? anchorBody : nodes[i - 2];
                SpringJoint2D brace = nodeObject.AddComponent<SpringJoint2D>();
                brace.connectedBody = braceBody;
                brace.autoConfigureConnectedAnchor = false;
                brace.anchor = Vector2.zero;
                brace.connectedAnchor = Vector2.zero;
                brace.autoConfigureDistance = false;
                brace.distance = Vector2.Distance(worldPoints[i - 1], worldPoints[i + 1]);
                brace.frequency = braceFrequency;
                brace.dampingRatio = braceDampingRatio;
                brace.enableCollision = false;
                braceJoints[i - 1] = brace;
            }

            previousBody = body;
        }

        IgnoreSelfCollisions(colliders);
        CreateVisual(renderZ);
        HideSourceVisual();
        UpdateVisual();

        Debug.Log($"Physics head lure initialized with {segmentCount} Rigidbody2D nodes on {name}.", this);
    }

    private Rigidbody2D CreateAnchor(float renderZ)
    {
        GameObject anchorObject = new GameObject("Head Socket Anchor");
        anchorObject.transform.SetParent(simulationRoot, false);
        Vector3 socketPosition = socket.position;
        anchorObject.transform.position = new Vector3(socketPosition.x, socketPosition.y, renderZ);

        Rigidbody2D body = anchorObject.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.position = new Vector2(socketPosition.x, socketPosition.y);
        return body;
    }

    private void ConfigureDynamicBody(Rigidbody2D body, float mass)
    {
        body.bodyType = RigidbodyType2D.Dynamic;
        body.mass = mass;
        body.gravityScale = gravityScale;
        body.linearDamping = 0.35f;
        body.angularDamping = 1f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.sleepMode = RigidbodySleepMode2D.NeverSleep;
    }

    private Vector2[] BuildInitialCurvePoints()
    {
        restLocalPoints = new Vector2[segmentCount + 1];
        Vector2[] worldPoints = new Vector2[segmentCount + 1];
        worldPoints[0] = socket.position;

        int straightLinks = Mathf.Clamp(
            Mathf.RoundToInt(segmentCount * straightFraction),
            1,
            segmentCount - 1);
        Vector2 localPoint = Vector2.zero;
        float horizontalSign = (float)curveDirection;

        for (int i = 0; i < segmentCount; i++)
        {
            float bendT = i < straightLinks
                ? 0f
                : Mathf.InverseLerp(straightLinks - 1f, segmentCount - 1f, i);
            bendT = bendT * bendT * (3f - 2f * bendT);

            float directionAngle = Mathf.Lerp(startAngleDegrees, endAngleDegrees, bendT) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(directionAngle) * horizontalSign, Mathf.Sin(directionAngle));
            localPoint += direction * segmentLength;
            restLocalPoints[i + 1] = localPoint;

            worldPoints[i + 1] = GetWorldTarget(localPoint);
        }

        return worldPoints;
    }

    private Vector2 GetWorldTarget(Vector2 localOffset)
    {
        // The authored sprite hierarchy is intentionally scaled. The physics link length is
        // specified in world units, so inherit socket rotation/mirroring but not sprite scale.
        Vector3 rotatedOffset = socket.TransformDirection(new Vector3(localOffset.x, localOffset.y, 0f));
        Vector3 worldPosition = socket.position + rotatedOffset;
        return new Vector2(worldPosition.x, worldPosition.y);
    }

    private void CreateVisual(float renderZ)
    {
        GameObject lineObject = new GameObject("Rope Line");
        lineObject.transform.SetParent(simulationRoot, false);
        lineObject.transform.position = new Vector3(0f, 0f, renderZ);
        lineRenderer = lineObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = segmentCount + 1;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.startColor = ropeColor;
        lineRenderer.endColor = ropeColor;
        lineRenderer.numCapVertices = 0;
        lineRenderer.numCornerVertices = 0;
        lineRenderer.textureMode = LineTextureMode.Stretch;

        if (ropeMaterial != null)
        {
            lineRenderer.sharedMaterial = ropeMaterial;
        }
        else
        {
            Shader shader = Shader.Find("Sprites/Default")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Hidden/Internal-Colored");
            generatedLineMaterial = new Material(shader) { name = "Head Lure Line (Runtime)" };
            lineRenderer.sharedMaterial = generatedLineMaterial;
        }

        if (bulbVisualPrefab == null)
        {
            Debug.LogError("Physics head lure needs a HeadLureBulbVisual prefab.", this);
            return;
        }

        bulbVisualInstance = Instantiate(bulbVisualPrefab, BulbBody.transform);
        bulbVisualInstance.name = bulbVisualPrefab.name;
        bulbVisualInstance.transform.localPosition = Vector3.zero;
        bulbVisualInstance.transform.localRotation = Quaternion.identity;
        bulbVisualInstance.ApplyConfiguration();
        bulbVisualInstance.SetHorizontalMirror(IsFacingLeft);

        bulbRenderer = bulbVisualInstance.Renderer;
        if (bulbRenderer != null)
        {
            lineRenderer.sortingLayerID = bulbRenderer.sortingLayerID;
            lineRenderer.sortingOrder = bulbRenderer.sortingOrder - 1;
        }
    }

    private void UpdateVisual()
    {
        if (lineRenderer == null || nodes == null)
        {
            return;
        }

        float renderZ = socket.position.z + renderZOffset;
        Vector2 anchorPosition = anchorBody.position;
        lineRenderer.SetPosition(0, new Vector3(anchorPosition.x, anchorPosition.y, renderZ));

        for (int i = 0; i < nodes.Length; i++)
        {
            Vector2 nodePosition = nodes[i].position;
            lineRenderer.SetPosition(i + 1, new Vector3(nodePosition.x, nodePosition.y, renderZ));
        }

        if (bulbVisualInstance != null)
        {
            bulbVisualInstance.SetHorizontalMirror(IsFacingLeft);
        }
    }

    private void UpdateFacingSign()
    {
        if (socket == null)
        {
            return;
        }

        float horizontalProjection = socket.TransformDirection(Vector3.right).x;
        if (Mathf.Abs(horizontalProjection) > 0.05f)
        {
            facingSign = Mathf.Sign(horizontalProjection);
        }
    }

    private static void IgnoreSelfCollisions(IReadOnlyList<Collider2D> colliders)
    {
        for (int i = 0; i < colliders.Count; i++)
        {
            for (int j = i + 1; j < colliders.Count; j++)
            {
                Physics2D.IgnoreCollision(colliders[i], colliders[j], true);
            }
        }
    }

    private void HideSourceVisual()
    {
        if (sourceBulbRenderer != null)
        {
            sourceBulbRenderer.enabled = false;
        }

        if (sourceLight != null)
        {
            sourceLight.enabled = false;
        }
    }

    private void RestoreSourceVisual()
    {
        if (sourceBulbRenderer != null)
        {
            sourceBulbRenderer.enabled = true;
        }

        if (sourceLight != null)
        {
            sourceLight.enabled = true;
        }
    }

    private void TearDown()
    {
        RestoreSourceVisual();

        if (simulationRoot != null)
        {
            if (Application.isPlaying)
            {
                Destroy(simulationRoot.gameObject);
            }
            else
            {
                DestroyImmediate(simulationRoot.gameObject);
            }
        }

        DestroyGeneratedObject(generatedLineMaterial);

        simulationRoot = null;
        anchorBody = null;
        nodes = null;
        distanceJoints = null;
        braceJoints = null;
        restLocalPoints = null;
        previousTargetPositions = null;
        lineRenderer = null;
        bulbRenderer = null;
        bulbVisualInstance = null;
        generatedLineMaterial = null;
        facingSign = 1f;
    }

    private static void DestroyGeneratedObject(Object generatedObject)
    {
        if (generatedObject == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(generatedObject);
        }
        else
        {
            DestroyImmediate(generatedObject);
        }
    }
}
