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

    /// <summary>Damages everything in front of the charger that has not already been hit.</summary>
    /// <returns>True if at least one new target was hit.</returns>
    public bool HitTargetsInFront(HashSet<IDamageable> alreadyHit)
    {
        var center = transform.position + transform.forward * hitForwardOffset;
        var count = Physics.OverlapSphereNonAlloc(center, hitRadius, HitBuffer, ~0, QueryTriggerInteraction.Ignore);

        var hitSomething = false;

        for (var i = 0; i < count; i++)
        {
            var damageable = HitBuffer[i].GetComponentInParent<IDamageable>();

            if (damageable == null || ReferenceEquals(damageable, this)) continue;
            if (!alreadyHit.Add(damageable)) continue;

            damageable.TakeDamage(EffectiveDamage);
            hitSomething = true;
        }

        return hitSomething;
    }

    /// <summary>True if the first thing in the charger's path is pushable furniture rather than something fixed.</summary>
    public bool IsPushableAhead()
    {
        var feet = transform.position - Vector3.up * agent.baseOffset;
        var bottom = feet + Vector3.up * (pushableCheckRadius + pushableCheckFloorClearance);
        var top = bottom + Vector3.up * pushableCheckHeight;

        // Leaves out the enemy layer so the sweep can't stop on the charger's own body
        var hitSomething = Physics.CapsuleCast(bottom, top, pushableCheckRadius, transform.forward, out var hit,
            pushableCheckDistance, ~(1 << gameObject.layer), QueryTriggerInteraction.Ignore);

        return hitSomething && (hit.collider.GetComponentInParent<Pushable>() || hit.collider.GetComponentInParent<ThrowableFurniture>());
    }

}
