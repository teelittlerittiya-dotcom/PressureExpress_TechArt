using MoreMountains.Feedbacks;
using Unity.Netcode;
using UnityEngine;

public class SubmarineCollision : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private LayerMask roomLayer;
    [SerializeField] private MMF_Player hitFB;
    [SerializeField] private float collisionCooldown = 0.8f;

    private float lastCollisionTime = -999f;
    private MapNetworkMovement cachedMapMovement;

    private void Awake()
    {
        if (roomLayer.value == 0)
        {
            int layer = LayerMask.NameToLayer("Room");
            if (layer != -1) roomLayer = 1 << layer;
            else roomLayer = ~0;
        }

        if (hitFB == null)
        {
            hitFB = GetComponentInChildren<MMF_Player>();
        }

        cachedMapMovement = Object.FindFirstObjectByType<MapNetworkMovement>(FindObjectsInactive.Include);
    }

    private bool IsObstacle(GameObject obj)
    {
        if (obj == null) return false;
        if (obj.CompareTag("MapObstacle")) return true;
        int obstacleLayer = LayerMask.NameToLayer("MapObstacle");
        if (obstacleLayer != -1 && obj.layer == obstacleLayer) return true;
        if (obj.name.StartsWith("Stone_Cluster") || obj.name.StartsWith("Layer Stone")) return true;
        return false;
    }

    private MapNetworkMovement GetMapMovement(GameObject hitObject)
    {
        if (cachedMapMovement == null)
        {
            cachedMapMovement = hitObject.GetComponentInParent<MapNetworkMovement>();
            if (cachedMapMovement == null)
            {
                cachedMapMovement = Object.FindFirstObjectByType<MapNetworkMovement>(FindObjectsInactive.Include);
            }
        }
        return cachedMapMovement;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!NetworkHelper.HasServerAuthority) return;

        if (IsObstacle(collision.gameObject))
        {
            if (collision.contactCount == 0) return;

            ContactPoint contact = collision.GetContact(0);
            Vector3 impactPoint = contact.point;
            Vector3 impactNormal = contact.normal;

            MapNetworkMovement mapMovement = GetMapMovement(collision.gameObject);
            if (mapMovement != null)
            {
                mapMovement.OnSubmarineHitObstacle(impactNormal);
            }

            ProcessObstacleImpact(impactPoint, impactNormal);
        }
    }

    public void ProcessObstacleImpact(Vector3 impactPoint, Vector3 impactNormal)
    {
        if (!NetworkHelper.HasServerAuthority) return;

        if (Time.time - lastCollisionTime < collisionCooldown) return;
        lastCollisionTime = Time.time;

        if (hitFB == null) hitFB = GetComponentInChildren<MMF_Player>();
        if (hitFB != null) hitFB.PlayFeedbacks();

        if (roomLayer.value == 0)
        {
            int layer = LayerMask.NameToLayer("Room");
            if (layer != -1) roomLayer = 1 << layer;
            else roomLayer = ~0;
        }

        Collider[] hitColliders = Physics.OverlapSphere(impactPoint, 12f, roomLayer, QueryTriggerInteraction.Collide);
        RoomMarker nearestRoom = null;
        float nearestSqrDistance = float.MaxValue;

        foreach (Collider roomCollider in hitColliders)
        {
            RoomMarker hitRoom = roomCollider.GetComponent<RoomMarker>();
            if (hitRoom == null) hitRoom = roomCollider.GetComponentInParent<RoomMarker>();
            if (hitRoom == null) continue;

            float sqrDistance = roomCollider.bounds.SqrDistance(impactPoint);
            if (sqrDistance < nearestSqrDistance)
            {
                nearestSqrDistance = sqrDistance;
                nearestRoom = hitRoom;
            }
        }

        if (nearestRoom == null && SubmarineManager.Instance != null && SubmarineManager.Instance.allRooms != null)
        {
            foreach (var room in SubmarineManager.Instance.allRooms)
            {
                if (room == null) continue;
                float sqrDist = (room.transform.position - impactPoint).sqrMagnitude;
                if (sqrDist < nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDist;
                    nearestRoom = room;
                }
            }
        }

        if (nearestRoom != null)
        {
            nearestRoom.SpawnLeak(impactPoint);
        }
        else
        {
            Debug.LogWarning($"[SubmarineCollision] No RoomMarker found near impact point {impactPoint} to spawn leak!");
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!NetworkHelper.HasServerAuthority) return;

        if (IsObstacle(collision.gameObject) && collision.contactCount > 0)
        {
            ContactPoint contact = collision.GetContact(0);
            MapNetworkMovement mapMovement = GetMapMovement(collision.gameObject);
            if (mapMovement != null)
            {
                mapMovement.OnSubmarineStayObstacle(contact.normal);
            }
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (!NetworkHelper.HasServerAuthority) return;

        if (IsObstacle(collision.gameObject))
        {
            MapNetworkMovement mapMovement = GetMapMovement(collision.gameObject);
            if (mapMovement != null)
            {
                mapMovement.OnSubmarineExitObstacle();
            }
        }
    }
}