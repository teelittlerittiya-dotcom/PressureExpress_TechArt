using UnityEngine;

public class PlayerHandController : MonoBehaviour
{
    private Camera mainCamera;
    private Rigidbody rb;

    void Start()
    {
        mainCamera = Camera.main;
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        // Project mouse to a world plane at the object's Z position
        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = mainCamera.WorldToScreenPoint(transform.position).z;
        Vector3 mouseWorldPosition = mainCamera.ScreenToWorldPoint(mouseScreenPos);
        mouseWorldPosition.z = transform.position.z;
        rb.MovePosition(mouseWorldPosition);
    }
}