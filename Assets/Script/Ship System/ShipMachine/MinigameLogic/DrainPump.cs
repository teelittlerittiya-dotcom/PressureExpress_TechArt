using UnityEngine;

public class DrainPump : MonoBehaviour
{
    [Header("Pump Handle")]
    [SerializeField] Transform pumpHandle;
    [SerializeField] Collider handleCollider;

    [Header("Movement")]
    [SerializeField] float maxDownDistance = 0.6f;
    [SerializeField] float dragSensitivity = 0.01f;

    [Header("Spring Back")]
    [SerializeField] float springForce = 20f;
    [SerializeField] float damping = 6f;

    float position;
    float velocity;
    bool dragging;
    float lastMouseY;
    Vector3 startLocalPos;

    private void Start()
    {
        startLocalPos = pumpHandle.localPosition;

        // Was a Collider2D field; a Collider2D reference cannot deserialize into a Collider,
        // so on an un-rewired prefab this is null and the handle is simply unclickable.
        if (handleCollider == null)
            Debug.LogError($"{name}: handleCollider is not assigned (was it a CapsuleCollider2D?). " +
                           "The pump handle will not respond to clicks.", this);
    }

    void Update()
    {
        HandleInput();

        if (!dragging)
            ApplySpring();

        ApplyMovement();
    }

    void HandleInput() 
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (handleCollider != null && handleCollider.Raycast(ray, out RaycastHit hitInfo, 100f))
            {
                dragging = true;
                lastMouseY = Input.mousePosition.y;
                velocity = 0f;
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            dragging = false;
        }

        if (!dragging) return;

        float deltaY = Input.mousePosition.y - lastMouseY;
        lastMouseY = Input.mousePosition.y;

        position -= deltaY * dragSensitivity;
        position = Mathf.Clamp01(position);
    }

    void ApplySpring()
    {
        float spring = -position * springForce;
        velocity += spring * Time.deltaTime;

        velocity -= velocity * damping * Time.deltaTime;

        position += velocity * Time.deltaTime;

        position = Mathf.Clamp01(position);
    }

    void ApplyMovement()
    {
        Vector3 localPos = startLocalPos;
        localPos.y += -position * maxDownDistance;
        pumpHandle.localPosition = localPos;
    }
}
