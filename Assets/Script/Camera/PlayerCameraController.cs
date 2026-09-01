using UnityEngine;

using PressureExpress.Framework;

/// <summary>
/// 2.5D follow camera for the local player.
///
/// Replaces CameraFollowWithMouseZone: there is no zoom and no mouse influence any more.
/// The camera tracks the player and swings around them on the Y axis toward the direction
/// they are travelling, so the 3D environment shows parallax while the sprites stay side-on.
/// The swing scales with speed, so walking gives a small lean and running gives a big one.
///
/// Every number below is exposed in the Inspector. Set <see cref="enableRotation"/> to false
/// to get a plain follow camera with no swing at all.
/// </summary>
public class PlayerCameraController : MonoBehaviour, ILateUpdateable
{
    [Header("Target")]
    [Tooltip("The player to follow. Assigned automatically by the local player on spawn - " +
             "leave this empty in the scene.")]
    public Transform player;

    [Header("Follow")]
    [Tooltip("Camera position relative to the player.")]
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 4.5f, -10.05f);

    [Tooltip("How quickly the camera catches up to the player. Higher = agile and snappier, lower = smoother lag.")]
    [SerializeField] private float followSpeed = 2f;

    [Tooltip("Track the player's Y exactly instead of easing into it.")]
    [SerializeField] private bool lockVerticalToOffset = true;

    [Header("Rotation")]
    [Tooltip("Turn the whole swing-on-move behaviour off and just follow.")]
    [SerializeField] private bool enableRotation = true;

    [Tooltip("Constant downward tilt in degrees. Positive looks down at the player.")]
    [SerializeField] private float basePitch = 11.5f;

    [Tooltip("Degrees the camera swings around the player at full walking speed.")]
    [SerializeField] private float walkYaw = 8f;

    [Tooltip("Degrees the camera swings around the player at full running speed.")]
    [SerializeField] private float runYaw = 16f;

    [Tooltip("Player speed (units/sec) treated as a full-tilt walk. Should match " +
             "CharacterController2D.moveSpeed.")]
    [SerializeField] private float walkSpeedReference = 5f;

    [Tooltip("Player speed (units/sec) treated as a full-tilt run. Should match " +
             "moveSpeed + runSpeedBonus.")]
    [SerializeField] private float runSpeedReference = 9f;

    [Tooltip("Movement slower than this counts as standing still, so the camera does not " +
             "twitch from physics jitter or a slow nudge of the stick.")]
    [SerializeField] private float speedDeadZone = 0.15f;

    [Tooltip("Degrees per second the swing is allowed to change.")]
    [SerializeField] private float yawSpeed = 20.4f;

    [Tooltip("Swing away from the direction of travel instead of into it (invert panning direction to watch the way ahead).")]
    [SerializeField] private bool invertYaw = false;

    [Tooltip("If enabled, camera rotates in only 1 direction regardless of movement direction.")]
    [SerializeField] private bool rotateOneDirectionOnly = false;

    [Tooltip("If enabled, camera only rotates when moving in positive direction (Right).")]
    [SerializeField] private bool rotateOnPositiveMoveOnly = false;

    [Header("Room Camera Override")]
    [Tooltip("How fast the camera transitions between custom room offsets and default framing.")]
    [SerializeField] private float roomTransitionSpeed = 3.5f;

    [HideInInspector] public bool isControlledExternally = false;
    private Transform trackedPlayer;
    private Rigidbody playerBody;
    private float lastPlayerX;
    private float currentYaw;
    private Vector3 currentDynamicOffset;
    private float currentDynamicPitch;
    private bool snapNextFrame;
    private bool registeredWithManager;

    private void Start()
    {
        currentDynamicOffset = followOffset;
        currentDynamicPitch = basePitch;
        if (player != null) SetTarget(player);
    }

    private void LateUpdate()
    {
        if (!enabled || isControlledExternally) return;
        UpdateCamera();
    }

    /// <summary>
    /// Point the camera at a player and jump straight to the framing, skipping the follow
    /// easing. Call this on spawn so the camera does not sweep across the level.
    /// </summary>
    public void SetTarget(Transform target)
    {
        player = target;
        trackedPlayer = target;
        playerBody = target != null ? target.GetComponent<Rigidbody>() : null;
        lastPlayerX = target != null ? target.position.x : 0f;
        currentYaw = 0f;
        currentDynamicOffset = followOffset;
        currentDynamicPitch = basePitch;
        if (!isControlledExternally)
        {
            snapNextFrame = true;
        }
    }

    /// <summary>
    /// Immediately apply the same framing used by the follow camera.
    /// This is useful when another system has temporarily controlled the camera
    /// and needs to hand control back without a one-frame jump or room-offset blend.
    /// </summary>
    public void SnapToTarget(Transform target)
    {
        if (target == null) return;

        player = target;
        trackedPlayer = target;
        playerBody = target.GetComponent<Rigidbody>();
        lastPlayerX = target.position.x;
        currentYaw = 0f;

        Vector3 targetOffset = followOffset;
        float targetPitch = basePitch;

        if (RoomCameraOverride.TryGetOverrideForPosition(target.position, out RoomCameraOverride roomOverride))
        {
            targetOffset = roomOverride.roomFollowOffset;
            if (roomOverride.overridePitch) targetPitch = roomOverride.roomPitch;
        }

        currentDynamicOffset = targetOffset;
        currentDynamicPitch = targetPitch;

        Vector3 desired = target.position + targetOffset;
        if (lockVerticalToOffset) desired.y = target.position.y + targetOffset.y;

        transform.position = desired;
        transform.rotation = Quaternion.Euler(targetPitch, 0f, 0f);
        snapNextFrame = false;
    }

    private void TryFindPlayer()
    {
        if (Unity.Netcode.NetworkManager.Singleton != null && 
            Unity.Netcode.NetworkManager.Singleton.LocalClient != null && 
            Unity.Netcode.NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            SetTarget(Unity.Netcode.NetworkManager.Singleton.LocalClient.PlayerObject.transform);
            return;
        }

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            SetTarget(playerObj.transform);
        }
    }

    public void OnLateUpdate()
    {
        if (!enabled || isControlledExternally) return;
        UpdateCamera();
    }

    private void UpdateCamera()
    {
        if (!enabled || isControlledExternally) return;

        if (player == null)
        {
            TryFindPlayer();
            if (player == null) return;
        }

        if (player != trackedPlayer) SetTarget(player);

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // Check if player is inside a room with a custom camera override
        Vector3 targetOffset = followOffset;
        float targetPitch = basePitch;

        if (RoomCameraOverride.TryGetOverrideForPosition(player.position, out RoomCameraOverride roomOverride))
        {
            targetOffset = roomOverride.roomFollowOffset;
            if (roomOverride.overridePitch) targetPitch = roomOverride.roomPitch;
        }

        currentDynamicOffset = Vector3.Lerp(currentDynamicOffset, targetOffset, dt * roomTransitionSpeed);
        currentDynamicPitch = Mathf.Lerp(currentDynamicPitch, targetPitch, dt * roomTransitionSpeed);

        currentYaw = Mathf.MoveTowardsAngle(currentYaw, ComputeTargetYaw(dt), yawSpeed * dt);

        Quaternion yawRotation = Quaternion.Euler(0f, currentYaw, 0f);
        Vector3 desired = player.position + yawRotation * currentDynamicOffset;
        if (lockVerticalToOffset) desired.y = player.position.y + currentDynamicOffset.y;

        // Smooth frame-rate independent follow for X, Y, and Z
        transform.position = snapNextFrame
            ? desired
            : Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-followSpeed * dt));

        transform.rotation = Quaternion.Euler(currentDynamicPitch, currentYaw, 0f);
        snapNextFrame = false;
    }

    private float ComputeTargetYaw(float dt)
    {
        float signedSpeed = ReadHorizontalSpeed(dt);
        if (!enableRotation) return 0f;

        float speed = Mathf.Abs(signedSpeed);
        if (speed <= speedDeadZone) return 0f;

        // If configured to only rotate when moving right/positive horizontal direction:
        if (rotateOnPositiveMoveOnly && signedSpeed < 0f) return 0f;

        // Two-stage ramp so walking and running look visibly different, rather than one linear
        // blend that makes a walk read as a slow run.
        float amount = speed <= walkSpeedReference
            ? Mathf.Lerp(0f, walkYaw, Mathf.InverseLerp(speedDeadZone, walkSpeedReference, speed))
            : Mathf.Lerp(walkYaw, runYaw, Mathf.InverseLerp(walkSpeedReference, runSpeedReference, speed));

        float directionMultiplier = rotateOneDirectionOnly ? 1f : Mathf.Sign(signedSpeed);
        return amount * directionMultiplier * (invertYaw ? -1f : 1f);
    }

    private float ReadHorizontalSpeed(float dt)
    {
        // Always advance the position history, even when the Rigidbody is the source, so the
        // fallback does not report a huge delta the first frame the body turns kinematic.
        float positionDelta = (player.position.x - lastPlayerX) / dt;
        lastPlayerX = player.position.x;

        // A kinematic body is a remote player driven by the network; its velocity stays zero,
        // so the position delta is the only honest reading there.
        if (playerBody != null && !playerBody.isKinematic) return playerBody.linearVelocity.x;
        return positionDelta;
    }
}
