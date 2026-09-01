using UnityEngine;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class PrototypePlayerDriver3D : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 4f;
    [SerializeField, Min(0f)] private float jumpSpeed = 5.5f;
    [SerializeField, Min(0f)] private float climbSpeed = 3f;

    [Header("Checks")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundMask = 1 << 6;
    [SerializeField] private LayerMask ladderMask = 1 << 7;
    [SerializeField, Min(0.01f)] private float groundCheckRadius = 0.14f;
    [SerializeField, Min(0.01f)] private float ladderCheckRadius = 0.35f;

    [Header("Visual")]
    [SerializeField] private Transform visualRoot;

    private Rigidbody body;
    private Quaternion visualRootBaseRotation;
    private Vector3 spawnPosition;
    private float horizontalInput;
    private float verticalInput;
    private bool jumpQueued;
    private float facing = 1f;

    public bool IsGrounded { get; private set; }
    public bool IsOnLadder { get; private set; }
    public bool IsClimbing { get; private set; }

    public void Configure(Transform playerVisualRoot, Transform playerGroundCheck)
    {
        visualRoot = playerVisualRoot;
        groundCheck = playerGroundCheck;
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.useGravity = true;
        body.isKinematic = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.Continuous;
        body.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;

        spawnPosition = transform.position;
        if (visualRoot != null)
        {
            visualRootBaseRotation = visualRoot.localRotation;
        }
    }

    private void Update()
    {
        horizontalInput = 0f;
        verticalInput = 0f;

        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) horizontalInput -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) horizontalInput += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) verticalInput -= 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) verticalInput += 1f;
        jumpQueued |= Input.GetKeyDown(KeyCode.Space);

        if (Mathf.Abs(horizontalInput) > 0.01f)
        {
            facing = Mathf.Sign(horizontalInput);
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetToSpawn();
        }
    }

    private void FixedUpdate()
    {
        Vector3 groundPosition = groundCheck != null
            ? groundCheck.position
            : transform.position + Vector3.down * 0.58f;

        IsGrounded = Physics.CheckSphere(
            groundPosition,
            groundCheckRadius,
            groundMask,
            QueryTriggerInteraction.Ignore);

        IsOnLadder = Physics.CheckSphere(
            transform.position + Vector3.up * 0.1f,
            ladderCheckRadius,
            ladderMask,
            QueryTriggerInteraction.Collide);

        if (IsOnLadder && Mathf.Abs(verticalInput) > 0.05f)
        {
            IsClimbing = true;
        }
        else if (!IsOnLadder || Mathf.Abs(horizontalInput) > 0.65f)
        {
            IsClimbing = false;
        }

        Vector3 velocity = body.linearVelocity;
        if (IsClimbing)
        {
            body.useGravity = false;
            velocity.x = horizontalInput * moveSpeed * 0.35f;
            velocity.y = verticalInput * climbSpeed;
        }
        else
        {
            body.useGravity = true;
            velocity.x = horizontalInput * moveSpeed;

            if (jumpQueued && IsGrounded)
            {
                velocity.y = jumpSpeed;
            }
        }

        velocity.z = 0f;
        body.linearVelocity = velocity;
        jumpQueued = false;

        if (transform.position.y < -6f)
        {
            ResetToSpawn();
        }
    }

    private void LateUpdate()
    {
        if (visualRoot == null)
        {
            return;
        }

        Quaternion mirror = facing < 0f ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
        visualRoot.localRotation = visualRootBaseRotation * mirror;
    }

    public void ResetToSpawn()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody>();
        }

        transform.position = spawnPosition;
        body.position = spawnPosition;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.useGravity = true;
        IsClimbing = false;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 groundPosition = groundCheck != null
            ? groundCheck.position
            : transform.position + Vector3.down * 0.58f;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundPosition, groundCheckRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.1f, ladderCheckRadius);
    }
}
