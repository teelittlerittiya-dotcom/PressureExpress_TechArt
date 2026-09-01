using UnityEngine;

public static class GripContactUtility
{
    public static bool TryFindContact(
        Collider[] handColliders,
        Collider[] cargoColliders,
        float tolerance,
        out Vector3 contactPoint)
    {
        contactPoint = default;
        if (handColliders == null || cargoColliders == null) return false;

        float allowedSeparation = Mathf.Max(0f, tolerance);
        float bestDistanceSquared = float.PositiveInfinity;
        Vector3 bestPoint = default;

        foreach (Collider hand in handColliders)
        {
            // The free Hand is intentionally a trigger: it must detect overlap without physically
            // pushing Cargo. Trigger colliders are still valid query shapes for the grab gate.
            if (hand == null || !hand.enabled) continue;

            Bounds expandedHandBounds = hand.bounds;
            expandedHandBounds.Expand(allowedSeparation * 2f);

            foreach (Collider cargo in cargoColliders)
            {
                if (cargo == null || !cargo.enabled || cargo.isTrigger) continue;
                if (!expandedHandBounds.Intersects(cargo.bounds)) continue;

                if (Physics.ComputePenetration(
                        hand, hand.transform.position, hand.transform.rotation,
                        cargo, cargo.transform.position, cargo.transform.rotation,
                        out Vector3 separationDirection, out float separationDistance))
                {
                    Vector3 cargoPoint = cargo.ClosestPoint(hand.bounds.center);
                    if (cargoPoint == hand.bounds.center)
                    {
                        cargoPoint -= separationDirection * separationDistance * 0.5f;
                    }

                    contactPoint = cargoPoint;
                    return true;
                }

                Vector3 handPoint = hand.ClosestPoint(cargo.bounds.center);
                Vector3 cargoPointNearHand = cargo.ClosestPoint(handPoint);
                handPoint = hand.ClosestPoint(cargoPointNearHand);
                float distanceSquared = (handPoint - cargoPointNearHand).sqrMagnitude;

                if (distanceSquared > allowedSeparation * allowedSeparation || distanceSquared >= bestDistanceSquared)
                {
                    continue;
                }

                bestDistanceSquared = distanceSquared;
                bestPoint = (handPoint + cargoPointNearHand) * 0.5f;
            }
        }

        if (float.IsPositiveInfinity(bestDistanceSquared)) return false;
        contactPoint = bestPoint;
        return true;
    }
}
