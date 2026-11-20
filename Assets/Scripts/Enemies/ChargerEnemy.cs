using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Baseline enemy: chases the player, winds up, then charges in a straight line.
/// The charge hurts anything damageable in its way, including other enemies.
/// </summary>
public class ChargerEnemy : EnemyBrain
{
    [Header("Charge Attack")]
    [Tooltip("Keep this within the player's hand range, or the wind-up can't be parried.")]
    public float attackRange = 3.5f;
    public float windupDuration = 0.8f;
    public float chargeSpeed = 14f;
    public float chargeDistance = 10f;
    [Tooltip("Damage the charger takes for running into a wall or other solid obstacle.")]
    public float wallSlamDamage = 20f;

    [Header("Charge Hit Detection")]
    [SerializeField] private float hitRadius = 0.9f;
    [SerializeField] private float hitForwardOffset = 0.6f;
    [Tooltip("Slightly under the agent radius, so walls running alongside the charge don't count as ahead.")]
    [SerializeField] private float pushableCheckRadius = 0.45f;
    [SerializeField] private float pushableCheckDistance = 0.65f;
    [SerializeField] private float pushableCheckHeight = 1.5f;
    [SerializeField] private float pushableCheckFloorClearance = 0.05f;

    [Header("Placeholder Colors")]
    public Color chaseColor = new Color(0.75f, 0.35f, 0.2f);
    public Color windupColor = new Color(1f, 0.8f, 0.1f);
    public Color chargeColor = new Color(1f, 0.25f, 0.1f);
    public Color recoverColor = new Color(0.45f, 0.45f, 0.5f);

    private static readonly Collider[] HitBuffer = new Collider[16];

    public override void ReturnToDefaultState()
    {
        SwitchState(new ChargerChaseState(this));
    }

    /// <summary>True when the player is close enough and nothing blocks a straight run at them.</summary>
    public bool CanStartCharge()
    {
        if (!target || !CanNavigate) return false;

        var toTarget = target.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude > attackRange * attackRange) return false;

        // A player can stand just outside the baked NavMesh (or beside moved furniture).
        // NavMesh raycasts reject that endpoint even when the player is plainly visible.
        // Use physical visibility to start the attack; charge movement still respects NavMesh walls.
        return CombatQuery.IsClear(transform.position, target.position, transform, target);
    }

    /// <summary>Damages actors in front of the charger that have not already been hit.</summary>
    /// <returns>True if at least one new target was hit.</returns>
    public bool HitTargetsInFront(HashSet<IDamageable> alreadyHit)
    {
        var center = transform.position + transform.forward * hitForwardOffset;
        var count = Physics.OverlapSphereNonAlloc(center, hitRadius, HitBuffer, ~0, QueryTriggerInteraction.Ignore);

        var hitSomething = false;

        for (var i = 0; i < count; i++)
        {
            // Environmental damageables are handled by the obstacle impact path, which stuns us.
            if (!IsActor(HitBuffer[i])) continue;
            var damageable = HitBuffer[i].GetComponentInParent<IDamageable>();

            if (damageable == null || ReferenceEquals(damageable, this)) continue;
            if (!alreadyHit.Add(damageable)) continue;

            CombatVfx.BluntHit(HitBuffer[i], transform.position);
            damageable.TakeDamage(EffectiveDamage);
            hitSomething = true;
        }

        return hitSomething;
    }

    private static bool IsActor(Collider collider)
    {
        return collider.GetComponentInParent<EnemyBrain>()
            || collider.GetComponentInParent<PlayerHealth>()
            || collider.GetComponentInParent<LocomotionController>();
    }

    /// <summary>Finds solid scenery ahead, including furniture that is not in the baked NavMesh.</summary>
    public bool TryGetChargeObstacle(Vector3 direction, float step, out Collider obstacle)
    {
        var feet = transform.position - Vector3.up * agent.baseOffset;
        var bottom = feet + Vector3.up * (pushableCheckRadius + pushableCheckFloorClearance);
        var top = bottom + Vector3.up * pushableCheckHeight;

        // Inspect all layers and filter by identity: scenery can share an actor's layer.
        // Keep the nearest actor in the query so scenery behind them cannot turn their hit into a stun.
        var hits = Physics.CapsuleCastAll(bottom, top, pushableCheckRadius, direction,
            Mathf.Max(step, pushableCheckDistance), ~0, QueryTriggerInteraction.Ignore);
        obstacle = null;
        var nearestDistance = float.PositiveInfinity;
        foreach (var hit in hits)
        {
            var collider = hit.collider;
            if (collider.transform.IsChildOf(transform) || hit.distance >= nearestDistance) continue;
            nearestDistance = hit.distance;
            obstacle = collider;
        }

        return obstacle && !IsActor(obstacle);
    }

    /// <summary>An environmental crash interrupts the charge and drives the existing stun stars.</summary>
    public void HitChargeObstacle(Collider obstacle, Vector3 direction)
    {
        var pushable = obstacle.GetComponentInParent<Pushable>()
            || obstacle.GetComponentInParent<ThrowableFurniture>();
        var damageable = obstacle.GetComponentInParent<IDamageable>();
        CombatVfx.ChargeObstacle(transform, direction);
        SwitchState(new EnemyStunState(this, stunDuration, new ChargerRecoverState(this)));
        damageable?.TakeDamage(EffectiveDamage);
        if (!pushable) TakeDamage(wallSlamDamage);
    }

}
