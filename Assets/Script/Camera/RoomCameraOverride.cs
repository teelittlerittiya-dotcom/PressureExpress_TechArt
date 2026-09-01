using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach to room GameObjects (or RoomMarker GameObjects). Allows per-room camera offset & pitch overrides.
/// When local player enters this room's bounds, PlayerCameraController smoothly blends to this room's offset.
/// When leaving, the camera smoothly blends back to default camera setup offset.
/// </summary>
public class RoomCameraOverride : MonoBehaviour
{
    private static readonly List<RoomCameraOverride> allOverrides = new List<RoomCameraOverride>();

    [Header("Room Camera Settings")]
    [Tooltip("Enable camera offset override for this room.")]
    public bool enableOverride = true;

    [Tooltip("Custom camera offset when player is inside this room.")]
    public Vector3 roomFollowOffset = new Vector3(0f, 1.5f, -5.0f);

    [Tooltip("Optionally override camera pitch angle for this room.")]
    public bool overridePitch = false;

    [Tooltip("Custom camera pitch angle in degrees.")]
    public float roomPitch = 11f;

    private Collider roomCollider;

    private void OnEnable()
    {
        allOverrides.Add(this);
        roomCollider = GetComponent<Collider>();
    }

    private void OnDisable()
    {
        allOverrides.Remove(this);
    }

    /// <summary>
    /// Finds the active RoomCameraOverride for a world position.
    /// </summary>
    public static bool TryGetOverrideForPosition(Vector3 worldPos, out RoomCameraOverride result)
    {
        for (int i = 0; i < allOverrides.Count; i++)
        {
            var ov = allOverrides[i];
            if (ov == null || !ov.enableOverride) continue;

            if (ov.roomCollider != null && ov.roomCollider.bounds.Contains(worldPos))
            {
                result = ov;
                return true;
            }
        }

        result = null;
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1.0f, 0.3f);
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
        }
    }
}
