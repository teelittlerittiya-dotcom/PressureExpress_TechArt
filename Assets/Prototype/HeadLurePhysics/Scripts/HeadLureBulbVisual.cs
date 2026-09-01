using UnityEngine;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public sealed class HeadLureBulbVisual : MonoBehaviour
{
    [Header("Bulb Sprite")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite bulbSprite;
    [SerializeField] private Color bulbColor = Color.white;
    [Tooltip("Final visible width and height in world units, independent of the source sprite's pixels-per-unit.")]
    [SerializeField] private Vector2 bulbSize = new Vector2(0.085f, 0.085f);
    [SerializeField, Range(0.1f, 1f)] private float colliderRadiusFraction = 0.36f;

    [Header("Authored Light References")]
    [Tooltip("Optional child 2D light. Its component settings and enabled state are never overwritten by this script.")]
    [SerializeField] private Light2D light2D;
    [Tooltip("Optional child 3D light. Configure its type, transform, color, intensity, range, angle, and shadows directly on the Light component.")]
    [SerializeField] private Light light3D;

    private Vector3 configuredSpriteScale = Vector3.one;
    private bool horizontallyMirrored;

    public SpriteRenderer Renderer => spriteRenderer;
    public Light2D TwoDimensionalLight => light2D;
    public Light ThreeDimensionalLight => light3D;
    public float PhysicsRadius => Mathf.Max(Mathf.Abs(bulbSize.x), Mathf.Abs(bulbSize.y)) * colliderRadiusFraction;
    public bool IsHorizontallyMirrored => horizontallyMirrored;

    private void Awake()
    {
        ResolveComponents();
        ApplyConfiguration();
    }

    private void Reset()
    {
        ResolveComponents();
        ApplyConfiguration();
    }

    private void OnValidate()
    {
        bulbSize.x = Mathf.Max(0.001f, bulbSize.x);
        bulbSize.y = Mathf.Max(0.001f, bulbSize.y);
        ResolveComponents();
        ApplyConfiguration();
    }

    public void ApplyConfiguration()
    {
        ResolveComponents();

        if (spriteRenderer != null)
        {
            if (bulbSprite != null)
            {
                spriteRenderer.sprite = bulbSprite;
            }

            spriteRenderer.color = bulbColor;

            Vector2 nativeSize = spriteRenderer.sprite != null
                ? spriteRenderer.sprite.bounds.size
                : Vector2.one;
            float nativeWidth = Mathf.Max(0.0001f, Mathf.Abs(nativeSize.x));
            float nativeHeight = Mathf.Max(0.0001f, Mathf.Abs(nativeSize.y));
            configuredSpriteScale = new Vector3(
                bulbSize.x / nativeWidth,
                bulbSize.y / nativeHeight,
                1f);
            ApplyHorizontalMirror();
        }
    }

    public void SetHorizontalMirror(bool shouldMirror)
    {
        if (horizontallyMirrored == shouldMirror)
        {
            return;
        }

        horizontallyMirrored = shouldMirror;
        ApplyHorizontalMirror();
    }

    private void ApplyHorizontalMirror()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        spriteRenderer.transform.localScale = configuredSpriteScale;
        spriteRenderer.flipX = horizontallyMirrored;
    }

    private void ResolveComponents()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        if (light2D == null)
        {
            light2D = GetComponentInChildren<Light2D>(true);
        }

        if (light3D == null)
        {
            light3D = GetComponentInChildren<Light>(true);
        }
    }
}
