using UnityEngine;

public readonly struct GripForceResult
{
    public GripForceResult(
        Vector2 error,
        Vector2 desiredVelocity,
        Vector2 force,
        bool reachClamped,
        bool forceClamped)
    {
        Error = error;
        DesiredVelocity = desiredVelocity;
        Force = force;
        ReachClamped = reachClamped;
        ForceClamped = forceClamped;
    }

    public Vector2 Error { get; }
    public Vector2 DesiredVelocity { get; }
    public Vector2 Force { get; }
    public bool ReachClamped { get; }
    public bool ForceClamped { get; }
}

/// <summary>
/// Pure 2D force math shared by the authoritative Cargo solver and tests.
/// Cargo mass is deliberately absent: equal grip force must accelerate heavy Cargo less.
/// </summary>
public static class GripForceModel
{
    public static GripForceResult Calculate(
        Vector2 cursorIntent,
        Vector2 gripPoint,
        Vector2 pointVelocity,
        float positionGain,
        float velocityGain,
        float maximumIntentSpeed,
        float maximumGripForce,
        float hardReach)
    {
        Vector2 rawError = cursorIntent - gripPoint;
        Vector2 error = Vector2.ClampMagnitude(rawError, Mathf.Max(0f, hardReach));
        bool reachClamped = rawError.sqrMagnitude > error.sqrMagnitude + 0.000001f;

        Vector2 desiredVelocity = Vector2.ClampMagnitude(
            error * Mathf.Max(0f, positionGain),
            Mathf.Max(0f, maximumIntentSpeed));

        Vector2 rawForce = (desiredVelocity - pointVelocity) * Mathf.Max(0f, velocityGain);
        Vector2 force = Vector2.ClampMagnitude(rawForce, Mathf.Max(0f, maximumGripForce));
        bool forceClamped = rawForce.sqrMagnitude > force.sqrMagnitude + 0.000001f;

        return new GripForceResult(error, desiredVelocity, force, reachClamped, forceClamped);
    }

    public static Vector2 CalculateAcceleration(Vector2 force, float mass)
    {
        return force / Mathf.Max(0.0001f, mass);
    }

    public static float CalculateTorqueZ(Vector2 worldPoint, Vector2 centerOfMass, Vector2 force)
    {
        Vector2 lever = worldPoint - centerOfMass;
        return lever.x * force.y - lever.y * force.x;
    }

    public static Vector2 ProjectXY(Vector3 value)
    {
        return new Vector2(value.x, value.y);
    }

    public static Vector3 ToWorld(Vector2 value, float z)
    {
        return new Vector3(value.x, value.y, z);
    }

    public static Vector2 ClampToRadius(Vector2 value, Vector2 origin, float radius)
    {
        return origin + Vector2.ClampMagnitude(value - origin, Mathf.Max(0f, radius));
    }

    public static Vector2 Quantize(Vector2 value, float step)
    {
        if (step <= 0f) return value;
        return new Vector2(
            Mathf.Round(value.x / step) * step,
            Mathf.Round(value.y / step) * step);
    }

    public static bool IsFinite(Vector2 value)
    {
        return float.IsFinite(value.x) && float.IsFinite(value.y);
    }
}
