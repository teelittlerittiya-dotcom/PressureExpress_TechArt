using UnityEngine;

[CreateAssetMenu(fileName = "Grip Configuration", menuName = "Pressure Express/Holding/Grip Configuration")]
public sealed class GripConfiguration : ScriptableObject
{
    [Header("Force Solver")]
    [SerializeField, Min(0.01f)] private float positionGain = 3f;
    [SerializeField, Min(0.01f)] private float velocityGain = 20f;
    [SerializeField, Min(0.01f)] private float maximumGripForce = 60f;
    [SerializeField, Min(0.01f)] private float maximumIntentSpeed = 3f;

    [Header("Reach And Contact")]
    [SerializeField, Min(0.01f)] private float freeHandRadius = 1f;
    [SerializeField, Min(0.01f)] private float initialGrabRange = 1.6f;
    [SerializeField, Min(0f)] private float grabContactTolerance = 0.035f;
    [SerializeField, Min(0.01f)] private float softReach = 0.75f;
    [SerializeField, Min(0.02f)] private float hardReach = 1.75f;
    [SerializeField, Min(0f)] private float hardReachGraceSeconds = 0.45f;
    [SerializeField, Min(0.01f)] private float staleIntentTimeoutSeconds = 0.8f;
    [SerializeField, Min(0f)] private float grabRetrySeconds = 0.1f;

    [Header("Network Intent")]
    [SerializeField, Range(1f, 60f)] private float intentSendRate = 25f;
    [SerializeField, Min(0.01f)] private float intentKeepaliveSeconds = 0.2f;
    [SerializeField, Min(0f)] private float intentChangeThreshold = 0.01f;
    [SerializeField, Min(0f)] private float intentQuantization = 0.01f;
    [SerializeField, Min(1f)] private float maximumWorldCoordinate = 1000f;

    [Header("Capacity And Debug")]
    [SerializeField, Range(1, 8)] private int maximumHolders = 4;
    [SerializeField] private bool drawDebugForces;

    public float PositionGain => positionGain;
    public float VelocityGain => velocityGain;
    public float MaximumGripForce => maximumGripForce;
    public float MaximumIntentSpeed => maximumIntentSpeed;
    public float FreeHandRadius => freeHandRadius;
    public float InitialGrabRange => initialGrabRange;
    public float GrabContactTolerance => grabContactTolerance;
    public float SoftReach => softReach;
    public float HardReach => hardReach;
    public float HardReachGraceSeconds => hardReachGraceSeconds;
    public float StaleIntentTimeoutSeconds => staleIntentTimeoutSeconds;
    public float GrabRetrySeconds => grabRetrySeconds;
    public float IntentSendRate => intentSendRate;
    public float IntentKeepaliveSeconds => intentKeepaliveSeconds;
    public float IntentChangeThreshold => intentChangeThreshold;
    public float IntentQuantization => intentQuantization;
    public float MaximumWorldCoordinate => maximumWorldCoordinate;
    public int MaximumHolders => maximumHolders;
    public bool DrawDebugForces => drawDebugForces;

    public bool ValidateConfiguration(out string error)
    {
        if (positionGain <= 0f || velocityGain <= 0f || maximumGripForce <= 0f || maximumIntentSpeed <= 0f)
        {
            error = "Force solver values must all be positive.";
            return false;
        }

        if (freeHandRadius <= 0f || initialGrabRange < freeHandRadius)
        {
            error = "Initial grab range must be at least the free-hand radius.";
            return false;
        }

        if (softReach <= 0f || hardReach <= softReach)
        {
            error = "Hard reach must be greater than soft reach.";
            return false;
        }

        if (staleIntentTimeoutSeconds <= intentKeepaliveSeconds)
        {
            error = "Stale timeout must be greater than the intent keepalive interval.";
            return false;
        }

        if (maximumHolders < 1)
        {
            error = "At least one holder must be allowed.";
            return false;
        }

        error = null;
        return true;
    }

    private void OnValidate()
    {
        positionGain = Mathf.Max(0.01f, positionGain);
        velocityGain = Mathf.Max(0.01f, velocityGain);
        maximumGripForce = Mathf.Max(0.01f, maximumGripForce);
        maximumIntentSpeed = Mathf.Max(0.01f, maximumIntentSpeed);
        freeHandRadius = Mathf.Max(0.01f, freeHandRadius);
        initialGrabRange = Mathf.Max(freeHandRadius, initialGrabRange);
        grabContactTolerance = Mathf.Max(0f, grabContactTolerance);
        softReach = Mathf.Max(0.01f, softReach);
        hardReach = Mathf.Max(softReach + 0.01f, hardReach);
        hardReachGraceSeconds = Mathf.Max(0f, hardReachGraceSeconds);
        staleIntentTimeoutSeconds = Mathf.Max(intentKeepaliveSeconds + 0.01f, staleIntentTimeoutSeconds);
        grabRetrySeconds = Mathf.Max(0f, grabRetrySeconds);
        intentSendRate = Mathf.Clamp(intentSendRate, 1f, 60f);
        intentKeepaliveSeconds = Mathf.Max(0.01f, intentKeepaliveSeconds);
        intentChangeThreshold = Mathf.Max(0f, intentChangeThreshold);
        intentQuantization = Mathf.Max(0f, intentQuantization);
        maximumWorldCoordinate = Mathf.Max(1f, maximumWorldCoordinate);
        maximumHolders = Mathf.Clamp(maximumHolders, 1, 8);
    }
}
