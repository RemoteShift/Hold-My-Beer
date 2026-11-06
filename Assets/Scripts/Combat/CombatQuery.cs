using System.Collections.Generic;
using UnityEngine;

/// <summary>Shared area damage, with one hit per actor and solid scenery blocking attacks.</summary>
public static class CombatQuery
{
    public static bool IsClear(Vector3 origin, Vector3 point, Transform owner, Transform victim)
    {
        var delta = point - origin;
        foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(owner) || hit.transform.IsChildOf(victim)) continue;
            if (hit.collider.GetComponentInParent<EnemyBrain>() || hit.collider.attachedRigidbody) continue;
            return false;
        }
        return true;
    }

    public static void DamageArea(EnemyBrain owner, Vector3 center, float radius, float damage,
        HashSet<IDamageable> hitTargets, float arc = 360f, float push = 0f)
    {
        foreach (var collider in Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Ignore))
        {
            var victim = collider.GetComponentInParent<IDamageable>();
            if (victim == null || ReferenceEquals(victim, owner) || hitTargets.Contains(victim)) continue;
            var offset = collider.bounds.center - center;
            offset.y = 0f;
            if (arc < 360f && Vector3.Angle(owner.transform.forward, offset) > arc * 0.5f) continue;
            if (!IsClear(center, collider.bounds.center, owner.transform, collider.transform)) continue;
            hitTargets.Add(victim);
            victim.TakeDamage(damage);
            if (push > 0f && victim is EnemyBrain enemy && !enemy.IsDead) enemy.KnockbackFrom(center, push);
        }
    }
}
