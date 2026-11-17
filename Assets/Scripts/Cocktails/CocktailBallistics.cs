using UnityEngine;

/// <summary>One swept flight model shared by the aiming preview and the thrown bottle.</summary>
public static class CocktailBallistics
{
    private static readonly RaycastHit[] sweepHits = new RaycastHit[32];
    public static float Speed => Mathf.Max(0.1f, CocktailSettings.Instance.throwSpeed);
    public static float Lift => CocktailSettings.Instance.upwardLift;
    public static float BottleRadius => Mathf.Max(0.01f, CocktailSettings.Instance.bottleCollisionRadius);
    public const float Step = 0.025f;
    public static float Lifetime => Mathf.Clamp(CocktailSettings.Instance.bottleLifetime, 0.1f, 30f);
    public static int MaxSteps => Mathf.CeilToInt(Lifetime / Step);

    public static void GetLaunch(Transform aim, out Vector3 position, out Vector3 velocity)
    {
        // Start at the camera, so a muzzle placed through a nearby wall cannot bypass it.
        position = aim.position;
        velocity = aim.forward * Speed + Vector3.up * Lift;
    }

    public static Vector3 Position(Vector3 origin, Vector3 velocity, float time)
    {
        return origin + velocity * time + Physics.gravity * (0.5f * time * time);
    }

    public static bool Sweep(Vector3 start, Vector3 end, Transform owner, out RaycastHit nearest)
    {
        nearest = default;
        var delta = end - start;
        if (delta.sqrMagnitude < 0.000001f) return false;
        var found = false;
        var count = Physics.SphereCastNonAlloc(start, BottleRadius, delta.normalized, sweepHits,
            delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
        // The common preview path allocates nothing. Dense collider piles still get a complete query.
        var hits = count < sweepHits.Length ? sweepHits : Physics.SphereCastAll(start, BottleRadius,
            delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
        if (hits != sweepHits) count = hits.Length;
        for (var i = 0; i < count; i++)
        {
            var hit = hits[i];
            if (IsOwner(hit.collider, owner)) continue;
            if (hit.distance <= 0f)
            {
                // Sphere casts can report a zero world-space point when starting against a wall.
                // Resolve that contact locally rather than drawing/landing a splash at world origin.
                var point = hit.collider.ClosestPoint(start);
                var normal = start - point;
                hit.point = point;
                hit.normal = normal.sqrMagnitude > 0.000001f ? normal.normalized : -delta.normalized;
            }
            if (!found || hit.distance < nearest.distance) { nearest = hit; found = true; }
        }
        return found;
    }

    private static bool IsOwner(Collider collider, Transform owner)
    {
        return owner && (collider.transform.IsChildOf(owner) || owner.IsChildOf(collider.transform));
    }

    /// <summary>Projects splashes below a hit patron or wall onto the supporting floor.</summary>
    public static bool GroundBelow(Vector3 point, Transform owner, out Vector3 ground, out Vector3 normal)
    {
        ground = point;
        normal = Vector3.up;
        var nearest = float.PositiveInfinity;
        var found = false;
        foreach (var hit in Physics.RaycastAll(point + Vector3.up * 0.4f, Vector3.down, 30f,
                     ~0, QueryTriggerInteraction.Ignore))
        {
            if (IsOwner(hit.collider, owner) || hit.collider.GetComponentInParent<EnemyBrain>()) continue;
            if (hit.collider.GetComponentInParent<PlayerHealth>() || Vector3.Dot(hit.normal, Vector3.up) < 0.5f) continue;
            // Loose props are not the tavern floor, and should not carry a hovering puddle.
            if (hit.rigidbody && !hit.rigidbody.isKinematic) continue;
            if (hit.distance >= nearest) continue;
            nearest = hit.distance;
            ground = hit.point;
            normal = hit.normal;
            found = true;
        }
        return found;
    }

    public static bool Reaches(EnemyBrain enemy, Vector3 center, float radius)
    {
        if (!enemy || enemy.IsDead || !enemy.isActiveAndEnabled || enemy.IsLeaving) return false;
        var offset = enemy.transform.position - center;
        // Keep floor splashes on the same storey, while allowing elevated enemy pivots.
        if (Mathf.Abs(offset.y) > 2.5f) return false;
        offset.y = 0f;
        if (offset.sqrMagnitude > radius * radius) return false;
        var targetPoint = enemy.transform.position + Vector3.up * 0.3f;
        var start = center + Vector3.up * 0.25f;
        var direction = targetPoint - start;
        foreach (var hit in Physics.RaycastAll(start, direction.normalized, direction.magnitude,
                     ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.GetComponentInParent<EnemyBrain>() || hit.collider.GetComponentInParent<PlayerHealth>()) continue;
            if (hit.rigidbody) continue;
            return false;
        }
        return true;
    }
}
