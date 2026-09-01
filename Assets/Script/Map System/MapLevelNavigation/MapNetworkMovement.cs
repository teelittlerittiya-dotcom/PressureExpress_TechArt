using System.Linq;
using Unity.Netcode;
using UnityEngine;

using PressureExpress.Framework;

[RequireComponent(typeof(NetworkObject))]
public class MapNetworkMovement : NetworkBehaviour, IFixedUpdateable
{
    [Header("Movement Settings")]
    [SerializeField] private Vector2 maxSpeed = new Vector2(10f, 10f);
    [SerializeField] private Vector2 acceleration = new Vector2(5f, 5f);
    [SerializeField] private Vector2 deceleration = new Vector2(2f, 2f);

    [Header("Buoyancy (การลอย/จม)")]
    [SerializeField] private float verticalSpeedMultiplier = 0.2f; 

    private Vector2 currentVelocity;
    private Vector2 currentInputVector;

    private float mapTopLocalY;
    private Transform playerSubmarine;
    private bool isDepthSystemReady = false;
    private Collider[] submarineSolidColliders;

    private void Awake()
    {
        // A Rigidbody on the Map root causes child non-convex MeshColliders to fail
        // PhysX collision detection against the Submarine Rigidbody.
        // Destroying the Rigidbody makes the child MeshColliders Static, which reliably collides with Dynamic Rigidbodies.
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            Destroy(rb);
        }
    }

    public void InitializeDepthSystem(float topY, Transform submarine)
    {
        mapTopLocalY = topY;
        playerSubmarine = submarine;
        isDepthSystemReady = true;
        CacheSubmarineColliders();
    }

    private void CacheSubmarineColliders()
    {
        if (playerSubmarine == null)
        {
            var sub = Object.FindFirstObjectByType<SubmarineCollision>(FindObjectsInactive.Include);
            if (sub != null) playerSubmarine = sub.transform;
        }

        if (playerSubmarine != null && (submarineSolidColliders == null || submarineSolidColliders.Length == 0))
        {
            submarineSolidColliders = playerSubmarine.GetComponentsInChildren<Collider>()
                .Where(c => c != null && !c.isTrigger)
                .ToArray();
        }
    }

    public void ResetMovement()
    {
        currentVelocity = Vector2.zero;
        currentInputVector = Vector2.zero;
    }

    private void Start()
    {
        if (NetworkHelper.IsOffline)
        {
            if (UpdateManager.Instance != null)
            {
                UpdateManager.Instance.RegisterFixedUpdateable(this);
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            UpdateManager.Instance.RegisterFixedUpdateable(this);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (UpdateManager.Instance != null)
        {
            UpdateManager.Instance.UnregisterFixedUpdateable(this);
        }
    }

    public override void OnDestroy()
    {
        if (UpdateManager.Instance != null)
        {
            UpdateManager.Instance.UnregisterFixedUpdateable(this);
        }
        base.OnDestroy();
    }

    public void OnFixedUpdate()
    {
        if (!NetworkHelper.HasServerAuthority) return;

        bool hasFuel = MachineManager.Instance != null && MachineManager.Instance.CanShipMove();

        Vector2 targetVelocity = Vector2.zero;
        if (hasFuel)
        {
            targetVelocity.x = currentInputVector.x * -1 * maxSpeed.x;
        }
        if (SubmarineManager.Instance != null)
        {
            float ballastWater = SubmarineManager.Instance.GetBallastWaterLevel();
            float leakWater = SubmarineManager.Instance.GetLeakWaterLevel();

            float totalWeight = ballastWater + (leakWater * SubmarineManager.Instance.leakWeightMultiplier);

            float weightDiff = totalWeight - SubmarineManager.Instance.neutralWeight;

            targetVelocity.y = weightDiff * verticalSpeedMultiplier;
            targetVelocity.y = Mathf.Clamp(targetVelocity.y, -maxSpeed.y, maxSpeed.y);
        }

        if (Mathf.Abs(targetVelocity.x) > 0.01f)
            currentVelocity.x = Mathf.MoveTowards(currentVelocity.x, targetVelocity.x, acceleration.x * Time.fixedDeltaTime);
        else
            currentVelocity.x = Mathf.MoveTowards(currentVelocity.x, 0f, deceleration.x * Time.fixedDeltaTime);

        if (Mathf.Abs(targetVelocity.y) > 0.01f)
            currentVelocity.y = Mathf.MoveTowards(currentVelocity.y, targetVelocity.y, acceleration.y * Time.fixedDeltaTime);
        else
            currentVelocity.y = Mathf.MoveTowards(currentVelocity.y, 0f, deceleration.y * Time.fixedDeltaTime);

        // Prospective movement deltas
        float moveX = currentVelocity.x * Time.fixedDeltaTime;
        float moveY = currentVelocity.y * Time.fixedDeltaTime;

        bool hitX = false;
        bool hitY = false;
        Vector3 impactPt = Vector3.zero;

        // Test X axis movement (when map moves by +moveX, obstacle moves +moveX relative to submarine, so test submarine offset by -moveX)
        if (Mathf.Abs(moveX) > 0.0001f)
        {
            if (CheckSubmarineOverlapsObstacle(new Vector3(-moveX, 0f, 0f), out impactPt))
            {
                hitX = true;
                currentVelocity.x = 0f;
                moveX = 0f;
            }
        }

        // Test Y axis movement
        if (Mathf.Abs(moveY) > 0.0001f)
        {
            if (CheckSubmarineOverlapsObstacle(new Vector3(0f, -moveY, 0f), out impactPt))
            {
                hitY = true;
                currentVelocity.y = 0f;
                moveY = 0f;
            }
        }

        // Trigger impact feedback and water leak spawn when hit occurs
        if (hitX || hitY)
        {
            CacheSubmarineColliders();
            SubmarineCollision subCol = null;
            if (playerSubmarine != null) subCol = playerSubmarine.GetComponent<SubmarineCollision>();
            if (subCol == null) subCol = Object.FindFirstObjectByType<SubmarineCollision>();

            if (subCol != null)
            {
                float normX = hitX ? (currentInputVector.x > 0 || currentVelocity.x < 0 ? -1f : 1f) : 0f;
                float normY = hitY ? (targetVelocity.y > 0 || currentVelocity.y > 0 ? -1f : 1f) : 0f;
                Vector3 normal = new Vector3(normX, normY, 0f);
                subCol.ProcessObstacleImpact(impactPt, normal);
            }
        }

        float distanceMoved = Mathf.Abs(moveX);
        if (distanceMoved > 0.001f && MachineManager.Instance != null)
        {
            MachineManager.Instance.ProcessMovementConsumption(distanceMoved);
        }

        // Apply movement
        transform.position += new Vector3(moveX, moveY, 0f);

        if (isDepthSystemReady && SubmarineManager.Instance != null && playerSubmarine != null)
        {
            Vector3 subLocalPos = transform.InverseTransformPoint(playerSubmarine.position);
            float calculatedDepth = mapTopLocalY - subLocalPos.y;
            SubmarineManager.Instance.CurrentDepth = Mathf.Max(0f, calculatedDepth);
        }
    }

    private bool CheckSubmarineOverlapsObstacle(Vector3 shipTestOffset, out Vector3 impactPoint)
    {
        impactPoint = Vector3.zero;
        CacheSubmarineColliders();
        if (submarineSolidColliders == null || submarineSolidColliders.Length == 0) return false;

        int obstacleMask = LayerMask.GetMask("MapObstacle", "Default");
        if (obstacleMask == 0) obstacleMask = ~0;

        for (int i = 0; i < submarineSolidColliders.Length; i++)
        {
            var col = submarineSolidColliders[i];
            if (col == null || col.isTrigger) continue;

            Collider[] hits = null;

            if (col is BoxCollider box)
            {
                Vector3 center = box.transform.TransformPoint(box.center) + shipTestOffset;
                Vector3 lossy = box.transform.lossyScale;
                Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, new Vector3(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y), Mathf.Abs(lossy.z)));
                Quaternion rot = box.transform.rotation;

                hits = Physics.OverlapBox(center, halfExtents, rot, obstacleMask, QueryTriggerInteraction.Ignore);
            }
            else if (col is SphereCollider sphere)
            {
                Vector3 center = sphere.transform.TransformPoint(sphere.center) + shipTestOffset;
                Vector3 lossy = sphere.transform.lossyScale;
                float radius = sphere.radius * Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y), Mathf.Abs(lossy.z));

                hits = Physics.OverlapSphere(center, radius, obstacleMask, QueryTriggerInteraction.Ignore);
            }
            else if (col is CapsuleCollider capsule)
            {
                Vector3 center = capsule.transform.TransformPoint(capsule.center) + shipTestOffset;
                Vector3 lossy = capsule.transform.lossyScale;
                float radius = capsule.radius * Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.z));
                float height = Mathf.Max(0f, (capsule.height * Mathf.Abs(lossy.y) * 0.5f) - radius);
                Vector3 dir = capsule.transform.up;
                if (capsule.direction == 0) dir = capsule.transform.right;
                else if (capsule.direction == 2) dir = capsule.transform.forward;

                Vector3 point1 = center + dir * height;
                Vector3 point2 = center - dir * height;

                hits = Physics.OverlapCapsule(point1, point2, radius, obstacleMask, QueryTriggerInteraction.Ignore);
            }
            else
            {
                Vector3 center = col.bounds.center + shipTestOffset;
                hits = Physics.OverlapBox(center, col.bounds.extents, Quaternion.identity, obstacleMask, QueryTriggerInteraction.Ignore);
            }

            if (hits == null || hits.Length == 0) continue;

            for (int h = 0; h < hits.Length; h++)
            {
                var hit = hits[h];
                if (hit == null || hit.isTrigger) continue;
                if (hit.transform.IsChildOf(playerSubmarine)) continue;
                if (hit.CompareTag("Exit") || hit.name.StartsWith("Exit")) continue;

                if (hit.transform.IsChildOf(transform) || hit.CompareTag("MapObstacle") || hit.name.StartsWith("Stone") || hit.name.StartsWith("Layer"))
                {
                    Vector3 obstaclePoint = hit.bounds.ClosestPoint(col.bounds.center);
                    impactPoint = col.ClosestPoint(obstaclePoint);
                    return true;
                }
            }
        }
        return false;
    }

    public void OnSubmarineHitObstacle(Vector3 impactNormal, float bounceSpeed = 2.0f)
    {
        if (!NetworkHelper.HasServerAuthority) return;

        Vector2 normal2D = new Vector2(impactNormal.x, impactNormal.y).normalized;
        if (normal2D.sqrMagnitude < 0.001f) return;

        float projection = Vector2.Dot(currentVelocity, normal2D);
        if (projection > 0f)
        {
            currentVelocity -= normal2D * (projection + bounceSpeed);
        }
        else
        {
            currentVelocity -= normal2D * bounceSpeed;
        }

        Vector3 pushBack = new Vector3(normal2D.x, normal2D.y, 0f) * 0.15f;
        transform.position -= pushBack;
    }

    public void OnSubmarineStayObstacle(Vector3 impactNormal)
    {
        if (!NetworkHelper.HasServerAuthority) return;

        Vector2 normal2D = new Vector2(impactNormal.x, impactNormal.y).normalized;
        if (normal2D.sqrMagnitude < 0.001f) return;

        float projection = Vector2.Dot(currentVelocity, normal2D);
        if (projection > 0f)
        {
            currentVelocity -= normal2D * projection;
        }
    }

    public void OnSubmarineExitObstacle()
    {
    }

    [Rpc(SendTo.Server)]
    public void SubmitInputServerRpc(Vector2 input)
    {
        currentInputVector = input;
    }
}