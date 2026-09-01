using UnityEngine;
using PressureExpress.Framework;

public class PressureMiniGame : MinigameBaseUI
{
    [Header("Pivots")]
    [SerializeField] private Transform topPivot;
    [SerializeField] private Transform bottomPivot;

    [Header("Marker")]
    [SerializeField] private Transform marker;
    private float markerPosition;
    private float markerDestination;
    private float markerTimer;
    private float markerSpeed;

    [SerializeField] private float timerMultiplicator = 3f;
    [SerializeField] private float smoothMotion = 0.3f;

    [Header("Hook")]
    [SerializeField] private Transform hook;
    private float hookPosition;
    private float hookPullVelocity;

    [SerializeField] private float hookPullPower = 5f;
    [SerializeField] private float hookGravityPower = 3f;

    [Header("Zone")]
    [SerializeField] private float hookSize = 0.08f;

    [Header("Progress")]
    [SerializeField] private float progressIncreaseSpeed = 0.4f;
    [SerializeField] private float progressDecreaseSpeed = 0.25f;
    private float hookProgress;

    [Header("Visual")]
    [SerializeField] private Transform progressBar;

    public override void ResetMinigame()
    {
        base.ResetMinigame();
        hookProgress = 0f;
        hookPosition = 0f;
        hookPullVelocity = 0f;
        markerPosition = 0f;
    }

    protected override void OnMinigameUpdate()
    {
        MarkerMovement();
        HookMovement();
        UpdateProgress();
    }

    private void MarkerMovement()
    {
        if (marker == null || topPivot == null || bottomPivot == null) return;
        markerTimer -= Time.deltaTime;

        if (markerTimer <= 0f)
        {
            markerTimer = Random.value * timerMultiplicator;
            markerDestination = Random.value;
        }

        markerPosition = Mathf.SmoothDamp(
            markerPosition,
            markerDestination,
            ref markerSpeed,
            smoothMotion
        );

        marker.position = Vector3.Lerp(
            bottomPivot.position,
            topPivot.position,
            markerPosition
        );
    }

    private void HookMovement()
    {
        if (hook == null || topPivot == null || bottomPivot == null) return;

        if (Input.GetMouseButton(0))
            hookPullVelocity += hookPullPower * Time.deltaTime;

        hookPullVelocity -= hookGravityPower * Time.deltaTime;
        hookPullVelocity = Mathf.Clamp(hookPullVelocity, -2f, 2f);

        hookPosition += hookPullVelocity * Time.deltaTime;
        hookPosition = Mathf.Clamp01(hookPosition);

        hook.position = Vector3.Lerp(
            bottomPivot.position,
            topPivot.position,
            hookPosition
        );
    }

    private void UpdateProgress()
    {
        float min = hookPosition - hookSize * 0.5f;
        float max = hookPosition + hookSize * 0.5f;

        bool isInZone = markerPosition > min && markerPosition < max;

        if (isInZone)
            hookProgress += progressIncreaseSpeed * Time.deltaTime;
        else
            hookProgress -= progressDecreaseSpeed * Time.deltaTime;

        hookProgress = Mathf.Clamp01(hookProgress);
        UpdateBarScaleY(progressBar, hookProgress);
        SetProgress(hookProgress);
    }
}
