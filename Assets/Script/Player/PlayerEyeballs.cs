using UnityEngine;
using Unity.Netcode;

[DefaultExecutionOrder(10001)]
public class PlayerEyeballs : NetworkBehaviour
{
    [Header("Eye Movement Constraint")]
    public Transform centerTransform; 
    public float maxRadius = 0.1f;

    [Header("Eye Z Offset")]
    [Tooltip("Z offset relative to the eye center. It is clamped to the player's visual depth band.")]
    public float zOffsetInFront = -0.1f;

    private Transform playerRoot;
    private CargoGrabController grabController;

    private void Awake()
    {
        CharacterController2D player = GetComponentInParent<CharacterController2D>();
        playerRoot = player != null ? player.transform : transform.root;
        grabController = player != null
            ? player.GetComponent<CargoGrabController>()
            : GetComponentInParent<CargoGrabController>();
    }

    private void LateUpdate()
    {
        if (centerTransform == null) return;

        // EyePos is a movement pivot, not the rendered eye surface. Measuring depth
        // from it pushed the pupils through cargo. Use its parent (Sprite-Eye) as the
        // surface, then clamp the final world Z behind the cargo depth boundary.
        Transform eyeSurface = centerTransform.parent != null
            ? centerTransform.parent
            : centerTransform;
        float safeSurfaceOffset = Mathf.Clamp(
            zOffsetInFront,
            SpriteRenderOrderPolicy.PlayerEyeballClosestSurfaceOffset,
            SpriteRenderOrderPolicy.PlayerEyeballFarthestSurfaceOffset);
        float requestedWorldZ = eyeSurface.position.z + safeSurfaceOffset;
        float playerRootWorldZ = playerRoot != null ? playerRoot.position.z : 0f;
        float targetZ = SpriteRenderOrderPolicy.ClampPlayerEyeballWorldZ(
            requestedWorldZ,
            playerRootWorldZ);

        Vector2 offset = Vector2.zero;
        PlayerHand visualHand = grabController != null ? grabController.RegisteredHand : null;
        if (visualHand != null)
        {
            // PlayerHand is the rendered gameplay cursor. Its owner drives the free
            // hand and its ClientNetworkTransform/held-cargo pose supplies the same
            // visual target to peers, so the pupils cannot diverge from the hand.
            Vector3 direction = visualHand.transform.position - centerTransform.position;
            direction.z = 0f;
            offset = Vector2.ClampMagnitude((Vector2)direction, Mathf.Max(0f, maxRadius));
        }

        Vector3 targetPos = centerTransform.position + new Vector3(offset.x, offset.y, 0f);
        targetPos.z = targetZ;
        transform.position = targetPos;
    }
}
