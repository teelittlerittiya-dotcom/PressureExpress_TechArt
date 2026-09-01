using UnityEngine;

[DisallowMultipleComponent]
public sealed class CursorIntentProvider : MonoBehaviour
{
    [SerializeField] private GripConfiguration configuration;

    private Camera localCamera;
    private Vector2 virtualHandOffset;
    private Vector2 lastMouseScreenPosition;
    private bool hasInitializedVirtualCursor;

    public Vector2 RawWorldIntent { get; private set; }
    public Vector2 CurrentWorldIntent { get; private set; }
    public bool HasValidIntent { get; private set; }
    public bool IsPointerWithinInteractionReach { get; private set; }
    public GripConfiguration Configuration => configuration;

    public void Configure(GripConfiguration newConfiguration)
    {
        configuration = newConfiguration;
    }

    private void OnEnable()
    {
        hasInitializedVirtualCursor = false;
    }

    public void ResetVirtualCursor()
    {
        hasInitializedVirtualCursor = false;
    }

    public bool RefreshIntent()
    {
        if (configuration == null)
        {
            InvalidateIntent();
            return false;
        }

        if (localCamera == null) localCamera = Camera.main;
        if (localCamera == null)
        {
            InvalidateIntent();
            return false;
        }

        Vector2 currentMouseScreen = Input.mousePosition;
        Vector2 origin = new Vector2(transform.position.x, transform.position.y);

        if (!hasInitializedVirtualCursor)
        {
            Ray pointerRay = localCamera.ScreenPointToRay(currentMouseScreen);
            Plane gameplayPlane = new Plane(Vector3.forward, new Vector3(0f, 0f, transform.position.z));
            if (!gameplayPlane.Raycast(pointerRay, out float distance))
            {
                InvalidateIntent();
                return false;
            }

            Vector3 world = pointerRay.GetPoint(distance);
            Vector2 raw = new Vector2(world.x, world.y);
            if (!GripForceModel.IsFinite(raw))
            {
                InvalidateIntent();
                return false;
            }

            virtualHandOffset = Vector2.ClampMagnitude(raw - origin, configuration.FreeHandRadius);
            lastMouseScreenPosition = currentMouseScreen;
            hasInitializedVirtualCursor = true;
        }
        else
        {
            Vector2 screenDelta = currentMouseScreen - lastMouseScreenPosition;
            lastMouseScreenPosition = currentMouseScreen;

            float worldUnitsPerPixel;
            if (localCamera.orthographic)
            {
                worldUnitsPerPixel = (localCamera.orthographicSize * 2f) / Mathf.Max(1f, Screen.height);
            }
            else
            {
                float distanceToPlane = Mathf.Abs(localCamera.transform.position.z - transform.position.z);
                float planeHeight = 2f * Mathf.Tan(localCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * distanceToPlane;
                worldUnitsPerPixel = planeHeight / Mathf.Max(1f, Screen.height);
            }

            Vector2 worldDelta = screenDelta * worldUnitsPerPixel;
            virtualHandOffset += worldDelta;
            virtualHandOffset = Vector2.ClampMagnitude(virtualHandOffset, configuration.FreeHandRadius);
        }

        Vector2 nextIntent = origin + virtualHandOffset;
        float deadZone = configuration.IntentChangeThreshold;
        if (HasValidIntent && deadZone > 0f
            && (nextIntent - CurrentWorldIntent).sqrMagnitude < deadZone * deadZone)
        {
            nextIntent = CurrentWorldIntent;
        }

        RawWorldIntent = nextIntent;
        CurrentWorldIntent = nextIntent;
        HasValidIntent = GripForceModel.IsFinite(CurrentWorldIntent);
        // Interaction remains within reach as long as intent is valid, since the hand position is clamped to the reach radius.
        IsPointerWithinInteractionReach = HasValidIntent;
        return HasValidIntent;
    }

    public bool TryGetSelectionRay(out Ray ray)
    {
        if (localCamera == null) localCamera = Camera.main;
        if (localCamera == null)
        {
            ray = default;
            return false;
        }

        if (HasValidIntent)
        {
            // Project selection ray through the clamped hand position so moving the mouse further
            // at maximum extension ("สุดมือแล้ว") locks selection to the hand's reach radius.
            Vector3 handWorld = new Vector3(CurrentWorldIntent.x, CurrentWorldIntent.y, transform.position.z);
            Vector3 screenPos = localCamera.WorldToScreenPoint(handWorld);
            ray = localCamera.ScreenPointToRay(screenPos);
            return true;
        }

        ray = localCamera.ScreenPointToRay(Input.mousePosition);
        return true;
    }

    private void InvalidateIntent()
    {
        HasValidIntent = false;
        IsPointerWithinInteractionReach = false;
        hasInitializedVirtualCursor = false;
    }
}
